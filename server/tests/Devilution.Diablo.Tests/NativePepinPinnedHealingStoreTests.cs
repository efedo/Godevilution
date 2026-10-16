using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativePepinPinnedHealingStoreTests
{
    [Fact]
    public void NativeProfilePreparationReplacesOnlyPinnedHealerRowsAndPreservesSeeds()
    {
        var items = CreateItems();
        var stores = TsvTable.Parse(
            "stores.tsv",
            "store_id\tstore_symbol\tstore_role\tstore_slot\titem_seed\tprice\titem_id\titem_symbol\n"
                + "1\tsmith\tSmith\t0\t42\t75\t1\tshort_sword\n"
                + "4\thealer\tHealer\t0\t300\t25\t2\tbuckler\n"
                + "4\thealer\tHealer\t1\t301\t50\t1\tshort_sword\n");
        var sidecars = CreateSidecars();
        var legacyPrepared = AuthoritativeCathedralNativePopulationBuilder.PrepareNativeProfileStoreTable(
            stores,
            items,
            sidecars);
        var legacyHealing = Assert.Single(legacyPrepared.Rows.Where(row => row.RequiredUInt32("store_id") == 4
            && row.RequiredUInt32("store_slot") == 0));
        Assert.Equal("2", legacyHealing.Required("item_id"));
        Assert.Equal("buckler", legacyHealing.Required("item_symbol"));
        Assert.Equal("25", legacyHealing.Required("price"));
        Assert.Equal("300", legacyHealing.Required("item_seed"));

        var prepared = AuthoritativeCathedralNativePopulationBuilder.PrepareNativeProfileStoreTable(
            stores,
            items,
            sidecars,
            nativeProgression: true);

        var smith = Assert.Single(prepared.Rows.Where(row => row.RequiredUInt32("store_id") == 1));
        Assert.Equal("1", smith.Required("item_id"));
        Assert.Equal("short_sword", smith.Required("item_symbol"));
        Assert.Equal("42", smith.Required("item_seed"));
        Assert.Equal("75", smith.Required("price"));

        var healing = Assert.Single(prepared.Rows.Where(row => row.RequiredUInt32("store_id") == 4
            && row.RequiredUInt32("store_slot") == 0));
        Assert.Equal("2180", healing.Required("item_id"));
        Assert.Equal("potion_healing_standard", healing.Required("item_symbol"));
        Assert.Equal("300", healing.Required("item_seed"));
        Assert.Equal("50", healing.Required("price"));

        var fullHealing = Assert.Single(prepared.Rows.Where(row => row.RequiredUInt32("store_id") == 4
            && row.RequiredUInt32("store_slot") == 1));
        Assert.Equal("2181", fullHealing.Required("item_id"));
        Assert.Equal("potion_full_healing", fullHealing.Required("item_symbol"));
        Assert.Equal("301", fullHealing.Required("item_seed"));
        Assert.Equal("150", fullHealing.Required("price"));
        var capability = AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable();
        Assert.Equal("native_pinned_stores.tsv", capability.SourcePath);
        Assert.Equal(2, capability.Rows.Count);
        Assert.Equal("2180", capability.Rows[0].Required("item_id"));
        Assert.Equal("77", capability.Rows[0].Required("native_item_index"));
        Assert.Equal("50", capability.Rows[0].Required("price"));
        Assert.Equal("2181", capability.Rows[1].Required("item_id"));
        Assert.Equal("78", capability.Rows[1].Required("native_item_index"));
        Assert.Equal("150", capability.Rows[1].Required("price"));
    }

    [Fact]
    public void NativePinnedPurchasesAdvanceOwnedSeedButRetainStockAcrossCohortRestore()
    {
        var items = CreateItems();
        var catalog = CreatePinnedCatalog(items);
        var pinnedStock = Assert.Single(catalog.GetItems(4).Where(item => item.StoreSlot == 0));
        Assert.Equal(6u, pinnedStock.State.CreateInfo);
        Assert.False(pinnedStock.State.Identified);
        Assert.True(pinnedStock.State.StatFlag);
        var random = new DiabloRandom(0x12345678);
        var expectedRandom = new DiabloRandom(random.State);
        var player = CreatePlayer(300);
        var processor = new DiabloStoreCommandProcessor(catalog, random: random);

        for (var purchase = 1; purchase <= 2; purchase++) {
            var expectedSeed = unchecked((uint)expectedRandom.AdvanceRndSeed());
            var result = processor.Execute(player, new DiabloPurchaseIntent(4, 0), (ulong)purchase);

            Assert.True(result.Succeeded);
            var ownedBeltItem = Assert.Single(player.Belt.Where(item => item.ItemSeed == expectedSeed));
            Assert.Equal((uint)(purchase - 1), ownedBeltItem.Slot);
            Assert.Equal(pinnedStock.State, ownedBeltItem.State);
            Assert.Empty(player.Inventory);
            Assert.Equal(pinnedStock, Assert.Single(catalog.GetItems(4).Where(item => item.StoreSlot == 0)));
            Assert.Equal(expectedRandom.State, random.State);
        }

        var checkpoint = catalog.CaptureAll();
        var restored = RestoreCatalog(catalog, checkpoint);
        Assert.Equal(pinnedStock, Assert.Single(restored.GetItems(4).Where(item => item.StoreSlot == 0)));
        var tamperedCheckpoint = checkpoint.Select(store => store with {
            Items = store.Items?.Select(item => item.StoreSlot == 0
                ? item with { ItemSeed = unchecked(item.ItemSeed + 1) }
                : item).ToArray(),
        }).ToArray();
        Assert.Throws<InvalidDataException>(() => RestoreCatalog(catalog, tamperedCheckpoint));
        var resumedRandom = new DiabloRandom(random.State);
        var expectedResumedRandom = new DiabloRandom(random.State);
        var resumedPlayer = CreatePlayer(200);
        var resumedProcessor = new DiabloStoreCommandProcessor(restored, random: resumedRandom);
        var resumedSeed = unchecked((uint)expectedResumedRandom.AdvanceRndSeed());

        var resumedResult = resumedProcessor.Execute(resumedPlayer, new DiabloPurchaseIntent(4, 0), 3);

        Assert.True(resumedResult.Succeeded);
        Assert.Equal(resumedSeed, Assert.Single(resumedPlayer.Belt).ItemSeed);
        Assert.Empty(resumedPlayer.Inventory);
        Assert.Equal(pinnedStock, Assert.Single(restored.GetItems(4).Where(item => item.StoreSlot == 0)));
        Assert.Equal(expectedResumedRandom.State, resumedRandom.State);
    }

    [Fact]
    public void RefusedPinnedPurchasesDoNotChangeGoldInventoryStockOrRandomState()
    {
        var items = CreateItems();
        var insufficientCatalog = CreatePinnedCatalog(items);
        var insufficientStock = insufficientCatalog.CaptureAll();
        var insufficientRandom = new DiabloRandom(77);
        var insufficientPlayer = CreatePlayer(49);
        var insufficientProcessor = new DiabloStoreCommandProcessor(insufficientCatalog, random: insufficientRandom);

        var insufficient = insufficientProcessor.Execute(
            insufficientPlayer,
            new DiabloPurchaseIntent(4, 0),
            1);

        Assert.False(insufficient.Succeeded);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, insufficient.Failure);
        Assert.Equal(49u, insufficientPlayer.Gold);
        Assert.Empty(insufficientPlayer.Inventory);
        Assert.Empty(insufficientPlayer.Belt);
        AssertStoreCheckpointEqual(insufficientStock, insufficientCatalog.CaptureAll());
        Assert.Equal(77u, insufficientRandom.State);

        var fullCatalog = CreatePinnedCatalog(items);
        var fullStock = fullCatalog.CaptureAll();
        var fullRandom = new DiabloRandom(88);
        var blockers = CreateInventoryBlockers();
        var fullBelt = CreateBeltBlockers(8);
        var fullPlayer = CreatePlayer(300, blockers, Enumerable.Range(0, 40).ToArray(), fullBelt);
        var fullProcessor = new DiabloStoreCommandProcessor(fullCatalog, random: fullRandom);

        var full = fullProcessor.Execute(fullPlayer, new DiabloPurchaseIntent(4, 0), 1);

        Assert.False(full.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, full.Failure);
        Assert.Equal(300u, fullPlayer.Gold);
        Assert.Equal(blockers, fullPlayer.Inventory);
        Assert.Equal(Enumerable.Range(0, 40), fullPlayer.InventoryGrid);
        Assert.Equal(fullBelt, fullPlayer.Belt);
        AssertStoreCheckpointEqual(fullStock, fullCatalog.CaptureAll());
        Assert.Equal(88u, fullRandom.State);
    }

    [Fact]
    public void NativePinnedPurchasePrefersFreeBeltAndFallsBackToInventory()
    {
        var items = CreateItems();
        var fullInventory = CreateInventoryBlockers();
        var almostFullBelt = CreateBeltBlockers(7);
        var beltCatalog = CreatePinnedCatalog(items);
        var expectedBeltState = Assert.Single(beltCatalog.GetItems(4).Where(item => item.StoreSlot == 0)).State;
        var beltRandom = new DiabloRandom(0x23456789);
        var expectedBeltRandom = new DiabloRandom(beltRandom.State);
        var beltPlayer = CreatePlayer(300, fullInventory, Enumerable.Range(0, 40).ToArray(), almostFullBelt);

        var beltResult = new DiabloStoreCommandProcessor(beltCatalog, random: beltRandom)
            .Execute(beltPlayer, new DiabloPurchaseIntent(4, 0), 1);

        Assert.True(beltResult.Succeeded);
        var beltSeed = unchecked((uint)expectedBeltRandom.AdvanceRndSeed());
        var purchasedBeltPotion = Assert.Single(beltPlayer.Belt.Where(item => item.ItemSeed == beltSeed));
        Assert.Equal(7U, purchasedBeltPotion.Slot);
        Assert.Equal(expectedBeltState, purchasedBeltPotion.State);
        Assert.Equal(fullInventory, beltPlayer.Inventory);
        Assert.Equal(Enumerable.Range(0, 40), beltPlayer.InventoryGrid);
        Assert.Equal(250U, beltPlayer.Gold);
        Assert.Equal(expectedBeltRandom.State, beltRandom.State);

        var fallbackCatalog = CreatePinnedCatalog(items);
        var expectedInventoryState = Assert.Single(fallbackCatalog.GetItems(4).Where(item => item.StoreSlot == 0)).State;
        var fullBelt = CreateBeltBlockers(8);
        var fallbackPlayer = CreatePlayer(300, belt: fullBelt);
        var fallbackRandom = new DiabloRandom(0x3456789A);
        var expectedFallbackRandom = new DiabloRandom(fallbackRandom.State);

        var fallbackResult = new DiabloStoreCommandProcessor(fallbackCatalog, random: fallbackRandom)
            .Execute(fallbackPlayer, new DiabloPurchaseIntent(4, 0), 2);

        Assert.True(fallbackResult.Succeeded);
        var inventorySeed = unchecked((uint)expectedFallbackRandom.AdvanceRndSeed());
        var purchasedInventoryPotion = Assert.Single(fallbackPlayer.Inventory);
        Assert.Equal(inventorySeed, purchasedInventoryPotion.ItemSeed);
        Assert.Equal(expectedInventoryState, purchasedInventoryPotion.State);
        Assert.Equal(0, fallbackPlayer.InventoryGrid[30]);
        Assert.Equal(fullBelt, fallbackPlayer.Belt);
        Assert.Equal(250U, fallbackPlayer.Gold);
        Assert.Equal(expectedFallbackRandom.State, fallbackRandom.State);
    }

    [Fact]
    public void LegacyFiniteHealerStockRemainsConsumable()
    {
        var items = CreateItems();
        var catalog = new StoreCatalog();
        catalog.AddStore(4, [
            new StoreItem(0, 300, 50, items.Generate(2180, 300)),
            new StoreItem(1, 301, 150, items.Generate(2181, 301)),
        ], AuthoritativeStoreRole.Healer);
        Assert.False(catalog.TryGetNativePinnedStoreItem(4, 0, out _));
        var player = CreatePlayer(300);
        var processor = new DiabloStoreCommandProcessor(catalog, random: new DiabloRandom(99));

        var result = processor.Execute(player, new DiabloPurchaseIntent(4, 0), 1);

        Assert.True(result.Succeeded);
        Assert.Single(player.Inventory);
        Assert.DoesNotContain(catalog.GetItems(4), item => item.StoreSlot == 0);
    }

    [Fact]
    public void NativePinnedSetupRejectsPartialIdentityWithoutChangingStock()
    {
        var items = CreateItems();
        var catalog = new StoreCatalog();
        catalog.AddStore(4, [
            new StoreItem(0, 300, 50, items.Generate(2180, 300)),
            new StoreItem(1, 301, 50, items.Generate(1, 301)),
        ], AuthoritativeStoreRole.Healer);
        var before = catalog.CaptureAll();

        Assert.Throws<InvalidDataException>(() => catalog.ConfigureNativePepinPinnedHealingStock(
            items,
            AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable()));

        AssertStoreCheckpointEqual(before, catalog.CaptureAll());
    }

    [Fact]
    public void NativePinnedSetupRejectsAlteredCapabilityBeforeChangingStock()
    {
        var items = CreateItems();
        var catalog = CreateUnconfiguredPinnedCatalog(items);
        var before = catalog.CaptureAll();
        var valid = AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable();
        var altered = TsvTable.Parse(
            valid.SourcePath,
            valid.ToCanonicalTsv().Replace("\t77\t50", "\t77\t49", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => catalog.ConfigureNativePepinPinnedHealingStock(items, altered));

        AssertStoreCheckpointEqual(before, catalog.CaptureAll());
    }

    private static StoreCatalog CreatePinnedCatalog(AuthoritativeItemCatalog items)
    {
        var catalog = CreateUnconfiguredPinnedCatalog(items);
        catalog.ConfigureNativePepinPinnedHealingStock(
            items,
            AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable());
        return catalog;
    }

    private static StoreCatalog CreateUnconfiguredPinnedCatalog(AuthoritativeItemCatalog items)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(4, [
            new StoreItem(0, 300, 50, items.Generate(2180, 300)),
            new StoreItem(1, 301, 150, items.Generate(2181, 301)),
        ], AuthoritativeStoreRole.Healer);
        return catalog;
    }

    private static StoreCatalog RestoreCatalog(
        StoreCatalog source,
        IReadOnlyList<DiabloStoreCatalogState> checkpoint)
    {
        var restored = new StoreCatalog();
        foreach (var store in source.CaptureAll())
            restored.AddStore(store.StoreId, store.Items, store.Role);
        source.CopyNativePinnedStorePoliciesTo(restored);
        restored.RestoreAll(checkpoint);
        return restored;
    }

    private static void AssertStoreCheckpointEqual(
        IReadOnlyList<DiabloStoreCatalogState> expected,
        IReadOnlyList<DiabloStoreCatalogState> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var storeIndex = 0; storeIndex < expected.Count; storeIndex++) {
            Assert.Equal(expected[storeIndex].StoreId, actual[storeIndex].StoreId);
            Assert.Equal(expected[storeIndex].Role, actual[storeIndex].Role);
            var expectedItems = expected[storeIndex].Items ?? [];
            var actualItems = actual[storeIndex].Items ?? [];
            Assert.Equal(expectedItems.Count, actualItems.Count);
            for (var itemIndex = 0; itemIndex < expectedItems.Count; itemIndex++)
                Assert.Equal(expectedItems[itemIndex], actualItems[itemIndex]);
        }
    }

    private static AuthoritativePlayerState CreatePlayer(
        uint gold,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        IReadOnlyList<int>? inventoryGrid = null,
        IReadOnlyList<BeltStoreItem>? belt = null)
    {
        var player = new AuthoritativePlayerState(
            gold,
            0,
            400,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            belt ?? [],
            0,
            0,
            640,
            1,
            0,
            0,
            inventory: inventory);
        player.ActiveStoreId = 4;
        return player;
    }

    private static OwnedStoreItem[] CreateInventoryBlockers()
        => Enumerable.Range(0, 40)
            .Select(index => new OwnedStoreItem(
                0,
                checked((uint)index),
                checked((uint)(1000 + index)),
                1,
                0,
                AuthoritativeItemState.Empty with {
                    ItemType = 0,
                    ItemIndex = 1000 + index,
                    StableItemId = checked((uint)(1000 + index)),
                    InventoryWidth = 1,
                    InventoryHeight = 1,
                }))
            .ToArray();

    private static BeltStoreItem[] CreateBeltBlockers(int count)
        => Enumerable.Range(0, count)
            .Select(index => new BeltStoreItem(
                checked((uint)index),
                checked((uint)(2000 + index)),
                AuthoritativeItemState.Empty with {
                    ItemType = 0,
                    ItemIndex = 2000 + index,
                    StableItemId = checked((uint)(2000 + index)),
                    InventoryWidth = 1,
                    InventoryHeight = 1,
                }))
            .ToArray();

    private static AuthoritativeItemCatalog CreateItems()
        => new([
            Definition(1, 1, "short_sword", 75, 0, DiabloItemActionKind.None),
            Definition(2, 2, "buckler", 25, 0, DiabloItemActionKind.None),
            Definition(2180, 77, "potion_healing_standard", 50, 3, DiabloItemActionKind.RestorePartialLife),
            Definition(2181, 78, "potion_full_healing", 150, 2, DiabloItemActionKind.RestoreFullLife),
        ]);

    private static AuthoritativeItemDefinition Definition(
        uint itemId,
        int nativeIndex,
        string symbol,
        int value,
        int miscId,
        DiabloItemActionKind action)
        => new(itemId, 0, value, value, 0, 0, 0, 0, 0, 1, 1, true) {
            StableSymbol = symbol,
            NativeItemIndex = nativeIndex,
            MiscId = miscId,
            ItemActionKind = action,
            Name = symbol,
            Usable = action != DiabloItemActionKind.None,
        };

    private static AuthoritativeCathedralNativeItemSidecars CreateSidecars()
    {
        var items = CreateItems();
        var indexes = new[] { 1, 2, 77, 78 };
        var footprints = "native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height\n"
            + string.Join("\n", indexes.Select(index => $"{index}\t{index + 12}\t28\t28\t1\t1"));
        var semantics = "native_item_index\tstable_item_symbol\tnative_cursor_id\tinventory_width\tinventory_height\tmisc_id\tspell_source_policy\titem_action_kind\n"
            + string.Join("\n", indexes.Select(index => {
                var item = items.Definitions.Single(definition => definition.NativeItemIndex == index);
                return $"{index}\t{item.StableSymbol}\t{index + 12}\t1\t1\t{item.MiscId}\tcatalog\t{item.ItemActionKind}";
            }));
        return AuthoritativeCathedralNativeItemSidecars.Load(
            TsvTable.Parse("native-rogue-l1-inventory.tsv", footprints),
            TsvTable.Parse("native_item_semantics.tsv", semantics));
    }
}
