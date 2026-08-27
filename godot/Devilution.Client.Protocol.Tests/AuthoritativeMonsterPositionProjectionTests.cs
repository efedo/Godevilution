using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeMonsterPositionProjectionTests
{
    [Fact]
    public void UsesTheAuthoritativeFutureTileWhileMoving()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
        };

        Assert.Equal(
            new ClientPresentationPosition(5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster));
    }

    [Fact]
    public void UsesTheOccupiedTileWhenStationary()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = false,
        };

        Assert.Equal(
            new ClientPresentationPosition(4, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster));
    }
}
