using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Native-compatible game mode selected for one authoritative simulation. */
public enum DiabloGameMode
{
    SinglePlayer,
    Multiplayer,
}

/** Native-compatible game difficulty stored in authoritative saves. */
public enum DiabloDifficulty
{
    Normal,
    Nightmare,
    Hell,
}

/** Construction data for one protocol-independent Diablo simulation. */
public sealed class DiabloSimulationOptions
{
    public const uint LegacyMonsterFlashSpellId = 4;

    public DiabloSimulationOptions(StoreCatalog storeCatalog, IDiabloStoreRules storeRules)
    {
        StoreCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        StoreRules = storeRules ?? throw new ArgumentNullException(nameof(storeRules));
    }

    public StoreCatalog StoreCatalog { get; }

    public IDiabloStoreRules StoreRules { get; }

    public AuthoritativeWorld? World { get; init; }

    /** Validated content-backed level-cell overlays activated by world objects. */
    public AuthoritativeWorldMutationCatalog? WorldMutations { get; init; }

    /** Optional active world-owned identities and Town Portal geometry. */
    public AuthoritativeWorldRules? WorldRules { get; init; }

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

    /** Validated repeatable quest/towner interactions supplied by content. */
    public AuthoritativeQuestTownerInteractionCatalog? QuestTownerInteractions { get; init; }

    /** Validated staged unique-monster dialogue supplied by content. */
    public AuthoritativeQuestDialogueStageCatalog? QuestDialogueStages { get; init; }

    /** Validated native greetings for general town vendors and service NPCs. */
    public AuthoritativeTownerGreetingCatalog? TownerGreetings { get; init; }

    /** Validated native fixed-object placements materialized when geometry is available. */
    public AuthoritativeSetLevelObjectCatalog? SetLevelObjects { get; init; }

    /** Runtime-generated origins for relative set-level object placements. */
    public IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? SetLevelObjectOrigins { get; init; }

    /** Deterministically planned origins for generated set-piece placements. */
    public AuthoritativeProceduralSetPieceOriginCatalog? ProceduralSetPieceOrigins { get; init; }

    /** Validated mappings for native DUN object-layer materialization. */
    public AuthoritativeNativeDunObjectCatalog? NativeDunObjects { get; init; }

    /** Validated mappings for native DUN monster-layer materialization. */
    public AuthoritativeNativeDunMonsterCatalog? NativeDunMonsters { get; init; }

    /** Validated native DUN fragments whose unique anchors may materialize on level entry. */
    public AuthoritativeNativeDunFragmentCatalog? NativeDunFragments { get; init; }

    /** Optional anchor-relative object placements applied with deferred fragments. */
    public AuthoritativeNativeDunFragmentObjectCatalog? NativeDunFragmentObjects { get; init; }

    /** Composed content bytes retained for deferred native fragment materialization. */
    public ComposedContent? Content { get; init; }

    /** Optional object templates used when mapped native DUN entities are rematerialized. */
    public IReadOnlyList<AuthoritativeWorldObject>? NativeObjectTemplates { get; init; }

    /** Validated native set-level map assets and entry points used by activation. */
    public AuthoritativeSetLevelMapCatalog? SetLevelMaps { get; init; }

    /** Validated server-owned text for authored dialogue events. */
    public AuthoritativeDialogueMessageCatalog? DialogueMessages { get; init; }

    public IReadOnlyList<AuthoritativeProjectile>? Projectiles { get; init; }

    /** Server-owned corpse tiles available to corpse-consuming monster AI. */
    public IReadOnlyList<AuthoritativeCorpse>? Corpses { get; init; }

    public AuthoritativeSpellCatalog? Spells { get; init; }

    /** Validated cumulative character-level experience thresholds. */
    public AuthoritativeExperienceCatalog? Experience { get; init; }

    /** Validated stable spell identity used by native Counselor Flash layers. */
    public uint MonsterFlashSpellId { get; init; } = LegacyMonsterFlashSpellId;

    /** Validated native dynamic spell-book metadata used by item rewards. */
    public AuthoritativeSpellBookCatalog? SpellBooks { get; init; }

