using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Acceptance purchases use Pepin's published stock, wallet and actual carrying space. */
internal static class JourneyHealingSupplies
{
    internal const int Target = 16;
    internal readonly record struct Purchase(int Index, uint Price, string Limit) { internal bool Buy => Index >= 0; }
    internal readonly record struct RestockAttempt(int Supply, uint Gold, bool CouldBuy);
    internal static bool NeedsTownDeparturePreparation(PlayerSnapshot player, int supply)
        => player.LevelId == 0 && (player.Life < player.LifeMaximum || supply < Target);

    internal static bool NeedsFloorResupply(PlayerSnapshot player, int supply, uint price, bool beforeDeepDescent = false)
        => player.LevelId != 0 && supply < (beforeDeepDescent ? Target : player.LevelId >= 5 ? 12 : 6)
            && price > 0 && player.Gold >= price && CanCarry(player);

    /** Returning through the cleared floors may call the same departure checks; never nest resupply trips. */
    internal sealed class ResupplyTrip
    {
        internal bool Active { get; private set; }
        internal async Task RunAsync(Func<Task> visit)
        {
            if (Active) return;
            Active = true;
            try { await visit(); }
            finally { Active = false; }
        }
    }

    internal static RestockAttempt BeginRestock(PlayerSnapshot player, int supply, StoreSnapshot store)
        => new(supply, player.Gold, NextPurchase(player, supply, store).Buy);

    internal static void RequireRestocked(RestockAttempt before, PlayerSnapshot player, int supply, StoreSnapshot store, string limit)
    {
        if (supply < before.Supply || before.CouldBuy && supply <= before.Supply || NextPurchase(player, supply, store).Buy)
            throw new InvalidOperationException($"Pepin restock did not buy the affordable healing supply: "
                + $"potions={before.Supply}->{supply}, Gold={before.Gold}->{player.Gold}, limit={limit}, "
                + $"couldBuyBefore={before.CouldBuy}, canCarry={CanCarry(player)}.");
    }

    internal static bool CanCarry(PlayerSnapshot player)
        => Enumerable.Range(0, 8).Any(slot => player.Belt.All(item => item.Slot != slot))
            || player.Inventory.Count < 40 && InventoryLayout.Build(player.Inventory, player.InventoryGrid).Occupants.Any(item => item < 0);

    internal static Purchase NextPurchase(PlayerSnapshot player, int supply, StoreSnapshot store)
    {
        if (supply >= Target) return new(-1, 0, "target reached");
        var rows = store.Items.OrderBy(item => item.StoreSlot).ToArray();
        var candidates = rows.Select((item, index) => (Item: item, Index: index))
            .Where(row => row.Item.State.ItemActionKind is ItemActionKind.RestorePartialLife or ItemActionKind.RestoreFullLife)
            .OrderBy(row => row.Item.Price).ThenBy(row => row.Item.StoreSlot).ToArray();
        if (candidates.Length == 0) return new(-1, 0, "no healing stock");
        var potion = candidates[0];
        if (player.Gold < potion.Item.Price) return new(-1, potion.Item.Price, "insufficient Gold");
        if (player.HeldItem is not null) return new(-1, potion.Item.Price, "occupied hand");
        if (!CanCarry(player)) return new(-1, potion.Item.Price, "belt and inventory full");
        return new(potion.Index, potion.Item.Price, string.Empty);
    }

    /** Called after the fresh-host baseline check and on every subsequent Town departure. */
    internal static async Task PrepareTownDepartureAsync(Func<(PlayerSnapshot Player, int Supply)> observe, Func<Task> visit)
    {
        var state = observe();
        if (NeedsTownDeparturePreparation(state.Player, state.Supply)) await visit();
    }
}
