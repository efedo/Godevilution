namespace Devilution.Diablo;

/** Native item resources, class-scaled attributes, and Hellfire conversions. */
public static class AuthoritativeEquipmentResourceRules
{
    /**
     * Applies usable, identified equipped resource bonuses and conversion
     * against the unmodified base resources. Native computes each conversion
     * portion independently in whole points before restoring Q6 precision and
     * applies class item-attribute factors after summing those item bonuses.
     */
    public static (int LifeQ6, int ManaQ6) ApplyHellfireConversions(
        int baseLifeQ6,
        int baseManaQ6,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativePlayerClassDefinition? playerClass = null)
    {
        if (baseLifeQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(baseLifeQ6));
        if (baseManaQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(baseManaQ6));
        ArgumentNullException.ThrowIfNull(equipment);

        var lifeQ6 = baseLifeQ6;
        var manaQ6 = baseManaQ6;
        var itemVitality = 0;
        var itemMagic = 0;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            lifeQ6 = checked(lifeQ6 + state.PlusHitPoints);
            manaQ6 = checked(manaQ6 + state.PlusMana);
            itemVitality = checked(itemVitality + state.PlusVitality);
            itemMagic = checked(itemMagic + state.PlusMagic);
            if (state.ManaToLifePercent != 0) {
                var portionQ6 = CalculatePortionQ6(baseManaQ6, state.ManaToLifePercent);
                manaQ6 = checked(manaQ6 - portionQ6);
                lifeQ6 = checked(lifeQ6 + portionQ6);
            }
            if (state.LifeToManaPercent != 0) {
                var portionQ6 = CalculatePortionQ6(baseLifeQ6, state.LifeToManaPercent);
                lifeQ6 = checked(lifeQ6 - portionQ6);
                manaQ6 = checked(manaQ6 + portionQ6);
            }
        }
        if (playerClass is not null) {
            var itemLifeFromVitality = checked(
                ((itemVitality * playerClass.ItemLifePerVitalityQ6) >> 6) << 6);
            var itemManaFromMagic = checked(
                ((itemMagic * playerClass.ItemManaPerMagicQ6) >> 6) << 6);
            lifeQ6 = checked(lifeQ6 + itemLifeFromVitality);
            manaQ6 = checked(manaQ6 + itemManaFromMagic);
        }
        return (lifeQ6, manaQ6);
    }

    private static int CalculatePortionQ6(int baseResourceQ6, int percent)
    {
        if (percent is < 0 or > 100)
            throw new InvalidDataException(
                $"Hellfire resource conversion percentage {percent} is outside 0..100.");
        return checked(((baseResourceQ6 >> 6) * percent / 100) << 6);
    }
}
