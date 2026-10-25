using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;

namespace Devilution.Client.Acceptance;

/** Ordinary generated combat and class abilities, followed by an exact cold checkpoint. */
public partial class SessionAcceptance
{
    private sealed record NativeClassCheckpoint(string PlayerPayload, uint RandomState, string WorldHash,
        ulong Sequence, uint ClassId, uint DefeatedMonster, ulong ExperienceBefore, ulong ExperienceAfter,
        int ChargesBefore, int ChargesAfterCast, int ChargesAfterRecharge, int ManaBefore, int ManaAfter,
        int DurabilityBeforeRepair, int DurabilityAfterRepair, int InitialDurability, uint RepairItemSlot, uint RepairItemStableId,
        int MaxDurabilityBeforeRepair, int MaxDurabilityAfterRepair, int MaxChargesBeforeRecharge, int MaxChargesAfterRecharge);

    private async Task VerifyNativeClassAsync(Main main, string directory, string phase, Evidence? previous)
    {
        StartNativePlayerArtCapture(main);
        StartNativeSceneArtCapture(main);
        StartNativeTownerCapture(main);
        StartNativeObjectCapture(main);
        var classId = phase.StartsWith("NativeWarrior", StringComparison.Ordinal) ? 0U
            : phase.StartsWith("NativeRogue", StringComparison.Ordinal) ? 1U : 2U;
        var journeyName = classId switch { 0 => "nativewarriorjourney.json", 1 => "nativeroguejourney.json", _ => "nativesorcererjourney.json" };
        // The Rogue fights with its bow and has no Town class ability to exercise here.
        var ranged = classId != 0;
        var identity = main.ObservedIdentity!;
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        using var profileDocument = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath
            ?? throw new InvalidDataException("An explicit native class profile is required.")));
        var profile = profileDocument.RootElement;
        ValidateNativeTownClassProfile(profile, identity.ContentManifestHash, classId);
        if (phase.EndsWith("Resume", StringComparison.Ordinal)) {
            Require(previous is { Passed: true }, "Native class resume requires passing creation evidence.");
            using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, journeyName)));
            var checkpoint = saved.RootElement.GetProperty("NativeClass").Deserialize<NativeClassCheckpoint>()!;
            await AwaitNativeTownerColdReadinessAsync(main);
            var snapshot = main.ObservedSnapshot!;
            var hello = main.ObservedInitialConnection?.ServerHello;
            Require(checkpoint.ClassId == classId
                && Convert.ToBase64String(snapshot.Players.Single().ToByteArray()) == checkpoint.PlayerPayload
                && snapshot.RandomState == checkpoint.RandomState && HashNativeProgressionWorld(snapshot) == checkpoint.WorldHash
                && main.ObservedInitialConnection is { AdmissionCompleted: true }
                && hello is { ResumedSessionMatched: true } && hello.LastClientSequence == checkpoint.Sequence
                && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
                "Fresh-host class resume changed the exact player/items/grid/resources, world, RNG or command cursor before input.");
            VerifyNativeObjectColdBaseline(main, directory);
            var first = await RecordFirstNativeReturnMovement(main, checkpoint.Sequence, 1);
            Require(first.Sequence == checkpoint.Sequence + 1 && first.AppliedTick > 0,
                "The resumed class did not accept the exact next ordinary movement receipt.");
            WriteNativeClassEvidence(main, directory, phase, checkpoint, exactColdBaseline: true);
            return;
        }

        var initial = main.ObservedSnapshot!.Players.Single();
        Require(previous is null && initial.LevelId == 0 && initial.CharacterClassId == classId
            && initial.BaseLifeMaximum == classId switch { 0 => 4480, 1 => 2880, _ => 1920 }
            && initial.BaseManaMaximum == classId switch { 0 => 640, 1 => 1440, _ => 4480 }
            && initial.LightRadius == 10 && initial.Life > 0,
            "Native class admission did not publish its canonical fresh resources and light.");
        var experienceBefore = initial.Experience;
        await TravelNativeClassRouteAsync(main, directory, 1);
        var chargesBefore = 0; var chargesAfterCast = 0; var chargesAfterRecharge = 0;
        var manaBefore = 0; var manaAfter = 0;
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        if (classId == 2) {
            chargesBefore = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).State.Charges;
            await SelectLifecycleStaffSourceAsync(main, directory, verifyKeyboard: false);
            var target = await FindNativeClassTargetAsync(main, ranged: true);
            var sequence = main.ObservedLastQueuedSequence;
            await ClickWorld(main, target.PositionX, target.PositionY);
            await AcceptedAndSettled(main, sequence, "Native Sorcerer staff cast");
            chargesAfterCast = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).State.Charges;
            Require(chargesAfterCast == chargesBefore - 1, "Native staff cast did not consume exactly one charge.");
            var sources = (ItemList)main.FindChild("SpellSources", true, false);
            var firebolt = FindListItem(sources, "Firebolt", "level 2");
            Require(firebolt >= 0, "Native Sorcerer has no learned level-two Firebolt source.");
            await ClickListItem(sections, 0, sources, firebolt);
            manaBefore = main.ObservedSnapshot!.Players.Single().Mana;
        }

        uint defeated = 0;
        var initialDurabilities = main.ObservedSnapshot!.Players.Single().Equipment.ToDictionary(item => item.Slot, item => item.State.Durability);
        var attacks = new List<object>();
        var refusals = new List<object>();
        var wearObservations = 0;
        for (var work = 0; work < 120; work++) {
            var player = main.ObservedSnapshot!.Players.Single();
            Require(player.Life > 0, "The admitted native class died during ordinary combat.");
            if (defeated != 0 && (classId != 0 || player.Equipment.Any(item => item.State.Durability < initialDurabilities[item.Slot])))
                break;
            await UseNativeHealingPotionIfNeeded(main);
            var target = await FindNativeClassTargetAsync(main, ranged);
            if (classId == 0 && defeated != 0 && wearObservations++ < 3) {
                // After a real kill, allow the next ordinary melee encounter to
                // reach its defensive block/hit phases before dispatching another attack.
                var observation = System.Diagnostics.Stopwatch.StartNew();
                while (observation.Elapsed < TimeSpan.FromSeconds(15)) {
                    var defending = main.ObservedSnapshot!.Players.Single();
                    Require(defending.Life > 0, "The Warrior died while observing native defensive equipment wear.");
                    if (defending.Equipment.Any(item => item.State.Durability < initialDurabilities[item.Slot])) break;
                    await UseNativeHealingPotionIfNeeded(main);
                    if ((long)defending.Life * 4 < defending.LifeMaximum) break;
                    await Frame();
                }
                if (main.ObservedSnapshot!.Players.Single().Equipment.Any(item => item.State.Durability < initialDurabilities[item.Slot])) break;
                target = await FindNativeClassTargetAsync(main, ranged: false);
            }
            var hpBefore = target.HitPointsQ6;
            var sequence = main.ObservedLastQueuedSequence;
            await ClickWorld(main, target.PositionX, target.PositionY, classId == 2 ? MouseButton.Left : MouseButton.Right);
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main),
                "Native class combat did not reach its authoritative receipt and action boundary.", 15);
            Require(main.ObservedSnapshot!.Players.Single().Life > 0, "The native class died after an ordinary attack.");
            var receipt = main.ObservedCommandResult!;
            if (!Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt)) {
                Require(receipt.Status == CommandStatus.Rejected
                    && receipt.RejectReason is CommandRejectReason.NotAllowed or CommandRejectReason.InvalidTarget,
                    $"Unexpected native class combat refusal: {receipt.Status}/{receipt.RejectReason}.");
                // A live target can move or enter recovery before the clocked intent applies.
                // Reobserve geometry/target and retain the bounded refusal as evidence.
                refusals.Add(new { Target = target.EntityId, receipt.ClientSequence, receipt.AppliedTick, Reason = receipt.RejectReason.ToString() });
                continue;
            }
            attacks.Add(new { Target = target.EntityId, Sequence = main.ObservedCommandResult!.ClientSequence,
                AppliedTick = main.ObservedCommandResult.AppliedTick, HitPointsBefore = hpBefore });
            if (main.ObservedSnapshot!.Corpses.Any(corpse => corpse.EntityId == target.EntityId)
                && main.ObservedSnapshot.Players.Single().Experience > experienceBefore)
                defeated = target.EntityId;
        }
        var combatPlayer = main.ObservedSnapshot!.Players.Single();
        manaAfter = combatPlayer.Mana;
        Require(defeated != 0 && combatPlayer.Experience > experienceBefore
            && (classId != 2 || manaAfter < manaBefore),
            "The native class did not earn a generated kill/XP with its ordinary attack source.");
        await VerifyNativeObjectInteractionAsync(main);
        await TravelNativeClassRouteAsync(main, directory, 0);
        if (classId == 1) {
            await FinishNativeClassJourneyAsync(main, directory, phase, new NativeClassCheckpoint(string.Empty, 0, string.Empty, 0, classId,
                defeated, experienceBefore, combatPlayer.Experience, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), attacks, refusals);
            return;
        }
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        await RevealControl(sections, 1, equipment);
        var abilityItem = classId == 0
            ? main.ObservedSnapshot!.Players.Single().Equipment.First(item => item.State.Durability < initialDurabilities[item.Slot])
            : main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4);
        var abilitySlot = abilityItem.Slot;
        var equipmentIndex = FindListItem(equipment, abilityItem.State.BaseName);
        Require(equipmentIndex >= 0, "The native starting weapon is absent from actual equipment controls.");
        await ClickListItem(sections, 1, equipment, equipmentIndex);
        var beforeAbilityPlayer = main.ObservedSnapshot!.Players.Single().Clone();
        var beforeAbility = beforeAbilityPlayer.Equipment.Single(item => item.Slot == abilitySlot).State.Clone();
        Require(beforeAbilityPlayer.ActiveStoreId == 0, "Class ability requires no active paid store.");
        Require(classId == 2 || beforeAbility.Durability < initialDurabilities[abilitySlot],
            "Warrior Repair requires naturally incurred combat durability loss.");
        var abilitySequence = main.ObservedLastQueuedSequence;
        Require(main.ObservedSelectedItemReference is { Location: PlayerItemLocation.Equipment } selected
            && selected.Slot == abilitySlot && selected.StableItemId == abilityItem.State.StableItemId,
            "Class ability selection did not retain the exact equipped item.");
        if (classId == 0) {
            var repair = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "Repair selected (Warrior)");
            await RevealControl(sections, 2, repair);
            Require(!repair.Disabled, "The actual Warrior Repair ability is unavailable for the worn item.");
            await Click(repair.GetGlobalRect().GetCenter());
        } else {
            await ClickControl(main, sections, 2, "RechargeSelected");
        }
        await AcceptedAndSettled(main, abilitySequence, "Native class ability");
        var afterAbility = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == abilitySlot).State;
        Require(classId == 0
            ? afterAbility.Durability == afterAbility.MaxDurability
                && afterAbility.MaxDurability < beforeAbility.MaxDurability
            : afterAbility.Charges == afterAbility.MaxCharges && afterAbility.MaxCharges == beforeAbility.MaxCharges - 1,
            "The native class ability did not produce its authoritative repair/recharge mutation.");
        var afterAbilityPlayer = main.ObservedSnapshot!.Players.Single();
        var expectedWeapon = beforeAbilityPlayer.Equipment.Single(item => item.Slot == abilitySlot).Clone();
        if (classId == 0) {
            expectedWeapon.State.Durability = afterAbility.Durability;
            expectedWeapon.State.MaxDurability = afterAbility.MaxDurability;
        } else {
            expectedWeapon.State.Charges = afterAbility.Charges;
            expectedWeapon.State.MaxCharges = afterAbility.MaxCharges;
        }
        Require(afterAbilityPlayer.ActiveStoreId == 0 && afterAbilityPlayer.Gold == beforeAbilityPlayer.Gold
            && afterAbilityPlayer.Inventory.SequenceEqual(beforeAbilityPlayer.Inventory)
            && afterAbilityPlayer.InventoryGrid.SequenceEqual(beforeAbilityPlayer.InventoryGrid)
            && afterAbilityPlayer.Belt.SequenceEqual(beforeAbilityPlayer.Belt)
            && afterAbilityPlayer.Equipment.Where(item => item.Slot != abilitySlot).SequenceEqual(beforeAbilityPlayer.Equipment.Where(item => item.Slot != abilitySlot))
            && afterAbilityPlayer.Equipment.Single(item => item.Slot == abilitySlot).Equals(expectedWeapon),
            "Class ability changed paid-service state, Gold, item identity or unrelated carried items/grid.");
        chargesAfterRecharge = classId == 2 ? afterAbility.Charges : 0;
        await FinishNativeClassJourneyAsync(main, directory, phase, new NativeClassCheckpoint(string.Empty, 0, string.Empty, 0, classId,
            defeated, experienceBefore, combatPlayer.Experience, chargesBefore, chargesAfterCast, chargesAfterRecharge, manaBefore, manaAfter,
            classId == 0 ? beforeAbility.Durability : 0, classId == 0 ? afterAbility.Durability : 0,
            initialDurabilities[abilitySlot], abilitySlot, abilityItem.State.StableItemId, beforeAbility.MaxDurability, afterAbility.MaxDurability,
            beforeAbility.MaxCharges, afterAbility.MaxCharges), attacks, refusals);
    }

    /** Town services and roaming, then the exact settled checkpoint the Resume phase restores. */
    private async Task FinishNativeClassJourneyAsync(Main main, string directory, string phase, NativeClassCheckpoint journey,
        List<object> attacks, List<object> refusals)
    {
        if (nativeTownClassProfile)
            await VerifyNativeTownServicesAsync(main, directory);
        await VerifyNativeTownerRoamingAsync(main);
        var snapshotAfter = main.ObservedSnapshot!;
        Require(Settled(main) && snapshotAfter.Players.Single() is { LevelId: 0, Life: > 0 },
            "The native class checkpoint did not finish alive and settled in Town.");
        var checkpointAfter = journey with {
            PlayerPayload = Convert.ToBase64String(snapshotAfter.Players.Single().ToByteArray()),
            RandomState = snapshotAfter.RandomState, WorldHash = HashNativeProgressionWorld(snapshotAfter),
            Sequence = main.ObservedLastQueuedSequence,
        };
        WriteEvidence(directory, "native-class-attacks.json", new { Passed = true, Attacks = attacks, Refusals = refusals });
        SaveNativeObjectCheckpoint(main, directory);
        WriteNativeClassEvidence(main, directory, phase, checkpointAfter, exactColdBaseline: false);
    }

    private void WriteNativeClassEvidence(Main main, string directory, string phase, NativeClassCheckpoint checkpoint, bool exactColdBaseline)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        WriteEvidence(directory, phase.ToLowerInvariant() + ".json", new {
            Passed = true, Mode = main.ObservedIdentity!.GameMode.ToString(), ContentHash = main.ObservedIdentity.ContentManifestHash,
            RulesetHash = main.ObservedIdentity.RulesetIdentityHash, EntityId = player.EntityId, ClassId = player.CharacterClassId,
            LevelId = player.LevelId, X = player.PositionX, Y = player.PositionY, Sequence = main.ObservedLastQueuedSequence,
            Visible = false, ExactColdBaseline = exactColdBaseline, NativeClass = checkpoint,
            NativePlayerArt = VerifyNativePlayerArtCapture(main, exactColdBaseline),
            NativeSceneArt = VerifyNativeSceneArtCapture(main, exactColdBaseline),
            NativeTown = CaptureNativeTownPresentation(main),
            NativeTowners = VerifyNativeTownerCapture(main, exactColdBaseline),
            NativeObjects = VerifyNativeObjectCapture(main, exactColdBaseline),
            FirstAcceptedSequence = exactColdBaseline ? main.ObservedCommandResult!.ClientSequence : 0,
            FirstAcceptedAppliedTick = exactColdBaseline ? main.ObservedCommandResult!.AppliedTick : 0,
        });
    }

    private async Task TravelNativeClassRouteAsync(Main main, string directory, uint destination)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var route = DepthGeometry(main, player.LevelId).WarpTriggers.Single(item => item.Available && item.TargetLevelId == destination);
        var distance = NativeDistance(player.PositionX, player.PositionY, route.TriggerX, route.TriggerY);
        // Snapshot cadence can skip intermediate native animation steps. Require
        // observed movement and the authoritative transition, without counting every tile.
        await TravelCathedralRoute(main, directory, route.WarpId, destination, stopEarly: false,
            minimumDistinctPositions: distance <= 1 ? 1 : 2, distanceBoundedApproachDeadline: true);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == destination,
            "The native class did not reach its ordinary route destination.");
    }

    private async Task<MonsterSnapshot> FindNativeClassTargetAsync(Main main, bool ranged, uint levelId = 1, int maximumMoves = 48)
    {
        for (var movement = 0; movement < maximumMoves; movement++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.Life > 0 && player.LevelId == levelId, $"Native class combat left its living L{levelId} context.");
            var geometry = DepthGeometry(main, levelId);
            var blocked = geometry.MissileBlockedCells.ToHashSet();
            var nativeRangedViewport = ranged && IsNativeSceneViewportActive(main, player.LevelId);
            var targets = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion && monster.LevelId == levelId)
                .OrderBy(monster => NativeMonsterDistance(player, monster)).ThenBy(monster => monster.EntityId).Take(12);
            foreach (var target in targets) {
                bool AttackCell(int x, int y) => ranged
                    ? NativeDistance(x, y, target.PositionX, target.PositionY) <= 6
                        && CanSeeNativeRangedLane(player, target, geometry, x, y, (target.PositionX, target.PositionY))
                        && HasClearNativeRangedLane(snapshot, geometry, blocked, levelId, x, y, target, (target.PositionX, target.PositionY))
                    : NativeDistance(x, y, target.PositionX, target.PositionY) <= 1;
                bool AttackGoal(int x, int y) => AttackCell(x, y)
                    && (!nativeRangedViewport || IsNativeSceneTileVisibleFromCamera(main, geometry, target, x, y));
                if (AttackCell(player.PositionX, player.PositionY)
                    && (!nativeRangedViewport || IsRenderedWorldTileVisible(main, target.PositionX, target.PositionY))
                    && AuthoritativeTargetSelector.SelectCombatTarget(snapshot, player.EntityId, target.PositionX, target.PositionY) == target.EntityId)
                    return target;
                var firstStep = FindNativeClassApproachStep(main, snapshot, player, geometry, AttackGoal,
                    nativeRangedViewport ? (x, y) => IsRenderedWorldTileVisible(main, x, y) : null);
                if (firstStep is null) continue;
                var sequence = main.ObservedLastQueuedSequence;
                await Click(main.ObservedTileScreenPosition(firstStep.Value.X, firstStep.Value.Y), walk: true);
                await AcceptedAndSettled(main, sequence, "Approach a generated native class encounter");
                await UseNativeHealingPotionIfNeeded(main);
                break;
            }
        }
        throw new InvalidOperationException("No ordinary reachable native class encounter within the movement budget.");
    }

    private static (int X, int Y)? FindNativeClassApproachStep(Main main, Snapshot snapshot, PlayerSnapshot player,
        LevelGeometrySnapshot geometry, Func<int, int, bool> goal, Func<int, int, bool>? firstStepVisible = null)
    {
        var start = (X: player.PositionX, Y: player.PositionY);
        var queue = new Queue<((int X, int Y) Cell, (int X, int Y) First)>();
        var visited = new HashSet<(int X, int Y)> { start };
        queue.Enqueue((start, start));
        while (queue.TryDequeue(out var current)) {
            if (current.Cell != start && goal(current.Cell.X, current.Cell.Y)) return current.First;
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) }) {
                var next = (X: current.Cell.X + dx, Y: current.Cell.Y + dy);
                if (visited.Contains(next) || !IsNativeApproachCellOpen(main, snapshot, player, geometry, next.X, next.Y)) continue;
                if (dx != 0 && dy != 0 && (!IsNativeApproachCellOpen(main, snapshot, player, geometry, current.Cell.X + dx, current.Cell.Y)
                    || !IsNativeApproachCellOpen(main, snapshot, player, geometry, current.Cell.X, current.Cell.Y + dy))) continue;
                if (current.Cell == start && firstStepVisible is not null && !firstStepVisible(next.X, next.Y)) continue;
                visited.Add(next);
                queue.Enqueue((next, current.Cell == start ? next : current.First));
            }
        }
        return null;
    }

    private static bool IsNativeSceneViewportActive(Main main, uint levelId)
    {
        var observation = main.ObservedNativeSceneDrawing;
        return observation is { NativeSceneActive: true } && observation.LevelId == levelId;
    }

    private static bool IsRenderedWorldTileVisible(Main main, int x, int y)
    {
        var worldArea = (Control)main.FindChild("WorldArea", true, false);
        return worldArea.GetGlobalRect().HasPoint(main.ObservedTileScreenPosition(x, y));
    }

    private static bool IsNativeSceneTileVisibleFromCamera(Main main, LevelGeometrySnapshot geometry,
        MonsterSnapshot target, int cameraX, int cameraY)
    {
        var worldArea = (Control)main.FindChild("WorldArea", true, false);
        var world = worldArea.GetChildren().OfType<WorldPresentation>().Single();
        var frame = world.GetParent() as Control ?? worldArea;
        var viewport = NativeSceneViewport.Create(
            Math.Max(1, Mathf.RoundToInt(frame.Size.X)),
            Math.Max(1, Mathf.RoundToInt(frame.Size.Y)),
            geometry.Width,
            geometry.Height,
            cameraX,
            cameraY);
        var targetScreen = viewport.WorldToScreen(target.PositionX, target.PositionY);
        return viewport.ContainsViewportPoint(targetScreen.X, targetScreen.Y);
    }
}
