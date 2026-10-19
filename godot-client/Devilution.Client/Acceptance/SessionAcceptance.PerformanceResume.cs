using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

/** Opt-in headless cold-resume continuation for a completed single-client PerformanceCreate. */
public partial class SessionAcceptance
{
    private async Task RunPerformanceResumeAsync(string directory)
    {
        Require(System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE") == "PerformanceResume",
            "The Performance resume handler requires its explicit acceptance phase.");
        Require(!PerformanceResumeStateContract.IsCommandTimingTraceEnabled(
                System.Environment.GetEnvironmentVariable("GODEVILUTION_COMMAND_TIMING_TRACE")),
            "PerformanceResume requires command timing tracing to remain disabled.");
        Require(ReadPerformanceParticipants() == 1 && ReadPerformanceSlot(1) == 1,
            "PerformanceResume requires one single-player participant in slot one.");
        Require(System.IO.Directory.Exists(directory),
            "PerformanceResume private evidence directory does not exist.");

        var baselinePath = System.Environment.GetEnvironmentVariable(
            "GODEVILUTION_PERFORMANCE_RESUME_BASELINE_PATH");
        Require(!string.IsNullOrWhiteSpace(baselinePath)
            && System.IO.Path.IsPathFullyQualified(baselinePath),
            "PerformanceResume requires an absolute PerformanceCreate baseline path.");
        var baseline = PerformanceResumeStateContract.ReadBaseline(baselinePath!);

        Main? main = null;
        try {
            var activeMain = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            main = activeMain;
            AddChild(activeMain);
            await Frame();
            await Frame();

            var setup = activeMain.GetNode<CharacterSetupPresentation>("CharacterSetup");
            var mode = setup.FindChild("SessionMode", true, false) as OptionButton
                ?? throw new InvalidOperationException("PerformanceResume session mode control is missing.");
            var resumeToken = setup.FindChild("ResumeToken", true, false) as LineEdit
                ?? throw new InvalidOperationException("PerformanceResume credential control is missing.");
            Require(setup.Visible && !activeMain.TransportConnected && mode.Selected == 1
                && resumeToken.Text.Length > 0,
                "PerformanceResume must use the real persisted single-player resume form.");

            var connect = setup.FindChild("Connect", true, false) as Button
                ?? throw new InvalidOperationException("PerformanceResume Connect control is missing.");
            var connectCenter = connect.GetGlobalRect().GetCenter();
            Require(connect.IsVisibleInTree() && GetViewport().GetVisibleRect().HasPoint(connectCenter),
                "PerformanceResume Connect control is outside the active viewport.");
            await Click(connectCenter);

            AuthoritativeInitialConnectionObservation? admittedObservation = null;
            await PerformanceUntil(() => {
                var currentObservation = activeMain.ObservedInitialConnection;
                admittedObservation = currentObservation;
                return activeMain.TransportConnected
                    && currentObservation is { AdmissionCompleted: true, ServerHello: not null }
                    && currentObservation.FirstWireSnapshot is not null;
            }, "PerformanceResume did not capture an admitted first wire snapshot.");

            var observation = admittedObservation
                ?? throw new InvalidDataException("PerformanceResume connection observation is missing.");
            var firstWireSnapshot = observation.FirstWireSnapshot
                ?? throw new InvalidDataException("PerformanceResume first wire snapshot is missing.");
            var hello = observation.ServerHello
                ?? throw new InvalidDataException("PerformanceResume ServerHello observation is missing.");
            var attemptEpoch = observation.AttemptEpoch;
            RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);

            var observedHello = new PerformanceResumeServerHello(
                new PerformanceResumeServerIdentity(
                    hello.ServerBuildId,
                    hello.ProtocolSchemaVersion,
                    hello.ContentManifestHash,
                    hello.RulesetIdentityHash,
                    hello.TickRateHz,
                    hello.GameMode.ToString(),
                    hello.CommandSchedulingMode.ToString()),
                hello.LastClientSequence,
                hello.ResumedSessionMatched);
            PerformanceResumeStateContract.ValidateResumeHandshakeAndFirstWire(
                baseline, observedHello, firstWireSnapshot);

            var expectedIdentity = new PerformanceIdentity(
                baseline.Identity.ServerBuildId,
                baseline.Identity.ProtocolSchemaVersion,
                baseline.Identity.TickRateHz,
                baseline.Identity.GameMode,
                baseline.Identity.ContentManifestHash,
                baseline.Identity.RulesetIdentityHash);
            await PerformanceUntil(() => activeMain.TransportConnected
                    && !setup.Visible
                    && activeMain.ObservedIdentity is not null
                    && activeMain.ObservedSnapshot is not null,
                "PerformanceResume did not publish its first authoritative model snapshot.");

            RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
            RequirePerformanceIdentityStable(activeMain, expectedIdentity, GameMode.SinglePlayer);
            Require(PerformanceResumeStateContract.IsFreshResumeClientQueueIdle(
                activeMain.ObservedLastQueuedSequence,
                activeMain.ObservedPendingCommandCount,
                activeMain.ObservedCommandResult is not null),
                $"PerformanceResume current client queue must be idle before input: savedServerCursor={baseline.LastCommand.ClientSequence}, expectedLocalQueuedSequence=0, observedLastQueuedSequence={activeMain.ObservedLastQueuedSequence}, pendingCommandCount={activeMain.ObservedPendingCommandCount}, commandResultPresent={activeMain.ObservedCommandResult is not null}.");

            var currentSnapshot = activeMain.ObservedSnapshot!.Clone();
            var legalControl = PerformanceResumeStateContract.SelectLegalControl(
                baseline, firstWireSnapshot, currentSnapshot)
                ?? throw new InvalidDataException(
                    "PerformanceResume has no adjacent destination proven safe by first-wire and current geometry.");
            RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
            RequirePerformanceIdentityStable(activeMain, expectedIdentity, GameMode.SinglePlayer);
            Require(PerformanceResumeStateContract.IsFreshResumeClientQueueIdle(
                activeMain.ObservedLastQueuedSequence,
                activeMain.ObservedPendingCommandCount,
                activeMain.ObservedCommandResult is not null),
                $"PerformanceResume current client queue must remain idle before dispatch: savedServerCursor={baseline.LastCommand.ClientSequence}, expectedLocalQueuedSequence=0, observedLastQueuedSequence={activeMain.ObservedLastQueuedSequence}, pendingCommandCount={activeMain.ObservedPendingCommandCount}, commandResultPresent={activeMain.ObservedCommandResult is not null}.");

            var latestSnapshot = activeMain.ObservedSnapshot?.Clone()
                ?? throw new InvalidDataException("PerformanceResume lost its current snapshot before input.");
            var latestControl = PerformanceResumeStateContract.SelectLegalControl(
                baseline, firstWireSnapshot, latestSnapshot);
            Require(latestControl == legalControl,
                "PerformanceResume destination changed before input and must be selected again by a new run.");
            var expectedSequence = checked(baseline.LastCommand.ClientSequence + 1);

            GetViewport().GuiReleaseFocus();
            var key = PerformanceResumeKey(legalControl.Direction);
            Input.ParseInputEvent(new InputEventKey {
                Keycode = key,
                PhysicalKeycode = key,
                Pressed = true,
            });
            try {
                await Frame();
            } finally {
                Input.ParseInputEvent(new InputEventKey {
                    Keycode = key,
                    PhysicalKeycode = key,
                    Pressed = false,
                });
            }
            await Frame();

            await PerformanceUntil(() => {
                RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
                var result = activeMain.ObservedCommandResult;
                if (result is null || result.ClientSequence <= baseline.LastCommand.ClientSequence)
                    return false;
                Require(result.ClientSequence == expectedSequence,
                    "PerformanceResume observed an unexpected client command after the saved cursor.");
                PerformanceResumeStateContract.ValidateNextCommandReceipt(
                    baseline,
                    new PerformanceResumeCommandReceipt(
                        result.ClientSequence,
                        result.ServerReceiptSequence,
                        result.AppliedTick,
                        result.Status.ToString(),
                        result.RejectReason.ToString()));
                return true;
            }, "PerformanceResume did not receive the exact next accepted command receipt.");

            await PerformanceUntil(() => {
                RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
                var result = activeMain.ObservedCommandResult;
                if (result is null || result.ClientSequence < expectedSequence)
                    return false;
                Require(result.ClientSequence == expectedSequence,
                    "PerformanceResume observed an unexpected client command after the saved cursor.");
                var command = new PerformanceResumeCommandReceipt(
                    result.ClientSequence,
                    result.ServerReceiptSequence,
                    result.AppliedTick,
                    result.Status.ToString(),
                    result.RejectReason.ToString());
                PerformanceResumeStateContract.ValidateNextCommandReceipt(baseline, command);
                if (activeMain.ObservedLastQueuedSequence != expectedSequence
                    || activeMain.ObservedPendingCommandCount != 0)
                    return false;

                var snapshot = activeMain.ObservedSnapshot;
                var player = snapshot?.Players.SingleOrDefault();
                if (snapshot is null || player is null)
                    return false;
                return PerformanceResumeStateContract.IsSettledAtControlTarget(
                    baseline,
                    command,
                    snapshot.Tick,
                    PerformanceResumeStateContract.Project(player),
                    legalControl);
            }, "PerformanceResume did not settle the accepted command at its prevalidated destination.");

            RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
            RequirePerformanceIdentityStable(activeMain, expectedIdentity, GameMode.SinglePlayer);
            var finalSnapshot = activeMain.ObservedSnapshot!.Clone();
            var finalPlayer = finalSnapshot.Players.Single();
            var finalReceipt = activeMain.ObservedCommandResult
                ?? throw new InvalidDataException("PerformanceResume final command receipt is missing.");
            var finalCommand = new PerformanceResumeCommandReceipt(
                finalReceipt.ClientSequence,
                finalReceipt.ServerReceiptSequence,
                finalReceipt.AppliedTick,
                finalReceipt.Status.ToString(),
                finalReceipt.RejectReason.ToString());
            var finalProjection = PerformanceResumeStateContract.Project(finalPlayer);
            PerformanceResumeStateContract.ValidateNextCommand(
                baseline, finalCommand, finalSnapshot.Tick, finalProjection, legalControl);
            var currentResult = activeMain.ObservedCommandResult
                ?? throw new InvalidDataException("PerformanceResume final command receipt disappeared.");
            RequirePerformanceResumeAttemptCurrent(activeMain, observation, attemptEpoch);
            Require(activeMain.ObservedLastQueuedSequence == expectedSequence
                && activeMain.ObservedPendingCommandCount == 0
                && currentResult.ClientSequence == expectedSequence,
                "PerformanceResume lost its accepted cursor before final evidence capture.");
            var currentCommand = new PerformanceResumeCommandReceipt(
                currentResult.ClientSequence,
                currentResult.ServerReceiptSequence,
                currentResult.AppliedTick,
                currentResult.Status.ToString(),
                currentResult.RejectReason.ToString());
            Require(currentCommand == finalCommand,
                "PerformanceResume receipt changed before final evidence capture.");
            PerformanceResumeStateContract.ValidateNextCommandReceipt(baseline, currentCommand);

            WriteEvidence(directory, "performance-resume.json", new {
                Passed = true,
                Phase = "PerformanceResume",
                Mode = observedHello.Identity.GameMode,
                ContentHash = observedHello.Identity.ContentManifestHash,
                RulesetHash = observedHello.Identity.RulesetIdentityHash,
                BaselineSchemaVersion = baseline.SchemaVersion,
                BaselineSnapshotTick = baseline.SnapshotTick,
                BaselineClientSequence = baseline.LastCommand.ClientSequence,
                BaselineServerReceiptSequence = baseline.LastCommand.ServerReceiptSequence,
                BaselineAppliedTick = baseline.LastCommand.AppliedTick,
                ResumeAttemptEpoch = attemptEpoch,
                AdmissionCompleted = observation.AdmissionCompleted,
                CurrentAttemptStable = true,
                CurrentTransportConnected = true,
                ResumeHelloCursor = hello.LastClientSequence,
                ResumedSessionMatched = hello.ResumedSessionMatched,
                FirstWireTick = firstWireSnapshot.Tick,
                FirstWireMatchesBaseline = true,
                FirstModelMatchesBaseline = true,
                InputDirection = legalControl.Direction.ToString(),
                TargetX = legalControl.TargetX,
                TargetY = legalControl.TargetY,
                ExpectedFacing = legalControl.ExpectedFacing.ToString(),
                ResumeClientSequence = finalCommand.ClientSequence,
                ResumeServerReceiptSequence = finalCommand.ServerReceiptSequence,
                ResumeAppliedTick = finalCommand.AppliedTick,
                ResumeStatus = finalCommand.Status,
                ResumeRejectReason = finalCommand.RejectReason,
                FinalSnapshotTick = finalSnapshot.Tick,
                FinalX = finalPlayer.PositionX,
                FinalY = finalPlayer.PositionY,
                FinalFacing = finalPlayer.FacingDirection.ToString(),
                FinalSettled = finalPlayer.CombatMode == PlayerCombatMode.Stand
                    && finalPlayer.CurrentAction is null,
            });
        } finally {
            if (main is not null)
                main.QueueFree();
        }
    }

    private static void RequirePerformanceResumeAttemptCurrent(
        Main main,
        AuthoritativeInitialConnectionObservation captured,
        long attemptEpoch)
    {
        var current = main.ObservedInitialConnection;
        Require(main.TransportConnected
            && captured.AdmissionCompleted
            && captured.AttemptEpoch == attemptEpoch
            && current is not null
            && ReferenceEquals(current, captured)
            && current.AttemptEpoch == attemptEpoch
            && current.AdmissionCompleted,
            "PerformanceResume lost its admitted current transport attempt.");
    }

    private static Key PerformanceResumeKey(PerformanceResumeDirection direction)
        => direction switch {
            PerformanceResumeDirection.Up => Key.Up,
            PerformanceResumeDirection.Right => Key.Right,
            PerformanceResumeDirection.Down => Key.Down,
            PerformanceResumeDirection.Left => Key.Left,
            _ => throw new InvalidDataException("PerformanceResume selected an unknown control direction."),
        };
}
