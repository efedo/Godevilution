using Devilution.Data;
using Devilution.Diablo;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Local pre-2.5a saves remain private; the archive-free logic comparisons run in the parity script. */
public sealed class NativeObjectLegacyCohortTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreBatchCohortRestoresAndItsTrapTriggerAndChestRemainOperable(bool catacombs)
    {
        var profileRoot = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_LEGACY_PROFILE_ROOT");
        var sessionRoot = Environment.GetEnvironmentVariable(catacombs
            ? "GODEVILUTION_OBJECT_LEGACY_C2_SESSION" : "GODEVILUTION_OBJECT_LEGACY_C1_SESSION");
        if (string.IsNullOrWhiteSpace(profileRoot) || string.IsNullOrWhiteSpace(sessionRoot))
            Assert.Skip("Select private pre-2.5a profile and C1/C2 save directories for the cold-load regression.");
        var path = Path.Combine(sessionRoot!, "save", AuthoritativeCohortStore.CheckpointFileName);
        var originalBytes = File.ReadAllBytes(path);
        var document = AuthoritativeCohortStore.DeserializeDocument(originalBytes);
        var codec = new DiabloCohortCodec();
        var saved = codec.Deserialize(document.SerializedModuleState);
        Assert.True(catacombs ? saved.Objects.Any(value => value.LevelId is >= 5 and <= 8)
            : saved.Objects.All(value => value.LevelId is >= 1 and <= 4));
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(directory => ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot!, directory))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, saved.Participants[0].Player.CharacterClassId,
            saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var candidate = activation.CohortCandidateFactory!(saved);
        var simulation = candidate.Simulation;
        var runtime = candidate.CreateRuntime();
        var player = runtime.Read(saved.Participants[0].SessionId, value => value);
        var world = simulation.WorldState;
        Assert.Equal(saved.CathedralItems!.GenerationFingerprint, world.CathedralItemGenerationFingerprint);
        if (catacombs)
            Assert.True(world.CaptureNativeTraps()!.Count > saved.NativeTraps!.Count,
                "The pre-batch save must restore newly supported links absent from its old trap array.");
        var armed = world.CaptureNativeTraps()!.Where(value => value.Disposition == DiabloNativeTrapDisposition.Armed
            && (catacombs ? value.Facts.LevelId is >= 5 and <= 8 : value.Facts.LevelId is >= 1 and <= 4)).ToArray();
        // A seed need not generate a trapped door. Its retained ordinary linked chest
        // is the same previously refused/unfired UpdateTrapState path on that floor.
        var trap = armed.FirstOrDefault(value => value.Facts.TriggerNativeTypeId is 1 or 2 or 42 or 43)
            ?? armed.First(value => value.Facts.TriggerNativeTypeId is 5 or 6 or 7);
        var door = world.Objects[trap.Facts.TriggerEntityId];
        Relocate(world, player, door);
        Assert.True(world.CanOperateNativeObject(door));
        var operated = simulation.Execute(player, new DiabloOperateObjectIntent(door.EntityId), saved.Tick + 1, [player]);
        Assert.True(operated.Succeeded, operated.Failure?.ToString());
        var fired = world.ConsumeReadyNativeTraps([player]);
        Assert.Contains(fired, value => value.TrapEntityId == trap.Facts.TrapEntityId);
        Assert.False(door.TrapArmed);
        Assert.Empty(world.ConsumeReadyNativeTraps([player]));

        var chest = world.Objects.Values.First(value => (catacombs ? value.LevelId is >= 5 and <= 8 : value.LevelId is >= 1 and <= 4)
            && world.CanOperateNativeChest(value)
            && (!catacombs || value.NativeObjectId is >= 68 and <= 70 && value.TrapArmed));
        Relocate(world, player, chest);
        var opened = simulation.Execute(player, new DiabloOperateObjectIntent(chest.EntityId), saved.Tick + 1, [player]);
        Assert.True(opened.Succeeded, opened.Failure?.ToString());
        Assert.True(chest.Activated);
        if (catacombs) {
            Assert.False(chest.TrapArmed);
            Assert.Contains(world.Projectiles.Values, value => value.SourceEntityId == chest.EntityId
                && value.NativeTrapArrow is { IntrinsicChest: true });
        }
        // Retain a real pending wall missile too. Its ordinary Arrow extension is
        // exactly the three-field payload written by ab5e38d1f.
        var projectileId = 0xEF000001U;
        foreach (var shot in fired) {
            while (world.Projectiles.ContainsKey(projectileId)) projectileId++;
            world.Projectiles.Add(projectileId, DiabloCombatCommandProcessor.AddObjectTrapMissile(
                projectileId, shot.TrapEntityId, world.CaptureNativeTraps()!.Single(
                    value => value.Facts.TrapEntityId == shot.TrapEntityId).Facts.TriggerEntityId, shot.LevelId, shot.TrapObjectSlot,
                shot.SourceX, shot.SourceY, shot.TargetX, shot.TargetY, shot.Direction,
                shot.MissileTypeId, false, new Devilution.Engine.DeterministicLegacyRandom(2588),
                (int)shot.LevelId));
            projectileId++;
        }
        var current = simulation.CaptureCohortState(runtime);
        var bytes = codec.Serialize(current);
        if (!catacombs) {
            using var json = System.Text.Json.JsonDocument.Parse(bytes);
            var oldArrow = json.RootElement.GetProperty("Projectiles").EnumerateArray()
                .First(value => value.GetProperty("NativeTrapArrow").ValueKind == System.Text.Json.JsonValueKind.Object)
                .GetProperty("NativeTrapArrow");
            Assert.Equal(new[] { "NativeObjectSlot", "MissileDistance", "LastCollisionTargetHash" },
                oldArrow.EnumerateObject().Select(value => value.Name));
        }
        var decoded = codec.Deserialize(bytes);
        var cold = activation.CohortCandidateFactory(decoded);
        Assert.Throws<InvalidDataException>(() => activation.CohortCandidateFactory(decoded with {
            Projectiles = decoded.Projectiles.Select((value, index) => index == 0
                ? value with { WallLeftX = 1 } : value).ToArray(),
        }));
        Assert.Equal(bytes, codec.Serialize(cold.Simulation.CaptureCohortState(cold.CreateRuntime())));
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
    }

    private static void Relocate(DiabloWorldState world, AuthoritativePlayerState player, AuthoritativeWorldObject value)
    {
        var adjacent = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }
            .Select(offset => (X: value.PositionX + offset.Item1, Y: value.PositionY + offset.Item2))
            .First(cell => world.IsWalkable(value.LevelId, cell.X, cell.Y)
                && !world.IsOccupied(value.LevelId, cell.X, cell.Y, [], player.EntityId)
                && !world.WorldItems.Values.Any(item => item.LevelId == value.LevelId
                    && item.PositionX == cell.X && item.PositionY == cell.Y));
        player.CurrentAction = null;
        player.NativeWalkPath = null;
        player.NativeQueuedRangedAttack = null;
        player.CombatMode = DiabloPlayerCombatMode.Stand;
        player.LevelId = value.LevelId;
        player.PositionX = adjacent.X;
        player.PositionY = adjacent.Y;
    }
}
