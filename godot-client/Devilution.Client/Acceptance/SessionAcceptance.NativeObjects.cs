using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/** Records actual object draws during the ordinary class route and exact pre-input cold retention. */
public partial class SessionAcceptance
{
    private sealed record NativeObjectArtInteraction(uint EntityId, uint NativeObjectId, uint BeforeFrame, uint AfterFrame,
        ulong Sequence, ulong AppliedTick, NativeObjectDrawObservation BeforeDraw, NativeObjectDrawObservation AfterDraw);
    private sealed record NativeObjectArtCheckpoint(string ContentHash, string[] Objects, string GeometryHash,
        NativeObjectArtInteraction Interaction);
    private Main? nativeObjectMain;
    private string? nativeObjectCaptureFailure;
    private NativeObjectArtInteraction? nativeObjectInteraction;
    private NativeObjectArtCheckpoint? nativeObjectCheckpoint;
    private bool nativeObjectColdVerified;
    private readonly Dictionary<(uint Entity, uint Frame, bool Activated, bool TrapArmed), NativeObjectDrawObservation> nativeObjectSamples = [];

    private void StartNativeObjectCapture(Main main)
    {
        nativeObjectMain = string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(NativeObjectAssets.ManifestEnvironmentVariable)) ? null : main;
        nativeObjectCaptureFailure = null; nativeObjectInteraction = null; nativeObjectCheckpoint = null;
        nativeObjectColdVerified = false; nativeObjectSamples.Clear();
    }

    private static bool IsNativeObjectArtId(uint id) => id is 1 or 2 or 5 or 6 or 7 or 48 or 53 or 54 or 57 or 58;

    private void CaptureNativeObjectDraws()
    {
        if (nativeObjectMain is not { ObservedSnapshot: { } snapshot } main)
            return;
        var draws = main.ObservedNativeObjectDrawing.Where(draw => draw.LevelId == 1 && IsNativeObjectArtId(draw.NativeObjectId)).ToArray();
        if (draws.Select(draw => draw.EntityId).Distinct().Count() != draws.Length)
            nativeObjectCaptureFailure = "The renderer drew a native object more than once at its occupied cells.";
        foreach (var draw in draws) {
            if (!draw.NativeArt || draw.GenericFallback || draw.AtlasPath is null)
                nativeObjectCaptureFailure = $"Requested L1 object art fell back for {draw.EntityId}/{draw.NativeObjectId}: {draw.Diagnostic}";
            if (draw.SnapshotTick == snapshot.Tick) {
                var state = snapshot.Objects.SingleOrDefault(state => state.EntityId == draw.EntityId);
                if (state is null || state.AnimationFrame != draw.NativeFrame || state.Activated != draw.Activated
                    || state.PositionX != draw.PositionX || state.PositionY != draw.PositionY || state.TrapArmed != draw.TrapArmed)
                    nativeObjectCaptureFailure = "A native object draw differed from its current authoritative snapshot.";
            }
            if (draw.AnchorInsideViewport && nativeObjectSamples.Count < 512)
                nativeObjectSamples.TryAdd((draw.EntityId, draw.NativeFrame, draw.Activated, draw.TrapArmed), draw);
        }
    }

    private async Task<NativeObjectDrawObservation> AwaitNativeObjectDraw(Main main, uint id, uint frame)
    {
        await Until(() => {
            CaptureNativeObjectDraws();
            Require(nativeObjectCaptureFailure is null, nativeObjectCaptureFailure ?? string.Empty);
            return main.ObservedNativeObjectDrawing.Any(draw => draw.EntityId == id && draw.NativeFrame == frame
                && draw.NativeArt && !draw.GenericFallback && draw.AnchorInsideViewport);
        }, $"Native object {id} frame {frame} did not issue an actual in-viewport draw.", 10);
        return main.ObservedNativeObjectDrawing.Single(draw => draw.EntityId == id && draw.NativeFrame == frame);
    }

    private async Task VerifyNativeObjectInteractionAsync(Main main)
    {
        if (nativeObjectMain is null)
            return;
        var candidate = FindReachableNativeChest(main.ObservedSnapshot!, []);
        Require(candidate is not null, "No ordinary reachable untrapped L1 chest is available for object-art acceptance.");
        for (var work = 0; work < 64; work++) {
            await UseNativeHealingPotionIfNeeded(main);
            var snapshot = main.ObservedSnapshot!; var player = snapshot.Players.Single();
            var target = FindNativeChest(snapshot, candidate!.EntityId);
            Require(player.Life > 0 && player.LevelId == 1, "The object-art approach did not remain alive in L1.");
            if (Math.Abs(player.PositionX - target.PositionX) + Math.Abs(player.PositionY - target.PositionY) == 1)
                break;
            var path = FindNativeChestApproachPath(snapshot, DepthGeometry(main, 1), target);
            Require(path is { Count: > 0 }, "The native object viewing path became blocked.");
            var steps = path!.Take(12).Where(cell => IsRenderedWorldTileVisible(main, cell.X, cell.Y)).ToArray();
            Require(steps.Length > 0, "No ordinary visible waypoint reaches the native object.");
            var waypoint = steps[^1]; var sequence = main.ObservedLastQueuedSequence;
            await Click(main.ObservedTileScreenPosition(waypoint.X, waypoint.Y), walk: true);
            await AcceptedAndSettled(main, sequence, "Native object viewing walk");
            var arrived = main.ObservedSnapshot!.Players.Single();
            Require(main.ObservedCommandResult!.ClientSequence == sequence + 1
                && arrived.PositionX == waypoint.X && arrived.PositionY == waypoint.Y,
                "The ordinary object-art waypoint did not accept its exact receipt and arrive.");
            Require(work < 63, "The native object-art approach exceeded its bounded work.");
        }
        var before = FindNativeChest(main.ObservedSnapshot!, candidate!.EntityId).Clone();
        Require(AuthoritativeObjectSelector.SelectAt(main.ObservedSnapshot!, before.PositionX, before.PositionY)?.EntityId == before.EntityId,
            "The ordinary object selector did not select the viewed native chest.");
        var beforeDraw = await AwaitNativeObjectDraw(main, before.EntityId, before.AnimationFrame);
        var geometry = HashProto(DepthGeometry(main, 1)); var prior = main.ObservedLastQueuedSequence;
        await ClickWorld(main, before.PositionX, before.PositionY);
        await AcceptedAndSettled(main, prior, "Open native chest with optional original art");
        var after = FindNativeChest(main.ObservedSnapshot!, before.EntityId);
        var receipt = main.ObservedCommandResult!.Clone();
        Require(receipt.ClientSequence == prior + 1 && after.NativeObjectId == before.NativeObjectId
            && after.Activated && !after.Destroyed && !after.NativeChestInteractable
            && after.AnimationFrame == before.AnimationFrame + 2 && HashProto(DepthGeometry(main, 1)) == geometry,
            "The ordinary native chest interaction changed its frame mapping, identity or collision geometry.");
        var afterDraw = await AwaitNativeObjectDraw(main, after.EntityId, after.AnimationFrame);
        nativeObjectInteraction = new(before.EntityId, before.NativeObjectId, before.AnimationFrame, after.AnimationFrame,
            receipt.ClientSequence, receipt.AppliedTick, beforeDraw, afterDraw);
    }

    private static string[] NativeObjectPayloads(Snapshot snapshot)
        => snapshot.Objects.Where(state => state.LevelId == 1 && IsNativeObjectArtId(state.NativeObjectId))
            .OrderBy(state => state.EntityId).Select(state => Convert.ToBase64String(state.ToByteArray())).ToArray();

    private void SaveNativeObjectCheckpoint(Main main, string directory)
    {
        if (nativeObjectMain is null)
            return;
        Require(nativeObjectInteraction is not null, "The native object-art journey omitted its ordinary interaction.");
        nativeObjectCheckpoint = new(main.ObservedIdentity!.ContentManifestHash, NativeObjectPayloads(main.ObservedSnapshot!),
            HashProto(DepthGeometry(main, 1)), nativeObjectInteraction!);
        WriteEvidence(directory, "native-object-art-checkpoint.json", nativeObjectCheckpoint);
    }

    private void VerifyNativeObjectColdBaseline(Main main, string directory)
    {
        if (nativeObjectMain is null)
            return;
        nativeObjectCheckpoint = JsonSerializer.Deserialize<NativeObjectArtCheckpoint>(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "native-object-art-checkpoint.json")))
            ?? throw new InvalidDataException("The native object-art checkpoint is empty.");
        Require(main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0
            && nativeObjectCheckpoint.ContentHash == main.ObservedIdentity!.ContentManifestHash
            && NativeObjectPayloads(main.ObservedSnapshot!).SequenceEqual(nativeObjectCheckpoint.Objects)
            && HashProto(DepthGeometry(main, 1)) == nativeObjectCheckpoint.GeometryHash,
            "Native object identity/frame/state or L1 collision geometry changed before the first cold input.");
        nativeObjectColdVerified = true;
    }

    private object? VerifyNativeObjectCapture(Main main, bool resumed)
    {
        if (nativeObjectMain is null)
            return null;
        CaptureNativeObjectDraws();
        Require(nativeObjectCaptureFailure is null, nativeObjectCaptureFailure ?? string.Empty);
        var pack = main.ObservedNativeObjectPack;
        Require(pack is { IsLoaded: true } && pack.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash
            && nativeObjectCheckpoint is { Objects.Length: > 0 }, "Native object art lost its catalog, identity or retained object family.");
        if (resumed)
            Require(nativeObjectColdVerified, "Native object cold frame/state/collision retention was not checked before input.");
        else
            Require(pack is { AtlasesReady: true } && pack.LoadedAtlasPaths.Count == 8 && pack.MissingAtlasPaths.Count == 0
                && nativeObjectSamples.Count > 0 && nativeObjectInteraction is not null,
                "Native object acceptance omitted its complete atlas preflight, actual draws or ordinary interaction.");
        return new {
            Passed = true, Pack = pack, Samples = nativeObjectSamples.Values.ToArray(),
            Checkpoint = nativeObjectCheckpoint, ColdObjectFrameStateCollisionRetained = nativeObjectColdVerified,
            ColdL1RedrawVerified = false, CurrentSnapshotFramesOnly = true,
            NativeLifecycleTimingParity = false, NativeLightingAndScenePhaseParity = false, RenderedParity = false,
        };
    }
}
