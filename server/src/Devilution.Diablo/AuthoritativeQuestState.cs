using Devilution.Data;

namespace Devilution.Diablo;

/** Resolves optional stable quest symbols without breaking numeric legacy rows. */
internal static class AuthoritativeQuestIdentity
{
    public static uint Resolve(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn = "quest_id",
        string symbolColumn = "quest_symbol")
    {
        var numericId = row.RequiredUInt32(numericColumn);
        var symbol = ReadSymbol(row, symbolColumn);
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Quest row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("quest", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Quest row {row.LineNumber} references unknown stable quest symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Quest row {row.LineNumber} maps quest {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    public static string ReadSymbol(TsvRow row, string symbolColumn = "quest_symbol")
        => row.TryGet(symbolColumn, out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            ? symbol
            : string.Empty;

    public static uint ResolveOptional(
        TsvRow row,
        StableContentIdCatalog? stableIds,
        string numericColumn,
        string symbolColumn)
    {
        var numericId = row.OptionalUInt32(numericColumn);
        if (!row.TryGet(symbolColumn, out var symbol)
            || string.IsNullOrWhiteSpace(symbol)) {
            return numericId;
        }
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Quest row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("quest", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Quest row {row.LineNumber} references unknown stable quest symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Quest row {row.LineNumber} maps quest {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }
}

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
        int completionRewardItemLevel = 0,
        int turnInTownerId = -1,
        uint turnInLevelId = 0,
        int turnInPositionX = 0,
        int turnInPositionY = 0,
        uint turnInItemId = 0,
        int turnInRequiredVariable2 = -1,
        uint turnInRewardUniqueId = 0,
        int turnInRewardItemLevel = 0,
        int turnInRewardPositionX = 0,
        int turnInRewardPositionY = 0,
        int turnInMessageId = 0)
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
        if (turnInTownerId is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInTownerId));
        if (turnInRequiredVariable2 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInRequiredVariable2));
        if (turnInRewardItemLevel is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(turnInRewardItemLevel));
        if (turnInMessageId < 0 || turnInMessageId > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInMessageId));
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
        TurnInTownerId = turnInTownerId;
        TurnInLevelId = turnInLevelId;
        TurnInPositionX = turnInPositionX;
        TurnInPositionY = turnInPositionY;
        TurnInItemId = turnInItemId;
        TurnInRequiredVariable2 = turnInRequiredVariable2;
        TurnInRewardUniqueId = turnInRewardUniqueId;
        TurnInRewardItemLevel = turnInRewardItemLevel;
        TurnInRewardPositionX = turnInRewardPositionX;
        TurnInRewardPositionY = turnInRewardPositionY;
        TurnInMessageId = turnInMessageId;
    }

    /** Optional stable symbol resolved from content_ids.tsv. */
    public string StableSymbol { get; init; } = string.Empty;

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
    /** Optional native towner identity that accepts this quest's turn-in. */
    public int TurnInTownerId { get; }
    /** Level containing the configured towner and reward position. */
    public uint TurnInLevelId { get; }
    public int TurnInPositionX { get; }
    public int TurnInPositionY { get; }
    /** Required normalized inventory item consumed by the turn-in. */
    public uint TurnInItemId { get; }
    /** Optional exact native _qvar2 required before turn-in. */
    public int TurnInRequiredVariable2 { get; }
    /** Optional stable unique reward generated at the native towner position. */
    public uint TurnInRewardUniqueId { get; }
    public int TurnInRewardItemLevel { get; }
    public int TurnInRewardPositionX { get; }
    public int TurnInRewardPositionY { get; }
    /** Optional native dialogue identity emitted after a successful turn-in. */
    public int TurnInMessageId { get; }
}

