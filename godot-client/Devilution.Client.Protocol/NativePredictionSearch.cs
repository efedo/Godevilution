using System.Diagnostics;

namespace Devilution.Client.Protocol;

/** Native path ordering, with a bounded speculative budget; authority always supplies the real path. */
internal sealed class NativePredictionSearch
{
    internal const int MaximumCollisionQueries = 16384;
    private readonly PredictionCollisionIndex collision;
    private readonly Func<int, int, bool> available;
    private readonly Func<int, int, int, int, bool> canStep;
    private (ClientPosition Origin, ClientPosition Target, ulong Revision)? key;
    private ClientPosition? result;
    private long started;
    internal int SearchCount { get; private set; }
    internal int CollisionQueries { get; private set; }
    internal bool BudgetExceeded { get; private set; }

    internal NativePredictionSearch(PredictionCollisionIndex collision)
    {
        this.collision = collision;
        available = IsAvailable;
        canStep = CanStep;
    }

    internal ClientPosition? FirstStep(ClientPosition origin, ClientPosition target)
    {
        var nextKey = (origin, target, collision.Revision);
        if (key == nextKey) return result;
        key = nextKey; result = null; CollisionQueries = 0; BudgetExceeded = false;
        if (origin == target || (uint)origin.X > 255 || (uint)origin.Y > 255
            || (uint)target.X > 255 || (uint)target.Y > 255) return null;
        SearchCount++;
        started = Stopwatch.GetTimestamp();
        try {
            var path = NativeWalkPredictionPathfinder.FindPath(origin.X, origin.Y, target.X, target.Y,
                NativeWalkPredictionPathfinder.MaximumPathLength, available, canStep);
            if (path.Length != 0) {
                var step = NativeWalkPredictionPathfinder.GetDisplacement(path[0]);
                var next = new ClientPosition(origin.X + step.X, origin.Y + step.Y);
                if (collision.IsAvailable(next.X, next.Y)) result = next;
            }
        } catch (PredictionBudgetExceededException) {
            // An exhausted visual search waits for authority; remember it across identical admissions.
            BudgetExceeded = true;
        }
        return result;
    }

    private bool IsAvailable(int x, int y)
    {
        CollisionQueries++;
        if (CollisionQueries > MaximumCollisionQueries
            || (CollisionQueries & 127) == 0 && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 4)
            throw new PredictionBudgetExceededException();
        return collision.IsAvailable(x, y);
    }

    private bool CanStep(int x, int y, int nx, int ny)
        => x == nx || y == ny || collision.IsCornerOpen(nx, y) && collision.IsCornerOpen(x, ny);

    private sealed class PredictionBudgetExceededException : Exception;
}
