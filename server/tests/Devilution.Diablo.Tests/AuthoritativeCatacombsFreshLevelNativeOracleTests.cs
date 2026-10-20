using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Data;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Complete phases consume source metadata, fixed seeds and prepared hero inputs; native worlds are comparison-only. */
public sealed class AuthoritativeCatacombsFreshLevelNativeOracleTests
{
    private const uint StorySeed = 0x1357_9BDF;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Lazy<AuthoritativeCatacombsAutomapMetadata> Automap = new(LoadAutomap);
    public static TheoryData<string, string, int, int, string, uint, bool> Cases
        => AuthoritativeCatacombsInitialPopulationNativeOracleTests.Cases;

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void CompleteFreshAndReadyPhasesMatchNative(
        string file, string characterClass, int characterClassId, int depth, string entry, uint seed, bool retainedUniqueHistory)
    {
        var (_, metadata) = AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs();
        using var document = AuthoritativeCatacombsInitialPopulationNativeOracleTests.OpenQualifiedCase(file);
        var row = document.RootElement;
        Assert.Equal(characterClass, S(row, "characterClass"));
        Assert.Equal(characterClassId, N(row, "characterClassId"));
        Assert.Equal(depth, N(row, "levelId"));
        Assert.Equal(seed, U(row, "inputSeed"));
        Assert.Equal(entry, S(row, "entry"));
        Assert.Equal(retainedUniqueHistory, B(row, "retainedUniqueHistory"));
        var life = characterClassId == 0 ? 4480 : 1920;
        var mana = characterClassId == 0 ? 640 : 4480;
        var idle = characterClassId == 0 ? 10 : 8;
        var hero = new AuthoritativeCatacombsFreshLevelHeroContext(life, mana, 10, idle, new(75, 68), true);
        var playerInput = row.GetProperty("playerGenerationInput");
        Assert.Equal(life, N(playerInput, "lifeMaximumBase"));
        Assert.Equal(mana, N(playerInput, "manaMaximumBase"));
        Assert.Equal(10, N(playerInput, "lightRadius"));
        Assert.Equal(1, N(playerInput, "characterLevel"));
        Assert.True(B(playerInput, "active"));
        Assert.True(B(playerInput, "levelChanging"));
        Assert.Equal(new(75, 68), Point(playerInput.GetProperty("positionFuture")));

        // Retained cases start with a separately prepared flag7, then use actual earlier
        // managed floors' outputs. Expected native history never feeds generation.
        IReadOnlySet<int> history = retainedUniqueHistory ? new HashSet<int> { 7 } : new HashSet<int>();
        if (retainedUniqueHistory) {
            for (var priorDepth = 5; priorDepth < depth; priorDepth++) {
                var prior = AuthoritativeCatacombsFreshLevelGenerator.Generate(metadata, priorDepth, seed, StorySeed,
                    AuthoritativeCatacombsEntry.Main, hero, initialUniqueItemFlags: history);
                history = prior.Population.State.UniqueItemFlags;
            }
        }
        AssertFlags(row.GetProperty("uniqueItemHistory").GetProperty("inputFlags"), history);
        var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(metadata, depth, seed, StorySeed,
            Entry(entry), hero, initialUniqueItemFlags: history);
        Assert.Equal(history.Order(), fresh.Request.InitialUniqueItemFlags.Order());
        Assert.Equal(U(row, "attemptSeed"), fresh.Geometry.GenerationResult.AttemptSeed);
        var boundaries = row.GetProperty("rngBoundaries");
        Assert.Equal(U(boundaries, "beforeCreateLevel"), fresh.MonsterClearFinalRandomState);
        Assert.Equal(U(boundaries, "afterCreateLevel"), fresh.Geometry.GenerationResult.FinalRandomState);
        Assert.Equal(U(boundaries, "afterGetLevelMTypes"), fresh.Population.InitialPopulation.Bestiary.FinalRandomState);
        Assert.Equal(U(boundaries, "afterInitThemes"), fresh.Population.InitialPopulation.Themes.FinalRandomState);
        Assert.Equal(U(boundaries, "beforeInitPlayer"), fresh.PlayerPreparation.RandomStateBefore);
        Assert.Equal(U(boundaries, "afterInitPlayer"), fresh.PlayerPreparation.FinalRandomState);
        var animation = row.GetProperty("playerBeforePopulation").GetProperty("animation");
        Assert.Equal(N(animation, "currentFrame"), fresh.PlayerPreparation.AnimationCurrentFrame);
        Assert.Equal(N(animation, "tickCounterOfCurrentFrame"), fresh.PlayerPreparation.AnimationTickCounter);
        Assert.Equal(N(animation, "frameCount"), fresh.PlayerPreparation.IdleFrames);
        Assert.Equal(N(animation, "ticksPerFrame"), fresh.PlayerPreparation.TicksPerFrame);
        Assert.Equal(B(animation, "isPetrified"), fresh.PlayerPreparation.IsPetrified);
        var stages = row.GetProperty("rngAfterPopulationStages");
        Assert.Equal(U(stages, "afterInitObjects"), fresh.Population.InitialPopulation.Population.FinalRandomState);
        Assert.Equal(U(stages, "afterInitMonsters"), fresh.Population.Encounters.FinalRandomState);
        Assert.Equal(U(stages, "afterInitItems"), fresh.Population.InitialItems.FinalRandomState);
        Assert.Equal(U(stages, "afterCreateThemeRooms"), fresh.Population.FinalRandomState);
        CompareEncounterStage(row.GetProperty("worldAfterInitMonsters"), fresh.Population.Encounters);
        EqualWorld(file + ": InitItems", ReadWorld(row.GetProperty("worldAfterInitItems")),
            new(fresh.Population.BeforeThemeRooms, fresh.Population.InitialItems.FinalRandomState,
                fresh.Population.InitialPopulation.Themes.PlacementOrigin, fresh.Population.InitialPopulation.Themes.PlacementVariant));
        EqualWorld(file + ": CreateThemeRooms", ReadWorld(row.GetProperty("worldAfterThemeCreation")),
            new(fresh.Population.State, fresh.Population.FinalRandomState,
                fresh.Population.ThemeRooms.FinalThemeOrigin, fresh.Population.ThemeRooms.FinalThemeVar1));
        AssertFlags(row.GetProperty("uniqueItemHistoryAfterItems"), fresh.Population.BeforeThemeRooms.UniqueItemFlags);
        AssertFlags(row.GetProperty("uniqueItemHistoryOutput"), fresh.Population.State.UniqueItemFlags);
        var itemReset = row.GetProperty("populationResetRecords").GetProperty("itemInitialization");
        Assert.Equal(B(itemReset, "showUniqueInfo"), fresh.Population.InitialItems.ShowUniqueItemInfoBox);
        Assert.Equal(N(itemReset, "recordCount"), fresh.Population.InitialItems.ItemGetRecordCount);
        Assert.Equal(itemReset.GetProperty("records").EnumerateArray().Select(value =>
            string.Join(',', value.EnumerateArray().Select(field => field.GetInt64()))),
            fresh.Population.InitialItems.ItemGetRecords.Select(value =>
                $"{value.Seed},{value.CreateInfo},{value.ItemIndex},{value.Timestamp}"));

        var before = JsonSerializer.Serialize(fresh.Population.State, JsonOptions);
        // InitPlayer has already reset Temp; Last remains the prepared independent scalar.
        var ready = AuthoritativeCatacombsLevelReadyGenerator.Generate(new(fresh,
            new(0, false, false, true, life, new(13, 17), new(0, 0), true), Automap.Value, portalsAbsent: true));
        Assert.Equal(before, JsonSerializer.Serialize(fresh.Population.State, JsonOptions));
        var expected = row.GetProperty("ready");
        EqualWorld(file + ": ready", ReadWorld(expected.GetProperty("world")),
            new(ready.State, ready.FinalRandomState,
                fresh.Population.ThemeRooms.FinalThemeOrigin, fresh.Population.ThemeRooms.FinalThemeVar1));
        Assert.Equal(fresh.Population.FinalRandomState, ready.FinalRandomState);
        Assert.Equal(U(row.GetProperty("rngAfterLevelReady"), "afterReadySequence"), ready.FinalRandomState);
        Assert.Equal(expected.GetProperty("corpses").EnumerateArray().Select((value, slot) =>
            new AuthoritativeCathedralLevelReadyCorpseState(slot, value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32())), ready.Corpses);
        Assert.Equal(N(expected, "stoneCorpseId"), ready.StoneCorpseId);
        Assert.Equal(expected.GetProperty("bestiaryCorpseIds").EnumerateArray().Select(value => value.GetInt32()), ready.BestiaryCorpseIds);
        var deaths = expected.GetProperty("deathAnimations");
        Assert.Equal(fresh.Population.InitialPopulation.Bestiary.Entries.Count, deaths.GetArrayLength());
        for (var index = 0; index < deaths.GetArrayLength(); index++) {
            Assert.Equal(deaths[index][0].GetInt32(), fresh.Population.InitialPopulation.Bestiary.Entries[index].Frames[4]);
            Assert.Equal(deaths[index][1].GetInt32(), fresh.Population.InitialPopulation.Bestiary.Entries[index].Widths[4]);
        }
        ComparePlayer(expected.GetProperty("player"), ready.Player);
        Assert.Equal(N(expected, "spellFlags"), ready.SpellFlags);
        Assert.Equal(B(expected, "infravision"), ready.Infravision);
        Assert.Equal(B(expected, "autoMapShowItems"), ready.AutoMapShowItems);
        Assert.Equal(B(expected, "missilePreFlag"), ready.MissilePreFlag);
        Assert.Equal(0, N(expected, "missileCount"));
        Assert.True(ready.MissilesCleared);
        Assert.Equal(expected.GetProperty("automapExploration").EnumerateArray().Select(value => value.GetByte()), ready.AutomapExploration);
        Assert.Equal(expected.GetProperty("visibleTransparencyRegions").EnumerateArray().Select(value => value.GetBoolean()), ready.VisibleTransparencyRegions);
        Assert.Equal(B(expected, "updateVision"), ready.UpdateVision);
        Assert.Equal(expected.GetProperty("visionActive").EnumerateArray().Select(value => value.GetBoolean()), ready.VisionActive);
        Assert.Equal(expected.GetProperty("visionRecords").EnumerateArray().Select(value =>
            value.Deserialize<AuthoritativeCathedralEncounterLightState>(JsonOptions)!), ready.VisionRecords);
        Assert.Equal(AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin, ready.Stage);
    }

