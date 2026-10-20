using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsNativeObjectPolicyTests
{
    [Fact]
    public void ClassifiesEveryNativeObjectRowAndKeepsUnimplementedOperationsExplicit()
    {
        for (var id = 0; id < AuthoritativeCatacombsNativeObjectPolicy.NativeObjectCount; id++) {
            var classification = AuthoritativeCatacombsNativeObjectPolicy.Classify(id);
            Assert.Equal(id, classification.NativeObjectId);
            Assert.True(Enum.IsDefined(classification.Role));
            Assert.True(Enum.IsDefined(classification.Operation));
        }

        Assert.Equal(AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            AuthoritativeCatacombsNativeObjectPolicy.Classify(68).Operation);
        Assert.True(AuthoritativeCatacombsNativeObjectPolicy.Classify(68).SupportedInCatacombs);
        Assert.Equal(AuthoritativeCatacombsNativeObjectOperation.Door,
            AuthoritativeCatacombsNativeObjectPolicy.Classify(42).Operation);
        Assert.True(AuthoritativeCatacombsNativeObjectPolicy.Classify(42).SupportedInCatacombs);
        Assert.Equal(AuthoritativeCatacombsNativeObjectRole.AutonomousTrap,
            AuthoritativeCatacombsNativeObjectPolicy.Classify(53).Role);
        Assert.False(AuthoritativeCatacombsNativeObjectPolicy.Classify(53).SupportedInCatacombs);
        Assert.Equal(AuthoritativeCatacombsNativeObjectOperation.Cauldron,
            AuthoritativeCatacombsNativeObjectPolicy.Classify(80).Operation);
        Assert.False(AuthoritativeCatacombsNativeObjectPolicy.Classify(80).SupportedInCatacombs);
        Assert.Equal(AuthoritativeCatacombsNativeObjectRole.Decorative,
            AuthoritativeCatacombsNativeObjectPolicy.Classify(0).Role);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsNativeObjectPolicy.Classify(-1));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsNativeObjectPolicy.Classify(109));
    }

    [Theory]
    [InlineData(20, AuthoritativeCatacombsNativeObjectOperation.Crux, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(21, AuthoritativeCatacombsNativeObjectOperation.Crux, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(22, AuthoritativeCatacombsNativeObjectOperation.Crux, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(26, AuthoritativeCatacombsNativeObjectOperation.BurningCross, AuthoritativeCatacombsNativeObjectRole.AutonomousEffect)]
    [InlineData(41, AuthoritativeCatacombsNativeObjectOperation.ChamberOfBoneBook, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(50, AuthoritativeCatacombsNativeObjectOperation.TrapLever, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(49, AuthoritativeCatacombsNativeObjectOperation.FlameTrap, AuthoritativeCatacombsNativeObjectRole.AutonomousTrap)]
    [InlineData(52, AuthoritativeCatacombsNativeObjectOperation.Unused, AuthoritativeCatacombsNativeObjectRole.InertSelectable)]
    [InlineData(84, AuthoritativeCatacombsNativeObjectOperation.MagicCircle, AuthoritativeCatacombsNativeObjectRole.AutonomousEffect)]
    [InlineData(85, AuthoritativeCatacombsNativeObjectOperation.MagicCircle, AuthoritativeCatacombsNativeObjectRole.AutonomousEffect)]
    [InlineData(91, AuthoritativeCatacombsNativeObjectOperation.BurningCross, AuthoritativeCatacombsNativeObjectRole.AutonomousEffect)]
    [InlineData(99, AuthoritativeCatacombsNativeObjectOperation.Barrel, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(100, AuthoritativeCatacombsNativeObjectOperation.Barrel, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(101, AuthoritativeCatacombsNativeObjectOperation.Barrel, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    [InlineData(102, AuthoritativeCatacombsNativeObjectOperation.Barrel, AuthoritativeCatacombsNativeObjectRole.Interactive)]
    public void KnownUnimplementedBehaviorIsExplicitInsteadOfDecorative(int id,
        AuthoritativeCatacombsNativeObjectOperation operation, AuthoritativeCatacombsNativeObjectRole role)
    {
        var actual = AuthoritativeCatacombsNativeObjectPolicy.Classify(id);
        Assert.Equal(operation, actual.Operation);
        Assert.Equal(role, actual.Role);
        Assert.False(actual.SupportedInCatacombs);
    }

    [Theory]
    [InlineData(55)]
    [InlineData(56)]
    [InlineData(78)]
    [InlineData(93)]
    [InlineData(98)]
    public void InertDisplayAliasesRemainDecorative(int rawId)
    {
        var actual = AuthoritativeCatacombsNativeObjectPolicy.Classify(rawId);
        Assert.Equal(AuthoritativeCatacombsNativeObjectRole.Decorative, actual.Role);
        Assert.Equal(AuthoritativeCatacombsNativeObjectOperation.None, actual.Operation);
        Assert.False(actual.SupportedInCatacombs);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    [InlineData(7, 7)]
    [InlineData(68, 5)]
    [InlineData(69, 6)]
    [InlineData(70, 7)]
    public void TrappedChestIdsMapOnlyTheirLootTier(int rawObjectId, int lootTier)
        => Assert.Equal(lootTier, AuthoritativeCatacombsNativeObjectPolicy.LootTierForChestType(rawObjectId));

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(68, false)]
    [InlineData(69, false)]
    [InlineData(70, false)]
    [InlineData(68, true)]
    [InlineData(69, true)]
    [InlineData(70, true)]
    public void AcceptsAllOrdinaryAndIntrinsicTrappedChestIds(int nativeType, bool trapFlag)
    {
        var result = Generate(Chest(nativeType, trapFlag, var4: 0, itemCount: 0));

        Assert.Equal(nativeType, result.Input.NativeObjectId);
        Assert.Equal(trapFlag, result.Input.TrapFlag);
        Assert.Equal(trapFlag, result.TrapPlan is not null);
        Assert.Empty(result.Loot.Entries);
        Assert.False(result.OpenedMutation.TrapFlag);
    }

    [Fact]
    public void IntrinsicTrapPlanUsesCurrentPlayerTileAndNativeArrowFacts()
    {
        var arrow = Generate(Chest(nativeType: 69, trap: true, var4: 0, itemCount: 2, animationFrame: 3),
            playerX: 54, playerY: 51);

        Assert.Equal(69, arrow.Input.NativeObjectId);
        Assert.Equal(2, arrow.Loot.Entries.Count);
        Assert.False(arrow.Loot.WorldState.ObjectCanInteract[0]);
        Assert.Equal(new AuthoritativeCatacombsChestOpenedMutation(5, 0, false), arrow.OpenedMutation);
        Assert.NotNull(arrow.TrapPlan);
        Assert.Equal(AuthoritativeCatacombsChestTrapState.Armed, arrow.TrapPlan!.StateBefore);
        Assert.Equal(AuthoritativeCatacombsChestTrapState.Fired, arrow.TrapPlan.StateDuring);
        Assert.Equal(AuthoritativeCatacombsChestTrapState.Disarmed, arrow.TrapPlan.StateAfter);
        Assert.Equal(AuthoritativeCatacombsChestTrapKind.Arrow, arrow.TrapPlan.Kind);
        Assert.Equal("Arrow", arrow.TrapPlan.NativeMissileId);
        Assert.Equal((50, 50), (arrow.TrapPlan.SourceX, arrow.TrapPlan.SourceY));
        Assert.Equal((54, 51), (arrow.TrapPlan.TargetX, arrow.TrapPlan.TargetY));
        Assert.Equal((int)DeterministicGridDirection.SouthEast, arrow.TrapPlan.Direction);
        Assert.True(arrow.TrapPlan.TargetsPlayers);
        Assert.Equal(-1, arrow.TrapPlan.OwnerId);
        Assert.Equal(0, arrow.TrapPlan.Damage);
    }

    [Fact]
    public void FireArrowTrapIsConsumedEvenWhenSeedAndItemCountAreZero()
    {
        var actual = Generate(Chest(nativeType: 70, trap: true, var4: 1,
            itemCount: 0, randomSeed: 0, animationFrame: 9), playerX: 49, playerY: 50);

        Assert.Equal(0u, actual.Loot.InitialRandomState);
        Assert.Equal(0u, actual.Loot.FinalRandomState);
        Assert.Empty(actual.Loot.Entries);
        Assert.NotNull(actual.TrapPlan);
        Assert.Equal(AuthoritativeCatacombsChestTrapKind.FireArrow, actual.TrapPlan!.Kind);
        Assert.Equal("FireArrow", actual.TrapPlan.NativeMissileId);
        Assert.Equal((int)DeterministicGridDirection.NorthWest, actual.TrapPlan.Direction);
        Assert.False(actual.OpenedMutation.TrapFlag);
        Assert.False(actual.Loot.WorldState.ObjectCanInteract[0]);
    }

    [Theory]
    [InlineData(5, false, -1, 1)]
    [InlineData(68, true, 0, 1)]
    [InlineData(69, true, 1, 2)]
    [InlineData(70, true, 1, 3)]
    public void RejectsItemCountAboveTheAliasedLootTier(
        int nativeType, bool trap, int var4, int maximumItems)
    {
        var invalidCount = Request(Chest(nativeType, trap, var4, maximumItems + 1));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsChestLootGenerator.Generate(
            invalidCount, AuthoritativeNativeDungeonFamilyContext.Catacombs(5)));
    }

    [Fact]
    public void RejectsVar2OutsideGeneratedEightWayBranchRange()
    {
        var request = Request(Chest(69, trap: true, var4: 0, itemCount: 1, itemBranch: 8));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsChestLootGenerator.Generate(
            request, AuthoritativeNativeDungeonFamilyContext.Catacombs(5)));
    }

    [Fact]
    public void RejectsInvalidIntrinsicTrapKindAndTrapTypeMismatch()
    {
        Assert.Throws<InvalidDataException>(() => Generate(Chest(68, trap: true, var4: 2, itemCount: 1)));
        Assert.Throws<InvalidDataException>(() => Generate(Chest(5, trap: true, var4: 0, itemCount: 1)));
    }

    [Fact]
    public void LinkedOrdinaryChestPreservesWallTriggerAndDoesNotEmitIntrinsicMissile()
    {
        var wall = new AuthoritativeCathedralPopulationObjectState {
            Slot = 1, Type = 53, Position = new(40, 40), Var1 = 50, Var2 = 50, Var3 = 1,
        };
        var chest = Chest(5, trap: true, var4: 0, itemCount: 1) with { LinkedWallTrap = wall };
        var request = Request(chest);
        var source = request.WorldState;
        var refs = source.ObjectRefs.ToArray();
        refs[40 * AuthoritativeCathedralThemeItemWorldState.Width + 40] = 2;
        var world = new AuthoritativeCathedralThemeItemWorldState(source.Items, source.ActiveItemIds,
            source.ActiveItemCount, source.ItemRefs, source.PlayerRefs, source.MonsterRefs, refs,
            source.SolidTiles, source.SetPieceTiles, [true, false], [true, false], source.UniqueItemFlags);
        var result = AuthoritativeCatacombsChestLootGenerator.Generate(request with { WorldState = world },
            AuthoritativeNativeDungeonFamilyContext.Catacombs(5));
        Assert.True(result.OpenedMutation.TrapFlag);
        Assert.Null(result.TrapPlan);
        Assert.Single(result.Loot.Entries);
        var negativeRefs = refs.ToArray();
        negativeRefs[40 * AuthoritativeCathedralThemeItemWorldState.Width + 40] = -2;
        var negativeWorld = new AuthoritativeCathedralThemeItemWorldState(source.Items, source.ActiveItemIds,
            source.ActiveItemCount, source.ItemRefs, source.PlayerRefs, source.MonsterRefs, negativeRefs,
            source.SolidTiles, source.SetPieceTiles, [true, false], [true, false], source.UniqueItemFlags);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsChestLootGenerator.Generate(
            request with { WorldState = negativeWorld }, AuthoritativeNativeDungeonFamilyContext.Catacombs(5)));
        var wrong = chest with { LinkedWallTrap = wall with { Var1 = 49 } };
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsChestLootGenerator.Generate(
            request with { WorldState = world, Chest = wrong }, AuthoritativeNativeDungeonFamilyContext.Catacombs(5)));
    }

    [Theory]
    [InlineData(68, 1)]
    [InlineData(69, 2)]
    [InlineData(70, 3)]
    public void DisarmedTrappedChestRetainsRawTypeAndLootWithoutFiring(int rawType, int itemCount)
    {
        var result = Generate(Chest(rawType, trap: false, var4: 1, itemCount));
        Assert.Equal(rawType, result.Input.NativeObjectId);
        Assert.Equal(itemCount, result.Loot.Entries.Count);
        Assert.Null(result.TrapPlan);
        Assert.False(result.OpenedMutation.TrapFlag);
        Assert.False(result.Loot.WorldState.ObjectCanInteract[0]);
    }

    [Fact]
    public void C1PublicGeneratorContinuesToRejectIntrinsicTrapsAndC2Depths()
    {
        var chest = Chest(68, trap: true, var4: 0, itemCount: 1);
        var source = Request(chest);
        var trappedRequest = new AuthoritativeCathedralChestLootRequest(
            source.WorldState, source.Catalog, new(1),
            (AuthoritativeCathedralOrdinaryChestType)68, chest.RandomSeed,
            chest.PositionX, chest.PositionY, chest.Var1, chest.Var2, TrapArmed: true) {
                ChestObjectSlot = chest.ObjectSlot,
            };
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralChestLootGenerator.Generate(trappedRequest));
        var ordinaryC2Request = trappedRequest with {
            Policy = new(5), ChestType = AuthoritativeCathedralOrdinaryChestType.Chest1, TrapArmed = false,
        };
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralChestLootGenerator.Generate(ordinaryC2Request));
    }

    private static AuthoritativeCatacombsChestLootResult Generate(
        AuthoritativeCatacombsNativeChestFacts chest,
        int playerX = 54,
        int playerY = 51)
        => AuthoritativeCatacombsChestLootGenerator.Generate(
            Request(chest) with { CurrentPlayerX = playerX, CurrentPlayerY = playerY },
            AuthoritativeNativeDungeonFamilyContext.Catacombs(5));

    private static AuthoritativeCatacombsChestLootRequest Request(
        AuthoritativeCatacombsNativeChestFacts chest)
        => new(CreateWorld(chest.PositionX, chest.PositionY),
            AuthoritativeCathedralMonsterLootGeneratorTests.Catalog(),
            new(5), chest, 54, 51,
            new(PlayerPresent: true, LevelLoading: true, MaximumManaBase: 3200, MaximumLifeBase: 6400));

    private static AuthoritativeCatacombsNativeChestFacts Chest(
        int nativeType,
        bool trap,
        int var4,
        int itemCount,
        uint randomSeed = 2588,
        uint animationFrame = 1,
        int itemBranch = 1)
        => new(0, nativeType, 50, 50, randomSeed, itemCount, itemBranch, var4, trap, animationFrame);

    private static AuthoritativeCathedralThemeItemWorldState CreateWorld(int x, int y)
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        objectRefs[y * AuthoritativeCathedralThemeItemWorldState.Width + x] = 1;
        return new(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(),
            0,
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            objectRefs,
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            [true],
            [true]);
    }
}
