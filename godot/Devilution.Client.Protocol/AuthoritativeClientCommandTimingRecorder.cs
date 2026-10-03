using System.Diagnostics;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/**
 * Opt-in, in-memory diagnostics for client command dispatch timing. The
 * recorder intentionally contains only numeric and enum metadata; it never
 * retains protocol payloads, session tokens, or gameplay identity.
 */
public sealed class AuthoritativeClientCommandTimingRecorder
{
    public const int DefaultCapacity = 4096;

    private readonly object sync = new();
    private readonly int capacity;
    private readonly List<AuthoritativeClientCommandTiming> commands = [];
    private readonly List<AuthoritativeClientProbeSentTiming> probeSent = [];
    private readonly List<AuthoritativeClientProbeOutcomeTiming> probeOutcomes = [];
    private readonly List<AuthoritativeClientSnapshotTiming> snapshots = [];
    private readonly List<AuthoritativeClientAcknowledgementTiming> acknowledgements = [];
    private long droppedRecordCount;
    private int recordCount;

    public AuthoritativeClientCommandTimingRecorder(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "The timing recorder capacity must be positive.");
        this.capacity = capacity;
    }

    /** Returns a detached snapshot; later client activity cannot mutate it. */
    public AuthoritativeClientTimingCaptureReport CaptureReport()
    {
        lock (sync) {
            return new AuthoritativeClientTimingCaptureReport(
                Array.AsReadOnly(commands.ToArray()),
                Array.AsReadOnly(probeSent.ToArray()),
                Array.AsReadOnly(probeOutcomes.ToArray()),
                Array.AsReadOnly(snapshots.ToArray()),
                Array.AsReadOnly(acknowledgements.ToArray()),
                Stopwatch.Frequency,
                droppedRecordCount);
        }
    }

    internal void RecordCommand(AuthoritativeClientCommandTiming timing)
    {
        lock (sync) {
            if (!ReserveRecord())
                return;
            commands.Add(timing);
        }
    }

    internal void RecordProbeSent(AuthoritativeClientProbeSentTiming timing)
    {
        lock (sync) {
            if (!ReserveRecord())
                return;
            probeSent.Add(timing);
        }
    }

    internal void RecordProbeOutcome(AuthoritativeClientProbeOutcomeTiming timing)
    {
        lock (sync) {
            if (!ReserveRecord())
                return;
            probeOutcomes.Add(timing);
        }
    }

    internal void RecordSnapshot(AuthoritativeClientSnapshotTiming timing)
    {
        lock (sync) {
            if (!ReserveRecord())
                return;
            snapshots.Add(timing);
        }
    }

    internal void RecordAcknowledgement(AuthoritativeClientAcknowledgementTiming timing)
    {
        lock (sync) {
            if (!ReserveRecord())
                return;
            acknowledgements.Add(timing);
        }
    }

    private bool ReserveRecord()
    {
        if (recordCount >= capacity) {
            droppedRecordCount++;
            return false;
        }
        recordCount++;
        return true;
    }
}

/** A detached, immutable view of all records retained by a timing recorder. */
public sealed record AuthoritativeClientTimingCaptureReport(
    IReadOnlyList<AuthoritativeClientCommandTiming> Commands,
    IReadOnlyList<AuthoritativeClientProbeSentTiming> ProbeSent,
    IReadOnlyList<AuthoritativeClientProbeOutcomeTiming> ProbeOutcomes,
    IReadOnlyList<AuthoritativeClientSnapshotTiming> Snapshots,
    IReadOnlyList<AuthoritativeClientAcknowledgementTiming> Acknowledgements,
    long StopwatchFrequency,
    long DroppedRecordCount);

public enum AuthoritativeClientCommandDispatchKind
{
    FirstDispatch = 0,
    Retransmission = 1,
}

public enum AuthoritativeClientProbeOutcome
{
    Reply = 0,
    Expired = 1,
}

public enum AuthoritativeClientWriteOutcome
{
    Failed = 0,
    Completed = 1,
}

/**
 * FreezeElapsedMilliseconds is the existing monotonic value captured before
 * protobuf serialization. Write timestamps describe the attempted stream
 * write, and are not a claim about physical wire departure.
 */
public sealed record AuthoritativeClientCommandTiming(
    ulong ClientSequence,
    long TransportGeneration,
    AuthoritativeClientCommandDispatchKind DispatchKind,
    int Attempt,
    ulong? ObservedTick,
    ulong RequestedTick,
    long FreezeElapsedMilliseconds,
    long? WriteStartElapsedMilliseconds,
    long? WriteEndElapsedMilliseconds,
    AuthoritativeClientWriteOutcome WriteOutcome,
    long FreezeStopwatchTimestamp,
    long? WriteStartStopwatchTimestamp,
    long? WriteEndStopwatchTimestamp,
    double ObservedRoundTripEstimateMilliseconds,
    bool TimingCalibrationReady);

public sealed record AuthoritativeClientProbeSentTiming(
    ulong TimingRequestId,
    long TransportGeneration,
    bool RequiresCalibration,
    long SentElapsedMilliseconds,
    long? WriteStartElapsedMilliseconds,
    long? WriteEndElapsedMilliseconds,
    AuthoritativeClientWriteOutcome WriteOutcome,
    long SentStopwatchTimestamp,
    long? WriteStartStopwatchTimestamp,
    long? WriteEndStopwatchTimestamp,
    double ObservedRoundTripEstimateMilliseconds,
    bool TimingCalibrationReady);

public sealed record AuthoritativeClientProbeOutcomeTiming(
    ulong TimingRequestId,
    long TransportGeneration,
    AuthoritativeClientProbeOutcome Outcome,
    long OutcomeElapsedMilliseconds,
    long? RoundTripMilliseconds,
    long OutcomeStopwatchTimestamp,
    bool TimingCalibrationReady);

public sealed record AuthoritativeClientSnapshotTiming(
    long TransportGeneration,
    ulong SnapshotTick,
    long ReceivedElapsedMilliseconds,
    ulong? PendingTimingRequestIdAtReceipt,
    long ReceivedStopwatchTimestamp,
    bool TimingCalibrationReady);

public sealed record AuthoritativeClientAcknowledgementTiming(
    long TransportGeneration,
    ulong ClientSequence,
    long ReceivedElapsedMilliseconds,
    CommandStatus Status,
    CommandRejectReason RejectReason,
    ulong AppliedTick,
    bool TrackedPendingCommand,
    bool WasRetransmitted,
    long? CommandRoundTripMilliseconds,
    long ReceivedStopwatchTimestamp);
