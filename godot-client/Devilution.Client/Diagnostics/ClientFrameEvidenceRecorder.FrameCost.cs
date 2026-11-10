using System.Diagnostics;

namespace Devilution.Client.Diagnostics;

public sealed partial class ClientFrameEvidenceRecorder
{
    public const string FrameCaptureEnvironmentVariable = "GODEVILUTION_CLIENT_FRAME_CAPTURE";
    public static bool IsRequested => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(FrameCaptureEnvironmentVariable));
    public static bool UsesNormalFrameCapture(string? performanceOutput, string? frameOutput)
        => string.IsNullOrWhiteSpace(performanceOutput) && !string.IsNullOrWhiteSpace(frameOutput);
    private readonly List<ClientSecondFrameCostSample> frameCostSamples = [];
    private readonly List<ClientSecondFrameCostSample> menuFrameCostSamples = [];
    private long costWindowStart, costAllocatedStart, costMainAllocatedStart, costTerrain, costPauseStart;
    private long costAdmissionBytes, costWorldBytes, costHudBytes, costDrawBytes;
    private long costSnapshots, costSnapshotBytes, costDecodeBytes, costProcessingBytes;
    private int costThread, costGen0, costGen1, costGen2;
    private double? costCpuStart;
    private int costFrames;
    private double costAdmission, costWorld, costHud, costDraw, costDrawCalls, costRenderCpu, costRenderGpu;
    private int costRenderSamples, costFrameSetupSamples;
    private double costProcess, costInput, costPrediction, costFrameSetup;
    private long costInputBytes, costPredictionBytes;

    /** Aggregates real process/renderer observations once per second; no gameplay data is retained. */
    public void RecordFrameCost(long timestamp, ClientFrameCostMetrics metrics)
    {
        if (stopped || !awaitingWorldAdmission && timestamp < activeAtTimestamp) return;
        if (!awaitingWorldAdmission && timestamp >= stopAtTimestamp) { FlushFrameCosts(timestamp); return; }
        if (costWindowStart == 0) {
            costWindowStart = timestamp;
            costAllocatedStart = GC.GetTotalAllocatedBytes(false);
            costMainAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
            costThread = Environment.CurrentManagedThreadId;
            costGen0 = GC.CollectionCount(0); costGen1 = GC.CollectionCount(1); costGen2 = GC.CollectionCount(2);
            costPauseStart = GC.GetTotalPauseDuration().Ticks;
            costCpuStart = CaptureProcessObservation().TotalProcessorTimeMilliseconds;
        }
        if (timestamp - costWindowStart >= Stopwatch.Frequency) FlushFrameCosts(timestamp);
        costFrames++;
        costAdmission += metrics.SnapshotAdmissionMilliseconds; costWorld += metrics.WorldUpdateMilliseconds;
        costHud += metrics.HudUpdateMilliseconds; costDraw += metrics.DrawMilliseconds;
        costAdmissionBytes += metrics.SnapshotAdmissionAllocatedBytes; costWorldBytes += metrics.WorldUpdateAllocatedBytes;
        costHudBytes += metrics.HudUpdateAllocatedBytes; costDrawBytes += metrics.DrawAllocatedBytes;
        costSnapshots += metrics.SnapshotsReceived; costSnapshotBytes += metrics.SnapshotWireBytesReceived;
        costDecodeBytes += metrics.SnapshotDecodeAllocatedBytes; costProcessingBytes += metrics.SnapshotProcessingAllocatedBytes;
        // Process includes admission/world/HUD; input and prediction can overlap it or each other.
        costProcess += metrics.ProcessMilliseconds; costInput += metrics.InputMilliseconds;
        costPrediction += metrics.PredictionMilliseconds;
        costInputBytes += metrics.InputAllocatedBytes; costPredictionBytes += metrics.PredictionAllocatedBytes;
        if (metrics.RenderFrameSetupCpuMilliseconds is { } setup) { costFrameSetup += setup; costFrameSetupSamples++; }
        costTerrain += metrics.TerrainSubmissions; costDrawCalls += metrics.RenderDrawCalls;
        if (metrics.ViewportRenderCpuMilliseconds is { } cpu && metrics.ViewportRenderGpuMilliseconds is { } gpu) {
            costRenderCpu += cpu; costRenderGpu += gpu; costRenderSamples++;
        }
    }

    /** Completes the callback after recording, so its own capture overhead is accounted for. */
    public void RecordProcessCost(long timestamp, double milliseconds)
    {
        if (stopped || !awaitingWorldAdmission && (timestamp < activeAtTimestamp || timestamp >= stopAtTimestamp)) return;
        costProcess += milliseconds;
    }

    private void FlushFrameCosts(long timestamp)
    {
        if (costWindowStart == 0 || costFrames == 0 || timestamp <= costWindowStart) return;
        var seconds = (timestamp - costWindowStart) / (double)Stopwatch.Frequency;
        var allocated = GC.GetTotalAllocatedBytes(false);
        var mainAllocated = GC.GetAllocatedBytesForCurrentThread();
        var sameThread = costThread == Environment.CurrentManagedThreadId;
        var processRate = Math.Max(0, allocated - costAllocatedStart) / seconds;
        double? mainRate = sameThread ? Math.Max(0, mainAllocated - costMainAllocatedStart) / seconds : null;
        var gen0 = GC.CollectionCount(0); var gen1 = GC.CollectionCount(1); var gen2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration().Ticks;
        var cpu = CaptureProcessObservation().TotalProcessorTimeMilliseconds;
        var section = awaitingWorldAdmission ? menuFrameCostSamples : frameCostSamples;
        var sectionStart = awaitingWorldAdmission ? createdAtTimestamp : activeAtTimestamp;
        if (section.Count < 3601) section.Add(new(
            (costWindowStart - sectionStart) / (double)Stopwatch.Frequency, seconds, costFrames / seconds,
            metadata.FrameCap, metadata.VsyncMode, costAdmission / costFrames, costWorld / costFrames,
            costHud / costFrames, costDraw / costFrames, processRate,
            cpu.HasValue && costCpuStart.HasValue ? Math.Max(0, cpu.Value - costCpuStart.Value) / seconds : null,
            costTerrain, costDrawCalls / costFrames,
            costRenderSamples == 0 ? null : costRenderCpu / costRenderSamples,
            costRenderSamples == 0 ? null : costRenderGpu / costRenderSamples,
            mainRate, mainRate.HasValue ? Math.Max(0, processRate - mainRate.Value) : null,
            costAdmissionBytes / seconds, costWorldBytes / seconds, costHudBytes / seconds, costDrawBytes / seconds,
            (gen0 - costGen0) / seconds, (gen1 - costGen1) / seconds, (gen2 - costGen2) / seconds,
            Math.Max(0, pause - costPauseStart) / (double)TimeSpan.TicksPerMillisecond / seconds,
            costSnapshots / seconds, costSnapshotBytes / seconds, costDecodeBytes / seconds, costProcessingBytes / seconds,
            costProcess / costFrames, costInput / costFrames, costPrediction / costFrames,
            costFrameSetupSamples == 0 ? null : costFrameSetup / costFrameSetupSamples,
            costInputBytes / seconds, costPredictionBytes / seconds));
        costWindowStart = timestamp; costAllocatedStart = allocated; costMainAllocatedStart = mainAllocated; costCpuStart = cpu;
        costThread = Environment.CurrentManagedThreadId; costGen0 = gen0; costGen1 = gen1; costGen2 = gen2; costPauseStart = pause;
        costFrames = 0; costAdmission = costWorld = costHud = costDraw = costDrawCalls = costRenderCpu = costRenderGpu = 0;
        costAdmissionBytes = costWorldBytes = costHudBytes = costDrawBytes = 0;
        costSnapshots = costSnapshotBytes = costDecodeBytes = costProcessingBytes = 0;
        costTerrain = 0; costRenderSamples = costFrameSetupSamples = 0;
        costProcess = costInput = costPrediction = costFrameSetup = 0;
        costInputBytes = costPredictionBytes = 0;
    }
}

