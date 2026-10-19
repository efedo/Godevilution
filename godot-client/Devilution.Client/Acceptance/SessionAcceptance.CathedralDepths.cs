using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** Ordinary-control depth traversal and cold restore; explicit seeds and authored population only. */
public partial class SessionAcceptance
{
    private async Task VerifyCathedralDepthsAsync(Main main, string directory, string phase, bool visible)
    {
        var profilePath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_DEPTH_PROFILE");
        Require(!string.IsNullOrWhiteSpace(profilePath), "Depth acceptance requires its separate compiled profile.");
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath!));
        Require(profile.RootElement.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-depths.v1",
            "The supplied profile is not the separate depth adapter.");
        VerifyDepthGeometry(main, profile.RootElement);
        var authoredDiagnostic = IsAuthoredDiagnosticDepthProfile(profile.RootElement);
        var townPortalRoute = IsTownPortalPreviewDepthProfile(profile.RootElement);
        Require(!townPortalRoute || authoredDiagnostic,
            "The Town Portal route requires the authored diagnostic Cathedral content.");
        if (phase == "CathedralDepthJourney") {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 0, "Depth journey must begin in Town through the new-character form.");
            TownPortalPurchaseEvidence? portalPurchase = null;
            if (authoredDiagnostic) {
                if (townPortalRoute) {
                    Require(main.ObservedSnapshot!.Players.Single().Gold == 100,
                        "The Town Portal preview route must begin with the ordinary 100-gold balance.");
                    portalPurchase = await VerifyTownPortalAcquisition(main,
                        distanceBoundedApproachDeadline: true, verifyCancellation: false);
                }
                await TravelDepthRoute(main, directory, 1, profile.RootElement);
                await ActivateAuthoredDepthObject(main, profile.RootElement);
                await VerifyCombatAndLoot(main, directory, visible, generated: true, distanceBoundedPickupDeadline: true);
                var encounterId = ProfileDepthUInt(profile.RootElement, 1, "encounterEntityId");
                using (var loot = JsonDocument.Parse(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(directory, "cathedral-combat-loot.json")))) {
                    Require(loot.RootElement.GetProperty("MonsterId").GetUInt32() == encounterId,
                        "The authored Cathedral encounter did not defeat its profile-selected monster.");
                }
                var sword = await EquipAuthoredDepthSword(main);
                Require(sword.State.Durability == sword.State.MaxDurability,
                    "The earned authored short sword was not at full durability for the Smith availability check.");
                await TravelDepthRoute(main, directory, 2, profile.RootElement);
                await MoveDepthNeighbor(main);
            } else {
                for (uint destination = 1; destination <= 4; destination++) {
                    await TravelDepthRoute(main, directory, destination, profile.RootElement);
                    if (visible)
                        await Capture(System.IO.Path.Combine(directory, $"cathedral-depth-{destination}.png"));
                }
                var fourth = DepthGeometry(main, 4);
                var nativeDown = profile.RootElement.GetProperty("levels")[3].GetProperty("downTrigger");
                Require(!fourth.WarpTriggers.Any(route => route.TargetLevelId == 5
                    || (route.TriggerX == nativeDown.GetProperty("x").GetInt32() && route.TriggerY == nativeDown.GetProperty("y").GetInt32())),
                    "The unimplemented native L4-to-Catacombs-5 boundary was replaced with a fabricated exit.");
                await TravelDepthRoute(main, directory, 3, profile.RootElement);
                await TravelDepthRoute(main, directory, 2, profile.RootElement);
                await EstablishDepthWallCheckpoint(main);
                await VerifyDepthWallCollision(main, directory);
                await MoveDepthNeighbor(main);
            }
            if (authoredDiagnostic) {
                WriteEvidence(directory, "cathedral-depth-state.json", new {
                    Passed = true, GeometryHash = DepthGeometryHash(main),
                    SavedDepth = 2,
                    AuthoredDiagnostic = CaptureAuthoredDepthCheckpoint(main, profile.RootElement, portalPurchase),
                });
            } else {
                WriteEvidence(directory, "cathedral-depth-state.json", new {
                    Passed = true, GeometryHash = DepthGeometryHash(main),
                    BoundaryDepth = 4, UnsupportedDestinationDepth = 5,
                });
            }
        } else {
            Require(main.ObservedSnapshot!.Players.Single().LevelId == 2, "Fresh-host restore did not return to the middle Cathedral floor.");
            using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedral-depth-state.json")));
            Require(saved.RootElement.GetProperty("Passed").GetBoolean()
                && saved.RootElement.GetProperty("GeometryHash").GetString() == DepthGeometryHash(main),
                "Fresh host changed the native depth geometry, transparency, blockers or resolved routes.");
            if (authoredDiagnostic) {
                var expected = saved.RootElement.GetProperty("AuthoredDiagnostic").Deserialize<AuthoredDepthCheckpoint>()
                    ?? throw new InvalidDataException("The authored Cathedral checkpoint is missing.");
                VerifyAuthoredDepthCheckpoint(main, profile.RootElement, expected, townPortalRoute);
            }
            if (visible)
                await Capture(System.IO.Path.Combine(directory, "cathedral-depth-middle-restored.png"));
            var previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "cathedraldepthjourney.json")))!;
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await MoveDepthNeighbor(main);
            var firstResumeMove = main.ObservedCommandResult!;
            if (authoredDiagnostic) {
                Require(firstResumeMove.Status == CommandStatus.Accepted
                    && firstResumeMove.ClientSequence == checked(previous.Sequence + 1)
                    && firstResumeMove.ClientSequence > sequence,
                    "The first fresh-host movement was not the next accepted command after the saved journey.");
            } else {
                Require(firstResumeMove.ClientSequence > previous.Sequence && firstResumeMove.ClientSequence > sequence,
                    "Fresh depth client did not advance the saved command sequence.");
            }
            await TravelDepthRoute(main, directory, 1, profile.RootElement);
            if (authoredDiagnostic) {
                VerifyCombatLootRestored(main, directory, "cathedral", allowEquippedLoot: true);
                var expected = saved.RootElement.GetProperty("AuthoredDiagnostic").Deserialize<AuthoredDepthCheckpoint>()!;
                VerifyAuthoredDepthRetention(main, profile.RootElement, expected);
                if (townPortalRoute) {
                    var routeEvidence = expected.TownPortalRoute
                        ?? throw new InvalidDataException("The Town Portal purchase identity is missing from the saved checkpoint.");
                    Require(routeEvidence.ScrollPreservedAtResume == false && !routeEvidence.ScrollConsumedAfterCast,
                        "The Town Portal evidence was already marked complete before the fresh-host route ran.");
                    var purchase = new TownPortalPurchaseEvidence(routeEvidence.ScrollStableItemId,
                        routeEvidence.ScrollSeed, routeEvidence.ScrollPrice);
                    var resumedPlayer = main.ObservedSnapshot!.Players.Single();
                    Require(CountAuthoredItemOccurrences(main, resumedPlayer, routeEvidence.ScrollStableItemId) == 1
                        && resumedPlayer.Inventory.Any(item => item.State.StableItemId == routeEvidence.ScrollStableItemId),
                        "The purchased Town Portal source was not present exactly once in resumed inventory.");
                    routeEvidence = routeEvidence with { ScrollPreservedAtResume = true };
                    var sections = (TabContainer)main.FindChild("HudSections", true, false);
                    var portal = await CastAndUseTownPortal(main, sections, purchase, visible);
                    AssertPortalState(main.ObservedSnapshot!, main.ObservedSnapshot!.Players.Single(), portal, requireTownReturn: true);
                    var portalPlayer = main.ObservedSnapshot!.Players.Single();
                    var scrollConsumed = CountAuthoredItemOccurrences(main, portalPlayer, routeEvidence.ScrollStableItemId) == 0;
                    Require(scrollConsumed, "The Town Portal scroll remained in a player or world container after casting.");
                    routeEvidence = routeEvidence with {
                        ScrollConsumedAfterCast = true,
                        PortalId = portal.PortalId,
                        PortalSourceLevelId = portal.PortalSourceLevelId,
                        PortalDestinationLevelId = portal.PortalDestinationLevelId,
                        PortalDestinationPositionX = portal.PortalDestinationPositionX,
                        PortalDestinationPositionY = portal.PortalDestinationPositionY,
                        ReturnedToTown = portalPlayer.LevelId == portal.PortalDestinationLevelId,
                    };
                    Require(routeEvidence.ReturnedToTown && portalPlayer.LevelId == 0,
                        "The Town Portal did not return the resumed character to Town.");
                    var stash = await VerifyAuthoredSwordStashRoundTrip(main, expected);
                    routeEvidence = routeEvidence with {
                        SwordStableItemId = stash.StableItemId,
                        SwordStashCell = stash.StashCell,
                        SwordStateSha256 = stash.StateSha256,
                        SwordRoundTripComplete = stash.Completed,
                        GoldAfterRoundTrip = stash.GoldAfter,
                        StashGoldBeforeRoundTrip = stash.StashGoldBefore,
                        StashGoldAfterRoundTrip = stash.StashGoldAfter,
                    };
                    await VerifyAuthoredSmithRefusal(main, directory, expected, routeEvidence);
                } else {
                    await TravelDepthRoute(main, directory, 0, profile.RootElement);
                    await VerifyAuthoredSmithRefusal(main, directory, expected);
                }
            } else {
                await TravelDepthRoute(main, directory, 0, profile.RootElement);
            }
        }
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var final = main.ObservedSnapshot!.Players.Single();
        var identity = main.ObservedIdentity!;
        WriteEvidence(directory, phase.ToLowerInvariant() + ".json", new Evidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            final.EntityId, final.CharacterClassId, final.LevelId, final.PositionX, final.PositionY,
            main.ObservedCommandResult!.ClientSequence, visible));
        GD.Print($"Godot Cathedral depths acceptance passed: {phase}; visible={visible}");
    }

    private static bool IsAuthoredDiagnosticDepthProfile(JsonElement profile)
        => profile.TryGetProperty("placementPolicy", out var placementPolicy)
            && placementPolicy.ValueKind == JsonValueKind.String
            && placementPolicy.GetString() == "authored-diagnostic"
            && profile.TryGetProperty("diagnosticDropItemSymbol", out var dropItemSymbol)
            && dropItemSymbol.ValueKind == JsonValueKind.String
            && dropItemSymbol.GetString() == "short_sword_standard";

    private static bool IsTownPortalPreviewDepthProfile(JsonElement profile)
    {
        if (!profile.TryGetProperty("townPortalRoute", out var route) || route.ValueKind == JsonValueKind.Null)
            return false;
        Require(route.ValueKind == JsonValueKind.String && route.GetString() == "town-portal-preview",
            "The compiled Cathedral profile contains an unsupported Town Portal route marker.");
        return true;
    }

    private async Task ActivateAuthoredDepthObject(Main main, JsonElement profile)
    {
        var objectId = ProfileDepthUInt(profile, 1, "objectEntityId");
        var (objectX, objectY) = ProfileDepthPoint(profile, 1, "objectPosition");
        var target = main.ObservedSnapshot!.Objects.Single(item => item.EntityId == objectId);
        Require(target.LevelId == 1 && (target.PositionX, target.PositionY) == (objectX, objectY) && !target.Activated,
            "The profile-selected L1 object was missing, moved or already activated.");
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickWorld(main, objectX, objectY);
        await Until(() => main.ObservedSnapshot!.Objects.Any(item => item.EntityId == objectId && item.Activated)
            && Settled(main) && main.ObservedCommandResult is { } result && result.ClientSequence > sequence
            && IsAcceptedOrSuccessfulDuplicate(result),
            "The profile-selected Cathedral object did not activate through ordinary controls.", 15);
    }

    private async Task<EquippedItemSnapshot> EquipAuthoredDepthSword(Main main)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var player = main.ObservedSnapshot!.Players.Single();
        var sword = player.Inventory.Single(item => item.ItemSeed == 6001).Clone();
        Require(player.Equipment.Any(item => item.Slot == 4), "The authored Rogue is missing its starting bow.");
        await ClickListItem(sections, 1, equipment, 4);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(player, player.Equipment.Single(item => item.Slot == 4).State));
        await AcceptedAndSettled(main, sequence, "Unequip starting bow for authored sword");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.All(item => item.Slot != 4),
            "The starting bow did not leave the authored Rogue's equipment slot.", 5);

        await SelectInventoryItem(main, sections, sword.State.StableItemId);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, 4);
        await AcceptedAndSettled(main, sequence, "Equip authored Cathedral short sword");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item => item.Slot == 4
            && item.State.StableItemId == sword.State.StableItemId), "The earned Cathedral sword was not equipped.", 5);
        var equipped = main.ObservedSnapshot!.Players.Single().Equipment.Single(item => item.Slot == 4).Clone();
        Require(main.ObservedSnapshot.Players.Single().Inventory.All(item => item.State.StableItemId != sword.State.StableItemId)
            && equipped.ItemSeed == 6001 && equipped.State.Durability == equipped.State.MaxDurability,
            "The authored short sword was duplicated or changed while equipping it.");
        return equipped;
    }

    private AuthoredDepthCheckpoint CaptureAuthoredDepthCheckpoint(
        Main main,
        JsonElement profile,
        TownPortalPurchaseEvidence? portalPurchase = null)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.LevelId == 2 && Settled(main) && main.ObservedPendingCommandCount == 0,
            "The authored off-level checkpoint must be settled on Cathedral depth 2.");
        var sword = player.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == 6001);
        var identity = main.ObservedIdentity!;
        AuthoredTownPortalRouteEvidence? townPortalRoute = null;
        if (portalPurchase is not null) {
            Require(portalPurchase.StableItemId != 0 && portalPurchase.ItemSeed == 9100
                && portalPurchase.Price == 50 && player.Gold == 50,
                "The Town Portal purchase did not preserve its expected identity or 50-gold balance at checkpoint.");
            townPortalRoute = new AuthoredTownPortalRouteEvidence(
                portalPurchase.StableItemId, portalPurchase.ItemSeed, 39, portalPurchase.Price, player.Gold);
        }
        return new AuthoredDepthCheckpoint(
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            player.EntityId, player.CharacterName, player.CharacterClassId, player.CharacterLevel,
            player.LevelId, player.PositionX, player.PositionY, player.Life, player.LifeMaximum,
            player.BaseLifeMaximum, player.Mana, player.ManaMaximum, player.BaseManaMaximum,
            player.Gold, player.StashGold, player.Experience,
            HashProtoCollection(player.Inventory.OrderBy(item => item.ItemSeed).ThenBy(item => item.State.StableItemId)
                .Select(item => item.ToByteArray())),
            HashProto(player.Attributes), HashProtoCollection(player.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
            HashProtoCollection(player.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())),
            sword.ItemSeed, sword.State.StableItemId, sword.State.Durability, HashProto(sword.State),
            ProfileDepthUInt(profile, 1, "encounterEntityId"), ProfileDepthUInt(profile, 1, "objectEntityId"),
            1_000_009, 6001, TownPortalRoute: townPortalRoute);
    }

    private static void VerifyAuthoredDepthCheckpoint(
        Main main,
        JsonElement profile,
        AuthoredDepthCheckpoint expected,
        bool townPortalRoute)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var identity = main.ObservedIdentity!;
        Require(Settled(main) && main.ObservedPendingCommandCount == 0
            && identity.GameMode.ToString() == expected.GameMode
            && identity.ContentManifestHash == expected.ContentManifestHash
            && identity.RulesetIdentityHash == expected.RulesetIdentityHash
            && player.EntityId == expected.EntityId && player.CharacterName == expected.CharacterName
            && player.CharacterClassId == expected.CharacterClassId && player.CharacterLevel == expected.CharacterLevel
            && player.LevelId == expected.LevelId && player.PositionX == expected.PositionX && player.PositionY == expected.PositionY
            && player.Life == expected.Life && player.LifeMaximum == expected.LifeMaximum
            && player.BaseLifeMaximum == expected.BaseLifeMaximum
            && player.Mana == expected.Mana && player.ManaMaximum == expected.ManaMaximum
            && player.BaseManaMaximum == expected.BaseManaMaximum
            && player.Gold == expected.Gold && player.StashGold == expected.StashGold
            && player.Experience == expected.Experience
            && HashProtoCollection(player.Inventory.OrderBy(item => item.ItemSeed).ThenBy(item => item.State.StableItemId)
                .Select(item => item.ToByteArray())) == expected.InventorySha256
            && HashProto(player.Attributes) == expected.AttributesSha256
            && HashProtoCollection(player.Belt.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.BeltSha256
            && HashProtoCollection(player.Equipment.OrderBy(item => item.Slot).Select(item => item.ToByteArray())) == expected.EquipmentSha256,
            "Fresh-host resume changed the authored actor identity, position, resources, attributes, belt or equipment baseline.");
        Require(expected.EncounterEntityId == ProfileDepthUInt(profile, 1, "encounterEntityId")
            && expected.ObjectEntityId == ProfileDepthUInt(profile, 1, "objectEntityId")
            && expected.DropEntityId == 1_000_009 && expected.DropSeed == 6001,
            "The authored Cathedral checkpoint no longer matches its compiled profile identities.");
        Require((expected.TownPortalRoute is not null) == townPortalRoute,
            "The saved Town Portal purchase does not match the compiled route marker.");
        if (townPortalRoute) {
            var route = expected.TownPortalRoute!;
            var scroll = player.Inventory.SingleOrDefault(item => item.State.StableItemId == route.ScrollStableItemId);
            Require(route.ScrollStableItemId != 0 && route.ScrollSeed == 9100 && route.ScrollSpellId == 39
                && route.ScrollPrice == 50 && route.GoldAfterPurchase == 50 && expected.Gold == 50
                && route.PortalId == 0 && !route.ScrollPreservedAtResume && !route.ScrollConsumedAfterCast
                && !route.ReturnedToTown && !route.SwordRoundTripComplete && !route.Passed
                && scroll is not null && scroll.ItemSeed == route.ScrollSeed
                && scroll.State.SpellId == route.ScrollSpellId
                && scroll.State.SpellSourceKind == ItemSpellSourceKind.ConsumableItem
                && CountAuthoredItemOccurrences(main, player, route.ScrollStableItemId) == 1,
                "Fresh-host resume did not preserve the purchased Town Portal source and 50-gold balance.");
        }
        var sword = player.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == expected.SwordSeed);
        Require(sword.State.StableItemId == expected.SwordStableItemId
            && sword.State.Durability == expected.SwordDurability
            && HashProto(sword.State) == expected.SwordStateSha256,
            "Fresh-host resume changed the equipped authored sword state.");
    }

    private static void VerifyAuthoredDepthRetention(Main main, JsonElement profile, AuthoredDepthCheckpoint expected)
    {
        var snapshot = main.ObservedSnapshot!;
        var encounterId = ProfileDepthUInt(profile, 1, "encounterEntityId");
        var objectId = ProfileDepthUInt(profile, 1, "objectEntityId");
        Require(encounterId == expected.EncounterEntityId,
            "The retained encounter no longer matches the compiled Cathedral profile.");
        Require(!snapshot.Monsters.Any(monster => monster.EntityId == encounterId && monster.Alive),
            "The off-level defeated encounter became alive after the cold restore.");
        Require(snapshot.Corpses.Count(corpse => corpse.EntityId == encounterId) == 1
            && snapshot.Corpses.Single(corpse => corpse.EntityId == encounterId).LevelId == 1,
            "The off-level defeated encounter corpse was missing or duplicated after the cold restore.");
        var retainedObjects = snapshot.Objects.Where(item => item.EntityId == objectId).ToArray();
        Require(retainedObjects.Length == 1 && retainedObjects[0] is { LevelId: 1, Activated: true },
            "The activated off-level Cathedral object did not survive the cold restore.");
        Require(!snapshot.WorldItems.Any(item => item.EntityId == expected.DropEntityId),
            "The claimed Cathedral loot reappeared after the cold restore.");
        var player = snapshot.Players.Single();
        var sword = player.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == expected.SwordSeed);
        Require(sword.State.StableItemId == expected.SwordStableItemId
            && sword.State.Durability == expected.SwordDurability
            && HashProto(sword.State) == expected.SwordStateSha256,
            "The earned short sword did not remain equipped after the fresh host returned to L1.");
    }

    private async Task VerifyAuthoredSmithRefusal(
        Main main,
        string directory,
        AuthoredDepthCheckpoint expected,
        AuthoredTownPortalRouteEvidence? townPortalRoute = null)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await ApproachAuthoredTownForSmithWithTrace(main, directory);
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 2, "OpenSmith");
        await AcceptedAndSettled(main, sequence, "Open Smith after Cathedral return");
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 1,
            "The Smith store did not open after returning to Town.", 5);

        var player = main.ObservedSnapshot!.Players.Single();
        var sword = player.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == expected.SwordSeed).Clone();
        Require(sword.State.StableItemId == expected.SwordStableItemId
            && sword.State.Durability == sword.State.MaxDurability
            && player.Gold == expected.Gold,
            "The authored sword or gold changed before the Town service check.");
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var selectionSequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, 4);
        var hud = main.GetChildren().OfType<HudPresentation>().Single();
        var selected = hud.SelectedItemReference?.Clone();
        Require(selected is {
            Location: PlayerItemLocation.Equipment,
            Slot: 4,
        } && selected.StableItemId == expected.SwordStableItemId,
            "The actual HUD did not select the earned sword as the Smith service target.");
        var repairButton = AuthoredDescendantButtons(sections.GetChild(2)).Single(button => button.Text.StartsWith("Repair", StringComparison.Ordinal));
        await RevealControl(sections, 2, repairButton);
        await Until(() => FindAuthoredRepairQuote(main, selected) is not null && repairButton.IsVisibleInTree(),
            "The Smith did not publish a repair quote for the selected equipped sword.", 10);
        var quoteRows = main.ObservedSnapshot!.ActiveStore!.Quotes!.Items
            .Where(row => SameAuthoredItemReference(row.Item, selected)).ToArray();
        Require(quoteRows.Length == 1 && quoteRows[0].Repair is not null,
            "The Smith repair quote is missing or ambiguous for the selected sword reference.");
        var quote = quoteRows[0].Repair!;
        var observedPlayer = main.ObservedSnapshot.Players.Single();
        var observedSword = observedPlayer.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == expected.SwordSeed);
        Require(quote.RejectReason == CommandRejectReason.NotAllowed && quote.Price == 0
            && repairButton.Disabled && observedPlayer.Gold == expected.Gold
            && HashProto(observedSword.State) == expected.SwordStateSha256
            && main.ObservedPendingCommandCount == 0
            && main.ObservedCommandResult!.ClientSequence == selectionSequence
            && main.ObservedLastQueuedSequence == selectionSequence,
            "The full-durability Smith refusal changed state, exposed a payable action or queued a command.");
        var repairButtonDisabled = repairButton.Disabled;

        sequence = main.ObservedCommandResult.ClientSequence;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, sequence, "Close Smith after service inspection");
        var townPlayer = main.ObservedSnapshot!.Players.Single();
        Require(townPlayer.LevelId == 0 && townPlayer.ActiveStoreId == 0 && main.ObservedSnapshot.ActiveStore is null
            && townPlayer.Gold == expected.Gold
            && HashProto(townPlayer.Equipment.Single(item => item.Slot == 4).State) == expected.SwordStateSha256,
            "Closing the Smith changed the retained sword/gold or did not leave the character in Town.");
        using var saved = JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "cathedral-depth-state.json")));
        var checkpoint = saved.RootElement.GetProperty("AuthoredDiagnostic").Deserialize<AuthoredDepthCheckpoint>()!;
        if (townPortalRoute is not null) {
            Require(townPortalRoute.ScrollPreservedAtResume && townPortalRoute.ScrollConsumedAfterCast
                && townPortalRoute.ReturnedToTown && townPortalRoute.PortalId != 0
                && townPortalRoute.PortalSourceLevelId == 1 && townPortalRoute.PortalDestinationLevelId == 0
                && townPortalRoute.SwordRoundTripComplete
                && townPortalRoute.SwordStableItemId == expected.SwordStableItemId
                && townPortalRoute.SwordStateSha256 == expected.SwordStateSha256
                && townPortalRoute.GoldAfterRoundTrip == expected.Gold
                && townPortalRoute.StashGoldBeforeRoundTrip == expected.StashGold
                && townPortalRoute.StashGoldAfterRoundTrip == expected.StashGold,
                "The Town Portal route evidence is incomplete or changed the sword/gold stash baseline.");
            townPortalRoute = townPortalRoute with { Passed = true };
        }
        WriteEvidence(directory, "cathedral-depth-resume-state.json", new {
            Passed = true,
            GeometryHash = DepthGeometryHash(main),
            SavedDepth = 2,
            AuthoredDiagnostic = checkpoint with {
                TownPortalRoute = townPortalRoute,
                SmithStoreId = 1,
                RepairQuoteReason = quote.RejectReason.ToString(),
                RepairQuotePrice = quote.Price,
                RepairButtonDisabled = repairButtonDisabled,
                RepairCommandQueued = false,
                GoldAfterSmith = townPlayer.Gold,
                SwordDurabilityAfterSmith = townPlayer.Equipment.Single(item => item.Slot == 4).State.Durability,
                ReturnedToTown = townPlayer.LevelId == 0,
            },
        });
    }

    private async Task<AuthoredSwordStashEvidence> VerifyAuthoredSwordStashRoundTrip(
        Main main,
        AuthoredDepthCheckpoint expected)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var player = main.ObservedSnapshot!.Players.Single();
        var equipped = player.Equipment.Single(item => item.Slot == 4 && item.ItemSeed == expected.SwordSeed).Clone();
        var goldBefore = player.Gold;
        var stashGoldBefore = player.StashGold;
        Require(player.LevelId == 0 && !player.StashOpen && player.ActiveStoreId == 0
            && equipped.State.StableItemId == expected.SwordStableItemId
            && HashProto(equipped.State) == expected.SwordStateSha256
            && goldBefore == expected.Gold && stashGoldBefore == expected.StashGold,
            "The returned Town baseline changed before the sword stash round trip.");

        await ClickListItem(sections, 1, equipment, 4);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        var inventoryCell = FindInventorySpace(player, equipped.State);
        await ClickGridCell(main, sections, 1, "InventoryGrid", inventoryCell);
        await AcceptedAndSettled(main, sequence, "Unequip earned sword for stash round trip");
        await Until(() => main.ObservedSnapshot!.Players.Single().Inventory.Any(item =>
            item.State.StableItemId == equipped.State.StableItemId)
            && main.ObservedSnapshot.Players.Single().Equipment.All(item => item.State.StableItemId != equipped.State.StableItemId),
            "The earned sword did not move from equipment to inventory.", 5);

        await ApproachDeclaredDestination(main, "towner:7", distanceBoundedApproachDeadline: true);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "OpenStash");
        await AcceptedAndSettled(main, sequence, "Open Town stash for earned sword");
        await Until(() => main.ObservedSnapshot!.Players.Single().StashOpen,
            "The Town stash did not open for the earned sword.", 5);

        player = main.ObservedSnapshot!.Players.Single();
        var stashGrid = (GridContainer)main.FindChild("StashGrid", true, false);
        var stashCell = Enumerable.Range(0, stashGrid.GetChildCount()).FirstOrDefault(cell =>
            player.Stash.All(item => item.Page != 0 || item.Cell != cell), -1);
        Require(stashCell >= 0, "No empty first-page stash cell is available for the earned sword.");
        var carriedSword = player.Inventory.Single(item => item.State.StableItemId == equipped.State.StableItemId);
        Require(HashProto(carriedSword.State) == expected.SwordStateSha256
            && player.Gold == goldBefore && player.StashGold == stashGoldBefore,
            "The sword or gold changed before depositing it into the stash.");
        await StashInventoryItem(main, sections, equipped.State.StableItemId, stashCell);
        await Until(() => main.ObservedSnapshot!.Players.Single().Stash.Any(item => item.Page == 0
            && item.Cell == stashCell && item.Item.State.StableItemId == equipped.State.StableItemId),
            "The earned sword did not reach its selected stash cell.", 5);
        player = main.ObservedSnapshot!.Players.Single();
        var stashed = player.Stash.Single(item => item.Page == 0 && item.Cell == stashCell);
        Require(stashed.Item.State.StableItemId == expected.SwordStableItemId
            && HashProto(stashed.Item.State) == expected.SwordStateSha256
            && player.Inventory.All(item => item.State.StableItemId != expected.SwordStableItemId)
            && CountAuthoredItemOccurrences(main, player, expected.SwordStableItemId) == 1
            && player.Gold == goldBefore && player.StashGold == stashGoldBefore,
            "Stashing the earned sword changed its exact state, duplicated it or changed either gold balance.");

        await ClickGridCell(main, sections, 3, "StashGrid", stashCell);
        var selectionTick = main.ObservedSnapshot!.Tick;
        await Until(() => main.ObservedSnapshot!.Tick > selectionTick + 2 && Settled(main)
            && main.ObservedPendingCommandCount == 0,
            "The selected earned sword did not remain stable in the open stash.", 5);
        sequence = main.ObservedCommandResult!.ClientSequence;
        var withdrawnCell = FindInventorySpace(main.ObservedSnapshot!.Players.Single(), stashed.Item.State);
        await ClickGridCell(main, sections, 1, "InventoryGrid", withdrawnCell);
        await AcceptedAndSettled(main, sequence, "Withdraw earned sword from stash");
        await Until(() => main.ObservedSnapshot!.Players.Single().Inventory.Any(item =>
            item.State.StableItemId == expected.SwordStableItemId)
            && main.ObservedSnapshot.Players.Single().Stash.All(item => item.Item.State.StableItemId != expected.SwordStableItemId),
            "The selected earned sword did not return from stash to inventory.", 5);
        var withdrawn = main.ObservedSnapshot!.Players.Single().Inventory.Single(item =>
            item.State.StableItemId == expected.SwordStableItemId);
        Require(HashProto(withdrawn.State) == expected.SwordStateSha256,
            "Withdrawing the earned sword changed its authoritative item state.");

        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "CloseStash");
        await AcceptedAndSettled(main, sequence, "Close stash after sword withdrawal");
        await SelectInventoryItem(main, sections, expected.SwordStableItemId);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, 4);
        await AcceptedAndSettled(main, sequence, "Re-equip earned sword after stash round trip");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item =>
            item.Slot == 4 && item.State.StableItemId == expected.SwordStableItemId),
            "The earned sword did not return to its original equipment slot.", 5);
        player = main.ObservedSnapshot!.Players.Single();
        var restored = player.Equipment.Single(item => item.Slot == 4);
        Require(HashProto(restored.State) == expected.SwordStateSha256
            && CountAuthoredItemOccurrences(main, player, expected.SwordStableItemId) == 1
            && player.LevelId == 0 && !player.StashOpen && player.ActiveStoreId == 0
            && player.Gold == goldBefore && player.StashGold == stashGoldBefore,
            "The sword was not re-equipped exactly once or the Town/stash gold baseline changed.");
        return new AuthoredSwordStashEvidence(expected.SwordStableItemId, stashCell,
            HashProto(restored.State), true, player.Gold, stashGoldBefore, player.StashGold);
    }

    private static int CountAuthoredItemOccurrences(Main main, PlayerSnapshot player, uint stableItemId)
    {
        bool Matches(ItemStateSnapshot? state) => state is not null && state.StableItemId == stableItemId;
        return player.Inventory.Count(item => Matches(item.State))
            + player.Belt.Count(item => Matches(item.State))
            + player.Equipment.Count(item => Matches(item.State))
            + player.Stash.Count(item => item.Item is not null && Matches(item.Item.State))
            + (player.HeldItem is not null && Matches(player.HeldItem.State) ? 1 : 0)
            + main.ObservedSnapshot!.WorldItems.Count(item => Matches(item.State));
    }

    private async Task ApproachAuthoredTownForSmithWithTrace(Main main, string directory)
    {
        const int traceLimit = 8192;
        var trace = new List<object>(traceLimit);
        var framesObserved = 0;
        var traceTruncated = false;
        Exception? failure = null;
        object? terminalSample = null;
        string? lastObservedState = null;
        object? projectedDestination = null;
        var deadlineSeconds = 80;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        object Capture(string observation, bool force = false)
        {
            var snapshot = main.ObservedSnapshot;
            var player = snapshot?.Players.SingleOrDefault();
            var result = main.ObservedCommandResult;
            var approach = main.FindChild("ApproachDestination", true, false) as Button;
            var cancelTravel = main.FindChild("CancelTravel", true, false) as Button;
            var state = new {
                Tick = snapshot?.Tick,
                LevelId = player?.LevelId,
                PositionX = player?.PositionX,
                PositionY = player?.PositionY,
                Mode = player?.CombatMode.ToString(),
                Action = player?.CurrentAction?.ToString(),
                Pending = main.ObservedPendingCommandCount,
                LastQueuedSequence = main.ObservedLastQueuedSequence,
                ResultSequence = result?.ClientSequence,
                ResultAppliedTick = result?.AppliedTick,
                ResultStatus = result?.Status.ToString(),
                ResultRejectReason = result?.RejectReason.ToString(),
                ApproachVisible = approach?.IsVisibleInTree(),
                ApproachDisabled = approach?.Disabled,
                CancelTravelVisible = cancelTravel?.IsVisibleInTree(),
                CancelTravelDisabled = cancelTravel?.Disabled,
            };
            var sample = new { Observation = observation, Frame = framesObserved, State = state };
            var stateKey = JsonSerializer.Serialize(state);
            if (force || !StringComparer.Ordinal.Equals(stateKey, lastObservedState)) {
                if (trace.Count < traceLimit) {
                    trace.Add(sample);
                    lastObservedState = stateKey;
                } else {
                    traceTruncated = true;
                }
            }
            return sample;
        }

        try {
            var destinations = AuthoritativeWorldDestinationSelector.GetDestinations(main.ObservedSnapshot).ToArray();
            var destinationIndex = Array.FindIndex(destinations, destination => destination.Key == "towner:0");
            Require(destinationIndex >= 0, "The server did not declare destination towner:0.");
            var destination = destinations[destinationIndex];
            var initialPlayer = main.ObservedSnapshot!.Players.Single();
            deadlineSeconds = DestinationApproachDeadlineSeconds(initialPlayer.PositionX, initialPlayer.PositionY,
                destination.X, destination.Y, distanceBounded: true);
            projectedDestination = new {
                destination.Key,
                destination.LevelId,
                destination.X,
                destination.Y,
                destination.IsPortal,
            };
            Capture("before-approach");
            var approachTask = ApproachDeclaredDestination(main, "towner:0", distanceBoundedApproachDeadline: true);
            while (!approachTask.IsCompleted) {
                await Frame();
                framesObserved++;
                Capture("frame");
            }
            await approachTask;
            terminalSample = Capture("completed", force: true);
        } catch (Exception exception) {
            failure = exception;
            terminalSample = Capture("failure", force: true);
            throw;
        } finally {
            terminalSample ??= Capture(failure is null ? "completed" : "failure", force: true);
            elapsed.Stop();
            WriteEvidence(directory, "cathedral-smith-approach-trace.json", new {
                Passed = failure is null,
                DeadlineSeconds = deadlineSeconds,
                ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
                ProjectedDestination = projectedDestination,
                FramesObserved = framesObserved,
                TraceLimit = traceLimit,
                TraceTruncated = traceTruncated,
                TerminalSample = terminalSample,
                FailureType = failure?.GetType().FullName,
                FailureMessage = failure?.Message,
                Trace = trace,
            });
        }
    }

    private static StoreServicePriceQuote? FindAuthoredRepairQuote(Main main, PlayerItemReference? selected)
    {
        if (selected is null || main.ObservedSnapshot?.ActiveStore?.Quotes is not { } quotes)
            return null;
        var rows = quotes.Items.Where(row => SameAuthoredItemReference(row.Item, selected)).ToArray();
        return rows.Length == 1 ? rows[0].Repair : null;
    }

    private static bool SameAuthoredItemReference(PlayerItemReference? left, PlayerItemReference? right)
        => left is not null && right is not null && left.Location == right.Location
            && left.Slot == right.Slot && left.StableItemId == right.StableItemId && left.Page == right.Page;

    private static IEnumerable<Button> AuthoredDescendantButtons(Node node)
    {
        foreach (var child in node.GetChildren()) {
            if (child is Button button)
                yield return button;
            foreach (var descendant in AuthoredDescendantButtons(child))
                yield return descendant;
        }
    }

    private static uint ProfileDepthUInt(JsonElement profile, uint depth, string name)
        => profile.GetProperty("levels")[(int)depth - 1].GetProperty(name).GetUInt32();

    private static string HashProto(IMessage message)
        => Convert.ToHexString(SHA256.HashData(message.ToByteArray()));

    private static string HashProtoCollection(IEnumerable<byte[]> values)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            values.Select(Convert.ToBase64String).ToArray())));

    private sealed record AuthoredDepthCheckpoint(
        string GameMode, string ContentManifestHash, string RulesetIdentityHash,
        uint EntityId, string CharacterName, uint CharacterClassId, uint CharacterLevel,
        uint LevelId, int PositionX, int PositionY, int Life, int LifeMaximum, int BaseLifeMaximum,
        int Mana, int ManaMaximum, int BaseManaMaximum,
        uint Gold, uint StashGold, uint Experience,
        string InventorySha256, string AttributesSha256, string BeltSha256, string EquipmentSha256,
        uint SwordSeed, uint SwordStableItemId, int SwordDurability, string SwordStateSha256,
        uint EncounterEntityId, uint ObjectEntityId, uint DropEntityId, uint DropSeed,
        AuthoredTownPortalRouteEvidence? TownPortalRoute = null,
        uint SmithStoreId = 0, string RepairQuoteReason = "", uint RepairQuotePrice = 0,
        bool RepairButtonDisabled = false, bool RepairCommandQueued = false,
        uint GoldAfterSmith = 0, int SwordDurabilityAfterSmith = 0, bool ReturnedToTown = false);

    private sealed record AuthoredTownPortalRouteEvidence(
        uint ScrollStableItemId,
        uint ScrollSeed,
        uint ScrollSpellId,
        uint ScrollPrice,
        uint GoldAfterPurchase,
        bool ScrollPreservedAtResume = false,
        bool ScrollConsumedAfterCast = false,
        uint PortalId = 0,
        uint PortalSourceLevelId = 0,
        uint PortalDestinationLevelId = 0,
        int PortalDestinationPositionX = 0,
        int PortalDestinationPositionY = 0,
        bool ReturnedToTown = false,
        uint SwordStableItemId = 0,
        int SwordStashCell = -1,
        string SwordStateSha256 = "",
        bool SwordRoundTripComplete = false,
        uint GoldAfterRoundTrip = 0,
        uint StashGoldBeforeRoundTrip = 0,
        uint StashGoldAfterRoundTrip = 0,
        bool Passed = false);

    private sealed record AuthoredSwordStashEvidence(
        uint StableItemId,
        int StashCell,
        string StateSha256,
        bool Completed,
        uint GoldAfter,
        uint StashGoldBefore,
        uint StashGoldAfter);

    private async Task TravelDepthRoute(Main main, string directory, uint destination, JsonElement profile)
    {
        var source = main.ObservedSnapshot!.Players.Single().LevelId;
        var route = DepthGeometry(main, source).WarpTriggers.Single(candidate => candidate.TargetLevelId == destination);
        var origin = main.ObservedSnapshot.Players.Single();
        // Native stairs can be only one or two steps from MAIN. Retain the old
        // eight-position proof for longer routes without inventing a detour.
        var minimumPositions = Math.Clamp(Math.Max(Math.Abs(origin.PositionX - route.TriggerX),
            Math.Abs(origin.PositionY - route.TriggerY)), 1, 8);
        await TravelCathedralRoute(main, directory, route.WarpId, destination, stopEarly: false,
            minimumDistinctPositions: minimumPositions);
        var player = main.ObservedSnapshot!.Players.Single();
        var expected = destination == 0 ? (25, 31) : ProfileDepthPoint(profile, destination,
            destination > source ? "mainEntry" : "previousEntry");
        Require((player.LevelId, player.PositionX, player.PositionY) == (destination, expected.Item1, expected.Item2),
            "Native depth travel reached the wrong entry anchor.");
    }

    private static void VerifyDepthGeometry(Main main, JsonElement profile)
    {
        var levels = profile.GetProperty("levels");
        Require(levels.GetArrayLength() == 4, "The depth profile must describe four native floors.");
        for (uint depth = 1; depth <= 4; depth++) {
            var geometry = DepthGeometry(main, depth);
            Require(geometry.Width == 112 && geometry.Height == 112
                && geometry.NativePieceLayer.Count == 112 * 112 && geometry.NativeTransparency.Count == 112 * 112
                && geometry.NativeMegaTileLayer.Count == 0 && geometry.BlockedCells.Count > 0
                && geometry.NativePieceLayer.Count(piece => piece == 128) == 1
                && geometry.NativePieceLayer.Count(piece => piece == 114) == 1,
                $"Depth {depth} lacks complete native fine geometry and unique stairs.");
            var up = ProfileDepthPoint(profile, depth, "upTrigger");
            var down = ProfileDepthPoint(profile, depth, "downTrigger");
            Require(geometry.NativePieceLayer[up.Item2 * 112 + up.Item1] == 128
                && geometry.NativePieceLayer[down.Item2 * 112 + down.Item1] == 114,
                $"Depth {depth} stair anchors differ from its published profile.");
            Require(geometry.WarpTriggers.Count == (depth == 4 ? 1 : 2), "The depth chain contains extra or missing routes.");
            var backward = geometry.WarpTriggers.Single(route => route.TargetLevelId == depth - 1);
            Require((backward.TriggerX, backward.TriggerY) == up && backward.Available,
                "Up stairs do not bind to the preceding native floor.");
            if (depth < 4) {
                var forward = geometry.WarpTriggers.Single(route => route.TargetLevelId == depth + 1);
                Require((forward.TriggerX, forward.TriggerY) == down && forward.Available,
                    "Down stairs do not bind to the next native floor.");
            }
        }
    }

    private async Task EstablishDepthWallCheckpoint(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.LevelId == 2, "The collision checkpoint must be on the middle native floor.");
        var geometry = DepthGeometry(main, 2);
        var targets = Enumerable.Range(0, geometry.Width * geometry.Height)
            .Select(index => (X: index % geometry.Width, Y: index / geometry.Width))
            .Where(point => Math.Abs(point.X - player.PositionX) + Math.Abs(point.Y - player.PositionY) <= 8
                && IsDepthWalkTarget(main, point.X, point.Y)
                && CardinalOffsets.Any(offset => IsDepthWall(geometry, point.X + offset.X, point.Y + offset.Y)))
            .OrderBy(point => Math.Abs(point.X - player.PositionX) + Math.Abs(point.Y - player.PositionY)).ToArray();
        Require(targets.Length > 0, "No nearby non-trigger native wall checkpoint exists.");
        var target = targets[0];
        for (var step = 0; step < 32; step++) {
            var before = main.ObservedSnapshot!.Players.Single().Clone();
            if (before.PositionX == target.X && before.PositionY == target.Y)
                return;
            var sequence = main.ObservedCommandResult!.ClientSequence;
            await Click(main.ObservedTileScreenPosition(target.X, target.Y), walk: true);
            await AcceptedAndSettled(main, sequence, "Ordinary depth collision-checkpoint movement");
            var after = main.ObservedSnapshot!.Players.Single();
            Require(after.LevelId == 2 && (after.PositionX, after.PositionY) != (before.PositionX, before.PositionY),
                "Server-owned approach did not progress toward the middle-floor wall checkpoint.");
        }
        throw new InvalidOperationException("The bounded ordinary-input wall approach did not reach its checkpoint.");
    }

    private async Task VerifyDepthWallCollision(Main main, string directory)
    {
        var before = main.ObservedSnapshot!.Players.Single().Clone();
        var geometry = DepthGeometry(main, before.LevelId);
        var wall = CardinalOffsets.First(offset => IsDepthWall(geometry, before.PositionX + offset.X, before.PositionY + offset.Y));
        var key = wall switch { (0, 1) => Key.Down, (0, -1) => Key.Up, (1, 0) => Key.Right, _ => Key.Left };
        GetViewport().GuiReleaseFocus();
        var oldSequence = main.ObservedLastQueuedSequence;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        var sequence = main.ObservedLastQueuedSequence;
        Require(sequence > oldSequence, "The native depth wall key did not queue fresh ordinary input.");
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == sequence
            && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main), "Native depth wall input did not settle.", 10);
        var receipt = main.ObservedCommandResult!;
        var after = main.ObservedSnapshot!.Players.Single();
        var passed = receipt.Status == CommandStatus.Rejected && receipt.RejectReason == CommandRejectReason.InvalidTarget
            && (after.LevelId, after.PositionX, after.PositionY) == (before.LevelId, before.PositionX, before.PositionY);
        WriteEvidence(directory, "cathedral-depth-collision.json", new {
            Passed = passed, before.LevelId, before.PositionX, before.PositionY,
            WallX = before.PositionX + wall.X, WallY = before.PositionY + wall.Y,
            receipt.ClientSequence, Status = receipt.Status.ToString(), Rejection = receipt.RejectReason.ToString(),
        });
        Require(passed, "The exact native wall input was not rejected without moving the middle-floor character.");
    }

    private async Task MoveDepthNeighbor(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single().Clone();
        var offset = CardinalOffsets.FirstOrDefault(offset => IsDepthWalkTarget(main, player.PositionX + offset.X, player.PositionY + offset.Y));
        Require(offset != (0, 0), "No ordinary non-trigger adjacent native depth cell is free.");
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await Click(main.ObservedTileScreenPosition(player.PositionX + offset.X, player.PositionY + offset.Y), walk: true);
        await AcceptedAndSettled(main, sequence, "Native depth movement after collision or restore");
        var after = main.ObservedSnapshot!.Players.Single();
        Require((after.LevelId, after.PositionX, after.PositionY) == (player.LevelId, player.PositionX + offset.X, player.PositionY + offset.Y),
            "Ordinary native depth movement did not reach its selected adjacent cell.");
    }

    private static bool IsDepthWalkTarget(Main main, int x, int y)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = DepthGeometry(main, player.LevelId);
        return x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height
            && !geometry.BlockedCells.Contains(y * geometry.Width + x)
            && !geometry.WarpTriggers.Any(route => route.TriggerX == x && route.TriggerY == y)
            && !snapshot.Objects.Any(item => item.LevelId == player.LevelId && !item.Destroyed && item.PositionX == x && item.PositionY == y)
            && !snapshot.Monsters.Any(item => item.LevelId == player.LevelId && item.Alive && item.PositionX == x && item.PositionY == y);
    }

    private static bool IsDepthWall(LevelGeometrySnapshot geometry, int x, int y)
        => x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height && geometry.BlockedCells.Contains(y * geometry.Width + x);

    private static LevelGeometrySnapshot DepthGeometry(Main main, uint levelId)
        => main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == levelId);

    private static (int, int) ProfileDepthPoint(JsonElement profile, uint depth, string name)
    {
        var point = profile.GetProperty("levels")[(int)depth - 1].GetProperty(name);
        return (point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32());
    }

    private static string DepthGeometryHash(Main main)
        => Convert.ToHexString(SHA256.HashData(main.ObservedSnapshot!.LevelGeometry
            .Where(level => level.LevelId is >= 1 and <= 4).OrderBy(level => level.LevelId)
            .SelectMany(level => level.ToByteArray()).ToArray()));
}
