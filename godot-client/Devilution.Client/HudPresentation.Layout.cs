using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Responsive controls only; all actions still use the existing server intents. */
public partial class HudPresentation
{
    private readonly AssetPalette hudAssets = new();
    private readonly PanelContainer statusPanel = new() { Name = "StatusPanel" };
    private readonly HBoxContainer combatBeltQuickUseRow = new() { Name = "CombatBeltQuickUse" };
    private readonly TabContainer sections = new() { Name = "HudSections" };
    private readonly OptionButton routes = new() { Name = "Routes", ClipText = true };
    private readonly Button travel = new() { Name = "Travel", Text = "Travel to route" };
    private readonly Button cancelTravel = new() { Name = "CancelTravel", Text = "Cancel travel", Disabled = true };
    private readonly Label travelStatus = new() { Name = "TravelStatus" };
    private WarpTriggerSnapshot[] displayedRoutes = [];
    private readonly OptionButton destinations = new() { Name = "WorldDestinations", ClipText = true };
    private readonly Button approachDestination = new() { Name = "ApproachDestination", Text = "Approach and interact" };
    private AuthoritativeWorldDestination[] displayedDestinations = [];

    public event Action<uint>? TravelRequested;
    public event Action? CancelTravelRequested;
    public event Action<AuthoritativeWorldDestination>? DestinationRequested;
    public Rect2 PlayArea { get; private set; }

