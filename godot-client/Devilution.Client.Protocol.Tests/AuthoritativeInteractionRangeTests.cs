using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeInteractionRangeTests
{
    [Theory]
    [InlineData(10, 20, 10, 20, true)]
    [InlineData(10, 20, 11, 21, true)]
    [InlineData(10, 20, 12, 20, false)]
    public void MatchesAdjacentInteractionRange(
        int playerX,
        int playerY,
        int targetX,
        int targetY,
        bool expected)
    {
        Assert.Equal(
            expected,
            AuthoritativeInteractionRange.IsAdjacent(
                new PlayerSnapshot { PositionX = playerX, PositionY = playerY },
                targetX,
                targetY));
    }

    [Fact]
    public void RejectsExtremeCoordinatesWithoutOverflowing()
    {
        var player = new PlayerSnapshot {
            PositionX = int.MinValue,
            PositionY = int.MaxValue,
        };

        Assert.False(AuthoritativeInteractionRange.IsAdjacent(
            new PlayerSnapshot(), int.MinValue, 0));
        Assert.False(AuthoritativeInteractionRange.IsAdjacent(player, int.MaxValue, int.MinValue));
        Assert.False(AuthoritativeInteractionRange.IsAdjacent(player, int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void RequiresExactPortalEndpointAndHonorsBidirectionalReturn()
    {
        var player = new PlayerSnapshot { LevelId = 2, PositionX = 8, PositionY = 9 };
        var portal = new PortalSnapshot {
            PortalId = 4,
            SourceLevelId = 2,
            SourcePositionX = 8,
            SourcePositionY = 9,
            DestinationLevelId = 5,
            DestinationPositionX = 21,
            DestinationPositionY = 22,
        };

        Assert.True(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
        player.PositionX = 9;
        Assert.False(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
        player.LevelId = 5;
        player.PositionX = 21;
        player.PositionY = 22;
        Assert.False(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
        portal.Bidirectional = true;
        Assert.True(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
    }

    [Fact]
    public void TreatsLevelZeroPortalAsTownLevel()
    {
        var player = new PlayerSnapshot { LevelId = 0, PositionX = 3, PositionY = 4 };
        var portal = new PortalSnapshot {
            SourceLevelId = 0,
            SourcePositionX = 3,
            SourcePositionY = 4,
        };

        Assert.True(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));

        player.LevelId = 9;
        player.PositionX = 5;
        Assert.False(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
    }

    [Fact]
    public void HonorsBidirectionalTownDestination()
    {
        var player = new PlayerSnapshot { LevelId = 0, PositionX = 7, PositionY = 8 };
        var portal = new PortalSnapshot {
            SourceLevelId = 4,
            DestinationLevelId = 0,
            DestinationPositionX = 7,
            DestinationPositionY = 8,
            Bidirectional = true,
        };

        Assert.True(AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal));
    }
}
