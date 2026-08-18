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
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null,
        uint adriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId,
        AuthoritativeWorldRules? worldRules = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeQuestTownerInteractionCatalog? questTownerInteractions = null,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null,
        AuthoritativeDialogueMessageCatalog? dialogueMessages = null)
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
        AdriaStoreId = adriaStoreId;
        WorldRules = worldRules ?? AuthoritativeWorldRules.NativeDefaults;
        SpellBooks = spellBooks;
        QuestTownerInteractions = questTownerInteractions;
        QuestDialogueStages = questDialogueStages;
        DialogueMessages = dialogueMessages;
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

    /** Service-only store identity supplied by the active world rules. */
    public uint AdriaStoreId { get; }

    /** Validated world identities and optional Town Portal destination geometry. */
    public AuthoritativeWorldRules WorldRules { get; }

    /** Validated native dynamic spell-book metadata from the active content pack. */
    public AuthoritativeSpellBookCatalog? SpellBooks { get; }

    /** Validated repeatable quest/towner interactions from the active content pack. */
    public AuthoritativeQuestTownerInteractionCatalog? QuestTownerInteractions { get; }

    /** Validated staged unique-monster dialogue from the active content pack. */
    public AuthoritativeQuestDialogueStageCatalog? QuestDialogueStages { get; }

    /** Validated server-owned text for authored dialogue events. */
    public AuthoritativeDialogueMessageCatalog? DialogueMessages { get; }
}

/** Engine capability that validates content and creates one Diablo game state. */
public interface IDiabloContentBootstrap
{
    ContentSchemaIdentity Schema { get; }

