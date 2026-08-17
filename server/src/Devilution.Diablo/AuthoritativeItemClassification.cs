namespace Devilution.Diablo;

public enum DiabloEquipmentSlotKind
{
    Unknown,
    Hand,
    TwoHanded,
    BodyArmor,
    Helmet,
    Ring,
    Amulet,
}

/** Shared content-first equipment classification with native compatibility fallback. */
public static class AuthoritativeItemClassification
{
    /** Returns whether a classified item can occupy a native equipment slot. */
    public static bool IsCompatibleWithEquipmentSlot(
        DiabloEquipmentSlotKind kind,
        uint slot)
    {
        return kind switch {
            DiabloEquipmentSlotKind.Hand => slot is 4 or 5,
            DiabloEquipmentSlotKind.TwoHanded => slot == 4,
            DiabloEquipmentSlotKind.BodyArmor => slot == 6,
            DiabloEquipmentSlotKind.Helmet => slot == 0,
            DiabloEquipmentSlotKind.Ring => slot is 1 or 2,
            DiabloEquipmentSlotKind.Amulet => slot == 3,
            _ => false,
        };
    }

    public static bool IsWeapon(
        AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (HasWeaponSubtype(definition.GenerationTags))
            return true;
        if (HasExplicitEquipmentSubtype(definition.GenerationTags))
            return false;
        return definition.ItemClass == 1;
    }

