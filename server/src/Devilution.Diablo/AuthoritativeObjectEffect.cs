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
