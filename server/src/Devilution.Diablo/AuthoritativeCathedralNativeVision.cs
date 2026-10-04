namespace Devilution.Diablo;

/** Shared source-exact native vision rays for encounter and level-load processing. */
internal static class AuthoritativeCathedralNativeVision
{
	internal static void Cast(
		AuthoritativeCathedralPopulationPoint origin, int radius,
		Action<AuthoritativeCathedralPopulationPoint> markVisible,
		Action<AuthoritativeCathedralPopulationPoint> markTransparent,
		Func<AuthoritativeCathedralPopulationPoint, bool> passesLight)
	{
		ArgumentNullException.ThrowIfNull(markVisible);
		ArgumentNullException.ThrowIfNull(markTransparent);
		ArgumentNullException.ThrowIfNull(passesLight);
		if (!Contains(origin) || radius is < 0 or > 15)
			throw new InvalidDataException("Native Cathedral vision origin/radius is outside its ray table.");
		markVisible(origin);
		foreach (var quadrant in Quadrants)
			for (var ray = 0; ray < VisionRays.Length; ray++) {
				var rayLength = radius - RayLengthAdjustments[ray];
				for (var pointIndex = 0; pointIndex < rayLength; pointIndex++) {
					var relative = VisionRays[ray][pointIndex];
					var point = new AuthoritativeCathedralPopulationPoint(
						origin.X + relative.X * quadrant.X,
						origin.Y + relative.Y * quadrant.Y);
					if (!Contains(point))
						break;
					if (relative.X > 0 && relative.Y > 0) {
						var adjacent1 = new AuthoritativeCathedralPopulationPoint(point.X - quadrant.X, point.Y);
						var adjacent2 = new AuthoritativeCathedralPopulationPoint(point.X, point.Y - quadrant.Y);
						if (!passesLight(adjacent1) && !passesLight(adjacent2))
							break;
					}
					markVisible(point);
					if (!passesLight(point))
						break;
					markTransparent(point);
				}
		}
	}

	private static bool Contains(AuthoritativeCathedralPopulationPoint point)
		=> point.X is >= 0 and < 112 && point.Y is >= 0 and < 112;

	private static readonly (int X, int Y)[] Quadrants = [(1, 1), (-1, 1), (1, -1), (-1, -1)];
	private static readonly int[] RayLengthAdjustments = [0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 4, 3, 2, 2, 2, 1, 1, 1, 0, 0, 0, 0];
	private static readonly (int X, int Y)[][] VisionRays = [
		[(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (10, 0), (11, 0), (12, 0), (13, 0), (14, 0), (15, 0)],
		[(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 1), (9, 1), (10, 1), (11, 1), (12, 1), (13, 1), (14, 1), (15, 1)],
		[(1, 0), (2, 0), (3, 0), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (12, 2), (13, 2), (14, 2), (15, 2)],
		[(1, 0), (2, 0), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 2), (9, 2), (10, 2), (11, 2), (12, 2), (13, 3), (14, 3), (15, 3)],
		[(1, 0), (2, 1), (3, 1), (4, 1), (5, 1), (6, 2), (7, 2), (8, 2), (9, 3), (10, 3), (11, 3), (12, 3), (13, 4), (14, 4)],
		[(1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2), (7, 3), (8, 3), (9, 3), (10, 4), (11, 4), (12, 4), (13, 5), (14, 5)],
		[(1, 0), (2, 1), (3, 1), (4, 2), (5, 2), (6, 3), (7, 3), (8, 3), (9, 4), (10, 4), (11, 5), (12, 5), (13, 6), (14, 6)],
		[(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 3), (7, 4), (8, 4), (9, 5), (10, 5), (11, 6), (12, 6), (13, 7)],
		[(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 4), (7, 4), (8, 5), (9, 6), (10, 6), (11, 7), (12, 7), (12, 8), (13, 8)],
		[(1, 1), (2, 2), (3, 2), (4, 3), (5, 4), (6, 5), (7, 5), (8, 6), (9, 7), (10, 7), (10, 8), (11, 8), (12, 9)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 5), (7, 6), (8, 7), (9, 8), (10, 9), (11, 9), (11, 10)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 7), (8, 8), (9, 9), (10, 10), (11, 11)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (5, 6), (6, 7), (7, 8), (8, 9), (9, 10), (9, 11), (10, 11)],
		[(1, 1), (2, 2), (2, 3), (3, 4), (4, 5), (5, 6), (5, 7), (6, 8), (7, 9), (7, 10), (8, 10), (8, 11), (9, 12)],
		[(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 6), (4, 7), (5, 8), (6, 9), (6, 10), (7, 11), (7, 12), (8, 12), (8, 13)],
		[(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (3, 6), (4, 7), (4, 8), (5, 9), (5, 10), (6, 11), (6, 12), (7, 13)],
		[(0, 1), (1, 2), (1, 3), (2, 4), (2, 5), (3, 6), (3, 7), (3, 8), (4, 9), (4, 10), (5, 11), (5, 12), (6, 13), (6, 14)],
		[(0, 1), (1, 2), (1, 3), (1, 4), (2, 5), (2, 6), (3, 7), (3, 8), (3, 9), (4, 10), (4, 11), (4, 12), (5, 13), (5, 14)],
		[(0, 1), (1, 2), (1, 3), (1, 4), (1, 5), (2, 6), (2, 7), (2, 8), (3, 9), (3, 10), (3, 11), (3, 12), (4, 13), (4, 14)],
		[(0, 1), (0, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (2, 8), (2, 9), (2, 10), (2, 11), (2, 12), (3, 13), (3, 14), (3, 15)],
		[(0, 1), (0, 2), (0, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (1, 9), (1, 10), (1, 11), (2, 12), (2, 13), (2, 14), (2, 15)],
		[(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (1, 8), (1, 9), (1, 10), (1, 11), (1, 12), (1, 13), (1, 14), (1, 15)],
		[(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9), (0, 10), (0, 11), (0, 12), (0, 13), (0, 14), (0, 15)],
	];
}
