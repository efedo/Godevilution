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

    // Explicit: real-time measurement is opt-in and must not compete with the suite.
    [Theory(Explicit = true)]
    [InlineData(1, "single-player", GameMode.SinglePlayer)]
    [InlineData(4, "multiplayer", GameMode.Multiplayer)]
    public async Task MeasureStandaloneHostBaseline(int participants, string mode, GameMode expectedMode)
    {
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
            performanceOutput: outputPath);
        var clients = new List<AuthoritativeClient>();
        var snapshots = new Snapshot?[participants];
        var accepted = new HashSet<ulong>();
        var snapshotCounts = new int[participants];
        try {
            for (var index = 0; index < participants; index++) {
                var client = new AuthoritativeClient(CreateOptions(server, "host-performance", 1) with {
                    RequestedCharacterName = $"Perf-Rogue-{index + 1}",
                });
                clients.Add(client);
                await client.ConnectAsync(cancellationToken);
                Assert.Equal(expectedMode, client.ServerGameMode);
                Assert.Equal(CommandSchedulingMode.Immediate, client.ServerCommandSchedulingMode);
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
            Assert.All(clients, client => Assert.Equal(schedulingMode, client.ServerCommandSchedulingMode));
            ValidatePerformanceMetricSummaries(report.GetProperty("Metrics"), schedulingMode.ToString());
            Assert.Equal(20, accepted.Count);
            Assert.All(snapshotCounts, count => Assert.True(count >= 40));
            await File.WriteAllTextAsync(Path.Combine(directory, "workload.json"), JsonSerializer.Serialize(new {
                Passed = true, Participants = participants, Mode = mode, BuildConfiguration = ResolveBuildConfiguration(),
                SchedulingMode = schedulingMode.ToString(), SchedulingModeEvidence = "NegotiatedServerHello",
                NotApplicableMetrics = GetNotApplicablePerformanceMetrics(schedulingMode.ToString()),
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
