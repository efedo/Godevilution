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
