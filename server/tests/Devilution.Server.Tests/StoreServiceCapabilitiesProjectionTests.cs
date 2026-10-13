using Devilution.Diablo;
using Google.Protobuf;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreServiceCapabilitiesProjectionTests
{
    [Fact]
    public void OptionalHintsPreserveLegacyStoreWireAndStateHashAndAreNotHydrated()
    {
        var legacy = DiabloProtocolMapper.ToProtocolSnapshot(CreateSource(null));
        var allFalse = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateSource(new DiabloStoreServices(false, false, false, false)));
        var available = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateSource(new DiabloStoreServices(true, false, true, false)));

        Assert.Null(legacy.ActiveStore.Services);
        Assert.Equal(new byte[] { 0x08, 0x0C }, legacy.ActiveStore.ToByteArray());
        Assert.NotNull(allFalse.ActiveStore.Services);
        Assert.Equal(
            new byte[] { 0x08, 0x0C, 0x1A, 0x00 },
            allFalse.ActiveStore.ToByteArray());

        var roundTrippedStore = StoreSnapshot.Parser.ParseFrom(available.ActiveStore.ToByteArray());
        Assert.NotNull(roundTrippedStore.Services);
        Assert.True(roundTrippedStore.Services.Repair);
        Assert.False(roundTrippedStore.Services.Recharge);
        Assert.True(roundTrippedStore.Services.Identify);
        Assert.False(roundTrippedStore.Services.RefillMana);

        Assert.Equal(legacy.StateSha256, allFalse.StateSha256);
        Assert.Equal(legacy.StateSha256, available.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(legacy), legacy.StateSha256);

        var imported = DiabloProtocolMapper.FromProtocolSnapshot(available);
        Assert.Null(imported.ActiveStore!.Services);
    }

    private static DiabloSimulationSnapshot CreateSource(DiabloStoreServices? services)
    {
        var player = new StorePlayerSnapshot(
            0,
            12,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        return new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            new DiabloStoreState(12, []) { Services = services });
    }
}