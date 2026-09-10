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
    int StartPositionY = 0)
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
        if (StartPositionX < 0 || StartPositionX >= Width
            || StartPositionY < 0 || StartPositionY >= Height) {
            throw new InvalidDataException(
                $"Procedural level {LevelId} start position ({StartPositionX}, {StartPositionY}) is outside its dimensions.");
        }
        return this;
    }

    /** Generates a deterministic connected layout from the external seed. */
    public AuthoritativeLevel Generate(uint worldSeed = 0)
    {
        Validate();
        var layout = DeterministicProceduralGrid.Generate(
            Width,
            Height,
            GenerationSeed ^ worldSeed,
            BlockedPercent,
            MissileBlockedPercent ?? BlockedPercent,
            [StartPositionY * Width + StartPositionX]);
        var level = new AuthoritativeLevel(
            LevelId,
            Width,
            Height,
            layout.BlockedCells,
            layout.MissileBlockedCells,
            IsDefaultStart,
            SuppressMultiplayerDeathDrops,
            startPositionX: StartPositionX,
            startPositionY: StartPositionY) {
            StableSymbol = StableSymbol,
        };
        level.ValidateStartPosition();
        return level;
    }
}

/** Loads and materializes optional procedural level definitions from content. */
public sealed class AuthoritativeProceduralLevelCatalog
{
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
                    : 0) {
                StableSymbol = row.TryGet("level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
            }));
    }

}
