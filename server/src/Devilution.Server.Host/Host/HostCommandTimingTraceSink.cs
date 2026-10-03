using System.Threading.Channels;
using System.Text.Json;

namespace Devilution.Server.Host;

/**
 * Asynchronous bounded stdout publisher for command-timing drains. CaptureComplete
 * describes recorder/sink delivery and accounting only; it is not evidence that
 * every operation was accepted or that every projection reached the client.
 * The writer is injected so hosts can keep Console I/O off request paths and
 * tests can exercise saturation and output failures without touching console.
 */
public sealed class HostCommandTimingTraceSink
{
    public const string Prefix = "COMMAND_TIMING_JSON: ";
    public const int QueueCapacity = 64;
    public const int MaximumOutputLines = HostCommandTimingRecorder.DefaultCapacity;

    private const int MaximumDataLines = MaximumOutputLines - 1;
    private readonly Channel<HostCommandTimingDrain> pending =
        Channel.CreateBounded<HostCommandTimingDrain>(new BoundedChannelOptions(QueueCapacity) {
            // Wait mode makes TryWrite return false when full, allowing an
            // explicit sink-drop diagnostic rather than silent loss.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    private readonly Action<string> writeLine;
    private long droppedDrains;
    private long droppedLines;
    private long outputLines;
    private long outputFailures;
    private long recorderDroppedCount;
    private long recorderFailureCount;
    private bool recorderIncomplete;
    private bool capMarkerWritten;

    public HostCommandTimingTraceSink(Action<string> writeLine)
    {
        this.writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
    }

    public long DroppedDrainCount => Volatile.Read(ref droppedDrains);

    public long DroppedLineCount => Volatile.Read(ref droppedLines);

    public long OutputFailureCount => Volatile.Read(ref outputFailures);

    public void Enqueue(HostCommandTimingDrain drain)
    {
        ArgumentNullException.ThrowIfNull(drain);
        if (!pending.Writer.TryWrite(drain))
            Interlocked.Increment(ref droppedDrains);
    }

    /** Starts on the thread pool so prequeued output cannot block the caller. */
    public Task RunAsync() => Task.Run(RunCoreAsync);

    private async Task RunCoreAsync()
    {
        await foreach (var drain in pending.Reader.ReadAllAsync()) {
            recorderFailureCount = Math.Max(recorderFailureCount, drain.FailureCount);
            if (drain.DroppedCount != 0 || drain.CaptureFaulted || drain.FailureCount != 0) {
                recorderIncomplete = true;
                recorderDroppedCount = checked(recorderDroppedCount + drain.DroppedCount);
            }

            foreach (var record in drain.Records) {
                if (!TryWriteData(new { SchemaVersion = 1, Record = record }))
                    WriteCapMarker();
            }
            if (drain.DroppedCount != 0 || drain.CaptureFaulted || drain.FailureCount != 0)
                if (!TryWriteData(new {
                    SchemaVersion = 1,
                    Kind = "diagnostic",
                    DroppedCount = drain.DroppedCount,
                    CaptureFaulted = drain.CaptureFaulted,
                    FailureCount = drain.FailureCount,
                }))
                    WriteCapMarker();
        }

        if (!capMarkerWritten)
            TryWriteTerminal(new {
                SchemaVersion = 1,
                Kind = "sink-complete",
                PublisherComplete = true,
                CaptureComplete = !recorderIncomplete
                    && recorderFailureCount == 0
                    && DroppedDrainCount == 0
                    && DroppedLineCount == 0
                    && OutputFailureCount == 0,
                DroppedDrains = DroppedDrainCount,
                DroppedLines = DroppedLineCount,
                RecorderDroppedCount = recorderDroppedCount,
                RecorderFailureCount = recorderFailureCount,
                OutputFailures = OutputFailureCount,
            });
    }

    public void Complete() => pending.Writer.TryComplete();

    private bool TryWriteData<T>(T value)
    {
        if (outputLines >= MaximumDataLines) {
            Interlocked.Increment(ref droppedLines);
            return false;
        }
        Write(value);
        return true;
    }

    private void WriteCapMarker()
    {
        if (capMarkerWritten)
            return;
        capMarkerWritten = true;
        Write(new {
            SchemaVersion = 1,
            Kind = "sink-cap",
            PublisherComplete = false,
            CaptureComplete = false,
            OutputCapped = true,
            DroppedLines = DroppedLineCount,
            DroppedLinesIsLowerBound = true,
            DroppedDrains = DroppedDrainCount,
            DroppedDrainsIsLowerBound = true,
        });
    }

    private void TryWriteTerminal<T>(T value)
    {
        if (outputLines >= MaximumOutputLines)
            return;
        Write(value);
    }

    private void Write<T>(T value)
    {
        try {
            outputLines++;
            writeLine(Prefix + JsonSerializer.Serialize(value));
        } catch {
            Interlocked.Increment(ref outputFailures);
        }
    }
}
