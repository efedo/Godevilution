using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/**
 * Bounded production-host coverage for the committed-but-unobserved command
 * boundary.  The participant writes a real TCP command and deliberately does
 * not read its response; the canonical cohort document, rather than delivery
 * assumptions, proves whether the command committed before host loss.
 */
public sealed class CohortCliPendingCommandRecoveryTests
{
    private const uint FixedWorldSeed = 2588;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    [Fact]
    public async Task ProductionCliRecoversCommittedButUnobservedCommandExactlyOnce()
    {
        var root = NewFixtureRoot();
        PendingCliHost? initial = null;
        PendingCliHost? resumed = null;
        PendingParticipant? pending = null;
        PendingParticipant? recovered = null;
        Exception? primary = null;
        try {
            initial = await PendingCliHost.StartAsync(
                root,
                FixedWorldSeed,
                TestContext.Current.CancellationToken);
            Assert.Contains("Persistence: complete cohort", initial.StartupLines);
            Assert.Contains("Command scheduling: clock-gated", initial.StartupLines);

            pending = await PendingParticipant.ConnectAsync(
                initial,
                "pending-cli",
                string.Empty,
                TestContext.Current.CancellationToken);
            var resumeToken = pending.SessionToken;
            var sessionId = pending.SessionId;
            var ownerEntityId = pending.EntityId;
            // Handshake snapshots can already be stale while the host is
            // publishing startup/participant state.  Correlate command
            // admission with an explicit fresh snapshot immediately before
            // choosing the item, target, and future requested tick.
            var freshSnapshot = await pending.RequestSnapshotAsync(
                TestContext.Current.CancellationToken);
            var commandSnapshot = freshSnapshot.Snapshot;
            Assert.NotEqual(0UL, freshSnapshot.TimingRequestId);
            Assert.True(freshSnapshot.TimingReplySnapshotTick <= commandSnapshot.Tick);
            var starterItem = Assert.Single(
                commandSnapshot.Players.Single(player => player.CharacterName == "pending-cli")
                    .Inventory
                    .Select((item, slot) => (item, slot)),
                candidate => candidate.item.State is not null
                    && candidate.item.State.PresentationKind != ItemPresentationKind.Gold
                    && candidate.item.State.StableItemId != 0);
            var target = FindDropTarget(commandSnapshot, ownerEntityId);
            var futureLeadTicks = ComputeFutureLeadTicks(
                freshSnapshot.ProbeRoundTrip,
                pending.TickRateHz);
            var requestedTick = checked(commandSnapshot.Tick + futureLeadTicks);
            var command = new Command {
                ClientSequence = 1,
                RequestedTick = requestedTick,
                DropItemRequested = new DropItemRequested {
                    Item = new PlayerItemReference {
                        Location = PlayerItemLocation.Inventory,
                        Slot = checked((uint)starterItem.slot),
                        StableItemId = starterItem.item.State.StableItemId,
                    },
                    TargetX = target.X,
                    TargetY = target.Y,
                },
            };

            // The TCP write completes, but this participant intentionally never
            // reads the response.  This models an outcome unknown to the client
            // without claiming that the network response was lost.
            await pending.SendWithoutReadingAsync(
                command,
                TestContext.Current.CancellationToken);
            var committed = await WaitForCommittedDropAsync(
                root,
                sessionId,
                starterItem.item.State.StableItemId,
                target,
                freshSnapshot.TimingRequestId,
                freshSnapshot.TimingReplySnapshotTick,
                commandSnapshot.Tick,
                requestedTick,
                freshSnapshot.ProbeRoundTrip,
                pending.TickRateHz,
                futureLeadTicks,
                TestContext.Current.CancellationToken);
            Assert.Equal(1UL, committed.Receipt.ClientSequence);
            Assert.Equal(1UL, committed.Receipt.ServerReceiptSequence);
            Assert.Equal(CommandStatus.Accepted, committed.Receipt.Status);
            Assert.Equal(CommandRejectReason.Unspecified, committed.Receipt.RejectReason);
            Assert.True(
                committed.Live.Tick >= requestedTick,
                "The canonical checkpoint must cross the command's future scheduling boundary.");
            Assert.Equal(ownerEntityId, committed.Module.Participants.Single(
                participant => participant.SessionId == sessionId).EntityId);

            // The receipt and world-item effect are both durable before the
            // process is killed, proving committed-but-unobserved rather than
            // inferring commitment from the unread response.
            await initial.AbruptStopAsync(TestContext.Current.CancellationToken);
            await pending.DisposeAsync();
            pending = null;

            // Only after the host releases its lease may the test use the
            // production store reader.  This validates the exact canonical
            // document, including its integrity hash, at the stop boundary.
            var committedDocument = LoadStoppedCanonicalDocument(root);
            Assert.True(committedDocument.Generation >= committed.Live.Generation);
            Assert.True(committedDocument.Tick >= committed.Live.Tick);
            Assert.Equal(2UL, committedDocument.NextServerReceiptSequence);
            var committedReceipt = committedDocument.Participants
                .Single(participant => participant.SessionId == sessionId)
                .CommandReceipts.Single(receipt => receipt.ClientSequence == 1);
            Assert.Equal(CommandStatus.Accepted, committedReceipt.Status);
            Assert.Equal(CommandRejectReason.Unspecified, committedReceipt.RejectReason);
            Assert.Equal(committed.Receipt.ClientSequence, committedReceipt.ClientSequence);
            Assert.Equal(committed.Receipt.ServerReceiptSequence,
                committedReceipt.ServerReceiptSequence);
            Assert.Equal(committed.Receipt.AppliedTick, committedReceipt.AppliedTick);
            var committedModule = new DiabloCohortCodec().Deserialize(
                committedDocument.SerializedModuleState);
            Assert.Equal(committedDocument.Tick, committedModule.Tick);
            var committedItem = Assert.Single(
                committedModule.WorldItems,
                item => item.State.StableItemId == starterItem.item.State.StableItemId);
            committed = new CommittedDrop(
                committedDocument,
                committed.Live,
                committedModule,
                committedReceipt,
                committedItem);

            resumed = await PendingCliHost.StartAsync(
                root,
                null,
                TestContext.Current.CancellationToken);
            recovered = await PendingParticipant.ConnectAsync(
                resumed,
                "pending-cli",
                resumeToken,
                TestContext.Current.CancellationToken);
            Assert.Equal(ownerEntityId, recovered.EntityId);
            Assert.Equal(1UL, recovered.LastClientSequence);
            Assert.True(
                recovered.Snapshot.Tick >= committed.Document.Tick,
                "The fresh host must resume from the durable canonical tick.");
            var restoredWorldItem = Assert.Single(
                recovered.Snapshot.WorldItems,
                item => item.State is not null
                    && item.State.StableItemId == starterItem.item.State.StableItemId);
            Assert.Equal(committed.Item.EntityId, restoredWorldItem.EntityId);
            Assert.Equal(committed.Item.ItemSeed, restoredWorldItem.ItemSeed);
            Assert.Equal(target.X, restoredWorldItem.PositionX);
            Assert.Equal(target.Y, restoredWorldItem.PositionY);

            var duplicate = await recovered.SendAndReadAsync(
                command,
                TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Duplicate, duplicate.Result.Status);
            Assert.Equal(CommandRejectReason.Unspecified, duplicate.Result.RejectReason);
            Assert.Equal(command.ClientSequence, duplicate.Result.ClientSequence);
            Assert.Equal(committed.Receipt.ServerReceiptSequence, duplicate.Result.ServerReceiptSequence);
            Assert.Equal(committed.Receipt.AppliedTick, duplicate.Result.AppliedTick);

            var followUpFresh = await recovered.RequestSnapshotAsync(
                TestContext.Current.CancellationToken);
            Assert.NotEqual(0UL, followUpFresh.TimingRequestId);
            Assert.True(
                followUpFresh.TimingReplySnapshotTick <= followUpFresh.Snapshot.Tick);
            var nextOwner = followUpFresh.Snapshot.Players.Single(
                player => player.EntityId == ownerEntityId);
            var nextTarget = FindDropTarget(followUpFresh.Snapshot, ownerEntityId);
            var followUpLeadTicks = ComputeFutureLeadTicks(
                followUpFresh.ProbeRoundTrip,
                recovered.TickRateHz);
            var followUp = new Command {
                ClientSequence = 2,
                RequestedTick = checked(followUpFresh.Snapshot.Tick + followUpLeadTicks),
                MoveRequested = new MoveRequested {
                    DirectionX = nextTarget.X - nextOwner.PositionX,
                    DirectionY = nextTarget.Y - nextOwner.PositionY,
                },
            };
            var acceptedNext = await recovered.SendAndReadAsync(
                followUp,
                TestContext.Current.CancellationToken,
                AdmissionDiagnostics(
                    followUpFresh,
                    recovered.TickRateHz,
                    followUpLeadTicks));
            Assert.Equal(CommandStatus.Accepted, acceptedNext.Result.Status);
            Assert.Equal(CommandRejectReason.Unspecified, acceptedNext.Result.RejectReason);
            Assert.Equal(2UL, acceptedNext.Result.ClientSequence);
            Assert.Equal(
                committed.Receipt.ServerReceiptSequence + 1,
                acceptedNext.Result.ServerReceiptSequence);
            var nextObserved = await WaitForLiveReceiptAsync(
                root,
                sessionId,
                2,
                receipt => receipt.Status == CommandStatus.Accepted
                    && receipt.RejectReason == CommandRejectReason.Unspecified,
                TestContext.Current.CancellationToken);
            Assert.Equal(acceptedNext.Result.AppliedTick,
                nextObserved.Participants.Single(participant => participant.SessionId == sessionId)
                    .CommandReceipts.Single(receipt => receipt.ClientSequence == 2).AppliedTick);

            await resumed.AbruptStopAsync(TestContext.Current.CancellationToken);
            await recovered.DisposeAsync();
            recovered = null;
            var terminalDocument = LoadStoppedCanonicalDocument(root);
            var terminalModule = new DiabloCohortCodec().Deserialize(
                terminalDocument.SerializedModuleState);
            var terminalParticipant = Assert.Single(
                terminalDocument.Participants,
                participant => participant.SessionId == sessionId);
            Assert.Equal(2, terminalParticipant.CommandReceipts.Count);
            Assert.Equal(1UL, terminalParticipant.CommandReceipts[0].ClientSequence);
            Assert.Equal(
                committed.Receipt.ServerReceiptSequence,
                terminalParticipant.CommandReceipts[0].ServerReceiptSequence);
            Assert.Equal(
                committed.Receipt.AppliedTick,
                terminalParticipant.CommandReceipts[0].AppliedTick);
            Assert.Equal(CommandStatus.Accepted, terminalParticipant.CommandReceipts[0].Status);
            Assert.Equal(CommandRejectReason.Unspecified, terminalParticipant.CommandReceipts[0].RejectReason);
            Assert.Equal(2UL, terminalParticipant.CommandReceipts[1].ClientSequence);
            Assert.Equal(acceptedNext.Result.ServerReceiptSequence,
                terminalParticipant.CommandReceipts[1].ServerReceiptSequence);
            Assert.Equal(CommandStatus.Accepted, terminalParticipant.CommandReceipts[1].Status);
            Assert.Equal(CommandRejectReason.Unspecified, terminalParticipant.CommandReceipts[1].RejectReason);
            Assert.Equal(3UL, terminalDocument.NextServerReceiptSequence);
            Assert.Equal(terminalDocument.Tick, terminalModule.Tick);
            Assert.Equal(ownerEntityId,
                terminalModule.Participants.Single(participant => participant.SessionId == sessionId).EntityId);
            Assert.Single(
                terminalModule.WorldItems,
                item => item.State.StableItemId == starterItem.item.State.StableItemId);
            Assert.DoesNotContain(
                terminalModule.Participants.Single(participant => participant.SessionId == sessionId)
                    .Player.Inventory,
                item => item.State is not null
                    && item.State.StableItemId == starterItem.item.State.StableItemId);
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            var cleanupFailure = primary;
            cleanupFailure = await DisposeParticipantAsync(recovered, cleanupFailure);
            cleanupFailure = await DisposeParticipantAsync(pending, cleanupFailure);
            cleanupFailure = await DisposeHostAsync(resumed, cleanupFailure);
            cleanupFailure = await DisposeHostAsync(initial, cleanupFailure);
            if (primary is null && cleanupFailure is null) {
                try {
                    DeleteFixtureRoot(root);
                } catch (Exception exception) {
                    RecordCleanupFailure(ref cleanupFailure, exception, "Fixture");
                }
            }
            if (primary is null && cleanupFailure is not null)
                ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }

    private static async Task<CommittedDrop> WaitForCommittedDropAsync(
        string root,
        string sessionId,
        uint stableItemId,
        (int X, int Y) target,
        ulong timingRequestId,
        ulong timingReplySnapshotTick,
        ulong snapshotTick,
        ulong requestedTick,
        TimeSpan probeRoundTrip,
        uint tickRateHz,
        ulong futureLeadTicks,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var codec = new DiabloCohortCodec();
        string? lastReadError = null;
        CommandResult? lastReceipt = null;
        while (true) {
            var live = TryReadCanonicalCheckpoint(root, out lastReadError);
            if (live is not null) {
                var participant = live.Participants.SingleOrDefault(
                    candidate => candidate.SessionId == sessionId);
                var receipt = participant?.CommandReceipts.SingleOrDefault(
                    candidate => candidate.ClientSequence == 1);
                if (participant is not null && receipt is not null) {
                    lastReceipt = receipt;
                    if (receipt.Status != CommandStatus.Accepted
                        || receipt.RejectReason != CommandRejectReason.Unspecified) {
                        throw new XunitException(
                            "Pending command admission was not accepted: "
                            + $"timing request {timingRequestId}, "
                            + $"timing reply snapshot tick {timingReplySnapshotTick}, "
                            + $"command snapshot tick {snapshotTick}, requested tick {requestedTick}, "
                            + $"probe RTT {probeRoundTrip.TotalMilliseconds:F3} ms, "
                            + $"tick rate {tickRateHz}, future lead {futureLeadTicks}, "
                            + $"receipt status {receipt.Status}, reason {receipt.RejectReason}, "
                            + $"applied tick {receipt.AppliedTick}.");
                    }
                    try {
                        var module = codec.Deserialize(live.SerializedModuleState);
                        var matchingItems = module.WorldItems
                            .Where(item => item.State.StableItemId == stableItemId)
                            .ToArray();
                        var owner = module.Participants.SingleOrDefault(
                            candidate => candidate.SessionId == sessionId);
                        var inventoryStillOwnsItem = owner?.Player.Inventory.Any(
                            item => item.State is not null
                                && item.State.StableItemId == stableItemId) == true;
                        if (owner is not null
                            && matchingItems.Length == 1
                            && matchingItems[0].PositionX == target.X
                            && matchingItems[0].PositionY == target.Y
                            && !inventoryStillOwnsItem) {
                            return new CommittedDrop(
                                null!,
                                live,
                                module,
                                receipt,
                                matchingItems[0]);
                        }
                    } catch (InvalidDataException exception) {
                        lastReadError = $"Module decode InvalidDataException: {exception.Message}";
                    } catch (JsonException exception) {
                        lastReadError = $"Module decode JsonException: {exception.Message}";
                    } catch (InvalidOperationException exception) {
                        lastReadError = $"Module decode InvalidOperationException: {exception.Message}";
                    }
                }
            }
            try {
                await Task.Delay(25, timeout.Token);
            } catch (OperationCanceledException) when (
                timeout.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested) {
                throw new XunitException(
                    "Timed out waiting for the committed command in the canonical checkpoint. "
                    + $"timing request {timingRequestId}, "
                    + $"timing reply snapshot tick {timingReplySnapshotTick}, "
                    + $"command snapshot tick {snapshotTick}, requested tick {requestedTick}, "
                    + $"probe RTT {probeRoundTrip.TotalMilliseconds:F3} ms, "
                    + $"tick rate {tickRateHz}, future lead {futureLeadTicks}, "
                    + (lastReceipt is null
                        ? "no receipt observed. "
                        : $"last receipt status {lastReceipt.Status}, "
                            + $"reason {lastReceipt.RejectReason}, "
                            + $"applied tick {lastReceipt.AppliedTick}. ")
                    + $"Last canonical read error: {lastReadError ?? "none"}.");
            }
        }
    }

    private static async Task<LiveCanonicalCheckpoint> WaitForLiveReceiptAsync(
        string root,
        string sessionId,
        ulong clientSequence,
        Func<CommandResult, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string? lastReadError = null;
        while (true) {
            var live = TryReadCanonicalCheckpoint(root, out lastReadError);
            var receipt = live?.Participants
                .SingleOrDefault(participant => participant.SessionId == sessionId)
                ?.CommandReceipts.SingleOrDefault(
                    candidate => candidate.ClientSequence == clientSequence);
            if (live is not null && receipt is not null && predicate(receipt))
                return live;
            try {
                await Task.Delay(25, timeout.Token);
            } catch (OperationCanceledException) when (
                timeout.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested) {
                throw new XunitException(
                    $"Timed out waiting for canonical receipt {clientSequence}. "
                    + $"Last canonical read error: {lastReadError ?? "none"}.");
            }
        }
    }

    private static AuthoritativeCohortDocument LoadStoppedCanonicalDocument(string root)
    {
        using var store = new AuthoritativeCohortStore(root);
        return store.Load()
            ?? throw new XunitException("The stopped production host left no canonical cohort document.");
    }

    /** Reads a published checkpoint without opening the host-owned exclusive lease. */
    private static LiveCanonicalCheckpoint? TryReadCanonicalCheckpoint(
        string root,
        out string? error)
    {
        error = null;
        var path = Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName);
        try {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var rootElement = document.RootElement;
            var participants = rootElement
                .GetProperty("Participants")
                .EnumerateArray()
                .Select(participant => new LiveCanonicalParticipant(
                    participant.GetProperty("SessionId").GetString()
                        ?? throw new JsonException("A cohort participant has no session ID."),
                    participant.GetProperty("EntityId").GetUInt32(),
                    participant
                        .GetProperty("CommandReceipts")
                        .EnumerateArray()
                        .Select(receipt => new CommandResult {
                            ClientSequence = receipt.GetProperty("ClientSequence").GetUInt64(),
                            ServerReceiptSequence = receipt.GetProperty("ServerReceiptSequence").GetUInt64(),
                            AppliedTick = receipt.GetProperty("AppliedTick").GetUInt64(),
                            Status = (CommandStatus)receipt.GetProperty("Status").GetInt32(),
                            RejectReason = (CommandRejectReason)receipt.GetProperty("RejectReason").GetInt32(),
                        })
                        .ToArray()))
                .ToArray();
            return new LiveCanonicalCheckpoint(
                rootElement.GetProperty("Generation").GetUInt64(),
                rootElement.GetProperty("Tick").GetUInt64(),
                rootElement.GetProperty("NextServerReceiptSequence").GetUInt64(),
                participants,
                DecodeCanonicalModuleState(
                    rootElement.GetProperty("SerializedModuleState").GetString()
                        ?? throw new JsonException("A cohort checkpoint has no module state.")));
        } catch (FileNotFoundException) {
            error = "Checkpoint has not been published yet.";
            return null;
        } catch (DirectoryNotFoundException) {
            error = "Cohort root is not available yet.";
            return null;
        } catch (IOException) {
            error = "I/O while reading canonical checkpoint.";
            return null;
        } catch (JsonException exception) {
            error = $"JsonException: {exception.Message}";
            return null;
        } catch (InvalidOperationException) {
            error = "InvalidOperationException while reading canonical checkpoint.";
            return null;
        } catch (KeyNotFoundException) {
            error = "KeyNotFoundException while reading canonical checkpoint.";
            return null;
        } catch (InvalidDataException exception) {
            error = $"InvalidDataException: {exception.Message}";
            return null;
        }
    }

