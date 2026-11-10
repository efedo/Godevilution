using Devilution.Client.PresentationAssets;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerOpacityMaskTests
{
    [Theory]
    [InlineData(96, 96)] [InlineData(128, 128)]
    public void PickingUsesTheDrawnPixelsInsteadOfTheFormerShiftedBoxOrTransparentPadding(int width, int height)
    {
        var rgba = new byte[256 * 256 * 4];
        var frame = new NativeTownerFrame(0, 3, 2, width, height, 0, 0);
        var localX = width / 2; const int localY = 40;
        rgba[((frame.Y + localY) * 256 + frame.X + localX) * 4 + 3] = 255; // Black can still be opaque.
        var mask = new NativeTownerOpacityMask(256, 256, rgba);
        var view = NativeSceneViewport.Create(800, 600, 112, 112, 62, 63);
        var anchor = view.CellBottomAnchor(62, 63);
        var drawX = width == 96 ? 352 : 336;
        var drawY = height == 96 ? 220 : 188;
        var pointerX = drawX + localX + 0.25;
        var pointerY = drawY + localY + 0.25;
        Assert.True(mask.HitTest(frame, width, anchor.X, anchor.Y, pointerX, pointerY));
        Assert.NotEqual((62, 63), view.ScreenToTile(pointerX, pointerY)); // A body's screen pixel is not its occupied floor cell.
        Assert.False(mask.HitTest(frame, width, anchor.X, anchor.Y, pointerX + 32, pointerY));
        Assert.False(mask.HitTest(frame, width, anchor.X, anchor.Y, drawX + 1, drawY + 1));
        Assert.False(mask.HitTest(frame, width, anchor.X, anchor.Y, drawX + width, pointerY));
        Assert.False(mask.HitTest(frame, width, anchor.X, anchor.Y, pointerX, drawY + height));
        Assert.False(mask.HitTest(frame, width, anchor.X, anchor.Y, double.NaN, pointerY));
    }

    [Fact]
    public void PickingFollowsFractionalCameraMotionAndTheCurrentFrameRegion()
    {
        var rgba = new byte[256 * 256 * 4];
        var frame = new NativeTownerFrame(0, 3, 2, 96, 96, 0, 0);
        rgba[((frame.Y + 40) * 256 + frame.X + 48) * 4 + 3] = 255;
        var mask = new NativeTownerOpacityMask(256, 256, rgba);
        for (var step = 0; step <= 24; step++) {
            var view = NativeSceneViewport.Create(800, 600, 112, 112, 62 + step / 24d, 63);
            var anchor = view.CellBottomAnchor(62, 63);
            var bounds = NativeScenePlacement.ResolveResidentBounds((float)anchor.X, (float)anchor.Y, 96, 96, 96);
            var x = (float)bounds.X + 48.25; var y = (float)bounds.Y + 40.25;
            Assert.True(mask.HitTest(frame, 96, (float)anchor.X, (float)anchor.Y, x, y));
            Assert.False(mask.HitTest(frame with { X = 110 }, 96, (float)anchor.X, (float)anchor.Y, x, y));
        }
    }

    [Fact]
    public void MaskRejectsMalformedRgbaAndCopiesOpacityOnce()
    {
        Assert.Throws<ArgumentException>(() => new NativeTownerOpacityMask(0, 1, []));
        Assert.Throws<ArgumentException>(() => new NativeTownerOpacityMask(int.MaxValue, int.MaxValue, []));
        Assert.Throws<ArgumentException>(() => new NativeTownerOpacityMask(1, 1, [0, 0, 0]));
        var rgba = new byte[] { 0, 0, 0, 255 };
        var mask = new NativeTownerOpacityMask(1, 1, rgba);
        rgba[3] = 0;
        var frame = new NativeTownerFrame(0, 0, 0, 1, 1, 0, 0);
        Assert.True(mask.HitTest(frame, 96, 48, 0, 0, 0));
        Assert.False(mask.HitTest(frame with { X = 1 }, 96, 48, 0, 0, 0));
    }
}
