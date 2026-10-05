using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemWorldNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Actual SpawnLoot captures: only pre-call metadata, pool, tiles and monster seed are generator inputs. */
public sealed class AuthoritativeCathedralMonsterLootNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_CATHEDRAL_MONSTER_LOOT_ORACLE_DIRECTORY";
    private const string FileName = "native-cathedral-monster-loot.json";
    private sealed record Lane(string Name, uint Seed, int MonsterId = 0, bool Unique = false,
        int Count = 0, bool Shuffled = false, int Placement = 0, int History = 0, bool Signed = false, int Flags = 0, bool HistorySeed = false);
    private static readonly Lane[] Lanes = [
        new("ordinary-gold-selection", 0),
        new("ordinary-high-level-selection", 22, MonsterId: 2),
        new("ordinary-zero-seed", 0, Signed: true),
        new("ordinary-no-drop-flag", 0x80000000, MonsterId: 38),
        new("ordinary-no-item", 1),
        new("ordinary-full-capacity", 22, Count: 127, Shuffled: true),
        new("ordinary-last-slot", 22, Count: 126, Shuffled: true),
        new("ordinary-placement-fallback", 22, Placement: 1),
        new("ordinary-placement-exhausted", 22, Placement: 2),
        new("ordinary-signed-item-ref", 22, Placement: 1, Signed: true),
        new("ordinary-unique-history", 0, Unique: true, History: 127, HistorySeed: true),
        new("ordinary-unique-history-all", 0, Unique: true, History: 128, HistorySeed: true),
        new("ordinary-unique-zero-seed", 0, Unique: true),
        new("ordinary-unique-history-fresh", 0, Unique: true, HistorySeed: true),
        new("ordinary-extreme-seed", uint.MaxValue, MonsterId: 2),
        new("ordinary-player-minion-skipped", 2588, Flags: 32),
        new("ordinary-berserk-golem-drops", 22, Flags: 2080),
    ];

    [Theory(Explicit = true)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DeathTimeLootMatchesNative(int depth)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(DirectoryVariable);
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 128 * 1024 * 1024));
        foreach (var row in corpus.Cases.Where(row => row.Depth == depth)) {
            var before = Snapshot(row.Input, row.Seed);
            var request = new AuthoritativeCathedralMonsterLootRequest(row.Input, corpus.Catalog,
                new AuthoritativeCathedralThemeItemPolicy(depth), corpus.Monsters, row.MonsterId,
                row.Seed, 56, 56, row.Unique, IsMinion(row.Flags),
                row.Unique ? corpus.Uniques[18] : null,
                new(LevelLoading: true, MaximumLifeBase: 6400, MaximumManaBase: 3200));
            var actual = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(request);
            Assert.Equal(row.Seed, actual.InitialRandomState);
            Assert.Equal(row.Outcome, actual.Outcome);
            Assert.Equal(corpus.Monsters.Get(row.MonsterId).Level, actual.RawDataLevel);
            EqualState(row.Name, row.Expected, Snapshot(actual.WorldState, actual.FinalRandomState));
            EqualState("caller unchanged", before, Snapshot(row.Input, row.Seed));
            Assert.Equal(row.Input.PlayerRefs, actual.WorldState.PlayerRefs);
            Assert.Equal(row.Input.MonsterRefs, actual.WorldState.MonsterRefs);
            Assert.Equal(row.Input.ObjectRefs, actual.WorldState.ObjectRefs);
            Assert.Equal(row.Input.SolidTiles, actual.WorldState.SolidTiles);
            Assert.Equal(row.Input.SetPieceTiles, actual.WorldState.SetPieceTiles);
            Assert.Equal(row.Input.ObjectSolid, actual.WorldState.ObjectSolid);
            Assert.Equal(row.Input.ObjectCanInteract, actual.WorldState.ObjectCanInteract);
            if (row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem) {
                var slot = row.Input.ActiveItemIds[row.Input.ActiveItemCount];
                Assert.Equal(slot, actual.SelectedSlot);
                Assert.Equal(row.Expected.Items[slot], actual.Item);
                Assert.Equal(row.Expected.Items[slot].ItemIndex, actual.SelectedNativeItemIndex);
                Assert.Equal(row.Expected.ItemRefs.Contains(slot + 1), actual.PlacementSucceeded);
                Assert.Equal(row.Placement != 0, actual.Placement.UsedSuperItemSpaceFallback);
                Assert.NotSame(row.Input.Items[slot], actual.Items[slot]);
                // Native SpawnItem uses raw MonsterData level here, not dungeon*2
                // or unique Monster::level (Rotfeast has raw1/effective4).
                // Unique continuation may intentionally retarget the level;
                // its final CreateInfo is compared in the full item above.
                if (!row.Unique && actual.Item!.Magical != 2)
                    Assert.Equal(actual.RawDataLevel, actual.Item.CreateInfo & 0x3F);
            } else {
                Assert.Equal(-1, actual.SelectedSlot);
                Assert.Null(actual.Item);
                Assert.Equal(0u, actual.ItemSeed);
            }
        }
    }

    private static Corpus ReadCorpus(JsonElement value)
    {
        Properties(value, "format", "version", "policy", "boundary", "nativeSource", "playerMaximumLife", "playerMaximumMana",
            "gameMode", "difficulty", "levelType", "questsDisabled", "hellfire", "spawn", "multiplayer", "monsterRuntimeCount",
            "uniqueMonsterCount", "historySearchSeed", "catalog", "monsterRuntime", "uniqueMonsters", "cases", "caseCount", "depthCount", "lanesPerDepth", "nativeCoverage");
        if (S(value, "format") != "godevilution-cathedral-monster-loot" || N(value, "version") != 1 ||
            S(value, "policy") != "original-diablo-single-player-normal-cathedral-ordinary-monster-death-loot" ||
            S(value, "boundary") != "MonsterDeath-rndItemSeed-reset-then-SpawnLoot" ||
            S(value, "nativeSource") != "BUILD_TESTING TestMonsterSpawnLoot -> Source/game/monsters/monsters.cpp:SpawnLoot -> Source/game/items/items.cpp:SpawnItem" ||
            S(value, "gameMode") != "single-player" || S(value, "difficulty") != "normal" || S(value, "levelType") != "cathedral" ||
            !B(value, "questsDisabled") || B(value, "hellfire") || B(value, "spawn") || B(value, "multiplayer") ||
            N(value, "playerMaximumLife") != 6400 || N(value, "playerMaximumMana") != 3200)
            throw new InvalidDataException("Native death-time loot identity/policy differs from the bounded seam.");
        var catalog = ReadCatalog(value.GetProperty("catalog"));
        if (catalog.Items.Count != 168 || catalog.Prefixes.Count != 83 || catalog.Suffixes.Count != 95 ||
            catalog.Uniques.Count != 90 || catalog.Spells.Count != 37 || catalog.Rules.NoDropPercent != 59 || catalog.Rules.GoldPercent != 74)
            throw new InvalidDataException("Native death-time loot raw item metadata is incomplete or changed.");
        var monsters = ReadMonsters(A(value, "monsterRuntime"));
        var uniques = ReadUniques(A(value, "uniqueMonsters"));
        if (N(value, "monsterRuntimeCount") != 138 || N(value, "uniqueMonsterCount") != 100)
            throw new InvalidDataException("Native raw monster metadata counters are incomplete.");
        var historySeed = U(value, "historySearchSeed");
        if (historySeed > 4096) throw new InvalidDataException("The actual native history search must be bounded to seeds0..4096.");
        var cases = A(value, "cases").EnumerateArray().Select(row => ReadCase(row, monsters, uniques, historySeed)).ToArray();
        if (cases.Length != 4 * Lanes.Length || N(value, "caseCount") != cases.Length || N(value, "depthCount") != 4 ||
            N(value, "lanesPerDepth") != Lanes.Length)
            throw new InvalidDataException("Native death-time loot matrix is incomplete.");
        foreach (var depth in Enumerable.Range(1, 4))
            foreach (var lane in Lanes)
                if (cases.Count(row => row.Depth == depth && row.Name == lane.Name) != 1)
                    throw new InvalidDataException("Native death-time loot matrix contains missing or duplicate cases.");
        foreach (var depth in Enumerable.Range(1, 4)) {
            var fresh = cases.Single(row => row.Depth == depth && row.Name == "ordinary-unique-history-fresh");
            var exhausted = cases.Single(row => row.Depth == depth && row.Name == "ordinary-unique-history-all");
            var rawLevel = monsters.Get(fresh.MonsterId).Level;
            var selectedMinimum = catalog.Items.Single(item => item.NativeItemIndex == fresh.Expected.Items[0].ItemIndex).MinimumMonsterLevel;
            if (fresh.Expected.Items[0].Magical != 2 || exhausted.Expected.Items[0].Magical == 2 ||
                fresh.Seed != exhausted.Seed || !exhausted.Expected.Flags.SetEquals(exhausted.Input.UniqueItemFlags) ||
                selectedMinimum <= rawLevel || selectedMinimum > uniques[18].DungeonLevel * 2 ||
                (fresh.Expected.Items[0].CreateInfo & 0x3F) != rawLevel ||
                (exhausted.Expected.Items[0].CreateInfo & 0x3F) != rawLevel + catalog.Rules.BonusLevelsUnique)
                throw new InvalidDataException("Matched native fresh/exhausted history did not exercise unique regeneration.");
        }
        var coverage = value.GetProperty("nativeCoverage");
        var derived = DerivedCoverage(cases);
        Properties(coverage, derived.Keys.ToArray());
        foreach (var (name, count) in derived)
            if (count <= 0 || N(coverage, name) != count)
                throw new InvalidDataException($"Native death-time loot branch evidence is incomplete: {name}.");
        return new(catalog, monsters, uniques, cases);
    }

    private static Case ReadCase(JsonElement value, AuthoritativeCathedralMonsterRuntimeCatalog monsters,
        AuthoritativeCathedralUniqueDefinition[] uniques, uint historySeed)
    {
        Properties(value, "name", "depth", "monsterSeed", "preDeathRandomState", "monsterTypeIndex", "unique", "activeCount",
            "shuffledActiveItems", "placementMode", "uniqueHistoryCount", "signedItemRefs", "player", "monster", "input", "expected", "expectedRandomState");
        var name = S(value, "name"); var depth = N(value, "depth");
        var lane = Lanes.SingleOrDefault(row => row.Name == name)
            ?? throw new InvalidDataException("Unknown native death-time loot lane.");
        var seed = U(value, "monsterSeed");
        var monster = value.GetProperty("monster");
        Properties(monster, "type", "levelType", "dataLevel", "treasure", "flags", "uniqueType", "rndItemSeed", "positionX", "positionY",
            "futureX", "futureY", "oldX", "oldY", "mode", "hitPoints", "maxHitPoints", "whoHit");
        var monsterId = N(monster, "type");
        var typeIndex = monsterId switch { 0 => 0, 2 => 1, 38 => 2, _ => -1 };
        if (depth is < 1 or > 4 || seed != (lane.HistorySeed ? historySeed : lane.Seed) || U(value, "preDeathRandomState") != (seed ^ 0x9E3779B9u) ||
            B(value, "unique") != lane.Unique || N(value, "activeCount") != lane.Count || B(value, "shuffledActiveItems") != lane.Shuffled ||
            N(value, "placementMode") != lane.Placement || N(value, "uniqueHistoryCount") != lane.History || B(value, "signedItemRefs") != lane.Signed ||
            monsterId != lane.MonsterId || N(value, "monsterTypeIndex") != typeIndex || N(monster, "levelType") != typeIndex ||
            N(monster, "flags") != lane.Flags || N(monster, "uniqueType") != (lane.Unique ? 18 : 255) ||
            U(monster, "rndItemSeed") != seed || N(monster, "dataLevel") != monsters.Get(monsterId).Level ||
            N(monster, "treasure") != monsters.Get(monsterId).Treasure || N(monster, "mode") != 0 ||
            N(monster, "hitPoints") != 64 || N(monster, "maxHitPoints") != 64 || N(monster, "whoHit") != 0 ||
            new[] { "positionX", "positionY", "futureX", "futureY", "oldX", "oldY" }.Any(field => N(monster, field) != 56) ||
            lane.Unique && (uniques[18].QuestOnly || uniques[18].MonsterType != monsterId || uniques[18].DungeonLevel != 2))
            throw new InvalidDataException("Native death-time loot pre-call identity/monster inputs changed.");
        var player = value.GetProperty("player");
        Properties(player, "class", "maximumLifeBase", "maximumManaBase", "level", "pLvlLoad", "strength", "magic", "dexterity", "vitality", "positionX", "positionY", "lightRadius");
        if (N(player, "maximumLifeBase") != 6400 || N(player, "maximumManaBase") != 3200 || N(player, "pLvlLoad") != 1 ||
            N(player, "positionX") != 75 || N(player, "positionY") != 68)
            throw new InvalidDataException("Native death-time loot player power inputs changed.");
        foreach (var field in new[] { "class", "level", "strength", "magic", "dexterity", "vitality", "lightRadius" }) _ = N(player, field);
        var input = ReadInput(value.GetProperty("input"));
        var expected = ReadState(value.GetProperty("expected"), false);
        if (U(value.GetProperty("input"), "randomState") != seed || expected.RandomState != U(value, "expectedRandomState") ||
            input.ActiveItemCount != lane.Count || !input.ActiveItemIds.SequenceEqual(Enumerable.Range(0, 127).Select(id => lane.Shuffled && id != 126 ? 125 - id : id)) ||
            !input.UniqueItemFlags.SetEquals(Enumerable.Range(0, lane.History)) ||
            input.ItemRefs[54 * 112 + 54] != (lane.Signed ? -1 : 0))
            throw new InvalidDataException("Native death-time loot complete pool/history/seed inputs changed.");
        var outcome = IsMinion(lane.Flags) ? AuthoritativeCathedralMonsterLootOutcome.MinionSkipped
            : monsters.HasNoDrop(monsterId) && !lane.Unique ? AuthoritativeCathedralMonsterLootOutcome.NoDropFlag
            : !lane.Unique && Draw100(seed) >= 41 ? AuthoritativeCathedralMonsterLootOutcome.NoDropRoll
            : lane.Count == 127 ? AuthoritativeCathedralMonsterLootOutcome.PoolFull : AuthoritativeCathedralMonsterLootOutcome.DroppedItem;
        if (expected.Count != lane.Count + (outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem ? 1 : 0) ||
            !expected.ActiveIds.SequenceEqual(input.ActiveItemIds))
            throw new InvalidDataException("Native death-time loot allocation count/order differs.");
        if (outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem) {
            var slot = input.ActiveItemIds[input.ActiveItemCount]; var item = expected.Items[slot];
            if (lane.Placement == 2 && (item.PositionX != 0 || item.PositionY != 0 || expected.ItemRefs.Contains(slot + 1)) ||
                lane.Placement != 2 && expected.ItemRefs[item.PositionY * 112 + item.PositionX] != slot + 1 ||
                lane.Placement == 1 && Math.Abs(item.PositionX - 56) <= 1 && Math.Abs(item.PositionY - 56) <= 1 ||
                lane.Count == 126 && (slot != 126 || !expected.ItemRefs.Contains(127)) ||
                !lane.Unique && Draw100(Next(seed)) >= 74 && item.ItemIndex == 0 ||
                !lane.Unique && Draw100(Next(seed)) < 74 && item.ItemIndex != 0)
                throw new InvalidDataException("Native death-time loot declared branch did not execute.");
        } else {
            var draws = outcome == AuthoritativeCathedralMonsterLootOutcome.NoDropRoll ? 1
                : outcome == AuthoritativeCathedralMonsterLootOutcome.PoolFull ? (Draw100(Next(seed)) < 74 ? 2 : 3) : 0;
            var finalState = seed;
            for (var draw = 0; draw < draws; draw++) finalState = Next(finalState);
            EqualState("no allocation", Snapshot(input, finalState), expected);
        }
        return new(name, depth, seed, monsterId, lane.Unique, lane.Flags, lane.Placement, input, expected, outcome);
    }

    private static Dictionary<string, int> DerivedCoverage(Case[] cases) => new() {
        ["droppedCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem),
        ["noDropFlagCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.NoDropFlag),
        ["noDropRollCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.NoDropRoll),
        ["poolFullCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.PoolFull),
        ["minionSkippedCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.MinionSkipped),
        ["weightedCases"] = cases.Count(row => !row.Unique && (row.Outcome is AuthoritativeCathedralMonsterLootOutcome.DroppedItem or AuthoritativeCathedralMonsterLootOutcome.PoolFull) && Draw100(Next(row.Seed)) >= 74),
        ["goldCases"] = cases.Count(row => !row.Unique && row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem && Draw100(Next(row.Seed)) < 74),
        ["lastSlotCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem && row.Input.ActiveItemCount == 126 && row.Expected.ItemRefs.Contains(127)),
        ["fallbackCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem && row.Placement == 1 && row.Expected.ItemRefs.Contains(row.Input.ActiveItemIds[row.Input.ActiveItemCount] + 1)),
        ["exhaustedCases"] = cases.Count(row => row.Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem && row.Placement == 2 && row.Expected.ItemRefs.SequenceEqual(row.Input.ItemRefs)),
        ["historyFreshCases"] = cases.Count(row => row.Name == "ordinary-unique-history-fresh" && row.Expected.Items[0].Magical == 2),
        ["historyRerollCases"] = cases.Count(row => row.Name == "ordinary-unique-history-all" && row.Input.UniqueItemFlags.Count == 128 && row.Expected.Items[0].Magical != 2),
    };

    private static bool IsMinion(int flags) => (flags & 32) != 0 && (flags & 2048) == 0;
    private static uint Next(uint state) => unchecked(state * 0x015A4E35u + 1);
    private static int Draw100(uint state) {
        var signed = unchecked((int)Next(state));
        var absolute = signed < 0 ? unchecked(-signed) : signed;
        return (absolute >> 16) % 100;
    }

    internal static AuthoritativeCathedralMonsterRuntimeCatalog ReadMonsters(JsonElement value)
        => new(value.EnumerateArray().Select(row => {
            Properties(row, "nativeMonsterId", "stableSymbol", "name", "level", "toHit", "animFrameNum", "toHitSpecial",
                "animFrameNumSpecial", "monsterClass", "selectionRegion", "treasure", "experience", "resistanceHell");
            return new AuthoritativeCathedralMonsterRuntimeDefinition(N(row, "nativeMonsterId"), S(row, "stableSymbol"), S(row, "name"),
                N(row, "level"), N(row, "toHit"), N(row, "animFrameNum"), N(row, "toHitSpecial"), N(row, "animFrameNumSpecial"),
                N(row, "monsterClass"), N(row, "selectionRegion"), N(row, "treasure"), N(row, "experience"), N(row, "resistanceHell"));
        }).ToArray());

    internal static AuthoritativeCathedralUniqueDefinition[] ReadUniques(JsonElement value)
    {
        var rows = value.EnumerateArray().Select(row => {
            Properties(row, "nativeUniqueId", "monsterType", "dungeonLevel", "maximumHitPoints", "ai", "intelligence", "minDamage", "maxDamage",
                "resistance", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana",
                "monsterPack", "customToHit", "customArmorClass", "talkMessage", "questOnly");
            return new AuthoritativeCathedralUniqueDefinition(N(row, "nativeUniqueId"), N(row, "monsterType"), N(row, "dungeonLevel"),
                N(row, "maximumHitPoints"), N(row, "ai"), N(row, "intelligence"), N(row, "minDamage"), N(row, "maxDamage"), N(row, "resistance"),
                N(row, "reducePlayerStrength"), N(row, "reducePlayerMagic"), N(row, "reducePlayerDexterity"), N(row, "reducePlayerVitality"),
                N(row, "reducePlayerMaxHP"), N(row, "reducePlayerMaxMana"), (AuthoritativeCathedralUniquePack)N(row, "monsterPack"),
                N(row, "customToHit"), N(row, "customArmorClass"), N(row, "talkMessage"), B(row, "questOnly"));
        }).ToArray();
        if (rows.Length != 100 || rows.Where((row, id) => row.NativeUniqueId != id).Any() ||
            rows.Any(row => row.MonsterType is < 0 or > 137 || row.DungeonLevel is < 0 or > 255 || row.MonsterPack is < 0 or > (AuthoritativeCathedralUniquePack)2 ||
                row.MaximumHitPoints is < 0 or > 65535 || row.Resistance is < 0 or > 65535 || row.TalkMessage is < -32768 or > 32767 ||
                new[] { row.Ai, row.Intelligence, row.MinDamage, row.MaxDamage, row.ReducePlayerStrength, row.ReducePlayerMagic,
                    row.ReducePlayerDexterity, row.ReducePlayerVitality, row.ReducePlayerMaxHP, row.ReducePlayerMaxMana,
                    row.CustomToHit, row.CustomArmorClass }.Any(field => field is < 0 or > 255) ||
                row.QuestOnly != new[] { 0, 2, 3, 7, 8 }.Contains(row.NativeUniqueId)))
            throw new InvalidDataException("Native unique monster metadata is incomplete, reordered or unsafe.");
        return rows;
    }

    [Theory]
    [InlineData("short")]
    [InlineData("reordered")]
    [InlineData("level-width")]
    [InlineData("treasure-width")]
    [InlineData("float")]
    [InlineData("extra")]
    public void CompleteMonsterMetadataRejectsMalformedEvidence(string mutation)
    {
        var rows = new JsonArray(Enumerable.Range(0, 138).Select(id => (JsonNode)new JsonObject {
            ["nativeMonsterId"] = id, ["stableSymbol"] = $"NativeMonster_{id:D3}", ["name"] = "",
            ["level"] = 1, ["toHit"] = 0, ["animFrameNum"] = 0, ["toHitSpecial"] = 0,
            ["animFrameNumSpecial"] = 0, ["monsterClass"] = 0, ["selectionRegion"] = 0,
            ["treasure"] = 0, ["experience"] = 0, ["resistanceHell"] = 0,
        }).ToArray());
        switch (mutation) {
            case "short": rows.RemoveAt(137); break;
            case "reordered": rows[0]!["nativeMonsterId"] = 1; break;
            case "level-width": rows[0]!["level"] = 128; break;
            case "treasure-width": rows[0]!["treasure"] = 65536; break;
            case "float": rows[0]!["level"] = 0.5; break;
            case "extra": rows[0]!["extra"] = 0; break;
        }
        using var document = JsonDocument.Parse(rows.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadMonsters(document.RootElement));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("reordered")]
    [InlineData("quest-identity")]
    [InlineData("level-width")]
    [InlineData("damage-width")]
    [InlineData("pack")]
    [InlineData("boolean")]
    [InlineData("extra")]
    public void CompleteUniqueMetadataRejectsMalformedEvidence(string mutation)
    {
        var rows = new JsonArray(Enumerable.Range(0, 100).Select(id => (JsonNode)new JsonObject {
            ["nativeUniqueId"] = id, ["monsterType"] = 0, ["dungeonLevel"] = 0, ["maximumHitPoints"] = 1,
            ["ai"] = 0, ["intelligence"] = 0, ["minDamage"] = 0, ["maxDamage"] = 0, ["resistance"] = 0,
            ["reducePlayerStrength"] = 0, ["reducePlayerMagic"] = 0, ["reducePlayerDexterity"] = 0,
            ["reducePlayerVitality"] = 0, ["reducePlayerMaxHP"] = 0, ["reducePlayerMaxMana"] = 0,
            ["monsterPack"] = 0, ["customToHit"] = 0, ["customArmorClass"] = 0, ["talkMessage"] = 0,
            ["questOnly"] = new[] { 0, 2, 3, 7, 8 }.Contains(id),
        }).ToArray());
        switch (mutation) {
            case "short": rows.RemoveAt(99); break;
            case "reordered": rows[0]!["nativeUniqueId"] = 1; break;
            case "quest-identity": rows[0]!["questOnly"] = false; break;
            case "level-width": rows[0]!["dungeonLevel"] = 256; break;
            case "damage-width": rows[0]!["maxDamage"] = 256; break;
            case "pack": rows[0]!["monsterPack"] = 3; break;
            case "boolean": rows[0]!["questOnly"] = 1; break;
            case "extra": rows[0]!["extra"] = 0; break;
        }
        using var document = JsonDocument.Parse(rows.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadUniques(document.RootElement));
    }

    private sealed record Corpus(AuthoritativeCathedralThemeItemCatalog Catalog, AuthoritativeCathedralMonsterRuntimeCatalog Monsters,
        AuthoritativeCathedralUniqueDefinition[] Uniques, Case[] Cases);
    private sealed record Case(string Name, int Depth, uint Seed, int MonsterId, bool Unique, int Flags, int Placement,
        AuthoritativeCathedralThemeItemWorldState Input, State Expected, AuthoritativeCathedralMonsterLootOutcome Outcome);
}
