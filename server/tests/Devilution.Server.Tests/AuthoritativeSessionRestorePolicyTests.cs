using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSessionRestorePolicyTests
{
    private const string ActiveContentHash = "content-current";
    private const string ActiveRulesetHash = "ruleset-current";
    private const string ActiveSchema = "0.1.0";

    [Fact]
    public async Task DefaultPolicyRetainsLegacySaveAndReceiptSidecarRestore()
    {
        var root = CreateOwnedRoot();
        try {
            const string token = "compatibility-session";
            var sessionId = SessionIdForToken(token);
            var saveStore = new AuthoritativeSaveStore(root);
            var receiptStore = new AuthoritativeCommandReceiptStore(root);
            saveStore.Save(sessionId, "legacy-state");
            receiptStore.Save(sessionId, new Dictionary<ulong, CommandResult> {
                [3] = Receipt(3, 9, 17),
            });
            var provider = new RecordingSaveProvider { ImportedStateTick = 17 };
            var initializer = new RecordingInitializer();
            var executor = new RecordingExecutor();
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(executor),
                CreateHandshake(),
                () => 10,
                snapshotProvider: new RecordingSnapshotProvider(),
                saveStore: saveStore,
                saveProvider: provider,
                participantInitializer: initializer,
                commandReceiptStore: receiptStore,
                sessionStore: new AuthoritativeSessionStore(root));

            Assert.Equal(AuthoritativeSessionRestorePolicy.Compatibility, server.SessionRestorePolicy);
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, timeout.Token, token);
            var (hello, snapshot) = await ReadHandshakeSnapshotAsync(stream, timeout.Token);

            Assert.Equal(3UL, hello.LastClientSequence);
            Assert.Equal(17UL, snapshot.Tick);
            Assert.Equal("legacy-state", Assert.Single(provider.ImportedStates).State);
            Assert.Equal(0, initializer.CallCount);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictPolicyRestoresOnlyMatchingCombinedCheckpointAndItsLedger()
    {
        var root = CreateOwnedRoot();
        try {
            const string token = "strict-current-checkpoint";
            var sessionId = SessionIdForToken(token);
            var sessionStore = new AuthoritativeSessionStore(root);
            sessionStore.Save(
                sessionId,
                "current-state",
                new Dictionary<ulong, CommandResult> { [2] = Receipt(2, 5, 23) },
                ActiveRulesetHash);
            var provider = new RecordingSaveProvider { ImportedStateTick = 23 };
            var initializer = new RecordingInitializer();
            var executor = new RecordingExecutor();
            var clock = new RecordingResumableClock(10);
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(executor),
                CreateHandshake(),
                clock,
                snapshotProvider: new RecordingSnapshotProvider(),
                saveStore: new AuthoritativeSaveStore(root),
                saveProvider: provider,
                participantInitializer: initializer,
                commandReceiptStore: new AuthoritativeCommandReceiptStore(root),
                sessionStore: new AuthoritativeSessionStore(root)) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            };
            server.Start();

            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await SendHelloAsync(
                stream,
                timeout.Token,
                token,
                contentHash: "content-from-old-client",
                rulesetHash: "ruleset-from-old-client");
            var (hello, snapshot) = await ReadHandshakeSnapshotAsync(stream, timeout.Token);

            Assert.Equal(ActiveContentHash, hello.ContentManifestHash);
            Assert.Equal(ActiveRulesetHash, hello.RulesetIdentityHash);
            Assert.Equal(2UL, hello.LastClientSequence);
            Assert.Equal(23UL, snapshot.Tick);
            Assert.Equal("current-state", Assert.Single(provider.ImportedStates).State);
            Assert.Equal(1, provider.ImportCount);
            Assert.Equal(23UL, clock.RestoredTick.GetValueOrDefault());
            Assert.True(clock.RestoredTick.HasValue);
            Assert.Equal(1, clock.RestoreCount);
            Assert.Equal(0, initializer.CallCount);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Theory]
    [InlineData("legacy-save")]
    [InlineData("empty-receipt-ledger")]
    [InlineData("nonempty-receipt-ledger")]
    public async Task StrictPolicyRejectsIdentitylessSidecarsBeforeSessionAllocation(string sidecar)
    {
        var root = CreateOwnedRoot();
        try {
            const string rejectedToken = "strict-identityless-session";
            var rejectedSessionId = SessionIdForToken(rejectedToken);
            var saveStore = new AuthoritativeSaveStore(root);
            var receiptStore = new AuthoritativeCommandReceiptStore(root);
            if (sidecar == "legacy-save")
                saveStore.Save(rejectedSessionId, "identityless-state");
            else
                receiptStore.Save(
                    rejectedSessionId,
                    sidecar == "empty-receipt-ledger"
                        ? new Dictionary<ulong, CommandResult>()
                        : new Dictionary<ulong, CommandResult> { [1] = Receipt(1, 1, 2) });

            var provider = new RecordingSaveProvider();
            var initializer = new RecordingInitializer();
            var executor = new RecordingExecutor();
            await using var server = CreateServer(
                root,
                provider,
                initializer,
                executor,
                policy: AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
                saveStore: saveStore,
                receiptStore: receiptStore);
            server.Start();
            using var timeout = CreateTimeout();
            await WaitForStableValueAsync(() => executor.AdvanceCount, timeout.Token);
            var advancesBeforeRejectedSidecar = executor.AdvanceCount;

            using (var rejectedClient = new TcpClient()) {
                await rejectedClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var rejectedStream = rejectedClient.GetStream();
                await SendHelloAsync(rejectedStream, timeout.Token, rejectedToken);
                var rejection = await EnvelopeCodec.ReadAsync(rejectedStream, timeout.Token);
                Assert.NotNull(rejection);
                Assert.Equal(Envelope.PayloadOneofCase.Error, rejection!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, rejection.Error.Code);
                Assert.Equal(
                    "The saved session cannot be resumed under the configured restore policy.",
                    rejection.Error.Detail);
            }

            Assert.Empty(server.ActiveSessionIds);
            Assert.Equal(advancesBeforeRejectedSidecar, executor.AdvanceCount);
            Assert.Equal(0, provider.ImportCount);
            Assert.Equal(0, initializer.CallCount);

            using var freshClient = new TcpClient();
            await freshClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var freshStream = freshClient.GetStream();
            await SendHelloAsync(freshStream, timeout.Token, "strict-fresh-session");
            var (_, snapshot) = await ReadHandshakeSnapshotAsync(freshStream, timeout.Token);
            Assert.Equal(1U, Assert.Single(snapshot.Players).EntityId);
            Assert.Equal(1, initializer.CallCount);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Theory]
    [InlineData("format-1")]
    [InlineData("format-2")]
    [InlineData("format-3")]
    [InlineData("format-4-no-identity")]
    [InlineData("format-4-mismatch")]
    [InlineData("format-4-case-mismatch")]
    [InlineData("corrupt")]
    public async Task StrictPolicyRejectsInvalidCombinedCheckpointBeforeMutation(string checkpointKind)
    {
        var root = CreateOwnedRoot();
        try {
            const string rejectedToken = "strict-invalid-checkpoint";
            var sessionId = SessionIdForToken(rejectedToken);
            WriteCheckpointVariant(root, sessionId, checkpointKind);
            var provider = new RecordingSaveProvider();
            var initializer = new RecordingInitializer();
            var executor = new RecordingExecutor();
            await using var server = CreateServer(
                root,
                provider,
                initializer,
                executor,
                policy: AuthoritativeSessionRestorePolicy.CurrentRulesetOnly);
            server.Start();
            using var timeout = CreateTimeout();
            await WaitForStableValueAsync(() => executor.AdvanceCount, timeout.Token);
            var advancesBeforeRejectedCheckpoint = executor.AdvanceCount;

            using (var rejectedClient = new TcpClient()) {
                await rejectedClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var rejectedStream = rejectedClient.GetStream();
                await SendHelloAsync(rejectedStream, timeout.Token, rejectedToken);
                var rejection = await EnvelopeCodec.ReadAsync(rejectedStream, timeout.Token);
                Assert.NotNull(rejection);
                Assert.Equal(Envelope.PayloadOneofCase.Error, rejection!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, rejection.Error.Code);
                Assert.Equal(
                    "The saved session cannot be resumed under the configured restore policy.",
                    rejection.Error.Detail);
            }

            Assert.Empty(server.ActiveSessionIds);
            Assert.Equal(advancesBeforeRejectedCheckpoint, executor.AdvanceCount);
            Assert.Equal(0, provider.ImportCount);
            Assert.Equal(0, initializer.CallCount);

            using var freshClient = new TcpClient();
            await freshClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var freshStream = freshClient.GetStream();
            await SendHelloAsync(freshStream, timeout.Token, "strict-fresh-after-invalid-checkpoint");
            var (_, snapshot) = await ReadHandshakeSnapshotAsync(freshStream, timeout.Token);
            Assert.Equal(1U, Assert.Single(snapshot.Players).EntityId);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictPolicyRejectsMissingDependenciesAndCohortCombinationAtStart()
    {
        var root = CreateOwnedRoot();
        try {
            var executor = new RecordingExecutor();
            var commandServer = new AuthoritativeCommandServer(executor);
            var handshake = CreateHandshake();
            await using (var missingSessionStore = new AuthoritativeTcpServer(
                commandServer,
                handshake,
                () => 10,
                saveProvider: new RecordingSaveProvider()) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            }) {
                Assert.Throws<InvalidOperationException>(missingSessionStore.Start);
            }

            await using (var missingSaveProvider = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(new RecordingExecutor()),
                CreateHandshake(),
                () => 10,
                sessionStore: new AuthoritativeSessionStore(root)) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            }) {
                Assert.Throws<InvalidOperationException>(missingSaveProvider.Start);
            }

            await using var cohortServer = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(new RecordingExecutor()),
                CreateHandshake(),
                () => 10,
                saveProvider: new RecordingSaveProvider(),
                sessionStore: new AuthoritativeSessionStore(Path.Combine(root, "cohort-session"))) {
                CohortStore = new AuthoritativeCohortStore(Path.Combine(root, "cohort")),
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            };
            Assert.Throws<NotSupportedException>(cohortServer.Start);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void UndefinedPolicyValueIsRejectedWhenConfigured()
    {
        var root = CreateOwnedRoot();
        try {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateServer(
                root,
                new RecordingSaveProvider(),
                new RecordingInitializer(),
                new RecordingExecutor(),
                policy: (AuthoritativeSessionRestorePolicy)int.MaxValue));

            Assert.Throws<ArgumentException>(() => new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                ActiveSchema,
                ActiveContentHash,
                20) { RulesetIdentityHash = string.Empty }));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictPolicySerializesColdAdmissionWithRulesetReloadAndKeepsActiveSessionUsable()
    {
        var root = CreateOwnedRoot();
        using var initializationGate = new ManualResetEventSlim();
        try {
            var initial = new ReloadableGameAdapter("initial", initializationGate);
            var replacement = new ReloadableGameAdapter("replacement");
            var adapters = new Queue<IAuthoritativeGameHostAdapter>([initial, replacement]);
            using var leasedAdapter = CreateLeasedAdapter(adapters, () => 10);
            var commandServer = new AuthoritativeCommandServer(leasedAdapter);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                ActiveSchema,
                ActiveContentHash,
                20) { RulesetIdentityHash = ActiveRulesetHash });
            await using var server = new AuthoritativeTcpServer(
                commandServer,
                handshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: leasedAdapter,
                saveStore: new AuthoritativeSaveStore(root)) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            };
            server.Start();
            using var timeout = CreateTimeout();
            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, timeout.Token, "strict-reload-owner");
            await initial.InitializationEntered.Task.WaitAsync(timeout.Token);

            var reloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reloadTask = Task.Run(() => {
                reloadStarted.TrySetResult();
                return server.TryReloadRuleset(
                    CreateReloadManifest("2", "replacement"),
                    [new ReloadableModule("rules", "2.0.0")]);
            }, timeout.Token);
            await reloadStarted.Task.WaitAsync(timeout.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
            Assert.False(reloadTask.IsCompleted);

            initial.ReleaseInitialization();
            _ = await ReadHandshakeSnapshotAsync(firstStream, timeout.Token);
            var reload = await reloadTask.WaitAsync(timeout.Token);
            Assert.True(reload.Succeeded);
            Assert.Equal(1, replacement.ImportCount);

            using var resumedClient = new TcpClient();
            await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var resumedStream = resumedClient.GetStream();
            await SendHelloAsync(
                resumedStream,
                timeout.Token,
                "strict-reload-owner",
                contentHash: ActiveContentHash,
                rulesetHash: ActiveRulesetHash);
            var (resumedHello, resumedSnapshot) = await ReadHandshakeSnapshotAsync(resumedStream, timeout.Token);
            Assert.Equal(reload.Active.Identity.CombinedSha256, resumedHello.RulesetIdentityHash);
            Assert.Equal(reload.Active.Identity.CombinedSha256, resumedSnapshot.RulesetIdentityHash);
            Assert.Equal("initial", Assert.Single(resumedSnapshot.Players).CharacterName);
        } finally {
            initializationGate.Set();
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictFailedColdProviderImportPoisonsTokenAcrossRetryAndRulesetReload()
    {
        var root = CreateOwnedRoot();
        const string token = "strict-poisoned-provider-import";
        var sessionId = SessionIdForToken(token);
        try {
            var sessionStore = new AuthoritativeSessionStore(root);
            sessionStore.Save(
                sessionId,
                "persisted-state",
                new Dictionary<ulong, CommandResult> { [4] = Receipt(4, 9, 23) },
                ActiveRulesetHash);
            var initial = new ReloadableGameAdapter("initial", throwAfterImport: true);
            var replacement = new ReloadableGameAdapter("replacement");
            var adapters = new Queue<IAuthoritativeGameHostAdapter>([initial, replacement]);
            using var leasedAdapter = CreateLeasedAdapter(adapters, () => 10);
            var saveStore = new AuthoritativeSaveStore(root);
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                CreateHandshake(),
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: leasedAdapter,
                saveStore: saveStore) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            };
            server.Start();
            using var timeout = CreateTimeout();

            using (var firstClient = new TcpClient()) {
                await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var firstStream = firstClient.GetStream();
                await SendHelloAsync(firstStream, timeout.Token, token);
                var firstFailure = await EnvelopeCodec.ReadAsync(firstStream, timeout.Token);
                Assert.NotNull(firstFailure);
                Assert.Equal(Envelope.PayloadOneofCase.Error, firstFailure!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, firstFailure.Error.Code);
                Assert.Equal(
                    "The saved session cannot be resumed under the configured restore policy.",
                    firstFailure.Error.Detail);
            }

            Assert.Equal(1, initial.ImportCount);
            Assert.Empty(server.ActiveSessionIds);
            var retainedCheckpoint = sessionStore.LoadForRuleset(sessionId, ActiveRulesetHash);
            Assert.NotNull(retainedCheckpoint);
            Assert.Equal("persisted-state", retainedCheckpoint!.SerializedState);
            var retainedReceipt = Assert.Single(retainedCheckpoint.CommandReceipts);
            Assert.Equal(4UL, retainedReceipt.ClientSequence);
            Assert.Equal(9UL, retainedReceipt.ServerReceiptSequence);

            // A same-generation retry must not reuse the partially imported receipt ledger or state.
            using (var retryClient = new TcpClient()) {
                await retryClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var retryStream = retryClient.GetStream();
                await SendHelloAsync(retryStream, timeout.Token, token);
                var retryFailure = await EnvelopeCodec.ReadAsync(retryStream, timeout.Token);
                Assert.NotNull(retryFailure);
                Assert.Equal(Envelope.PayloadOneofCase.Error, retryFailure!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, retryFailure.Error.Code);
            }
            Assert.Equal(1, initial.ImportCount);
            Assert.Equal(0, replacement.ImportCount);

            // Even a valid same-identity replacement checkpoint cannot revive a token whose
            // earlier import may have partially changed shared world state.
            sessionStore.Save(
                sessionId,
                "replacement-state",
                new Dictionary<ulong, CommandResult> { [8] = Receipt(8, 13, 27) },
                ActiveRulesetHash);
            using (var replacementRetryClient = new TcpClient()) {
                await replacementRetryClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var replacementRetryStream = replacementRetryClient.GetStream();
                await SendHelloAsync(replacementRetryStream, timeout.Token, token);
                var replacementFailure = await EnvelopeCodec.ReadAsync(replacementRetryStream, timeout.Token);
                Assert.NotNull(replacementFailure);
                Assert.Equal(Envelope.PayloadOneofCase.Error, replacementFailure!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, replacementFailure.Error.Code);
            }
            Assert.Equal(1, initial.ImportCount);

            var reload = server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]);
            Assert.True(reload.Succeeded);
            var preRepairCheckpoint = sessionStore.Load(sessionId);
            Assert.NotNull(preRepairCheckpoint);
            Assert.Equal("replacement-state", preRepairCheckpoint!.SerializedState);
            Assert.Equal(ActiveRulesetHash, preRepairCheckpoint.RulesetIdentityHash);

            // Model an externally replaced checkpoint that now matches the reloaded identity.
            sessionStore.Save(
                sessionId,
                "replacement-state-for-reloaded-ruleset",
                new Dictionary<ulong, CommandResult> { [9] = Receipt(9, 14, 28) },
                server.ActiveRulesetIdentityHash);

            using (var postReloadClient = new TcpClient()) {
                await postReloadClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
                await using var postReloadStream = postReloadClient.GetStream();
                await SendHelloAsync(
                    postReloadStream,
                    timeout.Token,
                    token,
                    contentHash: server.ActiveContentManifestHash,
                    rulesetHash: server.ActiveRulesetIdentityHash);
                var retryFailure = await EnvelopeCodec.ReadAsync(postReloadStream, timeout.Token);
                Assert.NotNull(retryFailure);
                Assert.Equal(Envelope.PayloadOneofCase.Error, retryFailure!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, retryFailure.Error.Code);
            }

            Assert.Equal(1, initial.ImportCount);
            Assert.Equal(0, replacement.ImportCount);
            var stillRetained = sessionStore.LoadForRuleset(sessionId, server.ActiveRulesetIdentityHash);
            Assert.NotNull(stillRetained);
            Assert.Equal("replacement-state-for-reloaded-ruleset", stillRetained!.SerializedState);
            Assert.Equal(server.ActiveRulesetIdentityHash, stillRetained.RulesetIdentityHash);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictRestoreSaveSidecarExistenceIsPresenceOnlyAndKeepsPathValidation()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSaveStore(root);
            Assert.False(store.Exists("presence-check"));
            Assert.Throws<ArgumentException>(() => store.Exists(string.Empty));
            Assert.Throws<ArgumentException>(() => store.Exists(" "));
            Assert.Throws<ArgumentException>(() => store.Exists("../outside"));
            Assert.Throws<ArgumentException>(() => store.Exists("session.name"));

            store.Save("presence-check", "state");
            Assert.True(store.Exists("presence-check"));
            Assert.True(store.Delete("presence-check"));
            Assert.False(store.Exists("presence-check"));

            File.WriteAllBytes(Path.Combine(root, "invalid-utf8.json"), new byte[] { 0xFF });
            Assert.True(store.Exists("invalid-utf8"));
            Assert.Throws<InvalidDataException>(() => store.Load("invalid-utf8"));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictPersistedAdmissionCapturesHelloAndFirstSnapshotBeforeRulesetReload()
    {
        var root = CreateOwnedRoot();
        using var importGate = new ManualResetEventSlim();
        const string token = "strict-persisted-import-reload-race";
        try {
            var sessionStore = new AuthoritativeSessionStore(root);
            sessionStore.Save(
                SessionIdForToken(token),
                "persisted-checkpoint-state",
                new Dictionary<ulong, CommandResult> { [5] = Receipt(5, 12, 23) },
                ActiveRulesetHash);
            var initial = new ReloadableGameAdapter("initial", importGate: importGate, importedStateTick: 23);
            var replacement = new ReloadableGameAdapter("replacement");
            var adapters = new Queue<IAuthoritativeGameHostAdapter>([initial, replacement]);
            using var leasedAdapter = CreateLeasedAdapter(adapters, () => 10);
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                CreateHandshake(),
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: leasedAdapter,
                saveStore: new AuthoritativeSaveStore(root)) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
            };
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await SendHelloAsync(
                stream,
                timeout.Token,
                token,
                contentHash: "content-from-prior-client",
                rulesetHash: "ruleset-from-prior-client");
            await initial.ImportEntered.Task.WaitAsync(timeout.Token);

            var reloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reloadTask = Task.Run(() => {
                reloadStarted.TrySetResult();
                return server.TryReloadRuleset(
                    CreateReloadManifest("2", "replacement"),
                    [new ReloadableModule("rules", "2.0.0")]);
            }, timeout.Token);
            await reloadStarted.Task.WaitAsync(timeout.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
            Assert.False(reloadTask.IsCompleted);

            importGate.Set();
            var (hello, snapshot) = await ReadHandshakeSnapshotAsync(stream, timeout.Token);
            Assert.Equal(ActiveContentHash, hello.ContentManifestHash);
            Assert.Equal(ActiveRulesetHash, hello.RulesetIdentityHash);
            Assert.Equal(5UL, hello.LastClientSequence);
            Assert.Equal(ActiveContentHash, snapshot.ContentManifestHash);
            Assert.Equal(ActiveRulesetHash, snapshot.RulesetIdentityHash);
            Assert.Equal(23UL, snapshot.Tick);
            Assert.Equal("persisted-checkpoint-state", Assert.Single(snapshot.Players).CharacterName);

            var reload = await reloadTask.WaitAsync(timeout.Token);
            Assert.True(reload.Succeeded);
            Assert.NotEqual(ActiveRulesetHash, server.ActiveRulesetIdentityHash);
            Assert.Equal(1, initial.ImportCount);
            Assert.Equal(1, replacement.ImportCount);
        } finally {
            importGate.Set();
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public async Task StrictQueuedColdRestoreDoesNotCacheAcrossReloadAndRetry()
    {
        ulong physicalTick = 111;
        var root = CreateOwnedRoot();
        const string ownerToken = "strict-clocked-owner";
        const string persistedToken = "strict-clocked-cold-session";
        try {
            var initial = new ReloadableGameAdapter("initial");
            var replacement = new ReloadableGameAdapter("replacement");
            var adapters = new Queue<IAuthoritativeGameHostAdapter>([initial, replacement]);
            using var leasedAdapter = CreateLeasedAdapter(adapters, () => Volatile.Read(ref physicalTick));
            var saveStore = new AuthoritativeSaveStore(root);
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                new ProtocolHandshake(new ProtocolServerIdentity(
                    "server-build",
                    ActiveSchema,
                    ActiveContentHash,
                    20) { RulesetIdentityHash = ActiveRulesetHash }),
                new DelegateAuthoritativeClock(() => Volatile.Read(ref physicalTick)),
                gameAdapter: leasedAdapter,
                saveStore: saveStore) {
                SessionRestorePolicy = AuthoritativeSessionRestorePolicy.CurrentRulesetOnly,
                UseClockGatedCommandScheduling = true,
            };
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var ownerClient = new TcpClient();
            await ownerClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var ownerStream = ownerClient.GetStream();
            await SendHelloAsync(ownerStream, cancellationToken, ownerToken, supportsClockGated: true);
            await ReadHandshakeSnapshotAsync(ownerStream, cancellationToken);
            await EnvelopeCodec.WriteAsync(ownerStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 125,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    } },
                },
            }, cancellationToken);
            await WaitUntilAsync(() => server.PendingCommandCount == 1, cancellationToken);
            await WaitForStableValueAsync(() => initial.AdvanceCount, cancellationToken);
            var advancesBeforeRejectedColdRestore = initial.AdvanceCount;

            new AuthoritativeSessionStore(root).Save(
                SessionIdForToken(persistedToken),
                "uninitialized-cold-state",
                new Dictionary<ulong, CommandResult>(),
                server.ActiveRulesetIdentityHash);
            using (var coldClient = new TcpClient()) {
                await coldClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var coldStream = coldClient.GetStream();
                await SendHelloAsync(coldStream, cancellationToken, persistedToken, supportsClockGated: true);
                var error = await EnvelopeCodec.ReadAsync(coldStream, cancellationToken);
                Assert.NotNull(error);
                Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
            }
            Assert.DoesNotContain(SessionIdForToken(persistedToken), server.ActiveSessionIds);
            Assert.Equal(advancesBeforeRejectedColdRestore, initial.AdvanceCount);

            Volatile.Write(ref physicalTick, 130);
            var ownerAck = await ReadWithinAsync(ownerStream, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, Assert.Single(ownerAck.CommandAck.Results).Status);
            var reload = server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]);
            Assert.True(reload.Succeeded);

            using var retryClient = new TcpClient();
            await retryClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var retryStream = retryClient.GetStream();
            await SendHelloAsync(
                retryStream,
                cancellationToken,
                persistedToken,
                contentHash: server.ActiveContentManifestHash,
                rulesetHash: server.ActiveRulesetIdentityHash,
                supportsClockGated: true);
            var staleError = await EnvelopeCodec.ReadAsync(retryStream, cancellationToken);
            Assert.NotNull(staleError);
            Assert.Equal(Envelope.PayloadOneofCase.Error, staleError!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, staleError.Error.Code);
            Assert.Equal(1, replacement.ImportCount);
            Assert.DoesNotContain(SessionIdForToken(persistedToken), replacement.ImportedSessionIds);

            using var freshClient = new TcpClient();
            await freshClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var freshStream = freshClient.GetStream();
            await SendHelloAsync(
                freshStream,
                cancellationToken,
                "strict-fresh-after-reload",
                contentHash: server.ActiveContentManifestHash,
                rulesetHash: server.ActiveRulesetIdentityHash,
                supportsClockGated: true);
            var (_, freshSnapshot) = await ReadHandshakeSnapshotAsync(freshStream, cancellationToken);
            Assert.Equal(2U, Assert.Single(freshSnapshot.Players).EntityId);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    private static AuthoritativeTcpServer CreateServer(
        string root,
        RecordingSaveProvider provider,
        RecordingInitializer initializer,
        RecordingExecutor executor,
        AuthoritativeSessionRestorePolicy policy,
        AuthoritativeSaveStore? saveStore = null,
        AuthoritativeCommandReceiptStore? receiptStore = null)
    {
        return new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(executor),
            CreateHandshake(),
            () => 10,
            snapshotProvider: new RecordingSnapshotProvider(),
            saveStore: saveStore ?? new AuthoritativeSaveStore(root),
            saveProvider: provider,
            participantInitializer: initializer,
            commandReceiptStore: receiptStore ?? new AuthoritativeCommandReceiptStore(root),
            sessionStore: new AuthoritativeSessionStore(root)) {
            SessionRestorePolicy = policy,
        };
    }

    private static ProtocolHandshake CreateHandshake()
        => new(new ProtocolServerIdentity(
            "server-build",
            ActiveSchema,
            ActiveContentHash,
            20) { RulesetIdentityHash = ActiveRulesetHash });

    private static async ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string resumeToken,
        string? contentHash = null,
        string? rulesetHash = null,
        bool supportsClockGated = false)
    {
        var hello = new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = ActiveSchema,
            ContentManifestHash = contentHash ?? ActiveContentHash,
            RulesetIdentityHash = rulesetHash ?? ActiveRulesetHash,
            ResumeToken = resumeToken,
            SupportsClockGatedCommands = supportsClockGated,
        };
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope { ClientHello = hello },
            cancellationToken);
    }

    private static async Task<(ServerHello Hello, Snapshot Snapshot)> ReadHandshakeSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(snapshot);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        return (hello.ServerHello, snapshot.Snapshot);
    }

    private static async Task<Envelope> ReadWithinAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var envelope = await EnvelopeCodec.ReadAsync(stream, deadline.Token);
        Assert.NotNull(envelope);
        return envelope!;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate())
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
    }

    private static async Task WaitForStableValueAsync(Func<int> readValue, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var previous = readValue();
        var stableSamples = 0;
        while (stableSamples < 3) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
            var current = readValue();
            if (current == previous) {
                stableSamples++;
            } else {
                previous = current;
                stableSamples = 0;
            }
        }
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        return timeout;
    }

    private static string CreateOwnedRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Strict-Session-Restore-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteOwnedRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullTemp = Path.GetFullPath(Path.GetTempPath());
        Assert.StartsWith(fullTemp, fullRoot, StringComparison.OrdinalIgnoreCase);
        if (Directory.Exists(fullRoot))
            Directory.Delete(fullRoot, recursive: true);
    }

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static CommandResult Receipt(ulong clientSequence, ulong serverSequence, ulong appliedTick)
        => new() {
            ClientSequence = clientSequence,
            ServerReceiptSequence = serverSequence,
            AppliedTick = appliedTick,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        };

    private static void WriteCheckpointVariant(string root, string sessionId, string kind)
    {
        var path = Path.Combine(root, sessionId + ".session.json");
        if (kind == "corrupt") {
            File.WriteAllText(path, "{");
            return;
        }

        var storedRuleset = kind switch {
            "format-4-mismatch" => "ruleset-old",
            "format-4-case-mismatch" => "RULESET-CURRENT",
            _ => ActiveRulesetHash,
        };
        new AuthoritativeSessionStore(root).Save(
            sessionId,
            "stored-state",
            new Dictionary<ulong, CommandResult>(),
            storedRuleset);
        if (kind.StartsWith("format-", StringComparison.Ordinal)
            && kind.Length == "format-1".Length
            && char.IsDigit(kind[^1])) {
            var formatVersion = int.Parse(kind[^1].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var versionKey = json.Select(entry => entry.Key)
                .Single(key => string.Equals(key, "FormatVersion", StringComparison.OrdinalIgnoreCase));
            json[versionKey] = formatVersion;
            File.WriteAllText(path, json.ToJsonString());
        } else if (kind == "format-4-no-identity") {
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var identityKey = json.Select(entry => entry.Key)
                .Single(key => string.Equals(key, "RulesetIdentityHash", StringComparison.OrdinalIgnoreCase));
            json.Remove(identityKey);
            File.WriteAllText(path, json.ToJsonString());
        }
    }

    private static ContentManifest CreateReloadManifest(string version, string value)
        => new(
            "rules",
            version,
            [new ContentPack(
                "base",
                version,
                [TsvTable.Parse("rules.tsv", $"id\tvalue\na\t{value}\n")])]);

    private static LeasedAuthoritativeGameHostAdapter CreateLeasedAdapter(
        Queue<IAuthoritativeGameHostAdapter> adapters,
        Func<ulong> tickProvider)
    {
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(tickProvider),
            (_, _) => adapters.Dequeue());
        return new LeasedAuthoritativeGameHostAdapter(runtime);
    }

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor, IAuthoritativeTickExecutor
    {
        public int ExecuteCount { get; private set; }
        private int advanceCount;

        public int AdvanceCount => Volatile.Read(ref advanceCount);

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            ExecuteCount++;
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick)
        {
            Interlocked.Increment(ref advanceCount);
        }
    }

    private sealed class RecordingSnapshotProvider : IAuthoritativeSnapshotProvider
    {
        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
    }

    private sealed class RecordingSaveProvider : IAuthoritativeSaveProvider
    {
        public List<(string SessionId, string State)> ImportedStates { get; } = [];
        public int ImportCount => ImportedStates.Count;
        public ulong ImportedStateTick { get; init; }

        public string ExportPlayerSave(string sessionId) => "fresh-state";

        public void ImportPlayerSave(string sessionId, string serializedSave)
            => ImportedStates.Add((sessionId, serializedSave));

        public ulong GetSessionStateTick(string sessionId) => ImportedStateTick;
    }

    private sealed class RecordingResumableClock(ulong initialTick) : IResumableAuthoritativeClock
    {
        private long currentTick = checked((long)initialTick);
        private long restoredTick = -1;
        private int restoreCount;

        public ulong CurrentTick => unchecked((ulong)Interlocked.Read(ref currentTick));
        public ulong? RestoredTick => Interlocked.Read(ref restoredTick) < 0
            ? null
            : unchecked((ulong)Interlocked.Read(ref restoredTick));
        public int RestoreCount => Volatile.Read(ref restoreCount);

        public void RestoreAtTick(ulong tick)
        {
            Interlocked.Exchange(ref currentTick, checked((long)tick));
            Interlocked.Exchange(ref restoredTick, checked((long)tick));
            Interlocked.Increment(ref restoreCount);
        }
    }

    private sealed class RecordingInitializer : IAuthoritativeParticipantInitializer
    {
        public int CallCount { get; private set; }

        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
            => CallCount++;
    }

    private sealed class ReloadableGameAdapter(
        string name,
        ManualResetEventSlim? initializationGate = null,
        ManualResetEventSlim? importGate = null,
        bool throwAfterImport = false,
        ulong importedStateTick = 0)
        : IAuthoritativeGameHostAdapter, IDisposable
    {
        private readonly Dictionary<string, string> sessionStates = new(StringComparer.Ordinal);
        private int importCount;
        private int advanceCount;

        public TaskCompletionSource InitializationEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ImportEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int ImportCount => Volatile.Read(ref importCount);
        public int AdvanceCount => Volatile.Read(ref advanceCount);
        public List<string> ImportedSessionIds { get; } = [];
        public bool Disposed { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
            Interlocked.Increment(ref advanceCount);
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            var snapshot = new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot {
                    EntityId = entityId,
                    CharacterName = ExportSessionState(sessionId),
                } },
            };
            snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
            return snapshot;
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick) => null;

        public string ExportPlayerSave(string sessionId) => ExportSessionState(sessionId);

        public void ImportPlayerSave(string sessionId, string serializedSave)
            => ImportSessionState(sessionId, serializedSave);

        public string ExportSessionState(string sessionId)
            => sessionStates.TryGetValue(sessionId, out var state) ? state : name;

        public void ImportSessionState(string sessionId, string serializedState)
        {
            sessionStates[sessionId] = serializedState;
            ImportedSessionIds.Add(sessionId);
            Interlocked.Increment(ref importCount);
            ImportEntered.TrySetResult();
            if (importGate is not null && !importGate.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test session-state import was not released.");
            if (throwAfterImport)
                throw new InvalidDataException("The test provider failed after mutating shared state.");
        }

        public ulong GetSessionStateTick(string sessionId) => importedStateTick;

        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
        {
            if (initializationGate is not null) {
                InitializationEntered.TrySetResult();
                if (!initializationGate.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The test participant initializer was not released.");
            }
            sessionStates.TryAdd(sessionId, selection.CharacterName ?? name);
        }

        public void ReleaseInitialization() => initializationGate?.Set();

        public void Dispose() => Disposed = true;
    }

    private sealed class ReloadableModule(string id, string version) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = new(id, version, "hash-" + version);

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }
}
