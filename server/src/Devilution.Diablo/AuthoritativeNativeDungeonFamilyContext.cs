namespace Devilution.Diablo;

/** Explicit family scope for shared native population phases. */
internal enum AuthoritativeNativeDungeonFamily
{
	Cathedral,
	Catacombs,
}

/**
 * Depth is validated together with an explicit family choice. Shared phases
 * never infer a dungeon family from a level number.
 */
internal readonly record struct AuthoritativeNativeDungeonFamilyContext
{
	private AuthoritativeNativeDungeonFamilyContext(AuthoritativeNativeDungeonFamily family, int depth)
	{
		Family = family;
		Depth = depth;
	}

	internal AuthoritativeNativeDungeonFamily Family { get; }
	internal int Depth { get; }

	internal static AuthoritativeNativeDungeonFamilyContext Cathedral(int depth)
	{
		if (depth is < 1 or > 4)
			throw new InvalidDataException("Cathedral population requires depth 1 through 4.");
		return new(AuthoritativeNativeDungeonFamily.Cathedral, depth);
	}

	internal static AuthoritativeNativeDungeonFamilyContext Catacombs(int depth)
	{
		if (depth is < 5 or > 8)
			throw new InvalidDataException("Catacombs population requires depth 5 through 8.");
		return new(AuthoritativeNativeDungeonFamily.Catacombs, depth);
	}

	internal bool IsCatacombs => Family == AuthoritativeNativeDungeonFamily.Catacombs;

	internal void RequireMatch(int depth)
	{
		var validRange = Family switch {
			AuthoritativeNativeDungeonFamily.Cathedral => Depth is >= 1 and <= 4,
			AuthoritativeNativeDungeonFamily.Catacombs => Depth is >= 5 and <= 8,
			_ => false,
		};
		if (!validRange || Depth != depth)
			throw new InvalidDataException("Native dungeon family context does not match its phase depth.");
	}
}

/** One generated L2 theme assigned to its original ordered coarse room. */
public sealed record AuthoritativeCatacombsSelectedTheme(
	int ThemeType,
	AuthoritativeCatacombsThemeRoom Room,
	int PlacementX,
	int PlacementY,
	int PlacementVariant);
