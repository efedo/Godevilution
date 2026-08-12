using Xunit;

namespace Devilution.Server.Tests;

public sealed class CompositionBoundaryTests
{
    [Fact]
    public void ExecutableStartupDoesNotParseDiabloTablesOrConstructCatalogs()
    {
        var programPath = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "src",
            "Devilution.Server",
            "Program.cs");
        var source = File.ReadAllText(programPath);
        var forbidden = new[] {
            "AuthoritativeItemAffixCatalog",
            "AuthoritativeItemCatalog",
            "AuthoritativeItemGenerationRules",
            "AuthoritativeUniqueItemCatalog",
            "StoreCatalog",
            "StableContentIdCatalog",
            "AuthoritativeWorld.LoadTsv",
            "AuthoritativeSpellCatalog",
            "AuthoritativeCombatRules",
            "AuthoritativeMonsterCatalog",
            "AuthoritativePortalCatalog",
            "AuthoritativeWorldObjectCatalog",
            "AuthoritativeQuestCatalog",
            "StoreSimulationExecutor",
            "Diablo",
            ".tsv\"",
        };

        foreach (var token in forbidden)
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        Assert.Contains("BuiltInAuthoritativeGameHostAdapters", source, StringComparison.Ordinal);
        Assert.Contains("AuthoritativePlayerStartConfiguration", source, StringComparison.Ordinal);
        Assert.Contains(
            "GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new ContentRuntime(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("registry.Load(", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
