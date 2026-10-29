using System.Text.Json;
using Devilution.Data;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

/** The compiled all-class profile descends L1-L8 by stairs, restores cold, and climbs back. */
public sealed class AuthoritativeCatacombsNativeProgressionTests
{
    private const string ProfileVariable = "GODEVILUTION_NATIVE_ALL_CLASS_PROFILE";

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(2U)]
    public void AllClassProfileDescendsToL8AndRestoresCold(uint classId)
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, classId, seed);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.NotNull(activation.NativeCatacombsMetadata);
        // Single-player Town warps stay closed until their unlock is modelled.
        Assert.DoesNotContain(world.World!.WarpTargets, row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        var start = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-catacombs-descent", classId,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-catacombs-descent", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-catacombs-descent", value => value);
        var commands = WorldCommands(simulation);
        var codec = new DiabloCohortCodec();

        for (uint level = 1; level < 8; level++) {
            UseStair(world, commands, player, level, level + 1);
            Assert.True(world.OwnsNativePopulationLevel(level + 1));
        }
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeCatacombsProgressionFormatVersion, captured.FormatVersion);
        Assert.Equal(Enumerable.Range(2, 7).Select(value => (uint)value),
            captured.NativeProgression!.Entries.Select(entry => entry.LevelId));
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (uint)value),
            captured.CathedralItems!.Levels.Select(level => level.LevelId));
        var bytes = codec.Serialize(captured);
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));

        var restoredWorld = candidate.Simulation.WorldState;
        var restoredPlayer = restoredRuntime.Read("native-catacombs-descent", value => value);
        var restoredCommands = WorldCommands(candidate.Simulation);
        for (var level = 8U; level > 1; level--)
            UseStair(restoredWorld, restoredCommands, restoredPlayer, level, level - 1);
        Assert.Equal(1U, restoredPlayer.LevelId);
    }

    /** Native CheckTriggers ignores a walking player: the L5 down path crosses the up stair. */
    [Fact(Explicit = true)]
    public void PathThroughTheUpStairReachesTheNextCatacombsDepth()
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var start = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-catacombs-walk", 0,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-catacombs-walk", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-catacombs-walk", value => value);
        var commands = WorldCommands(simulation);
        for (uint level = 1; level < 5; level++)
            UseStair(world, commands, player, level, level + 1);
        var up = world.World!.WarpTargets.Single(row => row.SourceLevelId == 5 && row.TargetLevelId == 4);
        var down = world.World.WarpTargets.Single(row => row.SourceLevelId == 5 && row.TargetLevelId == 6);
        while (player.CurrentAction is { } arrival)
            runtime.AdvanceTo(Math.Max(arrival.EndTick, checked(runtime.LastAdvancedTick + 1)));
        var tick = checked(runtime.LastAdvancedTick + 1);
        var walk = simulation.Execute(player, new DiabloMoveToIntent(down.TriggerX, down.TriggerY, FollowPath: true), tick, [player]);
        Assert.True(walk.Succeeded, $"{walk.Failure} {player.CombatMode} {player.CurrentAction}");
        var crossed = false;
        for (var step = 0; step < 400 && player.LevelId == 5; step++) {
            runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
            crossed |= player.LevelId == 5 && (player.PositionX, player.PositionY) == (up.TriggerX, up.TriggerY);
        }
        Assert.True(crossed, $"The planned path no longer crosses the up stair: L{player.LevelId} ({player.PositionX},{player.PositionY}).");
        Assert.Equal(6U, player.LevelId);
    }

    /** Native multiplayer WM_DIABTOWNWARP: clvl 8 enters L5 first by ENTRY_TWARPDN; L5's warp-up returns. */
    [Fact(Explicit = true)]
    public void MultiplayerTownWarpGatesCharacterLevelEntersL5AndRestoresCold()
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed, DiabloGameMode.Multiplayer);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var down = world.World!.WarpTargets.Single(row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        Assert.Equal((49, 21, 8U, AuthoritativeCatacombsEntry.TownWarpDown),
            (down.TriggerX, down.TriggerY, down.RequiredCharacterLevel, down.FirstEntry));
        var template = activation.PlayerLoadouts.CreateTemplate("native-town-warp", 0,
            null, null, null, startingLevelId: 0, startingPositionX: down.TriggerX, startingPositionY: down.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-town-warp", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-town-warp", value => value);
        var commands = WorldCommands(simulation);

        // Below clvl 8 the warp is refused and the player walks one tile south.
        Assert.Equal(1U, player.CharacterLevel);
        var refused = commands.ExecuteLevelTrigger(player, 0, [player]);
        Assert.Equal(DiabloRuleFailure.NotAllowed, refused.Failure);
        Assert.Equal(0U, player.LevelId);
        var turnBack = Assert.Single(player.NativeWalkPath!.Directions);
        Assert.Equal((0, 1), AuthoritativeNativePlayerPathfinder.GetDisplacement(turnBack));

        player.NativeWalkPath = null;
        player.CharacterLevel = 8;
        UseStair(world, commands, player, 0, 5);
        Assert.Equal((down.TargetX, down.TargetY), (player.PositionX, player.PositionY));
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(new AuthoritativeTownWarpOpenings(true, true, true, false, true), captured.NativeTown!.WarpOpenings);
        var entry = Assert.Single(captured.NativeProgression!.Entries);
        Assert.Equal((5U, AuthoritativeCatacombsEntry.TownWarpDown), (entry.LevelId, entry.CatacombsEntry));

        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(captured);
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredPlayer = restoredRuntime.Read("native-town-warp", value => value);
        UseStair(candidate.Simulation.WorldState, WorldCommands(candidate.Simulation), restoredPlayer, 5, 0);
        Assert.Equal((49, 22), (restoredPlayer.PositionX, restoredPlayer.PositionY));
    }

    [Fact]
    public void CatacombsDepthsOwnOnlyLevelsFiveThroughEight()
    {
        Assert.Equal(new uint[] { 5, 6, 7, 8 }, Enumerable.Range(0, 12).Select(value => (uint)value)
            .Where(AuthoritativeCatacombsNativeDepths.Owns));
        Assert.Equal("cathedral_depth_4_to_catacombs_depth_5",
            $"{AuthoritativeCatacombsNativeDepths.Symbol(4)}_to_{AuthoritativeCatacombsNativeDepths.Symbol(5)}");
        Assert.Equal("cathedral", AuthoritativeCatacombsNativeDepths.Symbol(1));
    }

    [Fact]
    public void CatacombsCheckpointRejectsOwnerStateOutsideEnteredFloors()
    {
        var empty = new DiabloNativeCatacombsCheckpoint([], [], [], []);
        empty.ValidateShape(new HashSet<uint> { 5 });
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = null! }).ValidateShape(new HashSet<uint> { 5 }));
        var door = new AuthoritativeCatacombsDoorState(0x0A000001, 6, 42, AuthoritativeCatacombsDoorKind.Left,
            3, 3, AuthoritativeCatacombsDoorStatus.Closed, 1, 3, false, false);
        (empty with { Doors = [door] }).ValidateShape(new HashSet<uint> { 5, 6 });
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = [door] }).ValidateShape(new HashSet<uint> { 5 }));
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = [null!] }).ValidateShape(new HashSet<uint> { 5 }));
    }

    private static void UseStair(DiabloWorldState world, DiabloWorldCommandProcessor commands,
        AuthoritativePlayerState player, uint source, uint target)
    {
        var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == source && row.TargetLevelId == target);
        player.CurrentAction = null;
        player.PositionX = stair.TriggerX;
        player.PositionY = stair.TriggerY;
        var result = commands.ExecuteLevelTrigger(player, 0, [player]);
        Assert.True(result.Succeeded, $"Stair {source}->{target}: {result.Failure}");
        Assert.Equal(target, player.LevelId);
    }

    private static DiabloWorldCommandProcessor WorldCommands(DiabloSimulation simulation)
        => (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;

    private static (ComposedContent Content, uint Seed) LoadAllClassProfile()
    {
        var directory = Environment.GetEnvironmentVariable(ProfileVariable);
        Assert.False(string.IsNullOrWhiteSpace(directory), $"{ProfileVariable} must name a compiled all-class profile.");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!,
            "cathedral-native-all-class-town-playtest.json")));
        var profile = document.RootElement;
        var nested = profile.GetProperty("classProfile");
        var directories = new[] { "contentDirectory", "populationDirectory", "runtimeDirectory" }
            .Select(key => nested.GetProperty(key).GetString()!)
            .Append(profile.GetProperty("townDirectory").GetString()!)
            .Append(AuthoritativeCatacombsPopulationMetadata.ProfileSidecarDirectory);
        var packs = directories.Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(directory!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        Assert.Equal(profile.GetProperty("contentManifestHash").GetString(), manifest.Sha256);
        return (ComposedContent.Compose(manifest), profile.GetProperty("townInitialSeed").GetUInt32());
    }
}
