using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Compares ordinary untrapped L1 chest generation with the native 36-case corpus. */
public sealed class AuthoritativeCathedralChestLootNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_CATHEDRAL_CHEST_ORACLE_DIRECTORY";
    private const string FileName = "native-chest-operations.json";
    private const int MaximumBytes = 32 * 1024 * 1024;

    [Theory(Explicit = true)]
    [InlineData(1)]
    public void OrdinaryChestLootMatchesNative(int depth)
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException($"Set {DirectoryVariable} to the captured native chest oracle directory.");
        directory = Path.GetFullPath(directory);
        Assert.True(Directory.Exists(directory), "Native chest oracle directory must exist.");
        Assert.Equal(new[] { FileName }, Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!).OrderBy(value => value, StringComparer.Ordinal));
        var path = Path.Combine(directory, FileName);
        var length = new FileInfo(path).Length;
        Assert.InRange(length, 1L, (long)MaximumBytes);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions {
            MaxDepth = 64,
        });
        var corpus = document.RootElement;
        Properties(corpus, "format", "version", "playerMaximumLife", "playerMaximumMana", "catalog", "cases", "caseCount");
        Assert.Equal("godevilution-cathedral-chest-operations", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        Assert.Equal(168, catalog.Items.Count);
        Assert.Equal(36, N(corpus, "caseCount"));
        var cases = A(corpus, "cases").EnumerateArray().ToArray();
        Assert.Equal(36, cases.Length);

        foreach (var value in cases) {
            Properties(value, "name", "depth", "slot", "levelLoading", "inputChest", "input", "expectedChest", "expected",
                "repeatedChest", "repeated");
            Assert.Equal(depth, N(value, "depth"));
            Assert.Equal(S(value, "name") != "ordinary-open", B(value, "levelLoading"));
            var chest = value.GetProperty("inputChest");
            Properties(chest, "slot", "type", "position", "applyLighting", "animFlag", "animDelay", "animCount",
                "animLength", "animFrame", "animWidth", "deleteFlag", "breakFlag", "solidFlag", "missilePassable",
                "selectionRegion", "preFlag", "trapFlag", "doorFlag", "lightId", "rndSeed", "var1", "var2",
                "var3", "var4", "var5", "var6", "bookMessage", "var8");
            var slot = N(value, "slot");
            Assert.Equal(slot, N(chest, "slot"));
            Assert.False(B(chest, "trapFlag"));
            var position = chest.GetProperty("position");
            Properties(position, "x", "y");
            var input = AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadInput(value.GetProperty("input"));
            var inputSnapshot = AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(
                input, U(value.GetProperty("input"), "randomState"));
            var request = new AuthoritativeCathedralChestLootRequest(
                input,
                catalog,
                new AuthoritativeCathedralThemeItemPolicy(depth),
                (AuthoritativeCathedralOrdinaryChestType)N(chest, "type"),
                U(chest, "rndSeed"),
                N(position, "x"),
                N(position, "y"),
                N(chest, "var1"),
                N(chest, "var2"),
                B(chest, "trapFlag"),
                new(true, B(value, "levelLoading"), N(corpus, "playerMaximumMana"), N(corpus, "playerMaximumLife"))) {
                    ChestObjectSlot = slot,
                };

            var actual = AuthoritativeCathedralChestLootGenerator.Generate(request);
            var expected = AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadState(
                value.GetProperty("expected"), inputs: false);
            var actualState = AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(
                actual.WorldState, actual.FinalRandomState);
            for (var itemSlot = 0; itemSlot < expected.Items.Length; itemSlot++) {
                if (expected.Items[itemSlot] == actualState.Items[itemSlot])
                    continue;
                var expectedFields = JsonSerializer.SerializeToNode(expected.Items[itemSlot])!.AsObject();
                var actualFields = JsonSerializer.SerializeToNode(actualState.Items[itemSlot])!.AsObject();
                var differences = expectedFields.Where(field => !JsonNode.DeepEquals(field.Value, actualFields[field.Key]))
                    .Select(field => $"{field.Key}: native={field.Value}, managed={actualFields[field.Key]}");
                Assert.Fail($"type={N(chest, "type")}/branch={N(chest, "var2")}/{S(value, "name")}/slot={itemSlot}: {string.Join("; ", differences)}");
            }
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(
                S(value, "name"), expected, actualState);
            Assert.Equal(expected.Count - input.ActiveItemCount,
                actual.WorldState.ActiveItemCount - input.ActiveItemCount);
            Assert.Equal(U(chest, "rndSeed"), actual.InitialRandomState);
            Assert.Equal(expected.RandomState, actual.FinalRandomState);
            Assert.True(input.ObjectCanInteract[slot], "The caller's closed-chest input must remain unchanged.");
            Assert.False(actual.WorldState.ObjectCanInteract[slot], "Native item placement must see the chest already unselected.");
            Assert.Equal(input.ObjectCanInteract.Where((_, index) => index != slot),
                actual.WorldState.ObjectCanInteract.Where((_, index) => index != slot));
            Assert.Equal(input.ObjectRefs, actual.WorldState.ObjectRefs);
            Assert.Equal(input.ObjectSolid, actual.WorldState.ObjectSolid);
            Assert.Equal(input.SolidTiles, actual.WorldState.SolidTiles);
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(
                "caller unchanged", inputSnapshot,
                AuthoritativeCathedralThemeItemWorldNativeOracleTests.Snapshot(
                    input, U(value.GetProperty("input"), "randomState")));
            AssertChestOpenProjection(chest, value.GetProperty("expectedChest"));
            Assert.Equal(JsonNode.Parse(value.GetProperty("expectedChest").GetRawText())!.ToJsonString(),
                JsonNode.Parse(value.GetProperty("repeatedChest").GetRawText())!.ToJsonString());
            AuthoritativeCathedralThemeItemWorldNativeOracleTests.EqualState(
                "repeated native chest operation", expected,
                AuthoritativeCathedralThemeItemWorldNativeOracleTests.ReadState(
                    value.GetProperty("repeated"), inputs: false));
        }
    }

    [Fact]
    public void PreparedChestLootCommitsOnceAndSurvivesCheckpointRestore()
    {
        var profile = CreateRuntimeProfileWithNativeChest();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var request = CreateRuntimeRequest(runtime);
        var first = runtime.PrepareChestLoot(1, request);
        var stale = runtime.PrepareChestLoot(1, request);
        var nextEntityId = 0x50000000u;
        var openCount = 0;
        var committed = runtime.CommitChestLoot(
            first,
            () => nextEntityId++,
            _ => false,
            () => true,
            () => openCount++);

        Assert.NotNull(committed);
        Assert.Equal(1, openCount);
        Assert.NotEmpty(committed.Projections);
        var checkpoint = runtime.CaptureCheckpoint();
        var level = Assert.Single(checkpoint.Levels);
        Assert.Equal(1, level.ActiveItemCount);
        Assert.Equal(0x50000000u, level.SlotEntityBindings[0].EntityId);
        Assert.Equal(committed.Projections[0].Raw, level.Items[0]);

        var restored = new AuthoritativeCathedralItemRuntime(profile, checkpoint);
        AssertCheckpointEqual(checkpoint, restored.CaptureCheckpoint());

        var allocatorAfterCommit = nextEntityId;
        Assert.Throws<InvalidOperationException>(() => runtime.CommitChestLoot(
            stale,
            () => nextEntityId++,
            _ => false,
            () => true,
            () => openCount++));
        Assert.Equal(allocatorAfterCommit, nextEntityId);
        Assert.Equal(1, openCount);
        AssertCheckpointEqual(checkpoint, runtime.CaptureCheckpoint());
    }

    [Fact]
    public void RejectedChestOpenLeavesPoolAndUniqueHistoryUnchanged()
    {
        var profile = CreateRuntimeProfileWithNativeChest();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var before = runtime.CaptureCheckpoint();
        var transaction = runtime.PrepareChestLoot(1, CreateRuntimeRequest(runtime));
        var entityId = 0x50000000u;
        var openCount = 0;

        var result = runtime.CommitChestLoot(
            transaction,
            () => entityId++,
            _ => false,
            () => false,
            () => openCount++);

        Assert.Null(result);
        Assert.Equal(0x50000000u, entityId);
        Assert.Equal(0, openCount);
        AssertCheckpointEqual(before, runtime.CaptureCheckpoint());
    }

    private static AuthoritativeCathedralItemRuntimeProfile CreateRuntimeProfileWithNativeChest()
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.Profile();
        var bindings = source.ItemCatalog.Items.ToDictionary(
            item => item.NativeItemIndex,
            item => new AuthoritativeCathedralItemContentBinding(
                item.NativeItemIndex,
                checked((uint)(5000 + item.NativeItemIndex)),
                $"native_item_{item.NativeItemIndex}"));
        var objectRefs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        objectRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = 1;
        var level = new AuthoritativeCathedralItemLevelProfile(
            1,
            1,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            objectRefs,
            [new AuthoritativeCathedralItemObjectBinding(
                0, 900, (int)AuthoritativeCathedralOrdinaryChestType.Chest1,
                50, 50, false, 1, MutationPolicy: AuthoritativeCathedralItemObjectMutationPolicy.NativeChest)]);
        return source with {
            ItemBindings = bindings,
            LevelProfiles = [level],
        };
    }

    private static AuthoritativeCathedralChestLootRequest CreateRuntimeRequest(
        AuthoritativeCathedralItemRuntime runtime)
    {
        var level = runtime.Profile.LevelProfiles.Single(value => value.LevelId == 1);
        var occupancy = new AuthoritativeCathedralItemOccupancyContext(
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            level.InitialObjectRefs,
            level.SolidTiles,
            level.SetPieceTiles,
            [false],
            [true]);
        var worldState = runtime.BuildWorldState(1, occupancy);
        return new(
            worldState,
            runtime.Profile.ItemCatalog,
            runtime.Profile.ItemPolicy,
            AuthoritativeCathedralOrdinaryChestType.Chest1,
            2,
            50,
            50,
            1,
            1,
            Player: new(true, false, 3200, 6400)) {
                ChestObjectSlot = 0,
            };
    }

    private static void AssertCheckpointEqual(
        DiabloCathedralItemCheckpoint expected,
        DiabloCathedralItemCheckpoint actual)
    {
        Assert.Equal(expected.MetadataFingerprint, actual.MetadataFingerprint);
        Assert.Equal(expected.GenerationFingerprint, actual.GenerationFingerprint);
        Assert.Equal(expected.UniqueItemFlags, actual.UniqueItemFlags);
        Assert.Equal(expected.Levels.Count, actual.Levels.Count);
        for (var index = 0; index < expected.Levels.Count; index++) {
            var left = expected.Levels[index];
            var right = actual.Levels[index];
            Assert.Equal(left.LevelId, right.LevelId);
            Assert.Equal(left.Depth, right.Depth);
            Assert.Equal(left.ActiveItemCount, right.ActiveItemCount);
            Assert.Equal(left.ActiveItemIds, right.ActiveItemIds);
            Assert.Equal(left.SignedItemRefs, right.SignedItemRefs);
            Assert.Equal(left.SlotEntityBindings, right.SlotEntityBindings);
            Assert.Equal(left.RandomState, right.RandomState);
            Assert.Equal(left.GenerationIdentity, right.GenerationIdentity);
            Assert.Equal(left.Items.Count, right.Items.Count);
            for (var slot = 0; slot < left.Items.Count; slot++)
                Assert.Equal(left.Items[slot], right.Items[slot]);
        }
    }

    private static void AssertChestOpenProjection(JsonElement input, JsonElement expected)
    {
        var projection = JsonNode.Parse(input.GetRawText())!;
        projection["animFrame"] = N(input, "animFrame") + 2;
        projection["selectionRegion"] = 0;
        var expectedNode = JsonNode.Parse(expected.GetRawText())!;
        Assert.Equal(expectedNode.ToJsonString(), projection.ToJsonString());
    }
}
