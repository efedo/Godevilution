using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/**
 * One server run's command log and retained checkpoints, in a directory of its own. Every
 * method only clones and queues, so it is safe under the authority lock; serialization and disk
 * work happen on the appender thread.
 */
public sealed class HostCommandLog : IDisposable
{
    public const string LogFileName = "server.cmdlog";

    /** Newest checkpoints kept for replay, so a long Debug session cannot fill the disk. */
    public const int RetainedCheckpoints = 16;

    private readonly string directory;
    private readonly CommandLogAppender appender;
    private readonly Queue<string> retained = new();

    public HostCommandLog(
        string directory,
        IEnumerable<string> serverArguments,
        uint randomSeed,
        Action<Exception>? failureObserver = null)
    {
        this.directory = Directory.CreateDirectory(directory).FullName;
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

    /** The document is immutable, so it can serialize off the lock like a timed checkpoint write. */
    public void RecordCheckpoint(AuthoritativeCohortDocument document)
    {
        var fileName = $"checkpoint-{document.Generation}.json";
        appender.Append(new CommandLogRecord {
            Checkpoint = new CheckpointRecord {
                Generation = document.Generation,
                Tick = document.Tick,
                FileName = fileName,
            },
        }, () => {
            File.WriteAllBytes(Path.Combine(directory, fileName), AuthoritativeCohortStore.SerializeDocument(document));
            retained.Enqueue(fileName);
            while (retained.Count > RetainedCheckpoints)
                File.Delete(Path.Combine(directory, retained.Dequeue()));
        });
    }

    public void Dispose() => appender.Dispose();
}
