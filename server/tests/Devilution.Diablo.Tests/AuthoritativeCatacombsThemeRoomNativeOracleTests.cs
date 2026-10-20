using System.Text.Json;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Controlled L2 theme seams supplement the ordinary generated-map corpus, without qualifying floor selection. */
public sealed class AuthoritativeCatacombsThemeRoomNativeOracleTests
{
    private const string FileName = "native-catacombs-theme-rooms.json";
    private static readonly int[] Types = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16];
    private static readonly string[] EdgeNames = ["ordered-racks", "object-full-armor", "object-full-weapon",
        "item-full-gold", "item-full-minimum-gold", "item-full-nongold"];
    private static readonly Lazy<JsonElement> Corpus = new(LoadCorpus);
    public static TheoryData<int, string> Cases {
        get {
            var result = new TheoryData<int, string>();
            foreach (var depth in new[] { 5, 8 })
                foreach (var name in Names())
                    result.Add(depth, name);
            return result;
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void CompleteCatacombsThemeBranchMatchesNative(int depth, string name)
    {
        var corpus = Corpus.Value;
        var row = corpus.GetProperty("cases").EnumerateArray().Single(value =>
            N(value, "depth") == depth && S(value, "name") == name);
        var itemCatalog = ReadCatalog(corpus.GetProperty("itemCatalog"));
        var catalog = ReadRoomCatalog(corpus.GetProperty("roomCatalog"));
        var input = ReadWorld(row.GetProperty("input"));
        var themes = row.GetProperty("themes").EnumerateArray().Select(value =>
            new AuthoritativeCathedralThemeEntry(value[0].GetInt32(), value[1].GetInt32())).ToArray();
        var expectedTypes = name == "ordered-racks" ? new[] { 10, 16, 10, 16 }
            : new[] { name.StartsWith("type-", StringComparison.Ordinal) ? int.Parse(name[5..])
                : name == "object-full-armor" ? 10 : name == "object-full-weapon" ? 16 : 4 };
        Assert.Equal(expectedTypes, themes.Select(value => value.ThemeType));
        Assert.All(themes, value => Assert.Equal(1, value.RegionId));
        Assert.Equal(2588U + (uint)depth * 17, U(row, "seed"));
        Assert.Equal(U(row, "seed"), input.RandomState);
        var player = row.GetProperty("player").Deserialize<AuthoritativeCathedralPopulationPlayer>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        Assert.Equal(new AuthoritativeCathedralPopulationPlayer(0, depth, true, false, new(60, 60), new(61, 60)), player);
        var before = JsonSerializer.Serialize(input.State);
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(new(input.State, catalog, itemCatalog,
            new(depth), themes, input.RandomState, player, new(MaximumLifeBase: 6400, MaximumManaBase: 3200),
            InitialThemeVar1: input.ThemeVar1, InitialThemeOrigin: input.Origin),
            AuthoritativeNativeDungeonFamilyContext.Catacombs(depth));
        EqualWorld($"L{depth}:{name}", ReadWorld(row.GetProperty("expected")),
            new(result.State, result.FinalRandomState, result.FinalThemeOrigin, result.FinalThemeVar1));
        Assert.Equal(before, JsonSerializer.Serialize(input.State));
        Assert.NotSame(input.State.ObjectSlots, result.State.ObjectSlots);
        Assert.NotSame(input.State.ItemSlots, result.State.ItemSlots);
    }

    private static JsonElement LoadCorpus()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_THEME_ROOM_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(configuredDirectory) || !Directory.Exists(configuredDirectory))
            throw new InvalidDataException("An explicit Catacombs controlled-theme reference directory is required.");
        var directory = Path.GetFullPath(configuredDirectory);
        for (var ancestor = new DirectoryInfo(directory); ancestor is not null; ancestor = ancestor.Parent)
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The controlled-theme reference directory has a reparse-point ancestor.");
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024);
        Assert.Equal("godevilution-catacombs-theme-rooms", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal("controlled-original-diablo-single-player-normal-catacombs-theme-branches", S(corpus, "policy"));
        Assert.Equal(6400, N(corpus, "playerMaximumLife"));
        Assert.Equal(3200, N(corpus, "playerMaximumMana"));
        Assert.Equal(44, N(corpus, "caseCount"));
        Assert.Equal(from depth in new[] { 5, 8 } from name in Names() select $"{depth}:{name}",
            corpus.GetProperty("cases").EnumerateArray().Select(value => $"{N(value, "depth")}:{S(value, "name")}"));
        return corpus;
    }

    private static IEnumerable<string> Names() => Types.Select(type => $"type-{type}").Concat(EdgeNames);
}
