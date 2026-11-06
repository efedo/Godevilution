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

    public LiveServerProcess.Identity Identity { get; private set; }
    public bool HasStopped { get; private set; }

    public static InProcessLiveServer Start(string repositoryRoot, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ServerHostOptions.TryParse(arguments, out var options, out var error))
            throw new ArgumentException(error, nameof(arguments));
        var host = new InProcessLiveServer();
        try {
            host.StartCore(repositoryRoot, options, arguments);
            cancellationToken.ThrowIfCancellationRequested();
            return host;
        } catch (Exception failure) {
            try { host.Dispose(); }
            catch (Exception cleanup) { failure.Data["InProcessHostCleanupFailure"] = cleanup; }
            throw;
        }
    }

    private void StartCore(string repositoryRoot, ServerHostOptions options, IReadOnlyList<string> arguments)
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
        var clock = new RealtimeAuthoritativeClock(options.TickRateHz);
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
}
