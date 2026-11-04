using Devilution.Protocol.CommandLog;
using Devilution.Server.Commands;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

public sealed record AuthoritativeCommandLogReplayResult(
    bool Matches,
    ulong FromGeneration,
    ulong ToGeneration,
    int CommandsApplied,
    string Message);

public sealed record AuthoritativeCommandLogHeader(
    IReadOnlyList<string> ServerArguments,
    string WorkingDirectory,
    uint RandomSeed);

public sealed record AuthoritativeCommandLogComparison(
    bool Matches,
    string Summary,
    IReadOnlyList<string> Mismatches);

/**
 * Loads a retained checkpoint into a fresh host, applies the commands logged after it through the
 * ordinary command server, and compares the result with a later checkpoint byte for byte.
 */
public static class AuthoritativeCommandLogReplay
{
    public static AuthoritativeCommandLogHeader ReadHeader(string directory)
    {
        var header = CommandLogReader.ReadAll(Path.Combine(directory, HostCommandLog.LogFileName))
            .FirstOrDefault(record => record.KindCase == CommandLogRecord.KindOneofCase.Header)?.Header
            ?? throw new InvalidDataException("The command log has no header.");
        return new AuthoritativeCommandLogHeader(header.ServerArguments.ToArray(), header.WorkingDirectory, header.RandomSeed);
    }

    /** Compares a client log with the server runs it played against, whose directories are given. */
    public static AuthoritativeCommandLogComparison Compare(string clientLog, IEnumerable<string> serverRuns)
    {
        var comparison = CommandLogComparer.Compare(
            CommandLogReader.ReadAll(clientLog),
            serverRuns.Select(run => CommandLogReader.ReadAll(Path.Combine(run, HostCommandLog.LogFileName))).ToArray());
        return new AuthoritativeCommandLogComparison(
            comparison.Matches,
            $"Command logs {(comparison.Matches ? "match" : "differ")}: "
                + $"{comparison.MatchedCommands} commands matched, {comparison.UnconfirmedSends} sends unconfirmed.",
            comparison.Mismatches);
    }

    /**
     * Null generations mean the earliest retained checkpoint with a participant and the last
     * logged one. A checkpoint without participants only ever starts a fresh host, not a restore.
     */
    public static AuthoritativeCommandLogReplayResult Run(
        string directory,
        ulong? fromGeneration,
        ulong? toGeneration,
        IAuthoritativeGameCohortAdapter adapter,
        IAuthoritativeParticipantInitializer participants,
        AuthoritativeCommandServer commandServer)
    {
        var records = CommandLogReader.ReadAll(Path.Combine(directory, HostCommandLog.LogFileName));
        if (records.Any(record => record.KindCase == CommandLogRecord.KindOneofCase.RecordsDropped))
            throw new InvalidDataException("The log dropped records, so it cannot be replayed.");
        var markers = records.Select((record, index) => (record, index))
            .Where(entry => entry.record.KindCase == CommandLogRecord.KindOneofCase.Checkpoint
                && File.Exists(Path.Combine(directory, entry.record.Checkpoint.FileName)))
            .ToArray();
        if (markers.Length < 2)
            throw new InvalidDataException("A replay needs at least two retained checkpoints.");
        var from = fromGeneration is { } requestedFrom
            ? markers.Last(entry => entry.record.Checkpoint.Generation == requestedFrom)
            : markers.First(entry => ReadDocument(directory, entry.record).Participants.Count != 0);
        var to = toGeneration is { } requestedTo
            ? markers.Last(entry => entry.record.Checkpoint.Generation == requestedTo)
            : markers[^1];
        if (to.index <= from.index)
            throw new InvalidDataException("A replay must end at a later checkpoint than it starts from.");

        var start = ReadDocument(directory, from.record);
        if (start.Participants.Count == 0)
            throw new InvalidDataException($"Checkpoint {start.Generation} has no participants to restore.");
        var expectedBytes = File.ReadAllBytes(Path.Combine(directory, to.record.Checkpoint.FileName));
        var expected = AuthoritativeCohortStore.DeserializeDocument(expectedBytes);
        Restore(start, adapter, commandServer);

        var applied = 0;
        AuthoritativeCommandLogReplayResult Result(string message, bool matches = false) => new(matches, start.Generation, expected.Generation, applied, message);
        for (var index = from.index + 1; index < to.index; index++) {
            var record = records[index];
            if (record.KindCase == CommandLogRecord.KindOneofCase.ServerCommand) {
                var logged = record.ServerCommand;
                var result = commandServer.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick);
                if (!result.Equals(logged.Result))
                    return Result($"Sequence {logged.Command.ClientSequence} at tick {logged.CurrentTick} "
                        + $"replayed as {result}; the log has {logged.Result}.");
                applied++;
            } else if (record.KindCase == CommandLogRecord.KindOneofCase.ParticipantJoined
                && !record.ParticipantJoined.Restored) {
                var joined = record.ParticipantJoined;
                commandServer.AdvanceTo(joined.Tick);
                participants.InitializeParticipant(joined.SessionId,
                    new AuthoritativeParticipantSelection(joined.HasRequestedClassId ? joined.RequestedClassId : null) {
                        CharacterName = joined.HasCharacterName ? joined.CharacterName : null,
                        EntityId = joined.EntityId,
                    });
            }
        }

