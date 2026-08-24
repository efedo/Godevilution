using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeEntityPositionProjectionTests
{
    [Theory]
    [InlineData(0u, 3u, true)]
    [InlineData(3u, 3u, true)]
    [InlineData(2u, 3u, false)]
    public void ExposesOnlyActiveOrWildcardEntityLevels(
        uint entityLevelId,
        uint currentLevelId,
        bool expected)
    {
        Assert.Equal(
            expected,
            AuthoritativeEntityPositionProjection.IsVisibleOnActiveLevel(
                entityLevelId,
                currentLevelId));
    }

    [Fact]
    public void KeepsInterpolationOnTheSameLevel()
    {
        Assert.False(AuthoritativeEntityPositionProjection.RequiresLevelReset(2, 2));
    }

    [Fact]
    public void ResetsWhenAnExplicitLevelChanges()
    {
        Assert.True(AuthoritativeEntityPositionProjection.RequiresLevelReset(2, 3));
    }

    [Fact]
    public void ResetsWhenWildcardLevelIdentityChanges()
    {
        Assert.True(AuthoritativeEntityPositionProjection.RequiresLevelReset(0, 2));
    }
}
