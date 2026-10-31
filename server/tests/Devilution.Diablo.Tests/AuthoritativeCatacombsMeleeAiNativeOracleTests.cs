using System.Text.Json;
using Xunit;
using static Devilution.Diablo.Tests.CatacombsAiNativeOracle;

namespace Devilution.Diablo.Tests;

/** Free-running native L5-L8 melee AI: one production-admitted actor against a passive Warrior, compared every tick. */
public sealed class AuthoritativeCatacombsMeleeAiNativeOracleTests
{
    private const string Policy = "prepared-original-diablo-catacombs-melee-ai-free-run";
    private static readonly string[] LaneNames =
        ["zombie", "fallen", "skeleton", "scavenger", "sneak", "goat-melee", "fat", "bat", "gargoyle", "rhino"];
    private static readonly Lazy<JsonDocument> Corpus = new(() => ReadCorpus("GODEVILUTION_CATACOMBS_MELEE_AI_ORACLE_DIRECTORY",
        "native-catacombs-melee-ai.json", "godevilution-catacombs-melee-ai", Policy, LaneNames));

    public static TheoryData<string> Lanes => new(LaneNames);

    [Theory(Explicit = true)]
    [MemberData(nameof(Lanes))]
    public void NativeMeleeAiMatchesEveryTick(string lane)
    {
        var row = Corpus.Value.RootElement.GetProperty("cases").EnumerateArray().Single(value => S(value, "name") == lane);
        var run = Arena.Create(row);
        var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(N(Corpus.Value.RootElement, "tickCount"), ticks.Length);
        var previousRandom = U(row, "initialRandom");
        for (var index = 0; index < ticks.Length; index++) {
            run.Processor.AdvanceTick((ulong)index + 1, [run.Player]);
            var tick = ticks[index];
            var actor = tick.GetProperty("actor");
            var expected = Observe(U(tick, "random"), N(tick, "playerLife"), N(tick, "playerLifeMaximum"),
                N(actor.GetProperty("tile"), "x"), N(actor.GetProperty("tile"), "y"), N(actor, "hitPoints"),
                (N(actor, "flags") & MonsterFlagHidden) != 0);
            var target = run.World.CombatTargets.GetValueOrDefault(EntityId);
            var observed = Observe(run.Random.State, run.Player.Life, run.Player.LifeMaximum,
                target?.PositionX ?? -1, target?.PositionY ?? -1, target?.HitPointsQ6 ?? -1, target?.SneakHidden ?? false);
            Assert.True(expected == observed,
                $"{lane} diverges at tick {index} (native mode {N(actor, "mode")}, "
                + $"draws native {Draws(previousRandom, U(tick, "random"))} managed {Draws(previousRandom, run.Random.State)}): "
                + $"native {expected}, managed {observed}");
            previousRandom = U(tick, "random");
        }
    }

    private static string Observe(uint random, int life, int lifeMaximum, int x, int y, int hitPoints, bool hidden)
        => $"rng={random} life={life}/{lifeMaximum} tile=({x},{y}) hp={hitPoints} hidden={hidden}";
}
