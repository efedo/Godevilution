using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

/**
 * Locks down the current TCP scheduling behavior around future commands.
 * This is a compatibility characterization, not a statement that advancing
 * the shared simulation to a client's future tick is the desired policy.
 */
public sealed class AuthoritativeTcpSchedulingCharacterizationTests
{
    [Fact]
    public async Task FutureCommandUsesRequestedTickWhenNoPeerHasAdvancedTheBoundary()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(
            new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 111,
            snapshotProvider: executor);
        server.Start();

        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var cancellationToken = timeout.Token;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();

        await SendHelloAsync(stream, cancellationToken, "characterization-control");
        await ReadHandshakeSnapshotAsync(stream, 111, cancellationToken);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 120,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        var result = Assert.Single(acknowledgement!.CommandAck.Results);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(120UL, result.AppliedTick);
        Assert.Equal(120UL, commandServer.CurrentTick);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal([120UL], executor.AppliedTicks);
        var snapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.Equal(120UL, snapshot!.Snapshot.Tick);
    }

    [Fact]
    public async Task FutureCommandAdvancesSharedTickAndMakesAnotherClientCommandTooLate()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(
            new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            () => 111,
            snapshotProvider: executor);
        server.Start();

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var cancellationToken = timeout.Token;

        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();

        await SendHelloAsync(firstStream, cancellationToken, "characterization-first");
        await SendHelloAsync(secondStream, cancellationToken, "characterization-second");
        await ReadHandshakeSnapshotAsync(firstStream, 111, cancellationToken);
        await ReadHandshakeSnapshotAsync(secondStream, 111, cancellationToken);
        Assert.Equal(111UL, commandServer.CurrentTick);
        Assert.Empty(executor.AppliedTicks);

        await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 125,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var futureAckEnvelope = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        var futureResult = Assert.Single(futureAckEnvelope!.CommandAck.Results);
        Assert.Equal(CommandStatus.Accepted, futureResult.Status);
        Assert.Equal(CommandRejectReason.Unspecified, futureResult.RejectReason);
        Assert.Equal(125UL, futureResult.AppliedTick);
        Assert.Equal(125UL, commandServer.CurrentTick);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal([125UL], executor.AppliedTicks);

        var futureSnapshotEnvelope = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, futureSnapshotEnvelope!.PayloadCase);
        Assert.Equal(125UL, futureSnapshotEnvelope.Snapshot.Tick);

        // The physical clock remains fixed at 111. The second command still
        // observes the shared command-server boundary advanced to 125.
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 120,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var lateAckEnvelope = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        var lateResult = Assert.Single(lateAckEnvelope!.CommandAck.Results);
        Assert.Equal(CommandStatus.Rejected, lateResult.Status);
        Assert.Equal(CommandRejectReason.TooLate, lateResult.RejectReason);
        Assert.Equal(125UL, lateResult.AppliedTick);
        Assert.Equal(125UL, commandServer.CurrentTick);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal([125UL], executor.AppliedTicks);

        var lateSnapshotEnvelope = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, lateSnapshotEnvelope!.PayloadCase);
        Assert.Equal(125UL, lateSnapshotEnvelope.Snapshot.Tick);
    }

    private static ValueTask SendHelloAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string resumeToken)
    {
        return EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
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

    private sealed class RecordingExecutor
        : IAuthoritativeCommandExecutor,
          IAuthoritativeSnapshotProvider
    {
        public int CallCount { get; private set; }

        public List<ulong> AppliedTicks { get; } = [];

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
        {
            CallCount++;
            AppliedTicks.Add(appliedTick);
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
}
