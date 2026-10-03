using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientCommandTimingRecorderTests
{
    [Fact]
    public void BoundedCaptureReportsDroppedRecordsExplicitly()
    {
        var recorder = new AuthoritativeClientCommandTimingRecorder(capacity: 2);
        recorder.RecordSnapshot(new AuthoritativeClientSnapshotTiming(1, 10, 20, null, 30, true));
        recorder.RecordAcknowledgement(new AuthoritativeClientAcknowledgementTiming(
            1, 4, 40, CommandStatus.Accepted, CommandRejectReason.Unspecified, 10, true, false, 20, 50));
        recorder.RecordSnapshot(new AuthoritativeClientSnapshotTiming(1, 11, 21, null, 31, true));

        var report = recorder.CaptureReport();

        Assert.Single(report.Snapshots);
        Assert.Single(report.Acknowledgements);
        Assert.Equal(1, report.DroppedRecordCount);
    }

    [Fact]
    public void CaptureReportIsDetachedAndReadOnly()
    {
        var recorder = new AuthoritativeClientCommandTimingRecorder(capacity: 4);
        recorder.RecordSnapshot(new AuthoritativeClientSnapshotTiming(2, 10, 20, 7, 30, false));
        var report = recorder.CaptureReport();

        recorder.RecordSnapshot(new AuthoritativeClientSnapshotTiming(2, 11, 21, 8, 31, false));

        Assert.Single(report.Snapshots);
        var snapshots = Assert.IsAssignableFrom<IList<AuthoritativeClientSnapshotTiming>>(report.Snapshots);
        Assert.Throws<NotSupportedException>(() => snapshots.Add(
            new AuthoritativeClientSnapshotTiming(2, 12, 22, 9, 32, false)));
    }

    [Fact]
    public void CommandCapturePreservesExplicitTickAndRetryIdentity()
    {
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        recorder.RecordCommand(new AuthoritativeClientCommandTiming(
            9, 3, AuthoritativeClientCommandDispatchKind.FirstDispatch, 1, 100, 107,
            500, 501, 502, AuthoritativeClientWriteOutcome.Completed, 10, 11, 12, 120, true));
        recorder.RecordCommand(new AuthoritativeClientCommandTiming(
            9, 4, AuthoritativeClientCommandDispatchKind.Retransmission, 2, 100, 107,
            900, 901, 902, AuthoritativeClientWriteOutcome.Completed, 20, 21, 22, 120, true));

        var commands = recorder.CaptureReport().Commands;

        Assert.Equal(2, commands.Count);
        Assert.Equal(100UL, commands[0].ObservedTick!.Value);
        Assert.Equal(107UL, commands[0].RequestedTick);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.Retransmission, commands[1].DispatchKind);
        Assert.Equal(4, commands[1].TransportGeneration);
    }

    [Fact]
    public void PublicTimingTypesContainNoPayloadOrIdentityFields()
    {
        var forbidden = new[] { "Token", "Name", "Position", "Payload", "Gameplay" };
        foreach (var type in new[] {
                     typeof(AuthoritativeClientCommandTiming),
                     typeof(AuthoritativeClientProbeSentTiming),
                     typeof(AuthoritativeClientProbeOutcomeTiming),
                     typeof(AuthoritativeClientSnapshotTiming),
                     typeof(AuthoritativeClientAcknowledgementTiming),
                 }) {
            Assert.DoesNotContain(type.GetProperties(), property =>
                forbidden.Any(word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public async Task DefaultClientRecorderIsInert()
    {
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", 1, "timing-test", "0.1.0", "content-hash"));

        Assert.Null(client.CommandTimingRecorder);
    }

    [Fact]
    public async Task CapturesFrozenRetryAndAcknowledgementFromTheLiveClient()
    {
        using var fixtureCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        fixtureCancellation.CancelAfter(TimeSpan.FromSeconds(6));
        var cancellationToken = fixtureCancellation.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
                (await ReadEnvelopeAsync(stream, cancellationToken)).PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                SessionToken = "timing-session", TickRateHz = 20, GameMode = GameMode.SinglePlayer,
            } }, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } }, cancellationToken);
            var first = Assert.Single((await ReadEnvelopeAsync(stream, cancellationToken)).CommandBatch.Commands);
            var second = Assert.Single((await ReadEnvelopeAsync(stream, cancellationToken)).CommandBatch.Commands);
            Assert.Equal(first, second);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult {
                    ClientSequence = first.ClientSequence,
                    Status = CommandStatus.Accepted,
                    AppliedTick = first.RequestedTick,
                } },
            } }, cancellationToken);
        }, cancellationToken);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "timing-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(20),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(20),
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        }) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(cancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(cancellationToken);
        await Task.Delay(80, cancellationToken);
        await client.PollAsync(cancellationToken);
        await serverTask.WaitAsync(cancellationToken);

        for (var attempt = 0; attempt < 100 && recorder.CaptureReport().Acknowledgements.Count == 0; attempt++)
            await Task.Delay(10, cancellationToken);
        var report = recorder.CaptureReport();
        Assert.Equal(2, report.Commands.Count);
        Assert.Equal(report.Commands[0].ClientSequence, report.Commands[1].ClientSequence);
        Assert.Equal(report.Commands[0].RequestedTick, report.Commands[1].RequestedTick);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.FirstDispatch, report.Commands[0].DispatchKind);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.Retransmission, report.Commands[1].DispatchKind);
        Assert.Equal(AuthoritativeClientWriteOutcome.Completed, report.Commands[0].WriteOutcome);
        var acknowledgement = Assert.Single(report.Acknowledgements);
        Assert.Equal(report.Commands[0].ClientSequence, acknowledgement.ClientSequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        Assert.True(acknowledgement.TrackedPendingCommand);
        Assert.Equal(report.Commands[0].TransportGeneration, acknowledgement.TransportGeneration);
    }

    [Fact]
    public async Task CapturesCurrentGenerationProbeReplyAndSnapshotReceipt()
    {
        using var fixtureCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        fixtureCancellation.CancelAfter(TimeSpan.FromSeconds(6));
        var cancellationToken = fixtureCancellation.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        ulong timingRequestId = 0;
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
                (await ReadEnvelopeAsync(stream, cancellationToken)).PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                SessionToken = "probe-session", TickRateHz = 20, GameMode = GameMode.SinglePlayer,
                CommandSchedulingMode = CommandSchedulingMode.ClockGated,
            } }, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } }, cancellationToken);
            var request = (await ReadEnvelopeAsync(stream, cancellationToken)).SnapshotRequest;
            timingRequestId = request.TimingRequestId;
            Assert.NotEqual(0UL, timingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 101 } }, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope { TimingReply = new TimingReply {
                RequestId = timingRequestId,
            } }, cancellationToken);
        }, cancellationToken);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "timing-probe-test", "0.1.0", "content-hash") {
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        }) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(cancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(cancellationToken);
        await serverTask.WaitAsync(cancellationToken);

        for (var attempt = 0; attempt < 100 && recorder.CaptureReport().ProbeOutcomes.Count == 0; attempt++)
            await Task.Delay(10, cancellationToken);
        var report = recorder.CaptureReport();
        var sent = Assert.Single(report.ProbeSent);
        var outcome = Assert.Single(report.ProbeOutcomes);
        Assert.Equal(timingRequestId, sent.TimingRequestId);
        Assert.Equal(timingRequestId, outcome.TimingRequestId);
        Assert.Equal(AuthoritativeClientProbeOutcome.Reply, outcome.Outcome);
        Assert.Equal(sent.TransportGeneration, outcome.TransportGeneration);
        Assert.Contains(report.Snapshots, snapshot => snapshot.SnapshotTick == 101
            && snapshot.PendingTimingRequestIdAtReceipt == timingRequestId);
    }

    private static async Task WriteEnvelopeAsync(
        NetworkStream stream,
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<Envelope> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var payload = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
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
