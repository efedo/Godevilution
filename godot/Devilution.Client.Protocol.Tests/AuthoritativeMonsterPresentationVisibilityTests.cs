using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeMonsterPresentationVisibilityTests
{
    [Fact]
    public void ExcludesDeadHiddenAndOffLevelMonsters()
    {
        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 1, LevelId = 2, Alive = false },
            2));
        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 2, LevelId = 2, Alive = true, SneakHidden = true },
            2));
        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 3, LevelId = 3, Alive = true },
            2));
    }

    [Fact]
    public void AcceptsLivingVisibleMonstersAndLevelWildcard()
    {
        Assert.True(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 4, LevelId = 2, Alive = true },
            2));
        Assert.True(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 5, LevelId = 0, Alive = true },
            2));
    }
}
