using System.Diagnostics;

namespace Devilution.Server.Host;

/**
 * Bounded, opt-in host timing evidence. This recorder stores only detached
 * scalar values; it never invokes an observer and therefore is safe to use
 * while an authoritative monitor is held. Callers drain the immutable records
 * after leaving that monitor.
 */
public sealed class HostCommandTimingRecorder
{
    public const int DefaultCapacity = 4096;

    private readonly object synchronization = new();
    private readonly Queue<HostCommandTimingRecord> records = new();
    private readonly int capacity;
    private long droppedCount;
    private long reportedDroppedCount;
    private long failureCount;
    private long reportedFailureCount;

    public HostCommandTimingRecorder(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    public int Capacity => capacity;

    /** Total records discarded because the bounded buffer was full. */
    public long DroppedCount => Volatile.Read(ref droppedCount);

    /** Diagnostic recorder failures; a nonzero value makes evidence incomplete. */
    public long FailureCount => Volatile.Read(ref failureCount);

    /** Records an output/observer fault without affecting authoritative work. */
    public void MarkObserverFailure() => Interlocked.Increment(ref failureCount);

    public int Count
    {
        get
        {
            lock (synchronization)
                return records.Count;
        }
    }

    /**
     * Adds one detached observation. A recorder failure is diagnostic only and
     * is deliberately unable to affect command admission or simulation.
     */
    public void Record(HostCommandTimingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try {
            lock (synchronization) {
                if (records.Count >= capacity) {
                    Interlocked.Increment(ref droppedCount);
                    return;
                }
                records.Enqueue(record);
            }
        } catch {
            Interlocked.Increment(ref droppedCount);
            Interlocked.Increment(ref failureCount);
        }
    }

    /**
     * Removes all currently buffered records and returns the newly observed
     * drop count. The returned list is immutable from the recorder's point of
     * view and may be serialized or handed to an asynchronous sink.
     */
    public HostCommandTimingDrain Drain()
    {
        try {
            lock (synchronization) {
                IReadOnlyList<HostCommandTimingRecord> drained =
                    Array.AsReadOnly(records.ToArray());
                records.Clear();
                var dropped = Volatile.Read(ref droppedCount);
                var newlyDropped = dropped - reportedDroppedCount;
                reportedDroppedCount = dropped;
                var failures = Volatile.Read(ref failureCount);
                var captureFaulted = failures != reportedFailureCount;
                reportedFailureCount = failures;
                return new HostCommandTimingDrain(
                    drained,
                    newlyDropped,
                    captureFaulted,
                    failures);
            }
        } catch {
            Interlocked.Increment(ref failureCount);
            return new HostCommandTimingDrain([], 0, true, FailureCount);
        }
    }
}

/** Immutable batch detached from the bounded timing recorder. */
public sealed record HostCommandTimingDrain(
    IReadOnlyList<HostCommandTimingRecord> Records,
    long DroppedCount,
    bool CaptureFaulted = false,
    long FailureCount = 0);

/**
 * A diagnostic observation correlated only by public entity/sequence and
 * process-local connection/trace ordinals. Null values mean that the host did
 * not have a truthful value at that boundary.
 */
public sealed record HostCommandTimingRecord
{
    public string Kind { get; init; } = string.Empty;
    public int ConnectionOrdinal { get; init; }
    public long TraceOrdinal { get; init; }
    public uint? EntityId { get; init; }
    public ulong? ClientSequence { get; init; }

    /** Stopwatch.GetTimestamp() captured after full envelope read and parse. */
    public long? EnvelopeReadCompletedTimestamp { get; init; }
    public long StopwatchFrequency { get; init; } = Stopwatch.Frequency;
    public long? AuthorityLockWaitStopwatchTicks { get; init; }

    /** Existing samples used by the host at the command admission boundary. */
    public ulong? SampledClockCurrentTick { get; init; }
    public ulong? SampledCommandServerCurrentTick { get; init; }
    /** Exact batch-boundary tick supplied to SubmitBatchAsync/ProcessBatch. */
    public ulong? ActualAdmissionTick { get; init; }
    public ulong? RequestedTick { get; init; }
    public string? AckStatus { get; init; }
    public string? AckReason { get; init; }
    public ulong? ReceiptSequence { get; init; }
    /** Protocol-shaped aliases retained for straightforward evidence joins. */
    public string? Status { get; init; }
    public string? RejectReason { get; init; }
    public ulong? ServerReceiptSequence { get; init; }
    public ulong? AppliedTick { get; init; }
    public long? AckWriteStartTimestamp { get; init; }
    public long? AckWriteEndTimestamp { get; init; }
    public bool? AckWriteCompleted { get; init; }

    public long? SnapshotMaterializeStartTimestamp { get; init; }
    public long? SnapshotMaterializeEndTimestamp { get; init; }
    public long? SnapshotWriteStartTimestamp { get; init; }
    public long? SnapshotWriteEndTimestamp { get; init; }
    public ulong? SnapshotTick { get; init; }
    public ulong? TimingRequestId { get; init; }
    public bool? TimingReplyEmitted { get; init; }
}
