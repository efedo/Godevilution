using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Data-driven authoritative inventory, store, interaction, and event UI. */
public partial class HudPresentation : CanvasLayer
{
    private const int InventoryColumns = 10;
    private const int VisibleInventoryRows = 4;
    private readonly Label status = new() { Name = "ConnectionStatus" };
    private readonly Label sessionWarning = new() {
        Name = "SessionWarning", Visible = false, Modulate = new Color("ff9b9b"),
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };
    private readonly Label state = new();
    private readonly Label statusEffects = new();
    private readonly Label attributes = new();
    private readonly Label clock = new();
    private readonly Label inventory = new();
    private readonly Label store = new();
    private readonly Label stash = new();
    private readonly Label dialog = new();
    private readonly Label commandFeedback = new();
    private readonly Label diagnostics = new();
    private readonly Label victory = new();
    private readonly ItemList spellSources = new() { Name = "SpellSources", AllowReselect = true };
    private readonly ItemList storeItems = new() { Name = "StoreItems" };
    private readonly ItemList stashItems = new();
    private readonly OptionButton stashPageSelector = new();
    private readonly ItemList beltItems = new() { Name = "BeltItems" };
    private readonly ItemList equipmentItems = new() { Name = "EquipmentItems" };
    private readonly ItemList oilTargets = new();
    private readonly GridContainer inventoryGrid = new() { Name = "InventoryGrid", Columns = InventoryColumns };
    private readonly GridContainer stashGrid = new() { Name = "StashGrid", Columns = StashLayout.ColumnCount };
    private readonly List<Button> inventoryCells = [];
    private readonly List<Button> stashCells = [];
    private readonly List<uint> storeSlots = [];
    private readonly List<uint> beltSlots = [];
    private readonly List<uint> equipmentSlots = [];
    private readonly List<string> spellSourceKeys = [];
    private readonly List<PlayerItemReference> oilTargetReferences = [];
    private readonly Button openSmith = new() { Name = "OpenSmith", Text = "Open Smith" };
    private readonly Button openAdria = new() { Text = "Open Adria" };
    private readonly Button closeStore = new() { Name = "CloseStore", Text = "Close store" };
    private readonly Button openStash = new() { Name = "OpenStash", Text = "Open stash" };
    private readonly Button closeStash = new() { Name = "CloseStash", Text = "Close stash" };
    private readonly Button depositStashGold = new() { Name = "DepositStashGold", Text = "Deposit stash gold" };
    private readonly Button withdrawStashGold = new() { Name = "WithdrawStashGold", Text = "Withdraw stash gold" };
    private readonly LineEdit stashGoldAmount = new() {
        Name = "StashGoldAmount",
        Text = "10",
        PlaceholderText = "Gold amount",
    };
    private readonly Button buySelected = new() { Name = "BuySelected", Text = "Buy selected" };
    private readonly Button sellSelected = new() { Text = "Sell selected" };
    private readonly Button repairSelected = new() { Text = "Repair selected" };
    private readonly Button rechargeSelected = new() { Name = "RechargeSelected", Text = "Recharge selected" };
    private readonly Button identifySelected = new() { Text = "Identify selected" };
    private readonly Button readBookSelected = new() { Text = "Read book selected" };
    private readonly Button useConsumableSelected = new() { Name = "UseConsumable", Text = "Use consumable selected" };
    private readonly Button applyOilSelected = new() { Text = "Apply oil to target" };
    private readonly Button allocateStrength = new() { Text = "+Strength" };
    private readonly Button allocateMagic = new() { Text = "+Magic" };
    private readonly Button allocateDexterity = new() { Text = "+Dexterity" };
    private readonly Button allocateVitality = new() { Text = "+Vitality" };
    private readonly Button disarmTrap = new() { Text = "Disarm trap (Rogue)" };
    private readonly Button interactObject = new() { Text = "Interact" };
    private readonly Button advanceQuest = new() { Text = "Advance quest" };
    private readonly Button questTownerInteraction = new() { Name = "QuestTowner", Text = "Use quest/towner" };
    private readonly Button restartTown = new() {
        Name = "RestartTown", Text = "Restart in Town (Esc)", Visible = false,
        TooltipText = "Available after the server confirms that death has settled.",
    };
    private readonly Button refillMana = new() { Text = "Refill mana" };
    private readonly Button dropHeld = new() { Text = "Drop held (X)" };
    private readonly Button clearEvents = new() { Text = "Clear events" };
    private readonly CheckButton diagnosticsToggle = new() { Text = "Diagnostics (F3)" };
    private readonly CheckButton highContrastToggle = new() { Text = "High contrast (F4)" };
    private PanelContainer? sidePanel;
    private InventoryLayout inventoryLayout = InventoryLayout.Build(null, null, InventoryColumns, VisibleInventoryRows);
    private uint activeStoreId;
    private uint stashPage;
    private uint? selectedStoreSlot;
    private uint? availableObjectEntityId;
    private uint? availableTrapEntityId;
    private uint? availableQuestId;
    private QuestTownerInteractionSnapshot? availableQuestTownerInteraction;
    private int selectedInventoryIndex = -1;
    private int selectedStashItemIndex = -1;
    private uint? selectedStashStableItemId;
    private uint selectedStashItemPage;
    private uint selectedStashItemCell;
    private uint? selectedBeltSlot;
    private uint? selectedEquipmentSlot;
    private PlayerItemReference? selectedItemReference;
    private PlayerItemReference? selectedOilTarget;
    private PlayerSnapshot? currentPlayer;
    private string selectedSpellText = "none";

