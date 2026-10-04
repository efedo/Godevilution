using System.Text.Json;
using System.Text.RegularExpressions;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class ProjectDocumentationConsistencyTests
{
    [Fact]
    public void CompletePopulationMetadataDocumentationDoesNotClaimRuntimeActivation()
    {
        var root = FindRepositoryRoot();
        var architecture = Read(root, "docs", "csharp-authoritative-server-migration.md");
        Assert.Contains(AuthoritativeCathedralPopulationMetadata.SchemaId, architecture, StringComparison.Ordinal);
        Assert.Contains("version 1.0.0 requires nineteen", architecture, StringComparison.Ordinal);
        Assert.Contains("does not silently activate a runtime profile", architecture, StringComparison.Ordinal);
        var plan = Read(root, "docs", "completion-plan.md");
        Assert.Contains("metadata compilation alone is not playable profile acceptance", plan, StringComparison.Ordinal);
        Assert.Contains("Next update the raw extension", plan, StringComparison.Ordinal);
        var guidance = Read(root, "docs", "ai", "msvc-build.md");
        Assert.Contains("test-cathedral-population-metadata.ps1", guidance, StringComparison.Ordinal);
        Assert.Contains("-FullPopulationEvidenceDirectory", guidance, StringComparison.Ordinal);
        Assert.Contains("--compile-population-metadata", guidance, StringComparison.Ordinal);
        Assert.Contains("expected intermediate/final state is comparison-only", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshPopulationDocumentationRetainsColdOnlyAndRuntimeAcceptanceGates()
    {
        var root = FindRepositoryRoot();
        var architecture = Read(root, "docs", "csharp-authoritative-server-migration.md");
        Assert.Contains(nameof(AuthoritativeCathedralFreshLevelGenerator), architecture, StringComparison.Ordinal);
        Assert.Contains("cold-zero storage, not retained level state", architecture, StringComparison.Ordinal);
        Assert.Contains("It ends at `CreateThemeRooms`", architecture, StringComparison.Ordinal);
        Assert.Contains("before admission, preserving existing cohort", architecture, StringComparison.Ordinal);
        var plan = Read(root, "docs", "completion-plan.md");
        Assert.Contains("not retained-state campaign loading", plan, StringComparison.Ordinal);
        Assert.Contains("not persistence", plan, StringComparison.Ordinal);
        Assert.Contains("raw state and projections atomically", plan, StringComparison.Ordinal);
        var guidance = Read(root, "docs", "ai", "msvc-build.md");
        Assert.Contains("test-cathedral-fresh-population.ps1", guidance, StringComparison.Ordinal);
        Assert.Contains("-MetadataPackDirectory", guidance, StringComparison.Ordinal);
        Assert.Contains("requires byte-identical captures", guidance, StringComparison.Ordinal);
        Assert.Contains("Godot interaction or cold-restore acceptance", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeMonsterImportDocumentationPreservesLiveMutationAndLegacyGates()
    {
        var root = FindRepositoryRoot();
        var architecture = Read(root, "docs", "csharp-authoritative-server-migration.md");
        Assert.Contains(AuthoritativeCathedralMonsterRuntimeCatalog.SchemaId, architecture, StringComparison.Ordinal);
        Assert.Contains("returned targets are not installed or gameplay-ready", architecture, StringComparison.Ordinal);
        Assert.Contains("Immutable initial state", architecture, StringComparison.Ordinal);
        Assert.Contains("is not a live-updated lossless raw checkpoint", architecture, StringComparison.Ordinal);
        var plan = Read(root, "docs", "completion-plan.md");
        Assert.Contains("native death-time loot", plan, StringComparison.Ordinal);
        Assert.Contains("legacy v1/v2 schemas/default bytes remain frozen", plan, StringComparison.Ordinal);
        var protocol = Read(root, "protocol", "README.md");
        Assert.Contains("native_initialization_present", protocol, StringComparison.Ordinal);
        Assert.Contains("No default profile emits it", protocol, StringComparison.Ordinal);
        Assert.Contains("test-cathedral-monster-runtime.ps1", Read(root, "docs", "ai", "msvc-build.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveProjectDocumentationUsesCurrentIdentityAndSchema()
    {
        var root = FindRepositoryRoot();
        var currentSchema = DiabloContentSchema.Current.ToString();
        var currentVersion = DiabloContentSchema.Current.Version.ToString();

        var readme = Read(root, "README.md");
        Assert.Contains("# Godevilution", readme, StringComparison.Ordinal);
        Assert.Contains("DevilutionX", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("DevilutionY", readme, StringComparison.OrdinalIgnoreCase);

        var migrationStatus = Read(root, "docs", "migration-status.md");
        Assert.Contains($"active content schema is now `{currentVersion}`", migrationStatus, StringComparison.Ordinal);
        Assert.Contains("ordinary_monster_capacity", migrationStatus, StringComparison.Ordinal);
        Assert.Contains("default 190-slot", migrationStatus, StringComparison.Ordinal);
        Assert.Contains("coverage_mode=Complete", migrationStatus, StringComparison.Ordinal);
        // Delivery order belongs to the active plan, not a duplicated status backlog.
        Assert.Contains("[completion plan](completion-plan.md)", migrationStatus, StringComparison.Ordinal);
        var completionPlan = Read(root, "docs", "completion-plan.md");
        Assert.Contains("## M2: Native-compatible world generation and placement", completionPlan, StringComparison.Ordinal);
        Assert.Contains("## M3: Gameplay and content closure by playable route", completionPlan, StringComparison.Ordinal);
        Assert.Contains("Hellfire world and quests", completionPlan, StringComparison.Ordinal);
        Assert.Contains(
            "version-4 canonical SHA-256 state and command-ledger integrity envelope bound to the active ruleset identity",
            migrationStatus,
            StringComparison.Ordinal);
        Assert.Contains(
            "all-session candidate export preparation before ruleset-reload checkpoint publication, transactional rollback of earlier combined and compatibility files",
            migrationStatus,
            StringComparison.Ordinal);
        Assert.Contains(
            "transactional rollback of earlier combined and compatibility files",
            migrationStatus,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "an atomic combined per-session checkpoint with a version-3 canonical",
            migrationStatus,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Expand authoritative combat and spell fidelity for the migrated Firebolt", migrationStatus, StringComparison.Ordinal);

        var changelog = Read(root, "docs", "CHANGELOG.md");
        Assert.Contains($"schema to `{currentVersion}`", changelog, StringComparison.Ordinal);
        Assert.Contains("documentation/content identity regression guard", changelog, StringComparison.Ordinal);
        Assert.Contains("distributable ZIP archive", changelog, StringComparison.Ordinal);
        Assert.Contains("blocked-monster-melee Reflect behavior", changelog, StringComparison.Ordinal);

        var replayFixtures = Read(root, "docs", "replay-fixtures.md");
        Assert.Contains("gameplay-reflect-blocked-monster-melee.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-singleplayer-blocked-monster-melee.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-singleplayer-monster-melee-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-singleplayer-monster-melee-q6-range.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-rhino-charge-native-q6-range.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-goat-melee-secondary-q6-range.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-goat-ranged-secondary-q6-range.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-goat-ranged-secondary-get-hit.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-etherealized.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-goat-ranged-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-skeleton-ranged-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-goat-ranged-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-skeleton-ranged-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-fire-bat-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-fire-bat-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-torchant-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-torchant-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-storm-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-hork-spawn-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-acid-primary-autonomous.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-acid-unique-primary-autonomous.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-arrow-flash-protected.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-arch-lich-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-psychorb-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-necromorb-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-bone-demon-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-arch-lich-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-psychorb-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-necromorb-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-monster-magic-bone-demon-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-multiplayer-secondary-monster-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-bone-spirit-hellfire-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-firebolt-hellfire-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-firebolt-hellfire-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-fireball-hellfire-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-fireball-hellfire-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-charged-bolt-hellfire-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-charged-bolt-hellfire-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-bone-spirit-hellfire-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-lightning-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-lightning-wall-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-flame-wave-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-chain-lightning-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-immolation-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-elemental-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-guardian-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-flash-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-fire-wall-secondary-cast-primary-player.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-golem-secondary-summon.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-hork-spawn-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-blood-star-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-blood-star-lazarus-succubus-primary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);
        Assert.Contains("gameplay-magma-secondary-warrior-block-chance.json", replayFixtures, StringComparison.Ordinal);

        var packageWorkflow = Read(root, ".github", "workflows", "diablo-adapter-package.yml");
        Assert.Contains("package-diablo-adapter.ps1", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("verify-diablo-adapter-archive.ps1", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("godevilution.diablo.package", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("godevilution-v*", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("GITHUB_REF_NAME -replace '^godevilution-v', ''", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("only the verified ZIP", migrationStatus, StringComparison.Ordinal);

        var contentReadme = Read(root, "server", "content", "README.md");
        Assert.Contains(currentSchema, contentReadme, StringComparison.Ordinal);
        Assert.Contains("Explicit `monster_id` selectors are cross-checked", contentReadme, StringComparison.Ordinal);
        Assert.Contains("`Complete` requires a role for every active monster type", contentReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("DevilutionY", contentReadme, StringComparison.OrdinalIgnoreCase);

        foreach (var packName in new[] { "base", "hellfire" }) {
            using var manifest = JsonDocument.Parse(Read(root, "server", "content", packName, "content-pack.json"));
            var schema = manifest.RootElement.GetProperty("schema");
            Assert.Equal(DiabloContentSchema.Current.Id, schema.GetProperty("id").GetString());
            Assert.Equal(currentVersion, schema.GetProperty("version").GetString());
        }
    }

    [Fact]
    public void CathedralDepthContractAndPlaytestDocumentationAgree()
    {
        var root = FindRepositoryRoot();
        var contractText = Read(root, "docs", "csharp-authoritative-server-migration.md");
        Assert.Single(Regex.Matches(contractText, "(?m)^- The separately selected `devilution\\.diablo\\.cathedral-depths\\.v1`"));
        Assert.Contains(AuthoritativeCathedralDepthRuntimeCatalog.TableName, contractText, StringComparison.Ordinal);
        Assert.Contains(AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId, contractText, StringComparison.Ordinal);
        Assert.Contains("level_id,level_symbol,depth,generator_id,generator_revision,dungeon_seed,entry,policy", contractText, StringComparison.Ordinal);
        Assert.Contains("quest-disabled-original-diablo", contractText, StringComparison.Ordinal);
        Assert.Contains("Strict version-3 saves", contractText, StringComparison.Ordinal);
        Assert.Contains("L4.Down has no Catacombs-5 route", contractText, StringComparison.Ordinal);
        Assert.Contains("ai/godot-build.md#generated-cathedral-depths-playtest", contractText, StringComparison.Ordinal);
        var guide = Read(root, "docs", "ai", "godot-build.md");
        Assert.Contains("run-godot-cathedral-depths-playtest.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("-CathedralDepthProfileDirectory", guide, StringComparison.Ordinal);
        Assert.Contains("devilution.diablo.cathedral-depths.v1", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayFixtureInventoryMatchesNativeGateDocumentation()
    {
        var root = FindRepositoryRoot();
        var replayRoot = Path.Combine(root, "test", "fixtures", "replay");
        var replaySource = Read(root, "test", "replay_test.cpp");
        var replayDocumentation = Read(root, "docs", "replay-fixtures.md");
        Assert.Contains(
            "gameplay-apocalypse-secondary-cast-primary-player.json",
            replayDocumentation,
            StringComparison.Ordinal);

        var fixtureCount = Directory.EnumerateFiles(
                replayRoot,
                "*.json",
                SearchOption.AllDirectories)
            .Count();
        var referencedFixtures = Regex.Matches(
                replaySource,
                @"test/fixtures/replay/[A-Za-z0-9._/-]+\.json",
                RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var nativeTestCount = Regex.Matches(
                replaySource,
                "(?m)^TEST\\(",
                RegexOptions.CultureInvariant)
            .Count;
        var status = Regex.Match(
            replayDocumentation,
            @"native replay gate contains (?<fixtures>\d+) checked-in JSON fixtures and (?<tests>\d+) registered\s+tests",
            RegexOptions.CultureInvariant);

        Assert.True(status.Success, "Replay-fixture documentation must publish the native gate inventory.");
        Assert.Equal(fixtureCount, referencedFixtures);
        Assert.Equal(fixtureCount, int.Parse(status.Groups["fixtures"].Value));
        Assert.Equal(nativeTestCount, int.Parse(status.Groups["tests"].Value));
    }

    [Fact]
    public void CathedralInitialPopulationDocumentationPreservesItsPhaseBoundaries()
    {
        var root = FindRepositoryRoot();
        var contract = Read(root, "docs", "csharp-authoritative-server-migration.md");
        var guide = Read(root, "docs", "ai", "msvc-build.md");
        var plan = Read(root, "docs", "completion-plan.md");
        Assert.Contains("`InitThemes` and `HoldThemeRooms`", contract, StringComparison.Ordinal);
        Assert.Contains("persistent native theme", contract, StringComparison.Ordinal);
        Assert.Contains("`PlacementVariant`", contract, StringComparison.Ordinal);
        Assert.Contains("detached theme-room proof, not composed full-level population", contract, StringComparison.Ordinal);
        Assert.Contains("comparison evidence, not composed-generator inputs", contract, StringComparison.Ordinal);
        Assert.Contains("version-2 pre-theme input sidecar", guide, StringComparison.Ordinal);
        Assert.Contains("wrapper requires", guide, StringComparison.Ordinal);
        Assert.Contains("version 2 and cannot report upstream parity from old evidence", guide, StringComparison.Ordinal);
        Assert.Contains("`-CathedralEncounters`", plan, StringComparison.Ordinal);
        Assert.Contains("A separate detached `CreateThemeRooms` port", plan, StringComparison.Ordinal);
        Assert.Contains("not composed full-level population or a runtime pack", plan, StringComparison.Ordinal);
        Assert.Contains("tools/test-cathedral-full-population.ps1", plan, StringComparison.Ordinal);
        Assert.Contains("./tools/test-cathedral-full-population.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("`PlacementOrigin`", contract, StringComparison.Ordinal);
        Assert.Contains("normal level loading clears monsters before geometry creation", plan, StringComparison.Ordinal);
        Assert.Contains("post-CreateLevel seam", contract, StringComparison.Ordinal);
        Assert.Contains("tools/test-theme-room-generation.ps1", plan, StringComparison.Ordinal);
        Assert.Contains("./tools/test-theme-room-generation.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Unsafe reached native reads/writes", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("Next port `CreateThemeRooms`", plan, StringComparison.Ordinal);
        Assert.Contains("complete nineteen-table population metadata schema", plan, StringComparison.Ordinal);
        Assert.Contains("tools/test-theme-item-placement.ps1", plan, StringComparison.Ordinal);
        Assert.Contains("./tools/test-theme-item-placement.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("unreferenced allocations", plan, StringComparison.Ordinal);
        Assert.Contains("`ItemNoFlippy`", plan, StringComparison.Ordinal);
        Assert.Contains("`-CathedralInitialItems`", guide, StringComparison.Ordinal);
        Assert.Contains(AuthoritativeCathedralUniqueMonsterCatalog.TableName, guide, StringComparison.Ordinal);
        Assert.Contains("complete 200-slot monster pool", contract, StringComparison.Ordinal);
        Assert.Contains("`cathedral_population_monsters.tsv`", guide, StringComparison.Ordinal);
        Assert.Contains("`cathedral_population_objects.tsv`", guide, StringComparison.Ordinal);
        Assert.Contains("exactly sixteen explicit", guide, StringComparison.Ordinal);
        Assert.Contains("recorded pre-clear", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Next port upstream bestiary/theme/reservation", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalDocumentationExplainsWhyInheritedPoliciesAreNotActive()
    {
        var root = FindRepositoryRoot();
        var historyReadme = Read(root, "docs", "history", "README.md");

        Assert.Contains("historical", historyReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Inherited DevilutionX policy", historyReadme, StringComparison.Ordinal);
        Assert.Contains("removed from the active tree", historyReadme, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments)
        => File.ReadAllText(Path.Combine([root, .. segments]));

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
