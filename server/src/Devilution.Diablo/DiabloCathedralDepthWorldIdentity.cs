using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Devilution.Diablo;

/** Immutable game-owned identity of the eagerly generated Cathedral depths 1–4 and routes. */
public sealed class DiabloCathedralDepthWorldIdentity
{
	public DiabloCathedralDepthWorldIdentity(
		IReadOnlyList<AuthoritativeCathedralDepthGenerationIdentity> levels,
		string routeFingerprint)
	{
		if (levels is null || levels.Count != AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
			throw new InvalidDataException(
				"This generated world identity requires exactly four Cathedral depths.");
		ValidateFingerprint(routeFingerprint);

		var symbols = new HashSet<string>(StringComparer.Ordinal);
		for (var index = 0; index < levels.Count; index++)
		{
			var level = levels[index];
			var expectedDepth = index + 1;
			if (level is null
				|| level.CatalogId != AuthoritativeCathedralDepthRuntimeCatalog.CatalogId
				|| level.GeneratorId != AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId
				|| level.GeneratorRevision != AuthoritativeCathedralDepthRuntimeCatalog.GeneratorRevision
				|| level.Depth != expectedDepth
				|| level.LevelId != (uint)expectedDepth
				|| !Enum.IsDefined(level.Entry)
				|| level.Policy != AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs
				|| string.IsNullOrWhiteSpace(level.LevelSymbol)
				|| level.LevelSymbol.Length > 256
				|| !symbols.Add(level.LevelSymbol))
				throw new InvalidDataException(
					"Generated Cathedral depth identity must contain native depths 1 through 4 in order with unique symbols.");
			ValidateFingerprint(level.CoarseFingerprint);
			ValidateFingerprint(level.FineFingerprint);
			ValidateFingerprint(level.RouteFingerprint);
			ValidateFingerprint(level.CatalogFingerprint);
		}

		// Preserve the supplied order exactly. Sorting here would allow a corrupted
		// save to masquerade as the canonical native depth sequence.
		Levels = new ReadOnlyCollection<AuthoritativeCathedralDepthGenerationIdentity>(levels.ToArray());
		RouteFingerprint = routeFingerprint;
	}

	public IReadOnlyList<AuthoritativeCathedralDepthGenerationIdentity> Levels { get; }

	public string RouteFingerprint { get; }

	internal static void RequireSame(
		DiabloCathedralDepthWorldIdentity? expected,
		DiabloCathedralDepthWorldIdentity? restored)
	{
		if (expected is null && restored is null)
			return;
		if (expected is null || restored is null
			|| !string.Equals(expected.RouteFingerprint, restored.RouteFingerprint, StringComparison.Ordinal)
			|| !expected.Levels.SequenceEqual(restored.Levels))
			throw new InvalidDataException(
				"Saved Cathedral depth identity does not match active seeds, generator, geometry catalogs or routes.");
	}

	private static void ValidateFingerprint(string fingerprint)
	{
		if (fingerprint is null
			|| !Regex.IsMatch(fingerprint, "\\A[0-9A-F]{64}\\z", RegexOptions.CultureInvariant))
			throw new InvalidDataException(
				"Generated Cathedral fingerprints must be canonical uppercase SHA-256 values.");
	}
}
