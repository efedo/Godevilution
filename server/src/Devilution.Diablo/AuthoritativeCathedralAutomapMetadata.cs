using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Devilution.Data;

namespace Devilution.Diablo;

/** The exact AutomapTile::Types ordinal sequence used by original Diablo. */
public enum AuthoritativeCathedralAutomapTileType : byte
{
	None = 0,
	Diamond = 1,
	Vertical = 2,
	Horizontal = 3,
	Cross = 4,
	FenceVertical = 5,
	FenceHorizontal = 6,
	Corner = 7,
	CaveHorizontalCross = 8,
	CaveVerticalCross = 9,
	CaveHorizontal = 10,
	CaveVertical = 11,
	CaveCross = 12,
	Bridge = 13,
	River = 14,
	RiverCornerEast = 15,
	RiverCornerNorth = 16,
	RiverCornerSouth = 17,
	RiverCornerWest = 18,
	RiverForkIn = 19,
	RiverForkOut = 20,
	RiverLeftIn = 21,
	RiverLeftOut = 22,
	RiverRightIn = 23,
	RiverRightOut = 24,
	CaveHorizontalWoodCross = 25,
	CaveVerticalWoodCross = 26,
	CaveLeftCorner = 27,
	CaveRightCorner = 28,
	CaveBottomCorner = 29,
	CaveHorizontalWood = 30,
	CaveVerticalWood = 31,
	CaveWoodCross = 32,
	CaveRightWoodCross = 33,
	CaveLeftWoodCross = 34,
	HorizontalLavaThin = 35,
	VerticalLavaThin = 36,
	BendSouthLavaThin = 37,
	BendWestLavaThin = 38,
	BendEastLavaThin = 39,
	BendNorthLavaThin = 40,
	VerticalWallLava = 41,
	HorizontalWallLava = 42,
	SELava = 43,
	SWLava = 44,
	NELava = 45,
	NWLava = 46,
	SLava = 47,
	WLava = 48,
	ELava = 49,
	NLava = 50,
	Lava = 51,
	CaveHorizontalWallLava = 52,
	CaveVerticalWallLava = 53,
	HorizontalBridgeLava = 54,
	VerticalBridgeLava = 55,
	VerticalDiamond = 56,
	HorizontalDiamond = 57,
	PentagramClosed = 58,
	PentagramOpen = 59,
}

[Flags]
public enum AuthoritativeCathedralAutomapTileFlags : byte
{
	None = 0,
	VerticalDoor = 1 << 0,
	HorizontalDoor = 1 << 1,
	VerticalArch = 1 << 2,
	HorizontalArch = 1 << 3,
	VerticalGrate = 1 << 4,
	HorizontalGrate = 1 << 5,
	Dirt = 1 << 6,
	Stairs = 1 << 7,
	VerticalPassage = VerticalDoor | VerticalArch | VerticalGrate,
	HorizontalPassage = HorizontalDoor | HorizontalArch | HorizontalGrate,
}

/** One immutable semantic entry from the detached original-Diablo l1.amp table. */
public readonly record struct AuthoritativeCathedralAutomapTileDefinition(
	byte TileId,
	AuthoritativeCathedralAutomapTileType Type,
	AuthoritativeCathedralAutomapTileFlags Flags);

/**
 * Complete detached AutomapTile metadata for ordinary original-Diablo Cathedral levels.
 *
 * The native table is indexed by an unsigned byte and has a deliberately implicit zero
 * entry: InitAutomap assigns external records to indices one through the loaded count.
 * This catalog makes that cold tile explicit as row zero and requires all 256 entries.
 * It does not read MPQs, invoke native code, or infer automap semantics from geometry.
 */
public sealed class AuthoritativeCathedralAutomapMetadata
{
	public const string SchemaId = "godevilution.cathedral-automap-metadata";
	public const string SchemaVersion = "1.0.0";
	public const string CatalogId = "native-cathedral-automap";
	public const int GeneratorRevision = 1;
	public const string TableName = "cathedral_automap.tsv";
	public const int TileCount = 256;
	public const int CoarseWidth = 40;
	public const int CoarseHeight = 40;
	public const int CoarseCellCount = CoarseWidth * CoarseHeight;
	public const int ExplorationCellCount = CoarseCellCount;
	public const byte MaximumType = (byte)AuthoritativeCathedralAutomapTileType.PentagramOpen;

	private static readonly string[] Columns = ["id", "type", "flags"];

	private readonly ReadOnlyCollection<AuthoritativeCathedralAutomapTileDefinition> definitions;
	private readonly ReadOnlyCollection<TsvTable> tables;

	private AuthoritativeCathedralAutomapMetadata(TsvTable table,
		IReadOnlyList<AuthoritativeCathedralAutomapTileDefinition> definitions)
	{
		this.definitions = Array.AsReadOnly(definitions.ToArray());
		tables = Array.AsReadOnly([CloneTable(table)]);
		Fingerprint = Convert.ToHexString(SHA256.HashData(
			Encoding.UTF8.GetBytes(table.ToCanonicalTsv())));
	}

