using Devilution.Data;

namespace Devilution.Diablo;

/** Mutable server-owned progress for one quest definition. */
public sealed class AuthoritativeQuestState
{
    public AuthoritativeQuestState(uint questId, uint levelId, uint requiredProgress, uint progress = 0, bool completed = false)
    {
        if (questId == 0 || requiredProgress == 0 || progress > requiredProgress)
            throw new ArgumentOutOfRangeException(nameof(questId));
        QuestId = questId;
        LevelId = levelId;
        RequiredProgress = requiredProgress;
        Progress = progress;
        Completed = completed || progress == requiredProgress;
    }

    public uint QuestId { get; }
    public uint LevelId { get; set; }
    public uint Progress { get; set; }
    public uint RequiredProgress { get; set; }
    public bool Completed { get; set; }
}

/** External quest definitions and initial progress. */
public static class AuthoritativeQuestCatalog
{
    public static IReadOnlyList<AuthoritativeQuestState> LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeQuestTypeCatalog? questTypes = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var quests = table.Rows.Select(row => {
            var questId = row.RequiredUInt32("quest_id");
            if (questTypes is not null && !questTypes.TryGet(questId, out _))
                throw new InvalidDataException(
                    $"Quest row {row.LineNumber} references unknown quest type {questId}.");
            return new AuthoritativeQuestState(
                questId,
                row.RequiredUInt32("level_id"),
                row.RequiredUInt32("required_progress"),
                row.OptionalUInt32("progress"),
                row.OptionalInt32("completed") != 0);
        }).ToArray();
        if (quests.Select(quest => quest.QuestId).Distinct().Count() != quests.Length)
            throw new InvalidDataException($"Quest table '{sourcePath}' contains duplicate quest IDs.");
        return quests;
    }
}
