namespace Devilution.Diablo;

public sealed partial class AuthoritativeCathedralNativePopulationPolicy
{
    internal bool TryGetNativeBarrelObjectSlot(uint entityId, out int slot)
        => Bindings.TryGetNativeBarrelObjectSlot(entityId, out slot);

    internal bool CanBreakNativeBarrel(AuthoritativeWorldObject value)
        => Bindings.CanBreakNativeBarrel(value);

    internal bool TryGetNativeBarrelFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeBarrelFacts facts)
        => Bindings.TryGetNativeBarrelFacts(value, out facts);

    internal AuthoritativeCathedralNativeBarrelFacts GetNativeBarrelFacts(int objectSlot)
        => Bindings.GetNativeBarrelFacts(objectSlot);

    internal IReadOnlyList<DiabloNativeBarrelState> CreateInitialNativeBarrelStates()
        => Bindings.CreateInitialNativeBarrelStates();

    internal bool IsNativeBarrelReservationSpawned(int nativeSlot, uint entityId, DiabloWorldState world)
        => Bindings.IsNativeBarrelReservationSpawned(nativeSlot, entityId, world);

    internal bool OwnsNativeBarrelActor(uint entityId)
        => Bindings.OwnsNativeBarrelActor(entityId);

    internal AuthoritativeNativeMonsterInitialization GetNativeBarrelReservation(int objectSlot)
        => Bindings.GetNativeBarrelReservation(objectSlot);

    internal AuthoritativeNativeMonsterInitialization GetPreSpawnReservation(int objectSlot)
        => Bindings.GetPreSpawnReservation(objectSlot);

    internal AuthoritativeCombatTarget CreateNativeBarrelTarget(
        int nativeSlot,
        int positionX,
        int positionY,
        int direction)
        => Bindings.CreateNativeBarrelTarget(nativeSlot, positionX, positionY, direction);

    internal static bool IsSupportedNativeBarrel(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
        => AuthoritativeNativePopulationBindings.IsSupportedNativeBarrel(state, raw);
}
