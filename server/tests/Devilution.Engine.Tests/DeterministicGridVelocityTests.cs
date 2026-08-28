using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridVelocityTests
{
    [Fact]
    public void StationaryTargetProducesNoVelocity()
    {
        Assert.Equal(
            (0, 0),
            DeterministicGridVelocity.CalculateNative(4, 7, 4, 7, 32));
    }

    [Fact]
    public void HorizontalVelocityUsesNativeRotatedGridScaling()
    {
        Assert.Equal(
            (1482880, 741440),
            DeterministicGridVelocity.CalculateNative(0, 0, 10, 0, 32));
    }

    [Fact]
    public void MirroredHorizontalVelocityPreservesNativeSign()
    {
        Assert.Equal(
            (-1482880, -741440),
            DeterministicGridVelocity.CalculateNative(0, 0, -10, 0, 32));
    }
}
