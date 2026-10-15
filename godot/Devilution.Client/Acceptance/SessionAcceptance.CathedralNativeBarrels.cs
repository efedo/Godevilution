using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/** Ordinary native barrel melee, retained broken collision/loot and fresh-host resume. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralNativeBarrelsAsync(
        Main main, string directory, string phase, bool visible, Evidence? previous)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath), "Native barrel acceptance requires its pinned profile.");
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        VerifyNativeProfileMarkers(profile.RootElement);
        var identity = main.ObservedIdentity ?? throw new InvalidDataException("Missing native barrel identity.");
        Require(identity.GameMode == GameMode.SinglePlayer && !visible,
            "Native barrel acceptance requires headless single-player.");
        var routeDirectory = System.IO.Path.Combine(directory, phase.ToLowerInvariant());
        System.IO.Directory.CreateDirectory(routeDirectory);

        if (phase == "CathedralNativeBarrelJourney") {
            Require(previous is { Passed: true, LevelId: 0 }, "Barrels require a passed sarcophagus Town save.");
            using var prior = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativesarcophagusresume.json")));
            using var priorJourney = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedralnativesarcophagusjourney.json")));
            var priorRoot = prior.RootElement;
            var priorState = priorRoot.GetProperty("NativeSarcophagus");
            var priorSequence = priorRoot.GetProperty("Sequence").GetUInt64();
            VerifyNativeBarrelResumeCursor(main, priorSequence);
            var player = main.ObservedSnapshot!.Players.Single();
            var oldPlayer = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(
                priorJourney.RootElement.GetProperty("NativeSarcophagus").GetProperty("Player").GetString()!));
            VerifyNativeSarcophagusTownBaseline(player, oldPlayer);
            Require(previous!.Sequence == priorSequence && priorRoot.GetProperty("Passed").GetBoolean()
                && player.Life == priorState.GetProperty("FinalLife").GetInt32()
                && player.Mana == priorState.GetProperty("FinalMana").GetInt32()
                && player.Gold == priorState.GetProperty("FinalGold").GetUInt32()
                && player.Experience == priorState.GetProperty("FinalExperience").GetUInt32(),
                "The prior native Town save changed before barrel input.");
            await TravelNativeCathedralRoute(main, routeDirectory, 1);
            var originalWeapon = main.ObservedSnapshot!.Players.Single().Equipment.Single(value => value.Slot == 4).Clone();
            var initialObjects = main.ObservedSnapshot!.Objects.Where(value => value.LevelId == 1
                && value.NativeObjectId is 57 or 58).ToDictionary(value => value.EntityId, value => value.Clone());
            var operations = new List<NativeBarrelBreakEvidence>();
            var work = new NativeCombatWorkBudget();
            while (!operations.Any(value => value.NativeTypeId == 58)
                || !operations.Any(value => value.NativeTypeId == 57)) {
                var snapshot = main.ObservedSnapshot!;
                var geometry = DepthGeometryFromSnapshot(snapshot, 1);
                // The live pair checks both input paths. Native/owned-runtime
                // tests independently require actual loot and exact cold restore.
                var needsOrdinary = !operations.Any(value => value.NativeTypeId == 57);
                var needsExplosion = !operations.Any(value => value.NativeTypeId == 58);
                var candidate = snapshot.Objects.Where(value => value.LevelId == 1
                    && value.NativeObjectId is 57 or 58 && value.NativeBarrelMeleeBreakable
                    && (needsOrdinary && value.NativeObjectId == 57 || needsExplosion && value.NativeObjectId == 58)
                    && !value.Activated && !value.Destroyed)
                    .Select(value => (Object: value, Path: FindNativeChestApproachPath(snapshot, geometry, value)))
                    .Where(value => value.Path is not null).OrderBy(value => value.Path!.Count)
                    .ThenBy(value => value.Object.EntityId).Select(value => value.Object).FirstOrDefault();
                Require(candidate is not null, "No public reachable barrel remains for the required input path.");
                await MoveAdjacentToNativeBarrel(main, candidate!.EntityId);
                await UnequipNativeBarrelWeapon(main);
                var before = main.ObservedSnapshot!;
                var beforeIds = before.WorldItems.Select(value => value.EntityId).ToHashSet();
                var actorIds = before.Monsters.Select(value => value.EntityId).ToHashSet();
                var closed = before.Objects.Where(value => value.LevelId == 1
                    && value.NativeObjectId is 57 or 58 && value.NativeBarrelMeleeBreakable
                    && !value.Activated && !value.Destroyed)
                    .Select(value => value.EntityId).ToHashSet();
                var sequence = main.ObservedCommandResult!.ClientSequence;
                Require(main.ObservedSnapshot.Players.Single().Equipment.All(value => value.Slot != 4),
                    "The barrel must be struck with ordinary melee, not a bow projectile.");
                var strikePlayer = before.Players.Single();
                var strikeObject = before.Objects.Single(value => value.EntityId == candidate.EntityId);
                var selected = AuthoritativeTargetSelector.SelectCombatTarget(before,
                    strikePlayer.EntityId, candidate.PositionX, candidate.PositionY);
                GD.Print($"Native barrel strike: object={candidate.EntityId}@({candidate.PositionX},{candidate.PositionY}); "
                    + $"player=({strikePlayer.PositionX},{strikePlayer.PositionY}); selected={selected}; "
                    + $"capability={strikeObject.NativeBarrelMeleeBreakable}; "
                    + $"valid={AuthoritativeObjectSelector.IsValidNativeBarrelMeleeBreakable(strikeObject)}; "
                    + $"heldItem={strikePlayer.HeldItem is not null}; sequence={sequence}; "
                    + $"point={main.ObservedTileScreenPosition(candidate.PositionX, candidate.PositionY)}; "
                    + $"worldRect={((Control)main.FindChild("WorldArea", true, false)).GetGlobalRect()}");
                Require(selected == candidate.EntityId,
                    $"The public selector cannot target the adjacent native barrel {candidate.EntityId}; selected={selected}.");
                await ClickWorld(main, candidate.PositionX, candidate.PositionY, MouseButton.Right);
                try {
                    await AcceptedAndSettled(main, sequence, "Break native Cathedral barrel with ordinary melee");
                } catch (InvalidOperationException) {
                    var failed = main.ObservedSnapshot!.Players.Single();
                    GD.Print($"Native barrel strike failed: object={candidate.EntityId}; prior={sequence}; "
                        + $"queued={main.ObservedLastQueuedSequence}; pending={main.ObservedPendingCommandCount}; "
                        + $"receipt={main.ObservedCommandResult?.ClientSequence}/{main.ObservedCommandResult?.Status}/"
                        + $"{main.ObservedCommandResult?.RejectReason}; player=({failed.PositionX},{failed.PositionY}); "
                        + $"life={failed.Life}; mode={failed.CombatMode}; action={failed.CurrentAction}; "
                        + $"selected={AuthoritativeTargetSelector.SelectCombatTarget(main.ObservedSnapshot, failed.EntityId, candidate.PositionX, candidate.PositionY)}");
                    throw;
                }
                Require(main.ObservedSnapshot!.Players.Single().Life > 0,
                    "The hero did not survive the native barrel blast.");
                var breakSequence = main.ObservedCommandResult!.ClientSequence;
                var after = main.ObservedSnapshot!;
                var broken = after.Objects.Where(value => closed.Contains(value.EntityId) && value.Activated).ToArray();
                Require(broken.Any(value => value.EntityId == candidate.EntityId),
                    "The ordinary melee callback did not break the requested native barrel.");
                // Only ordinary57 can generate barrel loot; explosive new drops belong
                // to slain victims and remain covered by the complete world-item digest.
                var loot = candidate.NativeObjectId == 57
                    ? after.WorldItems.Where(value => value.LevelId == 1 && !beforeIds.Contains(value.EntityId))
                        .OrderBy(value => value.EntityId).Select(CaptureNativeChestLoot).ToArray()
                    : Array.Empty<NativeChestLootEvidence>();
                var releasedIds = after.Monsters.Where(value => value.LevelId == 1 && !actorIds.Contains(value.EntityId))
                    .Select(value => value.EntityId).ToArray();
                var kills = new List<NativeKillEvidence>();
                if (releasedIds.Length > 0) {
                    await ReequipNativeBarrelWeapon(main, originalWeapon);
                    foreach (var actorId in releasedIds) {
                        var killed = await DefeatNativeTargetAsync(main, actorId, NativeGearHuntMaximumAttacksPerTarget,
                            combatWorkBudget: work);
                        Require(killed is not null, "The barrel-released skeleton did not finish ordinary combat.");
                        kills.Add(killed!);
                    }
                }
                await ReequipNativeBarrelWeapon(main, originalWeapon);
                foreach (var value in broken) {
                    var frame = await ObserveNativeBarrelAnimation(main, value.EntityId);
                    var currentGeometry = DepthGeometry(main, 1);
                    Require(!currentGeometry.StaticObjectBlockedCells.Contains(
                            value.PositionY * currentGeometry.Width + value.PositionX),
                        "Broken barrel collision still blocks its native tile.");
                    operations.Add(new(value.EntityId, value.NativeObjectId, value.PositionX, value.PositionY,
                        initialObjects[value.EntityId].AnimationFrame, frame,
                        breakSequence,
                        value.EntityId == candidate.EntityId ? loot : [],
                        value.EntityId == candidate.EntityId ? kills.ToArray() : []));
                }
            }
            await TravelNativeCathedralRoute(main, routeDirectory, 0);
            if (main.ObservedSnapshot!.Players.Single().Life < main.ObservedSnapshot.Players.Single().LifeMaximum)
                await UseNativeTownHealer(main);
            var cancelTravel = main.FindChild("CancelTravel", true, false) as Button
                ?? throw new InvalidDataException("The native barrel checkpoint has no travel-state control.");
            var travelStatus = main.FindChild("TravelStatus", true, false) as Label;
            await DrainNativeBarrelTownCheckpoint(main, cancelTravel, travelStatus);

            // Capture the persisted boundary without awaiting between the final
            // queue, receipt, and snapshot reads, then recheck the live guards.
            var queuedBeforeCapture = main.ObservedLastQueuedSequence;
            var receipt = main.ObservedCommandResult?.Clone()
                ?? throw new InvalidDataException("The native barrel checkpoint has no final command receipt.");
            var checkpointSnapshot = main.ObservedSnapshot?.Clone()
                ?? throw new InvalidDataException("The native barrel checkpoint has no final snapshot.");
            var queuedAfterCapture = main.ObservedLastQueuedSequence;
            var checkpointPending = main.ObservedPendingCommandCount;
            var checkpointSettled = Settled(main);
            var checkpointTravelDisabled = cancelTravel.Disabled;
            var checkpointStatus = travelStatus?.Text ?? "<missing>";
            var town = checkpointSnapshot.Players.Single();
            Require(queuedBeforeCapture > 0 && queuedBeforeCapture == queuedAfterCapture
                && receipt.ClientSequence == queuedBeforeCapture
                && AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt) && receipt.AppliedTick > 0
                && checkpointSnapshot.Tick >= receipt.AppliedTick
                && main.ObservedLastQueuedSequence == queuedAfterCapture
                && main.ObservedCommandResult?.ClientSequence == receipt.ClientSequence
                && main.ObservedPendingCommandCount == 0 && checkpointPending == 0
                && checkpointSettled && Settled(main)
                && checkpointTravelDisabled && cancelTravel.Disabled,
                DescribeNativeBarrelCheckpointBoundary(main, cancelTravel, travelStatus,
                    receipt.ClientSequence, checkpointSnapshot.Tick));
            GD.Print($"Native barrel checkpoint drained: tick={checkpointSnapshot.Tick}; queued={queuedAfterCapture}; "
                + $"ack={receipt.ClientSequence}/{receipt.Status}/{receipt.RejectReason}@{receipt.AppliedTick}; "
                + $"pending={checkpointPending}; mode={town.CombatMode}; action={town.CurrentAction}; "
                + $"settled={checkpointSettled}; travelDisabled={checkpointTravelDisabled}; "
                + $"travelStatus={checkpointStatus}.");
            Require(town.LevelId == 0 && town.Life > 0, "The barrel journey did not return alive to Town.");
            Require(town.Equipment.Any(value => value.Slot == 4
                && value.ItemSeed == originalWeapon.ItemSeed
                && value.State.StableItemId == originalWeapon.State.StableItemId),
                "The exact native bow was not re-equipped before the Town checkpoint.");
            var checkpoint = new NativeBarrelTownCheckpoint(identity.ContentManifestHash,
                identity.RulesetIdentityHash ?? throw new InvalidDataException("Missing ruleset identity."),
                priorSequence, queuedAfterCapture,
                Convert.ToBase64String(town.ToByteArray()), checkpointSnapshot.RandomState,
                HashProto(checkpointSnapshot.LevelGeometry.Single(level => level.LevelId == 1)),
                HashNativeChestWorldItems(checkpointSnapshot),
                HashNativeBarrelCorpses(checkpointSnapshot), HashNativeBarrelMonsters(checkpointSnapshot),
                operations.ToArray(), operations.Any(value => value.Loot.Length > 0));
            WriteEvidence(directory, "cathedralnativebarreljourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = town.EntityId, ClassId = town.CharacterClassId,
                LevelId = town.LevelId, X = town.PositionX, Y = town.PositionY, Sequence = checkpoint.Sequence,
                Visible = visible, NativeBarrel = checkpoint,
            });
            GD.Print($"Native barrel Town checkpoint passed; sequence={checkpoint.Sequence}; broken={operations.Count}; loot={operations.Sum(value => value.Loot.Length)}");
            return;
        }

        Require(phase == "CathedralNativeBarrelResume" && previous is { Passed: true, LevelId: 0 },
            "Barrel resume requires its passed Town checkpoint.");
        using var journey = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativebarreljourney.json")));
        var expected = journey.RootElement.GetProperty("NativeBarrel").Deserialize<NativeBarrelTownCheckpoint>()
            ?? throw new InvalidDataException("Missing barrel checkpoint.");
        VerifyNativeBarrelResumeCursor(main, expected.Sequence);
        var savedPlayer = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.Player));
        var resumedPlayer = main.ObservedSnapshot!.Players.Single();
        VerifyNativeSarcophagusTownBaseline(resumedPlayer, savedPlayer);
        var savedWeapon = savedPlayer.Equipment.SingleOrDefault(value => value.Slot == 4);
        Require(savedWeapon is not null && resumedPlayer.Equipment.Any(value => value.Slot == 4
                && value.ItemSeed == savedWeapon.ItemSeed
                && value.State.StableItemId == savedWeapon.State.StableItemId),
            "The fresh-host Town save did not resume with the exact native bow equipped.");
        Require(identity.ContentManifestHash == expected.ContentHash && identity.RulesetIdentityHash == expected.RulesetHash
            && main.ObservedSnapshot.RandomState == expected.RandomState
            && HashNativeBarrelMonsters(main.ObservedSnapshot) == expected.MonstersSha256,
            "Fresh-host barrel resume changed identity or saved RNG.");
        VerifyNativeBarrelRetention(main.ObservedSnapshot, expected);
        await TravelNativeCathedralRoute(main, routeDirectory, 1);
        VerifyNativeBarrelRetention(main.ObservedSnapshot!, expected);
        var repeat = expected.Opened[0];
        await MoveAdjacentToNativeBarrel(main, repeat.EntityId);
        var repeatSequence = main.ObservedCommandResult!.ClientSequence;
        await ClickWorld(main, repeat.X, repeat.Y, MouseButton.Right);
        await Frame();
        await Frame();
        var repeatCommandQueued = main.ObservedLastQueuedSequence > repeatSequence;
        if (repeatCommandQueued) {
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > repeatSequence
                && main.ObservedPendingCommandCount == 0 && Settled(main),
                "The repeated broken-barrel click did not settle.", 10);
            var receipt = main.ObservedCommandResult!;
            Require(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt)
                || receipt.Status == CommandStatus.Rejected && receipt.RejectReason == CommandRejectReason.InvalidTarget,
                "The broken-barrel repeat returned an unexpected command result.");
        } else {
            Require(main.ObservedLastQueuedSequence == repeatSequence && Settled(main),
                "The unavailable broken-barrel target left an unsettled command.");
        }
        VerifyNativeBarrelRetention(main.ObservedSnapshot!, expected);
        await TravelNativeCathedralRoute(main, routeDirectory, 0);
        if (main.ObservedSnapshot!.Players.Single().Life < main.ObservedSnapshot.Players.Single().LifeMaximum)
            await UseNativeTownHealer(main);
        await DrainNativeBarrelTownCheckpoint(main,
            (Button)main.FindChild("CancelTravel", true, false),
            main.FindChild("TravelStatus", true, false) as Label);
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == 0 && final.Life > 0, "The resumed barrel journey did not return alive to Town.");
        VerifyNativeBarrelRetention(main.ObservedSnapshot, expected);
        WriteEvidence(directory, "cathedralnativebarrelresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = final.EntityId, ClassId = final.CharacterClassId,
            LevelId = final.LevelId, X = final.PositionX, Y = final.PositionY,
            Sequence = main.ObservedCommandResult!.ClientSequence, Visible = visible,
            NativeBarrel = new {
                Passed = true, SavedSequence = expected.Sequence, ResumedSessionMatched = true,
                LootBranchExercised = expected.LootBranchExercised,
                OpenedRetained = expected.Opened.Length, ExactLootRetained = expected.Opened.Sum(value => value.Loot.Length),
                ReleasedSkeletonCorpsesRetained = expected.Opened.Sum(value => value.ReleasedSkeletons.Length),
                GeometrySha256 = expected.GeometrySha256, WorldItemsSha256 = expected.WorldItemsSha256,
                RepeatDidNotBreakAgain = true, RepeatCommandQueued = repeatCommandQueued, FinalSequence = main.ObservedCommandResult.ClientSequence,
                FinalLife = final.Life, FinalMana = final.Mana, FinalGold = final.Gold, FinalExperience = final.Experience,
            },
        });
        GD.Print($"Native barrel cold resume passed; saved={expected.Sequence}; retained={expected.Opened.Length}");
    }

    private async Task MoveAdjacentToNativeBarrel(Main main, uint entityId)
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
            try {
                await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == sequence
                    && main.ObservedLastQueuedSequence == sequence && main.ObservedPendingCommandCount == 0
                    && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main),
                    "The cardinal native barrel approach command did not settle.", stageSeconds: 10);
            } catch (InvalidOperationException) {
                var diagnostic = main.ObservedSnapshot!.Players.Single();
                GD.Print($"Barrel approach failed: object={entityId}@({chest.PositionX},{chest.PositionY}); "
                    + $"from=({player.PositionX},{player.PositionY}), requested=({step.X},{step.Y}); "
                    + $"sequence={sequence}, queued={main.ObservedLastQueuedSequence}, "
                    + $"result={main.ObservedCommandResult?.ClientSequence}/{main.ObservedCommandResult?.Status}/"
                    + $"{main.ObservedCommandResult?.RejectReason}, pending={main.ObservedPendingCommandCount}; "
                    + $"tick={main.ObservedSnapshot.Tick}, applied={main.ObservedCommandResult?.AppliedTick}; "
                    + $"position=({diagnostic.PositionX},{diagnostic.PositionY}), mode={diagnostic.CombatMode}, "
                    + $"action={diagnostic.CurrentAction}; nearbyActors="
                    + DescribeNativeBarrelNearbyActors(main.ObservedSnapshot, diagnostic));
                throw;
            }
            var receipt = main.ObservedCommandResult!;
            Require(receipt.Status == CommandStatus.Accepted
                && receipt.RejectReason == CommandRejectReason.Unspecified,
                $"The cardinal native chest approach was rejected: object={entityId}@({chest.PositionX},{chest.PositionY}); "
                + $"from=({player.PositionX},{player.PositionY}), requested=({step.X},{step.Y}); "
                + $"receipt={receipt.Status}/{receipt.RejectReason}; mode={main.ObservedSnapshot!.Players.Single().CombatMode}; "
                + $"nearbyActors={DescribeNativeBarrelNearbyActors(main.ObservedSnapshot, main.ObservedSnapshot.Players.Single())}.");
            var after = main.ObservedSnapshot!.Players.Single();
            Require(after.LevelId == player.LevelId && after.PositionX == step.X && after.PositionY == step.Y,
                $"The ordinary native chest approach did not reach its requested adjacent path cell: "
                + $"object={entityId}@({chest.PositionX},{chest.PositionY}); from=({player.PositionX},{player.PositionY}); "
                + $"requested=({step.X},{step.Y}); settled=({after.PositionX},{after.PositionY}); mode={after.CombatMode}; "
                + $"nearbyActors={DescribeNativeBarrelNearbyActors(main.ObservedSnapshot, after)}.");
        }
    }

    private static string DescribeNativeBarrelNearbyActors(Snapshot snapshot, PlayerSnapshot player)
        => string.Join(";", snapshot.Monsters.Where(value => value.LevelId == player.LevelId && value.Alive)
            .OrderBy(value => NativeDistance(player.PositionX, player.PositionY,
                value.Moving ? value.FuturePositionX : value.PositionX,
                value.Moving ? value.FuturePositionY : value.PositionY))
            .Take(6).Select(value => $"{value.EntityId}@({value.PositionX},{value.PositionY})"
                + (value.Moving ? $"->({value.FuturePositionX},{value.FuturePositionY})" : string.Empty)
                + $"/hp={value.HitPoints}/moving={value.Moving}"));

    private async Task UnequipNativeBarrelWeapon(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var weapon = player.Equipment.Single(value => value.Slot == 4);
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        await ClickListItem(sections, 1, equipment, 4);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(player, weapon.State));
        await AcceptedAndSettled(main, sequence, "Unequip native bow for barrel melee");
        Require(main.ObservedSnapshot!.Players.Single().Equipment.All(value => value.Slot != 4),
            "The native bow did not leave its ordinary equipment slot.");
    }

    private async Task ReequipNativeBarrelWeapon(Main main, EquippedItemSnapshot weapon)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        if (player.Equipment.Any(value => value.Slot == 4 && value.ItemSeed == weapon.ItemSeed
                && value.State.StableItemId == weapon.State.StableItemId))
            return;
        var index = player.Inventory.ToList().FindIndex(value => value.ItemSeed == weapon.ItemSeed
            && value.State.StableItemId == weapon.State.StableItemId);
        Require(index >= 0, "The retained native bow is missing from inventory.");
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var layout = InventoryLayout.Build(player.Inventory, player.InventoryGrid);
        await ClickGridCell(main, sections, 1, "InventoryGrid", layout.Anchors[index]);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, 4);
        await AcceptedAndSettled(main, sequence, "Re-equip the retained bow for released skeleton combat");
        Require(main.ObservedSnapshot!.Players.Single().Equipment.Any(value => value.Slot == 4
            && value.ItemSeed == weapon.ItemSeed && value.State.StableItemId == weapon.State.StableItemId),
            "Ordinary equipment controls did not restore the exact retained bow.");
    }

    private async Task<uint> ObserveNativeBarrelAnimation(Main main, uint entityId)
    {
        uint frame = 0;
        ulong changed = 0;
        await Until(() => {
            var snapshot = main.ObservedSnapshot!;
            var value = snapshot.Objects.Single(item => item.EntityId == entityId);
            if (frame != value.AnimationFrame) { frame = value.AnimationFrame; changed = snapshot.Tick; }
            return value.Activated && !value.Destroyed && value.MissilePassable
                && frame > 1 && snapshot.Tick >= changed + 4;
        }, "The native barrel break animation did not finish.", 15);
        return frame;
    }

    private static void VerifyNativeBarrelRetention(Snapshot snapshot, NativeBarrelTownCheckpoint expected)
    {
        Require(HashProto(DepthGeometryFromSnapshot(snapshot, 1)) == expected.GeometrySha256
            && HashNativeChestWorldItems(snapshot) == expected.WorldItemsSha256
            && HashNativeBarrelCorpses(snapshot) == expected.CorpsesSha256,
            "Native barrel restore/repeat changed retained collision or exact world items.");
        foreach (var operation in expected.Opened) {
            var value = snapshot.Objects.Single(item => item.EntityId == operation.EntityId);
            Require(value.NativeObjectId == operation.NativeTypeId && value.LevelId == 1
                && value.PositionX == operation.X && value.PositionY == operation.Y
                && value.Activated && !value.Destroyed && value.MissilePassable
                && value.AnimationFrame == operation.TerminalFrame,
                "A broken native barrel lost its one-way projection or completed animation.");
            foreach (var loot in operation.Loot)
                VerifyNativeChestLoot(snapshot, loot);
            foreach (var released in operation.ReleasedSkeletons)
                Require(!snapshot.Monsters.Any(item => item.EntityId == released.MonsterEntityId && item.Alive)
                    && snapshot.Corpses.Any(item => item.EntityId == released.MonsterEntityId
                        && item.PositionX == released.DeathX && item.PositionY == released.DeathY),
                    "A barrel-released skeleton corpse was lost or revived.");
        }
    }

    private static string HashNativeBarrelCorpses(Snapshot snapshot)
        => HashProtoCollection(snapshot.Corpses.Where(value => value.LevelId == 1)
            .OrderBy(value => value.EntityId).Select(value => value.ToByteArray()));

    private static string HashNativeBarrelMonsters(Snapshot snapshot)
        => HashProtoCollection(snapshot.Monsters.Where(value => value.LevelId == 1)
            .OrderBy(value => value.EntityId).Select(value => value.ToByteArray()));

    private async Task DrainNativeBarrelTownCheckpoint(Main main, Button cancelTravel, Label? travelStatus)
    {
        const ulong stableTicks = 64;
        ulong? stableSequence = null;
        ulong? stableAppliedTick = null;
        ulong? stableStartTick = null;
        CommandStatus? stableStatus = null;
        CommandRejectReason? stableRejectReason = null;
        ulong? lastObservedTick = null;

        try {
            await Until(() => {
                var snapshot = main.ObservedSnapshot;
                var result = main.ObservedCommandResult;
                var queued = main.ObservedLastQueuedSequence;
                var player = snapshot?.Players.SingleOrDefault();
                var conditionsHold = snapshot is not null && player is not null && result is not null
                    && queued > 0 && result.ClientSequence == queued
                    && AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result) && result.AppliedTick > 0
                    && snapshot.Tick >= result.AppliedTick
                    && main.ObservedPendingCommandCount == 0 && Settled(main)
                    && cancelTravel.Disabled;

                if (!conditionsHold) {
                    stableSequence = null;
                    stableAppliedTick = null;
                    stableStartTick = null;
                    stableStatus = null;
                    stableRejectReason = null;
                    lastObservedTick = snapshot?.Tick;
                    return false;
                }

                var stableSnapshot = snapshot!;
                var stableResult = result!;
                if (stableSequence != queued || stableAppliedTick != stableResult.AppliedTick
                    || stableStatus != stableResult.Status || stableRejectReason != stableResult.RejectReason
                    || lastObservedTick is { } previousTick && stableSnapshot.Tick < previousTick) {
                    stableSequence = queued;
                    stableAppliedTick = stableResult.AppliedTick;
                    stableStatus = stableResult.Status;
                    stableRejectReason = stableResult.RejectReason;
                    stableStartTick = stableSnapshot.Tick;
                }
                lastObservedTick = stableSnapshot.Tick;
                return stableStartTick is { } startTick && stableSnapshot.Tick >= startTick
                    && stableSnapshot.Tick - startTick >= stableTicks;
            }, "Native barrel Town checkpoint did not remain settled for 64 authoritative ticks.", stageSeconds: 10);
        } catch (InvalidOperationException exception) {
            GD.Print(DescribeNativeBarrelCheckpointBoundary(main, cancelTravel, travelStatus,
                stableSequence ?? 0, main.ObservedSnapshot?.Tick));
            throw new InvalidOperationException(
                "Native barrel Town checkpoint drain timed out before a stable durable boundary.", exception);
        }
    }

    private static string DescribeNativeBarrelCheckpointBoundary(
        Main main, Button cancelTravel, Label? travelStatus, ulong expectedSequence, ulong? capturedTick)
    {
        var player = main.ObservedSnapshot?.Players.SingleOrDefault();
        var receipt = main.ObservedCommandResult;
        return $"Native barrel checkpoint boundary mismatch: expectedSequence={expectedSequence}, "
            + $"tick={capturedTick ?? main.ObservedSnapshot?.Tick}, queued={main.ObservedLastQueuedSequence}, "
            + $"ack={receipt?.ClientSequence}/{receipt?.Status}/{receipt?.RejectReason}@{receipt?.AppliedTick}, "
            + $"pending={main.ObservedPendingCommandCount}, mode={player?.CombatMode.ToString() ?? "<missing>"}, "
            + $"action={player?.CurrentAction?.ToString() ?? "<none>"}, settled={Settled(main)}, "
            + $"travelDisabled={cancelTravel.Disabled}, travelStatus={travelStatus?.Text ?? "<missing>"}.";
    }

    private static void VerifyNativeBarrelResumeCursor(Main main, ulong sequence)
    {
        var hello = main.ObservedInitialConnection?.ServerHello;
        var matches = hello is { ResumedSessionMatched: true }
            && hello.LastClientSequence == sequence && main.ObservedLastQueuedSequence == 0
            && main.ObservedPendingCommandCount == 0 && Settled(main);
        Require(matches,
            $"The native barrel host did not restore its exact cursor before resumed input: "
            + $"expected={sequence}, helloPresent={hello is not null}, "
            + $"resumedSessionMatched={hello?.ResumedSessionMatched}, helloSequence={hello?.LastClientSequence}, "
            + $"queued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}, "
            + $"tick={main.ObservedSnapshot?.Tick}, mode={main.ObservedSnapshot?.Players.SingleOrDefault()?.CombatMode.ToString() ?? "<missing>"}, "
            + $"action={main.ObservedSnapshot?.Players.SingleOrDefault()?.CurrentAction?.ToString() ?? "<none>"}, "
            + $"settled={Settled(main)}.");
    }

    private sealed record NativeBarrelBreakEvidence(uint EntityId, uint NativeTypeId, int X, int Y,
        uint InitialFrame, uint TerminalFrame, ulong Sequence, NativeChestLootEvidence[] Loot,
        NativeKillEvidence[] ReleasedSkeletons);
    private sealed record NativeBarrelTownCheckpoint(string ContentHash, string RulesetHash,
        ulong PriorSequence, ulong Sequence, string Player, uint RandomState, string GeometrySha256,
        string WorldItemsSha256, string CorpsesSha256, string MonstersSha256, NativeBarrelBreakEvidence[] Opened,
        bool LootBranchExercised);
}
