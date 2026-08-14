namespace Devilution.Diablo;

/** Native equipped-item and class modifiers used by initialized physical attacks. */
public static class AuthoritativeEquipmentDamageRules
{
    public static AuthoritativePhysicalDamageProfile Calculate(
        uint characterLevel,
        PlayerAttributesState attributes,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativePlayerClassDefinition playerClass,
        AuthoritativeItemCatalog? items = null)
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
        var hasShield = handItems.Any(item => HasItemTag(item, items, "Shield", 5));
        var hasStaff = handItems.Any(item => HasItemTag(item, items, "Staff", 10));
        var hasBow = handItems.Any(item => HasItemTag(item, items, "Bow", 3));
        var hasSword = handItems.Any(item => HasItemTag(item, items, "Sword", 1));
        var hasAxe = handItems.Any(item => HasItemTag(item, items, "Axe", 2));
        var hasMace = handItems.Any(item => HasItemTag(item, items, "Mace", 4));

        if (minimumDamage == 0 && maximumDamage == 0) {
            minimumDamage = 1;
            maximumDamage = hasShield ? 3 : 1;
            if (playerClass.PhysicalDamageFormula == AuthoritativePhysicalDamageFormula.Monk) {
                minimumDamage = Math.Max(
                    minimumDamage,
                    checked((int)characterLevel
                        / playerClass.MonkUnarmedMinimumDamageLevelDivisor));
                maximumDamage = Math.Max(
                    maximumDamage,
                    checked((int)characterLevel
                        / playerClass.MonkUnarmedMaximumDamageLevelDivisor));
            }
        }

        var level = checked((int)characterLevel);
        var strength = attributes.Strength.Current;
        var dexterity = attributes.Dexterity.Current;
        var vitality = attributes.Vitality.Current;
        var strengthModifier = checked(level * strength);
        var strengthDexterityModifier = checked(level * (strength + dexterity));
        var damageModifier = playerClass.PhysicalDamageFormula switch {
            AuthoritativePhysicalDamageFormula.Rogue => strengthDexterityModifier / 200,
            AuthoritativePhysicalDamageFormula.Monk => hasStaff || handItems.Length == 0
                ? strengthDexterityModifier / 150
                : strengthDexterityModifier / 300,
            AuthoritativePhysicalDamageFormula.Bard => hasSword
                ? strengthDexterityModifier / 150
                : hasBow
                    ? strengthDexterityModifier / 250
                    : strengthModifier / 100,
            AuthoritativePhysicalDamageFormula.Barbarian => CalculateBarbarianModifier(
                level,
                strengthModifier,
                vitality,
                hasAxe,
                hasMace,
                hasBow,
                hasShield,
                hasStaff),
            _ => strengthModifier / 100,
        };

        return new AuthoritativePhysicalDamageProfile(
            minimumDamage,
            maximumDamage,
            damageModifier);
    }

    private static bool HasItemTag(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        string tag,
        int legacyItemType)
    {
        if (items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out var definition)) {
            if (definition.GenerationTags.Contains(tag))
                return true;
            if (HasExplicitEquipmentSubtype(definition.GenerationTags))
                return false;
        }
        return state.ItemType == legacyItemType;
    }

    private static bool HasExplicitEquipmentSubtype(IReadOnlySet<string> tags)
        => tags.Contains("Sword")
            || tags.Contains("Axe")
            || tags.Contains("Mace")
            || tags.Contains("Bow")
            || tags.Contains("Shield")
            || tags.Contains("Staff");

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
