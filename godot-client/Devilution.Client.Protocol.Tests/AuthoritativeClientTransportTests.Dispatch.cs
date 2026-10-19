using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class AuthoritativeClientTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstDispatchFreezesAllCommandBytesAcrossRetryAndReconnect(bool realtime)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var secondRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            Command first;
            using (var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var stream = connection.GetStream();
                await DispatchHandshake(stream, 100);
                first = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
                Assert.Equal(9, first.MoveRequested.TargetX);
                if (realtime)
                    Assert.True(first.RequestedTick >= 101);
                else
                    Assert.Equal(109UL, first.RequestedTick);
                var repeated = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
                Assert.Equal(first.ToByteString(), repeated.ToByteString());
                secondRead.SetResult();
            }
            using (var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var stream = connection.GetStream();
                await DispatchHandshake(stream, 1000);
                var resumed = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
                Assert.Equal(first.ToByteString(), resumed.ToByteString());
                await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                    Results = { new CommandResult { ClientSequence = first.ClientSequence,
                        Status = CommandStatus.Duplicate, AppliedTick = first.RequestedTick } },
                } });
            }
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var command = new Command { MoveRequested = new MoveRequested { TargetCell = true, TargetX = 9, TargetY = 4 } };
        var sequence = realtime ? client.QueueRealtime(command, 100) : client.Queue(command, 109);
        command.MoveRequested.TargetX = 999;
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await secondRead.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await UntilAcknowledged(client);
        Assert.Equal(1UL, sequence);
    }

    [Fact]
    public async Task CancelledPollBeforeDispatchEntryStillSchedulesAtTheLaterDispatch()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.True(command.RequestedTick >= 115);
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PollAsync(cancelled.Token));
        await Task.Delay(750, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConcurrentPollsDoNotDuplicateANewCommand()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var pollsFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await pollsFinished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(150);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                var nextByte = new byte[1];
                var read = await stream.ReadAsync(nextByte, deadline.Token);
                Assert.Fail($"Unexpected transport bytes after one send: {read}");
            });
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener) with {
            MinimumRetryTimeout = TimeSpan.FromSeconds(30), MaximumRetryTimeout = TimeSpan.FromSeconds(30),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(
            () => client.PollAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        pollsFinished.SetResult();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RetransmittedAcknowledgementDoesNotUnderestimateRoundTripTime()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var first = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            var repeated = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.Equal(first, repeated);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = first.ClientSequence, Status = CommandStatus.Accepted } },
            } });
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener) with {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25), MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        var originalTimeout = client.RetryTimeout;
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(originalTimeout + TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await UntilAcknowledged(client);
        Assert.Equal(originalTimeout, client.RetryTimeout);
    }

    [Fact]
    public async Task PollingDuringReconnectWaitsForHandshakeAndUsesTheNewClock()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var helloReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandshake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using (var original = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
                await using var stream = original.GetStream();
                await DispatchHandshake(stream, 100);
            }
            using var resumed = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var resumedStream = resumed.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello, (await ReadEnvelopeAsync(resumedStream)).PayloadCase);
            helloReceived.SetResult();
            await releaseHandshake.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await DispatchHelloAndSnapshot(resumedStream, 1000);
            var command = Assert.Single((await ReadEnvelopeAsync(resumedStream)).CommandBatch.Commands);
            Assert.Equal(1UL, command.ClientSequence);
            Assert.True(command.RequestedTick >= 1002, "An unsent command ignored the replacement clock.");
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        var reconnect = client.ReconnectAsync(TestContext.Current.CancellationToken);
        await helloReceived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var poll = client.PollAsync(TestContext.Current.CancellationToken);
        try {
            Assert.False(poll.IsCompleted, "Poll must wait behind the replacement handshake.");
        } finally {
            releaseHandshake.TrySetResult();
        }
        await Task.WhenAll(reconnect, poll).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RejectedRealtimeCommandIsNotRestampedOrRetried()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var finalPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, 100);
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = command.ClientSequence,
                    Status = CommandStatus.Rejected, RejectReason = CommandRejectReason.TooLate } },
            } });
            await finalPoll.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(150);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                var nextByte = new byte[1];
                var read = await stream.ReadAsync(nextByte, deadline.Token);
                Assert.Fail($"A rejected command emitted new transport bytes: {read}");
            });
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(DispatchOptions(listener));
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await UntilAcknowledged(client);
        var acknowledgement = Assert.Single(client.DrainMessages().Where(message => message.Acknowledgement is not null)).Acknowledgement!;
        Assert.Equal(CommandRejectReason.TooLate, Assert.Single(acknowledgement.Results).RejectReason);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        finalPoll.SetResult();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static ClientConnectionOptions DispatchOptions(TcpListener listener)
        => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "dispatch-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25), MaximumRetryTimeout = TimeSpan.FromMilliseconds(25),
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        };

    private static async Task DispatchHandshake(NetworkStream stream, ulong tick)
    {
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, (await ReadEnvelopeAsync(stream)).PayloadCase);
        await DispatchHelloAndSnapshot(stream, tick);
    }

    private static async Task DispatchHelloAndSnapshot(NetworkStream stream, ulong tick)
    {
        await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
            ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash", SessionToken = "dispatch-session",
            TickRateHz = 20, GameMode = GameMode.SinglePlayer,
        } });
        await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = tick } });
    }

    private static async Task UntilAcknowledged(AuthoritativeClient client)
    {
        for (var attempt = 0; attempt < 100 && client.PendingCommandCount != 0; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
    }
}
