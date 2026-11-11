using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Game speed against the production host and real gameplay adapter. */
[Collection(WallClockTimingCollection.CollectionName)]
[Trait("Category", "Slow")]
public sealed class LiveAuthoritativeGameSpeedTests : LiveAuthoritativeTestBase
{
    [Fact]
    public async Task SinglePlayerClientScalesTheHostClockAndKeepsCommandsOnTime()
    {
        using var deadline = Deadline(60);
        var cancellationToken = deadline.Token;
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: 0, randomSeed: 0, gameMode: "single-player",
            commandSchedulingMode: CommandSchedulingMode.ClockGated, tickRateHz: 20);
        await using var client = new AuthoritativeClient(CreateOptions(server, "godot-live-game-speed", 1) with {
            // Match the Godot client: references and level-scoped snapshots stay on at speed.
            UseSnapshotReferences = true, UseLevelScopedIdleReferences = true, UseLevelScopedSnapshots = true,
            SnapshotPollInterval = TimeSpan.FromMilliseconds(100),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        });
        var pump = new Pump(client, cancellationToken);
        await client.ConnectAsync(cancellationToken);
        Assert.True(client.CanControlGameSpeed);
        Assert.Equal(1U, client.GameSpeedMultiplier);
        Assert.Equal(20U, client.ServerTickRateHz);

        client.RequestGameSpeed(10);
        await pump.UntilAsync(() => client.GameSpeedMultiplier == 10, "The host did not acknowledge 10x.");
        Assert.Equal(10U, server.GameSpeedMultiplier);
        Assert.Equal(200U, client.ServerTickRateHz);
        Assert.Equal(20U, client.BaseTickRateHz);
        Assert.Equal(20U, client.ServerIdentity?.TickRateHz);
        Assert.Equal(1, client.GameSpeedRevision);

        // The host runs at 200 Hz in wall time (one 20-tick checkpoint of slack).
        var first = await pump.SnapshotAsync(_ => true, "No snapshot arrived at 10x.");
        var wall = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var second = await pump.SnapshotAsync(current => current.Tick > first.Tick + 100, "The host did not run at 10x.");
        var expected = wall.Elapsed.TotalSeconds * 200;
        Assert.InRange(second.Tick - first.Tick, (ulong)(expected * 0.75 - 20), (ulong)(expected * 1.25 + 20));

