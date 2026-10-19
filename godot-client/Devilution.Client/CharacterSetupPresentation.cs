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

    private readonly OptionButton classSelector = new() { Name = "CharacterClass" };
    private readonly OptionButton sessionModeSelector = new() { Name = "SessionMode" };
    private readonly Button connectButton = new() { Name = "Connect", Text = "Connect" };
    private readonly LineEdit hostField = new() {
        Name = "ServerHost",
        PlaceholderText = "Server host",
        CustomMinimumSize = new Vector2(240, 0),
    };
    private readonly SpinBox portField = new() {
        Name = "ServerPort",
        MinValue = 1,
        MaxValue = 65535,
        Step = 1,
        Value = 6113,
        CustomMinimumSize = new Vector2(100, 0),
    };
    private readonly LineEdit nameField = new() { Name = "CharacterName", PlaceholderText = "Character name" };
    private readonly LineEdit resumeTokenField = new() {
        PlaceholderText = "Resume token",
        Name = "ResumeToken",
        Secret = true,
    };
    private readonly Label classLabel = new() { Text = "Character class" };
    private readonly Label nameLabel = new() { Text = "Character name" };
    private readonly Label resumeTokenLabel = new() { Text = "Resume token" };
    private readonly Label validation = new() {
        Name = "Validation", AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };
    private string configuredHost = "127.0.0.1";
    private int configuredPort = 6113;
    private ClientConnectionOptions? setupOptions;
    private bool classChoicesInitialized;
    private string? classConfigurationError;

    public event Action<uint, string, string, int, string>? Submitted;

    public override void _Ready()
    {
        var configuredEndpoint = ClientConnectionOptions.FromEnvironment();
        configuredHost = configuredEndpoint.Host;
        configuredPort = configuredEndpoint.Port;

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(420, 320),
        };
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.AddChild(panel);

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

        foreach (var choice in ClientCharacterSetupPolicy.AllClasses)
            classSelector.AddItem(choice.Name, (int)choice.Id);
        classSelector.Selected = 0;
        layout.AddChild(classLabel);
        layout.AddChild(classSelector);

        layout.AddChild(new Label { Text = "Authoritative server" });
        var endpoint = new HBoxContainer();
        hostField.TextChanged += _ => RefreshClassChoicesForCurrentMode();
        portField.ValueChanged += _ => RefreshClassChoicesForCurrentMode();
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

        connectButton.Pressed += SubmitSelection;
        nameField.TextSubmitted += _ => SubmitSelection();
        resumeTokenField.TextSubmitted += _ => SubmitSelection();
        layout.AddChild(connectButton);

        validation.Modulate = new Color("ff9b9b");
        layout.AddChild(validation);
        RefreshSessionMode();
    }

    /** Shows the setup form, using environment values as defaults when present. */
    public void ShowSetup(ClientConnectionOptions options)
    {
        setupOptions = null;
        classChoicesInitialized = false;
        Visible = true;
        resumeTokenField.Text = options.ResumeToken;
        sessionModeSelector.Selected = string.IsNullOrWhiteSpace(options.ResumeToken)
            ? (int)SessionMode.NewCharacter
            : (int)SessionMode.ResumeSession;
        if (!string.IsNullOrWhiteSpace(options.RequestedCharacterName))
            nameField.Text = options.RequestedCharacterName;
        hostField.Text = options.Host;
        portField.Value = options.Port;
        setupOptions = options;
        RefreshSessionMode();
        if (CurrentSessionMode() == SessionMode.ResumeSession)
            resumeTokenField.GrabFocus();
        else
            nameField.GrabFocus();
    }

    public void HideSetup() => Visible = false;

    public void ShowError(string message) => validation.Text = message;

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

        if (classConfigurationError is not null) {
            validation.Text = classConfigurationError;
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
        if (resume) {
            var previousError = classConfigurationError;
            classConfigurationError = null;
            if (previousError is not null && string.Equals(validation.Text, previousError, StringComparison.Ordinal))
                validation.Text = string.Empty;
            RefreshClassChoices(preserveSelection: classChoicesInitialized, resumeSession: true);
            resumeTokenField.GrabFocus();
        } else {
            RefreshClassChoices(preserveSelection: classChoicesInitialized, resumeSession: false);
        }
    }

    private void RefreshClassChoicesForCurrentMode()
    {
        if (setupOptions is not null && CurrentSessionMode() != SessionMode.ResumeSession)
            RefreshClassChoices(preserveSelection: classChoicesInitialized, resumeSession: false);
    }

    private void RefreshClassChoices(bool preserveSelection, bool resumeSession)
    {
        if (setupOptions is null)
            return;

        var previousError = classConfigurationError;
        var preferredClassId = preserveSelection && classSelector.Selected >= 0
            ? (uint?)classSelector.GetSelectedId()
            : setupOptions.RequestedCharacterClassId;
        try {
            var choices = ClientCharacterSetupPolicy.GetAvailableClasses(
                hostField.Text,
                (int)portField.Value,
                configuredHost,
                configuredPort,
                System.Environment.GetEnvironmentVariable(
                    ClientCharacterSetupPolicy.SupportedClassIdsEnvironmentVariable),
                resumeSession);
            classSelector.Clear();
            foreach (var choice in choices)
                classSelector.AddItem(choice.Name, (int)choice.Id);
            var selectedClassId = ClientCharacterSetupPolicy.SelectClassId(choices, preferredClassId);
            classSelector.Selected = choices.ToList().FindIndex(choice => choice.Id == selectedClassId);
            classSelector.Disabled = choices.Count == 1;
            classChoicesInitialized = true;
            classConfigurationError = null;
            if (previousError is not null && string.Equals(validation.Text, previousError, StringComparison.Ordinal))
                validation.Text = string.Empty;
        } catch (InvalidDataException exception) {
            classSelector.Clear();
            classSelector.Disabled = true;
            classChoicesInitialized = false;
            classConfigurationError = exception.Message;
            validation.Text = exception.Message;
        }
    }
}
