using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeCorpsePresentationVisibilityTests
{
    [Fact]
    public void ShowsCorpseOnItsActiveLevel()
    {
        Assert.True(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 3 },
            3));
    }

    [Fact]
    public void PreservesNativeLevelZeroWildcard()
    {
        Assert.True(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 0 },
            3));
    }

    [Fact]
    public void HidesCorpseFromOtherLevels()
    {
        Assert.False(AuthoritativeCorpsePresentationVisibility.IsVisible(
            new CorpseSnapshot { EntityId = 42, LevelId = 2 },
            3));
    }
}
