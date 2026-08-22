using Devilution.Data;

namespace Devilution.Diablo;

/** Server-owned reward behavior applied when a staged monster conversation advances. */
public enum AuthoritativeQuestDialogueRewardKind
{
    None,
    /** Selects and generates one native-quality unique-monster item drop. */
    RandomUniqueMonsterDrop,
}

/** Server-owned follow-up applied after an accepted staged conversation. */
public enum AuthoritativeQuestDialogueFollowUpKind
{
    None,
    /** Applies one validated level-cell overlay from world_mutations.tsv. */
    WorldMutation,
}

/** One server-owned, user-visible stage in a unique-monster conversation. */
public sealed record AuthoritativeQuestDialogueStage(
    uint QuestId,
    int RequiredVariable1,
    int RequiredVariable2,
    int MessageId,
    int NextVariable1,
    int NextVariable2,
    uint RequiredItemId = 0,
    bool ConsumeItem = false,
    bool CompleteQuest = false,
    AuthoritativeQuestDialogueRewardKind RewardKind = AuthoritativeQuestDialogueRewardKind.None,
    int RewardItemLevel = 0,
    int RewardOffsetX = 0,
    int RewardOffsetY = 0,
    AuthoritativeQuestDialogueFollowUpKind FollowUpKind = AuthoritativeQuestDialogueFollowUpKind.None,
    uint FollowUpAmount = 0,
    int NextMessageId = 0);

/** Validated staged dialogue definitions supplied by an external content pack. */
public sealed class AuthoritativeQuestDialogueStageCatalog
{
    private readonly IReadOnlyList<AuthoritativeQuestDialogueStage> definitions;
    private readonly IReadOnlySet<uint> questIds;

    public AuthoritativeQuestDialogueStageCatalog(
        IEnumerable<AuthoritativeQuestDialogueStage> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(stage =>
                stage.QuestId == 0
                || stage.RequiredVariable1 is < 0 or > byte.MaxValue
                || stage.RequiredVariable2 is < -1 or > byte.MaxValue
                || stage.MessageId is <= 0 or > short.MaxValue
                || stage.NextMessageId is < 0 or > short.MaxValue
                || stage.NextVariable1 is < 0 or > byte.MaxValue
                || stage.NextVariable2 is < 0 or > byte.MaxValue
                || stage.RequiredVariable2 >= 0
                    && stage.NextVariable2 < stage.RequiredVariable2
                || stage.CompleteQuest
                    && (stage.RequiredItemId == 0 || !stage.ConsumeItem)
                || stage.RequiredItemId == 0 && stage.ConsumeItem
                || stage.NextVariable1 <= stage.RequiredVariable1
                || !Enum.IsDefined(stage.RewardKind)
                || stage.RewardItemLevel < 0
                || stage.RewardOffsetX is < -16 or > 16
                || stage.RewardOffsetY is < -16 or > 16
                || !Enum.IsDefined(stage.FollowUpKind)
                || stage.FollowUpKind == AuthoritativeQuestDialogueFollowUpKind.None
                    && stage.FollowUpAmount != 0
                || stage.FollowUpKind == AuthoritativeQuestDialogueFollowUpKind.WorldMutation
                    && stage.FollowUpAmount == 0
                || stage.RewardKind != AuthoritativeQuestDialogueRewardKind.None
                    && stage.RewardKind != AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop)) {
            throw new InvalidDataException(
                "Quest dialogue stages contain invalid values or a non-increasing variable-1 transition.");
        }
        if (values.GroupBy(stage => (
                stage.QuestId,
                stage.RequiredVariable1,
                stage.RequiredVariable2))
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Quest dialogue stages contain duplicate quest/variable gates.");
        }

