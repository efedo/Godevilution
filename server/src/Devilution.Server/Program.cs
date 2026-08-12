using System.Security.Cryptography;
using Devilution.Server.Commands;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;

namespace Devilution.Server;

internal static class Program
{
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
        var contentRoot = Path.GetFullPath(options.ContentRoot);
        var contentPacks = new List<ContentPack> {
            LoadBaseContentPack(options, contentRoot),
        };
        foreach (var overlayRoot in options.ContentOverlayRoots)
            contentPacks.Add(ContentPackLoader.LoadManifestDirectory(overlayRoot));
        var contentManifest = new ContentManifest(
            options.ContentId,
            options.ContentVersion,
            contentPacks);
        var clock = new RealtimeAuthoritativeClock(options.TickRateHz);
        var gameAdapters = BuiltInAuthoritativeGameHostAdapters.Create();
        var builtInModules = gameAdapters.CreateBuiltInModules();
        var modulePackageLoader = new GameplayModulePackageLoader(
            builtInModules.Select(module => module.GetType().Assembly));
        var modulePackages = options.GameplayModulePackageRoots
            .Select(modulePackageLoader.LoadDirectory)
            .ToArray();
        var duplicatePackageIds = modulePackages
            .GroupBy(package => package.PackageId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicatePackageIds.Length != 0) {
            throw new InvalidDataException(
                $"Gameplay module package IDs must be unique: {string.Join(", ", duplicatePackageIds)}.");
        }
        var moduleCandidates = builtInModules.ToList();
        foreach (var package in modulePackages)
            moduleCandidates.AddRange(package.Modules);
        var randomSeed = options.RandomSeed
            ?? BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
        var playerStart = new AuthoritativePlayerStartConfiguration(
            options.StartingGold,
            options.StartingLife,
            options.StartingMana) {
            RandomSeed = randomSeed,
            GameMode = options.GameMode,
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
        var gameAdapter = activeRuleset.Target;
        var commandServer = new AuthoritativeCommandServer(gameAdapter);
        var saveStore = new AuthoritativeSaveStore(options.SaveRoot);
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
            saveStore);
        server.Start();
        Console.WriteLine($"Godevilution authoritative server listening on {options.BindAddress}:{server.Port}");
        Console.WriteLine($"Content packs: {string.Join(", ", contentManifest.Packs.Select(pack => $"{pack.Id}@{pack.Version}"))}");
        if (modulePackages.Length != 0) {
            Console.WriteLine($"Module packages: {string.Join(", ", modulePackages.Select(package => $"{package.PackageId}@{package.PackageVersion}"))}");
        }
        foreach (var diagnostic in modulePlan.Diagnostics)
            Console.WriteLine($"Module {diagnostic.ModuleId}: {diagnostic.Message}");
        Console.WriteLine($"Content manifest: {contentManifest.Sha256}");
        Console.WriteLine($"Ruleset identity: {ruleset.CombinedSha256}");
        Console.WriteLine($"Simulation RNG seed: {randomSeed}");
        Console.WriteLine($"Game mode: {options.GameMode}");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        try {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        }
        return 0;
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
