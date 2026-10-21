using System.Reflection;
using System.Text.Json;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Validates prepared native Arrow trace structure and compares natural flight with the managed movement primitive.
 * Impact postconditions below validate the captured native artifact only; generated-owner registration and managed
 * impact behavior are covered by separate Arrow owner/runtime tests. Synthetic native slots and prepared seams remain leaf evidence.
 */
public sealed class AuthoritativeNativeCatacombsArrowNativeOracleTests
{
    private const string EnvironmentVariable = "GODEVILUTION_CATACOMBS_ARROW_FLIGHT_ORACLE_DIRECTORY";
    private const string FileName = "native-catacombs-arrow-flight.json";
    private const string Format = "godevilution-catacombs-arrow-flight";
    private const string Policy = "prepared-original-diablo-catacombs-arrow-flight-impact";

    [Fact(Explicit = true)]
    public void PreparedNativeArrowCorpusValidatesTraceAndManagedNaturalFlight()
    {
        using var document = ReadCorpus();
        var root = document.RootElement;
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(48, N(root, "caseCount"));
        Assert.Equal(48, cases.Length);
        Assert.Equal(cases.Length, cases.Select(value => S(value, "name")).Distinct(StringComparer.Ordinal).Count());

        CompareCaseCoverage(cases);
        CompareNaturalFixedPointFlights(cases);
        ComparePreparedImpactOutcomes(cases);
    }