    public event Action<uint>? OpenStoreRequested;
    public event Action? CloseStoreRequested;
    public event Action? OpenStashRequested;
    public event Action? CloseStashRequested;
    public event Action<uint>? DepositStashGoldRequested;
    public event Action<uint>? WithdrawStashGoldRequested;
    public event Action<uint, uint>? PurchaseRequested;
    public event Action<PlayerItemReference>? SellItemRequested;
    public event Action<PlayerItemReference>? RepairItemRequested;
    public event Action<PlayerItemReference>? RechargeItemRequested;
    public event Action<PlayerItemReference>? IdentifyItemRequested;
    public event Action<PlayerItemReference>? ReadBookRequested;
    public event Action<PlayerItemReference>? UseConsumableRequested;
    public event Action<PlayerItemReference, PlayerItemReference>? ApplyOilRequested;
    public event Action<uint>? DisarmTrapRequested;
    public event Action<uint, uint>? MoveInventoryItemRequested;
    public event Action<PlayerItemReference, PlayerItemReference>? MoveItemRequested;
    public event Action<PlayerAttribute>? AllocateAttributeRequested;
    public event Action<uint>? OperateObjectRequested;
    public event Action<uint>? AdvanceQuestRequested;
    public event Action<uint, uint>? QuestTownerInteractionRequested;
    public event Action? RestartTownRequested;
    public event Action? RefillManaRequested;
    public event Action? DropHeldItemRequested;
    public event Action? ClearEventsRequested;
    public event Action<string>? SpellSourceSelected;

    /** Selected carried item used by world-drop routing; stash items are rejected by the client rules. */
    public PlayerItemReference? SelectedItemReference => selectedItemReference;

    /** Clears pending item selection after a drop intent or explicit spell choice. */
    public void ClearItemSelection() => ClearSelectedItem();