	public IReadOnlyList<AuthoritativeCathedralAutomapTileDefinition> Definitions => definitions;
	public IReadOnlyList<AuthoritativeCathedralAutomapTileDefinition> Tiles => definitions;
	public IReadOnlyList<TsvTable> Tables => tables;
	public string Fingerprint { get; }
	public string CatalogFingerprint => Fingerprint;

	public static IReadOnlyList<string> TableColumns
		=> Array.AsReadOnly(Columns.ToArray());

	/** Loads the one detached table directly from UTF-16 text decoded from an external UTF-8 file. */
	public static AuthoritativeCathedralAutomapMetadata Load(string contents)
	{
		ArgumentNullException.ThrowIfNull(contents);
		return LoadTable(TsvTable.Parse(TableName, contents));
	}

	/** Explicit parser name used by detached sidecar loaders. */
	public static AuthoritativeCathedralAutomapMetadata ParseTsv(string contents)
		=> Load(contents);

	/** Decodes an external UTF-8 table strictly before applying TSV validation. */
	public static AuthoritativeCathedralAutomapMetadata LoadUtf8(ReadOnlySpan<byte> contents)
	{
		try {
			return Load(new UTF8Encoding(false, true).GetString(contents));
		} catch (DecoderFallbackException exception) {
			throw new InvalidDataException($"{TableName} is not valid UTF-8.", exception);
		}
	}

	/** Loads a standalone versioned sidecar pack without changing the legacy 19-table pack. */
	public static AuthoritativeCathedralAutomapMetadata Load(ContentPack pack)
	{
		ArgumentNullException.ThrowIfNull(pack);
		if (pack.Schema is null || pack.Schema.Id != SchemaId
			|| pack.Schema.Version.ToString() != SchemaVersion
			|| pack.Dependencies.Count != 0 || pack.DependencyConstraints.Count != 0
			|| pack.Overlays.Count != 0 || pack.Assets.Count != 0
			|| pack.Tables.Count != 1 || pack.Tables[0].SourcePath != TableName)
			throw new InvalidDataException("A standalone version-1 Cathedral automap metadata pack is required.");
		return LoadTable(CloneTable(pack.Tables[0]));
	}

