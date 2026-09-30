using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Protocol;

namespace Devilution.Client.Diagnostics;

/**
 * Opt-in, bounded frame evidence that never stores gameplay or credentials.
 * The destination must be fresh and the capture is unsuccessful unless an
 * explicit caller outcome is supplied before the window is stopped.
 */
public sealed class ClientFrameEvidenceRecorder
{
    private const string EvidenceFileName = "client-frame-evidence.json";
    private readonly ClientFrameEvidenceConfiguration configuration;
    private readonly ClientFrameEvidenceMetadata metadata;
    private readonly long createdAtTimestamp;
    private readonly DateTimeOffset createdAtUtc;
    private readonly long activeAtTimestamp;
    private readonly long stopAtTimestamp;
    private readonly ClientFrameEvidenceSample[] samples;
    private readonly ClientProcessObservation startupProcess;
    private ClientFrameEvidenceServerIdentity? serverIdentity;
    private AuthoritativeServerIdentity? observedServerIdentity;
    private DateTimeOffset? measurementStartedAtUtc;
    private DateTimeOffset? stoppedAtUtc;
    private long firstMeasurementTimestamp;
    private long measurementStopTimestamp;
    private long previousFrameTimestamp;
    private int observedFrameCount;
    private int sampleCount;
    private int droppedSampleCount;
    private int nonMonotonicTimestampCount;
    private int invalidTimestampCount;
    private int? observedParticipantCount;
    private bool identityChanged;
    private bool participantCountChanged;
    private ClientFrameEvidenceQuality? quality;
    private bool stopped;
    private string outcomeKind = "not-signaled";
    private bool outcomeSucceeded;

    private ClientFrameEvidenceRecorder(
        ClientFrameEvidenceConfiguration configuration,
        ClientFrameEvidenceMetadata metadata)
    {
        this.configuration = configuration;
        this.metadata = metadata;
        createdAtTimestamp = Stopwatch.GetTimestamp();
        createdAtUtc = DateTimeOffset.UtcNow;
        activeAtTimestamp = AddSeconds(createdAtTimestamp, configuration.Warmup.TotalSeconds);
        stopAtTimestamp = AddSeconds(
            activeAtTimestamp,
            configuration.MeasurementWindow.TotalSeconds);
        samples = new ClientFrameEvidenceSample[configuration.MaximumSamples];
        startupProcess = CaptureProcessObservation();
        Directory.CreateDirectory(configuration.OutputDirectory);
    }

    public string OutputDirectory => configuration.OutputDirectory;

    public string EvidenceFilePath
        => Path.Combine(configuration.OutputDirectory, EvidenceFileName);

    public bool IsStopped => stopped;

    public bool IsActive
        => !stopped
        && Stopwatch.GetTimestamp() >= activeAtTimestamp
        && Stopwatch.GetTimestamp() < stopAtTimestamp;

    public int SampleCount => sampleCount;

