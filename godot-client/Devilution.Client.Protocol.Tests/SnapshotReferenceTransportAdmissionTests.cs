using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class SnapshotReferenceTransportAdmissionTests
{
    [Fact]
    public async Task ReferenceAwareClientsStillConnectToPeersWithoutTheNewCapability()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try {
            var server = Task.Run(async () => {
                using var connection = await listener.AcceptTcpClientAsync(token);
                var stream = connection.GetStream();
                await EnvelopeCodec.ReadAsync(stream, token);
                await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = new() {
                    ProtocolSchemaVersion = "0.2.0", ContentManifestHash = "content-hash", RulesetIdentityHash = "ruleset-hash",
                    SessionToken = "test-session", TickRateHz = 20, GameMode = GameMode.SinglePlayer,
                } }, token);
                await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = new() {
                    Tick = 10, ContentManifestHash = "content-hash", RulesetIdentityHash = "ruleset-hash",
                    Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64 } },
                } }, token);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }, token);
            await using var client = new AuthoritativeClient(new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                "snapshot-reference-test", "0.2.0", "content-hash", RulesetIdentityHash: "ruleset-hash") { UseSnapshotReferences = true });
            try {
                await client.ConnectAsync(token);
                var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
                var model = new AuthoritativeClientModel();
                model.Apply(first);
                Assert.NotEmpty(model.Snapshot!.Players);
                Assert.Equal(0UL, model.Snapshot.GeometryRevision);
            } finally {
                deadline.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server);
            }
        } finally { listener.Stop(); }
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, true)]
    public async Task ATransportAttemptAlwaysRequiresACompleteInitialSnapshot(bool geometry, bool body)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try {
            var featureRequested = false;
            var server = Task.Run(async () => {
                using var connection = await listener.AcceptTcpClientAsync(token);
                var stream = connection.GetStream();
                var hello = await EnvelopeCodec.ReadAsync(stream, token);
                featureRequested = hello!.ClientHello.SupportsSnapshotReferences;
                await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = new() {
                    ProtocolSchemaVersion = "0.2.0", ContentManifestHash = "content-hash", RulesetIdentityHash = "ruleset-hash",
                    SessionToken = "test-session", TickRateHz = 20, GameMode = GameMode.SinglePlayer,
                    SupportsSnapshotReferences = true,
                } }, token);
                await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = new() {
                    Tick = 10, ContentManifestHash = "content-hash", RulesetIdentityHash = "ruleset-hash",
                    GeometryRevision = 1, GeometryUnchanged = geometry, StateRevision = 1, StateUnchanged = body,
                } }, token);
            }, token);
            await using var client = new AuthoritativeClient(new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                "snapshot-reference-test", "0.2.0", "content-hash", RulesetIdentityHash: "ruleset-hash") { UseSnapshotReferences = true });
            await Assert.ThrowsAsync<InvalidDataException>(() => client.ConnectAsync(token));
            Assert.False(client.InitialConnectionObservation!.AdmissionCompleted);
            Assert.NotNull(client.InitialConnectionObservation.FirstWireSnapshot);
            await server.WaitAsync(TimeSpan.FromSeconds(10), token);
            Assert.True(featureRequested);
        } finally { listener.Stop(); }
    }
}
