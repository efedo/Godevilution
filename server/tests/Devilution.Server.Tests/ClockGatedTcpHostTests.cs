using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
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

/** Characterizes opt-in clock-gated scheduling through the real TCP host. */
public sealed class ClockGatedTcpHostTests
{
    [Fact]
    public async Task ClockGatedHostRejectsLegacyHelloBeforeInitialization()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var initializer = new RecordingParticipantInitializer();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(
            commandServer,
            () => Volatile.Read(ref physicalTick),
            executor,
            participantInitializer: initializer);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();

        await SendHelloAsync(
            stream,
            cancellationToken,
            "clock-gated-legacy",
            supportsClockGatedCommands: false);
        var response = await AwaitWithin(ReadAsync(stream, cancellationToken), cancellationToken);

        Assert.Equal(Envelope.PayloadOneofCase.Error, response!.PayloadCase);
        Assert.Empty(initializer.Selections);
        Assert.Empty(executor.AppliedTicks);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task ColdPersistedRestoreIsRejectedBeforeImportWhileFutureWorkIsQueued()
    {
        ulong physicalTick = 111;
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Clock-Gated-Cold-Restore-" + Guid.NewGuid().ToString("N"));
        const string ownerToken = "clock-gated-restore-owner";
        const string persistedToken = "clock-gated-restore-cold";
        var saveStore = new AuthoritativeSaveStore(saveRoot);
        var executor = new RecordingExecutor();
        var initializer = new RecordingParticipantInitializer();
        var saveProvider = new ToggleSaveProvider();
        var commandServer = new AuthoritativeCommandServer(executor);
        try {
            await using var server = CreateServer(
                commandServer,
                () => Volatile.Read(ref physicalTick),
                executor,
                saveProvider: saveProvider,
                participantInitializer: initializer,
                saveStore: saveStore);
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var ownerClient = new TcpClient();
            await ownerClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var ownerStream = ownerClient.GetStream();
            await SendHelloAsync(ownerStream, cancellationToken, ownerToken);
            await ReadHandshakeSnapshotAsync(ownerStream, 111, cancellationToken);
            await SendBatchAsync(
                ownerStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 1));
            await WaitForPendingCountAsync(server, 1, cancellationToken);

            new AuthoritativeSessionStore(saveRoot).Save(
                SessionIdForToken(persistedToken),
                "{}",
                new Dictionary<ulong, CommandResult>(),
                server.ActiveRulesetIdentityHash);
            using var restoreClient = new TcpClient();
            await restoreClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var restoreStream = restoreClient.GetStream();
            await SendHelloAsync(restoreStream, cancellationToken, persistedToken);
            var response = await AwaitWithin(
                ReadAsync(restoreStream, cancellationToken),
                cancellationToken);

            Assert.Equal(Envelope.PayloadOneofCase.Error, response!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, response.Error.Code);
            Assert.Equal(0, saveProvider.ImportCount);
            Assert.Single(initializer.Selections);
            Assert.Equal(111UL, commandServer.CurrentTick);
            Assert.Equal(1, server.PendingCommandCount);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RulesetReloadIsRejectedBeforeTargetMigrationWhileFutureWorkIsQueued()
    {
        ulong physicalTick = 111;
        var initial = new ReloadableHostAdapter("initial");
        var replacement = new ReloadableHostAdapter("replacement");
        var candidates = new Queue<ReloadableHostAdapter>([initial, replacement]);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => Volatile.Read(ref physicalTick)),
            (_, _) => candidates.Dequeue());
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var commandServer = new AuthoritativeCommandServer(leasedAdapter);
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20)),
            new DelegateAuthoritativeClock(() => Volatile.Read(ref physicalTick)),
            gameAdapter: leasedAdapter) {
            UseClockGatedCommandScheduling = true,
        };
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken, "clock-gated-reload-fence");
        await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);
        await SendBatchAsync(
            stream,
            cancellationToken,
            Attack(1, requestedTick: 125, targetEntityId: 1));
        await WaitForPendingCountAsync(server, 1, cancellationToken);
        var identityBeforeReload = server.ActiveRulesetIdentityHash;

        var exception = Assert.Throws<InvalidOperationException>(() => server.TryReloadRuleset(
            CreateReloadManifest("2", "replacement"),
            [new ReloadableModule("rules", "2.0.0")]));

        Assert.Contains("queued", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(initial.Disposed);
        Assert.False(replacement.SessionStateImported);
        Assert.Equal(identityBeforeReload, server.ActiveRulesetIdentityHash);
        Assert.Equal(1, server.PendingCommandCount);
    }

    [Fact]
    public async Task RejectedColdRestoreDoesNotCacheAcrossDrainAndRulesetReload()
    {
        ulong physicalTick = 111;
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Clock-Gated-Cold-Reload-" + Guid.NewGuid().ToString("N"));
        const string ownerToken = "clock-gated-cold-reload-owner";
        const string persistedToken = "clock-gated-cold-reload-session";
        var saveStore = new AuthoritativeSaveStore(saveRoot);
        var initial = new ReloadableHostAdapter("initial");
        var replacement = new ReloadableHostAdapter("replacement");
        var candidates = new Queue<ReloadableHostAdapter>([initial, replacement]);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => Volatile.Read(ref physicalTick)),
            (_, _) => candidates.Dequeue());
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        try {
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                new ProtocolHandshake(new ProtocolServerIdentity(
                    "server-build",
                    "0.1.0",
                    "content-hash",
                    20)),
                new DelegateAuthoritativeClock(() => Volatile.Read(ref physicalTick)),
                gameAdapter: leasedAdapter,
                saveStore: saveStore) {
                UseClockGatedCommandScheduling = true,
            };
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var ownerClient = new TcpClient();
            await ownerClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var ownerStream = ownerClient.GetStream();
            await SendHelloAsync(ownerStream, cancellationToken, ownerToken);
            await ReadHandshakeSnapshotAsync(ownerStream, 111, cancellationToken);
            await SendBatchAsync(
                ownerStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 1));
            await WaitForPendingCountAsync(server, 1, cancellationToken);

            new AuthoritativeSessionStore(saveRoot).Save(
                SessionIdForToken(persistedToken),
                "{}",
                new Dictionary<ulong, CommandResult>(),
                server.ActiveRulesetIdentityHash);
            using (var coldClient = new TcpClient()) {
                await coldClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var coldStream = coldClient.GetStream();
                await SendHelloAsync(coldStream, cancellationToken, persistedToken);
                var coldError = await AwaitWithin(
                    ReadAsync(coldStream, cancellationToken),
                    cancellationToken);
                Assert.Equal(Envelope.PayloadOneofCase.Error, coldError!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, coldError.Error.Code);
            }
            Assert.Equal(0, initial.ImportCount);

            Volatile.Write(ref physicalTick, 130);
            var ownerAcknowledgement = await AwaitWithin(
                ReadAsync(ownerStream, cancellationToken),
                cancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                Assert.Single(ownerAcknowledgement!.CommandAck.Results).Status);
            Assert.True(server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]).Succeeded);
            Assert.Equal(1, replacement.ImportCount);
            Assert.Contains(
                SessionIdForToken(ownerToken),
                replacement.ImportedSessionIds);

            using var retryClient = new TcpClient();
            await retryClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var retryStream = retryClient.GetStream();
            await SendHelloAsync(
                retryStream,
                cancellationToken,
                persistedToken,
                contentManifestHash: server.ActiveContentManifestHash,
                rulesetIdentityHash: server.ActiveRulesetIdentityHash);
            var staleError = await AwaitWithin(
                ReadAsync(retryStream, cancellationToken),
                cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Error, staleError!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, staleError.Error.Code);
            Assert.Contains("ruleset identity", staleError.Error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, replacement.ImportCount);
            Assert.DoesNotContain(
                SessionIdForToken(persistedToken),
                replacement.ImportedSessionIds);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PendingCapacityRejectsTheSecondBatchWithoutAReceiptAndPreservesTheFirstOwner()
    {
        ulong physicalTick = 111;
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Clock-Gated-Capacity-" + Guid.NewGuid().ToString("N"));
        const string firstToken = "clock-gated-capacity-first";
        const string secondToken = "clock-gated-capacity-second";
        var saveStore = new AuthoritativeSaveStore(saveRoot);
        var executor = new RecordingExecutor();
        try {
            await using var server = CreateServer(
                new AuthoritativeCommandServer(executor),
                () => Volatile.Read(ref physicalTick),
                executor,
                saveProvider: new ToggleSaveProvider(),
                saveStore: saveStore,
                maximumPendingCommands: 1);
            server.Start();
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken, firstToken);
            await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
            await SendBatchAsync(
                firstStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 1));
            await WaitForPendingCountAsync(server, 1, cancellationToken);
            var firstResponse = ReadAsync(firstStream, cancellationToken);

            using var secondClient = new TcpClient();
            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var secondStream = secondClient.GetStream();
            await SendHelloAsync(secondStream, cancellationToken, secondToken);
            await ReadHandshakeSnapshotAsync(secondStream, 111, cancellationToken);
            await SendBatchAsync(
                secondStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 2));
            var capacityError = await AwaitWithin(
                ReadAsync(secondStream, cancellationToken),
                cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Error, capacityError!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, capacityError.Error.Code);
            Assert.Contains("capacity", capacityError.Error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(new AuthoritativeCommandReceiptStore(saveRoot)
                .Load(SessionIdForToken(secondToken)));
            Assert.Equal(1, server.PendingCommandCount);

            Volatile.Write(ref physicalTick, 130);
            var firstAcknowledgement = await AwaitWithin(firstResponse, cancellationToken);
            var firstResult = Assert.Single(firstAcknowledgement!.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, firstResult.Status);
            Assert.Equal([1UL], executor.TargetEntityIds);
            Assert.Single(new AuthoritativeCommandReceiptStore(saveRoot)
                .Load(SessionIdForToken(firstToken)));
            Assert.Equal(0, server.PendingCommandCount);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PeriodicPublicationFaultCancelsFutureWaiterAndDisposeRethrowsAfterCleanup()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var saveProvider = new ToggleSaveProvider();
        var server = CreateServer(
            new AuthoritativeCommandServer(executor),
            () => Volatile.Read(ref physicalTick),
            executor,
            saveProvider);
        server.Start();
        var disposeAttempted = false;
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, "clock-gated-periodic-fault");
            await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);
            await SendBatchAsync(
                stream,
                cancellationToken,
                Attack(1, requestedTick: ulong.MaxValue, targetEntityId: 1));
            var response = ReadAsync(stream, cancellationToken);
            await WaitForPendingCountAsync(server, 1, cancellationToken);

            saveProvider.ThrowOnExport = true;
            Volatile.Write(ref physicalTick, 112);
            var completionFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.Completion.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
            Assert.Equal("checkpoint failed", completionFailure.Message);
            await AwaitConnectionClosureAsync(response, cancellationToken);
            Assert.Equal(0, server.PendingCommandCount);

            disposeAttempted = true;
            var disposalFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.DisposeAsync().AsTask());
            Assert.Equal("checkpoint failed", disposalFailure.Message);
        } finally {
            if (!disposeAttempted) {
                try {
                    await server.DisposeAsync();
                } catch {
                }
            }
        }
    }

    [Fact]
    public async Task ClientFailureObserverFaultIsReportedByDisposeAfterHandlerCleanup()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var saveProvider = new ToggleSaveProvider();
        var server = CreateServer(
            new AuthoritativeCommandServer(executor),
            () => Volatile.Read(ref physicalTick),
            executor,
            saveProvider,
            clientFailureObserver: _ => throw new InvalidOperationException("observer failed"));
        server.Start();
        var disposeAttempted = false;
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, "clock-gated-observer-fault");
            await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);
            // Fail only the command's own durable publication. A persistent
            // failure would also fault the periodic checkpoint loop, racing a
            // second whole-host failure against the client failure under test.
            saveProvider.FailNextExportWhen = () => executor.CallCount > 0;
            await SendBatchAsync(
                stream,
                cancellationToken,
                Attack(1, requestedTick: 111, targetEntityId: 1));
            await AwaitConnectionClosureAsync(
                ReadAsync(stream, cancellationToken),
                cancellationToken);
            await WaitUntilAsync(
                () => server.LastClientFailure is not null,
                cancellationToken);
            Assert.IsType<InvalidOperationException>(server.LastClientFailure);
            Assert.Equal("checkpoint failed", server.LastClientFailure!.Message);
            var completionFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.Completion.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
            Assert.Equal("observer failed", completionFailure.Message);

            disposeAttempted = true;
            var disposalFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.DisposeAsync().AsTask());
            Assert.Equal("observer failed", disposalFailure.Message);
        } finally {
            if (!disposeAttempted) {
                try {
                    await server.DisposeAsync();
                } catch {
                }
            }
        }
    }

    [Fact]
    public async Task PoisonedLaneIsReportedWhenHostCancellationPrecedesTheWriter()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var saveProvider = new ToggleSaveProvider();
        var writerWoken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdWriter = 0;
        var server = CreateServer(
            new AuthoritativeCommandServer(executor),
            () => Volatile.Read(ref physicalTick),
            executor,
            saveProvider,
            writerWakeGate: async _ => {
                // Hold the first armed wake so host cancellation wins before dequeue.
                if (Interlocked.Exchange(ref holdWriter, 0) == 0)
                    return;
                writerWoken.TrySetResult();
                await releaseWriter.Task;
            });
        server.Start();
        var disposeAttempted = false;
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, "clock-gated-unobserved-lane");
            await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);
            Volatile.Write(ref holdWriter, 1);
            await SendBatchAsync(
                stream,
                cancellationToken,
                Attack(1, requestedTick: 112, targetEntityId: 1));
            var response = ReadAsync(stream, cancellationToken);
            await writerWoken.Task.WaitAsync(cancellationToken);
            await WaitForPendingCountAsync(server, 1, cancellationToken);

            // At tick 112 the command's durable publication fails and poisons
            // the lane; the periodic checkpoint then fails and cancels the host.
            saveProvider.ThrowOnExport = true;
            Volatile.Write(ref physicalTick, 112);
            var completionFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.Completion.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
            Assert.Equal("checkpoint failed", completionFailure.Message);
            Assert.Equal(1, executor.CallCount);
            Assert.Null(server.LastClientFailure);

            releaseWriter.TrySetResult();
            await AwaitConnectionClosureAsync(response, cancellationToken);
            await WaitUntilAsync(
                () => server.LastClientFailure is not null,
                cancellationToken);
            Assert.IsType<InvalidOperationException>(server.LastClientFailure);
            Assert.Equal("checkpoint failed", server.LastClientFailure!.Message);
            Assert.NotSame(completionFailure, server.LastClientFailure);

            disposeAttempted = true;
            var disposalFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.DisposeAsync().AsTask());
            Assert.Equal("checkpoint failed", disposalFailure.Message);
        } finally {
            releaseWriter.TrySetResult();
            if (!disposeAttempted) {
                try {
                    await server.DisposeAsync();
                } catch {
                }
            }
        }
    }

    [Fact]
    public async Task FutureCommandsStayPendingUntilPhysicalClockAdvancesAndDrainInDueOrder()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();

        await SendHelloAsync(firstStream, cancellationToken, "clock-gated-first");
        await SendHelloAsync(secondStream, cancellationToken, "clock-gated-second");
        await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
        await ReadHandshakeSnapshotAsync(secondStream, 111, cancellationToken);

        await SendBatchAsync(
            firstStream,
            cancellationToken,
            Attack(1, requestedTick: 125, targetEntityId: 1));
        await SendBatchAsync(
            secondStream,
            cancellationToken,
            Attack(1, requestedTick: 120, targetEntityId: 2));
        var firstResponse = ReadAsync(firstStream, cancellationToken);
        var secondResponse = ReadAsync(secondStream, cancellationToken);

        await AssertNotCompletedAsync(firstResponse, cancellationToken);
        await AssertNotCompletedAsync(secondResponse, cancellationToken);
        Assert.Equal(111UL, commandServer.CurrentTick);
        await WaitForPendingCountAsync(server, 2, cancellationToken);
        Assert.Empty(executor.AppliedTicks);
        Assert.Equal([111UL, 111UL], executor.SnapshotTicks);

        Volatile.Write(ref physicalTick, 130);
        var firstAcknowledgement = await AwaitWithin(firstResponse, cancellationToken);
        var secondAcknowledgement = await AwaitWithin(secondResponse, cancellationToken);
        var firstResult = Assert.Single(firstAcknowledgement!.CommandAck.Results);
        var secondResult = Assert.Single(secondAcknowledgement!.CommandAck.Results);
        Assert.Equal(CommandStatus.Accepted, firstResult.Status);
        Assert.Equal(125UL, firstResult.AppliedTick);
        Assert.Equal(CommandStatus.Accepted, secondResult.Status);
        Assert.Equal(120UL, secondResult.AppliedTick);
        Assert.Equal([120UL, 125UL], executor.AppliedTicks);
        Assert.Equal(130UL, commandServer.CurrentTick);
        Assert.Equal(0, server.PendingCommandCount);

        var firstSnapshot = await AwaitWithin(ReadAsync(firstStream, cancellationToken), cancellationToken);
        var secondSnapshot = await AwaitWithin(ReadAsync(secondStream, cancellationToken), cancellationToken);
        Assert.Equal(130UL, firstSnapshot!.Snapshot.Tick);
        Assert.Equal(130UL, secondSnapshot!.Snapshot.Tick);
    }

    [Fact]
    public async Task AscendingBatchExecutesAtEachRequestedTickAfterClockAdvance()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken, "clock-gated-ascending");
        await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);

        await SendBatchAsync(
            stream,
            cancellationToken,
            Attack(1, requestedTick: 120, targetEntityId: 1),
            Attack(2, requestedTick: 125, targetEntityId: 2));
        var response = ReadAsync(stream, cancellationToken);
        await AssertNotCompletedAsync(response, cancellationToken);
        await WaitForPendingCountAsync(server, 2, cancellationToken);

        Volatile.Write(ref physicalTick, 130);
        var acknowledgement = await AwaitWithin(response, cancellationToken);
        Assert.Collection(
            acknowledgement!.CommandAck.Results,
            first => {
                Assert.Equal(CommandStatus.Accepted, first.Status);
                Assert.Equal(120UL, first.AppliedTick);
            },
            second => {
                Assert.Equal(CommandStatus.Accepted, second.Status);
                Assert.Equal(125UL, second.AppliedTick);
            });
        Assert.Equal([120UL, 125UL], executor.AppliedTicks);
        Assert.Equal(130UL, commandServer.CurrentTick);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task DescendingBatchPreservesWireOrderAndRejectsTheLateTail()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken, "clock-gated-descending");
        await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);

        await SendBatchAsync(
            stream,
            cancellationToken,
            Attack(1, requestedTick: 125, targetEntityId: 1),
            Attack(2, requestedTick: 120, targetEntityId: 2));
        var response = ReadAsync(stream, cancellationToken);
        await AssertNotCompletedAsync(response, cancellationToken);
        Volatile.Write(ref physicalTick, 130);

        var acknowledgement = await AwaitWithin(response, cancellationToken);
        Assert.Collection(
            acknowledgement!.CommandAck.Results,
            first => {
                Assert.Equal(CommandStatus.Accepted, first.Status);
                Assert.Equal(125UL, first.AppliedTick);
            },
            second => {
                Assert.Equal(CommandStatus.Rejected, second.Status);
                Assert.Equal(CommandRejectReason.TooLate, second.RejectReason);
                Assert.Equal(125UL, second.AppliedTick);
            });
        Assert.Equal([125UL], executor.AppliedTicks);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task PhysicallyLateGameplayCommandRemainsStrictlyTooLate()
    {
        ulong physicalTick = 123;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken, "clock-gated-late");
        await ReadHandshakeSnapshotAsync(stream, 123, cancellationToken);

        await SendBatchAsync(
            stream,
            cancellationToken,
            Attack(1, requestedTick: 120, targetEntityId: 1));
        var acknowledgement = await AwaitWithin(ReadAsync(stream, cancellationToken), cancellationToken);
        var result = Assert.Single(acknowledgement!.CommandAck.Results);
        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.TooLate, result.RejectReason);
        Assert.Equal(123UL, result.AppliedTick);
        Assert.Empty(executor.AppliedTicks);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task HandshakeAndSnapshotPathsKeepQueuedWorkBehindThePhysicalBoundary()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var firstClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await SendHelloAsync(firstStream, cancellationToken, "clock-gated-boundary-first");
        await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
        await SendBatchAsync(
            firstStream,
            cancellationToken,
            Attack(1, requestedTick: 120, targetEntityId: 1));
        var firstResponse = ReadAsync(firstStream, cancellationToken);
        await AssertNotCompletedAsync(firstResponse, cancellationToken);

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(secondStream, cancellationToken, "clock-gated-boundary-second");
        await ReadHandshakeSnapshotAsync(secondStream, 111, cancellationToken);
        Assert.Equal(111UL, commandServer.CurrentTick);
        await WaitForPendingCountAsync(server, 1, cancellationToken);

        Volatile.Write(ref physicalTick, 120);
        await EnvelopeCodec.WriteAsync(
            secondStream,
            new Envelope {
                SnapshotRequest = new Envelope.Types.SnapshotRequest {
                    TimingRequestId = 17,
                },
            },
            cancellationToken);
        var secondSnapshot = await AwaitWithin(ReadAsync(secondStream, cancellationToken), cancellationToken);
        var firstAcknowledgement = await AwaitWithin(firstResponse, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, secondSnapshot!.PayloadCase);
        Assert.Equal(120UL, secondSnapshot.Snapshot.Tick);
        var timingReply = await AwaitWithin(ReadAsync(secondStream, cancellationToken), cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.TimingReply, timingReply!.PayloadCase);
        Assert.Equal(17UL, timingReply.TimingReply.RequestId);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(firstAcknowledgement!.CommandAck.Results).Status);
        Assert.Equal(120UL, Assert.Single(firstAcknowledgement.CommandAck.Results).AppliedTick);
        var firstSnapshot = await AwaitWithin(ReadAsync(firstStream, cancellationToken), cancellationToken);
        Assert.Equal(120UL, firstSnapshot!.Snapshot.Tick);
        Assert.Equal([120UL], executor.AppliedTicks);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task DisconnectedQueuedCommandSurvivesAndChangedDuplicateKeepsFirstPayload()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        const string resumeToken = "clock-gated-disconnect";

        using (var disconnectedClient = new TcpClient()) {
            await disconnectedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var disconnectedStream = disconnectedClient.GetStream();
            await SendHelloAsync(disconnectedStream, cancellationToken, resumeToken);
            await ReadHandshakeSnapshotAsync(disconnectedStream, 111, cancellationToken);
            await SendBatchAsync(
                disconnectedStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 1));
            await WaitForPendingCountAsync(server, 1, cancellationToken);
            disconnectedClient.Dispose();
        }

        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await SendHelloAsync(resumedStream, cancellationToken, resumeToken);
        await ReadHandshakeSnapshotAsync(resumedStream, 111, cancellationToken);
        await SendBatchAsync(
            resumedStream,
            cancellationToken,
            Attack(1, requestedTick: 125, targetEntityId: 99));
        var duplicateResponse = ReadAsync(resumedStream, cancellationToken);
        await AssertNotCompletedAsync(duplicateResponse, cancellationToken);

        Volatile.Write(ref physicalTick, 130);
        var acknowledgement = await AwaitWithin(duplicateResponse, cancellationToken);
        var result = Assert.Single(acknowledgement!.CommandAck.Results);
        Assert.Equal(CommandStatus.Duplicate, result.Status);
        Assert.Equal(125UL, result.AppliedTick);
        Assert.Equal([1UL], executor.TargetEntityIds);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task InMemoryReconnectReportsQueuedSequenceAndKeepsFirstPayload()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(
            commandServer,
            () => Volatile.Read(ref physicalTick),
            executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        const string resumeToken = "clock-gated-in-memory-resume";

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken, resumeToken);
            await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
            await SendBatchAsync(
                firstStream,
                cancellationToken,
                Attack(1, requestedTick: 125, targetEntityId: 1));
            await WaitForPendingCountAsync(server, 1, cancellationToken);
        }

        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await SendHelloAsync(resumedStream, cancellationToken, resumeToken);
        var resumedHello = await ReadHandshakeSnapshotAsync(resumedStream, 111, cancellationToken);
        Assert.Equal(1UL, resumedHello.LastClientSequence);
        await SendBatchAsync(
            resumedStream,
            cancellationToken,
            Attack(1, requestedTick: 125, targetEntityId: 99),
            Attack(2, requestedTick: 125, targetEntityId: 2));
        var acknowledgementTask = ReadAsync(resumedStream, cancellationToken);
        await AssertNotCompletedAsync(acknowledgementTask, cancellationToken);

        Volatile.Write(ref physicalTick, 130);
        var acknowledgement = await AwaitWithin(acknowledgementTask, cancellationToken);
        Assert.Collection(
            acknowledgement!.CommandAck.Results,
            first => Assert.Equal(CommandStatus.Duplicate, first.Status),
            second => Assert.Equal(CommandStatus.Accepted, second.Status));
        Assert.Equal([1UL, 2UL], executor.TargetEntityIds);
        Assert.Equal(2, executor.CallCount);
        Assert.Equal(0, server.PendingCommandCount);
    }

    [Fact]
    public async Task ClockSamplingHoldsTheAuthoritativeLockDuringThePhysicalRead()
    {
        var clock = new BlockingClock(111);
        var commandServer = new AuthoritativeCommandServer(new RecordingExecutor());
        var server = new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20)),
            clock,
            snapshotProvider: new RecordingExecutor()) {
            UseClockGatedCommandScheduling = true,
        };
        server.Start();
        var releaseClock = false;
        try {
            using var timeout = CreateTimeout();
            var cancellationToken = timeout.Token;
            Assert.True(clock.WaitUntilEntered(cancellationToken));
            var contender = Task.Factory.StartNew(
                () => commandServer.WithSynchronizedState(() => true),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            var completed = await Task.WhenAny(
                contender,
                Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken));
            Assert.NotSame(contender, completed);
            clock.Release();
            releaseClock = true;
            Assert.True(await AwaitWithin(contender, cancellationToken));
        } finally {
            if (!releaseClock)
                clock.Release();
            try {
                await server.DisposeAsync();
            } catch {
            }
        }
    }

    [Fact]
    public async Task EventProjectionDrainsDueCommandsBeforeAdvancingTheEventBoundary()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var eventProvider = new RecordingEventProvider(executor);
        var commandServer = new AuthoritativeCommandServer(executor);
        await using var server = CreateServer(
            commandServer,
            () => Volatile.Read(ref physicalTick),
            executor,
            eventProvider: eventProvider);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        using var firstClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await SendHelloAsync(firstStream, cancellationToken, "clock-gated-event-first");
        await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
        await SendBatchAsync(
            firstStream,
            cancellationToken,
            Attack(1, requestedTick: 120, targetEntityId: 1));
        await WaitForPendingCountAsync(server, 1, cancellationToken);
        var firstResponse = ReadAsync(firstStream, cancellationToken);

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(secondStream, cancellationToken, "clock-gated-event-second");
        await ReadHandshakeSnapshotAsync(secondStream, 111, cancellationToken);
        Volatile.Write(ref physicalTick, 120);
        await EnvelopeCodec.WriteAsync(
            secondStream,
            new Envelope { SnapshotRequest = new Envelope.Types.SnapshotRequest() },
            cancellationToken);

        var eventEnvelope = await AwaitWithin(
            ReadAsync(secondStream, cancellationToken),
            cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope!.PayloadCase);
        Assert.Equal(120UL, eventEnvelope.EventBatch.Tick);
        var snapshotEnvelope = await AwaitWithin(
            ReadAsync(secondStream, cancellationToken),
            cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshotEnvelope!.PayloadCase);
        Assert.Equal(120UL, snapshotEnvelope.Snapshot.Tick);
        var firstAcknowledgement = await AwaitWithin(firstResponse, cancellationToken);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(firstAcknowledgement!.CommandAck.Results).Status);
        var firstEvents = await AwaitWithin(ReadAsync(firstStream, cancellationToken), cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.EventBatch, firstEvents!.PayloadCase);
        var firstSnapshot = await AwaitWithin(ReadAsync(firstStream, cancellationToken), cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, firstSnapshot!.PayloadCase);
        Assert.Equal([1, 1], eventProvider.ExecutorCallCountsAtDrain);
        Assert.Equal([120UL, 120UL], eventProvider.Ticks);
        Assert.Contains(
            SessionIdForToken("clock-gated-event-second"),
            eventProvider.SessionIds);
    }

    [Fact]
    public async Task ShutdownCancelsQueuedHandlersAndReleasesPendingCommands()
    {
        ulong physicalTick = 111;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var server = CreateServer(commandServer, () => Volatile.Read(ref physicalTick), executor);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, "clock-gated-shutdown");
            await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);
            await SendBatchAsync(
                stream,
                cancellationToken,
                Attack(1, requestedTick: ulong.MaxValue, targetEntityId: 1));
            var response = ReadAsync(stream, cancellationToken);
            await AssertNotCompletedAsync(response, cancellationToken);
            await WaitForPendingCountAsync(server, 1, cancellationToken);

            await server.DisposeAsync();
            Assert.Equal(0, server.PendingCommandCount);
            await AwaitConnectionClosureAsync(response, cancellationToken);
        } finally {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task PersistenceFailureSendsNoAckAndRetryPublishesTheDurableDuplicate()
    {
        // Freeze at the initial simulation tick so the injected command-publication
        // failure cannot race an unrelated periodic checkpoint export.
        ulong physicalTick = 0;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var saveProvider = new ToggleSaveProvider();
        var server = CreateServer(
            commandServer,
            () => Volatile.Read(ref physicalTick),
            executor,
            saveProvider);
        server.Start();
        using var timeout = CreateTimeout();
        var cancellationToken = timeout.Token;
        const string resumeToken = "clock-gated-persistence";
        try {
            using (var client = new TcpClient()) {
                await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var stream = client.GetStream();
                await SendHelloAsync(stream, cancellationToken, resumeToken);
                await ReadHandshakeSnapshotAsync(stream, 0, cancellationToken);
                saveProvider.ThrowOnExport = true;
                await SendBatchAsync(
                    stream,
                    cancellationToken,
                    Attack(1, requestedTick: 0, targetEntityId: 1));
                var failedResponse = ReadAsync(stream, cancellationToken);
                await AwaitConnectionClosureAsync(failedResponse, cancellationToken);
            }

            await WaitUntilAsync(
                () => server.LastClientFailure is not null,
                cancellationToken);
            Assert.IsType<InvalidOperationException>(server.LastClientFailure);
            Assert.False(server.Completion.IsCompleted);
            Assert.Equal(1, executor.CallCount);
            saveProvider.ThrowOnExport = false;
            using var retryClient = new TcpClient();
            await retryClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var retryStream = retryClient.GetStream();
            await SendHelloAsync(retryStream, cancellationToken, resumeToken);
            await ReadHandshakeSnapshotAsync(retryStream, 0, cancellationToken);
            await SendBatchAsync(
                retryStream,
                cancellationToken,
                Attack(1, requestedTick: 0, targetEntityId: 99));
            var retryAcknowledgement = await AwaitWithin(
                ReadAsync(retryStream, cancellationToken),
                cancellationToken);
            var retryResult = Assert.Single(retryAcknowledgement!.CommandAck.Results);
            Assert.Equal(CommandStatus.Duplicate, retryResult.Status);
            Assert.Equal(1, executor.CallCount);
        } finally {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClockGatedReceiptFailureLeavesNoAckAndFreshHostReturnsDurableDuplicate()
    {
        if (!OperatingSystem.IsWindows())
            return; // Uses Windows no-delete sharing to fail atomic replacement.

        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Clock-Gated-Receipt-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "clock-gated-receipt-restart";
        var sessionId = SessionIdForToken(resumeToken);
        var command = Attack(1, requestedTick: 0, targetEntityId: 1);
        var saveStore = new AuthoritativeSaveStore(saveRoot);
        try {
            var executor = new RecordingExecutor();
            await using (var server = CreateServer(
                             new AuthoritativeCommandServer(executor),
                             () => 0,
                             executor,
                             saveProvider: new ToggleSaveProvider(),
                             saveStore: saveStore)) {
                server.Start();
                using var timeout = CreateTimeout();
                var cancellationToken = timeout.Token;
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var stream = client.GetStream();
                await SendHelloAsync(stream, cancellationToken, resumeToken);
                await ReadHandshakeSnapshotAsync(stream, 0, cancellationToken);

                var receiptPath = Path.Combine(saveRoot, sessionId + ".commands.json");
                Assert.True(File.Exists(receiptPath));
                using (var noDelete = new FileStream(
                           receiptPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite)) {
                    await SendBatchAsync(stream, cancellationToken, command);
                    await AwaitConnectionClosureAsync(
                        ReadAsync(stream, cancellationToken),
                        cancellationToken);
                }

                Assert.Equal(1, executor.CallCount);
                Assert.Empty(new AuthoritativeCommandReceiptStore(saveRoot).Load(sessionId));
                Assert.Single(new AuthoritativeSessionStore(saveRoot).Load(sessionId)!.CommandReceipts);
            }

            var freshExecutor = new RecordingExecutor();
            await using (var freshServer = CreateServer(
                             new AuthoritativeCommandServer(freshExecutor),
                             () => 0,
                             freshExecutor,
                             saveProvider: new ToggleSaveProvider(),
                             saveStore: saveStore)) {
                freshServer.Start();
                using var timeout = CreateTimeout();
                var cancellationToken = timeout.Token;
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, freshServer.Port, cancellationToken);
                await using var stream = client.GetStream();
                await SendHelloAsync(stream, cancellationToken, resumeToken);
                await ReadHandshakeSnapshotAsync(stream, 0, cancellationToken);
                await SendBatchAsync(
                    stream,
                    cancellationToken,
                    command.Clone());
                var acknowledgement = await AwaitWithin(
                    ReadAsync(stream, cancellationToken),
                    cancellationToken);
                var result = Assert.Single(acknowledgement!.CommandAck.Results);
                Assert.Equal(CommandStatus.Duplicate, result.Status);
                Assert.Equal(0, freshExecutor.CallCount);
                Assert.Equal(0, freshServer.PendingCommandCount);
            }
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    private static AuthoritativeTcpServer CreateServer(
        AuthoritativeCommandServer commandServer,
        Func<ulong> physicalTickProvider,
        IAuthoritativeSnapshotProvider snapshotProvider,
        IAuthoritativeSaveProvider? saveProvider = null,
        IAuthoritativeParticipantInitializer? participantInitializer = null,
        AuthoritativeSaveStore? saveStore = null,
        int? maximumPendingCommands = null,
        Action<Exception>? clientFailureObserver = null,
        IAuthoritativeEventProvider? eventProvider = null,
        Func<CancellationToken, Task>? writerWakeGate = null)
    {
        return new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20)),
            physicalTickProvider,
            snapshotProvider: snapshotProvider,
            eventProvider: eventProvider,
            saveProvider: saveProvider,
            participantInitializer: participantInitializer,
            saveStore: saveStore) {
            UseClockGatedCommandScheduling = true,
            MaximumPendingCommands = maximumPendingCommands
                ?? ClockGatedCommandScheduler.DefaultMaximumPendingCommands,
            ClientFailureObserver = clientFailureObserver,
            ClockGatedWriterWakeGateForTests = writerWakeGate,
        };
    }

    private static string SessionIdForToken(string resumeToken)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();

    private static ContentManifest CreateReloadManifest(string version, string value)
    {
        return new ContentManifest(
            "rules",
            version,
            [new ContentPack(
                "base",
                version,
                [TsvTable.Parse("rules.tsv", $"id\tvalue\na\t{value}\n")])]);
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout;
    }

    private static async Task SendBatchAsync(
        Stream stream,
        CancellationToken cancellationToken,
        params Command[] commands)
    {
        var batch = new CommandBatch();
        batch.Commands.Add(commands);
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope { CommandBatch = batch },
            cancellationToken);
    }

    private static ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string resumeToken,
        bool supportsClockGatedCommands = true,
        string? contentManifestHash = null,
        string? rulesetIdentityHash = null)
    {
        return EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = contentManifestHash ?? "content-hash",
                ResumeToken = resumeToken,
                RulesetIdentityHash = rulesetIdentityHash ?? string.Empty,
                SupportsClockGatedCommands = supportsClockGatedCommands,
            },
        }, cancellationToken);
    }

    private static async Task<ServerHello> ReadHandshakeSnapshotAsync(
        Stream stream,
        ulong expectedTick,
        CancellationToken cancellationToken)
    {
        var hello = await AwaitWithin(ReadAsync(stream, cancellationToken), cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        Assert.Equal(CommandSchedulingMode.ClockGated, hello.ServerHello.CommandSchedulingMode);
        var snapshot = await AwaitWithin(ReadAsync(stream, cancellationToken), cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        Assert.Equal(expectedTick, snapshot.Snapshot.Tick);
        return hello.ServerHello;
    }

    private static Task<Envelope?> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => EnvelopeCodec.ReadAsync(stream, cancellationToken).AsTask();

    private static async Task AssertNotCompletedAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(
            task,
            Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken));
        Assert.NotSame(task, completed);
    }

    private static async Task WaitForPendingCountAsync(
        AuthoritativeTcpServer server,
        int expected,
        CancellationToken cancellationToken)
    {
        await WaitUntilAsync(
            () => server.PendingCommandCount == expected,
            cancellationToken);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        using var waitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate())
            await Task.Delay(TimeSpan.FromMilliseconds(10), waitTimeout.Token);
    }

    private static Task<T> AwaitWithin<T>(Task<T> task, CancellationToken cancellationToken)
        => task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

    private static async Task AwaitConnectionClosureAsync(
        Task<Envelope?> readTask,
        CancellationToken cancellationToken)
    {
        try {
            var envelope = await AwaitWithin(readTask, cancellationToken);
            Assert.Null(envelope);
        } catch (IOException) {
        } catch (SocketException) {
        }
    }

    private static Command Attack(ulong clientSequence, ulong requestedTick, uint targetEntityId)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = targetEntityId },
        };
    }

    private sealed class RecordingExecutor
        : IAuthoritativeCommandExecutor,
          IAuthoritativeSnapshotProvider
    {
        private readonly object synchronization = new();

        public int CallCount { get; private set; }

        public List<ulong> AppliedTicks { get; } = [];

        public List<ulong> SnapshotTicks { get; } = [];

        public List<ulong> TargetEntityIds { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            lock (synchronization) {
                CallCount++;
                AppliedTicks.Add(appliedTick);
                TargetEntityIds.Add(command.AttackRequested is { } attack
                    ? attack.TargetEntityId
                    : 0UL);
            }
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            lock (synchronization)
                SnapshotTicks.Add(tick);
            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
        }
    }

    private sealed class RecordingParticipantInitializer
        : IAuthoritativeParticipantInitializer
    {
        public List<(string SessionId, AuthoritativeParticipantSelection Selection)> Selections { get; } = [];

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            Selections.Add((sessionId, selection));
        }
    }

    private sealed class RecordingEventProvider(RecordingExecutor executor)
        : IAuthoritativeEventProvider
    {
        public List<int> ExecutorCallCountsAtDrain { get; } = [];

        public List<string> SessionIds { get; } = [];

        public List<ulong> Ticks { get; } = [];

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        {
            SessionIds.Add(sessionId);
            ExecutorCallCountsAtDrain.Add(executor.CallCount);
            Ticks.Add(tick);
            return new EventBatch {
                Tick = tick,
                Events = { new GameEvent { Experience = new ExperienceEvent() } },
            };
        }
    }

    private sealed class BlockingClock(ulong tick) : IAuthoritativeClock
    {
        private readonly ManualResetEventSlim entered = new(initialState: false);
        private readonly ManualResetEventSlim release = new(initialState: false);
        private int firstSample = 1;

        public ulong CurrentTick
        {
            get {
                if (Interlocked.Exchange(ref firstSample, 0) == 1) {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The clock sample was not released.");
                }
                return tick;
            }
        }

        public bool WaitUntilEntered(CancellationToken cancellationToken)
            => entered.Wait(TimeSpan.FromSeconds(5), cancellationToken);

        public void Release()
            => release.Set();
    }

    private sealed class ToggleSaveProvider : IAuthoritativeSaveProvider
    {
        public bool ThrowOnExport { get; set; }

        /** One-shot failure for the first export after the predicate holds. */
        public Func<bool>? FailNextExportWhen { get; set; }

        public int ImportCount { get; private set; }

        public string ExportPlayerSave(string sessionId)
        {
            if (FailNextExportWhen?.Invoke() == true) {
                FailNextExportWhen = null;
                throw new InvalidOperationException("checkpoint failed");
            }
            if (ThrowOnExport)
                throw new InvalidOperationException("checkpoint failed");
            return "{}";
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            ImportCount++;
        }
    }

    private sealed class ReloadableHostAdapter(string name)
        : IAuthoritativeGameHostAdapter, IDisposable
    {
        public bool Disposed { get; private set; }

        public bool SessionStateImported { get; private set; }

        public int ImportCount { get; private set; }

        public List<string> ImportedSessionIds { get; } = [];

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            var snapshot = new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot {
                    EntityId = entityId,
                    CharacterName = name,
                } },
            };
            snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
            return snapshot;
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => null;

        public string ExportPlayerSave(string sessionId)
            => ExportSessionState(sessionId);

        public void ImportPlayerSave(string sessionId, string serializedSave)
            => ImportSessionState(sessionId, serializedSave);

        public string ExportSessionState(string sessionId)
            => name;

        public void ImportSessionState(string sessionId, string serializedState)
        {
            ImportCount++;
            ImportedSessionIds.Add(sessionId);
            SessionStateImported = true;
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
        }

        public void Dispose()
            => Disposed = true;
    }

    private sealed class ReloadableModule(string id, string version) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } =
            new(id, version, $"hash-{version}");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }
}
