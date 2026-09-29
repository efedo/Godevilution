using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class HostPerformanceRecorderTests
{
    [Fact]
    public void DisabledAndStoppedRecordingCannotChangeEvidence()
    {
        var recorder = new HostPerformanceRecorder();
        Assert.Equal(0, recorder.Begin());
        recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, 99);
        Assert.Throws<InvalidOperationException>(() => recorder.Stop());
        Assert.True(recorder.Start());
        Assert.False(recorder.Start());
        recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, 100);
        recorder.Complete(HostPerformanceMetric.SnapshotWriteMilliseconds, 0);
        var report = recorder.Stop();
        recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, 200);
        Assert.False(recorder.Start());
        Assert.Equal(0, recorder.Begin());
        Assert.Equal(1, report.Metrics[nameof(HostPerformanceMetric.SnapshotEnvelopePayloadBytes)].Samples);
        Assert.Equal(100d, report.Metrics[nameof(HostPerformanceMetric.SnapshotEnvelopePayloadBytes)].Mean);
        var empty = report.Metrics[nameof(HostPerformanceMetric.SnapshotWriteMilliseconds)];
        Assert.Equal(0, empty.Samples);
        Assert.Null(empty.P95);
    }

    [Fact]
    public void UsesExactNearestRankPercentilesAndExplicitTruncation()
    {
        var recorder = new HostPerformanceRecorder();
        recorder.Start();
        for (var index = 1; index <= HostPerformanceRecorder.MaximumSamplesPerMetric + 3; index++)
            recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, index);
        var metric = recorder.Stop().Metrics[nameof(HostPerformanceMetric.SnapshotEnvelopePayloadBytes)];
        Assert.Equal(20000, metric.Samples);
        Assert.Equal(3, metric.DroppedSamples);
        Assert.Equal(10000.5, metric.Mean);
        Assert.Equal(10000d, metric.P50);
        Assert.Equal(19000d, metric.P95);
        Assert.Equal(20000d, metric.Maximum);
    }

    [Fact]
    public void ConcurrentWritersRetainAllBoundedSamples()
    {
        var recorder = new HostPerformanceRecorder();
        recorder.Start();
        Parallel.For(0, 1000, _ => recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, 42));
        var metric = recorder.Stop().Metrics[nameof(HostPerformanceMetric.SnapshotEnvelopePayloadBytes)];
        Assert.Equal(1000, metric.Samples);
        Assert.Equal(0, metric.DroppedSamples);
        Assert.Equal(42d, metric.P95);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidMeasurements(double value)
    {
        var recorder = new HostPerformanceRecorder();
        recorder.Start();
        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, value));
    }
}
