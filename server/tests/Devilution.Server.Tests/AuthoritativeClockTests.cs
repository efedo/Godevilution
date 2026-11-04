using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeClockTests
{
    [Fact]
    public void RealtimeClockContinuesImmediatelyFromSavedTick()
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(20, time);
        time.AdvanceMilliseconds(50);
        Assert.Equal(1UL, clock.CurrentTick);

        clock.RestoreAtTick(10000);
        Assert.Equal(10000UL, clock.CurrentTick);
        time.AdvanceMilliseconds(50);
        Assert.Equal(10001UL, clock.CurrentTick);

        // Older/equal saves neither rewind nor discard the fractional tick.
        time.AdvanceMilliseconds(25);
        clock.RestoreAtTick(10000);
        clock.RestoreAtTick(10001);
        time.AdvanceMilliseconds(25);
        Assert.Equal(10002UL, clock.CurrentTick);
    }

    [Theory]
    [InlineData(20U, 1UL)]
    [InlineData(60U, 3UL)]
    [InlineData(200U, 10UL)]
    public void AStallSlowsTheClockInsteadOfJumping(uint rate, ulong maximumStep)
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(rate, time);

        time.AdvanceMilliseconds(30_000);
        Assert.Equal(maximumStep, clock.CurrentTick);

        // The stalled time is gone: later reads see only time since the last one.
        Assert.Equal(maximumStep, clock.CurrentTick);
        time.AdvanceMilliseconds(50);
        Assert.Equal(maximumStep * 2, clock.CurrentTick);
    }

    [Fact]
    public void SteadyReadsRunAtExactlyTheTargetRate()
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(200, time);

        for (var read = 0; read < 1000; read++) {
            time.AdvanceMilliseconds(7);
            _ = clock.CurrentTick;
        }

        Assert.Equal(1400UL, clock.CurrentTick);
    }

    [Fact]
    public void RealtimeClockRejectsZeroRateAndPreservesOriginalConstructor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RealtimeAuthoritativeClock(0));
        Assert.NotNull(typeof(RealtimeAuthoritativeClock).GetConstructor([typeof(uint)]));
    }

    [Fact]
    public void DelegateClockReadsTheAuthoritativeTickAtEachAccess()
    {
        ulong tick = 10;
        var clock = new DelegateAuthoritativeClock(() => tick);

        Assert.Equal(10UL, clock.CurrentTick);
        tick = 11;
        Assert.Equal(11UL, clock.CurrentTick);
    }
}

/** Deterministic wall time for clock and live-host restore tests. */
internal sealed class ManualClockTimeProvider : TimeProvider
{
    private long timestamp;

    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => Interlocked.Read(ref timestamp);
    public void AdvanceMilliseconds(long milliseconds) => Interlocked.Add(ref timestamp, milliseconds);
}