        commandServer.AdvanceTo(expected.Tick);
        var document = AuthoritativeTcpServer.ComposeCohortDocument(
            expected.Generation,
            expected.Identity,
            adapter.CaptureCohortState(),
            commandServer.CaptureCohortState(),
            expected.NextEntityId,
            expected.Participants.Select(participant =>
                new AuthoritativeModuleCohortParticipant(participant.SessionId, participant.EntityId)).ToArray());
        var actualBytes = AuthoritativeCohortStore.SerializeDocument(document);
        if (actualBytes.AsSpan().SequenceEqual(expectedBytes))
            return Result($"Replayed {applied} commands to the same {actualBytes.Length} checkpoint bytes.", matches: true);

        var replayedPath = Path.Combine(directory, $"replayed-{expected.Generation}.json");
        File.WriteAllBytes(replayedPath, actualBytes);
        return Result($"The replayed checkpoint differs ({Difference(document, expected)}); wrote {replayedPath}.");
    }

    private static AuthoritativeCohortDocument ReadDocument(string directory, CommandLogRecord marker)
        => AuthoritativeCohortStore.DeserializeDocument(
            File.ReadAllBytes(Path.Combine(directory, marker.Checkpoint.FileName)));

    private static void Restore(
        AuthoritativeCohortDocument document,
        IAuthoritativeGameCohortAdapter adapter,
        AuthoritativeCommandServer commandServer)
    {
        using var prepared = adapter.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId,
            document.Identity.ModuleFormatVersion,
            document.Tick,
            Array.AsReadOnly(document.Participants.Select(participant =>
                new AuthoritativeModuleCohortParticipant(participant.SessionId, participant.EntityId)).ToArray()),
            document.SerializedModuleState));
        commandServer.InitializeCohortState(new AuthoritativeCommandCohortState(
            document.Tick,
            document.NextServerReceiptSequence,
            document.Participants.Select(participant =>
                new AuthoritativeSessionCommandLedger(participant.SessionId, participant.CommandReceipts) {
                    RetiredThroughClientSequence = participant.RetiredThroughClientSequence,
                }).ToArray()));
        adapter.CommitCohortRestore(prepared);
    }

    private static string Difference(AuthoritativeCohortDocument actual, AuthoritativeCohortDocument expected)
    {
        if (actual.Tick != expected.Tick)
            return $"tick {actual.Tick} against {expected.Tick}";
        if (actual.NextServerReceiptSequence != expected.NextServerReceiptSequence)
            return $"next receipt {actual.NextServerReceiptSequence} against {expected.NextServerReceiptSequence}";
        if (actual.SerializedModuleState == expected.SerializedModuleState)
            return "command receipts";
        var length = Math.Min(actual.SerializedModuleState.Length, expected.SerializedModuleState.Length);
        var first = 0;
        while (first < length && actual.SerializedModuleState[first] == expected.SerializedModuleState[first])
            first++;
        return $"module state at character {first} of {expected.SerializedModuleState.Length}";
    }
}
