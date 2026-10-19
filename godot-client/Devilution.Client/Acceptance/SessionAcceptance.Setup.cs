using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private async Task VerifyProfileClassChoicesAsync()
    {
        var environmentOverrides = new Dictionary<string, string?> {
            ["DEVILUTION_SERVER_HOST"] = "127.0.0.1",
            ["DEVILUTION_SERVER_PORT"] = "6113",
            ["DEVILUTION_CHARACTER_CLASS_ID"] = null,
            [ClientCharacterSetupPolicy.SupportedClassIdsEnvironmentVariable] = "1",
        };
        var savedEnvironment = environmentOverrides.Keys.ToDictionary(
            name => name,
            name => System.Environment.GetEnvironmentVariable(name));
        CharacterSetupPresentation? setup = null;
        try {
            foreach (var (environmentName, environmentValue) in environmentOverrides)
                System.Environment.SetEnvironmentVariable(environmentName, environmentValue);

            setup = new CharacterSetupPresentation();
            AddChild(setup);
            await Frame();

            var options = new ClientConnectionOptions(
                "127.0.0.1", 6113, "setup-policy-test", "0.2.0", "content-hash") {
                RequestedCharacterClassId = 0,
                RequestedCharacterName = "Profile-Hero",
            };
            setup.ShowSetup(options);
            await Frame();
            var classes = (OptionButton)setup.FindChild("CharacterClass", true, false);
            var host = (LineEdit)setup.FindChild("ServerHost", true, false);
            var name = (LineEdit)setup.FindChild("CharacterName", true, false);
            var connect = (Button)setup.FindChild("Connect", true, false);
            var validation = (Label)setup.FindChild("Validation", true, false);
            var submissions = new List<(uint ClassId, string Name, string Host, int Port, string ResumeToken)>();
            setup.Submitted += (classId, characterName, submittedHost, port, resumeToken) =>
                submissions.Add((classId, characterName, submittedHost, port, resumeToken));

            Require(classes.ItemCount == 1 && classes.GetSelectedId() == 1 && classes.Disabled,
                "The native-profile setup form did not lock creation to the supported Rogue class.");
            name.Text = string.Empty;
            await Click(connect.GetGlobalRect().GetCenter());
            Require(submissions.Count == 0 && validation.Text.Contains("Enter 1-31 printable ASCII characters", StringComparison.Ordinal),
                "The actual Connect control did not reject an invalid new-character name.");

            name.Text = "Profile-Rogue";
            await Click(connect.GetGlobalRect().GetCenter());
            Require(submissions.Count == 1 && submissions[0].ClassId == 1
                && submissions[0].Name == "Profile-Rogue" && submissions[0].Host == "127.0.0.1",
                "The actual Connect control did not submit the supported local Rogue selection.");

            host.Text = "remote.example";
            host.EmitSignal(LineEdit.SignalName.TextChanged, host.Text);
            Require(classes.ItemCount == 6 && !classes.Disabled,
                "Changing the endpoint did not restore generic remote class choices.");
            classes.Select(2);
            host.Text = "127.0.0.1";
            host.EmitSignal(LineEdit.SignalName.TextChanged, host.Text);
            Require(classes.ItemCount == 1 && classes.GetSelectedId() == 1 && classes.Disabled,
                "Returning to the configured local endpoint did not restore the Rogue profile limit.");

            System.Environment.SetEnvironmentVariable(
                ClientCharacterSetupPolicy.SupportedClassIdsEnvironmentVariable,
                "invalid");
            var resumeToken = (LineEdit)setup.FindChild("ResumeToken", true, false);
            resumeToken.Text = "existing-session-token";
            var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
            mode.Select(1);
            mode.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            await Frame();
            Require(!classes.Visible && classes.ItemCount == 6,
                "Resume incorrectly required or displayed the native character-class capability.");
            await Click(connect.GetGlobalRect().GetCenter());
            Require(submissions.Count == 2 && submissions[1].ClassId == 0
                && submissions[1].Name.Length == 0
                && submissions[1].ResumeToken == "existing-session-token",
                "The actual resume control submitted a new-character class selection.");
        } finally {
            if (setup is not null && GodotObject.IsInstanceValid(setup)) {
                setup.QueueFree();
                await Frame();
            }
            foreach (var (name, value) in savedEnvironment)
                System.Environment.SetEnvironmentVariable(name, value);
        }
    }
}
