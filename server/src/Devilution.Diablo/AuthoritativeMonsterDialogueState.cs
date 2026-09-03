namespace Devilution.Diablo;

/** Server-owned compatibility state for an authored unique-monster dialogue route. */
public enum AuthoritativeMonsterDialogueState
{
    /** No authored dialogue state is available; preserve legacy client behavior. */
    Unspecified,
    /** The encounter is waiting for the next authored conversation step. */
    Inquiring,
    /** The authored conversation is complete and normal combat may resume. */
    Combat,
}
