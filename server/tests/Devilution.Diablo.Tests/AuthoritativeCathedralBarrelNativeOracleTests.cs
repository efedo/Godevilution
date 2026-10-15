using System.Text.Json;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemWorldNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Compares detached native barrel loot/open/spawn semantics with real game calls. */
public sealed class AuthoritativeCathedralBarrelNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY";
    private const string FileName = "native-barrel-operations.json";
    private const int MaximumBytes = 64 * 1024 * 1024;

    [Theory(Explicit = true)]
    [InlineData(1)]
    public void BarrelOperationsMatchNative(int depth)
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException($"Set {DirectoryVariable} to the native barrel capture directory.");
        directory = Path.GetFullPath(directory);
        Assert.True(Directory.Exists(directory), "Native barrel capture directory must exist.");
        Assert.Equal(new[] { FileName }, Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        var path = Path.Combine(directory, FileName);
        Assert.InRange(new FileInfo(path).Length, 1L, MaximumBytes);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var corpus = document.RootElement;
        Properties(corpus, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "catalog", "roomCatalog", "cases", "caseCount");
        Assert.Equal("godevilution-cathedral-barrel-operations", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal("original-diablo-single-player-normal-untrapped-cathedral-level1", S(corpus, "policy"));
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        var roomCatalog = corpus.GetProperty("roomCatalog");
        var cases = A(corpus, "cases").EnumerateArray().ToArray();
        Assert.Equal(16, N(corpus, "caseCount"));
        Assert.Equal(16, cases.Length);
        Assert.Equal(16, cases.Select(value => S(value, "name")).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] {
            "explosive-chain-monster-player-order", "explosive-monster-fire-immune",
            "explosive-monster-fire-resisted", "explosive-monster-lethal-credited",
            "explosive-monster-lethal-credited-level-delta", "explosive-monster-lethal-uncredited",
            "explosive-player-blocked",
            "explosive-player-fire-resisted", "explosive-player-hit", "explosive-player-miss",
            "explosive-three-barrel-nw-monster-player-order", "ordinary-empty", "ordinary-linked-skeleton", "ordinary-linked-skeleton-signed-neighbor",
            "ordinary-random", "ordinary-useful",
        }, cases.Select(value => S(value, "name")).OrderBy(value => value, StringComparer.Ordinal));

        foreach (var value in cases) {
            Properties(value, "name", "depth", "slot", "initialRandomState", "barrelSeed", "nativeType",
                "var1", "var2", "var3", "var4", "levelLoading", "spawnTilesAvailable", "linkedMonsterSlot",
                "inputLinkedMonster", "lootActorContext", "inputLinkedMonsterPetrified", "specialFrameCount", "specialTicksPerFrame",
                "inputBarrel", "inputItems", "inputPlayer", "inputWorld", "expectedRandomState", "expectedBarrel",
                "expectedItems", "expectedPlayer", "expectedWorld", "repeatedBarrel", "repeatedItems", "repeatedPlayer", "repeatedWorld");
            Assert.Equal(depth, N(value, "depth"));
            var name = S(value, "name");
            var inputBarrel = value.GetProperty("inputBarrel");
            var expectedBarrel = value.GetProperty("expectedBarrel");
            var inputPlayer = value.GetProperty("inputPlayer");
            var expectedPlayer = value.GetProperty("expectedPlayer");
            var inputWorld = value.GetProperty("inputWorld");
            var expectedWorld = value.GetProperty("expectedWorld");
            var initial = ReadInput(value.GetProperty("inputItems"));
            var initialRandomState = N(inputBarrel, "slot") >= 0
                ? U(value, "initialRandomState")
                : throw new InvalidDataException("Native barrel slot is invalid.");
            Assert.Equal(initialRandomState, U(value.GetProperty("inputItems"), "randomState"));
            var type = N(value, "nativeType");
            Assert.Equal(N(inputBarrel, "type"), type);
            Assert.Contains(type, new[] {
                AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType,
                AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType,
            });
            Assert.Equal(U(value, "barrelSeed"), U(inputBarrel, "rndSeed"));
            Assert.Equal(N(value, "var1"), N(inputBarrel, "var1"));
            Assert.Equal(N(value, "var2"), N(inputBarrel, "var2"));
            Assert.Equal(N(value, "var3"), N(inputBarrel, "var3"));
            Assert.Equal(N(value, "var4"), N(inputBarrel, "var4"));
            Assert.False(B(inputBarrel, "trapFlag"));
            Assert.NotEqual(0, N(inputBarrel, "selectionRegion"));
            var position = inputBarrel.GetProperty("position");
            Properties(position, "x", "y");
            var x = N(position, "x");
            var y = N(position, "y");
            Assert.Equal(N(value, "slot") + 1, initial.ObjectRefs[y * AuthoritativeCathedralThemeItemWorldState.Width + x]);
            var spawnTiles = A(value, "spawnTilesAvailable").EnumerateArray().Select(row => row.GetBoolean()).ToArray();
            Assert.Equal(9, spawnTiles.Length);
            Assert.Equal(initialRandomState, U(inputWorld, "randomState"));
            var lootActorContext = value.GetProperty("lootActorContext");
            if (lootActorContext.ValueKind == JsonValueKind.Null) {
                Assert.Contains(name, new[] {
                    "explosive-chain-monster-player-order", "explosive-player-blocked", "explosive-player-fire-resisted",
                    "explosive-player-hit", "explosive-player-miss", "ordinary-empty", "ordinary-linked-skeleton",
                    "ordinary-linked-skeleton-signed-neighbor", "ordinary-random", "ordinary-useful",
                });
            } else {
                Assert.Contains(name, new[] {
                    "explosive-monster-fire-immune", "explosive-monster-fire-resisted",
                    "explosive-monster-lethal-credited", "explosive-monster-lethal-uncredited",
                    "explosive-monster-lethal-credited-level-delta", "explosive-three-barrel-nw-monster-player-order",
                });
                Properties(lootActorContext, "slot", "type", "level", "experience", "treasure", "whoHit", "rndItemSeed");
                var actorSlot = N(lootActorContext, "slot");
                var inputActor = A(inputWorld, "monsters").EnumerateArray().ElementAt(actorSlot);
                Assert.Equal(actorSlot, N(inputActor, "slot"));
                Assert.Equal(N(lootActorContext, "type"), N(inputActor, "type"));
                Assert.Equal(N(lootActorContext, "whoHit"), N(inputActor, "whoHit"));
                Assert.Equal(U(lootActorContext, "rndItemSeed"), U(inputActor, "rndItemSeed"));
            }

            if (name == "explosive-player-blocked") {
                Assert.True(B(inputPlayer, "blockFlag"));
                Assert.Equal(N(inputPlayer, "life"), N(expectedPlayer, "life"));
            } else if (name == "explosive-player-fire-resisted") {
                Assert.True(N(inputPlayer, "fireResistance") > 0);
                Assert.InRange(N(expectedPlayer, "life"), 1, N(inputPlayer, "life") - 1);
            } else if (name == "explosive-player-hit") {
                Assert.InRange(N(expectedPlayer, "life"), 1, N(inputPlayer, "life") - 1);
            } else if (name == "explosive-player-miss") {
                Assert.Equal(N(inputPlayer, "life"), N(expectedPlayer, "life"));
            } else if (name == "explosive-monster-fire-resisted") {
                var slot = N(lootActorContext, "slot");
                var inputActor = A(inputWorld, "monsters").EnumerateArray().ElementAt(slot);
                var expectedActor = A(expectedWorld, "monsters").EnumerateArray().ElementAt(slot);
                Assert.NotEqual(0, N(inputActor, "resistance"));
                Assert.InRange(N(expectedActor, "hitPoints"), 1, N(inputActor, "hitPoints") - 1);
            } else if (name == "explosive-monster-fire-immune") {
                var slot = N(lootActorContext, "slot");
                var inputActor = A(inputWorld, "monsters").EnumerateArray().ElementAt(slot);
                var expectedActor = A(expectedWorld, "monsters").EnumerateArray().ElementAt(slot);
                Assert.NotEqual(0, N(inputActor, "resistance"));
                Assert.Equal(N(inputActor, "hitPoints"), N(expectedActor, "hitPoints"));
            } else if (name is "explosive-monster-lethal-credited" or "explosive-monster-lethal-uncredited"
                or "explosive-monster-lethal-credited-level-delta") {
                var slot = N(lootActorContext, "slot");
                var expectedActor = A(expectedWorld, "monsters").EnumerateArray().ElementAt(slot);
                Assert.Equal(0, N(expectedActor, "hitPoints"));
                var credited = N(lootActorContext, "whoHit") != 0;
                Assert.Equal(credited, N(expectedPlayer, "experience") > N(inputPlayer, "experience"));
                if (name == "explosive-monster-lethal-credited-level-delta")
                    Assert.Equal(51, N(expectedPlayer, "experience") - N(inputPlayer, "experience"));
                Assert.True(N(value.GetProperty("expectedItems"), "activeCount") > N(value.GetProperty("inputItems"), "activeCount"));
            }

            Assert.Equal(expectedBarrel.GetRawText(), value.GetProperty("repeatedBarrel").GetRawText());
            Assert.Equal(value.GetProperty("expectedItems").GetRawText(), value.GetProperty("repeatedItems").GetRawText());
            Assert.Equal(expectedPlayer.GetRawText(), value.GetProperty("repeatedPlayer").GetRawText());
            Assert.Equal(expectedWorld.GetRawText(), value.GetProperty("repeatedWorld").GetRawText());
            var linkedSlot = N(value, "linkedMonsterSlot");
            AuthoritativeNativeMonsterInitialization? linked = null;
            if (linkedSlot >= 0) {
                Assert.Equal(N(inputBarrel, "var4"), linkedSlot);
                var actor = value.GetProperty("inputLinkedMonster");
                Assert.Equal(linkedSlot, N(actor, "slot"));
                linked = ReadLinkedMonster(actor, roomCatalog, linkedSlot, N(value, "depth"),
                    B(value, "inputLinkedMonsterPetrified"));
            } else {
                Assert.Equal(JsonValueKind.Null, value.GetProperty("inputLinkedMonster").ValueKind);
            }
            var facts = new AuthoritativeCathedralNativeBarrelFacts(
                N(value, "slot"), checked((uint)(500_000 + N(value, "slot"))), type,
                checked((uint)depth), new(x, y), U(value, "barrelSeed"),
                N(inputBarrel, "var1"), N(inputBarrel, "var2"), N(inputBarrel, "var3"), N(inputBarrel, "var4"),
                N(inputBarrel, "selectionRegion"), U(inputBarrel, "animFrame"), U(inputBarrel, "animFlag"),
                N(inputBarrel, "animDelay"), N(inputBarrel, "animCount"), U(inputBarrel, "animLength"),
                B(inputBarrel, "solidFlag"), B(inputBarrel, "missilePassable"), N(inputBarrel, "breakFlag"),
                B(inputBarrel, "preFlag"), B(inputBarrel, "trapFlag"), false, linked,
                N(value, "specialFrameCount"), N(value, "specialTicksPerFrame"));
            var request = new AuthoritativeCathedralBarrelOperationRequest(
                initial,
                catalog,
                new(depth),
                facts,
                spawnTiles,
                initialRandomState,
                new(LevelLoading: B(value, "levelLoading"),
                    MaximumLifeBase: N(corpus, "playerMaximumLife"),
                    MaximumManaBase: N(corpus, "playerMaximumMana")));
            var before = Snapshot(initial, initialRandomState);
            var actual = AuthoritativeCathedralBarrelOperationGenerator.Generate(request);
            Assert.Equal(AuthoritativeCathedralBarrelOpenedMutation.Native, actual.OpenedMutation);
            Assert.Equal(initialRandomState, actual.InitialWorldRandomState);
            Assert.False(actual.WorldState.ObjectCanInteract[facts.ObjectSlot]);
            Assert.False(actual.WorldState.ObjectSolid[facts.ObjectSlot]);
            Assert.Equal(initial.ObjectRefs, actual.WorldState.ObjectRefs);
            Assert.Equal(initial.PlayerRefs, actual.WorldState.PlayerRefs);
            Assert.Equal(initial.MonsterRefs, actual.WorldState.MonsterRefs);
            Assert.Equal(initial.SolidTiles, actual.WorldState.SolidTiles);
            Assert.Equal(initial.SetPieceTiles, actual.WorldState.SetPieceTiles);
            EqualState($"{name} caller unchanged", before, Snapshot(initial, initialRandomState));
            AssertOpened(expectedBarrel);
            AssertOpened(value.GetProperty("repeatedBarrel"));

            if (type == AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType) {
                var expected = ReadState(value.GetProperty("expectedItems"), inputs: false);
                EqualState(name, expected, Snapshot(actual.WorldState, actual.FinalWorldRandomState));
                Assert.Equal(U(value, "expectedRandomState"), actual.FinalWorldRandomState);
                if (facts.Var2 <= 1)
                    Assert.NotNull(actual.Loot);
                else
                    Assert.Null(actual.Loot);
                if (linked is null) {
                    Assert.Null(actual.Spawn);
                } else {
                    var expectedActor = value.GetProperty("expectedWorld").GetProperty("monsters")[linkedSlot];
                    var expectedTile = ReadPoint(expectedActor.GetProperty("tile"));
                    var expectedFuture = ReadPoint(expectedActor.GetProperty("future"));
                    if (expectedTile == new AuthoritativeCathedralPopulationPoint(0, 0)) {
                        Assert.Null(actual.Spawn);
                    } else {
                        Assert.NotNull(actual.Spawn);
                        Assert.Equal(expectedTile, new(actual.Spawn!.PositionX, actual.Spawn.PositionY));
                        Assert.Equal(expectedFuture, expectedTile);
                        Assert.Equal(N(expectedActor, "direction"), actual.Spawn.Facing);
                        Assert.Equal(linkedSlot, actual.Spawn.NativeMonsterSlot);
                        Assert.Equal(linked.Binding.EntityId, actual.Spawn.EntityId);
                        Assert.Equal(N(value, "specialFrameCount"), actual.Spawn.SpecialFrameCount);
                        Assert.Equal(N(value, "specialTicksPerFrame"), actual.Spawn.SpecialTicksPerFrame);
                    }
                }
            } else {
                Assert.True(actual.IsExplosive);
                Assert.Null(actual.Loot);
                Assert.Null(actual.Spawn);
                Assert.Equal(initialRandomState, actual.FinalWorldRandomState);
                Assert.Null(actual.WorldState.Items.Except(initial.Items).FirstOrDefault());
                Assert.Equal(initial.ActiveItemCount, actual.WorldState.ActiveItemCount);
                Assert.Equal(initial.ActiveItemIds, actual.WorldState.ActiveItemIds);
                Assert.Equal(initial.ItemRefs, actual.WorldState.ItemRefs);
                Assert.Equal(initial.UniqueItemFlags.Order(), actual.WorldState.UniqueItemFlags.Order());
                // The native expected world may contain hit, block, death-loot, or chain effects;
                // those are applied by the combat owner after this detached object/item result.
                Assert.NotEqual(JsonValueKind.Undefined, value.GetProperty("expectedWorld").ValueKind);
            }
        }
    }

    private static AuthoritativeNativeMonsterInitialization ReadLinkedMonster(
        JsonElement value,
        JsonElement roomCatalog,
        int slot,
        int levelId,
        bool petrified)
    {
        var state = new AuthoritativeCathedralPopulationMonsterState {
            Slot = N(value, "slot"), Type = N(value, "type"), LevelType = N(value, "levelType"),
            Mode = N(value, "mode"), Goal = N(value, "goal"), GoalVar1 = N(value, "goalVar1"),
            GoalVar2 = N(value, "goalVar2"), GoalVar3 = N(value, "goalVar3"), Var1 = N(value, "var1"),
            Var2 = N(value, "var2"), Var3 = N(value, "var3"), PathCount = N(value, "pathCount"),
            Tile = ReadPoint(value.GetProperty("tile")), Future = ReadPoint(value.GetProperty("future")),
            Old = ReadPoint(value.GetProperty("old")), Last = ReadPoint(value.GetProperty("last")),
            Temp = ReadPoint(value.GetProperty("temp")), Direction = N(value, "direction"),
            Enemy = N(value, "enemy"), EnemyPosition = ReadPoint(value.GetProperty("enemyPosition")),
            Invalid = B(value, "invalid"), Ai = N(value, "ai"), Intelligence = N(value, "intelligence"),
            ActiveForTicks = N(value, "activeForTicks"), UniqueType = N(value, "uniqueType"),
            UniqueTranslation = N(value, "uniqueTranslation"), CorpseId = N(value, "corpseId"),
            WhoHit = N(value, "whoHit"), MinDamage = N(value, "minDamage"), MaxDamage = N(value, "maxDamage"),
            MinDamageSpecial = N(value, "minDamageSpecial"), MaxDamageSpecial = N(value, "maxDamageSpecial"),
            ArmorClass = N(value, "armorClass"), Resistance = N(value, "resistance"), TalkMessage = N(value, "talkMessage"),
            Leader = N(value, "leader"), LeaderRelation = N(value, "leaderRelation"), PackSize = N(value, "packSize"),
            Flags = U(value, "flags"), RndItemSeed = U(value, "rndItemSeed"), AiSeed = U(value, "aiSeed"),
            LightId = N(value, "lightId"), HitPoints = N(value, "hitPoints"), MaxHitPoints = N(value, "maxHitPoints"),
            AnimTicksPerFrame = N(value, "animTicksPerFrame"), AnimTickCounter = N(value, "animTickCounter"),
            AnimNumberOfFrames = N(value, "animNumberOfFrames"), AnimCurrentFrame = N(value, "animCurrentFrame"),
            GolemToHit = N(value, "golemToHit"), ReducePlayerStrength = N(value, "reducePlayerStrength"),
            ReducePlayerMagic = N(value, "reducePlayerMagic"), ReducePlayerDexterity = N(value, "reducePlayerDexterity"),
            ReducePlayerVitality = N(value, "reducePlayerVitality"), ReducePlayerMaxHP = N(value, "reducePlayerMaxHP"),
            ReducePlayerMaxMana = N(value, "reducePlayerMaxMana"),
        };
        var bestiary = A(roomCatalog, "bestiary").EnumerateArray()
            .Single(row => N(row, "nativeType") == state.Type);
        var frames = A(bestiary, "frames").EnumerateArray().Select(Integer).ToArray();
        var rates = A(bestiary, "rates").EnumerateArray().Select(Integer).ToArray();
        var binding = new AuthoritativeNativeMonsterBinding(
            slot, state.Type, state.UniqueType, checked((uint)(600_000 + slot)),
            checked((uint)(700_000 + state.Type)), checked((uint)levelId),
            state.UniqueType == byte.MaxValue ? string.Empty : $"native-monster-{state.Type}");
        return new(new string('A', 64), state, 0, 0, 0, 0, binding, petrified) {
            NativeAttackFrameCount = frames[2], NativeAttackTicksPerFrame = rates[2],
            NativeHitRecoveryFrameCount = frames[3], NativeHitRecoveryTicksPerFrame = rates[3],
            NativeWalkFrameCount = frames[1], NativeWalkTicksPerFrame = rates[1],
        };
    }

    private static AuthoritativeCathedralPopulationPoint ReadPoint(JsonElement value)
    {
        Properties(value, "x", "y");
        return new(N(value, "x"), N(value, "y"));
    }

    private static void AssertOpened(JsonElement value)
    {
        Assert.Equal(1, N(value, "animFlag"));
        Assert.Equal(1, N(value, "animFrame"));
        Assert.Equal(1, N(value, "animDelay"));
        Assert.False(B(value, "solidFlag"));
        Assert.True(B(value, "missilePassable"));
        Assert.Equal(-1, N(value, "breakFlag"));
        Assert.Equal(0, N(value, "selectionRegion"));
        Assert.True(B(value, "preFlag"));
    }
}
