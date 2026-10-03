using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

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
        if (phase is not ("Create" or "Resume" or "Recovery" or "Journey" or "JourneyResume" or "TownLoop" or "TownLoopResume" or "Lifecycle" or "LifecycleResume" or "MultiplayerCreate" or "MultiplayerResume" or "MultiplayerRecovery" or "MultiplayerPendingRecovery" or "Performance" or "CathedralJourney" or "CathedralResume" or "CathedralDepthJourney" or "CathedralDepthResume"))
            throw new InvalidOperationException("Explicit acceptance phase is required.");
        var portalPreview = Environment.GetEnvironmentVariable("GODEVILUTION_PORTAL_PREVIEW") == "1";
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_DIRECTORY")
            ?? throw new InvalidOperationException("An isolated evidence directory is required.");
        // The headless display driver otherwise supplies a 64x64 viewport.
        if (DisplayServer.GetName() == "headless")
            GetTree().Root.Size = new Vector2I(1280, 720);
        if (phase is "MultiplayerCreate" or "MultiplayerResume" or "MultiplayerRecovery" or "MultiplayerPendingRecovery") {
            await RunMultiplayerAsync(directory, phase);
            return;
        }
        if (phase == "Performance") {
            await RunPerformanceAsync(directory);
            return;
        }
        var bookmarkPath = System.IO.Path.Combine(directory, "resume-token.txt");
        var creating = phase is "Create" or "Journey" or "TownLoop" or "Lifecycle" or "CathedralJourney" or "CathedralDepthJourney";
        if (creating) {
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
        if (creating) {
            Require(mode.Selected == 0, "A corrupt bookmark must leave new-character selection available.");
            Require(((Label)setup.FindChild("Validation", true, false)).Text.Contains("Could not read local resume bookmark"),
                "The corrupt bookmark was not reported in the setup UI.");
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = phase == "Lifecycle"
                ? "M1-Sorcerer" : "M0-Rogue";
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(phase == "Lifecycle" ? 2 : 1);
        } else {
            previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, phase switch {
                    "Recovery" => "resume.json", "JourneyResume" => "journey.json",
                    "TownLoopResume" => "townloop.json", "CathedralResume" => "cathedraljourney.json",
                    "CathedralDepthResume" => "cathedraldepthjourney.json", "LifecycleResume" => "lifecycle.json", _ => "create.json",
                })))
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
        var lifecyclePhase = phase is "Lifecycle" or "LifecycleResume";
        var expectedClass = lifecyclePhase ? 2U : 1U;
        var expectedName = lifecyclePhase ? "M1-Sorcerer" : "M0-Rogue";
        Require(initial.CharacterClassId == expectedClass && initial.CharacterName == expectedName, "Character creation identity did not reach the host.");
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
        await VerifyResponsiveLayout(main, directory, visible && creating);
        TownPortalPurchaseEvidence? portalPurchase = null;
        if (phase == "TownLoop" && portalPreview) {
            portalPurchase = await VerifyTownPortalAcquisition(main);
            initial = main.ObservedSnapshot!.Players.Single().Clone();
        }
        if (previous is not null) {
            Require(main.ObservedIdentity!.ContentManifestHash == previous.ContentHash
                && main.ObservedIdentity.RulesetIdentityHash == previous.RulesetHash,
                "Fresh host/client changed the persisted content or ruleset identity.");
            Require(initial.EntityId == previous.EntityId && initial.PositionX == previous.X
                && initial.PositionY == previous.Y && initial.LevelId == previous.LevelId,
                $"Fresh host/client restore mismatch: expected entity/level/position {previous.EntityId}/{previous.LevelId}/{previous.X},{previous.Y}; observed {initial.EntityId}/{initial.LevelId}/{initial.PositionX},{initial.PositionY}.");
        }
        if (phase is "Lifecycle" or "LifecycleResume") {
            await VerifySorcererLifecycleAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralJourney" or "CathedralResume") {
            await VerifyGeneratedCathedralAsync(main, directory, phase, visible);
            return;
        }
        if (phase is "CathedralDepthJourney" or "CathedralDepthResume") {
            await VerifyCathedralDepthsAsync(main, directory, phase, visible);
            return;
        }
        if (phase == "JourneyResume")
            VerifyCombatLootRestored(main, directory);
        if (phase == "TownLoopResume")
            await VerifyTownLoopRestored(main, directory, visible, portalPreview);
        if (phase == "TownLoopResume" && portalPreview)
            initial = main.ObservedSnapshot!.Players.Single().Clone();

        GetViewport().GuiReleaseFocus();
        var preInputTick = main.ObservedSnapshot!.Tick;
        // The preview's town arrival is immediately north of the entry stair.
        var moveKey = phase == "TownLoopResume" ? Key.Left : Key.Down;
        Input.ParseInputEvent(new InputEventKey { Keycode = moveKey, PhysicalKeycode = moveKey, Pressed = true });
        double? inputStallMilliseconds = null;
        if (creating) {
            Input.FlushBufferedEvents();
            Require(main.ObservedPendingCommandCount == 1, "The actual input handler did not queue movement before the frame stall.");
            // Model a busy frame after real input but before the next transport poll.
            // Only the client pauses; the separate authoritative host keeps running.
            var stall = Stopwatch.StartNew();
            System.Threading.Thread.Sleep(750);
            inputStallMilliseconds = stall.Elapsed.TotalMilliseconds;
        }
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = moveKey, PhysicalKeycode = moveKey, Pressed = false });
        await Until(() => IsAcceptedOrSuccessfulDuplicate(main.ObservedCommandResult)
            && main.ObservedSnapshot?.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } player
            && player.LevelId == initial.LevelId
            && player.PositionX == initial.PositionX + (moveKey == Key.Left ? -1 : 0)
            && player.PositionY == initial.PositionY + (moveKey == Key.Down ? 1 : 0),
            "Movement did not finish with an accepted command and settled authoritative position.");
        var moved = main.ObservedSnapshot!.Players.Single();
        var result = main.ObservedCommandResult!;
        if (inputStallMilliseconds is { } stallMilliseconds)
            WriteEvidence(directory, phase!.ToLowerInvariant() + "-input-stall.json", new {
                Passed = true, StallMilliseconds = stallMilliseconds,
                PreInputTick = preInputTick,
                result.ClientSequence, result.AppliedTick,
            });
        Require(previous is null || result.ClientSequence > previous.Sequence,
            "Fresh client reused a persisted command sequence.");
        var identity = main.ObservedIdentity!;
        Require(identity.GameMode == GameMode.SinglePlayer, "The connected client did not observe single-player mode.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-world.png"));
        // Permit the host's periodic checkpoint to catch up; the next phase
        // proves the actual disk restore instead of treating this delay as proof.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        if (phase == "Recovery") {
            await VerifyRecovery(main, directory, visible);
            moved = main.ObservedSnapshot!.Players.Single();
            result = main.ObservedCommandResult!;
        }
        if (phase is "Journey" or "TownLoop") {
            await VerifyRouteJourney(main, directory, visible);
            await VerifyCombatAndLoot(main, directory, visible);
            if (phase == "TownLoop")
                await VerifyEquipmentAndTownLoop(main, directory, visible, portalPurchase);
            moved = main.ObservedSnapshot!.Players.Single();
            result = main.ObservedCommandResult!;
        }
        var evidence = new Evidence(true, identity.GameMode.ToString(), identity.ContentManifestHash,
            identity.RulesetIdentityHash, moved.EntityId, moved.CharacterClassId, moved.LevelId,
            moved.PositionX, moved.PositionY, result.ClientSequence, visible);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, phase!.ToLowerInvariant() + ".json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"Godot application acceptance passed: {phase}; visible={visible}; sequence={result.ClientSequence}");
    }

    private async Task VerifyRecovery(Main main, string directory, bool visible)
    {
        Require(main.ObservedPendingCommandCount == 0, "Recovery requires an acknowledged, settled command.");
        var savedPlayer = main.ObservedSnapshot!.Players.Single();
        var savedSequence = main.ObservedCommandResult!.ClientSequence;
        var savedIdentity = main.ObservedIdentity!;
        WriteEvidence(directory, "recovery-ready.json", new { Ready = true, Sequence = savedSequence });
        // The launcher owns process termination/restart. The scene only observes
        // the real transport and exercises the same input handlers as a user.
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(directory, "recovery-server-stopped.json"))
            && !main.TransportConnected, "The owned server did not stop or the client did not detect its loss.");
        var status = (Label)main.FindChild("ConnectionStatus", true, false);
        await Until(() => status.Text.Contains("Connection lost") && status.Text.Contains("reconnecting"),
            "The scene did not show explicit disconnect/reconnect feedback.");
        await Frame();
        var disconnected = main.ObservedSnapshot!;
        var disconnectedRevision = main.ObservedSnapshotRevision;
        var screenPosition = main.ObservedPlayerScreenPosition
            ?? throw new InvalidOperationException("The disconnected scene lost its last authoritative presentation.");
        Require(main.ObservedPendingCommandCount == 0, "The disconnected scene retained an unexpected command.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "recovery-disconnected.png"));

        // Leave the host unavailable across several retry attempts. Key input
        // must not change state, prediction/rendering, or the command ledger.
        var offline = Stopwatch.StartNew();
        while (offline.Elapsed < TimeSpan.FromSeconds(1.5)) {
            await PressDown();
            Require(!main.TransportConnected, "The host restarted before the offline assertions completed.");
            Require(disconnected.Equals(main.ObservedSnapshot), "Gameplay state advanced without a server.");
            Require(main.ObservedSnapshotRevision == disconnectedRevision, "An unexpected snapshot arrived while the host was absent.");
            Require(main.ObservedPendingCommandCount == 0
                && main.ObservedCommandResult!.ClientSequence == savedSequence,
                "Disconnected input queued or acknowledged a gameplay command.");
            Require(main.ObservedPlayerScreenPosition is { } displayed
                && displayed.DistanceTo(screenPosition) < 0.01f,
                "Disconnected input moved the rendered player.");
            Require(status.Text.Contains("Connection lost"), "Disconnect feedback disappeared while the host was absent.");
        }
        WriteEvidence(directory, "recovery-disconnected.json", new {
            Passed = true, Tick = disconnected.Tick, Sequence = savedSequence,
            OfflineSeconds = offline.Elapsed.TotalSeconds,
        });
        await Until(() => main.TransportConnected && main.ObservedSnapshotRevision > disconnectedRevision,
            "The same Godot client did not receive a fresh snapshot after host restart.");
        Require(savedIdentity == main.ObservedIdentity, "Reconnect changed the server content/ruleset/mode identity.");
        var restored = main.ObservedSnapshot!.Players.Single();
        Require(restored.EntityId == savedPlayer.EntityId && restored.CharacterClassId == savedPlayer.CharacterClassId
            && restored.CharacterName == savedPlayer.CharacterName && restored.LevelId == savedPlayer.LevelId
            && restored.PositionX == savedPlayer.PositionX && restored.PositionY == savedPlayer.PositionY,
            "The restarted host did not restore the saved character and position.");
        await Until(() => status.Text.StartsWith("Connected -", StringComparison.Ordinal),
            "The scene retained a stale disconnect error after receiving authoritative state.");
        await PressDown();
        await Until(() => main.ObservedCommandResult is { } result && IsAcceptedOrSuccessfulDuplicate(result)
            && result.ClientSequence == savedSequence + 1
            && main.ObservedSnapshot!.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } player
            && player.PositionX == restored.PositionX && player.PositionY == restored.PositionY + 1,
            "Reconnect did not restore exactly-once authoritative movement with the next command sequence.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "recovery-reconnected.png"));
    }

    private async Task PressDown()
    {
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = true });
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = false });
        await Frame();
    }

    private static void WriteEvidence(string directory, string filename, object evidence)
    {
        var path = System.IO.Path.Combine(directory, filename);
        var staging = path + ".tmp";
        System.IO.File.WriteAllText(staging, JsonSerializer.Serialize(evidence));
        System.IO.File.Move(staging, path);
    }

    private async Task Until(Func<bool> condition, string failure, double? stageSeconds = null)
    {
        var stage = Stopwatch.StartNew();
        while (!condition()) {
            var seconds = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE") switch {
                "CathedralJourney" => 300, "CathedralResume" => 180,
                "CathedralDepthJourney" => 885, "CathedralDepthResume" => 435,
                "Lifecycle" => 345, "LifecycleResume" => 70,
                "TownLoop" => 225, "Journey" => 150, "MultiplayerCreate" or "MultiplayerResume" => 70,
                "MultiplayerRecovery" => 90, _ => 40,
            };
            Require(deadline.Elapsed < TimeSpan.FromSeconds(seconds), failure);
            Require(stageSeconds is null || stage.Elapsed.TotalSeconds < stageSeconds, failure);
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
