using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

/** The private pack stays local; the archive-free owner projection is covered by NativeShrineHostPublicationTests. */
public sealed partial class NativeShrineStartupTests(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public async Task WarriorL8FirstSnapshotPublishesAndEntersTheClient()
    {
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(profile)) Assert.Skip("Select the private journey profile to exercise its exact first snapshot.");
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var repository = FindRepository();
        var definition = File.ReadAllText(Path.Combine(repository, "tools/native-test-heroes/warrior-l8.json"));
        var factory = new DiabloAuthoritativeGameHostAdapterFactory("devilution.diablo.cathedral-native-all-class-town.v1");
        var clock = new TestClock();
        var start = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 0, GameMode = AuthoritativeGameMode.SinglePlayer, StartingLevelId = 0,
            TestParticipantDefinition = definition,
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(manifest,
            factory.CreateBuiltInModules(), clock, (content, registry) => factory.Activate(registry, content, start));
        var identity = ProtocolServerIdentity.FromRuleset("shrine-startup", "0.2.0", 20, runtime.Active.Identity,
            AuthoritativeGameMode.SinglePlayer);
        var root = Path.Combine(repository, ".local", "sol", "shrine-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var log = new HostCommandLog(Path.Combine(root, "command-log"), [], 0);
            await using var server = new AuthoritativeTcpServer(new AuthoritativeCommandServer(runtime.Active.Target),
                new ProtocolHandshake(identity), clock, runtime.Active.Target, address: System.Net.IPAddress.Loopback) {
                CohortStore = new Devilution.Server.Stores.AuthoritativeCohortStore(Path.Combine(root, "save")),
                UseClockGatedCommandScheduling = true, CommandLog = log,
                ClientFailureObserver = failure => output.WriteLine($"Host refused its initial snapshot: {failure}"),
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            server.Start();
            using var client = new System.Net.Sockets.TcpClient { NoDelay = true };
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, new Envelope { ClientHello = new ClientHello {
                ClientBuildId = "shrine-startup", ProtocolSchemaVersion = identity.ProtocolSchemaVersion,
                ContentManifestHash = identity.ContentManifestHash, RulesetIdentityHash = identity.RulesetIdentityHash,
                RequestedCharacterClassId = 0, RequestedCharacterName = "ShrineStartup", SupportsClockGatedCommands = true,
            } }, timeout.Token);
            var hello = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
            Assert.True(hello?.PayloadCase == Envelope.PayloadOneofCase.ServerHello, $"Initial handshake failed: {server.LastClientFailure}");
            var publication = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
            Assert.True(publication?.PayloadCase == Envelope.PayloadOneofCase.Snapshot, $"Initial snapshot failed: {server.LastClientFailure}");
            var snapshot = publication!.Snapshot;
            output.WriteLine($"Published warrior-l8 first snapshot: tick {snapshot.Tick}, {snapshot.Objects.Count} objects.");
            var shrine = Assert.Single(snapshot.Objects, value => value.NativeObjectId is 59 or 60);
            Assert.Equal((151003193U, 60U, 1U, true, true),
                (shrine.EntityId, shrine.NativeObjectId, shrine.NativeShrineType, shrine.IsShrine, shrine.NativeShrineInteractable));
            var model = new Devilution.Client.Protocol.AuthoritativeClientModel();
            model.Apply(new Devilution.Client.Protocol.AuthoritativeClientMessage(Snapshot: snapshot));
            Assert.Equal((18U, 0U), (model.Snapshot!.Players[0].CharacterLevel, model.Snapshot.Players[0].LevelId));
            Assert.Null(server.LastClientFailure);
            Assert.False(server.Completion.IsCompleted);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    internal static Devilution.Diablo.AuthoritativePlayerState CreateWarriorL8()
    {
        var repository = FindRepository();
        var pack = ContentPackLoader.LoadManifestDirectory(Path.Combine(repository, "server/content/base"));
        var content = ComposedContent.Compose(new ContentManifest(pack.Id, pack.Version, [pack]));
        var activation = Devilution.Diablo.DiabloModule.Instance.ContentBootstrap.Activate(content);
        var test = NativeTestParticipant.Parse(File.ReadAllText(Path.Combine(repository, "tools/native-test-heroes/warrior-l8.json")))
            .ToTestStart(activation.Simulation.Items!);
        var player = activation.PlayerLoadouts.CreateTemplate("native-startup", 0, null, null, null, 0,
            startingPositionX: 75, startingPositionY: 68, testStart: test).CreatePlayer();
        player.EntityId = 1;
        return player;
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "legacy-native"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository is required.");
    }
    private sealed class TestClock : IAuthoritativeClock { public ulong CurrentTick => 32; }
}