    private void BuildLayout()
    {
        travel.Text = hudAssets.Localize("ui.hud.travel", "Travel to route");
        cancelTravel.Text = hudAssets.Localize("ui.hud.cancel-travel", "Cancel travel");
        approachDestination.Text = hudAssets.Localize("ui.hud.approach", "Approach and interact");
        foreach (var label in new[] { status, sessionWarning, state, statusEffects, attributes,
                     inventory, store, stash, dialog, commandFeedback, diagnostics, travelStatus }) {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.AddThemeFontSizeOverride("font_size", 14);
        }
        var statusBox = new VBoxContainer();
        statusPanel.AddChild(statusBox);
        var heading = new HBoxContainer();
        status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(status);
        clock.HorizontalAlignment = HorizontalAlignment.Right;
        clock.MouseFilter = Control.MouseFilterEnum.Ignore;
        heading.AddChild(clock);
        statusBox.AddChild(heading);
        foreach (var label in new[] { sessionWarning, state, statusEffects, commandFeedback })
            statusBox.AddChild(label);
        var beltCaption = Caption("ui.hud.quick-belt", "Quick use belt (keys 1–8)");
        beltCaption.AutowrapMode = TextServer.AutowrapMode.Off;
        beltCaption.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        combatBeltQuickUseRow.AddChild(beltCaption);
        foreach (var button in combatBeltQuickUseButtons)
            combatBeltQuickUseRow.AddChild(button);
        statusBox.AddChild(combatBeltQuickUseRow);
        AddChild(statusPanel);

        victory.HorizontalAlignment = HorizontalAlignment.Center;
        victory.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        victory.Modulate = new Color("ffe38a");
        victory.AddThemeFontSizeOverride("font_size", 28);
        victory.MouseFilter = Control.MouseFilterEnum.Ignore;
        victory.Visible = false;
        AddChild(victory);

        sidePanel = new PanelContainer { Name = "HudPanel" };
        sidePanel.AddChild(sections);
        AddChild(sidePanel);
        var hero = AddPage("Hero");
        hero.AddChild(attributes);
        hero.AddChild(Buttons(allocateStrength, allocateMagic, allocateDexterity, allocateVitality));
        AddList(hero, "ui.hud.spell-sources", "Spell sources (select here, or use Q/E)", spellSources, 140);
        hero.AddChild(restartTown);
        hero.AddChild(returnToSessionMenu);
        hero.AddChild(highContrastToggle);

        var items = AddPage("Items");
        items.AddChild(inventory);
        items.AddChild(inventoryGrid);
        readBookSelected.Text = "Read book";
        useConsumableSelected.Text = "Use item";
        items.AddChild(Buttons(readBookSelected, useConsumableSelected, dropHeld));
        AddList(items, "ui.hud.belt", "Belt: select a book or consumable source", beltItems, 80);
        AddList(items, "ui.hud.equipment", "Equipment: select an item, or an inventory item's destination", equipmentItems, 200);
        AddList(items, "ui.hud.oil-targets", "Oil targets: select equipment or inventory", oilTargets, 100);
        items.AddChild(applyOilSelected);

        var trade = AddPage("Trade");
        trade.AddChild(Buttons(openSmith, openAdria, openPepin, closeStore, buySelected));
        AddList(trade, "ui.hud.store-stock", "Store stock", storeItems, 160);
        trade.AddChild(store);
        trade.AddChild(Caption("ui.hud.service-help",
            $"Select an item in {hudAssets.Localize("ui.hud.tab.items", "Items")} or {hudAssets.Localize("ui.hud.tab.stash", "Stash")} before requesting a service."));
        trade.AddChild(Buttons(sellSelected, repairSelected, rechargeSelected, identifySelected, refillMana));

        var storage = AddPage("Stash");
        storage.AddChild(Buttons(openStash, closeStash));
        for (var page = 0U; page < StashLayout.PageCount; page++)
            stashPageSelector.AddItem($"Page {page}");
        stashPageSelector.ItemSelected += index => {
            stashPage = checked((uint)index);
            ApplyStash(currentPlayer);
        };
        storage.AddChild(stashPageSelector);
        storage.AddChild(stashGrid);
        AddList(storage, "ui.hud.stored-items", "Stored items", stashItems, 100);
        storage.AddChild(stash);
        storage.AddChild(stashGoldAmount);
        depositStashGold.Text = "Deposit gold";
        withdrawStashGold.Text = "Withdraw gold";
        storage.AddChild(Buttons(depositStashGold, withdrawStashGold));

        var interactions = AddPage("World");
        interactions.AddChild(Caption("ui.hud.routes", "Routes supplied by the server", "RouteCaption"));
        interactions.AddChild(routes);
        interactions.AddChild(Buttons(travel, cancelTravel));
        interactions.AddChild(travelStatus);
        travel.Pressed += () => {
            if (routes.Selected >= 0 && routes.Selected < displayedRoutes.Length)
                TravelRequested?.Invoke(displayedRoutes[routes.Selected].WarpId);
        };
        cancelTravel.Pressed += () => CancelTravelRequested?.Invoke();
        interactions.AddChild(Caption("ui.hud.destinations", "Declared towners and portal endpoints", "DestinationCaption"));
        interactions.AddChild(destinations);
        interactions.AddChild(Buttons(approachDestination));
        approachDestination.Pressed += () => {
            if (destinations.Selected >= 0 && destinations.Selected < displayedDestinations.Length)
                DestinationRequested?.Invoke(displayedDestinations[destinations.Selected]);
        };
        interactions.AddChild(Caption("ui.hud.world-help",
            $"WASD / arrows: walk. Ctrl + left click: walk toward a tile. Left click: interact or cast. Right click: attack. Movement, world clicks, or Escape cancel travel. After death settles, Escape or {hudAssets.Localize("ui.hud.tab.hero", "Hero")} / Restart in Town requests server recovery.",
            "WorldHelpCaption"));
        interactions.AddChild(Buttons(interactObject, disarmTrap, advanceQuest, questTownerInteraction, clearEvents));
        interactions.AddChild(dialog);
        interactions.AddChild(diagnosticsToggle);
        interactions.AddChild(diagnostics);
        UpdateLayout();
    }

