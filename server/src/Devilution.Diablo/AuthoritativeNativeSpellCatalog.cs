using Devilution.Data;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Native spell names and ordinals retained for legacy item-table compatibility. */
public sealed record AuthoritativeNativeSpellDefinition(
    uint NativeSpellId,
    string Symbol);

/** Validated external native spell namespace used by legacy item data. */
public sealed class AuthoritativeNativeSpellCatalog
{
    public const uint MaximumNativeSpellId = NativeCompatibilityLimits.MaximumNativeSpellId;

    private static readonly IReadOnlyList<string> ProductionColumns = [
        "native_spell_id",
        "native_spell_symbol",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-spell compatibility table '{table.SourcePath}' must use the complete ordered schema.");
        var catalog = LoadTsv(table.SourcePath, table.ToCanonicalTsv());
        catalog.ValidateCompleteNamespace();
    }

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

    /** Rejects a partial namespace when loading a production compatibility pack. */
    public void ValidateCompleteNamespace()
    {
        var missing = Enumerable.Range(0, checked((int)MaximumNativeSpellId + 1))
            .Where(nativeSpellId => !byId.ContainsKey(checked((uint)nativeSpellId)))
            .Select(nativeSpellId => nativeSpellId.ToString())
            .ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException(
                $"The native spell namespace is incomplete; missing IDs: {string.Join(", ", missing)}.");
    }

    public static AuthoritativeNativeSpellCatalog LoadTsv(
        string sourcePath,
        string contents,
        bool requireCompleteNamespace = false)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeSpellDefinition(
                row.RequiredUInt32("native_spell_id"),
                row.Required("native_spell_symbol")));
        var catalog = new AuthoritativeNativeSpellCatalog(definitions);
        if (requireCompleteNamespace)
            catalog.ValidateCompleteNamespace();
        return catalog;
    }
}
