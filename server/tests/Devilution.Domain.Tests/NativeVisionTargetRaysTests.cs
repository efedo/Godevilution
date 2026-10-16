using Devilution.Shared;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class NativeVisionTargetRaysTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(99)]
    public void MatchesOriginalRaysAcrossMasksQuadrantsRadiusAndEdges(int radius)
    {
        static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < 33 && y < 33;
        foreach (var pattern in Enumerable.Range(0, 5)) {
            bool Passes(int x, int y) => InBounds(x, y) && pattern switch {
                0 => true,
                1 => !((x == 17 && y == 16) || (x == 16 && y == 17)),
                2 => x != 17 || y == 20,
                3 => unchecked((uint)(x * 73856093 ^ y * 19349663)) % 7 != 0,
                _ => (x + y) % 2 == 0,
            };
            foreach (var viewer in new[] { (X: 16, Y: 16), (X: 0, Y: 0), (X: 32, Y: 16) })
                for (var dy = -16; dy <= 16; dy++)
                    for (var dx = -16; dx <= 16; dx++) {
                        var x = viewer.X + dx; var y = viewer.Y + dy;
                        var expected = NativeVision.IsVisible(viewer.X, viewer.Y, x, y, radius, Passes, InBounds);
                        var actual = NativeVision.IsVisibleOnTargetRays(viewer.X, viewer.Y, x, y, radius, Passes, InBounds);
                        Assert.True(expected == actual,
                            $"pattern={pattern},radius={radius},viewer={viewer},target=({x},{y})");
                    }
        }
    }
}
