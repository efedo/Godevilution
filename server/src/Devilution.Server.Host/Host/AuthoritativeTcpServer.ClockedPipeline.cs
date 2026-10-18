using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Protocol;

namespace Devilution.Server.Host;

/** Atomically reserves retained response weight across all clocked connections. */
internal sealed class ClockGatedResponseBudget
{
    private readonly object synchronization = new();
    private readonly int capacity;
    private int available;

    public ClockGatedResponseBudget(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        available = capacity;
    }

    public bool TryReserve(int weight)
    {
        if (weight <= 0 || weight > capacity)
            return false;
        lock (synchronization) {
            if (weight > available)
                return false;
            available -= weight;
            return true;
        }
    }

    public void Release(int weight)
    {
        if (weight <= 0)
            return;
        lock (synchronization) {
            var released = checked(available + weight);
            if (released > capacity)
                throw new InvalidOperationException("Clock-gated response weight was released twice.");
            available = released;
        }
    }
}

/** Marks a socket write failure without classifying authority failures as transport loss. */
internal sealed class ClockGatedTransportException(Exception innerException)
    : IOException("The clock-gated peer transport closed while writing a response.", innerException);

public sealed partial class AuthoritativeTcpServer
{
    private async Task HandleClockGatedConnectionAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong currentTick,
        int connectionOrdinal,
        CancellationToken hostCancellationToken)
    {
        var scheduler = commandScheduler
            ?? throw new InvalidOperationException("Clock-gated scheduling is not initialized.");
        var responseBudget = clockGatedResponseBudget
            ?? throw new InvalidOperationException("Clock-gated response capacity is not initialized.");
        using var pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            hostCancellationToken);
        var lane = new ClockGatedCommandLane();
        var workChannel = Channel.CreateBounded<ClockGatedResponseWork>(
            new BoundedChannelOptions(Math.Max(1, MaximumPendingCommands)) {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        var reader = ReadClockGatedConnectionAsync(
            stream,
            sessionId,
            entityId,
            connectionOrdinal,
            lane,
            scheduler,
            responseBudget,
            workChannel.Writer,
            pipelineCancellation,
            hostCancellationToken);
        var writer = WriteClockGatedConnectionAsync(
            stream,
            sessionId,
            entityId,
            currentTick,
            connectionOrdinal,
            lane,
            scheduler,
            responseBudget,
            workChannel.Reader,
            pipelineCancellation,
            hostCancellationToken);
        try {
            await Task.WhenAll(reader, writer);
        } catch (Exception outcome) when (lane.IsTransportClosed
            && lane.Failure is { } primaryFailure) {
            // Reader cleanup can win WhenAll's exception selection after an
            // authoritative failure. Retain authority as primary, not that IO.
            if (!ReferenceEquals(outcome, primaryFailure)) {
                try { primaryFailure.Data["ClockGatedTransportFailure"] = outcome; } catch { }
            }
            ReportClockGatedOrphanFailureOnce(lane, primaryFailure);
        } catch (ClockGatedTransportException outcome)
            when (outcome.InnerException is OperationCanceledException
                && reader.IsFaulted
                && reader.Exception?.InnerException is { } readFailure
                && readFailure is IOException or SocketException or ObjectDisposedException) {
            // The failed reader cancels a stalled writer before rethrowing.
            // After both tasks join, keep that original read error as primary.
            try { readFailure.Data["ClockGatedWriterCancellation"] = outcome; } catch { }
            ExceptionDispatchInfo.Capture(readFailure).Throw();
        } finally {
            pipelineCancellation.Cancel();
            workChannel.Writer.TryComplete();
            while (workChannel.Reader.TryRead(out var abandoned)) {
                abandoned.Release(responseBudget);
            }
        }
    }

    private async Task ReadClockGatedConnectionAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        int connectionOrdinal,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        ClockGatedResponseBudget responseBudget,
        ChannelWriter<ClockGatedResponseWork> workWriter,
        CancellationTokenSource pipelineCancellation,
        CancellationToken hostCancellationToken)
    {
        var terminal = false;
        var transportFailure = false;
        var pipelineFailure = false;
        Exception? readFailure = null;
        try {
            while (!pipelineCancellation.IsCancellationRequested) {
                Envelope? envelope;
                try {
                    envelope = await EnvelopeCodec.ReadAsync(
                        stream,
                        pipelineCancellation.Token);
                } catch (InvalidDataException exception) {
                    await EnqueueClockGatedErrorAsync(
                        workWriter,
                        responseBudget,
                        ProtocolErrorCode.InvalidMessage,
                        exception.Message,
                        terminal: true,
                        reserveResponseWeight: false,
                        pipelineCancellation.Token);
                    terminal = true;
                    break;
                } catch (EndOfStreamException) {
                    transportFailure = true;
                    break;
                } catch (Exception exception)
                    when (exception is SocketException or IOException or ObjectDisposedException) {
                    transportFailure = true;
                    if (!hostCancellationToken.IsCancellationRequested)
                        readFailure = exception;
                    break;
                }

                if (envelope is null)
                    break;

                var envelopeReadCompletedTimestamp = CommandTimingRecorder is null
                    ? (long?)null
                    : Stopwatch.GetTimestamp();
                switch (envelope.PayloadCase) {
                case Envelope.PayloadOneofCase.CommandBatch:
                    try {
                        AuthoritativeCommandBatchValidator.Validate(envelope.CommandBatch);
                        var work = await AdmitClockGatedBatch(
                            sessionId,
                            entityId,
                            connectionOrdinal,
                            envelopeReadCompletedTimestamp,
                            envelope.CommandBatch,
                            lane,
                            scheduler,
                            responseBudget,
                            pipelineCancellation.Token);
                        await EnqueueClockGatedWorkAsync(
                            workWriter,
                            responseBudget,
                            work,
                            pipelineCancellation.Token);
                    } catch (ClockGatedCommandCapacityException exception) {
                        await EnqueueClockGatedErrorAsync(
                            workWriter,
                            responseBudget,
                            ProtocolErrorCode.InvalidMessage,
                            exception.Message,
                            terminal: true,
                            reserveResponseWeight: false,
                            pipelineCancellation.Token);
                        terminal = true;
                    } catch (InvalidDataException exception) {
                        await EnqueueClockGatedErrorAsync(
                            workWriter,
                            responseBudget,
                            ProtocolErrorCode.InvalidMessage,
                            exception.Message,
                            terminal: true,
                            reserveResponseWeight: false,
                            pipelineCancellation.Token);
                        terminal = true;
                    } catch (Exception exception)
                        when (lane.Failure is { } laneFailure
                            && ReferenceEquals(exception, laneFailure)) {
                        await EnqueueClockGatedErrorAsync(
                            workWriter,
                            responseBudget,
                            ProtocolErrorCode.InvalidMessage,
                            exception.Message,
                            terminal: true,
                            reserveResponseWeight: false,
                            pipelineCancellation.Token);
                        terminal = true;
                    }
                    break;
                case Envelope.PayloadOneofCase.ClockProbeRequest:
                    try {
                        var requestId = envelope.ClockProbeRequest.RequestId;
                        if (requestId == 0) {
                            await EnqueueClockGatedErrorAsync(
                                workWriter,
                                responseBudget,
                                ProtocolErrorCode.InvalidMessage,
                                "A clock probe request requires a nonzero request ID.",
                                terminal: true,
                                reserveResponseWeight: false,
                                pipelineCancellation.Token);
                            terminal = true;
                            break;
                        }
                        var clockProbeWork = ClockGatedResponseWork.ClockProbe(
                            requestId,
                            envelopeReadCompletedTimestamp);
                        if (!responseBudget.TryReserve(1)) {
                            await EnqueueClockGatedErrorAsync(
                                workWriter,
                                responseBudget,
                                ProtocolErrorCode.InvalidMessage,
                                "The clock-gated command response capacity is exhausted.",
                                terminal: true,
                                reserveResponseWeight: false,
                                pipelineCancellation.Token);
                            terminal = true;
                            break;
                        }
                        clockProbeWork.SetResponseWeight(1);
                        await EnqueueClockGatedWorkAsync(
                            workWriter,
                            responseBudget,
                            clockProbeWork,
                            pipelineCancellation.Token);
                    } catch (OperationCanceledException)
                        when (pipelineCancellation.IsCancellationRequested) {
                        return;
                    }
                    break;
                case Envelope.PayloadOneofCase.SnapshotRequest:
                    try {
                        var work = ClockGatedResponseWork.Snapshot(
                            envelope.SnapshotRequest.TimingRequestId,
                            envelopeReadCompletedTimestamp);
                        if (!responseBudget.TryReserve(1)) {
                            await EnqueueClockGatedErrorAsync(
                                workWriter,
                                responseBudget,
                                ProtocolErrorCode.InvalidMessage,
                                "The clock-gated command response capacity is exhausted.",
                                terminal: true,
                                reserveResponseWeight: false,
                                pipelineCancellation.Token);
                            terminal = true;
                            break;
                        }
                        work.SetResponseWeight(1);
                        await EnqueueClockGatedWorkAsync(
                            workWriter,
                            responseBudget,
                            work,
                            pipelineCancellation.Token);
                    } catch (OperationCanceledException)
                        when (pipelineCancellation.IsCancellationRequested) {
                        return;
                    }
                    break;
                default:
                    await EnqueueClockGatedErrorAsync(
                        workWriter,
                        responseBudget,
                        ProtocolErrorCode.InvalidMessage,
                        "The session only accepts command batches after the handshake.",
                        terminal: true,
                        reserveResponseWeight: false,
                        pipelineCancellation.Token);
                    terminal = true;
                    break;
                }

                if (terminal)
                    break;
            }
        } catch (OperationCanceledException)
            when (pipelineCancellation.IsCancellationRequested) {
        } catch {
            pipelineFailure = true;
            throw;
        } finally {
            workWriter.TryComplete();
            // A clean FIN completes ingress but leaves the writer draining
            // accepted work, preserving the legacy half-close behavior. A
            // failed read cancels only transport work; accepted scheduler
            // intents remain live and are observed independently.
            if (transportFailure)
                MarkClockGatedTransportClosed(lane);
            if (transportFailure || pipelineFailure)
                pipelineCancellation.Cancel();
        }
        if (readFailure is not null)
            ExceptionDispatchInfo.Capture(readFailure).Throw();
    }

    private async Task<ClockGatedResponseWork> AdmitClockGatedBatch(
        string sessionId,
        uint entityId,
        int connectionOrdinal,
        long? envelopeReadCompletedTimestamp,
        CommandBatch batch,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        ClockGatedResponseBudget responseBudget,
        CancellationToken pipelineCancellationToken)
    {
        var responseWeight = Math.Max(1, batch.Commands.Count);
        if (responseWeight > MaximumPendingCommands)
            throw new ClockGatedCommandCapacityException(
                "The clock-gated command response capacity is exhausted.");
        if (!responseBudget.TryReserve(responseWeight))
            throw new ClockGatedCommandCapacityException(
                "The clock-gated command response capacity is exhausted.");
        var retainWeight = true;
        try {
            ulong sampledClockCurrentTick = 0;
            ulong sampledCommandServerCurrentTick = 0;
            ulong actualAdmissionTick = 0;
            long lockWaitStopwatchTicks = 0;
            var lockWaitStart = CommandTimingRecorder is null
                ? 0L
                : Stopwatch.GetTimestamp();
            var measurement = PerformanceRecorder?.Begin() ?? 0;
            var lockEntered = 0L;
            IDisposable? authorityTurn = null;
            ClockedCommandBoundaryCapture? commitBoundaryCapture = null;
            Task<CommandAck> pending;
            try {
                if (UseClockGatedCommandScheduling && CohortStore is not null)
                    authorityTurn = await cohortAuthorityTurnGate.EnterAsync(pipelineCancellationToken);
                pending = commandServer.WithSynchronizedSession(sessionId, () => {
                    lockEntered = CommandTimingRecorder is null
                        ? 0L
                        : Stopwatch.GetTimestamp();
                    RequireCohortHealthy();
                    sampledClockCurrentTick = clock.CurrentTick;
                    sampledCommandServerCurrentTick = commandServer.CurrentTick;
                    actualAdmissionTick = Math.Max(
                        sampledClockCurrentTick,
                        sampledCommandServerCurrentTick);
                    Func<string, CommandAck, bool>? commitBoundaryPublication = null;
                    if (batch.Commands.Count == 1
                        && SupportsCommitBoundaryCommandSnapshots
                        && CohortStore is not null
                        && snapshotProvider is not null
                        && (commandScheduler is not null)) {
                        var command = batch.Commands[0];
                        var responseCapture = new ClockedCommandBoundaryCapture(
                            sessionId,
                            command.ClientSequence,
                            handshake.Identity.RulesetIdentityHash,
                            GetRulesetGenerationToken(),
                            pipelineCancellationToken);
                        commitBoundaryCapture = responseCapture;
                        commitBoundaryPublication = (ownerSessionId, acknowledgement)
                            => TryPublishCommitBoundaryCommandCapture(
                                responseCapture,
                                ownerSessionId,
                                acknowledgement,
                                entityId);
                    }
                    return scheduler.SubmitBatchAsync(
                        sessionId,
                        batch,
                        actualAdmissionTick,
                        pipelineCancellationToken,
                        lane,
                        commitBoundaryPublication);
                });
            } finally {
                authorityTurn?.Dispose();
            }
            PerformanceRecorder?.Complete(
                HostPerformanceMetric.CommandQueueAdmissionMilliseconds,
                measurement);
            if (lockEntered != 0)
                lockWaitStopwatchTicks = Math.Max(0, lockEntered - lockWaitStart);
            var admissionTraceOrdinals = RecordCommandAdmissionTiming(
                connectionOrdinal,
                entityId,
                envelopeReadCompletedTimestamp,
                lockWaitStopwatchTicks,
                sampledClockCurrentTick,
                sampledCommandServerCurrentTick,
                actualAdmissionTick,
                batch);
            EmitCommandTimingOutsideAuthorityLock();
            ObserveClockGatedTaskFailure(pending, lane, scheduler);
            var work = ClockGatedResponseWork.Command(
                batch,
                pending,
                responseWeight,
                envelopeReadCompletedTimestamp,
                admissionTraceOrdinals,
                sampledClockCurrentTick,
                sampledCommandServerCurrentTick,
                actualAdmissionTick,
                lockWaitStopwatchTicks,
                commitBoundaryCapture);
            retainWeight = false;
            return work;
        } finally {
            if (retainWeight)
                responseBudget.Release(responseWeight);
        }
    }

    private bool TryPublishCommitBoundaryCommandCapture(
        ClockedCommandBoundaryCapture responseCapture,
        string sessionId,
        CommandAck acknowledgement,
        uint entityId)
    {
        if (!responseCapture.IsWriterReady
            || responseCapture.CancellationToken.IsCancellationRequested
            || !StringComparer.Ordinal.Equals(sessionId, responseCapture.SessionId)
            || acknowledgement.Results.Count != 1
            || acknowledgement.Results[0].ClientSequence != responseCapture.ClientSequence
            || acknowledgement.Results[0].Status != CommandStatus.Accepted
            || !SupportsCommitBoundaryCommandSnapshots
            || !StringComparer.Ordinal.Equals(
                handshake.Identity.RulesetIdentityHash,
                responseCapture.RulesetIdentityHash)
            || !ReferenceEquals(GetRulesetGenerationToken(), responseCapture.GenerationToken))
            return false;

        return commandServer.WithSynchronizedSession(sessionId, () => {
            RequireCohortHealthy();
            var appliedTick = acknowledgement.Results[0].AppliedTick;
            var tick = commandServer.CurrentTick;
            if (tick != appliedTick
                || acknowledgement.Results.Any(result => result.AppliedTick > tick)
                || !StringComparer.Ordinal.Equals(
                    handshake.Identity.RulesetIdentityHash,
                    responseCapture.RulesetIdentityHash)
                || !ReferenceEquals(GetRulesetGenerationToken(), responseCapture.GenerationToken))
                return false;

            if (responseCapture.CancellationToken.IsCancellationRequested)
                return false;

            var hasSnapshot = snapshotProvider is not null;
            var materialization = hasSnapshot ? PerformanceRecorder?.Begin() ?? 0 : 0;
            var materializeStartTimestamp = hasSnapshot && CommandTimingRecorder is not null
                ? Stopwatch.GetTimestamp()
                : (long?)null;
            var snapshotTraceOrdinal = hasSnapshot && CommandTimingRecorder is not null
                ? Interlocked.Increment(ref nextTimingOrdinal)
                : 0L;
            try {
                var events = eventProvider?.DrainEvents(sessionId, entityId, tick)?.Clone();
                if (events is not null)
                    AuthoritativeEventBatchValidator.Validate(events, tick);

                Snapshot? snapshot = null;
                if (snapshotProvider is not null) {
                    snapshot = snapshotProvider.CreateSnapshot(sessionId, entityId, tick).Clone();
                    if (snapshot.Tick != tick)
                        throw new InvalidDataException(
                            "The clock-gated command-boundary snapshot tick does not match the captured event boundary.");
                    var identity = handshake.Identity;
                    snapshot.ContentManifestHash = identity.ContentManifestHash;
                    snapshot.RulesetIdentityHash = identity.RulesetIdentityHash;
                    AuthoritativeSnapshotValidator.Validate(snapshot);
                }

                PublishCohortCore();
                var materializeEndTimestamp = hasSnapshot && CommandTimingRecorder is not null
                    ? Stopwatch.GetTimestamp()
                    : (long?)null;
                var capture = new ClockedCohortSnapshotPoll(
                    tick,
                    events,
                    snapshot,
                    materializeStartTimestamp,
                    materializeEndTimestamp,
                    snapshotTraceOrdinal,
                    materialization);
                if (!responseCapture.TryPublish(capture))
                    throw new InvalidOperationException(
                        "The clock-gated command response capture was already committed.");
                return true;
            } catch (Exception failure) {
                FenceCohort(failure);
                throw;
            }
        });
    }

    private object? GetRulesetGenerationToken()
        => gameAdapter switch {
            LeasedAuthoritativeGameHostAdapter leasedAdapter => leasedAdapter.ActiveActivationToken,
            { } adapter => adapter,
            _ => null,
        };

    private void RecordCommitBoundarySnapshotStart(
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        ClockedCohortSnapshotPoll capture)
    {
        if (CommandTimingRecorder is null || capture.SnapshotTraceOrdinal == 0)
            return;

        CommandTimingRecorder.Record(new HostCommandTimingRecord {
            Kind = "snapshot-start",
            ConnectionOrdinal = connectionOrdinal,
            TraceOrdinal = capture.SnapshotTraceOrdinal,
            EntityId = entityId,
            EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
            SnapshotMaterializeStartTimestamp = capture.MaterializeStartTimestamp,
            TimingReplyEmitted = null,
        });
        EmitCommandTimingOutsideAuthorityLock();
    }

    private async Task WriteClockGatedConnectionAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong currentTick,
        int connectionOrdinal,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        ClockGatedResponseBudget responseBudget,
        ChannelReader<ClockGatedResponseWork> workReader,
        CancellationTokenSource pipelineCancellation,
        CancellationToken hostCancellationToken)
    {
        try {
            while (await workReader.WaitToReadAsync(pipelineCancellation.Token)) {
                while (!pipelineCancellation.IsCancellationRequested
                    && workReader.TryRead(out var work)) {
                    try {
                        switch (work.Kind) {
                        case ClockGatedResponseKind.Command:
                            currentTick = await WriteClockGatedCommandAsync(
                                stream,
                                sessionId,
                                entityId,
                                connectionOrdinal,
                                work,
                                lane,
                                scheduler,
                                pipelineCancellation.Token,
                                hostCancellationToken,
                                currentTick);
                            break;
                        case ClockGatedResponseKind.ClockProbe:
                            currentTick = await WriteClockGatedClockProbeAsync(
                                stream,
                                entityId,
                                connectionOrdinal,
                                work.TimingRequestId,
                                work.EnvelopeReadCompletedTimestamp,
                                lane,
                                scheduler,
                                pipelineCancellation.Token,
                                currentTick);
                            break;
                        case ClockGatedResponseKind.Snapshot:
                            currentTick = await WriteClockGatedSnapshotAsync(
                                stream,
                                sessionId,
                                entityId,
                                connectionOrdinal,
                                work.TimingRequestId,
                                work.EnvelopeReadCompletedTimestamp,
                                lane,
                                scheduler,
                                pipelineCancellation.Token,
                                hostCancellationToken,
                                currentTick);
                            break;
                        case ClockGatedResponseKind.Error:
                            await WriteClockedTransportEnvelopeAsync(
                                stream,
                                new Envelope { Error = work.Error },
                                pipelineCancellation.Token);
                            break;
                        }
                    } finally {
                        work.Release(responseBudget);
                    }
                    if (work.Terminal)
                        return;
                }
            }
        } catch (OperationCanceledException)
            when (pipelineCancellation.IsCancellationRequested) {
        } catch (ClockGatedTransportException) when (hostCancellationToken.IsCancellationRequested) {
            MarkClockGatedTransportClosed(lane);
        } catch (ClockGatedTransportException) {
            MarkClockGatedTransportClosed(lane);
            pipelineCancellation.Cancel();
            throw;
        } catch (Exception) {
            pipelineCancellation.Cancel();
            throw;
        } finally {
            pipelineCancellation.Cancel();
        }
    }

    private async Task<ulong> WriteClockGatedCommandAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        int connectionOrdinal,
        ClockGatedResponseWork work,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        CancellationToken pipelineCancellationToken,
        CancellationToken hostCancellationToken,
        ulong currentTick)
    {
        CommandAck acknowledgement;
        work.CommitBoundaryCapture?.MarkWriterReady();
        if (work.CommitBoundaryCapture is { } readyCapture)
            CommitBoundaryResponseReadyObserverForTests?.Invoke(sessionId, readyCapture.ClientSequence);
        try {
            acknowledgement = await work.Acknowledgement!
                .WaitAsync(pipelineCancellationToken);
            AuthoritativeCommandAckValidator.Validate(acknowledgement);
        } catch (OperationCanceledException)
            when (pipelineCancellationToken.IsCancellationRequested) {
            return currentTick;
        } catch (Exception exception) {
            PoisonClockGatedLane(scheduler, lane, exception);
            throw;
        }

        try {
            ClockedCohortSnapshotPoll? cohortCapture = null;
            if (CohortStore is null) {
                currentTick = commandServer.WithSynchronizedState(() => {
                    RequireClockedTransportActive(true, pipelineCancellationToken);
                    return AdvanceAndPublishCohortCore();
                });
            } else {
                if (work.CommitBoundaryCapture?.TryGetCapture(out var commitCapture) == true) {
                    cohortCapture = commitCapture;
                    RecordCommitBoundarySnapshotStart(
                        connectionOrdinal,
                        entityId,
                        work.EnvelopeReadCompletedTimestamp,
                        commitCapture);
                    PerformanceRecorder?.Complete(
                        HostPerformanceMetric.SnapshotMaterializeMilliseconds,
                        commitCapture.Materialization);
                } else {
                    // Ineligible, late or canceled work keeps the existing latest-tick
                    // response capture after the scheduler has durably saved its receipt.
                    cohortCapture = await CaptureClockedCohortSnapshotPollAsync(
                        sessionId,
                        entityId,
                        connectionOrdinal,
                        null,
                        work.EnvelopeReadCompletedTimestamp,
                        pipelineCancellationToken,
                        currentTick);
                }
                currentTick = cohortCapture.Tick;
            }
            var commandTraceOrdinals = RecordCommandTiming(
                connectionOrdinal,
                entityId,
                work.EnvelopeReadCompletedTimestamp,
                work.LockWaitStopwatchTicks,
                work.SampledClockCurrentTick,
                work.SampledCommandServerCurrentTick,
                work.ActualAdmissionTick,
                work.Batch!,
                acknowledgement,
                work.AdmissionTraceOrdinals);
            EmitCommandTimingOutsideAuthorityLock();
            var ackWriteStartTimestamp = CommandTimingRecorder is null
                ? (long?)null
                : Stopwatch.GetTimestamp();
            var ackWriteCompleted = false;
            try {
                await WriteClockedTransportEnvelopeAsync(
                    stream,
                    new Envelope { CommandAck = acknowledgement },
                    pipelineCancellationToken);
                ackWriteCompleted = true;
            } finally {
                var ackWriteEndTimestamp = CommandTimingRecorder is null
                    ? (long?)null
                    : Stopwatch.GetTimestamp();
                RecordAckWriteTiming(
                    connectionOrdinal,
                    entityId,
                    work.EnvelopeReadCompletedTimestamp,
                    acknowledgement,
                    commandTraceOrdinals,
                    ackWriteStartTimestamp,
                    ackWriteEndTimestamp,
                    ackWriteCompleted);
                EmitCommandTimingOutsideAuthorityLock();
            }
            if (cohortCapture is null) {
                await SendEventsIfAvailable(
                    stream,
                    sessionId,
                    entityId,
                    currentTick,
                    pipelineCancellationToken,
                    classifyClockedTransportFailure: true,
                    hostCancellationToken: hostCancellationToken);
                await SendSnapshotIfAvailableAsync(
                    stream,
                    sessionId,
                    entityId,
                    currentTick,
                    connectionOrdinal,
                    work.EnvelopeReadCompletedTimestamp,
                    null,
                    pipelineCancellationToken,
                    classifyClockedTransportFailure: true,
                    hostCancellationToken: hostCancellationToken);
            } else {
                if (cohortCapture.Events is { Events.Count: > 0 } events) {
                    await WriteEventBatchAsync(
                        stream,
                        events,
                        currentTick,
                        pipelineCancellationToken,
                        classifyClockedTransportFailure: true,
                        hostCancellationToken: hostCancellationToken);
                }
                if (cohortCapture.Snapshot is not null) {
                    await WriteSnapshotEnvelopeAsync(
                        stream,
                        cohortCapture.Snapshot,
                        connectionOrdinal,
                        entityId,
                        work.EnvelopeReadCompletedTimestamp,
                        null,
                        cohortCapture.MaterializeStartTimestamp,
                        cohortCapture.MaterializeEndTimestamp,
                        cohortCapture.SnapshotTraceOrdinal,
                        pipelineCancellationToken,
                        classifyClockedTransportFailure: true);
                }
            }
            return currentTick;
        } catch (ClockGatedTransportException) {
            throw;
        } catch (Exception exception) {
            PoisonClockGatedLane(scheduler, lane, exception);
            throw;
        }
    }

    private async Task<ulong> WriteClockGatedClockProbeAsync(
        Stream stream,
        uint entityId,
        int connectionOrdinal,
        ulong requestId,
        long? envelopeReadCompletedTimestamp,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        CancellationToken pipelineCancellationToken,
        ulong currentTick)
    {
        try {
            // Serialize the reply with authority publication: wait for any in-flight
            // cohort write, then suppress the response if that publication fenced the host.
            ulong schedulingClockTick;
            IDisposable? authorityTurn = await EnterCohortAuthorityTurnAsync(
                pipelineCancellationToken,
                classifyClockedTransportFailure: true);
            try {
                schedulingClockTick = commandServer.WithSynchronizedState(() => {
                    RequireClockedTransportActive(true, pipelineCancellationToken);
                    RequireCohortHealthy();
                    return Math.Max(clock.CurrentTick, commandServer.CurrentTick);
                });
            } finally {
                authorityTurn?.Dispose();
            }
            await WriteClockedTransportEnvelopeAsync(
                stream,
                new Envelope {
                    TimingReply = new TimingReply {
                        RequestId = requestId,
                        ServerSchedulingClockTick = schedulingClockTick,
                    },
                },
                pipelineCancellationToken);
            RecordTimingReply(connectionOrdinal, entityId, envelopeReadCompletedTimestamp, requestId);
            EmitCommandTimingOutsideAuthorityLock();
            return currentTick;
        } catch (ClockGatedTransportException) {
            throw;
        } catch (Exception exception) {
            PoisonClockGatedLane(scheduler, lane, exception);
            throw;
        }
    }

    private async Task<ulong> WriteClockGatedSnapshotAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        int connectionOrdinal,
        ulong timingRequestId,
        long? envelopeReadCompletedTimestamp,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler,
        CancellationToken pipelineCancellationToken,
        CancellationToken hostCancellationToken,
        ulong currentTick)
    {
        try {
            if (CohortStore is null) {
                currentTick = commandServer.WithSynchronizedState(() => {
                    RequireClockedTransportActive(true, pipelineCancellationToken);
                    return AdvanceAndPublishCohortCore(currentTick);
                });
                await SendEventsIfAvailable(
                    stream,
                    sessionId,
                    entityId,
                    currentTick,
                    pipelineCancellationToken,
                    classifyClockedTransportFailure: true,
                    hostCancellationToken: hostCancellationToken);
                await SendSnapshotIfAvailableAsync(
                    stream,
                    sessionId,
                    entityId,
                    currentTick,
                    connectionOrdinal,
                    envelopeReadCompletedTimestamp,
                    timingRequestId == 0 ? null : timingRequestId,
                    pipelineCancellationToken,
                    classifyClockedTransportFailure: true,
                    hostCancellationToken: hostCancellationToken);
            } else {
                var capture = await CaptureClockedCohortSnapshotPollAsync(
                    sessionId,
                    entityId,
                    connectionOrdinal,
                    timingRequestId == 0 ? null : timingRequestId,
                    envelopeReadCompletedTimestamp,
                    pipelineCancellationToken,
                    currentTick);
                currentTick = capture.Tick;
                if (capture.Events is { Events.Count: > 0 } events) {
                    await WriteEventBatchAsync(
                        stream,
                        events,
                        currentTick,
                        pipelineCancellationToken,
                        classifyClockedTransportFailure: true,
                        hostCancellationToken: hostCancellationToken);
                }
                if (capture.Snapshot is not null) {
                    await WriteSnapshotEnvelopeAsync(
                        stream,
                        capture.Snapshot,
                        connectionOrdinal,
                        entityId,
                        envelopeReadCompletedTimestamp,
                        timingRequestId == 0 ? null : timingRequestId,
                        capture.MaterializeStartTimestamp,
                        capture.MaterializeEndTimestamp,
                        capture.SnapshotTraceOrdinal,
                        pipelineCancellationToken,
                        classifyClockedTransportFailure: true);
                }
            }
            if (timingRequestId != 0) {
                await WriteClockedTransportEnvelopeAsync(
                    stream,
                    new Envelope { TimingReply = new TimingReply { RequestId = timingRequestId } },
                    pipelineCancellationToken);
                RecordTimingReply(
                    connectionOrdinal,
                    entityId,
                    envelopeReadCompletedTimestamp,
                    timingRequestId);
                EmitCommandTimingOutsideAuthorityLock();
            }
        } catch (ClockGatedTransportException) {
            throw;
        } catch (Exception exception) {
            PoisonClockGatedLane(scheduler, lane, exception);
            throw;
        }
        return currentTick;
    }

    private async ValueTask<ClockedCohortSnapshotPoll> CaptureClockedCohortSnapshotPollAsync(
        string sessionId,
        uint entityId,
        int connectionOrdinal,
        ulong? timingRequestId,
        long? envelopeReadCompletedTimestamp,
        CancellationToken cancellationToken,
        ulong currentTick)
    {
        RequireClockedTransportActive(true, cancellationToken);
        var hasSnapshot = snapshotProvider is not null;
        var materialization = hasSnapshot ? PerformanceRecorder?.Begin() ?? 0 : 0;
        var materializeStartTimestamp = hasSnapshot && CommandTimingRecorder is not null
            ? Stopwatch.GetTimestamp()
            : (long?)null;
        var snapshotTraceOrdinal = hasSnapshot && CommandTimingRecorder is not null
            ? Interlocked.Increment(ref nextTimingOrdinal)
            : 0L;
        if (hasSnapshot && CommandTimingRecorder is not null) {
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot-start",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = snapshotTraceOrdinal,
                EntityId = entityId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                SnapshotMaterializeStartTimestamp = materializeStartTimestamp,
                TimingRequestId = timingRequestId,
                TimingReplyEmitted = timingRequestId.HasValue ? false : null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }

        ClockedCohortSnapshotPoll capture;
        long? materializeEndTimestamp;
        IDisposable? authorityTurn = await EnterCohortAuthorityTurnAsync(
            cancellationToken,
            classifyClockedTransportFailure: true);
        try {
            capture = commandServer.WithSynchronizedSession(sessionId, () => {
                RequireClockedTransportActive(true, cancellationToken);
                RequireCohortHealthy();
                try {
                    var tick = AdvanceCurrentTickCore(currentTick);
                    var events = eventProvider?.DrainEvents(sessionId, entityId, tick);
                    if (events is not null)
                        AuthoritativeEventBatchValidator.Validate(events, tick);

                    Snapshot? snapshot = null;
                    if (snapshotProvider is not null) {
                        snapshot = snapshotProvider.CreateSnapshot(sessionId, entityId, tick);
                        if (snapshot.Tick != tick)
                            throw new InvalidDataException(
                                "The clock-gated snapshot tick does not match the captured event boundary.");
                        var identity = handshake.Identity;
                        snapshot.ContentManifestHash = identity.ContentManifestHash;
                        snapshot.RulesetIdentityHash = identity.RulesetIdentityHash;
                        AuthoritativeSnapshotValidator.Validate(snapshot);
                    }

                    PublishCohortCore();
                    return new ClockedCohortSnapshotPoll(tick, events, snapshot);
                } catch (Exception failure) {
                    FenceCohort(failure);
                    throw;
                }
            });
            materializeEndTimestamp = hasSnapshot && CommandTimingRecorder is not null
                ? Stopwatch.GetTimestamp()
                : null;
        } finally {
            authorityTurn?.Dispose();
        }

        if (hasSnapshot)
            PerformanceRecorder?.Complete(HostPerformanceMetric.SnapshotMaterializeMilliseconds, materialization);
        return new ClockedCohortSnapshotPoll(
            capture.Tick,
            capture.Events,
            capture.Snapshot,
            materializeStartTimestamp,
            materializeEndTimestamp,
            snapshotTraceOrdinal);
    }

    private async ValueTask<IDisposable?> EnterCohortAuthorityTurnAsync(
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure = false)
    {
        if (!UseClockGatedCommandScheduling || CohortStore is null)
            return null;

        try {
            return await cohortAuthorityTurnGate.EnterAsync(cancellationToken);
        } catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested) {
            if (classifyClockedTransportFailure)
                throw new ClockGatedTransportException(exception);
            throw;
        }
    }

    private sealed record ClockedCohortSnapshotPoll(
        ulong Tick,
        EventBatch? Events,
        Snapshot? Snapshot,
        long? MaterializeStartTimestamp = null,
        long? MaterializeEndTimestamp = null,
        long SnapshotTraceOrdinal = 0,
        long Materialization = 0);

    private sealed class ClockedCommandBoundaryCapture(
        string sessionId,
        ulong clientSequence,
        string rulesetIdentityHash,
        object? generationToken,
        CancellationToken cancellationToken)
    {
        private ClockedCohortSnapshotPoll? capture;
        private int writerReady;

        public string SessionId { get; } = sessionId;
        public ulong ClientSequence { get; } = clientSequence;
        public string RulesetIdentityHash { get; } = rulesetIdentityHash;
        public object? GenerationToken { get; } = generationToken;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public bool IsWriterReady => Volatile.Read(ref writerReady) != 0;

        public void MarkWriterReady()
            => Volatile.Write(ref writerReady, 1);

        public bool TryPublish(ClockedCohortSnapshotPoll value)
            => Interlocked.CompareExchange(ref capture, value, null) is null;

        public bool TryGetCapture(out ClockedCohortSnapshotPoll value)
        {
            var current = Volatile.Read(ref capture);
            value = current!;
            return current is not null;
        }
    }

    private async Task EnqueueClockGatedErrorAsync(
        ChannelWriter<ClockGatedResponseWork> workWriter,
        ClockGatedResponseBudget responseBudget,
        ProtocolErrorCode code,
        string detail,
        bool terminal,
        bool reserveResponseWeight,
        CancellationToken cancellationToken)
    {
        var work = ClockGatedResponseWork.CreateError(
            new ProtocolError { Code = code, Detail = detail },
            terminal);
        if (reserveResponseWeight) {
            if (!responseBudget.TryReserve(1))
                throw new ClockGatedCommandCapacityException(
                    "The clock-gated command response capacity is exhausted.");
            work.SetResponseWeight(1);
        }
        try {
            await EnqueueClockGatedWorkAsync(
                workWriter,
                responseBudget,
                work,
                cancellationToken);
        } catch {
            work.Release(responseBudget);
            throw;
        }
    }

    private static async Task EnqueueClockGatedWorkAsync(
        ChannelWriter<ClockGatedResponseWork> workWriter,
        ClockGatedResponseBudget responseBudget,
        ClockGatedResponseWork work,
        CancellationToken cancellationToken)
    {
        try {
            await workWriter.WriteAsync(work, cancellationToken);
        } catch {
            work.Release(responseBudget);
            throw;
        }
    }

    private void ObserveClockGatedTaskFailure(
        Task<CommandAck> task,
        ClockGatedCommandLane lane,
        ClockGatedCommandScheduler scheduler)
    {
        _ = task.ContinueWith(
            completed => {
                if (!completed.IsFaulted)
                    return;
                var failure = completed.Exception?.GetBaseException()
                    ?? new InvalidOperationException("Clock-gated command work failed.");
                PoisonClockGatedLane(scheduler, lane, failure);
                if (lane.IsTransportClosed)
                    ReportClockGatedOrphanFailureOnce(lane, failure);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void PoisonClockGatedLane(
        ClockGatedCommandScheduler scheduler,
        ClockGatedCommandLane lane,
        Exception failure)
    {
        try {
            scheduler.PoisonLane(lane, failure);
        } catch (ObjectDisposedException) {
        }
    }

    private void ReportClockedOrphanFailure(Exception failure)
    {
        if (CohortStore is null) {
            Volatile.Write(ref lastClientFailure, failure);
            try { ClientFailureObserver?.Invoke(failure); }
            catch (Exception diagnosticFailure) {
                try { failure.Data["ClockedOrphanFailureObserver"] = diagnosticFailure; } catch { }
            }
            return;
        }

        WithCohortBarrier(() => Volatile.Write(ref lastClientFailure,
            Volatile.Read(ref cohortFailure) ?? failure));
        try { ClientFailureObserver?.Invoke(failure); }
        catch (Exception diagnosticFailure) {
            try { failure.Data["ClockedOrphanFailureObserver"] = diagnosticFailure; } catch { }
        }
    }

    private void MarkClockGatedTransportClosed(ClockGatedCommandLane lane)
    {
        lane.MarkTransportClosed();
        // A task can fault just before the reader/writer notices disconnect.
        // Recheck the synchronous authority fence instead of losing that race.
        if (lane.Failure is { } failure)
            ReportClockGatedOrphanFailureOnce(lane, failure);
    }

    private void ReportClockGatedOrphanFailureOnce(ClockGatedCommandLane lane, Exception failure)
    {
        if (!lane.TryMarkFailureReported())
            return;
        try { ReportClockedOrphanFailure(failure); }
        catch (Exception diagnosticFailure) {
            Volatile.Write(ref lastClientFailure, failure);
            try { failure.Data["ClockedOrphanFailureNotification"] = diagnosticFailure; } catch { }
        }
    }

    private async ValueTask WriteClockedTransportEnvelopeAsync(
        Stream stream,
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        try {
            await WriteAuthoritativeEnvelopeAsync(stream, envelope, cancellationToken);
        } catch (Exception exception)
            when ((exception is IOException or SocketException or ObjectDisposedException)
                && exception is not ClockGatedTransportException) {
            throw new ClockGatedTransportException(exception);
        } catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested) {
            // Only cancellation at the actual write site is transport loss.
            throw new ClockGatedTransportException(exception);
        }
    }

    private static void RequireClockedTransportActive(bool classifyClockedTransportFailure,
        CancellationToken cancellationToken)
    {
        if (classifyClockedTransportFailure && cancellationToken.IsCancellationRequested)
            throw new ClockGatedTransportException(new OperationCanceledException(cancellationToken));
    }

    private enum ClockGatedResponseKind
    {
        Command,
        ClockProbe,
        Snapshot,
        Error,
    }

    private sealed class ClockGatedResponseWork
    {
        private int released;

        private ClockGatedResponseWork(ClockGatedResponseKind kind)
        {
            Kind = kind;
        }

        public ClockGatedResponseKind Kind { get; }
        public CommandBatch? Batch { get; private init; }
        public Task<CommandAck>? Acknowledgement { get; private init; }
        public ulong TimingRequestId { get; private init; }
        public long? EnvelopeReadCompletedTimestamp { get; private init; }
        public ProtocolError? Error { get; private init; }
        public bool Terminal { get; private init; }
        public int ResponseWeight { get; private set; }
        public IReadOnlyList<long> AdmissionTraceOrdinals { get; private init; } = [];
        public ulong SampledClockCurrentTick { get; private init; }
        public ulong SampledCommandServerCurrentTick { get; private init; }
        public ulong ActualAdmissionTick { get; private init; }
        public long LockWaitStopwatchTicks { get; private init; }
        public ClockedCommandBoundaryCapture? CommitBoundaryCapture { get; private init; }

        public static ClockGatedResponseWork Command(
            CommandBatch batch,
            Task<CommandAck> acknowledgement,
            int responseWeight,
            long? envelopeReadCompletedTimestamp,
            IReadOnlyList<long> admissionTraceOrdinals,
            ulong sampledClockCurrentTick,
            ulong sampledCommandServerCurrentTick,
            ulong actualAdmissionTick,
            long lockWaitStopwatchTicks,
            ClockedCommandBoundaryCapture? commitBoundaryCapture)
            => new(ClockGatedResponseKind.Command) {
                Batch = batch,
                Acknowledgement = acknowledgement,
                ResponseWeight = responseWeight,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                AdmissionTraceOrdinals = admissionTraceOrdinals,
                SampledClockCurrentTick = sampledClockCurrentTick,
                SampledCommandServerCurrentTick = sampledCommandServerCurrentTick,
                ActualAdmissionTick = actualAdmissionTick,
                LockWaitStopwatchTicks = lockWaitStopwatchTicks,
                CommitBoundaryCapture = commitBoundaryCapture,
            };

        public static ClockGatedResponseWork ClockProbe(
            ulong requestId,
            long? envelopeReadCompletedTimestamp)
            => new(ClockGatedResponseKind.ClockProbe) {
                TimingRequestId = requestId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
            };

        public static ClockGatedResponseWork Snapshot(
            ulong timingRequestId,
            long? envelopeReadCompletedTimestamp)
            => new(ClockGatedResponseKind.Snapshot) {
                TimingRequestId = timingRequestId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
            };

        public static ClockGatedResponseWork CreateError(ProtocolError error, bool terminal)
            => new(ClockGatedResponseKind.Error) {
                Error = error,
                Terminal = terminal,
            };

        public void SetResponseWeight(int responseWeight)
            => ResponseWeight = responseWeight;

        public void Release(ClockGatedResponseBudget responseBudget)
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
                responseBudget.Release(ResponseWeight);
        }
    }
}
