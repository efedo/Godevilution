using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePlayerSettlementTests
{
    [Fact]
    public void StandWithUnstartedNativeWalkIsNotSettled()
    {
        var player = new PlayerSnapshot {
            CombatMode = PlayerCombatMode.Stand,
            NativeWalkPathPending = true,
        };

        Assert.False(AuthoritativePlayerSettlement.IsSettled(player, pendingCommandCount: 0));
    }

    [Fact]
    public void ActiveWalkAnimationIsNotSettledWithoutAnotherQueuedPath()
    {
        var player = new PlayerSnapshot {
            CombatMode = PlayerCombatMode.WalkSideways,
            CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Move },
        };

        Assert.False(AuthoritativePlayerSettlement.IsSettled(player, pendingCommandCount: 0));
    }

    [Fact]
    public void NonStandModeWithoutAnActionIsNotSettled()
    {
        var player = new PlayerSnapshot { CombatMode = PlayerCombatMode.Attack };

        Assert.False(AuthoritativePlayerSettlement.IsSettled(player, pendingCommandCount: 0));
    }

    [Fact]
    public void OutstandingClientCommandIsNotSettled()
    {
        var player = new PlayerSnapshot { CombatMode = PlayerCombatMode.Stand };

        Assert.False(AuthoritativePlayerSettlement.IsSettled(player, pendingCommandCount: 1));
    }

    [Fact]
    public void LegacyStandSnapshotWithoutPathFlagRemainsSettled()
    {
        var player = new PlayerSnapshot { CombatMode = PlayerCombatMode.Stand };

        Assert.True(AuthoritativePlayerSettlement.IsSettled(player, pendingCommandCount: 0));
        Assert.False(player.NativeWalkPathPending);
    }
}