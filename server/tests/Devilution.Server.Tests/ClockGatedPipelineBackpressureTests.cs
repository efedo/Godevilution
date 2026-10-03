using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

/** Proves retained response capacity and queued projection cancellation at real writes. */
public sealed class ClockGatedPipelineBackpressureTests
{
    [Fact]
    public async Task RetainedResponseWeightSurvivesActualStalledAckWrite()
    {
        ulong physicalTick = 100;
        var executor = new RecordingExecutor();
        var admissions = new AdmissionGate();
        var decorator = new TransportDecoratorFactory();
        var server = CreateServer(
            executor,
            () => Volatile.Read(ref physicalTick),
            admissions,
            decorator,
            maximumPendingCommands: 1);
        Exception? primaryFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;

            using var owner = await ConnectAsync(
                server,
                cancellationToken,
                "backpressure-owner");
            await SendCommandAsync(
                owner.Stream,
                Attack(1, 120, 1),
                cancellationToken);
            await admissions.WaitForAsync(1, cancellationToken);
            physicalTick = 120;
            await decorator.OwnerGate.WriteEntered.Task.WaitAsync(cancellationToken);
            Assert.Equal(0, server.PendingCommandCount);
            Assert.False(decorator.OwnerWrites.CommandAckWritten);

            using var probe = await ConnectAsync(
                server,
                cancellationToken,
                "backpressure-probe");
            await EnvelopeCodec.WriteAsync(probe.Stream, new Envelope {
                SnapshotRequest = new Envelope.Types.SnapshotRequest {
                    TimingRequestId = 77,
                },
            }, cancellationToken);
            var probeResponse = await ReadAsync(probe.Stream, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Error, probeResponse!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, probeResponse.Error.Code);
            Assert.Contains("capacity", probeResponse.Error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.False(decorator.OwnerGate.ReleaseRequested);

            decorator.OwnerGate.Release();
            await decorator.OwnerWrites.WaitForCommandAckAsync(cancellationToken);
            var acknowledgement = await ReadCommandAckAsync(owner.Stream, cancellationToken);
            Assert.Equal(1UL, acknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(120UL, acknowledgement.AppliedTick);
            Assert.Equal(120UL, await ReadSnapshotTickAsync(owner.Stream, cancellationToken));

            using var fresh = await ConnectAsync(
                server,
                cancellationToken,
                "backpressure-fresh");
            await EnvelopeCodec.WriteAsync(fresh.Stream, new Envelope {
                SnapshotRequest = new Envelope.Types.SnapshotRequest {
                    TimingRequestId = 88,
                },
            }, cancellationToken);
            Assert.Equal(120UL, await ReadSnapshotTickAsync(fresh.Stream, cancellationToken));
            Assert.Equal(88UL, await ReadTimingReplyAsync(fresh.Stream, cancellationToken));
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            decorator.OwnerGate.Release();
            physicalTick = 130;
            await FinishCleanupAsync(server, primaryFailure);
        }
    }

    [Fact]
    public async Task TransportCancellationSkipsQueuedSnapshotAndTimingProviderWork()
    {
        ulong physicalTick = 100;
        var executor = new RecordingExecutor();
        var snapshotProvider = new CountingSnapshotProvider();
        var admissions = new AdmissionGate();
        var decorator = new TransportDecoratorFactory();
        var clientFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var server = CreateServer(
            executor,
            () => Volatile.Read(ref physicalTick),
            admissions,
            decorator,
            snapshotProvider,
            maximumPendingCommands: 3,
            clientFailureObserver: failure => clientFailure.TrySetResult(failure));
        Exception? primaryFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var owner = await ConnectAsync(
                server,
                cancellationToken,
                "cancelled-projection-owner");
            await SendCommandAsync(
                owner.Stream,
                Attack(1, 120, 1),
                cancellationToken);
            await admissions.WaitForAsync(1, cancellationToken);
            physicalTick = 120;
            await decorator.OwnerGate.WriteEntered.Task.WaitAsync(cancellationToken);

            await SendSnapshotRequestAsync(owner.Stream, 41, cancellationToken);
            await decorator.OwnerReads.WaitForSnapshotAsync(41, cancellationToken);
            await SendSnapshotRequestAsync(owner.Stream, 42, cancellationToken);
            // Seeing frame 42 means the reader completed frame 41 and
            // returned to ingress, so frame 41 has already been enqueued.
            await decorator.OwnerReads.WaitForSnapshotAsync(42, cancellationToken);

            owner.ResetWithRst();
            var transportFailure = await clientFailure.Task.WaitAsync(cancellationToken);
            var originalReadFailure = await decorator.OwnerReads.Failure.Task.WaitAsync(cancellationToken);
            Assert.NotNull(transportFailure);
            Assert.IsNotType<ClockGatedTransportException>(transportFailure);
            Assert.True(transportFailure is IOException or SocketException or ObjectDisposedException,
                transportFailure.ToString());
            Assert.Same(originalReadFailure, transportFailure);
            Assert.Equal(0, server.PendingCommandCount);
            Assert.False(snapshotProvider.SecondCall.Task.IsCompleted);
            Assert.Equal(1, snapshotProvider.CreateSnapshotCount);
            Assert.Equal([1UL], executor.AppliedClientSequences.ToArray());
            Assert.Equal([1U], executor.AppliedTargetEntityIds.ToArray());
            Assert.False(decorator.OwnerGate.ReleaseRequested);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            decorator.OwnerGate.Release();
            physicalTick = 130;
            await FinishCleanupAsync(server, primaryFailure);
        }
    }

