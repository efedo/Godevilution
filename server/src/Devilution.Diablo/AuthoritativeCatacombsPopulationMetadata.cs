using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit native population rule for one quest-disabled original Catacombs depth. */
public sealed record AuthoritativeCatacombsPopulationDepthRule(
    int Depth,
    int MonsterDensityDivisor,
    int MultiplayerMonsterMultiplierPercent,
    int TrapPercent,
    int OrdinaryMonsterCapacity);

/** Source identity for the original Catacombs geometry inputs detached from the archive. */
public sealed record AuthoritativeCatacombsSourceAsset(
    string SourcePath, int SourceByteCount, string Sha256);

/**
 * Detached, standalone metadata for the original Catacombs population family.
 * This catalog intentionally does not create Cathedral generation parameters.
 */
public sealed class AuthoritativeCatacombsPopulationMetadata
{
    public const string SchemaId = "godevilution.catacombs-population-metadata";
    public const string SchemaVersion = "1.0.0";
    public const string CatalogId = "godevilution.catacombs-population-metadata";
    public const string Policy = "quest-disabled-original-diablo-single-player-normal-catacombs";
    public const int NativeTilRecordCount = 160;
    public const int NativeTilSourceByteCount = NativeTilRecordCount * sizeof(ushort) * 4;
    public const int NativeSolSourceByteCount = 559;
    public const string StorySourcePath = "txtdata/text/textdat.tsv";
    public const string DepthTableName = "catacombs_population_depth_rules.tsv";
    public const string SourceTableName = "catacombs_population_sources.tsv";
    public const string StoryTableName = "catacombs_population_story.tsv";
    public const string ShrineTableName = AuthoritativeCathedralPopulationMetadata.ShrineTableName;
    public const string EffectiveSolTableName = "catacombs_population_sol_properties.tsv";

    private static readonly string[] DepthColumnArray = [
        "depth", "policy", "monster_density_divisor", "mp_monster_multiplier_percent",
        "trap_percent", "ordinary_monster_capacity",
    ];
    private static readonly string[] SourceColumnArray = ["source_path", "source_byte_count", "sha256"];
    private static readonly string[] StoryColumnArray = ["story_index", "message_id"];
    private static readonly string[] ShrineColumnArray = ["native_shrine_id", "single_player_available"];
    private static readonly string[] EffectiveSolColumnArray = ["piece_id", "effective_flags"];
    private static readonly string[] RequiredSourcePaths = [
        "levels/l2data/l2.til",
        "levels/l2data/l2.sol",
        StorySourcePath,
    ];
    private static readonly int[] RequiredStoryMessages = [250, 253, 256];
    private readonly IReadOnlyList<TsvTable> tables;

