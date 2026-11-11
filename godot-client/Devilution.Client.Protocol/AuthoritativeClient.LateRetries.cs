using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

public sealed partial class AuthoritativeClient
{
    public const int MaximumTooLateRetries = 2;
    private Snapshot? retrySnapshot;
    private ulong retrySnapshotRevision, latestQueuedIntentSequence;

    private sealed record RetiredLateAttempt(CommandResult Receipt, AuthoritativeCommandReceiptContext Context);
    private sealed record LateRejection(CommandResult Receipt, long Generation, ulong SnapshotRevision, long Deadline);

    /** Local cancellation/selection is new intent even when it does not queue a gameplay command. */
    public void CancelRetainedIntentRetries()
    {
        lock (pendingLock) {
            latestQueuedIntentSequence = 0;
            // Known refusals are terminal immediately; unknown transmitted commands retain their frozen identity.
            ExpireLateRetriesLocked(ElapsedMilliseconds);
        }
    }

    private static bool IsLedgerBackedTooLate(CommandResult result) => result.ServerReceiptSequence > 0
        && result.Status is CommandStatus.Rejected or CommandStatus.Duplicate && result.RejectReason == CommandRejectReason.TooLate;

    private bool RetainLateRejectionLocked(PendingCommand command, CommandResult result, long generation, long now)
    {
        if (!IsLedgerBackedTooLate(result) || !command.TransmissionStarted || command.RetryContext is null
            || generation != Volatile.Read(ref transportGeneration) || command.LateRetryCount >= MaximumTooLateRetries
            || latestQueuedIntentSequence != command.IntentSequence || nextClientSequence == ulong.MaxValue
            || result.AppliedTick <= command.Command.RequestedTick) return false;
        // A duplicate is the same refused attempt, not another retry or another lead sample.
        if (command.LateRejection is null) {
            command.LateRejection = new(result.Clone(), generation, retrySnapshotRevision,
                SaturatingAdd(now, TimingProbeTimeout.TotalMilliseconds));
            if (IsClockGatedCommandScheduling && (command.Retransmitted || result.Status == CommandStatus.Duplicate))
                schedulingLead.ObserveLateAdmission(now, ServerTickRateHz, command.FirstDispatchLeadTicks,
                    result.AppliedTick - command.Command.RequestedTick);
        }
        pending.Add(command.Command.ClientSequence, command);
        return true;
    }

    private void ExpireLateRetriesLocked(long now)
    {
        while (true) {
            PendingCommand? stale = null;
            foreach (var command in pending.Values)
                if (command.LateRejection is { } rejection && (now >= rejection.Deadline
                    || rejection.Generation != Volatile.Read(ref transportGeneration) || latestQueuedIntentSequence != command.IntentSequence)) {
                    stale = command; break;
                }
            if (stale is null) return;
            PrepareLateRetryLocked(stale, now);
        }
    }

    private bool LateRetryReady(PendingCommand command, long now)
        => command.LateRejection is { } rejected && (now >= rejected.Deadline
            || rejected.Generation != Volatile.Read(ref transportGeneration)
            || latestQueuedIntentSequence != command.IntentSequence
            || retrySnapshotRevision > rejected.SnapshotRevision && retrySnapshot is { } snapshot && snapshot.Tick >= rejected.Receipt.AppliedTick);

    private PendingCommand? PrepareLateRetryLocked(PendingCommand previous, long now)
    {
        if (previous.LateRejection is not { } rejected) return previous;
        if (!LateRetryReady(previous, now)) return null;
        pending.Remove(previous.Command.ClientSequence);
        if (now >= rejected.Deadline || rejected.Generation != Volatile.Read(ref transportGeneration)
            || latestQueuedIntentSequence != previous.IntentSequence || nextClientSequence == ulong.MaxValue
            || retrySnapshot is not { } snapshot || previous.RetryContext is not { } context
            || !AuthoritativeCommandRetryPolicy.StillValid(previous.Command, context, snapshot)) {
            // Re-publish the actual refused receipt as final. Never synthesize an ACK for an unsent command.
            incoming.Enqueue(new(Acknowledgement: new() { Results = { rejected.Receipt.Clone() } }) {
                AcknowledgedCommands = [ReceiptContext(previous)],
            });
            return null;
        }
        var command = previous.Command.Clone();
        command.ClientSequence = checked(++nextClientSequence);
        command.RequestedTick = 0;
        var next = new PendingCommand(command, snapshot.Tick) {
            IntentSequence = previous.IntentSequence, LateRetryCount = previous.LateRetryCount + 1,
            RetryContext = context, QueuedElapsedMilliseconds = now,
            RetiredLateAttempts = previous.RetiredLateAttempts.Append(new RetiredLateAttempt(rejected.Receipt, ReceiptContext(previous, true))).ToArray(),
            QueuedStopwatchTimestamp = CommandTimingRecorder is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp(),
        };
        pending.Add(command.ClientSequence, next);
        incoming.Enqueue(new() { CommandRetry = new(previous.IntentSequence, previous.Command.ClientSequence, command.ClientSequence) });
        CommandLog?.Append(new CommandLogRecord { ClientQueued = new() { Command = command.Clone() } });
        return next;
    }

    private AuthoritativeCommandReceiptContext? RetiredLateContextLocked(CommandResult result)
    {
        if (!IsLedgerBackedTooLate(result)) return null;
        foreach (var command in pending.Values)
            foreach (var retired in command.RetiredLateAttempts)
                if (retired.Receipt.ClientSequence == result.ClientSequence && retired.Receipt.ServerReceiptSequence == result.ServerReceiptSequence)
                    return retired.Context;
        return null;
    }

    private static AuthoritativeCommandReceiptContext ReceiptContext(PendingCommand command, bool retryPending = false)
        => new(command.Command.ClientSequence, command.Command.RequestedTick, command.Command.IntentCase) {
            IntentSequence = command.IntentSequence, RetryPending = retryPending,
        };

    private static bool NeedsRealtimeCalibration(PendingCommand command)
        => command.LateRejection is not null || !command.TransmissionStarted && command.ObservedTick.HasValue;
}
