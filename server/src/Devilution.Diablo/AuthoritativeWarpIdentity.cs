using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable static-warp symbols without breaking numeric routes. */
internal static class AuthoritativeWarpIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "warp_id",
        string symbolColumn = "warp_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null)
            return numericId;

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("warp", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Warp row {row.LineNumber} references unknown stable warp symbol '{symbol}'.",
                exception);
        }
        if (numericId != resolvedId)
            throw new InvalidDataException(
                $"Warp row {row.LineNumber} maps warp {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "warp_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;
}
