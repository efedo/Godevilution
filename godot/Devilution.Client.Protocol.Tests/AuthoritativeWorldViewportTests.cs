using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeWorldViewportTests
{
    [Fact]
    public void CentersSmallLevelsAndRoundTripsTileCenters()
    {
        var viewport = AuthoritativeWorldViewport.Create(
            1280,
            720,
            10,
            6,
            4,
            2);

        Assert.Equal((480d, 264d), (viewport.OriginX, viewport.OriginY));
        var screen = viewport.WorldToScreen(4, 2);
        Assert.Equal((624d, 344d), screen);
        Assert.Equal((4, 2), viewport.ScreenToTile(screen.X, screen.Y));
        Assert.True(viewport.ContainsScreenPoint(screen.X, screen.Y));
    }

    [Fact]
    public void ClampsLargeLevelsAtBothCameraEdges()
    {
        var left = AuthoritativeWorldViewport.Create(320, 240, 40, 30, 0, 0);
        var right = AuthoritativeWorldViewport.Create(320, 240, 40, 30, 39, 29);

        Assert.Equal(0d, left.OriginX);
        Assert.Equal(0d, left.OriginY);
        Assert.Equal(-960d, right.OriginX);
        Assert.Equal(-720d, right.OriginY);
    }

    [Fact]
    public void InputOutsideTheClampedLevelIsRejected()
    {
        var viewport = AuthoritativeWorldViewport.Create(640, 480, 10, 6, 5, 3);

        Assert.False(viewport.ContainsScreenPoint(0, 0));
        Assert.True(viewport.ContainsScreenPoint(320, 240));
        Assert.Equal((-5, -5), viewport.ScreenToTile(0, 0));
    }

    [Fact]
    public void RejectsInvalidDimensionsAndTileSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeWorldViewport.Create(0, 480, 40, 30, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeWorldViewport.Create(640, 480, 40, 30, double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeWorldViewport.Create(640, 480, 40, 30, 0, 0, 0));
    }
}
