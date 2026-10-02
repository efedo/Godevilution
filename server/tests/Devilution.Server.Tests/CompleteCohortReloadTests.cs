using System.Net;
using System.Net.Sockets;
using System.Text;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/** Explicit cohort-mode ruleset reload coverage over real module graphs. */
public sealed class CompleteCohortReloadTests
{
    private const string IdentityHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CohortReloadPreservesDisconnectedBindingsAndCommandCursors()
    {
        var root = NewRoot("Godev-Cohort-Reload-Success-");
        var clock = new ManualClock(11);
        try {
            var initial = new ForwardingAdapter(CreateExecutor());
            var replacement = new ForwardingAdapter(CreateExecutor());
            var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
            var leased = new LeasedAuthoritativeGameHostAdapter(runtime);
            var store = new AuthoritativeCohortStore(root);
            await using var server = CreateServer(leased, clock, store);
            server.Start();

            using var clientA = new TcpClient();
            var connectedA = await ConnectAsync(
                clientA,
                server.Port,
                string.Empty,
                "A",
                TestContext.Current.CancellationToken);
            await using var streamA = connectedA.Stream;
            var sessionA = SessionIdForToken(connectedA.Token);
            var entityA = connectedA.Snapshot.Players.Single(
                player => player.CharacterName == "A").EntityId;

            using var clientB = new TcpClient();
            var connectedB = await ConnectAsync(
                clientB,
                server.Port,
                string.Empty,
                "B",
                TestContext.Current.CancellationToken);
            await using var streamB = connectedB.Stream;
            var sessionB = SessionIdForToken(connectedB.Token);
            var entityB = connectedB.Snapshot.Players.Single(
                player => player.CharacterName == "B").EntityId;
            clientB.Dispose();

            _ = await WaitForCheckpointWithParticipantsAsync(
                store,
                [
                    (sessionA, entityA),
                    (sessionB, entityB),
                ],
                TestContext.Current.CancellationToken);
            var command = await SendCommandAsync(
                streamA,
                new Command {
                    ClientSequence = 1,
                    RequestedTick = 11,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                        FriendlyMode = false,
                    },
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Accepted, command.Status);
            Assert.Equal(1UL, command.ServerReceiptSequence);
            var beforeReload = await WaitForCheckpointReceiptGreaterThanAsync(
                store,
                1,
                TestContext.Current.CancellationToken);
            var oldIdentity = server.ActiveRulesetIdentityHash;

            var result = server.TryReloadRuleset(
                CreateManifest("2", "replacement"),
                [new TestModule("rules", "2.0.0")]);

            Assert.True(result.Succeeded);
            Assert.NotEqual(oldIdentity, result.Active.Identity.CombinedSha256);
            Assert.Equal(result.Active.Identity.CombinedSha256, server.ActiveRulesetIdentityHash);
            Assert.True(initial.Disposed);
            Assert.False(replacement.Disposed);
            Assert.Equal(1, replacement.PrepareCalls);
            Assert.Equal(1, replacement.CommitCalls);
            Assert.Equal(0, replacement.ImportSessionStateCalls);
            Assert.Equal(0, replacement.ImportPlayerSaveCalls);
            Assert.Equal(
                new[] { sessionA, sessionB }.OrderBy(session => session, StringComparer.Ordinal),
                server.ActiveSessionIds);

            var afterReload = store.Load()!;
            Assert.Equal(result.Active.Identity.CombinedSha256, afterReload.Identity.RulesetIdentityHash);
            Assert.Equal(beforeReload.SerializedModuleState, afterReload.SerializedModuleState);
            Assert.Equal(beforeReload.NextServerReceiptSequence, afterReload.NextServerReceiptSequence);
            AssertParticipantCheckpointsEqual(beforeReload, afterReload);
            Assert.Equal(
                beforeReload.SerializedModuleState,
                replacement.Executor.CaptureCohortState().SerializedState);
            Assert.Equal(entityA, replacement.Executor.GetPlayerState(sessionA).EntityId);
            Assert.Equal(entityB, replacement.Executor.GetPlayerState(sessionB).EntityId);

            var duplicate = await SendCommandAsync(
                streamA,
                new Command {
                    ClientSequence = 1,
                    RequestedTick = 11,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                        FriendlyMode = false,
                    },
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
            Assert.Equal(1UL, duplicate.ServerReceiptSequence);

            var continued = await SendCommandAsync(
                streamA,
                new Command {
                    ClientSequence = 2,
                    RequestedTick = 11,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                        FriendlyMode = true,
                    },
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Accepted, continued.Status);
            Assert.Equal(2UL, continued.ServerReceiptSequence);
            var afterCommand = await WaitForCheckpointReceiptGreaterThanAsync(
                store,
                afterReload.NextServerReceiptSequence,
                TestContext.Current.CancellationToken);
            Assert.Equal(result.Active.Identity.CombinedSha256, afterCommand.Identity.RulesetIdentityHash);
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Reload-Success-");
        }
    }

    [Fact]
    public async Task DetachedCandidatePreparationFailureLeavesOldCohortHealthy()
    {
        var root = NewRoot("Godev-Cohort-Reload-Prepare-Failure-");
        var clock = new ManualClock(11);
        try {
            var initial = new ForwardingAdapter(CreateExecutor());
            var replacement = new ForwardingAdapter(
                CreateExecutor(),
                prepareFailure: new InvalidOperationException("detached candidate rejected"));
            var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
            var leased = new LeasedAuthoritativeGameHostAdapter(runtime);
            var store = new AuthoritativeCohortStore(root);
            await using var server = CreateServer(leased, clock, store);
            server.Start();

            using var client = new TcpClient();
            var connected = await ConnectAsync(
                client,
                server.Port,
                string.Empty,
                "A",
                TestContext.Current.CancellationToken);
            await using var stream = connected.Stream;
            var sessionId = SessionIdForToken(connected.Token);
            var before = await WaitForCheckpointWithParticipantAsync(
                store,
                sessionId,
                TestContext.Current.CancellationToken);
            var oldIdentity = server.ActiveRulesetIdentityHash;

            var result = server.TryReloadRuleset(
                CreateManifest("2", "rejected"),
                [new TestModule("rules", "2.0.0")]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "ruleset-target-preparation-failed",
                Assert.Single(result.Diagnostics).Code);
            Assert.Equal("detached candidate rejected", Assert.Single(result.Diagnostics).Message);
            Assert.Same(initial, runtime.Active.Target);
            Assert.True(replacement.Disposed);
            Assert.Equal(0, replacement.CommitCalls);
            Assert.Equal(oldIdentity, server.ActiveRulesetIdentityHash);
            Assert.False(server.Completion.IsCompleted);

            var afterFailure = await WaitForCheckpointGenerationGreaterThanAsync(
                store,
                before.Generation,
                TestContext.Current.CancellationToken);
            Assert.Equal(oldIdentity, afterFailure.Identity.RulesetIdentityHash);
            Assert.Equal(before.SerializedModuleState, afterFailure.SerializedModuleState);
            AssertParticipantCheckpointsEqual(before, afterFailure);

            var continued = await SendCommandAsync(
                stream,
                new Command {
                    ClientSequence = 1,
                    RequestedTick = 11,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested {
                        FriendlyMode = false,
                    },
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Accepted, continued.Status);
            Assert.Equal(1UL, continued.ServerReceiptSequence);
            var afterCommand = await WaitForCheckpointReceiptGreaterThanAsync(
                store,
                afterFailure.NextServerReceiptSequence,
                TestContext.Current.CancellationToken);
            Assert.Equal(oldIdentity, afterCommand.Identity.RulesetIdentityHash);
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Reload-Prepare-Failure-");
        }
    }

    [Fact]
    public async Task PreparedCleanupFailureFencesTcpAndRetainsChangedCandidateCheckpoint()
    {
        var root = NewRoot("Godev-Cohort-Reload-Cleanup-Failure-");
        var clock = new ManualClock(11);
        var primaryCleanup = new InvalidOperationException("prepared cleanup primary");
        var candidateCleanup = new InvalidOperationException("candidate cleanup secondary");
        var oldAdapterCleanup = new InvalidOperationException("old adapter cleanup secondary");
        AuthoritativeTcpServer? server = null;
        var serverDisposed = false;
        Exception? observedFatal = null;
        try {
            var initial = new ForwardingAdapter(CreateExecutor());
            ForwardingAdapter? replacement = null;
            var candidateSessionId = string.Empty;
            replacement = new ForwardingAdapter(
                CreateExecutor(),
                preparedDisposeFailure: primaryCleanup,
                afterCommit: () => {
                    _ = replacement!.Executor.Execute(
                        candidateSessionId,
                        new Command {
                            SetFriendlyModeRequested = new SetFriendlyModeRequested {
                                FriendlyMode = false,
                            },
                        },
                        11);
                });
            var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
            var leased = new LeasedAuthoritativeGameHostAdapter(
                runtime,
                adapter => {
                    if (ReferenceEquals(adapter, replacement)) {
                        replacement.Dispose();
                        throw candidateCleanup;
                    }
                    if (ReferenceEquals(adapter, initial)) {
                        initial.Dispose();
                        throw oldAdapterCleanup;
                    }
                    if (adapter is IDisposable disposable)
                        disposable.Dispose();
                });
            var store = new AuthoritativeCohortStore(root);
            server = CreateServer(leased, clock, store);
            server.Start();

            using var client = new TcpClient();
            var connected = await ConnectAsync(
                client,
                server.Port,
                string.Empty,
                "A",
                TestContext.Current.CancellationToken);
            await using var stream = connected.Stream;
            candidateSessionId = SessionIdForToken(connected.Token);
            var before = await WaitForCheckpointWithParticipantAsync(
                store,
                candidateSessionId,
                TestContext.Current.CancellationToken);

            var fatal = Assert.Throws<IOException>(() => server.TryReloadRuleset(
                CreateManifest("2", "cleanup-failure"),
                [new TestModule("rules", "2.0.0")]));
            observedFatal = fatal;

            Assert.Contains("ruleset-target-preparation-failed", fatal.Message);
            Assert.Contains("prepared cleanup primary", fatal.Message);
            Assert.True(replacement.Disposed);
            Assert.Same(
                candidateCleanup,
                primaryCleanup.Data["cohort-cleanup-candidate-adapter"]);
            Assert.False(initial.Disposed);
            Assert.Same(initial, runtime.Active.Target);
            Assert.Equal(IdentityHash, server.ActiveRulesetIdentityHash);

            var changed = store.Load()!;
            Assert.True(changed.Generation > before.Generation);
            Assert.NotEqual(before.Identity.RulesetIdentityHash, changed.Identity.RulesetIdentityHash);
            Assert.NotEqual(before.SerializedModuleState, changed.SerializedModuleState);
            Assert.Equal(
                candidateSessionId,
                Assert.Single(changed.Participants).SessionId);
            var completionFailure = await AwaitCompletionFailureAsync(
                server,
                TestContext.Current.CancellationToken);
            Assert.Same(fatal, completionFailure);
            Assert.Same(fatal, server.LastClientFailure);

            var response = await ReadEnvelopeOrNullAsync(
                stream,
                TestContext.Current.CancellationToken);
            Assert.True(response is null || response.PayloadCase != Envelope.PayloadOneofCase.CommandAck);

            var shutdownFailure = await Record.ExceptionAsync(
                async () => await server.DisposeAsync());
            serverDisposed = true;
            Assert.Same(fatal, shutdownFailure);
            Assert.Same(
                oldAdapterCleanup,
                fatal.Data["CohortShutdownCleanupFailure:game-adapter"]);
        } finally {
            if (server is not null && !serverDisposed) {
                try {
                    await server.DisposeAsync();
                } catch (Exception shutdownFailure) when (ReferenceEquals(shutdownFailure, observedFatal)) {
                    // The exact fatal outcome was already observed; never
                    // suppress an unrelated teardown or verification failure.
                }
            }
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Reload-Cleanup-Failure-");
        }
    }

    private static GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> CreateRuntime(
        Queue<ForwardingAdapter> candidates)
    {
        return new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateManifest("1", "initial"),
            [new TestModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 11),
            (_, _) => candidates.Dequeue());
    }

    private static ContentManifest CreateManifest(string version, string value)
    {
        return new ContentManifest(
            "rules",
            version,
            [new ContentPack(
                "base",
                version,
                [TsvTable.Parse("rules.tsv", $"id\tvalue\na\t{value}\n")])]);
    }

    private static StoreSimulationExecutor CreateExecutor()
    {
        return new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 100,
            worldWidth: 8,
            worldHeight: 8,
            startingPositionX: 0,
            startingPositionY: 0);
    }

