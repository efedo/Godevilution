using Devilution.Client.PresentationAssets;
using Devilution.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Client game-speed steps and presentation pacing at the scaled tick rate. */
public sealed class GameSpeedPresentationTests
{
    [Theory]
    [InlineData(1U, 1, 2U)]
    [InlineData(2U, 1, 3U)]
    [InlineData(3U, 1, 5U)]
    [InlineData(10U, 1, 20U)]
    [InlineData(20U, 1, 20U)]
    [InlineData(20U, -1, 10U)]
    [InlineData(1U, -1, 1U)]
    [InlineData(4U, 1, 5U)] // A saved value between steps moves from the step below it.
    [InlineData(4U, -1, 2U)]
    public void StepsMoveThroughTheSelectableSet(uint current, int direction, uint expected)
        => Assert.Equal(expected, GameSpeedLimits.Step(current, direction));

    [Theory]
    [InlineData(0U, 1U)]
    [InlineData(7U, 5U)]
    [InlineData(19U, 10U)]
    [InlineData(99U, 20U)]
    public void SavedValuesSnapToAStep(uint saved, uint expected)
        => Assert.Equal(expected, GameSpeedLimits.NearestStep(saved));

    /**
     * 20x of the 20 Hz reference is 400 Hz, faster than the 60 Hz frame rate: snapshots every
     * 10 ms poll arrive 5 ms late. Every frame must still advance presentation time (no frozen
     * frames), keep within one tick of authority and never run backward.
     */
    [Theory]
    [InlineData(20U)]
    [InlineData(400U)]
    public void PresentationAdvancesEveryFrameAtTheScaledRate(uint tickRateHz)
    {
        var clock = new AuthoritativePresentationClock();
        const double frameSeconds = 1d / 60;
        const double pollSeconds = 0.010;
        const double latencySeconds = 0.005;
        double? previous = null;
        var frozenFrames = 0;
        for (var frame = 1; frame <= 600; frame++) {
            var now = frame * frameSeconds;
            // Newest snapshot received by now: polled at k * pollSeconds, delivered latencySeconds later.
            var polledAt = Math.Floor((now - latencySeconds) / pollSeconds) * pollSeconds;
            var authoritativeTick = (ulong)Math.Floor(Math.Max(0, polledAt) * tickRateHz);
            var current = clock.Advance(authoritativeTick, frameSeconds, tickRateHz);
            Assert.InRange(current, authoritativeTick, authoritativeTick + 1d);
            if (previous is { } last) {
                Assert.True(current >= last);
                if (frame > 10 && current == last)
                    frozenFrames++;
            }
            previous = current;
        }
        Assert.Equal(0, frozenFrames);
        Assert.InRange(previous!.Value, 600 * frameSeconds * tickRateHz - 1 - tickRateHz * 0.02, 600 * frameSeconds * tickRateHz + 1);
    }

    [Fact]
    public void TownerAnimationFollowsTheMaximumGameSpeed()
    {
        var clock = new NativeTownerPresentationClock();
        clock.Advance(0, 400, true, true);
        Assert.Equal(40, clock.Advance(0.1, 400, true, true), 6);
        Assert.Equal(80, clock.Advance(0.1, 4000, true, true), 6);
    }
}
