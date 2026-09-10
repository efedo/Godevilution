using Devilution.Protocol;
using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client;

/** Collects the initial authoritative character identity before connecting. */
public partial class CharacterSetupPresentation : CanvasLayer
{
    private enum SessionMode
    {
        NewCharacter,
        ResumeSession,
    }

    private static readonly (uint Id, string Name)[] Classes = [
        (0, "Warrior"),
        (1, "Rogue"),
        (2, "Sorcerer"),
        (3, "Monk"),
        (4, "Bard"),
        (5, "Barbarian"),
    ];

    private readonly OptionButton classSelector = new();
    private readonly OptionButton sessionModeSelector = new();
    private readonly LineEdit hostField = new() {
        PlaceholderText = "Server host",
        CustomMinimumSize = new Vector2(240, 0),
    };
    private readonly SpinBox portField = new() {
        MinValue = 1,
        MaxValue = 65535,
        Step = 1,
        Value = 6113,
        CustomMinimumSize = new Vector2(100, 0),
    };
    private readonly LineEdit nameField = new() { PlaceholderText = "Character name" };
    private readonly LineEdit resumeTokenField = new() {
        PlaceholderText = "Resume token",
        Secret = true,
    };
    private readonly Label classLabel = new() { Text = "Character class" };
    private readonly Label nameLabel = new() { Text = "Character name" };
    private readonly Label resumeTokenLabel = new() { Text = "Resume token" };
    private readonly Label validation = new();

    public event Action<uint, string, string, int, string>? Submitted;

    public override void _Ready()
    {
        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(420, 320),
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
        layout.AddChild(new Label { Text = "Choose how to enter the authoritative session." });

        sessionModeSelector.AddItem("New character", (int)SessionMode.NewCharacter);
        sessionModeSelector.AddItem("Resume existing session", (int)SessionMode.ResumeSession);
        sessionModeSelector.Selected = (int)SessionMode.NewCharacter;
        sessionModeSelector.ItemSelected += _ => RefreshSessionMode();
        layout.AddChild(sessionModeSelector);

        foreach (var (id, name) in Classes)
            classSelector.AddItem(name, (int)id);
        classSelector.Selected = 0;
        layout.AddChild(classLabel);
        layout.AddChild(classSelector);

        layout.AddChild(new Label { Text = "Authoritative server" });
        var endpoint = new HBoxContainer();
        endpoint.AddChild(hostField);
        endpoint.AddChild(portField);
        layout.AddChild(endpoint);

        nameField.MaxLength = AuthoritativePlayerNameRules.MaximumLength;
        nameField.Text = "Hero";
        layout.AddChild(nameLabel);
        layout.AddChild(nameField);

        resumeTokenField.MaxLength = 512;
        layout.AddChild(resumeTokenLabel);
        layout.AddChild(resumeTokenField);

        var submit = new Button { Text = "Connect" };
        submit.Pressed += SubmitSelection;
        nameField.TextSubmitted += _ => SubmitSelection();
        resumeTokenField.TextSubmitted += _ => SubmitSelection();
        layout.AddChild(submit);

        validation.Modulate = new Color("ff9b9b");
        layout.AddChild(validation);
        RefreshSessionMode();
    }

    /** Shows the setup form, using environment values as defaults when present. */
    public void ShowSetup(ClientConnectionOptions options)
    {
        Visible = true;
        resumeTokenField.Text = options.ResumeToken;
        sessionModeSelector.Selected = string.IsNullOrWhiteSpace(options.ResumeToken)
            ? (int)SessionMode.NewCharacter
            : (int)SessionMode.ResumeSession;
        if (options.RequestedCharacterClassId is { } classId) {
            var selectedIndex = Array.FindIndex(Classes, entry => entry.Id == classId);
            if (selectedIndex >= 0)
                classSelector.Selected = selectedIndex;
        }
        if (!string.IsNullOrWhiteSpace(options.RequestedCharacterName))
            nameField.Text = options.RequestedCharacterName;
        hostField.Text = options.Host;
        portField.Value = options.Port;
        RefreshSessionMode();
        if (CurrentSessionMode() == SessionMode.ResumeSession)
            resumeTokenField.GrabFocus();
        else
            nameField.GrabFocus();
    }

    public void HideSetup() => Visible = false;

    private void SubmitSelection()
    {
        var host = hostField.Text.Trim();
        var port = (int)portField.Value;
        if (!ClientConnectionOptions.TryValidateEndpoint(host, port, out var endpointError)) {
            validation.Text = endpointError;
            return;
        }

        if (CurrentSessionMode() == SessionMode.ResumeSession) {
            var resumeToken = resumeTokenField.Text.Trim();
            if (string.IsNullOrEmpty(resumeToken)) {
                validation.Text = "Enter a resume token.";
                return;
            }

            validation.Text = string.Empty;
            Submitted?.Invoke(0, string.Empty, host, port, resumeToken);
            return;
        }

        var name = nameField.Text.Trim();
        if (string.IsNullOrEmpty(name)
            || !AuthoritativePlayerNameRules.IsValidShape(name)) {
            validation.Text = "Enter 1-31 printable ASCII characters.";
            return;
        }

        validation.Text = string.Empty;
        Submitted?.Invoke((uint)classSelector.GetSelectedId(), name, host, port, string.Empty);
    }

    private SessionMode CurrentSessionMode()
    {
        return (SessionMode)sessionModeSelector.GetSelectedId();
    }

    private void RefreshSessionMode()
    {
        var resume = CurrentSessionMode() == SessionMode.ResumeSession;
        classLabel.Visible = !resume;
        classSelector.Visible = !resume;
        nameLabel.Visible = !resume;
        nameField.Visible = !resume;
        resumeTokenLabel.Visible = resume;
        resumeTokenField.Visible = resume;
        if (resume)
            resumeTokenField.GrabFocus();
    }
}
