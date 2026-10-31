using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Projectile advancement, arrows, acid and charges (native missiles.cpp ProcessMissiles and Mi_* functions). */
public sealed partial class DiabloCombatCommandProcessor
{
    private void AdvanceProjectiles(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var projectileEntityIds = world.Projectiles.Keys.Order().ToList();
        var knownProjectileEntityIds = projectileEntityIds.ToHashSet();
        for (var projectileIndex = 0;
             projectileIndex < projectileEntityIds.Count;
             projectileIndex++) {
            try {
                if (!world.Projectiles.TryGetValue(
                        projectileEntityIds[projectileIndex],
                        out var projectile)) {
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ChargedBolt) {
                    AdvanceChargedBolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterChargedBolt) {
                    AdvanceChargedBolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow) {
                    AdvanceWeaponArrow(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.HorkSpawn) {
                    AdvanceHorkSpawn(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.HolyBolt) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Acid) {
                    AdvanceAcid(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat) {
                    AdvanceAcidSplat(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle) {
                    AdvanceAcidPuddle(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.SnakeCharge) {
                    AdvanceSnakeCharge(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BatCharge) {
                    AdvanceBatCharge(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BoneSpirit) {
                    AdvanceBoneSpirit(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.LightningWallController) {
                    AdvanceFireWallController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.RingOfFireController) {
                    AdvanceRingOfFireController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ImmolationController) {
                    AdvanceImmolationController(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment) {
                    AdvanceFireWallSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveController) {
                    AdvanceFlameWaveController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveSegment) {
                    AdvanceFlameWaveSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController) {
                    AdvanceInfernoController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment) {
                    AdvanceInfernoSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningController) {
                    AdvanceChainLightningController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.NovaController) {
                    AdvanceNovaController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseController) {
                    AdvanceApocalypseController(projectile, players);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.ChainLightningBolt) {
                    AdvanceLightningController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.NovaBall) {
                    AdvanceNovaBall(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom) {
                    AdvanceApocalypseBoom(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.RageController) {
                    AdvanceRageController(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Elemental) {
                    AdvanceElemental(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.GuardianController) {
                    AdvanceGuardian(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Fireball) {
                    AdvanceFireball(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.LightningSegment) {
                    AdvanceLightningSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.FlashBottom
                    or AuthoritativeProjectileBehavior.FlashTop) {
                    AdvanceFlashLayer(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.Teleport
                    or AuthoritativeProjectileBehavior.Phasing) {
                    AdvanceMovementController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ResurrectionBeam) {
                    AdvanceResurrectionBeam(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.StoneCurse) {
                    AdvanceStoneCurse(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow) {
                    if (world.HasNativeCatacombsArrow(projectile.EntityId)) {
                        AdvanceNativeCatacombsArrow(tick, projectile, players, events);
                        continue;
                    }
                    if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                        AdvanceMonsterArrow(tick, projectile, players, events);
                        continue;
                    }
                }
                if (projectile.RemainingTicks > 1) {
                    var nextX = projectile.PositionX
                        + Math.Sign((long)projectile.TargetX - projectile.PositionX);
                    var nextY = projectile.PositionY
                        + Math.Sign((long)projectile.TargetY - projectile.PositionY);
                    if (world.IsMissileBlocked(projectile.LevelId, nextX, nextY)
                        || !world.HasMissileLineOfSight(
                            projectile.LevelId,
                            projectile.PositionX,
                            projectile.PositionY,
                            nextX,
                            nextY)) {
                        BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                        world.Projectiles.Remove(projectile.EntityId);
                        continue;
                    }
                    projectile.PositionX = nextX;
                    projectile.PositionY = nextY;
                    projectile.RemainingTicks--;
                    continue;
                }

                if (projectile.TargetEntityId != 0) {
                    if (world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
                        && target.HitPoints > 0
                        && (target.LevelId == 0 || target.LevelId == projectile.LevelId)) {
                        ResolveProjectileDamage(projectile, target, players, events);
                    }
                } else {
                    foreach (var target in world.CombatTargets.Values
                                 .Where(target => target.HitPoints > 0
                                     && (target.LevelId == 0
                                         || target.LevelId == projectile.LevelId)
                                      && DeterministicGridMetrics.ChebyshevDistance(
                                          target.PositionX,
                                          target.PositionY,
                                          projectile.TargetX,
                                          projectile.TargetY)
                                          <= projectile.AreaRadius)
                                 .OrderBy(target => target.EntityId)) {
                        ResolveProjectileDamage(projectile, target, players, events);
                    }
                }
                world.Projectiles.Remove(projectile.EntityId);
            } finally {
                foreach (var newEntityId in world.Projectiles.Keys
                             .Where(entityId => knownProjectileEntityIds.Add(entityId))
                             .Order()) {
                    projectileEntityIds.Add(newEntityId);
                }
            }
        }
    }

    /** Native CheckMissileCol transition for a breakable arrow blocker. */
    private void BreakMissileBlockingObject(
        uint levelId,
        int positionX,
        int positionY)
    {
        if (world.TryGetMissileBlockingObject(
                levelId,
                positionX,
                positionY,
                out var blockingObject)
            && world.CanBreakWorldObject(blockingObject)) {
            blockingObject.Destroyed = true;
        }
    }

    private void AdvanceWeaponArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.NativeTrapArrow is not null) {
            AdvanceNativeTrapArrow(tick, projectile, players, events);
            return;
        }
        if (projectile.NativeWeaponArrow is not null) {
            AdvanceNativeWeaponArrow(tick, projectile, players, events);
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.ActivationDelayTicks > 0) {
            projectile.ActivationDelayTicks--;
            return;
        }
        if (projectile.RemainingTicks > 1) {
            // Legacy hand-built fixtures used Speed=1 as a one-step marker;
            // native AddArrow speeds occupy the 16..74 range after class and
            // Hellfire attack-speed modifiers are applied.
            var velocity = projectile.Speed is >= 16 and <= 74
                ? projectile.Speed
                : 32;
            var travelBudget = checked(velocity + projectile.PathTicks);
            var travelSteps = travelBudget / 32;
            projectile.PathTicks = travelBudget % 32;
            if (travelSteps == 0
                && TryResolveWeaponArrowTileCollision(
                    tick,
                    projectile,
                    players,
                    events)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
                return;
            for (var step = 0; step < travelSteps; step++) {
                var nextX = projectile.PositionX
                    + Math.Sign((long)projectile.TargetX - projectile.PositionX);
                var nextY = projectile.PositionY
                    + Math.Sign((long)projectile.TargetY - projectile.PositionY);
                if (world.IsMissileBlocked(projectile.LevelId, nextX, nextY)
                    || !world.HasMissileLineOfSight(
                        projectile.LevelId,
                        projectile.PositionX,
                        projectile.PositionY,
                        nextX,
                        nextY)) {
                    BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                projectile.PositionX = nextX;
                projectile.PositionY = nextY;
                if (TryResolveWeaponArrowTileCollision(
                        tick,
                        projectile,
                        players,
                        events)) {
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
                    return;
            }
            projectile.RemainingTicks--;
            return;
        }

        if (TryResolveWeaponArrowTileCollision(
                tick,
                projectile,
                players,
                events)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        if (projectile.TargetEntityId != 0
            && world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
            && target.HitPoints > 0
            && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
            && IsWeaponArrowEndpointOccupied(projectile, target)) {
            ResolveWeaponArrowImpact(tick, projectile, target, players, events);
        } else if (projectile.TargetEntityId != 0
            && players.FirstOrDefault(candidate =>
                candidate.EntityId == projectile.TargetEntityId
                && candidate.Life > 0
                && (candidate.LevelId == 0 || candidate.LevelId == projectile.LevelId)
                && candidate.PositionX == projectile.TargetX
                && candidate.PositionY == projectile.TargetY)
                is { } playerTarget) {
            _ = ResolvePlayerWeaponArrowImpact(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;
        world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessArrow only resolves the declared endpoint while it occupies that tile. */
    private static bool IsWeaponArrowEndpointOccupied(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target)
    {
        return target.PositionX == projectile.TargetX
            && target.PositionY == projectile.TargetY
            || target.IsMoving
            && target.IsPetrified
            && target.FuturePositionX == projectile.TargetX
            && target.FuturePositionY == projectile.TargetY;
    }

    private void AdvanceNativeWeaponArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.OwnsNativePopulationLevel(projectile.LevelId))
            throw new InvalidDataException(
                "A pending native arrow is outside its owned population level.");
        if (!players.Any(player => player.LevelId == projectile.LevelId))
            return;
        if (projectile.Phase != AuthoritativeProjectilePhase.Travel)
            throw new InvalidDataException(
                "A pending native arrow cannot already be in its impact phase.");
        if (projectile.NativeWeaponArrow is not { } nativeState
            || nativeState.MissileDistance > int.MaxValue)
            throw new InvalidDataException(
                "A pending native arrow has an invalid missile distance.");
        if (projectile.ActivationDelayTicks > 0) {
            projectile.ActivationDelayTicks--;
            return;
        }
        if (projectile.RemainingTicks == 0)
            throw new InvalidDataException(
                "A pending native arrow has no remaining native lifetime.");
        if (nativeState.MissileDistance == int.MaxValue)
            throw new InvalidDataException(
                "A pending native arrow exceeded the native missile-distance range.");
        projectile.RemainingTicks--;
        projectile.NativeWeaponArrow = nativeState with {
            MissileDistance = nativeState.MissileDistance + 1,
        };

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            // Native MoveMissile still checks the current tile when its pixel
            // movement does not cross a tile boundary, but suppresses repeats
            // against the same occupant until the tile contents change.
            if (TryResolveWeaponArrowTileCollision(tick, projectile, players, events)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            if (world.IsMissileBlocked(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY)) {
                BreakMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            // Changed cells always collide; only stationary cells suppress repeats.
            projectile.LastCollisionTargetEntityId = 0;
            // Native collision runs on every traversed tile before missile
            // blocking. A missed target keeps moving along the fixed velocity.
            if (TryResolveWeaponArrowTileCollision(tick, projectile, players, events)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            BreakMissileBlockingObject(projectile.LevelId, tile.X, tile.Y);
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessArrow collision check for each traversed tile. */
    private bool TryResolveWeaponArrowTileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var target = FindProjectileTarget(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (target is not null) {
            if (projectile.NativeWeaponArrow is null) {
                _ = ResolveWeaponArrowImpact(tick, projectile, target, players, events);
                return true;
            }
            if (projectile.LastCollisionTargetEntityId == target.EntityId)
                return false;
            var stops = ResolveWeaponArrowImpact(tick, projectile, target, players, events);
            projectile.LastCollisionTargetEntityId = target.EntityId;
            return stops;
        }

        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is null) {
            if (projectile.NativeWeaponArrow is not null)
                projectile.LastCollisionTargetEntityId = 0;
            return false;
        }

        if (projectile.NativeWeaponArrow is not null) {
            if (projectile.LastCollisionTargetEntityId == playerTarget.EntityId)
                return false;
            var stops = ResolveNativePlayerWeaponArrowImpact(
                tick,
                projectile,
                playerTarget,
                players,
                events);
            projectile.LastCollisionTargetEntityId = playerTarget.EntityId;
            return stops;
        }

        return ResolvePlayerWeaponArrowImpact(
            tick,
            projectile,
            playerTarget,
            players,
            events);
    }

    /** Advances a content-declared native Arrow from a monster to its player target. */
    private void AdvanceMonsterArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (!world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var travelSteps = Math.Max(1, projectile.Speed);
        for (var step = 0; step < travelSteps; step++) {
            var nextX = projectile.PositionX
                + Math.Sign((long)projectile.TargetX - projectile.PositionX);
            var nextY = projectile.PositionY
                + Math.Sign((long)projectile.TargetY - projectile.PositionY);
            if (!world.ContainsPosition(projectile.LevelId, nextX, nextY)
                || world.IsMissileBlocked(projectile.LevelId, nextX, nextY)) {
                BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            projectile.PositionX = nextX;
            projectile.PositionY = nextY;
            var target = FindProjectilePlayer(
                projectile,
                players,
                nextX,
                nextY);
            if (target is not null) {
                // Native PlayerMHit returns false for temporary player
                // protection (for example Etherealize). In that case the
                // arrow continues through the occupied tile instead of being
                // consumed as a successful collision.
                if (ResolveMonsterArrowImpact(tick, projectile, target, players, events)) {
                    if (projectile.ImpactLifetime > 0)
                        EnterProjectileImpact(projectile);
                    else
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
            }

            if (nextX == projectile.TargetX && nextY == projectile.TargetY)
                break;
        }

        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private bool ResolveMonsterArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        // DevilutionX's PlayerMHit rejects an arrow against an invincible or
        // Etherealized player without marking the missile as hit. Preserve
        // that distinction so the projectile keeps travelling.
        if (IsPlayerProtectedByFlash(target) || IsPlayerEtherealized(target))
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        var damage = projectile.Damage;
        if (damage > 0 && projectile.DamageMinimum > 0) {
            // Native ProcessArrow supplies the monster range to PlayerMHit,
            // which rolls physical damage when the arrow collides rather than
            // when the AI admits the shot.
            damage = random.RandomIntBetween(
                projectile.DamageMinimum,
                projectile.DamageMaximum);
        }
        var sourceMonster = world.CombatTargets.TryGetValue(
            projectile.SourceEntityId,
            out var source)
            ? source
            : null;
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - checked((sourceMonster is null
                    ? 0
                    : GetEffectiveMonsterLevel(sourceMonster))
                    * rules.MonsterPlayerBlockLevelScale),
            0,
            100);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        if (canBlock && blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }
        // Native PlayerMHit applies the target's `_pIGetHit` bonus after the
        // impact-time arrow roll, before resistance reduces the incoming hit.
        damage = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
            damage,
            rules.MinimumDamage,
            target.Equipment);
        if (resistance > 0)
            damage -= damage * resistance / 100;
        var incomingDamageQ6 = DeterministicFixedPointQ6.FromWhole(Math.Max(0, damage));
        var usesLegacyWholeLife = target.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
        var appliedDamageQ6 = usesLegacyWholeLife
            ? DeterministicFixedPointQ6.ToWholeTruncated(incomingDamageQ6)
            : incomingDamageQ6;
        var damageResult = playerDamage.Apply(target, appliedDamageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            var eventDamageQ6 = usesLegacyWholeLife
                ? incomingDamageQ6
                : damageResult.IncomingDamageQ6;
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    projectile.SourceEntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(eventDamageQ6)) {
                    AmountQ6 = eventDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                projectile.SourceEntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        return true;
    }

    private bool ResolvePlayerWeaponArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player =>
            player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return true;

        var gameplayEvents = new List<DiabloGameplayEvent>();
        if (projectile.PerilSelfDamageQ6 > 0) {
            var selfDamageResult = playerDamage.Apply(owner, projectile.PerilSelfDamageQ6);
            if (selfDamageResult.IncomingDamageQ6 > 0) {
                gameplayEvents.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        selfDamageResult.IncomingDamageQ6)) {
                    AmountQ6 = selfDamageResult.IncomingDamageQ6,
                });
            }
            if (owner.Life == 0) {
                StartPlayerDeath(
                    owner,
                    owner.EntityId,
                    tick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }

        // Native Plr2PlrMHit repeats the friendly-fire and Etherealize
        // boundaries when the delayed arrow reaches its target. The target
        // may change state after launch, so launch-time admission is not
        // sufficient.
        if ((!rules.FriendlyFire && owner.FriendlyMode)
            || IsPlayerEtherealized(target))
            return false;

        if (projectile.AttackHit
            && TryResolvePlayerBlock(owner, target, tick)) {
            if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                // Native RotateBlockedMissile consumes GenerateRnd(2) and
                // keeps a blocked missile alive through its impact animation.
                random.RandomIntLessThan(2);
                EnterProjectileImpact(projectile);
                foreach (var gameplayEvent in gameplayEvents)
                    events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
                return false;
            }
            foreach (var gameplayEvent in gameplayEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, projectile.DamageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            gameplayEvents.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                owner.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(
                    damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (projectile.AttackHit
            && (projectile.ElementalFireDamage > 0
                || projectile.ElementalLightningDamage > 0)) {
            var targetProxy = CreatePlayerCombatTargetProxy(target);
            ApplyPlayerElementalChannels(
                owner,
                target,
                targetProxy,
                new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(
                    projectile.ElementalFireDamage,
                    projectile.ElementalLightningDamage),
                tick,
                players,
                gameplayEvents);
        }
        ApplyPlayerKnockback(owner, target, projectile.DamageQ6, players);
        if (projectile.DamageQ6 > 0)
            ApplyPlayerAttackEffects(owner, projectile.DamageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool ResolveNativePlayerWeaponArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player =>
            player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return true;
        if (!world.OwnsNativePopulationLevel(projectile.LevelId)
            || owner.LevelId != projectile.LevelId
            || projectile.NativeWeaponArrow is not { } nativeState
            || nativeState.MissileDistance > int.MaxValue)
            throw new InvalidDataException(
                "A pending native arrow has an invalid player collision owner or distance.");

        // Native Plr2PlrMHit rechecks friendly-fire and temporary protection at
        // collision; a rejected player collision leaves the arrow in flight.
        if ((!rules.FriendlyFire && owner.FriendlyMode)
            || IsPlayerProtectedByFlash(target)
            || IsPlayerEtherealized(target))
            return false;

        var targetProxy = CreatePlayerCombatTargetProxy(target);
        var nativeDamageQ6 = ResolvePlayerAttackDamageQ6(
            owner,
            targetProxy,
            monsterTarget: false,
            ranged: true,
            deferGargoyleWake: true,
            targetDistance: checked((int)nativeState.MissileDistance),
            adjacentDamage: false,
            seed: 0,
            out _,
            out _,
            out _,
            out var attackHit,
            nativeImpactRoll: true);
        if (!attackHit)
            return false;
        if (TryResolvePlayerBlock(owner, target, tick))
            return true;

        var nativeEvents = new List<DiabloGameplayEvent>();
        var damageResult = playerDamage.Apply(target, Math.Max(0, nativeDamageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            nativeEvents.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                owner.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(
                    damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else {
            var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
                owner.Equipment,
                random.GenerateRnd,
                ranged: true);
            ApplyPlayerElementalChannels(
                owner,
                target,
                targetProxy,
                elemental,
                tick,
                players,
                nativeEvents);
        }
        ApplyPlayerKnockback(owner, target, nativeDamageQ6, players);
        if (nativeDamageQ6 > 0)
            ApplyPlayerAttackEffects(owner, nativeDamageQ6, nativeEvents);
        foreach (var gameplayEvent in nativeEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool ResolveWeaponArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player =>
            player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return true;

        if (projectile.NativeWeaponArrow is { } nativeState) {
            if (!world.OwnsNativePopulationLevel(projectile.LevelId)
                || owner.LevelId != projectile.LevelId)
                throw new InvalidDataException(
                    "A pending native arrow has no current owner on its population level.");
            if (nativeState.MissileDistance > int.MaxValue)
                throw new InvalidDataException(
                    "A pending native arrow has an invalid missile distance.");
            // MonsterMHit's isPossibleToHit guards precede its first RNG draw.
            // Physical arrows have no elemental-immunity channel in native isImmune.
            if (target.HitPointsQ6 <= 0 || target.LevelId != projectile.LevelId
                || target.DialogueMessageId != 0
                || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
                || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target))
                return false;

            var gargoyleWasSpecial = string.Equals(
                    target.NativeAi,
                    "Gargoyle",
                    StringComparison.Ordinal)
                && IsNativeGargoyleWakePending(target);
            var nativeDamageQ6 = ResolvePlayerAttackDamageQ6(
                owner,
                target,
                monsterTarget: true,
                ranged: true,
                deferGargoyleWake: true,
                targetDistance: checked((int)nativeState.MissileDistance),
                adjacentDamage: false,
                seed: 0,
                out var perilSelfDamageQ6,
                out var perilActive,
                out var doppelgangerProc,
                out var attackHit,
                nativeImpactRoll: true);
            if (gargoyleWasSpecial && !target.GargoyleSpecialActive)
                return true;
            if (!attackHit)
                return false;

            var nativeEvents = new List<DiabloGameplayEvent>();
            if (doppelgangerProc)
                SpawnDoppelganger(target, players);
            if (perilActive && perilSelfDamageQ6 > 0) {
                var selfDamage = playerDamage.Apply(owner, perilSelfDamageQ6);
                if (selfDamage.IncomingDamageQ6 > 0) {
                    nativeEvents.Add(new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Damage,
                        owner.EntityId,
                        owner.EntityId,
                        DeterministicFixedPointQ6.ToWholeTruncated(
                            selfDamage.IncomingDamageQ6)) {
                        AmountQ6 = selfDamage.IncomingDamageQ6,
                    });
                }
                if (owner.Life == 0) {
                    StartPlayerDeath(
                        owner,
                        owner.EntityId,
                        tick,
                        players,
                        PlayerDeathReason.MonsterOrTrap);
                }
            }
            ApplyResolvedDamageQ6(owner, target, nativeDamageQ6, nativeEvents);
            var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
                owner.Equipment,
                random.GenerateRnd,
                ranged: true);
            if (target.HitPointsQ6 > 0) {
                ApplyWeaponArrowElementalDamage(
                    owner,
                    target,
                    elemental.Fire,
                    elemental.Lightning,
                    nativeEvents);
            }
            ApplyEquipmentKnockback(owner, target, nativeDamageQ6, players);
            if (nativeDamageQ6 > 0)
                ApplyEquipmentAttackEffects(owner, nativeDamageQ6, nativeEvents);
            foreach (var gameplayEvent in nativeEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
            return true;
        }

        var gameplayEvents = new List<DiabloGameplayEvent>();
        if (string.Equals(target.NativeAi, "Gargoyle", StringComparison.Ordinal)
            && IsNativeGargoyleWakePending(target)) {
            // Native MonsterMHit consumes the Gargoyle wake-up flag at arrow
            // impact, before damage or any Hellfire equipment effect.
            _ = ConsumeNativeGargoyleWake(target);
            return true;
        }
        if (projectile.DoppelgangerProc)
            SpawnDoppelganger(target, players);
        if (projectile.PerilSelfDamageQ6 > 0) {
            var damageResult = playerDamage.Apply(owner, projectile.PerilSelfDamageQ6);
            if (damageResult.IncomingDamageQ6 > 0) {
                gameplayEvents.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (owner.Life == 0) {
                StartPlayerDeath(
                    owner,
                    owner.EntityId,
                    tick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }
        ApplyResolvedDamageQ6(
            owner,
            target,
            projectile.DamageQ6,
            gameplayEvents);
        if (projectile.AttackHit && target.HitPointsQ6 > 0) {
            ApplyWeaponArrowElementalDamage(
                owner,
                target,
                projectile,
                gameplayEvents);
        }
        ApplyEquipmentKnockback(owner, target, projectile.DamageQ6, players);
        if (projectile.DamageQ6 > 0)
            ApplyEquipmentAttackEffects(owner, projectile.DamageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private void ApplyWeaponArrowElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        AuthoritativeProjectile projectile,
        ICollection<DiabloGameplayEvent> events)
        => ApplyWeaponArrowElementalDamage(
            player,
            target,
            projectile.ElementalFireDamage,
            projectile.ElementalLightningDamage,
            events);

    private void ApplyWeaponArrowElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int fireChannelDamage,
        int lightningChannelDamage,
        ICollection<DiabloGameplayEvent> events)
    {
        if (fireChannelDamage > 0) {
            var fireDamage = rules.ResolveDamage(
                fireChannelDamage,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Fire,
                target);
            ApplyResolvedDamage(player, target, fireDamage, events);
        }
        if (lightningChannelDamage > 0 && target.HitPointsQ6 > 0) {
            var lightningDamage = rules.ResolveDamage(
                lightningChannelDamage,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Lightning,
                target);
            ApplyResolvedDamage(player, target, lightningDamage, events);
        }
    }

    private void AdvanceMovementController(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var player = players.FirstOrDefault(candidate =>
            IsProjectileOwner(projectile, candidate));
        if (player is null) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var destination = FindClosestTeleportDestination(
            player,
            projectile.PositionX,
            projectile.PositionY,
            players,
            projectile.Behavior == AuthoritativeProjectileBehavior.Teleport
                && spells.TryGet(projectile.SpellId, out var teleportSpell)
                ? teleportSpell.TeleportSearchRadius
                : 5);
        if (!destination.HasValue)
            return;
        player.PositionX = destination.Value.X;
        player.PositionY = destination.Value.Y;
    }

    private void AdvanceResurrectionBeam(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceStoneCurse(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (!world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
            || !target.IsPetrified) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (target.HitPointsQ6 == 0
            && projectile.Phase != AuthoritativeProjectilePhase.Impact) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        }
        if (projectile.RemainingTicks != 0)
            return;

        world.Projectiles.Remove(projectile.EntityId);
        if (target.HitPointsQ6 > 0)
            target.IsPetrified = false;
    }

    private void AdvanceFirebolt(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            if ((projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic)
                || projectile.Phase == AuthoritativeProjectilePhase.Impact) {
                world.Projectiles.Remove(projectile.EntityId);
            } else {
                EnterProjectileImpact(projectile);
            }
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var previousTraveledX = projectile.TraveledX;
        var previousTraveledY = projectile.TraveledY;
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
                if (projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic) {
                    if (!TryEnterMonsterProjectileImpact(projectile))
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                EnterProjectileImpact(projectile);
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
                if (projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic) {
                    if (!TryEnterMonsterProjectileImpact(projectile))
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                if (!(projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt
                        && world.CombatTargets.ContainsKey(projectile.SourceEntityId))) {
                    projectile.PositionX = previousX;
                    projectile.PositionY = previousY;
                    projectile.TraveledX = previousTraveledX;
                    projectile.TraveledY = previousTraveledY;
                }
                EnterProjectileImpact(projectile);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            previousX = tile.X;
            previousY = tile.Y;
            previousTraveledX = tile.TraveledX;
            previousTraveledY = tile.TraveledY;
        }
    }

    /** Native ProcessHorkSpawn travel and expiry summon admission. */
    private void AdvanceHorkSpawn(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null
            && TryResolveHorkSpawnPlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events)) {
            world.Projectiles.Remove(projectile.EntityId);
            CompleteHorkSpawn(projectile, players);
            return;
        }

        // Native CheckMissileCol checks the current tile before the summon
        // expiry branch, including breakable missile-blocking objects.
        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            BreakMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            world.Projectiles.Remove(projectile.EntityId);
            CompleteHorkSpawn(projectile, players);
            return;
        }

        if (projectile.RemainingTicks != 0) {
            foreach (var tile in MoveNativeProjectile(projectile)) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                if (world.IsMissileBlocked(
                        projectile.LevelId,
                        tile.X,
                        tile.Y)) {
                    // Native ProcessHorkSpawn moves into the blocked tile and
                    // checks it on the following tick before expiry/summoning.
                    return;
                }
            }
            return;
        }

        world.Projectiles.Remove(projectile.EntityId);
        CompleteHorkSpawn(projectile, players);
    }

    /** Native Hork Spawn expiry admission and summon creation. */
    private void CompleteHorkSpawn(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (monsterSpawnPlanner is null
            || world.CombatTargets.Count >= 256)
            return;

        var spawnPosition = DeterministicGridCrawl.FindClosestValidPosition(
            projectile.PositionX,
            projectile.PositionY,
            0,
            1,
            (candidateX, candidateY) =>
                world.IsWalkable(projectile.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    projectile.LevelId,
                    candidateX,
                    candidateY,
                    players));
        if (!spawnPosition.HasValue)
            return;

        if (projectile.SpellId == 0)
            return;

        var summon = monsterSpawnPlanner.CreateStableMonsterSummon(
            projectile.LevelId,
            projectile.SpellId,
            unchecked(
                projectile.EntityId
                ^ projectile.SourceEntityId
                ^ projectile.TargetEntityId),
            spawnPosition.Value.X,
            spawnPosition.Value.Y,
            AllocateMonsterEntityId(players),
            gameMode == DiabloGameMode.Multiplayer,
            projectile.OriginalDirection);
        if (summon is not null) {
            DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                summon,
                difficulty,
                rules.GameVariant,
                gameMode,
                rules.DifficultyRules);
            DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                summon,
                difficulty,
                monsterTypes);
            world.CombatTargets.Add(summon.EntityId, summon);
        }
    }

    /** Native AddAcid/ProcessGenericProjectile travel and expiry transition. */
    private void AdvanceAcid(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (TryResolveAcidProjectileCollision(
                    tick,
                    projectile,
                    players,
                    events)) {
                return;
            }
        }

        if (visitedTiles.Count == 0
            && TryResolveAcidProjectileCollision(
                tick,
                projectile,
                players,
                events)) {
            return;
        }

        if (projectile.RemainingTicks == 0)
            SpawnAcidSplat(projectile);
    }

    private bool TryResolveAcidProjectileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null
            && projectile.LastCollisionTargetEntityId != playerTarget.EntityId) {
            projectile.LastCollisionTargetEntityId = playerTarget.EntityId;
            if (TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events,
                    damageShifted: true)) {
                SpawnAcidSplat(projectile);
                return true;
            }
        }

        if (!world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY))
            return false;

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && world.CanBreakWorldObject(@object)) {
            @object.Destroyed = true;
        }
        SpawnAcidSplat(projectile);
        return true;
    }

    private void SpawnAcidSplat(AuthoritativeProjectile projectile)
    {
        if (!world.Projectiles.Remove(projectile.EntityId))
            return;

        // Native Acid replaces the missile with a fresh entity in the same
        // tick. Admission may have reused a released missile ID, so reserve
        // the removed ID before allocating the replacement to keep the new
        // projectile visible to the current advancement pass.
        projectileEntityIds.Reserve(projectile.EntityId);
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            new AuthoritativeProjectile(
                entityId,
                projectile.SourceEntityId,
                0,
                projectile.SpellId,
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                projectile.PositionX,
                projectile.PositionY,
                0,
                AuthoritativeDamageType.Acid,
                0,
                1U) {
                Behavior = AuthoritativeProjectileBehavior.AcidSplat,
            });
    }

    /** Native ProcessAcidSplate shifts the visual one tile before creating its puddle. */
    private void AdvanceAcidSplat(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks != 0)
            return;

        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var puddlePositionX = checked(projectile.PositionX + 1);
        var puddlePositionY = checked(projectile.PositionY + 1);
        var puddleMapping = TryGetMonsterMissile(monster, out var mappedMissile)
            && mappedMissile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
            ? mappedMissile
            : null;
        var puddleRandomRange = puddleMapping?.PuddleDurationRandomRange > 0
            ? puddleMapping.PuddleDurationRandomRange
            : 15;
        var puddleDurationBase = puddleMapping?.PuddleDurationBase > 0
            ? puddleMapping.PuddleDurationBase
            : 40;
        var puddleDurationIntelligenceScale = puddleMapping?.PuddleDurationIntelligenceScale > 0
            ? puddleMapping.PuddleDurationIntelligenceScale
            : 40;
        var puddleDuration = checked(
            random.GenerateRnd(puddleRandomRange)
            + puddleDurationBase
            + puddleDurationIntelligenceScale * monster.Intelligence);
        var puddleDamageMinimumLevel = puddleMapping?.PuddleDamageMinimumMonsterLevel > 0
            ? puddleMapping.PuddleDamageMinimumMonsterLevel
            : 2;
        var puddleDamageLow = puddleMapping?.PuddleDamageLow > 0
            ? puddleMapping.PuddleDamageLow
            : 1;
        var puddleDamageHigh = puddleMapping?.PuddleDamageHigh > 0
            ? puddleMapping.PuddleDamageHigh
            : 2;
        var puddleDamage = GetEffectiveMonsterLevel(monster) >= puddleDamageMinimumLevel
            ? puddleDamageHigh
            : puddleDamageLow;
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            new AuthoritativeProjectile(
                entityId,
                projectile.SourceEntityId,
                0,
                projectile.SpellId,
                projectile.LevelId,
                puddlePositionX,
                puddlePositionY,
                puddlePositionX,
                puddlePositionY,
                puddleDamage,
                AuthoritativeDamageType.Acid,
                0,
                checked((uint)puddleDuration)) {
                Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
            });
        world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessAcidPuddle checks the occupied player every active tick. */
    private void AdvanceAcidPuddle(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null) {
            _ = TryResolveAcidPuddlePlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }

        // Native CheckMissileCol still breaks a breakable blocker even though
        // the persistent puddle restores its duration after the collision.
        BreakMissileBlockingObject(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);

        if (projectile.RemainingTicks == 0) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            var impactLifetime = 2;
            if (world.CombatTargets.TryGetValue(
                    projectile.SourceEntityId,
                    out var sourceMonster)
                && TryGetMonsterMissile(sourceMonster, out var puddleMapping)
                && puddleMapping.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                && puddleMapping.PuddleImpactLifetime > 0) {
                impactLifetime = puddleMapping.PuddleImpactLifetime;
            }
            projectile.RemainingTicks = checked((uint)impactLifetime);
        }
    }

    /** Native ProcessRhino movement for a Snake-owned charging controller. */
    private void AdvanceSnakeCharge(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)
            || monster.HitPoints <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)
                || world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)) {
                projectile.PositionX = previousX;
                projectile.PositionY = previousY;
                _ = TryResolveSnakeChargePlayerHit(
                    tick,
                    projectile,
                    monster,
                    players,
                    previousX,
                    previousY,
                    events);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            monster.ApplyKnockback(tile.X, tile.Y);
            if (TryResolveSnakeChargePlayerHit(
                    tick,
                    projectile,
                    monster,
                    players,
                    tile.X,
                    tile.Y,
                    events)) {
                if (!TryEnterMonsterProjectileImpact(projectile))
                    world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            previousX = tile.X;
            previousY = tile.Y;
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessRhino movement for a Gloom-owned movement-only charge. */
    private void AdvanceBatCharge(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)
            || monster.HitPoints <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        foreach (var tile in MoveNativeProjectile(projectile)) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)
                || world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)
                || world.IsOccupied(
                    projectile.LevelId,
                    tile.X,
                    tile.Y,
                    players,
                    monster.EntityId)) {
                monster.ApplyKnockback(previousX, previousY);
                world.Projectiles.Remove(projectile.EntityId);
                // Native MissToMonst: startHit(0) plays the Hit sound, choosing its variant.
                if (IsNativeCatacombsActor(monster))
                    random.AdvanceRndSeed();
                return;
            }

            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            monster.ApplyKnockback(tile.X, tile.Y);
            previousX = tile.X;
            previousY = tile.Y;
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native MonsterAttackPlayer(500, special damage) at a Snake charge collision. */
    private bool TryResolveSnakeChargePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster,
        IReadOnlyList<AuthoritativePlayerState> players,
        int positionX,
        int positionY,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var target = players.FirstOrDefault(player =>
            player.Life > 0
            && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                or DiabloPlayerCombatMode.Quit)
            && player.LevelId == projectile.LevelId
            && player.PositionX == positionX
            && player.PositionY == positionY);
        if (target is null
            || IsPlayerProtectedByFlash(target)
            || IsPlayerEtherealized(target))
            return false;
        // One compact native replay pack records a secondary participant as
        // the selected target without carrying its impact transition. Keep
        // that exception data-driven; ordinary Snake charges hit any player.
        if (TryGetMonsterMissile(monster, out var missile)
            && missile.SuppressNonPrimaryPlayerImpact
            && players.Count > 1
            && target.EntityId != players[0].EntityId)
            return false;

        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        if (distance >= 2)
            return false;

        var targetClass = GetPlayerClass(target);
        var armorClass = target.ArmorClass;
        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            armorClass = AuthoritativeEquipmentIncomingDamageRules
                .ApplyHellfireMonsterClassArmor(
                    armorClass,
                    monster.IsDemon,
                    monster.IsUndead,
                    target.Equipment,
                    rules.HellfireArmorBonusAgainstDemons,
                    rules.HellfireArmorBonusAgainstUndead);
        }
        var hitChance = rules.ResolveMonsterChargeHitChance(
            GetEffectiveMonsterLevel(monster),
            target.CharacterLevel,
            target.LevelId,
            armorClass);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

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

        var specialDamageRange = GetEffectiveMonsterDamageRange(monster, special: true);
        var damageQ6 = checked(
            Math.Max(
                checked((projectile.Damage > 0
                        ? projectile.Damage
                        : random.RandomIntBetween(
                            specialDamageRange.Minimum,
                            specialDamageRange.Maximum))
                    * DeterministicFixedPointQ6.Scale)
                    + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                        * DeterministicFixedPointQ6.Scale,
                DeterministicFixedPointQ6.Scale));
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
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
        else if (ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }
}
