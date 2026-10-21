namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private readonly Dictionary<uint, AuthoritativeNativeCatacombsArrowState> nativeCatacombsArrows = [];

    internal IReadOnlyList<AuthoritativeNativeCatacombsArrowState> CaptureNativeCatacombsArrows()
        => CaptureNativeCatacombsArrowsCore(null);

    internal bool HasNativeCatacombsArrow(uint projectileEntityId)
        => nativeCatacombsArrows.ContainsKey(projectileEntityId);

    internal IReadOnlyList<AuthoritativeNativeCatacombsArrowState> CaptureNativeCatacombsArrows(uint levelId)
        => CaptureNativeCatacombsArrowsCore(levelId);

    private IReadOnlyList<AuthoritativeNativeCatacombsArrowState> CaptureNativeCatacombsArrowsCore(uint? levelId)
    {
        var values = nativeCatacombsArrows.Values
            .Where(value => levelId is null || value.LevelId == levelId)
            .OrderBy(value => value.LevelId)
            .ThenBy(value => value.ProjectileEntityId)
            .ToArray();
        var snapshots = new AuthoritativeNativeCatacombsArrowState[values.Length];
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            ValidateNativeCatacombsArrow(value);
            if (CombatTargets.TryGetValue(value.SourceEntityId, out var source)) {
                var bindings = NativePopulationForLevel(value.LevelId)
                    ?? throw new InvalidDataException("A live Catacombs Arrow source lost its floor owner.");
                value.ValidateSourceIdentity(source, bindings);
                // Snapshot the latest live facts without mutating the world's flight registry.
                value = value.RefreshSource(source);
                value.ValidateSource(source, bindings);
            }
            snapshots[index] = value with { };
        }
        return Array.AsReadOnly(snapshots);
    }

    internal void ValidateNativeCatacombsArrows(AuthoritativeNativePopulationBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.Depth is < 5 or > 8
            || NativePopulationForLevel(bindings.LevelId) is not { } current
            || !ReferenceEquals(current, bindings))
            throw new InvalidDataException("Native Catacombs Arrows require their exact admitted floor owner.");

        foreach (var arrow in nativeCatacombsArrows.Values.Where(value => value.LevelId == bindings.LevelId))
            ValidateNativeCatacombsArrow(arrow, bindings);

        foreach (var projectile in Projectiles.Values.Where(value => value.LevelId == bindings.LevelId
                     && value.Behavior == AuthoritativeProjectileBehavior.MonsterArrow)) {
            if (!nativeCatacombsArrows.TryGetValue(projectile.EntityId, out var arrow)
                || arrow.SourceEntityId != projectile.SourceEntityId)
                throw new InvalidDataException("A Catacombs MonsterArrow projectile has no exact private flight owner.");
        }
    }

    internal void RegisterNativeCatacombsArrow(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget source,
        AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile,
        int direction16)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(missile);
        var bindings = RequireNativeCatacombsRangedBindings(source);
        if (!nativeCatacombsRangedConfiguredLevels.Contains(bindings.LevelId)
            || nativePopulationParticipantClassId is not (0 or 2)
            || Projectiles.ContainsKey(projectile.EntityId)
            || nativeCatacombsArrows.ContainsKey(projectile.EntityId)
            || projectile.LevelId != bindings.LevelId
            || !nativeCatacombsRangedMissileCatalogs.TryGetValue(bindings.LevelId, out var configured)
            || !configured.TryGet(source.NativeAi ?? string.Empty, source.MonsterId, out var configuredMissile)
            || AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(configuredMissile)
                != AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile))
            throw new InvalidDataException("A native Catacombs Arrow cannot be registered outside its exact configured owner.");

        var state = AuthoritativeNativeCatacombsArrowState.Create(source, projectile, action, missile,
            bindings, checked((int)nativePopulationParticipantClassId.Value), direction16);
        ValidateNativeCatacombsArrow(state, bindings, projectileOverride: projectile,
            requireRegistered: false);
        Projectiles.Add(projectile.EntityId, projectile);
        nativeCatacombsArrows.Add(projectile.EntityId, state);
    }

    internal bool TryGetNativeCatacombsArrow(
        AuthoritativeProjectile projectile,
        out AuthoritativeNativeCatacombsArrowState state)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        state = null!;
        if (!nativeCatacombsArrows.TryGetValue(projectile.EntityId, out var current))
            return false;
        if (!Projectiles.TryGetValue(projectile.EntityId, out var liveProjectile)
            || !ReferenceEquals(projectile, liveProjectile))
            throw new InvalidDataException("A Catacombs Arrow cannot advance a detached projectile clone.");
        ValidateNativeCatacombsArrow(current);
        state = current with { };
        return true;
    }

    internal void UpdateNativeCatacombsArrow(
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativeProjectile projectile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(projectile);
        if (!nativeCatacombsArrows.TryGetValue(state.ProjectileEntityId, out var existing)
            || !Projectiles.TryGetValue(projectile.EntityId, out var currentProjectile)
            || !ReferenceEquals(projectile, currentProjectile)
            || projectile.EntityId != state.ProjectileEntityId)
            throw new InvalidDataException("A Catacombs Arrow update is detached from its owned projectile.");
        ValidateNativeCatacombsArrow(state, projectileOverride: projectile,
            expectedPreviousState: existing);
        nativeCatacombsArrows[state.ProjectileEntityId] = state with { };
    }

    internal void RemoveNativeCatacombsArrow(uint projectileEntityId)
    {
        nativeCatacombsArrows.Remove(projectileEntityId);
        Projectiles.Remove(projectileEntityId);
    }

    /** Retains the source's last live native facts before its entity becomes a corpse. */
    internal void RefreshNativeCatacombsArrowSource(AuthoritativeCombatTarget source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!CombatTargets.TryGetValue(source.EntityId, out var currentSource)
            || !ReferenceEquals(source, currentSource))
            throw new InvalidDataException("Catacombs Arrow source facts can only refresh from the exact live actor.");
        foreach (var projectileId in nativeCatacombsArrows.Values
                     .Where(value => value.SourceEntityId == source.EntityId)
                     .Select(value => value.ProjectileEntityId)
                     .ToArray()) {
            var previous = nativeCatacombsArrows[projectileId];
            if (source.LevelId != previous.LevelId
                || NativePopulationForLevel(previous.LevelId) is not { } bindings)
                throw new InvalidDataException("A Catacombs Arrow source moved outside its retained floor owner.");
            previous.ValidateSourceIdentity(source, bindings);
            var state = previous.RefreshSource(source);
            nativeCatacombsArrows[projectileId] = state;
        }
    }

    private void ValidateNativeCatacombsArrow(
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativeNativePopulationBindings? expectedBindings = null,
        AuthoritativeProjectile? projectileOverride = null,
        bool requireRegistered = true,
        AuthoritativeNativeCatacombsArrowState? expectedPreviousState = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ValidateShape();
        var isRegistered = nativeCatacombsArrows.TryGetValue(state.ProjectileEntityId, out var current);
        if (expectedPreviousState is not null) {
            if (!isRegistered || current != expectedPreviousState
                || !SameFlightIdentity(state, expectedPreviousState)
                || state.MissileDistance != expectedPreviousState.MissileDistance + 1
                || state.SourcePositionX != expectedPreviousState.SourcePositionX
                || state.SourcePositionY != expectedPreviousState.SourcePositionY
                || state.SourceMonsterLevel != expectedPreviousState.SourceMonsterLevel
                || state.SourceToHit != expectedPreviousState.SourceToHit
                || state.SourceMinimumDamage != expectedPreviousState.SourceMinimumDamage
                || state.SourceMaximumDamage != expectedPreviousState.SourceMaximumDamage
                || state.SourceIsPlayerMinion != expectedPreviousState.SourceIsPlayerMinion
                || state.SourceIsBerserked != expectedPreviousState.SourceIsBerserked)
                throw new InvalidDataException("A Catacombs Arrow update changed its frozen source or immutable flight identity.");
        } else if (requireRegistered ? !isRegistered || current != state : isRegistered) {
            throw new InvalidDataException("A Catacombs Arrow is detached from its exact registered flight state.");
        }

        if (NativePopulationForLevel(state.LevelId) is not { } bindings
            || expectedBindings is not null && !ReferenceEquals(expectedBindings, bindings)
            || bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.Depth != state.Depth
            || bindings.Fingerprint != state.PopulationFingerprint
            || bindings.MonsterMetadataFingerprint != state.MetadataFingerprint
            || (int?)nativePopulationParticipantClassId != state.CharacterClassId
            || !nativeCatacombsRangedConfiguredLevels.Contains(state.LevelId)
            || !nativeCatacombsRangedMissileCatalogs.TryGetValue(state.LevelId, out var catalog)
            || !catalog.TryGet(state.NativeAi, state.ContentMonsterId, out var missile)
            || missile.MissileId != "Arrow"
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.MonsterArrow
            || missile.AttackMode != AuthoritativeMonsterRangedAttackMode.Ranged
            || missile.SpellId != 0
            || AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile)
                != state.MissileMappingFingerprint)
            throw new InvalidDataException("A native Catacombs Arrow changed its floor, class, or missile mapping.");

        var projectile = projectileOverride
            ?? Projectiles.GetValueOrDefault(state.ProjectileEntityId)
            ?? throw new InvalidDataException("A native Catacombs Arrow lost its projectile before removal.");
        state.ValidateProjectile(projectile);
        if (CombatTargets.TryGetValue(state.SourceEntityId, out var source)) {
            // Source stats and position can change between a missile step and a
            // snapshot. They are refreshed on impact; validation here binds only
            // the immutable actor identity and native-slot provenance.
            state.ValidateSourceIdentity(source, bindings);
        } else if (!Corpses.TryGetValue(state.SourceEntityId, out var corpse)
            || corpse.LevelId != state.LevelId
            || corpse.PositionX != state.SourcePositionX || corpse.PositionY != state.SourcePositionY) {
            throw new InvalidDataException("A native Catacombs Arrow lost its live source without an exact retained corpse.");
        }
        if (!ContainsPosition(state.LevelId, projectile.TargetX, projectile.TargetY))
            throw new InvalidDataException("A native Catacombs Arrow has an effective destination outside its owned floor.");
    }

    private static bool SameFlightIdentity(
        AuthoritativeNativeCatacombsArrowState candidate,
        AuthoritativeNativeCatacombsArrowState previous)
        => candidate with {
            MissileDistance = previous.MissileDistance,
            LastCollisionTargetHash = previous.LastCollisionTargetHash,
            SourcePositionX = previous.SourcePositionX,
            SourcePositionY = previous.SourcePositionY,
            SourceMonsterLevel = previous.SourceMonsterLevel,
            SourceToHit = previous.SourceToHit,
            SourceMinimumDamage = previous.SourceMinimumDamage,
            SourceMaximumDamage = previous.SourceMaximumDamage,
            SourceIsPlayerMinion = previous.SourceIsPlayerMinion,
            SourceIsBerserked = previous.SourceIsBerserked,
        } == previous;
}