    public int DroppedSampleCount => droppedSampleCount;

    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    public static ClientFrameEvidenceRecorder Create(
        ClientFrameEvidenceConfiguration configuration,
        ClientFrameEvidenceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metadata);
        configuration.Validate();
        if (Directory.Exists(configuration.OutputDirectory)
            || File.Exists(configuration.OutputDirectory)) {
            throw new IOException("The client evidence directory must be fresh.");
        }
        return new ClientFrameEvidenceRecorder(configuration, metadata);
    }

    /** Returns null when evidence is not explicitly enabled or configuration is invalid. */
    public static ClientFrameEvidenceRecorder? TryStartFromEnvironment(
        ClientFrameEvidenceMetadata metadata,
        double? warmupSecondsOverride = null)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return null;
        var configuredWarmupSeconds = 0d;
        var warmupValid = warmupSecondsOverride is null
            ? TryReadSeconds(
                "GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS",
                defaultValue: 0,
                out configuredWarmupSeconds)
            : double.IsFinite(warmupSecondsOverride.Value)
                && warmupSecondsOverride.Value >= 0;
        var warmupSeconds = warmupSecondsOverride ?? configuredWarmupSeconds;
        if (!warmupValid
            || !TryReadSeconds(
                "GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS",
                defaultValue: 60,
                out var durationSeconds)
            || !TryReadPositiveInt(
                "GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES",
                defaultValue: 4096,
                out var maximumSamples))
            return null;

        try {
            return Create(
                new ClientFrameEvidenceConfiguration(
                    outputDirectory,
                    TimeSpan.FromSeconds(warmupSeconds),
                    TimeSpan.FromSeconds(durationSeconds),
                    maximumSamples),
                metadata);
        } catch (Exception exception) when (exception is
            ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or OverflowException) {
            return null;
        }
    }

    /** Updates only server identity fields; session and character identifiers are excluded. */
    public void ObserveServerIdentity(AuthoritativeServerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (stopped)
            return;
        if (observedServerIdentity is { } previous
            && previous.BuildId == identity.BuildId
            && previous.ProtocolSchemaVersion == identity.ProtocolSchemaVersion
            && previous.ContentManifestHash == identity.ContentManifestHash
            && previous.RulesetIdentityHash == identity.RulesetIdentityHash
            && previous.TickRateHz == identity.TickRateHz
            && previous.GameMode == identity.GameMode)
            return;
        if (observedServerIdentity is not null)
            identityChanged = true;
        if (observedServerIdentity is not null)
            return;
        observedServerIdentity = identity;
        serverIdentity = new ClientFrameEvidenceServerIdentity(
            identity.BuildId,
            identity.ProtocolSchemaVersion,
            identity.ContentManifestHash,
            identity.RulesetIdentityHash,
            identity.TickRateHz,
            identity.GameMode.ToString());
    }

    /** Records the authoritative participant count without retaining a snapshot. */
    public void ObserveParticipantCount(int participantCount)
    {
        if (stopped)
            return;
        if (participantCount < 0
            || (metadata.ParticipantCount is { } expected
                && participantCount != expected))
            participantCountChanged = true;
        if (observedParticipantCount is { } previous
            && previous != participantCount)
            participantCountChanged = true;
        observedParticipantCount ??= participantCount;
    }

    /**
     * Records monotonic callback interval and separately measured scene time.
     * No allocation occurs while the bounded sample window is active.
     */
    public void RecordFrame(
        long frameTimestamp,
        long sceneStartTimestamp,
        long sceneEndTimestamp)
    {
        if (stopped)
            return;
        if (frameTimestamp < activeAtTimestamp)
            return;
        if (frameTimestamp >= stopAtTimestamp) {
            Stop("measurement-window-ended", succeeded: false);
            return;
        }

        if (observedFrameCount == 0) {
            firstMeasurementTimestamp = frameTimestamp;
            measurementStartedAtUtc = DateTimeOffset.UtcNow;
        }
        observedFrameCount++;

        double? frameIntervalMilliseconds = null;
        if (previousFrameTimestamp != 0) {
            if (frameTimestamp <= previousFrameTimestamp) {
                frameIntervalMilliseconds = 0;
                nonMonotonicTimestampCount++;
            } else {
                frameIntervalMilliseconds = ToMilliseconds(
                    frameTimestamp - previousFrameTimestamp);
            }
        }
        double sceneMilliseconds;
        if (sceneEndTimestamp < sceneStartTimestamp) {
            sceneMilliseconds = 0;
            invalidTimestampCount++;
        } else {
            sceneMilliseconds = ToMilliseconds(
                sceneEndTimestamp - sceneStartTimestamp);
        }
        previousFrameTimestamp = frameTimestamp;
        if (sampleCount >= samples.Length) {
            droppedSampleCount++;
            return;
        }
        samples[sampleCount++] = new ClientFrameEvidenceSample(
            frameIntervalMilliseconds,
            sceneMilliseconds);
    }

    /** Stops and writes evidence; success is valid only when explicitly supplied by the caller. */
    public bool Stop(string reason, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (stopped)
            return false;
        stopped = true;
        stoppedAtUtc = DateTimeOffset.UtcNow;
        measurementStopTimestamp = Stopwatch.GetTimestamp();
        var capturedQuality = EvaluateQuality(measurementStopTimestamp);
        quality = capturedQuality;
        outcomeKind = reason;
        outcomeSucceeded = succeeded && capturedQuality.IsValid;
        if (succeeded && !capturedQuality.IsValid)
            outcomeKind = "quality-failed";
        WriteEvidence();
        return true;
    }

    private ClientFrameEvidenceQuality EvaluateQuality(long stopTimestamp)
    {
        var durationMilliseconds = firstMeasurementTimestamp == 0
            ? 0
            : ToMilliseconds(
                Math.Max(0, stopTimestamp - firstMeasurementTimestamp));
        var hasSamples = sampleCount > 0;
        var identityObserved = observedServerIdentity is not null;
        var participantCountMatches = metadata.ParticipantCount is { } expected
            && observedParticipantCount is { } actual
            && expected == actual
            && !participantCountChanged;
        var noDrops = droppedSampleCount == 0;
        var monotonic = nonMonotonicTimestampCount == 0;
        var valid = hasSamples
            && identityObserved
            && !identityChanged
            && participantCountMatches
            && noDrops
            && monotonic
            && invalidTimestampCount == 0
            && durationMilliseconds > 0;
        var failureReason = valid
            ? null
            : string.Join(",", new[] {
                hasSamples ? null : "no-samples",
                identityObserved ? null : "missing-server-identity",
                identityChanged ? "server-identity-changed" : null,
                participantCountMatches ? null : "participant-count-mismatch",
                noDrops ? null : "dropped-samples",
                monotonic ? null : "non-monotonic-timestamps",
                invalidTimestampCount == 0 ? null : "invalid-timestamps",
                durationMilliseconds > 0 ? null : "zero-measured-duration",
            }.Where(reason => reason is not null));
        return new ClientFrameEvidenceQuality(
            valid,
            hasSamples,
            identityObserved,
            !identityChanged,
            participantCountMatches,
            noDrops,
            monotonic,
            invalidTimestampCount == 0,
            durationMilliseconds,
            failureReason);
    }

    private void WriteEvidence()
    {
        var finalProcess = CaptureProcessObservation();
        var document = new ClientFrameEvidenceDocument(
            SchemaVersion: "1",
            CaptureState: "stopped",
            StartedAtUtc: createdAtUtc,
            MeasurementStartedAtUtc: measurementStartedAtUtc,
            StoppedAtUtc: stoppedAtUtc,
            MeasuredDurationMilliseconds: quality?.MeasuredDurationMilliseconds ?? 0,
            ActiveWindowStartTimestamp: activeAtTimestamp,
            StoppedAtTimestamp: measurementStopTimestamp,
            ActiveWindowDurationMilliseconds: ToMilliseconds(
                Math.Max(0, measurementStopTimestamp - activeAtTimestamp)),
            WarmupMilliseconds: configuration.Warmup.TotalMilliseconds,
            MeasurementWindowMilliseconds: configuration.MeasurementWindow.TotalMilliseconds,
            Configuration: metadata,
            ServerIdentity: serverIdentity,
            StartupProcess: startupProcess,
            FinalProcess: finalProcess,
            Outcome: new ClientFrameEvidenceOutcome(outcomeKind, outcomeSucceeded),
            SampleCount: sampleCount,
            DroppedSampleCount: droppedSampleCount,
            NonMonotonicTimestampCount: nonMonotonicTimestampCount,
            InvalidTimestampCount: invalidTimestampCount,
            Quality: quality ?? EvaluateQuality(measurementStopTimestamp),
            Samples: samples[..sampleCount]);
        var options = new JsonSerializerOptions { WriteIndented = true };
        using var stream = new FileStream(
            EvidenceFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        JsonSerializer.Serialize(stream, document, options);
    }

    private static long AddSeconds(long timestamp, double seconds)
    {
        var ticks = seconds * Stopwatch.Frequency;
        if (ticks >= long.MaxValue - timestamp)
            return long.MaxValue;
        return timestamp + (long)Math.Ceiling(ticks);
    }

    private static double ToMilliseconds(long ticks)
        => ticks * 1000d / Stopwatch.Frequency;

    private static ClientProcessObservation CaptureProcessObservation()
    {
        try {
            using var process = Process.GetCurrentProcess();
            return new ClientProcessObservation(
                process.WorkingSet64,
                process.PeakWorkingSet64,
                GC.GetGCMemoryInfo().HeapSizeBytes,
                process.TotalProcessorTime.TotalMilliseconds);
        } catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException) {
            return new ClientProcessObservation(null, null, null, null);
        }
    }

    private static bool TryReadSeconds(
        string variable,
        double defaultValue,
        out double value)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw)) {
            value = defaultValue;
            return true;
        }
        return double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value)
            && double.IsFinite(value)
            && value >= 0;
    }

    private static bool TryReadPositiveInt(
        string variable,
        int defaultValue,
        out int value)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw)) {
            value = defaultValue;
            return true;
        }
        return int.TryParse(raw, out value) && value > 0;
    }
}

