using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemWorldNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Compares detached sarcophagus item and linked-spawn plans to actual native operations. */
public sealed class AuthoritativeCathedralSarcophagusNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_CATHEDRAL_SARCOPHAGUS_ORACLE_DIRECTORY";
    private const string FileName = "native-sarcophagus-operations.json";
    private const int MaximumBytes = 64 * 1024 * 1024;
    private static readonly (int X, int Y)[] SpawnOffsets = [
        (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
    ];

    [Theory(Explicit = true)]
    [InlineData(1)]
    public void SarcophagusOperationsMatchNative(int depth)
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException($"Set {DirectoryVariable} to the captured native sarcophagus oracle directory.");
        directory = Path.GetFullPath(directory);
        Assert.True(Directory.Exists(directory), "Native sarcophagus oracle directory must exist.");
        Assert.Equal(new[] { FileName }, Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!).OrderBy(value => value, StringComparer.Ordinal));
        var path = Path.Combine(directory, FileName);
        Assert.InRange(new FileInfo(path).Length, 1L, MaximumBytes);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var corpus = document.RootElement;
        Properties(corpus, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "catalog", "roomCatalog", "cases", "caseCount");
        Assert.Equal("godevilution-cathedral-sarcophagus-operations", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal("original-diablo-single-player-normal-untrapped-cathedral-level1", S(corpus, "policy"));
        Assert.Equal(6400, N(corpus, "playerMaximumLife"));
        Assert.Equal(3200, N(corpus, "playerMaximumMana"));
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        var roomCatalog = corpus.GetProperty("roomCatalog");
        Properties(roomCatalog, "objects", "bestiary", "shrineAvailable");
        var cases = corpus.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(10, N(corpus, "caseCount"));
        Assert.Equal(10, cases.Length);
        Assert.Equal(10, cases.Select(value => S(value, "name")).Distinct(StringComparer.Ordinal).Count());

        foreach (var value in cases) {
            Properties(value, "name", "depth", "slot", "var1", "var2", "sarcophagusSeed", "levelLoading",
                "linkedMonsterSlot", "specialFrameCount", "specialTicksPerFrame", "spawnTilesAvailable",
                "inputSarcophagus", "input", "inputActiveObjectCount", "inputActiveObjectIds", "inputActiveMonsterIds",
                "inputLinkedMonster", "inputLinkedMonsterPetrified", "expectedRandomState", "expectedSarcophagus", "expected",
                "expectedActiveObjectCount", "expectedActiveObjectIds", "expectedActiveMonsterIds", "expectedSpawnTiles",
                "expectedLinkedMonster", "expectedLinkedMonsterPetrified", "repeatedSarcophagus", "repeated", "repeatedActiveMonsterIds",
                "repeatedActiveObjectCount", "repeatedActiveObjectIds", "repeatedLinkedMonster", "repeatedLinkedMonsterPetrified");
            Assert.Equal(depth, N(value, "depth"));
            var inputObject = value.GetProperty("inputSarcophagus");
            var expectedObject = value.GetProperty("expectedSarcophagus");
            var repeatedObject = value.GetProperty("repeatedSarcophagus");
            var slot = N(value, "slot");
            Assert.Equal(slot, N(inputObject, "slot"));
            Assert.Equal(48, N(inputObject, "type"));
            Assert.False(B(inputObject, "trapFlag"));
            Assert.Equal(N(value, "var1"), N(inputObject, "var1"));
            Assert.Equal(N(value, "var2"), N(inputObject, "var2"));
            Assert.Equal(U(value, "sarcophagusSeed"), U(inputObject, "rndSeed"));
            Assert.NotEqual(0, N(inputObject, "selectionRegion"));
            var position = inputObject.GetProperty("position");
            Properties(position, "x", "y");
            var x = N(position, "x");
            var y = N(position, "y");
            var input = ReadInput(value.GetProperty("input"));
            var inputRandomState = U(value.GetProperty("input"), "randomState");
            Assert.Equal(-(slot + 1), input.ObjectRefs[(y - 1) * AuthoritativeCathedralThemeItemWorldState.Width + x]);
            Assert.Equal(slot + 1, input.ObjectRefs[y * AuthoritativeCathedralThemeItemWorldState.Width + x]);
            Assert.True(input.ObjectCanInteract[slot]);
            var spawnTiles = value.GetProperty("spawnTilesAvailable").EnumerateArray().Select(tile => tile.GetBoolean()).ToArray();
            Assert.Equal(9, spawnTiles.Length);
            var linkedSlot = N(value, "linkedMonsterSlot");
            var linked = linkedSlot < 0 ? null : ReadLinkedMonster(value, roomCatalog, linkedSlot);
            Assert.Equal(linkedSlot >= 0, value.GetProperty("inputLinkedMonster").ValueKind != JsonValueKind.Null);
            var facts = new AuthoritativeCathedralNativeSarcophagusFacts(
                slot,
                checked((uint)(500_000 + slot)),
                N(inputObject, "type"),
                checked((uint)depth),
                new(x, y),
                U(inputObject, "rndSeed"),
                N(inputObject, "var1"),
                N(inputObject, "var2"),
                N(inputObject, "selectionRegion"),
                U(inputObject, "animFrame"),
                checked((uint)N(inputObject, "animFlag")),
                N(inputObject, "animDelay"),
                N(inputObject, "animCount"),
                U(inputObject, "animLength"),
                B(inputObject, "trapFlag"),
                false,
                linked,
                N(value, "specialFrameCount"),
                N(value, "specialTicksPerFrame"));
            var request = new AuthoritativeCathedralSarcophagusOperationRequest(
                input,
                catalog,
                new(depth),
                facts,
                spawnTiles,
                new(true, B(value, "levelLoading"), N(corpus, "playerMaximumMana"), N(corpus, "playerMaximumLife")));
            var before = Snapshot(input, inputRandomState);
            var actual = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(request);
            var expected = ReadState(value.GetProperty("expected"), inputs: false);
            EqualState(S(value, "name"), expected, Snapshot(actual.WorldState, actual.FinalRandomState));
            EqualState("caller unchanged", before, Snapshot(input, inputRandomState));
            Assert.Equal(U(inputObject, "rndSeed"), actual.InitialRandomState);
            Assert.Equal(U(value, "expectedRandomState"), actual.FinalRandomState);
            Assert.False(actual.WorldState.ObjectCanInteract[slot]);
            Assert.Equal(input.ObjectCanInteract.Where((_, index) => index != slot),
                actual.WorldState.ObjectCanInteract.Where((_, index) => index != slot));
            Assert.Equal(input.ObjectRefs, actual.WorldState.ObjectRefs);
            Assert.Equal(input.ObjectSolid, actual.WorldState.ObjectSolid);
            Assert.Equal(input.SolidTiles, actual.WorldState.SolidTiles);
            Assert.Equal(input.SetPieceTiles, actual.WorldState.SetPieceTiles);

            AssertOpenProjection(inputObject, expectedObject);
            Assert.Equal(JsonNode.Parse(expectedObject.GetRawText())!.ToJsonString(),
                JsonNode.Parse(repeatedObject.GetRawText())!.ToJsonString());
            EqualState("repeat-open", expected, ReadState(value.GetProperty("repeated"), inputs: false));
            Assert.Equal(A(value, "inputActiveMonsterIds").EnumerateArray().Select(Integer),
                A(value, "expectedActiveMonsterIds").EnumerateArray().Select(Integer));
            Assert.Equal(A(value, "expectedActiveMonsterIds").EnumerateArray().Select(Integer),
                A(value, "repeatedActiveMonsterIds").EnumerateArray().Select(Integer));
            Assert.Equal(N(value, "inputActiveObjectCount"), N(value, "expectedActiveObjectCount"));
            Assert.Equal(N(value, "expectedActiveObjectCount"), N(value, "repeatedActiveObjectCount"));
            Assert.Equal(A(value, "inputActiveObjectIds").EnumerateArray().Select(Integer),
                A(value, "expectedActiveObjectIds").EnumerateArray().Select(Integer));
            Assert.Equal(A(value, "expectedActiveObjectIds").EnumerateArray().Select(Integer),
                A(value, "repeatedActiveObjectIds").EnumerateArray().Select(Integer));
            var expectedSpawnTiles = value.GetProperty("expectedSpawnTiles").EnumerateArray().ToArray();
            Assert.Equal(9, expectedSpawnTiles.Length);
            foreach (var (tile, offset) in expectedSpawnTiles.Zip(SpawnOffsets)) {
                var tilePosition = tile.GetProperty("position");
                Assert.Equal(x + offset.X, N(tilePosition, "x"));
                Assert.Equal(y + offset.Y, N(tilePosition, "y"));
            }

            var expectedActorValue = value.GetProperty("expectedLinkedMonster");
            var repeatedActorValue = value.GetProperty("repeatedLinkedMonster");
            Assert.Equal(linkedSlot >= 0, expectedActorValue.ValueKind != JsonValueKind.Null);
            Assert.Equal(expectedActorValue.ValueKind, repeatedActorValue.ValueKind);
            if (linked is null) {
                Assert.Null(actual.Spawn);
            } else if (actual.Spawn is { } spawn) {
                Assert.Equal(spawnTiles[0], spawn.IsCenterSpawn);
                var actor = ReadMonsterState(expectedActorValue);
                Assert.Equal(linkedSlot, actor.Slot);
                Assert.Equal(spawn.NativeTypeId, actor.Type);
                Assert.Equal(spawn.PositionX, actor.Tile.X);
                Assert.Equal(spawn.PositionY, actor.Tile.Y);
                Assert.Equal(spawn.PositionX, actor.Future.X);
                Assert.Equal(spawn.PositionY, actor.Future.Y);
                Assert.Equal(spawn.Facing, actor.Direction);
                var spawnTile = expectedSpawnTiles.Single(tile => {
                    var tilePosition = tile.GetProperty("position");
                    return N(tilePosition, "x") == spawn.PositionX && N(tilePosition, "y") == spawn.PositionY;
                });
                Assert.Equal(spawn.NativeMonsterSlot + 1, N(spawnTile, "monster"));
                Assert.Equal(spawn.NativeMonsterSlot, linked.Binding.NativeSlot);
                Assert.Equal(spawn.EntityId, linked.Binding.EntityId);
                Assert.Equal(N(value, "specialFrameCount"), spawn.SpecialFrameCount);
                Assert.Equal(N(value, "specialTicksPerFrame"), spawn.SpecialTicksPerFrame);
            } else {
                Assert.Equal(JsonNode.Parse(value.GetProperty("inputLinkedMonster").GetRawText())!.ToJsonString(),
                    JsonNode.Parse(expectedActorValue.GetRawText())!.ToJsonString());
            }
            if (expectedActorValue.ValueKind != JsonValueKind.Null)
                Assert.Equal(JsonNode.Parse(expectedActorValue.GetRawText())!.ToJsonString(),
                    JsonNode.Parse(repeatedActorValue.GetRawText())!.ToJsonString());
        }
    }

    private static AuthoritativeNativeMonsterInitialization ReadLinkedMonster(
        JsonElement value,
        JsonElement roomCatalog,
        int linkedSlot)
    {
        var actorValue = value.GetProperty("inputLinkedMonster");
        var state = ReadMonsterState(actorValue);
        Assert.Equal(linkedSlot, state.Slot);
        var bestiary = roomCatalog.GetProperty("bestiary").EnumerateArray()
            .Single(row => N(row, "nativeType") == state.Type);
        var frames = bestiary.GetProperty("frames").EnumerateArray().Select(Integer).ToArray();
        var rates = bestiary.GetProperty("rates").EnumerateArray().Select(Integer).ToArray();
        Assert.Equal(N(value, "specialFrameCount"), frames[5]);
        Assert.Equal(N(value, "specialTicksPerFrame"), rates[5]);
        var uniqueType = state.UniqueType < 0 ? 255 : state.UniqueType;
        state = state with { UniqueType = uniqueType };
        var binding = new AuthoritativeNativeMonsterBinding(
            linkedSlot,
            state.Type,
            uniqueType,
            checked((uint)(700_000 + linkedSlot)),
            checked((uint)(800 + state.Type)),
            checked((uint)N(value, "depth")),
            uniqueType == 255 ? string.Empty : $"native_unique_{uniqueType}");
        return new(new string('A', 64), state, 0, 0, 0, 0, binding,
            B(value, "inputLinkedMonsterPetrified")) {
            NativeAttackFrameCount = frames[2],
            NativeAttackTicksPerFrame = rates[2],
            NativeHitRecoveryFrameCount = frames[3],
            NativeHitRecoveryTicksPerFrame = rates[3],
            NativeWalkFrameCount = frames[1],
            NativeWalkTicksPerFrame = rates[1],
        };
    }

    private static AuthoritativeCathedralPopulationMonsterState ReadMonsterState(JsonElement value)
    {
        Properties(value, "slot", "type", "levelType", "mode", "goal", "goalVar1", "goalVar2", "goalVar3",
            "var1", "var2", "var3", "pathCount", "tile", "future", "old", "last", "temp", "direction", "enemy",
            "enemyPosition", "invalid", "ai", "intelligence", "activeForTicks", "uniqueType", "uniqueTranslation",
            "corpseId", "whoHit", "minDamage", "maxDamage", "minDamageSpecial", "maxDamageSpecial", "armorClass",
            "resistance", "talkMessage", "leader", "leaderRelation", "packSize", "flags", "rndItemSeed", "aiSeed",
            "lightId", "hitPoints", "maxHitPoints", "animTicksPerFrame", "animTickCounter", "animNumberOfFrames",
            "animCurrentFrame", "golemToHit", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity",
            "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana");
        return new() {
            Slot = N(value, "slot"), Type = N(value, "type"), LevelType = N(value, "levelType"), Mode = N(value, "mode"),
            Goal = N(value, "goal"), GoalVar1 = N(value, "goalVar1"), GoalVar2 = N(value, "goalVar2"), GoalVar3 = N(value, "goalVar3"),
            Var1 = N(value, "var1"), Var2 = N(value, "var2"), Var3 = N(value, "var3"), PathCount = N(value, "pathCount"),
            Tile = ReadPoint(value.GetProperty("tile")), Future = ReadPoint(value.GetProperty("future")),
            Old = ReadPoint(value.GetProperty("old")), Last = ReadPoint(value.GetProperty("last")), Temp = ReadPoint(value.GetProperty("temp")),
            Direction = N(value, "direction"), Enemy = N(value, "enemy"), EnemyPosition = ReadPoint(value.GetProperty("enemyPosition")),
            Invalid = B(value, "invalid"), Ai = N(value, "ai"), Intelligence = N(value, "intelligence"),
            ActiveForTicks = N(value, "activeForTicks"), UniqueType = N(value, "uniqueType"), UniqueTranslation = N(value, "uniqueTranslation"),
            CorpseId = N(value, "corpseId"), WhoHit = N(value, "whoHit"), MinDamage = N(value, "minDamage"),
            MaxDamage = N(value, "maxDamage"), MinDamageSpecial = N(value, "minDamageSpecial"), MaxDamageSpecial = N(value, "maxDamageSpecial"),
            ArmorClass = N(value, "armorClass"), Resistance = N(value, "resistance"), TalkMessage = N(value, "talkMessage"),
            Leader = N(value, "leader"), LeaderRelation = N(value, "leaderRelation"), PackSize = N(value, "packSize"),
            Flags = U(value, "flags"), RndItemSeed = U(value, "rndItemSeed"), AiSeed = U(value, "aiSeed"), LightId = N(value, "lightId"),
            HitPoints = N(value, "hitPoints"), MaxHitPoints = N(value, "maxHitPoints"), AnimTicksPerFrame = N(value, "animTicksPerFrame"),
            AnimTickCounter = N(value, "animTickCounter"), AnimNumberOfFrames = N(value, "animNumberOfFrames"),
            AnimCurrentFrame = N(value, "animCurrentFrame"), GolemToHit = N(value, "golemToHit"),
            ReducePlayerStrength = N(value, "reducePlayerStrength"), ReducePlayerMagic = N(value, "reducePlayerMagic"),
            ReducePlayerDexterity = N(value, "reducePlayerDexterity"), ReducePlayerVitality = N(value, "reducePlayerVitality"),
            ReducePlayerMaxHP = N(value, "reducePlayerMaxHP"), ReducePlayerMaxMana = N(value, "reducePlayerMaxMana"),
        };
    }

    private static AuthoritativeCathedralPopulationPoint ReadPoint(JsonElement value)
    {
        Properties(value, "x", "y");
        return new(N(value, "x"), N(value, "y"));
    }

    private static void AssertOpenProjection(JsonElement input, JsonElement actual)
    {
        var expected = JsonNode.Parse(input.GetRawText())!.AsObject();
        expected["animFlag"] = 1;
        expected["animDelay"] = 3;
        expected["selectionRegion"] = 0;
        Assert.Equal(JsonNode.Parse(actual.GetRawText())!.ToJsonString(), expected.ToJsonString());
    }
}
