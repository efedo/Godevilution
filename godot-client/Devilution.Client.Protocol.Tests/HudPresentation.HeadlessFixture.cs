using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

// Only the controls read by the linked production scheduling partial are stubbed; no Godot runtime is loaded.
namespace Godot
{
    public readonly record struct Vector2(float X, float Y);
}

namespace Devilution.Client
{
    public partial class HudPresentation
    {
        private readonly AuthoritativeClientModel headlessModel = new();
        private readonly HeadlessControl sections = new(), diagnosticsToggle = new(), routes = new(), destinations = new();
        private readonly HeadlessControl clock = new(), inventory = new(), attributes = new(), statusEffects = new();
        private int selectedInventoryIndex = -1, selectedStashItemIndex = -1;
        private uint? selectedBeltSlot = null, selectedEquipmentSlot = null, selectedStoreSlot = null;
        private uint stashPage = 0;
        private PlayerItemReference? selectedOilTarget = null;
        private string selectedSpellText = "none";
        private bool sessionMenuTransition = false;
        private PlayerSnapshot? currentPlayer;
        private PlayerItemReference? selectedItemReference;
        internal PlayerItemReference? HeadlessSelectedItem => selectedItemReference;
        internal PlayerItemReference? HeadlessSelectedOilTarget => selectedOilTarget;
        internal uint? HeadlessSelectedStoreSlot => selectedStoreSlot;
        internal bool HeadlessItemSelectionIndicesCleared => selectedInventoryIndex == -1 && selectedStashItemIndex == -1
            && selectedBeltSlot is null && selectedEquipmentSlot is null;
        internal void ApplyHeadlessItemSelection(PlayerSnapshot? player, StoreSnapshot? store = null)
        {
            ReconcileItemSelections(player, store);
            currentPlayer = player;
        }
        internal void SelectHeadlessItem(PlayerSnapshot player, PlayerItemReference reference)
        {
            currentPlayer = player;
            selectedItemReference = reference;
            selectedInventoryIndex = reference.Location == PlayerItemLocation.Inventory ? (int)reference.Slot : -1;
            selectedBeltSlot = reference.Location == PlayerItemLocation.Belt ? reference.Slot : null;
            selectedEquipmentSlot = reference.Location == PlayerItemLocation.Equipment ? reference.Slot : null;
            selectedStashItemIndex = reference.Location == PlayerItemLocation.Stash
                ? FindSelectedStashItemIndex(player, reference.StableItemId, reference.Page, reference.Slot) : -1;
        }
        internal void SelectHeadlessOilTarget(PlayerSnapshot player, PlayerItemReference reference)
        {
            currentPlayer = player;
            selectedOilTarget = reference;
        }
        internal void SelectHeadlessStoreItem(StoreSnapshot store, uint slot)
        {
            ReconcileItemSelections(null, store);
            selectedStoreSlot = slot;
        }
        internal static int HeadlessFindSelectedStashItem(PlayerSnapshot player, uint? stableId, uint page, uint cell)
            => FindSelectedStashItemIndex(player, stableId, page, cell);
        // Widget refresh is outside this fixture; the existing clear operation resets these fields.
        private void ClearSelectedItem()
        {
            selectedInventoryIndex = selectedStashItemIndex = -1;
            selectedBeltSlot = selectedEquipmentSlot = null;
            selectedItemReference = null;
        }

        internal bool NeedsHeadlessHudUpdate() => NeedsHudApply(headlessModel, null, null);
        internal int RouteSelection { set => routes.Selected = value; }
        internal int DestinationSelection { set => destinations.Selected = value; }
        internal bool CaptionVisible { set => inventory.Visible = value; }
        internal int CaptionFormatCount { get; private set; }
        internal PlayerSnapshot? CaptionPlayer { get; private set; }
        internal void ApplyHeadlessCaption(PlayerSnapshot? player) => ApplyInventoryCaption(player);

        internal void ChangeLocalInput(string input)
        {
            switch (input) {
            case "tab": sections.CurrentTab = 1; break;
            case "diagnostics": diagnosticsToggle.ButtonPressed = true; break;
            case "inventory": selectedInventoryIndex = 0; break;
            case "belt": selectedBeltSlot = 1; break;
            case "equipment": selectedEquipmentSlot = 4; break;
            case "store": selectedStoreSlot = 0; break;
            case "stash": selectedStashItemIndex = 0; break;
            case "stash-page": stashPage = 1; break;
            case "oil-target": selectedOilTarget = new() {Location = PlayerItemLocation.Equipment, Slot = 4}; break;
            case "spell": selectedSpellText = "spell"; break;
            case "session-transition": sessionMenuTransition = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(input));
            }
        }

        private string FormatInventory(PlayerSnapshot? player)
        {
            CaptionFormatCount++;
            CaptionPlayer = player;
            return string.Empty;
        }

        private static string FormatAttributes(PlayerSnapshot? player) => string.Empty;
        private static HeadlessViewport GetViewport() => throw new NotSupportedException("Rendering is outside the scheduling fixture.");
        private sealed class HeadlessViewport { public HeadlessRectangle GetVisibleRect() => new(); }
        private sealed class HeadlessRectangle { public Godot.Vector2 Size => default; }
        private sealed class HeadlessControl
        {
            public int Selected { get; set; } = -1;
            public int CurrentTab { get; set; }
            public bool ButtonPressed { get; set; }
            public bool Visible { get; set; } = true;
            public string Text { get; set; } = string.Empty;
            public bool IsVisibleInTree() => Visible;
        }
    }
}
