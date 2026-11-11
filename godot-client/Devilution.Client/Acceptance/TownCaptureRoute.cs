using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A deterministic tour of the admitted Town's four quadrants, never across a warp trigger. */
internal sealed class TownCaptureRoute
{
    private static readonly (int X, int Y)[] Steps = [(1, 0), (0, -1), (-1, 0), (0, 1)];
    private readonly int width, height;
    private readonly bool[] blocked;
    private readonly int[] parents, queue, path, anchors;
    private int anchor, pathCount, pathIndex;

    internal TownCaptureRoute(Snapshot snapshot)
    {
        var player = snapshot.Players.Single();
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == 0);
        if (player.LevelId != 0) throw new InvalidOperationException("Town capture requires Town geometry.");
        width = geometry.Width; height = geometry.Height;
        blocked = new bool[checked(width * height)];
        parents = new int[blocked.Length]; queue = new int[blocked.Length]; path = new int[blocked.Length];
        foreach (var cell in geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells)) blocked[cell] = true;
        // A margin also protects against a held-key turn completing one outstanding step.
        foreach (var warp in geometry.WarpTriggers)
            for (var y = warp.TriggerY - 1; y <= warp.TriggerY + 1; y++)
                for (var x = warp.TriggerX - 1; x <= warp.TriggerX + 1; x++)
                    if (Inside(x, y)) blocked[y * width + x] = true;
        foreach (var resident in snapshot.NativeTownResidents) {
            // Leave room for the cows' multi-cell footprint and their ordinary idle movement.
            var radius = resident.TownerId == 9 ? 2 : 0;
            for (var y = resident.PositionY - radius; y <= resident.PositionY + radius; y++)
                for (var x = resident.PositionX - radius; x <= resident.PositionX + radius; x++)
                    if (Inside(x, y)) blocked[y * width + x] = true;
        }
        foreach (var row in snapshot.Objects.Where(row => row.LevelId == 0 && !row.Activated && !row.Destroyed && !row.ActivateOnOccupancy))
            blocked[row.PositionY * width + row.PositionX] = true;
        Search(player.PositionX, player.PositionY);
        var reachable = queue.AsSpan(0, reachableCount).ToArray();
        var minX = reachable.Min(cell => cell % width); var maxX = reachable.Max(cell => cell % width);
        var minY = reachable.Min(cell => cell / width); var maxY = reachable.Max(cell => cell / width);
        anchors = new int[4];
        (double X, double Y)[] quadrants = [(.2, .2), (.8, .2), (.8, .8), (.2, .8)];
        for (var index = 0; index < quadrants.Length; index++) {
            var x = minX + (maxX - minX) * quadrants[index].X;
            var y = minY + (maxY - minY) * quadrants[index].Y;
            anchors[index] = reachable.MinBy(cell => Math.Abs(cell % width - x) + Math.Abs(cell / width - y));
        }
        anchor = Enumerable.Range(0, 4).MinBy(index => Distance(anchors[index], player.PositionX, player.PositionY));
    }

    private int reachableCount;
    private bool Inside(int x, int y) => (uint)x < width && (uint)y < height;
    private int Distance(int cell, int x, int y) => Math.Abs(cell % width - x) + Math.Abs(cell / width - y);
    internal bool IsBlocked(int x, int y) => !Inside(x, y) || blocked[y * width + x];

    private void Search(int x, int y)
    {
        Array.Fill(parents, -2);
        var origin = y * width + x;
        parents[origin] = -1; queue[0] = origin;
        var head = 0; reachableCount = 1;
        while (head < reachableCount) {
            var cell = queue[head++];
            foreach (var step in Steps) {
                var nextX = cell % width + step.X; var nextY = cell / width + step.Y;
                if (IsBlocked(nextX, nextY)) continue;
                var next = nextY * width + nextX;
                if (parents[next] != -2) continue;
                parents[next] = cell; queue[reachableCount++] = next;
            }
        }
    }

    /** Reuses its search buffers; only a route corner or displacement requires another search. */
    internal (int X, int Y) Next(int x, int y, int maximumSteps)
    {
        if (Distance(anchors[anchor], x, y) == 0) { anchor = (anchor + 1) % anchors.Length; pathCount = 0; }
        var current = y * width + x;
        var found = -1;
        for (var index = pathIndex; index < Math.Min(pathCount, pathIndex + 16); index++)
            if (path[index] == current) { found = index; break; }
        if (found < 0) {
            Search(x, y);
            if (parents[anchors[anchor]] == -2) throw new InvalidOperationException("The Town capture route became unreachable.");
            pathCount = 0;
            for (var cell = anchors[anchor]; cell >= 0; cell = parents[cell]) path[pathCount++] = cell;
            Array.Reverse(path, 0, pathCount);
            found = 0;
        }
        pathIndex = found;
        var next = path[Math.Min(pathCount - 1, found + maximumSteps)];
        return (next % width, next / width);
    }

    internal (int X, int Y) TowardResident(int x, int y, int targetX, int targetY, int maximumSteps)
    {
        var goal = ResidentGoal(x, y, targetX, targetY);
        if (goal < 0) throw new InvalidOperationException($"No safe Town route reaches the resident at ({targetX},{targetY}) from ({x},{y}).");
        pathCount = 0;
        for (var cell = goal; cell >= 0; cell = parents[cell]) path[pathCount++] = cell;
        Array.Reverse(path, 0, pathCount);
        var next = path[Math.Min(pathCount - 1, maximumSteps)];
        pathCount = 0;
        return (next % width, next / width);
    }

    internal int ResidentDistance(int x, int y, int targetX, int targetY)
    {
        var goal = ResidentGoal(x, y, targetX, targetY);
        if (goal < 0) return int.MaxValue;
        var distance = 0;
        for (var cell = goal; parents[cell] >= 0; cell = parents[cell]) distance++;
        return distance;
    }

    private int ResidentGoal(int x, int y, int targetX, int targetY)
    {
        Search(x, y);
        for (var index = 0; index < reachableCount; index++) {
            var cell = queue[index];
            if (Math.Max(Math.Abs(cell % width - targetX), Math.Abs(cell / width - targetY)) <= 1) return cell;
        }
        return -1;
    }
}
