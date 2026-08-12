using System.Globalization;

namespace Devilution.Engine;

/** Semantic version of the deterministic Engine-to-module API contract. */
public readonly record struct GameplayEngineApiVersion(
    uint Major,
    uint Minor,
    uint Patch) : IComparable<GameplayEngineApiVersion>
{
    public static GameplayEngineApiVersion Current { get; } = new(0, 1, 0);

    public static GameplayEngineApiVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var components = value.Split('.', StringSplitOptions.None);
        if (components.Length != 3
            || !uint.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !uint.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !uint.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) {
            throw new ArgumentException(
                $"Engine API version '{value}' must contain three unsigned integers.",
                nameof(value));
        }
        return new GameplayEngineApiVersion(major, minor, patch);
    }

    public int CompareTo(GameplayEngineApiVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/** Three-component semantic version used by gameplay-module contracts. */
public readonly record struct GameplayModuleVersion(
    uint Major,
    uint Minor,
    uint Patch) : IComparable<GameplayModuleVersion>
{
    public static GameplayModuleVersion Parse(string value)
    {
        if (!TryParse(value, out var result)) {
            throw new ArgumentException(
                $"Gameplay module version '{value}' must contain three unsigned integers.",
                nameof(value));
        }
        return result;
    }

    public static bool TryParse(string? value, out GameplayModuleVersion result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var components = value.Split('.', StringSplitOptions.None);
        if (components.Length != 3
            || !uint.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !uint.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !uint.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) {
            return false;
        }
        result = new GameplayModuleVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(GameplayModuleVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/** Inclusive semantic-version requirement for one named gameplay module. */
public sealed record GameplayModuleDependencyConstraint
{
    public GameplayModuleDependencyConstraint(
        string dependencyId,
        GameplayModuleVersion minimum,
        GameplayModuleVersion maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyId);
        if (minimum.CompareTo(maximum) > 0) {
            throw new ArgumentException(
                "The minimum gameplay-module dependency version cannot exceed the maximum.");
        }
        DependencyId = dependencyId;
        Minimum = minimum;
        Maximum = maximum;
    }

    public GameplayModuleDependencyConstraint(
        string dependencyId,
        string minimum,
        string maximum)
        : this(
            dependencyId,
            GameplayModuleVersion.Parse(minimum),
            GameplayModuleVersion.Parse(maximum))
    {
    }

    public string DependencyId { get; }

    public GameplayModuleVersion Minimum { get; }

    public GameplayModuleVersion Maximum { get; }

    public bool Supports(GameplayModuleVersion version)
    {
        return version.CompareTo(Minimum) >= 0 && version.CompareTo(Maximum) <= 0;
    }
}

/** Inclusive Engine API range declared by a gameplay module. */
public sealed record GameplayModuleCompatibility
{
    public GameplayModuleCompatibility(
        GameplayEngineApiVersion minimum,
        GameplayEngineApiVersion maximum)
    {
        if (minimum.CompareTo(maximum) > 0) {
            throw new ArgumentException(
                "The minimum compatible Engine API cannot exceed the maximum.");
        }
        Minimum = minimum;
        Maximum = maximum;
    }

    public GameplayModuleCompatibility(string minimum, string maximum)
        : this(GameplayEngineApiVersion.Parse(minimum), GameplayEngineApiVersion.Parse(maximum))
    {
    }

    public GameplayEngineApiVersion Minimum { get; }

    public GameplayEngineApiVersion Maximum { get; }

    public bool Supports(GameplayEngineApiVersion version)
    {
        return version.CompareTo(Minimum) >= 0 && version.CompareTo(Maximum) <= 0;
    }
}

public enum GameplayModuleDiagnosticSeverity
{
    Information,
    Error,
}

/** Actionable result from module discovery and compatibility preflight. */
public sealed record GameplayModuleDiagnostic(
    GameplayModuleDiagnosticSeverity Severity,
    string Code,
    string ModuleId,
    string Message);

/** Immutable module order and diagnostics computed before registration begins. */
public sealed class GameplayModuleLoadPlan
{
    internal GameplayModuleLoadPlan(
        IReadOnlyList<IGameplayModule> orderedModules,
        IReadOnlyList<GameplayModuleDiagnostic> diagnostics)
    {
        OrderedModules = orderedModules;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<IGameplayModule> OrderedModules { get; }

    public IReadOnlyList<GameplayModuleDiagnostic> Diagnostics { get; }

    public bool IsCompatible => Diagnostics.All(
        diagnostic => diagnostic.Severity != GameplayModuleDiagnosticSeverity.Error);
}

public sealed class GameplayModuleCompatibilityException : InvalidOperationException
{
    public GameplayModuleCompatibilityException(GameplayModuleLoadPlan plan)
        : base(CreateMessage(plan))
    {
        Plan = plan;
    }

    public GameplayModuleLoadPlan Plan { get; }

    private static string CreateMessage(GameplayModuleLoadPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = plan.Diagnostics
            .Where(diagnostic => diagnostic.Severity == GameplayModuleDiagnosticSeverity.Error)
            .Select(diagnostic => $"[{diagnostic.Code}] {diagnostic.Message}")
            .ToArray();
        return errors.Length == 0
            ? "Gameplay module compatibility failed without a diagnostic."
            : string.Join(" ", errors);
    }
}

internal static class GameplayModulePlanner
{
    public static GameplayModuleLoadPlan Create(
        IEnumerable<IGameplayModule> candidates,
        GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<GameplayModuleDiagnostic>();
        var discovered = candidates.ToArray();
        for (var index = 0; index < discovered.Length; index++) {
            var module = discovered[index];
            if (module is null) {
                diagnostics.Add(Error(
                    "module-instance-null",
                    $"<candidate-{index}>",
                    $"Gameplay module candidate {index} is null."));
            } else if (module.Identity is null) {
                diagnostics.Add(Error(
                    "module-identity-missing",
                    $"<candidate-{index}>",
                    $"Gameplay module candidate {index} does not provide an identity."));
            }
        }
        var valid = discovered
            .Where(module => module is not null && module.Identity is not null)
            .ToArray();
        foreach (var duplicate in valid.GroupBy(
                     module => module.Identity.Id,
                     StringComparer.Ordinal).Where(group => group.Count() > 1)) {
            diagnostics.Add(Error(
                "module-id-duplicate",
                duplicate.Key,
                $"Gameplay module ID '{duplicate.Key}' is provided {duplicate.Count()} times."));
        }

        var knownModules = valid
            .GroupBy(module => module.Identity.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var knownIds = knownModules.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var module in valid) {
            var compatibility = module.Identity.Compatibility;
            if (compatibility is not null
                && !compatibility.Supports(context.EngineApiVersion)) {
                diagnostics.Add(Error(
                    "engine-api-incompatible",
                    module.Identity.Id,
                    $"Gameplay module '{module.Identity.Id}' supports Engine API "
                    + $"{compatibility.Minimum} through {compatibility.Maximum}, "
                    + $"but the host provides {context.EngineApiVersion}."));
            }
            foreach (var dependency in module.Identity.Dependencies
                         .Where(dependency => !knownIds.Contains(dependency))) {
                diagnostics.Add(Error(
                    "module-dependency-missing",
                    module.Identity.Id,
                    $"Gameplay module '{module.Identity.Id}' requires missing module '{dependency}'."));
            }
            foreach (var constraint in module.Identity.DependencyConstraints) {
                if (!knownModules.TryGetValue(constraint.DependencyId, out var dependency))
                    continue;
                if (!GameplayModuleVersion.TryParse(
                        dependency.Identity.Version,
                        out var dependencyVersion)) {
                    diagnostics.Add(Error(
                        "module-dependency-version-invalid",
                        module.Identity.Id,
                        $"Gameplay module '{module.Identity.Id}' constrains "
                        + $"'{constraint.DependencyId}', but loaded version "
                        + $"'{dependency.Identity.Version}' is not a three-component semantic version."));
                } else if (!constraint.Supports(dependencyVersion)) {
                    diagnostics.Add(Error(
                        "module-dependency-version-incompatible",
                        module.Identity.Id,
                        $"Gameplay module '{module.Identity.Id}' requires "
                        + $"'{constraint.DependencyId}' version {constraint.Minimum} through "
                        + $"{constraint.Maximum}, but {dependencyVersion} is loaded."));
                }
            }
        }

        var ordered = new List<IGameplayModule>();
        if (!diagnostics.Any(diagnostic =>
                diagnostic.Code is "module-instance-null"
                    or "module-identity-missing"
                    or "module-id-duplicate"
                    or "module-dependency-missing")) {
            while (ordered.Count != valid.Length) {
                var next = valid.FirstOrDefault(module =>
                    !ordered.Contains(module)
                    && module.Identity.Dependencies.All(dependency =>
                        ordered.Any(loaded => string.Equals(
                            loaded.Identity.Id,
                            dependency,
                            StringComparison.Ordinal))));
                if (next is null) {
                    var unresolved = valid
                        .Where(module => !ordered.Contains(module))
                        .Select(module => module.Identity.Id)
                        .ToArray();
                    diagnostics.Add(Error(
                        "module-dependency-cycle",
                        string.Join(",", unresolved),
                        $"Gameplay module dependencies contain a cycle among: {string.Join(", ", unresolved)}."));
                    break;
                }
                ordered.Add(next);
            }
        }

        if (!diagnostics.Any(diagnostic =>
                diagnostic.Severity == GameplayModuleDiagnosticSeverity.Error)) {
            foreach (var module in ordered) {
                var compatibility = module.Identity.Compatibility;
                diagnostics.Add(new GameplayModuleDiagnostic(
                    GameplayModuleDiagnosticSeverity.Information,
                    "module-compatible",
                    module.Identity.Id,
                    compatibility is null
                        ? $"Gameplay module '{module.Identity.Id}@{module.Identity.Version}' accepts the host Engine API without a declared range."
                        : $"Gameplay module '{module.Identity.Id}@{module.Identity.Version}' accepts Engine API {context.EngineApiVersion}."));
            }
        }
        return new GameplayModuleLoadPlan(ordered.ToArray(), diagnostics.ToArray());
    }

    private static GameplayModuleDiagnostic Error(string code, string moduleId, string message)
    {
        return new GameplayModuleDiagnostic(
            GameplayModuleDiagnosticSeverity.Error,
            code,
            moduleId,
            message);
    }
}