    public override void _Ready()
    {
        BuildLayout();

        for (var index = 0; index < InventoryColumns * VisibleInventoryRows; index++) {
            var cell = new Button {
                CustomMinimumSize = new Vector2(32, 32),
                ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Text = "·",
            };
            var cellIndex = index;
            cell.Pressed += () => SelectOrMoveInventoryItem(cellIndex);
            inventoryCells.Add(cell);
            inventoryGrid.AddChild(cell);
        }

        for (var index = 0; index < StashLayout.CellCount; index++) {
            var cell = new Button {
                CustomMinimumSize = new Vector2(32, 32),
                ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Text = "·",
            };
            var cellIndex = index;
            cell.Pressed += () => SelectOrMoveStashItem(cellIndex);
            stashCells.Add(cell);
            stashGrid.AddChild(cell);
        }

        storeItems.ItemSelected += index => {
            var itemIndex = checked((int)index);
            selectedStoreSlot = itemIndex >= 0 && itemIndex < storeSlots.Count ? storeSlots[itemIndex] : null;
        };
        spellSources.ItemSelected += index => {
            var sourceIndex = checked((int)index);
            if (sourceIndex >= 0 && sourceIndex < spellSourceKeys.Count)
                SpellSourceSelected?.Invoke(spellSourceKeys[sourceIndex]);
        };
        oilTargets.ItemSelected += index => {
            var targetIndex = checked((int)index);
            selectedOilTarget = targetIndex >= 0 && targetIndex < oilTargetReferences.Count
                ? oilTargetReferences[targetIndex]
                : null;
        };
        beltItems.ItemSelected += index => {
            var beltIndex = checked((int)index);
            selectedBeltSlot = beltIndex >= 0 && beltIndex < beltSlots.Count
                ? beltSlots[beltIndex]
                : null;
            selectedEquipmentSlot = null;
            if (selectedBeltSlot is not null) {
                selectedInventoryIndex = -1;
                ClearSelectedStashItem();
                selectedItemReference = ResolveSelectedItemReference(currentPlayer);
            }
        };
        equipmentItems.ItemSelected += index => {
            var equipmentIndex = checked((int)index);
            var equipmentSlot = equipmentIndex >= 0 && equipmentIndex < equipmentSlots.Count
                ? equipmentSlots[equipmentIndex]
                : (uint?)null;
            if (equipmentSlot is not { } slot || currentPlayer is null)
                return;
            if (selectedInventoryIndex >= 0 && selectedInventoryIndex < currentPlayer.Inventory.Count) {
                MoveItemRequested?.Invoke(
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Inventory,
                        Slot = checked((uint)selectedInventoryIndex),
                        StableItemId = currentPlayer.Inventory[selectedInventoryIndex].State?.StableItemId ?? 0,
                    },
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Equipment,
                        Slot = slot,
                    });
                selectedInventoryIndex = -1;
                selectedItemReference = null;
                return;
            }
            selectedEquipmentSlot = slot;
            selectedBeltSlot = null;
            selectedInventoryIndex = -1;
            ClearSelectedStashItem();
            selectedItemReference = ResolveSelectedItemReference(currentPlayer);
        };
        openSmith.Pressed += () => OpenStoreRequested?.Invoke(1);
        openAdria.Pressed += () => OpenStoreRequested?.Invoke(10);
        closeStore.Pressed += () => CloseStoreRequested?.Invoke();
        openStash.Pressed += () => OpenStashRequested?.Invoke();
        closeStash.Pressed += () => CloseStashRequested?.Invoke();
        depositStashGold.Pressed += () => InvokeStashGoldAction(DepositStashGoldRequested);
        withdrawStashGold.Pressed += () => InvokeStashGoldAction(WithdrawStashGoldRequested);
        buySelected.Pressed += BuySelected;
        sellSelected.Pressed += () => InvokeInventoryAction(SellItemRequested);
        repairSelected.Pressed += () => InvokeInventoryAction(RepairItemRequested);
        rechargeSelected.Pressed += () => InvokeInventoryAction(RechargeItemRequested);
        identifySelected.Pressed += () => InvokeInventoryAction(IdentifyItemRequested);
        readBookSelected.Pressed += () => InvokeSelectedItemAction(ReadBookRequested);
        useConsumableSelected.Pressed += () => InvokeSelectedItemAction(UseConsumableRequested);
        applyOilSelected.Pressed += ApplyOil;
        allocateStrength.Pressed += () => AllocateAttributeRequested?.Invoke(PlayerAttribute.Strength);
        allocateMagic.Pressed += () => AllocateAttributeRequested?.Invoke(PlayerAttribute.Magic);
        allocateDexterity.Pressed += () => AllocateAttributeRequested?.Invoke(PlayerAttribute.Dexterity);
        allocateVitality.Pressed += () => AllocateAttributeRequested?.Invoke(PlayerAttribute.Vitality);
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
        questTownerInteraction.Pressed += () => {
            if (availableQuestTownerInteraction is { } interaction)
                QuestTownerInteractionRequested?.Invoke(interaction.QuestId, checked((uint)interaction.TownerId));
        };
        restartTown.Pressed += () => RestartTownRequested?.Invoke();
        refillMana.Pressed += () => RefillManaRequested?.Invoke();
        dropHeld.Pressed += () => DropHeldItemRequested?.Invoke();
        clearEvents.Pressed += () => ClearEventsRequested?.Invoke();
        diagnosticsToggle.Toggled += visible => diagnostics.Visible = visible;
        highContrastToggle.Toggled += _ => ApplyAccessibility();

        status.Text = "Starting...";
        statusEffects.Text = "Statuses: waiting for authoritative snapshot";
        commandFeedback.Text = "Commands are sent to the authoritative server.";
        attributes.Text = "Attributes\nWaiting for authoritative snapshot";
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

    public void SetSpellSources(
        IReadOnlyList<AuthoritativeSpellSource> sources,
        string? selectedSourceKey)
    {
        var rebuild = spellSourceKeys.Count != sources.Count;
        if (!rebuild) {
            for (var index = 0; index < sources.Count; index++) {
                if (!string.Equals(spellSourceKeys[index], sources[index].Key, StringComparison.Ordinal)
                    || !string.Equals(
                        spellSources.GetItemText(index),
                        sources[index].DisplayName,
                        StringComparison.Ordinal)) {
                    rebuild = true;
                    break;
                }
            }
        }
        if (rebuild) {
            spellSources.Clear();
            spellSourceKeys.Clear();
            foreach (var source in sources) {
                spellSources.AddItem(source.DisplayName);
                spellSourceKeys.Add(source.Key);
            }
        }
        var selectedIndex = spellSourceKeys.FindIndex(
            key => string.Equals(key, selectedSourceKey, StringComparison.Ordinal));
        if (selectedIndex >= 0)
            spellSources.Select(selectedIndex, false);
        var selectedSource = selectedIndex >= 0 ? sources[selectedIndex] : null;
        selectedSpellText = selectedSource?.DisplayName ?? "none";
    }

    public void ToggleDiagnostics() => diagnosticsToggle.ButtonPressed = !diagnosticsToggle.ButtonPressed;

    public void SetSessionWarning(string? message)
    {
        sessionWarning.Text = message ?? string.Empty;
        sessionWarning.Visible = !string.IsNullOrEmpty(message);
    }

    public void ToggleHighContrast() => highContrastToggle.ButtonPressed = !highContrastToggle.ButtonPressed;

    public void Apply(AuthoritativeClientModel model, AuthoritativeClient? client, Task? connectionTask)
    {
        clock.Text = ClockPresentation.Format(DateTimeOffset.Now);
        if (client is { IsConnected: false } && model.Snapshot is not null)
            status.Text = "Connection lost - reconnecting. Gameplay input disabled.";
        else if (!string.IsNullOrWhiteSpace(model.LastError))
            status.Text = model.LastError;
        else if (client?.IsConnected == true)
            status.Text = $"Connected - retry {client.RetryTimeout.TotalMilliseconds:0} ms";
        else if (connectionTask is { IsFaulted: true })
            status.Text = connectionTask.Exception?.GetBaseException().Message ?? "Connection failed";

        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null || selectedInventoryIndex >= player.Inventory.Count)
            selectedInventoryIndex = -1;
        if (player is null)
            ClearSelectedStashItem();
        else if (selectedStashStableItemId is { } stableItemId && stableItemId != 0) {
            selectedStashItemIndex = FindStashItemIndex(
                player,
                stableItemId,
                selectedStashItemPage,
                selectedStashItemCell);
            if (selectedStashItemIndex < 0)
                ClearSelectedStashItem();
        } else if (selectedStashItemIndex >= 0) {
            // Positional fallback is for an explicit older-peer selection only.
            // Cleared defaults (page 0/cell 0) must not select a newly stored item.
            selectedStashItemIndex = FindStashItemIndex(
                player,
                selectedStashStableItemId,
                selectedStashItemPage,
                selectedStashItemCell);
            if (selectedStashItemIndex < 0)
                ClearSelectedStashItem();
        }
        if (player is null || selectedBeltSlot is not { } beltSlot || !player.Belt.Any(item => item.Slot == beltSlot))
            selectedBeltSlot = null;
        if (player is null || selectedEquipmentSlot is not { } equipmentSlot || !player.Equipment.Any(item => item.Slot == equipmentSlot))
            selectedEquipmentSlot = null;
        currentPlayer = player;
        selectedItemReference = ResolveSelectedItemReference(player);
        var statusPresentation = player is null
            ? null
            : StatusPresentationStyle.ResolvePlayer(player);
        statusEffects.Text = statusPresentation is null
            ? "Statuses: waiting for authoritative snapshot"
            : $"Statuses: {StatusPresentationStyle.FormatDetailed(statusPresentation.Badges)}";
        state.Text = player is null
            ? "Waiting for authoritative snapshot"
            : $"Tick {model.CurrentTick}   Difficulty {AuthoritativeDifficultyPresentation.Format(model.Snapshot?.Difficulty ?? GameDifficulty.Normal)}   Level {player.LevelId}   Life {player.Life}/{player.LifeMaximum}   Mana {player.Mana}/{player.ManaMaximum}   Spell {selectedSpellText}   Gold {player.Gold}   XP {player.Experience}";
        attributes.Text = FormatAttributes(player);

        var victoryEvent = model.RecentEvents.LastOrDefault(
            @event => @event.EventCase == GameEvent.EventOneofCase.Victory);
        if (model.Snapshot?.VictoryTriggered == true || victoryEvent is not null) {
            victory.Text = victoryEvent is not null
                ? AuthoritativeVictoryPresentation.Format(victoryEvent.Victory)
                : "Victory — Diablo defeated";
            victory.Visible = true;
        } else {
            victory.Visible = false;
        }

        commandFeedback.Text = FormatCommandResult(model.LastCommandResult);
        diagnostics.Text = FormatDiagnostics(model, client);
        inventory.Text = FormatInventory(player);
        ApplyInventoryGrid(player);
        ApplyBeltItems(player);
        ApplyEquipmentItems(player);
        ApplyOilTargets(player);
        ApplyStore(model.Snapshot?.ActiveStore);
        ApplyStash(player);
        dialog.Text = FormatDialog(model.Snapshot, model.RecentEvents);
        availableObjectEntityId = AuthoritativeObjectSelector.SelectFirst(model.Snapshot)?.EntityId;
        availableTrapEntityId = model.ArmedTrapObjects
            .Where(@object => player is not null
                && AuthoritativeLevelVisibility.IsVisible(@object.LevelId, player.LevelId))
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault()
            ?.EntityId;
        availableQuestId = model.Snapshot?.Quests
            .Where(quest => player is not null
                && !quest.Completed
                && !quest.Unavailable
                && AuthoritativeLevelVisibility.IsVisible(quest.LevelId, player.LevelId))
            .OrderBy(quest => quest.QuestId)
            .FirstOrDefault()
            ?.QuestId;
        availableQuestTownerInteraction = AuthoritativeQuestTownerInteractionSelector.Select(model.Snapshot);

        var connected = client?.IsConnected == true;
        restartTown.Visible = player?.Life == 0;
        restartTown.Disabled = !connected || !AuthoritativeDeathRecoveryRules.CanRequestTownRestart(player);
        ApplyRoutes(model.VisibleWarpTriggers, connected && player is { Life: > 0 });
        ApplyDestinations(model.Snapshot, connected && player is { Life: > 0 });
        var hasStatPoints = connected && player?.StatPoints > 0;
        var activeStore = model.Snapshot?.ActiveStore;
        var stashOpen = player?.StashOpen == true;
        var hasInventory = player is not null && player.Inventory.Count > 0;
        var hasSelectedInventory = hasInventory && selectedInventoryIndex >= 0 && selectedInventoryIndex < player!.Inventory.Count;
        var hasSelectedBelt = player is not null && selectedBeltSlot is { } selectedBelt
            && player.Belt.Any(item => item.Slot == selectedBelt);
        var hasSelectedEquipment = player is not null && selectedEquipmentSlot is { } selectedEquipment
            && player.Equipment.Any(item => item.Slot == selectedEquipment);
        var hasSelectedItem = hasSelectedInventory || hasSelectedBelt || hasSelectedEquipment;
        var hasSelectedServiceItem = player is not null
            && selectedItemReference is { } serviceReference
            && AuthoritativeItemActionRules.IsServiceTarget(
                player,
                serviceReference,
                stashOpen);
        var hasIdentifyAbility = player?.AbilityKind == PlayerAbilityKind.Identify;
        var hasIdentifyScroll = player?.Inventory.Any(item => item.State?.ItemActionKind == ItemActionKind.Identify) == true
            || player?.Belt.Any(item => item.State?.ItemActionKind == ItemActionKind.Identify) == true;
        var hasRepairAbility = player?.AbilityKind == PlayerAbilityKind.Repair;
        var hasRechargeAbility = player?.AbilityKind == PlayerAbilityKind.Recharge;
        var hasTrapDisarmAbility = player?.AbilityKind == PlayerAbilityKind.TrapDisarm;
        var serviceAvailability = AuthoritativeStoreServicePresentation.Evaluate(
            connected,
            activeStore,
            hasSelectedServiceItem,
            hasRepairAbility,
            hasRechargeAbility,
            hasIdentifyAbility,
            hasIdentifyScroll);
        var quoteTargetIsCurrent = connected && hasSelectedServiceItem;
        var repairQuote = AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            activeStore, selectedItemReference, StoreServiceQuoteAction.Repair,
            quoteTargetIsCurrent, serviceAvailability.CanRepair);
        var rechargeQuote = AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            activeStore, selectedItemReference, StoreServiceQuoteAction.Recharge,
            quoteTargetIsCurrent, serviceAvailability.CanRecharge);
        var identifyQuote = AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            activeStore, selectedItemReference, StoreServiceQuoteAction.Identify,
            quoteTargetIsCurrent, serviceAvailability.CanIdentify);
        var refillManaQuote = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            activeStore, connected, serviceAvailability.CanRefillMana);
        var selectedItemAction = hasSelectedInventory
            ? player!.Inventory[selectedInventoryIndex].State?.ItemActionKind ?? ItemActionKind.Unspecified
            : hasSelectedBelt
                ? player!.Belt.First(item => item.Slot == selectedBeltSlot).State?.ItemActionKind ?? ItemActionKind.Unspecified
                : ItemActionKind.Unspecified;
        var hasTargetlessConsumable = hasSelectedItem
            && AuthoritativeItemActionRules.IsTargetlessConsumable(selectedItemAction);
        var hasTargetedConsumable = hasSelectedItem
            && AuthoritativeItemActionRules.IsTargetedConsumable(selectedItemAction);
        openSmith.Disabled = !connected || stashOpen;
        openAdria.Disabled = !connected || stashOpen;
        closeStore.Disabled = !connected || activeStore is null;
        openStash.Disabled = !connected || stashOpen || activeStore is not null;
        closeStash.Disabled = !connected || !stashOpen;
        stashGoldAmount.Editable = connected && stashOpen;
        stashPageSelector.Disabled = !connected;
        depositStashGold.Disabled = !connected || !stashOpen;
        withdrawStashGold.Disabled = !connected || !stashOpen;
        allocateStrength.Disabled = !hasStatPoints;
        allocateMagic.Disabled = !hasStatPoints;
        allocateDexterity.Disabled = !hasStatPoints;
        allocateVitality.Disabled = !hasStatPoints;
        buySelected.Disabled = !connected || activeStore is null || selectedStoreSlot is null;
        sellSelected.Disabled = !connected || !hasSelectedItem;
        repairSelected.Text = repairQuote.ButtonText ?? (activeStore is null && hasRepairAbility
            ? "Repair selected (Warrior)"
            : "Repair selected");
        repairSelected.Disabled = !repairQuote.CanInvoke;
        rechargeSelected.Text = rechargeQuote.ButtonText ?? (activeStore is null && hasRechargeAbility
            ? "Recharge selected (Sorcerer)"
            : "Recharge selected");
        rechargeSelected.Disabled = !rechargeQuote.CanInvoke;
        identifySelected.Text = identifyQuote.ButtonText ?? (activeStore is null && hasIdentifyAbility
            ? "Identify selected (Bard)"
            : activeStore is null && hasIdentifyScroll
                ? "Identify selected (Scroll)"
                : "Identify selected");
        identifySelected.Disabled = !identifyQuote.CanInvoke;
        readBookSelected.Disabled = !connected || selectedItemAction != ItemActionKind.ReadBook;
        useConsumableSelected.Disabled = !connected || !hasTargetlessConsumable;
        applyOilSelected.Disabled = !connected || !hasTargetedConsumable || selectedOilTarget is null;
        disarmTrap.Disabled = !connected || !hasTrapDisarmAbility || availableTrapEntityId is null;
        interactObject.Disabled = !connected || availableObjectEntityId is null;
        advanceQuest.Disabled = !connected || availableQuestId is null;
        questTownerInteraction.Disabled = !connected || availableQuestTownerInteraction is null;
        questTownerInteraction.Text = availableQuestTownerInteraction is null
            ? "Use quest/towner"
            : $"Use quest/towner ({availableQuestTownerInteraction.TownerId})";
        refillMana.Text = refillManaQuote.ButtonText ?? "Refill mana";
        refillMana.Disabled = !refillManaQuote.CanInvoke;
        dropHeld.Disabled = !connected || player?.HeldItem is null;
        clearEvents.Disabled = model.RecentEvents.Count == 0;
        beltItems.MouseFilter = !connected || player?.Belt.Count is not > 0
            ? Control.MouseFilterEnum.Ignore
            : Control.MouseFilterEnum.Stop;
        equipmentItems.MouseFilter = !connected || player is null
            ? Control.MouseFilterEnum.Ignore
            : Control.MouseFilterEnum.Stop;
        spellSources.MouseFilter = !connected || spellSourceKeys.Count == 0
            ? Control.MouseFilterEnum.Ignore
            : Control.MouseFilterEnum.Stop;
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

    private void ApplyStash(PlayerSnapshot? player)
    {
        var layout = StashLayout.Build(player?.Stash, stashPage);
        for (var cellIndex = 0; cellIndex < stashCells.Count; cellIndex++) {
            var occupant = layout.Occupants[cellIndex];
            var anchor = occupant >= 0 && occupant < layout.Anchors.Count
                ? layout.Anchors[occupant]
                : -1;
            var cell = stashCells[cellIndex];
            cell.Text = occupant < 0
                ? "·"
                : anchor == cellIndex
                    ? occupant == selectedStashItemIndex ? $"[{occupant + 1}]" : $"{occupant + 1}"
                    : "·";
            cell.TooltipText = occupant >= 0
                && player is not null
                && occupant < player.Stash.Count
                ? DescribeItem(player.Stash[occupant].Item?.State)
                : "Empty stash cell. Select an item, then choose a destination.";
            cell.Disabled = player is null || !player.StashOpen;
        }

        stashItems.Clear();
        if (player is null) {
            stash.Text = "No authoritative stash projection";
            return;
        }

        foreach (var item in player.Stash.OrderBy(item => item.Page).ThenBy(item => item.Cell)) {
            stashItems.AddItem(
                $"page {item.Page}, cell {item.Cell}: {DescribeItem(item.Item?.State)}");
        }

        var occupied = layout.Anchors.Count(cell => cell >= 0);
        var selected = selectedStashItemIndex >= 0 && selectedStashItemIndex < player.Stash.Count
            ? $"   Selected {selectedStashItemIndex + 1}"
            : string.Empty;
        stash.Text = $"{(player.StashOpen ? "Open" : "Closed")}   Gold {player.StashGold}   Items {player.Stash.Count}   Page {stashPage} occupied {occupied}{selected}";
    }

    private void InvokeStashGoldAction(Action<uint>? action)
    {
        if (action is null || !uint.TryParse(stashGoldAmount.Text, out var amount) || amount == 0)
            return;
        action(amount);
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

    private void ApplyBeltItems(PlayerSnapshot? player)
    {
        beltItems.Clear();
        beltSlots.Clear();
        if (player is null)
        {
            selectedBeltSlot = null;
            return;
        }

        foreach (var item in player.Belt.OrderBy(item => item.Slot))
        {
            beltSlots.Add(item.Slot);
            beltItems.AddItem($"slot {item.Slot}: {DescribeItem(item.State)}");
        }
        if (selectedBeltSlot is { } beltSlot)
        {
            var selectedIndex = beltSlots.IndexOf(beltSlot);
            if (selectedIndex < 0)
                selectedBeltSlot = null;
            else
                beltItems.Select(selectedIndex, false);
        }
    }

    private void ApplyEquipmentItems(PlayerSnapshot? player)
    {
        equipmentItems.Clear();
        equipmentSlots.Clear();
        if (player is null) {
            selectedEquipmentSlot = null;
            return;
        }

        // Empty destinations must remain selectable, including after unequipping
        // the last item. The server still validates slot/type/attribute rules.
        string[] labels = ["Head", "Left ring", "Right ring", "Amulet", "Left hand", "Right hand", "Body"];
        for (var slot = 0U; slot < labels.Length; slot++) {
            var item = player.Equipment.FirstOrDefault(candidate => candidate.Slot == slot);
            equipmentSlots.Add(slot);
            equipmentItems.AddItem($"{labels[slot]}: {(item is null ? "empty" : DescribeItem(item.State))}");
        }
        if (selectedEquipmentSlot is { } equipmentSlot) {
            var selectedIndex = equipmentSlots.IndexOf(equipmentSlot);
            if (selectedIndex < 0)
                selectedEquipmentSlot = null;
            else
                equipmentItems.Select(selectedIndex, false);
        }
    }

    private void ApplyOilTargets(PlayerSnapshot? player)
    {
        var previousTarget = selectedOilTarget;
        oilTargetReferences.Clear();
        oilTargets.Clear();
        if (player is null) {
            selectedOilTarget = null;
            return;
        }

        foreach (var item in player.Equipment.OrderBy(item => item.Slot)) {
            oilTargetReferences.Add(new PlayerItemReference {
                Location = PlayerItemLocation.Equipment,
                Slot = item.Slot,
                StableItemId = item.State?.StableItemId ?? 0,
            });
            oilTargets.AddItem($"equipment {item.Slot}: {DescribeItem(item.State)}");
        }
        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            oilTargetReferences.Add(new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = checked((uint)index),
                StableItemId = item.State?.StableItemId ?? 0,
            });
            oilTargets.AddItem($"inventory {index + 1}: {DescribeItem(item.State)}");
        }

        var selectedIndex = previousTarget is null
            ? -1
            : oilTargetReferences.FindIndex(reference =>
                AuthoritativeItemActionRules.AreSameReference(reference, previousTarget));
        if (selectedIndex < 0) {
            selectedOilTarget = null;
            return;
        }
        selectedOilTarget = oilTargetReferences[selectedIndex];
        oilTargets.Select(selectedIndex, false);
    }

    private void SelectOrMoveInventoryItem(int cellIndex)
    {
        if (selectedStashItemIndex >= 0 && currentPlayer is not null) {
            if (ResolveSelectedItemReference(currentPlayer) is { } stashItem) {
                MoveItemRequested?.Invoke(
                    stashItem,
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Inventory,
                        Slot = checked((uint)cellIndex),
                    });
                ClearSelectedItem();
            }
            return;
        }

        var occupant = inventoryLayout.Occupants[cellIndex];
        if (occupant >= 0) {
            selectedInventoryIndex = occupant;
            selectedEquipmentSlot = null;
            selectedBeltSlot = null;
            ClearSelectedStashItem();
            selectedItemReference = ResolveSelectedItemReference(currentPlayer);
            return;
        }
        if (selectedEquipmentSlot is { } equipmentSlot && currentPlayer is not null) {
            var equipped = currentPlayer.Equipment.FirstOrDefault(item => item.Slot == equipmentSlot);
            if (equipped is not null) {
                MoveItemRequested?.Invoke(
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Equipment,
                        Slot = equipmentSlot,
                        StableItemId = equipped.State?.StableItemId ?? 0,
                    },
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Inventory,
                        Slot = checked((uint)cellIndex),
                    });
                selectedEquipmentSlot = null;
                selectedItemReference = null;
            }
            return;
        }
        if (selectedInventoryIndex >= 0)
            MoveInventoryItemRequested?.Invoke(checked((uint)selectedInventoryIndex), checked((uint)cellIndex));
    }

    private void SelectOrMoveStashItem(int cellIndex)
    {
        var layout = StashLayout.Build(currentPlayer?.Stash, stashPage);
        var occupant = layout.GetItemIndexAt(checked((uint)cellIndex));
        if (selectedItemReference is { } source && source.Location != PlayerItemLocation.Stash) {
            MoveItemRequested?.Invoke(
                source,
                new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = stashPage,
                    Slot = checked((uint)cellIndex),
                });
            ClearSelectedItem();
            return;
        }

        if (occupant >= 0 && currentPlayer is not null && occupant < currentPlayer.Stash.Count) {
            if (selectedStashItemIndex >= 0 && occupant != selectedStashItemIndex
                && ResolveSelectedItemReference(currentPlayer) is { } stashItem) {
                MoveItemRequested?.Invoke(
                    stashItem,
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Stash,
                        Page = stashPage,
                        Slot = checked((uint)cellIndex),
                    });
                ClearSelectedItem();
                return;
            }
            selectedStashItemIndex = occupant;
            var selectedStashItem = currentPlayer.Stash[occupant];
            var stableItemId = selectedStashItem.Item?.State?.StableItemId ?? 0;
            selectedStashStableItemId = stableItemId == 0 ? null : stableItemId;
            selectedStashItemPage = selectedStashItem.Page;
            selectedStashItemCell = selectedStashItem.Cell;
            selectedInventoryIndex = -1;
            selectedEquipmentSlot = null;
            selectedBeltSlot = null;
            selectedItemReference = ResolveSelectedItemReference(currentPlayer);
            ApplyStash(currentPlayer);
            return;
        }

        if (selectedStashItemIndex >= 0 && currentPlayer is not null
            && ResolveSelectedItemReference(currentPlayer) is { } sourceStashItem) {
            MoveItemRequested?.Invoke(
                sourceStashItem,
                new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = stashPage,
                    Slot = checked((uint)cellIndex),
                });
            ClearSelectedItem();
        }
    }

    private void ClearSelectedStashItem()
    {
        selectedStashItemIndex = -1;
        selectedStashStableItemId = null;
        selectedStashItemPage = 0;
        selectedStashItemCell = 0;
    }

    private void ClearSelectedItem()
    {
        selectedInventoryIndex = -1;
        selectedBeltSlot = null;
        selectedEquipmentSlot = null;
        ClearSelectedStashItem();
        selectedItemReference = null;
    }

    private static int FindStashItemIndex(
        PlayerSnapshot player,
        uint? stableItemId,
        uint page,
        uint cell)
    {
        for (var index = 0; index < player.Stash.Count; index++)
            if (stableItemId is { } stable
                ? player.Stash[index].Item?.State?.StableItemId == stable
                : player.Stash[index].Page == page && player.Stash[index].Cell == cell)
                return index;
        return -1;
    }

    private void BuySelected()
    {
        if (activeStoreId != 0 && selectedStoreSlot is { } slot)
            PurchaseRequested?.Invoke(activeStoreId, slot);
    }

    private void ApplyOil()
    {
        var source = ResolveSelectedItemReference();
        if (source is null || selectedOilTarget is null)
            return;
        ApplyOilRequested?.Invoke(
            source,
            selectedOilTarget);
    }

    private void InvokeInventoryAction(Action<PlayerItemReference>? action)
    {
        if (selectedItemReference is { } item)
            action?.Invoke(item);
    }

    private void InvokeSelectedItemAction(Action<PlayerItemReference>? action)
    {
        if (ResolveSelectedItemReference() is { } reference)
            action?.Invoke(reference);
    }

    private PlayerItemReference? ResolveSelectedItemReference()
    {
        return selectedItemReference;
    }

    private PlayerItemReference? ResolveSelectedItemReference(PlayerSnapshot? player)
    {
        if (player is null)
            return null;
        if (selectedInventoryIndex >= 0 && selectedInventoryIndex < player.Inventory.Count)
            return new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = checked((uint)selectedInventoryIndex),
                StableItemId = player.Inventory[selectedInventoryIndex].State?.StableItemId ?? 0,
            };
        if (selectedStashItemIndex >= 0 && selectedStashItemIndex < player.Stash.Count) {
            var stashItem = player.Stash[selectedStashItemIndex];
            return new PlayerItemReference {
                Location = PlayerItemLocation.Stash,
                Page = stashItem.Page,
                Slot = stashItem.Cell,
                StableItemId = stashItem.Item?.State?.StableItemId ?? 0,
            };
        }
        if (selectedBeltSlot is { } beltSlot
            && player.Belt.FirstOrDefault(item => item.Slot == beltSlot) is { } beltItem)
            return new PlayerItemReference {
                Location = PlayerItemLocation.Belt,
                Slot = beltSlot,
                StableItemId = beltItem.State?.StableItemId ?? 0,
            };
        if (selectedEquipmentSlot is { } equipmentSlot
            && player.Equipment.FirstOrDefault(item => item.Slot == equipmentSlot) is { } equipmentItem)
            return new PlayerItemReference {
                Location = PlayerItemLocation.Equipment,
                Slot = equipmentSlot,
                StableItemId = equipmentItem.State?.StableItemId ?? 0,
            };
        return null;
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
                ? $"Inventory\n(empty)\nBelt ({player.Belt.Count})\nEquipment ({player.Equipment.Count})\nClick an occupied cell after an item arrives."
                : $"Inventory\n(empty)\nBelt ({player.Belt.Count})\nEquipment ({player.Equipment.Count})\nHeld: {DescribeItem(player.HeldItem.State)}";
        var held = player.HeldItem is null
            ? string.Empty
            : $"\nHeld: {DescribeItem(player.HeldItem.State)}";
        return $"Inventory ({player.Inventory.Count})\nBelt ({player.Belt.Count})\nEquipment ({player.Equipment.Count})\nClick an item to select it; click an empty cell to request a move.{held}";
    }

    private static string FormatAttributes(PlayerSnapshot? player)
    {
        if (player is null)
            return "Attributes\nWaiting for authoritative snapshot";
        var attributes = player.Attributes;
        return $"Attributes (points: {player.StatPoints})\n"
            + $"Strength {attributes?.Strength?.Base ?? 0}  Magic {attributes?.Magic?.Base ?? 0}\n"
            + $"Dexterity {attributes?.Dexterity?.Base ?? 0}  Vitality {attributes?.Vitality?.Base ?? 0}";
    }

    private static string FormatStoreItem(StoreItemSnapshot item)
    {
        return $"slot {item.StoreSlot}  {DescribeItem(item.State)}  price {item.Price}";
    }

    private static string DescribeItem(ItemStateSnapshot? state)
    {
        if (state is null)
            return "unknown item";
        var name = ItemPresentationStyle.ResolveName(state);
        if (!string.IsNullOrEmpty(name))
            return $"{name}, value {state.Value}";
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
            GameEvent.EventOneofCase.Dialogue => AuthoritativeDialoguePresentation.Format(@event.Dialogue),
            GameEvent.EventOneofCase.MapOpened => "Cathedral Map opened",
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
        return AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result)
            ? $"Last command accepted at tick {result.AppliedTick}."
            : $"Last command: {result.Status} ({result.RejectReason}).";
    }

    private void ApplyAccessibility()
    {
        var color = highContrastToggle.ButtonPressed ? Colors.White : new Color("d5d5e5");
        var muted = highContrastToggle.ButtonPressed ? Colors.White : new Color("b2b2c5");
        foreach (var control in new Control[] { status, state, statusEffects, victory, inventory, store, dialog, commandFeedback, diagnostics }) {
            control.Modulate = control == diagnostics ? muted : color;
            control.AddThemeFontSizeOverride("font_size", highContrastToggle.ButtonPressed ? 18 : 14);
        }
    }

    private static string FormatDiagnostics(AuthoritativeClientModel model, AuthoritativeClient? client)
    {
        var snapshot = model.Snapshot;
        var localPlayer = snapshot?.Players.FirstOrDefault();
        var serverIdentity = client?.ServerIdentity;
        var playerIdentity = string.IsNullOrEmpty(localPlayer?.CharacterName)
            ? $"Player {model.PlayerEntityId}"
            : $"{localPlayer.CharacterName} ({model.PlayerEntityId})";
        var identity = serverIdentity is null
            ? "Server identity unavailable"
            : $"Server {serverIdentity.BuildId}  ruleset {FormatHash(serverIdentity.RulesetIdentityHash)}  content {FormatHash(serverIdentity.ContentManifestHash)}";
        return identity + $"\nTick {model.CurrentTick}  pending {client?.PendingCommandCount ?? 0}  RTT {client?.RetryTimeout.TotalMilliseconds ?? 0:0} ms\n"
            + $"{playerIdentity}  entities: {snapshot?.Monsters.Count ?? 0} monsters, {snapshot?.WorldItems.Count ?? 0} items, {snapshot?.Projectiles.Count ?? 0} projectiles\n"
            + $"Prediction {(model.IsCorrectingPosition ? "active" : "settled")}  events {model.RecentEvents.Count}";
    }

    private static string FormatHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "none";
        return value.Length <= 12 ? value : value[..12];
    }
}
