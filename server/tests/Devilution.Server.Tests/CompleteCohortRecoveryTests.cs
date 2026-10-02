using System.Net;
using System.Net.Sockets;
using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/**
 * Loopback coverage for the complete canonical host checkpoint.  The test
 * deliberately copies only the published checkpoint, never the live lease.
 */
public sealed partial class CompleteCohortRecoveryTests
{
    private const uint ClaimedItemEntityId = 0xCAFE01;
    private const uint ClaimedItemStableId = 0xCAFE02;
    private const uint ClaimedItemSeed = 0xCAFE1234;
    private const string IdentityHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompleteCheckpointRestoresSharedWorldForEitherResumeOrder(
        bool clockGated,
        bool resumeAFirst)
    {
        var root = NewRoot("Godev-Cohort-Recovery-");
        var retainedRoot = root + "-retained";
        var clock = new ManualClock(11);
        var tokenA = string.Empty;
        var tokenB = string.Empty;
        uint entityA;
        uint entityB;
        try {
            var store = new AuthoritativeCohortStore(root);
            await using (var server = CreateServer(CreateExecutor(), clock, store, clockGated)) {
                server.Start();
                using var clientA = new TcpClient();
                var connectedA = await ConnectAsync(
                    clientA,
                    server.Port,
                    string.Empty,
                    "A",
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var streamA = connectedA.Stream;
                tokenA = connectedA.Token;
                entityA = connectedA.Snapshot.Players.Single(
                    player => player.CharacterName == "A").EntityId;

                using var clientB = new TcpClient();
                var connectedB = await ConnectAsync(
                    clientB,
                    server.Port,
                    string.Empty,
                    "B",
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var streamB = connectedB.Stream;
                tokenB = connectedB.Token;
                entityB = connectedB.Snapshot.Players.Single(
                    player => player.CharacterName == "B").EntityId;
                Assert.NotEqual(entityA, entityB);

                Assert.Contains(connectedA.Snapshot.WorldItems, IsClaimedItem);
                clientB.Dispose(); // B remains an offline canonical participant.

                await EnvelopeCodec.WriteAsync(
                    streamA,
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
                    streamA,
                    TestContext.Current.CancellationToken);
                Assert.Equal(Envelope.PayloadOneofCase.CommandAck, pickupAck.PayloadCase);
                var pickupResult = Assert.Single(pickupAck.CommandAck.Results);
                Assert.Equal(CommandStatus.Accepted, pickupResult.Status);
                Assert.Equal(1UL, pickupResult.ServerReceiptSequence);
                var pickedUpSnapshot = await ReadSnapshotAsync(
                    streamA,
                    TestContext.Current.CancellationToken);
                Assert.DoesNotContain(pickedUpSnapshot.WorldItems, IsClaimedItem);
                Assert.Contains(
                    pickedUpSnapshot.Players.Single(player => player.CharacterName == "A").Inventory,
                    item => item.State.StableItemId == ClaimedItemStableId
                        && item.ItemSeed == ClaimedItemSeed);
                Assert.DoesNotContain(
                    pickedUpSnapshot.Players.Single(player => player.CharacterName == "B").Inventory,
                    item => item.State.StableItemId == ClaimedItemStableId
                        && item.ItemSeed == ClaimedItemSeed);

                await WaitForCheckpointAsync(store, TestContext.Current.CancellationToken);
                CopyPublishedCheckpoint(store, retainedRoot);
            }

            var retainedStore = new AuthoritativeCohortStore(retainedRoot);
            await using (var restarted = CreateServer(CreateExecutor(), clock, retainedStore, clockGated)) {
                restarted.Start();
                Assert.Equal(
                    new[] { SessionIdForToken(tokenA), SessionIdForToken(tokenB) }
                        .OrderBy(sessionId => sessionId, StringComparer.Ordinal),
                    restarted.ActiveSessionIds);
                var firstToken = resumeAFirst ? tokenA : tokenB;
                var secondToken = resumeAFirst ? tokenB : tokenA;
                var firstName = resumeAFirst ? "A" : "B";
                var secondName = resumeAFirst ? "B" : "A";

                using var firstClient = new TcpClient();
                var first = await ConnectAsync(
                    firstClient,
                    restarted.Port,
                    firstToken,
                    firstName,
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var firstStream = first.Stream;
                Assert.Equal(11UL, first.Snapshot.Tick);
                Assert.Equal(entityA, first.Snapshot.Players.Single(
                    player => player.CharacterName == "A").EntityId);
                Assert.Equal(entityB, first.Snapshot.Players.Single(
                    player => player.CharacterName == "B").EntityId);
                Assert.DoesNotContain(first.Snapshot.WorldItems, IsClaimedItem);
                Assert.Contains(
                    first.Snapshot.Players.Single(player => player.CharacterName == "A").Inventory,
                    item => item.State.StableItemId == ClaimedItemStableId
                        && item.ItemSeed == ClaimedItemSeed);
                Assert.DoesNotContain(
                    first.Snapshot.Players.Single(player => player.CharacterName == "B").Inventory,
                    item => item.State.StableItemId == ClaimedItemStableId
                        && item.ItemSeed == ClaimedItemSeed);

                using var secondClient = new TcpClient();
                var second = await ConnectAsync(
                    secondClient,
                    restarted.Port,
                    secondToken,
                    secondName,
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var secondStream = second.Stream;
                Assert.Equal(entityA, second.Snapshot.Players.Single(
                    player => player.CharacterName == "A").EntityId);
                Assert.Equal(entityB, second.Snapshot.Players.Single(
                    player => player.CharacterName == "B").EntityId);
                Assert.DoesNotContain(second.Snapshot.WorldItems, IsClaimedItem);

                var aStream = resumeAFirst ? firstStream : second.Stream;
                await EnvelopeCodec.WriteAsync(
                    aStream,
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
                var duplicate = await ReadRequiredEnvelopeAsync(
                    aStream,
                    TestContext.Current.CancellationToken);
                var duplicateResult = Assert.Single(duplicate.CommandAck.Results);
                Assert.Equal(CommandStatus.Duplicate, duplicateResult.Status);
                Assert.Equal(1UL, duplicateResult.ClientSequence);
                Assert.Equal(1UL, duplicateResult.ServerReceiptSequence);
                _ = await ReadSnapshotAsync(aStream, TestContext.Current.CancellationToken);

                using var newClient = new TcpClient();
                var newParticipant = await ConnectAsync(
                    newClient,
                    restarted.Port,
                    string.Empty,
                    "C",
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var newStream = newParticipant.Stream;
                var entityC = newParticipant.Snapshot.Players.Single(
                    player => player.CharacterName == "C").EntityId;
                Assert.DoesNotContain(entityC, new[] { entityA, entityB });
                Assert.True(entityC > Math.Max(entityA, entityB));
                Assert.DoesNotContain(newParticipant.Snapshot.WorldItems, IsClaimedItem);
            }
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Recovery-");
            DeleteOwnedTempDirectory(retainedRoot, "Godev-Cohort-Recovery-");
        }
    }

    [Fact]
    public async Task CorruptCanonicalCheckpointRefusesBeforeListeningAndPreservesBytes()
    {
        await AssertCanonicalStartupRefusesBeforeListeningAsync(corrupt: true);
    }

    [Fact]
    public async Task CanonicalIdentityMismatchRefusesBeforeListeningAndPreservesBytes()
    {
        await AssertCanonicalStartupRefusesBeforeListeningAsync(corrupt: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCohortPublicationFencesHostWithoutAckOrRepublish(bool clockGated)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = NewRoot("Godev-Cohort-Failure-");
        var clock = new ManualClock(11);
        try {
            var store = new AuthoritativeCohortStore(root);
            var executor = CreateExecutor();
            var commandServer = new AuthoritativeCommandServer(executor);
            var durableBeforeFailure = Array.Empty<byte>();
            var server = CreateServer(
                             executor,
                             clock,
                             store,
                             clockGated,
                             commandServer: commandServer);
            Exception? observedFatal = null;
            try {
                server.Start();
                using var client = new TcpClient();
                var connected = await ConnectAsync(
                    client,
                    server.Port,
                    string.Empty,
                    "A",
                    clockGated,
                    TestContext.Current.CancellationToken);
                await using var stream = connected.Stream;
                await WaitForCheckpointAsync(store, TestContext.Current.CancellationToken);
                durableBeforeFailure = File.ReadAllBytes(store.CheckpointPath);

                using (var noDelete = new FileStream(
                           store.CheckpointPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read)) {
                    await EnvelopeCodec.WriteAsync(
                        stream,
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

                    using var completionWait = CancellationTokenSource.CreateLinkedTokenSource(
                        TestContext.Current.CancellationToken);
                    completionWait.CancelAfter(TimeSpan.FromSeconds(5));
                    var completionFailure = await Record.ExceptionAsync(
                        async () => await server.Completion.WaitAsync(completionWait.Token));
                    observedFatal = Assert.IsType<IOException>(completionFailure);

                    var response = await ReadEnvelopeOrNullAsync(
                        stream,
                        TestContext.Current.CancellationToken);
                    Assert.True(response is null
                        || response.PayloadCase != Envelope.PayloadOneofCase.CommandAck);

                    clock.CurrentTick = 12;
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                    Assert.Equal(11UL, commandServer.CurrentTick);
                }

                // The using scope deliberately releases the Windows no-delete
                // handle before host shutdown; a fenced host must not republish
                // the already-executed but undurable pickup.
            } finally {
                var shutdownFailure = await Record.ExceptionAsync(async () => await server.DisposeAsync());
                if (shutdownFailure is not null) {
                    Assert.NotNull(observedFatal);
                    Assert.Same(observedFatal, shutdownFailure);
                }
            }

            Assert.Equal(
                durableBeforeFailure,
                File.ReadAllBytes(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName)));
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Failure-");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedClassAdmissionPublishesAllocatorGapAndAllowsTokenRetry(
        bool clockGated)
    {
        var root = NewRoot("Godev-Cohort-Class-Rejection-");
        const string retryToken = "cohort-class-retry-token";
        var clock = new ManualClock(11);
        try {
            var content = ActivateBaseContent();
            var executor = CreateClassSelectingExecutor(content);
            var validClassId = content.PlayerClasses.Default.ClassId;
            var store = new AuthoritativeCohortStore(root);
            await using var server = CreateServer(executor, clock, store, clockGated);
            server.Start();

            using var baselineClient = new TcpClient();
            var baseline = await ConnectAsync(
                baselineClient,
                server.Port,
                string.Empty,
                "Baseline",
                clockGated,
                TestContext.Current.CancellationToken,
                requestedClassId: validClassId);
            await using var baselineStream = baseline.Stream;
            var before = await WaitForCheckpointWithParticipantAsync(
                store,
                SessionIdForToken(baseline.Token),
                TestContext.Current.CancellationToken);

            using var rejectedClient = new TcpClient();
            await rejectedClient.ConnectAsync(
                    IPAddress.Loopback,
                    server.Port,
                    TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var rejectedStream = rejectedClient.GetStream();
            await EnvelopeCodec.WriteAsync(
                rejectedStream,
                new Envelope {
                    ClientHello = CreateClientHello(
                        retryToken,
                        "Rejected",
                        clockGated,
                        uint.MaxValue),
                },
                TestContext.Current.CancellationToken);
            var rejection = await ReadEnvelopeOrNullAsync(
                rejectedStream,
                TestContext.Current.CancellationToken);
            Assert.True(
                rejection is null || rejection.PayloadCase == Envelope.PayloadOneofCase.Error,
                "An unsupported class must not receive a successful ServerHello.");
            var rejectedSessionId = SessionIdForToken(retryToken);
            Assert.DoesNotContain(rejectedSessionId, server.ActiveSessionIds);

            var afterFailure = await WaitForCheckpointGenerationGreaterThanAsync(
                store,
                before.Generation,
                TestContext.Current.CancellationToken);
            Assert.Equal(before.SerializedModuleState, afterFailure.SerializedModuleState);
            Assert.DoesNotContain(
                afterFailure.Participants,
                participant => participant.SessionId == rejectedSessionId);
            Assert.True(
                afterFailure.NextEntityId > before.NextEntityId,
                "The failed reservation must leave a published allocator gap.");

            clock.CurrentTick = 12;
            var after = await WaitForCheckpointAtLeastAsync(
                store,
                12,
                TestContext.Current.CancellationToken);
            Assert.DoesNotContain(
                after.Participants,
                participant => participant.SessionId == rejectedSessionId);
            Assert.Equal(afterFailure.NextEntityId, after.NextEntityId);

            using var retryClient = new TcpClient();
            var retry = await ConnectAsync(
                retryClient,
                server.Port,
                retryToken,
                "Recovered",
                clockGated,
                TestContext.Current.CancellationToken,
                requestedClassId: validClassId);
            await using var retryStream = retry.Stream;
            var recovered = retry.Snapshot.Players.Single(
                player => player.CharacterName == "Recovered");
            Assert.Equal(after.NextEntityId, recovered.EntityId);
            Assert.DoesNotContain(
                retry.Snapshot.Players,
                player => player.CharacterName == "Rejected");
            Assert.Contains(
                retry.Snapshot.Players,
                player => player.CharacterName == "Baseline");
            Assert.Contains(rejectedSessionId, server.ActiveSessionIds);
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Class-Rejection-");
        }
    }

    private static async Task AssertCanonicalStartupRefusesBeforeListeningAsync(bool corrupt)
    {
        var root = NewRoot("Godev-Cohort-Startup-");
        var clock = new ManualClock(11);
        try {
            var store = new AuthoritativeCohortStore(root);
            await using (var initial = CreateServer(CreateExecutor(), clock, store, false)) {
                initial.Start();
                using var client = new TcpClient();
                var connected = await ConnectAsync(
                    client,
                    initial.Port,
                    string.Empty,
                    "A",
                    false,
                    TestContext.Current.CancellationToken);
                await using var stream = connected.Stream;
                await WaitForCheckpointAsync(store, TestContext.Current.CancellationToken);
            }

            if (corrupt) {
                var corrupted = File.ReadAllBytes(store.CheckpointPath);
                corrupted[^1] = corrupted[^1] == (byte)'}' ? (byte)'!' : (byte)'}';
                File.WriteAllBytes(store.CheckpointPath, corrupted);
            }

            var restartStore = new AuthoritativeCohortStore(root + "-restart");
            if (corrupt)
                File.Copy(store.CheckpointPath, restartStore.CheckpointPath);
            else {
                File.Copy(store.CheckpointPath, restartStore.CheckpointPath);
            }
            var expectedBytes = File.ReadAllBytes(restartStore.CheckpointPath);
            var identity = corrupt
                ? IdentityHash
                : "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
            await using var rejected = CreateServer(
                CreateExecutor(),
                clock,
                restartStore,
                false,
                identity);
            Assert.Throws<InvalidDataException>(() => rejected.Start());
            Assert.Equal(0, rejected.Port);
            Assert.Equal(expectedBytes, File.ReadAllBytes(restartStore.CheckpointPath));
        } finally {
            DeleteOwnedTempDirectory(root, "Godev-Cohort-Startup-");
            DeleteOwnedTempDirectory(root + "-restart", "Godev-Cohort-Startup-");
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

    private static StoreSimulationExecutor CreateClassSelectingExecutor(
        DiabloContentActivation content)
    {
        return new StoreSimulationExecutor(
            content,
            (sessionId, classId) => content.PlayerLoadouts.CreateTemplate(
                sessionId,
                classId ?? content.PlayerClasses.Default.ClassId,
                null,
                null,
                null,
                content.DefaultStartingLevelId));
    }

    private static DiabloContentActivation ActivateBaseContent()
    {
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        var manifest = new ContentManifest(
            "devilution.base",
            "0.1.0",
            [ContentPackLoader.LoadDirectory(
                "devilution.base",
                "0.1.0",
                contentRoot)]);
        var registry = new GameplayModuleRegistry();
        registry.Load(
            [new DiabloModule()],
            new GameplayModuleContext(
                manifest,
                new DelegateAuthoritativeClock(() => 0)));
        return registry.GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(
                ComposedContent.Compose(manifest),
                gameMode: DiabloGameMode.Multiplayer);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }

    private static AuthoritativeTcpServer CreateServer(
        StoreSimulationExecutor executor,
        IAuthoritativeClock clock,
        AuthoritativeCohortStore cohortStore,
        bool clockGated,
        string contentHash = IdentityHash,
        AuthoritativeCommandServer? commandServer = null)
    {
        commandServer ??= new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            contentHash,
            20) {
            RulesetIdentityHash = IdentityHash,
            GameMode = AuthoritativeGameMode.Multiplayer,
        });
        return new AuthoritativeTcpServer(
            commandServer,
            handshake,
            clock,
            executor,
            port: 0,
            address: IPAddress.Loopback) {
            CohortStore = cohortStore,
            UseClockGatedCommandScheduling = clockGated,
        };
    }

    private static async Task<(string Token, NetworkStream Stream, Snapshot Snapshot)> ConnectAsync(
        TcpClient client,
        int port,
        string resumeToken,
        string name,
        bool clockGated,
        CancellationToken cancellationToken,
        uint? requestedClassId = null)
    {
        await client.ConnectAsync(IPAddress.Loopback, port)
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(
            stream,
            new Envelope {
                ClientHello = CreateClientHello(
                    resumeToken,
                    name,
                    clockGated,
                    requestedClassId),
            },
            cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        var snapshot = await ReadSnapshotAsync(stream, cancellationToken);
        return (hello.ServerHello.SessionToken, stream, snapshot);
    }

    private static ClientHello CreateClientHello(
        string resumeToken,
        string name,
        bool clockGated,
        uint? requestedClassId = null)
    {
        var hello = new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = IdentityHash,
            RulesetIdentityHash = IdentityHash,
            ResumeToken = resumeToken,
            RequestedCharacterName = name,
            SupportsClockGatedCommands = clockGated,
        };
        if (requestedClassId.HasValue)
            hello.RequestedCharacterClassId = requestedClassId.Value;
        return hello;
    }

    private static async Task<Snapshot> ReadSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, envelope.PayloadCase);
        return envelope.Snapshot;
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

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointAsync(
        AuthoritativeCohortStore store,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null)
                return document;
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointAtLeastAsync(
        AuthoritativeCohortStore store,
        ulong tick,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true) {
            var document = store.Load();
            if (document is not null && document.Tick >= tick)
                return document;
            await Task.Delay(10, timeout.Token);
        }
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

    private static void CopyPublishedCheckpoint(
        AuthoritativeCohortStore source,
        string destinationRoot)
    {
        var sourceRoot = Path.GetFullPath(source.RootDirectory);
        var destination = Path.GetFullPath(destinationRoot);
        if (Directory.Exists(destination))
            throw new InvalidOperationException("The retained cohort root must be fresh.");
        Directory.CreateDirectory(destination);
        var sourcePath = Path.GetFullPath(source.CheckpointPath);
        var targetPath = Path.GetFullPath(Path.Combine(
            destination,
            AuthoritativeCohortStore.CheckpointFileName));
        if (!sourcePath.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !targetPath.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The checkpoint copy escaped its owned root.");
        File.Copy(sourcePath, targetPath, overwrite: false);
        // Deliberately do not copy authoritative-cohort.lease.
    }

    private static bool IsClaimedItem(WorldItemSnapshot item)
        => item.EntityId == ClaimedItemEntityId
            && item.State.StableItemId == ClaimedItemStableId
            && item.ItemSeed == ClaimedItemSeed;

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
        var stem = name.EndsWith("-retained", StringComparison.Ordinal)
            ? name[..^"-retained".Length]
            : name.EndsWith("-restart", StringComparison.Ordinal)
                ? name[..^"-restart".Length]
                : name;
        if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar), parent, StringComparison.OrdinalIgnoreCase)
            || !stem.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(stem[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Refusing to delete an unowned cohort test directory.");
        if (!Directory.Exists(full))
            return;
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to recurse through a reparse point.");
        Directory.Delete(full, recursive: true);
    }

    private sealed class ManualClock(ulong tick) : IAuthoritativeClock
    {
        public ulong CurrentTick { get; set; } = tick;
    }
}
