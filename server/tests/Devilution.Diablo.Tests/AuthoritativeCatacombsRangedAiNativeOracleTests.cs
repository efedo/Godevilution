using System.Text.Json;
using Xunit;
using static Devilution.Diablo.Tests.CatacombsAiNativeOracle;

namespace Devilution.Diablo.Tests;

/** Free-running native L5-L8 ranged AI and its missiles: one production-admitted actor against a passive Warrior. */
public sealed class AuthoritativeCatacombsRangedAiNativeOracleTests
{
    private const string Policy = "prepared-original-diablo-catacombs-ranged-ai-free-run";
    private static readonly string[] LaneNames =
        ["skeleton-archer", "goat-archer", "acid", "poison-spitter", "magma", "blood-stone", "familiar"];
    private static readonly Lazy<JsonDocument> Corpus = new(() => ReadCorpus("GODEVILUTION_CATACOMBS_RANGED_AI_ORACLE_DIRECTORY",
        "native-catacombs-ranged-ai.json", "godevilution-catacombs-ranged-ai", Policy, LaneNames));
    // Native MissileID values of the missiles these families create.
    private static readonly Dictionary<int, string> NativeMissileNames = new() {
        [0] = "Arrow", [8] = "Lightning", [9] = "MagmaBallExplosion", [21] = "MagmaBall",
        [57] = "Acid", [58] = "AcidSplat", [59] = "AcidPuddle",
    };

    public static TheoryData<string> Lanes => new(LaneNames);

    [Theory(Explicit = true)]
    [MemberData(nameof(Lanes))]
    public void NativeRangedAiMatchesEveryTick(string lane)
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
            var nativeMissiles = tick.GetProperty("missiles").EnumerateArray()
                .Select(missile => Missile(NativeMissileNames.GetValueOrDefault(N(missile, "type"), $"#{N(missile, "type")}"),
                    N(missile.GetProperty("tile"), "x"), N(missile.GetProperty("tile"), "y")));
            var expected = Observe(U(tick, "random"), N(tick, "playerLife"), N(tick, "playerLifeMaximum"),
                N(actor.GetProperty("tile"), "x"), N(actor.GetProperty("tile"), "y"), N(actor, "hitPoints"), nativeMissiles);
            var target = run.World.CombatTargets.GetValueOrDefault(EntityId);
            var managedMissiles = run.World.Projectiles.Values
                .Select(projectile => Missile(ManagedMissileName(projectile), projectile.PositionX, projectile.PositionY));
            var observed = Observe(run.Random.State, run.Player.Life, run.Player.LifeMaximum,
                target?.PositionX ?? -1, target?.PositionY ?? -1, target?.HitPointsQ6 ?? -1, managedMissiles);
            Assert.True(expected == observed,
                $"{lane} diverges at tick {index} (native mode {N(actor, "mode")} var1 {N(actor, "var1")} "
                + $"var2 {N(actor, "var2")} goal {N(actor, "goal")}/{N(actor, "goalVar1")}/{N(actor, "goalVar2")}; "
                + $"managed {target?.NativeAiVars} cursor {target?.NativeMeleeCursor} moving {target?.IsMoving}; "
                + $"draws native {Draws(previousRandom, U(tick, "random"))} managed {Draws(previousRandom, run.Random.State)}): "
                + $"native {expected}, managed {observed}");
            previousRandom = U(tick, "random");
        }
    }

    /** The native missile a managed projectile stands for. */
    private static string ManagedMissileName(AuthoritativeProjectile projectile) => projectile.Behavior switch {
        AuthoritativeProjectileBehavior.Acid => "Acid",
        AuthoritativeProjectileBehavior.AcidSplat => "AcidSplat",
        AuthoritativeProjectileBehavior.AcidPuddle => "AcidPuddle",
        _ => projectile.NativeMissileId ?? projectile.Behavior.ToString(),
    };

    private static string Missile(string name, int x, int y) => $"{name}@({x},{y})";

    private static string Observe(uint random, int life, int lifeMaximum, int x, int y, int hitPoints, IEnumerable<string> missiles)
        => $"rng={random} life={life}/{lifeMaximum} tile=({x},{y}) hp={hitPoints} missiles=[{string.Join(" ", missiles.Order(StringComparer.Ordinal))}]";
}
