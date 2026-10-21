namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private readonly Dictionary<uint, AuthoritativeNativeCatacombsRangedAction> nativeCatacombsRangedActions = [];
    private readonly HashSet<uint> nativeCatacombsRangedConfiguredLevels = [];
    private readonly Dictionary<uint, AuthoritativeMonsterMissileCatalog> nativeCatacombsRangedMissileCatalogs = [];

    internal IReadOnlyList<AuthoritativeNativeCatacombsRangedAction> CaptureNativeCatacombsRangedActions()
        => CaptureNativeCatacombsRangedActionsCore(null);

    internal IReadOnlyList<AuthoritativeNativeCatacombsRangedAction> CaptureNativeCatacombsRangedActions(
        uint levelId)
        => CaptureNativeCatacombsRangedActionsCore(levelId);

    private IReadOnlyList<AuthoritativeNativeCatacombsRangedAction> CaptureNativeCatacombsRangedActionsCore(
        uint? levelId)
    {
        var values = nativeCatacombsRangedActions.Values
            .Where(action => levelId is null || action.LevelId == levelId)
            .OrderBy(action => action.LevelId)
            .ThenBy(action => action.NativeSlot)
            .ToArray();
        foreach (var action in values) {
            action.ValidateShape();
            if (!CombatTargets.TryGetValue(action.EntityId, out var target))
                throw new InvalidDataException("A pending Catacombs ranged action lost its live actor before capture.");
            var bindings = RequireNativeCatacombsRangedBindings(target);
            action.ValidateTarget(target, bindings.Fingerprint);
            ValidateNativeCatacombsRangedActor(target, bindings);
            action.ValidateMissile(ResolveNativeCatacombsRangedMissile(bindings, target, action));
        }
        return Array.AsReadOnly(values.Select(action => action with { }).ToArray());
    }

    internal void ConfigureNativeCatacombsRangedActions(
        AuthoritativeNativePopulationBindings bindings,
        AuthoritativeMonsterMissileCatalog missiles,
        IReadOnlyList<AuthoritativeNativeCatacombsRangedAction>? saved = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(missiles);
        var actual = saved ?? Array.Empty<AuthoritativeNativeCatacombsRangedAction>();
        if (bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.Depth is < 5 or > 8
            || bindings.LevelId == 0
            || NativePopulationForLevel(bindings.LevelId) is not { } current
            || !ReferenceEquals(current, bindings)
            || actual.Count > AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || nativeCatacombsRangedConfiguredLevels.Contains(bindings.LevelId)
            || nativeCatacombsRangedMissileCatalogs.ContainsKey(bindings.LevelId))
            throw new InvalidDataException("Catacombs ranged actions require one exact, unconfigured floor owner.");

        ValidateNativeCatacombsRangedMissileCoverage(bindings, missiles);

        var ids = new HashSet<uint>();
        var slots = new HashSet<int>();
        var detached = new List<AuthoritativeNativeCatacombsRangedAction>(actual.Count);
        foreach (var action in actual) {
            ArgumentNullException.ThrowIfNull(action);
            action.ValidateShape();
            if (action.LevelId != bindings.LevelId
                || action.PopulationFingerprint != bindings.Fingerprint
                || action.MetadataFingerprint != bindings.MonsterMetadataFingerprint
                || !ids.Add(action.EntityId)
                || !slots.Add(action.NativeSlot)
                || nativeCatacombsRangedActions.ContainsKey(action.EntityId)
                || !CombatTargets.TryGetValue(action.EntityId, out var target))
                throw new InvalidDataException("A saved Catacombs ranged action changed its floor owner or actor order.");
            if (!missiles.TryGet(target.NativeAi ?? string.Empty, target.MonsterId, out var missile)
                || missile.NativeAi != target.NativeAi
                || missile.MissileId != action.MissileId)
                throw new InvalidDataException("A saved Catacombs ranged action is detached from its configured missile row.");
            action.ValidateTarget(target, bindings.Fingerprint);
            ValidateNativeCatacombsRangedActor(target, bindings);
            action.ValidateMissile(missile);
            detached.Add(action with { });
        }

        foreach (var action in detached)
            nativeCatacombsRangedActions.Add(action.EntityId, action);
        nativeCatacombsRangedMissileCatalogs.Add(bindings.LevelId, missiles);
        nativeCatacombsRangedConfiguredLevels.Add(bindings.LevelId);
    }

    /** Checks the complete owner/action relationship before admission, capture, or restore. */
    internal void ValidateNativeCatacombsRangedActions(AuthoritativeNativePopulationBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.Depth is < 5 or > 8
            || NativePopulationForLevel(bindings.LevelId) is not { } current
            || !ReferenceEquals(current, bindings)
            || !nativeCatacombsRangedConfiguredLevels.Contains(bindings.LevelId)
            || !nativeCatacombsRangedMissileCatalogs.ContainsKey(bindings.LevelId))
            throw new InvalidDataException("Catacombs ranged action state is not configured for this exact floor owner.");

        if (!nativeCatacombsRangedConfiguredLevels.SetEquals(nativeCatacombsRangedMissileCatalogs.Keys)
            || nativeCatacombsRangedConfiguredLevels.Any(levelId =>
                NativePopulationForLevel(levelId) is not { Family: AuthoritativeNativePopulationFamily.Catacombs }))
            throw new InvalidDataException("Catacombs ranged configuration contains an orphan floor or missile catalog.");

        foreach (var action in nativeCatacombsRangedActions.Values) {
            var actionBindings = NativePopulationForLevel(action.LevelId);
            if (actionBindings is not { Family: AuthoritativeNativePopulationFamily.Catacombs }
                || actionBindings.Depth is < 5 or > 8
                || actionBindings.LevelId != action.LevelId
                || !nativeCatacombsRangedConfiguredLevels.Contains(action.LevelId)
                || !CombatTargets.TryGetValue(action.EntityId, out var target))
                throw new InvalidDataException("A pending Catacombs ranged action is detached from its live floor owner or combat target.");
            action.ValidateTarget(target, actionBindings.Fingerprint);
            ValidateNativeCatacombsRangedActor(target, actionBindings);
            action.ValidateMissile(ResolveNativeCatacombsRangedMissile(actionBindings, target, action));
        }
    }

    internal bool HasNativeCatacombsRangedAction(uint entityId)
        => nativeCatacombsRangedActions.ContainsKey(entityId);

    internal bool TryGetNativeCatacombsRangedAction(
        AuthoritativeCombatTarget target,
        out AuthoritativeNativeCatacombsRangedAction action)
    {
        ArgumentNullException.ThrowIfNull(target);
        action = null!;
        if (!nativeCatacombsRangedActions.TryGetValue(target.EntityId, out var current))
            return false;
        if (!CombatTargets.TryGetValue(target.EntityId, out var liveTarget) || !ReferenceEquals(target, liveTarget))
            throw new InvalidDataException("A Catacombs ranged action cannot advance a detached target clone.");
        var bindings = RequireNativeCatacombsRangedBindings(target);
        current.ValidateTarget(target, bindings.Fingerprint);
        ValidateNativeCatacombsRangedActor(target, bindings);
        current.ValidateMissile(ResolveNativeCatacombsRangedMissile(bindings, target, current));
        action = current with { };
        return true;
    }

    internal void StartNativeCatacombsRangedAction(
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile,
        AuthoritativePlayerState victim,
        uint attackSeed)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(missile);
        ArgumentNullException.ThrowIfNull(victim);
        var bindings = RequireNativeCatacombsRangedBindings(target);
        if (!nativeCatacombsRangedConfiguredLevels.Contains(bindings.LevelId)
            || !CombatTargets.TryGetValue(target.EntityId, out var currentTarget)
            || !ReferenceEquals(target, currentTarget)
            || nativeCatacombsRangedActions.ContainsKey(target.EntityId))
            throw new InvalidOperationException("A Catacombs ranged action requires an active, configured actor without another action.");
        ValidateNativeCatacombsRangedActor(target, bindings);
        ValidateNativeCatacombsRangedMissile(bindings, target, missile);
        if (victim.EntityId == 0 || victim.LevelId != target.LevelId)
            throw new InvalidDataException("A Catacombs ranged action requires a same-floor target.");

        var action = AuthoritativeNativeCatacombsRangedAction.Create(
            target, missile, victim.EntityId, victim.FuturePositionX, victim.FuturePositionY,
            attackSeed, bindings.Fingerprint);
        action.ValidateMissile(missile);
        nativeCatacombsRangedActions.Add(target.EntityId, action);
    }

    internal AuthoritativeNativeCatacombsRangedActionStep AdvanceNativeCatacombsRangedAction(
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile,
        int? targetFutureX,
        int? targetFutureY)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(missile);
        if (targetFutureX.HasValue != targetFutureY.HasValue)
            throw new InvalidDataException("A Catacombs ranged target endpoint must provide both coordinates or neither.");
        if (!TryGetNativeCatacombsRangedAction(target, out var action))
            throw new InvalidOperationException("No Catacombs ranged action is active for this actor.");
        var bindings = RequireNativeCatacombsRangedBindings(target);
        ValidateNativeCatacombsRangedMissile(bindings, target, missile);
        if (targetFutureX is { } x && targetFutureY is { } y)
            action = action with { TargetX = x, TargetY = y };
        action.ValidateShape();
        var step = action.Advance();
        if (step.Completed)
            nativeCatacombsRangedActions.Remove(target.EntityId);
        else
            nativeCatacombsRangedActions[target.EntityId] = step.Action;
        return step;
    }

    internal void CancelNativeCatacombsRangedAction(uint entityId)
        => nativeCatacombsRangedActions.Remove(entityId);

    /** Returns whether a C2 initial SpecialStand cursor still owns this tick and whether it ended naturally. */
    internal (bool Active, bool CompletedNaturally) AdvanceNativeCatacombsInitialSpecialStand(
        AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.NativeInitialization is not { CatacombsDepth: >= 5 and <= 8 }
            || !nativeInitialSpecialStand.TryGetValue(target.EntityId, out var before)
            || before.Completed)
            return (false, false);
        if (target.IsPetrified && target.HitPointsQ6 > 0)
            return (true, false);

        var active = AdvanceNativeInitialSpecialStand(target);
        var completedNaturally = !active
            && nativeInitialSpecialStand.TryGetValue(target.EntityId, out var after)
            && after.Completed
            && target.HitPointsQ6 > 0
            && !target.IsMoving
            && target.NativeMeleeCursor is not { HitRecovery: true };
        return (active, completedNaturally);
    }

    /** Advances a generated Catacombs sarcophagus or barrel skeleton emergence. */
    internal (bool Active, bool CompletedNaturally) AdvanceNativeCatacombsObjectEmergence(
        AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var sarcophagus = nativeSarcophagiByEntityId.Values.FirstOrDefault(value =>
            value.LinkedMonsterEntityId == target.EntityId
            && value.ActorState == DiabloNativeSarcophagusActorState.Emerging);
        if (sarcophagus is not null) {
            var active = AdvanceNativeSarcophagusSpecialStand(target);
            var completed = !active
                && nativeSarcophagiByEntityId.TryGetValue(sarcophagus.EntityId, out var current)
                && current.ActorState == DiabloNativeSarcophagusActorState.Active
                && IsNativeCatacombsEmergenceCompletion(target);
            return (active, completed);
        }

        var barrel = nativeBarrelsByEntityId.Values.FirstOrDefault(value =>
            value.LinkedMonsterEntityId == target.EntityId
            && value.ActorState == DiabloNativeBarrelActorState.Emerging);
        if (barrel is not null) {
            var active = AdvanceNativeBarrelSpecialStand(target);
            var completed = !active
                && nativeBarrelsByEntityId.TryGetValue(barrel.EntityId, out var current)
                && current.ActorState == DiabloNativeBarrelActorState.Active
                && IsNativeCatacombsEmergenceCompletion(target);
            return (active, completed);
        }

        return (false, false);
    }

    private static bool IsNativeCatacombsEmergenceCompletion(AuthoritativeCombatTarget target)
        => target.HitPointsQ6 > 0
            && !target.IsPetrified
            && !target.IsMoving
            && target.NativeMeleeCursor is not { HitRecovery: true };

    private AuthoritativeNativePopulationBindings RequireNativeCatacombsRangedBindings(
        AuthoritativeCombatTarget target)
    {
        var bindings = NativePopulationForEntity(target.EntityId);
        if (bindings is null
            || bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.Depth is < 5 or > 8
            || bindings.LevelId != target.LevelId
            || target.NativeInitialization is not { } initialization
            || initialization.CatacombsDepth != bindings.Depth
            || initialization.MetadataFingerprint != bindings.MonsterMetadataFingerprint)
            throw new InvalidDataException("A Catacombs ranged action has no matching admitted floor binding.");
        return bindings;
    }

    private AuthoritativeMonsterMissileDefinition ResolveNativeCatacombsRangedMissile(
        AuthoritativeNativePopulationBindings bindings,
        AuthoritativeCombatTarget target,
        AuthoritativeNativeCatacombsRangedAction action)
    {
        if (!nativeCatacombsRangedMissileCatalogs.TryGetValue(bindings.LevelId, out var catalog)
            || !catalog.TryGet(target.NativeAi ?? string.Empty, target.MonsterId, out var missile)
            || missile.NativeAi != target.NativeAi
            || missile.MissileId != action.MissileId)
            throw new InvalidDataException("A Catacombs ranged action has no matching floor missile catalog row.");
        return missile;
    }

    private void ValidateNativeCatacombsRangedMissile(
        AuthoritativeNativePopulationBindings bindings,
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile)
    {
        if (!nativeCatacombsRangedMissileCatalogs.TryGetValue(bindings.LevelId, out var catalog)
            || !catalog.TryGet(target.NativeAi ?? string.Empty, target.MonsterId, out var configured)
            || configured.NativeAi != target.NativeAi
            || AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(configured)
                != AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile))
            throw new InvalidDataException("A Catacombs ranged action changed its admitted missile catalog mapping.");
    }

    private void ValidateNativeCatacombsRangedActor(
        AuthoritativeCombatTarget target,
        AuthoritativeNativePopulationBindings bindings)
    {
        var isLiveActor = bindings.LiveNativeAis.TryGetValue(target.EntityId, out var nativeAi)
            && nativeAi == target.NativeAi;
        if ((!isLiveActor && !IsActiveNativeCatacombsReservedRangedSkeleton(target, bindings))
            || target.HitPointsQ6 <= 0
            || target.IsMoving
            || target.NativeMeleeCursor is not null
            || HasNativeCatacombsMonsterPhase(target.EntityId)
            || nativeInitialSpecialStand.TryGetValue(target.EntityId, out var emergence) && !emergence.Completed)
            throw new InvalidDataException("A pending Catacombs ranged action changed actor state or overlaps another native mode owner.");
    }

    private bool IsActiveNativeCatacombsReservedRangedSkeleton(
        AuthoritativeCombatTarget target,
        AuthoritativeNativePopulationBindings bindings)
    {
        if (target.NativeAi != "SkeletonRanged"
            || target.NativeInitialization is not { InitialState.Ai: 3 } initialization)
            return false;

        var barrel = CaptureNativeBarrels()?.SingleOrDefault(value => value.LinkedMonsterEntityId == target.EntityId);
        if (barrel is not null)
            return barrel.ActorState == DiabloNativeBarrelActorState.Active
                && bindings.GetNativeBarrelReservation(barrel.ObjectSlot) == initialization
                && bindings.IsNativeBarrelReservationSpawned(barrel.LinkedMonsterSlot, target.EntityId, this);

        var sarcophagus = CaptureNativeSarcophagi()?.SingleOrDefault(value =>
            value.LinkedMonsterEntityId == target.EntityId);
        return sarcophagus is not null
            && sarcophagus.ActorState == DiabloNativeSarcophagusActorState.Active
            && bindings.GetPreSpawnReservation(sarcophagus.ObjectSlot) == initialization
            && bindings.IsSarcophagusReservationSpawned(sarcophagus.LinkedMonsterSlot, target.EntityId, this);
    }

    private void ValidateNativeCatacombsRangedMissileCoverage(
        AuthoritativeNativePopulationBindings bindings,
        AuthoritativeMonsterMissileCatalog missiles)
    {
        foreach (var (entityId, nativeAi) in bindings.LiveNativeAis) {
            if (nativeAi is not ("SkeletonRanged" or "GoatRanged" or "Acid" or "Magma" or "AcidUnique"))
                continue;
            if (!CombatTargets.TryGetValue(entityId, out var target)
                || target.LevelId != bindings.LevelId
                || target.NativeAi != nativeAi
                || !missiles.TryGet(nativeAi, target.MonsterId, out var missile)
                || missile.NativeAi != nativeAi
                || (!AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(target, missile)
                    && !AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(target, missile)))
                throw new InvalidDataException("A live Catacombs ranged actor lacks its exact supported missile mapping.");
        }

        var linkedSkeletonReservations = bindings.CreateInitialNativeBarrelStates()
            .Where(value => value.LinkedMonsterEntityId != 0)
            .Select(value => bindings.GetNativeBarrelReservation(value.ObjectSlot))
            .Concat(bindings.CreateInitialNativeSarcophagusStates()
                .Where(value => value.LinkedMonsterEntityId != 0)
                .Select(value => bindings.GetPreSpawnReservation(value.ObjectSlot)))
            .DistinctBy(value => value.Binding.NativeSlot);
        foreach (var reservation in linkedSkeletonReservations.Where(value => value.InitialState.Ai == 3)) {
            if (!missiles.TryGet("SkeletonRanged", reservation.Binding.ContentMonsterId, out var missile)
                || missile.NativeAi != "SkeletonRanged"
                || missile.MissileId != "Arrow"
                || missile.AttackMode != AuthoritativeMonsterRangedAttackMode.Ranged
                || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.MonsterArrow
                || missile.SpellId != 0)
                throw new InvalidDataException("A reserved Catacombs SkeletonRanged actor lacks its exact Arrow mapping.");
        }
    }
}
