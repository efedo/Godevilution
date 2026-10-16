using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** End-to-end ordinary-control journey for the explicitly opted-in native L1 profile. */
public partial class SessionAcceptance
{
    private const int NativeRangedMinimumThreatSpacing = 3;
    private const int NativeGearHuntMaximumReselectionYields = 12;
    private const int NativeGearHuntMaximumAttacksPerTarget = 48;
    private const int NativeGearHuntMaximumCombatWork = 576;
    private const int NativeMaximumRangedApproachCommands = 12;
    private const int NativeMaximumDoorOperations = 4;
    // Native GetItemSpace chooses an available cell from the monster-centered 3x3 area.
    private const int NativeMonsterDropPlacementRadius = 1;

    private async Task VerifyCathedralNativePopulationAsync(
        Main main,
        string directory,
        string phase,
        bool visible,
        Evidence? previous)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath) && System.IO.File.Exists(profilePath),
            "Native Cathedral acceptance requires its pinned profile file.");
        using var profileDocument = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        var profile = profileDocument.RootElement;
        Require(profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-population.v1"
            && profile.GetProperty("nativePopulation").GetBoolean()
            && !profile.GetProperty("nativePopulationClaim").GetBoolean()
            && profile.GetProperty("placementPolicy").GetString() == "none"
            && profile.GetProperty("nativeDepth").GetInt32() == 1
            && profile.GetProperty("heroClassSymbol").GetString() == "rogue"
            && profile.GetProperty("depthSeeds").EnumerateArray().Select(value => value.GetUInt32())
                .SequenceEqual(new uint[] { 2588, 2588, 1, uint.MaxValue }),
            "The supplied profile does not select the explicit, non-claimed native Rogue population.");
        Require(profile.GetProperty("levels").GetArrayLength() == 4,
            "The native Cathedral profile does not contain its four generated geometry levels.");
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("Native Cathedral acceptance has no admitted server identity.");
        Require(identity.GameMode == GameMode.SinglePlayer
            && identity.ContentManifestHash == profile.GetProperty("contentManifestHash").GetString(),
            "The admitted content manifest does not match the explicitly selected native profile.");

        if (phase == "CathedralNativeJourney") {
            Require(previous is null && main.ObservedSnapshot!.Players.Single() is {
                LevelId: 0, CharacterClassId: 1, CharacterName: "M0-Rogue",
            }, "The native journey must create the ordinary Rogue in Town.");
            await TravelNativeCathedralRoute(main, directory, 1);
            VerifyNativeCathedralGeometry(main);
            var journey = await FightForNativeEquipmentAsync(main, directory, visible);
            await TravelNativeCathedralRoute(main, directory, 0);
            var player = main.ObservedSnapshot!.Players.Single();
            var equipped = player.Equipment.Single(item => item.Slot == journey.EquipmentSlot
                && item.State.StableItemId == journey.StableItemId && item.ItemSeed == journey.ItemSeed);
            Require(player.LevelId == 0 && Settled(main) && main.ObservedPendingCommandCount == 0
                && player.Experience > journey.ExperienceBefore && equipped.State.StatFlag
                && CountNativeItemOccurrences(main, player, journey.StableItemId, journey.ItemSeed) == 1,
                "The native Town checkpoint lacks the earned, usable, exactly-once equipment item.");
            var geometryHash = HashProto(DepthGeometry(main, 1));
            var checkpoint = new NativePopulationCheckpoint(
                profile.GetProperty("adapterId").GetString()!, profile.GetProperty("populationPolicy").GetString()!,
                profile.GetProperty("populationMetadataFingerprint").GetString()!,
                identity.ContentManifestHash, identity.RulesetIdentityHash,
                player.EntityId, player.CharacterName, player.CharacterClassId, player.CharacterLevel,
                player.LevelId, player.PositionX, player.PositionY, player.Life, player.LifeMaximum,
                player.Mana, player.ManaMaximum, player.Experience, player.Gold, player.StashGold,
                journey.MonsterEntityId, journey.MonsterId, journey.DropEntityId, journey.ItemSeed,
                journey.StableItemId, journey.NativeItemIndex, journey.ItemName, journey.EquipmentSlot,
                HashProto(equipped.State), HashProtoCollection(player.Inventory.OrderBy(item => item.ItemSeed)
                    .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())),
                HashProtoCollection(player.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
                HashProtoCollection(player.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
                geometryHash, main.ObservedCommandResult!.ClientSequence);
            WriteEvidence(directory, "cathedralnativejourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = player.EntityId,
                ClassId = player.CharacterClassId, LevelId = player.LevelId, X = player.PositionX,
                Y = player.PositionY, Sequence = main.ObservedCommandResult.ClientSequence, Visible = visible,
                JourneyReturnedToTown = true,
                DoorOperations = journey.DoorOperations,
                NativePopulation = checkpoint,
            });
            GD.Print($"Native Cathedral Town checkpoint passed; sequence={checkpoint.Sequence}; actor={journey.MonsterEntityId}; loot={journey.DropEntityId}");
            return;
        }

        Require(phase == "CathedralNativeResume" && previous is { Passed: true },
            "Native Cathedral resume requires a successful native journey checkpoint.");
        using var journeyDocument = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativejourney.json")));
        var expected = journeyDocument.RootElement.GetProperty("NativePopulation").Deserialize<NativePopulationCheckpoint>()
            ?? throw new InvalidDataException("The saved native population checkpoint is empty.");
        var resumed = main.ObservedSnapshot!.Players.Single();
        Require(previous.LevelId == 0
            && previous.ContentHash == identity.ContentManifestHash
            && previous.RulesetHash == identity.RulesetIdentityHash
            && expected.AdapterId == profile.GetProperty("adapterId").GetString()
            && expected.PopulationPolicy == profile.GetProperty("populationPolicy").GetString()
            && expected.PopulationMetadataFingerprint == profile.GetProperty("populationMetadataFingerprint").GetString()
            && expected.LevelId == 0 && resumed.LevelId == 0
            && expected.EntityId == resumed.EntityId && expected.CharacterName == resumed.CharacterName
            && expected.ClassId == resumed.CharacterClassId && expected.CharacterLevel == resumed.CharacterLevel
            && expected.LevelId == resumed.LevelId && expected.X == resumed.PositionX && expected.Y == resumed.PositionY
            && expected.Life == resumed.Life && expected.LifeMaximum == resumed.LifeMaximum
            && expected.Mana == resumed.Mana && expected.ManaMaximum == resumed.ManaMaximum
            && expected.Experience == resumed.Experience && expected.Gold == resumed.Gold
            && expected.StashGold == resumed.StashGold
            && HashProtoCollection(resumed.Inventory.OrderBy(item => item.ItemSeed)
                .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())) == expected.InventorySha256
            && HashProtoCollection(resumed.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.BeltSha256
            && HashProtoCollection(resumed.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.EquipmentSha256,
            "Fresh-host native resume changed the saved Rogue identity, position, resources or inventory baseline.");
        var resumeObservation = main.ObservedInitialConnection;
        var resumeHello = resumeObservation?.ServerHello;
        Require(resumeObservation is { AdmissionCompleted: true }
            && resumeHello is { ResumedSessionMatched: true }
            && resumeHello.LastClientSequence == expected.Sequence,
            $"Fresh-host native resume ServerHello did not confirm the saved command cursor: "
            + $"saved={expected.Sequence}, observed={resumeHello?.LastClientSequence}, "
            + $"admissionCompleted={resumeObservation?.AdmissionCompleted}, "
            + $"resumedSessionMatched={resumeHello?.ResumedSessionMatched}.");
        Require(main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0
            && Settled(main),
            $"Fresh-host native resume was not input-idle before the first command: "
            + $"lastQueued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}, "
            + $"settled={Settled(main)}.");
        Require(main.ObservedSnapshot!.LevelGeometry.Any(level => level.LevelId == 1
                && HashProto(level) == expected.GeometrySha256),
            "Fresh-host native resume changed the saved L1 geometry before input.");
        var restoredBaseline = new {
            EntityId = resumed.EntityId, ClassId = resumed.CharacterClassId, CharacterLevel = resumed.CharacterLevel,
            LevelId = resumed.LevelId, X = resumed.PositionX, Y = resumed.PositionY,
            Life = resumed.Life, LifeMaximum = resumed.LifeMaximum, Mana = resumed.Mana,
            ManaMaximum = resumed.ManaMaximum, Experience = resumed.Experience, Gold = resumed.Gold,
            StashGold = resumed.StashGold,
            InventorySha256 = HashProtoCollection(resumed.Inventory.OrderBy(item => item.ItemSeed)
                .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())),
            BeltSha256 = HashProtoCollection(resumed.Belt.OrderBy(item => item.Slot)
                .Select(item => item.ToByteArray())),
            EquipmentSha256 = HashProtoCollection(resumed.Equipment.OrderBy(item => item.Slot)
                .Select(item => item.ToByteArray())),
            GeometrySha256 = HashProto(main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == 1)),
        };
        var restoredEquipment = resumed.Equipment.Single(item => item.Slot == expected.EquipmentSlot
            && item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed);
        Require(HashProto(restoredEquipment.State) == expected.ItemStateSha256
            && CountNativeItemOccurrences(main, resumed, expected.StableItemId, expected.ItemSeed) == 1,
            "Fresh-host native resume lost, changed or duplicated the earned equipment.");

        var firstReturnCommand = await RecordFirstNativeReturnMovement(main, expected.Sequence, 1);
        await TravelNativeCathedralRoute(main, directory, 1);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == 1
            && HashProto(DepthGeometry(main, 1)) == expected.GeometrySha256,
            "The ordinary Town-to-L1 revisit changed the saved native Cathedral geometry.");
        VerifyNativeL1RetainedLoot(main, expected);
        var journeyDoorOperations = journeyDocument.RootElement.GetProperty("DoorOperations")
            .Deserialize<NativeDoorOperationEvidence[]>() ?? [];
        VerifyNativeDoorOperationStates(main, journeyDoorOperations);
        var l1GeometrySha256 = HashProto(DepthGeometry(main, 1));
        await TravelNativeCathedralRoute(main, directory, 0);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == 0,
            "The ordinary reverse Cathedral route did not return the resumed Rogue to Town.");
        var healerEvidence = await UseNativeTownHealer(main);
        var stashEvidence = await RoundTripNativeEquipmentThroughStash(main, expected);
        var serviceEvidence = await InspectNativeSmithRepair(main, expected);
        var town = main.ObservedSnapshot!.Players.Single();
        var finalEquipment = town.Equipment.Single(item => item.Slot == expected.EquipmentSlot
            && item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed);
        Require(town.LevelId == 0 && !town.StashOpen && town.ActiveStoreId == 0
            && CountNativeItemOccurrences(main, town, expected.StableItemId, expected.ItemSeed) == 1,
            "The native return route did not finish in Town with one retained item.");
        var finalIdentity = main.ObservedIdentity!;
        var finalSequence = main.ObservedCommandResult!.ClientSequence;
        WriteEvidence(directory, "cathedralnativeresume.json", new {
            Passed = true, Mode = finalIdentity.GameMode.ToString(), ContentHash = finalIdentity.ContentManifestHash,
            RulesetHash = finalIdentity.RulesetIdentityHash, EntityId = town.EntityId,
            ClassId = town.CharacterClassId, LevelId = town.LevelId, X = town.PositionX, Y = town.PositionY,
            Sequence = finalSequence, Visible = visible,
            NativePopulation = new {
                Passed = true, AdapterId = expected.AdapterId, PopulationPolicy = expected.PopulationPolicy,
                PopulationMetadataFingerprint = expected.PopulationMetadataFingerprint,
                ProfileContentManifestHash = identity.ContentManifestHash,
                SavedSequence = expected.Sequence,
                SavedObservedSequence = resumeHello!.LastClientSequence,
                ResumedSessionMatched = resumeHello.ResumedSessionMatched,
                RestoredBaseline = restoredBaseline,
                FirstAcceptedReturnCommandSequence = firstReturnCommand.Sequence,
                FirstAcceptedReturnCommandAppliedTick = firstReturnCommand.AppliedTick,
                FirstAcceptedReturnCommandStatus = firstReturnCommand.Status,
                FirstAcceptedReturnCommandRejectReason = firstReturnCommand.RejectReason,
                FirstAcceptedReturnCommandX = firstReturnCommand.X,
                FirstAcceptedReturnCommandY = firstReturnCommand.Y,
                L1RetentionVerified = true,
                L1GeometrySha256 = l1GeometrySha256,
                L1RevisitLevelId = 1,
                MonsterEntityId = expected.MonsterEntityId, MonsterId = expected.MonsterId,
                CorpseRetained = true, ClaimedDropEntityId = expected.DropEntityId,
                ClaimedDropSeed = expected.ItemSeed, ClaimedLootAbsent = true,
                StableItemId = expected.StableItemId, NativeItemIndex = expected.NativeItemIndex,
                ItemName = expected.ItemName, EquipmentSlot = expected.EquipmentSlot,
                ItemStateSha256 = expected.ItemStateSha256, TownReturned = true,
                HealerService = healerEvidence,
                StashRoundTripComplete = stashEvidence.Completed, StashCell = stashEvidence.Cell,
                StashItemStateSha256 = stashEvidence.ItemStateSha256,
                FinalItemStateSha256 = HashProto(finalEquipment.State),
                Gold = town.Gold, StashGold = town.StashGold,
                SmithStoreId = serviceEvidence.StoreId, RepairQuoteReason = serviceEvidence.Reason,
                RepairQuotePrice = serviceEvidence.Price, RepairButtonDisabled = serviceEvidence.Disabled,
                RepairCommandAccepted = serviceEvidence.Repaired, FinalSequence = finalSequence,
                RepairGoldBefore = serviceEvidence.GoldBefore, RepairGoldAfter = serviceEvidence.GoldAfter,
                RepairItemSeed = serviceEvidence.ItemSeed,
                RepairItemStateBeforeSha256 = serviceEvidence.ItemStateBeforeSha256,
                RepairItemStateAfterSha256 = serviceEvidence.ItemStateAfterSha256,
                RepairDurabilityBefore = serviceEvidence.DurabilityBefore,
                RepairDurabilityAfter = serviceEvidence.DurabilityAfter,
                RepairMaximumDurability = serviceEvidence.MaximumDurability,
            },
        });
        GD.Print($"Native Cathedral fresh-host resume passed; town sequence={finalSequence}; item={expected.StableItemId}");
    }

    private static void VerifyNativeProfileMarkers(JsonElement profile)
    {
        Require(profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-population.v1"
            && profile.GetProperty("nativePopulation").GetBoolean()
            && !profile.GetProperty("nativePopulationClaim").GetBoolean()
            && profile.GetProperty("placementPolicy").GetString() == "none",
            "The selected profile is not the opt-in native population route.");
    }

    private async Task TravelNativeCathedralRoute(Main main, string directory, uint destination)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var geometry = DepthGeometry(main, player.LevelId);
        var route = geometry.WarpTriggers.SingleOrDefault(item => item.Available && item.TargetLevelId == destination)
            ?? throw new InvalidDataException($"The current level does not publish an available native route to {destination}.");
        var minimumPositions = Math.Clamp(Math.Max(Math.Abs(player.PositionX - route.TriggerX),
            Math.Abs(player.PositionY - route.TriggerY)), 1, 8);
        await TravelCathedralRoute(main, directory, route.WarpId, destination, stopEarly: false,
            minimumDistinctPositions: minimumPositions, distanceBoundedApproachDeadline: true);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == destination,
            $"The ordinary route did not reach level {destination}.");
    }

    private async Task<NativeHealerEvidence> UseNativeTownHealer(Main main)
    {
        await ApproachDeclaredDestination(main, "towner:1", distanceBoundedApproachDeadline: true);
        var destination = AuthoritativeWorldDestinationSelector.GetDestinations(main.ObservedSnapshot)
            .Single(item => item.Key == "towner:1");
        var before = main.ObservedSnapshot!.Players.Single();
        var beforeItemIdentity = CaptureNativePlayerItemIdentity(before);
        var previousSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickWorld(main, destination.X, destination.Y);
        await AcceptedAndSettled(main, previousSequence, "Receive ordinary healing from the Town Healer");

        var result = main.ObservedCommandResult;
        var after = main.ObservedSnapshot!.Players.Single();
        var afterItemIdentity = CaptureNativePlayerItemIdentity(after);
        Require(result is { Status: CommandStatus.Accepted, RejectReason: CommandRejectReason.Unspecified }
            && result.ClientSequence == checked(previousSequence + 1) && result.AppliedTick > 0
            && before.LevelId == 0 && after.LevelId == 0
            && after.Life == after.LifeMaximum && after.LifeMaximum == before.LifeMaximum
            && after.Mana == before.Mana && after.Gold == before.Gold && after.StashGold == before.StashGold
            && after.Experience == before.Experience
            && beforeItemIdentity == afterItemIdentity,
            $"The ordinary Town Healer interaction changed unsupported resources/items or did not restore life: "
            + $"receipt={result?.ClientSequence}/{result?.AppliedTick}/{result?.Status}/{result?.RejectReason}, "
            + $"life={before.Life}/{before.LifeMaximum}->{after.Life}/{after.LifeMaximum}, "
            + $"mana={before.Mana}->{after.Mana}, gold={before.Gold}->{after.Gold}, "
            + $"stashGold={before.StashGold}->{after.StashGold}, "
            + $"experience={before.Experience}->{after.Experience}.");

        return new NativeHealerEvidence(1, result!.ClientSequence, result.AppliedTick,
            result.Status.ToString(), result.RejectReason.ToString(), before.Life, after.Life,
            after.LifeMaximum, before.Mana, after.Mana, before.Gold, after.Gold, before.StashGold,
            after.StashGold, before.Experience, after.Experience, beforeItemIdentity, afterItemIdentity);
    }

    private static NativePlayerItemIdentity CaptureNativePlayerItemIdentity(PlayerSnapshot player)
        => new(
            HashProtoCollection(player.Inventory.OrderBy(item => item.ItemSeed)
                .ThenBy(item => item.State.StableItemId).Select(item => item.ToByteArray())),
            HashProtoCollection(player.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
            HashProtoCollection(player.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
            HashProtoCollection(player.Stash.OrderBy(item => item.Page).ThenBy(item => item.Cell)
                .Select(item => item.ToByteArray())),
            player.HeldItem?.ToByteArray() is { } heldItem
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(heldItem))
                : string.Empty);

    private async Task<NativeReturnMovementEvidence> RecordFirstNativeReturnMovement(
        Main main,
        ulong savedObservedSequence,
        uint destinationLevelId)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = DepthGeometry(main, player.LevelId);
        var route = geometry.WarpTriggers.SingleOrDefault(item => item.Available && item.TargetLevelId == destinationLevelId)
            ?? throw new InvalidDataException(
                $"The resumed native Town snapshot has no available route to level {destinationLevelId}.");
        var resumeObservation = main.ObservedInitialConnection;
        var resumeHello = resumeObservation?.ServerHello;
        Require(resumeObservation is { AdmissionCompleted: true }
            && resumeHello is { ResumedSessionMatched: true }
            && resumeHello.LastClientSequence == savedObservedSequence,
            $"The fresh-host resume ServerHello did not retain the saved cursor before return movement: "
            + $"saved={savedObservedSequence}, observed={resumeHello?.LastClientSequence}, "
            + $"admissionCompleted={resumeObservation?.AdmissionCompleted}, "
            + $"resumedSessionMatched={resumeHello?.ResumedSessionMatched}.");
        Require(main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0
            && Settled(main),
            $"The fresh-host resume baseline was not input-idle before return movement: "
            + $"lastQueued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}, "
            + $"settled={Settled(main)}.");

        var offsets = new (int X, int Y)[] {
            (0, 1), (1, 0), (0, -1), (-1, 0),
            (1, 1), (1, -1), (-1, -1), (-1, 1),
        };
        var startDistance = NativeDistance(player.PositionX, player.PositionY, route.TriggerX, route.TriggerY);
        var stepCandidates = offsets.Select(offset => (X: player.PositionX + offset.X, Y: player.PositionY + offset.Y))
            .Where(point => IsDepthWalkTarget(main, point.X, point.Y)
                && !geometry.StaticObjectBlockedCells.Contains(point.Y * geometry.Width + point.X)
                && !snapshot.WorldItems.Any(item => item.LevelId == player.LevelId
                    && item.PositionX == point.X && item.PositionY == point.Y)
                && !snapshot.Monsters.Any(monster => monster.LevelId == player.LevelId && monster.Alive
                    && (monster.PositionX == point.X && monster.PositionY == point.Y
                        || monster.Moving && monster.FuturePositionX == point.X && monster.FuturePositionY == point.Y)))
            .OrderBy(point => NativeDistance(point.X, point.Y, route.TriggerX, route.TriggerY))
            .ThenBy(point => point.Y).ThenBy(point => point.X)
            .ToArray();
        Require(stepCandidates.Length > 0 && NativeDistance(stepCandidates[0].X, stepCandidates[0].Y,
                route.TriggerX, route.TriggerY) < startDistance
            || stepCandidates.Length > 0 && startDistance <= 1
                && NativeDistance(stepCandidates[0].X, stepCandidates[0].Y, route.TriggerX, route.TriggerY) <= 1,
            "No safe ordinary L1 movement step advances the resumed character toward the Town route.");
        var step = stepCandidates[0];

        await Click(main.ObservedTileScreenPosition(step.X, step.Y), walk: true);
        await AcceptedAndSettled(main, savedObservedSequence, "First native Town-return movement");
        var result = main.ObservedCommandResult
            ?? throw new InvalidDataException("The first native Town-return movement has no receipt.");
        Require(result.ClientSequence == checked(savedObservedSequence + 1)
            && result.AppliedTick > 0
            && result.Status == CommandStatus.Accepted
            && result.RejectReason == CommandRejectReason.Unspecified
            && main.ObservedSnapshot!.Players.Single() is { LevelId: 0 } after
            && (after.PositionX, after.PositionY) == step,
            "The first native Town-to-L1 movement did not produce its exact next accepted receipt and Town position.");
        return new NativeReturnMovementEvidence(result.ClientSequence, result.AppliedTick,
            result.Status.ToString(), result.RejectReason.ToString(), step.X, step.Y);
    }

    private static void VerifyNativeCathedralGeometry(Main main)
    {
        var geometry = DepthGeometry(main, 1);
        Require(geometry.Width == 112 && geometry.Height == 112
            && geometry.NativePieceLayer.Count == 112 * 112
            && geometry.NativeTransparency.Count == 112 * 112
            && geometry.NativeSpecialLayer.Count == 112 * 112
            && geometry.BlockedCells.Count > 0,
            "Native population entry did not publish the complete L1 geometry projection.");
    }

    private async Task<NativeEquipmentKillEvidence> FightForNativeEquipmentAsync(
        Main main,
        string directory,
        bool visible,
        bool useCardinalLootApproach = false)
    {
        var levelId = main.ObservedSnapshot!.Players.Single().LevelId;
        var initialExperience = main.ObservedSnapshot!.Players.Single().Experience;
        var initialEligibleEntityIds = main.ObservedSnapshot.Monsters.Where(monster => monster.LevelId == levelId
                && monster.Alive && !monster.IsPlayerMinion)
            .Select(monster => monster.EntityId).ToHashSet();
        var initialEligibleActorCount = initialEligibleEntityIds.Count;
        var killed = new List<NativeKillEvidence>();
        var visited = new HashSet<uint>();
        var combatWork = new NativeCombatWorkBudget(enforceCumulativeTargetLimit: IsNativeProgressionL2(main));
        var attackRefusals = combatWork.AttackRefusals;
        var completedTargets = 0;
        var reselectionYields = 0;
        var doorApproachCommands = new Dictionary<uint, int>();
        var doorOperations = new HashSet<uint>();
        var doorOperationsEvidence = new List<NativeDoorOperationEvidence>();
        while (completedTargets < initialEligibleActorCount
            && combatWork.Used < NativeCombatWorkBudget.MaximumWork) {
            if (useCardinalLootApproach && await CollectNearbyProgressionHealing(main)) {
                combatWork.ConsumeExploration("collect native generated healing");
                await UseNativeHealingPotionIfNeeded(main);
                continue;
            }
            var player = main.ObservedSnapshot!.Players.Single();
            var snapshot = main.ObservedSnapshot!;
            var remainingTargets = snapshot.Monsters.Where(monster => monster.LevelId == levelId
                    && monster.Alive && !monster.IsPlayerMinion
                    && initialEligibleEntityIds.Contains(monster.EntityId)
                    && !visited.Contains(monster.EntityId))
                .OrderBy(monster => NativeMonsterDistance(player, monster))
                .ThenBy(monster => monster.EntityId)
                .ToArray();
            var target = remainingTargets.FirstOrDefault(monster =>
                HasNativeRangedLaneWithinApproach(main, snapshot, player,
                    monster, NativeMaximumRangedApproachCommands));
            if (target is null) {
                if (remainingTargets.Length > 0 && doorOperations.Count < NativeMaximumDoorOperations
                    && await AdvanceTowardReachableNativeDoor(main, doorApproachCommands,
                        doorOperations, doorOperationsEvidence, combatWork))
                    continue;
                throw new InvalidOperationException(
                    $"No reachable native combat target or bounded closed-door route remains: "
                    + $"completedTargets={completedTargets}/{initialEligibleActorCount} initial eligible actors, "
                    + $"reselectionYields={reselectionYields}/{NativeGearHuntMaximumReselectionYields}, "
                    + $"combatWork={combatWork.Used}/{NativeCombatWorkBudget.MaximumWork}, "
                    + $"doorOperations={doorOperations.Count}/{NativeMaximumDoorOperations}, "
                    + DescribeNativeAttackSnapshot(snapshot, 0) + "; "
                    + "remainingTargets=["
                    + string.Join(";", remainingTargets.Take(6).Select(monster =>
                        $"{monster.EntityId}:{monster.MonsterId}@{monster.PositionX},{monster.PositionY}"
                        + $"/future={monster.FuturePositionX},{monster.FuturePositionY}/moving={monster.Moving}"))
                    + "]; "
                    + DescribeNativeUnopenedDoors(snapshot, doorOperations) + "; "
                    + DescribeNativeDoorOperations(doorOperationsEvidence));
            }
            var found = await DefeatNativeTargetAsync(main, target.EntityId,
                maximumAttacks: NativeGearHuntMaximumAttacksPerTarget,
                combatWorkBudget: combatWork, collectGeneratedHealing: useCardinalLootApproach);
            if (found is null) {
                reselectionYields++;
                Require(reselectionYields <= NativeGearHuntMaximumReselectionYields,
                    $"The native gear hunt exhausted its {NativeGearHuntMaximumReselectionYields} reselection yields "
                    + $"after {completedTargets}/{initialEligibleActorCount} completed targets "
                    + $"and {combatWork.Used}/{NativeCombatWorkBudget.MaximumWork} combat-work steps; "
                    + $"last yielded target={target.EntityId}.");
                continue;
            }
            completedTargets++;
            visited.Add(found.MonsterEntityId);
            killed.Add(found);
            await CollectNearbyNativeHealingDropsAsync(main, found.DropEntityIds);
            await UseNativeHealingPotionIfNeeded(main);
            var newDropIds = found.DropEntityIds.ToHashSet();
            var gear = main.ObservedSnapshot!.WorldItems.Where(item => newDropIds.Contains(item.EntityId)
                    && item.LevelId == levelId
                    && (found.DropAttribution == "EncounterAreaCandidates"
                        ? found.EncounterCorpses.Any(corpse => corpse.LevelId == item.LevelId
                            && NativeDistance(item.PositionX, item.PositionY, corpse.PositionX, corpse.PositionY)
                                <= NativeMonsterDropPlacementRadius)
                        : NativeDistance(item.PositionX, item.PositionY, found.DeathX, found.DeathY)
                            <= NativeMonsterDropPlacementRadius)
                    && IsUsableNativeEquipment(item.State, main.ObservedSnapshot.Players.Single()))
                .OrderBy(item => item.State.EquipLocation == 3 ? 0 : item.State.EquipLocation)
                .ThenBy(item => item.EntityId).FirstOrDefault();
            if (gear is null)
                continue;

            var currentPlayer = main.ObservedSnapshot!.Players.Single();
            if (useCardinalLootApproach)
                await ApproachNativeProgressionLoot(main, gear, visited, killed, combatWork);
            else if (NativeDistance(currentPlayer.PositionX, currentPlayer.PositionY,
                    gear.PositionX, gear.PositionY) > 1)
                await ClearImmediateNativeThreats(main, gear.PositionX, gear.PositionY, visited, killed);
            var prePickupPlayer = main.ObservedSnapshot!.Players.Single();
            Require(prePickupPlayer.Life > 0, "The Rogue died before reaching the native earned equipment.");
            var pickupSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            try {
                await ClickWorld(main, gear.PositionX, gear.PositionY);
                if (useCardinalLootApproach)
                    await AcceptedAndSettled(main, pickupSequence, "Pick up the adjacent native earned equipment");
                var distance = NativeDistance(prePickupPlayer.PositionX, prePickupPlayer.PositionY, gear.PositionX, gear.PositionY);
                var pickupSeconds = (int)Math.Clamp((distance + 2) * 5, 80, 240);
                await Until(() => {
                    var snapshot = main.ObservedSnapshot!;
                    var current = snapshot.Players.Single();
                    return current.Life > 0
                        && snapshot.WorldItems.All(item => item.EntityId != gear.EntityId)
                        && current.Inventory.Any(item => item.ItemSeed == gear.ItemSeed
                            && item.State.StableItemId == gear.State.StableItemId);
                }, "The native earned equipment did not reach the Rogue through the ordinary pickup control.", pickupSeconds);
            } catch (InvalidOperationException exception) {
                throw new InvalidOperationException($"{exception.Message} Native gear pickup: "
                    + $"gear={gear.EntityId}/{gear.State.StableItemId}/{gear.ItemSeed}@{gear.PositionX},{gear.PositionY}; "
                    + $"queued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}; "
                    + $"receipt={main.ObservedCommandResult?.ClientSequence}/{main.ObservedCommandResult?.Status}/"
                    + $"{main.ObservedCommandResult?.RejectReason}; "
                    + DescribeNativeAttackSnapshot(main.ObservedSnapshot!, 0), exception);
            } finally {
                if (useCardinalLootApproach) {
                    var current = main.ObservedSnapshot!;
                    var currentHero = current.Players.Single();
                    WriteEvidence(directory, $"native-earned-pickup-{levelId}-{gear.EntityId}.json", new {
                        LevelId = levelId, gear.EntityId, gear.ItemSeed, gear.State.StableItemId,
                        StartSequence = pickupSequence, Queued = main.ObservedLastQueuedSequence,
                        Receipt = main.ObservedCommandResult, Pending = main.ObservedPendingCommandCount,
                        currentHero.PositionX, currentHero.PositionY, currentHero.Life,
                        DropPresent = current.WorldItems.Any(item => item.EntityId == gear.EntityId),
                        ExactCarriedCount = CountNativeItemOccurrences(main, currentHero, gear.State.StableItemId, gear.ItemSeed),
                    });
                }
            }
            var carried = main.ObservedSnapshot!.Players.Single().Inventory.Single(item =>
                item.ItemSeed == gear.ItemSeed && item.State.StableItemId == gear.State.StableItemId);
            var pickupResult = main.ObservedCommandResult;
            Require(pickupResult is { } acceptedPickup
                && acceptedPickup.ClientSequence > pickupSequence && IsAcceptedOrSuccessfulDuplicate(acceptedPickup)
                && carried.State.StatFlag
                && carried.State.MinimumStrength <= main.ObservedSnapshot.Players.Single().Attributes.Strength.Current
                && carried.State.MinimumMagic <= main.ObservedSnapshot.Players.Single().Attributes.Magic.Current
                && carried.State.MinimumDexterity <= main.ObservedSnapshot.Players.Single().Attributes.Dexterity.Current,
                "Pickup did not resolve the native equipment into a usable Rogue item.");
            await CollectNearbyNativeHealingDropsAsync(main, found.DropEntityIds);
            await UseNativeHealingPotionIfNeeded(main);
            carried = main.ObservedSnapshot!.Players.Single().Inventory.Single(item =>
                item.ItemSeed == gear.ItemSeed && item.State.StableItemId == gear.State.StableItemId);
            var slot = NativeEquipmentSlot(carried.State, main.ObservedSnapshot.Players.Single());
            await EquipNativeItem(main, carried, slot);
            var equipped = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == slot
                && item.ItemSeed == gear.ItemSeed && item.State.StableItemId == gear.State.StableItemId);
            Require(CountNativeItemOccurrences(main, main.ObservedSnapshot.Players.Single(), gear.State.StableItemId, gear.ItemSeed) == 1,
                "Equipping the earned native item duplicated its stable identity.");
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedralnative-earned-equipment.png"));
            return new NativeEquipmentKillEvidence(
                found.MonsterEntityId, found.MonsterId, found.ExperienceBefore, main.ObservedSnapshot.Players.Single().Experience,
                gear.EntityId, gear.ItemSeed, gear.State.StableItemId, gear.State.ItemIndex,
                string.IsNullOrWhiteSpace(equipped.State.BaseName) ? equipped.State.IdentifiedName : equipped.State.BaseName,
                slot, killed.ToArray(), doorOperationsEvidence.ToArray(), attackRefusals.ToArray());
        }
        throw new InvalidOperationException(
            $"No profile-generated usable equipment was earned within the native gear-hunt limits: "
            + $"completedTargets={completedTargets}/{initialEligibleActorCount} initial eligible actors, "
            + $"reselectionYields={reselectionYields}/{NativeGearHuntMaximumReselectionYields}, "
            + $"combatWork={combatWork.Used}/{NativeCombatWorkBudget.MaximumWork}, "
            + $"kills={killed.Count}, refusals={attackRefusals.Count}, initialExperience={initialExperience}; "
            + $"attackRefusals=[{string.Join(";", attackRefusals)}].");
    }

    private async Task<NativeKillEvidence?> DefeatNativeTargetAsync(
        Main main, uint targetId, int maximumAttacks, int? lootX = null, int? lootY = null,
        NativeCombatWorkBudget? combatWorkBudget = null, bool collectGeneratedHealing = false)
    {
        var initial = main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == targetId);
        var progressionL2 = IsNativeProgressionL2(main);
        Require(!progressionL2 || combatWorkBudget is not null,
            "Progression L2 target combat requires its bounded shared work context.");
        var initialSnapshot = main.ObservedSnapshot;
        var baseline = progressionL2
            ? combatWorkBudget!.GetTargetBaseline(targetId, initial.LevelId, initialSnapshot)
            : null;
        var experienceBefore = baseline?.ExperienceBefore ?? initialSnapshot.Players.Single().Experience;
        var playerAtStart = main.ObservedSnapshot.Players.Single();
        var intendedBow = progressionL2
            ? playerAtStart.Equipment.SingleOrDefault(item => item.Slot == 4)
            : null;
        Require(!progressionL2 || intendedBow is not null && IsNativeProgressionBowUsable(intendedBow),
            "Progression L2 native combat requires a usable equipped native bow in slot 4.");
        var startingItems = baseline?.StartingItems ?? initialSnapshot.WorldItems
            .Where(item => item.LevelId == initial.LevelId).Select(item => item.EntityId).ToHashSet();
        var startingCorpses = baseline?.StartingCorpses ?? initialSnapshot.Corpses
            .Where(corpse => corpse.LevelId == initial.LevelId).Select(corpse => corpse.EntityId).ToHashSet();
        var acceptedShots = progressionL2
            ? combatWorkBudget!.AcceptedShotsFor(targetId).ToList()
            : new List<NativeAcceptedShotEvidence>();
        var localRefusals = progressionL2
            ? combatWorkBudget!.RefusalsFor(targetId).ToList()
            : new List<NativeAttackRefusalEvidence>();
        Require(lootX.HasValue == lootY.HasValue, "Native optional-threat scope requires both loot coordinates.");
        var attacks = 0;
        while (main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == targetId && monster.Alive)) {
            if (collectGeneratedHealing && await CollectNearbyProgressionHealing(main)) {
                combatWorkBudget?.ConsumeExploration("collect native generated healing during combat");
                await UseNativeHealingPotionIfNeeded(main);
                continue;
            }
            var liveSnapshot = main.ObservedSnapshot!;
            var activeTarget = liveSnapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
            if (activeTarget is null)
                break;
            var currentPlayer = liveSnapshot.Players.Single();
            if (!IsNativeThreatWithinScope(currentPlayer, activeTarget, lootX, lootY))
                return null;
            if (HasCloserNativeThreat(main, liveSnapshot, currentPlayer, activeTarget, lootX, lootY))
                return null;
            var targetAttackLimit = Math.Min(maximumAttacks, NativeGearHuntMaximumAttacksPerTarget);
            Require(++attacks <= targetAttackLimit && currentPlayer.Life > 0,
                $"Native target {targetId} exceeded its {targetAttackLimit}-step ordinary attack budget or the Rogue died; "
                + $"refusals=[{string.Join(";", localRefusals)}].");
            combatWorkBudget?.Consume(targetId, attacks);
            await UseNativeHealingPotionIfNeeded(main);
            if (progressionL2 && !await WaitForNativeProgressionAttackReadyAsync(
                    main, targetId, initial.LevelId, intendedBow!, System.Diagnostics.Stopwatch.StartNew()))
                break;
            var attackTile = await ApproachNativeRangedTargetAsync(main, targetId, lootX, lootY);
            if (attackTile is null) {
                var remainingTarget = main.ObservedSnapshot!.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
                var remainingPlayer = main.ObservedSnapshot!.Players.Single();
                if (remainingTarget is not null && !IsNativeThreatWithinScope(remainingPlayer, remainingTarget, lootX, lootY))
                    return null;
                continue;
            }
            var beforeSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            var beforeSnapshot = main.ObservedSnapshot;
            var livePlayer = beforeSnapshot.Players.Single();
            var liveTarget = beforeSnapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
            if (liveTarget is null)
                continue;
            if (!IsNativeThreatWithinScope(livePlayer, liveTarget, lootX, lootY))
                return null;
            if (HasCloserNativeThreat(main, beforeSnapshot, livePlayer, liveTarget, lootX, lootY))
                return null;
            if (progressionL2) {
                var targetTile = ProjectedNativeTargetTile(liveTarget);
                var attackGeometry = DepthGeometry(main, livePlayer.LevelId);
                var attackBlockedCells = attackGeometry.MissileBlockedCells.ToHashSet();
                Require(targetTile == attackTile.Value
                    && CanSelectNativeRangedTarget(main, beforeSnapshot, livePlayer, liveTarget,
                        attackGeometry, targetTile)
                    && HasClearNativeRangedLane(beforeSnapshot, attackGeometry, attackBlockedCells,
                        livePlayer.LevelId, livePlayer.PositionX, livePlayer.PositionY, liveTarget, targetTile),
                    $"Native target {targetId} lost its current/future visible missile lane before dispatch: "
                    + DescribeNativeAttackSnapshot(beforeSnapshot, targetId) + "; "
                    + DescribeNativeTargetClick(main, attackGeometry, livePlayer.PositionX,
                        livePlayer.PositionY, targetTile));
            }
            Require(AuthoritativeTargetSelector.SelectCombatTarget(beforeSnapshot, livePlayer.EntityId,
                    attackTile.Value.X, attackTile.Value.Y) == targetId,
                $"Native target {targetId} stopped being the selected visible entity before the attack dispatch.");
            var clickProjection = ProjectNativeTargetClick(main, DepthGeometry(main, livePlayer.LevelId),
                livePlayer.PositionX, livePlayer.PositionY, attackTile.Value);
            Require(clickProjection.Inside,
                $"Native target {targetId} moved outside the rendered world area before attack dispatch: "
                + $"player=({livePlayer.PositionX},{livePlayer.PositionY}), target=({attackTile.Value.X},{attackTile.Value.Y}), "
                + $"screen=({clickProjection.Point.X:F1},{clickProjection.Point.Y:F1}), "
                + $"worldArea={clickProjection.Bounds}.");
            if (progressionL2) {
                var previousReceipt = main.ObservedCommandResult;
                Require(main.ObservedPendingCommandCount == 0
                    && previousReceipt is not null
                    && main.ObservedLastQueuedSequence == beforeSequence
                    && previousReceipt.ClientSequence == beforeSequence
                    && beforeSnapshot.Tick >= previousReceipt.AppliedTick
                    && livePlayer.LevelId == initial.LevelId && livePlayer.Life > 0
                    && IsNativeProgressionBowUnchanged(livePlayer, intendedBow!),
                    $"Progression L2 attack boundary changed before dispatch: {DescribeNativeAttackSnapshot(beforeSnapshot, targetId)}; "
                    + $"queued={main.ObservedLastQueuedSequence},pending={main.ObservedPendingCommandCount}, "
                    + $"receipt={main.ObservedCommandResult?.ClientSequence}/{main.ObservedCommandResult?.AppliedTick}.");
                if (!IsNativeProgressionAttackReady(livePlayer, beforeSnapshot.Tick))
                    continue;
            }
            var expectedSequence = checked(beforeSequence + 1);
            var attemptClock = progressionL2 ? System.Diagnostics.Stopwatch.StartNew() : null;
            await ClickWorld(main, attackTile.Value.X, attackTile.Value.Y, MouseButton.Right);
            if (progressionL2) {
                if (main.ObservedLastQueuedSequence != expectedSequence
                    && main.ObservedCommandResult?.ClientSequence != expectedSequence) {
                    throw new InvalidOperationException(
                        $"Progression L2 attack did not queue exact sequence {expectedSequence}: "
                        + $"queued={main.ObservedLastQueuedSequence},pending={main.ObservedPendingCommandCount}, "
                        + $"receipt={main.ObservedCommandResult?.ClientSequence}; "
                        + DescribeNativeAttackSnapshot(main.ObservedSnapshot!, targetId));
                }
                var receipt = await AwaitNativeProgressionAttackReceiptAsync(
                    main, targetId, initial.LevelId, intendedBow!, expectedSequence, attemptClock!);
                if (receipt.Status == CommandStatus.Rejected) {
                    var rejectedSnapshot = main.ObservedSnapshot!;
                    var rejectedPlayer = rejectedSnapshot.Players.Single();
                    var reason = receipt.RejectReason;
                    var targetDeadWithProof = !rejectedSnapshot.Monsters.Any(monster =>
                            monster.EntityId == targetId && monster.Alive)
                        && rejectedSnapshot.Corpses.Any(corpse => corpse.EntityId == targetId
                            && corpse.LevelId == initial.LevelId)
                        && rejectedPlayer.Experience > experienceBefore
                        && acceptedShots.Any(shot => shot.TargetEntityId == targetId);
                    var classification = targetDeadWithProof
                        ? "ResolvedByPriorAcceptedShot"
                        : reason == CommandRejectReason.NotAllowed
                            && rejectedPlayer.CombatMode == PlayerCombatMode.GotHit
                                ? "HitRecoveryObserved"
                                : "Unclassified";
                    var refusal = new NativeAttackRefusalEvidence(targetId, expectedSequence,
                        receipt.AppliedTick, reason.ToString(), rejectedSnapshot.Tick, rejectedPlayer.Life,
                        rejectedPlayer.CombatMode.ToString(), rejectedPlayer.CurrentAction?.Kind.ToString(),
                        classification);
                    localRefusals.Add(refusal);
                    combatWorkBudget!.RecordRefusal(refusal);
                    if (reason == CommandRejectReason.InvalidTarget && targetDeadWithProof)
                        break;
                    var targetStillAlive = rejectedSnapshot.Monsters.Any(monster =>
                        monster.EntityId == targetId && monster.Alive);
                    if (reason == CommandRejectReason.NotAllowed && !targetStillAlive) {
                        if (targetDeadWithProof)
                            break;
                        throw new InvalidOperationException(
                            $"Progression L2 NotAllowed refusal coincided with target loss but lacked prior-shot proof: "
                            + $"target={targetId},sequence={expectedSequence},classification={classification}; "
                            + DescribeNativeAttackSnapshot(rejectedSnapshot, targetId));
                    }
                    if (reason == CommandRejectReason.NotAllowed && rejectedPlayer.Life > 0
                        && rejectedPlayer.LevelId == initial.LevelId
                        && IsNativeProgressionBowUnchanged(rejectedPlayer, intendedBow!)) {
                        GD.Print($"Native L2 attack refusal reselected: target={targetId},sequence={expectedSequence},"
                            + $"reason={reason},classification={classification},tick={rejectedSnapshot.Tick},"
                            + $"life={rejectedPlayer.Life},mode={rejectedPlayer.CombatMode},"
                            + $"action={rejectedPlayer.CurrentAction?.Kind}; attempts={attacks}/{targetAttackLimit}.");
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Progression L2 attack refusal is not safely recoverable: target={targetId}, "
                        + $"sequence={expectedSequence},reason={reason},classification={classification}; "
                        + DescribeNativeAttackSnapshot(rejectedSnapshot, targetId)
                        + $"; recordedRefusals=[{string.Join(";", localRefusals)}].");
                }
                Require(IsAcceptedOrSuccessfulDuplicate(receipt),
                    $"Progression L2 ranged attack returned unexpected {receipt.Status}/{receipt.RejectReason} "
                    + $"for target {targetId}, sequence {expectedSequence}.");
                var acceptedShot = new NativeAcceptedShotEvidence(targetId, expectedSequence,
                    attackTile.Value.X, attackTile.Value.Y, receipt.AppliedTick);
                acceptedShots.Add(acceptedShot);
                combatWorkBudget!.RecordAcceptedShot(acceptedShot);
                if (!await WaitForNativeProgressionAttackReadyAsync(
                        main, targetId, initial.LevelId, intendedBow!, attemptClock!))
                    break;
            } else {
                try {
                    await AcceptedAndSettled(main, beforeSequence, "Native Cathedral ranged attack");
                    var legacyReceipt = main.ObservedCommandResult!;
                    acceptedShots.Add(new NativeAcceptedShotEvidence(targetId, legacyReceipt.ClientSequence,
                        attackTile.Value.X, attackTile.Value.Y, legacyReceipt.AppliedTick));
                } catch (InvalidOperationException exception) {
                    var afterSnapshot = main.ObservedSnapshot;
                    var result = main.ObservedCommandResult;
                    throw new InvalidOperationException(
                        $"{exception.Message} Native attack context: before[{DescribeNativeAttackSnapshot(beforeSnapshot, targetId)}]; "
                        + $"after[{(afterSnapshot is null ? "missing snapshot" : DescribeNativeAttackSnapshot(afterSnapshot, targetId))}]; "
                        + $"input=RightClick; selectedSpellSource={DescribeNativeSpellSourceSelection(main)}; "
                        + $"receipt=(sequence={result?.ClientSequence},appliedTick={result?.AppliedTick},"
                        + $"status={result?.Status},rejectReason={result?.RejectReason}).",
                        exception);
                }
                var playerId = main.ObservedSnapshot!.Players.Single().EntityId;
                await WaitForNativeArrowSettlementAsync(main, targetId, playerId);
            }
        }
        await Until(() => main.ObservedSnapshot!.Corpses.Any(corpse => corpse.EntityId == targetId)
            && main.ObservedSnapshot.Players.Single().Experience > experienceBefore,
            $"Native target {targetId} did not publish its corpse and ordinary XP reward.", 10);
        var corpse = main.ObservedSnapshot!.Corpses.Single(item => item.EntityId == targetId);
        Require(!progressionL2 || acceptedShots.Any(shot => shot.TargetEntityId == targetId),
            $"Native target {targetId} produced a corpse and XP without an accepted ordinary shot aimed at it.");
        var encounterCorpses = progressionL2
            ? main.ObservedSnapshot.Corpses.Where(item => item.LevelId == corpse.LevelId
                    && !startingCorpses.Contains(item.EntityId)
                    && NativeDistance(item.PositionX, item.PositionY, corpse.PositionX, corpse.PositionY)
                        <= NativeRangedMinimumThreatSpacing)
                .Select(item => new NativeEncounterCorpseEvidence(item.EntityId, item.LevelId,
                    item.PositionX, item.PositionY)).ToArray()
            : [new NativeEncounterCorpseEvidence(corpse.EntityId, corpse.LevelId,
                corpse.PositionX, corpse.PositionY)];
        var dropEntityIds = progressionL2
            ? main.ObservedSnapshot.WorldItems.Where(item => item.LevelId == corpse.LevelId
                && !startingItems.Contains(item.EntityId)
                && encounterCorpses.Any(encounterCorpse => NativeDistance(item.PositionX, item.PositionY,
                    encounterCorpse.PositionX, encounterCorpse.PositionY) <= NativeMonsterDropPlacementRadius))
                .Select(item => item.EntityId).ToArray()
            : main.ObservedSnapshot.WorldItems.Where(item => item.LevelId == corpse.LevelId
                && NativeDistance(item.PositionX, item.PositionY, corpse.PositionX, corpse.PositionY)
                    <= NativeMonsterDropPlacementRadius
                && !startingItems.Contains(item.EntityId)).Select(item => item.EntityId).ToArray();
        return new NativeKillEvidence(targetId, initial.MonsterId, experienceBefore,
            main.ObservedSnapshot!.Players.Single().Experience, corpse.PositionX, corpse.PositionY,
            progressionL2 ? acceptedShots[^1].Sequence : main.ObservedCommandResult!.ClientSequence,
            dropEntityIds, encounterCorpses,
            acceptedShots.ToArray(), localRefusals.ToArray(),
            progressionL2 ? "EncounterAreaCandidates" : "TargetCorpse");
    }

    private async Task<CommandResult> AwaitNativeProgressionAttackReceiptAsync(
        Main main, uint targetId, uint levelId, EquippedItemSnapshot intendedBow,
        ulong expectedSequence, System.Diagnostics.Stopwatch attemptClock)
    {
        const double maximumAttemptSeconds = 20;
        var failure = $"Progression L2 ranged attack {expectedSequence} for target {targetId} exceeded its 20-second bound.";
        double RemainingSeconds()
        {
            var remaining = maximumAttemptSeconds - attemptClock.Elapsed.TotalSeconds;
            Require(remaining > 0, $"{failure} {DescribeNativeHealingState(main)}.");
            return remaining;
        }

        await Until(() => {
            Require(main.ObservedLastQueuedSequence <= expectedSequence,
                $"Unexpected command overtook progression L2 attack {expectedSequence}: "
                + $"queued={main.ObservedLastQueuedSequence}; {DescribeNativeHealingState(main)}.");
            var result = main.ObservedCommandResult;
            if (result is { } observed && observed.ClientSequence > expectedSequence)
                throw new InvalidOperationException(
                    $"Progression L2 attack receipt advanced past {expectedSequence}: {observed.ClientSequence}.");
            return result?.ClientSequence == expectedSequence;
        }, failure, RemainingSeconds());
        Require(attemptClock.Elapsed.TotalSeconds <= maximumAttemptSeconds,
            $"{failure} The exact receipt poll exceeded its bound; {DescribeNativeHealingState(main)}.");

        var receipt = main.ObservedCommandResult!;
        Require(main.ObservedLastQueuedSequence == expectedSequence,
            $"Progression L2 attack receipt {expectedSequence} disagrees with queued sequence "
            + $"{main.ObservedLastQueuedSequence}.");
        await Until(() => {
            var snapshot = main.ObservedSnapshot;
            var player = snapshot?.Players.SingleOrDefault();
            Require(player is not null && player.Life > 0,
                $"The Rogue died before progression L2 attack {expectedSequence} became observable; "
                + $"{DescribeNativeHealingState(main)}.");
            Require(player!.LevelId == levelId && IsNativeProgressionBowUnchanged(player, intendedBow),
                $"The progression L2 player level or intended bow changed at attack receipt {expectedSequence}; "
                + DescribeNativeAttackSnapshot(snapshot!, targetId));
            return snapshot!.Tick >= receipt.AppliedTick && main.ObservedPendingCommandCount == 0;
        }, failure, RemainingSeconds());
        Require(attemptClock.Elapsed.TotalSeconds <= maximumAttemptSeconds,
            $"{failure} The applied-snapshot poll exceeded its bound; {DescribeNativeHealingState(main)}.");
        return receipt;
    }

    private async Task<bool> WaitForNativeProgressionAttackReadyAsync(
        Main main, uint targetId, uint levelId, EquippedItemSnapshot intendedBow,
        System.Diagnostics.Stopwatch attemptClock)
    {
        const double maximumReadySeconds = 20;
        var failure = $"Progression L2 ranged action for target {targetId} did not reach its native repeat-ready boundary.";
        while (true) {
            Require(attemptClock.Elapsed.TotalSeconds <= maximumReadySeconds,
                $"{failure} {DescribeNativeHealingState(main)}.");
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.Life > 0,
                $"The Rogue died while awaiting progression L2 ranged readiness; {DescribeNativeHealingState(main)}.");
            Require(player.LevelId == levelId && IsNativeProgressionBowUnchanged(player, intendedBow),
                $"The progression L2 level or intended bow changed while awaiting repeat readiness; "
                + DescribeNativeAttackSnapshot(snapshot, targetId));
            var result = main.ObservedCommandResult;
            var fresh = result is not null && snapshot.Tick >= result.AppliedTick;
            Require(!fresh || main.ObservedPendingCommandCount == 0,
                $"A progression L2 command remained pending at the attack readiness boundary; "
                + DescribeNativeHealingState(main));
            if (fresh && main.ObservedPendingCommandCount == 0) {
                var targetAlive = snapshot.Monsters.Any(monster => monster.EntityId == targetId && monster.Alive);
                if (!targetAlive)
                    return false;
                if (IsNativeProgressionAttackReady(player, snapshot.Tick))
                    return true;
            }

            var remaining = maximumReadySeconds - attemptClock.Elapsed.TotalSeconds;
            Require(remaining > 0,
                $"{failure} {DescribeNativeHealingState(main)}; {DescribeNativeAttackSnapshot(snapshot, targetId)}.");
            if (main.ObservedPendingCommandCount == 0 && NativeHealingIsDue(main)) {
                await UseNativeHealingPotionIfNeeded(main);
                Require(attemptClock.Elapsed.TotalSeconds <= maximumReadySeconds,
                    $"The ordinary healing command exceeded the progression L2 attack bound; "
                    + $"{DescribeNativeHealingState(main)}.");
                continue;
            }
            await Until(() => {
                var current = main.ObservedSnapshot!;
                var currentPlayer = current.Players.Single();
                Require(currentPlayer.Life > 0,
                    $"The Rogue died while awaiting progression L2 attack readiness; {DescribeNativeHealingState(main)}.");
                Require(currentPlayer.LevelId == levelId && IsNativeProgressionBowUnchanged(currentPlayer, intendedBow),
                    $"The progression L2 level or intended bow changed while awaiting repeat readiness; "
                    + DescribeNativeAttackSnapshot(current, targetId));
                if (current.Tick < (main.ObservedCommandResult?.AppliedTick ?? ulong.MaxValue)
                    || main.ObservedPendingCommandCount != 0)
                    return false;
                return !current.Monsters.Any(monster => monster.EntityId == targetId && monster.Alive)
                    || IsNativeProgressionAttackReady(currentPlayer, current.Tick)
                    || NativeHealingIsDue(main);
            }, failure, remaining);
            Require(attemptClock.Elapsed.TotalSeconds <= maximumReadySeconds,
                $"{failure} The ready poll exceeded its original bound; {DescribeNativeHealingState(main)}.");
        }
    }

    private static bool IsNativeProgressionAttackReady(PlayerSnapshot player, ulong tick)
        => player.CombatMode == PlayerCombatMode.Stand && player.CurrentAction is null
            || player.CombatMode == PlayerCombatMode.RangedAttack
                && player.CurrentAction is { Kind: PlayerActionKind.Attack, EffectApplied: true } action
                && tick >= action.ReadyTick;

    private static bool IsNativeProgressionBowUnchanged(PlayerSnapshot player, EquippedItemSnapshot intendedBow)
        => player.Equipment.Any(item => item.Slot == intendedBow.Slot
            && item.ItemSeed == intendedBow.ItemSeed
            && item.State.StableItemId == intendedBow.State.StableItemId
            && IsNativeProgressionBowUsable(item));

    private static bool IsNativeProgressionBowUsable(EquippedItemSnapshot item)
        => item.Slot == 4 && item.State is { Deleted: false, StableItemId: > 0, ItemType: 3, ItemClass: 1, StatFlag: true }
            && item.State.EquipLocation == 2;

    private async Task<(int X, int Y)?> ApproachNativeRangedTargetAsync(
        Main main, uint targetId, int? lootX = null, int? lootY = null)
    {
        const int maximumMovementCommands = NativeMaximumRangedApproachCommands;
        var unchangedMoves = 0;
        var lowHealthEscapeStepUsed = false;
        for (var movement = 0; movement <= maximumMovementCommands; movement++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var target = snapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
            if (target is null)
                return null;
            if (!IsNativeThreatWithinScope(player, target, lootX, lootY))
                return null;
            if (HasCloserNativeThreat(main, snapshot, player, target, lootX, lootY))
                return null;
            var geometry = DepthGeometry(main, player.LevelId);
            var missileBlockedCells = geometry.MissileBlockedCells.ToHashSet();
            var targetTile = ProjectedNativeTargetTile(target);
            var currentThreatClearance = NativeThreatClearance(snapshot, player.LevelId,
                player.PositionX, player.PositionY);
            var hasClearShot = CanSelectNativeRangedTarget(main, snapshot, player, target, geometry, targetTile)
                && HasClearNativeRangedLane(snapshot, geometry, missileBlockedCells, player.LevelId,
                    player.PositionX, player.PositionY, target, targetTile);
            var belowHealingThreshold = (long)player.Life * 3 <= (long)player.LifeMaximum * 2;
            if (hasClearShot
                && (currentThreatClearance >= NativeRangedMinimumThreatSpacing
                    || !belowHealingThreshold || lowHealthEscapeStepUsed
                    || movement == maximumMovementCommands))
                return targetTile;

            if (movement == maximumMovementCommands)
                break;
            var step = FindNativeRangedLaneStep(main, snapshot, player, target, geometry,
                missileBlockedCells, currentThreatClearance, maximumMovementCommands - movement);
            if (step is null && hasClearShot)
                return targetTile;
            if (step is null)
                throw new InvalidOperationException(
                    $"No bounded ordinary movement step leads to a visible, unobstructed ranged shot at native target {targetId}. "
                    + DescribeNativeAttackSnapshot(snapshot, targetId) + "; "
                    + DescribeNativeThreatSpacing(snapshot, player.LevelId, player.PositionX, player.PositionY) + "; "
                    + DescribeNativeTargetClick(main, geometry, player.PositionX, player.PositionY, targetTile));
            var isImprovingEscapeStep = false;
            if (hasClearShot) {
                var escapeClearance = NativeThreatClearance(snapshot, player.LevelId, step.Value.X, step.Value.Y);
                if (escapeClearance <= currentThreatClearance)
                    return targetTile;
                isImprovingEscapeStep = true;
            }

            var previousPosition = (X: player.PositionX, Y: player.PositionY);
            var previousSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            if (!IsNativeMovementStepInViewport(main, step.Value.X, step.Value.Y))
                throw new InvalidOperationException(
                    $"The next ordinary native movement step ({step.Value.X},{step.Value.Y}) is outside the current viewport. "
                    + DescribeNativeAttackSnapshot(snapshot, targetId));
            await Click(main.ObservedTileScreenPosition(step.Value.X, step.Value.Y), walk: true);
            await AcceptedAndSettled(main, previousSequence, "Take one ordinary step toward a native ranged lane");
            await UseNativeHealingPotionIfNeeded(main);
            var after = main.ObservedSnapshot!.Players.Single();
            var settledPosition = (after.PositionX, after.PositionY);
            if (settledPosition == step.Value) {
                unchangedMoves = 0;
                if (isImprovingEscapeStep)
                    lowHealthEscapeStepUsed = true;
            } else if (settledPosition == previousPosition) {
                unchangedMoves++;
            } else {
                throw new InvalidOperationException(
                    $"An ordinary native approach step settled at ({after.PositionX},{after.PositionY}), "
                    + $"outside requested step ({step.Value.X},{step.Value.Y}) from "
                    + $"({previousPosition.X},{previousPosition.Y}); "
                    + DescribeNativeAttackSnapshot(main.ObservedSnapshot, targetId));
            }
            if (unchangedMoves >= 3)
                throw new InvalidOperationException(
                    $"Three accepted ordinary approach commands made no position progress toward native target {targetId}; "
                    + $"requested=({step.Value.X},{step.Value.Y}), "
                    + $"settled=({after.PositionX},{after.PositionY}). "
                    + DescribeNativeAttackSnapshot(main.ObservedSnapshot, targetId));
        }

        var finalSnapshot = main.ObservedSnapshot!;
        var finalPlayer = finalSnapshot.Players.Single();
        var finalTarget = finalSnapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
        var finalClick = finalTarget is null
            ? "target is no longer alive"
            : DescribeNativeTargetClick(main, DepthGeometry(main, finalPlayer.LevelId),
                finalPlayer.PositionX, finalPlayer.PositionY, ProjectedNativeTargetTile(finalTarget));
        throw new InvalidOperationException(
            $"Native target {targetId} did not enter a selectable, clear ranged lane after {maximumMovementCommands} ordinary movement commands. "
            + DescribeNativeAttackSnapshot(finalSnapshot, targetId) + "; " + finalClick);
    }

    private static (int X, int Y) ProjectedNativeTargetTile(MonsterSnapshot target)
        => target.Moving
            ? (target.FuturePositionX, target.FuturePositionY)
            : (target.PositionX, target.PositionY);

    private static bool CanSelectNativeRangedTarget(
        Main main,
        Snapshot snapshot,
        PlayerSnapshot player,
        MonsterSnapshot target,
        LevelGeometrySnapshot geometry,
        (int X, int Y) targetTile)
    {
        var radius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return AuthoritativeMonsterPresentationVisibility.IsVisible(target, player.LevelId)
            && AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId, target.PositionX, target.PositionY,
                player.LevelId, geometry, player.PositionX, player.PositionY, radius)
            && (!target.Moving || AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId,
                target.FuturePositionX, target.FuturePositionY,
                player.LevelId, geometry, player.PositionX, player.PositionY, radius))
            && AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId, targetTile.X, targetTile.Y,
                player.LevelId, geometry, player.PositionX, player.PositionY, radius)
            && ProjectNativeTargetClick(main, geometry, player.PositionX, player.PositionY, targetTile).Inside
            && AuthoritativeTargetSelector.SelectCombatTarget(snapshot, player.EntityId,
                targetTile.X, targetTile.Y) == target.EntityId;
    }

    private static (int X, int Y)? FindNativeRangedLaneStep(
        Main main,
        Snapshot snapshot,
        PlayerSnapshot player,
        MonsterSnapshot target,
        LevelGeometrySnapshot geometry,
        IReadOnlySet<int> missileBlockedCells,
        long currentThreatClearance,
        int maximumSteps)
    {
        var start = (X: player.PositionX, Y: player.PositionY);
        var queue = new Queue<(int X, int Y, int Steps)>();
        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        var clearances = new Dictionary<(int X, int Y), long> { [start] = currentThreatClearance };
        var firstSteps = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var visited = new HashSet<(int X, int Y)> { start };
        queue.Enqueue((start.X, start.Y, 0));
        NativeRangedLaneCandidate? safestLane = null;
        NativeRangedLaneCandidate? nearestLane = null;
        var offsets = new (int X, int Y)[] {
            (0, 1), (1, 0), (0, -1), (-1, 0),
            (1, 1), (1, -1), (-1, -1), (-1, 1),
        };

        while (queue.TryDequeue(out var current)) {
            var targetTile = ProjectedNativeTargetTile(target);
            if (current.Steps > 0
                && CanSeeNativeRangedLane(player, target, geometry,
                    current.X, current.Y, targetTile)
                && ProjectNativeTargetClick(main, geometry, current.X, current.Y, targetTile).Inside
                && HasClearNativeRangedLane(snapshot, geometry, missileBlockedCells, player.LevelId,
                    current.X, current.Y, target, targetTile)) {
                var clearance = clearances[(current.X, current.Y)];
                var candidate = new NativeRangedLaneCandidate(current.X, current.Y, current.Steps, clearance);
                var firstStepClearance = clearances[firstSteps[(current.X, current.Y)]];
                if (clearance >= NativeRangedMinimumThreatSpacing
                    && (currentThreatClearance >= NativeRangedMinimumThreatSpacing
                        || firstStepClearance > currentThreatClearance)
                    && (safestLane is null || IsBetterSafeNativeLane(candidate, safestLane.Value)))
                    safestLane = candidate;
                if (nearestLane is null || IsBetterNearestNativeLane(candidate, nearestLane.Value))
                    nearestLane = candidate;
            }

            if (current.Steps >= maximumSteps)
                continue;

            foreach (var offset in offsets) {
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (visited.Contains(next) || !IsNativeApproachCellOpen(main, snapshot, player, geometry, next.X, next.Y))
                    continue;
                if (offset.X != 0 && offset.Y != 0
                    && (!IsNativeApproachCellOpen(main, snapshot, player, geometry, current.X + offset.X, current.Y)
                        || !IsNativeApproachCellOpen(main, snapshot, player, geometry, current.X, current.Y + offset.Y)))
                    continue;
                visited.Add(next);
                previous[next] = (current.X, current.Y);
                clearances[next] = NativeThreatClearance(snapshot, player.LevelId, next.X, next.Y);
                firstSteps[next] = current.Steps == 0 ? next : firstSteps[(current.X, current.Y)];
                queue.Enqueue((next.X, next.Y, current.Steps + 1));
            }
        }
        var selectedLane = safestLane ?? nearestLane;
        if (selectedLane is not { } lane)
            return null;

        var path = new List<(int X, int Y)>();
        var pathPosition = (lane.X, lane.Y);
        while (pathPosition != start) {
            path.Add(pathPosition);
            if (!previous.TryGetValue(pathPosition, out var parent))
                return null;
            pathPosition = parent;
        }
        path.Reverse();
        return path.Count == 0 ? null : path[0];
    }

    private async Task<bool> AdvanceTowardReachableNativeDoor(
        Main main,
        Dictionary<uint, int> approachCommands,
        HashSet<uint> operatedDoorIds,
        List<NativeDoorOperationEvidence> doorEvidence,
        NativeCombatWorkBudget combatWork,
        Func<ObjectSnapshot, bool>? doorFilter = null)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = DepthGeometry(main, player.LevelId);
        var doors = snapshot.Objects.Where(@object => @object.LevelId == player.LevelId
                && @object.CathedralDoor is { DoorState: 0 }
                && !operatedDoorIds.Contains(@object.EntityId)
                && (doorFilter?.Invoke(@object) ?? true)
                && AuthoritativeObjectSelector.HasAuthoritativeInteraction(@object))
            .OrderBy(@object => NativeDistance(player.PositionX, player.PositionY,
                @object.PositionX, @object.PositionY))
            .ThenBy(@object => @object.EntityId);

        foreach (var door in doors) {
            var usedCommands = approachCommands.GetValueOrDefault(door.EntityId);
            if (NativeDistance(player.PositionX, player.PositionY,
                    door.PositionX, door.PositionY) <= 1
                && IsNativeDoorCellUnoccupied(snapshot, player.LevelId, door)
                && AuthoritativeObjectSelector.SelectAt(snapshot, door.PositionX, door.PositionY)?.EntityId
                    == door.EntityId) {
                var previousSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
                var geometryBefore = HashProto(geometry);
                var doorStateBefore = door.CathedralDoor!.DoorState;
                operatedDoorIds.Add(door.EntityId);
                combatWork.ConsumeExploration($"operate native door {door.EntityId}");
                await ClickWorld(main, door.PositionX, door.PositionY);
                await AcceptedAndSettled(main, previousSequence, "Operate a reachable native Cathedral door");
                var result = main.ObservedCommandResult
                    ?? throw new InvalidDataException($"Native door {door.EntityId} produced no operation receipt.");
                Require(result.Status == CommandStatus.Accepted
                    && result.RejectReason == CommandRejectReason.Unspecified
                    && result.ClientSequence == checked(previousSequence + 1) && result.AppliedTick > 0,
                    $"Native door {door.EntityId} did not produce its exact next accepted operation receipt: "
                    + $"sequence={result?.ClientSequence}, tick={result?.AppliedTick}, "
                    + $"status={result?.Status}, rejectReason={result?.RejectReason}.");
                await Until(() => main.ObservedSnapshot!.Objects.SingleOrDefault(item =>
                    item.EntityId == door.EntityId)?.CathedralDoor is { DoorState: not 0 },
                    $"Native door {door.EntityId} did not publish its changed state.", 5);
                var afterDoor = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == door.EntityId);
                var doorStateAfter = afterDoor.CathedralDoor!.DoorState;
                var geometryAfter = HashProto(DepthGeometry(main, player.LevelId));
                Require(doorStateAfter is 1 or 2 && geometryAfter != geometryBefore,
                    $"Native door {door.EntityId} changed to state {doorStateAfter} without a projected geometry mutation.");
                doorEvidence.Add(new NativeDoorOperationEvidence(door.EntityId,
                    afterDoor.CathedralDoor!.NativeObjectId, door.PositionX, door.PositionY,
                    doorStateBefore, doorStateAfter, doorStateAfter == 1, result.ClientSequence,
                    result.AppliedTick, geometryBefore, geometryAfter));
                return true;
            }

            if (usedCommands >= NativeMaximumRangedApproachCommands)
                continue;
            var step = FindNativeDoorApproachStep(main, snapshot, player, door,
                NativeMaximumRangedApproachCommands - usedCommands);
            if (step is not { } next || !IsNativeMovementStepInViewport(main, next.X, next.Y))
                continue;

            var previousPosition = (X: player.PositionX, Y: player.PositionY);
            var previousSequenceForMove = main.ObservedCommandResult?.ClientSequence ?? 0;
            approachCommands[door.EntityId] = usedCommands + 1;
            combatWork.ConsumeExploration($"approach native door {door.EntityId}");
            await Click(main.ObservedTileScreenPosition(next.X, next.Y), walk: true);
            await AcceptedAndSettled(main, previousSequenceForMove, "Approach a reachable native Cathedral door");
            var movementResult = main.ObservedCommandResult;
            var afterPlayer = main.ObservedSnapshot!.Players.Single();
            Require(movementResult is { Status: CommandStatus.Accepted,
                    RejectReason: CommandRejectReason.Unspecified }
                && movementResult.ClientSequence == checked(previousSequenceForMove + 1)
                && movementResult.AppliedTick > 0
                && afterPlayer.Life > 0 && afterPlayer.LevelId == player.LevelId
                && ((afterPlayer.PositionX, afterPlayer.PositionY) == next
                    || (afterPlayer.PositionX, afterPlayer.PositionY) == previousPosition),
                $"Native door approach to {door.EntityId} did not settle at its requested step or the prior tile: "
                + $"requested=({next.X},{next.Y}), previous=({previousPosition.X},{previousPosition.Y}), "
                + $"actual=({afterPlayer.PositionX},{afterPlayer.PositionY}), "
                + $"receipt={movementResult?.ClientSequence}/{movementResult?.Status}/{movementResult?.RejectReason}.");
            return true;
        }
        return false;
    }

    private static (int X, int Y)? FindNativeDoorApproachStep(
        Main main,
        Snapshot snapshot,
        PlayerSnapshot player,
        ObjectSnapshot door,
        int maximumSteps)
    {
        if (maximumSteps <= 0)
            return null;
        var geometry = DepthGeometry(main, player.LevelId);
        var start = (X: player.PositionX, Y: player.PositionY);
        var queue = new Queue<(int X, int Y, int Steps)>();
        var firstSteps = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var visited = new HashSet<(int X, int Y)> { start };
        var offsets = new (int X, int Y)[] {
            (0, 1), (1, 0), (0, -1), (-1, 0),
            (1, 1), (1, -1), (-1, -1), (-1, 1),
        };
        queue.Enqueue((start.X, start.Y, 0));

        while (queue.TryDequeue(out var current)) {
            if (current.Steps > 0
                && NativeDistance(current.X, current.Y, door.PositionX, door.PositionY) <= 1)
                return firstSteps[(current.X, current.Y)];
            if (current.Steps >= maximumSteps)
                continue;
            foreach (var offset in offsets) {
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (visited.Contains(next)
                    || !IsNativeApproachCellOpen(main, snapshot, player, geometry, next.X, next.Y))
                    continue;
                if (offset.X != 0 && offset.Y != 0
                    && (!IsNativeApproachCellOpen(main, snapshot, player, geometry,
                            current.X + offset.X, current.Y)
                        || !IsNativeApproachCellOpen(main, snapshot, player, geometry,
                            current.X, current.Y + offset.Y)))
                    continue;
                visited.Add(next);
                firstSteps[next] = current.Steps == 0 ? next : firstSteps[(current.X, current.Y)];
                queue.Enqueue((next.X, next.Y, current.Steps + 1));
            }
        }
        return null;
    }

    private static string DescribeNativeUnopenedDoors(Snapshot snapshot, HashSet<uint> operatedDoorIds)
        => "closedDoors=["
            + string.Join(";", snapshot.Objects.Where(@object => @object.LevelId == 1
                    && @object.CathedralDoor is { DoorState: 0 }
                    && !operatedDoorIds.Contains(@object.EntityId))
                .OrderBy(@object => @object.EntityId).Take(8)
                .Select(@object => $"{@object.EntityId}@{@object.PositionX},{@object.PositionY}"))
            + "]";

    private static bool IsNativeDoorCellUnoccupied(Snapshot snapshot, uint levelId, ObjectSnapshot door)
        => !snapshot.WorldItems.Any(item => item.LevelId == levelId
                && item.PositionX == door.PositionX && item.PositionY == door.PositionY)
            && !snapshot.Monsters.Any(monster => monster.LevelId == levelId && monster.Alive
                && (monster.PositionX == door.PositionX && monster.PositionY == door.PositionY
                    || monster.Moving && monster.FuturePositionX == door.PositionX
                        && monster.FuturePositionY == door.PositionY))
            && !snapshot.Objects.Any(@object => @object.EntityId != door.EntityId
                && @object.LevelId == levelId && @object.PositionX == door.PositionX
                && @object.PositionY == door.PositionY && !@object.Destroyed);

    private static string DescribeNativeDoorOperations(
        IReadOnlyCollection<NativeDoorOperationEvidence> operations)
        => "doorOperations=["
            + string.Join(";", operations.Select(item =>
                $"{item.EntityId}@{item.X},{item.Y}:{item.DoorStateBefore}->{item.DoorStateAfter}"
                + $"/opened={item.Opened}/geometry={item.GeometryBeforeSha256}->{item.GeometryAfterSha256}"))
            + "]";

    private static bool IsNativeMovementStepInViewport(Main main, int x, int y)
    {
        var worldArea = (Control)main.FindChild("WorldArea", true, false);
        return worldArea.GetGlobalRect().HasPoint(main.ObservedTileScreenPosition(x, y));
    }

    private static bool IsBetterSafeNativeLane(
        NativeRangedLaneCandidate candidate, NativeRangedLaneCandidate current)
        => candidate.Steps < current.Steps
            || candidate.Steps == current.Steps && candidate.ThreatClearance > current.ThreatClearance;

    private static bool IsBetterNearestNativeLane(
        NativeRangedLaneCandidate candidate, NativeRangedLaneCandidate current)
        => candidate.Steps < current.Steps
            || candidate.Steps == current.Steps && candidate.ThreatClearance > current.ThreatClearance;

    private static long NativeThreatClearance(Snapshot snapshot, uint levelId, int x, int y)
    {
        var clearance = long.MaxValue;
        foreach (var threat in snapshot.Monsters.Where(monster => monster.LevelId == levelId
                     && monster.Alive && !monster.IsPlayerMinion)) {
            clearance = Math.Min(clearance, NativeDistance(x, y, threat.PositionX, threat.PositionY));
            if (threat.Moving)
                clearance = Math.Min(clearance, NativeDistance(x, y, threat.FuturePositionX, threat.FuturePositionY));
        }
        return clearance;
    }

    private static string DescribeNativeThreatSpacing(Snapshot snapshot, uint levelId, int x, int y)
    {
        var nearby = snapshot.Monsters.Where(monster => monster.LevelId == levelId
                && monster.Alive && !monster.IsPlayerMinion)
            .Select(monster => new {
                monster.EntityId,
                Current = NativeDistance(x, y, monster.PositionX, monster.PositionY),
                Future = monster.Moving
                    ? NativeDistance(x, y, monster.FuturePositionX, monster.FuturePositionY)
                    : long.MaxValue,
                monster.Moving,
                monster.FuturePositionX,
                monster.FuturePositionY,
            })
            .OrderBy(threat => Math.Min(threat.Current, threat.Future))
            .ThenBy(threat => threat.EntityId)
            .Take(4)
            .Select(threat => $"{threat.EntityId}:distance={threat.Current},futureDistance={threat.Future},moving={threat.Moving}"
                + $"/future={threat.FuturePositionX},{threat.FuturePositionY}");
        return $"threatClearance={NativeThreatClearance(snapshot, levelId, x, y)},"
            + $"required={NativeRangedMinimumThreatSpacing},nearest=[{string.Join(";", nearby)}]";
    }

    private static (bool Inside, Vector2 Point, Rect2 Bounds) ProjectNativeTargetClick(
        Main main,
        LevelGeometrySnapshot geometry,
        int cameraX,
        int cameraY,
        (int X, int Y) targetTile)
    {
        var worldArea = (Control)main.FindChild("WorldArea", true, false);
        var world = worldArea.GetChildren().OfType<WorldPresentation>().Single();
        var player = main.ObservedSnapshot!.Players.Single();
        var width = Math.Max(1, Mathf.RoundToInt(worldArea.Size.X));
        var height = Math.Max(1, Mathf.RoundToInt(worldArea.Size.Y));
        const double tileSize = 32;
        var currentViewport = AuthoritativeWorldViewport.Create(width, height,
            geometry.Width, geometry.Height, player.PositionX, player.PositionY, tileSize);
        var candidateViewport = AuthoritativeWorldViewport.Create(width, height,
            geometry.Width, geometry.Height, cameraX, cameraY, tileSize);
        var currentScreen = currentViewport.WorldToScreen(targetTile.X, targetTile.Y);
        var candidateScreen = candidateViewport.WorldToScreen(targetTile.X, targetTile.Y);
        var observedGlobal = main.ObservedTileScreenPosition(targetTile.X, targetTile.Y);
        var observedLocal = world.ToLocal(observedGlobal);
        var candidateGlobal = world.ToGlobal(observedLocal + new Vector2(
            (float)(candidateScreen.X - currentScreen.X),
            (float)(candidateScreen.Y - currentScreen.Y)));
        var bounds = worldArea.GetGlobalRect();
        return (bounds.HasPoint(candidateGlobal), candidateGlobal, bounds);
    }

    private static string DescribeNativeTargetClick(
        Main main,
        LevelGeometrySnapshot geometry,
        int cameraX,
        int cameraY,
        (int X, int Y) targetTile)
    {
        var projection = ProjectNativeTargetClick(main, geometry, cameraX, cameraY, targetTile);
        return $"click=(inside={projection.Inside},tile=({targetTile.X},{targetTile.Y}),"
            + $"screen=({projection.Point.X:F1},{projection.Point.Y:F1}),worldArea={projection.Bounds})";
    }

    private static bool CanSeeNativeRangedLane(
        PlayerSnapshot player,
        MonsterSnapshot target,
        LevelGeometrySnapshot geometry,
        int viewerX,
        int viewerY,
        (int X, int Y) targetTile)
    {
        var radius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return AuthoritativeMonsterPresentationVisibility.IsVisible(target, player.LevelId)
            && AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId, target.PositionX, target.PositionY,
                player.LevelId, geometry, viewerX, viewerY, radius)
            && (!target.Moving || AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId,
                target.FuturePositionX, target.FuturePositionY,
                player.LevelId, geometry, viewerX, viewerY, radius))
            && AuthoritativeNativeVisionVisibility.IsVisible(target.LevelId, targetTile.X, targetTile.Y,
                player.LevelId, geometry, viewerX, viewerY, radius);
    }

    private static bool IsNativeApproachCellOpen(
        Main main,
        Snapshot snapshot,
        PlayerSnapshot player,
        LevelGeometrySnapshot geometry,
        int x,
        int y)
    {
        if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height
            || geometry.WarpTriggers.Any(route => route.TriggerX == x && route.TriggerY == y))
            return false;
        var cell = checked(y * geometry.Width + x);
        var passableDoor = snapshot.Objects.FirstOrDefault(@object => @object.LevelId == player.LevelId
            && @object.PositionX == x && @object.PositionY == y
            && @object.CathedralDoor is { DoorState: 1 or 2 }
            && AuthoritativeObjectSelector.HasAuthoritativeInteraction(@object));
        if (passableDoor is null
            && (geometry.BlockedCells.Contains(cell)
                || geometry.StaticObjectBlockedCells.Contains(cell)
                || snapshot.Objects.Any(@object => @object.LevelId == player.LevelId
                    && @object.PositionX == x && @object.PositionY == y && !@object.Destroyed)))
            return false;
        if (passableDoor is not null
            && snapshot.Objects.Any(@object => @object.EntityId != passableDoor.EntityId
                && @object.LevelId == player.LevelId && @object.PositionX == x
                && @object.PositionY == y && !@object.Destroyed))
            return false;
        if (snapshot.WorldItems.Any(item => item.LevelId == player.LevelId
                && item.PositionX == x && item.PositionY == y))
            return false;
        return !snapshot.Monsters.Any(monster => monster.LevelId == player.LevelId
            && monster.Alive && !monster.IsPlayerMinion
            && (monster.PositionX == x && monster.PositionY == y
                || monster.Moving && monster.FuturePositionX == x && monster.FuturePositionY == y));
    }

    private static bool HasClearNativeRangedLane(
        Snapshot snapshot,
        LevelGeometrySnapshot geometry,
        IReadOnlySet<int> missileBlockedCells,
        uint levelId,
        int startX,
        int startY,
        MonsterSnapshot target,
        (int X, int Y) projectedTarget)
    {
        var currentTarget = (target.PositionX, target.PositionY);
        if (!HasNativeMissileLineOfSight(geometry, missileBlockedCells,
                startX, startY, currentTarget.PositionX, currentTarget.PositionY)
            || !HasConservativeRangedInterferenceClear(snapshot, levelId,
                startX, startY, currentTarget.PositionX, currentTarget.PositionY, target.EntityId))
            return false;
        if (!target.Moving)
            return projectedTarget == currentTarget;

        return HasNativeMissileLineOfSight(geometry, missileBlockedCells,
                startX, startY, target.FuturePositionX, target.FuturePositionY)
            && HasConservativeRangedInterferenceClear(snapshot, levelId,
                startX, startY, target.FuturePositionX, target.FuturePositionY, target.EntityId)
            && projectedTarget == (target.FuturePositionX, target.FuturePositionY);
    }

    /** Mirrors DeterministicGridLineTraversal.HasLineOfSight over the published missile mask. */
    private static bool HasNativeMissileLineOfSight(
        LevelGeometrySnapshot geometry,
        IReadOnlySet<int> missileBlockedCells,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        long x = startX;
        long y = startY;
        var deltaX = Math.Abs((long)endX - startX);
        var deltaY = Math.Abs((long)endY - startY);
        if (Math.Max(deltaX, deltaY) > 4096)
            return false;
        var stepX = startX < endX ? 1L : -1L;
        var stepY = startY < endY ? 1L : -1L;
        var error = deltaX - deltaY;
        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY)
                && (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height
                    || missileBlockedCells.Contains(checked((int)(y * geometry.Width + x)))))
                return false;
            if (x == endX && y == endY)
                return true;

            var doubledError = 2L * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }

    /** Conservative lane-planning guard only; this is not a projectile simulation. */
    private static bool HasConservativeRangedInterferenceClear(
        Snapshot snapshot,
        uint levelId,
        int startX,
        int startY,
        int targetX,
        int targetY,
        uint targetEntityId)
    {
        var x = startX;
        var y = startY;
        while (x != targetX || y != targetY) {
            x += Math.Sign(targetX - x);
            y += Math.Sign(targetY - y);
            if (x == targetX && y == targetY)
                return true;
            if (snapshot.Objects.Any(@object => @object.LevelId == levelId && !@object.Destroyed
                    && !@object.MissilePassable && @object.PositionX == x && @object.PositionY == y)
                || snapshot.Monsters.Any(monster => monster.EntityId != targetEntityId
                    && monster.LevelId == levelId && monster.Alive && !monster.IsPlayerMinion
                    && (monster.PositionX == x && monster.PositionY == y
                        || monster.Moving && monster.FuturePositionX == x && monster.FuturePositionY == y)))
                return false;
        }
        return true;
    }

    private static string DescribeNativeAttackSnapshot(Snapshot snapshot, uint targetId)
    {
        var player = snapshot.Players.SingleOrDefault();
        var target = snapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId);
        var nearest = player is null
            ? "unavailable"
            : string.Join(";", snapshot.Monsters.Where(monster => monster.LevelId == player.LevelId
                    && monster.Alive && !monster.IsPlayerMinion)
                .OrderBy(monster => NativeDistance(player.PositionX, player.PositionY,
                    monster.PositionX, monster.PositionY))
                .ThenBy(monster => monster.EntityId)
                .Take(4)
                .Select(monster => $"{monster.EntityId}:{monster.MonsterId}@{monster.PositionX},{monster.PositionY}"
                    + $"/moving={monster.Moving}/future={monster.FuturePositionX},{monster.FuturePositionY}"
                    + $"/hpQ6={monster.HitPointsQ6}/ai={monster.NativeAi}"));
        var playerState = player is null
            ? "missing"
            : $"{player.EntityId}@{player.PositionX},{player.PositionY}/level={player.LevelId}"
                + $"/life={player.Life}/mode={player.CombatMode}/action={player.CurrentAction?.Kind}"
                + $"/actionTarget={player.CurrentAction?.TargetEntityId}";
        var targetState = target is null
            ? "missing"
            : $"{target.EntityId}:{target.MonsterId}@{target.PositionX},{target.PositionY}/level={target.LevelId}"
                + $"/alive={target.Alive}/moving={target.Moving}/future={target.FuturePositionX},{target.FuturePositionY}"
                + $"/hpQ6={target.HitPointsQ6}/ai={target.NativeAi}";
        return $"tick={snapshot.Tick};player={playerState};target={targetState};nearest=[{nearest}]";
    }

    private static string DescribeNativeSpellSourceSelection(Main main)
    {
        if (main.FindChild("SpellSources", true, false) is not ItemList sources)
            return "unavailable";
        var selected = sources.GetSelectedItems();
        if (selected.Length == 0)
            return "none";
        return string.Join(";", selected.Where(index => index >= 0 && index < sources.ItemCount)
            .Select(index => $"{index}:{sources.GetItemText(index)}"));
    }

    private async Task ClearImmediateNativeThreats(
        Main main,
        int lootX,
        int lootY,
        HashSet<uint> visited,
        List<NativeKillEvidence> killed)
    {
        for (var cleared = 0; cleared < 8; cleared++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var threat = snapshot.Monsters.Where(monster => monster.LevelId == player.LevelId
                    && monster.Alive && !monster.IsPlayerMinion && !visited.Contains(monster.EntityId)
                    && IsNativeThreatWithinScope(player, monster, lootX, lootY))
                .OrderBy(monster => NativeMonsterDistance(player, monster))
                .ThenBy(monster => monster.EntityId).FirstOrDefault();
            if (threat is null)
                return;
            var killedThreat = await DefeatNativeTargetAsync(main, threat.EntityId, maximumAttacks: 48,
                lootX: lootX, lootY: lootY);
            if (killedThreat is null)
                continue;
            visited.Add(killedThreat.MonsterEntityId);
            killed.Add(killedThreat);
        }
    }

    private static bool IsNativeThreatWithinScope(
        PlayerSnapshot player, MonsterSnapshot target, int? lootX, int? lootY)
    {
        if (!lootX.HasValue || !lootY.HasValue)
            return true;
        var playerDistance = NativeDistance(player.PositionX, player.PositionY, target.PositionX, target.PositionY);
        var lootDistance = NativeDistance(lootX.Value, lootY.Value, target.PositionX, target.PositionY);
        if (!target.Moving)
            return playerDistance <= 5 || lootDistance <= 4;
        var futurePlayerDistance = NativeDistance(player.PositionX, player.PositionY, target.FuturePositionX, target.FuturePositionY);
        var futureLootDistance = NativeDistance(lootX.Value, lootY.Value, target.FuturePositionX, target.FuturePositionY);
        return Math.Min(playerDistance, futurePlayerDistance) <= 5
            || Math.Min(lootDistance, futureLootDistance) <= 4;
    }

    private static bool HasNativeRangedLaneWithinApproach(
        Main main, Snapshot snapshot, PlayerSnapshot player, MonsterSnapshot target, int maximumSteps)
    {
        if (!target.Alive || target.LevelId != player.LevelId)
            return false;
        var geometry = DepthGeometry(main, player.LevelId);
        var missileBlockedCells = geometry.MissileBlockedCells.ToHashSet();
        var targetTile = ProjectedNativeTargetTile(target);
        if (CanSelectNativeRangedTarget(main, snapshot, player, target, geometry, targetTile)
            && HasClearNativeRangedLane(snapshot, geometry, missileBlockedCells, player.LevelId,
                player.PositionX, player.PositionY, target, targetTile))
            return true;

        var step = FindNativeRangedLaneStep(main, snapshot, player, target, geometry,
            missileBlockedCells, NativeThreatClearance(snapshot, player.LevelId,
                player.PositionX, player.PositionY), maximumSteps);
        return step is { } point && IsNativeMovementStepInViewport(main, point.X, point.Y);
    }

    private static bool HasCloserNativeThreat(
        Main main, Snapshot snapshot, PlayerSnapshot player, MonsterSnapshot selected,
        int? lootX, int? lootY)
    {
        var selectedDistance = NativeMonsterDistance(player, selected);
        return snapshot.Monsters.Any(monster => monster.EntityId != selected.EntityId
            && monster.LevelId == player.LevelId
            && monster.Alive && !monster.IsPlayerMinion
            && NativeMonsterDistance(player, monster) <= 5
            && NativeMonsterDistance(player, monster) < selectedDistance
            && IsNativeThreatWithinScope(player, monster, lootX, lootY)
            && HasNativeRangedLaneWithinApproach(main, snapshot, player, monster,
                NativeMaximumRangedApproachCommands));
    }

    private static long NativeMonsterDistance(PlayerSnapshot player, MonsterSnapshot monster)
    {
        var currentDistance = NativeDistance(player.PositionX, player.PositionY,
            monster.PositionX, monster.PositionY);
        return monster.Moving
            ? Math.Min(currentDistance, NativeDistance(player.PositionX, player.PositionY,
                monster.FuturePositionX, monster.FuturePositionY))
            : currentDistance;
    }

    private async Task UseNativeHealingPotionIfNeeded(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.Life > 0, $"The Rogue died before a native healing command could be dispatched; {DescribeNativeHealingState(main)}.");
        if (!NativeHealingThresholdReached(main))
            return;
        var beltPotion = player.Belt.OrderBy(item => item.Slot)
            .FirstOrDefault(item => IsNativeHealingConsumable(item.State));
        var inventoryPotion = beltPotion is null
            ? player.Inventory.Select((item, index) => (Item: item, Index: index))
                .FirstOrDefault(entry => IsNativeHealingConsumable(entry.Item.State))
            : default;
        if (beltPotion is null && inventoryPotion.Item is null)
            return;

        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        Button useButton;
        if (beltPotion is not null) {
            Require(beltPotion.Slot < 8, $"The healing potion has invalid belt slot {beltPotion.Slot}.");
            useButton = (Button)main.FindChild($"QuickUseBelt{beltPotion.Slot + 1}", true, false);
        } else {
            var potion = inventoryPotion.Item!;
            var index = inventoryPotion.Index;
            var layout = InventoryLayout.Build(player.Inventory, player.InventoryGrid);
            var selectedSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await ClickGridCell(main, sections, 1, "InventoryGrid", layout.Anchors[index]);
            var selected = main.GetChildren().OfType<HudPresentation>().Single().SelectedItemReference;
            Require(selected is { Location: PlayerItemLocation.Inventory } reference
                && reference.Slot == (uint)index && reference.StableItemId == potion.State.StableItemId
                && main.ObservedPendingCommandCount == 0
                && main.ObservedCommandResult?.ClientSequence == selectedSequence,
                "The ordinary inventory selection did not retain the exact healing potion reference.");
            useButton = (Button)main.FindChild("UseConsumable", true, false);
        }
        Require(useButton.Visible && !useButton.Disabled,
            $"The ordinary consumable control was unavailable; life={player.Life}/{player.LifeMaximum}.");
        var beforeLife = player.Life;
        var beforeTick = main.ObservedSnapshot!.Tick;
        var beforeAction = player.CurrentAction?.Kind;
        var beforeMode = player.CombatMode;
        var potionSeed = beltPotion?.ItemSeed ?? inventoryPotion.Item!.ItemSeed;
        var potionStableId = beltPotion?.State.StableItemId ?? inventoryPotion.Item!.State.StableItemId;
        var beforeCount = CountNativeHealingItemOccurrences(player, potionStableId, potionSeed);
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        Require(main.ObservedLastQueuedSequence == sequence && main.ObservedPendingCommandCount == 0,
            $"Quick-use began before the previous command settled; result={sequence}, "
            + $"queued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}.");
        var expectedSequence = checked(sequence + 1);
        await Click(useButton.GetGlobalRect().GetCenter());
        Require(main.ObservedLastQueuedSequence == expectedSequence
            && (main.ObservedPendingCommandCount > 0
                || main.ObservedCommandResult?.ClientSequence == expectedSequence),
            $"The ordinary consumable control did not queue the exact healing item; expected sequence={expectedSequence}, "
            + $"queued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}, "
            + $"result={main.ObservedCommandResult?.ClientSequence}.");
        try {
            await AcceptedAndSettled(main, sequence, "Use native-route healing consumable");
        } catch (InvalidOperationException exception) {
            var afterSnapshot = main.ObservedSnapshot;
            var afterPlayer = afterSnapshot?.Players.SingleOrDefault();
            var failureReceipt = main.ObservedCommandResult;
            throw new InvalidOperationException(
                $"{exception.Message} Native healing context: before[tick={beforeTick},life={beforeLife}/{player.LifeMaximum},"
                + $"mode={beforeMode},action={beforeAction}]; after[tick={afterSnapshot?.Tick},"
                + $"life={afterPlayer?.Life}/{afterPlayer?.LifeMaximum},mode={afterPlayer?.CombatMode},"
                + $"action={afterPlayer?.CurrentAction?.Kind}]; queued={main.ObservedLastQueuedSequence},"
                + $"pending={main.ObservedPendingCommandCount},itemSeed={potionSeed},stableId={potionStableId},"
                + $"receipt={failureReceipt?.ClientSequence}/{failureReceipt?.AppliedTick}/{failureReceipt?.Status}/{failureReceipt?.RejectReason}.",
                exception);
        }
        var result = main.ObservedCommandResult;
        var after = main.ObservedSnapshot!.Players.Single();
        var progressionL2 = IsNativeProgressionL2(main);
        var exactPotionConsumed = CountNativeHealingItemOccurrences(after, potionStableId, potionSeed) == beforeCount - 1;
        var lifeOutcomeValid = progressionL2
            ? beforeLife < player.LifeMaximum && after.Life > 0
            : after.Life > beforeLife;
        Require(result is { ClientSequence: var acknowledgedSequence, Status: CommandStatus.Accepted,
                RejectReason: CommandRejectReason.Unspecified }
            && acknowledgedSequence == expectedSequence
            && exactPotionConsumed && lifeOutcomeValid,
            $"The ordinary consumable command did not produce its required accepted exact-item healing result; "
            + $"expected sequence={expectedSequence}, result={result?.ClientSequence}/{result?.Status}/{result?.RejectReason}, "
            + $"matchingItems={beforeCount}->{CountNativeHealingItemOccurrences(after, potionStableId, potionSeed)}, "
            + $"life={beforeLife}->{after.Life}, "
            + $"seed={potionSeed}, stableId={potionStableId}; {DescribeNativeHealingState(main)}.");
        if (progressionL2)
            GD.Print($"Native L2 potion accepted: sequence={result!.ClientSequence}, appliedTick={result.AppliedTick}, "
                + $"observedTick={main.ObservedSnapshot!.Tick}, life={beforeLife}->{after.Life}, "
                + $"netLifeDelta={after.Life - beforeLife}, exactSeed={potionSeed}, stableId={potionStableId}.");
    }

    private static bool IsNativeProgressionL2(Main main)
    {
        var phase = System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE");
        return main.ObservedSnapshot?.Players.SingleOrDefault()?.LevelId == 2
            && (phase == "CathedralNativeProgressionJourney" || phase == "CathedralNativeProgressionResume");
    }

    private static bool NativeHealingThresholdReached(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        if (player.Life <= 0 || player.LifeMaximum <= 0)
            return false;
        return IsNativeProgressionL2(main)
            ? (long)player.Life * 100 <= (long)player.LifeMaximum * 90
            : (long)player.Life * 3 <= (long)player.LifeMaximum * 2;
    }

    private static bool NativeHealingPotionAvailable(PlayerSnapshot player)
        => player.Belt.Any(item => IsNativeHealingConsumable(item.State))
            || player.Inventory.Any(item => IsNativeHealingConsumable(item.State));

    private static bool NativeHealingIsDue(Main main)
        => main.ObservedSnapshot?.Players.SingleOrDefault() is { } player
            && NativeHealingThresholdReached(main) && NativeHealingPotionAvailable(player);

    private async Task WaitForNativeArrowSettlementAsync(Main main, uint targetId, uint playerId)
    {
        bool ArrowSettled() => !main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == targetId && monster.Alive)
            || !main.ObservedSnapshot.Projectiles.Any(projectile => projectile.SourceEntityId == playerId
                && projectile.TargetEntityId == targetId);

        var failure = $"The native arrow against target {targetId} did not settle within its existing 20-second bound.";
        await Until(ArrowSettled, failure, 20);
    }

    private static string DescribeNativeHealingState(Main main)
    {
        var snapshot = main.ObservedSnapshot;
        var player = snapshot?.Players.SingleOrDefault();
        var result = main.ObservedCommandResult;
        return $"tick={snapshot?.Tick},life={player?.Life}/{player?.LifeMaximum},mode={player?.CombatMode},"
            + $"action={player?.CurrentAction?.Kind},queued={main.ObservedLastQueuedSequence},"
            + $"pending={main.ObservedPendingCommandCount},receipt={result?.ClientSequence}/{result?.AppliedTick}/"
            + $"{result?.Status}/{result?.RejectReason}";
    }

    private async Task CollectNearbyNativeHealingDropsAsync(Main main, IReadOnlyCollection<uint> earnedDropEntityIds)
    {
        var attempted = new HashSet<uint>();
        for (var count = 0; count < earnedDropEntityIds.Count; count++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var candidate = snapshot.WorldItems.Where(item => earnedDropEntityIds.Contains(item.EntityId)
                    && item.LevelId == player.LevelId && IsNativeHealingConsumable(item.State)
                    && NativeDistance(player.PositionX, player.PositionY, item.PositionX, item.PositionY) <= 1
                    && !attempted.Contains(item.EntityId))
                .OrderBy(item => item.EntityId)
                .FirstOrDefault(item => !snapshot.WorldItems.Any(other => other.EntityId != item.EntityId
                    && other.LevelId == item.LevelId && other.PositionX == item.PositionX
                    && other.PositionY == item.PositionY));
            if (candidate is null)
                return;

            attempted.Add(candidate.EntityId);
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            var gold = player.Gold;
            await ClickWorld(main, candidate.PositionX, candidate.PositionY);
            await AcceptedAndSettled(main, sequence, "Pick up a genuinely earned native healing item");
            await Until(() => main.ObservedSnapshot!.WorldItems.All(item => item.EntityId != candidate.EntityId)
                && CountNativeHealingItemOccurrences(main.ObservedSnapshot!.Players.Single(),
                    candidate.State.StableItemId, candidate.ItemSeed) == 1,
                "The nearby native healing drop did not move into the Rogue's carried items.", 5);
            var after = main.ObservedSnapshot!.Players.Single();
            var result = main.ObservedCommandResult;
            Require(result is { Status: CommandStatus.Accepted, RejectReason: CommandRejectReason.Unspecified }
                && result.ClientSequence == sequence + 1
                && after.Gold == gold
                && CountNativeItemOccurrences(main, after, candidate.State.StableItemId, candidate.ItemSeed) == 1,
                $"Native healing-drop pickup changed Gold or failed exact ownership: result="
                + $"{result?.ClientSequence}/{result?.Status}/{result?.RejectReason}, gold={gold}->{after.Gold}, "
                + $"seed={candidate.ItemSeed}, stableId={candidate.State.StableItemId}.");
        }
    }

    private static bool IsNativeHealingConsumable(ItemStateSnapshot state)
        => state.ItemActionKind is ItemActionKind.RestorePartialLife
            or ItemActionKind.RestoreFullLife
            or ItemActionKind.RestorePartialLifeAndMana
            or ItemActionKind.RestoreFullLifeAndMana;

    private static int CountNativeHealingItemOccurrences(PlayerSnapshot player, uint stableItemId, uint itemSeed)
        => player.Inventory.Count(item => item.State.StableItemId == stableItemId && item.ItemSeed == itemSeed)
            + player.Belt.Count(item => item.State.StableItemId == stableItemId && item.ItemSeed == itemSeed);

    private async Task EquipNativeItem(Main main, ItemSnapshot carried, uint slot)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var player = main.ObservedSnapshot!.Players.Single();
        var occupant = player.Equipment.FirstOrDefault(item => item.Slot == slot);
        if (occupant is not null) {
            await ClickListItem(sections, 1, equipment, checked((int)slot));
            var sequence = main.ObservedCommandResult!.ClientSequence;
            await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(player, occupant.State));
            await AcceptedAndSettled(main, sequence, "Unequip native-route replacement item");
            await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.All(item => item.Slot != slot),
                "The replaced equipment did not leave its native slot.", 5);
        }
        player = main.ObservedSnapshot!.Players.Single();
        var index = player.Inventory.ToList().FindIndex(item => item.State.StableItemId == carried.State.StableItemId
            && item.ItemSeed == carried.ItemSeed);
        Require(index >= 0, "The exact earned native item is missing from inventory before equipment selection.");
        var layout = Devilution.Client.Protocol.InventoryLayout.Build(player.Inventory, player.InventoryGrid);
        await ClickGridCell(main, sections, 1, "InventoryGrid", layout.Anchors[index]);
        var equipSequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, checked((int)slot));
        await AcceptedAndSettled(main, equipSequence, "Equip earned native Cathedral item");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item => item.Slot == slot
            && item.State.StableItemId == carried.State.StableItemId && item.ItemSeed == carried.ItemSeed),
            "The actual equipment control did not equip the earned native item.", 5);
    }

    private async Task<NativeStashEvidence> RoundTripNativeEquipmentThroughStash(
        Main main,
        NativePopulationCheckpoint expected)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var player = main.ObservedSnapshot!.Players.Single();
        var equipped = player.Equipment.Single(item => item.Slot == expected.EquipmentSlot
            && item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed).Clone();
        var itemHash = HashProto(equipped.State);
        var gold = player.Gold;
        var stashGold = player.StashGold;
        await ClickListItem(sections, 1, equipment, checked((int)expected.EquipmentSlot));
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(player, equipped.State));
        await AcceptedAndSettled(main, sequence, "Unequip native-earned equipment for Town stash");
        await Until(() => main.ObservedSnapshot!.Players.Single().Inventory.Any(item =>
            item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed),
            "The native-earned item did not move into the ordinary Town inventory.", 5);
        Require(HashProto(main.ObservedSnapshot!.Players.Single().Inventory.Single(item =>
                item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed).State) == itemHash,
            "Unequipping changed the native-earned item's state before stashing.");
        await ApproachDeclaredDestination(main, "towner:7", distanceBoundedApproachDeadline: true);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "OpenStash");
        await AcceptedAndSettled(main, sequence, "Open Town stash for native-earned equipment");
        await Until(() => main.ObservedSnapshot!.Players.Single().StashOpen,
            "The Town stash did not open for the native-earned equipment.", 5);

        player = main.ObservedSnapshot!.Players.Single();
        var cell = FindNativeStashCell(player, equipped.State);
        await StashInventoryItem(main, sections, expected.StableItemId, cell);
        var stored = main.ObservedSnapshot!.Players.Single().Stash.Single(item => item.Page == 0 && item.Cell == cell);
        Require(stored.Item.State.StableItemId == expected.StableItemId
            && stored.Item.ItemSeed == expected.ItemSeed
            && HashProto(stored.Item.State) == itemHash
            && CountNativeItemOccurrences(main, main.ObservedSnapshot.Players.Single(), expected.StableItemId, expected.ItemSeed) == 1
            && main.ObservedSnapshot.Players.Single().Gold == gold
            && main.ObservedSnapshot.Players.Single().StashGold == stashGold,
            "Stashing changed the native-earned item, duplicated it or changed Gold.");

        await ClickGridCell(main, sections, 3, "StashGrid", cell);
        var selectTick = main.ObservedSnapshot!.Tick;
        await Until(() => main.ObservedSnapshot!.Tick > selectTick + 2 && Settled(main),
            "The native-earned item did not remain selected in the open stash.", 5);
        sequence = main.ObservedCommandResult!.ClientSequence;
        var inventoryCell = FindInventorySpace(main.ObservedSnapshot!.Players.Single(), stored.Item.State);
        await ClickGridCell(main, sections, 1, "InventoryGrid", inventoryCell);
        await AcceptedAndSettled(main, sequence, "Withdraw native-earned equipment from Town stash");
        await Until(() => main.ObservedSnapshot!.Players.Single().Inventory.Any(item =>
            item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed)
            && main.ObservedSnapshot.Players.Single().Stash.All(item => item.Item.State.StableItemId != expected.StableItemId),
            "The native-earned item did not return to inventory from the stash.", 5);
        Require(HashProto(main.ObservedSnapshot!.Players.Single().Inventory.Single(item =>
                item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed).State) == itemHash,
            "Withdrawing the native-earned item changed its state.");
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "CloseStash");
        await AcceptedAndSettled(main, sequence, "Close Town stash after native item withdrawal");
        await SelectInventoryItem(main, sections, expected.StableItemId);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, checked((int)expected.EquipmentSlot));
        await AcceptedAndSettled(main, sequence, "Re-equip native-earned item after stash round trip");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item =>
            item.Slot == expected.EquipmentSlot && item.State.StableItemId == expected.StableItemId
                && item.ItemSeed == expected.ItemSeed),
            "The native-earned item did not re-equip after its stash round trip.", 5);
        player = main.ObservedSnapshot!.Players.Single();
        var final = player.Equipment.Single(item => item.Slot == expected.EquipmentSlot
            && item.State.StableItemId == expected.StableItemId && item.ItemSeed == expected.ItemSeed);
        Require(HashProto(final.State) == itemHash && player.Gold == gold && player.StashGold == stashGold
            && CountNativeItemOccurrences(main, player, expected.StableItemId, expected.ItemSeed) == 1,
            "The native-earned item or Gold changed during its full stash round trip.");
        return new NativeStashEvidence(true, cell, HashProto(final.State));
    }

    private async Task<NativeRepairEvidence> InspectNativeSmithRepair(
        Main main,
        NativePopulationCheckpoint expected)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await ApproachDeclaredDestination(main, "towner:0", distanceBoundedApproachDeadline: true);
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "OpenSmith");
        await AcceptedAndSettled(main, sequence, "Open Town Smith for native-earned item");
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 1,
            "The Smith did not open after the native Cathedral return.", 5);
        var item = main.ObservedSnapshot!.Players.Single().Equipment.Single(equipped =>
            equipped.Slot == expected.EquipmentSlot && equipped.State.StableItemId == expected.StableItemId
                && equipped.ItemSeed == expected.ItemSeed);
        var goldBefore = main.ObservedSnapshot.Players.Single().Gold;
        var stateBefore = item.State.Clone();
        var stateBeforeHash = HashProto(stateBefore);
        var durabilityBefore = item.State.Durability;
        var maximumDurability = item.State.MaxDurability;
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var selectionSequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, checked((int)expected.EquipmentSlot));
        var selected = main.GetChildren().OfType<HudPresentation>().Single().SelectedItemReference?.Clone();
        Require(selected is { Location: PlayerItemLocation.Equipment }
            && selected.Slot == expected.EquipmentSlot && selected.StableItemId == expected.StableItemId,
            "The actual HUD did not select the native-earned equipment as the Smith target.");
        var repairButton = AuthoredDescendantButtons(sections.GetChild(2))
            .Single(button => button.Text.StartsWith("Repair", StringComparison.Ordinal));
        await RevealControl(sections, 2, repairButton);
        await Until(() => FindAuthoredRepairQuote(main, selected) is not null && repairButton.IsVisibleInTree(),
            "The Smith did not publish its authoritative repair quote for the selected native item.", 10);
        var quote = FindAuthoredRepairQuote(main, selected)!;
        var repairButtonDisabled = repairButton.Disabled;
        var repaired = false;
        if (quote.RejectReason == CommandRejectReason.Unspecified && !repairButtonDisabled) {
            Require(goldBefore >= quote.Price,
                "The Smith enabled a paid repair whose quoted price exceeds the Rogue's Gold.");
            sequence = main.ObservedCommandResult!.ClientSequence;
            await Click(repairButton.GetGlobalRect().GetCenter());
            await AcceptedAndSettled(main, sequence, "Repair native-earned equipment at the Town Smith");
            await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(equipped =>
                equipped.Slot == expected.EquipmentSlot && equipped.State.StableItemId == expected.StableItemId
                && equipped.ItemSeed == expected.ItemSeed
                && equipped.State.Durability == equipped.State.MaxDurability),
                "The accepted Smith repair did not restore the item's authoritative durability.", 10);
            var repairedPlayer = main.ObservedSnapshot!.Players.Single();
            var repairedItem = repairedPlayer.Equipment.Single(equipped => equipped.Slot == expected.EquipmentSlot
                && equipped.State.StableItemId == expected.StableItemId && equipped.ItemSeed == expected.ItemSeed);
            var expectedRepairedState = stateBefore.Clone();
            expectedRepairedState.Durability = expectedRepairedState.MaxDurability;
            Require(repairedPlayer.Gold == goldBefore - quote.Price
                && repairedItem.State.Durability == maximumDurability
                && repairedItem.ItemSeed == item.ItemSeed
                && HashProto(repairedItem.State) == HashProto(expectedRepairedState),
                "The accepted Smith repair did not charge the exact quote or changed unrelated item state.");
            repaired = true;
        } else {
            Require(repairButtonDisabled && main.ObservedCommandResult!.ClientSequence == selectionSequence
                && main.ObservedPendingCommandCount == 0 && main.ObservedLastQueuedSequence == selectionSequence,
                "An unavailable Smith service was enabled or queued a command.");
            var refusedPlayer = main.ObservedSnapshot!.Players.Single();
            var refusedItem = refusedPlayer.Equipment.Single(equipped => equipped.Slot == expected.EquipmentSlot
                && equipped.State.StableItemId == expected.StableItemId && equipped.ItemSeed == expected.ItemSeed);
            Require(refusedPlayer.Gold == goldBefore && refusedItem.State.Durability == durabilityBefore
                && HashProto(refusedItem.State) == stateBeforeHash,
                "The refused Smith repair changed Gold, durability or the native item's state.");
        }
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, sequence, "Close Town Smith after native service check");
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.LevelId == 0 && player.ActiveStoreId == 0 && main.ObservedSnapshot.ActiveStore is null
            && player.Gold <= expected.Gold,
            "The native Town service check did not finish cleanly or increased Gold.");
        var finalItem = player.Equipment.Single(equipped => equipped.Slot == expected.EquipmentSlot
            && equipped.State.StableItemId == expected.StableItemId && equipped.ItemSeed == expected.ItemSeed);
        Require(repaired ? player.Gold == goldBefore - quote.Price
                : player.Gold == goldBefore && HashProto(finalItem.State) == stateBeforeHash,
            "Closing the Smith changed the exact paid or refused repair outcome.");
        return new NativeRepairEvidence(1, quote.RejectReason.ToString(), quote.Price,
            repairButtonDisabled, repaired, goldBefore, player.Gold, expected.ItemSeed,
            stateBeforeHash, HashProto(finalItem.State), durabilityBefore, finalItem.State.Durability,
            maximumDurability);
    }

    private static void VerifyNativeL1RetainedLoot(Main main, NativePopulationCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        Require(snapshot.Players.Single().LevelId == 1
            && snapshot.Monsters.All(monster => monster.EntityId != expected.MonsterEntityId || !monster.Alive)
            && snapshot.Corpses.Count(corpse => corpse.EntityId == expected.MonsterEntityId && corpse.LevelId == 1) == 1
            && snapshot.WorldItems.All(item => item.EntityId != expected.DropEntityId),
            "The ordinary L1 revisit revived the native encounter, lost its corpse or restored claimed loot.");
    }

    private static void VerifyNativeDoorOperationStates(
        Main main,
        IReadOnlyCollection<NativeDoorOperationEvidence> expectedOperations)
        => VerifyNativeDoorOperationStates(main.ObservedSnapshot!, expectedOperations);

    private static void VerifyNativeDoorOperationStates(
        Snapshot snapshot,
        IReadOnlyCollection<NativeDoorOperationEvidence> expectedOperations)
    {
        foreach (var expected in expectedOperations) {
            var door = snapshot.Objects.SingleOrDefault(item => item.EntityId == expected.EntityId);
            Require(door is { LevelId: 1, CathedralDoor: not null }
                && door.CathedralDoor.NativeObjectId == expected.NativeObjectId
                && door.CathedralDoor.DoorState == expected.DoorStateAfter,
                $"The ordinary L1 revisit did not retain native door {expected.EntityId} state "
                + $"{expected.DoorStateAfter} (actual={door?.CathedralDoor?.DoorState}).");
        }
    }

    private static int CountNativeItemOccurrences(
        Main main,
        PlayerSnapshot player,
        uint stableItemId,
        uint itemSeed)
    {
        bool Matches(ItemStateSnapshot? state, uint seed)
            => state is not null && state.StableItemId == stableItemId && seed == itemSeed;

        return player.Inventory.Count(item => Matches(item.State, item.ItemSeed))
            + player.Belt.Count(item => Matches(item.State, item.ItemSeed))
            + player.Equipment.Count(item => Matches(item.State, item.ItemSeed))
            + player.Stash.Count(item => item.Item is not null && Matches(item.Item.State, item.Item.ItemSeed))
            + (player.HeldItem is not null && Matches(player.HeldItem.State, player.HeldItem.ItemSeed) ? 1 : 0)
            + main.ObservedSnapshot!.WorldItems.Count(item => Matches(item.State, item.ItemSeed));
    }

    private static bool IsUsableNativeEquipment(ItemStateSnapshot state, PlayerSnapshot player)
        => state.StableItemId != 0 && state.EquipLocation is >= 1 and <= 6
            && state.MinimumStrength <= player.Attributes.Strength.Current
            && state.MinimumMagic <= player.Attributes.Magic.Current
            && state.MinimumDexterity <= player.Attributes.Dexterity.Current;

    private static uint NativeEquipmentSlot(ItemStateSnapshot state, PlayerSnapshot player)
        => state.EquipLocation switch {
            1 or 2 => 4,
            3 => 6,
            4 => 0,
            5 => player.Equipment.Any(item => item.Slot == 1) ? 2U : 1U,
            6 => 3,
            _ => throw new InvalidDataException($"Item {state.StableItemId} has no supported equipment location."),
        };

    private static int FindNativeStashCell(PlayerSnapshot player, ItemStateSnapshot item)
    {
        var width = Math.Max(1, checked((int)item.InventoryWidth));
        var height = Math.Max(1, checked((int)item.InventoryHeight));
        var occupied = new HashSet<int>();
        foreach (var entry in player.Stash.Where(entry => entry.Page == 0)) {
            var existingCell = checked((int)entry.Cell);
            var existingWidth = Math.Max(1, checked((int)entry.Item.State.InventoryWidth));
            var existingHeight = Math.Max(1, checked((int)entry.Item.State.InventoryHeight));
            for (var y = 0; y < existingHeight; y++)
                for (var x = 0; x < existingWidth; x++)
                    occupied.Add(existingCell % 10 + x + (existingCell / 10 + y) * 10);
        }
        for (var cell = 0; cell < 100; cell++) {
            if (cell % 10 + width > 10 || cell / 10 + height > 10)
                continue;
            if (Enumerable.Range(0, height).All(y => Enumerable.Range(0, width)
                .All(x => !occupied.Contains(cell + x + y * 10))))
                return cell;
        }
        throw new InvalidOperationException("The first Town stash page has no free footprint for the native-earned item.");
    }

    private static long NativeDistance(int startX, int startY, int endX, int endY)
        => Math.Max(Math.Abs((long)startX - endX), Math.Abs((long)startY - endY));

    private readonly record struct NativeRangedLaneCandidate(
        int X, int Y, int Steps, long ThreatClearance);

    private sealed class NativeCombatWorkBudget(bool enforceCumulativeTargetLimit = false)
    {
        public const int MaximumWork = NativeGearHuntMaximumCombatWork;

        private readonly Dictionary<uint, int> _targetWork = [];
        private readonly Dictionary<uint, NativeCombatTargetBaseline> _targetBaselines = [];
        private readonly Dictionary<uint, List<NativeAcceptedShotEvidence>> _acceptedShots = [];
        private readonly Dictionary<uint, List<NativeAttackRefusalEvidence>> _refusalsByTarget = [];

        public int Used { get; private set; }

        public List<NativeAttackRefusalEvidence> AttackRefusals { get; } = [];

        public NativeCombatTargetBaseline GetTargetBaseline(uint targetId, uint levelId, Snapshot snapshot)
        {
            if (_targetBaselines.TryGetValue(targetId, out var baseline)) {
                if (baseline.LevelId != levelId)
                    throw new InvalidOperationException(
                        $"Native combat target {targetId} changed levels during its bounded encounter.");
                return baseline;
            }

            baseline = new NativeCombatTargetBaseline(levelId, snapshot.Players.Single().Experience,
                snapshot.WorldItems.Where(item => item.LevelId == levelId).Select(item => item.EntityId).ToHashSet(),
                snapshot.Corpses.Where(corpse => corpse.LevelId == levelId).Select(corpse => corpse.EntityId).ToHashSet());
            _targetBaselines.Add(targetId, baseline);
            return baseline;
        }

        public IReadOnlyList<NativeAcceptedShotEvidence> AcceptedShotsFor(uint targetId)
            => _acceptedShots.TryGetValue(targetId, out var shots) ? shots : Array.Empty<NativeAcceptedShotEvidence>();

        public IReadOnlyList<NativeAttackRefusalEvidence> RefusalsFor(uint targetId)
            => _refusalsByTarget.TryGetValue(targetId, out var refusals)
                ? refusals : Array.Empty<NativeAttackRefusalEvidence>();

        public void RecordAcceptedShot(NativeAcceptedShotEvidence shot)
        {
            if (!_acceptedShots.TryGetValue(shot.TargetEntityId, out var shots))
                _acceptedShots.Add(shot.TargetEntityId, shots = []);
            if (shots.Count >= NativeGearHuntMaximumAttacksPerTarget)
                throw new InvalidOperationException(
                    $"Native target {shot.TargetEntityId} exceeded its bounded accepted-shot ledger.");
            shots.Add(shot);
        }

        public void RecordRefusal(NativeAttackRefusalEvidence refusal)
        {
            AttackRefusals.Add(refusal);
            if (!_refusalsByTarget.TryGetValue(refusal.TargetEntityId, out var refusals))
                _refusalsByTarget.Add(refusal.TargetEntityId, refusals = []);
            refusals.Add(refusal);
        }

        public void Consume(uint targetId, int targetWork)
        {
            var action = $"attack work on target {targetId} step {targetWork}";
            EnsureAvailable(action);
            var targetUsed = _targetWork.GetValueOrDefault(targetId);
            if (enforceCumulativeTargetLimit && targetUsed >= NativeGearHuntMaximumAttacksPerTarget)
                throw new InvalidOperationException(
                    $"The native combat target {targetId} exhausted its shared "
                    + $"{NativeGearHuntMaximumAttacksPerTarget}-step work limit before {action}.");
            _targetWork[targetId] = targetUsed + 1;
            Used++;
        }

        public void ConsumeExploration(string action)
        {
            EnsureAvailable(action);
            Used++;
        }

        private void EnsureAvailable(string action)
        {
            if (Used >= MaximumWork) {
                throw new InvalidOperationException(
                    $"The native gear hunt exhausted its shared {MaximumWork}-step combat-work ceiling "
                    + $"before {action}; no more combat or exploration work is permitted.");
            }
        }
    }

    private sealed record NativeCombatTargetBaseline(uint LevelId, uint ExperienceBefore,
        HashSet<uint> StartingItems, HashSet<uint> StartingCorpses);

    private sealed record NativeKillEvidence(uint MonsterEntityId, uint MonsterId, uint ExperienceBefore,
        uint ExperienceAfter, int DeathX, int DeathY, ulong Sequence, uint[] DropEntityIds,
        NativeEncounterCorpseEvidence[] EncounterCorpses, NativeAcceptedShotEvidence[] AcceptedShots,
        NativeAttackRefusalEvidence[] AttackRefusals, string DropAttribution);

    private sealed record NativeEncounterCorpseEvidence(uint EntityId, uint LevelId, int PositionX, int PositionY);
    private sealed record NativeAcceptedShotEvidence(uint TargetEntityId, ulong Sequence, int AimX, int AimY,
        ulong AppliedTick);
    private sealed record NativeAttackRefusalEvidence(uint TargetEntityId, ulong Sequence, ulong AppliedTick,
        string RejectReason, ulong ObservedTick, int Life, string CombatMode, string? Action, string Classification);

    private sealed record NativeEquipmentKillEvidence(uint MonsterEntityId, uint MonsterId,
        uint ExperienceBefore, uint ExperienceAfter, uint DropEntityId, uint ItemSeed,
        uint StableItemId, int NativeItemIndex, string ItemName, uint EquipmentSlot,
        NativeKillEvidence[] Kills, NativeDoorOperationEvidence[] DoorOperations,
        NativeAttackRefusalEvidence[] AttackRefusals);

    private sealed record NativeDoorOperationEvidence(uint EntityId, uint NativeObjectId,
        int X, int Y, int DoorStateBefore, int DoorStateAfter, bool Opened,
        ulong Sequence, ulong AppliedTick, string GeometryBeforeSha256, string GeometryAfterSha256);

    private sealed record NativePopulationCheckpoint(string AdapterId, string PopulationPolicy,
        string PopulationMetadataFingerprint, string ContentHash, string RulesetHash,
        uint EntityId, string CharacterName, uint ClassId, uint CharacterLevel,
        uint LevelId, int X, int Y, int Life, int LifeMaximum, int Mana, int ManaMaximum,
        uint Experience, uint Gold, uint StashGold, uint MonsterEntityId, uint MonsterId,
        uint DropEntityId, uint ItemSeed, uint StableItemId, int NativeItemIndex, string ItemName,
        uint EquipmentSlot, string ItemStateSha256, string InventorySha256, string BeltSha256,
        string EquipmentSha256, string GeometrySha256, ulong Sequence);

    private sealed record NativeStashEvidence(bool Completed, int Cell, string ItemStateSha256);
    private sealed record NativePlayerItemIdentity(string InventorySha256, string BeltSha256,
        string EquipmentSha256, string StashSha256, string HeldItemSha256);
    private sealed record NativeHealerEvidence(uint HealerId, ulong Sequence, ulong AppliedTick,
        string Status, string RejectReason, int LifeBefore, int LifeAfter, int LifeMaximum,
        int ManaBefore, int ManaAfter, uint GoldBefore, uint GoldAfter, uint StashGoldBefore,
        uint StashGoldAfter, uint ExperienceBefore, uint ExperienceAfter,
        NativePlayerItemIdentity ItemsBefore, NativePlayerItemIdentity ItemsAfter);
    private sealed record NativeRepairEvidence(uint StoreId, string Reason, uint Price,
        bool Disabled, bool Repaired, uint GoldBefore, uint GoldAfter, uint ItemSeed,
        string ItemStateBeforeSha256, string ItemStateAfterSha256, int DurabilityBefore,
        int DurabilityAfter, int MaximumDurability);
    private sealed record NativeReturnMovementEvidence(ulong Sequence, ulong AppliedTick,
        string Status, string RejectReason, int X, int Y);
}
