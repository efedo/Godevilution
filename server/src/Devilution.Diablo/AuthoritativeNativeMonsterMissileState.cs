namespace Devilution.Diablo;

/** Native missiles created by L5-L8 monsters and by their missiles' expiry. */
public enum AuthoritativeNativeMonsterMissileKind
{
    Arrow = 1,
    Acid = 2,
    AcidSplat = 3,
    AcidPuddle = 4,
    MagmaBall = 5,
    MagmaBallExplosion = 6,
    Lightning = 7,
}

/**
 * Native monster missile state carried on its projectile; RemainingTicks is the native duration.
 * MissileDistance is the Arrow's _midist; LastCollisionTargetHash suppresses repeat collisions on
 * an unchanged tile; Ending is the Acid puddle's End frame group. The source facts are what
 * native reads from the (possibly dead) monster slot.
 */
public sealed record AuthoritativeNativeMonsterMissileState(
    AuthoritativeNativeMonsterMissileKind Kind,
    uint MissileDistance,
    short LastCollisionTargetHash,
    bool Ending,
    int SourcePositionX,
    int SourcePositionY,
    int SourceMonsterLevel,
    int SourceToHit,
    int SourceMinimumDamage,
    int SourceMaximumDamage,
    int SourceIntelligence,
    bool SourceIsPlayerMinion,
    bool SourceIsBerserked)
{
    internal const uint ArrowFlightDuration = 256;

    internal static AuthoritativeNativeMonsterMissileState Create(
        AuthoritativeNativeMonsterMissileKind kind,
        AuthoritativeCombatTarget source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(kind, 0, 0, false, source.PositionX, source.PositionY, source.MonsterLevel, source.ToHit,
            source.MinDamage, source.MaxDamage, source.Intelligence, source.IsPlayerMinion, source.IsBerserked);
    }

    /** The live source's current facts; a dead or removed source keeps its last ones. */
    internal AuthoritativeNativeMonsterMissileState Refresh(AuthoritativeCombatTarget source)
        => this with {
            SourcePositionX = source.PositionX,
            SourcePositionY = source.PositionY,
            SourceMonsterLevel = source.MonsterLevel,
            SourceToHit = source.ToHit,
            SourceMinimumDamage = source.MinDamage,
            SourceMaximumDamage = source.MaxDamage,
            SourceIntelligence = source.Intelligence,
            SourceIsPlayerMinion = source.IsPlayerMinion,
            SourceIsBerserked = source.IsBerserked,
        };

    /** The native missile name used for presentation and evidence. */
    internal string MissileId => Kind.ToString();

    internal void ValidateShape(AuthoritativeProjectile projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        if (!Enum.IsDefined(Kind)
            || LastCollisionTargetHash is < -256 or > 255
            || SourcePositionX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || SourcePositionY is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || SourceMonsterLevel is < 0 or > 255 || SourceToHit is < 0 or > 255
            || SourceMinimumDamage is < 0 or > 10_000
            || SourceMaximumDamage < SourceMinimumDamage || SourceMaximumDamage > 10_000
            || SourceIntelligence is < 0 or > 255
            || Ending && Kind != AuthoritativeNativeMonsterMissileKind.AcidPuddle
            || MissileDistance != 0 && Kind != AuthoritativeNativeMonsterMissileKind.Arrow
            || projectile.NativeMissileId != MissileId
            || projectile.NativeTrapArrow is not null || projectile.NativeWeaponArrow is not null)
            throw new InvalidDataException("A native monster missile is outside its kind, clock or source facts.");
        if (Kind != AuthoritativeNativeMonsterMissileKind.Arrow)
            return;
        var expectedVelocity = Devilution.Engine.DeterministicGridVelocity.CalculateNative(
            projectile.StartX, projectile.StartY, projectile.TargetX, projectile.TargetY, 32);
        if (MissileDistance >= ArrowFlightDuration
            || projectile.RemainingTicks + MissileDistance != ArrowFlightDuration
            || projectile.Behavior != AuthoritativeProjectileBehavior.MonsterArrow
            || projectile.SpellId != 0 || projectile.Phase != AuthoritativeProjectilePhase.Travel
            || projectile.Speed != 32 || projectile.Damage != 0
            || projectile.DamageType != AuthoritativeDamageType.Physical
            || projectile.VelocityX != expectedVelocity.X || projectile.VelocityY != expectedVelocity.Y
            || projectile.TraveledX != checked(expectedVelocity.X * (int)MissileDistance)
            || projectile.TraveledY != checked(expectedVelocity.Y * (int)MissileDistance))
            throw new InvalidDataException("A native monster Arrow is outside its fixed-point flight.");
    }
}
