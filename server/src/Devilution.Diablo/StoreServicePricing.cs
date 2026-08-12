using Devilution.Data;

namespace Devilution.Diablo;

/** Externalized constants used by authoritative store services. */
public sealed class StoreServicePricing
{
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

    /** Loads one service row per key and optional spell staff-cost rows. */
    public static StoreServicePricing LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var spellCosts = new Dictionary<int, uint>();
        foreach (var row in table.Rows) {
            var key = row.Required("key");
            if (key.Equals("spell_staff_cost", StringComparison.Ordinal)) {
                var spellId = row.RequiredInt32("spell_id");
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
}
