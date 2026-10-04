using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Devilution.Data;

namespace Devilution.Diablo;

/** One canonical raw metadata table schema used by the Cathedral item boundary. */
public sealed record AuthoritativeCathedralThemeItemTableSchema(
    string Path,
    IReadOnlyList<string> Columns);

/**
 * Complete detached raw metadata boundary for the original item tables.
 *
 * This boundary deliberately does not read MPQs or normalize item rows. The
 * catalog is constructed only after every required ordered table has been
 * checked, including cursor animation and glyph rows that existing catalog
 * callers may omit for convenience.
 */
public sealed class AuthoritativeCathedralThemeItemMetadata
{
    public const string ItemsTablePath = "cathedral_population_item_bases.tsv";
    public const string PrefixesTablePath = "cathedral_population_item_prefixes.tsv";
    public const string SuffixesTablePath = "cathedral_population_item_suffixes.tsv";
    public const string UniquesTablePath = "cathedral_population_item_uniques.tsv";
    public const string SpellsTablePath = "cathedral_population_item_spells.tsv";
    public const string CursorAnimationsTablePath = "cathedral_population_item_cursor_animations.tsv";
    public const string GlyphsTablePath = "cathedral_population_item_glyphs.tsv";
    public const string CursorsTablePath = "cathedral_population_item_cursors.tsv";
    public const string RulesTablePath = "cathedral_population_item_rules.tsv";
    public const string AlwaysMagicMiscTablePath = "cathedral_population_item_always_magic_misc.tsv";

    public const int ItemRowCount = 168;
    public const int PrefixRowCount = 83;
    public const int SuffixRowCount = 95;
    public const int UniqueRowCount = 90;
    public const int SpellRowCount = 37;
    private const int NativeSpellIdMax = 51;
    public const int CursorAnimationRowCount = 229;
    public const int CursorSentinelRowCount = 3;
    private const int NativeUniqueBaseItemIdMax = 69;
    // IMISC_ARENAPOT is the last named source misc value (0..55);
    // IMISC_INVALID=-1 is a sentinel, never a policy category.
    private const int MaximumDefinedMiscId = 55;
    private const int MaximumRawTextUtf8Bytes = 1 << 20;
    // The ordinary generator's SaveItemPower switch covers the original
    // Diablo effect identities below. Numeric gaps in the byte-sized enum are
    // not supported ordinary effects; retain only -1 as the inactive slot
    // sentinel in the flattened unique rows.
    private static readonly FrozenSet<int> SupportedPowerTypes = new[] {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        14, 15, 16, 17,
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
        41, 42, 43, 45, 46, 50, 52, 53,
        55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
        68, 69, 70, 72, 73, 75, 76, 77, 79, 80, 81, 82,
        84, 85, 86, 87, 88, 89, 90, 91, 92, 93,
    }.ToFrozenSet();
    private static readonly FrozenSet<int> SupportedToHitBonusLevels = new[] {
        -50, -25, 20, 36, 51, 66, 81, 96, 111, 126, 151,
    }.ToFrozenSet();

    private static readonly IReadOnlyList<AuthoritativeCathedralThemeItemTableSchema> Schemas =
        new ReadOnlyCollection<AuthoritativeCathedralThemeItemTableSchema>([
            Schema(ItemsTablePath,
                "native_item_index", "native_item_id", "drop_rate", "item_class", "equip_location",
                "cursor_graphic", "item_type", "name", "short_name", "minimum_monster_level",
                "durability", "minimum_damage", "maximum_damage", "minimum_armor_class",
                "maximum_armor_class", "minimum_strength", "minimum_magic", "minimum_dexterity",
                "flags", "misc_id", "spell_id", "usable", "value", "available"),
            Schema(PrefixesTablePath,
                "affix_index", "name", "power_type", "power_param1", "power_param2", "minimum_level",
                "item_type_flags", "alignment", "chance", "useful", "minimum_value", "maximum_value",
                "value_multiplier"),
            Schema(SuffixesTablePath,
                "affix_index", "name", "power_type", "power_param1", "power_param2", "minimum_level",
                "item_type_flags", "alignment", "chance", "useful", "minimum_value", "maximum_value",
                "value_multiplier"),
            Schema(UniquesTablePath,
                "native_unique_id", "name", "cursor_graphic", "native_base_item_id", "minimum_level",
                // This is a detached shape count for the six flattened slots,
                // not the native UINumPL presentation field.  Generation uses
                // the ordered slots through the first IPL_INVALID entry.
                "value", "power_count",
                "power1_type", "power1_param1", "power1_param2", "power2_type", "power2_param1",
                "power2_param2", "power3_type", "power3_param1", "power3_param2", "power4_type",
                "power4_param1", "power4_param2", "power5_type", "power5_param1", "power5_param2",
                "power6_type", "power6_param1", "power6_param2"),
            Schema(SpellsTablePath,
                "native_spell_id", "name", "book_level", "staff_level", "minimum_magic", "book_cost",
                "staff_cost", "magic_type", "staff_minimum_charges", "staff_maximum_charges",
                "available_in_book", "available_in_staff"),
            Schema(CursorAnimationsTablePath, "cursor_graphic", "frame_count", "ticks_per_frame"),
            Schema(GlyphsTablePath, "codepoint", "width"),
            Schema(CursorsTablePath, "book_red", "book_blue", "book_grey", "gold_small", "gold_medium", "gold_large"),
            Schema(RulesTablePath,
                "magic_chance_base", "magic_chance_per_level", "unique_chance_normal",
                "unique_chance_unique", "bonus_levels_unique", "prefix_percent", "suffix_percent",
                "only_good_chance", "no_drop_percent", "gold_percent"),
            Schema(AlwaysMagicMiscTablePath, "misc_id"),
        ]);

