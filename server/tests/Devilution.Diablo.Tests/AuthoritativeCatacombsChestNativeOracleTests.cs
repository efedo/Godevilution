using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Native C2 operation results are comparison-only; production generation never reads this corpus. */
public sealed class AuthoritativeCatacombsChestNativeOracleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Theory(Explicit = true)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void ChestLootObjectMutationAndIntrinsicTrapPlanMatchNative(int depth)
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_CHEST_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidDataException("An explicit native Catacombs chest comparison directory is required.");
        var directory = Path.GetFullPath(configured);
        Assert.True(Directory.Exists(directory));
        const string file = "native-catacombs-chest-operations.json";
        Assert.Equal(new[] { file }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory)
            .Select(Path.GetFileName));
        Assert.InRange(new FileInfo(Path.Combine(directory, file)).Length, 1, 64L * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, file)),
            new JsonDocumentOptions { MaxDepth = 64 });
        var corpus = document.RootElement;
        Properties(corpus, "format", "version", "playerMaximumLife", "playerMaximumMana", "catalog", "roomCatalog", "cases", "caseCount");
        Assert.Equal("godevilution-catacombs-chest-operations", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        Assert.Equal(168, catalog.Items.Count);
        var cases = corpus.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(30, N(corpus, "caseCount"));
        Assert.Equal(30, cases.Length);
        Assert.Equal(cases.Length, cases.Select(row => S(row, "name")).Distinct().Count());
        Assert.Equal(new[] { 5, 6, 7, 68, 69, 70 }, cases.Where(row => N(row, "depth") == depth)
            .Select(row => N(row, "rawType")).Distinct().Order());
        foreach (var value in cases.Where(row => N(row, "depth") == depth)) {
            var name = S(value, "name");
            var chest = ReadObject(value.GetProperty("inputChest"));
            var wall = value.GetProperty("inputLinkedWallTrap").ValueKind == JsonValueKind.Null
                ? null : ReadObject(value.GetProperty("inputLinkedWallTrap"));
            var inputJson = value.GetProperty("input");
            var input = AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadInput(inputJson);
            var before = AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(input, U(inputJson, "randomState"));
            var facts = new AuthoritativeCatacombsNativeChestFacts(chest.Slot, chest.Type,
                chest.Position.X, chest.Position.Y, chest.RndSeed, chest.Var1, chest.Var2, chest.Var4,
                chest.TrapFlag, chest.AnimFrame) { LinkedWallTrap = wall };
            var result = AuthoritativeCatacombsChestLootGenerator.Generate(new(input, catalog,
                new(depth), facts, N(value, "remotePlayerX"), N(value, "remotePlayerY"),
                new(true, true, N(corpus, "playerMaximumMana"), N(corpus, "playerMaximumLife"))),
                AuthoritativeNativeDungeonFamilyContext.Catacombs(depth));
            var expected = AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadState(
                value.GetProperty("expected"), inputs: false);
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(name, expected,
                AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(result.Loot.WorldState, result.Loot.FinalRandomState));
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState("caller unchanged", before,
                AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(input, U(inputJson, "randomState")));
            var mutation = result.OpenedMutation;
            Assert.Equal(ReadObject(value.GetProperty("expectedChest")), chest with {
                AnimFrame = mutation.AnimationFrame, SelectionRegion = mutation.SelectionRegion,
                TrapFlag = mutation.TrapFlag,
            });
            Assert.False(result.Loot.WorldState.ObjectCanInteract[chest.Slot]);
            Assert.Equal(input.ObjectRefs, result.Loot.WorldState.ObjectRefs);
            Assert.Equal(input.ObjectSolid, result.Loot.WorldState.ObjectSolid);
            Assert.Equal(input.SolidTiles, result.Loot.WorldState.SolidTiles);
            Assert.Equal(input.SetPieceTiles, result.Loot.WorldState.SetPieceTiles);
            var missiles = value.GetProperty("expectedMissiles").EnumerateArray().ToArray();
            Assert.Empty(value.GetProperty("inputMissiles").EnumerateArray());
            if (result.TrapPlan is { } trap) {
                var missile = Assert.Single(missiles);
                Assert.Equal(trap.Kind == AuthoritativeCatacombsChestTrapKind.Arrow ? 0 : 27, N(missile, "type"));
                Assert.Equal(trap.Direction, N(value, "trapDirectionArgument"));
                Assert.Equal((trap.SourceX, trap.SourceY), (N(missile, "startX"), N(missile, "startY")));
                Assert.Equal((trap.TargetX, trap.TargetY),
                    (N(missile, "destinationArgumentX"), N(missile, "destinationArgumentY")));
                Assert.Equal(trap.OwnerId, N(missile, "source"));
                Assert.Equal(trap.TargetsPlayers ? 1 : 0, N(missile, "ownerKind"));
                Assert.Equal(trap.Damage, N(missile, "damage"));
                Assert.Equal(0, N(missile, "spellLevel"));
            } else {
                Assert.Empty(missiles);
            }
            Assert.Equal(ReadObject(value.GetProperty("expectedChest")), ReadObject(value.GetProperty("repeatedChest")));
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState("repeat native operation", expected,
                AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadState(value.GetProperty("repeated"), inputs: false));
            Assert.Equal(value.GetProperty("expectedMissiles").GetRawText(), value.GetProperty("repeatedMissiles").GetRawText());
            Assert.Equal(value.GetProperty("inputLinkedWallTrap").GetRawText(), value.GetProperty("expectedLinkedWallTrap").GetRawText());
            Assert.Equal(value.GetProperty("inputActiveObjectIds").GetRawText(), value.GetProperty("repeatedActiveObjectIds").GetRawText());
        }
    }

    private static AuthoritativeCathedralPopulationObjectState ReadObject(JsonElement value)
        => value.Deserialize<AuthoritativeCathedralPopulationObjectState>(JsonOptions)
            ?? throw new InvalidDataException("A retained native chest object row is required.");
}
