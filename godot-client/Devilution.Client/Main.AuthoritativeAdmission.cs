using Devilution.Client.Protocol;

namespace Devilution.Client;

public partial class Main
{
    internal const int MaximumAdmissionRecoveryAttempts = 3;
    private TimeProvider reconnectTime = TimeProvider.System;
    private RecoveryAdmissionBoundary? recoveryAdmission;
    private AdmissionRecoveryFailure? failedAdmissionRecovery;
    private int admissionRecoveryAttempts;
    private sealed record RecoveryAdmissionBoundary(AuthoritativeClient Client, long AttemptEpoch,
        TaskCompletionSource<Exception?> Completion);
    private sealed record AdmissionRecoveryFailure(AuthoritativeClient Client, long Generation, string Detail);

    private static bool NeedsTransportReconnect(AuthoritativeClient activeClient, Task? connection, Task? reconnect)
        => !activeClient.IsTransportConnected && (connection?.IsCompleted ?? true) && (reconnect?.IsCompleted ?? true);

    /** Drain receipts even after a snapshot fails: the transport has already acknowledged their commands. */
    private bool ApplyAuthoritativeBatch(AuthoritativeClient activeClient, IReadOnlyList<AuthoritativeClientMessage> messages)
    {
        Exception? failure = null;
        long failedEpoch = 0, recoveredEpoch = 0;
        foreach (var message in messages) {
            if (message.Snapshot is not null && (failure is not null || !activeClient.IsCurrentSnapshotMessage(message)))
                continue;
            try {
                var admissionStarted = FrameCostTimestamp();
                var allocationStarted = FrameCostAllocation();
                var recovering = activeClient.RequiresCompleteSnapshotAdmission;
                try { activeClient.ApplyMessageToModel(model, message); }
                finally {
                    admissionMilliseconds += FrameCostElapsed(admissionStarted);
                    admissionAllocated += FrameCostAllocationSince(allocationStarted);
                }
                if (recovering && !activeClient.RequiresCompleteSnapshotAdmission) {
                    sessionStatusOverride = null;
                    recoveredEpoch = message.ConnectionAttemptEpoch != 0 ? message.ConnectionAttemptEpoch
                        : activeClient.InitialConnectionObservation!.AttemptEpoch;
                }
                if (message.Events is not null) observedEventBatchCount = checked(observedEventBatchCount + 1);
                if (message.Acknowledgement is { } acknowledgement) {
                    foreach (var result in acknowledgement.Results) heldEntityAttack.ObserveResult(result);
                    AcceptanceAcknowledgementObserver?.Invoke(acknowledgement.Clone());
                }
                if (message.Snapshot is not null) {
                    snapshotRevision++;
                    AcceptanceSnapshotObserver?.Invoke(model.CompleteSnapshotObservation(message.Snapshot));
                }
            } catch (Exception exception) {
                failure ??= exception;
                failedEpoch = activeClient.InitialConnectionObservation!.AttemptEpoch;
                sessionStatusOverride = $"Invalid authoritative state: {failure.Message} Reconnecting for a complete snapshot; gameplay is disabled.";
                hud?.SetStatus(sessionStatusOverride);
            }
        }
        // Complete the recovery boundary only after every non-snapshot message in this batch was delivered.
        var boundary = Volatile.Read(ref recoveryAdmission);
        var epoch = failure is not null ? failedEpoch : recoveredEpoch;
        if (boundary is not null && ReferenceEquals(boundary.Client, activeClient) && boundary.AttemptEpoch == epoch)
            boundary.Completion.TrySetResult(failure);
        return failure is null;
    }

    private void StartReconnect(bool force)
    {
        routeTravel.Cancel("Travel stopped: connection lost.");
        interactionTravel.Cancel();
        model.ResetTransientPresentation();
        world?.MarkPresentationEpochPending(model.AcceptedSnapshotRevision);
        if (client is { } activeClient && !sessionTransition && Volatile.Read(ref failedAdmissionRecovery) is null
            && (reconnectTask is null || reconnectTask.IsCompleted))
            reconnectTask = ReconnectLoopAsync(activeClient, sessionGeneration,
                connectionLifetime?.Token ?? lifetime.Token, force);
    }

    private async Task ReconnectLoopAsync(AuthoritativeClient reconnectClient, long generation,
        CancellationToken cancellationToken, bool force)
    {
        var admissionRecovery = reconnectClient.RequiresCompleteSnapshotAdmission;
        var firstAttempt = true;
        var delay = admissionRecovery ? TimeSpan.FromMilliseconds(250) : TimeSpan.Zero;
        Exception? lastFailure = null;
        var attempts = 0;
        while (!cancellationToken.IsCancellationRequested && IsCurrentSession(reconnectClient, generation)) {
            if (admissionRecovery && attempts == MaximumAdmissionRecoveryAttempts) {
                await reconnectClient.DisconnectAsync();
                if (!cancellationToken.IsCancellationRequested && IsCurrentSession(reconnectClient, generation))
                    Interlocked.Exchange(ref failedAdmissionRecovery, new(reconnectClient, generation,
                        $"Authoritative snapshot recovery failed after {attempts} attempts. Returned to the session menu. {lastFailure?.Message}"));
                return;
            }
            if (!force && !firstAttempt && reconnectClient.IsConnected) return;
            try {
                if (delay > TimeSpan.Zero) await Task.Delay(delay, reconnectTime, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentSession(reconnectClient, generation)) return;
                RecoveryAdmissionBoundary? boundary = null;
                if (admissionRecovery) {
                    Volatile.Write(ref admissionRecoveryAttempts, ++attempts);
                    boundary = new(reconnectClient, checked((reconnectClient.InitialConnectionObservation?.AttemptEpoch ?? 0) + 1),
                        new(TaskCreationOptions.RunContinuationsAsynchronously));
                    Volatile.Write(ref recoveryAdmission, boundary);
                }
                try {
                    await reconnectClient.ReconnectAsync(cancellationToken);
                    if (!admissionRecovery) return;
                    lastFailure = await boundary!.Completion.Task.WaitAsync(cancellationToken);
                    if (lastFailure is null) {
                        Volatile.Write(ref admissionRecoveryAttempts, 0);
                        return;
                    }
                } finally {
                    if (boundary is not null) Interlocked.CompareExchange(ref recoveryAdmission, null, boundary);
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            } catch (Exception exception) {
                lastFailure = exception;
                SetSessionError(reconnectClient, generation, exception.Message);
            }
            delay = delay == TimeSpan.Zero ? TimeSpan.FromMilliseconds(250)
                : TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
            force = false;
            firstAttempt = false;
        }
    }

    /** Menu publication stays on the main thread, after any already-drained receipts. */
    private bool CompleteFailedAdmissionRecovery()
    {
        var failure = Interlocked.Exchange(ref failedAdmissionRecovery, null);
        if (failure is null || !IsCurrentSession(failure.Client, failure.Generation)) return false;
        returnCleanupOutcome = null;
        returnCleanupTask = ReturnToSessionMenuAsync(failure.Detail);
        return true;
    }
}
