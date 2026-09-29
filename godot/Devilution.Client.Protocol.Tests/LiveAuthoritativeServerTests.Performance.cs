using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
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
            using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath, cancellationToken));
            var host = evidence.RootElement;
            Assert.Equal("Completed", host.GetProperty("Status").GetString());
            Assert.Equal(participants, host.GetProperty("ActiveParticipants").GetInt32());
            Assert.Equal(expectedMode.ToString(), host.GetProperty("GameMode").GetString());
            Assert.Equal(server.ContentHash, host.GetProperty("ContentHash").GetString());
            Assert.Equal(server.RulesetHash, host.GetProperty("RulesetHash").GetString());
            var report = host.GetProperty("Report");
            Assert.InRange(report.GetProperty("ElapsedSeconds").GetDouble(), 15, 40);
            foreach (var metric in report.GetProperty("Metrics").EnumerateObject()) {
                Assert.True(metric.Value.GetProperty("Samples").GetInt32() > 0, metric.Name);
                Assert.Equal(0, metric.Value.GetProperty("DroppedSamples").GetInt64());
                Assert.True(metric.Value.GetProperty("P95").GetDouble() > 0, metric.Name);
            }
            Assert.Equal(20, accepted.Count);
            Assert.All(snapshotCounts, count => Assert.True(count >= 40));
            await File.WriteAllTextAsync(Path.Combine(directory, "workload.json"), JsonSerializer.Serialize(new {
                Passed = true, Participants = participants, Mode = mode, BuildConfiguration = ResolveBuildConfiguration(),
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
}