    private static AuthoritativeTcpServer CreateServer(
        RecordingExecutor executor,
        Func<ulong> physicalTickProvider,
        AdmissionGate admissions,
        TransportDecoratorFactory decorator,
        CountingSnapshotProvider? snapshotProvider = null,
        int maximumPendingCommands = 1024,
        Action<Exception>? clientFailureObserver = null)
        => new(
            new AuthoritativeCommandServer(executor),
            Handshake,
            physicalTickProvider,
            snapshotProvider: (IAuthoritativeSnapshotProvider?)snapshotProvider ?? executor) {
            UseClockGatedCommandScheduling = true,
            MaximumPendingCommands = maximumPendingCommands,
            CommandTimingRecorder = new HostCommandTimingRecorder(),
            CommandTimingDrainObserver = admissions.Record,
            ClientFailureObserver = clientFailureObserver,
            ClockGatedTransportDecorator = decorator.Decorate,
        };

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        return timeout;
    }

    private static async Task<ConnectedClient> ConnectAsync(
        AuthoritativeTcpServer server,
        CancellationToken cancellationToken,
        string resumeToken)
    {
        var client = new TcpClient();
        try {
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            var stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, new Envelope {
                ClientHello = new ClientHello {
                    ClientBuildId = "clocked-backpressure-client",
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = "content-hash",
                    ResumeToken = resumeToken,
                    SupportsClockGatedCommands = true,
                },
            }, cancellationToken);
            var hello = await ReadAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
            var snapshot = await ReadAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
            return new ConnectedClient(client, stream);
        } catch {
            client.Dispose();
            throw;
        }
    }

    private static ValueTask SendCommandAsync(
        Stream stream,
        Command command,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);

    private static ValueTask SendSnapshotRequestAsync(
        Stream stream,
        ulong timingRequestId,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest {
                TimingRequestId = timingRequestId,
            },
        }, cancellationToken);

    private static Task<Envelope?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
        => EnvelopeCodec.ReadAsync(stream, cancellationToken).AsTask();

    private static async Task<CommandResult> ReadCommandAckAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, envelope!.PayloadCase);
        return Assert.Single(envelope.CommandAck.Results);
    }

    private static async Task<ulong> ReadSnapshotTickAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, envelope!.PayloadCase);
        return envelope.Snapshot.Tick;
    }

    private static async Task<ulong> ReadTimingReplyAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.TimingReply, envelope!.PayloadCase);
        return envelope.TimingReply.RequestId;
    }

    private static Command Attack(
        ulong clientSequence,
        ulong requestedTick,
        uint targetEntityId)
        => new() {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = targetEntityId },
        };

    private static ProtocolHandshake Handshake => new(
        new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));

    private static async Task FinishCleanupAsync(
        AuthoritativeTcpServer server,
        Exception? primaryFailure)
    {
        try {
            await server.DisposeAsync().AsTask().WaitAsync(
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
        } catch (Exception cleanupFailure) {
            if (primaryFailure is null)
                throw;
            try {
                primaryFailure.Data["ClockGatedPipelineBackpressureTests.CleanupFailure"] =
                    cleanupFailure;
            } catch {
            }
        }
    }

    private sealed class ConnectedClient(TcpClient client, NetworkStream stream) : IDisposable
    {
        public NetworkStream Stream { get; } = stream;

        public void ResetWithRst()
        {
            client.Client.LingerState = new LingerOption(true, 0);
            // TcpClient.Dispose first asks NetworkStream to shut down both directions,
            // which produces a clean FIN even when linger is configured. Close the
            // connected socket directly while the abortive linger setting is active.
            client.Client.Close(0);
        }

        public void Dispose()
        {
            Stream.Dispose();
            client.Dispose();
        }
    }

    private sealed class AdmissionGate
    {
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> pending = [];

        public void Record(HostCommandTimingDrain drain)
        {
            foreach (var record in drain.Records) {
                if (record.Kind != "command-admission"
                    || record.ClientSequence is not { } sequence)
                    continue;
                pending.GetOrAdd(
                    sequence,
                    static _ => new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                    .TrySetResult(true);
            }
        }

        public Task WaitForAsync(ulong sequence, CancellationToken cancellationToken)
            => pending.GetOrAdd(
                    sequence,
                    static _ => new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                .Task
                .WaitAsync(cancellationToken);
    }

    private sealed class TransportDecoratorFactory
    {
        private int ownerAssigned;

        public WriteGate OwnerGate { get; } = new();

        public FrameCapture OwnerReads { get; } = new();

        public FrameCapture OwnerWrites { get; } = new();

        public Stream Decorate(Stream stream)
        {
            var owner = Interlocked.Exchange(ref ownerAssigned, 1) == 0;
            return new GatedTransportStream(
                stream,
                owner ? OwnerGate : null,
                owner ? OwnerReads : new FrameCapture(),
                owner ? OwnerWrites : new FrameCapture());
        }
    }

    private sealed class WriteGate
    {
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WriteEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ReleaseRequested => release.Task.IsCompleted;

        public Task WaitForReleaseAsync(CancellationToken cancellationToken)
            => release.Task.WaitAsync(cancellationToken);

        public void Release()
            => release.TrySetResult(true);
    }

    private sealed class FrameCapture
    {
        public TaskCompletionSource<Exception> Failure { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object synchronization = new();
        private readonly List<byte> pending = [];
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> snapshots = [];
        private readonly TaskCompletionSource<bool> commandAck = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CommandAckWritten => commandAck.Task.IsCompleted;

        public Task WaitForCommandAckAsync(CancellationToken cancellationToken)
            => commandAck.Task.WaitAsync(cancellationToken);

        public Task WaitForSnapshotAsync(
            ulong timingRequestId,
            CancellationToken cancellationToken)
            => snapshots.GetOrAdd(
                    timingRequestId,
                    static _ => new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                .Task
                .WaitAsync(cancellationToken);

        public void Observe(ReadOnlySpan<byte> bytes)
        {
            lock (synchronization) {
                pending.AddRange(bytes.ToArray());
                while (pending.Count >= sizeof(uint)) {
                    var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(
                        CollectionsMarshal.AsSpan(pending));
                    var frameLength = checked(sizeof(uint) + (int)payloadLength);
                    if (pending.Count < frameLength)
                        return;
                    var payload = pending.GetRange(sizeof(uint), (int)payloadLength).ToArray();
                    pending.RemoveRange(0, frameLength);
                    var envelope = Envelope.Parser.ParseFrom(payload);
                    if (envelope.PayloadCase == Envelope.PayloadOneofCase.SnapshotRequest)
                        snapshots.GetOrAdd(
                                envelope.SnapshotRequest.TimingRequestId,
                                static _ => new TaskCompletionSource<bool>(
                                    TaskCreationOptions.RunContinuationsAsynchronously))
                            .TrySetResult(true);
                    else if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck)
                        commandAck.TrySetResult(true);
                }
            }
        }
    }

    private sealed class GatedTransportStream(
        Stream inner,
        WriteGate? writeGate,
        FrameCapture reads,
        FrameCapture writes) : Stream
    {
        private int gateConsumed;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            if (read != 0)
                reads.Observe(buffer.AsSpan(offset, read));
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            if (read != 0)
                reads.Observe(buffer[..read]);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            try {
                var read = await inner.ReadAsync(buffer, cancellationToken);
                if (read != 0)
                    reads.Observe(buffer[..read].Span);
                return read;
            } catch (Exception failure) when (failure is IOException or SocketException or ObjectDisposedException) {
                reads.Failure.TrySetResult(failure);
                throw;
            }
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
            if (read != 0)
                reads.Observe(buffer.AsSpan(offset, read));
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WaitForGate(CancellationToken.None);
            inner.Write(buffer, offset, count);
            writes.Observe(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WaitForGate(CancellationToken.None);
            inner.Write(buffer);
            writes.Observe(buffer);
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await WaitForGateAsync(cancellationToken);
            await inner.WriteAsync(buffer, cancellationToken);
            writes.Observe(buffer.Span);
        }

        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            await WaitForGateAsync(cancellationToken);
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
            writes.Observe(buffer.AsSpan(offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
            => inner.Seek(offset, origin);

        public override void SetLength(long value)
            => inner.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            // The host owns the underlying NetworkStream and closes it outside
            // the decorator; disposing this wrapper must not close that owner.
        }

        public override ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        private void WaitForGate(CancellationToken cancellationToken)
        {
            if (writeGate is null || Interlocked.Exchange(ref gateConsumed, 1) != 0)
                return;
            writeGate.WriteEntered.TrySetResult(true);
            writeGate.WaitForReleaseAsync(cancellationToken).GetAwaiter().GetResult();
        }

        private async Task WaitForGateAsync(CancellationToken cancellationToken)
        {
            if (writeGate is null || Interlocked.Exchange(ref gateConsumed, 1) != 0)
                return;
            writeGate.WriteEntered.TrySetResult(true);
            await writeGate.WaitForReleaseAsync(cancellationToken);
        }
    }

    private sealed class RecordingExecutor
        : IAuthoritativeCommandExecutor,
          IAuthoritativeSnapshotProvider
    {
        public ConcurrentQueue<ulong> AppliedTicks { get; } = [];

        public ConcurrentQueue<ulong> AppliedClientSequences { get; } = [];

        public ConcurrentQueue<uint> AppliedTargetEntityIds { get; } = [];

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
        {
            AppliedTicks.Enqueue(appliedTick);
            AppliedClientSequences.Enqueue(command.ClientSequence);
            AppliedTargetEntityIds.Enqueue(
                command.AttackRequested is { } attack
                    ? attack.TargetEntityId
                    : 0U);
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
    }

    private sealed class CountingSnapshotProvider
        : IAuthoritativeSnapshotProvider
    {
        private int createSnapshotCount;

        public int CreateSnapshotCount => Volatile.Read(ref createSnapshotCount);

        public TaskCompletionSource<bool> SecondCall { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            var call = Interlocked.Increment(ref createSnapshotCount);
            if (call >= 2)
                SecondCall.TrySetResult(true);
            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
        }
    }
}
