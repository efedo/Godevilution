namespace Devilution.Diablo;

/** Native-compatible item-backed spell source classification. */
public enum DiabloItemSpellSourceKind
{
    None,
    ChargedItem,
    ConsumableItem,
}

/** Native-compatible classification of item-backed spell sources. */
public static class DiabloCastSourceRules
{
    public const uint LeftHandEquipmentSlot = 4;

    private const int ScrollMiscId = 21;
    private const int TargetedScrollMiscId = 22;
    private const int StaffMiscId = 23;
    private const int UniqueMiscId = 27;
    private const int FirstRuneMiscId = 47;
    private const int LastRuneMiscId = 51;

    /** Classifies an item for presentation without exposing native IDs. */
    public static DiabloItemSpellSourceKind ClassifyItem(
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind;
        if (TryGetDefinition(state, items, out var definition)
            && definition is not null
            && definition.SpellSourceKind is { } contentKind)
            return contentKind;
        var miscId = GetEffectiveMiscId(state, definition);
        var spellId = GetEffectiveSpellId(state, definition);
        var maximumCharges = GetEffectiveMaximumCharges(state, definition);
        if (GetChargedSpellId(miscId, spellId, maximumCharges) != 0)
            return DiabloItemSpellSourceKind.ChargedItem;
        if (GetConsumableSpellId(miscId, spellId, spells) != 0)
            return DiabloItemSpellSourceKind.ConsumableItem;
        return DiabloItemSpellSourceKind.None;
    }

    public static uint GetChargedSpellId(int miscId, int spellId, int maximumCharges)
    {
        return miscId is StaffMiscId or UniqueMiscId
            && spellId > 0
            && maximumCharges > 0
            ? checked((uint)spellId)
            : 0;
    }

    /** Resolves the spell identity using explicit content metadata when present. */
    public static uint GetItemSpellId(
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        TryGetDefinition(state, items, out var definition);
        var sourceKind = state.SpellSourceKind ?? definition?.SpellSourceKind;
        if (sourceKind is { } explicitKind)
            return explicitKind is DiabloItemSpellSourceKind.ChargedItem
                or DiabloItemSpellSourceKind.ConsumableItem
                ? GetEffectiveSpellId(state, definition) > 0
                    ? checked((uint)GetEffectiveSpellId(state, definition))
                    : 0
                : 0;
        var miscId = GetEffectiveMiscId(state, definition);
        var spellId = GetEffectiveSpellId(state, definition);
        var maximumCharges = GetEffectiveMaximumCharges(state, definition);
        var chargedSpellId = GetChargedSpellId(miscId, spellId, maximumCharges);
        return chargedSpellId != 0
            ? chargedSpellId
            : GetConsumableSpellId(miscId, spellId, spells);
    }

    public static uint GetConsumableSpellId(
        int miscId,
        int spellId,
        AuthoritativeSpellCatalog? spells)
    {
        if (miscId is ScrollMiscId or TargetedScrollMiscId)
            return spellId > 0 ? checked((uint)spellId) : 0;
        if (miscId is < FirstRuneMiscId or > LastRuneMiscId
            || spells is null
            || !spells.TryGetByConsumableMiscId(miscId, out var definition)) {
            return 0;
        }
        return definition.SpellId;
    }

    public static bool IsMatchingChargedItem(
        AuthoritativeItemState state,
        uint spellId,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        TryGetDefinition(state, items, out var definition);
        var sourceKind = state.SpellSourceKind ?? definition?.SpellSourceKind;
        if (sourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ChargedItem
                && GetEffectiveSpellId(state, definition) > 0
                && GetEffectiveMaximumCharges(state, definition) > 0
                && (uint)GetEffectiveSpellId(state, definition) == spellId;
        return GetChargedSpellId(
                GetEffectiveMiscId(state, definition),
                GetEffectiveSpellId(state, definition),
                GetEffectiveMaximumCharges(state, definition))
            == spellId;
    }

    public static bool IsMatchingConsumableItem(
        AuthoritativeItemState state,
        uint spellId,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        TryGetDefinition(state, items, out var definition);
        var sourceKind = state.SpellSourceKind ?? definition?.SpellSourceKind;
        if (sourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ConsumableItem
                && GetEffectiveSpellId(state, definition) > 0
                && (uint)GetEffectiveSpellId(state, definition) == spellId;
        return GetConsumableSpellId(
                GetEffectiveMiscId(state, definition),
                GetEffectiveSpellId(state, definition),
                spells)
            == spellId;
    }

    private static bool TryGetDefinition(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        out AuthoritativeItemDefinition? definition)
    {
        definition = null;
        return items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out definition);
    }

    private static int GetEffectiveMiscId(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition)
        => state.MiscId != 0 ? state.MiscId : definition?.MiscId ?? 0;

    private static int GetEffectiveSpellId(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition)
        => state.SpellId != 0 ? state.SpellId : definition?.SpellId ?? 0;

    private static int GetEffectiveMaximumCharges(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition)
        => state.MaxCharges != 0 ? state.MaxCharges : definition?.MaxCharges ?? 0;
}
