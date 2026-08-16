using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridNavigationTests
{
    [Fact]
    public void FindsTheFirstStepUsingTheStableNeighborOrder()
    {
        var step = DeterministicGridNavigation.FindStep(
            0,
            0,
            3,
            0,
            maximumDepth: 8,
            isWalkable: static (_, _) => true,
            isOccupied: static (_, _) => false);

        Assert.Equal((1, 0), step);
    }

    [Fact]
    public void SkipsBlockedCellsAndChoosesTheNextStableRoute()
    {
        var step = DeterministicGridNavigation.FindStep(
            0,
            0,
            3,
            0,
            maximumDepth: 8,
            isWalkable: static (x, y) => (x, y) != (1, 0),
            isOccupied: static (_, _) => false);

        Assert.Equal((1, 1), step);
    }

    [Fact]
    public void HonorsDepthAndOccupancyBoundaries()
    {
        var depthLimited = DeterministicGridNavigation.FindStep(
            0,
            0,
            5,
            0,
            maximumDepth: 1,
            isWalkable: static (_, _) => true,
            isOccupied: static (_, _) => false);
        var occupied = DeterministicGridNavigation.FindStep(
            0,
            0,
            1,
            0,
            maximumDepth: 8,
            isWalkable: static (_, _) => true,
            isOccupied: static (x, y) => (x, y) == (1, 0));

        Assert.Null(depthLimited);
        Assert.Equal((1, 1), occupied);
    }

    [Fact]
    public void RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicGridNavigation.FindStep(
                0,
                0,
                1,
                0,
                -1,
                static (_, _) => true,
                static (_, _) => false));
        Assert.Throws<ArgumentNullException>(() =>
            DeterministicGridNavigation.FindStep(
                0,
                0,
                1,
                0,
                1,
                null!,
                static (_, _) => false));
    }
}
