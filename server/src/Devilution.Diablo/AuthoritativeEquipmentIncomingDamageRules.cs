using Devilution.Engine;

namespace Devilution.Diablo;

/** Native usable equipment modifiers applied to incoming physical player damage. */
public static class AuthoritativeEquipmentIncomingDamageRules
{
    /** Native ItemSpecialEffect::HalfTrapDamage flag. */
    public const uint HalfTrapDamageFlag = 1U << 28;

    /** Native ItemSpecialEffect::DrainLife flag. */
    public const uint DrainLifeFlag = 1U << 6;

    /** Native ItemSpecialEffect::Thorns flag. */
    public const uint ThornsFlag = 1U << 26;

    /** Native Hellfire ItemSpecialEffectHf::Devastation flag. */
    public const int DevastationFlag = 1 << 0;

    /** Native Hellfire ItemSpecialEffectHf::Decay flag. */
    public const int DecayFlag = 1 << 1;

    /** Native Hellfire ItemSpecialEffectHf::Jesters flag. */
    public const int JestersFlag = 1 << 3;

    /** Native Hellfire ItemSpecialEffectHf::Peril flag. */
    public const int PerilFlag = 1 << 2;

    /** Native Hellfire ItemSpecialEffectHf::Doppelganger flag. */
    public const int DoppelgangerFlag = 1 << 4;

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

        return Math.Max(
            minimumDamage,
            checked(incomingDamage + ResolveGetHitBonus(equipment)));
    }

    /** Applies native `_pIGetHit` in the Q6 damage unit used by MonsterAttackPlayer. */
    public static int ApplyGetHitQ6(
        int incomingDamageQ6,
        int minimumDamageQ6,
        IEnumerable<EquippedStoreItem> equipment)
    {
        if (incomingDamageQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(incomingDamageQ6));
        if (minimumDamageQ6 <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamageQ6));
        ArgumentNullException.ThrowIfNull(equipment);
        if (incomingDamageQ6 == 0)
            return 0;

        return Math.Max(
            minimumDamageQ6,
            checked(incomingDamageQ6
                + DeterministicFixedPointQ6.FromWhole(ResolveGetHitBonus(equipment))));
    }

    /** Resolves the native `_pIGetHit` total from eligible equipped items. */
    public static int ResolveGetHitBonus(IEnumerable<EquippedStoreItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var getHit = 0;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            getHit = checked(getHit + state.PlusGetHit);
        }
        return getHit;
    }

    /** Applies native Hellfire armor bonuses against the target monster class. */
    public static int ApplyHellfireMonsterClassArmor(
        int armorClass,
        bool targetDemon,
        bool targetUndead,
        IEnumerable<EquippedStoreItem> equipment,
        int againstDemonsArmorBonus = AgainstDemonsArmorBonus,
        int againstUndeadArmorBonus = AgainstUndeadArmorBonus)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (againstDemonsArmorBonus < 0 || againstUndeadArmorBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(againstDemonsArmorBonus));
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
            result = checked(result + againstDemonsArmorBonus);
        if (targetUndead && againstUndead)
            result = checked(result + againstUndeadArmorBonus);
        return result;
    }
}
