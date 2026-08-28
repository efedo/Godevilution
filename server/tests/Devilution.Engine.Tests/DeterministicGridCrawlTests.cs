using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridCrawlTests
{
    [Fact]
    public void SearchOrderMatchesNativeCrawlTableIteration()
    {
        var visited = new List<(int X, int Y)>();

        var result = DeterministicGridCrawl.FindClosestValidPosition(
            10,
            20,
            0,
            2,
            (x, y) => {
                visited.Add((x - 10, y - 20));
                return x == 8 && y == 19;
            });

        Assert.Equal((8, 19), result);
        Assert.Equal([
            (0, 0),
            (0, 1), (0, -1), (-1, 0), (1, 0),
            (0, 2), (0, -2),
            (-1, 2), (1, 2), (-1, -2), (1, -2),
            (-1, 1), (1, 1), (-1, -1), (1, -1),
            (-2, 0), (2, 0), (-2, 1), (2, 1), (-2, -1),
        ], visited);
    }

    [Fact]
    public void SearchValidatesItsNativeSupportedRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicGridCrawl.FindClosestValidPosition(0, 0, 2, 1, static (_, _) => true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicGridCrawl.EnumerateDisplacements(0, 51).ToArray());
    }

    [Fact]
    public void SearchSkipsCandidatesThatOverflowTheCoordinateDomain()
    {
        var result = DeterministicGridCrawl.FindClosestValidPosition(
            int.MaxValue,
            int.MaxValue,
            1,
            1,
            static (_, _) => false);

        Assert.Null(result);
    }

    [Fact]
    public void EnumerationIsStableAndIndependentForEachCall()
    {
        var first = DeterministicGridCrawl.EnumerateDisplacements(1, 1).ToArray();
        var second = DeterministicGridCrawl.EnumerateDisplacements(1, 1).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(4, first.Length);
    }
}
