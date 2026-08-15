using Devilution.Data;

namespace Devilution.Diablo;

/** One deterministic item combination declared by the active content pack. */
public sealed record AuthoritativeItemCombinationDefinition(
    uint ResultItemId,
    IReadOnlyList<uint> RequiredItemIds);

/** Validated content-owned item-combination rules used by server inventory actions. */
public sealed class AuthoritativeItemCombinationCatalog
{
    private readonly IReadOnlyList<AuthoritativeItemCombinationDefinition> definitions;

    public AuthoritativeItemCombinationCatalog(
        IEnumerable<AuthoritativeItemCombinationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.ToArray();
        if (ordered.Any(definition => definition.ResultItemId == 0
                || definition.RequiredItemIds is null
                || definition.RequiredItemIds.Count < 2
                || definition.RequiredItemIds.Any(itemId => itemId == 0)
                || definition.RequiredItemIds.Distinct().Count() != definition.RequiredItemIds.Count
                || definition.RequiredItemIds.Contains(definition.ResultItemId))) {
            throw new InvalidDataException("Item combinations contain invalid identities.");
        }
        if (ordered.GroupBy(definition => definition.ResultItemId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Item combinations define a result more than once.");
        this.definitions = ordered
            .OrderBy(definition => definition.ResultItemId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeItemCombinationDefinition> Definitions => definitions;

    /** Validates every combination identity against the active normalized item catalog. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in definitions) {
            if (!items.TryGet(definition.ResultItemId, out _))
                throw new InvalidDataException(
                    $"Item combination result {definition.ResultItemId} is not present in the authoritative catalog.");
            foreach (var itemId in definition.RequiredItemIds) {
                if (!items.TryGet(itemId, out _))
                    throw new InvalidDataException(
                        $"Item combination input {itemId} is not present in the authoritative catalog.");
            }
        }
    }

    public static AuthoritativeItemCombinationCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeItemCombinationCatalog(table.Rows.Select(row =>
            new AuthoritativeItemCombinationDefinition(
                row.RequiredUInt32("result_item_id"),
                ParseRequiredItemIds(row.Required("required_item_ids"), sourcePath, row.LineNumber))));
    }

    private static IReadOnlyList<uint> ParseRequiredItemIds(
        string value,
        string sourcePath,
        int lineNumber)
    {
        var values = value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => uint.TryParse(token, out var itemId) && itemId != 0
                ? itemId
                : throw new InvalidDataException(
                    $"Item combination table '{sourcePath}' has an invalid item ID '{token}' on line {lineNumber}."))
            .ToArray();
        if (values.Length < 2)
            throw new InvalidDataException(
                $"Item combination table '{sourcePath}' requires at least two input items on line {lineNumber}.");
        return values;
    }
}
