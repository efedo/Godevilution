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
        uint storytellerStoreId = DiabloStoreCommandProcessor.StorytellerStoreId,
        AuthoritativeWorldRules? worldRules = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeQuestTownerInteractionCatalog? questTownerInteractions = null,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null,
        AuthoritativeDialogueMessageCatalog? dialogueMessages = null,
        AuthoritativeSetLevelObjectCatalog? setLevelObjects = null,
        AuthoritativeSetLevelMapCatalog? setLevelMaps = null,
        AuthoritativeNativeDunObjectCatalog? nativeDunObjects = null,
        AuthoritativeNativeDunFragmentCatalog? nativeDunFragments = null,
        AuthoritativeNativeDunFragmentObjectCatalog? nativeDunFragmentObjects = null,
        AuthoritativeQuestSequenceVariantCatalog? questSequenceVariants = null,
        AuthoritativeUniqueMonsterPlacementCatalog? uniqueMonsterPlacements = null,
        AuthoritativeProceduralSetPieceOriginCatalog? proceduralSetPieceOrigins = null,
        AuthoritativeNativeDunMonsterCatalog? nativeDunMonsters = null,
        AuthoritativeUniqueMonsterPackCatalog? uniqueMonsterPacks = null,
        AuthoritativeExperienceCatalog? experience = null,
        AuthoritativeNativeMissileCatalog? nativeMissiles = null,
        AuthoritativeNativeMissileSpriteCatalog? nativeMissileSprites = null,
        AuthoritativeMonsterSpawnPolicyCatalog? monsterSpawnPolicies = null,
        AuthoritativeTownerGreetingCatalog? townerGreetings = null,
        AuthoritativeNativeDunGeometryCatalog? nativeDunGeometry = null)
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
        MonsterSpawnPolicies = monsterSpawnPolicies;
        MonsterMissiles = monsterMissiles;
        AdriaStoreId = adriaStoreId;
        StorytellerStoreId = storytellerStoreId;
        WorldRules = worldRules ?? AuthoritativeWorldRules.NativeDefaults;
        SpellBooks = spellBooks;
        QuestTownerInteractions = questTownerInteractions;
        QuestDialogueStages = questDialogueStages;
        DialogueMessages = dialogueMessages;
        SetLevelObjects = setLevelObjects;
        ProceduralSetPieceOrigins = proceduralSetPieceOrigins;
        SetLevelMaps = setLevelMaps;
        NativeDunObjects = nativeDunObjects;
        NativeDunFragments = nativeDunFragments;
        NativeDunGeometry = nativeDunGeometry;
        NativeDunFragmentObjects = nativeDunFragmentObjects;
        NativeDunMonsters = nativeDunMonsters;
        QuestSequenceVariants = questSequenceVariants;
        UniqueMonsterPlacements = uniqueMonsterPlacements;
        UniqueMonsterPacks = uniqueMonsterPacks;
        Experience = experience;
        NativeMissiles = nativeMissiles;
        NativeMissileSprites = nativeMissileSprites;
        TownerGreetings = townerGreetings;
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

    /** Validated content-owned procedural monster admission roles. */
    public AuthoritativeMonsterSpawnPolicyCatalog? MonsterSpawnPolicies { get; }

    /** Validated native AI-to-missile mappings from the active content pack. */
    public AuthoritativeMonsterMissileCatalog? MonsterMissiles { get; }

    /** Validated native missile identities and presentation metadata. */
    public AuthoritativeNativeMissileCatalog? NativeMissiles { get; }

    /** Validated native missile sprite dimensions and animation timing. */
    public AuthoritativeNativeMissileSpriteCatalog? NativeMissileSprites { get; }

    public AuthoritativeTownerGreetingCatalog? TownerGreetings { get; }

    /** Service-only store identity supplied by the active world rules. */
    public uint AdriaStoreId { get; }

    /** Service-only store identity supplied by the active world rules. */
    public uint StorytellerStoreId { get; }

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

    /** Validated native fixed-object placements materialized when geometry is available. */
    public AuthoritativeSetLevelObjectCatalog? SetLevelObjects { get; }

    /** Validated deterministic origin rules for generated set-piece placements. */
    public AuthoritativeProceduralSetPieceOriginCatalog? ProceduralSetPieceOrigins { get; }

    /** Validated mappings for native DUN object-layer materialization. */
    public AuthoritativeNativeDunObjectCatalog? NativeDunObjects { get; }

    /** Validated native DUN fragments composed into ordinary runtime levels. */
    public AuthoritativeNativeDunFragmentCatalog? NativeDunFragments { get; }

    /** Shared validated native geometry definitions retained by activation. */
    public AuthoritativeNativeDunGeometryCatalog? NativeDunGeometry { get; }

    /** Validated anchor-relative authored objects for native DUN fragments. */
    public AuthoritativeNativeDunFragmentObjectCatalog? NativeDunFragmentObjects { get; }

    /** Validated raw DUN monster-layer conversion mappings. */
    public AuthoritativeNativeDunMonsterCatalog? NativeDunMonsters { get; }

    /** Validated native ordered-quest layout alternatives selected at activation. */
    public AuthoritativeQuestSequenceVariantCatalog? QuestSequenceVariants { get; }

    /** Validated content-owned placements for generated unique encounters. */
    public AuthoritativeUniqueMonsterPlacementCatalog? UniqueMonsterPlacements { get; }

    /** Validated pack placement metadata for generated unique encounters. */
    public AuthoritativeUniqueMonsterPackCatalog? UniqueMonsterPacks { get; }

    /** Validated native set-level map assets and entry points used by activation. */
    public AuthoritativeSetLevelMapCatalog? SetLevelMaps { get; }

    /** Validated cumulative character-level experience thresholds. */
    public AuthoritativeExperienceCatalog? Experience { get; }
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
    private readonly IDiabloSimulationFactory simulationFactory;

    public DiabloContentBootstrap(
        IDiabloSimulationFactory simulationFactory)
    {
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
            && !DiabloContentSchema.IsCompatible(content.Manifest.Schema)) {
            throw new InvalidDataException(
                $"Content schema '{content.Manifest.Schema}' is incompatible with "
                + $"the Diablo module schema '{Schema}'.");
        }

        var stableIdsTable = GetRequiredExternalTable(content, "content_ids.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv(
            stableIdsTable.SourcePath,
            stableIdsTable.ToCanonicalTsv());
        var pricingTable = GetRequiredExternalTable(content, "store_services.tsv");
        var pricing = StoreServicePricing.LoadTsv(
            pricingTable.SourcePath,
            pricingTable.ToCanonicalTsv(),
            stableIds);
        var generationTable = GetRequiredExternalTable(content, "item_generation.tsv");
        var generationRules = AuthoritativeItemGenerationRules.LoadTsv(
            generationTable.SourcePath,
            generationTable.ToCanonicalTsv());
        var experienceTable = GetRequiredExternalTable(content, "experience.tsv");
        var experience = AuthoritativeExperienceCatalog.LoadTsv(
            experienceTable.SourcePath,
            experienceTable.ToCanonicalTsv());
        var playerClassTable = content.GetRequiredTable("classes.tsv");
        var playerClasses = AuthoritativePlayerClassCatalog.LoadTsv(
            playerClassTable.SourcePath,
            playerClassTable.ToCanonicalTsv(),
            stableIds);
        stableIds.ValidateReferences("class", classId => playerClasses.TryGet(classId, out _));
        var playerAnimationTable = content.GetRequiredTable("class_animations.tsv");
        var playerAnimations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            playerAnimationTable.SourcePath,
            playerAnimationTable.ToCanonicalTsv(),
            playerClasses,
            stableIds);
        var spellsTable = GetRequiredExternalTable(content, "spells.tsv");
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            spellsTable.SourcePath,
            spellsTable.ToCanonicalTsv(),
            stableIds);
        pricing.ValidateSpellReferences(spells);
        spells.ValidateExternalizedNativePatterns();
        var nativeSpellsTable = GetRequiredExternalTable(content, "native_spells.tsv");
        var nativeSpells = AuthoritativeNativeSpellCatalog.LoadTsv(
            nativeSpellsTable.SourcePath,
            nativeSpellsTable.ToCanonicalTsv(),
            requireCompleteNamespace: true);
        var nativeMissileAliases = content.TryGetTable(
            "native_missile_aliases.tsv",
            out var nativeMissileAliasesTable)
            ? AuthoritativeNativeMissileCatalog.LoadCompatibilityAliasesTsv(
                nativeMissileAliasesTable.SourcePath,
                nativeMissileAliasesTable.ToCanonicalTsv())
            : null;
        var nativeMissilesTable = GetRequiredExternalTable(content, "native_missiles.tsv");
        var nativeMissiles = AuthoritativeNativeMissileCatalog.LoadTsv(
            nativeMissilesTable.SourcePath,
            nativeMissilesTable.ToCanonicalTsv(),
            nativeMissileAliases);
        var nativeMissileSpritesTable = GetRequiredExternalTable(
            content,
            "native_missile_sprites.tsv");
        var nativeMissileSprites = AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            nativeMissileSpritesTable.SourcePath,
            nativeMissileSpritesTable.ToCanonicalTsv());
        nativeMissiles.ValidateCompleteNativeNamespace();
        nativeMissileSprites.ValidateRequiredNativeNamespace();
        nativeMissiles.ValidateGraphicReferences(nativeMissileSprites);
        var nativeMiscTable = GetRequiredExternalTable(content, "native_item_misc.tsv");
        var nativeMisc = AuthoritativeNativeMiscCatalog.LoadTsv(
            nativeMiscTable.SourcePath,
            nativeMiscTable.ToCanonicalTsv());
        var nativeTopologyTable = GetRequiredExternalTable(content, "native_item_topology.tsv");
        var nativeTopology = AuthoritativeNativeItemTopologyCatalog.LoadTsv(
            nativeTopologyTable.SourcePath,
            nativeTopologyTable.ToCanonicalTsv());
        var nativeItemTagsTable = GetRequiredExternalTable(content, "native_item_tags.tsv");
        var nativeItemTags = AuthoritativeNativeItemTagCatalog.LoadTsv(
            nativeItemTagsTable.SourcePath,
            nativeItemTagsTable.ToCanonicalTsv());
        var nativeItemEffectsTable = GetRequiredExternalTable(content, "native_item_effects.tsv");
        var nativeItemEffects = AuthoritativeNativeItemEffectCatalog.LoadTsv(
            nativeItemEffectsTable.SourcePath,
            nativeItemEffectsTable.ToCanonicalTsv());
        var nativeItemHellfireEffectsTable = GetRequiredExternalTable(
            content,
            "native_item_hellfire_effects.tsv");
        var nativeItemHellfireEffects = AuthoritativeNativeItemHellfireEffectCatalog.LoadTsv(
            nativeItemHellfireEffectsTable.SourcePath,
            nativeItemHellfireEffectsTable.ToCanonicalTsv());
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
                generationRules,
                nativeItemEffectCatalog: nativeItemEffects,
                nativeItemHellfireEffectCatalog: nativeItemHellfireEffects)
            : content.TryGetTable("item_affixes.tsv", out var affixTable)
                ? AuthoritativeItemAffixCatalog.LoadTsv(
                    affixTable.SourcePath,
                    affixTable.ToCanonicalTsv(),
                    generationRules,
                    nativeItemEffectCatalog: nativeItemEffects,
                    nativeItemHellfireEffectCatalog: nativeItemHellfireEffects)
                : null;
        affixes?.ValidatePowers();
        var cursorTable = GetRequiredExternalTable(content, "item_cursor_graphics.tsv");
        var cursorCatalog = AuthoritativeItemCursorCatalog.LoadTsv(
            cursorTable.SourcePath,
            cursorTable.ToCanonicalTsv());

        var itemTable = GetRequiredExternalTable(content, "items.tsv");
        var itemCatalog = AuthoritativeItemCatalog.LoadTsv(
            itemTable.SourcePath,
            itemTable.ToCanonicalTsv(),
            affixes,
            null,
            cursorCatalog,
            stableIds,
            nativeItemEffects,
            nativeItemHellfireEffects);
        stableIds.ValidateReferences("item", itemId => itemCatalog.TryGet(itemId, out _));
        var spellBooks = content.TryGetTable("spell_books.tsv", out var spellBooksTable)
            ? AuthoritativeSpellBookCatalog.LoadTsv(
                spellBooksTable.SourcePath,
                spellBooksTable.ToCanonicalTsv(),
                stableIds,
                cursorCatalog)
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
        var worldRulesTable = GetRequiredExternalTable(content, "world_rules.tsv");
        var worldRules = AuthoritativeWorldRules.LoadTsv(
            worldRulesTable.SourcePath,
            worldRulesTable.ToCanonicalTsv(),
            stableIds);
        var combatTable = GetRequiredExternalTable(content, "combat.tsv");
        var combatRules = AuthoritativeCombatRules.LoadTsv(
            combatTable.SourcePath,
            combatTable.ToCanonicalTsv(),
            worldRules.TownLevelId,
            stableIds);
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

        var uniqueTable = GetRequiredExternalTable(content, "unique_items.tsv");
        var uniqueItems = AuthoritativeUniqueItemCatalog.LoadTsv(
            uniqueTable.SourcePath,
            uniqueTable.ToCanonicalTsv(),
            cursorCatalog,
            stableIds,
            nativeItemHellfireEffects,
            nativeItemEffects);
        uniqueItems.ValidatePowers();
        itemCatalog.AttachUniqueCatalog(uniqueItems);
        uniqueItems.ValidateBaseItems(itemCatalog);
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
        stableIds.ValidateReferences(
            "store",
            storeId => stores.ContainsStore(storeId)
                || storeId == worldRules.AdriaStoreId
                || storeId == worldRules.StorytellerStoreId);
        worldRules.ValidateStores(stores);
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
                foreach (var level in generatedWorld.Levels) {
                    // Explicit levels remain authoritative when a variant pack
                    // overlays metadata for the same identity. Procedural rows
                    // fill only levels absent from that pack's explicit world.
                    if (!world.ContainsLevel(level.LevelId))
                        world.AddLevel(level);
                }
            }
        }
        var nativeDunGeometry = AuthoritativeNativeDunGeometryCatalog.LoadOptional(content);
        var nativeDunLevels = content.TryGetTable(
            "native_dun_levels.tsv",
            out var nativeDunLevelsTable)
            ? AuthoritativeNativeDunLevelCatalog.LoadTsv(
                nativeDunLevelsTable.SourcePath,
                nativeDunLevelsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (nativeDunLevels is not null) {
            if (world is null)
                throw new InvalidDataException(
                    "Native DUN level layers require levels.tsv or procedural_levels.tsv.");
            nativeDunLevels.ValidateAssets(content, nativeDunGeometry);
            nativeDunLevels.Apply(content, world, nativeDunGeometry);
        }
        var nativeDunFragments = content.TryGetTable(
                "native_dun_fragments.tsv",
                out var nativeDunFragmentsTable)
            ? AuthoritativeNativeDunFragmentCatalog.LoadTsv(
                nativeDunFragmentsTable.SourcePath,
                nativeDunFragmentsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var nativeDunFragmentObjects = content.TryGetTable(
                "native_dun_fragment_objects.tsv",
                out var nativeDunFragmentObjectsTable)
            ? AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
                nativeDunFragmentObjectsTable.SourcePath,
                nativeDunFragmentObjectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var questSequenceVariants = content.TryGetTable(
                "quest_sequence_variants.tsv",
                out var questSequenceVariantsTable)
            ? AuthoritativeQuestSequenceVariantCatalog.LoadTsv(
                questSequenceVariantsTable.SourcePath,
                questSequenceVariantsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var levelGeneration = content.TryGetTable(
            "level_generation.tsv",
            out var levelGenerationTable)
            ? AuthoritativeLevelGenerationCatalog.LoadTsv(
                levelGenerationTable.SourcePath,
                levelGenerationTable.ToCanonicalTsv())
            : null;
        playerLoadouts.ValidateCastSources(spells);
        spells.ValidateStableReferences(
            stableIds,
            playerLoadouts.Definitions.Select(definition => definition.AbilitySpellId));
        var identifyAbilitySpellId = ResolveAbilityId(stableIds, "identify");
        var repairAbilitySpellId = ResolveAbilityId(stableIds, "repair");
        var rechargeAbilitySpellId = ResolveAbilityId(stableIds, "recharge");
        var trapDisarmAbilitySpellId = ResolveAbilityId(stableIds, "trap_disarm");
        var monsterFlashSpellId = ResolveSpellId(stableIds, "flash");
        itemCatalog.ValidateSpellReferences(spells);
        if (world is not null)
            worldRules.ValidateWorld(world);
        if (world is not null)
            combatRules.ValidateWorld(world);
        var monsterTypesTable = GetRequiredExternalTable(content, "monster_types.tsv");
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            monsterTypesTable.SourcePath,
            monsterTypesTable.ToCanonicalTsv(),
            stableIds);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        var monsterSpawnPolicies = content.TryGetTable(
                "monster_spawn_policies.tsv",
                out var monsterSpawnPoliciesTable)
            ? AuthoritativeMonsterSpawnPolicyCatalog.LoadTsv(
                monsterSpawnPoliciesTable.SourcePath,
                monsterSpawnPoliciesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (monsterSpawnPolicies is not null) {
            if (monsterTypes is null)
                throw new InvalidDataException(
                    "Monster spawn policies require monster_types.tsv.");
            if (monsterSpawnPolicies.RequiresCompleteCoverage)
                monsterSpawnPolicies.ValidateCompleteMonsterTypes(monsterTypes);
            else
                monsterSpawnPolicies.ValidateMonsterTypes(monsterTypes);
        }
        var nativeDunMonsters = content.TryGetTable(
            "native_dun_monsters.tsv",
            out var nativeDunMonstersTable)
            ? AuthoritativeNativeDunMonsterCatalog.LoadTsv(
                nativeDunMonstersTable.SourcePath,
                nativeDunMonstersTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (nativeDunMonsters is not null) {
            if (monsterTypes is null)
                throw new InvalidDataException(
                    "Native DUN monster mappings require monster_types.tsv.");
            nativeDunMonsters.ValidateMonsterTypes(monsterTypes);
        }
        if (monsterTypes is not null)
            stableIds.ValidateReferences("monster", monsterId => monsterTypes.TryGet(monsterId, out _));
        spells.ValidateMonsterReferences(monsterTypes!);
        var monsterMissilesTable = GetRequiredExternalTable(content, "monster_missiles.tsv");
        var monsterMissiles = AuthoritativeMonsterMissileCatalog.LoadTsv(
            monsterMissilesTable.SourcePath,
            monsterMissilesTable.ToCanonicalTsv(),
            stableIds);
        monsterMissiles.ValidateSpellReferences(spells);
        monsterMissiles.ValidateMonsterSelectorReferences(monsterTypes!);
        monsterMissiles.ValidateSpawnMonsterReferences(monsterTypes!);
        monsterMissiles.ValidateMissileReferences(nativeMissiles);
        var monstersTable = GetRequiredExternalTable(content, "monsters.tsv");
        var monsters = AuthoritativeMonsterCatalog.LoadTsv(
            monstersTable.SourcePath,
            monstersTable.ToCanonicalTsv(),
            itemCatalog,
            uniqueItems,
            monsterTypes,
            stableIds,
            spellBooks,
            gameMode);
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
            if (monsterTypes is not null)
                uniqueMonsters.ValidateMonsterTypes(monsterTypes);
            uniqueMonsters.ApplyTo(monsters.Targets);
        }
        var uniqueMonsterPlacements = content.TryGetTable(
            "unique_monster_placements.tsv",
            out var uniqueMonsterPlacementsTable)
            ? AuthoritativeUniqueMonsterPlacementCatalog.LoadTsv(
                uniqueMonsterPlacementsTable.SourcePath,
                uniqueMonsterPlacementsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var uniqueMonsterPacks = content.TryGetTable(
            "unique_monster_packs.tsv",
            out var uniqueMonsterPacksTable)
            ? AuthoritativeUniqueMonsterPackCatalog.LoadTsv(
                uniqueMonsterPacksTable.SourcePath,
                uniqueMonsterPacksTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (uniqueMonsterPacks is not null) {
            if (uniqueMonsterPlacements is null || monsterTypes is null)
                throw new InvalidDataException(
                    "Unique monster packs require unique placements and monster types.");
            uniqueMonsterPacks.Validate(uniqueMonsterPlacements, monsterTypes);
        }
        if (world is not null)
            stableIds.ValidateReferences("level", world.ContainsLevel);
        var objects = content.TryGetTable("objects.tsv", out var objectsTable)
            ? AuthoritativeWorldObjectCatalog.LoadTsv(
                objectsTable.SourcePath,
                objectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var objectTemplates = content.TryGetTable("objects.tsv", out objectsTable)
            ? AuthoritativeWorldObjectCatalog.LoadTemplates(
                objectsTable.SourcePath,
                objectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var objectItemGates = content.TryGetTable(
                "object_item_gates.tsv",
                out var objectItemGatesTable)
            ? AuthoritativeObjectItemGateCatalog.LoadTsv(
                objectItemGatesTable.SourcePath,
                objectItemGatesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var objectPresentations = content.TryGetTable(
            "object_presentations.tsv",
            out var objectPresentationsTable)
            ? AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
                objectPresentationsTable.SourcePath,
                objectPresentationsTable.ToCanonicalTsv())
            : null;
        objectPresentations?.Apply((objects ?? []).Concat(objectTemplates ?? []));
        nativeDunFragmentObjects?.ValidateTemplates(objectTemplates ?? []);
        var objectDefinitions = (objects ?? [])
            .Concat(objectTemplates ?? [])
            .ToArray();
        var worldMutations = content.TryGetTable(
            "world_mutations.tsv",
            out var worldMutationsTable)
            ? AuthoritativeWorldMutationCatalog.LoadTsv(
                worldMutationsTable.SourcePath,
                worldMutationsTable.ToCanonicalTsv(),
                stableIds,
                content)
            : null;
        if (objectDefinitions.Length > 0)
            stableIds.ValidateReferences(
                "object",
                objectId => objectDefinitions.Any(@object => @object.ObjectId == objectId));
        if (world is not null && objects is not null)
            AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
        if (nativeDunFragmentObjects is not null) {
            if (world is null
                || nativeDunFragments is null
                || objects is null && objectTemplates is null)
                throw new InvalidDataException(
                    "Native DUN fragment object placements require a world, fragments, and authored objects.");
            var generatedObjects = nativeDunFragmentObjects
                .Filter(placement => placement.AnchorEntityId != 0)
                .Materialize(
                    world,
                    objects ?? [],
                    objectTemplates ?? [],
                    nativeDunFragments);
            if (generatedObjects.Count > 0)
                objects = (objects ?? []).Concat(generatedObjects).ToArray();
            if (objects is not null)
                AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
        }
        if (objectDefinitions.Length > 0)
            AuthoritativeWorldObjectCatalog.ValidateItemRequirements(objectDefinitions, itemCatalog);
        if (objectItemGates is not null) {
            objectItemGates.ValidateItems(itemCatalog);
            objectItemGates.ValidateObjects((objects ?? []).Concat(objectTemplates ?? []));
        }
        if (objectDefinitions.Length > 0)
            AuthoritativeWorldObjectCatalog.ValidateRewardRequirements(objectDefinitions, itemCatalog);
        if (objectDefinitions.Length > 0 && spells is not null)
            AuthoritativeWorldObjectCatalog.ValidateSpellRequirements(objectDefinitions, spells);
        if (objectDefinitions.Length > 0 && monsterTypes is not null)
            AuthoritativeWorldObjectCatalog.ValidateMonsterRequirements(objectDefinitions, monsterTypes);
        if (objectDefinitions.Any(@object =>
                @object.QuestSequenceFollowUpKind
                    == AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster) == true
            && monsterTypes is null) {
            throw new InvalidDataException(
                "Native-monster follow-ups require an active monster type catalog.");
        }
        if (objectDefinitions.Any(@object => @object.EffectKind is
                AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || @object.QuestSequenceFollowUpKind
                    == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile) == true
            && spells is null) {
            throw new InvalidDataException(
                "Spell-effect objects require an active spell catalog.");
        }
        if (objectDefinitions.Any(@object => @object.EffectKind
                == AuthoritativeObjectEffectKind.WorldMutation
                || @object.QuestSequenceFollowUpKind
                    == AuthoritativeQuestSequenceFollowUpKind.WorldMutation
                || @object.QuestSequenceFollowUpWorldMutationId != 0) == true
            && worldMutations is null) {
            throw new InvalidDataException(
                "World-mutation objects require a world_mutations.tsv content table.");
        }
        if (objectDefinitions.Length > 0 && worldMutations is not null)
            AuthoritativeWorldObjectCatalog.ValidateMutationRequirements(objectDefinitions, worldMutations);
        var setLevelNames = content.TryGetTable(
            "set_level_names.tsv",
            out var setLevelNamesTable)
            ? AuthoritativeSetLevelNameCatalog.LoadTsv(
                setLevelNamesTable.SourcePath,
                setLevelNamesTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (setLevelNames is not null)
            stableIds.ValidateReferences(
                "set_level",
                setLevelId => setLevelId <= int.MaxValue
                    && setLevelNames.TryGet((int)setLevelId, out _));
        var proceduralSetPieceOrigins = content.TryGetTable(
                "procedural_set_piece_origins.tsv",
                out var proceduralSetPieceOriginsTable)
            ? AuthoritativeProceduralSetPieceOriginCatalog.LoadTsv(
                proceduralSetPieceOriginsTable.SourcePath,
                proceduralSetPieceOriginsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (proceduralSetPieceOrigins is not null) {
            if (setLevelNames is not null)
                foreach (var definition in proceduralSetPieceOrigins.Definitions)
                    if (!setLevelNames.TryGet(definition.SetLevelId, out _))
                        throw new InvalidDataException(
                            $"Procedural set-piece origin {definition.SetLevelId} references an unknown set level.");
        }
        var proceduralQuestPieceOrigins = content.TryGetTable(
                "procedural_quest_piece_origins.tsv",
                out var proceduralQuestPieceOriginsTable)
            ? AuthoritativeProceduralQuestPieceOriginCatalog.LoadTsv(
                proceduralQuestPieceOriginsTable.SourcePath,
                proceduralQuestPieceOriginsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (proceduralQuestPieceOrigins is not null && world is null)
            throw new InvalidDataException(
                "Procedural quest-piece origins require levels.tsv or procedural_levels.tsv.");
        if (proceduralQuestPieceOrigins is not null)
            stableIds.ValidateReferences(
                "procedural_origin",
                originId => proceduralQuestPieceOrigins.Definitions
                    .Any(definition => definition.OriginId == originId));
        var setLevelObjects = content.TryGetTable(
            "set_level_objects.tsv",
            out var setLevelObjectsTable)
            ? AuthoritativeSetLevelObjectCatalog.LoadTsv(
                setLevelObjectsTable.SourcePath,
                setLevelObjectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        var nativeDunObjects = content.TryGetTable(
            "native_dun_objects.tsv",
            out var nativeDunObjectsTable)
            ? AuthoritativeNativeDunObjectCatalog.LoadTsv(
                nativeDunObjectsTable.SourcePath,
                nativeDunObjectsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (setLevelObjects is not null) {
            if (setLevelNames is null)
                throw new InvalidDataException(
                    "Set-level object placements require set_level_names.tsv.");
            setLevelObjects.ValidateSetLevels(setLevelNames);
        }
        if (nativeDunObjects is not null) {
            if (nativeDunObjects.Definitions.Any(definition =>
                    definition.TargetKind == AuthoritativeNativeDunObjectTargetKind.SetLevel)
                && setLevelNames is null)
                throw new InvalidDataException(
                    "Native DUN object mappings require set_level_names.tsv.");
            if (setLevelNames is not null)
                nativeDunObjects.ValidateSetLevels(setLevelNames);
            if (nativeDunObjects.Definitions.Any(definition =>
                    definition.TargetKind == AuthoritativeNativeDunObjectTargetKind.Level)) {
                if (world is null)
                    throw new InvalidDataException(
                        "Ordinary-level native DUN object mappings require an active world.");
                nativeDunObjects.ValidateWorld(world);
            }
            nativeDunObjects.ValidateTemplates(objectDefinitions);
        }
        setLevelObjects?.ValidateTemplates(objectDefinitions);
        // Generation metadata covers ordinary/procedural levels; native
        // set-level variants are validated by their own map catalog below.
        if (world is not null && levelGeneration is not null)
            levelGeneration.ValidateWorld(world);
        var setLevelMaps = content.TryGetTable(
            "set_level_maps.tsv",
            out var setLevelMapsTable)
            ? AuthoritativeSetLevelMapCatalog.LoadTsv(
                setLevelMapsTable.SourcePath,
                setLevelMapsTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (setLevelMaps is not null) {
            if (setLevelNames is null)
                throw new InvalidDataException(
                    "Set-level map definitions require set_level_names.tsv.");
            setLevelMaps.ValidateSetLevels(setLevelNames);
            setLevelMaps.ValidateAssets(content, nativeDunGeometry);
            if (world is not null) {
                foreach (var level in setLevelMaps.LoadAvailableGeometry(content, nativeDunGeometry))
                    world.AddLevel(level);
            }
        }
        // Set-level geometry is published before warps so authored destinations
        // can target a materialized pre-map or active-map variant.
        if (world is not null && content.TryGetTable("warps.tsv", out var warpsTable)) {
            foreach (var warp in AuthoritativeWarpCatalog.LoadTsv(
                         warpsTable.SourcePath,
                         warpsTable.ToCanonicalTsv(),
                         world,
                         stableIds,
                         setLevelMaps)) {
                world.AddWarpTarget(warp);
            }
        }
        if (world is not null)
            stableIds.ValidateReferences(
                "warp",
                warpId => world.WarpTargets.Any(warp => warp.WarpId == warpId));
        var portals = content.TryGetTable("portals.tsv", out var portalsTable)
            ? AuthoritativePortalCatalog.LoadTsv(
                portalsTable.SourcePath,
                portalsTable.ToCanonicalTsv(),
                stableIds,
                setLevelMaps)
            : null;
        if (portals is not null)
            stableIds.ValidateReferences(
                "portal",
                portalId => portals.Any(portal => portal.PortalId == portalId));
        if (world is not null && portals is not null)
            AuthoritativePortalCatalog.ValidateWorld(portals, world);
        if (world is not null && (setLevelObjects is not null || nativeDunObjects is not null)) {
            var generatedObjects = new List<AuthoritativeWorldObject>();
            if (setLevelObjects is not null) {
                generatedObjects.AddRange(
                    setLevelObjects.LoadAvailableObjects(world, objects, objectDefinitions));
            }
            if (nativeDunObjects is not null) {
                generatedObjects.AddRange(
                    nativeDunObjects.LoadAvailableObjects(
                        world,
                        (objects ?? []).Concat(generatedObjects),
                        objectDefinitions,
                        allowMissingLayers: nativeDunFragments is not null));
            }
            if (generatedObjects.Count > 0) {
                objects = (objects ?? []).Concat(generatedObjects).ToArray();
                AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
            }
        }
        if (world is not null && worldMutations is not null)
            worldMutations.ValidateWorld(world, objects);
        if (worldMutations is not null && nativeDunMonsters is not null)
            worldMutations.ValidateMonsterMappings(nativeDunMonsters);
        var questTypes = content.TryGetTable("quest_types.tsv", out var questTypesTable)
            ? AuthoritativeQuestTypeCatalog.LoadTsv(
                questTypesTable.SourcePath,
                questTypesTable.ToCanonicalTsv(),
                setLevelNames,
                stableIds)
            : null;
        if (questTypes is not null)
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
        if (world is not null && questTypes is not null && quests is not null)
            questTypes.ValidateWorldItemAnchors(quests, world);

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
        var townerGreetings = content.TryGetTable(
            "towner_greetings.tsv",
            out var townerGreetingsTable)
            ? AuthoritativeTownerGreetingCatalog.LoadTsv(
                townerGreetingsTable.SourcePath,
                townerGreetingsTable.ToCanonicalTsv(),
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
                .SelectMany(stage => new[] { stage.MessageId, stage.NextMessageId }));
        dialogueMessages?.ValidateMessageIds(
            objectDefinitions.Select(@object => @object.DialogueMessageId));
        dialogueMessages?.ValidateMessageIds(
            (itemCatalog?.Definitions ?? [])
                .Select(item => item.ItemActionMessageId));

        if (world is not null
            && quests is null
            && (world.WarpTargets.Any(target => target.RequiredQuestId != 0)
                || objectDefinitions.Any(@object => @object.QuestId != 0))) {
            throw new InvalidDataException(
                "Quest-gated warps and objects require an active quest catalog.");
        }
        if (world is not null && townerGreetings is not null)
            townerGreetings.ValidateWorld(world);
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
            if (objectDefinitions.Length > 0)
                AuthoritativeWorldObjectCatalog.ValidateQuestRequirements(
                    objectDefinitions,
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
                if (questDialogueStages.Definitions.Any(stage =>
                        stage.RequiredItemId != 0
                        || stage.RewardKind != AuthoritativeQuestDialogueRewardKind.None)) {
                    if (itemCatalog is null)
                        throw new InvalidDataException(
                            "Item-gated or item-reward quest dialogue stages require an active item catalog.");
                    questDialogueStages.ValidateItems(itemCatalog);
                }
                if (questDialogueStages.Definitions.Any(stage => stage.RewardUniqueId != 0)) {
                    if (uniqueItems is null)
                        throw new InvalidDataException(
                            "Unique-item quest dialogue stages require an active unique-item catalog.");
                    questDialogueStages.ValidateUniqueItems(uniqueItems);
                }
                if (questDialogueStages.Definitions.Any(stage =>
                        stage.FollowUpKind == AuthoritativeQuestDialogueFollowUpKind.WorldMutation)) {
                    if (worldMutations is null)
                        throw new InvalidDataException(
                            "Quest dialogue follow-ups require a world_mutations.tsv content table.");
                    questDialogueStages.ValidateWorldMutations(worldMutations);
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
        if (questSequenceVariants is not null && !questSequenceVariants.IsEmpty) {
            if (objects is null || quests is null)
                throw new InvalidDataException(
                    "Quest-sequence variants require authored objects and quests.");
            questSequenceVariants.Apply(objects, quests, randomSeed);
        }
        if (gameMode == DiabloGameMode.SinglePlayer
            && quests is not null
            && questPools is not null) {
            AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(
                quests,
                questPools,
                randomSeed);
        }
        if (nativeDunFragments is not null
            && nativeDunFragments.Definitions.Any(definition => definition.RequiredQuestId is not null)) {
            if (quests is null)
                throw new InvalidDataException(
                    "Quest-gated native DUN fragments require an active quest catalog.");
            nativeDunFragments = nativeDunFragments.FilterByQuestAvailability(quests);
            var activeFragmentIds = nativeDunFragments.Definitions
                .Select(definition => definition.FragmentId)
                .ToHashSet();
            nativeDunFragmentObjects = nativeDunFragmentObjects?.Filter(
                placement => activeFragmentIds.Contains(placement.FragmentId));
        }
        if (uniqueMonsterPlacements is not null) {
            foreach (var placement in uniqueMonsterPlacements.Definitions
                         .Where(definition => definition.RequiredQuestId != 0)) {
                if (quests is null
                    || !quests.Any(quest => quest.QuestId == placement.RequiredQuestId)) {
                    throw new InvalidDataException(
                        $"Unique placement {placement.PlacementId} references unknown quest "
                        + $"{placement.RequiredQuestId}.");
                }
            }
            uniqueMonsterPlacements = uniqueMonsterPlacements.Filter(placement =>
                placement.RequiredQuestId == 0
                || quests!.Single(quest => quest.QuestId == placement.RequiredQuestId).Unavailable == false);
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
            ?? stableIds.Resolve("level", "town");
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
                uniqueMonsters,
                monsterSpawnPolicies)
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
        if (uniqueMonsterPlacements is not null
            && !uniqueMonsterPlacements.Definitions.Any()) {
            uniqueMonsterPlacements = null;
        }
        if (uniqueMonsterPlacements is not null) {
            if (world is null
                || monsterTypes is null
                || uniqueMonsters is null
                || monsterSpawnPlanner is null) {
                throw new InvalidDataException(
                    "Unique monster placements require world, monster types, unique identities, and a spawn planner.");
            }
            var uniquePlacementReservedPositions = uniqueMonsterPlacements.Definitions
                .SelectMany(definition => GetReservedPositions(
                        definition.LevelId,
                        world,
                        portals,
                        objects)
                    .Select(position => (
                        definition.LevelId,
                        position.X,
                        position.Y)))
                .ToArray();
            uniqueMonsterPlacements.Validate(
                world,
                monsterTypes,
                uniqueMonsters,
                combatTargets,
                uniquePlacementReservedPositions,
                reservedEntityIds: objects?.Select(@object => @object.EntityId));
            var authoredTargets = uniqueMonsterPlacements.Materialize(
                monsterSpawnPlanner,
                uniqueMonsters,
                gameMode == DiabloGameMode.Multiplayer,
                uniqueMonsterPacks,
                combatTargets,
                uniquePlacementReservedPositions);
            combatTargets = (combatTargets ?? [])
                .Concat(authoredTargets)
                .ToArray();
        }
        if (nativeDunFragmentObjects is not null
            && nativeDunFragmentObjects.Placements.Any(placement => placement.AnchorEntityId == 0)) {
            if (world is null
                || nativeDunFragments is null
                || objects is null && objectTemplates is null)
                throw new InvalidDataException(
                    "Native DUN fragment object placements require a world, fragments, and authored objects.");
            var generatedObjects = nativeDunFragmentObjects
                .Filter(placement => placement.AnchorEntityId == 0
                    && placement.AnchorSetLevelId is null
                    && placement.AnchorProceduralQuestPieceOriginId is null
                    && (nativeDunFragments.Definitions
                        .FirstOrDefault(fragment => fragment.FragmentId == placement.FragmentId)
                        ?.AnchorSetLevelId is null)
                    && (nativeDunFragments.Definitions
                        .FirstOrDefault(fragment => fragment.FragmentId == placement.FragmentId)
                        ?.AnchorProceduralQuestPieceOriginId is null))
                .Materialize(
                    world,
                    objects ?? [],
                    objectTemplates ?? [],
                    nativeDunFragments,
                    combatTargets);
            if (generatedObjects.Count > 0)
                objects = (objects ?? []).Concat(generatedObjects).ToArray();
            if (objects is not null)
                AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
        }
        if (nativeDunFragments is not null) {
            if (world is null || objects is null)
                throw new InvalidDataException(
                    "Native DUN fragments require a world and authored anchors.");
            nativeDunFragments.ValidateWorld(world);
            if (setLevelNames is not null)
                nativeDunFragments.ValidateSetLevels(setLevelNames);
            if (setLevelNames is not null)
                nativeDunFragmentObjects?.ValidateSetLevels(setLevelNames);
            if (nativeDunFragments.Definitions.Any(
                    definition => definition.AnchorProceduralQuestPieceOriginId is not null)) {
                if (proceduralQuestPieceOrigins is null)
                    throw new InvalidDataException(
                        "Native DUN fragments with procedural quest-piece anchors require procedural_quest_piece_origins.tsv.");
                nativeDunFragments.ValidateProceduralQuestPieceOrigins(
                    proceduralQuestPieceOrigins);
            }
            if (nativeDunFragmentObjects?.Placements.Any(placement =>
                    placement.AnchorProceduralQuestPieceOriginId is not null
                    || nativeDunFragments.Definitions
                        .FirstOrDefault(fragment => fragment.FragmentId == placement.FragmentId)
                        ?.AnchorProceduralQuestPieceOriginId is not null) == true) {
                if (proceduralQuestPieceOrigins is null)
                    throw new InvalidDataException(
                        "Native DUN fragment objects with procedural quest-piece anchors require procedural_quest_piece_origins.tsv.");
                nativeDunFragmentObjects.ValidateProceduralQuestPieceOrigins(
                    proceduralQuestPieceOrigins);
            }
            nativeDunFragments.ValidateAssets(content, nativeDunGeometry);
            nativeDunFragments
                .Filter(definition => definition.AnchorEntityId != 0
                    || definition.AnchorPositionX is not null)
                .Apply(content, world, objects, combatTargets, externalGeometry: nativeDunGeometry);
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
            TownerGreetings = townerGreetings,
            DialogueMessages = dialogueMessages,
            SetLevelObjects = setLevelObjects,
            ProceduralSetPieceOrigins = proceduralSetPieceOrigins,
            ProceduralQuestPieceOrigins = proceduralQuestPieceOrigins,
            SetLevelMaps = setLevelMaps,
            NativeDunObjects = nativeDunObjects,
            NativeDunFragments = nativeDunFragments,
            NativeDunGeometry = nativeDunGeometry,
            NativeDunFragmentObjects = nativeDunFragmentObjects,
            NativeDunMonsters = nativeDunMonsters,
            Content = content,
            NativeObjectTemplates = objectTemplates,
            Spells = spells,
            Experience = experience,
            SpellBooks = spellBooks,
            CombatRules = combatRules,
            Items = itemCatalog,
            ObjectItemGates = objectItemGates,
            ItemCombinations = itemCombinations,
            ItemQuestEffects = itemQuestEffects,
            IdentifyAbilitySpellId = identifyAbilitySpellId,
            RepairAbilitySpellId = repairAbilitySpellId,
            RechargeAbilitySpellId = rechargeAbilitySpellId,
            TrapDisarmAbilitySpellId = trapDisarmAbilitySpellId,
            MonsterFlashSpellId = monsterFlashSpellId,
            AdriaStoreId = worldRules.AdriaStoreId,
            StorytellerStoreId = worldRules.StorytellerStoreId,
            PlayerClasses = playerClasses,
            PlayerAnimations = playerAnimations,
            MonsterMissiles = monsterMissiles,
            NativeMissiles = nativeMissiles,
            NativeMissileSprites = nativeMissileSprites,
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
            worldRules.StorytellerStoreId,
            worldRules,
            spellBooks,
            questTownerInteractions,
            questDialogueStages,
            dialogueMessages,
            setLevelObjects,
            setLevelMaps,
            nativeDunObjects,
            nativeDunFragments,
            nativeDunFragmentObjects,
            questSequenceVariants,
            uniqueMonsterPlacements,
            proceduralSetPieceOrigins,
            nativeDunMonsters,
            uniqueMonsterPacks,
            experience,
            nativeMissiles,
            nativeMissileSprites,
            monsterSpawnPolicies,
            townerGreetings,
            nativeDunGeometry: nativeDunGeometry);
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

    private static TsvTable GetRequiredExternalTable(
        ComposedContent content,
        string sourcePath)
    {
        if (content.TryGetTable(sourcePath, out var table))
            return table;
        throw new InvalidDataException(
            $"Production Diablo activation requires external '{sourcePath}' content.");
    }

    private static uint ResolveAbilityId(StableContentIdCatalog stableIds, string symbol)
    {
        return stableIds.TryResolve("ability", symbol, out var abilityId)
            ? abilityId
            : stableIds.Resolve("spell", symbol);
    }

    private static uint ResolveSpellId(StableContentIdCatalog stableIds, string symbol)
    {
        return stableIds.Resolve("spell", symbol);
    }
}
