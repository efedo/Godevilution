using Xunit;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools;

namespace Devilution.Diablo.Tests;

public sealed class CatacombsPopulationMetadataCompilerTests
{
    [Fact]
    public void CompilerPublishesDeterministicReloadableCrLfPackAndRefusesExistingOutput()
    {
        using var root = new OwnedDirectory();
        var first = CatacombsPopulationMetadataCompiler.Compile(
            AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables(),
            Path.Combine(root.Path, "one"));
        var second = CatacombsPopulationMetadataCompiler.Compile(
            AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables(),
            Path.Combine(root.Path, "two"));

        Assert.Equal(first.MetadataFingerprint, second.MetadataFingerprint);
        Assert.Equal(first.ManifestHash, second.ManifestHash);
        Assert.Equal(first.FileTreeHash, second.FileTreeHash);
        Assert.Equal(AuthoritativeCatacombsPopulationMetadata.TableNames.Count, first.TableCount);
        var loaded = CatacombsPopulationMetadataCompiler.LoadDirectory(first.OutputDirectory);
        Assert.Equal(first.MetadataFingerprint, loaded.Fingerprint);
        Assert.Equal(first.ManifestHash, loaded.ManifestHash);
        foreach (var file in Directory.EnumerateFiles(first.OutputDirectory)) {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("\n", text.Replace("\r\n", "", StringComparison.Ordinal));
        }
        Assert.Equal(
            AuthoritativeCatacombsPopulationMetadata.TableNames.Append(ContentPackLoader.ManifestFileName)
                .Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(first.OutputDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Throws<IOException>(() => CatacombsPopulationMetadataCompiler.Compile(
            AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables(), first.OutputDirectory));
        Assert.Equal(first.MetadataFingerprint,
            CatacombsPopulationMetadataCompiler.LoadDirectory(first.OutputDirectory).Fingerprint);
    }

    [Fact]
    public void DirectoryCompilerAndDedicatedCliModeRejectInvalidOrMixedInputs()
    {
        using var root = new OwnedDirectory();
        var input = Path.Combine(root.Path, "input");
        var output = Path.Combine(root.Path, "output");
        WriteInput(input, AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables());
        Assert.Throws<InvalidDataException>(() => CatacombsPopulationMetadataCompiler.CompileDirectory(
            input, Path.Combine(input, "nested")));
        File.WriteAllText(Path.Combine(input, "unexpected.tsv"), "extra");
        Assert.Throws<InvalidDataException>(() => CatacombsPopulationMetadataCompiler.CompileDirectory(input, output));
        Assert.False(Directory.Exists(output));
        File.Delete(Path.Combine(input, "unexpected.tsv"));

        var main = GetProgramMain();
        Assert.Equal(0, InvokeMain(main, [
            "--compile-catacombs-population-metadata",
            "--metadata-directory", input,
            "--output-directory", output,
        ]));
        Assert.Equal(AuthoritativeCatacombsPopulationMetadata.TableNames.Count,
            CatacombsPopulationMetadataCompiler.LoadDirectory(output).Tables.Count);

        Assert.Equal(1, InvokeMain(main, ["--compile-catacombs-population-metadata"]));
        Assert.Equal(1, InvokeMain(main, [
            "--compile-catacombs-population-metadata",
            "--metadata-directory", input,
            "--metadata-directory", input,
            "--output-directory", Path.Combine(root.Path, "duplicate"),
        ]));
        Assert.Equal(1, InvokeMain(main, [
            "--compile-catacombs-population-metadata",
            "--compile-population-metadata",
            "--metadata-directory", input,
            "--output-directory", Path.Combine(root.Path, "mixed"),
        ]));
        Assert.False(Directory.Exists(Path.Combine(root.Path, "duplicate")));
        Assert.False(Directory.Exists(Path.Combine(root.Path, "mixed")));

        var invalidInput = Path.Combine(root.Path, "invalid-input");
        WriteInput(invalidInput, AuthoritativeCatacombsPopulationMetadataTests.SetCell(
            AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables(),
            AuthoritativeCatacombsPopulationMetadata.DepthTableName, 0, "trap_percent", "21"));
        var invalidOutput = Path.Combine(root.Path, "invalid-output");
        Assert.Equal(1, InvokeMain(main, [
            "--compile-catacombs-population-metadata",
            "--metadata-directory", invalidInput,
            "--output-directory", invalidOutput,
        ]));
        Assert.False(Directory.Exists(invalidOutput));
    }

    [Fact(Explicit = true)]
    public void QualifiedPopulationFiveSourceAndGlobalTablesCompileWithoutExpectedWorldSnapshots()
    {
        var sourcePath = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_SOURCE_METADATA");
        var globalDirectory = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_GLOBAL_METADATA_DIRECTORY");
        var storySourcePath = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_STORY_SOURCE_TABLE");
        Assert.False(string.IsNullOrWhiteSpace(sourcePath),
            "Set GODEVILUTION_NATIVE_CATACOMBS_SOURCE_METADATA to the qualified population-5 source JSON.");
        Assert.False(string.IsNullOrWhiteSpace(globalDirectory),
            "Set GODEVILUTION_NATIVE_CATACOMBS_GLOBAL_METADATA_DIRECTORY to the read-only global native TSV directory.");
        Assert.False(string.IsNullOrWhiteSpace(storySourcePath),
            "Set GODEVILUTION_NATIVE_CATACOMBS_STORY_SOURCE_TABLE to canonical assets/txtdata/text/textdat.tsv.");
        using var root = new OwnedDirectory();
        var tables = BuildTablesFromQualifiedInputs(sourcePath!, globalDirectory!, storySourcePath!);
        var receipt = CatacombsPopulationMetadataCompiler.Compile(tables, Path.Combine(root.Path, "catacombs-pack"));
        var loaded = CatacombsPopulationMetadataCompiler.LoadDirectory(receipt.OutputDirectory);
        Assert.Equal(160, loaded.Geometry.Pieces.Definitions.Count);
        Assert.Equal(1379, loaded.EffectivePieceProperties.Count);
        Assert.Equal(559, loaded.SolSourceByteCount);
        Assert.Equal(new[] { 250, 253, 256 }, loaded.StoryMessages);
        Assert.Equal(3, loaded.SourceAssets.Count);
        Assert.Equal("txtdata/text/textdat.tsv", loaded.SourceAssets[2].SourcePath);
        Assert.Equal(checked((int)new FileInfo(storySourcePath!).Length), loaded.SourceAssets[2].SourceByteCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(storySourcePath!))).ToLowerInvariant(),
            loaded.SourceAssets[2].Sha256);
        Assert.Equal(new[] { 5, 6, 7, 8 }, loaded.DepthRules.Select(rule => rule.Depth));
        Assert.Equal(138, loaded.Population.Monsters.Count);
        Assert.Equal(109, loaded.Population.Objects.Count);
        Assert.Equal(100, loaded.UniqueMonsters.Definitions.Count);
        Assert.Equal(AuthoritativeCatacombsPopulationMetadata.NativeTilSourceByteCount,
            loaded.SourceAssets[0].SourceByteCount);

        using var sourceDocument = JsonDocument.Parse(File.ReadAllBytes(sourcePath!));
        var ampWords = sourceDocument.RootElement.GetProperty("automapRawWordsLittleEndian")
            .EnumerateArray().Select(value => value.GetUInt16()).ToArray();
        Assert.Equal(160, ampWords.Length);
        var ampBytes = new byte[ampWords.Length * sizeof(ushort)];
        for (var index = 0; index < ampWords.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(ampBytes.AsSpan(index * sizeof(ushort)), ampWords[index]);
        var automapTables = AuthoritativeCatacombsAutomapMetadata.CreateTables(
            ampWords, Convert.ToHexString(SHA256.HashData(ampBytes)).ToLowerInvariant());
        var automap = AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
            AuthoritativeCatacombsAutomapMetadata.CatalogId,
            AuthoritativeCatacombsAutomapMetadata.SchemaVersion,
            automapTables,
            schema: new ContentSchemaIdentity(
                AuthoritativeCatacombsAutomapMetadata.SchemaId,
                AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));
        Assert.Equal(256, automap.Definitions.Count);
        Assert.Equal(AuthoritativeCathedralAutomapTileType.FenceHorizontal, automap.Get(42).Type);
    }

