using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectApproachTests
{
    [Fact]
    public void DiagonalChestDropUsesNativePickupRangeWithoutAnotherWalkCommand()
    {
        var snapshot = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, LevelId = 5, PositionX = 66, PositionY = 23 } } };
        var geometry = new LevelGeometrySnapshot { LevelId = 5, Width = 112, Height = 112 };
        var drop = new ObjectSnapshot { EntityId = 20, LevelId = 5, PositionX = 65, PositionY = 24 };
        Assert.Empty(NativeObjectApproachPath.Find(snapshot, geometry, drop, pickup: true)!);
        Assert.Equal((66, 24), Assert.Single(NativeObjectApproachPath.Find(snapshot, geometry, drop)!));
        // Gold and open chests do not make pickup require standing on their tile.
        geometry.StaticObjectBlockedCells.Add(24 * 112 + 65);
        Assert.Empty(NativeObjectApproachPath.Find(snapshot, geometry, drop, pickup: true)!);
    }

    [Fact]
    public void PickupCanApproachAnAccessibleDiagonalEvenWhenEveryCardinalCellIsBlocked()
    {
        var snapshot = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, LevelId = 5, PositionX = 1, PositionY = 1 } } };
        var geometry = new LevelGeometrySnapshot { LevelId = 5, Width = 7, Height = 7,
            StaticObjectBlockedCells = { 2 * 7 + 3, 4 * 7 + 3, 3 * 7 + 2, 3 * 7 + 4 } };
        var drop = new ObjectSnapshot { LevelId = 5, PositionX = 3, PositionY = 3 };
        Assert.Null(NativeObjectApproachPath.Find(snapshot, geometry, drop));
        var path = NativeObjectApproachPath.Find(snapshot, geometry, drop, pickup: true);
        Assert.NotNull(path);
        Assert.Equal((2, 2), path![^1]);
    }

    [Fact]
    public void ABlockedDropDoesNotAcquireAPathByIgnoringWallsOrLiveActors()
    {
        var snapshot = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, LevelId = 5, PositionX = 1, PositionY = 1 } } };
        var geometry = new LevelGeometrySnapshot { LevelId = 5, Width = 5, Height = 5,
            BlockedCells = { 1, 5, 11 }, StaticObjectBlockedCells = { 7 } };
        var drop = new ObjectSnapshot { LevelId = 5, PositionX = 3, PositionY = 3 };
        Assert.Null(NativeObjectApproachPath.Find(snapshot, geometry, drop, pickup: true));
    }

    [Fact]
    public void WarriorObservesActualDoorTrapMissileWithoutSeeingItsArmedFlag()
    {
        var snapshot = DoorShot();
        Assert.False(snapshot.Objects[0].TrapArmed);
        Assert.False(snapshot.Objects[0].NativeTrapInteractable);
        var firing = Assert.Single(NativeCatacombsDoorTrapObservation.Find(snapshot));
        Assert.Equal(new NativeCatacombsDoorTrapObservation.Firing(7, 20, 21, 300), firing);
        snapshot.Projectiles.Clear();
        Assert.Empty(NativeCatacombsDoorTrapObservation.Find(snapshot));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OpeningAnUntrappedDoorOrAnUnrelatedMissileDoesNotProveDoorTrapFiring(int corruption)
    {
        var snapshot = DoorShot();
        switch (corruption) {
        case 0: snapshot.Objects[0].MissilePassable = false; break;
        case 1: snapshot.Objects[1].NativeObjectId = 57; break;
        case 2: snapshot.Projectiles[0].SpellId = 1; break;
        case 3: snapshot.Projectiles[0].TargetEntityId = 999; break;
        case 4: snapshot.Objects[1].LevelId = 6; break;
        }
        Assert.Empty(NativeCatacombsDoorTrapObservation.Find(snapshot));
    }

    [Theory]
    [InlineData(53, 55, 52, 56)]
    [InlineData(54, 57, 55, 56)]
    public void ANewlyReservedMonsterCellBlocksReplanningEvenBeforeTheMonsterArrives(int x, int y, int oldX, int oldY)
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = x - 1, PositionY = y } },
            Monsters = { new MonsterSnapshot { EntityId = 2, LevelId = 6, Alive = true,
                PositionX = oldX, PositionY = oldY, Moving = true, FuturePositionX = x, FuturePositionY = y } },
        };
        Assert.True(NativeObjectApproachPath.IsActorOccupied(snapshot, 6, x, y));
        var target = new ObjectSnapshot { LevelId = 6, PositionX = x + 3, PositionY = y };
        var path = NativeObjectApproachPath.Find(snapshot,
            new LevelGeometrySnapshot { LevelId = 6, Width = 112, Height = 112 }, target);
        Assert.NotNull(path);
        Assert.DoesNotContain((x, y), path!);
        snapshot.Monsters[0].Alive = false;
        Assert.False(NativeObjectApproachPath.IsActorOccupied(snapshot, 6, x, y));
    }

    [Fact]
    public void NormalApproachDoesNotWalkThroughAClosedDoorAndSeededRouteSelectsOnlyItsFirstNeededDoor()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 7, PositionX = 1, PositionY = 2 } },
            Objects = {
                new ObjectSnapshot { EntityId = 10, LevelId = 7, NativeObjectId = 42, PositionX = 4, PositionY = 2 },
                new ObjectSnapshot { EntityId = 11, LevelId = 7, NativeObjectId = 43, PositionX = 1, PositionY = 3 },
                new ObjectSnapshot { EntityId = 12, LevelId = 7, NativeObjectId = 43, PositionX = 7, PositionY = 2 },
            },
        };
        var geometry = new LevelGeometrySnapshot { LevelId = 7, Width = 9, Height = 6 };
        for (var y = 0; y < geometry.Height; y++)
            for (var x = 0; x < geometry.Width; x++)
                if (y != 2) geometry.BlockedCells.Add(y * geometry.Width + x);
        var target = snapshot.Objects[2];
        Assert.Null(NativeObjectApproachPath.Find(snapshot, geometry, target));
        var route = NativeObjectApproachPath.Find(snapshot, geometry, target, allowClosedDoors: true, ignoreActors: true);
        Assert.NotNull(route);
        Assert.Equal(10U, NativeObjectApproachPath.FirstClosedDoorOnRoute(snapshot, 7, route!)!.EntityId);
        snapshot.Objects[0].MissilePassable = true;
        Assert.Null(NativeObjectApproachPath.FirstClosedDoorOnRoute(snapshot, 7, route!));
        Assert.NotNull(NativeObjectApproachPath.Find(snapshot, geometry, target));
    }

    private static Snapshot DoorShot()
        => new() { Tick = 300,
            Objects = {
                new ObjectSnapshot { EntityId = 20, LevelId = 7, NativeObjectId = 42, MissilePassable = true },
                new ObjectSnapshot { EntityId = 21, LevelId = 7, NativeObjectId = 53 },
            },
            Projectiles = { new ProjectileSnapshot { EntityId = 30, LevelId = 7, SourceEntityId = 21, TargetEntityId = 20 } },
        };
}
