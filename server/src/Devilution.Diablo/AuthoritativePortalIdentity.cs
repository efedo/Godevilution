using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable portal symbols without breaking numeric routes. */
internal static class AuthoritativePortalIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "portal_id",
        string symbolColumn = "portal_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null)
            return numericId;

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("portal", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Portal row {row.LineNumber} references unknown stable portal symbol '{symbol}'.",
                exception);
        }
        if (numericId != resolvedId)
            throw new InvalidDataException(
                $"Portal row {row.LineNumber} maps portal {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "portal_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;
}
