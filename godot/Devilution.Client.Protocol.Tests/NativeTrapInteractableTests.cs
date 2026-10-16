using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTrapInteractableTests
{
    [Fact]
    public void ExplicitNativeTrapCapabilitySelectsArmedDoorAndChest()
    {
        var door = NativeDoorTrap();
        var chest = NativeChestTrap();
        var snapshot = Snapshot(door, chest);

        Assert.Equal(10U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
        Assert.Equal(11U, AuthoritativeObjectSelector.SelectAt(snapshot, 4, 4)?.EntityId);
        Assert.True(AuthoritativeObjectSelector.IsValidNativeTrapInteraction(door));
        Assert.True(AuthoritativeObjectSelector.IsValidNativeTrapInteraction(chest));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(7)]
    public void SupportsTheOtherOwnedNativeTriggerTypes(int nativeObjectId)
    {
        var trap = nativeObjectId == 2
            ? NativeDoorTrap(2)
            : NativeChestTrap(checked((uint)nativeObjectId));

        Assert.True(AuthoritativeObjectSelector.IsValidNativeTrapInteraction(trap));
        Assert.Equal(trap.EntityId, AuthoritativeObjectSelector.SelectAt(
            Snapshot(trap), trap.PositionX, trap.PositionY)?.EntityId);
    }

    [Fact]
    public void DisarmClearsTransientCapabilityAndRevealsOrdinaryOperationCapability()
    {
        var door = NativeDoorTrap();
        Assert.Equal(10U, AuthoritativeObjectSelector.SelectAt(Snapshot(door), 3, 4)?.EntityId);

        door.NativeTrapInteractable = false;
        door.TrapArmed = false;
        Assert.Equal(10U, AuthoritativeObjectSelector.SelectAt(Snapshot(door), 3, 4)?.EntityId);

        var chest = NativeChestTrap();
        chest.NativeTrapInteractable = false;
        chest.TrapArmed = false;
        chest.PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Unspecified;
        chest.NativeChestInteractable = true;
        Assert.Equal(11U, AuthoritativeObjectSelector.SelectAt(Snapshot(chest), 4, 4)?.EntityId);
    }

    [Fact]
    public void ClientModelAcceptsExplicitNativeTrapShapeAndLegacyArmedTrapDefault()
    {
        var native = Apply(NativeDoorTrap());
        Assert.True(Assert.Single(native.Objects).NativeTrapInteractable);

        var legacy = Apply(new ObjectSnapshot {
            EntityId = 12,
            ObjectId = 1,
            LevelId = 1,
            PositionX = 3,
            PositionY = 4,
            TrapArmed = true,
        });
        Assert.False(Assert.Single(legacy.Objects).NativeTrapInteractable);
        Assert.Equal(12U, AuthoritativeObjectSelector.SelectAt(legacy, 3, 4)?.EntityId);
    }

    [Fact]
    public void ClientModelRejectsForgedOrInconsistentNativeTrapCapability()
    {
        var malformed = new Action<ObjectSnapshot>[] {
            value => value.NativeObjectId = 4,
            value => value.NativeObjectId = 8,
            value => value.NativeObjectId = 54,
            value => value.TrapArmed = false,
            value => value.Activated = true,
            value => value.Destroyed = true,
            value => value.NativeChestInteractable = true,
            value => value.NativeSarcophagusInteractable = true,
            value => value.NativeBarrelMeleeBreakable = true,
            value => value.CathedralDoor!.EntityId = 99,
            value => value.CathedralDoor!.SelectionRegion = 2,
        };

        foreach (var corrupt in malformed) {
            var value = NativeDoorTrap();
            corrupt(value);
            Assert.Throws<InvalidDataException>(() => Apply(value));
        }

        var chestWithDoor = NativeChestTrap();
        chestWithDoor.CathedralDoor = new CathedralDoorSnapshot { EntityId = chestWithDoor.EntityId };
        Assert.Throws<InvalidDataException>(() => Apply(chestWithDoor));
    }

    [Fact]
    public void NativeTrapCapabilitySurvivesProtocolRoundTripAndDefaultsFalse()
    {
        var source = new Snapshot {
            Objects = {
                NativeDoorTrap(),
                new ObjectSnapshot { EntityId = 12, ObjectId = 1, LevelId = 1 },
            },
        };

        var restored = Devilution.Protocol.V1.Snapshot.Parser.ParseFrom(source.ToByteArray());

        Assert.True(restored.Objects[0].NativeTrapInteractable);
        Assert.False(restored.Objects[1].NativeTrapInteractable);
    }

    private static Snapshot Apply(ObjectSnapshot value)
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = { new PlayerSnapshot {
                EntityId = 7,
                LevelId = 1,
                PositionX = 3,
                PositionY = 3,
                VisitedLevelIds = { 0, 1 },
            } },
            Objects = { value },
        }));
        return model.Snapshot!;
    }

    private static Snapshot Snapshot(params ObjectSnapshot[] objects)
        => new() {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1, PositionX = 3, PositionY = 3 } },
            Objects = { objects },
        };

    private static ObjectSnapshot NativeDoorTrap(uint nativeObjectId = 1)
        => new() {
            EntityId = 10,
            ObjectId = nativeObjectId,
            LevelId = 1,
            PositionX = 3,
            PositionY = 4,
            NativeObjectId = nativeObjectId,
            TrapArmed = true,
            NativeTrapInteractable = true,
            KeepVisibleAfterActivation = true,
            AnimationFrame = nativeObjectId == 1 ? 1U : 2U,
            CathedralDoor = new CathedralDoorSnapshot {
                EntityId = 10,
                LevelId = 1,
                PositionX = 3,
                PositionY = 4,
                NativeObjectId = nativeObjectId,
                DoorState = 0,
                CachedPiece = 43,
                CachedNeighborPiece = 49,
                AnimationFrame = nativeObjectId == 1 ? 1U : 2U,
                SelectionRegion = 3,
            },
        };

    private static ObjectSnapshot NativeChestTrap(uint nativeObjectId = 5)
        => new() {
            EntityId = 11,
            ObjectId = nativeObjectId,
            LevelId = 1,
            PositionX = 4,
            PositionY = 4,
            NativeObjectId = nativeObjectId,
            TrapArmed = true,
            NativeTrapInteractable = true,
            PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Trap,
        };
}
