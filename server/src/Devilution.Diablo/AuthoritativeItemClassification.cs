namespace Devilution.Diablo;

/** Shared content-first equipment classification with native compatibility fallback. */
public static class AuthoritativeItemClassification
{
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
        if (items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out var definition))
            return IsWeapon(definition);
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
        if (items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out var definition))
            return IsStaff(definition);
        return state.ItemType == 10;
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
}
