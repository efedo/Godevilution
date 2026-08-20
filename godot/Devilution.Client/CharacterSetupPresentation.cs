using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client;

/** Collects the initial authoritative character identity before connecting. */
public partial class CharacterSetupPresentation : CanvasLayer
{
    private static readonly (uint Id, string Name)[] Classes = [
        (0, "Warrior"),
        (1, "Rogue"),
        (2, "Sorcerer"),
        (3, "Monk"),
        (4, "Bard"),
        (5, "Barbarian"),
    ];

    private readonly OptionButton classSelector = new();
    private readonly LineEdit nameField = new() { PlaceholderText = "Character name" };
    private readonly Label validation = new();

    public event Action<uint, string>? Submitted;

    public override void _Ready()
    {
        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(420, 240),
        };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        var title = new Label {
            Text = "Godevilution",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        layout.AddChild(title);
        layout.AddChild(new Label { Text = "Choose the character identity for this authoritative session." });

        foreach (var (id, name) in Classes)
            classSelector.AddItem(name, (int)id);
        classSelector.Selected = 0;
        layout.AddChild(classSelector);

        nameField.MaxLength = 32;
        nameField.Text = "Hero";
        layout.AddChild(nameField);

        var submit = new Button { Text = "Connect" };
        submit.Pressed += SubmitSelection;
        nameField.TextSubmitted += _ => SubmitSelection();
        layout.AddChild(submit);

        validation.Modulate = new Color("ff9b9b");
        layout.AddChild(validation);
    }

    /** Shows the setup form, using environment values as defaults when present. */
    public void ShowSetup(ClientConnectionOptions options)
    {
        Visible = true;
        if (options.RequestedCharacterClassId is { } classId) {
            var selectedIndex = Array.FindIndex(Classes, entry => entry.Id == classId);
            if (selectedIndex >= 0)
                classSelector.Selected = selectedIndex;
        }
        if (!string.IsNullOrWhiteSpace(options.RequestedCharacterName))
            nameField.Text = options.RequestedCharacterName;
        nameField.GrabFocus();
    }

    public void HideSetup() => Visible = false;

    private void SubmitSelection()
    {
        var name = nameField.Text.Trim();
        if (name.Length is < 1 or > 32) {
            validation.Text = "Enter a character name between 1 and 32 characters.";
            return;
        }

        validation.Text = string.Empty;
        Submitted?.Invoke((uint)classSelector.GetSelectedId(), name);
    }
}
