using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Selects a usable targetless item from one authoritative belt slot. */
public static class AuthoritativeBeltConsumableSelector
{
    private const int BeltSlotCount = 8;

    /** Returns the exact belt reference, or null when the slot is not usable. */
    public static PlayerItemReference? Select(PlayerSnapshot? player, int slot)
    {
        if (player is null
            || player.Life <= 0
            || player.CombatMode is PlayerCombatMode.NewLevel or PlayerCombatMode.Quit
            || slot is < 0 or >= BeltSlotCount) {
            return null;
        }

        var matchingItems = player.Belt.Where(item => item.Slot == (uint)slot).Take(2).ToArray();
        if (matchingItems.Length != 1
            || matchingItems[0].State is not { Deleted: false } state
            || state.StableItemId == 0
            || !AuthoritativeItemActionRules.IsTargetlessConsumable(state.ItemActionKind)) {
            return null;
        }

        return new PlayerItemReference {
            Location = PlayerItemLocation.Belt,
            Slot = (uint)slot,
            StableItemId = state.StableItemId,
        };
    }
}
