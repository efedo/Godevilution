using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeEntityPositionProjectionTests
{
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
