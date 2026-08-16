using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable store symbols without breaking numeric vendor IDs. */
internal static class AuthoritativeStoreIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "store_id",
        string symbolColumn = "store_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent: true);
    }

    public static uint ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn,
        uint defaultValue)
    {
        var numericPresent = row.TryGet(numericColumn, out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var numericId = row.OptionalUInt32(numericColumn, defaultValue);
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent);
    }

    private static uint ResolveValue(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        uint numericId,
        string symbolColumn,
        bool numericPresent)
    {
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (!numericPresent)
                throw new InvalidDataException(
                    $"Store row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("store", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Store row {row.LineNumber} references unknown stable store symbol '{symbol}'.",
                exception);
        }
        if (numericPresent && numericId != resolvedId)
            throw new InvalidDataException(
                $"Store row {row.LineNumber} maps store {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "store_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;
}
