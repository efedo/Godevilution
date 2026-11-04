using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/**
 * One server run's command log and retained checkpoints, in a directory of its own. Every
 * method only clones and queues, so it is safe under the authority lock; serialization and disk
 * work happen on background threads. Checkpoint encoding never holds up the appender.
 */
public sealed class HostCommandLog : IDisposable
{
    public const string LogFileName = "server.cmdlog";

    /** Newest checkpoints kept for replay, so a long Debug session cannot fill the disk. */
    public const int RetainedCheckpoints = 16;

    private readonly string directory;
    private readonly CommandLogAppender appender;
    private readonly object checkpointSynchronization = new();
    private readonly object retentionSynchronization = new();
    private readonly HashSet<Task> checkpointWrites = new();
    private readonly SortedDictionary<long, string> retained = new();
    private readonly TaskCompletionSource disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Exception>? failureObserver;
    private long nextCheckpointAttempt;
    private bool stopping;

    public HostCommandLog(
        string directory,
        IEnumerable<string> serverArguments,
        uint randomSeed,
        Action<Exception>? failureObserver = null)
    {
        this.directory = Directory.CreateDirectory(directory).FullName;
        this.failureObserver = failureObserver;
        appender = new CommandLogAppender(Path.Combine(this.directory, LogFileName), failureObserver);
        appender.Append(new CommandLogRecord {
            Header = new CommandLogHeader {
                ServerArguments = { serverArguments },
                WorkingDirectory = Environment.CurrentDirectory,
                RandomSeed = randomSeed,
            },
        });
    }

    public void RecordCommand(string sessionId, Command command, ulong currentTick, CommandResult result)
        => appender.Append(new CommandLogRecord {
            ServerCommand = new ServerCommandRecord {
                SessionId = sessionId,
                CurrentTick = currentTick,
                Command = command.Clone(),
                Result = result.Clone(),
            },
        });

    public void RecordJoin(
        string sessionId,
        uint entityId,
        ulong tick,
        AuthoritativeParticipantSelection? selection,
        bool restored = false)
    {
        var joined = new ParticipantJoinedRecord {
            SessionId = sessionId,
            EntityId = entityId,
            Tick = tick,
            Restored = restored,
        };
        if (selection?.ArchetypeId is { } classId)
            joined.RequestedClassId = classId;
        if (selection?.CharacterName is { } name)
            joined.CharacterName = name;
        appender.Append(new CommandLogRecord { ParticipantJoined = joined });
    }

    public void RecordLeave(string sessionId, ulong tick)
        => appender.Append(new CommandLogRecord {
            ParticipantLeft = new ParticipantLeftRecord { SessionId = sessionId, Tick = tick },
        });

    /** Queues the marker now; checkpoint serialization and publication use their own task. */
    public void RecordCheckpoint(AuthoritativeCohortDocument document)
        => QueueCheckpoint(document.Generation, document.Tick, Task.FromResult<AuthoritativeCohortDocument?>(document),
            distinguishAttempt: false);

    /** The marker's position and attempt identity are fixed before encoding finishes. */
    internal void RecordCheckpoint(ulong generation, ulong tick, Task<AuthoritativeCohortDocument?> encoded)
        => QueueCheckpoint(generation, tick, encoded, distinguishAttempt: true);

    private void QueueCheckpoint(ulong generation, ulong tick, Task<AuthoritativeCohortDocument?> encoded,
        bool distinguishAttempt)
    {
        lock (checkpointSynchronization) {
            ObjectDisposedException.ThrowIf(stopping, this);
            var attempt = checked(++nextCheckpointAttempt);
            var fileName = distinguishAttempt
                ? $"checkpoint-{generation}-attempt-{attempt}.json" : $"checkpoint-{generation}.json";
            // No callback on the appender: even a parked encoder cannot hold up command flushes.
            appender.Append(new CommandLogRecord {
                Checkpoint = new CheckpointRecord { Generation = generation, Tick = tick, FileName = fileName },
            });
            var write = Task.Run(async () => {
                try {
                    var document = await encoded.ConfigureAwait(false);
                    if (document is null)
                        return; // A failed attempt leaves only its marker, which replay skips.
                    if (document.Generation != generation || document.Tick != tick)
                        throw new InvalidDataException("The encoded checkpoint does not match its command-log marker.");
                    WriteCheckpoint(document, fileName, attempt);
                } catch (Exception failure) {
                    try { failureObserver?.Invoke(failure); } catch { }
                }
            });
            checkpointWrites.Add(write);
            _ = write.ContinueWith(completed => {
                lock (checkpointSynchronization)
                    checkpointWrites.Remove(completed);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private void WriteCheckpoint(AuthoritativeCohortDocument document, string fileName, long attempt)
    {
        var path = Path.Combine(directory, fileName);
        var staging = path + ".pending";
        try {
            File.WriteAllBytes(staging, AuthoritativeCohortStore.SerializeDocument(document));
            // File.Exists must never admit a partially written checkpoint after a crash.
            File.Move(staging, path, overwrite: true);
        } finally {
            File.Delete(staging);
        }
        lock (retentionSynchronization) {
            retained.Add(attempt, fileName);
            // Encoding can finish out of order; keep the newest marker positions, not finishes.
            while (retained.Count > RetainedCheckpoints) {
                var oldest = retained.First();
                File.Delete(Path.Combine(directory, oldest.Value));
                retained.Remove(oldest.Key);
            }
        }
    }

    public void Dispose()
    {
        Task[]? pending = null;
        lock (checkpointSynchronization) {
            if (!stopping) {
                stopping = true;
                pending = checkpointWrites.ToArray();
            }
        }
        if (pending is null) {
            disposal.Task.GetAwaiter().GetResult();
            return;
        }
        try {
            // Flush commands first, then wait outside both locks for the independent file writes.
            try { appender.Dispose(); }
            finally { Task.WhenAll(pending).GetAwaiter().GetResult(); }
            disposal.TrySetResult();
        } catch (Exception failure) {
            disposal.TrySetException(failure);
            throw;
        }
    }
}
