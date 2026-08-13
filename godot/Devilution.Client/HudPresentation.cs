using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Data-driven authoritative inventory, store, interaction, and event UI. */
public partial class HudPresentation : CanvasLayer
{
    private const uint IdentifySpellId = 5;
    private const uint RepairSpellId = 26;
    private const uint RechargeSpellId = 27;
    private const uint TrapDisarmSpellId = 28;
    private const int InventoryColumns = 10;
    private const int VisibleInventoryRows = 4;
    private readonly Label status = new();
    private readonly Label state = new();
    private readonly Label inventory = new();
    private readonly Label store = new();
    private readonly Label dialog = new();
    private readonly Label commandFeedback = new();
    private readonly Label diagnostics = new();
    private readonly ItemList storeItems = new();
    private readonly GridContainer inventoryGrid = new() { Columns = InventoryColumns };
    private readonly List<Button> inventoryCells = [];
    private readonly List<uint> storeSlots = [];
    private readonly Button openSmith = new() { Text = "Open Smith" };
    private readonly Button openAdria = new() { Text = "Open Adria" };
    private readonly Button buySelected = new() { Text = "Buy selected" };
    private readonly Button sellSelected = new() { Text = "Sell selected" };
    private readonly Button repairSelected = new() { Text = "Repair selected" };
    private readonly Button rechargeSelected = new() { Text = "Recharge selected" };
    private readonly Button identifySelected = new() { Text = "Identify selected" };
    private readonly Button disarmTrap = new() { Text = "Disarm trap (Rogue)" };
    private readonly Button interactObject = new() { Text = "Interact" };
    private readonly Button advanceQuest = new() { Text = "Advance quest" };
    private readonly Button refillMana = new() { Text = "Refill mana" };
    private readonly Button dropHeld = new() { Text = "Drop held (X)" };
    private readonly Button clearEvents = new() { Text = "Clear events" };
    private readonly CheckButton diagnosticsToggle = new() { Text = "Diagnostics (F3)" };
    private readonly CheckButton highContrastToggle = new() { Text = "High contrast (F4)" };
    private PanelContainer? sidePanel;
    private InventoryLayout inventoryLayout = InventoryLayout.Build(null, null, InventoryColumns, VisibleInventoryRows);
    private uint activeStoreId;
    private uint? selectedStoreSlot;
    private uint? availableObjectEntityId;
    private uint? availableTrapEntityId;
    private uint? availableQuestId;
    private int selectedInventoryIndex = -1;
    private string selectedSpellText = "none";

    public event Action<uint>? OpenStoreRequested;
    public event Action<uint, uint>? PurchaseRequested;
    public event Action<uint>? SellItemRequested;
    public event Action<uint>? RepairItemRequested;
    public event Action<uint>? RechargeItemRequested;
    public event Action<uint>? IdentifyItemRequested;
    public event Action<uint>? DisarmTrapRequested;
    public event Action<uint, uint>? MoveInventoryItemRequested;
    public event Action<uint>? OperateObjectRequested;
    public event Action<uint>? AdvanceQuestRequested;
    public event Action? RefillManaRequested;
    public event Action? DropHeldItemRequested;
    public event Action? ClearEventsRequested;