    private VBoxContainer AddPage(string title)
    {
        var scroll = new ScrollContainer {
            Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        sections.AddChild(scroll);
        // Display labels may localize; control names remain stable for input and accessibility.
        sections.SetTabTitle(sections.GetTabCount() - 1,
            hudAssets.Localize("ui.hud.tab." + title.ToLowerInvariant(), title));
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(box);
        return box;
    }

    private Label Caption(string key, string fallback, string? name = null)
    {
        var caption = new Label {
            Text = hudAssets.Localize(key, fallback),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        if (name is not null)
            caption.Name = name;
        return caption;
    }

    private void AddList(VBoxContainer box, string key, string caption, ItemList list, int height)
    {
        box.AddChild(Caption(key, caption));
        list.CustomMinimumSize = new Vector2(0, height);
        box.AddChild(list);
    }

    private static GridContainer Buttons(params Button[] buttons)
    {
        var grid = new GridContainer { Columns = 2 };
        foreach (var button in buttons) {
            button.TooltipText = button.Text;
            button.ClipText = true;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(button);
        }
        return grid;
    }

    /** Called before world rendering/input; container minimums include wrapped text. */
    public void UpdateLayout()
    {
        if (sidePanel is null)
            return;
        var size = GetViewport().GetVisibleRect().Size;
        var sideWidth = Mathf.Clamp(size.X * 0.34f, 400, 480);
        sidePanel.Position = new Vector2(size.X - sideWidth - 12, 12);
        sidePanel.Size = new Vector2(sideWidth, Math.Max(1, size.Y - 24));
        statusPanel.Position = new Vector2(12, 12);
        statusPanel.Size = new Vector2(Math.Max(1, sidePanel.Position.X - 24),
            Math.Max(96, statusPanel.GetCombinedMinimumSize().Y));
        var top = statusPanel.GetRect().End.Y + 12;
        PlayArea = new Rect2(12, top, statusPanel.Size.X, Math.Max(1, size.Y - top - 12));
        victory.Position = PlayArea.Position + new Vector2(0, 12);
        victory.Size = new Vector2(PlayArea.Size.X, 80);
    }

    private void ApplyRoutes(IReadOnlyList<WarpTriggerSnapshot> next, bool enabled)
    {
        var ordered = next.OrderBy(route => route.WarpId).ToArray();
        if (!displayedRoutes.SequenceEqual(ordered)) {
            var selectedId = routes.Selected >= 0 && routes.Selected < displayedRoutes.Length
                ? displayedRoutes[routes.Selected].WarpId : (uint?)null;
            displayedRoutes = ordered.Select(route => route.Clone()).ToArray();
            routes.Clear();
            foreach (var route in displayedRoutes)
                routes.AddItem($"{route.StableSymbol.Replace('_', ' ')} — level {route.TargetLevelId}{(route.Available ? "" : " (unavailable)")}");
            var selected = Array.FindIndex(displayedRoutes, route => route.WarpId == selectedId);
            if (displayedRoutes.Length > 0)
                routes.Select(Math.Max(0, selected));
        }
        routes.Disabled = !enabled || displayedRoutes.Length == 0;
        travel.Disabled = !enabled || routes.Selected < 0 || routes.Selected >= displayedRoutes.Length
            || !displayedRoutes[routes.Selected].Available;
    }

    public void SetTravelStatus(bool active, string message)
    {
        cancelTravel.Disabled = !active;
        travelStatus.Text = message;
    }

    private void ApplyDestinations(Snapshot? snapshot, bool enabled)
    {
        var next = AuthoritativeWorldDestinationSelector.GetDestinations(snapshot).ToArray();
        if (!displayedDestinations.SequenceEqual(next)) {
            var selectedKey = destinations.Selected >= 0 && destinations.Selected < displayedDestinations.Length
                ? displayedDestinations[destinations.Selected].Key : null;
            displayedDestinations = next;
            destinations.Clear();
            foreach (var destination in next)
                destinations.AddItem(destination.Label);
            var selected = Array.FindIndex(next, destination => destination.Key == selectedKey);
            if (next.Length > 0)
                destinations.Select(Math.Max(0, selected));
        }
        destinations.Disabled = !enabled || next.Length == 0;
        approachDestination.Disabled = !enabled || destinations.Selected < 0 || destinations.Selected >= next.Length;
    }
}
