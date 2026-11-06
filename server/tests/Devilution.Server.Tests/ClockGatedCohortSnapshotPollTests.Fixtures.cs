using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/** Verifies that one clock-gated cohort snapshot poll has one durable boundary. */
public sealed partial class ClockGatedCohortSnapshotPollTests
{

    private static AuthoritativeTcpServer CreateServer(
        SnapshotPollAdapter adapter,
        IAuthoritativeClock clock,
        AuthoritativeCohortStore store,
        bool includeProjectionProviders = true,
        Func<Stream, Stream>? clockGatedTransportDecorator = null,
        HostCommandTimingRecorder? commandTimingRecorder = null,
        Action<string, ulong>? commitBoundaryResponseReadyObserverForTests = null,
        TimeSpan? checkpointInterval = null,
        TimeProvider? checkpointTime = null,
        HostPerformanceRecorder? performanceRecorder = null,
        Action<Exception>? checkpointFailureObserver = null)
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "cohort-poll-tests",
            "0.1.0",
            ContentHash,
            20) {
            RulesetIdentityHash = RulesetHash,
            GameMode = AuthoritativeGameMode.Multiplayer,
        });
        var commandServer = new AuthoritativeCommandServer(adapter);
        return includeProjectionProviders
            ? new AuthoritativeTcpServer(
                commandServer,
                handshake,
                clock,
                adapter,
                port: 0,
                address: IPAddress.Loopback) {
                CohortStore = store,
                UseClockGatedCommandScheduling = true,
                ClockGatedTransportDecorator = clockGatedTransportDecorator,
                CommandTimingRecorder = commandTimingRecorder,
                CommitBoundaryResponseReadyObserverForTests = commitBoundaryResponseReadyObserverForTests,
                CohortCheckpointInterval = checkpointInterval,
                CheckpointTimeProvider = checkpointTime ?? TimeProvider.System,
                PerformanceRecorder = performanceRecorder,
                CheckpointFailureObserver = checkpointFailureObserver,
            }
            : new AuthoritativeTcpServer(
                commandServer,
                handshake,
                clock,
                port: 0,
                address: IPAddress.Loopback,
                snapshotProvider: null,
                eventProvider: null,
                saveProvider: adapter,
                participantInitializer: adapter) {
                CohortStore = store,
                UseClockGatedCommandScheduling = true,
                ClockGatedTransportDecorator = clockGatedTransportDecorator,
                CommandTimingRecorder = commandTimingRecorder,
                CommitBoundaryResponseReadyObserverForTests = commitBoundaryResponseReadyObserverForTests,
                CohortCheckpointInterval = checkpointInterval,
                CheckpointTimeProvider = checkpointTime ?? TimeProvider.System,
                PerformanceRecorder = performanceRecorder,
                CheckpointFailureObserver = checkpointFailureObserver,
            };
    }

    private static async Task ReadHandshakeAsync(
        Stream stream,
        bool hasSnapshot,
        CancellationToken cancellationToken,
        string resumeToken = "cohort-poll-session")
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "cohort-poll-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = ContentHash,
                RulesetIdentityHash = RulesetHash,
                ResumeToken = resumeToken,
                RequestedCharacterName = "PollTester",
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        Assert.True(hello.ServerHello.SupportsClockOnlyProbes);
        if (hasSnapshot) {
            var snapshot = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
            Assert.Equal(FixedTick, snapshot.Snapshot.Tick);
        }
    }

    private static async Task<Snapshot> ReadHandshakeSnapshotAsync(
        Stream stream,
        string resumeToken,
        string characterName,
        CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "cohort-poll-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = ContentHash,
                RulesetIdentityHash = RulesetHash,
                ResumeToken = resumeToken,
                RequestedCharacterName = characterName,
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        Assert.True(hello.ServerHello.SupportsClockOnlyProbes);
        var snapshot = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
        return snapshot.Snapshot;
    }

    private static ValueTask SendSnapshotRequestAsync(
        Stream stream,
        ulong timingRequestId,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest {
                TimingRequestId = timingRequestId,
            },
        }, cancellationToken);

    private static ValueTask SendClockProbeAsync(
        Stream stream,
        ulong requestId,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClockProbeRequest = new ClockProbeRequest { RequestId = requestId },
        }, cancellationToken);

    private static ValueTask SendCommandAsync(Stream stream, CancellationToken cancellationToken)
        => SendCommandAtTickAsync(stream, FixedTick, cancellationToken);

    private static ValueTask SendCommandAtTickAsync(
        Stream stream,
        ulong requestedTick,
        CancellationToken cancellationToken)
        => SendCommandWithSequenceAsync(stream, 1, requestedTick, cancellationToken);

    private static ValueTask SendCommandWithSequenceAsync(
        Stream stream,
        ulong clientSequence,
        ulong requestedTick,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    CreateCommand(clientSequence, requestedTick),
                },
            },
        }, cancellationToken);

    private static ValueTask SendCommandBatchAsync(
        Stream stream,
        IReadOnlyList<Command> commands,
        CancellationToken cancellationToken)
    {
        var batch = new CommandBatch();
        batch.Commands.Add(commands);
        return EnvelopeCodec.WriteAsync(stream, new Envelope { CommandBatch = batch }, cancellationToken);
    }

    private static Command CreateCommand(ulong clientSequence, ulong requestedTick, uint? targetEntityId = null)
        => new() {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = targetEntityId ?? checked((uint)clientSequence) },
        };

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        return envelope ?? throw new XunitException("The cohort host closed before sending the expected response.");
    }

    private static async Task AssertConnectionClosesWithoutResponseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try {
            var response = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            Assert.Null(response);
        } catch (Exception exception) when (
            exception is IOException or SocketException or ObjectDisposedException) {
            // The host fences and closes the lane after authoritative failure.
        }
    }

    private static async Task DisposeServerAsync(
        AuthoritativeTcpServer server,
        Exception? expectedFailure)
    {
        var failure = await Record.ExceptionAsync(async () => await server.DisposeAsync());
        if (failure is not null)
            Assert.Same(expectedFailure, failure);
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
    }

    private static async Task WaitForCommandAdmissionRecordAsync(
        HostCommandTimingRecorder recorder,
        ulong clientSequence,
        CancellationToken cancellationToken)
    {
        var matchingRecords = 0;
        foreach (var record in recorder.Drain().Records) {
            if (record.Kind == "command-admission" && record.ClientSequence == clientSequence)
                matchingRecords++;
        }
        Assert.True(matchingRecords >= 1);
        while (matchingRecords < 2) {
            await WaitUntilAsync(() => recorder.Count > 0, cancellationToken);
            foreach (var record in recorder.Drain().Records) {
                if (record.Kind == "command-admission" && record.ClientSequence == clientSequence)
                    matchingRecords++;
            }
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "Godev-Cohort-Snapshot-Poll-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    public enum PollFailure
    {
        InvalidEventBatch,
        InvalidSnapshot,
        Publication,
    }

    private sealed class SignalStream(
        Stream inner,
        Func<bool> isArmed,
        Action onRead,
        Func<bool>? shouldBlockWrite = null,
        Action? onWriteBlocked = null,
        Action? onWriteCanceled = null,
        Task? releaseWrite = null,
        Action<CancellationToken>? onReadStarted = null,
        Func<bool>? failReadOnEof = null,
        Func<Envelope, bool>? shouldBlockEnvelope = null,
        Action<Envelope>? onEnvelopeWriteStarted = null,
        Action? onDisposed = null,
        Task? injectedReadFailureSignal = null,
        Action? onInjectedReadFailure = null,
        Exception? injectedReadFailureException = null) : Stream
    {
        private int? expectedPayloadLength;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            FailInjectedEof(read);
            Observe(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            FailInjectedEof(read);
            Observe(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            onReadStarted?.Invoke(cancellationToken);
            var read = await ReadWithInjectedFailureAsync(buffer, cancellationToken);
            FailInjectedEof(read);
            Observe(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            onReadStarted?.Invoke(cancellationToken);
            var read = await ReadWithInjectedFailureAsync(buffer.AsMemory(offset, count), cancellationToken);
            FailInjectedEof(read);
            Observe(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => WriteObservedAsync(buffer, cancellationToken);
        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => WriteObservedAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                inner.Dispose();
                onDisposed?.Invoke();
            }
            base.Dispose(disposing);
        }

        private void Observe(int read)
        {
            if (read > 0 && isArmed())
                onRead();
        }

        private void FailInjectedEof(int read)
        {
            if (read == 0 && failReadOnEof?.Invoke() == true)
                throw new IOException("Injected participant transport abort after EOF.");
        }

        private async ValueTask WriteObservedAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            var blockWrite = shouldBlockWrite?.Invoke() == true;
            var envelope = ObserveEnvelopeWrite(buffer);
            if (envelope is not null) {
                onEnvelopeWriteStarted?.Invoke(envelope);
                blockWrite |= shouldBlockEnvelope?.Invoke(envelope) == true;
            }
            if (blockWrite) {
                onWriteBlocked?.Invoke();
                try {
                    await (releaseWrite ?? throw new InvalidOperationException("A blocked write needs a release task.")).WaitAsync(cancellationToken);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    onWriteCanceled?.Invoke();
                    throw;
                }
            }
            await inner.WriteAsync(buffer, cancellationToken);
        }

        private async ValueTask<int> ReadWithInjectedFailureAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (injectedReadFailureSignal is not { } failureSignal)
                return await inner.ReadAsync(buffer, cancellationToken);

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var readTask = inner.ReadAsync(buffer, linkedCancellation.Token).AsTask();
            if (await Task.WhenAny(readTask, failureSignal) != readTask) {
                linkedCancellation.Cancel();
                try {
                    await readTask;
                } catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested) {
                } catch (IOException) when (linkedCancellation.IsCancellationRequested) {
                } catch (SocketException) when (linkedCancellation.IsCancellationRequested) {
                } catch (ObjectDisposedException) when (linkedCancellation.IsCancellationRequested) {
                }
                onInjectedReadFailure?.Invoke();
                throw injectedReadFailureException ?? new IOException("Injected pending participant read failure.");
            }
            return await readTask;
        }

        private Envelope? ObserveEnvelopeWrite(ReadOnlyMemory<byte> buffer)
        {
            if (expectedPayloadLength is null) {
                if (buffer.Length != sizeof(uint))
                    return null;
                var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);
                if (payloadLength == 0 || payloadLength > EnvelopeCodec.MaxEnvelopeBytes)
                    throw new InvalidDataException("The fixture observed an invalid outgoing envelope length.");
                expectedPayloadLength = checked((int)payloadLength);
                return null;
            }

            var expectedLength = expectedPayloadLength.Value;
            expectedPayloadLength = null;
            if (buffer.Length != expectedLength)
                throw new InvalidDataException("The fixture observed a split outgoing envelope payload.");
            return Envelope.Parser.ParseFrom(buffer.ToArray());
        }
    }

    private sealed class FixedClock(ulong tick) : IAuthoritativeClock
    {
        public ulong CurrentTick => tick;
    }

    private sealed class MutableClock(ulong tick) : IAuthoritativeClock
    {
        private long currentTick = checked((long)tick);

        public ulong CurrentTick => checked((ulong)Interlocked.Read(ref currentTick));

        public void Set(ulong value)
            => Interlocked.Exchange(ref currentTick, checked((long)value));
    }

    private sealed class SnapshotPollAdapter :
        IAuthoritativeGameHostAdapter,
        IAuthoritativeGameCohortAdapter
    {
        private readonly object synchronization = new();
        private readonly List<AuthoritativeModuleCohortParticipant> participants = [];
        private ulong tick;
        private int eventDrainCount;
        private int snapshotCount;
        private int captureCount;
        private int advanceCount;
        private int commitBoundaryFeatureReadCount;
        private string? lastCapturedSerializedState;
        private Snapshot? reusableSnapshot;
        private Action? onSecondCommitBoundaryFeatureRead;

        public bool EmitNextEvent { get; set; }

        public bool CommitBoundarySnapshotsEnabled { get; set; }

        public Action? OnSecondCommitBoundaryFeatureRead
        {
            set {
                Interlocked.Exchange(ref commitBoundaryFeatureReadCount, 0);
                Interlocked.Exchange(ref onSecondCommitBoundaryFeatureRead, value);
            }
        }

        public bool ReuseSnapshotInstance { get; set; }

        public AuthoritativeProtocolFeatures SupportedProtocolFeatures
        {
            get {
                if (!CommitBoundarySnapshotsEnabled)
                    return AuthoritativeProtocolFeatures.None;
                if (Interlocked.Increment(ref commitBoundaryFeatureReadCount) == 2)
                    Interlocked.Exchange(ref onSecondCommitBoundaryFeatureRead, null)?.Invoke();
                return AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots;
            }
        }

        public bool ReturnInvalidEventBatch { get; set; }

        public bool ReturnInvalidSnapshot { get; set; }

        public bool VarySnapshotStateHash { get; set; }

        public int CaptureCount => Volatile.Read(ref captureCount);

        public int AdvanceCount => Volatile.Read(ref advanceCount);

        public int EventDrainCount
        {
            get { lock (synchronization) return eventDrainCount; }
        }

        public int SnapshotCount
        {
            get { lock (synchronization) return snapshotCount; }
        }

        public uint[] ParticipantEntityIds
        {
            get { lock (synchronization) return participants.Select(participant => participant.EntityId).ToArray(); }
        }

        public bool SupportsCohortPersistence => true;

        public string CohortModuleId => "clock-gated-snapshot-poll-tests";

        public int CohortFormatVersion => 1;

        public bool IsEntityIdOccupied(uint entityId) => false;

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
            lock (synchronization)
                this.tick = tick;
            Interlocked.Increment(ref advanceCount);
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong requestedTick)
        {
            lock (synchronization) {
                snapshotCount++;
                var snapshot = ReuseSnapshotInstance
                    ? reusableSnapshot ??= new Snapshot()
                    : new Snapshot();
                snapshot.Tick = requestedTick;
                snapshot.StateSha256 = ReturnInvalidSnapshot
                    ? "invalid"
                    : VarySnapshotStateHash
                        ? new string((char)('a' + snapshotCount % 6), 64)
                        : string.Empty;
                snapshot.Players.Clear();
                snapshot.Players.Add(new PlayerSnapshot { EntityId = entityId });
                return snapshot;
            }
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong requestedTick)
        {
            lock (synchronization) {
                eventDrainCount++;
                if (ReturnInvalidEventBatch)
                    return new EventBatch { Tick = requestedTick, Events = { new GameEvent() } };
                if (!EmitNextEvent)
                    return new EventBatch { Tick = requestedTick };
                EmitNextEvent = false;
                return new EventBatch {
                    Tick = requestedTick,
                    Events = { new GameEvent { Experience = new ExperienceEvent { Amount = 1 } } },
                };
            }
        }

        public string ExportPlayerSave(string sessionId) => "{}";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            if (selection.EntityId is not uint entityId || entityId == 0)
                throw new InvalidDataException("The test participant must receive a stable entity ID.");
            lock (synchronization)
                participants.Add(new AuthoritativeModuleCohortParticipant(sessionId, entityId));
        }

        public AuthoritativeModuleCohortState CaptureCohortState()
        {
            lock (synchronization) {
                Interlocked.Increment(ref captureCount);
                var serialized = SerializeCurrentStateCore();
                lastCapturedSerializedState = serialized;
                return new AuthoritativeModuleCohortState(
                    CohortModuleId,
                    CohortFormatVersion,
                    tick,
                    participants.OrderBy(participant => participant.SessionId, StringComparer.Ordinal).ToArray(),
                    serialized);
            }
        }

        public string SerializeCurrentState()
        {
            lock (synchronization)
                return SerializeCurrentStateCore();
        }

        public string LastCapturedSerializedState
        {
            get {
                lock (synchronization)
                    return lastCapturedSerializedState
                        ?? throw new InvalidOperationException("No cohort state has been captured yet.");
            }
        }

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
            AuthoritativeModuleCohortState state)
            => throw new NotSupportedException("Restore is outside this snapshot-poll fixture.");

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
            => throw new NotSupportedException("Restore is outside this snapshot-poll fixture.");

        private string SerializeCurrentStateCore()
            => JsonSerializer.Serialize(new SnapshotPollState(
                tick,
                eventDrainCount,
                snapshotCount,
                participants.OrderBy(participant => participant.SessionId, StringComparer.Ordinal).ToArray()));

        private sealed record SnapshotPollState(
            ulong Tick,
            int EventDrainCount,
            int SnapshotCount,
            IReadOnlyList<AuthoritativeModuleCohortParticipant> Participants);
    }
}
