namespace Devilution.Server.Host;

/** Grants authority turns in arrival order without holding a monitor across an await. */
internal sealed class CohortAuthorityTurnGate
{
    private readonly object synchronization = new();
    private readonly Queue<Waiter> waiters = new();
    private bool held;

    internal int QueuedWaiterCount
    {
        get {
            lock (synchronization) {
                var count = 0;
                foreach (var waiter in waiters) {
                    if (waiter.State == WaiterState.Waiting)
                        count++;
                }
                return count;
            }
        }
    }

    public ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Waiter waiter;
        lock (synchronization) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!held) {
                held = true;
                return ValueTask.FromResult<IDisposable>(new Lease(this));
            }

            waiter = new Waiter(this, cancellationToken);
            waiters.Enqueue(waiter);
        }

        if (cancellationToken.CanBeCanceled)
            RegisterCancellation(waiter);

        return WaitForTurnAsync(waiter);
    }

    private void RegisterCancellation(Waiter waiter)
    {
        var registration = waiter.CancellationToken.Register(
            static state => {
                var canceledWaiter = (Waiter)state!;
                canceledWaiter.Owner.Cancel(canceledWaiter);
            },
            waiter);

        var keepRegistration = false;
        lock (synchronization) {
            if (waiter.State == WaiterState.Waiting) {
                waiter.CancellationRegistration = registration;
                waiter.HasCancellationRegistration = true;
                keepRegistration = true;
            }
        }

        if (!keepRegistration)
            registration.Dispose();
    }

    private void Cancel(Waiter waiter)
    {
        lock (synchronization) {
            if (waiter.State != WaiterState.Waiting)
                return;
            waiter.State = WaiterState.Canceled;
        }

        waiter.Completion.TrySetCanceled(waiter.CancellationToken);
    }

    private static async ValueTask<IDisposable> WaitForTurnAsync(Waiter waiter)
    {
        try {
            return await waiter.Completion.Task.ConfigureAwait(false);
        } finally {
            waiter.Owner.DisposeCancellationRegistration(waiter);
        }
    }

    private void DisposeCancellationRegistration(Waiter waiter)
    {
        CancellationTokenRegistration registration;
        lock (synchronization) {
            registration = TakeCancellationRegistrationLocked(waiter);
        }
        registration.Dispose();
    }

    private void Release()
    {
        Waiter? next = null;
        CancellationTokenRegistration registration = default;
        lock (synchronization) {
            while (waiters.TryDequeue(out var candidate)) {
                if (candidate.State != WaiterState.Waiting)
                    continue;

                // Keep ownership logically held during handoff so a new arrival
                // cannot pass this waiter before its continuation runs.
                candidate.State = WaiterState.Granted;
                registration = TakeCancellationRegistrationLocked(candidate);
                next = candidate;
                break;
            }

            if (next is null)
                held = false;
        }

        registration.Dispose();
        next?.Completion.TrySetResult(new Lease(this));
    }

    private static CancellationTokenRegistration TakeCancellationRegistrationLocked(Waiter waiter)
    {
        if (!waiter.HasCancellationRegistration)
            return default;

        waiter.HasCancellationRegistration = false;
        var registration = waiter.CancellationRegistration;
        waiter.CancellationRegistration = default;
        return registration;
    }

    private sealed class Lease(CohortAuthorityTurnGate owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                owner.Release();
        }
    }

    private sealed class Waiter(CohortAuthorityTurnGate owner, CancellationToken cancellationToken)
    {
        public CohortAuthorityTurnGate Owner { get; } = owner;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public TaskCompletionSource<IDisposable> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public WaiterState State { get; set; } = WaiterState.Waiting;
        public CancellationTokenRegistration CancellationRegistration { get; set; }
        public bool HasCancellationRegistration { get; set; }
    }

    private enum WaiterState
    {
        Waiting,
        Granted,
        Canceled,
    }
}
