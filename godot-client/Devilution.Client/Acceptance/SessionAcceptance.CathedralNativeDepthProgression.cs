using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeDepthProgressionGeometry(uint LevelId, string Hash);
    private sealed record NativeDepthProgressionCombat(uint LevelId, NativeProgressionCombatEvidence Combat);
    private sealed record NativeDepthProgressionTownCheckpoint(
        string PlayerPayload, uint RandomState, ulong Sequence, string WorldHash,
        NativeDepthProgressionGeometry[] Floors, NativeEquipmentKillEvidence L1,
        NativeProgressionGoldCollection L1Gold, NativeDepthProgressionCombat[] Combat,
        NativeProgressionHealerStock HealerStock);

    /** The Rogue four-floor journey on the separate native depth-progression profile. */
    private static bool IsNativeDepthProgressionPhase(string? phase)
        => phase is "CathedralNativeDepthProgressionJourney" or "CathedralNativeDepthProgressionResume";

    private async Task VerifyCathedralNativeDepthProgressionAsync(
        Main main, string directory, string phase, bool visible, Evidence? previous)
    {
        var path = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        Require(!string.IsNullOrWhiteSpace(path), "Native depth progression requires its explicit compiled profile.");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path!));
        var profile = document.RootElement;
        Require(profile.GetProperty("formatVersion").GetInt32() == 3
            && profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-depth-progression.v1"
            && profile.GetProperty("nativeDepth").GetInt32() == 4
            && !profile.GetProperty("nativePopulationClaim").GetBoolean(),
            "The supplied profile does not select the separate native Town/L1-L4 route.");
        var identity = main.ObservedIdentity!;
        Require(identity.ContentManifestHash == profile.GetProperty("contentManifestHash").GetString(),
            "The native depth progression host admitted a different content pack.");
        var journeyPhase = phase == "CathedralNativeDepthProgressionJourney";
        var entryContinuationPath = System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_ENTRY_BASELINE");
        var entryContinuation = phase == "CathedralNativeDepthProgressionResume"
            && !string.IsNullOrWhiteSpace(entryContinuationPath);
        var entryContinuationTownResume = entryContinuation
            && IsNativeDepthEntryTownCheckpointBaseline(entryContinuationPath!);
        var leg = 0;
        async Task Travel(uint destination)
        {
            var routeDirectory = System.IO.Path.Combine(directory, "native-depth-progression-routes", phase,
                (++leg).ToString(System.Globalization.CultureInfo.InvariantCulture));
            System.IO.Directory.CreateDirectory(routeDirectory);
            if (main.ObservedSnapshot!.Players.Single().LevelId != 0) {
                await WaitForNativeDepthProgressionCommandBoundary(main,
                    $"before stair approach to L{destination}");
                await ApproachNativeProgressionStair(main, routeDirectory, destination,
                    allowDoorOperations: journeyPhase || (entryContinuation && !entryContinuationTownResume));
            }
            await TravelNativeCathedralRoute(main, routeDirectory, destination);
        }
        async Task TownCheckpoint()
        {
            await DrainNativeBarrelTownCheckpoint(main,
                (Button)main.FindChild("CancelTravel", true, false),
                main.FindChild("TravelStatus", true, false) as Label);
        }

        if (entryContinuation) {
            await VerifyNativeDepthEntryContinuationAsync(main, directory, entryContinuationPath!, visible,
                Travel, TownCheckpoint);
            return;
        }

        if (journeyPhase) {
            Require(previous is null && main.ObservedSnapshot!.Players.Single().LevelId == 0,
                "Native depth progression must start with a fresh Rogue in Town.");
            await VerifyNativeConsumableSelectionRefresh(main, directory);
            await Travel(1);
            var bow = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).Clone();
            var l1 = await FightForNativeEquipmentAsync(main, directory, visible, useCardinalLootApproach: true);
            await ReequipNativeBarrelWeapon(main, bow);
            var gold = await CollectNativeProgressionGoldAsync(main, l1);
            await Travel(0);
            var supplies = await BuyNativeProgressionHealingAsync(main, targetSupply: 6, maximumPurchases: 8);
            Require(gold.Pickups.Length > 0 && supplies.PurchaseVerified && supplies.Purchases.Length > 0,
                "The four-floor journey did not collect earned L1 Gold and buy ordinary Pepin supplies.");
            await UseNativeTownHealer(main);
            GD.Print("Native depth progression L1 equipment/earned Gold/Pepin passed; descending through L2-L4");
            await Travel(1);
            var combat = new List<NativeDepthProgressionCombat>();
            for (uint depth = 2; depth <= 4; depth++) {
                await Travel(depth);
                var entered = main.ObservedSnapshot!.Players.Single();
                WriteEvidence(directory, $"native-depth-progression-l{depth}-entry.json", new {
                    entered.LevelId, entered.Life, entered.LifeMaximum, entered.Experience, entered.Gold,
                    entered.PositionX, entered.PositionY,
                    ThreatSpacing = DescribeNativeThreatSpacing(main.ObservedSnapshot!, entered.LevelId,
                        entered.PositionX, entered.PositionY),
                    HealingItems = NativeHealingSupply(entered), Sequence = main.ObservedCommandResult?.ClientSequence,
                });
                GD.Print($"Native depth progression L{depth} entry; life={entered.Life}/{entered.LifeMaximum}; healing={NativeHealingSupply(entered)}");
                var defeated = await FightNativeProgressionCombatAsync(main, directory, depth);
                combat.Add(new(depth, defeated));
                await ReequipNativeBarrelWeapon(main, bow);
                GD.Print($"Native depth progression L{depth} first combat passed; XP={defeated.ExperienceBefore}->{defeated.ExperienceAfter}; "
                    + $"observed encounter-area new drops={defeated.ObservedNewDropEntityIds.Length}");
            }
            for (var destination = 3; destination >= 0; destination--)
                await Travel((uint)destination);
            await UseNativeTownHealer(main);
            await TownCheckpoint();
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var checkpoint = new NativeDepthProgressionTownCheckpoint(
                Convert.ToBase64String(player.ToByteArray()), snapshot.RandomState,
                main.ObservedCommandResult!.ClientSequence, HashNativeProgressionWorld(snapshot),
                Enumerable.Range(1, 4).Select(depth => new NativeDepthProgressionGeometry((uint)depth,
                    HashProto(DepthGeometry(main, (uint)depth)))).ToArray(),
                l1, gold, combat.ToArray(), supplies.Stock);
            VerifyNativeDepthProgressionRetention(main, checkpoint);
            WriteEvidence(directory, "cathedralnativedepthprogressionjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = player.EntityId, ClassId = player.CharacterClassId,
                LevelId = player.LevelId, X = player.PositionX, Y = player.PositionY,
                Sequence = checkpoint.Sequence, Visible = visible, NativeDepthProgression = checkpoint,
                GoldCollection = gold, HealerPreparation = supplies,
            });
            GD.Print($"Native Town/L1-L4 combat/retention checkpoint passed; sequence={checkpoint.Sequence}");
            return;
        }

        Require(phase == "CathedralNativeDepthProgressionResume" && previous is { Passed: true },
            "Native depth progression Resume requires its completed four-floor journey.");
        using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "cathedralnativedepthprogressionjourney.json")));
        var expected = saved.RootElement.GetProperty("NativeDepthProgression")
            .Deserialize<NativeDepthProgressionTownCheckpoint>()!;
        var initial = main.ObservedSnapshot!;
        Require(Convert.ToBase64String(initial.Players.Single().ToByteArray()) == expected.PlayerPayload
            && initial.RandomState == expected.RandomState && HashNativeProgressionWorld(initial) == expected.WorldHash,
            "Fresh-host native depth progression changed the saved player, RNG or full four-floor world before input.");
        var connection = main.ObservedInitialConnection;
        Require(connection is { AdmissionCompleted: true, ServerHello: { ResumedSessionMatched: true } }
            && connection.ServerHello.LastClientSequence == expected.Sequence
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
            "Fresh-host native depth progression did not resume the exact command cursor before input.");
        VerifyNativeDepthProgressionRetention(main, expected);
        GD.Print("Native depth progression exact cold player/cursor/RNG/four-floor world verified before input");
        var stock = await BuyNativeProgressionHealingAsync(main,
            targetSupply: NativeHealingSupply(initial.Players.Single()) + 1,
            maximumPurchases: 1, expectedStock: expected.HealerStock);
        var healer = await UseNativeTownHealer(main);
        for (uint depth = 1; depth <= 4; depth++) {
            await Travel(depth);
            VerifyNativeDepthProgressionRetention(main, expected);
        }
        for (var destination = 3; destination >= 0; destination--) {
            await Travel((uint)destination);
            VerifyNativeDepthProgressionRetention(main, expected);
        }
        await TownCheckpoint();
        VerifyNativeDepthProgressionRetention(main, expected);
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == 0 && final.Life > 0, "The resumed four-floor journey did not return alive to Town.");
        WriteEvidence(directory, "cathedralnativedepthprogressionresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = final.EntityId, ClassId = final.CharacterClassId,
            LevelId = final.LevelId, X = final.PositionX, Y = final.PositionY,
            Sequence = main.ObservedCommandResult!.ClientSequence, Visible = visible,
            ExactColdBaseline = true, AllFloorsRetained = true, Healer = healer, HealerStockContinuation = stock,
        });
        GD.Print($"Native Town/L1-L4 fresh-host resume and retained revisit passed; sequence={main.ObservedCommandResult.ClientSequence}");
    }

    private static uint? FindNativeDepthProgressionStairBlocker(Snapshot snapshot, int stairX, int stairY)
    {
        if (!IsNativeDepthProgressionPhase(System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")))
            return null;
        var player = snapshot.Players.Single();
        var planning = snapshot.Clone();
        planning.Monsters.Clear();
        var path = FindNativeChestApproachPath(planning, DepthGeometryFromSnapshot(snapshot, player.LevelId),
            new ObjectSnapshot { LevelId = player.LevelId, PositionX = stairX, PositionY = stairY });
        if (path is null)
            return null;
        foreach (var cell in path) {
            var blocker = snapshot.Monsters.Where(monster => monster.LevelId == player.LevelId && monster.Alive
                    && ((monster.PositionX == cell.X && monster.PositionY == cell.Y)
                        || (monster.Moving && monster.FuturePositionX == cell.X && monster.FuturePositionY == cell.Y)))
                .OrderBy(monster => monster.EntityId).FirstOrDefault();
            if (blocker is not null)
                return blocker.EntityId;
        }
        return null;
    }

    private async Task WaitForNativeDepthProgressionCommandBoundary(Main main, string context)
    {
        if (!IsNativeDepthProgressionPhase(System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")))
            return;
        await Until(() => main.ObservedPendingCommandCount == 0 && Settled(main)
            && (main.ObservedCommandResult is null
                || main.ObservedCommandResult.ClientSequence == main.ObservedLastQueuedSequence),
            $"Native depth-progression {context} did not reach a settled command boundary.", stageSeconds: 10);
    }

    private static void VerifyNativeDepthProgressionRetention(Main main, NativeDepthProgressionTownCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        Require(expected.Floors.Select(floor => floor.LevelId).SequenceEqual(new uint[] { 1, 2, 3, 4 })
            && expected.Combat.Select(floor => floor.LevelId).SequenceEqual(new uint[] { 2, 3, 4 }),
            "Native depth progression omitted or reordered a required floor's geometry/combat proof.");
        foreach (var floor in expected.Floors)
            Require(HashProto(DepthGeometry(main, floor.LevelId)) == floor.Hash,
                $"Retained native floor {floor.LevelId} changed its generated geometry or door mutations.");
        var item = expected.L1;
        Require(snapshot.WorldItems.All(row => row.EntityId != item.DropEntityId)
            && CountNativeItemOccurrences(main, snapshot.Players.Single(), item.StableItemId, item.ItemSeed) == 1,
            "The retained native run lost or duplicated its claimed L1 equipment.");
        Require(expected.L1Gold.Pickups.Length > 0
            && expected.L1Gold.Pickups.All(pickup =>
                snapshot.WorldItems.All(row => row.EntityId != pickup.EntityId)),
            "A collected L1 Gold pickup respawned during retained-floor revisit.");
        void RetainedKill(NativeKillEvidence kill, uint level)
        {
            Require(kill.AcceptedShots.Length > 0 && kill.ExperienceAfter > kill.ExperienceBefore
                && snapshot.Corpses.Count(corpse => corpse.EntityId == kill.MonsterEntityId && corpse.LevelId == level) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == kill.MonsterEntityId && monster.Alive),
                $"Retained native floor {level} revived or lost actual combat corpse {kill.MonsterEntityId}.");
        }
        foreach (var kill in expected.L1.Kills.Concat(expected.L1Gold.ApproachKills))
            RetainedKill(kill, 1);
        foreach (var floor in expected.Combat) {
            Require(floor.Combat.Kills.Length > 0 && floor.Combat.ExperienceAfter > floor.Combat.ExperienceBefore
                && floor.Combat.WorkUsed is > 0 and <= NativeCombatWorkBudget.MaximumWork,
                $"Native floor {floor.LevelId} has no bounded actual encounter/XP proof.");
            foreach (var kill in floor.Combat.Kills)
                RetainedKill(kill, floor.LevelId);
        }
    }
}