    public override void _Ready()
    {
        var statusPanel = new PanelContainer { Position = new Vector2(16, 16), Size = new Vector2(760, 100) };
        var statusBox = new VBoxContainer();
        statusPanel.AddChild(statusBox);
        statusBox.AddChild(status);
        statusBox.AddChild(state);
        AddChild(statusPanel);

        sidePanel = new PanelContainer { Position = new Vector2(880, 16), Size = new Vector2(380, 680) };
        var scroll = new ScrollContainer();
        var sideBox = new VBoxContainer();
        sidePanel.AddChild(scroll);
        scroll.AddChild(sideBox);
        sideBox.AddChild(new Label { Text = "AUTHORITATIVE CLIENT" });
        sideBox.AddChild(commandFeedback);
        sideBox.AddChild(diagnostics);
        sideBox.AddChild(inventory);
        sideBox.AddChild(inventoryGrid);
        sideBox.AddChild(new Label { Text = "Store stock" });
        storeItems.CustomMinimumSize = new Vector2(340, 110);
        sideBox.AddChild(storeItems);
        sideBox.AddChild(store);
        sideBox.AddChild(new Label { Text = "Dialog / world interactions" });
        sideBox.AddChild(dialog);

        var controls = new GridContainer { Columns = 2 };
        foreach (var button in new Control[] {
            openSmith, openAdria, buySelected, sellSelected, repairSelected,
            rechargeSelected, identifySelected, disarmTrap, interactObject, advanceQuest,
            refillMana, dropHeld, clearEvents,
        })
            controls.AddChild(button);
        sideBox.AddChild(controls);
        sideBox.AddChild(diagnosticsToggle);
        sideBox.AddChild(highContrastToggle);
        AddChild(sidePanel);

        for (var index = 0; index < InventoryColumns * VisibleInventoryRows; index++) {
            var cell = new Button {
                CustomMinimumSize = new Vector2(32, 32),
                Text = "·",
            };
            var cellIndex = index;
            cell.Pressed += () => SelectOrMoveInventoryItem(cellIndex);
            inventoryCells.Add(cell);
            inventoryGrid.AddChild(cell);
        }

        storeItems.ItemSelected += index => {
            var itemIndex = checked((int)index);
            selectedStoreSlot = itemIndex >= 0 && itemIndex < storeSlots.Count ? storeSlots[itemIndex] : null;
        };
        openSmith.Pressed += () => OpenStoreRequested?.Invoke(1);
        openAdria.Pressed += () => OpenStoreRequested?.Invoke(10);
        buySelected.Pressed += BuySelected;
        sellSelected.Pressed += () => InvokeInventoryAction(SellItemRequested);
        repairSelected.Pressed += () => InvokeInventoryAction(RepairItemRequested);
        rechargeSelected.Pressed += () => InvokeInventoryAction(RechargeItemRequested);
        identifySelected.Pressed += () => InvokeInventoryAction(IdentifyItemRequested);
        disarmTrap.Pressed += () => {
            if (availableTrapEntityId is { } entityId)
                DisarmTrapRequested?.Invoke(entityId);
        };
        interactObject.Pressed += () => {
            if (availableObjectEntityId is { } entityId)
                OperateObjectRequested?.Invoke(entityId);
        };
        advanceQuest.Pressed += () => {
            if (availableQuestId is { } questId)
                AdvanceQuestRequested?.Invoke(questId);
        };
        refillMana.Pressed += () => RefillManaRequested?.Invoke();
        dropHeld.Pressed += () => DropHeldItemRequested?.Invoke();
        clearEvents.Pressed += () => ClearEventsRequested?.Invoke();
        diagnosticsToggle.Toggled += visible => diagnostics.Visible = visible;
        highContrastToggle.Toggled += _ => ApplyAccessibility();

        status.Text = "Starting...";
        commandFeedback.Text = "Commands are sent to the authoritative server.";
        inventory.Text = "Inventory\nWaiting for authoritative snapshot";
        store.Text = "No active store";
        dialog.Text = "No interaction selected.";
        diagnostics.Text = "Diagnostics disabled. Press F3 to show.";
        diagnostics.Visible = false;
    }

    public void SetStatus(string value)
    {
        status.Text = value;
    }

    public void SetSelectedSpellSource(AuthoritativeSpellSource? source)
    {
        selectedSpellText = source?.DisplayName ?? "none";
    }

    public void ToggleDiagnostics() => diagnosticsToggle.ButtonPressed = !diagnosticsToggle.ButtonPressed;

    public void ToggleHighContrast() => highContrastToggle.ButtonPressed = !highContrastToggle.ButtonPressed;

