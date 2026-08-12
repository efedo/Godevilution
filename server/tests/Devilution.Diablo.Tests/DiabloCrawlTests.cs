using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloCrawlTests
{
    [Fact]
    public void SearchOrderMatchesNativeCrawlTableIteration()
    {
        var visited = new List<(int X, int Y)>();

        var result = DiabloCrawl.FindClosestValidPosition(
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
            DiabloCrawl.FindClosestValidPosition(0, 0, 2, 1, static (_, _) => true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DiabloCrawl.FindClosestValidPosition(0, 0, 0, 51, static (_, _) => true));
    }
}
