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
        var request = actual.FullPopulationRequest;
        var geometry = actual.InitialPopulation.PreThemeGeometry;
        var baseline = ReadWorld(row.GetProperty("baseline")).State;
        Assert.Equal(seed, U(row, "preClearRandomState"));
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
        Assert.Equal(10, N(row, "playerIdleFrames"));
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
