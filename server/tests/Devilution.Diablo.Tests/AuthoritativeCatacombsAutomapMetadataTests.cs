using Xunit;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Data;
using Devilution.Diablo;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsAutomapMetadataTests
{
    [Fact]
    public void Expands160PackedAmpWordsIntoTheExact256EntryColdCatacombsTable()
    {
        var tables = CreateCompleteTables();
        var metadata = Load(tables);

        Assert.Equal(160, metadata.SourceWords.Count);
        Assert.Equal(256, metadata.Definitions.Count);
        Assert.Equal((ushort)0x4002, metadata.SourceWords[41].PackedWord);
        Assert.Equal(new AuthoritativeCatacombsAutomapDefinition(
            0, AuthoritativeCathedralAutomapTileType.None, AuthoritativeCathedralAutomapTileFlags.None),
            metadata.Get(0));
        Assert.Equal((byte)0, metadata.ResolveGeneratedTile(0).Id);
        Assert.Equal((AuthoritativeCathedralAutomapTileType)3, metadata.Get(41).Type);
        Assert.Equal((AuthoritativeCathedralAutomapTileFlags)1, metadata.Get(41).Flags);
        Assert.Equal(new AuthoritativeCatacombsAutomapDefinition(
            42, AuthoritativeCathedralAutomapTileType.FenceHorizontal, AuthoritativeCathedralAutomapTileFlags.None),
            metadata.Get(42));
        Assert.Equal((AuthoritativeCathedralAutomapTileType)4, metadata.Get(43).Type);
        Assert.Equal((AuthoritativeCathedralAutomapTileFlags)0x80, metadata.Get(43).Flags);
        Assert.All(metadata.Definitions.Skip(161), definition => {
            Assert.Equal(AuthoritativeCathedralAutomapTileType.None, definition.Type);
            Assert.Equal(AuthoritativeCathedralAutomapTileFlags.None, definition.Flags);
        });
        Assert.Equal((byte)160, metadata.ResolveGeneratedTile(160).Id);
        Assert.Throws<InvalidDataException>(() => metadata.ResolveGeneratedTile(161));
    }

    [Theory]
    [InlineData("short-source")]
    [InlineData("source-order")]
    [InlineData("source-range")]
    [InlineData("source-type")]
    [InlineData("source-hash")]
    [InlineData("source-hash-mismatch")]
    [InlineData("source-length")]
    [InlineData("missing-effective-row")]
    [InlineData("changed-effective-override")]
    [InlineData("changed-effective-flag")]
    public void RejectsMalformedOrUnboundAmpProjection(string mutation)
    {
        var tables = CreateCompleteTables();
        switch (mutation) {
        case "short-source":
            tables = Replace(tables, AuthoritativeCatacombsAutomapMetadata.SourceWordsTableName,
                table => DropLast(table));
            break;
        case "source-order":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceWordsTableName,
                1, "source_index", "0");
            break;
        case "source-range":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceWordsTableName,
                0, "packed_word", "65536");
            break;
        case "source-type":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceWordsTableName,
                0, "packed_word", "255");
            break;
        case "source-hash":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceAssetTableName,
                0, "sha256", new string('A', 64));
            break;
        case "source-hash-mismatch":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceAssetTableName,
                0, "sha256", new string('e', 64));
            break;
        case "source-length":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.SourceAssetTableName,
                0, "source_byte_count", "318");
            break;
        case "missing-effective-row":
            tables = Replace(tables, AuthoritativeCatacombsAutomapMetadata.DefinitionsTableName,
                table => DropLast(table));
            break;
        case "changed-effective-override":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.DefinitionsTableName,
                42, "type", "2");
            break;
        case "changed-effective-flag":
            tables = SetCell(tables, AuthoritativeCatacombsAutomapMetadata.DefinitionsTableName,
                42, "flags", "1");
            break;
        default:
            throw new InvalidOperationException();
        }

        Assert.Throws<InvalidDataException>(() => Load(tables));
    }

    [Fact]
    public void CreateTablesRequiresCompleteSourceAndCanonicalHash()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsAutomapMetadata.CreateTables(
            CreateWords()[..^1], new string('a', 64)));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsAutomapMetadata.CreateTables(
            CreateWords(), new string('G', 64)));
    }

    [Fact]
    public void AmpIdentityMustMatchWordsAndReboundWordsChangeCatalogIdentity()
    {
        var original = Load(CreateCompleteTables());
        var words = CreateWords();
        words[0] ^= 0x0100;
        var changedTables = AuthoritativeCatacombsAutomapMetadata.CreateTables(words, ComputeSha256(words));
        var changed = Load(changedTables);

        Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
        Assert.NotEqual(original.ManifestHash, changed.ManifestHash);
        var stale = SetCell(CreateCompleteTables(), AuthoritativeCatacombsAutomapMetadata.SourceAssetTableName,
            0, "sha256", new string('e', 64));
        Assert.Throws<InvalidDataException>(() => Load(stale));
    }

    internal static IReadOnlyList<TsvTable> CreateCompleteTables()
    {
        var words = CreateWords();
        return AuthoritativeCatacombsAutomapMetadata.CreateTables(words, ComputeSha256(words));
    }

    internal static ushort[] CreateWords()
    {
        var values = Enumerable.Range(0, AuthoritativeCatacombsAutomapMetadata.SourceWordCount)
            .Select(index => (ushort)(((index % 5) << 8) | (index % 8))).ToArray();
        values[40] = (ushort)((1 << 8) | 3);
        values[41] = 0x4002;
        values[42] = (ushort)((0x80 << 8) | 4);
        return values;
    }

    internal static AuthoritativeCatacombsAutomapMetadata Load(IEnumerable<TsvTable> tables)
        => AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
            AuthoritativeCatacombsAutomapMetadata.CatalogId,
            AuthoritativeCatacombsAutomapMetadata.SchemaVersion,
            tables.ToArray(),
            schema: new ContentSchemaIdentity(
                AuthoritativeCatacombsAutomapMetadata.SchemaId,
                AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));

    private static string ComputeSha256(IReadOnlyList<ushort> words)
    {
        var bytes = new byte[words.Count * sizeof(ushort)];
        for (var index = 0; index < words.Count; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort)), words[index]);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static IReadOnlyList<TsvTable> SetCell(
        IEnumerable<TsvTable> tables, string path, int rowIndex, string column, string value)
        => Replace(tables, path, table => {
            var lines = table.ToCanonicalTsv().TrimEnd('\n').Split('\n');
            var cells = lines[rowIndex + 1].Split('\t');
            cells[Array.IndexOf(table.Columns.ToArray(), column)] = value;
            lines[rowIndex + 1] = string.Join('\t', cells);
            return TsvTable.Parse(path, string.Join('\n', lines) + "\n");
        });

    private static IReadOnlyList<TsvTable> Replace(
        IEnumerable<TsvTable> tables, string path, Func<TsvTable, TsvTable> replace)
        => tables.Select(table => table.SourcePath == path ? replace(table) : table).ToArray();

    private static TsvTable DropLast(TsvTable table)
        => TsvTable.Parse(table.SourcePath,
            string.Join('\n', table.ToCanonicalTsv().TrimEnd('\n').Split('\n')[..^1]) + "\n");
}
