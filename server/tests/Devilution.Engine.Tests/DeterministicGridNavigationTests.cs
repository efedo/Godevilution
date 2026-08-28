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
    public void DoesNotWrapNeighborCoordinatesAtIntegerMaximum()
    {
        var step = DeterministicGridNavigation.FindStep(
            int.MaxValue,
            int.MaxValue,
            int.MinValue,
            int.MinValue,
            maximumDepth: 1,
            isWalkable: static (_, _) => true,
            isOccupied: static (_, _) => false);

        Assert.Null(step);
    }

    [Fact]
    public void RejectsExtremeAdjacencyDistanceWithoutOverflowing()
    {
        var step = DeterministicGridNavigation.FindStep(
            0,
            0,
            int.MaxValue,
            int.MaxValue,
            maximumDepth: 1,
            isWalkable: static (_, _) => true,
            isOccupied: static (_, _) => false);

        Assert.Null(step);
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
    public void FindsTheFirstStepToAnyCallerSelectedGoal()
    {
        var step = DeterministicGridNavigation.FindStepToGoal(
            0,
            0,
            maximumDepth: 8,
            isGoal: static (x, y) => (x, y) is (2, 1) or (3, 0),
            isWalkable: static (_, _) => true,
            isOccupied: static (_, _) => false);

        Assert.Equal((1, 0), step);
    }

    [Fact]
    public void ExposesTheStableNeighborOrderUsedBySearch()
    {
        Assert.Equal(
            new[] {
                (1, 0),
                (1, 1),
                (0, 1),
                (-1, 1),
                (-1, 0),
                (-1, -1),
                (0, -1),
                (1, -1),
            },
            DeterministicGridNavigation.NeighborDisplacements);
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
        Assert.Throws<ArgumentNullException>(() =>
            DeterministicGridNavigation.FindStepToGoal(
                0,
                0,
                1,
                null!,
                static (_, _) => true,
                static (_, _) => false));
    }
}
