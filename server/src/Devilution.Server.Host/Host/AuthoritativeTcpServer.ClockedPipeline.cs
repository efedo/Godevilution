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
                    return scheduler.SubmitBatchAsync(
                        sessionId,
                        batch,
                        actualAdmissionTick,
                        pipelineCancellationToken,
                        lane);
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
                lockWaitStopwatchTicks);
            retainWeight = false;
            return work;
        } finally {
            if (retainWeight)
                responseBudget.Release(responseWeight);
        }
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
                // The scheduler has already persisted this receipt. Capture the
                // command response projection as one cohort boundary before writing it.
                cohortCapture = CaptureClockedCohortSnapshotPoll(
                    sessionId,
                    entityId,
                    connectionOrdinal,
                    null,
                    work.EnvelopeReadCompletedTimestamp,
                    pipelineCancellationToken,
                    currentTick);
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
            var schedulingClockTick = commandServer.WithSynchronizedState(() => {
                RequireClockedTransportActive(true, pipelineCancellationToken);
                RequireCohortHealthy();
                return Math.Max(clock.CurrentTick, commandServer.CurrentTick);
            });
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
                var capture = CaptureClockedCohortSnapshotPoll(
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

    private ClockedCohortSnapshotPoll CaptureClockedCohortSnapshotPoll(
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

        var capture = commandServer.WithSynchronizedSession(sessionId, () => {
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

        var materializeEndTimestamp = hasSnapshot && CommandTimingRecorder is not null
            ? Stopwatch.GetTimestamp()
            : (long?)null;
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

    private sealed record ClockedCohortSnapshotPoll(
        ulong Tick,
        EventBatch? Events,
        Snapshot? Snapshot,
        long? MaterializeStartTimestamp = null,
        long? MaterializeEndTimestamp = null,
        long SnapshotTraceOrdinal = 0);

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

        public static ClockGatedResponseWork Command(
            CommandBatch batch,
            Task<CommandAck> acknowledgement,
            int responseWeight,
            long? envelopeReadCompletedTimestamp,
            IReadOnlyList<long> admissionTraceOrdinals,
            ulong sampledClockCurrentTick,
            ulong sampledCommandServerCurrentTick,
            ulong actualAdmissionTick,
            long lockWaitStopwatchTicks)
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
