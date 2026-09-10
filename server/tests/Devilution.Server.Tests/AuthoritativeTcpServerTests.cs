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
    public async Task FutureAppliedTickIsRetainedAcrossCommandAndSnapshotPoll()
    {
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
            snapshotProvider: executor);
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken);
        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        var initialSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(10UL, initialSnapshot!.Snapshot.Tick);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 15,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);

        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement!.PayloadCase);
        Assert.Equal(15UL, acknowledgement.CommandAck.Results.Single().AppliedTick);
        Assert.Equal(15UL, Assert.Single(executor.AppliedTicks));

        var commandSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, commandSnapshot!.PayloadCase);
        Assert.Equal(15UL, commandSnapshot.Snapshot.Tick);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest(),
        }, cancellationToken);
        var polledSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, polledSnapshot!.PayloadCase);
        Assert.Equal(15UL, polledSnapshot.Snapshot.Tick);
        Assert.Equal([10UL, 15UL, 15UL], executor.SnapshotTicks);
    }

    [Fact]
    public async Task FutureAppliedTickIsVisibleToOtherConnectedSession()
    {
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
            snapshotProvider: executor);
        server.Start();

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(firstStream, cancellationToken);
        await SendHelloAsync(secondStream, cancellationToken);
        await ReadHandshakeSnapshotAsync(firstStream, 10, cancellationToken);
        await ReadHandshakeSnapshotAsync(secondStream, 10, cancellationToken);

        await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 15,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var acknowledgement = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        Assert.Equal(15UL, acknowledgement!.CommandAck.Results.Single().AppliedTick);
        var firstSnapshot = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        Assert.Equal(15UL, firstSnapshot!.Snapshot.Tick);

        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest(),
        }, cancellationToken);
        var secondSnapshot = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        Assert.Equal(15UL, secondSnapshot!.Snapshot.Tick);
        Assert.Equal([10UL, 10UL, 15UL, 15UL], executor.SnapshotTicks);
    }

    [Fact]
    public async Task OversizedCommandBatchIsRejectedBeforeExecution()
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
        await SendHelloAsync(stream, cancellationToken);
        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);

        var batch = new CommandBatch();
        for (var index = 0; index <= AuthoritativeCommandBatchValidator.MaximumCommandBatchCount; index++)
            batch.Commands.Add(new Command {
                ClientSequence = (ulong)(index + 1),
                RequestedTick = 10,
                RestartTownRequested = new RestartTownRequested(),
            });
        await EnvelopeCodec.WriteAsync(stream, new Envelope { CommandBatch = batch }, cancellationToken);

        var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(error);
        Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
        Assert.Contains("command batch is too large", error.Error.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task MalformedEventsFromAdapterAreRejectedBeforeWireSerialization()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor,
            eventProvider: new MalformedEventProvider());
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken);

        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        Assert.Equal(
            Envelope.PayloadOneofCase.Snapshot,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);

        Assert.Equal(
            Envelope.PayloadOneofCase.CommandAck,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(error);
        Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
        Assert.Contains("unknown event kind", error.Error.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaleEventBatchTickIsRejectedBeforeWireSerialization()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor,
            eventProvider: new StaleEventProvider());
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken);

        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        Assert.Equal(
            Envelope.PayloadOneofCase.Snapshot,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);

        Assert.Equal(
            Envelope.PayloadOneofCase.CommandAck,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(error);
        Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
        Assert.Contains("does not match the applied tick", error.Error.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedSnapshotsFromAdapterAreRejectedBeforeWireSerialization()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: new MalformedSnapshotProvider());
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await SendHelloAsync(stream, cancellationToken);

        Assert.Equal(
            Envelope.PayloadOneofCase.ServerHello,
            (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.PayloadCase);
        var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(error);
        Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
        Assert.Contains("invalid SHA-256 state hash", error.Error.Detail, StringComparison.OrdinalIgnoreCase);
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
            startingInventoryGrid: []);
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
        Assert.Empty(initialPlayer.InventoryGrid);
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
        Assert.Equal(handshake.Identity.RulesetIdentityHash, server.ActiveRulesetIdentityHash);
        Assert.Equal(0, server.RetiredRulesetCount);
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
    public async Task TcpServerRestartRestoresSavedSessionStateAcrossFreshAdapters()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Restart-Recovery-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "restart-recovery-session";
        var sessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();
        uint initialEntityId;
        string expectedStateHash;
        ulong acceptedCommandCount = 0;

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
                expectedStateHash = initialSnapshot.StateSha256;
                Assert.Equal(
                    SnapshotStateHasher.Compute(initialSnapshot),
                    expectedStateHash);

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
                var initialAcknowledgement = (await EnvelopeCodec.ReadAsync(
                    stream,
                    cancellationToken))!.CommandAck.Results.Single();
                Assert.Equal(CommandStatus.Accepted, initialAcknowledgement.Status);
                Assert.Equal(1UL, initialAcknowledgement.ServerReceiptSequence);
                acceptedCommandCount = 1;
                Assert.Equal(
                    "Persistent-Hero",
                    Assert.Single((await EnvelopeCodec.ReadAsync(
                        stream,
                        cancellationToken))!.Snapshot.Players).CharacterName);
                Assert.NotNull(saveStore.Load(sessionId));
                Assert.Equal(resumeToken, hello.SessionToken);
            }

            Assert.NotNull(new AuthoritativeSessionStore(saveRoot).Load(sessionId));
            File.WriteAllText(Path.Combine(saveRoot, sessionId + ".json"), "stale-save");
            File.WriteAllText(Path.Combine(saveRoot, sessionId + ".commands.json"), "malformed-receipts");

            const int freshAdapterRestarts = 4;
            for (var restart = 1; restart <= freshAdapterRestarts; restart++) {
                var replacement = new ReloadableHostAdapter($"fresh-{restart}");
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
                await using (var restartedServer = new AuthoritativeTcpServer(
                                 new AuthoritativeCommandServer(replacementAdapter),
                                 replacementHandshake,
                                 new DelegateAuthoritativeClock(() => 10),
                                 gameAdapter: replacementAdapter,
                                 saveStore: saveStore)) {
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
                    Assert.Equal(expectedStateHash, resumedSnapshot.StateSha256);
                    Assert.Equal(
                        SnapshotStateHasher.Compute(resumedSnapshot),
                        resumedSnapshot.StateSha256);

                    for (ulong replaySequence = 1; replaySequence <= acceptedCommandCount; replaySequence++) {
                        await EnvelopeCodec.WriteAsync(resumedStream, new Envelope {
                            CommandBatch = new CommandBatch {
                                Commands = { new Command {
                                    ClientSequence = replaySequence,
                                    RequestedTick = 10,
                                    AttackRequested = new AttackRequested {
                                        TargetEntityId = initialEntityId,
                                    },
                                } },
                            },
                        }, restartCancellationToken);
                        var duplicate = (await EnvelopeCodec.ReadAsync(
                            resumedStream,
                            restartCancellationToken))!.CommandAck.Results.Single();
                        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
                        Assert.Equal(replaySequence, duplicate.ClientSequence);
                        Assert.Equal(replaySequence, duplicate.ServerReceiptSequence);
                        await EnvelopeCodec.ReadAsync(resumedStream, restartCancellationToken);
                    }

                    if (restart < freshAdapterRestarts) {
                        var nextSequence = acceptedCommandCount + 1;
                        await EnvelopeCodec.WriteAsync(resumedStream, new Envelope {
                            CommandBatch = new CommandBatch {
                                Commands = { new Command {
                                    ClientSequence = nextSequence,
                                    RequestedTick = 10,
                                    AttackRequested = new AttackRequested {
                                        TargetEntityId = initialEntityId,
                                    },
                                } },
                            },
                        }, restartCancellationToken);
                        var accepted = (await EnvelopeCodec.ReadAsync(
                            resumedStream,
                            restartCancellationToken))!.CommandAck.Results.Single();
                        Assert.Equal(CommandStatus.Accepted, accepted.Status);
                        Assert.Equal(nextSequence, accepted.ClientSequence);
                        Assert.Equal(nextSequence, accepted.ServerReceiptSequence);
                        await EnvelopeCodec.ReadAsync(resumedStream, restartCancellationToken);
                        acceptedCommandCount = nextSequence;
                        Assert.Equal(1, replacement.ExecuteCount);
                    } else {
                        Assert.Equal(0, replacement.ExecuteCount);
                    }
                }

                Assert.True(replacement.Disposed);
            }
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TcpServerRejectsCurrentCheckpointFromAnotherRulesetBeforeImport()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Ruleset-Checkpoint-Rejection-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "ruleset-checkpoint-session";
        var sessionId = SessionIdForToken(resumeToken);
        try {
            new AuthoritativeSessionStore(saveRoot).Save(
                sessionId,
                "old-state",
                new Dictionary<ulong, CommandResult>(),
                "old-ruleset");

            var adapter = new ReloadableHostAdapter("fresh");
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20) {
                RulesetIdentityHash = "new-ruleset",
            });
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(adapter),
                handshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: adapter,
                saveStore: new AuthoritativeSaveStore(saveRoot));
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
                    ResumeToken = resumeToken,
                },
            }, cancellationToken);

            var error = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            Assert.NotNull(error);
            Assert.Equal(Envelope.PayloadOneofCase.Error, error!.PayloadCase);
            Assert.Equal(ProtocolErrorCode.InvalidMessage, error.Error.Code);
            Assert.Contains("ruleset identity", error.Error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.False(adapter.SessionStateImported);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TcpServerRestartRestoresTwoParticipantSessionsAndReceiptLedgers()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Two-Participant-Restart-" + Guid.NewGuid().ToString("N"));
        const string firstResumeToken = "two-participant-first";
        const string secondResumeToken = "two-participant-second";
        var firstSessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(firstResumeToken)))
            .ToLowerInvariant();
        var secondSessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(secondResumeToken)))
            .ToLowerInvariant();
        uint firstEntityId;
        uint secondEntityId;

        try {
            var initial = new MultiParticipantReloadableHostAdapter("initial");
            var initialRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
                CreateReloadManifest("1", "initial"),
                [new ReloadableModule("rules", "1.0.0")],
                new DelegateAuthoritativeClock(() => 10),
                (_, _) => initial);
            var initialAdapter = new LeasedAuthoritativeGameHostAdapter(initialRuntime);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            var saveStore = new AuthoritativeSaveStore(saveRoot);
            await using (var server = new AuthoritativeTcpServer(
                             new AuthoritativeCommandServer(initialAdapter),
                             handshake,
                             new DelegateAuthoritativeClock(() => 10),
                             gameAdapter: initialAdapter,
                             saveStore: saveStore)) {
                server.Start();
                var cancellationToken = TestContext.Current.CancellationToken;

                using var firstClient = new TcpClient();
                await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var firstStream = firstClient.GetStream();
                await SendHelloAsync(
                    firstStream,
                    cancellationToken,
                    firstResumeToken,
                    requestedCharacterName: "First-Participant");
                var firstHello = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.ServerHello;
                var firstSnapshot = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.Snapshot;
                firstEntityId = Assert.Single(firstSnapshot.Players).EntityId;
                Assert.Equal("First-Participant", firstSnapshot.Players[0].CharacterName);

                using var secondClient = new TcpClient();
                await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var secondStream = secondClient.GetStream();
                await SendHelloAsync(
                    secondStream,
                    cancellationToken,
                    secondResumeToken,
                    requestedCharacterName: "Second-Participant");
                var secondHello = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.ServerHello;
                var secondSnapshot = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.Snapshot;
                secondEntityId = secondSnapshot.Players[0].EntityId;
                Assert.NotEqual(firstEntityId, secondEntityId);
                Assert.Equal(
                    [secondEntityId, firstEntityId],
                    secondSnapshot.Players.Select(player => player.EntityId));
                Assert.Equal(firstResumeToken, firstHello.SessionToken);
                Assert.Equal(secondResumeToken, secondHello.SessionToken);

                await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 1,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = secondEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                var firstAcknowledgement = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.CommandAck.Results.Single();
                Assert.Equal(CommandStatus.Accepted, firstAcknowledgement.Status);
                Assert.Equal(1UL, firstAcknowledgement.ServerReceiptSequence);
                await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);

                await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 1,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = firstEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                var secondAcknowledgement = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.CommandAck.Results.Single();
                Assert.Equal(CommandStatus.Accepted, secondAcknowledgement.Status);
                Assert.Equal(2UL, secondAcknowledgement.ServerReceiptSequence);
                await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
            }

            Assert.NotNull(new AuthoritativeSessionStore(saveRoot).Load(firstSessionId));
            Assert.NotNull(new AuthoritativeSessionStore(saveRoot).Load(secondSessionId));

            var replacement = new MultiParticipantReloadableHostAdapter("replacement");
            var replacementRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
                CreateReloadManifest("1", "initial"),
                [new ReloadableModule("rules", "1.0.0")],
                new DelegateAuthoritativeClock(() => 10),
                (_, _) => replacement);
            var replacementAdapter = new LeasedAuthoritativeGameHostAdapter(replacementRuntime);
            await using (var restartedServer = new AuthoritativeTcpServer(
                             new AuthoritativeCommandServer(replacementAdapter),
                             handshake,
                             new DelegateAuthoritativeClock(() => 10),
                             gameAdapter: replacementAdapter,
                             saveStore: saveStore)) {
                restartedServer.Start();
                var cancellationToken = TestContext.Current.CancellationToken;
                using var resumedFirstClient = new TcpClient();
                await resumedFirstClient.ConnectAsync(
                    IPAddress.Loopback,
                    restartedServer.Port,
                    cancellationToken);
                await using var resumedFirstStream = resumedFirstClient.GetStream();
                await SendHelloAsync(resumedFirstStream, cancellationToken, firstResumeToken);
                var resumedFirstHello = (await EnvelopeCodec.ReadAsync(
                    resumedFirstStream,
                    cancellationToken))!.ServerHello;
                var resumedFirstSnapshot = (await EnvelopeCodec.ReadAsync(
                    resumedFirstStream,
                    cancellationToken))!.Snapshot;

                using var resumedSecondClient = new TcpClient();
                await resumedSecondClient.ConnectAsync(
                    IPAddress.Loopback,
                    restartedServer.Port,
                    cancellationToken);
                await using var resumedSecondStream = resumedSecondClient.GetStream();
                await SendHelloAsync(resumedSecondStream, cancellationToken, secondResumeToken);
                var resumedSecondHello = (await EnvelopeCodec.ReadAsync(
                    resumedSecondStream,
                    cancellationToken))!.ServerHello;
                var resumedSecondSnapshot = (await EnvelopeCodec.ReadAsync(
                    resumedSecondStream,
                    cancellationToken))!.Snapshot;
                Assert.Equal(firstResumeToken, resumedFirstHello.SessionToken);
                Assert.Equal(secondResumeToken, resumedSecondHello.SessionToken);
                Assert.Equal(firstEntityId, Assert.Single(resumedFirstSnapshot.Players).EntityId);
                Assert.Equal(secondEntityId, resumedSecondSnapshot.Players[0].EntityId);
                Assert.Equal(
                    [secondEntityId, firstEntityId],
                    resumedSecondSnapshot.Players.Select(player => player.EntityId));
                Assert.Equal("First-Participant", resumedFirstSnapshot.Players[0].CharacterName);
                Assert.Equal("Second-Participant", resumedSecondSnapshot.Players[0].CharacterName);
                Assert.True(replacement.SessionStateImported);
                Assert.Equal(2, replacement.ImportCount);

                await EnvelopeCodec.WriteAsync(resumedFirstStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 1,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = secondEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                var duplicate = (await EnvelopeCodec.ReadAsync(
                    resumedFirstStream,
                    cancellationToken))!.CommandAck.Results.Single();
                Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
                Assert.Equal(1UL, duplicate.ServerReceiptSequence);
                await EnvelopeCodec.ReadAsync(resumedFirstStream, cancellationToken);

                await EnvelopeCodec.WriteAsync(resumedSecondStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 2,
                            RequestedTick = 10,
                            AttackRequested = new AttackRequested {
                                TargetEntityId = firstEntityId,
                            },
                        } },
                    },
                }, cancellationToken);
                var accepted = (await EnvelopeCodec.ReadAsync(
                    resumedSecondStream,
                    cancellationToken))!.CommandAck.Results.Single();
                Assert.Equal(CommandStatus.Accepted, accepted.Status);
                Assert.Equal(3UL, accepted.ServerReceiptSequence);
                await EnvelopeCodec.ReadAsync(resumedSecondStream, cancellationToken);
            }
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
            Assert.Equal(handshake.Identity.ContentManifestHash, initialSnapshot.ContentManifestHash);
            Assert.Equal(handshake.Identity.RulesetIdentityHash, initialSnapshot.RulesetIdentityHash);
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
            Assert.Equal(handshake.Identity.RulesetIdentityHash, server.ActiveRulesetIdentityHash);
            Assert.Equal(0, server.RetiredRulesetCount);

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
            Assert.Equal(handshake.Identity.ContentManifestHash, migratedSnapshot.ContentManifestHash);
            Assert.Equal(handshake.Identity.RulesetIdentityHash, migratedSnapshot.RulesetIdentityHash);
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
        Assert.Equal(handshake.Identity.ContentManifestHash, resumedSnapshot.ContentManifestHash);
        Assert.Equal(handshake.Identity.RulesetIdentityHash, resumedSnapshot.RulesetIdentityHash);
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
    public async Task TcpRulesetReloadPersistsMigratedSessionsBeforePublishingIdentity()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Ruleset-Reload-Persistence-" + Guid.NewGuid().ToString("N"));
        const string resumeToken = "ruleset-reload-persistence-session";
        var initialManifest = CreateReloadManifest("1", "initial");
        var replacementManifest = CreateReloadManifest("2", "replacement");
        var initialAdapter = new ReloadableHostAdapter("initial");
        var initialRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            initialManifest,
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => initialAdapter);
        var initialIdentity = initialRuntime.Active.Identity.CombinedSha256;

        try {
            using (var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(initialRuntime)) {
                var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                    "server-build",
                    "0.1.0",
                    "content-hash",
                    20) {
                    RulesetIdentityHash = initialIdentity,
                });
                await using (var server = new AuthoritativeTcpServer(
                                 new AuthoritativeCommandServer(leasedAdapter),
                                 handshake,
                                 new DelegateAuthoritativeClock(() => 10),
                                 gameAdapter: leasedAdapter,
                                 saveStore: new AuthoritativeSaveStore(saveRoot))) {
                    server.Start();
                    var cancellationToken = TestContext.Current.CancellationToken;
                    using (var client = new TcpClient()) {
                        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                        await using var stream = client.GetStream();
                        await SendHelloAsync(
                            stream,
                            cancellationToken,
                            resumeToken: resumeToken,
                            requestedCharacterName: "Reload-Hero");
                        var hello = (await EnvelopeCodec.ReadAsync(
                            stream,
                            cancellationToken))!.ServerHello;
                        Assert.Equal(initialIdentity, hello.RulesetIdentityHash);
                        var snapshot = (await EnvelopeCodec.ReadAsync(
                            stream,
                            cancellationToken))!.Snapshot;
                        Assert.Equal("Reload-Hero", Assert.Single(snapshot.Players).CharacterName);
                    }

                    var reload = server.TryReloadRuleset(
                        replacementManifest,
                        [new ReloadableModule("rules", "2.0.0")]);

                    Assert.True(reload.Succeeded);
                    var persisted = new AuthoritativeSessionStore(saveRoot).Load(
                        SessionIdForToken(resumeToken),
                        reload.Active.Identity.CombinedSha256);
                    Assert.NotNull(persisted);
                    Assert.Equal(reload.Active.Identity.CombinedSha256, persisted.RulesetIdentityHash);
                    Assert.Equal("Reload-Hero", persisted.SerializedState);
                }
            }

            var replacementAdapter = new ReloadableHostAdapter("replacement");
            var replacementRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
                replacementManifest,
                [new ReloadableModule("rules", "2.0.0")],
                new DelegateAuthoritativeClock(() => 10),
                (_, _) => replacementAdapter);
            using var replacementLeasedAdapter = new LeasedAuthoritativeGameHostAdapter(replacementRuntime);
            var replacementHandshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20) {
                RulesetIdentityHash = replacementRuntime.Active.Identity.CombinedSha256,
            });
            await using var replacementServer = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(replacementLeasedAdapter),
                replacementHandshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: replacementLeasedAdapter,
                saveStore: new AuthoritativeSaveStore(saveRoot));
            replacementServer.Start();

            using var resumedClient = new TcpClient();
            var resumedCancellationToken = TestContext.Current.CancellationToken;
            await resumedClient.ConnectAsync(
                IPAddress.Loopback,
                replacementServer.Port,
                resumedCancellationToken);
            await using var resumedStream = resumedClient.GetStream();
            await SendHelloAsync(
                resumedStream,
                resumedCancellationToken,
                resumeToken: resumeToken);
            var resumedHello = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                resumedCancellationToken))!.ServerHello;
            Assert.Equal(
                replacementRuntime.Active.Identity.CombinedSha256,
                resumedHello.RulesetIdentityHash);
            var resumedSnapshot = (await EnvelopeCodec.ReadAsync(
                resumedStream,
                resumedCancellationToken))!.Snapshot;
            Assert.Equal("Reload-Hero", Assert.Single(resumedSnapshot.Players).CharacterName);
            Assert.True(replacementAdapter.SessionStateImported);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TcpRulesetReloadCheckpointFailureKeepsPreviousGenerationAndIdentity()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Ruleset-Reload-Failure-" + Guid.NewGuid().ToString("N"));
        var initial = new ReloadableHostAdapter("initial");
        var replacement = new ReloadableHostAdapter("replacement") {
            ThrowOnExport = true,
        };
        var candidates = new Queue<ReloadableHostAdapter>([initial, replacement]);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => candidates.Dequeue());
        var initialIdentity = runtime.Active.Identity.CombinedSha256;

        try {
            using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20) {
                RulesetIdentityHash = initialIdentity,
            });
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                handshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: leasedAdapter,
                saveStore: new AuthoritativeSaveStore(saveRoot));
            server.Start();
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var stream = client.GetStream();
            await SendHelloAsync(
                stream,
                cancellationToken,
                requestedCharacterName: "Reload-Failure-Hero");
            var hello = (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.ServerHello;
            var snapshot = (await EnvelopeCodec.ReadAsync(stream, cancellationToken))!.Snapshot;
            Assert.Equal(initialIdentity, hello.RulesetIdentityHash);
            Assert.Equal("Reload-Failure-Hero", Assert.Single(snapshot.Players).CharacterName);

            var reload = server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]);

            Assert.False(reload.Succeeded);
            Assert.Equal(
                "ruleset-target-preparation-failed",
                Assert.Single(reload.Diagnostics).Code);
            Assert.Same(initial, runtime.Active.Target);
            Assert.Equal(initialIdentity, server.ActiveRulesetIdentityHash);
            Assert.False(initial.Disposed);
            Assert.True(replacement.Disposed);
            Assert.Equal(0, server.RetiredRulesetCount);
            var persisted = new AuthoritativeSessionStore(saveRoot).Load(
                SessionIdForToken(hello.SessionToken),
                initialIdentity);
            Assert.NotNull(persisted);
            Assert.Equal(initialIdentity, persisted.RulesetIdentityHash);
            Assert.Equal("Reload-Failure-Hero", persisted.SerializedState);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TcpRulesetReloadPreparesAllSessionCheckpointsBeforeWriting()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Ruleset-Reload-Batch-" + Guid.NewGuid().ToString("N"));
        var initial = new ReloadableHostAdapter("initial");
        var replacement = new ReloadableHostAdapter("replacement");
        var candidates = new Queue<ReloadableHostAdapter>([initial, replacement]);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateReloadManifest("1", "initial"),
            [new ReloadableModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 10),
            (_, _) => candidates.Dequeue());
        var initialIdentity = runtime.Active.Identity.CombinedSha256;

        try {
            using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20) {
                RulesetIdentityHash = initialIdentity,
            });
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(leasedAdapter),
                handshake,
                new DelegateAuthoritativeClock(() => 10),
                gameAdapter: leasedAdapter,
                saveStore: new AuthoritativeSaveStore(saveRoot));
            server.Start();
            var cancellationToken = TestContext.Current.CancellationToken;
            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(
                firstStream,
                cancellationToken,
                requestedCharacterName: "Batch-First");
            var firstHello = (await EnvelopeCodec.ReadAsync(
                firstStream,
                cancellationToken))!.ServerHello;
            _ = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;

            using var secondClient = new TcpClient();
            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var secondStream = secondClient.GetStream();
            await SendHelloAsync(
                secondStream,
                cancellationToken,
                requestedCharacterName: "Batch-Second");
            var secondHello = (await EnvelopeCodec.ReadAsync(
                secondStream,
                cancellationToken))!.ServerHello;
            _ = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;

            var failingSessionId = server.ActiveSessionIds
                .OrderBy(sessionId => sessionId, StringComparer.Ordinal)
                .Last();
            replacement.ThrowOnExportSessionId = failingSessionId;
            var reload = server.TryReloadRuleset(
                CreateReloadManifest("2", "replacement"),
                [new ReloadableModule("rules", "2.0.0")]);

            Assert.False(reload.Succeeded);
            Assert.Equal(
                "ruleset-target-preparation-failed",
                Assert.Single(reload.Diagnostics).Code);
            Assert.Same(initial, runtime.Active.Target);
            Assert.Equal(initialIdentity, server.ActiveRulesetIdentityHash);
            Assert.False(initial.Disposed);
            Assert.True(replacement.Disposed);
            Assert.Equal(0, server.RetiredRulesetCount);

            var store = new AuthoritativeSessionStore(saveRoot);
            var firstPersisted = store.Load(
                SessionIdForToken(firstHello.SessionToken),
                initialIdentity);
            var secondPersisted = store.Load(
                SessionIdForToken(secondHello.SessionToken),
                initialIdentity);
            Assert.NotNull(firstPersisted);
            Assert.NotNull(secondPersisted);
            Assert.Equal("Batch-First", firstPersisted.SerializedState);
            Assert.Equal("Batch-Second", secondPersisted.SerializedState);
            Assert.Equal(initialIdentity, firstPersisted.RulesetIdentityHash);
            Assert.Equal(initialIdentity, secondPersisted.RulesetIdentityHash);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
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
    public async Task ReconnectPreservesThreeParticipantSnapshotAndSelfFirstOrder()
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

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        using var thirdClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await SendHelloAsync(firstStream, cancellationToken);
        var firstHello = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello;
        var firstInitial = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;

        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(secondStream, cancellationToken);
        _ = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        var secondInitial = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;

        await thirdClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var thirdStream = thirdClient.GetStream();
        await SendHelloAsync(thirdStream, cancellationToken);
        _ = await EnvelopeCodec.ReadAsync(thirdStream, cancellationToken);
        var thirdInitial = (await EnvelopeCodec.ReadAsync(thirdStream, cancellationToken))!.Snapshot;
        var firstEntityId = Assert.Single(firstInitial.Players).EntityId;
        var secondEntityId = secondInitial.Players[0].EntityId;
        var thirdEntityId = thirdInitial.Players[0].EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);
        Assert.NotEqual(secondEntityId, thirdEntityId);

        Assert.Equal(
            [secondEntityId, firstEntityId],
            secondInitial.Players.Select(player => player.EntityId));
        Assert.Equal(
            [thirdEntityId, firstEntityId, secondEntityId],
            thirdInitial.Players.Select(player => player.EntityId));
        Assert.Equal(
            SnapshotStateHasher.Compute(thirdInitial),
            thirdInitial.StateSha256);

        firstClient.Close();
        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await SendHelloAsync(resumedStream, cancellationToken, firstHello.SessionToken);
        var resumedHello = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.ServerHello;
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.Snapshot;

        Assert.Equal(firstHello.SessionToken, resumedHello.SessionToken);
        Assert.Equal(
            [firstEntityId, secondEntityId, thirdEntityId],
            resumedSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(
            SnapshotStateHasher.Compute(resumedSnapshot),
            resumedSnapshot.StateSha256);
    }

    [Fact]
    public async Task ReconnectPreservesFourParticipantSnapshotAndSelfFirstOrder()
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

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        using var thirdClient = new TcpClient();
        using var fourthClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await SendHelloAsync(firstStream, cancellationToken);
        var firstHello = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello;
        var firstInitial = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;

        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await SendHelloAsync(secondStream, cancellationToken);
        _ = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        var secondInitial = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;

        await thirdClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var thirdStream = thirdClient.GetStream();
        await SendHelloAsync(thirdStream, cancellationToken);
        _ = await EnvelopeCodec.ReadAsync(thirdStream, cancellationToken);
        var thirdInitial = (await EnvelopeCodec.ReadAsync(thirdStream, cancellationToken))!.Snapshot;

        await fourthClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var fourthStream = fourthClient.GetStream();
        await SendHelloAsync(fourthStream, cancellationToken);
        _ = await EnvelopeCodec.ReadAsync(fourthStream, cancellationToken);
        var fourthInitial = (await EnvelopeCodec.ReadAsync(fourthStream, cancellationToken))!.Snapshot;

        var firstEntityId = Assert.Single(firstInitial.Players).EntityId;
        var secondEntityId = secondInitial.Players[0].EntityId;
        var thirdEntityId = thirdInitial.Players[0].EntityId;
        var fourthEntityId = fourthInitial.Players[0].EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);
        Assert.NotEqual(secondEntityId, thirdEntityId);
        Assert.NotEqual(thirdEntityId, fourthEntityId);

        Assert.Equal(
            [fourthEntityId, firstEntityId, secondEntityId, thirdEntityId],
            fourthInitial.Players.Select(player => player.EntityId));
        Assert.Equal(
            SnapshotStateHasher.Compute(fourthInitial),
            fourthInitial.StateSha256);

        firstClient.Close();
        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await SendHelloAsync(resumedStream, cancellationToken, firstHello.SessionToken);
        var resumedHello = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.ServerHello;
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.Snapshot;

        Assert.Equal(firstHello.SessionToken, resumedHello.SessionToken);
        Assert.Equal(
            [firstEntityId, secondEntityId, thirdEntityId, fourthEntityId],
            resumedSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(
            SnapshotStateHasher.Compute(resumedSnapshot),
            resumedSnapshot.StateSha256);
    }

    [Fact]
    public async Task ResumeTokenRestoresPurchasedStoreStateAndDuplicatePurchaseDoesNotMutateIt()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
            new StoreItem(1, 43, 25, RemainingStockState()),
        });
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: []);
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

    [Fact]
    public async Task ResumeTokenPreservesPurchasedStateAndCompleteParticipantSet()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
        });
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: []);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 10,
            snapshotProvider: executor,
            participantInitializer: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string firstResumeToken;
        uint firstEntityId;
        uint secondEntityId;
        Snapshot purchasedSnapshot;

        using (var firstClient = new TcpClient())
        using (var secondClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken, requestedCharacterName: "Store-First");
            firstResumeToken = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello.SessionToken;
            var firstInitial = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;
            firstEntityId = Assert.Single(firstInitial.Players).EntityId;

            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var secondStream = secondClient.GetStream();
            await SendHelloAsync(secondStream, cancellationToken, requestedCharacterName: "Store-Second");
            _ = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
            var secondInitial = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
            secondEntityId = secondInitial.Players[0].EntityId;
            Assert.NotEqual(firstEntityId, secondEntityId);

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
            _ = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);

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
            Assert.Equal(
                [firstEntityId, secondEntityId],
                purchasedSnapshot.Players.Select(player => player.EntityId));
            Assert.Equal(25U, purchasedSnapshot.Players[0].Gold);
            Assert.Single(purchasedSnapshot.Players[0].Inventory);
            Assert.Equal(SnapshotStateHasher.Compute(purchasedSnapshot), purchasedSnapshot.StateSha256);
        }

        using var resumedClient = new TcpClient();
        await resumedClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var resumedStream = resumedClient.GetStream();
        await SendHelloAsync(resumedStream, cancellationToken, firstResumeToken);
        var resumedHello = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.ServerHello;
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(resumedStream, cancellationToken))!.Snapshot;

        Assert.Equal(firstResumeToken, resumedHello.SessionToken);
        Assert.Equal(
            [firstEntityId, secondEntityId],
            resumedSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal("Store-First", resumedSnapshot.Players[0].CharacterName);
        Assert.Equal("Store-Second", resumedSnapshot.Players[1].CharacterName);
        Assert.Equal(25U, resumedSnapshot.Players[0].Gold);
        Assert.Single(resumedSnapshot.Players[0].Inventory);
        Assert.Equal(purchasedSnapshot.StateSha256, resumedSnapshot.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(resumedSnapshot), resumedSnapshot.StateSha256);
    }

    [Fact]
    public async Task TcpServerRestartRestoresTwoParticipantStoreStateWithAFreshAdapter()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Multiplayer-Restart-Recovery-" + Guid.NewGuid().ToString("N"));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with {
                Identified = true,
                ItemType = 1,
            }),
        });
        const string firstCharacterName = "Restart-First";
        const string secondCharacterName = "Restart-Second";
        string firstResumeToken;
        string secondResumeToken;
        uint firstEntityId;
        uint secondEntityId;
        Snapshot purchasedSnapshot;

        try {
            var saveStore = new AuthoritativeSaveStore(saveRoot);
            var initialExecutor = new StoreSimulationExecutor(
                catalog,
                startingGold: 100,
                startingInventoryGrid: []);
            var initialCommandServer = new AuthoritativeCommandServer(initialExecutor);
            var initialHandshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            await using (var server = new AuthoritativeTcpServer(
                             initialCommandServer,
                             initialHandshake,
                             () => 10,
                             snapshotProvider: initialExecutor,
                             participantInitializer: initialExecutor,
                             saveProvider: initialExecutor,
                             saveStore: saveStore)) {
                server.Start();
                var cancellationToken = TestContext.Current.CancellationToken;
                using var firstClient = new TcpClient();
                using var secondClient = new TcpClient();
                await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var firstStream = firstClient.GetStream();
                await SendHelloAsync(
                    firstStream,
                    cancellationToken,
                    requestedCharacterName: firstCharacterName);
                firstResumeToken = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.ServerHello.SessionToken;
                var firstInitialSnapshot = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.Snapshot;
                firstEntityId = Assert.Single(firstInitialSnapshot.Players).EntityId;

                await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
                await using var secondStream = secondClient.GetStream();
                await SendHelloAsync(
                    secondStream,
                    cancellationToken,
                    requestedCharacterName: secondCharacterName);
                secondResumeToken = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.ServerHello.SessionToken;
                var secondInitialSnapshot = (await EnvelopeCodec.ReadAsync(
                    secondStream,
                    cancellationToken))!.Snapshot;
                secondEntityId = secondInitialSnapshot.Players[0].EntityId;
                Assert.NotEqual(firstEntityId, secondEntityId);
                Assert.Equal(
                    [secondEntityId, firstEntityId],
                    secondInitialSnapshot.Players.Select(player => player.EntityId));

                await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 1,
                            RequestedTick = 10,
                            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
                        } },
                    },
                }, cancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!
                        .CommandAck.Results.Single().Status);
                _ = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);

                await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                    CommandBatch = new CommandBatch {
                        Commands = { new Command {
                            ClientSequence = 2,
                            RequestedTick = 10,
                            PurchaseRequested = new PurchaseRequested {
                                StoreId = 1,
                                StoreSlot = 0,
                            },
                        } },
                    },
                }, cancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!
                        .CommandAck.Results.Single().Status);
                purchasedSnapshot = (await EnvelopeCodec.ReadAsync(
                    firstStream,
                    cancellationToken))!.Snapshot;
                Assert.Equal(
                    [firstEntityId, secondEntityId],
                    purchasedSnapshot.Players.Select(player => player.EntityId));
                Assert.Equal(firstCharacterName, purchasedSnapshot.Players[0].CharacterName);
                Assert.Equal(secondCharacterName, purchasedSnapshot.Players[1].CharacterName);
                Assert.Equal(25U, purchasedSnapshot.Players[0].Gold);
                Assert.Single(purchasedSnapshot.Players[0].Inventory);
                Assert.Equal(
                    SnapshotStateHasher.Compute(purchasedSnapshot),
                    purchasedSnapshot.StateSha256);
            }

            Assert.NotNull(saveStore.Load(SessionIdForToken(firstResumeToken)));
            Assert.NotNull(saveStore.Load(SessionIdForToken(secondResumeToken)));

            var replacementExecutor = new StoreSimulationExecutor(
                catalog,
                startingGold: 100,
                startingInventoryGrid: []);
            var replacementCommandServer = new AuthoritativeCommandServer(replacementExecutor);
            var replacementHandshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "server-build",
                "0.1.0",
                "content-hash",
                20));
            await using var restartedServer = new AuthoritativeTcpServer(
                replacementCommandServer,
                replacementHandshake,
                () => 10,
                snapshotProvider: replacementExecutor,
                participantInitializer: replacementExecutor,
                saveProvider: replacementExecutor,
                saveStore: saveStore);
            restartedServer.Start();
            var restartCancellationToken = TestContext.Current.CancellationToken;
            using var resumedFirstClient = new TcpClient();
            using var resumedSecondClient = new TcpClient();
            await resumedFirstClient.ConnectAsync(
                IPAddress.Loopback,
                restartedServer.Port,
                restartCancellationToken);
            await using var resumedFirstStream = resumedFirstClient.GetStream();
            await SendHelloAsync(
                resumedFirstStream,
                restartCancellationToken,
                firstResumeToken,
                requestedCharacterName: "Ignored-First-Name");
            var resumedFirstHello = (await EnvelopeCodec.ReadAsync(
                resumedFirstStream,
                restartCancellationToken))!.ServerHello;
            var resumedFirstOnlySnapshot = (await EnvelopeCodec.ReadAsync(
                resumedFirstStream,
                restartCancellationToken))!.Snapshot;
            Assert.Equal(firstResumeToken, resumedFirstHello.SessionToken);
            Assert.Single(resumedFirstOnlySnapshot.Players);

            await resumedSecondClient.ConnectAsync(
                IPAddress.Loopback,
                restartedServer.Port,
                restartCancellationToken);
            await using var resumedSecondStream = resumedSecondClient.GetStream();
            await SendHelloAsync(
                resumedSecondStream,
                restartCancellationToken,
                secondResumeToken,
                requestedCharacterName: "Ignored-Second-Name");
            var resumedSecondHello = (await EnvelopeCodec.ReadAsync(
                resumedSecondStream,
                restartCancellationToken))!.ServerHello;
            var resumedSecondSnapshot = (await EnvelopeCodec.ReadAsync(
                resumedSecondStream,
                restartCancellationToken))!.Snapshot;
            Assert.Equal(secondResumeToken, resumedSecondHello.SessionToken);
            Assert.Equal(
                [secondEntityId, firstEntityId],
                resumedSecondSnapshot.Players.Select(player => player.EntityId));
            Assert.Equal(secondCharacterName, resumedSecondSnapshot.Players[0].CharacterName);
            Assert.Equal(firstCharacterName, resumedSecondSnapshot.Players[1].CharacterName);
            Assert.Equal(25U, resumedSecondSnapshot.Players[1].Gold);
            Assert.Single(resumedSecondSnapshot.Players[1].Inventory);

            await EnvelopeCodec.WriteAsync(resumedFirstStream, new Envelope {
                SnapshotRequest = new Envelope.Types.SnapshotRequest(),
            }, restartCancellationToken);
            var resumedFirstSnapshot = (await EnvelopeCodec.ReadAsync(
                resumedFirstStream,
                restartCancellationToken))!.Snapshot;
            Assert.Equal(
                [firstEntityId, secondEntityId],
                resumedFirstSnapshot.Players.Select(player => player.EntityId));
            Assert.Equal(firstCharacterName, resumedFirstSnapshot.Players[0].CharacterName);
            Assert.Equal(secondCharacterName, resumedFirstSnapshot.Players[1].CharacterName);
            Assert.Equal(25U, resumedFirstSnapshot.Players[0].Gold);
            Assert.Single(resumedFirstSnapshot.Players[0].Inventory);
            Assert.Equal(purchasedSnapshot.StateSha256, resumedFirstSnapshot.StateSha256);
            Assert.Equal(
                SnapshotStateHasher.Compute(resumedFirstSnapshot),
                resumedFirstSnapshot.StateSha256);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
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

    private static string SessionIdForToken(string resumeToken)
    {
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();
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

    private static async Task ReadHandshakeSnapshotAsync(
        Stream stream,
        ulong expectedTick,
        CancellationToken cancellationToken)
    {
        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot!.PayloadCase);
        Assert.Equal(expectedTick, snapshot.Snapshot.Tick);
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

        public List<ulong> AppliedTicks { get; } = [];

        public List<ulong> SnapshotTicks { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            AppliedTicks.Add(appliedTick);
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            SnapshotTicks.Add(tick);
            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
        }
    }

    private sealed class MalformedEventProvider : IAuthoritativeEventProvider
    {
        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        {
            return new EventBatch {
                Tick = tick,
                Events = { new GameEvent() },
            };
        }
    }

    private sealed class StaleEventProvider : IAuthoritativeEventProvider
    {
        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        {
            return new EventBatch {
                Tick = tick == 0 ? 0 : tick - 1,
                Events = { new GameEvent { Experience = new ExperienceEvent() } },
            };
        }
    }

    private sealed class MalformedSnapshotProvider : IAuthoritativeSnapshotProvider
    {
        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            return new Snapshot {
                Tick = tick,
                StateSha256 = "not-a-hash",
            };
        }
    }

    private sealed class ReloadableHostAdapter(string name)
        : IAuthoritativeGameHostAdapter, IDisposable
    {
        private readonly Dictionary<string, string> sessionStates = new(StringComparer.Ordinal);
        private readonly object initializationSynchronization = new();
        private int executeCount;

        public bool Disposed { get; private set; }

        public bool SessionStateImported { get; private set; }

        public bool ThrowOnExport { get; init; }

        public string? ThrowOnExportSessionId { get; set; }

        public int ExecuteCount => Volatile.Read(ref executeCount);

        public List<string> InitializedNames { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            Interlocked.Increment(ref executeCount);
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
            if (ThrowOnExport || string.Equals(
                    ThrowOnExportSessionId,
                    sessionId,
                    StringComparison.Ordinal))
                throw new InvalidDataException("candidate session export rejected");
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

    private sealed class MultiParticipantReloadableHostAdapter(string name)
        : IAuthoritativeGameHostAdapter, IDisposable
    {
        private readonly Dictionary<string, string> sessionStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> sessionEntities = new(StringComparer.Ordinal);
        private readonly object synchronization = new();

        public bool SessionStateImported { get; private set; }

        public int ImportCount { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            lock (synchronization) {
                sessionEntities[sessionId] = entityId;
                var snapshot = new Snapshot { Tick = tick };
                var ordered = sessionEntities
                    .OrderBy(pair => pair.Key == sessionId ? 0 : 1)
                    .ThenBy(pair => pair.Value);
                foreach (var pair in ordered) {
                    snapshot.Players.Add(new PlayerSnapshot {
                        EntityId = pair.Value,
                        CharacterName = sessionStates.TryGetValue(pair.Key, out var state)
                            ? state
                            : name,
                    });
                }
                snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
                return snapshot;
            }
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => null;

        public string ExportPlayerSave(string sessionId)
        {
            lock (synchronization)
                return sessionStates.TryGetValue(sessionId, out var state) ? state : name;
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            lock (synchronization)
                sessionStates[sessionId] = serializedSave;
            ImportCount++;
            SessionStateImported = true;
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            lock (synchronization)
                sessionStates.TryAdd(sessionId, selection.CharacterName ?? name);
        }

        public void Dispose()
        {
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
