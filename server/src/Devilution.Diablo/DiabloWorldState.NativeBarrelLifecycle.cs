namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private Dictionary<uint, DiabloNativeBarrelState> nativeBarrelsByEntityId = [];

    internal bool HasNativeBarrelMutations => nativeBarrelsByEntityId.Values.Any(value => value.Broken);

    internal IReadOnlyList<DiabloNativeBarrelState>? CaptureNativeBarrels()
        => nativePopulationPolicy is null
            ? null
            : Array.AsReadOnly(nativeBarrelsByEntityId.Values.OrderBy(value => Objects[value.EntityId].LevelId).ThenBy(value => value.ObjectSlot).ToArray());

    internal DiabloNativeBarrelState? GetNativeBarrelState(int objectSlot)
        => nativePopulationPolicy is null ? null
            : GetNativeBarrelState(nativePopulationPolicy.LevelId, objectSlot);

    internal DiabloNativeBarrelState? GetNativeBarrelState(uint levelId, int objectSlot)
        => nativeBarrelsByEntityId.Values.SingleOrDefault(value => value.ObjectSlot == objectSlot
            && Objects.TryGetValue(value.EntityId, out var owner) && owner.LevelId == levelId);

    internal void ConfigureNativeBarrels(
        AuthoritativeNativePopulationBindings policy,
        int formatVersion,
        IReadOnlyList<DiabloNativeBarrelState>? savedStates)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!ReferenceEquals(NativePopulationForLevel(policy.LevelId), policy))
            throw new InvalidOperationException("Native barrel state requires its configured immutable population owner.");

        var initial = policy.CreateInitialNativeBarrelStates();
        if (formatVersion >= DiabloCohortState.NativeBarrelFormatVersion) {
            if (savedStates is null)
                throw new InvalidDataException("Native barrel cohorts require complete object lifecycle records.");
            if (savedStates.Count != initial.Count)
                throw new InvalidDataException("Native barrel cohort omits a supported generated object.");
            var restored = new Dictionary<uint, DiabloNativeBarrelState>();
            for (var index = 0; index < initial.Count; index++) {
                var actual = savedStates[index];
                actual.ValidateShape();
                if (!SameNativeBarrelIdentity(actual, initial[index])
                    || !restored.TryAdd(actual.EntityId, actual))
                    throw new InvalidDataException("Native barrel lifecycle identity differs from the exact generated object and reservation.");
            }
            foreach (var entry in restored)
                nativeBarrelsByEntityId.Add(entry.Key, entry.Value);
        } else {
            if (savedStates is not null)
                throw new InvalidDataException("Older native cohorts cannot carry barrel lifecycle state.");
            foreach (var entry in initial)
                nativeBarrelsByEntityId.Add(entry.EntityId, entry);
        }

        ValidateNativeBarrelState(policy);
    }

    internal bool CanBreakNativeBarrel(AuthoritativeWorldObject value)
    {
        var nativePopulationPolicy = NativePopulationForLevel(value.LevelId);
        if (nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId
            || !Objects.TryGetValue(value.EntityId, out var current) || !ReferenceEquals(current, value)
            || !nativePopulationPolicy.TryGetNativeBarrelFacts(value, out var facts))
            return false;
        return nativeBarrelsByEntityId.TryGetValue(facts.EntityId, out var state) && !state.Broken;
    }

    internal bool TryGetNativeBarrelFacts(
        AuthoritativeWorldObject value,
        out AuthoritativeCathedralNativeBarrelFacts facts)
    {
        facts = null!;
        return CanBreakNativeBarrel(value)
            && NativePopulationForLevel(value.LevelId)!.TryGetNativeBarrelFacts(value, out facts);
    }

    internal IReadOnlyList<bool> GetNativeBarrelSpawnAvailability(
        AuthoritativeWorldObject barrel,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(barrel);
        ArgumentNullException.ThrowIfNull(participants);
        if (!TryGetNativeBarrelFacts(barrel, out var facts) || facts.LinkedMonster is null)
            return Array.AsReadOnly(new bool[9]);
        if (cathedralItems is null)
            throw new InvalidDataException("Native barrel placement requires its item-world owner.");

        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, facts.LevelId, participants);
        var offsets = new (int X, int Y)[] {
            (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
        };
        return Array.AsReadOnly(offsets.Select(offset => NativeBarrelTileAvailable(
            facts.LevelId,
            checked(facts.PositionX + offset.X),
            checked(facts.PositionY + offset.Y),
            facts.ObjectSlot,
            occupancy)).ToArray());
    }

    internal bool CanCommitNativeBarrelBreak(
        AuthoritativeWorldObject value,
        AuthoritativeCathedralBarrelOperationResult operation,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var nativePopulationPolicy = NativePopulationForLevel(value.LevelId);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(participants);
        if (!TryGetNativeBarrelFacts(value, out var facts)
            || operation.IsExplosive != facts.IsExplosive
            || operation.OpenedMutation != AuthoritativeCathedralBarrelOpenedMutation.Native)
            return false;

        var state = nativeBarrelsByEntityId[facts.EntityId];
        if (state.Broken || state.ActorState is not (DiabloNativeBarrelActorState.None
            or DiabloNativeBarrelActorState.Reserved))
            return false;
        var shouldLoot = !facts.IsExplosive && facts.Var2 <= 1;
        if (shouldLoot != (operation.Loot is not null))
            return false;
        var shouldSpawn = !facts.IsExplosive && facts.Var2 >= 8 && facts.Var4 >= 0;
        var available = GetNativeBarrelSpawnAvailability(value, participants);
        if (!shouldSpawn)
            return operation.Spawn is null;
        if (operation.Spawn is not { } spawn)
            return !available.Any(tile => tile);

        var index = SpawnAvailabilityIndex(
            facts.PositionX, facts.PositionY, spawn.PositionX, spawn.PositionY);
        if (facts.LinkedMonster is not { } linked
            || index < 0 || !available[index]
            || spawn.NativeMonsterSlot != linked.Binding.NativeSlot
            || spawn.EntityId != linked.Binding.EntityId
            || spawn.NativeTypeId != linked.Binding.NativeTypeId
            || spawn.SpecialFrameCount != facts.NativeSpecialFrameCount
            || spawn.SpecialTicksPerFrame != facts.NativeSpecialTicksPerFrame
            || spawn.IsCenterSpawn != (index == 0))
            return false;
        if (index == 0) {
            if (spawn.Facing != 1)
                return false;
        } else if (!IsNativeSpawnDirection(
                facts.PositionX, facts.PositionY, spawn.PositionX, spawn.PositionY, spawn.Facing)) {
            return false;
        }

        if (CombatTargets.ContainsKey(spawn.EntityId) || WorldItems.ContainsKey(spawn.EntityId)
            || Objects.ContainsKey(spawn.EntityId) || Projectiles.ContainsKey(spawn.EntityId)
            || Portals.ContainsKey(spawn.EntityId) || Corpses.ContainsKey(spawn.EntityId))
            return false;

        var owner = nativePopulationPolicy!.GetNativeBarrelReservation(facts.ObjectSlot);
        if (owner != linked || owner.Binding.EntityId != spawn.EntityId
            || owner.Binding.NativeSlot != spawn.NativeMonsterSlot
            || owner.Binding.NativeTypeId != spawn.NativeTypeId)
            return false;
        var target = nativePopulationPolicy.CreateNativeBarrelTarget(
            spawn.NativeMonsterSlot, spawn.PositionX, spawn.PositionY, spawn.Facing);
        return IsValidNativeBarrelSpawnTarget(target, owner, spawn, facts.LevelId);
    }

    internal bool TryCommitNativeBarrelBreak(
        AuthoritativeWorldObject value,
        AuthoritativeCathedralBarrelOperationResult operation,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var nativePopulationPolicy = NativePopulationForLevel(value.LevelId);
        if (!CanCommitNativeBarrelBreak(value, operation, participants))
            return false;
        var facts = nativePopulationPolicy!.TryGetNativeBarrelFacts(value, out var currentFacts)
            ? currentFacts
            : throw new InvalidOperationException("A prevalidated barrel lost its immutable owner facts.");
        var current = nativeBarrelsByEntityId[facts.EntityId];
        AuthoritativeCombatTarget? preparedTarget = null;
        var next = current with {
            Broken = true,
            AnimationFlag = operation.OpenedMutation.AnimationFlag,
            AnimationFrame = operation.OpenedMutation.AnimationFrame,
            AnimationDelay = operation.OpenedMutation.AnimationDelay,
            SolidFlag = operation.OpenedMutation.SolidFlag,
            MissilePassable = operation.OpenedMutation.MissilePassable,
            BreakFlag = operation.OpenedMutation.BreakFlag,
            SelectionRegion = operation.OpenedMutation.SelectionRegion,
            PreFlag = operation.OpenedMutation.PreFlag,
        };
        if (operation.Spawn is { } spawn) {
            var owner = nativePopulationPolicy.GetNativeBarrelReservation(facts.ObjectSlot);
            preparedTarget = nativePopulationPolicy.CreateNativeBarrelTarget(
                spawn.NativeMonsterSlot, spawn.PositionX, spawn.PositionY, spawn.Facing);
            if (!IsValidNativeBarrelSpawnTarget(preparedTarget, owner, spawn, facts.LevelId))
                throw new InvalidOperationException("A prevalidated native barrel lost its exact linked actor.");
            next = next with {
                ActorState = DiabloNativeBarrelActorState.Emerging,
                SpawnPositionX = spawn.PositionX,
                SpawnPositionY = spawn.PositionY,
                SpawnDirection = spawn.Facing,
                SpecialFrameIndex = 0,
                SpecialTickCounter = 0,
            };
        }
        next.ValidateShape();
        var projection = DiabloSimulation.CaptureObject(value) with {
            Activated = true,
            AnimationFrame = checked((int)next.AnimationFrame),
            MissilePassable = next.MissilePassable,
        };
        nativePopulationPolicy.ValidateObjectProjection(projection);
        if (preparedTarget is not null && !CombatTargets.TryAdd(preparedTarget.EntityId, preparedTarget))
            throw new InvalidOperationException("A prevalidated barrel could not materialize its exact reserved actor.");
        nativeBarrelsByEntityId[facts.EntityId] = next;
        value.Activated = true;
        value.AnimationFrame = checked((int)next.AnimationFrame);
        value.MissilePassable = next.MissilePassable;
        ValidateNativeBarrelState(nativePopulationPolicy);
        return true;
    }

    internal bool AdvanceNativeBarrelSpecialStand(AuthoritativeCombatTarget target)
    {
        var nativePopulationPolicy = NativePopulationForLevel(target.LevelId);
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsNativeBarrelActor(target.EntityId))
            return false;
        var entry = nativeBarrelsByEntityId.Values.Single(value => value.LinkedMonsterEntityId == target.EntityId);
        if (entry.ActorState != DiabloNativeBarrelActorState.Emerging)
            return false;
        if (target.IsPetrified && target.HitPointsQ6 > 0)
            return true; // Stone Curse pauses and later resumes the source SpecialStand.
        if (target.HitPointsQ6 <= 0 || target.NativeMeleeCursor is { HitRecovery: true }) {
            InterruptNativeBarrelSpecialStand(target);
            return false;
        }
        var facts = nativePopulationPolicy.GetNativeBarrelFacts(entry.ObjectSlot);
        if (entry.SpecialFrameIndex >= facts.NativeSpecialFrameCount - 1) {
            nativeBarrelsByEntityId[entry.EntityId] = entry with {
                ActorState = DiabloNativeBarrelActorState.Active,
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
        nativeBarrelsByEntityId[entry.EntityId] = entry with {
            SpecialFrameIndex = frame,
            SpecialTickCounter = tick,
        };
        return true;
    }

    /** A hard hit or death interrupts the one-shot native emergence immediately. */
    internal void InterruptNativeBarrelSpecialStand(AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var nativePopulationPolicy = NativePopulationForLevel(target.LevelId);
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsNativeBarrelActor(target.EntityId))
            return;
        var entry = nativeBarrelsByEntityId.Values.Single(value => value.LinkedMonsterEntityId == target.EntityId);
        if (entry.ActorState != DiabloNativeBarrelActorState.Emerging)
            return;
        nativeBarrelsByEntityId[entry.EntityId] = entry with {
            ActorState = DiabloNativeBarrelActorState.Active,
            SpecialFrameIndex = 0,
            SpecialTickCounter = 0,
        };
    }

    internal void CompleteNativeBarrelActorDeath(uint entityId)
    {
        var nativePopulationPolicy = NativePopulationForEntity(entityId);
        if (nativePopulationPolicy is null || !nativePopulationPolicy.OwnsNativeBarrelActor(entityId))
            return;
        var entry = nativeBarrelsByEntityId.Values.Single(value => value.LinkedMonsterEntityId == entityId);
        if (entry.ActorState is not (DiabloNativeBarrelActorState.Emerging or DiabloNativeBarrelActorState.Active))
            throw new InvalidDataException("A native barrel actor reached corpse conversion from an invalid lifecycle state.");
        nativeBarrelsByEntityId[entry.EntityId] = entry with {
            ActorState = DiabloNativeBarrelActorState.Dead,
            SpecialFrameIndex = 0,
            SpecialTickCounter = 0,
        };
    }

    internal void AdvanceNativeBarrelAnimations(uint levelId)
    {
        var nativePopulationPolicy = NativePopulationForLevel(levelId);
        if (nativePopulationPolicy is null || levelId != nativePopulationPolicy.LevelId)
            return;
        foreach (var initialEntry in nativeBarrelsByEntityId.Values.Where(value => value.Broken && Objects[value.EntityId].LevelId == levelId)
                     .OrderBy(value => value.ObjectSlot).ToArray()) {
            var entry = initialEntry;
            if (entry.AnimationFlag == 0)
                continue;
            if (entry.AnimationFrame == entry.InitialAnimationLength) {
                entry = entry with { AnimationDelay = 1000, AnimationCount = 0 };
            }
            var count = entry.AnimationCount + 1;
            var frame = entry.AnimationFrame;
            if (count >= entry.AnimationDelay) {
                count = 0;
                frame++;
                if (frame > entry.InitialAnimationLength)
                    frame = 1;
            }
            entry = entry with { AnimationCount = count, AnimationFrame = frame };
            entry.ValidateShape();
            nativeBarrelsByEntityId[entry.EntityId] = entry;
            if (!Objects.TryGetValue(entry.EntityId, out var value))
                throw new InvalidDataException("A broken native barrel lost its object projection.");
            value.AnimationFrame = checked((int)entry.AnimationFrame);
            nativePopulationPolicy.ValidateObject(value);
        }
    }

    internal void ValidateNativeBarrelState(AuthoritativeNativePopulationBindings policy)
    {
        if (!ReferenceEquals(NativePopulationForLevel(policy.LevelId), policy))
            throw new InvalidOperationException("Native barrel validation requires the owning population policy.");
        var expected = policy.CreateInitialNativeBarrelStates();
        if (nativeBarrelsByEntityId.Values.Count(state => Objects.TryGetValue(state.EntityId, out var owner)
                && owner.LevelId == policy.LevelId) != expected.Count)
            throw new InvalidDataException("The native population lost a supported barrel lifecycle record.");
        foreach (var initial in expected) {
            if (!nativeBarrelsByEntityId.TryGetValue(initial.EntityId, out var state)
                || !SameNativeBarrelIdentity(state, initial))
                throw new InvalidDataException("The native population lost an exact generated barrel lifecycle identity.");
            state.ValidateShape();
            if (!Objects.TryGetValue(state.EntityId, out var value)
                || value.LevelId != policy.LevelId || value.PositionX != state.InitialPositionX
                || value.PositionY != state.InitialPositionY || value.Activated != state.Broken
                || value.AnimationFrame != state.AnimationFrame
                || value.MissilePassable != state.MissilePassable)
                throw new InvalidDataException("Native barrel lifecycle state conflicts with its object projection.");
            policy.ValidateObject(value);

            if (state.ActorState is DiabloNativeBarrelActorState.Emerging or DiabloNativeBarrelActorState.Active) {
                var reservation = policy.GetNativeBarrelReservation(state.ObjectSlot);
                var expectedTarget = policy.CreateNativeBarrelTarget(
                    state.LinkedMonsterSlot, state.SpawnPositionX, state.SpawnPositionY, state.SpawnDirection);
                if (!CombatTargets.TryGetValue(state.LinkedMonsterEntityId, out var target)
                    || target.NativeInitialization != reservation
                    || target.NativeInitialization?.Binding.NativeTypeId != state.LinkedMonsterTypeId
                    || target.LevelId != policy.LevelId || target.IsPlayerMinion
                    || !string.Equals(target.NativeAi, expectedTarget.NativeAi, StringComparison.Ordinal))
                    throw new InvalidDataException("A spawned native barrel actor lost its exact live reservation or trusted AI classification.");
                if (state.ActorState == DiabloNativeBarrelActorState.Emerging
                    && (target.PositionX != state.SpawnPositionX || target.PositionY != state.SpawnPositionY
                        || target.FuturePositionX != state.SpawnPositionX || target.FuturePositionY != state.SpawnPositionY
                        || target.IsMoving || target.HitPointsQ6 <= 0 || target.NativeMeleeCursor is not null))
                    throw new InvalidDataException("An emerging barrel skeleton moved or changed life before SpecialStand completed.");
                if (state.ActorState == DiabloNativeBarrelActorState.Emerging) {
                    var facts = policy.GetNativeBarrelFacts(state.ObjectSlot);
                    if (facts.NativeSpecialFrameCount <= 0
                        || state.SpecialFrameIndex >= facts.NativeSpecialFrameCount
                        || state.SpecialTickCounter >= Math.Max(1, facts.NativeSpecialTicksPerFrame))
                        throw new InvalidDataException("A barrel skeleton SpecialStand cursor is outside imported animation metadata.");
                }
            } else if (state.ActorState == DiabloNativeBarrelActorState.Dead) {
                if (CombatTargets.ContainsKey(state.LinkedMonsterEntityId)
                    || !Corpses.TryGetValue(state.LinkedMonsterEntityId, out var corpse)
                    || corpse.LevelId != policy.LevelId)
                    throw new InvalidDataException("A dead barrel reservation lost its retained corpse disposition.");
            } else if (state.LinkedMonsterEntityId != 0
                && (CombatTargets.ContainsKey(state.LinkedMonsterEntityId)
                    || WorldItems.ContainsKey(state.LinkedMonsterEntityId)
                    || Objects.ContainsKey(state.LinkedMonsterEntityId)
                    || Projectiles.ContainsKey(state.LinkedMonsterEntityId)
                    || Portals.ContainsKey(state.LinkedMonsterEntityId)
                    || Corpses.ContainsKey(state.LinkedMonsterEntityId))) {
                throw new InvalidDataException("An unspawned barrel reservation was reused.");
            }
        }
    }

    private bool NativeBarrelTileAvailable(
        uint levelId,
        int x,
        int y,
        int brokenObjectSlot,
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
        if (objectSlot == brokenObjectSlot)
            return true; // BreakBarrel clears solidity before ActivateSkeleton.
        return objectSlot >= 0 && objectSlot < occupancy.CurrentObjectSolid.Count
            && !occupancy.CurrentObjectSolid[objectSlot];
    }

    private static bool SameNativeBarrelIdentity(DiabloNativeBarrelState left, DiabloNativeBarrelState right)
        => left.ObjectSlot == right.ObjectSlot && left.EntityId == right.EntityId
            && left.NativeTypeId == right.NativeTypeId
            && left.InitialPositionX == right.InitialPositionX && left.InitialPositionY == right.InitialPositionY
            && left.RandomSeed == right.RandomSeed && left.Var1 == right.Var1 && left.Var2 == right.Var2
            && left.Var3 == right.Var3 && left.Var4 == right.Var4
            && left.InitialSelectionRegion == right.InitialSelectionRegion
            && left.InitialAnimationFrame == right.InitialAnimationFrame
            && left.InitialAnimationFlag == right.InitialAnimationFlag
            && left.InitialAnimationDelay == right.InitialAnimationDelay
            && left.InitialAnimationCount == right.InitialAnimationCount
            && left.InitialAnimationLength == right.InitialAnimationLength
            && left.InitialSolidFlag == right.InitialSolidFlag
            && left.InitialMissilePassable == right.InitialMissilePassable
            && left.InitialBreakFlag == right.InitialBreakFlag && left.InitialPreFlag == right.InitialPreFlag
            && left.TrapArmed == right.TrapArmed && left.HasLinkedTrap == right.HasLinkedTrap
            && left.LinkedMonsterSlot == right.LinkedMonsterSlot
            && left.LinkedMonsterEntityId == right.LinkedMonsterEntityId
            && left.LinkedMonsterTypeId == right.LinkedMonsterTypeId;

    private static bool IsValidNativeBarrelSpawnTarget(
        AuthoritativeCombatTarget target,
        AuthoritativeNativeMonsterInitialization owner,
        AuthoritativeCathedralBarrelSpawnPlan spawn,
        uint levelId)
        => target.EntityId == spawn.EntityId && target.MonsterId != 0
            && target.LevelId == levelId && target.NativeInitialization == owner
            && target.NativeInitialization?.Binding.NativeSlot == spawn.NativeMonsterSlot
            && target.NativeInitialization.Binding.NativeTypeId == spawn.NativeTypeId
            && target.PositionX == spawn.PositionX && target.PositionY == spawn.PositionY
            && target.FuturePositionX == spawn.PositionX && target.FuturePositionY == spawn.PositionY
            && target.Direction == spawn.Facing && !target.IsMoving
            && target.HitPointsQ6 > 0 && !target.IsPlayerMinion
            && target.NativeMeleeCursor is null && target.NativeWalkCursor is null;

    private int FindNativeObjectSlot(uint entityId)
        => NativePopulationForEntity(entityId) is { } policy
            && policy.TryGetNativeBarrelObjectSlot(entityId, out var slot)
                ? slot
                : throw new InvalidOperationException("A native barrel slot requires its population owner.");
}
