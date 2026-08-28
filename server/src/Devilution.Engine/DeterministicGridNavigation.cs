namespace Devilution.Engine;

/** Deterministic bounded breadth-first navigation over a caller-owned grid. */
public static class DeterministicGridNavigation
{
    private static readonly (int X, int Y)[] NeighborOffsets = [
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
        (0, -1),
        (1, -1),
    ];

    /**
     * Finds the first step to a walkable cell adjacent to the target.
     *
     * The caller owns grid semantics through the two predicates. Neighbor
     * order, breadth-first traversal, depth limits, and first-step selection
     * are deterministic and independent of any game module.
     */
    public static (int X, int Y)? FindStep(
        int startX,
        int startY,
        int targetX,
        int targetY,
        int maximumDepth,
        Func<int, int, bool> isWalkable,
        Func<int, int, bool> isOccupied)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);
        ArgumentNullException.ThrowIfNull(isWalkable);
        ArgumentNullException.ThrowIfNull(isOccupied);

        var start = (X: startX, Y: startY);
        var queue = new Queue<(int X, int Y)>();
        var distances = new Dictionary<(int X, int Y), int> {
            [start] = 0,
        };
        var parents = new Dictionary<(int X, int Y), (int X, int Y)>();
        queue.Enqueue(start);

        while (queue.Count != 0) {
            var current = queue.Dequeue();
            var depth = distances[current];
            if (depth >= maximumDepth)
                continue;

            foreach (var offset in NeighborOffsets) {
                if (offset.X > 0 && current.X == int.MaxValue
                    || offset.X < 0 && current.X == int.MinValue
                    || offset.Y > 0 && current.Y == int.MaxValue
                    || offset.Y < 0 && current.Y == int.MinValue) {
                    continue;
                }
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (distances.ContainsKey(next)
                    || !isWalkable(next.X, next.Y)
                    || isOccupied(next.X, next.Y)) {
                    continue;
                }

                var nextDepth = depth + 1;
                distances.Add(next, nextDepth);
                parents.Add(next, current);
                if (IsAdjacentTo(next, targetX, targetY))
                    return ReconstructFirstStep(start, next, parents);
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static bool IsAdjacentTo(
        (int X, int Y) position,
        int targetX,
        int targetY)
    {
        return Math.Max(
            Math.Abs((long)position.X - targetX),
            Math.Abs((long)position.Y - targetY)) <= 1
            && (position.X != targetX || position.Y != targetY);
    }

    private static (int X, int Y) ReconstructFirstStep(
        (int X, int Y) start,
        (int X, int Y) destination,
        IReadOnlyDictionary<(int X, int Y), (int X, int Y)> parents)
    {
        var current = destination;
        while (parents[current] != start)
            current = parents[current];
        return current;
    }
}
