using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Spell controller and segment missiles (native missiles.cpp Mi_* functions). */
public sealed partial class DiabloCombatCommandProcessor
{
    private void AdvanceBoneSpirit(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var expiresAfterMovement = projectile.RemainingTicks == 0;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(
                projectile.LevelId,
                tile.X,
                tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                if (projectile.PathIndex == 0
                    && tile.X == projectile.TargetX
                    && tile.Y == projectile.TargetY) {
                    RetargetBoneSpirit(projectile, players);
                } else {
                    EnterBoneSpiritImpact(projectile);
                }
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        }

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
                && CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                if (projectile.PathIndex == 0
                    && projectile.PositionX == projectile.TargetX
                    && projectile.PositionY == projectile.TargetY) {
                    RetargetBoneSpirit(projectile, players);
                } else {
                    EnterBoneSpiritImpact(projectile);
                }
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        }

        if (projectile.PathIndex == 0
            && projectile.PositionX == projectile.TargetX
            && projectile.PositionY == projectile.TargetY) {
            RetargetBoneSpirit(projectile, players);
        }
        if (expiresAfterMovement)
            EnterBoneSpiritImpact(projectile);
    }

    private void AdvanceRingOfFireController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        controller.RemainingTicks--;
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.RingOfFireController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        // ProcessRingOfFire computes a damage value that AddFireWall replaces,
        // but the two native GenerateRnd(10) draws still advance the shared RNG.
        _ = random.GenerateRnd(10);
        _ = random.GenerateRnd(10);
        if (!controller.RingOfFireLimitReached) {
            foreach (var displacement in DeterministicGridCrawl.EnumerateDisplacements(
                         spell.RingOfFireSearchRadius,
                         spell.RingOfFireSearchRadius)) {
                var targetX = checked(controller.StartX + displacement.X);
                var targetY = checked(controller.StartY + displacement.Y);
                if (!world.ContainsPosition(controller.LevelId, targetX, targetY)
                    || !world.IsWalkable(controller.LevelId, targetX, targetY))
                    continue;
                if (world.IsCathedralLightObjectSolid(controller.LevelId, targetX, targetY)
                    || world.Objects.Values.Any(@object => !@object.Activated
                        && !@object.Destroyed
                        && @object.LevelId == controller.LevelId
                        && @object.PositionX == targetX
                        && @object.PositionY == targetY))
                    continue;
                if (!world.HasMissileLineOfSight(
                        controller.LevelId,
                        controller.PositionX,
                        controller.PositionY,
                        targetX,
                        targetY))
                    continue;
                if (world.IsMissileGeometryBlocked(controller.LevelId, targetX, targetY)) {
                    controller.RingOfFireLimitReached = true;
                    break;
                }

                var segmentEntityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    segmentEntityId,
                    CreateWallSegment(
                        segmentEntityId,
                        controller,
                        spell,
                        owner,
                        targetX,
                        targetY,
                        0,
                        -1));
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private AuthoritativeProjectile CreateImmolationController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++)
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        damage = checked(
            damage
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        damage /= spell.MagicDamageDivisor;
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage * spell.DamageScale);

        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        var direction = GetDirection(player.PositionX, player.PositionY, targetX, targetY);
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.ImmolationControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ImmolationController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateImmolationBall(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY);
        var (velocityX, velocityY) = CalculateVelocity(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            controller.Damage,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            // FireballBow uses the native persistent fireball movement and impact path.
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private void AdvanceFireWallController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior is not (AuthoritativeProjectileBehavior.FireWallController
                or AuthoritativeProjectileBehavior.LightningWallController)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var leftPosition = (X: controller.WallLeftX, Y: controller.WallLeftY);
        var rightPosition = (X: controller.WallRightX, Y: controller.WallRightY);
        if (leftPosition == rightPosition) {
            if (!CanPlaceWall(controller.LevelId, leftPosition.X, leftPosition.Y)) {
                world.Projectiles.Remove(controller.EntityId);
                return;
            }
            PlaceWallSegment(
                controller,
                spell,
                owner,
                leftPosition.X,
                leftPosition.Y,
                controller.OriginalDirection,
                -1);
            controller.WallLeftX = checked(leftPosition.X
                + DirectionDisplacements[controller.WallLeftDirection].X);
            controller.WallLeftY = checked(leftPosition.Y
                + DirectionDisplacements[controller.WallLeftDirection].Y);
            controller.WallRightX = checked(rightPosition.X
                + DirectionDisplacements[controller.WallRightDirection].X);
            controller.WallRightY = checked(rightPosition.Y
                + DirectionDisplacements[controller.WallRightDirection].Y);
            return;
        }

        if (!controller.WallLeftStopped) {
            if (TryGrowWall(
                    controller,
                    spell,
                    owner,
                    leftPosition.X,
                    leftPosition.Y,
                    controller.WallLeftDirection)) {
                controller.WallLeftX = checked(leftPosition.X
                    + DirectionDisplacements[controller.WallLeftDirection].X);
                controller.WallLeftY = checked(leftPosition.Y
                    + DirectionDisplacements[controller.WallLeftDirection].Y);
            } else {
                controller.WallLeftStopped = true;
            }
        }
        if (!controller.WallRightStopped) {
            if (TryGrowWall(
                    controller,
                    spell,
                    owner,
                    rightPosition.X,
                    rightPosition.Y,
                    controller.WallRightDirection)) {
                controller.WallRightX = checked(rightPosition.X
                    + DirectionDisplacements[controller.WallRightDirection].X);
                controller.WallRightY = checked(rightPosition.Y
                    + DirectionDisplacements[controller.WallRightDirection].Y);
            } else {
                controller.WallRightStopped = true;
            }
        }
    }

    private void AdvanceFlameWaveController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.FlameWaveController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var spreadDirection = GetDirection(
            controller.PositionX,
            controller.PositionY,
            controller.TargetX,
            controller.TargetY);
        var start = (
            X: checked(controller.PositionX + DirectionDisplacements[spreadDirection].X),
            Y: checked(controller.PositionY + DirectionDisplacements[spreadDirection].Y));
        if (CanPlaceFlameWaveSegment(controller.LevelId, start.X, start.Y)) {
            PlaceFlameWaveSegment(controller, spell, owner, start.X, start.Y);
            var branchCount = checked(
                spell.FlameWaveBranchBase
                + controller.EffectiveSpellLevel / spell.FlameWaveBranchSpellLevelDivisor);
            var leftDirection = RotateDirection(spreadDirection, -2);
            var rightDirection = RotateDirection(spreadDirection, 2);
            var left = start;
            for (var branch = 0; branch < branchCount; branch++) {
                left = (
                    checked(left.X + DirectionDisplacements[leftDirection].X),
                    checked(left.Y + DirectionDisplacements[leftDirection].Y));
                if (!TryGrowFlameWave(
                        controller,
                        spell,
                        owner,
                        left.X,
                        left.Y,
                        leftDirection))
                    break;
            }
            var right = start;
            for (var branch = 0; branch < branchCount; branch++) {
                right = (
                    checked(right.X + DirectionDisplacements[rightDirection].X),
                    checked(right.Y + DirectionDisplacements[rightDirection].Y));
                if (!TryGrowFlameWave(
                        controller,
                        spell,
                        owner,
                        right.X,
                        right.Y,
                        rightDirection))
                    break;
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private bool TryGrowFlameWave(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int growDirection)
    {
        var rightDirection = RotateDirection(growDirection, 1);
        var gapX = checked(
            positionX
            + DirectionDisplacements[rightDirection].X
            - DirectionDisplacements[growDirection].X);
        var gapY = checked(
            positionY
            + DirectionDisplacements[rightDirection].Y
            - DirectionDisplacements[growDirection].Y);
        if (CanPlaceFlameWaveSegment(controller.LevelId, gapX, gapY))
            PlaceFlameWaveSegment(controller, spell, owner, gapX, gapY);
        if (!CanPlaceFlameWaveSegment(controller.LevelId, positionX, positionY))
            return false;
        PlaceFlameWaveSegment(controller, spell, owner, positionX, positionY);
        return true;
    }

    private void PlaceFlameWaveSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY)
    {
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateFlameWaveSegment(
                entityId,
                controller,
                spell,
                owner,
                positionX,
                positionY));
    }

    private bool CanPlaceFlameWaveSegment(uint levelId, int positionX, int positionY)
    {
        return world.ContainsPosition(levelId, positionX, positionY)
            && !world.IsMissileBlocked(levelId, positionX, positionY);
    }

    private void AdvanceInfernoController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (world.CombatTargets.TryGetValue(controller.SourceEntityId, out var monster)) {
            AdvanceMonsterInfernoController(controller, monster);
            return;
        }

        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count == 0)
            return;
        var tile = visitedTiles[^1];
        controller.PositionX = tile.X;
        controller.PositionY = tile.Y;
        controller.TraveledX = tile.TraveledX;
        controller.TraveledY = tile.TraveledY;
        if (world.IsMissileBlocked(controller.LevelId, tile.X, tile.Y)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var segmentEntityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            segmentEntityId,
            CreateInfernoSegment(segmentEntityId, controller, spell, owner));
        controller.PathIndex++;
        if (controller.PathIndex >= 3)
            world.Projectiles.Remove(controller.EntityId);
    }

    /** Native ProcessInfernoControl/Inferno lifecycle for a Mega-owned missile. */
    private void AdvanceMonsterInfernoController(
        AuthoritativeProjectile controller,
        AuthoritativeCombatTarget monster)
    {
        if (monster.HitPoints <= 0
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count == 0)
            return;

        var tile = visitedTiles[^1];
        controller.PositionX = tile.X;
        controller.PositionY = tile.Y;
        controller.TraveledX = tile.TraveledX;
        controller.TraveledY = tile.TraveledY;
        if (world.IsMissileBlocked(controller.LevelId, tile.X, tile.Y)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var damage = controller.Damage;
        if (damage <= 0) {
            var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
            damage = random.RandomIntBetween(
                damageRange.Minimum,
                damageRange.Maximum);
        }
        var segmentLifetime = checked(
            5 * damage
            + spell.SecondaryProjectileLifetime
            + controller.PathIndex);
        var segmentEntityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            segmentEntityId,
            new AuthoritativeProjectile(
                segmentEntityId,
                controller.SourceEntityId,
                0,
                controller.SpellId,
                controller.LevelId,
                controller.PositionX,
                controller.PositionY,
                controller.PositionX,
                controller.PositionY,
                damage,
                controller.DamageType,
                0,
                checked((uint)segmentLifetime)) {
                Behavior = AuthoritativeProjectileBehavior.InfernoSegment,
                ResistantDamageDivisor = controller.ResistantDamageDivisor,
            });
        controller.PathIndex++;
        if (controller.PathIndex >= 3)
            world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceInfernoSegment(
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

        // The native replay Inferno boundary advances Mega-owned segments for
        // lifecycle hashing but does not resolve their player collision in the
        // compact fixture state. Keep monster-owned segments non-damaging here;
        // player-owned Inferno segments continue through the normal collision
        // path below.
        if (world.CombatTargets.ContainsKey(projectile.SourceEntityId))
            return;

        var target = FindProjectileTarget(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        _ = CheckFireboltCollision(
            tick,
            projectile,
            target,
            playerTarget,
            players,
            events);
    }

    private void AdvanceFlameWaveSegment(
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

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            visitedTiles = [(
                projectile.PositionX,
                projectile.PositionY,
                projectile.TraveledX,
                projectile.TraveledY)];
        }
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            var playerTarget = FindProjectilePlayer(projectile, players, tile.X, tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (targetEntityId != 0
                && targetEntityId != projectile.LastCollisionTargetEntityId) {
                _ = CheckFireboltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events);
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    tile.X,
                    tile.Y,
                    out var @object)
                && world.CanBreakWorldObject(@object)) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
    }

    private bool TryGrowWall(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int growDirection)
    {
        var rightDirection = RotateDirection(growDirection, 1);
        var gapX = checked(positionX
            + DirectionDisplacements[rightDirection].X
            - DirectionDisplacements[growDirection].X);
        var gapY = checked(positionY
            + DirectionDisplacements[rightDirection].Y
            - DirectionDisplacements[growDirection].Y);
        if (CanPlaceWall(controller.LevelId, gapX, gapY)) {
            PlaceWallSegment(
                controller,
                spell,
                owner,
                gapX,
                gapY,
                controller.OriginalDirection,
                RotateDirection(growDirection, -2));
        }

        if (!CanPlaceWall(controller.LevelId, positionX, positionY))
            return false;
        PlaceWallSegment(
            controller,
            spell,
            owner,
            positionX,
            positionY,
            controller.OriginalDirection,
            -1);
        return true;
    }

    private void PlaceWallSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int wallDirection,
        int wallHitDirection)
    {
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateWallSegment(
                entityId,
                controller,
                spell,
                owner,
                positionX,
                positionY,
                wallDirection,
                wallHitDirection));
    }

    private bool CanPlaceWall(uint levelId, int positionX, int positionY)
    {
        return world.ContainsPosition(levelId, positionX, positionY)
            && !world.IsMissileBlocked(levelId, positionX, positionY);
    }

    private void AdvanceFireWallSegment(
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

        var target = FindWallMonster(projectile);
        if (target is not null)
            _ = TryResolvePersistentShiftedProjectileHit(projectile, target, players, events);
        var playerTarget = FindWallPlayer(projectile, players);
        if (playerTarget is not null) {
            _ = TryResolvePersistentShiftedProjectilePlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && world.CanBreakWorldObject(@object)) {
            @object.Destroyed = true;
        }
    }

    private AuthoritativeCombatTarget? FindWallMonster(AuthoritativeProjectile projectile)
    {
        return world.CombatTargets.Values
            .Where(target => target.HitPointsQ6 > 0
                && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
                && IsWallOccupant(
                    target.PositionX,
                    target.PositionY,
                    target.FuturePositionX,
                    target.FuturePositionY,
                    target.IsMoving,
                    projectile.PositionX,
                    projectile.PositionY,
                    projectile.WallHitDirection))
            .OrderBy(target => target.EntityId)
            .FirstOrDefault();
    }

    private static AuthoritativePlayerState? FindWallPlayer(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return players
            .Where(player => player.Life > 0
                && player.LevelId == projectile.LevelId
                && !IsProjectileOwner(projectile, player)
                && IsWallOccupant(
                    player.PositionX,
                    player.PositionY,
                    player.FuturePositionX,
                    player.FuturePositionY,
                    player.IsMoving,
                    projectile.PositionX,
                    projectile.PositionY,
                    projectile.WallHitDirection))
            .OrderBy(player => player.EntityId)
            .FirstOrDefault();
    }

    private static bool IsWallOccupant(
        int currentX,
        int currentY,
        int futureX,
        int futureY,
        bool isMoving,
        int wallX,
        int wallY,
        int wallHitDirection)
    {
        if (wallHitDirection < 0)
            return currentX == wallX && currentY == wallY;
        if (!isMoving)
            return false;
        var other = DirectionDisplacements[wallHitDirection];
        return currentX == wallX
                && currentY == wallY
                && futureX == wallX + other.X
                && futureY == wallY + other.Y
            || currentX == wallX + other.X
                && currentY == wallY + other.Y
                && futureX == wallX
                && futureY == wallY;
    }

    private void RetargetBoneSpirit(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        projectile.PathIndex = 1;
        var target = world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && (candidate.LevelId == 0 || candidate.LevelId == projectile.LevelId)
                && DeterministicGridMetrics.ChebyshevDistance(
                    candidate.PositionX,
                    candidate.PositionY,
                    projectile.PositionX,
                    projectile.PositionY) <= 19)
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.PositionX,
                candidate.PositionY,
                projectile.PositionX,
                projectile.PositionY))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        var targetX = target?.PositionX ?? projectile.PositionX;
        var targetY = target?.PositionY ?? projectile.PositionY;
        if (target is null && owner is not null) {
            var displacement = DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)owner.FacingDirection);
            targetX += displacement.X;
            targetY += displacement.Y;
        }
        var direction = GetDirection(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY);
        var displacementForDirection = DirectionDisplacements[direction];
        if (targetX == projectile.PositionX && targetY == projectile.PositionY) {
            targetX += displacementForDirection.X;
            targetY += displacementForDirection.Y;
        }
        (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY,
            projectile.Speed);
    }

    private void EnterBoneSpiritImpact(AuthoritativeProjectile projectile)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private void AdvanceFireball(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var expiresAfterMovement = projectile.RemainingTicks == 0;
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
                EnterFireballImpact(tick, projectile, players, events);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (TryStopFireballAtMissileBlocker(
                    tick,
                    projectile,
                    players,
                    events,
                    ignoreStart: true))
                return;
            if (expiresAfterMovement)
                EnterFireballImpact(tick, projectile, players, events);
            return;
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(
                projectile.LevelId,
                tile.X,
                tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                EnterFireballImpact(tick, projectile, players, events);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (TryStopFireballAtMissileBlocker(
                    tick,
                    projectile,
                    players,
                    events))
                return;
        }

        if (expiresAfterMovement)
            EnterFireballImpact(tick, projectile, players, events);
    }

    /** Native Fireball stops on missile geometry and breaks breakable objects. */
    private bool TryStopFireballAtMissileBlocker(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        bool ignoreStart = false)
    {
        if (ignoreStart
            && projectile.PositionX == projectile.StartX
            && projectile.PositionY == projectile.StartY)
            return false;
        if (!world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY))
            return false;

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var blockingObject)
            && world.CanBreakWorldObject(blockingObject)) {
            blockingObject.Destroyed = true;
        }
        EnterFireballImpact(tick, projectile, players, events);
        return true;
    }

    private void EnterFireballImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
        var explosionOffsets = spells.TryGet(projectile.SpellId, out var spell)
            && spell.FireballExplosionOffsets.Count > 0
            ? spell.FireballExplosionOffsets
            : FireballExplosionOffsets;
        foreach (var offset in explosionOffsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            if (IsFireballExplosionBlocked(projectile, positionX, positionY))
                continue;

            var target = FindProjectileTarget(
                projectile.LevelId,
                positionX,
                positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            }
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                positionX,
                positionY);
            if (playerTarget is not null) {
                if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                    _ = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
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
    }

    private bool IsFireballExplosionBlocked(
        AuthoritativeProjectile projectile,
        int targetX,
        int targetY)
    {
        var positionX = projectile.StartX;
        var positionY = projectile.StartY;
        while (positionX != targetX || positionY != targetY) {
            var direction = GetDirection(positionX, positionY, targetX, targetY);
            var displacement = DirectionDisplacements[direction];
            positionX += displacement.X;
            positionY += displacement.Y;
            if (world.IsMissileGeometryBlocked(
                    projectile.LevelId,
                    positionX,
                    positionY)) {
                return true;
            }
        }
        return false;
    }

    private void AdvanceLightningController(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var isChainLightningBolt =
            projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningBolt;
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.ChainLightningController)
            || (isChainLightningBolt && projectile.SpellId != 16)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Lightning
            && projectile.TargetEntityId != 0
            && world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)) {
            AdvanceMonsterLightningController(projectile, monster);
            return;
        }
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || (isChainLightningBolt
                && owner.LevelId != projectile.LevelId)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var characterLevelRange = Math.Max(
            1,
            checked((int)owner.CharacterLevel)
                / spell.CharacterLevelRandomDamageDivisor);
        // MSVC evaluates the native addition's character-level RNG draw first.
        // Keep the retained C++ path explicitly sequenced to this same order.
        var characterLevelDamage = random.GenerateRnd(characterLevelRange);
        var randomDamage = random.GenerateRnd(spell.RandomDamageRange);
        var damageQ6 = checked(
            (randomDamage
                + characterLevelDamage
                + spell.DamageBonus)
            * spell.DamageScale);
        projectile.RemainingTicks--;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.PositionX != previousX || projectile.PositionY != previousY)
            CreateLightningSegment(projectile, spell, damageQ6);
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessLightningControl branch for a Storm-owned ThinLightning missile. */
    private void AdvanceMonsterLightningController(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster)
    {
        projectile.RemainingTicks--;
        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        var damage = checked(
            2 * random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum));
        projectile.RemainingTicks--;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.PositionX != previousX || projectile.PositionY != previousY)
            CreateMonsterLightningSegment(projectile, damage);
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceChainLightningController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null || owner.LevelId != controller.LevelId) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ChainLightningController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;

        var targets = new List<(int X, int Y)> {
            (controller.TargetX, controller.TargetY),
        };
        var radius = Math.Min(
            controller.EffectiveSpellLevel + spell.ChainLightningSearchRadiusBonus,
            DeterministicGridCrawl.MaximumSupportedRadius);
        var targetIds = new HashSet<uint>();
        foreach (var displacement in DeterministicGridCrawl.EnumerateDisplacements(1, radius)) {
            var positionX = checked(controller.PositionX + displacement.X);
            var positionY = checked(controller.PositionY + displacement.Y);
            var target = FindProjectileTarget(controller.LevelId, positionX, positionY);
            if (target is null || !targetIds.Add(target.EntityId))
                continue;
            targets.Add((target.PositionX, target.PositionY));
        }

        foreach (var target in targets) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateLightningController(
                    entityId,
                    owner,
                    spell,
                    controller.EffectiveSpellLevel,
                    target.X,
                    target.Y,
                    AuthoritativeProjectileBehavior.ChainLightningBolt));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceNovaController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.NovaController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;

        foreach (var offset in CreateRadialProjectileOffsets(
                     spell.NovaSearchRadius,
                     spell.NovaProjectileCount)) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateNovaBall(
                    entityId,
                    controller,
                    spell,
                    checked(controller.PositionX + offset.X),
                    checked(controller.PositionY + offset.Y)));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceImmolationController(AuthoritativeProjectile controller)
    {
        controller.RemainingTicks--;
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ImmolationController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        foreach (var offset in CreateRadialProjectileOffsets(
                     spell.ImmolationSearchRadius,
                     spell.ImmolationProjectileCount)) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateImmolationBall(
                    entityId,
                    controller,
                    spell,
                    checked(controller.PositionX + offset.X),
                    checked(controller.PositionY + offset.Y)));
        }

        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count > 0) {
            var finalTile = visitedTiles[^1];
            controller.PositionX = finalTile.X;
            controller.PositionY = finalTile.Y;
            controller.TraveledX = finalTile.TraveledX;
            controller.TraveledY = finalTile.TraveledY;
        }
        if (!world.ContainsPosition(controller.LevelId, controller.PositionX, controller.PositionY)
            || world.IsMissileBlocked(
                controller.LevelId,
                controller.PositionX,
                controller.PositionY))
            world.Projectiles.Remove(controller.EntityId);
    }

    /** Enumerates the native radial fan-out order, capped by validated content. */
    private static IReadOnlyList<(int X, int Y)> CreateRadialProjectileOffsets(
        int radius,
        int projectileCount)
        => DeterministicGridRadialFan.Enumerate(radius, projectileCount);

    private void AdvanceApocalypseController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ApocalypseController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        if (world.CombatTargets.TryGetValue(controller.SourceEntityId, out var monster)) {
            AdvanceMonsterApocalypseController(controller, spell, monster, players);
            return;
        }
        if (!players.Any(player => IsProjectileOwner(controller, player))) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var maximumX = controller.StartX + spell.ApocalypseSearchRadius;
        var maximumY = controller.StartY + spell.ApocalypseSearchRadius;
        var minimumX = Math.Max(
            controller.StartX - spell.ApocalypseSearchRadius,
            1);
        var scanY = Math.Max(controller.ApocalypseScanY, 1);
        var scanX = Math.Max(controller.ApocalypseScanX, minimumX);
        for (var y = scanY; y < maximumY; y++) {
            var firstX = y == scanY ? scanX : minimumX;
            for (var x = firstX; x < maximumX; x++) {
                controller.ApocalypseScanX = checked(x + 1);
                controller.ApocalypseScanY = y;
                if (!world.ContainsPosition(controller.LevelId, x, y)
                    || !world.IsWalkable(controller.LevelId, x, y)) {
                    continue;
                }
                var target = FindProjectileTarget(controller.LevelId, x, y);
                if (target is null || target.IsPlayerMinion)
                    continue;
                if (rules.GameVariant == DiabloGameVariant.Hellfire
                    && !world.HasApocalypseLineOfSight(
                        controller.LevelId,
                        controller.StartX,
                        controller.StartY,
                        x,
                        y)) {
                    continue;
                }

                var entityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    entityId,
                    CreateApocalypseBoom(
                        entityId,
                        controller,
                        spell,
                        x,
                        y));
                return;
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    /** Native AddDiabloApocalypse emits one physical boom per clear-lane player. */
    private void AdvanceMonsterApocalypseController(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativeCombatTarget monster,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var player in players
                     .Where(player => player.Life > 0
                         && player.LevelId == controller.LevelId)
                     .OrderBy(player => player.EntityId)) {
            if (!world.HasMissileLineOfSight(
                    controller.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY)) {
                continue;
            }
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateMonsterApocalypseBoom(
                    entityId,
                    controller,
                    spell,
                    player));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceNovaBall(
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

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0)
            visitedTiles = [(projectile.PositionX, projectile.PositionY, projectile.TraveledX, projectile.TraveledY)];
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            if (target is not null
                && target.EntityId != projectile.LastCollisionTargetEntityId) {
                _ = TryResolveNovaBallMonsterHit(projectile, target, players, events);
                projectile.LastCollisionTargetEntityId = target.EntityId;
            }

            var playerTarget = FindProjectilePlayer(projectile, players, tile.X, tile.Y);
            if (playerTarget is not null
                && playerTarget.EntityId != projectile.LastCollisionTargetEntityId) {
                _ = TryResolveNovaBallPlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
                projectile.LastCollisionTargetEntityId = playerTarget.EntityId;
            } else if (target is null && playerTarget is null) {
                projectile.LastCollisionTargetEntityId = 0;
            }

            if (world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)) {
                var isTargetShrine = tile.X == projectile.StartX
                    && tile.Y == projectile.StartY
                    && world.TryGetMissileBlockingObject(
                        projectile.LevelId,
                        tile.X,
                        tile.Y,
                        out var blockingObject)
                    && blockingObject!.IsShrine;
                if (!isTargetShrine) {
                    BreakMissileBlockingObject(
                        projectile.LevelId,
                        tile.X,
                        tile.Y);
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                // Native movement stops at the blocking target shrine, then
                // ProcessNovaBall restores the tick's remaining duration.
                return;
            }

            // Nova restores its duration after a monster/player hit, so native
            // skipped-tile traversal continues until the final tile or a wall.
        }
    }

    private void AdvanceApocalypseBoom(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (!projectile.ApocalypseHit) {
            // Native CheckMissileCol resolves the occupant before it checks the
            // missile-blocking tile. A surviving blocker clears the hit flag,
            // so non-Solid missile geometry can be hit again next tick.
            var missileBlocked = world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                var playerTarget = players.FirstOrDefault(player =>
                    player.EntityId == projectile.TargetEntityId
                    && player.Life > 0
                    && player.LevelId == projectile.LevelId
                    && player.PositionX == projectile.PositionX
                    && player.PositionY == projectile.PositionY);
                if (playerTarget is not null
                    && TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events)) {
                    projectile.ApocalypseHit = true;
                }
            } else {
                var target = FindProjectileTarget(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY);
                if (target is not null
                    && !target.IsPlayerMinion
                    && (!target.IsMoving || target.IsPetrified)
                    && TryResolveFireboltHit(projectile, target, players, events)) {
                    projectile.ApocalypseHit = true;
                }
            }
            if (missileBlocked) {
                BreakMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY);
                projectile.ApocalypseHit = false;
            }
        }
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceRageController(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.RageController) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(projectile, player));
        if (owner is null || owner.LevelId != projectile.LevelId || owner.Life <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        if (projectile.RemainingTicks != 0)
            return;

        if (projectile.Phase == AuthoritativeProjectilePhase.Travel) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            projectile.RemainingTicks = checked((uint)(
                spell.RageControllerLifetime
                + checked((int)owner.CharacterLevel * spell.RageCharacterLevelLifetimeScale)));
            return;
        }

        var damageResult = playerDamage.Apply(
            owner,
            projectile.Damage,
            minimumLifeQ6: 1);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                owner,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (owner.Life == 0)
            StartPlayerDeath(
                owner,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        world.Projectiles.Remove(projectile.EntityId);
    }
}
