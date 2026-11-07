namespace Devilution.Client.Protocol.Tests;

/** Deterministic monotonic time and timers; socket-delivery deadlines still use real time. */
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object synchronization = new();
    private readonly object advanceSynchronization = new();
    private readonly List<ManualTimer> timers = [];
    private long timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (synchronization) return timestamp; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public TimeSpan? NextTimerDelay
    {
        get {
            lock (synchronization) {
                var next = timers.Where(timer => timer.DueAt.HasValue).Select(timer => timer.DueAt).Min();
                return next.HasValue ? TimeSpan.FromTicks(next.Value - timestamp) : null;
            }
        }
    }

    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        lock (advanceSynchronization) {
            long target;
            lock (synchronization) target = checked(timestamp + duration.Ticks);
            while (true) {
                ManualTimer? due;
                lock (synchronization) {
                    due = timers.Where(timer => timer.DueAt is { } at && at <= target)
                        .OrderBy(timer => timer.DueAt).FirstOrDefault();
                    if (due is null) {
                        timestamp = target;
                        return;
                    }
                    timestamp = due.DueAt!.Value;
                    due.DueAt = due.Period > TimeSpan.Zero
                        ? checked(timestamp + due.Period.Ticks) : null;
                }
                due.Callback(due.State);
            }
        }
    }

    public void AdvanceMilliseconds(double milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        lock (synchronization) {
            timers.Add(timer);
            timer.Change(dueTime, period);
        }
        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public long? DueAt { get; set; }
        public TimeSpan Period { get; private set; }
        private bool disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            if (period < TimeSpan.Zero && period != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(period));
            lock (owner.synchronization) {
                if (disposed) return false;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : checked(owner.timestamp + dueTime.Ticks);
                Period = period;
                return true;
            }
        }

        public void Dispose()
        {
            lock (owner.synchronization) {
                disposed = true;
                DueAt = null;
                owner.timers.Remove(this);
            }
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
