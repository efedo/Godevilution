using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class MonsterHealthBarProjectionTests
{
    [Fact]
    public void UsesExactQ6ValuesWhenAvailable()
    {
        var projection = MonsterHealthBarProjection.Resolve(new MonsterSnapshot {
            Alive = true,
            HitPoints = 1,
            MaxHitPoints = 1,
            HitPointsQ6 = 96,
            MaxHitPointsQ6 = 256,
        });

        Assert.True(projection.Visible);
        Assert.Equal(0.375f, projection.Ratio);
    }

    [Fact]
    public void FallsBackToRoundedValuesForLegacySnapshots()
    {
        var projection = MonsterHealthBarProjection.Resolve(new MonsterSnapshot {
            Alive = true,
            HitPoints = 3,
            MaxHitPoints = 4,
        });

        Assert.True(projection.Visible);
        Assert.Equal(0.75f, projection.Ratio);
    }

    [Theory]
    [InlineData(false, 100, 100, 0f, false)]
    [InlineData(true, 0, 100, 0f, true)]
    [InlineData(true, 150, 100, 1f, true)]
    public void ClampsAndHidesInvalidBars(
        bool alive,
        int hitPoints,
        int maxHitPoints,
        float expectedRatio,
        bool expectedVisible)
    {
        var projection = MonsterHealthBarProjection.Resolve(new MonsterSnapshot {
            Alive = alive,
            HitPoints = hitPoints,
            MaxHitPoints = maxHitPoints,
        });

        Assert.Equal(expectedVisible, projection.Visible);
        Assert.Equal(expectedRatio, projection.Ratio);
    }
}
