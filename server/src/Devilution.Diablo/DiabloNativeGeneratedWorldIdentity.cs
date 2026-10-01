using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Devilution.Diablo;

/** Immutable game-owned identity of eagerly generated geometry and resolved routes. */
public sealed class DiabloNativeGeneratedWorldIdentity
{
    public DiabloNativeGeneratedWorldIdentity(
        IReadOnlyList<AuthoritativeCathedralLevel1GenerationIdentity> levels,
        string routeFingerprint)
    {
        if (levels is null || levels.Count != 1)
            throw new InvalidDataException("This generated world identity requires exactly one ordinary Cathedral level 1.");
        ValidateFingerprint(routeFingerprint);
        var ordered = levels.OrderBy(level => level?.LevelId).ToArray();
        var ids = new HashSet<uint>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var level in ordered) {
            if (level is null
                || level.CatalogId != AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId
                || level.GeneratorId != AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId
                || level.GeneratorRevision != AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorRevision
                || level.LevelId != 1
                || !Enum.IsDefined(level.Entry)
                || string.IsNullOrWhiteSpace(level.LevelSymbol)
                || level.LevelSymbol.Length > 256
                || !ids.Add(level.LevelId)
                || !symbols.Add(level.LevelSymbol)) {
                throw new InvalidDataException("Generated world identity contains invalid or duplicate level metadata.");
            }
            ValidateFingerprint(level.CoarseFingerprint);
            ValidateFingerprint(level.FineFingerprint);
            ValidateFingerprint(level.RouteFingerprint);
            ValidateFingerprint(level.CatalogFingerprint);
        }
        Levels = new ReadOnlyCollection<AuthoritativeCathedralLevel1GenerationIdentity>(ordered);
        RouteFingerprint = routeFingerprint;
    }

    public IReadOnlyList<AuthoritativeCathedralLevel1GenerationIdentity> Levels { get; }

    public string RouteFingerprint { get; }

    internal static void RequireSame(
        DiabloNativeGeneratedWorldIdentity? expected,
        DiabloNativeGeneratedWorldIdentity? restored)
    {
        if (expected is null && restored is null)
            return;
        if (expected is null || restored is null
            || !string.Equals(expected.RouteFingerprint, restored.RouteFingerprint, StringComparison.Ordinal)
            || !expected.Levels.SequenceEqual(restored.Levels)) {
            throw new InvalidDataException(
                "Saved generated world identity does not match active seed, generator, geometry catalogs or routes.");
        }
    }

    private static void ValidateFingerprint(string fingerprint)
    {
        if (fingerprint is null || !Regex.IsMatch(fingerprint, "\\A[0-9A-F]{64}\\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Generated world fingerprints must be canonical uppercase SHA-256 values.");
    }
}

public sealed partial class DiabloSimulation
{
    /** Null for unchanged authored/legacy profiles; never projected onto the live wire. */
    public DiabloNativeGeneratedWorldIdentity? NativeGeneratedWorldIdentity { get; private set; }

    internal void ConfigureNativeGeneratedWorldIdentity(DiabloNativeGeneratedWorldIdentity? identity)
    {
        if (NativeGeneratedWorldIdentity is not null)
            throw new InvalidOperationException("Generated world identity is already configured.");
        NativeGeneratedWorldIdentity = identity;
    }
}
