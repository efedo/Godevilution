using Devilution.Data;

namespace Devilution.Diablo;

public enum AuthoritativeNativeItemTopologyKind
{
    ItemType,
    ItemClass,
    EquipLocation,
}

/** Native legacy item topology symbol and ordinal. */
public sealed record AuthoritativeNativeItemTopologyDefinition(
    AuthoritativeNativeItemTopologyKind Kind,
    string Symbol,
    int NativeId);

/** Validated external item type, class, and equipment-location namespace. */
public sealed class AuthoritativeNativeItemTopologyCatalog
{
    public const int MaximumNativeId = 127;

    private static readonly IReadOnlyList<string> ProductionColumns = [
        "kind",
        "symbol",
        "native_id",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-item-topology compatibility table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("kind");
            _ = row.Required("symbol");
            _ = row.RequiredInt32("native_id");
        }
    }

    private readonly Dictionary<
        (AuthoritativeNativeItemTopologyKind Kind, string Symbol),
        int> bySymbol;
    private readonly IReadOnlyList<AuthoritativeNativeItemTopologyDefinition> definitions;

    public AuthoritativeNativeItemTopologyCatalog(
        IEnumerable<AuthoritativeNativeItemTopologyDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        bySymbol = new Dictionary<
            (AuthoritativeNativeItemTopologyKind Kind, string Symbol),
            int>();
        var normalizedDefinitions = new List<AuthoritativeNativeItemTopologyDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (!Enum.IsDefined(definition.Kind))
                throw new InvalidDataException(
                    $"Native item topology kind '{definition.Kind}' is invalid.");
            if (definition.NativeId is < 0 or > MaximumNativeId)
                throw new InvalidDataException(
                    $"Native item topology ID {definition.NativeId} is outside the compatibility range.");
            if (string.IsNullOrWhiteSpace(definition.Symbol))
                throw new InvalidDataException(
                    $"Native item topology {definition.Kind} has an empty symbol.");
            var normalizedSymbol = definition.Symbol.Trim();
            var key = (definition.Kind, NormalizeSymbol(normalizedSymbol));
            if (!bySymbol.TryAdd(key, definition.NativeId))
                throw new InvalidDataException(
                    $"Native item topology symbol '{normalizedSymbol}' is defined more than once for {definition.Kind}.");
            normalizedDefinitions.Add(definition with { Symbol = normalizedSymbol });
        }
        this.definitions = normalizedDefinitions
            .OrderBy(definition => definition.Kind)
            .ThenBy(definition => definition.NativeId)
            .ThenBy(definition => definition.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeNativeItemTopologyDefinition> Definitions
        => definitions;

    public bool TryGetId(
        AuthoritativeNativeItemTopologyKind kind,
        string symbol,
        out int nativeId)
    {
        if (string.IsNullOrWhiteSpace(symbol)) {
            nativeId = 0;
            return false;
        }
        return bySymbol.TryGetValue((kind, NormalizeSymbol(symbol)), out nativeId);
    }

    public int GetRequiredId(
        AuthoritativeNativeItemTopologyKind kind,
        string symbol)
    {
        return TryGetId(kind, symbol, out var nativeId)
            ? nativeId
            : throw new KeyNotFoundException(
                $"Native item topology symbol '{symbol}' is not defined for {kind}.");
    }

    public static AuthoritativeNativeItemTopologyCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeItemTopologyDefinition(
                ParseKind(row.Required("kind"), row.LineNumber),
                row.Required("symbol"),
                row.RequiredInt32("native_id")));
        return new AuthoritativeNativeItemTopologyCatalog(definitions);
    }

    private static AuthoritativeNativeItemTopologyKind ParseKind(
        string value,
        int lineNumber)
    {
        if (Enum.TryParse<AuthoritativeNativeItemTopologyKind>(
                value,
                ignoreCase: true,
                out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"TSV row {lineNumber} contains unknown native item topology kind '{value}'.");
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }
}
