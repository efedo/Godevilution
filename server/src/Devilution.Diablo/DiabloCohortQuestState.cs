namespace Devilution.Diablo;

/**
 * Complete quest state for cohort persistence.  This is deliberately separate
 * from DiabloQuestState, which remains the legacy save/protocol projection.
 */
public sealed record DiabloCohortQuestState(
    uint QuestId,
    uint LevelId,
    uint RequiredProgress,
    uint Progress,
    bool Completed,
    bool Unavailable,
    bool Active,
    bool Logged,
    int Variable1,
    int Variable2,
    int MessageId,
    uint CompletionMonsterId,
    string CompletionUniqueIdentity,
    int DialogueVariable1,
    int DialogueVariable2,
    int DialogueCompletionVariable1,
    int CompletionVariable1,
    int CompletionVariable2,
    uint CompletionActivateQuestId,
    bool ActivateOnRoute,
    int RouteActivationVariable1,
    bool CompletionDiablo,
    uint CompletionRewardItemId,
    int CompletionRewardItemLevel,
    int TurnInTownerId,
    uint TurnInLevelId,
    int TurnInPositionX,
    int TurnInPositionY,
    uint TurnInItemId,
    int TurnInRequiredVariable2,
    uint TurnInRewardUniqueId,
    int TurnInRewardItemLevel,
    int TurnInRewardPositionX,
    int TurnInRewardPositionY,
    uint TurnInRewardItemId,
    int TurnInMessageId,
    bool TurnInActivationOnly,
    bool TurnInOnCompletion,
    int TurnInCompletionVariable2,
    int TurnInCompletionMessageId,
    uint TurnInCompletionRewardUniqueId,
    int TurnInCompletionRewardItemLevel,
    int TurnInCompletionRewardPositionX,
    int TurnInCompletionRewardPositionY,
    uint TurnInCompletionRewardItemId,
    uint TurnInAlternateItemId,
    int TurnInAlternateMessageId,
    int TurnInActivationVariable1,
    int TurnInCompletionRequiredVariable1,
    int TurnInCompletionVariable1,
    bool TurnInDialogueOnly,
    int TurnInRequiredVariable1,
    int TurnInVariable1,
    bool TurnInProgressOnly,
    uint TurnInBlockedVisitedLevel,
    uint DialogueItemId,
    uint DialogueRewardUniqueId,
    int DialogueRewardItemLevel,
    int DialogueRewardOffsetX,
    int DialogueRewardOffsetY,
    int DialogueCompletionMessageId,
    uint DialogueRewardItemId,
    bool DialogueRewardOnActivation,
    bool DialogueCompletionDefeatsTarget,
    string StableSymbol,
    uint? NativeQuestId)
{
    /** Rejects malformed persisted shape before any mutable quest is exposed. */
    public void ValidateShape()
    {
        if (QuestId == 0 || RequiredProgress == 0 || Progress > RequiredProgress)
            throw new InvalidDataException("A cohort quest contains invalid progress bounds.");
        if (CompletionUniqueIdentity is null || StableSymbol is null)
            throw new InvalidDataException("A cohort quest contains a null identity string.");
    }
}
