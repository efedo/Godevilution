using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable schema and detachment tests for the complete raw Cathedral item boundary. */
public sealed class AuthoritativeCathedralThemeItemMetadataTests
{
    private static readonly int[] SupportedPowerTypes = [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        14, 15, 16, 17,
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
        41, 42, 43, 45, 46, 50, 52, 53,
        55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
        68, 69, 70, 72, 73, 75, 76, 77, 79, 80, 81, 82,
        84, 85, 86, 87, 88, 89, 90, 91, 92, 93,
    ];
    [Fact]
    public void CompleteCatalogRoundTripsEveryMetadataFamilyInCanonicalOrder()
    {
        var catalog = CreateCatalog(wideRequirements: true, extraGlyphRow: true);
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(catalog);
        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables.Reverse());

        Assert.Equal(
            AuthoritativeCathedralThemeItemMetadata.TableNames.ToArray(),
            metadata.Tables.Select(table => table.SourcePath).ToArray());
        Assert.Equal(168, metadata.Catalog.Items.Count);
        Assert.Equal(83, metadata.Catalog.Prefixes.Count);
        Assert.Equal(95, metadata.Catalog.Suffixes.Count);
        Assert.Equal(90, metadata.Catalog.Uniques.Count);
        Assert.Equal(37, metadata.Catalog.Spells.Count);
        Assert.Equal(229, metadata.Catalog.CursorAnimations.Count);
        Assert.Equal(512, metadata.Catalog.GlyphWidths.Count);
        Assert.Equal(128, metadata.Catalog.Items[7].MinimumStrength);
        Assert.Equal(255, metadata.Catalog.Items[8].MinimumDexterity);
        Assert.Equal(6, metadata.Catalog.Uniques[0].Powers.Count);
        Assert.Equal(5, metadata.Catalog.Uniques[0].Powers[5].Type);
        Assert.Contains(24, metadata.AlwaysMagicMiscIds);

