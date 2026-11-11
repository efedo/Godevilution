using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** Shared native combat recovery, healing and item-service controls. */
public partial class SessionAcceptance
{
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
        var player = JourneySafetyPlayer(main, JourneySafetySnapshot(main));
        Require(player.Life > 0, $"The character died before a healing command could be dispatched; {DescribeNativeHealingState(main)}.");
        if (!NativeHealingThresholdReached(main))
            return;
        var safety = JourneySafetyDecision(main);
        GD.Print($"Journey healing L{player.LevelId}: life={player.Life}/{player.LifeMaximum}, "
            + $"threshold={safety.HealingThreshold:P0}, lossPerTick={safety.LossPerTick:0.##}, "
            + $"nearby={safety.Nearby}, adjacent={safety.Adjacent}, mode={player.CombatMode}.");
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
            var selectedSequence = NativeProgressionCommandCursor(main);
            await ClickGridCell(main, sections, 1, "InventoryGrid", layout.Anchors[index]);
            var selected = main.GetChildren().OfType<HudPresentation>().Single().SelectedItemReference;
            Require(selected is { Location: PlayerItemLocation.Inventory } reference
                && reference.Slot == (uint)index && reference.StableItemId == potion.State.StableItemId
                && main.ObservedPendingCommandCount == 0
                && NativeProgressionCommandCursor(main) == selectedSequence,
                "The ordinary inventory selection did not retain the exact healing potion reference.");
            useButton = (Button)main.FindChild("UseConsumable", true, false);
        }
        Require(useButton.Visible && !useButton.Disabled,
            $"The ordinary consumable control was unavailable; life={player.Life}/{player.LifeMaximum}.");
        await WaitForNativeCommandBoundaryAsync(main, "Native healing quick-use before clicking", settlePlayer: false);
        player = JourneySafetyPlayer(main, JourneySafetySnapshot(main));
        var beforeLife = player.Life;
        var beforeTick = main.ObservedSnapshot!.Tick;
        var beforeAction = player.CurrentAction?.Kind;
        var beforeMode = player.CombatMode;
        var potionSeed = beltPotion?.ItemSeed ?? inventoryPotion.Item!.ItemSeed;
        var potionStableId = beltPotion?.State.StableItemId ?? inventoryPotion.Item!.State.StableItemId;
        var beforeCount = CountNativeHealingItemOccurrences(player, potionStableId, potionSeed);
        _ = JourneySafetyDecision(main);
        var damageAtDispatch = journeySafety.DamageObservations;
        var sequence = NativeProgressionCommandCursor(main);
        var expectedSequence = checked(sequence + 1);
        await Click(useButton.GetGlobalRect().GetCenter());
        // A press can be lost while the HUD rebuilds under heavy combat; press again only if nothing queued.
        for (var retry = 0; retry < 3 && main.ObservedLastQueuedSequence == sequence && main.ObservedPendingCommandCount == 0; retry++) {
            await Frame();
            if (main.ObservedLastQueuedSequence == sequence && useButton.Visible && !useButton.Disabled)
                await Click(useButton.GetGlobalRect().GetCenter());
        }
        Require(main.ObservedQueuedCommandMatchesIntent(expectedSequence)
            && (main.ObservedPendingCommandCount > 0
                || main.ObservedReceiptMatchesIntent(main.ObservedCommandResult, expectedSequence)),
            $"The ordinary consumable control did not queue the exact healing item; expected sequence={expectedSequence}, "
            + $"queued={main.ObservedLastQueuedSequence}, pending={main.ObservedPendingCommandCount}, "
            + $"result={main.ObservedCommandResult?.ClientSequence}.");
        try {
            // Native quick-use does not require Stand, including melee and repeated hit recovery.
            await WaitForNativeCommandAppliedAsync(main, sequence, "Use native-route healing consumable");
        } catch (InvalidOperationException exception) {
            var afterSnapshot = main.ObservedSnapshot;
            var afterPlayer = afterSnapshot is null ? null : JourneySafetyPlayer(main, afterSnapshot);
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
        var after = JourneySafetyPlayer(main, JourneySafetySnapshot(main));
        _ = JourneySafetyDecision(main);
        var concurrentDamage = journeySafety.DamageObservations > damageAtDispatch;
        var exactPotionConsumed = CountNativeHealingItemOccurrences(after, potionStableId, potionSeed) == beforeCount - 1;
        // Only an observed life loss or a fresh hit-recovery interval can mask the potion's net life gain.
        var lifeOutcomeValid = journeySafety.HealingOutcomeValid(beforeLife, player.LifeMaximum, after.Life, damageAtDispatch);
        // A retransmitted command answers Duplicate with the original outcome.
        Require(result is not null && IsAcceptedOrSuccessfulDuplicate(result)
            && main.ObservedReceiptMatchesIntent(result, expectedSequence)
            && exactPotionConsumed && lifeOutcomeValid,
            $"The ordinary consumable command did not produce its required accepted exact-item healing result; "
            + $"expected sequence={expectedSequence}, result={result?.ClientSequence}/{result?.Status}/{result?.RejectReason}, "
            + $"matchingItems={beforeCount}->{CountNativeHealingItemOccurrences(after, potionStableId, potionSeed)}, "
            + $"life={beforeLife}->{after.Life}, "
            + $"seed={potionSeed}, stableId={potionStableId}; {DescribeNativeHealingState(main)}.");
        if (concurrentDamage)
            GD.Print($"Native L{player.LevelId} potion accepted: sequence={result!.ClientSequence}, appliedTick={result.AppliedTick}, "
                + $"observedTick={main.ObservedSnapshot!.Tick}, life={beforeLife}->{after.Life}, "
                + $"netLifeDelta={after.Life - beforeLife}, exactSeed={potionSeed}, stableId={potionStableId}.");
    }

    private static bool IsNativeProgressionCombatFloor(Main main)
    {
        var phase = System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE");
        var level = main.ObservedSnapshot?.Players.SingleOrDefault()?.LevelId;
        return level == 2
                && (phase == "CathedralNativeProgressionJourney" || phase == "CathedralNativeProgressionResume")
            || level is >= 2 and <= 4 && IsNativeDepthProgressionPhase(phase);
    }

    private bool NativeHealingThresholdReached(Main main)
    {
        var observation = ObserveJourneySafety(main);
        var decision = journeySafety.Observe(observation, journeySafetyActivity);
        return observation.Level != 0 && observation.Life < observation.MaximumLife
            && observation.Life <= observation.MaximumLife * decision.HealingThreshold;
    }

    private static bool NativeHealingPotionAvailable(PlayerSnapshot player)
        => player.Belt.Any(item => IsNativeHealingConsumable(item.State))
            || player.Inventory.Any(item => IsNativeHealingConsumable(item.State));

    private bool NativeHealingIsDue(Main main) => JourneySafetyDecision(main).Drink;

    private async Task WaitForNativeArrowSettlementAsync(Main main, uint targetId, uint playerId)
    {
        bool ArrowSettled() => !main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == targetId && monster.Alive)
            || !main.ObservedSnapshot.Projectiles.Any(projectile => projectile.SourceEntityId == playerId
                && projectile.TargetEntityId == targetId);

        var failure = $"The native arrow against target {targetId} did not settle within its existing 20-second bound.";
        await Until(ArrowSettled, failure, 20,
            beforePoll: () => JourneySafetyCheckpointAsync(main, JourneySafetyActivity.Combat));
    }

    private static string DescribeNativeHealingState(Main main)
    {
        var snapshot = main.ObservedSnapshot;
        var player = snapshot?.Players.FirstOrDefault();
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
            Require(result is not null && IsAcceptedOrSuccessfulDuplicate(result)
                && main.ObservedReceiptMatchesIntent(result, sequence + 1)
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

}
