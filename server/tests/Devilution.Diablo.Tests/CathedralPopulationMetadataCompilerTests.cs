using System.Globalization;
using System.Reflection;
using System.Text;
using Devilution.Data;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class CathedralPopulationMetadataCompilerTests
{
    [Fact]
    public void CompletePackLoadsAllGenerationDependenciesAndDerivesSharedPotions()
    {
        var metadata = Load(CreateCompleteTables());
        Assert.Equal(206, metadata.Geometry.Pieces.Definitions.Count);
        Assert.Equal(1379, metadata.Geometry.TileProperties.Definitions.Count);
        Assert.Equal(138, metadata.Population.Monsters.Count);
        Assert.Equal(109, metadata.Population.Objects.Count);
        Assert.Equal(100, metadata.UniqueMonsters.Definitions.Count);
        Assert.Equal(168, metadata.Items.Catalog.Items.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, metadata.DepthRules.Select(rule => rule.Depth));
        Assert.Equal(new[] { 100, 101, 102 }, metadata.StoryMessages);
        Assert.Equal(26, metadata.ShrineAvailable.Count);
        Assert.Equal(new[] { 24, 25 }, metadata.PotionDefinitions.Select(row => row.NativeItemId));
        foreach (var potion in metadata.PotionDefinitions) {
            var item = metadata.Items.Catalog.Items[potion.NativeItemId];
            Assert.Equal(item.Name, potion.Name);
            Assert.Equal(item.MinimumStrength, potion.MinimumStrength);
            Assert.Equal(item.AnimationFrameCount, potion.AnimationFrameCount);
        }
        Assert.Equal(metadata.Fingerprint, Load(metadata.ToContentPack().Tables).Fingerprint);
        Assert.Equal(metadata.ManifestHash, Load(metadata.ToContentPack().Tables).ManifestHash);
    }

    public static IEnumerable<object[]> RequiredTables => AuthoritativeCathedralPopulationMetadata.TableNames.Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(RequiredTables))]
    public void EveryMetadataFamilyIsRequired(string path)
    {
        Assert.Throws<InvalidDataException>(() => Load(CreateCompleteTables().Where(table => table.SourcePath != path)));
    }

    [Fact]
    public void RejectsUnknownTablesAssetsDependenciesAndOtherSchemas()
    {
        var tables = CreateCompleteTables().ToArray();
        Assert.Throws<InvalidDataException>(() => Load(tables.Append(TsvTable.Parse("expected_world.tsv", "world\n1\n"))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1", tables)));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1", tables,
            schema: new(AuthoritativeCathedralPopulationMetadata.SchemaId, "2.0.0"))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1", tables,
            dependencies: ["base"], schema: Schema())));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1", tables,
            overlays: [new(AuthoritativeCathedralPopulationMetadata.DepthTableName, ["depth"])], schema: Schema())));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1", tables,
            assets: [new("licensed.sol", new byte[] { 0 })], schema: Schema())));
    }

    [Fact]
    public void BundleRejectsCallerRowFieldsBeforeCanonicalNormalizationCanHideThem()
    {
        var tables = CreateCompleteTables().ToArray();
        var index = Array.FindIndex(tables, table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath);
        var original = tables[index];
        var rows = original.Rows.ToArray();
        var values = new Dictionary<string, string>(rows[0].Values) { ["unrecognized"] = "hidden" };
        rows[0] = new(rows[0].LineNumber, values);
        tables[index] = new(original.SourcePath, original.Columns, rows);
        Assert.Throws<InvalidDataException>(() => Load(tables));
        using var root = new OwnedDirectory();
        var output = Path.Combine(root.Path, "refused");
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.Compile(tables, output));
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("til-gap")]
    [InlineData("til-short")]
    [InlineData("sol-short")]
    [InlineData("sol-blank")]
    [InlineData("sol-float")]
    [InlineData("sol-reference")]
    [InlineData("geometry-column")]
    public void RejectsIncompleteOrImplicitGeometry(string mutation)
    {
        var tables = CreateCompleteTables();
        var til = AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath;
        var sol = AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath;
        tables = mutation switch {
            "til-gap" => SetCell(tables, til, 1, "mega_tile_id", "0"),
            "til-short" => DropLastRow(tables, til),
            "sol-short" => DropLastRow(tables, sol),
            "sol-blank" => SetCell(tables, sol, 0, "block_light", ""),
            "sol-float" => SetCell(tables, sol, 0, "transparent", "0.0"),
            "sol-reference" => SetCell(tables, til, 0, "piece_north_west", "1379"),
            "geometry-column" => Replace(tables, til, table => TsvTable.Parse(til,
                table.ToCanonicalTsv().Replace("piece_north_west", "unknown", StringComparison.Ordinal))),
            _ => throw new InvalidOperationException(),
        };
        Assert.Throws<InvalidDataException>(() => Load(tables));
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("density")]
    [InlineData("capacity")]
    [InlineData("trap")]
    [InlineData("depth-order")]
    [InlineData("story")]
    [InlineData("shrine-order")]
    [InlineData("no-shrines")]
    [InlineData("unavailable-potion")]
    public void RejectsUnsupportedOrIncompletePopulationPolicy(string mutation)
    {
        var tables = CreateCompleteTables();
        var depth = AuthoritativeCathedralPopulationMetadata.DepthTableName;
        tables = mutation switch {
            "policy" => SetCell(tables, depth, 0, "policy", "hellfire-multiplayer"),
            "density" => SetCell(tables, depth, 0, "monster_density_divisor", "0"),
            "capacity" => SetCell(tables, depth, 0, "ordinary_monster_capacity", "200"),
            "trap" => SetCell(tables, depth, 0, "trap_percent", "101"),
            "depth-order" => SetCell(tables, depth, 0, "depth", "2"),
            "story" => SetCell(tables, AuthoritativeCathedralPopulationMetadata.StoryTableName, 0, "message_id", "-1"),
            "shrine-order" => SetCell(tables, AuthoritativeCathedralPopulationMetadata.ShrineTableName, 0, "native_shrine_id", "1"),
            "no-shrines" => Replace(tables, AuthoritativeCathedralPopulationMetadata.ShrineTableName,
                table => Table(table.SourcePath, table.Columns, 26, row => [row.ToString(CultureInfo.InvariantCulture), "false"])),
            "unavailable-potion" => SetCell(tables, AuthoritativeCathedralThemeItemMetadata.ItemsTablePath, 24, "available", "false"),
            _ => throw new InvalidOperationException(),
        };
        Assert.Throws<InvalidDataException>(() => Load(tables));
    }

    [Fact]
    public void AllGeometryPolicyAndCatalogChangesAreBoundToPackIdentity()
    {
        var tables = CreateCompleteTables();
        var original = Load(tables);
        var changes = new (string Table, int Row, string Column, string Value)[] {
            (AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath, 0, "piece_north_west", "5"),
            (AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath, 0, "solid", "true"),
            (AuthoritativeCathedralPopulationMetadata.DepthTableName, 0, "trap_percent", "11"),
            (AuthoritativeCathedralPopulationMetadata.StoryTableName, 0, "message_id", "103"),
            (AuthoritativeCathedralPopulationMetadata.ShrineTableName, 0, "single_player_available", "false"),
            (AuthoritativeCathedralPopulationCatalog.MonsterTableName, 0, "width", "65"),
            (AuthoritativeCathedralPopulationCatalog.ObjectTableName, 0, "anim_width", "65"),
            (AuthoritativeCathedralUniqueMonsterCatalog.TableName, 0, "maximum_hit_points", "101"),
            (AuthoritativeCathedralThemeItemMetadata.ItemsTablePath, 24, "minimum_strength", "255"),
            (AuthoritativeCathedralThemeItemMetadata.CursorAnimationsTablePath, 0, "frame_count", "4"),
            (AuthoritativeCathedralThemeItemMetadata.GlyphsTablePath, 1, "width", "7"),
            (AuthoritativeCathedralThemeItemMetadata.RulesTablePath, 0, "gold_percent", "73"),
        };
        foreach (var change in changes) {
            var modified = SetCell(tables, change.Table, change.Row, change.Column, change.Value);
            if (change.Table == AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath)
                modified = SetCell(modified, AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName, 0, "effective_flags", "1");
            var changed = Load(modified);
            Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
            Assert.NotEqual(original.ManifestHash, changed.ManifestHash);
        }
        var copy = original.ToContentPack();
        ((string[])copy.Tables[0].Columns)[0] = "caller-mutation";
        Assert.Equal(original.Fingerprint, Load(original.ToContentPack().Tables).Fingerprint);
    }

    [Fact]
    public void OpaqueSolBitIsPreservedAndIdentityBoundWithoutChangingNamedGeometry()
    {
        var original = Load(CreateCompleteTables());
        var metadata = Load(SetCell(CreateCompleteTables(), AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName, 0, "effective_flags", "64"));
        Assert.Equal((byte)64, metadata.EffectivePieceProperties[0]);
        Assert.Equal(original.Geometry.TileProperties.Definitions, metadata.Geometry.TileProperties.Definitions);
        Assert.NotEqual(original.Fingerprint, metadata.Fingerprint);
        var rows = AuthoritativeCathedralPopulationMetadata.CreateTables(metadata.Geometry, metadata.EffectivePieceProperties,
            metadata.Population.Monsters, metadata.Population.Objects, metadata.UniqueMonsters.Definitions, metadata.Items.Catalog,
            metadata.DepthRules, metadata.StoryMessages, metadata.ShrineAvailable);
        Assert.Equal((byte)64, Load(rows).EffectivePieceProperties[0]);
        Assert.Equal(metadata.Items.Fingerprint, Load(rows).Items.Fingerprint);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("0.0")]
    public void RawEffectiveSolRejectsMismatchAndInvalidByteCells(string value)
    {
        Assert.Throws<InvalidDataException>(() => Load(SetCell(CreateCompleteTables(),
            AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName, 0, "effective_flags", value)));
        Assert.Throws<InvalidDataException>(() => Load(DropLastRow(CreateCompleteTables(), AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName)));
    }

    [Fact]
    public void InputAndOutputAncestorLinksAreRefusedBeforeFollowingAliases()
    {
        using var root = new OwnedDirectory();
        var real = Path.Combine(root.Path, "real");
        var input = Path.Combine(real, "input");
        Directory.CreateDirectory(input);
        foreach (var table in CreateCompleteTables()) File.WriteAllText(Path.Combine(input, table.SourcePath), table.ToCanonicalTsv());
        var link = Path.Combine(root.Path, "alias");
        try {
            try { Directory.CreateSymbolicLink(link, real); }
            catch (UnauthorizedAccessException error) { Assert.Skip("Directory-link privilege is unavailable: " + error.Message); }
            catch (IOException error) { Assert.Skip("Directory links are unavailable on the fixture filesystem: " + error.Message); }
            Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.CompileDirectory(Path.Combine(link, "input"), Path.Combine(root.Path, "output")));
            Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.CompileDirectory(input, Path.Combine(link, "input", "nested")));
            Assert.False(Directory.Exists(Path.Combine(input, "nested")));
        } finally {
            if (Directory.Exists(link)) Directory.Delete(link);
        }
    }

    [Fact]
    public void PreSelectionParametersComeFromReloadedCatalogsAndEffectiveSol()
    {
        var metadata = Load(CreateCompleteTables());
        var flags = metadata.Geometry.TileProperties.Definitions.Select(row => (byte)row.Flags).ToArray();
        var geometry = new AuthoritativeCathedralPopulationGeometry(new ushort[112 * 112], new byte[112 * 112],
            new byte[112 * 112], flags, new bool[112 * 112], new bool[112 * 112], new bool[112 * 112]);
        var player = new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(50, 50), new(50, 50));
        var parameters = metadata.CreateInitialPopulationParameters(1, 123, 456, geometry, player, 1, []);
        Assert.Equal(123U, parameters.PopulationSeed);
        Assert.Equal(metadata.Population.Objects, parameters.ObjectTemplate.Objects);
        Assert.Equal(metadata.StoryMessages, parameters.ObjectTemplate.StoryMessages);
        Assert.Equal(10, parameters.ObjectTemplate.TrapPercent);
        Assert.Equal(30, metadata.RuleForDepth(1).MonsterDensityDivisor);
        Assert.Throws<InvalidDataException>(() => metadata.RuleForDepth(5));
        flags[0] = 1;
        var changed = new AuthoritativeCathedralPopulationGeometry(new ushort[112 * 112], new byte[112 * 112],
            new byte[112 * 112], flags, new bool[112 * 112], new bool[112 * 112], new bool[112 * 112]);
        Assert.Throws<InvalidDataException>(() => metadata.CreateInitialPopulationParameters(1, 123, 456, changed, player, 1, []));
    }

    [Fact]
    public void CompilerPublishesDeterministicReloadableCrLfPackWithoutExpectedStates()
    {
        using var root = new OwnedDirectory();
        var first = CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables(), Path.Combine(root.Path, "one"));
        var second = CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables(), Path.Combine(root.Path, "two"));
        Assert.Equal(first.MetadataFingerprint, second.MetadataFingerprint);
        Assert.Equal(first.ManifestHash, second.ManifestHash);
        Assert.Equal(first.FileTreeHash, second.FileTreeHash);
        Assert.Equal(AuthoritativeCathedralPopulationMetadata.TableNames.Count, first.TableCount);
        var loaded = CathedralPopulationMetadataCompiler.LoadDirectory(first.OutputDirectory);
        Assert.Equal(first.MetadataFingerprint, loaded.Fingerprint);
        Assert.Equal(first.ManifestHash, loaded.ManifestHash);
        foreach (var file in Directory.EnumerateFiles(first.OutputDirectory)) {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("\n", text.Replace("\r\n", "", StringComparison.Ordinal));
        }
        Assert.Equal(AuthoritativeCathedralPopulationMetadata.TableNames.Append(ContentPackLoader.ManifestFileName).Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(first.OutputDirectory).Select(System.IO.Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Throws<IOException>(() => CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables(), first.OutputDirectory));
        Assert.Equal(first.MetadataFingerprint, CathedralPopulationMetadataCompiler.LoadDirectory(first.OutputDirectory).Fingerprint);
    }

    [Fact]
    public void DirectoryCompilerRejectsOverlapUnknownFilesAndSubtreesWithoutPublication()
    {
        using var root = new OwnedDirectory();
        var input = Path.Combine(root.Path, "input");
        Directory.CreateDirectory(input);
        foreach (var table in CreateCompleteTables()) File.WriteAllText(Path.Combine(input, table.SourcePath), table.ToCanonicalTsv());
        var output = Path.Combine(root.Path, "output");
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.CompileDirectory(input, Path.Combine(input, "nested")));
        File.WriteAllText(Path.Combine(input, "expected.json"), "{}");
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.CompileDirectory(input, output));
        Assert.False(Directory.Exists(output));
        File.Delete(Path.Combine(input, "expected.json"));
        Directory.CreateDirectory(Path.Combine(input, "unexpected"));
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.CompileDirectory(input, output));
        Directory.Delete(Path.Combine(input, "unexpected"));
        var receipt = CathedralPopulationMetadataCompiler.CompileDirectory(input, output);
        Assert.Equal(Load(CreateCompleteTables()).Fingerprint, receipt.MetadataFingerprint);
        File.WriteAllText(Path.Combine(output, "expected.json"), "{}");
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.LoadDirectory(output));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("float")]
    [InlineData("scalar")]
    [InlineData("syntax")]
    public void PublishedMetadataRejectsMalformedManifests(string mutation)
    {
        using var root = new OwnedDirectory();
        var receipt = CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables(), Path.Combine(root.Path, "pack"));
        var file = Path.Combine(receipt.OutputDirectory, ContentPackLoader.ManifestFileName);
        var text = File.ReadAllText(file);
        text = mutation switch {
            "duplicate" => text.TrimEnd()[..^1] + ",\"id\":\"other\"}",
            "unknown" => text.TrimEnd()[..^1] + ",\"expected\":{}}",
            "float" => text.Replace("\"formatVersion\": 1", "\"formatVersion\": 1.0", StringComparison.Ordinal),
            "scalar" => text.Replace("\"formatVersion\": 1", "\"formatVersion\": true", StringComparison.Ordinal),
            "syntax" => "{ invalid json",
            _ => throw new InvalidOperationException(),
        };
        File.WriteAllText(file, text);
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.LoadDirectory(receipt.OutputDirectory));
    }

    [Fact]
    public void PublishedMetadataRejectsNonUtf8ManifestWithoutChangingPackFiles()
    {
        using var root = new OwnedDirectory();
        var receipt = CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables(), Path.Combine(root.Path, "pack"));
        var file = Path.Combine(receipt.OutputDirectory, ContentPackLoader.ManifestFileName);
        var original = File.ReadAllBytes(file);
        File.WriteAllBytes(file, [0xFF, 0xFE]);
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.LoadDirectory(receipt.OutputDirectory));
        Assert.Equal(new byte[] { 0xFF, 0xFE }, File.ReadAllBytes(file));
        File.WriteAllBytes(file, original);
        Assert.Equal(receipt.MetadataFingerprint, CathedralPopulationMetadataCompiler.LoadDirectory(receipt.OutputDirectory).Fingerprint);
    }

    [Fact]
    public void InvalidInputsFailBeforeCreatingOutputAndCliRejectsMixedModes()
    {
        using var root = new OwnedDirectory();
        var output = Path.Combine(root.Path, "invalid");
        Assert.Throws<InvalidDataException>(() => CathedralPopulationMetadataCompiler.Compile(CreateCompleteTables().Skip(1), output));
        Assert.False(Directory.Exists(output));
        var program = typeof(CathedralPopulationMetadataCompiler).Assembly.GetType("Godevilution.Tools.Program")!;
        var main = program.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!;
        foreach (var args in new[] {
            new[] { "--compile-population-metadata" },
            new[] { "--compile-population-metadata", "--metadata-directory", root.Path, "--output-directory", output, "--depth-seeds", "1,2,3,4" },
            new[] { "--compile-population-metadata", "--metadata-directory", root.Path, "--output-directory", output, "--compile-population-metadata" },
        }) Assert.Equal(1, main.Invoke(null, [args]));
        Assert.False(Directory.Exists(output));
    }

    internal static IReadOnlyList<TsvTable> CreateCompleteTables()
    {
        var tables = new List<TsvTable>(AuthoritativeCathedralThemeItemMetadata.CreateTables(
            AuthoritativeCathedralThemeItemMetadataTests.CreateCatalog()));
        tables.Add(Table(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            ["mega_tile_id", "piece_north_west", "piece_north_east", "piece_south_west", "piece_south_east"], 206,
            row => [row.ToString(CultureInfo.InvariantCulture), "0", "1", "2", "3"]));
        tables.Add(Table(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            ["piece_id", "solid", "block_light", "block_missile", "transparent", "transparent_left", "transparent_right", "trap"], 1379,
            row => [row.ToString(CultureInfo.InvariantCulture), "false", "false", "false", "false", "false", "false", "false"]));
        tables.Add(Table(AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName,
            AuthoritativeCathedralPopulationMetadata.EffectiveSolColumns, 1379,
            row => [row.ToString(CultureInfo.InvariantCulture), "0"]));
        tables.Add(Table(AuthoritativeCathedralPopulationCatalog.MonsterTableName,
            AuthoritativeCathedralPopulationCatalog.MonsterColumns, 138, id => [
                id.ToString(CultureInfo.InvariantCulture), "1", (100 + id).ToString(CultureInfo.InvariantCulture), "0", "16",
                id is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 ? "1" : "0",
                "2", "4", "0", "0", "0", "1", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0",
                "0", "64", "1", "0", "0", "0", "0", "7", "1", "0", "0", "0", "0", "9",
            ]));
        tables.Add(Table(AuthoritativeCathedralPopulationCatalog.ObjectTableName,
            AuthoritativeCathedralPopulationCatalog.ObjectColumns, 109,
            row => [row.ToString(CultureInfo.InvariantCulture), "0", "5", "8", "64", "0"]));
        tables.Add(Table(AuthoritativeCathedralUniqueMonsterCatalog.TableName,
            AuthoritativeCathedralUniqueMonsterCatalog.Columns, 100, id => [
                id.ToString(CultureInfo.InvariantCulture), (id % 138).ToString(CultureInfo.InvariantCulture), (id % 5).ToString(CultureInfo.InvariantCulture),
                "100", "0", "0", "1", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "-1",
            ]));
        tables.Add(Table(AuthoritativeCathedralPopulationMetadata.DepthTableName, AuthoritativeCathedralPopulationMetadata.DepthColumns, 4,
            row => [(row + 1).ToString(CultureInfo.InvariantCulture), AuthoritativeCathedralPopulationMetadata.Policy, "30", row == 0 ? "10" : "15", "190"]));
        tables.Add(Table(AuthoritativeCathedralPopulationMetadata.StoryTableName, AuthoritativeCathedralPopulationMetadata.StoryColumns, 3,
            row => [row.ToString(CultureInfo.InvariantCulture), (100 + row).ToString(CultureInfo.InvariantCulture)]));
        tables.Add(Table(AuthoritativeCathedralPopulationMetadata.ShrineTableName, AuthoritativeCathedralPopulationMetadata.ShrineColumns, 26,
            row => [row.ToString(CultureInfo.InvariantCulture), row is 18 or 25 ? "false" : "true"]));
        return tables;
    }

    internal static IReadOnlyList<TsvTable> SetCell(IEnumerable<TsvTable> tables, string path, int row, string column, string value)
        => Replace(tables, path, table => {
            var lines = table.ToCanonicalTsv().TrimEnd('\n').Split('\n');
            var cells = lines[row + 1].Split('\t');
            cells[Array.IndexOf(table.Columns.ToArray(), column)] = value;
            lines[row + 1] = string.Join('\t', cells);
            return TsvTable.Parse(path, string.Join('\n', lines) + "\n");
        });

    private static IReadOnlyList<TsvTable> DropLastRow(IEnumerable<TsvTable> tables, string path)
        => Replace(tables, path, table => TsvTable.Parse(path,
            string.Join('\n', table.ToCanonicalTsv().TrimEnd('\n').Split('\n')[..^1]) + "\n"));

    private static IReadOnlyList<TsvTable> Replace(IEnumerable<TsvTable> tables, string path, Func<TsvTable, TsvTable> replace)
        => tables.Select(table => table.SourcePath == path ? replace(table) : table).ToArray();

    private static TsvTable Table(string path, IReadOnlyList<string> columns, int count, Func<int, string[]> row)
    {
        var contents = new StringBuilder(string.Join('\t', columns) + "\n");
        for (var index = 0; index < count; index++) contents.AppendJoin('\t', row(index)).Append('\n');
        return TsvTable.Parse(path, contents.ToString());
    }

    private static AuthoritativeCathedralPopulationMetadata Load(IEnumerable<TsvTable> tables)
        => AuthoritativeCathedralPopulationMetadata.Load(new("fixture", "1.0.0", tables.ToArray(), schema: Schema()));
    private static ContentSchemaIdentity Schema() => new(AuthoritativeCathedralPopulationMetadata.SchemaId,
        AuthoritativeCathedralPopulationMetadata.SchemaVersion);

    private sealed class OwnedDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Godevilution-Metadata-Test-" + Guid.NewGuid().ToString("N"));
        public OwnedDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
