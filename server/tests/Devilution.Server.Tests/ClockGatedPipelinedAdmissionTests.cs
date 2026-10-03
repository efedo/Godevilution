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

/** Locks down same-connection admission while an earlier future command waits. */
public sealed class ClockGatedPipelinedAdmissionTests
{
    [Fact]
    public async Task ConsecutiveFutureEnvelopesAreAdmittedBeforeClockAdvanceAndAckInWireOrder()
    {
        ulong physicalTick = 100;
        var executor = new RecordingExecutor();
        var admissions = new ScalarAdmissionRecorder();
        var server = new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(executor),
            Handshake,
            () => Volatile.Read(ref physicalTick),
            snapshotProvider: executor) {
            UseClockGatedCommandScheduling = true,
            CommandTimingRecorder = new HostCommandTimingRecorder(),
            CommandTimingDrainObserver = admissions.Record,
        };
        server.Start();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var cancellationToken = timeout.Token;
        Exception? primaryFailure = null;
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken);
            await ReadHandshakeAsync(stream, cancellationToken);

            await SendCommandAsync(
                stream,
                CreateAttack(1, 120, 1),
                cancellationToken);
            var firstAdmission = await admissions.WaitForAsync(1, cancellationToken);
            Assert.Equal(100UL, firstAdmission.ActualAdmissionTick);
            Assert.Equal(120UL, firstAdmission.RequestedTick);
            Assert.Equal(1, server.PendingCommandCount);
            Assert.Empty(executor.AppliedTicks);
            Assert.False(admissions.CaptureWasIncomplete);

            await SendCommandAsync(
                stream,
                CreateAttack(2, 125, 2),
                cancellationToken);
            var secondAdmission = await admissions.WaitForAsync(
                2,
                cancellationToken,
                () => $"physicalTick={Volatile.Read(ref physicalTick)}, "
                    + $"pending={server.PendingCommandCount}, "
                    + $"admissions=[{string.Join(',', admissions.ClientSequences)}], "
                    + $"captureIncomplete={admissions.CaptureWasIncomplete}");
            Assert.Equal(100UL, secondAdmission.ActualAdmissionTick);
            Assert.Equal(125UL, secondAdmission.RequestedTick);
            Assert.Equal(2, server.PendingCommandCount);
            Assert.Equal(100UL, Volatile.Read(ref physicalTick));
            Assert.Empty(executor.AppliedTicks);
            Assert.False(admissions.CaptureWasIncomplete);

            Volatile.Write(ref physicalTick, 130);

            var firstAcknowledgement = await ReadCommandAckAsync(stream, cancellationToken);
            Assert.Equal(1UL, firstAcknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, firstAcknowledgement.Status);
            Assert.Equal(120UL, firstAcknowledgement.AppliedTick);
            await ReadSnapshotAsync(stream, cancellationToken);

            var secondAcknowledgement = await ReadCommandAckAsync(stream, cancellationToken);
            Assert.Equal(2UL, secondAcknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, secondAcknowledgement.Status);
            Assert.Equal(125UL, secondAcknowledgement.AppliedTick);
            await ReadSnapshotAsync(stream, cancellationToken);

            Assert.Equal([120UL, 125UL], executor.AppliedTicks);
            Assert.Equal(0, server.PendingCommandCount);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            // This cleanup deadline is independent of the test token. It must
            // release future-command admission even when the regression hangs.
            Volatile.Write(ref physicalTick, 130);
            try {
                await server.DisposeAsync().AsTask().WaitAsync(
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);
            } catch (Exception cleanupFailure) {
                if (primaryFailure is null)
                    throw;
                try {
                    primaryFailure.Data["ClockGatedPipelinedAdmissionTests.CleanupFailure"] =
                        cleanupFailure;
                } catch {
                }
            }
        }
    }

    private static Command CreateAttack(
        ulong clientSequence,
        ulong requestedTick,
        uint targetEntityId)
        => new() {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = targetEntityId },
        };

    private static async ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "pipelined-admission-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = "pipelined-admission-session",
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);
    }

    private static async Task ReadHandshakeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        Assert.Equal(
            CommandSchedulingMode.ClockGated,
            hello.ServerHello.CommandSchedulingMode);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        Assert.Equal(100UL, snapshot.Snapshot.Tick);
    }

    private static ValueTask SendCommandAsync(
        Stream stream,
        Command command,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);

    private static async Task<CommandResult> ReadCommandAckAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement!.PayloadCase);
        return Assert.Single(acknowledgement.CommandAck.Results);
    }

    private static async Task ReadSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
    }

    private static ProtocolHandshake Handshake => new(
        new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));

    private sealed class ScalarAdmissionRecorder
    {
        private readonly ConcurrentQueue<Admission> admissions = [];
        private readonly TaskCompletionSource<Admission> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Admission> second =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CaptureWasIncomplete { get; private set; }

        public ulong[] ClientSequences
            => admissions.Select(admission => admission.ClientSequence).ToArray();

        public void Record(HostCommandTimingDrain drain)
        {
            if (drain.DroppedCount != 0 || drain.CaptureFaulted)
                CaptureWasIncomplete = true;
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
                admissions.Enqueue(admission);
                if (clientSequence == 1)
                    first.TrySetResult(admission);
                else if (clientSequence == 2)
                    second.TrySetResult(admission);
            }
        }

        public Task<Admission> WaitForAsync(
            ulong clientSequence,
            CancellationToken cancellationToken,
            Func<string>? timeoutDiagnostic = null)
            => WaitForCoreAsync(
                clientSequence == 1 ? first : second,
                cancellationToken,
                timeoutDiagnostic);

        private static async Task<Admission> WaitForCoreAsync(
            TaskCompletionSource<Admission> completion,
            CancellationToken cancellationToken,
            Func<string>? timeoutDiagnostic)
        {
            try {
                return await completion.Task.WaitAsync(cancellationToken);
            } catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested
                && timeoutDiagnostic is not null) {
                throw new TimeoutException(
                    $"Timed out waiting for clock-gated admission: {timeoutDiagnostic()}");
            }
        }
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

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
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
