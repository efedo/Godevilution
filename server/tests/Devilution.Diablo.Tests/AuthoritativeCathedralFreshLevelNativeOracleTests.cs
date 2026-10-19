using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godevilution.Tools;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Native outputs are comparison-only: production receives metadata, seeds and hero values. */
public sealed class AuthoritativeCathedralFreshLevelNativeOracleTests
{
    internal const string Policy = "fresh-cold-original-diablo-single-player-normal-clear-before-createlevel-through-createthemerooms";
    private const string FileName = "native-fresh-population.json";
    public static TheoryData<int, uint, string> Cases => AuthoritativeCathedralEncounterNativeOracleTests.Cases;

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void FreshLevelMatchesNative(int depth, uint seed, string entry)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_FRESH_POPULATION_ORACLE_DIRECTORY");
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024);
        ValidateHeader(corpus);
        var rows = A(corpus, "cases").EnumerateArray().ToArray();
        ValidateMatrix(rows.Select(row => (N(row, "depth"), U(row, "seed"), S(row, "entry"))).ToArray());
        var row = rows.Single(row => N(row, "depth") == depth && U(row, "seed") == seed && S(row, "entry") == entry);
        Properties(row, "depth", "seed", "entry", "snapshot", "preClearRandomState", "postClearRandomState",
            "nextTransparencyValue", "triggerPositions", "prePlayerRandomState", "playerIdleFrames",
            "postPlayerRandomState", "playerAnimation", "visionActive", "visionRecords", "updateVision", "baseline",
            "selectedRoomCatalog", "selectedThemes", "afterItems", "itemInitialization", "visibleTransparencyRegions", "expected");
        var metadata = CathedralPopulationMetadataCompiler.LoadDirectory(
            AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
                "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY"));
        var nativeItems = ReadCatalog(corpus.GetProperty("itemCatalog"));
        Assert.Equal(AuthoritativeCathedralThemeItemMetadata.CreateTables(nativeItems).Select(table => table.ToCanonicalTsv()),
            AuthoritativeCathedralThemeItemMetadata.CreateTables(metadata.Items.Catalog).Select(table => table.ToCanonicalTsv()));
        Assert.Equal(N(corpus, "multiRoomCases"), rows.Count(value => A(value, "selectedThemes").GetArrayLength() > 1));
        // Warrior/unarmed idleFrames=10 comes from classes/warrior/animations.tsv.
        // These resolved frames and Q6 values are independent fixture policy,
        // not selected/native-world inputs to the factory.
        var actual = AuthoritativeCathedralFreshLevelGenerator.Generate(metadata, depth, seed, 0x13579BDF,
            entry == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous,
            new(MaximumLifeBase: 6400, MaximumManaBase: 3200, LightRadius: 8, IdleFrames: 10,
                PreEntryFuture: new(75, 68), LevelChanging: true));
        VerifyFreshRow(row, metadata, actual, 10);
    }

    [Fact(Explicit = true)]
    public void PlayableRogueL1TupleMatchesNativeLoader()
    {
        const string fileName = "native-rogue-l1-population.json";
        const string inventoryFileName = "native-rogue-l1-inventory.tsv";
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_NATIVE_ROGUE_L1_ORACLE_DIRECTORY");
        var expectedEntries = new[] { fileName, inventoryFileName }.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedEntries, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory)
            .Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, fileName), 128 * 1024 * 1024);
        Properties(corpus, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "playerLightRadius",
            "storySeed", "playerFuture", "playerLevelChanging", "itemCatalog", "cases", "caseCount", "multiRoomCases");
        Assert.Equal("godevilution-cathedral-native-rogue-l1", S(corpus, "format"));
        Assert.Equal("fresh-cold-original-diablo-rogue-l1-main-seed2588", S(corpus, "policy"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal(2880, N(corpus, "playerMaximumLife"));
        Assert.Equal(1440, N(corpus, "playerMaximumMana"));
        Assert.Equal(8, N(corpus, "playerLightRadius"));
        Assert.Equal(0x13579BDFU, U(corpus, "storySeed"));
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(57, 40), Point(corpus.GetProperty("playerFuture")));
        Assert.True(B(corpus, "playerLevelChanging"));
        Assert.Equal(1, N(corpus, "caseCount"));
        var row = Assert.Single(A(corpus, "cases").EnumerateArray());
        Assert.Equal((1, 2588U, "main"), (N(row, "depth"), U(row, "seed"), S(row, "entry")));
        var metadata = CathedralPopulationMetadataCompiler.LoadDirectory(
            AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
                "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY"));
        var nativeItems = ReadCatalog(corpus.GetProperty("itemCatalog"));
        Assert.Equal(AuthoritativeCathedralThemeItemMetadata.CreateTables(nativeItems).Select(table => table.ToCanonicalTsv()),
            AuthoritativeCathedralThemeItemMetadata.CreateTables(metadata.Items.Catalog).Select(table => table.ToCanonicalTsv()));
        var footprints = ReadNativeInventoryFootprints(Path.Combine(directory, inventoryFileName));
        var selectableItems = nativeItems.Items.Where(item => item.Available && item.ItemType >= 0 && item.CursorGraphic != byte.MaxValue).ToArray();
        Assert.Equal(selectableItems.Select(item => item.NativeItemIndex), footprints.Select(value => value.NativeItemIndex));
        for (var index = 0; index < selectableItems.Length; index++) {
            var item = selectableItems[index];
            var footprint = footprints[index];
            Assert.Equal(12 + item.CursorGraphic, footprint.NativeCursorId);
            Assert.InRange(footprint.PixelWidth, 1, ushort.MaxValue);
            Assert.InRange(footprint.PixelHeight, 1, ushort.MaxValue);
            Assert.Equal(footprint.PixelWidth / 28, footprint.InventoryWidth);
            Assert.Equal(footprint.PixelHeight / 28, footprint.InventoryHeight);
        }
        var footprintIndices = footprints.Select(value => value.NativeItemIndex).ToHashSet();
        var actualGroundItemIndices = A(row.GetProperty("afterItems"), "items").EnumerateArray()
            .Where(item => N(item, "itemType") != -1).Select(item => N(item, "itemIndex")).Distinct().ToArray();
        Assert.NotEmpty(actualGroundItemIndices);
        Assert.All(actualGroundItemIndices, itemIndex => Assert.Contains(itemIndex, footprintIndices));
        Assert.Equal(N(corpus, "multiRoomCases"), A(row, "selectedThemes").GetArrayLength() > 1 ? 1 : 0);
        var actual = AuthoritativeCathedralFreshLevelGenerator.Generate(metadata, 1, 2588, 0x13579BDF,
            AuthoritativeCathedralEntry.Main, new(2880, 1440, 8, 8, new(57, 40), LevelChanging: true));
        VerifyFreshRow(row, metadata, actual, 8);
    }

    [Theory(Explicit = true)]
    [InlineData(0, "WARRIOR", 4480, 640, 10)]
    [InlineData(2, "SORCERER", 1920, 4480, 8)]
    public void PlayableClassL1TupleMatchesNativeLoader(int classId, string environmentClass,
        int lifeMaximum, int manaMaximum, int idleFrames)
    {
        const string fileName = "native-class-l1-population.json";
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            $"GODEVILUTION_CATHEDRAL_NATIVE_{environmentClass}_L1_ORACLE_DIRECTORY");
        Assert.Equal(new[] { fileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, fileName), 128 * 1024 * 1024);
        Properties(corpus, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "playerClassId", "playerLightRadius",
            "storySeed", "playerFuture", "playerLevelChanging", "itemCatalog", "cases", "caseCount", "multiRoomCases");
        Assert.Equal("godevilution-cathedral-native-class-l1", S(corpus, "format"));
        Assert.Equal("fresh-cold-original-diablo-class-l1-main-seed2588", S(corpus, "policy"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal(classId, N(corpus, "playerClassId"));
        Assert.Equal(lifeMaximum, N(corpus, "playerMaximumLife"));
        Assert.Equal(manaMaximum, N(corpus, "playerMaximumMana"));
        Assert.Equal(10, N(corpus, "playerLightRadius"));
        Assert.Equal(0x13579BDFU, U(corpus, "storySeed"));
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(57, 40), Point(corpus.GetProperty("playerFuture")));
        Assert.True(B(corpus, "playerLevelChanging"));
        Assert.Equal(1, N(corpus, "caseCount"));
        var row = Assert.Single(A(corpus, "cases").EnumerateArray());
        Assert.Equal((1, 2588U, "main"), (N(row, "depth"), U(row, "seed"), S(row, "entry")));
        var metadata = CathedralPopulationMetadataCompiler.LoadDirectory(
            Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY")
                ?? throw new InvalidDataException("An explicit validated population metadata pack is required."));
        var nativeItems = ReadCatalog(corpus.GetProperty("itemCatalog"));
        Assert.Equal(AuthoritativeCathedralThemeItemMetadata.CreateTables(nativeItems).Select(table => table.ToCanonicalTsv()),
            AuthoritativeCathedralThemeItemMetadata.CreateTables(metadata.Items.Catalog).Select(table => table.ToCanonicalTsv()));
        var actual = AuthoritativeCathedralFreshLevelGenerator.Generate(metadata, 1, 2588, 0x13579BDF,
            AuthoritativeCathedralEntry.Main, new(lifeMaximum, manaMaximum, 10, idleFrames, new(57, 40), LevelChanging: true));
        VerifyFreshRow(row, metadata, actual, idleFrames);
    }

    private static NativeInventoryFootprint[] ReadNativeInventoryFootprints(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > 4 * 1024 * 1024 || bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            throw new InvalidDataException("The native inventory footprint sidecar size or encoding is invalid.");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        if (!text.EndsWith("\r\n", StringComparison.Ordinal))
            throw new InvalidDataException("The native inventory footprint sidecar must use CRLF rows.");
        for (var index = 0; index < text.Length; index++)
            if (text[index] == '\n' && (index == 0 || text[index - 1] != '\r'))
                throw new InvalidDataException("The native inventory footprint sidecar contains a non-CRLF line ending.");
        var lines = text.Split("\r\n", StringSplitOptions.None);
        if (lines.Length < 3 || lines[^1].Length != 0
            || lines[0] != "native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height")
            throw new InvalidDataException("The native inventory footprint header or row framing is invalid.");
        var rows = new NativeInventoryFootprint[lines.Length - 2];
        var priorItemIndex = -1;
        for (var index = 1; index < lines.Length - 1; index++) {
            var fields = lines[index].Split('\t');
            if (fields.Length != 6)
                throw new InvalidDataException("A native inventory footprint row has the wrong number of fields.");
            var values = new int[6];
            for (var field = 0; field < fields.Length; field++)
                if (!int.TryParse(fields[field], NumberStyles.None, CultureInfo.InvariantCulture, out values[field]))
                    throw new InvalidDataException("A native inventory footprint field is not a canonical integer.");
            if (values[0] <= priorItemIndex || values[1] < 1 || values[2] < 1 || values[3] < 1 || values[4] < 1 || values[5] < 1
                || values[2] > ushort.MaxValue || values[3] > ushort.MaxValue)
                throw new InvalidDataException("Native inventory footprint rows must be ordered and have valid dimensions.");
            rows[index - 1] = new(values[0], values[1], values[2], values[3], values[4], values[5]);
            priorItemIndex = values[0];
        }
        return rows;
    }

    private sealed record NativeInventoryFootprint(
        int NativeItemIndex,
        int NativeCursorId,
        int PixelWidth,
        int PixelHeight,
        int InventoryWidth,
        int InventoryHeight);

    private static void VerifyFreshRow(JsonElement row, AuthoritativeCathedralPopulationMetadata metadata,
        AuthoritativeCathedralFreshLevelResult actual, int idleFrames)
    {
        Properties(row, "depth", "seed", "entry", "snapshot", "preClearRandomState", "postClearRandomState",
            "nextTransparencyValue", "triggerPositions", "prePlayerRandomState", "playerIdleFrames",
            "postPlayerRandomState", "playerAnimation", "visionActive", "visionRecords", "updateVision", "baseline",
            "selectedRoomCatalog", "selectedThemes", "afterItems", "itemInitialization", "visibleTransparencyRegions", "expected");
        var request = actual.FullPopulationRequest;
        var geometry = actual.InitialPopulation.PreThemeGeometry;
        var baseline = ReadWorld(row.GetProperty("baseline")).State;
        Assert.Equal(actual.Request.DungeonSeed, U(row, "preClearRandomState"));
        Assert.Equal(U(row, "postClearRandomState"), actual.MonsterClearFinalRandomState);
        Assert.Equal(N(row, "nextTransparencyValue"), actual.NextTransparencyValue);
        Assert.Equal(A(row, "triggerPositions").EnumerateArray().Select(Point), actual.TriggerPositions);
        Assert.Equal(baseline.Pieces, geometry.Pieces);
        Assert.Equal(baseline.Special, geometry.Special);
        Assert.Equal(baseline.Transparency.Select(value => unchecked((byte)value)), geometry.Transparency);
        Assert.Equal(baseline.PieceProperties, metadata.EffectivePieceProperties);
        Assert.Equal(baseline.TileFlags, request.InitialTileFlags);
        Assert.Equal(baseline.PlayerRefs, request.InitialPlayerRefs);
        Assert.Equal(baseline.MonsterRefs, request.InitialMonsterTileRefs);
        Assert.Equal(baseline.Corpses, request.InitialCorpses);
        Assert.Equal(baseline.Light, request.InitialLight);
        Assert.Equal(baseline.PreLight, request.InitialPreLight);
        Assert.Equal(baseline.ItemSlots, request.InitialItemSlots);
        Assert.Equal(baseline.ActiveItemIds, request.InitialActiveItemIds);
        Assert.Equal(baseline.ActiveLightCount, request.LightAllocator.ActiveLightCount);
        Assert.Equal(baseline.ActiveLightIds, request.LightAllocator.ActiveLightSlots);
        Assert.Equal(baseline.LightRecords, request.LightAllocator.LightRecords);
        Assert.Equal(baseline.UpdateLighting, request.LightAllocator.UpdateLighting);
        Assert.Equal(U(row, "prePlayerRandomState"), actual.Population.InitialPopulation.Themes.FinalRandomState);
        Assert.Equal(idleFrames, N(row, "playerIdleFrames"));
        Assert.Equal(U(row, "postPlayerRandomState"), actual.PlayerPreparation.FinalRandomState);
        var animation = A(row, "playerAnimation").EnumerateArray().ToArray();
        Assert.Equal(5, animation.Length);
        Assert.Equal(animation.Take(4).Select(Integer), new[] {
            actual.PlayerPreparation.AnimationCurrentFrame, actual.PlayerPreparation.AnimationTickCounter,
            actual.PlayerPreparation.IdleFrames, actual.PlayerPreparation.TicksPerFrame });
        Assert.Equal(animation[4].GetBoolean(), actual.PlayerPreparation.IsPetrified);
        Assert.Equal(A(row, "visionActive").EnumerateArray().Select(value => value.GetBoolean()), actual.VisionActive);
        Assert.Equal(A(row, "visionRecords").EnumerateArray().Select(Light), actual.VisionRecords);
        Assert.Equal(B(row, "updateVision"), actual.UpdateVision);
        var selected = A(row, "selectedThemes").EnumerateArray().Select(value => {
            var pair = value.EnumerateArray().Select(Integer).ToArray();
            Assert.Equal(2, pair.Length);
            return new AuthoritativeCathedralThemeEntry(pair[0], pair[1]);
        }).ToArray();
        Assert.Equal(selected, actual.Population.InitialPopulation.Themes.Themes);
        var roomCatalog = ReadRoomCatalog(row.GetProperty("selectedRoomCatalog"), false);
        var bestiary = actual.Population.InitialPopulation.Bestiary.Entries.Select(value =>
            new AuthoritativeCathedralThemeRoomMonsterDefinition(value.PopulationDefinition,
                checked((uint)value.PlaceFlags), AuthoritativeCathedralThemeRoomGenerator.IsNativeGoat(value.NativeType))).ToArray();
        Assert.Equal(roomCatalog.OrderedBestiary.Count, bestiary.Length);
        for (var index = 0; index < bestiary.Length; index++) {
            var expected = roomCatalog.OrderedBestiary[index]; var generated = bestiary[index];
            Assert.Equal(expected.PlaceFlags, generated.PlaceFlags);
            Assert.Equal(expected.IsGoat, generated.IsGoat);
            Assert.Equal(expected.Definition with { Frames = generated.Definition.Frames, Rates = generated.Definition.Rates }, generated.Definition);
            Assert.Equal(expected.Definition.Frames, generated.Definition.Frames);
            Assert.Equal(expected.Definition.Rates, generated.Definition.Rates);
        }
        Assert.Equal(metadata.Population.Objects, roomCatalog.Objects);
        Assert.Equal(metadata.ShrineAvailable, roomCatalog.ShrineAvailable);
        EqualWorld("fresh after InitItems", ReadWorld(row.GetProperty("afterItems")),
            new(actual.Population.BeforeThemeRooms, actual.Population.InitialItems.FinalRandomState,
                actual.Population.InitialPopulation.Themes.PlacementOrigin, actual.Population.InitialPopulation.Themes.PlacementVariant));
        EqualWorld("fresh after CreateThemeRooms", ReadWorld(row.GetProperty("expected")),
            new(actual.Population.State, actual.Population.FinalRandomState,
                actual.Population.ThemeRooms.FinalThemeOrigin, actual.Population.ThemeRooms.FinalThemeVar1));
        Assert.Equal(A(row, "visibleTransparencyRegions").EnumerateArray().Select(value => value.GetBoolean()),
            actual.Population.Encounters.VisibleTransparencyRegions);
        var initialization = row.GetProperty("itemInitialization");
        Properties(initialization, "showUniqueInfo", "recordCount", "records");
        Assert.Equal(B(initialization, "showUniqueInfo"), actual.Population.InitialItems.ShowUniqueItemInfoBox);
        Assert.Equal(N(initialization, "recordCount"), actual.Population.InitialItems.ItemGetRecordCount);
        var records = A(initialization, "records").EnumerateArray().Select(value => {
            var fields = value.EnumerateArray().Select(Integer).ToArray();
            Assert.Equal(4, fields.Length);
            Assert.All(fields, field => Assert.Equal(0, field));
            return new AuthoritativeCathedralInitialItemGetRecord();
        }).ToArray();
        Assert.Equal(127, records.Length);
        Assert.Equal(records, actual.Population.InitialItems.ItemGetRecords);
    }

    private static AuthoritativeCathedralPopulationPoint Point(JsonElement value)
    {
        Properties(value, "x", "y");
        return new(N(value, "x"), N(value, "y"));
    }

    private static AuthoritativeCathedralEncounterLightState Light(JsonElement value)
    {
        Properties(value, "lightId", "tile", "oldTile", "offsetX", "offsetY", "radius", "oldRadius", "isInvalid", "hasChanged");
        return new(N(value, "lightId"), Point(value.GetProperty("tile")), Point(value.GetProperty("oldTile")),
            N(value, "offsetX"), N(value, "offsetY"), N(value, "radius"), N(value, "oldRadius"), B(value, "isInvalid"), B(value, "hasChanged"));
    }

    internal static void ValidateHeader(JsonElement value)
    {
        Properties(value, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "playerLightRadius",
            "storySeed", "playerFuture", "playerLevelChanging", "itemCatalog", "cases", "caseCount", "multiRoomCases");
        if (S(value, "format") != "godevilution-cathedral-fresh-population" || N(value, "version") != 1
            || S(value, "policy") != Policy || N(value, "playerMaximumLife") != 6400
            || N(value, "playerMaximumMana") != 3200 || N(value, "playerLightRadius") != 8
            || U(value, "storySeed") != 0x13579BDF || N(value, "caseCount") != 16
            || N(value, "multiRoomCases") <= 0 || !B(value, "playerLevelChanging")
            || Point(value.GetProperty("playerFuture")) != new AuthoritativeCathedralPopulationPoint(75, 68))
            throw new InvalidDataException("The exact fresh-cold native load-order scope is required.");
    }

    internal static void ValidateMatrix(IReadOnlyList<(int Depth, uint Seed, string Entry)> rows)
    {
        var expected = (from depth in Enumerable.Range(1, 4) from seed in new uint[] { 2588, 743271966 }
            from entry in new[] { "main", "previous" } select (depth, seed, entry));
        if (!rows.SequenceEqual(expected))
            throw new InvalidDataException("The exact ordered sixteen-case fresh matrix is required.");
    }

    [Theory]
    [InlineData("format")]
    [InlineData("policy")]
    [InlineData("version")]
    [InlineData("playerMaximumLife")]
    [InlineData("playerMaximumMana")]
    [InlineData("playerLightRadius")]
    [InlineData("playerFuture")]
    [InlineData("playerLevelChanging")]
    [InlineData("storySeed")]
    [InlineData("caseCount")]
    [InlineData("multiRoomCases")]
    [InlineData("extra")]
    public void HeaderRejectsWrongScopeAndHeroBindings(string field)
    {
        var node = new JsonObject {
            ["format"] = "godevilution-cathedral-fresh-population", ["version"] = 1, ["policy"] = Policy,
            ["playerMaximumLife"] = 6400, ["playerMaximumMana"] = 3200, ["playerLightRadius"] = 8,
            ["storySeed"] = 0x13579BDF, ["itemCatalog"] = new JsonObject(), ["cases"] = new JsonArray(),
            ["playerFuture"] = new JsonObject { ["x"] = 75, ["y"] = 68 },
            ["playerLevelChanging"] = true,
            ["caseCount"] = 16, ["multiRoomCases"] = 1,
        };
        using (var valid = JsonDocument.Parse(node.ToJsonString())) ValidateHeader(valid.RootElement);
        node[field] = field switch {
            "format" => JsonValue.Create("godevilution-cathedral-full-population"),
            "policy" => JsonValue.Create("controlled-seam"),
            "playerFuture" => new JsonObject { ["x"] = 75, ["y"] = 69 },
            "playerLevelChanging" => JsonValue.Create(false),
            _ => JsonValue.Create(0),
        };
        using var invalid = JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ValidateHeader(invalid.RootElement));
    }

    [Fact]
    public void MatrixRejectsMissingDuplicateAndReorderedCases()
    {
        var rows = (from depth in Enumerable.Range(1, 4) from seed in new uint[] { 2588, 743271966 }
            from entry in new[] { "main", "previous" } select (depth, seed, entry)).ToArray();
        ValidateMatrix(rows);
        Assert.Throws<InvalidDataException>(() => ValidateMatrix(rows[..^1]));
        Assert.Throws<InvalidDataException>(() => ValidateMatrix(rows.Reverse().ToArray()));
        rows[1] = rows[0];
        Assert.Throws<InvalidDataException>(() => ValidateMatrix(rows));
    }
}
