using System.Diagnostics;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/** Opt-in real-control menu return/rejoin; no transport or simulation state injection. */
public partial class SessionAcceptance
{
    private void ClickControlWithoutFrame(Button button)
    {
        var center = button.GetGlobalRect().GetCenter();
        Require(button.IsVisibleInTree() && !button.Disabled && GetViewport().GetVisibleRect().HasPoint(center),
            "Session-menu control is unavailable or outside the viewport.");
        Input.ParseInputEvent(new InputEventMouseMotion { Position = center, GlobalPosition = center });
        foreach (var pressed in new[] { true, false })
            Input.ParseInputEvent(new InputEventMouseButton {
                ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center, Pressed = pressed,
            });
        // Refusal must happen before a frame can drain the pending receipt.
        Input.FlushBufferedEvents();
    }

    private async Task VerifySessionMenuAsync(
        Main main, string directory, string phase, bool visible,
        bool pendingReturnRejected, bool bookmarkReturnRejected, int pendingCountAtRefusal)
    {
        await VerifyProfileClassChoicesAsync();
        Require(main.ObservedPendingCommandCount == 0, "Settled menu return requires an empty retry ledger.");
        var initial = main.ObservedSnapshot!.Players.Single().Clone();
        var initialSequence = main.ObservedCommandResult!.ClientSequence;
        var initialGeneration = main.ObservedSessionGeneration;
        var initialModelGeneration = main.ObservedModelGeneration;
        var identity = main.ObservedIdentity!;
        var bookmarkPath = System.IO.Path.Combine(directory, "resume-token.txt");
        var originalBookmark = System.IO.File.ReadAllBytes(bookmarkPath);
        var retainedToken = LocalSessionBookmark.Read(bookmarkPath);
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        await SelectTab((TabContainer)main.FindChild("HudSections", true, false), 0);
        var returnButton = (Button)main.FindChild("ReturnToSessionMenu", true, false);
        Require(returnButton.IsVisibleInTree() && !returnButton.Disabled,
            "Settled menu return is not available through the actual HUD.");
        await Click(returnButton.GetGlobalRect().GetCenter());
        await Until(() => setup.Visible && !main.TransportConnected && !main.ObservedSessionTransition,
            "Settled return did not finish old-connection cleanup and show the setup form.", 10);
        var returnedGeneration = main.ObservedSessionGeneration;
        Require(returnedGeneration > initialGeneration, "Return did not invalidate the old connection generation.");
        var quiet = Stopwatch.StartNew();
        while (quiet.Elapsed.TotalSeconds < 0.5) {
            Require(setup.Visible && !main.TransportConnected && main.ObservedSnapshot is null
                && main.ObservedCommandResult is null && main.ObservedPendingCommandCount == 0
                && main.ObservedPlayerScreenPosition is null && main.ObservedLastQueuedSequence == 0
                && main.ObservedSessionGeneration == returnedGeneration,
                "Old connection work or presentation state survived the settled menu return.");
            await Frame();
        }
        Require(originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath)),
            "Returning to the menu changed the durable resume bookmark.");
        var tokenField = (LineEdit)setup.FindChild("ResumeToken", true, false);
        Require(((OptionButton)setup.FindChild("SessionMode", true, false)).Selected == 1
            && tokenField.Secret && tokenField.Text == retainedToken && !string.IsNullOrWhiteSpace(retainedToken),
            "The menu did not restore the private resume credential and resume selection.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-session-menu.png"));
        await VerifyConnectingReturnAsync(main, setup, directory, phase, bookmarkPath, originalBookmark, retainedToken!);
        await VerifyConfirmedHandshakeTokenAsync(setup, directory, phase, blockBookmark: false);
        await VerifyConfirmedHandshakeTokenAsync(setup, directory, phase, blockBookmark: true);
        returnedGeneration = main.ObservedSessionGeneration;
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count == 1,
            "Resume through the returned setup form did not rejoin the authoritative host.", 15);
        var rejoined = main.ObservedSnapshot!.Players.Single().Clone();
        var rejoinedGeneration = main.ObservedSessionGeneration;
        var rejoinedModelGeneration = main.ObservedModelGeneration;
        Require(!setup.Visible && rejoinedGeneration > returnedGeneration && main.ObservedCommandResult is null,
            "Rejoin did not use a fresh connection/model generation.");
        Require(rejoined.EntityId == initial.EntityId && rejoined.CharacterClassId == initial.CharacterClassId
            && rejoined.CharacterName == initial.CharacterName && rejoined.LevelId == initial.LevelId
            && rejoined.PositionX == initial.PositionX && rejoined.PositionY == initial.PositionY
            && main.ObservedIdentity!.GameMode == identity.GameMode
            && main.ObservedIdentity.ContentManifestHash == identity.ContentManifestHash
            && main.ObservedIdentity.RulesetIdentityHash == identity.RulesetIdentityHash,
            "Menu rejoin changed the authoritative character, position or content/ruleset identity.");
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = true });
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = false });
        await Until(() => IsAcceptedOrSuccessfulDuplicate(main.ObservedCommandResult)
            && main.ObservedCommandResult!.ClientSequence == initialSequence + 1
            && main.ObservedPendingCommandCount == 0
            && main.ObservedSnapshot?.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } player
            && player.PositionX == initial.PositionX && player.PositionY == initial.PositionY + 1,
            "Rejoined input did not settle with the next exact authoritative sequence.", 15);
        var final = main.ObservedSnapshot!.Players.Single();
        Require(originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath)),
            "Same-host rejoin replaced the existing durable resume bookmark.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-session-rejoined.png"));
        WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-return.json", new {
            Passed = true, Phase = phase, Mode = identity.GameMode.ToString(),
            ContentHash = identity.ContentManifestHash, RulesetHash = identity.RulesetIdentityHash,
            initial.EntityId, ClassId = initial.CharacterClassId, initial.LevelId,
            ReturnedToSetup = true, BookmarkPreserved = true, OldWorldCleared = true, Rejoined = true,
            ResumeDefaultsPreserved = true, SettledReturnConfirmed = true,
            ConnectCancellationConfirmed = true, RemoteDefaultsPreserved = true,
            LocalConfirmedTokenPreserved = true, LocalTokenDurableWhenWritable = true,
            InMemoryTokenWarningWhenBlocked = true,
            PendingReturnRejected = pendingReturnRejected, BookmarkReturnRejected = bookmarkReturnRejected,
            PendingCountAtRefusal = pendingCountAtRefusal,
            InitialSequence = initialSequence, FinalSequence = main.ObservedCommandResult!.ClientSequence,
            InitialX = initial.PositionX, InitialY = initial.PositionY,
            RejoinedX = rejoined.PositionX, RejoinedY = rejoined.PositionY,
            FinalX = final.PositionX, FinalY = final.PositionY,
            InitialGeneration = initialGeneration, ReturnedGeneration = returnedGeneration,
            RejoinedGeneration = rejoinedGeneration,
            InitialModelGeneration = initialModelGeneration,
            RejoinedModelGeneration = rejoinedModelGeneration,
        });
        await VerifyLiveClearEventsAcrossModelReplacementAsync(
            main,
            directory,
            phase,
            visible,
            initial,
            initialSequence,
            initialGeneration,
            initialModelGeneration,
            identity,
            bookmarkPath,
            originalBookmark,
            final.Clone(),
            rejoinedGeneration,
            rejoinedModelGeneration);
        await VerifySessionMenuSceneExitAsync(main, directory, phase);
        // The following cold phase, not this delay, proves durable checkpoint restore.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task VerifyLiveClearEventsAcrossModelReplacementAsync(
        Main main,
        string directory,
        string phase,
        bool visible,
        PlayerSnapshot initial,
        ulong initialSequence,
        long initialGeneration,
        long initialModelGeneration,
        AuthoritativeServerIdentity identity,
        string bookmarkPath,
        byte[] originalBookmark,
        PlayerSnapshot priorMenuReturnFinal,
        long firstRejoinedGeneration,
        long firstRejoinedModelGeneration)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var priorMenuReturnSequence = main.ObservedCommandResult!.ClientSequence;
        var firstApproachInitialSequence = priorMenuReturnSequence;
        Require(firstApproachInitialSequence == initialSequence + 1,
            "The supplementary event flow did not begin at the existing settled menu-return result.");
        var target = main.ObservedSnapshot!.TownerGreetings.SingleOrDefault(greeting => greeting.TownerId == 0)
            ?? throw new InvalidOperationException("The rejoined authoritative snapshot did not declare Smith's greeting.");
        var firstPlayer = main.ObservedSnapshot.Players.Single();
        var firstApproachAccepted = true;
        if (!AuthoritativeInteractionRange.IsAdjacent(firstPlayer, target.PositionX, target.PositionY)) {
            await ApproachDeclaredDestination(main, "towner:0");
            var firstApproachCancelTravel = (Button)main.FindChild("CancelTravel", true, false);
            await Until(() => main.ObservedCommandResult is { } settledFirstApproach
                    && settledFirstApproach.ClientSequence == main.ObservedLastQueuedSequence
                    && main.ObservedPendingCommandCount == 0
                    && IsAcceptedOrSuccessfulDuplicate(settledFirstApproach)
                    && main.ObservedSnapshot!.Tick >= settledFirstApproach.AppliedTick
                    && Settled(main)
                    && firstApproachCancelTravel.Disabled,
                "The final towner approach acknowledgement did not settle in the client presentation.", 15);
        }
        var firstApproachResult = main.ObservedCommandResult
            ?? throw new InvalidOperationException("No authoritative result followed the towner approach.");
        firstApproachAccepted &= IsAcceptedOrSuccessfulDuplicate(firstApproachResult);
        var firstApproachFinalSequence = firstApproachResult.ClientSequence;
        Require(firstApproachAccepted && main.ObservedPendingCommandCount == 0
            && AuthoritativeInteractionRange.IsAdjacent(main.ObservedSnapshot!.Players.Single(), target.PositionX, target.PositionY),
            "The server-owned approach did not settle beside the live towner.");

        await SelectTab(sections, 4);
        var firstDialogue = await ClickLiveTownerAndWaitForDialogue(main, target, firstApproachFinalSequence);
        Require(firstDialogue.CommandSequence == firstApproachFinalSequence + 1,
            "The first actual towner click did not receive the exact next command acknowledgement.");
        var firstDialogueSessionGeneration = main.ObservedSessionGeneration;
        var firstDialogueModelGeneration = main.ObservedModelGeneration;
        var firstClearModelGeneration = main.ObservedModelGeneration;
        var firstClearGeneration = main.ObservedSessionGeneration;
        Require(firstClearModelGeneration == firstRejoinedModelGeneration
            && firstClearGeneration == firstRejoinedGeneration,
            "The first Clear-events click is not bound to the first rejoined model and session.");
        var firstClearStartSequence = main.ObservedRecentEventSequenceStart;
        var firstClearEndSequence = main.ObservedRecentEventSequenceEnd;
        var firstClearEventCount = main.ObservedRecentEventCount;
        var firstClearEventBatchCount = checked((int)main.ObservedEventBatchCount);
        var firstClearEventKind = main.ObservedRecentEventKind;
        Require(firstClearEventCount > 0 && firstClearEventBatchCount > 0
            && firstClearEventKind == GameEvent.EventOneofCase.Dialogue.ToString()
            && firstClearEndSequence > firstClearStartSequence,
            "The real first towner click did not append live authoritative dialogue history.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-menu-events-before-clear.png"));
        var firstClearCommandSequenceBeforeClick = main.ObservedCommandResult?.ClientSequence ?? 0;
        var firstClearLastQueuedSequenceBeforeClick = main.ObservedLastQueuedSequence;
        var firstClear = await ClickClearEventsAndObserveEmptyAsync(main, sections, "first");
        var firstClearCommandSequenceAfterClick = main.ObservedCommandResult?.ClientSequence ?? 0;
        var firstClearLastQueuedSequenceAfterClick = main.ObservedLastQueuedSequence;
        var firstClearSessionGenerationAfterClick = main.ObservedSessionGeneration;
        var firstClearModelGenerationAfterClick = main.ObservedModelGeneration;
        if (visible)
            await Capture(System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-menu-events-after-clear.png"));

        var firstPostClearCommandSequenceBeforeClick = main.ObservedCommandResult?.ClientSequence ?? 0;
        var firstPostClearLastQueuedSequenceBeforeClick = main.ObservedLastQueuedSequence;
        var firstPostClearSessionGenerationBeforeClick = main.ObservedSessionGeneration;
        var firstPostClearModelGenerationBeforeClick = main.ObservedModelGeneration;
        var firstPostClearEventBatchCountBeforeClick = main.ObservedEventBatchCount;
        var firstPostClearEventCountBeforeClick = main.ObservedRecentEventCount;
        var firstPostClearEventSequenceStartBeforeClick = main.ObservedRecentEventSequenceStart;
        var firstPostClearEventSequenceEndBeforeClick = main.ObservedRecentEventSequenceEnd;
        var firstPostClearDialogue = await ClickLiveTownerAndWaitForDialogue(main, target);
        var firstPostClearSequenceMatched = firstPostClearDialogue.CommandSequence == firstDialogue.CommandSequence + 1;
        var firstPostClearEventsObserved = firstPostClearDialogue.EventCount > 0
            && firstPostClearDialogue.EventBatchDelta > 0;
        if (!firstPostClearSequenceMatched || !firstPostClearEventsObserved) {
            try {
                WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-events-post-clear-failure.json", new {
                    Passed = false,
                    Phase = phase,
                    FirstDialogueSequence = firstDialogue.CommandSequence,
                    FirstDialogueEventBatchDelta = firstDialogue.EventBatchDelta,
                    FirstDialogueEventCount = firstDialogue.EventCount,
                    FirstDialogueEventSequenceStart = firstDialogue.StartSequence,
                    FirstDialogueEventSequenceEnd = firstDialogue.EndSequence,
                    FirstDialogueSessionGeneration = firstDialogueSessionGeneration,
                    FirstDialogueModelGeneration = firstDialogueModelGeneration,
                    FirstClearCommandSequenceBeforeClick = firstClearCommandSequenceBeforeClick,
                    FirstClearLastQueuedSequenceBeforeClick = firstClearLastQueuedSequenceBeforeClick,
                    FirstClearCommandSequenceAfterClick = firstClearCommandSequenceAfterClick,
                    FirstClearLastQueuedSequenceAfterClick = firstClearLastQueuedSequenceAfterClick,
                    FirstClearSessionGenerationAfterClick = firstClearSessionGenerationAfterClick,
                    FirstClearModelGenerationAfterClick = firstClearModelGenerationAfterClick,
                    FirstClearRemainingCount = firstClear.RemainingCount,
                    FirstClearRemainingStartSequence = firstClear.RemainingStartSequence,
                    FirstClearRemainingEndSequence = firstClear.RemainingEndSequence,
                    FirstPostClearCommandSequenceBeforeClick = firstPostClearCommandSequenceBeforeClick,
                    FirstPostClearLastQueuedSequenceBeforeClick = firstPostClearLastQueuedSequenceBeforeClick,
                    FirstPostClearSessionGenerationBeforeClick = firstPostClearSessionGenerationBeforeClick,
                    FirstPostClearModelGenerationBeforeClick = firstPostClearModelGenerationBeforeClick,
                    FirstPostClearEventBatchCountBeforeClick = firstPostClearEventBatchCountBeforeClick,
                    FirstPostClearEventCountBeforeClick = firstPostClearEventCountBeforeClick,
                    FirstPostClearEventSequenceStartBeforeClick = firstPostClearEventSequenceStartBeforeClick,
                    FirstPostClearEventSequenceEndBeforeClick = firstPostClearEventSequenceEndBeforeClick,
                    FirstPostClearDialogueSequence = firstPostClearDialogue.CommandSequence,
                    FirstPostClearDialogueEventBatchDelta = firstPostClearDialogue.EventBatchDelta,
                    FirstPostClearDialogueEventCount = firstPostClearDialogue.EventCount,
                    FirstPostClearDialogueEventKind = firstPostClearDialogue.EventKind,
                    FirstPostClearDialogueEventSequenceStart = firstPostClearDialogue.StartSequence,
                    FirstPostClearDialogueEventSequenceEnd = firstPostClearDialogue.EndSequence,
                    SessionGenerationAtFailure = main.ObservedSessionGeneration,
                    ModelGenerationAtFailure = main.ObservedModelGeneration,
                    CommandSequenceAtFailure = main.ObservedCommandResult?.ClientSequence ?? 0,
                    LastQueuedSequenceAtFailure = main.ObservedLastQueuedSequence,
                    EventBatchCountAtFailure = main.ObservedEventBatchCount,
                    EventCountAtFailure = main.ObservedRecentEventCount,
                    EventSequenceStartAtFailure = main.ObservedRecentEventSequenceStart,
                    EventSequenceEndAtFailure = main.ObservedRecentEventSequenceEnd,
                    SameSessionAndModel = firstDialogueSessionGeneration == main.ObservedSessionGeneration
                        && firstDialogueModelGeneration == main.ObservedModelGeneration,
                    FirstPostClearSequenceMatched = firstPostClearSequenceMatched,
                    FirstPostClearEventsObserved = firstPostClearEventsObserved,
                });
            } catch (Exception evidenceException) {
                try {
                    GD.PushWarning($"Could not persist post-clear failure evidence ({evidenceException.GetType().Name}).");
                } catch {
                    // Preserve the original acceptance failure if Godot logging is unavailable.
                }
            }
        }
        Require(firstPostClearSequenceMatched && firstPostClearEventsObserved,
            "A new actual towner click did not repopulate history after Clear-events.");
        var firstPostClearEventBatchCount = checked((int)main.ObservedEventBatchCount);
        var firstPostClearPlayer = main.ObservedSnapshot!.Players.Single().Clone();

        await SelectTab(sections, 0);
        var returnButton = (Button)main.FindChild("ReturnToSessionMenu", true, false);
        Require(returnButton.IsVisibleInTree() && !returnButton.Disabled,
            "The second settled menu return is unavailable through the actual HUD.");
        await Click(returnButton.GetGlobalRect().GetCenter());
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        await Until(() => setup.Visible && !main.TransportConnected && !main.ObservedSessionTransition
            && main.ObservedSnapshot is null && main.ObservedCommandResult is null,
            "The second menu return did not finish old-model cleanup.", 10);
        var secondReturnedGeneration = main.ObservedSessionGeneration;
        var secondReturnedModelGeneration = main.ObservedModelGeneration;
        Require(secondReturnedGeneration > firstClearGeneration
            && secondReturnedModelGeneration > firstClearModelGeneration,
            "The second return did not advance session and model generations.");
        Require(originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath)),
            "The second menu return changed the durable local bookmark.");
        var secondResumeTokenField = (LineEdit)setup.FindChild("ResumeToken", true, false);
        Require(((OptionButton)setup.FindChild("SessionMode", true, false)).Selected == 1
            && secondResumeTokenField.Secret && !string.IsNullOrWhiteSpace(secondResumeTokenField.Text),
            "The second return lost the private resume selection or credential.");
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count == 1,
            "The second actual setup-form rejoin did not restore the authoritative player.", 15);
        var secondRejoined = main.ObservedSnapshot!.Players.Single();
        var secondRejoinedGeneration = main.ObservedSessionGeneration;
        var secondRejoinedModelGeneration = main.ObservedModelGeneration;
        Require(secondRejoinedGeneration > secondReturnedGeneration
            && secondRejoinedModelGeneration == secondReturnedModelGeneration
            && secondRejoined.EntityId == initial.EntityId
            && secondRejoined.PositionX == firstPostClearPlayer.PositionX
            && secondRejoined.PositionY == firstPostClearPlayer.PositionY,
            "The second rejoin did not retain the returned model and settled authoritative position.");
        Require(originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath)),
            "The second host rejoin replaced the existing durable bookmark.");
        await SelectTab(sections, 4);
        var secondTarget = main.ObservedSnapshot.TownerGreetings.SingleOrDefault(greeting => greeting.TownerId == 0)
            ?? throw new InvalidOperationException("The second rejoined snapshot did not declare Smith's greeting.");
        if (!AuthoritativeInteractionRange.IsAdjacent(secondRejoined, secondTarget.PositionX, secondTarget.PositionY)) {
            await ApproachDeclaredDestination(main, "towner:0");
            var secondApproachCancelTravel = (Button)main.FindChild("CancelTravel", true, false);
            await Until(() => main.ObservedCommandResult is { } settledSecondApproach
                    && settledSecondApproach.ClientSequence == main.ObservedLastQueuedSequence
                    && main.ObservedPendingCommandCount == 0
                    && IsAcceptedOrSuccessfulDuplicate(settledSecondApproach)
                    && main.ObservedSnapshot!.Tick >= settledSecondApproach.AppliedTick
                    && Settled(main)
                    && secondApproachCancelTravel.Disabled,
                "The final second-model towner approach acknowledgement did not settle in the client presentation.", 15);
        }
        var secondApproachResult = main.ObservedCommandResult;
        var secondApproachFinalSequence = secondApproachResult?.ClientSequence
            ?? firstPostClearDialogue.CommandSequence;
        Require((secondApproachResult is null || IsAcceptedOrSuccessfulDuplicate(secondApproachResult))
            && main.ObservedPendingCommandCount == 0
            && AuthoritativeInteractionRange.IsAdjacent(main.ObservedSnapshot!.Players.Single(), secondTarget.PositionX, secondTarget.PositionY),
            "The second model did not settle beside the live towner.");
        var secondDialogue = await ClickLiveTownerAndWaitForDialogue(main, secondTarget, secondApproachFinalSequence);
        Require(secondDialogue.CommandSequence == firstPostClearDialogue.CommandSequence + 1
            && secondDialogue.CommandSequence == secondApproachFinalSequence + 1,
            "The second-model dialogue did not continue the authoritative command sequence.");
        var secondClearModelGeneration = main.ObservedModelGeneration;
        var secondClearGeneration = main.ObservedSessionGeneration;
        Require(secondClearModelGeneration == secondReturnedModelGeneration
            && secondClearModelGeneration == secondRejoinedModelGeneration
            && secondClearGeneration == secondRejoinedGeneration,
            "The second Clear-events click was not bound to the retained model and rejoined session.");
        var secondClearStartSequence = main.ObservedRecentEventSequenceStart;
        var secondClearEndSequence = main.ObservedRecentEventSequenceEnd;
        var secondClearEventCount = main.ObservedRecentEventCount;
        var secondClearEventBatchCount = checked((int)main.ObservedEventBatchCount);
        var secondClearEventBatchDelta = checked((int)secondDialogue.EventBatchDelta);
        var secondClearEventKind = main.ObservedRecentEventKind;
        Require(secondClearEventCount > 0 && secondClearEventBatchCount > 0
            && secondClearEventKind == GameEvent.EventOneofCase.Dialogue.ToString()
            && secondClearEndSequence > secondClearStartSequence,
            "The second actual towner click did not append events to the replacement model.");
        var secondClear = await ClickClearEventsAndObserveEmptyAsync(main, sections, "second");

        var finalMoveTarget = SelectAdjacentOpenWorldTile(main, secondTarget);
        var finalMovementInitialSequence = main.ObservedCommandResult!.ClientSequence;
        var finalMovementLastQueuedSequenceBeforeClick = main.ObservedLastQueuedSequence;
        var finalMovementModelGeneration = main.ObservedModelGeneration;
        var finalMovementSessionGeneration = main.ObservedSessionGeneration;
        var finalMovementPlayerBefore = main.ObservedSnapshot!.Players.Single().Clone();
        var finalMovementClickPoint = main.ObservedTileScreenPosition(finalMoveTarget.X, finalMoveTarget.Y);
        var finalMovementViewport = GetViewport().GetVisibleRect();
        var finalMovementClickCompleted = false;
        try {
            await Click(finalMovementClickPoint, walk: true);
            finalMovementClickCompleted = true;
            await Until(() => main.ObservedCommandResult is { } result
                && result.ClientSequence == finalMovementInitialSequence + 1
                && IsAcceptedOrSuccessfulDuplicate(result)
                && main.ObservedPendingCommandCount == 0
                && main.ObservedSnapshot?.Players.Single() is { } player
                && player.PositionX == finalMoveTarget.X && player.PositionY == finalMoveTarget.Y
                && player.CombatMode == PlayerCombatMode.Stand && player.CurrentAction is null,
                "The second rejoined model did not settle after its next actual world-tile movement.", 15);
        } catch (InvalidOperationException exception) {
            var resultAtFailure = main.ObservedCommandResult;
            var playerAtFailure = main.ObservedSnapshot?.Players.SingleOrDefault();
            try {
                WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-final-movement-failure.json", new {
                    Passed = false,
                    Phase = phase,
                    FailureType = exception.GetType().Name,
                    ClickCompleted = finalMovementClickCompleted,
                    BaselineCommandSequence = finalMovementInitialSequence,
                    LastQueuedSequenceBeforeClick = finalMovementLastQueuedSequenceBeforeClick,
                    ResultCommandSequenceAtFailure = resultAtFailure?.ClientSequence,
                    LastQueuedSequenceAtFailure = main.ObservedLastQueuedSequence,
                    ResultStatusAtFailure = resultAtFailure?.Status.ToString(),
                    ResultRejectReasonAtFailure = resultAtFailure?.RejectReason.ToString(),
                    ResultAppliedTickAtFailure = resultAtFailure?.AppliedTick,
                    TargetX = finalMoveTarget.X,
                    TargetY = finalMoveTarget.Y,
                    PlayerXBeforeClick = finalMovementPlayerBefore.PositionX,
                    PlayerYBeforeClick = finalMovementPlayerBefore.PositionY,
                    PlayerCombatModeBeforeClick = finalMovementPlayerBefore.CombatMode.ToString(),
                    PlayerActionPresentBeforeClick = finalMovementPlayerBefore.CurrentAction is not null,
                    PlayerXAtFailure = playerAtFailure?.PositionX,
                    PlayerYAtFailure = playerAtFailure?.PositionY,
                    PlayerCombatModeAtFailure = playerAtFailure?.CombatMode.ToString(),
                    PlayerActionPresentAtFailure = playerAtFailure?.CurrentAction is not null,
                    PendingCommandCountAtFailure = main.ObservedPendingCommandCount,
                    ModelGenerationAtClick = finalMovementModelGeneration,
                    ModelGenerationAtFailure = main.ObservedModelGeneration,
                    SessionGenerationAtClick = finalMovementSessionGeneration,
                    SessionGenerationAtFailure = main.ObservedSessionGeneration,
                    ClickPointX = finalMovementClickPoint.X,
                    ClickPointY = finalMovementClickPoint.Y,
                    ViewportX = finalMovementViewport.Position.X,
                    ViewportY = finalMovementViewport.Position.Y,
                    ViewportWidth = finalMovementViewport.Size.X,
                    ViewportHeight = finalMovementViewport.Size.Y,
                    ClickPointInsideViewport = finalMovementViewport.HasPoint(finalMovementClickPoint),
                });
            } catch (Exception evidenceException) {
                try {
                    GD.PushWarning($"Could not persist final-movement failure evidence ({evidenceException.GetType().Name}).");
                } catch {
                    // Preserve the original acceptance failure if Godot logging is unavailable.
                }
            }
            throw;
        }
        var finalPlayer = main.ObservedSnapshot!.Players.Single();
        var identityPreserved = main.ObservedIdentity is { } currentIdentity
            && currentIdentity.GameMode == identity.GameMode
            && currentIdentity.ContentManifestHash == identity.ContentManifestHash
            && currentIdentity.RulesetIdentityHash == identity.RulesetIdentityHash;
        var bookmarkPreserved = originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath));
        Require(identityPreserved && bookmarkPreserved && finalPlayer.EntityId == initial.EntityId,
            "The second model changed host identity, actor identity or the durable local bookmark.");
        WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-events.json", new {
            Passed = true,
            Phase = phase,
            ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash,
            EntityId = finalPlayer.EntityId,
            InitialSequence = initialSequence,
            InitialX = initial.PositionX,
            InitialY = initial.PositionY,
            InitialGeneration = initialGeneration,
            InitialModelGeneration = initialModelGeneration,
            PriorMenuReturnSequence = priorMenuReturnSequence,
            PriorMenuReturnX = priorMenuReturnFinal.PositionX,
            PriorMenuReturnY = priorMenuReturnFinal.PositionY,
            FirstApproachAccepted = firstApproachAccepted,
            FirstApproachInitialSequence = firstApproachInitialSequence,
            FirstApproachFinalSequence = firstApproachFinalSequence,
            FirstClearDialogueSequence = firstDialogue.CommandSequence,
            FirstClearGeneration = firstClearGeneration,
            FirstClearModelGeneration = firstClearModelGeneration,
            FirstClearEventBatchCount = firstClearEventBatchCount,
            FirstClearEventCount = firstClearEventCount,
            FirstClearEventKind = firstClearEventKind,
            FirstClearStartSequence = firstClearStartSequence,
            FirstClearEndSequence = firstClearEndSequence,
            FirstClearButtonEnabled = firstClear.ButtonEnabled,
            FirstClearButtonClicked = firstClear.ButtonClicked,
            FirstClearRemainingCount = firstClear.RemainingCount,
            FirstClearRemainingStartSequence = firstClear.RemainingStartSequence,
            FirstClearRemainingEndSequence = firstClear.RemainingEndSequence,
            FirstPostClearDialogueSequence = firstPostClearDialogue.CommandSequence,
            FirstPostClearEventBatchCount = firstPostClearEventBatchCount,
            FirstPostClearEventBatchDelta = checked((int)firstPostClearDialogue.EventBatchDelta),
            FirstPostClearEventCount = firstPostClearDialogue.EventCount,
            FirstPostClearEventKind = firstPostClearDialogue.EventKind,
            SecondReturnedGeneration = secondReturnedGeneration,
            SecondReturnedModelGeneration = secondReturnedModelGeneration,
            SecondRejoinedGeneration = secondRejoinedGeneration,
            SecondRejoinedModelGeneration = secondRejoinedModelGeneration,
            SecondModelDialogueSequence = secondDialogue.CommandSequence,
            SecondClearGeneration = secondClearGeneration,
            SecondClearModelGeneration = secondClearModelGeneration,
            SecondClearEventBatchCount = secondClearEventBatchCount,
            SecondClearEventBatchDelta = secondClearEventBatchDelta,
            SecondClearEventCount = secondClearEventCount,
            SecondClearEventKind = secondClearEventKind,
            SecondClearStartSequence = secondClearStartSequence,
            SecondClearEndSequence = secondClearEndSequence,
            SecondClearButtonEnabled = secondClear.ButtonEnabled,
            SecondClearButtonClicked = secondClear.ButtonClicked,
            SecondClearRemainingCount = secondClear.RemainingCount,
            SecondClearRemainingStartSequence = secondClear.RemainingStartSequence,
            SecondClearRemainingEndSequence = secondClear.RemainingEndSequence,
            FinalMovementAccepted = true,
            FinalMovementSequence = main.ObservedCommandResult!.ClientSequence,
            FinalCommandSequence = main.ObservedCommandResult.ClientSequence,
            FinalX = finalPlayer.PositionX,
            FinalY = finalPlayer.PositionY,
            BookmarkBytesPreserved = bookmarkPreserved,
            IdentityPreserved = identityPreserved,
            RejoinedAgain = true,
            ClearEventsVerified = true,
        });
    }

    private async Task<LiveDialogueObservation> ClickLiveTownerAndWaitForDialogue(
        Main main,
        TownerGreetingSnapshot target,
        ulong? priorCommandSequence = null)
    {
        var previousSequence = priorCommandSequence ?? main.ObservedCommandResult?.ClientSequence ?? 0;
        var previousBatchCount = main.ObservedEventBatchCount;
        await Click(main.ObservedTileScreenPosition(target.PositionX, target.PositionY));
        await Until(() => main.ObservedCommandResult is { } result
            && result.ClientSequence == previousSequence + 1
            && result.ClientSequence == main.ObservedLastQueuedSequence
            && IsAcceptedOrSuccessfulDuplicate(result)
            && main.ObservedPendingCommandCount == 0
            && main.ObservedEventBatchCount > previousBatchCount
            && main.ObservedRecentEventCount > 0
            && main.ObservedRecentEventKind == GameEvent.EventOneofCase.Dialogue.ToString(),
            "The actual world click did not yield an accepted live authoritative towner dialogue event.", 15);
        return new LiveDialogueObservation(
            main.ObservedCommandResult!.ClientSequence,
            main.ObservedEventBatchCount - previousBatchCount,
            main.ObservedRecentEventCount,
            main.ObservedRecentEventKind!,
            main.ObservedRecentEventSequenceStart,
            main.ObservedRecentEventSequenceEnd);
    }

    private async Task<ClearEventsObservation> ClickClearEventsAndObserveEmptyAsync(
        Main main,
        TabContainer sections,
        string which)
    {
        var button = (Button)main.FindChild("ClearEvents", true, false);
        await RevealControl(sections, 4, button);
        var enabled = !button.Disabled;
        Require(enabled && main.ObservedRecentEventCount > 0,
            $"The {which} Clear-events button was unavailable or had no live events to clear.");
        await Click(button.GetGlobalRect().GetCenter());
        await Until(() => main.ObservedRecentEventCount == 0
            && main.ObservedRecentEventSequenceStart == main.ObservedRecentEventSequenceEnd,
            $"The {which} real Clear-events HUD click did not empty the active model history.", 5);
        return new ClearEventsObservation(
            enabled,
            true,
            main.ObservedRecentEventCount,
            main.ObservedRecentEventSequenceStart,
            main.ObservedRecentEventSequenceEnd);
    }

    private static (int X, int Y) SelectAdjacentOpenWorldTile(Main main, TownerGreetingSnapshot target)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId);
        bool IsUnoccupiedWalkable(int x, int y)
            => AuthoritativeLevelGeometryCellResolver.Resolve(geometry, x, y)
                    == AuthoritativeLevelGeometryCell.Walkable
                && !geometry.WarpTriggers.Any(warp => warp.TriggerX == x && warp.TriggerY == y)
                && !snapshot.TownerGreetings.Any(greeting => greeting.LevelId == player.LevelId
                    && greeting.PositionX == x && greeting.PositionY == y)
                && !snapshot.Players.Any(other => other.EntityId != player.EntityId
                    && other.LevelId == player.LevelId && other.PositionX == x && other.PositionY == y)
                && !snapshot.Monsters.Any(monster => monster.Alive && monster.LevelId == player.LevelId
                    && monster.PositionX == x && monster.PositionY == y)
                && !snapshot.WorldItems.Any(item => item.LevelId == player.LevelId
                    && item.PositionX == x && item.PositionY == y)
                && !snapshot.Objects.Any(@object => @object.LevelId == player.LevelId && !@object.Destroyed
                    && @object.PositionX == x && @object.PositionY == y);
        var candidates = new[] { (X: player.PositionX, Y: player.PositionY - 1),
                (X: player.PositionX + 1, Y: player.PositionY),
                (X: player.PositionX, Y: player.PositionY + 1),
                (X: player.PositionX - 1, Y: player.PositionY) }
            .Where(cell => IsUnoccupiedWalkable(cell.X, cell.Y)
                && IsUnoccupiedWalkable(cell.X, cell.Y + 1)
                && IsUnoccupiedWalkable(cell.X, cell.Y + 2))
            .OrderByDescending(cell => Math.Abs(cell.X - target.PositionX) + Math.Abs(cell.Y - target.PositionY))
            .ToArray();
        Require(candidates.Length > 0,
            "No adjacent open world tile preserves the next two ordinary Down inputs for Resume and menu rejoin.");
        return candidates[0];
    }

    private async Task VerifySessionMenuSceneExitAsync(Main owner, string directory, string phase)
    {
        owner.Visible = false;
        try {
            var handshake = await VerifyPendingHandshakeExitCaseAsync(directory, phase, duringReturnCleanup: false);
            var returnCleanup = await VerifyPendingHandshakeExitCaseAsync(directory, phase, duringReturnCleanup: true);
            WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-exit.json", new {
                Passed = true,
                Phase = phase,
                ExitDuringHandshakeVerified = handshake.Verified,
                HandshakeExitPendingObserved = handshake.PendingObserved,
                HandshakeExitMainFreed = handshake.MainFreed,
                HandshakeExitPeerClosed = handshake.PeerClosed,
                HandshakeExitCleanupCompleted = handshake.CleanupCompleted,
                HandshakeExitCleanupFailureCount = handshake.CleanupFailureCount,
                HandshakeExitReturnCleanupJoined = handshake.ReturnCleanupJoined,
                HandshakeExitPostExitMenuPublicationAttemptCount = handshake.PostExitPublicationAttemptCount,
                HandshakeExitNoPostExitMenuPublication = handshake.NoPostExitMenuPublication,
                ExitDuringReturnVerified = returnCleanup.Verified,
                ReturnExitPendingObserved = returnCleanup.PendingObserved,
                ReturnExitCleanupGateObserved = returnCleanup.GateObserved,
                ReturnExitStillPendingWhileGated = returnCleanup.StillPendingWhileGated,
                ReturnExitMainFreed = returnCleanup.MainFreed,
                ReturnExitPeerClosed = returnCleanup.PeerClosed,
                ReturnExitCleanupCompleted = returnCleanup.CleanupCompleted,
                ReturnExitCleanupFailureCount = returnCleanup.CleanupFailureCount,
                ReturnExitReturnCleanupJoined = returnCleanup.ReturnCleanupJoined,
                ReturnExitPostExitMenuPublicationAttemptCount = returnCleanup.PostExitPublicationAttemptCount,
                ReturnExitNoPostExitMenuPublication = returnCleanup.NoPostExitMenuPublication,
            });
        } finally {
            owner.Visible = true;
        }
    }

    private async Task<ExitCaseObservation> VerifyPendingHandshakeExitCaseAsync(
        string directory,
        string phase,
        bool duringReturnCleanup)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stageDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var suffix = duringReturnCleanup ? "return-exit" : "handshake-exit";
        var fixtureDirectory = System.IO.Path.Combine(directory, phase.ToLowerInvariant() + "-" + suffix);
        System.IO.Directory.CreateDirectory(fixtureDirectory);
        var fixtureBookmark = System.IO.Path.Combine(fixtureDirectory, "resume-token.txt");
        var overrides = new Dictionary<string, string?> {
            ["DEVILUTION_SERVER_HOST"] = "127.0.0.1",
            ["DEVILUTION_SERVER_PORT"] = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(),
            ["DEVILUTION_RESUME_TOKEN_PATH"] = fixtureBookmark,
            ["DEVILUTION_RESUME_TOKEN"] = null,
            ["DEVILUTION_CHARACTER_CLASS_ID"] = null,
            ["DEVILUTION_CHARACTER_NAME"] = null,
        };
        var savedEnvironment = overrides.Keys.ToDictionary(name => name,
            name => System.Environment.GetEnvironmentVariable(name));
        var gate = duringReturnCleanup
            ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
            : null;
        Main? fixture = null;
        TcpClient? peer = null;
        NetworkStream? peerStream = null;
        Task<MainExitCleanupObservation>? cleanupTask = null;
        var mainFreed = false;
        var peerClosed = false;
        var cleanup = new MainExitCleanupObservation(false, false, -1, -1, false);
        var pendingObserved = false;
        var gateObserved = false;
        var stillPendingWhileGated = false;
        try {
            try {
                foreach (var (name, value) in overrides)
                    System.Environment.SetEnvironmentVariable(name, value);
                fixture = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
                if (gate is not null)
                    fixture.AcceptanceReturnCleanupGate = gate.Task;
                AddChild(fixture);
                cleanupTask = fixture.ObservedExitCleanupCompletion;
            } finally {
                foreach (var (name, value) in savedEnvironment)
                    System.Environment.SetEnvironmentVariable(name, value);
            }
            await Frame();
            await Frame();
            var setup = fixture!.GetNode<CharacterSetupPresentation>("CharacterSetup");
            Require(setup.Visible && !fixture.TransportConnected,
                "The pending-exit fixture did not start at the actual character setup form.");
            var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
            mode.Select(0);
            mode.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = "Exit-Fixture";
            await Frame();
            await Frame();
            await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
            Require(!setup.Visible && fixture.ObservedSessionGeneration == 1,
                "The actual setup Connect control did not begin the isolated pending handshake.");
            peer = await listener.AcceptTcpClientAsync(stageDeadline.Token);
            peerStream = peer.GetStream();
            await ReadClientHelloAsync(peerStream, stageDeadline.Token);
            pendingObserved = !fixture.TransportConnected && fixture.ObservedSnapshot is null;
            Require(pendingObserved, "The loopback peer did not hold a real, unpublished ServerHello handshake.");

            if (duringReturnCleanup) {
                var sections = (TabContainer)fixture.FindChild("HudSections", true, false);
                var returnButton = (Button)fixture.FindChild("ReturnToSessionMenu", true, false);
                await RevealControl(sections, 0, returnButton);
                await Click(returnButton.GetGlobalRect().GetCenter());
                await Until(() => fixture.ObservedReturnCleanupGateEntered,
                    "The actual return control never reached its joined cleanup gate.", 10);
                gateObserved = fixture.ObservedReturnCleanupActive && fixture.ObservedReturnCleanupGateEntered;
                Require(gateObserved, "The return cleanup was not active at the acceptance-only gate.");
            }

            fixture.QueueFree();
            await Until(() => !GodotObject.IsInstanceValid(fixture),
                "QueueFree did not remove the actual Main scene node.", 10);
            mainFreed = !GodotObject.IsInstanceValid(fixture);
            if (duringReturnCleanup) {
                await Frame();
                stillPendingWhileGated = !cleanupTask.IsCompleted;
                Require(stillPendingWhileGated,
                    "The actual scene exit finished before the active return cleanup gate was released.");
                gate!.TrySetResult(true);
            }
            cleanup = await cleanupTask.WaitAsync(stageDeadline.Token);
            peerClosed = await ObservePeerClosureAsync(peerStream, stageDeadline.Token);
            var verified = pendingObserved && mainFreed && peerClosed && cleanup.Completed
                && cleanup.FailureCount == 0
                && (!duringReturnCleanup || cleanup.ReturnCleanupJoined)
                && cleanup.PostExitMenuPublicationAttemptCount == 0
                && cleanup.NoPostExitMenuPublication
                && (!duringReturnCleanup || gateObserved && stillPendingWhileGated);
            Require(verified,
                $"The {(duringReturnCleanup ? "return-cleanup" : "handshake")} scene-exit evidence was incomplete.");
            return new ExitCaseObservation(
                verified,
                pendingObserved,
                gateObserved,
                stillPendingWhileGated,
                mainFreed,
                peerClosed,
                cleanup.Completed,
                cleanup.FailureCount,
                cleanup.ReturnCleanupJoined,
                cleanup.PostExitMenuPublicationAttemptCount,
                cleanup.NoPostExitMenuPublication);
        } finally {
            gate?.TrySetResult(true);
            if (fixture is not null && GodotObject.IsInstanceValid(fixture))
                fixture.QueueFree();
            if (cleanupTask is not null && !cleanupTask.IsCompleted) {
                try {
                    await cleanupTask.WaitAsync(TimeSpan.FromSeconds(10));
                } catch (TimeoutException) {
                    GD.PushWarning("A bounded pending-exit acceptance fixture did not finish cleanup.");
                }
            }
            peerStream?.Dispose();
            peer?.Dispose();
            listener.Stop();
        }
    }

    private static async Task ReadClientHelloAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Require(length is > 0 and <= 65536, "The pending-exit peer received an invalid handshake frame.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        var envelope = Envelope.Parser.ParseFrom(payload);
        Require(envelope.PayloadCase == Envelope.PayloadOneofCase.ClientHello,
            "The pending-exit scene did not submit ClientHello through the real transport.");
    }

    private static async Task<bool> ObservePeerClosureAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try {
            return await stream.ReadAsync(new byte[1], cancellationToken) == 0;
        } catch (IOException exception) when (exception.InnerException is SocketException socket
            && socket.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown) {
            return true;
        } catch (SocketException exception) when (
            exception.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown) {
            return true;
        }
    }

    private sealed record LiveDialogueObservation(
        ulong CommandSequence,
        ulong EventBatchDelta,
        int EventCount,
        string EventKind,
        ulong StartSequence,
        ulong EndSequence);

    private sealed record ClearEventsObservation(
        bool ButtonEnabled,
        bool ButtonClicked,
        int RemainingCount,
        ulong RemainingStartSequence,
        ulong RemainingEndSequence);

    private sealed record ExitCaseObservation(
        bool Verified,
        bool PendingObserved,
        bool GateObserved,
        bool StillPendingWhileGated,
        bool MainFreed,
        bool PeerClosed,
        bool CleanupCompleted,
        int CleanupFailureCount,
        bool ReturnCleanupJoined,
        long PostExitPublicationAttemptCount,
        bool NoPostExitMenuPublication);

    private async Task VerifyConnectingReturnAsync(
        Main main,
        CharacterSetupPresentation setup,
        string directory,
        string phase,
        string bookmarkPath,
        byte[] originalBookmark,
        string retainedToken)
    {
        // An owned loopback peer withholds ServerHello. This is a real pending
        // handshake, not a sleep-based assumption about connection state.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stageDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var portField = (SpinBox)setup.FindChild("ServerPort", true, false);
        var actualPort = (int)portField.Value;
        var withheldPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        portField.Value = withheldPort;
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        using var peer = await listener.AcceptTcpClientAsync(stageDeadline.Token);
        await using var stream = peer.GetStream();
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, stageDeadline.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Require(length is > 0 and <= 65536, "Pending connection wrote an invalid handshake frame.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, stageDeadline.Token);
        var hello = Envelope.Parser.ParseFrom(payload);
        Require(hello.PayloadCase == Envelope.PayloadOneofCase.ClientHello
            && hello.ClientHello.ResumeToken == retainedToken && !main.TransportConnected
            && main.ObservedSnapshot is null && main.ObservedPendingCommandCount == 0,
            "The edited endpoint did not enter the real pending resume handshake.");
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var returnButton = main.FindChild("ReturnToSessionMenu", true, false) as Button;
        Require(returnButton is not null,
            "The actual Return-to-session-menu HUD control was not found during a pending handshake.");
        await RevealControl(sections, 0, returnButton!);
        var heroPage = sections.GetChildCount() > 0 ? sections.GetChild(0) as Control : null;
        var hudVisibleBeforeClick = sections.IsVisibleInTree();
        var currentHudTabBeforeClick = sections.CurrentTab;
        var returnButtonVisibleBeforeClick = returnButton?.IsVisibleInTree();
        var returnButtonDisabledBeforeClick = returnButton?.Disabled;
        var returnButtonMouseFilterBeforeClick = returnButton?.MouseFilter.ToString();
        var viewportBefore = GetViewport().GetVisibleRect();
        var heroRectBefore = heroPage?.GetGlobalRect();
        var returnRectBefore = returnButton?.GetGlobalRect();
        var connectingGeneration = main.ObservedSessionGeneration;
        var transitionBeforeClick = main.ObservedSessionTransition;
        var setupVisibleBeforeClick = setup.Visible;
        var transportConnectedBeforeClick = main.TransportConnected;
        var returnCleanupActiveBeforeClick = main.ObservedReturnCleanupActive;
        var returnCleanupCompletedBeforeClick = main.ObservedReturnCleanupCompleted;
        var returnCleanupOutcomePresentBeforeClick = main.ObservedReturnCleanupOutcomePresent;
        var returnCleanupSucceededBeforeClick = main.ObservedReturnCleanupSucceeded;
        var returnCleanupFailedBeforeClick = main.ObservedReturnCleanupFailed;
        long? sessionGenerationAfterClickHelper = null;
        bool? sessionTransitionAfterClickHelper = null;
        bool? setupVisibleAfterClickHelper = null;
        bool? transportConnectedAfterClickHelper = null;
        bool? returnCleanupActiveAfterClickHelper = null;
        bool? returnCleanupCompletedAfterClickHelper = null;
        bool? returnCleanupOutcomePresentAfterClickHelper = null;
        bool? returnCleanupSucceededAfterClickHelper = null;
        bool? returnCleanupFailedAfterClickHelper = null;
        var clickCompleted = false;
        try {
            Require(returnButton is not null,
                "The actual Return-to-session-menu HUD control was not found during a pending handshake.");
            await Click(returnButton!.GetGlobalRect().GetCenter());
            clickCompleted = true;
            sessionGenerationAfterClickHelper = main.ObservedSessionGeneration;
            sessionTransitionAfterClickHelper = main.ObservedSessionTransition;
            setupVisibleAfterClickHelper = setup.Visible;
            transportConnectedAfterClickHelper = main.TransportConnected;
            returnCleanupActiveAfterClickHelper = main.ObservedReturnCleanupActive;
            returnCleanupCompletedAfterClickHelper = main.ObservedReturnCleanupCompleted;
            returnCleanupOutcomePresentAfterClickHelper = main.ObservedReturnCleanupOutcomePresent;
            returnCleanupSucceededAfterClickHelper = main.ObservedReturnCleanupSucceeded;
            returnCleanupFailedAfterClickHelper = main.ObservedReturnCleanupFailed;
            await Until(() => setup.Visible && !main.ObservedSessionTransition && !main.TransportConnected,
                "Return could not cancel and join a pending connection handshake.", 10);
        } catch (InvalidOperationException exception) {
            var returnRectAfter = returnButton?.GetGlobalRect();
            var heroRectAfter = heroPage?.GetGlobalRect();
            var viewportAfter = GetViewport().GetVisibleRect();
            try {
                WriteEvidence(directory, phase.ToLowerInvariant() + "-menu-pending-return-failure.json", new {
                    Passed = false,
                    Phase = phase,
                    FailureType = exception.GetType().Name,
                    ClickCompleted = clickCompleted,
                    PendingHandshakeObserved = true,
                    HudVisibleBeforeClick = hudVisibleBeforeClick,
                    HudVisibleAtFailure = sections.IsVisibleInTree(),
                    ReturnButtonFound = returnButton is not null,
                    ReturnButtonVisibleBeforeClick = returnButtonVisibleBeforeClick,
                    ReturnButtonDisabledBeforeClick = returnButtonDisabledBeforeClick,
                    ReturnButtonVisibleAtFailure = returnButton?.IsVisibleInTree(),
                    ReturnButtonDisabledAtFailure = returnButton?.Disabled,
                    ReturnButtonMouseFilterBeforeClick = returnButtonMouseFilterBeforeClick,
                    ReturnButtonCenterXBeforeClick = returnRectBefore?.GetCenter().X,
                    ReturnButtonCenterYBeforeClick = returnRectBefore?.GetCenter().Y,
                    ReturnButtonRectXBeforeClick = returnRectBefore?.Position.X,
                    ReturnButtonRectYBeforeClick = returnRectBefore?.Position.Y,
                    ReturnButtonRectWidthBeforeClick = returnRectBefore?.Size.X,
                    ReturnButtonRectHeightBeforeClick = returnRectBefore?.Size.Y,
                    ReturnButtonRectXAtFailure = returnRectAfter?.Position.X,
                    ReturnButtonRectYAtFailure = returnRectAfter?.Position.Y,
                    ReturnButtonRectWidthAtFailure = returnRectAfter?.Size.X,
                    ReturnButtonRectHeightAtFailure = returnRectAfter?.Size.Y,
                    ViewportXBeforeClick = viewportBefore.Position.X,
                    ViewportYBeforeClick = viewportBefore.Position.Y,
                    ViewportWidthBeforeClick = viewportBefore.Size.X,
                    ViewportHeightBeforeClick = viewportBefore.Size.Y,
                    ViewportXAtFailure = viewportAfter.Position.X,
                    ViewportYAtFailure = viewportAfter.Position.Y,
                    ViewportWidthAtFailure = viewportAfter.Size.X,
                    ViewportHeightAtFailure = viewportAfter.Size.Y,
                    ReturnButtonCenterInsideViewportBeforeClick = returnRectBefore is { } beforeRect
                        && viewportBefore.HasPoint(beforeRect.GetCenter()),
                    ReturnButtonRectInsideHeroPageBeforeClick = heroRectBefore is { } beforeHeroRect
                        && returnRectBefore is { } visibleButtonRect
                        && beforeHeroRect.Encloses(visibleButtonRect.Grow(-0.5f)),
                    ReturnButtonCenterInsideViewportAtFailure = returnRectAfter is { } afterRect
                        && viewportAfter.HasPoint(afterRect.GetCenter()),
                    HeroPageXBeforeClick = heroRectBefore?.Position.X,
                    HeroPageYBeforeClick = heroRectBefore?.Position.Y,
                    HeroPageWidthBeforeClick = heroRectBefore?.Size.X,
                    HeroPageHeightBeforeClick = heroRectBefore?.Size.Y,
                    HeroPageXAtFailure = heroRectAfter?.Position.X,
                    HeroPageYAtFailure = heroRectAfter?.Position.Y,
                    CurrentHudTabBeforeClick = currentHudTabBeforeClick,
                    CurrentHudTabAtFailure = sections.CurrentTab,
                    SessionGenerationAfterClickHelper = sessionGenerationAfterClickHelper,
                    SessionGenerationBeforeClick = connectingGeneration,
                    SessionGenerationAtFailure = main.ObservedSessionGeneration,
                    SessionTransitionBeforeClick = transitionBeforeClick,
                    SessionTransitionAfterClickHelper = sessionTransitionAfterClickHelper,
                    SessionTransitionAtFailure = main.ObservedSessionTransition,
                    SetupVisibleBeforeClick = setupVisibleBeforeClick,
                    SetupVisibleAfterClickHelper = setupVisibleAfterClickHelper,
                    SetupVisibleAtFailure = setup.Visible,
                    TransportConnectedBeforeClick = transportConnectedBeforeClick,
                    TransportConnectedAfterClickHelper = transportConnectedAfterClickHelper,
                    TransportConnectedAtFailure = main.TransportConnected,
                    PendingCommandCountAtFailure = main.ObservedPendingCommandCount,
                    SnapshotPresentAtFailure = main.ObservedSnapshot is not null,
                    CommandResultPresentAtFailure = main.ObservedCommandResult is not null,
                    ReturnCleanupActiveBeforeClick = returnCleanupActiveBeforeClick,
                    ReturnCleanupActiveAfterClickHelper = returnCleanupActiveAfterClickHelper,
                    ReturnCleanupActiveAtFailure = main.ObservedReturnCleanupActive,
                    ReturnCleanupCompletedBeforeClick = returnCleanupCompletedBeforeClick,
                    ReturnCleanupCompletedAfterClickHelper = returnCleanupCompletedAfterClickHelper,
                    ReturnCleanupCompletedAtFailure = main.ObservedReturnCleanupCompleted,
                    ReturnCleanupOutcomePresentBeforeClick = returnCleanupOutcomePresentBeforeClick,
                    ReturnCleanupOutcomePresentAfterClickHelper = returnCleanupOutcomePresentAfterClickHelper,
                    ReturnCleanupOutcomePresentAtFailure = main.ObservedReturnCleanupOutcomePresent,
                    ReturnCleanupSucceededBeforeClick = returnCleanupSucceededBeforeClick,
                    ReturnCleanupSucceededAfterClickHelper = returnCleanupSucceededAfterClickHelper,
                    ReturnCleanupSucceededAtFailure = main.ObservedReturnCleanupSucceeded,
                    ReturnCleanupFailedBeforeClick = returnCleanupFailedBeforeClick,
                    ReturnCleanupFailedAfterClickHelper = returnCleanupFailedAfterClickHelper,
                    ReturnCleanupFailedAtFailure = main.ObservedReturnCleanupFailed,
                    ReturnCleanupGateEnteredAtFailure = main.ObservedReturnCleanupGateEntered,
                });
            } catch (Exception evidenceException) {
                try {
                    GD.PushWarning($"Could not persist pending-return failure evidence ({evidenceException.GetType().Name}).");
                } catch {
                    // Preserve the original acceptance failure if Godot logging is unavailable.
                }
            }
            throw;
        }
        Require(main.ObservedSessionGeneration > connectingGeneration && main.ObservedSnapshot is null
            && main.ObservedCommandResult is null && main.ObservedPlayerScreenPosition is null,
            "Canceled connection work reinstalled old world/model state.");
        Require((int)portField.Value == withheldPort
            && ((LineEdit)setup.FindChild("ResumeToken", true, false)).Text == retainedToken
            && originalBookmark.AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(bookmarkPath)),
            "Remote return lost its in-memory endpoint/token or overwrote the startup-local bookmark.");
        try {
            Require(await stream.ReadAsync(new byte[1], stageDeadline.Token) == 0,
                "The canceled handshake left its owned TCP connection open or wrote another envelope.");
        } catch (IOException exception) when (exception.InnerException is SocketException socket
            && socket.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown) {
            // A confirmed peer reset is also a terminal transport observation.
        }
        // Resume the real host through the actual form; the canceled endpoint
        // remains a diagnostic fixture, never a replacement authority.
        portField.Value = actualPort;
    }

    private async Task VerifyConfirmedHandshakeTokenAsync(
        CharacterSetupPresentation originalSetup, string directory, string phase, bool blockBookmark)
    {
        // This peer issues a credential but withholds the first snapshot. It
        // exercises only client connection ownership, never gameplay authority.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stageDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var fixtureDirectory = System.IO.Path.Combine(directory,
            phase.ToLowerInvariant() + (blockBookmark ? "-blocked-handshake" : "-fresh-handshake"));
        System.IO.Directory.CreateDirectory(fixtureDirectory);
        var fixtureBookmark = System.IO.Path.Combine(fixtureDirectory, "resume-token.txt");
        const string previousToken = "private-old-handshake-fixture";
        if (blockBookmark)
            LocalSessionBookmark.Write(fixtureBookmark, previousToken);
        using var writeBlock = blockBookmark
            ? new FileStream(fixtureBookmark, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)
            : null;
        var overrides = new Dictionary<string, string?> {
            ["DEVILUTION_SERVER_PORT"] = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(),
            ["DEVILUTION_RESUME_TOKEN_PATH"] = fixtureBookmark,
            ["DEVILUTION_RESUME_TOKEN"] = null,
            ["DEVILUTION_CHARACTER_CLASS_ID"] = null,
            ["DEVILUTION_CHARACTER_NAME"] = null,
        };
        var savedEnvironment = overrides.Keys.ToDictionary(name => name,
            name => System.Environment.GetEnvironmentVariable(name));
        Main? connecting = null;
        originalSetup.Visible = false;
        try {
            try {
                foreach (var (name, value) in overrides)
                    System.Environment.SetEnvironmentVariable(name, value);
                connecting = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
                AddChild(connecting);
            } finally {
                foreach (var (name, value) in savedEnvironment)
                    System.Environment.SetEnvironmentVariable(name, value);
            }
            await Frame();
            await Frame();
            var setup = connecting.GetNode<CharacterSetupPresentation>("CharacterSetup");
            Require(setup.Visible && !connecting.TransportConnected,
                "Confirmed-token fixture did not enter through its actual setup form.");
            var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
            mode.Select(0);
            mode.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = "Handshake-Fixture";
            // Changing Resume to New character resizes the form. Wait for
            // container layout before calculating the actual Connect position.
            await Frame();
            await Frame();
            await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
            Require(!setup.Visible && connecting.ObservedSessionGeneration == 1,
                "The actual new-character Connect control did not begin the fixture handshake.");
            using var peer = await listener.AcceptTcpClientAsync(stageDeadline.Token);
            await using var stream = peer.GetStream();
            var header = new byte[4];
            await stream.ReadExactlyAsync(header, stageDeadline.Token);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            Require(length is > 0 and <= 65536, "Confirmed-token fixture received an invalid frame.");
            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, stageDeadline.Token);
            var envelope = Envelope.Parser.ParseFrom(payload);
            Require(envelope.PayloadCase == Envelope.PayloadOneofCase.ClientHello,
                "The confirmed-token fixture did not receive ClientHello.");
            var hello = envelope.ClientHello
                ?? throw new InvalidOperationException("The confirmed-token fixture received no ClientHello.");
            Require(string.IsNullOrWhiteSpace(hello.ResumeToken)
                && hello.RequestedCharacterName == "Handshake-Fixture",
                "The confirmed-token fixture did not submit a new-character handshake.");
            var confirmedToken = Guid.NewGuid().ToString("N");
            var response = new Envelope { ServerHello = new ServerHello {
                ServerBuildId = "handshake-ownership-fixture", ProtocolSchemaVersion = hello.ProtocolSchemaVersion,
                ContentManifestHash = hello.ContentManifestHash, RulesetIdentityHash = hello.RulesetIdentityHash,
                TickRateHz = 20, GameMode = GameMode.SinglePlayer, SessionToken = confirmedToken,
            } }.ToByteArray();
            BinaryPrimitives.WriteInt32LittleEndian(header, response.Length);
            await stream.WriteAsync(header, stageDeadline.Token);
            await stream.WriteAsync(response, stageDeadline.Token);
            await Until(() => connecting.ObservedIdentity is not null,
                "ServerHello was not confirmed before the withheld initial snapshot.", 5);
            Require(!connecting.TransportConnected && connecting.ObservedSnapshot is null
                && connecting.ObservedPendingCommandCount == 0,
                "The confirmed-token fixture unexpectedly completed a gameplay handshake.");
            await Click(((Button)connecting.FindChild("ReturnToSessionMenu", true, false)).GetGlobalRect().GetCenter());
            await Until(() => setup.Visible && !connecting.ObservedSessionTransition && !connecting.TransportConnected,
                "Confirmed-token return did not join the canceled handshake and show its menu.", 10);
            Require(((LineEdit)setup.FindChild("ResumeToken", true, false)).Text == confirmedToken
                && mode.Selected == 1,
                "A confirmed local token was lost to an absent or stale bookmark after cancellation.");
            var warning = ((Label)setup.FindChild("Validation", true, false)).Text;
            if (blockBookmark) {
                Require(LocalSessionBookmark.Read(fixtureBookmark) == previousToken
                    && warning.Contains("memory", StringComparison.OrdinalIgnoreCase)
                    && !warning.Contains(confirmedToken, StringComparison.Ordinal),
                    "Blocked bookmark return lost the new token, changed durable data or falsely claimed persistence.");
            } else {
                Require(LocalSessionBookmark.Read(fixtureBookmark) == confirmedToken,
                    "Writable local storage did not retain the confirmed canceled-handshake credential durably.");
            }
            Require(await stream.ReadAsync(new byte[1], stageDeadline.Token) == 0,
                "The confirmed canceled handshake did not close its owned TCP connection.");
        } finally {
            connecting?.QueueFree();
            originalSetup.Visible = true;
            await Frame();
        }
    }
}
