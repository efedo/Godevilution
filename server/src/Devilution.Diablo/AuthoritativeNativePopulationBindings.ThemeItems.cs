namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    internal bool IsSupportedNativeThemeItem(AuthoritativeCathedralPopulationObjectState raw)
        => IsSupportedNativeThemeItem(raw, InitialState.MonsterSlots);

    internal static bool IsSupportedNativeThemeItem(AuthoritativeCathedralPopulationObjectState raw,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsters)
        => AuthoritativeNativeThemeItemLootGenerator.Supports(raw.Type) && raw.SelectionRegion != 0
            && raw.AnimFrame is >= 1 and <= 253 && !raw.TrapFlag
            && (raw.Type is not (62 or 63) || raw.AnimFrame >= 3
                // Native objects.cpp OperateBookcase consults Monsters[MAX_PLRS], unique type Zhar = 2.
                && monsters[4].UniqueType != 2);

    internal bool CanOperateNativeThemeItem(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        return !value.Activated && objectRows.TryGetValue(value.EntityId, out var raw)
            && IsSupportedNativeThemeItem(raw);
    }

    internal bool TryGetNativeThemeItemFacts(AuthoritativeWorldObject value,
        out AuthoritativeCathedralPopulationObjectState? raw)
    {
        ValidateObject(value);
        raw = null;
        if (!CanOperateNativeThemeItem(value)) return false;
        raw = objectRows[value.EntityId];
        return true;
    }

    internal static int ThemeItemOpenedFrame(AuthoritativeCathedralPopulationObjectState raw)
        => raw.Type switch { 61 or 64 => (int)raw.AnimFrame + 2, 62 or 63 => (int)raw.AnimFrame - 2,
            67 => (int)raw.AnimFrame, _ => (int)raw.AnimFrame + 1 };

    internal void CommitNativeThemeItem(AuthoritativeWorldObject value)
    {
        if (!CanOperateNativeThemeItem(value)) throw new InvalidOperationException("A prepared native item object lost its owner.");
        value.Activated = true;
        value.AnimationFrame = ThemeItemOpenedFrame(objectRows[value.EntityId]);
        ValidateObject(value);
    }
}
