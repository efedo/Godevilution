using Google.Protobuf;

namespace Devilution.Protocol.CommandLog;

/**
 * Appends records to a length-delimited log on its own thread. Append never waits on disk, so
 * simulation and network threads can call it; a full queue drops records and logs the gap.
 */
public sealed class CommandLogAppender : IDisposable
{
    public const int MaximumQueuedRecords = 1 << 18;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(50);

    private readonly object writeLock = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<(CommandLogRecord Record, Action? Before)> queue = new();
    private readonly FileStream output;
    private readonly Action<Exception>? failureObserver;
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread thread;
    private int queued;
    private long dropped;
    private bool failed;
    private volatile bool stopping;
    private bool disposed;

    public CommandLogAppender(string path, Action<Exception>? failureObserver = null)
    {
        this.failureObserver = failureObserver;
        output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        AppDomain.CurrentDomain.UnhandledException += FlushOnUnhandledException;
        thread = new Thread(Run) { IsBackground = true, Name = "Command log appender" };
        thread.Start();
    }

    /** Stamps and queues the record; before runs on the appender thread just ahead of the write. */
    public void Append(CommandLogRecord record, Action? before = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (Interlocked.Increment(ref queued) > MaximumQueuedRecords) {
            Interlocked.Decrement(ref queued);
            Interlocked.Increment(ref dropped);
            return;
        }
        record.UnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        queue.Enqueue((record, before));
    }

    /** Writes everything queued so far; used on shutdown and unhandled exceptions. */
    public void Flush()
    {
        lock (writeLock)
            Drain();
    }

    private void Run()
    {
        while (!stopping) {
            wake.WaitOne(FlushInterval);
            Flush();
        }
    }

    private void Drain()
    {
        if (failed || !output.CanWrite)
            return;
        try {
            var wrote = false;
            while (queue.TryDequeue(out var entry)) {
                Interlocked.Decrement(ref queued);
                WriteDroppedGap();
                entry.Before?.Invoke();
                entry.Record.WriteDelimitedTo(output);
                wrote = true;
            }
            WriteDroppedGap();
            if (wrote)
                output.Flush();
        } catch (Exception failure) {
            // Diagnostics must never take the game down; keep what reached the file.
            failed = true;
            try { failureObserver?.Invoke(failure); } catch { }
        }
    }

    private void WriteDroppedGap()
    {
        var count = Interlocked.Exchange(ref dropped, 0);
        if (count != 0) {
            new CommandLogRecord {
                UnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RecordsDropped = new RecordsDroppedRecord { Count = (ulong)count },
            }.WriteDelimitedTo(output);
        }
    }

    private void FlushOnUnhandledException(object sender, UnhandledExceptionEventArgs args) => Flush();

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        AppDomain.CurrentDomain.UnhandledException -= FlushOnUnhandledException;
        stopping = true;
        wake.Set();
        thread.Join();
        lock (writeLock) {
            Drain();
            output.Dispose();
        }
        wake.Dispose();
    }
}

/** Reads a log, stopping quietly at a record a crash truncated. */
public static class CommandLogReader
{
    public static IReadOnlyList<CommandLogRecord> ReadAll(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var records = new List<CommandLogRecord>();
        while (input.Position < input.Length) {
            try {
                records.Add(CommandLogRecord.Parser.ParseDelimitedFrom(input));
            } catch (InvalidProtocolBufferException) {
                break;
            }
        }
        return records;
    }
}
