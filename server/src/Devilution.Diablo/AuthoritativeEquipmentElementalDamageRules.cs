namespace Devilution.Diablo;

/** Native elemental damage channels supplied by usable equipped items. */
public static class AuthoritativeEquipmentElementalDamageRules
{
    /** Native ItemSpecialEffect::FireDamage flag. */
    public const uint FireDamageFlag = 1U << 4;

    /** Native ItemSpecialEffect::LightningDamage flag. */
    public const uint LightningDamageFlag = 1U << 5;

    /** Native ItemSpecialEffect::FireArrows flag. */
    public const uint FireArrowsFlag = 1U << 3;

    /** Native ItemSpecialEffect::LightningArrows flag. */
    public const uint LightningArrowsFlag = 1U << 25;

    /** Native post-hit elemental damage in whole simulation points. */
    public readonly record struct ElementalDamage(int Fire, int Lightning);

    /**
     * Resolves the native inclusive elemental rolls from eligible equipment.
     * The random callback receives the exclusive upper bound used by
     * GenerateRnd and is invoked only for an active damage channel.
     */
    public static ElementalDamage Resolve(
        IEnumerable<EquippedStoreItem> equipment,
        Func<int, int> generateRnd)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(generateRnd);

        var fireMinimum = 0;
        var fireMaximum = 0;
        var lightningMinimum = 0;
        var lightningMaximum = 0;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;

            if ((state.Flags & FireDamageFlag) != 0) {
                fireMinimum = checked(fireMinimum + Math.Max(0, state.FireMinDamage));
                fireMaximum = checked(fireMaximum + Math.Max(0, state.FireMaxDamage));
            }
            if ((state.Flags & LightningDamageFlag) != 0) {
                lightningMinimum = checked(
                    lightningMinimum + Math.Max(0, state.LightningMinDamage));
                lightningMaximum = checked(
                    lightningMaximum + Math.Max(0, state.LightningMaxDamage));
            }
        }

        return new ElementalDamage(
            Roll(fireMinimum, fireMaximum, generateRnd),
            Roll(lightningMinimum, lightningMaximum, generateRnd));
    }

    private static int Roll(int minimum, int maximum, Func<int, int> generateRnd)
    {
        if (maximum < minimum || maximum == 0)
            return 0;
        return checked(minimum + generateRnd(checked(maximum - minimum + 1)));
    }
}
