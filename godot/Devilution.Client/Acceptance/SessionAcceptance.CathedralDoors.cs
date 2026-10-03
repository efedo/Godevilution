using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;

namespace Devilution.Client.Acceptance;

/** Ordinary input verifies mutable doors; no checkpoint edits or local simulation. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralDoorsAsync(Main main, string directory, string phase, bool visible)
    {
        var path = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_DOOR_PROFILE");
        Require(!string.IsNullOrWhiteSpace(path), "Door acceptance requires its isolated compiled profile.");
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(path!));
        Require(profile.RootElement.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-doors.v1",
            "The supplied profile does not select the explicit door adapter.");
        var snapshot = main.ObservedSnapshot!;
        Require(snapshot.LevelGeometry.Count(level => level.LevelId is >= 1 and <= 4) == 4
            && snapshot.LevelGeometry.Where(level => level.LevelId is >= 1 and <= 4).All(level =>
                level.Width == 112 && level.Height == 112 && level.NativePieceLayer.Count == 112 * 112
                && level.NativeSpecialLayer.Count == 112 * 112), "Complete native door geometry did not arrive.");

        uint doorId;
        (int X, int Y) approach;
        uint closedFrame;
        if (phase == "CathedralDoorJourney") {
            Require(snapshot.Players.Single().LevelId == 0, "The door journey must start in Town.");
            await TravelCathedralRoute(main, directory, 1001, 1, stopEarly: false);
            var selected = SelectReachableCathedralDoor(main.ObservedSnapshot!);
            doorId = selected.Door.EntityId;
            closedFrame = selected.Door.AnimationFrame;
            foreach (var point in selected.Path)
                await MoveDoorStep(main, point.X, point.Y);
            var player = main.ObservedSnapshot!.Players.Single();
            approach = (player.PositionX, player.PositionY);
            await ObserveDoorFrame(main, directory, doorId, 0, visible, "closed");
            await OperateDoor(main, doorId, 1, closedFrame + 2);
            await ObserveDoorFrame(main, directory, doorId, 1, visible, "open");
            await VerifyOccupiedDoor(main, directory, doorId, approach, closedFrame, visible, "create");
            await OperateDoor(main, doorId, 0, closedFrame);
            await ObserveDoorFrame(main, directory, doorId, 0, visible, "reclosed");
            await OperateDoor(main, doorId, 1, closedFrame + 2);
            await ObserveDoorFrame(main, directory, doorId, 1, visible, "saved-open");
            WriteEvidence(directory, "cathedral-door-state.json", new {
                Passed = true, WorldHash = DoorMutableWorldHash(main.ObservedSnapshot!), DoorEntityId = doorId,
                ClosedFrame = closedFrame, ApproachX = approach.X, ApproachY = approach.Y,
                TraversedDepths = new[] { 1 }, NativePopulationClaim = false,
            });
        } else {
            Require(snapshot.Players.Single().LevelId == 1, "Door cold restore did not return to Cathedral one.");
            using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedral-door-state.json")));
            var expected = saved.RootElement;
            Require(expected.GetProperty("Passed").GetBoolean()
                && expected.GetProperty("WorldHash").GetString() == DoorMutableWorldHash(snapshot),
                "Cold restore changed saved geometry, special bytes, blockers or exact typed door state.");
            doorId = expected.GetProperty("DoorEntityId").GetUInt32();
            closedFrame = expected.GetProperty("ClosedFrame").GetUInt32();
            approach = (expected.GetProperty("ApproachX").GetInt32(), expected.GetProperty("ApproachY").GetInt32());
            await ObserveDoorFrame(main, directory, doorId, 1, visible, "restored-open");
            var oldSequence = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedraldoorjourney.json")))!.Sequence;
            await VerifyOccupiedDoor(main, directory, doorId, approach, closedFrame, visible, "resume", oldSequence);
            await OperateDoor(main, doorId, 0, closedFrame);
            Require(main.ObservedCommandResult!.ClientSequence > oldSequence,
                "The fresh door client reused its persisted command sequence.");
            await ObserveDoorFrame(main, directory, doorId, 0, visible, "restored-reclosed");
        }
        // Fresh-process restore, not this delay, proves the checkpoint publication.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var final = main.ObservedSnapshot!.Players.Single();
        var identity = main.ObservedIdentity!;
        WriteEvidence(directory, phase.ToLowerInvariant() + ".json", new Evidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            final.EntityId, final.CharacterClassId, final.LevelId, final.PositionX, final.PositionY,
            main.ObservedCommandResult!.ClientSequence, visible));
        GD.Print($"Godot Cathedral door acceptance passed: {phase}; visible={visible}");
    }

    private async Task VerifyOccupiedDoor(Main main, string directory, uint doorId,
        (int X, int Y) approach, uint closedFrame, bool visible, string phaseLabel, ulong savedSequenceFloor = 0)
    {
        var door = ObserveDoor(main, doorId);
        await MoveDoorStep(main, door.PositionX, door.PositionY, savedSequenceFloor);
        await Until(() => ObserveDoor(main, doorId).CathedralDoor.DoorState == 2,
            "The native occupancy update did not mark the door BLOCKED.", 5);
        await ObserveDoorFrame(main, directory, doorId, 2, visible, phaseLabel + "-occupied");
        var hash = DoorMutableWorldHash(main.ObservedSnapshot!);
        var beforeSequence = main.ObservedCommandResult!.ClientSequence;
        await OperateDoor(main, doorId, 2, closedFrame + 2);
        Require(hash == DoorMutableWorldHash(main.ObservedSnapshot!),
            "Closing an occupied door changed native geometry, frame or door state.");
        WriteEvidence(directory, "cathedral-door-occupied-" + beforeSequence + ".json", new {
            Passed = true, DoorEntityId = doorId, DoorState = 2,
            Sequence = main.ObservedCommandResult!.ClientSequence, UnchangedMutableWorldHash = hash,
        });
        await MoveDoorStep(main, approach.X, approach.Y);
        await Until(() => ObserveDoor(main, doorId).CathedralDoor.DoorState == 1,
            "Leaving the door did not clear BLOCKED back to OPEN.", 5);
    }

    private async Task OperateDoor(Main main, uint entityId, int expectedState, uint expectedFrame)
    {
        var door = ObserveDoor(main, entityId);
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        GetViewport().GuiReleaseFocus();
        await ClickWorld(main, door.PositionX, door.PositionY);
        await AcceptedAndSettled(main, sequence, "Operate native Cathedral door");
        await Until(() => ObserveDoor(main, entityId) is { } observed
            && observed.CathedralDoor.DoorState == expectedState && observed.AnimationFrame == expectedFrame,
            "The accepted door operation did not publish its exact native status/frame.", 5);
        var after = ObserveDoor(main, entityId);
        Require(after.CathedralDoor.AnimationFrame == expectedFrame
            && after.CathedralDoor.SelectionRegion == (expectedState == 0 ? 3 : 2)
            && after.CathedralDoor.PreDraw == (expectedState != 0)
            && after.CathedralDoor.MissilePassable == (expectedState != 0)
            && after.MissilePassable == (expectedState != 0), "Door metadata copies disagree after operation.");
    }

    private async Task MoveDoorStep(Main main, int x, int y, ulong savedSequenceFloor = 0)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(Math.Abs(player.PositionX - x) + Math.Abs(player.PositionY - y) == 1,
            "Door acceptance must request one ordinary adjacent step, not teleportation.");
        GetViewport().GuiReleaseFocus();
        var sequence = Math.Max(main.ObservedCommandResult?.ClientSequence ?? 0, savedSequenceFloor);
        await Click(main.ObservedTileScreenPosition(x, y), walk: true);
        Require(main.ObservedLastQueuedSequence > sequence,
            "The first ordinary door movement did not advance the acknowledged/saved sequence floor.");
        Require(savedSequenceFloor == 0 || main.ObservedLastQueuedSequence == checked(savedSequenceFloor + 1),
            "The first resumed door command did not continue at exactly saved sequence plus one.");
        await AcceptedAndSettled(main, sequence, "Ordinary Cathedral door movement");
        var moved = main.ObservedSnapshot!.Players.Single();
        Require((moved.LevelId, moved.PositionX, moved.PositionY) == (player.LevelId, x, y),
            "The server-owned door approach did not reach the requested adjacent cell.");
    }

    private async Task ObserveDoorFrame(Main main, string directory, uint entityId, int state, bool visible, string label)
    {
        var door = ObserveDoor(main, entityId);
        Require(door.CathedralDoor.DoorState == state, "The observed door has the wrong server state.");
        if (!visible)
            return;
        await Until(() => main.ObservedCathedralDoorDrawing(entityId) is { } drawn
            && drawn.DoorState == state && drawn.AnimationFrame == door.AnimationFrame
            && drawn.NativeObjectId == door.NativeObjectId && drawn.Frame == ObjectPresentationStyle.ResolveFrame(door),
            "The actual world draw did not use the authoritative door state/frame.", 5);
        await Capture(System.IO.Path.Combine(directory, "cathedral-door-" + label + ".png"));
    }

    private static ObjectSnapshot ObserveDoor(Main main, uint entityId)
    {
        var door = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == entityId);
        Require(door.CathedralDoor is not null && AuthoritativeObjectSelector.HasAuthoritativeInteraction(door),
            "The selected door lost its validated repeatable interaction capability.");
        return door;
    }

    private static (ObjectSnapshot Door, List<(int X, int Y)> Path) SelectReachableCathedralDoor(Snapshot snapshot)
    {
        var player = snapshot.Players.Single();
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId);
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        foreach (var route in geometry.WarpTriggers)
            blocked.Add(route.TriggerY * geometry.Width + route.TriggerX);
        foreach (var monster in snapshot.Monsters.Where(item => item.LevelId == player.LevelId && item.Alive))
            blocked.Add(monster.PositionY * geometry.Width + monster.PositionX);
        foreach (var item in snapshot.Objects.Where(item => item.LevelId == player.LevelId && !item.Destroyed
            && (item.CathedralDoor is null || item.CathedralDoor.DoorState == 0)))
            blocked.Add(item.PositionY * geometry.Width + item.PositionX);
        var doors = snapshot.Objects.Where(item => item.LevelId == player.LevelId
            && item.CathedralDoor is { DoorState: 0 } && AuthoritativeObjectSelector.HasAuthoritativeInteraction(item))
            .OrderBy(item => item.EntityId).ToArray();
        var origin = player.PositionY * geometry.Width + player.PositionX;
        var parents = new Dictionary<int, int> { [origin] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var cell)) {
            var x = cell % geometry.Width;
            var y = cell / geometry.Width;
            var door = doors.FirstOrDefault(item => Math.Abs(item.PositionX - x) + Math.Abs(item.PositionY - y) == 1);
            if (door is not null) {
                var path = new List<(int X, int Y)>();
                for (var cursor = cell; cursor != origin; cursor = parents[cursor])
                    path.Add((cursor % geometry.Width, cursor / geometry.Width));
                path.Reverse();
                return (door, path);
            }
            foreach (var offset in CardinalOffsets) {
                var nx = x + offset.X;
                var ny = y + offset.Y;
                var next = ny * geometry.Width + nx;
                if (nx < 0 || ny < 0 || nx >= geometry.Width || ny >= geometry.Height
                    || blocked.Contains(next) || parents.ContainsKey(next))
                    continue;
                parents[next] = cell;
                queue.Enqueue(next);
            }
        }
        throw new InvalidOperationException("No CLOSED Cathedral door has an ordinary reachable adjacent approach.");
    }

    private static string DoorMutableWorldHash(Snapshot snapshot)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var value in snapshot.LevelGeometry.Where(level => level.LevelId is >= 1 and <= 4)
            .OrderBy(level => level.LevelId).Select(level => level.ToByteArray())
            .Concat(snapshot.Objects.Where(item => item.CathedralDoor is not null)
                .OrderBy(item => item.EntityId).Select(item => item.ToByteArray()))) {
            BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
            hash.AppendData(length);
            hash.AppendData(value);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
