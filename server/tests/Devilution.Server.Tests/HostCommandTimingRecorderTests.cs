using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class HostCommandTimingRecorderTests
{
    [Fact]
    public void DefaultCapacityIsBoundedAndReportsDrops()
    {
        var recorder = new HostCommandTimingRecorder(2);
        recorder.Record(new HostCommandTimingRecord { Kind = "one", TraceOrdinal = 1 });
        recorder.Record(new HostCommandTimingRecord { Kind = "two", TraceOrdinal = 2 });
        recorder.Record(new HostCommandTimingRecord { Kind = "three", TraceOrdinal = 3 });

        var drain = recorder.Drain();

        Assert.Equal(2, drain.Records.Count);
        Assert.Equal(1, drain.DroppedCount);
        Assert.False(drain.CaptureFaulted);
        Assert.Equal(1, recorder.DroppedCount);
        Assert.Empty(recorder.Drain().Records);
    }

    [Fact]
    public void DrainReturnsDetachedImmutableRecordsAndFailureState()
    {
        var recorder = new HostCommandTimingRecorder();
        recorder.Record(new HostCommandTimingRecord {
            Kind = "command",
            EntityId = 42,
            ClientSequence = 7,
            ActualAdmissionTick = 120,
            AckStatus = "Rejected",
            AckReason = "TooLate",
        });

        var drain = recorder.Drain();
        var record = Assert.Single(drain.Records);

        Assert.Equal("command", record.Kind);
        Assert.Equal(42U, record.EntityId);
        Assert.Equal(7UL, record.ClientSequence);
        Assert.Equal(120UL, record.ActualAdmissionTick);
        Assert.Equal("Rejected", record.AckStatus);
        Assert.Equal("TooLate", record.AckReason);
        Assert.Equal(0, recorder.FailureCount);

        recorder.MarkObserverFailure();
        var failureDrain = recorder.Drain();
        Assert.True(failureDrain.CaptureFaulted);
        Assert.Equal(1, failureDrain.FailureCount);
    }

    [Fact]
    public void ConcurrentRecordersNeverExceedCapacity()
    {
        var recorder = new HostCommandTimingRecorder(64);
        Parallel.For(0, 1000, index => recorder.Record(new HostCommandTimingRecord {
            Kind = "command",
            TraceOrdinal = index,
        }));

        var drain = recorder.Drain();

        Assert.Equal(64, drain.Records.Count);
        Assert.Equal(936, drain.DroppedCount);
    }
}
