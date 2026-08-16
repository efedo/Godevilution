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
        uint completionMonsterId = 0,
        string completionUniqueIdentity = "",
        int dialogueVariable1 = 0,
        int dialogueVariable2 = 0,
        int completionVariable1 = 0,
        int completionVariable2 = 0,
        uint completionActivateQuestId = 0,
        bool activateOnRoute = true,
        bool completionDiablo = false,
        uint completionRewardItemId = 0,
        int completionRewardItemLevel = 0)
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
        if (dialogueVariable1 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueVariable1));
        if (dialogueVariable2 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueVariable2));
        if (completionVariable1 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(completionVariable1));
        if (completionVariable2 is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(completionVariable2));
        if (completionRewardItemLevel is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(completionRewardItemLevel));
        if (completionUniqueIdentity.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(completionUniqueIdentity));
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
        CompletionUniqueIdentity = completionUniqueIdentity;
        DialogueVariable1 = dialogueVariable1;
        DialogueVariable2 = dialogueVariable2;
        CompletionVariable1 = completionVariable1;
        CompletionVariable2 = completionVariable2;
        CompletionActivateQuestId = completionActivateQuestId;
        ActivateOnRoute = activateOnRoute;
        CompletionDiablo = completionDiablo;
        CompletionRewardItemId = completionRewardItemId;
        CompletionRewardItemLevel = completionRewardItemLevel;
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
    /** Optional native unique-monster identity whose defeat advances this quest. */
    public string CompletionUniqueIdentity { get; set; }
    /** Content-owned minimum native _qvar1 after the first quest conversation. */
    public int DialogueVariable1 { get; }
    /** Content-owned minimum native _qvar2 after the first quest conversation. */
    public int DialogueVariable2 { get; }
    /** Content-owned minimum native _qvar1 after quest completion. */
    public int CompletionVariable1 { get; }
    /** Content-owned minimum native _qvar2 after quest completion. */
    public int CompletionVariable2 { get; }
    /** Content-owned quest identity activated when this quest completes. */
    public uint CompletionActivateQuestId { get; }
    /** Whether native route-level entry may activate this quest automatically. */
    public bool ActivateOnRoute { get; }
    /** Whether the native Diablo boss identity completes this quest. */
    public bool CompletionDiablo { get; }
    /** Optional item definition spawned at the native monster-defeat reward boundary. */
    public uint CompletionRewardItemId { get; }
    /** Optional native item level used when generating the completion reward. */
    public int CompletionRewardItemLevel { get; }
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
                completionMonsterId: row.OptionalUInt32("completion_monster_id"),
                completionUniqueIdentity: row.TryGet("completion_unique_identity", out var uniqueIdentity)
                    && !string.Equals(uniqueIdentity, "NONE", StringComparison.OrdinalIgnoreCase)
                    ? uniqueIdentity
                    : string.Empty,
                dialogueVariable1: row.OptionalInt32("dialogue_variable1"),
                dialogueVariable2: row.OptionalInt32("dialogue_variable2"),
                completionVariable1: row.OptionalInt32("completion_variable1"),
                completionVariable2: row.OptionalInt32("completion_variable2"),
                completionActivateQuestId: row.OptionalUInt32("completion_activate_quest_id"),
                activateOnRoute: row.OptionalInt32("route_activation") != 0,
                completionDiablo: row.OptionalInt32("completion_diablo") != 0,
                completionRewardItemId: row.OptionalUInt32("completion_reward_item_id"),
                completionRewardItemLevel: row.OptionalInt32("completion_reward_item_level"));
        }).ToArray();
        if (quests.Select(quest => quest.QuestId).Distinct().Count() != quests.Length)
            throw new InvalidDataException($"Quest table '{sourcePath}' contains duplicate quest IDs.");
        return quests;
    }

    /** Validates quest-completion rewards against the active item catalog. */
    public static void ValidateRewardItems(
        IReadOnlyList<AuthoritativeQuestState> quests,
        AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var quest in quests.Where(quest => quest.CompletionRewardItemId != 0)) {
            if (!items.TryGet(quest.CompletionRewardItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown completion reward item {quest.CompletionRewardItemId}.");
        }
    }

    /** Combines base quest state with an additive variant-specific state table. */
    public static IReadOnlyList<AuthoritativeQuestState> Merge(
        IReadOnlyList<AuthoritativeQuestState> baseQuests,
        IReadOnlyList<AuthoritativeQuestState> additionalQuests,
        string additionalSourcePath = "quest_states.tsv")
    {
        ArgumentNullException.ThrowIfNull(baseQuests);
        ArgumentNullException.ThrowIfNull(additionalQuests);
        ArgumentException.ThrowIfNullOrWhiteSpace(additionalSourcePath);

        var result = baseQuests.ToList();
        var knownQuestIds = result.Select(quest => quest.QuestId).ToHashSet();
        foreach (var quest in additionalQuests) {
            if (!knownQuestIds.Add(quest.QuestId))
                throw new InvalidDataException(
                    $"Quest state table '{additionalSourcePath}' contains duplicate quest ID {quest.QuestId}.");
            result.Add(quest);
        }
        ValidateActivationTargets(result);
        return result;
    }

    /** Validates follow-on quest identities for any active quest table. */
    public static void ValidateActivationTargets(
        IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var values = quests.ToArray();
        var questIds = values.Select(quest => quest.QuestId).ToHashSet();
        foreach (var quest in values.Where(quest => quest.CompletionActivateQuestId != 0)) {
            if (!questIds.Contains(quest.CompletionActivateQuestId))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} activates unknown quest {quest.CompletionActivateQuestId}.");
        }
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
