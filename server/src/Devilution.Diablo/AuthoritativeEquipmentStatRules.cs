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
        uint characterLevel = 1,
        AuthoritativeItemCatalog? items = null)
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

        if (playerClass?.MonkArmorLevelDivisor > 0) {
            var chest = equipment.FirstOrDefault(item => item.Slot == 3);
            if (chest?.State.StatFlag == true) {
                var state = chest.State;
                var level = checked((int)characterLevel);
                var armorSubtype = ResolveChestArmorSubtype(state, items);
                var numerator = armorSubtype switch {
                    ArmorSubtype.Medium when state.Magical == 2
                        => playerClass.MonkUniqueMediumArmorLevelMultiplier,
                    ArmorSubtype.Medium => playerClass.MonkMediumArmorLevelMultiplier,
                    ArmorSubtype.Heavy when state.Magical == 2
                        => playerClass.MonkUniqueHeavyArmorLevelMultiplier,
                    ArmorSubtype.Light => playerClass.MonkLightArmorLevelMultiplier,
                    _ => 0,
                };
                armorClass = checked(
                    armorClass
                    + level * numerator / playerClass.MonkArmorLevelDivisor);
            }
        }

        return Math.Max(0, armorClass);
    }

    private static ArmorSubtype ResolveChestArmorSubtype(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        if (items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out var definition)) {
            if (definition.GenerationTags.Contains("LightArmor"))
                return ArmorSubtype.Light;
            if (definition.GenerationTags.Contains("MediumArmor"))
                return ArmorSubtype.Medium;
            if (definition.GenerationTags.Contains("HeavyArmor"))
                return ArmorSubtype.Heavy;
        }

        return state.ItemType switch {
            6 => ArmorSubtype.Light,
            8 => ArmorSubtype.Medium,
            9 => ArmorSubtype.Heavy,
            _ => ArmorSubtype.None,
        };
    }

    private enum ArmorSubtype
    {
        None,
        Light,
        Medium,
        Heavy,
    }
}
