using Devilution.Data;

namespace Devilution.Diablo;

/** Externalized constants used by authoritative store services. */
public sealed class StoreServicePricing
{
    private static readonly string[] RequiredProductionKeys = [
        "sale_divisor",
        "normal_repair_divisor",
        "magical_repair_percent",
        "magical_repair_divisor",
        "identification_price",
        "recharge_divisor",
        "mana_chunk",
    ];

    private readonly Dictionary<int, uint> spellStaffCosts;

    public StoreServicePricing(
        int saleDivisor = 4,
        int normalRepairDivisor = 2,
        int magicalRepairPercent = 30,
        int magicalRepairDivisor = 200,
        uint identificationPrice = 100,
        int rechargeDivisor = 2,
        int manaChunk = 64,
        IReadOnlyDictionary<int, uint>? spellStaffCosts = null)
    {
        if (saleDivisor <= 0 || normalRepairDivisor <= 0 || magicalRepairPercent < 0 || magicalRepairDivisor <= 0 || rechargeDivisor <= 0 || manaChunk <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleDivisor), "Store service divisors and mana chunk must be positive.");

        SaleDivisor = saleDivisor;
        NormalRepairDivisor = normalRepairDivisor;
        MagicalRepairPercent = magicalRepairPercent;
        MagicalRepairDivisor = magicalRepairDivisor;
        IdentificationPrice = identificationPrice;
        RechargeDivisor = rechargeDivisor;
        ManaChunk = manaChunk;
        this.spellStaffCosts = spellStaffCosts?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
    }

    public int SaleDivisor { get; }

    public int NormalRepairDivisor { get; }

    public int MagicalRepairPercent { get; }

    public int MagicalRepairDivisor { get; }

    public uint IdentificationPrice { get; }

    public int RechargeDivisor { get; }

    public int ManaChunk { get; }

    public static StoreServicePricing Default { get; } = new();

    public uint GetSpellStaffCost(int spellId)
    {
        return spellStaffCosts.TryGetValue(spellId, out var cost) ? cost : 0;
    }

    /** Validates non-zero spell-staff pricing identities against active spells. */
    public void ValidateSpellReferences(AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        foreach (var spellId in spellStaffCosts.Keys.Where(spellId => spellId != 0)) {
            if (spellId < 0 || !spells.TryGet(checked((uint)spellId), out _))
                throw new InvalidDataException(
                    $"Store service pricing references unknown spell {spellId}.");
        }
    }

    /**
     * Rejects omitted service pricing at the current production content
     * boundary. Direct callers may continue to load partial compatibility rows.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.Contains("key", StringComparer.Ordinal)
            || !table.Columns.Contains("value", StringComparer.Ordinal)) {
            throw new InvalidDataException(
                $"Production store pricing table '{table.SourcePath}' must declare key and value columns.");
        }

        var rowsByKey = table.Rows.GroupBy(
            row => row.Required("key"),
            StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var missingKeys = RequiredProductionKeys
            .Where(key => !rowsByKey.ContainsKey(key))
            .ToArray();
        if (missingKeys.Length != 0)
            throw new InvalidDataException(
                $"Production store pricing table '{table.SourcePath}' is missing required keys: "
                + string.Join(", ", missingKeys));

        if (!rowsByKey.ContainsKey("spell_staff_cost"))
            throw new InvalidDataException(
                $"Production store pricing table '{table.SourcePath}' requires an explicit spell_staff_cost row.");

        foreach (var row in table.Rows) {
            _ = row.Required("value");
            if (row.Required("key").Equals("spell_staff_cost", StringComparison.Ordinal))
                _ = row.Required("spell_id");
        }
    }

    /** Loads one service row per key and optional spell staff-cost rows. */
    public static StoreServicePricing LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var spellCosts = new Dictionary<int, uint>();
        foreach (var row in table.Rows) {
            var key = row.Required("key");
            if (key.Equals("spell_staff_cost", StringComparison.Ordinal)) {
                var spellId = ResolveSpellId(row, stableIds);
                if (spellId < 0)
                    throw new InvalidDataException(
                        $"Store pricing table '{sourcePath}' contains a negative spell {spellId}.");
                var cost = row.RequiredUInt32("value");
                if (!spellCosts.TryAdd(spellId, cost))
                    throw new InvalidDataException($"Store pricing table '{sourcePath}' contains duplicate spell {spellId}.");
                continue;
            }

            var value = row.RequiredInt32("value");
            if (!values.TryAdd(key, value))
                throw new InvalidDataException($"Store pricing table '{sourcePath}' contains duplicate key '{key}'.");
        }

        int Required(string key, int fallback)
        {
            return values.TryGetValue(key, out var value) ? value : fallback;
        }

        return new StoreServicePricing(
            Required("sale_divisor", Default.SaleDivisor),
            Required("normal_repair_divisor", Default.NormalRepairDivisor),
            Required("magical_repair_percent", Default.MagicalRepairPercent),
            Required("magical_repair_divisor", Default.MagicalRepairDivisor),
            checked((uint)Required("identification_price", (int)Default.IdentificationPrice)),
            Required("recharge_divisor", Default.RechargeDivisor),
            Required("mana_chunk", Default.ManaChunk),
            spellCosts);
    }

    private static int ResolveSpellId(TsvRow row, StableContentIdCatalog? stableIds)
    {
        var numericId = row.RequiredInt32("spell_id");
        if (!row.TryGet("spell_symbol", out var symbol)
            || string.IsNullOrWhiteSpace(symbol))
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Store pricing row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Store pricing row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (resolvedId > int.MaxValue)
            throw new InvalidDataException(
                $"Store pricing row {row.LineNumber} resolves spell symbol '{symbol}' "
                + $"to unsupported ID {resolvedId}.");
        var resolvedInt = checked((int)resolvedId);
        if (numericId != 0 && numericId != resolvedInt)
            throw new InvalidDataException(
                $"Store pricing row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedInt}.");
        return resolvedInt;
    }
}
