using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Elemental, lightning and flash missiles and collision resolution (native missiles.cpp CheckMissileCol). */
public sealed partial class DiabloCombatCommandProcessor
{
    private void AdvanceElemental(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.Elemental) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(projectile, player));
        if (owner is null || owner.LevelId != projectile.LevelId || owner.Life <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            ApplyElementalBurst(tick, projectile, spell, players, events);
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.RemainingTicks == 0) {
            if (projectile.PathIndex == 0) {
                projectile.PathIndex = 1;
                projectile.RemainingTicks = checked((uint)spell.ElementalRetargetLifetime);
                RetargetElemental(projectile, owner);
            } else {
                EnterElementalImpact(projectile, spell);
            }
            return;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                EnterElementalImpact(projectile, spell);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        } else {
            foreach (var tile in visitedTiles) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
                var playerTarget = FindProjectilePlayer(projectile, players, tile.X, tile.Y);
                var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
                if (CheckProjectileCollision(
                        tick,
                        projectile,
                        target,
                        playerTarget,
                        players,
                        events)) {
                    projectile.LastCollisionTargetEntityId = targetEntityId;
                    EnterElementalImpact(projectile, spell);
                    return;
                }
                projectile.LastCollisionTargetEntityId = targetEntityId;
            }
        }

        if (projectile.PathIndex == 0
            && projectile.PositionX == projectile.TargetX
            && projectile.PositionY == projectile.TargetY) {
            projectile.PathIndex = 1;
            projectile.RemainingTicks = checked((uint)spell.ElementalRetargetLifetime);
            RetargetElemental(projectile, owner);
        }
    }

    private void AdvanceGuardian(
        ulong tick,
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.GuardianController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || owner.LevelId != controller.LevelId
            || owner.Life < 0
            || owner.LifeMaximum <= 0
            || owner.Life > owner.LifeMaximum) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;
        if (controller.GuardianState > 0)
            controller.GuardianState--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        if (controller.RemainingTicks % (uint)spell.GuardianFireInterval != 0)
            return;

        var position = (X: controller.PositionX, Y: controller.PositionY);
        var arcOffsets = spell.GuardianArcOffsets.Count > 0
            ? spell.GuardianArcOffsets
            : GuardianArc;
        foreach (var offset in arcOffsets) {
            var candidates = new[] {
                (X: position.X + offset.X, Y: position.Y + offset.Y),
                (X: position.X + offset.Y, Y: position.Y + offset.X),
                (X: position.X + offset.Y, Y: position.Y - offset.X),
                (X: position.X + offset.X, Y: position.Y - offset.Y),
            };
            foreach (var candidate in candidates) {
                if (TryFireGuardianAt(
                        tick,
                        controller,
                        owner,
                        spell,
                        candidate.X,
                        candidate.Y,
                        players,
                        events))
                    return;
            }
        }
    }

    private bool TryFireGuardianAt(
        ulong tick,
        AuthoritativeProjectile controller,
        AuthoritativePlayerState owner,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.HasGuardianTargetLineOfSight(
                controller.LevelId,
                controller.PositionX,
                controller.PositionY,
                targetX,
                targetY))
            return false;
        if (DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                controller.PositionX,
                controller.PositionY) > spell.GuardianTargetSearchRadius)
            return false;
        var target = FindProjectileTarget(controller.LevelId, targetX, targetY);
        if (target is null || target.HitPointsQ6 <= 0 || target.IsPlayerMinion)
            return false;
        // AddGuardian computes a discarded damage roll before AddFirebolt;
        // consume the same native RNG draw to preserve replay parity.
        _ = random.GenerateRnd(spell.RandomDamageRange);
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateGuardianBoltProjectile(
                entityId,
                owner,
                spell,
                GetEffectiveSpellLevel(owner, spell.SpellId),
                controller.PositionX,
                controller.PositionY,
                targetX,
                targetY));
        controller.GuardianState = 3;
        return true;
    }

    private void RetargetElemental(
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState owner)
    {
        var nextMonster = world.CombatTargets.Values
            .Where(target => target.HitPointsQ6 > 0
                && !target.IsPlayerMinion
                && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
                && DeterministicGridMetrics.ChebyshevDistance(
                    target.PositionX,
                    target.PositionY,
                    projectile.PositionX,
                    projectile.PositionY) <= 19)
            .OrderBy(target => DeterministicGridMetrics.ChebyshevDistance(
                target.PositionX,
                target.PositionY,
                projectile.PositionX,
                projectile.PositionY))
            .ThenBy(target => target.EntityId)
            .FirstOrDefault();
        var targetX = nextMonster?.PositionX
            ?? checked(projectile.PositionX
                + DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)owner.FacingDirection).X);
        var targetY = nextMonster?.PositionY
            ?? checked(projectile.PositionY
                + DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)owner.FacingDirection).Y);
        var direction = GetDirection(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == projectile.PositionX && velocityTargetY == projectile.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
            projectile.PositionX,
            projectile.PositionY,
            velocityTargetX,
            velocityTargetY,
            projectile.Speed);
        projectile.TargetX = targetX;
        projectile.TargetY = targetY;
        projectile.OriginalDirection = direction;
        projectile.TraveledX = 0;
        projectile.TraveledY = 0;
        projectile.LastCollisionTargetEntityId = 0;
    }

    private static void EnterElementalImpact(
        AuthoritativeProjectile projectile,
        AuthoritativeSpellDefinition spell)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)spell.ProjectileImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private void ApplyElementalBurst(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var startX = projectile.PathIndex == 1
            ? projectile.ElementalOriginX
            : projectile.StartX;
        var startY = projectile.PathIndex == 1
            ? projectile.ElementalOriginY
            : projectile.StartY;
        var burstOffsets = spell.ElementalBurstOffsets.Count > 0
            ? spell.ElementalBurstOffsets
            : ElementalBurstOffsets;
        foreach (var offset in burstOffsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            if (!world.HasSolidLineOfSight(
                    projectile.LevelId,
                    startX,
                    startY,
                    positionX,
                    positionY)) {
                continue;
            }
            var target = FindProjectileTarget(projectile.LevelId, positionX, positionY);
            var playerTarget = FindProjectilePlayer(projectile, players, positionX, positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                _ = TryResolvePersistentShiftedProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            }
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    positionX,
                    positionY,
                    out var @object)
                && world.CanBreakWorldObject(@object)) {
                @object.Destroyed = true;
            }
        }
    }

    private void CreateLightningSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int damageQ6)
    {
        var segmentLifetime = checked(
            spell.SecondaryProjectileLifetime
            + controller.EffectiveSpellLevel
                / spell.SecondaryProjectileLifetimeSpellLevelDivisor);
        var entityId = AllocateProjectileEntityId();
        var segment = new AuthoritativeProjectile(
            entityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.StartX,
            controller.StartY,
            damageQ6,
            controller.DamageType,
            0,
            (uint)segmentLifetime) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment,
            StartX = controller.StartX,
            StartY = controller.StartY,
            ResistantDamageDivisor = controller.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
        _ = random.RandomIntBetween(1, 8);
        world.Projectiles.Add(entityId, segment);
    }

    private void CreateMonsterLightningSegment(
        AuthoritativeProjectile controller,
        int damage)
    {
        var entityId = AllocateProjectileEntityId();
        var segment = new AuthoritativeProjectile(
            entityId,
            controller.SourceEntityId,
            controller.TargetEntityId,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.StartX,
            controller.StartY,
            damage,
            AuthoritativeDamageType.Lightning,
            0,
            10U) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment,
            StartX = controller.StartX,
            StartY = controller.StartY,
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(entityId, segment);
    }

    private void AdvanceLightningSegment(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.SpellId == 16) {
            var owner = players.FirstOrDefault(player => IsProjectileOwner(projectile, player));
            if (owner is null || owner.LevelId != projectile.LevelId) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            if (!spells.TryGet(projectile.SpellId, out var spell)
                || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ChainLightningController) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
        }

        projectile.RemainingTicks--;
        if (projectile.PositionX != projectile.StartX
            || projectile.PositionY != projectile.StartY) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                if (projectile.TargetEntityId != 0
                    && world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                    var hit = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                    // Native lightning's PlayerMHit consumes the zero-width
                    // damage-range draw after a successful monster hit.
                    if (hit)
                        _ = random.RandomIntBetween(1, 1);
                } else {
                    _ = TryResolvePersistentShiftedProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                }
            }
        }

        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
                && world.CanBreakWorldObject(@object)) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceFlashLayer(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var offsets = projectile.Behavior == AuthoritativeProjectileBehavior.FlashBottom
            ? GetFlashOffsets(projectile.SpellId, bottom: true)
            : GetFlashOffsets(projectile.SpellId, bottom: false);
        foreach (var offset in offsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            var target = FindProjectileTarget(projectile.LevelId, positionX, positionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                positionX,
                positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                var isMonsterOwnedFlash = world.CombatTargets.ContainsKey(projectile.SourceEntityId)
                    && !players.Any(player => IsProjectileOwner(projectile, player));
                if (isMonsterOwnedFlash) {
                    _ = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events,
                        damageShifted: true);
                } else {
                    _ = TryResolvePersistentShiftedProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                }
            }

            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    positionX,
                    positionY,
                    out var @object)
                && world.CanBreakWorldObject(@object)) {
                @object.Destroyed = true;
            }
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private IReadOnlyList<(int X, int Y)> GetFlashOffsets(uint spellId, bool bottom)
    {
        if (spells.TryGet(spellId, out var spell)) {
            var configuredOffsets = bottom
                ? spell.FlashBottomOffsets
                : spell.FlashTopOffsets;
            if (configuredOffsets.Count > 0)
                return configuredOffsets;
        }
        return bottom ? FlashBottomOffsets : FlashTopOffsets;
    }

    private static void EnterProjectileImpact(AuthoritativeProjectile projectile)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = (uint)projectile.ImpactLifetime;
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private bool TryEnterMonsterProjectileImpact(AuthoritativeProjectile projectile)
    {
        if (projectile.ImpactLifetime <= 0
            || !world.CombatTargets.ContainsKey(projectile.SourceEntityId))
            return false;
        EnterProjectileImpact(projectile);
        return true;
    }

    private void AdvanceChargedBolt(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        var pathPattern = spells.TryGet(projectile.SpellId, out var spell)
            && spell.ChargedBoltPathPattern.Count > 0
            ? spell.ChargedBoltPathPattern
            : ChargedBoltPath;

        if (projectile.PathTicks == 0) {
            var direction = projectile.OriginalDirection;
            direction = pathPattern[projectile.PathIndex & 0xF] switch {
                -1 => (direction + 7) % 8,
                1 => (direction + 1) % 8,
                _ => direction,
            };
            projectile.PathIndex = (projectile.PathIndex + 1) & 0xF;
            var displacement = DirectionDisplacements[direction];
            (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
                projectile.PositionX,
                projectile.PositionY,
                projectile.PositionX + displacement.X,
                projectile.PositionY + displacement.Y,
                projectile.Speed);
            projectile.PathTicks = 16;
        } else {
            projectile.PathTicks--;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckChargedBoltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            return;
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckChargedBoltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                return;
            }
        }

        projectile.LastCollisionTargetEntityId = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)?.EntityId
            ?? FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY)?.EntityId
            ?? 0;
    }

    private static IReadOnlyList<(int X, int Y, int TraveledX, int TraveledY)> MoveNativeProjectile(
        AuthoritativeProjectile projectile)
    {
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        projectile.TraveledX = unchecked(projectile.TraveledX + projectile.VelocityX);
        projectile.TraveledY = unchecked(projectile.TraveledY + projectile.VelocityY);
        var finalTile = CalculateMissileTile(
            projectile.StartX,
            projectile.StartY,
            projectile.TraveledX,
            projectile.TraveledY);
        projectile.PositionX = finalTile.X;
        projectile.PositionY = finalTile.Y;

        var possibleVisitedTiles = projectile.VelocityX == 0 || projectile.VelocityY == 0
            ? DeterministicGridMetrics.ChebyshevDistance(
                previousX,
                previousY,
                finalTile.X,
                finalTile.Y)
            : Math.Abs((long)previousX - finalTile.X)
                + Math.Abs((long)previousY - finalTile.Y);
        if (possibleVisitedTiles == 0)
            return [];

        var result = new List<(int X, int Y, int TraveledX, int TraveledY)>();
        if (possibleVisitedTiles > 1) {
            var speedX = Math.Abs((long)projectile.VelocityX);
            var speedY = Math.Abs((long)projectile.VelocityY);
            var denominator = (float)(2 * speedY >= speedX ? 2 * speedY : speedX);
            var velocityFraction = (32 << 16) / denominator;
            var incrementX = unchecked((int)(projectile.VelocityX * velocityFraction));
            var incrementY = unchecked((int)(projectile.VelocityY * velocityFraction));
            if (incrementX != 0 || incrementY != 0) {
                var traveledX = unchecked(projectile.TraveledX - projectile.VelocityX);
                var traveledY = unchecked(projectile.TraveledY - projectile.VelocityY);
                if (incrementX != 0)
                    traveledX = traveledX / incrementX * incrementX;
                if (incrementY != 0)
                    traveledY = traveledY / incrementY * incrementY;
                while (true) {
                    var initialDifferenceX = unchecked(projectile.TraveledX - traveledX);
                    var initialDifferenceY = unchecked(projectile.TraveledY - traveledY);
                    traveledX = unchecked(traveledX + incrementX);
                    traveledY = unchecked(traveledY + incrementY);
                    var incrementedDifferenceX = unchecked(projectile.TraveledX - traveledX);
                    var incrementedDifferenceY = unchecked(projectile.TraveledY - traveledY);
                    if ((initialDifferenceX < 0) != (incrementedDifferenceX < 0)
                        || (initialDifferenceY < 0) != (incrementedDifferenceY < 0)) {
                        break;
                    }

                    var tile = CalculateMissileTile(
                        projectile.StartX,
                        projectile.StartY,
                        traveledX,
                        traveledY);
                    if (tile == finalTile)
                        break;
                    var pixelX = traveledX >> 16;
                    var pixelY = traveledY >> 16;
                    if (pixelY % 16 == 0
                        && pixelX % 32 == 0
                        && Math.Abs(pixelY / 16) % 2
                            != Math.Abs(pixelX / 32) % 2) {
                        continue;
                    }
                    if ((previousX, previousY) == tile
                        || (result.Count > 0
                            && (result[^1].X, result[^1].Y) == tile)) {
                        continue;
                    }
                    result.Add((tile.X, tile.Y, traveledX, traveledY));
                }
            }
        }
        if (result.Count == 0 || (result[^1].X, result[^1].Y) != finalTile) {
            result.Add((
                finalTile.X,
                finalTile.Y,
                projectile.TraveledX,
                projectile.TraveledY));
        }
        return result;
    }

    private static (int X, int Y) CalculateMissileTile(
        int startX,
        int startY,
        int traveledX,
        int traveledY)
    {
        var pixelX = traveledX >> 16;
        var pixelY = traveledY >> 16;
        var xNumerator = checked(2 * pixelY + pixelX);
        var yNumerator = checked(2 * pixelY - pixelX);
        return (
            checked(startX + (xNumerator + (xNumerator >= 0 ? 32 : -32)) / 64),
            checked(startY + (yNumerator + (yNumerator >= 0 ? 32 : -32)) / 64));
    }

    private AuthoritativeCombatTarget? FindProjectileTarget(
        uint levelId,
        int positionX,
        int positionY)
    {
        return world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && (candidate.LevelId == 0 || candidate.LevelId == levelId)
                && ((candidate.PositionX == positionX
                        && candidate.PositionY == positionY)
                    || (candidate.IsMoving
                        && candidate.IsPetrified
                        && candidate.FuturePositionX == positionX
                        && candidate.FuturePositionY == positionY)))
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }

    private static AuthoritativePlayerState? FindProjectilePlayer(
        AuthoritativeProjectile projectile,
        IEnumerable<AuthoritativePlayerState> players,
        int positionX,
        int positionY)
    {
        return players
            .Where(candidate => candidate.Life > 0
                && candidate.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                    or DiabloPlayerCombatMode.Quit)
                && candidate.LevelId == projectile.LevelId
                && !IsProjectileOwner(projectile, candidate)
                && candidate.PositionX == positionX
                && candidate.PositionY == positionY)
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }

    private bool CheckProjectileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        return projectile.Behavior == AuthoritativeProjectileBehavior.HolyBolt
            ? CheckHolyBoltCollision(
                tick,
                projectile,
                target,
                playerTarget,
                players,
                events)
            : projectile.Behavior == AuthoritativeProjectileBehavior.BoneSpirit
                ? CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)
            : CheckFireboltCollision(
                tick,
                projectile,
                target,
                playerTarget,
                players,
                events);
    }

    private bool CheckFireboltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveFireboltHit(projectile, target, players, events);
        var playerHit = false;
        var playerStopsProjectile = false;
        if (!hitTarget && playerTarget is not null) {
            if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar
                && projectile.TargetEntityId != 0
                && world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                playerStopsProjectile = TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            } else if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                playerStopsProjectile = TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            } else {
                playerHit = TryResolveFireboltPlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events,
                    out playerStopsProjectile);
            }
        }

        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && world.CanBreakWorldObject(@object)) {
            @object.Destroyed = true;
        }
        // Hellfire blocks retain the projectile, but still terminate travel in impact.
        return blocked || hitTarget || playerHit || playerStopsProjectile;
    }

    private bool CheckHolyBoltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveHolyBoltHit(projectile, target, players, events);

        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
            && world.CanBreakWorldObject(@object)) {
            @object.Destroyed = true;
        }
        return blocked || hitTarget;
    }

    private bool TryResolveHolyBoltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!target.IsUndead)
            return false;
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var holyBoltSpell)
            && holyBoltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.HolyBolt) {
            hitMonsterLevelScale = holyBoltSpell.HolyBoltHitMonsterLevelScale;
            minimumHitChance = holyBoltSpell.HolyBoltMinimumHitChance;
            maximumHitChance = holyBoltSpell.HolyBoltMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(projectile.Damage);
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    /** Native PlayerMHit magic branch for a monster-owned non-arrow missile. */
    private bool TryResolveMonsterMagicProjectilePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        bool damageShifted = false)
    {
        if (!world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)
            || IsPlayerProtectedByFlash(target)) {
            return false;
        }

        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        var hitChance = rules.ResolveMonsterMagicProjectileHitChance(
            GetEffectiveMonsterLevel(monster),
            target.CharacterLevel,
            target.LevelId,
            distance);
        var hitRoll = random.RandomIntLessThan(100);
        if (hitRoll >= hitChance)
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = !damageShifted && canBlock
            ? random.RandomIntLessThan(100)
            : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - (rules.MonsterPlayerBlockLevelScale * GetEffectiveMonsterLevel(monster)),
            0,
            100);
        if (!damageShifted
            && (resistance <= 0 || rules.GameVariant == DiabloGameVariant.Hellfire)
            && blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }

        var projectileDamage = ResolveMonsterMagicProjectileDamage(
            projectile,
            monster);
        var damageQ6 = checked(
            Math.Max(
                1,
                damageShifted
                    ? random.RandomIntBetween(projectileDamage, projectileDamage)
                    : projectileDamage)
                * DeterministicFixedPointQ6.Scale
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;
        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    /** Native ProcessHorkSpawn collision: a zero-payload non-arrow PlayerMHit. */
    private bool TryResolveHorkSpawnPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)
            || IsPlayerProtectedByFlash(target))
            return false;

        var distance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        if (random.RandomIntLessThan(100)
            >= rules.ResolveMonsterMagicProjectileHitChance(
                GetEffectiveMonsterLevel(monster),
                target.CharacterLevel,
                target.LevelId,
                distance))
            return false;

        var targetClass = GetPlayerClass(target);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - (rules.MonsterPlayerBlockLevelScale * GetEffectiveMonsterLevel(monster)),
            0,
            100);
        if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }

        // PlayerMHit always rolls the zero-width damage range before applying
        // its one-point minimum, so preserve that RNG consumption.
        var damageQ6 = checked(
            Math.Max(64, random.RandomIntBetween(0, 0))
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        var damageResult = playerDamage.Apply(target, damageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    /** Native monster Fireball rolls its damage range at each collision. */
    private int ResolveMonsterMagicProjectileDamage(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster)
    {
        if (projectile.Behavior != AuthoritativeProjectileBehavior.Fireball
            || !TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.Fireball
            || missile.ProjectileDamage > 0)
            return projectile.Damage;

        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        return random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum);
    }

    /** Native PlayerMHit branch used by the persistent AcidPuddle missile. */
    private bool TryResolveAcidPuddlePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (IsPlayerProtectedByFlash(target))
            return false;

        if (random.RandomIntLessThan(100)
            >= rules.ResolveMonsterAcidPuddleHitChance(target.LevelId))
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            AuthoritativeDamageType.Acid);
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage)
                * DeterministicFixedPointQ6.Scale
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    projectile.SourceEntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                projectile.SourceEntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    private bool CheckBoneSpiritCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveBoneSpiritHit(projectile, target, players, events);
        var playerHit = playerTarget is not null
            && TryResolveBoneSpiritPlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
            && world.CanBreakWorldObject(@object)) {
            @object.Destroyed = true;
        }
        return blocked || hitTarget || playerHit;
    }

    private bool TryResolveBoneSpiritHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        var damageHitPointsDivisor = 3;
        if (spells.TryGet(projectile.SpellId, out var boneSpiritSpell)
            && boneSpiritSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.BoneSpirit) {
            hitMonsterLevelScale = boneSpiritSpell.BoneSpiritHitMonsterLevelScale;
            minimumHitChance = boneSpiritSpell.BoneSpiritMinimumHitChance;
            maximumHitChance = boneSpiritSpell.BoneSpiritMaximumHitChance;
            damageHitPointsDivisor = boneSpiritSpell.BoneSpiritDamageHitPointsDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        if (!target.IsPetrified
            && random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = target.HitPointsQ6 / damageHitPointsDivisor / DeterministicFixedPointQ6.Scale
            * DeterministicFixedPointQ6.Scale;
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolveBoneSpiritPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode)) {
            return false;
        }

        var damageHitPointsDivisor = 3;
        if (spells.TryGet(projectile.SpellId, out var boneSpiritSpell)
            && boneSpiritSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.BoneSpirit) {
            damageHitPointsDivisor = boneSpiritSpell.BoneSpiritPlayerDamageHitPointsDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
                - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        // Native Plr2PlrMHit computes Bone Spirit damage from current life and
        // then applies the shared non-arrow half-damage step.
        var damageQ6 = target.Life / damageHitPointsDivisor / 2;
        if (resistance > 0) {
            damageQ6 -= damageQ6 * resistance / 100;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire)
                _ = random.RandomIntLessThan(2);
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private bool TryResolveFireboltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.HitPointsQ6 <= 0
            || target.DialogueMessageId != 0
            || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target)
            || IsNativeRetreatTarget(target)) {
            return false;
        }

        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var fireboltSpell)
            && fireboltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.Firebolt) {
            hitMonsterLevelScale = fireboltSpell.FireboltHitMonsterLevelScale;
            minimumHitChance = fireboltSpell.FireboltMinimumHitChance;
            maximumHitChance = fireboltSpell.FireboltMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (UsesNativeAi(target, "Gargoyle") && IsNativeGargoyleWakePending(target)) {
            // MonsterMHit consumes the hit roll, then tryLiftGargoyle clears
            // MFLAG_ALLOW_SPECIAL and reports a collision before hit testing
            // or the fixed Apocalypse damage roll.
            _ = ConsumeNativeGargoyleWake(target);
            return true;
        }
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(
            random.RandomIntBetween(projectile.Damage, projectile.Damage));
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolveFireboltPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        out bool stopsProjectile)
    {
        stopsProjectile = false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        // Devilution rolls Firebolt damage before the player block roll.
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage) * 32);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var targetClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0)
            + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
            - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        if (resistance > 0) {
            damageQ6 -= damageQ6 * resistance / 100;
            stopsProjectile = true;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                _ = random.RandomIntLessThan(2);
            } else {
                stopsProjectile = true;
            }
            return true;
        } else {
            stopsProjectile = true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    /** Resolves NovaBall's native zero-distance MonsterMHit damage path. */
    private bool TryResolveNovaBallMonsterHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var ownerClass = GetPlayerClass(owner);
        // Native Nova balls never increment Missile::_midist, so MonsterMHit's
        // distance penalty is zero for this projectile family.
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * 2,
            5,
            95);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        // Native MonsterMHit rolls one fixed-width damage value, shifts it to
        // Q6, and quarters positive elemental resistance.
        var damageWhole = random.RandomIntBetween(
            projectile.Damage / DeterministicFixedPointQ6.Scale,
            projectile.Damage / DeterministicFixedPointQ6.Scale);
        var damageQ6 = checked(damageWhole * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damageQ6 /= Math.Max(projectile.ResistantDamageDivisor, 1);

        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    /** Native NovaBall PvP uses Plr2PlrMHit's non-arrow, unshifted damage path. */
    private bool TryResolveNovaBallPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode)) {
            return false;
        }

        var ownerClass = GetPlayerClass(owner);
        // Native ProcessNovaBall never increments Missile::_midist, so its
        // Plr2PlrMHit accuracy has no travel-distance penalty.
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var targetClass = GetPlayerClass(target);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
                - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);

        // Nova stores its scaled payload in Q6; native Plr2PlrMHit first rolls
        // the fixed-width whole damage, then shifts it to Q6 and halves it.
        var damageRoll = random.RandomIntBetween(
            projectile.Damage / DeterministicFixedPointQ6.Scale,
            projectile.Damage / DeterministicFixedPointQ6.Scale);
        var damageQ6 = checked(damageRoll * 32);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        if (resistance > 0) {
            damageQ6 -= damageQ6 * resistance / 100;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire)
                _ = random.RandomIntLessThan(2);
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private static int GetMonsterResistance(
        AuthoritativeCombatTarget target,
        AuthoritativeDamageType damageType)
    {
        return damageType switch {
            AuthoritativeDamageType.Fire => target.FireResistance,
            AuthoritativeDamageType.Lightning => target.LightningResistance,
            AuthoritativeDamageType.Magic => target.MagicResistance,
            AuthoritativeDamageType.Acid => target.AcidResistance,
            _ => 0,
        };
    }

    private bool TryResolvePersistentShiftedProjectileHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var persistentSpell)) {
            hitMonsterLevelScale = persistentSpell.PersistentProjectileHitMonsterLevelScale;
            minimumHitChance = persistentSpell.PersistentProjectileMinimumHitChance;
            maximumHitChance = persistentSpell.PersistentProjectileMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = random.RandomIntBetween(projectile.Damage, projectile.Damage);
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolvePersistentShiftedProjectilePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var hitCharacterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        var damageDivisor = 2;
        if (spells.TryGet(projectile.SpellId, out var persistentSpell)) {
            hitCharacterLevelScale = persistentSpell.PersistentProjectileHitCharacterLevelScale;
            minimumHitChance = persistentSpell.PersistentProjectilePlayerMinimumHitChance;
            maximumHitChance = persistentSpell.PersistentProjectilePlayerMaximumHitChance;
            damageDivisor = persistentSpell.PersistentProjectilePlayerDamageDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - checked((int)target.CharacterLevel * hitCharacterLevelScale),
            minimumHitChance,
            maximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = random.RandomIntBetween(projectile.Damage, projectile.Damage) / damageDivisor;
        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private bool CheckChargedBoltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = projectile.Behavior != AuthoritativeProjectileBehavior.MonsterChargedBolt
            && target is not null
            && TryResolveChargedBoltHit(projectile, target, players, events);
        if (!hitTarget && playerTarget is not null) {
            hitTarget = projectile.Behavior == AuthoritativeProjectileBehavior.MonsterChargedBolt
                ? TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events)
                : TryResolveChargedBoltPlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
        }
        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
                && world.CanBreakWorldObject(@object)) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return true;
        }
        if (!hitTarget)
            return false;

        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = (uint)projectile.ImpactLifetime;
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
        return true;
    }

    private bool TryResolveChargedBoltPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var targetClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0)
            + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
            - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage) * 32);
        var lightningResistance = GetLightningResistance(target, targetClass);
        if (lightningResistance > 0) {
            damageQ6 -= damageQ6 * lightningResistance / 100;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire)
                _ = random.RandomIntLessThan(2);
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (lightningResistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }
}