    DiabloContentActivation Activate(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal);
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
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal)
    {
        ArgumentNullException.ThrowIfNull(content);
        monsterSpawns?.Validate();
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (content.Manifest.Schema is not null
            && content.Manifest.Schema != Schema) {
            throw new InvalidDataException(
                $"Content schema '{content.Manifest.Schema}' is incompatible with "
                + $"the Diablo module schema '{Schema}'.");
        }

        var stableIds = content.TryGetTable("content_ids.tsv", out var stableIdsTable)
            ? StableContentIdCatalog.LoadTsv(
                stableIdsTable.SourcePath,
                stableIdsTable.ToCanonicalTsv())
            : null;
        var pricing = content.TryGetTable("store_services.tsv", out var pricingTable)
            ? StoreServicePricing.LoadTsv(
                pricingTable.SourcePath,
                pricingTable.ToCanonicalTsv(),
                stableIds)
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
            playerClassTable.ToCanonicalTsv(),
            stableIds);
        if (stableIds is not null)
            stableIds.ValidateReferences("class", classId => playerClasses.TryGet(classId, out _));
        var playerAnimationTable = content.GetRequiredTable("class_animations.tsv");
        var playerAnimations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            playerAnimationTable.SourcePath,
            playerAnimationTable.ToCanonicalTsv(),
            playerClasses,
            stableIds);
        var spells = content.TryGetTable("spells.tsv", out var spellsTable)
            ? AuthoritativeSpellCatalog.LoadTsv(
                spellsTable.SourcePath,
                spellsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (spells is not null)
            pricing.ValidateSpellReferences(spells);
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
        var cursorCatalog = content.TryGetTable(
            "item_cursor_graphics.tsv",
            out var cursorTable)
            ? AuthoritativeItemCursorCatalog.LoadTsv(
                cursorTable.SourcePath,
                cursorTable.ToCanonicalTsv())
            : null;

        var itemCatalog = content.TryGetTable("items.tsv", out var itemTable)
            ? AuthoritativeItemCatalog.LoadTsv(
                itemTable.SourcePath,
                itemTable.ToCanonicalTsv(),
                affixes,
                null,
                cursorCatalog,
                stableIds)
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
                spells,
                cursorCatalog);
        }
        if (itemCatalog is null) {
            throw new InvalidDataException(
                "Active Diablo content must provide items.tsv or itemdat.tsv.");
        }
        stableIds?.ValidateReferences("item", itemId => itemCatalog.TryGet(itemId, out _));
        var spellBooks = content.TryGetTable("spell_books.tsv", out var spellBooksTable)
            ? AuthoritativeSpellBookCatalog.LoadTsv(
                spellBooksTable.SourcePath,
                spellBooksTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (spellBooks is not null) {
            if (spells is null)
                throw new InvalidDataException(
                    "Spell-book metadata requires an active spell catalog.");
            spellBooks.Validate(spells, itemCatalog);
        }
        var itemCombinations = content.TryGetTable(
            "item_combinations.tsv",
            out var itemCombinationsTable)
            ? AuthoritativeItemCombinationCatalog.LoadTsv(
                itemCombinationsTable.SourcePath,
                itemCombinationsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        itemCombinations?.ValidateItems(itemCatalog);
        var worldRules = content.TryGetTable("world_rules.tsv", out var worldRulesTable)
            ? AuthoritativeWorldRules.LoadTsv(
                worldRulesTable.SourcePath,
                worldRulesTable.ToCanonicalTsv(),
                stableIds)
            : AuthoritativeWorldRules.NativeDefaults;
        var combatRules = content.TryGetTable("combat.tsv", out var combatTable)
            ? AuthoritativeCombatRules.LoadTsv(
                combatTable.SourcePath,
                combatTable.ToCanonicalTsv(),
                worldRules.TownLevelId,
                stableIds)
            : new AuthoritativeCombatRules(
                10,
                1,
                100,
                townLevelId: worldRules.TownLevelId);
        combatRules.ValidateItems(itemCatalog);
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
            playerLoadoutItemTable.ToCanonicalTsv(),
            stableIds);

        var uniqueItems = content.TryGetTable("unique_items.tsv", out var uniqueTable)
            ? AuthoritativeUniqueItemCatalog.LoadTsv(
                uniqueTable.SourcePath,
                uniqueTable.ToCanonicalTsv(),
                cursorCatalog,
                stableIds)
            : itemCatalog is not null
                && content.TryGetTable("unique_itemdat.tsv", out var legacyUniqueTable)
                ? AuthoritativeUniqueItemCatalog.LoadLegacyTsv(
                    legacyUniqueTable.SourcePath,
                    legacyUniqueTable.ToCanonicalTsv(),
                    itemCatalog,
                    cursorCatalog)
                : null;
        if (itemCatalog is not null && uniqueItems is not null)
            itemCatalog.AttachUniqueCatalog(uniqueItems);
        if (uniqueItems is not null)
            uniqueItems.ValidateBaseItems(itemCatalog!);
        if (stableIds is not null && uniqueItems is not null)
            stableIds.ValidateReferences(
                "unique",
                uniqueId => uniqueItems.TryGet(uniqueId, out _));

        var storeTable = content.GetRequiredTable("stores.tsv");
        var stores = StoreCatalog.LoadTsv(
            storeTable.SourcePath,
            storeTable.ToCanonicalTsv(),
            itemCatalog,
            uniqueItems,
            stableIds,
            spellBooks,
            gameMode);
        if (stableIds is not null)
            stableIds.ValidateReferences(
                "store",
                storeId => stores.ContainsStore(storeId)
                    || storeId == worldRules.AdriaStoreId);
        worldRules.ValidateStores(stores);
        if (spells is not null)
            stores.ValidateSpellReferences(spells);
        var world = content.TryGetTable("levels.tsv", out var levelsTable)
            ? AuthoritativeWorld.LoadTsv(
                levelsTable.SourcePath,
                levelsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (content.TryGetTable(
                "procedural_levels.tsv",
                out var proceduralLevelsTable)) {
            var proceduralLevels = AuthoritativeProceduralLevelCatalog.LoadTsv(
                proceduralLevelsTable.SourcePath,
                proceduralLevelsTable.ToCanonicalTsv());
            var generatedWorld = proceduralLevels.GenerateWorld(randomSeed);
            if (world is null) {
                world = generatedWorld;
            } else {
                foreach (var level in generatedWorld.Levels)
                    world.AddLevel(level);
            }
        }
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
                         world,
                         stableIds)) {
                world.AddWarpTarget(warp);
            }
        }
        if (stableIds is not null && world is not null)
            stableIds.ValidateReferences(
                "warp",
                warpId => world.WarpTargets.Any(warp => warp.WarpId == warpId));
        if (world is not null && levelGeneration is not null)
            levelGeneration.ValidateWorld(world);
        playerLoadouts.ValidateCastSources(spells);
        if (spells is not null && stableIds is not null)
            spells.ValidateStableReferences(
                stableIds,
                playerLoadouts.Definitions.Select(definition => definition.AbilitySpellId));
        var identifyAbilitySpellId = ResolveAbilityId(stableIds, "identify");
        var repairAbilitySpellId = ResolveAbilityId(stableIds, "repair");
        var rechargeAbilitySpellId = ResolveAbilityId(stableIds, "recharge");
        var trapDisarmAbilitySpellId = ResolveAbilityId(stableIds, "trap_disarm");
        var monsterFlashSpellId = ResolveSpellId(stableIds, "flash");
        if (spells is not null)
            itemCatalog!.ValidateSpellReferences(spells);
        if (world is not null)
            worldRules.ValidateWorld(world);
        if (world is not null)
            combatRules.ValidateWorld(world);
        var monsterTypes = content.TryGetTable("monster_types.tsv", out var monsterTypesTable)
            ? AuthoritativeMonsterTypeCatalog.LoadTsv(
                monsterTypesTable.SourcePath,
                monsterTypesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (stableIds is not null && monsterTypes is not null)
            stableIds.ValidateReferences("monster", monsterId => monsterTypes.TryGet(monsterId, out _));
        var monsterMissiles = content.TryGetTable(
            "monster_missiles.tsv",
            out var monsterMissilesTable)
            ? AuthoritativeMonsterMissileCatalog.LoadTsv(
                monsterMissilesTable.SourcePath,
                monsterMissilesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (monsterMissiles is not null && spells is not null)
            monsterMissiles.ValidateSpellReferences(spells);
        if (monsterMissiles is not null && monsterTypes is not null) {
            monsterMissiles.ValidateMonsterSelectorReferences(monsterTypes);
            monsterMissiles.ValidateSpawnMonsterReferences(monsterTypes);
        }
        var monsters = content.TryGetTable("monsters.tsv", out var monstersTable)
            ? AuthoritativeMonsterCatalog.LoadTsv(
                monstersTable.SourcePath,
                monstersTable.ToCanonicalTsv(),
                itemCatalog,
                uniqueItems,
                monsterTypes,
                stableIds,
                spellBooks,
                gameMode)
            : null;
        if (world is not null && monsters is not null)
            AuthoritativeMonsterCatalog.ValidateWorld(monsters.Targets, world);
        var uniqueMonsters = content.TryGetTable(
            "unique_monsters.tsv",
            out var uniqueMonstersTable)
            ? AuthoritativeUniqueMonsterCatalog.LoadTsv(
                uniqueMonstersTable.SourcePath,
                uniqueMonstersTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (uniqueMonsters is not null) {
            if (monsters is null)
                throw new InvalidDataException(
                    "Unique monster identities require a monsters.tsv table.");
            uniqueMonsters.ApplyTo(monsters.Targets);
        }
        var portals = content.TryGetTable("portals.tsv", out var portalsTable)
            ? AuthoritativePortalCatalog.LoadTsv(
                portalsTable.SourcePath,
                portalsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (stableIds is not null && world is not null)
            stableIds.ValidateReferences("level", world.ContainsLevel);
        if (stableIds is not null && portals is not null)
            stableIds.ValidateReferences(
                "portal",
                portalId => portals.Any(portal => portal.PortalId == portalId));
        if (world is not null && portals is not null)
            AuthoritativePortalCatalog.ValidateWorld(portals, world);
        var objects = content.TryGetTable("objects.tsv", out var objectsTable)
            ? AuthoritativeWorldObjectCatalog.LoadTsv(
                objectsTable.SourcePath,
                objectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var worldMutations = content.TryGetTable(
            "world_mutations.tsv",
            out var worldMutationsTable)
            ? AuthoritativeWorldMutationCatalog.LoadTsv(
                worldMutationsTable.SourcePath,
                worldMutationsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (stableIds is not null && objects is not null)
            stableIds.ValidateReferences(
                "object",
                objectId => objects.Any(@object => @object.ObjectId == objectId));
        if (world is not null && objects is not null)
            AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
        if (objects is not null)
            AuthoritativeWorldObjectCatalog.ValidateItemRequirements(objects, itemCatalog!);
        if (objects is not null)
            AuthoritativeWorldObjectCatalog.ValidateRewardRequirements(objects, itemCatalog!);
        if (objects is not null && spells is not null)
            AuthoritativeWorldObjectCatalog.ValidateSpellRequirements(objects, spells);
        if (objects?.Any(@object => @object.EffectKind
                == AuthoritativeObjectEffectKind.GrantSpellLevel) == true
            && spells is null) {
            throw new InvalidDataException(
                "Spell-level grant objects require an active spell catalog.");
        }
        if (world is not null && worldMutations is not null)
            worldMutations.ValidateWorld(world);
        if (objects?.Any(@object => @object.EffectKind
                == AuthoritativeObjectEffectKind.WorldMutation) == true
            && worldMutations is null) {
            throw new InvalidDataException(
                "World-mutation objects require a world_mutations.tsv content table.");
        }
        if (objects is not null && worldMutations is not null)
            AuthoritativeWorldObjectCatalog.ValidateMutationRequirements(objects, worldMutations);
        var setLevelNames = content.TryGetTable(
            "set_level_names.tsv",
            out var setLevelNamesTable)
            ? AuthoritativeSetLevelNameCatalog.LoadTsv(
                setLevelNamesTable.SourcePath,
                setLevelNamesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (stableIds is not null && setLevelNames is not null)
            stableIds.ValidateReferences(
                "set_level",
                setLevelId => setLevelId <= int.MaxValue
                    && setLevelNames.TryGet((int)setLevelId, out _));
        var questTypes = content.TryGetTable("quest_types.tsv", out var questTypesTable)
            ? AuthoritativeQuestTypeCatalog.LoadTsv(
                questTypesTable.SourcePath,
                questTypesTable.ToCanonicalTsv(),
                setLevelNames,
                stableIds)
            : null;
        if (stableIds is not null && questTypes is not null)
            stableIds.ValidateReferences("quest", questId => questTypes.TryGet(questId, out _));
        var questPools = content.TryGetTable("quest_pools.tsv", out var questPoolsTable)
            ? AuthoritativeQuestPoolCatalog.LoadTsv(
                questPoolsTable.SourcePath,
                questPoolsTable.ToCanonicalTsv(),
                questTypes,
                stableIds)
            : null;
        var quests = content.TryGetTable("quests.tsv", out var questsTable)
            ? AuthoritativeQuestCatalog.LoadTsv(
                questsTable.SourcePath,
                questsTable.ToCanonicalTsv(),
                questTypes,
                stableIds)
            : null;
        if (content.TryGetTable("quest_states.tsv", out var questStatesTable)) {
            var additionalQuests = AuthoritativeQuestCatalog.LoadTsv(
                questStatesTable.SourcePath,
                questStatesTable.ToCanonicalTsv(),
                questTypes,
                stableIds);
            quests = quests is null
                ? additionalQuests
                : AuthoritativeQuestCatalog.Merge(
                    quests,
                    additionalQuests,
                    questStatesTable.SourcePath);
        }

        var questTownerInteractions = content.TryGetTable(
            "quest_towner_interactions.tsv",
            out var questTownerInteractionsTable)
            ? AuthoritativeQuestTownerInteractionCatalog.LoadTsv(
                questTownerInteractionsTable.SourcePath,
                questTownerInteractionsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var questDialogueStages = content.TryGetTable(
            "quest_dialogue_stages.tsv",
            out var questDialogueStagesTable)
            ? AuthoritativeQuestDialogueStageCatalog.LoadTsv(
                questDialogueStagesTable.SourcePath,
                questDialogueStagesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var dialogueMessages = content.TryGetTable(
            "dialogue_messages.tsv",
            out var dialogueMessagesTable)
            ? AuthoritativeDialogueMessageCatalog.LoadTsv(
                dialogueMessagesTable.SourcePath,
                dialogueMessagesTable.ToCanonicalTsv())
            : null;
        dialogueMessages?.ValidateMessageIds(
            (questDialogueStages?.Definitions ?? [])
                .Select(stage => stage.MessageId));

        if (world is not null
            && quests is null
            && (world.WarpTargets.Any(target => target.RequiredQuestId != 0)
                || objects?.Any(@object => @object.QuestId != 0) == true)) {
            throw new InvalidDataException(
                "Quest-gated warps and objects require an active quest catalog.");
        }
        if (quests is not null) {
            AuthoritativeQuestCatalog.ValidateActivationTargets(quests);
            if (monsterTypes is not null)
                AuthoritativeQuestCatalog.ValidateMonsterCompletionTargets(
                    quests,
                    monsterTypes);
            if (world is not null)
                AuthoritativeWarpCatalog.ValidateQuestRequirements(
                    world.WarpTargets,
                    quests);
            if (objects is not null)
                AuthoritativeWorldObjectCatalog.ValidateQuestRequirements(
                    objects,
                    quests);
            var rewardQuests = quests
                .Where(quest => quest.CompletionRewardItemId != 0)
                .ToArray();
            if (rewardQuests.Length != 0) {
                if (itemCatalog is null)
                    throw new InvalidDataException(
                        "Quest completion rewards require an active item catalog.");
                AuthoritativeQuestCatalog.ValidateRewardItems(quests, itemCatalog);
            }
            if (quests.Any(quest => quest.TurnInTownerId >= 0)) {
                if (itemCatalog is null || uniqueItems is null)
                    throw new InvalidDataException(
                        "Quest towner turn-ins require active item and unique-item catalogs.");
                AuthoritativeQuestCatalog.ValidateTurnInRewards(
                    quests,
                    itemCatalog,
                    uniqueItems);
                if (world is not null)
                    AuthoritativeQuestCatalog.ValidateTurnInPositions(quests, world);
            }
            if (quests.Any(quest => quest.DialogueItemId != 0
                                   || quest.DialogueRewardUniqueId != 0
                                   || quest.DialogueRewardItemId != 0)) {
                if (itemCatalog is null || uniqueItems is null)
                    throw new InvalidDataException(
                        "Quest dialogue hand-ins require active item and unique-item catalogs.");
                AuthoritativeQuestCatalog.ValidateDialogueRewards(
                    quests,
                    itemCatalog,
                    uniqueItems);
            }
            if (questTownerInteractions is not null) {
                if (itemCatalog is null)
                    throw new InvalidDataException(
                        "Quest/towner interactions require an active item catalog.");
                questTownerInteractions.ValidateItems(itemCatalog);
                questTownerInteractions.ValidateQuests(quests);
                if (world is not null)
                    questTownerInteractions.ValidateWorld(world);
            }
            if (questDialogueStages is not null) {
                if (questDialogueStages.Definitions.Any(stage => stage.RequiredItemId != 0)) {
                    if (itemCatalog is null)
                        throw new InvalidDataException(
                            "Item-gated quest dialogue stages require an active item catalog.");
                    questDialogueStages.ValidateItems(itemCatalog);
                }
                questDialogueStages.ValidateQuests(quests);
            }
        } else if (questTownerInteractions is not null) {
            throw new InvalidDataException(
                "Quest/towner interactions require an active quest catalog.");
        } else if (questDialogueStages is not null) {
            throw new InvalidDataException(
                "Quest dialogue stages require an active quest catalog.");
        }
        if (gameMode == DiabloGameMode.SinglePlayer
            && quests is not null
            && questPools is not null) {
            AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(
                quests,
                questPools,
                randomSeed);
        }
        var itemQuestEffects = content.TryGetTable(
            "item_quest_effects.tsv",
            out var itemQuestEffectsTable)
            ? AuthoritativeItemQuestEffectCatalog.LoadTsv(
                itemQuestEffectsTable.SourcePath,
                itemQuestEffectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (itemQuestEffects is not null) {
            if (itemCatalog is null || quests is null)
                throw new InvalidDataException(
                    "Item quest effects require active item and quest catalogs.");
            itemQuestEffects.ValidateItems(itemCatalog);
            itemQuestEffects.ValidateQuests(quests);
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
                monsters,
                spellBooks,
                uniqueMonsters)
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

        var rules = new DiabloStoreRules(pricing, itemCatalog);
        var simulation = simulationFactory.Create(new DiabloSimulationOptions(stores, rules) {
            World = world,
            WorldMutations = worldMutations,
            WorldRules = worldRules,
            CombatTargets = combatTargets,
            Portals = portals,
            Objects = objects,
            Quests = quests,
            QuestTypes = questTypes,
            QuestTownerInteractions = questTownerInteractions,
            QuestDialogueStages = questDialogueStages,
            DialogueMessages = dialogueMessages,
            Spells = spells,
            SpellBooks = spellBooks,
            CombatRules = combatRules,
            Items = itemCatalog,
            ItemCombinations = itemCombinations,
            ItemQuestEffects = itemQuestEffects,
            IdentifyAbilitySpellId = identifyAbilitySpellId,
            RepairAbilitySpellId = repairAbilitySpellId,
            RechargeAbilitySpellId = rechargeAbilitySpellId,
            TrapDisarmAbilitySpellId = trapDisarmAbilitySpellId,
            MonsterFlashSpellId = monsterFlashSpellId,
            AdriaStoreId = worldRules.AdriaStoreId,
            PlayerClasses = playerClasses,
            PlayerAnimations = playerAnimations,
            MonsterMissiles = monsterMissiles,
            MonsterTypes = monsterTypes,
            UseNativePlayerActionTiming = true,
            RandomSeed = randomSeed,
            GameMode = gameMode,
            Difficulty = difficulty,
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
            monsterMissiles,
            worldRules.AdriaStoreId,
            worldRules,
            spellBooks,
            questTownerInteractions,
            questDialogueStages,
            dialogueMessages);
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

    private static uint ResolveAbilityId(StableContentIdCatalog? stableIds, string symbol)
    {
        if (stableIds is null)
            return 0;
        return stableIds.TryResolve("ability", symbol, out var abilityId)
            ? abilityId
            : stableIds.Resolve("spell", symbol);
    }

    private static uint ResolveSpellId(StableContentIdCatalog? stableIds, string symbol)
    {
        if (stableIds is null)
            return DiabloSimulationOptions.LegacyMonsterFlashSpellId;
        return stableIds.Resolve("spell", symbol);
    }
}
