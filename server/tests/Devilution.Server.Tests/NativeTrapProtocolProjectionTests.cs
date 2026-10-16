using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeTrapProtocolProjectionTests
{
    [Fact]
    public void TransientTrapCapabilityDoesNotChangeSnapshotValueEquality()
    {
        var source = Snapshot();
        var first = source with {
            NativeTrapInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2, 3]),
        };
        var second = source with {
            NativeTrapInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([3, 2]),
        };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(source, source with {
            NativeTrapInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Empty,
        });
    }

    [Fact]
    public void MapsOnlyExplicitArmedNativeDoorAndChestCapabilities()
    {
        var source = Snapshot() with {
            NativeTrapInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2, 3]),
        };

        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var restored = Devilution.Protocol.V1.Snapshot.Parser.ParseFrom(wire.ToByteArray());

        Assert.Equal(wire, restored);
        Assert.True(restored.Objects.Single(value => value.EntityId == 2).NativeTrapInteractable);
        Assert.True(restored.Objects.Single(value => value.EntityId == 3).NativeTrapInteractable);
        Assert.False(restored.Objects.Single(value => value.EntityId == 4).NativeTrapInteractable);
        Assert.All(restored.Objects, value => {
            Assert.False(value.NativeChestInteractable);
            Assert.False(value.NativeSarcophagusInteractable);
            Assert.False(value.NativeBarrelMeleeBreakable);
        });
        Assert.Empty(DiabloProtocolMapper.FromProtocolSnapshot(restored)
            .NativeTrapInteractableEntityIds);

        var hiddenTraps = DiabloProtocolMapper.ToProtocolSnapshot(source, revealTraps: false);
        Assert.All(hiddenTraps.Objects, value => {
            Assert.False(value.TrapArmed);
            Assert.False(value.NativeTrapInteractable);
        });
    }

    private static DiabloSimulationSnapshot Snapshot()
        => new(
            1,
            new StorePlayerSnapshot(0, null, [], 0, 64, 0, 0,
                PlayerAttributesState.Zero, [], [], []) {
                EntityId = 1,
                LevelId = 1,
            },
            [],
            [],
            [NativeObject(2, 2), NativeObject(3, 5), NativeObject(4, 54)],
            [],
            [],
            null) {
                CathedralDoorObjects = [new DiabloCathedralDoorObjectState(
                    2, 1, 2, 10, 2, 0, 43, 49, 2, 3, false, false, false)],
            };

    private static DiabloObjectState NativeObject(uint entityId, uint nativeObjectId)
        => new(entityId, nativeObjectId, 1, checked((int)entityId), 10, false, 0,
            AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = nativeObjectId,
                TrapArmed = true,
                AnimationFrame = nativeObjectId == 2 ? 2 : 0,
                HideWhenActivated = nativeObjectId != 2,
            };
}
