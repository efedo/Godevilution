using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Ordinary cardinal input reaches objects, or any native pickup cell including a diagonal. */
internal static class NativeObjectApproachPath
{
    private static readonly (int X, int Y)[] Offsets = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    /** Native players.cpp CheckNewPath, ACTION_PICKUPITEM/ACTION_PICKUPAITEM: both offsets <= 1. */
    internal static bool IsInRange(int x, int y, int targetX, int targetY, bool pickup)
        => pickup ? Math.Max(Math.Abs((long)x - targetX), Math.Abs((long)y - targetY)) <= 1
            : Math.Abs((long)x - targetX) + Math.Abs((long)y - targetY) == 1;

    internal static List<(int X, int Y)>? Find(
        Snapshot snapshot,
        LevelGeometrySnapshot geometry,
        ObjectSnapshot chest, bool pickup = false, bool allowClosedDoors = false, bool ignoreActors = false)
    {
        var player = snapshot.Players.Single();
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        foreach (var trigger in geometry.WarpTriggers)
            blocked.Add(trigger.TriggerY * geometry.Width + trigger.TriggerX);
        foreach (var door in snapshot.Objects.Where(value => value.LevelId == player.LevelId
                && value.NativeObjectId is 42 or 43 && !value.MissilePassable)) {
            var cell = door.PositionY * geometry.Width + door.PositionX;
            if (allowClosedDoors) blocked.Remove(cell);
            else blocked.Add(cell);
        }
        foreach (var monster in snapshot.Monsters.Where(value => !ignoreActors && value.LevelId == player.LevelId && value.Alive)) {
            blocked.Add(monster.PositionY * geometry.Width + monster.PositionX);
            if (monster.Moving)
                blocked.Add(monster.FuturePositionY * geometry.Width + monster.FuturePositionX);
        }
        var origin = player.PositionY * geometry.Width + player.PositionX;
        if ((uint)player.PositionX >= geometry.Width || (uint)player.PositionY >= geometry.Height)
            return null;
        var parents = new Dictionary<int, int> { [origin] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var cell)) {
            var x = cell % geometry.Width;
            var y = cell / geometry.Width;
            if (IsInRange(x, y, chest.PositionX, chest.PositionY, pickup)) {
                var path = new List<(int X, int Y)>();
                for (var cursor = cell; cursor != origin; cursor = parents[cursor])
                    path.Add((cursor % geometry.Width, cursor / geometry.Width));
                path.Reverse();
                return path;
            }
            foreach (var offset in Offsets) {
                var nextX = x + offset.X;
                var nextY = y + offset.Y;
                if ((uint)nextX >= geometry.Width || (uint)nextY >= geometry.Height)
                    continue;
                var next = nextY * geometry.Width + nextX;
                if (blocked.Contains(next) || parents.ContainsKey(next))
                    continue;
                parents.Add(next, cell);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    internal static ObjectSnapshot? FirstClosedDoorOnRoute(Snapshot snapshot, uint levelId, IReadOnlyList<(int X, int Y)> path)
        => path.Select(cell => snapshot.Objects.FirstOrDefault(value => value.LevelId == levelId
                && value.NativeObjectId is 42 or 43 && !value.MissilePassable
                && value.PositionX == cell.X && value.PositionY == cell.Y)).FirstOrDefault(value => value is not null);

    /** Native players.cpp Player::positionIsAvailable refuses occupied and negative future-cell monster refs. */
    internal static bool IsActorOccupied(Snapshot snapshot, uint levelId, int x, int y)
        => snapshot.Monsters.Any(value => value.Alive && value.LevelId == levelId
            && (value.PositionX == x && value.PositionY == y
                || value.Moving && value.FuturePositionX == x && value.FuturePositionY == y));
}