    private AuthoritativeCathedralThemeItemMetadata(
        IReadOnlyList<TsvTable> tables,
        AuthoritativeCathedralThemeItemCatalog catalog,
        IReadOnlySet<int> alwaysMagicMiscIds)
    {
        Tables = new ReadOnlyCollection<TsvTable>(tables.Select(CloneTable).ToArray());
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        AlwaysMagicMiscIds = alwaysMagicMiscIds.ToFrozenSet();
        Fingerprint = ComputeFingerprint(Tables);
    }

    public static IReadOnlyList<AuthoritativeCathedralThemeItemTableSchema> TableSchemas => Schemas;

    public static IReadOnlyList<string> TablePaths
        => Schemas.Select(schema => schema.Path).ToArray();

    /** Alias used by content-bundle composers when selecting this schema. */
    public static IReadOnlyList<string> TableNames => TablePaths;

    public IReadOnlyList<TsvTable> Tables { get; }
    public AuthoritativeCathedralThemeItemCatalog Catalog { get; }
    public IReadOnlySet<int> AlwaysMagicMiscIds { get; }
    /** SHA-256 of every canonical table path, schema, and row in schema order. */
    public string Fingerprint { get; }

    public static AuthoritativeCathedralThemeItemMetadata LoadTables(
        IEnumerable<TsvTable> sourceTables)
    {
        ArgumentNullException.ThrowIfNull(sourceTables);
        var tables = sourceTables.Select(CloneTable).ToArray();
        var byPath = ValidateTableSet(tables);
        // Input order is intentionally irrelevant at this boundary.  Keep the
        // detached table collection and its fingerprint in canonical schema
        // order so equivalent bundles cannot acquire different identities.
        var orderedTables = Schemas.Select(schema => byPath[schema.Path]).ToArray();

        var animations = ParseAnimations(byPath[CursorAnimationsTablePath]);
        var glyphs = ParseGlyphs(byPath[GlyphsTablePath]);
        var itemRows = ParseItems(byPath[ItemsTablePath], animations);
        var prefixes = ParseAffixes(byPath[PrefixesTablePath], PrefixesTablePath);
        var suffixes = ParseAffixes(byPath[SuffixesTablePath], SuffixesTablePath);
        var uniques = ParseUniques(byPath[UniquesTablePath]);
        var spells = ParseSpells(byPath[SpellsTablePath]);
        var cursors = ParseCursors(byPath[CursorsTablePath], animations);
        var rules = ParseRules(byPath[RulesTablePath]);
        var alwaysMagicMiscIds = ParseAlwaysMagicMisc(byPath[AlwaysMagicMiscTablePath]);

        var catalog = new AuthoritativeCathedralThemeItemCatalog(
            itemRows,
            prefixes,
            suffixes,
            uniques,
            spells,
            animations,
            glyphs,
            cursors,
            alwaysMagicMiscIds,
            rules,
            glyphs['?']);
        ValidateProductionCatalog(catalog);
        return new(orderedTables, catalog, alwaysMagicMiscIds);
    }

