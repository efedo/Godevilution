using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeProgressionTownCheckpoint(
        string PlayerPayload, uint RandomState, ulong Sequence, string WorldHash,
        string L1GeometryHash, string L2GeometryHash,
        NativeEquipmentKillEvidence L1,
        NativeProgressionGoldCollection L1Gold,
        NativeProgressionCombatEvidence L2,
        NativeProgressionHealerStock HealerStock);

    private sealed record NativeProgressionHealerStock(string[] ItemRowsBase64, string Hash);
    private sealed record NativeProgressionGoldPickupEvidence(
        uint EntityId, uint ItemSeed, uint StableItemId, int PositionX, int PositionY, int GoldValue,
        uint GoldBefore, uint GoldAfter, ulong Sequence, ulong AppliedTick);
    private sealed record NativeProgressionHealerPurchaseEvidence(
        uint StoreSlot, int NativeItemIndex, uint StableItemId, uint StoreRowSeed, uint OwnedItemSeed,
        uint Price, uint GoldBefore, uint GoldAfter, int SupplyBefore, int SupplyAfter,
        ulong Sequence, ulong AppliedTick);
    private sealed record NativeProgressionGoldCollection(
        uint GoldBefore, uint GoldAfter, NativeProgressionGoldPickupEvidence[] Pickups,
        NativeKillEvidence[] ApproachKills, NativeAttackRefusalEvidence[] AttackRefusals,
        int WorkUsed, int UncollectedEligibleGoldValue);
    private sealed record NativeProgressionHealerPreparation(
        int TargetSupply, int SupplyBefore, int SupplyAfter, int SupplyShortfall, uint GoldBefore, uint GoldAfter,
        bool PurchaseVerified, NativeProgressionHealerPurchaseEvidence[] Purchases,
        NativeProgressionHealerStock Stock, bool RetainedStockMatched);

    private async Task VerifyCathedralNativeProgressionAsync(
        Main main, string directory, string phase, bool visible, Evidence? previous)
    {
        var path = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(path is not null, "Native progression requires its explicit compiled profile.");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path!));
        var profile = document.RootElement;
        Require(profile.GetProperty("formatVersion").GetInt32() == 2
            && profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-progression.v1"
            && profile.GetProperty("nativeDepth").GetInt32() == 2
            && !profile.GetProperty("nativePopulationClaim").GetBoolean(),
            "The supplied profile does not select the separate native Town/L1/L2 route.");
        var routeLeg = 0;
        async Task Travel(uint destination) {
            var routeDirectory = System.IO.Path.Combine(directory, "native-progression-routes", phase,
                (++routeLeg).ToString(System.Globalization.CultureInfo.InvariantCulture));
            System.IO.Directory.CreateDirectory(routeDirectory);
            if (main.ObservedSnapshot!.Players.Single().LevelId != 0)
                await ApproachNativeProgressionStair(main, routeDirectory, destination,
                    allowDoorOperations: phase == "CathedralNativeProgressionJourney");
            await TravelNativeCathedralRoute(main, routeDirectory, destination);
        }
        var identity = main.ObservedIdentity!;
        Require(identity.ContentManifestHash == profile.GetProperty("contentManifestHash").GetString(),
            "The native progression host admitted a different content pack.");
        if (phase == "CathedralNativeProgressionJourney") {
            Require(previous is null && main.ObservedSnapshot!.Players.Single().LevelId == 0,
                "The native progression journey must start with a fresh Rogue in Town.");
            await VerifyNativeConsumableSelectionRefresh(main, directory);
            await Travel(1);
            var initialBow = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).Clone();
            var l1 = await FightForNativeEquipmentAsync(main, directory, visible, useCardinalLootApproach: true);
            await ReequipNativeBarrelWeapon(main, initialBow);
            GD.Print($"Native progression L1 earned equipment acquired; XP={main.ObservedSnapshot!.Players.Single().Experience}");
            var goldCollection = await CollectNativeProgressionGoldAsync(main, l1);
            await Travel(0);
            var healingPreparation = await BuyNativeProgressionHealingAsync(
                main, targetSupply: 6, maximumPurchases: 8);
            Require(healingPreparation.PurchaseVerified && healingPreparation.Purchases.Length > 0
                && goldCollection.Pickups.Length > 0,
                "The native journey did not buy ordinary Pepin supplies after collecting earned L1 Gold.");
            await UseNativeTownHealer(main);
            GD.Print("Native progression Town resupply complete; returning through L1 to L2");
            await Travel(1);
            await Travel(2);
            var l2Entry = main.ObservedSnapshot!.Players.Single();
            WriteEvidence(directory, "native-progression-l2-entry.json", new {
                l2Entry.LevelId, l2Entry.Life, l2Entry.LifeMaximum, l2Entry.Experience, l2Entry.Gold,
                HealingItems = l2Entry.Inventory.Count(row => IsNativeHealingConsumable(row.State))
                    + l2Entry.Belt.Count(row => IsNativeHealingConsumable(row.State)),
                Sequence = main.ObservedCommandResult?.ClientSequence,
            });
            GD.Print($"Native progression L2 entry; life={l2Entry.Life}/{l2Entry.LifeMaximum}; healing={l2Entry.Inventory.Count(row => IsNativeHealingConsumable(row.State)) + l2Entry.Belt.Count(row => IsNativeHealingConsumable(row.State))}");
            var bow = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).Clone();
            var l2 = await FightNativeProgressionL2CombatAsync(main, directory);
            Require(main.ObservedSnapshot!.Players.Single() is { LevelId: 2, Life: > 0 }
                && l2.ExperienceAfter > l2.ExperienceBefore && l2.Kills.Length > 0
                && l2.WorkUsed <= NativeCombatWorkBudget.MaximumWork
                && l2.Kills.All(kill => kill.AcceptedShots.Length > 0
                    && main.ObservedSnapshot.Corpses.Count(corpse => corpse.EntityId == kill.MonsterEntityId) == 1
                    && !main.ObservedSnapshot.Monsters.Any(monster => monster.EntityId == kill.MonsterEntityId && monster.Alive)),
                "L2 did not retain a living player, actual native combat kill, XP and its accepted-shot ledger.");
            GD.Print($"Native progression L2 first combat passed; XP={l2.ExperienceBefore}->{l2.ExperienceAfter}; "
                + $"observed encounter-area new drops={l2.ObservedNewDropEntityIds.Length}");
            await ReequipNativeBarrelWeapon(main, bow);
            await Travel(1);
            await Travel(0);
            await UseNativeTownHealer(main);
            await DrainNativeBarrelTownCheckpoint(main,
                (Button)main.FindChild("CancelTravel", true, false),
                main.FindChild("TravelStatus", true, false) as Label);
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var checkpoint = new NativeProgressionTownCheckpoint(
                Convert.ToBase64String(player.ToByteArray()), snapshot.RandomState,
                main.ObservedCommandResult!.ClientSequence, HashNativeProgressionWorld(snapshot),
                HashProto(DepthGeometry(main, 1)), HashProto(DepthGeometry(main, 2)), l1, goldCollection, l2,
                healingPreparation.Stock);
            VerifyNativeProgressionRetention(main, checkpoint);
            WriteEvidence(directory, "cathedralnativeprogressionjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = player.EntityId, ClassId = player.CharacterClassId,
                LevelId = player.LevelId, X = player.PositionX, Y = player.PositionY,
                Sequence = checkpoint.Sequence, Visible = visible, NativeProgression = checkpoint,
                GoldCollection = goldCollection,
                HealerPreparation = healingPreparation,
            });
            GD.Print($"Native Town/L1 equipment/L2 first-combat checkpoint passed; sequence={checkpoint.Sequence}; L1={l1.MonsterEntityId}; L2={l2.MonsterEntityId}");
            return;
        }
        Require(phase == "CathedralNativeProgressionResume" && previous is { Passed: true },
            "Native progression resume requires a completed journey checkpoint.");
        using var journey = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativeprogressionjourney.json")));
        var expected = journey.RootElement.GetProperty("NativeProgression").Deserialize<NativeProgressionTownCheckpoint>()!;
        var initial = main.ObservedSnapshot!;
        Require(Convert.ToBase64String(initial.Players.Single().ToByteArray()) == expected.PlayerPayload
            && initial.RandomState == expected.RandomState && HashNativeProgressionWorld(initial) == expected.WorldHash,
            "Fresh-host native progression changed the exact saved player, RNG or retained two-floor world before input.");
        var connection = main.ObservedInitialConnection;
        Require(connection is { AdmissionCompleted: true }
            && connection.ServerHello is { ResumedSessionMatched: true }
            && connection.ServerHello.LastClientSequence == expected.Sequence
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
            "Fresh-host native progression did not resume the exact command cursor before input.");
        VerifyNativeProgressionRetention(main, expected);
        GD.Print("Native progression exact cold player/cursor/RNG/two-floor world verified before input");
        var resumedHealerStock = await BuyNativeProgressionHealingAsync(main,
            targetSupply: NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) + 1,
            maximumPurchases: 1, expectedStock: expected.HealerStock);
        var healer = await UseNativeTownHealer(main);
        await Travel(1);
        VerifyNativeProgressionRetention(main, expected);
        await Travel(2);
        VerifyNativeProgressionRetention(main, expected);
        await Travel(1);
        await Travel(0);
        await DrainNativeBarrelTownCheckpoint(main,
            (Button)main.FindChild("CancelTravel", true, false), main.FindChild("TravelStatus", true, false) as Label);
        VerifyNativeProgressionRetention(main, expected);
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == 0 && final.Life > 0, "The resumed two-floor native journey did not return alive to Town.");
        WriteEvidence(directory, "cathedralnativeprogressionresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = final.EntityId, ClassId = final.CharacterClassId,
            LevelId = final.LevelId, X = final.PositionX, Y = final.PositionY,
            Sequence = main.ObservedCommandResult!.ClientSequence, Visible = visible,
            ExactColdBaseline = true, BothFloorsRetained = true, Healer = healer,
            HealerStockContinuation = resumedHealerStock,
        });
        GD.Print($"Native Town/L1/L2 fresh-host resume and retained revisit passed; sequence={main.ObservedCommandResult.ClientSequence}");
    }

    private async Task VerifyNativeConsumableSelectionRefresh(Main main, string directory)
    {
        await Frame();
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var payload = player.ToByteArray();
        var queued = main.ObservedLastQueuedSequence;
        var pending = main.ObservedPendingCommandCount;
        var ordinaryIndex = player.Inventory.ToList().FindIndex(row =>
            !Devilution.Client.Protocol.AuthoritativeItemActionRules.IsTargetlessConsumable(row.State.ItemActionKind));
        var belt = player.Belt.OrderBy(row => row.Slot).ToArray();
        var healingIndex = Array.FindIndex(belt, row => IsNativeHealingConsumable(row.State));
        Require(ordinaryIndex >= 0 && healingIndex >= 0, "The native starting loadout lacks selection regression sources.");
        var hud = main.GetChildren().OfType<HudPresentation>().Single();
        var grid = (GridContainer)main.FindChild("InventoryGrid", true, false);
        var list = (ItemList)main.FindChild("BeltItems", true, false);
        var use = (Button)main.FindChild("UseConsumable", true, false);
        var layout = Devilution.Client.Protocol.InventoryLayout.Build(player.Inventory, player.InventoryGrid);
        var passed = false;
        try {
            // Exercise synchronous local selection callbacks, without a server update or gameplay command.
            grid.GetChild<Button>(layout.Anchors[ordinaryIndex]).EmitSignal(Button.SignalName.Pressed);
            Require(use.Disabled, "Selecting a non-consumable did not immediately disable Use item.");
            list.EmitSignal(ItemList.SignalName.ItemSelected, healingIndex);
            Require(hud.SelectedItemReference is { Location: PlayerItemLocation.Belt } source
                && source.Slot == belt[healingIndex].Slot && source.StableItemId == belt[healingIndex].State.StableItemId,
                "The belt selection callback did not retain its exact carried source.");
            Require(!use.Disabled, "Selecting a healing item did not immediately enable Use item before the next snapshot.");
            hud.ClearItemSelection();
            Require(use.Disabled, "Clearing the selected item did not immediately disable Use item.");
            Require(main.ObservedSnapshot!.Tick == snapshot.Tick
                && main.ObservedSnapshot.Players.Single().ToByteArray().SequenceEqual(payload)
                && main.ObservedLastQueuedSequence == queued && main.ObservedPendingCommandCount == pending,
                "The local selection regression changed authoritative state or queued a gameplay command.");
            passed = true;
        } finally {
            WriteEvidence(directory, "native-consumable-selection-refresh.json", new {
                Passed = passed, SnapshotTick = snapshot.Tick, QueuedBefore = queued,
                QueuedAfter = main.ObservedLastQueuedSequence, PendingBefore = pending,
                PendingAfter = main.ObservedPendingCommandCount, use.Disabled,
            });
            hud.ClearItemSelection();
        }
        GD.Print("Native consumable selection refresh passed without a new snapshot or gameplay command");
    }

    private async Task ApproachNativeProgressionStair(Main main, string directory, uint destination, bool allowDoorOperations)
    {
        var initial = main.ObservedSnapshot!.Players.Single().Clone();
        var stair = DepthGeometry(main, initial.LevelId).WarpTriggers.Single(row => row.Available
            && row.TargetLevelId == destination);
        var kills = new List<NativeKillEvidence>();
        var visited = new HashSet<uint>();
        var doors = new HashSet<uint>();
        var doorEvidence = new List<NativeDoorOperationEvidence>();
        var doorCommands = new Dictionary<uint, int>();
        var work = new NativeCombatWorkBudget(enforceCumulativeTargetLimit: IsNativeProgressionCombatFloor(main));
        await MoveAdjacentToNativePosition(main, _ => new ObjectSnapshot {
            LevelId = initial.LevelId, PositionX = stair.TriggerX, PositionY = stair.TriggerY,
        }, async blocked => {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.Life > 0, "The Rogue died while clearing the native stair route.");
            if (await CollectNearbyProgressionHealing(main)) {
                work.ConsumeExploration("collect native stair-route healing");
                await UseNativeHealingPotionIfNeeded(main);
                return true;
            }
            var routeBlocker = blocked
                ? FindNativeDepthProgressionStairBlocker(snapshot, stair.TriggerX, stair.TriggerY) : null;
            var threat = snapshot.Monsters.Where(row => row.LevelId == player.LevelId && row.Alive
                    && !row.IsPlayerMinion && !visited.Contains(row.EntityId)
                    && (NativeMonsterDistance(player, row) <= 5 || row.EntityId == routeBlocker)
                    && HasNativeRangedLaneWithinApproach(main, snapshot, player, row,
                        NativeMaximumRangedApproachCommands))
                .OrderBy(row => NativeMonsterDistance(player, row)).ThenBy(row => row.EntityId).FirstOrDefault();
            if (threat is not null) {
                var distantRouteBlocker = threat.EntityId == routeBlocker && NativeMonsterDistance(player, threat) > 5;
                var killed = await DefeatNativeTargetAsync(main, threat.EntityId,
                    NativeGearHuntMaximumAttacksPerTarget,
                    distantRouteBlocker ? threat.PositionX : player.PositionX,
                    distantRouteBlocker ? threat.PositionY : player.PositionY, work,
                    collectGeneratedHealing: true);
                if (killed is not null) {
                    visited.Add(killed.MonsterEntityId);
                    kills.Add(killed);
                    await CollectNearbyNativeHealingDropsAsync(main, killed.DropEntityIds);
                    await UseNativeHealingPotionIfNeeded(main);
                } else work.ConsumeExploration("reselect native stair-route threat");
                await WaitForNativeDepthProgressionCommandBoundary(main,
                    $"after stair-route combat or threat reselection for L{destination}");
                return true;
            }
            work.ConsumeExploration("walk or open native stair route");
            return allowDoorOperations && blocked && doors.Count < NativeMaximumDoorOperations
                && await AdvanceTowardReachableNativeDoor(main, doorCommands, doors, doorEvidence, work);
        });
        WriteEvidence(directory, "native-stair-approach.json", new {
            Passed = true, SourceLevel = initial.LevelId, Destination = destination,
            Kills = kills.ToArray(), AttackRefusals = work.AttackRefusals.ToArray(), Doors = doorEvidence.ToArray(),
            Sequence = main.ObservedCommandResult?.ClientSequence,
            Life = main.ObservedSnapshot!.Players.Single().Life,
            LifeMaximum = main.ObservedSnapshot.Players.Single().LifeMaximum,
        });
    }

    private async Task ApproachNativeProgressionLoot(Main main, WorldItemSnapshot gear,
        HashSet<uint> visited, List<NativeKillEvidence> kills, NativeCombatWorkBudget work,
        bool collectGeneratedHealing = true)
    {
        var doors = new HashSet<uint>();
        var doorEvidence = new List<NativeDoorOperationEvidence>();
        var doorCommands = new Dictionary<uint, int>();
        await MoveAdjacentToNativePosition(main, _ => new ObjectSnapshot {
            LevelId = gear.LevelId, PositionX = gear.PositionX, PositionY = gear.PositionY,
        }, async blocked => {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            if (collectGeneratedHealing && await CollectNearbyProgressionHealing(main)) {
                work.ConsumeExploration("collect native earned-loot approach healing");
                await UseNativeHealingPotionIfNeeded(main);
                return true;
            }
            var threat = snapshot.Monsters.Where(row => row.LevelId == player.LevelId && row.Alive
                    && !row.IsPlayerMinion && !visited.Contains(row.EntityId)
                    && NativeMonsterDistance(player, row) <= 5
                    && HasNativeRangedLaneWithinApproach(main, snapshot, player, row,
                        NativeMaximumRangedApproachCommands))
                .OrderBy(row => NativeMonsterDistance(player, row)).ThenBy(row => row.EntityId).FirstOrDefault();
            if (threat is not null) {
                var killed = await DefeatNativeTargetAsync(main, threat.EntityId,
                    NativeGearHuntMaximumAttacksPerTarget, gear.PositionX, gear.PositionY, work,
                    collectGeneratedHealing: collectGeneratedHealing);
                if (killed is not null) {
                    visited.Add(killed.MonsterEntityId);
                    kills.Add(killed);
                    if (collectGeneratedHealing)
                        await CollectNearbyNativeHealingDropsAsync(main, killed.DropEntityIds);
                } else work.ConsumeExploration("reselect native earned-loot threat");
                return true;
            }
            work.ConsumeExploration("approach native earned loot");
            return blocked && doors.Count < NativeMaximumDoorOperations
                && await AdvanceTowardReachableNativeDoor(main, doorCommands, doors, doorEvidence, work);
        });
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == gear.LevelId && final.Life > 0
            && NativeDistance(final.PositionX, final.PositionY, gear.PositionX, gear.PositionY) == 1,
            "Ordinary cardinal movement did not reach the adjacent earned loot.");
    }

    private async Task<NativeProgressionGoldCollection> CollectNativeProgressionGoldAsync(
        Main main, NativeEquipmentKillEvidence earnedL1)
    {
        const int maximumPickups = 8;
        const int maximumCandidatePathCells = 40;
        const int targetSupply = 6;
        var initialPlayer = main.ObservedSnapshot!.Players.Single();
        Require(initialPlayer.LevelId == 1 && initialPlayer.Life > 0,
            "Native gold preparation requires a living Rogue on L1.");
        var goldBefore = initialPlayer.Gold;
        var eligibleDrops = earnedL1.Kills.SelectMany(kill => kill.DropEntityIds).ToHashSet();
        var attempted = new HashSet<uint>();
        var visited = earnedL1.Kills.Select(kill => kill.MonsterEntityId).ToHashSet();
        var approachKills = earnedL1.Kills.ToList();
        var originalKillCount = approachKills.Count;
        var work = new NativeCombatWorkBudget(enforceCumulativeTargetLimit: IsNativeProgressionCombatFloor(main));
        var pickups = new List<NativeProgressionGoldPickupEvidence>();
        while (pickups.Count < maximumPickups) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.LevelId == 1 && player.Life > 0,
                "The Rogue died while collecting earned native gold.");
            var desiredGold = checked(Math.Max(0, targetSupply - NativeHealingSupply(player)) * 50);
            if (player.Gold >= desiredGold)
                break;
            eligibleDrops.UnionWith(approachKills.Skip(originalKillCount).SelectMany(kill => kill.DropEntityIds));
            var geometry = DepthGeometry(main, 1);
            var candidate = snapshot.WorldItems.Where(row => eligibleDrops.Contains(row.EntityId)
                    && row.LevelId == 1 && row.State.PresentationKind == ItemPresentationKind.Gold
                    && row.State.Value > 0 && !attempted.Contains(row.EntityId)
                    && !snapshot.WorldItems.Any(other => other.EntityId != row.EntityId && other.LevelId == row.LevelId
                        && other.PositionX == row.PositionX && other.PositionY == row.PositionY))
                .Select(row => (Item: row,
                    Distance: NativeDistance(player.PositionX, player.PositionY, row.PositionX, row.PositionY),
                    Path: NativeDistance(player.PositionX, player.PositionY, row.PositionX, row.PositionY) <= 1
                        ? null : FindNativeChestApproachPath(snapshot, geometry, new ObjectSnapshot {
                            LevelId = row.LevelId, PositionX = row.PositionX, PositionY = row.PositionY,
                        })))
                .Where(row => row.Distance <= 1
                    || row.Path is not null && row.Path.Count <= maximumCandidatePathCells)
                .OrderBy(row => row.Distance <= 1 ? 0 : row.Path!.Count)
                .ThenBy(row => row.Item.EntityId).FirstOrDefault();
            if (candidate.Item is null)
                break;

            var source = candidate.Item;
            attempted.Add(source.EntityId);
            if (candidate.Distance > 1)
                await ApproachNativeProgressionLoot(main, source, visited, approachKills, work,
                    collectGeneratedHealing: false);
            work.ConsumeExploration("pick up an earned native gold drop");
            var before = main.ObservedSnapshot!.Players.Single();
            var goldBeforePickup = before.Gold;
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await ClickWorld(main, source.PositionX, source.PositionY);
            await AcceptedAndSettled(main, sequence, "Pick up gold dropped by an actual L1 kill");
            await Until(() => main.ObservedSnapshot!.WorldItems.All(item => item.EntityId != source.EntityId)
                && main.ObservedSnapshot.Players.Single().Gold == checked(goldBeforePickup + source.State.Value),
                "The earned native gold drop did not enter the Rogue's wallet.", 5);
            var result = main.ObservedCommandResult!;
            var after = main.ObservedSnapshot!.Players.Single();
            Require(result is { Status: CommandStatus.Accepted, RejectReason: CommandRejectReason.Unspecified }
                && result.ClientSequence == checked(sequence + 1) && result.AppliedTick > 0
                && after.Gold == checked(goldBeforePickup + source.State.Value)
                && main.ObservedSnapshot.WorldItems.All(item => item.EntityId != source.EntityId),
                "The native gold pickup did not produce the exact wallet delta and accepted receipt.");
            pickups.Add(new NativeProgressionGoldPickupEvidence(source.EntityId, source.ItemSeed,
                source.State.StableItemId, source.PositionX, source.PositionY, source.State.Value,
                goldBeforePickup, after.Gold, result.ClientSequence, result.AppliedTick));
        }

        var finalSnapshot = main.ObservedSnapshot!;
        var remainingEligibleGold = finalSnapshot.WorldItems.Where(row => eligibleDrops.Contains(row.EntityId)
                && row.LevelId == 1 && row.State.PresentationKind == ItemPresentationKind.Gold && row.State.Value > 0)
            .Sum(row => row.State.Value);
        return new NativeProgressionGoldCollection(goldBefore, finalSnapshot.Players.Single().Gold,
            pickups.ToArray(), approachKills.Skip(originalKillCount).ToArray(), work.AttackRefusals.ToArray(),
            work.Used, remainingEligibleGold);
    }

    private async Task<NativeProgressionHealerPreparation> BuyNativeProgressionHealingAsync(
        Main main, int targetSupply, int maximumPurchases,
        NativeProgressionHealerStock? expectedStock = null)
    {
        Require(targetSupply >= 0 && maximumPurchases >= 0,
            "Native Healer purchase bounds must be non-negative.");
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await ApproachDeclaredDestination(main, "towner:1", distanceBoundedApproachDeadline: true);
        var openSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "OpenHealer4");
        await AcceptedAndSettled(main, openSequence, "Open Pepin's native Healer stock");
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 4,
            "Pepin's native Healer stock did not open.", 5);

        var stockAtOpen = CaptureNativeProgressionHealerStock(main.ObservedSnapshot!.ActiveStore!);
        ValidateNativeProgressionHealerStock(main.ObservedSnapshot!.ActiveStore!);
        var retainedStockMatched = expectedStock is null
            || stockAtOpen.Hash == expectedStock.Hash
                && stockAtOpen.ItemRowsBase64.SequenceEqual(expectedStock.ItemRowsBase64);
        Require(retainedStockMatched,
            "Fresh-host Pepin stock rows changed from the exact saved pinned item rows before purchase.");

        await Frame();
        var buyButton = (Button)main.FindChild("BuySelected", true, false);
        Require(buyButton.Disabled, "A newly opened Healer store retained a stale purchase selection.");
        var stockList = (ItemList)main.FindChild("StoreItems", true, false);
        var selectionSnapshot = main.ObservedSnapshot!;
        var selectionPlayer = selectionSnapshot.Players.Single().ToByteArray();
        var selectionQueued = main.ObservedLastQueuedSequence;
        var selectionPending = main.ObservedPendingCommandCount;
        var initialRows = selectionSnapshot.ActiveStore!.Items.OrderBy(item => item.StoreSlot).ToArray();
        var initialPurchaseIndex = Array.FindIndex(initialRows, item => item.StoreSlot == 0);
        Require(initialPurchaseIndex >= 0, "Pepin's pinned standard healing potion disappeared from slot 0.");
        // These local callbacks must update the control before another snapshot or command.
        stockList.EmitSignal(ItemList.SignalName.ItemSelected, initialPurchaseIndex);
        Require(!buyButton.Disabled, "Selecting Pepin's stock did not immediately enable Buy selected.");
        stockList.EmitSignal(ItemList.SignalName.ItemSelected, -1L);
        Require(buyButton.Disabled, "Clearing Pepin's stock selection did not immediately disable Buy selected.");
        stockList.EmitSignal(ItemList.SignalName.ItemSelected, initialPurchaseIndex);
        Require(!buyButton.Disabled
            && main.ObservedSnapshot!.Tick == selectionSnapshot.Tick
            && main.ObservedSnapshot.Players.Single().ToByteArray().SequenceEqual(selectionPlayer)
            && main.ObservedLastQueuedSequence == selectionQueued
            && main.ObservedPendingCommandCount == selectionPending,
            "Local store selection waited for authoritative state or queued a gameplay command.");
        GD.Print("Native store selection refresh passed without a new snapshot or gameplay command");

        var initialPlayer = main.ObservedSnapshot!.Players.Single();
        var supplyBefore = NativeHealingSupply(initialPlayer);
        var goldBefore = initialPlayer.Gold;
        var purchases = new List<NativeProgressionHealerPurchaseEvidence>();
        while (purchases.Count < maximumPurchases
            && NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) < targetSupply) {
            var player = main.ObservedSnapshot!.Players.Single();
            var store = main.ObservedSnapshot!.ActiveStore!;
            ValidateNativeProgressionHealerStock(store);
            var stockRows = store.Items.OrderBy(item => item.StoreSlot).ToArray();
            var purchaseIndex = Array.FindIndex(stockRows, item => item.StoreSlot == 0);
            Require(purchaseIndex >= 0, "Pepin's pinned standard healing potion disappeared from slot 0.");
            var row = stockRows[purchaseIndex];
            if (player.Gold < checked((int)row.Price))
                break;
            var seedCountsBefore = NativeProgressionPotionSeedCounts(player, row.State.StableItemId);
            var countBefore = NativeHealingSupply(player);
            var playerGoldBefore = player.Gold;
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await ClickListItem(sections, 2, (ItemList)main.FindChild("StoreItems", true, false), purchaseIndex);
            sequence = main.ObservedCommandResult?.ClientSequence ?? sequence;
            await ClickControl(main, sections, 2, "BuySelected");
            await AcceptedAndSettled(main, sequence, "Buy Pepin's pinned standard healing potion");
            await Until(() => main.ObservedSnapshot!.Players.Single().Gold == checked(playerGoldBefore - (int)row.Price)
                && NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) == countBefore + 1,
                "Pepin's purchase did not add the selected item and debit its exact price.", 5);

            var receipt = main.ObservedCommandResult!;
            var purchasedPlayer = main.ObservedSnapshot!.Players.Single();
            var seedCountsAfter = NativeProgressionPotionSeedCounts(purchasedPlayer, row.State.StableItemId);
            var addedSeedCounts = seedCountsAfter.Where(entry => entry.Value
                    > seedCountsBefore.GetValueOrDefault(entry.Key))
                .Select(entry => (Seed: entry.Key, Added: entry.Value - seedCountsBefore.GetValueOrDefault(entry.Key)))
                .ToArray();
            var addedOccurrences = addedSeedCounts.Sum(entry => entry.Added);
            var countAfter = NativeHealingSupply(purchasedPlayer);
            Require(receipt is { Status: CommandStatus.Accepted, RejectReason: CommandRejectReason.Unspecified }
                && receipt.ClientSequence == checked(sequence + 1) && receipt.AppliedTick > 0
                && purchasedPlayer.Gold == checked(playerGoldBefore - (int)row.Price)
                && addedOccurrences == 1 && addedSeedCounts.Length == 1
                && countAfter == countBefore + 1,
                $"Pepin purchase identity/economy mismatch: receipt={receipt?.ClientSequence}/{receipt?.Status}/{receipt?.RejectReason}, "
                + $"gold={playerGoldBefore}->{purchasedPlayer.Gold} price={row.Price}, newSeedOccurrences={addedOccurrences}, "
                + $"supply={countBefore}->{countAfter}.");
            var ownedSeed = addedSeedCounts.Single().Seed;
            var ownedPotion = purchasedPlayer.Inventory.Any(item => item.ItemSeed == ownedSeed
                    && item.State.StableItemId == row.State.StableItemId && item.State.ItemIndex == 77
                    && item.State.ItemActionKind == ItemActionKind.RestorePartialLife)
                || purchasedPlayer.Belt.Any(item => item.ItemSeed == ownedSeed
                    && item.State.StableItemId == row.State.StableItemId && item.State.ItemIndex == 77
                    && item.State.ItemActionKind == ItemActionKind.RestorePartialLife);
            Require(ownedPotion, "The accepted Pepin purchase did not create the exact pinned standard-potion identity.");
            purchases.Add(new NativeProgressionHealerPurchaseEvidence(row.StoreSlot, row.State.ItemIndex,
                row.State.StableItemId, row.ItemSeed, ownedSeed, row.Price, playerGoldBefore, purchasedPlayer.Gold,
                countBefore, countAfter, receipt.ClientSequence, receipt.AppliedTick));

            ValidateNativeProgressionHealerStock(main.ObservedSnapshot!.ActiveStore!);
            var currentRows = CaptureNativeProgressionHealerStock(main.ObservedSnapshot!.ActiveStore!);
            Require(currentRows.Hash == stockAtOpen.Hash
                && currentRows.ItemRowsBase64.SequenceEqual(stockAtOpen.ItemRowsBase64),
                "Buying a pinned Healer potion changed the persistent store item rows.");
        }

        var finalStore = main.ObservedSnapshot!.ActiveStore!;
        ValidateNativeProgressionHealerStock(finalStore);
        var finalStock = CaptureNativeProgressionHealerStock(finalStore);
        var finalPlayer = main.ObservedSnapshot.Players.Single();
        var finalSupply = NativeHealingSupply(finalPlayer);
        var preparation = new NativeProgressionHealerPreparation(targetSupply, supplyBefore,
            finalSupply, Math.Max(0, targetSupply - finalSupply), goldBefore, finalPlayer.Gold,
            purchases.Count > 0, purchases.ToArray(),
            finalStock, retainedStockMatched);
        var closeSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, closeSequence, "Close Pepin's Healer store");
        Require(main.ObservedSnapshot!.ActiveStore is null
            && main.ObservedSnapshot.Players.Single().ActiveStoreId == 0,
            "Pepin's store remained open after the ordinary close command.");
        return preparation;
    }

    private static int NativeHealingSupply(PlayerSnapshot player)
        => player.Inventory.Count(row => IsNativeHealingConsumable(row.State))
            + player.Belt.Count(row => IsNativeHealingConsumable(row.State));

    private static Dictionary<uint, int> NativeProgressionPotionSeedCounts(PlayerSnapshot player, uint stableItemId)
        => player.Inventory.Where(item => item.State.StableItemId == stableItemId).Select(item => item.ItemSeed)
            .Concat(player.Belt.Where(item => item.State.StableItemId == stableItemId).Select(item => item.ItemSeed))
            .GroupBy(seed => seed).ToDictionary(group => group.Key, group => group.Count());

    private static void ValidateNativeProgressionHealerStock(StoreSnapshot store)
    {
        var rows = store.Items.OrderBy(item => item.StoreSlot).ToArray();
        Require(store.StoreId == 4 && rows.Length == 2
            && rows[0].StoreSlot == 0 && rows[0].Price == 50
            && rows[0].State.ItemIndex == 77 && rows[0].State.StableItemId == 2180
            && rows[0].State.ItemActionKind == ItemActionKind.RestorePartialLife
            && rows[1].StoreSlot == 1 && rows[1].Price == 150
            && rows[1].State.ItemIndex == 78 && rows[1].State.StableItemId == 2181
            && rows[1].State.ItemActionKind == ItemActionKind.RestoreFullLife,
            "Pepin's stock does not match the two supported pinned native healing rows.");
    }

    private static NativeProgressionHealerStock CaptureNativeProgressionHealerStock(StoreSnapshot store)
    {
        var rows = store.Items.OrderBy(item => item.StoreSlot).Select(item => item.ToByteArray()).ToArray();
        return new NativeProgressionHealerStock(rows.Select(bytes => Convert.ToBase64String(bytes)).ToArray(),
            HashProtoCollection(rows));
    }

    private async Task<bool> CollectNearbyProgressionHealing(Main main)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        Require(player.Life > 0, "The Rogue died before collecting native generated healing.");
        var nearby = snapshot.WorldItems.Where(row => row.LevelId == player.LevelId
                && IsNativeHealingConsumable(row.State)
                && NativeDistance(player.PositionX, player.PositionY, row.PositionX, row.PositionY) <= 1)
            .Select(row => row.EntityId).ToArray();
        if (nearby.Length == 0)
            return false;
        var sequence = main.ObservedLastQueuedSequence;
        await CollectNearbyNativeHealingDropsAsync(main, nearby);
        return main.ObservedLastQueuedSequence > sequence;
    }

    private static void VerifyNativeProgressionRetention(Main main, NativeProgressionTownCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        Require(HashProto(DepthGeometry(main, 1)) == expected.L1GeometryHash
            && HashProto(DepthGeometry(main, 2)) == expected.L2GeometryHash,
            "A retained native floor changed its generated geometry or door mutations.");
        var earned = expected.L1;
        Require(!snapshot.WorldItems.Any(item => item.EntityId == earned.DropEntityId)
            && CountNativeItemOccurrences(main, snapshot.Players.Single(), earned.StableItemId, earned.ItemSeed) == 1
            && snapshot.Corpses.Count(corpse => corpse.EntityId == earned.MonsterEntityId) == 1,
            "A retained L1 floor revived the earned-gear victim, duplicated its claimed drop or lost the item.");
        foreach (var kill in expected.L1.Kills.Concat(expected.L1Gold.ApproachKills))
            Require(snapshot.Corpses.Count(corpse => corpse.EntityId == kill.MonsterEntityId) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == kill.MonsterEntityId && monster.Alive),
                $"A retained L1 floor revived or lost actual combat corpse {kill.MonsterEntityId}.");
        Require(expected.L2.Kills.Length > 0
            && expected.L2.ExperienceAfter > expected.L2.ExperienceBefore
            && expected.L2.WorkUsed <= NativeCombatWorkBudget.MaximumWork
            && expected.L2.Kills.All(kill => kill.AcceptedShots.Length > 0),
            "The fresh-host L2 checkpoint does not retain its actual combat, XP and accepted-shot proof.");
        foreach (var kill in expected.L2.Kills)
            Require(snapshot.Corpses.Count(corpse => corpse.EntityId == kill.MonsterEntityId) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == kill.MonsterEntityId && monster.Alive),
                $"A retained L2 floor revived or lost actual combat corpse {kill.MonsterEntityId}.");
    }

    private static string HashNativeProgressionWorld(Snapshot snapshot)
        => HashProtoCollection(snapshot.LevelGeometry.OrderBy(value => value.LevelId).Select(value => value.ToByteArray())
            .Concat(snapshot.Monsters.OrderBy(value => value.EntityId).Select(value => value.ToByteArray()))
            .Concat(snapshot.Objects.OrderBy(value => value.EntityId).Select(value => value.ToByteArray()))
            .Concat(snapshot.WorldItems.OrderBy(value => value.EntityId).Select(value => value.ToByteArray()))
            .Concat(snapshot.Corpses.OrderBy(value => value.EntityId).Select(value => value.ToByteArray()))
            .Concat(snapshot.Projectiles.OrderBy(value => value.EntityId).Select(value => value.ToByteArray()))
            .Concat(snapshot.Portals.OrderBy(value => value.PortalId).Select(value => value.ToByteArray())));
}
