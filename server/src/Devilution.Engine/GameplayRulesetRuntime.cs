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

    public GameplayRulesetReloadResult<TActivation> TryReload(
        ContentManifest candidateManifest,
        IEnumerable<IGameplayModule> candidateModules)
    {
        ArgumentNullException.ThrowIfNull(candidateManifest);
        ArgumentNullException.ThrowIfNull(candidateModules);
        lock (reloadSynchronization) {
            try {
                var candidate = Create(candidateManifest, candidateModules);
                lock (publicationSynchronization)
                    active = candidate;
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
