using System.Diagnostics;
using Devilution.Engine;

namespace Devilution.Server.Simulation;

/** Monotonic wall-clock tick source for the standalone server host. */
public sealed class RealtimeAuthoritativeClock : IAuthoritativeClock
{
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private readonly uint tickRateHz;

    public RealtimeAuthoritativeClock(uint tickRateHz)
    {
        if (tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz));
        this.tickRateHz = tickRateHz;
    }

    public ulong CurrentTick
    {
        get
        {
            var elapsed = Stopwatch.GetTimestamp() - startedAt;
            return (ulong)(elapsed * tickRateHz / (double)Stopwatch.Frequency);
        }
    }
}
