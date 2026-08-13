namespace Devilution.Diablo;

/** Native-compatible game mode selected for one authoritative simulation. */
public enum DiabloGameMode
{
    SinglePlayer,
    Multiplayer,
}

/** Construction data for one protocol-independent Diablo simulation. */
public sealed class DiabloSimulationOptions
{
    public DiabloSimulationOptions(StoreCatalog storeCatalog, IDiabloStoreRules storeRules)
    {
        StoreCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        StoreRules = storeRules ?? throw new ArgumentNullException(nameof(storeRules));
    }

    public StoreCatalog StoreCatalog { get; }

    public IDiabloStoreRules StoreRules { get; }

    public AuthoritativeWorld? World { get; init; }

    public int FallbackWidth { get; init; } = 40;

    public int FallbackHeight { get; init; } = 40;

    public IEnumerable<int>? FallbackBlockedCells { get; init; }

    public IEnumerable<int>? FallbackMissileBlockedCells { get; init; }

    public IReadOnlyList<AuthoritativeCombatTarget>? CombatTargets { get; init; }

    public IReadOnlyList<AuthoritativePortal>? Portals { get; init; }

    public IReadOnlyList<AuthoritativeWorldItem>? WorldItems { get; init; }

    public IReadOnlyList<AuthoritativeWorldObject>? Objects { get; init; }

    public IReadOnlyList<AuthoritativeQuestState>? Quests { get; init; }

    /** Optional native quest routing metadata used to activate initial quests. */
    public AuthoritativeQuestTypeCatalog? QuestTypes { get; init; }

    public IReadOnlyList<AuthoritativeProjectile>? Projectiles { get; init; }

    public AuthoritativeSpellCatalog? Spells { get; init; }

    public AuthoritativeCombatRules? CombatRules { get; init; }

    public AuthoritativeItemCatalog? Items { get; init; }

    /** Stable spell identity authorizing the Bard's zero-cost Identify action. */
    public uint IdentifyAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Warrior's native Repair action. */
    public uint RepairAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Sorcerer's native Staff Recharge action. */
    public uint RechargeAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Rogue's native Trap Disarm action. */
    public uint TrapDisarmAbilitySpellId { get; init; }

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; init; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; init; }

    /** Enables deferred native animation-frame effects during the migration. */
    public bool UseNativePlayerActionTiming { get; init; }

    /** Server-owned initial state for the native-compatible gameplay RNG. */
    public uint RandomSeed { get; init; }

    /** Explicit session mode; it must not be inferred from connected participants. */
    public DiabloGameMode GameMode { get; init; } = DiabloGameMode.SinglePlayer;

    /** Optional server-owned encounter policy used for initial and entered levels. */
    public DiabloMonsterSpawnConfiguration? ProceduralMonsterSpawns { get; init; }

    /** Planner supplied by the validated content activation boundary. */
    public AuthoritativeMonsterSpawnPlanner? MonsterSpawnPlanner { get; init; }
}

/** Engine capability that constructs a configured first-party Diablo simulation. */
public interface IDiabloSimulationFactory
{
    DiabloSimulation Create(DiabloSimulationOptions options);
}

/** Default first-party simulation factory registered by the Diablo module. */
public sealed class DiabloSimulationFactory : IDiabloSimulationFactory
{
    public static DiabloSimulationFactory Instance { get; } = new();

    private DiabloSimulationFactory()
    {
    }

