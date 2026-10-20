using Devilution.Client.PresentationAssets;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerPlacementTests
{
    [Theory]
    [InlineData(96, 96)]
    [InlineData(128, 128)]
    public void ClippedAnchorRetainsVisibleSpriteAndCountsOnlyIntersectingRasters(int alignmentWidth, int width)
    {
        var horizontalOffset = (alignmentWidth - 64) / 2;
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
}
