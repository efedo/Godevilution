using System.Net;
using System.Net.Sockets;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeTcpServerTargetCellPathTests
{
    [Theory]
    [InlineData(AuthoritativeProtocolFeatures.None, false)]
    [InlineData(AuthoritativeProtocolFeatures.TargetCellPathing, true)]
    public async Task ServerHelloAdvertisesOnlyActiveAdapterFeatures(
        AuthoritativeProtocolFeatures features,
        bool expectedTargetCellPathing)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var adapter = new FeatureAdapter(features);
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            new ContentManifest("target-path", "1", [new ContentPack(
                "base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]),
            [new TestModule()],
            new Devilution.Server.Simulation.DelegateAuthoritativeClock(() => 100),
            (_, _) => adapter);
        using var leasedAdapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var commandServer = new AuthoritativeCommandServer(leasedAdapter);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "target-path-test-server",
            "0.1.0",
            "target-path-content",
            20));
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            new Devilution.Server.Simulation.DelegateAuthoritativeClock(() => 100),
            leasedAdapter);
        server.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, new Envelope { ClientHello = new ClientHello {
            ClientBuildId = "target-path-test-client",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "target-path-content",
        } }, timeout.Token);

        var hello = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);
        Assert.Equal(expectedTargetCellPathing, hello.ServerHello.SupportsTargetCellPathing);
        var initialSnapshot = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
        Assert.NotNull(initialSnapshot);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, initialSnapshot!.PayloadCase);
    }

    private sealed class FeatureAdapter(AuthoritativeProtocolFeatures features)
        : IAuthoritativeGameHostAdapter
    {
        public AuthoritativeProtocolFeatures SupportedProtocolFeatures => features;

        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
        {
        }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() { Tick = tick };

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => null;

        public string ExportPlayerSave(string sessionId)
            => "target-path-save";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }
    }

    private sealed class TestModule : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = new("target-path", "1", "target-path-module");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }
}
