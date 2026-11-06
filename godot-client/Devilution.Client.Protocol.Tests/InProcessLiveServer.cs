using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;

namespace Devilution.Client.Protocol.Tests;

/** Hosts production TCP, content and game adapters without the standalone executable's startup work. */
internal sealed class InProcessLiveServer : IDisposable
{
    private AuthoritativeTcpServer? server;
    private LeasedAuthoritativeGameHostAdapter? adapter;
    private AuthoritativeCohortStore? cohortStore;
    private HostCommandLog? commandLog;
    private bool disposed;
    private readonly ObservationTimeProvider observationTime = new();
    private RealtimeAuthoritativeClock? clock;
    private IDisposable? controlledClock;

    public LiveServerProcess.Identity Identity { get; private set; }
    public bool HasStopped { get; private set; }
    public ulong CurrentTick => clock!.CurrentTick;
    public bool IsClockHeld => observationTime.IsHeld;
    public IDisposable HoldClockForObservation(ulong? tick = null)
    {
        var hold = observationTime.Hold();
        if (tick is { } boundary)
            clock!.RestoreAtTick(boundary);
        return hold;
    }
    public void AdvanceObservedClock(ulong tick)
    {
        if (!IsClockHeld)
            throw new InvalidOperationException("Advancing an observation requires a held clock.");
        clock!.RestoreAtTick(tick);
    }

