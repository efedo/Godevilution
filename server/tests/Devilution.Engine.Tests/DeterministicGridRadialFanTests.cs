using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridRadialFanTests
{
    [Fact]
    public void EnumeratePreservesNativeMirroredQuarterOrderAndCount()
    {
        var offsets = DeterministicGridRadialFan.Enumerate(2, 12);

        Assert.Equal(
            new[] {
                (2, 0), (-2, 0), (-2, 0), (2, 0),
                (2, 1), (-2, -1), (-2, 1), (2, -1),
                (2, 2), (-2, -2), (-2, 2), (2, -2),
            },
            offsets);
    }

    [Fact]
    public void EnumerateAllowsZeroProjectileCount()
    {
        Assert.Empty(DeterministicGridRadialFan.Enumerate(4, 0));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void EnumerateRejectsNegativeInputs(int radius, int projectileCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicGridRadialFan.Enumerate(radius, projectileCount));
    }
}
