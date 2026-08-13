namespace Devilution.Diablo;

/** Native equipped-item and class modifiers used by initialized physical attacks. */
public static class AuthoritativeEquipmentDamageRules
{
    public static AuthoritativePhysicalDamageProfile Calculate(
        uint characterLevel,
        PlayerAttributesState attributes,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativePlayerClassDefinition playerClass)
    {
        if (characterLevel == 0)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(playerClass);

        var usableEquipment = equipment
            .Where(item => item is not null && item.State.StatFlag)
            .ToArray();
        var minimumDamage = 0;
        var maximumDamage = 0;
        foreach (var equippedItem in usableEquipment) {
            minimumDamage = checked(minimumDamage + equippedItem.State.MinDamage);
            maximumDamage = checked(maximumDamage + equippedItem.State.MaxDamage);
        }

        var handItems = usableEquipment
            .Where(item => item.Slot is 4 or 5)
            .Select(item => item.State)
            .ToArray();
        var hasShield = handItems.Any(item => item.ItemType == 5);
        var hasStaff = handItems.Any(item => item.ItemType == 10);
        var hasBow = handItems.Any(item => item.ItemType == 3);
        var hasSword = handItems.Any(item => item.ItemType == 1);
        var hasAxe = handItems.Any(item => item.ItemType == 2);
        var hasMace = handItems.Any(item => item.ItemType == 4);

        if (minimumDamage == 0 && maximumDamage == 0) {
            minimumDamage = 1;
            maximumDamage = hasShield ? 3 : 1;
            if (string.Equals(playerClass.Symbol, "monk", StringComparison.Ordinal)) {
                minimumDamage = Math.Max(minimumDamage, checked((int)characterLevel / 2));
                maximumDamage = Math.Max(maximumDamage, checked((int)characterLevel));
            }
        }

        var level = checked((int)characterLevel);
        var strength = attributes.Strength.Current;
        var dexterity = attributes.Dexterity.Current;
        var vitality = attributes.Vitality.Current;
        var strengthModifier = checked(level * strength);
        var strengthDexterityModifier = checked(level * (strength + dexterity));
        var damageModifier = string.Equals(playerClass.Symbol, "rogue", StringComparison.Ordinal)
            ? strengthDexterityModifier / 200
            : string.Equals(playerClass.Symbol, "monk", StringComparison.Ordinal)
                ? (hasStaff || handItems.Length == 0
                    ? strengthDexterityModifier / 150
                    : strengthDexterityModifier / 300)
                : string.Equals(playerClass.Symbol, "bard", StringComparison.Ordinal)
                    ? hasSword
                        ? strengthDexterityModifier / 150
                        : hasBow
                            ? strengthDexterityModifier / 250
                            : strengthModifier / 100
                    : string.Equals(playerClass.Symbol, "barbarian", StringComparison.Ordinal)
                        ? CalculateBarbarianModifier(
                            level,
                            strengthModifier,
                            vitality,
                            hasAxe,
                            hasMace,
                            hasBow,
                            hasShield,
                            hasStaff)
                        : strengthModifier / 100;

        return new AuthoritativePhysicalDamageProfile(
            minimumDamage,
            maximumDamage,
            damageModifier);
    }

    private static int CalculateBarbarianModifier(
        int level,
        int strengthModifier,
        int vitality,
        bool hasAxe,
        bool hasMace,
        bool hasBow,
        bool hasShield,
        bool hasStaff)
    {
        var damageModifier = hasAxe || hasMace
            ? strengthModifier / 75
            : hasBow
                ? strengthModifier / 300
                : strengthModifier / 100;
        if (!hasShield && !hasStaff && !hasBow)
            damageModifier = checked(damageModifier + level * vitality / 100);
        return damageModifier;
    }
}

/** Native physical damage range and class-specific flat damage contribution. */
public readonly record struct AuthoritativePhysicalDamageProfile(
    int MinimumDamage,
    int MaximumDamage,
    int DamageModifier);
