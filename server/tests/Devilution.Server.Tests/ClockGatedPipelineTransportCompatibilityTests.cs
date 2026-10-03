using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/**
 * Exercises the physical-clock pipeline at the transport boundary. These
 * cases intentionally use loopback TCP so admission, retained response
 * weight, half-close handling, and host shutdown are observed together.
 */
public sealed class ClockGatedPipelineTransportCompatibilityTests
{
    [Fact]
    public async Task HalfCloseAfterTwoQueuedFutureEnvelopesPreservesOrderedResponses()
    {
        ulong physicalTick = 100;
        var executor = new RecordingExecutor();
        var admissions = new AdmissionGate();
        var server = CreateServer(
            executor,
            () => Volatile.Read(ref physicalTick),
            admissions);
        Exception? primaryFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, "half-close", cancellationToken);
            await ReadHandshakeSnapshotAsync(stream, 100, cancellationToken);

            await SendCommandAsync(
                stream,
                Attack(1, requestedTick: 120, targetEntityId: 1),
                cancellationToken);
            var firstAdmission = await admissions.WaitForAsync(1, cancellationToken);
            Assert.Equal(100UL, firstAdmission.ActualAdmissionTick);
            Assert.Equal(120UL, firstAdmission.RequestedTick);

            await SendCommandAsync(
                stream,
                Attack(2, requestedTick: 125, targetEntityId: 2),
                cancellationToken);
            var secondAdmission = await admissions.WaitForAsync(2, cancellationToken);
            Assert.Equal(100UL, secondAdmission.ActualAdmissionTick);
            Assert.Equal(125UL, secondAdmission.RequestedTick);
            Assert.Equal(2, server.PendingCommandCount);
            Assert.Empty(executor.AppliedTicks);

            client.Client.Shutdown(SocketShutdown.Send);
            Volatile.Write(ref physicalTick, 130);

            var firstAcknowledgement = await ReadCommandAckAsync(stream, cancellationToken);
            Assert.Equal(1UL, firstAcknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, firstAcknowledgement.Status);
            Assert.Equal(120UL, firstAcknowledgement.AppliedTick);
            Assert.Equal(130UL, await ReadSnapshotTickAsync(stream, cancellationToken));

            var secondAcknowledgement = await ReadCommandAckAsync(stream, cancellationToken);
            Assert.Equal(2UL, secondAcknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, secondAcknowledgement.Status);
            Assert.Equal(125UL, secondAcknowledgement.AppliedTick);
            Assert.Equal(130UL, await ReadSnapshotTickAsync(stream, cancellationToken));

