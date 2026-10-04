using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit per-depth rules for ordinary original-Diablo Cathedral population. */
public sealed record AuthoritativeCathedralPopulationDepthRule(
    int Depth, int MonsterDensityDivisor, int TrapPercent, int OrdinaryMonsterCapacity);

/**
 * Complete, MPQ-free metadata pack for the controlled Cathedral compositor.
 * Geometry is the effective native TIL/SOL projection, not raw archive bytes.
 * No seeds, selected population, expected worlds or runtime activation are stored.
 */
public sealed class AuthoritativeCathedralPopulationMetadata
{
    public const string SchemaId = "godevilution.cathedral-population-metadata";
    public const string SchemaVersion = "1.0.0";
    public const string Policy = "quest-disabled-original-diablo-single-player-normal";
    public const int NativeTilRecordCount = 206;
    public const string DepthTableName = "cathedral_population_depth_rules.tsv";
    public const string StoryTableName = "cathedral_population_story.tsv";
    public const string ShrineTableName = "cathedral_population_shrines.tsv";
    public const string EffectiveSolTableName = "cathedral_population_sol_properties.tsv";

    private static readonly string[] DepthColumnArray = [
        "depth", "policy", "monster_density_divisor", "trap_percent", "ordinary_monster_capacity",
    ];
    private static readonly string[] StoryColumnArray = ["story_index", "message_id"];
    private static readonly string[] ShrineColumnArray = ["native_shrine_id", "single_player_available"];
    private static readonly string[] EffectiveSolColumnArray = ["piece_id", "effective_flags"];
    private readonly IReadOnlyList<TsvTable> tables;

