using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/** Resumes a passed native Town cohort, opens ordinary Cathedral chests, then proves cold retention. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralNativeChestsAsync(
        Main main,
        string directory,
        string phase,
        bool visible,
        Evidence? previous)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath) && System.IO.File.Exists(profilePath),
            "Native chest acceptance requires the pinned native Cathedral profile.");
        using var profileDocument = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        var profile = profileDocument.RootElement;
        Require(profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-population.v1"
            && profile.GetProperty("nativePopulation").GetBoolean()
            && !profile.GetProperty("nativePopulationClaim").GetBoolean()
            && profile.GetProperty("populationPolicy").GetString() == "original-diablo-single-player-normal-rogue-l1-main",
            "The selected profile is not the pinned, non-claimed native L1 population.");
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("Native chest acceptance has no admitted server identity.");
        var rulesetHash = identity.RulesetIdentityHash
            ?? throw new InvalidDataException("Native chest acceptance has no admitted ruleset identity.");
        var previousEvidence = previous
            ?? throw new InvalidDataException("Native chest acceptance requires its prior checkpoint evidence.");
        Require(identity.GameMode == GameMode.SinglePlayer
            && identity.ContentManifestHash == profile.GetProperty("contentManifestHash").GetString(),
            "The native chest host did not admit the pinned content manifest.");

        var routeEvidenceDirectory = System.IO.Path.Combine(directory, phase.ToLowerInvariant());
        System.IO.Directory.CreateDirectory(routeEvidenceDirectory);

        if (phase == "CathedralNativeChestJourney") {
            Require(previousEvidence is { Passed: true, LevelId: 0 },
                "Native chest journey must resume the passed Town checkpoint.");
            using var priorDocument = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativeresume.json")));
            var priorEvidence = priorDocument.RootElement;
            var prior = priorEvidence.GetProperty("NativePopulation");
            var player = main.ObservedSnapshot!.Players.Single();
            var savedSequence = priorEvidence.GetProperty("Sequence").GetUInt64();
            var priorLife = prior.GetProperty("HealerService").GetProperty("LifeAfter").GetInt32();
            var priorMana = prior.GetProperty("RestoredBaseline").GetProperty("Mana").GetInt32();
            var priorExperience = prior.GetProperty("RestoredBaseline").GetProperty("Experience").GetUInt32();
            var priorGold = prior.GetProperty("Gold").GetUInt32();
            var priorStashGold = prior.GetProperty("StashGold").GetUInt32();
            var priorSeed = prior.GetProperty("RepairItemSeed").GetUInt32();
            var priorStableItem = prior.GetProperty("StableItemId").GetUInt32();
            var priorItemSlot = prior.GetProperty("EquipmentSlot").GetUInt32();
            var priorItemHash = prior.GetProperty("FinalItemStateSha256").GetString()
                ?? throw new InvalidDataException("The prior native Town checkpoint has no final equipment hash.");
            var priorGeometryHash = prior.GetProperty("L1GeometrySha256").GetString();
            var hello = main.ObservedInitialConnection?.ServerHello;
            Require(priorEvidence.GetProperty("Passed").GetBoolean()
                && prior.GetProperty("Passed").GetBoolean()
                && previousEvidence.Sequence == savedSequence
                && prior.GetProperty("FinalSequence").GetUInt64() == savedSequence
                && prior.GetProperty("AdapterId").GetString() == profile.GetProperty("adapterId").GetString()
                && prior.GetProperty("PopulationPolicy").GetString() == profile.GetProperty("populationPolicy").GetString()
                && prior.GetProperty("PopulationMetadataFingerprint").GetString() == profile.GetProperty("populationMetadataFingerprint").GetString()
                && prior.GetProperty("ProfileContentManifestHash").GetString() == identity.ContentManifestHash
                && hello is { ResumedSessionMatched: true, LastClientSequence: var journeyObservedSequence }
                && journeyObservedSequence == savedSequence
                && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main)
                && player.EntityId == priorEvidence.GetProperty("EntityId").GetUInt32()
                && player.CharacterClassId == 1 && player.CharacterLevel == prior.GetProperty("RestoredBaseline").GetProperty("CharacterLevel").GetUInt32()
                && player.LevelId == 0 && player.PositionX == priorEvidence.GetProperty("X").GetInt32()
                && player.PositionY == priorEvidence.GetProperty("Y").GetInt32()
                && player.Life == priorLife && player.LifeMaximum == prior.GetProperty("HealerService").GetProperty("LifeMaximum").GetInt32()
                && player.Mana == priorMana && player.ManaMaximum == prior.GetProperty("RestoredBaseline").GetProperty("ManaMaximum").GetInt32()
                && player.Experience == priorExperience && player.Gold == priorGold && player.StashGold == priorStashGold,
                "The prior passed R3 Town cohort did not restore its final identity, sequence, resources, or location.");
            var earnedItem = player.Equipment.Single(item => item.Slot == priorItemSlot
                && item.ItemSeed == priorSeed && item.State.StableItemId == priorStableItem);
            Require(HashProto(earnedItem.State) == priorItemHash
                && CountNativeItemOccurrences(main, player, priorStableItem, priorSeed) == 1,
                "The prior Town checkpoint did not retain the exact repaired native-earned equipment.");
            var retainedGeometry = main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == 1);
            Require(HashProto(retainedGeometry) == priorGeometryHash,
                "The old native L1 geometry did not restore before the chest journey began.");

            await TravelNativeCathedralRoute(main, routeEvidenceDirectory, 1);
            Require(HashProto(DepthGeometry(main, 1)) == priorGeometryHash,
                "The ordinary Town-to-L1 route changed the restored Cathedral geometry.");
            var preChestWorldItems = main.ObservedSnapshot!.WorldItems
                .Where(item => item.LevelId == 1).Select(item => item.EntityId).ToHashSet();
            var openChests = new List<NativeChestOpenEvidence>();
            var openedEntityIds = new HashSet<uint>();
            while (true) {
                var candidate = FindReachableNativeChest(main.ObservedSnapshot!, openedEntityIds);
                if (candidate is null)
                    break;
                await MoveAdjacentToNativeChest(main, candidate.EntityId);
                var chestBefore = FindNativeChest(main.ObservedSnapshot!, candidate.EntityId);
                Require(chestBefore.NativeChestInteractable && !chestBefore.Activated
                    && !chestBefore.Destroyed && !chestBefore.TrapArmed,
                    "The selected native ordinary chest lost its untrapped closed capability before operation.");
                var beforeRandomState = main.ObservedSnapshot!.RandomState;
                var beforeSequence = main.ObservedCommandResult?.ClientSequence ?? savedSequence;
                GetViewport().GuiReleaseFocus();
                await ClickWorld(main, chestBefore.PositionX, chestBefore.PositionY);
                await AcceptedAndSettled(main, beforeSequence, "Open an ordinary native Cathedral chest");
                var result = main.ObservedCommandResult!;
                var chestAfter = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == chestBefore.EntityId);
                Require(result.Status == CommandStatus.Accepted && result.RejectReason == CommandRejectReason.Unspecified
                    && chestAfter.Activated && !chestAfter.NativeChestInteractable
                    && chestAfter.AnimationFrame == checked(chestBefore.AnimationFrame + 2)
                    && chestAfter.NativeObjectId == chestBefore.NativeObjectId
                    && chestAfter.PositionX == chestBefore.PositionX && chestAfter.PositionY == chestBefore.PositionY,
                    "The ordinary chest command did not publish its one-way opened animation state.");
                var generated = main.ObservedSnapshot.WorldItems
                    .Where(item => item.LevelId == 1 && !preChestWorldItems.Contains(item.EntityId))
                    .OrderBy(item => item.EntityId)
                    .Select(CaptureNativeChestLoot).ToArray();
                openChests.Add(new NativeChestOpenEvidence(
                    chestAfter.EntityId, chestAfter.NativeObjectId, chestAfter.PositionX, chestAfter.PositionY,
                    chestBefore.AnimationFrame, chestAfter.AnimationFrame, beforeRandomState,
                    main.ObservedSnapshot.RandomState, result.ClientSequence, result.AppliedTick, generated));
                openedEntityIds.Add(chestAfter.EntityId);
                foreach (var item in generated)
                    preChestWorldItems.Add(item.EntityId);
                if (generated.Length > 0)
                    break;
            }
            Require(openChests.Count > 0 && openChests.Any(chest => chest.Loot.Length > 0),
                "No reachable ordinary untrapped Cathedral chest produced visible native loot.");
            var l1GeometryHash = HashProto(DepthGeometry(main, 1));
            Require(l1GeometryHash == priorGeometryHash,
                "Opening the ordinary native chest changed the Cathedral collision geometry.");
            var l1ItemsHash = HashNativeChestWorldItems(main.ObservedSnapshot!);
            await TravelNativeCathedralRoute(main, routeEvidenceDirectory, 0);
            var town = main.ObservedSnapshot!.Players.Single();
            Require(town.LevelId == 0 && main.ObservedSnapshot.RandomState != 0,
                "The native chest route did not return to Town with a retained gameplay RNG state.");
            var checkpoint = new NativeChestJourneyEvidence(
                profile.GetProperty("adapterId").GetString()!, profile.GetProperty("populationPolicy").GetString()!,
                profile.GetProperty("populationMetadataFingerprint").GetString()!, identity.ContentManifestHash,
                rulesetHash, savedSequence, priorSeed, priorStableItem, priorItemHash,
                town.EntityId, town.CharacterName, town.CharacterClassId, town.CharacterLevel, town.LevelId,
                town.PositionX, town.PositionY, town.Life, town.LifeMaximum, town.Mana, town.ManaMaximum,
                town.Experience, town.Gold, town.StashGold,
                HashProtoCollection(town.Inventory.OrderBy(item => item.ItemSeed)
                    .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())),
                HashProtoCollection(town.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
                HashProtoCollection(town.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
                HashProto(main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == 1)),
                l1ItemsHash, main.ObservedSnapshot.RandomState, openChests.ToArray(),
                main.ObservedCommandResult!.ClientSequence);
            Require(checkpoint.L1GeometrySha256 == priorGeometryHash,
                "The L1 geometry changed while returning the chest checkpoint to Town.");
            WriteEvidence(directory, "cathedralnativechestjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = rulesetHash, EntityId = town.EntityId,
                ClassId = town.CharacterClassId, LevelId = town.LevelId, X = town.PositionX, Y = town.PositionY,
                Sequence = checkpoint.Sequence, Visible = visible, JourneyReturnedToTown = true,
                NativeChest = checkpoint,
            });
            GD.Print($"Native Cathedral chest Town checkpoint passed; sequence={checkpoint.Sequence}; opened={openChests.Count}; visibleLoot={openChests.Sum(chest => chest.Loot.Length)}");
            return;
        }

        Require(phase == "CathedralNativeChestResume" && previousEvidence is { Passed: true, LevelId: 0 },
            "Native chest cold resume requires its Town checkpoint.");
        using var journeyDocument = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativechestjourney.json")));
        var journeyRoot = journeyDocument.RootElement;
        var expected = journeyRoot.GetProperty("NativeChest").Deserialize<NativeChestJourneyEvidence>()
            ?? throw new InvalidDataException("The native chest checkpoint is empty.");
        var resumed = main.ObservedSnapshot!.Players.Single();
        var resumeHello = main.ObservedInitialConnection?.ServerHello;
        Require(journeyRoot.GetProperty("Passed").GetBoolean()
            && previousEvidence.Sequence == expected.Sequence && expected.LevelId == 0
            && expected.AdapterId == profile.GetProperty("adapterId").GetString()
            && expected.PopulationPolicy == profile.GetProperty("populationPolicy").GetString()
            && expected.PopulationMetadataFingerprint == profile.GetProperty("populationMetadataFingerprint").GetString()
            && expected.ContentHash == identity.ContentManifestHash && expected.RulesetHash == rulesetHash
            && resumeHello is { ResumedSessionMatched: true, LastClientSequence: var resumeObservedSequence }
            && resumeObservedSequence == expected.Sequence
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main)
            && resumed.EntityId == expected.EntityId && resumed.CharacterName == expected.CharacterName
            && resumed.CharacterClassId == expected.ClassId && resumed.CharacterLevel == expected.CharacterLevel
            && resumed.LevelId == expected.LevelId && resumed.PositionX == expected.X && resumed.PositionY == expected.Y
            && resumed.Life == expected.Life && resumed.LifeMaximum == expected.LifeMaximum
            && resumed.Mana == expected.Mana && resumed.ManaMaximum == expected.ManaMaximum
            && resumed.Experience == expected.Experience && resumed.Gold == expected.Gold && resumed.StashGold == expected.StashGold
            && HashProtoCollection(resumed.Inventory.OrderBy(item => item.ItemSeed)
                .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())) == expected.InventorySha256
            && HashProtoCollection(resumed.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.BeltSha256
            && HashProtoCollection(resumed.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.EquipmentSha256
            && main.ObservedSnapshot.RandomState == expected.RandomStateAtTown,
            "Fresh-host chest resume changed the Town checkpoint, exact command cursor, items, or persisted native RNG.");
        var retainedEquipment = resumed.Equipment.Single(item => item.ItemSeed == expected.EquipmentSeed
            && item.State.StableItemId == expected.EquipmentStableItemId);
        Require(HashProto(retainedEquipment.State) == expected.EquipmentStateSha256,
            "Fresh-host chest resume changed the native-earned Town equipment.");
        Require(HashProto(main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == 1)) == expected.L1GeometrySha256,
            "Fresh-host chest resume changed the retained L1 geometry before re-entry.");

        await TravelNativeCathedralRoute(main, routeEvidenceDirectory, 1);
        Require(HashProto(DepthGeometry(main, 1)) == expected.L1GeometrySha256
            && HashNativeChestWorldItems(main.ObservedSnapshot!) == expected.L1WorldItemsSha256,
            "The native chest return route failed to retain L1 geometry or world-item projections.");
        foreach (var operation in expected.OpenedChests) {
            var chest = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == operation.EntityId);
            Require(chest.LevelId == 1 && chest.NativeObjectId == operation.NativeObjectId
                && chest.PositionX == operation.X && chest.PositionY == operation.Y
                && chest.Activated && !chest.NativeChestInteractable
                && chest.AnimationFrame == operation.AnimationFrameAfter,
                "Fresh-host restore changed an opened native chest identity or one-way animation state.");
            foreach (var loot in operation.Loot)
                VerifyNativeChestLoot(main.ObservedSnapshot, loot);
        }
        var visibleLoot = expected.OpenedChests.SelectMany(chest => chest.Loot).ToArray();
        Require(visibleLoot.Length > 0 && visibleLoot.Select(item => item.EntityId).Distinct().Count() == visibleLoot.Length,
            "The checkpoint did not retain at least one distinct chest-generated world item.");
        var targetEvidence = expected.OpenedChests.First(chest => chest.Loot.Length > 0);
        await MoveAdjacentToNativeChest(main, targetEvidence.EntityId);
        var beforeRepeat = main.ObservedSnapshot!;
        var beforeRepeatChest = beforeRepeat.Objects.Single(item => item.EntityId == targetEvidence.EntityId);
        var beforeRepeatItemsHash = HashNativeChestWorldItems(beforeRepeat);
        var beforeRepeatRandomState = beforeRepeat.RandomState;
        var beforeRepeatTick = beforeRepeat.Tick;
        var priorQueued = main.ObservedLastQueuedSequence;
        var priorResultSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        Require(!AuthoritativeObjectSelector.HasAuthoritativeInteraction(beforeRepeatChest)
            && AuthoritativeObjectSelector.SelectAt(beforeRepeat, beforeRepeatChest.PositionX, beforeRepeatChest.PositionY)?.EntityId != beforeRepeatChest.EntityId
            && !beforeRepeat.WorldItems.Any(item => item.LevelId == 1
                && item.PositionX == beforeRepeatChest.PositionX && item.PositionY == beforeRepeatChest.PositionY),
            "The repeated chest click requires an unselectable opened chest without an overlapping selectable item.");
        GetViewport().GuiReleaseFocus();
        await ClickWorld(main, beforeRepeatChest.PositionX, beforeRepeatChest.PositionY);
        var queuedAfterClick = main.ObservedLastQueuedSequence;
        string repeatDisposition;
        var repeatStatus = "NotQueued";
        var repeatRejectReason = CommandRejectReason.Unspecified.ToString();
        ulong repeatSequence = priorResultSequence;
        if (queuedAfterClick > priorQueued) {
            await Until(() => main.ObservedCommandResult is { } result
                && result.ClientSequence >= queuedAfterClick && main.ObservedPendingCommandCount == 0
                && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main),
                "The duplicate native chest request did not settle.", stageSeconds: 12);
            var repeatResult = main.ObservedCommandResult!;
            Require(queuedAfterClick == checked(priorQueued + 1)
                && repeatResult.ClientSequence == queuedAfterClick
                && (AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(repeatResult)
                    || (repeatResult.Status == CommandStatus.Rejected
                        && repeatResult.RejectReason is CommandRejectReason.NotAllowed or CommandRejectReason.InvalidTarget)),
                "The ordinary click on an opened chest did not receive a valid settled fallback receipt.");
            repeatSequence = repeatResult.ClientSequence;
            repeatStatus = repeatResult.Status.ToString();
            repeatRejectReason = repeatResult.RejectReason.ToString();
            repeatDisposition = "QueuedOrdinaryClick";
        } else {
            Require(main.ObservedCommandResult?.ClientSequence == priorResultSequence,
                "The ordinary repeat click changed the last authoritative command receipt without queuing input.");
            repeatDisposition = "ClientDidNotQueueOpenedChest";
        }
        var afterRepeat = main.ObservedSnapshot!;
        var afterRepeatChest = afterRepeat.Objects.Single(item => item.EntityId == targetEvidence.EntityId);
        var afterRepeatTick = afterRepeat.Tick;
        var repeatRandomStateCheckPassed = afterRepeatTick != beforeRepeatTick
            || afterRepeat.RandomState == beforeRepeatRandomState;
        Require(afterRepeatChest.Activated && !afterRepeatChest.NativeChestInteractable
            && !AuthoritativeObjectSelector.HasAuthoritativeInteraction(afterRepeatChest)
            && afterRepeatChest.AnimationFrame == targetEvidence.AnimationFrameAfter
            && repeatRandomStateCheckPassed
            && HashNativeChestWorldItems(afterRepeat) == beforeRepeatItemsHash,
            "A repeated open attempt changed the chest state, generated duplicate loot, or consumed RNG without advancing a world tick.");
        foreach (var loot in targetEvidence.Loot)
            VerifyNativeChestLoot(afterRepeat, loot);
        var repeatEvidence = new NativeChestRepeatEvidence(
            repeatDisposition, repeatStatus, repeatRejectReason, repeatSequence, beforeRepeatTick, afterRepeatTick,
            beforeRepeatRandomState, afterRepeat.RandomState, repeatRandomStateCheckPassed,
            HashNativeChestWorldItems(afterRepeat), afterRepeatChest.Activated, afterRepeatChest.AnimationFrame);

        await TravelNativeCathedralRoute(main, routeEvidenceDirectory, 0);
        var finalTown = main.ObservedSnapshot!.Players.Single();
        Require(finalTown.LevelId == 0 && finalTown.EntityId == expected.EntityId
            && finalTown.PositionX >= 0 && finalTown.PositionX < 112
            && finalTown.PositionY >= 0 && finalTown.PositionY < 112
            && finalTown.Life == expected.Life && finalTown.LifeMaximum == expected.LifeMaximum
            && finalTown.Mana == expected.Mana && finalTown.ManaMaximum == expected.ManaMaximum
            && finalTown.Experience == expected.Experience && finalTown.Gold == expected.Gold
            && finalTown.StashGold == expected.StashGold
            && HashProtoCollection(finalTown.Inventory.OrderBy(item => item.ItemSeed)
                .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())) == expected.InventorySha256
            && HashProtoCollection(finalTown.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.BeltSha256
            && HashProtoCollection(finalTown.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.EquipmentSha256
            && HashNativeChestWorldItems(main.ObservedSnapshot!) == expected.L1WorldItemsSha256,
            "The resumed native chest route changed Town resources/equipment or returned without the retained L1 item pool.");
        var finalEquipment = finalTown.Equipment.Single(item => item.ItemSeed == expected.EquipmentSeed
            && item.State.StableItemId == expected.EquipmentStableItemId);
        Require(HashProto(finalEquipment.State) == expected.EquipmentStateSha256
            && CountNativeItemOccurrences(main, finalTown, expected.EquipmentStableItemId, expected.EquipmentSeed) == 1,
            "The native-earned equipment changed or duplicated during the chest return route.");
        var finalIdentity = main.ObservedIdentity
            ?? throw new InvalidDataException("Native chest resume lost its admitted server identity.");
        var finalRulesetHash = finalIdentity.RulesetIdentityHash
            ?? throw new InvalidDataException("Native chest resume lost its admitted ruleset identity.");
        var finalSequence = main.ObservedCommandResult!.ClientSequence;
        var finalEvidence = new {
            Passed = true, Mode = finalIdentity.GameMode.ToString(), ContentHash = finalIdentity.ContentManifestHash,
            RulesetHash = finalRulesetHash, EntityId = finalTown.EntityId,
            ClassId = finalTown.CharacterClassId, LevelId = finalTown.LevelId, X = finalTown.PositionX, Y = finalTown.PositionY,
            Sequence = finalSequence, Visible = visible,
            NativeChest = new {
                Passed = true, ResumedSessionMatched = true, SavedSequence = expected.Sequence,
                OpenedChestsRetained = expected.OpenedChests.Length, VisibleLootRetainedExactlyOnce = visibleLoot.Length,
                L1RetentionVerified = true, L1GeometrySha256 = expected.L1GeometrySha256,
                L1WorldItemsSha256 = expected.L1WorldItemsSha256,
                RepeatClickDidNotReopen = true, RepeatDisposition = repeatEvidence.Disposition,
                RepeatClickStatus = repeatEvidence.Status, RepeatClickRejectReason = repeatEvidence.RejectReason,
                RepeatSequence = repeatEvidence.Sequence, RepeatTickBefore = repeatEvidence.TickBefore,
                RepeatTickAfter = repeatEvidence.TickAfter,
                RepeatRandomStateBefore = repeatEvidence.RandomStateBefore,
                RepeatRandomStateAfter = repeatEvidence.RandomStateAfter,
                RepeatRandomStateCheckPassed = repeatEvidence.RandomStateCheckPassed,
                RepeatWorldItemsSha256 = repeatEvidence.WorldItemsSha256,
                RepeatChestActivated = repeatEvidence.Activated, RepeatChestAnimationFrame = repeatEvidence.AnimationFrame,
                TownReturned = true, FinalSequence = finalSequence,
                FinalLife = finalTown.Life, FinalMana = finalTown.Mana, FinalExperience = finalTown.Experience,
                FinalGold = finalTown.Gold, FinalStashGold = finalTown.StashGold,
                FinalInventorySha256 = HashProtoCollection(finalTown.Inventory.OrderBy(item => item.ItemSeed)
                    .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())),
                FinalBeltSha256 = HashProtoCollection(finalTown.Belt.OrderBy(item => item.Slot)
                    .Select(item => item.ToByteArray())),
                FinalEquipmentSha256 = HashProtoCollection(finalTown.Equipment.OrderBy(item => item.Slot)
                    .Select(item => item.ToByteArray())),
                FinalEquipmentStateSha256 = HashProto(finalEquipment.State),
                FinalL1WorldItemsSha256 = HashNativeChestWorldItems(main.ObservedSnapshot!),
                FinalRandomState = main.ObservedSnapshot.RandomState,
            },
        };
        WriteEvidence(directory, "cathedralnativechestresume.json", finalEvidence);
        GD.Print($"Native Cathedral chest cold resume passed; saved={expected.Sequence}; resumed={resumeHello!.LastClientSequence}; loot={visibleLoot.Length}; repeat={repeatDisposition}");
    }

    private static NativeChestCandidate? FindReachableNativeChest(Snapshot snapshot, HashSet<uint> excluded)
    {
        var player = snapshot.Players.Single();
        var geometry = DepthGeometryFromSnapshot(snapshot, player.LevelId);
        var candidates = snapshot.Objects.Where(value => value.LevelId == player.LevelId
                && !excluded.Contains(value.EntityId) && value.NativeChestInteractable
                && AuthoritativeObjectSelector.HasAuthoritativeInteraction(value)
                && !value.Activated && !value.Destroyed && !value.TrapArmed
                && value.NativeObjectId is >= 5 and <= 7)
            .Select(value => (Object: value, Path: FindNativeChestApproachPath(snapshot, geometry, value)))
            .Where(pair => pair.Path is not null)
            .OrderBy(pair => pair.Path!.Count)
            .ThenBy(pair => pair.Object.EntityId)
            .FirstOrDefault();
        return candidates.Object is null ? null : new NativeChestCandidate(candidates.Object.EntityId, candidates.Path!);
    }

    private static LevelGeometrySnapshot DepthGeometryFromSnapshot(Snapshot snapshot, uint levelId)
        => snapshot.LevelGeometry.Single(level => level.LevelId == levelId);

    private static List<(int X, int Y)>? FindNativeChestApproachPath(
        Snapshot snapshot,
        LevelGeometrySnapshot geometry,
        ObjectSnapshot chest)
    {
        var player = snapshot.Players.Single();
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        foreach (var trigger in geometry.WarpTriggers)
            blocked.Add(trigger.TriggerY * geometry.Width + trigger.TriggerX);
        foreach (var monster in snapshot.Monsters.Where(value => value.LevelId == player.LevelId && value.Alive)) {
            blocked.Add(monster.PositionY * geometry.Width + monster.PositionX);
            if (monster.Moving)
                blocked.Add(monster.FuturePositionY * geometry.Width + monster.FuturePositionX);
        }
        var origin = player.PositionY * geometry.Width + player.PositionX;
        if ((uint)player.PositionX >= geometry.Width || (uint)player.PositionY >= geometry.Height)
            return null;
        var parents = new Dictionary<int, int> { [origin] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var cell)) {
            var x = cell % geometry.Width;
            var y = cell / geometry.Width;
            if (Math.Abs(chest.PositionX - x) + Math.Abs(chest.PositionY - y) == 1) {
                var path = new List<(int X, int Y)>();
                for (var cursor = cell; cursor != origin; cursor = parents[cursor])
                    path.Add((cursor % geometry.Width, cursor / geometry.Width));
                path.Reverse();
                return path;
            }
            foreach (var offset in CardinalOffsets) {
                var nextX = x + offset.X;
                var nextY = y + offset.Y;
                if ((uint)nextX >= geometry.Width || (uint)nextY >= geometry.Height)
                    continue;
                var next = nextY * geometry.Width + nextX;
                if (blocked.Contains(next) || parents.ContainsKey(next))
                    continue;
                parents.Add(next, cell);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private async Task MoveAdjacentToNativeChest(Main main, uint entityId,
        Func<bool, Task<bool>>? resolveObstruction = null)
    {
        while (true) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var chest = FindNativeChest(snapshot, entityId);
            if (player.LevelId == chest.LevelId
                && Math.Abs(player.PositionX - chest.PositionX) + Math.Abs(player.PositionY - chest.PositionY) == 1)
                return;
            var geometry = DepthGeometryFromSnapshot(snapshot, chest.LevelId);
            var path = FindNativeChestApproachPath(snapshot, geometry, chest);
            if (resolveObstruction is not null && await resolveObstruction(path is null))
                continue;
            Require(path is { Count: > 0 }, $"No ordinary walk path reaches adjacent to native chest {entityId}.");
            var step = path![0];
            var dx = step.X - player.PositionX;
            var dy = step.Y - player.PositionY;
            Require(Math.Abs(dx) + Math.Abs(dy) == 1,
                "The native chest route planner produced a non-cardinal ordinary walk step.");
            var key = dx switch {
                1 => Key.Right,
                -1 => Key.Left,
                _ => dy > 0 ? Key.Down : Key.Up,
            };
            var previousSequence = main.ObservedLastQueuedSequence;
            Require(main.ObservedPendingCommandCount == 0 && Settled(main)
                && (main.ObservedCommandResult is null
                    || main.ObservedCommandResult.ClientSequence == previousSequence),
                "Native chest approach requires a settled client command boundary before walking.");
            GetViewport().GuiReleaseFocus();
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            Input.FlushBufferedEvents();
            await Frame();
            var sequence = main.ObservedLastQueuedSequence;
            Require(sequence == checked(previousSequence + 1),
                "The cardinal chest-approach key did not queue exactly one fresh movement command.");
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            Input.FlushBufferedEvents();
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == sequence
                && main.ObservedLastQueuedSequence == sequence && main.ObservedPendingCommandCount == 0
                && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main),
                "The cardinal native chest approach command did not settle.", stageSeconds: 10);
            var receipt = main.ObservedCommandResult!;
            Require(receipt.Status == CommandStatus.Accepted
                && receipt.RejectReason == CommandRejectReason.Unspecified,
                $"The cardinal native chest approach was rejected: {receipt.Status}/{receipt.RejectReason}; "
                + $"step=({step.X},{step.Y}); observedTick={snapshot.Tick}; appliedTick={receipt.AppliedTick}; "
                + $"before[{DescribeNativeAttackSnapshot(snapshot, 0)}]; "
                + $"after[{DescribeNativeAttackSnapshot(main.ObservedSnapshot!, 0)}].");
            var after = main.ObservedSnapshot!.Players.Single();
            Require(after.LevelId == player.LevelId && after.PositionX == step.X && after.PositionY == step.Y,
                "The ordinary native chest approach did not reach its requested adjacent path cell.");
        }
    }

    private static ObjectSnapshot FindNativeChest(Snapshot snapshot, uint entityId)
        => snapshot.Objects.Single(value => value.EntityId == entityId && value.LevelId == 1);

    private static NativeChestLootEvidence CaptureNativeChestLoot(WorldItemSnapshot item)
        => new(item.EntityId, item.ItemSeed, item.State.StableItemId, item.State.ItemIndex,
            item.PositionX, item.PositionY, item.Price, HashProto(item.State), HashProto(item));

    private static void VerifyNativeChestLoot(Snapshot snapshot, NativeChestLootEvidence expected)
    {
        var matches = snapshot.WorldItems.Where(item => item.EntityId == expected.EntityId && item.LevelId == 1).ToArray();
        Require(matches.Length == 1, $"Native chest loot entity {expected.EntityId} was lost or duplicated after cold restore.");
        var actual = CaptureNativeChestLoot(matches[0]);
        Require(actual == expected,
            $"Native chest loot identity/state changed or duplicated for entity {expected.EntityId}.");
    }

    private static string HashNativeChestWorldItems(Snapshot snapshot)
        => HashProtoCollection(snapshot.WorldItems.Where(item => item.LevelId == 1)
            .OrderBy(item => item.EntityId).Select(item => item.ToByteArray()));

    private sealed record NativeChestCandidate(uint EntityId, List<(int X, int Y)> Path);
    private sealed record NativeChestLootEvidence(uint EntityId, uint ItemSeed, uint StableItemId, int NativeItemIndex,
        int X, int Y, uint Price, string ItemStateSha256, string WorldItemSha256);
    private sealed record NativeChestOpenEvidence(uint EntityId, uint NativeObjectId, int X, int Y,
        uint AnimationFrameBefore, uint AnimationFrameAfter, uint RandomStateBefore, uint RandomStateAfter,
        ulong Sequence, ulong AppliedTick, NativeChestLootEvidence[] Loot);
    private sealed record NativeChestJourneyEvidence(string AdapterId, string PopulationPolicy,
        string PopulationMetadataFingerprint, string ContentHash, string RulesetHash, ulong PriorTownSequence,
        uint EquipmentSeed, uint EquipmentStableItemId, string EquipmentStateSha256,
        uint EntityId, string CharacterName, uint ClassId, uint CharacterLevel, uint LevelId, int X, int Y,
        int Life, int LifeMaximum, int Mana, int ManaMaximum, uint Experience, uint Gold, uint StashGold,
        string InventorySha256, string BeltSha256, string EquipmentSha256, string L1GeometrySha256,
        string L1WorldItemsSha256, uint RandomStateAtTown, NativeChestOpenEvidence[] OpenedChests, ulong Sequence);
    private sealed record NativeChestRepeatEvidence(string Disposition, string Status, string RejectReason, ulong Sequence,
        ulong TickBefore, ulong TickAfter, uint RandomStateBefore, uint RandomStateAfter,
        bool RandomStateCheckPassed, string WorldItemsSha256, bool Activated, uint AnimationFrame);
}
