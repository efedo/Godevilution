using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/** Ordinary native sarcophagus controls, retained object/loot/actor state and fresh-host resume. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralNativeSarcophagiAsync(
        Main main, string directory, string phase, bool visible, Evidence? previous)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath), "Native sarcophagus acceptance requires its pinned profile.");
        using var profileDocument = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        var profile = profileDocument.RootElement;
        VerifyNativeProfileMarkers(profile);
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("Native sarcophagus acceptance has no admitted identity.");
        Require(identity.GameMode == GameMode.SinglePlayer
            && profile.GetProperty("nativePopulation").GetBoolean()
            && !profile.GetProperty("nativePopulationClaim").GetBoolean()
            && identity.ContentManifestHash == profile.GetProperty("contentManifestHash").GetString(),
            "Native sarcophagus acceptance did not admit the pinned unclaimed single-player profile.");
        var routeDirectory = System.IO.Path.Combine(directory, phase.ToLowerInvariant());
        System.IO.Directory.CreateDirectory(routeDirectory);

        if (phase == "CathedralNativeSarcophagusJourney") {
            var prior = previous ?? throw new InvalidDataException("Sarcophagus journey requires the passed chest Town save.");
            using var oldDocument = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativechestresume.json")));
            var oldRoot = oldDocument.RootElement;
            var old = oldRoot.GetProperty("NativeChest");
            var savedSequence = oldRoot.GetProperty("Sequence").GetUInt64();
            var player = main.ObservedSnapshot!.Players.Single();
            VerifyNativeSarcophagusResumeCursor(main, savedSequence);
            Require(prior.Passed && prior.LevelId == 0 && prior.Sequence == savedSequence
                && oldRoot.GetProperty("Passed").GetBoolean()
                && old.GetProperty("Passed").GetBoolean()
                && old.GetProperty("FinalSequence").GetUInt64() == savedSequence
                && player.EntityId == oldRoot.GetProperty("EntityId").GetUInt32()
                && player.LevelId == 0 && player.PositionX == oldRoot.GetProperty("X").GetInt32()
                && player.PositionY == oldRoot.GetProperty("Y").GetInt32()
                && player.Life == old.GetProperty("FinalLife").GetInt32()
                && player.Mana == old.GetProperty("FinalMana").GetInt32()
                && player.Experience == old.GetProperty("FinalExperience").GetUInt32()
                && player.Gold == old.GetProperty("FinalGold").GetUInt32()
                && player.StashGold == old.GetProperty("FinalStashGold").GetUInt32()
                && CaptureNativePlayerItemIdentity(player) is var items
                && items.InventorySha256 == old.GetProperty("FinalInventorySha256").GetString()
                && items.BeltSha256 == old.GetProperty("FinalBeltSha256").GetString()
                && items.EquipmentSha256 == old.GetProperty("FinalEquipmentSha256").GetString(),
                "The old chest Town checkpoint did not retain its final identity, cursor, resources and items.");
            var geometryHash = old.GetProperty("L1GeometrySha256").GetString()!;
            Require(HashProto(DepthGeometry(main, 1)) == geometryHash
                && HashNativeChestWorldItems(main.ObservedSnapshot!) == old.GetProperty("FinalL1WorldItemsSha256").GetString(),
                "The old chest cohort lost retained L1 geometry or item state before sarcophagus input.");
            await TravelNativeCathedralRoute(main, routeDirectory, 1);
            var opened = new List<NativeSarcophagusOpenEvidence>();
            var excluded = new HashSet<uint>();
            var combatWork = new NativeCombatWorkBudget();
            while (true) {
                var candidate = FindNextNativeSarcophagus(main.ObservedSnapshot!, excluded);
                Require(candidate is not null, "No reachable closed native sarcophagus remains for the ordinary route.");
                await MoveAdjacentToNativeChest(main, candidate!.EntityId);
                var before = main.ObservedSnapshot!;
                var beforeObject = before.Objects.Single(value => value.EntityId == candidate.EntityId);
                var itemIds = before.WorldItems.Select(value => value.EntityId).ToHashSet();
                var actorIds = before.Monsters.Select(value => value.EntityId).ToHashSet();
                var sequence = main.ObservedCommandResult!.ClientSequence;
                Require(beforeObject.NativeSarcophagusInteractable && !beforeObject.Activated
                    && AuthoritativeObjectSelector.SelectAt(before, beforeObject.PositionX, beforeObject.PositionY)?.EntityId == beforeObject.EntityId,
                    "The native sarcophagus was not selectable through its ordinary object capability.");
                await ClickWorld(main, beforeObject.PositionX, beforeObject.PositionY);
                await AcceptedAndSettled(main, sequence, "Open an ordinary native Cathedral sarcophagus");
                var openedReceipt = main.ObservedCommandResult!;
                var afterOpen = main.ObservedSnapshot!;
                var afterObject = afterOpen.Objects.Single(value => value.EntityId == candidate.EntityId);
                Require(afterObject.Activated && !afterObject.NativeSarcophagusInteractable
                    && !afterObject.Destroyed && afterObject.NativeObjectId == 48
                    && afterObject.AnimationFrame >= beforeObject.AnimationFrame
                    && !AuthoritativeObjectSelector.HasAuthoritativeInteraction(afterObject),
                    "The native sarcophagus did not publish its one-way opened state.");
                var loot = afterOpen.WorldItems.Where(value => value.LevelId == 1 && !itemIds.Contains(value.EntityId))
                    .OrderBy(value => value.EntityId).Select(CaptureNativeChestLoot).ToArray();
                var released = afterOpen.Monsters.Where(value => value.LevelId == 1 && !actorIds.Contains(value.EntityId))
                    .Select(value => value.EntityId).ToArray();
                var kills = new List<NativeKillEvidence>();
                foreach (var actorId in released) {
                    var killed = await DefeatNativeTargetAsync(main, actorId, NativeGearHuntMaximumAttacksPerTarget,
                        combatWorkBudget: combatWork);
                    Require(killed is not null, "The released native skeleton did not complete its bounded ordinary combat route.");
                    kills.Add(killed!);
                }
                var terminalFrame = await ObserveNativeSarcophagusAnimation(main, candidate.EntityId, beforeObject.AnimationFrame);
                Require(HashProto(DepthGeometry(main, 1)) == geometryHash,
                    "Opening a native sarcophagus changed its retained solid footprint or Cathedral geometry.");
                opened.Add(new(candidate.EntityId, beforeObject.PositionX, beforeObject.PositionY,
                    beforeObject.AnimationFrame, afterObject.AnimationFrame, terminalFrame,
                    openedReceipt.ClientSequence, openedReceipt.AppliedTick, loot, kills.ToArray()));
                excluded.Add(candidate.EntityId);
                // Pinned seed2588 has no linked skeleton sarcophagi. The native
                // oracle and live owner lifecycle tests cover that branch.
                if (opened.Any(value => value.Loot.Length > 0))
                    break;
            }
            await TravelNativeCathedralRoute(main, routeDirectory, 0);
            if (main.ObservedSnapshot!.Players.Single().Life < main.ObservedSnapshot.Players.Single().LifeMaximum)
                await UseNativeTownHealer(main);
            var town = main.ObservedSnapshot!.Players.Single();
            Require(town.LevelId == 0 && town.Life > 0, "The sarcophagus journey did not return alive to Town.");
            var checkpoint = new NativeSarcophagusTownCheckpoint(identity.ContentManifestHash,
                identity.RulesetIdentityHash ?? throw new InvalidDataException("Missing native ruleset identity."),
                savedSequence, main.ObservedCommandResult!.ClientSequence,
                Convert.ToBase64String(town.ToByteArray()), main.ObservedSnapshot.RandomState,
                geometryHash, HashNativeChestWorldItems(main.ObservedSnapshot), opened.ToArray());
            WriteEvidence(directory, "cathedralnativesarcophagusjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = town.EntityId, ClassId = town.CharacterClassId,
                LevelId = town.LevelId, X = town.PositionX, Y = town.PositionY, Sequence = checkpoint.Sequence,
                Visible = visible, SpawnBranchExercised = opened.Any(value => value.ReleasedSkeletons.Length > 0),
                NativeSarcophagus = checkpoint,
            });
            GD.Print($"Native sarcophagus Town checkpoint passed; sequence={checkpoint.Sequence}; opened={opened.Count}; loot={opened.Sum(value => value.Loot.Length)}; released={opened.Sum(value => value.ReleasedSkeletons.Length)}");
            return;
        }

        Require(phase == "CathedralNativeSarcophagusResume" && previous is { Passed: true, LevelId: 0 },
            "Native sarcophagus fresh-host resume requires its passed Town checkpoint.");
        using var journeyDocument = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativesarcophagusjourney.json")));
        var expected = journeyDocument.RootElement.GetProperty("NativeSarcophagus").Deserialize<NativeSarcophagusTownCheckpoint>()
            ?? throw new InvalidDataException("The native sarcophagus checkpoint is empty.");
        var expectedPlayer = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.Player));
        VerifyNativeSarcophagusResumeCursor(main, expected.Sequence);
        VerifyNativeSarcophagusTownBaseline(main.ObservedSnapshot!.Players.Single(), expectedPlayer);
        Require(expected.ContentHash == identity.ContentManifestHash && expected.RulesetHash == identity.RulesetIdentityHash
            && main.ObservedSnapshot.RandomState == expected.RandomState
            && HashProto(DepthGeometry(main, 1)) == expected.GeometrySha256
            && HashNativeChestWorldItems(main.ObservedSnapshot) == expected.WorldItemsSha256,
            "Fresh-host sarcophagus resume changed the saved identities, RNG, L1 geometry or item pool.");
        VerifyNativeSarcophagusRetention(main.ObservedSnapshot, expected);
        await TravelNativeCathedralRoute(main, routeDirectory, 1);
        Require(HashProto(DepthGeometry(main, 1)) == expected.GeometrySha256
            && HashNativeChestWorldItems(main.ObservedSnapshot!) == expected.WorldItemsSha256,
            "The actual L1 re-entry lost sarcophagus geometry or item state.");
        VerifyNativeSarcophagusRetention(main.ObservedSnapshot!, expected);
        var repeatTarget = expected.Opened[0];
        await MoveAdjacentToNativeChest(main, repeatTarget.EntityId);
        var beforeRepeat = main.ObservedSnapshot!;
        var actorIdsBefore = beforeRepeat.Monsters.Select(value => value.EntityId).ToHashSet();
        Require(!beforeRepeat.WorldItems.Any(value => value.LevelId == 1
            && value.PositionX == repeatTarget.X && value.PositionY == repeatTarget.Y),
            "The repeated sarcophagus click overlaps selectable loot.");
        var previousQueued = main.ObservedLastQueuedSequence;
        var priorReceipt = main.ObservedCommandResult!.ClientSequence;
        await ClickWorld(main, repeatTarget.X, repeatTarget.Y);
        var queued = main.ObservedLastQueuedSequence;
        var repeatStatus = "NotQueued";
        var repeatReason = CommandRejectReason.Unspecified.ToString();
        if (queued > previousQueued) {
            Require(queued == checked(previousQueued + 1), "Repeated sarcophagus click queued more than one command.");
            await Until(() => main.ObservedCommandResult?.ClientSequence == queued
                && main.ObservedPendingCommandCount == 0 && Settled(main)
                && main.ObservedSnapshot!.Tick >= main.ObservedCommandResult.AppliedTick,
                "The ordinary opened-sarcophagus click did not settle.", 12);
            var receipt = main.ObservedCommandResult!;
            Require(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt)
                || receipt.Status == CommandStatus.Rejected
                    && receipt.RejectReason is CommandRejectReason.NotAllowed or CommandRejectReason.InvalidTarget,
                "The ordinary repeated click did not receive a valid fallback receipt.");
            repeatStatus = receipt.Status.ToString();
            repeatReason = receipt.RejectReason.ToString();
        } else {
            Require(main.ObservedCommandResult?.ClientSequence == priorReceipt,
                "A repeated click changed the authoritative receipt without queuing input.");
        }
        var afterRepeat = main.ObservedSnapshot!;
        Require(HashNativeChestWorldItems(afterRepeat) == expected.WorldItemsSha256
            && afterRepeat.Monsters.All(value => actorIdsBefore.Contains(value.EntityId))
            && (afterRepeat.Tick != beforeRepeat.Tick || afterRepeat.RandomState == beforeRepeat.RandomState),
            "Repeated sarcophagus input generated new loot/actors or consumed RNG without a world tick.");
        VerifyNativeSarcophagusRetention(afterRepeat, expected);
        await TravelNativeCathedralRoute(main, routeDirectory, 0);
        if (main.ObservedSnapshot!.Players.Single().Life < main.ObservedSnapshot.Players.Single().LifeMaximum)
            await UseNativeTownHealer(main);
        var finalTown = main.ObservedSnapshot!.Players.Single();
        Require(finalTown.LevelId == 0 && finalTown.Life > 0 && finalTown.EntityId == expectedPlayer.EntityId
            && finalTown.Gold == expectedPlayer.Gold && finalTown.StashGold == expectedPlayer.StashGold
            && finalTown.Experience == expectedPlayer.Experience
            && HashNativeChestWorldItems(main.ObservedSnapshot) == expected.WorldItemsSha256,
            "The resumed sarcophagus route did not return to Town with retained loot and expected wallet/XP.");
        WriteEvidence(directory, "cathedralnativesarcophagusresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = finalTown.EntityId, ClassId = finalTown.CharacterClassId,
            LevelId = finalTown.LevelId, X = finalTown.PositionX, Y = finalTown.PositionY,
            Sequence = main.ObservedCommandResult!.ClientSequence, Visible = visible,
            SpawnBranchExercised = expected.Opened.Any(value => value.ReleasedSkeletons.Length > 0),
            NativeSarcophagus = new {
                Passed = true, SavedSequence = expected.Sequence, ResumedSessionMatched = true,
                OpenedRetained = expected.Opened.Length,
                ExactLootRetained = expected.Opened.Sum(value => value.Loot.Length),
                ReleasedSkeletonCorpsesRetained = expected.Opened.Sum(value => value.ReleasedSkeletons.Length),
                GeometrySha256 = expected.GeometrySha256, WorldItemsSha256 = expected.WorldItemsSha256,
                RepeatClickDidNotReopen = true, RepeatStatus = repeatStatus, RepeatRejectReason = repeatReason,
                FinalSequence = main.ObservedCommandResult.ClientSequence,
                FinalLife = finalTown.Life, FinalMana = finalTown.Mana, FinalGold = finalTown.Gold,
                FinalExperience = finalTown.Experience,
            },
        });
        GD.Print($"Native sarcophagus cold resume passed; saved={expected.Sequence}; opened={expected.Opened.Length}; repeat={repeatStatus}/{repeatReason}");
    }

    private static void VerifyNativeSarcophagusResumeCursor(Main main, ulong sequence)
        => Require(main.ObservedInitialConnection?.ServerHello is { ResumedSessionMatched: true } hello
            && hello.LastClientSequence == sequence && main.ObservedLastQueuedSequence == 0
            && main.ObservedPendingCommandCount == 0 && Settled(main),
            "The native sarcophagus host did not restore its exact cursor before any resumed input.");

    private static void VerifyNativeSarcophagusTownBaseline(PlayerSnapshot actual, PlayerSnapshot expected)
        => Require(actual.EntityId == expected.EntityId && actual.CharacterName == expected.CharacterName
            && actual.CharacterClassId == expected.CharacterClassId && actual.CharacterLevel == expected.CharacterLevel
            && actual.LevelId == 0 && actual.PositionX == expected.PositionX && actual.PositionY == expected.PositionY
            && actual.Life == expected.Life && actual.LifeMaximum == expected.LifeMaximum
            && actual.Mana == expected.Mana && actual.ManaMaximum == expected.ManaMaximum
            && actual.Gold == expected.Gold && actual.StashGold == expected.StashGold
            && actual.Experience == expected.Experience
            && CaptureNativePlayerItemIdentity(actual) == CaptureNativePlayerItemIdentity(expected),
            "Fresh-host sarcophagus resume changed the complete saved Town baseline.");

    private static ObjectSnapshot? FindNextNativeSarcophagus(Snapshot snapshot, HashSet<uint> excluded)
    {
        var geometry = DepthGeometryFromSnapshot(snapshot, 1);
        return snapshot.Objects.Where(value => value.LevelId == 1 && value.NativeSarcophagusInteractable
                && !excluded.Contains(value.EntityId) && AuthoritativeObjectSelector.HasAuthoritativeInteraction(value))
            .Select(value => (Object: value, Path: FindNativeChestApproachPath(snapshot, geometry, value)))
            .Where(value => value.Path is not null).OrderBy(value => value.Path!.Count)
            .ThenBy(value => value.Object.EntityId).Select(value => value.Object).FirstOrDefault();
    }

    private async Task<uint> ObserveNativeSarcophagusAnimation(Main main, uint entityId, uint initialFrame)
    {
        var lastFrame = uint.MaxValue;
        ulong lastChange = 0;
        await Until(() => {
            var snapshot = main.ObservedSnapshot!;
            var value = snapshot.Objects.Single(item => item.EntityId == entityId);
            if (value.AnimationFrame != lastFrame) {
                lastFrame = value.AnimationFrame;
                lastChange = snapshot.Tick;
            }
            return value.Activated && !value.NativeSarcophagusInteractable
                && value.AnimationFrame > initialFrame && snapshot.Tick >= lastChange + 6;
        }, "The native sarcophagus opening animation did not advance and settle.", 15);
        return lastFrame;
    }

    private static void VerifyNativeSarcophagusRetention(Snapshot snapshot, NativeSarcophagusTownCheckpoint expected)
    {
        foreach (var operation in expected.Opened) {
            var value = snapshot.Objects.Single(item => item.EntityId == operation.EntityId);
            Require(value.NativeObjectId == 48 && value.LevelId == 1 && value.PositionX == operation.X && value.PositionY == operation.Y
                && value.Activated && !value.Destroyed && !value.NativeSarcophagusInteractable
                && value.AnimationFrame == operation.TerminalFrame && !AuthoritativeObjectSelector.HasAuthoritativeInteraction(value),
                "Fresh-host restore lost an opened native sarcophagus or its completed animation.");
            foreach (var loot in operation.Loot)
                VerifyNativeChestLoot(snapshot, loot);
            foreach (var released in operation.ReleasedSkeletons)
                Require(!snapshot.Monsters.Any(value => value.EntityId == released.MonsterEntityId && value.Alive)
                    && snapshot.Corpses.Any(value => value.EntityId == released.MonsterEntityId
                        && value.PositionX == released.DeathX && value.PositionY == released.DeathY),
                    "Fresh-host restore duplicated or revived a released native skeleton.");
        }
    }

    private sealed record NativeSarcophagusOpenEvidence(uint EntityId, int X, int Y, uint InitialFrame,
        uint OpenedFrame, uint TerminalFrame, ulong Sequence, ulong AppliedTick,
        NativeChestLootEvidence[] Loot, NativeKillEvidence[] ReleasedSkeletons);
    private sealed record NativeSarcophagusTownCheckpoint(string ContentHash, string RulesetHash,
        ulong PriorSequence, ulong Sequence, string Player, uint RandomState,
        string GeometrySha256, string WorldItemsSha256, NativeSarcophagusOpenEvidence[] Opened);
}
