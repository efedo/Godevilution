using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable monster symbols without breaking numeric legacy rows. */
internal static class AuthoritativeMonsterIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "monster_id",
        string symbolColumn = "monster_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        return ResolveValue(row, stableIds, numericId, symbolColumn);
    }

    public static uint ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn)
    {
        var numericId = row.OptionalUInt32(numericColumn);
        return ResolveValue(row, stableIds, numericId, symbolColumn);
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "monster_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;

    private static uint ResolveValue(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        uint numericId,
        string symbolColumn)
    {
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Monster row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("monster", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Monster row {row.LineNumber} references unknown stable monster symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Monster row {row.LineNumber} maps monster {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }
}
