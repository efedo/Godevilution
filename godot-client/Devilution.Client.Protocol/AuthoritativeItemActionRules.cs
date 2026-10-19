using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation admission for one-item server-owned consumable actions. */
public static class AuthoritativeItemActionRules
{
    /**
     * Returns true when the HUD can submit the item without a second target.
     * Oil actions intentionally remain target-driven and are handled by a
     * separate cursor flow.
     */
    public static bool IsTargetlessConsumable(ItemActionKind action)
    {
        return action is ItemActionKind.RestorePartialLife
            or ItemActionKind.RestoreFullLife
            or ItemActionKind.RestorePartialMana
            or ItemActionKind.RestoreFullMana
            or ItemActionKind.RestorePartialLifeAndMana
            or ItemActionKind.RestoreFullLifeAndMana
            or ItemActionKind.IncreaseStrength
            or ItemActionKind.IncreaseMagic
            or ItemActionKind.IncreaseDexterity
            or ItemActionKind.IncreaseVitality
            or ItemActionKind.IncreaseAllAttributes
            or ItemActionKind.OpenCathedralMap
            or ItemActionKind.ReadQuestNote;
    }

    /** Returns true when the action needs a second owned-item target. */
    public static bool IsTargetedConsumable(ItemActionKind action)
    {
        return action is ItemActionKind.ApplyBlacksmithOil
            or ItemActionKind.ApplyAccuracyOil
            or ItemActionKind.ApplySharpnessOil
            or ItemActionKind.ApplyFortitudeOil;
    }

    /** Compares the complete stable reference used by an oil target request. */
    public static bool AreSameReference(
        PlayerItemReference left,
        PlayerItemReference right)
    {
        return left.Location == right.Location
            && left.Slot == right.Slot
            && left.StableItemId == right.StableItemId;
    }

    /**
     * Returns true when a selected stable reference is still present and may
     * be submitted to an item service. Stash services require the projected
     * stash session to remain open; other locations use their normal snapshot
     * ownership rules.
     */
    public static bool IsServiceTarget(
        PlayerSnapshot player,
        PlayerItemReference reference,
        bool stashOpen)
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
            PlayerItemLocation.Stash => stashOpen && player.Stash.Any(item =>
                item.Page == reference.Page
                && item.Cell == reference.Slot
                && item.Item?.State is { } state
                && Matches(state, reference)),
            _ => false,
        };
    }

    private static bool Matches(ItemStateSnapshot? state, PlayerItemReference reference)
    {
        return state is not null
            && (reference.StableItemId == 0 || state.StableItemId == reference.StableItemId);
    }
}