    private static string DecodeCanonicalModuleState(string encoded)
    {
        byte[] bytes;
        try {
            bytes = Convert.FromBase64String(encoded);
        } catch (FormatException exception) {
            throw new InvalidDataException(
                "The canonical module state is not Base64.",
                exception);
        } catch (ArgumentNullException exception) {
            throw new InvalidDataException(
                "The canonical module state is not Base64.",
                exception);
        }
        if (!string.Equals(
                Convert.ToBase64String(bytes),
                encoded,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "The canonical module state is not canonical Base64.");
        try {
            return StrictUtf8.GetString(bytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException(
                "The canonical module state is not strict UTF-8.",
                exception);
        }
    }

    private static (int X, int Y) FindDropTarget(Snapshot snapshot, uint ownerEntityId)
    {
        var owner = snapshot.Players.Single(player => player.EntityId == ownerEntityId);
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == owner.LevelId);
        var blocked = geometry.BlockedCells.ToHashSet();
        var occupied = snapshot.Players
            .Select(player => Cell(player.PositionX, player.PositionY, geometry.Width))
            .Concat(snapshot.WorldItems
                .Where(item => item.LevelId == owner.LevelId)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .Concat(snapshot.Objects
                .Where(item => item.LevelId == owner.LevelId)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .Concat(snapshot.Monsters
                .Where(item => item.LevelId == owner.LevelId && item.Alive)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .ToHashSet();
        foreach (var (x, y) in new[] {
                     (owner.PositionX + 1, owner.PositionY),
                     (owner.PositionX, owner.PositionY + 1),
                     (owner.PositionX - 1, owner.PositionY),
                     (owner.PositionX, owner.PositionY - 1),
                 }) {
            if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height)
                continue;
            var cell = Cell(x, y, geometry.Width);
            if (!blocked.Contains(cell) && !occupied.Contains(cell))
                return (x, y);
        }
        throw new XunitException("The shipped base level has no adjacent valid drop target.");
    }

    private static int Cell(int x, int y, int width) => checked(y * width + x);

    private static string NewFixtureRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "Godev-Cohort-Pending-" + Guid.NewGuid().ToString("N"));

    private static void DeleteFixtureRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = new DirectoryInfo(fullRoot).Name;
        const string prefix = "Godev-Cohort-Pending-";
        if (!string.Equals(
                Path.GetDirectoryName(fullRoot)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !leaf.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(leaf[prefix.Length..], "N", out _)) {
            throw new InvalidOperationException(
                $"Refusing to delete an unowned pending cohort root: {fullRoot}");
        }
        if (!Directory.Exists(fullRoot))
            return;
        AssertNoFixtureReparsePoints(fullRoot);
        Directory.Delete(fullRoot, recursive: true);
    }

    private static void AssertNoFixtureReparsePoints(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0) {
            var current = pending.Pop();
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    $"Refusing to recurse through a reparse point: {current}");
            if ((attributes & FileAttributes.Directory) == 0)
                continue;
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
                pending.Push(child);
        }
    }

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var starts = string.IsNullOrWhiteSpace(configured)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configured!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string FindServerAssembly()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY");
        var configuration = typeof(CohortCliPendingCommandRecoveryTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new XunitException("The executing test assembly has no build configuration.");
        var assembly = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(FindRepositoryRoot(), "server", "src", "Devilution.Server", "bin",
                configuration, "net10.0", "Devilution.Server.dll")
            : Path.GetFullPath(configured);
        if (!File.Exists(assembly))
            throw new XunitException($"GODEVILUTION_SERVER_ASSEMBLY does not exist: {assembly}");
        return assembly;
    }

