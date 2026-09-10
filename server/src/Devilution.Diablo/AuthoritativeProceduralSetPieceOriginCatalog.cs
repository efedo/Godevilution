using Devilution.Engine;
using Devilution.Data;

namespace Devilution.Diablo;

/** External rule for selecting a deterministic origin for a generated set piece. */
public sealed record AuthoritativeProceduralSetPieceOriginDefinition(
    int SetLevelId,
    uint LevelId,
    int FootprintWidth,
    int FootprintHeight,
    uint SelectionSeed,
    int Alignment = 2)
{
    /** Stable set-level symbol retained for composed-pack diagnostics. */
    public string SetLevelSymbol { get; init; } = string.Empty;

    /** Stable ordinary-level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeProceduralSetPieceOriginDefinition Validate()
    {
        if (SetLevelId is < 0 or > 8
            || LevelId == 0
            || FootprintWidth is < 1 or > 128
            || FootprintHeight is < 1 or > 128
            || Alignment is < 1 or > 16
            || (Alignment & (Alignment - 1)) != 0) {
            throw new InvalidDataException(
                $"Procedural set-piece origin {SetLevelId} contains invalid level, footprint, or alignment data.");
        }
        return this;
    }
}

/** Validated deterministic origin rules for generated set pieces. */
public sealed class AuthoritativeProceduralSetPieceOriginCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "set_level_id",
        "set_level_symbol",
        "level_id",
        "level_symbol",
        "footprint_width",
        "footprint_height",
        "selection_seed",
        "alignment",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production procedural set-piece origin table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredInt32("set_level_id");
            _ = row.RequiredUInt32("level_id");
            _ = row.RequiredInt32("footprint_width");
            _ = row.RequiredInt32("footprint_height");
            _ = row.RequiredUInt32("selection_seed");
            _ = row.RequiredInt32("alignment");
        }
    }

    private readonly IReadOnlyList<AuthoritativeProceduralSetPieceOriginDefinition> definitions;

    public AuthoritativeProceduralSetPieceOriginCatalog(
        IEnumerable<AuthoritativeProceduralSetPieceOriginDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.SetLevelId)
            .ToArray();
        if (values.Select(definition => definition.SetLevelId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "Procedural set-piece origin rules must use unique set-level identities.");
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeProceduralSetPieceOriginDefinition> Definitions
        => definitions;

    /**
     * Plans every origin whose target level is available. Missing levels remain
     * deferred so metadata-only or partially composed packs stay compatible.
     */
    public IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin> Plan(
        AuthoritativeWorld world,
        uint worldSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        var origins = new Dictionary<int, AuthoritativeSetLevelObjectOrigin>();
        foreach (var definition in definitions) {
            var level = world.Levels.SingleOrDefault(candidate => candidate.LevelId == definition.LevelId);
            if (level is null)
                continue;

            var selectionSeed = DeterministicHash.Mix32(
                definition.SelectionSeed
                ^ worldSeed
                ^ unchecked((uint)definition.LevelId * 0x9E3779B9U));
            (int X, int Y) selected;
            try {
                selected = DeterministicGridPlacement.SelectAlignedWalkableFootprint(
                    level.Width,
                    level.Height,
                    definition.FootprintWidth,
                    definition.FootprintHeight,
                    definition.Alignment,
                    selectionSeed,
                    level.IsWalkable);
            } catch (InvalidOperationException) {
                throw new InvalidDataException(
                    $"Procedural set-piece origin {definition.SetLevelId} has no walkable placement on level {definition.LevelId}.");
            }
            origins.Add(
                definition.SetLevelId,
                new AuthoritativeSetLevelObjectOrigin(
                    definition.LevelId,
                    selected.X,
                    selected.Y));
        }
        return origins;
    }

    public static AuthoritativeProceduralSetPieceOriginCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeProceduralSetPieceOriginCatalog(table.Rows.Select(row =>
            new AuthoritativeProceduralSetPieceOriginDefinition(
                AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "set_level_id",
                    "set_level_symbol"),
                AuthoritativeLevelIdentity.Resolve(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredInt32("footprint_width"),
                row.RequiredInt32("footprint_height"),
                row.OptionalUInt32("selection_seed"),
                row.TryGet("alignment", out _)
                    ? row.OptionalInt32("alignment")
                    : 2) {
                SetLevelSymbol = row.TryGet("set_level_symbol", out var setLevelSymbol)
                    ? setLevelSymbol
                    : string.Empty,
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            }));
    }

}
