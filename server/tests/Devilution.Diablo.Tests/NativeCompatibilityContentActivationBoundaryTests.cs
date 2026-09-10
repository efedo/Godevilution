using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeCompatibilityContentActivationBoundaryTests
{
    [Theory]
    [InlineData("native_spells.tsv", "Production native-spell compatibility table")]
    [InlineData("native_item_misc.tsv", "Production native-misc compatibility table")]
    [InlineData("native_item_topology.tsv", "Production native-item-topology compatibility table")]
    [InlineData("native_item_tags.tsv", "Production native-item-tag compatibility table")]
    public void CurrentSchemaRejectsLegacyOrderedCompatibilitySchemas(
        string sourcePath,
        string expectedMessage)
    {
        var basePack = LoadBasePack();
        var table = basePack.Tables.Single(candidate => candidate.SourcePath == sourcePath);
        var pack = ReplaceBaseTable(basePack, sourcePath, ReverseColumns(table));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-compatibility-schema",
                    "1",
                    [pack]))));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("native_spells.tsv")]
    [InlineData("native_item_misc.tsv")]
    [InlineData("native_item_topology.tsv")]
    [InlineData("native_item_tags.tsv")]
    public void CurrentSchemaRequiresEveryLegacyCompatibilityNamespace(string sourcePath)
    {
        var basePack = LoadBasePack();
        var pack = RemoveBaseTable(basePack, sourcePath);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "missing-native-compatibility-namespace",
                    "1",
                    [pack]))));

        Assert.Contains($"requires '{sourcePath}' compatibility content", exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentSchemaRetainsValidatedLegacyCompatibilityCatalogs()
    {
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "retained-native-compatibility-catalogs",
                "1",
                [LoadBasePack()])));

        Assert.NotNull(activation.NativeSpells);
        Assert.Equal(52, activation.NativeSpells!.Definitions.Count);
        Assert.NotNull(activation.NativeItemMisc);
        Assert.NotNull(activation.NativeItemTopology);
        Assert.NotNull(activation.NativeItemTags);
        Assert.True(activation.NativeItemTopology!.TryGetId(
            AuthoritativeNativeItemTopologyKind.ItemType,
            "weapon",
            out _));
        Assert.True(activation.NativeItemTags!.TryGetTags("sword", out var tags));
        Assert.Contains("Weapon", tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyOrderedCompatibilityLoading()
    {
        var basePack = LoadBasePack();
        var tables = basePack.Tables
            .Select(table => table.SourcePath is
                "native_spells.tsv"
                or "native_item_misc.tsv"
                or "native_item_topology.tsv"
                or "native_item_tags.tsv"
                    ? ReverseColumns(table)
                    : table)
            .ToArray();
        var previous = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            basePack.Dependencies,
            basePack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.501.0"),
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-compatibility-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
        Assert.Null(activation.NativeSpells);
        Assert.Null(activation.NativeItemMisc);
        Assert.Null(activation.NativeItemTopology);
        Assert.Null(activation.NativeItemTags);
    }

    [Fact]
    public void PreviousSchemaRetainsCompatibilityNamespaceOmission()
    {
        var basePack = LoadBasePack();
        var tables = basePack.Tables
            .Where(table => table.SourcePath is not
                ("native_spells.tsv"
                or "native_item_misc.tsv"
                or "native_item_topology.tsv"
                or "native_item_tags.tsv"))
            .ToArray();
        var previous = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            basePack.Dependencies,
            basePack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.501.0"),
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-compatibility-omission",
                "1",
                [previous])));

        Assert.NotNull(activation);
    }

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
    }

    private static ContentPack ReplaceBaseTable(
        ContentPack basePack,
        string sourcePath,
        TsvTable replacement)
    {
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => table.SourcePath != sourcePath)
                .Concat([replacement])
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
    }

    private static TsvTable ReverseColumns(TsvTable table)
    {
        var columns = table.Columns.Reverse().ToArray();
        var lines = new List<string> { string.Join('\t', columns) };
        lines.AddRange(table.Rows.Select(row =>
            string.Join('\t', columns.Select(column => row[column]))));
        return TsvTable.Parse(table.SourcePath, string.Join('\n', lines) + "\n");
    }

    private static ContentPack RemoveBaseTable(ContentPack basePack, string sourcePath)
    {
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Where(table => table.SourcePath != sourcePath).ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
    }

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
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
