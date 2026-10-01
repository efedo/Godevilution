using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Transport-only coverage for the negotiated clock-gated command mode. */
public sealed class DeferredCommandTimingTests
{
    [Fact]
    public async Task FutureAcknowledgementDoesNotCauseRoutinePrematureRetry()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var received = 0;
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, CommandSchedulingMode.ClockGated, 100);
            var readinessProbe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(readinessProbe.TimingRequestId > 0);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = readinessProbe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Interlocked.Increment(ref received);
            Assert.True(command.RequestedTick >= 103);
            using var duplicateCancellation = new CancellationTokenSource();
            var duplicateRead = ReadEnvelopeAsync(stream, duplicateCancellation.Token);
            var ackDelay = Task.Delay(120, TestContext.Current.CancellationToken);
            var completed = await Task.WhenAny(duplicateRead, ackDelay);
            Assert.NotSame(duplicateRead, completed);
            duplicateCancellation.Cancel();
            try {
                await duplicateRead;
            } catch (OperationCanceledException) {
            }
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = Accepted(command.ClientSequence) });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(20),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(40),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 30 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, received);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task GatedCommandAcknowledgementDoesNotUpdateRttButTimingReplyDoes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var timingRequestSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTiming = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, CommandSchedulingMode.ClockGated, 100);
            var readinessProbe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(readinessProbe.TimingRequestId > 0);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = readinessProbe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = Accepted(command.ClientSequence) });
            var request = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(request.TimingRequestId > 0);
            timingRequestSeen.SetResult();
            await releaseTiming.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 101 } });
            await Task.Delay(150, TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = request.TimingRequestId },
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(20),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(500),
            SnapshotPollInterval = TimeSpan.FromMilliseconds(1),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initialRetryTimeout = client.RetryTimeout;
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 30 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await client.PollAsync(TestContext.Current.CancellationToken);
        await timingRequestSeen.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(initialRetryTimeout, client.RetryTimeout);
        releaseTiming.SetResult();
        for (var attempt = 0; attempt < 40 && client.RetryTimeout <= initialRetryTimeout; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(client.RetryTimeout > initialRetryTimeout);
    }

    [Fact]
    public async Task TimingProbeCorrelationAndExpiryIgnoreOldReplies()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, CommandSchedulingMode.ClockGated, 100);
            var firstRequest = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(firstRequest.TimingRequestId > 0);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = firstRequest.TimingRequestId + 1 },
            });
            var secondRequest = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(secondRequest.TimingRequestId > firstRequest.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = firstRequest.TimingRequestId },
            });
            await Task.Delay(600, TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = secondRequest.TimingRequestId },
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(20),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(500),
            SnapshotPollInterval = TimeSpan.FromMilliseconds(1),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await Task.Delay(10, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 350 && !serverTask.IsCompleted; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(client.RetryTimeout > TimeSpan.FromMilliseconds(400));
    }

    [Fact]
    public async Task GatedReconnectResendsTheFrozenCommandBytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstCommandSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            byte[] firstBytes;
            using (var firstConnection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var firstStream = firstConnection.GetStream();
                await DispatchHandshake(firstStream, CommandSchedulingMode.ClockGated, 100, "resume");
                var readinessProbe = (await ReadEnvelopeAsync(firstStream)).SnapshotRequest;
                Assert.True(readinessProbe.TimingRequestId > 0);
                await WriteEnvelopeAsync(firstStream, new Envelope {
                    TimingReply = new TimingReply { RequestId = readinessProbe.TimingRequestId },
                });
                firstBytes = (await ReadEnvelopeAsync(firstStream)).CommandBatch.Commands.Single().ToByteArray();
                firstCommandSeen.SetResult();
            }

            using var resumedConnection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var resumedStream = resumedConnection.GetStream();
            await DispatchHandshake(resumedStream, CommandSchedulingMode.ClockGated, 1000, "resume");
            var resumed = Assert.Single((await ReadEnvelopeAsync(resumedStream)).CommandBatch.Commands);
            Assert.Equal(firstBytes, resumed.ToByteArray());
            await WriteEnvelopeAsync(resumedStream, new Envelope { CommandAck = Accepted(resumed.ClientSequence) });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 30 && !firstCommandSeen.Task.IsCompleted; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }
        await firstCommandSeen.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownSchedulingModeIsRejectedBeforeInitialSnapshot()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = "content-hash",
                    SessionToken = "unknown-mode",
                    TickRateHz = 20,
                    GameMode = GameMode.SinglePlayer,
                    CommandSchedulingMode = (CommandSchedulingMode)99,
                },
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LegacyModeKeepsSnapshotTimingIdZero()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, CommandSchedulingMode.Immediate, 100);
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = Accepted(command.ClientSequence) });
            var request = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.Equal(0UL, request.TimingRequestId);
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with {
            SnapshotPollInterval = TimeSpan.FromMilliseconds(1),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static ClientConnectionOptions Options(TcpListener listener)
        => new(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "timing-test",
            "0.1.0",
            "content-hash") {
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        };

    private static async Task DispatchHandshake(
        NetworkStream stream,
        CommandSchedulingMode mode,
        ulong tick,
        string sessionToken = "timing-session")
    {
        var clientHello = await ReadEnvelopeAsync(stream);
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, clientHello.PayloadCase);
        Assert.True(clientHello.ClientHello.SupportsClockGatedCommands);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                SessionToken = sessionToken,
                TickRateHz = 20,
                GameMode = GameMode.SinglePlayer,
                CommandSchedulingMode = mode,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = tick } });
    }

    private static CommandAck Accepted(ulong clientSequence)
        => new() { Results = { new CommandResult {
            ClientSequence = clientSequence,
            Status = CommandStatus.Accepted,
        } } };

    private static async Task<Envelope> ReadEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken = default)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}
