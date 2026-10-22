using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemWorldNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Detached and committed behavior at the supported native barrel boundary. */
public sealed class AuthoritativeCathedralBarrelOperationTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 0, false)]
    public void OrdinaryBarrelBranchesMatchNativeItemGeneratorAndSeedBoundary(
        int var2,
        int var3,
        bool useful)
    {
        var world = CreateWorld();
        var before = Snapshot(world, 0x12345678);
        var request = Request(world, var2, var3, seed: 2588);

        var actual = AuthoritativeCathedralBarrelOperationGenerator.Generate(request);

        Assert.False(actual.IsExplosive);
        Assert.Equal(request.InitialWorldRandomState, actual.InitialWorldRandomState);
        Assert.Equal(AuthoritativeCathedralBarrelOpenedMutation.Native, actual.OpenedMutation);
        Assert.False(actual.WorldState.ObjectCanInteract[0]);
        Assert.False(actual.WorldState.ObjectSolid[0]);
        Assert.True(world.ObjectCanInteract[0]);
        Assert.True(world.ObjectSolid[0]);
        AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(
            "caller unchanged", before, Snapshot(world, 0x12345678));
        if (var2 <= 1) {
            Assert.NotNull(actual.Loot);
            var expectedWorld = Copy(world, objectCanInteract: [false], objectSolid: [false]);
            var expectedRequest = new AuthoritativeCathedralThemeItemWorldRequest(
                expectedWorld, request.Catalog, request.Policy, 2588, 50, 50,
                OnlyGood: false, Player: request.Player);
            var expected = useful
                ? AuthoritativeCathedralThemeItemWorldGenerator.CreateUsefulItem(expectedRequest, 1)
                : AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(expectedRequest);
            Assert.Equal(expected.SelectedNativeItemIndex, actual.Loot!.SelectedNativeItemIndex);
            Assert.Equal(expected.SelectedSlot, actual.Loot.Slot);
            Assert.Equal(expected.ItemSeed, actual.Loot.ItemSeed);
            Assert.Equal(expected.Item, actual.Loot.Item);
            Assert.Equal(expected.FinalRandomState, actual.FinalWorldRandomState);
            Assert.Equal(1, actual.WorldState.ActiveItemCount);
            Assert.Equal(expected.ActiveItemIds, actual.WorldState.ActiveItemIds);
            Assert.Equal(expected.ItemRefs, actual.WorldState.ItemRefs);
            Assert.Equal(expected.Items, actual.WorldState.Items);
        } else {
            Assert.Null(actual.Loot);
            Assert.Equal(2588u, actual.FinalWorldRandomState);
            Assert.Equal(0, actual.WorldState.ActiveItemCount);
        }
    }

    [Fact]
    public void LinkedPreSpawnUsesCenterWithoutDrawThenNeighborWithOneDraw()
    {
        var world = CreateWorld();
        var centerAvailable = new bool[9];
        centerAvailable[0] = true;
        var center = AuthoritativeCathedralBarrelOperationGenerator.Generate(
            Request(world, var2: 8, var3: 0, seed: 743271966, spawnTiles: centerAvailable, linked: LinkedMonster(3)));

        Assert.Null(center.Loot);
        Assert.NotNull(center.Spawn);
        Assert.True(center.Spawn!.IsCenterSpawn);
        Assert.Equal((50, 50), (center.Spawn.PositionX, center.Spawn.PositionY));
        Assert.Equal(1, center.Spawn.Facing);
        Assert.Equal(8, center.Spawn.SpecialFrameCount);
        Assert.Equal(743271966u, center.FinalWorldRandomState);

        var neighbors = new bool[9];
        neighbors[1] = true;
        neighbors[2] = true;
        neighbors[3] = true;
        var expectedRandom = new AuthoritativeLegacyRandom(1457187811);
        Assert.Equal(-8, expectedRandom.GenerateRnd(15));
        var expectedDirection = new[] { 4, 5, 6, 3, 7, 2, 1, 0 }[2];
        var neighbor = AuthoritativeCathedralBarrelOperationGenerator.Generate(
            Request(world, var2: 8, var3: 1, seed: 1457187811, spawnTiles: neighbors, linked: LinkedMonster(3)));

        Assert.NotNull(neighbor.Spawn);
        Assert.False(neighbor.Spawn!.IsCenterSpawn);
        Assert.Equal((51, 49), (neighbor.Spawn.PositionX, neighbor.Spawn.PositionY));
        Assert.Equal((expectedDirection + 4) % 8, neighbor.Spawn.Facing);
        Assert.Equal(expectedRandom.State, neighbor.FinalWorldRandomState);
    }

    [Fact]
    public void ExplosiveBarrelOnlyClearsItsObjectFootprintAndLeavesLootRngAndActorsForCombatOwner()
    {
        var world = CreateWorld();
        var request = Request(world, var2: 0, var3: 2, seed: 123, explosive: true,
            initialRandomState: 0x80000000, spawnTiles: Enumerable.Repeat(true, 9).ToArray());
        var before = Snapshot(world, request.InitialWorldRandomState);

        var actual = AuthoritativeCathedralBarrelOperationGenerator.Generate(request);

        Assert.True(actual.IsExplosive);
        Assert.Null(actual.Loot);
        Assert.Null(actual.Spawn);
        Assert.Equal(request.InitialWorldRandomState, actual.FinalWorldRandomState);
        Assert.Equal(0x80000000u, actual.FinalWorldRandomState);
        Assert.False(actual.WorldState.ObjectCanInteract[0]);
        Assert.False(actual.WorldState.ObjectSolid[0]);
        AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(
            "caller unchanged", before, Snapshot(world, request.InitialWorldRandomState));
        Assert.Equal(world.Items, actual.WorldState.Items);
        Assert.Equal(world.ItemRefs, actual.WorldState.ItemRefs);
    }

    [Fact]
    public void RuntimeOwnerRejectionAndForeignOrStaleTransactionsDoNotAllocateOrMutate()
    {
        var profile = RuntimeProfile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = RuntimeRequest(runtime, var2: 0, var3: 1, seed: 2588);
        var before = runtime.CaptureCheckpoint();
        var transaction = runtime.PrepareBarrelOperation(1, request);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());

        var allocations = 0;
        var commits = 0;
        Assert.Null(runtime.CommitBarrelOperation(transaction,
            () => { allocations++; return 0x53000000; }, _ => false, () => false, () => commits++));
        Assert.Equal(0, allocations);
        Assert.Equal(0, commits);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());

        var foreign = new AuthoritativeCathedralItemRuntime(profile);
        var foreignBefore = foreign.CaptureCheckpoint();
        Assert.Throws<InvalidOperationException>(() => foreign.CommitBarrelOperation(transaction,
            () => throw new InvalidOperationException("Foreign runtime must not allocate an entity."),
            _ => false,
            () => throw new InvalidOperationException("Foreign runtime must not invoke owner preflight."),
            () => throw new InvalidOperationException("Foreign runtime must not commit the owner.")));
        AssertCheckpointEqual(foreignBefore, foreign.CaptureCheckpoint());

        var successful = runtime.PrepareBarrelOperation(1, request);
        var committed = runtime.CommitBarrelOperation(successful,
            () => { allocations++; return 0x53000001; }, _ => false, () => true, () => commits++);
        Assert.NotNull(committed);
        Assert.Equal(1, allocations);
        Assert.Equal(1, commits);
        Assert.Single(committed.Projections);
        Assert.Equal(0x53000001u, committed.Projections[0].EntityId);
        Assert.Equal(successful.Operation, committed.Operation);
        var checkpoint = runtime.CaptureCheckpoint();
        Assert.Equal(1, checkpoint.Levels.Single().ActiveItemCount);
        Assert.Equal(committed.Projections[0].Raw,
            checkpoint.Levels.Single().Items[committed.Projections[0].Slot]);
        var restored = new AuthoritativeCathedralItemRuntime(profile, checkpoint);
        AssertCheckpointEqual(checkpoint, restored.CaptureCheckpoint());
    }

    [Fact]
    public void FullPoolBarrelSelectionCommitsOpenButDoesNotAllocateAnItemEntity()
    {
        var profile = RuntimeProfile(AuthoritativeCathedralItemRuntimeTestData.FullPoolCheckpoint());
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = RuntimeRequest(runtime, var2: 1, var3: 1, seed: 2588);
        var prepared = runtime.PrepareBarrelOperation(1, request);
        Assert.NotNull(prepared.Operation.Loot);
        Assert.False(prepared.Operation.Loot!.Allocated);
        Assert.False(prepared.Operation.WorldState.ObjectCanInteract[0]);
        var before = runtime.CaptureCheckpoint();
        var allocated = false;
        var ownerCommits = 0;

        var committed = runtime.CommitBarrelOperation(prepared,
            () => { allocated = true; return 0x53000002; },
            _ => false,
            () => true,
            () => ownerCommits++);

        Assert.NotNull(committed);
        Assert.Empty(committed.Projections);
        Assert.False(allocated);
        Assert.Equal(1, ownerCommits);
        Assert.Equal(127, runtime.CaptureCheckpoint().Levels.Single().ActiveItemCount);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());
    }

    [Fact]
    public void InvalidTrapFootprintAndSpawnBindingAreRefusedBeforeAnyMutation()
    {
        var world = CreateWorld();
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralBarrelOperationGenerator.Generate(
            Request(world, 0, 0, trapArmed: true)));

        var wrongRefs = world.ObjectRefs.ToArray();
        wrongRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = -1;
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralBarrelOperationGenerator.Generate(
            Request(Copy(world, objectRefs: wrongRefs), 0, 0)));

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralBarrelOperationGenerator.Generate(
            Request(world, 8, 0, var4: 3, linked: null)));
        Assert.Equal(0, world.ActiveItemCount);
        Assert.True(world.ObjectSolid[0]);
        Assert.True(world.ObjectCanInteract[0]);
    }

    private static AuthoritativeCathedralItemRuntimeProfile RuntimeProfile(
        DiabloCathedralItemCheckpoint? checkpoint = null)
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.Profile(forceDrop: true);
        var itemCatalog = WithNativeGoldAndUsefulPotions(source.ItemCatalog);
        var objectRefs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        objectRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = 1;
        objectRefs[49 * AuthoritativeCathedralItemRuntime.Width + 50] = -1;
        var baseLevel = source.LevelProfiles.Single();
        var levelProfile = baseLevel with {
            InitialObjectRefs = objectRefs,
            ObjectBindings = [new AuthoritativeCathedralItemObjectBinding(
                0, 900, AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType,
                50, 50, true, 1, MutationPolicy: AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel)],
        };
        return source with {
            InitialCheckpoint = checkpoint ?? source.InitialCheckpoint,
            ItemCatalog = itemCatalog,
            LevelProfiles = [levelProfile],
        };
    }

    private static AuthoritativeCathedralThemeItemCatalog WithNativeGoldAndUsefulPotions(
        AuthoritativeCathedralThemeItemCatalog source)
    {
        var nativeGold = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog().Items
            .Single(value => value.NativeItemIndex == 0);
        var rows = source.Items.Select(value => value.NativeItemIndex switch {
            0 => nativeGold with {
                AnimationFrameCount = source.CursorAnimations[nativeGold.CursorGraphic].FrameCount,
                AnimationTicksPerFrame = source.CursorAnimations[nativeGold.CursorGraphic].TicksPerFrame,
            },
            24 => value with {
                NativeItemId = 0,
                ItemType = 0,
                MiscId = 3,
                CursorGraphic = 32,
                Name = "Potion of Healing",
                ShortName = string.Empty,
                SpellId = 0,
                MinimumMonsterLevel = 0,
                DropRate = 0,
                Usable = true,
                Available = true,
                AnimationFrameCount = source.CursorAnimations[32].FrameCount,
                AnimationTicksPerFrame = source.CursorAnimations[32].TicksPerFrame,
            },
            25 => value with {
                NativeItemId = 0,
                ItemType = 0,
                MiscId = 6,
                CursorGraphic = 39,
                Name = "Potion of Mana",
                ShortName = string.Empty,
                SpellId = 0,
                MinimumMonsterLevel = 0,
                DropRate = 0,
                Usable = true,
                Available = true,
                AnimationFrameCount = source.CursorAnimations[39].FrameCount,
                AnimationTicksPerFrame = source.CursorAnimations[39].TicksPerFrame,
            },
            _ => value,
        }).ToArray();
        return new(rows, source.Prefixes, source.Suffixes, source.Uniques, source.Spells,
            source.CursorAnimations, source.GlyphWidths, source.Cursors, source.AlwaysMagicMiscIds,
            source.Rules, source.ReplacementGlyphWidth);
    }

    private static AuthoritativeCathedralBarrelOperationRequest RuntimeRequest(
        AuthoritativeCathedralItemRuntime runtime,
        int var2,
        int var3,
        uint seed)
    {
        var profile = runtime.Profile;
        var level = profile.LevelProfiles.Single();
        var world = runtime.BuildWorldState(1, new(
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            level.InitialObjectRefs,
            level.SolidTiles,
            level.SetPieceTiles,
            [true],
            [true]));
        return Request(world, var2, var3, seed, catalog: profile.ItemCatalog, policy: profile.ItemPolicy);
    }

    private static AuthoritativeCathedralBarrelOperationRequest Request(
        AuthoritativeCathedralThemeItemWorldState world,
        int var2,
        int var3,
        uint seed = 2588,
        bool explosive = false,
        bool trapArmed = false,
        int? var4 = null,
        IReadOnlyList<bool>? spawnTiles = null,
        AuthoritativeNativeMonsterInitialization? linked = null,
        AuthoritativeCathedralThemeItemCatalog? catalog = null,
        AuthoritativeCathedralThemeItemPolicy? policy = null,
        uint initialRandomState = 0x12345678)
        => new(
            world,
            catalog ?? RuntimeProfile().ItemCatalog,
            policy ?? new(1),
            Facts(var2, var3, seed, explosive, trapArmed, linked, var4),
            spawnTiles ?? new bool[9],
            initialRandomState,
            new(LevelLoading: true, MaximumLifeBase: 6400, MaximumManaBase: 3200));

    private static AuthoritativeCathedralNativeBarrelFacts Facts(
        int var2,
        int var3,
        uint seed,
        bool explosive = false,
        bool trapArmed = false,
        AuthoritativeNativeMonsterInitialization? linked = null,
        int? var4 = null)
        => new(
            0, 900, explosive ? 58 : 57, 1, new(50, 50), seed,
            0, var2, var3, var4 ?? (linked is null ? -1 : linked.Binding.NativeSlot),
            1, 1, 0, 1000, 0, 5, true, true, 1, false,
            trapArmed, false, linked, linked is null ? 0 : 8, linked is null ? 0 : 1);

    private static AuthoritativeNativeMonsterInitialization LinkedMonster(int slot)
    {
        var initial = new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, Type = 8, LevelType = 2, Mode = 0, Goal = 1, Direction = 4,
            UniqueType = 0, Tile = new(0, 0), Future = new(0, 0), Old = new(0, 0),
            Last = new(0, 0), Temp = new(0, 0), EnemyPosition = new(0, 0), HitPoints = 64,
            MaxHitPoints = 64, AnimTicksPerFrame = 1, AnimNumberOfFrames = 8,
        };
        var binding = new AuthoritativeNativeMonsterBinding(
            slot, 8, 0, checked((uint)(7000 + slot)), 800, 1, "skeleton");
        return new(new string('A', 64), initial, 0, 1, 0, 0, binding, false) {
            NativeAttackFrameCount = 8, NativeAttackTicksPerFrame = 1,
            NativeHitRecoveryFrameCount = 8, NativeHitRecoveryTicksPerFrame = 1,
            NativeWalkFrameCount = 8, NativeWalkTicksPerFrame = 1,
        };
    }

    private static AuthoritativeCathedralThemeItemWorldState CreateWorld()
    {
        var profile = RuntimeProfile();
        var level = profile.InitialCheckpoint.Levels.Single();
        var levelProfile = profile.LevelProfiles.Single();
        return new(level.Items, level.ActiveItemIds, level.ActiveItemCount, level.SignedItemRefs,
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            levelProfile.InitialObjectRefs,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            [true], [true], new HashSet<int>());
    }

    private static AuthoritativeCathedralThemeItemWorldState Copy(
        AuthoritativeCathedralThemeItemWorldState world,
        IReadOnlyList<int>? objectRefs = null,
        IReadOnlyList<bool>? objectSolid = null,
        IReadOnlyList<bool>? objectCanInteract = null)
        => new(world.Items, world.ActiveItemIds, world.ActiveItemCount, world.ItemRefs,
            world.PlayerRefs, world.MonsterRefs, objectRefs ?? world.ObjectRefs,
            world.SolidTiles, world.SetPieceTiles, objectSolid ?? world.ObjectSolid,
            objectCanInteract ?? world.ObjectCanInteract,
            world.UniqueItemFlags);

    private static State Snapshot(AuthoritativeCathedralThemeItemWorldState world, uint randomState)
        => AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(world, randomState);

    private static void AssertCheckpointEqual(DiabloCathedralItemCheckpoint expected, DiabloCathedralItemCheckpoint actual)
    {
        Assert.Equal(expected.MetadataFingerprint, actual.MetadataFingerprint);
        Assert.Equal(expected.GenerationFingerprint, actual.GenerationFingerprint);
        Assert.Equal(expected.UniqueItemFlags, actual.UniqueItemFlags);
        var expectedLevel = Assert.Single(expected.Levels);
        var actualLevel = Assert.Single(actual.Levels);
        Assert.Equal(expectedLevel.ActiveItemCount, actualLevel.ActiveItemCount);
        Assert.Equal(expectedLevel.ActiveItemIds, actualLevel.ActiveItemIds);
        Assert.Equal(expectedLevel.SignedItemRefs, actualLevel.SignedItemRefs);
        Assert.Equal(expectedLevel.SlotEntityBindings, actualLevel.SlotEntityBindings);
        Assert.Equal(expectedLevel.Items, actualLevel.Items);
    }
}
