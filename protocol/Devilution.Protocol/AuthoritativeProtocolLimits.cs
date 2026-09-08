using System.Text;
using Devilution.Protocol.V1;

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
    public const int MaximumNativeMonsterDeathAnimationTicks = 1024;
    public const uint MaximumNativeMonsterExperienceReward = 1_000_000;
    public const int MaximumNativeMonsterToHit = 255;
    public const int MaximumNativeMonsterIntelligence = 255;
    public const int MaximumNativeMonsterDirection = 7;
    public const int MaximumNativeMonsterPackSize = 255;
    public const int MaximumNativeMonsterActiveTicks = 255;
    public const uint MaximumStableSpellId = byte.MaxValue;
    public const uint MaximumNativeSpellLevel = 15;
    public const int MaximumMemorizedSpellCount = 51;
    public const uint MaximumNativeCharacterLevel = 50;
    public const uint MaximumNativeDiabloKillLevel = byte.MaxValue;
    public const uint MaximumNativeReflectCharges = ushort.MaxValue;
    public const int MinimumNativeLightRadius = 2;
    public const int MaximumNativeLightRadius = 15;
    public const int MaximumQuestTownerInteractionCount = 1024;
    public const int MaximumTownerGreetingCount = 256;
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
