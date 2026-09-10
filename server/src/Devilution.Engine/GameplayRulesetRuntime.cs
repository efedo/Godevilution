using Devilution.Data;

namespace Devilution.Engine;

/** One atomically published content, module, identity, and target activation. */
public sealed record GameplayRulesetActivation<TActivation>
    where TActivation : class
{
    internal GameplayRulesetActivation(
        ComposedContent content,
        GameplayModuleRegistry modules,
        GameplayModuleLoadPlan modulePlan,
        GameplayRulesetIdentity identity,
        TActivation target)
    {
        Content = content;
        Modules = modules;
        ModulePlan = modulePlan;
        Identity = identity;
        Target = target;
    }

    public ComposedContent Content { get; }

    public GameplayModuleRegistry Modules { get; }

    public GameplayModuleLoadPlan ModulePlan { get; }

    public GameplayRulesetIdentity Identity { get; }

    /** Game-specific activation validated before this ruleset was published. */
    public TActivation Target { get; }
}

/** Actionable failed candidate-activation result. */
public sealed record GameplayRulesetActivationDiagnostic(string Code, string Message);

/** Result of attempting to replace one active ruleset atomically. */
public sealed record GameplayRulesetReloadResult<TActivation>(
    bool Succeeded,
    GameplayRulesetActivation<TActivation> Active,
    IReadOnlyList<GameplayRulesetActivationDiagnostic> Diagnostics)
    where TActivation : class;

/**
 * Coordinates Data composition, Engine module activation, ruleset identity,
 * and game-specific validation behind one publication boundary.
 */
