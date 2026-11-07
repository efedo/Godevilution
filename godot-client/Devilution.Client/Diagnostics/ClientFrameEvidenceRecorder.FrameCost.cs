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
    private long costWindowStart, costAllocatedStart, costTerrain;
    private double? costCpuStart;
    private int costFrames;
    private double costAdmission, costWorld, costHud, costDraw, costDrawCalls, costRenderCpu, costRenderGpu;
    private int costRenderSamples;

    /** Aggregates real process/renderer observations once per second; no gameplay data is retained. */
    public void RecordFrameCost(long timestamp, ClientFrameCostMetrics metrics)
    {
        if (stopped || timestamp < activeAtTimestamp) return;
        if (timestamp >= stopAtTimestamp) { FlushFrameCosts(timestamp); return; }
        if (costWindowStart == 0) {
            costWindowStart = timestamp;
            costAllocatedStart = GC.GetTotalAllocatedBytes(false);
            costCpuStart = CaptureProcessObservation().TotalProcessorTimeMilliseconds;
        }
        if (timestamp - costWindowStart >= Stopwatch.Frequency) FlushFrameCosts(timestamp);
        costFrames++;
        costAdmission += metrics.SnapshotAdmissionMilliseconds; costWorld += metrics.WorldUpdateMilliseconds;
        costHud += metrics.HudUpdateMilliseconds; costDraw += metrics.DrawMilliseconds;
        costTerrain += metrics.TerrainSubmissions; costDrawCalls += metrics.RenderDrawCalls;
        if (metrics.ViewportRenderCpuMilliseconds is { } cpu && metrics.ViewportRenderGpuMilliseconds is { } gpu) {
            costRenderCpu += cpu; costRenderGpu += gpu; costRenderSamples++;
        }
    }

    private void FlushFrameCosts(long timestamp)
    {
        if (costWindowStart == 0 || costFrames == 0 || timestamp <= costWindowStart) return;
        var seconds = (timestamp - costWindowStart) / (double)Stopwatch.Frequency;
        var allocated = GC.GetTotalAllocatedBytes(false);
        var cpu = CaptureProcessObservation().TotalProcessorTimeMilliseconds;
        if (frameCostSamples.Count < 3601) frameCostSamples.Add(new(
            (costWindowStart - activeAtTimestamp) / (double)Stopwatch.Frequency, seconds, costFrames / seconds,
            metadata.FrameCap, metadata.VsyncMode, costAdmission / costFrames, costWorld / costFrames,
            costHud / costFrames, costDraw / costFrames, Math.Max(0, allocated - costAllocatedStart) / seconds,
            cpu.HasValue && costCpuStart.HasValue ? Math.Max(0, cpu.Value - costCpuStart.Value) / seconds : null,
            costTerrain, costDrawCalls / costFrames,
            costRenderSamples == 0 ? null : costRenderCpu / costRenderSamples,
            costRenderSamples == 0 ? null : costRenderGpu / costRenderSamples));
        costWindowStart = timestamp; costAllocatedStart = allocated; costCpuStart = cpu;
        costFrames = 0; costAdmission = costWorld = costHud = costDraw = costDrawCalls = costRenderCpu = costRenderGpu = 0;
        costTerrain = 0; costRenderSamples = 0;
    }
}

public readonly record struct ClientFrameCostMetrics(double SnapshotAdmissionMilliseconds, double WorldUpdateMilliseconds,
    double HudUpdateMilliseconds, double DrawMilliseconds, long TerrainSubmissions, double RenderDrawCalls,
    double? ViewportRenderCpuMilliseconds, double? ViewportRenderGpuMilliseconds);

public sealed record ClientSecondFrameCostSample(double StartSeconds, double DurationSeconds, double FramesPerSecond,
    int? FrameCap, string? VsyncMode, double SnapshotAdmissionMillisecondsPerFrame, double WorldUpdateMillisecondsPerFrame,
    double HudUpdateMillisecondsPerFrame, double DrawMillisecondsPerFrame, double ManagedAllocationBytesPerSecond,
    double? ProcessCpuMillisecondsPerSecond, long TerrainSubmissions, double RenderTotalDrawCallsPerFrame,
    double? ViewportRenderCpuMillisecondsPerFrame, double? ViewportRenderGpuMillisecondsPerFrame);
