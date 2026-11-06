using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineHostPublicationTests
{
    [Theory]
    [InlineData(1, 18)]
    [InlineData(5, 18)]
    [InlineData(1, 25)]
    [InlineData(5, 25)]
    public async Task DeadPeerKeepsZeroLifeAndNativePersistentTaintedChangesAcrossPublicationAndRestore(int depth, int type)
    {
        var fixture = new NativeShrineTestFixture(depth, type, 743271966, mode: DiabloGameMode.Multiplayer);
        var actor = Player(fixture, depth);
        var peer = DeadPeer(fixture, depth);
        var before = fixture.Simulation.CaptureSnapshot(peer, 0).Player;
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(peer, 0), spells: fixture.Spells));
        Assert.True(fixture.Simulation.Execute(actor, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [actor, peer]).Succeeded);
        Assert.Equal(0, peer.Life);
        Assert.Equal(DiabloPlayerCombatMode.Death, peer.CombatMode);
        if (type == 18) {
            Assert.Equal(before.Attributes, peer.Attributes);
            Assert.Equal((before.LifeMaximum, before.BaseLifeMaximum, before.Mana, before.ManaMaximum, before.BaseManaMaximum,
                before.NativeLifeBase, before.NativeManaBase), (peer.LifeMaximum, peer.BaseLifeMaximum, peer.Mana, peer.ManaMaximum,
                peer.BaseManaMaximum, peer.NativeLifeBase, peer.NativeManaBase));
        } else {
            Assert.Equal((49, 39, 44, 61), (peer.Attributes.Strength.Base, peer.Attributes.Magic.Base, peer.Attributes.Dexterity.Base, peer.Attributes.Vitality.Base));
            Assert.Equal((6528, 6528, 128), (peer.LifeMaximum, peer.BaseLifeMaximum, peer.NativeLifeBase));
            Assert.Equal((6336, 6336, 1216, 1216), (peer.ManaMaximum, peer.BaseManaMaximum, peer.Mana, peer.NativeManaBase));
        }
        peer.RecalculateEquipmentResources();
        Assert.Equal(0, peer.Life);
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(peer, tick), spells: fixture.Spells),
            tick => fixture.Simulation.AdvanceTick(tick, [actor, peer]));
        await host.ConnectAsync();
        Assert.Equal(0, (await host.PollAsync(1)).Players.Single().Life);
        var saved = fixture.Simulation.CaptureSnapshot(peer, 1);
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, peer, saved, depth)));
        var fresh = new NativeShrineTestFixture(depth, type, 743271966, mode: DiabloGameMode.Multiplayer);
        var restored = DeadPeer(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = Assert.Single(decoded.Participants).Player,
            Objects = decoded.Objects, WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles });
        Assert.Equal(0, restored.Life);
        Assert.Equal(saved.Player.Attributes, restored.Attributes);
        Assert.Equal((saved.Player.LifeMaximum, saved.Player.BaseLifeMaximum, saved.Player.NativeLifeBase,
            saved.Player.Mana, saved.Player.ManaMaximum, saved.Player.BaseManaMaximum, saved.Player.NativeManaBase),
            (restored.LifeMaximum, restored.BaseLifeMaximum, restored.NativeLifeBase,
            restored.Mana, restored.ManaMaximum, restored.BaseManaMaximum, restored.NativeManaBase));
        restored.RecalculateEquipmentResources();
        Assert.Equal(0, restored.Life);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 1), spells: fresh.Spells));
        Assert.Null(host.Server.LastClientFailure);
    }

    public static IEnumerable<object[]> OperatorOnlyShrines => Enumerable.Range(0, 26)
        .Where(type => type is not (18 or 25)).Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(OperatorOnlyShrines))]
    public void OtherShrinesDoNotChangeDeadPeersResourcesPositionOrInventory(int type)
    {
        var fixture = new NativeShrineTestFixture(5, type, mode: DiabloGameMode.Multiplayer);
        var actor = Player(fixture, 5);
        var peer = DeadPeer(fixture, 5);
        var before = fixture.Simulation.CaptureSnapshot(peer, 0).Player;
        Assert.True(fixture.Simulation.Execute(actor, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [actor, peer]).Succeeded);
        fixture.Simulation.AdvanceTick(1, [actor, peer]);
        var after = fixture.Simulation.CaptureSnapshot(peer, 1).Player;
        Assert.Equal((before.Life, before.LifeMaximum, before.BaseLifeMaximum, before.NativeLifeBase,
            before.Mana, before.ManaMaximum, before.BaseManaMaximum, before.NativeManaBase, before.PositionX, before.PositionY),
            (after.Life, after.LifeMaximum, after.BaseLifeMaximum, after.NativeLifeBase,
            after.Mana, after.ManaMaximum, after.BaseManaMaximum, after.NativeManaBase, after.PositionX, after.PositionY));
        Assert.Equal(before.Attributes, after.Attributes);
        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(before.Equipment, after.Equipment);
        Assert.Equal(before.MemorizedSpells, after.MemorizedSpells);
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(peer, 1),
            [fixture.Simulation.CaptureSnapshot(actor, 1).Player, after], spells: fixture.Spells);
        Admit(wire);
    }

    private static AuthoritativePlayerState DeadPeer(NativeShrineTestFixture fixture, int depth)
    {
        var peer = Player(fixture, depth);
        peer.EntityId = 2; peer.PositionX = 52; peer.Life = 0; peer.Mana = 1280;
        peer.RestoreNativeResourceBases(0, 1280);
        peer.CombatMode = DiabloPlayerCombatMode.Death; peer.CombatModeEndTick = 10;
        peer.CurrentAction = new(DiabloPlayerActionKind.Death, 0, 0, 10, 10) {
            EffectApplied = true, TargetEntityId = 1,
        };
        return peer;
    }
}
