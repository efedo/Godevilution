namespace Devilution.Diablo;

public sealed partial class AuthoritativeCathedralNativePopulationPolicy
{
    internal bool CanOperateNativeSarcophagus(AuthoritativeWorldObject value)
        => Bindings.CanOperateNativeSarcophagus(value);

    internal bool TryGetNativeSarcophagusOperationFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeSarcophagusFacts facts)
        => Bindings.TryGetNativeSarcophagusOperationFacts(value, out facts);

    internal AuthoritativeCathedralNativeSarcophagusFacts GetSarcophagusOperationFacts(int objectSlot)
        => Bindings.GetSarcophagusOperationFacts(objectSlot);

    internal static bool IsSupportedNativeSarcophagus(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
        => AuthoritativeNativePopulationBindings.IsSupportedNativeSarcophagus(state, raw);

    internal IReadOnlyList<DiabloNativeSarcophagusState> CreateInitialNativeSarcophagusStates()
        => Bindings.CreateInitialNativeSarcophagusStates();

    internal bool IsSarcophagusReservationSpawned(int nativeSlot, uint entityId, DiabloWorldState world)
        => Bindings.IsSarcophagusReservationSpawned(nativeSlot, entityId, world);

    internal bool OwnsSarcophagusActor(uint entityId)
        => Bindings.OwnsSarcophagusActor(entityId);

    internal AuthoritativeNativeMonsterInitialization GetSarcophagusReservation(int objectSlot)
        => Bindings.GetSarcophagusReservation(objectSlot);

    internal AuthoritativeCombatTarget CreateSarcophagusTarget(
        int nativeSlot,
        int positionX,
        int positionY,
        int direction)
        => Bindings.CreateSarcophagusTarget(nativeSlot, positionX, positionY, direction);
}
