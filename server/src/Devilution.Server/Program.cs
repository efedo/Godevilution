using Devilution.Server.Commands;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Devilution.Server;

internal static class Program
{
    private delegate IReadOnlyList<IGameplayModule> ModuleCandidateLoader(
        out LoadedGameplayModulePackage[] loadedPackages);

    public static async Task<int> Main(string[] args)
    {
        if (!ServerHostOptions.TryParse(args, out var options, out var error)) {
            if (!string.IsNullOrEmpty(error))
                Console.Error.WriteLine(error);
            Console.WriteLine(ServerHostOptions.Usage);
            return string.IsNullOrEmpty(error) ? 0 : 2;
        }

        try {
            return await RunAsync(options);
        } catch (Exception exception) {
            Console.Error.WriteLine($"Server startup failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(ServerHostOptions options)
    {
        // Lease precedes every seed/save-root write, including startup failures.
        using var cohortStore = options.UseCohortPersistence
            ? new AuthoritativeCohortStore(options.SaveRoot) : null;
        if (cohortStore is not null) {
            AuthoritativeTcpServer.ValidateCohortRootForStartup(cohortStore);
            if (cohortStore.Load() is not null
                && !File.Exists(Path.Combine(cohortStore.RootDirectory, AuthoritativeWorldSeedStore.FileName)))
                throw new InvalidDataException("The cohort root is missing its original world seed. Preserve the complete root; no seed is regenerated.");
        }
        // Reserve a new output before binding. Never overwrite an existing artifact.
        using var performanceOutput = options.PerformanceOutput is null ? null
            : new FileStream(Path.GetFullPath(options.PerformanceOutput), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var performance = performanceOutput is null ? null : new HostPerformanceRecorder();
        var contentRoot = Path.GetFullPath(options.ContentRoot);
        ContentManifest LoadContentManifest()
        {
            var contentPacks = new List<ContentPack> {
                LoadBaseContentPack(options, contentRoot),
            };
            foreach (var overlayRoot in options.ContentOverlayRoots)
                contentPacks.Add(ContentPackLoader.LoadManifestDirectory(overlayRoot));
            return new ContentManifest(
                options.ContentId,
                options.ContentVersion,
                contentPacks);
        }

        var contentManifest = LoadContentManifest();
        var saveStore = options.UseCohortPersistence ? null : new AuthoritativeSaveStore(options.SaveRoot);
        var worldSeedStore = new AuthoritativeWorldSeedStore(options.SaveRoot);
        var clock = new RealtimeAuthoritativeClock(options.TickRateHz);
        var adapterPackageLoader = new AuthoritativeGameHostAdapterPackageLoader();
        var adapterPackages = options.GameHostAdapterPackageRoots
            .Select(adapterPackageLoader.LoadDirectory)
            .ToArray();
        var duplicateAdapterPackageIds = adapterPackages
            .GroupBy(package => package.PackageId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateAdapterPackageIds.Length != 0)
            throw new InvalidDataException(
                $"Game-host adapter package IDs must be unique: {string.Join(", ", duplicateAdapterPackageIds)}.");
        var adapterFactories = BuiltInAuthoritativeGameHostAdapters.CreateFactories(options.GameAdapterId)
            .Concat(adapterPackages.Select(package => package.Factory))
            .ToArray();
        var gameAdapters = new AuthoritativeGameHostAdapterCatalog(
            adapterFactories,
            options.GameAdapterId);
        var builtInModules = gameAdapters.CreateBuiltInModules();
        var modulePackageLoader = new GameplayModulePackageLoader(
            builtInModules.Select(module => module.GetType().Assembly));
        IReadOnlyList<IGameplayModule> LoadModuleCandidates(
            out LoadedGameplayModulePackage[] loadedPackages)
        {
            loadedPackages = options.GameplayModulePackageRoots
                .Select(modulePackageLoader.LoadDirectory)
                .ToArray();
            var duplicatePackageIds = loadedPackages
                .GroupBy(package => package.PackageId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePackageIds.Length != 0) {
                throw new InvalidDataException(
                    $"Gameplay module package IDs must be unique: {string.Join(", ", duplicatePackageIds)}.");
            }

            var candidates = gameAdapters.CreateBuiltInModules().ToList();
            foreach (var package in loadedPackages)
                candidates.AddRange(package.Modules);
            return candidates;
        }

        var moduleCandidates = LoadModuleCandidates(out var modulePackages);
        var randomSeed = worldSeedStore.LoadOrCreate(options.RandomSeed);
        var playerStart = new AuthoritativePlayerStartConfiguration(
            options.StartingGold,
            options.StartingLife,
            options.StartingMana) {
            StatPoints = options.StartingStatPoints,
            StartingLevelId = options.StartingLevelId,
            RandomSeed = randomSeed,
            GameMode = options.GameMode,
            Difficulty = options.Difficulty,
            ProceduralMonsterLimit = options.ProceduralMonsterLimit,
            ProceduralMonsterLevelId = options.ProceduralMonsterLevelId,
        };
        var rulesetRuntime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            contentManifest,
            moduleCandidates,
            clock,
            (content, registry) => gameAdapters.Activate(
                registry,
                content,
                playerStart));
        var activeRuleset = rulesetRuntime.Active;
        var modulePlan = activeRuleset.ModulePlan;
        var ruleset = activeRuleset.Identity;
        var gameAdapter = new LeasedAuthoritativeGameHostAdapter(rulesetRuntime);
        var commandServer = new AuthoritativeCommandServer(gameAdapter);
        var handshake = new ProtocolHandshake(ProtocolServerIdentity.FromRuleset(
            options.BuildId,
            options.ProtocolSchemaVersion,
            options.TickRateHz,
            ruleset,
            options.GameMode));

        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            clock,
            gameAdapter,
            options.Port,
            options.BindAddress,
            saveStore) {
            PerformanceRecorder = performance,
            UseClockGatedCommandScheduling = options.UseClockGatedCommandScheduling,
            CohortStore = cohortStore,
            ClientFailureObserver = exception => Console.Error.WriteLine(
                $"Authoritative session failed: {exception.GetType().Name}: {exception.Message}"),
        };
        server.Start();
        Console.WriteLine($"Godevilution authoritative server listening on {options.BindAddress}:{server.Port}");
        Console.WriteLine($"Content packs: {string.Join(", ", contentManifest.Packs.Select(pack => $"{pack.Id}@{pack.Version}"))}");
        if (modulePackages.Length != 0) {
            Console.WriteLine($"Module packages: {string.Join(", ", modulePackages.Select(package => $"{package.PackageId}@{package.PackageVersion}"))}");
        }
        if (adapterPackages.Length != 0) {
            Console.WriteLine(
                $"Game-host adapter packages: {string.Join(", ", adapterPackages.Select(package => $"{package.PackageId}@{package.PackageVersion}:{package.Factory.Id}"))}");
        }
        foreach (var diagnostic in modulePlan.Diagnostics)
            Console.WriteLine($"Module {diagnostic.ModuleId}: {diagnostic.Message}");
        Console.WriteLine($"Content manifest: {contentManifest.Sha256}");
        Console.WriteLine($"Ruleset identity: {ruleset.CombinedSha256}");
        Console.WriteLine($"Protocol schema: {options.ProtocolSchemaVersion}");
        Console.WriteLine($"Server/world RNG seed: {randomSeed}");
        Console.WriteLine($"Game mode: {options.GameMode}");
        Console.WriteLine($"Command scheduling: {(options.UseClockGatedCommandScheduling ? "clock-gated" : "immediate")}");
        Console.WriteLine($"Persistence: {(options.UseCohortPersistence ? "complete cohort" : "legacy per-session")}");
        if (options.ProceduralMonsterLimit is int proceduralMonsterLimit)
            Console.WriteLine(
                $"Procedural monsters: max {proceduralMonsterLimit}"
                + (options.ProceduralMonsterLevelId is uint levelId
                    ? $" on level {levelId}"
                    : " on the default starting level"));
        Console.WriteLine("Operator commands: status, reload, help, quit; profile-start (requires --performance-output).");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        var operatorLoop = RunOperatorLoopAsync(
            server,
            LoadContentManifest,
            LoadModuleCandidates,
            cancellation.Cancel,
            cancellation.Token);
        Exception? hostFailure = null;
        try {
            var stopped = Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
            await Task.WhenAny(stopped, server.Completion);
            if (server.Completion.IsCompleted)
                await server.Completion;
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            hostFailure = exception;
        } finally {
            cancellation.Cancel();
            try {
                await operatorLoop;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            } catch (Exception exception) {
                hostFailure ??= exception;
            }
        }
        if (hostFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(hostFailure).Throw();
        if (performanceOutput is not null && performance is not null) {
            var report = performance.Begin() != 0 ? performance.Stop() : null;
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            await JsonSerializer.SerializeAsync(performanceOutput, new {
                SchemaVersion = 1,
                Status = report is null ? "NotStarted" : "Completed",
                Report = report,
                options.TickRateHz,
                GameMode = options.GameMode.ToString(),
                ActiveParticipants = server.ActiveSessionIds.Count,
                ContentHash = server.ActiveContentManifestHash,
                RulesetHash = server.ActiveRulesetIdentityHash,
                StartupRulesetHash = ruleset.CombinedSha256,
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                LogicalProcessors = Environment.ProcessorCount,
                WorkingSetBytes = process.WorkingSet64,
                PeakWorkingSetBytes = process.PeakWorkingSet64,
                PrivateMemoryBytes = process.PrivateMemorySize64,
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        return 0;
    }

    private static async Task RunOperatorLoopAsync(
        AuthoritativeTcpServer server,
        Func<ContentManifest> loadContentManifest,
        ModuleCandidateLoader loadModuleCandidates,
        Action requestShutdown,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            string? line;
            try {
                line = await Console.In.ReadLineAsync(cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            }
            if (line is null)
                return;

            switch (ServerOperatorCommand.Parse(line)) {
            case ServerOperatorCommandKind.Empty:
                continue;
            case ServerOperatorCommandKind.Help:
                Console.WriteLine("Commands: status, reload, help, quit; profile-start (requires --performance-output).");
                break;
            case ServerOperatorCommandKind.ProfileStart:
                Console.WriteLine(server.PerformanceRecorder?.Start() == true
                    ? "Performance recording started."
                    : "Performance recording unavailable or already started.");
                break;
            case ServerOperatorCommandKind.Status:
                Console.WriteLine($"Active sessions: {server.ActiveSessionIds.Count}");
                Console.WriteLine($"Listening: {server.Port}");
                Console.WriteLine($"Ruleset identity: {server.ActiveRulesetIdentityHash}");
                Console.WriteLine($"Retired rulesets awaiting disposal: {server.RetiredRulesetCount}");
                break;
            case ServerOperatorCommandKind.Reload:
                await ReloadRulesetAsync(
                    server,
                    loadContentManifest,
                    loadModuleCandidates);
                break;
            case ServerOperatorCommandKind.Quit:
                requestShutdown();
                return;
            default:
                Console.WriteLine("Unknown command. Use 'help'.");
                break;
            }
        }
    }

    private static Task ReloadRulesetAsync(
        AuthoritativeTcpServer server,
        Func<ContentManifest> loadContentManifest,
        ModuleCandidateLoader loadModuleCandidates)
    {
        try {
            var candidateManifest = loadContentManifest();
            var candidateModules = loadModuleCandidates(out var modulePackages);
            var result = server.TryReloadRuleset(candidateManifest, candidateModules);
            if (!result.Succeeded) {
                Console.WriteLine("Reload rejected:");
                foreach (var diagnostic in result.Diagnostics)
                    Console.WriteLine($"  {diagnostic}");
                return Task.CompletedTask;
            }

            Console.WriteLine($"Reloaded ruleset: {result.Active.Identity.CombinedSha256}");
            Console.WriteLine($"Content manifest: {candidateManifest.Sha256}");
            if (modulePackages.Length != 0)
                Console.WriteLine($"Module packages: {string.Join(", ", modulePackages.Select(package => $"{package.PackageId}@{package.PackageVersion}"))}");
        } catch (Exception exception) {
            Console.WriteLine($"Reload failed: {exception.Message}");
        }
        return Task.CompletedTask;
    }

    private static ContentPack LoadBaseContentPack(
        ServerHostOptions options,
        string contentRoot)
    {
        return File.Exists(Path.Combine(contentRoot, ContentPackLoader.ManifestFileName))
            ? ContentPackLoader.LoadManifestDirectory(contentRoot)
            : ContentPackLoader.LoadDirectory(
                options.ContentId,
                options.ContentVersion,
                contentRoot);
    }
}
