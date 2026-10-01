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
        AuthoritativeQuestRouteMessageCatalog? questRouteMessages = null,
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
        AuthoritativeNativeDunGeometryCatalog? nativeDunGeometry = null,
        AuthoritativeNativeSpellCatalog? nativeSpells = null,
        AuthoritativeNativeMiscCatalog? nativeItemMisc = null,
        AuthoritativeNativeItemTopologyCatalog? nativeItemTopology = null,
        AuthoritativeNativeItemTagCatalog? nativeItemTags = null)
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
        QuestRouteMessages = questRouteMessages;
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
        NativeSpells = nativeSpells;
        NativeItemMisc = nativeItemMisc;
        NativeItemTopology = nativeItemTopology;
        NativeItemTags = nativeItemTags;
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

    /** Validated native spell namespace retained for legacy item compatibility. */
    public AuthoritativeNativeSpellCatalog? NativeSpells { get; }

    /** Validated native miscellaneous-item namespace retained for legacy compatibility. */
    public AuthoritativeNativeMiscCatalog? NativeItemMisc { get; }

    /** Validated native item topology namespace retained for legacy compatibility. */
    public AuthoritativeNativeItemTopologyCatalog? NativeItemTopology { get; }

    /** Validated native item-generation tags retained for legacy compatibility. */
    public AuthoritativeNativeItemTagCatalog? NativeItemTags { get; }

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

    /** Validated route-entry dialogue supplied by the active content pack. */
    public AuthoritativeQuestRouteMessageCatalog? QuestRouteMessages { get; }

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

/** Additive, explicitly selected native-generation capability; legacy activation is unchanged. */
public interface IDiabloNativeGeneratedContentBootstrap
{
    DiabloContentActivation ActivateGenerated(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal);
}

/** Separate opt-in ordinary Cathedral depths 1-4 contract; existing L1 profiles stay unchanged. */
public interface IDiabloCathedralDepthContentBootstrap
{
    DiabloContentActivation ActivateDepths(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal);
}

