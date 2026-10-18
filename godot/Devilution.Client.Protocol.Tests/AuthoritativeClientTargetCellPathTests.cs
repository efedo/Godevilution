using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientTargetCellPathTests
{
    [Fact]
    public async Task NegotiatedRealtimeTargetMoveKeepsPathFlagAcrossRetryAndReconnect()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstCommandsRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            Command original;
            using (var connection = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = connection.GetStream();
                await DispatchHandshakeAsync(stream, supportsTargetCellPathing: true, 100, timeout.Token);
                original = Assert.Single((await ReadAsync(stream, timeout.Token)).CommandBatch.Commands);
                Assert.True(original.MoveRequested.FollowPath);
                var retry = Assert.Single((await ReadAsync(stream, timeout.Token)).CommandBatch.Commands);
                Assert.Equal(original.ToByteString(), retry.ToByteString());
                firstCommandsRead.TrySetResult();
            }

            using var resumed = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumed.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello, (await ReadAsync(resumedStream, timeout.Token)).PayloadCase);
            await DispatchHelloAndSnapshotAsync(resumedStream, supportsTargetCellPathing: true, 1000, timeout.Token);
            var retriedAfterReconnect = Assert.Single((await ReadAsync(resumedStream, timeout.Token)).CommandBatch.Commands);
            Assert.Equal(original.ToByteString(), retriedAfterReconnect.ToByteString());
            await WriteAsync(resumedStream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult {
                    ClientSequence = original.ClientSequence,
                    Status = CommandStatus.Duplicate,
                    AppliedTick = original.RequestedTick,
                } },
            } }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(timeout.Token);
        Assert.True(client.SupportsTargetCellPathing);
        var callerCommand = TargetMove();
        var sequence = client.QueueRealtime(callerCommand, observedTick: 100);
        Assert.False(callerCommand.MoveRequested.FollowPath);
        await client.PollAsync(timeout.Token);
        await Task.Delay(60, timeout.Token);
        await client.PollAsync(timeout.Token);
        await firstCommandsRead.Task.WaitAsync(timeout.Token);
        await client.ReconnectAsync(timeout.Token);
        Assert.True(client.SupportsTargetCellPathing);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
        await UntilAcknowledgedAsync(client, timeout.Token);
        Assert.Equal(1UL, sequence);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task OnlyNegotiatedRealtimeTargetMovesGainPathFlag(
        bool serverSupportsTargetCellPathing,
        bool expectedRealtimePathFlag)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(
                stream,
                serverSupportsTargetCellPathing,
                100,
                timeout.Token);
            var commands = new Command[3];
            for (var index = 0; index < commands.Length; index++) {
                var envelope = await ReadAsync(stream, timeout.Token);
                Assert.NotNull(envelope);
                Assert.Equal(Envelope.PayloadOneofCase.CommandBatch, envelope!.PayloadCase);
                commands[index] = Assert.Single(envelope.CommandBatch.Commands);
            }
            Assert.Collection(commands,
                realtime => {
                    Assert.Equal(1UL, realtime.ClientSequence);
                    Assert.Equal(expectedRealtimePathFlag, realtime.MoveRequested.FollowPath);
                },
                explicitCommand => {
                    Assert.Equal(2UL, explicitCommand.ClientSequence);
                    Assert.False(explicitCommand.MoveRequested.FollowPath);
                },
                directional => {
                    Assert.Equal(3UL, directional.ClientSequence);
                    Assert.False(directional.MoveRequested.TargetCell);
                    Assert.False(directional.MoveRequested.FollowPath);
                });
            await WriteAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = {
                    new CommandResult { ClientSequence = commands[0].ClientSequence, Status = CommandStatus.Accepted },
                    new CommandResult { ClientSequence = commands[1].ClientSequence, Status = CommandStatus.Accepted },
                    new CommandResult { ClientSequence = commands[2].ClientSequence, Status = CommandStatus.Accepted },
                },
            } }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(timeout.Token);
        var realtimeCommand = TargetMove();
        client.QueueRealtime(realtimeCommand, observedTick: 100);
        client.Queue(TargetMove(), requestedTick: 109);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, observedTick: 100);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
        await UntilAcknowledgedAsync(client, timeout.Token);
        Assert.Equal(serverSupportsTargetCellPathing, client.SupportsTargetCellPathing);
        Assert.False(realtimeCommand.MoveRequested.FollowPath);
    }

    [Fact]
    public async Task ReconnectFailsWhenPendingPathMoveIsNotSupportedByNewPeer()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using (var original = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = original.GetStream();
                await DispatchHandshakeAsync(stream, supportsTargetCellPathing: true, 100, timeout.Token);
                var command = Assert.Single((await ReadAsync(stream, timeout.Token)).CommandBatch.Commands);
                Assert.True(command.MoveRequested.FollowPath);
            }

            using var resumed = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumed.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello, (await ReadAsync(resumedStream, timeout.Token)).PayloadCase);
            await WriteAsync(resumedStream, new Envelope { ServerHello = Hello(supportsTargetCellPathing: false) }, timeout.Token);
            using var eofTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            eofTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            var trailing = await ReadAsync(resumedStream, eofTimeout.Token);
            Assert.Null(trailing);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(TargetMove(), observedTick: 100);
        await client.PollAsync(timeout.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ReconnectAsync(timeout.Token));
        await serverTask.WaitAsync(timeout.Token);
        Assert.False(client.SupportsTargetCellPathing);
        Assert.Equal(1, client.PendingCommandCount);
    }

    [Fact]
    public void PathCapabilityAndRequestFieldsRoundTripAsAdditiveProtocolFields()
    {
        var envelope = new Envelope {
            ServerHello = Hello(supportsTargetCellPathing: true),
        };
        envelope.ServerHello.SupportsClockOnlyProbes = true;
        envelope.ServerHello.CommandSchedulingMode = CommandSchedulingMode.ClockGated;
        var request = new Command {
            MoveRequested = new MoveRequested {
                TargetCell = true,
                TargetX = 12,
                TargetY = 34,
                FollowPath = true,
            },
        };

        var parsedHello = Envelope.Parser.ParseFrom(envelope.ToByteArray()).ServerHello;
        var parsedMove = Command.Parser.ParseFrom(request.ToByteArray()).MoveRequested;

        Assert.True(parsedHello.SupportsTargetCellPathing);
        Assert.True(parsedHello.SupportsClockOnlyProbes);
        Assert.Equal(CommandSchedulingMode.ClockGated, parsedHello.CommandSchedulingMode);
        Assert.True(parsedMove.TargetCell);
        Assert.Equal((12, 34), (parsedMove.TargetX, parsedMove.TargetY));
        Assert.True(parsedMove.FollowPath);
    }

    private static ClientConnectionOptions Options(TcpListener listener)
        => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "target-path-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(25),
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        };

    private static Command TargetMove()
        => new() { MoveRequested = new MoveRequested { TargetCell = true, TargetX = 8, TargetY = 12 } };

    private static ServerHello Hello(bool supportsTargetCellPathing)
        => new() {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            SessionToken = "target-path-session",
            TickRateHz = 20,
            GameMode = GameMode.SinglePlayer,
            SupportsTargetCellPathing = supportsTargetCellPathing,
        };

    private static async Task DispatchHandshakeAsync(
        NetworkStream stream,
        bool supportsTargetCellPathing,
        ulong tick,
        CancellationToken cancellationToken)
    {
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, (await ReadAsync(stream, cancellationToken)).PayloadCase);
        await DispatchHelloAndSnapshotAsync(stream, supportsTargetCellPathing, tick, cancellationToken);
    }

    private static async Task DispatchHelloAndSnapshotAsync(
        NetworkStream stream,
        bool supportsTargetCellPathing,
        ulong tick,
        CancellationToken cancellationToken)
    {
        await WriteAsync(stream, new Envelope { ServerHello = Hello(supportsTargetCellPathing) }, cancellationToken);
        await WriteAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = tick } }, cancellationToken);
    }

    private static ValueTask<Envelope?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken)
        => EnvelopeCodec.ReadAsync(stream, cancellationToken);

    private static ValueTask WriteAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, envelope, cancellationToken);

    private static async Task UntilAcknowledgedAsync(AuthoritativeClient client, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100 && client.PendingCommandCount != 0; attempt++)
            await Task.Delay(10, cancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }
}