    private static void CompareEncounterStage(JsonElement expectedWorld, AuthoritativeCathedralEncounterResult actual)
    {
        var expected = ReadWorld(expectedWorld);
        var state = expected.State;
        Assert.Equal(expected.RandomState, actual.FinalRandomState);
        Assert.Equal(state.ActiveMonsterCount, actual.ActiveMonsterCount);
        Assert.Equal(state.MonsterTargetCount, actual.TotalMonsters);
        Assert.Equal(state.ActiveMonsterIds, actual.ActiveMonsterIds);
        Assert.Equal(state.MonsterSlots, actual.MonsterSlots);
        Assert.Equal(state.MonsterRefs, actual.MonsterTileRefs);
        Assert.Equal(state.TileFlags, actual.TileFlags);
        Assert.Equal(state.ActiveLightCount, actual.ActiveLightCount);
        Assert.Equal(state.ActiveLightIds, actual.ActiveLightSlots);
        Assert.Equal(state.LightRecords, actual.LightRecords);
        Assert.Equal(state.UpdateLighting, actual.UpdateLighting);
        Assert.Equal(state.DisableLighting, actual.DisableLighting);
    }

    private static void ComparePlayer(JsonElement expected, AuthoritativeCathedralLevelReadyPlayerState actual)
    {
        Assert.Equal(Point(expected.GetProperty("tile")), actual.Tile);
        Assert.Equal(Point(expected.GetProperty("future")), actual.Future);
        Assert.Equal(Point(expected.GetProperty("old")), actual.Old);
        Assert.Equal(Point(expected.GetProperty("last")), actual.Last);
        Assert.Equal(Point(expected.GetProperty("temp")), actual.Temp);
        Assert.Equal(Point(expected.GetProperty("view")), actual.Tile);
        Assert.Equal(N(expected, "id"), actual.Id);
        Assert.Equal(N(expected, "level"), actual.Level);
        Assert.Equal(N(expected, "currentLife"), actual.LifeCurrentQ6);
        Assert.Equal(N(expected, "lightId"), actual.LightId);
        Assert.Equal(N(expected, "lightRadius"), actual.LightRadius);
        Assert.Equal(B(expected, "active"), actual.Active);
        Assert.Equal(B(expected, "levelChanging"), actual.LevelChanging);
    }

