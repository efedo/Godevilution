namespace Devilution.Diablo;

/** Native item-flag rules that affect player spell resource admission. */
public static class AuthoritativeEquipmentManaRules
{
    /** Native ItemSpecialEffect::NoMana flag. */
    public const uint NoManaFlag = 1U << 27;

    /** Returns whether usable, identified equipment disables ordinary mana use. */
    public static bool HasNoManaItem(IEnumerable<EquippedStoreItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (state.StatFlag
                && (state.Magical == 0 || state.Identified)
                && (state.Flags & NoManaFlag) != 0) {
                return true;
            }
        }
        return false;
    }
}
