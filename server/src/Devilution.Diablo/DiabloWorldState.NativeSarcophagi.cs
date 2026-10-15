namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private Dictionary<int, DiabloNativeSarcophagusState> nativeSarcophagiBySlot = [];

    internal bool HasNativeSarcophagusMutations
        => nativeSarcophagiBySlot.Values.Any(value => value.Opened);

    internal IReadOnlyList<DiabloNativeSarcophagusState>? CaptureNativeSarcophagi()
        => nativePopulationPolicy is null
            ? null
            : Array.AsReadOnly(nativeSarcophagiBySlot.Values.OrderBy(value => value.ObjectSlot).ToArray());

    internal DiabloNativeSarcophagusState? GetNativeSarcophagusState(int objectSlot)
        => nativeSarcophagiBySlot.TryGetValue(objectSlot, out var state) ? state : null;

    internal void ConfigureNativeSarcophagi(
        AuthoritativeCathedralNativePopulationPolicy policy,
        int formatVersion,
        IReadOnlyList<DiabloNativeSarcophagusState>? savedStates)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (nativePopulationPolicy != policy)
            throw new InvalidOperationException("Native sarcophagus state requires its configured immutable population owner.");

        var initial = policy.CreateInitialNativeSarcophagusStates();
        if (formatVersion >= DiabloCohortState.NativeSarcophagusFormatVersion) {
            if (savedStates is null)
                throw new InvalidDataException("Native sarcophagus cohorts require their complete object lifecycle records.");
            if (savedStates.Count != initial.Count)
                throw new InvalidDataException("Native sarcophagus cohort omits a supported generated object.");
            var restored = new Dictionary<int, DiabloNativeSarcophagusState>();
            for (var index = 0; index < initial.Count; index++) {
                var actual = savedStates[index];
                actual.ValidateShape();
                if (actual.ObjectSlot != initial[index].ObjectSlot
                    || actual.EntityId != initial[index].EntityId
                    || actual.NativeTypeId != initial[index].NativeTypeId
                    || actual.InitialPositionX != initial[index].InitialPositionX
                    || actual.InitialPositionY != initial[index].InitialPositionY
                    || actual.RandomSeed != initial[index].RandomSeed
                    || actual.Var1 != initial[index].Var1 || actual.Var2 != initial[index].Var2
                    || actual.InitialSelectionRegion != initial[index].InitialSelectionRegion
                    || actual.TrapArmed != initial[index].TrapArmed
                    || actual.HasLinkedTrap != initial[index].HasLinkedTrap
                    || actual.InitialAnimationFrame != initial[index].InitialAnimationFrame
                    || actual.InitialAnimationFlag != initial[index].InitialAnimationFlag
                    || actual.InitialAnimationDelay != initial[index].InitialAnimationDelay
                    || actual.InitialAnimationCount != initial[index].InitialAnimationCount
                    || actual.InitialAnimationLength != initial[index].InitialAnimationLength
                    || actual.LinkedMonsterSlot != initial[index].LinkedMonsterSlot
                    || actual.LinkedMonsterEntityId != initial[index].LinkedMonsterEntityId
                    || actual.LinkedMonsterTypeId != initial[index].LinkedMonsterTypeId
                    || !restored.TryAdd(actual.ObjectSlot, actual))
                    throw new InvalidDataException("Native sarcophagus lifecycle identity differs from the exact generated object and reservation.");
            }
            nativeSarcophagiBySlot = restored;
        } else {
            if (savedStates is not null)
                throw new InvalidDataException("Older native cohorts cannot carry sarcophagus lifecycle state.");
            nativeSarcophagiBySlot = initial.ToDictionary(value => value.ObjectSlot);
        }

        ValidateNativeSarcophagusState(policy);
    }

    internal bool CanOperateNativeSarcophagus(AuthoritativeWorldObject value)
    {
        if (nativePopulationPolicy is null || !nativePopulationPolicy.CanOperateNativeSarcophagus(value))
            return false;
        if (!nativePopulationPolicy.TryGetNativeSarcophagusOperationFacts(value, out var facts))
            return false;
        var state = GetNativeSarcophagusState(facts.ObjectSlot);
        return state is { Opened: false, ActorState: not DiabloNativeSarcophagusActorState.Emerging
            and not DiabloNativeSarcophagusActorState.Active and not DiabloNativeSarcophagusActorState.Dead };
    }

    internal bool TryGetNativeSarcophagusOperationFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeSarcophagusFacts facts)
    {
        facts = null!;
        if (!CanOperateNativeSarcophagus(value))
            return false;
        return nativePopulationPolicy!.TryGetNativeSarcophagusOperationFacts(value, out facts);
    }

    internal IReadOnlyList<bool> GetNativeSarcophagusSpawnAvailability(
        AuthoritativeWorldObject sarcophagus,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(sarcophagus);
        ArgumentNullException.ThrowIfNull(participants);
        if (!TryGetNativeSarcophagusOperationFacts(sarcophagus, out var facts)
            || facts.LinkedMonster is null)
            return Array.AsReadOnly(new bool[9]);

        // ActivateSkeleton's direction order maps to NW, N, NE, W, E, SW, S, SE.
        var offsets = new (int X, int Y)[] {
            (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
        };
        if (cathedralItems is null)
            throw new InvalidDataException("Native sarcophagus placement requires its item-world owner.");
        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, facts.LevelId, participants);
        return Array.AsReadOnly(offsets.Select(offset => {
            var x = checked(facts.PositionX + offset.X);
            var y = checked(facts.PositionY + offset.Y);
            return NativeSarcophagusTileAvailable(facts.LevelId, x, y, occupancy);
        }).ToArray());
    }

    internal bool CanCommitNativeSarcophagusOperation(
        AuthoritativeWorldObject value,
        AuthoritativeCathedralSarcophagusOperationResult operation,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(participants);
        if (!TryGetNativeSarcophagusOperationFacts(value, out var facts)
            || operation.InitialRandomState != facts.RandomSeed)
            return false;

        var shouldLoot = facts.Var1 <= 2;
        if (shouldLoot != (operation.Loot is not null))
            return false;
        var shouldConsiderSpawn = facts.Var1 >= 8 && facts.Var2 >= 0;
        if (!shouldConsiderSpawn && operation.Spawn is not null)
            return false;

        var currentState = nativeSarcophagiBySlot[facts.ObjectSlot];
        if (currentState.Opened || currentState.ActorState is not (DiabloNativeSarcophagusActorState.None
            or DiabloNativeSarcophagusActorState.Reserved))
            return false;

        var availableSpawnTiles = GetNativeSarcophagusSpawnAvailability(value, participants);

        if (operation.Spawn is not { } spawn)
            return !shouldConsiderSpawn
                || !availableSpawnTiles.Any(available => available);
        var availabilityIndex = SpawnAvailabilityIndex(
            facts.PositionX, facts.PositionY, spawn.PositionX, spawn.PositionY);
        if (facts.LinkedMonster is null || spawn.NativeMonsterSlot != facts.LinkedMonster.Binding.NativeSlot
            || spawn.EntityId != facts.LinkedMonster.Binding.EntityId
            || spawn.NativeTypeId != facts.LinkedMonster.Binding.NativeTypeId
            || spawn.SpecialFrameCount != facts.NativeSpecialFrameCount
            || spawn.SpecialTicksPerFrame != facts.NativeSpecialTicksPerFrame
            || (uint)spawn.PositionX >= AuthoritativeCathedralThemeRoomState.Width
            || (uint)spawn.PositionY >= AuthoritativeCathedralThemeRoomState.Width
            || spawn.Facing is < 0 or > 7
            || availabilityIndex < 0 || !availableSpawnTiles[availabilityIndex])
            return false;

        var entityId = spawn.EntityId;
        if (CombatTargets.ContainsKey(entityId) || WorldItems.ContainsKey(entityId)
            || Objects.ContainsKey(entityId) || Projectiles.ContainsKey(entityId)
            || Portals.ContainsKey(entityId) || Corpses.ContainsKey(entityId))
            return false;
        var owner = nativePopulationPolicy!.GetSarcophagusReservation(facts.ObjectSlot);
        if (owner.Binding.EntityId != entityId || owner.Binding.NativeSlot != spawn.NativeMonsterSlot
            || owner.Binding.NativeTypeId != spawn.NativeTypeId)
            return false;

        var center = spawn.PositionX == facts.PositionX && spawn.PositionY == facts.PositionY;
        if (spawn.IsCenterSpawn != center)
            return false;
        if (center ? spawn.Facing != 1 : !IsNativeSpawnDirection(facts.PositionX, facts.PositionY,
                spawn.PositionX, spawn.PositionY, spawn.Facing))
            return false;

        // Construct the exact actor in preflight. This validates the retained
        // slot, type, derived combat projection, and spawn location before the
        // item transaction allocates any world entity IDs.
        var prepared = nativePopulationPolicy.CreateSarcophagusTarget(
            spawn.NativeMonsterSlot, spawn.PositionX, spawn.PositionY, spawn.Facing);
        return IsValidSarcophagusSpawnTarget(prepared, owner, spawn, facts.LevelId);
    }

    internal bool TryCommitNativeSarcophagusOperation(
        AuthoritativeWorldObject value,
        AuthoritativeCathedralSarcophagusOperationResult operation,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!CanCommitNativeSarcophagusOperation(value, operation, participants))
            return false;
        var facts = nativePopulationPolicy!.TryGetNativeSarcophagusOperationFacts(value, out var resolved)
            ? resolved
            : throw new InvalidOperationException("A prevalidated sarcophagus lost its raw owner facts.");
        var current = nativeSarcophagiBySlot[facts.ObjectSlot];
        var next = current with {
            Opened = true,
            AnimationFlag = 1,
            AnimationDelay = 3,
            ActorState = current.LinkedMonsterSlot < 0
                ? DiabloNativeSarcophagusActorState.None
                : DiabloNativeSarcophagusActorState.Reserved,
        };
        next.ValidateShape();
        var openedProjection = DiabloSimulation.CaptureObject(value) with {
            Activated = true,
            AnimationFrame = checked((int)next.AnimationFrame),
        };
        nativePopulationPolicy.ValidateObjectProjection(openedProjection);
        AuthoritativeCombatTarget? preparedTarget = null;
        if (operation.Spawn is { } spawn) {
            var owner = nativePopulationPolicy.GetSarcophagusReservation(facts.ObjectSlot);
            preparedTarget = nativePopulationPolicy.CreateSarcophagusTarget(
                spawn.NativeMonsterSlot, spawn.PositionX, spawn.PositionY, spawn.Facing);
            if (!IsValidSarcophagusSpawnTarget(preparedTarget, owner, spawn, facts.LevelId))
                return false;
            next = next with {
                ActorState = DiabloNativeSarcophagusActorState.Emerging,
                SpawnPositionX = spawn.PositionX,
                SpawnPositionY = spawn.PositionY,
                SpawnDirection = spawn.Facing,
                SpecialFrameIndex = 0,
                SpecialTickCounter = 0,
            };
            next.ValidateShape();
            if (preparedTarget is null || !CombatTargets.TryAdd(preparedTarget.EntityId, preparedTarget))
                throw new InvalidOperationException("A prevalidated sarcophagus could not materialize its reserved actor identity.");
        }
        nativeSarcophagiBySlot[facts.ObjectSlot] = next;
        value.Activated = true;
        value.AnimationFrame = checked((int)next.AnimationFrame);
        ValidateNativeSarcophagusState(nativePopulationPolicy);
        return true;
    }

    internal bool AdvanceNativeSarcophagusSpecialStand(AuthoritativeCombatTarget target)
    {
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsSarcophagusActor(target.EntityId))
            return false;
        var entry = nativeSarcophagiBySlot.Values.Single(value => value.LinkedMonsterEntityId == target.EntityId);
        if (entry.ActorState != DiabloNativeSarcophagusActorState.Emerging)
            return false;
        if (target.IsPetrified && target.HitPointsQ6 > 0)
            return true; // Stone Curse pauses and later resumes the source SpecialStand.
        if (target.HitPointsQ6 <= 0 || target.NativeMeleeCursor is { HitRecovery: true }) {
            InterruptNativeSarcophagusSpecialStand(target);
            return false;
        }
        var facts = nativePopulationPolicy.GetSarcophagusOperationFacts(entry.ObjectSlot);
        if (entry.SpecialFrameIndex >= facts.NativeSpecialFrameCount - 1) {
            nativeSarcophagiBySlot[entry.ObjectSlot] = entry with {
                ActorState = DiabloNativeSarcophagusActorState.Active,
                SpecialFrameIndex = 0,
                SpecialTickCounter = 0,
            };
            return false;
        }

        var tick = entry.SpecialTickCounter + 1;
        var frame = entry.SpecialFrameIndex;
        if (tick >= Math.Max(1, facts.NativeSpecialTicksPerFrame)) {
            tick = 0;
            frame++;
        }
        nativeSarcophagiBySlot[entry.ObjectSlot] = entry with {
            SpecialFrameIndex = frame,
            SpecialTickCounter = tick,
        };
        return true;
    }

    internal void InterruptNativeSarcophagusSpecialStand(AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsSarcophagusActor(target.EntityId))
            return;
        var entry = nativeSarcophagiBySlot.Values.Single(value => value.LinkedMonsterEntityId == target.EntityId);
        if (entry.ActorState != DiabloNativeSarcophagusActorState.Emerging)
            return;
        nativeSarcophagiBySlot[entry.ObjectSlot] = entry with {
            ActorState = DiabloNativeSarcophagusActorState.Active,
            SpecialFrameIndex = 0,
            SpecialTickCounter = 0,
        };
    }

    internal void CompleteNativeSarcophagusActorDeath(uint entityId)
    {
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsSarcophagusActor(entityId))
            return;
        var entry = nativeSarcophagiBySlot.Values.Single(value => value.LinkedMonsterEntityId == entityId);
        if (entry.ActorState is not (DiabloNativeSarcophagusActorState.Emerging or DiabloNativeSarcophagusActorState.Active))
            throw new InvalidDataException("A native sarcophagus actor reached corpse conversion from an invalid lifecycle state.");
        nativeSarcophagiBySlot[entry.ObjectSlot] = entry with {
            ActorState = DiabloNativeSarcophagusActorState.Dead,
            SpecialFrameIndex = 0,
            SpecialTickCounter = 0,
        };
    }

    internal void AdvanceNativeSarcophagusAnimations(uint levelId)
    {
        if (nativePopulationPolicy is null || levelId != nativePopulationPolicy.LevelId)
            return;
        foreach (var initialEntry in nativeSarcophagiBySlot.Values.Where(value => value.Opened).OrderBy(value => value.ObjectSlot).ToArray()) {
            var entry = initialEntry;
            if (entry.AnimationFlag == 0)
                continue;
            if (entry.AnimationFrame == entry.InitialAnimationLength) {
                entry = entry with { AnimationFlag = 0 };
            } else {
                var count = entry.AnimationCount + 1;
                var frame = entry.AnimationFrame;
                if (count >= entry.AnimationDelay) {
                    count = 0;
                    frame++;
                    if (frame > entry.InitialAnimationLength)
                        frame = 1;
                }
                entry = entry with { AnimationCount = count, AnimationFrame = frame };
            }
            nativeSarcophagiBySlot[entry.ObjectSlot] = entry;
            if (!Objects.TryGetValue(entry.EntityId, out var value))
                throw new InvalidDataException("An open native sarcophagus lost its object projection.");
            value.AnimationFrame = checked((int)entry.AnimationFrame);
            nativePopulationPolicy.ValidateObject(value);
        }
    }

    internal void ValidateNativeSarcophagusState(AuthoritativeCathedralNativePopulationPolicy policy)
    {
        if (nativePopulationPolicy != policy)
            throw new InvalidOperationException("Native sarcophagus validation requires the owning population policy.");
        var expected = policy.CreateInitialNativeSarcophagusStates();
        if (nativeSarcophagiBySlot.Count != expected.Count)
            throw new InvalidDataException("The native population lost a supported sarcophagus lifecycle record.");
        foreach (var initial in expected) {
            if (!nativeSarcophagiBySlot.TryGetValue(initial.ObjectSlot, out var state))
                throw new InvalidDataException("The native population omitted a supported sarcophagus lifecycle record.");
            state.ValidateShape();
            if (state.EntityId != initial.EntityId || state.NativeTypeId != initial.NativeTypeId
                || state.InitialPositionX != initial.InitialPositionX
                || state.InitialPositionY != initial.InitialPositionY
                || state.RandomSeed != initial.RandomSeed || state.Var1 != initial.Var1 || state.Var2 != initial.Var2
                || state.LinkedMonsterSlot != initial.LinkedMonsterSlot
                || state.LinkedMonsterEntityId != initial.LinkedMonsterEntityId
                || state.LinkedMonsterTypeId != initial.LinkedMonsterTypeId
                || !Objects.TryGetValue(state.EntityId, out var value)
                || value.LevelId != policy.LevelId || value.PositionX != state.InitialPositionX
                || value.PositionY != state.InitialPositionY || value.Activated != state.Opened
                || value.AnimationFrame != state.AnimationFrame)
                throw new InvalidDataException("Native sarcophagus lifecycle state conflicts with its exact object projection.");
            policy.ValidateObject(value);
            if (state.ActorState is DiabloNativeSarcophagusActorState.Emerging or DiabloNativeSarcophagusActorState.Active) {
                var reservation = policy.GetSarcophagusReservation(state.ObjectSlot);
                var expectedTarget = policy.CreateSarcophagusTarget(
                    state.LinkedMonsterSlot, state.SpawnPositionX, state.SpawnPositionY, state.SpawnDirection);
                if (!CombatTargets.TryGetValue(state.LinkedMonsterEntityId, out var target)
                    || target.NativeInitialization != reservation
                    || target.NativeInitialization?.Binding.NativeTypeId != state.LinkedMonsterTypeId
                    || target.LevelId != policy.LevelId || target.IsPlayerMinion
                    || !string.Equals(target.NativeAi, expectedTarget.NativeAi, StringComparison.Ordinal))
                    throw new InvalidDataException("A spawned native sarcophagus actor lost its exact live reservation or trusted AI classification.");
                if (state.ActorState == DiabloNativeSarcophagusActorState.Emerging
                    && (target.PositionX != state.SpawnPositionX || target.PositionY != state.SpawnPositionY
                        || target.FuturePositionX != state.SpawnPositionX || target.FuturePositionY != state.SpawnPositionY
                        || target.IsMoving || target.HitPointsQ6 <= 0))
                    throw new InvalidDataException("A native SpecialStand actor moved or changed life before emergence completed.");
                if (state.ActorState == DiabloNativeSarcophagusActorState.Emerging) {
                    var facts = policy.GetSarcophagusOperationFacts(state.ObjectSlot);
                    if (facts.NativeSpecialFrameCount <= 0
                        || state.SpecialFrameIndex >= facts.NativeSpecialFrameCount
                        || state.SpecialTickCounter >= Math.Max(1, facts.NativeSpecialTicksPerFrame))
                        throw new InvalidDataException("A native SpecialStand cursor is outside its imported animation.");
                }
            } else if (state.ActorState == DiabloNativeSarcophagusActorState.Dead) {
                if (CombatTargets.ContainsKey(state.LinkedMonsterEntityId))
                    throw new InvalidDataException("A dead sarcophagus reservation still has a live combat target.");
                if (!Corpses.TryGetValue(state.LinkedMonsterEntityId, out var corpse)
                    || corpse.LevelId != policy.LevelId)
                    throw new InvalidDataException("A dead sarcophagus actor lost its retained corpse disposition.");
            } else if (state.LinkedMonsterEntityId != 0
                && (CombatTargets.ContainsKey(state.LinkedMonsterEntityId)
                    || WorldItems.ContainsKey(state.LinkedMonsterEntityId)
                    || Objects.ContainsKey(state.LinkedMonsterEntityId)
                    || Projectiles.ContainsKey(state.LinkedMonsterEntityId)
                    || Portals.ContainsKey(state.LinkedMonsterEntityId)
                    || Corpses.ContainsKey(state.LinkedMonsterEntityId))) {
                throw new InvalidDataException("An unopened or unspawned sarcophagus reservation was reused.");
            }
        }
    }

    private bool NativeSarcophagusTileAvailable(
        uint levelId,
        int x,
        int y,
        AuthoritativeCathedralItemOccupancyContext occupancy)
    {
        if (!ContainsPosition(levelId, x, y))
            return false;
        var cell = y * AuthoritativeCathedralThemeRoomState.Width + x;
        if (occupancy.PlayerRefs[cell] != 0 || occupancy.MonsterRefs[cell] != 0
            || occupancy.CurrentSolidTiles[cell])
            return false;
        var objectReference = occupancy.ObjectRefs[cell];
        if (objectReference == 0)
            return true;
        var objectSlot = Math.Abs(objectReference) - 1;
        return objectSlot >= 0 && objectSlot < occupancy.CurrentObjectSolid.Count
            && !occupancy.CurrentObjectSolid[objectSlot];
    }

    private static int SpawnAvailabilityIndex(int originX, int originY, int x, int y)
    {
        var offset = (x - originX, y - originY);
        return offset switch {
            (0, 0) => 0,
            (-1, -1) => 1,
            (0, -1) => 2,
            (1, -1) => 3,
            (-1, 0) => 4,
            (1, 0) => 5,
            (-1, 1) => 6,
            (0, 1) => 7,
            (1, 1) => 8,
            _ => -1,
        };
    }

    private static bool IsNativeSpawnDirection(int originX, int originY, int x, int y, int facing)
    {
        var direction = DirectionFromSarcophagus(originX, originY, x, y);
        return direction >= 0 && facing == (direction + 4) % 8;
    }

    private static bool IsValidSarcophagusSpawnTarget(
        AuthoritativeCombatTarget target,
        AuthoritativeNativeMonsterInitialization owner,
        AuthoritativeCathedralSarcophagusSpawnPlan spawn,
        uint levelId)
        => target.EntityId == spawn.EntityId && target.MonsterId != 0
            && target.LevelId == levelId && target.NativeInitialization == owner
            && target.NativeInitialization?.Binding.NativeSlot == spawn.NativeMonsterSlot
            && target.NativeInitialization.Binding.NativeTypeId == spawn.NativeTypeId
            && target.PositionX == spawn.PositionX && target.PositionY == spawn.PositionY
            && target.FuturePositionX == spawn.PositionX && target.FuturePositionY == spawn.PositionY

            && !target.IsMoving && target.HitPointsQ6 > 0 && !target.IsPlayerMinion
            && target.NativeMeleeCursor is null && target.NativeWalkCursor is null;

    private static int DirectionFromSarcophagus(int originX, int originY, int destinationX, int destinationY)
    {
        var dx = destinationX - originX;
        var dy = destinationY - originY;
        return (dx, dy) switch {
            (0, -1) => 5,
            (1, -1) => 6,
            (1, 0) => 7,
            (-1, -1) => 4,
            (1, 1) => 0,
            (-1, 0) => 3,
            (-1, 1) => 2,
            (0, 1) => 1,
            _ => -1,
        };
    }
}
