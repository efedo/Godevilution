using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    private static readonly string[] PerformanceMetricNames = {
        "PeriodicAdvanceAndCheckpointMilliseconds",
        "CommandAdmissionAndCheckpointMilliseconds",
        "SnapshotMaterializeMilliseconds",
        "SnapshotWriteMilliseconds",
        "SnapshotEnvelopePayloadBytes",
        "PeriodicAdvanceLockInclusiveMilliseconds",
        "SessionCheckpointLockWaitMilliseconds",
        "SessionStateAndLedgerCaptureMilliseconds",
        "CombinedCheckpointPublishMilliseconds",
        "CompatibilityStatePublishMilliseconds",
        "CompatibilityReceiptsPublishMilliseconds",
        "CommandQueueAdmissionMilliseconds",
        "ScheduledCommandCheckpointMilliseconds",
    };

    // These metrics are recorded only by the named scheduler policy.
    private static readonly HashSet<string> ClockGatedOnlyPerformanceMetrics = new(StringComparer.Ordinal) {
        "CommandQueueAdmissionMilliseconds",
        "ScheduledCommandCheckpointMilliseconds",
    };

    private static readonly HashSet<string> ImmediateOnlyPerformanceMetrics = new(StringComparer.Ordinal) {
        "CommandAdmissionAndCheckpointMilliseconds",
    };

    private static readonly string[] PerformanceStatisticNames = { "Mean", "P50", "P95", "Maximum" };

    [Fact]
    public async Task PerformanceOutputNeverOverwritesAndUnstartedCaptureIsExplicit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Performance-Output-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var existing = Path.Combine(directory, "existing.json");
        var cancellationToken = TestContext.Current.CancellationToken;
        try {
            await File.WriteAllTextAsync(existing, "preserve", cancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                using var unexpected = await StartedServer.StartAsync(cancellationToken, performanceOutput: existing);
            });
            Assert.Equal("preserve", await File.ReadAllTextAsync(existing, cancellationToken));
            var output = Path.Combine(directory, "not-started.json");
            using var server = await StartedServer.StartAsync(cancellationToken, performanceOutput: output);
            await server.QuitAsync(cancellationToken);
            using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(output, cancellationToken));
            Assert.Equal("NotStarted", evidence.RootElement.GetProperty("Status").GetString());
            Assert.Equal(JsonValueKind.Null, evidence.RootElement.GetProperty("Report").ValueKind);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PerformanceMetricSummariesRequireCompleteApplicableSchema()
    {
        using (var immediate = CreateMetricDocument("Immediate"))
            ValidatePerformanceMetricSummaries(immediate.RootElement, "Immediate");
        using (var clockGated = CreateMetricDocument("ClockGated"))
            ValidatePerformanceMetricSummaries(clockGated.RootElement, "ClockGated");

        var missing = CreateValidMetricSummaries("Immediate");
        missing.Remove(PerformanceMetricNames[0]);
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(missing, "Immediate"));

        var unknown = CreateValidMetricSummaries("Immediate");
        unknown.Add("UnexpectedMetric", PopulatedMetricSummary());
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(unknown, "Immediate"));

        var immediateClockGatedMetricPopulated = CreateValidMetricSummaries("Immediate");
        immediateClockGatedMetricPopulated["CommandQueueAdmissionMilliseconds"] = PopulatedMetricSummary();
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(immediateClockGatedMetricPopulated, "Immediate"));

        var immediateRequiredMetricEmpty = CreateValidMetricSummaries("Immediate");
        immediateRequiredMetricEmpty["SnapshotWriteMilliseconds"] = EmptyMetricSummary();
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(immediateRequiredMetricEmpty, "Immediate"));

        var clockGatedMetricEmpty = CreateValidMetricSummaries("ClockGated");
        clockGatedMetricEmpty["ScheduledCommandCheckpointMilliseconds"] = EmptyMetricSummary();
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(clockGatedMetricEmpty, "ClockGated"));

        var clockGatedImmediateOnlyMetricPopulated = CreateValidMetricSummaries("ClockGated");
        clockGatedImmediateOnlyMetricPopulated["CommandAdmissionAndCheckpointMilliseconds"] = PopulatedMetricSummary();
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(clockGatedImmediateOnlyMetricPopulated, "ClockGated"));

        var missingClockGatedDeclaration = CreateValidMetricSummaries("ClockGated");
        missingClockGatedDeclaration.Remove("CommandAdmissionAndCheckpointMilliseconds");
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(missingClockGatedDeclaration, "ClockGated"));

        var clockGatedApplicableMetricEmpty = CreateValidMetricSummaries("ClockGated");
        clockGatedApplicableMetricEmpty["SnapshotWriteMilliseconds"] = EmptyMetricSummary();
        Assert.Throws<InvalidDataException>(() => ValidateMetricFixture(clockGatedApplicableMetricEmpty, "ClockGated"));

        using var unknownMode = CreateMetricDocument("Immediate");
        Assert.Throws<InvalidDataException>(() => ValidatePerformanceMetricSummaries(unknownMode.RootElement, "Unknown"));
    }

    [Fact]
    public void ClockGatedTimingEvidenceBindsEveryReceiptToTheStableRequestedTick()
    {
        var firstDispatch = CreateCommandTiming(AuthoritativeClientCommandDispatchKind.FirstDispatch, requestedTick: 42);
        var retransmission = CreateCommandTiming(AuthoritativeClientCommandDispatchKind.Retransmission, requestedTick: 42);
        var acceptedReceipt = CreateAcknowledgementTiming(CommandStatus.Accepted, CommandRejectReason.Unspecified, appliedTick: 42);
        var receipts = new Dictionary<ulong, ClockGatedReceiptIdentity> {
            [1] = new(77, 42, CommandStatus.Accepted, CommandRejectReason.Unspecified),
        };

        var proof = ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch, retransmission], [acceptedReceipt]), [1UL], receipts);
        var binding = Assert.Single(proof.CommandBindings);
        Assert.Equal(77UL, binding.ServerReceiptSequence);
        Assert.Equal(42UL, binding.ReceiptAppliedTick);

        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch, CreateCommandTiming(AuthoritativeClientCommandDispatchKind.Retransmission, requestedTick: 43)],
            [acceptedReceipt]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch], [CreateAcknowledgementTiming(CommandStatus.Accepted, CommandRejectReason.Unspecified, appliedTick: 43)]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch], [CreateAcknowledgementTiming(CommandStatus.Rejected, CommandRejectReason.NotAllowed, appliedTick: 42)]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch], [acceptedReceipt]), [1UL], new Dictionary<ulong, ClockGatedReceiptIdentity> {
                [1] = new(77, 43, CommandStatus.Accepted, CommandRejectReason.Unspecified),
            }));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [CreateCommandTiming(AuthoritativeClientCommandDispatchKind.FirstDispatch, requestedTick: 0)], [acceptedReceipt]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch with { WriteOutcome = AuthoritativeClientWriteOutcome.Failed }], [acceptedReceipt]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch with { WriteEndElapsedMilliseconds = 9 }], [acceptedReceipt]), [1UL], receipts));
        Assert.Throws<InvalidDataException>(() => ValidateClockGatedReceiptTiming(CreateTimingReport(
            [firstDispatch], [acceptedReceipt]), [1UL], new Dictionary<ulong, ClockGatedReceiptIdentity> {
                [1] = new(0, 42, CommandStatus.Accepted, CommandRejectReason.Unspecified),
            }));
    }

    [Fact]
    public void PerformanceReceiptIdentitiesRequirePositiveStableServerReceipts()
    {
        var identities = new Dictionary<ulong, ClockGatedReceiptIdentity>();
        RecordStableReceiptIdentity(identities, new CommandResult {
            ClientSequence = 1,
            ServerReceiptSequence = 77,
            AppliedTick = 42,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        });
        RecordStableReceiptIdentity(identities, new CommandResult {
            ClientSequence = 1,
            ServerReceiptSequence = 77,
            AppliedTick = 42,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        });

        Assert.Throws<InvalidDataException>(() => RecordStableReceiptIdentity(identities, new CommandResult {
            ClientSequence = 1,
            ServerReceiptSequence = 78,
            AppliedTick = 42,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        }));
        Assert.Throws<InvalidDataException>(() => RecordStableReceiptIdentity(identities, new CommandResult {
            ClientSequence = 2,
            ServerReceiptSequence = 0,
            AppliedTick = 42,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        }));
        Assert.Throws<InvalidDataException>(() => RecordStableReceiptIdentity(identities, new CommandResult {
            ClientSequence = 2,
            ServerReceiptSequence = 78,
            AppliedTick = 0,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        }));
    }

    // Explicit: real-time measurement is opt-in and must not compete with the suite.
    [Theory(Explicit = true)]
    [InlineData(1, "single-player", GameMode.SinglePlayer, CommandSchedulingMode.Immediate)]
    [InlineData(4, "multiplayer", GameMode.Multiplayer, CommandSchedulingMode.Immediate)]
    [InlineData(1, "single-player", GameMode.SinglePlayer, CommandSchedulingMode.ClockGated)]
    [InlineData(4, "multiplayer", GameMode.Multiplayer, CommandSchedulingMode.ClockGated)]
    public async Task MeasureStandaloneHostBaseline(
        int participants, string mode, GameMode expectedMode, CommandSchedulingMode expectedSchedulingMode)
    {
        RejectServerAssemblyOverrideForReleaseRun();
        var buildConfiguration = ResolveBuildConfiguration();
        Assert.Equal("Release", buildConfiguration);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(75));
        var cancellationToken = deadline.Token;
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_PERFORMANCE_DIRECTORY")
            ?? Path.Combine(Path.GetTempPath(), "Godevilution-Host-Performance-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(Path.GetFullPath(root), $"{participants}-participants-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "host.json");
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: 0, randomSeed: 0, gameMode: mode,
            saveRootOverride: Path.Combine(directory, "save"), deleteSaveRootOnDispose: false,
            performanceOutput: outputPath, commandSchedulingMode: expectedSchedulingMode);
        var clients = new List<AuthoritativeClient>();
        var snapshots = new Snapshot?[participants];
        var accepted = new HashSet<ulong>();
        var receiptIdentities = new Dictionary<ulong, ClockGatedReceiptIdentity>();
        var snapshotCounts = new int[participants];
        try {
            for (var index = 0; index < participants; index++) {
                var client = new AuthoritativeClient(CreateOptions(server, "host-performance", 1) with {
                    RequestedCharacterName = $"Perf-Rogue-{index + 1}",
                }) {
                    CommandTimingRecorder = index == 0 && expectedSchedulingMode == CommandSchedulingMode.ClockGated
                        ? new AuthoritativeClientCommandTimingRecorder()
                        : null,
                };
                clients.Add(client);
                await client.ConnectAsync(cancellationToken);
                Assert.Equal(expectedMode, client.ServerGameMode);
                Assert.Equal(expectedSchedulingMode, client.ServerCommandSchedulingMode);
                snapshots[index] = (await WaitForMessageAsync(client, message => message.Snapshot is not null)).Snapshot;
            }
            // Only the first participant walks. All participants poll and must
            // observe its settled position before another command is submitted.
            var actor = snapshots[0]!.Players.First();
            var actorId = actor.EntityId;
            var startingY = actor.PositionY;

            async Task PumpAsync()
            {
                for (var index = 0; index < participants; index++) {
                    await clients[index].PollAsync(cancellationToken);
                    foreach (var message in clients[index].DrainMessages()) {
                        Assert.Null(message.Error);
                        if (message.Snapshot is { } snapshot) {
                            snapshots[index] = snapshot;
                            snapshotCounts[index]++;
                        }
                        if (message.Acknowledgement is { } ack) {
                            Assert.Equal(0, index);
                            foreach (var result in ack.Results) {
                                Assert.True(result.Status == CommandStatus.Accepted,
                                    $"Command {result.ClientSequence}: {result.Status}/{result.RejectReason}, applied tick {result.AppliedTick}.");
                                Assert.Equal(CommandRejectReason.Unspecified, result.RejectReason);
                                RecordStableReceiptIdentity(receiptIdentities, result);
                                accepted.Add(result.ClientSequence);
                            }
                        }
                    }
                }
            }

            async Task RunRoundsAsync(int rounds, int priorMoves)
            {
                for (var round = 0; round < rounds; round++) {
                    var sequence = clients[0].Queue(new Command {
                        MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
                    }, clients[0].SuggestedCommandTick(snapshots[0]!.Tick));
                    var interval = Stopwatch.StartNew();
                    bool Settled() => accepted.Contains(sequence) && snapshots.All(snapshot => snapshot is not null
                        && snapshot.Players.Count == participants
                        && snapshot.Players.Single(player => player.EntityId == actorId) is {
                            CombatMode: PlayerCombatMode.Stand, CurrentAction: null,
                        } moved && moved.PositionY == startingY + priorMoves + round + 1);
                    while (interval.Elapsed < TimeSpan.FromSeconds(1) || !Settled()) {
                        await PumpAsync();
                        Assert.True(interval.Elapsed < TimeSpan.FromSeconds(5),
                            $"Movement {sequence} did not settle for every participant within five seconds.");
                        await Task.Delay(25, cancellationToken);
                    }
                    Assert.Contains(sequence, accepted);
                    foreach (var snapshot in snapshots) {
                        Assert.NotNull(snapshot);
                        Assert.Equal(participants, snapshot.Players.Count);
                        var moved = snapshot.Players.Single(player => player.EntityId == actorId);
                        Assert.True(moved.PositionY == startingY + priorMoves + round + 1,
                            $"Actor {actorId} in tick {snapshot.Tick}, owner {snapshot.Players[0].EntityId}: expected Y={startingY + priorMoves + round + 1}, got {moved.PositionY}.");
                        Assert.Equal(PlayerCombatMode.Stand, moved.CombatMode);
                        Assert.Null(moved.CurrentAction);
                    }
                }
            }

            var warmup = Stopwatch.StartNew();
            await RunRoundsAsync(5, 0);
            warmup.Stop();
            await server.StartPerformanceRecordingAsync(cancellationToken);
            var measured = Stopwatch.StartNew();
            await RunRoundsAsync(15, 5);
            // Quit while connected; shutdown emits the completed measurement.
            await server.QuitAsync(cancellationToken);
            Assert.True(server.HasExited, "The owned host must have exited before workload evidence is accepted.");
            using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath, cancellationToken));
            var host = evidence.RootElement;
            Assert.Equal("Completed", host.GetProperty("Status").GetString());
            Assert.Equal(participants, host.GetProperty("ActiveParticipants").GetInt32());
            Assert.Equal(expectedMode.ToString(), host.GetProperty("GameMode").GetString());
            Assert.Equal(server.ContentHash, host.GetProperty("ContentHash").GetString());
            Assert.Equal(server.RulesetHash, host.GetProperty("RulesetHash").GetString());
            var report = host.GetProperty("Report");
            Assert.InRange(report.GetProperty("ElapsedSeconds").GetDouble(), 15, 40);
            var schedulingMode = clients[0].ServerCommandSchedulingMode;
            Assert.Equal(expectedSchedulingMode, schedulingMode);
            Assert.All(clients, client => Assert.Equal(schedulingMode, client.ServerCommandSchedulingMode));
            ValidatePerformanceMetricSummaries(report.GetProperty("Metrics"), schedulingMode.ToString());
            Assert.Equal(20, accepted.Count);
            Assert.All(snapshotCounts, count => Assert.True(count >= 40));
            var clockGatedTimingProof = expectedSchedulingMode == CommandSchedulingMode.ClockGated
                ? ValidateClockGatedReceiptTiming(
                    clients[0].CommandTimingRecorder?.CaptureReport()
                        ?? throw new InvalidDataException("The moving client has no clock-gated timing recorder."),
                    accepted,
                    receiptIdentities)
                : null;
            await File.WriteAllTextAsync(Path.Combine(directory, "workload.json"), JsonSerializer.Serialize(new {
                Passed = true, Participants = participants, Mode = mode, BuildConfiguration = buildConfiguration,
                SchedulingMode = schedulingMode.ToString(), SchedulingModeEvidence = "NegotiatedServerHello",
                ParticipantSchedulingModes = clients.Select(client => client.ServerCommandSchedulingMode.ToString()).ToArray(),
                NotApplicableMetrics = GetNotApplicablePerformanceMetrics(schedulingMode.ToString()),
                ClockGatedTimingProof = clockGatedTimingProof,
                OwnedHostProcessId = server.ProcessId, OwnedHostExited = server.HasExited, OwnedHostCleanQuitVerified = true,
                WarmupSeconds = warmup.Elapsed.TotalSeconds, MeasuredSeconds = measured.Elapsed.TotalSeconds,
                MeasuredCommands = 15, AcceptedCommandsIncludingWarmup = accepted.Count,
                SnapshotCountsIncludingWarmup = snapshotCounts, PumpDelayMilliseconds = 25,
                SnapshotPollIntervalMilliseconds = 250, MinimumCommandIntervalSeconds = 1,
                Level = 0, Seed = 0, Workload = "One moving Rogue; remaining participants observe; loopback TCP; persistent checkpoints",
                Scope = "Standalone server only; authored town; no Godot frames, campaign load, native generation, or release gate",
            }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        } finally {
            foreach (var client in clients)
                await client.DisposeAsync();
            File.WriteAllText(Path.Combine(directory, "observations.json"), JsonSerializer.Serialize(
                snapshots.Select(snapshot => new {
                    snapshot?.Tick,
                    Players = snapshot?.Players.Select(player => new { player.EntityId, player.PositionX, player.PositionY }),
                }), new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed record ClockGatedCommandTimingBinding(
        ulong ClientSequence, ulong FirstDispatchRequestedTick, ulong ReceiptAppliedTick,
        ulong ServerReceiptSequence, string ReceiptStatus, string ReceiptRejectReason,
        int DispatchCount, int AcknowledgementCount);

    private sealed record ClockGatedReceiptIdentity(
        ulong ServerReceiptSequence, ulong AppliedTick, CommandStatus Status, CommandRejectReason RejectReason);

    private sealed record ClockGatedTimingProof(
        int AcceptedSequences, int FirstDispatches, int StableRequestedTickDispatches, int MatchedAcknowledgements,
        long DroppedTimingRecords, IReadOnlyList<ClockGatedCommandTimingBinding> CommandBindings);

    private static AuthoritativeClientCommandTiming CreateCommandTiming(
        AuthoritativeClientCommandDispatchKind dispatchKind, ulong requestedTick)
        => new(1, 1, dispatchKind, dispatchKind == AuthoritativeClientCommandDispatchKind.FirstDispatch ? 1 : 2,
            null, requestedTick, 10, 11, 12, AuthoritativeClientWriteOutcome.Completed,
            100, 101, 102, 0, true);

    private static AuthoritativeClientAcknowledgementTiming CreateAcknowledgementTiming(
        CommandStatus status, CommandRejectReason rejectReason, ulong appliedTick)
        => new(1, 1, 0, status, rejectReason, appliedTick, true, false, null, 0);

    private static AuthoritativeClientTimingCaptureReport CreateTimingReport(
        IReadOnlyList<AuthoritativeClientCommandTiming> commands,
        IReadOnlyList<AuthoritativeClientAcknowledgementTiming> acknowledgements)
        => new(commands, [], [], [], acknowledgements, Stopwatch.Frequency, 0);

    private static void RecordStableReceiptIdentity(
        IDictionary<ulong, ClockGatedReceiptIdentity> receiptIdentities, CommandResult result)
    {
        if (result.ServerReceiptSequence == 0 || result.AppliedTick == 0)
            throw new InvalidDataException($"Command {result.ClientSequence} has no positive server receipt identity.");
        var identity = new ClockGatedReceiptIdentity(
            result.ServerReceiptSequence, result.AppliedTick, result.Status, result.RejectReason);
        if (receiptIdentities.TryGetValue(result.ClientSequence, out var priorIdentity)) {
            if (priorIdentity != identity)
                throw new InvalidDataException($"Command {result.ClientSequence} changed its server receipt identity.");
            return;
        }
        receiptIdentities.Add(result.ClientSequence, identity);
    }

    private static ClockGatedTimingProof ValidateClockGatedReceiptTiming(
        AuthoritativeClientTimingCaptureReport report, IEnumerable<ulong> acceptedSequences,
        IReadOnlyDictionary<ulong, ClockGatedReceiptIdentity> receiptIdentities)
    {
        if (report.DroppedRecordCount != 0)
            throw new InvalidDataException("Clock-gated client timing evidence dropped records.");

        var accepted = acceptedSequences.Distinct().Order().ToArray();
        if (accepted.Length == 0)
            throw new InvalidDataException("Clock-gated timing evidence has no accepted command sequences.");
        if (!receiptIdentities.Keys.Order().SequenceEqual(accepted))
            throw new InvalidDataException("Clock-gated server receipt evidence does not cover exactly the accepted sequences.");
        var commandsBySequence = report.Commands.GroupBy(command => command.ClientSequence)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (!commandsBySequence.Keys.Order().SequenceEqual(accepted))
            throw new InvalidDataException("Clock-gated dispatch evidence does not cover exactly the accepted sequences.");
        var acknowledgementsBySequence = report.Acknowledgements.GroupBy(acknowledgement => acknowledgement.ClientSequence)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (!acknowledgementsBySequence.Keys.Order().SequenceEqual(accepted))
            throw new InvalidDataException("Clock-gated acknowledgement evidence does not cover exactly the accepted sequences.");

        var bindings = new List<ClockGatedCommandTimingBinding>(accepted.Length);
        foreach (var sequence in accepted) {
            var dispatches = commandsBySequence[sequence];
            var firstDispatches = dispatches.Where(dispatch =>
                dispatch.DispatchKind == AuthoritativeClientCommandDispatchKind.FirstDispatch).ToArray();
            if (firstDispatches.Length != 1)
                throw new InvalidDataException($"Clock-gated sequence {sequence} must have exactly one first dispatch.");
            var firstDispatch = firstDispatches[0];
            var requestedTick = firstDispatch.RequestedTick;
            if (requestedTick == 0)
                throw new InvalidDataException($"Clock-gated sequence {sequence} has no positive requested tick.");
            if (firstDispatch.WriteOutcome != AuthoritativeClientWriteOutcome.Completed
                || firstDispatch.WriteStartElapsedMilliseconds is not { } writeStartElapsed
                || firstDispatch.WriteEndElapsedMilliseconds is not { } writeEndElapsed
                || writeStartElapsed < firstDispatch.FreezeElapsedMilliseconds
                || writeEndElapsed < writeStartElapsed
                || firstDispatch.WriteStartStopwatchTimestamp is not { } writeStartTimestamp
                || firstDispatch.WriteEndStopwatchTimestamp is not { } writeEndTimestamp
                || writeStartTimestamp < firstDispatch.FreezeStopwatchTimestamp
                || writeEndTimestamp < writeStartTimestamp) {
                throw new InvalidDataException($"Clock-gated sequence {sequence} lacks a completed first write with coherent timestamps.");
            }
            if (dispatches.Any(dispatch => dispatch.DispatchKind is not (
                    AuthoritativeClientCommandDispatchKind.FirstDispatch or AuthoritativeClientCommandDispatchKind.Retransmission)
                || dispatch.RequestedTick != requestedTick)) {
                throw new InvalidDataException($"Clock-gated sequence {sequence} changed its requested tick across dispatches.");
            }
            var acknowledgements = acknowledgementsBySequence[sequence];
            var receiptIdentity = receiptIdentities[sequence];
            if (receiptIdentity.ServerReceiptSequence == 0 || receiptIdentity.AppliedTick == 0
                || receiptIdentity.Status != CommandStatus.Accepted
                || receiptIdentity.RejectReason != CommandRejectReason.Unspecified
                || receiptIdentity.AppliedTick != requestedTick) {
                throw new InvalidDataException($"Clock-gated sequence {sequence} has an invalid server receipt identity or tick.");
            }
            if (acknowledgements.Length == 0 || acknowledgements.Any(acknowledgement =>
                    acknowledgement.Status != CommandStatus.Accepted
                    || acknowledgement.RejectReason != CommandRejectReason.Unspecified
                    || acknowledgement.AppliedTick != requestedTick
                    || acknowledgement.AppliedTick != receiptIdentity.AppliedTick)) {
                throw new InvalidDataException($"Clock-gated sequence {sequence} lacks an accepted receipt at its first-dispatch requested tick.");
            }
            bindings.Add(new ClockGatedCommandTimingBinding(
                sequence, requestedTick, receiptIdentity.AppliedTick, receiptIdentity.ServerReceiptSequence,
                receiptIdentity.Status.ToString(), receiptIdentity.RejectReason.ToString(), dispatches.Length, acknowledgements.Length));
        }

        return new ClockGatedTimingProof(accepted.Length, accepted.Length, bindings.Sum(binding => binding.DispatchCount),
            bindings.Sum(binding => binding.AcknowledgementCount), report.DroppedRecordCount, bindings);
    }

    private static void RejectServerAssemblyOverrideForReleaseRun()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY")))
            throw new InvalidOperationException("The Release host baseline cannot use an explicit server assembly override.");
    }

    private sealed record PerformanceMetricFixture(
        int Samples, long DroppedSamples, double? Mean, double? P50, double? P95, double? Maximum);

    private static PerformanceMetricFixture PopulatedMetricSummary()
        => new(4, 0, 1.5, 1, 2, 3);

    private static PerformanceMetricFixture EmptyMetricSummary()
        => new(0, 0, null, null, null, null);

    private static Dictionary<string, PerformanceMetricFixture> CreateValidMetricSummaries(string schedulingMode)
    {
        var summaries = new Dictionary<string, PerformanceMetricFixture>(StringComparer.Ordinal);
        foreach (var name in PerformanceMetricNames) {
            summaries.Add(name, IsPerformanceMetricApplicable(schedulingMode, name)
                ? PopulatedMetricSummary() : EmptyMetricSummary());
        }
        return summaries;
    }

    private static JsonDocument CreateMetricDocument(string schedulingMode)
        => JsonDocument.Parse(JsonSerializer.Serialize(CreateValidMetricSummaries(schedulingMode)));

    private static void ValidateMetricFixture(
        Dictionary<string, PerformanceMetricFixture> summaries, string schedulingMode)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summaries));
        ValidatePerformanceMetricSummaries(document.RootElement, schedulingMode);
    }

    private static string[] GetNotApplicablePerformanceMetrics(string schedulingMode)
        => PerformanceMetricNames.Where(name => !IsPerformanceMetricApplicable(schedulingMode, name)).ToArray();

    private static bool IsPerformanceMetricApplicable(string schedulingMode, string metricName)
        => schedulingMode switch {
            "Immediate" => !ClockGatedOnlyPerformanceMetrics.Contains(metricName),
            "ClockGated" => !ImmediateOnlyPerformanceMetrics.Contains(metricName),
            _ => throw new InvalidDataException($"Unknown command scheduling mode '{schedulingMode}'."),
        };

    private static void ValidatePerformanceMetricSummaries(JsonElement metrics, string schedulingMode)
    {
        if (schedulingMode is not ("Immediate" or "ClockGated"))
            throw new InvalidDataException($"Unknown command scheduling mode '{schedulingMode}'.");
        RequireExactProperties(metrics, PerformanceMetricNames, "Metrics");

        foreach (var name in PerformanceMetricNames) {
            var summary = metrics.GetProperty(name);
            RequireExactProperties(summary,
                new[] { "Samples", "DroppedSamples", "Mean", "P50", "P95", "Maximum" }, name);
            var samplesValue = summary.GetProperty("Samples");
            var droppedValue = summary.GetProperty("DroppedSamples");
            if (samplesValue.ValueKind != JsonValueKind.Number || !samplesValue.TryGetInt32(out var samples) || samples < 0)
                throw new InvalidDataException($"Metric {name} has an invalid Samples value.");
            if (droppedValue.ValueKind != JsonValueKind.Number || !droppedValue.TryGetInt64(out var dropped) || dropped != 0)
                throw new InvalidDataException($"Metric {name} has an invalid or nonzero DroppedSamples value.");

            var statistics = PerformanceStatisticNames.Select(name => summary.GetProperty(name)).ToArray();
            if (!IsPerformanceMetricApplicable(schedulingMode, name)) {
                if (samples != 0 || statistics.Any(value => value.ValueKind != JsonValueKind.Null))
                    throw new InvalidDataException($"{schedulingMode} scheduling must leave inapplicable metric {name} empty.");
                continue;
            }

            if (samples == 0)
                throw new InvalidDataException($"Applicable metric {name} has no samples.");
            var values = new double[statistics.Length];
            for (var index = 0; index < statistics.Length; index++) {
                if (statistics[index].ValueKind != JsonValueKind.Number
                    || !statistics[index].TryGetDouble(out values[index])
                    || !double.IsFinite(values[index]) || values[index] < 0) {
                    throw new InvalidDataException($"Applicable metric {name} has an invalid {PerformanceStatisticNames[index]} value.");
                }
            }
            if (values[2] <= 0 || values[1] > values[2] || values[2] > values[3] || values[0] > values[3])
                throw new InvalidDataException($"Applicable metric {name} has inconsistent summary statistics.");
        }
    }

    private static void RequireExactProperties(JsonElement value, IEnumerable<string> expectedNames, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{label} must be a JSON object.");
        var expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) {
            if (!actual.Add(property.Name))
                throw new InvalidDataException($"{label} contains duplicate property {property.Name}.");
        }
        if (!actual.SetEquals(expected))
            throw new InvalidDataException($"{label} has missing or unexpected properties.");
    }
}
