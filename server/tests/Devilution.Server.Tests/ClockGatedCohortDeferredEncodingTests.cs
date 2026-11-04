using System.Net;
using System.Net.Sockets;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class ClockGatedCohortSnapshotPollTests
{
    [Fact]
    public async Task TimedEncodingDoesNotHoldAuthorityAndSkipsWhileTheEncoderIsBusy()
    {
        var root = NewRoot();
        var adapter = new DeferredSnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var server = CreateDeferredServer(adapter, clock, time, store);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            adapter.BeforeEncoding = () => {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10), timeout.Token))
                    throw new TimeoutException("The test did not release the encoder.");
            };
            var published = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.PublicationObserver = (phase, _) => {
                if (phase == AuthoritativePublicationPhase.AfterCanonicalRename)
                    published.TrySetResult(File.ReadAllBytes(store.CheckpointPath));
            };

            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 3);
            await WaitUntilAsync(() => entered.IsSet, timeout.Token);
            var captured = adapter.LastDeferredCapture!;
            // Encoding is still parked while the live world advances and a second checkpoint is due.
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 4);
            Assert.Equal(FixedTick + 4, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            clock.Set(FixedTick + 5);
            Assert.Equal(FixedTick + 5, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            Assert.Equal(1, adapter.DeferredCaptures);
            Assert.Equal(1, adapter.Encodings);
            Assert.Equal(before.Generation, store.Load()!.Generation);

            release.Set();
            var bytes = await published.Task.WaitAsync(timeout.Token);
            Assert.Equal(AuthoritativeCohortStore.SerializeDocument(before with {
                Generation = before.Generation + 1,
                Tick = captured.Tick,
                SerializedModuleState = captured.SerializedState,
            }), bytes);
            Assert.Null(server.LastClientFailure);
        } finally {
            release.Set();
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task TimedEncodingFailureIsReportedWithoutFencingAndRetriesNextInterval()
    {
        var root = NewRoot();
        var adapter = new DeferredSnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = CreateDeferredServer(adapter, clock, time, store, failure => reported.TrySetResult(failure));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            var armed = 1;
            adapter.BeforeEncoding = () => {
                if (Interlocked.Exchange(ref armed, 0) == 1)
                    throw new InvalidDataException("Injected deferred encoding failure.");
            };

            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 3);
            var failure = await reported.Task.WaitAsync(timeout.Token);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(before.Generation, store.Load()!.Generation);
            clock.Set(FixedTick + 4);
            Assert.Equal(FixedTick + 4, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            Assert.Equal(1, adapter.Encodings);
            Assert.Null(server.LastClientFailure);
            Assert.False(server.Completion.IsCompleted);

            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 5);
            await WaitUntilAsync(() => store.Load()!.Generation == before.Generation + 1, timeout.Token);
            Assert.Equal(2, adapter.Encodings);
            Assert.True(store.Load()!.Tick >= FixedTick + 4);
            Assert.Null(server.LastClientFailure);
            Assert.False(server.Completion.IsCompleted);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    private static AuthoritativeTcpServer CreateDeferredServer(
        DeferredSnapshotPollAdapter adapter,
        IAuthoritativeClock clock,
        TimeProvider time,
        AuthoritativeCohortStore store,
        Action<Exception>? failureObserver = null)
        => new(new AuthoritativeCommandServer(adapter),
            new ProtocolHandshake(new ProtocolServerIdentity("deferred-encoding-tests", "0.1.0", ContentHash, 20) {
                RulesetIdentityHash = RulesetHash,
                GameMode = AuthoritativeGameMode.Multiplayer,
            }), clock, adapter, port: 0, address: IPAddress.Loopback) {
            CohortStore = store,
            UseClockGatedCommandScheduling = true,
            CohortCheckpointInterval = CheckpointInterval,
            CheckpointTimeProvider = time,
            CheckpointFailureObserver = failureObserver,
        };

    private sealed class DeferredSnapshotPollAdapter : IAuthoritativeGameHostAdapter, IAuthoritativeDeferredGameCohortAdapter
    {
        private readonly SnapshotPollAdapter inner = new();
        private int deferredCaptures;
        private int encodings;
        public Action? BeforeEncoding { get; set; }
        public AuthoritativeModuleCohortState? LastDeferredCapture { get; private set; }
        public int DeferredCaptures => Volatile.Read(ref deferredCaptures);
        public int Encodings => Volatile.Read(ref encodings);
        public bool SupportsCohortPersistence => inner.SupportsCohortPersistence;
        public string CohortModuleId => inner.CohortModuleId;
        public int CohortFormatVersion => inner.CohortFormatVersion;
        public bool IsEntityIdOccupied(uint entityId) => inner.IsEntityIdOccupied(entityId);
        public AuthoritativeModuleCohortState CaptureCohortState() => inner.CaptureCohortState();
        public Func<AuthoritativeModuleCohortState> CaptureCohortForDeferredEncoding()
        {
            var captured = inner.CaptureCohortState();
            LastDeferredCapture = captured;
            Interlocked.Increment(ref deferredCaptures);
            return () => {
                Interlocked.Increment(ref encodings);
                BeforeEncoding?.Invoke();
                return captured;
            };
        }
        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(AuthoritativeModuleCohortState state)
            => inner.PrepareCohortRestore(state);
        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared) => inner.CommitCohortRestore(prepared);
        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => inner.Execute(sessionId, command, appliedTick);
        public void AdvanceTo(ulong tick) => inner.AdvanceTo(tick);
        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick) => inner.CreateSnapshot(sessionId, entityId, tick);
        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick) => inner.DrainEvents(sessionId, entityId, tick);
        public string ExportPlayerSave(string sessionId) => inner.ExportPlayerSave(sessionId);
        public void ImportPlayerSave(string sessionId, string serializedSave) => inner.ImportPlayerSave(sessionId, serializedSave);
        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
            => inner.InitializeParticipant(sessionId, selection);
    }
}
