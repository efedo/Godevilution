namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    internal bool TryGetNativeBarrelObjectSlot(uint entityId, out int slot)
    {
        if (objectRows.TryGetValue(entityId, out var raw)
            && IsSupportedNativeBarrel(InitialState, raw)) {
            slot = raw.Slot;
            return true;
        }
        slot = -1;
        return false;
    }

    internal bool CanBreakNativeBarrel(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        return objectRows.TryGetValue(value.EntityId, out var raw)
            && IsSupportedNativeBarrel(InitialState, raw)
            && !value.Activated;
    }

    internal bool TryGetNativeBarrelFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeBarrelFacts facts)
    {
        facts = null!;
        if (!CanBreakNativeBarrel(value))
            return false;
        facts = GetNativeBarrelFacts(objectRows[value.EntityId].Slot);
        return true;
    }

    internal AuthoritativeCathedralNativeBarrelFacts GetNativeBarrelFacts(int objectSlot)
    {
        if (!objectEntitiesBySlot.TryGetValue(objectSlot, out var entityId)
            || !objectRows.TryGetValue(entityId, out var raw)
            || !IsSupportedNativeBarrel(InitialState, raw))
            throw new InvalidDataException("The requested barrel slot is not part of the supported immutable population.");

        barrelReservationsByObjectSlot.TryGetValue(raw.Slot, out var linkedMonster);
        var specialFrameCount = 0;
        var specialTicksPerFrame = 0;
        if (linkedMonster is not null) {
            if (!populationMonsterDefinitions.TryGetValue(linkedMonster.Binding.NativeTypeId, out var definition)
                || definition.Frames.Count != 6 || definition.Rates.Count != 6)
                throw new InvalidDataException("A linked barrel skeleton lacks complete Special animation metadata.");
            specialFrameCount = definition.Frames[5];
            specialTicksPerFrame = definition.Rates[5];
            if (specialFrameCount <= 0 || specialFrameCount > byte.MaxValue
                || specialTicksPerFrame < 0 || specialTicksPerFrame > byte.MaxValue)
                throw new InvalidDataException("A linked barrel skeleton Special animation is outside runtime bounds.");
        }

        return new AuthoritativeCathedralNativeBarrelFacts(
            raw.Slot,
            entityId,
            raw.Type,
            LevelId,
            raw.Position,
            raw.RndSeed,
            raw.Var1,
            raw.Var2,
            raw.Var3,
            raw.Var4,
            raw.SelectionRegion,
            raw.AnimFrame,
            raw.AnimFlag,
            raw.AnimDelay,
            raw.AnimCount,
            raw.AnimLength,
            raw.SolidFlag,
            raw.MissilePassable,
            raw.BreakFlag,
            raw.PreFlag,
            raw.TrapFlag,
            HasLinkedNativeTrap(InitialState, raw),
            linkedMonster,
            specialFrameCount,
            specialTicksPerFrame);
    }

    internal IReadOnlyList<DiabloNativeBarrelState> CreateInitialNativeBarrelStates()
        => objectRows.Values
            .Where(raw => IsSupportedNativeBarrel(InitialState, raw))
            .OrderBy(raw => raw.Slot)
            .Select(raw => {
                var linked = barrelReservationsByObjectSlot.TryGetValue(raw.Slot, out var initialization)
                    ? initialization
                    : null;
                return new DiabloNativeBarrelState(
                    raw.Slot,
                    objectEntitiesBySlot[raw.Slot],
                    raw.Type,
                    raw.Position.X,
                    raw.Position.Y,
                    raw.RndSeed,
                    raw.Var1,
                    raw.Var2,
                    raw.Var3,
                    raw.Var4,
                    raw.SelectionRegion,
                    raw.AnimFrame,
                    raw.AnimFlag,
                    raw.AnimDelay,
                    raw.AnimCount,
                    raw.AnimLength,
                    raw.SolidFlag,
                    raw.MissilePassable,
                    raw.BreakFlag,
                    raw.PreFlag,
                    raw.TrapFlag,
                    HasLinkedNativeTrap(InitialState, raw),
                    Broken: false,
                    raw.AnimFlag,
                    raw.AnimFrame,
                    raw.AnimDelay,
                    raw.AnimCount,
                    raw.SolidFlag,
                    raw.MissilePassable,
                    raw.BreakFlag,
                    raw.SelectionRegion,
                    raw.PreFlag,
                    linked?.Binding.NativeSlot ?? -1,
                    linked?.Binding.EntityId ?? 0,
                    linked?.Binding.NativeTypeId ?? -1,
                    linked is null ? DiabloNativeBarrelActorState.None : DiabloNativeBarrelActorState.Reserved,
                    0,
                    0,
                    0,
                    0,
                    0);
            })
            .ToArray();

    internal bool IsNativeBarrelReservationSpawned(int nativeSlot, uint entityId, DiabloWorldState world)
    {
        if (!linkedPreSpawnOwnerObjectByMonsterSlot.TryGetValue(nativeSlot, out var objectSlot)
            || !barrelReservationsByObjectSlot.TryGetValue(objectSlot, out var initialization)
            || initialization.Binding.EntityId != entityId)
            return false;
        return world.GetNativeBarrelState(LevelId, objectSlot)?.ActorState is DiabloNativeBarrelActorState.Emerging
            or DiabloNativeBarrelActorState.Active or DiabloNativeBarrelActorState.Dead;
    }

    internal bool OwnsNativeBarrelActor(uint entityId)
        => barrelReservationsByObjectSlot.Values.Any(value => value.Binding.EntityId == entityId);

    internal AuthoritativeNativeMonsterInitialization GetNativeBarrelReservation(int objectSlot)
        => barrelReservationsByObjectSlot.TryGetValue(objectSlot, out var initialization)
            ? initialization
            : throw new InvalidDataException("A native barrel has no exact pre-spawn reservation.");

    internal AuthoritativeNativeMonsterInitialization GetPreSpawnReservation(int objectSlot)
        => sarcophagusReservationsByObjectSlot.TryGetValue(objectSlot, out var sarcophagus)
            ? sarcophagus
            : GetNativeBarrelReservation(objectSlot);

    internal AuthoritativeCombatTarget CreateNativeBarrelTarget(
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
            || !barrelReservationsByObjectSlot.ContainsKey(ownerSlot))
            throw new InvalidDataException("A native barrel may activate only its exact linked skeleton reservation.");
        return liveMonsterAdapter.CreatePreSpawnTarget(nativeSlot, positionX, positionY, direction);
    }

    internal static bool IsSupportedNativeBarrel(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
    {
        if (raw.Type is not (57 or 58) || raw.TrapFlag || raw.SelectionRegion == 0
            || raw.AnimFlag != 0 || raw.AnimDelay < 1
            || raw.AnimCount != 0
            || raw.AnimLength == 0 || raw.AnimLength > byte.MaxValue
            || raw.AnimFrame > raw.AnimLength || !raw.SolidFlag
            || raw.Var1 != 0 || raw.Var3 is < 0 or > 2 || raw.BreakFlag < 0
            || HasLinkedNativeTrap(state, raw))
            return false;
        if (raw.Type == AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType
            && (raw.Var2 is < 0 or > 9 || raw.Var3 is < 0 or > 2))
            return false;
        if (raw.Type == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType
            && raw.Var2 != 0)
            return false;
        if (raw.Type == AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType
            && raw.Var2 >= 8 && raw.Var4 >= 0
            && !state.ActiveMonsterIds.Take(state.ActiveMonsterCount).Contains(raw.Var4))
            return false;
        return true;
    }
}
