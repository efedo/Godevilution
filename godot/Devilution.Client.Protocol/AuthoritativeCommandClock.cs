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

    public void Reset()
    {
        lock (synchronization)
            snapshotTick = null;
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
