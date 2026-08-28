using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation-side admission for stable carried-item world drops. */
public static class AuthoritativeItemDropRules
{
    /** Returns true when a selected reference still names a carried item. */
    public static bool IsCarriedItem(
        PlayerSnapshot player,
        PlayerItemReference reference)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(reference);

        return reference.Location switch {
            PlayerItemLocation.Inventory => reference.Slot < player.Inventory.Count
                && Matches(player.Inventory[(int)reference.Slot].State, reference),
            PlayerItemLocation.Belt => player.Belt.Any(item =>
                item.Slot == reference.Slot
                && Matches(item.State, reference)),
            PlayerItemLocation.Equipment => player.Equipment.Any(item =>
                item.Slot == reference.Slot
                && Matches(item.State, reference)),
            _ => false,
        };
    }

    private static bool Matches(
        ItemStateSnapshot? state,
        PlayerItemReference reference)
    {
        return state is not null
            && (reference.StableItemId == 0 || state.StableItemId == reference.StableItemId);
    }
}
