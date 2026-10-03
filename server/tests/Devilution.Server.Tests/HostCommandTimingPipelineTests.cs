using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class HostCommandTimingPipelineTests
{
    [Fact]
    public async Task ActualBatchBoundaryCorrelatesStrictTooLateAndFrozenDuplicate()
    {
        var executor = new TimingExecutor();
        var recorder = new HostCommandTimingRecorder();
        var drains = new ConcurrentQueue<HostCommandTimingDrain>();
        await using var server = CreateServer(executor, recorder, drains);
        server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, timeout.Token);
        await ReadHandshakeAsync(stream, timeout.Token);

        var command = new Command {
            ClientSequence = 1,
            RequestedTick = 120,
            AttackRequested = new AttackRequested { TargetEntityId = 1 },
        };
        var first = await SendCommandAsync(stream, command, timeout.Token);
        var duplicate = await SendCommandAsync(stream, command, timeout.Token);

        Assert.Equal(CommandStatus.Rejected, first.Status);
        Assert.Equal(CommandRejectReason.TooLate, first.RejectReason);
        Assert.Equal(123UL, first.AppliedTick);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(first.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(first.AppliedTick, duplicate.AppliedTick);
        Assert.Empty(executor.AppliedTicks);

        var commandRecords = drains
            .SelectMany(drain => drain.Records)
            .Where(record => record.Kind == "command" && record.ClientSequence == 1)
            .ToArray();
        Assert.Equal(2, commandRecords.Length);
        Assert.All(commandRecords, record => {
            Assert.Equal(123UL, record.SampledClockCurrentTick);
            Assert.Equal(123UL, record.SampledCommandServerCurrentTick);
            Assert.Equal(123UL, record.ActualAdmissionTick);
            Assert.Equal(120UL, record.RequestedTick);
        });
        Assert.Contains(commandRecords, record =>
            record.Status == nameof(CommandStatus.Rejected)
            && record.RejectReason == nameof(CommandRejectReason.TooLate)
            && record.ServerReceiptSequence == first.ServerReceiptSequence
            && record.AppliedTick == 123);
        Assert.Contains(commandRecords, record =>
            record.Status == nameof(CommandStatus.Duplicate)
            && record.ServerReceiptSequence == first.ServerReceiptSequence
            && record.AppliedTick == 123);
        Assert.Contains(
            drains.SelectMany(drain => drain.Records),
            record => record.Kind == "command-ack-write"
                && record.AckWriteCompleted == true
                && record.AckWriteStartTimestamp.HasValue
                && record.AckWriteEndTimestamp.HasValue);
    }

    [Fact]
    public async Task ThrowingTimingObserverCannotReplaceTcpAcknowledgement()
    {
        var executor = new TimingExecutor();
        var recorder = new HostCommandTimingRecorder();
        await using var server = new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(executor),
            Handshake,
            () => 123,
            snapshotProvider: executor) {
            CommandTimingRecorder = recorder,
            CommandTimingDrainObserver = _ => throw new InvalidOperationException("diagnostic observer"),
        };
        server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, timeout.Token);
        await ReadHandshakeAsync(stream, timeout.Token);
        var result = await SendCommandAsync(stream, new Command {
            ClientSequence = 1,
            RequestedTick = 123,
            AttackRequested = new AttackRequested { TargetEntityId = 1 },
        }, timeout.Token);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.False(server.Completion.IsCompleted);
        Assert.True(recorder.FailureCount > 0);
    }

    [Fact]
    public async Task ClockGatedFutureCommandKeepsSubmittedBoundaryWhileAckWaits()
    {
        ulong physicalTick = 100;
        var executor = new TimingExecutor();
        var recorder = new HostCommandTimingRecorder();
        var drains = new ConcurrentQueue<HostCommandTimingDrain>();
        await using var server = CreateServer(
            executor,
            recorder,
            drains,
            clockGated: true,
            tickProvider: () => Volatile.Read(ref physicalTick));
        server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, timeout.Token, supportsClockGatedCommands: true);
        await ReadHandshakeAsync(stream, timeout.Token);
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { new Command {
                ClientSequence = 1,
                RequestedTick = 120,
                AttackRequested = new AttackRequested { TargetEntityId = 1 },
            } } },
        }, timeout.Token);

        await WaitUntilAsync(
            () => drains.SelectMany(drain => drain.Records)
                .Any(record => record.Kind == "command-admission" && record.ClientSequence == 1),
            timeout.Token);
        Assert.DoesNotContain(executor.AppliedTicks, tick => tick == 120);
        Volatile.Write(ref physicalTick, 120);
        await WaitUntilAsync(() => executor.AppliedTicks.Contains(120UL), timeout.Token);
        var acknowledgement = (await EnvelopeCodec.ReadAsync(stream, timeout.Token))!.CommandAck.Results.Single();
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        Assert.Equal(120UL, acknowledgement.AppliedTick);
        Assert.Equal(
            Envelope.PayloadOneofCase.Snapshot,
            (await EnvelopeCodec.ReadAsync(stream, timeout.Token))!.PayloadCase);

        var records = drains.SelectMany(drain => drain.Records).ToArray();
        var admission = Assert.Single(records, record =>
            record.Kind == "command-admission" && record.ClientSequence == 1);
        var command = Assert.Single(records, record =>
            record.Kind == "command" && record.ClientSequence == 1);
        Assert.Equal(100UL, admission.ActualAdmissionTick);
        Assert.Equal(100UL, command.ActualAdmissionTick);
        Assert.Equal(120UL, command.AppliedTick);
    }

    [Fact]
    public async Task SnapshotAndTimingProbeRecordsCorrelateOutsideAuthorityLock()
    {
        var executor = new TimingExecutor();
        var recorder = new HostCommandTimingRecorder();
        var drains = new ConcurrentQueue<HostCommandTimingDrain>();
        var commandServer = new AuthoritativeCommandServer(executor);
        var observerFailures = new ConcurrentQueue<Exception>();
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            Handshake,
            () => 123,
            snapshotProvider: executor) {
            CommandTimingRecorder = recorder,
            UseClockGatedCommandScheduling = true,
            CommandTimingDrainObserver = drain => {
                try {
                    if (!Task.Run(() => commandServer.CurrentTick)
                        .Wait(TimeSpan.FromSeconds(1)))
                        throw new TimeoutException("Timing observer ran under the authority lock.");
                    drains.Enqueue(drain);
                } catch (Exception exception) {
                    observerFailures.Enqueue(exception);
                }
            },
        };
        server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, timeout.Token, supportsClockGatedCommands: true);
        await ReadHandshakeAsync(stream, timeout.Token);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest { TimingRequestId = 17 },
        }, timeout.Token);
        var snapshot = (await EnvelopeCodec.ReadAsync(stream, timeout.Token))!.Snapshot;
        var timingReply = (await EnvelopeCodec.ReadAsync(stream, timeout.Token))!.TimingReply;
        await WaitUntilAsync(
            () => drains.SelectMany(drain => drain.Records)
                .Any(record => record.Kind == "timing-reply" && record.TimingRequestId == 17),
            timeout.Token);

        Assert.Equal(123UL, snapshot.Tick);
        Assert.Equal(17UL, timingReply.RequestId);
        Assert.Empty(observerFailures);
        var records = drains.SelectMany(drain => drain.Records).ToArray();
        var projection = Assert.Single(records, record =>
            record.Kind == "snapshot" && record.TimingRequestId == 17);
        Assert.Equal(123UL, projection.SnapshotTick);
        Assert.True(projection.SnapshotMaterializeStartTimestamp.HasValue);
        Assert.True(projection.SnapshotMaterializeEndTimestamp.HasValue);
        Assert.True(projection.SnapshotWriteStartTimestamp.HasValue);
        Assert.True(projection.SnapshotWriteEndTimestamp.HasValue);
        var reply = Assert.Single(records, record =>
            record.Kind == "timing-reply" && record.TimingRequestId == 17);
        Assert.True(reply.TimingReplyEmitted);
    }

    private static AuthoritativeTcpServer CreateServer(
        TimingExecutor executor,
        HostCommandTimingRecorder recorder,
        ConcurrentQueue<HostCommandTimingDrain> drains,
        bool clockGated = false,
        Func<ulong>? tickProvider = null)
        => new(
            new AuthoritativeCommandServer(executor),
            Handshake,
            tickProvider ?? (() => 123),
            snapshotProvider: executor) {
            CommandTimingRecorder = recorder,
            CommandTimingDrainObserver = drains.Enqueue,
            UseClockGatedCommandScheduling = clockGated,
        };

    private static async Task SendHelloAsync(
        NetworkStream stream,
        CancellationToken cancellationToken,
        bool supportsClockGatedCommands = false)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "timing-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                SupportsClockGatedCommands = supportsClockGatedCommands,
            },
        }, cancellationToken);
    }

    private static async Task ReadHandshakeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        Assert.Equal(
            Envelope.PayloadOneofCase.Snapshot,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
    }

    private static async Task<CommandResult> SendCommandAsync(
        NetworkStream stream,
        Command command,
        CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);
        var acknowledgement = (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.CommandAck.Results.Single();
        Assert.Equal(
            Envelope.PayloadOneofCase.Snapshot,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        return acknowledgement;
    }

    private static ProtocolHandshake Handshake => new(
        new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        while (!predicate())
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
    }

    private sealed class TimingExecutor : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
    {
        public ConcurrentQueue<ulong> AppliedTicks { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            AppliedTicks.Enqueue(appliedTick);
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
    }
}
