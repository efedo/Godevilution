using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeChestProtocolProjectionTests
{
    [Fact]
    public void TransientCapabilityDoesNotChangeSnapshotValueEquality()
    {
        var source = Snapshot();
        var first = source with {
            NativeChestInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2, 3]),
        };
        var second = source with {
            NativeChestInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([3, 2]),
        };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(source, source with {
            NativeChestInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Empty,
        });
    }

    [Fact]
    public void MapsOnlyExplicitNativeChestCapabilityAndLeavesLegacyObjectsUntouched()
    {
        var source = Snapshot() with {
            NativeChestInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2]),
        };

        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.True(wire.Objects.Single(value => value.EntityId == 2).NativeChestInteractable);
        Assert.False(wire.Objects.Single(value => value.EntityId == 3).NativeChestInteractable);
        var roundTripped = Devilution.Protocol.V1.Snapshot.Parser.ParseFrom(wire.ToByteArray());
        Assert.Equal(wire, roundTripped);
        Assert.True(roundTripped.Objects.Single(value => value.EntityId == 2).NativeChestInteractable);
        Assert.False(roundTripped.Objects.Single(value => value.EntityId == 3).NativeChestInteractable);
        Assert.False(DiabloProtocolMapper.FromProtocolSnapshot(roundTripped)
            .NativeChestInteractableEntityIds.Contains(2));
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
            [
                NativeObject(2, 5),
                NativeObject(3, 5),
            ],
            [],
            [],
            null);

    private static DiabloObjectState NativeObject(uint entityId, uint nativeObjectId)
        => new(entityId, nativeObjectId, 1, checked((int)entityId), 10, false, 0,
            AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = nativeObjectId,
            };
}
