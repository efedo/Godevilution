namespace Devilution.Diablo;

public enum AuthoritativeProjectilePhase
{
    Travel,
    Impact,
}

/** Native-owned bow-arrow state retained until a collision resolves the attack. */
public sealed record AuthoritativeNativeWeaponArrowState(uint MissileDistance);

/** Server-owned in-flight spell or weapon-missile state. Projectiles resolve in stable entity order. */
public sealed class AuthoritativeProjectile
{
    public AuthoritativeProjectile(
        uint entityId,
        uint sourceEntityId,
        uint targetEntityId,
        uint spellId,
        uint levelId,
        int positionX,
        int positionY,
        int targetX,
        int targetY,
        int damage,
        AuthoritativeDamageType damageType,
        int areaRadius,
        uint remainingTicks,
        bool allowZeroSpellId = false)
    {
        if (entityId == 0
            || sourceEntityId == 0
            || (!allowZeroSpellId && spellId == 0)
            || remainingTicks == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (damage < 0 || areaRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        EntityId = entityId;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        SpellId = spellId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
        TargetX = targetX;
        TargetY = targetY;
        Damage = damage;
        DamageType = damageType;
        AreaRadius = areaRadius;
        RemainingTicks = remainingTicks;
        StartX = positionX;
        StartY = positionY;
    }

    public uint EntityId { get; }
    public uint SourceEntityId { get; }
    public uint TargetEntityId { get; }
    public uint SpellId { get; }
    public uint LevelId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int Damage { get; }

    /** Exact physical attack damage in native Q6 units for weapon arrows. */
    public int DamageQ6 { get; init; }

    /** Dynamic monster-arrow minimum damage rolled at impact; zero means fixed payload. */
    public int DamageMinimum { get; init; }

    /** Dynamic monster-arrow maximum damage rolled at impact; zero means fixed payload. */
    public int DamageMaximum { get; init; }

    /** Elemental channels captured when a weapon arrow is fired. */
    public int ElementalFireDamage { get; init; }

    public int ElementalLightningDamage { get; init; }

    /** Whether the native physical attack roll hit its target. */
    public bool AttackHit { get; init; }

    /** Present while this native-owned arrow still needs collision-time hit resolution. */
    public AuthoritativeNativeWeaponArrowState? NativeWeaponArrow { get; set; }

    /** Native wall-trap source and flight clock, separate from player bow authority. */
    public AuthoritativeNativeTrapArrowState? NativeTrapArrow { get; set; }

    /** Deferred Hellfire Peril self-damage for a weapon arrow. */
    public int PerilSelfDamageQ6 { get; init; }

    /** Deferred Hellfire Doppelganger proc for a weapon arrow. */
    public bool DoppelgangerProc { get; init; }

    /** Ticks before a delayed arrow begins moving. */
    public uint ActivationDelayTicks { get; set; }
    public AuthoritativeDamageType DamageType { get; }
    public int AreaRadius { get; }
    public uint RemainingTicks { get; set; }

    public AuthoritativeProjectileBehavior Behavior { get; init; }

    /** Content-owned missile identity used for exact native presentation compatibility. */
    public string? NativeMissileId { get; init; }

    public AuthoritativeProjectilePhase Phase { get; set; }

    public int StartX { get; init; }

    public int StartY { get; init; }

    /** Isometric screen-space displacement in native 16.16 fixed point. */
    public int TraveledX { get; set; }

    public int TraveledY { get; set; }

    public int VelocityX { get; set; }

    public int VelocityY { get; set; }

    /** Native eight-way direction encoded South=0 through SouthEast=7. */
    public int OriginalDirection { get; set; }

    public int PathIndex { get; set; }

    public int PathTicks { get; set; }

    public int Speed { get; init; }

    public int ImpactLifetime { get; init; } = 1;

    public int ResistantDamageDivisor { get; init; } = 1;

    /** Effective spell level captured when the projectile controller was created. */
    public int EffectiveSpellLevel { get; init; }

    /** Stable occupant checked on the last stationary tick, matching native collision suppression. */
    public uint LastCollisionTargetEntityId { get; set; }

    /** Native Fire Wall controller's current left branch position. */
    public int WallLeftX { get; set; }

    public int WallLeftY { get; set; }

    /** Native Fire Wall controller's current right branch position. */
    public int WallRightX { get; set; }

    public int WallRightY { get; set; }

    /** Native Fire Wall branch directions, encoded with the shared eight-way order. */
    public int WallLeftDirection { get; init; } = -1;

    public int WallRightDirection { get; init; } = -1;

    /** Branches stop independently when the native wall cannot grow further. */
    public bool WallLeftStopped { get; set; }

    public bool WallRightStopped { get; set; }

    /** Optional native only-hit-walking direction for a grown Fire Wall segment. */
    public int WallHitDirection { get; init; } = -1;

    /** Native Apocalypse row-major scan cursor. */
    public int ApocalypseScanX { get; set; }

    public int ApocalypseScanY { get; set; }

    /** Apocalypse Boom stops retrying after its first successful hit. */
    public bool ApocalypseHit { get; set; }

    /** Native Ring of Fire stops its crawl after the first missile blocker. */
    public bool RingOfFireLimitReached { get; set; }

    /** Native Guardian attack animation cooldown state (three ticks after firing). */
    public int GuardianState { get; set; }

    /** Initial Elemental destination retained for native burst line-of-sight checks. */
    public int ElementalOriginX { get; init; }

    public int ElementalOriginY { get; init; }
}
