using Devilution.Data;

namespace Devilution.Diablo;

/** Native Hellfire item effect symbol and bit flag. */
public sealed record AuthoritativeNativeItemHellfireEffectDefinition(
    string Symbol,
    int NativeFlag);

/** Validated external Hellfire item effect flags used by normalized item data. */
public sealed class AuthoritativeNativeItemHellfireEffectCatalog
{
    public const int MaximumNativeFlag = 0x7F;
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "symbol",
        "native_flag",
    ];

    private readonly Dictionary<string, int> bySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeNativeItemHellfireEffectCatalog(
        IEnumerable<AuthoritativeNativeItemHellfireEffectDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.Symbol))
                throw new InvalidDataException(
                    "Native Hellfire item effect definitions require a symbol.");
            if (definition.NativeFlag is < 1 or > MaximumNativeFlag)
                throw new InvalidDataException(
                    $"Native Hellfire item effect '{definition.Symbol}' has a flag outside the native range.");
            var symbol = definition.Symbol.Trim();
            if (!bySymbol.TryAdd(symbol, definition.NativeFlag))
                throw new InvalidDataException(
                    $"Native Hellfire item effect symbol '{symbol}' is defined more than once.");
        }
    }

    public IReadOnlyList<AuthoritativeNativeItemHellfireEffectDefinition> Definitions
        => bySymbol
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new AuthoritativeNativeItemHellfireEffectDefinition(
                pair.Key,
                pair.Value))
            .ToArray();

    public bool TryGetFlag(string symbol, out int nativeFlag)
    {
        if (string.IsNullOrWhiteSpace(symbol)) {
            nativeFlag = 0;
            return false;
        }
        return bySymbol.TryGetValue(symbol.Trim(), out nativeFlag);
    }

    public int GetRequiredFlag(string symbol)
    {
        return TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : throw new KeyNotFoundException(
                $"Native Hellfire item effect symbol '{symbol}' is not defined by the active content pack.");
    }

    /** Rejects partial native Hellfire item-effect metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-Hellfire-item-effect table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("symbol");
            _ = row.RequiredInt32("native_flag");
        }
    }

    public static AuthoritativeNativeItemHellfireEffectCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeItemHellfireEffectDefinition(
                row.Required("symbol"),
                row.RequiredInt32("native_flag")));
        return new AuthoritativeNativeItemHellfireEffectCatalog(definitions);
    }
}