    private static AuthoritativeTcpServer CreateServer(
        LeasedAuthoritativeGameHostAdapter adapter,
        ManualClock clock,
        AuthoritativeCohortStore store)
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
        };
    }

    private static async Task<(string Token, NetworkStream Stream, Snapshot Snapshot)> ConnectAsync(
        TcpClient client,
        int port,
        string resumeToken,
        string name,
        CancellationToken cancellationToken)
    {
        await client.ConnectAsync(IPAddress.Loopback, port)
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope {
                ClientHello = new ClientHello {
                    ClientBuildId = "client-build",
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = IdentityHash,
                    RulesetIdentityHash = IdentityHash,
                    ResumeToken = resumeToken,
                    RequestedCharacterName = name,
                },
            },
            cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        var snapshot = await ReadSnapshotAsync(stream, cancellationToken);
        return (hello.ServerHello.SessionToken, stream, snapshot);
    }

    private static async Task<CommandResult> SendCommandAsync(
        Stream stream,
        Command command,
        CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope {
                CommandBatch = new CommandBatch { Commands = { command } },
            },
            cancellationToken);
        var acknowledgement = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement.PayloadCase);
        var result = Assert.Single(acknowledgement.CommandAck.Results);
        _ = await ReadSnapshotAsync(stream, cancellationToken);
        return result;
    }

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await EnvelopeCodec.ReadAsync(stream)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        return envelope ?? throw new XunitException("The host closed the connection unexpectedly.");
    }

    private static async Task<Envelope?> ReadEnvelopeOrNullAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        return await EnvelopeCodec.ReadAsync(stream)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    private static async Task<Snapshot> ReadSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, envelope.PayloadCase);
        return envelope.Snapshot;
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointWithParticipantAsync(
        AuthoritativeCohortStore store,
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null
                && document.Participants.Any(participant => participant.SessionId == sessionId))
                return document;
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointWithParticipantsAsync(
        AuthoritativeCohortStore store,
        IReadOnlyList<(string SessionId, uint EntityId)> participants,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null
                && participants.All(expected => document.Participants.Any(actual =>
                    actual.SessionId == expected.SessionId && actual.EntityId == expected.EntityId)))
                return document;
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointReceiptGreaterThanAsync(
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

    private static void AssertParticipantCheckpointsEqual(
        AuthoritativeCohortDocument expected,
        AuthoritativeCohortDocument actual)
    {
        Assert.Equal(
            expected.Participants.Select(participant =>
                (participant.SessionId, participant.EntityId)),
            actual.Participants.Select(participant =>
                (participant.SessionId, participant.EntityId)));
        Assert.Equal(
            expected.Participants.SelectMany(participant => participant.CommandReceipts.Select(
                receipt => (participant.SessionId, receipt.ClientSequence, receipt.ServerReceiptSequence))),
            actual.Participants.SelectMany(participant => participant.CommandReceipts.Select(
                receipt => (participant.SessionId, receipt.ClientSequence, receipt.ServerReceiptSequence))));
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointGenerationGreaterThanAsync(
        AuthoritativeCohortStore store,
        ulong generation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null && document.Generation > generation)
                return document;
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<Exception> AwaitCompletionFailureAsync(
        AuthoritativeTcpServer server,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var failure = await Record.ExceptionAsync(
            async () => await server.Completion.WaitAsync(timeout.Token));
        return failure ?? throw new XunitException("The cohort host did not fault.");
    }

    private static string NewRoot(string prefix)
        => Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static void DeleteOwnedTempDirectory(string directory, string prefix)
    {
        var full = Path.GetFullPath(directory);
        var parent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(full);
        if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                parent,
                StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Refusing to delete an unowned cohort test directory.");
        if (!Directory.Exists(full))
            return;
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to recurse through a reparse point.");
        Directory.Delete(full, recursive: true);
    }

    private sealed class ForwardingAdapter(
        StoreSimulationExecutor executor,
        Exception? prepareFailure = null,
        InvalidOperationException? preparedDisposeFailure = null,
        Action? afterCommit = null)
        : IAuthoritativeGameHostAdapter, IAuthoritativeGameCohortAdapter, IDisposable
    {
        public StoreSimulationExecutor Executor { get; } = executor;

        public Exception? PrepareFailure { get; } = prepareFailure;

        public InvalidOperationException? PreparedDisposeFailure { get; } = preparedDisposeFailure;

        public int PrepareCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int ImportSessionStateCalls { get; private set; }

        public int ImportPlayerSaveCalls { get; private set; }

        public bool Disposed { get; private set; }

        private IAuthoritativeGameCohortAdapter Cohort
            => (IAuthoritativeGameCohortAdapter)Executor;

        public bool SupportsCohortPersistence => Cohort.SupportsCohortPersistence;

        public string CohortModuleId => Cohort.CohortModuleId;

        public int CohortFormatVersion => Cohort.CohortFormatVersion;

        public bool IsEntityIdOccupied(uint entityId) => Cohort.IsEntityIdOccupied(entityId);

        public AuthoritativeModuleCohortState CaptureCohortState()
            => Cohort.CaptureCohortState();

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
            AuthoritativeModuleCohortState state)
        {
            PrepareCalls++;
            if (PrepareFailure is not null)
                throw PrepareFailure;
            return new ForwardedPrepared(
                Cohort.PrepareCohortRestore(state),
                PreparedDisposeFailure);
        }

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
        {
            CommitCalls++;
            if (prepared is not ForwardedPrepared forwarded)
                throw new InvalidOperationException(
                    "The forwarding decorator received an unknown prepared token.");
            Cohort.CommitCohortRestore(forwarded.Inner);
            afterCommit?.Invoke();
        }

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
            => Executor.Execute(sessionId, command, appliedTick);

        public void AdvanceTo(ulong tick) => Executor.AdvanceTo(tick);

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => Executor.CreateSnapshot(sessionId, entityId, tick);

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => Executor.DrainEvents(sessionId, entityId, tick);

        public string ExportPlayerSave(string sessionId)
            => Executor.ExportPlayerSave(sessionId);

        public string ExportSessionState(string sessionId)
            => Executor.ExportSessionState(sessionId);

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            ImportPlayerSaveCalls++;
            Executor.ImportPlayerSave(sessionId, serializedSave);
        }

        public void ImportSessionState(string sessionId, string serializedState)
        {
            ImportSessionStateCalls++;
            Executor.ImportSessionState(sessionId, serializedState);
        }

        public ulong GetSessionStateTick(string sessionId)
            => Executor.GetSessionStateTick(sessionId);

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
            => Executor.InitializeParticipant(sessionId, selection);

        public void Dispose() => Disposed = true;

        private sealed class ForwardedPrepared(
            IAuthoritativePreparedCohortRestore inner,
            InvalidOperationException? disposeFailure)
            : IAuthoritativePreparedCohortRestore
        {
            public IAuthoritativePreparedCohortRestore Inner { get; } = inner;

            public AuthoritativeModuleCohortState CohortState => Inner.CohortState;

            public void Dispose()
            {
                Inner.Dispose();
                if (disposeFailure is not null)
                    throw disposeFailure;
            }
        }
    }

    private sealed class TestModule(string id, string version) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } =
            new(id, version, $"hash-{version}");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }

    private sealed class ManualClock(ulong tick) : IAuthoritativeClock
    {
        public ulong CurrentTick { get; set; } = tick;
    }
}
