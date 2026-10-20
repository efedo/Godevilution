namespace Devilution.Diablo;

/** Native SetAutomapView projection shared by families with independently validated catalogs. */
internal sealed class AuthoritativeNativeAutomapExploration
{
	private const int CoarseWidth = 40;
	private const int CoarseHeight = 40;
	private const int CoarseCellCount = CoarseWidth * CoarseHeight;
	private const int ExplorationCellCount = CoarseCellCount;
	private readonly AuthoritativeCathedralAutomapTileDefinition[] definitions;
	private readonly int maximumTileId;
	private readonly string family;

	internal AuthoritativeNativeAutomapExploration(
		IEnumerable<AuthoritativeCathedralAutomapTileDefinition> definitions,
		int maximumTileId, string family)
	{
		this.definitions = definitions.ToArray();
		this.maximumTileId = maximumTileId;
		this.family = family;
	}

	internal void Project(IReadOnlyList<ushort> coarseTiles,
		int positionX, int positionY, byte explorer, Span<byte> exploration)
	{
		ValidateProjectionInputs(coarseTiles, explorer, exploration);
		var mapX = (positionX - 16) / 2;
		var mapY = (positionY - 16) / 2;
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return;
		ProjectAtMap(coarseTiles, mapX, mapY, explorer, exploration);
	}

	internal void ProjectAtMap(IReadOnlyList<ushort> coarseTiles,
		int mapX, int mapY, byte explorer, Span<byte> exploration)
	{
		ValidateProjectionInputs(coarseTiles, explorer, exploration);
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return;

		var tile = EffectiveTile(coarseTiles, mapX, mapY);
		Update(exploration, mapX, mapY, explorer);
		var solid = HasFlag(tile, AuthoritativeCathedralAutomapTileFlags.Dirt);
		switch (tile.Type) {
		case AuthoritativeCathedralAutomapTileType.Vertical:
			if (solid) {
				var southWest = EffectiveTile(coarseTiles, mapX, mapY + 1);
				if (southWest.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southWest, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX, mapY + 1, explorer);
			} else if (HasFlagAt(coarseTiles, mapX - 1, mapY, AuthoritativeCathedralAutomapTileFlags.Dirt)) {
				Update(exploration, mapX - 1, mapY, explorer);
			}
			break;
		case AuthoritativeCathedralAutomapTileType.Horizontal:
			if (solid) {
				var southEast = EffectiveTile(coarseTiles, mapX + 1, mapY);
				if (southEast.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southEast, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX + 1, mapY, explorer);
			} else if (HasFlagAt(coarseTiles, mapX, mapY - 1, AuthoritativeCathedralAutomapTileFlags.Dirt)) {
				Update(exploration, mapX, mapY - 1, explorer);
			}
			break;
		case AuthoritativeCathedralAutomapTileType.Cross:
			if (solid) {
				var southWest = EffectiveTile(coarseTiles, mapX, mapY + 1);
				if (southWest.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southWest, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX, mapY + 1, explorer);
				var southEast = EffectiveTile(coarseTiles, mapX + 1, mapY);
				if (southEast.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southEast, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX + 1, mapY, explorer);
			} else {
				UpdateIfDirt(coarseTiles, exploration, mapX - 1, mapY, explorer);
				UpdateIfDirt(coarseTiles, exploration, mapX, mapY - 1, explorer);
				UpdateIfDirt(coarseTiles, exploration, mapX - 1, mapY - 1, explorer);
			}
			break;
		case AuthoritativeCathedralAutomapTileType.FenceVertical:
			if (solid) {
				UpdateIfDirt(coarseTiles, exploration, mapX, mapY - 1, explorer);
				var southWest = EffectiveTile(coarseTiles, mapX, mapY + 1);
				if (southWest.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southWest, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX, mapY + 1, explorer);
			} else {
				UpdateIfDirt(coarseTiles, exploration, mapX - 1, mapY, explorer);
			}
			break;
		case AuthoritativeCathedralAutomapTileType.FenceHorizontal:
			if (solid) {
				UpdateIfDirt(coarseTiles, exploration, mapX - 1, mapY, explorer);
				var southEast = EffectiveTile(coarseTiles, mapX + 1, mapY);
				if (southEast.Type == AuthoritativeCathedralAutomapTileType.Corner
					&& HasFlag(southEast, AuthoritativeCathedralAutomapTileFlags.Dirt))
					Update(exploration, mapX + 1, mapY, explorer);
			} else {
				UpdateIfDirt(coarseTiles, exploration, mapX, mapY - 1, explorer);
			}
			break;
		}
	}

	private void ValidateProjectionInputs(IReadOnlyList<ushort> coarseTiles,
		byte explorer, Span<byte> exploration)
	{
		ArgumentNullException.ThrowIfNull(coarseTiles);
		if (coarseTiles.Count != CoarseCellCount)
			throw new InvalidDataException($"{family} automap projection requires exactly 1600 coarse tile IDs.");
		if (exploration.Length != ExplorationCellCount)
			throw new ArgumentException($"{family} automap exploration must contain exactly 1600 bytes.", nameof(exploration));
		if (explorer > 4)
			throw new InvalidDataException($"{family} automap exploration type must be a native value from 0 through 4.");
		if (coarseTiles.Any(tile => tile > maximumTileId))
			throw new InvalidDataException(maximumTileId == byte.MaxValue
				? "Cathedral coarse automap tile IDs must fit the native uint8 table."
				: $"{family} coarse automap tile IDs must be in the supported range 0..{maximumTileId}.");
	}

	private AuthoritativeCathedralAutomapTileDefinition EffectiveTile(
		IReadOnlyList<ushort> coarseTiles, int mapX, int mapY)
	{
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return default;
		var tile = definitions[coarseTiles[mapX + mapY * CoarseWidth]];
		if (tile.Type == AuthoritativeCathedralAutomapTileType.Corner
			&& HasFlagAt(coarseTiles, mapX - 1, mapY,
				AuthoritativeCathedralAutomapTileFlags.HorizontalArch)
			&& HasFlagAt(coarseTiles, mapX, mapY - 1,
				AuthoritativeCathedralAutomapTileFlags.VerticalArch))
			return tile with { Type = AuthoritativeCathedralAutomapTileType.Diamond };
		return tile;
	}

	private bool HasFlagAt(IReadOnlyList<ushort> coarseTiles, int mapX, int mapY,
		AuthoritativeCathedralAutomapTileFlags flag)
		=> (uint)mapX < CoarseWidth && (uint)mapY < CoarseHeight
			&& HasFlag(definitions[coarseTiles[mapX + mapY * CoarseWidth]], flag);

	private static bool HasFlag(AuthoritativeCathedralAutomapTileDefinition tile,
		AuthoritativeCathedralAutomapTileFlags flag)
		=> (tile.Flags & flag) != 0;

	private void UpdateIfDirt(IReadOnlyList<ushort> coarseTiles, Span<byte> exploration,
		int mapX, int mapY, byte explorer)
	{
		if (HasFlagAt(coarseTiles, mapX, mapY, AuthoritativeCathedralAutomapTileFlags.Dirt))
			Update(exploration, mapX, mapY, explorer);
	}

	private static void Update(Span<byte> exploration, int mapX, int mapY, byte explorer)
	{
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return;
		var index = mapX + mapY * CoarseWidth;
		if (exploration[index] < explorer)
			exploration[index] = explorer;
	}

}
