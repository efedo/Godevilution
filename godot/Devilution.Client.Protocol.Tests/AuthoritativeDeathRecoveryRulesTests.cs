using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeDeathRecoveryRulesTests
{
    [Theory]
    [InlineData(0, PlayerCombatMode.Death, false, true)]
    [InlineData(0, PlayerCombatMode.Death, true, false)]
    [InlineData(1, PlayerCombatMode.Death, false, false)]
    [InlineData(-1, PlayerCombatMode.Death, false, false)]
    [InlineData(int.MaxValue, PlayerCombatMode.Death, false, false)]
    [InlineData(0, (PlayerCombatMode)int.MaxValue, false, false)]
    public void RequiresExactlyZeroLifeAndSettledDeath(
        int life,
        PlayerCombatMode mode,
        bool actionPending,
        bool expected)
    {
        var player = new PlayerSnapshot {
            Life = life,
            CombatMode = mode,
            CurrentAction = actionPending ? new PlayerActionSnapshot() : null,
        };
        var before = player.Clone();

        Assert.Equal(expected, AuthoritativeDeathRecoveryRules.CanRequestTownRestart(player));
        Assert.Equal(before, player);
    }

    [Fact]
    public void RejectsEveryNonDeathModeEvenWithZeroLife()
    {
        foreach (var mode in Enum.GetValues<PlayerCombatMode>()) {
            if (mode == PlayerCombatMode.Death)
                continue;
            Assert.False(AuthoritativeDeathRecoveryRules.CanRequestTownRestart(
                new PlayerSnapshot { Life = 0, CombatMode = mode }));
        }
    }

    [Fact]
    public void RejectsMissingAuthoritativePlayer()
        => Assert.False(AuthoritativeDeathRecoveryRules.CanRequestTownRestart(null));
}
