using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using System.Text.Json;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Authored preview only: real controls and server outcomes, no state injection. */
    private async Task VerifyEquipmentAndTownLoop(Main main, string directory, bool visible)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        var player = main.ObservedSnapshot!.Players.Single();
        var bow = player.Equipment.Single(item => item.Slot == 4).Clone();
        var sword = player.Inventory.Single(item => item.ItemSeed == 6001).Clone();
        var initialGold = player.Gold;
        Require(initialGold == 100 && sword.State.ItemIndex == 119 && sword.State.StableItemId == 2160,
            "The preview must use the unmodified Rogue starting gold and native sword definition.");
        Require(equipment.ItemCount == 7, "Empty equipment destinations are missing.");

        // Free the occupied hand through the same inventory grid used by players.
        await ClickListItem(sections, 1, equipment, 4);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        var emptyCell = FindInventorySpace(player, bow.State);
        await ClickGridCell(main, sections, 1, "InventoryGrid", emptyCell);
        await AcceptedAndSettled(main, sequence, "Unequip starting bow");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Count == 0,
            "The starting bow did not leave equipment.", 5);
        Require(equipment.ItemCount == 7 && equipment.GetItemText(4).EndsWith("empty", StringComparison.Ordinal)
            && equipment.MouseFilter == Control.MouseFilterEnum.Stop,
            "Unequipping the final item disabled the empty equipment destination.");

        await SelectInventoryItem(main, sections, sword.State.StableItemId);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickListItem(sections, 1, equipment, 4);
        await AcceptedAndSettled(main, sequence, "Equip earned sword in empty hand");
        await Until(() => main.ObservedSnapshot!.Players.Single().Equipment.Any(item => item.Slot == 4
            && item.State.StableItemId == sword.State.StableItemId), "The earned sword was not equipped.", 5);
        Require(main.ObservedSnapshot!.Players.Single().Inventory.All(item => item.State.StableItemId != sword.State.StableItemId),
            "Equipping duplicated the earned sword.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-equipment.png"));

        await SelectTab(sections, 4);
        var routes = (OptionButton)main.FindChild("Routes", true, false);
        Require(routes.ItemCount == 2 && routes.GetItemText(1).Contains("preview cathedral to town"),
            "The isolated reverse-stair route is missing or ambiguous.");
        await SelectRoute(routes, 1);
        await ClickControl(main, sections, 4, "Travel");
        await Until(() => main.ObservedSnapshot!.Players.Single().LevelId == 0 && Settled(main),
            "The authored return stair did not reach town.", 15);
        player = main.ObservedSnapshot!.Players.Single();
        Require(player.PositionX == 57 && player.PositionY == 39,
            "The return stair did not reach its non-trigger town destination.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-return.png"));

        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 2, "OpenSmith");
        await AcceptedAndSettled(main, sequence, "Open Smith in town");
        await Until(() => main.ObservedSnapshot!.ActiveStore?.StoreId == 1, "Smith stock did not arrive.", 5);
        var stock = main.ObservedSnapshot!.ActiveStore.Items.OrderBy(item => item.StoreSlot).ToArray();
        var purchaseIndex = Array.FindIndex(stock, item => item.StoreSlot == 1);
        Require(purchaseIndex >= 0 && stock[purchaseIndex].Price == 25 && stock[purchaseIndex].ItemSeed == 43,
            "The existing affordable Smith stock changed.");
        await ClickListItem(sections, 2, (ItemList)main.FindChild("StoreItems", true, false), purchaseIndex);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 2, "BuySelected");
        await AcceptedAndSettled(main, sequence, "Buy Smith buckler");
        await Until(() => main.ObservedSnapshot!.Players.Single().Gold == initialGold - 25,
            "The purchase did not debit the authoritative gold.", 5);
        var bought = main.ObservedSnapshot!.Players.Single().Inventory.Single(item => item.ItemSeed == 43).Clone();
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-purchase.png"));
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, sequence, "Close Smith");
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "OpenStash");
        await AcceptedAndSettled(main, sequence, "Open stash in town");
        await Until(() => main.ObservedSnapshot!.Players.Single().StashOpen, "Stash did not open.", 5);

        await StashInventoryItem(main, sections, bow.State.StableItemId, 0);
        await StashInventoryItem(main, sections, bought.State.StableItemId, 5);
        // An explicit stash selection still withdraws an item after snapshots.
        await ClickGridCell(main, sections, 3, "StashGrid", 5);
        var tick = main.ObservedSnapshot!.Tick;
        await Until(() => main.ObservedSnapshot!.Tick > tick + 2, "No snapshot arrived during stash selection.", 5);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 1, "InventoryGrid", FindInventorySpace(main.ObservedSnapshot.Players.Single(), bought.State));
        await AcceptedAndSettled(main, sequence, "Withdraw selected stash item");
        await Until(() => main.ObservedSnapshot!.Players.Single().Inventory.Any(item => item.ItemSeed == bought.ItemSeed),
            "The selected stash item was not withdrawn.", 5);
        await StashInventoryItem(main, sections, bought.State.StableItemId, 5);
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "DepositStashGold");
        await AcceptedAndSettled(main, sequence, "Deposit ten gold");
        await Until(() => main.ObservedSnapshot!.Players.Single().StashGold == 10, "Gold deposit was lost.", 5);
        ((LineEdit)main.FindChild("StashGoldAmount", true, false)).Text = "5";
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "WithdrawStashGold");
        await AcceptedAndSettled(main, sequence, "Withdraw five gold");
        await Until(() => main.ObservedSnapshot!.Players.Single() is { Gold: 70, StashGold: 5 },
            "Gold withdrawal did not conserve the purchase/deposit balance.", 5);
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-stash-gold.png"));
        await RevealControl(sections, 3, (Control)main.FindChild("StashGrid", true, false));
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-stash-items.png"));
        sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickControl(main, sections, 3, "CloseStash");
        await AcceptedAndSettled(main, sequence, "Close stash");
        player = main.ObservedSnapshot!.Players.Single();
        var evidence = new TownLoopEvidence(sword.State.StableItemId, bow.State.StableItemId,
            bought.State.StableItemId, bow.ItemSeed, bought.ItemSeed, player.Experience,
            player.Gold, player.StashGold, main.ObservedIdentity!.ContentManifestHash,
            main.ObservedIdentity.RulesetIdentityHash);
        AssertTownLoopState(main, evidence);
        WriteEvidence(directory, "townloop-items-gold.json", evidence);
    }

    private async Task VerifyTownLoopRestored(Main main, string directory, bool visible)
    {
        using var combatEvidence = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "journey-combat-loot.json")));
        VerifyRemainingBelt(main.ObservedSnapshot!.Players.Single(), combatEvidence.RootElement);
        var evidence = JsonSerializer.Deserialize<TownLoopEvidence>(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "townloop-items-gold.json")))
            ?? throw new InvalidDataException("Town-loop evidence is empty.");
        AssertTownLoopState(main, evidence);
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await RevealControl(sections, 1, (Control)main.FindChild("EquipmentItems", true, false));
        var swordName = ItemPresentationStyle.ResolveName(main.ObservedSnapshot!.Players.Single().Equipment.Single().State);
        Require(!string.IsNullOrEmpty(swordName)
            && ((ItemList)main.FindChild("EquipmentItems", true, false)).GetItemText(4).Contains(swordName),
            "Restored equipment did not reach the actual controls.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-restored-equipment.png"));
        await RevealControl(sections, 3, (Control)main.FindChild("StashGrid", true, false));
        Require(((GridContainer)main.FindChild("StashGrid", true, false)).GetChild<Button>(0).Text != "·"
            && ((GridContainer)main.FindChild("StashGrid", true, false)).GetChild<Button>(5).Text != "·",
            "Restored stash items did not reach the actual controls.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "townloop-restored-stash.png"));
    }

    private static void AssertTownLoopState(Main main, TownLoopEvidence expected)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(main.ObservedIdentity!.ContentManifestHash == expected.ContentHash
            && main.ObservedIdentity.RulesetIdentityHash == expected.RulesetHash,
            "The preview content/ruleset identity changed across restore.");
        Require(player.LevelId == 0 && player.Experience == expected.Experience && player.Experience == 100,
            "Town return or combat experience was lost.");
        Require(!player.StashOpen && player.ActiveStoreId == 0 && player.Gold == expected.Gold
            && player.StashGold == expected.StashGold && player.Gold + player.StashGold == 75,
            "Gold conservation or closed service state was not preserved.");
        Require(player.Equipment.Count == 1 && player.Equipment.Single() is { Slot: 4, ItemSeed: 6001 } sword
            && sword.State.StableItemId == expected.SwordId, "The earned sword's equipment identity was not preserved.");
        Require(player.Stash.Count == 2
            && player.Stash.Any(item => item is { Page: 0, Cell: 0 } && item.Item.ItemSeed == expected.BowSeed
                && item.Item.State.StableItemId == expected.BowId)
            && player.Stash.Any(item => item is { Page: 0, Cell: 5 } && item.Item.ItemSeed == expected.BoughtSeed
                && item.Item.State.StableItemId == expected.BoughtId), "Stash item identity or placement was lost.");
        var ids = player.Inventory.Select(item => item.State.StableItemId)
            .Concat(player.Equipment.Select(item => item.State.StableItemId))
            .Concat(player.Belt.Select(item => item.State.StableItemId))
            .Concat(player.Stash.Select(item => item.Item.State.StableItemId))
            .Concat(player.HeldItem is null ? [] : new[] { player.HeldItem.State.StableItemId }).ToArray();
        Require(new[] { expected.SwordId, expected.BowId, expected.BoughtId }.All(id => id != 0 && ids.Count(candidate => candidate == id) == 1),
            "The equipment/trade/stash route lost or duplicated an item.");
        Require(!main.ObservedSnapshot.WorldItems.Any(item => item.EntityId == 1000009), "The picked-up drop was duplicated.");
    }

    private async Task StashInventoryItem(Main main, TabContainer sections, uint stableId, int cell)
    {
        await SelectInventoryItem(main, sections, stableId);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 3, "StashGrid", cell);
        await AcceptedAndSettled(main, sequence, "Move carried item into stash");
        await Until(() => main.ObservedSnapshot!.Players.Single().Stash.Any(item => item.Cell == cell
            && item.Item.State.StableItemId == stableId), "Stash transfer did not reach its chosen cell.", 5);
        await Frame();
        Require(main.GetChildren().OfType<HudPresentation>().Single().SelectedItemReference is null,
            "A snapshot selected the newly stored item without user input.");
    }

    private async Task SelectInventoryItem(Main main, TabContainer sections, uint stableId)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var index = player.Inventory.ToList().FindIndex(item => item.State.StableItemId == stableId);
        Require(index >= 0, "The carried item is missing.");
        var layout = InventoryLayout.Build(player.Inventory, player.InventoryGrid);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await ClickGridCell(main, sections, 1, "InventoryGrid", layout.Anchors[index]);
        var selection = main.GetChildren().OfType<HudPresentation>().Single().SelectedItemReference;
        Require(selection is { Location: PlayerItemLocation.Inventory } && selection.Slot == index
            && selection.StableItemId == stableId,
            $"The inventory click selected {selection} instead of inventory {index}/{stableId}.");
        Require(main.ObservedPendingCommandCount == 0 && main.ObservedCommandResult!.ClientSequence == sequence,
            "Selecting a carried inventory item unexpectedly submitted a gameplay command.");
    }

    private static int FindInventorySpace(PlayerSnapshot player, ItemStateSnapshot item)
    {
        var layout = InventoryLayout.Build(player.Inventory, player.InventoryGrid);
        var width = Math.Max(1, (int)item.InventoryWidth);
        var height = Math.Max(1, (int)item.InventoryHeight);
        for (var cell = 0; cell < 40; cell++) {
            if (cell % 10 + width > 10 || cell / 10 + height > 4)
                continue;
            if (Enumerable.Range(0, height).All(y => Enumerable.Range(0, width)
                .All(x => layout.Occupants[cell + y * 10 + x] < 0)))
                return cell;
        }
        throw new InvalidOperationException("The ordinary starting inventory cannot hold the unequipped bow.");
    }

    private async Task ClickControl(Main main, TabContainer sections, int page, string name)
    {
        var control = (Button)main.FindChild(name, true, false);
        await RevealControl(sections, page, control);
        Require(!control.Disabled, $"{name} is disabled.");
        await Click(control.GetGlobalRect().GetCenter());
    }

    private async Task ClickGridCell(Main main, TabContainer sections, int page, string name, int cell)
    {
        var control = ((GridContainer)main.FindChild(name, true, false)).GetChild<Button>(cell);
        await RevealControl(sections, page, control);
        Require(!control.Disabled, $"{name} cell {cell} is disabled.");
        await Click(control.GetGlobalRect().GetCenter());
    }

    private async Task ClickListItem(TabContainer sections, int page, ItemList list, int index)
    {
        await RevealControl(sections, page, list);
        await Click(list.GetGlobalRect().Position + list.GetItemRect(index).GetCenter());
    }

    private async Task RevealControl(TabContainer sections, int page, Control control)
    {
        if (sections.CurrentTab != page)
            await SelectTab(sections, page);
        ((ScrollContainer)sections.GetChild(page)).EnsureControlVisible(control);
        await Frame();
        await Frame();
        Require(control.IsVisibleInTree(), $"{control.Name} is not visible in its tab.");
        Require(((Control)sections.GetChild(page)).GetGlobalRect().Encloses(control.GetGlobalRect().Grow(-0.5f)),
            $"{control.Name} {control.GetGlobalRect()} remains clipped by its tab {((Control)sections.GetChild(page)).GetGlobalRect()}.");
    }

    private async Task SelectRoute(OptionButton routes, int index)
    {
        await Click(routes.GetGlobalRect().GetCenter());
        var popup = routes.GetPopup();
        Require(popup.Visible, "The route selection popup did not open.");
        for (var attempt = 0; popup.GetFocusedItem() != index && attempt <= routes.ItemCount; attempt++)
            await PressControlKey(popup, Key.Down);
        Require(popup.GetFocusedItem() == index,
            $"Keyboard input did not focus route {index}: focused={popup.GetFocusedItem()}, window={popup.GetWindowId()}, embedded={popup.IsEmbedded()}.");
        await PressControlKey(popup, Key.Enter);
        Require(routes.Selected == index && !popup.Visible, "The route popup did not accept the selected route.");
    }

    private async Task PressControlKey(Window target, Key key)
    {
        // Window dispatch invokes PopupMenu's _input_from_window. PushInput only
        // reaches child controls and bypasses that menu navigation handler.
        Input.ParseInputEvent(new InputEventKey { WindowId = target.GetWindowId(), Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Frame();
        Input.ParseInputEvent(new InputEventKey { WindowId = target.GetWindowId(), Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frame();
    }

    private sealed record TownLoopEvidence(uint SwordId, uint BowId, uint BoughtId, uint BowSeed,
        uint BoughtSeed, uint Experience, uint Gold, uint StashGold, string ContentHash, string RulesetHash);
}
