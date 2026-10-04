using System.Text.Json;
using Godevilution.Tools;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Native worlds are comparison-only; generation receives metadata, seeds and explicit hero context. */
public sealed class AuthoritativeCathedralLevelReadyNativeOracleTests
{
    internal const string Policy = "fresh-cold-original-diablo-single-player-normal-post-theme-through-light-vision-before-join";
    internal const string FileName = "native-level-ready.json";
    public static TheoryData<int, uint, string> Cases => AuthoritativeCathedralEncounterNativeOracleTests.Cases;

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void LevelReadyMatchesNative(int depth, uint seed, string entry)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_CATHEDRAL_LEVEL_READY_ORACLE_DIRECTORY");
        Assert.Equal(new[] { AuthoritativeCathedralAutomapMetadata.TableName, FileName },
            AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024);
        ValidateHeader(corpus);
        var rows = A(corpus, "cases").EnumerateArray().ToArray();
        AuthoritativeCathedralFreshLevelNativeOracleTests.ValidateMatrix(
            rows.Select(row => (N(row, "depth"), U(row, "seed"), S(row, "entry"))).ToArray());
        var row = rows.Single(row => N(row, "depth") == depth && U(row, "seed") == seed && S(row, "entry") == entry);
        Properties(row, "depth", "seed", "entry", "snapshot", "preClearRandomState", "postClearRandomState",
            "nextTransparencyValue", "triggerPositions", "prePlayerRandomState", "playerIdleFrames",
            "postPlayerRandomState", "playerAnimation", "visionActive", "visionRecords", "updateVision", "baseline",
            "selectedRoomCatalog", "selectedThemes", "afterItems", "itemInitialization", "visibleTransparencyRegions", "expected", "ready");
        var metadata = CathedralPopulationMetadataCompiler.LoadDirectory(
            AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
                "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY"));
        var automap = AuthoritativeCathedralAutomapMetadata.LoadUtf8(
            File.ReadAllBytes(Path.Combine(directory, AuthoritativeCathedralAutomapMetadata.TableName)));
        Assert.Equal(automap.Fingerprint, AuthoritativeCathedralAutomapMetadata.Load(automap.ToContentPack()).Fingerprint);
        var fresh = AuthoritativeCathedralFreshLevelGenerator.Generate(metadata, depth, seed, 0x13579BDF,
            entry == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous,
            new(6400, 3200, 8, 10, new(75, 68), true));
        var before = JsonSerializer.Serialize(fresh.Population.State);
        var result = AuthoritativeCathedralLevelReadyGenerator.Generate(new(fresh,
            new(255, true, true, true, 6400, new(13, 17), new(0, 0), true), automap, portalsAbsent: true));
        Assert.Equal(before, JsonSerializer.Serialize(fresh.Population.State));
        EqualWorld("before loader handoff", ReadWorld(row.GetProperty("expected")),
            new(fresh.Population.State, fresh.Population.FinalRandomState,
                fresh.Population.ThemeRooms.FinalThemeOrigin, fresh.Population.ThemeRooms.FinalThemeVar1));
        var expected = row.GetProperty("ready");
        Properties(expected, "world", "corpses", "stoneCorpseId", "bestiaryCorpseIds", "deathAnimations", "player",
            "spellFlags", "infravision", "autoMapShowItems", "missileCount", "missilePreFlag", "automapExploration",
            "visibleTransparencyRegions", "updateVision", "visionActive", "visionRecords");
        EqualWorld("after loader handoff", ReadWorld(expected.GetProperty("world")),
            new(result.State, result.FinalRandomState,
                fresh.Population.ThemeRooms.FinalThemeOrigin, fresh.Population.ThemeRooms.FinalThemeVar1));
        Assert.Equal(fresh.Population.FinalRandomState, result.FinalRandomState);
        Assert.Equal(A(expected, "corpses").EnumerateArray().Select((value, slot) => {
            var fields = value.EnumerateArray().Select(Integer).ToArray();
            Assert.Equal(3, fields.Length);
            return new AuthoritativeCathedralLevelReadyCorpseState(slot, fields[0], fields[1], fields[2]);
        }), result.Corpses);
        Assert.Equal(N(expected, "stoneCorpseId"), result.StoneCorpseId);
        Assert.Equal(A(expected, "bestiaryCorpseIds").EnumerateArray().Select(Integer), result.BestiaryCorpseIds);
        var deaths = A(expected, "deathAnimations").EnumerateArray().ToArray();
        Assert.Equal(fresh.Population.InitialPopulation.Bestiary.Entries.Count, deaths.Length);
        for (var index = 0; index < deaths.Length; index++)
            Assert.Equal(deaths[index].EnumerateArray().Select(Integer), new[] {
                fresh.Population.InitialPopulation.Bestiary.Entries[index].Frames[4], fresh.Population.InitialPopulation.Bestiary.Entries[index].Widths[4] });
        var player = expected.GetProperty("player");
        Properties(player, "tile", "future", "old", "last", "temp", "view", "id", "level", "currentLife",
            "lightId", "lightRadius", "active", "levelChanging");
        Assert.Equal(Point(player.GetProperty("tile")), result.Player.Tile);
        Assert.Equal(Point(player.GetProperty("future")), result.Player.Future);
        Assert.Equal(Point(player.GetProperty("old")), result.Player.Old);
        Assert.Equal(Point(player.GetProperty("last")), result.Player.Last);
        Assert.Equal(Point(player.GetProperty("temp")), result.Player.Temp);
        Assert.Equal(Point(player.GetProperty("view")), result.Player.Tile);
        Assert.Equal(N(player, "id"), result.Player.Id);
        Assert.Equal(N(player, "level"), result.Player.Level);
        Assert.Equal(N(player, "currentLife"), result.Player.LifeCurrentQ6);
        Assert.Equal(N(player, "lightId"), result.Player.LightId);
        Assert.Equal(N(player, "lightRadius"), result.Player.LightRadius);
        Assert.Equal(B(player, "active"), result.Player.Active);
        Assert.Equal(B(player, "levelChanging"), result.Player.LevelChanging);
        Assert.True(result.Player.LevelChanging);
        Assert.Equal(N(expected, "spellFlags"), result.SpellFlags);
        Assert.Equal(B(expected, "infravision"), result.Infravision);
        Assert.Equal(B(expected, "autoMapShowItems"), result.AutoMapShowItems);
        Assert.Equal(B(expected, "missilePreFlag"), result.MissilePreFlag);
        Assert.Equal(0, N(expected, "missileCount"));
        Assert.True(result.MissilesCleared);
        Assert.Equal(A(expected, "automapExploration").EnumerateArray().Select(Integer), result.AutomapExploration.Select(value => (int)value));
        Assert.Contains((byte)4, result.AutomapExploration);
        Assert.Equal(A(expected, "visibleTransparencyRegions").EnumerateArray().Select(value => value.GetBoolean()), result.VisibleTransparencyRegions);
        Assert.Equal(B(expected, "updateVision"), result.UpdateVision);
        Assert.Equal(A(expected, "visionActive").EnumerateArray().Select(value => value.GetBoolean()), result.VisionActive);
        Assert.Equal(A(expected, "visionRecords").EnumerateArray().Select(Light), result.VisionRecords);
        Assert.Equal(AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin, result.Stage);
    }

    internal static void ValidateHeader(JsonElement value)
    {
        Properties(value, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "playerLightRadius",
            "storySeed", "playerFuture", "playerLevelChanging", "coldPlayer", "itemCatalog", "cases", "caseCount", "multiRoomCases");
        if (S(value, "format") != "godevilution-cathedral-level-ready" || N(value, "version") != 1
            || S(value, "policy") != Policy || N(value, "playerMaximumLife") != 6400
            || N(value, "playerMaximumMana") != 3200 || N(value, "playerLightRadius") != 8
            || U(value, "storySeed") != 0x13579BDF || N(value, "caseCount") != 16
            || N(value, "multiRoomCases") <= 0 || !B(value, "playerLevelChanging")
            || Point(value.GetProperty("playerFuture")) != new AuthoritativeCathedralPopulationPoint(75, 68))
            throw new InvalidDataException("The exact cold post-theme native loader scope is required.");
        var context = value.GetProperty("coldPlayer");
        Properties(context, "spellFlags", "infravision", "autoMapShowItems", "missilePreFlag", "currentLife",
            "priorMissilesEmpty", "portalsAbsent", "last", "temp");
        if (N(context, "spellFlags") != 255 || !B(context, "infravision") || !B(context, "autoMapShowItems")
            || !B(context, "missilePreFlag") || N(context, "currentLife") != 6400
            || !B(context, "priorMissilesEmpty") || !B(context, "portalsAbsent")
            || Point(context.GetProperty("last")) != new AuthoritativeCathedralPopulationPoint(13, 17)
            || Point(context.GetProperty("temp")) != new AuthoritativeCathedralPopulationPoint(0, 0))
            throw new InvalidDataException("Explicit native cold hero and missile inputs are required.");
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
}
