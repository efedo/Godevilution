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
    [InlineData(1, 66)] [InlineData(5, 66)] [InlineData(1, 76)] [InlineData(5, 76)]
    [InlineData(1, 79)] [InlineData(5, 79)] [InlineData(1, 80)] [InlineData(5, 80)]
    [InlineData(1, 81)] [InlineData(5, 81)] [InlineData(1, 82)] [InlineData(5, 82)]
    public void ThemeEffectsPublishAndSurviveTheRealCohortAndFreshOwnerRestore(int depth, int nativeType)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: nativeType);
        var hero = Player(fixture, depth);
        if (depth == 5 && nativeType is 66 or 76) {
            hero.Life = hero.Mana = 6336;
            hero.RestoreNativeResourceBases(6000, 6000);
        }
        // Capture the first, previously-refused state as well as the consumed state.
        Publish(0);
        var result = fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]);
        Assert.True(result.Succeeded);
        if (nativeType is 79 or 80) {
            var batch = DiabloProtocolMapper.ToProtocolEventBatch(result.Events, hero.EntityId, 0)!;
            var client = new AuthoritativeClientModel();
            client.Apply(new AuthoritativeClientMessage(Events: EventBatch.Parser.ParseFrom(batch.ToByteArray())));
            Assert.Contains(batch.Events, value => value.Dialogue is { Text.Length: > 0 });
        }
        Publish(0);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(Checkpoint(fixture, hero, saved, depth));
        var decoded = codec.Deserialize(bytes);
        Assert.Equal(bytes, codec.Serialize(decoded));
        var pool = decoded.CathedralItems! with { Levels = decoded.CathedralItems.Levels.Where(value => value.LevelId == (uint)depth).ToArray() };
        var fresh = new NativeShrineTestFixture(depth, 0, restoredItems: pool, nativeObjectId: nativeType);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with {
            Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
            WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles,
        });
        var recaptured = fresh.Simulation.CaptureSnapshot(restored, 0);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(saved.Player), System.Text.Json.JsonSerializer.Serialize(recaptured.Player));
        Assert.Equal(saved.Objects, recaptured.Objects);
        Assert.Equal(saved.Projectiles, recaptured.Projectiles);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 0), spells: fresh.Spells));
        Assert.Equal(nativeType is 66 or 76,
            fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(fresh.Shrine.EntityId), 1, [restored]).Succeeded);
        if (nativeType == 81) {
            var remaining = Assert.Single(restored.StatusEffects).RemainingTicks;
            fresh.Simulation.AdvanceStatusEffects(restored, 1);
            fresh.Simulation.AdvanceStatusEffects(restored, remaining + 1);
            Assert.Empty(restored.StatusEffects);
        }

        void Publish(ulong tick)
        {
            var capture = fixture.Simulation.CapturePresentationSnapshot(hero, tick);
            Assert.Equal(!fixture.Shrine.Activated, capture.NativeThemeEffectInteractableEntityIds.Contains(fixture.Shrine.EntityId));
            Admit(DiabloProtocolMapper.ToProtocolSnapshot(capture, spells: fixture.Spells));
        }
    }

    [Theory]
    [InlineData(1, 26)] [InlineData(5, 91)]
    public void BurningCrossDamageRetainsManaShieldBasesAndResumesOnTheNextNativeTick(int depth, int type)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth);
        var damage = depth <= 4 ? 6 : 8;
        hero.PositionY = 49; hero.Mana = 62 + damage; hero.ManaShieldActive = true;
        fixture.Simulation.AdvanceTick(1, [hero]);
        Assert.Equal(62, hero.Mana);
        Assert.False(hero.ManaShieldActive);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 1);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(saved, spells: fixture.Spells));
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, saved, depth)));
        var fresh = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with {
            Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
            Projectiles = decoded.Projectiles, WorldItems = decoded.WorldItems,
        });
        fixture.Simulation.AdvanceTick(2, [hero]); fresh.Simulation.AdvanceTick(2, [restored]);
        Assert.Equal(hero.Mana, restored.Mana); Assert.Equal(hero.NativeManaBase, restored.NativeManaBase);
        Assert.Equal(hero.Life, restored.Life);
        Assert.Equal(saved.Player.Mana, restored.Mana);
        Assert.Equal(saved.Player.Life - damage, restored.Life);
        Assert.False(restored.ManaShieldActive);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 2), spells: fresh.Spells));
    }

    [Theory]
    [InlineData(1, 66)] [InlineData(5, 80)] [InlineData(1, 82)]
    public void OlderUnoperatedThemeObjectsLoadWithoutAnyNewSaveFields(int depth, int type)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var node = System.Text.Json.Nodes.JsonNode.Parse(codec.Serialize(Checkpoint(fixture, hero, saved, depth)))!;
        RemoveBases(node);
        var decoded = codec.Deserialize(node.ToJsonString());
        Assert.Equal(Checkpoint(fixture, hero, saved, depth).FormatVersion, decoded.FormatVersion);
        var fresh = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = Assert.Single(decoded.Participants).Player,
            Objects = decoded.Objects, Projectiles = decoded.Projectiles, WorldItems = decoded.WorldItems });
        Assert.Null(restored.NativeLifeBase);
        Assert.True(fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(fresh.Shrine.EntityId), 1, [restored]).Succeeded);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 1), spells: fresh.Spells));

        static void RemoveBases(System.Text.Json.Nodes.JsonNode node)
        {
            if (node is System.Text.Json.Nodes.JsonObject value) {
                value.Remove("NativeLifeBase"); value.Remove("NativeManaBase");
                foreach (var entry in value.ToArray()) if (entry.Value is not null) RemoveBases(entry.Value);
            } else if (node is System.Text.Json.Nodes.JsonArray array)
                foreach (var entry in array) if (entry is not null) RemoveBases(entry);
        }
    }

    [Theory]
    [InlineData(80)] [InlineData(81)]
    public async Task ThemeEffectResumePublishesThroughAFreshTcpHost(int type)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectId: type);
        var hero = Player(fixture, 5);
        fixture.Simulation.AdvanceStatusEffects(hero, 0);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, saved, 5)));
        var fresh = new NativeShrineTestFixture(5, 0, nativeObjectId: type);
        var restored = Player(fresh, 5);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = Assert.Single(decoded.Participants).Player,
            Objects = decoded.Objects, Projectiles = decoded.Projectiles, WorldItems = decoded.WorldItems });
        // Cohort restoration restores its participant clock separately from the player snapshot.
        restored.LastAppliedTick = Assert.Single(decoded.Participants).LastAppliedTick;
        var runtime = fresh.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, 5));
        runtime.AddParticipant("hero", restored);
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, tick), spells: fresh.Spells),
            tick => runtime.AdvanceTo(tick));
        await host.ConnectAsync();
        Assert.True(host.Model.Snapshot!.Objects.Single(value => value.EntityId == fresh.Shrine.EntityId).Activated);
        var wire = await host.PollAsync(1);
        Assert.Equal(1UL, wire.Tick);
        Assert.Null(host.Server.LastClientFailure);
        if (type == 81) Assert.Equal(2534U, Assert.Single(wire.Players[0].StatusEffects).RemainingTicks);
    }
}
