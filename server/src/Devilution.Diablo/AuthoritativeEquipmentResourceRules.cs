namespace Devilution.Diablo;

/** Native Hellfire resource conversions applied to a newly equipped player. */
public static class AuthoritativeEquipmentResourceRules
{
    /**
     * Applies each equipped item's conversion against the unmodified base
     * resources. Native computes each item's portion independently in whole
     * points before restoring Q6 precision, so the order and truncation are
     * intentionally preserved here.
     */
    public static (int LifeQ6, int ManaQ6) ApplyHellfireConversions(
        int baseLifeQ6,
        int baseManaQ6,
        IEnumerable<EquippedStoreItem> equipment)
    {
        if (baseLifeQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(baseLifeQ6));
        if (baseManaQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(baseManaQ6));
        ArgumentNullException.ThrowIfNull(equipment);

        var lifeQ6 = baseLifeQ6;
        var manaQ6 = baseManaQ6;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
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
