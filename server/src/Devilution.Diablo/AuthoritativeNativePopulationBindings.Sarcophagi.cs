namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    private const int Sarcophagus = 48;
    private const int Barrel = 57;
    private static readonly HashSet<int> NativeSkeletonTypes = [8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27];
    private readonly Dictionary<int, int> linkedPreSpawnOwnerObjectByMonsterSlot = [];
    private readonly Dictionary<int, AuthoritativeNativeMonsterInitialization> sarcophagusReservationsByObjectSlot = [];
    private readonly Dictionary<int, AuthoritativeNativeMonsterInitialization> barrelReservationsByObjectSlot = [];
    private readonly IReadOnlyDictionary<int, AuthoritativeCathedralPopulationMonsterDefinition> populationMonsterDefinitions;

    private static Dictionary<int, int> GetNativePreSpawnOwnerLinks(
        IEnumerable<AuthoritativeCathedralPopulationObjectState> objects)
    {
        var result = new Dictionary<int, int>();
        foreach (var value in objects) {
            int linkedSlot;
            if (value.Type == Sarcophagus && value.Var1 >= 8)
                linkedSlot = value.Var2;
            else if (value.Type == Barrel && value.Var2 >= 8)
                linkedSlot = value.Var4;
            else
                continue;
            if (linkedSlot < 0)
                continue;
            if (linkedSlot >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
                || !result.TryAdd(linkedSlot, value.Slot))
                throw new InvalidDataException("A native pre-spawn skeleton has an invalid or duplicated object owner.");
        }
        return result;
    }

    private static bool IsLinkedPreSpawnSkeleton(
        AuthoritativeCathedralMonsterRuntimeRecord row,
        AuthoritativeCathedralThemeRoomState state)
    {
        var raw = row.InitialState;
        var reference = checked(raw.Slot + 1);
        return row.UnsupportedReason == AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton
            && NativeSkeletonTypes.Contains(raw.Type)
            && raw.Slot is >= 0 and < AuthoritativeCathedralThemeRoomState.MonsterCapacity
            && raw.Tile == default && raw.Future == raw.Tile && raw.Old == raw.Tile
            && raw.Mode == 0 && raw.Goal == 1
            && raw.GoalVar1 == 0 && raw.GoalVar2 == 0 && raw.GoalVar3 == 0
            && raw.UniqueType == 255
            && !row.Initialization.InitialAnimationPetrified
            && !state.MonsterRefs.Any(value => value == reference || value == -reference);
    }

    internal bool CanOperateNativeSarcophagus(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        return objectRows.TryGetValue(value.EntityId, out var raw)
            && IsSupportedNativeSarcophagus(InitialState, raw)
            && !value.Activated;
    }

    internal bool TryGetNativeSarcophagusOperationFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeSarcophagusFacts facts)
    {
        facts = null!;
        if (!CanOperateNativeSarcophagus(value))
            return false;

        facts = GetSarcophagusOperationFacts(objectRows[value.EntityId].Slot);
        return true;
    }

    internal AuthoritativeCathedralNativeSarcophagusFacts GetSarcophagusOperationFacts(int objectSlot)
    {
        if (!objectEntitiesBySlot.TryGetValue(objectSlot, out var entityId)
            || !objectRows.TryGetValue(entityId, out var raw)
            || !IsSupportedNativeSarcophagus(InitialState, raw))
            throw new InvalidDataException("The requested sarcophagus slot is not part of the supported immutable population.");
        sarcophagusReservationsByObjectSlot.TryGetValue(raw.Slot, out var linkedMonster);
        var specialFrameCount = 0;
        var specialTicksPerFrame = 0;
        if (linkedMonster is not null) {
            if (!populationMonsterDefinitions.TryGetValue(linkedMonster.Binding.NativeTypeId, out var definition)
                || definition.Frames.Count != 6 || definition.Rates.Count != 6)
                throw new InvalidDataException("A linked native skeleton lacks complete Special animation metadata.");
            specialFrameCount = definition.Frames[5];
            specialTicksPerFrame = definition.Rates[5];
            if (specialFrameCount <= 0 || specialFrameCount > byte.MaxValue
                || specialTicksPerFrame < 0 || specialTicksPerFrame > byte.MaxValue)
                throw new InvalidDataException("A linked native skeleton Special animation is outside runtime bounds.");
        }

        return new AuthoritativeCathedralNativeSarcophagusFacts(
            raw.Slot,
            entityId,
            raw.Type,
            LevelId,
            raw.Position,
            raw.RndSeed,
            raw.Var1,
            raw.Var2,
            raw.SelectionRegion,
            raw.AnimFrame,
            raw.AnimFlag,
            raw.AnimDelay,
            raw.AnimCount,
            raw.AnimLength,
            raw.TrapFlag,
            HasLinkedNativeTrap(InitialState, raw),
            linkedMonster,
            specialFrameCount,
            specialTicksPerFrame);
    }

    // Native animation delay/count are int fields; SetupObject legitimately uses a 1000-tick delay.
    internal static bool IsSupportedNativeSarcophagus(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
    {
        if (raw.Type != Sarcophagus || raw.TrapFlag || raw.SelectionRegion == 0
            || raw.AnimFlag != 0 || raw.AnimDelay < 1
            || raw.AnimCount < 0 || raw.AnimCount >= raw.AnimDelay
            || raw.AnimLength == 0 || raw.AnimFrame > raw.AnimLength
            || raw.Var1 is < 0 or > 9 || HasLinkedNativeTrap(state, raw))
            return false;
        if (raw.Var1 >= 8 && raw.Var2 >= 0) {
            if (!state.ActiveMonsterIds.Take(state.ActiveMonsterCount).Contains(raw.Var2))
                return false;
        }
        return true;
    }

    internal IReadOnlyList<DiabloNativeSarcophagusState> CreateInitialNativeSarcophagusStates()
        => objectRows.Values
            .Where(raw => IsSupportedNativeSarcophagus(InitialState, raw))
            .OrderBy(raw => raw.Slot)
            .Select(raw => {
                var linked = sarcophagusReservationsByObjectSlot.TryGetValue(raw.Slot, out var initialization)
                    ? initialization
                    : null;
                return new DiabloNativeSarcophagusState(
                    raw.Slot,
                    objectEntitiesBySlot[raw.Slot],
                    raw.Type,
                    raw.Position.X,
                    raw.Position.Y,
                    raw.RndSeed,
                    raw.Var1,
                    raw.Var2,
                    raw.SelectionRegion,
                    raw.TrapFlag,
                    HasLinkedNativeTrap(InitialState, raw),
                    raw.AnimFrame,
                    raw.AnimFlag,
                    raw.AnimDelay,
                    raw.AnimCount,
                    raw.AnimLength,
                    Opened: false,
                    raw.AnimFlag,
                    raw.AnimDelay,
                    raw.AnimCount,
                    raw.AnimFrame,
                    linked?.Binding.NativeSlot ?? -1,
                    linked?.Binding.EntityId ?? 0,
                    linked?.Binding.NativeTypeId ?? -1,
                    linked is null ? DiabloNativeSarcophagusActorState.None : DiabloNativeSarcophagusActorState.Reserved,
                    0,
                    0,
                    0,
                    0,
                    0);
            })
            .ToArray();

    internal bool IsSarcophagusReservationSpawned(int nativeSlot, uint entityId, DiabloWorldState world)
    {
        if (!linkedPreSpawnOwnerObjectByMonsterSlot.TryGetValue(nativeSlot, out var objectSlot)
            || !sarcophagusReservationsByObjectSlot.TryGetValue(objectSlot, out var initialization)
            || initialization.Binding.EntityId != entityId)
            return false;
        var state = world.GetNativeSarcophagusState(LevelId, objectSlot);
        return state?.ActorState is DiabloNativeSarcophagusActorState.Emerging
            or DiabloNativeSarcophagusActorState.Active
            or DiabloNativeSarcophagusActorState.Dead;
    }

    internal bool OwnsSarcophagusActor(uint entityId)
        => sarcophagusReservationsByObjectSlot.Values.Any(value => value.Binding.EntityId == entityId);

    internal AuthoritativeNativeMonsterInitialization GetSarcophagusReservation(int objectSlot)
        => sarcophagusReservationsByObjectSlot.TryGetValue(objectSlot, out var initialization)
            ? initialization
            : throw new InvalidDataException("A native sarcophagus has no exact pre-spawn reservation.");

    internal AuthoritativeCombatTarget CreateSarcophagusTarget(
        int nativeSlot,
        int positionX,
        int positionY,
        int direction)
    {
        var record = liveMonsterAdapter.Records.SingleOrDefault(value => value.Binding.NativeSlot == nativeSlot)
            ?? throw new InvalidDataException("A linked native skeleton slot is outside the retained active pool.");
        if (record.UnsupportedReason != AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton
            || record.Target is not null
            || !linkedPreSpawnOwnerObjectByMonsterSlot.TryGetValue(nativeSlot, out var ownerSlot)
            || !sarcophagusReservationsByObjectSlot.ContainsKey(ownerSlot))
            throw new InvalidDataException("A native sarcophagus may activate only its exact linked skeleton reservation.");
        return liveMonsterAdapter.CreatePreSpawnTarget(nativeSlot, positionX, positionY, direction);
    }

    private static bool HasLinkedNativeTrap(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
        => state.ActiveObjectIds.Take(state.ActiveObjectCount).Any(slot => {
            var candidate = state.ObjectSlots[slot];
            return candidate.Type is LeftTrap or RightTrap
                && candidate.Var1 == raw.Position.X
                && candidate.Var2 == raw.Position.Y;
        });
}
