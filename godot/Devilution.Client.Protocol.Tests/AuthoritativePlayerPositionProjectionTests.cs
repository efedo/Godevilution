using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePlayerPositionProjectionTests
{
    [Fact]
    public void UsesTheOccupiedTileWhenNoWalkIsInProgress()
    {
        var player = new PlayerSnapshot { PositionX = 4, PositionY = 5 };

        Assert.Equal(
            new ClientPosition(4, 5),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void UsesTheReservedDestinationDuringAnAuthoritativeWalk()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CombatMode = PlayerCombatMode.WalkSideways,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                DirectionX = -1,
                DirectionY = 1,
            },
        };

        Assert.Equal(
            new ClientPosition(3, 6),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void DoesNotApplyAttackDirectionFieldsToPresentationPosition()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CombatMode = PlayerCombatMode.Attack,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Attack,
                DirectionX = 1,
                DirectionY = 1,
            },
        };

        Assert.Equal(
            new ClientPosition(4, 5),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }
}