    private static async Task<Exception?> DisposeParticipantAsync(
        PendingParticipant? participant,
        Exception? failure)
    {
        if (participant is null)
            return failure;
        try {
            await participant.DisposeAsync();
        } catch (Exception exception) {
            RecordCleanupFailure(ref failure, exception, "Participant");
        }
        return failure;
    }

    private static async Task<Exception?> DisposeHostAsync(
        PendingCliHost? host,
        Exception? failure)
    {
        if (host is null)
            return failure;
        try {
            await host.DisposeAsync();
        } catch (Exception exception) {
            RecordCleanupFailure(ref failure, exception, "Host");
        }
        return failure;
    }

    private static void RecordCleanupFailure(
        ref Exception? failure,
        Exception secondary,
        string stage)
    {
        if (failure is null)
            failure = secondary;
        else
            failure.Data["PendingCliCleanup-" + stage] = secondary.Message;
    }

    private static ulong ComputeFutureLeadTicks(
        TimeSpan probeRoundTrip,
        uint tickRateHz)
    {
        Assert.True(tickRateHz > 0, "The production handshake must negotiate a tick rate.");
        Assert.True(
            probeRoundTrip >= TimeSpan.Zero
                && probeRoundTrip.TotalSeconds < ulong.MaxValue / (double)tickRateHz,
            "The measured timing probe duration must be finite and bounded.");
        var measuredTicks = probeRoundTrip.TotalSeconds * tickRateHz;
        Assert.True(
            double.IsFinite(measuredTicks),
            "The measured timing probe duration must produce a finite tick count.");
        return checked((ulong)Math.Ceiling(measuredTicks) + 1);
    }

