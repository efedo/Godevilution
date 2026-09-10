using Devilution.Data;

namespace Devilution.Diablo;

/** Native-compatible dungeon-generation rules supplied by validated content. */
public sealed record AuthoritativeLevelGenerationRule(
    int MinimumLevel,
    int MaximumLevel,
    int MonsterDensityDivisor,
    int MultiplayerMonsterMultiplierPercent,
    int TrapPercent)
{
    /** Native ordinary-spawn capacity; older rows retain the 190-slot limit. */
    public const int DefaultOrdinaryMonsterCapacity = 190;

    /** Content-owned ordinary encounter ceiling before runtime reservations. */
    public int OrdinaryMonsterCapacity { get; init; } = DefaultOrdinaryMonsterCapacity;

    public bool AppliesTo(uint levelId)
        => levelId <= int.MaxValue
            && levelId >= MinimumLevel
            && levelId <= MaximumLevel;
}

/** Validated level-range metadata for server-owned encounter generation. */
public sealed class AuthoritativeLevelGenerationCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "minimum_level",
        "maximum_level",
        "monster_density_divisor",
        "multiplayer_monster_multiplier_percent",
        "trap_percent",
        "ordinary_monster_capacity",
    ];

    private readonly IReadOnlyList<AuthoritativeLevelGenerationRule> definitions;

    public AuthoritativeLevelGenerationCatalog(
        IEnumerable<AuthoritativeLevelGenerationRule> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.OrderBy(definition => definition.MinimumLevel).ToArray();
        if (ordered.Length == 0)
            throw new InvalidDataException("The level-generation catalog cannot be empty.");
        for (var index = 0; index < ordered.Length; index++) {
            var definition = ordered[index];
            if (definition.MinimumLevel < 0
                || definition.MaximumLevel < definition.MinimumLevel
                || definition.MonsterDensityDivisor <= 0
                || definition.MultiplayerMonsterMultiplierPercent < 0
                || definition.TrapPercent is < 0 or > 100
                || definition.OrdinaryMonsterCapacity is < 1 or > 200
                || (index > 0
                    && definition.MinimumLevel <= ordered[index - 1].MaximumLevel)) {
                throw new InvalidDataException(
                    "Level-generation definitions contain invalid or overlapping ranges.");
            }
        }
        this.definitions = ordered;
    }

    public IReadOnlyList<AuthoritativeLevelGenerationRule> Definitions => definitions;

    /** Rejects omitted level-generation metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production level-generation table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    /** Ensures every declared world level has an applicable generation rule. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var level in world.Levels)
            _ = GetRequired(level.LevelId);
    }

    /** Calculates the native-compatible encounter count before runtime caps. */
    public int CalculateMonsterCount(
        uint levelId,
        int walkableCellCount,
        bool multiplayer)
    {
        if (walkableCellCount < 0)
            throw new ArgumentOutOfRangeException(nameof(walkableCellCount));

        var rule = GetRequired(levelId);
        var baseCount = walkableCellCount / rule.MonsterDensityDivisor;
        var scaledCount = multiplayer
            ? checked((long)baseCount * rule.MultiplayerMonsterMultiplierPercent / 100)
            : baseCount;
        return checked((int)scaledCount);
    }

    public bool TryGet(uint levelId, out AuthoritativeLevelGenerationRule definition)
    {
        definition = definitions.FirstOrDefault(candidate => candidate.AppliesTo(levelId))!;
        return definition is not null;
    }

    public AuthoritativeLevelGenerationRule GetRequired(uint levelId)
    {
        if (!TryGet(levelId, out var definition))
            throw new InvalidDataException(
                $"Level-generation metadata does not define level {levelId}.");
        return definition;
    }

    public static AuthoritativeLevelGenerationCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeLevelGenerationCatalog(table.Rows.Select(row =>
            new AuthoritativeLevelGenerationRule(
                row.RequiredInt32("minimum_level"),
                row.RequiredInt32("maximum_level"),
                row.RequiredInt32("monster_density_divisor"),
                row.RequiredInt32("multiplayer_monster_multiplier_percent"),
                row.RequiredInt32("trap_percent")) {
                OrdinaryMonsterCapacity = row.OptionalInt32(
                    "ordinary_monster_capacity",
                    AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity),
            }));
    }
}
