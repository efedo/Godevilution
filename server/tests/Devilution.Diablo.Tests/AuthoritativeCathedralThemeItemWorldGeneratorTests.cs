using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable regressions for detached native world-item selection/allocation. */
public sealed class AuthoritativeCathedralThemeItemWorldGeneratorTests
{
    private static readonly AuthoritativeCathedralThemeItemPolicy Policy = new(1);

    [Fact]
    public void SelectorConsumesOnlySelectorRandomAndDoesNotAllocate()
    {
        var catalog = Catalog();
        var request = new AuthoritativeCathedralThemeItemWorldSelectionRequest(
            2588, 1, true, catalog, Policy, 1);

        var selected = AuthoritativeCathedralThemeItemWorldGenerator.SelectTypeItem(request);

        var expected = new DeterministicLegacyRandom(request.RandomState);
        Assert.Equal(1, selected.SelectedNativeItemIndex);
        _ = expected.GenerateRnd(1); // one weighted candidate still consumes native RndTypeItems RNG.
        Assert.Equal(expected.State, selected.FinalRandomState);
    }

    [Fact]
    public void CreateTypeItemAllocatesOrderedSlotAndWritesSignedItemReference()
    {
        var state = EmptyWorld();
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, requestedItemType: 1));

        Assert.Equal(0, result.SelectedSlot);
        Assert.Equal(1, result.ActiveItemCount);
        Assert.Equal(0, result.ActiveItemIds[0]);
        Assert.True(result.PlacementSucceeded);
        Assert.Equal(result.SelectedSlot + 1,
            result.ItemRefs[Index(result.PlacedX, result.PlacedY)]);
        Assert.Equal(result.PlacedX, result.Items[result.SelectedSlot].PositionX);
        Assert.Equal(result.PlacedY, result.Items[result.SelectedSlot].PositionY);
    }

    [Fact]
    public void CreateRandomItemUsesNativeRandomSelectorBeforeWorldAllocation()
    {
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(
            Request(EmptyWorld(), onlyGood: false));

        Assert.InRange(result.SelectedNativeItemIndex, 0, 2);
        Assert.Equal(0, result.SelectedSlot);
        Assert.Equal(1, result.ActiveItemCount);
    }

    [Fact]
    public void AllocationZerosSelectedSlotButPreservesInactiveAndScratchSentinels()
    {
        var items = Enumerable.Range(0, 128)
            .Select(index => new AuthoritativeCathedralInitialItemState {
                PositionX = index,
                PlusDamage = index + 10,
                Buff = unchecked((uint)(0xA5000000 + index)),
            })
            .ToArray();
        var state = EmptyWorld(items: items);
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, requestedItemType: 1));

        Assert.Equal(0, result.Items[0].PlusDamage);
        Assert.Equal(0u, result.Items[0].Buff);
        Assert.Equal(17, result.Items[7].PlusDamage);
        Assert.Equal(0xA500007F, result.Items[127].Buff);
    }

    [Fact]
    public void FullActivePoolConsumesSelectorButDoesNotAdvanceSeedAllocateOrPlace()
    {
        var active = Enumerable.Range(0, 127).ToArray();
        var itemRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        var state = EmptyWorld(activeItemIds: active, activeItemCount: 127, itemRefs: itemRefs);
        var selector = AuthoritativeCathedralThemeItemWorldGenerator.SelectTypeItem(
            new AuthoritativeCathedralThemeItemWorldSelectionRequest(
                743271966, 1, true, Catalog(), Policy, 1));

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, randomState: 743271966, requestedItemType: 1));

        Assert.Equal(-1, result.SelectedSlot);
        Assert.Null(result.Item);
        Assert.Equal(127, result.ActiveItemCount);
        Assert.Equal(selector.FinalRandomState, result.FinalRandomState);
        Assert.Equal(itemRefs, result.ItemRefs);
        Assert.Equal(state.Items[127], result.Items[127]);
    }

    [Fact]
    public void LastActiveSlotUsesNativePositiveReference127()
    {
        var state = EmptyWorld(activeItemCount: 126);
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, requestedItemType: 1));

        Assert.Equal(126, result.SelectedSlot);
        Assert.Equal(127, result.ItemRefs[Index(result.PlacedX, result.PlacedY)]);
        Assert.Equal(127, result.ActiveItemCount);
    }

    [Fact]
    public void PreGenerateIsPassedAsTheNativeCreateInfoFlagOnly()
    {
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(EmptyWorld(), preGenerate: true, requestedItemType: 1));

        Assert.NotEqual(0, result.Item!.CreateInfo & 0x8000);
        Assert.False(result.Placement is null);
    }

    [Fact]
    public void SuperItemSpaceUsesInclusiveSquareFallbackWithoutExtraRandomDraw()
    {
        var solids = new bool[AuthoritativeCathedralThemeItemWorldState.CellCount];
        for (var y = 49; y <= 51; y++)
            for (var x = 49; x <= 51; x++)
                solids[Index(x, y)] = true;
        var state = EmptyWorld(solidTiles: solids);

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 50, positionY: 50, requestedItemType: 1));

        Assert.True(result.PlacementSucceeded);
        Assert.True(result.UsedSuperItemSpaceFallback);
        Assert.InRange(result.PlacedX, 0, 111);
        Assert.InRange(result.PlacedY, 0, 111);
        Assert.Equal(1, result.ItemRefs[Index(result.PlacedX, result.PlacedY)]);

        var expected = new DeterministicLegacyRandom(result.SelectorRandomState);
        _ = expected.GenerateRnd(15); // GetItemSpace is the only placement roll.
        Assert.Equal(unchecked((uint)expected.AdvanceRndSeed()), result.ItemSeed);
    }

    [Fact]
    public void SignedNonPositivePlacementRollSelectsUpperLeftWithoutClamping()
    {
        var solids = Enumerable.Repeat(true, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        solids[Index(20, 20)] = false;
        var itemRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        itemRefs[Index(19, 19)] = 99;
        var state = EmptyWorld(solidTiles: solids, itemRefs: itemRefs);

        // Native typed-book selection consumes the one-candidate selector roll;
        // the following placement roll is the signed INT_MIN boundary.
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, randomState: 988045466, positionX: 20, positionY: 20,
                requestedItemType: 0, requestedMiscId: 24));

        Assert.True(result.PlacementSucceeded);
        Assert.Equal((19, 19), (result.PlacedX, result.PlacedY));
        Assert.Equal(1, result.ItemRefs[Index(19, 19)]);
        Assert.Equal(0, result.ItemRefs[Index(20, 20)]);
    }

    [Fact]
    public void SignedNegativePlacementAtUpperLeftFailsClosedWithoutMutatingInput()
    {
        var state = EmptyWorld();
        var originalItems = state.Items.ToArray();
        var originalRefs = state.ItemRefs.ToArray();

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
                Request(state, randomState: 988045466, positionX: 0, positionY: 0,
                    requestedItemType: 0, requestedMiscId: 24)));

        Assert.Equal(0, state.ActiveItemCount);
        Assert.Equal(originalItems, state.Items);
        Assert.Equal(originalRefs, state.ItemRefs);
    }

    [Fact]
    public void SuperItemSpaceSearchIncludesRadius49ButNotRadius50()
    {
        var radius49 = Enumerable.Repeat(true, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        radius49[Index(7, 7)] = false;
        var radius49Result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(EmptyWorld(solidTiles: radius49), positionX: 56, positionY: 56,
                requestedItemType: 1));
        Assert.True(radius49Result.PlacementSucceeded);
        Assert.True(radius49Result.UsedSuperItemSpaceFallback);
        Assert.Equal((7, 7), (radius49Result.PlacedX, radius49Result.PlacedY));

        var radius50 = Enumerable.Repeat(true, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        radius50[Index(6, 6)] = false;
        var radius50Result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(EmptyWorld(solidTiles: radius50), positionX: 56, positionY: 56,
                requestedItemType: 1));
        Assert.False(radius50Result.PlacementSucceeded);
        Assert.True(radius50Result.UsedSuperItemSpaceFallback);
        Assert.Equal((0, 0), (radius50Result.Items[0].PositionX, radius50Result.Items[0].PositionY));
        Assert.DoesNotContain(1, radius50Result.ItemRefs);
    }

    [Fact]
    public void TypedSelectionRejectsDefaultNoneInsteadOfFallingBackToRandom()
    {
        var selection = new AuthoritativeCathedralThemeItemWorldSelectionRequest(
            2588, 1, true, Catalog(), Policy);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemWorldGenerator.SelectTypeItem(selection));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(Request(EmptyWorld())));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
                new AuthoritativeCathedralThemeItemSelectionRequest(2588, 1, true),
                Catalog(), Policy));
    }

    [Fact]
    public void SetPieceDoesNotBlockItemSpaceButSolidAndEntityRefsDo()
    {
        var setPieces = new bool[AuthoritativeCathedralThemeItemWorldState.CellCount];
        setPieces[Index(20, 20)] = true;
        var solids = new bool[AuthoritativeCathedralThemeItemWorldState.CellCount];
        for (var y = 19; y <= 21; y++)
            for (var x = 19; x <= 21; x++)
                solids[Index(x, y)] = true;
        solids[Index(20, 20)] = false;
        var state = EmptyWorld(solidTiles: solids, setPieceTiles: setPieces);
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 20, positionY: 20, requestedItemType: 1));

        Assert.True(result.PlacementSucceeded);
        Assert.Equal(20, result.PlacedX);
        Assert.Equal(20, result.PlacedY);

        solids[Index(20, 20)] = true;
        solids[Index(19, 19)] = false;
        var blockedState = EmptyWorld(solidTiles: solids, setPieceTiles: setPieces);
        var blockedResult = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(blockedState, positionX: 20, positionY: 20, requestedItemType: 1));
        Assert.True(blockedResult.PlacementSucceeded);
        Assert.Equal((19, 19), (blockedResult.PlacedX, blockedResult.PlacedY));
    }

    [Fact]
    public void SolidInteractiveObjectBlocksAccordingToNativeSouthAndDiagonalRules()
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        objectRefs[Index(30, 30)] = 1;
        var objectSolid = new[] { true };
        var state = EmptyWorld(objectRefs: objectRefs, objectSolid: objectSolid,
            objectCanInteract: new[] { false });

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 30, positionY: 30, requestedItemType: 1));

        Assert.True(result.PlacementSucceeded);
        Assert.NotEqual((30, 30), (result.PlacedX, result.PlacedY));
    }

    [Fact]
    public void SouthInteractiveObjectBlocksForPositiveAndNegativeReferences()
    {
        foreach (var objectReference in new[] { 1, -1 }) {
            var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
            objectRefs[Index(41, 41)] = objectReference;
            var state = ObjectPlacementWorld(objectRefs, new[] { false }, new[] { true });

            var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
                Request(state, positionX: 40, positionY: 40, requestedItemType: 1));

            Assert.True(result.PlacementSucceeded);
            Assert.True(result.UsedSuperItemSpaceFallback);
            Assert.Equal((38, 38), (result.PlacedX, result.PlacedY));
        }
    }

    [Fact]
    public void DiagonalInteractiveObjectsRequireBothPositiveReferences()
    {
        foreach (var (southEast, southWest, expectedFallback) in new[] {
            (1, 2, true),
            (-1, 2, false),
            (1, -2, false),
        }) {
            var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
            objectRefs[Index(41, 40)] = southEast;
            objectRefs[Index(40, 41)] = southWest;
            var state = ObjectPlacementWorld(objectRefs, new[] { false, false }, new[] { true, true });

            var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
                Request(state, positionX: 40, positionY: 40, requestedItemType: 1));

            Assert.True(result.PlacementSucceeded);
            Assert.Equal(expectedFallback, result.UsedSuperItemSpaceFallback);
            Assert.Equal(expectedFallback ? (38, 38) : (40, 40),
                (result.PlacedX, result.PlacedY));
        }
    }

    [Fact]
    public void InteractiveDiagonalSingletonDoesNotBlockItemSpace()
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        objectRefs[Index(41, 40)] = 1;
        var state = ObjectPlacementWorld(objectRefs, new[] { false }, new[] { true });

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 40, positionY: 40, requestedItemType: 1));

        Assert.True(result.PlacementSucceeded);
        Assert.False(result.UsedSuperItemSpaceFallback);
        Assert.Equal((40, 40), (result.PlacedX, result.PlacedY));
    }

    [Fact]
    public void DirectNegativeObjectReferenceUsesSolidFlagAndNotInteraction()
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        objectRefs[Index(40, 40)] = -1;

        var solidState = ObjectPlacementWorld(objectRefs, new[] { true }, new[] { false });
        var solidResult = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(solidState, positionX: 40, positionY: 40, requestedItemType: 1));
        Assert.True(solidResult.PlacementSucceeded);
        Assert.True(solidResult.UsedSuperItemSpaceFallback);

        var nonsolidState = ObjectPlacementWorld(objectRefs, new[] { false }, new[] { true });
        var nonsolidResult = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(nonsolidState, positionX: 40, positionY: 40, requestedItemType: 1));
        Assert.True(nonsolidResult.PlacementSucceeded);
        Assert.False(nonsolidResult.UsedSuperItemSpaceFallback);
        Assert.Equal((40, 40), (nonsolidResult.PlacedX, nonsolidResult.PlacedY));
    }

    [Fact]
    public void SignedPlayerAndMonsterReferencesBlockItemSpace()
    {
        var playerRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        var monsterRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        playerRefs[Index(40, 40)] = -1;
        monsterRefs[Index(39, 39)] = -1;
        var state = new AuthoritativeCathedralThemeItemWorldState(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(), 0,
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            playerRefs,
            monsterRefs,
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            Array.Empty<bool>(),
            Array.Empty<bool>());

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 40, positionY: 40, requestedItemType: 1));

        Assert.True(result.PlacementSucceeded);
        Assert.NotEqual((40, 40), (result.PlacedX, result.PlacedY));
        Assert.NotEqual((39, 39), (result.PlacedX, result.PlacedY));
    }

    [Fact]
    public void ExhaustedPlacementKeepsAllocatedZeroPositionAndDoesNotWriteGrid()
    {
        var solids = Enumerable.Repeat(true, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        var itemRefs = Enumerable.Repeat(0, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        var state = EmptyWorld(solidTiles: solids, itemRefs: itemRefs);

        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(state, positionX: 56, positionY: 56, requestedItemType: 1));

        Assert.False(result.PlacementSucceeded);
        Assert.True(result.UsedSuperItemSpaceFallback);
        Assert.Equal((0, 0), (result.Items[0].PositionX, result.Items[0].PositionY));
        Assert.Equal(itemRefs, result.ItemRefs);
        Assert.Equal(1, result.ActiveItemCount);
    }

    [Fact]
    public void WorldInputAndNestedUniqueHistoryAreDetached()
    {
        var itemRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        var flags = new HashSet<int>();
        var state = EmptyWorld(itemRefs: itemRefs, uniqueItemFlags: flags);
        var request = Request(state, requestedItemType: 1);
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(request);

        itemRefs[0] = 127;
        flags.Add(9);
        Assert.Equal(0, result.WorldState.ItemRefs[0]);
        Assert.DoesNotContain(9, result.UniqueItemFlags);
    }

    [Fact]
    public void UniqueHistoryAcceptsNativeFinalSlot127()
    {
        var state = EmptyWorld(uniqueItemFlags: new HashSet<int> { 127 });

        Assert.Contains(127, state.UniqueItemFlags);
    }

    [Fact]
    public void UnsafeObjectReferenceAndUnsupportedPolicyFailClosed()
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        objectRefs[0] = 2;
        Assert.Throws<InvalidDataException>(() => EmptyWorld(objectRefs: objectRefs));

        var policy = Policy with { GameMode = DiabloGameMode.Multiplayer };
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            Request(EmptyWorld(), policy: policy, requestedItemType: 1)));
    }

    [Fact]
    public void GoldSelectionRequiresNativeRowZeroAndPreservesItsMetadata()
    {
        var customGold = Catalog().Items
            .Select(item => item.NativeItemIndex == 0 ? item with { Flags = 0x40000000u } : item)
            .ToArray();
        var customCatalog = Catalog(customGold);
        var generated = AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
            new AuthoritativeCathedralThemeItemSelectionRequest(2588, 1, false, 11),
            customCatalog, Policy);

        Assert.Equal(0, generated.SelectedNativeItemIndex);
        Assert.Equal(0x40000000u, generated.Item.Flags);

        var relocatedGold = Catalog().Items
            .Select(item => item.NativeItemIndex == 0
                ? item with { ItemType = 1 }
                : item.NativeItemIndex == 1 ? item with { ItemType = 11 } : item)
            .ToArray();
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
            new AuthoritativeCathedralThemeItemSelectionRequest(2588, 1, false, 11),
            Catalog(relocatedGold), Policy));

        var unavailableGold = Catalog().Items
            .Select(item => item.NativeItemIndex == 0 ? item with { Available = false } : item)
            .ToArray();
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
            new AuthoritativeCathedralThemeItemSelectionRequest(2588, 1, false, 11),
            Catalog(unavailableGold), Policy));
    }

    [Fact]
    public void OnlyGoodBookBypassRequiresMiscItemType()
    {
        var highLevelBook = Catalog().Items
            .Select(item => item.NativeItemIndex is 1 or 2
                ? item with { MinimumMonsterLevel = 99 }
                : item)
            .ToArray();
        var validBookSelection = AuthoritativeCathedralThemeItemWorldGenerator.SelectRandomItem(
            new AuthoritativeCathedralThemeItemWorldSelectionRequest(
                2588, 1, true, Catalog(highLevelBook), Policy));
        Assert.Equal(2, validBookSelection.SelectedNativeItemIndex);

        var mismatchedBook = highLevelBook
            .Select(item => item.NativeItemIndex == 2 ? item with { ItemType = 1 } : item)
            .ToArray();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemWorldGenerator.SelectRandomItem(
                new AuthoritativeCathedralThemeItemWorldSelectionRequest(
                    2588, 1, true, Catalog(mismatchedBook), Policy)));
    }

    [Fact]
    public void WorldPlacementRejectsOutOfBoundsOriginsBeforeNativeOffsetting()
    {
        foreach (var coordinate in new[] { int.MinValue, int.MinValue + 1, -1, 112, int.MaxValue }) {
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
                    Request(EmptyWorld(), positionX: coordinate, requestedItemType: 1)));
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
                    Request(EmptyWorld(), positionY: coordinate, requestedItemType: 1)));
        }
    }

    private static AuthoritativeCathedralThemeItemWorldRequest Request(
        AuthoritativeCathedralThemeItemWorldState state,
        uint randomState = 2588,
        int positionX = 40,
        int positionY = 40,
        bool onlyGood = true,
        bool preGenerate = false,
        int requestedItemType = -1,
        int requestedMiscId = -1,
        AuthoritativeCathedralThemeItemPolicy? policy = null)
        => new(state, Catalog(), policy ?? Policy, randomState, positionX, positionY,
            onlyGood, preGenerate, requestedItemType, requestedMiscId,
            new AuthoritativeCathedralThemeItemPlayerContext(true, true));

    private static AuthoritativeCathedralThemeItemWorldState EmptyWorld(
        IReadOnlyList<AuthoritativeCathedralInitialItemState>? items = null,
        IReadOnlyList<int>? activeItemIds = null,
        int activeItemCount = 0,
        IReadOnlyList<int>? itemRefs = null,
        IReadOnlyList<int>? playerRefs = null,
        IReadOnlyList<int>? monsterRefs = null,
        IReadOnlyList<int>? objectRefs = null,
        IReadOnlyList<bool>? solidTiles = null,
        IReadOnlyList<bool>? setPieceTiles = null,
        IReadOnlyList<bool>? objectSolid = null,
        IReadOnlyList<bool>? objectCanInteract = null,
        IReadOnlySet<int>? uniqueItemFlags = null)
    {
        var cells = AuthoritativeCathedralThemeItemWorldState.CellCount;
        return new(
            items ?? Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            activeItemIds ?? Enumerable.Range(0, 127).ToArray(),
            activeItemCount,
            itemRefs ?? new int[cells],
            playerRefs ?? new int[cells],
            monsterRefs ?? new int[cells],
            objectRefs ?? new int[cells],
            solidTiles ?? new bool[cells],
            setPieceTiles ?? new bool[cells],
            objectSolid ?? Array.Empty<bool>(),
            objectCanInteract ?? Array.Empty<bool>(),
            uniqueItemFlags);
    }

    private static AuthoritativeCathedralThemeItemWorldState ObjectPlacementWorld(
        IReadOnlyList<int> objectRefs,
        IReadOnlyList<bool> objectSolid,
        IReadOnlyList<bool> objectCanInteract)
    {
        var solids = Enumerable.Repeat(true, AuthoritativeCathedralThemeItemWorldState.CellCount).ToArray();
        solids[Index(40, 40)] = false;
        solids[Index(38, 38)] = false;
        return EmptyWorld(
            objectRefs: objectRefs,
            solidTiles: solids,
            objectSolid: objectSolid,
            objectCanInteract: objectCanInteract);
    }

    private static AuthoritativeCathedralThemeItemCatalog Catalog(
        IReadOnlyList<AuthoritativeCathedralNativeItemDefinition>? items = null)
    {
        var defaultItems = new[] {
            Item(0, 11, 4, "Gold"),
            Item(1, 1, 0, "Sword"),
            Item(2, 0, 24, "Book"),
        };
        return new(
            items ?? defaultItems,
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeUniqueDefinition>(),
            Enumerable.Range(0, 58).Select(index => new AuthoritativeCathedralNativeSpellDefinition(
                index, $"Spell{index}", index == 0 ? -1 : 0, index == 1 ? 0 : -1,
                1, 10, 10, 0, 1, 3)).ToArray(),
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> {
                [4] = new(4), [10] = new(4), [86] = new(4),
                [87] = new(4), [88] = new(4),
            },
            Enumerable.Range(0, 256).ToDictionary(value => value, _ => 8),
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            rules: new AuthoritativeItemGenerationRules(0, 0, 1, 15, 4, 0, 0, 0, 59, 0),
            replacementGlyphWidth: 8);
    }

    private static AuthoritativeCathedralNativeItemDefinition Item(
        int index, int type, int misc, string name)
        => new(index, index, 1, type == 11 ? 4 : 1, 7, type == 11 ? 4 : 10,
            type, name, name, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            misc, 0, true, 10, 4);

    private static int Index(int x, int y)
        => checked(y * AuthoritativeCathedralThemeItemWorldState.Width + x);
}