public sealed class GameplayRulesetRuntime<TActivation>
    where TActivation : class
{
    private readonly object publicationSynchronization = new();
    private readonly object reloadSynchronization = new();
    private readonly IAuthoritativeClock clock;
    private readonly GameplayEngineApiVersion engineApiVersion;
    private readonly Func<ComposedContent, GameplayModuleRegistry, TActivation> activateTarget;
    private readonly Dictionary<GameplayRulesetActivation<TActivation>, int> leases =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<GameplayRulesetActivation<TActivation>> retired = [];
    private GameplayRulesetActivation<TActivation> active;

    public GameplayRulesetRuntime(
        ContentManifest initialManifest,
        IEnumerable<IGameplayModule> initialModules,
        IAuthoritativeClock clock,
        Func<ComposedContent, GameplayModuleRegistry, TActivation> activateTarget,
        GameplayEngineApiVersion? engineApiVersion = null)
    {
        ArgumentNullException.ThrowIfNull(initialManifest);
        ArgumentNullException.ThrowIfNull(initialModules);
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.activateTarget = activateTarget
            ?? throw new ArgumentNullException(nameof(activateTarget));
        this.engineApiVersion = engineApiVersion ?? GameplayEngineApiVersion.Current;
        active = Create(initialManifest, initialModules);
    }

    public GameplayRulesetActivation<TActivation> Active
    {
        get {
            lock (publicationSynchronization)
                return active;
        }
    }

    /** Number of replaced generations awaiting lease release. */
    public int RetiredActivationCount
    {
        get {
            lock (publicationSynchronization)
                return retired.Count;
        }
    }

    /**
     * Pins the currently published generation. Callers must dispose the lease
     * after the session operation has completed before retiring old targets.
     */
    public GameplayRulesetLease<TActivation> Acquire()
    {
        lock (publicationSynchronization) {
            var generation = active;
            leases[generation] = leases.TryGetValue(generation, out var count)
                ? checked(count + 1)
                : 1;
            return new GameplayRulesetLease<TActivation>(
                generation,
                () => Release(generation));
        }
    }

    /**
     * Removes replaced generations that no operation still pins and returns
     * them to the host for disposal of game-specific resources.
     */
    public IReadOnlyList<GameplayRulesetActivation<TActivation>> DrainRetired()
    {
        lock (publicationSynchronization) {
            if (retired.Count == 0)
                return [];

            var releasable = retired
                .Where(generation => !leases.ContainsKey(generation))
                .ToArray();
            if (releasable.Length == 0)
                return [];
            var releasableSet = releasable.ToHashSet(ReferenceEqualityComparer.Instance);
            retired.RemoveAll(releasableSet.Contains);
            return releasable;
        }
    }

    public GameplayRulesetReloadResult<TActivation> TryReload(
        ContentManifest candidateManifest,
        IEnumerable<IGameplayModule> candidateModules,
        Action<TActivation>? prepareCandidate = null)
        => TryReloadWithActivation(
            candidateManifest,
            candidateModules,
            prepareCandidate is null
                ? null
                : activation => prepareCandidate(activation.Target));

    /**
     * Replaces the active ruleset after preparation receives the complete
     * candidate activation, still before the publication boundary.
     */
    public GameplayRulesetReloadResult<TActivation> TryReloadWithActivation(
        ContentManifest candidateManifest,
        IEnumerable<IGameplayModule> candidateModules,
        Action<GameplayRulesetActivation<TActivation>>? prepareCandidate = null)
    {
        ArgumentNullException.ThrowIfNull(candidateManifest);
        ArgumentNullException.ThrowIfNull(candidateModules);
        lock (reloadSynchronization) {
            try {
                var candidate = Create(candidateManifest, candidateModules);
                try {
                    prepareCandidate?.Invoke(candidate);
                } catch (Exception exception) {
                    return new GameplayRulesetReloadResult<TActivation>(
                        false,
                        Active,
                        [new GameplayRulesetActivationDiagnostic(
                            "ruleset-target-preparation-failed",
                            exception.Message)]);
                }
                lock (publicationSynchronization) {
                    retired.Add(active);
                    active = candidate;
                }
                return new GameplayRulesetReloadResult<TActivation>(true, candidate, []);
            } catch (RulesetActivationStageException exception) {
                return new GameplayRulesetReloadResult<TActivation>(
                    false,
                    Active,
                    [new GameplayRulesetActivationDiagnostic(
                        exception.Code,
                        exception.InnerException?.Message ?? exception.Message)]);
            }
        }
    }

    private GameplayRulesetActivation<TActivation> Create(
        ContentManifest manifest,
        IEnumerable<IGameplayModule> moduleCandidates)
    {
        ComposedContent content;
        try {
            content = ComposedContent.Compose(manifest);
        } catch (Exception exception)
            when (exception is ArgumentException or InvalidDataException) {
            throw StageFailure("ruleset-content-invalid", exception);
        }

        var registry = new GameplayModuleRegistry();
        GameplayModuleLoadPlan modulePlan;
        try {
            modulePlan = registry.Activate(
                moduleCandidates,
                new GameplayModuleContext(content.Manifest, clock, engineApiVersion));
        } catch (GameplayModuleCompatibilityException exception) {
            throw StageFailure("ruleset-modules-incompatible", exception);
        } catch (Exception exception) {
            throw StageFailure("ruleset-module-registration-failed", exception);
        }

        GameplayRulesetIdentity identity;
        try {
            identity = new GameplayRulesetIdentity(
                content.Manifest,
                registry.Modules.Select(module => module.Identity));
        } catch (Exception exception)
            when (exception is ArgumentException or InvalidOperationException) {
            throw StageFailure("ruleset-identity-invalid", exception);
        }
        TActivation target;
        try {
            target = activateTarget(content, registry)
                ?? throw new InvalidOperationException(
                    "The ruleset target activator returned null.");
        } catch (Exception exception) {
            throw StageFailure("ruleset-target-activation-failed", exception);
        }
        return new GameplayRulesetActivation<TActivation>(
            content,
            registry,
            modulePlan,
            identity,
            target);
    }

    private static RulesetActivationStageException StageFailure(
        string code,
        Exception exception)
    {
        return new RulesetActivationStageException(code, exception);
    }

    private void Release(GameplayRulesetActivation<TActivation> generation)
    {
        lock (publicationSynchronization) {
            if (!leases.TryGetValue(generation, out var count))
                return;
            if (count <= 1)
                leases.Remove(generation);
            else
                leases[generation] = count - 1;
        }
    }

    private sealed class RulesetActivationStageException(
        string code,
        Exception innerException)
        : InvalidOperationException(
            $"Gameplay ruleset activation failed at stage '{code}': {innerException.Message}",
            innerException)
    {
        public string Code { get; } = code;
    }
}
