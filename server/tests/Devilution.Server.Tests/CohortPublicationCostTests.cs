using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/**
 * Explicit local measurement of the clock-gated complete-cohort snapshot poll on a
 * saved all-class native session (for example the L8 depth-progression Journey).
 */
public sealed class CohortPublicationCostTests
{
    private const int MeasuredPolls = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    [Fact(Explicit = true)]
    public async Task SavedNativeCohortSnapshotPollStaysWellBelowThePollInterval()
    {
        var profileRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_PROFILE_ROOT");
        var sessionRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_SESSION");
        if (string.IsNullOrWhiteSpace(profileRoot) || string.IsNullOrWhiteSpace(sessionRoot)) {
            Assert.Skip("Set GODEVILUTION_COHORT_COST_PROFILE_ROOT and GODEVILUTION_COHORT_COST_SESSION.");
            return;
        }

        var repositoryRoot = FindRepositoryRoot();
        var ownedRoot = Path.Combine(repositoryRoot, ".local", "cohort-cost-tests", Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.Combine(sessionRoot, "save"), ownedRoot);
        var performance = new HostPerformanceRecorder();
        AuthoritativeTcpServer? server = null;
        try {
            server = CreateHost(profileRoot, ownedRoot,
                File.ReadAllText(Path.Combine(repositoryRoot, "tools", "native-test-heroes", "warrior-l8.json")),
                performance);
            server.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, new Envelope {
                ClientHello = new ClientHello {
                    ClientBuildId = "cohort-cost-test-client",
                    ProtocolSchemaVersion = "0.2.0",
                    ContentManifestHash = server.ActiveContentManifestHash,
                    RulesetIdentityHash = server.ActiveRulesetIdentityHash,
                    ResumeToken = File.ReadAllText(Path.Combine(sessionRoot, "resume-token.txt")).Trim(),
                    SupportsClockGatedCommands = true,
                },
            }, timeout.Token);
            await ReadUntilAsync(stream, Envelope.PayloadOneofCase.Snapshot, timeout.Token);

            // Warm up the JIT and serializer caches before the measured polls.
            for (var index = 0; index < 3; index++)
                await PollAsync(stream, timeout.Token);
            performance.Start();
            var roundTrips = new List<double>();
            for (var index = 0; index < MeasuredPolls; index++) {
                var started = Stopwatch.GetTimestamp();
                await PollAsync(stream, timeout.Token);
                var elapsed = Stopwatch.GetElapsedTime(started);
                roundTrips.Add(elapsed.TotalMilliseconds);
                // Like the client, the next request starts one interval after this one did.
                if (elapsed < PollInterval)
                    await Task.Delay(PollInterval - elapsed, timeout.Token);
            }

            var materialize = performance.Stop().Metrics[nameof(HostPerformanceMetric.SnapshotMaterializeMilliseconds)];
            var cohortBytes = new FileInfo(Path.Combine(ownedRoot, AuthoritativeCohortStore.CheckpointFileName)).Length;
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"cohort {cohortBytes} bytes; poll round trip median {Median(roundTrips):F1} ms, max {roundTrips.Max():F1} ms; "
                + $"materialize {materialize}");
            Assert.True(Median(roundTrips) < 125, $"Median poll round trip {Median(roundTrips):F1} ms.");
        } finally {
            if (server is not null) {
                try { await server.DisposeAsync(); }
                finally { server.CommandLog?.Dispose(); }
            }
            try { Directory.Delete(ownedRoot, recursive: true); } catch (IOException) { }
        }
    }

    /** Timed capture cost and deferred byte parity on the saved native cohort. */
    [Fact(Explicit = true)]
    public async Task SavedNativeCohortCheckpointCapturesUnderTheLockAndWritesOffIt()
    {
        var profileRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_PROFILE_ROOT");
        var sessionRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_SESSION");
        if (string.IsNullOrWhiteSpace(profileRoot) || string.IsNullOrWhiteSpace(sessionRoot)) {
            Assert.Skip("Set GODEVILUTION_COHORT_COST_PROFILE_ROOT and GODEVILUTION_COHORT_COST_SESSION.");
            return;
        }

        var repositoryRoot = FindRepositoryRoot();
        var ownedRoot = Path.Combine(repositoryRoot, ".local", "cohort-cost-tests", Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.Combine(sessionRoot, "save"), ownedRoot);
        var performance = new HostPerformanceRecorder();
        AuthoritativeTcpServer? server = null;
        AuthoritativeCommandServer? commands = null;
        LeasedAuthoritativeGameHostAdapter? adapter = null;
        try {
            server = CreateHost(profileRoot, ownedRoot,
                File.ReadAllText(Path.Combine(repositoryRoot, "tools", "native-test-heroes", "warrior-l8.json")),
                performance, TimeSpan.FromSeconds(1), (capturedCommands, capturedAdapter) => {
                    commands = capturedCommands;
                    adapter = capturedAdapter;
                });
            server.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, new Envelope {
                ClientHello = new ClientHello {
                    ClientBuildId = "cohort-cost-test-client",
                    ProtocolSchemaVersion = "0.2.0",
                    ContentManifestHash = server.ActiveContentManifestHash,
                    RulesetIdentityHash = server.ActiveRulesetIdentityHash,
                    ResumeToken = File.ReadAllText(Path.Combine(sessionRoot, "resume-token.txt")).Trim(),
                    SupportsClockGatedCommands = true,
                },
            }, timeout.Token);
            await ReadUntilAsync(stream, Envelope.PayloadOneofCase.Snapshot, timeout.Token);
            for (var index = 0; index < 3; index++)
                await PollAsync(stream, timeout.Token);

            var captured = commands!.WithSynchronizedState(() => (
                Expected: adapter!.CaptureCohortState(),
                Encode: adapter!.CaptureCohortForDeferredEncoding()));
            while (commands.CurrentTick <= captured.Expected.Tick)
                await PollAsync(stream, timeout.Token);
            var deferred = await Task.Run(captured.Encode, timeout.Token);
            Assert.Equal(captured.Expected.SerializedState, deferred.SerializedState);
            Assert.Equal(captured.Expected.Participants, deferred.Participants);

            performance.Start();
            for (var index = 0; index < 60; index++) {
                await PollAsync(stream, timeout.Token);
                await Task.Delay(PollInterval, timeout.Token);
            }
            var metrics = performance.Stop().Metrics;
            var capture = metrics[nameof(HostPerformanceMetric.CohortCheckpointCaptureMilliseconds)];
            var write = metrics[nameof(HostPerformanceMetric.CohortCheckpointWriteMilliseconds)];
            var cohortBytes = new FileInfo(Path.Combine(ownedRoot, AuthoritativeCohortStore.CheckpointFileName)).Length;
            var report = $"cohort {cohortBytes} bytes; capture {capture}; write {write}";
            TestContext.Current.TestOutputHelper?.WriteLine(report);
            Console.WriteLine(report);
            Assert.True(capture.Samples >= 10, report);
            Assert.True(write.Samples >= 10, report);
            Assert.True(capture.Maximum < 1000.0 / 60, report);
            Assert.NotNull(server.CommandLog);
            Assert.True(Directory.GetFiles(Path.Combine(ownedRoot, "command-log"), "checkpoint-*.json").Length >= 10,
                "The attached command log must actually retain the measured checkpoints.");
        } finally {
            if (server is not null) {
                try { await server.DisposeAsync(); }
                finally { server.CommandLog?.Dispose(); }
            }
            try { Directory.Delete(ownedRoot, recursive: true); } catch (IOException) { }
        }
    }

    [Fact(Explicit = true)]
    public void SavedNativeCohortDeferredCaptureRetainsDoorBytesAndDefersValidationFailure()
    {
        var profileRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_PROFILE_ROOT");
        var sessionRoot = Environment.GetEnvironmentVariable("GODEVILUTION_COHORT_COST_SESSION");
        if (string.IsNullOrWhiteSpace(profileRoot) || string.IsNullOrWhiteSpace(sessionRoot)) {
            Assert.Skip("Set GODEVILUTION_COHORT_COST_PROFILE_ROOT and GODEVILUTION_COHORT_COST_SESSION.");
            return;
        }
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(sessionRoot, "save", AuthoritativeCohortStore.CheckpointFileName)));
        var codec = new DiabloCohortCodec();
        var state = codec.Deserialize(document.SerializedModuleState);
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(directory => ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot, directory))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, state.Participants[0].Player.CharacterClassId,
            state.Configuration.InitialRandomSeed, state.Configuration.GameMode);
        var candidate = activation.CohortCandidateFactory!(state);
        var simulation = candidate.Simulation;
        var runtime = candidate.CreateRuntime();
        var expected = codec.Serialize(simulation.CaptureCohortState(runtime));
        var captured = simulation.CaptureCohortForDeferredEncoding(runtime, out var validate);
        var door = captured.NativeCatacombs!.Doors.First(door => door.Status == AuthoritativeCatacombsDoorStatus.Closed);
        Assert.True(simulation.WorldState.TryOperateNativeCatacombsDoor(door.EntityId, []));
        validate();
        Assert.Equal(expected, codec.Serialize(captured));
        Assert.NotEqual(expected, codec.Serialize(simulation.CaptureCohortState(runtime)));

        // A malformed live projection is captured consistently and rejected on the writer.
        var value = simulation.WorldState.Objects[door.EntityId];
        value.AnimationFrame++;
        Assert.Throws<InvalidDataException>(() => simulation.CaptureCohortState(runtime));
        _ = simulation.CaptureCohortForDeferredEncoding(runtime, out var invalidValidation);
        Assert.Throws<InvalidDataException>(invalidValidation);
    }

    private static AuthoritativeTcpServer CreateHost(
        string profileRoot,
        string saveRoot,
        string testParticipant,
        HostPerformanceRecorder performance,
        TimeSpan? checkpointInterval = null,
        Action<AuthoritativeCommandServer, LeasedAuthoritativeGameHostAdapter>? observeAdapter = null)
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(profileRoot, "cathedral-native-all-class-town-playtest.json")));
        var adapterId = metadata.RootElement.GetProperty("adapterId").GetString()!;
        var packs = new[] {
            "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population",
        }.Select(directory => ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot, directory))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var store = new AuthoritativeCohortStore(saveRoot);
        AuthoritativeTcpServer.ValidateCohortRootForStartup(store);
        var seed = store.CreateWorldSeedStore().LoadOrCreate(null);
        var clock = new RealtimeAuthoritativeClock(20);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory(adapterId);
        var playerStart = new AuthoritativePlayerStartConfiguration(null, null, null) {
            TestParticipantDefinition = testParticipant,
            RandomSeed = seed,
            GameMode = AuthoritativeGameMode.SinglePlayer,
            Difficulty = AuthoritativeGameDifficulty.Normal,
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            manifest,
            factory.CreateBuiltInModules(),
            clock,
            (content, registry) => factory.Activate(registry, content, playerStart));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var identity = ProtocolServerIdentity.FromRuleset(
            "cohort-cost-test", "0.2.0", 20, runtime.Active.Identity, AuthoritativeGameMode.SinglePlayer);
        var commandLog = new HostCommandLog(Path.Combine(saveRoot, "command-log"), [], seed,
            failure => Console.Error.WriteLine($"Command log failed: {failure}"));
        var commands = new AuthoritativeCommandServer(adapter) { CommandLog = commandLog };
        observeAdapter?.Invoke(commands, adapter);
        return new AuthoritativeTcpServer(
            commands,
            new ProtocolHandshake(identity),
            clock,
            adapter,
            port: 0,
            address: IPAddress.Loopback) {
            CohortStore = store,
            CommandLog = commandLog,
            UseClockGatedCommandScheduling = true,
            PerformanceRecorder = performance,
            // The production host's default.
            CohortCheckpointInterval = checkpointInterval
                ?? TimeSpan.FromSeconds(ServerHostOptions.DefaultCohortCheckpointIntervalSeconds),
        };
    }

    private static async Task PollAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest(),
        }, cancellationToken);
        await ReadUntilAsync(stream, Envelope.PayloadOneofCase.Snapshot, cancellationToken);
    }

    private static async Task ReadUntilAsync(
        NetworkStream stream,
        Envelope.PayloadOneofCase payload,
        CancellationToken cancellationToken)
    {
        while (true) {
            var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
                ?? throw new IOException("The host closed the connection.");
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Error)
                throw new InvalidDataException(envelope.Error.Detail);
            if (envelope.PayloadCase == payload)
                return;
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
        throw new DirectoryNotFoundException("The repository root could not be located from the test output directory.");
    }
}
