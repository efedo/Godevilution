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
        AuthoritativeSpellCatalog? spells = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind;
        if (GetChargedSpellId(state.MiscId, state.SpellId, state.MaxCharges) != 0)
            return DiabloItemSpellSourceKind.ChargedItem;
        if (GetConsumableSpellId(state.MiscId, state.SpellId, spells) != 0)
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
        AuthoritativeSpellCatalog? spells = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind is DiabloItemSpellSourceKind.ChargedItem
                or DiabloItemSpellSourceKind.ConsumableItem
                ? state.SpellId > 0 ? checked((uint)state.SpellId) : 0
                : 0;
        var chargedSpellId = GetChargedSpellId(state.MiscId, state.SpellId, state.MaxCharges);
        return chargedSpellId != 0
            ? chargedSpellId
            : GetConsumableSpellId(state.MiscId, state.SpellId, spells);
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
        uint spellId)
    {
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ChargedItem
                && state.SpellId > 0
                && state.MaxCharges > 0
                && (uint)state.SpellId == spellId;
        return GetChargedSpellId(state.MiscId, state.SpellId, state.MaxCharges)
            == spellId;
    }

    public static bool IsMatchingConsumableItem(
        AuthoritativeItemState state,
        uint spellId,
        AuthoritativeSpellCatalog? spells)
    {
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ConsumableItem
                && state.SpellId > 0
                && (uint)state.SpellId == spellId;
        return GetConsumableSpellId(state.MiscId, state.SpellId, spells) == spellId;
    }
}
