using Devilution.Data;
using Devilution.Domain.Hashing;

namespace Devilution.Engine;

/** Combined validated-content and ordered-gameplay-module identity. */
public sealed class GameplayRulesetIdentity
{
    public GameplayRulesetIdentity(
        ContentManifest content,
        IEnumerable<GameplayModuleIdentity> modules)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ArgumentNullException.ThrowIfNull(modules);
        Modules = modules.ToArray();
        if (Modules.Count == 0)
            throw new ArgumentException("A gameplay ruleset must contain a module.", nameof(modules));
        if (Modules.Any(module => module is null))
            throw new ArgumentException("Gameplay module identities cannot be null.", nameof(modules));
        if (Modules.Select(module => module.Id).Distinct(StringComparer.Ordinal).Count()
            != Modules.Count) {
            throw new ArgumentException("Gameplay module identities must be unique.", nameof(modules));
        }
        CombinedSha256 = ComputeHash();
    }

    public ContentManifest Content { get; }

    public IReadOnlyList<GameplayModuleIdentity> Modules { get; }

    public string CombinedSha256 { get; }

    private string ComputeHash()
    {
        var hasher = new CanonicalStateHasher();
        hasher.AppendString(Content.Sha256);
        hasher.AppendUInt64((ulong)Modules.Count);
        foreach (var module in Modules) {
            hasher.AppendString(module.Id);
            hasher.AppendString(module.Version);
            hasher.AppendString(module.AssemblySha256);
            hasher.AppendUInt64((ulong)module.Dependencies.Count);
            foreach (var dependency in module.Dependencies)
                hasher.AppendString(dependency);
        }
        if (Modules.Any(module => module.DependencyConstraints.Count != 0)) {
            hasher.AppendString("module-dependency-constraints-v1");
            foreach (var module in Modules) {
                hasher.AppendUInt64((ulong)module.DependencyConstraints.Count);
                foreach (var constraint in module.DependencyConstraints) {
                    hasher.AppendString(constraint.DependencyId);
                    hasher.AppendString(constraint.Minimum.ToString());
                    hasher.AppendString(constraint.Maximum.ToString());
                }
            }
        }
        return hasher.HexDigest();
    }
}
