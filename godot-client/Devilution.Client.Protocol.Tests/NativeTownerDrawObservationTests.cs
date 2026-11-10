using Devilution.Client;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerDrawObservationTests
{
    [Theory]
    [InlineData(false, 352f, 220f)] [InlineData(true, 336f, 188f)]
    public void LiveGuardAcceptsCorrectedHumanAndCowBounds(bool cow, float x, float y)
    {
        var (draw, row, sprite) = Fixture(cow, x, y);
        Assert.True(draw.MatchesNativeDraw(row, sprite));
    }

    [Theory]
    [InlineData(false, 384f, 220f)] [InlineData(true, 368f, 188f)]
    public void LiveGuardRejectsTheFormerThirtyTwoPixelDisplacement(bool cow, float x, float y)
    {
        var (draw, row, sprite) = Fixture(cow, x, y);
        Assert.False(draw.MatchesNativeDraw(row, sprite));
    }

    [Fact]
    public void LiveGuardRetainsItsIdentityDirectionFrameAndBottomChecks()
    {
        var (draw, row, sprite) = Fixture(false, 352, 220);
        Assert.False((draw with { TownerId = 9 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { PositionX = 61 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { PositionY = 62 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { NativeDirection = 0 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { DirectionIndex = 1 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { FrameIndex = 1 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { AlignmentWidth = 128 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { DrawY = 221 }).MatchesNativeDraw(row, sprite));
        Assert.False((draw with { AnchorInsideViewport = false }).MatchesNativeDraw(row, sprite));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LiveGuardUsesTheSameFloatCoordinatesAsTheActualBlit(bool cow)
    {
        var (draw, row, sprite) = Fixture(cow, 0, 0);
        const float x = 30.123456f, y = 70.234567f;
        var frame = sprite.ResolveFrame(0, row.NativeDirection);
        var expected = NativeScenePlacement.ResolveResidentBounds(x, y, sprite.AlignmentWidth, frame.Width, frame.Height);
        draw = draw with { AnchorX = x, AnchorY = y, DrawX = (float)expected.X, DrawY = (float)expected.Y };
        Assert.True(draw.MatchesNativeDraw(row, sprite));
    }

    private static (NativeTownerDrawObservation, NativeTownResidentSnapshot, NativeTownerSprite) Fixture(bool cow, float x, float y)
    {
        var width = cow ? 128 : 96;
        var directions = cow ? 8 : 1;
        var sprite = new NativeTownerSprite(0, width, directions, 1, 1, 3, [],
            Enumerable.Range(0, directions).Select(direction => new NativeTownerFrame(0, 0, direction * width,
                width, width, direction, 0)).ToArray());
        var row = new NativeTownResidentSnapshot { ResidentSlot = cow ? 8 : 0, TownerId = cow ? 9 : 0,
            PositionX = 62, PositionY = 63, NativeDirection = 1 };
        return (new(row.ResidentSlot, row.TownerId, 62, 63, 1, true, false, "test", "atlas.png", cow ? 1 : 0,
            0, width, 400, 315, x, y, width, width, 0, null, true), row, sprite);
    }
}
