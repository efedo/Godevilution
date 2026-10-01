using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeCommandClockTests
{
    [Fact]
    public void AddsOnlyElapsedServerTimeToAReceivedSnapshot()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        Assert.Equal(80UL, clock.Estimate(80, 20));
        clock.Observe(100);
        Assert.Equal(100UL, clock.Estimate(100, 20));
        time.Milliseconds = 250;
        Assert.Equal(105UL, clock.Estimate(100, 20));
        Assert.Equal(105UL, clock.Estimate(80, 20));
        // A newer acknowledgement/model tick must not have snapshot age added twice.
        Assert.Equal(120UL, clock.Estimate(120, 20));
        time.Milliseconds = 251;
        Assert.Equal(106UL, clock.Estimate(100, 20));
    }

    [Fact]
    public void FreshSnapshotsAndReconnectResetTheTimeAnchor()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        clock.Observe(100);
        time.Milliseconds = 500;
        clock.Observe(115);
        Assert.Equal(115UL, clock.Estimate(100, 20));
        clock.Reset();
        Assert.Equal(2UL, clock.Estimate(2, 20));
        time.Milliseconds = 1000;
        clock.Observe(4);
        Assert.Equal(4UL, clock.Estimate(4, 20));
    }

    [Fact]
    public void MonotonicObservationKeepsTheProjectionForDelayedSnapshots()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        clock.ObserveMonotonic(100, 20);

        time.Milliseconds = 300;
        clock.ObserveMonotonic(101, 20);
        clock.ObserveMonotonic(100, 20);
        clock.ObserveMonotonic(90, 20);

        Assert.Equal(106UL, clock.Estimate(0, 20));
    }

    [Fact]
    public void MonotonicObservationDoesNotAccumulateCeilingRounding()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        clock.ObserveMonotonic(100, 20);

        time.Milliseconds = 25;
        // 101 equals the one-tick ceiling projection, so it must not become a
        // new anchor and cause another rounded tick at the next sample.
        clock.ObserveMonotonic(101, 20);
        time.Milliseconds = 50;

        Assert.Equal(101UL, clock.Estimate(0, 20));
        clock.ObserveMonotonic(101, 20);
        time.Milliseconds = 75;
        Assert.Equal(102UL, clock.Estimate(0, 20));
    }

    [Fact]
    public void MonotonicObservationReanchorsOnlyForAGenuinelyAheadSnapshot()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        clock.ObserveMonotonic(100, 20);
        time.Milliseconds = 300;

        clock.ObserveMonotonic(107, 20);

        Assert.Equal(107UL, clock.Estimate(0, 20));
        clock.Reset();
        Assert.Equal(5UL, clock.Estimate(5, 20));
        time.Milliseconds = 400;
        clock.ObserveMonotonic(2, 40);
        Assert.Equal(2UL, clock.Estimate(0, 40));
        time.Milliseconds = 425;
        Assert.Equal(3UL, clock.Estimate(0, 40));
    }

    [Fact]
    public void RejectsInvalidRateAndOverflowRatherThanWrappingTicks()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Estimate(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.ObserveMonotonic(1, 0));
        Assert.Equal(0UL, clock.Estimate(0, 20));
        clock.Observe(ulong.MaxValue);
        time.Milliseconds = 50;
        Assert.Throws<OverflowException>(() => clock.Estimate(ulong.MaxValue, 20));
    }

    [Fact]
    public void MonotonicObservationDoesNotAddToASaturatedAnchor()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        clock.ObserveMonotonic(ulong.MaxValue, 20);
        time.Milliseconds = 50;

        clock.ObserveMonotonic(ulong.MaxValue, 20);

        Assert.Throws<OverflowException>(() => clock.Estimate(ulong.MaxValue, 20));
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
