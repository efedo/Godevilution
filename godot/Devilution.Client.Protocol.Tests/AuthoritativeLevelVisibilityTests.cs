using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeLevelVisibilityTests
{
    [Theory]
    [InlineData(0u, 4u, true)]
    [InlineData(4u, 4u, true)]
    [InlineData(3u, 4u, false)]
    public void AppliesNativeLevelVisibilityRule(
        uint projectedLevelId,
        uint currentLevelId,
        bool expected)
    {
        Assert.Equal(
            expected,
            AuthoritativeLevelVisibility.IsVisible(projectedLevelId, currentLevelId));
    }
}
