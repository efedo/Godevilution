using Devilution.Client.PresentationAssets;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerPresentationClockTests
{
    [Fact]
    public void EntryReentryAndEpochStartAtZeroWithoutAccumulatingDungeonTime()
    {
        var clock = new NativeTownerPresentationClock();
        Assert.Equal(0, clock.Advance(20, 20, true, true));
        Assert.Equal(2, clock.Advance(0.1, 20, true, true));
        Assert.Equal(0, clock.Advance(50, 20, false, true));
        Assert.Equal(0, clock.Advance(50, 20, true, true));
        Assert.Equal(2, clock.Advance(0.1, 20, true, true));
        Assert.Equal(0, clock.Advance(0.1, 20, true, true, reset: true));
        clock.Reset();
        Assert.Equal(0, clock.Tick);
        Assert.Equal(0, clock.Advance(0.1, 20, true, true));
    }

    [Fact]
    public void DisconnectedPendingAndInvalidFrameDeltasDoNotAdvanceCosmeticTime()
    {
        var clock = new NativeTownerPresentationClock();
        clock.Advance(0, 20, true, true);
        Assert.Equal(0, clock.Advance(1, 20, true, false));
        foreach (var delta in new[] { 0D, -1D, double.NaN, double.PositiveInfinity })
            Assert.Equal(0, clock.Advance(delta, 20, true, true));
        Assert.Equal(5, clock.Advance(1000, 20, true, true));
        Assert.Equal(5, clock.Advance(1000, 20, true, false));
        Assert.Equal(7, clock.Advance(0.1, 20, true, true));
    }
}
