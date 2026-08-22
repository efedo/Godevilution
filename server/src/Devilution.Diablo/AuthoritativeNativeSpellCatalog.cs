using Devilution.Data;

namespace Devilution.Diablo;

/** Native spell names and ordinals retained for legacy item-table compatibility. */
public sealed record AuthoritativeNativeSpellDefinition(
    uint NativeSpellId,
    string Symbol);

/** Validated external native spell namespace used by legacy item data. */
public sealed class AuthoritativeNativeSpellCatalog
{
    public const uint MaximumNativeSpellId = 51;

    private readonly Dictionary<uint, AuthoritativeNativeSpellDefinition> byId;
    private readonly Dictionary<string, uint> bySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeNativeSpellCatalog(
        IEnumerable<AuthoritativeNativeSpellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        byId = new Dictionary<uint, AuthoritativeNativeSpellDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (definition.NativeSpellId > MaximumNativeSpellId)
                throw new InvalidDataException(
                    $"Native spell ID {definition.NativeSpellId} exceeds the compatibility range.");
            if (string.IsNullOrWhiteSpace(definition.Symbol))
                throw new InvalidDataException(
                    $"Native spell ID {definition.NativeSpellId} has an empty symbol.");
            var normalized = definition.Symbol.Trim();
            var normalizedDefinition = definition with { Symbol = normalized };
            if (!byId.TryAdd(definition.NativeSpellId, normalizedDefinition))
                throw new InvalidDataException(
                    $"Native spell ID {definition.NativeSpellId} is defined more than once.");
            if (!bySymbol.TryAdd(normalized, definition.NativeSpellId))
                throw new InvalidDataException(
                    $"Native spell symbol '{normalized}' is defined more than once.");
        }
    }

    public IReadOnlyList<AuthoritativeNativeSpellDefinition> Definitions
        => byId.Values.OrderBy(definition => definition.NativeSpellId).ToArray();

    public bool TryGetId(string symbol, out uint nativeSpellId)
    {
        if (string.IsNullOrWhiteSpace(symbol)) {
            nativeSpellId = 0;
            return false;
        }
        return bySymbol.TryGetValue(symbol.Trim(), out nativeSpellId);
    }

    public uint GetRequiredId(string symbol)
    {
        return TryGetId(symbol, out var nativeSpellId)
            ? nativeSpellId
            : throw new KeyNotFoundException(
                $"Native spell symbol '{symbol}' is not defined by the active content pack.");
    }

    public static AuthoritativeNativeSpellCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeSpellDefinition(
                row.RequiredUInt32("native_spell_id"),
                row.Required("native_spell_symbol")));
        return new AuthoritativeNativeSpellCatalog(definitions);
    }
}
