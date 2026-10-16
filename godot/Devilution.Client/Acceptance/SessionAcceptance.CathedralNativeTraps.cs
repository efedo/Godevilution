using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;

namespace Devilution.Client.Acceptance;

/** Real-control native trap disarm and fresh-host retention on the pinned L1 profile. */
public partial class SessionAcceptance
{
    private const uint NativeTrapTargetEntityId = 1778404889;

    private async Task VerifyCathedralNativeTrapsAsync(
        Main main, string directory, string phase, bool visible, Evidence? previous)
    {
        Require(!visible, "Native trap acceptance is headless only.");
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath), "Native trap acceptance requires its pinned population profile.");
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        VerifyNativeProfileMarkers(profile.RootElement);
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("Native trap acceptance has no admitted server identity.");
        Require(identity.GameMode == GameMode.SinglePlayer,
            "Native trap acceptance requires the pinned single-player population.");
        var routeDirectory = System.IO.Path.Combine(directory, phase.ToLowerInvariant());
        System.IO.Directory.CreateDirectory(routeDirectory);

        if (phase == "CathedralNativeTrapJourney") {
            Require(previous is { Passed: true, LevelId: 0, Sequence: 539 },
                "Native trap journey requires the passed R14 Town cursor-539 checkpoint.");
            var priorEvidence = previous ?? throw new InvalidDataException(
                "The R14 native barrel resume evidence is missing.");
            using var priorJourney = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativebarreljourney.json")));
            using var priorResume = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativebarrelresume.json")));
            var priorCheckpoint = priorJourney.RootElement.GetProperty("NativeBarrel");
            var priorPlayer = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(
                priorCheckpoint.GetProperty("Player").GetString()!));
            var priorTownResume = priorResume.RootElement.GetProperty("NativeBarrel");
            VerifyNativeBarrelResumeCursor(main, 539);
            var playerAtR14 = main.ObservedSnapshot!.Players.Single();
            var expectedR14 = priorPlayer.Clone();
            expectedR14.PositionX = priorEvidence.X;
            expectedR14.PositionY = priorEvidence.Y;
            expectedR14.Life = priorTownResume.GetProperty("FinalLife").GetInt32();
            expectedR14.Mana = priorTownResume.GetProperty("FinalMana").GetInt32();
            expectedR14.Gold = priorTownResume.GetProperty("FinalGold").GetUInt32();
            expectedR14.Experience = priorTownResume.GetProperty("FinalExperience").GetUInt32();
            VerifyNativeSarcophagusTownBaseline(playerAtR14, expectedR14);
            Require(identity.ContentManifestHash == priorCheckpoint.GetProperty("ContentHash").GetString()
                && identity.RulesetIdentityHash == priorCheckpoint.GetProperty("RulesetHash").GetString()
                && main.ObservedSnapshot.RandomState == priorCheckpoint.GetProperty("RandomState").GetUInt32()
                && priorEvidence.EntityId == playerAtR14.EntityId
                && priorEvidence.LevelId == playerAtR14.LevelId
                && priorEvidence.X == playerAtR14.PositionX && priorEvidence.Y == playerAtR14.PositionY
                && priorTownResume.GetProperty("FinalLife").GetInt32() == playerAtR14.Life
                && priorTownResume.GetProperty("FinalMana").GetInt32() == playerAtR14.Mana
                && priorTownResume.GetProperty("FinalGold").GetUInt32() == playerAtR14.Gold
                && priorTownResume.GetProperty("FinalExperience").GetUInt32() == playerAtR14.Experience
                && priorEvidence.ContentHash == identity.ContentManifestHash
                && priorEvidence.RulesetHash == identity.RulesetIdentityHash
                && priorResume.RootElement.GetProperty("Passed").GetBoolean()
                && priorResume.RootElement.GetProperty("Sequence").GetUInt64() == 539,
                "The copied R14 Town checkpoint changed identity, cursor, RNG, or the exact player state before trap input.");

            await TravelNativeCathedralRoute(main, routeDirectory, 1);
            var beforeSnapshot = main.ObservedSnapshot!;
            var beforePlayer = beforeSnapshot.Players.Single();
            Require(beforePlayer.AbilityKind == PlayerAbilityKind.TrapDisarm,
                "The native route requires the ordinary Rogue Trap Disarm ability.");
            var beforeTrap = beforeSnapshot.Objects.SingleOrDefault(value =>
                value.EntityId == NativeTrapTargetEntityId)
                ?? throw new InvalidDataException($"Pinned native trap target {NativeTrapTargetEntityId} is absent.");
            Require(beforeTrap.LevelId == 1 && beforeTrap.NativeObjectId == 2
                && beforeTrap.TrapArmed && beforeTrap.NativeTrapInteractable
                && !beforeTrap.Activated && !beforeTrap.Destroyed
                && beforeTrap.CathedralDoor is { DoorState: 0 }
                && AuthoritativeObjectSelector.IsValidNativeTrapInteraction(beforeTrap),
                "The R14 target is not the server-authorized armed native Cathedral door.");
            var approachKills = new List<NativeKillEvidence>();
            var approachVisited = new HashSet<uint>();
            var approachDoors = new HashSet<uint>();
            var approachDoorEvidence = new List<NativeDoorOperationEvidence>();
            var approachDoorCommands = new Dictionary<uint, int>();
            var approachWork = new NativeCombatWorkBudget();
            await MoveAdjacentToNativeChest(main, beforeTrap.EntityId, async pathBlocked => {
                var current = main.ObservedSnapshot!;
                var currentPlayer = current.Players.Single();
                var threat = current.Monsters.Where(value => value.LevelId == currentPlayer.LevelId
                        && value.Alive && !value.IsPlayerMinion && !approachVisited.Contains(value.EntityId)
                        && IsNativeThreatWithinScope(currentPlayer, value, beforeTrap.PositionX, beforeTrap.PositionY)
                        && HasNativeRangedLaneWithinApproach(main, current, currentPlayer, value,
                            NativeMaximumRangedApproachCommands))
                    .OrderBy(value => NativeMonsterDistance(currentPlayer, value))
                    .ThenBy(value => value.EntityId).FirstOrDefault();
                if (threat is not null) {
                    var killed = await DefeatNativeTargetAsync(main, threat.EntityId,
                        NativeGearHuntMaximumAttacksPerTarget, beforeTrap.PositionX, beforeTrap.PositionY,
                        approachWork);
                    if (killed is not null) {
                        approachVisited.Add(killed.MonsterEntityId);
                        approachKills.Add(killed);
                    } else {
                        approachWork.ConsumeExploration("reselect native trap approach threat");
                    }
                    // Replan after one target: do not chase other nearby monsters beyond a closed door.
                    return true;
                }
                return pathBlocked && approachDoors.Count < NativeMaximumDoorOperations
                    && await AdvanceTowardReachableNativeDoor(main, approachDoorCommands,
                        approachDoors, approachDoorEvidence, approachWork,
                        doorFilter: value => value.EntityId != beforeTrap.EntityId
                            && !value.TrapArmed && !value.NativeTrapInteractable);
            });
            var readySnapshot = main.ObservedSnapshot!;
            var readyPlayer = readySnapshot.Players.Single();
            var readyTrap = readySnapshot.Objects.Single(value => value.EntityId == beforeTrap.EntityId);
            Require(readyPlayer.LevelId == 1
                && Math.Abs(readyPlayer.PositionX - readyTrap.PositionX)
                    + Math.Abs(readyPlayer.PositionY - readyTrap.PositionY) == 1
                && readyPlayer.AbilityKind == PlayerAbilityKind.TrapDisarm
                && readyTrap.NativeTrapInteractable && readyTrap.TrapArmed
                && AuthoritativeObjectSelector.SelectAt(readySnapshot, readyTrap.PositionX, readyTrap.PositionY)?.EntityId
                    == readyTrap.EntityId,
                "Ordinary movement did not reach a selectable adjacent native trap target.");
            await Frame();
            var inputSnapshot = main.ObservedSnapshot!;
            var inputPlayer = inputSnapshot.Players.Single();
            var inputTrap = inputSnapshot.Objects.Single(value => value.EntityId == readyTrap.EntityId);
            Require(Settled(main) && inputPlayer.LevelId == 1
                && Math.Abs(inputPlayer.PositionX - inputTrap.PositionX)
                    + Math.Abs(inputPlayer.PositionY - inputTrap.PositionY) == 1
                && inputPlayer.AbilityKind == PlayerAbilityKind.TrapDisarm
                && inputTrap.TrapArmed && inputTrap.NativeTrapInteractable,
                "The player or native trap changed before the Rogue disarm control was pressed.");
            var readyGeometry = inputSnapshot.LevelGeometry.FirstOrDefault(level =>
                level.LevelId == inputPlayer.LevelId);
            var lightRadius = inputPlayer.LightRadius is >= 2 and <= 15 ? inputPlayer.LightRadius : 10;
            var firstVisibleArmedTrap = inputSnapshot.Objects
                .Where(value => value.TrapArmed
                    && AuthoritativeObjectPresentationVisibility.IsVisible(
                        value, inputPlayer.LevelId, readyGeometry,
                        inputPlayer.PositionX, inputPlayer.PositionY, lightRadius))
                .OrderBy(value => value.EntityId)
                .FirstOrDefault();
            Require(firstVisibleArmedTrap?.EntityId == inputTrap.EntityId,
                "The existing Rogue disarm control would target a different visible armed trap.");
            var hud = main.GetChildren().OfType<HudPresentation>().Single();
            var disarmButton = FindHudButton(hud, "Disarm trap (Rogue)");
            Require(disarmButton.IsInsideTree() && disarmButton.IsVisibleInTree() && !disarmButton.Disabled,
                "The existing Rogue Disarm Trap control is unavailable for the owned native target.");
            var priorSequence = main.ObservedLastQueuedSequence;
            var objectBefore = inputTrap.Clone();
            var geometryBefore = HashProto(DepthGeometry(main, 1));
            var projectileHashBefore = HashNativeTrapProjectiles(inputSnapshot);
            disarmButton.EmitSignal("pressed");
            await AcceptedAndSettled(main, priorSequence, "Disarm and operate the native Cathedral trap door");
            var receipt = main.ObservedCommandResult
                ?? throw new InvalidDataException("The native Rogue Disarm Trap button produced no command receipt.");
            Require(receipt.ClientSequence == checked(priorSequence + 1)
                && receipt.Status == CommandStatus.Accepted
                && receipt.RejectReason == CommandRejectReason.Unspecified
                && receipt.AppliedTick > 0,
                $"The ordinary native DisarmTrap action did not receive its exact accepted receipt: "
                + $"{receipt.ClientSequence}/{receipt.Status}/{receipt.RejectReason}@{receipt.AppliedTick}.");
            await Until(() => main.ObservedSnapshot!.Objects.SingleOrDefault(value =>
                    value.EntityId == readyTrap.EntityId)?.CathedralDoor is { DoorState: not 0 }
                && !main.ObservedSnapshot.Objects.Single(value => value.EntityId == readyTrap.EntityId)
                    .NativeTrapInteractable,
                "The accepted native trap command did not open the door and clear its armed capability.", 15);
            var afterSnapshot = main.ObservedSnapshot!;
            var afterTrap = afterSnapshot.Objects.Single(value => value.EntityId == readyTrap.EntityId);
            Require(afterTrap.CathedralDoor is { DoorState: 1 or 2 }
                && !afterTrap.NativeTrapInteractable
                && HashProto(afterTrap) != HashProto(objectBefore)
                && HashProto(DepthGeometry(main, 1)) != geometryBefore,
                "The accepted native operation did not publish the real opened-door projection and geometry.");

            await TravelNativeCathedralRoute(main, routeDirectory, 0);
            await DrainNativeBarrelTownCheckpoint(main,
                (Button)main.FindChild("CancelTravel", true, false),
                main.FindChild("TravelStatus", true, false) as Label);
            var checkpointSnapshot = main.ObservedSnapshot!.Clone();
            var checkpointPlayer = checkpointSnapshot.Players.Single();
            Require(checkpointPlayer.LevelId == 0 && checkpointPlayer.Life > 0
                && main.ObservedPendingCommandCount == 0 && Settled(main),
                "The native trap journey did not reach a settled live Town checkpoint.");
            var checkpoint = new NativeTrapTownCheckpoint(
                identity.ContentManifestHash,
                identity.RulesetIdentityHash ?? throw new InvalidDataException("Missing native trap ruleset identity."),
                539,
                main.ObservedCommandResult!.ClientSequence,
                Convert.ToBase64String(checkpointPlayer.ToByteArray()),
                checkpointSnapshot.RandomState,
                readyTrap.EntityId,
                readyTrap.NativeObjectId,
                readyTrap.PositionX,
                readyTrap.PositionY,
                objectBefore.TrapArmed,
                objectBefore.NativeTrapInteractable,
                objectBefore.CathedralDoor!.DoorState,
                afterTrap.TrapArmed,
                afterTrap.NativeTrapInteractable,
                afterTrap.Activated,
                afterTrap.CathedralDoor!.DoorState,
                HashProto(afterTrap),
                HashProto(DepthGeometry(main, 1)),
                HashNativeTrapProjectiles(checkpointSnapshot),
                HashNativeChestWorldItems(checkpointSnapshot),
                HashNativeBarrelCorpses(checkpointSnapshot),
                HashNativeBarrelMonsters(checkpointSnapshot),
                receipt.ClientSequence,
                receipt.AppliedTick,
                "DisarmTrapRequested",
                projectileHashBefore,
                approachKills.ToArray(),
                approachDoorEvidence.ToArray());
            WriteEvidence(directory, "cathedralnativetrapjourney.json", new {
                Passed = true,
                Mode = identity.GameMode.ToString(),
                ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash,
                EntityId = checkpointPlayer.EntityId,
                ClassId = checkpointPlayer.CharacterClassId,
                LevelId = checkpointPlayer.LevelId,
                X = checkpointPlayer.PositionX,
                Y = checkpointPlayer.PositionY,
                Sequence = checkpoint.Sequence,
                Visible = visible,
                NativeTrap = checkpoint,
            });
            return;
        }

        Require(phase == "CathedralNativeTrapResume" && previous is { Passed: true, LevelId: 0 },
            "Native trap resume requires its passed Town checkpoint.");
        var savedEvidence = previous ?? throw new InvalidDataException(
            "The saved native trap journey evidence is missing.");
        using var journey = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativetrapjourney.json")));
        var expected = journey.RootElement.GetProperty("NativeTrap").Deserialize<NativeTrapTownCheckpoint>()
            ?? throw new InvalidDataException("The saved native trap checkpoint is empty.");
        VerifyNativeBarrelResumeCursor(main, expected.Sequence);
        var savedPlayer = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.Player));
        var resumedPlayer = main.ObservedSnapshot!.Players.Single();
        VerifyNativeSarcophagusTownBaseline(resumedPlayer, savedPlayer);
        Require(identity.ContentManifestHash == expected.ContentHash
            && identity.RulesetIdentityHash == expected.RulesetHash
            && main.ObservedSnapshot.RandomState == expected.RandomState
            && savedEvidence.Sequence == expected.Sequence
            && savedEvidence.EntityId == resumedPlayer.EntityId
            && savedEvidence.LevelId == resumedPlayer.LevelId
            && savedEvidence.X == resumedPlayer.PositionX && savedEvidence.Y == resumedPlayer.PositionY
            && savedEvidence.ContentHash == identity.ContentManifestHash
            && savedEvidence.RulesetHash == identity.RulesetIdentityHash,
            "Fresh-host native trap resume changed the saved profile identity, cursor, or RNG.");
        VerifyNativeTrapWorld(main.ObservedSnapshot!, expected, exactLiveState: true);
        var healer = await UseNativeTownHealer(main);
        VerifyNativeTrapWorld(main.ObservedSnapshot!, expected, exactLiveState: true);
        await TravelNativeCathedralRoute(main, routeDirectory, 1);
        var retainedTrap = VerifyNativeTrapWorld(main.ObservedSnapshot!, expected, exactLiveState: false);
        Require(!retainedTrap.NativeTrapInteractable
            && retainedTrap.CathedralDoor is { DoorState: 1 or 2 },
            "The spent native trap regained its armed capability after cold restore.");
        await TravelNativeCathedralRoute(main, routeDirectory, 0);
        await DrainNativeBarrelTownCheckpoint(main,
            (Button)main.FindChild("CancelTravel", true, false),
            main.FindChild("TravelStatus", true, false) as Label);
        var finalPlayer = main.ObservedSnapshot!.Players.Single();
        Require(finalPlayer.LevelId == 0 && finalPlayer.Life > 0 && Settled(main),
            "Native trap cold-resume journey did not return alive to Town.");
        WriteEvidence(directory, "cathedralnativetrapresume.json", new {
            Passed = true,
            Mode = identity.GameMode.ToString(),
            ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash,
            EntityId = finalPlayer.EntityId,
            ClassId = finalPlayer.CharacterClassId,
            LevelId = finalPlayer.LevelId,
            X = finalPlayer.PositionX,
            Y = finalPlayer.PositionY,
            Sequence = main.ObservedCommandResult!.ClientSequence,
            Visible = visible,
            NativeTrap = new {
                SavedSequence = expected.Sequence,
                FinalSequence = main.ObservedCommandResult.ClientSequence,
                ResumedSessionMatched = true,
                L1RetentionVerified = true,
                ExactWorldRestoreCheckedBeforeInput = true,
                LiveStateAllowedToAdvanceOnRevisit = true,
                RevisitLevelId = retainedTrap.LevelId,
                TrapObjectSha256 = expected.TrapObjectSha256,
                L1GeometrySha256 = expected.L1GeometrySha256,
                ProjectilesSha256 = expected.ProjectilesSha256,
                WorldItemsSha256 = expected.WorldItemsSha256,
                CorpsesSha256 = expected.CorpsesSha256,
                MonstersSha256 = expected.MonstersSha256,
                RepeatDisarmCapabilityUnavailable = true,
                DisarmIntent = expected.DisarmIntent,
                Healer = healer,
                FinalLife = finalPlayer.Life,
                FinalMana = finalPlayer.Mana,
                FinalGold = finalPlayer.Gold,
                FinalExperience = finalPlayer.Experience,
            },
        });
    }

    private static ObjectSnapshot VerifyNativeTrapWorld(
        Snapshot snapshot, NativeTrapTownCheckpoint expected, bool exactLiveState)
    {
        var trap = snapshot.Objects.SingleOrDefault(value => value.EntityId == expected.TrapEntityId)
            ?? throw new InvalidDataException("Fresh-host native trap resume lost its original target.");
        Require(trap.NativeObjectId == expected.NativeObjectId
            && trap.PositionX == expected.X && trap.PositionY == expected.Y
            && trap.TrapArmed == expected.TrapArmedAfter
            && trap.NativeTrapInteractable == expected.NativeTrapInteractableAfter
            && trap.Activated == expected.ActivatedAfter
            && trap.CathedralDoor?.DoorState == expected.DoorStateAfter
            && HashProto(trap) == expected.TrapObjectSha256
            && HashProto(DepthGeometryFromSnapshot(snapshot, 1)) == expected.L1GeometrySha256
            && HashNativeChestWorldItems(snapshot) == expected.WorldItemsSha256
            && HashNativeBarrelCorpses(snapshot) == expected.CorpsesSha256
            && (!exactLiveState || HashNativeTrapProjectiles(snapshot) == expected.ProjectilesSha256
                && HashNativeBarrelMonsters(snapshot) == expected.MonstersSha256),
            exactLiveState
                ? "Fresh-host Town admission changed the exact saved native world before input."
                : "The live L1 revisit changed retained trap, door, item or corpse outcomes.");
        VerifyNativeDoorOperationStates(snapshot, expected.ApproachDoors);
        return trap;
    }

    private static Button FindHudButton(Node root, string text)
    {
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.TryPop(out var node)) {
            if (node is Button button && button.Text == text)
                return button;
            foreach (var child in node.GetChildren())
                pending.Push(child);
        }
        throw new InvalidDataException($"HUD button '{text}' is missing.");
    }

    private static string HashNativeTrapProjectiles(Snapshot snapshot)
        => HashProtoCollection(snapshot.Projectiles.OrderBy(value => value.EntityId)
            .Select(value => value.ToByteArray()));

    private sealed record NativeTrapTownCheckpoint(
        string ContentHash,
        string RulesetHash,
        ulong PriorSequence,
        ulong Sequence,
        string Player,
        uint RandomState,
        uint TrapEntityId,
        uint NativeObjectId,
        int X,
        int Y,
        bool TrapArmedBefore,
        bool NativeTrapInteractableBefore,
        int DoorStateBefore,
        bool TrapArmedAfter,
        bool NativeTrapInteractableAfter,
        bool ActivatedAfter,
        int DoorStateAfter,
        string TrapObjectSha256,
        string L1GeometrySha256,
        string ProjectilesSha256,
        string WorldItemsSha256,
        string CorpsesSha256,
        string MonstersSha256,
        ulong DisarmSequence,
        ulong DisarmAppliedTick,
        string DisarmIntent,
        string ProjectilesBeforeSha256,
        NativeKillEvidence[] ApproachKills, NativeDoorOperationEvidence[] ApproachDoors);
}
