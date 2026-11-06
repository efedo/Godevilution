using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Validates ordinary projectiles and the source-less native object-trap family. */
public sealed partial class AuthoritativeClientModel
{
    private static void ValidateProjectile(ProjectileSnapshot projectile, Snapshot snapshot)
    {
        var nativeTrap = IsNativeObjectTrapProjectile(projectile, snapshot);
        if (projectile.SourceEntityId == 0
            || (projectile.SpellId == 0 && !IsWeaponProjectile(projectile.Behavior) && !nativeTrap)
            || projectile.RemainingTicks == 0
            || projectile.Damage < 0
            || projectile.AreaRadius < 0
            || projectile.DamageMinimum < 0
            || projectile.DamageMaximum < projectile.DamageMinimum
            || projectile.DamageQ6 < 0
            || projectile.ElementalFireDamage < 0
            || projectile.ElementalLightningDamage < 0
            || projectile.PerilSelfDamageQ6 < 0
            || projectile.EffectiveSpellLevel < 0
            || projectile.DamageType is < 0 or > MaximumProjectileDamageType
            || projectile.Behavior is < 0 or > MaximumProjectileBehavior
            || projectile.Phase is < 0 or > MaximumProjectilePhase
            || (int)projectile.PresentationKind is < 0 or > MaximumProjectilePresentationKind
            || !Enum.IsDefined(projectile.PresentationKind)
            || (int)projectile.NativeVisual is < 0 or > MaximumNativeProjectileVisual
            || !Enum.IsDefined(projectile.NativeVisual)
            || UsesDirectionalPresentation(projectile)
                && projectile.OriginalDirection is < 0 or > MaximumNativeMonsterDirection
            || !IsValidNativeMissileIdentity(projectile) && !nativeTrap) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid projectile state.");
        }
    }

    /** Trap ownership is already visible: a retained trigger and its native wall object or chest. */
    private static bool IsNativeObjectTrapProjectile(ProjectileSnapshot projectile, Snapshot snapshot)
    {
        if (projectile.SpellId != 0 || projectile.LevelId is < 1 or > 8
            || projectile.RemainingTicks > 256)
            return false;
        var source = snapshot.Objects.FirstOrDefault(value => value.EntityId == projectile.SourceEntityId);
        var trigger = snapshot.Objects.FirstOrDefault(value => value.EntityId == projectile.TargetEntityId);
        if (source is null || trigger is null || source.LevelId != projectile.LevelId
            || trigger.LevelId != projectile.LevelId || source.Destroyed || trigger.Destroyed
            || projectile.StartX != source.PositionX || projectile.StartY != source.PositionY)
            return false;
        var intrinsic = source.EntityId == trigger.EntityId
            && source.NativeObjectId is 68 or 69 or 70 && source.Activated;
        var wall = source.NativeObjectId is 53 or 54
            // A fired door can be closed again while its missile is still alive.
            && (trigger.NativeObjectId is 1 or 2 or 42 or 43
                || trigger.NativeObjectId is 5 or 6 or 7 or 48 && trigger.Activated);
        if (!intrinsic && !wall)
            return false;
        const int travel = 0;
        const int impact = 1;
        return ((AuthoritativeProjectileBehavior)projectile.Behavior, projectile.NativeMissileId, projectile.Phase) switch {
            (AuthoritativeProjectileBehavior.WeaponArrow, "FireArrow", travel
                or impact) => intrinsic,
            (AuthoritativeProjectileBehavior.Firebolt, "Firebolt", travel) => wall,
            (AuthoritativeProjectileBehavior.Firebolt, "MagmaBallExplosion", impact) => wall,
            (AuthoritativeProjectileBehavior.Lightning, "LightningControl", travel) => wall,
            (AuthoritativeProjectileBehavior.LightningSegment, "Lightning", travel) => wall,
            _ => false,
        };
    }

    private static bool UsesDirectionalPresentation(ProjectileSnapshot projectile)
    {
        if (projectile.NativeVisual != NativeProjectileVisual.Unspecified
            || !string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;

        return (AuthoritativeProjectileBehavior)projectile.Behavior is
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
                or AuthoritativeProjectileBehavior.ResurrectionBeam
                or AuthoritativeProjectileBehavior.Phasing
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.HolyBolt
                or AuthoritativeProjectileBehavior.BloodStar
                or AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.FireWallSegment
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaBall
                or AuthoritativeProjectileBehavior.ApocalypseBoom
                or AuthoritativeProjectileBehavior.Elemental
                or AuthoritativeProjectileBehavior.GuardianController
                or AuthoritativeProjectileBehavior.Etherealize
                or AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.HorkSpawn
                or AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;
    }

    private static bool IsWeaponProjectile(int behavior)
        => (AuthoritativeProjectileBehavior)behavior is
            AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;

    private static bool IsValidNativeMissileIdentity(ProjectileSnapshot projectile)
    {
        if (string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;
        // Native Arrow and Acid-family missiles keep their behavior's identity.
        switch ((AuthoritativeProjectileBehavior)projectile.Behavior) {
        case AuthoritativeProjectileBehavior.MonsterArrow:
            return projectile.NativeMissileId == "Arrow";
        case AuthoritativeProjectileBehavior.Acid:
            return projectile.NativeMissileId == "Acid";
        case AuthoritativeProjectileBehavior.AcidSplat:
            return projectile.NativeMissileId == "AcidSplat";
        case AuthoritativeProjectileBehavior.AcidPuddle:
            return projectile.NativeMissileId == "AcidPuddle";
        }
        if ((AuthoritativeProjectileBehavior)projectile.Behavior
                != AuthoritativeProjectileBehavior.MonsterMagic
            || !AuthoritativeProtocolLimits.IsIdentityWithinLimit(projectile.NativeMissileId)
            || !IsAsciiLetter(projectile.NativeMissileId[0])
                && projectile.NativeMissileId[0] != '_') {
            return false;
        }
        return projectile.NativeMissileId.Skip(1).All(character =>
            IsAsciiLetter(character)
                || character is >= '0' and <= '9'
                || character is '_' or '-');
    }

    private static bool IsAsciiLetter(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

}