            Assert.Equal([120UL, 125UL], executor.AppliedTicks);
            Assert.Equal(0, server.PendingCommandCount);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            Volatile.Write(ref physicalTick, 130);
            await FinishCleanupAsync(server, primaryFailure, null, nameof(HalfCloseAfterTwoQueuedFutureEnvelopesPreservesOrderedResponses));
        }
    }

    [Fact]
    public async Task GlobalResponseBudgetRejectsSnapshotProbeUntilOwnerReleasesWeight()
    {
        ulong physicalTick = 100;
        const string ownerToken = "budget-owner";
        const string rejectedToken = "budget-probe";
        const string freshToken = "budget-fresh";
        var saveRoot = CreateTempRoot("clock-gated-response-budget");
        var saveStore = new AuthoritativeSaveStore(saveRoot);
        var executor = new RecordingExecutor();
        var admissions = new AdmissionGate();
        var server = CreateServer(
            executor,
            () => Volatile.Read(ref physicalTick),
            admissions,
            saveStore,
            maximumPendingCommands: 1);
        Exception? primaryFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;

            using var ownerClient = new TcpClient();
            await ownerClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var ownerStream = ownerClient.GetStream();
            await SendHelloAsync(ownerStream, ownerToken, cancellationToken);
            await ReadHandshakeSnapshotAsync(ownerStream, 100, cancellationToken);
            await SendCommandAsync(
                ownerStream,
                Attack(1, requestedTick: 125, targetEntityId: 1),
                cancellationToken);
            var ownerAdmission = await admissions.WaitForAsync(1, cancellationToken);
            Assert.Equal(100UL, ownerAdmission.ActualAdmissionTick);
            Assert.Equal(1, server.PendingCommandCount);
            Assert.Empty(executor.AppliedTicks);
            var ownerResponse = ReadAsync(ownerStream, cancellationToken);

            using var probeClient = new TcpClient();
            await probeClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var probeStream = probeClient.GetStream();
            await SendHelloAsync(probeStream, rejectedToken, cancellationToken);
            await ReadHandshakeSnapshotAsync(probeStream, 100, cancellationToken);
            await EnvelopeCodec.WriteAsync(probeStream, new Envelope {
                SnapshotRequest = new Envelope.Types.SnapshotRequest {
                    TimingRequestId = 77,
                },
            }, cancellationToken);

            var probeResponse = await ReadAsync(probeStream, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Error, probeResponse!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, probeResponse.Error.Code);
            Assert.Contains("capacity", probeResponse.Error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(100UL, Volatile.Read(ref physicalTick));
            Assert.Equal(1, server.PendingCommandCount);
            Assert.Empty(executor.AppliedTicks);
            Assert.Empty(new AuthoritativeCommandReceiptStore(saveRoot)
                .Load(SessionIdForToken(rejectedToken)));

            Volatile.Write(ref physicalTick, 130);
            var ownerAcknowledgement = await ownerResponse.WaitAsync(cancellationToken);
            var ownerResult = Assert.Single(ownerAcknowledgement!.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, ownerResult.Status);
            Assert.Equal(125UL, ownerResult.AppliedTick);
            Assert.Equal(130UL, await ReadSnapshotTickAsync(ownerStream, cancellationToken));
            Assert.Equal(0, server.PendingCommandCount);
            Assert.Single(new AuthoritativeCommandReceiptStore(saveRoot)
                .Load(SessionIdForToken(ownerToken)));

            using var freshClient = new TcpClient();
            await freshClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var freshStream = freshClient.GetStream();
            await SendHelloAsync(freshStream, freshToken, cancellationToken);
            await ReadHandshakeSnapshotAsync(freshStream, 130, cancellationToken);
            await SendCommandAsync(
                freshStream,
                Attack(1, requestedTick: 130, targetEntityId: 3),
                cancellationToken);
            var freshAcknowledgement = await ReadCommandAckAsync(freshStream, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, freshAcknowledgement.Status);
            Assert.Equal(130UL, freshAcknowledgement.AppliedTick);
            Assert.Equal(130UL, await ReadSnapshotTickAsync(freshStream, cancellationToken));
            Assert.Equal([1U, 3U], executor.TargetEntityIds);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            Volatile.Write(ref physicalTick, 130);
            await FinishCleanupAsync(server, primaryFailure, saveRoot, nameof(GlobalResponseBudgetRejectsSnapshotProbeUntilOwnerReleasesWeight));
        }
    }

    [Fact]
    public async Task ShutdownJoinsReaderAndWriterWhileFutureResponseWaits()
    {
        ulong physicalTick = 100;
        var executor = new RecordingExecutor();
        var admissions = new AdmissionGate();
        var server = CreateServer(
            executor,
            () => Volatile.Read(ref physicalTick),
            admissions);
        Exception? primaryFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, "shutdown-future", cancellationToken);
            await ReadHandshakeSnapshotAsync(stream, 100, cancellationToken);
            await SendCommandAsync(
                stream,
                Attack(1, requestedTick: ulong.MaxValue, targetEntityId: 1),
                cancellationToken);
            var admission = await admissions.WaitForAsync(1, cancellationToken);
            Assert.Equal(100UL, admission.ActualAdmissionTick);
            Assert.Equal(1, server.PendingCommandCount);

            // The server must cancel both its blocked reader and future-gated
            // writer; this deadline deliberately does not use the test token.
            await server.DisposeAsync().AsTask().WaitAsync(
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            Assert.Equal(0, server.PendingCommandCount);
            Assert.Empty(executor.AppliedTicks);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            Volatile.Write(ref physicalTick, 100);
            await FinishCleanupAsync(server, primaryFailure, null, nameof(ShutdownJoinsReaderAndWriterWhileFutureResponseWaits));
        }
    }

    private static AuthoritativeTcpServer CreateServer(
        RecordingExecutor executor,
        Func<ulong> physicalTickProvider,
        AdmissionGate admissions,
        AuthoritativeSaveStore? saveStore = null,
        int maximumPendingCommands = 1024)
        => new(
            new AuthoritativeCommandServer(executor),
            Handshake,
            physicalTickProvider,
            snapshotProvider: executor,
            saveStore: saveStore,
            saveProvider: saveStore is null ? null : new NoOpSaveProvider()) {
            UseClockGatedCommandScheduling = true,
            MaximumPendingCommands = maximumPendingCommands,
            CommandTimingRecorder = new HostCommandTimingRecorder(),
            CommandTimingDrainObserver = admissions.Record,
        };

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        return timeout;
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

    private static async ValueTask SendHelloAsync(
        Stream stream,
        string resumeToken,
        CancellationToken cancellationToken)
        => await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "clock-gated-transport-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);

    private static async Task ReadHandshakeSnapshotAsync(
        Stream stream,
        ulong expectedTick,
        CancellationToken cancellationToken)
    {
        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        Assert.Equal(CommandSchedulingMode.ClockGated, hello.ServerHello.CommandSchedulingMode);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        Assert.Equal(expectedTick, snapshot.Snapshot.Tick);
    }

    private static ValueTask SendCommandAsync(
        Stream stream,
        Command command,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);

    private static Task<Envelope?> ReadAsync(Stream stream, CancellationToken cancellationToken)
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

    private static ProtocolHandshake Handshake => new(
        new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));

    private static string SessionIdForToken(string resumeToken)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(resumeToken))).ToLowerInvariant();

    private static string CreateTempRoot(string stem)
        => Path.Combine(
            Path.GetFullPath(Path.GetTempPath()),
            $"Godevilution-{stem}-{Guid.NewGuid():N}");

    private static async Task FinishCleanupAsync(
        AuthoritativeTcpServer server,
        Exception? primaryFailure,
        string? ownedTempRoot,
        string testName)
    {
        var failures = new List<Exception>();
        try {
            await server.DisposeAsync().AsTask().WaitAsync(
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
        } catch (Exception exception) {
            failures.Add(exception);
        }

        if (ownedTempRoot is not null) {
            try {
                DeleteOwnedTempDirectory(ownedTempRoot);
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }

        if (primaryFailure is not null) {
            for (var index = 0; index < failures.Count; index++) {
                try {
                    primaryFailure.Data[$"{testName}.CleanupFailure:{index}"] = failures[index];
                } catch {
                }
            }
            return;
        }

        if (failures.Count == 0)
            return;
        for (var index = 1; index < failures.Count; index++) {
            try {
                failures[0].Data[$"{testName}.CleanupFailure:{index}"] = failures[index];
            } catch {
            }
        }
        ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }

    private static void DeleteOwnedTempDirectory(string path)
    {
        var absolutePath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var tempChildPrefix = tempRoot.EndsWith(Path.DirectorySeparatorChar)
            ? tempRoot
            : tempRoot + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(tempChildPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete non-temporary path '{absolutePath}'.");
        if (!Directory.Exists(absolutePath))
            return;

        var pending = new Stack<string>();
        pending.Push(absolutePath);
        while (pending.Count != 0) {
            var current = pending.Pop();
            var currentInfo = new DirectoryInfo(current);
            if ((currentInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Refusing to traverse reparse point '{current}'.");
            foreach (var file in Directory.EnumerateFiles(current)) {
                var fileInfo = new FileInfo(file);
                if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Refusing to delete reparse point '{file}'.");
            }
            foreach (var directory in Directory.EnumerateDirectories(current))
                pending.Push(directory);
        }

        Directory.Delete(absolutePath, recursive: true);
    }

    private sealed class AdmissionGate
    {
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<Admission>> pending = [];

        public void Record(HostCommandTimingDrain drain)
        {
            foreach (var record in drain.Records) {
                if (record.Kind != "command-admission"
                    || record.ClientSequence is not { } sequence
                    || record.RequestedTick is not { } requestedTick
                    || record.ActualAdmissionTick is not { } actualAdmissionTick)
                    continue;
                pending.GetOrAdd(
                    sequence,
                    static _ => new TaskCompletionSource<Admission>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                    .TrySetResult(new Admission(sequence, requestedTick, actualAdmissionTick));
            }
        }

        public Task<Admission> WaitForAsync(ulong clientSequence, CancellationToken cancellationToken)
            => pending.GetOrAdd(
                    clientSequence,
                    static _ => new TaskCompletionSource<Admission>(
                        TaskCreationOptions.RunContinuationsAsynchronously))
                .Task.WaitAsync(cancellationToken);
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
        public ConcurrentQueue<uint> TargetEntityIds { get; } = [];

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
        {
            AppliedTicks.Enqueue(appliedTick);
            if (command.IntentCase == Command.IntentOneofCase.AttackRequested)
                TargetEntityIds.Enqueue(command.AttackRequested.TargetEntityId);
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
    }

    private sealed class NoOpSaveProvider : IAuthoritativeSaveProvider
    {
        public string ExportPlayerSave(string sessionId) => "{}";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }
    }
}
