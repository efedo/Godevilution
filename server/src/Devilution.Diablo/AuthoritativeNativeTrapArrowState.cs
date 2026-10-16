namespace Devilution.Diablo;

/** Source-less native L1 Arrow clock and signed occupancy collision suppression. */
public sealed record AuthoritativeNativeTrapArrowState(
    int NativeObjectSlot,
    uint MissileDistance,
    short LastCollisionTargetHash)
{
    internal void ValidateShape(DiabloProjectileState projectile)
    {
        if (NativeObjectSlot is < 0 or >= 127 || MissileDistance > 256
            || projectile.NativeWeaponArrow is not null
            || projectile.Behavior != AuthoritativeProjectileBehavior.WeaponArrow
            || projectile.Phase != AuthoritativeProjectilePhase.Travel
            || projectile.SourceEntityId == 0 || projectile.TargetEntityId == 0
            || projectile.SpellId != 0 || projectile.NativeMissileId is not null
            || projectile.DamageType != AuthoritativeDamageType.Physical
            || projectile.Damage != 0 || projectile.DamageQ6 != 0 || projectile.AttackHit
            || projectile.ElementalFireDamage != 0 || projectile.ElementalLightningDamage != 0
            || projectile.PerilSelfDamageQ6 != 0 || projectile.DoppelgangerProc
            || projectile.AreaRadius != 0 || projectile.Speed != 32
            || projectile.RemainingTicks == 0 || projectile.RemainingTicks > 256
            || projectile.RemainingTicks + MissileDistance != 256
            || projectile.ActivationDelayTicks != 0
            || projectile.VelocityX == 0 && projectile.VelocityY == 0
            || projectile.OriginalDirection is < 0 or > 7
            || projectile.PathIndex != 0 || projectile.PathTicks != 0
            || projectile.LastCollisionTargetEntityId != 0
            || LastCollisionTargetHash is < -256 or > 255
            || (uint)projectile.StartX >= 112 || (uint)projectile.StartY >= 112
            || (uint)projectile.TargetX >= 112 || (uint)projectile.TargetY >= 112
            || (uint)projectile.PositionX >= 112 || (uint)projectile.PositionY >= 112)
            throw new InvalidDataException("A native trap arrow has invalid ownership, payload or flight state.");
        var velocity = DiabloCombatCommandProcessor.NativeTrapArrowVelocity(
            projectile.StartX, projectile.StartY, projectile.TargetX, projectile.TargetY);
        var traveledX = unchecked(velocity.X * (int)MissileDistance);
        var traveledY = unchecked(velocity.Y * (int)MissileDistance);
        var position = DiabloCombatCommandProcessor.NativeTrapArrowTile(
            projectile.StartX, projectile.StartY, traveledX, traveledY);
        var direction = (int)Devilution.Engine.DeterministicGridDirections.Toward(
            projectile.StartX, projectile.StartY, projectile.TargetX, projectile.TargetY);
        if (projectile.VelocityX != velocity.X || projectile.VelocityY != velocity.Y
            || projectile.TraveledX != traveledX || projectile.TraveledY != traveledY
            || projectile.PositionX != position.X || projectile.PositionY != position.Y
            || projectile.OriginalDirection != direction)
            throw new InvalidDataException("A native trap arrow changed its frozen native flight trajectory.");
    }
}
