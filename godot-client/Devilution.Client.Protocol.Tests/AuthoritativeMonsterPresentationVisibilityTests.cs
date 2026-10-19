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

    [Fact]
    public void GeometryBackedVisibilityUsesNativeVisionRadius()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 25, Height = 5 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 125));

        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 6, LevelId = 2, Alive = true, PositionX = 12, PositionY = 2 },
            2,
            geometry,
            0,
            2));
        Assert.True(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 7, LevelId = 2, Alive = true, PositionX = 8, PositionY = 2 },
            2,
            geometry,
            0,
            2));
    }

    [Fact]
    public void GeometryBackedVisibilityHonorsDiagonalCornerOcclusion()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 5 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 25));
        geometry.BlockedCells.Add(1);
        geometry.BlockedCells.Add(5);

        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 8, LevelId = 2, Alive = true, PositionX = 2, PositionY = 2 },
            2,
            geometry,
            0,
            0));
    }

    [Fact]
    public void GeometryBackedVisibilityUsesProjectedNativeVisionBlockers()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 5));
        geometry.NativeVisionBlockedCells.Add(1);

        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 9, LevelId = 2, Alive = true, PositionX = 4 },
            2,
            geometry,
            0,
            0));
    }

    [Fact]
    public void GeometryBackedVisibilityUsesVisionBlockersWithoutTransparencyLayer()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 };
        geometry.NativeVisionBlockedCells.Add(1);

        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 12, LevelId = 2, Alive = true, PositionX = 4 },
            2,
            geometry,
            0,
            0));
    }

    [Fact]
    public void GeometryBackedVisibilityUsesProjectedPlayerLightRadius()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 12, Height = 1 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 12));

        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 10, LevelId = 2, Alive = true, PositionX = 11 },
            2,
            geometry,
            0,
            0,
            10));
        Assert.True(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 10, LevelId = 2, Alive = true, PositionX = 11 },
            2,
            geometry,
            0,
            0,
            11));
    }

    [Fact]
    public void GeometryBackedVisibilityUsesTheNativeRayTable()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 2, Width = 16, Height = 16 };
        geometry.NativeTransparency.Add(Enumerable.Repeat<uint>(1, 256));
        geometry.BlockedCells.Add(1);

        Assert.True(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 11, LevelId = 2, Alive = true, PositionX = 8, PositionY = 4 },
            2,
            geometry,
            0,
            0,
            10));

        geometry.BlockedCells.Add(16);
        Assert.False(AuthoritativeMonsterPresentationVisibility.IsVisible(
            new MonsterSnapshot { EntityId = 11, LevelId = 2, Alive = true, PositionX = 8, PositionY = 4 },
            2,
            geometry,
            0,
            0,
            10));
    }
}
