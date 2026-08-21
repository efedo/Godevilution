using System.Collections.Concurrent;
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

public sealed class AuthoritativeTcpServerTests
{
    [Fact]
    public async Task FailedStartRollsBackSoTheServerCanRetryAfterThePortIsReleased()
    {
        using var portHolder = new TcpListener(IPAddress.Loopback, 0);
        portHolder.Start();
        var port = ((IPEndPoint)portHolder.LocalEndpoint).Port;
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            port);

        Assert.Throws<SocketException>(() => server.Start());
        portHolder.Stop();

        server.Start();
        Assert.Equal(port, server.Port);
    }

    [Fact]
    public async Task HandshakeAndCommandBatchRoundTripOverLoopback()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10);
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
            },
        }, cancellationToken);

        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);

        var command = new Command {
            ClientSequence = 1,
            RequestedTick = 10,
            AttackRequested = new AttackRequested { TargetEntityId = 1 },
        };
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);

        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(acknowledgement);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement!.PayloadCase);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.CommandAck.Results.Single().Status);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);
        var duplicateAcknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(duplicateAcknowledgement);
        Assert.Equal(CommandStatus.Duplicate, duplicateAcknowledgement!.CommandAck.Results.Single().Status);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task StoreSnapshotsAreSentAfterHandshakeAndCommands()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
        });
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 100,
            startingExperience: 200,
            startingLife: 640,
            startingMana: 32,
            startingAttributes: new PlayerAttributesState(
                new PlayerAttributeState(10, 12),
                new PlayerAttributeState(8, 9),
                new PlayerAttributeState(15, 16),
                new PlayerAttributeState(20, 21)),
            startingEquipment: [new EquippedStoreItem(0, 77)],
            startingInventoryGrid: [0, -1, 2]);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
            },
        }, cancellationToken);

        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);

        var initialSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(initialSnapshot);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, initialSnapshot!.PayloadCase);
        var initialPlayer = Assert.Single(initialSnapshot.Snapshot.Players);
        Assert.Equal(100U, initialPlayer.Gold);
        Assert.Equal(200U, initialPlayer.Experience);
        Assert.Equal(640, initialPlayer.Life);
        Assert.Equal(32, initialPlayer.Mana);
        Assert.Equal(0U, initialPlayer.ActiveStoreId);
        Assert.Equal(12, initialPlayer.Attributes.Strength.Current);
        Assert.Equal(77U, Assert.Single(initialPlayer.Equipment).ItemSeed);
        Assert.Equal(new[] { 0, -1, 2 }, initialPlayer.InventoryGrid);
        Assert.Empty(initialPlayer.Inventory);
        Assert.Equal(SnapshotStateHasher.Compute(initialSnapshot.Snapshot), initialSnapshot.Snapshot.StateSha256);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var openAck = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(openAck);
        Assert.Equal(CommandStatus.Accepted, openAck!.CommandAck.Results.Single().Status);
        var openSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(openSnapshot);
        Assert.Equal(1U, Assert.Single(openSnapshot!.Snapshot.Players).ActiveStoreId);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 2,
                        RequestedTick = 10,
                        PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                    },
                },
            },
        }, cancellationToken);
        var purchaseAck = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(purchaseAck);
        Assert.Equal(CommandStatus.Accepted, purchaseAck!.CommandAck.Results.Single().Status);
        var purchaseSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(purchaseSnapshot);
        var purchasePlayer = Assert.Single(purchaseSnapshot!.Snapshot.Players);
        Assert.Equal(25U, purchasePlayer.Gold);
        var purchaseItem = Assert.Single(purchasePlayer.Inventory);
        Assert.Equal(42U, purchaseItem.ItemSeed);
        Assert.True(purchaseItem.State.Identified);
        Assert.Equal(1, purchaseItem.State.ItemType);
        Assert.Equal(SnapshotStateHasher.Compute(purchaseSnapshot.Snapshot), purchaseSnapshot.Snapshot.StateSha256);
    }

    [Fact]
    public async Task ConnectedSessionsReceiveDistinctEntityIds()
    {
        var executor = new StoreSimulationExecutor(new StoreCatalog(), startingGold: 100);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();

        await SendHelloAsync(firstStream, cancellationToken);
        await SendHelloAsync(secondStream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.PayloadCase);
        var firstSnapshot = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        var secondSnapshot = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);

        var firstEntityId = firstSnapshot!.Snapshot.Players[0].EntityId;
        var secondEntityId = secondSnapshot!.Snapshot.Players[0].EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);

        await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        SetFriendlyModeRequested = new SetFriendlyModeRequested {
                            FriendlyMode = true,
                        },
                    },
                },
            },
        }, cancellationToken);
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        SetFriendlyModeRequested = new SetFriendlyModeRequested {
                            FriendlyMode = true,
                        },
                    },
                },
            },
        }, cancellationToken);
        Assert.Equal(
            Envelope.PayloadOneofCase.CommandAck,
            (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);
        Assert.Equal(
            Envelope.PayloadOneofCase.CommandAck,
            (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.PayloadCase);
        var firstCompleteSnapshot = (await EnvelopeCodec.ReadAsync(
            firstStream,
            cancellationToken))!.Snapshot;
        var secondCompleteSnapshot = (await EnvelopeCodec.ReadAsync(
            secondStream,
            cancellationToken))!.Snapshot;

        Assert.Equal(
            [firstEntityId, secondEntityId],
            firstCompleteSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(
            [secondEntityId, firstEntityId],
            secondCompleteSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(firstCompleteSnapshot.StateSha256, secondCompleteSnapshot.StateSha256);
    }

    [Fact]
    public async Task NewSessionSelectionIsInitializedOnceAndIgnoredOnResume()
    {
        var executor = new RecordingExecutor();
        var initializer = new RecordingParticipantInitializer();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor,
            participantInitializer: initializer);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(
                IPAddress.Loopback,
                server.Port,
                cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(
                firstStream,
                cancellationToken,
                requestedClassId: 3,
                requestedCharacterName: "Dead-Monk");
            resumeToken = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.ServerHello.SessionToken;
            Assert.Equal(
                Envelope.PayloadOneofCase.Snapshot,
                (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);
        }

        using (var resumedClient = new TcpClient()) {
            await resumedClient.ConnectAsync(
                IPAddress.Loopback,
                server.Port,
                cancellationToken);
            await using var resumedStream = resumedClient.GetStream();
            await SendHelloAsync(
                resumedStream,
                cancellationToken,
                resumeToken,
                requestedClassId: 1,
                requestedCharacterName: "Ignored-Rogue");
            Assert.Equal(
                resumeToken,
                (await EnvelopeCodec.ReadAsync(
                    resumedStream,
                    cancellationToken))!.ServerHello.SessionToken);
            Assert.Equal(
                Envelope.PayloadOneofCase.Snapshot,
                (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.PayloadCase);
        }

        var initialized = Assert.Single(initializer.Selections);
        Assert.Equal(3U, initialized.Selection.ArchetypeId);
        Assert.Equal("Dead-Monk", initialized.Selection.CharacterName);
    }

    [Fact]
    public async Task SimulationLoopDoesNotSaveSessionBeforeParticipantInitializationCompletes()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Initialization-Race-" + Guid.NewGuid().ToString("N"));
        try {
            var executor = new RecordingExecutor();
            var initializer = new BlockingParticipantInitializer();
            var saveProvider = new RecordingSaveProvider();
            var commandServer = new AuthoritativeCommandServer(executor);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            ulong tick = 0;
            await using var server = new AuthoritativeTcpServer(
                commandServer,
                handshake,
                () => Interlocked.Increment(ref tick),
                snapshotProvider: executor,
                saveStore: new AuthoritativeSaveStore(saveRoot),
                saveProvider: saveProvider,
                participantInitializer: initializer);
            server.Start();
            var cancellationToken = TestContext.Current.CancellationToken;

            using var client = new TcpClient();
            await client.ConnectAsync(
                IPAddress.Loopback,
                server.Port,
                cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(stream, cancellationToken, requestedClassId: 2);
            await initializer.Entered.Task.WaitAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            Assert.Equal(0, saveProvider.ExportCount);

            initializer.Release();
            Assert.Equal(
                Envelope.PayloadOneofCase.ServerHello,
                (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
            Assert.Equal(
                Envelope.PayloadOneofCase.Snapshot,
                (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
            await WaitUntilAsync(
                () => saveProvider.ExportCount > 0,
                cancellationToken);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task FailedSaveImportIsRetriedInsteadOfCreatingFreshSessionState()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Invalid-Resume-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "invalid-save-resume-token";
        var sessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();
        try {
            var saveStore = new AuthoritativeSaveStore(saveRoot);
            saveStore.Save(sessionId, "invalid-save");
            var executor = new RecordingExecutor();
            var saveProvider = new RejectingSaveProvider();
            var initializer = new RecordingParticipantInitializer();
            var commandServer = new AuthoritativeCommandServer(executor);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            await using var server = new AuthoritativeTcpServer(
                commandServer,
                handshake,
                () => 10,
                saveStore: saveStore,
                saveProvider: saveProvider,
                participantInitializer: initializer);
            server.Start();
            var cancellationToken = TestContext.Current.CancellationToken;

            for (var attempt = 0; attempt < 2; attempt++) {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var stream = client.GetStream();
                await SendHelloAsync(stream, cancellationToken, resumeToken);
                var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
                Assert.NotNull(error);
                Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
                Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
            }

            Assert.Equal(2, saveProvider.ImportCount);
            Assert.Empty(initializer.Selections);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TcpServerRestartRestoresSavedSessionStateWithAFreshAdapter()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Restart-Recovery-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "restart-recovery-session";
        var sessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();
        uint initialEntityId;

        try {
            var initial = new ReloadableHostAdapter("initial");
            var initialRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
                CreateReloadManifest("1", "initial"),
                [new ReloadableModule("rules", "1.0.0")],
                new DelegateAuthoritativeClock(() => 10),
                (_, _) => initial);
            var initialAdapter = new LeasedAuthoritativeGameHostAdapter(initialRuntime);
            var initialHandshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            var saveStore = new AuthoritativeSaveStore(saveRoot);
            await using (var server = new AuthoritativeTcpServer(
                             new AuthoritativeCommandServer(initialAdapter),
                             initialHandshake,
                             new DelegateAuthoritativeClock(() => 10),
                             gameAdapter: initialAdapter,
                             saveStore: saveStore)) {
                server.Start();
                var cancellationToken = TestContext.Current.CancellationToken;
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var stream = client.GetStream();
                await SendHelloAsync(
                    stream,
                    cancellationToken,
                    resumeToken,
                    requestedCharacterName: "Persistent-Hero");
                var hello = (await EnvelopeCodec.ReadAsync(
                    stream,
                    cancellationToken))!.ServerHello;
                var initialSnapshot = (await EnvelopeCodec.ReadAsync(
                    stream,
                    cancellationToken))!.Snapshot;
                initialEntityId = Assert.Single(initialSnapshot.Players).EntityId;
                Assert.Equal("Persistent-Hero", Assert.Single(initialSnapshot.Players).CharacterName);

                await EnvelopeCodec.WriteAsync(stream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 1,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = initialEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(
                        stream,
                        cancellationToken))!.CommandAck.Results.Single().Status);
                Assert.Equal(
                    "Persistent-Hero",
                    Assert.Single((await EnvelopeCodec.ReadAsync(
                        stream,
                        cancellationToken))!.Snapshot.Players).CharacterName);
                Assert.NotNull(saveStore.Load(sessionId));
                Assert.Equal(resumeToken, hello.SessionToken);
            }

            var replacement = new ReloadableHostAdapter("fresh");
            var replacementRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
                CreateReloadManifest("1", "initial"),
                [new ReloadableModule("rules", "1.0.0")],
                new DelegateAuthoritativeClock(() => 10),
                (_, _) => replacement);
            var replacementAdapter = new LeasedAuthoritativeGameHostAdapter(replacementRuntime);
            var replacementHandshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            await using var restartedServer = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(replacementAdapter),
                replacementHandshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: replacementAdapter,
                saveStore: saveStore);
            restartedServer.Start();
            var restartCancellationToken = TestContext.Current.CancellationToken;
            using var resumedClient = new TcpClient();
            await resumedClient.ConnectAsync(
                IPAddress.Loopback,
                restartedServer.Port,
                restartCancellationToken);
            await using var resumedStream = resumedClient.GetStream();
            await SendHelloAsync(
                resumedStream,
                restartCancellationToken,
                resumeToken,
                requestedCharacterName: "Ignored-New-Character");
            var resumedHello = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                restartCancellationToken))!.ServerHello;
            var resumedSnapshot = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                restartCancellationToken))!.Snapshot;
            var resumedPlayer = Assert.Single(resumedSnapshot.Players);
            Assert.Equal(resumeToken, resumedHello.SessionToken);
            Assert.True(replacement.SessionStateImported);
            Assert.Equal(initialEntityId, resumedPlayer.EntityId);
            Assert.Equal("Persistent-Hero", resumedPlayer.CharacterName);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentConnectionsForOneTokenShareOneParticipantInitialization()
    {
        var executor = new RecordingExecutor();
        var initializer = new BlockingParticipantInitializer();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor,
            participantInitializer: initializer);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();
        const string resumeToken = "concurrent-session-token";
        await Task.WhenAll(
            SendHelloAsync(firstStream, cancellationToken, resumeToken, requestedClassId: 2).AsTask(),
            SendHelloAsync(secondStream, cancellationToken, resumeToken, requestedClassId: 5).AsTask());

        try {
            await initializer.Entered.Task.WaitAsync(cancellationToken);
            initializer.Release();
            var firstHello = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello;
            var secondHello = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.ServerHello;
            Assert.Equal(firstHello.SessionToken, secondHello.SessionToken);
            var firstSnapshot = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;
            var secondSnapshot = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
            Assert.Equal(
                Assert.Single(firstSnapshot.Players).EntityId,
                Assert.Single(secondSnapshot.Players).EntityId);
            Assert.Equal(1, initializer.CallCount);
        } finally {
            initializer.Release();
        }
    }

    [Fact]
    public async Task ResumeTokenPreservesSessionLedgerAndEntityIdAcrossReconnect()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        uint firstEntityId;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken);
            var firstHello = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello;
            resumeToken = firstHello.SessionToken;
            Assert.NotEmpty(resumeToken);
            var firstSnapshot = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
            firstEntityId = Assert.Single(firstSnapshot!.Snapshot.Players).EntityId;

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    } },
                },
            }, cancellationToken);
            var acknowledgement = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, acknowledgement!.CommandAck.Results.Single().Status);
            firstClient.Dispose();
        }

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        var secondHello = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.ServerHello;
        Assert.Equal(resumeToken, secondHello.SessionToken);
        var secondSnapshot = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        Assert.Equal(firstEntityId, Assert.Single(secondSnapshot!.Snapshot.Players).EntityId);

        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = { new Command {
                    ClientSequence = 1,
                    RequestedTick = 10,
                    AttackRequested = new AttackRequested { TargetEntityId = 1 },
                } },
            },
        }, cancellationToken);
        var duplicate = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);

        Assert.Equal(CommandStatus.Duplicate, duplicate!.CommandAck.Results.Single().Status);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task TcpRulesetReloadMigratesAnActiveSessionAndResumesWithTheReplacementIdentity()
    {
        var initial = new ReloadableHostAdapter("initial");
        var replacement = new ReloadableHostAdapter("replacement");
        var candidates = new Queue<ReloadableHostAdapter>([initial, replacement]);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => candidates.Dequeue());
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var commandServer = new AuthoritativeCommandServer(leasedAdapter);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            new DelegateAuthoritativeClock(() => 10),
            gameAdapter: leasedAdapter);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        uint entityId;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(
                firstStream,
                cancellationToken,
                requestedCharacterName: "Migrating-Hero");
            var initialHello = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.ServerHello;
            resumeToken = initialHello.SessionToken;
            var initialSnapshot = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.Snapshot;
            var initialPlayer = Assert.Single(initialSnapshot.Players);
            entityId = initialPlayer.EntityId;
            Assert.Equal("Migrating-Hero", initialPlayer.CharacterName);
            Assert.Contains(Assert.Single(server.ActiveSessionIds), server.ActiveSessionIds);

            var reload = server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]);

            Assert.True(reload.Succeeded);
            Assert.True(initial.Disposed);
            Assert.True(replacement.SessionStateImported);
            Assert.Equal(
                reload.Active.Identity.CombinedSha256,
                handshake.Identity.ContentManifestHash);
            Assert.Equal(
                reload.Active.Identity.CombinedSha256,
                handshake.Identity.RulesetIdentityHash);

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        AttackRequested = new AttackRequested { TargetEntityId = entityId },
                    } },
                },
            }, cancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.CommandAck.Results.Single().Status);
            var migratedSnapshot = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.Snapshot;
            Assert.Equal("Migrating-Hero", Assert.Single(migratedSnapshot.Players).CharacterName);
        }

        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await EnvelopeCodec.WriteAsync(resumedStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = handshake.Identity.ContentManifestHash,
                RulesetIdentityHash = handshake.Identity.RulesetIdentityHash,
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        var resumedHello = (await EnvelopeCodec.ReadAsync(
            resumedStream,
            cancellationToken))!.ServerHello;
        Assert.Equal(handshake.Identity.ContentManifestHash, resumedHello.ContentManifestHash);
        Assert.Equal(handshake.Identity.RulesetIdentityHash, resumedHello.RulesetIdentityHash);
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(
            resumedStream,
            cancellationToken))!.Snapshot;
        var resumedPlayer = Assert.Single(resumedSnapshot.Players);
        Assert.Equal(entityId, resumedPlayer.EntityId);
        Assert.Equal("Migrating-Hero", resumedPlayer.CharacterName);
    }

    [Fact]
    public async Task TcpRulesetReloadSoakKeepsAConnectedAndResumedSessionAcrossGenerations()
    {
        const int replacementGenerations = 32;
        var adapters = Enumerable.Range(0, replacementGenerations + 1)
            .Select(index => new ReloadableHostAdapter(
                index == 0 ? "initial" : $"replacement-{index}"))
            .ToArray();
        var nextAdapter = 0;
        var liveRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => adapters[nextAdapter++]);
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(liveRuntime);
        var commandServer = new AuthoritativeCommandServer(leasedAdapter);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            new DelegateAuthoritativeClock(() => 10),
            gameAdapter: leasedAdapter);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        uint entityId;

        using (var client = new TcpClient()) {
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(
                stream,
                cancellationToken,
                requestedCharacterName: "Soak-Hero");
            var initialHello = (await EnvelopeCodec.ReadAsync(
                stream,
                cancellationToken))!.ServerHello;
            resumeToken = initialHello.SessionToken;
            entityId = Assert.Single((await EnvelopeCodec.ReadAsync(
                stream,
                cancellationToken))!.Snapshot.Players).EntityId;

            for (var generation = 1; generation <= replacementGenerations; generation++) {
                var reload = server.TryReloadRuleset(
                    CreateReloadManifest(generation.ToString(), $"replacement-{generation}"),
                    [new ReloadableModule("rules", $"{generation + 1}.0.0")]);

                Assert.True(reload.Succeeded);
                Assert.True(adapters[generation - 1].Disposed);
                Assert.True(adapters[generation].SessionStateImported);
                Assert.Equal(
                    reload.Active.Identity.CombinedSha256,
                    handshake.Identity.RulesetIdentityHash);

                await EnvelopeCodec.WriteAsync(stream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = (ulong)generation,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = entityId,
                            },
                        } },
                    },
                }, cancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(
                        stream,
                        cancellationToken))!.CommandAck.Results.Single().Status);
                var snapshot = (await EnvelopeCodec.ReadAsync(
                    stream,
                    cancellationToken))!.Snapshot;
                var player = Assert.Single(snapshot.Players);
                Assert.Equal(entityId, player.EntityId);
                Assert.Equal("Soak-Hero", player.CharacterName);
            }
        }

        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await EnvelopeCodec.WriteAsync(resumedStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = handshake.Identity.ContentManifestHash,
                RulesetIdentityHash = handshake.Identity.RulesetIdentityHash,
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        var resumedHello = (await EnvelopeCodec.ReadAsync(
            resumedStream,
            cancellationToken))!.ServerHello;
        Assert.Equal(
            handshake.Identity.RulesetIdentityHash,
            resumedHello.RulesetIdentityHash);
        var resumedPlayer = Assert.Single((await EnvelopeCodec.ReadAsync(
            resumedStream,
            cancellationToken))!.Snapshot.Players);
        Assert.Equal(entityId, resumedPlayer.EntityId);
        Assert.Equal("Soak-Hero", resumedPlayer.CharacterName);
    }

    [Fact]
    public async Task TcpRulesetReloadSoakMigratesTwoConnectedSessionsAcrossGenerations()
    {
        const int replacementGenerations = 8;
        var adapters = Enumerable.Range(0, replacementGenerations + 1)
            .Select(index => new ReloadableHostAdapter(
                index == 0 ? "initial" : $"replacement-{index}"))
            .ToArray();
        var nextAdapter = 0;
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => adapters[nextAdapter++]);
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var commandServer = new AuthoritativeCommandServer(leasedAdapter);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            new DelegateAuthoritativeClock(() => 10),
            gameAdapter: leasedAdapter);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string firstResumeToken;
        string secondResumeToken;
        uint firstEntityId;
        uint secondEntityId;

        using (var firstClient = new TcpClient())
        using (var secondClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await using var secondStream = secondClient.GetStream();
            await SendHelloAsync(
                firstStream,
                cancellationToken,
                requestedCharacterName: "Soak-First");
            await SendHelloAsync(
                secondStream,
                cancellationToken,
                requestedCharacterName: "Soak-Second");

            firstResumeToken = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.ServerHello.SessionToken;
            secondResumeToken = (await EnvelopeCodec.ReadAsync(
                secondStream,
                cancellationToken))!.ServerHello.SessionToken;
            var initialFirstPlayer = Assert.Single((await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.Snapshot.Players);
            var initialSecondPlayer = Assert.Single((await EnvelopeCodec.ReadAsync(
                secondStream,
                cancellationToken))!.Snapshot.Players);
            firstEntityId = initialFirstPlayer.EntityId;
            secondEntityId = initialSecondPlayer.EntityId;
            Assert.Equal(
                ["Soak-First", "Soak-Second"],
                adapters[0].InitializedNames.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal("Soak-First", initialFirstPlayer.CharacterName);
            Assert.Equal("Soak-Second", initialSecondPlayer.CharacterName);
            Assert.Equal(2, server.ActiveSessionIds.Count);

            for (var generation = 1; generation <= replacementGenerations; generation++) {
                var reload = server.TryReloadRuleset(
                    CreateReloadManifest(generation.ToString(), $"replacement-{generation}"),
                    [new ReloadableModule("rules", $"{generation + 1}.0.0")]);

                Assert.True(reload.Succeeded);
                Assert.True(adapters[generation - 1].Disposed);
                Assert.True(adapters[generation].SessionStateImported);
                Assert.Equal(
                    reload.Active.Identity.CombinedSha256,
                    handshake.Identity.RulesetIdentityHash);

                await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = (ulong)generation,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = firstEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = (ulong)generation,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = secondEntityId,
                            },
                        } },
                    },
                }, cancellationToken);

                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(
                        firstStream,
                        cancellationToken))!.CommandAck.Results.Single().Status);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(
                        secondStream,
                        cancellationToken))!.CommandAck.Results.Single().Status);
                var firstSnapshot = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.Snapshot;
                var secondSnapshot = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.Snapshot;
                var firstPlayer = Assert.Single(firstSnapshot.Players);
                var secondPlayer = Assert.Single(secondSnapshot.Players);
                Assert.Equal(firstEntityId, firstPlayer.EntityId);
                Assert.Equal(secondEntityId, secondPlayer.EntityId);
                Assert.Equal("Soak-First", firstPlayer.CharacterName);
                Assert.Equal("Soak-Second", secondPlayer.CharacterName);
            }
        }

        using var resumedFirstClient = new TcpClient();
        using var resumedSecondClient = new TcpClient();
        await resumedFirstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await resumedSecondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedFirstStream = resumedFirstClient.GetStream();
        await using var resumedSecondStream = resumedSecondClient.GetStream();
        await EnvelopeCodec.WriteAsync(resumedFirstStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = handshake.Identity.ContentManifestHash,
                RulesetIdentityHash = handshake.Identity.RulesetIdentityHash,
                ResumeToken = firstResumeToken,
            },
        }, cancellationToken);
        await EnvelopeCodec.WriteAsync(resumedSecondStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = handshake.Identity.ContentManifestHash,
                RulesetIdentityHash = handshake.Identity.RulesetIdentityHash,
                ResumeToken = secondResumeToken,
            },
        }, cancellationToken);
        var resumedFirstHello = (await EnvelopeCodec.ReadAsync(
            resumedFirstStream,
            cancellationToken))!.ServerHello;
        var resumedSecondHello = (await EnvelopeCodec.ReadAsync(
            resumedSecondStream,
            cancellationToken))!.ServerHello;
        Assert.Equal(handshake.Identity.RulesetIdentityHash, resumedFirstHello.RulesetIdentityHash);
        Assert.Equal(handshake.Identity.RulesetIdentityHash, resumedSecondHello.RulesetIdentityHash);
        var resumedFirstPlayer = Assert.Single((await EnvelopeCodec.ReadAsync(
            resumedFirstStream,
            cancellationToken))!.Snapshot.Players);
        var resumedSecondPlayer = Assert.Single((await EnvelopeCodec.ReadAsync(
            resumedSecondStream,
            cancellationToken))!.Snapshot.Players);
        Assert.Equal(firstEntityId, resumedFirstPlayer.EntityId);
        Assert.Equal(secondEntityId, resumedSecondPlayer.EntityId);
        Assert.Equal("Soak-First", resumedFirstPlayer.CharacterName);
        Assert.Equal("Soak-Second", resumedSecondPlayer.CharacterName);
    }

    [Fact]
    public async Task ReconnectSoakPreservesCompleteParticipantSnapshotAndSelfFirstOrder()
    {
        var executor = new ParticipantSnapshotExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "content-hash",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        uint firstEntityId;
        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(secondStream, cancellationToken);
        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.PayloadCase);
        var secondInitialSnapshot = (await EnvelopeCodec.ReadAsync(
            secondStream,
            cancellationToken))!.Snapshot;
        var secondEntityId = Assert.Single(secondInitialSnapshot.Players).EntityId;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken);
            var firstHello = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.ServerHello;
            resumeToken = firstHello.SessionToken;
            firstEntityId = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.Snapshot.Players[0].EntityId;
            Assert.NotEqual(firstEntityId, secondEntityId);
        }

        const int reconnectCycles = 12;
        for (var attempt = 0; attempt < reconnectCycles; attempt++) {
            using var resumedClient = new TcpClient();
            await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var resumedStream = resumedClient.GetStream();
            await SendHelloAsync(resumedStream, cancellationToken, resumeToken);
            var resumedHello = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                cancellationToken))!.ServerHello;
            Assert.Equal(resumeToken, resumedHello.SessionToken);
            var resumedSnapshot = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                cancellationToken))!.Snapshot;
            Assert.Equal(
                [firstEntityId, secondEntityId],
                resumedSnapshot.Players.Select(player => player.EntityId));
            Assert.Equal(
                SnapshotStateHasher.Compute(resumedSnapshot),
                resumedSnapshot.StateSha256);
        }
    }

    [Fact]
    public async Task ResumeTokenRestoresPurchasedStoreStateAndDuplicatePurchaseDoesNotMutateIt()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
            new StoreItem(1, 43, 25, RemainingStockState()),
        });
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: [-1, -1]);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        Snapshot purchasedSnapshot;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken);
            resumeToken = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello.SessionToken;
            Assert.NotEmpty(resumeToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
                    } },
                },
            }, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.CommandAck.Results.Single().Status);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 2,
                        RequestedTick = 10,
                        PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                    } },
                },
            }, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.CommandAck.Results.Single().Status);
            purchasedSnapshot = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;
        }

        var purchasedPlayer = Assert.Single(purchasedSnapshot.Players);
        Assert.Equal(25U, purchasedPlayer.Gold);
        Assert.Equal(1U, purchasedPlayer.ActiveStoreId);
        Assert.Single(purchasedPlayer.Inventory);
        Assert.Equal(SnapshotStateHasher.Compute(purchasedSnapshot), purchasedSnapshot.StateSha256);
        AssertRemainingStoreStock(purchasedSnapshot);

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        Assert.Equal(resumeToken, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.ServerHello.SessionToken);
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
        AssertMatchingStoreSnapshot(purchasedSnapshot, resumedSnapshot);

        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = { new Command {
                    ClientSequence = 2,
                    RequestedTick = 10,
                    PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                } },
            },
        }, cancellationToken);
        Assert.Equal(CommandStatus.Duplicate, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.CommandAck.Results.Single().Status);
        var duplicateSnapshot = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
        AssertMatchingStoreSnapshot(purchasedSnapshot, duplicateSnapshot);
    }

    private static void AssertMatchingStoreSnapshot(Snapshot expected, Snapshot actual)
    {
        Assert.Equal(expected.StateSha256, actual.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(actual), actual.StateSha256);

        var expectedPlayer = Assert.Single(expected.Players);
        var actualPlayer = Assert.Single(actual.Players);
        Assert.Equal(expectedPlayer.EntityId, actualPlayer.EntityId);
        Assert.Equal(expectedPlayer.Gold, actualPlayer.Gold);
        Assert.Equal(expectedPlayer.ActiveStoreId, actualPlayer.ActiveStoreId);
        Assert.Equal(expectedPlayer.Inventory.Select(item => item.ItemSeed), actualPlayer.Inventory.Select(item => item.ItemSeed));
        AssertRemainingStoreStock(expected);
        AssertRemainingStoreStock(actual);
    }

    private static void AssertRemainingStoreStock(Snapshot snapshot)
    {
        var store = Assert.IsType<StoreSnapshot>(snapshot.ActiveStore);
        Assert.Equal(1U, store.StoreId);
        var item = Assert.Single(store.Items);
        Assert.Equal(1U, item.StoreSlot);
        Assert.Equal(43U, item.ItemSeed);
        Assert.Equal(25U, item.Price);
        Assert.Equal(new ItemStateSnapshot {
            CreateInfo = 43,
            ItemType = 2,
            PositionX = 3,
            PositionY = -4,
            Identified = true,
            Value = 50,
            MaxDamage = 7,
            Flags = 9,
            ItemIndex = 8,
            Durability = 2,
            MaxDurability = 10,
            PlusDamage = 11,
            PrefixPower = 12,
            SuffixPower = 13,
            Buff = 14,
        }, item.State);
    }

    private static AuthoritativeItemState RemainingStockState()
    {
        return AuthoritativeItemState.Empty with {
            CreateInfo = 43,
            ItemType = 2,
            PositionX = 3,
            PositionY = -4,
            Identified = true,
            Value = 50,
            MaxDamage = 7,
            Flags = 9,
            ItemIndex = 8,
            Durability = 2,
            MaxDurability = 10,
            PlusDamage = 11,
            PrefixPower = 12,
            SuffixPower = 13,
            Buff = 14,
        };
    }

    private static ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string resumeToken = "",
        uint? requestedClassId = null,
        string? requestedCharacterName = null)
    {
        var hello = new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            ResumeToken = resumeToken,
        };
        if (requestedClassId.HasValue)
            hello.RequestedCharacterClassId = requestedClassId.Value;
        if (requestedCharacterName is not null)
            hello.RequestedCharacterName = requestedCharacterName;
        return EnvelopeCodec.WriteAsync(
            stream,
            new Envelope { ClientHello = hello },
            cancellationToken);
    }

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

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++) {
            if (predicate())
                return;
            await Task.Delay(10, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the server condition.");
    }

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
    {
        public int CallCount { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
        }
    }

    private sealed class ReloadableHostAdapter(string name)
        : IAuthoritativeGameHostAdapter, IDisposable
    {
        private readonly Dictionary<string, string> sessionStates = new(StringComparer.Ordinal);
        private readonly object initializationSynchronization = new();

        public bool Disposed { get; private set; }

        public bool SessionStateImported { get; private set; }

        public List<string> InitializedNames { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick)
        {
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

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        {
            return null;
        }

        public string ExportPlayerSave(string sessionId)
        {
            return ExportSessionState(sessionId);
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            ImportSessionState(sessionId, serializedSave);
        }

        public string ExportSessionState(string sessionId)
        {
            lock (initializationSynchronization) {
                return sessionStates.TryGetValue(sessionId, out var state)
                    ? state
                    : name;
            }
        }

        public void ImportSessionState(string sessionId, string serializedState)
        {
            lock (initializationSynchronization)
                sessionStates[sessionId] = serializedState;
            SessionStateImported = true;
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            var characterName = selection.CharacterName ?? name;
            lock (initializationSynchronization) {
                InitializedNames.Add(characterName);
                sessionStates.TryAdd(sessionId, characterName);
            }
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class ReloadableModule(string id, string version) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } =
            new(id, version, $"hash-{version}");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }

    private sealed class ParticipantSnapshotExecutor
        : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
    {
        private readonly ConcurrentDictionary<uint, byte> participants = new();

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            participants.TryAdd(entityId, 0);
            var snapshot = new Snapshot { Tick = tick };
            snapshot.Players.Add(new PlayerSnapshot { EntityId = entityId });
            foreach (var participant in participants.Keys
                         .Where(participant => participant != entityId)
                         .OrderBy(participant => participant)) {
                snapshot.Players.Add(new PlayerSnapshot { EntityId = participant });
            }
            snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
            return snapshot;
        }
    }

    private sealed class RecordingParticipantInitializer
        : IAuthoritativeParticipantInitializer
    {
        public List<(string SessionId, AuthoritativeParticipantSelection Selection)>
            Selections { get; } = [];

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            Selections.Add((sessionId, selection));
        }
    }

    private sealed class BlockingParticipantInitializer
        : IAuthoritativeParticipantInitializer
    {
        private readonly ManualResetEventSlim release = new(initialState: false);

        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            CallCount++;
            Entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The participant initializer was not released.");
        }

        public void Release()
        {
            release.Set();
        }
    }

    private sealed class RecordingSaveProvider : IAuthoritativeSaveProvider
    {
        private int exportCount;

        public int ExportCount => Volatile.Read(ref exportCount);

        public string ExportPlayerSave(string sessionId)
        {
            Interlocked.Increment(ref exportCount);
            return "{}";
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }
    }

    private sealed class RejectingSaveProvider : IAuthoritativeSaveProvider
    {
        public int ImportCount { get; private set; }

        public string ExportPlayerSave(string sessionId) => "{}";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            ImportCount++;
            throw new InvalidDataException("The save is invalid.");
        }
    }
}
