using Devilution.Data;

namespace Devilution.Diablo;

/** Mutable server-owned progress for one quest definition. */
public sealed class AuthoritativeQuestState
{
    public AuthoritativeQuestState(
        uint questId,
        uint levelId,
        uint requiredProgress,
        uint progress = 0,
        bool completed = false,
        bool unavailable = false,
        bool active = true,
        bool logged = false,
        int variable1 = 0,
        int variable2 = 0,
        int messageId = 0,
        uint completionMonsterId = 0)
    {
        if (questId == 0 || requiredProgress == 0 || progress > requiredProgress)
            throw new ArgumentOutOfRangeException(nameof(questId));
        if (unavailable && (completed || progress != 0 || logged))
            throw new ArgumentOutOfRangeException(nameof(unavailable));
        if (variable1 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(variable1));
        if (variable2 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(variable2));
        if (messageId < 0 || messageId > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(messageId));
        QuestId = questId;
        LevelId = levelId;
        RequiredProgress = requiredProgress;
        Progress = progress;
        Completed = completed || progress == requiredProgress;
        Unavailable = unavailable;
        Active = active && !Completed && !Unavailable;
        Logged = logged && !Unavailable;
        Variable1 = variable1;
        Variable2 = variable2;
        MessageId = messageId;
        CompletionMonsterId = completionMonsterId;
    }

    public uint QuestId { get; }
    public uint LevelId { get; set; }
    public uint Progress { get; set; }
    public uint RequiredProgress { get; set; }
    public bool Completed { get; set; }
    /** Native single-player pool state; unavailable quests cannot advance. */
    public bool Unavailable { get; set; }
    /** Native QUEST_INIT/QUEST_ACTIVE distinction; legacy rows default active. */
    public bool Active { get; set; }
    /** Native quest-log visibility bit set when the quest is surfaced. */
    public bool Logged { get; set; }
    /** Native _qvar1 quest-specific progression byte. */
    public int Variable1 { get; set; }
    /** Native _qvar2 quest-specific progression byte. */
    public int Variable2 { get; set; }
    /** Native _qmsg speech identity selected by quest synchronization. */
    public int MessageId { get; set; }
    /** Optional native monster identity whose defeat advances this quest. */
    public uint CompletionMonsterId { get; set; }
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
                row.OptionalInt32("completed") != 0,
                row.OptionalInt32("unavailable") != 0,
                active: row.OptionalInt32("initial") == 0,
                logged: row.OptionalInt32("logged") != 0,
                variable1: row.OptionalInt32("variable1"),
                variable2: row.OptionalInt32("variable2"),
                messageId: row.OptionalInt32("message_id"),
                completionMonsterId: row.OptionalUInt32("completion_monster_id"));
        }).ToArray();
        if (quests.Select(quest => quest.QuestId).Distinct().Count() != quests.Length)
            throw new InvalidDataException($"Quest table '{sourcePath}' contains duplicate quest IDs.");
        return quests;
    }

    /** Applies native single-player pool exclusions to the loaded quest state. */
    public static void ApplySinglePlayerPoolSelection(
        IReadOnlyList<AuthoritativeQuestState> quests,
        AuthoritativeQuestPoolCatalog questPools,
        uint seed)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(questPools);
        var unavailable = questPools.SelectUnavailableQuestIds(seed).ToHashSet();
        foreach (var quest in quests) {
            quest.Unavailable = !quest.Completed && unavailable.Contains(quest.QuestId);
            if (quest.Unavailable) {
                quest.Active = false;
                quest.Logged = false;
            }
        }
    }
}
