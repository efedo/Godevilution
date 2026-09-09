namespace Devilution.Diablo;

/** Native equipped-item and class modifiers used by initialized physical attacks. */
public static class AuthoritativeEquipmentDamageRules
{
    public static AuthoritativePhysicalDamageProfile Calculate(
        uint characterLevel,
        PlayerAttributesState attributes,
        IEnumerable<EquippedStoreItem> equipment,
        AuthoritativePlayerClassDefinition playerClass,
        AuthoritativeItemCatalog? items = null,
        int barbarianWeaponStrengthDamageDivisor = 75,
        int rogueStrengthDexterityDamageDivisor = 200,
        int monkStaffUnarmedDamageDivisor = 150,
        int monkWeaponDamageDivisor = 300,
        int bardSwordStrengthDexterityDamageDivisor = 150,
        int bardBowStrengthDexterityDamageDivisor = 250,
        int bardDefaultStrengthDamageDivisor = 100,
        int standardStrengthDamageDivisor = 100,
        int barbarianBowStrengthDamageDivisor = 300,
        int barbarianDefaultStrengthDamageDivisor = 100,
        int barbarianVitalityDamageDivisor = 100)
    {
        if (characterLevel == 0)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(playerClass);
        if (barbarianWeaponStrengthDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Barbarian weapon damage tuning is invalid.");
        if (rogueStrengthDexterityDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Rogue weapon damage tuning is invalid.");
        if (monkStaffUnarmedDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Monk staff/unarmed damage tuning is invalid.");
        if (monkWeaponDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Monk weapon damage tuning is invalid.");
        if (bardSwordStrengthDexterityDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Bard sword damage tuning is invalid.");
        if (bardBowStrengthDexterityDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Bard bow damage tuning is invalid.");
        if (bardDefaultStrengthDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Bard default damage tuning is invalid.");
        if (standardStrengthDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Standard damage tuning is invalid.");
        if (barbarianBowStrengthDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Barbarian bow damage tuning is invalid.");
        if (barbarianDefaultStrengthDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Barbarian default damage tuning is invalid.");
        if (barbarianVitalityDamageDivisor is < 1 or > 10_000)
            throw new InvalidDataException("Barbarian vitality damage tuning is invalid.");

        var usableEquipment = equipment
            .Where(item => item is not null && item.State.StatFlag)
            .ToArray();
        var minimumDamage = 0;
        var maximumDamage = 0;
        foreach (var equippedItem in usableEquipment) {
            var damage = ResolveBaseDamage(equippedItem.State, items);
            minimumDamage = checked(minimumDamage + damage.MinimumDamage);
            maximumDamage = checked(maximumDamage + damage.MaximumDamage);
        }

        var handItems = usableEquipment
            .Where(item => item.Slot is 4 or 5)
            .Select(item => item.State)
            .ToArray();
        var hasShield = handItems.Any(item => AuthoritativeItemClassification.IsShield(item, items));
        var hasStaff = handItems.Any(item => AuthoritativeItemClassification.IsStaff(item, items));
        var hasBow = handItems.Any(item => AuthoritativeItemClassification.IsBow(item, items));
        var hasSword = handItems.Any(item => AuthoritativeItemClassification.IsSword(item, items));
        var hasAxe = handItems.Any(item => AuthoritativeItemClassification.IsAxe(item, items));
        var hasMace = handItems.Any(item => AuthoritativeItemClassification.IsMace(item, items));
        var hasUnclassifiedHandItem = !hasShield
            && !hasStaff
            && !hasBow
            && !hasSword
            && !hasAxe
            && !hasMace;

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
            AuthoritativePhysicalDamageFormula.Rogue =>
                strengthDexterityModifier / rogueStrengthDexterityDamageDivisor,
            AuthoritativePhysicalDamageFormula.Monk => hasStaff || hasUnclassifiedHandItem
                ? strengthDexterityModifier / monkStaffUnarmedDamageDivisor
                : strengthDexterityModifier / monkWeaponDamageDivisor,
            AuthoritativePhysicalDamageFormula.Bard => hasSword
                ? strengthDexterityModifier / bardSwordStrengthDexterityDamageDivisor
                : hasBow
                    ? strengthDexterityModifier / bardBowStrengthDexterityDamageDivisor
                    : strengthModifier / bardDefaultStrengthDamageDivisor,
            AuthoritativePhysicalDamageFormula.Barbarian => CalculateBarbarianModifier(
                level,
                strengthModifier,
                vitality,
                hasAxe,
                hasMace,
                hasBow,
                hasShield,
                hasStaff,
                barbarianWeaponStrengthDamageDivisor,
                barbarianBowStrengthDamageDivisor,
                barbarianDefaultStrengthDamageDivisor,
                barbarianVitalityDamageDivisor),
            _ => strengthModifier / standardStrengthDamageDivisor,
        };

        return new AuthoritativePhysicalDamageProfile(
            minimumDamage,
            maximumDamage,
            damageModifier);
    }

    private static (int MinimumDamage, int MaximumDamage) ResolveBaseDamage(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        if (state.MaxDamage > 0 || items is null)
            return (state.MinDamage, state.MaxDamage);
        if (items.TryGetByState(state, out var definition)
            && definition.MaxDamage > 0)
            return (definition.MinDamage, definition.MaxDamage);
        if (items.TryGetBaseDamageByMiscId(
                state.MiscId,
                out var minimumDamage,
                out var maximumDamage))
            return (minimumDamage, maximumDamage);
        return (state.MinDamage, state.MaxDamage);
    }

    private static int CalculateBarbarianModifier(
        int level,
        int strengthModifier,
        int vitality,
        bool hasAxe,
        bool hasMace,
        bool hasBow,
        bool hasShield,
        bool hasStaff,
        int barbarianWeaponStrengthDamageDivisor,
        int barbarianBowStrengthDamageDivisor,
        int barbarianDefaultStrengthDamageDivisor,
        int barbarianVitalityDamageDivisor)
    {
        var damageModifier = hasAxe || hasMace
            ? strengthModifier / barbarianWeaponStrengthDamageDivisor
            : hasBow
                ? strengthModifier / barbarianBowStrengthDamageDivisor
                : strengthModifier / barbarianDefaultStrengthDamageDivisor;
        if (!hasShield && !hasStaff && !hasBow)
            damageModifier = checked(damageModifier + level * vitality / barbarianVitalityDamageDivisor);
        return damageModifier;
    }
}

/** Native physical damage range and class-specific flat damage contribution. */
public readonly record struct AuthoritativePhysicalDamageProfile(
    int MinimumDamage,
    int MaximumDamage,
    int DamageModifier);