    public static bool IsWeapon(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition))
                return IsWeapon(definition);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)) {
                if (HasWeaponSubtype(tags))
                    return true;
                if (HasExplicitEquipmentSubtype(tags))
                    return false;
            }
        }
        return state.ItemClass == 1;
    }

    public static bool IsStaff(
        AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.GenerationTags.Contains("Staff"))
            return true;
        if (HasExplicitEquipmentSubtype(definition.GenerationTags))
            return false;
        return definition.ItemType == 10;
    }

    public static bool IsStaff(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition))
                return IsStaff(definition);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)) {
                if (tags.Contains("Staff"))
                    return true;
                if (HasExplicitEquipmentSubtype(tags))
                    return false;
            }
        }
        return state.ItemType == 10;
    }

    public static bool IsShield(AuthoritativeItemDefinition definition)
        => HasSubtype(definition, "Shield", 5);

    public static bool IsShield(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasSubtype(state, items, "Shield", 5);

    public static bool IsSword(AuthoritativeItemDefinition definition)
        => HasSubtype(definition, "Sword", 1);

    public static bool IsSword(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasSubtype(state, items, "Sword", 1);

    public static bool IsAxe(AuthoritativeItemDefinition definition)
        => HasSubtype(definition, "Axe", 2);

    public static bool IsAxe(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasSubtype(state, items, "Axe", 2);

    public static bool IsMace(AuthoritativeItemDefinition definition)
        => HasSubtype(definition, "Mace", 4);

    public static bool IsMace(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasSubtype(state, items, "Mace", 4);

    public static bool IsBow(AuthoritativeItemDefinition definition)
        => HasSubtype(definition, "Bow", 3);

    public static bool IsBow(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasSubtype(state, items, "Bow", 3);

    public static bool IsGold(AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.GenerationTags.Contains("Gold")
            || definition.GenerationTags.Contains("Currency");
    }

    public static bool IsGold(
        AuthoritativeItemState state,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (items is not null
            && items.TryGetByState(state, out var definition)
            && IsGold(definition)) {
            return true;
        }
        return state.ItemType == rules.DeathGoldItemType
            && state.ItemIndex == rules.DeathGoldItemIndex;
    }

    public static bool IsBelt(AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.GenerationTags.Contains("Belt")
            || (definition.EquipLocation == 7 && definition.ItemClass == 3);
    }

    public static bool IsBelt(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition))
                return IsBelt(definition);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)
                && tags.Contains("Belt"))
                return true;
        }
        return state.EquipLocation == 7 && state.ItemClass == 3;
    }

    public static bool IsTwoHanded(AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.GenerationTags.Contains("TwoHanded")
            || definition.EquipLocation == 2;
    }

    public static bool IsTwoHanded(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition))
                return IsTwoHanded(definition);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)) {
                if (tags.Contains("TwoHanded"))
                    return true;
                if (HasExplicitEquipmentSubtype(tags))
                    return false;
            }
        }
        return state.EquipLocation == 2;
    }

    public static DiabloEquipmentSlotKind GetEquipmentSlotKind(
        AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.GenerationTags.Contains("TwoHanded"))
            return DiabloEquipmentSlotKind.TwoHanded;
        if (definition.GenerationTags.Contains("OneHanded"))
            return DiabloEquipmentSlotKind.Hand;
        if (definition.GenerationTags.Contains("BodyArmor"))
            return DiabloEquipmentSlotKind.BodyArmor;
        if (definition.GenerationTags.Contains("Helmet"))
            return DiabloEquipmentSlotKind.Helmet;
        if (definition.GenerationTags.Contains("Ring"))
            return DiabloEquipmentSlotKind.Ring;
        if (definition.GenerationTags.Contains("Amulet"))
            return DiabloEquipmentSlotKind.Amulet;
        return definition.EquipLocation switch {
            1 => DiabloEquipmentSlotKind.Hand,
            2 => DiabloEquipmentSlotKind.TwoHanded,
            3 => DiabloEquipmentSlotKind.BodyArmor,
            4 => DiabloEquipmentSlotKind.Helmet,
            5 => DiabloEquipmentSlotKind.Ring,
            6 => DiabloEquipmentSlotKind.Amulet,
            _ => DiabloEquipmentSlotKind.Unknown,
        };
    }

    public static DiabloEquipmentSlotKind GetEquipmentSlotKind(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition))
                return GetEquipmentSlotKind(definition);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags))
                return GetEquipmentSlotKind(tags, state.EquipLocation);
        }
        return state.EquipLocation switch {
            1 => DiabloEquipmentSlotKind.Hand,
            2 => DiabloEquipmentSlotKind.TwoHanded,
            3 => DiabloEquipmentSlotKind.BodyArmor,
            4 => DiabloEquipmentSlotKind.Helmet,
            5 => DiabloEquipmentSlotKind.Ring,
            6 => DiabloEquipmentSlotKind.Amulet,
            _ => DiabloEquipmentSlotKind.Unknown,
        };
    }

    private static DiabloEquipmentSlotKind GetEquipmentSlotKind(
        IReadOnlySet<string> tags,
        int fallbackEquipLocation)
    {
        if (tags.Contains("TwoHanded"))
            return DiabloEquipmentSlotKind.TwoHanded;
        if (tags.Contains("OneHanded"))
            return DiabloEquipmentSlotKind.Hand;
        if (tags.Contains("BodyArmor"))
            return DiabloEquipmentSlotKind.BodyArmor;
        if (tags.Contains("Helmet"))
            return DiabloEquipmentSlotKind.Helmet;
        if (tags.Contains("Ring"))
            return DiabloEquipmentSlotKind.Ring;
        if (tags.Contains("Amulet"))
            return DiabloEquipmentSlotKind.Amulet;
        return fallbackEquipLocation switch {
            1 => DiabloEquipmentSlotKind.Hand,
            2 => DiabloEquipmentSlotKind.TwoHanded,
            3 => DiabloEquipmentSlotKind.BodyArmor,
            4 => DiabloEquipmentSlotKind.Helmet,
            5 => DiabloEquipmentSlotKind.Ring,
            6 => DiabloEquipmentSlotKind.Amulet,
            _ => DiabloEquipmentSlotKind.Unknown,
        };
    }

    public static bool IsLightArmor(AuthoritativeItemDefinition definition)
        => HasArmorSubtype(definition, "LightArmor", 6);

    public static bool IsLightArmor(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasArmorSubtype(state, items, "LightArmor", 6);

    public static bool IsMediumArmor(AuthoritativeItemDefinition definition)
        => HasArmorSubtype(definition, "MediumArmor", 8);

    public static bool IsMediumArmor(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasArmorSubtype(state, items, "MediumArmor", 8);

    public static bool IsHeavyArmor(AuthoritativeItemDefinition definition)
        => HasArmorSubtype(definition, "HeavyArmor", 9);

    public static bool IsHeavyArmor(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items = null)
        => HasArmorSubtype(state, items, "HeavyArmor", 9);

    private static bool HasSubtype(
        AuthoritativeItemDefinition definition,
        string tag,
        int legacyItemType)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.GenerationTags.Contains(tag))
            return true;
        if (HasExplicitEquipmentSubtype(definition.GenerationTags))
            return false;
        return definition.ItemType == legacyItemType;
    }

    private static bool HasSubtype(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        string tag,
        int legacyItemType)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null
            && items.TryGetByState(state, out var definition))
            return HasSubtype(definition, tag, legacyItemType);
        return state.ItemType == legacyItemType;
    }

    private static bool HasArmorSubtype(
        AuthoritativeItemDefinition definition,
        string tag,
        int legacyItemType)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.GenerationTags.Contains(tag))
            return true;
        if (HasExplicitArmorSubtype(definition.GenerationTags))
            return false;
        return definition.ItemType == legacyItemType;
    }

    private static bool HasArmorSubtype(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        string tag,
        int legacyItemType)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (items is not null
            && items.TryGetByState(state, out var definition))
            return HasArmorSubtype(definition, tag, legacyItemType);
        return state.ItemType == legacyItemType;
    }

    private static bool HasWeaponSubtype(IReadOnlySet<string> tags)
        => tags.Contains("Weapon")
            || tags.Contains("Sword")
            || tags.Contains("Axe")
            || tags.Contains("Mace")
            || tags.Contains("Bow")
            || tags.Contains("Staff");

    private static bool HasExplicitEquipmentSubtype(IReadOnlySet<string> tags)
        => HasWeaponSubtype(tags)
            || tags.Contains("Shield");

    private static bool HasExplicitArmorSubtype(IReadOnlySet<string> tags)
        => tags.Contains("LightArmor")
            || tags.Contains("MediumArmor")
            || tags.Contains("HeavyArmor");
}