    public void Apply(AuthoritativeClientModel model, AuthoritativeClient? client, Task? connectionTask)
    {
        if (!string.IsNullOrWhiteSpace(model.LastError))
            status.Text = model.LastError;
        else if (client?.IsConnected == true)
            status.Text = $"Connected - retry {client.RetryTimeout.TotalMilliseconds:0} ms";
        else if (connectionTask is { IsFaulted: true })
            status.Text = connectionTask.Exception?.GetBaseException().Message ?? "Connection failed";

        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null || selectedInventoryIndex >= player.Inventory.Count)
            selectedInventoryIndex = -1;
        state.Text = player is null
            ? "Waiting for authoritative snapshot"
            : $"Tick {model.CurrentTick}   Level {player.LevelId}   Life {player.Life}/{player.LifeMaximum}   Mana {player.Mana}/{player.ManaMaximum}{(player.ManaShieldActive ? "   Mana Shield" : string.Empty)}{(player.ReflectCharges > 0 ? $"   Reflect {player.ReflectCharges}" : string.Empty)}{(player.StatusEffects.Any(effect => effect.Behavior == StatusEffectBehavior.Infravision) ? "   Infravision" : string.Empty)}{(player.StatusEffects.Any(effect => effect.Behavior == StatusEffectBehavior.Search) ? "   Search" : string.Empty)}{(player.StatusEffects.Any(effect => effect.Behavior == StatusEffectBehavior.Etherealize) ? "   Etherealize" : string.Empty)}   Spell {selectedSpellText}   Gold {player.Gold}   XP {player.Experience}";

        commandFeedback.Text = FormatCommandResult(model.LastCommandResult);
        diagnostics.Text = FormatDiagnostics(model, client);
        inventory.Text = FormatInventory(player);
        ApplyInventoryGrid(player);
        ApplyStore(model.Snapshot?.ActiveStore);
        dialog.Text = FormatDialog(model.Snapshot, model.RecentEvents);
        availableObjectEntityId = model.Snapshot?.Objects.FirstOrDefault(@object => !@object.Activated)?.EntityId;
        availableTrapEntityId = model.ArmedTrapObjects
            .FirstOrDefault(@object => player is not null && @object.LevelId == player.LevelId)
            ?.EntityId;
        availableQuestId = model.Snapshot?.Quests.FirstOrDefault(quest => !quest.Completed)?.QuestId;

