using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloModuleQuestPoolSelectionTests
{
    private static readonly SeedVector[] NativeSeedVectors = [
        new(0, [3013, 3008, 3009, 3011, 3005]),
        new(1, [3013, 3007, 3010, 3004, 3012]),
        new(1457187811, [3013, 3008, 3009, 3004, 3005]),
        new(988045466, [3007, 3001, 3002, 3005]),
        new(4203210069, [3013, 3008, 3009, 3004, 3012]),
        new(2557708932, [3013, 3008, 3001, 3002, 3012]),
        new(1272442071, [3013, 3003, 3001, 3004, 3005]),
        new(509604, [3014, 3003, 3001, 3004, 3005]),
    ];

    private static readonly SeedVector[] LegacySeedVectors = [
        new(1457187811, [3013, 3007, 3001, 3004, 3005]),
        new(988045466, [3003, 3009, 3002, 3005]),
        new(4203210069, [3013, 3007, 3001, 3004, 3012]),
        new(2557708932, [3013, 3007, 3009, 3002, 3012]),
        new(1272442071, [3013, 3008, 3009, 3004, 3005]),
        new(509604, [3014, 3008, 3009, 3004, 3005]),
        new(0, [3013, 3007, 3001, 3011, 3005]),
        new(1, [3013, 3003, 3010, 3004, 3012]),
    ];

    private static readonly uint[] BaseQuestStateRowIds = [
        3007, 3008, 3003, 3013, 3004, 3006, 3012, 3016, 3014,
    ];

    [Fact]
    public void ExistingConstructorsAndInstanceKeepLegacyIdentityAndDefaultPricing()
    {
        // The default literal must continue binding to the original pricing overload.
        var defaultLiteral = new DiabloModule(default);
        var ordinary = new DiabloModule();
        var explicitLegacy = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
            pricing: null);
        var explicitNullPricing = new DiabloModule(null);
        var customPricing = new StoreServicePricing(identificationPrice: 222);
        var customLegacy = new DiabloModule(customPricing);

        AssertLegacyModule(defaultLiteral);
        AssertLegacyModule(ordinary);
        AssertLegacyModule(explicitLegacy);
        AssertLegacyModule(explicitNullPricing);
        AssertLegacyModule(DiabloModule.Instance);
        AssertLegacyModule(customLegacy);
        Assert.Same(StoreServicePricing.Default, defaultLiteral.Pricing);
        Assert.Same(StoreServicePricing.Default, ordinary.Pricing);
        Assert.Same(StoreServicePricing.Default, explicitLegacy.Pricing);
        Assert.Same(StoreServicePricing.Default, explicitNullPricing.Pricing);
        Assert.Same(customPricing, customLegacy.Pricing);

        var content = LoadBaseAndHellfireContent();
        AssertExpectedSinglePlayerVector(
            DiabloModule.Instance,
            content,
            LegacySeedVectors.Single(vector => vector.Seed == 988045466));
        AssertExpectedSinglePlayerVector(
            explicitLegacy,
            content,
            LegacySeedVectors.Single(vector => vector.Seed == 0));
        AssertExpectedSinglePlayerVector(
            explicitNullPricing,
            content,
            LegacySeedVectors.Single(vector => vector.Seed == 988045466));
    }

    [Fact]
    public void NativePolicyHasStableDistinctIdentityAndForwardsExplicitPricing()
    {
        var pricing = new StoreServicePricing(identificationPrice: 321);
        var first = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            pricing);
        var second = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            pricing: null);
        var legacy = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
            pricing: null);
        var legacyContent = LoadBaseAndHellfireContent();
        var nativeContent = LoadBaseAndHellfireContent();

        Assert.Equal(legacyContent.Manifest.Sha256, nativeContent.Manifest.Sha256);
        var legacyRuleset = new GameplayRulesetIdentity(legacyContent.Manifest, [legacy.Identity]);
        var firstNativeRuleset = new GameplayRulesetIdentity(nativeContent.Manifest, [first.Identity]);
        var secondNativeRuleset = new GameplayRulesetIdentity(nativeContent.Manifest, [second.Identity]);
        Assert.Equal(firstNativeRuleset.Content.Sha256, secondNativeRuleset.Content.Sha256);
        Assert.Equal(firstNativeRuleset.CombinedSha256, secondNativeRuleset.CombinedSha256);
        Assert.NotEqual(legacyRuleset.CombinedSha256, firstNativeRuleset.CombinedSha256);
        Assert.Equal(AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1, first.QuestPoolSelectionOrder);
        Assert.Same(first.ContentBootstrap, first.NativeGeneratedContentBootstrap);
        Assert.Same(first.ContentBootstrap, first.CathedralDepthContentBootstrap);
        Assert.Same(first.ContentBootstrap, first.CathedralDoorContentBootstrap);
        Assert.Same(pricing, first.Pricing);
        Assert.Same(pricing, Assert.IsType<DiabloStoreRules>(first.Rules).Pricing);
        Assert.Same(StoreServicePricing.Default, second.Pricing);
        AssertModuleIdentity(first.Identity, "0.2.0");
        AssertModuleIdentity(second.Identity, "0.2.0");
        Assert.Equal(first.Identity.AssemblySha256, second.Identity.AssemblySha256);
        Assert.NotEqual(legacy.Identity.AssemblySha256, first.Identity.AssemblySha256);
        AssertModuleIdentity(legacy.Identity, "0.1.0");

        var rulesetBeforeActivation = firstNativeRuleset.CombinedSha256;
        _ = first.ContentBootstrap.Activate(
            nativeContent,
            randomSeed: 988045466,
            gameMode: DiabloGameMode.SinglePlayer);
        var rulesetAfterActivation = new GameplayRulesetIdentity(
            nativeContent.Manifest,
            [first.Identity]);
        Assert.Equal(rulesetBeforeActivation, rulesetAfterActivation.CombinedSha256);
        Assert.Equal(firstNativeRuleset.Content.Sha256, rulesetAfterActivation.Content.Sha256);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DiabloModule(
            (AuthoritativeQuestPoolSelectionOrder)12345,
            pricing: null));
    }

    [Fact]
    public void BothPoliciesMatchAllLiteralQuestPoolVectorsOnIntactBaseContent()
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        Assert.Empty(
            basePack.Tables
                .Single(table => table.SourcePath == "quest_sequence_variants.tsv")
                .Rows);
        Assert.Equal(
            BaseQuestStateRowIds,
            basePack.Tables
                .Single(table => table.SourcePath == "quest_states.tsv")
                .Rows
                .Select(row => row.RequiredUInt32("quest_id")));

        var content = ComposedContent.Compose(new ContentManifest(
            "explicit-base-quest-pool-policy-test",
            "1",
            [basePack]));
        var authoredTableSnapshot = SnapshotAuthoredTables(basePack);
        var composedTableSnapshot = SnapshotComposedTables(content);
        foreach (var (selectionOrder, vectors) in new[] {
                     (AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1, LegacySeedVectors),
                     (AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1, NativeSeedVectors),
                 }) {
            Assert.Equal(8, vectors.Length);
            var module = selectionOrder == AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1
                ? new DiabloModule(default)
                : new DiabloModule(selectionOrder, pricing: null);
            Assert.Equal(selectionOrder, module.QuestPoolSelectionOrder);
            foreach (var vector in vectors)
                AssertExpectedSinglePlayerVector(module, content, vector);
        }
        Assert.Equal(authoredTableSnapshot, SnapshotAuthoredTables(basePack));
        Assert.Equal(composedTableSnapshot, SnapshotComposedTables(content));
    }

    [Fact]
    public void ModulePoliciesApplySuccessfulLiteralVectorsToComposedBaseAndHellfireQuestState()
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "hellfire"));
        var content = ComposedContent.Compose(new ContentManifest(
            "explicit-quest-pool-policy-test",
            "1",
            [basePack, hellfirePack]));
        var authoredTableSnapshot = SnapshotAuthoredTables(basePack, hellfirePack);
        var composedTableSnapshot = SnapshotComposedTables(content);
        var authoredBaseStateIds = basePack.Tables
            .Single(table => table.SourcePath == "quest_states.tsv")
            .Rows
            .Select(row => row.RequiredUInt32("quest_id"))
            .ToArray();
        Assert.Equal(BaseQuestStateRowIds, authoredBaseStateIds);

        foreach (var (selectionOrder, vectors) in new[] {
                     (AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1, LegacySeedVectors),
                     (AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1, NativeSeedVectors),
                 }) {
            var module = selectionOrder == AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1
                ? new DiabloModule(default)
                : new DiabloModule(selectionOrder, pricing: null);
            Assert.Equal(selectionOrder, module.QuestPoolSelectionOrder);
            Assert.Same(module.ContentBootstrap, module.NativeGeneratedContentBootstrap);
            Assert.Same(module.ContentBootstrap, module.CathedralDepthContentBootstrap);
            Assert.Same(module.ContentBootstrap, module.CathedralDoorContentBootstrap);

            var successfulVectors = vectors
                .Where(vector => vector.Seed != 1457187811)
                .ToArray();
            Assert.Equal(7, successfulVectors.Length);
            foreach (var vector in successfulVectors) {
                var baselineById = LoadUnselectedQuestStates(content)
                    .ToDictionary(quest => quest.QuestId);
                var activation = module.ContentBootstrap.Activate(
                    content,
                    randomSeed: vector.Seed,
                    gameMode: DiabloGameMode.SinglePlayer);
                var questPools = Assert.IsType<AuthoritativeQuestPoolCatalog>(activation.QuestPools);
                var poolQuestIds = questPools.Definitions.Select(entry => entry.QuestId).ToHashSet();
                Assert.DoesNotContain(3006U, poolQuestIds);
                Assert.DoesNotContain(3016U, poolQuestIds);
                Assert.All(vector.QuestIds, questId => Assert.Contains(questId, poolQuestIds));
                var quests = Assert.IsAssignableFrom<IReadOnlyList<AuthoritativeQuestState>>(
                    activation.Simulation.WorldState.Quests.Values.ToArray());
                var actualById = quests.ToDictionary(quest => quest.QuestId);
                Assert.Equal(baselineById.Keys.Order(), actualById.Keys.Order());

                var expectedUnavailable = vector.QuestIds
                    .Where(id => baselineById.TryGetValue(id, out var before) && !before.Completed)
                    .Order()
                    .ToArray();
                Assert.Equal(
                    expectedUnavailable,
                    quests.Where(quest => quest.Unavailable)
                        .Select(quest => quest.QuestId)
                        .Order()
                        .ToArray());

                foreach (var quest in quests) {
                    var before = baselineById[quest.QuestId];
                    var selectedAndIncomplete = !before.Completed
                        && vector.QuestIds.Contains(quest.QuestId);
                    Assert.Equal(before.Completed, quest.Completed);
                    Assert.Equal(before.Progress, quest.Progress);
                    Assert.Equal(before.RequiredProgress, quest.RequiredProgress);
                    Assert.Equal(before.LevelId, quest.LevelId);
                    Assert.Equal(before.Variable1, quest.Variable1);
                    Assert.Equal(before.Variable2, quest.Variable2);
                    Assert.Equal(before.MessageId, quest.MessageId);
                    Assert.Equal(selectedAndIncomplete, quest.Unavailable);
                    Assert.Equal(selectedAndIncomplete ? false : before.Active, quest.Active);
                    Assert.Equal(selectedAndIncomplete ? false : before.Logged, quest.Logged);
                }

                foreach (var nonPoolQuestId in new uint[] { 3006, 3016 }) {
                    var before = baselineById[nonPoolQuestId];
                    var actual = actualById[nonPoolQuestId];
                    Assert.False(actual.Unavailable);
                    Assert.Equal(before.Completed, actual.Completed);
                    Assert.Equal(before.Active, actual.Active);
                    Assert.Equal(before.Logged, actual.Logged);
                }
            }
        }

        Assert.Equal(
            new uint[] { 3017, 3018, 3020, 3021, 3022, 3023, 3024, 3005 },
            hellfirePack.Tables
                .Single(table => table.SourcePath == "quest_states.tsv")
                .Rows
                .Select(row => row.RequiredUInt32("quest_id")));
        Assert.Equal(authoredTableSnapshot, SnapshotAuthoredTables(basePack, hellfirePack));
        Assert.Equal(composedTableSnapshot, SnapshotComposedTables(content));
    }

    [Fact]
    public void BothPoliciesRejectUnrepresentableHellfireVariantRollWithoutMutatingAuthoredTables()
    {
        const uint invalidVariantSeed = 1457187811;
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "hellfire"));
        var content = ComposedContent.Compose(new ContentManifest(
            "explicit-quest-pool-policy-test",
            "1",
            [basePack, hellfirePack]));
        var authoredTableSnapshot = SnapshotAuthoredTables(basePack, hellfirePack);
        var composedTableSnapshot = SnapshotComposedTables(content);
        var manifestSha256 = content.Manifest.Sha256;

        foreach (var selectionOrder in new[] {
                     AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
                     AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
                 }) {
            var module = selectionOrder == AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1
                ? new DiabloModule(default)
                : new DiabloModule(selectionOrder, pricing: null);

            Assert.Throws<InvalidDataException>(() => module.ContentBootstrap.Activate(
                content,
                randomSeed: invalidVariantSeed,
                gameMode: DiabloGameMode.SinglePlayer));
            Assert.Equal(manifestSha256, content.Manifest.Sha256);
            Assert.Equal(authoredTableSnapshot, SnapshotAuthoredTables(basePack, hellfirePack));
            Assert.Equal(composedTableSnapshot, SnapshotComposedTables(content));
        }
    }

    [Fact]
    public void MultiplayerQuestStateIsUnchangedBySelectionPolicy()
    {
        var content = LoadBaseAndHellfireContent();
        var legacy = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
            pricing: null).ContentBootstrap.Activate(
                content,
                randomSeed: 988045466,
                gameMode: DiabloGameMode.Multiplayer);
        var native = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            pricing: null).ContentBootstrap.Activate(
                content,
                randomSeed: 988045466,
                gameMode: DiabloGameMode.Multiplayer);

        var expectedFlags = LoadUnselectedQuestStates(content)
            .OrderBy(quest => quest.QuestId)
            .Select(QuestFlags)
            .ToArray();
        var legacyStates = Assert.IsAssignableFrom<IReadOnlyList<AuthoritativeQuestState>>(
                legacy.Simulation.WorldState.Quests.Values.ToArray())
            .OrderBy(quest => quest.QuestId)
            .Select(QuestFlags)
            .ToArray();
        var nativeStates = Assert.IsAssignableFrom<IReadOnlyList<AuthoritativeQuestState>>(
                native.Simulation.WorldState.Quests.Values.ToArray())
            .OrderBy(quest => quest.QuestId)
            .Select(QuestFlags)
            .ToArray();

        Assert.Equal(expectedFlags, legacyStates);
        Assert.Equal(expectedFlags, nativeStates);
    }

    private static void AssertExpectedSinglePlayerVector(
        DiabloModule module,
        ComposedContent content,
        SeedVector vector)
    {
        var before = LoadUnselectedQuestStates(content).ToDictionary(quest => quest.QuestId);
        var activation = module.ContentBootstrap.Activate(
            content,
            randomSeed: vector.Seed,
            gameMode: DiabloGameMode.SinglePlayer);
        var actual = Assert.IsAssignableFrom<IReadOnlyList<AuthoritativeQuestState>>(
            activation.Simulation.WorldState.Quests.Values.ToArray());
        var expectedUnavailable = vector.QuestIds
            .Where(id => before.TryGetValue(id, out var quest) && !quest.Completed)
            .Order()
            .ToArray();
        Assert.Equal(
            expectedUnavailable,
            actual.Where(quest => quest.Unavailable).Select(quest => quest.QuestId).Order().ToArray());
        foreach (var quest in actual) {
            var initial = before[quest.QuestId];
            var selectedAndIncomplete = !initial.Completed && vector.QuestIds.Contains(quest.QuestId);
            Assert.Equal(initial.Completed, quest.Completed);
            Assert.Equal(selectedAndIncomplete, quest.Unavailable);
            Assert.Equal(selectedAndIncomplete ? false : initial.Active, quest.Active);
            Assert.Equal(selectedAndIncomplete ? false : initial.Logged, quest.Logged);
        }
    }

    private static IReadOnlyList<AuthoritativeQuestState> LoadUnselectedQuestStates(
        ComposedContent content)
    {
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            content.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            content.GetRequiredTable("set_level_names.tsv").ToCanonicalTsv(),
            stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            content.GetRequiredTable("quest_types.tsv").ToCanonicalTsv(),
            setLevels,
            stableIds);
        var questDefinitionsTable = content.GetRequiredTable("quests.tsv");
        var questStatesTable = content.GetRequiredTable("quest_states.tsv");
        var questDefinitions = AuthoritativeQuestCatalog.LoadTsv(
            questDefinitionsTable.SourcePath,
            questDefinitionsTable.ToCanonicalTsv(),
            questTypes,
            stableIds);
        var questStates = AuthoritativeQuestCatalog.LoadTsv(
            questStatesTable.SourcePath,
            questStatesTable.ToCanonicalTsv(),
            questTypes,
            stableIds);
        return AuthoritativeQuestCatalog.Merge(
            questDefinitions,
            questStates,
            questStatesTable.SourcePath);
    }

    private static string[] SnapshotComposedTables(ComposedContent content)
        => content.Tables
            .Select(table => $"{table.SourcePath}\n{table.ToCanonicalTsv()}")
            .ToArray();

    private static string[] SnapshotAuthoredTables(params ContentPack[] packs)
        => packs.SelectMany((pack, packIndex) => pack.Tables.Select(table =>
                $"{packIndex}:{table.SourcePath}\n{table.ToCanonicalTsv()}"))
            .ToArray();

    private static (uint QuestId, bool Unavailable, bool Active, bool Logged) QuestFlags(
        AuthoritativeQuestState quest)
        => (quest.QuestId, quest.Unavailable, quest.Active, quest.Logged);

    private static void AssertLegacyModule(DiabloModule module)
    {
        Assert.Equal(
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
            module.QuestPoolSelectionOrder);
        AssertModuleIdentity(module.Identity, "0.1.0");
    }

    private static void AssertModuleIdentity(GameplayModuleIdentity identity, string expectedVersion)
    {
        Assert.Equal("devilution.diablo", identity.Id);
        Assert.Equal(expectedVersion, identity.Version);
        Assert.Equal(
            GameplayModuleIdentity.BuiltInAssemblyHash("devilution.diablo", expectedVersion),
            identity.AssemblySha256);
        Assert.Empty(identity.Dependencies);
        Assert.Empty(identity.DependencyConstraints);
        Assert.NotNull(identity.Compatibility);
        Assert.Equal(GameplayEngineApiVersion.Current, identity.Compatibility.Minimum);
        Assert.Equal(GameplayEngineApiVersion.Current, identity.Compatibility.Maximum);
    }

    private static ComposedContent LoadBaseAndHellfireContent()
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "hellfire"));
        return ComposedContent.Compose(new ContentManifest(
            "explicit-quest-pool-policy-test",
            "1",
            [basePack, hellfirePack]));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record SeedVector(uint Seed, uint[] QuestIds);
}
/** Actual opt-in bootstrap forwarding checks over compiler-produced Cathedral profiles. The
 * compiler fixture uses labeled synthetic TIL/SOL semantics and makes no native geometry claim. */
