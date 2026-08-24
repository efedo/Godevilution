namespace Devilution.Diablo;

/** Native Smith/Witch buy filters applied before generic sale pricing. */
public static class AuthoritativeStoreSalePolicy
{
    private const int OilFirst = 29;
    private const int OilLast = 41;
    // Retained only for legacy direct callers that do not provide an item catalog.
    private const int LegacyFirstQuestItemIndex = 6;
    private const int LegacyLastQuestItemIndex = 22;
    // Retained only for legacy direct callers that do not provide an item catalog.
    // Shipped authoritative content declares the exception through the NoSale tag.
    private const int LegacyLazarusStaffItemIndex = 33;

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
        if (IsNoSale(state, items))
            return false;
        if (IsOil(state, items))
            return true;
        if (IsMisc(state, items) || IsGold(state, items) || IsQuest(state, items))
            return false;
        if (IsStaff(state, items)
            && (gameVariant != DiabloGameVariant.Hellfire
                || HasValidSpell(state, spells)))
            return false;
        return items is not null || state.ItemIndex != LegacyLazarusStaffItemIndex;
    }

    private static bool WitchAccepts(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items,
        AuthoritativeSpellCatalog? spells,
        DiabloGameVariant gameVariant)
    {
        if (IsNoSale(state, items))
            return false;
        var accepts = IsMisc(state, items);
        if (IsOil(state, items))
            accepts = false;
        if (IsStaff(state, items)
            && (gameVariant != DiabloGameVariant.Hellfire
                || HasValidSpell(state, spells)))
            accepts = true;
        if (IsQuest(state, items)
            || (items is null
                && state.ItemIndex is >= LegacyFirstQuestItemIndex and <= LegacyLastQuestItemIndex))
            accepts = false;
        if (items is null && state.ItemIndex == LegacyLazarusStaffItemIndex)
            accepts = false;
        return accepts;
    }

    private static bool IsNoSale(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
        => items is not null
            && items.TryGetByState(state, out var definition)
            && definition.GenerationTags.Contains("NoSale", StringComparer.OrdinalIgnoreCase);

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

    private static bool IsOil(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        var action = state.ItemActionKind;
        if (action == DiabloItemActionKind.None
            && items is not null
            && items.TryGetByState(state, out var definition)) {
            action = definition.ItemActionKind;
        }
        var isOilAction = action is DiabloItemActionKind.ApplyBlacksmithOil
            or DiabloItemActionKind.ApplyAccuracyOil
            or DiabloItemActionKind.ApplySharpnessOil
            or DiabloItemActionKind.ApplyFortitudeOil;
        return isOilAction
            || items is null && state.MiscId > OilFirst && state.MiscId < OilLast;
    }
}