        var connected = client?.IsConnected == true;
        var activeStore = model.Snapshot?.ActiveStore;
        var hasInventory = player is not null && player.Inventory.Count > 0;
        var hasSelectedInventory = hasInventory && selectedInventoryIndex >= 0 && selectedInventoryIndex < player!.Inventory.Count;
        var hasIdentifyAbility = player?.AbilitySpellId == IdentifySpellId;
        var hasRepairAbility = player?.AbilitySpellId == RepairSpellId;
        var hasRechargeAbility = player?.AbilitySpellId == RechargeSpellId;
        var hasTrapDisarmAbility = player?.AbilitySpellId == TrapDisarmSpellId;
        openSmith.Disabled = !connected;
        openAdria.Disabled = !connected;
        buySelected.Disabled = !connected || activeStore is null || selectedStoreSlot is null;
        sellSelected.Disabled = !connected || !hasSelectedInventory;
        repairSelected.Text = activeStore is null && hasRepairAbility
            ? "Repair selected (Warrior)"
            : "Repair selected";
        repairSelected.Disabled = !connected
            || !hasSelectedInventory
            || (activeStore is null && !hasRepairAbility);
        rechargeSelected.Text = activeStore is null && hasRechargeAbility
            ? "Recharge selected (Sorcerer)"
            : "Recharge selected";
        rechargeSelected.Disabled = !connected
            || !hasSelectedInventory
            || (activeStore is null && !hasRechargeAbility);
        identifySelected.Text = activeStore is null && hasIdentifyAbility
            ? "Identify selected (Bard)"
            : "Identify selected";
        identifySelected.Disabled = !connected
            || !hasSelectedInventory
            || (activeStore is null && !hasIdentifyAbility);
        disarmTrap.Disabled = !connected || !hasTrapDisarmAbility || availableTrapEntityId is null;
        interactObject.Disabled = !connected || availableObjectEntityId is null;
        advanceQuest.Disabled = !connected || availableQuestId is null;
        refillMana.Disabled = !connected;
        dropHeld.Disabled = !connected || player?.HeldItem is null;
        clearEvents.Disabled = model.RecentEvents.Count == 0;
        ApplyAccessibility();
    }

    private void ApplyStore(StoreSnapshot? activeStore)
    {
        var nextStoreId = activeStore?.StoreId ?? 0;
        if (nextStoreId != activeStoreId)
            selectedStoreSlot = null;
        activeStoreId = nextStoreId;
        store.Text = activeStore is null || activeStore.StoreId == 0
            ? "No active store"
            : $"Store {activeStore.StoreId}: select stock above";

        storeSlots.Clear();
        storeItems.Clear();
        if (activeStore is null)
            return;
        foreach (var item in activeStore.Items.OrderBy(item => item.StoreSlot)) {
            storeSlots.Add(item.StoreSlot);
            storeItems.AddItem(FormatStoreItem(item));
        }
        if (selectedStoreSlot is { } slot) {
            var selectedIndex = storeSlots.IndexOf(slot);
            if (selectedIndex < 0)
                selectedStoreSlot = null;
            else
                storeItems.Select(selectedIndex, false);
        }
    }

    private void ApplyInventoryGrid(PlayerSnapshot? player)
    {
        inventoryLayout = InventoryLayout.Build(player?.Inventory, player?.InventoryGrid, InventoryColumns, VisibleInventoryRows);

        for (var cellIndex = 0; cellIndex < inventoryCells.Count; cellIndex++) {
            var occupant = inventoryLayout.Occupants[cellIndex];
            var anchor = occupant >= 0 && occupant < inventoryLayout.Anchors.Count ? inventoryLayout.Anchors[occupant] : -1;
            var cell = inventoryCells[cellIndex];
            cell.Text = occupant < 0 ? "·" : anchor == cellIndex
                ? selectedInventoryIndex == occupant ? $"[{occupant + 1}]" : $"{occupant + 1}"
                : "·";
            cell.TooltipText = occupant >= 0 && player is not null && occupant < player.Inventory.Count
                ? DescribeItem(player.Inventory[occupant].State)
                : "Empty inventory cell. Select an item, then choose a destination.";
            cell.Disabled = player is null;
        }
    }

    private void SelectOrMoveInventoryItem(int cellIndex)
    {
        var occupant = inventoryLayout.Occupants[cellIndex];
        if (occupant >= 0) {
            selectedInventoryIndex = occupant;
            return;
        }
        if (selectedInventoryIndex >= 0)
            MoveInventoryItemRequested?.Invoke(checked((uint)selectedInventoryIndex), checked((uint)cellIndex));
    }

    private void BuySelected()
    {
        if (activeStoreId != 0 && selectedStoreSlot is { } slot)
            PurchaseRequested?.Invoke(activeStoreId, slot);
    }

    private void InvokeInventoryAction(Action<uint>? action)
    {
        if (selectedInventoryIndex >= 0)
            action?.Invoke(checked((uint)selectedInventoryIndex));
    }

    private void AdvanceFirstQuest()
    {
        if (availableQuestId is { } questId)
            AdvanceQuestRequested?.Invoke(questId);
    }

    private static string FormatInventory(PlayerSnapshot? player)
    {
        if (player is null)
            return "Inventory\nWaiting for authoritative snapshot";
        if (player.Inventory.Count == 0)
            return player.HeldItem is null
                ? "Inventory\n(empty)\nClick an occupied cell after an item arrives."
                : $"Inventory\n(empty)\nHeld: {DescribeItem(player.HeldItem.State)}";
        var held = player.HeldItem is null
            ? string.Empty
            : $"\nHeld: {DescribeItem(player.HeldItem.State)}";
        return $"Inventory ({player.Inventory.Count})\nClick an item to select it; click an empty cell to request a move.{held}";
    }

    private static string FormatStoreItem(StoreItemSnapshot item)
    {
        return $"slot {item.StoreSlot}  {DescribeItem(item.State)}  price {item.Price}";
    }

    private static string DescribeItem(ItemStateSnapshot? state)
    {
        if (state is null)
            return "unknown item";
        if (!string.IsNullOrEmpty(state.BaseName))
            return $"{state.BaseName}, value {state.Value}";
        var details = state.ItemType switch {
            1 => state.MinDamage > 0 ? $"weapon {state.MinDamage}-{state.MaxDamage}" : $"armor {state.ArmorClass}",
            _ => $"item type {state.ItemType}",
        };
        return $"{details}, value {state.Value}, footprint {Math.Max(1, state.InventoryWidth)}x{Math.Max(1, state.InventoryHeight)}";
    }

    private static string FormatDialog(Snapshot? snapshot, IReadOnlyList<GameEvent> events)
    {
        var lines = new List<string>();
        if (snapshot is not null) {
            lines.AddRange(snapshot.Objects.Where(@object => !@object.Activated).Take(2).Select(@object => $"Object {@object.EntityId} is available."));
            lines.AddRange(snapshot.Quests.Where(quest => !quest.Completed).Take(2).Select(quest => $"Quest {quest.QuestId}: {quest.Progress}/{quest.RequiredProgress}"));
        }
        lines.AddRange(events.TakeLast(4).Select(@event => @event.EventCase switch {
            GameEvent.EventOneofCase.Damage => $"Damage {@event.Damage.Amount} to entity {@event.Damage.TargetEntityId}",
            GameEvent.EventOneofCase.Experience => $"Experience +{@event.Experience.Amount}",
            GameEvent.EventOneofCase.Healing => $"Healing +{FormatQ6(@event.Healing.AmountQ6 != 0 ? @event.Healing.AmountQ6 : @event.Healing.Amount)}",
            _ => "Authoritative event",
        }));
        return lines.Count == 0 ? "No interaction selected." : string.Join("\n", lines);
    }

    private static string FormatQ6(int amount)
    {
        return (amount / 64.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatCommandResult(CommandResult? result)
    {
        if (result is null)
            return "Commands are sent to the authoritative server.";
        return result.Status == CommandStatus.Accepted
            ? $"Last command accepted at tick {result.AppliedTick}."
            : $"Last command: {result.Status} ({result.RejectReason}).";
    }

    private void ApplyAccessibility()
    {
        var color = highContrastToggle.ButtonPressed ? Colors.White : new Color("d5d5e5");
        var muted = highContrastToggle.ButtonPressed ? Colors.White : new Color("b2b2c5");
        foreach (var control in new Control[] { status, state, inventory, store, dialog, commandFeedback, diagnostics }) {
            control.Modulate = control == diagnostics ? muted : color;
            control.AddThemeFontSizeOverride("font_size", highContrastToggle.ButtonPressed ? 18 : 14);
        }
    }

    private static string FormatDiagnostics(AuthoritativeClientModel model, AuthoritativeClient? client)
    {
        var snapshot = model.Snapshot;
        var localPlayer = snapshot?.Players.FirstOrDefault();
        var playerIdentity = string.IsNullOrEmpty(localPlayer?.CharacterName)
            ? $"Player {model.PlayerEntityId}"
            : $"{localPlayer.CharacterName} ({model.PlayerEntityId})";
        return $"Tick {model.CurrentTick}  pending {client?.PendingCommandCount ?? 0}  RTT {client?.RetryTimeout.TotalMilliseconds ?? 0:0} ms\n"
            + $"{playerIdentity}  entities: {snapshot?.Monsters.Count ?? 0} monsters, {snapshot?.WorldItems.Count ?? 0} items, {snapshot?.Projectiles.Count ?? 0} projectiles\n"
            + $"Prediction {(model.IsCorrectingPosition ? "active" : "settled")}  events {model.RecentEvents.Count}";
    }
}
