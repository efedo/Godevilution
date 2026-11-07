using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ResponsiveMovementTransportTests
{
    [Fact]
    public async Task MovementDispatchDoesNotWaitForAParkedPollOrPostAckSnapshot()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var peer = new Peer(timeout.Token);
        var time = new ManualTimeProvider();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = peer.Client(time, recorder);
        await client.ConnectAsync(timeout.Token);
        time.AdvanceMilliseconds(300);
        await client.PollAsync(timeout.Token);
        await peer.Next(Envelope.PayloadOneofCase.SnapshotRequest);
        await Until(() => recorder.CaptureReport().ProbeOutcomes.Count != 0, timeout.Token);
        var first = client.QueueRealtimeMovement(Move(55), 100);
        time.AdvanceMilliseconds(5);
        await client.PollAsync(timeout.Token);
        var sent = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(first, sent.ClientSequence);
        var trace = Assert.Single(recorder.CaptureReport().Commands);
        Assert.Equal(5, trace.InputToDispatchMilliseconds);
        Assert.Equal(1, trace.SchedulingLeadTicks);
        Assert.Equal(0, trace.SchedulingRoundTripMilliseconds);
        Assert.True(trace.LocalScheduling);
        Assert.Equal(100, trace.ObservedRoundTripEstimateMilliseconds); // Retry/probe floor is unchanged.
        Assert.Equal(101UL, sent.RequestedTick); // The next tick; elapsed fractions do not add a second tick.
        await peer.Send(new Envelope { CommandAck = new CommandAck { Results = { new CommandResult {
            ClientSequence = first, AppliedTick = sent.RequestedTick, Status = CommandStatus.Accepted } } } });
        await Until(() => client.PendingCommandCount == 0, timeout.Token);
        var second = client.QueueRealtimeMovement(Move(49), 100);
        await client.PollAsync(timeout.Token);
        var next = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(second, next.ClientSequence);
        Assert.True(second > first);
        Assert.Equal(1, peer.SnapshotRequests); // The first requested snapshot is still parked.
    }

    [Fact]
    public async Task APostAckSnapshotCannotBlockTheNextMovementInput()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var peer = new Peer(timeout.Token);
        var time = new ManualTimeProvider();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = peer.Client(time, recorder);
        await client.ConnectAsync(timeout.Token);
        var first = client.QueueRealtimeMovement(Move(55), 100);
        await client.PollAsync(timeout.Token);
        await Until(() => recorder.CaptureReport().ProbeOutcomes.Count != 0, timeout.Token);
        await client.PollAsync(timeout.Token);
        var sent = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        await peer.Send(new Envelope { CommandAck = new CommandAck { Results = { new CommandResult {
            ClientSequence = first, AppliedTick = sent.RequestedTick, Status = CommandStatus.Accepted } } } });
        await Until(() => client.PendingCommandCount == 0, timeout.Token);
        Assert.Equal(0, peer.SnapshotRequests); // Only the command's promised snapshot is missing.
        var second = client.QueueRealtimeMovement(Move(49), 100);
        await client.PollAsync(timeout.Token);
        var next = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(second, next.ClientSequence);
    }

    [Fact]
    public async Task UnsentManualMovementCoalescesWithoutChangingSentIdentitiesOrOtherCommands()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var peer = new Peer(timeout.Token);
        var time = new ManualTimeProvider();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = peer.Client(time, recorder);
        await client.ConnectAsync(timeout.Token);
        var other = client.QueueRealtime(new Command { OpenStoreRequested = new() { StoreId = 1 } }, 100);
        var first = client.QueueRealtimeMovement(Move(55), 100);
        Assert.Equal(first, client.QueueRealtimeMovement(Move(49), 100));
        Assert.Equal(2, client.PendingCommandCount);
        await client.PollAsync(timeout.Token);
        await Until(() => recorder.CaptureReport().ProbeOutcomes.Count != 0, timeout.Token);
        await client.PollAsync(timeout.Token);
        var otherSent = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        var moveSent = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(other, otherSent.ClientSequence);
        Assert.Equal(Command.IntentOneofCase.OpenStoreRequested, otherSent.IntentCase);
        Assert.Equal(first, moveSent.ClientSequence);
        Assert.Equal(49, moveSent.MoveRequested.TargetX);
        Assert.True(moveSent.MoveRequested.FollowPath);
        var second = client.QueueRealtimeMovement(Move(48), 100);
        Assert.NotEqual(first, second);
        Assert.Equal(second, client.QueueRealtimeMovement(Move(47), 100));
        await client.PollAsync(timeout.Token);
        var latest = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(second, latest.ClientSequence);
        Assert.Equal(47, latest.MoveRequested.TargetX);
        Assert.Equal(49, moveSent.MoveRequested.TargetX);
    }

    [Fact]
    public async Task CoalescingDoesNotMoveAnInputAcrossAnotherIntent()
    {
        await using var client = new AuthoritativeClient(new("127.0.0.1", 1, "test", "0.1.0", "content-hash"));
        var first = client.QueueRealtimeMovement(Move(55), 100);
        var other = client.QueueRealtime(new Command { OpenStoreRequested = new() { StoreId = 1 } }, 100);
        var last = client.QueueRealtimeMovement(Move(49), 100);
        Assert.True(last > other && other > first);
        Assert.Equal(last, client.QueueRealtimeMovement(Move(48), 100));
        Assert.Equal(3, client.PendingCommandCount);
    }

    [Fact]
    public async Task DispatchRemainsOrderedWhenAnAcknowledgementFreesADictionarySlot()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var peer = new Peer(timeout.Token);
        var time = new ManualTimeProvider();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = peer.Client(time, recorder);
        await client.ConnectAsync(timeout.Token);
        var first = client.QueueRealtimeMovement(Move(55), 100);
        await client.PollAsync(timeout.Token);
        await Until(() => recorder.CaptureReport().ProbeOutcomes.Count != 0, timeout.Token);
        await client.PollAsync(timeout.Token);
        var sent = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        var other = client.QueueRealtime(new Command { OpenStoreRequested = new() { StoreId = 1 } }, 100);
        await peer.Send(new Envelope { CommandAck = new CommandAck { Results = { new CommandResult {
            ClientSequence = first, AppliedTick = sent.RequestedTick, Status = CommandStatus.Accepted } } } });
        await Until(() => client.PendingCommandCount == 1, timeout.Token);
        var last = client.QueueRealtimeMovement(Move(49), 100);
        await client.PollAsync(timeout.Token);
        var a = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        var b = Assert.Single((await peer.Next(Envelope.PayloadOneofCase.CommandBatch)).CommandBatch.Commands);
        Assert.Equal(other, a.ClientSequence);
        Assert.Equal(last, b.ClientSequence);
    }

    [Theory]
    [InlineData(false, 250)] [InlineData(true, 100)]
    public async Task MotionPollingUsesTwoTicksAndNeverQueuesADuplicate(bool moving, int interval)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var peer = new Peer(timeout.Token, moving);
        var time = new ManualTimeProvider();
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = peer.Client(time, recorder);
        await client.ConnectAsync(timeout.Token);
        time.AdvanceMilliseconds(interval - 1);
        await client.PollAsync(timeout.Token);
        await Until(() => recorder.CaptureReport().ProbeOutcomes.Count != 0, timeout.Token);
        Assert.Equal(0, peer.SnapshotRequests);
        time.AdvanceMilliseconds(1);
        await client.PollAsync(timeout.Token);
        await peer.Next(Envelope.PayloadOneofCase.SnapshotRequest);
        for (var i = 0; i < 20; i++) {
            time.AdvanceMilliseconds(10);
            await client.PollAsync(timeout.Token);
        }
        Assert.Equal(1, peer.SnapshotRequests);
        await peer.Send(new Envelope { Snapshot = peer.Snapshot(102) });
        await Until(() => recorder.CaptureReport().Snapshots.Count >= 2, timeout.Token);
        time.AdvanceMilliseconds(interval - 1);
        await client.PollAsync(timeout.Token);
        Assert.Equal(1, peer.SnapshotRequests);
        time.AdvanceMilliseconds(1);
        await client.PollAsync(timeout.Token);
        await peer.Next(Envelope.PayloadOneofCase.SnapshotRequest);
        Assert.Equal(2, peer.SnapshotRequests);
    }

    private static Command Move(int x) => new() { MoveRequested = new() { TargetCell = true, TargetX = x, TargetY = 50 } };

    private static async Task Until(Func<bool> ready, CancellationToken token)
    {
        while (!ready()) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
    }

    private sealed class Peer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime;
        private readonly Channel<Envelope> messages = Channel.CreateUnbounded<Envelope>();
        private readonly bool moving;
        private readonly Task run;
        private readonly SemaphoreSlim write = new(1);
        private NetworkStream? stream;
        private int snapshotRequests;
        public int SnapshotRequests => Volatile.Read(ref snapshotRequests);

        public Peer(CancellationToken token, bool moving = false)
        {
            this.moving = moving;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            listener.Start();
            run = Run();
        }

        public AuthoritativeClient Client(ManualTimeProvider time, AuthoritativeClientCommandTimingRecorder recorder)
            => new(new ClientConnectionOptions("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                "responsive-movement-test", "0.1.0", "content-hash") { TimeProvider = time }) { CommandTimingRecorder = recorder };

        public Snapshot Snapshot(ulong tick) => new() { Tick = tick, Players = { new PlayerSnapshot {
            EntityId = 1, PositionX = 50, PositionY = 50, Life = 64, LifeMaximum = 64,
            NativeWalkPathPending = moving } } };

        private async Task Run()
        {
            try {
                using var connection = await listener.AcceptTcpClientAsync(lifetime.Token);
                stream = connection.GetStream();
                while (!lifetime.IsCancellationRequested) {
                    var header = new byte[4];
                    await stream.ReadExactlyAsync(header, lifetime.Token);
                    var bytes = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
                    await stream.ReadExactlyAsync(bytes, lifetime.Token);
                    var envelope = Envelope.Parser.ParseFrom(bytes);
                    if (envelope.ClientHello is not null) {
                        await Send(new Envelope { ServerHello = new() { ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                            SessionToken = "responsive-test", TickRateHz = 20, GameMode = GameMode.SinglePlayer,
                            CommandSchedulingMode = CommandSchedulingMode.ClockGated, SupportsClockOnlyProbes = true,
                            SupportsTargetCellPathing = true } });
                        await Send(new Envelope { Snapshot = Snapshot(100) });
                    } else if (envelope.ClockProbeRequest is { } probe) {
                        await Send(new Envelope { TimingReply = new() { RequestId = probe.RequestId, ServerSchedulingClockTick = 100 } });
                    } else if (envelope.SnapshotRequest is not null) {
                        Interlocked.Increment(ref snapshotRequests);
                    }
                    await messages.Writer.WriteAsync(envelope, lifetime.Token);
                }
            } catch (Exception e) when (e is OperationCanceledException or EndOfStreamException or IOException or ObjectDisposedException) {
                messages.Writer.TryComplete(e);
            }
        }

        public async Task<Envelope> Next(Envelope.PayloadOneofCase kind)
        {
            while (true) { var message = await messages.Reader.ReadAsync(lifetime.Token); if (message.PayloadCase == kind) return message; }
        }

        public async Task Send(Envelope envelope)
        {
            await write.WaitAsync(lifetime.Token);
            try {
                var bytes = envelope.ToByteArray(); var header = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
                await stream!.WriteAsync(header, lifetime.Token);
                await stream.WriteAsync(bytes, lifetime.Token);
            } finally { write.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel(); listener.Stop(); stream?.Dispose();
            await run; lifetime.Dispose(); write.Dispose();
        }
    }
}
