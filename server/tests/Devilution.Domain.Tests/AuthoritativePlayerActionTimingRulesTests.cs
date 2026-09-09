using Devilution.Domain.Protocol;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class AuthoritativePlayerActionTimingRulesTests
{
    [Fact]
    public void SnapshotBoundaryIsInclusiveAtEffectAndEndTicks()
    {
        Assert.True(AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(
            snapshotTick: 10,
            startedAtTick: 8,
            effectTick: 9,
            readyTick: 10,
            endTick: 10,
            effectApplied: true));
    }

    [Fact]
    public void SnapshotBeforeStartOrAfterEndIsRejected()
    {
        Assert.False(AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(7, 8, 9, 10, 10, false));
        Assert.False(AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(11, 8, 9, 10, 10, true));
    }

    [Fact]
    public void EffectAppliedMustMatchSnapshotBoundary()
    {
        Assert.False(AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(8, 8, 9, 10, 10, true));
        Assert.False(AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(9, 8, 9, 10, 10, false));
    }

    [Fact]
    public void OrderedLifecycleRejectsInvalidTickOrdering()
    {
        Assert.True(AuthoritativePlayerActionTimingRules.IsOrdered(8, 9, 10, 10));
        Assert.False(AuthoritativePlayerActionTimingRules.IsOrdered(9, 8, 10, 10));
        Assert.False(AuthoritativePlayerActionTimingRules.IsOrdered(8, 9, 8, 10));
        Assert.False(AuthoritativePlayerActionTimingRules.IsOrdered(8, 9, 10, 9));
    }
}
