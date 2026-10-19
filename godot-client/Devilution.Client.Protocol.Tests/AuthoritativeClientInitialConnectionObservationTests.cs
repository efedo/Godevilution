using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientInitialConnectionObservationTests
{
    [Fact]
    public async Task CapturesTheFirstWireSnapshotAndReturnsDefensiveClones()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var allowLaterSnapshot = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream, cancellationToken);
            await WriteHandshakeAsync(
                stream,
                "test-resume-credential",
                lastClientSequence: 42,
                snapshot: CreateSnapshot(100),
                cancellationToken: cancellationToken);
            await allowLaterSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await WriteEnvelopeAsync(
                stream,
                new Envelope { Snapshot = CreateSnapshot(101) },
                cancellationToken);
        }, cancellationToken);

        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "observation-test",
            "0.1.0",
            "content-hash",
            "ruleset-hash") {
            ResumeToken = "test-resume-credential",
        });

        try {
            await client.ConnectAsync(cancellationToken);
            var observation = Assert.IsType<AuthoritativeInitialConnectionObservation>(
                client.InitialConnectionObservation);
            Assert.Equal(1L, observation.AttemptEpoch);
            Assert.True(observation.AdmissionCompleted);
            Assert.True(observation.ServerHello!.ResumedSessionMatched);
            Assert.Equal("observation-server", observation.ServerHello.ServerBuildId);
            Assert.Equal("0.1.0", observation.ServerHello.ProtocolSchemaVersion);
            Assert.Equal("content-hash", observation.ServerHello.ContentManifestHash);
            Assert.Equal("ruleset-hash", observation.ServerHello.RulesetIdentityHash);
            Assert.Equal(20U, observation.ServerHello.TickRateHz);
            Assert.Equal(GameMode.SinglePlayer, observation.ServerHello.GameMode);
            Assert.Equal(CommandSchedulingMode.ClockGated, observation.ServerHello.CommandSchedulingMode);
            Assert.Equal(42UL, observation.ServerHello.LastClientSequence);
            Assert.DoesNotContain(
                typeof(AuthoritativeServerHelloObservation).GetProperties(),
                property => property.Name is "SessionToken" or "ResumeToken");

            var firstSnapshot = Assert.IsType<Snapshot>(observation.FirstWireSnapshot);
            Assert.Equal(100UL, firstSnapshot.Tick);
            firstSnapshot.Tick = 999;
            Assert.Equal(100UL, observation.FirstWireSnapshot!.Tick);

            var drainedInitialSnapshot = Assert.IsType<Snapshot>(
                Assert.Single(client.DrainMessages(), message => message.Snapshot is not null).Snapshot);
            Assert.Equal(100UL, drainedInitialSnapshot.Tick);
            drainedInitialSnapshot.Tick = 998;
            Assert.Equal(100UL, observation.FirstWireSnapshot!.Tick);

            allowLaterSnapshot.TrySetResult(true);
            var laterSnapshotObserved = false;
            for (var attempt = 0; attempt < 100 && !laterSnapshotObserved; attempt++) {
                laterSnapshotObserved = client.DrainMessages()
                    .Any(message => message.Snapshot?.Tick == 101);
                if (!laterSnapshotObserved)
                    await Task.Delay(10, cancellationToken);
            }
            Assert.True(laterSnapshotObserved);
            Assert.Equal(100UL, observation.FirstWireSnapshot!.Tick);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        } finally {
            allowLaterSnapshot.TrySetResult(true);
        }
    }

    [Fact]
    public async Task CapturedInitialSnapshotCannotQualifyWhenAdmissionFails()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            _ = await ReadEnvelopeAsync(stream, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                ServerHello = new ServerHello {
                    ServerBuildId = "observation-server",
                    ProtocolSchemaVersion = "0.1.0",
                    ContentManifestHash = "content-hash",
                    RulesetIdentityHash = "ruleset-hash",
                    TickRateHz = 20,
                    GameMode = GameMode.SinglePlayer,
                    SessionToken = "test-only-session",
                },
            }, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot {
                    Tick = 73,
                    ContentManifestHash = "content-hash",
                    Players = { new PlayerSnapshot { EntityId = 7 } },
                },
            }, cancellationToken);
        }, cancellationToken);

        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "observation-test",
            "0.1.0",
            "content-hash",
            "ruleset-hash"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ConnectAsync(cancellationToken));

        var observation = Assert.IsType<AuthoritativeInitialConnectionObservation>(
            client.InitialConnectionObservation);
        Assert.Equal(1L, observation.AttemptEpoch);
        Assert.False(observation.AdmissionCompleted);
        Assert.NotNull(observation.ServerHello);
        Assert.Equal(73UL, observation.FirstWireSnapshot!.Tick);
        Assert.False(client.IsConnected);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    [Fact]
    public async Task ReconnectResetsObservationAndSeedsNextIntentFromRestoredCursor()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var secondClientHelloReceived = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSecondHandshake = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using (var firstConnection = await listener.AcceptTcpClientAsync(cancellationToken)) {
                await using var firstStream = firstConnection.GetStream();
                _ = await ReadEnvelopeAsync(firstStream, cancellationToken);
                await WriteHandshakeAsync(firstStream, "test-reconnect-credential", 8, CreateSnapshot(10), cancellationToken);
                var closed = new byte[1];
                _ = await firstStream.ReadAsync(closed.AsMemory(), cancellationToken);
            }

            using var secondConnection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var secondStream = secondConnection.GetStream();
            _ = await ReadEnvelopeAsync(secondStream, cancellationToken);
            secondClientHelloReceived.TrySetResult(true);
            await allowSecondHandshake.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await WriteHandshakeAsync(secondStream, "test-reconnect-credential", 40, CreateSnapshot(20), cancellationToken);
        }, cancellationToken);

        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "observation-test",
            "0.1.0",
            "content-hash",
            "ruleset-hash"));

        try {
            await client.ConnectAsync(cancellationToken);
            var beforeReconnect = Assert.IsType<AuthoritativeInitialConnectionObservation>(
                client.InitialConnectionObservation);
            Assert.Equal(1L, beforeReconnect.AttemptEpoch);
            Assert.True(beforeReconnect.AdmissionCompleted);
            Assert.Equal(8UL, beforeReconnect.ServerHello!.LastClientSequence);
            Assert.Equal(10UL, beforeReconnect.FirstWireSnapshot!.Tick);

            var reconnectTask = client.ReconnectAsync(cancellationToken);
            await secondClientHelloReceived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            var inProgress = Assert.IsType<AuthoritativeInitialConnectionObservation>(
                client.InitialConnectionObservation);
            Assert.Equal(2L, inProgress.AttemptEpoch);
            Assert.Null(inProgress.ServerHello);
            Assert.Null(inProgress.FirstWireSnapshot);
            Assert.False(inProgress.AdmissionCompleted);

            allowSecondHandshake.TrySetResult(true);
            await reconnectTask;
            var afterReconnect = Assert.IsType<AuthoritativeInitialConnectionObservation>(
                client.InitialConnectionObservation);
            Assert.Equal(2L, afterReconnect.AttemptEpoch);
            Assert.True(afterReconnect.AdmissionCompleted);
            Assert.True(afterReconnect.ServerHello!.ResumedSessionMatched);
            Assert.Equal(40UL, afterReconnect.ServerHello.LastClientSequence);
            Assert.Equal(20UL, afterReconnect.FirstWireSnapshot!.Tick);
            Assert.Equal(41UL, client.Queue(
                new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
                requestedTick: 21));
            await serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        } finally {
            allowSecondHandshake.TrySetResult(true);
        }
    }

    private static Snapshot CreateSnapshot(ulong tick)
        => new() {
            Tick = tick,
            ContentManifestHash = "content-hash",
            RulesetIdentityHash = "ruleset-hash",
            Players = { new PlayerSnapshot { EntityId = 7 } },
        };

    private static async Task WriteHandshakeAsync(
        NetworkStream stream,
        string sessionCredential,
        ulong lastClientSequence,
        Snapshot snapshot,
        CancellationToken cancellationToken)
    {
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "observation-server",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                RulesetIdentityHash = "ruleset-hash",
                TickRateHz = 20,
                GameMode = GameMode.SinglePlayer,
                CommandSchedulingMode = CommandSchedulingMode.ClockGated,
                LastClientSequence = lastClientSequence,
                SessionToken = sessionCredential,
            },
        }, cancellationToken);
        await WriteEnvelopeAsync(stream, new Envelope { Snapshot = snapshot }, cancellationToken);
    }

    private static async Task<Envelope> ReadEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
            throw new InvalidDataException("Test server received an invalid envelope length.");
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task WriteEnvelopeAsync(
        NetworkStream stream,
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ToByteArray();
        if (payload.Length == 0 || payload.Length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
            throw new InvalidDataException("Test server cannot write an invalid envelope length.");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Test client hello ended before its envelope was complete.");
            offset += read;
        }
    }
}