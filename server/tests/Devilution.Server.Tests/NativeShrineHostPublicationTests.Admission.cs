using System.Text.Json.Nodes;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineHostPublicationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void PreShrineSerializedCohortRestoresAndItsPreviouslyRefusedShrineOperates(int depth)
    {
        var fixture = new NativeShrineTestFixture(depth, 13);
        var hero = Player(fixture, depth);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var document = JsonNode.Parse(codec.Serialize(Checkpoint(fixture, hero, saved, depth)))!;
        RemoveAdditions(document);
        var legacyBytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        Assert.DoesNotContain("NativeManaBase", System.Text.Encoding.UTF8.GetString(legacyBytes));
        var decoded = codec.Deserialize(System.Text.Encoding.UTF8.GetString(legacyBytes));
        Assert.Null(Assert.Single(decoded.Participants).Player.NativeManaBase);
        var fresh = new NativeShrineTestFixture(depth, 13);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with {
            Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
            Projectiles = decoded.Projectiles, WorldItems = decoded.WorldItems,
        });
        Assert.True(fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 1, [restored]).Succeeded);
        Assert.Equal(hero.Attributes.Magic.Base + 2, restored.Attributes.Magic.Base);
        Assert.True(fresh.Shrine.Activated);

        static void RemoveAdditions(JsonNode node)
        {
            if (node is JsonObject value) {
                foreach (var key in new[] { "NativeLifeBase", "NativeManaBase", "FullyExploredLevelIds", "NativeRandomSeed" }) value.Remove(key);
                foreach (var child in value.ToArray()) if (child.Value is not null) RemoveAdditions(child.Value);
            } else if (node is JsonArray array) foreach (var child in array) if (child is not null) RemoveAdditions(child);
        }
    }

    [Theory]
    [InlineData(59U)]
    [InlineData(60U)]
    public void HostAndClientAgreeOnRealShrineCapabilityAndRejectForgeries(uint type)
    {
        var fixture = new NativeShrineTestFixture(5, 13);
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(Player(fixture, 5), 0), spells: fixture.Spells);
        var shrine = snapshot.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId);
        shrine.NativeObjectId = type;
        Admit(snapshot);
        var corruptions = new Action<ObjectSnapshot>[] {
            value => value.NativeObjectId = 61,
            value => value.NativeShrineType = 26,
            value => value.Activated = true,
            value => value.Destroyed = true,
            value => value.IsShrine = false,
            value => value.NativeChestInteractable = true,
            value => value.TrapArmed = true,
        };
        foreach (var corrupt in corruptions) {
            var invalid = snapshot.Clone();
            corrupt(invalid.Objects.Single(value => value.EntityId == shrine.EntityId));
            Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(invalid));
            Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void NativeNegativeManaAndKnownLevelZeroSurviveWireAndRejectCorruption()
    {
        var fixture = new NativeShrineTestFixture(5, 9);
        var hero = Player(fixture, 5);
        hero.RestoreNativeResourceBases(3200, 0);
        hero.BaseManaMaximum = 0;
        hero.RestoreNativeMana(-640, 0);
        hero.MemorizedSpells[0] = hero.MemorizedSpells[0] with { Level = 0 };
        hero.MemorizedSpells[1] = hero.MemorizedSpells[1] with { Level = 0 };
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, 0), spells: fixture.Spells);
        Admit(snapshot);
        var wire = Assert.Single(snapshot.Players);
        Assert.Equal(-640, wire.NativeMana);
        Assert.Equal(0, wire.Mana);
        Assert.Equal(2, wire.ZeroLevelMemorizedSpells.Count);
        var corruptions = new Action<PlayerSnapshot>[] {
            value => value.ClearNativeManaMaximum(),
            value => value.NativeMana = -(2000 << 6) - 1,
            value => value.Mana = 1,
            value => value.ZeroLevelMemorizedSpells[0].Level = 1,
            value => value.ZeroLevelMemorizedSpells[0].SpellId = value.MemorizedSpells[0].SpellId,
            value => value.FullyExploredLevelIds.Add(0),
        };
        foreach (var corrupt in corruptions) {
            var invalid = snapshot.Clone(); corrupt(invalid.Players[0]);
            Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(invalid));
            Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(25)]
    public void CooperativeEffectsReachEveryOtherPlayerOnTheFloorAndNotTheActor(int type)
    {
        var fixture = new NativeShrineTestFixture(5, type, mode: DiabloGameMode.Multiplayer);
        var actor = Player(fixture, 5);
        var peers = Enumerable.Range(0, 3).Select(index => Player(fixture, 5)).ToArray();
        for (var index = 0; index < peers.Length; index++) { peers[index].EntityId = (uint)index + 2; peers[index].PositionX += index + 1; }
        peers[2].LevelId = 4;
        var participants = new[] { actor }.Concat(peers).ToArray();
        var life = actor.Life; var attributes = actor.Attributes;
        var result = fixture.Simulation.Execute(actor, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, participants);
        Assert.True(result.Succeeded);
        Assert.Equal(life, actor.Life); Assert.Equal(attributes, actor.Attributes);
        foreach (var peer in peers.Take(2)) {
            if (type == 18) Assert.Equal(peer.LifeMaximum, peer.Life);
            else Assert.NotEqual(attributes, peer.Attributes);
            Admit(DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(peer, 0), spells: fixture.Spells));
            var captured = fixture.Simulation.CaptureSnapshot(peer, 0);
            var codec = new DiabloCohortCodec();
            var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, peer, captured, 5)));
            captured = captured with { Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
                WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles };
            var fresh = new NativeShrineTestFixture(5, type, mode: DiabloGameMode.Multiplayer);
            var restored = Player(fresh, 5); restored.EntityId = peer.EntityId;
            fresh.Simulation.RestoreSnapshot(restored, captured);
            Assert.Equal(peer.Attributes, restored.Attributes); Assert.Equal(peer.Life, restored.Life);
        }
        Assert.Equal(life, peers[2].Life); Assert.Equal(attributes, peers[2].Attributes);
        var directed = fixture.Simulation.AdvanceTick(1, participants.Take(3).ToArray())
            .Where(delivery => delivery.Event.Kind == DiabloGameplayEventKind.Dialogue).ToArray();
        Assert.Equal(peers.Take(2), directed.Select(delivery => delivery.Recipient));
        foreach (var delivery in directed) {
            var batch = DiabloProtocolMapper.ToProtocolEventBatch([delivery.Event], delivery.Recipient.EntityId, 1)!;
            var model = new AuthoritativeClientModel();
            model.Apply(new AuthoritativeClientMessage(Events: EventBatch.Parser.ParseFrom(batch.ToByteArray())));
            Assert.NotEmpty(batch.Events[0].Dialogue.Text);
        }
    }

    [Fact]
    public async Task WisdomUnderManaPenaltyEquipmentPublishesAndRestoresNegativeMaximum()
    {
        var fixture = new NativeShrineTestFixture(5, 9);
        var hero = Player(fixture, 5);
        hero.Equipment[0] = hero.Equipment[0] with { State = hero.Equipment[0].State with { Identified = true, PlusMana = -6400 } };
        hero.RestoreNativeResourceBases(3200, 0);
        hero.RecalculateEquipmentResources();
        Assert.Equal((-6400, 0), (hero.Mana, hero.ManaMaximum));
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]).Succeeded);
        Assert.Equal((-7040, -640), (hero.Mana, hero.ManaMaximum));
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells), _ => { });
        await host.ConnectAsync();
        Assert.Equal(-640, host.Model.Snapshot!.Players[0].NativeManaMaximum);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, saved, 5)));
        var fresh = new NativeShrineTestFixture(5, 9);
        var restored = Player(fresh, 5);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = Assert.Single(decoded.Participants).Player,
            Objects = decoded.Objects, WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles });
        Assert.Equal((-7040, -640, -640), (restored.Mana, restored.ManaMaximum, restored.NativeManaBase));
        // Recalculating after restoration must use the retained current base, not infer it from clipped maxima.
        restored.RecalculateEquipmentResources();
        Assert.Equal((-7040, 0, -640), (restored.Mana, restored.ManaMaximum, restored.NativeManaBase));
    }

    private static void Admit(Snapshot snapshot)
    {
        var decoded = Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
        AuthoritativeSnapshotValidator.Validate(decoded);
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: decoded));
        Assert.Equal(snapshot.Tick, model.Snapshot!.Tick);
    }
}
