using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class GeneratedOriginContentActivationBoundaryTests
{
    [Theory]
    [InlineData("procedural_set_piece_origins.tsv", "Production procedural set-piece origin table")]
    [InlineData("procedural_quest_piece_origins.tsv", "Production procedural quest-piece origin table")]
    [InlineData("native_dun_objects.tsv", "Production native-DUN object table")]
    public void CurrentSchemaRejectsLegacyOrderedGeneratedOriginSchemas(
        string sourcePath,
        string expectedMessage)
    {
        var basePack = LoadBasePack();
        var tables = CreateOptionalTables()
            .Select(table => table.SourcePath == sourcePath
                ? ReverseColumns(table)
                : table)
            .ToArray();
        var pack = AddTables(basePack, tables);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-generated-origin-schema",
                    "1",
                    [pack]))));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyOrderedGeneratedOriginCompatibility()
    {
        var basePack = LoadBasePack();
        var tables = CreateOptionalTables().Select(ReverseColumns).ToArray();
        var previous = AddTables(
            basePack,
            tables,
            new ContentSchemaIdentity("devilution.diablo", "1.499.0"));

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-generated-origin-contract",
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

    private static IReadOnlyList<TsvTable> CreateOptionalTables()
        => [
            TsvTable.Parse(
                "procedural_set_piece_origins.tsv",
                "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tfootprint_width\tfootprint_height\tselection_seed\talignment\n"
                    + "1\tSL_SKELKING\t1\tcathedral\t2\t2\t1234\t2\n"),
            TsvTable.Parse(
                "procedural_quest_piece_origins.tsv",
                "origin_id\torigin_symbol\tlevel_id\tlevel_symbol\tfootprint_width\tfootprint_height\tselection_seed\talignment\n"
                    + "2201\t\t1\tcathedral\t2\t2\t1234\t2\n"),
            TsvTable.Parse(
                "native_dun_objects.tsv",
                "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tactive_map\tnative_object_id\tobject_id\tobject_symbol\ttemplate_entity_id\n"
                    + "\t\t1\tcathedral\t\t1\t1\tl1_left_door\t0\n"),
        ];

    private static ContentPack AddTables(
        ContentPack basePack,
        IEnumerable<TsvTable> additions,
        ContentSchemaIdentity? schema = null)
    {
        var addedPaths = additions
            .Select(table => table.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => !addedPaths.Contains(table.SourcePath))
                .Concat(additions)
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            schema ?? basePack.Schema,
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
