using Devilution.Data;

namespace Devilution.Diablo;

/** Optional server-owned encounter policy used during content activation. */
public sealed record DiabloMonsterSpawnConfiguration(
    int MaximumMonsters,
    uint? LevelId = null)
{
    public DiabloMonsterSpawnConfiguration Validate()
    {
        if (MaximumMonsters < 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumMonsters));
        return this;
    }
}

/** Fully validated module-owned game state created from composed content. */
public sealed record DiabloContentActivation
{
    public DiabloContentActivation(
        DiabloSimulation simulation,
        IDiabloStoreRules storeRules,
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativePlayerAnimationCatalog playerAnimations,
        AuthoritativePlayerLoadoutCatalog playerLoadouts,
        uint defaultStartingLevelId,
        AuthoritativeLevelGenerationCatalog? levelGeneration = null,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        AuthoritativeQuestPoolCatalog? questPools = null,
        AuthoritativeSetLevelNameCatalog? setLevelNames = null,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null)
    {
        Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        StoreRules = storeRules ?? throw new ArgumentNullException(nameof(storeRules));
        PlayerClasses = playerClasses
            ?? throw new ArgumentNullException(nameof(playerClasses));
        PlayerAnimations = playerAnimations
            ?? throw new ArgumentNullException(nameof(playerAnimations));
        PlayerLoadouts = playerLoadouts
            ?? throw new ArgumentNullException(nameof(playerLoadouts));
        DefaultStartingLevelId = defaultStartingLevelId;
        LevelGeneration = levelGeneration;
        QuestTypes = questTypes;
        QuestPools = questPools;
        SetLevelNames = setLevelNames;
        MonsterSpawnPlanner = monsterSpawnPlanner;
        MonsterMissiles = monsterMissiles;
    }

    public DiabloSimulation Simulation { get; }

    public IDiabloStoreRules StoreRules { get; }

    public AuthoritativePlayerClassCatalog PlayerClasses { get; }

    public AuthoritativePlayerAnimationCatalog PlayerAnimations { get; }

    public AuthoritativePlayerLoadoutCatalog PlayerLoadouts { get; }

    public uint DefaultStartingLevelId { get; }

    /** Native level-range rules reserved for server-owned map generation. */
    public AuthoritativeLevelGenerationCatalog? LevelGeneration { get; }

    public AuthoritativeQuestTypeCatalog? QuestTypes { get; }

    public AuthoritativeQuestPoolCatalog? QuestPools { get; }

    public AuthoritativeSetLevelNameCatalog? SetLevelNames { get; }

    /** Server-owned level spawn planning from the activated content contract. */
    public AuthoritativeMonsterSpawnPlanner? MonsterSpawnPlanner { get; }

    /** Validated native AI-to-missile mappings from the active content pack. */
    public AuthoritativeMonsterMissileCatalog? MonsterMissiles { get; }
}

/** Engine capability that validates content and creates one Diablo game state. */
public interface IDiabloContentBootstrap
{
    ContentSchemaIdentity Schema { get; }

    DiabloContentActivation Activate(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null);
}

/** First-party content activation kept entirely inside the Diablo module. */
internal sealed class DiabloContentBootstrap : IDiabloContentBootstrap
{
    private readonly StoreServicePricing fallbackPricing;
    private readonly IDiabloSimulationFactory simulationFactory;

    public DiabloContentBootstrap(
        StoreServicePricing fallbackPricing,
        IDiabloSimulationFactory simulationFactory)
    {
        this.fallbackPricing = fallbackPricing
            ?? throw new ArgumentNullException(nameof(fallbackPricing));
        this.simulationFactory = simulationFactory
            ?? throw new ArgumentNullException(nameof(simulationFactory));
    }

    public ContentSchemaIdentity Schema => DiabloContentSchema.Current;

