using Devilution.Client.Diagnostics;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Export outside transport locks on both success and failure, before scene quit. */
    private void WriteCommandTimingEvidence(bool acceptancePassed)
    {
        if (Environment.GetEnvironmentVariable("GODEVILUTION_COMMAND_TIMING_TRACE") != "1")
            return;
        var phase = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")
            ?? throw new InvalidOperationException("Timing diagnostics require an explicit acceptance phase.");
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_DIRECTORY")
            ?? throw new InvalidOperationException("Timing diagnostics require an isolated evidence directory.");
        var identity = timingObservedMain?.ObservedIdentity;
        var report = timingObservedMain?.ObservedCommandTimingReport;
        ClientCommandTimingEvidenceWriter.Write(directory, new ClientCommandTimingEvidence(
            SchemaVersion: 1,
            ProcessId: Environment.ProcessId,
            Phase: phase,
            AcceptancePassed: acceptancePassed,
            CapturedUtc: DateTimeOffset.UtcNow,
            CaptureBoundary: "acceptance epilogue before scene quit; transport may still receive",
            ContentHash: identity?.ContentManifestHash,
            RulesetHash: identity?.RulesetIdentityHash,
            Report: report));
    }
}
