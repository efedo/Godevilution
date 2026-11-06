namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    internal static bool IsSupportedNativeThemeEffect(AuthoritativeCathedralPopulationObjectState raw)
        => raw.Type is 66 or 76 or 79 or 80 or 81 or 82 && raw.SelectionRegion != 0 && !raw.TrapFlag;

    internal bool TryGetNativeThemeEffectFacts(AuthoritativeWorldObject value,
        out AuthoritativeCathedralPopulationObjectState raw)
    {
        ValidateObject(value);
        raw = objectRows[value.EntityId];
        return IsSupportedNativeThemeEffect(raw) && !value.Activated;
    }

    /** Native OperateFountains/OperateGoatShrine keep the current frame; only OperateCauldron replaces it. */
    private static bool IsNativeThemeEffectProjection(DiabloObjectState current,
        DiabloObjectState expected, AuthoritativeCathedralPopulationObjectState raw)
        => IsSupportedNativeThemeEffect(raw) && raw.Type is not (66 or 76) && current.Activated
            && current.AnimationFrame == (raw.Type == 80 ? 3 : expected.AnimationFrame)
            && current with { Activated = expected.Activated, AnimationFrame = expected.AnimationFrame } == expected;

    internal IEnumerable<uint> NativeBurningCrossEntitiesInActiveOrder()
        => InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount)
            .Select(slot => InitialState.ObjectSlots[slot]).Where(raw => raw.Type is 26 or 91)
            .Select(raw => objectEntitiesBySlot[raw.Slot]);
}
