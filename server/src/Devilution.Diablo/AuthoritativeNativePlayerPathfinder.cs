namespace Devilution.Diablo;

/** Native engine/path.cpp A* ordering and storage bounds for captured player walk paths. */
public static class AuthoritativeNativePlayerPathfinder
{
    public const int MaximumPathLength = 100;
    private const int MaximumFrontierNodes = 1024;
    private const int BucketCapacity = 48;
    private static readonly Cell[] NeighborOffsets = [
        new(-1, -1), new(-1, 1), new(1, -1), new(1, 1),
        new(-1, 0), new(0, -1), new(1, 0), new(0, 1),
    ];

    /** Captures native direction codes once; a blocked endpoint is retained as native FindPath does. */
    public static int[] FindPath(
        int startX, int startY, int targetX, int targetY, int maximumLength,
        Func<int, int, bool> isPositionValid,
        Func<int, int, int, int, bool> canStep)
    {
        ArgumentNullException.ThrowIfNull(isPositionValid);
        ArgumentNullException.ThrowIfNull(canStep);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumLength, MaximumPathLength);
        ValidateCoordinate(startX);
        ValidateCoordinate(startY);
        ValidateCoordinate(targetX);
        ValidateCoordinate(targetY);
        var start = new Cell(startX, startY);
        var destination = new Cell(targetX, targetY);
        var initialCost = Heuristic(start, destination);
        if (initialCost > 101 * maximumLength)
            return [];

        var explored = new Dictionary<Cell, ExploredNode>();
        var buckets = new int[64];
        var frontier = new List<FrontierNode>(MaximumFrontierNodes);
        explored.Add(start, new ExploredNode(default, 0));
        buckets[Bucket(start)]++;
        frontier.Add(new FrontierNode(start, initialCost));

        int Compare(FrontierNode left, FrontierNode right)
        {
            var comparison = left.Cost.CompareTo(right.Cost);
            if (comparison != 0)
                return comparison;
            comparison = Heuristic(left.Position, destination).CompareTo(Heuristic(right.Position, destination));
            if (comparison != 0)
                return comparison;
            var leftDiagonal = IsDiagonal(explored[left.Position].Previous, left.Position);
            var rightDiagonal = IsDiagonal(explored[right.Position].Previous, right.Position);
            comparison = rightDiagonal.CompareTo(leftDiagonal);
            if (comparison != 0)
                return comparison;
            comparison = left.Position.X.CompareTo(right.Position.X);
            return comparison != 0 ? comparison : left.Position.Y.CompareTo(right.Position.Y);
        }

        while (frontier.Count != 0) {
            var current = frontier[0];
            if (current.Position == destination)
                return Reconstruct(explored, destination, maximumLength);
            PopHeap(frontier, Compare);
            var currentCost = explored[current.Position].Cost;
            if (currentCost >= 101 * maximumLength
                || currentCost + Heuristic(current.Position, destination) > current.Cost)
                continue;

            foreach (var offset in NeighborOffsets) {
                if (current.Position.X == 0 && offset.X < 0
                    || current.Position.Y == 0 && offset.Y < 0)
                    continue;
                // Native path coordinates use uint8_t, including positive-edge wrapping.
                var neighbor = new Cell(unchecked((byte)(current.Position.X + offset.X)),
                    unchecked((byte)(current.Position.Y + offset.Y)));
                if (isPositionValid(neighbor.X, neighbor.Y)) {
                    if (!canStep(current.Position.X, current.Position.Y, neighbor.X, neighbor.Y))
                        continue;
                } else if (neighbor != destination) {
                    continue;
                }

                var cost = currentCost + (IsDiagonal(current.Position, neighbor) ? 101 : 100);
                if (explored.TryGetValue(neighbor, out var previous)) {
                    if (previous.Cost <= cost)
                        continue;
                    previous.Previous = current.Position;
                    previous.Cost = cost;
                } else {
                    var bucket = Bucket(neighbor);
                    if (buckets[bucket] >= BucketCapacity)
                        continue;
                    explored.Add(neighbor, new ExploredNode(current.Position, cost));
                    buckets[bucket]++;
                }
                if (frontier.Count >= MaximumFrontierNodes)
                    continue;
                frontier.Add(new FrontierNode(neighbor, cost + Heuristic(neighbor, destination)));
                PushHeap(frontier, Compare);
            }
        }
        return [];
    }

    /** Direction codes match native GetPathDirection, rather than the player-facing direction enum. */
    public static (int X, int Y) GetDisplacement(int direction)
        => direction switch {
            1 => (0, -1), 2 => (-1, 0), 3 => (1, 0), 4 => (0, 1),
            5 => (-1, -1), 6 => (1, -1), 7 => (1, 1), 8 => (-1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

    private static int[] Reconstruct(Dictionary<Cell, ExploredNode> explored, Cell destination, int maximumLength)
    {
        var directions = new List<int>();
        var current = destination;
        ReadOnlySpan<int> codes = [5, 1, 6, 2, 0, 3, 8, 4, 7];
        while (explored[current].Cost != 0) {
            if (directions.Count == maximumLength)
                return [];
            var previous = explored[current].Previous;
            directions.Add(codes[3 * (current.Y - previous.Y) + 4 + current.X - previous.X]);
            current = previous;
        }
        directions.Reverse();
        return directions.ToArray();
    }

    private static int Heuristic(Cell from, Cell to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return Math.Min(dx, dy) * 101 + Math.Abs(dx - dy) * 100;
    }

    private static bool IsDiagonal(Cell from, Cell to) => from.X != to.X && from.Y != to.Y;
    private static int Bucket(Cell cell) => (cell.X & 7) << 3 | (cell.Y & 7);

    private static void ValidateCoordinate(int coordinate)
    {
        if ((uint)coordinate > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(coordinate));
    }

    private static void PushHeap(List<FrontierNode> heap, Comparison<FrontierNode> compare)
    {
        var hole = heap.Count - 1;
        var value = heap[hole];
        while (hole > 0) {
            var parent = (hole - 1) / 2;
            if (compare(heap[parent], value) <= 0)
                break;
            heap[hole] = heap[parent];
            hole = parent;
        }
        heap[hole] = value;
    }

    private static void PopHeap(List<FrontierNode> heap, Comparison<FrontierNode> compare)
    {
        var value = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0)
            return;
        var hole = 0;
        while (2 * hole + 1 < heap.Count) {
            var child = 2 * hole + 1;
            if (child + 1 < heap.Count && compare(heap[child + 1], heap[child]) <= 0)
                child++;
            heap[hole] = heap[child];
            hole = child;
        }
        // Match pop_heap's adjust-then-push sequence: the native comparator
        // reads the latest explored parent when resolving diagonal ties.
        while (hole > 0) {
            var parent = (hole - 1) / 2;
            if (compare(heap[parent], value) <= 0)
                break;
            heap[hole] = heap[parent];
            hole = parent;
        }
        heap[hole] = value;
    }

    private readonly record struct Cell(int X, int Y);
    private readonly record struct FrontierNode(Cell Position, int Cost);
    private sealed class ExploredNode(Cell previous, int cost)
    {
        public Cell Previous { get; set; } = previous;
        public int Cost { get; set; } = cost;
    }
}
