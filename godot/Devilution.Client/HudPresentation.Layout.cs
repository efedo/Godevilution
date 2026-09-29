using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Responsive controls only; all actions still use the existing server intents. */
public partial class HudPresentation
{
    private readonly PanelContainer statusPanel = new() { Name = "StatusPanel" };
    private readonly TabContainer sections = new() { Name = "HudSections" };
    private readonly OptionButton routes = new() { Name = "Routes", ClipText = true };
    private readonly Button travel = new() { Name = "Travel", Text = "Travel to route" };
    private readonly Button cancelTravel = new() { Name = "CancelTravel", Text = "Cancel travel", Disabled = true };
    private readonly Label travelStatus = new() { Name = "TravelStatus" };
    private WarpTriggerSnapshot[] displayedRoutes = [];

    public event Action<uint>? TravelRequested;
    public event Action? CancelTravelRequested;
    public Rect2 PlayArea { get; private set; }

    private void BuildLayout()
    {
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
        AddList(hero, "Spell sources (select here, or use Q/E)", spellSources, 140);
        hero.AddChild(highContrastToggle);

        var items = AddPage("Items");
        items.AddChild(inventory);
        items.AddChild(inventoryGrid);
        readBookSelected.Text = "Read book";
        useConsumableSelected.Text = "Use item";
        items.AddChild(Buttons(readBookSelected, useConsumableSelected, dropHeld));
        AddList(items, "Belt: select a book or consumable source", beltItems, 80);
        AddList(items, "Equipment: select an item, or an inventory item's destination", equipmentItems, 120);
        AddList(items, "Oil targets: select equipment or inventory", oilTargets, 100);
        items.AddChild(applyOilSelected);

        var trade = AddPage("Trade");
        trade.AddChild(Buttons(openSmith, openAdria, closeStore, buySelected));
        AddList(trade, "Store stock", storeItems, 160);
        trade.AddChild(store);
        trade.AddChild(Caption("Select an item in Items or Stash before requesting a service."));
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
        AddList(storage, "Stored items", stashItems, 100);
        storage.AddChild(stash);
        storage.AddChild(stashGoldAmount);
        depositStashGold.Text = "Deposit gold";
        withdrawStashGold.Text = "Withdraw gold";
        storage.AddChild(Buttons(depositStashGold, withdrawStashGold));

        var interactions = AddPage("World");
        interactions.AddChild(Caption("Routes supplied by the server"));
        interactions.AddChild(routes);
        interactions.AddChild(Buttons(travel, cancelTravel));
        interactions.AddChild(travelStatus);
        travel.Pressed += () => {
            if (routes.Selected >= 0 && routes.Selected < displayedRoutes.Length)
                TravelRequested?.Invoke(displayedRoutes[routes.Selected].WarpId);
        };
        cancelTravel.Pressed += () => CancelTravelRequested?.Invoke();
        interactions.AddChild(Caption("WASD / arrows: walk. Ctrl + left click: walk toward a tile. Left click: interact or cast. Right click: attack. Movement, world clicks, or Escape cancel travel."));
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
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(box);
        return box;
    }

    private static Label Caption(string text)
        => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };

    private static void AddList(VBoxContainer box, string caption, ItemList list, int height)
    {
        box.AddChild(Caption(caption));
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
}
