using Devilution.Protocol.V1;

namespace Devilution.Protocol;

/** Shared admission for a server-declared, unused ordinary vanilla shrine. */
public static class AuthoritativeNativeShrineState
{
    public static bool IsValidProjection(ObjectSnapshot value)
        => value.NativeShrineType <= 25
            && (value.NativeShrineType == 0 || value.NativeObjectId is 59 or 60 && value.IsShrine)
            && (!value.NativeShrineInteractable || IsValidInteraction(value));

    public static bool IsValidInteraction(ObjectSnapshot value)
        => value.NativeShrineInteractable && value.NativeObjectId is 59 or 60 && value.IsShrine
            && value.LevelId != 0 && !value.Activated && !value.Destroyed && !value.TrapArmed
            && !value.Breakable && !value.NativeChestInteractable && !value.NativeTrapInteractable
            && !value.NativeSarcophagusInteractable && !value.NativeBarrelMeleeBreakable
            && value.CathedralDoor is null && !value.ActivateOnOccupancy
            && value.EffectKind == 0 && value.EffectAmount == 0 && value.QuestId == 0
            && value.RewardKind == 0 && value.QuestTransitionKind == 0
            && value.QuestSequenceFollowUpKind == 0 && value.DialogueMessageId == 0;
}
