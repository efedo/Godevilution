using Devilution.Data;

namespace Devilution.Diablo;

/** One validated content-owned entry point for a registered level. */
public sealed record AuthoritativeLevelStartPosition(
    uint LevelId,
    string LevelSymbol,
    int StartPositionX,
    int StartPositionY);

/** Optional strict catalog of entry points applied after all world geometry is materialized. */
public sealed class AuthoritativeLevelStartCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "level_id",
        "level_symbol",
        "start_x",
        "start_y",
    ];

    private readonly IReadOnlyList<AuthoritativeLevelStartPosition> definitions;

    private AuthoritativeLevelStartCatalog(
        IReadOnlyList<AuthoritativeLevelStartPosition> definitions)
    {
        this.definitions = definitions;
    }

    public IReadOnlyList<AuthoritativeLevelStartPosition> Definitions => definitions;

    /** Requires the complete symbolic schema whenever this optional table is supplied. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal)) {
            throw new InvalidDataException(
                $"Level-start table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        }
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    public static AuthoritativeLevelStartCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog stableIds)
    {
        ArgumentNullException.ThrowIfNull(stableIds);
        var table = TsvTable.Parse(sourcePath, contents);
        ValidateExternalizedProduction(table);

        var definitions = new List<AuthoritativeLevelStartPosition>(table.Rows.Count);
        var seenLevelIds = new HashSet<uint>();
        foreach (var row in table.Rows) {
            var levelId = AuthoritativeLevelIdentity.Resolve(row, stableIds);
            var levelSymbol = row.Required("level_symbol");
            if (!seenLevelIds.Add(levelId)) {
                throw new InvalidDataException(
                    $"Level-start table '{sourcePath}' contains duplicate level {levelId}.");
            }
            definitions.Add(new AuthoritativeLevelStartPosition(
                levelId,
                levelSymbol,
                row.RequiredInt32("start_x"),
                row.RequiredInt32("start_y")));
        }
        return new AuthoritativeLevelStartCatalog(definitions.AsReadOnly());
    }

    /** Checks the effective geometry after deferred fragments and active mutations. */
    public void ValidateEffectiveGeometry(DiabloWorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        if (worldState.World is null)
            throw new InvalidDataException("Level-start positions require an active simulation world.");
        foreach (var definition in definitions) {
            if (!worldState.IsWalkable(
                    definition.LevelId,
                    definition.StartPositionX,
                    definition.StartPositionY)) {
                throw new InvalidDataException(
                    $"Level-start entry {definition.LevelId}/{definition.LevelSymbol} contains an invalid start position "
                    + $"({definition.StartPositionX}, {definition.StartPositionY}) in effective geometry.");
            }
        }
    }

    /** Validates every override and stages every replacement before mutating the world. */
    public void Apply(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var replacements = new List<AuthoritativeLevel>(definitions.Count);
        foreach (var definition in definitions) {
            var level = world.Levels.SingleOrDefault(candidate => candidate.LevelId == definition.LevelId);
            if (level is null) {
                throw new InvalidDataException(
                    $"Level-start entry {definition.LevelId}/{definition.LevelSymbol} references an unregistered level.");
            }
            if (!string.Equals(level.StableSymbol, definition.LevelSymbol, StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Level-start entry {definition.LevelId}/{definition.LevelSymbol} does not match the registered level symbol '{level.StableSymbol}'.");
            }
            if (!world.IsWalkable(
                    definition.LevelId,
                    definition.StartPositionX,
                    definition.StartPositionY)) {
                throw new InvalidDataException(
                    $"Level-start entry {definition.LevelId}/{definition.LevelSymbol} contains an invalid start position "
                    + $"({definition.StartPositionX}, {definition.StartPositionY}).");
            }
            replacements.Add(level.WithStartPosition(
                definition.StartPositionX,
                definition.StartPositionY));
        }

        foreach (var replacement in replacements)
            world.ReplaceLevel(replacement);
    }
}
