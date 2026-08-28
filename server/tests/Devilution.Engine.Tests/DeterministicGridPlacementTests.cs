using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridPlacementTests
{
    [Fact]
    public void SelectsStableRowMajorAlignedCandidate()
    {
        var first = DeterministicGridPlacement.SelectAlignedWalkableFootprint(
            8,
            8,
            2,
            2,
            2,
            3,
            static (x, y) => x != 2 || y != 2);
        var second = DeterministicGridPlacement.SelectAlignedWalkableFootprint(
            8,
            8,
            2,
            2,
            2,
            3,
            static (x, y) => x != 2 || y != 2);

        Assert.Equal(first, second);
        Assert.Equal((6, 0), first);
    }

    [Fact]
    public void RejectsWhenNoAlignedFootprintIsWalkable()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeterministicGridPlacement.SelectAlignedWalkableFootprint(
                4,
                4,
                2,
                2,
                2,
                0,
                static (_, _) => false));
    }

    [Theory]
    [InlineData(0, 4, 1, 1, 1)]
    [InlineData(4, 4, 5, 1, 1)]
    [InlineData(4, 4, 1, 1, 0)]
    public void RejectsInvalidGeometry(
        int width,
        int height,
        int footprintWidth,
        int footprintHeight,
        int alignment)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicGridPlacement.SelectAlignedWalkableFootprint(
                width,
                height,
                footprintWidth,
                footprintHeight,
                alignment,
                0,
                static (_, _) => true));
    }
}
