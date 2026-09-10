using Devilution.Data;

namespace Devilution.Diablo;

/** Native quest transition requested when a content-owned item is picked up. */
public enum AuthoritativeItemQuestEffectKind
{
    ActivateQuest,
    CompleteQuest,
    SetVariable1,
}

/** One validated item-to-quest transition from the active content pack. */
public sealed record AuthoritativeItemQuestEffectDefinition(
    uint ItemId,
    uint QuestId,
    AuthoritativeItemQuestEffectKind EffectKind,
    int EffectAmount,
    int? RequiredVariable1 = null);

/** Validated content-owned quest effects applied at the server pickup boundary. */
public sealed class AuthoritativeItemQuestEffectCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "item_id",
        "item_symbol",
        "quest_id",
        "quest_symbol",
        "effect_kind",
        "effect_amount",
        "required_variable1",
    ];

    private readonly IReadOnlyList<AuthoritativeItemQuestEffectDefinition> definitions;

    public AuthoritativeItemQuestEffectCatalog(
        IEnumerable<AuthoritativeItemQuestEffectDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.ToArray();
        if (ordered.Any(definition =>
                definition.ItemId == 0
                || definition.QuestId == 0
                || !Enum.IsDefined(definition.EffectKind)
                || definition.EffectAmount is < 0 or > byte.MaxValue
                || definition.RequiredVariable1 is < 0 or > byte.MaxValue)) {
            throw new InvalidDataException("Item quest effects contain invalid values.");
        }
        if (ordered.GroupBy(definition =>
                (definition.ItemId, definition.QuestId, definition.EffectKind))
            .Any(group => group.Count() > 1)) {
            throw new InvalidDataException(
                "Item quest effects define the same transition more than once.");
        }
        this.definitions = ordered
            .OrderBy(definition => definition.ItemId)
            .ThenBy(definition => definition.QuestId)
            .ThenBy(definition => definition.EffectKind)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeItemQuestEffectDefinition> Definitions
        => definitions;

    /** Validates item identities against the active normalized item catalog. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in definitions) {
            if (!items.TryGet(definition.ItemId, out _))
                throw new InvalidDataException(
                    $"Item quest effect references unknown item {definition.ItemId}.");
        }
    }

    /** Validates quest identities against the active server-owned quest state. */
    public void ValidateQuests(IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var definition in definitions) {
            if (!questIds.Contains(definition.QuestId))
                throw new InvalidDataException(
                    $"Item quest effect references unknown quest {definition.QuestId}.");
        }
    }

    /** Rejects partial item-to-quest transition metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production item quest-effect table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("item_id");
            _ = row.Required("quest_id");
            _ = row.Required("effect_kind");
        }
    }

    public static AuthoritativeItemQuestEffectCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeItemQuestEffectCatalog(table.Rows.Select(row =>
            new AuthoritativeItemQuestEffectDefinition(
                AuthoritativeItemIdentity.Resolve(row, stableIds),
                AuthoritativeQuestIdentity.Resolve(
                    row,
                    stableIds,
                    "quest_id",
                    "quest_symbol"),
                ParseEffectKind(row.Required("effect_kind"), sourcePath, row.LineNumber),
                row.OptionalInt32("effect_amount"),
                row.TryGet("required_variable1", out var requiredVariable1)
                    && !string.IsNullOrWhiteSpace(requiredVariable1)
                    && !string.Equals(
                        requiredVariable1,
                        "NONE",
                        StringComparison.OrdinalIgnoreCase)
                    ? row.OptionalInt32("required_variable1")
                    : null)));
    }

    private static AuthoritativeItemQuestEffectKind ParseEffectKind(
        string value,
        string sourcePath,
        int lineNumber)
    {
        if (Enum.TryParse<AuthoritativeItemQuestEffectKind>(
                value,
                ignoreCase: true,
                out var effectKind)
            && Enum.IsDefined(effectKind)) {
            return effectKind;
        }
        throw new InvalidDataException(
            $"Item quest effect table '{sourcePath}' has an invalid effect kind '{value}' on line {lineNumber}.");
    }
}
