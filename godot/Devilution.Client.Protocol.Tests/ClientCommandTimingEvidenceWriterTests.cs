using System.Text.Json;
using Devilution.Client.Diagnostics;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientCommandTimingEvidenceWriterTests
{
    [Theory]
    [InlineData(true, "CathedralDoorResume")]
    [InlineData(false, "CathedralDoorResume")]
    [InlineData(true, "CathedralNativeProgressionJourney")]
    [InlineData(false, "CathedralNativeProgressionJourney")]
    [InlineData(true, "CathedralNativeProgressionResume")]
    [InlineData(false, "CathedralNativeProgressionResume")]
    public void WritesHonestOutcomeAndDetachedReport(bool passed, string phase)
    {
        using var directory = new EvidenceDirectory();
        var evidence = CreateEvidence(passed) with { Phase = phase };
        ClientCommandTimingEvidenceWriter.Write(directory.Path, evidence);
        var output = System.IO.Path.Combine(directory.Path, phase.ToLowerInvariant() + "-client-command-timing.json");
        using var json = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal(passed, json.RootElement.GetProperty("AcceptancePassed").GetBoolean());
        Assert.Equal(phase, json.RootElement.GetProperty("Phase").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("Report").GetProperty("DroppedRecordCount").GetInt64());
        Assert.Contains("before scene quit", json.RootElement.GetProperty("CaptureBoundary").GetString());
    }

    [Fact]
    public void FailedInitializationRetainsNullReportInsteadOfInventedCapture()
    {
        using var directory = new EvidenceDirectory();
        ClientCommandTimingEvidenceWriter.Write(directory.Path, CreateEvidence(false) with { Report = null });
        using var json = JsonDocument.Parse(File.ReadAllText(directory.Output));
        Assert.False(json.RootElement.GetProperty("AcceptancePassed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("Report").ValueKind);
    }

    [Fact]
    public void RefusesToOverwritePreviousEvidence()
    {
        using var directory = new EvidenceDirectory();
        ClientCommandTimingEvidenceWriter.Write(directory.Path, CreateEvidence(false));
        var original = File.ReadAllBytes(directory.Output);
        Assert.Throws<IOException>(() => ClientCommandTimingEvidenceWriter.Write(directory.Path, CreateEvidence(true)));
        Assert.Equal(original, File.ReadAllBytes(directory.Output));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("Performance")]
    [InlineData("")]
    [InlineData("cathedraldoorresume")]
    public void RefusesInvalidOrPerformancePhaseBeforeCreatingOutput(string phase)
    {
        using var directory = new EvidenceDirectory();
        Assert.Throws<ArgumentException>(() => ClientCommandTimingEvidenceWriter.Write(
            directory.Path, CreateEvidence(false) with { Phase = phase }));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public void RequiresExistingAbsoluteRoot()
    {
        using var directory = new EvidenceDirectory();
        Assert.Throws<ArgumentException>(() => ClientCommandTimingEvidenceWriter.Write("relative", CreateEvidence(false)));
        Assert.Throws<ArgumentException>(() => ClientCommandTimingEvidenceWriter.Write(
            System.IO.Path.Combine(directory.Path, "absent"), CreateEvidence(false)));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    private static ClientCommandTimingEvidence CreateEvidence(bool passed)
        => new(1, Environment.ProcessId, "CathedralDoorResume", passed, DateTimeOffset.UtcNow,
            "acceptance epilogue before scene quit; transport may still receive", "public-content", "public-ruleset",
            new AuthoritativeClientCommandTimingRecorder().CaptureReport());

    private sealed class EvidenceDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "Godevilution-Command-Timing-" + Guid.NewGuid().ToString("N"));
        public string Output => System.IO.Path.Combine(Path, "cathedraldoorresume-client-command-timing.json");

        public EvidenceDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
