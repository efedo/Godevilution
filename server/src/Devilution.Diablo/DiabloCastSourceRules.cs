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
        var spellId = GetEffectiveSpellId(state, definition, items);
        var maximumCharges = GetEffectiveMaximumCharges(state, definition, items);
        if (GetChargedSpellId(miscId, spellId, maximumCharges, items) != 0)
            return DiabloItemSpellSourceKind.ChargedItem;
        if (GetConsumableSpellId(miscId, spellId, spells, items) != 0)
            return DiabloItemSpellSourceKind.ConsumableItem;
        return DiabloItemSpellSourceKind.None;
    }

    public static uint GetChargedSpellId(
        int miscId,
        int spellId,
        int maximumCharges,
        AuthoritativeItemCatalog? items = null)
    {
        if (items is not null
            && items.TryGetChargedItemMetadataByMiscId(
                miscId,
                out var contentMaximumCharges,
                out var contentSpellId)) {
            if (spellId <= 0)
                spellId = contentSpellId;
            if (maximumCharges <= 0)
                maximumCharges = contentMaximumCharges;
        }
        var isChargedSource = items is not null
            && items.TryGetSpellSourceKindByMiscId(
                miscId,
                out var contentKind)
            ? contentKind == DiabloItemSpellSourceKind.ChargedItem
            : miscId is StaffMiscId or UniqueMiscId;
        return isChargedSource
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
        var miscId = GetEffectiveMiscId(state, definition);
        var sourceKind = state.SpellSourceKind ?? definition?.SpellSourceKind;
        var spellId = GetEffectiveSpellId(state, definition, items, sourceKind);
        var maximumCharges = GetEffectiveMaximumCharges(state, definition, items);
        if (sourceKind is { } explicitKind)
            return explicitKind is DiabloItemSpellSourceKind.ChargedItem
                or DiabloItemSpellSourceKind.ConsumableItem
                ? spellId > 0
                    ? checked((uint)spellId)
                    : 0
                : 0;
        var chargedSpellId = GetChargedSpellId(miscId, spellId, maximumCharges, items);
        return chargedSpellId != 0
            ? chargedSpellId
            : GetConsumableSpellId(miscId, spellId, spells, items);
    }

    public static uint GetConsumableSpellId(
        int miscId,
        int spellId,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeItemCatalog? items = null)
    {
        if (spellId <= 0
            && items is not null
            && items.TryGetConsumableItemSpellIdByMiscId(miscId, out var contentSpellId))
            spellId = contentSpellId;
        var isConsumableSource = items is not null
            && items.TryGetSpellSourceKindByMiscId(
                miscId,
                out var contentKind)
            ? contentKind == DiabloItemSpellSourceKind.ConsumableItem
            : miscId is ScrollMiscId or TargetedScrollMiscId;
        if (isConsumableSource)
            return spellId > 0 ? checked((uint)spellId) : 0;
        if (items is not null
            && items.TryGetSpellSourceKindByMiscId(miscId, out _))
            return 0;
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
        var spellIdFromContent = GetEffectiveSpellId(state, definition, items, sourceKind);
        var maximumCharges = GetEffectiveMaximumCharges(state, definition, items);
        if (sourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ChargedItem
                && spellIdFromContent > 0
                && maximumCharges > 0
                && (uint)spellIdFromContent == spellId;
        return GetChargedSpellId(
                GetEffectiveMiscId(state, definition),
                spellIdFromContent,
                maximumCharges,
                items)
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
        if (definition?.Usable == false)
            return false;
        var sourceKind = state.SpellSourceKind ?? definition?.SpellSourceKind;
        if (sourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ConsumableItem
                && GetEffectiveSpellId(state, definition, items, explicitKind) > 0
                && (uint)GetEffectiveSpellId(state, definition, items, explicitKind) == spellId;
        return GetConsumableSpellId(
                GetEffectiveMiscId(state, definition),
                GetEffectiveSpellId(state, definition, items, sourceKind),
                spells,
                items)
            == spellId;
    }

    private static bool TryGetDefinition(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        out AuthoritativeItemDefinition? definition)
    {
        definition = null;
        return items is not null
            && items.TryGetByState(state, out definition);
    }

    private static int GetEffectiveMiscId(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition)
        => state.MiscId != 0 ? state.MiscId : definition?.MiscId ?? 0;

    private static int GetEffectiveSpellId(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition,
        AuthoritativeItemCatalog? items,
        DiabloItemSpellSourceKind? sourceKind = null)
    {
        if (state.SpellId != 0)
            return state.SpellId;
        if (definition?.SpellId is > 0)
            return definition.SpellId;
        if (items is not null) {
            var miscId = GetEffectiveMiscId(state, definition);
            if (sourceKind != DiabloItemSpellSourceKind.ConsumableItem
                && items.TryGetChargedItemMetadataByMiscId(miscId, out _, out var chargedSpellId))
                return chargedSpellId;
            if (sourceKind != DiabloItemSpellSourceKind.ChargedItem
                && items.TryGetConsumableItemSpellIdByMiscId(miscId, out var consumableSpellId))
                return consumableSpellId;
        }
        return 0;
    }

    private static int GetEffectiveMaximumCharges(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition? definition,
        AuthoritativeItemCatalog? items)
    {
        if (state.MaxCharges != 0)
            return state.MaxCharges;
        if (definition?.MaxCharges is > 0)
            return definition.MaxCharges;
        if (items is not null
            && items.TryGetChargedItemMetadataByMiscId(
                GetEffectiveMiscId(state, definition),
                out var maximumCharges,
                out _))
            return maximumCharges;
        return 0;
    }
}
