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
    public void KeepsTheLargestSampleInsideTheWindow()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 1_320);
        Assert.Equal(1_320, window.Observe(5_000, 10));
        Assert.Equal(1_320, window.Observe(10_000, 10));
    }

    [Fact]
    public void AStallAgesOutOnceLaterProbesPassTheWindow()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 1_320);
        window.Observe(9_000, 250);
        Assert.Equal(250, window.Observe(10_001, 20));
        Assert.Equal(100, window.Observe(19_001, 20));
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
    public void ARisingSampleRaisesTheEstimateAtOnce()
    {
        var window = new ClockGatedRoundTripWindow(100, 10_000);
        window.Observe(0, 20);
        Assert.Equal(700, window.Observe(10, 700));
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