    private AuthoritativeCathedralPopulationMetadata(ContentPack pack,
        AuthoritativeNativeDunGeometryCatalog geometry,
        AuthoritativeCathedralPopulationCatalog population,
        AuthoritativeCathedralUniqueMonsterCatalog uniqueMonsters,
        AuthoritativeCathedralThemeItemMetadata items,
        IReadOnlyList<AuthoritativeCathedralPopulationDepthRule> depthRules,
        IReadOnlyList<int> storyMessages, IReadOnlyList<bool> shrineAvailable,
        IReadOnlyList<byte> effectivePieceProperties)
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
        tables = Array.AsReadOnly(pack.Tables.OrderBy(table => table.SourcePath, StringComparer.Ordinal)
            .Select(CloneTable).ToArray());
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(
            tables.Select(table => table.SourcePath + "\n" + table.ToCanonicalTsv())))));
        ManifestHash = new ContentManifest(pack.Id, pack.Version, [ToContentPack()]).Sha256;
        PotionDefinitions = Array.AsReadOnly(items.Catalog.Items.Where(row => row.NativeItemIndex is 24 or 25)
            .Select(row => new AuthoritativeCathedralInitialItemDefinition(row.NativeItemIndex,
                row.ItemType, row.CursorGraphic, row.Name, row.Name, row.EquipLocation, row.ItemClass,
                row.MinimumDamage, row.MaximumDamage, row.MinimumArmorClass, row.MaximumArmorClass,
                row.Flags, row.MiscId, row.SpellId, row.Value, row.Durability,
                row.MinimumStrength, row.MinimumMagic, row.MinimumDexterity,
                row.AnimationFrameCount, row.AnimationTicksPerFrame)).ToArray());
    }

    public string PackId { get; }
    public string PackVersion { get; }
    public string Fingerprint { get; }
    public string ManifestHash { get; }
    public AuthoritativeNativeDunGeometryCatalog Geometry { get; }
    public AuthoritativeCathedralPopulationCatalog Population { get; }
    public AuthoritativeCathedralUniqueMonsterCatalog UniqueMonsters { get; }
    public AuthoritativeCathedralThemeItemMetadata Items { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationDepthRule> DepthRules { get; }
    public IReadOnlyList<int> StoryMessages { get; }
    public IReadOnlyList<bool> ShrineAvailable { get; }
    /** Full native byte channel, including the opaque 0x40 bit absent from named geometry flags. */
    public IReadOnlyList<byte> EffectivePieceProperties { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> PotionDefinitions { get; }
    public static IReadOnlyList<string> DepthColumns => Array.AsReadOnly(DepthColumnArray);
    public static IReadOnlyList<string> StoryColumns => Array.AsReadOnly(StoryColumnArray);
    public static IReadOnlyList<string> ShrineColumns => Array.AsReadOnly(ShrineColumnArray);
    public static IReadOnlyList<string> EffectiveSolColumns => Array.AsReadOnly(EffectiveSolColumnArray);
    public static IReadOnlyList<string> TableNames => Array.AsReadOnly(new[] {
        AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
        AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
        AuthoritativeCathedralPopulationCatalog.MonsterTableName,
        AuthoritativeCathedralPopulationCatalog.ObjectTableName,
        AuthoritativeCathedralUniqueMonsterCatalog.TableName,
        DepthTableName, StoryTableName, ShrineTableName, EffectiveSolTableName,
    }.Concat(AuthoritativeCathedralThemeItemMetadata.TableNames).Order(StringComparer.Ordinal).ToArray());

    /** Validates every required table; partial, overlaid or asset-bearing packs refuse. */
    public static AuthoritativeCathedralPopulationMetadata Load(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (pack.Schema is null || pack.Schema.Id != SchemaId || pack.Schema.Version.ToString() != SchemaVersion
            || pack.Dependencies.Count != 0 || pack.DependencyConstraints.Count != 0
            || pack.Overlays.Count != 0 || pack.Assets.Count != 0)
            throw new InvalidDataException("A standalone version-1 Cathedral population metadata pack is required.");
        var expected = TableNames;
        if (!pack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal)
                .SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("The Cathedral population metadata pack has missing or unknown tables.");
        // The generic table record exposes caller-owned collections. Detach before
        // validation and never return these retained tables to callers.
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
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(pieces.SourcePath, pieces.ToCanonicalTsv(),
            properties.SourcePath, properties.ToCanonicalTsv());
        var effectiveSol = byPath[EffectiveSolTableName];
        RequireColumns(effectiveSol, EffectiveSolColumnArray);
        RequireOrderedIds(effectiveSol, "piece_id", AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        var effectiveFlags = effectiveSol.Rows.Select(row => {
            var value = row.RequiredInt32("effective_flags");
            if (value is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException("Effective Cathedral SOL properties must retain uint8 flags.");
            return (byte)value;
        }).ToArray();
        var namedFlags = geometry.TileProperties.Definitions;
        for (var index = 0; index < effectiveFlags.Length; index++)
            if ((effectiveFlags[index] & ~0x40) != (byte)namedFlags[index].Flags)
                throw new InvalidDataException("Raw effective SOL and named geometry properties disagree.");
        var population = AuthoritativeCathedralPopulationCatalog.LoadTables(
            byPath[AuthoritativeCathedralPopulationCatalog.MonsterTableName],
            byPath[AuthoritativeCathedralPopulationCatalog.ObjectTableName]);
        var uniqueMonsters = AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(
            byPath[AuthoritativeCathedralUniqueMonsterCatalog.TableName]);
        var items = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.TableNames.Select(path => byPath[path]));

        var depths = byPath[DepthTableName];
        RequireColumns(depths, DepthColumnArray);
        RequireOrderedIds(depths, "depth", 4, 1);
        var rules = depths.Rows.Select(row => {
            var rule = new AuthoritativeCathedralPopulationDepthRule(row.RequiredInt32("depth"),
                row.RequiredInt32("monster_density_divisor"), row.RequiredInt32("trap_percent"),
                row.RequiredInt32("ordinary_monster_capacity"));
            if (row.Required("policy") != Policy || rule.MonsterDensityDivisor <= 0
                || rule.TrapPercent is < 0 or > 100
                || rule.OrdinaryMonsterCapacity != AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity)
                throw new InvalidDataException("Unsupported ordinary Cathedral population policy or rule.");
            return rule;
        }).ToArray();
        var stories = byPath[StoryTableName];
        RequireColumns(stories, StoryColumnArray);
        RequireOrderedIds(stories, "story_index", 3);
        var messages = stories.Rows.Select(row => row.RequiredInt32("message_id")).ToArray();
        if (messages.Any(value => value < 0))
            throw new InvalidDataException("Cathedral story message identities must be nonnegative.");
        var shrines = byPath[ShrineTableName];
        RequireColumns(shrines, ShrineColumnArray);
        RequireOrderedIds(shrines, "native_shrine_id", 26);
        var available = shrines.Rows.Select(row => row.RequiredBoolean("single_player_available")).ToArray();
        if (!available.Any(value => value))
            throw new InvalidDataException("Ordinary Cathedral shrine generation requires an available row.");
        if (items.Catalog.Items.Where(row => row.NativeItemIndex is 24 or 25)
                .Any(row => !row.Available || row.CursorGraphic == byte.MaxValue))
            throw new InvalidDataException("Initial Cathedral potions must be available animated native items.");
        return new(copied, geometry, population, uniqueMonsters, items, rules, messages, available, effectiveFlags);
    }

    /** Returns a detached pack suitable for publication, hashing or another validation pass. */
    public ContentPack ToContentPack() => new(PackId, PackVersion, tables.Select(CloneTable).ToArray(),
        schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));

    /** Exports only raw metadata, then validates the complete schema before returning it. */
    public static IReadOnlyList<TsvTable> CreateTables(
        AuthoritativeNativeDunGeometryCatalog geometry,
        IReadOnlyList<byte> effectivePieceProperties,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> monsters,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
        IReadOnlyList<AuthoritativeCathedralUniqueDefinition> uniqueMonsters,
        AuthoritativeCathedralThemeItemCatalog items,
        IReadOnlyList<AuthoritativeCathedralPopulationDepthRule> depthRules,
        IReadOnlyList<int> storyMessages, IReadOnlyList<bool> shrineAvailable)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(effectivePieceProperties);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        ArgumentNullException.ThrowIfNull(depthRules);
        ArgumentNullException.ThrowIfNull(storyMessages);
        ArgumentNullException.ThrowIfNull(shrineAvailable);
        var result = new List<TsvTable>(AuthoritativeCathedralThemeItemMetadata.CreateTables(items));
        result.Add(BuildTable(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            ["mega_tile_id", "piece_north_west", "piece_north_east", "piece_south_west", "piece_south_east"],
            geometry.Pieces.Definitions.Select(row => new object[] { row.MegaTileId, row.NorthWestPieceId,
                row.NorthEastPieceId, row.SouthWestPieceId, row.SouthEastPieceId })));
        result.Add(BuildTable(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            ["piece_id", "solid", "block_light", "block_missile", "transparent", "transparent_left", "transparent_right", "trap"],
            geometry.TileProperties.Definitions.Select(row => new object[] { row.PieceId,
                row.Flags.HasFlag(AuthoritativeDunTileFlags.Solid), row.Flags.HasFlag(AuthoritativeDunTileFlags.BlockLight),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile), row.Flags.HasFlag(AuthoritativeDunTileFlags.Transparent),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.TransparentLeft), row.Flags.HasFlag(AuthoritativeDunTileFlags.TransparentRight),
                row.Flags.HasFlag(AuthoritativeDunTileFlags.Trap) })));
        result.Add(BuildTable(EffectiveSolTableName, EffectiveSolColumnArray,
            effectivePieceProperties.Select((value, index) => new object[] { index, value })));
        result.Add(BuildTable(AuthoritativeCathedralPopulationCatalog.MonsterTableName,
            AuthoritativeCathedralPopulationCatalog.MonsterColumns, monsters.Select(raw => {
                var row = raw.PopulationDefinition;
                return new object[] { raw.NativeMonsterId, (int)raw.Availability, raw.Image, raw.MinDungeonLevel,
                    raw.MaxDungeonLevel, row.IsSkeleton ? 1 : 0, row.MinimumHitPoints, row.MaximumHitPoints, row.Ai,
                    row.Intelligence, row.AbilityFlags, row.MinDamage, row.MaxDamage, row.MinDamageSpecial,
                    row.MaxDamageSpecial, row.ArmorClass, row.Resistance, row.ReducePlayerStrength,
                    row.ReducePlayerMagic, row.ReducePlayerDexterity, row.ReducePlayerVitality,
                    row.ReducePlayerMaxHP, row.ReducePlayerMaxMana, raw.HasSpecial ? 1 : 0, raw.Width }
                    .Concat(raw.Frames.Select(value => (object)value)).Concat(raw.Rates.Select(value => (object)value)).ToArray();
            })));
        result.Add(BuildTable(AuthoritativeCathedralPopulationCatalog.ObjectTableName,
            AuthoritativeCathedralPopulationCatalog.ObjectColumns, objects.Select(row => new object[] {
                row.NativeType, row.Flags, row.AnimDelay, row.AnimLength, row.AnimWidth, row.SelectionRegion })));
        result.Add(BuildTable(AuthoritativeCathedralUniqueMonsterCatalog.TableName,
            AuthoritativeCathedralUniqueMonsterCatalog.Columns, uniqueMonsters.Select(row => new object[] {
                row.NativeUniqueId, row.MonsterType, row.DungeonLevel, row.MaximumHitPoints, row.Ai,
                row.Intelligence, row.MinDamage, row.MaxDamage, row.Resistance, row.ReducePlayerStrength,
                row.ReducePlayerMagic, row.ReducePlayerDexterity, row.ReducePlayerVitality,
                row.ReducePlayerMaxHP, row.ReducePlayerMaxMana, (int)row.MonsterPack,
                row.CustomToHit, row.CustomArmorClass, row.TalkMessage })));
        result.Add(BuildTable(DepthTableName, DepthColumnArray, depthRules.Select(row => new object[] {
            row.Depth, Policy, row.MonsterDensityDivisor, row.TrapPercent, row.OrdinaryMonsterCapacity })));
        result.Add(BuildTable(StoryTableName, StoryColumnArray, storyMessages.Select((value, index) => new object[] { index, value })));
        result.Add(BuildTable(ShrineTableName, ShrineColumnArray, shrineAvailable.Select((value, index) => new object[] { index, value })));
        return Load(new ContentPack(SchemaId, SchemaVersion, result,
            schema: new ContentSchemaIdentity(SchemaId, SchemaVersion))).ToContentPack().Tables;
    }

    /** Uses only loaded metadata and caller-supplied pre-selection world inputs. */
    public AuthoritativeCathedralInitialPopulationParameters CreateInitialPopulationParameters(
        int depth, uint populationSeed, uint storySeed,
        AuthoritativeCathedralPopulationGeometry geometry,
        AuthoritativeCathedralPopulationPlayer player, int nextTransparencyValue,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        int maximumPlacementAttempts = 1_000_000, int maximumThemeSelectionAttempts = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var rule = RuleForDepth(depth);
        if (!geometry.PieceProperties.SequenceEqual(EffectivePieceProperties))
            throw new InvalidDataException("Pre-selection geometry must use the pack's effective SOL properties.");
        return new(depth, populationSeed, storySeed, geometry, Population.Monsters, Population.Objects,
            player, rule.TrapPercent, StoryMessages, nextTransparencyValue, triggerPositions,
            maximumPlacementAttempts, maximumThemeSelectionAttempts);
    }

    public AuthoritativeCathedralPopulationDepthRule RuleForDepth(int depth)
        => depth is >= 1 and <= 4 ? DepthRules[depth - 1]
            : throw new InvalidDataException("Only ordinary Cathedral depths 1-4 are supported.");

    private static TsvTable CloneTable(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Columns.Any(column => string.IsNullOrWhiteSpace(column) || column != column.Trim())
            || table.Columns.Distinct(StringComparer.Ordinal).Count() != table.Columns.Count
            || table.Rows.Any(row => row is null || row.Values.Count != table.Columns.Count
                || !row.Values.Keys.Order(StringComparer.Ordinal).SequenceEqual(table.Columns.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || row.Values.Values.Any(value => value is null || value.IndexOfAny(['\t', '\r', '\n']) >= 0)))
            throw new InvalidDataException("Raw population metadata rows must match their exact, representable TSV schema.");
        return TsvTable.Parse(table.SourcePath, table.ToCanonicalTsv());
    }

    private static TsvTable BuildTable(string path, IReadOnlyList<string> columns, IEnumerable<object[]> rows)
    {
        var text = new StringBuilder(string.Join('\t', columns) + "\n");
        foreach (var row in rows) {
            if (row.Length != columns.Count)
                throw new InvalidDataException("Raw population metadata serialization has an invalid row width.");
            var values = row.Select(value => value is bool flag ? flag ? "true" : "false"
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new InvalidDataException("Null metadata cell."));
            text.AppendJoin('\t', values).Append('\n');
        }
        return TsvTable.Parse(path, text.ToString());
    }

    private static void RequireColumns(TsvTable table, IReadOnlyList<string> columns)
    {
        if (!table.Columns.SequenceEqual(columns, StringComparer.Ordinal))
            throw new InvalidDataException($"{table.SourcePath} must use the complete ordered metadata schema.");
    }

    private static void RequireOrderedIds(TsvTable table, string column, int count, int start = 0)
    {
        if (table.Rows.Count != count || table.Rows.Where((row, index) =>
                row.RequiredInt32(column) != index + start).Any())
            throw new InvalidDataException($"{table.SourcePath} requires all {count} ordered native rows.");
    }
}
