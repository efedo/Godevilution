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
    /** Grants one level of the spell identified by effect_amount. */
    GrantSpellLevel,
    /** Spawns one validated server-owned spell projectile at the object position. */
    SpawnSpellProjectile,
    /** Grants one spell level and spawns that spell's validated projectile. */
    GrantSpellLevelAndSpawnProjectile,
}

/** Optional server-owned effect applied after a valid quest step or world mutation. */
public enum AuthoritativeQuestSequenceFollowUpKind
{
    None,
    WorldMutation,
    SpawnSpellProjectile,
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
