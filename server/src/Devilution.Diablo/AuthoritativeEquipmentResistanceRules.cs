namespace Devilution.Diablo;

/** Native player resistance calculation from usable equipped item state. */
public static class AuthoritativeEquipmentResistanceRules
{
    /** Native ItemSpecialEffect::ZeroResistance flag. */
    public const uint ZeroResistanceFlag = 1U << 31;

    /**
     * Calculates one elemental resistance after item bonuses and class bonuses.
     * Native ZeroResistance resets the accumulated value after natural resistance
     * is applied, then the result is clamped to the configured maximum (75 by
     * default for the native compatibility path).
     */
    public static int Calculate(
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativeDamageType damageType,
        int naturalResistanceBonus = 0,
        int maximumResistance = 75)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (!Enum.IsDefined(damageType))
            throw new ArgumentOutOfRangeException(nameof(damageType));
        if (naturalResistanceBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(naturalResistanceBonus));
        if (maximumResistance is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maximumResistance));

        var resistance = naturalResistanceBonus;
        var zeroResistance = false;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;

            resistance = checked(resistance + damageType switch {
                AuthoritativeDamageType.Fire => state.PlusFireResistance,
                AuthoritativeDamageType.Lightning => state.PlusLightningResistance,
                AuthoritativeDamageType.Magic
                    or AuthoritativeDamageType.Acid => state.PlusMagicResistance,
                _ => 0,
            });
            zeroResistance |= (state.Flags & ZeroResistanceFlag) != 0;
        }

        return zeroResistance ? 0 : Math.Clamp(resistance, 0, maximumResistance);
    }
}
