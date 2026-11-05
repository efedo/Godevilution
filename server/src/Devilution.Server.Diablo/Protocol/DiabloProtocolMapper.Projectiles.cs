using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Server.Snapshots;
using AuthoritativeMonsterTypeCatalog = Devilution.Diablo.AuthoritativeMonsterTypeCatalog;
using AuthoritativeProjectileBehavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Protocol;

/** Native projectile validation and wire presentation. */
internal static partial class DiabloProtocolMapper
{
    private static void ValidateProjectedProjectileStates(
        IReadOnlyList<DiabloProjectileState> projectiles,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        AuthoritativeMonsterMissileCatalog? monsterMissiles)
    {
        foreach (var projectile in projectiles) {
            // Object traps have no spell or monster caster. Reuse the module's
            // full native kind/stage/trajectory validation before projecting them.
            var objectTrap = projectile.NativeTrapArrow is not null;
            projectile.NativeTrapArrow?.ValidateShape(projectile);
            projectile.NativeMonsterMissile?.ValidateShape(projectile);
            // Native Arrow and Acid-family missiles keep their behavior's identity; other ids name monster magic.
            // MagmaBallExplosion has no native visual yet and draws as generic monster magic.
            if (!objectTrap && projectile.NativeMissileId is not null
                && NativeMissileIdentityOf(projectile.Behavior) != projectile.NativeMissileId
                && !(projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic
                    && projectile.NativeMissileId == "MagmaBallExplosion")
                && (projectile.Behavior != AuthoritativeProjectileBehavior.MonsterMagic
                    || ResolveNativeProjectileVisual(
                        projectile.Behavior,
                        projectile.DamageType,
                        projectile.NativeMissileId) == NativeProjectileVisual.Unspecified)) {
                throw new InvalidDataException(
                    "Authoritative projectile contains an unknown or misplaced native missile identity.");
            }

            ValidateProjectedProjectilePosition(
                projectile,
                projectile.PositionX,
                projectile.PositionY,
                levels,
                "projectile");
            ValidateProjectedProjectilePosition(
                projectile,
                ProjectedProjectileTarget(projectile).X,
                ProjectedProjectileTarget(projectile).Y,
                levels,
                "projectile target");
            if (projectile.SourceEntityId == 0
                || ProjectedProjectileSpellId(projectile, monsterMissiles) == 0 && !objectTrap
                    && projectile.Behavior is not (AuthoritativeProjectileBehavior.WeaponArrow
                        or AuthoritativeProjectileBehavior.MonsterArrow)
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
                || projectile.DamageType is < AuthoritativeDamageType.Physical
                    or > AuthoritativeDamageType.Acid
                || projectile.Behavior is < AuthoritativeProjectileBehavior.Targeted
                    or > AuthoritativeProjectileBehavior.MonsterArrow
                || projectile.Phase is < AuthoritativeProjectilePhase.Travel
                    or > AuthoritativeProjectilePhase.Impact
                || UsesDirectionalPresentation(projectile)
                    && projectile.OriginalDirection is < 0 or > MaximumNativeMonsterDirection) {
                throw new InvalidDataException(
                    "Authoritative projectile state is outside the shared protocol range.");
            }
        }
    }

    /** AddLightning's dst=start+SouthEast is an internal offset, not an entity aim tile. */
    private static (int X, int Y) ProjectedProjectileTarget(DiabloProjectileState projectile)
        => projectile.NativeMonsterMissile?.Kind == AuthoritativeNativeMonsterMissileKind.Lightning
            ? (projectile.PositionX, projectile.PositionY) : (projectile.TargetX, projectile.TargetY);

    /** Native BatAi/AddLightning has no spell caster; use its existing content binding on the wire. */
    private static uint ProjectedProjectileSpellId(DiabloProjectileState projectile,
        AuthoritativeMonsterMissileCatalog? monsterMissiles)
    {
        if (projectile.NativeMonsterMissile?.Kind != AuthoritativeNativeMonsterMissileKind.Lightning)
            return projectile.SpellId;
        projectile.NativeMonsterMissile.ValidateFamiliarLightningShape(projectile);
        if (monsterMissiles is null || !monsterMissiles.TryGetByMissileId("Lightning", out var mapping)
            || mapping.NativeAi != "Bat" || !mapping.IsPostMeleeMissile || mapping.SpellId == 0
            || mapping.ProjectileBehavior != AuthoritativeProjectileBehavior.MonsterMagic)
            throw new InvalidDataException("Native Familiar Lightning requires its explicit content spell binding.");
        return mapping.SpellId;
    }

