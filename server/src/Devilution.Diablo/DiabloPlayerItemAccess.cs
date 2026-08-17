using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/** Shared module-owned access to exact inventory, belt, and equipment items. */
internal static class DiabloPlayerItemAccess
{
    public static bool TryGet(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        out OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(player);

        switch (reference.Location) {
        case DiabloPlayerItemLocation.Inventory when reference.Slot < player.Inventory.Count:
            item = player.Inventory[(int)reference.Slot];
            return MatchesStableIdentity(item.State, reference);
        case DiabloPlayerItemLocation.Belt:
            var beltItem = player.Belt.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (beltItem is not null) {
                item = ToOwnedItem(beltItem.Slot, beltItem.ItemSeed, beltItem.State);
                return MatchesStableIdentity(item.State, reference);
            }
            break;
        case DiabloPlayerItemLocation.Equipment:
            var equippedItem = player.Equipment.FirstOrDefault(
                candidate => candidate.Slot == reference.Slot);
            if (equippedItem is not null) {
                item = ToOwnedItem(
                    equippedItem.Slot,
                    equippedItem.ItemSeed,
                    equippedItem.State);
                return MatchesStableIdentity(item.State, reference);
            }
            break;
        }

        item = null!;
        return false;
    }

    private static bool MatchesStableIdentity(
        AuthoritativeItemState state,
        DiabloPlayerItemReference reference)
    {
        return reference.StableItemId == 0
            || state.StableItemId == reference.StableItemId;
    }

    public static void Replace(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(item);

        if (reference.Location == DiabloPlayerItemLocation.Inventory) {
            player.Inventory[(int)reference.Slot] = item;
            return;
        }

        if (reference.Location == DiabloPlayerItemLocation.Belt) {
            var index = player.Belt.FindIndex(candidate => candidate.Slot == reference.Slot);
            if (index >= 0)
                player.Belt[index] = new BeltStoreItem(reference.Slot, item.ItemSeed, item.State);
            return;
        }

        var equipmentIndex = player.Equipment.FindIndex(
            candidate => candidate.Slot == reference.Slot);
        if (equipmentIndex >= 0) {
            player.Equipment[equipmentIndex] = new EquippedStoreItem(
                reference.Slot,
                item.ItemSeed,
                item.State);
        }
    }

    public static void Remove(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (reference.Location == DiabloPlayerItemLocation.Inventory) {
            player.Inventory.RemoveAt((int)reference.Slot);
            InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, (int)reference.Slot);
        } else if (reference.Location == DiabloPlayerItemLocation.Belt) {
            player.Belt.RemoveAll(candidate => candidate.Slot == reference.Slot);
        } else {
            player.Equipment.RemoveAll(candidate => candidate.Slot == reference.Slot);
        }
    }

    private static OwnedStoreItem ToOwnedItem(
        uint slot,
        uint itemSeed,
        AuthoritativeItemState state)
    {
        var value = state.Identified && state.IdentifiedValue > 0
            ? state.IdentifiedValue
            : state.Value;
        return new OwnedStoreItem(
            0,
            slot,
            itemSeed,
            (uint)Math.Max(1, value),
            0,
            state);
    }
}