    private static string AdmissionDiagnostics(
        FreshSnapshotObservation observation,
        uint tickRateHz,
        ulong futureLeadTicks)
        => $"timing request {observation.TimingRequestId}, "
            + $"timing reply snapshot tick {observation.TimingReplySnapshotTick}, "
            + $"command snapshot tick {observation.Snapshot.Tick}, "
            + $"probe RTT {observation.ProbeRoundTrip.TotalMilliseconds:F3} ms, "
            + $"tick rate {tickRateHz}, future lead {futureLeadTicks}";

    private sealed record CommittedDrop(
        AuthoritativeCohortDocument Document,
        LiveCanonicalCheckpoint Live,
        DiabloCohortState Module,
        CommandResult Receipt,
        DiabloWorldItemState Item);

    private sealed record FreshSnapshotObservation(
        Snapshot Snapshot,
        ulong TimingRequestId,
        ulong TimingReplySnapshotTick,
        TimeSpan ProbeRoundTrip);

    private sealed record LiveCanonicalCheckpoint(
        ulong Generation,
        ulong Tick,
        ulong NextServerReceiptSequence,
        IReadOnlyList<LiveCanonicalParticipant> Participants,
        string SerializedModuleState);

    private sealed record LiveCanonicalParticipant(
        string SessionId,
        uint EntityId,
        IReadOnlyList<CommandResult> CommandReceipts);

