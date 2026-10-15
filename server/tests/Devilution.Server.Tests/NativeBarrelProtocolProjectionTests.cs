using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeBarrelProtocolProjectionTests
{
    [Fact]
    public void TransientCapabilityDoesNotChangeSnapshotValueEquality()
    {
        var source = Snapshot();
        var first = source with {
            NativeBarrelMeleeBreakableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2, 3]),
        };
        var second = source with {
            NativeBarrelMeleeBreakableEntityIds = DiabloNativeChestInteractableEntitySet.Create([3, 2]),
        };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(source, source with {
            NativeBarrelMeleeBreakableEntityIds = DiabloNativeChestInteractableEntitySet.Empty,
        });
    }

    [Fact]
    public void MapsOnlyExplicitNativeBarrelCapabilityAndImportDiscardsIt()
    {
        var source = Snapshot() with {
            NativeBarrelMeleeBreakableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2]),
        };

        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var roundTripped = Devilution.Protocol.V1.Snapshot.Parser.ParseFrom(wire.ToByteArray());

        Assert.Equal(wire, roundTripped);
        Assert.True(roundTripped.Objects.Single(value => value.EntityId == 2).NativeBarrelMeleeBreakable);
        Assert.False(roundTripped.Objects.Single(value => value.EntityId == 3).NativeBarrelMeleeBreakable);
        Assert.All(roundTripped.Objects, value => {
            Assert.False(value.NativeChestInteractable);
            Assert.False(value.NativeSarcophagusInteractable);
        });
        Assert.Empty(DiabloProtocolMapper.FromProtocolSnapshot(roundTripped)
            .NativeBarrelMeleeBreakableEntityIds);
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
            [NativeObject(2, 57), NativeObject(3, 58)],
            [],
            [],
            null);

    private static DiabloObjectState NativeObject(uint entityId, uint nativeObjectId)
        => new(entityId, nativeObjectId, 1, checked((int)entityId), 10, false, 0,
            AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = nativeObjectId,
                MissilePassable = true,
            };
}
