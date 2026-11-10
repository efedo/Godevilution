using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class HudItemSelectionTests
{
    [Fact]
    public void ConfirmedInventoryConsumptionDoesNotSelectTheNextPotionOfTheSameKind()
    {
        var before = Player(PlayerItemLocation.Inventory);
        before.Inventory.Add(Item(2));
        var hud = Select(before, PlayerItemLocation.Inventory);
        var after = before.Clone();
        after.Inventory.RemoveAt(0);

        hud.ApplyHeadlessItemSelection(after);

        Assert.Null(hud.HeadlessSelectedItem);
        Assert.True(hud.HeadlessItemSelectionIndicesCleared);
        Assert.False(hud.HeadlessSelectedItem is { } selection && AuthoritativeItemDropRules.IsCarriedItem(after, selection));
    }

    [Fact]
    public void PendingOrRejectedUseKeepsTheItemUntilAnAdmittedSnapshotRemovesIt()
    {
        var before = Player(PlayerItemLocation.Inventory);
        var hud = Select(before, PlayerItemLocation.Inventory);
        hud.ApplyHeadlessItemSelection(before.Clone());
        Assert.NotNull(hud.HeadlessSelectedItem);
        var after = before.Clone();
        after.Inventory.Clear();
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void ConsumingAnIndistinguishableDuplicateStillReturnsTheCursorToHand()
    {
        var before = Player(PlayerItemLocation.Inventory);
        before.Inventory.Add(before.Inventory[0].Clone());
        var hud = Select(before, PlayerItemLocation.Inventory);
        var after = before.Clone();
        after.Inventory.RemoveAt(0);
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void QuickUseOfABeltPotionAlsoReturnsAnotherSelectedItemToTheHandCursor()
    {
        var before = Player(PlayerItemLocation.Inventory);
        before.Belt.Add(new BeltItemSnapshot { ItemSeed = 2, State = State() });
        var hud = Select(before, PlayerItemLocation.Inventory);
        var after = before.Clone();
        after.Belt.Clear();
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Theory]
    [InlineData(PlayerItemLocation.Inventory)] [InlineData(PlayerItemLocation.Belt)]
    [InlineData(PlayerItemLocation.Equipment)] [InlineData(PlayerItemLocation.Stash)]
    public void AReusedSlotNeverSelectsItsReplacement(PlayerItemLocation location)
    {
        var before = Player(location);
        var hud = Select(before, location);
        var after = Player(location, seed: 2);
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
        Assert.True(hud.HeadlessItemSelectionIndicesCleared);
    }

    [Fact]
    public void BeltRefillConsumptionAlsoClearsTheConsumedInventorySelection()
    {
        var before = Player(PlayerItemLocation.Inventory);
        before.Inventory.Add(Item(2));
        before.Belt.Add(new BeltItemSnapshot { ItemSeed = 3, State = State() });
        var hud = Select(before, PlayerItemLocation.Inventory);
        var after = before.Clone();
        after.Inventory.RemoveAt(0);
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Theory]
    [InlineData(PlayerItemLocation.Inventory)] [InlineData(PlayerItemLocation.Belt)]
    [InlineData(PlayerItemLocation.Equipment)] [InlineData(PlayerItemLocation.Stash)]
    public void TheSameItemSurvivesCloningAndServiceChanges(PlayerItemLocation location)
    {
        var before = Player(location);
        var hud = Select(before, location);
        var after = before.Clone();
        var state = location switch {
            PlayerItemLocation.Inventory => after.Inventory[0].State,
            PlayerItemLocation.Belt => after.Belt[0].State,
            PlayerItemLocation.Equipment => after.Equipment[0].State,
            _ => after.Stash[0].Item.State,
        };
        state.Identified = true;
        state.Durability++;
        state.Charges++;
        hud.ApplyHeadlessItemSelection(after);
        Assert.NotNull(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void ClosingTheStashClearsItsSelectionEvenWhileThePanelIsHidden()
    {
        var before = Player(PlayerItemLocation.Stash);
        var hud = Select(before, PlayerItemLocation.Stash);
        hud.CaptionVisible = false;
        var after = before.Clone();
        after.StashOpen = false;
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void StashLookupUsesPageAndCellWhenSeveralItemsShareTheirKind()
    {
        var player = Player(PlayerItemLocation.Stash);
        player.Stash.Insert(0, new StashItemSnapshot { Page = 0, Cell = 0, Item = Item(2) });
        Assert.Equal(1, HudPresentation.HeadlessFindSelectedStashItem(player, 42, 1, 0));
        Assert.Equal(-1, HudPresentation.HeadlessFindSelectedStashItem(player, 42, 2, 0));
    }

    [Fact]
    public void LegacyReferencesStillRetainTheSameSeedAndClearAReplacement()
    {
        var before = Player(PlayerItemLocation.Inventory);
        before.Inventory[0].State.StableItemId = 0;
        var hud = new HudPresentation();
        hud.SelectHeadlessItem(before, new PlayerItemReference { Location = PlayerItemLocation.Inventory });
        hud.ApplyHeadlessItemSelection(before.Clone());
        Assert.NotNull(hud.HeadlessSelectedItem);
        var after = before.Clone();
        after.Inventory[0].ItemSeed++;
        hud.ApplyHeadlessItemSelection(after);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void ReusingAnOilTargetSlotDoesNotRetargetTheOil()
    {
        var before = Player(PlayerItemLocation.Inventory);
        var hud = new HudPresentation();
        hud.SelectHeadlessOilTarget(before, Reference(PlayerItemLocation.Inventory));
        hud.ApplyHeadlessItemSelection(Player(PlayerItemLocation.Inventory, seed: 2));
        Assert.Null(hud.HeadlessSelectedOilTarget);
    }

    [Fact]
    public void ReplacedStoreStockClearsSelectionButAnUnchangedOfferRemainsSelected()
    {
        var store = Store();
        var hud = new HudPresentation();
        hud.SelectHeadlessStoreItem(store, 0);
        var after = store.Clone();
        after.Items[0].Price++;
        hud.ApplyHeadlessItemSelection(null, after);
        Assert.Equal(0U, hud.HeadlessSelectedStoreSlot);
        after = after.Clone();
        after.Items[0].ItemSeed++;
        hud.ApplyHeadlessItemSelection(null, after);
        Assert.Null(hud.HeadlessSelectedStoreSlot);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ClosingOrSwitchingStoresClearsTheirSelection(bool close)
    {
        var store = Store();
        var hud = new HudPresentation();
        hud.SelectHeadlessStoreItem(store, 0);
        var after = store.Clone();
        after.StoreId++;
        hud.ApplyHeadlessItemSelection(null, close ? null : after);
        Assert.Null(hud.HeadlessSelectedStoreSlot);
    }

    [Fact]
    public void LosingThePlayerClearsTheSelection()
    {
        var hud = Select(Player(PlayerItemLocation.Inventory), PlayerItemLocation.Inventory);
        hud.ApplyHeadlessItemSelection(null);
        Assert.Null(hud.HeadlessSelectedItem);
    }

    [Fact]
    public void UnchangedSelectionReconciliationDoesNotAllocatePerFrame()
    {
        var player = Player(PlayerItemLocation.Inventory);
        var hud = Select(player, PlayerItemLocation.Inventory);
        Action<int> frame = _ => hud.ApplyHeadlessItemSelection(player);
        HotPathAllocation.Warm(frame);
        var measured = HotPathAllocation.Measure(frame, 1000);
        Assert.InRange(measured.Bytes, 0, 256);
        Assert.NotNull(hud.HeadlessSelectedItem);
    }

    private static HudPresentation Select(PlayerSnapshot player, PlayerItemLocation location)
    {
        var hud = new HudPresentation();
        hud.SelectHeadlessItem(player, Reference(location));
        return hud;
    }

    private static PlayerItemReference Reference(PlayerItemLocation location)
        => new() { Location = location, Slot = 0, Page = location == PlayerItemLocation.Stash ? 1U : 0U, StableItemId = 42 };

    private static PlayerSnapshot Player(PlayerItemLocation location, uint seed = 1)
    {
        var player = new PlayerSnapshot { EntityId = 1, Life = 100, StashOpen = true };
        switch (location) {
        case PlayerItemLocation.Inventory: player.Inventory.Add(Item(seed)); break;
        case PlayerItemLocation.Belt: player.Belt.Add(new BeltItemSnapshot { ItemSeed = seed, State = State() }); break;
        case PlayerItemLocation.Equipment: player.Equipment.Add(new EquippedItemSnapshot { ItemSeed = seed, State = State() }); break;
        case PlayerItemLocation.Stash: player.Stash.Add(new StashItemSnapshot { Page = 1, Item = Item(seed) }); break;
        }
        return player;
    }

    private static ItemStateSnapshot State() => new() { StableItemId = 42, ItemIndex = 77, CreateInfo = 6,
        ItemActionKind = ItemActionKind.RestorePartialLife };
    private static ItemSnapshot Item(uint seed) => new() { ItemSeed = seed, State = State() };
    private static StoreSnapshot Store()
    {
        var store = new StoreSnapshot { StoreId = 4 };
        store.Items.Add(new StoreItemSnapshot { ItemSeed = 1, State = State() });
        return store;
    }
}
