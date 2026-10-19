using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PlayerResourceBarProjectionTests
{
    [Fact]
    public void ProjectsAuthoritativeLifeAndManaRatios()
    {
        var projection = PlayerResourceBarProjection.Resolve(new PlayerSnapshot {
            Life = 30,
            LifeMaximum = 40,
            Mana = 12,
            ManaMaximum = 24,
        });

        Assert.True(projection.LifeVisible);
        Assert.Equal(0.75f, projection.LifeRatio);
        Assert.True(projection.ManaVisible);
        Assert.Equal(0.5f, projection.ManaRatio);
    }

    [Theory]
    [InlineData(0, 40, 12, 24, false, false)]
    [InlineData(30, 0, 12, 24, false, true)]
    [InlineData(30, 40, 12, 0, true, false)]
    public void HidesBarsWhenTheAuthoritativeStateCannotDisplayThem(
        int life,
        int lifeMaximum,
        int mana,
        int manaMaximum,
        bool expectedLifeVisible,
        bool expectedManaVisible)
    {
        var projection = PlayerResourceBarProjection.Resolve(new PlayerSnapshot {
            Life = life,
            LifeMaximum = lifeMaximum,
            Mana = mana,
            ManaMaximum = manaMaximum,
        });

        Assert.Equal(expectedLifeVisible, projection.LifeVisible);
        Assert.Equal(expectedManaVisible, projection.ManaVisible);
    }

    [Fact]
    public void ClampsAuthoritativeValuesToBarBounds()
    {
        var projection = PlayerResourceBarProjection.Resolve(new PlayerSnapshot {
            Life = 60,
            LifeMaximum = 40,
            Mana = -2,
            ManaMaximum = 20,
        });

        Assert.Equal(1f, projection.LifeRatio);
        Assert.Equal(0f, projection.ManaRatio);
    }
}