    public DiabloContentActivation Activate(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        monsterSpawns?.Validate();
        if (content.Manifest.Schema is not null
            && content.Manifest.Schema != Schema) {
            throw new InvalidDataException(
                $"Content schema '{content.Manifest.Schema}' is incompatible with "
                + $"the Diablo module schema '{Schema}'.");
        }

        var pricing = content.TryGetTable("store_services.tsv", out var pricingTable)
            ? StoreServicePricing.LoadTsv(
                pricingTable.SourcePath,
                pricingTable.ToCanonicalTsv())
            : fallbackPricing;
        var generationRules = content.TryGetTable(
            "item_generation.tsv",
            out var generationTable)
            ? AuthoritativeItemGenerationRules.LoadTsv(
                generationTable.SourcePath,
                generationTable.ToCanonicalTsv())
            : AuthoritativeItemGenerationRules.Default;
        var playerClassTable = content.GetRequiredTable("classes.tsv");
        var playerClasses = AuthoritativePlayerClassCatalog.LoadTsv(
            playerClassTable.SourcePath,
            playerClassTable.ToCanonicalTsv());
        var playerAnimationTable = content.GetRequiredTable("class_animations.tsv");
        var playerAnimations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            playerAnimationTable.SourcePath,
            playerAnimationTable.ToCanonicalTsv(),
            playerClasses);
        var spells = content.TryGetTable("spells.tsv", out var spellsTable)
            ? AuthoritativeSpellCatalog.LoadTsv(
                spellsTable.SourcePath,
                spellsTable.ToCanonicalTsv())
            : null;

        var hasLegacyPrefixes = content.TryGetTable(
            "item_prefixes.tsv",
            out var legacyPrefixTable);
        var hasLegacySuffixes = content.TryGetTable(
            "item_suffixes.tsv",
            out var legacySuffixTable);
        var affixes = hasLegacyPrefixes && hasLegacySuffixes
            ? AuthoritativeItemAffixCatalog.LoadLegacyTsv(
                legacyPrefixTable.SourcePath,
                legacyPrefixTable.ToCanonicalTsv(),
                legacySuffixTable.SourcePath,
                legacySuffixTable.ToCanonicalTsv(),
                generationRules)
            : content.TryGetTable("item_affixes.tsv", out var affixTable)
                ? AuthoritativeItemAffixCatalog.LoadTsv(
                    affixTable.SourcePath,
                    affixTable.ToCanonicalTsv(),
                    generationRules)
                : null;

