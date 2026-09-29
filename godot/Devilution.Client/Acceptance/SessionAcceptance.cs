using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/** Opt-in application acceptance: operates real controls, never simulation state. */
public partial class SessionAcceptance : Node
{
    private readonly Stopwatch deadline = Stopwatch.StartNew();

    public override async void _Ready()
    {
        try {
            await RunAsync();
            GetTree().Quit();
        } catch (Exception exception) {
            GD.PushError($"Godot session acceptance failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var phase = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE");
        if (phase is not ("Create" or "Resume"))
            throw new InvalidOperationException("Explicit acceptance phase is required.");
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_DIRECTORY")
            ?? throw new InvalidOperationException("An isolated evidence directory is required.");
        // The headless display driver otherwise supplies a 64x64 viewport.
        if (DisplayServer.GetName() == "headless")
            GetTree().Root.Size = new Vector2I(1280, 720);
        var bookmarkPath = System.IO.Path.Combine(directory, "resume-token.txt");
        if (phase == "Create") {
            Require(!System.IO.File.Exists(bookmarkPath), "Creation requires a fresh isolated bookmark directory.");
            System.IO.File.WriteAllText(bookmarkPath, "invalid token");
        }
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        Require(setup.Visible && !main.TransportConnected, "Acceptance must enter through the setup form.");
        var mode = setup.FindChild("SessionMode", true, false) as OptionButton
            ?? throw new InvalidOperationException("Session mode control is missing.");
        Evidence? previous = null;
        if (phase == "Create") {
            Require(mode.Selected == 0, "A corrupt bookmark must leave new-character selection available.");
            Require(((Label)setup.FindChild("Validation", true, false)).Text.Contains("Could not read local resume bookmark"),
                "The corrupt bookmark was not reported in the setup UI.");
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = "M0-Rogue";
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);
        } else {
            previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "create.json")))
                ?? throw new InvalidDataException("The creation evidence is empty.");
            Require(previous.Passed && mode.Selected == 1, "Resume must use the persisted form defaults.");
            Require(((LineEdit)setup.FindChild("ResumeToken", true, false)).Text.Length > 0,
                "The resume credential was not restored to the form.");
        }

        var visible = DisplayServer.GetName() != "headless";
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-setup.png"));
        // Mouse input traverses Godot's Control routing and the real button handler.
        var connect = (Button)setup.FindChild("Connect", true, false);
        // A Windows sharing violation exercises the real atomic-write failure
        // path. The user-visible warning must survive subsequent snapshots.
        using var bookmarkWriteBlock = phase == "Create"
            ? new FileStream(bookmarkPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)
            : null;
        var center = connect.GetGlobalRect().GetCenter();
        Require(GetViewport().GetVisibleRect().HasPoint(center), $"The Connect button {connect.GetGlobalRect()} is outside viewport {GetViewport().GetVisibleRect()}.");
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center, Pressed = true,
        });
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center, Pressed = false,
        });
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count == 1,
            "No authoritative character snapshot arrived after submitting the form.");
        var initial = main.ObservedSnapshot!.Players.Single();
        Require(initial.CharacterClassId == 1 && initial.CharacterName == "M0-Rogue", "Character creation identity did not reach the host.");
        Require(!setup.Visible, "Setup did not close after submission.");
        if (phase == "Create") {
            var warning = (Label)main.FindChild("SessionWarning", true, false);
            await Until(() => warning.Visible, "Bookmark write failure was not shown persistently.");
            var observedTick = main.ObservedSnapshot!.Tick;
            await Until(() => main.ObservedSnapshot!.Tick > observedTick + 2,
                "No later authoritative snapshot arrived during the bookmark failure.");
            Require(warning.Visible, "A later snapshot cleared the bookmark failure warning.");
            bookmarkWriteBlock!.Dispose();
            await Until(() => !warning.Visible && System.IO.File.ReadAllText(bookmarkPath) != "invalid token",
                "Bookmark persistence did not recover after file access was restored.");
        }
        Require(LocalSessionBookmark.Read(bookmarkPath) is not null,
            "No durable local resume bookmark was written.");
        if (previous is not null) {
            Require(initial.EntityId == previous.EntityId && initial.PositionX == previous.X
                && initial.PositionY == previous.Y && initial.LevelId == previous.LevelId,
                $"Fresh host/client restore mismatch: expected entity/level/position {previous.EntityId}/{previous.LevelId}/{previous.X},{previous.Y}; observed {initial.EntityId}/{initial.LevelId}/{initial.PositionX},{initial.PositionY}.");
        }

        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = true });
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = false });
        await Until(() => main.ObservedCommandResult is { Status: CommandStatus.Accepted }
            && main.ObservedSnapshot?.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } player
            && player.PositionY == initial.PositionY + 1,
            "Movement did not finish with an accepted command and settled authoritative position.");
        var moved = main.ObservedSnapshot!.Players.Single();
        var result = main.ObservedCommandResult!;
        Require(previous is null || result.ClientSequence > previous.Sequence,
            "Fresh client reused a persisted command sequence.");
        var identity = main.ObservedIdentity!;
        Require(identity.GameMode == GameMode.SinglePlayer, "The connected client did not observe single-player mode.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-world.png"));
        // Permit the host's periodic checkpoint to catch up; the next phase
        // proves the actual disk restore instead of treating this delay as proof.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var evidence = new Evidence(true, identity.GameMode.ToString(), identity.ContentManifestHash,
            identity.RulesetIdentityHash, moved.EntityId, moved.CharacterClassId, moved.LevelId,
            moved.PositionX, moved.PositionY, result.ClientSequence, visible);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, phase!.ToLowerInvariant() + ".json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"Godot application acceptance passed: {phase}; visible={visible}; sequence={result.ClientSequence}");
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        while (!condition()) {
            Require(deadline.Elapsed < TimeSpan.FromSeconds(30), failure);
            await Frame();
        }
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Capture(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(path) == Error.Ok, "Could not save visible acceptance evidence.");
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }

    private sealed record Evidence(bool Passed, string Mode, string ContentHash, string RulesetHash,
        uint EntityId, uint ClassId, uint LevelId, int X, int Y, ulong Sequence, bool Visible);
}
