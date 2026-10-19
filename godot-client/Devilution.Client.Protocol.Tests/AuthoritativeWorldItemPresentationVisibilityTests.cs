using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeWorldItemPresentationVisibilityTests
{
    [Fact]
    public void GeometryBackedVisibilityHidesItemsBehindProjectedVisionBlockers()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 };
        geometry.NativeVisionBlockedCells.Add(1);

        Assert.False(AuthoritativeWorldItemPresentationVisibility.IsVisible(
            new WorldItemSnapshot { EntityId = 7, LevelId = 2, PositionX = 4 },
            2,
            geometry,
            0,
            0,
            10));
    }

    [Fact]
    public void GeometryBackedVisibilityHonorsProjectedPlayerLightRadius()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 12, Height = 1 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 12));

        Assert.False(AuthoritativeWorldItemPresentationVisibility.IsVisible(
            new WorldItemSnapshot { EntityId = 8, LevelId = 2, PositionX = 11 },
            2,
            geometry,
            0,
            0,
            10));
        Assert.True(AuthoritativeWorldItemPresentationVisibility.IsVisible(
            new WorldItemSnapshot { EntityId = 8, LevelId = 2, PositionX = 11 },
            2,
            geometry,
            0,
            0,
            11));
    }
}
