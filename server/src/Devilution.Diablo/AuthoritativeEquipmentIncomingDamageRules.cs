namespace Devilution.Diablo;

/** Native usable equipment modifiers applied to incoming physical player damage. */
public static class AuthoritativeEquipmentIncomingDamageRules
{
    /** Native Hellfire ItemSpecialEffectHf::Devastation flag. */
    public const int DevastationFlag = 1 << 0;

    /** Native Hellfire ItemSpecialEffectHf::ACAgainstDemons flag. */
    public const int AgainstDemonsFlag = 1 << 5;

    /** Native Hellfire ItemSpecialEffectHf::ACAgainstUndead flag. */
    public const int AgainstUndeadFlag = 1 << 6;

    /** Native armor bonus from ACAgainstDemons. */
    public const int AgainstDemonsArmorBonus = 40;

    /** Native armor bonus from ACAgainstUndead. */
    public const int AgainstUndeadArmorBonus = 20;

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

    /** Applies native Hellfire armor bonuses against the target monster class. */
    public static int ApplyHellfireMonsterClassArmor(
        int armorClass,
        bool targetDemon,
        bool targetUndead,
        IEnumerable<EquippedStoreItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var againstDemons = false;
        var againstUndead = false;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            againstDemons |= (state.HellfireDamageArmorFlags & AgainstDemonsFlag) != 0;
            againstUndead |= (state.HellfireDamageArmorFlags & AgainstUndeadFlag) != 0;
        }

        var result = armorClass;
        if (targetDemon && againstDemons)
            result = checked(result + AgainstDemonsArmorBonus);
        if (targetUndead && againstUndead)
            result = checked(result + AgainstUndeadArmorBonus);
        return result;
    }
}
