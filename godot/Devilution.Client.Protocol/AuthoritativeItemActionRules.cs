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
            or ItemActionKind.IncreaseAllAttributes;
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
}
