namespace Devilution.Diablo;

/** Existing Arrow extension, additively carrying source-less L1-L8 object missile stages. */
public sealed record AuthoritativeNativeTrapArrowState(
    int NativeObjectSlot,
    uint MissileDistance,
    short LastCollisionTargetHash)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int MissileTypeId { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IntrinsicChest { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public NativeObjectMissileStage Stage { get; init; }

    /** Validates the complete native payload for both durable state and wire projection. */
    public void ValidateShape(DiabloProjectileState projectile)
    {
        // This family replaces spell-specific validation, never the default-field contract.
        // Comparing the complete record also refuses unrelated spell/controller state.
        var canonical = new DiabloProjectileState(projectile.EntityId, projectile.SourceEntityId,
            projectile.TargetEntityId, projectile.SpellId, projectile.LevelId, projectile.PositionX,
            projectile.PositionY, projectile.TargetX, projectile.TargetY, projectile.Damage,
            projectile.DamageType, projectile.AreaRadius, projectile.RemainingTicks) {
            Behavior = projectile.Behavior, NativeMissileId = projectile.NativeMissileId, Phase = projectile.Phase,
            StartX = projectile.StartX, StartY = projectile.StartY,
            TraveledX = projectile.TraveledX, TraveledY = projectile.TraveledY,
            VelocityX = projectile.VelocityX, VelocityY = projectile.VelocityY,
            OriginalDirection = projectile.OriginalDirection, Speed = projectile.Speed, NativeTrapArrow = this,
        };
        if (projectile.EntityId == 0 || projectile.LevelId == 0 || projectile != canonical)
            throw new InvalidDataException("An object trap missile carries unrelated projectile state.");
        if (MissileTypeId != 0 || IntrinsicChest) {
            ValidateObjectMissileShape(projectile);
            return;
        }
        if (Stage != NativeObjectMissileStage.Flight)
            throw new InvalidDataException("An ordinary Arrow cannot change missile stage.");
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
    private void ValidateObjectMissileShape(DiabloProjectileState projectile)
    {
        if (MissileTypeId is not (0 or 1 or 7 or 27) || !Enum.IsDefined(Stage)
            || IntrinsicChest && MissileTypeId is not (0 or 27)
            || NativeObjectSlot is < 0 or >= 127 || MissileDistance > 256
            || projectile.EntityId == 0 || projectile.SourceEntityId == 0 || projectile.TargetEntityId == 0
            || projectile.SpellId != 0 || projectile.NativeWeaponArrow is not null || projectile.NativeMonsterMissile is not null
            || projectile.RemainingTicks is 0 or > 256 || projectile.AreaRadius != 0
            || projectile.DamageQ6 != 0 || projectile.AttackHit || projectile.PerilSelfDamageQ6 != 0
            || projectile.ElementalFireDamage != 0 || projectile.ElementalLightningDamage != 0
            || projectile.DoppelgangerProc || projectile.ActivationDelayTicks != 0
            || projectile.DamageMinimum != 0 || projectile.DamageMaximum != 0
            || projectile.PathIndex != 0 || projectile.PathTicks != 0
            || projectile.LastCollisionTargetEntityId != 0 || projectile.EffectiveSpellLevel != 0
            || projectile.ImpactLifetime != 1 || projectile.ResistantDamageDivisor != 1
            || projectile.OriginalDirection is < 0 or > 7 || LastCollisionTargetHash is < -256 or > 255
            || (uint)projectile.StartX >= 112 || (uint)projectile.StartY >= 112
            || (uint)projectile.PositionX >= 112 || (uint)projectile.PositionY >= 112
            || (uint)projectile.TargetX >= 112 || (uint)projectile.TargetY >= 112)
            throw new InvalidDataException("An object trap missile has invalid ownership or payload.");
        var expectedType = MissileTypeId == 1 ? AuthoritativeDamageType.Fire
            : MissileTypeId == 7 ? AuthoritativeDamageType.Lightning : AuthoritativeDamageType.Physical;
        var expectedBehavior = MissileTypeId == 1 ? AuthoritativeProjectileBehavior.Firebolt
            : MissileTypeId == 7 ? Stage == NativeObjectMissileStage.LightningSegment
                ? AuthoritativeProjectileBehavior.LightningSegment : AuthoritativeProjectileBehavior.Lightning
            : AuthoritativeProjectileBehavior.WeaponArrow;
        var expectedName = MissileTypeId switch {
            0 => null, 1 => Stage == NativeObjectMissileStage.Explosion ? "MagmaBallExplosion" : "Firebolt",
            7 => Stage == NativeObjectMissileStage.LightningSegment ? "Lightning" : "LightningControl", 27 => "FireArrow",
            _ => throw new InvalidDataException("Unknown object missile."),
        };
        if (projectile.DamageType != expectedType || projectile.Behavior != expectedBehavior
            || projectile.Phase != (Stage is NativeObjectMissileStage.ElementalImpact or NativeObjectMissileStage.Explosion
                ? AuthoritativeProjectilePhase.Impact : AuthoritativeProjectilePhase.Travel)
            || projectile.NativeMissileId != expectedName
            || (MissileTypeId is 0 or 27 && projectile.Damage != 0)
            || projectile.Damage < 0
            || Stage == NativeObjectMissileStage.LightningSegment && (MissileTypeId != 7 || projectile.RemainingTicks > 8)
            || Stage == NativeObjectMissileStage.Explosion && (MissileTypeId != 1 || projectile.RemainingTicks > 10)
            || Stage == NativeObjectMissileStage.ElementalImpact && (MissileTypeId != 27 || projectile.RemainingTicks > 3))
            throw new InvalidDataException("An object trap missile changed its native kind or stage.");
        if (Stage != NativeObjectMissileStage.Flight) {
            var frozenVelocity = DiabloCombatCommandProcessor.NativeObjectMissileVelocity(projectile.StartX,
                projectile.StartY, projectile.TargetX, projectile.TargetY, MissileTypeId == 1 ? 26 : 32);
            var distance = Stage == NativeObjectMissileStage.LightningSegment
                ? frozenVelocity.X != 0 ? projectile.TraveledX / frozenVelocity.X
                    : frozenVelocity.Y != 0 ? projectile.TraveledY / frozenVelocity.Y : -1
                : (int)MissileDistance;
            var traveled = (X: unchecked(frozenVelocity.X * distance), Y: unchecked(frozenVelocity.Y * distance));
            var backedOff = Stage == NativeObjectMissileStage.Explosion
                && projectile.TraveledX == traveled.X - frozenVelocity.X && projectile.TraveledY == traveled.Y - frozenVelocity.Y;
            var frozenTile = DiabloCombatCommandProcessor.NativeTrapArrowTile(projectile.StartX, projectile.StartY,
                projectile.TraveledX, projectile.TraveledY);
            if (projectile.VelocityX != 0 || projectile.VelocityY != 0
                || projectile.Speed != (Stage == NativeObjectMissileStage.ElementalImpact ? 32 : 0)
                || distance < 1 || distance > (Stage == NativeObjectMissileStage.LightningSegment ? 128 : 256)
                || Stage == NativeObjectMissileStage.LightningSegment && MissileDistance != 0
                || !backedOff && (projectile.TraveledX != traveled.X || projectile.TraveledY != traveled.Y)
                || (projectile.PositionX, projectile.PositionY) != frozenTile
                || Stage == NativeObjectMissileStage.Explosion && projectile.Damage != 0
                || Stage == NativeObjectMissileStage.LightningSegment && projectile.Damage is < 2 or > 23)
                throw new InvalidDataException("A stationary object trap missile changed its frozen native trajectory or payload.");
            return;
        }
        if (MissileTypeId != 1 && projectile.Damage != 0
            || MissileTypeId == 1 && projectile.Damage is < 1 or > 23)
            throw new InvalidDataException("An object trap missile changed its native L1-L8 damage range.");
        var speed = MissileTypeId == 1 ? 26 : 32;
        var velocity = DiabloCombatCommandProcessor.NativeObjectMissileVelocity(
            projectile.StartX, projectile.StartY, projectile.TargetX, projectile.TargetY, speed);
        var traveledX = unchecked(velocity.X * (int)MissileDistance);
        var traveledY = unchecked(velocity.Y * (int)MissileDistance);
        var tile = DiabloCombatCommandProcessor.NativeTrapArrowTile(projectile.StartX, projectile.StartY, traveledX, traveledY);
        if (projectile.Speed != speed || projectile.VelocityX != velocity.X || projectile.VelocityY != velocity.Y
            || projectile.TraveledX != traveledX || projectile.TraveledY != traveledY
            || projectile.PositionX != tile.X || projectile.PositionY != tile.Y
            || projectile.RemainingTicks + (MissileTypeId == 7 ? 2 : 1) * MissileDistance != 256)
            throw new InvalidDataException("An object trap missile changed its native flight trajectory.");
    }
}


public enum NativeObjectMissileStage
{
    Flight,
    ElementalImpact,
    Explosion,
    LightningSegment,
}
