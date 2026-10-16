namespace Devilution.Shared;

/** Native DoVision ray-table visibility shared by authoritative consumers. */
public static class NativeVision
{
    private static readonly (sbyte DeltaX, sbyte DeltaY)[][] VisionRays =
    [
        [(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (10, 0), (11, 0), (12, 0), (13, 0), (14, 0), (15, 0)],
        [(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 1), (9, 1), (10, 1), (11, 1), (12, 1), (13, 1), (14, 1), (15, 1)],
        [(1, 0), (2, 0), (3, 0), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (12, 2), (13, 2), (14, 2), (15, 2)],
        [(1, 0), (2, 0), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 2), (9, 2), (10, 2), (11, 2), (12, 2), (13, 3), (14, 3), (15, 3)],
        [(1, 0), (2, 1), (3, 1), (4, 1), (5, 1), (6, 2), (7, 2), (8, 2), (9, 3), (10, 3), (11, 3), (12, 3), (13, 4), (14, 4)],
        [(1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2), (7, 3), (8, 3), (9, 3), (10, 4), (11, 4), (12, 4), (13, 5), (14, 5)],
        [(1, 0), (2, 1), (3, 1), (4, 2), (5, 2), (6, 3), (7, 3), (8, 3), (9, 4), (10, 4), (11, 5), (12, 5), (13, 6), (14, 6)],
        [(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 3), (7, 4), (8, 4), (9, 5), (10, 5), (11, 6), (12, 6), (13, 7)],
        [(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 4), (7, 4), (8, 5), (9, 6), (10, 6), (11, 7), (12, 7), (12, 8), (13, 8)],
        [(1, 1), (2, 2), (3, 2), (4, 3), (5, 4), (6, 5), (7, 5), (8, 6), (9, 7), (10, 7), (10, 8), (11, 8), (12, 9)],
        [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 5), (7, 6), (8, 7), (9, 8), (10, 9), (11, 9), (11, 10)],
        [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 7), (8, 8), (9, 9), (10, 10), (11, 11)],
        [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (5, 6), (6, 7), (7, 8), (8, 9), (9, 10), (9, 11), (10, 11)],
        [(1, 1), (2, 2), (2, 3), (3, 4), (4, 5), (5, 6), (5, 7), (6, 8), (7, 9), (7, 10), (8, 10), (8, 11), (9, 12)],
        [(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 6), (4, 7), (5, 8), (6, 9), (6, 10), (7, 11), (7, 12), (8, 12), (8, 13)],
        [(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (3, 6), (4, 7), (4, 8), (5, 9), (5, 10), (6, 11), (6, 12), (7, 13)],
        [(0, 1), (1, 2), (1, 3), (2, 4), (2, 5), (3, 6), (3, 7), (3, 8), (4, 9), (4, 10), (5, 11), (5, 12), (6, 13), (6, 14)],
        [(0, 1), (1, 2), (1, 3), (1, 4), (2, 5), (2, 6), (3, 7), (3, 8), (3, 9), (4, 10), (4, 11), (4, 12), (5, 13), (5, 14)],
        [(0, 1), (1, 2), (1, 3), (1, 4), (1, 5), (2, 6), (2, 7), (2, 8), (3, 9), (3, 10), (3, 11), (3, 12), (4, 13), (4, 14)],
        [(0, 1), (0, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (2, 8), (2, 9), (2, 10), (2, 11), (2, 12), (3, 13), (3, 14), (3, 15)],
        [(0, 1), (0, 2), (0, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (1, 9), (1, 10), (1, 11), (2, 12), (2, 13), (2, 14), (2, 15)],
        [(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (1, 8), (1, 9), (1, 10), (1, 11), (1, 12), (1, 13), (1, 14), (1, 15)],
        [(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9), (0, 10), (0, 11), (0, 12), (0, 13), (0, 14), (0, 15)],
    ];

    private static readonly byte[] RayLengthAdjustments =
        [0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 4, 3, 2, 2, 2, 1, 1, 1, 0, 0, 0, 0];

    private static readonly (sbyte DeltaX, sbyte DeltaY)[] Quadrants =
        [(1, 1), (-1, 1), (1, -1), (-1, -1)];

    public static bool IsVisible(
        int viewerX,
        int viewerY,
        int targetX,
        int targetY,
        int radius,
        Func<int, int, bool> passesLight,
        Func<int, int, bool> inBounds)
    {
        ArgumentNullException.ThrowIfNull(passesLight);
        ArgumentNullException.ThrowIfNull(inBounds);
        if (!inBounds(viewerX, viewerY) || !inBounds(targetX, targetY))
            return false;
        if (viewerX == targetX && viewerY == targetY)
            return true;

        var effectiveRadius = Math.Clamp(radius, 0, 15);
        foreach (var quadrant in Quadrants) {
            for (var rayIndex = 0; rayIndex < VisionRays.Length; rayIndex++) {
                var rayLength = effectiveRadius - RayLengthAdjustments[rayIndex];
                var ray = VisionRays[rayIndex];
                for (var pointIndex = 0; pointIndex < rayLength && pointIndex < ray.Length; pointIndex++) {
                    var relative = ray[pointIndex];
                    var pointX = viewerX + relative.DeltaX * quadrant.DeltaX;
                    var pointY = viewerY + relative.DeltaY * quadrant.DeltaY;
                    if (!inBounds(pointX, pointY))
                        break;
                    if (relative.DeltaX > 0 && relative.DeltaY > 0
                        && !passesLight(pointX - quadrant.DeltaX, pointY)
                        && !passesLight(pointX, pointY - quadrant.DeltaY))
                        break;
                    if (pointX == targetX && pointY == targetY)
                        return true;
                    if (!passesLight(pointX, pointY))
                        break;
                }
            }
        }
        return false;
    }

    private readonly record struct TargetRay(int QuadrantIndex, int RayIndex, int PointIndex);
    private static readonly TargetRay[][] TargetRays = BuildTargetRays();

    private static TargetRay[][] BuildTargetRays()
    {
        var entries = Enumerable.Range(0, 31 * 31).Select(_ => new List<TargetRay>()).ToArray();
        for (var quadrantIndex = 0; quadrantIndex < Quadrants.Length; quadrantIndex++) {
            var quadrant = Quadrants[quadrantIndex];
            for (var rayIndex = 0; rayIndex < VisionRays.Length; rayIndex++) {
                var ray = VisionRays[rayIndex];
                for (var pointIndex = 0; pointIndex < ray.Length; pointIndex++) {
                    var point = ray[pointIndex];
                    var x = point.DeltaX * quadrant.DeltaX;
                    var y = point.DeltaY * quadrant.DeltaY;
                    entries[(y + 15) * 31 + x + 15].Add(new TargetRay(quadrantIndex, rayIndex, pointIndex));
                }
            }
        }
        return entries.Select(list => list.ToArray()).ToArray();
    }

    /**
     * Same native visibility for pure map predicates, examining only ray prefixes
     * that can reach the target. Retains no geometry or mutable blocker state.
     * The original IsVisible API preserves its delegate visitation behavior.
     */
    public static bool IsVisibleOnTargetRays(
        int viewerX, int viewerY, int targetX, int targetY, int radius,
        Func<int, int, bool> passesLight, Func<int, int, bool> inBounds)
    {
        ArgumentNullException.ThrowIfNull(passesLight);
        ArgumentNullException.ThrowIfNull(inBounds);
        if (!inBounds(viewerX, viewerY) || !inBounds(targetX, targetY))
            return false;
        if (viewerX == targetX && viewerY == targetY)
            return true;
        var deltaX = unchecked(targetX - viewerX);
        var deltaY = unchecked(targetY - viewerY);
        if (deltaX < -15 || deltaX > 15 || deltaY < -15 || deltaY > 15)
            return false;
        var effectiveRadius = Math.Clamp(radius, 0, 15);
        foreach (var candidate in TargetRays[(deltaY + 15) * 31 + deltaX + 15]) {
            if (candidate.PointIndex >= effectiveRadius - RayLengthAdjustments[candidate.RayIndex])
                continue;
            var quadrant = Quadrants[candidate.QuadrantIndex];
            var ray = VisionRays[candidate.RayIndex];
            for (var pointIndex = 0; pointIndex <= candidate.PointIndex; pointIndex++) {
                var relative = ray[pointIndex];
                var pointX = viewerX + relative.DeltaX * quadrant.DeltaX;
                var pointY = viewerY + relative.DeltaY * quadrant.DeltaY;
                if (!inBounds(pointX, pointY))
                    break;
                if (relative.DeltaX > 0 && relative.DeltaY > 0
                    && !passesLight(pointX - quadrant.DeltaX, pointY)
                    && !passesLight(pointX, pointY - quadrant.DeltaY))
                    break;
                if (pointIndex == candidate.PointIndex)
                    return true;
                if (!passesLight(pointX, pointY))
                    break;
            }
        }
        return false;
    }
}
