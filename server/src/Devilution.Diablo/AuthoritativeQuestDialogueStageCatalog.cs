using Devilution.Data;

namespace Devilution.Diablo;

/** Server-owned reward behavior applied when a staged monster conversation advances. */
public enum AuthoritativeQuestDialogueRewardKind
{
    None,
    /** Selects and generates one native-quality unique-monster item drop. */
    RandomUniqueMonsterDrop,
    /** Generates the authored normal item at the dialogue target's offset. */
    NormalItem,
    /** Generates the authored unique item at the dialogue target's offset. */
    UniqueItem,
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
    int NextMessageId = 0,
    uint RewardItemId = 0,
    bool DefeatTarget = false,
    uint RewardUniqueId = 0,
    bool RequireItemPresent = true);

/** Validated staged dialogue definitions supplied by an external content pack. */
public sealed class AuthoritativeQuestDialogueStageCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "quest_id",
        "quest_symbol",
        "required_variable1",
        "required_variable2",
        "message_id",
        "next_variable1",
        "next_variable2",
        "required_item_id",
        "required_item_symbol",
        "consume_item",
        "complete_quest",
        "reward_kind",
        "reward_item_level",
        "reward_offset_x",
        "reward_offset_y",
        "next_message_id",
        "follow_up_kind",
        "follow_up_amount",
        "reward_item_id",
        "reward_item_symbol",
        "defeat_target",
        "reward_unique_id",
        "reward_unique_symbol",
        "require_item_present",
    ];

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
                || stage.NextVariable1 < stage.RequiredVariable1
                || stage.NextVariable1 == stage.RequiredVariable1
                    && !(stage.CompleteQuest
                        || stage.RequiredItemId != 0 && !stage.RequireItemPresent)
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
                    && stage.RewardKind is not (AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop
                        or AuthoritativeQuestDialogueRewardKind.NormalItem
                        or AuthoritativeQuestDialogueRewardKind.UniqueItem)
                || stage.RewardKind == AuthoritativeQuestDialogueRewardKind.NormalItem
                    && stage.RewardItemId == 0
                || stage.RewardKind != AuthoritativeQuestDialogueRewardKind.NormalItem
                    && stage.RewardItemId != 0
                || stage.RewardKind == AuthoritativeQuestDialogueRewardKind.UniqueItem
                    && stage.RewardUniqueId == 0
                || stage.RewardKind != AuthoritativeQuestDialogueRewardKind.UniqueItem
                    && stage.RewardUniqueId != 0
                || stage.RewardItemId != 0 && stage.RewardUniqueId != 0
                || stage.RequiredItemId == 0 && !stage.RequireItemPresent
                || stage.RequiredItemId == 0 && stage.ConsumeItem
                || stage.RequiredItemId != 0 && !stage.RequireItemPresent && stage.ConsumeItem
                || stage.DefeatTarget && !stage.CompleteQuest)) {
            throw new InvalidDataException(
                "Quest dialogue stages contain invalid values, ambiguous gates, or an invalid variable-1 transition.");
        }
        if (values.GroupBy(stage => (
                stage.QuestId,
                stage.RequiredVariable1,
                stage.RequiredVariable2))
                .Any(group => group.Count() > 1
                    && (group.Count() != 2
                        || group.Any(stage => stage.RequiredItemId == 0)
                        || group.Select(stage => (
                                stage.RequiredItemId,
                                stage.RequireItemPresent))
                            .Distinct()
                            .Count() != 2
                        || group.Select(stage => stage.RequiredItemId).Distinct().Count() != 1))) {
            throw new InvalidDataException(
                "Quest dialogue stages contain duplicate quest/variable gates.");
        }

        this.definitions = values
            .OrderBy(stage => stage.QuestId)
            .ThenBy(stage => stage.RequiredVariable1)
            .ThenBy(stage => stage.RequiredVariable2)
            .ThenBy(stage => stage.RequiredItemId)
            .ThenByDescending(stage => stage.RequireItemPresent)
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

    /** Returns true when an authored stage still owns the current dialogue gate. */
    public bool HasPendingStage(uint questId, int variable1, int variable2)
        => TryGet(questId, variable1, variable2, out _);

    /**
     * Resolves the message identity safe for shared target presentation.
     *
     * Item branches are selected by the interacting player, so a projected
     * target must prefer the item-absent branch when no item-independent stage
     * exists. This keeps multiplayer snapshots from advertising a reward
     * branch that only one participant can satisfy.
     */
    public bool TryGetPresentationStage(
        uint questId,
        int variable1,
        int variable2,
        out AuthoritativeQuestDialogueStage stage)
    {
        var candidates = definitions
            .Where(candidate => candidate.QuestId == questId
                && candidate.RequiredVariable1 == variable1
                && (candidate.RequiredVariable2 == variable2
                    || candidate.RequiredVariable2 == -1))
            .OrderBy(candidate => candidate.RequiredVariable2 == variable2 ? 0 : 1)
            .ThenBy(candidate => candidate.RequiredItemId == 0
                ? 0
                : candidate.RequireItemPresent ? 2 : 1)
            .ThenBy(candidate => candidate.MessageId);
        stage = candidates.FirstOrDefault()!;
        return stage is not null;
    }

    /** Resolves an exact variable-2 gate before a wildcard gate. */
    public bool TryGet(
        uint questId,
        int variable1,
        int variable2,
        out AuthoritativeQuestDialogueStage stage)
        => TryGet(questId, variable1, variable2, static _ => true, out stage);

    /** Resolves a variable gate while applying an item-presence branch predicate. */
    public bool TryGet(
        uint questId,
        int variable1,
        int variable2,
        Func<AuthoritativeQuestDialogueStage, bool> predicate,
        out AuthoritativeQuestDialogueStage stage)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var exact = definitions.FirstOrDefault(candidate =>
            candidate.QuestId == questId
            && candidate.RequiredVariable1 == variable1
            && candidate.RequiredVariable2 == variable2
            && predicate(candidate));
        if (exact is not null) {
            stage = exact;
            return true;
        }

        var wildcard = definitions.FirstOrDefault(candidate =>
            candidate.QuestId == questId
            && candidate.RequiredVariable1 == variable1
            && candidate.RequiredVariable2 == -1
            && predicate(candidate));
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

    /** Validates item identities used by item-gated dialogue stages and rewards. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var stage in definitions.Where(stage => stage.RequiredItemId != 0)) {
            if (!items.TryGet(stage.RequiredItemId, out _))
                throw new InvalidDataException(
                    $"Quest dialogue stage for quest {stage.QuestId} references unknown item {stage.RequiredItemId}.");
        }
        foreach (var stage in definitions.Where(stage => stage.RewardItemId != 0)) {
            if (!items.TryGet(stage.RewardItemId, out _))
                throw new InvalidDataException(
                    $"Quest dialogue stage for quest {stage.QuestId} references unknown reward item {stage.RewardItemId}.");
        }
    }

    /** Validates unique-item identities used by authored staged rewards. */
    public void ValidateUniqueItems(AuthoritativeUniqueItemCatalog uniqueItems)
    {
        ArgumentNullException.ThrowIfNull(uniqueItems);
        foreach (var stage in definitions.Where(stage => stage.RewardUniqueId != 0)) {
            if (!uniqueItems.TryGet(stage.RewardUniqueId, out _))
                throw new InvalidDataException(
                    $"Quest dialogue stage for quest {stage.QuestId} references unknown unique reward {stage.RewardUniqueId}.");
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

    /** Rejects partial staged-dialogue metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest-dialogue stage table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("quest_id");
            _ = row.Required("required_variable1");
            _ = row.Required("message_id");
            _ = row.Required("next_variable1");
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
                row.OptionalInt32("next_message_id"),
                AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "reward_item_id",
                    "reward_item_symbol"),
                row.OptionalBoolean("defeat_target"),
                AuthoritativeUniqueItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "reward_unique_id",
                    "reward_unique_symbol"),
                row.OptionalBoolean("require_item_present", true))));
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
