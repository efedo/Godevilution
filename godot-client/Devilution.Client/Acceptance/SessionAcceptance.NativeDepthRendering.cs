using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

/** Per-floor native draw checks for the Warrior depth journey, with PNG captures when windowed. */
public partial class SessionAcceptance
{
    private sealed record NativeWarriorFloorDraw(uint LevelId, string? Location, int FloorDraws, int ContentDraws,
        int ActorDraws, bool NativePlayer, int NativeResidents, int NativeObjects, string? Capture);

    private int nativeWarriorCaptureCount;

    private static bool NativeWarriorVisible => DisplayServer.GetName() != "headless";

    private void StartNativeWarriorDrawCapture(Main main)
    {
        StartNativePlayerArtCapture(main);
        StartNativeSceneArtCapture(main);
        StartNativeTownerCapture(main);
        StartNativeObjectCapture(main);
    }

    /**
     * Waits until the current floor draws native scene layers and the native player, then saves
     * a capture when the client has a window. Residents visible from there are counted.
     */
    private async Task<NativeWarriorFloorDraw> VerifyNativeWarriorFloorDrawAsync(Main main, string directory, uint levelId)
    {
        var playerArt = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_PLAYER_PACK"));
        await Until(() => Settled(main) && main.ObservedSnapshot!.Players.Single().LevelId == levelId
            && main.ObservedNativeSceneDrawing is { NativeSceneActive: true } draw && draw.LevelId == levelId
            && draw.FloorRasterDrawCount > 0 && draw.ContentRasterDrawCount > 0 && draw.ActorDrawCount > 0
            && draw.MissingAtlasPaths.Count == 0
            && (!playerArt || main.ObservedNativePlayerDrawing is { IsLoaded: true, UsedFallback: false }),
            $"L{levelId} did not draw its native scene layers and player.", 15);
        var scene = main.ObservedNativeSceneDrawing!;
        var residents = main.ObservedNativeTownerDrawing.Count(resident => resident.NativeArt);
        string? capture = null;
        if (NativeWarriorVisible) {
            capture = $"native-depth-{++nativeWarriorCaptureCount:D2}-L{levelId}.png";
            await Capture(System.IO.Path.Combine(directory, capture));
        }
        var result = new NativeWarriorFloorDraw(levelId, scene.Location, scene.FloorRasterDrawCount,
            scene.ContentRasterDrawCount, scene.ActorDrawCount, main.ObservedNativePlayerDrawing is { UsedFallback: false },
            residents,
            main.ObservedNativeObjectDrawing.Count(item => item.NativeArt && item.LevelId == levelId), capture);
        GD.Print($"Native Warrior L{levelId} drew natively: {result}");
        return result;
    }

    private sealed record NativeWarriorPanels(string Attributes, string SkillSource, uint WeaponSeed,
        uint? SmithItemSeed, uint SmithPrice, uint GoldBefore, uint GoldAfterBuy, uint GoldAfterSell, int NativeResidents,
        string[] Captures);

    /**
     * Uses the Hero, Items and Trade panels as a player would in Town: read the attributes and
     * pick the class skill, move the weapon to the inventory and back, and buy then sell the
     * Smith's cheapest affordable item.
     */
    private async Task<NativeWarriorPanels> VerifyNativeWarriorPanelsAsync(Main main, string directory)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var captures = new List<string>();
        async Task Snap(string name)
        {
            if (!NativeWarriorVisible)
                return;
            var file = $"native-panel-{name}.png";
            await Capture(System.IO.Path.Combine(directory, file));
            captures.Add(file);
        }

        // Hero: the attributes match the authoritative player and the class skill is selectable.
        await SelectTab(sections, 0);
        var player = main.ObservedSnapshot!.Players.Single();
        var attributes = sections.GetChild(0).FindChildren("*", nameof(Label), true, false).OfType<Label>()
            .Single(label => label.Text.StartsWith("Attributes", StringComparison.Ordinal)).Text;
        Require(attributes.Contains($"Strength {player.Attributes.Strength.Base}", StringComparison.Ordinal)
            && attributes.Contains($"Vitality {player.Attributes.Vitality.Base}", StringComparison.Ordinal),
            $"The Hero panel shows \"{attributes}\" for the authoritative attributes {player.Attributes}.");
        var sources = (ItemList)main.FindChild("SpellSources", true, false);
        Require(sources.ItemCount > 0, "The Hero panel lists no class skill or spell source.");
        await ClickListItem(sections, 0, sources, 0);
        Require(sources.IsSelected(0), "Selecting the first Hero spell source did not select it.");
        var skill = sources.GetItemText(0);
        await Snap("hero");

