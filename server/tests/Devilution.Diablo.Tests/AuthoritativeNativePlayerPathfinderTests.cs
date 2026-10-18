using Xunit;

namespace Devilution.Diablo.Tests;

/** Golden paths are shared with native PathTest; continuation lifecycle is tested separately. */
public sealed class AuthoritativeNativePlayerPathfinderTests
{
    [Theory]
    [InlineData(7, 7, 5)]
    [InlineData(8, 7, 1)]
    [InlineData(9, 7, 6)]
    [InlineData(7, 8, 2)]
    [InlineData(9, 8, 3)]
    [InlineData(7, 9, 8)]
    [InlineData(8, 9, 4)]
    [InlineData(9, 9, 7)]
    public void EntersEachAdjacentDestinationWithNativeDirection(int x, int y, int expected)
        => Assert.Equal([expected], Find(8, 8, x, y));

    [Fact]
    public void OpenMapPathsMatchNativeDirectionAndTieBreakGoldens()
    {
        Assert.Empty(Find(8, 8, 8, 8));
        Assert.Equal([1, 1], Find(8, 8, 8, 6));
        Assert.Equal([2, 2], Find(8, 8, 6, 8));
        Assert.Equal([3, 3], Find(8, 8, 10, 8));
        Assert.Equal([4, 4], Find(8, 8, 8, 10));
        Assert.Equal([5, 5, 2], Find(8, 8, 5, 6));
        Assert.Equal([5, 5, 5, 5], Find(8, 8, 4, 4));
        Assert.Equal([7, 7, 4, 4], Find(8, 8, 10, 12));
        Assert.Equal([7, 7, 7, 7, 4, 4, 4, 4, 4, 4, 4, 4], Find(8, 8, 12, 20));
    }

    [Fact]
    public void NativeMaximumLengthIncludesTheEndpointAndRefusesAnOverlongPath()
    {
        Assert.Equal(Enumerable.Repeat(7, 24), Find(56, 56, 80, 80, 24));
        Assert.Empty(Find(56, 56, 81, 81, 24));
        Assert.Empty(Find(56, 56, 0, 0, 24));
        Assert.Equal(Enumerable.Repeat(3, 100), Find(8, 8, 108, 8, 100));
    }

    [Fact]
    public void BlockedEndpointIsRetainedWithoutCallingStepPredicateForThatEndpoint()
    {
        var path = AuthoritativeNativePlayerPathfinder.FindPath(8, 8, 10, 8, 24,
            (x, y) => x != 10 || y != 8,
            (_, _, x, y) => {
                Assert.False(x == 10 && y == 8);
                return true;
            });
        Assert.Equal([3, 3], path);
    }

    [Fact]
    public void ObstacleDetourUsesNativeCoordinateTieBreak()
        => Assert.Equal([6, 7], AuthoritativeNativePlayerPathfinder.FindPath(8, 8, 10, 8, 24,
            (x, y) => x != 9 || y != 8, (_, _, _, _) => true));

    [Fact]
    public void NativeCallerCanRefuseDiagonalCornerCrossing()
    {
        static bool Open(int x, int y) => x != 9 || y != 8;
        var path = AuthoritativeNativePlayerPathfinder.FindPath(8, 8, 9, 9, 24, Open,
            (x, y, nx, ny) => x == nx || y == ny || Open(nx, y) && Open(x, ny));
        Assert.Equal([4, 3], path);
    }

    [Fact]
    public void UnreachableGoalTerminatesWithinNativeSearchStorageBounds()
        => Assert.Empty(AuthoritativeNativePlayerPathfinder.FindPath(56, 56, 61, 56, 100,
            (x, y) => x is >= 0 and < 112 && y is >= 0 and < 112 && x != 60,
            (_, _, _, _) => true));

    [Fact]
    public void DirectionCodesRoundTripTheNativeDisplacementTable()
    {
        var expected = new (int, int)[] { (0, -1), (-1, 0), (1, 0), (0, 1), (-1, -1), (1, -1), (1, 1), (-1, 1) };
        Assert.Equal(expected, Enumerable.Range(1, 8).Select(AuthoritativeNativePlayerPathfinder.GetDisplacement));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeNativePlayerPathfinder.GetDisplacement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeNativePlayerPathfinder.GetDisplacement(9));
    }

    [Fact]
    public void InvalidNativeCoordinatesAndLengthsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Find(-1, 8, 8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => Find(8, 8, 256, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => Find(8, 8, 9, 8, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Find(8, 8, 9, 8, 101));
        Assert.Empty(Find(8, 8, 9, 8, 0));
    }

    private static int[] Find(int x, int y, int tx, int ty, int maximumLength = 24)
        => AuthoritativeNativePlayerPathfinder.FindPath(x, y, tx, ty, maximumLength,
            (_, _) => true, (_, _, _, _) => true);
}
