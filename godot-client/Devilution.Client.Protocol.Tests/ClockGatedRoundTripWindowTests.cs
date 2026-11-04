using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClockGatedRoundTripWindowTests
{
    [Fact]
    public void StartsAtTheFloorAndNeverDropsBelowIt()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        Assert.Equal(100, window.Estimate);
        Assert.Equal(100, window.Observe(0, 0));
        Assert.Equal(100, window.Observe(50, 15));
    }

    [Fact]
    public void NegativeSamplesCountAsZero()
    {
        var window = new ClockGatedRoundTripWindow(0, 10_000);
        Assert.Equal(0, window.Observe(0, -5));
    }

    [Fact]
    public void ALoneStallDoesNotRaiseTheEstimate()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 20);
        window.Observe(10, 20);
        Assert.Equal(100, window.Observe(20, 2_500));
    }

    [Fact]
    public void ASustainedRiseAgesOutOnceLaterProbesPassTheWindow()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 500);
        window.Observe(1, 500);
        Assert.Equal(500, window.Observe(2, 500));
        Assert.Equal(100, window.Observe(10_100, 20));
    }

    [Fact]
    public void KeepsTheNewestSampleAfterALongQuietGap()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 50);
        Assert.Equal(400, window.Observe(60_000, 400));
        Assert.Equal(400, window.Observe(60_010, 0));
    }

    [Fact]
    public void ARiseCountsOnceItIsTheMedian()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 20);
        window.Observe(1, 20);
        window.Observe(2, 700);
        Assert.Equal(700, window.Observe(3, 700));
    }

    [Fact]
    public void DefaultsMatchTheInitialClientEstimate()
    {
        var window = new ClockGatedRoundTripWindow();
        Assert.Equal(ClockGatedRoundTripWindow.DefaultFloorMilliseconds, window.Estimate);
        window.Observe(0, 2_000);
        Assert.Equal(2_000, window.Observe(ClockGatedRoundTripWindow.DefaultWindowMilliseconds, 0));
        Assert.Equal(ClockGatedRoundTripWindow.DefaultFloorMilliseconds,
            window.Observe(ClockGatedRoundTripWindow.DefaultWindowMilliseconds + 1, 0));
    }
}
