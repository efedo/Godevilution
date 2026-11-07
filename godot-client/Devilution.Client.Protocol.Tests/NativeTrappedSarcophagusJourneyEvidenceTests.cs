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
        AddRelease(before, true); before.Monsters[0].Alive = false; // Dormant reservation in the baseline.
        after.Monsters.Clear(); // Production has finished death conversion and removed the combat target.
        after.Corpses.Add(new CorpseSnapshot {EntityId = ReservedSkeletonEntity, LevelId = 4, PositionX = 33, PositionY = 62});
        var checkpoint = NativeTrappedSarcophagusJourneyEvidence.Capture(before, Wire(after), SarcophagusEntity, Receipt(), 210, 8);
        Assert.Empty(after.Monsters);
        Assert.Equal(new[] {ReservedSkeletonEntity}, checkpoint.Skeletons);
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(checkpoint))!;
        var resumed = Wire(after); resumed.Objects[0].AnimationFrame = 8;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, decoded);
        before.Corpses.Add(after.Corpses[0].Clone());
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8));
    }

    [Fact]
    public void CorpseOnlyReleaseRejectsOtherIdsFloorsAndPreexistingLiveMonsters()
    {
        var before = Baseline(); var after = Fired(111);
        after.Corpses.Add(new CorpseSnapshot {EntityId = ReservedSkeletonEntity, LevelId = 4});
        foreach (var corrupt in new Action<Snapshot>[] {
            value => value.Corpses[0].EntityId++, value => value.Corpses[0].LevelId = 3,
        }) {
            var broken = after.Clone(); corrupt(broken);
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, broken,
                SarcophagusEntity, Receipt(), 210, 8));
        }
        AddRelease(before, true);
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
            SarcophagusEntity, Receipt(), 210, 8));
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
    public void SparseL4WarriorPublicationRetainsItsReservedRangedSkeletonAfterItMoves()
    {
        // Failed 200 Hz journey: open 9342, firing 9343; by tick 9414 the reserved
        // archer has walked from its (34,63) release tile to (32,65).
        var before = Baseline(); before.Tick = 9341;
        var after = Fired(9343); after.Tick = 9414; after.Objects[0].AnimationFrame = 5;
        AddRelease(after, true);
        var skeleton = after.Monsters[0]; skeleton.NativeAi = "SkeletonRanged";
        skeleton.PositionX = skeleton.FuturePositionX = 32; skeleton.PositionY = skeleton.FuturePositionY = 65;
        skeleton.HitPoints = 12; skeleton.HitPointsQ6 = 768;
        var receipt = Receipt(); receipt.ClientSequence = 128; receipt.AppliedTick = 9342;
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(Wire(before), Wire(after), SarcophagusEntity, receipt, 11342, 5);
        Assert.Equal(new[] {ReservedSkeletonEntity}, record.Skeletons);
        Assert.Empty(record.Drops);
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(record))!;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(Wire(after), decoded);
        foreach (var corrupt in new Action<MonsterSnapshot>[] {
            value => value.EntityId++, value => value.LevelId = 3,
            value => value.NativeAi = "Zombie", value => value.IsPlayerMinion = true,
        }) {
            var broken = after.Clone(); corrupt(broken.Monsters[0]);
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, broken,
                SarcophagusEntity, receipt, 11342, 5));
        }
    }

    [Fact]
    public void ReservedSkeletonInItsPublishedDeathAnimationStillProvesRelease()
    {
        var before = Baseline(); var after = Fired(111); AddRelease(after, true);
        after.Monsters[0].Alive = false; after.Monsters[0].DeathTicksRemaining = 7;
        after.Objects[0].AnimationFrame = 8;
        Assert.Empty(after.Corpses); // Corpse conversion has not occurred yet.
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(before, Wire(after), SarcophagusEntity, Receipt(), 210, 8);
        Assert.Equal(new[] {ReservedSkeletonEntity}, record.Skeletons);
        NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record);
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(Wire(after), record);
        after.Monsters[0].DeathTicksRemaining = 0;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after,
            SarcophagusEntity, Receipt(), 210, 8));
    }

    [Theory]
    [InlineData(false, 4U, 1U, 2U, 110UL, 111UL, true, "hasRecord")]
    [InlineData(true, 0U, 1U, 2U, 110UL, 111UL, true, "level")]
    [InlineData(true, 5U, 1U, 2U, 110UL, 111UL, true, "level")]
    [InlineData(true, 4U, 0U, 2U, 110UL, 111UL, true, "entity")]
    [InlineData(true, 4U, 1U, 0U, 110UL, 111UL, true, "source")]
    [InlineData(true, 4U, 1U, 1U, 110UL, 111UL, true, "source")]
    [InlineData(true, 4U, 1U, 2U, 0UL, 111UL, true, "openingTick")]
    [InlineData(true, 4U, 1U, 2U, 110UL, 110UL, true, "firingTick")]
    [InlineData(true, 4U, 1U, 2U, 110UL, 109UL, true, "firingTick")]
    [InlineData(true, 4U, 1U, 2U, 110UL, 111UL, false, "hasOutcome")]
    public void IncompleteResumeEvidenceNamesTheFailingField(bool hasRecord, uint level, uint entity, uint source,
        ulong openingTick, ulong firingTick, bool hasOutcome, string field)
    {
        var error = Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyTrappedSarcophagus(
            true, hasRecord, level, entity, source, openingTick, firingTick, hasOutcome));
        Assert.Contains(field, error.Message);
    }

    [Fact]
    public void VisibleDropProducersRejectLootWithTheirNamedReasonInCaptureAndRecordedProof()
    {
        foreach (var (kind, reason) in new[] {("death", "monster"), ("corpse", "corpse"), ("vanished", "monster"),
                ("newDeath", "monster"), ("object", "object"), ("destroyed", "object"), ("door", "object"),
                ("newObject", "object"), ("missingObject", "object"),
                ("catacombsDoor", "object"), ("inventory", "inventory"), ("hand", "inventory"),
                ("duplicates", "inventory"), ("gold", "inventory"), ("handGold", "inventory")}) {
            var before = Baseline(); var after = Fired(111);
            foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = 2;
            AddRelease(after, false);
            before.Players.Add(new PlayerSnapshot {EntityId = 1, LevelId = 2, Gold = 100,
                Inventory = {new ItemSnapshot {ItemSeed = 10, State = new ItemStateSnapshot {StableItemId = 1}}},
                HeldItem = new ItemSnapshot {ItemSeed = 20, State = new ItemStateSnapshot {StableItemId = 2, Value = 500,
                    PresentationKind = Devilution.Protocol.V1.ItemPresentationKind.Gold}}});
            before.Monsters.Add(new MonsterSnapshot {EntityId = 30, LevelId = 3, Alive = true});
            before.Objects.Add(new ObjectSnapshot {EntityId = 31, NativeObjectId = 5, LevelId = 3});
            if (kind == "duplicates") before.Players[0].Inventory.Add(before.Players[0].Inventory[0].Clone());
            if (kind == "door") {
                before.Objects[2].NativeObjectId = 1;
                before.Objects[2].CathedralDoor = new CathedralDoorSnapshot {EntityId = 31, LevelId = 3, NativeObjectId = 1};
            }
            if (kind == "catacombsDoor") { before.Objects[2].NativeObjectId = 42; before.Objects[2].AnimationFrame = 4; }
            after.Players.Add(before.Players[0].Clone()); after.Monsters.Add(before.Monsters[0].Clone());
            after.Objects.Add(before.Objects[2].Clone());
            var valid = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8);
            switch (kind) {
            case "death": after.Monsters[0].Alive = false; after.Monsters[0].DeathTicksRemaining = 7; break;
            case "corpse": after.Corpses.Add(new CorpseSnapshot {EntityId = 32, LevelId = 3}); break;
            case "vanished": after.Monsters.Clear(); break;
            case "newDeath": after.Monsters.Add(new MonsterSnapshot {EntityId = 32, LevelId = 3, DeathTicksRemaining = 7}); break;
            case "object": after.Objects[2].Activated = true; break;
            case "destroyed": after.Objects[2].Destroyed = true; break;
            case "newObject": after.Objects.Add(new ObjectSnapshot {EntityId = 32, LevelId = 3, Activated = true}); break;
            case "missingObject": after.Objects.RemoveAt(2); break;
            case "door": after.Objects[2].CathedralDoor.DoorState = 1; break;
            case "catacombsDoor": after.Objects[2].AnimationFrame = 5; break;
            case "inventory": case "duplicates": after.Players[0].Inventory.RemoveAt(0); break;
            case "hand": after.Players[0].HeldItem = null; break;
            case "gold": after.Players[0].Gold--; break;
            case "handGold": after.Players[0].HeldItem.State.Value--; break;
            }
            var error = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(
                Wire(before), Wire(after), SarcophagusEntity, Receipt(), 210, 8));
            Assert.Contains(reason, error.Message);
            var forged = valid with {After = Convert.ToBase64String(Wire(after).ToByteArray())};
            var recorded = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(forged));
            Assert.Contains(reason, recorded.Message);
        }
    }

    [Fact]
    public void DisappearingFloorItemsRemainCompetingProducersEvenWhenTheCandidateHasAnotherIdentity()
    {
        var before = Baseline(); var after = Fired(111);
        foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = 2;
        AddRelease(after, false);
        var valid = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8);
        before.WorldItems.Add(new WorldItemSnapshot {EntityId = 50, LevelId = 3, ItemSeed = 99,
            State = new ItemStateSnapshot {StableItemId = 2, CreateInfo = 6}});
        var captured = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(
            Wire(before), Wire(after), SarcophagusEntity, Receipt(), 210, 8));
        Assert.Contains("floor item 50", captured.Message);
        var recorded = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(valid with {
            Before = Convert.ToBase64String(Wire(before).ToByteArray()),
        }));
        Assert.Contains("floor item 50", recorded.Message);
    }

    [Theory]
    [InlineData("floor")] [InlineData("inventory")] [InlineData("belt")] [InlineData("hand")]
    public void NewEntityCannotReuseAnyItemIdentityVisibleBeforeOpening(string origin)
    {
        var before = Baseline(); var after = Fired(111);
        foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = 2;
        AddRelease(after, false);
        var valid = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, SarcophagusEntity, Receipt(), 210, 8);
        var drop = Assert.Single(after.WorldItems);
        var item = new ItemSnapshot {ItemSeed = drop.ItemSeed, State = drop.State.Clone()};
        item.State.PositionX = 80; item.State.PositionY = 80; item.State.Identified = true; item.State.Durability = 7;
        var player = new PlayerSnapshot {EntityId = 1, LevelId = 2};
        switch (origin) {
        case "floor":
            var old = new WorldItemSnapshot {EntityId = 50, LevelId = 2, PositionX = 80, PositionY = 80,
                ItemSeed = item.ItemSeed, State = item.State.Clone()};
            before.WorldItems.Add(old); after.WorldItems.Add(old.Clone()); // No disappearance/loss; identity alone must reject it.
            break;
        case "inventory": player.Inventory.Add(item); break;
        case "belt": player.Belt.Add(new BeltItemSnapshot {ItemSeed = item.ItemSeed, State = item.State}); break;
        case "hand": player.HeldItem = item; break;
        }
        before.Players.Add(player.Clone()); after.Players.Add(player.Clone());
        var captured = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(
            Wire(before), Wire(after), SarcophagusEntity, Receipt(), 210, 8));
        Assert.Contains("preexisting item identity", captured.Message);
        var recorded = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(valid with {
            Before = Convert.ToBase64String(Wire(before).ToByteArray()), After = Convert.ToBase64String(Wire(after).ToByteArray()),
        }));
        Assert.Contains("preexisting item identity", recorded.Message);
    }

    [Fact]
    public void MovingOwnedItemsBetweenInventoryHandBeltAndEquipmentDoesNotProduceADrop()
    {
        var before = Baseline(); var after = Fired(111);
        foreach (var value in before.Objects.Concat(after.Objects)) value.LevelId = 2;
        AddRelease(after, false);
        var item = new ItemSnapshot {ItemSeed = 10, State = new ItemStateSnapshot {StableItemId = 1}};
        before.Players.Add(new PlayerSnapshot {EntityId = 1, LevelId = 2, Inventory = {item.Clone()}, HeldItem = item.Clone(),
            Belt = {new BeltItemSnapshot {ItemSeed = 20, State = new ItemStateSnapshot {StableItemId = 2}}}});
        var player = before.Players[0].Clone(); player.Inventory.Clear(); player.HeldItem = null; player.Belt.Clear();
        player.Equipment.Add(new EquippedItemSnapshot {ItemSeed = item.ItemSeed, State = item.State.Clone()});
        player.Belt.Add(new BeltItemSnapshot {ItemSeed = item.ItemSeed, State = item.State.Clone()});
        player.Inventory.Add(new ItemSnapshot {ItemSeed = 20, State = before.Players[0].Belt[0].State.Clone()});
        after.Players.Add(player);
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(Wire(before), Wire(after), SarcophagusEntity, Receipt(), 210, 8);
        NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record);
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
