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
    public void RejectsInvalidRateAndOverflowRatherThanWrappingTicks()
    {
        var time = new ManualClock();
        var clock = new AuthoritativeCommandClock(time);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Estimate(1, 0));
        clock.Observe(ulong.MaxValue);
        time.Milliseconds = 50;
        Assert.Throws<OverflowException>(() => clock.Estimate(ulong.MaxValue, 20));
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
