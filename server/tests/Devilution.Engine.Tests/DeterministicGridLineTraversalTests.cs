using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridLineTraversalTests
{
    [Fact]
    public void IgnoresEndpointsAndRejectsBlockedInterior()
    {
        var blocked = new HashSet<(int X, int Y)> { (2, 2) };

        Assert.True(DeterministicGridLineTraversal.HasLineOfSight(
            0, 0, 1, 1, (_, _) => true));
        Assert.False(DeterministicGridLineTraversal.HasLineOfSight(
            0, 0, 4, 4, (x, y) => blocked.Contains((x, y))));
        Assert.True(DeterministicGridLineTraversal.HasLineOfSight(
            0, 0, 4, 4, (x, y) => x == 0 && y == 0 || x == 4 && y == 4));
    }

    [Fact]
    public void TraversesFullRangeCoordinatesWithoutOverflow()
    {
        var visited = 0;

        Assert.False(DeterministicGridLineTraversal.HasLineOfSight(
            int.MinValue,
            int.MinValue,
            int.MaxValue,
            int.MaxValue,
            (_, _) => {
                visited++;
                return false;
            }));
        Assert.Equal(0, visited);

        Assert.True(DeterministicGridLineTraversal.HasLineOfSight(
            int.MaxValue - 3,
            int.MaxValue - 3,
            int.MaxValue,
            int.MaxValue,
            (_, _) => false));
    }
}