    private sealed class PendingCliHost : IAsyncDisposable
    {
        private readonly Task<string> outputTail;
        private readonly Task<string> errorTail;
        private bool stopped;
        private bool disposed;

        private PendingCliHost(
            Process process,
            string[] startupLines,
            int port,
            string contentManifestHash,
            string rulesetIdentityHash,
            string protocolSchemaVersion,
            Task<string> outputTail,
            Task<string> errorTail)
        {
            Process = process;
            StartupLines = startupLines;
            Port = port;
            ContentManifestHash = contentManifestHash;
            RulesetIdentityHash = rulesetIdentityHash;
            ProtocolSchemaVersion = protocolSchemaVersion;
            this.outputTail = outputTail;
            this.errorTail = errorTail;
        }

        public Process Process { get; }
        public IReadOnlyList<string> StartupLines { get; }
        public int Port { get; }
        public string ContentManifestHash { get; }
        public string RulesetIdentityHash { get; }
        public string ProtocolSchemaVersion { get; }

        public static async Task<PendingCliHost> StartAsync(
            string saveRoot,
            uint? randomSeed,
            CancellationToken cancellationToken)
        {
            var process = StartProcess(saveRoot, randomSeed);
            Task<string>? errorTail = null;
            Task<string>? outputTail = null;
            try {
                errorTail = process.StandardError.ReadToEndAsync();
                var lines = new List<string>();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                while (true) {
                    var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                    if (line is null)
                        throw new XunitException(
                            $"Production CLI exited before startup (exit {process.ExitCode}): "
                            + $"{await errorTail.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token)}");
                    lines.Add(line);
                    if (line.StartsWith("Operator commands:", StringComparison.Ordinal))
                        break;
                }
                var port = ParsePort(lines);
                var content = ParseRequiredLine(lines, "Content manifest:");
                var ruleset = ParseRequiredLine(lines, "Ruleset identity:");
                var protocol = ParseRequiredLine(lines, "Protocol schema:");
                Assert.Equal(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity, protocol);
                Assert.Contains("Game mode: Multiplayer", lines);
                outputTail = process.StandardOutput.ReadToEndAsync();
                return new PendingCliHost(
                    process,
                    lines.ToArray(),
                    port,
                    content,
                    ruleset,
                    protocol,
                    outputTail,
                    errorTail);
            } catch (Exception primary) {
                await TerminateAndDisposeAsync(process, outputTail, errorTail, primary);
                throw;
            }
        }