    private AuthoritativeCatacombsPopulationMetadata(
        ContentPack pack,
        AuthoritativeNativeDunGeometryCatalog geometry,
        AuthoritativeCathedralPopulationCatalog population,
        AuthoritativeCathedralUniqueMonsterCatalog uniqueMonsters,
        AuthoritativeCathedralThemeItemMetadata items,
        IReadOnlyList<AuthoritativeCatacombsPopulationDepthRule> depthRules,
        IReadOnlyList<int> storyMessages,
        IReadOnlyList<bool> shrineAvailable,
        IReadOnlyList<byte> effectivePieceProperties,
        IReadOnlyList<AuthoritativeCatacombsSourceAsset> sourceAssets)
    {
        PackId = pack.Id;
        PackVersion = pack.Version;
        Geometry = geometry;
        Population = population;
        UniqueMonsters = uniqueMonsters;
        Items = items;
        DepthRules = Array.AsReadOnly(depthRules.ToArray());
        StoryMessages = Array.AsReadOnly(storyMessages.ToArray());
        ShrineAvailable = Array.AsReadOnly(shrineAvailable.ToArray());
        EffectivePieceProperties = Array.AsReadOnly(effectivePieceProperties.ToArray());
        SourceAssets = Array.AsReadOnly(sourceAssets.ToArray());
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
    public AuthoritativeNativeDunGeometryCatalog Geometry { get; }
    public AuthoritativeCathedralPopulationCatalog Population { get; }
    public AuthoritativeCathedralUniqueMonsterCatalog UniqueMonsters { get; }
    public AuthoritativeCathedralThemeItemMetadata Items { get; }
    public IReadOnlyList<AuthoritativeCatacombsPopulationDepthRule> DepthRules { get; }
    public IReadOnlyList<int> StoryMessages { get; }
    public IReadOnlyList<bool> ShrineAvailable { get; }
    /** Effective 1379-byte SOL channel; indices 559..1378 are explicit zero padding. */
    public IReadOnlyList<byte> EffectivePieceProperties { get; }
    public IReadOnlyList<AuthoritativeCatacombsSourceAsset> SourceAssets { get; }
    public int SolSourceByteCount => SourceAssets.Single(source => source.SourcePath == "levels/l2data/l2.sol").SourceByteCount;
    public IReadOnlyList<TsvTable> Tables => Array.AsReadOnly(tables.Select(CloneTable).ToArray());
    public static IReadOnlyList<string> DepthColumns => Array.AsReadOnly(DepthColumnArray);
    public static IReadOnlyList<string> SourceColumns => Array.AsReadOnly(SourceColumnArray);
    public static IReadOnlyList<string> StoryColumns => Array.AsReadOnly(StoryColumnArray);
    public static IReadOnlyList<string> ShrineColumns => Array.AsReadOnly(ShrineColumnArray);
    public static IReadOnlyList<string> EffectiveSolColumns => Array.AsReadOnly(EffectiveSolColumnArray);

    public static IReadOnlyList<string> TableNames => Array.AsReadOnly(new[] {
        AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
        AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
        AuthoritativeCathedralPopulationCatalog.MonsterTableName,
        AuthoritativeCathedralPopulationCatalog.ObjectTableName,
        AuthoritativeCathedralUniqueMonsterCatalog.TableName,
        DepthTableName, StoryTableName, ShrineTableName, EffectiveSolTableName, SourceTableName,
    }.Concat(AuthoritativeCathedralThemeItemMetadata.TableNames).Order(StringComparer.Ordinal).ToArray());

    /** Validates a complete standalone Catacombs metadata pack and detaches its tables. */
    public static AuthoritativeCatacombsPopulationMetadata Load(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (pack.Id != CatalogId || pack.Version != SchemaVersion || pack.Schema is null || pack.Schema.Id != SchemaId
            || pack.Schema.Version.ToString() != SchemaVersion
            || pack.Dependencies.Count != 0 || pack.DependencyConstraints.Count != 0
            || pack.Overlays.Count != 0 || pack.Assets.Count != 0)
            throw new InvalidDataException("A standalone version-1 Catacombs population metadata pack is required.");
        if (!pack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal)
                .SequenceEqual(TableNames, StringComparer.Ordinal))
            throw new InvalidDataException("The Catacombs population metadata pack has missing or unknown tables.");

        var copied = new ContentPack(pack.Id, pack.Version, pack.Tables.Select(CloneTable).ToArray(),
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));
        var byPath = copied.Tables.ToDictionary(table => table.SourcePath, StringComparer.Ordinal);
        var pieces = byPath[AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath];
        var properties = byPath[AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath];
        AuthoritativeDunPieceCatalog.ValidateExternalizedProduction(pieces);
        AuthoritativeDunTilePropertyCatalog.ValidateExternalizedProduction(properties);
        RequireOrderedIds(pieces, "mega_tile_id", NativeTilRecordCount);
        RequireOrderedIds(properties, "piece_id", AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        foreach (var row in properties.Rows)
            foreach (var column in properties.Columns.Skip(1))
                _ = row.RequiredBoolean(column);
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            pieces.SourcePath, pieces.ToCanonicalTsv(), properties.SourcePath, properties.ToCanonicalTsv());

