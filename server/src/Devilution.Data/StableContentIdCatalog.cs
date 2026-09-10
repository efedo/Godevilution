namespace Devilution.Data;

/** A validated, explicit symbolic-to-numeric ID catalog for protocol content. */
public sealed class StableContentIdCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "kind",
        "symbol",
        "numeric_id",
    ];

    private readonly Dictionary<(string Kind, string Symbol), uint> bySymbol;
    private readonly Dictionary<(string Kind, uint Id), string> byId;

    public StableContentIdCatalog(IEnumerable<StableContentId> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var values = entries.ToArray();
        if (values.Length == 0)
            throw new ArgumentException("A stable ID catalog must contain at least one entry.", nameof(entries));

        bySymbol = new Dictionary<(string Kind, string Symbol), uint>();
        byId = new Dictionary<(string Kind, uint Id), string>();
        foreach (var entry in values) {
            if (string.IsNullOrWhiteSpace(entry.Kind)
                || string.IsNullOrWhiteSpace(entry.Symbol)
                || (entry.NumericId == 0
                    && !string.Equals(entry.Kind, "class", StringComparison.Ordinal)
                    && !string.Equals(entry.Kind, "level", StringComparison.Ordinal)
                    && !string.Equals(entry.Kind, "set_level", StringComparison.Ordinal)))
                throw new InvalidDataException(
                    "Stable content IDs require non-empty kind/symbol values and a non-zero numeric ID, "
                    + "except for native class, level, and set-level namespaces where ID zero is valid.");
            if (!bySymbol.TryAdd((entry.Kind, entry.Symbol), entry.NumericId))
                throw new InvalidDataException($"Stable content symbol '{entry.Kind}:{entry.Symbol}' is duplicated.");
            if (!byId.TryAdd((entry.Kind, entry.NumericId), entry.Symbol))
                throw new InvalidDataException($"Stable content ID '{entry.Kind}:{entry.NumericId}' is duplicated.");
        }

        Entries = values
            .OrderBy(entry => entry.Kind, StringComparer.Ordinal)
            .ThenBy(entry => entry.NumericId)
            .ToArray();
    }

    public IReadOnlyList<StableContentId> Entries { get; }

    public uint Resolve(string kind, string symbol)
    {
        if (!TryResolve(kind, symbol, out var numericId))
            throw new KeyNotFoundException($"Stable content symbol '{kind}:{symbol}' is not registered.");
        return numericId;
    }

    public bool TryResolve(string kind, string symbol, out uint numericId)
    {
        return bySymbol.TryGetValue((kind, symbol), out numericId);
    }

    public string Resolve(uint numericId, string kind)
    {
        if (!byId.TryGetValue((kind, numericId), out var symbol))
            throw new KeyNotFoundException($"Stable content ID '{kind}:{numericId}' is not registered.");
        return symbol;
    }

    /** Validates one stable-ID namespace against its active runtime catalog. */
    public void ValidateReferences(string kind, Func<uint, bool> containsId)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("A stable-ID kind is required.", nameof(kind));
        ArgumentNullException.ThrowIfNull(containsId);
        foreach (var entry in Entries.Where(entry =>
                     string.Equals(entry.Kind, kind, StringComparison.Ordinal))) {
            if (!containsId(entry.NumericId))
                throw new InvalidDataException(
                    $"Stable content {kind} symbol '{entry.Symbol}' references unknown ID {entry.NumericId}.");
        }
    }

    /** Rejects partial stable identity metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production stable-ID table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("kind");
            _ = row.Required("symbol");
            _ = row.RequiredUInt32("numeric_id");
        }
    }

    public static StableContentIdCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new StableContentIdCatalog(table.Rows.Select(row => new StableContentId(
            row.Required("kind"),
            row.Required("symbol"),
            row.RequiredUInt32("numeric_id"))));
    }
}

public sealed record StableContentId(string Kind, string Symbol, uint NumericId);
