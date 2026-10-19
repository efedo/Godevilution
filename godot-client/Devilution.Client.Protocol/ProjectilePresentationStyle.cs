using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation family projected by the server, with a legacy behavior fallback. */
public enum ProjectilePresentationKind
{
    Generic,
    Fire,
    FireImpact,
    Lightning,
    Holy,
    BloodStar,
    BoneSpirit,
    Acid,
    Arrow,
    Guardian,
    Beam,
    StoneCurse,
    Teleport,
    Phasing,
    MonsterMagic,
    MonsterLightning,
    MonsterOrangeFlare,
    MonsterBlueFlare,
    MonsterRedFlare,
    MonsterYellowFlare,
    MonsterBlueFlare2,
    MonsterMagmaBall,
    HorkSpawn,
    Flash,
    Hidden,
    NovaBall,
}

/** Maps authoritative behavior identities to client-only visual families. */
public static class ProjectilePresentationStyle
{
    private const AuthoritativeProjectileBehavior Lightning = AuthoritativeProjectileBehavior.Lightning;
    private const AuthoritativeProjectileBehavior LightningSegment = AuthoritativeProjectileBehavior.LightningSegment;
    private const AuthoritativeProjectileBehavior ChargedBolt = AuthoritativeProjectileBehavior.ChargedBolt;
    private const AuthoritativeProjectileBehavior ResurrectionBeam = AuthoritativeProjectileBehavior.ResurrectionBeam;
    private const AuthoritativeProjectileBehavior StoneCurse = AuthoritativeProjectileBehavior.StoneCurse;
    private const AuthoritativeProjectileBehavior RageController = AuthoritativeProjectileBehavior.RageController;
    private const AuthoritativeProjectileBehavior Flash = AuthoritativeProjectileBehavior.Flash;
    private const AuthoritativeProjectileBehavior FlashBottom = AuthoritativeProjectileBehavior.FlashBottom;
    private const AuthoritativeProjectileBehavior FlashTop = AuthoritativeProjectileBehavior.FlashTop;
    private const AuthoritativeProjectileBehavior Teleport = AuthoritativeProjectileBehavior.Teleport;
    private const AuthoritativeProjectileBehavior Phasing = AuthoritativeProjectileBehavior.Phasing;
    private const AuthoritativeProjectileBehavior Firebolt = AuthoritativeProjectileBehavior.Firebolt;
    private const AuthoritativeProjectileBehavior Fireball = AuthoritativeProjectileBehavior.Fireball;
    private const AuthoritativeProjectileBehavior HolyBolt = AuthoritativeProjectileBehavior.HolyBolt;
    private const AuthoritativeProjectileBehavior BloodStar = AuthoritativeProjectileBehavior.BloodStar;
    private const AuthoritativeProjectileBehavior MonsterMagic = AuthoritativeProjectileBehavior.MonsterMagic;
    private const AuthoritativeProjectileBehavior HorkSpawn = AuthoritativeProjectileBehavior.HorkSpawn;
    private const AuthoritativeProjectileBehavior BoneSpirit = AuthoritativeProjectileBehavior.BoneSpirit;
    private const AuthoritativeProjectileBehavior FireWallController = AuthoritativeProjectileBehavior.FireWallController;
    private const AuthoritativeProjectileBehavior FireWallSegment = AuthoritativeProjectileBehavior.FireWallSegment;
    private const int LightningDamageType = 2;
    private const AuthoritativeProjectileBehavior FlameWaveController = AuthoritativeProjectileBehavior.FlameWaveController;
    private const AuthoritativeProjectileBehavior FlameWaveSegment = AuthoritativeProjectileBehavior.FlameWaveSegment;
    private const AuthoritativeProjectileBehavior InfernoController = AuthoritativeProjectileBehavior.InfernoController;
    private const AuthoritativeProjectileBehavior InfernoSegment = AuthoritativeProjectileBehavior.InfernoSegment;
    private const AuthoritativeProjectileBehavior ApocalypseController = AuthoritativeProjectileBehavior.ApocalypseController;
    private const AuthoritativeProjectileBehavior ApocalypseBoom = AuthoritativeProjectileBehavior.ApocalypseBoom;
    private const AuthoritativeProjectileBehavior Elemental = AuthoritativeProjectileBehavior.Elemental;
    private const AuthoritativeProjectileBehavior GuardianController = AuthoritativeProjectileBehavior.GuardianController;
    private const AuthoritativeProjectileBehavior ChainLightningController = AuthoritativeProjectileBehavior.ChainLightningController;
    private const AuthoritativeProjectileBehavior ChainLightningBolt = AuthoritativeProjectileBehavior.ChainLightningBolt;
    private const AuthoritativeProjectileBehavior NovaController = AuthoritativeProjectileBehavior.NovaController;
    private const AuthoritativeProjectileBehavior NovaBall = AuthoritativeProjectileBehavior.NovaBall;
    private const AuthoritativeProjectileBehavior RingOfFireController = AuthoritativeProjectileBehavior.RingOfFireController;
    private const AuthoritativeProjectileBehavior ImmolationController = AuthoritativeProjectileBehavior.ImmolationController;
    private const AuthoritativeProjectileBehavior LightningWallController = AuthoritativeProjectileBehavior.LightningWallController;
    private const AuthoritativeProjectileBehavior Acid = AuthoritativeProjectileBehavior.Acid;
    private const AuthoritativeProjectileBehavior AcidSplat = AuthoritativeProjectileBehavior.AcidSplat;
    private const AuthoritativeProjectileBehavior AcidPuddle = AuthoritativeProjectileBehavior.AcidPuddle;
    private const AuthoritativeProjectileBehavior WeaponArrow = AuthoritativeProjectileBehavior.WeaponArrow;
    private const AuthoritativeProjectileBehavior MonsterArrow = AuthoritativeProjectileBehavior.MonsterArrow;
    private const AuthoritativeProjectileBehavior MonsterChargedBolt = AuthoritativeProjectileBehavior.MonsterChargedBolt;
    private const int ImpactPhase = 1;

