using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable source-grounded checks for death-time Cathedral monster loot. */
public sealed class AuthoritativeCathedralMonsterLootGeneratorTests
{
    [Fact]
    public void MinionRestoresSeedAndSkipsSpawnItemWithoutMutatingWorld()
    {
        var state = EmptyWorld();
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(state, isPlayerMinion: true));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.MinionSkipped, result.Outcome);
        Assert.Equal(result.InitialRandomState, result.FinalRandomState);
        Assert.Equal(0, result.ActiveItemCount);
        Assert.Equal(state.ItemRefs, result.SignedItemGrid);
    }

    [Fact]
    public void MismatchedUniqueMetadataFailsClosedBeforeSelection()
    {
        var request = Request(EmptyWorld(), isUnique: true,
            unique: new AuthoritativeCathedralUniqueDefinition(
                0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                AuthoritativeCathedralUniquePack.None, 0, 0, 0, false));

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(request));
    }

    [Fact]
    public void HellfireMonsterRowsFailClosedEvenUnderNormalPolicy()
    {
        Assert.Throws<NotSupportedException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
                Request(EmptyWorld(), monsterId: 111)));
    }

    [Fact]
    public void InvalidPlayerPowerBaseFailsBeforeNoItemBranch()
    {
        var request = Request(EmptyWorld(), isPlayerMinion: true) with {
            Player = new AuthoritativeCathedralThemeItemPlayerContext(
                true, true, MaximumManaBase: -1),
        };

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(request));
    }

    [Fact]
    public void NoDropFlagReturnsBeforeAnySelectionRoll()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(), monsterId: 1));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.NoDropFlag, result.Outcome);
        Assert.Equal(result.InitialRandomState, result.FinalRandomState);
        Assert.Equal(-1, result.SelectedNativeItemIndex);
    }

    [Fact]
    public void NoItemBranchRetainsNativeUniqueHistoryIncludingAllocatorSentinel()
    {
        var state = EmptyWorld(uniqueItemFlags: new HashSet<int> { 7, 127 });
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(state, monsterId: 1));

        Assert.Contains(7, result.UniqueItemFlags);
        Assert.Contains(127, result.UniqueItemFlags);
    }

    [Fact]
    public void NoDropRollConsumesExactlyTheNativeGateDraw()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(), noDropPercent: 100));
        var expected = new DeterministicLegacyRandom(result.InitialRandomState);
        _ = expected.GenerateRnd(100);

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.NoDropRoll, result.Outcome);
        Assert.Equal(expected.State, result.FinalRandomState);
    }

    [Fact]
    public void OrdinaryGoldRollPrecedesWeightedItemSelection()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(), catalog: Catalog(goldPercent: 100)));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.DroppedItem, result.Outcome);
        Assert.Equal(0, result.SelectedNativeItemIndex);
        Assert.Equal(11, result.Item!.ItemType);
    }

    [Fact]
    public void OrdinaryDropUsesEffectiveLevelForSelectionAndRawLevelForSetup()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(), rawLevel: 4));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.DroppedItem, result.Outcome);
        Assert.Equal(4u, result.EffectiveMonsterLevel);
        Assert.Equal(4, result.RawDataLevel);
        Assert.Equal(4, result.Item!.CreateInfo & 0x3F);
        Assert.Equal(result.SelectedSlot + 1,
            result.SignedItemGrid[Index(result.Item.PositionX, result.Item.PositionY)]);
    }

    [Fact]
    public void UniqueSelectionKeepsFullIntLevelBoundaryAndSkipsGoldMisc()
    {
        var catalog = Catalog(items: new[] {
            Item(0, 11, 4, "Gold", minimumLevel: 0),
            Item(1, 1, 0, "Sword", minimumLevel: 120),
            Item(2, 0, 24, "Book", minimumLevel: 99),
        });
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(), catalog: catalog, seed: 0, isUnique: true,
                unique: new AuthoritativeCathedralUniqueDefinition(
                    0, 0, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    AuthoritativeCathedralUniquePack.None, 0, 0, 0, false),
                rawLevel: 1));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.DroppedItem, result.Outcome);
        Assert.Equal(128u, result.EffectiveMonsterLevel);
        Assert.Equal(1, result.SelectedNativeItemIndex);
        Assert.Equal(1, result.Item!.ItemIndex);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(100, 2)]
    public void FullPoolConsumesSelectionButDoesNotAllocateOrAdvanceItemSeed(int goldPercent, int draws)
    {
        var state = EmptyWorld(activeItemCount: 127);
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(state, catalog: Catalog(goldPercent: goldPercent)));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.PoolFull, result.Outcome);
        Assert.Equal(0u, result.ItemSeed);
        Assert.Equal(-1, result.SelectedSlot);
        Assert.Equal(127, result.ActiveItemCount);
        Assert.Equal(state.ItemRefs, result.SignedItemGrid);
        var expectedState = result.InitialRandomState;
        for (var draw = 0; draw < draws; draw++)
            expectedState = unchecked(expectedState * 0x015A4E35u + 1);
        Assert.Equal(expectedState, result.FinalRandomState);
    }

    [Fact]
    public void UniqueSelectorConvertsWrappedUnsignedLevelToNativeSignedInt()
    {
        var catalog = Catalog(items: new[] {
            Item(0, 11, 4, "Gold", minimumLevel: 0),
            Item(1, 1, 0, "Sword", minimumLevel: 0),
            Item(2, 0, 24, "Book", minimumLevel: 99),
        });
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(activeItemCount: 127), catalog: catalog, isUnique: true,
                rawLevel: -10, unique: new AuthoritativeCathedralUniqueDefinition(
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    AuthoritativeCathedralUniquePack.None, 0, 0, 0, false)));
        Assert.Equal(unchecked((uint)-5), result.EffectiveMonsterLevel);
        Assert.Equal(2, result.SelectedNativeItemIndex);
        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.PoolFull, result.Outcome);
    }

    [Fact]
    public void InvalidQuestPolicyFailsBeforeNoItemBranches()
    {
        var request = Request(EmptyWorld(), isPlayerMinion: true) with {
            Policy = new AuthoritativeCathedralThemeItemPolicy(1, Hellfire: true),
        };

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(request));
    }

    [Fact]
    public void LastAllocatorSlotWritesPositiveNativeReference127()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(activeItemCount: 126)));

        Assert.Equal(126, result.SelectedSlot);
        Assert.Equal(127, result.SignedItemGrid[Index(result.Item!.PositionX, result.Item.PositionY)]);
        Assert.Equal(127, result.ActiveItemCount);
    }

    [Fact]
    public void ExhaustedPlacementKeepsAllocatedZeroItemAndUnreferencedGrid()
    {
        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            Request(EmptyWorld(solidTiles: Enumerable.Repeat(true,
                AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray())));

        Assert.True(result.Dropped);
        Assert.False(result.PlacementSucceeded);
        Assert.Equal(0, result.SelectedSlot);
        Assert.Equal((0, 0), (result.Item!.PositionX, result.Item.PositionY));
        Assert.DoesNotContain(1, result.SignedItemGrid);
    }

    [Fact]
    public void SpecialTreasureBranchFailsClosedInsteadOfFabricatingQuestReward()
    {
        Assert.Throws<NotSupportedException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
                Request(EmptyWorld(), monsterId: 2)));
    }

    private static AuthoritativeCathedralMonsterLootRequest Request(
        AuthoritativeCathedralThemeItemWorldState state,
        AuthoritativeCathedralThemeItemCatalog? catalog = null,
        int monsterId = 0,
        uint seed = 2588,
        bool isUnique = false,
        bool isPlayerMinion = false,
        AuthoritativeCathedralUniqueDefinition? unique = null,
        int rawLevel = 1,
        int? noDropPercent = null)
    {
        var selectedCatalog = catalog ?? Catalog(noDropPercent: noDropPercent ?? 0);
        var monsters = RuntimeCatalog(noDropMonsterId: noDropPercent is null ? 1 : -1,
            specialTreasureMonsterId: 2, rawLevel: rawLevel);
        return new(
            state,
            selectedCatalog,
            new AuthoritativeCathedralThemeItemPolicy(1),
            monsters,
            monsterId,
            seed,
            40,
            40,
            isUnique,
            isPlayerMinion,
            unique,
            new AuthoritativeCathedralThemeItemPlayerContext(true, true));
    }

    private static AuthoritativeCathedralThemeItemWorldState EmptyWorld(
        int activeItemCount = 0,
        IReadOnlyList<bool>? solidTiles = null,
        IReadOnlySet<int>? uniqueItemFlags = null)
    {
        var cells = AuthoritativeCathedralThemeItemWorldState.CellCount;
        return new(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(),
            activeItemCount,
            new int[cells], new int[cells], new int[cells], new int[cells],
            solidTiles ?? new bool[cells], new bool[cells], Array.Empty<bool>(), Array.Empty<bool>(),
            uniqueItemFlags);
    }

    private static AuthoritativeCathedralThemeItemCatalog Catalog(
        IReadOnlyList<AuthoritativeCathedralNativeItemDefinition>? items = null,
        int noDropPercent = 0,
        int goldPercent = 0)
        => new(
            items ?? new[] {
                Item(0, 11, 4, "Gold", minimumLevel: 0),
                Item(1, 1, 0, "Sword", minimumLevel: 0),
                Item(2, 0, 24, "Book", minimumLevel: 0),
            },
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeUniqueDefinition>(),
            new[] {
                new AuthoritativeCathedralNativeSpellDefinition(0, "None", -1, -1,
                    1, 10, 10, 0, 1, 3),
                new AuthoritativeCathedralNativeSpellDefinition(1, "Bolt", 0, 0,
                    1, 10, 10, 0, 1, 3),
            },
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> {
                [4] = new(4), [10] = new(4), [86] = new(4), [87] = new(4), [88] = new(4),
            },
            Enumerable.Range(0, 256).ToDictionary(value => value, _ => 8),
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            rules: new AuthoritativeItemGenerationRules(0, 0, 1, 15, 0, 0, 0, 0,
                noDropPercent, goldPercent),
            replacementGlyphWidth: 8);

    private static AuthoritativeCathedralNativeItemDefinition Item(
        int index, int type, int misc, string name, int minimumLevel)
        => new(index, index, 1, type == 11 ? 4 : 1, 7, type == 11 ? 4 : 10,
            type, name, name, minimumLevel, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            misc, 0, true, 10, 4);

    private static AuthoritativeCathedralMonsterRuntimeCatalog RuntimeCatalog(
        int noDropMonsterId = 1,
        int specialTreasureMonsterId = 2,
        int rawLevel = 1)
    {
        var rows = Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
            .Select(index => new AuthoritativeCathedralMonsterRuntimeDefinition(
                index, $"monster_{index}", string.Empty, index == 0 ? rawLevel : 1, 0, 0, 0, 0, 0, 0,
                index == noDropMonsterId ? 0x4000 : index == specialTreasureMonsterId ? 0x8000 : 0,
                0, 0))
            .ToArray();
        return new(rows);
    }

    private static int Index(int x, int y)
        => checked(y * AuthoritativeCathedralThemeItemWorldState.Width + x);
}
