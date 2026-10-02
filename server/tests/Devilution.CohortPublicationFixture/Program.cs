using System.Net;
using System.Text.Json;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;

namespace Devilution.CohortPublicationFixture;

/** Identifies the separately built managed child; this is not a production CLI. */
public static class FixtureAssemblyMarker { }

internal static class Program
{
    private const uint Seed = 0x12345678;
    private static readonly string IdentityHash = new('f', 64);

    public static async Task<int> Main(string[] args)
    {
        try {
            if (args.Length != 5 || args[0] is not ("interrupt" or "recover")
                || args[2] is not ("seed" or "cohort")
                || !int.TryParse(args[4], out var port) || port is < 1 or > 65535)
                throw new ArgumentException("Expected action, owned root, source, phase and loopback port.");
            var interrupt = args[0] == "interrupt";
            if (interrupt && !Enum.GetNames<AuthoritativePublicationPhase>().Contains(args[3], StringComparer.Ordinal))
                throw new ArgumentException("Unknown publication phase.");
            var phase = interrupt ? Enum.Parse<AuthoritativePublicationPhase>(args[3]) : default;
            var source = args[2];
            using var store = new AuthoritativeCohortStore(args[1]);
            if (store.Load() is not null
                && !File.Exists(Path.Combine(store.RootDirectory, AuthoritativeWorldSeedStore.FileName)))
                throw new InvalidDataException("A canonical cohort cannot regenerate its missing original seed.");
            AuthoritativeTcpServer.ValidateCohortRootForStartup(store);
            var seedStore = store.CreateWorldSeedStore();

            void Observe(AuthoritativePublicationPhase observed, string path)
            {
                if (!interrupt || observed != phase)
                    return;
                Console.WriteLine("BOUNDARY:" + JsonSerializer.Serialize(new {
                    Source = source,
                    Phase = observed.ToString(),
                    StagePath = path,
                    Port = port,
                }));
                Console.Out.Flush();
                // The parent owns this process, checks the closed listener, and
                // kills this exact child. No finally/cleanup can simulate a crash.
                using var pause = new ManualResetEvent(false);
                pause.WaitOne();
            }

            if (source == "seed")
                seedStore.PublicationObserver = Observe;
            else
                store.PublicationObserver = Observe;
            var seed = seedStore.LoadOrCreate(Seed);
            var executor = new StoreSimulationExecutor(
                new StoreCatalog(),
                startingGold: 0,
                startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
                startingRandomSeed: seed,
                gameMode: DiabloGameMode.Multiplayer);
            var clock = new DelegateAuthoritativeClock(() => 0);
            var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
                "publication-fixture", "0.1.0", IdentityHash, 20) {
                RulesetIdentityHash = IdentityHash,
                GameMode = AuthoritativeGameMode.Multiplayer,
            });
            await using var server = new AuthoritativeTcpServer(
                new AuthoritativeCommandServer(executor), handshake, clock, executor,
                port: port, address: IPAddress.Loopback) {
                CohortStore = store,
            };
            server.Start();
            var checkpoint = store.Load() ?? throw new InvalidDataException("Host exposed no canonical cohort.");
            Console.WriteLine("READY:" + JsonSerializer.Serialize(new {
                checkpoint.Generation,
                checkpoint.Tick,
                ParticipantCount = checkpoint.Participants.Count,
                Seed = seed,
                Port = server.Port,
            }));
            Console.Out.Flush();
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
