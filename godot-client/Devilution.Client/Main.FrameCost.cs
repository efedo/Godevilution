using Devilution.Client.Protocol;
using Devilution.Client.Diagnostics;
using Godot;

namespace Devilution.Client;

public partial class Main
{
    private readonly AuthoritativeSpellSourceCache frameSpellSources = new();
    private double admissionMilliseconds, worldMilliseconds, hudMilliseconds;
    private long admissionAllocated, worldAllocated, hudAllocated;
    private ClientTransportAllocationObservation previousTransportAllocations;
    private bool renderTimeMeasurementEnabled;
    private bool normalFrameCaptureAttempted;
    private readonly bool normalFrameCaptureRequested = ClientFrameEvidenceRecorder.UsesNormalFrameCapture(
        System.Environment.GetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT"),
        System.Environment.GetEnvironmentVariable(ClientFrameEvidenceRecorder.FrameCaptureEnvironmentVariable));
    private long FrameCostAllocation() => frameEvidence is {IsStopped: false} ? GC.GetAllocatedBytesForCurrentThread() : 0;
    private long FrameCostAllocationSince(long start) => start == 0 ? 0 : GC.GetAllocatedBytesForCurrentThread() - start;
    private long FrameCostTimestamp() => frameEvidence is {IsStopped: false} ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
    private static double FrameCostElapsed(long started) => started == 0 ? 0 :
        (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;

    private void BeginFrameCostFrame()
    {
        if (frameEvidence is null && normalFrameCaptureRequested && !normalFrameCaptureAttempted) {
            normalFrameCaptureAttempted = true;
            if (!StartPerformanceRecording(model.Snapshot?.Players.Count ?? 1, 0, environmentWarmup: true))
                GD.PushWarning("Frame capture could not start: check the fresh output directory and capture settings.");
        }
        if (model.Snapshot is not null)
            frameEvidence?.ObserveWorldAdmission(ClientFrameEvidenceRecorder.GetTimestamp());
        admissionMilliseconds = worldMilliseconds = hudMilliseconds = 0;
        admissionAllocated = worldAllocated = hudAllocated = 0;
        if (world is not null) world.CaptureFrameCost = frameEvidence is {IsStopped: false};
        if (renderTimeMeasurementEnabled && frameEvidence is {IsStopped: true}) {
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), false);
            renderTimeMeasurementEnabled = false;
        }
        if (!renderTimeMeasurementEnabled && frameEvidence is {IsStopped: false} && DisplayServer.GetName() != "headless") {
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
            renderTimeMeasurementEnabled = true;
        }
    }

    private void RecordFrameCosts(ClientFrameEvidenceRecorder recorder, long timestamp)
    {
        if (recorder.IsStopped) return;
        var viewport = GetViewport().GetViewportRid();
        var transport = client?.AllocationMetrics?.Capture() ?? default;
        var previous = previousTransportAllocations;
        previousTransportAllocations = transport;
        recorder.RecordFrameCost(timestamp, new(admissionMilliseconds, worldMilliseconds, hudMilliseconds,
            world?.ConsumeDrawMilliseconds() ?? 0, world?.ConsumeTerrainSubmissions() ?? 0,
            Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            renderTimeMeasurementEnabled ? RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) : null,
            renderTimeMeasurementEnabled ? RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport) : null,
            admissionAllocated, worldAllocated, hudAllocated, world?.ConsumeDrawAllocatedBytes() ?? 0,
            Math.Max(0, transport.SnapshotCount - previous.SnapshotCount),
            Math.Max(0, transport.SnapshotWireBytes - previous.SnapshotWireBytes),
            Math.Max(0, transport.DecodeAllocatedBytes - previous.DecodeAllocatedBytes),
            Math.Max(0, transport.ProcessingAllocatedBytes - previous.ProcessingAllocatedBytes)));
    }

}
