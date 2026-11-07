using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Uses the existing native item generator to supply actual GetSuperItemSpace placements. */
public sealed class NativeSarcophagusFallbackEvidenceTests
{
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(49)]
    public void ActualNativeFallbackPlacementSurvivesWireCheckpointAndEvidenceRestore(int radius)
    {
        var (before, after) = Scenario(radius);
        Assert.Equal((50 - radius, 50 - radius), (after.WorldItems[0].PositionX, after.WorldItems[0].PositionY));
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(before, Wire(after), 20, Receipt(), 210, 8);
        Assert.Equal(new uint[] {40}, record.Drops);
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(record))!;
        var resumed = Wire(after); resumed.Objects[0].AnimationFrame = 8;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(resumed, decoded);
    }

    [Fact]
    public void FallbackRejectsOtherDistantLootEvenInsideTheNativeMaximumRadius()
    {
        var (before, after) = Scenario(2);
        before.LevelGeometry[0].BlockedCells.Remove(52 * 112 + 52); // Free, but later in native scan order.
        after.WorldItems[0].PositionX = 52; after.WorldItems[0].PositionY = 52;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, 20, Receipt(), 210, 8));
        var (validBefore, validAfter) = Scenario(2);
        validBefore.LevelGeometry[0].BlockedCells.Remove(52 * 112 + 52);
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(validBefore, validAfter, 20, Receipt(), 210, 8);
        var proof = Snapshot.Parser.ParseFrom(Convert.FromBase64String(record.After));
        proof.WorldItems[0].PositionX = 52; proof.WorldItems[0].PositionY = 52;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record with {
            After = Convert.ToBase64String(proof.ToByteArray()),
        }));
    }

    [Fact]
    public void FallbackRequiresAnExhaustedThreeByThreeAndRecordedPlacementContext()
    {
        foreach (var removeContext in new[] {false, true}) {
            var (before, after) = Scenario(2);
            if (removeContext) before.LevelGeometry.Clear();
            else before.LevelGeometry[0].BlockedCells.Remove(51 * 112 + 51);
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, 20, Receipt(), 210, 8));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnrelatedNativeDropAtTheExactFallbackCellCannotProveThisOpening(bool recorded)
    {
        var (before, expected) = Scenario(2);
        var (_, unrelated) = Scenario(2, randomState: 1);
        var intendedDrop = Assert.Single(expected.WorldItems);
        var otherDrop = Assert.Single(unrelated.WorldItems);
        Assert.Equal((intendedDrop.LevelId, intendedDrop.PositionX, intendedDrop.PositionY),
            (otherDrop.LevelId, otherDrop.PositionX, otherDrop.PositionY));
        Assert.NotEqual(intendedDrop.ItemSeed, otherDrop.ItemSeed);
        var monster = new MonsterSnapshot {EntityId = 30, LevelId = 2, Alive = true, PositionX = 80, PositionY = 80};
        before.Monsters.Add(monster.Clone()); expected.Monsters.Add(monster.Clone());
        monster.Alive = false; monster.DeathTicksRemaining = 7;
        unrelated.Monsters.Add(monster);
        if (!recorded) {
            var error = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(
                before, Wire(unrelated), 20, Receipt(), 210, 8));
            Assert.Contains("monster", error.Message);
            return;
        }
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(before, expected, 20, Receipt(), 210, 8);
        var forged = Snapshot.Parser.ParseFrom(Convert.FromBase64String(record.After));
        forged.WorldItems[0] = otherDrop.Clone(); // Valid native item, same floor/cell, different generation seed.
        forged.Monsters[0] = monster.Clone();
        var failure = Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record with {
            After = Convert.ToBase64String(forged.ToByteArray()),
        }));
        Assert.Contains("monster", failure.Message);
    }

    [Fact]
    public void OnlyTheSarcophagusProducerRetainsItsNativeDropAndCarriedItemProof()
    {
        var (before, after) = Scenario(2);
        var player = new PlayerSnapshot {EntityId = 1, LevelId = 2, Life = 64, PositionX = 51, PositionY = 50,
            HeldItem = new ItemSnapshot {ItemSeed = 20, State = new ItemStateSnapshot {StableItemId = 2}},
            Inventory = {new ItemSnapshot {ItemSeed = 10, State = new ItemStateSnapshot {StableItemId = 1}}}};
        before.Players.Add(player.Clone()); after.Players.Add(player.Clone());
        before.Monsters.Add(new MonsterSnapshot {EntityId = 30, LevelId = 2, Alive = true, HitPoints = 10});
        after.Monsters.Add(before.Monsters[0].Clone()); after.Monsters[0].HitPoints = 9; // A wound produces no loot.
        before.Corpses.Add(new CorpseSnapshot {EntityId = 31, LevelId = 2}); after.Corpses.Add(before.Corpses[0].Clone());
        before.Objects.Add(new ObjectSnapshot {EntityId = 32, NativeObjectId = 8, LevelId = 2, AnimationFrame = 1});
        after.Objects.Add(before.Objects[2].Clone()); after.Objects[2].AnimationFrame = 2; // An idle candle is not operated.
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(Wire(before), Wire(after), 20, Receipt(), 210, 8);
        Assert.Equal(new uint[] {40}, record.Drops);
        foreach (var proof in new[] {record.Before, record.After}) {
            var snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(proof));
            Assert.Equal(10U, Assert.Single(Assert.Single(snapshot.Players).Inventory).ItemSeed);
            Assert.Equal(20U, Assert.Single(snapshot.Players).HeldItem.ItemSeed);
        }
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(record))!;
        after.Objects[0].AnimationFrame = 8;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(Wire(after), decoded);
    }

    [Theory]
    [InlineData("item")] [InlineData("player")] [InlineData("monster")]
    [InlineData("dying")] [InlineData("moving")] [InlineData("solid")]
    [InlineData("south")] [InlineData("large")] [InlineData("sides")]
    [InlineData("purifying")] [InlineData("murky")]
    public void NativeFallbackSkipsOccupiedAndObjectBlockedCellsAndRetainsThatProof(string blocker)
    {
        var (before, after) = Scenario(2, blocker);
        Assert.Equal((49, 48), (after.WorldItems.Single(value => value.EntityId == 40).PositionX,
            after.WorldItems.Single(value => value.EntityId == 40).PositionY));
        var record = NativeTrappedSarcophagusJourneyEvidence.Capture(Wire(before), Wire(after), 20, Receipt(), 210, 8);
        var decoded = JsonSerializer.Deserialize<NativeTrappedSarcophagusCheckpoint>(JsonSerializer.Serialize(record))!;
        NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(decoded);
        var forged = Snapshot.Parser.ParseFrom(Convert.FromBase64String(record.After));
        var drop = forged.WorldItems.Single(value => value.EntityId == 40);
        drop.PositionX = drop.PositionY = 48;
        Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.VerifyRecorded(record with {
            After = Convert.ToBase64String(forged.ToByteArray()),
        }));
    }

    [Fact]
    public void DropsOutsideTheNativeSearchOrAlreadyPresentCannotProveRelease()
    {
        foreach (var preexisting in new[] {false, true}) {
            var (before, after) = Scenario(49);
            if (preexisting) before.WorldItems.Add(after.WorldItems[0].Clone());
            else after.WorldItems[0].PositionX = after.WorldItems[0].PositionY = 0; // Radius 50.
            Assert.Throws<InvalidOperationException>(() => NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, 20, Receipt(), 210, 8));
        }
    }

    private static (Snapshot Before, Snapshot After) Scenario(int radius, string blocker = "", uint randomState = 2588)
    {
        var fixture = new NativeShrineTestFixture(2, 0);
        const int width = 112;
        var solid = Enumerable.Repeat(true, width * width).ToArray();
        solid[(50 - radius) * width + 50 - radius] = false;
        var itemRefs = new int[width * width]; var playerRefs = new int[width * width];
        var monsterRefs = new int[width * width]; var objectRefs = new int[width * width];
        var objectSolid = new bool[2]; var interactive = new bool[2];
        var before = new Snapshot {Tick = 100, Objects = {
            new ObjectSnapshot {EntityId = 20, LevelId = 2, NativeObjectId = 48, PositionX = 50, PositionY = 50,
                NativeSarcophagusInteractable = true, AnimationFrame = 1},
            new ObjectSnapshot {EntityId = 21, LevelId = 2, NativeObjectId = 54, PositionX = 50, PositionY = 44},
        }};
        if (blocker.Length != 0) {
            solid[48 * width + 49] = false;
            switch (blocker) {
            case "item":
                itemRefs[48 * width + 48] = 1;
                before.WorldItems.Add(new WorldItemSnapshot {EntityId = 30, LevelId = 2, PositionX = 48, PositionY = 48});
                break;
            case "player":
                playerRefs[48 * width + 48] = 1;
                before.Players.Add(new PlayerSnapshot {EntityId = 30, LevelId = 2, PositionX = 48, PositionY = 48, Life = 64});
                break;
            case "monster": case "dying": case "moving":
                monsterRefs[48 * width + 48] = blocker == "moving" ? -1 : 1;
                before.Monsters.Add(new MonsterSnapshot {EntityId = 30, LevelId = 2,
                    PositionX = blocker == "moving" ? 47 : 48, PositionY = 48, Alive = blocker != "dying",
                    DeathTicksRemaining = blocker == "dying" ? 3 : 0, Moving = blocker == "moving", FuturePositionX = 48, FuturePositionY = 48});
                break;
            case "solid": objectRefs[48 * width + 48] = 1; objectSolid[0] = true; break;
            case "south": case "large":
                objectRefs[49 * width + 49] = blocker == "large" ? -1 : 1; interactive[0] = true;
                before.Objects.Add(new ObjectSnapshot {EntityId = 30, LevelId = 2, NativeObjectId = 48,
                    PositionX = 49, PositionY = blocker == "large" ? 50 : 49, NativeSarcophagusInteractable = true});
                break;
            case "purifying": case "murky":
                objectRefs[50 * width + 49] = 1; objectRefs[49 * width + 49] = -1;
                objectRefs[50 * width + 48] = -1; objectRefs[49 * width + 48] = -1; interactive[0] = true;
                before.Objects.Add(new ObjectSnapshot {EntityId = 30, LevelId = 2, NativeObjectId = blocker == "purifying" ? 76U : 81U,
                    PositionX = 49, PositionY = 50, NativeThemeObjectInteractable = true});
                break;
            case "sides":
                objectRefs[48 * width + 49] = 1; objectRefs[49 * width + 48] = 2;
                interactive[0] = interactive[1] = true;
                before.Objects.Add(new ObjectSnapshot {EntityId = 30, LevelId = 2, NativeObjectId = 5,
                    PositionX = 49, PositionY = 48, NativeChestInteractable = true});
                before.Objects.Add(new ObjectSnapshot {EntityId = 31, LevelId = 2, NativeObjectId = 5,
                    PositionX = 48, PositionY = 49, NativeChestInteractable = true});
                break;
            }
        }
        var state = new AuthoritativeCathedralThemeItemWorldState(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(), Enumerable.Range(0, 127).ToArray(), 0,
            itemRefs, playerRefs, monsterRefs, objectRefs, solid, new bool[width * width], objectSolid, interactive);
        var generated = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(new(state,
            fixture.ItemProfile.ItemCatalog, new(2), randomState, 50, 50, false));
        Assert.True(generated.PlacementSucceeded);
        Assert.True(generated.UsedSuperItemSpaceFallback);
        var raw = generated.Item!;
        var geometry = new LevelGeometrySnapshot {LevelId = 2, Width = width, Height = width};
        geometry.BlockedCells.Add(Enumerable.Range(0, width * width).Where(index => solid[index]));
        if (blocker == "solid") geometry.StaticObjectBlockedCells.Add(48 * width + 48);
        before.LevelGeometry.Add(geometry);
        var after = before.Clone(); after.Tick = 112;
        after.Objects[0].Activated = true; after.Objects[0].NativeSarcophagusInteractable = false; after.Objects[0].AnimationFrame = 2;
        after.Objects[1].NativeTrapFiredTriggerEntityId = 20; after.Objects[1].NativeTrapFiredTick = 111;
        after.WorldItems.Add(new WorldItemSnapshot {EntityId = 40, LevelId = 2, PositionX = raw.PositionX, PositionY = raw.PositionY,
            ItemSeed = raw.Seed, State = new ItemStateSnapshot {StableItemId = 1, CreateInfo = raw.CreateInfo}});
        return (before, after);
    }

    private static Snapshot Wire(Snapshot value) => Snapshot.Parser.ParseFrom(value.ToByteArray());
    private static CommandResult Receipt() => new() {Status = CommandStatus.Accepted, ClientSequence = 7, AppliedTick = 110};
}
