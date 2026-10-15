using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeSarcophagusProtocolProjectionTests
{
    [Fact]
    public void TransientCapabilityRetainsSnapshotValueEquality()
    {
        var source = Snapshot();
        var first = source with {
            NativeSarcophagusInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2, 3]),
        };
        var second = source with {
            NativeSarcophagusInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([3, 2]),
        };
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(source, source with {
            NativeSarcophagusInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Empty,
        });
    }

    [Fact]
    public void WireRoundTripPreservesExplicitCapabilityButImportDiscardsIt()
    {
        var source = Snapshot() with {
            NativeSarcophagusInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([2]),
        };
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var decoded = Devilution.Protocol.V1.Snapshot.Parser.ParseFrom(wire.ToByteArray());

        Assert.Equal(wire, decoded);
        Assert.True(decoded.Objects.Single(value => value.EntityId == 2).NativeSarcophagusInteractable);
        Assert.False(decoded.Objects.Single(value => value.EntityId == 3).NativeSarcophagusInteractable);
        Assert.All(decoded.Objects, value => Assert.False(value.NativeChestInteractable));
        Assert.Empty(DiabloProtocolMapper.FromProtocolSnapshot(decoded).NativeSarcophagusInteractableEntityIds);
    }

    private static DiabloSimulationSnapshot Snapshot()
        => new(1,
            new StorePlayerSnapshot(0, null, [], 0, 64, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                EntityId = 1,
                LevelId = 1,
            },
            [], [],
            [NativeObject(2), NativeObject(3)],
            [], [], null);

    private static DiabloObjectState NativeObject(uint entityId)
        => new(entityId, 48, 1, checked((int)entityId), 10, false, 0,
            AuthoritativeObjectEffectKind.None, 0) { NativeObjectId = 48 };
}