    public DiabloSimulation Create(DiabloSimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var worldState = new DiabloWorldState(
            options.World,
            options.FallbackWidth,
            options.FallbackHeight,
            options.FallbackBlockedCells,
            options.CombatTargets,
            options.Portals,
            options.WorldItems,
            options.Objects,
            options.Quests,
            options.Projectiles,
            options.FallbackMissileBlockedCells);
        var spells = options.Spells ?? new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0),
            new AuthoritativeSpellDefinition(2, 3, 0, 1, 10, 1),
        ]);
        var combatRules = options.CombatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        if (options.Items is not null)
            combatRules.ValidateItems(options.Items);
        var random = new DiabloRandom(options.RandomSeed);
        var playerDamage = new AuthoritativePlayerDamageResolver(spells);
        var worldCommands = new DiabloWorldCommandProcessor(
            worldState,
            options.PlayerAnimations,
            options.UseNativePlayerActionTiming,
            combatRules,
            random,
            playerDamage,
            options.TrapDisarmAbilitySpellId,
            options.QuestTypes,
            options.GameMode);
        return new DiabloSimulation(
            options.StoreCatalog,
            worldState,
            new DiabloStoreCommandProcessor(
                options.StoreCatalog,
                options.StoreRules,
                combatRules,
                random,
                options.IdentifyAbilitySpellId,
                options.RepairAbilitySpellId,
                options.RechargeAbilitySpellId,
                spells),
            worldCommands,
            new DiabloCombatCommandProcessor(
                worldState,
                spells,
                combatRules,
                random,
                options.PlayerClasses,
                options.PlayerAnimations,
                options.UseNativePlayerActionTiming,
                options.GameMode,
                options.Items,
                playerDamage,
                worldCommands),
            random,
            combatRules,
            options.PlayerClasses,
            options.PlayerAnimations,
            options.MonsterSpawnPlanner,
            options.ProceduralMonsterSpawns,
            options.RandomSeed,
            options.GameMode);
    }
}

/**
 * Protocol-independent aggregate composing the current Diablo state and all
 * authoritative gameplay transition processors.
 */
public sealed partial class DiabloSimulation
{
    private readonly StoreCatalog storeCatalog;
    private readonly DiabloStoreCommandProcessor storeCommands;
    private readonly DiabloWorldCommandProcessor worldCommands;
    private readonly DiabloCombatCommandProcessor combatCommands;
    private readonly DiabloRandom random;
    private readonly AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner;
    private readonly DiabloMonsterSpawnConfiguration? proceduralMonsterSpawns;
    private readonly uint proceduralMonsterSeed;
    private readonly DiabloGameMode gameMode;
    private readonly HashSet<uint> materializedProceduralLevels = [];
    private uint nextGeneratedMonsterEntityId = AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId;

    internal DiabloSimulation(
        StoreCatalog storeCatalog,
        DiabloWorldState worldState,
        DiabloStoreCommandProcessor storeCommands,
        DiabloWorldCommandProcessor worldCommands,
        DiabloCombatCommandProcessor combatCommands,
        DiabloRandom random,
        AuthoritativeCombatRules combatRules,
        AuthoritativePlayerClassCatalog? playerClasses,
        AuthoritativePlayerAnimationCatalog? playerAnimations,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        DiabloMonsterSpawnConfiguration? proceduralMonsterSpawns = null,
        uint proceduralMonsterSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        this.storeCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        WorldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
        this.storeCommands = storeCommands ?? throw new ArgumentNullException(nameof(storeCommands));
        this.worldCommands = worldCommands ?? throw new ArgumentNullException(nameof(worldCommands));
        this.combatCommands = combatCommands ?? throw new ArgumentNullException(nameof(combatCommands));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        CombatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
        PlayerClasses = playerClasses;
        PlayerAnimations = playerAnimations;
        this.monsterSpawnPlanner = monsterSpawnPlanner;
        this.proceduralMonsterSpawns = proceduralMonsterSpawns?.Validate();
        this.proceduralMonsterSeed = proceduralMonsterSeed;
        this.gameMode = gameMode;
        ReconcileProceduralSpawnState();
    }

