using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerPlacementTests
{
    [Theory]
    [InlineData(96, 96)]
    [InlineData(128, 128)]
    public void ClippedAnchorRetainsVisibleSpriteAndCountsOnlyIntersectingRasters(int alignmentWidth, int width)
    {
        var horizontalOffset = alignmentWidth / 2;
        var clippedAnchor = NativeScenePlacement.ResolveResidentBounds(-1, 100, alignmentWidth, width, 80);
        Assert.True(clippedAnchor.IntersectsViewport(200, 150));
        Assert.Equal(-1 - horizontalOffset, clippedAnchor.X);
        Assert.Equal(21, clippedAnchor.Y);
        Assert.False(NativeScenePlacement.ResolveResidentBounds(horizontalOffset - width, 100,
            alignmentWidth, width, 80).IntersectsViewport(200, 150));
        Assert.True(NativeScenePlacement.ResolveResidentBounds(horizontalOffset - width + 1, 100,
            alignmentWidth, width, 80).IntersectsViewport(200, 150));
        Assert.True(NativeScenePlacement.ResolveResidentBounds(100, 150, alignmentWidth, width, 80)
            .IntersectsViewport(200, 150));
        Assert.False(NativeScenePlacement.ResolveResidentBounds(100, -1, alignmentWidth, width, 80)
            .IntersectsViewport(200, 150));
    }
    [Theory]
    [InlineData(96, 96, 352, 220)] // Griswold and the other full-height human frames.
    [InlineData(96, 64, 352, 252)] // Wirt's shorter raster keeps the same horizontal anchor.
    [InlineData(128, 128, 336, 188)] // All three cows, regardless of animation direction.
    public void ResidentBoundsMatchNativeDrawingFromTheTerrainLeftEdge(int width, int height, int expectedX, int expectedY)
    {
        var view = NativeSceneViewport.Create(800, 600, 112, 112, 62, 63);
        var anchor = view.CellBottomAnchor(62, 63); // Native Griswold tile from towners.tsv.
        Assert.Equal((400d, 315d), anchor);
        // DrawMonsterHelper starts at the left floor microtile (368,315).
        // Towner::getRenderingOffset subtracts 16 for a human or 32 for a cow;
        // ClxDraw's last row is inclusive (dun_tile.hpp CalculateSpriteTileCenterX).
        var actual = NativeScenePlacement.ResolveResidentBounds(anchor.X, anchor.Y, width, width, height);
        Assert.Equal(expectedX, actual.X);
        Assert.Equal(expectedY, actual.Y);
        Assert.Equal(315, actual.Y + actual.Height - 1);
        var player = NativePlayerPresentation.CalculateDestinationRect(anchor.X, anchor.Y, width, height);
        Assert.Equal(player.X, actual.X);
        Assert.Equal(player.Y, actual.Y);
    }
}
