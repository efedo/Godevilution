using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralSarcophagusOperationTests
{
    [Fact]
    public void LootBranchRunsOneRandomWorldItemAfterClearingSelection()
    {
        var world = CreateWorld();
        var before = Capture(world);
        var request = Request(world, var1: 2, var2: -1, seed: 2588);
        var expectedWorld = CopyWithSelectionCleared(world);
        var expected = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(
            new(expectedWorld, request.Catalog, request.Policy, 2588, 50, 50, OnlyGood: false,
                Player: request.Player));

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(request);

        Assert.NotNull(actual.Loot);
        Assert.Null(actual.Spawn);
        Assert.Equal(1, actual.WorldState.ActiveItemCount);
        Assert.Equal(expected.SelectedNativeItemIndex, actual.Loot.SelectedNativeItemIndex);
        Assert.Equal(expected.SelectedSlot, actual.Loot.Slot);
        Assert.Equal(expected.ItemSeed, actual.Loot.ItemSeed);
        Assert.Equal(expected.Item, actual.Loot.Item);
        Assert.Equal(expected.Placement, actual.Loot.Placement);
        AssertWorldStateEqual(expected.WorldState, actual.WorldState);
        Assert.Equal(2588u, actual.InitialRandomState);
        Assert.Equal(expected.FinalRandomState, actual.FinalRandomState);
        Assert.False(actual.WorldState.ObjectCanInteract[0]);
        Assert.True(world.ObjectCanInteract[0]);
        AssertWorldStateEqual(before, world);
    }

    [Fact]
    public void EmptyBranchClearsSelectionWithoutConsumingAnOperationDraw()
    {
        var world = CreateWorld();
        var before = Capture(world);

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 7, var2: -1, seed: 0x80000000, spawnTiles: new bool[9]));

        Assert.Null(actual.Loot);
        Assert.Null(actual.Spawn);
        Assert.Equal(0x80000000u, actual.InitialRandomState);
        Assert.Equal(0x80000000u, actual.FinalRandomState);
        Assert.False(actual.WorldState.ObjectCanInteract[0]);
        AssertWorldStateEqual(before, world);
    }

    [Fact]
    public void LinkedSkeletonUsesCenterFirstWithNativeSouthWestFacingAndNoDraw()
    {
        var world = CreateWorld();
        var spawnTiles = new bool[9];
        spawnTiles[0] = true;

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 8, var2: 3, seed: 743271966, spawnTiles: spawnTiles, linked: LinkedMonster(3)));

        Assert.Null(actual.Loot);
        Assert.NotNull(actual.Spawn);
        Assert.Equal(3, actual.Spawn.NativeMonsterSlot);
        Assert.Equal(7003u, actual.Spawn.EntityId);
        Assert.Equal(8, actual.Spawn.NativeTypeId);
        Assert.Equal((50, 50), (actual.Spawn.PositionX, actual.Spawn.PositionY));
        Assert.Equal(1, actual.Spawn.Facing);
        Assert.True(actual.Spawn.IsCenterSpawn);
        Assert.Equal(8, actual.Spawn.SpecialFrameCount);
        Assert.Equal(0, actual.Spawn.SpecialTicksPerFrame);
        Assert.Equal(743271966u, actual.FinalRandomState);
    }

    [Fact]
    public void NeighborSpawnUsesNativeCandidateOrderSingleRandomDrawAndOppositeFacing()
    {
        var world = CreateWorld();
        var available = Enumerable.Repeat(true, 9).ToArray();
        available[0] = false;
        var expectedRandom = new AuthoritativeLegacyRandom(1);
        var nativeChoice = expectedRandom.GenerateRnd(15);
        var selected = unchecked((int)(unchecked((ulong)(long)nativeChoice) % 8UL));
        var directions = new[] { 4, 5, 6, 3, 7, 2, 1, 0 };
        var xOffsets = new[] { 1, 0, -1, -1, -1, 0, 1, 1 };
        var yOffsets = new[] { 1, 1, 1, 0, -1, -1, -1, 0 };
        var direction = directions[selected];

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 9, var2: 3, seed: 1, spawnTiles: available, linked: LinkedMonster(3)));

        Assert.NotNull(actual.Spawn);
        Assert.False(actual.Spawn.IsCenterSpawn);
        Assert.Equal((50 + xOffsets[direction], 50 + yOffsets[direction]),
            (actual.Spawn.PositionX, actual.Spawn.PositionY));
        Assert.Equal((direction + 4) % 8, actual.Spawn.Facing);
        Assert.Equal(expectedRandom.State, actual.FinalRandomState);
    }

    [Fact]
    public void NegativeNativeSpawnChoiceUsesUnsignedSizeTRemainder()
    {
        var world = CreateWorld();
        var available = new bool[9];
        available[1] = true; // North
        available[2] = true; // NorthEast
        available[3] = true; // East
        var expectedRandom = new AuthoritativeLegacyRandom(1457187811);
        var nativeChoice = expectedRandom.GenerateRnd(15);
        Assert.Equal(-8, nativeChoice);
        var choice = unchecked((int)(unchecked((ulong)(long)nativeChoice) % 3UL));
        Assert.Equal(2, choice);

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 8, var2: 3, seed: 1457187811, spawnTiles: available, linked: LinkedMonster(3)));

        Assert.NotNull(actual.Spawn);
        Assert.Equal((51, 49), (actual.Spawn.PositionX, actual.Spawn.PositionY));
        Assert.Equal(2, actual.Spawn.Facing);
        Assert.Equal(expectedRandom.State, actual.FinalRandomState);
    }

    [Fact]
    public void NoAvailableSpawnLeavesPreallocatedActorUnchangedAndConsumesNoDraw()
    {
        var world = CreateWorld();

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 8, var2: 3, seed: uint.MaxValue, linked: LinkedMonster(3)));

        Assert.Null(actual.Loot);
        Assert.Null(actual.Spawn);
        Assert.Equal(uint.MaxValue, actual.FinalRandomState);
    }

    [Fact]
    public void MissingPreSpawnReservationLeavesSarcophagusEmptyWithoutDrawing()
    {
        var world = CreateWorld();

        var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 8, var2: -1, seed: 743271966));

        Assert.Null(actual.Loot);
        Assert.Null(actual.Spawn);
        Assert.Equal(743271966u, actual.FinalRandomState);
    }

    [Fact]
    public void GeneratorRefusesTrapWrongSignedFootprintAndUnboundPreSpawnActor()
    {
        var world = CreateWorld();
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 3, var2: -1, trapArmed: true)));

        var badRefs = world.ObjectRefs.ToArray();
        badRefs[49 * AuthoritativeCathedralItemRuntime.Width + 50] = 1;
        var wrongFootprint = Copy(world, objectRefs: badRefs);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(wrongFootprint, var1: 3, var2: -1)));

        var badOriginRefs = world.ObjectRefs.ToArray();
        badOriginRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = 0;
        var wrongOrigin = Copy(world, objectRefs: badOriginRefs);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(wrongOrigin, var1: 3, var2: -1)));

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            Request(world, var1: 8, var2: 3, linked: null)));
    }

    [Fact]
    public void RuntimeCommitPreflightsOwnerAndColdRestoresExactLootPool()
    {
        var profile = RuntimeProfile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = RuntimeRequest(runtime, var1: 2, var2: -1, seed: 2588);
        var before = runtime.CaptureCheckpoint();
        var prepared = runtime.PrepareSarcophagusOperation(1, request);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());
        var nextEntityId = 0x50000000u;
        var ownerCommitCount = 0;

        var committed = runtime.CommitSarcophagusOperation(
            prepared,
            () => nextEntityId++,
            _ => false,
            () => true,
            () => ownerCommitCount++);

        Assert.NotNull(committed);
        Assert.Equal(1, ownerCommitCount);
        Assert.Single(committed.Projections);
        Assert.Equal(0x50000000u, committed.Projections[0].EntityId);
        Assert.Equal(prepared.Operation, committed.Operation);
        var checkpoint = runtime.CaptureCheckpoint();
        Assert.NotEqual(before.Levels[0].ActiveItemCount, checkpoint.Levels[0].ActiveItemCount);
        Assert.Equal(committed.Projections[0].Raw, checkpoint.Levels[0].Items[committed.Projections[0].Slot]);
        var restored = new AuthoritativeCathedralItemRuntime(profile, checkpoint);
        AssertCheckpointEqual(checkpoint, restored.CaptureCheckpoint());
    }

    [Fact]
    public void RejectedAndStaleRuntimeTransactionsDoNotAllocateOrOpenTwice()
    {
        var profile = RuntimeProfile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = RuntimeRequest(runtime, var1: 2, var2: -1, seed: 1);
        var rejected = runtime.PrepareSarcophagusOperation(1, request);
        var secondRuntime = new AuthoritativeCathedralItemRuntime(profile);
        var secondRuntimeBefore = secondRuntime.CaptureCheckpoint();
        Assert.Throws<InvalidOperationException>(() => secondRuntime.CommitSarcophagusOperation(
            rejected,
            () => throw new InvalidOperationException("Entity allocation must not run for a foreign transaction."),
            _ => false,
            () => throw new InvalidOperationException("Owner validation must not run for a foreign transaction."),
            () => throw new InvalidOperationException("Owner commit must not run for a foreign transaction.")));
        AssertCheckpointEqual(secondRuntimeBefore, secondRuntime.CaptureCheckpoint());

        var nextEntityId = 0x51000000u;
        var ownerCommitCount = 0;
        var before = runtime.CaptureCheckpoint();

        var result = runtime.CommitSarcophagusOperation(
            rejected,
            () => { nextEntityId++; return nextEntityId - 1; },
            _ => false,
            () => false,
            () => ownerCommitCount++);

        Assert.Null(result);
        Assert.Equal(0x51000000u, nextEntityId);
        Assert.Equal(0, ownerCommitCount);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());

        var first = runtime.PrepareSarcophagusOperation(1, request);
        var stale = runtime.PrepareSarcophagusOperation(1, request);
        Assert.NotNull(runtime.CommitSarcophagusOperation(first,
            () => nextEntityId++, _ => false, () => true, () => ownerCommitCount++));
        var afterFirst = runtime.CaptureCheckpoint();
        Assert.Throws<InvalidOperationException>(() => runtime.CommitSarcophagusOperation(stale,
            () => nextEntityId++, _ => false, () => true, () => ownerCommitCount++));
        Assert.Equal(1, ownerCommitCount);
        AssertCheckpointEqual(afterFirst, runtime.CaptureCheckpoint());
    }

    [Fact]
    public void FullPoolSarcophagusStillAdvancesRandomSelectionButCreatesNoBinding()
    {
        var profile = RuntimeProfile(AuthoritativeCathedralItemRuntimeTestData.FullPoolCheckpoint());
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = RuntimeRequest(runtime, var1: 0, var2: -1, seed: 2588);
        var prepared = runtime.PrepareSarcophagusOperation(1, request);
        Assert.NotNull(prepared.Operation.Loot);
        Assert.False(prepared.Operation.Loot.Allocated);
        Assert.DoesNotContain(true, prepared.Operation.WorldState.ObjectCanInteract);

        var before = runtime.CaptureCheckpoint();
        var allocated = false;
        var committed = runtime.CommitSarcophagusOperation(prepared,
            () => { allocated = true; return 0x52000000u; }, _ => false, () => true, () => { });

        Assert.NotNull(committed);
        Assert.Empty(committed.Projections);
        Assert.False(allocated);
        Assert.Equal(127, runtime.CaptureCheckpoint().Levels[0].ActiveItemCount);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());
    }

    private static AuthoritativeCathedralItemRuntimeProfile RuntimeProfile(
        DiabloCathedralItemCheckpoint? checkpoint = null)
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.Profile();
        source = source with { ItemCatalog = CatalogWithAvailableGold(source.ItemCatalog) };
        var objectRefs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        objectRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = 1;
        objectRefs[49 * AuthoritativeCathedralItemRuntime.Width + 50] = -1;
        var levelProfile = new AuthoritativeCathedralItemLevelProfile(
            1,
            1,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            objectRefs,
            [new AuthoritativeCathedralItemObjectBinding(
                0,
                900,
                48,
                50,
                50,
                false,
                1,
                MutationPolicy: AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus)]);
        var bindings = source.ItemCatalog.Items.ToDictionary(
            item => item.NativeItemIndex,
            item => new AuthoritativeCathedralItemContentBinding(
                item.NativeItemIndex,
                checked((uint)(6000 + item.NativeItemIndex)),
                $"native_item_{item.NativeItemIndex}"));
        return source with {
            InitialCheckpoint = checkpoint ?? source.InitialCheckpoint,
            ItemBindings = bindings,
            LevelProfiles = [levelProfile],
        };
    }

    private static AuthoritativeCathedralThemeItemCatalog CatalogWithAvailableGold(
        AuthoritativeCathedralThemeItemCatalog sourceCatalog)
    {
        var gold = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog().Items
            .Single(item => item.NativeItemIndex == 0);
        gold = gold with {
            AnimationFrameCount = sourceCatalog.CursorAnimations[gold.CursorGraphic].FrameCount,
            AnimationTicksPerFrame = sourceCatalog.CursorAnimations[gold.CursorGraphic].TicksPerFrame,
        };
        var itemCatalog = new AuthoritativeCathedralThemeItemCatalog(
            sourceCatalog.Items.Select(item => item.NativeItemIndex == 0 ? gold : item),
            sourceCatalog.Prefixes,
            sourceCatalog.Suffixes,
            sourceCatalog.Uniques,
            sourceCatalog.Spells,
            sourceCatalog.CursorAnimations,
            sourceCatalog.GlyphWidths,
            sourceCatalog.Cursors,
            sourceCatalog.AlwaysMagicMiscIds,
            sourceCatalog.Rules,
            sourceCatalog.ReplacementGlyphWidth);
        return itemCatalog;
    }

    private static AuthoritativeCathedralSarcophagusOperationRequest RuntimeRequest(
        AuthoritativeCathedralItemRuntime runtime,
        int var1,
        int var2,
        uint seed)
    {
        var profile = runtime.Profile;
        var level = Assert.Single(profile.LevelProfiles);
        var world = runtime.BuildWorldState(1, new(
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            level.InitialObjectRefs,
            level.SolidTiles,
            level.SetPieceTiles,
            [false],
            [true]));
        return Request(world, var1, var2, seed, catalog: profile.ItemCatalog, policy: profile.ItemPolicy);
    }

    private static AuthoritativeCathedralSarcophagusOperationRequest Request(
        AuthoritativeCathedralThemeItemWorldState world,
        int var1,
        int var2,
        uint seed = 2588,
        IReadOnlyList<bool>? spawnTiles = null,
        bool trapArmed = false,
        AuthoritativeNativeMonsterInitialization? linked = null,
        AuthoritativeCathedralThemeItemCatalog? catalog = null,
        AuthoritativeCathedralThemeItemPolicy? policy = null)
        => new(
            world,
            catalog ?? CatalogWithAvailableGold(AuthoritativeCathedralItemRuntimeTestData.Profile().ItemCatalog),
            policy ?? new(1),
            Facts(var1, var2, seed, trapArmed, linked),
            spawnTiles ?? new bool[9],
            new(LevelLoading: true, MaximumLifeBase: 6400, MaximumManaBase: 3200));

    private static AuthoritativeCathedralNativeSarcophagusFacts Facts(
        int var1,
        int var2,
        uint seed,
        bool trapArmed = false,
        AuthoritativeNativeMonsterInitialization? linked = null)
        => new(
            0,
            900,
            48,
            1,
            new(50, 50),
            seed,
            var1,
            var2,
            1,
            1,
            0,
            1,
            0,
            8,
            trapArmed,
            false,
            linked,
            8,
            0);

    private static AuthoritativeNativeMonsterInitialization LinkedMonster(int slot)
    {
        var initial = new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot,
            Type = 8,
            LevelType = 2,
            Mode = 0,
            Goal = 1,
            Direction = 4,
            UniqueType = 0,
            Tile = new(0, 0),
            Future = new(0, 0),
            Old = new(0, 0),
            Last = new(0, 0),
            Temp = new(0, 0),
            EnemyPosition = new(0, 0),
            HitPoints = 64,
            MaxHitPoints = 64,
            AnimTicksPerFrame = 1,
            AnimNumberOfFrames = 8,
        };
        var binding = new AuthoritativeNativeMonsterBinding(
            slot, 8, 0, checked((uint)(7000 + slot)), 800, 1, "skeleton");
        return new(
            new string('A', 64),
            initial,
            0,
            1,
            0,
            0,
            binding,
            false) {
            NativeAttackFrameCount = 8,
            NativeAttackTicksPerFrame = 1,
            NativeHitRecoveryFrameCount = 8,
            NativeHitRecoveryTicksPerFrame = 1,
            NativeWalkFrameCount = 8,
            NativeWalkTicksPerFrame = 1,
        };
    }

    private static AuthoritativeCathedralThemeItemWorldState CreateWorld()
    {
        var profile = RuntimeProfile();
        var level = profile.InitialCheckpoint.Levels.Single();
        var binding = profile.LevelProfiles.Single().ObjectBindings.Single();
        return new(
            level.Items,
            level.ActiveItemIds,
            level.ActiveItemCount,
            level.SignedItemRefs,
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            profile.LevelProfiles.Single().InitialObjectRefs,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            [binding.InitialSolid],
            [true],
            new HashSet<int>());
    }

    private static AuthoritativeCathedralThemeItemWorldState CopyWithSelectionCleared(
        AuthoritativeCathedralThemeItemWorldState world)
        => Copy(world, objectCanInteract: [false]);

    private static AuthoritativeCathedralThemeItemWorldState Copy(
        AuthoritativeCathedralThemeItemWorldState world,
        IReadOnlyList<int>? objectRefs = null,
        IReadOnlyList<bool>? objectCanInteract = null)
        => new(
            world.Items,
            world.ActiveItemIds,
            world.ActiveItemCount,
            world.ItemRefs,
            world.PlayerRefs,
            world.MonsterRefs,
            objectRefs ?? world.ObjectRefs,
            world.SolidTiles,
            world.SetPieceTiles,
            world.ObjectSolid,
            objectCanInteract ?? world.ObjectCanInteract,
            world.UniqueItemFlags);

    private static AuthoritativeCathedralThemeItemWorldState Capture(
        AuthoritativeCathedralThemeItemWorldState world)
        => Copy(world);

    private static void AssertWorldStateEqual(
        AuthoritativeCathedralThemeItemWorldState expected,
        AuthoritativeCathedralThemeItemWorldState actual)
    {
        Assert.Equal(expected.ActiveItemCount, actual.ActiveItemCount);
        Assert.Equal(expected.ActiveItemIds, actual.ActiveItemIds);
        Assert.Equal(expected.ItemRefs, actual.ItemRefs);
        Assert.Equal(expected.PlayerRefs, actual.PlayerRefs);
        Assert.Equal(expected.MonsterRefs, actual.MonsterRefs);
        Assert.Equal(expected.ObjectRefs, actual.ObjectRefs);
        Assert.Equal(expected.SolidTiles, actual.SolidTiles);
        Assert.Equal(expected.SetPieceTiles, actual.SetPieceTiles);
        Assert.Equal(expected.ObjectSolid, actual.ObjectSolid);
        Assert.Equal(expected.ObjectCanInteract, actual.ObjectCanInteract);
        Assert.Equal(expected.UniqueItemFlags.Order(), actual.UniqueItemFlags.Order());
        Assert.Equal(expected.Items, actual.Items);
    }

    private static void AssertCheckpointEqual(
        DiabloCathedralItemCheckpoint expected,
        DiabloCathedralItemCheckpoint actual)
    {
        Assert.Equal(expected.MetadataFingerprint, actual.MetadataFingerprint);
        Assert.Equal(expected.GenerationFingerprint, actual.GenerationFingerprint);
        Assert.Equal(expected.UniqueItemFlags, actual.UniqueItemFlags);
        Assert.Equal(expected.Levels.Count, actual.Levels.Count);
        for (var index = 0; index < expected.Levels.Count; index++) {
            var left = expected.Levels[index];
            var right = actual.Levels[index];
            Assert.Equal(left.LevelId, right.LevelId);
            Assert.Equal(left.Depth, right.Depth);
            Assert.Equal(left.ActiveItemCount, right.ActiveItemCount);
            Assert.Equal(left.ActiveItemIds, right.ActiveItemIds);
            Assert.Equal(left.SignedItemRefs, right.SignedItemRefs);
            Assert.Equal(left.SlotEntityBindings, right.SlotEntityBindings);
            Assert.Equal(left.Items, right.Items);
        }
    }
}
