using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/**
 * The default profile's Town to L4 journey as the Warrior: one ordinary melee kill
 * on each Cathedral floor, back to Town, then a fresh-host resume that revisits
 * every retained floor.
 */
public partial class SessionAcceptance
{
    private sealed record NativeWarriorFloor(uint LevelId, string GeometryHash, uint MonsterEntityId,
        ulong ExperienceBefore, ulong ExperienceAfter, int Attacks);

    private sealed record NativeWarriorDepthCheckpoint(string PlayerPayload, uint RandomState, ulong Sequence,
        string WorldHash, NativeWarriorFloor[] Floors);

    private const int NativeWarriorAttacksPerFloor = 160;
    private const int NativeWarriorTravelAttempts = 16;
    private int nativeWarriorRouteCount;
    // One-time evidence run: clear floors as a player would and record the Warrior entering each one.
    private const string GrindEnvironmentVariable = "GODEVILUTION_NATIVE_DEPTH_GRIND";
    private const string TargetEnvironmentVariable = "GODEVILUTION_NATIVE_DEPTH_TARGET";
    private const int NativeWarriorGrindKillsPerFloor = 40;
    // Single ordinary steps toward the nearest reachable monster on a whole floor.
    private const int NativeWarriorSearchMoves = 200;
    private const int NativeWarriorDoorOperations = 16;
    // Surrounded, hurt and out of potions, a player walks back to the Town healer and returns.
    private const int NativeWarriorRetreats = 4;
    // A full belt plus a row of inventory; the retreat keeps a reserve for the walk out.
    private const int NativeWarriorPotionSupply = 16;
    private const int NativeWarriorPotionReserve = 2;

    private sealed class NativeWarriorRetreatException(string message) : InvalidOperationException(message);

