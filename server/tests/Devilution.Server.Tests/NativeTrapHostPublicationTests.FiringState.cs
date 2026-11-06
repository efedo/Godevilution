using System.Text.Json.Nodes;
using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    [Theory]
    [InlineData(DiabloNativeTrapDisposition.Armed)]
    [InlineData(DiabloNativeTrapDisposition.Disarmed)]
    [InlineData(DiabloNativeTrapDisposition.Fired)]
    public async Task RetainedFiringPublishesAfterTheEntireMissileLifeAndFreshOwnerRestore(DiabloNativeTrapDisposition disposition)
    {
        var missile = new BoundaryMissile(new(1, false, 48, 50, 56, 50, true, 2588, 42), 7, 0);
        if (disposition == DiabloNativeTrapDisposition.Fired) {
            for (ulong tick = 1; tick <= 256 && missile.World.Projectiles.Count != 0; tick++)
                missile.AdvanceTo(tick, 7);
            Assert.Empty(missile.World.Projectiles);
        }
        var owners = missile.Owners.Select(value => value with { Disposition = disposition,
            TrapVar4 = disposition == DiabloNativeTrapDisposition.Armed ? 0 : 1,
            TriggerTrapArmed = disposition == DiabloNativeTrapDisposition.Armed,
            FiredTick = disposition == DiabloNativeTrapDisposition.Fired ? 17UL : null }).ToArray();
        var objects = missile.Objects.Select(value => value with {
            TrapArmed = owners.Any(owner => owner.Facts.TriggerEntityId == value.EntityId && owner.TriggerTrapArmed),
        }).ToArray();
        var initial = NativeMissileCohortFixture.Create(7) with {
            Tick = 17, Objects = objects, NativeTraps = owners, Projectiles = [],
        };
        var owner = Assert.Single(owners);
        var fixture = new NativeTrapRestoreFixture(7, objects, owners, 1);
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(fixture.PrepareDoorCheckpoint(initial));
        var checkpoint = codec.Deserialize(bytes);
        fixture.Restore(checkpoint);
        Snapshot Build(ulong tick) => DiabloProtocolMapper.ToProtocolSnapshot(fixture.CapturePresentation(tick));
        await using (var host = new SnapshotHost(100, Build, _ => { })) {
            await host.ConnectAsync();
            var snapshot = await host.PollAsync(200); // No publication during flight or explosion.
            Assert.Empty(snapshot.Projectiles);
            var source = snapshot.Objects.Single(value => value.EntityId == owner.Facts.TrapEntityId);
            Assert.Equal(disposition == DiabloNativeTrapDisposition.Fired ? owner.Facts.TriggerEntityId : 0,
                source.NativeTrapFiredTriggerEntityId);
            var observed = NativeCatacombsDoorTrapObservation.Find(snapshot).ToArray();
            if (disposition == DiabloNativeTrapDisposition.Fired) {
                var fired = Assert.Single(observed);
                Assert.Equal(17UL, fired.Tick);
                Assert.True(source.HasNativeTrapFiredTick);
                Assert.Equal((7U, owner.Facts.TrapEntityId, owner.Facts.TriggerEntityId),
                    (fired.Level, fired.TrapEntity, fired.DoorEntity));
            } else Assert.Empty(observed); // An open/disarmed door is not evidence of firing.
            Assert.Null(host.Server.LastClientFailure);
        }
        var fresh = new NativeTrapRestoreFixture(7, objects, owners, 1);
        fresh.Restore(codec.Deserialize(bytes));
        await using var resumed = new SnapshotHost(200,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fresh.CapturePresentation(tick)), _ => { });
        await resumed.ConnectAsync();
        Assert.Equal(disposition == DiabloNativeTrapDisposition.Fired,
            NativeCatacombsDoorTrapObservation.Find(resumed.Model.Snapshot!).Any());
        Assert.Equal(bytes, codec.Serialize(checkpoint));
        Assert.Null(resumed.Server.LastClientFailure);
    }

    [Fact]
    public void NativeObjectTickEmitsTheMissileBeforeRetainingItsFiringThroughExpiry()
    {
        var missile = new BoundaryMissile(new(1, false, 48, 50, 56, 50, true, 2588, 42), 7, 0);
        var armed = missile.Owners.Select(value => value with { Disposition = DiabloNativeTrapDisposition.Armed,
            TrapVar4 = 0, TriggerTrapArmed = true }).ToArray();
        var objects = missile.Objects.Select(value => value with {
            TrapArmed = armed.Any(owner => owner.Facts.TriggerEntityId == value.EntityId),
        }).ToArray();
        var fixture = new NativeTrapRestoreFixture(7, objects, armed, 1);
        var checkpoint = NativeMissileCohortFixture.Create(7) with { Objects = objects, NativeTraps = armed, Projectiles = [] };
        var codec = new DiabloCohortCodec();
        fixture.Restore(codec.Deserialize(codec.Serialize(fixture.PrepareDoorCheckpoint(checkpoint))));
        Assert.Empty(fixture.CapturePresentation(0).NativeFiredTraps);
        fixture.AdvanceTo(1);
        var birth = fixture.CapturePresentation(1);
        var fired = Assert.Single(birth.NativeFiredTraps);
        Assert.Equal(1UL, fired.FiredTick);
        var shot = Assert.Single(birth.Projectiles);
        Assert.Equal((fired.Facts.TrapEntityId, fired.Facts.TriggerEntityId, "Firebolt"),
            (shot.SourceEntityId, shot.TargetEntityId, shot.NativeMissileId));
        fixture.AdvanceTo(256);
        var expired = fixture.CapturePresentation(256);
        Assert.Empty(expired.Projectiles);
        Assert.Equal(fired, Assert.Single(expired.NativeFiredTraps));
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(expired);
        AuthoritativeSnapshotValidator.Validate(wire);
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: wire));
        Assert.Single(NativeCatacombsDoorTrapObservation.Find(wire));
    }

    [Fact]
    public void LegacyFiredBytesRestoreWithoutInventingATickAndFutureFiringIsRefused()
    {
        var missile = new BoundaryMissile(new(1, false, 48, 50, 56, 50, true, 2588, 42), 7, 0);
        var fixture = new NativeTrapRestoreFixture(7, missile.Objects, missile.Owners, 1);
        var codec = new DiabloCohortCodec();
        var checkpoint = fixture.PrepareDoorCheckpoint(NativeMissileCohortFixture.Create(7) with {
            Objects = missile.Objects, NativeTraps = missile.Owners, Projectiles = [],
        });
        var oldBytes = codec.Serialize(checkpoint);
        Assert.DoesNotContain("FiredTick", oldBytes);
        var decoded = codec.Deserialize(oldBytes);
        Assert.Null(Assert.Single(decoded.NativeTraps!).FiredTick);
        fixture.Restore(decoded);
        var oldWire = DiabloProtocolMapper.ToProtocolSnapshot(fixture.CapturePresentation(100));
        var source = oldWire.Objects.Single(value => value.NativeTrapFiredTriggerEntityId != 0);
        Assert.False(source.HasNativeTrapFiredTick);
        AuthoritativeSnapshotValidator.Validate(oldWire);
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: oldWire));
        Assert.Empty(NativeCatacombsDoorTrapObservation.Find(oldWire));
        Assert.Equal(oldBytes, codec.Serialize(decoded));
        var json = JsonNode.Parse(oldBytes)!;
        json["NativeTraps"]![0]!["FiredTick"] = decoded.Tick + 1;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void FiredLinksAdmitBothWallHandsAndEverySupportedTriggerAlias()
    {
        foreach (var hand in new[] { 53, 54 }) {
            foreach (var type in new[] { 1, 2, 5, 6, 7, 42, 43 }) {
                var snapshot = new NativeMissileSnapshots(1, hand, type).Build(0);
                snapshot.Objects[0].NativeTrapFiredTriggerEntityId = snapshot.Objects[1].EntityId;
                AuthoritativeSnapshotValidator.Validate(snapshot);
                new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void ForgedFiredLinksAreRefusedByHostAndClient(int corruption)
    {
        var snapshot = new NativeMissileSnapshots(1, 53, 42).Build(0);
        var source = snapshot.Objects[0]; var trigger = snapshot.Objects[1];
        source.NativeTrapFiredTriggerEntityId = trigger.EntityId;
        AuthoritativeSnapshotValidator.Validate(snapshot);
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        switch (corruption) {
        case 0: source.NativeObjectId = 5; break;
        case 1: source.Destroyed = true; source.Breakable = true; break;
        case 2: source.NativeTrapFiredTriggerEntityId = source.EntityId; break;
        case 3: source.NativeTrapFiredTriggerEntityId = uint.MaxValue; break;
        case 4: trigger.LevelId++; break;
        case 5: trigger.NativeObjectId = 61; break;
        case 6: trigger.NativeObjectId = 68; break;
        case 7: snapshot.Objects.Remove(trigger); break;
        case 8: trigger.TrapArmed = true; break;
        case 9: source.NativeTrapFiredTick = snapshot.Tick + 1; break;
        case 10: source.NativeTrapFiredTriggerEntityId = 0; source.NativeTrapFiredTick = snapshot.Tick; break;
        }
        Assert.Multiple(
            () => Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(snapshot)),
            () => Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot))));
    }
}
