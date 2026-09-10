using Devilution.Data;

namespace Devilution.Diablo;

/** External item quality and affix allocation probabilities. */
public sealed record AuthoritativeItemGenerationRules(
    int MagicChanceBase,
    int MagicChancePerLevel,
    int UniqueChanceNormal,
    int UniqueChanceUnique,
    int BonusLevelsUnique,
    int PrefixPercent,
    int SuffixPercent,
    int OnlyGoodChance,
    int NoDropPercent,
    int GoldPercent)
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "magicChanceBase",
        "magicChancePerLevel",
        "alwaysMagicMisc",
        "uniqueChanceNormal",
        "uniqueChanceUnique",
        "bonusLevelsUnique",
        "prefixPercent",
        "suffixPercent",
        "onlygoodChance",
        "noDropPercent",
        "goldPercent",
    ];

    /** Rejects partial item-generation metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production item-generation table '{table.SourcePath}' must use the complete ordered schema.");
        var row = table.Rows.Count == 1
            ? table.Rows[0]
            : throw new InvalidDataException(
                $"Production item-generation table '{table.SourcePath}' must contain exactly one row.");
        foreach (var column in ProductionColumns)
            _ = row.Required(column);
    }

    public static AuthoritativeItemGenerationRules Default { get; } = new(10, 1, 1, 15, 4, 25, 66, 66, 59, 74);

    /** Legacy misc categories that bypass the normal magic-quality roll. */
    public IReadOnlySet<string> AlwaysMagicMisc { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static AuthoritativeItemGenerationRules LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        var result = new AuthoritativeItemGenerationRules(
            row.RequiredInt32("magicChanceBase"),
            row.RequiredInt32("magicChancePerLevel"),
            row.RequiredInt32("uniqueChanceNormal"),
            row.RequiredInt32("uniqueChanceUnique"),
            row.RequiredInt32("bonusLevelsUnique"),
            row.RequiredInt32("prefixPercent"),
            row.RequiredInt32("suffixPercent"),
            row.RequiredInt32("onlygoodChance"),
            row.RequiredInt32("noDropPercent"),
            row.RequiredInt32("goldPercent"));
        if (row.TryGet("alwaysMagicMisc", out var alwaysMagic) && !string.IsNullOrWhiteSpace(alwaysMagic))
            result = result with {
                AlwaysMagicMisc = alwaysMagic.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
            };
        if (result.MagicChanceBase < 0 || result.MagicChancePerLevel < 0
            || result.UniqueChanceNormal < 0 || result.UniqueChanceUnique < 0 || result.BonusLevelsUnique < 0
            || result.PrefixPercent is < 0 or > 100 || result.SuffixPercent is < 0 or > 100
            || result.OnlyGoodChance is < 0 or > 100 || result.NoDropPercent is < 0 or > 100 || result.GoldPercent is < 0 or > 100)
            throw new InvalidDataException($"Item generation table '{sourcePath}' contains invalid probabilities.");
        return result;
    }
}
