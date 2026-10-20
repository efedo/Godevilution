using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Optional resident observations come from actual clipped renderer draws. */
public partial class SessionAcceptance
{
    private Main? nativeTownerMain;
    private string? nativeTownerCaptureFailure;
    private object? nativeTownerColdReadiness;
    private readonly Dictionary<(int Slot, int Direction, int Frame), NativeTownerDrawObservation> nativeTownerSamples = [];

    private void StartNativeTownerCapture(Main main)
    {
        nativeTownerSamples.Clear();
        nativeTownerCaptureFailure = null;
        nativeTownerColdReadiness = null;
        nativeTownerMain = string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(NativeTownerAssets.ManifestEnvironmentVariable))
            ? null : main;
    }

    private async Task AwaitNativeTownerColdReadinessAsync(Main main)
    {
        if (nativeTownerMain is null)
            return;
        await Until(() => {
            CaptureNativeTownerDraws();
            Require(nativeTownerCaptureFailure is null, nativeTownerCaptureFailure ?? string.Empty);
            return main.TransportConnected && Settled(main) && main.ObservedPendingCommandCount == 0
                && main.ObservedNativeTownerDrawing.Any(draw => draw.NativeArt && draw.AnchorInsideViewport);
        }, "Cold resident presentation did not reach an actual native draw.", 10);
        // The observed _Draw completed on this same scene thread; no unbounded
        // render-signal wait is needed after the bounded actual-draw barrier.
        var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var startRevision = main.ObservedSnapshotRevision;
        var startTick = main.ObservedSnapshot!.Tick;
        // Existing receive timestamps distinguish fresh traffic from a startup backlog.
        // Waiting never queues input or changes the exact saved baseline below.
        await Until(() => {
            Require(main.TransportConnected && Settled(main) && main.ObservedPendingCommandCount == 0
                && main.ObservedLastQueuedSequence == 0, "Cold resident readiness queued input or lost its idle connection.");
            var report = main.ObservedCommandTimingReport;
            Require(main.ObservedClockGatedScheduling && report is not null,
                "Resident cold acceptance requires the existing command timing trace.");
            Require(report!.Commands.Count == 0 && report.DroppedRecordCount == 0,
                "Cold resident readiness dispatched input or lost timing observations.");
            var probe = report.ProbeSent.FirstOrDefault(row => row.SentStopwatchTimestamp > startTimestamp
                && row.WriteStartStopwatchTimestamp > startTimestamp && row.WriteEndStopwatchTimestamp >= row.WriteStartStopwatchTimestamp
                && row.WriteOutcome == AuthoritativeClientWriteOutcome.Completed);
            if (probe is null)
                return false;
            var reply = report.ProbeOutcomes.LastOrDefault(row => row.TimingRequestId == probe.TimingRequestId
                && row.TransportGeneration == probe.TransportGeneration && row.Outcome == AuthoritativeClientProbeOutcome.Reply
                && row.TimingCalibrationReady && row.OutcomeStopwatchTimestamp > probe.WriteEndStopwatchTimestamp);
            if (reply is null)
                return false;
            var samples = report.Snapshots.Where(row => row.TransportGeneration == probe.TransportGeneration
                    && row.ReceivedStopwatchTimestamp > reply.OutcomeStopwatchTimestamp && row.SnapshotTick > startTick)
                .DistinctBy(row => row.SnapshotTick).OrderBy(row => row.ReceivedStopwatchTimestamp).Take(2).ToArray();
            if (samples.Length != 2 || samples[1].SnapshotTick <= samples[0].SnapshotTick
                || main.ObservedSnapshotRevision < startRevision + 2 || main.ObservedSnapshot!.Tick < samples[1].SnapshotTick)
                return false;
            CaptureNativeTownerDraws();
            Require(nativeTownerCaptureFailure is null, nativeTownerCaptureFailure ?? string.Empty);
            if (!main.ObservedNativeTownerDrawing.Any(draw => draw.NativeArt && draw.AnchorInsideViewport))
                return false;
            nativeTownerColdReadiness = new {
                Passed = true, StartTimestamp = startTimestamp, report.StopwatchFrequency,
                StartRevision = startRevision, EndRevision = main.ObservedSnapshotRevision,
                StartTick = startTick, EndTick = main.ObservedSnapshot.Tick,
                Probe = probe, Reply = reply, Snapshots = samples,
            };
            return true;
        }, "Cold resident presentation did not observe a fresh clock reply and two subsequent snapshots.", 10);
    }

    private void CaptureNativeTownerDraws()
    {
        if (nativeTownerMain is not { } main)
            return;
        var draws = main.ObservedNativeTownerDrawing;
        if (draws.Select(row => row.ResidentSlot).Distinct().Count() != draws.Count)
            nativeTownerCaptureFailure = "The renderer drew a resident more than once, including a cow collision cell.";
        foreach (var draw in draws) {
            if (!draw.NativeArt || draw.GenericFallback || draw.AtlasPath is null)
                nativeTownerCaptureFailure = $"Requested Town resident art fell back for slot {draw.ResidentSlot}: {draw.Diagnostic}";
            if (draw.AnchorInsideViewport && nativeTownerSamples.Count < 512)
                nativeTownerSamples.TryAdd((draw.ResidentSlot, draw.DirectionIndex, draw.FrameIndex), draw);
        }
    }

    private async Task VerifyNativeTownerRoamingAsync(Main main)
    {
        if (nativeTownerMain is null)
            return;
        Require(nativeTownClassProfile, "Native resident acceptance requires a qualified Town profile.");
        var remaining = main.ObservedSnapshot!.NativeTownResidents.ToList();
        while (remaining.Count > 0) {
            CaptureNativeTownerDraws();
            Require(nativeTownerCaptureFailure is null, nativeTownerCaptureFailure ?? string.Empty);
            remaining.RemoveAll(row => nativeTownerSamples.Values.Where(draw => draw.ResidentSlot == row.ResidentSlot)
                .Select(draw => draw.FrameIndex).Distinct().Count() >= 2);
            if (remaining.Count == 0)
                break;
            var player = main.ObservedSnapshot!.Players.Single();
            var resident = remaining.MinBy(row => NativeDistance(player.PositionX, player.PositionY, row.PositionX, row.PositionY))!;
            if (resident.TownerId != 9) {
                await ApproachDeclaredDestination(main, $"towner:{resident.TownerId}", distanceBoundedApproachDeadline: true);
            } else {
                // Cows have no dialogue destination. Use ordinary visible destination
                // walks rather than a command/checkpoint for each adjacent tile.
                for (var step = 0; step < 48; step++) {
                    var snapshot = main.ObservedSnapshot!;
                    player = snapshot.Players.Single();
                    bool Goal(int x, int y) => NativeDistance(x, y, resident.PositionX, resident.PositionY) <= 3;
                    if (Goal(player.PositionX, player.PositionY))
                        break;
                    var next = FindNativeTownerViewingWaypoint(main, snapshot, player, DepthGeometry(main, 0), Goal);
                    Require(next is not null, "No ordinary Town path reaches the cow viewing position.");
                    var sequence = main.ObservedLastQueuedSequence;
                    await Click(main.ObservedTileScreenPosition(next!.Value.X, next.Value.Y), walk: true);
                    await AcceptedAndSettled(main, sequence, "Native cow viewing walk");
                    var arrived = main.ObservedSnapshot!.Players.Single();
                    Require(main.ObservedCommandResult!.ClientSequence == sequence + 1
                        && arrived.LevelId == 0 && arrived.PositionX == next.Value.X && arrived.PositionY == next.Value.Y,
                        "Native cow viewing walk did not accept its exact receipt and arrive at the clicked waypoint.");
                    Require(step < 47, "Cow viewing walk exceeded its bounded path.");
                }
            }
            await Until(() => {
                CaptureNativeTownerDraws();
                return nativeTownerSamples.Values.Where(draw => draw.ResidentSlot == resident.ResidentSlot)
                    .Select(draw => draw.FrameIndex).Distinct().Count() >= 2;
            }, $"Town resident {resident.ResidentSlot} did not issue two actual native frames.", 12);
        }
    }

    private static (int X, int Y)? FindNativeTownerViewingWaypoint(Main main, Snapshot snapshot,
        PlayerSnapshot player, LevelGeometrySnapshot geometry, Func<int, int, bool> goal)
    {
        var start = (X: player.PositionX, Y: player.PositionY);
        var parents = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current)) {
            if (current != start && goal(current.X, current.Y)) {
                var path = new List<(int X, int Y)>();
                for (var cell = current; cell != start; cell = parents[cell]) {
                    if (path.Count >= 256)
                        return null;
                    path.Add(cell);
                }
                path.Reverse();
                (int X, int Y)? waypoint = null;
                // Bound each ordinary walk as well as total route/leg counts.
                foreach (var cell in path.Take(8)) {
                    if (!IsRenderedWorldTileVisible(main, cell.X, cell.Y))
                        break;
                    waypoint = cell;
                }
                return waypoint;
            }
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) }) {
                var next = (X: current.X + dx, Y: current.Y + dy);
                if (parents.ContainsKey(next) || !IsNativeApproachCellOpen(main, snapshot, player, geometry, next.X, next.Y))
                    continue;
                if (dx != 0 && dy != 0 && (!IsNativeApproachCellOpen(main, snapshot, player, geometry, current.X + dx, current.Y)
                    || !IsNativeApproachCellOpen(main, snapshot, player, geometry, current.X, current.Y + dy)))
                    continue;
                parents.Add(next, current);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private object? VerifyNativeTownerCapture(Main main, bool resumed)
    {
        if (nativeTownerMain is null)
            return null;
        CaptureNativeTownerDraws();
        Require(nativeTownerCaptureFailure is null, nativeTownerCaptureFailure ?? string.Empty);
        var pack = main.ObservedNativeTownerPack;
        Require(pack is { IsLoaded: true, IdentityMatched: true, AtlasesReady: true }
            && pack.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash
            && pack.TownCatalogFingerprint == main.ObservedSnapshot!.NativeTownCatalogFingerprint
            && pack.MissingAtlasPaths.Count == 0 && pack.LoadedAtlasPaths.Count > 0,
            "Native resident art lost its content, Town roster or complete atlas binding.");
        var path = System.Environment.GetEnvironmentVariable(NativeTownerAssets.ManifestEnvironmentVariable)!;
        Require(NativeTownerCatalog.TryParse(Godot.FileAccess.GetFileAsString(path), out var catalog, out _),
            "The observed resident catalog cannot be independently parsed.");
        foreach (var draw in nativeTownerSamples.Values) {
            var row = main.ObservedSnapshot!.NativeTownResidents.Single(row => row.ResidentSlot == draw.ResidentSlot);
            var metadata = catalog!.Residents[draw.ResidentSlot];
            Require(catalog.TryGetSprite(metadata.SpriteId, out var sprite), "The observed resident sprite is absent.");
            var frame = sprite.ResolveFrame(draw.CosmeticTick, row.NativeDirection);
            Require(draw.AnchorInsideViewport && draw.TownerId == row.TownerId && draw.PositionX == row.PositionX && draw.PositionY == row.PositionY
                && draw.NativeDirection == row.NativeDirection
                && draw.DirectionIndex == frame.DirectionIndex && draw.FrameIndex == frame.FrameIndex
                && draw.AlignmentWidth == sprite.AlignmentWidth
                && draw.DrawX == draw.AnchorX - (sprite.AlignmentWidth - 64) / 2f
                && draw.DrawY == draw.AnchorY - frame.Height + 1,
                "An actual resident draw changed its authoritative identity, native group, frame order or bottom anchor.");
        }
        Require(nativeTownerSamples.Count > 0, "No actual native resident raster draw was observed.");
        if (!resumed) {
            Require(main.ObservedSnapshot!.NativeTownResidents.Count == 11
                && Enumerable.Range(0, 11).All(slot => nativeTownerSamples.Values.Where(draw => draw.ResidentSlot == slot)
                    .Select(draw => draw.FrameIndex).Distinct().Count() >= 2),
                "The class journey did not observe all eight humans and three cows advancing native frames.");
        }
        return new {
            Passed = true, Pack = pack, Samples = nativeTownerSamples.Values.ToArray(),
            ColdReadiness = nativeTownerColdReadiness,
            CompleteRosterRoaming = !resumed, CosmeticAnimation = true,
            NativeInitializationAndColdPhaseParity = false, RenderedParity = false,
        };
    }
}