        public async Task AbruptStopAsync(CancellationToken cancellationToken)
        {
            if (stopped)
                return;
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await DrainAsync(cancellationToken);
            stopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
                return;
            disposed = true;
            await TerminateAndDisposeAsync(Process, outputTail, errorTail);
        }

        private async Task DrainAsync(CancellationToken cancellationToken)
        {
            await outputTail.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await errorTail.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }

        private static Process StartProcess(string saveRoot, uint? randomSeed)
        {
            var repository = FindRepositoryRoot();
            var contentRoot = Path.Combine(repository, "server", "content", "base");
            var startInfo = new ProcessStartInfo {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = repository,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(FindServerAssembly());
            startInfo.ArgumentList.Add("--content-root");
            startInfo.ArgumentList.Add(contentRoot);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            startInfo.ArgumentList.Add("--bind");
            startInfo.ArgumentList.Add("127.0.0.1");
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--persistence-mode");
            startInfo.ArgumentList.Add("cohort");
            startInfo.ArgumentList.Add("--game-mode");
            startInfo.ArgumentList.Add("multiplayer");
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--command-scheduling");
            startInfo.ArgumentList.Add("clock-gated");
            startInfo.ArgumentList.Add("--starting-level");
            startInfo.ArgumentList.Add("0");
            if (randomSeed.HasValue) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add(randomSeed.Value.ToString());
            }
            var process = new Process { StartInfo = startInfo };
            try {
                if (!process.Start())
                    throw new XunitException("The production Devilution.Server process did not start.");
                return process;
            } catch (Exception primary) {
                try {
                    process.Dispose();
                } catch (Exception secondary) {
                    primary.Data["PendingCliStartupDispose"] = secondary.Message;
                }
                throw;
            }
        }

