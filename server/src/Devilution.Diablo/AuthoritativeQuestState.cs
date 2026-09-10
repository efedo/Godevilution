using Devilution.Data;
using Devilution.Domain.Protocol;

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
        int turnInMessageId = 0,
        uint turnInRewardItemId = 0,
        uint dialogueItemId = 0,
        uint dialogueRewardUniqueId = 0,
        int dialogueRewardItemLevel = 0,
        int dialogueRewardOffsetX = 0,
        int dialogueRewardOffsetY = 0,
        int dialogueCompletionMessageId = 0,
        uint dialogueRewardItemId = 0,
        bool dialogueRewardOnActivation = false,
        bool turnInActivationOnly = false,
        bool turnInOnCompletion = false,
        int turnInCompletionVariable2 = -1,
        int turnInCompletionMessageId = 0,
        uint turnInCompletionRewardUniqueId = 0,
        int turnInCompletionRewardItemLevel = 0,
        int turnInCompletionRewardPositionX = 0,
        int turnInCompletionRewardPositionY = 0,
        uint turnInCompletionRewardItemId = 0,
        uint turnInAlternateItemId = 0,
        int turnInAlternateMessageId = 0,
        int turnInActivationVariable1 = -1,
        int routeActivationVariable1 = -1,
        int turnInCompletionRequiredVariable1 = -1,
        int turnInCompletionVariable1 = -1,
        bool turnInDialogueOnly = false,
        int turnInRequiredVariable1 = -1,
        int turnInVariable1 = -1,
        bool turnInProgressOnly = false,
        int dialogueCompletionVariable1 = -1,
        uint turnInBlockedVisitedLevel = 0,
        bool dialogueCompletionDefeatsTarget = false)
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
        if (turnInCompletionVariable2 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInCompletionVariable2));
        if (turnInCompletionMessageId < 0 || turnInCompletionMessageId > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInCompletionMessageId));
        if (turnInCompletionRewardItemLevel is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(turnInCompletionRewardItemLevel));
        if (turnInAlternateMessageId < 0 || turnInAlternateMessageId > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInAlternateMessageId));
        if (turnInActivationVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInActivationVariable1));
        if (routeActivationVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(routeActivationVariable1));
        if (turnInCompletionRequiredVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInCompletionRequiredVariable1));
        if (turnInCompletionVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInCompletionVariable1));
        if (turnInRequiredVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInRequiredVariable1));
        if (turnInVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInVariable1));
        if (dialogueCompletionVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueCompletionVariable1));
        if (turnInBlockedVisitedLevel > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(turnInBlockedVisitedLevel));
        if (dialogueRewardItemLevel is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(dialogueRewardItemLevel));
        if (dialogueCompletionMessageId < 0 || dialogueCompletionMessageId > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueCompletionMessageId));
        if (dialogueCompletionDefeatsTarget
            && (dialogueItemId == 0 || string.IsNullOrWhiteSpace(completionUniqueIdentity)))
            throw new ArgumentException(
                "A dialogue completion defeat requires an item hand-in and unique encounter identity.",
                nameof(dialogueCompletionDefeatsTarget));
        if (!NativeTextLimits.IsIdentityWithinLimit(completionUniqueIdentity))
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
        RouteActivationVariable1 = routeActivationVariable1;
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
        TurnInRewardItemId = turnInRewardItemId;
        DialogueItemId = dialogueItemId;
        DialogueRewardUniqueId = dialogueRewardUniqueId;
        DialogueRewardItemLevel = dialogueRewardItemLevel;
        DialogueRewardOffsetX = dialogueRewardOffsetX;
        DialogueRewardOffsetY = dialogueRewardOffsetY;
        DialogueCompletionMessageId = dialogueCompletionMessageId;
        DialogueRewardItemId = dialogueRewardItemId;
        DialogueRewardOnActivation = dialogueRewardOnActivation;
        TurnInActivationOnly = turnInActivationOnly;
        TurnInOnCompletion = turnInOnCompletion;
        TurnInCompletionVariable2 = turnInCompletionVariable2;
        TurnInCompletionMessageId = turnInCompletionMessageId;
        TurnInCompletionRewardUniqueId = turnInCompletionRewardUniqueId;
        TurnInCompletionRewardItemLevel = turnInCompletionRewardItemLevel;
        TurnInCompletionRewardPositionX = turnInCompletionRewardPositionX;
        TurnInCompletionRewardPositionY = turnInCompletionRewardPositionY;
        TurnInCompletionRewardItemId = turnInCompletionRewardItemId;
        TurnInAlternateItemId = turnInAlternateItemId;
        TurnInAlternateMessageId = turnInAlternateMessageId;
        TurnInActivationVariable1 = turnInActivationVariable1;
        TurnInCompletionRequiredVariable1 = turnInCompletionRequiredVariable1;
        TurnInCompletionVariable1 = turnInCompletionVariable1;
        TurnInDialogueOnly = turnInDialogueOnly;
        TurnInRequiredVariable1 = turnInRequiredVariable1;
        TurnInVariable1 = turnInVariable1;
        TurnInProgressOnly = turnInProgressOnly;
        DialogueCompletionVariable1 = dialogueCompletionVariable1;
        TurnInBlockedVisitedLevel = turnInBlockedVisitedLevel;
        DialogueCompletionDefeatsTarget = dialogueCompletionDefeatsTarget;
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
    /** Optional native _qvar1 marker written by a subsequent quest conversation. */
    public int DialogueCompletionVariable1 { get; }
    /** Content-owned minimum native _qvar1 after quest completion. */
    public int CompletionVariable1 { get; }
    /** Content-owned minimum native _qvar2 after quest completion. */
    public int CompletionVariable2 { get; }
    /** Content-owned quest identity activated when this quest completes. */
    public uint CompletionActivateQuestId { get; }
    /** Whether native route-level entry may activate this quest automatically. */
    public bool ActivateOnRoute { get; }
    /** Optional native _qvar1 marker written when route-level entry activates the quest. */
    public int RouteActivationVariable1 { get; }
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
    /** Optional normal item generated at the native towner position. */
    public uint TurnInRewardItemId { get; }
    /** Optional native dialogue identity emitted after a successful turn-in. */
    public int TurnInMessageId { get; }
    /** Whether the towner interaction activates the quest and creates its initial item instead of completing it. */
    public bool TurnInActivationOnly { get; }
    /** Whether the towner has a one-shot interaction after this quest completes. */
    public bool TurnInOnCompletion { get; }
    /** Native _qvar2 marker written after the completion-stage interaction. */
    public int TurnInCompletionVariable2 { get; }
    /** Optional dialogue identity emitted after the completion-stage interaction. */
    public int TurnInCompletionMessageId { get; }
    /** Optional unique reward generated after the completion-stage interaction. */
    public uint TurnInCompletionRewardUniqueId { get; }
    public int TurnInCompletionRewardItemLevel { get; }
    public int TurnInCompletionRewardPositionX { get; }
    public int TurnInCompletionRewardPositionY { get; }
    /** Optional normal item generated after the completion-stage interaction. */
    public uint TurnInCompletionRewardItemId { get; }
    /** Optional alternate item consumed by the same towner without changing quest state. */
    public uint TurnInAlternateItemId { get; }
    /** Dialogue identity emitted after consuming the alternate towner item. */
    public int TurnInAlternateMessageId { get; }
    /** Optional native _qvar1 marker written by an activation-only towner interaction. */
    public int TurnInActivationVariable1 { get; }
    /** Optional native _qvar1 required before a completion-stage towner interaction. */
    public int TurnInCompletionRequiredVariable1 { get; }
    /** Optional native _qvar1 marker written by a completion-stage towner interaction. */
    public int TurnInCompletionVariable1 { get; }
    /** Whether the towner emits one item-gated dialogue without consuming or changing quest state. */
    public bool TurnInDialogueOnly { get; }
    /** Optional native _qvar1 gate for an active staged item hand-in. */
    public int TurnInRequiredVariable1 { get; }
    /** Optional native _qvar1 marker written by an active staged item hand-in. */
    public int TurnInVariable1 { get; }
    /** Whether the turn-in consumes its item and reward but leaves the quest active. */
    public bool TurnInProgressOnly { get; }
    /** Optional native level visitation marker that suppresses this towner turn-in. */
    public uint TurnInBlockedVisitedLevel { get; }
    /** Optional item consumed by a unique-monster quest conversation. */
    public uint DialogueItemId { get; }
    /** Optional unique item generated relative to the unique-monster target. */
    public uint DialogueRewardUniqueId { get; }
    public int DialogueRewardItemLevel { get; }
    public int DialogueRewardOffsetX { get; }
    public int DialogueRewardOffsetY { get; }
    /** Optional native dialogue identity emitted after a dialogue hand-in. */
    public int DialogueCompletionMessageId { get; }
    /** Optional normal item generated relative to the unique-monster target. */
    public uint DialogueRewardItemId { get; }
    /** Whether the authored dialogue reward is generated on first conversation rather than hand-in. */
    public bool DialogueRewardOnActivation { get; }
    /** Whether a successful item-gated dialogue completion defeats its unique target. */
    public bool DialogueCompletionDefeatsTarget { get; }
}

