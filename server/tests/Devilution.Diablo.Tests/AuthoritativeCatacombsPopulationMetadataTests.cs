using Xunit;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Data;
using Devilution.Diablo;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsPopulationMetadataTests
{
    [Fact]
    public void LoadsExactCatacombsGeometryRawSolSourceAndDepthRules()
    {
        var metadata = Load(CreateCompleteTables());

        Assert.Equal(160, metadata.Geometry.Pieces.Definitions.Count);
        Assert.Equal(1379, metadata.EffectivePieceProperties.Count);
        Assert.Equal(559, metadata.SolSourceByteCount);
        Assert.Equal((byte)0x40, metadata.EffectivePieceProperties[4]);
        Assert.All(metadata.EffectivePieceProperties.Skip(559), flags => Assert.Equal((byte)0, flags));
        Assert.Equal("levels/l2data/l2.til", metadata.SourceAssets[0].SourcePath);
        Assert.Equal(1280, metadata.SourceAssets[0].SourceByteCount);
        Assert.Equal("levels/l2data/l2.sol", metadata.SourceAssets[1].SourcePath);
        Assert.Equal(559, metadata.SourceAssets[1].SourceByteCount);
        Assert.Equal("txtdata/text/textdat.tsv", metadata.SourceAssets[2].SourcePath);
        Assert.True(metadata.SourceAssets[2].SourceByteCount > 0);
        Assert.Equal(ReadCatacombsStorySource().Sha256, metadata.SourceAssets[2].Sha256);
        Assert.Equal(new[] { 250, 253, 256 }, metadata.StoryMessages);
        Assert.Equal(new[] { 5, 6, 7, 8 }, metadata.DepthRules.Select(rule => rule.Depth));
        Assert.Equal(new[] { 20, 20, 25, 25 }, metadata.DepthRules.Select(rule => rule.TrapPercent));
        Assert.All(metadata.DepthRules, rule => {
            Assert.Equal(30, rule.MonsterDensityDivisor);
            Assert.Equal(150, rule.MultiplayerMonsterMultiplierPercent);
            Assert.Equal(AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity,
                rule.OrdinaryMonsterCapacity);
        });
        Assert.Equal(20, metadata.RuleForDepth(5).TrapPercent);
        Assert.Equal(25, metadata.RuleForDepth(8).TrapPercent);
        Assert.Throws<InvalidDataException>(() => metadata.RuleForDepth(4));
        Assert.Throws<InvalidDataException>(() => metadata.RuleForDepth(9));
        Assert.Equal(138, metadata.Population.Monsters.Count);
        Assert.Equal(109, metadata.Population.Objects.Count);
        Assert.Equal(100, metadata.UniqueMonsters.Definitions.Count);
        Assert.Equal(26, metadata.ShrineAvailable.Count);
    }

    [Theory]
    [InlineData("til-count")]
    [InlineData("sol-tail")]
    [InlineData("sol-flags")]
    [InlineData("source-byte-count")]
    [InlineData("source-hash")]
    [InlineData("til-source-hash-mismatch")]
    [InlineData("sol-source-hash-mismatch")]
    [InlineData("story-source-byte-count")]
    [InlineData("story-source-hash")]
    [InlineData("depth-policy")]
    [InlineData("depth-trap")]
    [InlineData("depth-order")]
    [InlineData("story-c1-values")]
    [InlineData("automap-as-population")]
    public void RejectsMalformedCatacombsPopulationSchema(string mutation)
    {
        var tables = CreateCompleteTables();
        tables = mutation switch {
            "til-count" => Replace(tables,
                AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
                table => DropLast(table)),
            "sol-tail" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.EffectiveSolTableName, 559, "effective_flags", "64"),
            "sol-flags" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.EffectiveSolTableName, 0, "effective_flags", "1"),
            "source-byte-count" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 1, "source_byte_count", "560"),
            "source-hash" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 0, "sha256", new string('A', 64)),
            "til-source-hash-mismatch" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 0, "sha256", new string('c', 64)),
            "sol-source-hash-mismatch" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 1, "sha256", new string('d', 64)),
            "story-source-byte-count" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 2, "source_byte_count", "0"),
            "story-source-hash" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.SourceTableName, 2, "sha256", new string('A', 64)),
            "depth-policy" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.DepthTableName, 0, "policy", "cathedral"),
            "depth-trap" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.DepthTableName, 0, "trap_percent", "21"),
            "depth-order" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.DepthTableName, 0, "depth", "6"),
            "story-c1-values" => SetCell(tables,
                AuthoritativeCatacombsPopulationMetadata.StoryTableName, 0, "message_id", "249"),
            "automap-as-population" => tables.Append(TsvTable.Parse(
                AuthoritativeCatacombsAutomapMetadata.DefinitionsTableName, "id\ttype\tflags\n")).ToArray(),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidDataException>(() => Load(tables));
    }

    [Fact]
    public void SourceHashesAndEveryC2TableAreBoundToTheDetachedPackIdentity()
    {
        var tables = CreateCompleteTables();
        var original = Load(tables);
        Assert.Throws<InvalidDataException>(() => Load(SetCell(tables,
            AuthoritativeCatacombsPopulationMetadata.SourceTableName, 0, "sha256", new string('c', 64))));
        Assert.Throws<InvalidDataException>(() => Load(SetCell(tables,
            AuthoritativeCatacombsPopulationMetadata.SourceTableName, 1, "sha256", new string('d', 64))));
        var changedGeometry = SetCell(tables,
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath, 0, "piece_north_west", "7");
        changedGeometry = SetCell(changedGeometry, AuthoritativeCatacombsPopulationMetadata.SourceTableName,
            0, "sha256", ComputeTilSha256(changedGeometry.Single(table =>
                table.SourcePath == AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath)));
        var changedBoundGeometry = Load(changedGeometry);
        var changedSol = SetCell(tables, AuthoritativeCatacombsPopulationMetadata.EffectiveSolTableName,
            0, "effective_flags", "1");
        changedSol = SetCell(changedSol, AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            0, "solid", "true");
        changedSol = SetCell(changedSol, AuthoritativeCatacombsPopulationMetadata.SourceTableName,
            1, "sha256", ComputeSolSha256(changedSol.Single(table =>
                table.SourcePath == AuthoritativeCatacombsPopulationMetadata.EffectiveSolTableName)));
        var changedBoundSol = Load(changedSol);
        var changedStorySource = Load(SetCell(tables, AuthoritativeCatacombsPopulationMetadata.SourceTableName,
            2, "sha256", new string('d', 64)));
        Assert.NotEqual(original.Fingerprint, changedBoundGeometry.Fingerprint);
        Assert.NotEqual(original.ManifestHash, changedBoundGeometry.ManifestHash);
        Assert.NotEqual(original.Fingerprint, changedBoundSol.Fingerprint);
        Assert.NotEqual(original.ManifestHash, changedBoundSol.ManifestHash);
        Assert.NotEqual(original.Fingerprint, changedStorySource.Fingerprint);
        Assert.NotEqual(original.ManifestHash, changedStorySource.ManifestHash);
        Assert.Equal(new[] { 250, 253, 256 }, original.StoryMessages);
        Assert.Equal(original.Fingerprint,
            Load(original.ToContentPack().Tables).Fingerprint);

        var detached = original.ToContentPack();
        ((string[])detached.Tables[0].Columns)[0] = "mutated-by-caller";
        Assert.Equal(original.Fingerprint, Load(original.ToContentPack().Tables).Fingerprint);
    }

    internal static IReadOnlyList<TsvTable> CreateCompleteTables()
    {
        var c1Tables = CathedralPopulationMetadataCompilerTests.CreateCompleteTables();
        var c1 = AuthoritativeCathedralPopulationMetadata.Load(new ContentPack(
            "c1-fixture", "1.0.0", c1Tables,
            schema: new ContentSchemaIdentity(
                AuthoritativeCathedralPopulationMetadata.SchemaId,
                AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
        var geometry = CreateGeometry();
        var effectiveSol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        effectiveSol[4] = 0x40;
        var rules = new[] {
            new AuthoritativeCatacombsPopulationDepthRule(5, 30, 150, 20,
                AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity),
            new AuthoritativeCatacombsPopulationDepthRule(6, 30, 150, 20,
                AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity),
            new AuthoritativeCatacombsPopulationDepthRule(7, 30, 150, 25,
                AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity),
            new AuthoritativeCatacombsPopulationDepthRule(8, 30, 150, 25,
                AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity),
        };
        var storySource = ReadCatacombsStorySource();
        return AuthoritativeCatacombsPopulationMetadata.CreateTables(
            geometry,
            effectiveSol,
            ComputeTilSha256(geometry.Pieces.Definitions),
            ComputeSha256(effectiveSol.Take(AuthoritativeCatacombsPopulationMetadata.NativeSolSourceByteCount)),
            c1.Population.Monsters,
            c1.Population.Objects,
            c1.UniqueMonsters.Definitions,
            c1.Items.Catalog,
            rules,
            storySource.MessageIds,
            storySource.SourceByteCount,
            storySource.Sha256,
            c1.ShrineAvailable);
    }

    private static (int[] MessageIds, int SourceByteCount, string Sha256) ReadCatacombsStorySource()
    {
        var path = Path.Combine(FindRepositoryRoot(), "assets", "txtdata", "text", "textdat.tsv");
        var bytes = File.ReadAllBytes(path);
        var table = TsvTable.Parse("assets/txtdata/text/textdat.tsv", File.ReadAllText(path));
        var expectedSymbols = new[] { "TEXT_BOOK12", "TEXT_BOOK22", "TEXT_BOOK32" };
        var rows = table.Rows.Select((row, index) => (Symbol: row.Required("txtstrid"), Id: index))
            .ToDictionary(row => row.Symbol, row => row.Id, StringComparer.Ordinal);
        return (expectedSymbols.Select(symbol => rows.TryGetValue(symbol, out var id)
                ? id : throw new InvalidDataException($"Canonical text data is missing {symbol}.")).ToArray(),
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "text", "textdat.tsv")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository text metadata.");
    }

    internal static AuthoritativeNativeDunGeometryCatalog CreateGeometry()
    {
        var pieces = new AuthoritativeDunPieceCatalog(Enumerable.Range(0,
            AuthoritativeCatacombsPopulationMetadata.NativeTilRecordCount).Select(index =>
            new AuthoritativeDunPieceDefinition((uint)index, 0, 1, 2, 3)));
        var properties = new AuthoritativeDunTilePropertyCatalog(Enumerable.Range(0,
            AuthoritativeDunTilePropertyCatalog.NativeTileCount).Select(index =>
            new AuthoritativeDunTileProperty((uint)index, AuthoritativeDunTileFlags.None)));
        return new(pieces, properties);
    }

    internal static string ComputeTilSha256(TsvTable table)
    {
        var bytes = new byte[table.Rows.Count * sizeof(ushort) * 4];
        for (var index = 0; index < table.Rows.Count; index++) {
            var row = table.Rows[index];
            var offset = index * sizeof(ushort) * 4;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)row.RequiredInt32("piece_north_west")));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), checked((ushort)row.RequiredInt32("piece_north_east")));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), checked((ushort)row.RequiredInt32("piece_south_west")));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), checked((ushort)row.RequiredInt32("piece_south_east")));
        }
        return ComputeSha256(bytes);
    }

    private static string ComputeSolSha256(TsvTable table)
        => ComputeSha256(table.Rows.Take(AuthoritativeCatacombsPopulationMetadata.NativeSolSourceByteCount)
            .Select(row => checked((byte)row.RequiredInt32("effective_flags"))));

    private static string ComputeTilSha256(IReadOnlyList<AuthoritativeDunPieceDefinition> definitions)
    {
        var bytes = new byte[definitions.Count * sizeof(ushort) * 4];
        for (var index = 0; index < definitions.Count; index++) {
            var piece = definitions[index];
            var offset = index * sizeof(ushort) * 4;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)piece.NorthWestPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), checked((ushort)piece.NorthEastPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), checked((ushort)piece.SouthWestPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), checked((ushort)piece.SouthEastPieceId));
        }
        return ComputeSha256(bytes);
    }

    private static string ComputeSha256(IEnumerable<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();

    internal static AuthoritativeCatacombsPopulationMetadata Load(IEnumerable<TsvTable> tables)
        => AuthoritativeCatacombsPopulationMetadata.Load(new ContentPack(
            AuthoritativeCatacombsPopulationMetadata.CatalogId,
            AuthoritativeCatacombsPopulationMetadata.SchemaVersion,
            tables.ToArray(),
            schema: new ContentSchemaIdentity(
                AuthoritativeCatacombsPopulationMetadata.SchemaId,
                AuthoritativeCatacombsPopulationMetadata.SchemaVersion)));

    internal static IReadOnlyList<TsvTable> SetCell(
        IEnumerable<TsvTable> tables, string path, int rowIndex, string column, string value)
        => Replace(tables, path, table => {
            var lines = table.ToCanonicalTsv().TrimEnd('\n').Split('\n');
            var cells = lines[rowIndex + 1].Split('\t');
            cells[Array.IndexOf(table.Columns.ToArray(), column)] = value;
            lines[rowIndex + 1] = string.Join('\t', cells);
            return TsvTable.Parse(path, string.Join('\n', lines) + "\n");
        });

    internal static IReadOnlyList<TsvTable> Replace(
        IEnumerable<TsvTable> tables, string path, Func<TsvTable, TsvTable> replace)
        => tables.Select(table => table.SourcePath == path ? replace(table) : table).ToArray();

    private static TsvTable DropLast(TsvTable table)
        => TsvTable.Parse(table.SourcePath,
            string.Join('\n', table.ToCanonicalTsv().TrimEnd('\n').Split('\n')[..^1]) + "\n");
}
