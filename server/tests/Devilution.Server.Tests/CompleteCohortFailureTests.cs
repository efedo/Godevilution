using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class CompleteCohortRecoveryTests
{
    [Fact]
    public async Task GatedMutationFailureStopsPeerAndRecoversLastDurableCohort()
    {
        var root = NewRoot("Godev-Cohort-Mutating-Failure-");
        var retainedRoot = root + "-retained";
        var clock = new ManualClock(11);
        const string tokenA = "cohort-mutating-a";
        const string tokenB = "cohort-mutating-b";
        try {
            var executor = CreateExecutor();
            var adapter = new ThrowingCohortAdapter(executor);
            var store = new AuthoritativeCohortStore(root);
            var server = CreateFailureServer(adapter, clock, store, gated: true);
            try {
                server.Start();
                using var clientA = new TcpClient();
                var connectedA = await ConnectAsync(
                    clientA,
                    server.Port,
                    tokenA,
                    "A",
                    clockGated: true,
                    TestContext.Current.CancellationToken);
                await using var streamA = connectedA.Stream;
                using var clientB = new TcpClient();
                var connectedB = await ConnectAsync(
                    clientB,
                    server.Port,
                    tokenB,
                    "B",
                    clockGated: true,
                    TestContext.Current.CancellationToken);
                await using var streamB = connectedB.Stream;
                var before = await WaitForCheckpointWithParticipantAsync(
                    store,
                    SessionIdForToken(tokenB),
                    TestContext.Current.CancellationToken);

                await EnvelopeCodec.WriteAsync(
                    streamA,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 1,
                                    RequestedTick = 11,
                                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                                        FriendlyMode = true,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                var durableAck = await ReadRequiredEnvelopeAsync(
                    streamA,
                    TestContext.Current.CancellationToken);
                Assert.Equal(Envelope.PayloadOneofCase.CommandAck, durableAck.PayloadCase);
                Assert.Equal(CommandStatus.Accepted, Assert.Single(durableAck.CommandAck.Results).Status);
                _ = await ReadSnapshotAsync(streamA, TestContext.Current.CancellationToken);
                var durable = await WaitForCheckpointReceiptSequenceGreaterThanAsync(
                    store,
                    before.NextServerReceiptSequence,
                    TestContext.Current.CancellationToken);
                var durableBytes = Array.Empty<byte>();
                var capturesAtFailureBoundary = -1;
                adapter.BeforeExecuteFailure = () => {
                    durableBytes = File.ReadAllBytes(store.CheckpointPath);
                    capturesAtFailureBoundary = adapter.CaptureCount;
                };
                adapter.ThrowAfterExecute = true;

                await EnvelopeCodec.WriteAsync(
                    streamA,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 2,
                                    RequestedTick = 12,
                                    PickupWorldItemRequested = new PickupWorldItemRequested {
                                        ItemEntityId = ClaimedItemEntityId,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                await WaitForPendingCommandsAsync(
                    server,
                    expected: 1,
                    TestContext.Current.CancellationToken);
                await EnvelopeCodec.WriteAsync(
                    streamB,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 1,
                                    RequestedTick = 12,
                                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                                        FriendlyMode = true,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                await WaitForPendingCommandsAsync(
                    server,
                    expected: 2,
                    TestContext.Current.CancellationToken);
                clock.CurrentTick = 12;
                var primaryFailure = await AssertCompletionFaultedAsync(
                    server,
                    TestContext.Current.CancellationToken);
                Assert.Same(adapter.InjectedFailure, primaryFailure);
                Assert.Same(primaryFailure, server.LastClientFailure);

                var failedModule = new DiabloCohortCodec().Deserialize(
                    adapter.CaptureInnerStateForAssertion().SerializedState);
                Assert.DoesNotContain(
                    failedModule.WorldItems,
                    item => item.EntityId == ClaimedItemEntityId);

                Assert.Equal(2, adapter.ExecutedCommands.Count);
                Assert.Contains(
                    adapter.ExecutedCommands,
                    command => command.SessionId == SessionIdForToken(tokenA)
                        && command.ClientSequence == 2);
                Assert.DoesNotContain(
                    adapter.ExecutedCommands,
                    command => command.SessionId == SessionIdForToken(tokenB)
                        && command.ClientSequence == 1);
                Assert.True(adapter.AdvancedTicks.Count(tick => tick == 12) <= 1);
                await Task.Delay(100, TestContext.Current.CancellationToken);
                Assert.Equal(capturesAtFailureBoundary, adapter.CaptureCount);

                var aResponse = await ReadEnvelopeOrNullAsync(
                    streamA,
                    TestContext.Current.CancellationToken);
                var bResponse = await ReadEnvelopeOrNullAsync(
                    streamB,
                    TestContext.Current.CancellationToken);
                Assert.True(aResponse is null || aResponse.PayloadCase != Envelope.PayloadOneofCase.CommandAck);
                Assert.True(bResponse is null || bResponse.PayloadCase != Envelope.PayloadOneofCase.CommandAck);
                CopyPublishedCheckpoint(store, retainedRoot);
                Assert.Equal(durableBytes, File.ReadAllBytes(store.CheckpointPath));
                Assert.Equal(durable.NextServerReceiptSequence, store.Load()!.NextServerReceiptSequence);
            } finally {
                await DisposeFailureServerAsync(server, adapter.InjectedFailure);
            }

            Assert.Equal(
                File.ReadAllBytes(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName)),
                File.ReadAllBytes(Path.Combine(retainedRoot, AuthoritativeCohortStore.CheckpointFileName)));

            var retainedStore = new AuthoritativeCohortStore(retainedRoot);
            await using (var restarted = CreateFailureServer(
                             new ThrowingCohortAdapter(CreateExecutor()),
                             clock,
                             retainedStore,
                             gated: true)) {
                restarted.Start();
                using var resumedA = new TcpClient();
                var resumed = await ConnectAsync(
                    resumedA,
                    restarted.Port,
                    tokenA,
                    "A",
                    clockGated: true,
                    TestContext.Current.CancellationToken);
                await using var resumedStream = resumed.Stream;
                Assert.Contains(resumed.Snapshot.WorldItems, IsClaimedItem);
                await EnvelopeCodec.WriteAsync(
                    resumedStream,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 1,
                                    RequestedTick = 12,
                                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                                        FriendlyMode = true,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                var duplicate = await ReadRequiredEnvelopeAsync(
                    resumedStream,
                    TestContext.Current.CancellationToken);
                Assert.Equal(Envelope.PayloadOneofCase.CommandAck, duplicate.PayloadCase);
                var duplicateResult = Assert.Single(duplicate.CommandAck.Results);
                Assert.Equal(CommandStatus.Duplicate, duplicateResult.Status);
                Assert.Equal(1UL, duplicateResult.ServerReceiptSequence);
            }
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Mutating-Failure-");
            DeleteOwnedTempDirectory(retainedRoot, "Godev-Cohort-Mutating-Failure-");
        }
    }

    [Fact]
    public async Task GatedSnapshotMaterializationFailureFencesWithoutSnapshotOrRepublish()
    {
        var root = NewRoot("Godev-Cohort-Snapshot-Failure-");
        var clock = new ManualClock(11);
        try {
            var executor = CreateExecutor();
            var adapter = new ThrowingCohortAdapter(executor);
            var store = new AuthoritativeCohortStore(root);
            var durableBytesAtSnapshotBoundary = Array.Empty<byte>();
            var server = CreateFailureServer(adapter, clock, store, gated: true);
            try {
                server.Start();
                using var client = new TcpClient();
                var connected = await ConnectAsync(
                    client,
                    server.Port,
                    "cohort-snapshot-failure",
                    "A",
                    clockGated: true,
                    TestContext.Current.CancellationToken);
                await using var stream = connected.Stream;
                await WaitForCheckpointWithParticipantAsync(
                    store,
                    SessionIdForToken(connected.Token),
                    TestContext.Current.CancellationToken);
                adapter.BeforeSnapshotFailure = () =>
                    durableBytesAtSnapshotBoundary = File.ReadAllBytes(store.CheckpointPath);
                adapter.ThrowAfterSnapshot = true;

                await EnvelopeCodec.WriteAsync(
                    stream,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 1,
                                    RequestedTick = 12,
                                    PickupWorldItemRequested = new PickupWorldItemRequested {
                                        ItemEntityId = ClaimedItemEntityId,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                clock.CurrentTick = 12;
                var primaryFailure = await AssertCompletionFaultedAsync(
                    server,
                    TestContext.Current.CancellationToken);
                Assert.Same(adapter.InjectedFailure, primaryFailure);
                Assert.Same(primaryFailure, server.LastClientFailure);
                Assert.Equal(2, adapter.SnapshotCount);
                var capturesAfterFailure = adapter.CaptureCount;
                await Task.Delay(100, TestContext.Current.CancellationToken);
                Assert.Equal(capturesAfterFailure, adapter.CaptureCount);

                var envelopes = new List<Envelope>();
                while (true) {
                    var envelope = await ReadEnvelopeOrNullAsync(
                        stream,
                        TestContext.Current.CancellationToken);
                    if (envelope is null)
                        break;
                    envelopes.Add(envelope);
                }
                Assert.DoesNotContain(
                    envelopes,
                    envelope => envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot);
            } finally {
                await DisposeFailureServerAsync(server, adapter.InjectedFailure);
            }
            Assert.Equal(
                durableBytesAtSnapshotBoundary,
                File.ReadAllBytes(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName)));
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Snapshot-Failure-");
        }
    }

    private static async Task DisposeFailureServerAsync(
        AuthoritativeTcpServer server,
        Exception? expectedFailure)
    {
        var shutdownFailure = await Record.ExceptionAsync(async () => await server.DisposeAsync());
        if (shutdownFailure is not null) {
            Assert.NotNull(expectedFailure);
            Assert.Same(expectedFailure, shutdownFailure);
        }
    }

    private static AuthoritativeTcpServer CreateFailureServer(
        ThrowingCohortAdapter adapter,
        IAuthoritativeClock clock,
        AuthoritativeCohortStore store,
        bool gated)
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            IdentityHash,
            20) {
            RulesetIdentityHash = IdentityHash,
            GameMode = AuthoritativeGameMode.Multiplayer,
        });
        return new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(adapter),
            handshake,
            clock,
            adapter,
            port: 0,
            address: IPAddress.Loopback) {
            CohortStore = store,
            UseClockGatedCommandScheduling = gated,
        };
    }

    private static async Task<Exception> AssertCompletionFaultedAsync(
        AuthoritativeTcpServer server,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var failure = await Record.ExceptionAsync(
            async () => await server.Completion.WaitAsync(timeout.Token));
        Assert.NotNull(failure);
        Assert.False(failure is OperationCanceledException);
        return failure!;
    }

    private static async Task WaitForPendingCommandsAsync(
        AuthoritativeTcpServer server,
        int expected,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (server.PendingCommandCount < expected)
            await Task.Delay(10, timeout.Token);
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointReceiptSequenceGreaterThanAsync(
        AuthoritativeCohortStore store,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null && document.NextServerReceiptSequence > sequence)
                return document;
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ThrowingCohortAdapter :
        IAuthoritativeGameHostAdapter,
        IAuthoritativeGameCohortAdapter
    {
        private readonly StoreSimulationExecutor inner;
        private int executeFailureThrown;
        private int snapshotFailureThrown;

        public ThrowingCohortAdapter(StoreSimulationExecutor inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool ThrowAfterExecute { get; set; }

        public bool ThrowAfterSnapshot { get; set; }

        public Action? BeforeExecuteFailure { get; set; }

        public Action? BeforeSnapshotFailure { get; set; }

        public ConcurrentQueue<(string SessionId, ulong ClientSequence)> ExecutedCommands { get; } = new();

        public ConcurrentQueue<ulong> AdvancedTicks { get; } = new();

        public Exception? InjectedFailure { get; private set; }

        public int CaptureCount => Volatile.Read(ref captureCount);

        public int SnapshotCount => Volatile.Read(ref snapshotCount);

        private int captureCount;
        private int snapshotCount;

        bool IAuthoritativeGameCohortAdapter.SupportsCohortPersistence
            => inner.SupportsCohortPersistence;

        public string CohortModuleId => inner.CohortModuleId;

        public int CohortFormatVersion => inner.CohortFormatVersion;

        public bool IsEntityIdOccupied(uint entityId) => inner.IsEntityIdOccupied(entityId);

        public AuthoritativeModuleCohortState CaptureCohortState()
        {
            Interlocked.Increment(ref captureCount);
            return inner.CaptureCohortState();
        }

        public AuthoritativeModuleCohortState CaptureInnerStateForAssertion()
            => inner.CaptureCohortState();

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
            AuthoritativeModuleCohortState state)
            => inner.PrepareCohortRestore(state);

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
            => inner.CommitCohortRestore(prepared);

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
        {
            var result = inner.Execute(sessionId, command, appliedTick);
            ExecutedCommands.Enqueue((sessionId, command.ClientSequence));
            if (ThrowAfterExecute
                && Interlocked.Exchange(ref executeFailureThrown, 1) == 0) {
                BeforeExecuteFailure?.Invoke();
                var failure = new InvalidOperationException("Injected post-command failure.");
                InjectedFailure = failure;
                throw failure;
            }
            return result;
        }

        public void AdvanceTo(ulong tick)
        {
            AdvancedTicks.Enqueue(tick);
            inner.AdvanceTo(tick);
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            var injectFailure = ThrowAfterSnapshot
                && Interlocked.Exchange(ref snapshotFailureThrown, 1) == 0;
            if (injectFailure)
                BeforeSnapshotFailure?.Invoke();
            var snapshot = inner.CreateSnapshot(sessionId, entityId, tick);
            Interlocked.Increment(ref snapshotCount);
            if (injectFailure) {
                var failure = new InvalidOperationException("Injected post-snapshot failure.");
                InjectedFailure = failure;
                throw failure;
            }
            return snapshot;
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => inner.DrainEvents(sessionId, entityId, tick);

        public string ExportPlayerSave(string sessionId) => inner.ExportPlayerSave(sessionId);

        public void ImportPlayerSave(string sessionId, string serializedSave)
            => inner.ImportPlayerSave(sessionId, serializedSave);

        public string ExportSessionState(string sessionId) => inner.ExportSessionState(sessionId);

        public void ImportSessionState(string sessionId, string serializedState)
            => inner.ImportSessionState(sessionId, serializedState);

        public ulong GetSessionStateTick(string sessionId) => inner.GetSessionStateTick(sessionId);

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
            => inner.InitializeParticipant(sessionId, selection);
    }
}
