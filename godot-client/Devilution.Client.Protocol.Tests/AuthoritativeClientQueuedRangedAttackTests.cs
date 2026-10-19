using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientQueuedRangedAttackTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task OnlyNegotiatedRealtimeAttacksGainQueueFlagAndKeepCallerClone(
        bool serverSupportsQueuedRangedAttacks,
        bool expectedRealtimeFlag)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, serverSupportsQueuedRangedAttacks, 100, timeout.Token);

            var realtimeEnvelope = await ReadAsync(stream, timeout.Token);
            Assert.NotNull(realtimeEnvelope);
            Assert.Equal(Envelope.PayloadOneofCase.CommandBatch, realtimeEnvelope!.PayloadCase);
            var realtime = Assert.Single(realtimeEnvelope.CommandBatch.Commands);
            Assert.Equal(1UL, realtime.ClientSequence);
            Assert.Equal(0x70000011U, realtime.AttackRequested.TargetEntityId);
            Assert.Equal(expectedRealtimeFlag, realtime.AttackRequested.QueueDuringMove);

            var explicitEnvelope = await ReadAsync(stream, timeout.Token);
            Assert.NotNull(explicitEnvelope);
            Assert.Equal(Envelope.PayloadOneofCase.CommandBatch, explicitEnvelope!.PayloadCase);
            var explicitCommand = Assert.Single(explicitEnvelope.CommandBatch.Commands);
            Assert.Equal(2UL, explicitCommand.ClientSequence);
            Assert.False(explicitCommand.AttackRequested.QueueDuringMove);

            await WriteAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = {
                    new CommandResult { ClientSequence = realtime.ClientSequence, Status = CommandStatus.Accepted },
                    new CommandResult { ClientSequence = explicitCommand.ClientSequence, Status = CommandStatus.Accepted },
                },
            } }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener)) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(timeout.Token);
        Assert.Equal(serverSupportsQueuedRangedAttacks, client.SupportsQueuedRangedAttacks);
        Assert.Equal(serverSupportsQueuedRangedAttacks,
            client.InitialConnectionObservation?.ServerHello?.SupportsQueuedRangedAttacks);
        var realtimeCommand = AttackCommand(0x70000011);
        client.QueueRealtime(realtimeCommand, observedTick: 100);
        Assert.False(realtimeCommand.AttackRequested.QueueDuringMove);
        realtimeCommand.AttackRequested.TargetEntityId = 0x70000022;
        client.Queue(AttackCommand(0x70000033), requestedTick: 109);

        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
        await UntilAcknowledgedAsync(client, timeout.Token);

        var dispatches = recorder.CaptureReport().Commands.OrderBy(item => item.ClientSequence).ToArray();
        Assert.Equal(2, dispatches.Length);
        Assert.Equal(expectedRealtimeFlag, dispatches[0].AttackQueueDuringMove);
        Assert.False(dispatches[1].AttackQueueDuringMove);
    }

    [Fact]
    public async Task NegotiatedQueuedAttackRetryRetainsExactFlagAndPayloadAcrossReconnect()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstCommandRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            ByteString original;
            using (var connection = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = connection.GetStream();
                await DispatchHandshakeAsync(stream, supportsQueuedRangedAttacks: true, 100, timeout.Token);
                var command = Assert.Single((await ReadAsync(stream, timeout.Token))!.CommandBatch.Commands);
                Assert.True(command.AttackRequested.QueueDuringMove);
                original = command.ToByteString();
                firstCommandRead.TrySetResult();
            }

            using var resumed = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumed.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
                (await ReadAsync(resumedStream, timeout.Token))!.PayloadCase);
            await DispatchHelloAndSnapshotAsync(resumedStream, supportsQueuedRangedAttacks: true, 1000, timeout.Token);
            var retry = Assert.Single((await ReadAsync(resumedStream, timeout.Token))!.CommandBatch.Commands);
            Assert.Equal(original, retry.ToByteString());
            await WriteAsync(resumedStream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult {
                    ClientSequence = retry.ClientSequence,
                    Status = CommandStatus.Duplicate,
                    AppliedTick = retry.RequestedTick,
                } },
            } }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(AttackCommand(0x70000044), observedTick: 100);
        await client.PollAsync(timeout.Token);
        await firstCommandRead.Task.WaitAsync(timeout.Token);
        await client.ReconnectAsync(timeout.Token);
        Assert.True(client.SupportsQueuedRangedAttacks);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
        await UntilAcknowledgedAsync(client, timeout.Token);
    }

    [Fact]
    public async Task ReconnectFailsWhenPendingQueuedAttackIsUnsupportedByNewPeer()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstCommandRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using (var connection = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = connection.GetStream();
                await DispatchHandshakeAsync(stream, supportsQueuedRangedAttacks: true, 100, timeout.Token);
                var command = Assert.Single((await ReadAsync(stream, timeout.Token))!.CommandBatch.Commands);
                Assert.True(command.AttackRequested.QueueDuringMove);
                firstCommandRead.TrySetResult();
            }

            using var resumed = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumed.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
                (await ReadAsync(resumedStream, timeout.Token))!.PayloadCase);
            await WriteAsync(resumedStream, new Envelope {
                ServerHello = Hello(supportsQueuedRangedAttacks: false),
            }, timeout.Token);
            using var eofTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            eofTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            Assert.Null(await ReadAsync(resumedStream, eofTimeout.Token));
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(AttackCommand(0x70000055), observedTick: 100);
        await client.PollAsync(timeout.Token);
        await firstCommandRead.Task.WaitAsync(timeout.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ReconnectAsync(timeout.Token));
        await serverTask.WaitAsync(timeout.Token);
        Assert.False(client.SupportsQueuedRangedAttacks);
        Assert.Equal(1, client.PendingCommandCount);
    }

    [Fact]
    public void CapabilityAndQueueFlagRoundTripAsAdditiveProtocolFields()
    {
        var hello = Hello(supportsQueuedRangedAttacks: true);
        var attack = AttackCommand(0x70000066);
        attack.AttackRequested.QueueDuringMove = true;

        var parsedHello = ServerHello.Parser.ParseFrom(hello.ToByteArray());
        var parsedAttack = AttackRequested.Parser.ParseFrom(attack.AttackRequested.ToByteArray());

        Assert.True(parsedHello.SupportsQueuedRangedAttacks);
        Assert.Equal(0x70000066U, parsedAttack.TargetEntityId);
        Assert.True(parsedAttack.QueueDuringMove);
    }

    private static ClientConnectionOptions Options(TcpListener listener)
        => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "queued-ranged-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(25),
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        };

    private static Command AttackCommand(uint targetEntityId)
        => new() { AttackRequested = new AttackRequested { TargetEntityId = targetEntityId } };

    private static ServerHello Hello(bool supportsQueuedRangedAttacks)
        => new() {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            SessionToken = "queued-ranged-session",
            TickRateHz = 20,
            GameMode = GameMode.SinglePlayer,
            SupportsQueuedRangedAttacks = supportsQueuedRangedAttacks,
        };

    private static async Task DispatchHandshakeAsync(
        NetworkStream stream,
        bool supportsQueuedRangedAttacks,
        ulong tick,
        CancellationToken cancellationToken)
    {
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
            (await ReadAsync(stream, cancellationToken))!.PayloadCase);
        await DispatchHelloAndSnapshotAsync(stream, supportsQueuedRangedAttacks, tick, cancellationToken);
    }

    private static async Task DispatchHelloAndSnapshotAsync(
        NetworkStream stream,
        bool supportsQueuedRangedAttacks,
        ulong tick,
        CancellationToken cancellationToken)
    {
        await WriteAsync(stream, new Envelope {
            ServerHello = Hello(supportsQueuedRangedAttacks),
        }, cancellationToken);
        await WriteAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = tick } }, cancellationToken);
    }

    private static ValueTask<Envelope?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken)
        => EnvelopeCodec.ReadAsync(stream, cancellationToken);

    private static ValueTask WriteAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, envelope, cancellationToken);

    private static async Task UntilAcknowledgedAsync(
        AuthoritativeClient client,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100 && client.PendingCommandCount != 0; attempt++)
            await Task.Delay(10, cancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }
}
