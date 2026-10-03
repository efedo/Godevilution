using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Client.Protocol.Tests;

/** Production TCP acceptance for a committed command whose acknowledgement is lost. */
public sealed class CohortPendingCommandRecoveryTests
{
    private static readonly ConditionalWeakTable<AuthoritativeClient, MessageBacklog> messageBacklogs = new();

    [Fact]
    public async Task PendingCommandReplaysToCohortReplacementWithoutDuplicatingItsEffect()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var cancellationToken = timeout.Token;
        var stage = "start-original-server";
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Cohort-Pending-Recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        ProductionServer? originalServer = null;
        ProductionServer? replacementServer = null;
        AckLossRelay? relay = null;
        AuthoritativeClient? client = null;
        Exception? primaryFailure = null;
        try {
            originalServer = await ProductionServer.StartAsync(
                saveRoot,
                portOverride: null,
                includeSeed: true,
                cancellationToken: cancellationToken);
            relay = new AckLossRelay(originalServer.Port);
            var options = new ClientConnectionOptions(
                "127.0.0.1",
                relay.Port,
                "godot-cohort-pending-test",
                ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                originalServer.ContentHash,
                originalServer.RulesetHash);
            client = new AuthoritativeClient(options);
            stage = "connect-original-client";
            await client.ConnectAsync(cancellationToken);
            Assert.Equal(GameMode.Multiplayer, client.ServerGameMode);
            Assert.True(client.IsClockGatedCommandScheduling);
            var model = new AuthoritativeClientModel();
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null,
                cancellationToken));
            var initialPlayer = Assert.Single(model.Snapshot!.Players);
            var initialEntityId = initialPlayer.EntityId;
            var initialPositionY = initialPlayer.PositionY;

            stage = "commit-original-command";
            var command = new Command {
                MoveRequested = new MoveRequested { DirectionY = 1 },
            };
            var sequence = client.QueueRealtime(command, model.CurrentTick);
            var originalReceipt = await WaitForWithheldReceiptWhilePollingAsync(
                client,
                relay,
                cancellationToken);
            var originalCommand = await relay.WaitForOriginalCommandAsync(cancellationToken);
            Assert.Equal(sequence, originalReceipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, originalReceipt.Status);
            Assert.Equal(CommandRejectReason.Unspecified, originalReceipt.RejectReason);
            Assert.Equal(1, client.PendingCommandCount);
            var persistedOriginal = await WaitForPersistedReceiptAsync(
                saveRoot,
                client.SessionToken,
                initialEntityId,
                sequence,
                cancellationToken);
            Assert.Equal(originalReceipt.ServerReceiptSequence, persistedOriginal.ServerReceiptSequence);
            Assert.Equal(originalReceipt.AppliedTick, persistedOriginal.AppliedTick);

            var committedSnapshot = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } snapshot
                    && snapshot.Players.Any(player =>
                        player.EntityId == initialEntityId
                        && player.PositionY == initialPositionY + 1),
                cancellationToken);
            model.Apply(committedSnapshot);
            Assert.Equal(initialEntityId, model.PlayerEntityId);
            Assert.Equal(initialPositionY + 1, model.Snapshot!.Players.Single().PositionY);

            stage = "stop-original-server";
            relay.ExpectOwnedHostLoss();
            originalServer.Kill();
            await originalServer.WaitForExitAsync(cancellationToken);
            await WaitUntilAsync(() => !client.IsConnected, cancellationToken);
            ClearMessageBacklogAndDrain(client);
            Assert.Equal(1, client.PendingCommandCount);

            stage = "start-replacement-server";
            replacementServer = await ProductionServer.StartAsync(
                saveRoot,
                originalServer.Port,
                includeSeed: false,
                cancellationToken: cancellationToken);
            stage = "reconnect-original-client";
            relay.VerifyHealthy();
            await client.ReconnectAsync(cancellationToken);
            relay.VerifyHealthy();
            var resumedSnapshot = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null,
                cancellationToken);
            model.Apply(resumedSnapshot);
            var resumedPlayer = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(initialEntityId, resumedPlayer.EntityId);
            Assert.Equal(initialPositionY + 1, resumedPlayer.PositionY);
            var resumedTick = resumedSnapshot.Snapshot!.Tick;

            stage = "recover-original-command";
            var replayedCommand = await WaitForRecoveredCommandWhilePollingAsync(
                client,
                relay,
                cancellationToken);
            Assert.Equal(originalCommand.ToByteArray(), replayedCommand.ToByteArray());
            Assert.Equal(sequence, replayedCommand.ClientSequence);
            Assert.Equal(originalCommand.RequestedTick, replayedCommand.RequestedTick);
            Assert.Equal(
                originalCommand.MoveRequested.ToByteArray(),
                replayedCommand.MoveRequested.ToByteArray());
            var duplicateReceipt = await WaitForCommandResultAsync(
                client,
                model,
                sequence,
                cancellationToken);
            Assert.Equal(CommandStatus.Duplicate, duplicateReceipt.Status);
            Assert.True(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(duplicateReceipt));
            Assert.Equal(originalReceipt.ServerReceiptSequence, duplicateReceipt.ServerReceiptSequence);
            Assert.Equal(originalReceipt.AppliedTick, duplicateReceipt.AppliedTick);
            Assert.Equal(originalReceipt.RejectReason, duplicateReceipt.RejectReason);
            Assert.Equal(0, client.PendingCommandCount);
            var duplicateSettledSnapshot = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick > resumedTick
                    && snapshot.Tick >= duplicateReceipt.AppliedTick
                    && snapshot.Players.Any(player =>
                        player.EntityId == initialEntityId
                        && player.PositionY == initialPositionY + 1
                        && player.CurrentAction is null
                        && player.CombatMode == PlayerCombatMode.Stand),
                cancellationToken);
            model.Apply(duplicateSettledSnapshot);
            Assert.Equal(initialEntityId, model.PlayerEntityId);
            Assert.Equal(initialPositionY + 1, model.Snapshot!.Players.Single().PositionY);

            stage = "accept-next-command";
            var nextCommand = new Command {
                MoveRequested = new MoveRequested { DirectionX = 1 },
            };
            var nextSequence = client.QueueRealtime(nextCommand, model.CurrentTick);
            Assert.Equal(sequence + 1, nextSequence);
            await client.PollAsync(cancellationToken);
            var nextReceipt = await WaitForCommandResultAsync(
                client,
                model,
                nextSequence,
                cancellationToken);
            Assert.Equal(CommandStatus.Accepted, nextReceipt.Status);
            Assert.True(nextReceipt.ServerReceiptSequence > duplicateReceipt.ServerReceiptSequence);
            var nextSettledSnapshot = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= nextReceipt.AppliedTick
                    && snapshot.Players.Any(player =>
                        player.EntityId == initialEntityId
                        && player.PositionX == initialPlayer.PositionX + 1
                        && player.PositionY == initialPositionY + 1
                        && player.CurrentAction is null
                        && player.CombatMode == PlayerCombatMode.Stand),
                cancellationToken);
            model.Apply(nextSettledSnapshot);
            var nextPersisted = await WaitForPersistedReceiptAsync(
                saveRoot,
                client.SessionToken,
                initialEntityId,
                nextSequence,
                cancellationToken);
            Assert.True(nextPersisted.ServerReceiptSequence > duplicateReceipt.ServerReceiptSequence);
            Assert.Equal(nextReceipt.AppliedTick, nextPersisted.AppliedTick);
            Assert.Equal(initialEntityId, model.PlayerEntityId);
            Assert.Equal(initialPlayer.PositionX + 1, model.Snapshot!.Players.Single().PositionX);
            Assert.Equal(initialPositionY + 1, model.Snapshot!.Players.Single().PositionY);
            relay.VerifyHealthy();
            Assert.True(relay.RecoveredTransmissionCount >= 1);
        } catch (Exception exception) {
            primaryFailure = new InvalidOperationException(
                $"Cohort pending-command recovery failed at {stage}; retained fixture: {saveRoot}.",
                exception);
            throw primaryFailure;
        } finally {
            Exception? cleanupFailure = null;
            void RecordCleanupFailure(string stage, Exception exception)
            {
                if (primaryFailure is not null)
                    primaryFailure.Data["Cleanup." + stage] = exception;
                else if (cleanupFailure is null)
                    cleanupFailure ??= new InvalidOperationException(
                        $"Cohort pending-recovery cleanup failed at {stage}.",
                        exception);
                else
                    cleanupFailure.Data["Cleanup." + stage] = exception;
            }
            try {
                if (client is not null)
                    await client.DisposeAsync().AsTask().WaitAsync(
                        TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            } catch (Exception exception) {
                RecordCleanupFailure("client", exception);
            }
            try {
                if (relay is not null)
                    await relay.DisposeAsync();
            } catch (Exception exception) {
                RecordCleanupFailure("relay", exception);
            }
            try {
                replacementServer?.Dispose();
            } catch (Exception exception) {
                RecordCleanupFailure("replacement-server", exception);
            }
            try {
                originalServer?.Dispose();
            } catch (Exception exception) {
                RecordCleanupFailure("original-server", exception);
            }
            try {
                if (primaryFailure is null && cleanupFailure is null)
                    DeleteFixtureRootIfOwned(saveRoot);
            } catch (Exception exception) {
                RecordCleanupFailure("fixture-root", exception);
            }
            if (primaryFailure is null && cleanupFailure is not null)
                throw cleanupFailure;
        }
    }

    private static async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500; attempt++) {
            if (TryTakeMessage(client, predicate, out var message))
                return message;
            await client.PollAsync(cancellationToken);
            if (TryTakeMessage(client, predicate, out message))
                return message;
            await Task.Delay(20, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the production authoritative message.");
    }

    private static async Task<CommandResult> WaitForCommandResultAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500; attempt++) {
            await client.PollAsync(cancellationToken);
            if (TryTakeMessage(
                    client,
                    message => message.Acknowledgement?.Results
                        .Any(candidate => candidate.ClientSequence == sequence) == true,
                    out var message)) {
                model.Apply(message);
                return message.Acknowledgement!.Results
                    .Single(candidate => candidate.ClientSequence == sequence);
            }
            await Task.Delay(20, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the production duplicate receipt.");
    }

    private static bool TryTakeMessage(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate,
        out AuthoritativeClientMessage message)
    {
        var backlog = messageBacklogs.GetOrCreateValue(client);
        foreach (var incoming in client.DrainMessages())
            backlog.Messages.Add(incoming);
        for (var index = 0; index < backlog.Messages.Count; index++) {
            var candidate = backlog.Messages[index];
            if (candidate.Error is not null)
                throw new InvalidDataException(candidate.Error.Detail);
            if (!predicate(candidate))
                continue;
            backlog.Messages.RemoveAt(index);
            message = candidate;
            return true;
        }
        message = null!;
        return false;
    }

    private static void ClearMessageBacklogAndDrain(AuthoritativeClient client)
    {
        messageBacklogs.Remove(client);
        _ = client.DrainMessages();
    }

    private static async Task<CommandResult> WaitForWithheldReceiptWhilePollingAsync(
        AuthoritativeClient client,
        AckLossRelay relay,
        CancellationToken cancellationToken)
    {
        var receipt = relay.WaitForWithheldReceiptAsync(cancellationToken);
        for (var attempt = 0; attempt < 500; attempt++) {
            await client.PollAsync(cancellationToken);
            if (receipt.IsCompleted)
                return await receipt;
            await Task.Delay(20, cancellationToken);
        }
        return await receipt.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    private static async Task<Command> WaitForRecoveredCommandWhilePollingAsync(
        AuthoritativeClient client,
        AckLossRelay relay,
        CancellationToken cancellationToken)
    {
        var command = relay.WaitForRecoveredCommandAsync(cancellationToken);
        for (var attempt = 0; attempt < 500; attempt++) {
            await client.PollAsync(cancellationToken);
            if (command.IsCompleted)
                return await command;
            await Task.Delay(20, cancellationToken);
        }
        return await command.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    private sealed class MessageBacklog
    {
        public List<AuthoritativeClientMessage> Messages { get; } = [];
    }

    private static async Task<PersistedReceipt> WaitForPersistedReceiptAsync(
        string saveRoot,
        string resumeToken,
        uint entityId,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(saveRoot, "authoritative-cohort.json");
        for (var attempt = 0; attempt < 500; attempt++) {
            if (File.Exists(path)) {
                try {
                    await using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        options: FileOptions.SequentialScan);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    var expectedSessionId = SessionIdForToken(resumeToken);
                    foreach (var participant in document.RootElement
                        .GetProperty("Participants")
                        .EnumerateArray()) {
                        if (!string.Equals(
                                participant.GetProperty("SessionId").GetString(),
                                expectedSessionId,
                                StringComparison.Ordinal)
                            || participant.GetProperty("EntityId").GetUInt32() != entityId)
                            continue;
                        foreach (var receipt in participant
                            .GetProperty("CommandReceipts")
                            .EnumerateArray()) {
                            if (receipt.GetProperty("ClientSequence").GetUInt64() != sequence)
                                continue;
                            return new PersistedReceipt(
                                receipt.GetProperty("ServerReceiptSequence").GetUInt64(),
                                receipt.GetProperty("AppliedTick").GetUInt64());
                        }
                    }
                } catch (JsonException) {
                } catch (IOException) {
                }
            }
            await Task.Delay(20, cancellationToken);
        }
        throw new TimeoutException("The committed command receipt did not reach the durable cohort checkpoint.");
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500; attempt++) {
            if (predicate())
                return;
            await Task.Delay(20, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the production transport state.");
    }

    private static string SessionIdForToken(string resumeToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();

    private static void DeleteFixtureRootIfOwned(string saveRoot)
    {
        var fullRoot = Path.GetFullPath(saveRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(fullRoot)?.FullName
            ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(fullRoot);
        if (!Directory.Exists(fullRoot)
            || !string.Equals(parent, tempRoot, StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith("Godevilution-Godot-Cohort-Pending-Recovery-", StringComparison.Ordinal)
            || !Guid.TryParseExact(name["Godevilution-Godot-Cohort-Pending-Recovery-".Length..], "N", out _)
            || (File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
            return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(
            fullRoot,
            "*",
            SearchOption.AllDirectories)) {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                return;
        }
        Directory.Delete(fullRoot, recursive: true);
    }

    private readonly record struct PersistedReceipt(
        ulong ServerReceiptSequence,
        ulong AppliedTick);

    private sealed class AckLossRelay : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new();
        private readonly int upstreamPort;
        private readonly Task worker;
        private readonly object synchronization = new();
        private readonly TaskCompletionSource<Command> originalCommand =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<CommandResult> withheldReceipt =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Command> recoveredCommand =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? failure;
        private byte[]? originalCommandBytes;
        private int connectionCount;
        private int recoveredTransmissionCount;
        private int ownedHostLoss;

        public AckLossRelay(int upstreamPort)
        {
            this.upstreamPort = upstreamPort;
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            worker = RunAsync();
        }

        public int Port { get; }

        public int RecoveredTransmissionCount
            => Volatile.Read(ref recoveredTransmissionCount);

        public void ExpectOwnedHostLoss()
        {
            VerifyHealthy();
            if (!withheldReceipt.Task.IsCompletedSuccessfully)
                throw new InvalidOperationException("Host loss must follow the original withheld receipt.");
            Volatile.Write(ref ownedHostLoss, 1);
        }

        public async Task<Command> WaitForOriginalCommandAsync(CancellationToken cancellationToken)
            => await originalCommand.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        public async Task<CommandResult> WaitForWithheldReceiptAsync(CancellationToken cancellationToken)
            => await withheldReceipt.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        public async Task<Command> WaitForRecoveredCommandAsync(CancellationToken cancellationToken)
            => await recoveredCommand.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        public void VerifyHealthy()
        {
            lock (synchronization) {
                if (failure is not null)
                    throw new InvalidOperationException("The production ACK-loss relay failed.", failure);
            }
        }

        private async Task RunAsync()
        {
            try {
                while (!lifetime.IsCancellationRequested) {
                    using var downstream = await listener.AcceptTcpClientAsync(lifetime.Token);
                    downstream.NoDelay = true;
                    using var upstream = new TcpClient { NoDelay = true };
                    try {
                        await upstream.ConnectAsync(
                            IPAddress.Loopback,
                            upstreamPort,
                            lifetime.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    } catch (SocketException) when (Volatile.Read(ref connectionCount) > 0) {
                        continue;
                    }

                    var firstConnection = Interlocked.Increment(ref connectionCount) == 1;
                    using var connection = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    var send = ForwardAsync(
                        downstream.GetStream(),
                        upstream.GetStream(),
                        responses: false,
                        firstConnection,
                        connection.Token);
                    var receive = ForwardAsync(
                        upstream.GetStream(),
                        downstream.GetStream(),
                        responses: true,
                        firstConnection,
                        connection.Token);
                    await Task.WhenAny(send, receive);
                    connection.Cancel();
                    downstream.Close();
                    upstream.Close();
                    await Task.WhenAll(send, receive);
                }
            } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
            } catch (Exception) when (lifetime.IsCancellationRequested) {
            } catch (Exception exception) {
                lock (synchronization)
                    failure = exception;
                withheldReceipt.TrySetException(exception);
                recoveredCommand.TrySetException(exception);
            }
        }

        private async Task ForwardAsync(
            NetworkStream from,
            NetworkStream to,
            bool responses,
            bool firstConnection,
            CancellationToken cancellationToken)
        {
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    var header = new byte[sizeof(int)];
                    // Owned host termination may end or truncate its final
                    // frame, but only after the committed receipt is retained.
                    if (!await ReadExactlyAsync(from, header, cancellationToken))
                        return;
                    var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length <= 0 || length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
                        throw new InvalidDataException("The production relay received an invalid frame size.");
                    var payload = new byte[length];
                    if (!await ReadExactlyAsync(from, payload, cancellationToken))
                        throw new EndOfStreamException("The production relay received a truncated frame.");
                    var envelope = Envelope.Parser.ParseFrom(payload);
                    if (!responses && envelope.PayloadCase == Envelope.PayloadOneofCase.CommandBatch)
                        ObserveCommands(envelope.CommandBatch, firstConnection);

                    if (responses && envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck) {
                        var retained = new List<CommandResult>();
                        foreach (var result in envelope.CommandAck.Results) {
                            if (result.ClientSequence == OriginalSequence()) {
                                if (firstConnection) {
                                    withheldReceipt.TrySetResult(result.Clone());
                                    continue;
                                }
                            }
                            retained.Add(result);
                        }
                        envelope.CommandAck.Results.Clear();
                        envelope.CommandAck.Results.AddRange(retained);
                        if (retained.Count == 0)
                            continue;
                        payload = envelope.ToByteArray();
                        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
                    }

                    await to.WriteAsync(header, cancellationToken);
                    await to.WriteAsync(payload, cancellationToken);
                    await to.FlushAsync(cancellationToken);
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            } catch (IOException) when (cancellationToken.IsCancellationRequested) {
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
            } catch (EndOfStreamException) when (firstConnection
                && Volatile.Read(ref ownedHostLoss) != 0
                && withheldReceipt.Task.IsCompletedSuccessfully) {
            } catch (IOException exception) when (firstConnection
                && Volatile.Read(ref ownedHostLoss) != 0
                && withheldReceipt.Task.IsCompletedSuccessfully
                && exception.InnerException is SocketException {
                    SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted }) {
                // Only the deliberate original-host loss is an expected reset.
            }
        }

        private void ObserveCommands(CommandBatch batch, bool firstConnection)
        {
            foreach (var command in batch.Commands) {
                if (command.ClientSequence == 0)
                    continue;
                lock (synchronization) {
                    if (originalCommandBytes is null) {
                        if (!firstConnection)
                            throw new InvalidDataException("The pending command was not sent before host loss.");
                        originalCommandBytes = command.ToByteArray();
                        originalCommand.TrySetResult(command.Clone());
                    } else {
                        var original = Command.Parser.ParseFrom(originalCommandBytes);
                        if (command.ClientSequence != original.ClientSequence)
                            continue;
                        if (!originalCommandBytes.AsSpan().SequenceEqual(command.ToByteArray())) {
                            throw new InvalidDataException("Reconnect changed the pending command identity.");
                        }
                    }
                }
                if (!firstConnection) {
                    recoveredCommand.TrySetResult(command.Clone());
                    Interlocked.Increment(ref recoveredTransmissionCount);
                }
            }
        }

        private ulong OriginalSequence()
        {
            lock (synchronization) {
                if (originalCommandBytes is null)
                    return 0;
                return Command.Parser.ParseFrom(originalCommandBytes).ClientSequence;
            }
        }

        private static async Task<bool> ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length) {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
                if (read == 0) {
                    if (offset == 0)
                        return false;
                    throw new EndOfStreamException("The production relay received a truncated frame.");
                }
                offset += read;
            }
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel();
            listener.Stop();
            try {
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
            } finally {
                lifetime.Dispose();
            }
            VerifyHealthy();
        }
    }

    private sealed class ProductionServer : IDisposable
    {
        private readonly Process process;
        private readonly CancellationTokenSource drainLifetime;
        private readonly Task stdoutDrain;
        private readonly Task<string> stderrDrain;
        private bool disposed;

        private ProductionServer(
            Process process,
            int port,
            string contentHash,
            string rulesetHash,
            CancellationTokenSource drainLifetime,
            Task stdoutDrain,
            Task<string> stderrDrain)
        {
            this.process = process;
            this.drainLifetime = drainLifetime;
            this.stdoutDrain = stdoutDrain;
            this.stderrDrain = stderrDrain;
            Port = port;
            ContentHash = contentHash;
            RulesetHash = rulesetHash;
        }

        public int Port { get; }

        public string ContentHash { get; }

        public string RulesetHash { get; }

        public static async Task<ProductionServer> StartAsync(
            string saveRoot,
            int? portOverride,
            bool includeSeed,
            CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(Path.Combine(
                repositoryRoot,
                "server/src/Devilution.Server/Devilution.Server.csproj"));
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(ResolveBuildConfiguration());
            startInfo.ArgumentList.Add("--no-build");
            var artifactsPath = Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH");
            if (!string.IsNullOrWhiteSpace(artifactsPath)) {
                startInfo.ArgumentList.Add("--artifacts-path");
                startInfo.ArgumentList.Add(Path.GetFullPath(artifactsPath));
            }
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--bind");
            startInfo.ArgumentList.Add("127.0.0.1");
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add((portOverride ?? 0).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            startInfo.ArgumentList.Add("--persistence-mode");
            startInfo.ArgumentList.Add("cohort");
            startInfo.ArgumentList.Add("--game-mode");
            startInfo.ArgumentList.Add("multiplayer");
            startInfo.ArgumentList.Add("--command-scheduling");
            startInfo.ArgumentList.Add("clock-gated");
            if (includeSeed) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add("17");
            }

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the production authoritative server.");
            var drainLifetime = new CancellationTokenSource();
            var stderrDrain = process.StandardError.ReadToEndAsync(drainLifetime.Token);
            Task stdoutDrain = Task.CompletedTask;
            try {
                using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                startupTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                int? port = null;
                string? contentHash = null;
                string? rulesetHash = null;
                while (await process.StandardOutput.ReadLineAsync(startupTimeout.Token) is { } line) {
                    if (line.Contains("listening on", StringComparison.Ordinal)
                        && int.TryParse(
                            line[(line.LastIndexOf(':') + 1)..],
                            out var parsedPort))
                        port = parsedPort;
                    if (line.StartsWith("Content manifest: ", StringComparison.Ordinal))
                        contentHash = line["Content manifest: ".Length..];
                    if (line.StartsWith("Ruleset identity: ", StringComparison.Ordinal))
                        rulesetHash = line["Ruleset identity: ".Length..];
                    if (port.HasValue && contentHash is not null && rulesetHash is not null) {
                        stdoutDrain = DrainStandardOutputAsync(
                            process.StandardOutput,
                            drainLifetime.Token);
                        return new ProductionServer(
                            process,
                            port.Value,
                            contentHash,
                            rulesetHash,
                            drainLifetime,
                            stdoutDrain,
                            stderrDrain);
                    }
                }
                var error = await stderrDrain.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    startupTimeout.Token);
                throw new InvalidOperationException(
                    $"The production server exited before publishing its identity: {error}");
            } catch (Exception startupException) {
                if (!TryTerminate(process)) {
                    drainLifetime.Cancel();
                    throw new InvalidOperationException(
                        "The production server could not be terminated after startup failure.",
                        startupException);
                }
                drainLifetime.Cancel();
                try {
                    await Task.WhenAll(stdoutDrain, stderrDrain)
                        .WaitAsync(TimeSpan.FromSeconds(5));
                } catch (Exception drainException) {
                    startupException.Data["StartupDrainFailure"] = drainException;
                }
                drainLifetime.Dispose();
                process.Dispose();
                throw new InvalidOperationException(
                    "The production server exited before publishing its identity.",
                    startupException);
            }
        }

        private static async Task DrainStandardOutputAsync(
            StreamReader output,
            CancellationToken cancellationToken)
        {
            try {
                while (await output.ReadLineAsync(cancellationToken) is not null) {
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            } catch (IOException) when (cancellationToken.IsCancellationRequested) {
            }
        }

        private static bool TryTerminate(Process process)
        {
            try {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                if (process.WaitForExit(10000))
                    return true;
                try {
                    process.Kill(entireProcessTree: true);
                } catch (InvalidOperationException) {
                }
                return process.WaitForExit(1000);
            } catch (InvalidOperationException) {
                return process.HasExited;
            }
        }

        public void Kill()
        {
            if (process.HasExited)
                return;
            process.Kill(entireProcessTree: true);
        }

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            try {
                await Task.WhenAll(stdoutDrain, stderrDrain)
                    .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            } catch {
                drainLifetime.Cancel();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            Exception? terminationFailure = null;
            if (!TryTerminate(process)) {
                terminationFailure = new InvalidOperationException(
                    "The production server did not terminate within the bounded cleanup deadline.");
            }
            Exception? drainFailure = null;
            try {
                if (!Task.WhenAll(stdoutDrain, stderrDrain)
                        .Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException(
                        "The production server output drain did not complete within the bounded cleanup deadline.");
            } catch (Exception exception) {
                drainFailure = exception;
                drainLifetime.Cancel();
            }
            if (terminationFailure is not null) {
                if (drainFailure is not null)
                    terminationFailure.Data["DrainFailure"] = drainFailure;
                throw terminationFailure;
            }
            drainLifetime.Dispose();
            process.Dispose();
            disposed = true;
            if (drainFailure is not null)
                throw new InvalidOperationException(
                    "The production server output drain did not complete.",
                    drainFailure);
        }

        private static string FindRepositoryRoot()
        {
            var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
            var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
                ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
                : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (var start in searchRoots) {
                for (var directory = new DirectoryInfo(start);
                    directory is not null;
                    directory = directory.Parent) {
                    if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                        return directory.FullName;
                }
            }
            throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
        }

        private static string ResolveBuildConfiguration()
        {
            return typeof(CohortPendingCommandRecoveryTests).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                ?? throw new XunitException("The executing test assembly has no build configuration.");
        }
    }
}
