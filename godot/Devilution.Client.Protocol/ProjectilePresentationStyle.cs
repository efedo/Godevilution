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
    Beam,
    StoneCurse,
    Teleport,
    Phasing,
}

/** Maps authoritative behavior identities to client-only visual families. */
public static class ProjectilePresentationStyle
{
    private const int Lightning = 3;
    private const int LightningSegment = 4;
    private const int ResurrectionBeam = 11;
    private const int StoneCurse = 13;
    private const int Teleport = 9;
    private const int Phasing = 15;
    private const int Fireball = 16;
    private const int HolyBolt = 19;
    private const int BloodStar = 20;
    private const int BoneSpirit = 21;
    private const int FireWallController = 22;
    private const int FireWallSegment = 23;
    private const int FlameWaveController = 24;
    private const int FlameWaveSegment = 25;
    private const int InfernoController = 26;
    private const int InfernoSegment = 27;
    private const int ChainLightningController = 28;
    private const int ChainLightningBolt = 29;
    private const int NovaController = 30;
    private const int NovaBall = 31;
    private const int RingOfFireController = 42;
    private const int ImmolationController = 43;
    private const int LightningWallController = 47;
    private const int Acid = 51;
    private const int AcidSplat = 52;
    private const int AcidPuddle = 53;
    private const int WeaponArrow = 58;
    private const int MonsterArrow = 59;
    private const int ImpactPhase = 1;

    public static ProjectilePresentationKind Resolve(ProjectileSnapshot projectile)
    {
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
            Devilution.Protocol.V1.ProjectilePresentationKind.BoneSpirit
                => ProjectilePresentationKind.BoneSpirit,
            Devilution.Protocol.V1.ProjectilePresentationKind.Acid
                => ProjectilePresentationKind.Acid,
            Devilution.Protocol.V1.ProjectilePresentationKind.Arrow
                => ProjectilePresentationKind.Arrow,
            Devilution.Protocol.V1.ProjectilePresentationKind.Beam
                => ProjectilePresentationKind.Beam,
            Devilution.Protocol.V1.ProjectilePresentationKind.StoneCurse
                => ProjectilePresentationKind.StoneCurse,
            Devilution.Protocol.V1.ProjectilePresentationKind.Teleport
                => ProjectilePresentationKind.Teleport,
            Devilution.Protocol.V1.ProjectilePresentationKind.Phasing
                => ProjectilePresentationKind.Phasing,
            _ => Resolve(projectile.Behavior, projectile.Phase),
        };
    }

    public static ProjectilePresentationKind Resolve(int behavior, int phase)
    {
        if (behavior == ResurrectionBeam)
            return ProjectilePresentationKind.Beam;
        if (behavior == Teleport)
            return ProjectilePresentationKind.Teleport;
        if (behavior == Phasing)
            return ProjectilePresentationKind.Phasing;
        if (behavior == StoneCurse)
            return ProjectilePresentationKind.StoneCurse;
        if (behavior == Fireball)
            return phase == ImpactPhase
                ? ProjectilePresentationKind.FireImpact
                : ProjectilePresentationKind.Fire;
        if (behavior is FireWallController
                or FireWallSegment
                or FlameWaveController
                or FlameWaveSegment
                or InfernoController
                or InfernoSegment
                or RingOfFireController
                or ImmolationController)
            return ProjectilePresentationKind.Fire;
        if (behavior is Lightning
                or LightningSegment
                or ChainLightningController
                or ChainLightningBolt
                or NovaController
                or NovaBall
                or LightningWallController)
            return ProjectilePresentationKind.Lightning;
        if (behavior == HolyBolt)
            return ProjectilePresentationKind.Holy;
        if (behavior == BloodStar)
            return ProjectilePresentationKind.BloodStar;
        if (behavior == BoneSpirit)
            return ProjectilePresentationKind.BoneSpirit;
        if (behavior is Acid or AcidSplat or AcidPuddle)
            return ProjectilePresentationKind.Acid;
        if (behavior is WeaponArrow or MonsterArrow)
            return ProjectilePresentationKind.Arrow;
        return ProjectilePresentationKind.Generic;
    }
}
