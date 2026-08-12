namespace Devilution.Diablo;

/** Native-compatible classification of item-backed spell sources. */
internal static class DiabloCastSourceRules
{
    public const uint LeftHandEquipmentSlot = 4;

    private const int ScrollMiscId = 21;
    private const int TargetedScrollMiscId = 22;
    private const int StaffMiscId = 23;
    private const int UniqueMiscId = 27;
    private const int FirstRuneMiscId = 47;
    private const int LastRuneMiscId = 51;

    public static uint GetChargedSpellId(int miscId, int spellId, int maximumCharges)
    {
        return miscId is StaffMiscId or UniqueMiscId
            && spellId > 0
            && maximumCharges > 0
            ? checked((uint)spellId)
            : 0;
    }

    public static uint GetConsumableSpellId(int miscId, int spellId)
    {
        if (miscId is ScrollMiscId or TargetedScrollMiscId)
            return spellId > 0 ? checked((uint)spellId) : 0;
        return miscId is >= FirstRuneMiscId and <= LastRuneMiscId
            ? checked((uint)miscId)
            : 0;
    }

    public static bool IsMatchingChargedItem(
        AuthoritativeItemState state,
        uint spellId)
    {
        return GetChargedSpellId(state.MiscId, state.SpellId, state.MaxCharges)
            == spellId;
    }

    public static bool IsMatchingConsumableItem(
        AuthoritativeItemState state,
        uint spellId)
    {
        return GetConsumableSpellId(state.MiscId, state.SpellId) == spellId;
    }
}
