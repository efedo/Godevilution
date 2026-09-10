using System.Globalization;
using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Validated external definition for a server-owned generated level layout. */
public sealed record AuthoritativeProceduralLevelDefinition(
    uint LevelId,
    int Width,
    int Height,
    uint GenerationSeed,
    int BlockedPercent,
    bool IsDefaultStart = false,
    bool SuppressMultiplayerDeathDrops = false,
    int? MissileBlockedPercent = null,
    int StartPositionX = 0,
    int StartPositionY = 0,
    int? VisionBlockedPercent = null,
    IReadOnlyList<(int X, int Y)>? RequiredWalkableCells = null)
{
    /** Stable level symbol retained for composed-pack identity diagnostics. */
    public string StableSymbol { get; init; } = string.Empty;

    public AuthoritativeProceduralLevelDefinition Validate()
    {
        if (Width < 3
            || Height < 3
            || Width > AuthoritativeLevel.NativeMaximumWidth
            || Height > AuthoritativeLevel.NativeMaximumHeight)
            throw new InvalidDataException(
                $"Procedural level {LevelId} dimensions must be between 3 and the native "
                + $"maximum of {AuthoritativeLevel.NativeMaximumWidth}x{AuthoritativeLevel.NativeMaximumHeight}.");
        if (BlockedPercent is < 0 or > 80)
            throw new InvalidDataException(
                $"Procedural level {LevelId} blocked percent must be between 0 and 80.");
        if (MissileBlockedPercent is < 0 or > 80)
            throw new InvalidDataException(
                $"Procedural level {LevelId} missile-blocked percent must be between 0 and 80.");
        if (VisionBlockedPercent is < 0 or > 80)
            throw new InvalidDataException(
                $"Procedural level {LevelId} vision-blocked percent must be between 0 and 80.");
        if (StartPositionX < 0 || StartPositionX >= Width
            || StartPositionY < 0 || StartPositionY >= Height) {
            throw new InvalidDataException(
                $"Procedural level {LevelId} start position ({StartPositionX}, {StartPositionY}) is outside its dimensions.");
        }
        var requiredCells = RequiredWalkableCells ?? [];
        if (requiredCells.Distinct().Count() != requiredCells.Count)
            throw new InvalidDataException(
                $"Procedural level {LevelId} required-walkable cells must be unique.");
        foreach (var (x, y) in requiredCells) {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new InvalidDataException(
                    $"Procedural level {LevelId} required-walkable cell ({x}, {y}) is outside its dimensions.");
        }
        return this;
    }

    /** Generates a deterministic connected layout from the external seed. */
    public AuthoritativeLevel Generate(uint worldSeed = 0)
    {
        Validate();
        var requiredCells = (RequiredWalkableCells ?? [])
            .Select(cell => cell.Y * Width + cell.X)
            .Append(StartPositionY * Width + StartPositionX)
            .Distinct()
            .ToArray();
        var layout = DeterministicProceduralGrid.Generate(
            Width,
            Height,
            GenerationSeed ^ worldSeed,
            BlockedPercent,
            MissileBlockedPercent ?? BlockedPercent,
            requiredCells,
            VisionBlockedPercent ?? BlockedPercent);
        var level = new AuthoritativeLevel(
            LevelId,
            Width,
            Height,
            layout.BlockedCells,
            layout.MissileBlockedCells,
            IsDefaultStart,
            SuppressMultiplayerDeathDrops,
            startPositionX: StartPositionX,
            startPositionY: StartPositionY,
            nativeVisionBlockedCells: VisionBlockedPercent.HasValue
                ? layout.NativeVisionBlockedCells
                : null) {
            StableSymbol = StableSymbol,
        };
        level.ValidateStartPosition();
        return level;
    }
}

/** Loads and materializes optional procedural level definitions from content. */
public sealed class AuthoritativeProceduralLevelCatalog
{
    private static readonly IReadOnlyList<string> SymbolicProductionColumns = [
        "level_id",
        "level_symbol",
        "width",
        "height",
        "generation_seed",
        "blocked_percent",
        "missile_blocked_percent",
        "default_start",
        "suppress_multiplayer_death_drops",
        "required_walkable_cells",
    ];

    private readonly IReadOnlyList<AuthoritativeProceduralLevelDefinition> definitions;

    public AuthoritativeProceduralLevelCatalog(
        IEnumerable<AuthoritativeProceduralLevelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.LevelId)
            .ToArray();
        if (ordered.Length == 0)
            throw new InvalidDataException(
                "The procedural-level catalog cannot be empty.");
        if (ordered.Select(definition => definition.LevelId).Distinct().Count() != ordered.Length)
            throw new InvalidDataException(
                "Procedural-level definitions must use unique level IDs.");
        this.definitions = ordered;
    }

    public IReadOnlyList<AuthoritativeProceduralLevelDefinition> Definitions => definitions;

    /** Rejects partial procedural-level metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(SymbolicProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production procedural-level table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows) {
            foreach (var column in SymbolicProductionColumns) {
                if (column == "required_walkable_cells") {
                    if (!row.TryGet(column, out _))
                        throw new InvalidDataException(
                            $"Column '{column}' is not present on TSV row {row.LineNumber}.");
                } else {
                    _ = row.Required(column);
                }
            }
        }
    }

    public AuthoritativeWorld GenerateWorld(uint worldSeed = 0)
    {
        var world = new AuthoritativeWorld();
        foreach (var definition in definitions)
            world.AddLevel(definition.Generate(worldSeed));
        return world;
    }

    public static AuthoritativeProceduralLevelCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeProceduralLevelCatalog(table.Rows.Select(row =>
            new AuthoritativeProceduralLevelDefinition(
                row.RequiredUInt32("level_id"),
                row.RequiredInt32("width"),
                row.RequiredInt32("height"),
                row.OptionalUInt32("generation_seed"),
                row.OptionalInt32("blocked_percent"),
                row.OptionalBoolean("default_start"),
                row.OptionalBoolean("suppress_multiplayer_death_drops"),
                row.TryGet("missile_blocked_percent", out _)
                    ? row.OptionalInt32("missile_blocked_percent")
                    : null,
                row.TryGet("start_x", out _)
                    ? row.RequiredInt32("start_x")
                    : 0,
                row.TryGet("start_y", out _)
                    ? row.RequiredInt32("start_y")
                    : 0,
                row.TryGet("vision_blocked_percent", out _)
                    ? row.OptionalInt32("vision_blocked_percent")
                    : null,
                ParseRequiredWalkableCells(row,
                    row.RequiredInt32("width"),
                    row.RequiredInt32("height"))) {
                StableSymbol = row.TryGet("level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
            }));
    }

    private static IReadOnlyList<(int X, int Y)> ParseRequiredWalkableCells(
        TsvRow row,
        int width,
        int height)
    {
        if (!row.TryGet("required_walkable_cells", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var cells = new List<(int X, int Y)>();
        foreach (var token in value.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            var coordinates = token.Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 2
                || !int.TryParse(coordinates[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(coordinates[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                throw new InvalidDataException(
                    $"Column 'required_walkable_cells' on TSV row {row.LineNumber} must contain semicolon-separated x,y coordinates.");
            if (x < 0 || x >= width || y < 0 || y >= height)
                throw new InvalidDataException(
                    $"Column 'required_walkable_cells' on TSV row {row.LineNumber} contains ({x}, {y}) outside its dimensions.");
            cells.Add((x, y));
        }
        return cells;
    }

}
