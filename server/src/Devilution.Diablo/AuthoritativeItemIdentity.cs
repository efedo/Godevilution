using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable item symbols without breaking numeric legacy rows. */
internal static class AuthoritativeItemIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "item_id",
        string symbolColumn = "item_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Item row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("item", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Item row {row.LineNumber} references unknown stable item symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Item row {row.LineNumber} maps item {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "item_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            && !string.Equals(symbol, "NONE", StringComparison.OrdinalIgnoreCase)
            ? symbol
            : string.Empty;

    public static uint ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn)
    {
        var numericId = row.OptionalUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Item row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("item", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Item row {row.LineNumber} references unknown stable item symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Item row {row.LineNumber} maps item {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }
}
