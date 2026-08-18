using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable spell symbols without breaking numeric legacy rows. */
internal static class AuthoritativeSpellIdentity
{
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
                    $"Object row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Object row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Object row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn)
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            && !string.Equals(symbol, "NONE", StringComparison.OrdinalIgnoreCase)
            ? symbol
            : string.Empty;
}