        var itemCatalog = content.TryGetTable("items.tsv", out var itemTable)
            ? AuthoritativeItemCatalog.LoadTsv(
                itemTable.SourcePath,
                itemTable.ToCanonicalTsv(),
                affixes)
            : null;
        // Normalized IDs take precedence. The legacy table remains a fallback
        // and intentionally derives compatibility IDs from its own row order.
        if (itemCatalog is null
            && content.TryGetTable("itemdat.tsv", out var legacyItemTable)) {
            itemCatalog = AuthoritativeItemCatalog.LoadLegacyTsv(
                legacyItemTable.SourcePath,
                legacyItemTable.ToCanonicalTsv(),
                affixes,
                null,
                spells);
        }
        if (itemCatalog is null) {
            throw new InvalidDataException(
                "Active Diablo content must provide items.tsv or itemdat.tsv.");
        }
        var combatRules = content.TryGetTable("combat.tsv", out var combatTable)
            ? AuthoritativeCombatRules.LoadTsv(
                combatTable.SourcePath,
                combatTable.ToCanonicalTsv())
            : new AuthoritativeCombatRules(10, 1, 100);
        var playerLoadoutTable = content.GetRequiredTable("class_loadouts.tsv");
        var playerLoadoutItemTable = content.GetRequiredTable(
            "class_loadout_items.tsv");
        var playerLoadouts = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            playerClasses,
            itemCatalog,
            combatRules,
            playerLoadoutTable.SourcePath,
            playerLoadoutTable.ToCanonicalTsv(),
            playerLoadoutItemTable.SourcePath,
            playerLoadoutItemTable.ToCanonicalTsv());

        var uniqueItems = content.TryGetTable("unique_items.tsv", out var uniqueTable)
            ? AuthoritativeUniqueItemCatalog.LoadTsv(
                uniqueTable.SourcePath,
                uniqueTable.ToCanonicalTsv())
            : itemCatalog is not null
                && content.TryGetTable("unique_itemdat.tsv", out var legacyUniqueTable)
                ? AuthoritativeUniqueItemCatalog.LoadLegacyTsv(
                    legacyUniqueTable.SourcePath,
                    legacyUniqueTable.ToCanonicalTsv(),
                    itemCatalog)
                : null;
        if (itemCatalog is not null && uniqueItems is not null)
            itemCatalog.AttachUniqueCatalog(uniqueItems);

        var storeTable = content.GetRequiredTable("stores.tsv");
        var stores = StoreCatalog.LoadTsv(
            storeTable.SourcePath,
            storeTable.ToCanonicalTsv(),
            itemCatalog,
            uniqueItems);
        var stableIds = content.TryGetTable("content_ids.tsv", out var stableIdsTable)
            ? StableContentIdCatalog.LoadTsv(
                stableIdsTable.SourcePath,
                stableIdsTable.ToCanonicalTsv())
            : null;
        var identifyAbilitySpellId = stableIds?.Resolve("spell", "identify") ?? 0;
        var repairAbilitySpellId = stableIds?.Resolve("spell", "repair") ?? 0;
        var rechargeAbilitySpellId = stableIds?.Resolve("spell", "recharge") ?? 0;
        var trapDisarmAbilitySpellId = stableIds?.Resolve("spell", "trap_disarm") ?? 0;
        var world = content.TryGetTable("levels.tsv", out var levelsTable)
            ? AuthoritativeWorld.LoadTsv(
                levelsTable.SourcePath,
                levelsTable.ToCanonicalTsv())
            : null;
        var levelGeneration = content.TryGetTable(
            "level_generation.tsv",
            out var levelGenerationTable)
            ? AuthoritativeLevelGenerationCatalog.LoadTsv(
                levelGenerationTable.SourcePath,
                levelGenerationTable.ToCanonicalTsv())
            : null;
        if (world is not null && content.TryGetTable("warps.tsv", out var warpsTable)) {
            foreach (var warp in AuthoritativeWarpCatalog.LoadTsv(
                         warpsTable.SourcePath,
                         warpsTable.ToCanonicalTsv(),
                         world)) {
                world.AddWarpTarget(warp);
            }
        }
        playerLoadouts.ValidateCastSources(spells);
        if (world is not null)
            combatRules.ValidateWorld(world);
        var monsterTypes = content.TryGetTable("monster_types.tsv", out var monsterTypesTable)
            ? AuthoritativeMonsterTypeCatalog.LoadTsv(
                monsterTypesTable.SourcePath,
                monsterTypesTable.ToCanonicalTsv())
            : null;
        var monsterMissiles = content.TryGetTable(
            "monster_missiles.tsv",
            out var monsterMissilesTable)
            ? AuthoritativeMonsterMissileCatalog.LoadTsv(
                monsterMissilesTable.SourcePath,
                monsterMissilesTable.ToCanonicalTsv())
            : null;
        var monsters = content.TryGetTable("monsters.tsv", out var monstersTable)
            ? AuthoritativeMonsterCatalog.LoadTsv(
                monstersTable.SourcePath,
                monstersTable.ToCanonicalTsv(),
                itemCatalog,
                uniqueItems,
                monsterTypes)
            : null;
        var portals = content.TryGetTable("portals.tsv", out var portalsTable)
            ? AuthoritativePortalCatalog.LoadTsv(
                portalsTable.SourcePath,
                portalsTable.ToCanonicalTsv())
            : null;
        var objects = content.TryGetTable("objects.tsv", out var objectsTable)
            ? AuthoritativeWorldObjectCatalog.LoadTsv(
                objectsTable.SourcePath,
                objectsTable.ToCanonicalTsv())
            : null;
        var setLevelNames = content.TryGetTable(
            "set_level_names.tsv",
            out var setLevelNamesTable)
            ? AuthoritativeSetLevelNameCatalog.LoadTsv(
                setLevelNamesTable.SourcePath,
                setLevelNamesTable.ToCanonicalTsv())
            : null;
        var questTypes = content.TryGetTable("quest_types.tsv", out var questTypesTable)
            ? AuthoritativeQuestTypeCatalog.LoadTsv(
                questTypesTable.SourcePath,
                questTypesTable.ToCanonicalTsv(),
                setLevelNames)
            : null;
        var questPools = content.TryGetTable("quest_pools.tsv", out var questPoolsTable)
            ? AuthoritativeQuestPoolCatalog.LoadTsv(
                questPoolsTable.SourcePath,
                questPoolsTable.ToCanonicalTsv(),
                questTypes)
            : null;
        var quests = content.TryGetTable("quests.tsv", out var questsTable)
            ? AuthoritativeQuestCatalog.LoadTsv(
                questsTable.SourcePath,
                questsTable.ToCanonicalTsv(),
                questTypes)
            : null;
        if (gameMode == DiabloGameMode.SinglePlayer
            && quests is not null
            && questPools is not null) {
            AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(
                quests,
                questPools,
                randomSeed);
        }

        var defaultStartingLevelId = world?.DefaultStartingLevelId
            ?? world?.Levels.FirstOrDefault()?.LevelId
            ?? stableIds?.Resolve("level", "town")
            ?? 0;
        var monsterSpawnPlanner = world is not null
            && monsterTypes is not null
            && levelGeneration is not null
            ? new AuthoritativeMonsterSpawnPlanner(
                world,
                monsterTypes,
                levelGeneration,
                itemCatalog,
                monsters)
            : null;
        IReadOnlyList<AuthoritativeCombatTarget>? combatTargets = monsters?.Targets;
        if (monsterSpawns is not null) {
            if (monsterSpawnPlanner is null)
                throw new InvalidDataException(
                    "Procedural monster spawning requires levels.tsv, level_generation.tsv, and monster_types.tsv.");
            combatTargets = monsterSpawnPlanner.PlanCombatTargets(
                monsterSpawns.LevelId ?? defaultStartingLevelId,
                randomSeed,
                gameMode == DiabloGameMode.Multiplayer,
                monsterSpawns.MaximumMonsters,
                reservedPositions: GetReservedPositions(
                    monsterSpawns.LevelId ?? defaultStartingLevelId,
                    world,
                    portals,
                    objects));
        }

        var rules = new DiabloStoreRules(pricing);
        var simulation = simulationFactory.Create(new DiabloSimulationOptions(stores, rules) {
            World = world,
            CombatTargets = combatTargets,
            Portals = portals,
            Objects = objects,
            Quests = quests,
            QuestTypes = questTypes,
            Spells = spells,
            CombatRules = combatRules,
            Items = itemCatalog,
            IdentifyAbilitySpellId = identifyAbilitySpellId,
            RepairAbilitySpellId = repairAbilitySpellId,
            RechargeAbilitySpellId = rechargeAbilitySpellId,
            TrapDisarmAbilitySpellId = trapDisarmAbilitySpellId,
            PlayerClasses = playerClasses,
            PlayerAnimations = playerAnimations,
            MonsterMissiles = monsterMissiles,
            UseNativePlayerActionTiming = true,
            RandomSeed = randomSeed,
            GameMode = gameMode,
            MonsterSpawnPlanner = monsterSpawnPlanner,
            ProceduralMonsterSpawns = monsterSpawns,
        });
        return new DiabloContentActivation(
            simulation,
            rules,
            playerClasses,
            playerAnimations,
            playerLoadouts,
            defaultStartingLevelId,
            levelGeneration,
            questTypes,
            questPools,
            setLevelNames,
            monsterSpawnPlanner,
            monsterMissiles);
    }

    private static IReadOnlySet<(int X, int Y)> GetReservedPositions(
        uint levelId,
        AuthoritativeWorld? world,
        IReadOnlyList<AuthoritativePortal>? portals,
        IReadOnlyList<AuthoritativeWorldObject>? objects)
    {
        var reserved = new HashSet<(int X, int Y)>();
        if (world is not null) {
            foreach (var warp in world.WarpTargets) {
                if (warp.SourceLevelId == levelId)
                    reserved.Add((warp.TriggerX, warp.TriggerY));
                if (warp.TargetLevelId == levelId)
                    reserved.Add((warp.TargetX, warp.TargetY));
            }
        }
        foreach (var portal in portals ?? []) {
            if (portal.SourceLevelId == levelId)
                reserved.Add((portal.SourcePositionX, portal.SourcePositionY));
            if (portal.DestinationLevelId == levelId)
                reserved.Add((portal.DestinationPositionX, portal.DestinationPositionY));
        }
        foreach (var @object in objects ?? []) {
            if (!@object.Activated
                && !@object.Destroyed
                && @object.LevelId == levelId) {
                reserved.Add((@object.PositionX, @object.PositionY));
            }
        }
        return reserved;
    }
}
