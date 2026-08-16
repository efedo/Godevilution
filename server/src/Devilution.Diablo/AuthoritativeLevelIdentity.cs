using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable level symbols without breaking numeric legacy links. */
internal static class AuthoritativeLevelIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "level_id",
        string symbolColumn = "level_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent: true);
    }

    public static uint ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn)
    {
        var numericPresent = row.TryGet(numericColumn, out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var numericId = row.OptionalUInt32(numericColumn);
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent);
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "level_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;

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
                    $"Level row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("level", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Level row {row.LineNumber} references unknown stable level symbol '{symbol}'.",
                exception);
        }
        if (numericPresent && numericId != resolvedId)
            throw new InvalidDataException(
                $"Level row {row.LineNumber} maps level {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }
}
