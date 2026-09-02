using System.Text.Json;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class ProjectDocumentationConsistencyTests
{
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

        var changelog = Read(root, "docs", "CHANGELOG.md");
        Assert.Contains($"schema to `{currentVersion}`", changelog, StringComparison.Ordinal);
        Assert.Contains("documentation/content identity regression guard", changelog, StringComparison.Ordinal);

        var contentReadme = Read(root, "server", "content", "README.md");
        Assert.Contains(currentSchema, contentReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("DevilutionY", contentReadme, StringComparison.OrdinalIgnoreCase);

        foreach (var packName in new[] { "base", "hellfire" }) {
            using var manifest = JsonDocument.Parse(Read(root, "server", "content", packName, "content-pack.json"));
            var schema = manifest.RootElement.GetProperty("schema");
            Assert.Equal(DiabloContentSchema.Current.Id, schema.GetProperty("id").GetString());
            Assert.Equal(currentVersion, schema.GetProperty("version").GetString());
        }
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
