using Devilution.Protocol.V1;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Two real inputs before one transport poll must retain both authoritative results. */
    private async Task VerifyInputBurstAsync(Main main, string directory)
    {
        Require(main.ObservedClockGatedScheduling && main.ObservedPendingCommandCount == 0,
            "Input-burst acceptance requires a settled clock-gated client.");
        var initial = main.ObservedSnapshot!.Players.Single().Clone();
        var sequenceFloor = main.ObservedLastQueuedSequence;
        var firstSequence = checked(sequenceFloor + 1);
        var secondSequence = checked(firstSequence + 1);
        var results = new List<CommandResult>(2);
        int? queuedBeforeFrame = null;
        var previousObserver = main.AcceptanceAcknowledgementObserver;
        main.AcceptanceAcknowledgementObserver = acknowledgement => {
            foreach (var result in acknowledgement.Results) {
                if (result.ClientSequence == firstSequence || result.ClientSequence == secondSequence) {
                    // Retain one detached result per sequence, never an unbounded history.
                    if (!results.Any(observed => observed.ClientSequence == result.ClientSequence))
                        results.Add(result.Clone());
                }
            }
        };
        try {
            for (var index = 0; index < 2; index++) {
                Input.ParseInputEvent(new InputEventKey {
                    Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = true,
                });
                Input.ParseInputEvent(new InputEventKey {
                    Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = false,
                });
                Input.FlushBufferedEvents();
            }
            // No frame or poll occurs between these actual input handlers.
            queuedBeforeFrame = main.ObservedPendingCommandCount;
            Require(main.ObservedLastQueuedSequence == secondSequence
                && queuedBeforeFrame == 2,
                "The real input handlers did not queue both commands before the next frame.");
            await Until(() => results.Count == 2 && main.ObservedPendingCommandCount == 0
                && main.ObservedSnapshot!.Tick >= results.Max(result => result.AppliedTick)
                && main.ObservedSnapshot.Players.Single().HostileMode != initial.HostileMode,
                "Both queued inputs did not reach acknowledged authoritative state.");
            Require(results[0].ClientSequence == firstSequence
                && results[1].ClientSequence == secondSequence
                && main.ObservedLastQueuedSequence == secondSequence,
                "Input-burst acknowledgements were not exposed in wire order.");
            Require(results.All(result => result.Status == CommandStatus.Accepted
                    && IsAcceptedOrSuccessfulDuplicate(result))
                && results[0].ServerReceiptSequence != 0
                && results[1].ServerReceiptSequence > results[0].ServerReceiptSequence,
                "Input burst did not retain two separately accepted ordered receipts.");
            var settled = main.ObservedSnapshot!.Players.Single();
            Require(settled.EntityId == initial.EntityId && settled.LevelId == initial.LevelId
                && settled.PositionX == initial.PositionX && settled.PositionY == initial.PositionY,
                "Friendly-mode input burst unexpectedly changed identity or world position.");
            // Both inputs read the same snapshot and assign the same mode. This is
            // intentionally not a relative toggle or a client-owned gameplay effect.
            WriteEvidence(directory, "create-input-burst.json", new {
                Passed = true, QueuedBeforeFrame = queuedBeforeFrame,
                InitialHostileMode = initial.HostileMode, SettledHostileMode = settled.HostileMode,
                Results = results.ToArray(),
            });
        } catch (Exception failure) {
            try {
                WriteEvidence(directory, "create-input-burst.json", new {
                    Passed = false, QueuedBeforeFrame = queuedBeforeFrame,
                    InitialHostileMode = initial.HostileMode,
                    main.ObservedLastQueuedSequence, main.ObservedPendingCommandCount,
                    SnapshotTick = main.ObservedSnapshot?.Tick, Results = results.ToArray(),
                    Failure = failure.Message,
                });
            } catch (Exception evidenceFailure) {
                try { failure.Data["InputBurstEvidenceFailure"] = evidenceFailure; } catch { }
            }
            throw;
        } finally {
            main.AcceptanceAcknowledgementObserver = previousObserver;
        }
    }
}
