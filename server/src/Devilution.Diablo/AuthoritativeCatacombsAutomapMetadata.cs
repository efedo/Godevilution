using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** One entry in the complete L2 AutomapTypeTiles projection. */
public readonly record struct AuthoritativeCatacombsAutomapDefinition(
    byte Id,
    AuthoritativeCathedralAutomapTileType Type,
    AuthoritativeCathedralAutomapTileFlags Flags);

/** One packed uint16 source row from the original Catacombs l2.amp file. */
public readonly record struct AuthoritativeCatacombsAutomapSourceWord(
    int SourceIndex, ushort PackedWord);

/**
 * Detached native Catacombs automap data. The source has 160 words; InitAutomap
 * places them at runtime IDs 1..160 and overrides source row 41. The compiled
 * cold projection keeps ID 0 implicit/default and emits default rows for 161..255,
 * but a hot native level can retain stale tail values. Generated Catacombs
 * geometry is therefore restricted to IDs 1..160; ID 0 remains the implicit
 * cold entry and IDs above 160 are not claimed as native hot-level parity.
 */
public sealed class AuthoritativeCatacombsAutomapMetadata
{
    public const string SchemaId = "godevilution.catacombs-automap-metadata";
    public const string SchemaVersion = "1.0.0";
    public const string CatalogId = "godevilution.catacombs-automap";
    public const string DefinitionsTableName = "catacombs_automap.tsv";
    public const string SourceWordsTableName = "catacombs_automap_source_words.tsv";
    public const string SourceAssetTableName = "catacombs_automap_source_asset.tsv";
    public const int DefinitionCount = 256;
    public const int SourceWordCount = 160;
    public const int MaximumGeneratedTileId = SourceWordCount;
    public const string SourcePath = "levels/l2data/l2.amp";

    private static readonly string[] DefinitionColumnsArray = ["id", "type", "flags"];
    private static readonly string[] SourceWordColumnsArray = ["source_index", "packed_word"];
    private static readonly string[] SourceAssetColumnsArray = ["source_path", "source_byte_count", "sha256"];
    private readonly ReadOnlyCollection<AuthoritativeCatacombsAutomapDefinition> definitions;
    private readonly ReadOnlyCollection<AuthoritativeCatacombsAutomapSourceWord> sourceWords;
    private readonly ReadOnlyCollection<TsvTable> tables;
    private readonly AuthoritativeNativeAutomapExploration explorationProjector;