    public static ProjectilePresentationKind Resolve(ProjectileSnapshot projectile)
    {
        if (ResolveNativeVisual(projectile.NativeVisual) is { } nativeVisual)
            return nativeVisual;
        if (ResolveNativeMissile(projectile.NativeMissileId) is { } nativeMissile)
            return nativeMissile;

        return projectile.PresentationKind switch {
            Devilution.Protocol.V1.ProjectilePresentationKind.Generic
                => ProjectilePresentationKind.Generic,
            Devilution.Protocol.V1.ProjectilePresentationKind.Fire
                => ProjectilePresentationKind.Fire,
            Devilution.Protocol.V1.ProjectilePresentationKind.FireImpact
                => ProjectilePresentationKind.FireImpact,
            Devilution.Protocol.V1.ProjectilePresentationKind.Lightning
                => ProjectilePresentationKind.Lightning,
            Devilution.Protocol.V1.ProjectilePresentationKind.Holy
                => ProjectilePresentationKind.Holy,
            Devilution.Protocol.V1.ProjectilePresentationKind.BloodStar
                => ProjectilePresentationKind.BloodStar,
            Devilution.Protocol.V1.ProjectilePresentationKind.MonsterMagic
                => ProjectilePresentationKind.MonsterMagic,
            Devilution.Protocol.V1.ProjectilePresentationKind.HorkSpawn
                => ProjectilePresentationKind.HorkSpawn,
            Devilution.Protocol.V1.ProjectilePresentationKind.Flash
                => ProjectilePresentationKind.Flash,
            Devilution.Protocol.V1.ProjectilePresentationKind.Hidden
                => ProjectilePresentationKind.Hidden,
            Devilution.Protocol.V1.ProjectilePresentationKind.NovaBall
                => ProjectilePresentationKind.NovaBall,
            Devilution.Protocol.V1.ProjectilePresentationKind.BoneSpirit
                => ProjectilePresentationKind.BoneSpirit,
            Devilution.Protocol.V1.ProjectilePresentationKind.Acid
                => ProjectilePresentationKind.Acid,
            Devilution.Protocol.V1.ProjectilePresentationKind.Arrow
                => ProjectilePresentationKind.Arrow,
            Devilution.Protocol.V1.ProjectilePresentationKind.Guardian
                => ProjectilePresentationKind.Guardian,
            Devilution.Protocol.V1.ProjectilePresentationKind.Beam
                => ProjectilePresentationKind.Beam,
            Devilution.Protocol.V1.ProjectilePresentationKind.StoneCurse
                => ProjectilePresentationKind.StoneCurse,
            Devilution.Protocol.V1.ProjectilePresentationKind.Teleport
                => ProjectilePresentationKind.Teleport,
            Devilution.Protocol.V1.ProjectilePresentationKind.Phasing
                => ProjectilePresentationKind.Phasing,
            _ => Resolve(projectile.Behavior, projectile.Phase, projectile.DamageType),
        };
    }