        var sourceAssets = ParseSourceAssets(byPath[SourceTableName]);
        var effectiveTable = byPath[EffectiveSolTableName];
        RequireColumns(effectiveTable, EffectiveSolColumnArray);
        RequireOrderedIds(effectiveTable, "piece_id", AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        var effectiveFlags = effectiveTable.Rows.Select(row => {
            var flags = row.RequiredInt32("effective_flags");
            if (flags is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException("Catacombs raw SOL properties must fit an unsigned byte.");
            return (byte)flags;
        }).ToArray();
        if (effectiveFlags.Skip(NativeSolSourceByteCount).Any(value => value != 0))
            throw new InvalidDataException("Catacombs SOL padding after the 559 source bytes must be all zero.");
        var namedFlags = geometry.TileProperties.Definitions;
        for (var index = 0; index < effectiveFlags.Length; index++)
            if ((effectiveFlags[index] & ~0x40) != (byte)namedFlags[index].Flags)
                throw new InvalidDataException("Catacombs raw SOL and named geometry properties disagree.");
        ValidateReconstructedSourceHashes(sourceAssets, geometry, effectiveFlags);

        var population = AuthoritativeCathedralPopulationCatalog.LoadTables(
            byPath[AuthoritativeCathedralPopulationCatalog.MonsterTableName],
            byPath[AuthoritativeCathedralPopulationCatalog.ObjectTableName]);
        var uniqueMonsters = AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(
            byPath[AuthoritativeCathedralUniqueMonsterCatalog.TableName]);
        var items = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.TableNames.Select(path => byPath[path]));

        var depthTable = byPath[DepthTableName];
        RequireColumns(depthTable, DepthColumnArray);
        RequireOrderedIds(depthTable, "depth", 4, 5);
        var depthRules = depthTable.Rows.Select((row, index) => {
            var rule = new AuthoritativeCatacombsPopulationDepthRule(
                row.RequiredInt32("depth"),
                row.RequiredInt32("monster_density_divisor"),
                row.RequiredInt32("mp_monster_multiplier_percent"),
                row.RequiredInt32("trap_percent"),
                row.RequiredInt32("ordinary_monster_capacity"));
            var expectedTrapPercent = index < 2 ? 20 : 25;
            if (row.Required("policy") != Policy || rule.MonsterDensityDivisor != 30
                || rule.MultiplayerMonsterMultiplierPercent != 150
                || rule.TrapPercent != expectedTrapPercent
                || rule.OrdinaryMonsterCapacity != AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity)
                throw new InvalidDataException("Unsupported quest-disabled Catacombs level-generation rule.");
            return rule;
        }).ToArray();

        var storyTable = byPath[StoryTableName];
        RequireColumns(storyTable, StoryColumnArray);
        RequireOrderedIds(storyTable, "story_index", 3);
        var storyMessages = storyTable.Rows.Select(row => row.RequiredInt32("message_id")).ToArray();
        if (!storyMessages.SequenceEqual(RequiredStoryMessages))
            throw new InvalidDataException("Catacombs story books must use the native L8 text column IDs 250, 253, and 256.");

        var shrineTable = byPath[ShrineTableName];
        RequireColumns(shrineTable, ShrineColumnArray);
        RequireOrderedIds(shrineTable, "native_shrine_id", 26);
        var shrineAvailable = shrineTable.Rows.Select(row => row.RequiredBoolean("single_player_available")).ToArray();
        if (!shrineAvailable.Any(value => value))
            throw new InvalidDataException("Ordinary Catacombs shrine generation requires an available row.");

        return new(copied, geometry, population, uniqueMonsters, items, depthRules,
            storyMessages, shrineAvailable, effectiveFlags, sourceAssets);
    }

