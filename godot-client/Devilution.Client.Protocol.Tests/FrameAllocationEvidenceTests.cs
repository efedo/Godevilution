using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Diagnostics;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class FrameAllocationEvidenceTests
{
    [Fact]
    public void RecordsStageThreadProcessGcAndSnapshotRates()
    {
        var root = NewRoot();
        try {
            var recorder = Create(root);
            var start = Stopwatch.GetTimestamp();
            var metrics = new ClientFrameCostMetrics(1, 2, 3, 4, 0, 20, null, null,
                11, 22, 33, 44, 1, 1234, 55, 66);
            for (var i = 0; i < 60; i++) {
                var timestamp = start + i * Stopwatch.Frequency / 60;
                recorder.RecordFrameCost(timestamp, metrics);
                recorder.RecordFrame(timestamp, timestamp, timestamp);
            }
            recorder.RecordFrameCost(start + Stopwatch.Frequency, default);
            recorder.Stop("test-complete", true);
            using var document = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            var sample = document.RootElement.GetProperty("FrameCosts")[0];
            Assert.Equal(660, sample.GetProperty("SnapshotAdmissionAllocatedBytesPerSecond").GetDouble());
            Assert.Equal(1320, sample.GetProperty("WorldUpdateAllocatedBytesPerSecond").GetDouble());
            Assert.Equal(1980, sample.GetProperty("HudUpdateAllocatedBytesPerSecond").GetDouble());
            Assert.Equal(2640, sample.GetProperty("DrawAllocatedBytesPerSecond").GetDouble());
            Assert.Equal(60, sample.GetProperty("SnapshotsReceivedPerSecond").GetDouble());
            Assert.Equal(74040, sample.GetProperty("SnapshotWireBytesPerSecond").GetDouble());
            Assert.Equal(3300, sample.GetProperty("SnapshotDecodeAllocatedBytesPerSecond").GetDouble());
            Assert.Equal(3960, sample.GetProperty("SnapshotProcessingAllocatedBytesPerSecond").GetDouble());
            foreach (var name in new[] { "ManagedAllocationBytesPerSecond", "MainThreadAllocationBytesPerSecond",
                "BackgroundAllocationBytesPerSecond", "Gen0CollectionsPerSecond", "Gen1CollectionsPerSecond",
                "Gen2CollectionsPerSecond", "GcPauseMillisecondsPerSecond" })
                Assert.True(sample.GetProperty(name).GetDouble() >= 0, name);
        } finally { DeleteRoot(root); }
    }

    [Fact]
    public void MenuSamplesRemainAvailableWithoutClaimingAnAuthoritativeAdmission()
    {
        var root = NewRoot();
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(new(root, TimeSpan.Zero, TimeSpan.FromSeconds(3), 128, true),
                new("build", "schema", "content", "rules", "renderer", "device", "808x520", true, 60, true, "4.7.2", 1));
            var start = Stopwatch.GetTimestamp();
            recorder.RecordFrameCost(start, default);
            recorder.RecordFrame(start, start, start);
            recorder.RecordFrameCost(start + Stopwatch.Frequency, default);
            recorder.Stop("menu-capture", true);
            using var document = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.NotEmpty(document.RootElement.GetProperty("FrameCosts").EnumerateArray());
            Assert.False(document.RootElement.GetProperty("Quality").GetProperty("IdentityObserved").GetBoolean());
        } finally { DeleteRoot(root); }
    }

    [Fact]
    public void SteadyFrameCollectionDoesNotAllocatePerFrame()
    {
        var root = NewRoot();
        try {
            var recorder = Create(root);
            var start = Stopwatch.GetTimestamp();
            recorder.RecordFrameCost(start, default);
            recorder.RecordFrame(start, start, start);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 1; i <= 4096; i++) {
                recorder.RecordFrameCost(start + i, default);
                recorder.RecordFrame(start + i, start + i, start + i);
            }
            Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 256);
            recorder.Stop("test-complete", true);
        } finally { DeleteRoot(root); }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "Godevilution-AllocationEvidence-" + Guid.NewGuid().ToString("N"));

    private static ClientFrameEvidenceRecorder Create(string root)
    {
        var recorder = ClientFrameEvidenceRecorder.Create(new(root, TimeSpan.Zero, TimeSpan.FromSeconds(3), 128, true),
            new("build", "schema", "content", "rules", "renderer", "device", "808x520", true, 60, true, "4.7.2", 1));
        recorder.ObserveServerIdentity(new("server", "schema", "content", "rules", 20, GameMode.SinglePlayer));
        recorder.ObserveParticipantCount(1);
        return recorder;
    }

    private static void DeleteRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            || !Path.GetFileName(full).StartsWith("Godevilution-AllocationEvidence-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected allocation evidence cleanup path.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
