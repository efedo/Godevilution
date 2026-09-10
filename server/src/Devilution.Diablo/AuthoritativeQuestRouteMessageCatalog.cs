using Devilution.Data;

namespace Devilution.Diablo;

/** One server-owned dialogue message emitted when a route activates a quest. */
public sealed record AuthoritativeQuestRouteMessageDefinition(
    uint QuestId,
    uint LevelId,
    int MessageId);

/** Validated route-entry quest dialogue supplied by an external content pack. */
public sealed class AuthoritativeQuestRouteMessageCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "quest_id",
        "quest_symbol",
        "level_id",
        "level_symbol",
        "message_id",
    ];

    private readonly IReadOnlyDictionary<uint, AuthoritativeQuestRouteMessageDefinition>
        definitions;

    public AuthoritativeQuestRouteMessageCatalog(
        IEnumerable<AuthoritativeQuestRouteMessageDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition =>
                definition.QuestId == 0
                || definition.LevelId > ushort.MaxValue
                || definition.MessageId <= 0
                || definition.MessageId > short.MaxValue)) {
            throw new InvalidDataException(
                "Quest route messages contain invalid values.");
        }
        if (values.GroupBy(definition => definition.QuestId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Quest route messages contain duplicate quest IDs.");
        }
        this.definitions = values.ToDictionary(definition => definition.QuestId);
    }

    public IReadOnlyCollection<AuthoritativeQuestRouteMessageDefinition> Definitions
        => definitions.Values.ToArray();

    public bool TryGet(
        uint questId,
        out AuthoritativeQuestRouteMessageDefinition definition)
        => definitions.TryGetValue(questId, out definition!);

    public void ValidateQuests(IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var definition in definitions.Values) {
            if (!questIds.Contains(definition.QuestId))
                throw new InvalidDataException(
                    $"Quest route message references unknown quest {definition.QuestId}.");
        }
    }

    /** Rejects partial route-message metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest route message table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("quest_id");
            _ = row.Required("level_id");
            _ = row.Required("message_id");
        }
    }

    public static AuthoritativeQuestRouteMessageCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeQuestRouteMessageCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestRouteMessageDefinition(
                AuthoritativeQuestIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredInt32("message_id"))));
    }
}
