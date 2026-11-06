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

/** Real type-48 owners and the existing wall-trap lifecycle, including cold owner rehydration. */
public sealed class NativeTrappedSarcophagusHostTests
{
    public static IEnumerable<object[]> Cases => Enumerable.Range(1, 4).SelectMany(depth =>
        new[] { 0, 1, 7 }.SelectMany(missile => new[] { false, true }.Select(disarm => new object[] { depth, missile, disarm })));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OpeningPublishesLootAndOneShotTrapAcrossFreshHostRestore(int depth, int missile, bool disarm)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, sarcophagusTrapMissile: missile);
        var hero = Hero(fixture, depth);
        var runtime = fixture.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, (uint)depth));
        runtime.AddParticipant("hero", hero);
        var before = Build(fixture, hero, 0);
        var sarcophagus = before.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId);
        Assert.True(sarcophagus.NativeSarcophagusInteractable);
        Assert.True(sarcophagus.NativeTrapInteractable);
        Assert.True(sarcophagus.TrapArmed);
        Assert.True(AuthoritativeObjectSelector.IsValidNativeSarcophagusInteraction(sarcophagus));
        Assert.True(AuthoritativeObjectSelector.IsValidNativeTrapInteraction(sarcophagus));
        AuthoritativeSnapshotValidator.Validate(before);
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: before));
        if (disarm) {
            Assert.Equal(DiabloNativeTrapDisarmResult.Disarmed,
                fixture.World.TryDisarmNativeTrapTrigger(hero, fixture.Shrine, new Devilution.Engine.DeterministicLegacyRandom(0)));
        }
        var opened = fixture.Simulation.Execute(hero,
            new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]);
        Assert.True(opened.Succeeded, opened.Failure?.ToString());
        Assert.True(fixture.Shrine.Activated);
        Assert.Equal(!disarm, fixture.Shrine.TrapArmed);
        Assert.Empty(fixture.World.Projectiles); // Native OperateSarcophagus leaves firing to the next ProcessObjects.
        var loot = Assert.Single(fixture.World.WorldItems.Values);
        Assert.Equal(2U * (uint)depth, loot.State.CreateInfo & 63);
        await RestoreAndPublish(fixture, hero, depth, missile, 0, disarm);
        await using var live = new NativeTrapHostPublicationTests.SnapshotHost(0,
            tick => Build(fixture, hero, tick), runtime.AdvanceTo);
        await live.ConnectAsync();
        await live.PollAsync(1);
        var state = Assert.Single(fixture.World.CaptureNativeTraps()!);
        Assert.Equal(disarm ? DiabloNativeTrapDisposition.Disarmed : DiabloNativeTrapDisposition.Fired, state.Disposition);
        Assert.Equal(1, state.TrapVar4);
        Assert.False(state.TriggerTrapArmed);
        Assert.False(fixture.Shrine.TrapArmed);
        if (disarm) Assert.Empty(fixture.World.Projectiles);
        else {
            Assert.Equal(1UL, state.FiredTick);
            Assert.NotEmpty(fixture.World.Projectiles);
            Assert.All(fixture.World.Projectiles.Values, shot => {
                Assert.Equal(NativeShrineTestFixture.AdditionalObjectEntityId, shot.SourceEntityId);
                Assert.Equal(NativeShrineTestFixture.ShrineEntityId, shot.TargetEntityId);
                Assert.Equal((50, 51), (shot.TargetX, shot.TargetY));
            });
        }
        await RestoreAndPublish(fixture, hero, depth, missile, 1, disarm);
        var phases = new HashSet<string> { string.Join(',', live.Model.Snapshot!.Projectiles.Select(value => (value.Behavior, value.Phase)).Distinct().Order()) };
        for (ulong tick = 2; tick <= 256 && fixture.World.Projectiles.Count != 0; tick++) {
            var wire = await live.PollAsync(tick); // Every state of each real type-48-owned missile crosses the live publication path.
            var phase = string.Join(',', wire.Projectiles.Select(value => (value.Behavior, value.Phase)).Distinct().Order());
            if (phases.Add(phase)) await RestoreAndPublish(fixture, hero, depth, missile, tick, disarm);
        }
        Assert.Null(live.Server.LastClientFailure);
        runtime.AdvanceTo(256);
        Assert.Empty(fixture.World.Projectiles);
        Assert.Equal(state, Assert.Single(fixture.World.CaptureNativeTraps()!));
        Assert.Equal(loot.ItemSeed, Assert.Single(fixture.World.WorldItems.Values).ItemSeed);
        Assert.False(fixture.Simulation.Execute(hero,
            new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 256, [hero]).Succeeded);
        await RestoreAndPublish(fixture, hero, depth, missile, 256, disarm);
    }

    [Fact]
    public async Task PreviouslyRefusedClosedSarcophagusLoadsFromRealOldBytesAndCanFire()
    {
        var fixture = new NativeShrineTestFixture(1, 0, sarcophagusTrapMissile: 0);
        var hero = Hero(fixture, 1);
        var snapshot = fixture.Simulation.CaptureSnapshot(hero, 0);
        var checkpoint = NativeShrineHostPublicationTests.Checkpoint(fixture, hero, snapshot, 1) with {
            NativeSarcophagi = [], NativeTraps = [],
        };
        var codec = new DiabloCohortCodec();
        var old = codec.Deserialize(codec.Serialize(checkpoint));
        var fresh = new NativeShrineTestFixture(1, 0, sarcophagusTrapMissile: 0,
            restoredItems: old.CathedralItems! with { Levels = old.CathedralItems!.Levels.Where(value => value.LevelId == 1).ToArray() },
            restoredSarcophagi: old.NativeSarcophagi, restoredTraps: old.NativeTraps);
        var restoredHero = Hero(fresh, 1);
        fresh.Simulation.RestoreSnapshot(restoredHero, snapshot with { Player = old.Participants[0].Player, Objects = old.Objects });
        var runtime = fresh.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, 1));
        runtime.AddParticipant("hero", restoredHero);
        Assert.True(fresh.Simulation.Execute(restoredHero,
            new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [restoredHero]).Succeeded);
        runtime.AdvanceTo(1);
        Assert.Equal(DiabloNativeTrapDisposition.Fired, Assert.Single(fresh.World.CaptureNativeTraps()!).Disposition);
        await RestoreAndPublish(fresh, restoredHero, 1, 0, 1, false);
    }

    [Fact]
    public void AClosedSarcophagusCannotClaimAConsumedFiringAtAnyAdmissionLayer()
    {
        var fixture = new NativeShrineTestFixture(1, 0, sarcophagusTrapMissile: 0);
        var hero = Hero(fixture, 1);
        var runtime = fixture.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, 1));
        runtime.AddParticipant("hero", hero);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        runtime.AdvanceTo(256);
        var wire = Build(fixture, hero, 256);
        AuthoritativeSnapshotValidator.Validate(wire);
        wire.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId).Activated = false;
        var checkpoint = NativeShrineHostPublicationTests.Checkpoint(fixture, hero, fixture.Simulation.CaptureSnapshot(hero, 256), 1) with {
            NativeTraps = fixture.World.CaptureNativeTraps(), NativeSarcophagi = fixture.World.CaptureNativeSarcophagi(),
        };
        var codec = new DiabloCohortCodec();
        var json = JsonNode.Parse(codec.Serialize(checkpoint))!;
        var owner = json["NativeSarcophagi"]![0]!;
        owner["Opened"] = false; owner["AnimationFlag"] = 0; owner["AnimationDelay"] = 1000; owner["AnimationCount"] = 0;
        owner["AnimationFrame"] = 1;
        var trigger = json["Objects"]!.AsArray().Single(value => value!["EntityId"]!.GetValue<uint>() == fixture.Shrine.EntityId)!;
        trigger["Activated"] = false; trigger["AnimationFrame"] = 1;
        Assert.Multiple(
            () => Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(wire)),
            () => Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: wire))),
            () => Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString())));
    }

    private static Snapshot Build(NativeShrineTestFixture fixture, AuthoritativePlayerState hero, ulong tick)
        => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells);

    private static async Task RestoreAndPublish(NativeShrineTestFixture fixture, AuthoritativePlayerState hero,
        int depth, int missile, ulong tick, bool disarm)
    {
        var snapshot = fixture.Simulation.CaptureSnapshot(hero, tick);
        var checkpoint = NativeShrineHostPublicationTests.Checkpoint(fixture, hero, snapshot, depth) with {
            NativeSarcophagi = fixture.World.CaptureNativeSarcophagi(), NativeTraps = fixture.World.CaptureNativeTraps(),
        };
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(checkpoint);
        var decoded = codec.Deserialize(bytes);
        var fresh = new NativeShrineTestFixture(depth, 0, sarcophagusTrapMissile: missile,
            restoredItems: decoded.CathedralItems! with { Levels = decoded.CathedralItems!.Levels.Where(value => value.LevelId == depth).ToArray() },
            restoredSarcophagi: decoded.NativeSarcophagi, restoredTraps: decoded.NativeTraps);
        var restoredHero = Hero(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restoredHero, snapshot with { Player = decoded.Participants[0].Player,
            Objects = decoded.Objects, WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles });
        Assert.Equal(fixture.World.CaptureNativeSarcophagi(), fresh.World.CaptureNativeSarcophagi());
        Assert.Equal(fixture.World.CaptureNativeTraps(), fresh.World.CaptureNativeTraps());
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(tick,
            value => Build(fresh, restoredHero, value), _ => { });
        await host.ConnectAsync(); // Real publication, host validation, range checks, protobuf and client admission.
        var wire = Snapshot.Parser.ParseFrom(host.Model.Snapshot!.ToByteArray());
        var model = new AuthoritativeClientModel(); model.Apply(new AuthoritativeClientMessage(Snapshot: wire));
        var wall = wire.Objects.Single(value => value.EntityId == NativeShrineTestFixture.AdditionalObjectEntityId);
        Assert.Equal(!disarm && tick > 0 ? NativeShrineTestFixture.ShrineEntityId : 0, wall.NativeTrapFiredTriggerEntityId);
        Assert.Equal(!disarm && tick > 0, wall.HasNativeTrapFiredTick);
        if (wall.HasNativeTrapFiredTick) Assert.Equal(1UL, wall.NativeTrapFiredTick);
        Assert.False(wire.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId).NativeSarcophagusInteractable);
        Assert.Null(host.Server.LastClientFailure);
        Assert.Equal(bytes, codec.Serialize(decoded));
        if (tick == 0) {
            var resumed = fresh.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 51, (uint)depth));
            resumed.AddParticipant("hero", restoredHero);
            resumed.AdvanceTo(1);
            var after = Assert.Single(fresh.World.CaptureNativeTraps()!);
            Assert.Equal(disarm ? DiabloNativeTrapDisposition.Disarmed : DiabloNativeTrapDisposition.Fired, after.Disposition);
            Assert.Equal(!disarm, fresh.World.Projectiles.Count != 0);
        } else {
            Assert.Empty(fresh.World.ConsumeReadyNativeTraps([restoredHero], tick + 1));
        }
    }

    private static AuthoritativePlayerState Hero(NativeShrineTestFixture fixture, int depth)
        => new(0, 0, 6400, 0, 0, new(new(50, 50), new(40, 40), new(200, 200), new(60, 60)),
            [], [], [], 50, 51, 6400, 10, (uint)depth, 0,
            playerClass: fixture.Classes.GetRequired(1), abilitySpellId: 28, abilityKind: DiabloPlayerAbilityKind.TrapDisarm, items: fixture.Items) { EntityId = 1 };
}
