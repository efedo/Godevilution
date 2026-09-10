using Devilution.Data;

namespace Devilution.Diablo;

/** Native miscellaneous-item symbol and ordinal retained for legacy item data. */
public sealed record AuthoritativeNativeMiscDefinition(
    int NativeMiscId,
    string Symbol);

/** Validated external native miscellaneous-item mapping for legacy loaders. */
public sealed class AuthoritativeNativeMiscCatalog
{
    public const int MaximumNativeMiscId = 127;

    private static readonly IReadOnlyList<string> ProductionColumns = [
        "native_misc_id",
        "native_misc_symbol",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-misc compatibility table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredInt32("native_misc_id");
            _ = row.Required("native_misc_symbol");
        }
    }

    private readonly Dictionary<int, AuthoritativeNativeMiscDefinition> byId;
    private readonly Dictionary<string, int> bySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeNativeMiscCatalog(
        IEnumerable<AuthoritativeNativeMiscDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        byId = new Dictionary<int, AuthoritativeNativeMiscDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (definition.NativeMiscId is < 0 or > MaximumNativeMiscId)
                throw new InvalidDataException(
                    $"Native miscellaneous ID {definition.NativeMiscId} is outside the compatibility range.");
            if (string.IsNullOrWhiteSpace(definition.Symbol))
                throw new InvalidDataException(
                    $"Native miscellaneous ID {definition.NativeMiscId} has an empty symbol.");
            var normalized = definition.Symbol.Trim();
            var normalizedDefinition = definition with { Symbol = normalized };
            if (!byId.TryAdd(definition.NativeMiscId, normalizedDefinition))
                throw new InvalidDataException(
                    $"Native miscellaneous ID {definition.NativeMiscId} is defined more than once.");
            if (!bySymbol.TryAdd(normalized, definition.NativeMiscId))
                throw new InvalidDataException(
                    $"Native miscellaneous symbol '{normalized}' is defined more than once.");
        }
    }

    public IReadOnlyList<AuthoritativeNativeMiscDefinition> Definitions
        => byId.Values.OrderBy(definition => definition.NativeMiscId).ToArray();

    public bool TryGetId(string symbol, out int nativeMiscId)
    {
        if (string.IsNullOrWhiteSpace(symbol)) {
            nativeMiscId = 0;
            return false;
        }
        return bySymbol.TryGetValue(symbol.Trim(), out nativeMiscId);
    }

    public int GetRequiredId(string symbol)
    {
        return TryGetId(symbol, out var nativeMiscId)
            ? nativeMiscId
            : throw new KeyNotFoundException(
                $"Native miscellaneous symbol '{symbol}' is not defined by the active content pack.");
    }

    public static AuthoritativeNativeMiscCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeMiscDefinition(
                row.RequiredInt32("native_misc_id"),
                row.Required("native_misc_symbol")));
        return new AuthoritativeNativeMiscCatalog(definitions);
    }
}