    private async Task VerifyNativeWarriorDepthsAsync(Main main, string directory, string phase, Evidence? previous)
    {
        var identity = main.ObservedIdentity!;
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_PROFILE");
        using var profileDocument = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath
            ?? throw new InvalidDataException("The default four-floor journey requires its native profile.")));
        ValidateNativeTownClassProfile(profileDocument.RootElement, identity.ContentManifestHash, 0);
        Require(nativeTownClassProfile && profileDocument.RootElement.GetProperty("nativeDepth").GetInt32() == 4,
            "The default four-floor journey requires the all-class native Town profile.");

        // A per-floor test descends to its target floor (default L4) with a test hero of matching strength.
        var target = uint.TryParse(System.Environment.GetEnvironmentVariable(TargetEnvironmentVariable), out var requested)
            && requested is >= 1 and <= 4 ? requested : 4U;
        if (phase == "NativeDepthJourney" && System.Environment.GetEnvironmentVariable(GrindEnvironmentVariable) == "1") {
            await GrindNativeWarriorAsync(main, directory);
            return;
        }
        if (phase == "NativeDepthJourney") {
            Require(previous is null && main.ObservedSnapshot!.Players.Single() is { LevelId: 0, CharacterClassId: 0 },
                "The depth journey must start with a new Warrior in Town.");
            await VisitNativeWarriorTownAsync(main);
            var floors = new List<NativeWarriorFloor>();
            for (uint depth = 1; depth <= target; depth++) {
                await TravelNativeWarriorAsync(main, directory, depth);
                var floor = await DefeatNativeWarriorFloorAsync(main, directory, depth);
                floors.Add(floor);
                var afterKill = main.ObservedSnapshot!.Players.Single();
                GD.Print($"Native Warrior L{depth} kill; XP={floor.ExperienceBefore}->{floor.ExperienceAfter}; life={afterKill.Life}");
                if (depth < target && (long)afterKill.Life * 2 < afterKill.LifeMaximum) {
                    // Back up to the Town healer, then down again; the retained floors keep their state.
                    for (var up = (int)depth - 1; up >= 0; up--)
                        await TravelNativeWarriorAsync(main, directory, (uint)up);
                    await VisitNativeWarriorTownAsync(main);
                    for (uint down = 1; down <= depth; down++)
                        await TravelNativeWarriorAsync(main, directory, down);
                    GD.Print($"Native Warrior healed in Town and returned to L{depth}");
                }
            }
            for (var destination = (int)target - 1; destination >= 0; destination--)
                await TravelNativeWarriorAsync(main, directory, (uint)destination);
            await UseNativeTownHealer(main);
            await Until(() => Settled(main), "The four-floor journey did not settle in Town.");
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var checkpoint = new NativeWarriorDepthCheckpoint(Convert.ToBase64String(player.ToByteArray()),
                snapshot.RandomState, main.ObservedLastQueuedSequence, HashNativeProgressionWorld(snapshot),
                floors.Select(floor => floor with { GeometryHash = HashProto(DepthGeometry(main, floor.LevelId)) }).ToArray());
            VerifyNativeWarriorRetention(main, checkpoint);
            WriteEvidence(directory, "nativedepthjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = player.EntityId, ClassId = player.CharacterClassId,
                LevelId = player.LevelId, X = player.PositionX, Y = player.PositionY,
                Sequence = checkpoint.Sequence, Visible = false, NativeDepthWarrior = checkpoint,
            });
            GD.Print($"Native Warrior Town/L1-L4 checkpoint passed; sequence={checkpoint.Sequence}");
            return;
        }

        Require(phase == "NativeDepthResume" && previous is { Passed: true },
            "The four-floor resume requires its completed journey.");
        using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "nativedepthjourney.json")));
        var expected = saved.RootElement.GetProperty("NativeDepthWarrior").Deserialize<NativeWarriorDepthCheckpoint>()!;
        Require(expected.Floors.Length == target, "The resume targets a different floor than its journey.");
        await AwaitNativeTownerColdReadinessAsync(main);
        var initial = main.ObservedSnapshot!;
        var hello = main.ObservedInitialConnection?.ServerHello;
        Require(Convert.ToBase64String(initial.Players.Single().ToByteArray()) == expected.PlayerPayload
            && initial.RandomState == expected.RandomState && HashNativeProgressionWorld(initial) == expected.WorldHash
            && main.ObservedInitialConnection is { AdmissionCompleted: true }
            && hello is { ResumedSessionMatched: true } && hello.LastClientSequence == expected.Sequence
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
            "The fresh host changed the saved Warrior, RNG, four-floor world or command cursor before input.");
        VerifyNativeWarriorRetention(main, expected);
        for (uint depth = 1; depth <= target; depth++) {
            await TravelNativeWarriorAsync(main, directory, depth);
            VerifyNativeWarriorRetention(main, expected);
        }
        for (var destination = (int)target - 1; destination >= 0; destination--) {
            await TravelNativeWarriorAsync(main, directory, (uint)destination);
            VerifyNativeWarriorRetention(main, expected);
        }
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == 0 && final.Life > 0, "The resumed four-floor journey did not return alive to Town.");
        WriteEvidence(directory, "nativedepthresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = final.EntityId, ClassId = final.CharacterClassId,
            LevelId = final.LevelId, X = final.PositionX, Y = final.PositionY,
            Sequence = main.ObservedLastQueuedSequence, Visible = false, ExactColdBaseline = true, AllFloorsRetained = true,
        });
        GD.Print("Native Warrior Town/L1-L4 fresh-host resume and retained revisit passed");
    }

    private async Task GrindNativeWarriorAsync(Main main, string directory)
    {
        Require(main.ObservedSnapshot!.Players.Single() is { LevelId: 0, CharacterClassId: 0 },
            "The grind must start with a fresh Warrior in Town.");
        var floors = new List<object>();
        void Record(uint depth)
            => WriteEvidence(directory, $"native-warrior-grind-l{depth}.json", new { Passed = false, Floors = floors.ToArray() });
        for (uint depth = 1; depth <= 4; depth++) {
            await TravelNativeWarriorAsync(main, directory, depth);
            var entry = DescribeNativeWarrior(main);
            var kills = 0;
            string stop = "kill limit";
            while (kills < NativeWarriorGrindKillsPerFloor) {
                var player = main.ObservedSnapshot!.Players.Single();
                if ((long)player.Life * 2 < player.LifeMaximum) {
                    for (var up = (int)depth - 1; up >= 0; up--)
                        await TravelNativeWarriorAsync(main, directory, (uint)up);
                    await UseNativeTownHealer(main);
                    for (uint down = 1; down <= depth; down++)
                        await TravelNativeWarriorAsync(main, directory, down);
                }
                try {
                    await DefeatNativeWarriorFloorAsync(main, directory, depth);
                    kills++;
                } catch (InvalidOperationException failure) when (main.ObservedSnapshot!.Players.Single().Life > 0) {
                    stop = failure.Message;
                    break;
                }
            }
            floors.Add(new { Depth = depth, Entry = entry, Exit = DescribeNativeWarrior(main), Kills = kills, Stop = stop });
            Record(depth);
            GD.Print($"Native Warrior grind L{depth}: kills={kills}; stop={stop}");
        }
        WriteEvidence(directory, "native-warrior-grind.json", new { Passed = true, Floors = floors.ToArray() });
    }

    /** A player's Town stop: Pepin heals, then his cheapest healing potion is bought up to the supply. */
    private async Task VisitNativeWarriorTownAsync(Main main)
    {
        await UseNativeTownHealer(main);
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var openSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "OpenHealer4");
        await AcceptedAndSettled(main, openSequence, "Open Pepin's native Healer stock");
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 4, "Pepin's native Healer stock did not open.", 5);
        for (var purchases = 0; purchases < 2 * NativeWarriorPotionSupply; purchases++) {
            var player = main.ObservedSnapshot!.Players.Single();
            var rows = main.ObservedSnapshot!.ActiveStore!.Items.OrderBy(item => item.StoreSlot).ToArray();
            var index = Array.FindIndex(rows, item => item.State.ItemActionKind == ItemActionKind.RestorePartialLife);
            var supply = NativeHealingSupply(player);
            if (supply >= NativeWarriorPotionSupply || index < 0 || player.Gold < rows[index].Price)
                break;
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await ClickListItem(sections, 2, (ItemList)main.FindChild("StoreItems", true, false), index);
            sequence = main.ObservedCommandResult?.ClientSequence ?? sequence;
            await ClickControl(main, sections, 2, "BuySelected");
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main),
                "Pepin's healing potion purchase did not settle.", 10);
            // A clock-gated purchase can arrive after its tick; that refusal changes nothing, so buy again.
            if (main.ObservedCommandResult is { Status: CommandStatus.Rejected, RejectReason: CommandRejectReason.TooLate })
                continue;
            Require(Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(main.ObservedCommandResult!),
                $"Buy a healing potion from Pepin: {main.ObservedCommandResult!.Status}/{main.ObservedCommandResult.RejectReason}.");
            await Until(() => NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) == supply + 1,
                "Pepin's healing potion purchase did not add a potion.", 5);
        }
        var closeSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, closeSequence, "Close Pepin's Healer store");
        var restocked = main.ObservedSnapshot!.Players.Single();
        GD.Print($"Native Warrior restocked in Town: potions={NativeHealingSupply(restocked)}; gold={restocked.Gold}");
    }

    /** The played-character facts a per-floor test hero reproduces. */
    private static object DescribeNativeWarrior(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        return new {
            player.CharacterLevel, player.Experience, player.Life, player.LifeMaximum, player.BaseLifeMaximum,
            player.Mana, player.ManaMaximum, player.Gold,
            Attributes = player.Attributes?.ToString(),
            Equipment = player.Equipment.Select(item => new { item.Slot, item.State.BaseName, item.State.StableItemId,
                item.State.Durability, item.State.MaxDurability }).ToArray(),
            Belt = player.Belt.Select(item => new { item.Slot, item.State.BaseName }).ToArray(),
            Inventory = player.Inventory.Select(item => item.State.BaseName).ToArray(),
        };
    }

    /** Ordinary route travel; an interrupted walk clears the nearby attackers and retries. */
    private async Task TravelNativeWarriorAsync(Main main, string directory, uint destination, bool flee = false)
    {
        for (var attempt = 0; attempt < NativeWarriorTravelAttempts; attempt++) {
            var player = main.ObservedSnapshot!.Players.Single();
            if (player.LevelId == destination)
                return;
            if (player.LevelId != 0 && !flee)
                await ClearNativeWarriorThreatsAsync(main, player.LevelId);
            await UseNativeHealingPotionIfNeeded(main);
            // Journey and resume run in separate processes, so the phase keeps their route evidence apart.
            var routeDirectory = System.IO.Path.Combine(directory, "native-warrior-routes",
                System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE") ?? "phase",
                $"{++nativeWarriorRouteCount:D4}-{player.LevelId}-to-{destination}");
            System.IO.Directory.CreateDirectory(routeDirectory);
            var route = DepthGeometry(main, player.LevelId).WarpTriggers.Single(item => item.Available && item.TargetLevelId == destination);
            try {
                // Walking continuity is the route tests' concern; the journey needs only the transition.
                await TravelCathedralRoute(main, routeDirectory, route.WarpId, destination, stopEarly: false,
                    minimumDistinctPositions: 1, distanceBoundedApproachDeadline: true,
                    interrupt: () => NativeHealingThresholdReached(main) && NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) > 0);
                Require(main.ObservedSnapshot!.Players.Single().LevelId == destination,
                    $"The ordinary route did not reach level {destination}.");
                return;
            } catch (InvalidOperationException failure) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == player.LevelId) {
                GD.Print($"Native Warrior travel to L{destination} interrupted ({failure.Message}); clearing and retrying; "
                    + DescribeNativeWarriorSurroundings(main));
                await Until(() => ((Button)main.FindChild("CancelTravel", true, false)).Disabled && Settled(main),
                    "Interrupted native Warrior travel did not settle.", 15);
            }
        }
        throw new InvalidOperationException($"The Warrior could not reach level {destination} within {NativeWarriorTravelAttempts} attempts.");
    }

    private static string DescribeNativeWarriorSurroundings(Main main)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        return $"life {player.Life}/{player.LifeMaximum} at ({player.PositionX},{player.PositionY}) potions {NativeHealingSupply(player)}; nearby ["
            + string.Join("; ", snapshot.Monsters.Where(monster => monster.Alive && monster.LevelId == player.LevelId
                    && NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY) <= 8)
                .Select(monster => $"{monster.EntityId}:type{monster.MonsterId}:({monster.PositionX},{monster.PositionY}):hp{monster.HitPoints}"))
            + "]";
    }

    /** Kills every live monster within reach of the Warrior before it walks on. */
    private async Task ClearNativeWarriorThreatsAsync(Main main, uint levelId)
    {
        var skipped = new HashSet<uint>();
        for (var kills = 0; kills < 12; kills++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var threat = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion && monster.LevelId == levelId
                    && !skipped.Contains(monster.EntityId)
                    && NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY) <= 4)
                .OrderBy(monster => NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY))
                .ThenBy(monster => monster.EntityId).FirstOrDefault();
            if (threat is null || skipped.Contains(threat.EntityId))
                return;
            uint? killed;
            try {
                (killed, _) = await AttackNativeWarriorTargetAsync(main, levelId, threat.EntityId, NativeWarriorAttacksPerFloor);
            } catch (NativeWarriorRetreatException) {
                return; // Walk on instead; the route itself is the way out.
            }
            if (killed is null)
                skipped.Add(threat.EntityId);
        }
    }

    /** One generated kill that earns XP on the current floor. */
    private async Task<NativeWarriorFloor> DefeatNativeWarriorFloorAsync(Main main, string directory, uint levelId)
    {
        var retreats = 0;
        var experienceBefore = main.ObservedSnapshot!.Players.Single().Experience;
        var attacks = 0;
        var doors = new HashSet<uint>();
        var doorEvidence = new List<NativeDoorOperationEvidence>();
        var doorCommands = new Dictionary<uint, int>();
        var work = new NativeCombatWorkBudget();
        while (attacks < NativeWarriorAttacksPerFloor) {
            await UseNativeHealingPotionIfNeeded(main);
            MonsterSnapshot target;
            try {
                target = await FindNativeClassTargetAsync(main, ranged: false, levelId, NativeWarriorSearchMoves);
            } catch (InvalidOperationException) when (main.ObservedSnapshot!.Players.Single().Life > 0
                && doors.Count < NativeWarriorDoorOperations) {
                // Every nearby monster is behind a closed door: open the nearest reachable one and search again.
                var opened = doors.Count;
                do {
                    await ClearNativeWarriorThreatsAsync(main, levelId);
                    await UseNativeHealingPotionIfNeeded(main);
                } while (doors.Count == opened && await AdvanceTowardReachableNativeDoor(main, doorCommands, doors, doorEvidence, work));
                if (doors.Count == opened)
                    throw;
                GD.Print($"Native Warrior opened L{levelId} door {doorEvidence[^1].EntityId} to reach a monster");
                continue;
            }
            uint? killed;
            int used;
            try {
                (killed, used) = await AttackNativeWarriorTargetAsync(main, levelId, target.EntityId, NativeWarriorAttacksPerFloor - attacks);
            } catch (NativeWarriorRetreatException retreat) when (retreats < NativeWarriorRetreats) {
                retreats++;
                GD.Print($"Native Warrior retreating from L{levelId} ({retreat.Message})");
                for (var up = (int)levelId - 1; up >= 0; up--)
                    await TravelNativeWarriorAsync(main, directory, (uint)up, flee: up == (int)levelId - 1);
                await VisitNativeWarriorTownAsync(main);
                for (uint down = 1; down <= levelId; down++)
                    await TravelNativeWarriorAsync(main, directory, down);
                continue;
            }
            attacks += used;
            var player = main.ObservedSnapshot!.Players.Single();
            if (killed is { } monster && player.Experience > experienceBefore)
                return new(levelId, string.Empty, monster, experienceBefore, player.Experience, attacks);
        }
        throw new InvalidOperationException($"The Warrior earned no L{levelId} kill within {NativeWarriorAttacksPerFloor} attacks.");
    }

    /**
     * Ordinary right-click melee: always strike the weakest monster already adjacent (the ones
     * hitting the Warrior), and otherwise close in on the target. In melee below 60% life with
     * only the potion reserve left, it throws NativeWarriorRetreatException. Returns the
     * first attacked monster that dies, or null when the target escapes, is unreachable, or the
     * budget ends.
     */
    private async Task<(uint? Killed, int Attacks)> AttackNativeWarriorTargetAsync(Main main, uint levelId, uint targetId, int budget)
    {
        var attacks = 0;
        var attacked = new HashSet<uint>();
        var swings = new List<string>();
        while (attacks < budget) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.Life > 0, $"The Warrior died on L{levelId} during combat; last swings [{string.Join("; ", swings.TakeLast(12))}]; "
                + $"nearby [{string.Join("; ", snapshot.Monsters.Where(monster => monster.Alive && monster.LevelId == levelId
                    && NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY) <= 3)
                    .Select(monster => $"{monster.EntityId}:type{monster.MonsterId}:lvl{monster.MonsterLevel}:hp{monster.HitPoints}/{monster.MaxHitPoints}:ac{monster.ArmorClass}:dmg{monster.AttackDamage}"))}].");
            Require(player.LevelId == levelId, $"The Warrior left L{levelId} during combat.");
            if (snapshot.Corpses.FirstOrDefault(corpse => attacked.Contains(corpse.EntityId)) is { } corpse)
                return (corpse.EntityId, attacks);
            var adjacentMonsters = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion && monster.LevelId == levelId
                    && NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY) <= 1).ToArray();
            // Focus the weakest attacker so a pack loses members before its regeneration catches up.
            var adjacent = adjacentMonsters.OrderBy(monster => monster.HitPointsQ6)
                .ThenBy(monster => monster.EntityId == targetId ? 0 : 1).ThenBy(monster => monster.EntityId).FirstOrDefault();
            if (adjacentMonsters.Length >= 1 && (long)player.Life * 5 < (long)player.LifeMaximum * 3
                && NativeHealingSupply(player) <= NativeWarriorPotionReserve)
                throw new NativeWarriorRetreatException(
                    $"{adjacentMonsters.Length} attackers, life {player.Life}/{player.LifeMaximum}, potions {NativeHealingSupply(player)}");
            if (adjacent is null) {
                var target = snapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
                if (target is null)
                    return (null, attacks);
                MonsterSnapshot approached;
                try {
                    approached = await FindNativeClassTargetAsync(main, ranged: false, levelId);
                } catch (InvalidOperationException) when (main.ObservedSnapshot!.Players.Single().Life > 0) {
                    return (null, attacks); // Not reachable from here (for example behind a wall).
                }
                if (approached.EntityId != targetId
                    && NativeDistance(player.PositionX, player.PositionY, approached.PositionX, approached.PositionY) > 1)
                    return (null, attacks);
                continue;
            }
            await UseNativeHealingPotionIfNeeded(main);
            attacks++;
            attacked.Add(adjacent.EntityId);
            var sequence = main.ObservedLastQueuedSequence;
            await ClickWorld(main, adjacent.PositionX, adjacent.PositionY, MouseButton.Right);
            // A monster that stepped off the clicked tile yields no attack command; re-observe and retry.
            for (var frame = 0; frame < 3 && main.ObservedLastQueuedSequence == sequence; frame++)
                await Frame();
            if (main.ObservedLastQueuedSequence == sequence)
                continue;
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && main.ObservedSnapshot!.Tick >= result.AppliedTick,
                "Native Warrior melee did not reach its authoritative receipt.", 15);
            await Until(() => Settled(main) || main.ObservedSnapshot!.Players.Single().Life <= 0,
                "Native Warrior melee did not reach its action boundary.", 30);
            var receipt = main.ObservedCommandResult!;
            var after = main.ObservedSnapshot!;
            swings.Add($"{adjacent.EntityId}:hp{adjacent.HitPoints}->{after.Monsters.SingleOrDefault(monster => monster.EntityId == adjacent.EntityId)?.HitPoints}"
                + $":{receipt.Status}/{receipt.RejectReason}:life{after.Players.Single().Life}");
            // A live monster can move or recover before the clocked attack applies; re-observe and retry.
            if (!Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt))
                Require(receipt.Status == CommandStatus.Rejected
                    && receipt.RejectReason is CommandRejectReason.NotAllowed or CommandRejectReason.InvalidTarget,
                    $"Unexpected native Warrior attack refusal: {receipt.Status}/{receipt.RejectReason}.");
        }
        return (null, attacks);
    }

    private static void VerifyNativeWarriorRetention(Main main, NativeWarriorDepthCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        Require(expected.Floors.Length is >= 1 and <= 4
            && expected.Floors.Select(floor => floor.LevelId).SequenceEqual(Enumerable.Range(1, expected.Floors.Length).Select(level => (uint)level)),
            "The depth checkpoint omitted or reordered a floor.");
        foreach (var floor in expected.Floors) {
            Require(HashProto(DepthGeometry(main, floor.LevelId)) == floor.GeometryHash,
                $"Retained native floor {floor.LevelId} changed its generated geometry or door state.");
            Require(floor.ExperienceAfter > floor.ExperienceBefore
                && snapshot.Corpses.Count(corpse => corpse.EntityId == floor.MonsterEntityId && corpse.LevelId == floor.LevelId) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == floor.MonsterEntityId && monster.Alive),
                $"Retained native floor {floor.LevelId} revived or lost its combat corpse {floor.MonsterEntityId}.");
        }
    }
}
