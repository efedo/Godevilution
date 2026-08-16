using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable world-object symbols without breaking numeric rows. */
internal static class AuthoritativeWorldObjectIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "object_id",
        string symbolColumn = "object_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null)
            return numericId;

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("object", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"World-object row {row.LineNumber} references unknown stable object symbol '{symbol}'.",
                exception);
        }
        if (numericId != resolvedId)
            throw new InvalidDataException(
                $"World-object row {row.LineNumber} maps object {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "object_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;
}
