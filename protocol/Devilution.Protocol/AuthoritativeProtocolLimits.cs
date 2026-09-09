using System.Text;
using Devilution.Protocol.V1;
using Devilution.Shared;

namespace Devilution.Protocol;

/** Shared wire-level limits for server-owned text, identities, and bounded collections. */
public static class AuthoritativeProtocolLimits
{
    public const int MaximumEnvelopeBytes = 1024 * 1024;
    public const int MaximumDialogueTextBytes = 16 * 1024;
    public const int MaximumIdentityBytes = 128;
    public const int MaximumEventBatchCount = 4096;
    public const int MaximumWorldMutationCount = 4096;
    public const int MaximumSnapshotQuestCount = 4096;
    public const int MaximumSetLevelObjectOriginCount = 4096;
    public const int MaximumMaterializedProceduralLevelCount = 4096;
    public const int NativeMaximumPlayerCount = 4;
    public const int NativeMaximumInventoryCount = NativeCompatibilityLimits.MaximumInventoryCount;
    public const int NativeMaximumEquipmentCount = NativeCompatibilityLimits.MaximumEquipmentCount;
    public const int NativeMaximumBeltCount = NativeCompatibilityLimits.MaximumBeltCount;
    public const int NativeMaximumInventoryGridCount = 40;
    public const int NativeInventoryWidth = 10;
    public const int NativeInventoryHeight = 4;
    public const int NativeStashPageCount = NativeCompatibilityLimits.StashPageCount;
    public const int NativeStashCellCount = NativeCompatibilityLimits.StashCellCount;
    public const int NativeStashGridWidth = 10;
    public const int NativeStashGridHeight = 10;
    public const int NativeMaximumMonsterCount = 200;
    public const int NativeMaximumObjectCount = 127;
    public const int NativeMaximumItemCount = 127;
    public const int NativeMaximumWarpTriggerCount = 7;
    public const int NativeMaximumPortalCount = 4;
    public const int NativeMaximumWidth = 112;
    public const int NativeMaximumHeight = 112;
    public const int NativeMaximumMegaWidth = 40;
    public const int NativeMaximumMegaHeight = 40;
    public const uint NativeMaximumDunObjectLayerId = 146;
    public const uint NativeMaximumDunPieceId = 1378;
    public const uint NativeMaximumObjectId = 108;
    public const uint NativeMaximumQuestId = 23;
    public const int MaximumNativeVisitedLevelCount = 256;
    public const uint MaximumNativeMonsterExperienceReward = NativeCompatibilityLimits.MaximumNativeMonsterExperienceReward;
    public const int MaximumNativeMonsterToHit = NativeCompatibilityLimits.MaximumNativeMonsterToHit;
    public const int MaximumNativeMonsterIntelligence = NativeCompatibilityLimits.MaximumNativeMonsterIntelligence;
    public const int MaximumNativeMonsterDirection = NativeCompatibilityLimits.MaximumNativeMonsterDirection;
    public const int MaximumNativeMonsterPackSize = NativeCompatibilityLimits.MaximumNativeMonsterPackSize;
    public const int MaximumNativeMonsterActiveTicks = NativeCompatibilityLimits.MaximumNativeMonsterActiveTicks;
    public const int MaximumNativeMonsterMovementTicks = NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks;
    public const int MinimumNativeMonsterMovementMode = NativeCompatibilityLimits.MinimumNativeMonsterMovementMode;
    public const int MaximumNativeMonsterMovementMode = NativeCompatibilityLimits.MaximumNativeMonsterMovementMode;
    public const int MinimumNativeMonsterMovementTurn = NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn;
    public const int MaximumNativeMonsterMovementTurn = NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn;
    public const int MaximumNativeMonsterFallenStandAnimationFrame = NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationFrame;
    public const int MaximumNativeMonsterFallenStandAnimationTick = NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationTick;
    public const int MaximumNativeMonsterFallenRallyTicks = NativeCompatibilityLimits.MaximumNativeMonsterFallenRallyTicks;
    public const int MinimumNativeMonsterResistance = NativeCompatibilityLimits.MinimumNativeMonsterResistance;
    public const int MaximumNativeMonsterResistance = NativeCompatibilityLimits.MaximumNativeMonsterResistance;
    public const int MaximumNativeQuestByte = byte.MaxValue;
    public const int MinimumNativeOptionalQuestByte = -1;
    public const int MinimumNativeRewardOffset = -16;
    public const int MaximumNativeRewardOffset = 16;
    public const int MinimumNativeObjectOffset = -32;
    public const int MaximumNativeObjectOffset = 32;
    public const int MinimumNativeDirection = -1;
    public const int MaximumNativeDirection = 1;
    public const uint MaximumStableSpellId = NativeCompatibilityLimits.MaximumStableSpellId;
    public const uint MaximumNativeSpellId = NativeCompatibilityLimits.MaximumNativeSpellId;
    public const uint MaximumNativeSpellLevel = NativeCompatibilityLimits.MaximumNativeSpellLevel;
    public const int MaximumMemorizedSpellCount = NativeCompatibilityLimits.MaximumMemorizedSpellCount;
    public const uint MaximumNativeCharacterLevel = NativeCompatibilityLimits.MaximumNativeCharacterLevel;
    public const uint MaximumNativeDiabloKillLevel = NativeCompatibilityLimits.MaximumNativeDiabloKillLevel;
    public const uint MaximumNativeReflectCharges = NativeCompatibilityLimits.MaximumNativeReflectCharges;
    public const int MaximumNativeMonsterAiTimerTicks = NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks;
    public const int MaximumNativeMonsterDeathAnimationTicks = NativeCompatibilityLimits.MaximumNativeMonsterDeathAnimationTicks;
    public const int MaximumNativeMonsterShortTimerTicks = NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks;
    public const int MaximumNativeMonsterRetreatTicks = NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks;
    public const int MinimumNativeLightRadius = 2;
    public const int MaximumNativeLightRadius = 15;
    public const int MaximumQuestTownerInteractionCount = 1024;
    public const int MaximumTownerGreetingCount = 256;
    public const int MaximumStatusEffectCount = 128;
    public const int MaximumNativeSetLevelId = 8;
    public const int MaximumMonsterLeaderRelation = 2;
    public const int MaximumMonsterDialogueState =
        (int)MonsterDialogueState.Combat;
    public const int MaximumProjectileDamageType = 4;
    public const int MaximumProjectilePhase = 1;
    public const int MaximumProjectileBehavior =
        (int)AuthoritativeProjectileBehavior.MonsterArrow;
    public const int MaximumProjectilePresentationKind =
        (int)ProjectilePresentationKind.NovaBall;
    public const int MaximumNativeProjectileVisual =
        (int)NativeProjectileVisual.MonsterMagmaBall;
    public const int MaximumStatusEffectBehavior =
        (int)StatusEffectBehavior.Etherealize;
    public const int MaximumItemPresentationKind =
        (int)ItemPresentationKind.Ear;
    public const int MaximumItemSpellSourceKind =
        (int)ItemSpellSourceKind.ConsumableItem;
    public const int MaximumItemActionKind =
        (int)ItemActionKind.ReadQuestNote;
    public const int MaximumSpellTargetingKind =
        (int)SpellTargetingKind.WorldEntity;
    public const int MaximumObjectPresentationKind =
        (int)ObjectPresentationKind.Trap;
    public const int MaximumObjectEffectKind = 9;
    public const int MaximumObjectQuestTransitionKind = 8;
    public const int MaximumObjectRewardKind = 2;
    public const int MaximumObjectRewardOriginKind = 1;
    public const int MaximumObjectQuestSequenceFollowUpKind = 3;
    public const int MaximumObjectProjectileSourceKind = 1;
    public const int MaximumQuestTownerInteractionKind = 4;

    public static bool IsDialogueTextValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return !value.Contains('\0')
            && !value.Contains('\r')
            && Encoding.UTF8.GetByteCount(value) <= MaximumDialogueTextBytes;
    }

    public static bool IsIdentityWithinLimit(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetByteCount(value) <= MaximumIdentityBytes;
    }
}
