using Devilution.Engine;

namespace Devilution.Server.Simulation;

/** Monotonic wall-clock tick source for the standalone server host. */
public sealed class RealtimeAuthoritativeClock : IAuthoritativeClock, IResumableAuthoritativeClock
{
    private readonly object synchronization = new();
    private readonly TimeProvider time;
    private long startedAt;
    private ulong startingTick;
    private readonly uint tickRateHz;

    public RealtimeAuthoritativeClock(uint tickRateHz)
        : this(tickRateHz, TimeProvider.System)
    {
    }

    public RealtimeAuthoritativeClock(uint tickRateHz, TimeProvider? timeProvider)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        this.tickRateHz = tickRateHz;
        time = timeProvider ?? TimeProvider.System;
        startedAt = time.GetTimestamp();
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
            startingTick = tick;
            startedAt = time.GetTimestamp();
        }
    }

    private ulong CurrentTickCore()
        => checked(startingTick + (ulong)(time.GetElapsedTime(startedAt).TotalSeconds * tickRateHz));
}
