using System.Net;
using System.Net.Sockets;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/**
 * Explicit known-gap coverage for cold multiplayer resume. A participant
 * checkpoint currently contains the complete shared world, so importing a
 * stale participant can overwrite a newer participant's world outcome.
 *
 * Keep this fixture explicit until the host has atomic cohort recovery: load
 * one canonical shared-world checkpoint before admitting participants, then
 * merge participant-owned state. ImportSessionState remains a complete-world
 * migration contract and is intentionally not changed or bypassed here.
 */
public sealed class ColdParticipantWorldRestoreTests
{
    private const uint ClaimedItemEntityId = 0xCAFE01;
    private const uint ClaimedItemStableId = 0xCAFE02;
    private const uint ClaimedItemSeed = 0xCAFE1234;
    private const string RulesetIdentityHash = "content-hash";

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ColdResumeDoesNotLetStaleParticipantCheckpointResurrectClaimedWorldItem(
        bool resumeAFirst)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Cold-Participant-World-Restore-" + Guid.NewGuid().ToString("N"));
        var retainedRoot = root + "-crash-snapshot";
        var clock = new TestClock { Tick = 10 };
        var firstToken = string.Empty;
        var secondToken = string.Empty;
        try {
            var saveStore = new AuthoritativeSaveStore(root);
            var initialExecutor = CreateExecutor();
            var initialServer = CreateServer(
                initialExecutor,
                saveStore,
                () => (ulong)System.Threading.Volatile.Read(ref clock.Tick));
            initialServer.Start();
            try {
                using var firstClient = new TcpClient();
                var first = await ConnectAsync(
                    firstClient,
                    initialServer.Port,
                    resumeToken: string.Empty,
                    requestedCharacterName: "A",
                    cancellationToken: TestContext.Current.CancellationToken);
                await using var firstStream = first.Stream;
                firstToken = first.Token;
                Assert.Contains(
                    first.Snapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));

                using var secondClient = new TcpClient();
                var second = await ConnectAsync(
                    secondClient,
                    initialServer.Port,
                    resumeToken: string.Empty,
                    requestedCharacterName: "B",
                    cancellationToken: TestContext.Current.CancellationToken);
                await using var secondStream = second.Stream;
                secondToken = second.Token;
                Assert.Contains(
                    second.Snapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));

