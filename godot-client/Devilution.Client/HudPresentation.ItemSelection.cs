using Devilution.Protocol.V1;

namespace Devilution.Client;

public partial class HudPresentation
{
    private StoreSnapshot? selectionStore;

    /** legacy-native/game/players/inventory.cpp::UseInvItem keeps the hand cursor after consumption. */
    private void ReconcileItemSelections(PlayerSnapshot? player, StoreSnapshot? store)
    {
        if (selectedItemReference is { } selected
            && (ItemCount(currentPlayer) > ItemCount(player) || !SameSelectedItem(currentPlayer, player, selected)))
            ClearSelectedItem();
        if (selectedOilTarget is { } target && !SameSelectedItem(currentPlayer, player, target))
            selectedOilTarget = null;
        if (selectedStoreSlot is { } slot
            && (selectionStore?.StoreId != store?.StoreId
                || StoreItemIdentity(selectionStore, slot) is not { } identity
                || StoreItemIdentity(store, slot) != identity))
            selectedStoreSlot = null;
        selectionStore = store;
    }

    // Quick-use may consume a different belt/inventory item, including an indistinguishable duplicate.
    private static int ItemCount(PlayerSnapshot? player)
        => player is null ? 0 : player.Inventory.Count + player.Belt.Count + player.Equipment.Count + player.Stash.Count;

    private static bool SameSelectedItem(PlayerSnapshot? before, PlayerSnapshot? after, PlayerItemReference reference)
        => before is not null && after is not null && before.EntityId == after.EntityId
            && SelectedItemIdentity(before, reference) is { } identity
            && SelectedItemIdentity(after, reference) == identity;

    private static ItemSelectionIdentity? SelectedItemIdentity(PlayerSnapshot player, PlayerItemReference reference)
    {
        switch (reference.Location) {
        case PlayerItemLocation.Inventory:
            if (reference.Slot < player.Inventory.Count) {
                var item = player.Inventory[(int)reference.Slot];
                return SelectionIdentity(item.ItemSeed, item.State, reference.StableItemId);
            }
            break;
        case PlayerItemLocation.Belt:
            foreach (var item in player.Belt)
                if (item.Slot == reference.Slot)
                    return SelectionIdentity(item.ItemSeed, item.State, reference.StableItemId);
            break;
        case PlayerItemLocation.Equipment:
            foreach (var item in player.Equipment)
                if (item.Slot == reference.Slot)
                    return SelectionIdentity(item.ItemSeed, item.State, reference.StableItemId);
            break;
        case PlayerItemLocation.Stash:
            if (!player.StashOpen) break;
            foreach (var item in player.Stash)
                if (item.Page == reference.Page && item.Cell == reference.Slot && item.Item is { } stored)
                    return SelectionIdentity(stored.ItemSeed, stored.State, reference.StableItemId);
            break;
        }
        return null;
    }

    private static ItemSelectionIdentity? StoreItemIdentity(StoreSnapshot? store, uint slot)
    {
        if (store is not null)
            foreach (var item in store.Items)
                if (item.StoreSlot == slot)
                    return SelectionIdentity(item.ItemSeed, item.State);
        return null;
    }

    private static ItemSelectionIdentity? SelectionIdentity(uint seed, ItemStateSnapshot? state, uint stableItemId = 0)
        => state is { Deleted: false } && (stableItemId == 0 || stableItemId == state.StableItemId)
            ? new(seed, state.CreateInfo, state.ItemIndex, state.StableItemId) : null;

    /** StableItemId names a kind of item; several stash potions can share it. */
    private static int FindSelectedStashItemIndex(PlayerSnapshot player, uint? stableItemId, uint page, uint cell)
    {
        for (var index = 0; index < player.Stash.Count; index++)
            if (player.Stash[index].Page == page && player.Stash[index].Cell == cell
                && (stableItemId is null || player.Stash[index].Item?.State?.StableItemId == stableItemId))
                return index;
        return -1;
    }

    private readonly record struct ItemSelectionIdentity(uint Seed, uint CreateInfo, int ItemIndex, uint StableItemId);
}
