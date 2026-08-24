using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeObjectPresentationVisibilityTests
{
    [Fact]
    public void ExcludesActivatedDestroyedAndOffLevelObjects()
    {
        Assert.False(AuthoritativeObjectPresentationVisibility.IsVisible(
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, LevelId = 2, Activated = true },
            2));
        Assert.False(AuthoritativeObjectPresentationVisibility.IsVisible(
            new ObjectSnapshot { EntityId = 2, ObjectId = 1, LevelId = 2, Breakable = true, Destroyed = true },
            2));
        Assert.False(AuthoritativeObjectPresentationVisibility.IsVisible(
            new ObjectSnapshot { EntityId = 3, ObjectId = 1, LevelId = 3 },
            2));
    }

    [Fact]
    public void AcceptsActiveObjectsAndLevelWildcard()
    {
        Assert.True(AuthoritativeObjectPresentationVisibility.IsVisible(
            new ObjectSnapshot { EntityId = 4, ObjectId = 1, LevelId = 2 },
            2));
        Assert.True(AuthoritativeObjectPresentationVisibility.IsVisible(
            new ObjectSnapshot { EntityId = 5, ObjectId = 1, LevelId = 0 },
            2));
    }
}