    private static ProjectilePresentationKind? ResolveNativeVisual(NativeProjectileVisual visual)
        => visual switch {
            NativeProjectileVisual.MonsterLightning => ProjectilePresentationKind.MonsterLightning,
            NativeProjectileVisual.MonsterOrangeFlare => ProjectilePresentationKind.MonsterOrangeFlare,
            NativeProjectileVisual.MonsterBlueFlare => ProjectilePresentationKind.MonsterBlueFlare,
            NativeProjectileVisual.MonsterRedFlare => ProjectilePresentationKind.MonsterRedFlare,
            NativeProjectileVisual.MonsterYellowFlare => ProjectilePresentationKind.MonsterYellowFlare,
            NativeProjectileVisual.MonsterBlueFlare2 => ProjectilePresentationKind.MonsterBlueFlare2,
            NativeProjectileVisual.MonsterMagmaBall => ProjectilePresentationKind.MonsterMagmaBall,
            _ => null,
        };

    private static ProjectilePresentationKind? ResolveNativeMissile(string missileId)
        => missileId switch {
            "Lightning" => ProjectilePresentationKind.MonsterLightning,
            "OrangeFlare" => ProjectilePresentationKind.MonsterOrangeFlare,
            "BlueFlare" => ProjectilePresentationKind.MonsterBlueFlare,
            "RedFlare" => ProjectilePresentationKind.MonsterRedFlare,
            "YellowFlare" => ProjectilePresentationKind.MonsterYellowFlare,
            "BlueFlare2" => ProjectilePresentationKind.MonsterBlueFlare2,
            "MagmaBall" => ProjectilePresentationKind.MonsterMagmaBall,
            "HorkSpawn" => ProjectilePresentationKind.HorkSpawn,
            _ => null,
        };

    public static ProjectilePresentationKind Resolve(int behavior, int phase)
        => Resolve(behavior, phase, 0);

    public static ProjectilePresentationKind Resolve(int behavior, int phase, int damageType)
        => Resolve((AuthoritativeProjectileBehavior)behavior, phase, damageType);

    private static ProjectilePresentationKind Resolve(
        AuthoritativeProjectileBehavior behavior,
        int phase,
        int damageType)
    {
        if (behavior == ResurrectionBeam)
            return ProjectilePresentationKind.Beam;
        if (behavior == Teleport)
            return ProjectilePresentationKind.Teleport;
        if (behavior == Phasing)
            return ProjectilePresentationKind.Phasing;
        if (behavior == StoneCurse)
            return ProjectilePresentationKind.StoneCurse;
        if (behavior == RageController)
            return ProjectilePresentationKind.Hidden;
        if (behavior is Flash or FlashBottom or FlashTop)
            return ProjectilePresentationKind.Flash;
        if (behavior is Firebolt or Fireball)
            return phase == ImpactPhase
                ? ProjectilePresentationKind.FireImpact
                : ProjectilePresentationKind.Fire;
        if (behavior == FireWallSegment)
            return damageType == LightningDamageType
                ? ProjectilePresentationKind.Lightning
                : ProjectilePresentationKind.Fire;
        if (behavior is FireWallController
                or FlameWaveController
                or FlameWaveSegment
                or InfernoController
                or InfernoSegment
                or RingOfFireController
                or ImmolationController
                or ApocalypseController
                or Elemental)
            return ProjectilePresentationKind.Fire;
        if (behavior == ApocalypseBoom)
            return ProjectilePresentationKind.FireImpact;
        if (behavior == GuardianController)
            return ProjectilePresentationKind.Guardian;
        if (behavior == NovaBall)
            return ProjectilePresentationKind.NovaBall;
        if (behavior is ChargedBolt
                or Lightning
                or LightningSegment
                or ChainLightningController
                or ChainLightningBolt
                or NovaController
                or LightningWallController
                or MonsterChargedBolt)
            return ProjectilePresentationKind.Lightning;
        if (behavior == HolyBolt)
            return ProjectilePresentationKind.Holy;
        if (behavior == BloodStar)
            return ProjectilePresentationKind.BloodStar;
        if (behavior == MonsterMagic)
            return ProjectilePresentationKind.MonsterMagic;
        if (behavior == HorkSpawn)
            return ProjectilePresentationKind.HorkSpawn;
        if (behavior == BoneSpirit)
            return ProjectilePresentationKind.BoneSpirit;
        if (behavior is Acid or AcidSplat or AcidPuddle)
            return ProjectilePresentationKind.Acid;
        if (behavior is WeaponArrow or MonsterArrow)
            return ProjectilePresentationKind.Arrow;
        return ProjectilePresentationKind.Generic;
    }
}
