using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeCorpsePresentationVisibilityTests
{
    [Fact]
    public void ShowsCorpseOnItsActiveLevel()
    {
        Assert.True(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 3 },
            3));
    }

    [Fact]
    public void PreservesNativeLevelZeroWildcard()
    {
        Assert.True(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 0 },
            3));
    }

    [Fact]
    public void HidesCorpseFromOtherLevels()
    {
        Assert.False(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 2 },
            3));
    }

    [Fact]
    public void GeometryBackedVisibilityHidesCorpseBehindProjectedVisionBlockers()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 3, Width = 5, Height = 1 };
        geometry.NativeVisionBlockedCells.Add(1);

        Assert.False(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 3, PositionX = 4 },
            3,
            geometry,
            0,
            0,
            10));
    }
}
