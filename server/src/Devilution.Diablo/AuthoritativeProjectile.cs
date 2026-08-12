namespace Devilution.Diablo;

public enum AuthoritativeProjectilePhase
{
    Travel,
    Impact,
}

/** Server-owned in-flight spell state. Projectiles resolve in stable entity order. */
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
        uint remainingTicks)
    {
        if (entityId == 0 || sourceEntityId == 0 || spellId == 0 || remainingTicks == 0)
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
    public int TargetX { get; }
    public int TargetY { get; }
    public int Damage { get; }
    public AuthoritativeDamageType DamageType { get; }
    public int AreaRadius { get; }
    public uint RemainingTicks { get; set; }

    public AuthoritativeProjectileBehavior Behavior { get; init; }

    public AuthoritativeProjectilePhase Phase { get; set; }

    public int StartX { get; init; }

    public int StartY { get; init; }

    /** Isometric screen-space displacement in native 16.16 fixed point. */
    public int TraveledX { get; set; }

    public int TraveledY { get; set; }

    public int VelocityX { get; set; }

    public int VelocityY { get; set; }

    /** Native eight-way direction encoded South=0 through SouthEast=7. */
    public int OriginalDirection { get; init; }

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
}
