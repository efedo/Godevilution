using System.Diagnostics;

namespace Devilution.Client.Diagnostics;

public sealed partial class ClientFrameEvidenceRecorder
{
    /** Town capture measures the normal workload; journey-only draw histories would distort it. */
    public static bool RequestsDetailedPresentationHistory(string? acceptancePhase, string? explicitHistory)
        => explicitHistory == "1" || !string.IsNullOrWhiteSpace(acceptancePhase) && acceptancePhase != "TownCapture";

    private readonly List<ClientFramePhaseSummary> phaseSummaries = [];
    private bool requiresWorkloadCompletion;
    private string? capturePhase;
    private double[]? phaseIntervals;
    private int phaseIntervalCount, phaseFirstSecond;
    private long phaseStartedAt, phasePreviousFrame;

    /** Optional workload labels; collection allocates only at phase boundaries, never per frame. */
    public void BeginPhase(string name, long timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (stopped || awaitingWorldAdmission || timestamp < activeAtTimestamp || timestamp >= stopAtTimestamp)
            throw new InvalidOperationException("A capture phase requires an active admitted-world measurement.");
        if (capturePhase is not null && timestamp <= phaseStartedAt)
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        FlushFrameCosts(timestamp);
        FinishPhase(timestamp);
        requiresWorkloadCompletion = true;
        capturePhase = name;
        phaseStartedAt = timestamp;
        phasePreviousFrame = 0;
        phaseIntervalCount = 0;
        phaseFirstSecond = frameCostSamples.Count;
        // A normal capture can exceed the older 4,096 raw-sample limit (resident approach walks included).
        // One bounded buffer covers the configured window at the normal 60 FPS cap and is reused by every phase.
        phaseIntervals ??= new double[Math.Max(configuration.MaximumSamples,
            checked((int)Math.Ceiling(configuration.MeasurementWindow.TotalSeconds * 60) + 1))];
    }

    private void RecordPhaseFrame(long timestamp)
    {
        if (capturePhase is null) return;
        // Exclude the interval crossing a phase boundary or warmup.
        if (phasePreviousFrame != 0 && timestamp > phasePreviousFrame) {
            if (phaseIntervalCount == phaseIntervals!.Length) droppedSampleCount++;
            else phaseIntervals[phaseIntervalCount++] = ToMilliseconds(timestamp - phasePreviousFrame);
        }
        phasePreviousFrame = timestamp;
    }

    private void FinishPhase(long timestamp)
    {
        if (capturePhase is null) return;
        var intervals = phaseIntervals!.AsSpan(0, phaseIntervalCount);
        intervals.Sort();
        ClientSecondFrameCostSample? worst = null;
        var cpu = new List<double>();
        var allocations = new List<double>();
        for (var index = phaseFirstSecond; index < frameCostSamples.Count; index++) {
            var second = frameCostSamples[index];
            // A final partial second is useful evidence, but not a comparable worst *second*.
            if (second.DurationSeconds >= .9 && (worst is null || second.FramesPerSecond < worst.FramesPerSecond)) worst = second;
            if (second.ProcessCpuMillisecondsPerSecond is { } value) cpu.Add(value);
            allocations.Add(second.ManagedAllocationBytesPerSecond);
        }
        cpu.Sort(); allocations.Sort();
        phaseSummaries.Add(new(capturePhase, (phaseStartedAt - activeAtTimestamp) / (double)Stopwatch.Frequency,
            (timestamp - phaseStartedAt) / (double)Stopwatch.Frequency, phaseIntervalCount,
            Percentile(intervals, .5), Percentile(intervals, .99), intervals.IsEmpty ? null : intervals[^1],
            Percentile(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(cpu), .5),
            Percentile(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(allocations), .5), worst));
        capturePhase = null;
    }

    // Nearest-rank p99 and the usual midpoint median, computed only when a phase ends.
    private static double? Percentile(ReadOnlySpan<double> sorted, double percentile)
        => sorted.IsEmpty ? null : percentile == .5
            ? (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2
            : sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
}

public sealed record ClientFramePhaseSummary(string Phase, double StartSeconds, double DurationSeconds, int FrameIntervalCount,
    double? MedianFrameMilliseconds, double? P99FrameMilliseconds, double? MaximumFrameMilliseconds,
    double? MedianProcessCpuMillisecondsPerSecond, double? MedianManagedAllocationBytesPerSecond,
    ClientSecondFrameCostSample? WorstSecond);
