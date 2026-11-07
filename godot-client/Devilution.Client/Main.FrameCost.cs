using Devilution.Client.Protocol;
using Devilution.Client.Diagnostics;
using Godot;

namespace Devilution.Client;

public partial class Main
{
    private readonly AuthoritativeSpellSourceCache frameSpellSources = new();
    private double admissionMilliseconds, worldMilliseconds, hudMilliseconds;
    private bool renderTimeMeasurementEnabled;
    private bool normalFrameCaptureAttempted;
    private readonly bool normalFrameCaptureRequested = ClientFrameEvidenceRecorder.UsesNormalFrameCapture(
        System.Environment.GetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT"),
        System.Environment.GetEnvironmentVariable(ClientFrameEvidenceRecorder.FrameCaptureEnvironmentVariable));
    private long FrameCostTimestamp() => frameEvidence is {IsStopped: false} ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
    private static double FrameCostElapsed(long started) => started == 0 ? 0 :
        (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;

    private void BeginFrameCostFrame()
    {
        if (frameEvidence is null && normalFrameCaptureRequested && !normalFrameCaptureAttempted
            && model.Snapshot is {Players.Count: > 0} snapshot && client?.ServerIdentity is not null) {
            normalFrameCaptureAttempted = true;
            if (!StartPerformanceRecording(snapshot.Players.Count, 0, environmentWarmup: true))
                GD.PushWarning("Frame capture could not start: check the fresh output directory and capture settings.");
        }
        admissionMilliseconds = worldMilliseconds = hudMilliseconds = 0;
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
        recorder.RecordFrameCost(timestamp, new(admissionMilliseconds, worldMilliseconds, hudMilliseconds,
            world?.ConsumeDrawMilliseconds() ?? 0, world?.ConsumeTerrainSubmissions() ?? 0,
            Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            renderTimeMeasurementEnabled ? RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) : null,
            renderTimeMeasurementEnabled ? RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport) : null));
    }

}
