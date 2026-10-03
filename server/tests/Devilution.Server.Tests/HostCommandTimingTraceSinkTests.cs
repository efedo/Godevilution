using System.Collections.Concurrent;
using System.Text.Json;
using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class HostCommandTimingTraceSinkTests
{
    [Fact]
    public async Task OutputLineCapIncludesCapMarkerAndLabelsEarlyLowerBound()
    {
        var lines = new ConcurrentQueue<string>();
        var sink = new HostCommandTimingTraceSink(lines.Enqueue);
        var records = Enumerable.Range(0, HostCommandTimingTraceSink.MaximumOutputLines + 10)
            .Select(index => new HostCommandTimingRecord {
                Kind = "command",
                TraceOrdinal = index,
            })
            .ToArray();

        sink.Enqueue(new HostCommandTimingDrain(records, 0));
        var publisher = sink.RunAsync();
        sink.Complete();
        await publisher.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(HostCommandTimingTraceSink.MaximumOutputLines, lines.Count);
        Assert.Equal(11, sink.DroppedLineCount);
        var cap = ParseLines(lines).Single(IsKind("sink-cap"));
        Assert.False(cap.RootElement.GetProperty("PublisherComplete").GetBoolean());
        Assert.False(cap.RootElement.GetProperty("CaptureComplete").GetBoolean());
        Assert.True(cap.RootElement.GetProperty("DroppedLinesIsLowerBound").GetBoolean());
        Assert.True(cap.RootElement.GetProperty("DroppedDrainsIsLowerBound").GetBoolean());
    }

    [Fact]
    public async Task ChannelOverflowIsCountedAndMarksCaptureIncomplete()
    {
        var lines = new ConcurrentQueue<string>();
        var sink = new HostCommandTimingTraceSink(lines.Enqueue);
        for (var index = 0; index < HostCommandTimingTraceSink.QueueCapacity + 20; index++)
            sink.Enqueue(new HostCommandTimingDrain([
                new HostCommandTimingRecord { Kind = "command", TraceOrdinal = index },
            ], 0));

        var publisher = sink.RunAsync();
        sink.Complete();
        await publisher.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(20, sink.DroppedDrainCount);
        var terminal = ParseLines(lines).Single(IsKind("sink-complete"));
        Assert.False(terminal.RootElement.GetProperty("CaptureComplete").GetBoolean());
        Assert.Equal(20, terminal.RootElement.GetProperty("DroppedDrains").GetInt64());
    }

    [Fact]
    public async Task RecorderDropsAndFailuresRemainIncompleteAtSinkCompletion()
    {
        var lines = new ConcurrentQueue<string>();
        var sink = new HostCommandTimingTraceSink(lines.Enqueue);
        sink.Enqueue(new HostCommandTimingDrain([], 4, true, 3));

        var publisher = sink.RunAsync();
        sink.Complete();
        await publisher.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var terminal = ParseLines(lines).Single(IsKind("sink-complete"));
        Assert.False(terminal.RootElement.GetProperty("CaptureComplete").GetBoolean());
        Assert.Equal(4, terminal.RootElement.GetProperty("RecorderDroppedCount").GetInt64());
        Assert.Equal(3, terminal.RootElement.GetProperty("RecorderFailureCount").GetInt64());
    }

    [Fact]
    public async Task CumulativeRecorderFailureWithoutFaultDeltaRemainsIncomplete()
    {
        var lines = new ConcurrentQueue<string>();
        var sink = new HostCommandTimingTraceSink(lines.Enqueue);
        sink.Enqueue(new HostCommandTimingDrain([], 0, false, 2));

        var publisher = sink.RunAsync();
        sink.Complete();
        await publisher.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var terminal = ParseLines(lines).Single(IsKind("sink-complete"));
        Assert.False(terminal.RootElement.GetProperty("CaptureComplete").GetBoolean());
        Assert.Equal(2, terminal.RootElement.GetProperty("RecorderFailureCount").GetInt64());
        Assert.Contains(ParseLines(lines), document => IsKind("diagnostic")(document));
    }

    [Fact]
    public async Task ThrowingWriterCannotMaskPublisherCompletion()
    {
        var sink = new HostCommandTimingTraceSink(_ => throw new InvalidOperationException("diagnostic sink"));
        sink.Enqueue(new HostCommandTimingDrain([
            new HostCommandTimingRecord { Kind = "command", TraceOrdinal = 1 },
        ], 0));

        var publisher = sink.RunAsync();
        sink.Complete();
        await publisher.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(sink.OutputFailureCount > 0);
    }

    [Fact]
    public async Task PrequeuedBlockedWriterDoesNotBlockPublisherStart()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new HostCommandTimingTraceSink(_ => {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        });
        sink.Enqueue(new HostCommandTimingDrain([
            new HostCommandTimingRecord { Kind = "command", TraceOrdinal = 1 },
        ], 0));

        var publisher = sink.RunAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.False(publisher.IsCompleted);
            sink.Complete();
            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await publisher.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
        } finally {
            release.TrySetResult();
            sink.Complete();
            // Cleanup must finish even when the test's own cancellation fired.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await publisher.WaitAsync(cleanup.Token);
        }
    }

    private static IEnumerable<JsonDocument> ParseLines(IEnumerable<string> lines)
        => lines.Select(line => JsonDocument.Parse(
            line[HostCommandTimingTraceSink.Prefix.Length..]));

    private static Func<JsonDocument, bool> IsKind(string expected)
        => document => document.RootElement.TryGetProperty("Kind", out var kind)
            && kind.GetString() == expected;
}
