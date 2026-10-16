using System.Text.Json;
using Devilution.Client.Protocol;

namespace Devilution.Client.Diagnostics;

/** Private diagnostic output only; no protocol payloads or session credentials. */
internal sealed record ClientCommandTimingEvidence(
    int SchemaVersion,
    int ProcessId,
    string Phase,
    bool AcceptancePassed,
    DateTimeOffset CapturedUtc,
    string CaptureBoundary,
    string? ContentHash,
    string? RulesetHash,
    AuthoritativeClientTimingCaptureReport? Report);

internal static class ClientCommandTimingEvidenceWriter
{
    internal static void Write(string directory, ClientCommandTimingEvidence evidence)
    {
        // Fixed phase names keep output beneath the already isolated evidence root.
        if (evidence.Phase is not ("Create" or "Resume" or "Recovery" or "Journey" or "JourneyResume"
            or "TownLoop" or "TownLoopResume" or "Lifecycle" or "LifecycleResume"
            or "MultiplayerCreate" or "MultiplayerResume" or "MultiplayerRecovery" or "MultiplayerPendingRecovery"
            or "CooperativeJourney" or "CooperativeResume"
            or "CathedralJourney" or "CathedralResume" or "CathedralDepthJourney" or "CathedralDepthResume"
            or "CathedralDoorJourney" or "CathedralDoorResume"
            or "CathedralNativeProgressionJourney" or "CathedralNativeProgressionResume"))
            throw new ArgumentException("An explicit non-performance acceptance phase is required.", nameof(evidence));
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new ArgumentException("An existing absolute evidence directory is required.", nameof(directory));
        var path = Path.Combine(directory, evidence.Phase.ToLowerInvariant() + "-client-command-timing.json");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, evidence, new JsonSerializerOptions { WriteIndented = true });
        stream.Flush(flushToDisk: true);
    }
}