	/** Returns the detached semantic pack used by sidecar composition and identity checks. */
	public ContentPack ToContentPack()
		=> new(CatalogId, SchemaVersion, tables.Select(CloneTable).ToArray(),
			schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));

	public string ToCanonicalTsv() => tables[0].ToCanonicalTsv();

	/** Builds and validates the canonical table used by a sidecar pack composer. */
	public static TsvTable CreateTable(
		IEnumerable<AuthoritativeCathedralAutomapTileDefinition> definitions)
	{
		ArgumentNullException.ThrowIfNull(definitions);
		var entries = definitions.ToArray();
		var output = new StringBuilder(string.Join('\t', Columns)).Append('\n');
		foreach (var definition in entries)
			output.Append(definition.TileId.ToString(CultureInfo.InvariantCulture)).Append('\t')
				.Append(((byte)definition.Type).ToString(CultureInfo.InvariantCulture)).Append('\t')
				.Append(((byte)definition.Flags).ToString(CultureInfo.InvariantCulture)).Append('\n');
		return Load(output.ToString()).Tables[0];
	}

	public static string ToCanonicalTsv(
		IEnumerable<AuthoritativeCathedralAutomapTileDefinition> definitions)
		=> CreateTable(definitions).ToCanonicalTsv();

	public AuthoritativeCathedralAutomapTileDefinition Get(int tileId)
	{
		if ((uint)tileId >= TileCount)
			throw new InvalidDataException($"Cathedral automap tile id {tileId} does not fit the native uint8 table.");
		return definitions[tileId];
	}

	/** Resolves the native uint16 carrier, refusing values that cannot index AutomapTypeTiles. */
	public AuthoritativeCathedralAutomapTileDefinition Resolve(ushort tileId)
		=> Get(tileId);

	/**
	 * Applies native SetAutomapView semantics for one fine-grid world position. The supplied
	 * 40x40 mega-tile carrier is row-major (x + y * 40), matching Cathedral generator results.
	 */
	public void ProjectExploration(IReadOnlyList<ushort> coarseTiles,
		int positionX, int positionY, byte explorer, Span<byte> exploration)
	{
		ValidateProjectionInputs(coarseTiles, explorer, exploration);
		var mapX = (positionX - 16) / 2;
		var mapY = (positionY - 16) / 2;
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return;
		ProjectExplorationAtMap(coarseTiles, mapX, mapY, explorer, exploration);
	}

	public void ProjectExploration(IReadOnlyList<ushort> coarseTiles,
		AuthoritativeCathedralPoint position, byte explorer, Span<byte> exploration)
		=> ProjectExploration(coarseTiles, position.X, position.Y, explorer, exploration);

	/** Applies SetAutomapView semantics when the caller already has the coarse map coordinate. */
	public void ProjectExplorationAtMap(IReadOnlyList<ushort> coarseTiles,
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

	public void ProjectExploration(IReadOnlyList<ushort> coarseTiles,
		int positionX, int positionY, byte explorer, byte[] exploration)
	{
		ArgumentNullException.ThrowIfNull(exploration);
		ProjectExploration(coarseTiles, positionX, positionY, explorer, exploration.AsSpan());
	}

	public IReadOnlyList<byte> ProjectExploration(IReadOnlyList<ushort> coarseTiles,
		int positionX, int positionY, byte explorer)
	{
		var exploration = new byte[ExplorationCellCount];
		ProjectExploration(coarseTiles, positionX, positionY, explorer, exploration.AsSpan());
		return Array.AsReadOnly(exploration);
	}

	public IReadOnlyList<byte> ProjectExploration(IReadOnlyList<ushort> coarseTiles,
		AuthoritativeCathedralPoint position, byte explorer)
		=> ProjectExploration(coarseTiles, position.X, position.Y, explorer);

	public IReadOnlyList<byte> ProjectExplorationAtMap(IReadOnlyList<ushort> coarseTiles,
		int mapX, int mapY, byte explorer)
	{
		var exploration = new byte[ExplorationCellCount];
		ProjectExplorationAtMap(coarseTiles, mapX, mapY, explorer, exploration.AsSpan());
		return Array.AsReadOnly(exploration);
	}

	private static void ValidateProjectionInputs(IReadOnlyList<ushort> coarseTiles,
		byte explorer, Span<byte> exploration)
	{
		ArgumentNullException.ThrowIfNull(coarseTiles);
		if (coarseTiles.Count != CoarseCellCount)
			throw new InvalidDataException("Cathedral automap projection requires exactly 1600 coarse tile IDs.");
		if (exploration.Length != ExplorationCellCount)
			throw new ArgumentException("Cathedral automap exploration must contain exactly 1600 bytes.", nameof(exploration));
		if (explorer > 4)
			throw new InvalidDataException("Cathedral automap exploration type must be a native value from 0 through 4.");
		if (coarseTiles.Any(tile => tile >= TileCount))
			throw new InvalidDataException("Cathedral coarse automap tile IDs must fit the native uint8 table.");
	}

	private AuthoritativeCathedralAutomapTileDefinition EffectiveTile(
		IReadOnlyList<ushort> coarseTiles, int mapX, int mapY)
	{
		if ((uint)mapX >= CoarseWidth || (uint)mapY >= CoarseHeight)
			return default;
		var tile = Get(coarseTiles[mapX + mapY * CoarseWidth]);
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
			&& HasFlag(Get(coarseTiles[mapX + mapY * CoarseWidth]), flag);

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

	private static AuthoritativeCathedralAutomapMetadata LoadTable(TsvTable table)
	{
		if (!table.Columns.SequenceEqual(Columns, StringComparer.Ordinal))
			throw new InvalidDataException(
				$"{TableName} must have the exact ordered header: {string.Join(',', Columns)}.");
		if (table.Rows.Count != TileCount)
			throw new InvalidDataException($"{TableName} must contain exactly {TileCount} ordered rows.");

		var definitions = new AuthoritativeCathedralAutomapTileDefinition[TileCount];
		foreach (var (row, index) in table.Rows.Select((row, index) => (row, index))) {
			if (row.Values.Count != Columns.Length || !row.Values.Keys.Order(StringComparer.Ordinal)
				.SequenceEqual(Columns.Order(StringComparer.Ordinal), StringComparer.Ordinal))
				throw new InvalidDataException($"{TableName} row {row.LineNumber} requires exactly the declared columns.");
			var tileId = ParseByte(row, "id");
			if (tileId != index)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} must retain tile order {index}.");
			var type = ParseByte(row, "type");
			if (type > MaximumType)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} has an unsupported automap type {type}.");
			var flags = ParseByte(row, "flags");
			if (index == 0 && (type != (byte)AuthoritativeCathedralAutomapTileType.None || flags != 0))
				throw new InvalidDataException($"{TableName} tile zero must remain the native cold {nameof(AuthoritativeCathedralAutomapTileType.None)} tile with no flags.");
			definitions[index] = new((byte)tileId,
				(AuthoritativeCathedralAutomapTileType)type,
				(AuthoritativeCathedralAutomapTileFlags)flags);
		}
		return new(table, definitions);
	}

	private static byte ParseByte(TsvRow row, string column)
	{
		var text = row.Required(column);
		if (!byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException(
				$"{TableName} row {row.LineNumber} column '{column}' must be an unsigned 8-bit integer.");
		return value;
	}

	private static TsvTable CloneTable(TsvTable table)
		=> new(table.SourcePath,
			new ReadOnlyCollection<string>(table.Columns.ToArray()),
			new ReadOnlyCollection<TsvRow>(table.Rows.Select(row => new TsvRow(
				row.LineNumber,
				new ReadOnlyDictionary<string, string>(row.Values.ToDictionary(
					pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)))).ToArray()));
}
