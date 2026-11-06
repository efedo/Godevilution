namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    internal static bool IsSupportedNativeShrine(AuthoritativeCathedralPopulationObjectState value)
        => value.Type is 59 or 60 && value.Var1 is >= 0 and <= 25 && value.SelectionRegion != 0;

    internal int? NativeShrineType(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        return objectRows.TryGetValue(value.EntityId, out var raw) && IsSupportedNativeShrine(raw) ? raw.Var1 : null;
    }

    internal bool TryGetNativeShrineFacts(AuthoritativeWorldObject value, out int shrineType,
        out uint seed, out int finalFrame)
    {
        ValidateObject(value);
        shrineType = 0; seed = 0; finalFrame = 0;
        if (value.Activated || !objectRows.TryGetValue(value.EntityId, out var raw) || !IsSupportedNativeShrine(raw))
            return false;
        shrineType = raw.Var1; seed = raw.RndSeed; finalFrame = checked((int)raw.AnimLength);
        return true;
    }

    /** Native objects.cpp OperateShrineThaumaturgic visits ActiveObjects in native slot order. */
    internal IEnumerable<uint> NativeChestEntitiesInActiveOrder()
        => InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount)
            .Select(slot => InitialState.ObjectSlots[slot]).Where(IsSupportedNativeChest)
            .Select(raw => objectEntitiesBySlot[raw.Slot]);
}
