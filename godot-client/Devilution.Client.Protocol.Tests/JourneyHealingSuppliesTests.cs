using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyHealingSuppliesTests
{
    private static PlayerSnapshot Player(uint gold = 4582)
        => new() { EntityId = 1, Life = 11776, LifeMaximum = 11776, Gold = gold };
    private static StoreSnapshot Stock() => new() { StoreId = 4, Items = {
        new StoreItemSnapshot { StoreSlot = 0, Price = 50, State = new() { ItemActionKind = ItemActionKind.RestorePartialLife } },
        new StoreItemSnapshot { StoreSlot = 1, Price = 150, State = new() { ItemActionKind = ItemActionKind.RestoreFullLife } },
    } };

    [Theory]
    [InlineData(0)] [InlineData(8)] [InlineData(16)]
    public async Task FreshHostAndEveryTownDepartureFillAffordableSupplyThroughThePurchaseCallback(int carried)
    {
        var player = Player(); var supply = carried; var buys = 0;
        var stock = Stock(); var beforeGold = player.Gold;
        async Task Visit()
        {
            while (JourneyHealingSupplies.NextPurchase(player, supply, stock) is { Buy: true } purchase) {
                buys++; supply++; player.Gold -= purchase.Price;
                await Task.CompletedTask;
            }
        }
        // The exact cold baseline is inspected before invoking this departure path.
        Assert.Equal(carried, supply);
        await JourneyHealingSupplies.PrepareTownDepartureAsync(() => (player, supply), Visit);
        Assert.Equal(16, supply);
        Assert.Equal(16 - carried, buys);
        Assert.Equal(beforeGold - (uint)(buys * 50), player.Gold);
        await JourneyHealingSupplies.PrepareTownDepartureAsync(() => (player, supply), Visit);
        Assert.Equal(16 - carried, buys);
        supply = 0; // A later Town return must not retain a "prepared" latch from the first departure.
        await JourneyHealingSupplies.PrepareTownDepartureAsync(() => (player, supply), Visit);
        Assert.Equal(16, supply);
    }

    [Fact]
    public void RecordedWalletBuysSixteenAndLowSuppliesAreCheckedEvenOnSkippedFightFloors()
    {
        var player = Player();
        Assert.Equal(0, JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Index);
        Assert.Equal(50U, JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Price);
        foreach (var level in new[] { 1U, 4U, 5U, 8U }) {
            player.LevelId = level;
            Assert.True(JourneyHealingSupplies.NeedsFloorResupply(player, 0, 50));
            Assert.False(JourneyHealingSupplies.NeedsFloorResupply(player, 16, 50));
        }
        player.Gold = 49;
        Assert.False(JourneyHealingSupplies.NeedsFloorResupply(player, 0, 50));
        Assert.Equal("insufficient Gold", JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Limit);
    }

    [Fact]
    public void PurchaseLimitsNameGoldStockAndActualBeltInventorySpace()
    {
        var player = Player();
        for (uint slot = 0; slot < 8; slot++) player.Belt.Add(new BeltItemSnapshot { Slot = slot });
        for (var index = 0; index < 40; index++) player.Inventory.Add(new ItemSnapshot { State = new() { InventoryWidth = 1, InventoryHeight = 1 } });
        Assert.False(JourneyHealingSupplies.CanCarry(player));
        Assert.Equal("belt and inventory full", JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Limit);
        player.Inventory.RemoveAt(0);
        Assert.True(JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Buy);
        Assert.Equal("no healing stock", JourneyHealingSupplies.NextPurchase(player, 0, new()).Limit);
        Assert.Equal("target reached", JourneyHealingSupplies.NextPurchase(player, 16, Stock()).Limit);
        player.HeldItem = new();
        Assert.Equal("occupied hand", JourneyHealingSupplies.NextPurchase(player, 0, Stock()).Limit);
    }

    [Theory]
    [InlineData(4U)] [InlineData(5U)] [InlineData(6U)] [InlineData(7U)]
    public void EveryDeepDescentRestocksEvenWhenTheFloorWorkReserveWasEnough(uint level)
    {
        var player = Player(); player.LevelId = level;
        Assert.False(JourneyHealingSupplies.NeedsFloorResupply(player, 12, 50));
        Assert.True(JourneyHealingSupplies.NeedsFloorResupply(player, 12, 50, beforeDeepDescent: true));
        Assert.False(JourneyHealingSupplies.NeedsFloorResupply(player, 16, 50, beforeDeepDescent: true));
    }

    [Fact]
    public async Task ReturningThroughTheFloorsCannotNestAnotherRestockTrip()
    {
        var player = Player(); player.LevelId = 4; var supply = 8; var visits = 0; var bought = 0;
        var trip = new JourneyHealingSupplies.ResupplyTrip();
        Assert.True(JourneyHealingSupplies.NeedsFloorResupply(player, supply, 50, beforeDeepDescent: true));
        await trip.RunAsync(async () => {
            visits++; player.LevelId = 0;
            await JourneyHealingSupplies.PrepareTownDepartureAsync(() => (player, supply), () => {
                var attempt = JourneyHealingSupplies.BeginRestock(player, supply, Stock());
                while (JourneyHealingSupplies.NextPurchase(player, supply, Stock()) is { Buy: true } purchase) {
                    supply++; bought++; player.Gold -= purchase.Price;
                }
                JourneyHealingSupplies.RequireRestocked(attempt, player, supply, Stock(), "target reached");
                return Task.CompletedTask;
            });
            player.LevelId = 4;
            await trip.RunAsync(() => throw new InvalidOperationException("Returning through L5 must not recursively restock."));
        });
        Assert.Equal(1, visits); Assert.Equal(8, bought); Assert.Equal(16, supply);
        Assert.Equal(4182U, player.Gold); Assert.Equal(4U, player.LevelId); Assert.False(trip.Active);
        await Assert.ThrowsAsync<InvalidOperationException>(() => trip.RunAsync(() => throw new InvalidOperationException("Shop refused.")));
        Assert.False(trip.Active);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RestockCannotSilentlyBuyNothingOrStopWhileGoldAndSpaceAllowMore(int bought)
    {
        var player = Player(); var attempt = JourneyHealingSupplies.BeginRestock(player, 0, Stock());
        player.Gold -= (uint)bought * 50;
        var error = Assert.Throws<InvalidOperationException>(() =>
            JourneyHealingSupplies.RequireRestocked(attempt, player, bought, Stock(), "target reached"));
        Assert.Contains("affordable healing supply", error.Message);
        Assert.Contains($"potions=0->{bought}", error.Message);
        Assert.Contains("Gold=4582", error.Message);
    }

    [Fact]
    public void AlreadyFullAndGenuineWalletLimitsMayBuyNothingOrOnlyPartOfTheTarget()
    {
        var full = Player();
        JourneyHealingSupplies.RequireRestocked(JourneyHealingSupplies.BeginRestock(full, 16, Stock()), full, 16, Stock(), "target reached");
        var poor = Player(49);
        JourneyHealingSupplies.RequireRestocked(JourneyHealingSupplies.BeginRestock(poor, 0, Stock()), poor, 0, Stock(), "insufficient Gold");
        var limited = Player(100); var attempt = JourneyHealingSupplies.BeginRestock(limited, 0, Stock());
        limited.Gold = 0;
        JourneyHealingSupplies.RequireRestocked(attempt, limited, 2, Stock(), "insufficient Gold");
    }
}
