using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientFrameEvidenceRecorderTests
{
    [Fact]
    public void WritesAnExplicitOutcomeAndNeverOverwritesAnExistingCapture()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            recorder.ObserveServerIdentity(CreateServerIdentity());
            recorder.ObserveParticipantCount(1);
            var timestamp = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(timestamp, timestamp, timestamp + Stopwatch.Frequency / 100);
            recorder.RecordFrame(timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10 + Stopwatch.Frequency / 100);

            Assert.True(recorder.Stop("workload-complete", succeeded: true));
            Assert.False(recorder.Stop("second-stop", succeeded: true));
            Assert.True(File.Exists(recorder.EvidenceFilePath));

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.Equal("stopped", evidence.RootElement.GetProperty("CaptureState").GetString());
            Assert.Equal("workload-complete", evidence.RootElement.GetProperty("Outcome").GetProperty("Kind").GetString());
            Assert.True(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.True(evidence.RootElement.GetProperty("Quality").GetProperty("IsValid").GetBoolean());
            Assert.DoesNotContain("session", evidence.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("character", evidence.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("command", evidence.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);

            Assert.Throws<IOException>(() => ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata()));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProcessEvidenceLabelsTheLastGcHeapRatherThanALiveAllocationEstimate()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(CreateConfiguration(outputDirectory), CreateMetadata());
            recorder.Stop("application-exit", succeeded: false);
            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            foreach (var property in new[] { "StartupProcess", "FinalProcess" }) {
                var observation = evidence.RootElement.GetProperty(property);
                Assert.InRange(observation.GetProperty("LastGcHeapSizeBytes").GetInt64(), 0, long.MaxValue);
                Assert.False(observation.TryGetProperty("ManagedHeapBytes", out _));
                Assert.InRange(observation.GetProperty("WorkingSetBytes").GetInt64(), 0, long.MaxValue);
                Assert.InRange(observation.GetProperty("TotalProcessorTimeMilliseconds").GetDouble(), 0, double.MaxValue);
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void KeepsFrameIntervalsSeparateFromSceneDurationAndBoundsSamples()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory, maximumSamples: 2),
                CreateMetadata());
            var first = ClientFrameEvidenceRecorder.GetTimestamp();
            var frequency = Stopwatch.Frequency;

            recorder.RecordFrame(first, first, first + frequency / 100);
            recorder.RecordFrame(first + frequency / 10, first + frequency / 10,
                first + frequency / 10 + frequency / 50);
            recorder.RecordFrame(first + frequency / 5, first + frequency / 5,
                first + frequency / 5 + frequency / 50);
            recorder.Stop("workload-failed", succeeded: false);

            Assert.Equal(2, recorder.SampleCount);
            Assert.Equal(1, recorder.DroppedSampleCount);
            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            var samples = evidence.RootElement.GetProperty("Samples").EnumerateArray().ToArray();
            Assert.Equal(2, samples.Length);
            Assert.Equal(JsonValueKind.Null, samples[0].GetProperty("CallbackIntervalMilliseconds").ValueKind);
            Assert.InRange(samples[0].GetProperty("ProcessCallbackMilliseconds").GetDouble(), 9, 11);
            Assert.InRange(samples[1].GetProperty("CallbackIntervalMilliseconds").GetDouble(), 99, 101);
            Assert.InRange(samples[1].GetProperty("ProcessCallbackMilliseconds").GetDouble(), 19, 21);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapturesServerIdentityWithoutSessionOrCharacterState()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            recorder.ObserveServerIdentity(new AuthoritativeServerIdentity(
                "server-build", "schema", "content", "rules", 20, GameMode.Multiplayer));
            recorder.Stop("application-exit", succeeded: false);

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            var identity = evidence.RootElement.GetProperty("ServerIdentity");
            Assert.Equal("server-build", identity.GetProperty("BuildId").GetString());
            Assert.Equal(20u, identity.GetProperty("TickRateHz").GetUInt32());
            Assert.Equal("Multiplayer", identity.GetProperty("GameMode").GetString());
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsRelativeOrUnboundedConfigurationBeforeCreatingOutput()
    {
        var relative = new ClientFrameEvidenceConfiguration(
            "relative-capture", TimeSpan.Zero, TimeSpan.FromSeconds(1), 1);
        Assert.Throws<ArgumentException>(() => relative.Validate());

        var root = CreateTestRoot(out var outputDirectory);
        try {
            var unbounded = CreateConfiguration(outputDirectory, maximumSamples: 4097);
            Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.Validate());
            Assert.False(Directory.Exists(outputDirectory));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DowngradesARequestedSuccessWhenEvidenceQualityIsIncomplete()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            Assert.True(recorder.Stop("workload-complete", succeeded: true));

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.Equal("quality-failed", evidence.RootElement.GetProperty("Outcome").GetProperty("Kind").GetString());
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.Contains("no-samples", evidence.RootElement.GetProperty("Quality").GetProperty("FailureReason").GetString());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DuplicateOrBackwardCallbackTimestampsInvalidateRequestedSuccess(int difference)
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(CreateConfiguration(outputDirectory), CreateMetadata());
            recorder.ObserveServerIdentity(CreateServerIdentity());
            recorder.ObserveParticipantCount(1);
            var first = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(first, first, first);
            recorder.RecordFrame(first + difference, first + difference, first + difference);
            recorder.Stop("workload-complete", succeeded: true);
            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.Equal(1, evidence.RootElement.GetProperty("NonMonotonicTimestampCount").GetInt32());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidSceneTimestampsCannotProduceAValidCapture()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            recorder.ObserveServerIdentity(CreateServerIdentity());
            recorder.ObserveParticipantCount(1);
            var timestamp = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(timestamp, timestamp + 10, timestamp);
            recorder.RecordFrame(timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10 + Stopwatch.Frequency / 100);
            recorder.Stop("workload-complete", succeeded: true);

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.Equal(1, evidence.RootElement.GetProperty("InvalidTimestampCount").GetInt32());
            Assert.False(evidence.RootElement.GetProperty("Quality").GetProperty("ValidTimestamps").GetBoolean());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IdentityChangesCannotProduceAValidCapture()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            recorder.ObserveServerIdentity(CreateServerIdentity());
            recorder.ObserveParticipantCount(1);
            var timestamp = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(timestamp, timestamp, timestamp + Stopwatch.Frequency / 100);
            recorder.ObserveServerIdentity(CreateServerIdentity() with { BuildId = "changed" });
            recorder.RecordFrame(timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10 + Stopwatch.Frequency / 100);
            recorder.Stop("workload-complete", succeeded: true);

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.False(evidence.RootElement.GetProperty("Quality").GetProperty("OriginalIdentityUnchanged").GetBoolean());
            Assert.Contains("server-identity-changed", evidence.RootElement.GetProperty("Quality").GetProperty("FailureReason").GetString());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParticipantCountChangesCannotProduceAValidCapture()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            recorder.ObserveServerIdentity(CreateServerIdentity());
            recorder.ObserveParticipantCount(1);
            var timestamp = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(timestamp, timestamp, timestamp + Stopwatch.Frequency / 100);
            recorder.ObserveParticipantCount(2);
            recorder.RecordFrame(timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10,
                timestamp + Stopwatch.Frequency / 10 + Stopwatch.Frequency / 100);
            recorder.Stop("workload-complete", succeeded: true);

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.False(evidence.RootElement.GetProperty("Quality").GetProperty("ParticipantCountMatches").GetBoolean());
            Assert.Contains("participant-count-mismatch", evidence.RootElement.GetProperty("Quality").GetProperty("FailureReason").GetString());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DisabledEnvironmentDoesNotCreateRecorderOrStorage()
    {
        var previousOutput = Environment.GetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT");
        try {
            Environment.SetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT", null);
            Assert.Null(ClientFrameEvidenceRecorder.TryStartFromEnvironment(CreateMetadata()));
        } finally {
            Environment.SetEnvironmentVariable("GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT", previousOutput);
        }
    }

    [Fact]
    public void ExistingEvidenceFileCausesFailClosedWriteFailure()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                CreateConfiguration(outputDirectory),
                CreateMetadata());
            var evidencePath = recorder.EvidenceFilePath;
            File.WriteAllText(evidencePath, "preserve");
            Assert.Throws<IOException>(() => recorder.Stop("workload-failed", succeeded: false));
            Assert.Equal("preserve", File.ReadAllText(evidencePath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WarmupDoesNotSampleAndWindowExpiryIsUnsuccessful()
    {
        var root = CreateTestRoot(out var outputDirectory);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(
                new ClientFrameEvidenceConfiguration(
                    outputDirectory,
                    TimeSpan.FromMilliseconds(25),
                    TimeSpan.FromMilliseconds(10),
                    4096),
                CreateMetadata());
            var beforeWarmup = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(beforeWarmup, beforeWarmup, beforeWarmup);
            Assert.Equal(0, recorder.SampleCount);
            Thread.Sleep(75);
            var expired = ClientFrameEvidenceRecorder.GetTimestamp();
            recorder.RecordFrame(expired, expired, expired);

            using var evidence = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.Equal("measurement-window-ended", evidence.RootElement.GetProperty("Outcome").GetProperty("Kind").GetString());
            Assert.False(evidence.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ClientFrameEvidenceConfiguration CreateConfiguration(
        string outputDirectory,
        int maximumSamples = 128)
        => new(
            outputDirectory,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            maximumSamples);

    private static ClientFrameEvidenceMetadata CreateMetadata()
        => new(
            "client-build", "schema", "content-hash", "rules-hash",
            "renderer", "device", "960x600",
            VsyncEnabled: false, FrameCap: 60, Headless: true,
            EngineVersion: "4.7.1", ParticipantCount: 1);

    private static AuthoritativeServerIdentity CreateServerIdentity()
        => new("server-build", "schema", "content-hash", "rules-hash", 20, GameMode.SinglePlayer);

    private static string CreateTestRoot(out string outputDirectory)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Client-Frame-Evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        outputDirectory = Path.Combine(root, "capture");
        return root;
    }
}
