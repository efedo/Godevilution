using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
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