using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CommandLogTests : IDisposable
{
    private static readonly string FirstSession = new('a', 64);
    private static readonly string SecondSession = new('b', 64);
    private static readonly AuthoritativeCohortIdentity Identity = new(
        "replay-test", 1, "0.2.0", new string('c', 64), new string('d', 64), 20, AuthoritativeGameMode.SinglePlayer);

    private readonly string root = Directory.CreateTempSubdirectory("godevilution-command-log-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static CommandLogRecord Queued(ulong sequence) => new() {
        ClientQueued = new ClientQueuedRecord { Command = Move(sequence, 0) },
    };

    private static Command Move(ulong sequence, ulong requestedTick, int direction = 1) => new() {
        ClientSequence = sequence,
        RequestedTick = requestedTick,
        MoveRequested = new MoveRequested { DirectionX = direction },
    };

    private static CommandResult Accepted(ulong sequence, ulong receipt, ulong tick) => new() {
        ClientSequence = sequence,
        ServerReceiptSequence = receipt,
        AppliedTick = tick,
        Status = CommandStatus.Accepted,
    };

    [Fact]
    public void AppenderWritesInOrderAndRunsWorkBeforeItsRecord()
    {
        var path = Path.Combine(root, "order.cmdlog");
        var workRan = false;
        using (var appender = new CommandLogAppender(path)) {
            appender.Append(Queued(1));
            appender.Append(Queued(2), () => workRan = true);
            appender.Append(Queued(3));
        }

        Assert.True(workRan);
        var records = CommandLogReader.ReadAll(path);
        Assert.Equal([1UL, 2UL, 3UL], records.Select(record => record.ClientQueued.Command.ClientSequence));
        Assert.All(records, record => Assert.True(record.UnixMilliseconds > 0));
    }

    [Fact]
    public async Task AppenderMakesRecordsReadableWithoutBeingClosed()
    {
        var path = Path.Combine(root, "flush.cmdlog");
        using var appender = new CommandLogAppender(path);
        appender.Append(Queued(1));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (CommandLogReader.ReadAll(path).Count == 0)
            await Task.Delay(10, timeout.Token);
    }

    [Fact]
    public void ReaderStopsAtATruncatedLastRecord()
    {
        var path = Path.Combine(root, "truncated.cmdlog");
        using (var appender = new CommandLogAppender(path)) {
            appender.Append(Queued(1));
            appender.Append(Queued(2));
        }
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..^3]);

        Assert.Single(CommandLogReader.ReadAll(path));
    }

    [Fact]
    public void AppenderFailureIsReportedAndNeverReachesTheCaller()
    {
        var path = Path.Combine(root, "failure.cmdlog");
        Exception? reported = null;
        using (var appender = new CommandLogAppender(path, failure => reported = failure)) {
            appender.Append(Queued(1), () => throw new IOException("disk full"));
            appender.Append(Queued(2));
            appender.Flush();
        }

        Assert.IsType<IOException>(reported);
        Assert.Empty(CommandLogReader.ReadAll(path));
    }

    private static IReadOnlyList<CommandLogRecord> Server(string session, params (Command Command, CommandResult Result)[] commands)
        => commands.Select(entry => new CommandLogRecord {
            ServerCommand = new ServerCommandRecord {
                SessionId = session,
                CurrentTick = entry.Result.AppliedTick,
                Command = entry.Command,
                Result = entry.Result,
            },
        }).ToArray();

    private static CommandLogRecord Sent(Command command) => new() { ClientSent = new ClientSentRecord { Command = command } };

    private static CommandLogRecord Receipt(CommandResult result) => new() { ClientReceipt = new ClientReceiptRecord { Result = result } };

    [Fact]
    public void ComparerMatchesAcrossRetransmissionsDuplicatesAndHosts()
    {
        var firstAttempt = Move(1, 10);
        var secondAttempt = Move(1, 12);
        var duplicate = Accepted(1, 5, 12);
        duplicate.Status = CommandStatus.Duplicate;
        var client = new[] {
            Sent(firstAttempt), Sent(secondAttempt), Receipt(duplicate),
            Sent(Move(2, 20)), Receipt(Accepted(2, 1, 20)),
            Sent(Move(3, 30)),
        };

        var comparison = CommandLogComparer.Compare(client, [
            Server(FirstSession, (secondAttempt, Accepted(1, 5, 12))),
            Server(FirstSession, (Move(2, 20), Accepted(2, 1, 20))),
        ]);

        Assert.True(comparison.Matches, string.Join("; ", comparison.Mismatches));
        Assert.Equal(2, comparison.MatchedCommands);
        Assert.Equal(1, comparison.UnconfirmedSends);
    }

    [Fact]
    public void ComparerIgnoresOtherParticipantsOnTheSameHost()
    {
        var comparison = CommandLogComparer.Compare(
            [Sent(Move(1, 10)), Receipt(Accepted(1, 2, 10))],
            [[
                .. Server(SecondSession, (Move(1, 9, direction: -1), Accepted(1, 1, 9))),
                .. Server(FirstSession, (Move(1, 10), Accepted(1, 2, 10))),
            ]]);

        Assert.True(comparison.Matches, string.Join("; ", comparison.Mismatches));
        Assert.Equal(1, comparison.MatchedCommands);
    }

    [Fact]
    public void ComparerReportsDifferentPayloadsResultsAndMissingRecords()
    {
        var rejected = Accepted(2, 2, 20);
        rejected.Status = CommandStatus.Rejected;
        rejected.RejectReason = CommandRejectReason.NotAllowed;
        var comparison = CommandLogComparer.Compare(
            [
                Sent(Move(1, 10)), Receipt(Accepted(1, 1, 10)),
                Sent(Move(2, 20)), Receipt(Accepted(2, 2, 20)),
                Sent(Move(3, 30)), Receipt(Accepted(3, 9, 30)),
            ],
            [Server(FirstSession,
                (Move(1, 10, direction: -1), Accepted(1, 1, 10)),
                (Move(2, 20), rejected))]);

        Assert.False(comparison.Matches);
        Assert.Contains(comparison.Mismatches, value => value.StartsWith("Sequence 1 executed with a payload"));
        Assert.Contains(comparison.Mismatches, value => value.StartsWith("Sequence 2 receipt"));
        Assert.Contains(comparison.Mismatches, value => value.StartsWith("Sequence 3 has a receipt"));
    }

    [Fact]
    public void ComparerRefusesALogWithAGapAndAnEmptyComparison()
    {
        var gap = new CommandLogRecord { RecordsDropped = new RecordsDroppedRecord { Count = 1 } };
        Assert.False(CommandLogComparer.Compare(
            [gap, Sent(Move(1, 10)), Receipt(Accepted(1, 1, 10))],
            [Server(FirstSession, (Move(1, 10), Accepted(1, 1, 10)))]).Matches);
        Assert.False(CommandLogComparer.Compare([], []).Matches);
    }

    [Fact]
    public void OptionsParseLogAndReplaySettings()
    {
        Assert.True(ServerHostOptions.TryParse(
            [
                "--command-log", "false",
                "--command-log-directory", "logs",
                "--replay-command-log", "run",
                "--replay-from-generation", "3",
                "--replay-to-generation", "4",
                "--compare-client-command-log", "client",
                "--compare-server-command-log", "a",
                "--compare-server-command-log", "b",
            ],
            out var options,
            out _));
        Assert.False(options.CommandLog);
        Assert.Equal("logs", options.CommandLogDirectory);
        Assert.Equal("run", options.ReplayCommandLog);
        Assert.Equal(3UL, options.ReplayFromGeneration);
        Assert.Equal(4UL, options.ReplayToGeneration);
        Assert.Equal("client", options.CompareClientCommandLog);
        Assert.Equal(["a", "b"], options.CompareServerCommandLogs);
        Assert.False(ServerHostOptions.TryParse(["--command-log", "maybe"], out _, out _));
        Assert.False(ServerHostOptions.TryParse(["--replay-from-generation", "x"], out _, out _));
        Assert.True(ServerHostOptions.TryParse([], out var defaults, out _));
        Assert.Equal(ServerHostOptions.DefaultCommandLog, defaults.CommandLog);
    }

    /** Records a deterministic run and returns the run directory. */
    private string RecordRun(bool logCheckpoints = true, int extraCheckpoints = 0)
    {
        var directory = Path.Combine(root, "run");
        var module = new ChainModule();
        using var log = new HostCommandLog(directory, ["--port", "1"], 7);
        var server = new AuthoritativeCommandServer(module) { CommandLog = log };
        var generation = 0UL;
        void Checkpoint(uint nextEntityId)
        {
            var document = AuthoritativeTcpServer.ComposeCohortDocument(++generation, Identity, module.CaptureCohortState(),
                server.CaptureCohortState(), nextEntityId, module.Roster);
            if (logCheckpoints)
                log.RecordCheckpoint(document);
        }

        void Join(string session, uint entity)
        {
            var selection = new AuthoritativeParticipantSelection(4) { CharacterName = "Hero", EntityId = entity };
            module.InitializeParticipant(session, selection);
            log.RecordJoin(session, entity, server.CurrentTick, selection);
        }

        server.AdvanceTo(5);
        Checkpoint(1);
        Join(FirstSession, 1);
        Checkpoint(2);
        server.Process(FirstSession, Move(1, 10), 10);
        server.AdvanceTo(15);
        Checkpoint(2);
        server.Process(FirstSession, Move(2, 20), 20);
        Join(SecondSession, 2);
        server.Process(SecondSession, Move(1, 21, direction: -1), 21);
        server.Process(FirstSession, Move(2, 20), 22);
        server.AdvanceTo(30);
        Checkpoint(3);
        for (var extra = 0; extra < extraCheckpoints; extra++)
            Checkpoint(3);
        return directory;
    }

    private static AuthoritativeCommandLogReplayResult Replay(string directory, ChainModule module, ulong? from = null, ulong? to = null)
        => AuthoritativeCommandLogReplay.Run(directory, from, to, module, module, new AuthoritativeCommandServer(module));

    [Fact]
    public void ReplayFromAMidRunCheckpointReproducesTheLaterCheckpointBytes()
    {
        var directory = RecordRun();

        var fromFirstParticipant = Replay(directory, new ChainModule());
        var fromMidRun = Replay(directory, new ChainModule(), from: 3);

        Assert.True(fromFirstParticipant.Matches, fromFirstParticipant.Message);
        Assert.Equal(2UL, fromFirstParticipant.FromGeneration);
        Assert.Equal(4UL, fromFirstParticipant.ToGeneration);
        // The retransmitted duplicate executes nothing, so it is not logged.
        Assert.Equal(3, fromFirstParticipant.CommandsApplied);
        Assert.True(fromMidRun.Matches, fromMidRun.Message);
        Assert.Equal(2, fromMidRun.CommandsApplied);
    }

    [Fact]
    public void ReplayReportsAResultThatDiffersFromTheLog()
    {
        var directory = RecordRun();

        var result = Replay(directory, new ChainModule { RejectCommands = true });

        Assert.False(result.Matches);
        Assert.Contains("replayed as", result.Message);
    }

    [Fact]
    public void ReplayReportsStateThatDiffersAndKeepsItForComparison()
    {
        var directory = RecordRun();

        var result = Replay(directory, new ChainModule { ChainSalt = "other" }, from: 3);

        Assert.False(result.Matches);
        Assert.Contains("module state at character", result.Message);
        Assert.True(File.Exists(Path.Combine(directory, "replayed-4.json")));
    }

    [Fact]
    public void ReplayRefusesACheckpointWithoutParticipantsAndARunWithoutCheckpoints()
    {
        var directory = RecordRun();
        Assert.Throws<InvalidDataException>(() => Replay(directory, new ChainModule(), from: 1));
        Directory.Delete(directory, recursive: true);

        var uncheckpointed = RecordRun(logCheckpoints: false);
        Assert.Throws<InvalidDataException>(() => Replay(uncheckpointed, new ChainModule()));
    }

    [Fact]
    public void LogKeepsOnlyTheNewestCheckpoints()
    {
        var directory = RecordRun(extraCheckpoints: HostCommandLog.RetainedCheckpoints);

        var retained = Directory.GetFiles(directory, "checkpoint-*.json").Select(Path.GetFileName).ToArray();

        Assert.Equal(HostCommandLog.RetainedCheckpoints, retained.Length);
        Assert.DoesNotContain("checkpoint-1.json", retained);
        Assert.Contains($"checkpoint-{4 + HostCommandLog.RetainedCheckpoints}.json", retained);
    }

    [Fact]
    public void HeaderRecordsWhatAReplayNeedsToRebuildTheHost()
    {
        var directory = RecordRun();

        var header = CommandLogReader.ReadAll(Path.Combine(directory, HostCommandLog.LogFileName))[0].Header;

        Assert.Equal(["--port", "1"], header.ServerArguments);
        Assert.Equal(7U, header.RandomSeed);
        Assert.Equal(Environment.CurrentDirectory, header.WorkingDirectory);
    }

    /** Its state is a hash chain of everything applied, so any lost or reordered step shows. */
    private sealed class ChainModule
        : IAuthoritativeCommandExecutor, IAuthoritativeTickExecutor, IAuthoritativeGameCohortAdapter, IAuthoritativeParticipantInitializer
    {
        private readonly List<AuthoritativeModuleCohortParticipant> roster = [];
        private ulong tick;
        private string chain = "start";

        public bool RejectCommands { get; init; }

        public string ChainSalt { get; init; } = string.Empty;

        public IReadOnlyList<AuthoritativeModuleCohortParticipant> Roster
            => roster.OrderBy(participant => participant.SessionId, StringComparer.Ordinal).ToArray();

        public bool SupportsCohortPersistence => true;

        public string CohortModuleId => Identity.ModuleId;

        public int CohortFormatVersion => Identity.ModuleFormatVersion;

        private void Mix(string value)
            => chain = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{chain}|{ChainSalt}|{value}")));

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            Mix($"{sessionId}:{command}:{appliedTick}");
            return RejectCommands
                ? CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed)
                : CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong target)
        {
            for (; tick < target; tick++)
                Mix("tick");
        }

        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
        {
            Assert.Equal(4U, selection.ArchetypeId);
            Assert.Equal("Hero", selection.CharacterName);
            roster.Add(new(sessionId, selection.EntityId!.Value));
            Mix($"join:{sessionId}");
        }

        public bool IsEntityIdOccupied(uint entityId) => roster.Any(participant => participant.EntityId == entityId);

        public AuthoritativeModuleCohortState CaptureCohortState()
            => new(CohortModuleId, CohortFormatVersion, tick, Roster, chain);

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(AuthoritativeModuleCohortState state) => new Prepared(state);

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
        {
            tick = prepared.CohortState.Tick;
            chain = prepared.CohortState.SerializedState;
            roster.AddRange(prepared.CohortState.Participants);
        }

        private sealed record Prepared(AuthoritativeModuleCohortState CohortState) : IAuthoritativePreparedCohortRestore
        {
            public void Dispose()
            {
            }
        }
    }
}
