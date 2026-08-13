namespace Devilution.Diablo;

/** Native equipment-derived defensive statistics shared by authoritative paths. */
public static class AuthoritativeEquipmentStatRules
{
    /**
     * Calculates native player armor from base armor, dexterity, and equipped
     * item armor. Item armor is available when requirements are met; magical
     * percentage armor requires identification and truncates like native.
     * Class armor bonuses are applied after equipment-derived armor.
     */
    public static int CalculateArmorClass(
        int baseArmorClass,
        int dexterity,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativePlayerClassDefinition? playerClass = null,
        uint characterLevel = 1)
    {
        if (baseArmorClass < 0)
            throw new ArgumentOutOfRangeException(nameof(baseArmorClass));
        if (dexterity < 0)
            throw new ArgumentOutOfRangeException(nameof(dexterity));
        ArgumentNullException.ThrowIfNull(equipment);

        var armorClass = checked(baseArmorClass + dexterity / 5);
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag)
                continue;

            armorClass = checked(armorClass + state.ArmorClass);
            if (state.Magical != 0 && !state.Identified)
                continue;

            if (state.PlusArmorClass == 0)
                continue;
            var bonus = state.ArmorClass * state.PlusArmorClass / 100;
            if (bonus == 0)
                bonus = Math.Sign(state.PlusArmorClass);
            armorClass = checked(armorClass + bonus);
        }

        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.IronSkin) == true)
            armorClass = checked(armorClass + (int)(characterLevel / 4));

        return Math.Max(0, armorClass);
    }
}