        // A step scheduled from the scaled lead is accepted on its requested tick.
        for (var step = 0; step < 3; step++) {
            var start = await pump.SnapshotAsync(current => current.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null },
                $"Step {step} did not start from a settled player.");
            var requested = client.SuggestedCommandTick(start.Tick);
            var sequence = client.Queue(new Command { MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = step % 2 == 0 ? 1 : -1 } },
                requested);
            var result = await pump.ResultAsync(sequence, $"Step {step} was not acknowledged at 10x.");
            Assert.Equal(CommandStatus.Accepted, result.Status);
            Assert.Equal(requested, result.AppliedTick);
        }

        // A reconnect starts at the host default and the client re-requests its speed.
        await client.DisconnectAsync();
        await pump.UntilAsync(() => server.GameSpeedMultiplier == 1, "The host kept the speed of a closed session.");
        await client.ReconnectAsync(cancellationToken);
        await pump.UntilAsync(() => client.GameSpeedMultiplier == 10 && server.GameSpeedMultiplier == 10,
            "The reconnected client did not restore 10x.");
    }

    [Fact]
    public async Task MultiplayerSpeedIsSetByTheHostOnly()
    {
        using var deadline = Deadline(30);
        var cancellationToken = deadline.Token;
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: 0, randomSeed: 0, gameMode: "multiplayer",
            commandSchedulingMode: CommandSchedulingMode.ClockGated, tickRateHz: 20, gameSpeed: 3);
        await using var client = new AuthoritativeClient(CreateOptions(server, "godot-live-game-speed-mp", 1));
        var pump = new Pump(client, cancellationToken);
        await client.ConnectAsync(cancellationToken);
        Assert.False(client.CanControlGameSpeed);
        Assert.Equal(3U, client.GameSpeedMultiplier);
        Assert.Equal(60U, client.ServerTickRateHz);

        client.RequestGameSpeed(20);
        await pump.SnapshotAsync(_ => true, "No snapshot arrived.");
        await pump.SnapshotAsync(_ => true, "No snapshot arrived.");
        Assert.Equal(0, client.GameSpeedRevision); // Nothing is sent without control.
        Assert.Equal(3U, server.GameSpeedMultiplier);
    }

    /**
     * The same command script on the same seed yields the same authoritative state at 1x and
     * 20x. A manual host clock lands both runs on tick 400 exactly; the 20x run reaches it in
     * twenty 20-tick steps instead of 400 single ticks.
     */
    [Fact]
    public async Task SameCommandsGiveTheSameStateAtOneAndTwentyTimes()
    {
        using var deadline = Deadline(120);
        var normal = await RunScriptAsync(1, deadline.Token);
        var fast = await RunScriptAsync(20, deadline.Token);
        Assert.Equal(400UL, normal.Tick);
        Assert.Equal(normal.Tick, fast.Tick);
        Assert.Equal(normal.Players.Single().PositionX, fast.Players.Single().PositionX);
        Assert.Equal(normal.Players.Single().PositionY, fast.Players.Single().PositionY);
        Assert.Equal(normal.RandomState, fast.RandomState);
        Assert.Equal(WithoutStartingItemSeeds(normal), WithoutStartingItemSeeds(fast));
        Assert.NotEqual((0, 0), (normal.Players.Single().PositionX - startPosition.X, normal.Players.Single().PositionY - startPosition.Y));
    }

    private (int X, int Y) startPosition;

    /**
     * A new participant's starting items draw seeds outside the world seed, so two 1x runs
     * differ there too. Everything else, including world RNG, must match exactly.
     */
    private static Snapshot WithoutStartingItemSeeds(Snapshot snapshot)
    {
        var copy = snapshot.Clone();
        copy.StateSha256 = string.Empty;
        foreach (var player in copy.Players)
            ClearItemSeeds(player);
        return copy;

        static void ClearItemSeeds(Google.Protobuf.IMessage message)
        {
            foreach (var field in message.Descriptor.Fields.InFieldNumberOrder()) {
                var value = field.Accessor.GetValue(message);
                if (field.Name == "item_seed" && !field.IsRepeated)
                    field.Accessor.SetValue(message, 0U);
                else if (field.FieldType == Google.Protobuf.Reflection.FieldType.Message && field.IsRepeated && !field.IsMap)
                    foreach (var child in (System.Collections.IEnumerable)value)
                        ClearItemSeeds((Google.Protobuf.IMessage)child);
                else if (field.FieldType == Google.Protobuf.Reflection.FieldType.Message && !field.IsRepeated && value is Google.Protobuf.IMessage child)
                    ClearItemSeeds(child);
            }
        }
    }

    private async Task<Snapshot> RunScriptAsync(uint speed, CancellationToken cancellationToken)
    {
        var time = new ManualTimeProvider();
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: 1, randomSeed: 7, gameMode: "single-player",
            commandSchedulingMode: CommandSchedulingMode.ClockGated, tickRateHz: 20, clockTime: time);
        await using var client = new AuthoritativeClient(CreateOptions(server, "godot-live-game-speed-determinism", 1));
        var pump = new Pump(client, cancellationToken);
        await client.ConnectAsync(cancellationToken);
        if (speed != 1) {
            client.RequestGameSpeed(speed);
            await pump.UntilAsync(() => client.GameSpeedMultiplier == speed, $"The host did not acknowledge {speed}x.");
        }
        var initial = await pump.SnapshotAsync(_ => true, "No initial snapshot arrived.");
        Assert.Equal(0UL, initial.Tick);
        startPosition = (initial.Players.Single().PositionX, initial.Players.Single().PositionY);

        (ulong Tick, int X, int Y)[] script = [(40, 0, 1), (120, 1, 0), (200, 0, 1), (280, -1, 0)];
        var sequences = script.Select(step => client.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = step.X, DirectionY = step.Y },
        }, step.Tick)).ToArray();
        await pump.UntilAsync(() => server.PendingCommandCount == script.Length, "The script was not admitted ahead of time.");

        // Each 50 ms step is one maximum clock step: 1 tick at 1x, 20 ticks at 20x.
        while (server.CurrentTick < 400) {
            time.Advance(TimeSpan.FromMilliseconds(50));
            _ = server.CurrentTick;
        }
        Assert.Equal(400UL, server.CurrentTick);
        foreach (var sequence in sequences) {
            var result = await pump.ResultAsync(sequence, "A scripted command was not acknowledged.");
            Assert.Equal(CommandStatus.Accepted, result.Status);
        }
        return await pump.SnapshotAsync(current => current.Tick == 400, "No snapshot arrived at tick 400.");
    }

    /**
     * Local evidence, not CI: plays 20 s of back-and-forth walking at a game speed of the 20 Hz
     * reference and reports snapshot admission cost, snapshot cadence, host rate and command
     * timeliness. Budget: the host keeps 90% of its paced rate, admission p95 fits one 60 Hz
     * frame, no step is refused as too late and at least 90% apply on their requested tick.
     */
    [Theory(Explicit = true)]
    [InlineData(1U, 0U)]
    [InlineData(20U, 0U)]
    [InlineData(20U, 1U)]
    public async Task GameSpeedStaysWithinBudget(uint speed, uint level)
    {
        using var deadline = Deadline(120);
        var cancellationToken = deadline.Token;
        using var server = await StartedServer.StartAsync(cancellationToken,
            startingLevelOverride: level, randomSeed: 0, gameMode: "single-player",
            commandSchedulingMode: CommandSchedulingMode.ClockGated, tickRateHz: 20);
        await using var client = new AuthoritativeClient(CreateOptions(server, "godot-live-game-speed-budget", 1) with {
            // Match the Godot client: references and level-scoped snapshots stay on at speed.
            UseSnapshotReferences = true, UseLevelScopedIdleReferences = true, UseLevelScopedSnapshots = true,
            SnapshotPollInterval = TimeSpan.FromMilliseconds(250),
            MovingSnapshotPollInterval = TimeSpan.FromMilliseconds(100),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        });
        var model = new AuthoritativeClientModel();
        var admissions = new List<double>();
        var snapshotGaps = new List<double>();
        var results = new Dictionary<ulong, CommandResult>();
        Snapshot? latest = null;
        long? lastSnapshotAt = null;
        await client.ConnectAsync(cancellationToken);
        client.RequestGameSpeed(speed);

        async Task FrameAsync()
        {
            await client.PollAsync(cancellationToken);
            foreach (var message in client.DrainMessages()) {
                Assert.Null(message.Error);
                var started = Stopwatch.GetTimestamp();
                client.ApplyMessageToModel(model, message);
                if (message.Snapshot is { } snapshot) {
                    admissions.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    var now = Stopwatch.GetTimestamp();
                    if (lastSnapshotAt is { } previous)
                        snapshotGaps.Add(Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
                    lastSnapshotAt = now;
                    if (latest is null || snapshot.Tick >= latest.Tick)
                        latest = model.Snapshot ?? snapshot;
                }
                foreach (var result in message.Acknowledgement?.Results ?? [])
                    results.TryAdd(result.ClientSequence, result);
            }
            await Task.Delay(TimeSpan.FromSeconds(1d / 60), cancellationToken);
        }

        while (client.GameSpeedMultiplier != speed || latest is null)
            await FrameAsync();
        admissions.Clear();
        snapshotGaps.Clear();
        var startTick = latest.Tick;
        var wall = Stopwatch.StartNew();
        var queued = new List<(ulong Sequence, ulong Requested)>();
        var direction = 1;
        while (wall.Elapsed < TimeSpan.FromSeconds(20)) {
            // Only a snapshot after the previous step's applied tick shows whether it finished.
            var settledAfter = queued.Count == 0 || !results.TryGetValue(queued[^1].Sequence, out var last) ? 0 : last.AppliedTick;
            if (latest.Tick > settledAfter && latest.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null }
                && client.PendingCommandCount == 0) {
                direction = -direction;
                var requested = client.SuggestedCommandTick(latest.Tick);
                queued.Add((client.Queue(new Command { MoveRequested = new MoveRequested { DirectionY = direction } }, requested), requested));
            }
            await FrameAsync();
        }
        var elapsed = wall.Elapsed.TotalSeconds;
        var ticks = latest.Tick - startTick;
        for (var settle = 0; settle < 120 && queued.Any(entry => !results.ContainsKey(entry.Sequence)); settle++)
            await FrameAsync();
        // Gameplay refusals (a blocked tile, an interrupted player) say nothing about timing.
        var timed = queued.Where(entry => results.TryGetValue(entry.Sequence, out var result)
            && (result.Status == CommandStatus.Accepted || result.RejectReason == CommandRejectReason.TooLate)).ToArray();
        var onTime = timed.Count(entry => results[entry.Sequence] is { Status: CommandStatus.Accepted } result
            && result.AppliedTick == entry.Requested);
        static double Percentile(List<double> values, double p)
            => values.Count == 0 ? 0 : values.Order().ElementAt((int)Math.Min(values.Count - 1, Math.Floor(values.Count * p)));
        var pacedRate = 20d * speed;
        var tooLate = queued.Count(entry => results.TryGetValue(entry.Sequence, out var result)
            && result.RejectReason == CommandRejectReason.TooLate);
        var report = $"{speed}x of 20 Hz on level {level} for {elapsed:0.0} s: host {ticks / elapsed:0} ticks/s of {pacedRate}; "
            + $"{admissions.Count} snapshots, admission p50 {Percentile(admissions, 0.5):0.000} ms, p95 {Percentile(admissions, 0.95):0.000} ms, max {admissions.DefaultIfEmpty().Max():0.000} ms; "
            + $"snapshot gap p50 {Percentile(snapshotGaps, 0.5):0.0} ms, p95 {Percentile(snapshotGaps, 0.95):0.0} ms; "
            + $"steps {queued.Count}, timed {timed.Length}, on time {onTime}; others: "
            + string.Join(", ", queued.Where(entry => !results.TryGetValue(entry.Sequence, out var result)
                    || result.Status != CommandStatus.Accepted || result.AppliedTick != entry.Requested)
                .Select(entry => results.TryGetValue(entry.Sequence, out var result)
                    ? $"{result.Status}/{result.RejectReason} +{(long)result.AppliedTick - (long)entry.Requested}"
                    : "unacknowledged")) + ".";
        TestContext.Current.SendDiagnosticMessage(report);
        Console.WriteLine(report);
        Assert.InRange(ticks / elapsed, pacedRate * 0.9, pacedRate * 1.1);
        Assert.True(Percentile(admissions, 0.95) < 1000d / 60, report);
        Assert.Equal(0, tooLate);
        Assert.True(timed.Length >= 5 && onTime >= timed.Length * 0.9, report);
    }

    private static CancellationTokenSource Deadline(int seconds)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    private sealed class Pump(AuthoritativeClient client, CancellationToken cancellationToken)
    {
        private readonly Dictionary<ulong, CommandResult> results = [];
        private readonly AuthoritativeClientModel model = new();
        private Snapshot? snapshot;

        private async Task StepAsync()
        {
            await client.PollAsync(cancellationToken);
            foreach (var message in client.DrainMessages()) {
                Assert.Null(message.Error);
                // Admit through the model: referenced snapshots omit unchanged bodies on the wire.
                client.ApplyMessageToModel(model, message);
                if (message.Snapshot is not null && model.Snapshot is { } admitted
                    && (snapshot is null || admitted.Tick >= snapshot.Tick))
                    snapshot = admitted;
                foreach (var result in message.Acknowledgement?.Results ?? [])
                    results.TryAdd(result.ClientSequence, result);
            }
            await Task.Delay(10, cancellationToken);
        }

        public async Task UntilAsync(Func<bool> done, string failure)
        {
            var limit = Stopwatch.StartNew();
            while (!done()) {
                Assert.True(limit.Elapsed < TimeSpan.FromSeconds(15), failure);
                await StepAsync();
            }
        }

        public async Task<Snapshot> SnapshotAsync(Func<Snapshot, bool> done, string failure)
        {
            snapshot = null;
            await UntilAsync(() => snapshot is not null && done(snapshot), failure);
            return snapshot!;
        }

        public async Task<CommandResult> ResultAsync(ulong sequence, string failure)
        {
            await UntilAsync(() => results.ContainsKey(sequence), failure);
            return results[sequence];
        }
    }
}