    private AuthoritativeCatacombsAutomapMetadata(
        ContentPack pack,
        IReadOnlyList<AuthoritativeCatacombsAutomapDefinition> definitions,
        IReadOnlyList<AuthoritativeCatacombsAutomapSourceWord> sourceWords)
    {
        PackId = pack.Id;
        PackVersion = pack.Version;
        this.definitions = Array.AsReadOnly(definitions.ToArray());
        explorationProjector = new(this.definitions.Select(value =>
            new AuthoritativeCathedralAutomapTileDefinition(value.Id, value.Type, value.Flags)),
            MaximumGeneratedTileId, "Catacombs");
        this.sourceWords = Array.AsReadOnly(sourceWords.ToArray());
        tables = Array.AsReadOnly(pack.Tables.OrderBy(table => table.SourcePath, StringComparer.Ordinal)
            .Select(CloneTable).ToArray());
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(
            tables.Select(table => table.SourcePath + "\n" + table.ToCanonicalTsv())))));
        ManifestHash = new ContentManifest(pack.Id, pack.Version, [ToContentPack()]).Sha256;
    }

    public string PackId { get; }
    public string PackVersion { get; }
    public string Fingerprint { get; }
    public string ManifestHash { get; }
    public IReadOnlyList<AuthoritativeCatacombsAutomapDefinition> Definitions => definitions;
    public IReadOnlyList<AuthoritativeCatacombsAutomapSourceWord> SourceWords => sourceWords;
    public IReadOnlyList<TsvTable> Tables => Array.AsReadOnly(tables.Select(CloneTable).ToArray());
    public static IReadOnlyList<string> DefinitionColumns => Array.AsReadOnly(DefinitionColumnsArray);
    public static IReadOnlyList<string> SourceWordColumns => Array.AsReadOnly(SourceWordColumnsArray);
    public static IReadOnlyList<string> SourceAssetColumns => Array.AsReadOnly(SourceAssetColumnsArray);
    public static IReadOnlyList<string> TableNames => Array.AsReadOnly(new[] {
        DefinitionsTableName, SourceWordsTableName, SourceAssetTableName,
    }.Order(StringComparer.Ordinal).ToArray());

    public static AuthoritativeCatacombsAutomapMetadata Load(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (pack.Id != CatalogId || pack.Version != SchemaVersion
            || pack.Schema is null || pack.Schema.Id != SchemaId
            || pack.Schema.Version.ToString() != SchemaVersion
            || pack.Dependencies.Count != 0 || pack.DependencyConstraints.Count != 0
            || pack.Overlays.Count != 0 || pack.Assets.Count != 0
            || !pack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal)
                .SequenceEqual(TableNames, StringComparer.Ordinal))
            throw new InvalidDataException("A standalone version-1 Catacombs automap metadata pack is required.");

        var copied = new ContentPack(pack.Id, pack.Version, pack.Tables.Select(CloneTable).ToArray(),
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));
        var byPath = copied.Tables.ToDictionary(table => table.SourcePath, StringComparer.Ordinal);
        var sourceWords = ParseSourceWords(byPath[SourceWordsTableName]);
        ValidateSourceAsset(byPath[SourceAssetTableName], sourceWords);
        var definitions = ParseDefinitions(byPath[DefinitionsTableName]);
        var expected = Expand(sourceWords);
        if (!definitions.SequenceEqual(expected))
            throw new InvalidDataException("Catacombs automap definitions do not match native source ordering and overrides.");
        return new(copied, definitions, sourceWords);
    }

    public ContentPack ToContentPack()
        => new(PackId, PackVersion, tables.Select(CloneTable).ToArray(),
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));

    /** Builds the exact 256-row cold projection from the 160 packed native source words. */
    public static IReadOnlyList<TsvTable> CreateTables(IReadOnlyList<ushort> packedSourceWords, string sourceSha256)
    {
        ArgumentNullException.ThrowIfNull(packedSourceWords);
        if (packedSourceWords.Count != SourceWordCount || !IsLowerSha256(sourceSha256))
            throw new InvalidDataException("L2 automap metadata requires 160 source words and a lowercase SHA-256.");
        var rows = packedSourceWords.Select((word, index) =>
            new AuthoritativeCatacombsAutomapSourceWord(index, word)).ToArray();
        var definitions = Expand(rows);
        var sourceTable = BuildTable(SourceWordsTableName, SourceWordColumnsArray,
            rows.Select(row => new object[] { row.SourceIndex, row.PackedWord }));
        var definitionTable = BuildTable(DefinitionsTableName, DefinitionColumnsArray,
            definitions.Select(row => new object[] { row.Id, (byte)row.Type, (byte)row.Flags }));
        var sourceAssetTable = BuildTable(SourceAssetTableName, SourceAssetColumnsArray,
            [new object[] { SourcePath, SourceWordCount * sizeof(ushort), sourceSha256 }]);
        return Load(new ContentPack(CatalogId, SchemaVersion,
            [definitionTable, sourceTable, sourceAssetTable],
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion))).ToContentPack().Tables;
    }

    public AuthoritativeCatacombsAutomapDefinition Get(int tileId)
    {
        if ((uint)tileId >= DefinitionCount)
            throw new InvalidDataException($"Catacombs automap tile ID {tileId} is outside uint8.");
        return definitions[tileId];
    }

    /** Resolves implicit cold ID 0 or supported generated Catacombs IDs 1..160. */
    public AuthoritativeCatacombsAutomapDefinition ResolveGeneratedTile(ushort tileId)
    {
        if (tileId > MaximumGeneratedTileId)
            throw new InvalidDataException("Catacombs automap IDs must be implicit cold ID 0 or generated ID 1..160.");
        return definitions[tileId];
    }

    /** Applies native exploration to an independently validated generated L2 coarse map. */
    public void ProjectExploration(IReadOnlyList<ushort> coarseTiles,
        int positionX, int positionY, byte explorer, Span<byte> exploration)
        => explorationProjector.Project(coarseTiles, positionX, positionY, explorer, exploration);

    public void ProjectExploration(IReadOnlyList<ushort> coarseTiles,
        int positionX, int positionY, byte explorer, byte[] exploration)
    {
        ArgumentNullException.ThrowIfNull(exploration);
        ProjectExploration(coarseTiles, positionX, positionY, explorer, exploration.AsSpan());
    }

    public IReadOnlyList<byte> ProjectExploration(IReadOnlyList<ushort> coarseTiles,
        int positionX, int positionY, byte explorer)
    {
        var result = new byte[1600];
        ProjectExploration(coarseTiles, positionX, positionY, explorer, result);
        return Array.AsReadOnly(result);
    }

    /** Coarse-coordinate counterpart for native automap consumers and qualification. */
    public void ProjectExplorationAtMap(IReadOnlyList<ushort> coarseTiles,
        int mapX, int mapY, byte explorer, Span<byte> exploration)
        => explorationProjector.ProjectAtMap(coarseTiles, mapX, mapY, explorer, exploration);

    public IReadOnlyList<byte> ProjectExplorationAtMap(IReadOnlyList<ushort> coarseTiles,
        int mapX, int mapY, byte explorer)
    {
        var result = new byte[1600];
        ProjectExplorationAtMap(coarseTiles, mapX, mapY, explorer, result.AsSpan());
        return Array.AsReadOnly(result);
    }

    private static AuthoritativeCatacombsAutomapDefinition[] ParseDefinitions(TsvTable table)
    {
        RequireColumns(table, DefinitionColumnsArray);
        if (table.Rows.Count != DefinitionCount)
            throw new InvalidDataException("Catacombs automap must contain exactly 256 ordered definitions.");
        var result = new AuthoritativeCatacombsAutomapDefinition[DefinitionCount];
        for (var index = 0; index < result.Length; index++) {
            var row = table.Rows[index];
            if (row.RequiredInt32("id") != index)
                throw new InvalidDataException("Catacombs automap definitions must retain exact 0..255 order.");
            var type = ParseByte(row, "type");
            var flags = ParseByte(row, "flags");
            if (type > AuthoritativeCathedralAutomapMetadata.MaximumType)
                throw new InvalidDataException("Catacombs automap contains an unsupported native tile type.");
            result[index] = new((byte)index,
                (AuthoritativeCathedralAutomapTileType)type,
                (AuthoritativeCathedralAutomapTileFlags)flags);
        }
        return result;
    }

    private static AuthoritativeCatacombsAutomapSourceWord[] ParseSourceWords(TsvTable table)
    {
        RequireColumns(table, SourceWordColumnsArray);
        if (table.Rows.Count != SourceWordCount)
            throw new InvalidDataException("L2 AMP source metadata must contain exactly 160 ordered words.");
        var result = new AuthoritativeCatacombsAutomapSourceWord[SourceWordCount];
        for (var index = 0; index < result.Length; index++) {
            var row = table.Rows[index];
            if (row.RequiredInt32("source_index") != index)
                throw new InvalidDataException("L2 AMP words must retain their exact source order.");
            var word = row.RequiredInt32("packed_word");
            if (word is < ushort.MinValue or > ushort.MaxValue)
                throw new InvalidDataException("L2 AMP source words must be unsigned 16-bit values.");
            var nativeType = word & 0xFF;
            if (nativeType > AuthoritativeCathedralAutomapMetadata.MaximumType)
                throw new InvalidDataException("L2 AMP source word contains an unsupported native tile type.");
            result[index] = new(index, (ushort)word);
        }
        return result;
    }

    private static AuthoritativeCatacombsAutomapDefinition[] Expand(
        IReadOnlyList<AuthoritativeCatacombsAutomapSourceWord> sourceWords)
    {
        if (sourceWords.Count != SourceWordCount)
            throw new InvalidDataException("L2 AMP expansion requires exactly 160 ordered source words.");
        var result = new AuthoritativeCatacombsAutomapDefinition[DefinitionCount];
        for (var id = 0; id < result.Length; id++)
            result[id] = new((byte)id, AuthoritativeCathedralAutomapTileType.None,
                AuthoritativeCathedralAutomapTileFlags.None);
        for (var sourceIndex = 0; sourceIndex < sourceWords.Count; sourceIndex++) {
            var packed = sourceWords[sourceIndex].PackedWord;
            var type = (byte)(packed & 0xFF);
            var flags = (byte)(packed >> 8);
            if (sourceIndex == 41) {
                type = (byte)AuthoritativeCathedralAutomapTileType.FenceHorizontal;
                flags = 0;
            }
            result[sourceIndex + 1] = new((byte)(sourceIndex + 1),
                (AuthoritativeCathedralAutomapTileType)type,
                (AuthoritativeCathedralAutomapTileFlags)flags);
        }
        return result;
    }

    private static void ValidateSourceAsset(
        TsvTable table, IReadOnlyList<AuthoritativeCatacombsAutomapSourceWord> sourceWords)
    {
        RequireColumns(table, SourceAssetColumnsArray);
        var source = table.Rows.Count == 1 ? table.Rows[0] : null;
        var bytes = new byte[SourceWordCount * sizeof(ushort)];
        for (var index = 0; index < sourceWords.Count; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort)), sourceWords[index].PackedWord);
        var reconstructedSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (source is null || source.Required("source_path") != SourcePath
            || source.RequiredInt32("source_byte_count") != SourceWordCount * sizeof(ushort)
            || !IsLowerSha256(source.Required("sha256"))
            || !string.Equals(source.Required("sha256"), reconstructedSha256, StringComparison.Ordinal))
            throw new InvalidDataException("L2 AMP source identity must bind the 320-byte canonical input.");
    }

    private static bool IsLowerSha256(string? value)
        => value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static byte ParseByte(TsvRow row, string column)
    {
        var text = row.Required(column);
        if (!byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"Catacombs automap column '{column}' must be an unsigned byte.");
        return value;
    }

    private static TsvTable BuildTable(string path, IReadOnlyList<string> columns, IEnumerable<object[]> rows)
    {
        var text = new StringBuilder(string.Join('\t', columns)).Append('\n');
        foreach (var row in rows)
            text.AppendJoin('\t', row.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)
                ?? throw new InvalidDataException("Null Catacombs automap cell."))).Append('\n');
        return TsvTable.Parse(path, text.ToString());
    }

    private static void RequireColumns(TsvTable table, IReadOnlyList<string> columns)
    {
        if (!table.Columns.SequenceEqual(columns, StringComparer.Ordinal)
            || table.Rows.Any(row => row.Values.Count != columns.Count
                || !row.Values.Keys.Order(StringComparer.Ordinal).SequenceEqual(columns.Order(StringComparer.Ordinal), StringComparer.Ordinal)))
            throw new InvalidDataException($"{table.SourcePath} must use the exact ordered Catacombs automap schema.");
    }

    private static TsvTable CloneTable(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return TsvTable.Parse(table.SourcePath, table.ToCanonicalTsv());
    }
}
