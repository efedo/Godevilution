using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;
using static Devilution.Server.Tests.NativeShrineHostPublicationTests;

namespace Devilution.Server.Tests;

/** Both object families share the level owner and item pool through publication and a fresh host. */
public sealed class NativeThemeObjectCoexistenceTests
{
    [Theory]
    [InlineData(1, 66)] [InlineData(5, 66)] [InlineData(1, 76)] [InlineData(5, 76)]
    [InlineData(1, 79)] [InlineData(5, 79)] [InlineData(1, 80)] [InlineData(5, 80)]
    [InlineData(1, 81)] [InlineData(5, 81)] [InlineData(1, 82)] [InlineData(5, 82)]
    public async Task ItemObjectAndThemeEffectRetainLootEffectsAndCapabilitiesOnTheSameLevel(int depth, int effectType)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectType: 64, additionalNativeObjectType: effectType);
        var hero = Player(fixture, depth);
        var itemId = fixture.Shrine.EntityId;
        var effectId = NativeShrineTestFixture.AdditionalObjectEntityId;
        var initial = fixture.Simulation.CapturePresentationSnapshot(hero, 0);
        Assert.Contains(itemId, initial.NativeThemeObjectInteractableEntityIds);
        Assert.Contains(effectId, initial.NativeThemeEffectInteractableEntityIds);
        var initialWire = DiabloProtocolMapper.ToProtocolSnapshot(initial, spells: fixture.Spells);
        Admit(initialWire);
        Assert.True(initialWire.Objects.Single(value => value.EntityId == itemId).NativeThemeObjectInteractable);
        Assert.True(initialWire.Objects.Single(value => value.EntityId == effectId).NativeThemeObjectInteractable);
        fixture.Simulation.AdvanceStatusEffects(hero, 0);
        var runtime = fixture.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, (uint)depth));
        runtime.AddParticipant("hero", hero);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(itemId), 0, [hero]).Succeeded);
        var item = Assert.Single(fixture.World.WorldItems.Values);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(effectId), 0, [hero]).Succeeded);
        Assert.Contains(item.EntityId, fixture.World.WorldItems.Keys);
        await using (var firstHost = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells),
            tick => runtime.AdvanceTo(tick))) {
            await firstHost.ConnectAsync();
            Assert.True(firstHost.Model.Snapshot!.Objects.Single(value => value.EntityId == itemId).Activated);
            Assert.Equal(effectType is 66 or 76,
                firstHost.Model.Snapshot.Objects.Single(value => value.EntityId == effectId).NativeThemeObjectInteractable);
            Assert.Contains(firstHost.Model.Snapshot.WorldItems, value => value.EntityId == item.EntityId);
            Assert.Null(firstHost.Server.LastClientFailure);
        }
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(Checkpoint(fixture, hero, saved, depth));
        var decoded = codec.Deserialize(bytes);
        Assert.Equal(bytes, codec.Serialize(decoded));
        var pool = decoded.CathedralItems! with { Levels = decoded.CathedralItems!.Levels
            .Where(value => value.LevelId == (uint)depth).ToArray() };
        var fresh = new NativeShrineTestFixture(depth, 0, restoredItems: pool,
            nativeObjectType: 64, additionalNativeObjectType: effectType);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with {
            Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
            WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles,
        });
        restored.LastAppliedTick = Assert.Single(decoded.Participants).LastAppliedTick;
        var recaptured = fresh.Simulation.CaptureSnapshot(restored, 0);
        Assert.Equal(JsonSerializer.Serialize(saved.Player), JsonSerializer.Serialize(recaptured.Player));
        Assert.Equal(saved.Objects, recaptured.Objects);
        Assert.Equal(saved.WorldItems, recaptured.WorldItems);
        Assert.Equal(saved.Projectiles, recaptured.Projectiles);
        fresh.World.CaptureCathedralItems();
        var resumedRuntime = fresh.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, (uint)depth));
        resumedRuntime.AddParticipant("hero", restored);
        await using var resumedHost = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, tick), spells: fresh.Spells),
            tick => resumedRuntime.AdvanceTo(tick));
        await resumedHost.ConnectAsync();
        var wire = await resumedHost.PollAsync(1);
        runtime.AdvanceTo(1);
        var expected = fixture.Simulation.CaptureSnapshot(hero, 1);
        var actual = fresh.Simulation.CaptureSnapshot(restored, 1);
        Assert.Equal(JsonSerializer.Serialize(expected.Player), JsonSerializer.Serialize(actual.Player));
        Assert.Equal(expected.Objects, actual.Objects);
        Assert.Equal(expected.WorldItems, actual.WorldItems);
        Assert.Equal(expected.Projectiles, actual.Projectiles);
        Assert.Equal(1UL, wire.Tick);
        Assert.True(wire.Objects.Single(value => value.EntityId == itemId).Activated);
        var retainedEffect = wire.Objects.Single(value => value.EntityId == effectId);
        Assert.Equal(effectType is not (66 or 76), retainedEffect.Activated);
        Assert.Equal(effectType is 66 or 76, retainedEffect.NativeThemeObjectInteractable);
        if (effectType == 80) Assert.Equal(3U, retainedEffect.AnimationFrame);
        Assert.False(fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(itemId), 1, [restored]).Succeeded);
        Assert.Equal(effectType is 66 or 76,
            fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(effectId), 1, [restored]).Succeeded);
        Assert.Null(resumedHost.Server.LastClientFailure);
        Assert.False(resumedHost.Server.Completion.IsCompleted);
    }

    private static void Admit(Snapshot snapshot)
    {
        AuthoritativeSnapshotValidator.Validate(snapshot);
        var client = new AuthoritativeClientModel();
        client.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot.Parser.ParseFrom(snapshot.ToByteArray())));
        Assert.NotNull(client.Snapshot);
    }
}
