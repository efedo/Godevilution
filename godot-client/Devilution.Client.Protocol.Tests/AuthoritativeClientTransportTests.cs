using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class AuthoritativeClientTransportTests
{
    [Fact]
    public async Task SchedulesAnUnsentInteractiveCommandAfterAFrameStall()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                TickRateHz = 20, GameMode = GameMode.SinglePlayer,
            } });
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            Assert.True(command.RequestedTick >= 119, $"Unsent command retained its pre-stall tick: {command.RequestedTick}");
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "stall-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(150),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(150),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        client.QueueRealtime(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, 100);
        // Godot can queue during input/route handling and only poll on a later frame.
        await Task.Delay(750, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SchedulesCommandsUsingTheAgeOfAReceivedSnapshot()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                TickRateHz = 20, GameMode = GameMode.SinglePlayer,
            } });
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } });
            var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
            // Fixed 150ms retry lead is four ticks. A >300ms-old snapshot must
            // add at least six elapsed ticks, without changing the server policy.
            Assert.True(command.RequestedTick >= 110, $"Stale requested tick: {command.RequestedTick}");
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "clock-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(150),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(150),
        });
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await Task.Delay(320, TestContext.Current.CancellationToken);
        client.Queue(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, client.SuggestedCommandTick(100));
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null, 0, 1)]
    [InlineData(0UL, 0, 1)]
    [InlineData(42UL, 0, 43)]
    [InlineData(1UL, 3, 4)]
    [InlineData(42UL, 3, 43)]
    public async Task SeedsNewCommandsWithoutRenumberingPendingRetries(ulong? highWater, int pendingCount, ulong expectedSequence)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            var hello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                RulesetIdentityHash = "ruleset-hash", GameMode = GameMode.SinglePlayer,
            };
            if (highWater.HasValue)
                hello.LastClientSequence = highWater.Value;
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = hello });
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot {
                ContentManifestHash = "content-hash", RulesetIdentityHash = "ruleset-hash",
            } });
            for (var i = 0; i <= pendingCount; i++) {
                var command = Assert.Single((await ReadEnvelopeAsync(stream)).CommandBatch.Commands);
                Assert.Equal(i < pendingCount ? (ulong)i + 1 : expectedSequence, command.ClientSequence);
            }
        }, TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "test", "0.1.0", "content-hash"));
        for (var i = 0; i < pendingCount; i++)
            client.Queue(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, 1);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(pendingCount, client.PendingCommandCount);
        Assert.Equal(expectedSequence, client.Queue(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, 1));
        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void PreservesTownerGreetingCommandAcrossProtocolSerialization()
    {
        var command = new Command {
            TalkToTownerRequested = new TalkToTownerRequested { TownerId = 7 },
        };

        var roundTrip = Command.Parser.ParseFrom(command.ToByteArray());

        Assert.Equal(Command.IntentOneofCase.TalkToTownerRequested, roundTrip.IntentCase);
        Assert.Equal(7U, roundTrip.TalkToTownerRequested.TownerId);
    }

    [Fact]
    public void PreservesTargetCellMovementFieldsAcrossProtocolSerialization()
    {
        var command = new Command {
            MoveRequested = new MoveRequested {
                TargetCell = true,
                TargetX = 14,
                TargetY = 9,
            },
        };

        var roundTrip = Command.Parser.ParseFrom(command.ToByteArray());

        Assert.True(roundTrip.MoveRequested.TargetCell);
        Assert.Equal(14, roundTrip.MoveRequested.TargetX);
        Assert.Equal(9, roundTrip.MoveRequested.TargetY);
    }

    [Fact]
    public async Task RequiresRulesetIdentityForProtocolV2()
    {
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            1,
            "godot-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            "content-hash"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PerformsHandshakeFramesCommandsAndReceivesAcknowledgements()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RunServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            port,
            "godot-test",
            "0.1.0",
            "content-hash") {
            RequestedCharacterClassId = 2,
            RequestedCharacterName = "Godot-Rogue",
        });

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GameMode.Multiplayer, client.ServerGameMode);
        Assert.Equal(
            new AuthoritativeServerIdentity(
                "test-server",
                "0.1.0",
                "content-hash",
                "ruleset-hash",
                20,
                GameMode.Multiplayer),
            client.ServerIdentity);
        var initial = Assert.Single(client.DrainMessages());
        var initialSnapshot = initial.Snapshot;
        Assert.NotNull(initialSnapshot);
        Assert.Equal(12UL, initialSnapshot!.Tick);
        Assert.Equal("content-hash", initialSnapshot.ContentManifestHash);
        Assert.Equal("ruleset-hash", initialSnapshot.RulesetIdentityHash);

        client.Queue(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 13);
        await client.PollAsync(TestContext.Current.CancellationToken);
        AuthoritativeClientMessage? acknowledgementMessage = null;
        Snapshot? reloadedSnapshot = null;
        for (var attempt = 0; attempt < 50 && (acknowledgementMessage is null || reloadedSnapshot is null); attempt++) {
            foreach (var message in client.DrainMessages()) {
                if (message.Acknowledgement is not null)
                    acknowledgementMessage = message;
                if (message.Snapshot?.Tick == 13)
                    reloadedSnapshot = message.Snapshot;
            }
            if (acknowledgementMessage is null || reloadedSnapshot is null)
                await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.NotNull(acknowledgementMessage);
        var acknowledgement = acknowledgementMessage!.Acknowledgement;
        Assert.NotNull(acknowledgement);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(acknowledgement.Results).Status);
        Assert.NotNull(reloadedSnapshot);
        Assert.Equal("reloaded-content", reloadedSnapshot!.ContentManifestHash);
        Assert.Equal("reloaded-ruleset", reloadedSnapshot.RulesetIdentityHash);
        Assert.Equal("reloaded-content", client.ServerIdentity!.ContentManifestHash);
        Assert.Equal("reloaded-ruleset", client.ServerIdentity.RulesetIdentityHash);
        await serverTask;
    }

    [Fact]
    public async Task OwnsQueuedCommandAfterCallerMutation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RunOwnedCommandServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        _ = client.DrainMessages();
        var command = new Command {
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = -1 },
        };
        client.Queue(command, 13);
        command.MoveRequested.DirectionX = 9;
        command.MoveRequested.DirectionY = 9;

        await client.PollAsync(TestContext.Current.CancellationToken);
        await serverTask;
    }

    [Fact]
    public async Task RejectsAHandshakeWithAnIncompatibleRulesetIdentity()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunMismatchedRulesetServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash",
            "client-ruleset-hash"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        await serverTask;
    }

    [Fact]
    public async Task AcceptsACompleteInitialSnapshotIdentityAfterLiveReload()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunInitialSnapshotIdentityServerAsync(listener, incomplete: false);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash",
            "ruleset-hash"));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.True(client.IsConnected);
        Assert.Equal("different-ruleset", client.ServerIdentity!.RulesetIdentityHash);
        Assert.NotNull(Assert.Single(client.DrainMessages()).Snapshot);
        await serverTask;
    }

    [Fact]
    public async Task RejectsAnInitialSnapshotWithAnIncompleteIdentity()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunInitialSnapshotIdentityServerAsync(listener, incomplete: true);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash",
            "ruleset-hash"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.False(client.IsConnected);
        await serverTask;
    }

    [Fact]
    public async Task RejectsAnErrorBeforeTheInitialSnapshot()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunInitialSnapshotErrorServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.Equal("snapshot unavailable", exception.Message);
        Assert.False(client.IsConnected);
        await serverTask;
    }

    [Fact]
    public async Task RejectsAHandshakeWithAnInvalidGameMode()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunInvalidGameModeServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.False(client.IsConnected);
        await serverTask;
    }

    [Fact]
    public async Task RejectsMalformedAcknowledgementWithoutDroppingPendingCommand()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RunMalformedAcknowledgementServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        _ = client.DrainMessages();
        client.Queue(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 13);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(25, TestContext.Current.CancellationToken);

        var message = Assert.Single(client.DrainMessages());
        Assert.NotNull(message.Error);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, message.Error!.Code);
        Assert.Equal(1, client.PendingCommandCount);
        await serverTask;
    }

    private static async Task RunServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        var hello = await ReadEnvelopeAsync(stream);
        Assert.Equal("0.1.0", hello.ClientHello.ProtocolSchemaVersion);
        Assert.Equal("content-hash", hello.ClientHello.ContentManifestHash);
        Assert.True(hello.ClientHello.HasRequestedCharacterClassId);
        Assert.Equal(2U, hello.ClientHello.RequestedCharacterClassId);
        Assert.True(hello.ClientHello.HasRequestedCharacterName);
        Assert.Equal("Godot-Rogue", hello.ClientHello.RequestedCharacterName);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "ruleset-hash",
                TickRateHz = 20,
                GameMode = GameMode.Multiplayer,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot {
                Tick = 12,
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "ruleset-hash",
                Players = { new PlayerSnapshot { EntityId = 7 } },
            },
        });
        var batch = await ReadEnvelopeAsync(stream);
        var command = Assert.Single(batch.CommandBatch.Commands);
        Assert.Equal(1UL, command.ClientSequence);
        Assert.Equal(13UL, command.RequestedTick);
        await WriteEnvelopeAsync(stream, new Envelope {
            CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = 1, Status = CommandStatus.Accepted } },
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot {
                Tick = 13,
                ContentManifestHash = "reloaded-content",
                RulesetIdentityHash = "reloaded-ruleset",
                Players = { new PlayerSnapshot { EntityId = 7 } },
            },
        });
        listener.Stop();
    }

    private static async Task RunMismatchedRulesetServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        var hello = await ReadEnvelopeAsync(stream);
        Assert.Equal("content-hash", hello.ClientHello.ContentManifestHash);
        Assert.Equal("client-ruleset-hash", hello.ClientHello.RulesetIdentityHash);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "server-ruleset-hash",
                TickRateHz = 20,
            },
        });
        listener.Stop();
    }

    private static async Task RunInitialSnapshotIdentityServerAsync(
        TcpListener listener,
        bool incomplete)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "ruleset-hash",
                TickRateHz = 20,
                SessionToken = "resume-token",
                GameMode = GameMode.SinglePlayer,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot {
                Tick = 1,
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = incomplete ? string.Empty : "different-ruleset",
                Players = { new PlayerSnapshot { EntityId = 7 } },
            },
        });
        await Task.Delay(100, TestContext.Current.CancellationToken);
        listener.Stop();
    }

    private static async Task RunOwnedCommandServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
                GameMode = GameMode.Multiplayer,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot {
                Tick = 12,
                Players = { new PlayerSnapshot { EntityId = 7 } },
            },
        });
        var batch = await ReadEnvelopeAsync(stream);
        var command = Assert.Single(batch.CommandBatch.Commands);
        Assert.Equal(1UL, command.ClientSequence);
        Assert.Equal(13UL, command.RequestedTick);
        Assert.Equal(1, command.MoveRequested.DirectionX);
        Assert.Equal(-1, command.MoveRequested.DirectionY);
        listener.Stop();
    }

    private static async Task RunInitialSnapshotErrorServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
                GameMode = GameMode.Multiplayer,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Error = new ProtocolError {
                Code = ProtocolErrorCode.InvalidMessage,
                Detail = "snapshot unavailable",
            },
        });
        listener.Stop();
    }

    private static async Task RunInvalidGameModeServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
                GameMode = GameMode.Unspecified,
            },
        });
        listener.Stop();
    }

    private static async Task RunMalformedAcknowledgementServerAsync(TcpListener listener)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
                GameMode = GameMode.Multiplayer,
            },
        });
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot {
                Tick = 12,
                Players = { new PlayerSnapshot { EntityId = 7 } },
            },
        });
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = 1 } },
            },
        });
        listener.Stop();
    }

    private static async Task<Envelope> ReadEnvelopeAsync(NetworkStream stream)
    {
        var header = new byte[4];
        await ReadExactlyAsync(stream, header);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset));
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}
