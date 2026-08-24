namespace Devilution.Diablo;

/** Native Smith/Witch buy filters applied before generic sale pricing. */
public static class AuthoritativeStoreSalePolicy
{
    private const int OilFirst = 29;
    private const int OilLast = 41;
    private const int FirstQuestItemIndex = 6;
    private const int LastQuestItemIndex = 22;
    private const int LazarusStaffItemIndex = 33;

    public static bool Accepts(
        AuthoritativeStoreRole role,
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        AuthoritativeSpellCatalog? spells,
        DiabloGameVariant gameVariant)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ItemType < 0)
            return false;
        return role switch {
            AuthoritativeStoreRole.Generic => true,
            AuthoritativeStoreRole.Smith => SmithAccepts(state, items, spells, gameVariant),
            AuthoritativeStoreRole.Witch => WitchAccepts(state, items, spells, gameVariant),
            AuthoritativeStoreRole.Wirt
                or AuthoritativeStoreRole.Healer
                or AuthoritativeStoreRole.Adria => false,
            _ => false,
        };
    }

    private static bool SmithAccepts(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        AuthoritativeSpellCatalog? spells,
        DiabloGameVariant gameVariant)
    {
        if (IsOil(state))
            return true;
        if (IsMisc(state, items) || IsGold(state, items) || IsQuest(state, items))
            return false;
        if (IsStaff(state, items)
            && (gameVariant != DiabloGameVariant.Hellfire
                || HasValidSpell(state, spells)))
            return false;
        return state.ItemIndex != LazarusStaffItemIndex;
    }

    private static bool WitchAccepts(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        AuthoritativeSpellCatalog? spells,
        DiabloGameVariant gameVariant)
    {
        var accepts = IsMisc(state, items);
        if (state.MiscId > OilFirst && state.MiscId < OilLast)
            accepts = false;
        if (IsStaff(state, items)
            && (gameVariant != DiabloGameVariant.Hellfire
                || HasValidSpell(state, spells)))
            accepts = true;
        if (IsQuest(state, items)
            || state.ItemIndex is >= FirstQuestItemIndex and <= LastQuestItemIndex)
            accepts = false;
        if (state.ItemIndex == LazarusStaffItemIndex)
            accepts = false;
        return accepts;
    }

    private static bool IsMisc(AuthoritativeItemState state, AuthoritativeItemCatalog? items)
    {
        if (items is not null && items.TryGetByState(state, out var definition))
            return definition.ItemClass == 3;
        return state.ItemClass == 3 || state.ItemType == 0;
    }

    private static bool IsQuest(AuthoritativeItemState state, AuthoritativeItemCatalog? items)
    {
        if (items is not null && items.TryGetByState(state, out var definition))
            return definition.ItemClass == 5
                || definition.GenerationTags.Contains("Quest", StringComparer.OrdinalIgnoreCase);
        return state.ItemClass == 5;
    }

    private static bool IsGold(AuthoritativeItemState state, AuthoritativeItemCatalog? items)
    {
        if (items is not null && items.TryGetByState(state, out var definition))
            return AuthoritativeItemClassification.IsGold(definition);
        return state.ItemClass == 4 || state.ItemType == 11;
    }

    private static bool IsStaff(AuthoritativeItemState state, AuthoritativeItemCatalog? items)
        => AuthoritativeItemClassification.IsStaff(state, items);

    private static bool HasValidSpell(
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells)
        => state.SpellId > 0
            && spells is not null
            && spells.TryGet(checked((uint)state.SpellId), out _);

    private static bool IsOil(AuthoritativeItemState state)
        => state.MiscId > OilFirst && state.MiscId < OilLast;
}
