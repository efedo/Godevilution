namespace Devilution.Diablo;

/** Native equipment-derived primary attributes shared by authoritative paths. */
public readonly record struct AuthoritativeEquipmentAttributeBonuses(
    int Strength,
    int Magic,
    int Dexterity,
    int Vitality)
{
    public static AuthoritativeEquipmentAttributeBonuses Zero { get; } = new(0, 0, 0, 0);
}

public static class AuthoritativeEquipmentAttributeRules
{
    private const int NativeMaximumAttribute = 750;

    /** Applies requirement-qualified, identified item attribute bonuses. */
    public static PlayerAttributesState Apply(
        PlayerAttributesState baseAttributes,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativeEquipmentAttributeBonuses previousBonuses = default)
    {
        ArgumentNullException.ThrowIfNull(baseAttributes);
        ArgumentNullException.ThrowIfNull(equipment);

        var bonuses = ResolveBonuses(equipment);

        return new PlayerAttributesState(
            baseAttributes.Strength with {
                Current = AddAndClamp(
                    baseAttributes.Strength.Current - previousBonuses.Strength,
                    bonuses.Strength),
            },
            baseAttributes.Magic with {
                Current = AddAndClamp(
                    baseAttributes.Magic.Current - previousBonuses.Magic,
                    bonuses.Magic),
            },
            baseAttributes.Dexterity with {
                Current = AddAndClamp(
                    baseAttributes.Dexterity.Current - previousBonuses.Dexterity,
                    bonuses.Dexterity),
            },
            baseAttributes.Vitality with {
                Current = AddAndClamp(
                    baseAttributes.Vitality.Current - previousBonuses.Vitality,
                    bonuses.Vitality),
            });
    }

    /** Resolves the currently applied native bonuses for snapshot bookkeeping. */
    public static AuthoritativeEquipmentAttributeBonuses ResolveBonuses(
        IEnumerable<EquippedStoreItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var strength = 0;
        var magic = 0;
        var dexterity = 0;
        var vitality = 0;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            strength = checked(strength + state.PlusStrength);
            magic = checked(magic + state.PlusMagic);
            dexterity = checked(dexterity + state.PlusDexterity);
            vitality = checked(vitality + state.PlusVitality);
        }
        return new(strength, magic, dexterity, vitality);
    }

    private static int AddAndClamp(int baseValue, int bonus)
    {
        return Math.Clamp(checked(baseValue + bonus), 0, NativeMaximumAttribute);
    }
}
