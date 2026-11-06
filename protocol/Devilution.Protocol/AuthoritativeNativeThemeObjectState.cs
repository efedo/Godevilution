using Devilution.Protocol.V1;

namespace Devilution.Protocol;

/** Admission for ordinary item objects and the shared native theme-effect capability. */
public static class AuthoritativeNativeThemeObjectState
{
    public static bool IsInteractiveType(uint type)
        => type is 61 or 62 or 63 or 64 or 66 or 67 or 76 or 77 or 79 or 80 or 81 or 82 or 89 or 90 or 92;

    public static bool IsValidProjection(ObjectSnapshot value)
        => !value.NativeThemeObjectInteractable || IsValidInteraction(value);

    public static bool IsValidInteraction(ObjectSnapshot value)
        => value.NativeThemeObjectInteractable && IsInteractiveType(value.NativeObjectId)
            && value.LevelId != 0 && !value.Activated && !value.Destroyed && !value.TrapArmed
            && !value.Breakable && !value.IsShrine && value.NativeShrineType == 0
            && !value.NativeShrineInteractable && !value.NativeChestInteractable && !value.NativeTrapInteractable
            && !value.NativeSarcophagusInteractable && !value.NativeBarrelMeleeBreakable
            && value.CathedralDoor is null && !value.ActivateOnOccupancy
            && value.EffectKind == 0 && value.EffectAmount == 0 && value.QuestId == 0
            && value.RewardKind == 0 && value.QuestTransitionKind == 0
            && value.QuestSequenceFollowUpKind == 0 && value.DialogueMessageId == 0;
}
