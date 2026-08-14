using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeTargetSelectorTests
{
    [Fact]
    public void PrefersALivingMonsterAndExcludesHiddenOrDeadParticipants()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 1, PositionY = 1 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 4, PositionY = 4, Life = 20 },
                new PlayerSnapshot { EntityId = 9, LevelId = 3, PositionX = 4, PositionY = 4, Life = 20 },
                new PlayerSnapshot { EntityId = 10, LevelId = 2, PositionX = 5, PositionY = 5, Life = 0 },
                new PlayerSnapshot { EntityId = 11, LevelId = 3, PositionX = 6, PositionY = 6, Life = 20 },
            },
            Monsters = {
                new MonsterSnapshot { EntityId = 20, LevelId = 2, PositionX = 4, PositionY = 4, Alive = true },
                new MonsterSnapshot { EntityId = 21, LevelId = 2, PositionX = 5, PositionY = 5, Alive = false },
                new MonsterSnapshot { EntityId = 22, LevelId = 2, PositionX = 7, PositionY = 7, Alive = true, SneakHidden = true },
            },
        };

        Assert.Equal(20U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 4, 4));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 5, 5));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 6, 6));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 7, 7));
    }

    [Fact]
    public void SelectsLivingRemotePlayerOnTheLocalLevel()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 3, PositionY = 4, Life = 10 },
            },
        };

        Assert.Equal(8U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 4));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 5));
    }
}