    /** Validated native AI-to-missile mappings supplied by active content. */
    public AuthoritativeMonsterMissileCatalog? MonsterMissiles { get; init; }

    /** Validated native missile identities and presentation metadata. */
    public AuthoritativeNativeMissileCatalog? NativeMissiles { get; init; }

    /** Validated native missile sprite dimensions and animation timing. */
    public AuthoritativeNativeMissileSpriteCatalog? NativeMissileSprites { get; init; }

    /** Validated monster type metadata supplied by active content. */
    public AuthoritativeMonsterTypeCatalog? MonsterTypes { get; init; }

    public AuthoritativeCombatRules? CombatRules { get; init; }

    public AuthoritativeItemCatalog? Items { get; init; }

    /** Validated server-owned item combinations applied during pickup. */
    public AuthoritativeItemCombinationCatalog? ItemCombinations { get; init; }

    /** Validated server-owned quest transitions applied when items are picked up. */
    public AuthoritativeItemQuestEffectCatalog? ItemQuestEffects { get; init; }

    /** Stable spell identity authorizing the Bard's zero-cost Identify action. */
    public uint IdentifyAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Warrior's native Repair action. */
    public uint RepairAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Sorcerer's native Staff Recharge action. */
    public uint RechargeAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Rogue's native Trap Disarm action. */
    public uint TrapDisarmAbilitySpellId { get; init; }

    /** External service-only store identity used by Adria's mana action. */
    public uint AdriaStoreId { get; init; } = DiabloStoreCommandProcessor.AdriaStoreId;

