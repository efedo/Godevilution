using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    private sealed record LeveledBoundary(int Depth, BoundarySpec Spec);
    private enum NativeBoundaryRole { Birth, PhaseStart, PhaseEnd, Final }
    private sealed record NativeBoundary(DiabloCohortState Checkpoint, DiabloProjectileState Projectile, NativeBoundaryRole Role);
    private sealed record NativeBoundaryClass(int Type, NativeObjectMissileStage Stage, NativeBoundaryRole Role,
        int Family, uint SourceKind, uint TriggerKind, int StartPosition, int Position, int TargetPosition,
        (int X, int Y, int Original) Direction, int Damage);

    /**
     * Depths with the same owner family and damage class reach the same validation branches.
     * Retain both Lightning depth endpoints: their children supply the global damage extrema.
     * Source coordinates, exact velocity, target extrema, collision mode and native kind remain
     * distinct. Interior aim distances with the same velocity, repeated interior damage/level
     * values, and seeds which produce the same boundary class add no validation branch.
     */
    private static IEnumerable<LeveledBoundary> DistinctBoundarySpecs()
    {
        var all = Enumerable.Range(1, 8)
            .SelectMany(depth => BoundarySpecs(depth).Select(spec => new LeveledBoundary(depth, spec)));
        var distinct = all.Where(value => value.Spec.Type != 7 || value.Depth is 6 or 8)
            .DistinctBy(value => {
                var spec = value.Spec;
                var direction = (int)DeterministicGridDirections.Toward(spec.X, spec.Y, spec.TargetX, spec.TargetY);
                var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(1, 2, spec.Intrinsic ? 2U : 3U,
                    (uint)value.Depth, 0, spec.X, spec.Y, spec.TargetX, spec.TargetY, direction, spec.Type,
                    spec.Intrinsic, new DeterministicLegacyRandom(spec.Seed), value.Depth);
                // Collinear interior aim points produce exactly the same velocity and trajectory.
                // Keep distinct target boundary classes and the equal-point native nudge.
                return (spec.Type, spec.Intrinsic, Family: value.Depth <= 4 ? 1 : 2,
                    LightningDepth: spec.Type == 7 ? value.Depth : 0,
                    spec.X, spec.Y, Target: PositionClass(initial.TargetX, initial.TargetY, 0, 111),
                    initial.VelocityX, initial.VelocityY, initial.OriginalDirection, spec.Blocked,
                    Damage: DamageClass(spec.Type, initial.Damage));
            }).ToArray();
        // Literal level bounds and each intermediate floor retain a real publication lifecycle.
        return distinct.Concat(new[] { 2, 4, 7, 8 }.Select(depth =>
            new LeveledBoundary(depth, BoundarySpecs(depth).First(value => value.Blocked))));
    }

    private static IEnumerable<LeveledBoundary> DoorBoundarySpecs()
    {
        foreach (var trigger in new[] { 1, 2, 42, 43 })
            foreach (var type in new[] { 0, 1, 7 })
                yield return new(trigger <= 2 ? 4 : 8,
                    new(type, false, 48, 50, 56, 50, true, 2588, trigger));
    }

    // Native aliases share an admission branch; the live snapshots and admission regressions
    // still check each literal native ID. Source/target identity relationships are never merged.
    private static uint OwnerClass(uint nativeType) => nativeType switch {
        53 or 54 => 1, 68 or 69 or 70 => 2, 5 or 6 or 7 => 3,
        1 or 2 or 42 or 43 => nativeType, _ => 0,
    };

    /** Pack actual footprints, reserving eight live children per Lightning controller. */
    private static IEnumerable<BoundarySpec[]> PackedBoundaryBatches(IEnumerable<BoundarySpec> specs)
    {
        var lightning = new Queue<BoundarySpec>(specs.Where(value => value.Type == 7));
        var other = new Queue<BoundarySpec>(specs.Where(value => value.Type != 7));
        while (lightning.Count != 0 || other.Count != 0) {
            var batch = new List<BoundarySpec>();
            var objectSlots = 0;
            var liveMissiles = 0;
            while (batch.Count < 8 && lightning.TryDequeue(out var controller)) {
                batch.Add(controller);
                objectSlots += 2;
                liveMissiles += 9;
            }
            while (other.TryPeek(out var next) && liveMissiles < 125
                && objectSlots + (next.Intrinsic ? 1 : 2) <= 127) {
                batch.Add(other.Dequeue());
                objectSlots += next.Intrinsic ? 1 : 2;
                liveMissiles++;
            }
            yield return batch.ToArray();
        }
    }

    private static int PositionClass(int x, int y, int minimum, int maximum)
        => PositionAxisClass(x, minimum, maximum) | (PositionAxisClass(y, minimum, maximum) << 3);

    private static int PositionAxisClass(int value, int minimum, int maximum)
        => value == minimum ? 1 : value == maximum ? 2 : value > maximum ? 3 : value < minimum ? 4 : 0;

    private static (int X, int Y, int Original) DirectionClass(DiabloProjectileState projectile)
    {
        var x = projectile.TargetX - projectile.StartX;
        var y = projectile.TargetY - projectile.StartY;
        var a = Math.Abs(x); var b = Math.Abs(y);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? (0, 0, projectile.OriginalDirection)
            : (x / a, y / a, projectile.OriginalDirection);
    }

    private static int DamageClass(int type, int damage)
        => damage == 0 ? 0 : damage == (type == -1 ? 1 : type == 7 ? 12 : 3) ? 1
            : damage == (type == -1 ? 10 : 23) ? 2 : 3;

    private static void AssertBoundaryClassCoverage(IEnumerable<NativeBoundaryClass> classes)
    {
        var values = classes.ToArray();
        foreach (var type in new[] { -1, 0, 1, 7, 27 }) {
            Assert.Contains(values, value => value.Type == type && value.Role == NativeBoundaryRole.Birth);
            Assert.Contains(values, value => value.Type == type && value.Role == NativeBoundaryRole.Final);
        }
        foreach (var type in new[] { 1, 27 })
            Assert.Contains(values, value => value.Type == type && value.Stage is NativeObjectMissileStage.Explosion
                or NativeObjectMissileStage.ElementalImpact);
        Assert.Contains(values, value => value.Type == 7 && value.Stage == NativeObjectMissileStage.LightningSegment);
        foreach (var kind in new uint[] { 0, 1, 2 })
            Assert.Contains(values, value => value.SourceKind == kind);
        foreach (var family in new[] { 1, 2 })
            Assert.Contains(values, value => value.Family == family);
        foreach (var direction in Enumerable.Range(0, 8))
            Assert.Contains(values, value => value.Direction.Original == direction);
        foreach (var damage in new[] { 1, 2 }) {
            Assert.Contains(values, value => value.Type == 1 && value.Damage == damage);
            Assert.Contains(values, value => value.Type == 7 && value.Damage == damage);
            Assert.Contains(values, value => value.Type == -1 && value.Damage == damage);
        }
        foreach (var type in new[] { 0, 1, 7, 27 }) {
            foreach (var position in new[] { 0, 1, 2, 8, 9, 10, 16, 17, 18 })
                Assert.Contains(values, value => value.Type == type && value.StartPosition == position);
            foreach (var aim in new[] { (1, 0), (2, 1), (1, 1), (1, 2), (0, 1), (-1, 2), (-1, 1), (-2, 1),
                (-1, 0), (-2, -1), (-1, -1), (-1, -2), (0, -1), (1, -2), (1, -1), (2, -1) })
                Assert.Contains(values, value => value.Type == type && value.Direction.X == aim.Item1
                    && value.Direction.Y == aim.Item2);
        }
    }
}
