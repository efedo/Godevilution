using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Native set-level identity and presentation name supplied by content. */
public sealed record AuthoritativeSetLevelName(
    int SetLevelId,
    string Symbol,
    string DisplayName);

/** Validated native set-level names used by quest routing and presentation. */
public sealed class AuthoritativeSetLevelNameCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "set_level_id",
        "symbol",
        "display_name",
    ];

    private readonly Dictionary<int, AuthoritativeSetLevelName> definitions;

    public AuthoritativeSetLevelNameCatalog(
        IEnumerable<AuthoritativeSetLevelName> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<int, AuthoritativeSetLevelName>();
        foreach (var definition in definitions) {
            if (definition.SetLevelId is < 0 or > 8
                || string.IsNullOrWhiteSpace(definition.Symbol)
                || !this.definitions.TryAdd(definition.SetLevelId, definition)) {
                throw new InvalidDataException(
                    "Set-level definitions contain invalid or duplicate identities.");
            }
        }
        if (this.definitions.Count == 0
            || Enumerable.Range(0, 9).Any(setLevel => !this.definitions.ContainsKey(setLevel))) {
            throw new InvalidDataException(
                "The set-level catalog must define every native set-level identity.");
        }
    }

    public IReadOnlyList<AuthoritativeSetLevelName> Definitions
        => definitions.Values.OrderBy(definition => definition.SetLevelId).ToArray();

    public bool TryGet(int setLevelId, out AuthoritativeSetLevelName definition)
        => definitions.TryGetValue(setLevelId, out definition!);

    public string GetDisplayName(int setLevelId)
        => TryGet(setLevelId, out var definition)
            ? definition.DisplayName
            : throw new InvalidDataException($"Unknown native set-level identity {setLevelId}.");

    /** Rejects partial set-level identity metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production set-level name table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("set_level_id");
            _ = row.Required("symbol");
        }
    }

    public static AuthoritativeSetLevelNameCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSetLevelNameCatalog(table.Rows.Select(row =>
            new AuthoritativeSetLevelName(
                AuthoritativeSetLevelIdentity.Resolve(row, stableIds),
                row.Required("symbol"),
                OptionalText(row, "display_name"))));
    }

    private static string OptionalText(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }
        return value;
    }
}

/** One native single-player quest-randomization pool entry. */
public sealed record AuthoritativeQuestPoolEntry(
    int PoolId,
    uint QuestId);

/** Selects which validated row order seeds single-player quest selection. */
public enum AuthoritativeQuestPoolSelectionOrder
{
    LegacyQuestIdSortedV1 = 0,
    NativeTsvRowsV1 = 1,
}

/** Validated quest pools that preserve native stable quest identities. */
public sealed class AuthoritativeQuestPoolCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "pool_id",
        "quest_id",
        "quest_symbol",
    ];

    private readonly IReadOnlyList<AuthoritativeQuestPoolEntry> definitions;
    private readonly IReadOnlyList<AuthoritativeQuestPoolEntry> selectionDefinitions;

    public AuthoritativeQuestPoolCatalog(
        IEnumerable<AuthoritativeQuestPoolEntry> definitions,
        AuthoritativeQuestTypeCatalog? questTypes = null)
        : this(definitions, questTypes, AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1)
    {
    }

    public AuthoritativeQuestPoolCatalog(
        IEnumerable<AuthoritativeQuestPoolEntry> definitions,
        AuthoritativeQuestTypeCatalog? questTypes,
        AuthoritativeQuestPoolSelectionOrder selectionOrder)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (!Enum.IsDefined(typeof(AuthoritativeQuestPoolSelectionOrder), selectionOrder))
            throw new ArgumentOutOfRangeException(nameof(selectionOrder));

        var entries = definitions.ToArray();
        if (entries.Length == 0
            || entries.Any(entry => entry.PoolId <= 0 || entry.QuestId == 0)
            || entries.GroupBy(entry => (entry.PoolId, entry.QuestId)).Any(group => group.Count() != 1)
            || entries.GroupBy(entry => entry.PoolId).Any(group => group.Any(entry =>
                questTypes is not null && !questTypes.TryGet(entry.QuestId, out _)))) {
            throw new InvalidDataException(
                "Quest pool definitions contain invalid, duplicate, or unknown identities.");
        }
        this.definitions = entries
            .OrderBy(entry => entry.PoolId)
            .ThenBy(entry => entry.QuestId)
            .ToArray();
        this.selectionDefinitions = selectionOrder == AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1
            ? entries
            : this.definitions;
    }

    public IReadOnlyList<AuthoritativeQuestPoolEntry> Definitions => definitions;

    public IReadOnlyList<AuthoritativeQuestPoolEntry> GetPool(int poolId)
        => definitions.Where(entry => entry.PoolId == poolId).ToArray();

    /** Selects the native single-player quest exclusions for one level-15 seed. */
    public IReadOnlyList<uint> SelectUnavailableQuestIds(uint seed)
    {
        var random = new DeterministicLegacyRandom(seed);
        var unavailable = new List<uint>();
        foreach (var pool in selectionDefinitions.GroupBy(entry => entry.PoolId).OrderBy(group => group.Key)) {
            // Vanilla compatibility: QuestGroup1[-2] skips pool 1 after one
            // discarded RNG draw for this historical seed.
            if (seed == 988045466U && pool.Key == 1) {
                random.Discard(1);
                continue;
            }
            var entries = pool.ToArray();
            unavailable.Add(entries[random.RandomIntLessThan(entries.Length)].QuestId);
        }
        return unavailable;
    }

    /** Rejects partial quest-pool metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest-pool table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("pool_id");
            _ = row.Required("quest_id");
        }
    }

    public static AuthoritativeQuestPoolCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        StableContentIdCatalog? stableIds = null)
        => LoadTsv(
            sourcePath,
            contents,
            questTypes,
            stableIds,
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1);

    public static AuthoritativeQuestPoolCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeQuestTypeCatalog? questTypes,
        StableContentIdCatalog? stableIds,
        AuthoritativeQuestPoolSelectionOrder selectionOrder)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var entries = table.Rows.Select(row =>
            new AuthoritativeQuestPoolEntry(
                row.RequiredInt32("pool_id"),
                AuthoritativeQuestIdentity.Resolve(row, stableIds)));
        return new AuthoritativeQuestPoolCatalog(entries, questTypes, selectionOrder);
    }
}