    /** External service-only store identity used by Cain's Identify action. */
    public uint StorytellerStoreId { get; init; } = DiabloStoreCommandProcessor.StorytellerStoreId;

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; init; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; init; }

    /** Enables deferred native animation-frame effects during the migration. */
    public bool UseNativePlayerActionTiming { get; init; }

    /** Server-owned initial state for the native-compatible gameplay RNG. */
    public uint RandomSeed { get; init; }

    /** Explicit session mode; it must not be inferred from connected participants. */
    public DiabloGameMode GameMode { get; init; } = DiabloGameMode.SinglePlayer;

    /** Native difficulty selected for this authoritative simulation. */
    public DiabloDifficulty Difficulty { get; init; } = DiabloDifficulty.Normal;

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
            options.FallbackMissileBlockedCells,
            options.Corpses,
            options.WorldMutations,
            nativeDunObjects: options.NativeDunObjects,
            nativeObjectTemplates: options.NativeObjectTemplates,
            allowMissingNativeDunLayers: options.NativeDunFragments is not null);
        if (options.NativeDunFragments is not null && options.Content is null)
            throw new InvalidDataException(
                "Deferred native DUN fragments require their composed content bytes.");
        var spells = options.Spells ?? new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0),
            new AuthoritativeSpellDefinition(2, 3, 0, 1, 10, 1),
        ]);
        options.WorldRules?.Validate();
        var combatRules = options.CombatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        if (options.Items is not null)
            combatRules.ValidateItems(options.Items);
        foreach (var target in worldState.CombatTargets.Values) {
            DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                target,
                options.Difficulty,
                combatRules.GameVariant,
                options.GameMode,
                combatRules.DifficultyRules);
            DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                target,
                options.Difficulty,
                options.MonsterTypes);
        }
        var random = new DeterministicLegacyRandom(options.RandomSeed);
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
            options.GameMode,
            options.Items,
            options.ItemCombinations,
            options.ItemQuestEffects,
            options.SpellBooks,
            options.QuestTownerInteractions,
            options.QuestDialogueStages,
            spells,
            options.MonsterSpawnPlanner,
            options.Difficulty,
            combatRules.GameVariant,
            options.MonsterTypes,
            options.Experience,
            options.TownerGreetings);
        var simulation = new DiabloSimulation(
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
                spells,
                options.Items,
                options.AdriaStoreId,
                options.WorldRules?.TownLevelId ?? AuthoritativePortalRules.TownLevelId,
                options.StorytellerStoreId),
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
                worldCommands,
                options.MonsterMissiles,
                options.MonsterSpawnPlanner,
                options.MonsterTypes,
                options.Difficulty,
                options.WorldRules,
                options.SpellBooks,
                options.MonsterFlashSpellId,
                options.Experience,
                options.QuestDialogueStages),
            spells,
            random,
            combatRules,
            options.PlayerClasses,
            options.PlayerAnimations,
            options.MonsterSpawnPlanner,
            options.ProceduralMonsterSpawns,
            options.RandomSeed,
            options.GameMode,
            options.Items,
            options.Difficulty,
            options.MonsterTypes,
            options.MonsterMissiles,
            options.SpellBooks,
            options.QuestTownerInteractions,
            options.QuestDialogueStages,
            options.DialogueMessages,
            options.SetLevelObjects,
            options.SetLevelObjectOrigins,
            options.SetLevelMaps,
            options.NativeDunObjects,
            options.NativeDunFragments,
            options.NativeDunFragmentObjects,
            options.NativeDunMonsters,
            options.NativeObjectTemplates,
            options.Content,
            questTypes: options.QuestTypes,
            proceduralSetPieceOrigins: options.ProceduralSetPieceOrigins?.Plan(
                options.World ?? new AuthoritativeWorld(),
                options.RandomSeed),
            proceduralSetPieceOriginCatalog: options.ProceduralSetPieceOrigins,
            proceduralSetPieceOriginSeed: options.RandomSeed,
            experience: options.Experience,
            nativeMissiles: options.NativeMissiles,
            nativeMissileSprites: options.NativeMissileSprites);
        worldState.SetSetLevelOriginResolver(simulation.ResolveSetLevelObjectOrigin);
        worldCommands.SetSetLevelOriginResolver(simulation.ResolveSetLevelObjectOrigin);
        return simulation;
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
    private readonly DeterministicLegacyRandom random;
    private readonly AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner;
    private readonly DiabloMonsterSpawnConfiguration? proceduralMonsterSpawns;
    private readonly uint proceduralMonsterSeed;
    private readonly DiabloGameMode gameMode;
    private readonly DiabloDifficulty difficulty;
    private readonly AuthoritativeMonsterTypeCatalog? monsterTypes;
    private readonly AuthoritativeMonsterMissileCatalog? monsterMissiles;
    private readonly AuthoritativeNativeDunFragmentCatalog? nativeDunFragments;
    private readonly AuthoritativeNativeDunFragmentObjectCatalog? nativeDunFragmentObjects;
    private readonly AuthoritativeNativeDunMonsterCatalog? nativeDunMonsters;
    private readonly AuthoritativeProceduralSetPieceOriginCatalog? proceduralSetPieceOriginCatalog;
    private readonly uint proceduralSetPieceOriginSeed;
    private readonly IReadOnlyList<AuthoritativeWorldObject> nativeObjectTemplates;
    private readonly ComposedContent? content;
    private readonly HashSet<uint> appliedNativeDunFragments = [];
    private readonly HashSet<uint> materializedProceduralLevels = [];
    private readonly Dictionary<int, AuthoritativeSetLevelObjectOrigin> setLevelObjectOrigins = [];
    private uint nextGeneratedMonsterEntityId = AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId;

    private sealed record DynamicOriginPublicationState(
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin> Origins,
        IReadOnlyDictionary<uint, AuthoritativeWorldObject> Objects,
        IReadOnlyDictionary<uint, AuthoritativeLevel> Levels,
        IReadOnlySet<uint> AppliedNativeDunFragments);

    internal DiabloSimulation(
        StoreCatalog storeCatalog,
        DiabloWorldState worldState,
        DiabloStoreCommandProcessor storeCommands,
        DiabloWorldCommandProcessor worldCommands,
        DiabloCombatCommandProcessor combatCommands,
        AuthoritativeSpellCatalog spells,
        DeterministicLegacyRandom random,
        AuthoritativeCombatRules combatRules,
        AuthoritativePlayerClassCatalog? playerClasses,
        AuthoritativePlayerAnimationCatalog? playerAnimations,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        DiabloMonsterSpawnConfiguration? proceduralMonsterSpawns = null,
        uint proceduralMonsterSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null,
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeQuestTownerInteractionCatalog? questTownerInteractions = null,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null,
        AuthoritativeDialogueMessageCatalog? dialogueMessages = null,
        AuthoritativeSetLevelObjectCatalog? setLevelObjects = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelObjectOrigins = null,
        AuthoritativeSetLevelMapCatalog? setLevelMaps = null,
        AuthoritativeNativeDunObjectCatalog? nativeDunObjects = null,
        AuthoritativeNativeDunFragmentCatalog? nativeDunFragments = null,
        AuthoritativeNativeDunFragmentObjectCatalog? nativeDunFragmentObjects = null,
        AuthoritativeNativeDunMonsterCatalog? nativeDunMonsters = null,
        IReadOnlyList<AuthoritativeWorldObject>? nativeObjectTemplates = null,
        ComposedContent? content = null,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? proceduralSetPieceOrigins = null,
        AuthoritativeProceduralSetPieceOriginCatalog? proceduralSetPieceOriginCatalog = null,
        uint proceduralSetPieceOriginSeed = 0,
        AuthoritativeExperienceCatalog? experience = null,
        AuthoritativeNativeMissileCatalog? nativeMissiles = null,
        AuthoritativeNativeMissileSpriteCatalog? nativeMissileSprites = null)
    {
        this.storeCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        WorldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
        this.storeCommands = storeCommands ?? throw new ArgumentNullException(nameof(storeCommands));
        this.worldCommands = worldCommands ?? throw new ArgumentNullException(nameof(worldCommands));
        this.combatCommands = combatCommands ?? throw new ArgumentNullException(nameof(combatCommands));
        Spells = spells ?? throw new ArgumentNullException(nameof(spells));
        Experience = experience;
        NativeMissiles = nativeMissiles;
        NativeMissileSprites = nativeMissileSprites;
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        CombatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
        PlayerClasses = playerClasses;
        PlayerAnimations = playerAnimations;
        this.monsterSpawnPlanner = monsterSpawnPlanner;
        this.proceduralMonsterSpawns = proceduralMonsterSpawns?.Validate();
        this.proceduralMonsterSeed = proceduralMonsterSeed;
        this.gameMode = gameMode;
        this.difficulty = Enum.IsDefined(difficulty)
            ? difficulty
            : throw new ArgumentOutOfRangeException(nameof(difficulty));
        Items = items;
        this.monsterTypes = monsterTypes;
        this.monsterMissiles = monsterMissiles;
        QuestTypes = questTypes;
        SpellBooks = spellBooks;
        QuestTownerInteractions = questTownerInteractions;
        QuestDialogueStages = questDialogueStages;
        DialogueMessages = dialogueMessages;
        SetLevelObjects = setLevelObjects;
        if (proceduralSetPieceOrigins is not null) {
            foreach (var (setLevelId, origin) in proceduralSetPieceOrigins)
                PublishSetLevelObjectOrigin(setLevelId, origin, materialize: false);
        }
        if (setLevelObjectOrigins is not null) {
            foreach (var (setLevelId, origin) in setLevelObjectOrigins)
                PublishSetLevelObjectOrigin(setLevelId, origin, materialize: false);
        }
        SetLevelMaps = setLevelMaps;
        NativeDunObjects = nativeDunObjects;
        this.nativeDunFragments = nativeDunFragments;
        this.nativeDunFragmentObjects = nativeDunFragmentObjects;
        this.nativeDunMonsters = nativeDunMonsters;
        this.nativeObjectTemplates = nativeObjectTemplates ?? [];
        this.content = content;
        this.proceduralSetPieceOriginCatalog = proceduralSetPieceOriginCatalog;
        this.proceduralSetPieceOriginSeed = proceduralSetPieceOriginSeed;
        MaterializeRelativeSetLevelObjects();
        ApplyDeferredNativeDunFragments();
        MaterializeNativeDunMonsters();
        ReconcileProceduralSpawnState();
        RefreshAuthoredDialogueTargets();
    }

    public DiabloWorldState WorldState { get; }

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; }

    /** Validated spell catalog used for protocol source classification. */
    public AuthoritativeSpellCatalog Spells { get; }

    /** Validated cumulative character-level experience thresholds. */
    public AuthoritativeExperienceCatalog? Experience { get; }

    /** Validated native missile identities and presentation metadata. */
    public AuthoritativeNativeMissileCatalog? NativeMissiles { get; }

    /** Validated native missile sprite dimensions and animation timing. */
    public AuthoritativeNativeMissileSpriteCatalog? NativeMissileSprites { get; }

    /** Validated native dynamic spell-book metadata used for presentation names. */
    public AuthoritativeSpellBookCatalog? SpellBooks { get; }

    /** Validated native AI-to-missile mappings used by snapshot restore validation. */
    public AuthoritativeMonsterMissileCatalog? MonsterMissiles => monsterMissiles;

    /** Validated native quest identity mapping used by compatibility projections. */
    public AuthoritativeQuestTypeCatalog? QuestTypes { get; }

    /** Validated repeatable quest/towner interactions projected to clients. */
    public AuthoritativeQuestTownerInteractionCatalog? QuestTownerInteractions { get; }

    /** Validated staged unique-monster dialogue projected to world commands. */
    public AuthoritativeQuestDialogueStageCatalog? QuestDialogueStages { get; }

    /** Validated native fixed-object placements materialized when geometry is available. */
    public AuthoritativeSetLevelObjectCatalog? SetLevelObjects { get; }

    /** Runtime-generated origins used by relative set-level object placements. */
    public IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin> SetLevelObjectOrigins
        => setLevelObjectOrigins;

    internal AuthoritativeSetLevelObjectOrigin? ResolveSetLevelObjectOrigin(int setLevelId)
        => setLevelObjectOrigins.TryGetValue(setLevelId, out var origin) ? origin : null;

    /** Validated mappings for native DUN object-layer materialization. */
    public AuthoritativeNativeDunObjectCatalog? NativeDunObjects { get; }

    /** Validated mappings for native DUN monster-layer materialization. */
    public AuthoritativeNativeDunMonsterCatalog? NativeDunMonsters
        => nativeDunMonsters;

    /** Validated native set-level map assets and entry points used by activation. */
    public AuthoritativeSetLevelMapCatalog? SetLevelMaps { get; }

    /** Validated server-owned text projected with authored dialogue events. */
    public AuthoritativeDialogueMessageCatalog? DialogueMessages { get; }

    /** Validated content rules governing this Diablo simulation instance. */
    public AuthoritativeCombatRules CombatRules { get; }

    /** Validated item definitions used by snapshot and world identity checks. */
    public AuthoritativeItemCatalog? Items { get; }

    /** Native difficulty selected for this authoritative simulation. */
    public DiabloDifficulty Difficulty => difficulty;

    public uint RandomState => random.State;

    /** Publishes one generated set-piece origin and materializes its authored objects. */
    public IReadOnlyList<AuthoritativeWorldObject> PublishSetLevelObjectOrigin(
        int setLevelId,
        AuthoritativeSetLevelObjectOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var previous = CaptureDynamicOriginPublicationState();
        try {
            var generated = PublishSetLevelObjectOrigin(setLevelId, origin, materialize: true);
            ApplyDeferredNativeDunFragments();
            return generated;
        } catch {
            RestoreDynamicOriginPublicationState(previous);
            throw;
        }
    }

    /**
     * Publishes origins whose generated target levels became available after
     * initial activation. Existing origins are revalidated but not rematerialized.
     */
    public IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>
        PublishAvailableProceduralSetPieceOrigins()
    {
        if (proceduralSetPieceOriginCatalog is null || WorldState.World is null)
            return new Dictionary<int, AuthoritativeSetLevelObjectOrigin>();

        var previous = CaptureDynamicOriginPublicationState();
        try {
            var planned = proceduralSetPieceOriginCatalog.Plan(
                WorldState.World,
                proceduralSetPieceOriginSeed);
            var published = new Dictionary<int, AuthoritativeSetLevelObjectOrigin>();
            foreach (var (setLevelId, origin) in planned) {
                if (setLevelObjectOrigins.TryGetValue(setLevelId, out var existing)) {
                    if (existing != origin)
                        throw new InvalidDataException(
                            $"Procedural set-level origin {setLevelId} conflicts with its published origin.");
                    continue;
                }

                PublishSetLevelObjectOrigin(setLevelId, origin, materialize: false);
                published.Add(setLevelId, origin);
                MaterializeRelativeSetLevelObjects(setLevelId);
            }
            if (published.Count != 0)
                ApplyDeferredNativeDunFragments();
            return published;
        } catch {
            RestoreDynamicOriginPublicationState(previous);
            throw;
        }
    }

    private DynamicOriginPublicationState CaptureDynamicOriginPublicationState()
    {
        return new DynamicOriginPublicationState(
            setLevelObjectOrigins.ToDictionary(pair => pair.Key, pair => pair.Value),
            WorldState.Objects.ToDictionary(pair => pair.Key, pair => pair.Value),
            WorldState.World?.Levels.ToDictionary(level => level.LevelId)
                ?? new Dictionary<uint, AuthoritativeLevel>(),
            appliedNativeDunFragments.ToHashSet());
    }

    private void RestoreDynamicOriginPublicationState(
        DynamicOriginPublicationState previous)
    {
        setLevelObjectOrigins.Clear();
        foreach (var (setLevelId, origin) in previous.Origins)
            setLevelObjectOrigins.Add(setLevelId, origin);

        WorldState.Objects.Clear();
        foreach (var (@object, value) in previous.Objects)
            WorldState.Objects.Add(@object, value);

        appliedNativeDunFragments.Clear();
        appliedNativeDunFragments.UnionWith(previous.AppliedNativeDunFragments);

        if (WorldState.World is { } world)
            foreach (var level in previous.Levels.Values)
                world.ReplaceLevel(level);
        WorldState.RefreshProjectedGeometry();
    }

    private IReadOnlyList<AuthoritativeWorldObject> PublishSetLevelObjectOrigin(
        int setLevelId,
        AuthoritativeSetLevelObjectOrigin origin,
        bool materialize)
    {
        if (setLevelId is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(setLevelId));
        if (WorldState.World is null)
            throw new InvalidDataException(
                "Set-level object origins require an authoritative world geometry catalog.");
        if (!WorldState.World.ContainsLevel(origin.LevelId)
            || !WorldState.World.ContainsPosition(origin.LevelId, origin.PositionX, origin.PositionY))
            throw new InvalidDataException(
                $"Set-level object origin {setLevelId} is outside runtime level {origin.LevelId}.");
        if (setLevelObjectOrigins.TryGetValue(setLevelId, out var existing)) {
            if (existing != origin)
                throw new InvalidDataException(
                    $"Set-level object origin {setLevelId} was already published at a different position.");
            return [];
        }
        setLevelObjectOrigins.Add(setLevelId, origin);
        WorldState.RefreshProjectedGeometry();
        return materialize ? MaterializeRelativeSetLevelObjects(setLevelId) : [];
    }

    private IReadOnlyList<AuthoritativeWorldObject> MaterializeRelativeSetLevelObjects(
        int? setLevelId = null)
    {
        if (SetLevelObjects is null || WorldState.World is null || setLevelObjectOrigins.Count == 0)
            return [];
        var relativeCatalog = SetLevelObjects.Filter(placement =>
            placement.PlacementMode == AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset
            && (!setLevelId.HasValue || placement.SetLevelId == setLevelId.Value));
        if (relativeCatalog.Definitions.Count == 0)
            return [];
        var generated = relativeCatalog.LoadAvailableObjects(
            WorldState.World,
            WorldState.Objects.Values,
            nativeObjectTemplates,
            setLevelObjectOrigins);
        foreach (var @object in generated) {
            if (!WorldState.Objects.TryAdd(@object.EntityId, @object))
                throw new InvalidDataException(
                    $"Set-level object entity {@object.EntityId} is already occupied.");
        }
        if (generated.Count != 0)
            AuthoritativeWorldObjectCatalog.ValidateWorld(
                WorldState.Objects.Values,
                WorldState.World);
        return generated;
    }

    public DiabloIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloStoreIntent intent,
        ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        if (WorldState.VictoryTriggered)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        ConfigurePlayer(player);
        return storeCommands.Execute(player, intent, appliedTick);
    }

    public DiabloWorldIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloWorldIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(players);
        if (WorldState.VictoryTriggered)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        ConfigurePlayer(player);
        var participants = players as IReadOnlyList<AuthoritativePlayerState>
            ?? players.ToArray();
        foreach (var participant in participants)
            ConfigurePlayer(participant);
        var previousLevelId = player.LevelId;
        var result = worldCommands.Execute(player, intent, appliedTick, participants);
        if (result.Succeeded && player.LevelId != previousLevelId)
            PublishAvailableProceduralSetPieceOrigins();
        if (result.Succeeded)
            EnsureProceduralMonsterSpawns(player.LevelId, participants);
        if (result.Succeeded)
            RefreshAuthoredDialogueTargets();
        return result;
    }

    /** Projects the current authored message identity onto pending unique-dialogue targets. */
    private void RefreshAuthoredDialogueTargets()
    {
        if (QuestDialogueStages is null)
            return;

        foreach (var target in WorldState.CombatTargets.Values
                     .Where(target => target.IsUnique
                         && !string.IsNullOrWhiteSpace(target.UniqueIdentity))) {
            var quest = WorldState.Quests.Values
                .Where(quest => QuestDialogueStages.ContainsQuest(quest.QuestId)
                    && (quest.LevelId == 0 || quest.LevelId == target.LevelId)
                    && string.Equals(
                        quest.CompletionUniqueIdentity,
                        target.UniqueIdentity,
                        StringComparison.Ordinal))
                .OrderBy(quest => quest.QuestId)
                .FirstOrDefault();
            if (quest is null) {
                continue;
            }

            if (quest.Unavailable || quest.Completed || !quest.Active) {
                target.DialogueMessageId = 0;
                target.DialogueState = AuthoritativeMonsterDialogueState.Unspecified;
                continue;
            }

            if (QuestDialogueStages.TryGetPresentationStage(
                quest.QuestId,
                quest.Variable1,
                quest.Variable2,
                out var stage)) {
                target.DialogueMessageId = stage.MessageId;
                target.DialogueState = AuthoritativeMonsterDialogueState.Inquiring;
            } else {
                target.DialogueMessageId = 0;
                target.DialogueState = AuthoritativeMonsterDialogueState.Combat;
            }
        }
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
        foreach (var target in targets) {
            DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                target,
                difficulty,
                CombatRules.GameVariant,
                gameMode,
                CombatRules.DifficultyRules);
            DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                target,
                difficulty,
                monsterTypes);
            WorldState.CombatTargets.Add(target.EntityId, target);
        }
        ApplyDeferredNativeDunFragments();
        MaterializeNativeDunMonsters();
        if (targets.Count != 0)
            nextGeneratedMonsterEntityId = checked(targets[^1].EntityId + 1);
        RefreshAuthoredDialogueTargets();
    }

    /** Materializes mapped native DUN monster cells after deferred geometry appears. */
    private void MaterializeNativeDunMonsters()
    {
        if (nativeDunMonsters is null
            || monsterSpawnPlanner is null
            || WorldState.World is null)
            return;
        var targets = nativeDunMonsters.Materialize(
            WorldState.World,
            monsterSpawnPlanner,
            WorldState.CombatTargets.Values,
            WorldState.Objects.Values,
            proceduralMonsterSeed,
            gameMode == DiabloGameMode.Multiplayer);
        foreach (var target in targets) {
            DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                target,
                difficulty,
                CombatRules.GameVariant,
                gameMode,
                CombatRules.DifficultyRules);
            DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                target,
                difficulty,
                monsterTypes);
            if (!WorldState.CombatTargets.TryAdd(target.EntityId, target))
                throw new InvalidDataException(
                    $"Native DUN monster entity {target.EntityId} is already occupied.");
        }
        if (targets.Count != 0)
            ApplyDeferredNativeDunFragments();
        RefreshAuthoredDialogueTargets();
    }

    /** Applies unique-anchored fragments once their generated encounter exists. */
    private void ApplyDeferredNativeDunFragments()
    {
        if (nativeDunFragments is null
            || content is null
            || WorldState.World is null)
            return;
        var targetPool = WorldState.CombatTargets.Values.ToArray();
        var candidates = nativeDunFragments.Definitions
            .Where(definition => definition.AnchorEntityId == 0
                && definition.AnchorPositionX is null
                && !appliedNativeDunFragments.Contains(definition.FragmentId))
            .Where(definition => {
                if (definition.AnchorSetLevelId is { } setLevelId)
                    return setLevelObjectOrigins.ContainsKey(setLevelId);
                var matches = targetPool.Where(target => target.IsUnique
                    && target.LevelId == definition.LevelId
                    && string.Equals(
                        target.UniqueIdentity,
                        definition.AnchorUniqueIdentity,
                        StringComparison.Ordinal)).ToArray();
                if (matches.Length > 1)
                    throw new InvalidDataException(
                        $"Native DUN fragment {definition.FragmentId} unique anchor "
                        + $"'{definition.AnchorUniqueIdentity}' must match exactly one encounter; found {matches.Length}.");
                return matches.Length == 1;
            })
            .ToArray();
        if (candidates.Length == 0)
            return;
        var catalog = new AuthoritativeNativeDunFragmentCatalog(candidates);
        if (nativeDunFragmentObjects is not null) {
            var candidateIds = candidates
                .Select(candidate => candidate.FragmentId)
                .ToHashSet();
            var generatedObjects = nativeDunFragmentObjects
                .Filter(placement => candidateIds.Contains(placement.FragmentId))
                .Materialize(
                    WorldState.World,
                    WorldState.Objects.Values,
                    nativeObjectTemplates,
                    catalog,
                    targetPool,
                    setLevelObjectOrigins);
            foreach (var generatedObject in generatedObjects) {
                if (WorldState.Objects.TryGetValue(generatedObject.EntityId, out var existing)) {
                    if (!existing.HasSameAuthoritativeState(generatedObject))
                        throw new InvalidDataException(
                            $"Deferred native DUN fragment object entity {generatedObject.EntityId} is already occupied with different state.");
                    continue;
                }
                WorldState.Objects.Add(generatedObject.EntityId, generatedObject);
            }
            AuthoritativeWorldObjectCatalog.ValidateWorld(
                WorldState.Objects.Values,
                WorldState.World);
        }
        catalog.Apply(
            content,
            WorldState.World,
            WorldState.Objects.Values,
            targetPool,
            setLevelObjectOrigins);
        WorldState.RefreshProjectedGeometry();
        // The fragment may have introduced mapped native-DUN object cells.
        // Reconcile after geometry publication so deferred set-piece objects
        // enter the same authoritative object catalog as initial mappings.
        WorldState.ReconcileNativeDunObjects();
        foreach (var candidate in candidates)
            appliedNativeDunFragments.Add(candidate.FragmentId);
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
        return WorldState.IsEntityIdOccupied(entityId)
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
        foreach (var corpse in WorldState.Corpses.Values.Where(corpse => corpse.LevelId == levelId))
            reserved.Add((corpse.PositionX, corpse.PositionY));
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

    /** Rebuilds transient procedural tracking from the restored world state. */
    private void ResetProceduralSpawnState()
    {
        appliedNativeDunFragments.Clear();
        materializedProceduralLevels.Clear();
        nextGeneratedMonsterEntityId = AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId;
        ReconcileProceduralSpawnState();
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        if (WorldState.VictoryTriggered)
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        ConfigurePlayer(player);
        return combatCommands.Execute(player, intent, appliedTick, [player]);
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(players);
        if (WorldState.VictoryTriggered)
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        ConfigurePlayer(player);
        foreach (var participant in players)
            ConfigurePlayer(participant);
        return combatCommands.Execute(player, intent, appliedTick, players);
    }

    public void AdvanceStatusEffects(AuthoritativePlayerState player, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (WorldState.VictoryTriggered)
            return;
        ConfigurePlayer(player);
        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, tick);
    }

    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTick(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (WorldState.VictoryTriggered)
            return [];
        foreach (var participant in players)
            ConfigurePlayer(participant);
        return combatCommands.AdvanceTick(tick, players);
    }

    private void ConfigurePlayer(AuthoritativePlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        worldCommands.EnsureLevelEntryState(player.LevelId);
        player.ConfigureMaximumAttribute(CombatRules.MaximumAttribute);
    }
}
