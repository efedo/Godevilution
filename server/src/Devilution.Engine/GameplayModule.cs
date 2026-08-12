using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Engine;

/** Stable identity of one engine-loaded gameplay module. */
public sealed record GameplayModuleIdentity
{
    public GameplayModuleIdentity(string id, string version, string assemblySha256)
        : this(id, version, assemblySha256, [], null, [])
    {
    }

    public GameplayModuleIdentity(
        string id,
        string version,
        string assemblySha256,
        IReadOnlyList<string>? dependencies,
        GameplayModuleCompatibility? compatibility = null)
        : this(id, version, assemblySha256, dependencies, compatibility, [])
    {
    }

    public GameplayModuleIdentity(
        string id,
        string version,
        string assemblySha256,
        IReadOnlyList<string>? dependencies,
        GameplayModuleCompatibility? compatibility,
        IReadOnlyList<GameplayModuleDependencyConstraint>? dependencyConstraints)
    {
        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(version)
            || string.IsNullOrWhiteSpace(assemblySha256)) {
            throw new ArgumentException(
                "Gameplay module IDs, versions, and assembly hashes are required.");
        }

        var dependencyIds = dependencies?.ToArray() ?? [];
        if (dependencyIds.Any(string.IsNullOrWhiteSpace)) {
            throw new ArgumentException(
                "Gameplay module dependencies must be named.",
                nameof(dependencies));
        }
        if (dependencyIds.Distinct(StringComparer.Ordinal).Count() != dependencyIds.Length)
            throw new ArgumentException("Gameplay module dependencies must be unique.", nameof(dependencies));
        if (dependencyIds.Contains(id, StringComparer.Ordinal))
            throw new ArgumentException("A gameplay module cannot depend on itself.", nameof(dependencies));

        var constraints = dependencyConstraints?.ToArray() ?? [];
        if (constraints.Any(constraint => constraint is null)) {
            throw new ArgumentException(
                "Gameplay module dependency constraints cannot contain null.",
                nameof(dependencyConstraints));
        }
        if (constraints.Select(constraint => constraint.DependencyId)
                .Distinct(StringComparer.Ordinal).Count() != constraints.Length) {
            throw new ArgumentException(
                "Gameplay module dependency constraints must be unique.",
                nameof(dependencyConstraints));
        }
        var undeclaredConstraints = constraints
            .Where(constraint => !dependencyIds.Contains(
                constraint.DependencyId,
                StringComparer.Ordinal))
            .Select(constraint => constraint.DependencyId)
            .ToArray();
        if (undeclaredConstraints.Length != 0) {
            throw new ArgumentException(
                "Gameplay module dependency constraints must refer to declared dependencies: "
                + string.Join(", ", undeclaredConstraints),
                nameof(dependencyConstraints));
        }

        Id = id;
        Version = version;
        AssemblySha256 = assemblySha256;
        Dependencies = dependencyIds;
        Compatibility = compatibility;
        DependencyConstraints = constraints;
    }

    public string Id { get; }

    public string Version { get; }

    public string AssemblySha256 { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public GameplayModuleCompatibility? Compatibility { get; }

    public IReadOnlyList<GameplayModuleDependencyConstraint> DependencyConstraints { get; }

    /** Creates an identity whose named dependencies all carry version requirements. */
    public static GameplayModuleIdentity WithDependencyConstraints(
        string id,
        string version,
        string assemblySha256,
        IReadOnlyList<GameplayModuleDependencyConstraint>? dependencyConstraints,
        GameplayModuleCompatibility? compatibility = null)
    {
        var constraints = dependencyConstraints?.ToArray() ?? [];
        return new GameplayModuleIdentity(
            id,
            version,
            assemblySha256,
            constraints.Select(constraint => constraint?.DependencyId ?? string.Empty).ToArray(),
            compatibility,
            constraints);
    }

    public static string BuiltInAssemblyHash(string moduleId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"builtin:{moduleId}:{version}")))
            .ToLowerInvariant();
    }
}

/** Engine services and validated content visible while a module registers capabilities. */
public sealed record GameplayModuleContext
{
    public GameplayModuleContext(ContentManifest content, IAuthoritativeClock clock)
        : this(content, clock, GameplayEngineApiVersion.Current)
    {
    }