                var firstSessionId = SessionIdForToken(firstToken);
                var secondSessionId = SessionIdForToken(secondToken);
                using var staleCheckpointWait =
                    CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                staleCheckpointWait.CancelAfter(TimeSpan.FromSeconds(5));
                var staleCheckpointStore = new AuthoritativeSessionStore(root);
                await WaitUntilAsync(
                    () => HasCheckpoint(
                        staleCheckpointStore,
                        firstSessionId,
                        "A",
                        10,
                        containsClaimedWorldItem: true)
                        && HasCheckpoint(
                            staleCheckpointStore,
                            secondSessionId,
                            "B",
                            10,
                            containsClaimedWorldItem: true),
                    staleCheckpointWait.Token);
                var staleCheckpoint = staleCheckpointStore.Load(secondSessionId, RulesetIdentityHash);
                Assert.NotNull(staleCheckpoint);
                var saveCodec = new AuthoritativeSaveCodec();
                var staleSnapshot = saveCodec.Deserialize(
                    staleCheckpoint!.SerializedState);
                Assert.Equal(10UL, staleSnapshot.Tick);
                Assert.Equal("B", staleSnapshot.Player.CharacterName);
                Assert.Contains(
                    staleSnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));

                System.Threading.Volatile.Write(ref clock.Tick, 11);
                using var initialTickWait =
                    CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                initialTickWait.CancelAfter(TimeSpan.FromSeconds(5));
                await WaitUntilAsync(
                    () => HasCheckpoint(
                        staleCheckpointStore,
                        firstSessionId,
                        "A",
                        11,
                        containsClaimedWorldItem: true)
                        && HasCheckpoint(
                            staleCheckpointStore,
                            secondSessionId,
                            "B",
                            11,
                            containsClaimedWorldItem: true),
                    initialTickWait.Token);

                await EnvelopeCodec.WriteAsync(
                    firstStream,
                    new Envelope {
                        CommandBatch = new CommandBatch {
                            Commands = {
                                new Command {
                                    ClientSequence = 1,
                                    RequestedTick = 11,
                                    PickupWorldItemRequested = new PickupWorldItemRequested {
                                        ItemEntityId = ClaimedItemEntityId,
                                    },
                                },
                            },
                        },
                    },
                    TestContext.Current.CancellationToken);
                var pickupAck = await ReadRequiredEnvelopeAsync(
                    firstStream,
                    TestContext.Current.CancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    pickupAck.CommandAck.Results.Single().Status);
                var pickedUpSnapshot = (await ReadRequiredEnvelopeAsync(
                    firstStream,
                    TestContext.Current.CancellationToken)).Snapshot;
                Assert.DoesNotContain(
                    pickedUpSnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                Assert.Contains(
                    pickedUpSnapshot.Players.Single(player => player.CharacterName == "A").Inventory,
                    item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
                Assert.Equal(11UL, pickedUpSnapshot.Tick);

                using var latestCheckpointWait =
                    CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                latestCheckpointWait.CancelAfter(TimeSpan.FromSeconds(5));
                await WaitUntilAsync(
                    () => {
                        var document = staleCheckpointStore.Load(firstSessionId, RulesetIdentityHash);
                        if (document is null)
                            return false;
                        var snapshot = saveCodec.Deserialize(document.SerializedState);
                        return snapshot.Tick == 11
                            && !snapshot.WorldItems.Any(item => IsClaimedItem(
                                item.EntityId,
                                item.State.StableItemId,
                                item.ItemSeed))
                            && snapshot.Player.Inventory.Any(item => IsClaimedItem(
                                item.State.StableItemId,
                                item.ItemSeed));
                    },
                    latestCheckpointWait.Token);
                var latestCheckpoint = staleCheckpointStore.Load(firstSessionId, RulesetIdentityHash);
                Assert.NotNull(latestCheckpoint);
                var latestSnapshot = saveCodec.Deserialize(
                    latestCheckpoint!.SerializedState);
                Assert.Equal(11UL, latestSnapshot.Tick);
                Assert.Equal("A", latestSnapshot.Player.CharacterName);
                Assert.DoesNotContain(
                    latestSnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                Assert.Contains(
                    latestSnapshot.Player.Inventory,
                    item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));

                // Read B again immediately after A's command checkpoint. Both
                // saves are tick 11, but B must still be the pre-pickup world.
                // This proves the stale publication is real rather than an
                // assumed hand-authored checkpoint.
                var staleAfterPickup = staleCheckpointStore.Load(
                    secondSessionId,
                    RulesetIdentityHash);
                Assert.NotNull(staleAfterPickup);
                var staleAfterPickupSnapshot = saveCodec.Deserialize(
                    staleAfterPickup!.SerializedState);
                Assert.Equal(latestSnapshot.Tick, staleAfterPickupSnapshot.Tick);
                Assert.Equal("B", staleAfterPickupSnapshot.Player.CharacterName);
                Assert.Contains(
                    staleAfterPickupSnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));

                // The clock is held at tick 11. The command checkpoint updates
                // A, while the durable B checkpoint remains the pre-pickup
                // publication. Copy those actual server files before shutdown
                // to model a crash/restart without fabricating checkpoint data.
                CopyDirectory(root, retainedRoot);
                var retainedStore = new AuthoritativeSessionStore(retainedRoot);
                var retainedA = retainedStore.Load(firstSessionId, RulesetIdentityHash);
                var retainedB = retainedStore.Load(secondSessionId, RulesetIdentityHash);
                Assert.NotNull(retainedA);
                Assert.NotNull(retainedB);
                var retainedASnapshot = saveCodec.Deserialize(retainedA!.SerializedState);
                var retainedBSnapshot = saveCodec.Deserialize(retainedB!.SerializedState);
                Assert.Contains(
                    retainedASnapshot.Player.Inventory,
                    item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
                Assert.DoesNotContain(
                    retainedASnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                Assert.Contains(
                    retainedBSnapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                Assert.Equal(latestSnapshot.Tick, retainedASnapshot.Tick);
                Assert.Equal(staleAfterPickupSnapshot.Tick, retainedBSnapshot.Tick);
            } finally {
                await initialServer.DisposeAsync();
            }

            var replacementExecutor = CreateExecutor();
            var replacementServer = CreateServer(
                replacementExecutor,
                new AuthoritativeSaveStore(retainedRoot),
                () => 11);
            replacementServer.Start();
            try {
                var firstResumeToken = resumeAFirst ? firstToken : secondToken;
                var secondResumeToken = resumeAFirst ? secondToken : firstToken;
                var firstExpectedName = resumeAFirst ? "A" : "B";
                var secondExpectedName = resumeAFirst ? "B" : "A";

                using var firstResumeClient = new TcpClient();
                var firstResume = await ConnectAsync(
                    firstResumeClient,
                    replacementServer.Port,
                    firstResumeToken,
                    firstExpectedName,
                    TestContext.Current.CancellationToken);
                await using var firstResumeStream = firstResume.Stream;
                Assert.Equal(11UL, firstResume.Snapshot.Tick);
                Assert.DoesNotContain(
                    firstResume.Snapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                if (resumeAFirst) {
                    Assert.Contains(
                        firstResume.Snapshot.Players.Single(player => player.CharacterName == "A").Inventory,
                        item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
                } else {
                    Assert.DoesNotContain(
                        firstResume.Snapshot.Players.Single(player => player.CharacterName == "B").Inventory,
                        item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
                }

                using var secondResumeClient = new TcpClient();
                var secondResume = await ConnectAsync(
                    secondResumeClient,
                    replacementServer.Port,
                    secondResumeToken,
                    secondExpectedName,
                    TestContext.Current.CancellationToken);
                await using var secondResumeStream = secondResume.Stream;
                Assert.Equal(11UL, secondResume.Snapshot.Tick);
                Assert.DoesNotContain(
                    secondResume.Snapshot.WorldItems,
                    item => IsClaimedItem(item.EntityId, item.State.StableItemId, item.ItemSeed));
                Assert.Contains(
                    secondResume.Snapshot.Players.Single(player => player.CharacterName == "A").Inventory,
                    item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
                Assert.DoesNotContain(
                    secondResume.Snapshot.Players.Single(player => player.CharacterName == "B").Inventory,
                    item => IsClaimedItem(item.State.StableItemId, item.ItemSeed));
            } finally {
                await replacementServer.DisposeAsync();
            }
        } finally {
            DeleteOwnedTempDirectory(
                root,
                Path.GetFullPath(Path.GetTempPath()),
                Path.GetFileName(root));
            DeleteOwnedTempDirectory(
                retainedRoot,
                Path.GetFullPath(Path.GetTempPath()),
                Path.GetFileName(retainedRoot));
        }
    }

    private static StoreSimulationExecutor CreateExecutor()
    {
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = ClaimedItemStableId,
            ItemType = 1,
            Value = 75,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        return new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            gameMode: DiabloGameMode.Multiplayer,
            startingWorldItems: [new AuthoritativeWorldItem(
                ClaimedItemEntityId,
                0,
                1,
                0,
                ClaimedItemSeed,
                75,
                itemState)]);
    }

    private static AuthoritativeTcpServer CreateServer(
        StoreSimulationExecutor executor,
        AuthoritativeSaveStore saveStore,
        Func<ulong> currentTickProvider)
    {
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            RulesetIdentityHash,
            20) {
            GameMode = AuthoritativeGameMode.Multiplayer,
        });
        return new AuthoritativeTcpServer(
            commandServer,
            handshake,
            currentTickProvider,
            port: 0,
            address: IPAddress.Loopback,
            snapshotProvider: executor,
            participantInitializer: executor,
            saveProvider: executor,
            saveStore: saveStore);
    }

    private static async Task<(string Token, NetworkStream Stream, Snapshot Snapshot)> ConnectAsync(
        TcpClient client,
        int port,
        string resumeToken,
        string requestedCharacterName,
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
                    ContentManifestHash = RulesetIdentityHash,
                    RulesetIdentityHash = RulesetIdentityHash,
                    ResumeToken = resumeToken,
                    RequestedCharacterName = requestedCharacterName,
                },
            },
            cancellationToken);
        var serverHello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, serverHello.PayloadCase);
        Assert.Equal(GameMode.Multiplayer, serverHello.ServerHello.GameMode);
        var snapshot = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
        return (serverHello.ServerHello.SessionToken, stream, snapshot.Snapshot);
    }

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        return envelope ?? throw new XunitException("The server closed the connection before sending an envelope.");
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++) {
            if (predicate())
                return;
            await Task.Delay(10, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for a durable checkpoint.");
    }

    private static bool IsClaimedItem(
        uint entityId,
        uint stableItemId,
        uint itemSeed)
        => entityId == ClaimedItemEntityId
            && stableItemId == ClaimedItemStableId
            && itemSeed == ClaimedItemSeed;

    private static bool IsClaimedItem(uint stableItemId, uint itemSeed)
        => stableItemId == ClaimedItemStableId
            && itemSeed == ClaimedItemSeed;

    private static bool HasCheckpoint(
        AuthoritativeSessionStore store,
        string sessionId,
        string expectedCharacterName,
        ulong expectedTick,
        bool containsClaimedWorldItem)
    {
        var document = store.Load(sessionId, RulesetIdentityHash);
        if (document is null)
            return false;
        var snapshot = new AuthoritativeSaveCodec().Deserialize(document.SerializedState);
        return snapshot.Tick == expectedTick
            && string.Equals(
                snapshot.Player.CharacterName,
                expectedCharacterName,
                StringComparison.Ordinal)
            && snapshot.WorldItems.Any(item => IsClaimedItem(
                item.EntityId,
                item.State.StableItemId,
                item.ItemSeed)) == containsClaimedWorldItem;
    }

    private static string SessionIdForToken(string resumeToken)
    {
        return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        var source = Path.GetFullPath(sourceRoot);
        var destination = Path.GetFullPath(destinationRoot);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The crash snapshot directory must be distinct from the live save root.");
        if (Directory.Exists(destination))
            throw new InvalidOperationException("The crash snapshot directory must be fresh before copying durable files.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The crash snapshot copy escaped its owned directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void DeleteOwnedTempDirectory(
        string directory,
        string expectedParent,
        string expectedName)
    {
        var fullDirectory = Path.GetFullPath(directory);
        var fullParent = Path.GetFullPath(expectedParent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        const string namePrefix = "Godevilution-Cold-Participant-World-Restore-";
        const string retainedSuffix = "-crash-snapshot";
        var guidName = expectedName.EndsWith(retainedSuffix, StringComparison.Ordinal)
            ? expectedName[..^retainedSuffix.Length]
            : expectedName;
        if (!string.Equals(Path.GetDirectoryName(fullDirectory)?.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar), fullParent, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(fullDirectory), expectedName, StringComparison.Ordinal)
            || !guidName.StartsWith(namePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(guidName[namePrefix.Length..], "N", out _)) {
            throw new InvalidOperationException(
                "Refusing to delete a cold-restore path outside its exact owned GUID temp directory.");
        }
        if (!Directory.Exists(fullDirectory))
            return;
        if ((File.GetAttributes(fullDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to recurse through a reparse-point test root.");
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(fullDirectory);
        while (pendingDirectories.Count != 0) {
            var current = pendingDirectories.Pop();
            foreach (var child in new DirectoryInfo(current).EnumerateFileSystemInfos(
                         "*",
                         SearchOption.TopDirectoryOnly)) {
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Refusing to recurse through a reparse point in the test root.");
                if (child is DirectoryInfo)
                    pendingDirectories.Push(child.FullName);
            }
        }
        Directory.Delete(fullDirectory, recursive: true);
    }

    private sealed class TestClock
    {
        public long Tick;
    }
}
