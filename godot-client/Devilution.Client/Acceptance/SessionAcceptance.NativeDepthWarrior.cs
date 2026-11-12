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
        string WorldHash, NativeWarriorFloor[] Floors, NativeCatacombsObjectCheckpoint? Objects = null, NativeShrineCheckpoint? Shrine = null,
        NativeThemeObjectCheckpoint? ThemeObject = null, NativeTrappedSarcophagusCheckpoint? TrappedSarcophagus = null);

    private const int NativeWarriorAttacksPerFloor = 160;
    // L5-L8 melee families fight as native since batch 2.4a, so every floor records a kill.
    private const uint NativeWarriorDeepestFloor = 8;
    private const uint NativeWarriorFoughtFloor = 8;
    private const uint NativeWarriorTownWarpDepth = 5;
    private const int NativeWarriorTravelAttempts = 16;
    private int nativeWarriorRouteCount;
    // One-time evidence run: clear floors as a player would and record the Warrior entering each one.
    private const string GrindEnvironmentVariable = "GODEVILUTION_NATIVE_DEPTH_GRIND";
    private const string TargetEnvironmentVariable = "GODEVILUTION_NATIVE_DEPTH_TARGET";
    // Development runs may fight only from this floor down; shallower floors are traversed without a kill.
    private const string FirstFightEnvironmentVariable = "GODEVILUTION_NATIVE_DEPTH_FIRST_FIGHT";

    private static uint NativeWarriorFirstFightFloor
        => uint.TryParse(System.Environment.GetEnvironmentVariable(FirstFightEnvironmentVariable), out var first)
            && first is >= 1 and <= NativeWarriorDeepestFloor ? first : 1U;
    private const int NativeWarriorGrindKillsPerFloor = 40;
    // Single ordinary steps toward the nearest reachable monster on a whole floor.
    private const int NativeWarriorSearchMoves = 200;
    private const int NativeWarriorDoorOperations = 16;
    private const int NativeWarriorDoorApproachAttempts = 3;
    private const int NativeWarriorDoorObstructionTries = 6;
    private const int NativeWarriorPackRadius = 6;
    // Below this many potions the Warrior restocks in Town before the next floor.
    private const int NativeWarriorPotionRestock = 6;
    private const int NativeWarriorTargetApproaches = 8;
    // An L6-L8 kill can cost a dozen potions, so the Catacombs restock comes back with a near-full stock.
    private const int NativeWarriorCatacombsPotionRestock = 12;
    private const int NativeWarriorPackSize = 3;
    // Surrounded, hurt and out of potions, a player walks back to the Town healer and returns.
    private const int NativeWarriorRetreats = 8;
    // A full belt plus a row of inventory; the retreat keeps a reserve for the walk out.
    private const int NativeWarriorPotionSupply = 16;
    private const int NativeWarriorPotionReserve = 2;
    // L5-L8 packs pin the Warrior for longer, so the walk out keeps more.
    private const int NativeWarriorCatacombsPotionReserve = 6;

    private static int NativeWarriorPotionReserveOn(uint levelId)
        => levelId >= NativeWarriorTownWarpDepth ? NativeWarriorCatacombsPotionReserve : NativeWarriorPotionReserve;

    private sealed class NativeWarriorRetreatException(string message) : InvalidOperationException(message);

    private async Task VerifyNativeWarriorDepthsAsync(Main main, string directory, string phase, Evidence? previous)
    {
        nativeWarriorTiming = new(main);
        var passed = false;
        try {
            await RunNativeWarriorDepthsAsync(main, directory, phase, previous);
            passed = true;
        } finally {
            try {
                nativeWarriorTiming.Write(directory, phase, passed);
            } catch (Exception exception) when (!passed) {
                GD.PrintErr($"Native Warrior timing was not written: {exception.Message}");
            }
            nativeWarriorTiming = null;
            main.AcceptanceSnapshotObserver = null;
        }
    }

    private async Task RunNativeWarriorDepthsAsync(Main main, string directory, string phase, Evidence? previous)
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
            && requested is >= 1 and <= NativeWarriorDeepestFloor ? requested : 4U;
        if (phase == "NativeDepthJourney" && System.Environment.GetEnvironmentVariable(GrindEnvironmentVariable) == "1") {
            await GrindNativeWarriorAsync(main, directory);
            return;
        }
        if (phase == "NativeDepthJourney") {
            Require(previous is null && main.ObservedSnapshot!.Players.Single() is { LevelId: 0, CharacterClassId: 0 },
                "The depth journey must start with a new Warrior in Town.");
            ConfigureNativeCatacombsRequiredDoor();
            StartNativeWarriorDrawCapture(main);
            var draws = new List<NativeWarriorFloorDraw> { await VerifyNativeWarriorFloorDrawAsync(main, directory, 0) };
            var panels = await VerifyNativeWarriorPanelsAsync(main, directory);
            var controls = await VerifyNativeWarriorControlsAsync(main, directory);
            await VisitNativeWarriorTownAsync(main);
            var floors = new List<NativeWarriorFloor>();
            for (uint depth = 1; depth <= target; depth++) {
                await TravelNativeWarriorAsync(main, directory, depth);
                draws.Add(await VerifyNativeWarriorFloorDrawAsync(main, directory, depth));
                await TryNativeTrappedSarcophagusAsync(main);
                await TryNativeThemeObjectAsync(main);
                await TryNativeShrineAsync(main);
                if (depth > NativeWarriorFoughtFloor || depth < NativeWarriorFirstFightFloor) {
                    if (depth >= 5)
                        nativeCatacombsObjects = await TryNativeCatacombsObjectsAsync(main);
                    var entered = main.ObservedSnapshot!.Players.Single();
                    floors.Add(new(depth, string.Empty, 0, entered.Experience, entered.Experience, 0));
                    GD.Print($"Native Warrior entered L{depth}; life={entered.Life}");
                    continue;
                }
                var floor = await DefeatNativeWarriorFloorAsync(main, directory, depth);
                floors.Add(floor);
                if (depth >= 5)
                    nativeCatacombsObjects = await TryNativeCatacombsObjectsAsync(main);
                var afterKill = main.ObservedSnapshot!.Players.Single();
                GD.Print($"Native Warrior L{depth} kill; XP={floor.ExperienceBefore}->{floor.ExperienceAfter}; life={afterKill.Life}");
                if (depth < target && ((long)afterKill.Life * 2 < afterKill.LifeMaximum
                        || NativeHealingSupply(afterKill) < (depth >= NativeWarriorTownWarpDepth
                            ? NativeWarriorCatacombsPotionRestock : NativeWarriorPotionRestock))) {
                    // Back up to the Town healer and potion stock, then down again; the retained floors keep their state.
                    for (var up = (int)depth - 1; up >= 0; up--)
                        await TravelNativeWarriorAsync(main, directory, (uint)up);
                    await VisitNativeWarriorTownAsync(main);
                    for (uint down = 1; down <= depth; down++)
                        await TravelNativeWarriorAsync(main, directory, down);
                    GD.Print($"Native Warrior healed in Town and returned to L{depth}");
                }
            }
            NativeCatacombsChestStage.VerifyAvailability(target,
                System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_SEED"), nativeCatacombsObjects?.ChestEntity > 0);
            Require(!NativeWarriorRequiresShrine || nativeShrine is not null,
                "No visited floor offered a nearby reachable native shrine with a lasting effect. " + string.Join("; ", nativeShrineApproaches));
            Require(!NativeWarriorRequiresThemeObject || nativeThemeObject is not null,
                "No visited floor offered a nearby reachable native theme object with a demonstrable effect. " + string.Join("; ", nativeThemeObjectApproaches));
            // From L5 and below, the way home is L5's warp-up, which opens the Town warp (batch 2.2c).
            var stairsUpTo = target >= NativeWarriorTownWarpDepth ? (int)NativeWarriorTownWarpDepth : 0;
            for (var destination = (int)target - 1; destination >= stairsUpTo; destination--)
                await TravelNativeWarriorAsync(main, directory, (uint)destination);
            // Count actual draws on arrival and while operating visible objects along the route.
            Require(target < NativeWarriorTownWarpDepth || nativeWarriorCatacombsDoorDraws + nativeWarriorCatacombsOperatedObjectDraws
                    + draws.Where(draw => draw.LevelId >= NativeWarriorTownWarpDepth).Sum(draw => draw.NativeObjects) > 0,
                "No Catacombs object was drawn with native art on L5-L8.");
            var townWarp = target >= NativeWarriorTownWarpDepth ? await NativeWarriorTownWarpLegAsync(main, directory) : null;
            await HealNativeWarriorInTownAsync(main);
            await Until(() => Settled(main), "The four-floor journey did not settle in Town.");
            draws.Add(await VerifyNativeWarriorFloorDrawAsync(main, directory, 0));
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            nativeCatacombsObjects = WithNativeCatacombsDoor(nativeCatacombsObjects);
            Require(!NativeWarriorRequiresTrappedDoor || nativeCatacombsRequiredDoorFiring is not null,
                "The required seeded run did not fire a reachable Catacombs door trap.");
            var checkpoint = new NativeWarriorDepthCheckpoint(Convert.ToBase64String(player.ToByteArray()),
                snapshot.RandomState, main.ObservedLastQueuedSequence, HashNativeProgressionWorld(snapshot),
                floors.Select(floor => floor with { GeometryHash = HashProto(DepthGeometry(main, floor.LevelId)) }).ToArray(),
                nativeCatacombsObjects, nativeShrine, nativeThemeObject, nativeTrappedSarcophagus);
            VerifyNativeWarriorRetention(main, checkpoint);
            WriteEvidence(directory, "nativedepthjourney.json", new {
                Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
                RulesetHash = identity.RulesetIdentityHash, EntityId = player.EntityId, ClassId = player.CharacterClassId,
                LevelId = player.LevelId, X = player.PositionX, Y = player.PositionY,
                Sequence = checkpoint.Sequence, Visible = NativeWarriorVisible, NativeDepthWarrior = checkpoint,
                RequiredChest = NativeCatacombsChestStage.RequiresEvidence(System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_SEED")),
                ChestPickedUp = checkpoint.Objects?.ChestEntity > 0,
                ShrineApproaches = nativeShrineApproaches, RequiredShrine = NativeWarriorRequiresShrine,
                ShrineOperated = checkpoint.Shrine is not null,
                RequiredThemeObject = NativeWarriorRequiresThemeObject, ThemeObjectOperated = checkpoint.ThemeObject is not null,
                ThemeObjectApproaches = nativeThemeObjectApproaches,
                RequiredTrappedSarcophagus = NativeWarriorRequiresTrappedSarcophagus, TrappedSarcophagusOpened = checkpoint.TrappedSarcophagus is not null,
                RequiredTrappedDoor = NativeWarriorRequiresTrappedDoor, TrappedDoorOperated = checkpoint.Objects?.DoorEntity > 0,
                RequiredDoorTarget = nativeCatacombsRequiredDoor,
                UnreachableNativeObjects = nativeCatacombsUnreachableObjects,
                NativeDraws = draws.ToArray(), NativePanels = panels, NativeControls = controls, TownWarp = townWarp,
            });
            GD.Print($"Native Warrior Town/L1-L{target} checkpoint passed; sequence={checkpoint.Sequence}");
            return;
        }

        Require(phase == "NativeDepthResume" && previous is { Passed: true },
            "The four-floor resume requires its completed journey.");
        using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "nativedepthjourney.json")));
        var expected = saved.RootElement.GetProperty("NativeDepthWarrior").Deserialize<NativeWarriorDepthCheckpoint>()!;
        Require(expected.Floors.Length == target, "The resume targets a different floor than its journey.");
        using (NativeWarriorStep("checks", "cold Town readiness"))
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
        Require(target < NativeWarriorTownWarpDepth || NativeWarriorTownWarpOpen(main),
            "The fresh host did not restore the open Town warp.");
        for (uint depth = 1; depth <= target; depth++) {
            await TravelNativeWarriorAsync(main, directory, depth);
            Require(main.ObservedSnapshot!.Players.Single().LevelId == depth,
                $"The retained native revisit did not publish L{depth}'s actual receiving view.");
            VerifyNativeWarriorRetention(main, expected);
            await VerifyNativeThemeObjectReuseAsync(main, expected.ThemeObject);
        }
        for (var destination = (int)target - 1; destination >= 0; destination--) {
            await TravelNativeWarriorAsync(main, directory, (uint)destination);
            VerifyNativeWarriorRetention(main, expected);
        }
        if (target >= NativeWarriorTownWarpDepth) {
            await TravelNativeWarriorAsync(main, directory, NativeWarriorTownWarpDepth);
            await TravelNativeWarriorAsync(main, directory, 0);
        }
        Require(expected.ThemeObject is null || !NativeThemeObjectJourneyEvidence.IsReusable(expected.ThemeObject.Type)
                || nativeThemeObjectReuseVerified, "The fresh host did not reoperate the required reusable theme fountain.");
        var final = main.ObservedSnapshot!.Players.Single();
        Require(final.LevelId == 0 && final.Life > 0, "The resumed four-floor journey did not return alive to Town.");
        WriteEvidence(directory, "nativedepthresume.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, EntityId = final.EntityId, ClassId = final.CharacterClassId,
            LevelId = final.LevelId, X = final.PositionX, Y = final.PositionY,
            Sequence = main.ObservedLastQueuedSequence, Visible = false, ExactColdBaseline = true, AllFloorsRetained = true,
            TrappedSarcophagusRetained = expected.TrappedSarcophagus is not null,
            ThemeObjectRetained = expected.ThemeObject is not null, ThemeObjectReusableVerified = nativeThemeObjectReuseVerified,
        });
        GD.Print("Native Warrior Town/L1-L4 fresh-host resume and retained revisit passed");
    }

    /**
     * Native single-player WM_DIABTWARPUP: L5's warp-up returns to Town (49,22) and opens
     * the Catacombs Town warp, which leads back to L5; the Warrior then returns by the warp-up.
     */
    private async Task<object> NativeWarriorTownWarpLegAsync(Main main, string directory)
    {
        using var timed = NativeWarriorStep("travel", "Town warp leg");
        Require(main.ObservedSnapshot!.Players.Single().LevelId == NativeWarriorTownWarpDepth && !NativeWarriorTownWarpOpen(main),
            "The Town warp leg must start on L5 with the Town warp closed.");
        await TravelNativeWarriorAsync(main, directory, 0);
        var arrival = main.ObservedSnapshot!.Players.Single();
        Require(NativeWarriorTownWarpOpen(main), "L5's warp-up did not open the Town warp.");
        await TravelNativeWarriorAsync(main, directory, NativeWarriorTownWarpDepth);
        await TravelNativeWarriorAsync(main, directory, 0);
        GD.Print($"Native Warrior opened the Town warp at ({arrival.PositionX},{arrival.PositionY}) and took it to L5 and back");
        return new { Opened = true, ArrivalX = arrival.PositionX, ArrivalY = arrival.PositionY };
    }

    private static bool NativeWarriorTownWarpOpen(Main main)
        => DepthGeometry(main, 0).WarpTriggers.Any(route => route.Available && route.TargetLevelId == NativeWarriorTownWarpDepth);

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

    /** Pepin's healing; a refusal while the Warrior is still entering Town changes nothing, so it is repeated. */
    private async Task HealNativeWarriorInTownAsync(Main main)
    {
        using var timed = NativeWarriorStep("town", "healer");
        for (var attempt = 1; ; attempt++) {
            try {
                await UseNativeTownHealer(main);
                return;
            } catch (InvalidOperationException failure) when (attempt < 4 && failure.Message.Contains("NotAllowed", StringComparison.Ordinal)) {
                var player = main.ObservedSnapshot!.Players.Single();
                GD.Print($"Native Warrior healer refused (attempt {attempt}): mode={player.CombatMode}; action={player.CurrentAction}; "
                    + $"at=({player.PositionX},{player.PositionY}); held={player.HeldItem is not null}; store={main.ObservedSnapshot.ActiveStore?.StoreId}");
                var tick = main.ObservedSnapshot.Tick;
                await Until(() => main.ObservedSnapshot!.Tick >= tick + 20 && Settled(main), "The refused healer visit did not settle.", 10);
            }
        }
    }

    /** A player's Town stop: Pepin heals, then his cheapest healing potion is bought up to the supply. */
    private async Task VisitNativeWarriorTownAsync(Main main)
    {
        using var timed = NativeWarriorStep("town", "healer and restock");
        await HealNativeWarriorInTownAsync(main);
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
        var origin = main.ObservedSnapshot!.Players.Single().LevelId;
        using var timed = NativeWarriorStep("travel", $"L{origin}->L{destination}");
        for (var attempt = 0; attempt < NativeWarriorTravelAttempts; attempt++) {
            // An interrupted walk can still finish on the trigger; let a pending transition land first.
            await WaitTicksWhile(main, () => !Settled(main) && main.ObservedSnapshot!.Players.Single().Life > 0, 10);
            var player = main.ObservedSnapshot!.Players.Single();
            if (player.LevelId == destination) {
                await Until(() => Settled(main) && (main.ObservedLastQueuedSequence == 0
                    || AcceptanceCommandBoundary.HasLatestAppliedReceipt(main.ObservedSnapshot!.Tick,
                        main.ObservedCommandResult, main.ObservedLastQueuedSequence)),
                    $"The final arrival command on L{destination} was not admitted.", 10);
                await UseNativeHealingPotionIfNeeded(main);
                return;
            }
            if (player.LevelId != origin) {
                // Fighting a pack stepped onto the floor's other stairs: walk back and go on.
                GD.Print($"Native Warrior stepped from L{origin} to L{player.LevelId} while clearing; returning");
                await TravelNativeWarriorAsync(main, directory, origin, flee);
                continue;
            }
            if (player.LevelId != 0 && !flee)
                await ClearNativeWarriorThreatsAsync(main, player.LevelId);
            await UseNativeHealingPotionIfNeeded(main);
            if (player.LevelId >= NativeWarriorTownWarpDepth)
                await OpenNativeWarriorCatacombsDoorsAsync(main, destination);
            await WaitTicksWhile(main, () => !Settled(main) && main.ObservedSnapshot!.Players.Single().Life > 0, 10);
            if (main.ObservedSnapshot!.Players.Single().LevelId != player.LevelId)
                continue; // Clearing finished an interrupted walk onto a trigger.
            // Journey and resume run in separate processes, so the phase keeps their route evidence apart.
            var routeDirectory = System.IO.Path.Combine(directory, "native-warrior-routes",
                System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE") ?? "phase",
                $"{++nativeWarriorRouteCount:D4}-{player.LevelId}-to-{destination}");
            System.IO.Directory.CreateDirectory(routeDirectory);
            var route = DepthGeometry(main, player.LevelId).WarpTriggers.Single(item => item.Available && item.TargetLevelId == destination);
            var position = main.ObservedSnapshot!.Players.Single();
            GD.Print($"Native Warrior route L{player.LevelId}->L{destination} attempt {attempt + 1} from ({position.PositionX},{position.PositionY}) "
                + $"to ({route.TriggerX},{route.TriggerY})");
            if (await AcceptanceRouteRecovery.TryAsync(async () => {
                // Walking continuity is the route tests' concern; the journey needs only the transition.
                await TravelCathedralRoute(main, routeDirectory, route.WarpId, destination, stopEarly: false,
                    minimumDistinctPositions: 1, distanceBoundedApproachDeadline: true,
                    interrupt: () => NativeHealingThresholdReached(main) && NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) > 0);
                Require(main.ObservedSnapshot!.Players.Single().LevelId == destination,
                    $"The ordinary route did not reach level {destination}.");
                // An interrupt can finish on the trigger; still perform the healing it requested.
                await UseNativeHealingPotionIfNeeded(main);
            }, () => ObserveNativeCommandBoundary(main), async failure => {
                GD.Print($"Native Warrior travel to L{destination} interrupted on L{main.ObservedSnapshot!.Players.Single().LevelId} "
                    + $"({failure.Message}); clearing and retrying; "
                    + DescribeNativeWarriorSurroundings(main));
                await StopNativeTravelAsync(main);
                // Repeated hits can hold the Warrior out of Stand; the retry's clearing fights them.
                await WaitTicksWhile(main, () => !Settled(main) && main.ObservedSnapshot!.Players.Single().Life > 0, 10);
                // Give the interrupted walk's last queued command time to return its receipt.
                await WaitTicksWhile(main, () => main.ObservedCommandResult?.ClientSequence != main.ObservedLastQueuedSequence, 2);
            })) return;
        }
        throw new InvalidOperationException($"The Warrior could not reach level {destination} within {NativeWarriorTravelAttempts} attempts.");
    }

    /** Already-stopped travel is stopped; reveal and hover can also finish a prior cancellation. */
    private async Task StopNativeTravelAsync(Main main)
    {
        var control = (Button)main.FindChild("CancelTravel", true, false);
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await AcceptanceControlClick.WhenEnabled(() => !control.Disabled, async () => {
            await RevealControl(sections, 4, control);
            var position = control.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
            await Frame();
        }, async () => {
            var position = control.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseButton {
                ButtonIndex = MouseButton.Left, Position = position, GlobalPosition = position, Pressed = true,
            });
            try { await Frame(); }
            finally {
                Input.ParseInputEvent(new InputEventMouseButton {
                    ButtonIndex = MouseButton.Left, Position = position, GlobalPosition = position, Pressed = false,
                });
            }
            await Frame();
        });
        await Until(() => control.Disabled, "Interrupted native Warrior travel did not stop.", 15);
    }

    /**
     * Catacombs doors (native 42/43) close off rooms such as L5's warp-up; until the route's
     * trigger is reachable, the Warrior opens the closed door it reaches nearest the trigger.
     */
    private async Task OpenNativeWarriorCatacombsDoorsAsync(Main main, uint destination)
    {
        var refusals = new Dictionary<uint, int>();
        for (var opened = 0; opened < NativeWarriorTravelAttempts; opened++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var geometry = DepthGeometryFromSnapshot(snapshot, player.LevelId);
            // A walk interrupted on its trigger can arrive while clearing.
            if (player.LevelId == destination
                || geometry.WarpTriggers.SingleOrDefault(item => item.Available && item.TargetLevelId == destination) is not { } route)
                return;
            var reach = NativeWarriorReach(geometry, player, route.TriggerX, route.TriggerY);
            if (reach.Contains((route.TriggerX, route.TriggerY)))
                return;
            var door = snapshot.Objects
                .Where(item => item.LevelId == player.LevelId && item.NativeObjectId is 42 or 43 && !item.MissilePassable
                    && refusals.GetValueOrDefault(item.EntityId) < 3
                    && reach.Any(cell => Math.Abs(cell.X - item.PositionX) + Math.Abs(cell.Y - item.PositionY) == 1))
                .OrderBy(item => NativeDistance(item.PositionX, item.PositionY, route.TriggerX, route.TriggerY))
                .ThenBy(item => item.EntityId).FirstOrDefault();
            // The route planner decides what remains; a door is opened only while one stands in reach.
            if (door is null)
                return;
            try {
                // Clearing can fail too when monsters crowd the door; both retry with the next plan.
                await ClearNativeWarriorThreatsAsync(main, player.LevelId);
                await MoveAdjacentToNativePosition(main, current => current.Objects.Single(item => item.EntityId == door.EntityId),
                    NativeWarriorDoorObstruction(main, player.LevelId));
            } catch (InvalidOperationException failure) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == player.LevelId) {
                // A monster stepped into the walk or kept hitting the Warrior; fight it and plan again.
                GD.Print($"Native Warrior door approach interrupted ({failure.Message[..Math.Min(failure.Message.Length, 120)]}); "
                    + DescribeNativeWarriorSurroundings(main) + $"; mode={alive.CombatMode}; action={alive.CurrentAction}; "
                    + $"walkPending={alive.NativeWalkPathPending}; pending={main.ObservedPendingCommandCount}; "
                    + $"queued={main.ObservedLastQueuedSequence}; result={main.ObservedCommandResult?.ClientSequence}/"
                    + $"{main.ObservedCommandResult?.Status}/{main.ObservedCommandResult?.RejectReason}");
                await SettleInterruptedNativeDoorApproachAsync(main);
                await UseNativeHealingPotionIfNeeded(main);
                continue;
            }
            try {
                await OperateNativeWarriorCatacombsDoorAsync(main, door);
            } catch (InvalidOperationException refused) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == player.LevelId) {
                // A hit or a monster in the doorway can refuse the operate; retry it, then skip it.
                refusals[door.EntityId] = refusals.GetValueOrDefault(door.EntityId) + 1;
                GD.Print($"Native Warrior door {door.EntityId} refused ({refused.Message[..Math.Min(refused.Message.Length, 120)]}); "
                    + DescribeNativeWarriorSurroundings(main));
                await Until(() => Settled(main) || main.ObservedSnapshot!.Players.Single().Life <= 0,
                    "The refused native Catacombs door operate did not settle.", 15);
                continue;
            }
            GD.Print($"Native Warrior opened Catacombs door {door.EntityId} at ({door.PositionX},{door.PositionY}) toward L{destination}");
        }
        throw new InvalidOperationException($"The Warrior opened {NativeWarriorTravelAttempts} Catacombs doors without reaching the L{destination} route.");
    }

    /**
     * An approach can fail on a snapshot that frames have not refreshed, so a retry needs a frame
     * and the interrupted walk's end; without them every retry fails on the same snapshot.
     */
    private async Task SettleInterruptedNativeDoorApproachAsync(Main main)
    {
        await Frame();
        await WaitTicksWhile(main, () => !NativeWalkBoundarySettled(main) && main.ObservedSnapshot!.Players.Single().Life > 0, 2);
    }

    /**
     * A door walk is blocked by monsters on its path: fight those nearby, then the nearest one, and
     * stop after a bounded number of tries rather than waiting on a monster that never moves.
     */
    private Func<bool, Task<bool>> NativeWarriorDoorObstruction(Main main, uint levelId)
    {
        var tries = 0;
        return async blocked => {
            if (!blocked)
                return false;
            Require(++tries <= NativeWarriorDoorObstructionTries,
                $"The Warrior's L{levelId} door walk stayed blocked; " + DescribeNativeWarriorSurroundings(main));
            var startTick = main.ObservedSnapshot!.Tick;
            await UseNativeHealingPotionIfNeeded(main);
            await ClearNativeWarriorThreatsAsync(main, levelId);
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var nearest = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion && monster.LevelId == levelId
                    && !monster.SneakHidden)
                .OrderBy(monster => NativeDistance(player.PositionX, player.PositionY, monster.PositionX, monster.PositionY))
                .ThenBy(monster => monster.EntityId).FirstOrDefault();
            if (nearest is null)
                return false;
            try {
                await AttackNativeWarriorTargetAsync(main, levelId, nearest.EntityId, NativeWarriorAttacksPerFloor / 4);
            } catch (NativeWarriorRetreatException) {
                await UseNativeHealingPotionIfNeeded(main);
            }
            // Nothing could act: let the world run until the blocking monsters move or reach the Warrior.
            var waitLife = main.ObservedSnapshot!.Players.Single().Life;
            var waitTick = main.ObservedSnapshot!.Tick;
            for (var frame = 0; frame < 240 && main.ObservedSnapshot!.Tick < Math.Max(startTick + 40, waitTick + 10); frame++) {
                var current = main.ObservedSnapshot!;
                var warrior = current.Players.Single();
                if (warrior.Life < waitLife || current.Tick >= waitTick + 10 && current.Monsters.Any(monster => monster.Alive && !monster.IsPlayerMinion
                        && monster.LevelId == levelId
                        && NativeDistance(warrior.PositionX, warrior.PositionY, monster.PositionX, monster.PositionY) <= 1))
                    break;
                await Frame();
            }
            return true;
        };
    }

    private async Task<NativeCatacombsDoorTrapObservation.Opened> OperateNativeWarriorCatacombsDoorAsync(Main main, ObjectSnapshot door)
    {
        await PrepareNativeObjectAsync(main, "Before operating a native Catacombs door");
        var baseline = new NativeCatacombsDoorTrapObservation.Opening(main.ObservedSnapshot!, door.EntityId);
        if (nativeCatacombsRequiredDoor?.Matches(door) == true) {
            nativeCatacombsRequiredDoorEntity = door.EntityId;
            nativeCatacombsRequiredDoorFiring = null;
        }
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickWorld(main, door.PositionX, door.PositionY);
        await AcceptedObjectOperationAndSettled(main, sequence, door.EntityId, "Operate a native Catacombs door");
        var operatedTick = main.ObservedCommandResult!.AppliedTick;
        var opening = new NativeCatacombsDoorTrapObservation.Opened(baseline, operatedTick,
            checked(operatedTick + 5UL * (main.ObservedIdentity?.TickRateHz ?? 20)));
        await Until(() => main.ObservedSnapshot!.Objects.Single(item => item.EntityId == door.EntityId).MissilePassable,
            $"Native Catacombs door {door.EntityId} did not open.", 5);
        await Until(() => main.ObservedNativeObjectDrawing.Any(item => item.EntityId == door.EntityId && item.NativeArt),
            $"Opened native Catacombs door {door.EntityId} was not drawn with native art.", 5);
        nativeWarriorCatacombsDoorDraws++;
        await Until(() => main.ObservedSnapshot!.Tick > operatedTick,
            "The opened Catacombs door did not receive its native object tick.", 5);
        if (door.TrapArmed) {
            await Until(() => !main.ObservedSnapshot!.Objects.Single(item => item.EntityId == door.EntityId).TrapArmed,
                "The opened native Catacombs door did not consume its linked trap.");
        }
        if (opening.Find(main.ObservedSnapshot!) is { } firing) RecordNativeCatacombsDoorFiring(firing);
        return opening;
    }

    /**
     * L5-L8 kill search: opens the closed Catacombs door nearest the Warrior within its walking reach,
     * preferring doors without a pack behind them (several attackers stun-lock a lone Warrior, as native
     * hit recovery does). An approach a monster interrupts is retried a few times before that door is given up.
     */
    private async Task<bool> OpenNearestNativeWarriorCatacombsDoorAsync(Main main, uint levelId, HashSet<uint> operated,
        Dictionary<uint, int> approaches)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var reach = NativeWarriorReach(DepthGeometryFromSnapshot(snapshot, levelId), player, -1, -1);
        var door = snapshot.Objects
            .Where(item => item.LevelId == levelId && item.NativeObjectId is 42 or 43 && !item.MissilePassable
                && !operated.Contains(item.EntityId)
                && reach.Any(cell => Math.Abs(cell.X - item.PositionX) + Math.Abs(cell.Y - item.PositionY) == 1))
            .OrderBy(item => snapshot.Monsters.Count(monster => monster.Alive && !monster.IsPlayerMinion
                    && monster.LevelId == levelId
                    && NativeDistance(item.PositionX, item.PositionY, monster.PositionX, monster.PositionY) <= NativeWarriorPackRadius)
                >= NativeWarriorPackSize ? 1 : 0)
            .ThenBy(item => NativeDistance(player.PositionX, player.PositionY, item.PositionX, item.PositionY))
            .ThenBy(item => item.EntityId).FirstOrDefault();
        if (door is null)
            return false;
        var attempts = approaches[door.EntityId] = approaches.GetValueOrDefault(door.EntityId) + 1;
        if (attempts >= NativeWarriorDoorApproachAttempts)
            operated.Add(door.EntityId);
        try {
            await MoveAdjacentToNativePosition(main, current => current.Objects.Single(item => item.EntityId == door.EntityId),
                NativeWarriorDoorObstruction(main, levelId));
        } catch (InvalidOperationException failure) {
            GD.Print($"Native Warrior L{levelId} door approach interrupted ({failure.Message[..Math.Min(failure.Message.Length, 120)]}); "
                + DescribeNativeWarriorSurroundings(main) + $"; mode={main.ObservedSnapshot!.Players.Single().CombatMode}; "
                + $"pending={main.ObservedPendingCommandCount}; queued={main.ObservedLastQueuedSequence}; "
                + $"result={main.ObservedCommandResult?.ClientSequence}/{main.ObservedCommandResult?.Status}");
            if (main.ObservedSnapshot!.Players.Single() is not { Life: > 0 } alive || alive.LevelId != levelId)
                throw;
            // Monsters pouring through an opened door block the walk; the kill loop fights them (and may retreat).
            await SettleInterruptedNativeDoorApproachAsync(main);
            await UseNativeHealingPotionIfNeeded(main);
            return true;
        }
        operated.Add(door.EntityId);
        try {
            await OperateNativeWarriorCatacombsDoorAsync(main, door);
        } catch (InvalidOperationException failure) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
            && alive.LevelId == levelId) {
            // The server refuses some door operations (for example an occupied doorway); try another door.
            GD.Print($"Native Warrior L{levelId} door {door.EntityId} refused ({failure.Message[..Math.Min(failure.Message.Length, 120)]}); "
                + DescribeNativeWarriorSurroundings(main));
            return true;
        }
        GD.Print($"Native Warrior opened L{levelId} Catacombs door {door.EntityId} to reach a monster");
        return true;
    }

    /** Cardinal walking reach over the projected blocked cells; the goal trigger counts as reached. */
    private static HashSet<(int X, int Y)> NativeWarriorReach(LevelGeometrySnapshot geometry, PlayerSnapshot player, int goalX, int goalY)
    {
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        var reach = new HashSet<(int X, int Y)> { (player.PositionX, player.PositionY) };
        var queue = new Queue<(int X, int Y)>(reach);
        while (queue.TryDequeue(out var cell)) {
            if (cell == (goalX, goalY))
                continue;
            foreach (var offset in CardinalOffsets) {
                var next = (X: cell.X + offset.X, Y: cell.Y + offset.Y);
                if ((uint)next.X >= geometry.Width || (uint)next.Y >= geometry.Height
                    || blocked.Contains(next.Y * (int)geometry.Width + next.X)
                    || next != (goalX, goalY) && geometry.WarpTriggers.Any(trigger => (trigger.TriggerX, trigger.TriggerY) == next)
                    || !reach.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }
        return reach;
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
            // Fighting can step onto stairs; the caller's travel loop takes the Warrior back.
            if (player.LevelId != levelId)
                return;
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
            } catch (InvalidOperationException) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId != levelId) {
                return;
            } catch (NativeWarriorRetreatException) {
                // Walking on through a pack only takes more hits: drink and fight on while potions last.
                if (NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) == 0)
                    return;
                await UseNativeHealingPotionIfNeeded(main);
                continue;
            }
            if (killed is null)
                skipped.Add(threat.EntityId);
        }
    }

    /** One generated kill that earns XP on the current floor. */
    private async Task<NativeWarriorFloor> DefeatNativeWarriorFloorAsync(Main main, string directory, uint levelId)
    {
        using var timed = NativeWarriorStep("fight", $"L{levelId}");
        var retreats = 0;
        var experienceBefore = main.ObservedSnapshot!.Players.Single().Experience;
        var attacks = 0;
        var doors = new HashSet<uint>();
        var doorEvidence = new List<NativeDoorOperationEvidence>();
        var doorCommands = new Dictionary<uint, int>();
        var work = new NativeCombatWorkBudget();
        while (attacks < NativeWarriorAttacksPerFloor) {
            if (NativeHealingSupply(main.ObservedSnapshot!.Players.Single()) > NativeWarriorPotionReserveOn(levelId))
                await UseNativeHealingPotionIfNeeded(main);
            MonsterSnapshot target;
            try {
                target = await FindNativeClassTargetAsync(main, ranged: false, levelId, NativeWarriorSearchMoves);
            } catch (InvalidOperationException) when (main.ObservedSnapshot!.Players.Single().Life > 0
                && doors.Count < NativeWarriorDoorOperations) {
                // Every nearby monster is behind a closed door: open the nearest reachable one and search again.
                if (levelId >= NativeWarriorTownWarpDepth) {
                    await ClearNativeWarriorThreatsAsync(main, levelId);
                    await UseNativeHealingPotionIfNeeded(main);
                    if (!await OpenNearestNativeWarriorCatacombsDoorAsync(main, levelId, doors, doorCommands))
                        throw;
                    continue;
                }
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
                (killed, used) = await AttackNativeWarriorTargetAsync(main, levelId, target.EntityId, NativeWarriorAttacksPerFloor - attacks,
                    NativeWarriorPotionReserveOn(levelId));
            } catch (NativeWarriorRetreatException retreat) when (retreats < NativeWarriorRetreats) {
                retreats++;
                GD.Print($"Native Warrior retreating from L{levelId} ({retreat.Message})");
                try {
                    await TravelNativeWarriorAsync(main, directory, levelId - 1, flee: true);
                } catch (InvalidOperationException blocked) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                    && alive.LevelId == levelId) {
                    // The pack holds the way up; stand and fight instead.
                    GD.Print($"Native Warrior retreat from L{levelId} blocked ({blocked.Message[..Math.Min(blocked.Message.Length, 120)]})");
                    continue;
                }
                for (var up = (int)levelId - 2; up >= 0; up--)
                    await TravelNativeWarriorAsync(main, directory, (uint)up);
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
     * only `reserve` potions left (kept undrunk for the walk out), or below 70% surrounded by three
     * or more attackers, it throws NativeWarriorRetreatException. Returns the
     * first attacked monster that dies, or null when the target escapes, is unreachable, or the
     * budget ends.
     */
    private async Task<(uint? Killed, int Attacks)> AttackNativeWarriorTargetAsync(Main main, uint levelId, uint targetId, int budget,
        int reserve = 0)
    {
        var attacks = 0;
        var approaches = 0;
        var attacked = new HashSet<uint>();
        // Monsters whose click queued nothing or was refused, for example diagonally past a door corner.
        var unattackable = new HashSet<uint>();
        var swings = new List<string>();
        // Approaches are bounded so a fading, retreating or unreachable monster cannot hold the loop.
        while (attacks < budget && approaches < NativeWarriorTargetApproaches) {
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
            var adjacent = adjacentMonsters.Where(monster => !unattackable.Contains(monster.EntityId))
                .OrderBy(monster => monster.HitPointsQ6)
                .ThenBy(monster => monster.EntityId == targetId ? 0 : 1).ThenBy(monster => monster.EntityId).FirstOrDefault();
            if (adjacentMonsters.Length >= 1 && (long)player.Life * 5 < (long)player.LifeMaximum * 3
                    && NativeHealingSupply(player) <= reserve
                || adjacentMonsters.Length >= 3 && (long)player.Life * 10 < (long)player.LifeMaximum * 7)
                throw new NativeWarriorRetreatException(
                    $"{adjacentMonsters.Length} attackers, life {player.Life}/{player.LifeMaximum}, potions {NativeHealingSupply(player)}");
            // Every pass drinks, including those that only approach or skip a monster that cannot be struck;
            // the reserve is kept for the retreat walk, which the 60% rule above starts.
            if (NativeHealingSupply(player) > reserve)
                await UseNativeHealingPotionIfNeeded(main);
            if (adjacent is null) {
                var target = snapshot.Monsters.SingleOrDefault(monster => monster.EntityId == targetId && monster.Alive);
                if (target is null)
                    return (null, attacks);
                approaches++;
                MonsterSnapshot approached;
                try {
                    approached = await FindNativeClassTargetAsync(main, ranged: false, levelId, requiredTarget: targetId);
                } catch (InvalidOperationException) when (main.ObservedSnapshot!.Players.Single().Life > 0) {
                    return (null, attacks); // Not reachable from here (for example behind a wall).
                }
                if (approached.EntityId != targetId
                    && NativeDistance(player.PositionX, player.PositionY, approached.PositionX, approached.PositionY) > 1)
                    return (null, attacks);
                continue;
            }
            attacks++;
            attacked.Add(adjacent.EntityId);
            var sequence = main.ObservedLastQueuedSequence;
            await ClickWorld(main, adjacent.PositionX, adjacent.PositionY, MouseButton.Right);
            // A monster that stepped off the clicked tile yields no attack command; re-observe and retry.
            for (var frame = 0; frame < 3 && main.ObservedLastQueuedSequence == sequence; frame++)
                await Frame();
            if (main.ObservedLastQueuedSequence == sequence) {
                unattackable.Add(adjacent.EntityId);
                continue;
            }
            const string receiptFailure = "Native Warrior melee did not reach its authoritative receipt.";
            var clickTick = main.ObservedSnapshot!.Tick;
            var clickClock = System.Diagnostics.Stopwatch.StartNew();
            try {
                await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                    && main.ObservedSnapshot!.Tick >= result.AppliedTick, receiptFailure, 15);
            } catch (InvalidOperationException exception) when (exception.Message == receiptFailure) {
                var last = main.ObservedCommandResult;
                throw new InvalidOperationException($"{receiptFailure} Clicked at tick {clickTick}; now tick {main.ObservedSnapshot!.Tick} "
                    + $"after {clickClock.Elapsed.TotalSeconds:0.0}s at {main.ObservedIdentity?.TickRateHz} Hz; queued {main.ObservedLastQueuedSequence} "
                    + $"pending {main.ObservedPendingCommandCount}; next command tick {main.ObservedSuggestedCommandTick} (round trip {main.ObservedRoundTripMilliseconds:0} ms); last receipt "
                    + $"{last?.ClientSequence}/{last?.AppliedTick}/{last?.Status}/{last?.RejectReason}.", exception);
            }
            await Until(() => Settled(main) || main.ObservedSnapshot!.Players.Single().Life <= 0,
                "Native Warrior melee did not reach its action boundary.", 30);
            var receipt = main.ObservedCommandResult!;
            var after = main.ObservedSnapshot!;
            swings.Add($"{adjacent.EntityId}:hp{adjacent.HitPoints}->{after.Monsters.SingleOrDefault(monster => monster.EntityId == adjacent.EntityId)?.HitPoints}"
                + $":{receipt.Status}/{receipt.RejectReason}:life{after.Players.Single().Life}");
            // A live monster can move or recover before the clocked attack applies; re-observe and retry.
            if (Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt)
                && receipt.AppliedTick >= snapshot.Tick)
                nativeWarriorTiming?.Reaction(receipt.AppliedTick - snapshot.Tick);
            // A clocked swing that reaches the host after its tick changes nothing, and a resend
            // of it returns the same refusal as a duplicate; swing again.
            if (receipt.RejectReason == CommandRejectReason.TooLate)
                continue;
            if (!Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt)) {
                Require(receipt.Status == CommandStatus.Rejected
                    && receipt.RejectReason is CommandRejectReason.NotAllowed or CommandRejectReason.InvalidTarget,
                    $"Unexpected native Warrior attack refusal: {receipt.Status}/{receipt.RejectReason}.");
                unattackable.Add(adjacent.EntityId);
            }
        }
        return (null, attacks);
    }

    private static void VerifyNativeWarriorRetention(Main main, NativeWarriorDepthCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        NativeCatacombsChestStage.VerifyAvailability((uint)expected.Floors.Length,
            System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_SEED"), expected.Objects?.ChestEntity > 0);
        NativeDepthResumeEvidence.Verify(NativeWarriorRequiresShrine, expected.Shrine is not null,
            NativeWarriorRequiresTrappedDoor, expected.Objects?.DoorEntity ?? 0,
            expected.Objects?.DoorTrapSource ?? 0, expected.Objects?.DoorTrapTick ?? 0);
        NativeDepthResumeEvidence.VerifyThemeObject(NativeWarriorRequiresThemeObject, expected.ThemeObject is not null);
        var sarcophagus = expected.TrappedSarcophagus;
        NativeDepthResumeEvidence.VerifyTrappedSarcophagus(NativeWarriorRequiresTrappedSarcophagus, sarcophagus is not null,
            sarcophagus?.Level ?? 0, sarcophagus?.Entity ?? 0, sarcophagus?.TrapSource ?? 0,
            sarcophagus?.OpeningTick ?? 0, sarcophagus?.FiringTick ?? 0,
            sarcophagus is not null && (sarcophagus.Drops?.Length ?? 0) + (sarcophagus.Skeletons?.Length ?? 0) > 0);
        if (sarcophagus is not null && LevelScopedSnapshotEvidence.IncludesLevel(snapshot, sarcophagus.Level))
            NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(snapshot, sarcophagus);
        if (expected.ThemeObject is not null) VerifyNativeThemeObjectRetention(snapshot, expected.ThemeObject);
        if (expected.Objects is not null)
            VerifyNativeCatacombsObjects(snapshot, expected.Objects);
        if (expected.Shrine is not null) VerifyNativeShrineRetention(snapshot, expected.Shrine);
        Require(expected.Floors.Length is >= 1 and <= (int)NativeWarriorDeepestFloor
            && expected.Floors.Select(floor => floor.LevelId).SequenceEqual(Enumerable.Range(1, expected.Floors.Length).Select(level => (uint)level)),
            "The depth checkpoint omitted or reordered a floor.");
        foreach (var floor in expected.Floors) {
            Require(HashProto(DepthGeometry(main, floor.LevelId)) == floor.GeometryHash,
                $"Retained native floor {floor.LevelId} changed its generated geometry or door state.");
            if (floor.LevelId > NativeWarriorFoughtFloor || floor.LevelId < NativeWarriorFirstFightFloor) {
                Require(floor.MonsterEntityId == 0, $"Traversed native floor {floor.LevelId} recorded a kill.");
                continue;
            }
            Require(floor.ExperienceAfter > floor.ExperienceBefore, $"Retained L{floor.LevelId} omitted its combat XP proof.");
            if (!LevelScopedSnapshotEvidence.IncludesLevel(snapshot, floor.LevelId)) continue;
            Require(snapshot.Corpses.Count(corpse => corpse.EntityId == floor.MonsterEntityId && corpse.LevelId == floor.LevelId) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == floor.MonsterEntityId && monster.Alive),
                $"Retained native floor {floor.LevelId} revived or lost its combat corpse {floor.MonsterEntityId}.");
        }
    }
}