    private static void CompareCaseCoverage(JsonElement[] cases)
    {
        foreach (var required in new[] {
            "natural-flight-beyond-requested-endpoint",
            "natural-skipped-tile-diagonal",
            "natural-map-edge-out-of-bounds-delete",
            "stationary-full-256-tick-lifetime",
            "warrior-low-damage-hit-no-recovery",
            "sorcerer-low-damage-hit-no-recovery",
            "player-arrow-miss",
            "player-arrow-block",
            "ordered-two-arrow-block-then-hit",
            "player-arrow-invincible",
            "player-arrow-etherealized",
            "player-arrow-already-dead",
            "moving-player-negative-then-settled",
            "new-monster-interceptor-at-current-cell",
            "new-player-interceptor-at-current-cell",
            "same-faction-monster-pass",
            "opposing-player-minion-target",
            "source-berserk-allows-same-faction-hit",
            "target-berserk-allows-same-faction-hit",
            "petrified-negative-monster-occupancy-hit",
            "moving-negative-nonpetrified-monster-ignored",
            "monster-impact-remaining-life-64",
            "monster-impact-lethal-fractional-63",
            "tile-missile-blocker",
            "player-hit-before-same-cell-tile-blocker",
            "player-hit-before-same-cell-breakable-crux",
            "source-live-damage-changed-before-impact",
            "source-removed-before-impact",
            "source-dead-before-impact",
            "final-lifetime-tick-collision",
        })
            _ = Case(cases, required);

        for (var direction = 0; direction < 16; direction++) {
            var name = $"natural-direction-{direction:D2}";
            var row = Case(cases, name);
            var launch = Assert.Single(row.GetProperty("launches").EnumerateArray().ToArray());
            Assert.Equal(direction, N(launch, "direction16"));
            Assert.Equal(direction + 1, N(launch, "animationFrameRaw"));
            Assert.Equal(0, N(launch, "frameGroupDirection16"));
            Assert.False(launch.GetProperty("preparedStationaryVelocity").GetBoolean());
        }

        foreach (var name in new[] { "direction-16-ordered-burst", "direction-16-reversed-burst" }) {
            var launches = Case(cases, name).GetProperty("launches").EnumerateArray().ToArray();
            Assert.Equal(16, launches.Length);
            Assert.Equal(Enumerable.Range(1, 16), launches.Select(value => N(value, "ordinal")));
            Assert.Equal(16, launches.Select(value => N(value, "direction16")).Distinct().Count());
        }

        foreach (var row in cases) {
            Assert.InRange(N(row, "heroClassId"), 0, 2);
            Assert.Contains(S(row, "heroClass"), new[] { "warrior", "sorcerer" });
            Assert.Equal(N(row, "sourceSlot"), N(Assert.Single(
                row.GetProperty("initial").GetProperty("actors").EnumerateArray().ToArray(),
                actor => N(actor, "slot") == N(row, "sourceSlot")), "slot"));
            Assert.NotEmpty(row.GetProperty("launches").EnumerateArray().ToArray());
            Assert.NotEmpty(row.GetProperty("ticks").EnumerateArray().ToArray());
            var launchesByOrdinal = row.GetProperty("launches").EnumerateArray()
                .ToDictionary(value => N(value, "ordinal"));
            foreach (var missile in row.GetProperty("initial").GetProperty("missiles").EnumerateArray()) {
                var missileLaunch = launchesByOrdinal[N(missile, "ordinal")];
                Assert.Equal(N(missileLaunch, "direction16") + 1, N(missile, "animationFrameRaw"));
                Assert.Equal(0, N(missile, "frameGroupDirection16"));
            }

            var priorOrdinals = row.GetProperty("launches").EnumerateArray()
                .Select(value => N(value, "ordinal")).ToArray();
            foreach (var tick in row.GetProperty("ticks").EnumerateArray()) {
                Assert.True(tick.GetProperty("rngBefore").TryGetUInt32(out _));
                Assert.True(tick.GetProperty("rngAfter").TryGetUInt32(out _));
                var before = tick.GetProperty("missilesBefore").EnumerateArray().ToArray();
                var after = tick.GetProperty("missilesAfter").EnumerateArray().ToArray();
                var beforeIds = before.Select(value => N(value, "ordinal")).ToArray();
                var afterIds = after.Select(value => N(value, "ordinal")).ToArray();
                Assert.Equal(beforeIds.Length, beforeIds.Distinct().Count());
                Assert.Equal(afterIds.Length, afterIds.Distinct().Count());
                Assert.All(afterIds, ordinal => Assert.Contains(ordinal, priorOrdinals));
                foreach (var missile in before.Concat(after)) {
                    var missileLaunch = launchesByOrdinal[N(missile, "ordinal")];
                    Assert.Equal(N(missileLaunch, "direction16") + 1, N(missile, "animationFrameRaw"));
                    Assert.Equal(0, N(missile, "frameGroupDirection16"));
                }
                var deleted = tick.GetProperty("deletedMissileOrdinals").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                Assert.All(deleted, ordinal => Assert.Contains(ordinal, beforeIds));
                Assert.All(deleted, ordinal => Assert.DoesNotContain(ordinal, afterIds));
                Assert.NotEmpty(tick.GetProperty("collisionCellsBefore").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal).ToArray());
                Assert.NotEmpty(tick.GetProperty("collisionCellsAfter").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal).ToArray());
            }
        }

        var stationary = Case(cases, "stationary-full-256-tick-lifetime");
        var lifetimeTicks = stationary.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(256, lifetimeTicks.Length);
        Assert.Contains(1, lifetimeTicks[^1].GetProperty("deletedMissileOrdinals").EnumerateArray().Select(value => value.GetInt32()));

        var edge = Case(cases, "natural-map-edge-out-of-bounds-delete");
        Assert.Contains(edge.GetProperty("ticks").EnumerateArray(), tick =>
            tick.GetProperty("deletedMissileOrdinals").EnumerateArray().Any(value => value.GetInt32() == 1));
    }

    private static void CompareNaturalFixedPointFlights(JsonElement[] cases)
    {
        var move = typeof(DiabloCombatCommandProcessor).GetMethod(
            "MoveNativeProjectile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(move);

        var flightNames = Enumerable.Range(0, 16).Select(direction => $"natural-direction-{direction:D2}")
            .Concat(["natural-flight-beyond-requested-endpoint", "natural-skipped-tile-diagonal"]);
        foreach (var name in flightNames) {
            var row = Case(cases, name);
            var launch = Assert.Single(row.GetProperty("launches").EnumerateArray().ToArray());
            var start = launch.GetProperty("start");
            var targetX = N(launch, "requestedTargetX");
            var targetY = N(launch, "requestedTargetY");
            var direction16 = N(launch, "direction16");
            Assert.Equal(direction16, AuthoritativeNativeCatacombsArrowState.ResolveDirection16(
                N(start, "x"), N(start, "y"), targetX, targetY));
            var velocity = DeterministicGridVelocity.CalculateNative(
                N(start, "x"), N(start, "y"), targetX, targetY, 32);
            var launchVelocity = launch.GetProperty("velocity16_16");
            Assert.Equal((N(launchVelocity, "x"), N(launchVelocity, "y")), velocity);

            var projectile = CreateUnownedArrowForFlightTrace(row, launch, velocity);
            var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
            foreach (var tick in ticks) {
                var beforeList = tick.GetProperty("missilesBefore").EnumerateArray().ToArray();
                if (beforeList.Length == 0)
                    break;
                var before = Assert.Single(beforeList);
                Assert.Equal(N(launch, "ordinal"), N(before, "ordinal"));
                Assert.Equal((projectile.PositionX, projectile.PositionY), ReadPoint(before.GetProperty("tile")));
                Assert.Equal((projectile.TraveledX, projectile.TraveledY), ReadPoint(before.GetProperty("traveled16_16")));
                Assert.Equal(checked((int)projectile.RemainingTicks), N(before, "duration"));

                projectile.RemainingTicks--;
                _ = move.Invoke(null, [projectile]);
                var after = tick.GetProperty("missilesAfter").EnumerateArray().ToArray();
                if (after.Length == 0) {
                    Assert.Contains(N(launch, "ordinal"), tick.GetProperty("deletedMissileOrdinals").EnumerateArray().Select(value => value.GetInt32()));
                    break;
                }

                var nativeAfter = Assert.Single(after);
                Assert.Equal((projectile.PositionX, projectile.PositionY), ReadPoint(nativeAfter.GetProperty("tile")));
                Assert.Equal((projectile.TraveledX, projectile.TraveledY), ReadPoint(nativeAfter.GetProperty("traveled16_16")));
                Assert.Equal(checked((int)projectile.RemainingTicks), N(nativeAfter, "duration"));
            }
        }

        var edge = Case(cases, "natural-map-edge-out-of-bounds-delete");
        var edgeLaunch = Assert.Single(edge.GetProperty("launches").EnumerateArray().ToArray());
        var edgeStart = edgeLaunch.GetProperty("start");
        var edgeVelocity = ReadPoint(edgeLaunch.GetProperty("velocity16_16"));
        Assert.True(edgeVelocity.X > 0 && edgeVelocity.Y > 0);
        var edgeProjectile = CreateUnownedArrowForFlightTrace(edge, edgeLaunch, edgeVelocity);
        var edgeDeleted = false;
        foreach (var tick in edge.GetProperty("ticks").EnumerateArray()) {
            edgeProjectile.RemainingTicks--;
            _ = move.Invoke(null, [edgeProjectile]);
            if (tick.GetProperty("deletedMissileOrdinals").EnumerateArray().Any(value => value.GetInt32() == N(edgeLaunch, "ordinal"))) {
                Assert.False(InBounds(edgeProjectile.PositionX, edgeProjectile.PositionY));
                edgeDeleted = true;
                break;
            }
            var nativeAfter = Assert.Single(tick.GetProperty("missilesAfter").EnumerateArray().ToArray());
            Assert.Equal((edgeProjectile.PositionX, edgeProjectile.PositionY), ReadPoint(nativeAfter.GetProperty("tile")));
            Assert.Equal((edgeProjectile.TraveledX, edgeProjectile.TraveledY), ReadPoint(nativeAfter.GetProperty("traveled16_16")));
        }
        Assert.True(edgeDeleted);
        Assert.Equal((110, 50), ReadPoint(edgeStart));
    }

    private static AuthoritativeProjectile CreateUnownedArrowForFlightTrace(
        JsonElement row,
        JsonElement launch,
        (int X, int Y) velocity)
    {
        var start = ReadPoint(launch.GetProperty("start"));
        return new AuthoritativeProjectile(
            checked((uint)N(launch, "ordinal")),
            checked((uint)(N(row, "sourceSlot") + 1)),
            0x70000001,
            0,
            5,
            start.X,
            start.Y,
            N(launch, "requestedTargetX"),
            N(launch, "requestedTargetY"),
            0,
            AuthoritativeDamageType.Physical,
            0,
            256,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            NativeMissileId = "Arrow",
            Speed = 32,
            ImpactLifetime = 0,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = N(launch, "direction16") / 2,
        };
    }

    // These postconditions qualify the captured native trace itself; they do not claim managed impact parity.
    private static void ComparePreparedImpactOutcomes(JsonElement[] cases)
    {
        foreach (var name in new[] {
            "player-arrow-miss", "player-arrow-block", "player-arrow-invincible",
            "player-arrow-etherealized", "player-arrow-already-dead",
            "tile-missile-blocker", "player-hit-before-same-cell-tile-blocker",
            "player-hit-before-same-cell-breakable-crux", "warrior-low-damage-hit-no-recovery",
            "sorcerer-low-damage-hit-no-recovery", "ordered-two-arrow-block-then-hit",
            "new-player-interceptor-at-current-cell", "moving-player-negative-then-settled",
        }) {
            var row = Case(cases, name);
            var tick = row.GetProperty("ticks").EnumerateArray()
                .Skip(name == "new-player-interceptor-at-current-cell" ? 1 : 0).First();
            var playersBefore = tick.GetProperty("playersBefore").EnumerateArray().ToArray();
            var playersAfter = tick.GetProperty("playersAfter").EnumerateArray().ToArray();
            var before = Assert.Single(playersBefore);
            var after = Assert.Single(playersAfter);
            var beforeLife = N(before, "lifeQ6");
            var afterLife = N(after, "lifeQ6");
            var beforeClass = N(before, "heroClassId");
            Assert.Equal(N(row, "heroClassId"), beforeClass);

            switch (name) {
            case "player-arrow-miss":
            case "player-arrow-invincible":
            case "player-arrow-etherealized":
            case "player-arrow-already-dead":
            case "tile-missile-blocker":
                Assert.Equal(beforeLife, afterLife);
                break;
            case "player-arrow-block":
                Assert.True(before.GetProperty("blockFlag").GetBoolean());
                Assert.Equal(beforeLife, afterLife);
                Assert.NotEqual(N(before, "mode"), N(after, "mode"));
                break;
            case "warrior-low-damage-hit-no-recovery":
            case "sorcerer-low-damage-hit-no-recovery":
                Assert.True(afterLife < beforeLife);
                Assert.Equal(N(before, "mode"), N(after, "mode"));
                Assert.NotEqual(U(tick, "rngBefore"), U(tick, "rngAfter"));
                Assert.Equal(name.StartsWith("warrior", StringComparison.Ordinal) ? 0 : 2, beforeClass);
                break;
            case "ordered-two-arrow-block-then-hit":
                Assert.Equal(new[] { 1, 2 }, row.GetProperty("launches").EnumerateArray().Select(value => N(value, "ordinal")));
                Assert.True(afterLife < beforeLife);
                var blockCase = Case(cases, "player-arrow-block");
                var blockAfter = Assert.Single(blockCase.GetProperty("ticks").EnumerateArray().First()
                    .GetProperty("playersAfter").EnumerateArray().ToArray());
                Assert.Equal(N(blockAfter, "mode"), N(after, "mode"));
                Assert.Equal(2, tick.GetProperty("deletedMissileOrdinals").EnumerateArray().Count());
                break;
            case "new-player-interceptor-at-current-cell":
                var interceptor = tick.GetProperty("mutationInputs");
                Assert.Equal("move-player-to-current-arrow-cell-before-step", S(interceptor, "kind"));
                Assert.True(afterLife < beforeLife);
                break;
            case "moving-player-negative-then-settled":
                Assert.Equal(beforeLife, afterLife);
                var next = row.GetProperty("ticks").EnumerateArray().Skip(1).First();
                Assert.True(N(Assert.Single(next.GetProperty("playersAfter").EnumerateArray().ToArray()), "lifeQ6")
                    < N(Assert.Single(next.GetProperty("playersBefore").EnumerateArray().ToArray()), "lifeQ6"));
                break;
            case "player-hit-before-same-cell-tile-blocker":
            case "player-hit-before-same-cell-breakable-crux":
                Assert.True(afterLife < beforeLife);
                break;
            }
        }

        var nonlethal = Case(cases, "monster-impact-remaining-life-64");
        var nonlethalTick = Assert.Single(nonlethal.GetProperty("ticks").EnumerateArray().ToArray());
        var nonlethalBefore = nonlethalTick.GetProperty("actorsBefore").EnumerateArray().Last();
        var nonlethalTarget = nonlethalTick.GetProperty("actorsAfter").EnumerateArray().Last();
        Assert.Equal(128, N(nonlethalBefore, "hitPointsQ6"));
        Assert.Equal(64, N(nonlethalTarget, "hitPointsQ6"));
        Assert.False(nonlethalTarget.GetProperty("hasNoLife").GetBoolean());
        Assert.Equal(N(nonlethalBefore, "killCountForType"), N(nonlethalTarget, "killCountForType"));

        var lethal = Case(cases, "monster-impact-lethal-fractional-63");
        var lethalTick = Assert.Single(lethal.GetProperty("ticks").EnumerateArray().ToArray());
        var lethalBefore = lethalTick.GetProperty("actorsBefore").EnumerateArray().Last();
        var lethalAfter = lethalTick.GetProperty("actorsAfter").EnumerateArray().Last();
        Assert.Equal(127, N(lethalBefore, "hitPointsQ6"));
        Assert.Equal(0, N(lethalAfter, "hitPointsQ6"));
        Assert.True(lethalAfter.GetProperty("hasNoLife").GetBoolean());
        Assert.Equal(N(lethalBefore, "killCountForType") + 1, N(lethalAfter, "killCountForType"));
        Assert.NotEqual(N(lethalBefore, "mode"), N(lethalAfter, "mode"));
        Assert.Equal(1, N(lethalBefore, "whoHit"));
        Assert.Equal(N(lethalTick.GetProperty("itemPoolBefore"), "activeCount") + 1,
            N(lethalTick.GetProperty("itemPoolAfter"), "activeCount"));

        foreach (var name in new[] {
            "same-faction-monster-pass", "opposing-player-minion-target",
            "source-berserk-allows-same-faction-hit", "target-berserk-allows-same-faction-hit",
            "petrified-negative-monster-occupancy-hit", "moving-negative-nonpetrified-monster-ignored",
        }) {
            var row = Case(cases, name);
            var tick = Assert.Single(row.GetProperty("ticks").EnumerateArray().ToArray());
            var before = tick.GetProperty("actorsBefore").EnumerateArray().Last();
            var after = tick.GetProperty("actorsAfter").EnumerateArray().Last();
            var changed = N(after, "hitPointsQ6") < N(before, "hitPointsQ6");
            Assert.Equal(name is "opposing-player-minion-target"
                    or "source-berserk-allows-same-faction-hit"
                    or "target-berserk-allows-same-faction-hit"
                    or "petrified-negative-monster-occupancy-hit",
                changed);
            if (name == "petrified-negative-monster-occupancy-hit") {
                var cell = tick.GetProperty("collisionCellsBefore").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal)
                    .Single(value => ReadPoint(value.GetProperty("position")) == (50, 50));
                Assert.True(N(cell, "signedMonster") < 0);
            }
            if (name == "moving-negative-nonpetrified-monster-ignored") {
                var cell = tick.GetProperty("collisionCellsBefore").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal)
                    .Single(value => ReadPoint(value.GetProperty("position")) == (50, 50));
                Assert.True(N(cell, "signedMonster") < 0);
            }
        }

        foreach (var name in new[] { "source-live-damage-changed-before-impact", "source-removed-before-impact", "source-dead-before-impact" }) {
            var row = Case(cases, name);
            var tick = Assert.Single(row.GetProperty("ticks").EnumerateArray().ToArray());
            Assert.True(N(Assert.Single(tick.GetProperty("playersAfter").EnumerateArray().ToArray()), "lifeQ6")
                < N(Assert.Single(tick.GetProperty("playersBefore").EnumerateArray().ToArray()), "lifeQ6"));
            if (name == "source-live-damage-changed-before-impact") {
                var source = tick.GetProperty("actorsBefore").EnumerateArray()
                    .Single(value => N(value, "slot") == N(row, "sourceSlot"));
                Assert.Equal(9, N(source, "minimumDamage"));
                Assert.Equal(13, N(source, "maximumDamage"));
            } else if (name == "source-removed-before-impact") {
                Assert.Equal("remove-source-from-active-pool-retain-slot-state", S(tick.GetProperty("mutationInputs"), "kind"));
                var sourceSlot = N(row, "sourceSlot");
                Assert.DoesNotContain(tick.GetProperty("activeMonsterPoolAfter").GetProperty("activeIds").EnumerateArray(),
                    value => value.GetInt32() == sourceSlot);
            } else {
                var source = tick.GetProperty("actorsBefore").EnumerateArray()
                    .Single(value => N(value, "slot") == N(row, "sourceSlot"));
                Assert.Equal(0, N(source, "hitPointsQ6"));
                Assert.True(N(source, "mode") != 0);
            }
        }

        var crux = Case(cases, "player-hit-before-same-cell-breakable-crux");
        var cruxTick = Assert.Single(crux.GetProperty("ticks").EnumerateArray().ToArray());
        var cruxCell = cruxTick.GetProperty("collisionCellsAfter").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal)
            .Single(value => ReadPoint(value.GetProperty("position")) == (50, 50));
        Assert.Equal(-1, N(cruxCell.GetProperty("object"), "breakState"));
        Assert.True(cruxCell.GetProperty("object").GetProperty("missilePassable").GetBoolean());
    }

    private static JsonElement Case(JsonElement[] cases, string name)
        => Assert.Single(cases, value => S(value, "name") == name);

    private static JsonDocument ReadCorpus()
    {
        var directory = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException($"An explicit native Catacombs Arrow reference directory is required in {EnvironmentVariable}.");
        var path = Path.Combine(directory, FileName);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is <= 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("The native Catacombs Arrow reference is missing or outside its size bound.");
        var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        if (S(root, "format") != Format || N(root, "version") != 1 || S(root, "policy") != Policy
            || N(root, "caseCount") != 48)
            throw new InvalidDataException("The native Catacombs Arrow corpus has an unsupported format, policy, or case count.");
        return document;
    }

    private static (int X, int Y) ReadPoint(JsonElement value)
        => (N(value, "x"), N(value, "y"));

    private static bool InBounds(int x, int y) => x is >= 0 and < 112 && y is >= 0 and < 112;
    private static int N(JsonElement value, string property) => value.GetProperty(property).GetInt32();
    private static uint U(JsonElement value, string property) => value.GetProperty(property).GetUInt32();
    private static string S(JsonElement value, string property)
        => value.GetProperty(property).GetString() ?? throw new InvalidDataException("A native Arrow string is null.");
}
