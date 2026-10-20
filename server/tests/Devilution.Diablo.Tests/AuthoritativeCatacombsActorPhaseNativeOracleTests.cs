using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Prepared native actor ticks qualify the new clocks; generated-world and campaign proofs remain separate. */
public sealed class AuthoritativeCatacombsActorPhaseNativeOracleTests
{
    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeGargoylePackWakeMatchesProductionTickOrder(bool followerFirst)
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_PACK_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidDataException("An explicit native Gargoyle pack reference directory is required.");
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(
            Path.Combine(directory, "native-catacombs-gargoyle-pack-order.json"), 256 * 1024);
        Assert.Equal("godevilution-catacombs-gargoyle-pack-order", S(corpus, "format"));
        Assert.Equal("prepared-inactive-gargoyle-pack-special-completion-order", S(corpus, "policy"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal(2, N(corpus, "caseCount"));
        Assert.Equal(2, corpus.GetProperty("cases").GetArrayLength());
        var row = corpus.GetProperty("cases").EnumerateArray().Single(value =>
            value.GetProperty("followerFirst").GetBoolean() == followerFirst);
        var pair = AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateGargoylePair(
            leaderEntityId: followerFirst ? 502u : 501u, followerEntityId: followerFirst ? 501u : 502u);
        AuthoritativeCatacombsMonsterRuntimeAdapterTests.AdvanceLeaderToLastFrame(pair.World, pair.Leader);
        var random = new DeterministicLegacyRandom(2588);
        var processor = new DiabloCombatCommandProcessor(pair.World,
            new AuthoritativeSpellCatalog([new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100), random);
        var player = AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateProcessorPlayer();
        var initialLife = pair.Follower.HitPointsQ6;
        var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(3, ticks.Length);
        foreach (var tick in ticks) {
            processor.AdvanceTick((ulong)N(tick, "tick") + 1, [player]);
            var leader = pair.World.NativeCatacombsMonsterPhase(pair.Leader.EntityId)!;
            var follower = pair.World.NativeCatacombsMonsterPhase(pair.Follower.EntityId)!;
            var expected = tick.GetProperty("follower");
            Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Completed, leader.State);
            Assert.Equal(0, N(tick.GetProperty("leader"), "mode"));
            Assert.Equal(7, N(expected, "mode"));
            Assert.Equal(N(expected, "animCurrentFrame"), follower.FrameIndex);
            Assert.Equal(N(expected, "animTickCounter"), follower.TickCounter);
            Assert.Equal((U(expected, "flags") & 4) != 0,
                follower.State == AuthoritativeNativeCatacombsMonsterPhaseState.Frozen);
            Assert.Equal(initialLife, pair.Follower.HitPointsQ6);
            Assert.Equal(0, N(expected, "activeForTicks"));
            Assert.Equal(0, pair.Follower.ActiveForTicks);
            Assert.Equal(U(tick, "random"), random.State);
        }
    }

    private static readonly Lazy<JsonElement> Corpus = new(LoadCorpus);
    public static TheoryData<int, int> Cases {
        get {
            var result = new TheoryData<int, int>();
            foreach (var classId in new[] { 0, 2 })
                foreach (var lane in new[] { 0, 5, 6, 7, 2, 3, 4 }) result.Add(classId, lane);
            return result;
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void NativeFatAndGargoyleSpecialClocksMatch(int classId, int lane)
    {
        var row = Corpus.Value.GetProperty("cases").EnumerateArray().Single(value =>
            N(value, "classId") == classId && N(value, "lane") == lane);
        var metadata = AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs().Metadata;
        var type = lane == 0 ? 52 : lane >= 5 ? lane + 48 : 81;
        Assert.Equal(type, N(row, "nativeType"));
        Assert.Equal(1, N(row, "characterLevel"));
        Assert.Equal(classId == 0 ? 4480 : 1920, N(row, "initialPlayerLifeQ6"));
        Assert.False(row.GetProperty("canBlock").GetBoolean());
        var raw = metadata.Population.Monsters.Single(value => value.NativeMonsterId == type);
        var frames = row.GetProperty("animations");
        for (var index = 0; index < 6; index++) {
            Assert.Equal(raw.Frames[index], frames[index][0].GetInt32());
            Assert.Equal(raw.Rates[index], frames[index][1].GetInt32());
        }
        Assert.Equal(0, N(row, "specialEffectFrame"));
        var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(45, ticks.Length);
        if (lane == 0 || lane >= 5) CompareFat(row, raw, ticks);
        else CompareGargoyle(row, raw, ticks, lane);
    }

    private static void CompareFat(JsonElement row, AuthoritativeCathedralRawMonsterDefinition raw, JsonElement[] ticks)
    {
        var random = new AuthoritativeLegacyRandom(U(row, "inputSeed"));
        var roll = random.GenerateRnd(100);
        var intelligence = N(row, "intelligence");
        var nativeType = N(row, "nativeType");
        Assert.InRange(roll, 4 * intelligence + 15, 4 * intelligence + 19);
        var state = new AuthoritativeCathedralPopulationMonsterState {
            Slot = 0, Type = N(row, "nativeType"), Ai = raw.Ai, Goal = 1, UniqueType = 255,
            HitPoints = 6400, MaxHitPoints = 6400, Leader = 255,
            Tile = new(50, 50), Future = new(50, 50), Old = new(50, 50),
        };
        var initialization = new AuthoritativeNativeMonsterInitialization(new string('A', 64), state, 0, 3,
            N(row, "normalEffectFrame"), 0, new(0, nativeType, 255, 900, (uint)nativeType, 5, ""), false) {
            CatacombsDepth = 5,
            NativeAttackFrameCount = raw.Frames[2], NativeAttackTicksPerFrame = raw.Rates[2],
            NativeSpecialAttackFrameCount = raw.Frames[5], NativeSpecialAttackTicksPerFrame = raw.Rates[5],
        };
        var target = new AuthoritativeCombatTarget(900, 50, 50, 100, maxHitPoints: 100,
            levelId: 5, monsterId: (uint)nativeType, nativeAi: "Fat", intelligence: intelligence);
        target.RestoreNativeInitialization(initialization, 0, 0);
        // Exercise production AI admission independently of the pure clock below.
        var live = new AuthoritativeCombatTarget(900, 50, 50, 100, maxHitPoints: 100,
            levelId: 5, monsterId: (uint)nativeType, nativeAi: "Fat", intelligence: intelligence);
        live.RestoreNativeInitialization(initialization, 0, 0);
        live.RestoreActiveForTicks(255);
        var stream = new DeterministicLegacyRandom(U(row, "inputSeed"));
        var processor = new DiabloCombatCommandProcessor(new DiabloWorldState(combatTargets: [live]),
            new AuthoritativeSpellCatalog([new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100), stream);
        var life = N(row, "initialPlayerLifeQ6");
        var player = new AuthoritativePlayerState(0, 0, life, 0, 0, PlayerAttributesState.Zero,
            [], Enumerable.Repeat(-1, 40).ToArray(), [], 51, 50, life, 1, 5, 0,
            characterClassId: (uint)N(row, "classId")) { EntityId = 1 };
        processor.AdvanceTick(1, [player]);
        Assert.True(live.NativeMeleeCursor!.UsesSpecialAttack);
        Assert.Equal((N(ticks[0], "frame"), N(ticks[0], "counter")),
            (live.NativeMeleeCursor.FrameIndex, live.NativeMeleeCursor.TickCounter));
        Assert.Equal(U(ticks[0], "random"), stream.State);
        Assert.Equal(N(row, "initialPlayerLifeQ6"), player.Life);
        DiabloCombatCommandProcessor.BeginNativeMelee(target, usesSpecialAttack: true);
        var compared = 0;
        foreach (var tick in ticks) {
            Assert.Equal(N(row, "initialPlayerLifeQ6"), N(tick, "playerLifeQ6"));
            Assert.Equal(random.State, U(tick, "random"));
            if (target.NativeMeleeCursor is null) continue;
            var step = DiabloCombatCommandProcessor.AdvanceNativeMelee(target);
            Assert.True(step.UsesSpecialAttack);
            Assert.False(step.EffectFrame);
            if (step.Completed) Assert.Equal(0, N(tick, "mode"));
            else {
                Assert.Equal(7, N(tick, "mode"));
                Assert.Equal(N(tick, "frame"), target.NativeMeleeCursor!.FrameIndex);
                Assert.Equal(N(tick, "counter"), target.NativeMeleeCursor.TickCounter);
            }
            compared++;
        }
        Assert.Equal((raw.Frames[5] - 1) * Math.Max(1, raw.Rates[5]) + 1, compared);
        Assert.Null(target.NativeMeleeCursor);
    }

    private static void CompareGargoyle(JsonElement row, AuthoritativeCathedralRawMonsterDefinition raw, JsonElement[] ticks, int lane)
    {
        var phase = new AuthoritativeNativeCatacombsMonsterPhase(0, 900, 5, 81, new string('A', 64),
            raw.Frames[5], Math.Max(1, raw.Rates[5]), 0, 0, AuthoritativeNativeCatacombsMonsterPhaseState.Frozen);
        foreach (var tick in ticks) {
            var number = N(tick, "tick");
            if (number == 3 && lane is 3 or 4) phase = phase.Start();
            Assert.Equal(number == 3 && lane == 4, tick.GetProperty("consumedWake").GetBoolean());
            phase = phase.Advance();
            Assert.Equal(N(row, "initialPlayerLifeQ6"), N(tick, "playerLifeQ6"));
            Assert.Equal(U(row, "inputSeed"), U(tick, "random"));
            if (phase.State == AuthoritativeNativeCatacombsMonsterPhaseState.Completed) {
                Assert.Equal(0, N(tick, "mode"));
            } else {
                Assert.Equal(7, N(tick, "mode"));
                Assert.Equal(N(tick, "frame"), phase.FrameIndex);
                Assert.Equal(N(tick, "counter"), phase.TickCounter);
                Assert.Equal(phase.State == AuthoritativeNativeCatacombsMonsterPhaseState.Frozen,
                    (U(tick, "flags") & 4) != 0);
            }
        }
        Assert.Equal(lane == 2 ? AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
            : AuthoritativeNativeCatacombsMonsterPhaseState.Completed, phase.State);
    }

    private static JsonElement LoadCorpus()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_ACTOR_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(configured) || !Directory.Exists(configured))
            throw new InvalidDataException("An explicit prepared native actor reference directory is required.");
        var directory = Path.GetFullPath(configured);
        Assert.Equal(new[] { "native-catacombs-actor-phases.json" },
            AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(
            Path.Combine(directory, "native-catacombs-actor-phases.json"), 2 * 1024 * 1024);
        Assert.Equal("godevilution-catacombs-actor-phases", S(corpus, "format"));
        Assert.Equal(2, N(corpus, "version"));
        Assert.Equal("prepared-original-diablo-single-player-normal-catacombs-actor-arena", S(corpus, "policy"));
        Assert.Equal(14, N(corpus, "caseCount"));
        Assert.Equal(from classId in new[] { 0, 2 } from lane in new[] { 0, 5, 6, 7, 2, 3, 4 } select $"{classId}:{lane}",
            corpus.GetProperty("cases").EnumerateArray().Select(row => $"{N(row, "classId")}:{N(row, "lane")}"));
        return corpus;
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    private static int N(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static uint U(JsonElement value, string name) => value.GetProperty(name).GetUInt32();
}
