using System.Text.Json;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Qualified native ticks compare release clocks and missile-construction RNG/damage; projectile flight remains separate. */
public sealed class AuthoritativeNativeCatacombsRangedNativeOracleTests
{
    private const string Policy = "prepared-original-diablo-catacombs-ranged-controller-release-clock";
    private const string MetadataFingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string PopulationFingerprint = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact(Explicit = true)]
    public void NativeRangedControllerClocksMatchEveryQualifiedActionTick()
    {
        using var corpus = ReadCorpus();
        var cases = corpus.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(9, N(corpus.RootElement, "caseCount"));
        Assert.Equal(9, cases.Length);
        foreach (var name in new[] { "skeleton-bow", "goat-ranged", "magma", "acid", "deathspit" }) {
            var row = cases.Single(value => S(value, "name") == name);
            Assert.True(row.GetProperty("naturalReleaseObserved").GetBoolean(), name);
            Assert.True(row.GetProperty("completedAndRedispatched").GetBoolean(), name);
            CompareNaturalAction(row);
        }

        ComparePreparedBoundaries(cases);
    }

    private static void CompareNaturalAction(JsonElement row)
    {
        var identity = ResolveIdentity(S(row, "name"));
        Assert.Equal(identity.Ai, identity.MissileAi);
        Assert.Equal(identity.Special, row.GetProperty("special").GetBoolean());
        var rawEffectFrame = N(row, identity.Special ? "specialEffectFrame" : "normalEffectFrame");
        Assert.InRange(rawEffectFrame, 1, sbyte.MaxValue);
        var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
        var mode = identity.Special ? 12 : 10; // MonsterMode::SpecialRangedAttack / RangedAttack.
        var firstActionTick = Array.FindIndex(ticks, tick => N(tick.GetProperty("beforeActor"), "mode") == mode);
        Assert.True(firstActionTick >= 0, S(row, "name"));

        var first = ticks[firstActionTick];
        var beforeActor = first.GetProperty("beforeActor");
        Assert.Equal(N(row, "nativeType"), N(beforeActor, "type"));
        Assert.Equal(N(row, "sourceAnimationFrames"), N(row.GetProperty("initialActor"), "animNumberOfFrames"));
        var frameCount = N(beforeActor, "animNumberOfFrames");
        var ticksPerFrame = Math.Max(1, N(beforeActor, "animTicksPerFrame"));
        Assert.InRange(frameCount, 1, sbyte.MaxValue);
        Assert.InRange(ticksPerFrame, 1, sbyte.MaxValue);

        var missile = new AuthoritativeMonsterMissileDefinition(
            identity.MissileAi,
            identity.MissileId,
            identity.Behavior,
            identity.AttackMode,
            SpellId: identity.Behavior is AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.MonsterMagic ? 1U : 0U);
        var future = first.GetProperty("beforePlayerFuture");
        var action = new AuthoritativeNativeCatacombsRangedAction(
            N(beforeActor, "slot"),
            checked((uint)(500 + N(beforeActor, "slot"))),
            checked((uint)N(row, "nativeType")),
            checked((uint)N(row, "depth")),
            N(row, "nativeType"),
            identity.Ai,
            MetadataFingerprint,
            PopulationFingerprint,
            AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile),
            identity.MissileId,
            identity.Behavior,
            identity.AttackMode,
            missile.SpellId,
            900,
            N(future, "x"),
            N(future, "y"),
            identity.Special,
            2588,
            frameCount,
            ticksPerFrame,
            rawEffectFrame - 1,
            N(beforeActor, "animCurrentFrame"),
            N(beforeActor, "animTickCounter"));

        var released = 0;
        var completed = false;
        for (var index = firstActionTick; index < ticks.Length; index++) {
            var tick = ticks[index];
            var before = tick.GetProperty("beforeActor");
            var beforeRandom = U(tick, "beforeRandom");
            var afterRandom = U(tick, "random");
            Assert.Equal(mode, N(before, "mode"));
            Assert.Equal(action.FrameIndex, N(before, "animCurrentFrame"));
            Assert.Equal(action.TickCounter, N(before, "animTickCounter"));
            Assert.Equal(action.ActionFrameCount, N(before, "animNumberOfFrames"));
            Assert.Equal(action.TicksPerFrame, N(before, "animTicksPerFrame"));
            var eligible = tick.GetProperty("releaseFrameEligible").GetBoolean();
            var step = action.Advance();
            Assert.Equal(eligible, step.Release);
            Assert.Equal(step.Release, tick.GetProperty("missileCreated").GetBoolean());
            Assert.Equal(N(tick, "missileCountBefore") + (step.Release ? 1 : 0), N(tick, "missileCountAfter"));
            if (step.Release) released++;

            // The qualified source draws shared damage RNG at Acid/Magma release.
            // Ordinary arrows do not draw here; terminal AI redispatch may draw
            // unrelated RNG after the ranged mode completes, so that boundary is
            // intentionally outside this per-action assertion.
            if (!step.Completed && step.Release && identity.Special) {
                var expectedRandom = new DeterministicLegacyRandom(beforeRandom);
                if (identity.Behavior == AuthoritativeProjectileBehavior.Acid)
                    _ = expectedRandom.AdvanceRndSeed(); // PlaySfxLoc(SpellAcid) precedes AddAcid.
                var expectedDamage = expectedRandom.RandomIntBetween(
                    N(before, "minDamage"), N(before, "maxDamage"));
                Assert.Equal(expectedRandom.State, afterRandom);
                var projectileCountBefore = N(tick, "missileCountBefore");
                var projectiles = tick.GetProperty("missiles").EnumerateArray().ToArray();
                Assert.Equal(N(tick, "missileCountAfter"), projectiles.Length);
                var releasedProjectile = Assert.Single(projectiles.Skip(projectileCountBefore));
                Assert.Equal(expectedDamage, N(releasedProjectile, "damage"));
            } else if (!step.Completed) {
                Assert.Equal(beforeRandom, afterRandom);
            }

            var after = tick.GetProperty("actor");
            var afterEnemy = after.GetProperty("enemyPosition");
            var expectedEnemy = tick.GetProperty("beforePlayerFuture");
            Assert.Equal((N(expectedEnemy, "x"), N(expectedEnemy, "y")),
                (N(afterEnemy, "x"), N(afterEnemy, "y")));
            var afterMode = N(after, "mode");
            var afterFrame = N(after, "animCurrentFrame");
            var completedThisTick = beforeModeLastFrame(before, frameCount)
                && (afterMode != mode || afterFrame < action.FrameIndex);
            Assert.Equal(step.Completed, completedThisTick);
            if (step.Completed) {
                completed = true;
                Assert.Equal(index, ticks.Length - 1);
                break;
            }

            action = step.Action;
            Assert.Equal(mode, afterMode);
            Assert.Equal(action.FrameIndex, afterFrame);
            Assert.Equal(action.TickCounter, N(after, "animTickCounter"));
        }
        Assert.True(completed, S(row, "name"));
        Assert.True(released > 0, S(row, "name"));
    }

    private static void ComparePreparedBoundaries(JsonElement[] cases)
    {
        var normal = cases.Single(value => S(value, "name") == "ordinary-held-effect-frame-repeat");
        Assert.Equal("prepared-held-normal-effect-frame", S(normal, "boundary"));
        var normalIdentity = ResolveIdentity("goat-ranged");
        var normalTicks = normal.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(2, normalTicks.Length);
        foreach (var tick in normalTicks) {
            var action = CreateActionFromBoundary(normal, tick, normalIdentity);
            var step = action.Advance();
            Assert.True(tick.GetProperty("releaseFrameEligible").GetBoolean());
            Assert.True(step.Release);
            Assert.True(tick.GetProperty("missileCreated").GetBoolean());
        }

        var counter = cases.Single(value => S(value, "name") == "special-counter-gated-release");
        var specialIdentity = ResolveIdentity("magma");
        var counterTicks = counter.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(2, counterTicks.Length);
        Assert.False(counterTicks[0].GetProperty("releaseFrameEligible").GetBoolean());
        Assert.False(CreateActionFromBoundary(counter, counterTicks[0], specialIdentity).Advance().Release);
        Assert.True(counterTicks[1].GetProperty("releaseFrameEligible").GetBoolean());
        Assert.True(CreateActionFromBoundary(counter, counterTicks[1], specialIdentity).Advance().Release);

        foreach (var name in new[] { "special-petrified-before-release", "special-hard-hit-before-release" }) {
            var row = cases.Single(value => S(value, "name") == name);
            var tick = Assert.Single(row.GetProperty("ticks").EnumerateArray().ToArray());
            Assert.False(tick.GetProperty("releaseFrameEligible").GetBoolean());
            Assert.False(tick.GetProperty("missileCreated").GetBoolean());
            Assert.Equal(N(tick, "missileCountBefore"), N(tick, "missileCountAfter"));
            Assert.True(row.GetProperty("releaseSuppressed").GetBoolean());
        }
    }

    private static AuthoritativeNativeCatacombsRangedAction CreateActionFromBoundary(
        JsonElement row,
        JsonElement tick,
        RangedIdentity identity)
    {
        var actor = tick.GetProperty("beforeActor");
        var future = tick.GetProperty("beforePlayerFuture");
        var missile = new AuthoritativeMonsterMissileDefinition(
            identity.MissileAi, identity.MissileId, identity.Behavior, identity.AttackMode,
            SpellId: identity.Behavior is AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.MonsterMagic ? 1U : 0U);
        return new(
            N(actor, "slot"), checked((uint)(500 + N(actor, "slot"))),
            checked((uint)N(row, "nativeType")), checked((uint)N(row, "depth")),
            N(row, "nativeType"), identity.Ai, MetadataFingerprint, PopulationFingerprint,
            AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile),
            identity.MissileId, identity.Behavior, identity.AttackMode, missile.SpellId, 900,
            N(future, "x"), N(future, "y"), identity.Special, 2588,
            N(actor, "animNumberOfFrames"), Math.Max(1, N(actor, "animTicksPerFrame")),
            N(row, identity.Special ? "specialEffectFrame" : "normalEffectFrame") - 1,
            N(actor, "animCurrentFrame"), N(actor, "animTickCounter"));
    }

    private static bool beforeModeLastFrame(JsonElement actor, int frameCount)
        => N(actor, "animCurrentFrame") == frameCount - 1;

    private static RangedIdentity ResolveIdentity(string name)
        => name switch {
            "skeleton-bow" => new("SkeletonRanged", "SkeletonRanged", "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow, AuthoritativeMonsterRangedAttackMode.Ranged, false),
            "goat-ranged" or "ordinary-held-effect-frame-repeat" => new("GoatRanged", "GoatRanged", "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow, AuthoritativeMonsterRangedAttackMode.Ranged, false),
            "magma" or "special-counter-gated-release" or "special-petrified-before-release"
                or "special-hard-hit-before-release" => new("Magma", "Magma", "MagmaBall",
                AuthoritativeProjectileBehavior.MonsterMagic, AuthoritativeMonsterRangedAttackMode.RangedAvoidance, true),
            "acid" => new("Acid", "Acid", "Acid", AuthoritativeProjectileBehavior.Acid,
                AuthoritativeMonsterRangedAttackMode.RangedAvoidance, true),
            "deathspit" => new("AcidUnique", "AcidUnique", "Acid", AuthoritativeProjectileBehavior.Acid,
                AuthoritativeMonsterRangedAttackMode.Ranged, true),
            _ => throw new InvalidDataException("The native ranged oracle contains an unsupported controller case."),
        };

    private static JsonDocument ReadCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_RANGED_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException("An explicit native Catacombs ranged reference directory is required.");
        var path = Path.Combine(directory, "native-catacombs-ranged-release.json");
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("The native Catacombs ranged reference is missing or outside its size limit.");
        var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 48 });
        var root = document.RootElement;
        if (S(root, "format") != "godevilution-catacombs-ranged-release"
            || N(root, "version") != 1
            || S(root, "policy") != Policy
            || N(root, "caseCount") != 9)
            throw new InvalidDataException("The native Catacombs ranged reference has an unsupported schema or policy.");
        return document;
    }

    private static int N(JsonElement value, string property) => value.GetProperty(property).GetInt32();
    private static uint U(JsonElement value, string property) => value.GetProperty(property).GetUInt32();
    private static string S(JsonElement value, string property)
        => value.GetProperty(property).GetString() ?? throw new InvalidDataException("A ranged oracle string is null.");

    private sealed record RangedIdentity(
        string Ai,
        string MissileAi,
        string MissileId,
        AuthoritativeProjectileBehavior Behavior,
        AuthoritativeMonsterRangedAttackMode AttackMode,
        bool Special);
}
