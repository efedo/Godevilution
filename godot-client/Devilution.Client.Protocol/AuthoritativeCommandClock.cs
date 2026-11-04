namespace Devilution.Client.Protocol;

/** Estimates a command's admission time, never advances authoritative gameplay or presentation. */
public sealed class AuthoritativeCommandClock(TimeProvider? timeProvider = null)
{
    /**
     * The clock-gated projection runs at the negotiated rate, which a slowed server does not
     * reach, so it never extends further than this past the server's last evidence. A live host
     * accepts a command that arrives late instead, within CommandAdmissionPolicy's tolerance.
     */
    public const double MaximumProjectionSeconds = 1;

    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly object synchronization = new();
    private ulong? snapshotTick;
    private long receivedAt;
    private ulong newestServerTick;
    private long? lastServerMessageAt;
    private bool boundedProjection;

    public void Observe(ulong tick)
    {
        lock (synchronization) {
            snapshotTick = tick;
            receivedAt = time.GetTimestamp();
            newestServerTick = tick;
            boundedProjection = false;
        }
    }

    /**
     * Retains the projection for delayed snapshots; use the negotiated rate until Reset. The
     * server wrote this snapshot after its previous message, so the snapshot can be no older than
     * the gap since that message and the projection is pulled back to what the gap allows.
     */
    public void ObserveMonotonic(ulong tick, uint tickRateHz)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        lock (synchronization) {
            var observedAt = time.GetTimestamp();
            var previousMessageAt = lastServerMessageAt ?? observedAt;
            lastServerMessageAt = observedAt;
            boundedProjection = true;
            if (snapshotTick is not { } currentTick) {
                snapshotTick = tick;
                receivedAt = observedAt;
                newestServerTick = tick;
                return;
            }

            // An older tick than one already seen is out of order and says nothing new.
            if (tick < newestServerTick)
                return;
            newestServerTick = tick;

            var elapsedTicks = ProjectedTicks(receivedAt, observedAt, tickRateHz);
            // Compare the delta instead of adding elapsedTicks so a saturated
            // anchor cannot fault the receive loop on projection overflow.
            if (tick > currentTick && tick - currentTick > elapsedTicks) {
                snapshotTick = tick;
                receivedAt = observedAt;
                return;
            }

            var largestAgeTicks = ProjectedTicks(previousMessageAt, observedAt, tickRateHz);
            if (ulong.MaxValue - tick < largestAgeTicks || ulong.MaxValue - currentTick < elapsedTicks)
                return;
            if (currentTick + elapsedTicks > tick + largestAgeTicks) {
                snapshotTick = tick + largestAgeTicks;
                receivedAt = observedAt;
            }
        }
    }

    /** Notes a non-snapshot server message, which later snapshots are ordered behind. */
    public void ObserveServerMessage()
    {
        lock (synchronization)
            lastServerMessageAt = time.GetTimestamp();
    }

    /**
     * Anchors on a sample of the server's scheduling clock, correcting the projection
     * backward too because a slowed server runs behind the negotiated rate. An older
     * sample than the newest server tick is out of order and ignored.
     */
    public void ObserveSchedulingClock(ulong tick)
    {
        lock (synchronization) {
            lastServerMessageAt = time.GetTimestamp();
            boundedProjection = true;
            if (snapshotTick is not null && tick < newestServerTick)
                return;
            snapshotTick = tick;
            receivedAt = lastServerMessageAt.Value;
            newestServerTick = tick;
        }
    }

    public void Reset()
    {
        lock (synchronization) {
            snapshotTick = null;
            receivedAt = 0;
            newestServerTick = 0;
            lastServerMessageAt = null;
            boundedProjection = false;
        }
    }

    public ulong Estimate(ulong observedTick, uint tickRateHz)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        lock (synchronization) {
            if (snapshotTick is not { } tick)
                return observedTick;
            var elapsedTicks = ProjectedTicks(receivedAt, time.GetTimestamp(), tickRateHz);
            return Math.Max(observedTick, checked(tick + elapsedTicks));
        }
    }

    private ulong ProjectedTicks(long from, long to, uint tickRateHz)
    {
        var elapsedSeconds = Math.Max(0, time.GetElapsedTime(from, to).TotalSeconds);
        if (boundedProjection)
            elapsedSeconds = Math.Min(elapsedSeconds, MaximumProjectionSeconds);
        return checked((ulong)Math.Ceiling(elapsedSeconds * tickRateHz));
    }
}