    public static InProcessLiveServer Start(string repositoryRoot, IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool controlClock = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ServerHostOptions.TryParse(arguments, out var options, out var error))
            throw new ArgumentException(error, nameof(arguments));
        var host = new InProcessLiveServer();
        try {
            host.StartCore(repositoryRoot, options, arguments, controlClock);
            cancellationToken.ThrowIfCancellationRequested();
            return host;
        } catch (Exception failure) {
            try { host.Dispose(); }
            catch (Exception cleanup) { failure.Data["InProcessHostCleanupFailure"] = cleanup; }
            throw;
        }
    }

    private void StartCore(string repositoryRoot, ServerHostOptions options, IReadOnlyList<string> arguments, bool controlClock)
    {
        // Match Program's content, activation, clock, admission and persistence configuration.
        // Startup/operator/crash tests continue to run Program in a separate process.
        cohortStore = options.UseCohortPersistence ? new AuthoritativeCohortStore(options.SaveRoot) : null;
        if (cohortStore is not null) {
            if (cohortStore.Load() is not null
                && !File.Exists(Path.Combine(cohortStore.RootDirectory, AuthoritativeWorldSeedStore.FileName)))
                throw new InvalidDataException("The cohort root is missing its original world seed.");
            AuthoritativeTcpServer.ValidateCohortRootForStartup(cohortStore);
        }
        var contentRoot = Path.GetFullPath(Path.Combine(repositoryRoot, options.ContentRoot));
        var packs = new List<ContentPack> {
            File.Exists(Path.Combine(contentRoot, ContentPackLoader.ManifestFileName))
                ? ContentPackLoader.LoadManifestDirectory(contentRoot)
                : ContentPackLoader.LoadDirectory(options.ContentId, options.ContentVersion, contentRoot),
        };
        packs.AddRange(options.ContentOverlayRoots.Select(ContentPackLoader.LoadManifestDirectory));
        var content = new ContentManifest(options.ContentId, options.ContentVersion, packs);
        var packages = options.GameHostAdapterPackageRoots
            .Select(new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory).ToArray();
        var catalog = new AuthoritativeGameHostAdapterCatalog(
            BuiltInAuthoritativeGameHostAdapters.CreateFactories(options.GameAdapterId)
                .Concat(packages.Select(package => package.Factory)), options.GameAdapterId);
        var modules = catalog.CreateBuiltInModules();
        var moduleLoader = new GameplayModulePackageLoader(modules.Select(module => module.GetType().Assembly));
        var candidates = modules.Concat(options.GameplayModulePackageRoots
            .SelectMany(root => moduleLoader.LoadDirectory(root).Modules)).ToArray();
        var seedStore = cohortStore?.CreateWorldSeedStore() ?? new AuthoritativeWorldSeedStore(options.SaveRoot);
        var seed = seedStore.LoadOrCreate(options.RandomSeed);
        // Combat fixtures must not consume ambient RNG during connection setup.
        // Only their explicit game-condition observations advance this per-host clock.
        if (controlClock)
            controlledClock = observationTime.Hold();
        clock = new RealtimeAuthoritativeClock(options.TickRateHz, observationTime);
        var start = new AuthoritativePlayerStartConfiguration(options.StartingGold, options.StartingLife, options.StartingMana) {
            StatPoints = options.StartingStatPoints,
            StartingLevelId = options.StartingLevelId,
            RandomSeed = seed,
            GameMode = options.GameMode,
            Difficulty = options.Difficulty,
            TestParticipantDefinition = options.TestParticipantPath is { } path ? File.ReadAllText(path) : null,
            ProceduralMonsterLimit = options.ProceduralMonsterLimit,
            ProceduralMonsterLevelId = options.ProceduralMonsterLevelId,
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            content, candidates, clock, (composed, registry) => catalog.Activate(registry, composed, start));
        adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        if (options.CommandLog) {
            // Separate runs share a save root but never a command-log file, even in the same second/PID.
            commandLog = new HostCommandLog(Path.Combine(options.SaveRoot, "test-command-logs", Guid.NewGuid().ToString("N")),
                arguments, seed);
        }
        var commands = new AuthoritativeCommandServer(adapter, CommandAdmissionPolicy.ForLiveHost(options.TickRateHz)) {
            CompletedCommandWindow = options.UseCohortPersistence && options.UseClockGatedCommandScheduling
                ? ServerHostOptions.CohortCompletedCommandWindow : null,
            CommandLog = commandLog,
        };
        var handshake = new ProtocolHandshake(ProtocolServerIdentity.FromRuleset(
            options.BuildId, options.ProtocolSchemaVersion, options.TickRateHz, runtime.Active.Identity, options.GameMode));
        server = new AuthoritativeTcpServer(commands, handshake, clock, adapter, options.Port, options.BindAddress,
            options.UseCohortPersistence ? null : new AuthoritativeSaveStore(options.SaveRoot)) {
            UseClockGatedCommandScheduling = options.UseClockGatedCommandScheduling,
            CohortStore = cohortStore,
            CommandLog = commandLog,
            CohortCheckpointInterval = options.UseClockGatedCommandScheduling
                ? TimeSpan.FromSeconds(options.CohortCheckpointIntervalSeconds) : null,
        };
        server.Start();
        Identity = new(server.Port, content.Sha256, runtime.Active.Identity.CombinedSha256);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        // Join every host task and background checkpoint before releasing the root for reuse.
        // If joining fails, the outer fixture retains the root and reports its cleanup failure.
        var stopped = server?.DisposeAsync().AsTask();
        Exception? failure = null;
        try {
            stopped?.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        } catch (Exception exception) {
            // An incomplete join still owns its adapter/store. Retain them and the save root.
            if (stopped?.IsCompleted == false)
                throw;
            failure = exception;
        }
        HasStopped = true;
        controlledClock?.Dispose();
        DisposeOwned(adapter, "adapter");
        DisposeOwned(cohortStore, "cohort-store");
        DisposeOwned(commandLog, "command-log");
        disposed = true;
        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();

        void DisposeOwned(IDisposable? resource, string stage)
        {
            try { resource?.Dispose(); }
            catch (Exception exception) {
                if (failure is null)
                    failure = exception;
                else
                    failure.Data["InProcessCleanup." + stage] = exception;
            }
        }
    }
    // A single test may inspect a transient game phase without racing the machine's load.
    // Unheld hosts use the system timestamp at their normal advertised rate.
    private sealed class ObservationTimeProvider : TimeProvider
    {
        private readonly object synchronization = new();
        private long? held;
        private long pausedTimestamps;
        private int holdDepth;
        public bool IsHeld { get { lock (synchronization) return held is not null; } }
        public override long TimestampFrequency => global::System.Diagnostics.Stopwatch.Frequency;
        public override long GetTimestamp()
        {
            lock (synchronization)
                return held ?? (global::System.Diagnostics.Stopwatch.GetTimestamp() - pausedTimestamps);
        }
        public IDisposable Hold()
        {
            lock (synchronization) {
                if (holdDepth++ == 0)
                    held = global::System.Diagnostics.Stopwatch.GetTimestamp() - pausedTimestamps;
                return new Observation(this);
            }
        }
        private sealed class Observation(ObservationTimeProvider owner) : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                lock (owner.synchronization) {
                    if (disposed)
                        return;
                    disposed = true;
                    if (--owner.holdDepth != 0)
                        return;
                    // Observing a phase pauses game time; resuming must not replay the
                    // entire wall-clock observation interval as an artificial tick jump.
                    owner.pausedTimestamps = global::System.Diagnostics.Stopwatch.GetTimestamp() - owner.held!.Value;
                    owner.held = null;
                }
            }
        }
    }

}
