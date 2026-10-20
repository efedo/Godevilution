using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeSceneViewportTests
{
    [Fact]
    public void EveryCellCenterRoundTripsAtCameraCenterAndBothClampedEdges()
    {
        foreach (var (cameraX, cameraY) in new[] { (0d, 0d), (53.25, 61.5), (111d, 111d), (-50d, 400d) }) {
            var viewport = NativeSceneViewport.Create(800, 480, 112, 112, cameraX, cameraY);
            for (var y = 0; y < 112; y++) for (var x = 0; x < 112; x++) {
                var screen = viewport.WorldToScreen(x, y);
                Assert.Equal((x, y), viewport.ScreenToTile(screen.X, screen.Y));
                Assert.True(viewport.ContainsScreenPoint(screen.X, screen.Y));
            }
        }
    }

    [Theory]
    [InlineData(1, 1, 0, 32)]
    [InlineData(1, 0, 32, 16)]
    [InlineData(1, -1, 64, 0)]
    [InlineData(0, -1, 32, -16)]
    [InlineData(-1, -1, 0, -32)]
    [InlineData(-1, 0, -32, -16)]
    [InlineData(-1, 1, -64, 0)]
    [InlineData(0, 1, -32, 16)]
    public void NativeDirectionsAndFractionalWalkingUseOneBasis(int dx, int dy, int screenX, int screenY)
    {
        var viewport = NativeSceneViewport.Create(800, 480, 112, 112, 55, 55);
        var start = viewport.WorldToScreen(55, 55);
        var end = viewport.WorldToScreen(55 + dx, 55 + dy);
        var halfway = viewport.WorldToScreen(55 + dx / 2d, 55 + dy / 2d);
        Assert.Equal((double)screenX, end.X - start.X);
        Assert.Equal((double)screenY, end.Y - start.Y);
        Assert.Equal(screenX / 2d, halfway.X - start.X);
        Assert.Equal(screenY / 2d, halfway.Y - start.Y);
        Assert.Equal((start.X, start.Y + 15), viewport.CellBottomAnchor(55, 55));
    }

    [Fact]
    public void DiamondEdgesHaveDeterministicOwnershipIncludingNegativeCoordinates()
    {
        var viewport = NativeSceneViewport.Create(800, 480, 112, 112, 55, 55);
        foreach (var (worldX, worldY) in new[] { (5d, 7d), (-1d, -3d), (111d, 111d) }) {
            var before = viewport.WorldToScreen(worldX + 0.5 - 0.00001, worldY);
            var edge = viewport.WorldToScreen(worldX + 0.5, worldY);
            var after = viewport.WorldToScreen(worldX + 0.5 + 0.00001, worldY);
            Assert.Equal(((int)worldX, (int)worldY), viewport.ScreenToTile(before.X, before.Y));
            Assert.Equal(((int)worldX + 1, (int)worldY), viewport.ScreenToTile(edge.X, edge.Y));
            Assert.Equal(((int)worldX + 1, (int)worldY), viewport.ScreenToTile(after.X, after.Y));
            var yBefore = viewport.WorldToScreen(worldX, worldY + 0.5 - 0.00001);
            var yEdge = viewport.WorldToScreen(worldX, worldY + 0.5);
            var yAfter = viewport.WorldToScreen(worldX, worldY + 0.5 + 0.00001);
            Assert.Equal(((int)worldX, (int)worldY), viewport.ScreenToTile(yBefore.X, yBefore.Y));
            Assert.Equal(((int)worldX, (int)worldY + 1), viewport.ScreenToTile(yEdge.X, yEdge.Y));
            Assert.Equal(((int)worldX, (int)worldY + 1), viewport.ScreenToTile(yAfter.X, yAfter.Y));
            foreach (var edgeX in new[] { -0.5, 0.5 }) foreach (var edgeY in new[] { -0.5, 0.5 }) {
                var corner = viewport.WorldToScreen(worldX + edgeX, worldY + edgeY);
                Assert.Equal(((int)worldX + (edgeX > 0 ? 1 : 0), (int)worldY + (edgeY > 0 ? 1 : 0)),
                    viewport.ScreenToTile(corner.X, corner.Y));
            }
        }
        var exterior = viewport.WorldToScreen(-1, 50);
        Assert.False(viewport.ContainsScreenPoint(exterior.X, exterior.Y));
        Assert.False(viewport.ContainsScreenPoint(double.NaN, 0));
    }

    [Fact]
    public void CentersSmallDiamondBoundsAndClampsLargeCameraBounds()
    {
        var small = NativeSceneViewport.Create(320, 240, 2, 4, 900, -900);
        Assert.Equal((192d, 88d), (small.OriginX, small.OriginY));
        var origin = NativeSceneViewport.Create(320, 240, 112, 112, 0, 0);
        Assert.Equal((160d, 16d), (origin.OriginX, origin.OriginY));
        var far = NativeSceneViewport.Create(320, 240, 112, 112, 111, 111);
        Assert.Equal((160d, -3328d), (far.OriginX, far.OriginY));
    }

    [Fact]
    public void TargetVisibilityUsesTheCandidateCameraAndHalfOpenViewportBounds()
    {
        var current = NativeSceneViewport.Create(240, 240, 112, 112, 55, 55);
        var targetFromCurrent = current.WorldToScreen(60, 55);
        Assert.False(current.ContainsViewportPoint(targetFromCurrent.X, targetFromCurrent.Y));

        var candidate = NativeSceneViewport.Create(240, 240, 112, 112, 60, 55);
        var targetFromCandidate = candidate.WorldToScreen(60, 55);
        Assert.True(candidate.ContainsViewportPoint(targetFromCandidate.X, targetFromCandidate.Y));
        Assert.False(candidate.ContainsViewportPoint(-0.01, 10));
        Assert.True(candidate.ContainsViewportPoint(0, 0));
        Assert.False(candidate.ContainsViewportPoint(240, 120));
        Assert.False(candidate.ContainsViewportPoint(120, double.PositiveInfinity));
    }

    [Fact]
    public void RejectsInvalidInputsAndHandlesDistantPointerCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSceneViewport.Create(0, 10, 1, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSceneViewport.Create(10, 10, 0, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSceneViewport.Create(10, 10, 1, 1, double.PositiveInfinity, 0));
        var viewport = NativeSceneViewport.Create(10, 10, 1, 1, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => viewport.ScreenToTile(double.NaN, 1));
        Assert.False(viewport.ContainsScreenPoint(1e30, 1e30));
    }
}