        // Items: the equipped weapon moves into the inventory grid and back through the real panel.
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var weapon = player.Equipment.Single(item => item.Slot == 4).Clone();
        await SubmitNativeWarriorCommandAsync(main, "Unequip the weapon to the inventory", async () => {
            await ClickListItem(sections, 1, equipment, 4);
            await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(main.ObservedSnapshot!.Players.Single(), weapon.State));
        });
        await Until(() => main.ObservedSnapshot!.Players.Single() is { } moved
                && moved.Equipment.All(item => item.Slot != 4) && moved.Inventory.Any(item => item.ItemSeed == weapon.ItemSeed),
            "The unequipped weapon did not reach the inventory.", 5);
        await Snap("items");
        await SubmitNativeWarriorCommandAsync(main, "Re-equip the weapon", async () => {
            await SelectInventoryItem(main, sections, weapon.State.StableItemId);
            await ClickListItem(sections, 1, equipment, 4);
        });
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item => item.Slot == 4 && item.ItemSeed == weapon.ItemSeed),
            "The weapon did not return to the hand slot.", 5);

        // Trade: buy the Smith's cheapest affordable item, then sell it back to him.
        await ApproachDeclaredDestination(main, "towner:0", distanceBoundedApproachDeadline: true);
        await SubmitNativeWarriorCommandAsync(main, "Open the Smith", () => ClickControl(main, sections, 2, "OpenSmith"));
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 1, "The Smith's stock did not open.", 5);
        // Standing at the Smith, he and his neighbours are drawn with native resident art when its pack is set.
        var residentArt = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(NativeTownerAssets.ManifestEnvironmentVariable));
        var residents = 0;
        await Until(() => (residents = main.ObservedNativeTownerDrawing.Count(resident => resident.NativeArt)) > 0 || !residentArt,
            "Town residents near the Smith were not drawn with native art.", 5);
        var goldBefore = main.ObservedSnapshot!.Players.Single().Gold;
        var stock = main.ObservedSnapshot.ActiveStore!.Items.OrderBy(item => item.StoreSlot).ToArray();
        var cheapest = stock.Select((item, index) => (Item: item, Index: index))
            .Where(entry => entry.Item.Price <= goldBefore).OrderBy(entry => entry.Item.Price).FirstOrDefault();
        uint? bought = null;
        var goldAfterBuy = goldBefore;
        var goldAfterSell = goldBefore;
        if (cheapest.Item is { } row) {
            await SubmitNativeWarriorCommandAsync(main, "Buy from the Smith", async () => {
                await ClickListItem(sections, 2, (ItemList)main.FindChild("StoreItems", true, false), cheapest.Index);
                await ClickControl(main, sections, 2, "BuySelected");
            });
            await Until(() => main.ObservedSnapshot!.Players.Single().Gold == goldBefore - row.Price,
                "The Smith purchase did not debit its price.", 5);
            var purchased = main.ObservedSnapshot!.Players.Single().Inventory
                .Single(item => item.State.StableItemId == row.State.StableItemId && item.ItemSeed != weapon.ItemSeed);
            bought = purchased.ItemSeed;
            goldAfterBuy = main.ObservedSnapshot.Players.Single().Gold;
            await Snap("trade");
            var sell = AuthoredDescendantButtons(sections.GetChild(2)).Single(button => button.Text.StartsWith("Sell", StringComparison.Ordinal));
            await SubmitNativeWarriorCommandAsync(main, "Sell to the Smith", async () => {
                await SelectInventoryItem(main, sections, purchased.State.StableItemId);
                await RevealControl(sections, 2, sell);
                await Until(() => !sell.Disabled, "Selling the selected item stayed disabled.", 5);
                await Click(sell.GetGlobalRect().GetCenter());
            });
            await Until(() => main.ObservedSnapshot!.Players.Single() is { } seller
                    && seller.Gold > goldAfterBuy && seller.Inventory.All(item => item.ItemSeed != purchased.ItemSeed),
                "The Smith did not buy the item back.", 5);
            goldAfterSell = main.ObservedSnapshot!.Players.Single().Gold;
        } else {
            await Snap("trade");
        }
        await SubmitNativeWarriorCommandAsync(main, "Close the Smith", () => ClickControl(main, sections, 2, "CloseStore"));
        await SelectTab(sections, 4);
        GD.Print($"Native Warrior panels passed: skill={skill}; smith={bought?.ToString() ?? "none affordable"}; gold={goldBefore}->{goldAfterBuy}->{goldAfterSell}");
        return new NativeWarriorPanels(attributes, skill, weapon.ItemSeed, bought, cheapest.Item?.Price ?? 0,
            goldBefore, goldAfterBuy, goldAfterSell, residents, captures.ToArray());
    }

    /** Runs one real-control action; a clock-gated TooLate refusal changes nothing, so it is repeated. */
    private async Task SubmitNativeWarriorCommandAsync(Main main, string action, Func<Task> submit)
    {
        for (var attempt = 0; attempt < 4; attempt++) {
            var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
            await submit();
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main), $"{action} did not settle.", 10);
            var receipt = main.ObservedCommandResult!;
            if (receipt is { Status: CommandStatus.Rejected, RejectReason: CommandRejectReason.TooLate })
                continue;
            Require(Devilution.Client.Protocol.AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt),
                $"{action}: {receipt.Status}/{receipt.RejectReason}.");
            return;
        }
        throw new InvalidOperationException($"{action} stayed too late after four attempts.");
    }
}
