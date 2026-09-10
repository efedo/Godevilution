using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Server.Host;

/** Server-side bridge from engine modules to protocol and persistence adapters. */
public interface IAuthoritativeGameHostAdapterFactory
{
    string Id { get; }

    IReadOnlyList<IGameplayModule> CreateBuiltInModules();

    bool CanActivate(GameplayModuleRegistry registry);

    IAuthoritativeGameHostAdapter Activate(
        GameplayModuleRegistry registry,
        ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart);
}

/** Selects one adapter deterministically after all gameplay modules preflight. */
public sealed class AuthoritativeGameHostAdapterCatalog
{
    private readonly IReadOnlyList<IAuthoritativeGameHostAdapterFactory> factories;
    private readonly string? selectedFactoryId;

    public AuthoritativeGameHostAdapterCatalog(
        IEnumerable<IAuthoritativeGameHostAdapterFactory> factories,
        string? selectedFactoryId = null)
    {
        ArgumentNullException.ThrowIfNull(factories);
        var discovered = factories.ToArray();
        if (discovered.Length == 0
            || discovered.Any(factory => factory is null)
            || discovered.Any(factory => string.IsNullOrWhiteSpace(factory.Id))) {
            throw new ArgumentException(
                "At least one named game-host adapter factory is required.",
                nameof(factories));
        }
        var normalizedSelection = selectedFactoryId?.Trim();
        if (selectedFactoryId is not null && string.IsNullOrWhiteSpace(normalizedSelection))
            throw new ArgumentException(
                "The selected game-host adapter factory ID cannot be blank.",
                nameof(selectedFactoryId));
        var duplicateIds = discovered
            .GroupBy(factory => factory.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length != 0) {
            throw new ArgumentException(
                $"Game-host adapter factory IDs must be unique: {string.Join(", ", duplicateIds)}.",
                nameof(factories));
        }
        this.factories = discovered;
        this.selectedFactoryId = normalizedSelection;
    }

    /** Optional host-selected factory ID; null retains deterministic single-match selection. */
    public string? SelectedFactoryId => selectedFactoryId;

    public IReadOnlyList<IGameplayModule> CreateBuiltInModules()
    {
        var selectedFactories = selectedFactoryId is null
            ? factories
            : factories.Where(factory => string.Equals(
                factory.Id,
                selectedFactoryId,
                StringComparison.Ordinal)).ToArray();
        if (selectedFactories.Count == 0)
            throw new InvalidOperationException(
                $"Configured game-host adapter factory '{selectedFactoryId}' is not registered.");
        var modules = new List<IGameplayModule>();
        foreach (var factory in selectedFactories) {
            var factoryModules = factory.CreateBuiltInModules()
                ?? throw new InvalidOperationException(
                    $"Game-host adapter factory '{factory.Id}' returned null modules.");
            if (factoryModules.Any(module => module is null)) {
                throw new InvalidOperationException(
                    $"Game-host adapter factory '{factory.Id}' returned a null module.");
            }
            modules.AddRange(factoryModules);
        }
        return modules.ToArray();
    }

    public IAuthoritativeGameHostAdapter Activate(
        GameplayModuleRegistry registry,
        ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playerStart);
        playerStart.Validate();
        var compatible = factories
            .Where(factory => factory.CanActivate(registry))
            .ToArray();
        if (compatible.Length == 0) {
            throw new InvalidOperationException(
                "No game-host adapter is compatible with the loaded gameplay modules.");
        }

        IAuthoritativeGameHostAdapterFactory selectedFactory;
        if (selectedFactoryId is not null) {
            var configuredFactory = factories.SingleOrDefault(
                factory => string.Equals(
                    factory.Id,
                    selectedFactoryId,
                    StringComparison.Ordinal));
            if (configuredFactory is null) {
                throw new InvalidOperationException(
                    $"Configured game-host adapter factory '{selectedFactoryId}' is not registered.");
            }
            if (!compatible.Contains(configuredFactory)) {
                throw new InvalidOperationException(
                    $"Configured game-host adapter factory '{selectedFactoryId}' is not compatible with the loaded gameplay modules.");
            }
            selectedFactory = configuredFactory;
        } else {
            if (compatible.Length > 1) {
                throw new InvalidOperationException(
                    "More than one game-host adapter is compatible with the loaded gameplay modules: "
                    + $"{string.Join(", ", compatible.Select(factory => factory.Id))}.");
            }
            selectedFactory = compatible[0];
        }

        return selectedFactory.Activate(registry, content, playerStart)
            ?? throw new InvalidOperationException(
                $"Game-host adapter factory '{selectedFactory.Id}' returned no adapter.");
    }
}