/** First-party content activation kept entirely inside the Diablo module. */
internal sealed class DiabloContentBootstrap : IDiabloContentBootstrap, IDiabloNativeGeneratedContentBootstrap, IDiabloCathedralDepthContentBootstrap
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
        => ActivateCore(content, randomSeed, gameMode, monsterSpawns, difficulty, nativeGenerated: false);

    public DiabloContentActivation ActivateGenerated(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal)
        => ActivateCore(content, randomSeed, gameMode, monsterSpawns, difficulty, nativeGenerated: true);

    public DiabloContentActivation ActivateDepths(
        ComposedContent content,
        uint randomSeed = 0,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloMonsterSpawnConfiguration? monsterSpawns = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal)
        => ActivateCore(content, randomSeed, gameMode, monsterSpawns, difficulty, nativeGenerated: false, cathedralDepths: true);

    private DiabloContentActivation ActivateCore(
        ComposedContent content,
        uint randomSeed,
        DiabloGameMode gameMode,
        DiabloMonsterSpawnConfiguration? monsterSpawns,
        DiabloDifficulty difficulty,
        bool nativeGenerated,
        bool cathedralDepths = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hasGeneratedLevels = content.TryGetTable(AuthoritativeCathedralLevel1RuntimeCatalog.TableName, out var generatedLevelsTable);
        var hasDepthLevels = content.TryGetTable(AuthoritativeCathedralDepthRuntimeCatalog.TableName, out var depthLevelsTable);
        var hasGeneratedRoutes = content.TryGetTable(AuthoritativeNativeGeneratedRouteBindings.TableName, out var generatedRoutesTable);
        if (cathedralDepths && (!hasDepthLevels || !hasGeneratedRoutes || hasGeneratedLevels))
            throw new InvalidDataException("Cathedral depths require their own level catalog and explicit route bindings, not an L1 catalog.");
        if (!cathedralDepths && (nativeGenerated ? !hasGeneratedLevels || !hasGeneratedRoutes || hasDepthLevels : hasGeneratedLevels || hasGeneratedRoutes || hasDepthLevels)) {
            throw new InvalidDataException(nativeGenerated
                ? "Native generation requires both level definitions and explicit route bindings."
                : "Native generation content requires the explicitly selected native-generated host adapter.");
        }
        monsterSpawns?.Validate();
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (content.Manifest.Schema is not null
            && !DiabloContentSchema.IsCompatible(content.Manifest.Schema)) {
            throw new InvalidDataException(
                $"Content schema '{content.Manifest.Schema}' is incompatible with "
                + $"the Diablo module schema '{Schema}'.");
        }
        var legacyCompatibility = LoadLegacyCompatibilityCatalogs(content);

        var stableIdsTable = GetRequiredExternalTable(content, "content_ids.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            StableContentIdCatalog.ValidateExternalizedProduction(stableIdsTable);
        var stableIds = StableContentIdCatalog.LoadTsv(
            stableIdsTable.SourcePath,
            stableIdsTable.ToCanonicalTsv());
        var pricingTable = GetRequiredExternalTable(content, "store_services.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            StoreServicePricing.ValidateExternalizedProduction(pricingTable);
        var pricing = StoreServicePricing.LoadTsv(
            pricingTable.SourcePath,
            pricingTable.ToCanonicalTsv(),
            stableIds);
        var generationTable = GetRequiredExternalTable(content, "item_generation.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemGenerationRules.ValidateExternalizedProduction(generationTable);
        var generationRules = AuthoritativeItemGenerationRules.LoadTsv(
            generationTable.SourcePath,
            generationTable.ToCanonicalTsv());
        var experienceTable = GetRequiredExternalTable(content, "experience.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeExperienceCatalog.ValidateExternalizedProduction(experienceTable);
        var experience = AuthoritativeExperienceCatalog.LoadTsv(
            experienceTable.SourcePath,
            experienceTable.ToCanonicalTsv());
        var playerClassTable = content.GetRequiredTable("classes.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativePlayerClassCatalog.ValidateExternalizedProduction(playerClassTable);
        var playerClasses = AuthoritativePlayerClassCatalog.LoadTsv(
            playerClassTable.SourcePath,
            playerClassTable.ToCanonicalTsv(),
            stableIds);
        stableIds.ValidateReferences("class", classId => playerClasses.TryGet(classId, out _));
        var playerAnimationTable = content.GetRequiredTable("class_animations.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativePlayerAnimationCatalog.ValidateExternalizedProduction(
                playerAnimationTable);
        var playerAnimations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            playerAnimationTable.SourcePath,
            playerAnimationTable.ToCanonicalTsv(),
            playerClasses,
            stableIds);
        var spellsTable = GetRequiredExternalTable(content, "spells.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeSpellCatalog.ValidateExternalizedProduction(spellsTable);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            spellsTable.SourcePath,
            spellsTable.ToCanonicalTsv(),
            stableIds);
        pricing.ValidateSpellReferences(spells);
        spells.ValidateExternalizedNativePatterns();
        var nativeMissileAliasesTable = GetRequiredExternalTable(
            content,
            "native_missile_aliases.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeMissileCatalog.ValidateCompatibilityAliasesProduction(
                nativeMissileAliasesTable);
        var nativeMissileAliases = AuthoritativeNativeMissileCatalog.LoadCompatibilityAliasesTsv(
            nativeMissileAliasesTable.SourcePath,
            nativeMissileAliasesTable.ToCanonicalTsv());
        var nativeMissilesTable = GetRequiredExternalTable(content, "native_missiles.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeMissileCatalog.ValidateExternalizedProduction(nativeMissilesTable);
        var nativeMissiles = AuthoritativeNativeMissileCatalog.LoadTsv(
            nativeMissilesTable.SourcePath,
            nativeMissilesTable.ToCanonicalTsv(),
            nativeMissileAliases);
        var nativeMissileSpritesTable = GetRequiredExternalTable(
            content,
            "native_missile_sprites.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeMissileSpriteCatalog.ValidateExternalizedProduction(
                nativeMissileSpritesTable);
        var nativeMissileSprites = AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            nativeMissileSpritesTable.SourcePath,
            nativeMissileSpritesTable.ToCanonicalTsv());
        nativeMissiles.ValidateCompleteNativeNamespace();
        nativeMissiles.ValidateCompatibilityAliases();
        nativeMissileSprites.ValidateRequiredNativeNamespace();
        nativeMissiles.ValidateGraphicReferences(nativeMissileSprites);
        var nativeItemEffectsTable = GetRequiredExternalTable(content, "native_item_effects.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeItemEffectCatalog.ValidateExternalizedProduction(
                nativeItemEffectsTable);
        var nativeItemEffects = AuthoritativeNativeItemEffectCatalog.LoadTsv(
            nativeItemEffectsTable.SourcePath,
            nativeItemEffectsTable.ToCanonicalTsv());
        var nativeItemHellfireEffectsTable = GetRequiredExternalTable(
            content,
            "native_item_hellfire_effects.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeItemHellfireEffectCatalog.ValidateExternalizedProduction(
                nativeItemHellfireEffectsTable);
        var nativeItemHellfireEffects = AuthoritativeNativeItemHellfireEffectCatalog.LoadTsv(
            nativeItemHellfireEffectsTable.SourcePath,
            nativeItemHellfireEffectsTable.ToCanonicalTsv());
        var affixTable = GetRequiredExternalTable(content, "item_affixes.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemAffixCatalog.ValidateExternalizedProduction(affixTable);
        var affixes = AuthoritativeItemAffixCatalog.LoadTsv(
            affixTable.SourcePath,
            affixTable.ToCanonicalTsv(),
            generationRules,
            nativeItemEffectCatalog: nativeItemEffects,
            nativeItemHellfireEffectCatalog: nativeItemHellfireEffects);
        affixes.ValidatePowers();
        var cursorTable = GetRequiredExternalTable(content, "item_cursor_graphics.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemCursorCatalog.ValidateExternalizedProduction(cursorTable);
        var cursorCatalog = AuthoritativeItemCursorCatalog.LoadTsv(
            cursorTable.SourcePath,
            cursorTable.ToCanonicalTsv());

        var itemTable = GetRequiredExternalTable(content, "items.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemCatalog.ValidateExternalizedProduction(itemTable);
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
        var spellBooksTable = GetRequiredExternalTable(content, "spell_books.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeSpellBookCatalog.ValidateExternalizedProduction(spellBooksTable);
        var spellBooks = AuthoritativeSpellBookCatalog.LoadTsv(
            spellBooksTable.SourcePath,
            spellBooksTable.ToCanonicalTsv(),
            stableIds,
            cursorCatalog);
        spellBooks.Validate(spells, itemCatalog);
        var itemCombinations = content.TryGetTable(
            "item_combinations.tsv",
            out var itemCombinationsTable)
            ? LoadItemCombinations(content, itemCombinationsTable, stableIds)
            : null;
        itemCombinations?.ValidateItems(itemCatalog);
        var worldRulesTable = GetRequiredExternalTable(content, "world_rules.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWorldRules.ValidateExternalizedProduction(worldRulesTable);
        var worldRules = AuthoritativeWorldRules.LoadTsv(
            worldRulesTable.SourcePath,
            worldRulesTable.ToCanonicalTsv(),
            stableIds);
        var combatTable = GetRequiredExternalTable(content, "combat.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeCombatRules.ValidateExternalizedProduction(combatTable);
        var combatRules = AuthoritativeCombatRules.LoadTsv(
            combatTable.SourcePath,
            combatTable.ToCanonicalTsv(),
            worldRules.TownLevelId,
            stableIds);
        combatRules.ValidateItems(itemCatalog);
        var playerLoadoutTable = content.GetRequiredTable("class_loadouts.tsv");
        var playerLoadoutItemTable = content.GetRequiredTable(
            "class_loadout_items.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativePlayerLoadoutCatalog.ValidateExternalizedProduction(
                playerLoadoutTable,
                playerLoadoutItemTable);
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
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeUniqueItemCatalog.ValidateExternalizedProduction(uniqueTable);
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
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            StoreCatalog.ValidateExternalizedProduction(storeTable);
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
            ? LoadWorldLevels(content, levelsTable, stableIds)
            : null;
        if (content.TryGetTable(
                "procedural_levels.tsv",
                out var proceduralLevelsTable)) {
            if (content.Manifest.Schema == DiabloContentSchema.Current)
                AuthoritativeProceduralLevelCatalog.ValidateExternalizedProduction(
                    proceduralLevelsTable);
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
        if (content.Manifest.Schema == DiabloContentSchema.Current && world is null)
            throw new InvalidDataException(
                "Current-schema Diablo activation requires levels.tsv or procedural_levels.tsv content.");
        var nativeDunGeometry = LoadNativeDunGeometry(content);
        var nativeDunLevels = content.TryGetTable(
            "native_dun_levels.tsv",
            out var nativeDunLevelsTable)
            ? LoadNativeDunLevels(content, nativeDunLevelsTable, stableIds)
            : null;
        if (nativeDunLevels is not null) {
            if (world is null)
                throw new InvalidDataException(
                    "Native DUN level layers require levels.tsv or procedural_levels.tsv.");
            nativeDunLevels.ValidateAssets(content, nativeDunGeometry);
            nativeDunLevels.Apply(content, world, nativeDunGeometry);
        }
        AuthoritativeCathedralLevel1GenerationPlan? nativeGenerationPlan = null;
        AuthoritativeCathedralDepthGenerationPlan? depthGenerationPlan = null;
        var generatedGeometry = new List<(AuthoritativeLevel Level, string FineFingerprint)>();
        if (nativeGenerated) {
            if (world is null || nativeDunGeometry is null)
                throw new InvalidDataException("Native Cathedral generation requires a world and validated external geometry catalogs.");
            var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(generatedLevelsTable!.ToCanonicalTsv(), stableIds);
            if (catalog.Definitions.Any(definition => definition.LevelId == worldRules.TownLevelId
                || world.Levels.Any(level => level.LevelId == definition.LevelId
                    && (level.NativePieceLayer.Count != 0 || level.NativeMegaTileLayer.Count != 0)))) {
                throw new InvalidDataException("Native Cathedral generation cannot replace Town or another native map layer.");
            }
            nativeGenerationPlan = catalog.Prepare(world, nativeDunGeometry);
            nativeGenerationPlan.Apply(world);
            generatedGeometry.AddRange(nativeGenerationPlan.Candidates.Select(candidate => (candidate.Level, candidate.Identity.FineFingerprint)));
        }
        if (cathedralDepths) {
            if (world is null || nativeDunGeometry is null)
                throw new InvalidDataException("Native Cathedral depths require a world and validated external geometry catalogs.");
            var catalog = AuthoritativeCathedralDepthRuntimeCatalog.Load(depthLevelsTable!.ToCanonicalTsv(), stableIds);
            if (catalog.Definitions.Any(definition => definition.LevelId == worldRules.TownLevelId
                || world.Levels.Any(level => level.LevelId == definition.LevelId
                    && (level.NativePieceLayer.Count != 0 || level.NativeMegaTileLayer.Count != 0))))
                throw new InvalidDataException("Native Cathedral depths cannot replace Town or another native map layer.");
            depthGenerationPlan = catalog.Prepare(world, nativeDunGeometry);
            depthGenerationPlan.Apply(world);
            generatedGeometry.AddRange(depthGenerationPlan.Candidates.Select(candidate => (candidate.Level, candidate.Identity.FineFingerprint)));
        }
        var nativeDunFragments = content.TryGetTable(
                "native_dun_fragments.tsv",
                out var nativeDunFragmentsTable)
            ? LoadNativeDunFragments(content, nativeDunFragmentsTable, stableIds)
            : null;
        if (nativeGenerationPlan is not null && nativeDunFragments?.Definitions.Any(definition =>
                nativeGenerationPlan.Candidates.Any(candidate => candidate.Level.LevelId == definition.LevelId)) == true) {
            throw new InvalidDataException("Ordinary generated Cathedral level 1 does not yet support native quest fragments.");
        }
        if (depthGenerationPlan is not null && nativeDunFragments?.Definitions.Any(definition =>
                depthGenerationPlan.Candidates.Any(candidate => candidate.Level.LevelId == definition.LevelId)) == true)
            throw new InvalidDataException("Ordinary generated Cathedral depths do not yet support native quest fragments.");
        var nativeDunFragmentObjects = content.TryGetTable(
                "native_dun_fragment_objects.tsv",
                out var nativeDunFragmentObjectsTable)
            ? LoadNativeDunFragmentObjects(content, nativeDunFragmentObjectsTable, stableIds)
            : null;
        var questSequenceVariants = content.TryGetTable(
                "quest_sequence_variants.tsv",
                out var questSequenceVariantsTable)
            ? LoadQuestSequenceVariants(content, questSequenceVariantsTable, stableIds)
            : null;
        var levelGeneration = LoadLevelGeneration(content);
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
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeMonsterTypeCatalog.ValidateExternalizedProduction(monsterTypesTable);
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            monsterTypesTable.SourcePath,
            monsterTypesTable.ToCanonicalTsv(),
            stableIds);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        var monsterSpawnPolicies = LoadMonsterSpawnPolicies(content, stableIds);
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
            ? LoadNativeDunMonsters(content, nativeDunMonstersTable, stableIds)
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
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeMonsterMissileCatalog.ValidateExternalizedProduction(monsterMissilesTable);
        var monsterMissiles = AuthoritativeMonsterMissileCatalog.LoadTsv(
            monsterMissilesTable.SourcePath,
            monsterMissilesTable.ToCanonicalTsv(),
            stableIds);
        monsterMissiles.ValidateSpellReferences(spells);
        monsterMissiles.ValidateMonsterSelectorReferences(monsterTypes!);
        monsterMissiles.ValidateSpawnMonsterReferences(monsterTypes!);
        monsterMissiles.ValidateMissileReferences(nativeMissiles);
        var monstersTable = GetRequiredExternalTable(content, "monsters.tsv");
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeMonsterCatalog.ValidateExternalizedProduction(monstersTable);
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
            ? LoadUniqueMonsters(content, uniqueMonstersTable, stableIds)
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
            ? LoadUniqueMonsterPlacements(content, uniqueMonsterPlacementsTable, stableIds)
            : null;
        var uniqueMonsterPacks = content.TryGetTable(
            "unique_monster_packs.tsv",
            out var uniqueMonsterPacksTable)
            ? LoadUniqueMonsterPacks(content, uniqueMonsterPacksTable, stableIds)
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
            ? LoadWorldObjects(content, objectsTable, stableIds)
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
            ? LoadObjectItemGates(content, objectItemGatesTable, stableIds)
            : null;
        var objectPresentations = content.TryGetTable(
            "object_presentations.tsv",
            out var objectPresentationsTable)
            ? LoadObjectPresentations(content, objectPresentationsTable)
            : null;
        objectPresentations?.Apply((objects ?? []).Concat(objectTemplates ?? []));
        nativeDunFragmentObjects?.ValidateTemplates(objectTemplates ?? []);
        var objectDefinitions = (objects ?? [])
            .Concat(objectTemplates ?? [])
            .ToArray();
        var worldMutations = content.TryGetTable(
            "world_mutations.tsv",
            out var worldMutationsTable)
            ? LoadWorldMutations(content, worldMutationsTable, stableIds)
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
            ? LoadSetLevelNames(content, setLevelNamesTable, stableIds)
            : null;
        if (setLevelNames is not null)
            stableIds.ValidateReferences(
                "set_level",
                setLevelId => setLevelId <= int.MaxValue
                    && setLevelNames.TryGet((int)setLevelId, out _));
        var proceduralSetPieceOrigins = content.TryGetTable(
                "procedural_set_piece_origins.tsv",
                out var proceduralSetPieceOriginsTable)
            ? LoadProceduralSetPieceOrigins(
                content,
                proceduralSetPieceOriginsTable,
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
            ? LoadProceduralQuestPieceOrigins(
                content,
                proceduralQuestPieceOriginsTable,
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
            ? LoadSetLevelObjects(content, setLevelObjectsTable, stableIds)
            : null;
        var nativeDunObjects = content.TryGetTable(
            "native_dun_objects.tsv",
            out var nativeDunObjectsTable)
            ? LoadNativeDunObjects(content, nativeDunObjectsTable, stableIds)
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
            ? LoadSetLevelMaps(content, setLevelMapsTable, stableIds)
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
        var warps = world is not null && content.TryGetTable("warps.tsv", out var warpsTable)
            ? LoadWarps(content, warpsTable, world, stableIds, setLevelMaps)
            : [];
        var portals = content.TryGetTable("portals.tsv", out var portalsTable)
            ? LoadPortals(content, portalsTable, stableIds, setLevelMaps)
            : null;
        DiabloNativeGeneratedWorldIdentity? nativeGeneratedIdentity = null;
        DiabloCathedralDepthWorldIdentity? cathedralDepthIdentity = null;
        if (nativeGenerationPlan is not null) {
            var town = world!.Levels.SingleOrDefault(level => level.LevelId == worldRules.TownLevelId)
                ?? throw new InvalidDataException("Native generated routes require an active Town level.");
            var routeBindings = AuthoritativeNativeGeneratedRouteBindings.Load(generatedRoutesTable!.ToCanonicalTsv(), stableIds);
            var resolved = AuthoritativeNativeGeneratedRouteBindings.Resolve(world, town, nativeGenerationPlan, warps, portals ?? [], routeBindings);
            warps = resolved.Warps;
            portals = resolved.Portals;
            nativeGeneratedIdentity = new DiabloNativeGeneratedWorldIdentity(
                nativeGenerationPlan.Candidates.Select(candidate => candidate.Identity).ToArray(), resolved.RouteFingerprint);
        }
        if (depthGenerationPlan is not null) {
            var town = world!.Levels.SingleOrDefault(level => level.LevelId == worldRules.TownLevelId)
                ?? throw new InvalidDataException("Native Cathedral depth routes require an active Town level.");
            var routeBindings = AuthoritativeNativeGeneratedRouteBindings.Load(generatedRoutesTable!.ToCanonicalTsv(), stableIds);
            var resolved = AuthoritativeNativeGeneratedRouteBindings.Resolve(world, town, depthGenerationPlan, warps, portals ?? [], routeBindings);
            warps = resolved.Warps;
            portals = resolved.Portals;
            cathedralDepthIdentity = new DiabloCathedralDepthWorldIdentity(
                depthGenerationPlan.Candidates.Select(candidate => candidate.Identity).ToArray(), resolved.RouteFingerprint);
        }
        if (world is not null) {
            foreach (var warp in warps)
                world.AddWarpTarget(warp);
            stableIds.ValidateReferences("warp", warpId => world.WarpTargets.Any(warp => warp.WarpId == warpId));
        }
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
            ? LoadQuestTypes(content, questTypesTable, setLevelNames, stableIds)
            : null;
        if (questTypes is not null)
            stableIds.ValidateReferences("quest", questId => questTypes.TryGet(questId, out _));
        var questPools = content.TryGetTable("quest_pools.tsv", out var questPoolsTable)
            ? LoadQuestPools(content, questPoolsTable, questTypes, stableIds)
            : null;
        var quests = content.TryGetTable("quests.tsv", out var questsTable)
            ? LoadQuests(content, questsTable, questTypes, stableIds)
            : null;
        if (content.TryGetTable("quest_states.tsv", out var questStatesTable)) {
            var additionalQuests = LoadQuestStates(
                content,
                questStatesTable,
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
            ? LoadQuestTownerInteractions(content, questTownerInteractionsTable, stableIds)
            : null;
        var questDialogueStages = content.TryGetTable(
            "quest_dialogue_stages.tsv",
            out var questDialogueStagesTable)
            ? LoadQuestDialogueStages(content, questDialogueStagesTable, stableIds)
            : null;
        var questRouteMessages = content.TryGetTable(
            "quest_route_messages.tsv",
            out var questRouteMessagesTable)
            ? LoadQuestRouteMessages(content, questRouteMessagesTable, stableIds)
            : null;
        var townerGreetings = content.TryGetTable(
            "towner_greetings.tsv",
            out var townerGreetingsTable)
            ? LoadTownerGreetings(content, townerGreetingsTable, stableIds)
            : null;
        var dialogueMessages = content.TryGetTable(
            "dialogue_messages.tsv",
            out var dialogueMessagesTable)
            ? LoadDialogueMessages(content, dialogueMessagesTable)
            : null;
        dialogueMessages?.ValidateMessageIds(
            (questDialogueStages?.Definitions ?? [])
                .SelectMany(stage => new[] { stage.MessageId, stage.NextMessageId }));
        dialogueMessages?.ValidateMessageIds(
            (questRouteMessages?.Definitions ?? [])
                .Select(definition => definition.MessageId));
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
            questRouteMessages?.ValidateQuests(quests);
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
            ? LoadItemQuestEffects(content, itemQuestEffectsTable, stableIds)
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

        var levelStartCatalog = content.TryGetTable(
                "level_start_positions.tsv",
                out var levelStartTable)
            ? AuthoritativeLevelStartCatalog.LoadTsv(
                levelStartTable.SourcePath,
                levelStartTable.ToCanonicalTsv(),
                stableIds)
            : null;
        if (levelStartCatalog is not null && world is null)
            throw new InvalidDataException(
                "Level-start positions require an active world.");
        if (generatedGeometry.Count != 0 && levelStartCatalog?.Definitions.Any(definition =>
                generatedGeometry.Any(candidate => candidate.Level.LevelId == definition.LevelId)) == true) {
            throw new InvalidDataException("Generated level entries cannot also have fixed level-start overrides.");
        }

        var rules = new DiabloStoreRules(pricing, itemCatalog);
        var simulation = simulationFactory.Create(new DiabloSimulationOptions(stores, rules) {
            World = world,
            NativeGeneratedWorldIdentity = nativeGeneratedIdentity,
            CathedralDepthWorldIdentity = cathedralDepthIdentity,
            WorldMutations = worldMutations,
            WorldRules = worldRules,
            CombatTargets = combatTargets,
            Portals = portals,
            Objects = objects,
            Quests = quests,
            QuestTypes = questTypes,
            QuestTownerInteractions = questTownerInteractions,
            QuestDialogueStages = questDialogueStages,
            QuestRouteMessages = questRouteMessages,
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
            RequireExternalContentCatalogs = true,
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
        if (levelStartCatalog is not null) {
            var finalWorld = simulation.WorldState.World
                ?? throw new InvalidDataException(
                    "Level-start positions require an active simulation world.");
            levelStartCatalog.ValidateEffectiveGeometry(simulation.WorldState);
            levelStartCatalog.Apply(finalWorld);
            simulation.WorldState.RefreshProjectedGeometry();
        }
        if (generatedGeometry.Count != 0) {
            foreach (var candidate in generatedGeometry) {
                var effective = simulation.WorldState.LevelGeometry.SingleOrDefault(level => level.LevelId == candidate.Level.LevelId);
                if (effective is null || effective.StartPositionX != candidate.Level.StartPositionX
                    || effective.StartPositionY != candidate.Level.StartPositionY
                    || effective.StableSymbol != candidate.Level.StableSymbol
                    || effective.IsDefaultStart != candidate.Level.IsDefaultStart
                    || effective.SuppressMultiplayerDeathDrops != candidate.Level.SuppressMultiplayerDeathDrops
                    || effective.NativeMegaTileLayer.Count != 0
                    || !string.Equals(candidate.FineFingerprint,
                        AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(effective), StringComparison.Ordinal)) {
                    throw new InvalidDataException("Generated level geometry was changed by an incompatible activation fragment or entry override.");
                }
            }
            foreach (var warp in warps) {
                if (!simulation.WorldState.IsWalkable(warp.SourceLevelId, warp.TriggerX, warp.TriggerY)
                    || !simulation.WorldState.IsWalkable(warp.TargetLevelId, warp.TargetX, warp.TargetY)) {
                    throw new InvalidDataException("Generated routes contain a blocked endpoint in final effective geometry.");
                }
            }
            foreach (var portal in portals ?? []) {
                if (!simulation.WorldState.IsWalkable(portal.SourceLevelId, portal.SourcePositionX, portal.SourcePositionY)
                    || !simulation.WorldState.IsWalkable(portal.DestinationLevelId, portal.DestinationPositionX, portal.DestinationPositionY)) {
                    throw new InvalidDataException("Generated portals contain a blocked endpoint in final effective geometry.");
                }
            }
        }
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
            questRouteMessages,
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
            nativeDunGeometry: nativeDunGeometry,
            nativeSpells: legacyCompatibility.NativeSpells,
            nativeItemMisc: legacyCompatibility.NativeItemMisc,
            nativeItemTopology: legacyCompatibility.NativeItemTopology,
            nativeItemTags: legacyCompatibility.NativeItemTags);
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

    private static AuthoritativeNativeDunFragmentCatalog LoadNativeDunFragments(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeDunFragmentCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeNativeDunFragmentObjectCatalog LoadNativeDunFragmentObjects(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeDunFragmentObjectCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeNativeDunMonsterCatalog LoadNativeDunMonsters(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeDunMonsterCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeNativeDunMonsterCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeUniqueMonsterCatalog LoadUniqueMonsters(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeUniqueMonsterCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeUniqueMonsterCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeUniqueMonsterPlacementCatalog LoadUniqueMonsterPlacements(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeUniqueMonsterPlacementCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeUniqueMonsterPlacementCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeUniqueMonsterPackCatalog LoadUniqueMonsterPacks(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeUniqueMonsterPackCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeUniqueMonsterPackCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeObjectItemGateCatalog LoadObjectItemGates(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeObjectItemGateCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeObjectItemGateCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeWorldObjectPresentationCatalog LoadObjectPresentations(
        ComposedContent content,
        TsvTable table)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWorldObjectPresentationCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv());
    }

    private static AuthoritativeItemQuestEffectCatalog LoadItemQuestEffects(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemQuestEffectCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeItemQuestEffectCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeQuestSequenceVariantCatalog LoadQuestSequenceVariants(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestSequenceVariantCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestSequenceVariantCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeQuestTownerInteractionCatalog LoadQuestTownerInteractions(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestTownerInteractionCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestTownerInteractionCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeQuestDialogueStageCatalog LoadQuestDialogueStages(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestDialogueStageCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestDialogueStageCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeQuestRouteMessageCatalog LoadQuestRouteMessages(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestRouteMessageCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestRouteMessageCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeTownerGreetingCatalog LoadTownerGreetings(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeTownerGreetingCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeTownerGreetingCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeDialogueMessageCatalog LoadDialogueMessages(
        ComposedContent content,
        TsvTable table)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeDialogueMessageCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeDialogueMessageCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv());
    }

    private static AuthoritativeSetLevelNameCatalog LoadSetLevelNames(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeSetLevelNameCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeSetLevelNameCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeQuestTypeCatalog LoadQuestTypes(
        ComposedContent content,
        TsvTable table,
        AuthoritativeSetLevelNameCatalog? setLevelNames,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestTypeCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestTypeCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            setLevelNames,
            stableIds);
    }

    private static AuthoritativeQuestPoolCatalog LoadQuestPools(
        ComposedContent content,
        TsvTable table,
        AuthoritativeQuestTypeCatalog? questTypes,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestPoolCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestPoolCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            questTypes,
            stableIds);
    }

    private static AuthoritativeWorldMutationCatalog LoadWorldMutations(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWorldMutationCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeWorldMutationCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds,
            content);
    }

    private static AuthoritativeProceduralSetPieceOriginCatalog LoadProceduralSetPieceOrigins(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeProceduralSetPieceOriginCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeProceduralSetPieceOriginCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeProceduralQuestPieceOriginCatalog LoadProceduralQuestPieceOrigins(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeProceduralQuestPieceOriginCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeProceduralQuestPieceOriginCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeNativeDunObjectCatalog LoadNativeDunObjects(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeDunObjectCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeNativeDunObjectCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeNativeDunLevelCatalog LoadNativeDunLevels(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeNativeDunLevelCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeNativeDunLevelCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeNativeDunGeometryCatalog? LoadNativeDunGeometry(
        ComposedContent content)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current) {
            if (content.TryGetTable(
                    AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
                    out var piecesTable)) {
                AuthoritativeDunPieceCatalog.ValidateExternalizedProduction(piecesTable);
            }
            if (content.TryGetTable(
                    AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
                    out var propertiesTable)) {
                AuthoritativeDunTilePropertyCatalog.ValidateExternalizedProduction(propertiesTable);
            }
        }
        return AuthoritativeNativeDunGeometryCatalog.LoadOptional(content);
    }

    private static LegacyCompatibilityCatalogs LoadLegacyCompatibilityCatalogs(
        ComposedContent content)
    {
        // These catalogs are additive outputs of the current production
        // contract. Previous-schema packs keep their direct legacy loaders,
        // but activation must not publish partially validated compatibility
        // catalogs to the current module surface.
        if (content.Manifest.Schema != DiabloContentSchema.Current)
            return new LegacyCompatibilityCatalogs(null, null, null, null);

        return new LegacyCompatibilityCatalogs(
            LoadCompatibilityCatalog(
                content,
                "native_spells.tsv",
                AuthoritativeNativeSpellCatalog.ValidateExternalizedProduction,
                static table => AuthoritativeNativeSpellCatalog.LoadTsv(
                    table.SourcePath,
                    table.ToCanonicalTsv())),
            LoadCompatibilityCatalog(
                content,
                "native_item_misc.tsv",
                AuthoritativeNativeMiscCatalog.ValidateExternalizedProduction,
                static table => AuthoritativeNativeMiscCatalog.LoadTsv(
                    table.SourcePath,
                    table.ToCanonicalTsv())),
            LoadCompatibilityCatalog(
                content,
                "native_item_topology.tsv",
                AuthoritativeNativeItemTopologyCatalog.ValidateExternalizedProduction,
                static table => AuthoritativeNativeItemTopologyCatalog.LoadTsv(
                    table.SourcePath,
                    table.ToCanonicalTsv())),
            LoadCompatibilityCatalog(
                content,
                "native_item_tags.tsv",
                AuthoritativeNativeItemTagCatalog.ValidateExternalizedProduction,
                static table => AuthoritativeNativeItemTagCatalog.LoadTsv(
                    table.SourcePath,
                    table.ToCanonicalTsv())));
    }

    private static TCatalog? LoadCompatibilityCatalog<TCatalog>(
        ComposedContent content,
        string sourcePath,
        Action<TsvTable> validator,
        Func<TsvTable, TCatalog> loader)
        where TCatalog : class
    {
        TsvTable table;
        if (content.Manifest.Schema == DiabloContentSchema.Current) {
            if (!content.TryGetTable(sourcePath, out table!))
                throw new InvalidDataException(
                    $"Production Diablo activation requires external '{sourcePath}' content "
                    + $"(requires '{sourcePath}' compatibility content).");
            validator(table);
        } else if (!content.TryGetTable(sourcePath, out table!)) {
            return null;
        }
        return loader(table);
    }

    private sealed record LegacyCompatibilityCatalogs(
        AuthoritativeNativeSpellCatalog? NativeSpells,
        AuthoritativeNativeMiscCatalog? NativeItemMisc,
        AuthoritativeNativeItemTopologyCatalog? NativeItemTopology,
        AuthoritativeNativeItemTagCatalog? NativeItemTags);

    private static AuthoritativeSetLevelObjectCatalog LoadSetLevelObjects(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeSetLevelObjectCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeSetLevelObjectCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeSetLevelMapCatalog LoadSetLevelMaps(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeSetLevelMapCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeSetLevelMapCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static IReadOnlyList<AuthoritativeWarpTarget> LoadWarps(
        ComposedContent content,
        TsvTable table,
        AuthoritativeWorld world,
        StableContentIdCatalog stableIds,
        AuthoritativeSetLevelMapCatalog? setLevelMaps)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWarpCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeWarpCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            world,
            stableIds,
            setLevelMaps);
    }

    private static IReadOnlyList<AuthoritativePortal> LoadPortals(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds,
        AuthoritativeSetLevelMapCatalog? setLevelMaps)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativePortalCatalog.ValidateExternalizedProduction(table);
        return AuthoritativePortalCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds,
            setLevelMaps);
    }

    private static AuthoritativeWorld LoadWorldLevels(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWorld.ValidateExternalizedProduction(table);
        return AuthoritativeWorld.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static AuthoritativeLevelGenerationCatalog? LoadLevelGeneration(
        ComposedContent content)
    {
        TsvTable table;
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            table = GetRequiredExternalTable(content, "level_generation.tsv");
        else if (!content.TryGetTable("level_generation.tsv", out table!))
            return null;

        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeLevelGenerationCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeLevelGenerationCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv());
    }

    private static AuthoritativeItemCombinationCatalog LoadItemCombinations(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeItemCombinationCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeItemCombinationCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static IReadOnlyList<AuthoritativeWorldObject> LoadWorldObjects(
        ComposedContent content,
        TsvTable table,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeWorldObjectCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeWorldObjectCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
    }

    private static IReadOnlyList<AuthoritativeQuestState> LoadQuests(
        ComposedContent content,
        TsvTable table,
        AuthoritativeQuestTypeCatalog? questTypes,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestCatalog.ValidateExternalizedProduction(table);
        return AuthoritativeQuestCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            questTypes,
            stableIds);
    }

    private static IReadOnlyList<AuthoritativeQuestState> LoadQuestStates(
        ComposedContent content,
        TsvTable table,
        AuthoritativeQuestTypeCatalog? questTypes,
        StableContentIdCatalog stableIds)
    {
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeQuestCatalog.ValidateExternalizedStateProduction(table);
        return AuthoritativeQuestCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            questTypes,
            stableIds);
    }

    private static AuthoritativeMonsterSpawnPolicyCatalog? LoadMonsterSpawnPolicies(
        ComposedContent content,
        StableContentIdCatalog stableIds)
    {
        TsvTable table;
        if (content.Manifest.Schema == DiabloContentSchema.Current)
            table = GetRequiredExternalTable(content, "monster_spawn_policies.tsv");
        else if (!content.TryGetTable("monster_spawn_policies.tsv", out table!))
            return null;

        if (content.Manifest.Schema == DiabloContentSchema.Current)
            AuthoritativeMonsterSpawnPolicyCatalog.ValidateExternalizedProduction(table);
        var catalog = AuthoritativeMonsterSpawnPolicyCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv(),
            stableIds);
        if (content.Manifest.Schema == DiabloContentSchema.Current
            && !catalog.RequiresCompleteCoverage) {
            throw new InvalidDataException(
                "Current-schema monster-spawn policy content must declare Complete coverage.");
        }
        return catalog;
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
