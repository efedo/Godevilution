using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Covers same-connection wire ordering at a held clock boundary. */
public sealed class ClockGatedPipelineOrderingTests
{
    [Fact]
    public async Task EqualRequestedTicksExecuteAndAcknowledgeInEnvelopeOrder()
    {
        await WithHostAsync(async (host, cancellationToken) => {
            using var client = await ConnectAsync(host, cancellationToken, "equal-ticks");
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 120, 1));
            await SendBatchAsync(client.Stream, cancellationToken, Attack(2, 120, 2));
            await host.Admissions.WaitForAsync(2, cancellationToken);
            Assert.Equal(2, host.Server.PendingCommandCount);
            Assert.Empty(host.Executor.AppliedTicks);

            host.PhysicalTick = 120;
            var first = await ReadResultAsync(client.Stream, cancellationToken);
            var second = await ReadResultAsync(client.Stream, cancellationToken);

            Assert.Equal(1UL, first.ClientSequence);
            Assert.Equal(2UL, second.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, first.Status);
            Assert.Equal(CommandStatus.Accepted, second.Status);
            Assert.Equal(120UL, first.AppliedTick);
            Assert.Equal(120UL, second.AppliedTick);
            Assert.Equal([120UL, 120UL], host.Executor.AppliedTicks.ToArray());
            Assert.Equal([1UL, 2UL], host.Executor.AppliedClientSequences.ToArray());
            Assert.Equal([1UL, 2UL], host.Executor.AppliedTargetEntityIds.ToArray());
        });
    }

    [Fact]
    public async Task DescendingRequestedTicksPreserveWireOrderThenRejectTheLateTail()
    {
        await WithHostAsync(async (host, cancellationToken) => {
            using var client = await ConnectAsync(host, cancellationToken, "descending-ticks");
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 125, 1));
            await SendBatchAsync(client.Stream, cancellationToken, Attack(2, 120, 2));
            await host.Admissions.WaitForAsync(2, cancellationToken);
            Assert.Equal(2, host.Server.PendingCommandCount);

            host.PhysicalTick = 130;
            var first = await ReadResultAsync(client.Stream, cancellationToken);
            var second = await ReadResultAsync(client.Stream, cancellationToken);

            Assert.Equal(1UL, first.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, first.Status);
            Assert.Equal(125UL, first.AppliedTick);
            Assert.Equal(2UL, second.ClientSequence);
            Assert.Equal(CommandStatus.Rejected, second.Status);
            Assert.Equal(CommandRejectReason.TooLate, second.RejectReason);
            Assert.Equal(125UL, second.AppliedTick);
            Assert.Equal([125UL], host.Executor.AppliedTicks.ToArray());
        });
    }

    [Fact]
    public async Task FutureDuplicateOnlyWorkUnblocksFreshTailWithoutDeadlock()
    {
        await WithHostAsync(async (host, cancellationToken) => {
            using var client = await ConnectAsync(host, cancellationToken, "duplicate-tail");
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 120, 1));
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 120, 1));
            await SendBatchAsync(client.Stream, cancellationToken, Attack(3, 125, 3));
            await host.Admissions.WaitForAsync(3, cancellationToken);
            Assert.Equal(3, host.Server.PendingCommandCount);

            host.PhysicalTick = 130;
            var first = await ReadResultAsync(client.Stream, cancellationToken);
            var duplicate = await ReadResultAsync(client.Stream, cancellationToken);
            var fresh = await ReadResultAsync(client.Stream, cancellationToken);

            Assert.Equal(1UL, first.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, first.Status);
            Assert.Equal(1UL, duplicate.ClientSequence);
            Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
            Assert.Equal(first.ServerReceiptSequence, duplicate.ServerReceiptSequence);
            Assert.Equal(3UL, fresh.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, fresh.Status);
            Assert.Equal([120UL, 125UL], host.Executor.AppliedTicks.ToArray());
        });
    }

    [Fact]
    public async Task FutureEmptyBatchPreservesMalformedReceiptBeforeFreshTailInOneDrain()
    {
        await WithHostAsync(async (host, cancellationToken) => {
            using var client = await ConnectAsync(host, cancellationToken, "empty-tail");
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 120, 1));
            await SendBatchAsync(client.Stream, cancellationToken);
            await SendBatchAsync(client.Stream, cancellationToken, Attack(3, 125, 3));
            await host.Admissions.WaitForAsync(1, cancellationToken);
            await host.Admissions.WaitForAsync(3, cancellationToken);
            Assert.Equal(2, host.Server.PendingCommandCount);

            host.PhysicalTick = 130;
            var first = await ReadResultAsync(client.Stream, cancellationToken);
            var empty = await ReadResultAsync(client.Stream, cancellationToken);
            var fresh = await ReadResultAsync(client.Stream, cancellationToken);

            Assert.Equal(1UL, first.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, first.Status);
            Assert.Equal(0UL, empty.ClientSequence);
            Assert.Equal(0UL, empty.ServerReceiptSequence);
            Assert.Equal(CommandStatus.Rejected, empty.Status);
            Assert.Equal(CommandRejectReason.Malformed, empty.RejectReason);
            Assert.Equal(3UL, fresh.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, fresh.Status);
            Assert.Equal([120UL, 125UL], host.Executor.AppliedTicks.ToArray());
        });
    }

    [Fact]
    public async Task PublicationFailurePoisonsAdmittedSuccessorAndFreshIngress()
    {
        var saveProvider = new BlockingFailureSaveProvider();
        await WithHostAsync(async (host, cancellationToken) => {
            using var client = await ConnectAsync(host, cancellationToken, "publication-failure");
            saveProvider.FailureBoundaryReached =
                () => !host.Executor.AppliedTicks.IsEmpty;
            saveProvider.FailOnExport = true;
            await SendBatchAsync(client.Stream, cancellationToken, Attack(1, 120, 1));
            await SendBatchAsync(client.Stream, cancellationToken, Attack(2, 125, 2));
            await host.Admissions.WaitForAsync(2, cancellationToken);

            host.PhysicalTick = 130;
            await saveProvider.Entered.Task.WaitAsync(cancellationToken);

            // This envelope is sent while publication owns the authority
            // boundary. It must not become a new healthy lane after failure.
            await SendBatchAsync(client.Stream, cancellationToken, Attack(3, 130, 3));
            saveProvider.Release();

            var observedFailure = await host.ClientFailure.Task.WaitAsync(cancellationToken);
            Assert.Same(saveProvider.Failure, observedFailure);
            await WaitForConnectionCloseAsync(client.Stream, cancellationToken);
            await WaitUntilAsync(
                () => host.Server.PendingCommandCount == 0,
                cancellationToken);
            Assert.Equal([120UL], host.Executor.AppliedTicks.ToArray());
            Assert.Equal([1UL], host.Executor.AppliedClientSequences.ToArray());
            Assert.DoesNotContain(
                host.Admissions.Records,
                admission => admission.ClientSequence == 3);
            Assert.IsType<InvalidOperationException>(saveProvider.Failure);
        }, saveProvider);
    }

    private static async Task WithHostAsync(
        Func<ClockedTestHost, CancellationToken, Task> body,
        BlockingFailureSaveProvider? saveProvider = null)
    {
        var host = new ClockedTestHost(saveProvider);
        Exception? primaryFailure = null;
        try {
            host.Server.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await body(host, timeout.Token);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            saveProvider?.Release();
            host.PhysicalTick = 130;
            try {
                await host.Server.DisposeAsync().AsTask().WaitAsync(
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);
            } catch (Exception cleanupFailure) {
                if (primaryFailure is null)
                    throw;
                try {
                    primaryFailure.Data["ClockGatedPipelineOrderingTests.CleanupFailure"] =
                        cleanupFailure;
                } catch {
                }
            }
        }
    }

    private static async Task<ConnectedClient> ConnectAsync(
        ClockedTestHost host,
        CancellationToken cancellationToken,
        string resumeToken)
    {
        var client = new TcpClient();
        try {
            await client.ConnectAsync(IPAddress.Loopback, host.Server.Port, cancellationToken);
            var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, resumeToken);
            await ReadHandshakeAsync(stream, cancellationToken);
            return new ConnectedClient(client, stream);
        } catch {
            client.Dispose();
            throw;
        }
    }

    private static ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string resumeToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "clocked-ordering-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);

    private static async Task ReadHandshakeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        Assert.Equal(100UL, snapshot.Snapshot.Tick);
    }

    private static async Task SendBatchAsync(
        Stream stream,
        CancellationToken cancellationToken,
        params Command[] commands)
    {
        var batch = new CommandBatch();
        batch.Commands.Add(commands);
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope { CommandBatch = batch },
            cancellationToken);
    }

    private static async Task<CommandResult> ReadResultAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement!.PayloadCase);
        var result = Assert.Single(acknowledgement.CommandAck.Results);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        return result;
    }

    private static async Task WaitForConnectionCloseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try {
            var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            Assert.Null(envelope);
        } catch (SocketException) {
            // A reset is also a genuine transport close for this failure path.
        } catch (IOException) {
            // A reset may surface as an IOException on NetworkStream.
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        while (!predicate())
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
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

    private sealed class ConnectedClient(TcpClient client, NetworkStream stream) : IDisposable
    {
        public NetworkStream Stream { get; } = stream;

        public void Dispose()
        {
            Stream.Dispose();
            client.Dispose();
        }
    }

    private sealed class ClockedTestHost
    {
        private ulong physicalTick = 100;

        public ClockedTestHost(BlockingFailureSaveProvider? saveProvider)
        {
            Executor = new RecordingExecutor();
            Admissions = new AdmissionRecorder();
            Server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(Executor),
                Handshake,
                () => Volatile.Read(ref physicalTick),
                snapshotProvider: Executor,
                saveProvider: saveProvider) {
                UseClockGatedCommandScheduling = true,
                CommandTimingRecorder = new HostCommandTimingRecorder(),
                CommandTimingDrainObserver = Admissions.Record,
                ClientFailureObserver = failure => ClientFailure.TrySetResult(failure),
            };
        }

        public AuthoritativeTcpServer Server { get; }

        public RecordingExecutor Executor { get; }

        public AdmissionRecorder Admissions { get; }

        public TaskCompletionSource<Exception> ClientFailure { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ulong PhysicalTick
        {
            get => Volatile.Read(ref physicalTick);
            set => Volatile.Write(ref physicalTick, value);
        }
    }

    private sealed class AdmissionRecorder
    {
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<Admission>> admissions = [];

        public ConcurrentQueue<Admission> Records { get; } = [];

        public void Record(HostCommandTimingDrain drain)
        {
            foreach (var record in drain.Records) {
                if (record.Kind != "command-admission"
                    || record.ClientSequence is not { } clientSequence
                    || record.RequestedTick is not { } requestedTick
                    || record.ActualAdmissionTick is not { } actualAdmissionTick)
                    continue;
                var admission = new Admission(
                    clientSequence,
                    requestedTick,
                    actualAdmissionTick);
                Records.Enqueue(admission);
                admissions.GetOrAdd(
                    clientSequence,
                    _ => new TaskCompletionSource<Admission>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                    .TrySetResult(admission);
            }
        }

        public Task<Admission> WaitForAsync(
            ulong clientSequence,
            CancellationToken cancellationToken)
            => admissions.GetOrAdd(
                clientSequence,
                _ => new TaskCompletionSource<Admission>(
                    TaskCreationOptions.RunContinuationsAsynchronously))
                .Task
                .WaitAsync(cancellationToken);
    }

    private sealed record Admission(
        ulong ClientSequence,
        ulong RequestedTick,
        ulong ActualAdmissionTick);

    private sealed class RecordingExecutor
        : IAuthoritativeCommandExecutor,
          IAuthoritativeSnapshotProvider
    {
        public ConcurrentQueue<ulong> AppliedTicks { get; } = [];

        public ConcurrentQueue<ulong> AppliedClientSequences { get; } = [];

        public ConcurrentQueue<ulong> AppliedTargetEntityIds { get; } = [];

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

    private sealed class BlockingFailureSaveProvider : IAuthoritativeSaveProvider
    {
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int failNextExport;

        public TaskCompletionSource<bool> Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Failure { get; private set; }

        public Func<bool>? FailureBoundaryReached { get; set; }

        public bool FailOnExport
        {
            get => Volatile.Read(ref failNextExport) != 0;
            set => Volatile.Write(ref failNextExport, value ? 1 : 0);
        }

        public string ExportPlayerSave(string sessionId)
            => ExportSessionState(sessionId);

        public string ExportSessionState(string sessionId)
        {
            if (!FailOnExport
                || FailureBoundaryReached?.Invoke() != true
                || Interlocked.Exchange(ref failNextExport, 0) == 0)
                return "{}";
            Entered.TrySetResult(true);
            if (!release.Task.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Publication test release was not signaled.");
            Failure = new InvalidOperationException("controlled publication failure");
            throw Failure;
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }

        public void Release()
            => release.TrySetResult(true);
    }
}