/** External quest definitions and initial progress. */
public static class AuthoritativeQuestCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "quest_id",
        "quest_symbol",
        "level_id",
        "level_symbol",
        "required_progress",
        "progress",
        "completed",
        "unavailable",
        "initial",
        "logged",
        "variable1",
        "variable2",
        "message_id",
        "completion_monster_id",
        "completion_monster_symbol",
        "completion_unique_identity",
        "dialogue_variable1",
        "dialogue_variable2",
        "completion_variable1",
        "completion_variable2",
        "completion_activate_quest_id",
        "completion_activate_quest_symbol",
        "route_activation",
        "completion_diablo",
        "completion_reward_item_id",
        "completion_reward_item_symbol",
        "completion_reward_item_level",
        "turn_in_towner_id",
        "turn_in_level_id",
        "turn_in_level_symbol",
        "turn_in_position_x",
        "turn_in_position_y",
        "turn_in_item_id",
        "turn_in_item_symbol",
        "turn_in_required_variable2",
        "turn_in_reward_unique_id",
        "turn_in_reward_unique_symbol",
        "turn_in_reward_item_level",
        "turn_in_reward_position_x",
        "turn_in_reward_position_y",
        "turn_in_message_id",
        "turn_in_reward_item_id",
        "turn_in_reward_item_symbol",
        "dialogue_item_id",
        "dialogue_item_symbol",
        "dialogue_reward_unique_id",
        "dialogue_reward_unique_symbol",
        "dialogue_reward_item_level",
        "dialogue_reward_offset_x",
        "dialogue_reward_offset_y",
        "dialogue_completion_message_id",
        "dialogue_reward_item_id",
        "dialogue_reward_item_symbol",
        "dialogue_reward_on_activation",
        "turn_in_activation_only",
        "turn_in_on_completion",
        "turn_in_completion_variable2",
        "turn_in_completion_message_id",
        "turn_in_completion_reward_unique_id",
        "turn_in_completion_reward_unique_symbol",
        "turn_in_completion_reward_item_level",
        "turn_in_completion_reward_position_x",
        "turn_in_completion_reward_position_y",
        "turn_in_completion_reward_item_id",
        "turn_in_completion_reward_item_symbol",
        "turn_in_alternate_item_id",
        "turn_in_alternate_item_symbol",
        "turn_in_alternate_message_id",
        "turn_in_activation_variable1",
        "turn_in_required_variable1",
        "turn_in_variable1",
        "turn_in_progress_only",
    ];

    /** Rejects legacy quest-table headers at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("quest_id");
            _ = row.RequiredUInt32("required_progress");
        }
    }

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
                routeActivationVariable1: row.OptionalInt32("route_activation_variable1", -1),
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
                turnInMessageId: row.OptionalInt32("turn_in_message_id"),
                turnInRewardItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_reward_item_id",
                    "turn_in_reward_item_symbol"),
                dialogueItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "dialogue_item_id",
                    "dialogue_item_symbol"),
                dialogueRewardUniqueId: AuthoritativeUniqueItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "dialogue_reward_unique_id",
                    "dialogue_reward_unique_symbol"),
                dialogueRewardItemLevel: row.OptionalInt32("dialogue_reward_item_level"),
                dialogueRewardOffsetX: row.OptionalInt32("dialogue_reward_offset_x"),
                dialogueRewardOffsetY: row.OptionalInt32("dialogue_reward_offset_y"),
                dialogueCompletionMessageId: row.OptionalInt32("dialogue_completion_message_id"),
                dialogueRewardItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "dialogue_reward_item_id",
                    "dialogue_reward_item_symbol"),
                dialogueRewardOnActivation: row.OptionalBoolean("dialogue_reward_on_activation"),
                turnInActivationOnly: row.OptionalBoolean("turn_in_activation_only"),
                turnInOnCompletion: row.OptionalBoolean("turn_in_on_completion"),
                turnInCompletionVariable2: row.OptionalInt32("turn_in_completion_variable2", -1),
                turnInCompletionMessageId: row.OptionalInt32("turn_in_completion_message_id"),
                turnInCompletionRewardUniqueId: AuthoritativeUniqueItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_completion_reward_unique_id",
                    "turn_in_completion_reward_unique_symbol"),
                turnInCompletionRewardItemLevel: row.OptionalInt32("turn_in_completion_reward_item_level"),
                turnInCompletionRewardPositionX: row.OptionalInt32("turn_in_completion_reward_position_x"),
                turnInCompletionRewardPositionY: row.OptionalInt32("turn_in_completion_reward_position_y"),
                turnInCompletionRewardItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_completion_reward_item_id",
                    "turn_in_completion_reward_item_symbol"),
                turnInAlternateItemId: AuthoritativeItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "turn_in_alternate_item_id",
                    "turn_in_alternate_item_symbol"),
                turnInAlternateMessageId: row.OptionalInt32("turn_in_alternate_message_id"),
                turnInActivationVariable1: row.OptionalInt32("turn_in_activation_variable1", -1),
                turnInCompletionRequiredVariable1: row.OptionalInt32(
                    "turn_in_completion_required_variable1",
                    -1),
                turnInCompletionVariable1: row.OptionalInt32(
                    "turn_in_completion_variable1",
                    -1),
                turnInDialogueOnly: row.OptionalBoolean("turn_in_dialogue_only"),
                turnInRequiredVariable1: row.OptionalInt32(
                    "turn_in_required_variable1",
                    -1),
                turnInVariable1: row.OptionalInt32("turn_in_variable1", -1),
                turnInProgressOnly: row.OptionalBoolean("turn_in_progress_only"),
                dialogueCompletionVariable1: row.OptionalInt32(
                    "dialogue_completion_variable1",
                    -1),
                turnInBlockedVisitedLevel: row.OptionalUInt32(
                    "turn_in_blocked_visited_level"),
                dialogueCompletionDefeatsTarget: row.OptionalBoolean(
                    "dialogue_completion_defeats_target")) {
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
            if (!quest.TurnInActivationOnly
                && quest.TurnInActivationVariable1 >= 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares an activation variable without enabling an activation-only turn-in.");
            if (quest.TurnInActivationOnly
                && quest.TurnInRewardUniqueId != 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} activation-only turn-in cannot generate a unique reward.");
            if (quest.TurnInActivationOnly && quest.ActivateOnRoute)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} activation-only turn-in cannot activate on route entry.");
            if (quest.TurnInAlternateItemId != 0
                && quest.TurnInActivationOnly)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} activation-only turn-in cannot declare an alternate item.");
            if (quest.TurnInDialogueOnly
                && (quest.TurnInActivationOnly || quest.TurnInOnCompletion))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue-only turn-in cannot be activation-only or completion-stage.");
            if (quest.TurnInDialogueOnly
                && quest.TurnInMessageId == 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue-only turn-in requires a dialogue message.");
            if (quest.TurnInDialogueOnly
                && (quest.TurnInRewardUniqueId != 0
                    || quest.TurnInRewardItemId != 0
                    || quest.TurnInAlternateItemId != 0
                    || quest.TurnInRequiredVariable2 >= 0
                    || quest.TurnInRequiredVariable1 >= 0
                    || quest.TurnInVariable1 >= 0
                    || quest.TurnInProgressOnly))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue-only turn-in cannot declare rewards, alternate items, or variable gates.");
            if (quest.TurnInBlockedVisitedLevel != 0 && !quest.TurnInDialogueOnly)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares a blocked visited level without a dialogue-only turn-in.");
            if (quest.TurnInProgressOnly
                && (quest.TurnInActivationOnly
                    || quest.TurnInOnCompletion
                    || quest.TurnInDialogueOnly))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} staged turn-in cannot be activation-only, completion-stage, or dialogue-only.");
            if (quest.TurnInProgressOnly
                && (quest.TurnInRequiredVariable1 < 0
                    || quest.TurnInVariable1 < 0
                    || quest.TurnInItemId == 0))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} staged turn-in requires an item and variable-1 gate/marker.");
            if (!quest.TurnInProgressOnly
                && (quest.TurnInRequiredVariable1 >= 0 || quest.TurnInVariable1 >= 0))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares staged variable-1 fields without enabling a staged turn-in.");
            if (quest.TurnInAlternateItemId != 0
                && !items.TryGet(quest.TurnInAlternateItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown alternate turn-in item {quest.TurnInAlternateItemId}.");
            if (quest.TurnInAlternateItemId != 0
                && quest.TurnInAlternateItemId == quest.TurnInItemId)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} cannot use the same item for primary and alternate turn-in.");
            if (quest.TurnInAlternateItemId != 0
                && quest.TurnInAlternateMessageId == 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} alternate turn-in requires a dialogue message.");
            if (quest.TurnInAlternateItemId == 0
                && quest.TurnInAlternateMessageId != 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares an alternate turn-in message without an item.");
            if (quest.TurnInItemId != 0
                && !items.TryGet(quest.TurnInItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in item {quest.TurnInItemId}.");
            if (!quest.TurnInActivationOnly
                && !quest.TurnInOnCompletion
                && !quest.TurnInDialogueOnly
                && quest.TurnInItemId == 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in item {quest.TurnInItemId}.");
            if (quest.TurnInRewardUniqueId != 0
                && !uniqueItems.TryGet(quest.TurnInRewardUniqueId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in unique reward {quest.TurnInRewardUniqueId}.");
            if (quest.TurnInRewardItemId != 0
                && !items.TryGet(quest.TurnInRewardItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown turn-in item reward {quest.TurnInRewardItemId}.");
            if (quest.TurnInRewardUniqueId != 0 && quest.TurnInRewardItemId != 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares both a turn-in unique reward and item reward.");
            if (quest.TurnInOnCompletion) {
                if (quest.TurnInCompletionVariable2 < 0
                    && (quest.TurnInCompletionRequiredVariable1 < 0
                        || quest.TurnInCompletionVariable1 < 0))
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} completion-stage turn-in requires a variable-2 marker or a variable-1 marker.");
                if (quest.TurnInCompletionRequiredVariable1 >= 0
                    && quest.TurnInCompletionVariable1 < 0)
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} completion-stage variable-1 gate requires a variable-1 marker.");
                if (quest.TurnInCompletionRewardUniqueId == 0
                    && quest.TurnInCompletionRewardItemId == 0
                    && quest.TurnInCompletionMessageId == 0
                    && quest.TurnInCompletionVariable1 < 0
                    && quest.CompletionActivateQuestId == 0)
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} completion-stage turn-in requires a reward, dialogue, variable, or follow-on activation.");
                if (quest.TurnInCompletionRewardUniqueId != 0
                    && !uniqueItems.TryGet(quest.TurnInCompletionRewardUniqueId, out _))
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} references unknown completion-stage unique reward {quest.TurnInCompletionRewardUniqueId}.");
                if (quest.TurnInCompletionRewardItemId != 0
                    && !items.TryGet(quest.TurnInCompletionRewardItemId, out _))
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} references unknown completion-stage item reward {quest.TurnInCompletionRewardItemId}.");
                if (quest.TurnInCompletionRewardUniqueId != 0
                    && quest.TurnInCompletionRewardItemId != 0)
                    throw new InvalidDataException(
                        $"Quest {quest.QuestId} declares both a completion-stage unique reward and item reward.");
            } else if (quest.TurnInCompletionRequiredVariable1 >= 0
                       || quest.TurnInCompletionVariable1 >= 0
                       || quest.TurnInCompletionVariable2 >= 0
                       || quest.TurnInCompletionRewardUniqueId != 0
                       || quest.TurnInCompletionRewardItemId != 0) {
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares completion-stage turn-in fields without enabling the stage.");
            }
        }
    }

    /** Validates content-owned unique-monster dialogue hand-ins and rewards. */
    public static void ValidateDialogueRewards(
        IReadOnlyList<AuthoritativeQuestState> quests,
        AuthoritativeItemCatalog items,
        AuthoritativeUniqueItemCatalog uniqueItems)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(uniqueItems);
        foreach (var quest in quests.Where(quest => quest.DialogueItemId != 0
                                                    || quest.DialogueRewardUniqueId != 0
                                                    || quest.DialogueRewardItemId != 0
                                                    || quest.DialogueRewardOnActivation
                                                    || quest.DialogueCompletionDefeatsTarget)) {
            if (!quest.DialogueRewardOnActivation
                && (quest.DialogueItemId == 0
                    || !items.TryGet(quest.DialogueItemId, out _)))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown dialogue item {quest.DialogueItemId}.");
            if (quest.DialogueRewardOnActivation && quest.DialogueItemId != 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} cannot combine an activation dialogue reward with an item hand-in.");
            if (string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue hand-in requires a unique-monster identity.");
            if (quest.DialogueRewardUniqueId != 0
                && !uniqueItems.TryGet(quest.DialogueRewardUniqueId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown dialogue unique reward {quest.DialogueRewardUniqueId}.");
            if (quest.DialogueRewardItemId != 0
                && !items.TryGet(quest.DialogueRewardItemId, out _))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} references unknown dialogue item reward {quest.DialogueRewardItemId}.");
            if (quest.DialogueRewardUniqueId != 0 && quest.DialogueRewardItemId != 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares both a dialogue unique reward and item reward.");
            if (quest.DialogueRewardOnActivation
                && quest.DialogueRewardUniqueId == 0
                && quest.DialogueRewardItemId == 0)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} enables an activation dialogue reward without a reward item.");
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
                || ((quest.TurnInRewardUniqueId != 0 || quest.TurnInRewardItemId != 0)
                    && !world.IsWalkable(
                        quest.TurnInLevelId,
                        quest.TurnInRewardPositionX,
                        quest.TurnInRewardPositionY))
                || (quest.TurnInOnCompletion
                    && (quest.TurnInCompletionRewardUniqueId != 0
                        || quest.TurnInCompletionRewardItemId != 0)
                    && !world.IsWalkable(
                        quest.TurnInLevelId,
                        quest.TurnInCompletionRewardPositionX,
                        quest.TurnInCompletionRewardPositionY)))
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
            if (quest.CompletionActivateQuestId == quest.QuestId)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} cannot activate itself on completion.");
            if (!questIds.Contains(quest.CompletionActivateQuestId))
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} activates unknown quest {quest.CompletionActivateQuestId}.");
        }
        foreach (var quest in values.Where(quest => quest.RouteActivationVariable1 >= 0)) {
            if (!quest.ActivateOnRoute)
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} declares a route activation variable without enabling route activation.");
        }
        foreach (var quest in values.Where(quest => quest.DialogueCompletionVariable1 >= 0)) {
            if (string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity)
                || quest.DialogueVariable1 == 0
                || quest.DialogueCompletionVariable1 <= quest.DialogueVariable1) {
                throw new InvalidDataException(
                    $"Quest {quest.QuestId} dialogue completion requires a unique identity and a strictly increasing dialogue variable-1 transition.");
            }
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
