namespace Devilution.Diablo;

/** Native usable equipment modifiers applied to incoming physical player damage. */
public static class AuthoritativeEquipmentIncomingDamageRules
{
    /**
     * Applies the native `_pIGetHit` adjustment to an already-resolved hit.
     * The authoritative monster path uses the simulation's current damage unit;
     * a successful hit remains at least the configured minimum damage.
     */
    public static int ApplyGetHit(
        int incomingDamage,
        int minimumDamage,
        IEnumerable<EquippedStoreItem> equipment)
    {
        if (incomingDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(incomingDamage));
        if (minimumDamage <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamage));
        ArgumentNullException.ThrowIfNull(equipment);
        if (incomingDamage == 0)
            return 0;

        var getHit = 0;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            getHit = checked(getHit + state.PlusGetHit);
        }
        return Math.Max(minimumDamage, checked(incomingDamage + getHit));
    }
}