    public static IReadOnlyList<TsvTable> CreateTables(
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateProductionCatalog(catalog);
        if (catalog.Rules.AlwaysMagicMisc.Count != 0)
            throw new InvalidDataException(
                "String AlwaysMagicMisc rules cannot be serialized by the numeric Cathedral metadata boundary.");

        var animations = RequireAnimations(catalog);
        var tables = new List<TsvTable>(Schemas.Count) {
            CreateTable(ItemsTablePath, Schemas[0].Columns,
                catalog.Items.Select(item => CreateItemRow(item, animations)).ToArray()),
            CreateTable(PrefixesTablePath, Schemas[1].Columns,
                catalog.Prefixes.Select((affix, index) => CreateAffixRow(affix, index)).ToArray()),
            CreateTable(SuffixesTablePath, Schemas[2].Columns,
                catalog.Suffixes.Select((affix, index) => CreateAffixRow(affix, index)).ToArray()),
            CreateTable(UniquesTablePath, Schemas[3].Columns,
                catalog.Uniques.Select(CreateUniqueRow).ToArray()),
            CreateTable(SpellsTablePath, Schemas[4].Columns,
                catalog.Spells.Select(CreateSpellRow).ToArray()),
            CreateTable(CursorAnimationsTablePath, Schemas[5].Columns,
                Enumerable.Range(0, CursorAnimationRowCount)
                    .Select(index => new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["cursor_graphic"] = Number(index),
                        ["frame_count"] = Number(animations[index].FrameCount),
                        ["ticks_per_frame"] = Number(animations[index].TicksPerFrame),
                    }).ToArray()),
            CreateTable(GlyphsTablePath, Schemas[6].Columns,
                catalog.GlyphWidths.OrderBy(pair => pair.Key).Select(pair =>
                    new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["codepoint"] = Number(pair.Key),
                        ["width"] = Number(pair.Value),
                    }).ToArray()),
            CreateTable(CursorsTablePath, Schemas[7].Columns, [new Dictionary<string, string>(StringComparer.Ordinal) {
                ["book_red"] = Number(catalog.Cursors.BookRed),
                ["book_blue"] = Number(catalog.Cursors.BookBlue),
                ["book_grey"] = Number(catalog.Cursors.BookGrey),
                ["gold_small"] = Number(catalog.Cursors.GoldSmall),
                ["gold_medium"] = Number(catalog.Cursors.GoldMedium),
                ["gold_large"] = Number(catalog.Cursors.GoldLarge),
            }]),
            CreateTable(RulesTablePath, Schemas[8].Columns, [new Dictionary<string, string>(StringComparer.Ordinal) {
                ["magic_chance_base"] = Number(catalog.Rules.MagicChanceBase),
                ["magic_chance_per_level"] = Number(catalog.Rules.MagicChancePerLevel),
                ["unique_chance_normal"] = Number(catalog.Rules.UniqueChanceNormal),
                ["unique_chance_unique"] = Number(catalog.Rules.UniqueChanceUnique),
                ["bonus_levels_unique"] = Number(catalog.Rules.BonusLevelsUnique),
                ["prefix_percent"] = Number(catalog.Rules.PrefixPercent),
                ["suffix_percent"] = Number(catalog.Rules.SuffixPercent),
                ["only_good_chance"] = Number(catalog.Rules.OnlyGoodChance),
                ["no_drop_percent"] = Number(catalog.Rules.NoDropPercent),
                ["gold_percent"] = Number(catalog.Rules.GoldPercent),
            }]),
            CreateTable(AlwaysMagicMiscTablePath, Schemas[9].Columns,
                catalog.AlwaysMagicMiscIds.OrderBy(value => value).Select(value =>
                    new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["misc_id"] = Number(value),
                    }).ToArray()),
        };
        return new ReadOnlyCollection<TsvTable>(tables.Select(CloneTable).ToArray());
    }

    /** Returns deterministic CRLF files for callers that write the tables to disk. */
    public static IReadOnlyDictionary<string, string> CreateCrlfFiles(
        AuthoritativeCathedralThemeItemCatalog catalog)
        => CreateTables(catalog).ToDictionary(table => table.SourcePath, ToCanonicalCrlf,
            StringComparer.Ordinal);

    public static string ToCanonicalCrlf(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.ToCanonicalTsv().Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    private static AuthoritativeCathedralThemeItemTableSchema Schema(
        string path, params string[] columns)
        => new(path, new ReadOnlyCollection<string>(columns));

    private static Dictionary<string, TsvTable> ValidateTableSet(
        IReadOnlyList<TsvTable> tables)
    {
        if (tables.Count != Schemas.Count)
            throw new InvalidDataException("Cathedral item metadata must contain exactly the ten required tables.");
        var byPath = new Dictionary<string, TsvTable>(StringComparer.Ordinal);
        foreach (var table in tables) {
            ArgumentNullException.ThrowIfNull(table);
            if (!byPath.TryAdd(table.SourcePath, table))
                throw new InvalidDataException($"Cathedral item metadata table '{table.SourcePath}' is duplicated.");
        }
        foreach (var schema in Schemas) {
            if (!byPath.TryGetValue(schema.Path, out var table))
                throw new InvalidDataException($"Cathedral item metadata table '{schema.Path}' is missing.");
            if (!table.Columns.SequenceEqual(schema.Columns, StringComparer.Ordinal))
                throw new InvalidDataException($"Cathedral item metadata table '{schema.Path}' has the wrong columns.");
            foreach (var row in table.Rows) {
                if (row.Values.Count != table.Columns.Count
                    || table.Columns.Any(column => !row.Values.ContainsKey(column))
                    || row.Values.Keys.Any(column => !table.Columns.Contains(column, StringComparer.Ordinal)))
                    throw new InvalidDataException(
                        $"Cathedral item metadata table '{schema.Path}' has a row with unknown or missing columns.");
            }
        }
        return byPath;
    }

    private static AuthoritativeCathedralNativeItemDefinition[] ParseItems(
        TsvTable table,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> animations)
    {
        if (table.Rows.Count != ItemRowCount)
            throw new InvalidDataException($"The native item table must contain exactly {ItemRowCount} rows.");
        var result = table.Rows.Select(row => {
            var index = row.RequiredInt32("native_item_index");
            var cursor = row.RequiredInt32("cursor_graphic");
            var frameCount = 0;
            var ticks = 1;
            if (cursor == byte.MaxValue) {
                if (row.RequiredInt32("drop_rate") != 0)
                    throw new InvalidDataException("Cursor 255 is reserved for non-dropping item placeholders.");
            } else {
                if (!animations.TryGetValue(cursor, out var animation))
                    throw new InvalidDataException($"Item row {index} references unknown cursor animation {cursor}.");
                frameCount = animation.FrameCount;
                ticks = animation.TicksPerFrame;
            }
            return new AuthoritativeCathedralNativeItemDefinition(
                index,
                row.RequiredInt32("native_item_id"),
                row.RequiredInt32("drop_rate"),
                row.RequiredInt32("item_class"),
                row.RequiredInt32("equip_location"),
                cursor,
                row.RequiredInt32("item_type"),
                Cell(row, "name"),
                Cell(row, "short_name"),
                row.RequiredInt32("minimum_monster_level"),
                row.RequiredInt32("durability"),
                row.RequiredInt32("minimum_damage"),
                row.RequiredInt32("maximum_damage"),
                row.RequiredInt32("minimum_armor_class"),
                row.RequiredInt32("maximum_armor_class"),
                row.RequiredInt32("minimum_strength"),
                row.RequiredInt32("minimum_magic"),
                row.RequiredInt32("minimum_dexterity"),
                row.RequiredUInt32("flags"),
                row.RequiredInt32("misc_id"),
                row.RequiredInt32("spell_id"),
                row.RequiredBoolean("usable"),
                row.RequiredInt32("value"),
                frameCount,
                ticks,
                row.RequiredBoolean("available"));
        }).ToArray();
        RequireOrdered(result, value => value.NativeItemIndex, ItemRowCount, "item");
        if (result.Count(value => value.CursorGraphic == byte.MaxValue) != CursorSentinelRowCount)
            throw new InvalidDataException("The native item table must retain exactly three cursor-255 placeholders.");
        return result;
    }

    private static AuthoritativeCathedralNativeAffixDefinition[] ParseAffixes(
        TsvTable table, string tablePath)
    {
        if (table.Rows.Count != (tablePath == PrefixesTablePath ? PrefixRowCount : SuffixRowCount))
            throw new InvalidDataException($"The native affix table '{tablePath}' has the wrong row count.");
        var result = table.Rows.Select(row => new {
            Index = row.RequiredInt32("affix_index"),
            Value = new AuthoritativeCathedralNativeAffixDefinition(
                Cell(row, "name"),
                new AuthoritativeCathedralNativeItemPower(
                    row.RequiredInt32("power_type"),
                    row.RequiredInt32("power_param1"),
                    row.RequiredInt32("power_param2")),
                row.RequiredInt32("minimum_level"),
                row.RequiredInt32("item_type_flags"),
                row.RequiredInt32("alignment"),
                row.RequiredInt32("chance"),
                row.RequiredBoolean("useful"),
                row.RequiredInt32("minimum_value"),
                row.RequiredInt32("maximum_value"),
                row.RequiredInt32("value_multiplier")),
        }).ToArray();
        RequireOrdered(result, value => value.Index,
            tablePath == PrefixesTablePath ? PrefixRowCount : SuffixRowCount, "affix");
        return result.Select(value => value.Value).ToArray();
    }

    private static AuthoritativeCathedralNativeUniqueDefinition[] ParseUniques(TsvTable table)
    {
        if (table.Rows.Count != UniqueRowCount)
            throw new InvalidDataException($"The native unique table must contain exactly {UniqueRowCount} rows.");
        var result = table.Rows.Select(row => {
            var powers = Enumerable.Range(1, 6).Select(index =>
                new AuthoritativeCathedralNativeItemPower(
                    row.RequiredInt32($"power{index}_type"),
                    row.RequiredInt32($"power{index}_param1"),
                    row.RequiredInt32($"power{index}_param2"))).ToArray();
            var powerCount = row.RequiredInt32("power_count");
            if (powerCount is < 0 or > 6
                || powers.Take(powerCount).Any(power => power.Type == -1)
                || powers.Skip(powerCount).Any(power => power.Type != -1))
                throw new InvalidDataException("Unique power_count does not match its six ordered power slots.");
            return new AuthoritativeCathedralNativeUniqueDefinition(
                row.RequiredInt32("native_unique_id"),
                Cell(row, "name"),
                row.RequiredInt32("cursor_graphic"),
                row.RequiredInt32("native_base_item_id"),
                row.RequiredInt32("minimum_level"),
                row.RequiredInt32("value"),
                powers);
        }).ToArray();
        RequireOrdered(result, value => value.NativeUniqueId, UniqueRowCount, "unique");
        return result;
    }

    private static AuthoritativeCathedralNativeSpellDefinition[] ParseSpells(TsvTable table)
    {
        if (table.Rows.Count != SpellRowCount)
            throw new InvalidDataException($"The native spell table must contain exactly {SpellRowCount} rows.");
        var result = table.Rows.Select(row => new AuthoritativeCathedralNativeSpellDefinition(
            row.RequiredInt32("native_spell_id"),
            Cell(row, "name"),
            row.RequiredInt32("book_level"),
            row.RequiredInt32("staff_level"),
            row.RequiredInt32("minimum_magic"),
            row.RequiredInt32("book_cost"),
            row.RequiredInt32("staff_cost"),
            row.RequiredInt32("magic_type"),
            row.RequiredInt32("staff_minimum_charges"),
            row.RequiredInt32("staff_maximum_charges"),
            row.RequiredBoolean("available_in_book"),
            row.RequiredBoolean("available_in_staff"))).ToArray();
        RequireOrdered(result, value => value.NativeSpellId, SpellRowCount, "spell");
        return result;
    }

    private static Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> ParseAnimations(TsvTable table)
    {
        if (table.Rows.Count != CursorAnimationRowCount)
            throw new InvalidDataException($"The cursor animation table must contain exactly {CursorAnimationRowCount} rows.");
        var result = table.Rows.Select(row => new AuthoritativeCathedralNativeAnimationDefinition(
            row.RequiredInt32("frame_count"), row.RequiredInt32("ticks_per_frame"))).ToArray();
        var ids = table.Rows.Select(row => row.RequiredInt32("cursor_graphic")).ToArray();
        if (!ids.SequenceEqual(Enumerable.Range(0, CursorAnimationRowCount)))
            throw new InvalidDataException("Cursor animations must retain native order 0..228.");
        return ids.Zip(result).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    private static Dictionary<int, int> ParseGlyphs(TsvTable table)
    {
        if (table.Rows.Count == 0)
            throw new InvalidDataException("The GameFont12 glyph table cannot be empty.");
        var pairs = table.Rows.Select(row => (
            Codepoint: row.RequiredInt32("codepoint"),
            Width: row.RequiredInt32("width"))).ToArray();
        if (pairs.Select(value => value.Codepoint).Distinct().Count() != pairs.Length
            || pairs.Any(value => value.Codepoint is < 0 or > 0x10FFFF
                || value.Width is < 0 or > ushort.MaxValue)
            || pairs.Select(value => value.Codepoint).SequenceEqual(
                pairs.Select(value => value.Codepoint).OrderBy(value => value)) == false)
            throw new InvalidDataException("Glyph rows must be unique, ordered, and within native Unicode widths.");
        ValidateGlyphRows(pairs.ToDictionary(value => value.Codepoint, value => value.Width));
        return pairs.ToDictionary(value => value.Codepoint, value => value.Width);
    }

    private static AuthoritativeCathedralNativeCursorDefinition ParseCursors(
        TsvTable table,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> animations)
    {
        if (table.Rows.Count != 1)
            throw new InvalidDataException("The native cursor table must contain exactly one row.");
        var row = table.Rows[0];
        var cursors = new AuthoritativeCathedralNativeCursorDefinition(
            row.RequiredInt32("book_red"), row.RequiredInt32("book_blue"), row.RequiredInt32("book_grey"),
            row.RequiredInt32("gold_small"), row.RequiredInt32("gold_medium"), row.RequiredInt32("gold_large"));
        foreach (var cursor in new[] { cursors.BookRed, cursors.BookBlue, cursors.BookGrey,
                     cursors.GoldSmall, cursors.GoldMedium, cursors.GoldLarge })
            if (!animations.ContainsKey(cursor))
                throw new InvalidDataException($"Native cursor {cursor} has no animation metadata.");
        return cursors;
    }

    private static AuthoritativeItemGenerationRules ParseRules(TsvTable table)
    {
        if (table.Rows.Count != 1)
            throw new InvalidDataException("The native item rules table must contain exactly one row.");
        var row = table.Rows[0];
        return new AuthoritativeItemGenerationRules(
            row.RequiredInt32("magic_chance_base"),
            row.RequiredInt32("magic_chance_per_level"),
            row.RequiredInt32("unique_chance_normal"),
            row.RequiredInt32("unique_chance_unique"),
            row.RequiredInt32("bonus_levels_unique"),
            row.RequiredInt32("prefix_percent"),
            row.RequiredInt32("suffix_percent"),
            row.RequiredInt32("only_good_chance"),
            row.RequiredInt32("no_drop_percent"),
            row.RequiredInt32("gold_percent")) {
            AlwaysMagicMisc = Array.Empty<string>().ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    private static FrozenSet<int> ParseAlwaysMagicMisc(TsvTable table)
    {
        var values = table.Rows.Select(row => row.RequiredInt32("misc_id")).ToArray();
        if (values.Distinct().Count() != values.Length
            || values.Any(value => value is < 0 or > MaximumDefinedMiscId))
            throw new InvalidDataException("Numeric always-magic misc IDs must be unique defined native misc values.");
        return values.ToFrozenSet();
    }

    private static void ValidateProductionCatalog(AuthoritativeCathedralThemeItemCatalog catalog)
    {
        if (catalog.Items.Count != ItemRowCount || catalog.Prefixes.Count != PrefixRowCount
            || catalog.Suffixes.Count != SuffixRowCount || catalog.Uniques.Count != UniqueRowCount
            || catalog.Spells.Count != SpellRowCount)
            throw new InvalidDataException("The production Cathedral metadata row counts are incomplete.");
        RequireOrdered(catalog.Items, value => value.NativeItemIndex, ItemRowCount, "item");
        RequireOrdered(catalog.Uniques, value => value.NativeUniqueId, UniqueRowCount, "unique");
        RequireOrdered(catalog.Spells, value => value.NativeSpellId, SpellRowCount, "spell");
        if (catalog.Items.Any(item => item.SpellId < -1 || item.SpellId > NativeSpellIdMax
                || item.Available && item.SpellId >= catalog.Spells.Count))
            throw new InvalidDataException(
                "Base item spell references must be -1 or a defined native spell; available rows require loaded Diablo spells.");
        if (catalog.Uniques.Any(unique => unique.NativeBaseItemId is < 0 or > NativeUniqueBaseItemIdMax))
            throw new InvalidDataException("Unique base-item references must be defined native unique-base IDs.");
        var sentinelCount = catalog.Items.Count(value => value.CursorGraphic == byte.MaxValue);
        if (sentinelCount != CursorSentinelRowCount
            || catalog.Items.Any(value => value.CursorGraphic == byte.MaxValue
                && (value.DropRate != 0 || value.AnimationFrameCount != 0)))
            throw new InvalidDataException("The production item metadata must retain three cursor-255 placeholders.");
        foreach (var item in catalog.Items) {
            RequireNativeText(item.Name, "item name");
            RequireNativeText(item.ShortName, "item short name");
        }
        foreach (var affix in catalog.Prefixes.Concat(catalog.Suffixes))
            RequireNativeText(affix.Name, "affix name");
        foreach (var unique in catalog.Uniques)
            RequireNativeText(unique.Name, "unique name");
        foreach (var spell in catalog.Spells)
            RequireNativeText(spell.Name, "spell name");
        if (catalog.AlwaysMagicMiscIds.Any(value => value is < 0 or > MaximumDefinedMiscId))
            throw new InvalidDataException("Numeric always-magic misc IDs must match a defined native misc ID.");
        foreach (var affix in catalog.Prefixes.Concat(catalog.Suffixes))
            if (!IsSupportedPowerType(affix.Power.Type))
                throw new InvalidDataException($"Affix power {affix.Power.Type} is not a supported ordinary effect identity.");
            else
                ValidatePowerReferences(affix.Power, "affix");
        foreach (var unique in catalog.Uniques)
            foreach (var power in unique.Powers)
                if (!IsSupportedPowerType(power.Type))
                    throw new InvalidDataException($"Unique power {power.Type} is not a supported ordinary effect identity.");
                else
                    ValidatePowerReferences(power, "unique");
        ValidateGlyphRows(catalog.GlyphWidths);
        _ = RequireAnimations(catalog);
        foreach (var cursor in new[] { catalog.Cursors.BookRed, catalog.Cursors.BookBlue,
                     catalog.Cursors.BookGrey, catalog.Cursors.GoldSmall,
                     catalog.Cursors.GoldMedium, catalog.Cursors.GoldLarge })
            if (!catalog.CursorAnimations.ContainsKey(cursor))
                throw new InvalidDataException($"Native cursor {cursor} has no animation metadata.");
        foreach (var unique in catalog.Uniques)
            if (unique.CursorGraphic != byte.MaxValue
                && !catalog.CursorAnimations.ContainsKey(unique.CursorGraphic))
                throw new InvalidDataException($"Unique {unique.NativeUniqueId} has no cursor animation metadata.");
    }

    private static IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> RequireAnimations(
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        if (catalog.CursorAnimations.Count != CursorAnimationRowCount
            || !catalog.CursorAnimations.Keys.OrderBy(value => value)
                .SequenceEqual(Enumerable.Range(0, CursorAnimationRowCount)))
            throw new InvalidDataException("The production metadata requires all cursor animations 0..228.");
        foreach (var item in catalog.Items) {
            if (item.CursorGraphic == byte.MaxValue)
                continue;
            if (!catalog.CursorAnimations.TryGetValue(item.CursorGraphic, out var animation)
                || animation.FrameCount != item.AnimationFrameCount
                || animation.TicksPerFrame != item.AnimationTicksPerFrame)
                throw new InvalidDataException("Item animation metadata must derive from its cursor row.");
        }
        return catalog.CursorAnimations;
    }

    private static void ValidateGlyphRows(IReadOnlyDictionary<int, int> glyphs)
    {
        if (!glyphs.ContainsKey('?'))
            throw new InvalidDataException("The glyph table must contain the native replacement glyph '?'.");
        foreach (var row in glyphs.Keys.Select(value => value >> 8).Distinct())
            for (var offset = 0; offset < 256; offset++)
                if (!glyphs.ContainsKey((row << 8) | offset))
                    throw new InvalidDataException($"Glyph row {row} is incomplete.");
    }

    private static Dictionary<string, string> CreateItemRow(
        AuthoritativeCathedralNativeItemDefinition item,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> animations)
    {
        EnsureCell(item.Name, "item name");
        EnsureCell(item.ShortName, "item short name");
        _ = animations;
        return new(StringComparer.Ordinal) {
            ["native_item_index"] = Number(item.NativeItemIndex),
            ["native_item_id"] = Number(item.NativeItemId),
            ["drop_rate"] = Number(item.DropRate),
            ["item_class"] = Number(item.ItemClass),
            ["equip_location"] = Number(item.EquipLocation),
            ["cursor_graphic"] = Number(item.CursorGraphic),
            ["item_type"] = Number(item.ItemType),
            ["name"] = item.Name,
            ["short_name"] = item.ShortName,
            ["minimum_monster_level"] = Number(item.MinimumMonsterLevel),
            ["durability"] = Number(item.Durability),
            ["minimum_damage"] = Number(item.MinimumDamage),
            ["maximum_damage"] = Number(item.MaximumDamage),
            ["minimum_armor_class"] = Number(item.MinimumArmorClass),
            ["maximum_armor_class"] = Number(item.MaximumArmorClass),
            ["minimum_strength"] = Number(item.MinimumStrength),
            ["minimum_magic"] = Number(item.MinimumMagic),
            ["minimum_dexterity"] = Number(item.MinimumDexterity),
            ["flags"] = Number(item.Flags),
            ["misc_id"] = Number(item.MiscId),
            ["spell_id"] = Number(item.SpellId),
            ["usable"] = Boolean(item.Usable),
            ["value"] = Number(item.Value),
            ["available"] = Boolean(item.Available),
        };
    }

    private static Dictionary<string, string> CreateAffixRow(
        AuthoritativeCathedralNativeAffixDefinition affix, int index)
    {
        EnsureCell(affix.Name, "affix name");
        return new(StringComparer.Ordinal) {
            ["affix_index"] = Number(index),
            ["name"] = affix.Name,
            ["power_type"] = Number(affix.Power.Type),
            ["power_param1"] = Number(affix.Power.Param1),
            ["power_param2"] = Number(affix.Power.Param2),
            ["minimum_level"] = Number(affix.MinimumLevel),
            ["item_type_flags"] = Number(affix.ItemTypeFlags),
            ["alignment"] = Number(affix.Alignment),
            ["chance"] = Number(affix.Chance),
            ["useful"] = Boolean(affix.Useful),
            ["minimum_value"] = Number(affix.MinimumValue),
            ["maximum_value"] = Number(affix.MaximumValue),
            ["value_multiplier"] = Number(affix.ValueMultiplier),
        };
    }

    private static Dictionary<string, string> CreateUniqueRow(
        AuthoritativeCathedralNativeUniqueDefinition unique)
    {
        EnsureCell(unique.Name, "unique name");
        var powers = unique.Powers.ToArray();
        if (powers.Length > 6)
            throw new InvalidDataException("A native unique cannot contain more than six powers.");
        var slots = Enumerable.Range(0, 6)
            .Select(index => index < powers.Length ? powers[index] : new AuthoritativeCathedralNativeItemPower(-1))
            .ToArray();
        var count = Array.FindIndex(slots, power => power.Type == -1);
        if (count < 0) count = slots.Length;
        if (slots.Skip(count).Any(power => power.Type != -1))
            throw new InvalidDataException("Unique power slots must terminate at the first IPL_INVALID power.");
        var row = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["native_unique_id"] = Number(unique.NativeUniqueId),
            ["name"] = unique.Name,
            ["cursor_graphic"] = Number(unique.CursorGraphic),
            ["native_base_item_id"] = Number(unique.NativeBaseItemId),
            ["minimum_level"] = Number(unique.MinimumLevel),
            ["value"] = Number(unique.Value),
            ["power_count"] = Number(count),
        };
        for (var index = 0; index < slots.Length; index++) {
            row[$"power{index + 1}_type"] = Number(slots[index].Type);
            row[$"power{index + 1}_param1"] = Number(slots[index].Param1);
            row[$"power{index + 1}_param2"] = Number(slots[index].Param2);
        }
        return row;
    }

    private static Dictionary<string, string> CreateSpellRow(
        AuthoritativeCathedralNativeSpellDefinition spell)
    {
        EnsureCell(spell.Name, "spell name");
        return new(StringComparer.Ordinal) {
            ["native_spell_id"] = Number(spell.NativeSpellId),
            ["name"] = spell.Name,
            ["book_level"] = Number(spell.BookLevel),
            ["staff_level"] = Number(spell.StaffLevel),
            ["minimum_magic"] = Number(spell.MinimumMagic),
            ["book_cost"] = Number(spell.BookCost),
            ["staff_cost"] = Number(spell.StaffCost),
            ["magic_type"] = Number(spell.MagicType),
            ["staff_minimum_charges"] = Number(spell.StaffMinimumCharges),
            ["staff_maximum_charges"] = Number(spell.StaffMaximumCharges),
            ["available_in_book"] = Boolean(spell.AvailableInBook),
            ["available_in_staff"] = Boolean(spell.AvailableInStaff),
        };
    }

    private static TsvTable CreateTable(
        string path,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var detachedRows = rows.Select((values, index) => new TsvRow(
            index + 2,
            new ReadOnlyDictionary<string, string>(columns.ToDictionary(
                column => column,
                column => values.TryGetValue(column, out var value)
                    ? value
                    : throw new InvalidDataException($"Generated table '{path}' omitted column '{column}'."),
                StringComparer.Ordinal)))).ToArray();
        return new(path, new ReadOnlyCollection<string>(columns.ToArray()), detachedRows);
    }

    private static string Cell(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value))
            throw new InvalidDataException($"Column '{column}' is missing on TSV row {row.LineNumber}.");
        EnsureCell(value, column);
        return value;
    }

    private static void EnsureCell(string value, string field)
    {
        if (value is null || value.Contains('\0') || value.Contains('\t')
            || value.Contains('\r') || value.Contains('\n'))
            throw new InvalidDataException($"Metadata field '{field}' contains an unrepresentable TSV character.");
    }

    private static void RequireNativeText(string value, string field)
    {
        EnsureCell(value, field);
        byte[] bytes;
        try {
            bytes = new UTF8Encoding(false, true).GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException($"Metadata field '{field}' is not valid UTF-8.", exception);
        }
        // Source item, affix, unique, and spell names are std::string values.
        // The generated Item fields are copied into smaller native buffers only
        // later by the verified generator.  Keep the raw boundary lossless and
        // impose only a generous resource bound for malformed external packs.
        if (bytes.Length > MaximumRawTextUtf8Bytes)
            throw new InvalidDataException($"Metadata field '{field}' exceeds the raw text resource bound.");
    }

    private static bool IsSupportedPowerType(int type)
        => type == -1 || SupportedPowerTypes.Contains(type);

    private static void ValidatePowerReferences(AuthoritativeCathedralNativeItemPower power, string source)
    {
        switch (power.Type) {
        case 4:
        case 89:
            if (!SupportedToHitBonusLevels.Contains(power.Param1))
                throw new InvalidDataException($"The {source} to-hit/damage power has an unsupported bonus level.");
            break;
        case 5: {
            var effectiveLevel = power.Param1 == int.MinValue ? int.MaxValue : -power.Param1;
            if (!SupportedToHitBonusLevels.Contains(effectiveLevel))
                throw new InvalidDataException($"The {source} cursed to-hit/damage power has an unsupported bonus level.");
            break;
        }
        case 66:
            if (power.Param1 < 0 || power.Param1 >= SpellRowCount || power.Param2 < 0)
                throw new InvalidDataException(
                    $"The {source} IPL_SPELL power must reference a declared spell and nonnegative charges.");
            break;
        }
    }

    private static void RequireOrdered<T>(
        IReadOnlyList<T> values, Func<T, int> id, int expectedCount, string kind)
    {
        if (values.Count != expectedCount
            || values.Select(id).SequenceEqual(Enumerable.Range(0, expectedCount)) == false)
            throw new InvalidDataException($"Native {kind} rows must retain contiguous source order.");
    }

    private static TsvTable CloneTable(TsvTable table)
        => new(table.SourcePath,
            new ReadOnlyCollection<string>(table.Columns.ToArray()),
            new ReadOnlyCollection<TsvRow>(table.Rows.Select(row => new TsvRow(
                row.LineNumber,
                new ReadOnlyDictionary<string, string>(row.Values.ToDictionary(
                    pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)))).ToArray()));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Number(uint value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Boolean(bool value) => value ? "true" : "false";

    private static string ComputeFingerprint(IReadOnlyList<TsvTable> tables)
    {
        var canonical = new StringBuilder();
        foreach (var table in tables) {
            canonical.Append(table.SourcePath).Append('\n');
            canonical.Append(table.ToCanonicalTsv());
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
