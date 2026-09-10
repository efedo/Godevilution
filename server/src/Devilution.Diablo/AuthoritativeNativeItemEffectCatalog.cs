using Devilution.Data;

namespace Devilution.Diablo;

/** Native item special-effect symbol and bit flag. */
public sealed record AuthoritativeNativeItemEffectDefinition(
    string Symbol,
    uint NativeFlag);

/** Validated external item special-effect flags shared by legacy and normalized loaders. */
public sealed class AuthoritativeNativeItemEffectCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "symbol",
        "native_flag",
    ];

    private readonly Dictionary<string, uint> bySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeNativeItemEffectCatalog(
        IEnumerable<AuthoritativeNativeItemEffectDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.Symbol))
                throw new InvalidDataException(
                    "Native item effect definitions require a symbol.");
            if (definition.NativeFlag == 0)
                throw new InvalidDataException(
                    $"Native item effect '{definition.Symbol}' has an empty flag.");
            if (!bySymbol.TryAdd(definition.Symbol.Trim(), definition.NativeFlag))
                throw new InvalidDataException(
                    $"Native item effect symbol '{definition.Symbol.Trim()}' is defined more than once.");
        }
    }

    public IReadOnlyList<AuthoritativeNativeItemEffectDefinition> Definitions
        => bySymbol
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new AuthoritativeNativeItemEffectDefinition(pair.Key, pair.Value))
            .ToArray();

    public bool TryGetFlag(string symbol, out uint nativeFlag)
    {
        if (string.IsNullOrWhiteSpace(symbol)) {
            nativeFlag = 0;
            return false;
        }
        return bySymbol.TryGetValue(symbol.Trim(), out nativeFlag);
    }

    public uint GetRequiredFlag(string symbol)
    {
        return TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : throw new KeyNotFoundException(
                $"Native item effect symbol '{symbol}' is not defined by the active content pack.");
    }

    /** Rejects partial native item-effect metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-item-effect table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("symbol");
            _ = row.RequiredUInt32("native_flag");
        }
    }

    public static AuthoritativeNativeItemEffectCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeItemEffectDefinition(
                row.Required("symbol"),
                row.RequiredUInt32("native_flag")));
        return new AuthoritativeNativeItemEffectCatalog(definitions);
    }
}
