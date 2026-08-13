using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientTransportTests
{
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
        var initial = Assert.Single(client.DrainMessages());
        var initialSnapshot = initial.Snapshot;
        Assert.NotNull(initialSnapshot);
        Assert.Equal(12UL, initialSnapshot!.Tick);

        client.Queue(new Command { MoveRequested = new MoveRequested { DirectionX = 1 } }, 13);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(25, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.Single(client.DrainMessages()).Acknowledgement;
        Assert.NotNull(acknowledgement);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(acknowledgement.Results).Status);
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
        await WriteEnvelopeAsync(stream, new Envelope {
            CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = 1, Status = CommandStatus.Accepted } },
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
