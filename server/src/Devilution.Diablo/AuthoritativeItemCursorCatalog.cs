using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Content-owned mapping from legacy item cursor symbols to native graphic IDs. */
public sealed record AuthoritativeItemCursorDefinition(
    string Symbol,
    int NativeCursorGraphic);

/** Validated item cursor graphics shared by normalized and legacy item tables. */
public sealed class AuthoritativeItemCursorCatalog
{
    private readonly Dictionary<string, AuthoritativeItemCursorDefinition> definitionsBySymbol;
    private readonly Dictionary<int, AuthoritativeItemCursorDefinition> definitionsByGraphic;

    public AuthoritativeItemCursorCatalog(
        IEnumerable<AuthoritativeItemCursorDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        definitionsBySymbol = new Dictionary<string, AuthoritativeItemCursorDefinition>(
            StringComparer.OrdinalIgnoreCase);
        definitionsByGraphic = new Dictionary<int, AuthoritativeItemCursorDefinition>();
        foreach (var definition in definitions) {
            var symbol = NormalizeSymbol(definition.Symbol);
            if (!IsValidSymbol(symbol)
                || definition.NativeCursorGraphic is < 0 or > byte.MaxValue
                || !definitionsBySymbol.TryAdd(symbol, definition with { Symbol = symbol })
                || !definitionsByGraphic.TryAdd(definition.NativeCursorGraphic, definition with { Symbol = symbol })) {
                throw new InvalidDataException(
                    $"Item cursor graphic '{definition.Symbol}' is invalid or duplicated.");
            }
        }
        if (definitionsBySymbol.Count == 0)
            throw new InvalidDataException("The item cursor graphic catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeItemCursorDefinition> Definitions
        => definitionsBySymbol.Values
            .OrderBy(definition => definition.NativeCursorGraphic)
            .ToArray();

    public bool TryGet(
        string symbol,
        out AuthoritativeItemCursorDefinition definition)
    {
        return definitionsBySymbol.TryGetValue(NormalizeSymbol(symbol), out definition!);
    }

    public bool TryGetByNativeGraphic(
        int nativeCursorGraphic,
        out AuthoritativeItemCursorDefinition definition)
    {
        return definitionsByGraphic.TryGetValue(nativeCursorGraphic, out definition!);
    }

    public int Resolve(string symbol)
    {
        return TryGet(symbol, out var definition)
            ? definition.NativeCursorGraphic
            : throw new InvalidDataException(
                $"Item cursor graphic '{symbol}' is not present in the active catalog.");
    }

    public static AuthoritativeItemCursorCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row => new AuthoritativeItemCursorDefinition(
            row.Required("symbol"),
            row.RequiredInt32("native_cursor_graphic")));
        return new AuthoritativeItemCursorCatalog(definitions);
    }

    private static string NormalizeSymbol(string symbol)
    {
        return (symbol ?? string.Empty).Trim();
    }

    private static bool IsValidSymbol(string symbol)
    {
        return !string.IsNullOrWhiteSpace(symbol)
            && Encoding.UTF8.GetByteCount(symbol) < 64
            && !symbol.Contains('\0')
            && symbol.All(character => character is >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '_');
    }
}