    internal static IReadOnlyList<TsvTable> BuildTablesFromQualifiedInputs(
        string sourceMetadataPath, string globalDirectory, string storySourcePath)
    {
        using var sourceDocument = JsonDocument.Parse(File.ReadAllBytes(sourceMetadataPath));
        var source = sourceDocument.RootElement;
        Assert.Equal("godevilution-native-catacombs-population-sources", source.GetProperty("format").GetString());
        Assert.Equal(1, source.GetProperty("version").GetInt32());
        Assert.Equal(160, source.GetProperty("tileDefinitionCount").GetInt32());
        var rawSolByteCount = source.GetProperty("solSourceByteCount").GetInt32();
        Assert.Equal(559, rawSolByteCount);
        Assert.Equal(320, source.GetProperty("automapSourceByteCount").GetInt32());
        Assert.Equal(160, source.GetProperty("automapWordCount").GetInt32());

        var pieceDefinitions = source.GetProperty("tileDefinitions").EnumerateArray()
            .Select((row, index) => {
                var words = row.EnumerateArray().Select(cell => cell.GetUInt16()).ToArray();
                Assert.Equal(4, words.Length);
                return new AuthoritativeDunPieceDefinition((uint)index, words[0], words[1], words[2], words[3]);
            }).ToArray();
        Assert.Equal(160, pieceDefinitions.Length);
        var sol = source.GetProperty("solEffectiveProperties").EnumerateArray()
            .Select(value => value.GetByte()).ToArray();
        var rawSol = source.GetProperty("solRawBytes").EnumerateArray()
            .Select(value => value.GetByte()).ToArray();
        Assert.Equal(1379, sol.Length);
        Assert.Equal(rawSolByteCount, rawSol.Length);
        Assert.Equal(rawSol, sol[..rawSol.Length]);
        Assert.All(sol.Skip(rawSolByteCount), value => Assert.Equal((byte)0, value));

        var tileProperties = sol.Select((value, index) => new AuthoritativeDunTileProperty(
            (uint)index, (AuthoritativeDunTileFlags)(value & ~0x40))).ToArray();
        var geometry = new AuthoritativeNativeDunGeometryCatalog(
            new AuthoritativeDunPieceCatalog(pieceDefinitions),
            new AuthoritativeDunTilePropertyCatalog(tileProperties));
        var tilBytes = new byte[pieceDefinitions.Length * 8];
        for (var index = 0; index < pieceDefinitions.Length; index++) {
            var row = pieceDefinitions[index];
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(index * 8), (ushort)row.NorthWestPieceId);
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(index * 8 + 2), (ushort)row.NorthEastPieceId);
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(index * 8 + 4), (ushort)row.SouthWestPieceId);
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(index * 8 + 6), (ushort)row.SouthEastPieceId);
        }
        var tilHash = Convert.ToHexString(SHA256.HashData(tilBytes)).ToLowerInvariant();
        var solHash = Convert.ToHexString(SHA256.HashData(rawSol)).ToLowerInvariant();

        var monsterTable = ReadGlobal(AuthoritativeCathedralPopulationCatalog.MonsterTableName);
        var objectTable = ReadGlobal(AuthoritativeCathedralPopulationCatalog.ObjectTableName);
        var population = AuthoritativeCathedralPopulationCatalog.LoadTables(monsterTable, objectTable);
        var uniqueMonsters = AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(
            ReadGlobal(AuthoritativeCathedralUniqueMonsterCatalog.TableName));
        var items = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.TableNames.Select(ReadGlobal));
        Assert.True(File.Exists(storySourcePath), $"Canonical Catacombs story source is missing: {storySourcePath}");
        var storySourceBytes = File.ReadAllBytes(storySourcePath);
        var storyText = TsvTable.Parse("txtdata/text/textdat.tsv", File.ReadAllText(storySourcePath));
        var storySymbols = new[] { "TEXT_BOOK12", "TEXT_BOOK22", "TEXT_BOOK32" };
        var storyRows = storyText.Rows.Select((row, index) => (Symbol: row.Required("txtstrid"), Id: index))
            .ToDictionary(row => row.Symbol, row => row.Id, StringComparer.Ordinal);
        var storyMessages = storySymbols.Select(symbol => storyRows.TryGetValue(symbol, out var id)
            ? id : throw new InvalidDataException($"Canonical text source is missing {symbol}.")).ToArray();
        Assert.Equal(new[] { 250, 253, 256 }, storyMessages);
        var storySourceSha256 = Convert.ToHexString(SHA256.HashData(storySourceBytes)).ToLowerInvariant();
        var shrineTable = ReadGlobal(AuthoritativeCatacombsPopulationMetadata.ShrineTableName);
        var shrines = shrineTable.Rows.Select(row => row.RequiredBoolean("single_player_available")).ToArray();
        var levelGeneration = ReadRepositoryTable("assets/txtdata/levels/level_generation.tsv");
        var rules = Enumerable.Range(5, 4).Select(depth => {
            var row = levelGeneration.Rows.Single(candidate =>
                candidate.RequiredInt32("minLevel") <= depth && candidate.RequiredInt32("maxLevel") >= depth);
            return new AuthoritativeCatacombsPopulationDepthRule(
                depth, row.RequiredInt32("monsterDensityDivisor"),
                row.RequiredInt32("mpMonsterMultiplierPercent"), row.RequiredInt32("trapPercent"),
                AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity);
        }).ToArray();
        return AuthoritativeCatacombsPopulationMetadata.CreateTables(
            geometry, sol, tilHash, solHash, population.Monsters, population.Objects,
            uniqueMonsters.Definitions, items.Catalog, rules, storyMessages,
            storySourceBytes.Length, storySourceSha256, shrines);

        TsvTable ReadGlobal(string name)
        {
            var path = Path.Combine(globalDirectory, name);
            Assert.True(File.Exists(path), $"Required detached global table is missing: {path}");
            return TsvTable.Parse(name, File.ReadAllText(path));
        }

        static TsvTable ReadRepositoryTable(string relativePath)
        {
            var root = FindRepositoryRoot();
            return TsvTable.Parse(relativePath, File.ReadAllText(Path.Combine(root, relativePath)));
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "levels", "level_generation.tsv")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository source-level metadata.");
    }

    private static MethodInfo GetProgramMain()
        => typeof(CatacombsPopulationMetadataCompiler).Assembly
            .GetType("Godevilution.Tools.Program")!
            .GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;

    private static int InvokeMain(MethodInfo main, string[] args)
        => (int)(main.Invoke(null, [args]) ?? throw new InvalidOperationException("Program.Main returned null."));

    private static void WriteInput(string directory, IEnumerable<TsvTable> tables)
    {
        Directory.CreateDirectory(directory);
        foreach (var table in tables)
            File.WriteAllText(Path.Combine(directory, table.SourcePath), table.ToCanonicalTsv());
    }

    private sealed class OwnedDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "Godevilution-Catacombs-Metadata-Test-" + Guid.NewGuid().ToString("N"));
        public OwnedDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