public sealed class DiabloModuleCathedralBootstrapQuestPolicyTests
{
    private const uint QuestSelectionSeed = 1;
    private static readonly uint[] LegacySeedOneQuestIds = [3013, 3003, 3010, 3004, 3012];
    private static readonly uint[] NativeSeedOneQuestIds = [3013, 3007, 3010, 3004, 3012];
    private static readonly uint[] CathedralDepthSeeds = [17, 2588, 1, uint.MaxValue];

    [Fact]
    public void NativeGeneratedBootstrapForwardsQuestPolicyForCompiledLevelOneProfile()
    {
        var basePack = LoadBasePack();
        var baseSnapshot = SnapshotTables(basePack);
        var output = NewOutput("native-generated");
        var completed = false;
        try
        {
            var generated = AuthoritativeCathedralLevel1Generator.Generate(
                QuestSelectionSeed,
                AuthoritativeCathedralEntry.Main);
            var profile = CathedralL1PlaytestCompiler.Compile(
                new CathedralL1PlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateLevelOnePieceDefinitions(generated),
                    CreateTileProperties(),
                    QuestSelectionSeed,
                    AuthoritativeCathedralEntry.Main));
            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralL1PlaytestCompiler.ContentDirectoryName));
            AssertQuestTablesMatchBase(basePack, pack);

            var pair = ActivateWithBothPolicies(
                pack,
                (module, content) => module.NativeGeneratedContentBootstrap.ActivateGenerated(
                    content,
                    randomSeed: QuestSelectionSeed,
                    gameMode: DiabloGameMode.SinglePlayer));
            Assert.Equal(profile.ContentManifestHash, pair.Content.Manifest.Sha256);
            Assert.Equal(QuestSelectionSeed, profile.DungeonSeed);
            Assert.Equal("main", profile.Entry);
            Assert.Equal(CathedralL1PlaytestCompiler.AdapterId, profile.AdapterId);
            AssertNativeGeneratedProfileWitness(pair, QuestSelectionSeed);
            Assert.Equal(baseSnapshot, SnapshotTables(basePack));
            completed = true;
        }
        finally
        {
            CleanupSuccessfulOutput(output, completed);
        }
    }

    [Fact]
    public void CathedralDepthBootstrapActivatesQuestDisabledCompiledProfileUnderBothPolicies()
    {
        var basePack = LoadBasePack();
        var baseSnapshot = SnapshotTables(basePack);
        var output = NewOutput("cathedral-depths");
        var completed = false;
        try
        {
            var profile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateDepthPieceDefinitions(),
                    CreateTileProperties(),
                    CathedralDepthSeeds)
                {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });
            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ContentDirectoryName));
            AssertQuestDisabledCompilerTables(basePack, pack);

            var pair = ActivateWithBothPolicies(
                pack,
                (module, content) => module.CathedralDepthContentBootstrap.ActivateDepths(
                    content,
                    randomSeed: QuestSelectionSeed,
                    gameMode: DiabloGameMode.SinglePlayer),
                expectQuestSelection: false);
            Assert.Equal(profile.ContentManifestHash, pair.Content.Manifest.Sha256);
            Assert.Equal(CathedralDepthSeeds, profile.DepthSeeds);
            Assert.False(profile.NativePopulationClaim);
            Assert.Equal(4, profile.Levels.Count);
            AssertCathedralDepthProfileWitness(pair);
            Assert.Equal(baseSnapshot, SnapshotTables(basePack));
            completed = true;
        }
        finally
        {
            CleanupSuccessfulOutput(output, completed);
        }
    }

    [Fact]
    public void CathedralDoorBootstrapActivatesQuestDisabledCompiledProfileUnderBothPolicies()
    {
        var basePack = LoadBasePack();
        var baseSnapshot = SnapshotTables(basePack);
        var output = NewOutput("cathedral-doors");
        var completed = false;
        try
        {
            var profile = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateDoorPieceDefinitions(),
                    CreateTileProperties(),
                    CathedralDepthSeeds));
            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDoorPlaytestCompiler.ContentDirectoryName));
            AssertQuestDisabledCompilerTables(basePack, pack);

            var pair = ActivateWithBothPolicies(
                pack,
                (module, content) => module.CathedralDoorContentBootstrap.ActivateDoors(
                    content,
                    randomSeed: QuestSelectionSeed,
                    gameMode: DiabloGameMode.SinglePlayer),
                expectQuestSelection: false);
            Assert.Equal(profile.ContentManifestHash, pair.Content.Manifest.Sha256);
            Assert.Equal(CathedralDepthSeeds, profile.DepthSeeds);
            Assert.False(profile.NativePopulationClaim);
            Assert.Equal(4, profile.DoorMappings.Count);
            AssertCathedralDoorProfileWitness(pair);
            Assert.Equal(baseSnapshot, SnapshotTables(basePack));
            completed = true;
        }
        finally
        {
            CleanupSuccessfulOutput(output, completed);
        }
    }

    private static PolicyPair ActivateWithBothPolicies(
        ContentPack pack,
        Func<DiabloModule, ComposedContent, DiabloContentActivation> activate,
        bool expectQuestSelection = true)
    {
        var manifest = new ContentManifest(pack.Id, pack.Version, [pack]);
        var content = ComposedContent.Compose(manifest);
        var initialTables = SnapshotComposedTables(content);
        var initialQuests = expectQuestSelection ? LoadUnselectedQuestStates(content).ToDictionary(quest => quest.QuestId) : null;
        var legacyModule = new DiabloModule(default);
        var nativeModule = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            pricing: null);

        AssertModuleIdentity(legacyModule, "0.1.0");
        AssertModuleIdentity(nativeModule, "0.2.0");
        Assert.NotEqual(legacyModule.Identity.AssemblySha256, nativeModule.Identity.AssemblySha256);
        Assert.Equal(
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1,
            legacyModule.QuestPoolSelectionOrder);
        Assert.Equal(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            nativeModule.QuestPoolSelectionOrder);
        var legacyRuleset = new GameplayRulesetIdentity(manifest, [legacyModule.Identity]);
        var nativeRuleset = new GameplayRulesetIdentity(manifest, [nativeModule.Identity]);
        Assert.Equal(legacyRuleset.Content.Sha256, nativeRuleset.Content.Sha256);
        Assert.NotEqual(legacyRuleset.CombinedSha256, nativeRuleset.CombinedSha256);

        var legacy = activate(legacyModule, content);
        var legacyRulesetAfterActivation = new GameplayRulesetIdentity(manifest, [legacyModule.Identity]);
        Assert.Equal(legacyRuleset.CombinedSha256, legacyRulesetAfterActivation.CombinedSha256);
        Assert.Equal(legacyRuleset.Content.Sha256, legacyRulesetAfterActivation.Content.Sha256);

        var native = activate(nativeModule, content);
        var nativeRulesetAfterActivation = new GameplayRulesetIdentity(manifest, [nativeModule.Identity]);
        Assert.Equal(nativeRuleset.CombinedSha256, nativeRulesetAfterActivation.CombinedSha256);
        Assert.Equal(nativeRuleset.Content.Sha256, nativeRulesetAfterActivation.Content.Sha256);

        var repeatedNativeModule = new DiabloModule(
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1,
            pricing: null);
        Assert.Equal(nativeModule.Identity, repeatedNativeModule.Identity);
        var repeatedNativeRuleset = new GameplayRulesetIdentity(manifest, [repeatedNativeModule.Identity]);
        Assert.Equal(nativeRulesetAfterActivation.CombinedSha256, repeatedNativeRuleset.CombinedSha256);
        Assert.Equal(nativeRulesetAfterActivation.Content.Sha256, repeatedNativeRuleset.Content.Sha256);
        if (expectQuestSelection)
        {
            AssertQuestPolicyResult(legacy, initialQuests!, LegacySeedOneQuestIds);
            AssertQuestPolicyResult(native, initialQuests!, NativeSeedOneQuestIds);
        }
        else
        {
            AssertQuestDisabledActivation(legacy);
            AssertQuestDisabledActivation(native);
        }
        Assert.Equal(initialTables, SnapshotComposedTables(content));
        return new PolicyPair(content, legacyModule, nativeModule, legacy, native);
    }

    private static void AssertQuestPolicyResult(
        DiabloContentActivation activation,
        IReadOnlyDictionary<uint, AuthoritativeQuestState> initialQuests,
        IReadOnlyCollection<uint> expectedSelectedQuestIds)
    {
        var pools = Assert.IsType<AuthoritativeQuestPoolCatalog>(activation.QuestPools);
        var poolQuestIds = pools.Definitions.Select(entry => entry.QuestId).ToHashSet();
        Assert.DoesNotContain(3006U, poolQuestIds);
        Assert.DoesNotContain(3016U, poolQuestIds);
        Assert.All(expectedSelectedQuestIds, questId => Assert.Contains(questId, poolQuestIds));

        var actualQuests = activation.Simulation.WorldState.Quests.Values.ToArray();
        var actualById = actualQuests.ToDictionary(quest => quest.QuestId);
        Assert.Equal(initialQuests.Keys.Order(), actualById.Keys.Order());
        var expectedUnavailable = expectedSelectedQuestIds
            .Where(questId => initialQuests.TryGetValue(questId, out var initial) && !initial.Completed)
            .Order()
            .ToArray();
        Assert.Equal(
            expectedUnavailable,
            actualQuests.Where(quest => quest.Unavailable)
                .Select(quest => quest.QuestId)
                .Order()
                .ToArray());

        foreach (var quest in actualQuests)
        {
            var initial = initialQuests[quest.QuestId];
            var selectedAndIncomplete = !initial.Completed && expectedSelectedQuestIds.Contains(quest.QuestId);
            Assert.Equal(initial.Completed, quest.Completed);
            Assert.Equal(initial.Progress, quest.Progress);
            Assert.Equal(initial.RequiredProgress, quest.RequiredProgress);
            Assert.Equal(initial.LevelId, quest.LevelId);
            Assert.Equal(initial.Variable1, quest.Variable1);
            Assert.Equal(initial.Variable2, quest.Variable2);
            Assert.Equal(initial.MessageId, quest.MessageId);
            Assert.Equal(selectedAndIncomplete, quest.Unavailable);
            Assert.Equal(selectedAndIncomplete ? false : initial.Active, quest.Active);
            Assert.Equal(selectedAndIncomplete ? false : initial.Logged, quest.Logged);
        }

        foreach (var nonPoolQuestId in new uint[] { 3006, 3016 })
        {
            var initial = initialQuests[nonPoolQuestId];
            var actual = actualById[nonPoolQuestId];
            Assert.False(actual.Unavailable);
            Assert.Equal(initial.Completed, actual.Completed);
            Assert.Equal(initial.Active, actual.Active);
            Assert.Equal(initial.Logged, actual.Logged);
        }
    }

    private static void AssertNativeGeneratedProfileWitness(PolicyPair pair, uint seed)
    {
        var legacy = Assert.IsType<DiabloSimulation>(pair.Legacy.Simulation);
        var native = Assert.IsType<DiabloSimulation>(pair.Native.Simulation);
        var legacyIdentity = Assert.IsType<DiabloNativeGeneratedWorldIdentity>(legacy.NativeGeneratedWorldIdentity);
        var nativeIdentity = Assert.IsType<DiabloNativeGeneratedWorldIdentity>(native.NativeGeneratedWorldIdentity);
        Assert.Null(legacy.CathedralDepthWorldIdentity);
        Assert.Null(native.CathedralDepthWorldIdentity);
        Assert.False(pair.Legacy.Simulation.WorldState.HasCathedralDoors);
        Assert.False(pair.Native.Simulation.WorldState.HasCathedralDoors);
        Assert.Equal(legacyIdentity.Levels, nativeIdentity.Levels);
        Assert.Equal(legacyIdentity.RouteFingerprint, nativeIdentity.RouteFingerprint);
        var generatedLevel = Assert.Single(nativeIdentity.Levels);
        Assert.Equal(1U, generatedLevel.LevelId);
        Assert.Equal("cathedral", generatedLevel.LevelSymbol);
        Assert.Equal(seed, generatedLevel.DungeonSeed);
        Assert.Equal(AuthoritativeCathedralEntry.Main, generatedLevel.Entry);
        Assert.Matches("^[A-F0-9]{64}$", nativeIdentity.RouteFingerprint);
        var worldLevel = pair.Native.Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1);
        Assert.Equal((112, 112), (worldLevel.Width, worldLevel.Height));
    }

    private static void AssertCathedralDepthProfileWitness(PolicyPair pair)
    {
        var legacy = pair.Legacy.Simulation;
        var native = pair.Native.Simulation;
        Assert.Null(legacy.NativeGeneratedWorldIdentity);
        Assert.Null(native.NativeGeneratedWorldIdentity);
        var legacyIdentity = Assert.IsType<DiabloCathedralDepthWorldIdentity>(legacy.CathedralDepthWorldIdentity);
        var nativeIdentity = Assert.IsType<DiabloCathedralDepthWorldIdentity>(native.CathedralDepthWorldIdentity);
        Assert.False(pair.Legacy.Simulation.WorldState.HasCathedralDoors);
        Assert.False(pair.Native.Simulation.WorldState.HasCathedralDoors);
        Assert.Equal(legacyIdentity.Levels, nativeIdentity.Levels);
        Assert.Equal(legacyIdentity.RouteFingerprint, nativeIdentity.RouteFingerprint);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, nativeIdentity.Levels.Select(level => level.LevelId));
        Assert.Equal(CathedralDepthSeeds, nativeIdentity.Levels.Select(level => level.DungeonSeed));
        Assert.All(nativeIdentity.Levels, level => Assert.Equal(
            AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
            level.Policy));
        Assert.Matches("^[A-F0-9]{64}$", nativeIdentity.RouteFingerprint);
    }

    private static void AssertCathedralDoorProfileWitness(PolicyPair pair)
    {
        var legacy = pair.Legacy.Simulation;
        var native = pair.Native.Simulation;
        Assert.Null(legacy.NativeGeneratedWorldIdentity);
        Assert.Null(native.NativeGeneratedWorldIdentity);
        Assert.IsType<DiabloCathedralDepthWorldIdentity>(legacy.CathedralDepthWorldIdentity);
        Assert.IsType<DiabloCathedralDepthWorldIdentity>(native.CathedralDepthWorldIdentity);
        Assert.True(pair.Legacy.Simulation.WorldState.HasCathedralDoors);
        Assert.True(pair.Native.Simulation.WorldState.HasCathedralDoors);
        var legacyDoors = pair.Legacy.Simulation.WorldState.CaptureCathedralDoorObjects();
        var nativeDoors = pair.Native.Simulation.WorldState.CaptureCathedralDoorObjects();
        Assert.NotEmpty(legacyDoors);
        Assert.Equal(legacyDoors, nativeDoors);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, nativeDoors.Select(door => door.LevelId).Distinct().Order());
        Assert.All(nativeDoors, door => Assert.Equal(0, door.DoorState));
    }

    private static IReadOnlyList<AuthoritativeQuestState> LoadUnselectedQuestStates(ComposedContent content)
    {
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            content.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            content.GetRequiredTable("set_level_names.tsv").ToCanonicalTsv(),
            stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            content.GetRequiredTable("quest_types.tsv").ToCanonicalTsv(),
            setLevels,
            stableIds);
        var definitions = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            content.GetRequiredTable("quests.tsv").ToCanonicalTsv(),
            questTypes,
            stableIds);
        var states = AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            content.GetRequiredTable("quest_states.tsv").ToCanonicalTsv(),
            questTypes,
            stableIds);
        return AuthoritativeQuestCatalog.Merge(definitions, states, "quest_states.tsv");
    }

    private static void AssertQuestDisabledActivation(DiabloContentActivation activation)
    {
        Assert.Null(activation.QuestPools);
        Assert.Empty(activation.Simulation.WorldState.Quests.Values);
    }

    private static void AssertQuestDisabledCompilerTables(ContentPack basePack, ContentPack profilePack)
    {
        var expectedQuestTables = new[]
        {
            "item_quest_effects.tsv",
            "quest_dialogue_stages.tsv",
            "quest_pools.tsv",
            "quest_sequence_variants.tsv",
            "quest_states.tsv",
            "quest_towner_interactions.tsv",
            "quest_types.tsv",
            "quests.tsv",
        };
        var baseQuestTables = basePack.Tables
            .Where(table => expectedQuestTables.Contains(table.SourcePath, StringComparer.Ordinal))
            .Select(table => table.SourcePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedQuestTables.OrderBy(path => path, StringComparer.Ordinal), baseQuestTables);

        var compiledQuestTables = profilePack.Tables
            .Where(table => expectedQuestTables.Contains(table.SourcePath, StringComparer.Ordinal))
            .Select(table => table.SourcePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "quests.tsv" }, compiledQuestTables);

        var baseDefinitions = basePack.Tables.Single(table => table.SourcePath == "quests.tsv");
        var compiledDefinitions = profilePack.Tables.Single(table => table.SourcePath == "quests.tsv");
        Assert.Equal(baseDefinitions.Columns, compiledDefinitions.Columns);
        Assert.Empty(compiledDefinitions.Rows);
    }
    private static void AssertQuestTablesMatchBase(ContentPack basePack, ContentPack profilePack)
    {
        foreach (var sourcePath in new[] {
                     "quests.tsv", "quest_pools.tsv", "quest_states.tsv", "quest_types.tsv",
                     "quest_sequence_variants.tsv",
                 })
        {
            var baseTable = basePack.Tables.Single(table => table.SourcePath == sourcePath);
            var profileTable = profilePack.Tables.Single(table => table.SourcePath == sourcePath);
            Assert.Equal(baseTable.ToCanonicalTsv(), profileTable.ToCanonicalTsv());
        }
    }

    private static string CreateLevelOnePieceDefinitions(AuthoritativeCathedralLevel1Result generated)
    {
        var unique = generated.MegaTiles
            .GroupBy(tile => tile)
            .Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId
                && group.Count() == 1)
            .Select(group => (uint)group.Key - 1)
            .Take(2)
            .ToArray();
        Assert.Equal(2, unique.Length);
        var blockedMega = generated.MegaTiles
            .GroupBy(tile => tile)
            .Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId
                && !unique.Contains((uint)group.Key - 1))
            .Select(group => (uint)group.Key - 1)
            .First();
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            var northWest = (uint)id == unique[0]
                ? 128
                : (uint)id == unique[1]
                    ? 114
                    : (uint)id == blockedMega ? 1 : 2;
            lines.Add($"{id}\t{northWest}\t2\t2\t2");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string CreateDepthPieceDefinitions()
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            if (id == 21)
            {
                lines.Add($"{id}\t1\t1\t1\t1");
                continue;
            }
            var northWest = id == 63 ? 128 : id == 58 ? 114 : 2;
            lines.Add($"{id}\t{northWest}\t2\t2\t2");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string CreateDoorPieceDefinitions()
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            var pieces = id switch {
                25 or 26 => "42\t45\t43\t2",
                63 => "128\t2\t2\t2",
                58 => "114\t2\t2\t2",
                21 => "1\t2\t2\t2",
                129 => "42\t45\t43\t269",
                _ => "2\t2\t2\t2",
            };
            lines.Add($"{id}\t{pieces}");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string CreateTileProperties()
    {
        var lines = new List<string> {
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
            lines.Add($"{id}\t{id == 1}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse");
        return string.Join('\n', lines) + "\n";
    }

    private static string[] SnapshotComposedTables(ComposedContent content)
        => content.Tables.Select(table => $"{table.SourcePath}\n{table.ToCanonicalTsv()}").ToArray();

    private static string[] SnapshotTables(ContentPack pack)
        => pack.Tables.Select(table => $"{table.SourcePath}\n{table.ToCanonicalTsv()}").ToArray();

    private static string NewOutput(string profile)
        => Path.Combine(Path.GetTempPath(), $"godevilution-bootstrap-quest-policy-{profile}-{Guid.NewGuid():N}");

    private static void CleanupSuccessfulOutput(string path, bool completed)
    {
        if (completed && Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "server", "content", "base"));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots)
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static void AssertModuleIdentity(DiabloModule module, string expectedVersion)
    {
        Assert.Equal("devilution.diablo", module.Identity.Id);
        Assert.Equal(expectedVersion, module.Identity.Version);
        Assert.Equal(
            GameplayModuleIdentity.BuiltInAssemblyHash("devilution.diablo", expectedVersion),
            module.Identity.AssemblySha256);
        Assert.Empty(module.Identity.Dependencies);
        Assert.Empty(module.Identity.DependencyConstraints);
        Assert.NotNull(module.Identity.Compatibility);
        Assert.Equal(GameplayEngineApiVersion.Current, module.Identity.Compatibility.Minimum);
        Assert.Equal(GameplayEngineApiVersion.Current, module.Identity.Compatibility.Maximum);
    }

    private sealed record PolicyPair(
        ComposedContent Content,
        DiabloModule LegacyModule,
        DiabloModule NativeModule,
        DiabloContentActivation Legacy,
        DiabloContentActivation Native);
}