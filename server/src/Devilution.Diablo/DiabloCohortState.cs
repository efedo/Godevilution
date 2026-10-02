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
    DiabloCathedralDepthWorldIdentity? CathedralDepthWorldIdentity);

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
    bool FallenRallyAttackPending);

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

    /** Detached records are captured in deterministic identity order. */
    public void ValidateShape()
    {
        if (FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException(
                $"Unsupported Diablo cohort format version {FormatVersion}.");
        ArgumentNullException.ThrowIfNull(Configuration);
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
        if (WorldItems.Any(item => item is null || item.State is null))
            throw new InvalidDataException("A Diablo cohort contains an incomplete world item.");
        if (Objects.Any(@object => @object is null))
            throw new InvalidDataException("A Diablo cohort contains a null object.");
        if (Projectiles.Any(projectile => projectile is null))
            throw new InvalidDataException("A Diablo cohort contains a null projectile.");
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