public sealed record ClientFrameEvidenceConfiguration(
    string OutputDirectory,
    TimeSpan Warmup,
    TimeSpan MeasurementWindow,
    int MaximumSamples)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new ArgumentException("An evidence directory is required.", nameof(OutputDirectory));
        var fullPath = Path.GetFullPath(OutputDirectory);
        if (!string.Equals(fullPath, OutputDirectory, StringComparison.Ordinal))
            throw new ArgumentException("The evidence directory must be an absolute path.", nameof(OutputDirectory));
        if (Warmup < TimeSpan.Zero
            || MeasurementWindow <= TimeSpan.Zero
            || Warmup > TimeSpan.FromHours(1)
            || MeasurementWindow > TimeSpan.FromHours(1)
            || MaximumSamples is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(MeasurementWindow));
        var parent = Directory.GetParent(OutputDirectory);
        if (parent is null || !parent.Exists)
            throw new DirectoryNotFoundException("The evidence directory parent must exist.");
    }
}

public sealed record ClientFrameEvidenceMetadata(
    string ClientBuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    string? Renderer,
    string? Device,
    string? Viewport,
    bool? VsyncEnabled,
    int? FrameCap,
    bool? Headless,
    string? EngineVersion,
    int? ParticipantCount,
    string? RenderingDriver = null,
    string? DisplayServer = null,
    string? OperatingSystem = null,
    string? OperatingSystemVersion = null,
    string? OperatingSystemFeatures = null,
    string? VsyncMode = null,
    string? Runtime = null,
    string? RuntimeOperatingSystem = null,
    string? Architecture = null,
    string? BuildConfiguration = null,
    string? WindowPosition = null,
    string? WindowMode = null,
    bool? WindowCanDraw = null,
    bool? WindowFocusedAtStart = null);

