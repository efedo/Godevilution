using System.Diagnostics;

namespace Devilution.Server.Host;

public enum HostPerformanceMetric
{
    PeriodicAdvanceAndCheckpointMilliseconds,
    CommandAdmissionAndCheckpointMilliseconds,
    SnapshotMaterializeMilliseconds,
    SnapshotWriteMilliseconds,
    SnapshotEnvelopePayloadBytes,
    PeriodicAdvanceLockInclusiveMilliseconds,
    SessionCheckpointLockWaitMilliseconds,
    SessionStateAndLedgerCaptureMilliseconds,
    CombinedCheckpointPublishMilliseconds,
    CompatibilityStatePublishMilliseconds,
    CompatibilityReceiptsPublishMilliseconds,
    CommandQueueAdmissionMilliseconds,
    ScheduledCommandCheckpointMilliseconds,
}

/** Opt-in, bounded diagnostics. No session IDs, credentials, or gameplay state. */
public sealed class HostPerformanceRecorder
{
    public const int MaximumSamplesPerMetric = 20000;
    private readonly object synchronization = new();
    private readonly Dictionary<HostPerformanceMetric, List<double>> samples =
        Enum.GetValues<HostPerformanceMetric>().ToDictionary(metric => metric, _ => new List<double>());
    private readonly Dictionary<HostPerformanceMetric, long> dropped =
        Enum.GetValues<HostPerformanceMetric>().ToDictionary(metric => metric, _ => 0L);
    private long startedAt;
    private DateTimeOffset startedUtc;
    private int state;

    /** Start once, after the caller's warm-up. Repeated starts cannot erase evidence. */
    public bool Start()
    {
        lock (synchronization) {
            if (state != 0)
                return false;
            startedAt = Stopwatch.GetTimestamp();
            startedUtc = DateTimeOffset.UtcNow;
            Volatile.Write(ref state, 1);
            return true;
        }
    }

    public long Begin() => Volatile.Read(ref state) == 1 ? Stopwatch.GetTimestamp() : 0;

    public void Complete(HostPerformanceMetric metric, long timestamp)
    {
        if (timestamp != 0)
            Record(metric, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
    }

    public void Record(HostPerformanceMetric metric, double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        lock (synchronization) {
            if (state != 1)
                return;
            var values = samples[metric];
            if (values.Count < MaximumSamplesPerMetric)
                values.Add(value);
            else
                dropped[metric]++;
        }
    }

    /** Freeze completed operations only; unfinished or failed work is not a success sample. */
    public HostPerformanceReport Stop()
    {
        lock (synchronization) {
            if (state != 1)
                throw new InvalidOperationException("Performance recording is not active.");
            Volatile.Write(ref state, 2);
            return new HostPerformanceReport(startedUtc, Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                samples.ToDictionary(pair => pair.Key.ToString(), pair => Summarize(pair.Value, dropped[pair.Key])));
        }
    }

    private static HostPerformanceSummary Summarize(List<double> samples, long dropped)
    {
        var sorted = samples.Order().ToArray();
        if (sorted.Length == 0)
            return new(0, dropped, null, null, null, null);
        // Nearest rank; all statistics refer to retained samples, never silent truncation.
        double Percentile(double fraction) => sorted[(int)Math.Ceiling(fraction * sorted.Length) - 1];
        return new(sorted.Length, dropped, sorted.Average(), Percentile(0.50), Percentile(0.95), sorted[^1]);
    }
}

public sealed record HostPerformanceSummary(int Samples, long DroppedSamples,
    double? Mean, double? P50, double? P95, double? Maximum);

public sealed record HostPerformanceReport(DateTimeOffset StartedUtc, double ElapsedSeconds,
    IReadOnlyDictionary<string, HostPerformanceSummary> Metrics);
