using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class CathedralDoorObjectSelectorTests
{
    [Fact]
    public void SelectsTypedDoorEvenWhenEffectIsNone()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 1 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 2,
                    LevelId = 1,
                    PositionX = 3,
                    PositionY = 4,
                    NativeObjectId = 1,
                    KeepVisibleAfterActivation = true,
                    AnimationFrame = 3,
                    MissilePassable = true,
                    CathedralDoor = new CathedralDoorSnapshot {
                        EntityId = 2,
                        LevelId = 1,
                        PositionX = 3,
                        PositionY = 4,
                        NativeObjectId = 1,
                        DoorState = 1,
                        CachedPiece = 43,
                        CachedNeighborPiece = 49,
                        AnimationFrame = 3,
                        SelectionRegion = 2,
                        PreDraw = true,
                        MissilePassable = true,
                    },
                },
            },
        };

        Assert.Equal(2U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
        Assert.True(AuthoritativeObjectSelector.HasAuthoritativeInteraction(snapshot.Objects[0]));
    }

    [Fact]
    public void RejectsTypedDoorWithMismatchedIdentityOrFlags()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 1 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 2,
                    LevelId = 1,
                    PositionX = 3,
                    PositionY = 4,
                    NativeObjectId = 1,
                    KeepVisibleAfterActivation = true,
                    AnimationFrame = 3,
                    MissilePassable = true,
                    CathedralDoor = new CathedralDoorSnapshot {
                        EntityId = 99,
                        LevelId = 1,
                        PositionX = 3,
                        PositionY = 4,
                        NativeObjectId = 1,
                        DoorState = 1,
                        CachedPiece = 43,
                        CachedNeighborPiece = 49,
                        AnimationFrame = 3,
                        SelectionRegion = 2,
                        PreDraw = true,
                        MissilePassable = true,
                    },
                },
            },
        };

        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
        snapshot.Objects[0].CathedralDoor!.EntityId = 2;
        snapshot.Objects[0].CathedralDoor!.SelectionRegion = 3;
        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
    }
}
