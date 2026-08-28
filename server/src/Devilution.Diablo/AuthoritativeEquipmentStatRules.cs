using Devilution.Engine;

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

            var itemArmorClass = ResolveArmorClass(state, equippedItem.ItemSeed, items);
            armorClass = checked(armorClass + itemArmorClass);
            if (state.Magical != 0 && !state.Identified)
                continue;

            if (state.PlusArmorClass == 0)
                continue;
            var bonus = itemArmorClass * state.PlusArmorClass / 100;
            if (bonus == 0)
                bonus = Math.Sign(state.PlusArmorClass);
            armorClass = checked(armorClass + bonus);
        }

        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.IronSkin) == true)
            armorClass = checked(
                armorClass + (int)(characterLevel / (uint)playerClass.IronSkinLevelDivisor));

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

    private static int ResolveArmorClass(
        AuthoritativeItemState state,
        uint itemSeed,
        AuthoritativeItemCatalog? items)
    {
        if (state.ArmorClass > 0 || items is null)
            return state.ArmorClass;
        if (items.TryGetByState(state, out var definition)) {
            if (definition.MinArmorClass >= 0) {
                if (definition.MaxArmorClass == definition.MinArmorClass)
                    return definition.MinArmorClass;
                if (itemSeed == 0)
                    return state.ArmorClass;
                return checked(
                    definition.MinArmorClass
                    + new DeterministicLegacyRandom(itemSeed)
                        .Next(checked(definition.MaxArmorClass - definition.MinArmorClass + 1)));
            }
            if (definition.MinArmorClass < 0 && definition.ArmorClass > 0)
                return definition.ArmorClass;
        }
        if (items.TryGetArmorClassRangeByMiscId(
                state.MiscId,
                out var minimumArmorClass,
                out var maximumArmorClass)) {
            if (minimumArmorClass == maximumArmorClass)
                return minimumArmorClass;
            if (itemSeed == 0)
                return state.ArmorClass;
            return checked(
                minimumArmorClass
                + new DeterministicLegacyRandom(itemSeed)
                    .Next(checked(maximumArmorClass - minimumArmorClass + 1)));
        }
        return state.ArmorClass;
    }

    private static ArmorSubtype ResolveChestArmorSubtype(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        if (AuthoritativeItemClassification.IsLightArmor(state, items))
            return ArmorSubtype.Light;
        if (AuthoritativeItemClassification.IsMediumArmor(state, items))
            return ArmorSubtype.Medium;
        if (AuthoritativeItemClassification.IsHeavyArmor(state, items))
            return ArmorSubtype.Heavy;
        return ArmorSubtype.None;
    }

    private enum ArmorSubtype
    {
        None,
        Light,
        Medium,
        Heavy,
    }
}