    /** The one native missile identity a non-magic behavior may carry, or null. */
    private static string? NativeMissileIdentityOf(AuthoritativeProjectileBehavior behavior)
        => behavior switch {
            AuthoritativeProjectileBehavior.MonsterArrow => "Arrow",
            AuthoritativeProjectileBehavior.Acid => "Acid",
            AuthoritativeProjectileBehavior.AcidSplat => "AcidSplat",
            AuthoritativeProjectileBehavior.AcidPuddle => "AcidPuddle",
            _ => null,
        };

    private static bool UsesDirectionalPresentation(DiabloProjectileState projectile)
    {
        if (!string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;

        return projectile.Behavior is
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

    private static void ValidateProjectedProjectilePosition(
        DiabloProjectileState projectile,
        int positionX,
        int positionY,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        string entityKind)
    {
        if (levels.TryGetValue(projectile.LevelId, out var level)
            && AuthoritativeProtocolLimits.IsElementalImpactPositionAtLevelEdge(
                (int)projectile.Behavior,
                (int)projectile.Phase,
                positionX,
                positionY,
                level.Width,
                level.Height)) {
            return;
        }
        ValidateProjectedEntityPosition(
            projectile.LevelId,
            positionX,
            positionY,
            levels,
            entityKind);
    }

    private static ProjectilePresentationKind ResolveProjectilePresentationKind(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeProjectilePhase phase,
        AuthoritativeDamageType damageType)
    {
        if (behavior == AuthoritativeProjectileBehavior.ResurrectionBeam)
            return ProjectilePresentationKind.Beam;
        if (behavior == AuthoritativeProjectileBehavior.Teleport)
            return ProjectilePresentationKind.Teleport;
        if (behavior == AuthoritativeProjectileBehavior.Phasing)
            return ProjectilePresentationKind.Phasing;
        if (behavior == AuthoritativeProjectileBehavior.StoneCurse)
            return ProjectilePresentationKind.StoneCurse;
        if (behavior == AuthoritativeProjectileBehavior.RageController)
            return ProjectilePresentationKind.Hidden;
        if (behavior is AuthoritativeProjectileBehavior.Flash
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop)
            return ProjectilePresentationKind.Flash;
        if (behavior is AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball)
            return phase == AuthoritativeProjectilePhase.Impact
                ? ProjectilePresentationKind.FireImpact
                : ProjectilePresentationKind.Fire;
        if (behavior is AuthoritativeProjectileBehavior.FireWallController
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.RingOfFireController
                or AuthoritativeProjectileBehavior.ImmolationController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.Elemental)
            return ProjectilePresentationKind.Fire;
        if (behavior == AuthoritativeProjectileBehavior.FireWallSegment)
            return damageType == AuthoritativeDamageType.Lightning
                ? ProjectilePresentationKind.Lightning
                : ProjectilePresentationKind.Fire;
        if (behavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
            return ProjectilePresentationKind.FireImpact;
        if (behavior == AuthoritativeProjectileBehavior.GuardianController)
            return ProjectilePresentationKind.Guardian;
        if (behavior == AuthoritativeProjectileBehavior.NovaBall)
            return ProjectilePresentationKind.NovaBall;
        if (behavior is AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.ChainLightningController
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaController
                or AuthoritativeProjectileBehavior.LightningWallController
                or AuthoritativeProjectileBehavior.MonsterChargedBolt)
            return ProjectilePresentationKind.Lightning;
        if (behavior == AuthoritativeProjectileBehavior.HolyBolt)
            return ProjectilePresentationKind.Holy;
        if (behavior == AuthoritativeProjectileBehavior.BloodStar)
            return ProjectilePresentationKind.BloodStar;
        if (behavior == AuthoritativeProjectileBehavior.MonsterMagic)
            return ProjectilePresentationKind.MonsterMagic;
        if (behavior == AuthoritativeProjectileBehavior.HorkSpawn)
            return ProjectilePresentationKind.HorkSpawn;
        if (behavior == AuthoritativeProjectileBehavior.BoneSpirit)
            return ProjectilePresentationKind.BoneSpirit;
        if (behavior is AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle)
            return ProjectilePresentationKind.Acid;
        if (behavior is AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow)
            return ProjectilePresentationKind.Arrow;
        return ProjectilePresentationKind.Generic;
    }

    /** Selects the native compatibility sprite without exposing gameplay IDs to the client. */
    private static NativeProjectileVisual ResolveNativeProjectileVisual(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeDamageType damageType,
        string? nativeMissileId)
    {
        if (behavior == AuthoritativeProjectileBehavior.MonsterMagic) {
            return nativeMissileId switch {
                "Lightning" => NativeProjectileVisual.MonsterLightning,
                "OrangeFlare" => NativeProjectileVisual.MonsterOrangeFlare,
                "BlueFlare" => NativeProjectileVisual.MonsterBlueFlare,
                "RedFlare" => NativeProjectileVisual.MonsterRedFlare,
                "YellowFlare" => NativeProjectileVisual.MonsterYellowFlare,
                "BlueFlare2" => NativeProjectileVisual.MonsterBlueFlare2,
                "MagmaBall" => NativeProjectileVisual.MonsterMagmaBall,
                _ => NativeProjectileVisual.Unspecified,
            };
        }

        return behavior switch {
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                => NativeProjectileVisual.ChargedBolt,
            AuthoritativeProjectileBehavior.Firebolt => NativeProjectileVisual.Firebolt,
            AuthoritativeProjectileBehavior.Lightning => NativeProjectileVisual.Lightning,
            AuthoritativeProjectileBehavior.LightningSegment => NativeProjectileVisual.ThinLightning,
            AuthoritativeProjectileBehavior.FlashBottom => NativeProjectileVisual.FlashBottom,
            AuthoritativeProjectileBehavior.FlashTop => NativeProjectileVisual.FlashTop,
            AuthoritativeProjectileBehavior.ResurrectionBeam => NativeProjectileVisual.ResurrectBeam,
            AuthoritativeProjectileBehavior.Phasing => NativeProjectileVisual.Phasing,
            AuthoritativeProjectileBehavior.Fireball => NativeProjectileVisual.Fireball,
            AuthoritativeProjectileBehavior.HolyBolt => NativeProjectileVisual.HolyBolt,
            AuthoritativeProjectileBehavior.BloodStar => NativeProjectileVisual.BloodStar,
            AuthoritativeProjectileBehavior.BoneSpirit => NativeProjectileVisual.BoneSpirit,
            AuthoritativeProjectileBehavior.FireWallSegment when damageType == AuthoritativeDamageType.Lightning => NativeProjectileVisual.LightningWall,
            AuthoritativeProjectileBehavior.FireWallSegment => NativeProjectileVisual.FireWall,
            AuthoritativeProjectileBehavior.FlameWaveController => NativeProjectileVisual.FlameWaveControl,
            AuthoritativeProjectileBehavior.FlameWaveSegment => NativeProjectileVisual.FlameWave,
            AuthoritativeProjectileBehavior.InfernoSegment => NativeProjectileVisual.Inferno,
            AuthoritativeProjectileBehavior.ChainLightningBolt => NativeProjectileVisual.ChainLightning,
            AuthoritativeProjectileBehavior.ApocalypseBoom => NativeProjectileVisual.ApocalypseBoom,
            AuthoritativeProjectileBehavior.Elemental => NativeProjectileVisual.Elemental,
            AuthoritativeProjectileBehavior.GuardianController => NativeProjectileVisual.Guardian,
            AuthoritativeProjectileBehavior.Etherealize => NativeProjectileVisual.Etherealize,
            AuthoritativeProjectileBehavior.Acid => NativeProjectileVisual.Acid,
            AuthoritativeProjectileBehavior.AcidSplat => NativeProjectileVisual.AcidSplat,
            AuthoritativeProjectileBehavior.AcidPuddle => NativeProjectileVisual.AcidPuddle,
            AuthoritativeProjectileBehavior.HorkSpawn => NativeProjectileVisual.HorkSpawn,
            AuthoritativeProjectileBehavior.NovaBall => NativeProjectileVisual.NovaBall,
            AuthoritativeProjectileBehavior.WeaponArrow or AuthoritativeProjectileBehavior.MonsterArrow => NativeProjectileVisual.Arrow,
            _ => NativeProjectileVisual.Unspecified,
        };
    }

    private static string? ResolveNativeMonsterMissileId(NativeProjectileVisual visual)
        => visual switch {
            NativeProjectileVisual.MonsterLightning => "Lightning",
            NativeProjectileVisual.MonsterOrangeFlare => "OrangeFlare",
            NativeProjectileVisual.MonsterBlueFlare => "BlueFlare",
            NativeProjectileVisual.MonsterRedFlare => "RedFlare",
            NativeProjectileVisual.MonsterYellowFlare => "YellowFlare",
            NativeProjectileVisual.MonsterBlueFlare2 => "BlueFlare2",
            NativeProjectileVisual.MonsterMagmaBall => "MagmaBall",
            _ => null,
        };
}
