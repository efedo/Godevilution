using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Collection(WallClockTimingCollection.CollectionName)]
[Trait("Category", "Slow")]
public sealed class LiveAuthoritativeTickRateSynchronizationTests : LiveAuthoritativeTestBase
{
    /**
     * Brief clock-gated play at a slow, the default and a fast odd rate: the client adopts
     * the host's rate, its tick estimate tracks the host clock, and each step it schedules
     * from its observed tick is accepted on that tick (not late) and walks one cell.
     */
    [Theory]
    [InlineData(9u)]
    [InlineData(20u)]
    [InlineData(41u)]
    public async Task ClockGatedPlayStaysSynchronizedAtTheHostTickRate(uint tickRateHz)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var cancellationToken = deadline.Token;
        // Keep the supplied 9/20/41 Hz rates: this test checks negotiation and real clock rate.
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: 0, randomSeed: 0, gameMode: "single-player",
            commandSchedulingMode: CommandSchedulingMode.ClockGated, tickRateHz: tickRateHz);
        await using var client = new AuthoritativeClient(CreateOptions(server, "godot-live-tick-rate-sync", 1) with {
            SnapshotPollInterval = TimeSpan.FromMilliseconds(250),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        });
        await client.ConnectAsync(cancellationToken);
        Assert.Equal(CommandSchedulingMode.ClockGated, client.ServerCommandSchedulingMode);
        Assert.Equal(tickRateHz, client.ServerTickRateHz);
        Assert.Equal(tickRateHz, client.ServerIdentity?.TickRateHz);

        Snapshot? snapshot = null;
        var results = new Dictionary<ulong, CommandResult>();
        async Task PumpAsync()
        {
            await client.PollAsync(cancellationToken);
            foreach (var message in client.DrainMessages()) {
                Assert.Null(message.Error);
                if (message.Snapshot is { } received && (snapshot is null || received.Tick >= snapshot.Tick))
                    snapshot = received;
                foreach (var result in message.Acknowledgement?.Results ?? [])
                    // A legitimate retry may follow acceptance in the same drain.
                    // Judge the first receipt, rather than replacing it with Duplicate.
                    results.TryAdd(result.ClientSequence, result);
            }
        }
        async Task<Snapshot> PumpUntilAsync(Func<Snapshot, bool> done, string failure)
        {
            var limit = Stopwatch.StartNew();
            while (snapshot is null || !done(snapshot)) {
                Assert.True(limit.Elapsed < TimeSpan.FromSeconds(10), failure);
                await PumpAsync();
                await Task.Delay(10, cancellationToken);
            }
            return snapshot;
        }

        // The host clock runs at the advertised rate (one 20-tick checkpoint of slack).
        var first = await PumpUntilAsync(_ => true, "No initial snapshot arrived.");
        var clock = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        var second = await PumpUntilAsync(current => current.Tick > first.Tick, "The host clock did not advance.");
        var expectedTicks = clock.Elapsed.TotalSeconds * tickRateHz;
        Assert.InRange(second.Tick - first.Tick, (ulong)Math.Max(1, expectedTicks * 0.75 - 20), (ulong)(expectedTicks * 1.25 + 20));

        var actorId = second.Players.Single().EntityId;
        for (var step = 1; step <= 3; step++) {
            var start = await PumpUntilAsync(current => current.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null },
                $"Step {step} did not start from a settled player at {tickRateHz} Hz.");
            var startY = start.Players.Single().PositionY;
            var sequence = client.Queue(new Command { MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 } },
                client.SuggestedCommandTick(start.Tick));
            var moved = await PumpUntilAsync(current => results.TryGetValue(sequence, out var result)
                    && current.Tick >= result.AppliedTick
                    && current.Players.Single(player => player.EntityId == actorId) is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null },
                $"Step {step} did not settle at {tickRateHz} Hz.");
            var receipt = results[sequence];
            Assert.True(receipt is { Status: CommandStatus.Accepted, RejectReason: CommandRejectReason.Unspecified },
                $"Step {step} at {tickRateHz} Hz: {receipt.Status}/{receipt.RejectReason}, applied tick {receipt.AppliedTick}.");
            Assert.True(receipt.AppliedTick > start.Tick, $"Step {step} applied at {receipt.AppliedTick}, not after observed tick {start.Tick}.");
            Assert.Equal(startY + 1, moved.Players.Single(player => player.EntityId == actorId).PositionY);
        }
    }
}
