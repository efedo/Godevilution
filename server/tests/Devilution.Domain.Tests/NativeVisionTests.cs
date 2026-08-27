using Devilution.Domain;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class NativeVisionTests
{
    [Fact]
    public void UsesTheNativeRayPathInsteadOfAContinuousLine()
    {
        var blocked = new HashSet<(int X, int Y)> { (1, 0) };

        Assert.True(IsVisible(8, 4, 10, blocked));
    }

    [Fact]
    public void DiagonalRayStopsWhenBothAdjacentCellsBlockLight()
    {
        var blocked = new HashSet<(int X, int Y)> { (1, 0), (0, 1) };

        Assert.False(IsVisible(8, 4, 10, blocked));
    }

    [Fact]
    public void RadiusTrimsTheNativeRayLength()
    {
        Assert.False(IsVisible(8, 4, 9, []));
        Assert.True(IsVisible(8, 4, 10, []));
    }

    [Fact]
    public void RejectsTargetsOutsideTheBounds()
    {
        Assert.False(NativeVision.IsVisible(
            0,
            0,
            8,
            4,
            10,
            static (_, _) => true,
            static (x, y) => x >= 0 && y >= 0 && x < 8 && y < 8));
    }

    private static bool IsVisible(int targetX, int targetY, int radius, HashSet<(int X, int Y)> blocked)
    {
        return NativeVision.IsVisible(
            0,
            0,
            targetX,
            targetY,
            radius,
            (x, y) => !blocked.Contains((x, y)),
            static (x, y) => x >= 0 && y >= 0 && x < 16 && y < 16);
    }
}
