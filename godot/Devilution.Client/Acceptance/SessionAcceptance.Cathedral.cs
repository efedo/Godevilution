using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** Native geometry playtest through ordinary controls; population is explicitly authored. */
public partial class SessionAcceptance
{
    private async Task VerifyGeneratedCathedralAsync(Main main, string directory, string phase, bool visible)
    {
        Require(!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_PROFILE")),
            "Generated acceptance requires an isolated compiled playtest profile.");
        if (phase == "CathedralJourney") {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 0, "The journey must walk from Town, not start inside the dungeon.");
            await TravelCathedralRoute(main, directory, 1001, 1, stopEarly: false);
            var geometry = CathedralGeometry(main);
            Require(geometry.Width == 112 && geometry.Height == 112
                && geometry.NativePieceLayer.Count == 112 * 112 && geometry.NativeTransparency.Count == 112 * 112
                && geometry.NativeMegaTileLayer.Count == 0 && geometry.BlockedCells.Count > 0
                && geometry.NativePieceLayer.Count(piece => piece == 128) == 1
                && geometry.NativePieceLayer.Count(piece => piece == 114) == 1,
                "The scene did not receive ordinary generated Cathedral fine geometry.");
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-entry.png"));

            var target = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == 21000);
            Require(target.LevelId == 1 && !target.Activated, "The diagnostic object was not present and inactive.");
            var sequence = main.ObservedCommandResult!.ClientSequence;
            await ClickWorld(main, target.PositionX, target.PositionY);
            await Until(() => main.ObservedSnapshot!.Objects.Any(item => item.EntityId == target.EntityId && item.Activated)
                && Settled(main) && main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && IsAcceptedOrSuccessfulDuplicate(result), "The generated-level object did not activate through ordinary controls.", 15);
            await VerifyCombatAndLoot(main, directory, visible, generated: true);
            await TravelCathedralRoute(main, directory, 1002, 2, stopEarly: true);
            await VerifyCathedralCollision(main, directory);
            await MoveCathedralNeighbor(main);
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-traversal.png"));
            WriteEvidence(directory, "cathedral-state.json", new {
                Passed = true, GeometryHash = CathedralGeometryHash(main), ObjectEntityId = target.EntityId,
                ObjectActivated = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == target.EntityId).Activated,
            });
        } else {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 1, "Fresh-host restore did not return to the moved generated-level position.");
            VerifyCombatLootRestored(main, directory, "cathedral");
            using var restored = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedral-state.json")));
            var expected = restored.RootElement;
            Require(expected.GetProperty("Passed").GetBoolean()
                && expected.GetProperty("GeometryHash").GetString() == CathedralGeometryHash(main),
                "Fresh-host restore changed generated fine pieces, transparency, blockers or routes.");
            Require(main.ObservedSnapshot!.Objects.Single(item => item.EntityId == expected.GetProperty("ObjectEntityId").GetUInt32()).Activated
                == expected.GetProperty("ObjectActivated").GetBoolean(), "Fresh-host restore lost the generated-level object state.");
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-restored.png"));
            var player = main.ObservedSnapshot!.Players.Single().Clone();
            var offset = SelectCathedralLootStep(main, new WorldItemSnapshot {
                PositionX = player.PositionX, PositionY = player.PositionY,
            }, []);
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await Click(main.ObservedTileScreenPosition(player.PositionX + offset.X, player.PositionY + offset.Y), walk: true);
            await AcceptedAndSettled(main, sequence, "Fresh-host generated-level movement");
            var previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedraljourney.json")))!;
            Require(main.ObservedCommandResult!.ClientSequence > previous.Sequence,
                "The fresh client reused the persisted generated-level command sequence.");
            Require(main.ObservedSnapshot!.Players.Single().PositionX == player.PositionX + offset.X
                && main.ObservedSnapshot.Players.Single().PositionY == player.PositionY + offset.Y,
                "Fresh-host generated-level movement did not reach the selected walkable cell.");
            await TravelCathedralRoute(main, directory, 1099, 0, stopEarly: false);
            var returned = main.ObservedSnapshot!.Players.Single();
            Require(returned.PositionX == 25 && returned.PositionY == 31,
                "The native return stairs did not reach their non-trigger Town endpoint.");
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-return.png"));
        }
        // This delay lets periodic persistence catch up; only the fresh-host phase proves durability.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var final = main.ObservedSnapshot!.Players.Single();
        var identity = main.ObservedIdentity!;
        var lastSequence = main.ObservedCommandResult!.ClientSequence;
        WriteEvidence(directory, phase.ToLowerInvariant() + ".json", new Evidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            final.EntityId, final.CharacterClassId, final.LevelId, final.PositionX, final.PositionY, lastSequence, visible));
        GD.Print($"Godot generated Cathedral acceptance passed: {phase}; visible={visible}; sequence={lastSequence}");
    }

    private async Task TravelCathedralRoute(Main main, string directory, uint warpId, uint destinationLevel, bool stopEarly,
        int minimumDistinctPositions = 8, bool distanceBoundedApproachDeadline = false)
    {
        await Until(() => Settled(main), "Generated-route start did not settle.");
        var origin = main.ObservedSnapshot!.Players.Single().Clone();
        var routes = main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == origin.LevelId)
            .WarpTriggers.OrderBy(route => route.WarpId).ToArray();
        var index = Array.FindIndex(routes, route => route.WarpId == warpId);
        Require(index >= 0 && routes[index].Available && routes[index].TargetLevelId == destinationLevel, "The server did not publish the requested native route.");
        var traversalDeadlineSeconds = distanceBoundedApproachDeadline
            ? DestinationApproachDeadlineSeconds(origin.PositionX, origin.PositionY,
                routes[index].TriggerX, routes[index].TriggerY, distanceBounded: true)
            : 120;
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await SelectTab(sections, 4);
        await SelectRoute((OptionButton)main.FindChild("Routes", true, false), index);
        await ClickControl(main, sections, 4, "Travel");
        // The click is dispatched by the next scene input/process cycle. Do
        // not treat the unchanged pre-dispatch HUD as stopped travel.
        await Until(() => !((Button)main.FindChild("CancelTravel", true, false)).Disabled,
            "The real Travel button did not start generated-route input.", 5);
        var samples = new List<object>();
        var positions = new HashSet<(int X, int Y)> { (origin.PositionX, origin.PositionY) };
        CathedralWallCheckpoint? boundary = null;
        ulong? cancellationSequence = null;
        var manualWallSetupSteps = 0;
        ulong lastTick = ulong.MaxValue;
        try {
            await Until(() => {
                var snapshot = main.ObservedSnapshot!;
                var player = snapshot.Players.Single();
                Require(player.Life > 0, "The character died during generated-route traversal.");
                if (player.LevelId != origin.LevelId) {
                    Require(!stopEarly && player.LevelId == destinationLevel, "Traversal left the generated level before the checkpoint.");
                    return Settled(main);
                }
                var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == origin.LevelId);
                Require(!geometry.BlockedCells.Contains(player.PositionY * geometry.Width + player.PositionX),
                    "Authoritative traversal entered a projected blocked fine cell.");
                positions.Add((player.PositionX, player.PositionY));
                if (snapshot.Tick != lastTick) {
                    lastTick = snapshot.Tick;
                    if (samples.Count < 1024)
                        samples.Add(new { snapshot.Tick, player.LevelId, X = player.PositionX, Y = player.PositionY,
                            Sequence = main.ObservedCommandResult?.ClientSequence, Pending = main.ObservedPendingCommandCount });
                }
                Require(!((Button)main.FindChild("CancelTravel", true, false)).Disabled,
                    $"Generated-route traversal stopped: {main.ObservedCommandResult}; {((Label)main.FindChild("TravelStatus", true, false)).Text}");
                if (!stopEarly || positions.Count < 16)
                    return false;
                boundary = FindAdjacentCathedralWall(main);
                return boundary is not null;
            }, "Continuous generated-route movement did not reach its boundary.", traversalDeadlineSeconds);
            if (stopEarly) {
                await ClickControl(main, sections, 4, "CancelTravel");
                await Until(() => ((Button)main.FindChild("CancelTravel", true, false)).Disabled
                    && main.ObservedCommandResult is { } settledResult
                    && settledResult.ClientSequence == main.ObservedLastQueuedSequence
                    && main.ObservedSnapshot!.Tick >= settledResult.AppliedTick && Settled(main),
                    "The final generated traversal step did not settle after cancellation.", 10);
                Require(IsAcceptedOrSuccessfulDuplicate(main.ObservedCommandResult!),
                    "The final generated traversal step did not receive an accepted authoritative receipt.");
                Require(main.ObservedSnapshot!.Players.Single().LevelId == 1 && positions.Count >= 16,
                    "No continuously moved generated-level checkpoint was established.");
                var stopped = main.ObservedSnapshot.Players.Single().Clone();
                var stoppedSequence = main.ObservedCommandResult!.ClientSequence;
                var stoppedAppliedTick = main.ObservedCommandResult.AppliedTick;
                var stoppedTick = main.ObservedSnapshot.Tick;
                cancellationSequence = stoppedSequence;
                await Until(() => main.ObservedSnapshot!.Tick >= stoppedTick + 4,
                    "No snapshots arrived to verify generated-route cancellation.", 5);
                Require(main.ObservedLastQueuedSequence == stoppedSequence && main.ObservedCommandResult!.ClientSequence == stoppedSequence
                    && main.ObservedSnapshot.Players.Single().PositionX == stopped.PositionX
                    && main.ObservedSnapshot.Players.Single().PositionY == stopped.PositionY && Settled(main),
                    "Cancelled generated travel continued submitting commands or moving the player.");
                Require(boundary is not null, "The generated route did not capture its adjacent-wall boundary.");
                var capturedBoundary = boundary!;
                var cancellationWall = FindAdjacentCathedralWall(main);
                WriteEvidence(directory, $"cathedral-route-{warpId}-cancellation.json", new {
                    Passed = true, StrictCancellationSettled = true,
                    BoundaryX = capturedBoundary.PlayerX, BoundaryY = capturedBoundary.PlayerY,
                    BoundaryWallX = capturedBoundary.WallX, BoundaryWallY = capturedBoundary.WallY,
                    CancellationX = stopped.PositionX, CancellationY = stopped.PositionY,
                    CancellationWallX = cancellationWall?.WallX, CancellationWallY = cancellationWall?.WallY,
                    CancellationSequence = stoppedSequence, CancellationAppliedTick = stoppedAppliedTick,
                    CancellationSnapshotTick = stoppedTick,
                    RetainedBoundary = cancellationWall is not null
                        && stopped.PositionX == capturedBoundary.PlayerX && stopped.PositionY == capturedBoundary.PlayerY
                        && cancellationWall.WallX == capturedBoundary.WallX && cancellationWall.WallY == capturedBoundary.WallY,
                });
                var setupSteps = await RestoreCathedralWallBoundary(main, capturedBoundary);
                manualWallSetupSteps = setupSteps;
                var finalWall = FindAdjacentCathedralWall(main);
                var finalPlayer = main.ObservedSnapshot!.Players.Single();
                Require(finalWall is not null && finalWall.WallX == capturedBoundary.WallX && finalWall.WallY == capturedBoundary.WallY
                    && finalPlayer.PositionX == capturedBoundary.PlayerX && finalPlayer.PositionY == capturedBoundary.PlayerY,
                    "Ordinary movement did not restore the captured adjacent-wall checkpoint.");
                WriteEvidence(directory, $"cathedral-route-{warpId}-manual-wall-setup.json", new {
                    Passed = true, Steps = setupSteps,
                    TargetX = capturedBoundary.PlayerX, TargetY = capturedBoundary.PlayerY,
                    TargetWallX = capturedBoundary.WallX, TargetWallY = capturedBoundary.WallY,
                    FinalSequence = main.ObservedCommandResult!.ClientSequence,
                    FinalAppliedTick = main.ObservedCommandResult.AppliedTick,
                });
            } else {
                Require(positions.Count >= minimumDistinctPositions, "The route boundary was reached without continuous walking.");
            }
            WriteEvidence(directory, $"cathedral-route-{warpId}.json", new {
                Passed = true, WarpId = warpId, SourceLevel = origin.LevelId, TargetLevel = destinationLevel,
                StoppedInsideSource = stopEarly, DistinctObservedPositions = positions.Count,
                MinimumDistinctPositions = minimumDistinctPositions,
                CancellationSequence = cancellationSequence, ManualWallSetupSteps = manualWallSetupSteps,
                Sequence = main.ObservedCommandResult!.ClientSequence,
            });
        } finally { WriteEvidence(directory, $"cathedral-route-{warpId}-trace.json", samples); }
    }

    private async Task VerifyCathedralCollision(Main main, string directory)
    {
        var player = main.ObservedSnapshot!.Players.Single().Clone();
        var geometry = CathedralGeometry(main);
        var wall = CardinalOffsets.First(offset => geometry.BlockedCells.Contains(
            (player.PositionY + offset.Y) * geometry.Width + player.PositionX + offset.X));
        var key = wall switch { (0, 1) => Key.Down, (0, -1) => Key.Up, (1, 0) => Key.Right, _ => Key.Left };
        var sequence = main.ObservedCommandResult!.ClientSequence;
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        var inputSequence = main.ObservedLastQueuedSequence;
        Require(inputSequence > sequence, "The wall key did not queue a fresh ordinary movement command.");
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == inputSequence
            && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main), "The wall-collision input did not settle.", 10);
        var receipt = main.ObservedCommandResult!;
        var after = main.ObservedSnapshot!.Players.Single();
        WriteEvidence(directory, "cathedral-collision.json", new { Passed = receipt.Status == CommandStatus.Rejected
            && receipt.RejectReason == CommandRejectReason.InvalidTarget && after.LevelId == player.LevelId
            && after.PositionX == player.PositionX && after.PositionY == player.PositionY,
            X = player.PositionX, Y = player.PositionY, WallX = player.PositionX + wall.X, WallY = player.PositionY + wall.Y,
            AfterX = after.PositionX, AfterY = after.PositionY, InputSequence = inputSequence,
            Status = receipt.Status.ToString(), Rejection = receipt.RejectReason.ToString(), receipt.ClientSequence });
        Require(receipt.Status == CommandStatus.Rejected && receipt.RejectReason == CommandRejectReason.InvalidTarget,
            $"Native wall collision requires a gameplay InvalidTarget rejection, not {receipt.Status}/{receipt.RejectReason}.");
        Require(after.LevelId == player.LevelId && after.PositionX == player.PositionX && after.PositionY == player.PositionY,
            "Rejected native wall movement changed the authoritative position.");
    }

    private static LevelGeometrySnapshot CathedralGeometry(Main main)
        => main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == 1);

    private async Task<int> RestoreCathedralWallBoundary(Main main, CathedralWallCheckpoint boundary)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.LevelId == 1, "Manual Cathedral wall setup started outside level 1.");
        var target = (X: boundary.PlayerX, Y: boundary.PlayerY);
        var geometry = CathedralGeometry(main);
        Require(target.X >= 0 && target.Y >= 0 && target.X < geometry.Width && target.Y < geometry.Height
            && !geometry.BlockedCells.Contains(target.Y * geometry.Width + target.X)
            && !geometry.WarpTriggers.Any(warp => warp.TriggerX == target.X && warp.TriggerY == target.Y),
            "The captured Cathedral wall checkpoint is no longer a valid level-1 walk target.");
        var previousAppliedTick = main.ObservedCommandResult?.AppliedTick ?? 0;
        var previousPosition = (X: player.PositionX, Y: player.PositionY);
        var steps = 0;
        while (previousPosition != target) {
            Require(steps < 8, "The captured Cathedral wall checkpoint is beyond the bounded ordinary setup path.");
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 1, "Manual Cathedral wall setup left level 1.");
            var queuedBefore = main.ObservedLastQueuedSequence;
            var previousDistance = Math.Abs(previousPosition.X - target.X) + Math.Abs(previousPosition.Y - target.Y);
            await Click(main.ObservedTileScreenPosition(target.X, target.Y), walk: true);
            var inputSequence = main.ObservedLastQueuedSequence;
            Require(inputSequence > queuedBefore, "Ordinary Ctrl-click did not queue a fresh Cathedral wall-setup step.");
            await Until(() => {
                var result = main.ObservedCommandResult;
                var current = main.ObservedSnapshot!.Players.Single();
                if (result is null || result.ClientSequence < inputSequence || current.LevelId != 1)
                    return false;
                Require(result.ClientSequence == inputSequence,
                    "A Cathedral wall-setup step was overtaken by another command receipt.");
                Require(IsAcceptedOrSuccessfulDuplicate(result),
                    $"Cathedral wall setup was rejected: {result.Status}/{result.RejectReason}.");
                var distance = Math.Abs(current.PositionX - target.X) + Math.Abs(current.PositionY - target.Y);
                return main.ObservedLastQueuedSequence == inputSequence
                    && main.ObservedSnapshot.Tick >= result.AppliedTick && Settled(main)
                    && distance < previousDistance;
            }, "An ordinary Cathedral wall-setup step did not settle with authoritative progress.", 10);
            var settled = main.ObservedSnapshot!.Players.Single();
            Require((settled.PositionX, settled.PositionY) != previousPosition,
                "A Cathedral wall-setup step settled without authoritative movement.");
            Require(main.ObservedCommandResult!.ClientSequence == inputSequence
                && main.ObservedCommandResult.AppliedTick >= previousAppliedTick,
                "Cathedral wall setup lost the exact fresh receipt or applied tick.");
            steps++;
            previousAppliedTick = main.ObservedCommandResult.AppliedTick;
            previousPosition = (settled.PositionX, settled.PositionY);
        }
        return steps;
    }

    private async Task MoveCathedralNeighbor(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single().Clone();
        var offset = SelectCathedralLootStep(main, new WorldItemSnapshot { PositionX = player.PositionX, PositionY = player.PositionY }, []);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await Click(main.ObservedTileScreenPosition(player.PositionX + offset.X, player.PositionY + offset.Y), walk: true);
        await AcceptedAndSettled(main, sequence, "Move after rejected wall input");
        Require(main.ObservedSnapshot!.Players.Single().PositionX == player.PositionX + offset.X
            && main.ObservedSnapshot.Players.Single().PositionY == player.PositionY + offset.Y,
            "Accepted movement after wall rejection did not reach the walkable cell.");
    }

    private static string CathedralGeometryHash(Main main)
        => Convert.ToHexString(SHA256.HashData(CathedralGeometry(main).ToByteArray()));

    private static readonly (int X, int Y)[] CardinalOffsets = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    private sealed record CathedralWallCheckpoint(int PlayerX, int PlayerY, int WallX, int WallY);

    private static CathedralWallCheckpoint? FindAdjacentCathedralWall(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var geometry = CathedralGeometry(main);
        foreach (var offset in CardinalOffsets) {
            var wallX = player.PositionX + offset.X;
            var wallY = player.PositionY + offset.Y;
            if (wallX >= 0 && wallY >= 0 && wallX < geometry.Width && wallY < geometry.Height
                && geometry.BlockedCells.Contains(wallY * geometry.Width + wallX))
                return new CathedralWallCheckpoint(player.PositionX, player.PositionY, wallX, wallY);
        }
        return null;
    }

    private static (int X, int Y) SelectCathedralLootStep(Main main, WorldItemSnapshot drop, HashSet<(int X, int Y)> visited,
        bool keepLootVisible = false)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = CathedralGeometry(main);
        var choices = CardinalOffsets.Where(offset => {
            var x = player.PositionX + offset.X;
            var y = player.PositionY + offset.Y;
            return x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height
                && !visited.Contains((x, y)) && !geometry.BlockedCells.Contains(y * geometry.Width + x)
                && (!keepLootVisible || Devilution.Client.Protocol.AuthoritativeWorldItemPresentationVisibility.IsVisible(
                    drop, player.LevelId, geometry, x, y, player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10))
                && !geometry.WarpTriggers.Any(warp => warp.TriggerX == x && warp.TriggerY == y)
                && !snapshot.Objects.Any(item => item.LevelId == player.LevelId && !item.Destroyed && item.PositionX == x && item.PositionY == y)
                && !snapshot.Monsters.Any(item => item.Alive && item.LevelId == player.LevelId && item.PositionX == x && item.PositionY == y);
        }).OrderByDescending(offset => Math.Max(Math.Abs(player.PositionX + offset.X - drop.PositionX),
            Math.Abs(player.PositionY + offset.Y - drop.PositionY)))
            .ThenByDescending(offset => Math.Abs(player.PositionX + offset.X - drop.PositionX)
            + Math.Abs(player.PositionY + offset.Y - drop.PositionY)).ToArray();
        Require(choices.Length > 0, "No unvisited ordinary adjacent walkable cell exists for loot/restore movement.");
        return choices[0];
    }
}
