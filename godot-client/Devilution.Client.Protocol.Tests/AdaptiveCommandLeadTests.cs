using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AdaptiveCommandLeadTests
{
    [Theory]
    [InlineData(10U)] [InlineData(20U)] [InlineData(200U)]
    public void IdleLoopbackKeepsOneTickWithoutTheProbeOrRetryFloor(uint rate)
    {
        var lead = new AdaptiveCommandLead();
        Assert.Equal(1, lead.Ticks(0, rate));
        lead.ObserveRoundTrip(0, 0);
        lead.ObserveRoundTrip(250, 2);
        Assert.Equal(1, lead.Ticks(250, rate));
    }

    [Fact]
    public void SlowerLinksCoverTheClockRepliesAgeAndTheNextOutboundTrip()
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveRoundTrip(0, 180);
        Assert.Equal(4, lead.Ticks(0, 20));
        Assert.Equal(37, lead.Ticks(0, 200));
    }

    [Fact]
    public void JitterRaisesTheMarginImmediatelyAndDecaysWithoutRaisingTheMedian()
    {
        var lead = new AdaptiveCommandLead();
        for (var i = 0; i < 3; i++) lead.ObserveRoundTrip(0, 2);
        lead.ObserveRoundTrip(0, 40);
        Assert.Equal(2, lead.RoundTripMilliseconds);
        Assert.Equal(9, lead.Ticks(0, 200));
        Assert.Equal(5, lead.Ticks(1000, 200));
        Assert.Equal(1, lead.Ticks(10_000, 200));
    }

    public static TheoryData<double[], int[]> AlternatingRoundTrips => new() {
        { [2, 40, 2], [1, 9, 8] },
        { [2, 40, 2, 40, 2, 40, 2], [1, 9, 8, 9, 8, 9, 8] },
        { [2, 15, 2, 30, 2, 60, 2], [1, 4, 3, 7, 6, 13, 11] },
        { [1, 5, 1, 80, 1], [1, 2, 2, 17, 14] },
    };

    [Theory]
    [MemberData(nameof(AlternatingRoundTrips))]
    public void AlternatingLatencyRetainsTheSpikeUntilItsMarginDecays(double[] samples, int[] ticks)
    {
        var lead = new AdaptiveCommandLead();
        for (var i = 0; i < samples.Length; i++) {
            lead.ObserveRoundTrip(i * 250, samples[i]);
            Assert.Equal(ticks[i], lead.Ticks(i * 250, 200));
        }
        var lastSpikeAt = (samples.Length - 2) * 250;
        Assert.Equal(1, lead.Ticks(lastSpikeAt + 10_000, 200));
    }

    [Fact]
    public void SustainedRttChangesFollowTheMeasuredWindowInBothDirections()
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveRoundTrip(0, 2);
        for (var i = 0; i < 4; i++) lead.ObserveRoundTrip(0, 250);
        Assert.Equal(250, lead.RoundTripMilliseconds);
        Assert.Equal(6, lead.Ticks(0, 20));
        lead.ObserveRoundTrip(10_001, 2);
        Assert.Equal(2, lead.RoundTripMilliseconds);
        Assert.Equal(1, lead.Ticks(10_001, 20));
    }

    [Fact]
    public void LateAdmissionAddsTheShortfallAndATickThenReturnsToIdle()
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveLateAdmission(0, 200, sentLeadTicks: 1, lateTicks: 2);
        Assert.Equal(4, lead.Ticks(0, 200));
        Assert.Equal(2, lead.Ticks(1000, 200));
        Assert.Equal(1, lead.Ticks(4000, 200));
        lead.ObserveLateAdmission(4000, 200, 1, 0);
        Assert.Equal(1, lead.Ticks(4000, 200));
    }

    [Fact]
    public void RepeatedLatenessRaisesTheNewLeadRatherThanDiscardingFeedback()
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveLateAdmission(0, 20, 1, 1);
        Assert.Equal(3, lead.Ticks(0, 20));
        lead.ObserveLateAdmission(0, 20, 3, 2);
        Assert.Equal(6, lead.Ticks(0, 20));
        // An older, smaller shortfall does not lower the current margin.
        lead.ObserveLateAdmission(0, 20, 1, 1);
        Assert.Equal(6, lead.Ticks(0, 20));
    }

    [Theory]
    [InlineData(20U, 10)] [InlineData(200U, 100)]
    public void AStallCannotScheduleMoreThanHalfASecondAhead(uint rate, int maximumTicks)
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveRoundTrip(0, 10_000);
        lead.ObserveLateAdmission(0, rate, int.MaxValue, ulong.MaxValue);
        Assert.Equal(maximumTicks, lead.Ticks(0, rate));
    }

    [Fact]
    public void ANewConnectionDoesNotInheritOldCongestion()
    {
        var previous = new AdaptiveCommandLead();
        previous.ObserveLateAdmission(0, 20, 1, 8);
        Assert.Equal(10, previous.Ticks(0, 20));
        Assert.Equal(1, new AdaptiveCommandLead().Ticks(0, 20));
    }
}