    public GameplayModuleContext(
        ContentManifest content,
        IAuthoritativeClock clock,
        GameplayEngineApiVersion engineApiVersion)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        EngineApiVersion = engineApiVersion;
    }

    public ContentManifest Content { get; }

    public IAuthoritativeClock Clock { get; }

    public GameplayEngineApiVersion EngineApiVersion { get; }
}

public interface IGameplayModule
{
    GameplayModuleIdentity Identity { get; }

    void Register(GameplayModuleRegistry registry, GameplayModuleContext context);
}

/**
 * Loads gameplay modules in dependency order and exposes their typed engine
 * capabilities without transport or game-specific dependencies.
 */
public sealed class GameplayModuleRegistry
{
    private readonly Dictionary<Type, CapabilityRegistration> capabilities = [];
    private readonly List<IGameplayModule> modules = [];
    private string? activeModuleId;
    private Dictionary<Type, object>? pendingCapabilities;

    public IReadOnlyList<IGameplayModule> Modules => modules;

    public IReadOnlyDictionary<Type, string> CapabilityProviders
        => capabilities.ToDictionary(entry => entry.Key, entry => entry.Value.ModuleId);

    public GameplayModuleLoadPlan? LastLoadPlan { get; private set; }

    public GameplayModuleLoadPlan Inspect(
        IEnumerable<IGameplayModule> candidates,
        GameplayModuleContext context)
    {
        return GameplayModulePlanner.Create(candidates, context);
    }

    public GameplayModuleLoadPlan Load(
        IEnumerable<IGameplayModule> candidates,
        GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        if (modules.Count != 0 || activeModuleId is not null)
            throw new InvalidOperationException("Gameplay modules have already been loaded.");

        var plan = Inspect(candidates, context);
        LastLoadPlan = plan;
        if (!plan.IsCompatible)
            throw new GameplayModuleCompatibilityException(plan);
        foreach (var next in plan.OrderedModules) {
            activeModuleId = next.Identity.Id;
            pendingCapabilities = [];
            try {
                next.Register(this, context);
                foreach (var capability in pendingCapabilities) {
                    capabilities.Add(
                        capability.Key,
                        new CapabilityRegistration(activeModuleId, capability.Value));
                }
                modules.Add(next);
            } finally {
                pendingCapabilities = null;
                activeModuleId = null;
            }
        }
        return plan;
    }

    /**
     * Registers a complete candidate set in isolation and publishes it only
     * after every module succeeds. Existing <see cref="Load"/> semantics remain
     * available to compatibility callers that inspect partial registration.
     */
    public GameplayModuleLoadPlan Activate(
        IEnumerable<IGameplayModule> candidates,
        GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        if (modules.Count != 0 || activeModuleId is not null)
            throw new InvalidOperationException("Gameplay modules have already been loaded.");

        var staging = new GameplayModuleRegistry();
        GameplayModuleLoadPlan plan;
        try {
            plan = staging.Load(candidates, context);
        } catch {
            LastLoadPlan = staging.LastLoadPlan;
            throw;
        }

        foreach (var capability in staging.capabilities)
            capabilities.Add(capability.Key, capability.Value);
        modules.AddRange(staging.modules);
        LastLoadPlan = plan;
        return plan;
    }

    public void RegisterCapability<TCapability>(TCapability capability)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (activeModuleId is null || pendingCapabilities is null) {
            throw new InvalidOperationException(
                "Capabilities may only be registered while a gameplay module is loading.");
        }

        var capabilityType = typeof(TCapability);
        if (capabilities.TryGetValue(capabilityType, out var existing)) {
            throw new InvalidOperationException(
                $"Capability '{capabilityType.FullName}' is already provided by module '{existing.ModuleId}'.");
        }
        if (!pendingCapabilities.TryAdd(capabilityType, capability)) {
            throw new InvalidOperationException(
                $"Module '{activeModuleId}' registered capability '{capabilityType.FullName}' more than once.");
        }
    }

    public bool TryGetCapability<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (capabilities.TryGetValue(typeof(TCapability), out var registration)) {
            capability = (TCapability)registration.Capability;
            return true;
        }

        capability = null;
        return false;
    }

    public TCapability GetRequiredCapability<TCapability>()
        where TCapability : class
    {
        return TryGetCapability<TCapability>(out var capability)
            ? capability!
            : throw new InvalidOperationException(
                $"No gameplay module provides capability '{typeof(TCapability).FullName}'.");
    }

    private sealed record CapabilityRegistration(string ModuleId, object Capability);
}
