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

    [Fact]
    public void NativeLineClearUsesNormalizedShallowAndSteepPathsWithNativeEndpoints()
    {
        var shallow = new List<(int X, int Y)>();
        Assert.True(DeterministicGridLineTraversal.HasNativeLineClear(
            0,
            0,
            4,
            2,
            (x, y) => {
                shallow.Add((x, y));
                return true;
            }));
        Assert.Equal([(1, 0), (2, 1), (3, 1), (4, 2)], shallow);

        var reverseShallow = new List<(int X, int Y)>();
        Assert.True(DeterministicGridLineTraversal.HasNativeLineClear(
            4,
            2,
            0,
            0,
            (x, y) => {
                reverseShallow.Add((x, y));
                return true;
            }));
        Assert.Equal([(1, 0), (2, 1), (3, 1)], reverseShallow);

        var steep = new List<(int X, int Y)>();
        Assert.True(DeterministicGridLineTraversal.HasNativeLineClear(
            0,
            0,
            2,
            4,
            (x, y) => {
                steep.Add((x, y));
                return true;
            }));
        Assert.Equal([(0, 1), (1, 2), (1, 3), (2, 4)], steep);
    }

    [Fact]
    public void NativeLineClearSkipsOriginalStartTestsOtherEndpointAndHandlesBounds()
    {
        var visited = new List<(int X, int Y)>();
        Assert.True(DeterministicGridLineTraversal.HasNativeLineClear(
            0,
            0,
            3,
            0,
            (x, y) => {
                visited.Add((x, y));
                return (x, y) != (0, 0);
            }));
        Assert.Equal([(1, 0), (2, 0), (3, 0)], visited);

        Assert.False(DeterministicGridLineTraversal.HasNativeLineClear(
            0,
            0,
            3,
            0,
            (x, y) => x != 3 || y != 0));
        var zeroLengthVisits = 0;
        Assert.True(DeterministicGridLineTraversal.HasNativeLineClear(
            2,
            2,
            2,
            2,
            (_, _) => {
                zeroLengthVisits++;
                return false;
            }));
        Assert.Equal(0, zeroLengthVisits);

        var overLimitVisits = 0;
        Assert.False(DeterministicGridLineTraversal.HasNativeLineClear(
            0,
            0,
            5,
            0,
            (_, _) => {
                overLimitVisits++;
                return true;
            },
            maximumSteps: 4));
        Assert.Equal(0, overLimitVisits);
    }
}
