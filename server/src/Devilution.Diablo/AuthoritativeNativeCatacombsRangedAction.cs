using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/**
 * Native C2 ranged animation waiting to release its bound missile.
 * EffectFrame is zero-based source metadata; ActionFrameCount is the graphic length.
 */
public sealed record AuthoritativeNativeCatacombsRangedAction(
    int NativeSlot,
    uint EntityId,
    uint ContentMonsterId,
    uint LevelId,
    int NativeTypeId,
    string NativeAi,
    string MetadataFingerprint,
    string PopulationFingerprint,
    string MissileMappingFingerprint,
    string MissileId,
    AuthoritativeProjectileBehavior ProjectileBehavior,
    AuthoritativeMonsterRangedAttackMode AttackMode,
    uint SpellId,
    uint TargetEntityId,
    int TargetX,
    int TargetY,
    bool UsesSpecialAttack,
    uint AttackSeed,
    int ActionFrameCount,
    int TicksPerFrame,
    int EffectFrame,
    int FrameIndex,
    int TickCounter)
{
    internal static AuthoritativeNativeCatacombsRangedAction Create(
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile,
        uint targetEntityId,
        int targetX,
        int targetY,
        uint attackSeed,
        string populationFingerprint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(missile);
        var initialization = target.NativeInitialization
            ?? throw new InvalidDataException("A Catacombs ranged action requires retained native initialization.");
        initialization.ValidateTarget(target.EntityId, target.MonsterId, target.LevelId,
            target.IsUnique, target.UniqueIdentity);
        if (initialization.CatacombsDepth is < 5 or > 8)
            throw new InvalidDataException("A Catacombs ranged action requires explicit L5-L8 initialization.");

        var special = IsSupportedSpecialController(target, missile);
        var ordinary = IsSupportedOrdinaryController(target, missile);
        if (!special && !ordinary)
            throw new NotSupportedException("This Catacombs actor and missile mapping has no ranged animation release controller.");

        var frameCount = special
            ? initialization.NativeSpecialAttackFrameCount
            : initialization.NativeAttackFrameCount;
        var sourceRate = special
            ? initialization.NativeSpecialAttackTicksPerFrame
            : initialization.NativeAttackTicksPerFrame;
        // Monster metadata stores effect frames one-based, separately from
        // the bestiary graphic frame counts retained above.
        var sourceEffectFrame = special
            ? initialization.SpecialAttackEffectFrame
            : initialization.AttackEffectFrame;
        if (frameCount is < 1 or > sbyte.MaxValue || sourceRate is < 0 or > sbyte.MaxValue)
            throw new InvalidDataException("Catacombs ranged action animation metadata is unavailable or outside native bounds.");
        if (sourceEffectFrame is < 1 or > sbyte.MaxValue)
            throw new InvalidDataException("Catacombs ranged action effect-frame metadata is outside native bounds.");

        var action = new AuthoritativeNativeCatacombsRangedAction(
            initialization.Binding.NativeSlot,
            target.EntityId,
            target.MonsterId,
            target.LevelId,
            initialization.Binding.NativeTypeId,
            target.NativeAi ?? string.Empty,
            initialization.MetadataFingerprint,
            populationFingerprint,
            ComputeMissileMappingFingerprint(missile),
            missile.MissileId,
            missile.ProjectileBehavior,
            missile.AttackMode,
            missile.SpellId,
            targetEntityId,
            targetX,
            targetY,
            special,
            attackSeed,
            frameCount,
            Math.Max(1, sourceRate),
            sourceEffectFrame - 1,
            0,
            0);
        action.ValidateShape();
        action.ValidateTarget(target, populationFingerprint);
        return action;
    }

    internal void ValidateShape()
    {
        if (NativeSlot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || EntityId == 0 || ContentMonsterId == 0 || LevelId == 0
            || NativeTypeId is < 0 or >= AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount
            || string.IsNullOrWhiteSpace(NativeAi) || NativeAi.Length > 64
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(MetadataFingerprint)
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(PopulationFingerprint)
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(MissileMappingFingerprint)
            || string.IsNullOrWhiteSpace(MissileId) || MissileId.Length > 64
            || !Enum.IsDefined(ProjectileBehavior) || !Enum.IsDefined(AttackMode)
            || TargetEntityId == 0
            || TargetX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || TargetY is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || ActionFrameCount is < 1 or > sbyte.MaxValue
            || TicksPerFrame is < 1 or > sbyte.MaxValue
            || EffectFrame < 0 || EffectFrame >= sbyte.MaxValue
            || FrameIndex < 0 || FrameIndex >= ActionFrameCount
            || TickCounter < 0 || TickCounter >= TicksPerFrame)
            throw new InvalidDataException("A Catacombs ranged action is outside its bound native animation or missile identity.");
        var supportedSpecial = UsesSpecialAttack
            && ((NativeAi == "Acid" && MissileId == "Acid"
                    && AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && ProjectileBehavior == AuthoritativeProjectileBehavior.Acid)
                || (NativeAi == "Magma" && MissileId == "MagmaBall"
                    && AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic)
                || (NativeAi == "AcidUnique" && MissileId == "Acid"
                    && AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
                    && ProjectileBehavior == AuthoritativeProjectileBehavior.Acid));
        var supportedOrdinary = !UsesSpecialAttack
            && NativeAi is "SkeletonRanged" or "GoatRanged"
            && MissileId == "Arrow"
            && AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterArrow
            && SpellId == 0;
        if ((!supportedSpecial || SpellId == 0) && !supportedOrdinary)
            throw new InvalidDataException("The saved Catacombs ranged action uses an unsupported actor or missile controller.");
    }

    internal void ValidateTarget(AuthoritativeCombatTarget target, string populationFingerprint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ValidateShape();
        var initialization = target.NativeInitialization
            ?? throw new InvalidDataException("A Catacombs ranged action lost its native initialization.");
        initialization.ValidateTarget(target.EntityId, target.MonsterId, target.LevelId,
            target.IsUnique, target.UniqueIdentity);
        var special = UsesSpecialAttack;
        if (initialization.CatacombsDepth is < 5 or > 8
            || initialization.Binding.NativeSlot != NativeSlot
            || initialization.Binding.NativeTypeId != NativeTypeId
            || target.EntityId != EntityId || target.MonsterId != ContentMonsterId
            || target.LevelId != LevelId || target.NativeAi != NativeAi
            || initialization.MetadataFingerprint != MetadataFingerprint
            || populationFingerprint != PopulationFingerprint
            || ((special ? initialization.SpecialAttackEffectFrame : initialization.AttackEffectFrame) - 1) != EffectFrame
            || (special ? initialization.NativeSpecialAttackFrameCount : initialization.NativeAttackFrameCount) != ActionFrameCount
            || Math.Max(1, special
                ? initialization.NativeSpecialAttackTicksPerFrame
                : initialization.NativeAttackTicksPerFrame) != TicksPerFrame)
            throw new InvalidDataException("A Catacombs ranged action no longer matches its admitted native actor.");
    }

    internal void ValidateMissile(AuthoritativeMonsterMissileDefinition missile)
    {
        ArgumentNullException.ThrowIfNull(missile);
        ValidateShape();
        if (missile.MissileId != MissileId || missile.ProjectileBehavior != ProjectileBehavior
            || missile.AttackMode != AttackMode || missile.SpellId != SpellId
            || ComputeMissileMappingFingerprint(missile) != MissileMappingFingerprint)
            throw new InvalidDataException("A pending Catacombs ranged action changed its immutable missile mapping.");
    }

    /** Native releases normal attacks on every visit to the source effect frame. */
    internal AuthoritativeNativeCatacombsRangedActionStep Advance()
    {
        ValidateShape();
        var release = FrameIndex == EffectFrame
            && (!UsesSpecialAttack || TickCounter == 0);
        // Native ranged mode handles the current frame before animation advances.
        // It releases on the action's effect frame, then enters Stand immediately
        // when the current graphic frame is already the final one.
        if (FrameIndex == ActionFrameCount - 1)
            return new(this with { FrameIndex = 0, TickCounter = 0 }, release, Completed: true);

        var nextCounter = TickCounter + 1;
        if (nextCounter >= TicksPerFrame)
            return new(this with { FrameIndex = FrameIndex + 1, TickCounter = 0 }, release, Completed: false);
        return new(this with { TickCounter = nextCounter }, release, Completed: false);
    }

    internal static bool IsSupportedOrdinaryController(
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile)
        => target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
            && target.NativeAi is "SkeletonRanged" or "GoatRanged"
            && missile.NativeAi == target.NativeAi
            && missile.MissileId == "Arrow"
            && missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterArrow
            && missile.SpellId == 0;

    internal static bool IsSupportedSpecialController(
        AuthoritativeCombatTarget target,
        AuthoritativeMonsterMissileDefinition missile)
        => target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
            && ((target.NativeAi == "Acid" && missile.NativeAi == "Acid" && missile.MissileId == "Acid"
                    && missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid)
                || (target.NativeAi == "Magma" && missile.NativeAi == "Magma"
                    && missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && missile.MissileId == "MagmaBall"
                    && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic)
                || (target.NativeAi == "AcidUnique" && missile.NativeAi == "AcidUnique"
                    && missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
                    && missile.MissileId == "Acid"
                    && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid))
            && missile.SpellId > 0;

    internal static string ComputeMissileMappingFingerprint(AuthoritativeMonsterMissileDefinition missile)
    {
        ArgumentNullException.ThrowIfNull(missile);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(missile)));
    }
}

internal readonly record struct AuthoritativeNativeCatacombsRangedActionStep(
    AuthoritativeNativeCatacombsRangedAction Action,
    bool Release,
    bool Completed);
