using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientTimingPipelineRegressionTests
{
    [Fact]
    public async Task CalibratedRealtimeBurstSendsSeparateFrozenEnvelopesBeforeEitherAcknowledgement()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await SendHandshakeAsync(stream, CommandSchedulingMode.ClockGated, 100, timeout.Token);
            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, probe.PayloadCase);
            Assert.NotEqual(0UL, probe.SnapshotRequest.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } }, timeout.Token);
            await WriteEnvelopeAsync(stream, new Envelope { TimingReply = new TimingReply {
                RequestId = probe.SnapshotRequest.TimingRequestId,
            } }, timeout.Token);

            var firstEnvelope = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandBatch, firstEnvelope.PayloadCase);
            var first = Assert.Single(firstEnvelope.CommandBatch.Commands);
            // Withhold the first ACK until a distinct second envelope arrives.
            // This fixture characterizes the client, not authoritative admission.
            var secondEnvelope = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandBatch, secondEnvelope.PayloadCase);
            var second = Assert.Single(secondEnvelope.CommandBatch.Commands);
            foreach (var command in new[] { first, second }) {
                await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                    Results = { new CommandResult {
                        ClientSequence = command.ClientSequence, Status = CommandStatus.Accepted,
                        AppliedTick = command.RequestedTick, ServerReceiptSequence = command.ClientSequence,
                    } },
                } }, timeout.Token);
            }
            return new[] { first, second };
        }, timeout.Token);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(ConnectionOptions(listener) with {
            MinimumRetryTimeout = TimeSpan.FromMinutes(1),
            MaximumRetryTimeout = TimeSpan.FromMinutes(1),
        }) { CommandTimingRecorder = recorder };
        Exception? primaryFailure = null;
        try {
            await client.ConnectAsync(timeout.Token);
            var firstSequence = client.QueueRealtime(new Command {
                SetFriendlyModeRequested = new SetFriendlyModeRequested { FriendlyMode = false },
            }, 100);
            var secondSequence = client.QueueRealtime(new Command {
                SetFriendlyModeRequested = new SetFriendlyModeRequested { FriendlyMode = false },
            }, 100);
            Assert.Equal(2, client.PendingCommandCount);
            await client.PollAsync(timeout.Token);
            Assert.Empty(recorder.CaptureReport().Commands);
            await WaitForBurstEvidenceAsync(() => recorder.CaptureReport().ProbeOutcomes.Any(probe =>
                probe.Outcome == AuthoritativeClientProbeOutcome.Reply && probe.TimingCalibrationReady), timeout.Token);
            await client.PollAsync(timeout.Token);
            var commands = await serverTask.WaitAsync(timeout.Token);
            await WaitForBurstEvidenceAsync(() => client.PendingCommandCount == 0
                && recorder.CaptureReport().Acknowledgements.Count == 2, timeout.Token);

            Assert.Equal(new[] { firstSequence, secondSequence }, commands.Select(command => command.ClientSequence));
            var report = recorder.CaptureReport();
            Assert.Equal(0, report.DroppedRecordCount);
            Assert.Equal(new[] { firstSequence, secondSequence },
                report.Commands.Select(command => command.ClientSequence));
            for (var index = 0; index < commands.Length; index++) {
                Assert.Equal(100UL, report.Commands[index].ObservedTick);
                Assert.True(report.Commands[index].TimingCalibrationReady);
                Assert.Equal(AuthoritativeClientCommandDispatchKind.FirstDispatch, report.Commands[index].DispatchKind);
                Assert.Equal(AuthoritativeClientWriteOutcome.Completed, report.Commands[index].WriteOutcome);
                Assert.Equal(commands[index].RequestedTick, report.Commands[index].RequestedTick);
                Assert.True(commands[index].RequestedTick > 100);
                Assert.Equal(Command.IntentOneofCase.SetFriendlyModeRequested, commands[index].IntentCase);
                Assert.False(commands[index].SetFriendlyModeRequested.FriendlyMode);
            }
            Assert.Equal(new[] { firstSequence, secondSequence },
                report.Acknowledgements.Select(acknowledgement => acknowledgement.ClientSequence));
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            timeout.Cancel();
            try {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            } catch (Exception cleanupFailure) {
                if (primaryFailure is null)
                    throw;
                try { primaryFailure.Data["RealtimeBurstServerCleanupFailure"] = cleanupFailure; } catch { }
            }
        }
    }

    private static async Task WaitForBurstEvidenceAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
            await Task.Delay(10, cancellationToken);
    }

    [Fact]
    public async Task CapturesTheExplicitRequestedTickFromQueueAndPoll()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await SendHandshakeAsync(stream, CommandSchedulingMode.Immediate, 100, timeout.Token);
            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            Assert.Equal(321UL, command.RequestedTick);
            await WriteEnvelopeAsync(stream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult {
                    ClientSequence = command.ClientSequence,
                    Status = CommandStatus.Accepted,
                    AppliedTick = command.RequestedTick,
                } },
            } }, timeout.Token);
        }, timeout.Token);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(ConnectionOptions(listener)) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(timeout.Token);
        var sequence = client.Queue(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            requestedTick: 321);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);

        var commandTiming = Assert.Single(recorder.CaptureReport().Commands);
        Assert.Equal(sequence, commandTiming.ClientSequence);
        Assert.Null(commandTiming.ObservedTick);
        Assert.Equal(321UL, commandTiming.RequestedTick);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.FirstDispatch, commandTiming.DispatchKind);
    }

    [Fact]
    public async Task CapturesFrozenRetryAcrossReconnectGenerations()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstCommandSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using (var connection = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = connection.GetStream();
                await SendHandshakeAsync(stream, CommandSchedulingMode.Immediate, 100, timeout.Token);
                _ = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
                firstCommandSeen.SetResult();
            }

            using var resumed = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumed.GetStream();
            await SendHandshakeAsync(resumedStream, CommandSchedulingMode.Immediate, 900, timeout.Token);
            var retry = Assert.Single((await ReadEnvelopeAsync(resumedStream, timeout.Token)).CommandBatch.Commands);
            await WriteEnvelopeAsync(resumedStream, new Envelope { CommandAck = new CommandAck {
                Results = { new CommandResult {
                    ClientSequence = retry.ClientSequence,
                    Status = CommandStatus.Duplicate,
                    AppliedTick = retry.RequestedTick,
                } },
            } }, timeout.Token);
        }, timeout.Token);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(ConnectionOptions(listener)) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(timeout.Token);
        var sequence = client.Queue(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            requestedTick: 654);
        await client.PollAsync(timeout.Token);
        await firstCommandSeen.Task.WaitAsync(timeout.Token);
        await client.ReconnectAsync(timeout.Token);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);

        var commands = recorder.CaptureReport().Commands
            .Where(command => command.ClientSequence == sequence)
            .ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Equal(654UL, commands[0].RequestedTick);
        Assert.Equal(commands[0].RequestedTick, commands[1].RequestedTick);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.FirstDispatch, commands[0].DispatchKind);
        Assert.Equal(AuthoritativeClientCommandDispatchKind.Retransmission, commands[1].DispatchKind);
        Assert.NotEqual(commands[0].TransportGeneration, commands[1].TransportGeneration);
    }

    [Fact]
    public async Task CapturesFailedWriteOutcomeWithoutChangingTheTransportFixture()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await SendHandshakeAsync(stream, CommandSchedulingMode.Immediate, 100, timeout.Token);
            await releaseServer.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(ConnectionOptions(listener)) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(timeout.Token);
        client.Queue(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            requestedTick: 654);

        using var replacementListener = new TcpListener(IPAddress.Loopback, 0);
        replacementListener.Start();
        using var replacementClient = new TcpClient();
        var replacementConnect = replacementClient.ConnectAsync(
            IPAddress.Loopback,
            ((IPEndPoint)replacementListener.LocalEndpoint).Port,
            timeout.Token);
        using var replacementServer = await replacementListener.AcceptTcpClientAsync(timeout.Token);
        await replacementConnect;
        var replacementStream = replacementClient.GetStream();
        var streamField = typeof(AuthoritativeClient).GetField(
            "stream", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AuthoritativeClient).FullName, "stream");
        streamField.SetValue(client, replacementStream);
        replacementStream.Dispose();

        await Assert.ThrowsAnyAsync<Exception>(() => client.PollAsync(timeout.Token));
        releaseServer.TrySetResult();
        await serverTask.WaitAsync(timeout.Token);

        var commandTiming = Assert.Single(recorder.CaptureReport().Commands);
        Assert.Equal(AuthoritativeClientWriteOutcome.Failed, commandTiming.WriteOutcome);
        Assert.Equal(654UL, commandTiming.RequestedTick);
    }

    [Fact]
    public async Task CapturesProbeExpiryWithItsCorrelatedRequestId()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestSeen = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await SendHandshakeAsync(stream, CommandSchedulingMode.ClockGated, 100, timeout.Token);
            var request = (await ReadEnvelopeAsync(stream, timeout.Token)).SnapshotRequest;
            requestSeen.SetResult(request.TimingRequestId);
            await releaseServer.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(ConnectionOptions(listener) with {
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        }) {
            CommandTimingRecorder = recorder,
        };
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 100);
        await client.PollAsync(timeout.Token);
        var requestId = await requestSeen.Task.WaitAsync(timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(2150), timeout.Token);
        await client.PollAsync(timeout.Token);
        releaseServer.TrySetResult();
        await serverTask.WaitAsync(timeout.Token);

        var outcome = recorder.CaptureReport().ProbeOutcomes.Single(probe =>
            probe.TimingRequestId == requestId
            && probe.Outcome == AuthoritativeClientProbeOutcome.Expired);
        Assert.Equal(requestId, outcome.TimingRequestId);
        Assert.Null(outcome.RoundTripMilliseconds);
    }

    private static ClientConnectionOptions ConnectionOptions(TcpListener listener)
        => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "timing-regression", "0.1.0", "content-hash") {
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(25),
        };

    private static async Task SendHandshakeAsync(
        NetworkStream stream,
        CommandSchedulingMode schedulingMode,
        ulong snapshotTick,
        CancellationToken cancellationToken)
    {
        var hello = await ReadEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, hello.PayloadCase);
        await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            SessionToken = "timing-regression-session",
            TickRateHz = 20,
            GameMode = GameMode.SinglePlayer,
            CommandSchedulingMode = schedulingMode,
        } }, cancellationToken);
        await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = snapshotTick } }, cancellationToken);
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

    private static async Task<Envelope> ReadEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Assert.InRange(length, 1, AuthoritativeProtocolLimits.MaximumEnvelopeBytes);
        var payload = new byte[length];
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
