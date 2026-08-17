using Devilution.Data;

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
    int? MissileBlockedPercent = null)
{
    /** Stable level symbol retained for composed-pack identity diagnostics. */
    public string StableSymbol { get; init; } = string.Empty;

    public AuthoritativeProceduralLevelDefinition Validate()
    {
        if (Width < 3 || Height < 3 || Width > 512 || Height > 512)
            throw new InvalidDataException(
                $"Procedural level {LevelId} dimensions must be between 3 and 512.");
        if (BlockedPercent is < 0 or > 80)
            throw new InvalidDataException(
                $"Procedural level {LevelId} blocked percent must be between 0 and 80.");
        if (MissileBlockedPercent is < 0 or > 80)
            throw new InvalidDataException(
                $"Procedural level {LevelId} missile-blocked percent must be between 0 and 80.");
        return this;
    }

    /** Generates a deterministic connected layout from the external seed. */
    public AuthoritativeLevel Generate(uint worldSeed = 0)
    {
        Validate();
        var seed = GenerationSeed ^ worldSeed;
        var missileBlockedPercent = MissileBlockedPercent ?? BlockedPercent;
        var blocked = new HashSet<int>();
        var missileBlocked = new HashSet<int>();
        for (var y = 1; y < Height - 1; y++) {
            for (var x = 1; x < Width - 1; x++) {
                // Keep a one-cell lattice of corridors and only block pocket
                // cells adjacent to that lattice, so every generated map
                // remains connected even when the authored density is high.
                if (x % 4 != 2 || y % 4 != 2)
                    continue;
                var cell = y * Width + x;
                var hash = Mix(seed + checked((uint)cell) * 0x9E3779B9U);
                if (hash % 100U < (uint)BlockedPercent)
                    blocked.Add(cell);
                var missileHash = Mix(
                    seed + checked((uint)cell) * 0x9E3779B9U ^ 0xA511E9B3U);
                if (missileHash % 100U < (uint)missileBlockedPercent)
                    missileBlocked.Add(cell);
            }
        }
        missileBlocked.UnionWith(blocked);
        return new AuthoritativeLevel(
            LevelId,
            Width,
            Height,
            blocked,
            missileBlocked,
            IsDefaultStart,
            SuppressMultiplayerDeathDrops) {
            StableSymbol = StableSymbol,
        };
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352DU;
        value ^= value >> 15;
        value *= 0x846CA68BU;
        return value ^ (value >> 16);
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
                    : null) {
                StableSymbol = row.TryGet("level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
            }));
    }

}
