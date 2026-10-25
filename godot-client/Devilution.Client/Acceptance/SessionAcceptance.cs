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
    private Main? timingObservedMain;

    public override async void _Ready()
    {
        Exception? primaryFailure = null;
        try {
            await RunAsync();
        } catch (Exception exception) {
            primaryFailure = exception;
        }
        try {
            WriteCommandTimingEvidence(primaryFailure is null);
        } catch (Exception traceFailure) {
            if (primaryFailure is null)
                primaryFailure = traceFailure;
            else
                GD.PushError($"Command timing evidence export also failed: {traceFailure}");
        }
        if (primaryFailure is not null)
            GD.PushError($"Godot session acceptance failed: {primaryFailure}");
        GetTree().Quit(primaryFailure is null ? 0 : 1);
    }

    private async Task RunAsync()
    {
        var phase = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE");
        if (phase is not ("Create" or "Resume" or "Recovery" or "Journey" or "JourneyResume" or "TownLoop" or "TownLoopResume" or "Lifecycle" or "LifecycleResume" or "MultiplayerCreate" or "MultiplayerResume" or "MultiplayerRecovery" or "MultiplayerPendingRecovery" or "CooperativeJourney" or "CooperativeResume" or "Performance" or "PerformanceCreate" or "PerformanceResume" or "CathedralJourney" or "CathedralResume" or "CathedralDepthJourney" or "CathedralDepthResume" or "CathedralDoorJourney" or "CathedralDoorResume" or "CathedralNativeJourney" or "CathedralNativeResume" or "CathedralNativeChestJourney" or "CathedralNativeChestResume" or "CathedralNativeSarcophagusJourney" or "CathedralNativeSarcophagusResume" or "CathedralNativeBarrelJourney" or "CathedralNativeBarrelResume" or "CathedralNativeTrapJourney" or "CathedralNativeTrapResume" or "CathedralNativeProgressionJourney" or "CathedralNativeProgressionResume" or "CathedralNativeDepthProgressionJourney" or "CathedralNativeDepthProgressionResume" or "NativeWarriorJourney" or "NativeWarriorResume" or "NativeSorcererJourney" or "NativeSorcererResume" or "NativeRogueJourney" or "NativeRogueResume" or "NativeDepthJourney" or "NativeDepthResume"))
            throw new InvalidOperationException("Explicit acceptance phase is required.");
        var sessionMenuAcceptance = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_SESSION_MENU") == "1";
        Require(!sessionMenuAcceptance || phase is "Create" or "Resume",
            "Session-menu acceptance only supports isolated base Create/Resume phases.");
        var nativeDepthEntryBaselinePath = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_ENTRY_BASELINE");
        var nativeDepthEntryContinuation = !string.IsNullOrWhiteSpace(nativeDepthEntryBaselinePath);
        Require(!nativeDepthEntryContinuation || phase == "CathedralNativeDepthProgressionResume",
            "A native depth entry baseline is only valid for the explicit depth-progression Resume phase.");
        if (nativeDepthEntryContinuation) {
            Require(System.IO.Path.IsPathFullyQualified(nativeDepthEntryBaselinePath!),
                "The native depth entry baseline path must be absolute.");
            nativeDepthEntryBaselinePath = System.IO.Path.GetFullPath(nativeDepthEntryBaselinePath!);
            Require(System.IO.File.Exists(nativeDepthEntryBaselinePath),
                "The explicit native depth entry baseline file is missing.");
        }
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
        if (phase is "CooperativeJourney" or "CooperativeResume") {
            await RunCooperativeAsync(directory, phase);
            return;
        }
        if (phase is "Performance" or "PerformanceCreate") {
            await RunPerformanceAsync(directory, captureResumeBaseline: phase == "PerformanceCreate");
            return;
        }
        if (phase == "PerformanceResume") {
            await RunPerformanceResumeAsync(directory);
            return;
        }
        var bookmarkPath = System.IO.Path.Combine(directory, "resume-token.txt");
        var creating = phase is "Create" or "Journey" or "TownLoop" or "Lifecycle" or "CathedralJourney" or "CathedralDepthJourney" or "CathedralDoorJourney" or "CathedralNativeJourney" or "CathedralNativeProgressionJourney" or "CathedralNativeDepthProgressionJourney" or "NativeWarriorJourney" or "NativeSorcererJourney" or "NativeRogueJourney" or "NativeDepthJourney";
        if (creating) {
            Require(!System.IO.File.Exists(bookmarkPath), "Creation requires a fresh isolated bookmark directory.");
            System.IO.File.WriteAllText(bookmarkPath, "invalid token");
        }
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        timingObservedMain = main;
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
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = phase switch {
                "NativeWarriorJourney" => "Native-Warrior", "NativeSorcererJourney" => "Native-Sorcerer", "NativeRogueJourney" => "Native-Rogue", "NativeDepthJourney" => "Native-Warrior",
                "Lifecycle" => "M1-Sorcerer", _ => "M0-Rogue",
            };
            var selector = (OptionButton)setup.FindChild("CharacterClass", true, false);
            selector.Select(selector.GetItemIndex(phase switch {
                "NativeWarriorJourney" or "NativeDepthJourney" => 0, "NativeSorcererJourney" or "Lifecycle" => 2, _ => 1,
            }));
        } else {
            if (!nativeDepthEntryContinuation) {
                previous = JsonSerializer.Deserialize<Evidence>(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(directory, phase switch {
                        "NativeWarriorResume" => "nativewarriorjourney.json",
                        "NativeSorcererResume" => "nativesorcererjourney.json",
                        "NativeRogueResume" => "nativeroguejourney.json",
                        "NativeDepthResume" => "nativedepthjourney.json",
                        "Recovery" => "resume.json", "JourneyResume" => "journey.json",
                        "TownLoopResume" => "townloop.json", "CathedralResume" => "cathedraljourney.json",
                        "CathedralDepthResume" => "cathedraldepthjourney.json", "CathedralDoorResume" => "cathedraldoorjourney.json",
                        "CathedralNativeResume" => "cathedralnativejourney.json",
                        "CathedralNativeProgressionResume" => "cathedralnativeprogressionjourney.json",
                        "CathedralNativeDepthProgressionResume" => "cathedralnativedepthprogressionjourney.json",
                        "CathedralNativeChestJourney" => "cathedralnativeresume.json",
                        "CathedralNativeChestResume" => "cathedralnativechestjourney.json",
                        "CathedralNativeSarcophagusJourney" => "cathedralnativechestresume.json",
                        "CathedralNativeSarcophagusResume" => "cathedralnativesarcophagusjourney.json",
                        "CathedralNativeBarrelJourney" => "cathedralnativesarcophagusresume.json",
                        "CathedralNativeBarrelResume" => "cathedralnativebarreljourney.json",
                        "CathedralNativeTrapJourney" => "cathedralnativebarrelresume.json",
                        "CathedralNativeTrapResume" => "cathedralnativetrapjourney.json",
                        "LifecycleResume" => "lifecycle.json", _ => "create.json",
                    })))
                    ?? throw new InvalidDataException("The creation evidence is empty.");
                Require(previous.Passed, "Resume requires its passing creation evidence.");
            }
            Require(mode.Selected == 1, "Resume must use the persisted form defaults.");
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
        var nativeAdmissionPhase = phase is "CathedralNativeJourney" or "CathedralNativeResume" or "CathedralNativeChestJourney" or "CathedralNativeChestResume" or "CathedralNativeSarcophagusJourney" or "CathedralNativeSarcophagusResume" or "CathedralNativeBarrelJourney" or "CathedralNativeBarrelResume" or "CathedralNativeTrapJourney" or "CathedralNativeTrapResume"
            or "CathedralNativeProgressionJourney" or "CathedralNativeProgressionResume"
            or "CathedralNativeDepthProgressionJourney" or "CathedralNativeDepthProgressionResume";
        var nativeSubmissionCount = 0;
        var nativeJourneySubmittedExpectedIdentity = false;
        var nativeResumeSubmittedExpectedForm = false;
        Action<uint, string, string, int, string>? nativeSubmissionObserver = null;
        if (nativeAdmissionPhase) {
            nativeSubmissionObserver = (submittedClassId, submittedName, _, _, _) => {
                nativeSubmissionCount++;
                if (phase is "CathedralNativeJourney" or "CathedralNativeProgressionJourney" or "CathedralNativeDepthProgressionJourney") {
                    nativeJourneySubmittedExpectedIdentity = submittedClassId == 1
                        && submittedName == "M0-Rogue";
                } else {
                    nativeResumeSubmittedExpectedForm = submittedClassId == 0
                        && string.IsNullOrEmpty(submittedName);
                }
            };
            setup.Submitted += nativeSubmissionObserver;
        }
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center, Pressed = true,
        });
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center, Pressed = false,
        });
        try {
            if (nativeAdmissionPhase) {
                await UntilNativeInitialJoin(main, setup, phase, () => nativeSubmissionCount,
                    () => nativeJourneySubmittedExpectedIdentity, () => nativeResumeSubmittedExpectedForm);
            } else {
                await Frame();
                Require(!setup.Visible,
                    $"Connect input left setup open; class={((OptionButton)setup.FindChild("CharacterClass", true, false)).GetSelectedId()}, "
                    + $"validation={((Label)setup.FindChild("Validation", true, false)).Text}.");
                try {
                    await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count == 1,
                        "No authoritative character snapshot arrived after submitting the form.");
                } catch (InvalidOperationException exception) {
                    throw new InvalidOperationException(
                        $"{exception.Message} Connected={main.TransportConnected}; "
                        + $"identity={main.ObservedIdentity is not null}; "
                        + $"status={((Label)main.FindChild("ConnectionStatus", true, false)).Text}", exception);
                }
            }
        } finally {
            if (nativeSubmissionObserver is not null)
                setup.Submitted -= nativeSubmissionObserver;
        }
        var initial = main.ObservedSnapshot!.Players.Single();
        var lifecyclePhase = phase is "Lifecycle" or "LifecycleResume";
        var expectedClass = phase.StartsWith("NativeWarrior", StringComparison.Ordinal) || phase.StartsWith("NativeDepth", StringComparison.Ordinal) ? 0U
            : phase.StartsWith("NativeSorcerer", StringComparison.Ordinal) ? 2U : lifecyclePhase ? 2U : 1U;
        var expectedName = phase.StartsWith("NativeWarrior", StringComparison.Ordinal) || phase.StartsWith("NativeDepth", StringComparison.Ordinal) ? "Native-Warrior"
            : phase.StartsWith("NativeSorcerer", StringComparison.Ordinal) ? "Native-Sorcerer"
            : phase.StartsWith("NativeRogue", StringComparison.Ordinal) ? "Native-Rogue" : lifecyclePhase ? "M1-Sorcerer" : "M0-Rogue";
        Require(initial.CharacterClassId == expectedClass && initial.CharacterName == expectedName, "Character creation identity did not reach the host.");
        Require(!setup.Visible, "Setup did not close after submission.");
        await VerifyPresentationPackAsync(main, directory, phase);
        var bookmarkReturnRejected = false;
        if (phase == "Create") {
            var warning = (Label)main.FindChild("SessionWarning", true, false);
            await Until(() => warning.Visible, "Bookmark write failure was not shown persistently.");
            var observedTick = main.ObservedSnapshot!.Tick;
            await Until(() => main.ObservedSnapshot!.Tick > observedTick + 2,
                "No later authoritative snapshot arrived during the bookmark failure.");
            Require(warning.Visible, "A later snapshot cleared the bookmark failure warning.");
            if (sessionMenuAcceptance) {
                await SelectTab((TabContainer)main.FindChild("HudSections", true, false), 0);
                var beforeReturnGeneration = main.ObservedSessionGeneration;
                await Click(((Button)main.FindChild("ReturnToSessionMenu", true, false)).GetGlobalRect().GetCenter());
                Require(main.TransportConnected && !setup.Visible && !main.ObservedSessionTransition
                    && main.ObservedSessionGeneration == beforeReturnGeneration && warning.Visible,
                    "Return discarded a session whose resume bookmark was not durably saved.");
                Require(((Label)main.FindChild("ConnectionStatus", true, false)).Text.Contains("bookmark", StringComparison.OrdinalIgnoreCase),
                    "Bookmark return refusal did not explain why the session must remain open.");
                bookmarkReturnRejected = true;
            }
            bookmarkWriteBlock!.Dispose();
            await Until(() => !warning.Visible && System.IO.File.ReadAllText(bookmarkPath) != "invalid token",
                "Bookmark persistence did not recover after file access was restored.");
            if (sessionMenuAcceptance)
                Require(main.ObservedSessionStatus is null,
                    "Recovered bookmark persistence left stale return-refusal feedback.");
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
        if (phase is "NativeWarriorJourney" or "NativeWarriorResume" or "NativeSorcererJourney" or "NativeSorcererResume"
            or "NativeRogueJourney" or "NativeRogueResume") {
            await VerifyNativeClassAsync(main, directory, phase, previous);
            return;
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
        if (phase is "CathedralDoorJourney" or "CathedralDoorResume") {
            await VerifyCathedralDoorsAsync(main, directory, phase, visible);
            return;
        }
        if (phase is "CathedralNativeBarrelJourney" or "CathedralNativeBarrelResume") {
            await VerifyCathedralNativeBarrelsAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralNativeTrapJourney" or "CathedralNativeTrapResume") {
            await VerifyCathedralNativeTrapsAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralNativeSarcophagusJourney" or "CathedralNativeSarcophagusResume") {
            await VerifyCathedralNativeSarcophagiAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralNativeChestJourney" or "CathedralNativeChestResume") {
            await VerifyCathedralNativeChestsAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "NativeDepthJourney" or "NativeDepthResume") {
            await VerifyNativeWarriorDepthsAsync(main, directory, phase, previous);
            return;
        }
        if (IsNativeDepthProgressionPhase(phase)) {
            await VerifyCathedralNativeDepthProgressionAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralNativeProgressionJourney" or "CathedralNativeProgressionResume") {
            await VerifyCathedralNativeProgressionAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase is "CathedralNativeJourney" or "CathedralNativeResume") {
            await VerifyCathedralNativePopulationAsync(main, directory, phase, visible, previous);
            return;
        }
        if (phase == "JourneyResume")
            VerifyCombatLootRestored(main, directory);
        if (phase == "TownLoopResume")
            await VerifyTownLoopRestored(main, directory, visible, portalPreview);
        if (phase == "TownLoopResume" && portalPreview)
            initial = main.ObservedSnapshot!.Players.Single().Clone();

        if (sessionMenuAcceptance)
            await SelectTab((TabContainer)main.FindChild("HudSections", true, false), 0);
        GetViewport().GuiReleaseFocus();
        if (phase == "Create")
            await VerifyInputBurstAsync(main, directory);
        var preInputTick = main.ObservedSnapshot!.Tick;
        var semanticInitialActorBinding = main.ObservedPlayerActorTextureBinding;
        // The preview's town arrival is immediately north of the entry stair.
        var moveKey = phase == "TownLoopResume" ? Key.Left : Key.Down;
        Input.ParseInputEvent(new InputEventKey { Keycode = moveKey, PhysicalKeycode = moveKey, Pressed = true });
        double? inputStallMilliseconds = null;
        var pendingReturnRejected = false;
        var pendingCountAtRefusal = 0;
        if (creating) {
            Input.FlushBufferedEvents();
            Require(main.ObservedPendingCommandCount == 1, "The actual input handler did not queue movement before the frame stall.");
            if (sessionMenuAcceptance) {
                pendingCountAtRefusal = main.ObservedPendingCommandCount;
                var beforeReturnGeneration = main.ObservedSessionGeneration;
                ClickControlWithoutFrame((Button)main.FindChild("ReturnToSessionMenu", true, false));
                Require(main.ObservedPendingCommandCount == pendingCountAtRefusal && main.TransportConnected
                    && !setup.Visible && !main.ObservedSessionTransition
                    && main.ObservedSessionGeneration == beforeReturnGeneration,
                    "Return discarded an unacknowledged gameplay command before the transport poll.");
                Require(((Label)main.FindChild("ConnectionStatus", true, false)).Text.Contains("acknowledged", StringComparison.OrdinalIgnoreCase),
                    "Pending-command return refusal did not give actionable feedback.");
                pendingReturnRejected = true;
            }
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
        await VerifySemanticActorTransitionAsync(main, directory, phase, semanticInitialActorBinding);
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
        if (sessionMenuAcceptance) {
            await VerifySessionMenuAsync(main, directory, phase, visible,
                pendingReturnRejected, bookmarkReturnRejected, pendingCountAtRefusal);
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
        var disconnectedFrameRegion = main.ObservedPlayerTextureBinding?.FrameRegion;
        var disconnectedActorBinding = main.ObservedPlayerActorTextureBinding;
        var semanticPresentation = (Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK") ?? "default") == "semantic";
        var disconnectedEpochResetCount = main.ObservedPresentationEpochResetCount;
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
            Require(main.ObservedPlayerTextureBinding?.FrameRegion == disconnectedFrameRegion,
                "Presentation animation advanced while the authoritative host was absent.");
            if (semanticPresentation) {
                Require(disconnectedActorBinding is {
                    IsSemanticSelection: true,
                    Binding: { IsLoaded: true, FrameRegion: not null },
                }, "The disconnected scene did not retain a loaded semantic actor atlas binding.");
                Require(main.ObservedPlayerActorTextureBinding == disconnectedActorBinding,
                    "The selected semantic actor changed while the authoritative host was absent.");
            }
            Require(main.ObservedPresentationEpochResetCount == disconnectedEpochResetCount,
                "Presentation animation reset before a replacement authoritative epoch was admitted.");
        }
        WriteEvidence(directory, "recovery-disconnected.json", new {
            Passed = true, Tick = disconnected.Tick, Sequence = savedSequence,
            OfflineSeconds = offline.Elapsed.TotalSeconds,
            FrameRegion = disconnectedFrameRegion, ActorBinding = disconnectedActorBinding,
            EpochResetCount = disconnectedEpochResetCount,
        });
        await Until(() => main.TransportConnected && main.ObservedSnapshotRevision > disconnectedRevision,
            "The same Godot client did not receive a fresh snapshot after host restart.");
        Require(savedIdentity == main.ObservedIdentity, "Reconnect changed the server content/ruleset/mode identity.");
        await Until(() => main.ObservedPresentationEpochResetCount == disconnectedEpochResetCount + 1,
            "The replacement admitted epoch did not reset presentation animation exactly once.");
        WriteEvidence(directory, "recovery-presentation-epoch.json", new {
            Passed = true, PreviousResetCount = disconnectedEpochResetCount,
            ResetCount = main.ObservedPresentationEpochResetCount,
            PreviousRevision = disconnectedRevision, Revision = main.ObservedSnapshotRevision,
        });
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
            var phase = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE");
            var sessionMenuPhase = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_SESSION_MENU") == "1"
                && phase is "Create" or "Resume";
            var seconds = phase switch {
                "CathedralJourney" => 300, "CathedralResume" => 180,
                "CathedralDepthJourney" => 885, "CathedralDepthResume" => IsTownPortalPreviewResumeProfile() ? 885 : 435,
                "CathedralDoorJourney" => 300, "CathedralDoorResume" => 180,
                "CathedralNativeJourney" or "CathedralNativeResume" or "CathedralNativeChestJourney" or "CathedralNativeChestResume" or "CathedralNativeSarcophagusJourney" or "CathedralNativeSarcophagusResume" => 885,
            "CathedralNativeBarrelJourney" => 1185,
            "CathedralNativeBarrelResume" => 585,
            "CathedralNativeTrapJourney" => 885,
            "CathedralNativeTrapResume" => 585,
            "CathedralNativeProgressionJourney" => 3540,
            "CathedralNativeProgressionResume" => 1785,
            "CathedralNativeDepthProgressionJourney" => 3540,
            "CathedralNativeDepthProgressionResume" => 3540,
            "NativeDepthJourney" or "NativeDepthResume" => 3540,
                "NativeWarriorJourney" or "NativeSorcererJourney" or "NativeRogueJourney" => 885,
            "NativeWarriorResume" or "NativeSorcererResume" or "NativeRogueResume" => 180,
            "Lifecycle" => 345, "LifecycleResume" => 70,
                "TownLoop" => 225, "Journey" => 150, "MultiplayerCreate" or "MultiplayerResume" => 70,
                "MultiplayerRecovery" => 180, "CooperativeJourney" => 285, "CooperativeResume" => 225, _ => 40,
            };
            if (sessionMenuPhase)
                seconds = 170;
            Require(deadline.Elapsed < TimeSpan.FromSeconds(seconds), failure);
            Require(stageSeconds is null || stage.Elapsed.TotalSeconds < stageSeconds, failure);
            await Frame();
        }
    }

    private async Task UntilNativeInitialJoin(
        Main main,
        CharacterSetupPresentation setup,
        string phase,
        Func<int> submissionCount,
        Func<bool> journeySubmittedExpectedIdentity,
        Func<bool> resumeSubmittedExpectedForm)
    {
        var stage = Stopwatch.StartNew();
        while (!main.TransportConnected || main.ObservedSnapshot?.Players.Count != 1) {
            if (stage.Elapsed >= TimeSpan.FromSeconds(45)) {
                var setupValidation = (setup.FindChild("Validation", true, false) as Label)?.Text ?? "<missing>";
                var connectionStatus = (main.FindChild("ConnectionStatus", true, false) as Label)?.Text ?? "<missing>";
                var playerCount = main.ObservedSnapshot?.Players.Count ?? 0;
                throw new InvalidOperationException(
                    $"Native {phase} did not admit its first participant within 45 seconds: "
                    + $"submissionCount={submissionCount()}; "
                    + $"journeySubmittedExpectedRogueIdentity={journeySubmittedExpectedIdentity()}; "
                    + $"resumeSubmittedExpectedForm={resumeSubmittedExpectedForm()}; "
                    + $"setupVisible={setup.Visible}; transportConnected={main.TransportConnected}; "
                    + $"snapshotPresent={main.ObservedSnapshot is not null}; snapshotPlayerCount={playerCount}; "
                    + $"setupValidation=[{SanitizeNativeStartupText(setupValidation)}]; "
                    + $"connectionStatus=[{SanitizeNativeStartupText(connectionStatus)}].");
            }
            await Frame();
        }
    }

    private static string SanitizeNativeStartupText(string value)
    {
        if (value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("credential", StringComparison.OrdinalIgnoreCase))
            return "[redacted credential-related status]";
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"(?i)(?<![A-Za-z0-9])[A-Za-z0-9_=-]{32,}(?![A-Za-z0-9])",
            "[redacted]");
    }

    private static bool IsTownPortalPreviewResumeProfile()
    {
        var path = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_DEPTH_PROFILE");
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return false;
        using var profile = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        return IsAuthoredDiagnosticDepthProfile(profile.RootElement)
            && IsTownPortalPreviewDepthProfile(profile.RootElement);
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
