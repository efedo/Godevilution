namespace Devilution.Diablo;

/// <summary>
/// The callback context used to classify a native-compatible player action start.
/// This is transient module metadata and is not part of player or snapshot state.
/// </summary>
internal enum DiabloCommandCallbackPhase
{
    OrdinaryCommandSlot,
    AttackOrRangedReadyRepeat,
    SpellReadmission,
    CompletionLoopStart,
}