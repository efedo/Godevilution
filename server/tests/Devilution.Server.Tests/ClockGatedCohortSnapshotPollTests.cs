using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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
public sealed class ClockGatedCohortSnapshotPollTests
{
    private const ulong FixedTick = 111;
    private static readonly string ContentHash = new('a', 64);
    private static readonly string RulesetHash = new('b', 64);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CohortSnapshotPollPublishesOnceAfterDrainingEventsAndMaterializingSnapshot(
        bool emitEvent)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { EmitNextEvent = emitEvent };
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            var snapshotsBefore = adapter.SnapshotCount;
            await SendSnapshotRequestAsync(stream, timingRequestId: 17, timeout.Token);

            Envelope? eventEnvelope = null;
            if (emitEvent) {
                eventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope.PayloadCase);
            }
            var snapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshotEnvelope.PayloadCase);
            var timingEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, timingEnvelope.PayloadCase);
            Assert.Equal(17UL, timingEnvelope.TimingReply.RequestId);

            var snapshot = snapshotEnvelope.Snapshot;
            var after = store.Load()!;
            Assert.Equal(FixedTick, snapshot.Tick);
            Assert.Equal(before.Generation + 1, after.Generation);
            Assert.Equal(capturesBefore + 1, adapter.CaptureCount);
            Assert.Equal(snapshot.Tick, after.Tick);
            Assert.Equal(adapter.SerializeCurrentState(), after.SerializedModuleState);
            Assert.Equal(adapter.LastCapturedSerializedState, after.SerializedModuleState);
            Assert.Equal(1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + 1, adapter.SnapshotCount);
            if (emitEvent) {
                Assert.Equal(snapshot.Tick, eventEnvelope!.EventBatch.Tick);
                Assert.Single(eventEnvelope.EventBatch.Events);
            }
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CohortSnapshotPollPublishesOnceEvenWithoutProjectionProviders()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, includeProjectionProviders: false);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: false, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            await SendSnapshotRequestAsync(stream, timingRequestId: 23, timeout.Token);
            var response = await ReadRequiredEnvelopeAsync(stream, timeout.Token);

            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, response.PayloadCase);
            Assert.Equal(23UL, response.TimingReply.RequestId);
            var after = store.Load()!;
            Assert.Equal(before.Generation + 1, after.Generation);
            Assert.Equal(capturesBefore + 1, adapter.CaptureCount);
            Assert.Equal(FixedTick, after.Tick);
            Assert.Equal(adapter.SerializeCurrentState(), after.SerializedModuleState);
            Assert.Equal(0, adapter.EventDrainCount);
            Assert.Equal(0, adapter.SnapshotCount);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(PollFailure.InvalidEventBatch)]
    [InlineData(PollFailure.InvalidSnapshot)]
    [InlineData(PollFailure.Publication)]
    public async Task CohortSnapshotPollFailureFencesWithoutPublishingAnyResponse(PollFailure failure)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        Exception? injectedPublicationFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            switch (failure) {
            case PollFailure.InvalidEventBatch:
                adapter.ReturnInvalidEventBatch = true;
                break;
            case PollFailure.InvalidSnapshot:
                adapter.ReturnInvalidSnapshot = true;
                break;
            case PollFailure.Publication:
                var publicationFailure = new IOException("Injected cohort poll publication failure.");
                injectedPublicationFailure = publicationFailure;
                store.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.BeforeStageWrite)
                        throw publicationFailure;
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            await SendSnapshotRequestAsync(stream, timingRequestId: 31, timeout.Token);
            await AssertConnectionClosesWithoutResponseAsync(stream, timeout.Token);
            var hostFailure = await Record.ExceptionAsync(async () =>
                await server.Completion.WaitAsync(timeout.Token));
            Assert.NotNull(hostFailure);
            Assert.Same(hostFailure, server.LastClientFailure);

            var after = store.Load()!;
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.SerializedModuleState, after.SerializedModuleState);
            Assert.Equal(capturesBefore + (failure == PollFailure.Publication ? 1 : 0), adapter.CaptureCount);
            if (injectedPublicationFailure is not null)
                Assert.Same(injectedPublicationFailure, server.LastClientFailure);
        } finally {
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }

    private static AuthoritativeTcpServer CreateServer(
        SnapshotPollAdapter adapter,
        FixedClock clock,
        AuthoritativeCohortStore store,
        bool includeProjectionProviders = true)
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
            };
    }

    private static async Task ReadHandshakeAsync(
        Stream stream,
        bool hasSnapshot,
        CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "cohort-poll-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = ContentHash,
                RulesetIdentityHash = RulesetHash,
                ResumeToken = "cohort-poll-session",
                RequestedCharacterName = "PollTester",
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        if (hasSnapshot) {
            var snapshot = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
            Assert.Equal(FixedTick, snapshot.Snapshot.Tick);
        }
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

    private sealed class FixedClock(ulong tick) : IAuthoritativeClock
    {
        public ulong CurrentTick => tick;
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
        private string? lastCapturedSerializedState;

        public bool EmitNextEvent { get; set; }

        public bool ReturnInvalidEventBatch { get; set; }

        public bool ReturnInvalidSnapshot { get; set; }

        public int CaptureCount => Volatile.Read(ref captureCount);

        public int EventDrainCount
        {
            get { lock (synchronization) return eventDrainCount; }
        }

        public int SnapshotCount
        {
            get { lock (synchronization) return snapshotCount; }
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
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong requestedTick)
        {
            lock (synchronization) {
                snapshotCount++;
                return new Snapshot {
                    Tick = requestedTick,
                    StateSha256 = ReturnInvalidSnapshot ? "invalid" : string.Empty,
                    Players = { new PlayerSnapshot { EntityId = entityId } },
                };
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
