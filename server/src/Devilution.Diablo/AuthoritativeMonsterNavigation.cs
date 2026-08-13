namespace Devilution.Diablo;

/** Deterministic bounded fallback navigation for server-owned monsters. */
internal static class AuthoritativeMonsterNavigation
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

    public static (int X, int Y)? FindStep(
        DiabloWorldState world,
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(players);

        var maximumDepth = Math.Clamp(
            checked(monster.AggroRange * 2 + 2),
            8,
            256);
        var start = (monster.PositionX, monster.PositionY);
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
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (distances.ContainsKey(next)
                    || !world.IsWalkable(monster.LevelId, next.X, next.Y)
                    || world.IsOccupied(
                        monster.LevelId,
                        next.X,
                        next.Y,
                        players,
                        monster.EntityId)) {
                    continue;
                }

                var nextDepth = depth + 1;
                distances.Add(next, nextDepth);
                parents.Add(next, current);
                if (IsAdjacentTo(next, victim.PositionX, victim.PositionY))
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
            Math.Abs(position.X - targetX),
            Math.Abs(position.Y - targetY)) <= 1
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
