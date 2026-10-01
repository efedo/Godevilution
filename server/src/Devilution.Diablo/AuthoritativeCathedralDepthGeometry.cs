namespace Devilution.Diablo;

/** Immutable fine geometry paired with a depth-aware Cathedral result. */
public sealed class AuthoritativeCathedralDepthGeometry
{
	internal AuthoritativeCathedralDepthGeometry(
		AuthoritativeCathedralDepthResult generationResult,
		AuthoritativeLevel level)
	{
		GenerationResult = generationResult ?? throw new ArgumentNullException(nameof(generationResult));
		Level = level ?? throw new ArgumentNullException(nameof(level));
	}

	/** Deep-copied coarse output, including depth, policy, seed, and stair metadata. */
	public AuthoritativeCathedralDepthResult GenerationResult { get; }

	/** Native 112x112 fine geometry produced by the shared Cathedral materializer. */
	public AuthoritativeLevel Level { get; }
}

/**
 * Expands a depth-aware Cathedral result through the existing native Cathedral
 * TIL/SOL materialization path. The level-1 materializer remains the shared
 * implementation so its established correction and copy semantics are reused.
 */
public static class AuthoritativeCathedralDepthGeometryMaterializer
{
	public const int NativeWidth = AuthoritativeCathedralLevel1GeometryMaterializer.NativeWidth;
	public const int NativeHeight = AuthoritativeCathedralLevel1GeometryMaterializer.NativeHeight;

	public static AuthoritativeCathedralDepthGeometry Materialize(
		uint levelId,
		AuthoritativeCathedralDepthResult result,
		AuthoritativeDunPieceCatalog pieces,
		AuthoritativeDunTilePropertyCatalog tileProperties,
		string stableSymbol,
		bool isDefaultStart = false,
		bool suppressMultiplayerDeathDrops = false)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(pieces);
		ArgumentNullException.ThrowIfNull(tileProperties);
		ArgumentNullException.ThrowIfNull(stableSymbol);
		_ = AuthoritativeCathedralDepthGenerator.MinimumAreaForDepth(result.Depth);
		if (result.Policy != AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs)
			throw new ArgumentOutOfRangeException(nameof(result), "Unsupported Cathedral depth generation policy.");
		ArgumentNullException.ThrowIfNull(result.MegaTiles);
		ArgumentNullException.ThrowIfNull(result.NativeTransparency);

		// The legacy materializer copies both input layers before publishing the
		// level. Constructing this compatibility result also gives the new API an
		// owned result even when callers pass mutable list implementations.
		var legacyResult = new AuthoritativeCathedralLevel1Result(
			result.InputSeed,
			result.AttemptSeed,
			result.FinalRandomState,
			Array.AsReadOnly(result.MegaTiles.ToArray()),
			Array.AsReadOnly(result.NativeTransparency.ToArray()),
			result.StairsUp,
			result.StairsDown,
			result.EntryPosition);
		var materialized = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
			levelId,
			legacyResult,
			pieces,
			tileProperties,
			stableSymbol,
			isDefaultStart,
			suppressMultiplayerDeathDrops);
		var ownedResult = new AuthoritativeCathedralDepthResult(
			result.Depth,
			result.Policy,
			legacyResult.InputSeed,
			legacyResult.AttemptSeed,
			legacyResult.FinalRandomState,
			legacyResult.MegaTiles,
			legacyResult.NativeTransparency,
			legacyResult.StairsUp,
			legacyResult.StairsDown,
			legacyResult.EntryPosition);
		return new AuthoritativeCathedralDepthGeometry(ownedResult, materialized.Level);
	}
}
