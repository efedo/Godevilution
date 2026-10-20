using Devilution.Data;

namespace Devilution.Diablo;

/** Canonical resident identity and placement compiled from the base Town roster. */
public sealed record AuthoritativeNativeTownResident(
    int TownerId,
    string Symbol,
    string Name,
    string ShortName,
    int PositionX,
    int PositionY,
    string Direction);

/** Original Diablo resident roster and its native occupancy footprint. */
public sealed class AuthoritativeNativeTownRoster
{
    public const string TableName = "native_town_geometry/roster.tsv";

    private static readonly string[] RequiredColumns = [
        "towner_id",
        "towner_symbol",
        "name",
        "short_name",
        "position_x",
        "position_y",
        "direction",
    ];

    private static readonly AuthoritativeNativeTownResident[] CanonicalResidents = [
        new(0, "smith", "Griswold the Blacksmith", "griswold", 62, 63, "south_west"),
        new(1, "healer", "Pepin the Healer", "pepin", 55, 79, "south_east"),
        new(3, "tavern", "Ogden the Tavern owner", "ogden", 55, 62, "south_west"),
        new(4, "story", "Cain the Elder", "cain", 62, 71, "south"),
        new(5, "drunk", "Farnham the Drunk", "farnham", 71, 84, "south"),
        new(6, "witch", "Adria the Witch", "adria", 80, 20, "south"),
        new(7, "barmaid", "Gillian the Barmaid", "gillian", 43, 66, "south"),
        new(8, "pegboy", "Wirt the Peg-legged boy", "wirt", 11, 53, "south"),
        new(9, "cow", "Cow", "cow", 58, 16, "south_west"),
        new(9, "cow", "Cow", "cow", 56, 14, "north_west"),
        new(9, "cow", "Cow", "cow", 59, 20, "north"),
    ];

    private readonly IReadOnlyList<AuthoritativeNativeTownResident> residents;
    private readonly IReadOnlyList<AuthoritativeNativeTownResident> humans;
    private readonly IReadOnlyList<int> occupiedCells;
    private readonly HashSet<int> occupiedCellSet;

    private AuthoritativeNativeTownRoster(
        IReadOnlyList<AuthoritativeNativeTownResident> residents)
    {
        this.residents = Array.AsReadOnly(residents.ToArray());
        humans = Array.AsReadOnly(residents.Where(resident => resident.TownerId != 9).ToArray());
        var cells = new HashSet<int>();
        foreach (var resident in residents) {
            AddCell(resident.PositionX, resident.PositionY);
            if (resident.TownerId == 9) {
                // InitCows places the other three footprint cells north and west of the anchor.
                AddCell(resident.PositionX - 1, resident.PositionY);
                AddCell(resident.PositionX, resident.PositionY - 1);
                AddCell(resident.PositionX - 1, resident.PositionY - 1);
            }
        }
        occupiedCells = Array.AsReadOnly(cells.Order().ToArray());
        occupiedCellSet = cells;

        void AddCell(int x, int y)
        {
            if (x < 0 || x >= AuthoritativeTownGeometry.FineWidth
                || y < 0 || y >= AuthoritativeTownGeometry.FineHeight) {
                throw new InvalidDataException("Canonical Town resident footprint exceeds the fine grid.");
            }
            cells.Add(y * AuthoritativeTownGeometry.FineWidth + x);
        }
    }

    public IReadOnlyList<AuthoritativeNativeTownResident> Residents => residents;
    public IReadOnlyList<AuthoritativeNativeTownResident> Humans => humans;
    /** Sorted fine-grid row-major cells occupied by humans and all four cells of each cow. */
    public IReadOnlyList<int> OccupiedCells => occupiedCells;

    public bool IsOccupied(int x, int y)
        => x >= 0 && x < AuthoritativeTownGeometry.FineWidth
            && y >= 0 && y < AuthoritativeTownGeometry.FineHeight
            && occupiedCellSet.Contains(y * AuthoritativeTownGeometry.FineWidth + x);

    /**
     * Validates the eight canonical human greeting rows while allowing only the native Smith
     * position to overlap SOL scenery. General world walkability rules remain unchanged.
     */
    public void ValidateGreetings(
        AuthoritativeTownerGreetingCatalog greetings,
        AuthoritativeTownGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(greetings);
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.NativePieceLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || geometry.NativeSolPropertyLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight) {
            throw new InvalidDataException("Native Town roster validation requires complete fine SOL channels.");
        }
        var definitions = greetings.Definitions.OrderBy(definition => definition.TownerId).ToArray();
        if (definitions.Length != humans.Count)
            throw new InvalidDataException("Native Town greetings must cover every canonical human resident exactly once.");

        foreach (var resident in humans) {
            var greeting = definitions.SingleOrDefault(definition => definition.TownerId == resident.TownerId);
            if (greeting is null
                || greeting.LevelId != AuthoritativeNativeTownCatalog.TownLevelId
                || greeting.PositionX != resident.PositionX
                || greeting.PositionY != resident.PositionY) {
                throw new InvalidDataException(
                    $"Native Town greeting for towner {resident.TownerId} does not match the canonical roster.");
            }
        }

        var smith = humans.Single(resident => resident.TownerId == 0);
        var smithCell = smith.PositionY * AuthoritativeTownGeometry.FineWidth + smith.PositionX;
        var smithAdjacentCell = (smith.PositionY + 1) * AuthoritativeTownGeometry.FineWidth + smith.PositionX;
        if (geometry.NativePieceLayer[smithCell] != 923
            || !geometry.NativeSolPropertyLayer[smithCell].HasFlag(AuthoritativeDunTileFlags.Solid)
            || geometry.NativePieceLayer[smithAdjacentCell] != 909
            || geometry.NativeSolPropertyLayer[smithAdjacentCell].HasFlag(AuthoritativeDunTileFlags.Solid)) {
            throw new InvalidDataException(
                "Native Town does not contain the canonical Smith scenery occupancy and adjacent walkable cell.");
        }

        var allowedScenery = new HashSet<int> { smithCell };
        var solid = geometry.NativeSolidCells.ToHashSet();
        foreach (var cell in occupiedCells) {
            if (solid.Contains(cell) && !allowedScenery.Contains(cell)) {
                throw new InvalidDataException(
                    $"Canonical Town resident occupancy overlaps SOL-blocked cell {cell}.");
            }
        }
    }

    internal static AuthoritativeNativeTownRoster Load(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"{TableName} must have the exact ordered header: {string.Join(',', RequiredColumns)}.");
        if (table.Rows.Count != CanonicalResidents.Length)
            throw new InvalidDataException(
                $"{TableName} must contain exactly {CanonicalResidents.Length} original Diablo base residents.");

        var parsed = table.Rows.Select(row => new AuthoritativeNativeTownResident(
            row.RequiredInt32("towner_id"),
            row.Required("towner_symbol"),
            row.Required("name"),
            row.Required("short_name"),
            row.RequiredInt32("position_x"),
            row.RequiredInt32("position_y"),
            row.Required("direction"))).ToArray();
        for (var index = 0; index < CanonicalResidents.Length; index++) {
            if (parsed[index] != CanonicalResidents[index])
                throw new InvalidDataException(
                    $"{TableName} row {table.Rows[index].LineNumber} does not match the canonical original Diablo resident roster.");
        }
        return new AuthoritativeNativeTownRoster(parsed);
    }
}
