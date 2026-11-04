using Devilution.Engine;

namespace Devilution.Server.Simulation;

/**
 * Tick source for the standalone server host, paced at a target rate by wall time. Reads
 * happen under the authority lock, so time spent executing ticks counts against the next
 * read: one read grants at most `MaximumStepSeconds` of game time and drops any excess.
 * A host that falls behind therefore runs slower instead of jumping to catch up.
 */
public sealed class RealtimeAuthoritativeClock : IAuthoritativeClock, IResumableAuthoritativeClock
{
    /** One tick at the 20 Hz reference rate; timer jitter below this loses no game time. */
    public const double MaximumStepSeconds = 0.05;

    private readonly object synchronization = new();
    private readonly TimeProvider time;
    private readonly uint tickRateHz;
    private readonly ulong maximumStepTicks;
    private long anchor;
    private ulong tick;

    public RealtimeAuthoritativeClock(uint tickRateHz)
        : this(tickRateHz, TimeProvider.System)
    {
    }

    public RealtimeAuthoritativeClock(uint tickRateHz, TimeProvider? timeProvider)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        this.tickRateHz = tickRateHz;
        maximumStepTicks = Math.Max(1UL, (ulong)(tickRateHz * MaximumStepSeconds));
        time = timeProvider ?? TimeProvider.System;
        anchor = time.GetTimestamp();
    }

    public ulong CurrentTick
    {
        get
        {
            lock (synchronization)
                return CurrentTickCore();
        }
    }

    /** Resumes immediately at a saved boundary without replaying offline time. */
    public void RestoreAtTick(ulong tick)
    {
        lock (synchronization) {
            if (tick <= CurrentTickCore())
                return;
            this.tick = tick;
            anchor = time.GetTimestamp();
        }
    }

    private ulong CurrentTickCore()
    {
        var now = time.GetTimestamp();
        var due = (ulong)(time.GetElapsedTime(anchor, now).TotalSeconds * tickRateHz);
        if (due == 0)
            return tick;
        if (due > maximumStepTicks) {
            tick = checked(tick + maximumStepTicks);
            anchor = now;
        } else {
            tick = checked(tick + due);
            // Keep the fractional tick so a steady reader runs at exactly the target rate.
            anchor += (long)(due * (double)time.TimestampFrequency / tickRateHz);
        }
        return tick;
    }
}
