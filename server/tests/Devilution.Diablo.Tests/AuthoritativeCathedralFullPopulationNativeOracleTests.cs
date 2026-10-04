using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Devilution.Data;
using Godevilution.Tools;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Actual pre-selection inputs feed every managed phase; all later native states are comparison-only. */
public sealed class AuthoritativeCathedralFullPopulationNativeOracleTests
{
    private const string FileName = "native-full-population.json";
    private const int Cells = 112 * 112;
    private static readonly int[] ThemeTypes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14, 15];

    public static TheoryData<int, uint, string> Cases => AuthoritativeCathedralEncounterNativeOracleTests.Cases;

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void FullPopulationMatchesNative(int depth, uint seed, string entry)
        => CompareFullPopulation(depth, seed, entry, useCompiledMetadata: false);

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void CompiledMetadataPopulationMatchesNative(int depth, uint seed, string entry)
        => CompareFullPopulation(depth, seed, entry, useCompiledMetadata: true);

    private static void CompareFullPopulation(int depth, uint seed, string entry, bool useCompiledMetadata)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY");
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024));
        var row = corpus.Cases.Single(row => row.Depth == depth && row.Seed == seed && row.Entry == entry);
        var independent = AuthoritativeCathedralEncounterNativeOracleTests.LoadIndependentPopulationCase(depth, seed, entry);
        var metadata = useCompiledMetadata ? CathedralPopulationMetadataCompiler.LoadDirectory(
            AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory("GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY")) : null;
        var itemCatalog = metadata?.Items.Catalog ?? corpus.ItemCatalog;
        var preThemeGeometry = independent.Parameters.PreThemeGeometry;
        if (metadata is not null) {
            var coarse = AuthoritativeCathedralDepthGenerator.Generate(depth, seed,
                entry == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous);
            var fine = AuthoritativeCathedralDepthGeometryMaterializer.Materialize((uint)depth,
                coarse, metadata.Geometry.Pieces, metadata.Geometry.TileProperties, "cathedral-metadata-proof", false, false).Level;
            Assert.Equal(preThemeGeometry.Pieces, fine.NativePieceLayer);
            Assert.Equal(preThemeGeometry.Transparency.Select(value => (ushort)value), fine.NativeTransparency);
            preThemeGeometry = new(fine.NativePieceLayer, preThemeGeometry.Special,
                fine.NativeTransparency.Select(value => checked((byte)value)).ToArray(), metadata.EffectivePieceProperties,
                preThemeGeometry.Populated, preThemeGeometry.PlayerOccupied, preThemeGeometry.MonsterOccupied);
        }
        var initialPopulation = metadata?.CreateInitialPopulationParameters(depth,
            independent.Parameters.PopulationSeed, independent.Parameters.StorySeed, preThemeGeometry,
            independent.Parameters.ObjectTemplate.Player, independent.Parameters.ThemeTemplate.NextTransparencyValue,
            independent.Parameters.ThemeTemplate.TriggerPositions) ?? independent.Parameters;
        if (metadata is not null) {
            Assert.Equal(independent.DensityDivisor, metadata.RuleForDepth(depth).MonsterDensityDivisor);
            Assert.Equal(independent.Parameters.ObjectTemplate.TrapPercent, metadata.RuleForDepth(depth).TrapPercent);
            Assert.Equal(row.SelectedRoomCatalog.ShrineAvailable, metadata.ShrineAvailable);
        }
        var geometry = independent.Parameters.PreThemeGeometry;
        var baseline = row.Baseline.State;
        Assert.Equal(independent.PreClearRandomState, row.PreClearRandomState);
        Assert.Equal(independent.PostClearRandomState, row.PostClearRandomState);
        Assert.Equal(independent.PreThemeTileFlags, baseline.TileFlags);
        Assert.Equal(independent.PlayerRefs, baseline.PlayerRefs);
        Assert.Equal(independent.MonsterRefs, baseline.MonsterRefs);
        Assert.Equal(geometry.Pieces, baseline.Pieces);
        Assert.Equal(geometry.Special, baseline.Special);
        Assert.Equal(geometry.Transparency.Select(value => unchecked((sbyte)value)), baseline.Transparency);
        Assert.Equal(geometry.PieceProperties, baseline.PieceProperties);
        Assert.Equal(0, baseline.ActiveObjectCount);
        Assert.Equal(0, baseline.ActiveMonsterCount);
        Assert.Equal(Enumerable.Range(0, 200), baseline.ActiveMonsterIds);
        Assert.Equal(0, baseline.ActiveItemCount);
        Assert.Equal(row.PreClearRandomState, row.Baseline.RandomState);
        Assert.Equal(default, row.Baseline.Origin);
        Assert.Equal(0, row.Baseline.ThemeVar1);
        Assert.Equal(0, baseline.ActiveLightCount);
        Assert.Equal(Enumerable.Range(0, 32), baseline.ActiveLightIds);
        Assert.False(baseline.UpdateLighting);
        Assert.False(baseline.DisableLighting);
        Assert.Empty(baseline.UniqueItemFlags);
        Assert.All(baseline.ObjectRefs, value => Assert.Equal(0, value));
        Assert.All(baseline.ItemRefs, value => Assert.Equal(0, value));
        Assert.All(baseline.Corpses, value => Assert.Equal(0, value));
        Assert.All(baseline.Light, value => Assert.Equal(15, value));
        Assert.All(baseline.PreLight, value => Assert.Equal(0, value));
        var freshItems = Enumerable.Range(0, 128).Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
        Assert.Equal(freshItems, baseline.ItemSlots);
        Assert.Equal(Enumerable.Range(0, 127), baseline.ActiveItemIds);
        var initialTiles = Enumerable.Range(0, Cells).Select(index => new AuthoritativeCathedralInitialItemTile(
            independent.PlayerRefs[index], independent.MonsterRefs[index], 0, 0,
            (independent.PreThemeTileFlags[index] & 8) != 0,
            (geometry.PieceProperties[geometry.Pieces[index]] & 1) != 0)).ToArray();
        var potions = itemCatalog.Items.Where(value => value.NativeItemIndex is 24 or 25)
            .Select(value => new AuthoritativeCathedralInitialItemDefinition(value.NativeItemIndex,
                value.ItemType, value.CursorGraphic, value.Name, value.Name, value.EquipLocation, value.ItemClass,
                value.MinimumDamage, value.MaximumDamage, value.MinimumArmorClass, value.MaximumArmorClass,
                value.Flags, value.MiscId, value.SpellId, value.Value, value.Durability,
                value.MinimumStrength, value.MinimumMagic, value.MinimumDexterity,
                value.AnimationFrameCount, value.AnimationTicksPerFrame)).ToArray();
        var request = new AuthoritativeCathedralFullPopulationRequest(initialPopulation, independent.PreClearRandomState,
            independent.MonsterRefs, independent.PlayerRefs, independent.PreThemeTileFlags, baseline.Corpses,
            baseline.Light, baseline.PreLight,
            new(baseline.ActiveLightIds, baseline.ActiveLightCount, baseline.LightRecords,
                baseline.UpdateLighting, baseline.DisableLighting),
            initialPopulation.ThemeTemplate.TriggerPositions, metadata?.UniqueMonsters.Definitions ?? independent.UniqueDefinitions,
            metadata?.RuleForDepth(depth).MonsterDensityDivisor ?? independent.DensityDivisor,
            metadata?.ShrineAvailable ?? row.SelectedRoomCatalog.ShrineAvailable, itemCatalog,
            new(depth), new(MaximumLifeBase: 64, MaximumManaBase: 0), freshItems,
            Enumerable.Range(0, 127).ToArray(), 0, new HashSet<int>(), initialTiles, metadata?.PotionDefinitions ?? potions);

        var actual = AuthoritativeCathedralFullPopulationGenerator.Generate(request);

        Assert.Equal(row.PostClearRandomState, actual.ClearedMonsterPool.FinalRandomState);
        Assert.Equal(row.SelectedThemes, actual.InitialPopulation.Themes.Themes);
        var generatedBestiary = actual.InitialPopulation.Bestiary.Entries.Select(value =>
            new AuthoritativeCathedralThemeRoomMonsterDefinition(value.PopulationDefinition,
                checked((uint)value.PlaceFlags), AuthoritativeCathedralThemeRoomGenerator.IsNativeGoat(value.NativeType))).ToArray();
        Assert.Equal(row.SelectedRoomCatalog.OrderedBestiary.Count, generatedBestiary.Length);
        for (var index = 0; index < generatedBestiary.Length; index++) {
            var expected = row.SelectedRoomCatalog.OrderedBestiary[index]; var generated = generatedBestiary[index];
            Assert.Equal(expected.PlaceFlags, generated.PlaceFlags);
            Assert.Equal(expected.IsGoat, generated.IsGoat);
            Assert.Equal(expected.Definition with { Frames = generated.Definition.Frames, Rates = generated.Definition.Rates }, generated.Definition);
            Assert.Equal(expected.Definition.Frames, generated.Definition.Frames);
            Assert.Equal(expected.Definition.Rates, generated.Definition.Rates);
        }
        Assert.Equal(independent.Parameters.ObjectTemplate.Objects, row.SelectedRoomCatalog.Objects);
        EqualWorld(row.Snapshot + ": after InitItems", row.AfterItems,
            new(actual.BeforeThemeRooms, actual.InitialItems.FinalRandomState,
                actual.InitialPopulation.Themes.PlacementOrigin, actual.InitialPopulation.Themes.PlacementVariant));
        EqualWorld(row.Snapshot + ": after CreateThemeRooms", row.Expected,
            new(actual.State, actual.FinalRandomState, actual.ThemeRooms.FinalThemeOrigin, actual.ThemeRooms.FinalThemeVar1));
        Assert.Equal(row.VisibleTransparencyRegions, actual.Encounters.VisibleTransparencyRegions);
        Assert.Equal(row.ShowUniqueInfo, actual.InitialItems.ShowUniqueItemInfoBox);
        Assert.Equal(row.ItemRecordCount, actual.InitialItems.ItemGetRecordCount);
        Assert.Equal(row.ItemRecords, actual.InitialItems.ItemGetRecords);
    }

    /** Test-only adapter exports raw catalog inputs, never selected/expected world records. */
    [Fact(Explicit = true)]
    public void ExportCompleteMetadataInputs()
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY");
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024));
        var geometryDirectory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_METADATA_GEOMETRY_DIRECTORY");
        var sourceDirectory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_METADATA_SOURCE_DIRECTORY");
        Assert.Empty(Directory.EnumerateFileSystemEntries(sourceDirectory));
        var geometryPack = ContentPackLoader.LoadDirectory("explicit-geometry", "1.0.0", geometryDirectory);
        Assert.Equal(new[] { AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath }.Order(StringComparer.Ordinal),
            geometryPack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal));
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            geometryPack.Tables.Single(table => table.SourcePath == AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath).ToCanonicalTsv(),
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            geometryPack.Tables.Single(table => table.SourcePath == AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath).ToCanonicalTsv());
        var inputs = Enumerable.Range(1, 4).Select(depth =>
            AuthoritativeCathedralEncounterNativeOracleTests.LoadIndependentPopulationCase(depth, 2588, "main")).ToArray();
        var first = inputs[0].Parameters;
        // The test-only offline adapter proves semantic SOL corrections against
        // independent native effective inputs, then publishes effective values.
        // Production loaders never guess or reinterpret these properties.
        var firstFine = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(1,
            AuthoritativeCathedralDepthGenerator.Generate(1, 2588, AuthoritativeCathedralEntry.Main),
            geometry.Pieces, geometry.TileProperties, "cathedral-metadata-proof", false, false).Level;
        Assert.Equal(first.PreThemeGeometry.PieceProperties.Select(value => (AuthoritativeDunTileFlags)(value & ~0x40)),
            firstFine.NativePieceProperties.OrderBy(row => row.Key).Select(row => row.Value));
        geometry = new(geometry.Pieces, new AuthoritativeDunTilePropertyCatalog(
            first.PreThemeGeometry.PieceProperties.Select((value, index) =>
                new AuthoritativeDunTileProperty((uint)index, (AuthoritativeDunTileFlags)(value & ~0x40)))));
        var rules = inputs.Select(row => new AuthoritativeCathedralPopulationDepthRule(row.Parameters.Depth,
            row.DensityDivisor, row.Parameters.ObjectTemplate.TrapPercent, 190)).ToArray();
        var shrines = corpus.Cases[0].SelectedRoomCatalog.ShrineAvailable;
        Assert.All(corpus.Cases, row => Assert.Equal(shrines, row.SelectedRoomCatalog.ShrineAvailable));
        var tables = AuthoritativeCathedralPopulationMetadata.CreateTables(geometry, first.PreThemeGeometry.PieceProperties,
            first.BestiaryParameters.Catalog, first.ObjectTemplate.Objects, inputs[0].UniqueDefinitions,
            corpus.ItemCatalog, rules, first.ObjectTemplate.StoryMessages, shrines);
        foreach (var row in inputs.Skip(1)) {
            var other = AuthoritativeCathedralPopulationMetadata.CreateTables(geometry, row.Parameters.PreThemeGeometry.PieceProperties,
                row.Parameters.BestiaryParameters.Catalog, row.Parameters.ObjectTemplate.Objects, row.UniqueDefinitions,
                corpus.ItemCatalog, rules, row.Parameters.ObjectTemplate.StoryMessages, shrines);
            Assert.Equal(tables.Select(table => table.ToCanonicalTsv()), other.Select(table => table.ToCanonicalTsv()));
        }
        foreach (var table in tables)
            File.WriteAllText(Path.Combine(sourceDirectory, table.SourcePath),
                table.ToCanonicalTsv().Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false, true));
        Assert.Equal(AuthoritativeCathedralPopulationMetadata.TableNames,
            Directory.EnumerateFiles(sourceDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    private sealed record Case(string Snapshot, int Depth, uint Seed, string Entry,
        uint PreClearRandomState, uint PostClearRandomState, World Baseline,
        AuthoritativeCathedralThemeRoomCatalog SelectedRoomCatalog,
        AuthoritativeCathedralThemeEntry[] SelectedThemes, World AfterItems, World Expected,
        bool[] VisibleTransparencyRegions, bool ShowUniqueInfo, int ItemRecordCount,
        AuthoritativeCathedralInitialItemGetRecord[] ItemRecords);
    private sealed record Corpus(AuthoritativeCathedralThemeItemCatalog ItemCatalog, Case[] Cases);

    private static Corpus ReadCorpus(JsonElement root)
    {
        Properties(root, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana",
            "itemCatalog", "cases", "caseCount", "multiRoomCases");
        if (S(root, "format") != "godevilution-cathedral-full-population" || N(root, "version") != 1
            || S(root, "policy") != "controlled-original-diablo-single-player-normal-post-createlevel-through-createthemerooms"
            || N(root, "playerMaximumLife") != 64 || N(root, "playerMaximumMana") != 0 || N(root, "caseCount") != 16)
            throw new InvalidDataException("The controlled ordinary Cathedral full-population corpus is required.");
        var catalog = ReadCatalog(root.GetProperty("itemCatalog"));
        if (catalog.Items.Count != 168 || catalog.Prefixes.Count != 83 || catalog.Suffixes.Count != 95
            || catalog.Uniques.Count != 90 || catalog.Spells.Count != 37)
            throw new InvalidDataException("Full original Diablo raw item metadata is required.");
        var rows = A(root, "cases").EnumerateArray().Select(ReadCase).ToArray();
        ValidateMatrix(rows.Select(row => row.Snapshot).ToArray());
        var multiple = rows.Count(row => row.SelectedThemes.Length > 1);
        if (multiple == 0 || N(root, "multiRoomCases") != multiple)
            throw new InvalidDataException("The recorded multi-room coverage must match actual selected rows.");
        return new(catalog, rows);
    }

    private static Case ReadCase(JsonElement value)
    {
        Properties(value, "snapshot", "depth", "seed", "entry", "preClearRandomState", "postClearRandomState",
            "baseline", "selectedRoomCatalog", "selectedThemes", "afterItems", "itemInitialization", "visibleTransparencyRegions", "expected");
        var depth = N(value, "depth"); var seed = U(value, "seed"); var entry = S(value, "entry");
        if (depth is < 1 or > 4 || seed is not (2588U or 743271966U) || entry is not ("main" or "previous")
            || S(value, "snapshot") != $"cathedral-population-depth-{depth}-{seed}-{entry}.json")
            throw new InvalidDataException("A full-population case is outside the exact ordinary matrix.");
        var themes = A(value, "selectedThemes").EnumerateArray().Select(row => {
            if (row.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Selected themes require integer pairs.");
            var values = row.EnumerateArray().Select(Integer).ToArray();
            if (values.Length != 2 || !ThemeTypes.Contains(values[0]) || values[1] is < 1 or > 126)
                throw new InvalidDataException("Selected themes exceed the ordinary Cathedral scope.");
            return new AuthoritativeCathedralThemeEntry(values[0], values[1]);
        }).ToArray();
        if (themes.Length > 50 || themes.Select(row => row.RegionId).Distinct().Count() != themes.Length
            || !themes.Select(row => row.RegionId).SequenceEqual(themes.Select(row => row.RegionId).Order()))
            throw new InvalidDataException("Selected theme regions must retain native source order.");
        var visibility = A(value, "visibleTransparencyRegions").EnumerateArray().Select(row => row.ValueKind switch {
            JsonValueKind.True => true, JsonValueKind.False => false,
            _ => throw new InvalidDataException("Visibility evidence requires Boolean tokens."),
        }).ToArray();
        if (visibility.Length != 256) throw new InvalidDataException("All native transparency visibility entries are required.");
        var initialization = value.GetProperty("itemInitialization");
        Properties(initialization, "showUniqueInfo", "recordCount", "records");
        var records = A(initialization, "records").EnumerateArray().Select(row => {
            if (row.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Item-get records require four integer tokens.");
            var fields = row.EnumerateArray().Select(Integer).ToArray();
            if (fields.Length != 4 || fields.Any(field => field != 0))
                throw new InvalidDataException("Ordinary InitItems must clear all four item-get record fields.");
            return new AuthoritativeCathedralInitialItemGetRecord();
        }).ToArray();
        if (records.Length != 127 || B(initialization, "showUniqueInfo") || N(initialization, "recordCount") != 0)
            throw new InvalidDataException("Ordinary InitItems must reset all pickup records and unique-item UI state.");
        return new(S(value, "snapshot"), depth, seed, entry, U(value, "preClearRandomState"), U(value, "postClearRandomState"),
            ReadWorld(value.GetProperty("baseline")), ReadRoomCatalog(value.GetProperty("selectedRoomCatalog"), false),
            themes, ReadWorld(value.GetProperty("afterItems")), ReadWorld(value.GetProperty("expected")), visibility,
            B(initialization, "showUniqueInfo"), N(initialization, "recordCount"), records);
    }

    private static void ValidateMatrix(IReadOnlyList<string> names)
    {
        var expected = (from depth in Enumerable.Range(1, 4) from seed in new uint[] { 2588, 743271966 }
            from entry in new[] { "main", "previous" } select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").ToArray();
        if (!names.SequenceEqual(expected)) throw new InvalidDataException("The exact ordered sixteen-case matrix is required.");
    }

    [Theory]
    [InlineData("float-seed")]
    [InlineData("entry")]
    [InlineData("snapshot")]
    [InlineData("unsupported-theme")]
    [InlineData("duplicate-theme")]
    [InlineData("reordered-themes")]
    [InlineData("visibility-length")]
    [InlineData("visibility-token")]
    [InlineData("record-count")]
    [InlineData("record-shape")]
    [InlineData("record-float")]
    [InlineData("unique-ui")]
    [InlineData("extra-property")]
    public void CaseRefusesOutOfScopeAndMalformedEvidence(string mutation)
    {
        // Mutations fail before any deliberately absent world/catalog payload
        // is read. Complete nested-world guards are shared with the room reader.
        var row = JsonNode.Parse("""
            {"snapshot":"cathedral-population-depth-1-2588-main.json","depth":1,"seed":2588,"entry":"main",
             "preClearRandomState":0,"postClearRandomState":0,"baseline":{},"selectedRoomCatalog":{},
             "selectedThemes":[],"afterItems":{},"expected":{},"visibleTransparencyRegions":[],
             "itemInitialization":{"showUniqueInfo":false,"recordCount":0,"records":[]}}
            """)!.AsObject();
        row["visibleTransparencyRegions"] = new JsonArray(Enumerable.Range(0, 256)
            .Select(_ => (JsonNode?)JsonValue.Create(false)).ToArray());
        var initialization = row["itemInitialization"]!.AsObject();
        initialization["records"] = new JsonArray(Enumerable.Range(0, 127)
            .Select(_ => (JsonNode?)new JsonArray(0, 0, 0, 0)).ToArray());
        switch (mutation) {
            case "float-seed": row["seed"] = JsonNode.Parse("2588.0"); break;
            case "entry": row["entry"] = "load"; break;
            case "snapshot": row["snapshot"] = "replacement.json"; break;
            case "unsupported-theme": row["selectedThemes"] = JsonNode.Parse("[[10,1]]"); break;
            case "duplicate-theme": row["selectedThemes"] = JsonNode.Parse("[[0,1],[1,1]]"); break;
            case "reordered-themes": row["selectedThemes"] = JsonNode.Parse("[[0,2],[1,1]]"); break;
            case "visibility-length": row["visibleTransparencyRegions"]!.AsArray().RemoveAt(0); break;
            case "visibility-token": row["visibleTransparencyRegions"]![0] = 0; break;
            case "record-count": initialization["recordCount"] = 1; break;
            case "record-shape": initialization["records"]![0] = JsonNode.Parse("[0,0,0]"); break;
            case "record-float": initialization["records"]![0]![0] = JsonNode.Parse("0.0"); break;
            case "unique-ui": initialization["showUniqueInfo"] = true; break;
            case "extra-property": row["unexpected"] = 1; break;
        }
        using var document = JsonDocument.Parse(row.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadCase(document.RootElement));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("reordered")]
    public void MatrixRefusesMissingDuplicateAndReorderedEvidence(string mutation)
    {
        var names = (from depth in Enumerable.Range(1, 4) from seed in new uint[] { 2588, 743271966 }
            from entry in new[] { "main", "previous" } select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").ToList();
        if (mutation == "missing") names.RemoveAt(0);
        else if (mutation == "duplicate") names[1] = names[0];
        else (names[0], names[1]) = (names[1], names[0]);
        Assert.Throws<InvalidDataException>(() => ValidateMatrix(names));
    }
}
