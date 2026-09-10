using Devilution.Data;

namespace Devilution.Diablo;

/** One deterministic item combination declared by the active content pack. */
public sealed record AuthoritativeItemCombinationDefinition(
    uint ResultItemId,
    IReadOnlyList<uint> RequiredItemIds);

/** Validated content-owned item-combination rules used by server inventory actions. */
public sealed class AuthoritativeItemCombinationCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "result_item_id",
        "result_item_symbol",
        "required_item_ids",
        "required_item_symbols",
    ];

    private readonly IReadOnlyList<AuthoritativeItemCombinationDefinition> definitions;

    public AuthoritativeItemCombinationCatalog(
        IEnumerable<AuthoritativeItemCombinationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.ToArray();
        if (ordered.Any(definition => definition.ResultItemId == 0
                || definition.RequiredItemIds is null
                || definition.RequiredItemIds.Count < 2
                || definition.RequiredItemIds.Any(itemId => itemId == 0)
                || definition.RequiredItemIds.Distinct().Count() != definition.RequiredItemIds.Count
                || definition.RequiredItemIds.Contains(definition.ResultItemId))) {
            throw new InvalidDataException("Item combinations contain invalid identities.");
        }
        if (ordered.GroupBy(definition => definition.ResultItemId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Item combinations define a result more than once.");
        this.definitions = ordered
            .OrderBy(definition => definition.ResultItemId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeItemCombinationDefinition> Definitions => definitions;

    /** Rejects numeric-only recipe identities at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production item-combination table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    /** Validates every combination identity against the active normalized item catalog. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in definitions) {
            if (!items.TryGet(definition.ResultItemId, out _))
                throw new InvalidDataException(
                    $"Item combination result {definition.ResultItemId} is not present in the authoritative catalog.");
            foreach (var itemId in definition.RequiredItemIds) {
                if (!items.TryGet(itemId, out _))
                    throw new InvalidDataException(
                        $"Item combination input {itemId} is not present in the authoritative catalog.");
            }
        }
    }

    public static AuthoritativeItemCombinationCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeItemCombinationCatalog(table.Rows.Select(row =>
            new AuthoritativeItemCombinationDefinition(
                AuthoritativeItemIdentity.Resolve(
                    row,
                    stableIds,
                    "result_item_id",
                    "result_item_symbol"),
                ParseRequiredItemIds(row, sourcePath, stableIds))));
    }

    private static IReadOnlyList<uint> ParseRequiredItemIds(
        TsvRow row,
        string sourcePath,
        StableContentIdCatalog? stableIds)
    {
        var value = row.Required("required_item_ids");
        var values = value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => uint.TryParse(token, out var itemId) && itemId != 0
                ? itemId
                : throw new InvalidDataException(
                    $"Item combination table '{sourcePath}' has an invalid item ID '{token}' on line {row.LineNumber}."))
            .ToArray();
        if (values.Length < 2)
            throw new InvalidDataException(
                $"Item combination table '{sourcePath}' requires at least two input items on line {row.LineNumber}.");
        if (!row.TryGet("required_item_symbols", out var symbols)
            || string.IsNullOrWhiteSpace(symbols))
            return values;
        var symbolValues = symbols.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (symbolValues.Length != values.Length)
            throw new InvalidDataException(
                $"Item combination table '{sourcePath}' has mismatched numeric and symbolic input counts on line {row.LineNumber}.");
        for (var index = 0; index < values.Length; index++) {
            uint resolved;
            if (stableIds is null) {
                resolved = values[index];
            } else {
                try {
                    resolved = stableIds.Resolve("item", symbolValues[index]);
                } catch (KeyNotFoundException exception) {
                    throw new InvalidDataException(
                        $"Item combination table '{sourcePath}' references unknown stable item symbol '{symbolValues[index]}' on line {row.LineNumber}.",
                        exception);
                }
            }
            if (values[index] != resolved)
                throw new InvalidDataException(
                    $"Item combination table '{sourcePath}' maps item {values[index]} to symbol '{symbolValues[index]}', which resolves to {resolved} on line {row.LineNumber}.");
        }
        return values;
    }
}
