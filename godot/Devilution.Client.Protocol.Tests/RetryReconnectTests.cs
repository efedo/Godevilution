using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class RetryReconnectTests
{
    [Fact]
    public async Task ResubmitsACommandWhenTheAcknowledgementIsDelayed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunDelayedAcknowledgementServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "godot-test", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(50),
        });

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        _ = client.DrainMessages();
        var sequence = client.Queue(new Command { MoveRequested = new MoveRequested { DirectionY = 1 } }, 100);
        await client.PollAsync(TestContext.Current.CancellationToken);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);

        await serverTask;
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
    }

    [Fact]
    public async Task ReconnectsWithTheServerIssuedResumeToken()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunReconnectServerAsync(listener);
        var options = new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "godot-test", "0.1.0", "content-hash");
        var firstClient = new AuthoritativeClient(options);
        await firstClient.ConnectAsync(TestContext.Current.CancellationToken);
        var sessionToken = firstClient.SessionToken;
        Assert.Equal("resume-token", sessionToken);
        _ = firstClient.DrainMessages();
        await firstClient.DisposeAsync();

        await using var resumedClient = new AuthoritativeClient(options with { ResumeToken = sessionToken });
        await resumedClient.ConnectAsync(TestContext.Current.CancellationToken);
        var snapshot = Assert.Single(resumedClient.DrainMessages()).Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(8UL, snapshot!.Tick);
        await serverTask;
    }

    [Fact]
    public async Task SameClientInstanceReconnectsAndRetainsResumeIdentity()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunSameInstanceReconnectServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal("reloaded-content", client.ServerIdentity!.ContentManifestHash);
        Assert.Equal("reloaded-ruleset", client.ServerIdentity.RulesetIdentityHash);
        var sessionToken = client.SessionToken;
        _ = client.DrainMessages();
        for (var attempt = 0; attempt < 50 && client.IsConnected; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.False(client.IsConnected);
        var sequence = client.Queue(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);

        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);

        Assert.Equal(sessionToken, client.SessionToken);
        Assert.Equal("reloaded-content", client.ServerIdentity!.ContentManifestHash);
        Assert.Equal("reloaded-ruleset", client.ServerIdentity.RulesetIdentityHash);
        var snapshot = Assert.Single(client.DrainMessages()).Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(9UL, snapshot!.Tick);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        await serverTask;
    }

    [Fact]
    public async Task SameClientInstanceRefreshesIdentityAfterResumedServerReload()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunResumedIdentityRefreshServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            "initial-content",
            "initial-ruleset"));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        _ = client.DrainMessages();
        for (var attempt = 0; attempt < 50 && client.IsConnected; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.False(client.IsConnected);

        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal("replacement-content", client.ServerIdentity!.ContentManifestHash);
        Assert.Equal("replacement-ruleset", client.ServerIdentity.RulesetIdentityHash);
        await serverTask;
    }

    [Fact]
    public async Task FailedHandshakeClosesTheTransportBeforeTheClientRetries()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = RunRejectedHandshakeServerAsync(listener);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "godot-test",
            "0.1.0",
            "content-hash"));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await client.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.False(client.IsConnected);
        await serverTask;
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        for (var attempt = 0; attempt < 50; attempt++) {
            foreach (var message in client.DrainMessages()) {
                var result = message.Acknowledgement?.Results.SingleOrDefault(candidate => candidate.ClientSequence == sequence);
                if (result is not null)
                    return result;
            }
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the retried command acknowledgement.");
    }

    private static async Task RunDelayedAcknowledgementServerAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, "resume-token", 4);
        _ = await ReadEnvelopeAsync(stream);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var retried = await ReadEnvelopeAsync(stream);
        var command = Assert.Single(retried.CommandBatch.Commands);
        await WriteEnvelopeAsync(stream, new Envelope {
            CommandAck = new CommandAck {
                Results = { new CommandResult { ClientSequence = command.ClientSequence, Status = CommandStatus.Accepted } },
            },
        });
        listener.Stop();
    }

    private static async Task RunReconnectServerAsync(TcpListener listener)
    {
        using (var firstClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = firstClient.GetStream();
            await CompleteHandshakeAsync(stream, "resume-token", 7);
        }
        using (var resumedClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = resumedClient.GetStream();
            var hello = await ReadEnvelopeAsync(stream);
            Assert.Equal("resume-token", hello.ClientHello.ResumeToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ServerBuildId = "test-server",
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = "content-hash",
                    RulesetIdentityHash = "content-hash",
                    TickRateHz = 20,
                    SessionToken = "resume-token",
                    GameMode = GameMode.SinglePlayer,
                },
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = 8, Players = { new PlayerSnapshot { EntityId = 7 } } },
            });
        }
        listener.Stop();
    }

    private static async Task RunSameInstanceReconnectServerAsync(TcpListener listener)
    {
        using (var firstClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = firstClient.GetStream();
            await CompleteHandshakeAsync(
                stream,
                "same-instance-token",
                7,
                contentManifestHash: "reloaded-content",
                rulesetIdentityHash: "reloaded-ruleset");
        }
        using (var resumedClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = resumedClient.GetStream();
            var hello = await ReadEnvelopeAsync(stream);
            Assert.Equal("same-instance-token", hello.ClientHello.ResumeToken);
            Assert.Equal("reloaded-content", hello.ClientHello.ContentManifestHash);
            Assert.Equal("reloaded-ruleset", hello.ClientHello.RulesetIdentityHash);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ServerBuildId = "test-server",
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = "reloaded-content",
                    RulesetIdentityHash = "reloaded-ruleset",
                    TickRateHz = 20,
                    SessionToken = "same-instance-token",
                    GameMode = GameMode.SinglePlayer,
                },
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot {
                    Tick = 9,
                    ContentManifestHash = "reloaded-content",
                    RulesetIdentityHash = "reloaded-ruleset",
                    Players = { new PlayerSnapshot { EntityId = 7 } },
                },
            });
            var commandEnvelope = await ReadEnvelopeAsync(stream);
            var command = Assert.Single(commandEnvelope.CommandBatch.Commands);
            Assert.Equal(1UL, command.ClientSequence);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck {
                    Results = { new CommandResult {
                        ClientSequence = command.ClientSequence,
                        Status = CommandStatus.Accepted,
                    } },
                },
            });
        }
        listener.Stop();
    }

    private static async Task RunResumedIdentityRefreshServerAsync(TcpListener listener)
    {
        const string sessionToken = "identity-refresh-token";
        using (var firstClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = firstClient.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ServerBuildId = "test-server",
                    ProtocolSchemaVersion = ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                    ContentManifestHash = "initial-content",
                    RulesetIdentityHash = "initial-ruleset",
                    TickRateHz = 20,
                    SessionToken = sessionToken,
                    GameMode = GameMode.SinglePlayer,
                },
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot {
                    Tick = 1,
                    ContentManifestHash = "initial-content",
                    RulesetIdentityHash = "initial-ruleset",
                    Players = { new PlayerSnapshot { EntityId = 7 } },
                },
            });
        }

        using (var resumedClient = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)) {
            await using var stream = resumedClient.GetStream();
            var hello = await ReadEnvelopeAsync(stream);
            Assert.Equal(sessionToken, hello.ClientHello.ResumeToken);
            Assert.Equal("initial-content", hello.ClientHello.ContentManifestHash);
            Assert.Equal("initial-ruleset", hello.ClientHello.RulesetIdentityHash);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ServerBuildId = "test-server",
                    ProtocolSchemaVersion = ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                    ContentManifestHash = "replacement-content",
                    RulesetIdentityHash = "replacement-ruleset",
                    TickRateHz = 20,
                    SessionToken = sessionToken,
                    GameMode = GameMode.SinglePlayer,
                },
            });
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot {
                    Tick = 2,
                    ContentManifestHash = "replacement-content",
                    RulesetIdentityHash = "replacement-ruleset",
                    Players = { new PlayerSnapshot { EntityId = 7 } },
                },
            });
        }
        listener.Stop();
    }

    private static async Task RunRejectedHandshakeServerAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        await using var stream = client.GetStream();
        _ = await ReadEnvelopeAsync(stream);
        await WriteEnvelopeAsync(stream, new Envelope {
            Error = new ProtocolError {
                Code = ProtocolErrorCode.ContentMismatch,
                Detail = "content mismatch",
            },
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[1];
        Assert.Equal(0, await stream.ReadAsync(buffer, timeout.Token));
        listener.Stop();
    }

    private static async Task CompleteHandshakeAsync(
        NetworkStream stream,
        string sessionToken,
        ulong tick,
        string? contentManifestHash = null,
        string? rulesetIdentityHash = null)
    {
        var hello = await ReadEnvelopeAsync(stream);
        Assert.Equal("0.1.0", hello.ClientHello.ProtocolSchemaVersion);
        Assert.Equal("content-hash", hello.ClientHello.ContentManifestHash);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "test-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "content-hash",
                TickRateHz = 20,
                SessionToken = sessionToken,
                GameMode = GameMode.SinglePlayer,
            },
        });
        var snapshot = new Snapshot { Tick = tick, Players = { new PlayerSnapshot { EntityId = 7 } } };
        if (contentManifestHash is not null)
            snapshot.ContentManifestHash = contentManifestHash;
        if (rulesetIdentityHash is not null)
            snapshot.RulesetIdentityHash = rulesetIdentityHash;
        await WriteEnvelopeAsync(stream, new Envelope { Snapshot = snapshot });
    }

    private static async Task<Envelope> ReadEnvelopeAsync(NetworkStream stream)
    {
        var header = new byte[4];
        await ReadExactlyAsync(stream, header);
        var payload = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
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