        var unavailableCatalog = CreateCatalog(includeUnavailableNativeSpellIds: true);
        var unavailable = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.CreateTables(unavailableCatalog));
        Assert.Equal(37, unavailable.Catalog.Items[10].SpellId);
        Assert.Equal(46, unavailable.Catalog.Items[11].SpellId);
        Assert.Equal(51, unavailable.Catalog.Items[12].SpellId);
        Assert.False(unavailable.Catalog.Items[10].Available);

        var secondLoad = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables);
        Assert.Equal(metadata.Fingerprint, secondLoad.Fingerprint);
        Assert.All(AuthoritativeCathedralThemeItemMetadata.CreateCrlfFiles(catalog), pair => {
            Assert.DoesNotContain("\n", pair.Value.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void FingerprintIsSchemaOrderedAndSensitiveToRawCells()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog());
        var shuffled = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables.Reverse());
        var canonical = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables);
        Assert.Equal(canonical.Fingerprint, shuffled.Fingerprint);

        var changed = ReplaceTable(tables, AuthoritativeCathedralThemeItemMetadata.ItemsTablePath,
            ReplaceCell(tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath),
                "value", "4321"));
        var changedMetadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(changed);
        Assert.NotEqual(canonical.Fingerprint, changedMetadata.Fingerprint);
    }

    [Fact]
    public void RejectsMissingDuplicateUnknownAndWrongSchemaTables()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(tables.Take(tables.Length - 1)));

        var duplicate = tables.ToList();
        duplicate[^1] = new TsvTable(
            tables[0].SourcePath, tables[^1].Columns, tables[^1].Rows);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(duplicate));

        var unknown = tables.ToList();
        unknown[^1] = new TsvTable(
            "unrelated.tsv", tables[^1].Columns, tables[^1].Rows);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(unknown));

        var wrongColumns = tables.ToList();
        var items = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath);
        wrongColumns[0] = new TsvTable(items.SourcePath, items.Columns.Reverse().ToArray(), items.Rows);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(wrongColumns));

        var extraColumnValues = items.Rows[0].Values.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        extraColumnValues["unexpected"] = "1";
        var extraColumn = new TsvTable(items.SourcePath, items.Columns,
            [new TsvRow(items.Rows[0].LineNumber, extraColumnValues), .. items.Rows.Skip(1)]);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(
                ReplaceTable(tables, items.SourcePath, extraColumn)));
    }

    [Fact]
    public void RejectsGapsReferencesIncompleteGlyphRowsAndNativeWidthViolations()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var items = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath);
        var reversed = ReplaceTable(tables, items.SourcePath,
            new TsvTable(items.SourcePath, items.Columns, items.Rows.Reverse().ToArray()));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(reversed));

        var badCursor = ReplaceTable(tables, items.SourcePath, ReplaceCell(items, "cursor_graphic", "250"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(badCursor));

        var glyphs = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.GlyphsTablePath);
        var missingGlyph = ReplaceTable(tables, glyphs.SourcePath,
            new TsvTable(glyphs.SourcePath, glyphs.Columns, glyphs.Rows.Take(glyphs.Rows.Count - 1).ToArray()));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(missingGlyph));

        var invalidStrength = ReplaceCell(items, "minimum_strength", "256", rowIndex: 7);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, items.SourcePath, invalidStrength)));
    }

    [Fact]
    public void RetainsThreeCursor255PlaceholdersAndRejectsPositiveDropRate()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var items = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath);
        var sentinels = items.Rows.Select((row, index) => (row, index))
            .Where(pair => pair.row.RequiredInt32("cursor_graphic") == byte.MaxValue)
            .ToArray();
        Assert.Equal(3, sentinels.Length);

        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables);
        Assert.Equal(3, metadata.Catalog.Items.Count(item => item.CursorGraphic == byte.MaxValue));
        Assert.All(metadata.Catalog.Items.Where(item => item.CursorGraphic == byte.MaxValue), item => {
            Assert.Equal(0, item.DropRate);
            Assert.Equal(0, item.AnimationFrameCount);
        });

        var malformed = ReplaceCell(items, "drop_rate", "1", sentinels[0].index);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, items.SourcePath, malformed)));
    }

    [Fact]
    public void PreservesAllSixUniquePowerSlotsButRejectsInconsistentShape()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables);
        Assert.Equal(6, metadata.Catalog.Uniques[0].Powers.Count);
        Assert.Equal(Enumerable.Range(0, 6).ToArray(), metadata.Catalog.Uniques[0].Powers.Select(power => power.Type).ToArray());

        var unique = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.UniquesTablePath);
        var malformed = ReplaceCell(unique, "power_count", "1");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, unique.SourcePath, malformed)));
    }

    [Fact]
    public void ClonesNestedMetadataAndRejectsUnrepresentableStringRules()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog());
        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(tables);

        Assert.NotSame(tables[0], metadata.Tables[0]);
        Assert.NotSame(tables[0].Rows[0], metadata.Tables[0].Rows[0]);
        Assert.NotSame(tables[0].Rows[0].Values, metadata.Tables[0].Rows[0].Values);
        Assert.NotSame(CreateCatalog().Uniques[0].Powers, metadata.Catalog.Uniques[0].Powers);
        Assert.IsAssignableFrom<IReadOnlySet<int>>(metadata.AlwaysMagicMiscIds);

        var rules = CreateCatalog().Rules with {
            AlwaysMagicMisc = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "book" },
        };
        var stringRulesCatalog = CreateCatalog(rules: rules);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.CreateTables(stringRulesCatalog));
    }

    [Fact]
    public void RequiresCompleteGlyphRowsAndReplacementGlyph()
    {
        var catalog = CreateCatalog(extraGlyphRow: true);
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(catalog).ToArray();
        var glyphs = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.GlyphsTablePath);
        var incompleteHighRow = new TsvTable(glyphs.SourcePath, glyphs.Columns,
            glyphs.Rows.Where(row => row.RequiredInt32("codepoint") != 0x100).ToArray());
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, glyphs.SourcePath, incompleteHighRow)));

        var missingReplacement = new TsvTable(glyphs.SourcePath, glyphs.Columns,
            glyphs.Rows.Where(row => row.RequiredInt32("codepoint") != '?').ToArray());
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, glyphs.SourcePath, missingReplacement)));
    }

    [Fact]
    public void RetainsLongMultibyteRawTextWithoutApplyingGeneratedFieldTruncation()
    {
        var longName = string.Concat(Enumerable.Repeat("é界", 200));
        var source = CreateCatalog().Items
            .Select(item => item.NativeItemIndex == 1
                ? item with { Name = longName }
                : item)
            .ToArray();
        var catalog = new AuthoritativeCathedralThemeItemCatalog(
            source,
            CreateCatalog().Prefixes,
            CreateCatalog().Suffixes,
            CreateCatalog().Uniques,
            CreateCatalog().Spells,
            CreateCatalog().CursorAnimations,
            CreateCatalog().GlyphWidths,
            CreateCatalog().Cursors,
            new HashSet<int> { 23, 24, 25, 26 },
            CreateCatalog().Rules,
            replacementGlyphWidth: 8);

        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.CreateTables(catalog));
        Assert.Equal(longName, metadata.Catalog.Items[1].Name);

        var generated = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(
            new AuthoritativeCathedralThemeItemRequest(1, 1, 2, 0, false, false),
            metadata.Catalog,
            new AuthoritativeCathedralThemeItemPolicy(1));
        Assert.NotEqual(longName, generated.Item.BaseName);
        Assert.StartsWith("é", generated.Item.BaseName);
    }

    [Fact]
    public void RejectsSerializedCursorReferencesOutsideTheCompleteAnimationTable()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var cursors = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.CursorsTablePath);
        var invalid = ReplaceCell(cursors, "book_red", "250");
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemMetadata.LoadTables(
                ReplaceTable(tables, cursors.SourcePath, invalid)));
    }

    [Fact]
    public void RejectsUnsupportedActivePrefixAndUniquePowerIdentities()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var prefixes = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.PrefixesTablePath);
        var invalidPrefix = ReplaceCell(prefixes, "power_type", "12");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, prefixes.SourcePath, invalidPrefix)));

        var uniques = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.UniquesTablePath);
        var invalidUnique = ReplaceCell(uniques, "power1_type", "127");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, uniques.SourcePath, invalidUnique)));
    }

    [Fact]
    public void RejectsMalformedSpellBasePowerAndMiscReferences()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var items = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.ItemsTablePath);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, items.SourcePath, ReplaceCell(items, "spell_id", "37"))));
        foreach (var invalidSpellId in new[] { "52", "127" })
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
                ReplaceTable(tables, items.SourcePath, ReplaceCell(items, "spell_id", invalidSpellId))));

        var prefixes = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.PrefixesTablePath);
        var spellPower = ReplaceCell(
            ReplaceCell(prefixes, "power_type", "66"), "power_param1", "37");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, prefixes.SourcePath, spellPower)));

        var uniques = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.UniquesTablePath);
        var invalidBase = ReplaceCell(uniques, "native_base_item_id", "-1");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, uniques.SourcePath, invalidBase)));
        foreach (var invalidBaseId in new[] { "70", "127" })
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
                ReplaceTable(tables, uniques.SourcePath,
                    ReplaceCell(uniques, "native_base_item_id", invalidBaseId))));

        var misc = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.AlwaysMagicMiscTablePath);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, misc.SourcePath, ReplaceCell(misc, "misc_id", "56"))));
    }

    [Fact]
    public void PreservesDefinedUniqueBaseWithoutMatchingLoadedItemRow()
    {
        var source = CreateCatalog();
        var items = source.Items.Select(item => item.NativeItemIndex == 67
            ? item with { NativeItemId = 66 }
            : item).ToArray();
        var catalog = new AuthoritativeCathedralThemeItemCatalog(
            items,
            source.Prefixes,
            source.Suffixes,
            source.Uniques,
            source.Spells,
            source.CursorAnimations,
            source.GlyphWidths,
            source.Cursors,
            source.AlwaysMagicMiscIds,
            source.Rules,
            source.ReplacementGlyphWidth);

        var metadata = AuthoritativeCathedralThemeItemMetadata.LoadTables(
            AuthoritativeCathedralThemeItemMetadata.CreateTables(catalog));
        Assert.Equal(67, metadata.Catalog.Uniques[67].NativeBaseItemId);
    }

    [Fact]
    public void RejectsUnsupportedCalculateToHitBonusArgumentsInAffixesAndUniques()
    {
        var tables = AuthoritativeCathedralThemeItemMetadata.CreateTables(CreateCatalog()).ToArray();
        var prefixes = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.PrefixesTablePath);
        foreach (var type in new[] { "4", "5" }) {
            var invalid = ReplaceCell(
                ReplaceCell(prefixes, "power_type", type), "power_param1", "0");
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
                ReplaceTable(tables, prefixes.SourcePath, invalid)));
        }

        var uniques = tables.Single(table => table.SourcePath == AuthoritativeCathedralThemeItemMetadata.UniquesTablePath);
        var invalidUnique = ReplaceCell(
            ReplaceCell(uniques, "power1_type", "89"), "power1_param1", "0");
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemMetadata.LoadTables(
            ReplaceTable(tables, uniques.SourcePath, invalidUnique)));
    }

    private static IReadOnlyList<TsvTable> ReplaceTable(
        IEnumerable<TsvTable> tables, string path, TsvTable replacement)
        => tables.Select(table => table.SourcePath == path ? replacement : table).ToArray();

    private static TsvTable ReplaceCell(TsvTable table, string column, string value, int rowIndex = 0)
    {
        var rows = table.Rows.Select((row, index) => {
            var values = row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            if (index == rowIndex)
                values[column] = value;
            return new TsvRow(row.LineNumber, values);
        }).ToArray();
        return new TsvTable(table.SourcePath, table.Columns, rows);
    }

    internal static AuthoritativeCathedralThemeItemCatalog CreateCatalog(
        bool wideRequirements = false,
        bool extraGlyphRow = false,
        bool includeUnavailableNativeSpellIds = false,
        AuthoritativeItemGenerationRules? rules = null)
    {
        var animations = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.CursorAnimationRowCount)
            .ToDictionary(index => index, index => new AuthoritativeCathedralNativeAnimationDefinition(1 + index % 7));
        var sentinelIndexes = new HashSet<int> { 23, 108, 111 };
        var items = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.ItemRowCount)
            .Select(index => {
                var cursor = sentinelIndexes.Contains(index) ? byte.MaxValue : index % 229;
                return new AuthoritativeCathedralNativeItemDefinition(
                    NativeItemIndex: index,
                    NativeItemId: index <= sbyte.MaxValue ? index : index - 256,
                    DropRate: sentinelIndexes.Contains(index) ? 0 : 1 + index % 8,
                    ItemClass: index % 12,
                    EquipLocation: index % 8,
                    CursorGraphic: cursor,
                    ItemType: index % 14,
                    Name: index == 7 ? "Éclair\u200B" : $"Item{index}",
                    ShortName: $"I{index}",
                    MinimumMonsterLevel: index % 64,
                    Durability: index % 2 == 0 ? 20 : 1,
                    MinimumDamage: index % 3,
                    MaximumDamage: 4 + index % 5,
                    MinimumArmorClass: index % 3,
                    MaximumArmorClass: 3 + index % 5,
                    MinimumStrength: wideRequirements && index == 7 ? 128 : 0,
                    MinimumMagic: index % 64,
                    MinimumDexterity: wideRequirements && index == 8 ? 255 : 0,
                    Flags: (uint)(index % 8),
                    MiscId: index == 24 ? 24 : index % 128,
                    SpellId: includeUnavailableNativeSpellIds && index is 10 or 11 or 12
                        ? index == 10 ? 37 : index == 11 ? 46 : 51
                        : index % 37,
                    Usable: index % 3 == 0,
                    Value: 100 + index,
                    AnimationFrameCount: sentinelIndexes.Contains(index) ? 0 : animations[cursor].FrameCount,
                    AnimationTicksPerFrame: 1,
                    Available: !(includeUnavailableNativeSpellIds && index is 10 or 11 or 12));
            }).ToArray();
        var prefixes = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.PrefixRowCount)
            .Select(index => Affix($"Prefix{index}", index, useful: index % 2 == 0)).ToArray();
        var suffixes = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.SuffixRowCount)
            .Select(index => Affix($"Suffix{index}", index + 1, useful: index % 2 != 0)).ToArray();
        var uniques = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.UniqueRowCount)
            .Select(index => new AuthoritativeCathedralNativeUniqueDefinition(
                index,
                $"Unique{index}",
                index % 229,
                index % 70,
                index % 64,
                500 + index,
                index == 0
                    ? Enumerable.Range(0, 6)
                        .Select(power => new AuthoritativeCathedralNativeItemPower(
                            power, ValidPowerParam1(power), power + 2)).ToArray()
                    : Array.Empty<AuthoritativeCathedralNativeItemPower>()))
            .ToArray();
        var spells = Enumerable.Range(0, AuthoritativeCathedralThemeItemMetadata.SpellRowCount)
            .Select(index => new AuthoritativeCathedralNativeSpellDefinition(
                index,
                $"Spell{index}",
                index == 0 ? -1 : index % 64,
                index == 0 ? -1 : index % 64,
                index % 256,
                index * 10,
                index * 10,
                index % 3,
                index % 5,
                (index % 5) + 1,
                AvailableInBook: index % 2 == 0,
                AvailableInStaff: index % 3 != 0))
            .ToArray();
        var glyphs = Enumerable.Range(0, 256).ToDictionary(codepoint => codepoint, _ => 8);
        if (extraGlyphRow)
            foreach (var codepoint in Enumerable.Range(256, 256))
                glyphs[codepoint] = 7;

        return new AuthoritativeCathedralThemeItemCatalog(
            items,
            prefixes,
            suffixes,
            uniques,
            spells,
            animations,
            glyphs,
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            new HashSet<int> { 23, 24, 25, 26 },
            rules ?? new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 25, 66, 66, 59, 74),
            replacementGlyphWidth: 8);
    }

    private static AuthoritativeCathedralNativeAffixDefinition Affix(string name, int power, bool useful)
    {
        var type = SupportedPowerTypes[power % SupportedPowerTypes.Length];
        var param1 = ValidPowerParam1(type, power);
        return new(
            name,
            new AuthoritativeCathedralNativeItemPower(type, param1, power + 1),
            power % 64,
            power % 64,
            power % 3,
            1,
            useful,
            0,
            100,
            1);
    }

    private static int ValidPowerParam1(int type, int fallback = 0)
        => type switch {
            4 or 89 => 20,
            5 => 25,
            66 => fallback % AuthoritativeCathedralThemeItemMetadata.SpellRowCount,
            _ => fallback,
        };
}
