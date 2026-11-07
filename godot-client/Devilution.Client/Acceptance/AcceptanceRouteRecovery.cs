namespace Devilution.Client.Acceptance;

/** A native walk can land on another stair; the caller's next attempt returns through its origin. */
internal static class AcceptanceRouteRecovery
{
    internal static async Task<bool> TryAsync(Func<Task> travel,
        Func<AcceptanceCommandBoundary.Observation> observe, Func<InvalidOperationException, Task> recover)
    {
        try {
            await travel();
            return true;
        } catch (InvalidOperationException failure) when (observe().Player is { Life: > 0 }) {
            await recover(failure);
            return false;
        }
    }
}
