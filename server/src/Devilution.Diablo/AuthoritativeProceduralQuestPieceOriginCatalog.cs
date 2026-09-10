using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** One deterministic ordinary-level origin reserved for a generated quest piece. */
public sealed record AuthoritativeProceduralQuestPieceOriginDefinition(
    uint OriginId,
    uint LevelId,
    int FootprintWidth,
    int FootprintHeight,
    uint SelectionSeed,
    int Alignment = 2)
{
    /** Stable content symbol retained for composed-pack diagnostics. */
    public string OriginSymbol { get; init; } = string.Empty;

    /** Stable ordinary-level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeProceduralQuestPieceOriginDefinition Validate()
    {
        if (OriginId == 0
            || LevelId == 0
            || FootprintWidth is < 1 or > 128
            || FootprintHeight is < 1 or > 128
            || Alignment is < 1 or > 16
            || (Alignment & (Alignment - 1)) != 0) {
            throw new InvalidDataException(
                $"Procedural quest-piece origin {OriginId} contains invalid level, footprint, or alignment data.");
        }
        return this;
    }
}

/** Runtime location selected for an ordinary-level procedural quest piece. */
public sealed record AuthoritativeProceduralQuestPieceOrigin(
    uint LevelId,
    int PositionX,
    int PositionY);

/** Validated deterministic origins for generated quest geometry on ordinary levels. */
public sealed class AuthoritativeProceduralQuestPieceOriginCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "origin_id",
        "origin_symbol",
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
                $"Production procedural quest-piece origin table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("origin_id");
            _ = row.RequiredUInt32("level_id");
            _ = row.RequiredInt32("footprint_width");
            _ = row.RequiredInt32("footprint_height");
            _ = row.RequiredUInt32("selection_seed");
            _ = row.RequiredInt32("alignment");
        }
    }

    private readonly IReadOnlyList<AuthoritativeProceduralQuestPieceOriginDefinition> definitions;

    public AuthoritativeProceduralQuestPieceOriginCatalog(
        IEnumerable<AuthoritativeProceduralQuestPieceOriginDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.OriginId)
            .ToArray();
        if (values.Select(definition => definition.OriginId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "Procedural quest-piece origin rules must use unique origin identities.");
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeProceduralQuestPieceOriginDefinition> Definitions
        => definitions;

    /**
     * Plans every origin whose target level is available. Missing levels remain
     * deferred so metadata-only or partially composed packs stay compatible.
     */
    public IReadOnlyDictionary<uint, AuthoritativeProceduralQuestPieceOrigin> Plan(
        AuthoritativeWorld world,
        uint worldSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        var origins = new Dictionary<uint, AuthoritativeProceduralQuestPieceOrigin>();
        foreach (var definition in definitions) {
            var level = world.Levels.SingleOrDefault(candidate => candidate.LevelId == definition.LevelId);
            if (level is null)
                continue;

            var selectionSeed = DeterministicHash.Mix32(
                definition.SelectionSeed
                ^ worldSeed
                ^ unchecked((uint)definition.LevelId * 0x9E3779B9U)
                ^ unchecked(definition.OriginId * 0x85EBCA6BU));
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
                    $"Procedural quest-piece origin {definition.OriginId} has no walkable placement on level {definition.LevelId}.");
            }
            origins.Add(
                definition.OriginId,
                new AuthoritativeProceduralQuestPieceOrigin(
                    definition.LevelId,
                    selected.X,
                    selected.Y));
        }
        return origins;
    }

    public static AuthoritativeProceduralQuestPieceOriginCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeProceduralQuestPieceOriginCatalog(table.Rows.Select(row =>
            new AuthoritativeProceduralQuestPieceOriginDefinition(
                ResolveOriginId(row, stableIds),
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                row.RequiredInt32("footprint_width"),
                row.RequiredInt32("footprint_height"),
                row.OptionalUInt32("selection_seed"),
                row.TryGet("alignment", out _)
                    ? row.OptionalInt32("alignment")
                    : 2) {
                OriginSymbol = row.TryGet("origin_symbol", out var originSymbol)
                    ? originSymbol
                    : string.Empty,
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            }));
    }

    private static uint ResolveOriginId(TsvRow row, StableContentIdCatalog? stableIds)
    {
        var numericPresent = row.TryGet("origin_id", out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var numericId = row.OptionalUInt32("origin_id");
        var symbol = row.TryGet("origin_symbol", out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : string.Empty;
        if (symbol.Length == 0) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Procedural quest-piece origin row {row.LineNumber} requires a non-zero origin_id.");
            return numericId;
        }
        if (stableIds is null) {
            if (!numericPresent || numericId == 0)
                throw new InvalidDataException(
                    $"Procedural quest-piece origin row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("procedural_origin", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Procedural quest-piece origin row {row.LineNumber} references unknown symbol '{symbol}'.",
                exception);
        }
        if (resolvedId == 0)
            throw new InvalidDataException(
                $"Procedural quest-piece origin row {row.LineNumber} resolves '{symbol}' to zero.");
        if (numericPresent && numericId != resolvedId)
            throw new InvalidDataException(
                $"Procedural quest-piece origin row {row.LineNumber} maps origin {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }
}