    public ContentPack ToContentPack()
        => new(PackId, PackVersion, tables.Select(CloneTable).ToArray(),
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));

    /** Builds the same detached schema from typed native metadata; inputs contain no generated world state. */
    public static IReadOnlyList<TsvTable> CreateTables(
        AuthoritativeNativeDunGeometryCatalog geometry,
        IReadOnlyList<byte> effectivePieceProperties,
        string tilSha256,
        string solSha256,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> monsters,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
        IReadOnlyList<AuthoritativeCathedralUniqueDefinition> uniqueMonsters,
        AuthoritativeCathedralThemeItemCatalog items,
        IReadOnlyList<AuthoritativeCatacombsPopulationDepthRule> depthRules,
        IReadOnlyList<int> storyMessages,
        int storyTextSourceByteCount,
        string storyTextSourceSha256,
        IReadOnlyList<bool> shrineAvailable)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(effectivePieceProperties);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(depthRules);
        ArgumentNullException.ThrowIfNull(storyMessages);
        ArgumentNullException.ThrowIfNull(shrineAvailable);
        if (storyTextSourceByteCount <= 0 || !IsLowerSha256(storyTextSourceSha256))
            throw new InvalidDataException("Catacombs story metadata must bind the exact canonical text table size and lowercase SHA-256.");

        var result = new List<TsvTable>(AuthoritativeCathedralThemeItemMetadata.CreateTables(items));
        result.Add(BuildTable(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            ["mega_tile_id", "piece_north_west", "piece_north_east", "piece_south_west", "piece_south_east"],
            geometry.Pieces.Definitions.Select(row => new object[] {
                row.MegaTileId, row.NorthWestPieceId, row.NorthEastPieceId, row.SouthWestPieceId, row.SouthEastPieceId,
            })));
        result.Add(BuildTable(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            ["piece_id", "solid", "block_light", "block_missile", "transparent", "transparent_left", "transparent_right", "trap"],
            geometry.TileProperties.Definitions.Select(row => new object[] {
                row.PieceId,
                row.Flags.HasFlag(AuthoritativeDunTileFlags.Solid),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.BlockLight),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.Transparent),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.TransparentLeft),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.TransparentRight),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.Trap),
            })));
        result.Add(BuildTable(EffectiveSolTableName, EffectiveSolColumnArray,
            effectivePieceProperties.Select((value, index) => new object[] { index, value })));
        result.Add(BuildTable(SourceTableName, SourceColumnArray, new[] {
            new object[] { RequiredSourcePaths[0], NativeTilSourceByteCount, tilSha256 },
            new object[] { RequiredSourcePaths[1], NativeSolSourceByteCount, solSha256 },
            new object[] { RequiredSourcePaths[2], storyTextSourceByteCount, storyTextSourceSha256 },
        }));
        result.Add(BuildTable(AuthoritativeCathedralPopulationCatalog.MonsterTableName,
            AuthoritativeCathedralPopulationCatalog.MonsterColumns, monsters.Select(raw => {
                var row = raw.PopulationDefinition;
                return new object[] {
                    raw.NativeMonsterId, (int)raw.Availability, raw.Image, raw.MinDungeonLevel, raw.MaxDungeonLevel,
                    row.IsSkeleton ? 1 : 0, row.MinimumHitPoints, row.MaximumHitPoints, row.Ai, row.Intelligence,
                    row.AbilityFlags, row.MinDamage, row.MaxDamage, row.MinDamageSpecial, row.MaxDamageSpecial,
                    row.ArmorClass, row.Resistance, row.ReducePlayerStrength, row.ReducePlayerMagic,
                    row.ReducePlayerDexterity, row.ReducePlayerVitality, row.ReducePlayerMaxHP,
                    row.ReducePlayerMaxMana, raw.HasSpecial ? 1 : 0, raw.Width,
                }.Concat(raw.Frames.Select(value => (object)value))
                    .Concat(raw.Rates.Select(value => (object)value)).ToArray();
            })));
        result.Add(BuildTable(AuthoritativeCathedralPopulationCatalog.ObjectTableName,
            AuthoritativeCathedralPopulationCatalog.ObjectColumns, objects.Select(row => new object[] {
                row.NativeType, row.Flags, row.AnimDelay, row.AnimLength, row.AnimWidth, row.SelectionRegion,
            })));
        result.Add(BuildTable(AuthoritativeCathedralUniqueMonsterCatalog.TableName,
            AuthoritativeCathedralUniqueMonsterCatalog.Columns, uniqueMonsters.Select(row => new object[] {
                row.NativeUniqueId, row.MonsterType, row.DungeonLevel, row.MaximumHitPoints, row.Ai,
                row.Intelligence, row.MinDamage, row.MaxDamage, row.Resistance, row.ReducePlayerStrength,
                row.ReducePlayerMagic, row.ReducePlayerDexterity, row.ReducePlayerVitality,
                row.ReducePlayerMaxHP, row.ReducePlayerMaxMana, (int)row.MonsterPack,
                row.CustomToHit, row.CustomArmorClass, row.TalkMessage,
            })));
        result.Add(BuildTable(DepthTableName, DepthColumnArray, depthRules.Select(row => new object[] {
            row.Depth, Policy, row.MonsterDensityDivisor, row.MultiplayerMonsterMultiplierPercent,
            row.TrapPercent, row.OrdinaryMonsterCapacity,
        })));
        result.Add(BuildTable(StoryTableName, StoryColumnArray,
            storyMessages.Select((value, index) => new object[] { index, value })));
        result.Add(BuildTable(ShrineTableName, ShrineColumnArray,
            shrineAvailable.Select((value, index) => new object[] { index, value })));

        return Load(new ContentPack(CatalogId, SchemaVersion, result,
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion))).ToContentPack().Tables;
    }

    public AuthoritativeCatacombsPopulationDepthRule RuleForDepth(int depth)
        => depth is >= 5 and <= 8 ? DepthRules[depth - 5]
            : throw new InvalidDataException("Only ordinary Catacombs depths 5-8 are supported.");

    private static IReadOnlyList<AuthoritativeCatacombsSourceAsset> ParseSourceAssets(TsvTable table)
    {
        RequireColumns(table, SourceColumnArray);
        if (table.Rows.Count != RequiredSourcePaths.Length)
            throw new InvalidDataException("Catacombs source manifest must identify exactly the TIL, SOL, and text-table inputs.");
        var result = new AuthoritativeCatacombsSourceAsset[RequiredSourcePaths.Length];
        for (var index = 0; index < result.Length; index++) {
            var row = table.Rows[index];
            var sourcePath = row.Required("source_path");
            var byteCount = row.RequiredInt32("source_byte_count");
            var sha256 = row.Required("sha256");
            var invalidSize = index switch {
                0 => byteCount != NativeTilSourceByteCount,
                1 => byteCount != NativeSolSourceByteCount,
                _ => byteCount <= 0,
            };
            if (sourcePath != RequiredSourcePaths[index] || invalidSize
                || !IsLowerSha256(sha256))
                throw new InvalidDataException("Catacombs source assets must retain canonical paths, sizes, and lowercase SHA-256 identities.");
            result[index] = new(sourcePath, byteCount, sha256);
        }
        return Array.AsReadOnly(result);
    }

    private static void ValidateReconstructedSourceHashes(
        IReadOnlyList<AuthoritativeCatacombsSourceAsset> sourceAssets,
        AuthoritativeNativeDunGeometryCatalog geometry,
        IReadOnlyList<byte> effectiveSol)
    {
        var tilBytes = new byte[NativeTilSourceByteCount];
        for (var index = 0; index < geometry.Pieces.Definitions.Count; index++) {
            var piece = geometry.Pieces.Definitions[index];
            var offset = index * sizeof(ushort) * 4;
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(offset), checked((ushort)piece.NorthWestPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(offset + 2), checked((ushort)piece.NorthEastPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(offset + 4), checked((ushort)piece.SouthWestPieceId));
            BinaryPrimitives.WriteUInt16LittleEndian(tilBytes.AsSpan(offset + 6), checked((ushort)piece.SouthEastPieceId));
        }
        var tilHash = Convert.ToHexString(SHA256.HashData(tilBytes)).ToLowerInvariant();
        var solHash = Convert.ToHexString(SHA256.HashData(effectiveSol.Take(NativeSolSourceByteCount).ToArray()))
            .ToLowerInvariant();
        if (!string.Equals(sourceAssets[0].Sha256, tilHash, StringComparison.Ordinal)
            || !string.Equals(sourceAssets[1].Sha256, solHash, StringComparison.Ordinal))
            throw new InvalidDataException("Catacombs TIL and SOL source hashes must match the reconstructed semantic inputs.");
    }

    private static bool IsLowerSha256(string? value)
        => value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static TsvTable CloneTable(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Columns.Any(column => string.IsNullOrWhiteSpace(column) || column != column.Trim())
            || table.Columns.Distinct(StringComparer.Ordinal).Count() != table.Columns.Count
            || table.Rows.Any(row => row is null || row.Values.Count != table.Columns.Count
                || !row.Values.Keys.Order(StringComparer.Ordinal).SequenceEqual(table.Columns.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || row.Values.Values.Any(value => value is null || value.IndexOfAny(['\t', '\r', '\n']) >= 0)))
            throw new InvalidDataException("Raw Catacombs metadata rows must match their exact, representable TSV schema.");
        return TsvTable.Parse(table.SourcePath, table.ToCanonicalTsv());
    }

    private static TsvTable BuildTable(string path, IReadOnlyList<string> columns, IEnumerable<object[]> rows)
    {
        var text = new StringBuilder(string.Join('\t', columns)).Append('\n');
        foreach (var row in rows) {
            if (row.Length != columns.Count)
                throw new InvalidDataException("Raw Catacombs metadata serialization has an invalid row width.");
            text.AppendJoin('\t', row.Select(value => value is bool flag ? flag ? "true" : "false"
                : Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? throw new InvalidDataException("Null Catacombs metadata cell."))).Append('\n');
        }
        return TsvTable.Parse(path, text.ToString());
    }

    private static void RequireColumns(TsvTable table, IReadOnlyList<string> columns)
    {
        if (!table.Columns.SequenceEqual(columns, StringComparer.Ordinal))
            throw new InvalidDataException($"{table.SourcePath} must use the complete ordered Catacombs schema.");
    }

    private static void RequireOrderedIds(TsvTable table, string column, int count, int firstId = 0)
    {
        if (table.Rows.Count != count || table.Rows.Where((row, index) =>
                row.RequiredInt32(column) != index + firstId).Any())
            throw new InvalidDataException($"{table.SourcePath} requires all {count} ordered native rows.");
    }
}
