using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable set-level symbols without breaking native zero. */
internal static class AuthoritativeSetLevelIdentity
{
    public static int Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "set_level_id",
        string symbolColumn = "symbol")
    {
        var numericId = row.RequiredInt32(numericColumn);
        if (numericId < 0)
            throw new InvalidDataException(
                $"Set-level row {row.LineNumber} contains a negative set-level ID.");
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent: true);
    }

    public static int ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn)
    {
        var numericPresent = row.TryGet(numericColumn, out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var numericId = row.OptionalInt32(numericColumn);
        if (numericId < 0)
            throw new InvalidDataException(
                $"Set-level row {row.LineNumber} contains a negative set-level ID.");
        return ResolveValue(row, stableIds, numericId, symbolColumn, numericPresent);
    }

    private static int ResolveValue(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        int numericId,
        string symbolColumn,
        bool numericPresent)
    {
        if (!row.TryGet(symbolColumn, out var symbol)
            || string.IsNullOrWhiteSpace(symbol)) {
            return numericId;
        }
        if (stableIds is null) {
            if (!numericPresent)
                throw new InvalidDataException(
                    $"Set-level row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("set_level", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Set-level row {row.LineNumber} references unknown stable set-level symbol '{symbol}'.",
                exception);
        }
        if (resolvedId > int.MaxValue)
            throw new InvalidDataException(
                $"Set-level row {row.LineNumber} resolves '{symbol}' outside the native integer range.");
        if (numericPresent && numericId != (int)resolvedId)
            throw new InvalidDataException(
                $"Set-level row {row.LineNumber} maps set level {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return (int)resolvedId;
    }
}
