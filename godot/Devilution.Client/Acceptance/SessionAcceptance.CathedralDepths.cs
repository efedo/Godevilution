using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** Ordinary-control depth traversal and cold restore; explicit seeds and authored population only. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralDepthsAsync(Main main, string directory, string phase, bool visible)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_DEPTH_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath), "Depth acceptance requires its separate compiled profile.");
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        Require(profile.RootElement.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-depths.v1",
            "The supplied profile is not the separate depth adapter.");
        VerifyDepthGeometry(main, profile.RootElement);
        if (phase == "CathedralDepthJourney") {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 0, "Depth journey must begin in Town through the new-character form.");
            for (uint destination = 1; destination <= 4; destination++) {
                await TravelDepthRoute(main, directory, destination, profile.RootElement);
                if (visible)
                    await Capture(System.IO.Path.Combine(directory, $"cathedral-depth-{destination}.png"));
            }
            var fourth = DepthGeometry(main, 4);
            var nativeDown = profile.RootElement.GetProperty("levels")[3].GetProperty("downTrigger");
            Require(!fourth.WarpTriggers.Any(route => route.TargetLevelId == 5
                || (route.TriggerX == nativeDown.GetProperty("x").GetInt32() && route.TriggerY == nativeDown.GetProperty("y").GetInt32())),
                "The unimplemented native L4-to-Catacombs-5 boundary was replaced with a fabricated exit.");
            await TravelDepthRoute(main, directory, 3, profile.RootElement);
            await TravelDepthRoute(main, directory, 2, profile.RootElement);
            await EstablishDepthWallCheckpoint(main);
            await VerifyDepthWallCollision(main, directory);
            await MoveDepthNeighbor(main);
            WriteEvidence(directory, "cathedral-depth-state.json", new {
                Passed = true, GeometryHash = DepthGeometryHash(main),
                BoundaryDepth = 4, UnsupportedDestinationDepth = 5,
            });
        } else {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 2, "Fresh-host restore did not return to the middle Cathedral floor.");
            using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedral-depth-state.json")));
            Require(saved.RootElement.GetProperty("Passed").GetBoolean()
                && saved.RootElement.GetProperty("GeometryHash").GetString() == DepthGeometryHash(main),
                "Fresh host changed the native depth geometry, transparency, blockers or resolved routes.");
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-depth-middle-restored.png"));
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await MoveDepthNeighbor(main);
            var previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedraldepthjourney.json")))!;
            Require(main.ObservedCommandResult!.ClientSequence > previous.Sequence
                && main.ObservedCommandResult.ClientSequence > sequence,
                "Fresh depth client did not advance the saved command sequence.");
            await TravelDepthRoute(main, directory, 1, profile.RootElement);
            await TravelDepthRoute(main, directory, 0, profile.RootElement);
        }
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var final = main.ObservedSnapshot!.Players.Single();
        var identity = main.ObservedIdentity!;
        WriteEvidence(directory, phase.ToLowerInvariant() + ".json", new Evidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            final.EntityId, final.CharacterClassId, final.LevelId, final.PositionX, final.PositionY,
            main.ObservedCommandResult!.ClientSequence, visible));
        GD.Print($"Godot Cathedral depths acceptance passed: {phase}; visible={visible}");
    }

    private async Task TravelDepthRoute(Main main, string directory, uint destination, JsonElement profile)
    {
        var source = main.ObservedSnapshot!.Players.Single().LevelId;
        var route = DepthGeometry(main, source).WarpTriggers.Single(candidate => candidate.TargetLevelId == destination);
        var origin = main.ObservedSnapshot.Players.Single();
        // Native stairs can be only one or two steps from MAIN. Retain the old
        // eight-position proof for longer routes without inventing a detour.
        var minimumPositions = Math.Clamp(Math.Max(Math.Abs(origin.PositionX - route.TriggerX),
            Math.Abs(origin.PositionY - route.TriggerY)), 1, 8);
        await TravelCathedralRoute(main, directory, route.WarpId, destination, stopEarly: false,
            minimumDistinctPositions: minimumPositions);
        var player = main.ObservedSnapshot!.Players.Single();
        var expected = destination == 0 ? (25, 31) : ProfileDepthPoint(profile, destination,
            destination > source ? "mainEntry" : "previousEntry");
        Require((player.LevelId, player.PositionX, player.PositionY) == (destination, expected.Item1, expected.Item2),
            "Native depth travel reached the wrong entry anchor.");
    }

    private static void VerifyDepthGeometry(Main main, JsonElement profile)
    {
        var levels = profile.GetProperty("levels");
        Require(levels.GetArrayLength() == 4, "The depth profile must describe four native floors.");
        for (uint depth = 1; depth <= 4; depth++) {
            var geometry = DepthGeometry(main, depth);
            Require(geometry.Width == 112 && geometry.Height == 112
                && geometry.NativePieceLayer.Count == 112 * 112 && geometry.NativeTransparency.Count == 112 * 112
                && geometry.NativeMegaTileLayer.Count == 0 && geometry.BlockedCells.Count > 0
                && geometry.NativePieceLayer.Count(piece => piece == 128) == 1
                && geometry.NativePieceLayer.Count(piece => piece == 114) == 1,
                $"Depth {depth} lacks complete native fine geometry and unique stairs.");
            var up = ProfileDepthPoint(profile, depth, "upTrigger");
            var down = ProfileDepthPoint(profile, depth, "downTrigger");
            Require(geometry.NativePieceLayer[up.Item2 * 112 + up.Item1] == 128
                && geometry.NativePieceLayer[down.Item2 * 112 + down.Item1] == 114,
                $"Depth {depth} stair anchors differ from its published profile.");
            Require(geometry.WarpTriggers.Count == (depth == 4 ? 1 : 2), "The depth chain contains extra or missing routes.");
            var backward = geometry.WarpTriggers.Single(route => route.TargetLevelId == depth - 1);
            Require((backward.TriggerX, backward.TriggerY) == up && backward.Available,
                "Up stairs do not bind to the preceding native floor.");
            if (depth < 4) {
                var forward = geometry.WarpTriggers.Single(route => route.TargetLevelId == depth + 1);
                Require((forward.TriggerX, forward.TriggerY) == down && forward.Available,
                    "Down stairs do not bind to the next native floor.");
            }
        }
    }

    private async Task EstablishDepthWallCheckpoint(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.LevelId == 2, "The collision checkpoint must be on the middle native floor.");
        var geometry = DepthGeometry(main, 2);
        var targets = Enumerable.Range(0, geometry.Width * geometry.Height)
            .Select(index => (X: index % geometry.Width, Y: index / geometry.Width))
            .Where(point => Math.Abs(point.X - player.PositionX) + Math.Abs(point.Y - player.PositionY) <= 8
                && IsDepthWalkTarget(main, point.X, point.Y)
                && CardinalOffsets.Any(offset => IsDepthWall(geometry, point.X + offset.X, point.Y + offset.Y)))
            .OrderBy(point => Math.Abs(point.X - player.PositionX) + Math.Abs(point.Y - player.PositionY)).ToArray();
        Require(targets.Length > 0, "No nearby non-trigger native wall checkpoint exists.");
        var target = targets[0];
        for (var step = 0; step < 32; step++) {
            var before = main.ObservedSnapshot!.Players.Single().Clone();
            if (before.PositionX == target.X && before.PositionY == target.Y)
                return;
            var sequence = main.ObservedCommandResult!.ClientSequence;
            await Click(main.ObservedTileScreenPosition(target.X, target.Y), walk: true);
            await AcceptedAndSettled(main, sequence, "Ordinary depth collision-checkpoint movement");
            var after = main.ObservedSnapshot!.Players.Single();
            Require(after.LevelId == 2 && (after.PositionX, after.PositionY) != (before.PositionX, before.PositionY),
                "Server-owned approach did not progress toward the middle-floor wall checkpoint.");
        }
        throw new InvalidOperationException("The bounded ordinary-input wall approach did not reach its checkpoint.");
    }

    private async Task VerifyDepthWallCollision(Main main, string directory)
    {
        var before = main.ObservedSnapshot!.Players.Single().Clone();
        var geometry = DepthGeometry(main, before.LevelId);
        var wall = CardinalOffsets.First(offset => IsDepthWall(geometry, before.PositionX + offset.X, before.PositionY + offset.Y));
        var key = wall switch { (0, 1) => Key.Down, (0, -1) => Key.Up, (1, 0) => Key.Right, _ => Key.Left };
        GetViewport().GuiReleaseFocus();
        var oldSequence = main.ObservedLastQueuedSequence;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        var sequence = main.ObservedLastQueuedSequence;
        Require(sequence > oldSequence, "The native depth wall key did not queue fresh ordinary input.");
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == sequence
            && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main), "Native depth wall input did not settle.", 10);
        var receipt = main.ObservedCommandResult!;
        var after = main.ObservedSnapshot!.Players.Single();
        var passed = receipt.Status == CommandStatus.Rejected && receipt.RejectReason == CommandRejectReason.InvalidTarget
            && (after.LevelId, after.PositionX, after.PositionY) == (before.LevelId, before.PositionX, before.PositionY);
        WriteEvidence(directory, "cathedral-depth-collision.json", new {
            Passed = passed, before.LevelId, before.PositionX, before.PositionY,
            WallX = before.PositionX + wall.X, WallY = before.PositionY + wall.Y,
            receipt.ClientSequence, Status = receipt.Status.ToString(), Rejection = receipt.RejectReason.ToString(),
        });
        Require(passed, "The exact native wall input was not rejected without moving the middle-floor character.");
    }

    private async Task MoveDepthNeighbor(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single().Clone();
        var offset = CardinalOffsets.FirstOrDefault(offset => IsDepthWalkTarget(main, player.PositionX + offset.X, player.PositionY + offset.Y));
        Require(offset != (0, 0), "No ordinary non-trigger adjacent native depth cell is free.");
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await Click(main.ObservedTileScreenPosition(player.PositionX + offset.X, player.PositionY + offset.Y), walk: true);
        await AcceptedAndSettled(main, sequence, "Native depth movement after collision or restore");
        var after = main.ObservedSnapshot!.Players.Single();
        Require((after.LevelId, after.PositionX, after.PositionY) == (player.LevelId, player.PositionX + offset.X, player.PositionY + offset.Y),
            "Ordinary native depth movement did not reach its selected adjacent cell.");
    }

    private static bool IsDepthWalkTarget(Main main, int x, int y)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = DepthGeometry(main, player.LevelId);
        return x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height
            && !geometry.BlockedCells.Contains(y * geometry.Width + x)
            && !geometry.WarpTriggers.Any(route => route.TriggerX == x && route.TriggerY == y)
            && !snapshot.Objects.Any(item => item.LevelId == player.LevelId && !item.Destroyed && item.PositionX == x && item.PositionY == y)
            && !snapshot.Monsters.Any(item => item.LevelId == player.LevelId && item.Alive && item.PositionX == x && item.PositionY == y);
    }

    private static bool IsDepthWall(LevelGeometrySnapshot geometry, int x, int y)
        => x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height && geometry.BlockedCells.Contains(y * geometry.Width + x);

    private static LevelGeometrySnapshot DepthGeometry(Main main, uint levelId)
        => main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == levelId);

    private static (int, int) ProfileDepthPoint(JsonElement profile, uint depth, string name)
    {
        var point = profile.GetProperty("levels")[(int)depth - 1].GetProperty(name);
        return (point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32());
    }

    private static string DepthGeometryHash(Main main)
        => Convert.ToHexString(SHA256.HashData(main.ObservedSnapshot!.LevelGeometry
            .Where(level => level.LevelId is >= 1 and <= 4).OrderBy(level => level.LevelId)
            .SelectMany(level => level.ToByteArray()).ToArray()));
}
