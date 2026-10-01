using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Ordered loopback coverage for clock-gated startup readiness. */
public sealed class InitialTimingCalibrationTests
{
    [Fact]
    public async Task RealtimeCommandWaitsForTheCurrentTimingReply()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var probeSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var probe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(probe.TimingRequestId > 0);
            probeSeen.SetResult();

            using var noCommand = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            noCommand.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ReadEnvelopeAsync(stream, noCommand.Token));

            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = 0 },
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.TimingRequestId + 1 },
            });
            using var noCommandAfterInvalidReplies = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            noCommandAfterInvalidReplies.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ReadEnvelopeAsync(stream, noCommandAfterInvalidReplies.Token));
            await Task.Delay(150, TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.Equal(1UL, command.ClientSequence);
            Assert.True(command.RequestedTick >= 104);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(command.ClientSequence),
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initialRetryTimeout = client.RetryTimeout;
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await probeSeen.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
        Assert.True(client.RetryTimeout > initialRetryTimeout);
    }

    [Fact]
    public async Task ExplicitQueueKeepsItsRequestedTickWhenRealtimeCalibrationIsPending()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var probe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(probe.TimingRequestId > 0);

            var explicitCommand = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.Equal(2UL, explicitCommand.ClientSequence);
            Assert.Equal(777UL, explicitCommand.RequestedTick);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(explicitCommand.ClientSequence),
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.TimingRequestId },
            });

            var realtimeCommand = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.Equal(1UL, realtimeCommand.ClientSequence);
            Assert.True(realtimeCommand.RequestedTick >= 103);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(realtimeCommand.ClientSequence),
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        client.Queue(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            777);
        await client.PollAsync(TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task PreviousEpochReplyCannotReleaseTheReplacementReadinessGate()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            ulong firstRequestId;
            using (var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var stream = connection.GetStream();
                await DispatchHandshake(stream, 100, "resume");
                firstRequestId = (await ReadEnvelopeAsync(stream)).SnapshotRequest.TimingRequestId;
                Assert.True(firstRequestId > 0);
            }

            using var resumed = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var resumedStream = resumed.GetStream();
            await DispatchHandshake(resumedStream, 200, "resume");
            var replacementProbe = (await ReadEnvelopeAsync(resumedStream)).SnapshotRequest;
            Assert.True(replacementProbe.TimingRequestId > firstRequestId);
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                TimingReply = new TimingReply { RequestId = firstRequestId },
            });
            using var noCommand = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            noCommand.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ReadEnvelopeAsync(resumedStream, noCommand.Token));
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                TimingReply = new TimingReply { RequestId = replacementProbe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(resumedStream)).CommandBatch.Commands);
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                CommandAck = Accepted(command.ClientSequence),
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with { ResumeToken = "resume" });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task ExpiredReplyIsIgnoredBeforeAReplacementProbeCanReleaseTheCommand()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var firstProbe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            await Task.Delay(2100, TestContext.Current.CancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = firstProbe.TimingRequestId },
            });
            var replacementProbe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            Assert.True(replacementProbe.TimingRequestId > firstProbe.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = replacementProbe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(command.ClientSequence),
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(2200, TestContext.Current.CancellationToken);

        await client.PollAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task CancelledPollLeavesReadinessIntentForALaterPoll()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var probe = (await ReadEnvelopeAsync(stream)).SnapshotRequest;
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.TimingRequestId },
            });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(command.ClientSequence),
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PollAsync(cancelled.Token));

        await client.PollAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task CalibrationTimeoutClosesTransportAndRetainsCommandsForReconnect()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            var probeCount = 0;
            using (var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var stream = connection.GetStream();
                await DispatchHandshake(stream, 100, "resume");
                try {
                    while (true) {
                        var envelope = await ReadEnvelopeAsync(stream, TestContext.Current.CancellationToken);
                        Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, envelope.PayloadCase);
                        Assert.True(envelope.SnapshotRequest.TimingRequestId > 0);
                        probeCount++;
                    }
                } catch (EndOfStreamException) {
                } catch (IOException) {
                } catch (ObjectDisposedException) {
                }
            }
            Assert.Equal(3, probeCount);

            using var resumed = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var resumedStream = resumed.GetStream();
            await DispatchHandshake(resumedStream, 200, "resume");
            var probe = (await ReadEnvelopeAsync(resumedStream)).SnapshotRequest;
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.TimingRequestId },
            });
            var commands = new List<Command>();
            for (var index = 0; index < 3; index++)
                commands.Add(Assert.Single((await ReadEnvelopeAsync(resumedStream)).CommandBatch.Commands));
            Assert.Equal(new[] { 1UL, 2UL, 3UL }, commands.Select(command => command.ClientSequence));
            Assert.All(commands, command => Assert.True(command.RequestedTick >= 203));
            var acknowledgement = new CommandAck();
            acknowledgement.Results.Add(commands.Select(command => new CommandResult {
                ClientSequence = command.ClientSequence,
                Status = CommandStatus.Accepted,
            }));
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                CommandAck = acknowledgement,
            });
        }, TestContext.Current.CancellationToken);

        await using var client = new AuthoritativeClient(Options(listener) with {
            ResumeToken = "resume",
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        await client.PollAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TimeoutException>(async () => {
            for (var attempt = 0; attempt < 100; attempt++) {
                if (attempt == 25 || attempt == 45)
                    client.QueueRealtime(
                        new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
                        100);
                await Task.Delay(100, TestContext.Current.CancellationToken);
                await client.PollAsync(TestContext.Current.CancellationToken);
            }
        });
        Assert.False(client.IsConnected);
        Assert.Equal(3, client.PendingCommandCount);
        Assert.Contains(
            client.DrainMessages(),
            message => message.Error?.Detail.Contains("bounded probe deadline", StringComparison.Ordinal) == true);

        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await client.PollAsync(TestContext.Current.CancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }

    private static ClientConnectionOptions Options(TcpListener listener)
        => new(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "timing-calibration-test",
            "0.1.0",
            "content-hash") {
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        };

    private static async Task DispatchHandshake(
        NetworkStream stream,
        ulong tick,
        string sessionToken = "timing-calibration-session")
    {
        var hello = await ReadEnvelopeAsync(stream);
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, hello.PayloadCase);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                SessionToken = sessionToken,
                TickRateHz = 20,
                GameMode = GameMode.SinglePlayer,
                CommandSchedulingMode = CommandSchedulingMode.ClockGated,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot { Tick = tick },
        });
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
        CancellationToken cancellationToken)
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