    private static AuthoritativeCatacombsAutomapMetadata LoadAutomap()
    {
        using var source = AuthoritativeCatacombsInitialPopulationNativeOracleTests.OpenQualifiedSource();
        Assert.Equal("godevilution-native-catacombs-population-sources", S(source.RootElement, "format"));
        Assert.Equal(1, N(source.RootElement, "version"));
        Assert.Equal(320, N(source.RootElement, "automapSourceByteCount"));
        Assert.Equal(160, N(source.RootElement, "automapWordCount"));
        var words = source.RootElement.GetProperty("automapRawWordsLittleEndian").EnumerateArray()
            .Select(value => value.GetUInt16()).ToArray();
        var bytes = new byte[words.Length * 2];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        var tables = AuthoritativeCatacombsAutomapMetadata.CreateTables(words,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
            AuthoritativeCatacombsAutomapMetadata.CatalogId, AuthoritativeCatacombsAutomapMetadata.SchemaVersion,
            tables, schema: new ContentSchemaIdentity(AuthoritativeCatacombsAutomapMetadata.SchemaId,
                AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));
    }

    private static void AssertFlags(JsonElement expected, IReadOnlySet<int> actual)
    {
        var flags = expected.EnumerateArray().Select(value => value.GetBoolean()).ToArray();
        Assert.Equal(128, flags.Length);
        Assert.Equal(flags.Select((set, index) => (set, index)).Where(value => value.set).Select(value => value.index), actual.Order());
    }

    private static AuthoritativeCatacombsEntry Entry(string entry)
        => entry switch {
            "main" => AuthoritativeCatacombsEntry.Main,
            "previous" => AuthoritativeCatacombsEntry.Previous,
            "town-warp-down" => AuthoritativeCatacombsEntry.TownWarpDown,
            _ => throw new InvalidDataException("Unsupported native Catacombs entry."),
        };
    private static AuthoritativeCathedralPopulationPoint Point(JsonElement value) => new(N(value, "x"), N(value, "y"));
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException(name);
    private static int N(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static uint U(JsonElement value, string name) => value.GetProperty(name).GetUInt32();
    private static bool B(JsonElement value, string name) => value.GetProperty(name).GetBoolean();
}
