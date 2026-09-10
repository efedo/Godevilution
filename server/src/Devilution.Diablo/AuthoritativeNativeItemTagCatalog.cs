using Devilution.Data;

namespace Devilution.Diablo;

/** Native legacy item-type symbol and generation tags. */
public sealed record AuthoritativeNativeItemTagDefinition(
    string ItemType,
    IReadOnlySet<string> Tags);

/** Validated external generation tags for legacy item-type symbols. */
public sealed class AuthoritativeNativeItemTagCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "item_type",
        "tags",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-item-tag compatibility table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("item_type");
            _ = row.Required("tags");
        }
    }

    private readonly Dictionary<string, IReadOnlySet<string>> byItemType =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeNativeItemTagCatalog(
        IEnumerable<AuthoritativeNativeItemTagDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.ItemType))
                throw new InvalidDataException(
                    "Native item tag definitions require an item type.");
            ArgumentNullException.ThrowIfNull(definition.Tags);
            var itemType = definition.ItemType.Trim();
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in definition.Tags) {
                if (string.IsNullOrWhiteSpace(tag))
                    throw new InvalidDataException(
                        $"Native item tag definition '{itemType}' contains an empty tag.");
                tags.Add(tag.Trim());
            }
            if (tags.Count == 0)
                throw new InvalidDataException(
                    $"Native item tag definition '{itemType}' requires at least one tag.");
            if (!byItemType.TryAdd(itemType, tags))
                throw new InvalidDataException(
                    $"Native item type '{itemType}' is defined more than once in the item tag catalog.");
        }
    }

    public IReadOnlyList<AuthoritativeNativeItemTagDefinition> Definitions
        => byItemType
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new AuthoritativeNativeItemTagDefinition(
                pair.Key,
                new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

    public bool TryGetTags(
        string itemType,
        out IReadOnlySet<string> tags)
    {
        if (string.IsNullOrWhiteSpace(itemType)) {
            tags = EmptyTags;
            return false;
        }
        return byItemType.TryGetValue(itemType.Trim(), out tags!);
    }

    public IReadOnlySet<string> GetRequiredTags(string itemType)
    {
        return TryGetTags(itemType, out var tags)
            ? tags
            : throw new KeyNotFoundException(
                $"Native item type '{itemType}' is not defined by the active item tag catalog.");
    }

    public static AuthoritativeNativeItemTagCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row =>
            new AuthoritativeNativeItemTagDefinition(
                row.Required("item_type"),
                ParseTags(row.Required("tags"), row.LineNumber)));
        return new AuthoritativeNativeItemTagCatalog(definitions);
    }

    private static IReadOnlySet<string> ParseTags(string value, int lineNumber)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in value.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (!tags.Add(tag))
                throw new InvalidDataException(
                    $"TSV row {lineNumber} defines native item tag '{tag}' more than once.");
        }
        return tags;
    }

    private static IReadOnlySet<string> EmptyTags { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
