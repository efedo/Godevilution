using Devilution.Engine;

namespace Devilution.Diablo;

/**
 * Detached allocator cursors owned by the Diablo module.  The two world-item
 * cursors intentionally remain separate: they are independent native-style
 * allocators even though they start in the same range.
 * The six processor cursors retain zero exhaustion. The simulation's separate
 * procedural monster cursor is raw legacy state: zero advances to one and
 * occupied identities are skipped; it must not be replaced by a high-water mark.
 */
public sealed record DiabloCohortAllocatorState(
    DiabloWorldCommandAllocatorState WorldCommand,
    DiabloCombatCommandAllocatorState CombatCommand,
    uint NextGeneratedMonsterEntityId);

/** Configuration fences required to install a captured simulation safely. */
public sealed record DiabloCohortConfigurationState(
    DiabloGameMode GameMode,
    DiabloGameVariant GameVariant,
    DiabloDifficulty Difficulty,
    bool UseNativePlayerActionTiming,
    uint InitialRandomSeed,
    uint ProceduralMonsterSeed,
    uint ProceduralSetPieceOriginSeed,
    uint ProceduralQuestPieceOriginSeed,
    bool AllowMissingNativeDunLayers,
    DiabloNativeGeneratedWorldIdentity? NativeGeneratedWorldIdentity,
    DiabloCathedralDepthWorldIdentity? CathedralDepthWorldIdentity)
{
    /** Required by native-initialized actor cohorts; absent in legacy schemas. */
    public string? NativeMonsterMetadataFingerprint { get; init; }

    /** Explicit fences for the separately versioned native item-pool owner. */
    public string? CathedralItemMetadataFingerprint { get; init; }
    public string? CathedralItemGenerationFingerprint { get; init; }
}

/** Full portal state, including metadata omitted by the legacy save projection. */
public sealed record DiabloCohortPortalState(
    uint PortalId,
    uint SourceLevelId,
    int SourcePositionX,
    int SourcePositionY,
    uint DestinationLevelId,
    int DestinationPositionX,
    int DestinationPositionY,
    int DestinationSetLevelId,
    bool DestinationSetLevelActiveMap,
    string DestinationSetLevelSymbol,
    string StableSymbol,
    uint RemainingTicks,
    uint OwnerEntityId,
    bool Bidirectional);

/** Detached player state plus constructor-only and timing supplements. */
public sealed record DiabloCohortParticipantState(
    string SessionId,
    uint EntityId,
    StorePlayerSnapshot Player,
    ulong? LastAppliedTick,
    int BaseArmorClass,
    int MaximumAttribute,
    bool GoldTopologyEnabled,
    int EffectiveArmorClass = 0,
    AuthoritativeEquipmentAttributeBonuses AppliedEquipmentAttributeBonuses = default,
    bool? PlayerClassBound = null,
    bool? ItemCatalogBound = null,
    AuthoritativePlayerClassDefinition? PlayerClassDefinition = null);

/** Combat target state not represented by the legacy DiabloMonsterState record. */
public sealed record DiabloCohortCombatTargetState(
    DiabloMonsterState Monster,
    DiabloWorldItemState? Drop,
    bool FallenRallyAttackPending)
{
    public AuthoritativeNativeMonsterInitialization? NativeInitialization { get; init; }
}

/**
 * Complete module-owned Diablo state for one exact engine tick.  Transport,
 * persistence, canonical hashing, and host participant ledgers intentionally
 * remain outside this record.
 */