        this.definitions = values
            .OrderBy(stage => stage.QuestId)
            .ThenBy(stage => stage.RequiredVariable1)
            .ThenBy(stage => stage.RequiredVariable2)
            .ThenBy(stage => stage.MessageId)
            .ToArray();
        questIds = this.definitions
            .Select(stage => stage.QuestId)
            .ToHashSet();
    }

    public IReadOnlyList<AuthoritativeQuestDialogueStage> Definitions
        => definitions;

    /** Returns true when this catalog owns the complete dialogue route for a quest. */
    public bool ContainsQuest(uint questId) => questIds.Contains(questId);

    /** Resolves an exact variable-2 gate before a wildcard gate. */
    public bool TryGet(
        uint questId,
        int variable1,
        int variable2,
        out AuthoritativeQuestDialogueStage stage)
    {
        var exact = definitions.FirstOrDefault(candidate =>
            candidate.QuestId == questId
            && candidate.RequiredVariable1 == variable1
            && candidate.RequiredVariable2 == variable2);
        if (exact is not null) {
            stage = exact;
            return true;
        }

        var wildcard = definitions.FirstOrDefault(candidate =>
            candidate.QuestId == questId
            && candidate.RequiredVariable1 == variable1
            && candidate.RequiredVariable2 == -1);
        if (wildcard is null) {
            stage = null!;
            return false;
        }

        stage = wildcard;
        return true;
    }

    /** Validates that every stage belongs to a unique-monster quest route. */
    public void ValidateQuests(IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var questById = quests.ToDictionary(quest => quest.QuestId);
        foreach (var group in definitions.GroupBy(stage => stage.QuestId)) {
            if (!questById.TryGetValue(group.Key, out var quest))
                throw new InvalidDataException(
                    $"Quest dialogue stages reference unknown quest {group.Key}.");
            if (string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue stages require a unique-monster identity.");
            if (quest.DialogueItemId != 0
                || quest.DialogueRewardUniqueId != 0
                || quest.DialogueRewardItemId != 0
                || quest.DialogueRewardOnActivation
                || quest.DialogueCompletionMessageId != 0) {
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} cannot combine staged dialogue with legacy dialogue hand-in fields.");
            }

            var finalVariable1 = group.Max(stage => stage.NextVariable1);
            if (quest.DialogueCompletionVariable1 >= 0
                && quest.DialogueCompletionVariable1 < finalVariable1) {
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue completion gate is below its final staged variable-1 value.");
            }
        }
    }

    /** Validates item identities used by item-gated dialogue stages. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var stage in definitions.Where(stage => stage.RequiredItemId != 0)) {
            if (!items.TryGet(stage.RequiredItemId, out _))
                throw new InvalidDataException(
                    $"Quest dialogue stage for quest {stage.QuestId} references unknown item {stage.RequiredItemId}.");
        }
    }

    /** Validates world-mutation references used by staged dialogue follow-ups. */
    public void ValidateWorldMutations(AuthoritativeWorldMutationCatalog mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        foreach (var stage in definitions.Where(stage =>
                     stage.FollowUpKind == AuthoritativeQuestDialogueFollowUpKind.WorldMutation)) {
            if (!mutations.TryGet(stage.FollowUpAmount, out _))
                throw new InvalidDataException(
                    $"Quest dialogue stage for quest {stage.QuestId} references unknown world mutation {stage.FollowUpAmount}.");
        }
    }

    public static AuthoritativeQuestDialogueStageCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeQuestDialogueStageCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestDialogueStage(
                AuthoritativeQuestIdentity.Resolve(row, stableIds),
                row.RequiredInt32("required_variable1"),
                row.OptionalInt32("required_variable2", -1),
                row.RequiredInt32("message_id"),
                row.RequiredInt32("next_variable1"),
                row.OptionalInt32("next_variable2"),
                AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "required_item_id",
                    "required_item_symbol"),
                row.OptionalBoolean("consume_item"),
                row.OptionalBoolean("complete_quest"),
                ParseRewardKind(row),
                row.OptionalInt32("reward_item_level"),
                row.OptionalInt32("reward_offset_x"),
                row.OptionalInt32("reward_offset_y"),
                ParseFollowUpKind(row),
                row.OptionalUInt32("follow_up_amount"),
                row.OptionalInt32("next_message_id"))));
    }

    private static AuthoritativeQuestDialogueRewardKind ParseRewardKind(TsvRow row)
    {
        if (!row.TryGet("reward_kind", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeQuestDialogueRewardKind.None;
        if (Enum.TryParse<AuthoritativeQuestDialogueRewardKind>(value, true, out var kind)
            && Enum.IsDefined(kind))
            return kind;
        throw new InvalidDataException(
            $"Quest dialogue stage row {row.LineNumber} contains an unknown reward kind '{value}'.");
    }

    private static AuthoritativeQuestDialogueFollowUpKind ParseFollowUpKind(TsvRow row)
    {
        if (!row.TryGet("follow_up_kind", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            return AuthoritativeQuestDialogueFollowUpKind.None;
        if (Enum.TryParse<AuthoritativeQuestDialogueFollowUpKind>(value, true, out var kind)
            && Enum.IsDefined(kind))
            return kind;
        throw new InvalidDataException(
            $"Quest dialogue stage row {row.LineNumber} contains an unknown follow-up kind '{value}'.");
    }
}
