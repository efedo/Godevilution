using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridMetricsTests
{
    [Fact]
    public void ComputesChebyshevDistanceForOrdinaryCoordinates()
    {
        Assert.Equal(7, DeterministicGridMetrics.ChebyshevDistance(2, 4, -5, 1));
        Assert.Equal(0, DeterministicGridMetrics.ChebyshevDistance(12, -3, 12, -3));
    }

    [Fact]
    public void ComputesExtremeCoordinateDistanceWithoutOverflow()
    {
        Assert.Equal(
            4_294_967_295L,
            DeterministicGridMetrics.ChebyshevDistance(
                int.MinValue,
                int.MaxValue,
                int.MaxValue,
                int.MinValue));
    }
}