/** External quest definitions and initial progress. */
public static class AuthoritativeQuestCatalog
{
    public static IReadOnlyList<AuthoritativeQuestState> LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var quests = table.Rows.Select(row => {
            var questId = AuthoritativeQuestIdentity.Resolve(row, stableIds);
            if (questTypes is not null && !questTypes.TryGet(questId, out _))
                throw new InvalidDataException(
                    $"Quest row {row.LineNumber} references unknown quest type {questId}.");
            return new AuthoritativeQuestState(
                questId,
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredUInt32("required_progress"),
                row.OptionalUInt32("progress"),
                row.OptionalBoolean("completed"),
                row.OptionalBoolean("unavailable"),
                active: !row.OptionalBoolean("initial"),
                logged: row.OptionalBoolean("logged"),
                variable1: row.OptionalInt32("variable1"),
                variable2: row.OptionalInt32("variable2"),
                messageId: row.OptionalInt32("message_id"),
                completionMonsterId: AuthoritativeMonsterIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "completion_monster_id",
                    "completion_monster_symbol"),
                completionUniqueIdentity: row.TryGet("completion_unique_identity", out var uniqueIdentity)
                    && !string.Equals(uniqueIdentity, "NONE", StringComparison.OrdinalIgnoreCase)
                    ? uniqueIdentity
                    : string.Empty,
                dialogueVariable1: row.OptionalInt32("dialogue_variable1"),
                dialogueVariable2: row.OptionalInt32("dialogue_variable2"),
                completionVariable1: row.OptionalInt32("completion_variable1"),
                completionVariable2: row.OptionalInt32("completion_variable2"),
                completionActivateQuestId: AuthoritativeQuestIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "completion_activate_quest_id",
                    "completion_activate_quest_symbol"),
                activateOnRoute: row.OptionalBoolean("route_activation"),
                completionDiablo: row.OptionalBoolean("completion_diablo"),
                completionRewardItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "completion_reward_item_id",
                    "completion_reward_item_symbol"),
                completionRewardItemLevel: row.OptionalInt32("completion_reward_item_level"),
                turnInTownerId: row.OptionalInt32("turn_in_towner_id", -1),
                turnInLevelId: AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_level_id",
                    "turn_in_level_symbol"),
                turnInPositionX: row.OptionalInt32("turn_in_position_x"),
                turnInPositionY: row.OptionalInt32("turn_in_position_y"),
                turnInItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_item_id",
                    "turn_in_item_symbol"),
                turnInRequiredVariable2: row.OptionalInt32("turn_in_required_variable2", -1),
                turnInRewardUniqueId: AuthoritativeUniqueItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_reward_unique_id",
                    "turn_in_reward_unique_symbol"),
                turnInRewardItemLevel: row.OptionalInt32("turn_in_reward_item_level"),
                turnInRewardPositionX: row.OptionalInt32("turn_in_reward_position_x"),
                turnInRewardPositionY: row.OptionalInt32("turn_in_reward_position_y"),
                turnInMessageId: row.OptionalInt32("turn_in_message_id")) {
                StableSymbol = AuthoritativeQuestIdentity.ReadSymbol(row),
            };
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

    /** Validates content-owned towner turn-in items and unique rewards. */
    public static void ValidateTurnInRewards(
        IReadOnlyList<AuthoritativeQuestState> quests,
        AuthoritativeItemCatalog items,
        AuthoritativeUniqueItemCatalog uniqueItems)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(uniqueItems);
        foreach (var quest in quests.Where(quest => quest.TurnInTownerId >= 0)) {
            if (quest.TurnInItemId == 0
                || !items.TryGet(quest.TurnInItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in item {quest.TurnInItemId}.");
            if (quest.TurnInRewardUniqueId != 0
                && !uniqueItems.TryGet(quest.TurnInRewardUniqueId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in unique reward {quest.TurnInRewardUniqueId}.");
        }
    }

    /** Validates authored towner and reward positions against active geometry. */
    public static void ValidateTurnInPositions(
        IReadOnlyList<AuthoritativeQuestState> quests,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(world);
        foreach (var quest in quests.Where(quest => quest.TurnInTownerId >= 0)) {
            if (!world.IsWalkable(
                    quest.TurnInLevelId,
                    quest.TurnInPositionX,
                    quest.TurnInPositionY)
                || (quest.TurnInRewardUniqueId != 0
                    && !world.IsWalkable(
                        quest.TurnInLevelId,
                        quest.TurnInRewardPositionX,
                        quest.TurnInRewardPositionY)))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} contains a blocked towner or reward position.");
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

    /** Validates explicit monster identities used by quest completion triggers.
     * Native Diablo completion remains an implicit generated-boss target in the
     * base pack and therefore has no required monster_types.tsv identity. */
    public static void ValidateMonsterCompletionTargets(
        IEnumerable<AuthoritativeQuestState> quests,
        AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var quest in quests) {
            if (quest.CompletionMonsterId != 0
                && !monsterTypes.TryGet(quest.CompletionMonsterId, out _)) {
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown completion monster {quest.CompletionMonsterId}.");
            }
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