    public DiabloWorldState WorldState { get; }

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; }

    /** Validated content rules governing this Diablo simulation instance. */
    public AuthoritativeCombatRules CombatRules { get; }

    public uint RandomState => random.State;

    public DiabloIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloStoreIntent intent,
        ulong appliedTick)
    {
        return storeCommands.Execute(player, intent, appliedTick);
    }

    public DiabloWorldIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloWorldIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        var participants = players as IReadOnlyList<AuthoritativePlayerState>
            ?? players.ToArray();
        var result = worldCommands.Execute(player, intent, appliedTick, participants);
        if (result.Succeeded)
            EnsureProceduralMonsterSpawns(player.LevelId, participants);
        return result;
    }

    /** Materializes one deterministic encounter set when a player enters a level. */
    private void EnsureProceduralMonsterSpawns(
        uint levelId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (monsterSpawnPlanner is null
            || proceduralMonsterSpawns is null
            || !materializedProceduralLevels.Add(levelId)) {
            return;
        }

        var firstEntityId = FindNextMonsterEntityId(participants);
        IReadOnlyList<AuthoritativeCombatTarget> targets;
        do {
            targets = monsterSpawnPlanner.PlanCombatTargets(
                levelId,
                proceduralMonsterSeed,
                gameMode == DiabloGameMode.Multiplayer,
                proceduralMonsterSpawns.MaximumMonsters,
                firstEntityId,
                GetReservedPositions(levelId, participants));
            if (targets.All(target => !IsEntityIdOccupied(target.EntityId, participants)))
                break;
            firstEntityId = checked(firstEntityId + 1);
        } while (true);
        foreach (var target in targets)
            WorldState.CombatTargets.Add(target.EntityId, target);
        if (targets.Count != 0)
            nextGeneratedMonsterEntityId = checked(targets[^1].EntityId + 1);
    }

    private uint FindNextMonsterEntityId(
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var candidate = nextGeneratedMonsterEntityId;
        while (candidate == 0 || IsEntityIdOccupied(candidate, participants)) {
            candidate = checked(candidate + 1);
        }
        return candidate;
    }

    private bool IsEntityIdOccupied(
        uint entityId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        return WorldState.CombatTargets.ContainsKey(entityId)
            || WorldState.WorldItems.ContainsKey(entityId)
            || WorldState.Objects.ContainsKey(entityId)
            || WorldState.Projectiles.ContainsKey(entityId)
            || WorldState.Portals.ContainsKey(entityId)
            || participants.Any(player => player.EntityId == entityId);
    }

    private IReadOnlySet<(int X, int Y)> GetReservedPositions(
        uint levelId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var reserved = new HashSet<(int X, int Y)>();
        foreach (var target in WorldState.CombatTargets.Values.Where(target =>
                     target.HitPoints > 0
                     && (target.LevelId == 0 || target.LevelId == levelId))) {
            reserved.Add((target.PositionX, target.PositionY));
            if (target.IsMoving)
                reserved.Add((target.FuturePositionX, target.FuturePositionY));
        }
        foreach (var item in WorldState.WorldItems.Values.Where(item => item.LevelId == levelId))
            reserved.Add((item.PositionX, item.PositionY));
        foreach (var @object in WorldState.Objects.Values.Where(@object =>
                     !@object.Activated
                     && !@object.Destroyed
                     && @object.LevelId == levelId))
            reserved.Add((@object.PositionX, @object.PositionY));
        foreach (var portal in WorldState.Portals.Values) {
            if (portal.SourceLevelId == levelId)
                reserved.Add((portal.SourcePositionX, portal.SourcePositionY));
            if (portal.DestinationLevelId == levelId)
                reserved.Add((portal.DestinationPositionX, portal.DestinationPositionY));
        }
        if (WorldState.World is not null) {
            foreach (var warp in WorldState.World.WarpTargets) {
                if (warp.SourceLevelId == levelId)
                    reserved.Add((warp.TriggerX, warp.TriggerY));
                if (warp.TargetLevelId == levelId)
                    reserved.Add((warp.TargetX, warp.TargetY));
            }
        }
        foreach (var player in participants.Where(player => player.LevelId == levelId)) {
            reserved.Add((player.PositionX, player.PositionY));
            if (player.IsMoving)
                reserved.Add((player.FuturePositionX, player.FuturePositionY));
        }
        return reserved;
    }

    /** Reconciles procedural-level tracking after a save or snapshot restore. */
    internal void ReconcileProceduralSpawnState()
    {
        foreach (var target in WorldState.CombatTargets.Values
                     .Where(target => target.EntityId >= AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId)) {
            materializedProceduralLevels.Add(target.LevelId);
            nextGeneratedMonsterEntityId = Math.Max(
                nextGeneratedMonsterEntityId,
                checked(target.EntityId + 1));
        }
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick)
    {
        return combatCommands.Execute(player, intent, appliedTick, [player]);
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return combatCommands.Execute(player, intent, appliedTick, players);
    }

    public void AdvanceStatusEffects(AuthoritativePlayerState player, ulong tick)
    {
        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, tick);
    }

    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTick(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return combatCommands.AdvanceTick(tick, players);
    }
}