public sealed record ClientFrameEvidenceServerIdentity(
    string BuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    uint TickRateHz,
    string GameMode);

public sealed record ClientProcessObservation(
    long? WorkingSetBytes,
    long? PeakWorkingSetBytes,
    long? LastGcHeapSizeBytes,
    double? TotalProcessorTimeMilliseconds);

public sealed record ClientFrameEvidenceOutcome(string Kind, bool Succeeded);

public sealed record ClientFrameEvidenceQuality(
    bool IsValid,
    bool HasSamples,
    bool IdentityObserved,
    bool OriginalIdentityUnchanged,
    bool ParticipantCountMatches,
    bool NoDroppedSamples,
    bool MonotonicTimestamps,
    bool ValidTimestamps,
    double MeasuredDurationMilliseconds,
    string? FailureReason);

public readonly record struct ClientFrameEvidenceSample(
    double? CallbackIntervalMilliseconds,
    double ProcessCallbackMilliseconds);

public sealed record ClientFrameEvidenceDocument(
    string SchemaVersion,
    string CaptureState,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? MeasurementStartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    double MeasuredDurationMilliseconds,
    long ActiveWindowStartTimestamp,
    long StoppedAtTimestamp,
    double ActiveWindowDurationMilliseconds,
    double WarmupMilliseconds,
    double MeasurementWindowMilliseconds,
    ClientFrameEvidenceMetadata Configuration,
    ClientFrameEvidenceServerIdentity? ServerIdentity,
    ClientProcessObservation StartupProcess,
    ClientProcessObservation FinalProcess,
    ClientFrameEvidenceOutcome Outcome,
    int SampleCount,
    int DroppedSampleCount,
    int NonMonotonicTimestampCount,
    int InvalidTimestampCount,
    ClientFrameEvidenceQuality Quality,
    IReadOnlyList<ClientFrameEvidenceSample> Samples);
