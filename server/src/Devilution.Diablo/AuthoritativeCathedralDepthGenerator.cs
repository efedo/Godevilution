namespace Devilution.Diablo;

/** The supported non-campaign Cathedral generation policy. */
public enum AuthoritativeCathedralDepthGenerationPolicy
{
	QuestDisabledOriginalDiabloStairs,
}

/** Immutable coarse/fine-input result for one generated Cathedral depth. */
public sealed record AuthoritativeCathedralDepthResult(
	int Depth,
	AuthoritativeCathedralDepthGenerationPolicy Policy,
	uint InputSeed,
	uint AttemptSeed,
	uint FinalRandomState,
	IReadOnlyList<ushort> MegaTiles,
	IReadOnlyList<ushort> NativeTransparency,
	AuthoritativeCathedralPoint StairsUp,
	AuthoritativeCathedralPoint StairsDown,
	AuthoritativeCathedralPoint EntryPosition)
{
	public const int MegaWidth = 40;
	public const int MegaHeight = 40;
	public const int TransparencyWidth = 80;
	public const int TransparencyHeight = 80;

	public ushort GetMegaTile(int x, int y)
	{
		if ((uint)x >= MegaWidth || (uint)y >= MegaHeight)
			throw new ArgumentOutOfRangeException();
		return MegaTiles[y * MegaWidth + x];
	}

	public ushort GetTransparency(int x, int y)
	{
		if ((uint)x >= TransparencyWidth || (uint)y >= TransparencyHeight)
			throw new ArgumentOutOfRangeException();
		return NativeTransparency[y * TransparencyWidth + x];
	}
}

/**
 * Shared native Cathedral level 1-4 generation entry point. These are all
 * DTYPE_CATHEDRAL levels driven by level_l1.cpp; Catacombs/Caves/Hell family
 * generators are intentionally outside this API.
 */
public static class AuthoritativeCathedralDepthGenerator
{
	public const int MinimumSupportedDepth = 1;
	public const int MaximumSupportedDepth = 4;

	public static AuthoritativeCathedralDepthResult Generate(
		int depth,
		uint dungeonSeed,
		AuthoritativeCathedralEntry entry,
		AuthoritativeCathedralDepthGenerationPolicy policy = AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs)
		=> AuthoritativeCathedralLevel1Generator.GenerateDepth(depth, dungeonSeed, entry, policy);

	public static int MinimumAreaForDepth(int depth)
		=> depth switch
		{
			1 => 533,
			2 => 693,
			3 or 4 => 761,
			_ => throw new ArgumentOutOfRangeException(nameof(depth), depth, "Cathedral depth must be between 1 and 4."),
		};
}
