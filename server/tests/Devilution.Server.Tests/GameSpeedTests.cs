using System.Net;
using System.Net.Sockets;
using Devilution.Engine;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

/** Game speed: clock scaling, admission scaling, host options and the negotiated protocol. */
public sealed class GameSpeedTests
{
    [Fact]
    public void SpeedMultiplierScalesThePacedRateWithoutSkippingTicks()
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(20, time);
        time.AdvanceMilliseconds(500);
        Assert.Equal(1UL, clock.CurrentTick); // One read grants at most one 50 ms step.
        time.AdvanceMilliseconds(950);
        Assert.Equal(2UL, clock.CurrentTick);

        Assert.Equal(2UL, clock.SetSpeedMultiplier(5));
        Assert.Equal(5U, clock.SpeedMultiplier);
        Assert.Equal(20U, clock.BaseTickRateHz);
        var ticks = new List<ulong>();
        for (var read = 0; read < 100; read++) {
            time.AdvanceMilliseconds(10);
            ticks.Add(clock.CurrentTick);
        }
        // 1 s at 100 Hz, one tick per 10 ms read, every tick in order.
        Assert.Equal(Enumerable.Range(3, 100).Select(tick => (ulong)tick), ticks);

        // The maximum step is 50 ms of wall time at the paced rate.
        time.AdvanceMilliseconds(10_000);
        Assert.Equal(107UL, clock.CurrentTick);
    }

    [Fact]
    public void SpeedChangeKeepsTheFractionalTickInProgress()
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(20, time);
        time.AdvanceMilliseconds(25); // Half of a 1x tick.
        Assert.Equal(0UL, clock.SetSpeedMultiplier(2));
        time.AdvanceMilliseconds(12); // Half of a 2x tick finishes it.
        Assert.Equal(0UL, clock.CurrentTick);
        time.AdvanceMilliseconds(1);
        Assert.Equal(1UL, clock.CurrentTick);

        Assert.Equal(1UL, clock.SetSpeedMultiplier(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeedMultiplier(0));
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    [InlineData(5U)]
    [InlineData(10U)]
    [InlineData(20U)]
    public void PacedRateIsTheBaseRateTimesTheMultiplier(uint multiplier)
    {
        var time = new ManualClockTimeProvider();
        var clock = new RealtimeAuthoritativeClock(20, time);
        clock.SetSpeedMultiplier(multiplier);
        var previous = clock.CurrentTick;
        for (var read = 0; read < 40; read++) {
            time.AdvanceMilliseconds(50);
            var current = clock.CurrentTick;
            Assert.Equal(previous + multiplier, current);
            previous = current;
        }
        Assert.Equal(40UL * multiplier, previous);
    }

    [Fact]
    public void LiveAdmissionToleranceIsWallTimeAtEverySpeed()
    {
        uint speed = 1;
        var policy = CommandAdmissionPolicy.ForLiveHost(20, () => speed);
        Assert.Equal(40UL, policy.GameplayCriticalLateToleranceTicks);
        Assert.Equal(2UL, policy.NonCriticalLateToleranceTicks);
        var late = new Command { RequestedTick = 100, MoveRequested = new MoveRequested { DirectionX = 1 } };
        Assert.Equal(CommandStatus.Rejected, policy.Evaluate(late, 300).Status);

        speed = 10;
        Assert.Equal(400UL, policy.GameplayCriticalLateToleranceTicks);
        Assert.Equal(20UL, policy.NonCriticalLateToleranceTicks);
        Assert.Equal(CommandStatus.Accepted, policy.Evaluate(late, 300).Status);
        Assert.Equal(40UL, CommandAdmissionPolicy.ForLiveHost(20).GameplayCriticalLateToleranceTicks);
    }

    [Theory]
    [InlineData(null, 1U)]
    [InlineData("1", 1U)]
    [InlineData("20", 20U)]
    public void HostOptionsParseGameSpeed(string? value, uint expected)
    {
        var arguments = value is null ? Array.Empty<string>() : ["--game-speed", value];
        Assert.True(ServerHostOptions.TryParse(arguments, out var options, out var error), error);
        Assert.Equal(expected, options.GameSpeedMultiplier);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("fast")]
    public void HostOptionsRejectInvalidGameSpeed(string value)
        => Assert.False(ServerHostOptions.TryParse(["--game-speed", value], out _, out _));

    [Fact]
    public void SpeedStepsCoverOneToTwentyTimes()
    {
        Assert.Equal([1U, 2U, 3U, 5U, 10U, 20U], GameSpeedLimits.Steps);
        Assert.Equal(GameSpeedLimits.Maximum, GameSpeedLimits.Steps[^1]);
        Assert.Equal(1U, GameSpeedLimits.FromWire(0));
    }

    [Fact]
    public async Task SinglePlayerSessionNegotiatesAndChangesSpeed()
    {
        await using var host = Host.Start(AuthoritativeGameMode.SinglePlayer);
        using var timeout = Timeout();
        await using var session = await host.ConnectAsync("speed-single", supportsGameSpeed: true, timeout.Token);
        Assert.True(session.Hello.SupportsGameSpeed);
        Assert.True(session.Hello.GameSpeedControllable);
        Assert.Equal(1U, session.Hello.GameSpeedMultiplier);
        Assert.Equal(20U, session.Hello.TickRateHz);

        host.Time.AdvanceMilliseconds(50);
        var ack = await session.RequestSpeedAsync(7, 5, timeout.Token);
        Assert.True(ack.Accepted);
        Assert.Equal(7UL, ack.RequestId);
        Assert.Equal(5U, ack.Multiplier);
        Assert.Equal(GameSpeedRejectReason.Unspecified, ack.RejectReason);
        Assert.True(ack.EffectiveTick >= 1);
        Assert.Equal(5U, host.Clock.SpeedMultiplier);

        var rejected = await session.RequestSpeedAsync(8, 21, timeout.Token);
        Assert.False(rejected.Accepted);
        Assert.Equal(GameSpeedRejectReason.OutOfRange, rejected.RejectReason);
        Assert.Equal(5U, rejected.Multiplier);
        Assert.Equal(5U, host.Clock.SpeedMultiplier);
    }

    [Fact]
    public async Task SinglePlayerSpeedEndsWithTheConnectionThatSetIt()
    {
        await using var host = Host.Start(AuthoritativeGameMode.SinglePlayer);
        using var timeout = Timeout();
        await using (var session = await host.ConnectAsync("speed-owner", supportsGameSpeed: true, timeout.Token))
            Assert.True((await session.RequestSpeedAsync(1, 10, timeout.Token)).Accepted);
        await WaitUntilAsync(() => host.Clock.SpeedMultiplier == 1, timeout.Token);

        // The next session starts at the host default, so an older client is accepted at 1x.
        await using var legacy = await host.ConnectAsync("speed-owner", supportsGameSpeed: false, timeout.Token);
        Assert.False(legacy.Hello.SupportsGameSpeed);
        Assert.Equal(0U, legacy.Hello.GameSpeedMultiplier);
    }

    [Fact]
    public async Task MultiplayerSpeedIsHostControlled()
    {
        await using var host = Host.Start(AuthoritativeGameMode.Multiplayer, defaultSpeed: 3);
        using var timeout = Timeout();
        Assert.Equal(3U, host.Clock.SpeedMultiplier);
        await using var session = await host.ConnectAsync("speed-multi", supportsGameSpeed: true, timeout.Token);
        Assert.True(session.Hello.SupportsGameSpeed);
        Assert.False(session.Hello.GameSpeedControllable);
        Assert.Equal(3U, session.Hello.GameSpeedMultiplier);

        var ack = await session.RequestSpeedAsync(1, 20, timeout.Token);
        Assert.False(ack.Accepted);
        Assert.Equal(GameSpeedRejectReason.HostControlled, ack.RejectReason);
        Assert.Equal(3U, ack.Multiplier);
        Assert.Equal(3U, host.Clock.SpeedMultiplier);
    }

    [Fact]
    public async Task HostAboveOneTimesRefusesClientsWithoutGameSpeed()
    {
        await using var host = Host.Start(AuthoritativeGameMode.Multiplayer, defaultSpeed: 2);
        using var timeout = Timeout();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, host.Server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, Hello("speed-legacy", supportsGameSpeed: false), timeout.Token);
        var response = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
        Assert.Equal(Envelope.PayloadOneofCase.Error, response!.PayloadCase);
        Assert.Equal(ProtocolErrorCode.UnsupportedSchema, response.Error.Code);
    }

    [Fact]
    public async Task ImmediateHostDoesNotOfferGameSpeed()
    {
        await using var host = Host.Start(AuthoritativeGameMode.SinglePlayer, clockGated: false);
        using var timeout = Timeout();
        await using var session = await host.ConnectAsync("speed-immediate", supportsGameSpeed: true, timeout.Token);
        Assert.False(session.Hello.SupportsGameSpeed);
        Assert.False(session.Hello.GameSpeedControllable);
    }

    private static Envelope Hello(string token, bool supportsGameSpeed) => new() {
        ClientHello = new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            ResumeToken = token,
            SupportsClockGatedCommands = true,
            SupportsGameSpeed = supportsGameSpeed,
        },
    };

    private static CancellationTokenSource Timeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
            await Task.Delay(10, cancellationToken);
    }

    private sealed class Host : IAsyncDisposable
    {
        public required AuthoritativeTcpServer Server { get; init; }
        public required RealtimeAuthoritativeClock Clock { get; init; }
        public required ManualClockTimeProvider Time { get; init; }

        public static Host Start(AuthoritativeGameMode mode, uint defaultSpeed = 1, bool clockGated = true)
        {
            var time = new ManualClockTimeProvider();
            var clock = new RealtimeAuthoritativeClock(20, time);
            var world = new StaticWorld();
            var commands = new AuthoritativeCommandServer(world, CommandAdmissionPolicy.ForLiveHost(20, () => clock.SpeedMultiplier));
            var server = new AuthoritativeTcpServer(
                commands,
                new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20) {
                    GameMode = mode,
                }),
                (Devilution.Server.Simulation.IAuthoritativeClock)clock,
                snapshotProvider: world) {
                UseClockGatedCommandScheduling = clockGated,
                DefaultGameSpeedMultiplier = defaultSpeed,
            };
            server.Start();
            return new Host { Server = server, Clock = clock, Time = time };
        }

        public async Task<Session> ConnectAsync(string token, bool supportsGameSpeed, CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, Server.Port, cancellationToken);
            var stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, Hello(token, supportsGameSpeed), cancellationToken);
            var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
            var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
            return new Session(client, stream, hello.ServerHello);
        }

        public ValueTask DisposeAsync() => Server.DisposeAsync();
    }

    private sealed class Session(TcpClient client, NetworkStream stream, ServerHello hello) : IAsyncDisposable
    {
        public ServerHello Hello { get; } = hello;

        public async Task<GameSpeedAck> RequestSpeedAsync(ulong requestId, uint multiplier, CancellationToken cancellationToken)
        {
            await EnvelopeCodec.WriteAsync(stream, new Envelope {
                GameSpeedRequest = new GameSpeedRequest { RequestId = requestId, Multiplier = multiplier },
            }, cancellationToken);
            while (true) {
                var response = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
                Assert.NotNull(response);
                Assert.NotEqual(Envelope.PayloadOneofCase.Error, response.PayloadCase);
                if (response.PayloadCase == Envelope.PayloadOneofCase.GameSpeedAck)
                    return response.GameSpeedAck;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
            client.Dispose();
        }
    }

    private sealed class StaticWorld : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
    {
        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() { Tick = tick, Players = { new PlayerSnapshot { EntityId = entityId } } };
    }
}