public readonly record struct ClientFrameCostMetrics(double SnapshotAdmissionMilliseconds, double WorldUpdateMilliseconds,
    double HudUpdateMilliseconds, double DrawMilliseconds, long TerrainSubmissions, double RenderDrawCalls,
    double? ViewportRenderCpuMilliseconds, double? ViewportRenderGpuMilliseconds,
    long SnapshotAdmissionAllocatedBytes = 0, long WorldUpdateAllocatedBytes = 0, long HudUpdateAllocatedBytes = 0,
    long DrawAllocatedBytes = 0, long SnapshotsReceived = 0, long SnapshotWireBytesReceived = 0,
    long SnapshotDecodeAllocatedBytes = 0, long SnapshotProcessingAllocatedBytes = 0,
    double ProcessMilliseconds = 0, double InputMilliseconds = 0, double PredictionMilliseconds = 0,
    double? RenderFrameSetupCpuMilliseconds = null, long InputAllocatedBytes = 0, long PredictionAllocatedBytes = 0);

public sealed record ClientSecondFrameCostSample(double StartSeconds, double DurationSeconds, double FramesPerSecond,
    int? FrameCap, string? VsyncMode, double SnapshotAdmissionMillisecondsPerFrame, double WorldUpdateMillisecondsPerFrame,
    double HudUpdateMillisecondsPerFrame, double DrawMillisecondsPerFrame, double ManagedAllocationBytesPerSecond,
    double? ProcessCpuMillisecondsPerSecond, long TerrainSubmissions, double RenderTotalDrawCallsPerFrame,
    double? ViewportRenderCpuMillisecondsPerFrame, double? ViewportRenderGpuMillisecondsPerFrame,
    double? MainThreadAllocationBytesPerSecond = null, double? BackgroundAllocationBytesPerSecond = null,
    double SnapshotAdmissionAllocatedBytesPerSecond = 0, double WorldUpdateAllocatedBytesPerSecond = 0,
    double HudUpdateAllocatedBytesPerSecond = 0, double DrawAllocatedBytesPerSecond = 0,
    double Gen0CollectionsPerSecond = 0, double Gen1CollectionsPerSecond = 0, double Gen2CollectionsPerSecond = 0,
    double GcPauseMillisecondsPerSecond = 0, double SnapshotsReceivedPerSecond = 0, double SnapshotWireBytesPerSecond = 0,
    double SnapshotDecodeAllocatedBytesPerSecond = 0, double SnapshotProcessingAllocatedBytesPerSecond = 0,
    double ProcessMillisecondsPerFrame = 0, double InputMillisecondsPerFrame = 0, double PredictionMillisecondsPerFrame = 0,
    double? RenderFrameSetupCpuMillisecondsPerFrame = null, double InputAllocatedBytesPerSecond = 0,
    double PredictionAllocatedBytesPerSecond = 0);