public sealed record DiabloCohortState(
    int FormatVersion,
    ulong Tick,
    DiabloCohortConfigurationState Configuration,
    IReadOnlyList<DiabloCohortParticipantState> Participants,
    IReadOnlyList<DiabloCohortCombatTargetState> CombatTargets,
    IReadOnlyList<DiabloWorldItemState> WorldItems,
    IReadOnlyList<DiabloObjectState> Objects,
    IReadOnlyList<DiabloCohortQuestState> Quests,
    IReadOnlyList<DiabloProjectileState> Projectiles,
    IReadOnlyList<DiabloCohortPortalState> Portals,
    IReadOnlyList<DiabloCorpseState> Corpses,
    IReadOnlyList<DiabloSetLevelObjectOriginState> SetLevelObjectOrigins,
    IReadOnlyList<DiabloProceduralQuestPieceOriginState> ProceduralQuestPieceOrigins,
    IReadOnlyList<uint> MaterializedProceduralLevelIds,
    IReadOnlyList<uint> AppliedNativeDunFragmentIds,
    IReadOnlyList<DiabloStoreCatalogState> Stores,
    DiabloCohortAllocatorState Allocators,
    uint RandomState,
    bool VictoryTriggered,
    DiabloWorldGeometryState? Geometry,
    DiabloWorldInfrastructureState WorldInfrastructure)
{
    public const int CurrentFormatVersion = 1;
    public const int CathedralDoorFormatVersion = 2;
    public const int NativeMonsterFormatVersion = 3;
    public const int CathedralItemFormatVersion = 4;
    public const int NativeCombatFormatVersion = 5;
    public const int NativeSarcophagusFormatVersion = 6;

    /** Required in v2, optional in native-initialization v3, forbidden in v1. */
    public DiabloCathedralDoorCheckpoint? CathedralDoors { get; init; }

    /** Complete native item pools, not a complete raw native level checkpoint. */
    public DiabloCathedralItemCheckpoint? CathedralItems { get; init; }

    /** Complete native sarcophagus/object reservation lifecycle for v6. */
    public IReadOnlyList<DiabloNativeSarcophagusState>? NativeSarcophagi { get; init; }

    /** Detached records are captured in deterministic identity order. */
    public void ValidateShape()
    {
        if ((FormatVersion != CurrentFormatVersion && FormatVersion != CathedralDoorFormatVersion
                && FormatVersion != NativeMonsterFormatVersion && FormatVersion != CathedralItemFormatVersion
                && FormatVersion != NativeCombatFormatVersion && FormatVersion != NativeSarcophagusFormatVersion)
            || (FormatVersion == CurrentFormatVersion && CathedralDoors is not null)
            || (FormatVersion == CathedralDoorFormatVersion && CathedralDoors is null))
            throw new InvalidDataException(
                $"Unsupported Diablo cohort format version {FormatVersion}.");
        CathedralDoors?.ValidateShape();
        ArgumentNullException.ThrowIfNull(Configuration);
        if (FormatVersion is NativeMonsterFormatVersion or CathedralItemFormatVersion or NativeCombatFormatVersion or NativeSarcophagusFormatVersion
            ? !AuthoritativeNativeMonsterInitialization.IsFingerprint(Configuration.NativeMonsterMetadataFingerprint)
            : Configuration.NativeMonsterMetadataFingerprint is not null)
            throw new InvalidDataException("Native monster cohorts require their explicit metadata identity.");
        if (FormatVersion is CathedralItemFormatVersion or NativeCombatFormatVersion or NativeSarcophagusFormatVersion) {
            if (CathedralItems is null)
                throw new InvalidDataException("Native item cohorts require their complete item pools.");
            CathedralItems.ValidateShape();
            if (Configuration.CathedralItemMetadataFingerprint != CathedralItems.MetadataFingerprint
                || Configuration.CathedralItemGenerationFingerprint != CathedralItems.GenerationFingerprint)
                throw new InvalidDataException("Native item cohorts have conflicting metadata or generation identities.");
        } else if (CathedralItems is not null || Configuration.CathedralItemMetadataFingerprint is not null
            || Configuration.CathedralItemGenerationFingerprint is not null) {
            throw new InvalidDataException("Native item state cannot be down-converted to an older cohort.");
        }
        if (FormatVersion == NativeSarcophagusFormatVersion) {
            if (NativeSarcophagi is null)
                throw new InvalidDataException("Native sarcophagus cohorts require their complete object lifecycle records.");
        } else if (NativeSarcophagi is not null) {
            throw new InvalidDataException("Native sarcophagus state cannot be down-converted to an older cohort.");
        }
        ArgumentNullException.ThrowIfNull(Participants);
        ArgumentNullException.ThrowIfNull(CombatTargets);
        ArgumentNullException.ThrowIfNull(WorldItems);
        ArgumentNullException.ThrowIfNull(Objects);
        ArgumentNullException.ThrowIfNull(Quests);
        ArgumentNullException.ThrowIfNull(Projectiles);
        ArgumentNullException.ThrowIfNull(Portals);
        ArgumentNullException.ThrowIfNull(Corpses);
        ArgumentNullException.ThrowIfNull(SetLevelObjectOrigins);
        ArgumentNullException.ThrowIfNull(ProceduralQuestPieceOrigins);
        ArgumentNullException.ThrowIfNull(MaterializedProceduralLevelIds);
        ArgumentNullException.ThrowIfNull(AppliedNativeDunFragmentIds);
        ArgumentNullException.ThrowIfNull(Stores);
        ArgumentNullException.ThrowIfNull(Allocators);
        ArgumentNullException.ThrowIfNull(WorldInfrastructure);
        ArgumentNullException.ThrowIfNull(WorldInfrastructure.BlockedCells);
        ArgumentNullException.ThrowIfNull(WorldInfrastructure.MissileBlockedCells);
        ArgumentNullException.ThrowIfNull(WorldInfrastructure.ActiveWorldMutationIds);
        if (WorldInfrastructure.EffectiveLevels is null)
            throw new InvalidDataException(
                "A complete Diablo cohort must carry its detached effective level projection.");

        var sessionIds = new HashSet<string>(StringComparer.Ordinal);
        var participantEntityIds = new HashSet<uint>();
        foreach (var participant in Participants) {
            if (participant is null)
                throw new InvalidDataException("A Diablo cohort contains a null participant.");
            if (string.IsNullOrWhiteSpace(participant.SessionId)
                || !sessionIds.Add(participant.SessionId)) {
                throw new InvalidDataException(
                    "A Diablo cohort contains a duplicate or blank participant ID.");
            }
            if (participant.Player is null)
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has no player state.");
            if (participant.PlayerClassBound is null
                || participant.ItemCatalogBound is null)
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' is missing its content-policy bindings.");
            if (participant.EntityId == 0
                || participant.Player.EntityId == 0
                || participant.EntityId != participant.Player.EntityId)
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has conflicting entity identities.");
            // Complete checkpoints require stable bindings before publication;
            // the generic legacy runtime may still contain unbound players.
            if (!participantEntityIds.Add(participant.EntityId))
                throw new InvalidDataException(
                    $"Participant entity {participant.EntityId} is registered more than once.");
        }

        if (CombatTargets.Any(target => target is null || target.Monster is null
            || target.Drop is not null && target.Drop.State is null))
            throw new InvalidDataException("A Diablo cohort contains an incomplete combat target.");
        var nativeSlots = new HashSet<(uint Level, int Slot)>();
        foreach (var target in CombatTargets) {
            if (target.Monster.NativeInitializationPresent != (target.NativeInitialization is not null))
                throw new InvalidDataException("Native monster policy is missing its initialization provenance.");
            if (target.NativeInitialization is not { } native)
                continue;
            native.ValidateTarget(target.Monster.EntityId, target.Monster.MonsterId,
                target.Monster.LevelId, target.Monster.IsUnique, target.Monster.UniqueIdentity);
            if ((FormatVersion != NativeMonsterFormatVersion && FormatVersion != CathedralItemFormatVersion
                    && FormatVersion != NativeCombatFormatVersion && FormatVersion != NativeSarcophagusFormatVersion)
                || native.MetadataFingerprint != Configuration.NativeMonsterMetadataFingerprint
                || !nativeSlots.Add((target.Monster.LevelId, native.InitialState.Slot)))
                throw new InvalidDataException("Native monster initialization conflicts with cohort metadata or slots.");
        }
        if (WorldItems.Any(item => item is null || item.State is null))
            throw new InvalidDataException("A Diablo cohort contains an incomplete world item.");
        if (Objects.Any(@object => @object is null))
            throw new InvalidDataException("A Diablo cohort contains a null object.");
        if (Projectiles.Any(projectile => projectile is null))
            throw new InvalidDataException("A Diablo cohort contains a null projectile.");
        if (FormatVersion is not (NativeCombatFormatVersion or NativeSarcophagusFormatVersion)
            && (CombatTargets.Any(target => target.Monster.NativeMeleeCursor is not null || target.Monster.NativeWalkCursor is not null)
                || Projectiles.Any(projectile => projectile.NativeWeaponArrow is not null)))
            throw new InvalidDataException("Native combat state cannot be down-converted to an older cohort.");
        if (FormatVersion is NativeCombatFormatVersion or NativeSarcophagusFormatVersion) {
            if (CathedralItems!.Levels.Count != 1)
                throw new InvalidDataException("Native combat cohorts require one owned level.");
            foreach (var target in CombatTargets) {
                var native = target.NativeInitialization;
                if (native is null) {
                    if (target.Monster.NativeMeleeCursor is not null || target.Monster.NativeWalkCursor is not null)
                        throw new InvalidDataException("A native cursor cannot belong to a nonnative actor.");
                    continue;
                }
                if (native.NativeAttackFrameCount <= 0 || native.NativeAttackTicksPerFrame < 0
                    || native.NativeHitRecoveryFrameCount <= 0 || native.NativeHitRecoveryTicksPerFrame < 0
                    || native.NativeWalkFrameCount <= 0 || native.NativeWalkTicksPerFrame < 0)
                    throw new InvalidDataException("Native combat cohorts require imported attack animation timing.");
                if (target.Monster.NativeMeleeCursor is { } cursor
                    && (cursor.FrameIndex < 0 || cursor.FrameIndex >= (cursor.HitRecovery ? native.NativeHitRecoveryFrameCount : native.NativeAttackFrameCount)
                        || cursor.TickCounter < 0 || cursor.TickCounter >= Math.Max(1, cursor.HitRecovery ? native.NativeHitRecoveryTicksPerFrame : native.NativeAttackTicksPerFrame)
                        || target.Monster.HitPointsQ6 <= 0 || target.Monster.Moving))
                    throw new InvalidDataException("A native melee cursor is outside its owned animation.");
                if (target.Monster.Moving != (target.Monster.NativeWalkCursor is not null))
                    throw new InvalidDataException("Native movement must retain its walk animation cursor.");
                if (target.Monster.NativeWalkCursor is { } walk
                    && (walk.FrameIndex < -1 || walk.FrameIndex >= native.NativeWalkFrameCount
                        || walk.TickCounter < 0 || walk.TickCounter >= Math.Max(1, native.NativeWalkTicksPerFrame)
                        || target.Monster.HitPointsQ6 <= 0 || target.Monster.NativeMeleeCursor is not null))
                    throw new InvalidDataException("A native walk cursor is outside its owned movement animation.");
                if (target.Monster.NativeWalkCursor is { } timedWalk
                    && timedWalk.FrameIndex != native.NativeWalkFrameCount - 1) {
                    var elapsed = (ulong)((timedWalk.FrameIndex + 1) * Math.Max(1, native.NativeWalkTicksPerFrame)
                        + timedWalk.TickCounter);
                    if (elapsed > Tick && elapsed - Tick > 1)
                        throw new InvalidDataException("Native walk elapsed timing exceeds its first possible update.");
                }
            }
            foreach (var projectile in Projectiles) {
                if (projectile.NativeWeaponArrow is { } arrow
                    && (arrow.MissileDistance > int.MaxValue
                        || projectile.Behavior != AuthoritativeProjectileBehavior.WeaponArrow
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.LevelId != CathedralItems!.Levels.Single().LevelId
                        || !Participants.Any(player => player.EntityId == projectile.SourceEntityId)
                        || projectile.AttackHit || projectile.Damage != 0 || projectile.DamageQ6 != 0
                        || projectile.PerilSelfDamageQ6 != 0 || projectile.DoppelgangerProc))
                    throw new InvalidDataException("A native arrow has no valid unresolved collision state.");
            }
        }
        if (FormatVersion == NativeSarcophagusFormatVersion) {
            var nativeSarcophagi = NativeSarcophagi
                ?? throw new InvalidDataException("Native sarcophagus cohorts require their complete lifecycle records.");
            if (nativeSarcophagi.Any(value => value is null))
                throw new InvalidDataException("A native sarcophagus cohort contains a null lifecycle record.");
            if (Corpses.Any(corpse => corpse is null))
                throw new InvalidDataException("A native sarcophagus cohort contains a null corpse.");
            var orderedSarcophagi = nativeSarcophagi.OrderBy(value => value.ObjectSlot).ToArray();
            if (!nativeSarcophagi.SequenceEqual(orderedSarcophagi)
                || nativeSarcophagi.Select(value => value.ObjectSlot).Distinct().Count() != nativeSarcophagi.Count
                || nativeSarcophagi.Select(value => value.EntityId).Distinct().Count() != nativeSarcophagi.Count)
                throw new InvalidDataException("Native sarcophagus lifecycle records must be unique and sorted by raw slot.");
            if (Objects.Select(value => value.EntityId).Distinct().Count() != Objects.Count
                || CombatTargets.Select(value => value.Monster.EntityId).Distinct().Count() != CombatTargets.Count
                || Corpses.Select(value => value.EntityId).Distinct().Count() != Corpses.Count)
                throw new InvalidDataException("Native sarcophagus owner identities must be unique before cross-reference validation.");
            var objectsById = Objects.ToDictionary(value => value.EntityId);
            var targetsById = CombatTargets.ToDictionary(value => value.Monster.EntityId);
            var corpsesById = Corpses.ToDictionary(value => value.EntityId);
            foreach (var sarcophagus in nativeSarcophagi) {
                sarcophagus.ValidateShape();
                if (!objectsById.TryGetValue(sarcophagus.EntityId, out var objectState)
                    || objectState.LevelId != CathedralItems!.Levels.Single().LevelId
                    || objectState.NativeObjectId != checked((uint)sarcophagus.NativeTypeId)
                    || objectState.PositionX != sarcophagus.InitialPositionX
                    || objectState.PositionY != sarcophagus.InitialPositionY
                    || objectState.Activated != sarcophagus.Opened
                    || objectState.AnimationFrame != sarcophagus.AnimationFrame)
                    throw new InvalidDataException("Native sarcophagus lifecycle identity conflicts with its saved object projection.");
                if (sarcophagus.ActorState is DiabloNativeSarcophagusActorState.Emerging or DiabloNativeSarcophagusActorState.Active) {
                    if (!targetsById.TryGetValue(sarcophagus.LinkedMonsterEntityId, out var actor)
                        || actor.NativeInitialization is not { } native
                        || native.Binding.NativeSlot != sarcophagus.LinkedMonsterSlot
                        || native.Binding.EntityId != sarcophagus.LinkedMonsterEntityId
                        || native.Binding.NativeTypeId != sarcophagus.LinkedMonsterTypeId
                        || native.InitialState.Slot != sarcophagus.LinkedMonsterSlot
                        || native.InitialState.Type != sarcophagus.LinkedMonsterTypeId
                        || actor.Monster.LevelId != objectState.LevelId || actor.Monster.IsPlayerMinion)
                        throw new InvalidDataException("A spawned sarcophagus actor lacks its exact saved native reservation.");
                    if (sarcophagus.ActorState == DiabloNativeSarcophagusActorState.Emerging
                        && (actor.Monster.PositionX != sarcophagus.SpawnPositionX
                            || actor.Monster.PositionY != sarcophagus.SpawnPositionY
                            || actor.Monster.FuturePositionX != sarcophagus.SpawnPositionX
                            || actor.Monster.FuturePositionY != sarcophagus.SpawnPositionY
                            || actor.Monster.Moving || actor.Monster.HitPointsQ6 <= 0
                            || actor.Monster.NativeMeleeCursor is not null))
                        throw new InvalidDataException("An emerging sarcophagus actor changed before its SpecialStand completed.");
                } else if (sarcophagus.ActorState == DiabloNativeSarcophagusActorState.Dead) {
                    if (targetsById.ContainsKey(sarcophagus.LinkedMonsterEntityId)
                        || !corpsesById.TryGetValue(sarcophagus.LinkedMonsterEntityId, out var corpse)
                        || corpse.LevelId != objectState.LevelId)
                        throw new InvalidDataException("A dead sarcophagus actor lost its exact retained corpse disposition.");
                } else if (sarcophagus.LinkedMonsterEntityId != 0
                    && (targetsById.ContainsKey(sarcophagus.LinkedMonsterEntityId)
                        || corpsesById.ContainsKey(sarcophagus.LinkedMonsterEntityId))) {
                    throw new InvalidDataException("An unspawned sarcophagus reservation was materialized in its cohort.");
                }
            }
        }
        if (Corpses.Any(corpse => corpse is null))
            throw new InvalidDataException("A Diablo cohort contains a null corpse.");
        if (Portals.Any(portal => portal is null))
            throw new InvalidDataException("A Diablo cohort contains a null portal.");
        foreach (var quest in Quests) {
            if (quest is null)
                throw new InvalidDataException("A Diablo cohort contains a null quest.");
            quest.ValidateShape();
        }
        EnsureUniqueIds(CombatTargets.Select(target => target.Monster.EntityId), "combat target");
        EnsureUniqueIds(WorldItems.Select(item => item.EntityId), "world item");
        EnsureUniqueIds(Objects.Select(@object => @object.EntityId), "object");
        EnsureUniqueIds(Projectiles.Select(projectile => projectile.EntityId), "projectile");
        EnsureUniqueIds(Corpses.Select(corpse => corpse.EntityId), "corpse");
        EnsureUniqueIds(Portals.Select(portal => portal.PortalId), "portal");
        EnsureUniqueIds(Quests.Select(quest => quest.QuestId), "quest");

        EnsureOrdered(Participants.Select(participant => participant.SessionId), StringComparer.Ordinal, "participant");
        EnsureOrdered(CombatTargets.Select(target => target.Monster.EntityId), Comparer<uint>.Default, "combat target");
        EnsureOrdered(WorldItems.Select(item => item.EntityId), Comparer<uint>.Default, "world item");
        EnsureOrdered(Objects.Select(item => item.EntityId), Comparer<uint>.Default, "object");
        EnsureOrdered(Quests.Select(quest => quest.QuestId), Comparer<uint>.Default, "quest");
        EnsureOrdered(Projectiles.Select(item => item.EntityId), Comparer<uint>.Default, "projectile");
        EnsureOrdered(Portals.Select(portal => portal.PortalId), Comparer<uint>.Default, "portal");
        EnsureOrdered(Corpses.Select(corpse => corpse.EntityId), Comparer<uint>.Default, "corpse");
        EnsureOrdered(SetLevelObjectOrigins.Select(origin => origin.SetLevelId), Comparer<int>.Default, "set-level origin");
        EnsureOrdered(ProceduralQuestPieceOrigins.Select(origin => origin.OriginId), Comparer<uint>.Default, "quest-piece origin");
        EnsureOrdered(MaterializedProceduralLevelIds, Comparer<uint>.Default, "materialized level");
        EnsureOrdered(AppliedNativeDunFragmentIds, Comparer<uint>.Default, "applied DUN fragment");
        EnsureOrdered(Stores.Select(store => store.StoreId), Comparer<uint>.Default, "store");

        static void EnsureOrdered<T>(IEnumerable<T> values, IComparer<T> comparer, string category)
        {
            using var iterator = values.GetEnumerator();
            if (!iterator.MoveNext())
                return;
            var previous = iterator.Current;
            while (iterator.MoveNext()) {
                if (comparer.Compare(previous, iterator.Current) >= 0)
                    throw new InvalidDataException($"A Diablo cohort has noncanonical {category} ordering.");
                previous = iterator.Current;
            }
        }

        static void EnsureUniqueIds(IEnumerable<uint> ids, string category)
        {
            var seen = new HashSet<uint>();
            foreach (var id in ids) {
                if (!seen.Add(id))
                    throw new InvalidDataException(
                        $"A Diablo cohort contains duplicate {category} identity {id}.");
            }
        }
    }

    /** Active world mutation IDs are part of the world infrastructure state. */
    public IReadOnlyList<uint> ActiveWorldMutationIds
        => WorldInfrastructure.ActiveWorldMutationIds;

    /** Exact nullable portal clock captured under the same cohort barrier. */
    public ulong? LastPortalTick => WorldInfrastructure.LastPortalTick;
}

/** Unexposed candidate simulation and detached participant roster. */
public sealed class DiabloCohortCandidate
{
    internal DiabloCohortCandidate(
        DiabloSimulation simulation,
        IReadOnlyList<Devilution.Engine.SimulationSession<AuthoritativePlayerState>> participants,
        ulong tick)
    {
        Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        Participants = participants ?? throw new ArgumentNullException(nameof(participants));
        Tick = tick;
    }

    public DiabloSimulation Simulation { get; }

    public IReadOnlyList<Devilution.Engine.SimulationSession<AuthoritativePlayerState>> Participants { get; }

    public ulong Tick { get; }

    private int runtimeCreated;

    /** Installs this detached candidate in one fresh engine runtime. */
    public AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>
        CreateRuntime()
    {
        if (System.Threading.Interlocked.Exchange(ref runtimeCreated, 1) != 0)
            throw new InvalidOperationException(
                "A Diablo cohort candidate can only initialize one runtime.");
        try {
            return Simulation.CreateCohortRuntime(Tick, Participants);
        } catch {
            System.Threading.Volatile.Write(ref runtimeCreated, 0);
            throw;
        }
    }
}
