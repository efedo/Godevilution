namespace Devilution.Client.Protocol;

/** Estimates a command's admission time, never advances authoritative gameplay or presentation. */
public sealed class AuthoritativeCommandClock(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly object synchronization = new();
    private ulong? snapshotTick;
    private long receivedAt;

    public void Observe(ulong tick)
    {
        lock (synchronization) {
            snapshotTick = tick;
            receivedAt = time.GetTimestamp();
        }
    }

    /** Retains the projection for delayed snapshots; use the negotiated rate until Reset. */
    public void ObserveMonotonic(ulong tick, uint tickRateHz)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        lock (synchronization) {
            var observedAt = time.GetTimestamp();
            if (snapshotTick is not { } currentTick) {
                snapshotTick = tick;
                receivedAt = observedAt;
                return;
            }

            var elapsedSeconds = Math.Max(0, time.GetElapsedTime(receivedAt, observedAt).TotalSeconds);
            var elapsedTicks = checked((ulong)Math.Ceiling(elapsedSeconds * tickRateHz));
            // Compare the delta instead of adding elapsedTicks so a saturated
            // anchor cannot fault the receive loop on projection overflow.
            if (tick > currentTick && tick - currentTick > elapsedTicks) {
                snapshotTick = tick;
                receivedAt = observedAt;
            }
        }
    }

    public void Reset()
    {
        lock (synchronization) {
            snapshotTick = null;
            receivedAt = 0;
        }
    }

    public ulong Estimate(ulong observedTick, uint tickRateHz)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        lock (synchronization) {
            if (snapshotTick is not { } tick)
                return observedTick;
            var elapsedSeconds = Math.Max(0, time.GetElapsedTime(receivedAt).TotalSeconds);
            var elapsedTicks = checked((ulong)Math.Ceiling(elapsedSeconds * tickRateHz));
            return Math.Max(observedTick, checked(tick + elapsedTicks));
        }
    }
}
