using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;
using static Devilution.Server.Tests.NativeShrineHostPublicationTests;

namespace Devilution.Server.Tests;

/** Genuine generated owners, shared item pool and the production publication/restore boundaries. */
public sealed class NativeThemeItemObjectPublicationTests
{
    public static IEnumerable<object[]> Objects => new[] {61, 62, 63, 64, 67, 77, 89, 90, 92}
        .SelectMany(type => new[] {false, true}.SelectMany(multiplayer =>
            (type == 67 ? new uint[] {2588, 0x12345678} : new uint[] {2588})
                .Select(seed => new object[] {type, multiplayer, seed})));

    [Theory]
    [MemberData(nameof(Objects))]
    public async Task NativeItemObjectLootAndOneUseStatePublishAndRestore(int type, bool multiplayer, uint seed)
    {
        foreach (var depth in new[] {1, 5, 8}) {
            var mode = multiplayer ? DiabloGameMode.Multiplayer : DiabloGameMode.SinglePlayer;
            var fixture = new NativeShrineTestFixture(depth, 0, seed, mode: mode, nativeObjectType: type);
            var hero = Player(fixture, depth);
            Snapshot Build(ulong tick) => DiabloProtocolMapper.ToProtocolSnapshot(
                fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells);
            await using var host = new NativeTrapHostPublicationTests.SnapshotHost(0, Build, _ => { });
            await host.ConnectAsync();
            var before = host.Model.Snapshot!.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId);
            Assert.True(before.NativeThemeObjectInteractable);
            Assert.True(AuthoritativeObjectSelector.HasAuthoritativeInteraction(before));
            Assert.True(fixture.Simulation.Execute(hero,
                new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
            Assert.True(fixture.Shrine.Activated);
            var drop = Assert.Single(fixture.World.WorldItems.Values);
            Assert.Equal((uint)(2 * depth), drop.State.CreateInfo & 0x3f);
            var wire = await host.PollAsync(1);
            var after = wire.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId);
            Assert.True(after.Activated);
            // Native objdat initial frames followed by the corresponding Operate* frame mutation.
            Assert.Equal(type switch {61 => 6U, 62 => 1U, 63 => 2U, 64 => 3U, 67 => 8U, _ => 2U}, after.AnimationFrame);
            Assert.False(after.NativeThemeObjectInteractable);
            Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(after));
            Assert.Single(wire.WorldItems);
            NativeThemeObjectJourneyEvidence.VerifyRetained(after, (uint)depth, (uint)type,
                NativeThemeObjectJourneyEvidence.UsedFrame(before));
            NativeThemeObjectJourneyEvidence.VerifyDrop(wire.WorldItems.Single(), (uint)depth);
            Assert.False(fixture.Simulation.Execute(hero,
                new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 1, [hero]).Succeeded);
            if (multiplayer) {
                var peer = Player(fixture, depth); peer.EntityId = 2; peer.PositionX = 51;
                Assert.False(fixture.Simulation.Execute(peer,
                    new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 1, [hero, peer]).Succeeded);
                Assert.Single(fixture.World.WorldItems);
            }
            var saved = fixture.Simulation.CaptureSnapshot(hero, 1);
            var codec = new DiabloCohortCodec();
            var bytes = codec.Serialize(Checkpoint(fixture, hero, saved, depth));
            var decoded = codec.Deserialize(bytes);
            Assert.Equal(bytes, codec.Serialize(decoded));
            var pool = decoded.CathedralItems! with {Levels = decoded.CathedralItems!.Levels
                .Where(value => value.LevelId == (uint)depth).ToArray()};
            var fresh = new NativeShrineTestFixture(depth, 0, seed, mode: mode, restoredItems: pool, nativeObjectType: type);
            var restored = Player(fresh, depth);
            fresh.Simulation.RestoreSnapshot(restored, saved with {
                Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
                WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles,
            });
            var recaptured = fresh.Simulation.CaptureSnapshot(restored, 1);
            Assert.Equal(saved.Objects, recaptured.Objects);
            Assert.Equal(saved.WorldItems, recaptured.WorldItems);
            Assert.Equal(pool.UniqueItemFlags, fresh.World.CaptureCathedralItems()!.UniqueItemFlags);
            Assert.False(fresh.Simulation.Execute(restored,
                new DiabloOperateObjectIntent(fresh.Shrine.EntityId), 2, [restored]).Succeeded);
            var restoredWire = DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 2), spells: fresh.Spells);
            Admit(restoredWire);
            NativeThemeObjectJourneyEvidence.VerifyRetained(restoredWire.Objects.Single(value => value.EntityId == after.EntityId),
                (uint)depth, (uint)type, after.AnimationFrame);
            NativeThemeObjectJourneyEvidence.VerifyDrop(restoredWire.WorldItems.Single(), (uint)depth);
            Assert.Null(host.Server.LastClientFailure);
        }
    }

    [Fact]
    public void NativeBodyGoldRetainsItsUncalculatedGroundFlagAndCorruptGoldStillFailsRestore()
    {
        var fixture = new NativeShrineTestFixture(1, 0, nativeObjectType: 67);
        var hero = Player(fixture, 1);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 1);
        var gold = Assert.Single(saved.WorldItems);
        Assert.Equal(11, gold.State.ItemType);
        Assert.False(gold.State.StatFlag); // Native GetItemAttrs leaves this false until pickup, outside level loading.
        Assert.Equal(0, gold.State.IdentifiedValue);
        Assert.True(gold.Price > 0);
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, saved, 1)));
        var pool = decoded.CathedralItems! with {Levels = decoded.CathedralItems!.Levels.Where(level => level.LevelId == 1).ToArray()};
        var fresh = new NativeShrineTestFixture(1, 0, nativeObjectType: 67, restoredItems: pool);
        var restored = Player(fresh, 1);
        saved = saved with {Player = decoded.Participants[0].Player, Objects = decoded.Objects, WorldItems = decoded.WorldItems};
        fresh.Simulation.RestoreSnapshot(restored, saved);
        Assert.False(Assert.Single(fresh.World.WorldItems.Values).State.StatFlag);
        fresh.World.CaptureCathedralItems();
        var legacyBytes = codec.Serialize(decoded with {WorldItems = [gold with {State = gold.State with {IdentifiedValue = gold.State.Value}}]});
        var legacy = codec.Deserialize(legacyBytes);
        fresh.Simulation.RestoreSnapshot(restored, saved with {WorldItems = legacy.WorldItems});
        Assert.Equal(0, Assert.Single(fresh.World.WorldItems.Values).State.IdentifiedValue);
        fresh.World.CaptureCathedralItems();
        foreach (var corrupt in new[] {
            gold with {Price = gold.Price + 1},
            gold with {State = gold.State with {Value = -1}},
            gold with {State = gold.State with {IdentifiedValue = gold.State.Value + 1}},
            gold with {EntityId = gold.EntityId + 1},
        }) Assert.Throws<InvalidDataException>(() => fresh.Simulation.RestoreSnapshot(restored, saved with {WorldItems = [corrupt]}));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    public void PreBatchBytesKeepTheRefusedBindingTagAndCanNowOperate(int depth)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectType: 62);
        var hero = Player(fixture, depth);
        var original = fixture.Simulation.CaptureSnapshot(hero, 0);
        // This is the pre-batch persisted shape: no new gameplay properties or binding-policy tag.
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, original, depth)));
        var fresh = new NativeShrineTestFixture(depth, 0, nativeObjectType: 62);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, original with {Player = decoded.Participants[0].Player,
            Objects = decoded.Objects, WorldItems = decoded.WorldItems});
        Assert.True(fresh.Simulation.Execute(restored,
            new DiabloOperateObjectIntent(fresh.Shrine.EntityId), 0, [restored]).Succeeded);
        Assert.Single(fresh.World.WorldItems);
    }

    [Theory]
    [InlineData(61)] [InlineData(64)] [InlineData(77)] [InlineData(92)]
    public void InitiallyConsumedBooksAndEmptyStandsHaveNoCapability(int type)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectType: type, nativeObjectSelectable: false);
        var hero = Player(fixture, 5);
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, 0), spells: fixture.Spells);
        var value = snapshot.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId);
        Assert.False(value.NativeThemeObjectInteractable);
        Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(value));
        Admit(snapshot);
        Assert.False(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        Assert.Empty(fixture.World.WorldItems);
    }

    [Fact]
    public void PartBEffectCapabilityMapsIntoTheSameProtocolCapability()
    {
        var fixture = new NativeShrineTestFixture(5, 0);
        var source = fixture.Simulation.CapturePresentationSnapshot(Player(fixture, 5), 0);
        source = source with {
            Objects = source.Objects.Select(value => value.EntityId == fixture.Shrine.EntityId ? value with {NativeObjectId = 80} : value).ToArray(),
            NativeShrineTypes = [], NativeShrineInteractableEntityIds = new HashSet<uint>(),
            NativeThemeEffectInteractableEntityIds = new HashSet<uint> {fixture.Shrine.EntityId},
        };
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source, spells: fixture.Spells);
        Assert.True(wire.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId).NativeThemeObjectInteractable);
        Admit(wire);
    }

    [Theory]
    [InlineData(25)] [InlineData(41)] [InlineData(52)] [InlineData(71)] [InlineData(72)]
    [InlineData(86)] [InlineData(88)] [InlineData(95)]
    public void QuestBooksAndOtherUnsupportedOwnersStayRefused(int type)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectType: type);
        var hero = Player(fixture, 5);
        Assert.False(fixture.Simulation.Execute(hero,
            new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        Assert.False(fixture.Shrine.Activated);
        Assert.Empty(fixture.World.WorldItems);
    }

    [Fact]
    public void SharedThemeCapabilityAdmitsPartBRegistrationsAndRefusesForgeries()
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectType: 64);
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(Player(fixture, 5), 0), spells: fixture.Spells);
        foreach (var type in new uint[] {61, 62, 63, 64, 66, 67, 76, 77, 79, 80, 81, 82, 89, 90, 92}) {
            var valid = snapshot.Clone(); var value = valid.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId);
            value.NativeObjectId = type;
            Admit(valid);
        }
        foreach (var corrupt in new Action<ObjectSnapshot>[] {
            value => value.NativeObjectId = 26, value => value.NativeObjectId = 91,
            value => value.NativeObjectId = 71, value => value.Activated = true,
            value => value.Destroyed = true, value => value.IsShrine = true,
            value => value.NativeChestInteractable = true, value => value.TrapArmed = true,
        }) {
            var invalid = snapshot.Clone(); corrupt(invalid.Objects.Single(value => value.EntityId == fixture.Shrine.EntityId));
            Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(invalid));
            Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    private static void Admit(Snapshot snapshot)
    {
        AuthoritativeSnapshotValidator.Validate(snapshot);
        var decoded = Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: decoded));
        Assert.NotNull(model.Snapshot);
    }
}
