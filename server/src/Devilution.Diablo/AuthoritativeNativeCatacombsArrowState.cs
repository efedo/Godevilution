namespace Devilution.Diablo;

/** Detached identity and impact-time source facts for one native C2 monster Arrow. */
public sealed record AuthoritativeNativeCatacombsArrowState(
    uint ProjectileEntityId,
    uint SourceEntityId,
    int NativeSlot,
    uint ContentMonsterId,
    int NativeTypeId,
    uint LevelId,
    int Depth,
    uint TargetEntityId,
    int CharacterClassId,
    string NativeAi,
    string MetadataFingerprint,
    string PopulationFingerprint,
    string MissileMappingFingerprint,
    int StartX,
    int StartY,
    int RequestedTargetX,
    int RequestedTargetY,
    int LaunchFacingDirection,
    int DestinationX,
    int DestinationY,
    int Direction16,
    uint MissileDistance,
    short LastCollisionTargetHash,
    int SourcePositionX,
    int SourcePositionY,
    int SourceMonsterLevel,
    int SourceToHit,
    int SourceMinimumDamage,
    int SourceMaximumDamage,
    bool SourceIsPlayerMinion,
    bool SourceIsBerserked)
{
    private const int LevelWidth = AuthoritativeCathedralThemeRoomState.Width;
    private const uint FlightDuration = 256;

    internal static AuthoritativeNativeCatacombsArrowState Create(
        AuthoritativeCombatTarget source,
        AuthoritativeProjectile projectile,
        AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile,
        AuthoritativeNativePopulationBindings bindings,
        int characterClassId,
        int direction16)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(missile);
        ArgumentNullException.ThrowIfNull(bindings);
        if (characterClassId is not (0 or 2))
            throw new InvalidDataException("A native Catacombs Arrow requires a bound Warrior or Sorcerer profile.");
        var initialization = source.NativeInitialization
            ?? throw new InvalidDataException("A native Catacombs Arrow source lost its initialization.");
        initialization.ValidateTarget(source.EntityId, source.MonsterId, source.LevelId,
            source.IsUnique, source.UniqueIdentity);
        var effectiveDestination = ResolveEffectiveDestination(
            source.PositionX, source.PositionY, action.TargetX, action.TargetY, source.Direction);
        var expectedDirection16 = ResolveDirection16(
            source.PositionX, source.PositionY, effectiveDestination.X, effectiveDestination.Y);
        if (projectile.StartX != source.PositionX || projectile.StartY != source.PositionY
            || projectile.TargetX != effectiveDestination.X || projectile.TargetY != effectiveDestination.Y
            || direction16 != expectedDirection16)
            throw new InvalidDataException("A native Catacombs Arrow changed its requested or effective launch direction.");
        var state = new AuthoritativeNativeCatacombsArrowState(
            projectile.EntityId,
            source.EntityId,
            initialization.Binding.NativeSlot,
            source.MonsterId,
            initialization.Binding.NativeTypeId,
            source.LevelId,
            bindings.Depth,
            action.TargetEntityId,
            characterClassId,
            source.NativeAi ?? string.Empty,
            initialization.MetadataFingerprint,
            bindings.Fingerprint,
            AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint(missile),
            projectile.StartX,
            projectile.StartY,
            action.TargetX,
            action.TargetY,
            source.Direction,
            projectile.TargetX,
            projectile.TargetY,
            direction16,
            0,
            0,
            source.PositionX,
            source.PositionY,
            source.MonsterLevel,
            source.ToHit,
            source.MinDamage,
            source.MaxDamage,
            source.IsPlayerMinion,
            source.IsBerserked);
        state.ValidateShape();
        state.ValidateProjectile(projectile);
        state.ValidateSource(source, bindings);
        return state;
    }

    public void ValidateShape()
    {
        if (ProjectileEntityId == 0 || SourceEntityId == 0 || NativeSlot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || ContentMonsterId == 0 || NativeTypeId is < 0 or >= AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount
            || LevelId == 0 || Depth is < 5 or > 8 || TargetEntityId == 0
            || CharacterClassId is not (0 or 2)
            || NativeAi is not ("SkeletonRanged" or "GoatRanged")
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(MetadataFingerprint)
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(PopulationFingerprint)
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(MissileMappingFingerprint)
            || !InBounds(StartX, StartY) || !InBounds(RequestedTargetX, RequestedTargetY)
            || LaunchFacingDirection is < 0 or >= 8 || !InBounds(DestinationX, DestinationY)
            || Direction16 is < 0 or > 15 || MissileDistance >= FlightDuration
            || StartX == DestinationX && StartY == DestinationY
            || ResolveEffectiveDestination(StartX, StartY, RequestedTargetX, RequestedTargetY,
                    LaunchFacingDirection) != (DestinationX, DestinationY)
            || Direction16 != ResolveDirection16(StartX, StartY, DestinationX, DestinationY)
            || !InBounds(SourcePositionX, SourcePositionY)
            || SourceMonsterLevel is < 0 or > 255 || SourceToHit is < 0 or > 255
            || SourceMinimumDamage is < 0 or > 10_000
            || SourceMaximumDamage < SourceMinimumDamage || SourceMaximumDamage > 10_000)
            throw new InvalidDataException("A native Catacombs Arrow is outside its bound source, floor, or flight identity.");
    }

    internal void ValidateProjectile(AuthoritativeProjectile projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        ValidateShape();
        var expectedVelocity = Devilution.Engine.DeterministicGridVelocity.CalculateNative(
            StartX, StartY, DestinationX, DestinationY, 32);
        var expectedPosition = CalculateTile(StartX, StartY, projectile.TraveledX, projectile.TraveledY);
        var expectedTraveledX = checked(expectedVelocity.X * checked((int)MissileDistance));
        var expectedTraveledY = checked(expectedVelocity.Y * checked((int)MissileDistance));
        if (projectile.EntityId != ProjectileEntityId || projectile.SourceEntityId != SourceEntityId
            || projectile.TargetEntityId != TargetEntityId || projectile.LevelId != LevelId
            || projectile.SpellId != 0 || projectile.Behavior != AuthoritativeProjectileBehavior.MonsterArrow
            || projectile.NativeMissileId != "Arrow" || projectile.Phase != AuthoritativeProjectilePhase.Travel
            || projectile.Speed != 32 || projectile.ImpactLifetime != 0
            || projectile.StartX != StartX || projectile.StartY != StartY
            || projectile.TargetX != DestinationX || projectile.TargetY != DestinationY
            || projectile.VelocityX != expectedVelocity.X || projectile.VelocityY != expectedVelocity.Y
            || projectile.TraveledX != expectedTraveledX || projectile.TraveledY != expectedTraveledY
            || projectile.OriginalDirection != Direction16 / 2
            || projectile.Damage != 0 || projectile.DamageMinimum != 0 || projectile.DamageMaximum != 0
            || projectile.RemainingTicks == 0 || projectile.RemainingTicks > FlightDuration
            || projectile.RemainingTicks + MissileDistance != FlightDuration
            || projectile.DamageType != AuthoritativeDamageType.Physical || projectile.AreaRadius != 0
            || projectile.DamageQ6 != 0
            || projectile.ElementalFireDamage != 0 || projectile.ElementalLightningDamage != 0
            || projectile.AttackHit || projectile.NativeWeaponArrow is not null
            || projectile.NativeTrapArrow is not null || projectile.PerilSelfDamageQ6 != 0
            || projectile.DoppelgangerProc || projectile.ActivationDelayTicks != 0
            || projectile.ResistantDamageDivisor != 1 || projectile.EffectiveSpellLevel != 0
            || projectile.LastCollisionTargetEntityId != 0
            || projectile.PathIndex != 0 || projectile.PathTicks != 0
            || projectile.WallLeftX != 0 || projectile.WallLeftY != 0
            || projectile.WallRightX != 0 || projectile.WallRightY != 0
            || projectile.WallLeftDirection != -1 || projectile.WallRightDirection != -1
            || projectile.WallLeftStopped || projectile.WallRightStopped || projectile.WallHitDirection != -1
            || projectile.ApocalypseScanX != 0 || projectile.ApocalypseScanY != 0
            || projectile.ApocalypseHit || projectile.RingOfFireLimitReached || projectile.GuardianState != 0
            || projectile.ElementalOriginX != 0 || projectile.ElementalOriginY != 0
            || !InBounds(expectedPosition.X, expectedPosition.Y)
            || projectile.PositionX != expectedPosition.X || projectile.PositionY != expectedPosition.Y)
            throw new InvalidDataException("A native Catacombs Arrow projectile changed its owned fixed-point flight.");
    }

    internal void ValidateSource(AuthoritativeCombatTarget source, AuthoritativeNativePopulationBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bindings);
        ValidateShape();
        if (bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.LevelId != LevelId || bindings.Depth != Depth
            || bindings.Fingerprint != PopulationFingerprint
            || bindings.MonsterMetadataFingerprint != MetadataFingerprint)
            throw new InvalidDataException("A native Catacombs Arrow no longer matches its admitted source actor.");
        ValidateSourceIdentity(source, bindings);
        if (source.PositionX != SourcePositionX || source.PositionY != SourcePositionY
            || source.MonsterLevel != SourceMonsterLevel || source.ToHit != SourceToHit
            || source.MinDamage != SourceMinimumDamage || source.MaxDamage != SourceMaximumDamage
            || source.IsPlayerMinion != SourceIsPlayerMinion || source.IsBerserked != SourceIsBerserked)
            throw new InvalidDataException("A native Catacombs Arrow has stale live source facts.");
    }

    internal void ValidateSourceIdentity(
        AuthoritativeCombatTarget source,
        AuthoritativeNativePopulationBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Family != AuthoritativeNativePopulationFamily.Catacombs
            || bindings.LevelId != LevelId || bindings.Depth != Depth
            || bindings.Fingerprint != PopulationFingerprint
            || bindings.MonsterMetadataFingerprint != MetadataFingerprint
            || source.EntityId != SourceEntityId || source.MonsterId != ContentMonsterId || source.LevelId != LevelId
            || source.NativeAi != NativeAi
            || source.NativeInitialization is not { } initialization
            || initialization.CatacombsDepth != Depth
            || initialization.Binding.NativeSlot != NativeSlot
            || initialization.Binding.NativeTypeId != NativeTypeId
            || initialization.MetadataFingerprint != MetadataFingerprint)
            throw new InvalidDataException("A native Catacombs Arrow no longer matches its admitted source actor.");
        initialization.ValidateTarget(source.EntityId, source.MonsterId, source.LevelId,
            source.IsUnique, source.UniqueIdentity);
    }

    internal AuthoritativeNativeCatacombsArrowState RefreshSource(AuthoritativeCombatTarget source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.EntityId != SourceEntityId || source.LevelId != LevelId
            || source.NativeInitialization is not { } initialization
            || initialization.Binding.NativeSlot != NativeSlot
            || initialization.Binding.NativeTypeId != NativeTypeId
            || initialization.MetadataFingerprint != MetadataFingerprint
            || source.MonsterId != ContentMonsterId || source.NativeAi != NativeAi)
            throw new InvalidDataException("A native Catacombs Arrow cannot rebind to changed source facts.");
        initialization.ValidateTarget(source.EntityId, source.MonsterId, source.LevelId,
            source.IsUnique, source.UniqueIdentity);
        var refreshed = this with {
            SourcePositionX = source.PositionX,
            SourcePositionY = source.PositionY,
            SourceMonsterLevel = source.MonsterLevel,
            SourceToHit = source.ToHit,
            SourceMinimumDamage = source.MinDamage,
            SourceMaximumDamage = source.MaxDamage,
            SourceIsPlayerMinion = source.IsPlayerMinion,
            SourceIsBerserked = source.IsBerserked,
        };
        refreshed.ValidateShape();
        return refreshed;
    }

    internal AuthoritativeNativeCatacombsArrowState Advance(uint distance, short collisionHash)
    {
        ValidateShape();
        if (distance != MissileDistance + 1 || distance >= FlightDuration)
            throw new InvalidDataException("A native Catacombs Arrow distance must advance once per ProcessArrow tick.");
        return this with { MissileDistance = distance, LastCollisionTargetHash = collisionHash };
    }

    internal static int ResolveDirection16(int startX, int startY, int destinationX, int destinationY)
    {
        var offsetX = destinationX - startX;
        var offsetY = destinationY - startY;
        var absoluteX = Math.Abs(offsetX);
        var absoluteY = Math.Abs(offsetY);
        var flipY = offsetX != absoluteX;
        var flipX = offsetY != absoluteY;
        var flipMedian = false;
        if (absoluteX > absoluteY) {
            (absoluteX, absoluteY) = (absoluteY, absoluteX);
            flipMedian = true;
        }

        var direction = 0;
        if (3 * absoluteX <= 2 * absoluteY)
            direction = 5 * absoluteX < absoluteY ? 2 : 1;
        var medianPivot = 0;
        if (flipY) {
            direction = FlipDirection16(direction, 2);
            medianPivot = FlipDirection16(medianPivot, 2);
        }
        if (flipX) {
            direction = FlipDirection16(direction, 14);
            medianPivot = FlipDirection16(medianPivot, 14);
        }
        if (flipMedian)
            direction = FlipDirection16(direction, medianPivot);
        return direction;
    }

    internal static (int X, int Y) ResolveEffectiveDestination(
        int startX,
        int startY,
        int requestedTargetX,
        int requestedTargetY,
        int launchFacingDirection)
    {
        if (!InBounds(startX, startY) || !InBounds(requestedTargetX, requestedTargetY))
            throw new InvalidDataException("A native Catacombs Arrow requested a tile outside its admitted floor.");
        if (startX != requestedTargetX || startY != requestedTargetY)
            return (requestedTargetX, requestedTargetY);
        if (launchFacingDirection is < 0 or >= 8)
            throw new InvalidDataException("A same-cell Catacombs Arrow needs a valid launch-facing direction.");
        var displacement = Devilution.Engine.DeterministicGridDirections.GetDisplacement(
            (Devilution.Engine.DeterministicGridDirection)launchFacingDirection);
        var effectiveX = checked(startX + displacement.X);
        var effectiveY = checked(startY + displacement.Y);
        if (!InBounds(effectiveX, effectiveY))
            throw new InvalidDataException("A same-cell Catacombs Arrow fallback points outside its admitted floor.");
        return (effectiveX, effectiveY);
    }

    private static int FlipDirection16(int direction, int pivot)
        => (2 * pivot + 16 - direction) % 16;

    private static (int X, int Y) CalculateTile(int startX, int startY, int traveledX, int traveledY)
    {
        var pixelX = traveledX >> 16;
        var pixelY = traveledY >> 16;
        var xNumerator = checked(2 * pixelY + pixelX);
        var yNumerator = checked(2 * pixelY - pixelX);
        return (
            checked(startX + (xNumerator + (xNumerator >= 0 ? 32 : -32)) / 64),
            checked(startY + (yNumerator + (yNumerator >= 0 ? 32 : -32)) / 64));
    }

    private static bool InBounds(int x, int y)
        => (uint)x < LevelWidth && (uint)y < LevelWidth;
}
