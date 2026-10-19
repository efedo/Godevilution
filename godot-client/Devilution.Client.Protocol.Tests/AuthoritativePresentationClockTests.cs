using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePresentationClockTests
{
    [Fact]
    public void AdvancesAtTheServerRateButStopsOneTickAhead()
    {
        var clock = new AuthoritativePresentationClock();

        Assert.Equal(12, clock.Advance(12, 0, 20));
        Assert.Equal(12.5, clock.Advance(12, 0.025, 20), precision: 6);
        Assert.Equal(13, clock.Advance(12, 1, 20));
    }

    [Fact]
    public void FollowsAForwardAuthoritativeTickWithoutRegressing()
    {
        var clock = new AuthoritativePresentationClock();

        clock.Advance(12, 0, 20);

        Assert.Equal(15, clock.Advance(15, 0, 20));
        Assert.Equal(16, clock.Advance(15, 1, 20));
    }

    [Fact]
    public void ResetsWhenAnOlderAuthoritativeSnapshotStartsANewStream()
    {
        var clock = new AuthoritativePresentationClock();

        clock.Advance(20, 0, 20);

        Assert.Equal(8, clock.Advance(8, 0, 20));
        Assert.Equal((ulong)8, clock.AuthoritativeSnapshotTick!.Value);
        Assert.True(clock.LastAdvanceRewound);
        Assert.Equal(8, clock.Advance(8, 0, 20));
        Assert.False(clock.LastAdvanceRewound);
    }

    [Fact]
    public void SanitizesInvalidFrameDeltaAndUsesTheDefaultRate()
    {
        var clock = new AuthoritativePresentationClock();

        Assert.Equal(10, clock.Advance(10, double.NaN, 0));
        Assert.Equal(11, clock.Advance(10, 1, 0));
        Assert.Equal(11, clock.Advance(10, -1, 0));
    }

    [Fact]
    public void ResetClearsThePresentationStream()
    {
        var clock = new AuthoritativePresentationClock();

        clock.Advance(10, 0.1, 20);
        clock.Reset();

        Assert.Null(clock.CurrentTick);
        Assert.Null(clock.AuthoritativeSnapshotTick);
        Assert.False(clock.LastAdvanceRewound);
        Assert.Equal(4, clock.Advance(4, 0, 20));
    }
}
