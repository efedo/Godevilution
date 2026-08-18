namespace Devilution.Diablo;

public enum AuthoritativeObjectEffectKind
{
    None,
    Heal,
    Damage,
    Experience,
    /** Advances a content-owned ordered quest interaction sequence. */
    QuestSequence,
    /** Consumes a required normalized item and completes the linked quest. */
    QuestItemCompletion,
    /** Applies one validated level-cell overlay when the object is activated. */
    WorldMutation,
}

/** Native quest-state transition applied when an authored object is activated. */
public enum AuthoritativeObjectQuestTransitionKind
{
    None,
    ActivateQuest,
    CompleteQuest,
    SetVariable1,
    SetVariable2,
    ActivateAndSetVariable1,
    ActivateAndSetVariable2,
    CompleteAndSetVariable1,
    CompleteAndSetVariable2,
}

/** Authored item reward generated when an object is activated. */
public enum AuthoritativeObjectRewardKind
{
    None,
    NormalItem,
    UniqueItem,
}
