using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeDunGeometryContentActivationBoundaryTests
{
    [Fact]
    public void CurrentSchemaAcceptsCompleteSharedGeometrySchemas()
    {
        var basePack = LoadBasePack();
        var pack = AddGeometryTables(basePack);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "current-native-dun-geometry-contract",
                "1",
                [pack])));

        Assert.NotNull(activation);
    }

    [Theory]
    [InlineData("native_dun_piece_definitions.tsv", "Production native-DUN piece table")]
    [InlineData("native_dun_tile_properties.tsv", "Production native-DUN tile-properties table")]
    public void CurrentSchemaRejectsLegacyOrderedSharedGeometrySchemas(
        string sourcePath,
        string expectedMessage)
    {
        var basePack = LoadBasePack();
        var pack = AddGeometryTables(
            basePack,
            sourcePath,
            ReverseColumns(CreateTable(sourcePath)));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-dun-geometry-schema",
                    "1",
                    [pack]))));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyOrderedSharedGeometryCompatibility()
    {
        var basePack = LoadBasePack();
        var pack = AddGeometryTables(
            basePack,
            "native_dun_piece_definitions.tsv",
            ReverseColumns(CreateTable("native_dun_piece_definitions.tsv")),
            new ContentSchemaIdentity("devilution.diablo", "1.500.0"));
        pack = ReplaceTable(
            pack,
            "native_dun_tile_properties.tsv",
            ReverseColumns(CreateTable("native_dun_tile_properties.tsv")));

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-dun-geometry-contract",
                "1",
                [pack])));

        Assert.NotNull(activation);
    }

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
    }

    private static ContentPack AddGeometryTables(
        ContentPack basePack,
        string? replacedSourcePath = null,
        TsvTable? replacement = null,
        ContentSchemaIdentity? schema = null)
    {
        var geometryTables = new[] {
            CreateTable("native_dun_piece_definitions.tsv"),
            CreateTable("native_dun_tile_properties.tsv"),
        };
        if (replacement is not null) {
            geometryTables = geometryTables
                .Select(table => table.SourcePath == replacedSourcePath
                    ? replacement
                    : table)
                .ToArray();
        }
        var tables = basePack.Tables
            .Where(table => table.SourcePath is not
                ("native_dun_piece_definitions.tsv" or "native_dun_tile_properties.tsv"))
            .Concat(geometryTables)
            .ToArray();
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            basePack.Dependencies,
            basePack.Overlays,
            schema ?? basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
    }

    private static ContentPack ReplaceTable(
        ContentPack pack,
        string sourcePath,
        TsvTable replacement)
    {
        return new ContentPack(
            pack.Id,
            pack.Version,
            pack.Tables
                .Where(table => table.SourcePath != sourcePath)
                .Concat([replacement])
                .ToArray(),
            pack.Dependencies,
            pack.Overlays,
            pack.Schema,
            pack.DependencyConstraints,
            pack.Assets);
    }

    private static TsvTable CreateTable(string sourcePath)
        => sourcePath switch {
            "native_dun_piece_definitions.tsv" => TsvTable.Parse(
                sourcePath,
                "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                    + "0\t1\t2\t3\t4\n"),
            "native_dun_tile_properties.tsv" => TsvTable.Parse(
                sourcePath,
                "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
                    + "1\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(sourcePath)),
        };

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