        private static int ParsePort(IReadOnlyList<string> lines)
        {
            var line = lines.Single(value => value.StartsWith(
                "Godevilution authoritative server listening on ",
                StringComparison.Ordinal));
            var separator = line.LastIndexOf(':');
            return int.Parse(line[(separator + 1)..]);
        }

        private static string ParseRequiredLine(
            IReadOnlyList<string> lines,
            string prefix)
        {
            var line = lines.Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
            return line[prefix.Length..].Trim();
        }

        private static async Task TerminateAndDisposeAsync(
            Process process,
            Task<string>? output,
            Task<string>? error,
            Exception? primary = null)
        {
            var failure = primary;
            try {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            } catch (Exception exception) {
                RecordCleanupFailure(ref failure, exception, "Kill");
            }
            try {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            } catch (Exception exception) {
                RecordCleanupFailure(ref failure, exception, "TerminalWait");
            }
            foreach (var (name, task) in new[] { ("Stdout", output), ("Stderr", error) }) {
                if (task is null)
                    continue;
                try {
                    await task.WaitAsync(TimeSpan.FromSeconds(2));
                } catch (Exception exception) {
                    RecordCleanupFailure(ref failure, exception, name);
                }
            }
            try {
                process.Dispose();
            } catch (Exception exception) {
                RecordCleanupFailure(ref failure, exception, "Handle");
            }
            if (primary is null && failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class PendingParticipant : IAsyncDisposable
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private ulong nextTimingRequestId;

        private PendingParticipant(
            TcpClient client,
            NetworkStream stream,
            string sessionToken,
            Snapshot snapshot,
            string characterName,
            ulong lastClientSequence,
            uint tickRateHz)
        {
            this.client = client;
            this.stream = stream;
            SessionToken = sessionToken;
            Snapshot = snapshot;
            CharacterName = characterName;
            LastClientSequence = lastClientSequence;
            TickRateHz = tickRateHz;
        }

        public string SessionToken { get; }
        public string SessionId => SessionIdForToken(SessionToken);
        public Snapshot Snapshot { get; private set; }
        public uint EntityId => Snapshot.Players.Single(
            player => player.CharacterName == CharacterName).EntityId;
        public ulong LastClientSequence { get; }
        public uint TickRateHz { get; }
        private string CharacterName { get; }

        public static async Task<PendingParticipant> ConnectAsync(
            PendingCliHost host,
            string characterName,
            string resumeToken,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var client = new TcpClient();
            try {
                await client.ConnectAsync(IPAddress.Loopback, host.Port, timeout.Token);
                var stream = client.GetStream();
                await EnvelopeCodec.WriteAsync(
                    stream,
                    new Envelope {
                        ClientHello = new ClientHello {
                            ClientBuildId = "cohort-cli-pending-recovery-test",
                            ProtocolSchemaVersion = host.ProtocolSchemaVersion,
                            ContentManifestHash = host.ContentManifestHash,
                            RulesetIdentityHash = host.RulesetIdentityHash,
                            ResumeToken = resumeToken,
                            RequestedCharacterName = characterName,
                            SupportsClockGatedCommands = true,
                        },
                    },
                    timeout.Token);
                var hello = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
                Assert.Equal(host.ContentManifestHash, hello.ServerHello.ContentManifestHash);
                Assert.Equal(host.RulesetIdentityHash, hello.ServerHello.RulesetIdentityHash);
                Assert.Equal(host.ProtocolSchemaVersion, hello.ServerHello.ProtocolSchemaVersion);
                Assert.Equal(GameMode.Multiplayer, hello.ServerHello.GameMode);
                Assert.Equal(CommandSchedulingMode.ClockGated, hello.ServerHello.CommandSchedulingMode);
                Assert.NotEqual(0U, hello.ServerHello.TickRateHz);
                var snapshot = await ReadSnapshotAsync(stream, timeout.Token);
                return new PendingParticipant(
                    client,
                    stream,
                    hello.ServerHello.SessionToken,
                    snapshot,
                    characterName,
                    hello.ServerHello.LastClientSequence,
                    hello.ServerHello.TickRateHz);
            } catch {
                client.Dispose();
                throw;
            }
        }

        public async Task<FreshSnapshotObservation> RequestSnapshotAsync(
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var timingRequestId = checked(++nextTimingRequestId);
            var probeStarted = Stopwatch.GetTimestamp();
            await EnvelopeCodec.WriteAsync(
                stream,
                new Envelope {
                    SnapshotRequest = new Envelope.Types.SnapshotRequest {
                        TimingRequestId = timingRequestId,
                    },
                },
                timeout.Token);
            Snapshot? timingSnapshot = null;
            while (true) {
                var envelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                switch (envelope.PayloadCase) {
                case Envelope.PayloadOneofCase.Snapshot:
                    // The production host publishes the correlated snapshot
                    // before its matching timing reply.  Keep consuming any
                    // queued broadcasts until that reply is observed.
                    timingSnapshot = envelope.Snapshot;
                    break;
                case Envelope.PayloadOneofCase.TimingReply:
                    if (envelope.TimingReply.RequestId != timingRequestId)
                        continue;
                    timingSnapshot ??= await ReadSnapshotAsync(stream, timeout.Token);
                    await EnvelopeCodec.WriteAsync(
                        stream,
                        new Envelope {
                            // Obtain the snapshot following the correlated
                            // timing reply, so no pre-request broadcast can be
                            // mistaken for the command-admission boundary.
                            SnapshotRequest = new Envelope.Types.SnapshotRequest(),
                        },
                        timeout.Token);
                    Snapshot = await ReadSnapshotAsync(stream, timeout.Token);
                    return new FreshSnapshotObservation(
                        Snapshot,
                        timingRequestId,
                        timingSnapshot.Tick,
                        Stopwatch.GetElapsedTime(probeStarted));
                case Envelope.PayloadOneofCase.EventBatch:
                    break;
                default:
                    throw new XunitException(
                        $"Unexpected payload while waiting for timing request {timingRequestId}: "
                        + $"{envelope.PayloadCase}.");
                }
            }
        }

        public async Task SendWithoutReadingAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await EnvelopeCodec.WriteAsync(
                stream,
                new Envelope {
                    CommandBatch = new CommandBatch { Commands = { command } },
                },
                timeout.Token);
        }

        public async Task<(CommandResult Result, Snapshot Snapshot)> SendAndReadAsync(
            Command command,
            CancellationToken cancellationToken,
            string? admissionDiagnostics = null)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var admissionSnapshotTick = Snapshot.Tick;
            await EnvelopeCodec.WriteAsync(
                stream,
                new Envelope {
                    CommandBatch = new CommandBatch { Commands = { command } },
                },
                timeout.Token);
            CommandResult? result = null;
            while (result is null) {
                var envelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                switch (envelope.PayloadCase) {
                case Envelope.PayloadOneofCase.CommandAck:
                    result = envelope.CommandAck.Results.SingleOrDefault(
                        candidate => candidate.ClientSequence == command.ClientSequence);
                    break;
                case Envelope.PayloadOneofCase.Snapshot:
                    Snapshot = envelope.Snapshot;
                    break;
                case Envelope.PayloadOneofCase.EventBatch:
                case Envelope.PayloadOneofCase.TimingReply:
                    break;
                default:
                    throw new XunitException(
                        $"Unexpected payload while waiting for command {command.ClientSequence}: "
                        + $"{envelope.PayloadCase}.");
                }
            }
            if (result!.Status is CommandStatus.Rejected or CommandStatus.Rescheduled) {
                throw new XunitException(
                    "Production CLI pending command admission was not accepted: "
                    + $"requested tick {command.RequestedTick}, "
                    + $"snapshot tick {admissionSnapshotTick}, "
                    + $"status {result.Status}, reason {result.RejectReason}, "
                    + $"applied tick {result.AppliedTick}, "
                    + $"receipt {result.ServerReceiptSequence}."
                    + (admissionDiagnostics is null
                        ? string.Empty
                        : $" Admission probe: {admissionDiagnostics}."));
            }
            Snapshot = await ReadSnapshotAsync(stream, timeout.Token);
            Assert.True(
                Snapshot.Tick >= result!.AppliedTick,
                $"Snapshot tick {Snapshot.Tick} regressed below applied tick {result.AppliedTick}.");
            return (result!, Snapshot);
        }

        public async ValueTask DisposeAsync()
        {
            try {
                await stream.DisposeAsync();
            } finally {
                client.Dispose();
            }
        }
    }

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static async Task<Snapshot> ReadSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (true) {
            var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot)
                return envelope.Snapshot;
            if (envelope.PayloadCase is Envelope.PayloadOneofCase.EventBatch
                or Envelope.PayloadOneofCase.TimingReply)
                continue;
            throw new XunitException(
                $"Unexpected payload while waiting for a snapshot: {envelope.PayloadCase}.");
        }
    }

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var envelope = await EnvelopeCodec.ReadAsync(stream, timeout.Token)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), timeout.Token);
        return envelope ?? throw new XunitException(
            "The production CLI closed a participant connection unexpectedly.");
    }
}
