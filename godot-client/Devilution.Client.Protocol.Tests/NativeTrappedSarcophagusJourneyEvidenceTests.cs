using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTrappedSarcophagusJourneyEvidenceTests
{
    private const uint SarcophagusEntity = 0x09032071;
    private const uint WallTrapEntity = 0x09032037;
    private const uint ReservedSkeletonEntity = 0x09031006;
    [Theory]
    [InlineData(1U, 53U)] [InlineData(2U, 54U)] [InlineData(3U, 53U)] [InlineData(4U, 54U)]
    public void SparseSnapshotsRetainTheActualFiringTickAfterOpening(uint level, uint sourceType)
    {
        var before = Baseline(); var after = Fired(111);
        foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = level;
        before.Objects[1].NativeObjectId = after.Objects[1].NativeObjectId = sourceType;
        after.Tick = 500; // The missile's entire flight has elapsed.
        var opening = new NativeTrappedSarcophagusJourneyEvidence.Opening(Wire(before), SarcophagusEntity);
        var firing = Assert.IsType<NativeTrappedSarcophagusJourneyEvidence.Firing>(opening.Find(Wire(after), 110, 210));
        Assert.Equal((WallTrapEntity, 111UL), (firing.TrapSource, firing.Tick));
    }

    [Theory]
    [InlineData(109UL, false)] [InlineData(110UL, false)] [InlineData(111UL, true)]
    [InlineData(209UL, true)] [InlineData(210UL, false)]
    public void TheFiringTickMustBeStrictlyNewerThanOpeningAndInsideItsDeadline(ulong tick, bool accepted)
        => Assert.Equal(accepted, new NativeTrappedSarcophagusJourneyEvidence.Opening(Baseline(), SarcophagusEntity).Find(Fired(tick), 110, 210) is not null);

    [Fact]
    public void SpentSourcesAndPreexistingMissilesCannotBecomeFreshEvidence()
    {
        foreach (var mutate in new Action<Snapshot>[] {
            value => { value.Objects[1].NativeTrapFiredTriggerEntityId = SarcophagusEntity; value.Objects[1].NativeTrapFiredTick = 90; },
            value => value.Objects[1].NativeTrapFiredTriggerEntityId = SarcophagusEntity, // Legacy untimed fired link.
            value => value.Projectiles.Add(new ProjectileSnapshot {SourceEntityId = WallTrapEntity, TargetEntityId = SarcophagusEntity}),
        }) {
            var before = Baseline(); mutate(before);
            Assert.Null(new NativeTrappedSarcophagusJourneyEvidence.Opening(Wire(before), SarcophagusEntity).Find(Fired(111), 110, 210));
        }
    }

    [Fact]
    public void BothSidesRequireTheSameNativeIdentityAndAnUnopenedBaseline()
    {
        foreach (var mutate in new Action<ObjectSnapshot>[] {
            value => value.Activated = true, value => value.NativeSarcophagusInteractable = false,
            value => value.Destroyed = true, value => value.LevelId = 5, value => value.NativeObjectId = 91,
        }) {
            var before = Baseline(); mutate(before.Objects[0]);
            Assert.Null(new NativeTrappedSarcophagusJourneyEvidence.Opening(before, SarcophagusEntity).Find(Fired(111), 110, 210));
        }
        foreach (var mutate in new Action<Snapshot>[] {
            value => value.Objects[0].Activated = false, value => value.Objects[0].TrapArmed = true,
            value => value.Objects[0].NativeSarcophagusInteractable = true, value => value.Objects[0].PositionX++,
            value => value.Objects[0].NativeObjectId = 91, value => value.Objects[0].Destroyed = true,
            value => value.Objects[1].LevelId++, value => value.Objects[1].PositionY++, value => value.Objects[1].Destroyed = true,
            value => value.Objects[1].TrapArmed = true, value => value.Objects[1].NativeTrapInteractable = true,
            value => value.Objects[1].NativeObjectId = 55, value => value.Objects[1].NativeTrapFiredTriggerEntityId = 22,
            value => value.Objects[1].ClearNativeTrapFiredTick(), value => value.Objects[1].NativeTrapFiredTick = 501,
            value => value.Objects[0].AnimationFrame = 0, value => value.Tick = 110,
        }) {
            var after = Fired(111); mutate(after);
            Assert.Null(new NativeTrappedSarcophagusJourneyEvidence.Opening(Baseline(), SarcophagusEntity).Find(Wire(after), 110, 210));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LootOrSkeletonBirthSurvivesJsonWireAndFreshHostRetention(bool skeleton)
    {
        var before = Baseline(); var after = Fired(111);
        if (!skeleton) foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = 2;
        AddRelease(after, skeleton);
        var checkpoint = NativeTrappedSarcophagusJourneyEvidence.Capture(before, Wire(after), SarcophagusEntity, Receipt(), 210, 8);
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(checkpoint))!;
        var resumed = Wire(after); resumed.Tick = 700; resumed.Objects[0].AnimationFrame = 8;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, decoded);
        if (skeleton) {
            resumed.Monsters.Clear(); resumed.Corpses.Add(new CorpseSnapshot {EntityId = ReservedSkeletonEntity, LevelId = 4});
            NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, decoded);
            resumed.Corpses.Clear();
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, decoded));
            AddRelease(resumed, true);
        }
        foreach (var mutate in new Action<Snapshot>[] {
            value => value.Objects[0].Activated = false, value => value.Objects[0].TrapArmed = true,
            value => value.Objects[0].NativeSarcophagusInteractable = true, value => value.Objects[0].AnimationFrame--,
            value => value.Objects[1].NativeTrapFiredTick++, value => value.Objects[1].ClearNativeTrapFiredTick(),
            value => value.Objects[1].TrapArmed = true, value => value.Objects[1].NativeTrapInteractable = true,
            value => value.Objects[1].NativeTrapFiredTriggerEntityId = 0,
        }) {
            var broken = resumed.Clone(); mutate(broken);
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(broken, decoded));
        }
    }

    [Fact]
    public void OpeningWithoutReleaseOrAnAcceptedReceiptCannotProduceTheCheckpoint()
    {
        var before = Baseline(); var after = Fired(111);
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8));
        AddRelease(after, true);
        var rejected = Receipt(); rejected.Status = CommandStatus.Rejected;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, rejected, 210, 8));
        before.Monsters.Add(after.Monsters[0].Clone());
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8));
        before.Monsters[0].Alive = false; // A retained dormant reservation becoming live is a release.
        NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8);
    }

    [Fact]
    public void ANewSkeletonKilledBeforeSparsePublicationStillProvesReleaseThroughItsNativeCorpse()
    {
        var before = Baseline(); var after = Fired(111); AddRelease(after, true);
        after.Monsters[0].Alive = false; after.Corpses.Add(new CorpseSnapshot {EntityId = ReservedSkeletonEntity, LevelId = 4});
        var checkpoint = NativeTrappedSarcophagusJourneyEvidence.Capture(before, Wire(after), SarcophagusEntity, Receipt(), 210, 8);
        var resumed = Wire(after); resumed.Objects[0].AnimationFrame = 8;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, checkpoint);
        before.Corpses.Add(after.Corpses[0].Clone());
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8));
    }

    [Fact]
    public void UnrelatedLevelLootAndSkeletonsCannotProveThisSarcophagusReleasedAnything()
    {
        var before = Baseline();
        foreach (var skeleton in new[] {false, true}) {
            var after = Fired(111); AddRelease(after, skeleton);
            if (skeleton) { after.Monsters[0].EntityId++; after.Monsters[0].PositionX = 90; after.Monsters[0].PositionY = 90; }
            else { after.WorldItems[0].PositionX = 90; after.WorldItems[0].PositionY = 90; }
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
                SarcophagusEntity, Receipt(), 210, 8));
        }
    }

    [Fact]
    public void PinnedSkeletonCaseCannotBorrowNearbyLootOrAnotherObjectsSkeleton()
    {
        var before = Baseline(); var after = Fired(111); AddRelease(after, false);
        after.WorldItems[0].PositionX = 33; after.WorldItems[0].PositionY = 62;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
            SarcophagusEntity, Receipt(), 210, 8));
        after.WorldItems.Clear(); AddRelease(after, true);
        after.Monsters[0].PositionX = 33; after.Monsters[0].PositionY = 62;
        after.Monsters[0].EntityId++; // A different reservation belongs to another object.
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
            SarcophagusEntity, Receipt(), 210, 8));
    }

    [Fact]
    public void ValidSkeletonReleaseDoesNotClaimOtherLevelLootAndRejectsForgedRecordedDrops()
    {
        var before = Baseline(); var after = Fired(111); AddRelease(after, true); AddRelease(after, false);
        after.Monsters[0].PositionX = 33; after.Monsters[0].PositionY = 62;
        after.WorldItems[0].PositionX = 90; after.WorldItems[0].PositionY = 90;
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
            SarcophagusEntity, Receipt(), 210, 8);
        Assert.Empty(record.Drops);
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record with {Drops = [40]}));
    }

    [Fact]
    public void WarriorBaselineRequiresItsSpecificUnspentWallSource()
    {
        var before = Baseline();
        Assert.False(before.Objects[0].TrapArmed);
        Assert.True(NativeTrappedSarcophagusJourneyEvidence.CanOpen(before, SarcophagusEntity));
        foreach (var corrupt in new Action<Snapshot>[] {
            value => value.Objects[1].NativeTrapFiredTick = 90,
            value => value.Objects[1].NativeTrapFiredTriggerEntityId = SarcophagusEntity,
            value => value.Objects[1].PositionY++, value => value.Objects[1].NativeObjectId = 53,
            value => value.Objects.RemoveAt(1),
        }) {
            var broken = before.Clone(); corrupt(broken);
            Assert.False(NativeTrappedSarcophagusJourneyEvidence.CanOpen(broken, SarcophagusEntity));
        }
        var wrongSource = Fired(111); wrongSource.Objects[1].PositionY++;
        Assert.Null(new NativeTrappedSarcophagusJourneyEvidence.Opening(before, SarcophagusEntity).Find(wrongSource, 110, 210));
    }

    [Fact]
    public void OldCheckpointsRemainCompatibleUnlessTheNewStageIsRequired()
    {
        NativeDepthResumeEvidence.VerifyTrappedSarcophagus(false, false, 0, 0, 0, 0, 0, false);
        Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, false, 0, 0, 0, 0, 0, false));
        Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, true, 4, SarcophagusEntity, WallTrapEntity, 110, 110, true));
        Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, true, 5, SarcophagusEntity, WallTrapEntity, 110, 111, true));
        Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, true, 4, SarcophagusEntity, WallTrapEntity, 110, 111, false));
        NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, true, 4, SarcophagusEntity, WallTrapEntity, 110, 111, true);
    }

    private static Snapshot Baseline() => new() {Tick = 100, Objects = {
        new ObjectSnapshot {EntityId = SarcophagusEntity, LevelId = 4, NativeObjectId = 48, PositionX = 33, PositionY = 62,
            AnimationFrame = 1, KeepVisibleAfterActivation = true, NativeSarcophagusInteractable = true},
        new ObjectSnapshot {EntityId = WallTrapEntity, LevelId = 4, NativeObjectId = 54, PositionX = 33, PositionY = 44},
    }};
    private static Snapshot Fired(ulong tick)
    {
        var after = Baseline(); after.Tick = 500; after.Objects[0].Activated = true;
        after.Objects[0].NativeSarcophagusInteractable = false; after.Objects[0].TrapArmed = false;
        after.Objects[0].AnimationFrame = 2;
        after.Objects[1].NativeTrapFiredTriggerEntityId = SarcophagusEntity; after.Objects[1].NativeTrapFiredTick = tick;
        return after;
    }
    private static void AddRelease(Snapshot snapshot, bool skeleton)
    {
        var target = snapshot.Objects[0];
        if (skeleton) snapshot.Monsters.Add(new MonsterSnapshot {EntityId = ReservedSkeletonEntity, LevelId = target.LevelId,
            PositionX = target.PositionX, PositionY = target.PositionY, Alive = true, NativeAi = "SkeletonMelee"});
        else snapshot.WorldItems.Add(new WorldItemSnapshot {EntityId = 40, LevelId = target.LevelId, ItemSeed = 42,
            PositionX = target.PositionX + 1, PositionY = target.PositionY,
            State = new ItemStateSnapshot {StableItemId = 1, CreateInfo = 2 * target.LevelId}});
    }
    private static CommandResult Receipt() => new() {Status = CommandStatus.Accepted, ClientSequence = 7, AppliedTick = 110};
    private static Snapshot Wire(Snapshot snapshot) => Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
}
