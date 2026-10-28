using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Succubus, skeleton archer, rhino and special-attack AI, and player minions (native monster.cpp). */
public sealed partial class DiabloCombatCommandProcessor
{
    /** Native AddInfernoControl state for a monster-owned Mega projectile. */
    private AuthoritativeProjectile CreateMonsterInfernoController(
        uint projectileEntityId,
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        uint spellId,
        int speed,
        int lifetime,
        int impactLifetime,
        AuthoritativeDamageType damageType,
        int damage)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var targetX = player.PositionX;
        var targetY = player.PositionY;
        if (targetX == monster.PositionX && targetY == monster.PositionY) {
            var displacement = DirectionDisplacements[direction];
            targetX += displacement.X;
            targetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            monster.EntityId,
            player.EntityId,
            spellId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.InfernoController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = impactLifetime,
            ResistantDamageDivisor = 1,
        };
    }

    /** Native Blood Star dispatch selected by a validated missile mapping. */
    private bool TryAdvanceSuccubusRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        AuthoritativeMonsterMissileDefinition? missile = null;
        if (monsterMissiles is not null) {
            if (!TryGetMonsterMissile(monster, out var mappedMissile)
                || mappedMissile.ProjectileBehavior
                    != AuthoritativeProjectileBehavior.BloodStar)
                return false;
            missile = mappedMissile;
        } else if (!UsesNativeAi(monster, "Succubus")
            && !UsesNativeAi(monster, "LazarusSuccubus")) {
            return false;
        }

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        if (monster.AdvanceRangedDelay())
            return true;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar))
            return true;
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var retreatTuning = ResolveRangedRetreatTuning(missile);
        var retreatChance = checked(
            retreatTuning.ChanceBase
            + retreatTuning.IntelligenceScale * monster.Intelligence);
        if (distance < retreatTuning.MinimumDistance) {
            var retreatRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)(attackSeed % 100U);
            if (retreatRoll < Math.Min(100, retreatChance)) {
                var retreat = usesNativeAiStream
                    ? FindNativeRandomWalkStepFromRandom(
                        monster,
                        RotateDirection(
                            GetDirection(
                                monster.PositionX,
                                monster.PositionY,
                                player.PositionX,
                                player.PositionY),
                            4),
                        players)
                    : FindGoatRangedRetreatDestination(monster, player, players, distance);
                if (retreat.HasValue) {
                    monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                    return true;
                }
            }
        }

        if (!world.HasMissileLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            return true;
        }

        var spell = spells.Definitions.FirstOrDefault(definition =>
            definition.ProjectileBehavior == AuthoritativeProjectileBehavior.BloodStar);
        var spellId = missile is { SpellId: > 0 }
            ? missile.SpellId
            : spell?.SpellId ?? 36U;
        var speed = missile?.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : spell?.ProjectileSpeed > 0
                ? spell.ProjectileSpeed
                : 16;
        var lifetime = missile?.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : spell?.ProjectileLifetime > 0
                ? spell.ProjectileLifetime
                : 256;
        var damageType = missile?.DamageType
            ?? spell?.DamageType
            ?? AuthoritativeDamageType.Magic;
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            speed);
        var damage = missile?.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : SelectEffectiveMonsterDamage(
                monster,
                special: false,
                seed: attackSeed);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            spellId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.BloodStar,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        monster.StartRangedDelay(
            usesNativeAiStream
                ? ResolveRangedDelayFromRandom(missile)
                : ResolveRangedDelay(missile, attackSeed));
        return true;
    }

    /** Native Arrow admission captures the resolved hit before travel. */
    private bool CreateNativeRangedAttackProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        int distance,
        uint attackSeed,
        AuthoritativeMonsterMissileDefinition? missile = null)
    {
        missile ??= TryGetMonsterMissile(monster, out var mappedMissile)
            ? mappedMissile
            : null;
        var armorClass = player.ArmorClass;
        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            armorClass = AuthoritativeEquipmentIncomingDamageRules
                .ApplyHellfireMonsterClassArmor(
                    armorClass,
                    monster.IsDemon,
                    monster.IsUndead,
                    player.Equipment,
                    rules.HellfireArmorBonusAgainstDemons,
                    rules.HellfireArmorBonusAgainstUndead);
        }
        // Native ProcessArrow uses the monster range when it is non-fixed;
        // fixed rows retain a configured missile payload when one is present.
        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        var hasConfiguredDamage = missile is not null
            && missile.ProjectileDamage > 0
            && damageRange.Minimum == damageRange.Maximum;
        var baseDamage = hasConfiguredDamage
            ? missile!.ProjectileDamage
            : missile is not null && missile.ProjectileDamage == 0
                ? damageRange.Minimum == damageRange.Maximum
                    ? damageRange.Maximum
                    : monster.AttackDamage
                : damageRange.Maximum;
        var damage = rules.ResolveMonsterRangedAttackDamage(
            baseDamage,
            GetEffectiveMonsterToHit(monster, special: false),
            GetEffectiveMonsterLevel(monster),
            player.CharacterLevel,
            monster.LevelId,
            armorClass,
            distance,
            attackSeed);
        var projectileSpeed = missile?.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : 1;
        var projectileLifetime = missile?.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : Math.Max(1, distance);
        var damageType = missile?.DamageType ?? AuthoritativeDamageType.Physical;

        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            spellId: 0,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            areaRadius: 0,
            checked((uint)projectileLifetime),
            allowZeroSpellId: true) {
            // The content row selects the native Arrow identity; the module
            // resolves both explicit MonsterArrow rows and legacy Targeted
            // overlays to this server-owned monster lifecycle.
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            Speed = projectileSpeed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
            DamageMinimum = hasConfiguredDamage
                ? 0
                : damageRange.Minimum,
            DamageMaximum = hasConfiguredDamage
                ? 0
                : damageRange.Maximum,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        return true;
    }

    private bool HasActiveMonsterArrow(AuthoritativeCombatTarget monster)
        => world.Projectiles.Values.Any(projectile =>
            projectile.SourceEntityId == monster.EntityId
            && projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow);

    /** Native SkeletonBow AI retreats when close before considering its arrow. */
    private bool TryAdvanceSkeletonRangedRetreat(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var skeletonRangedTuning = GetSkeletonRangedTuning(monster);
        if (!skeletonRangedTuning.HasValue
            || distance >= skeletonRangedTuning.Value.RetreatMinimumDistance)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var retreatChance = GetSkeletonRangedMovementChance(
            monster,
            skeletonRangedTuning.Value.RetreatChanceBase,
            skeletonRangedTuning.Value.RetreatChanceIntelligenceScale,
            skeletonRangedTuning.Value.MovementFreshChanceBonus);
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)((actionSeed / 100U) % 100U);
        if (movementRoll >= retreatChance) {
            monster.AdvanceSkeletonRangedMovementIdle();
            return false;
        }

        var retreat = FindGoatRangedRetreatDestination(
            monster,
            player,
            players,
            distance);
        if (!retreat.HasValue) {
            monster.AdvanceSkeletonRangedMovementIdle();
            return false;
        }
        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
        monster.RecordSkeletonRangedMovement(
            retreat.Value.X - monster.PositionX,
            retreat.Value.Y - monster.PositionY);
        return true;
    }

    /** Native SkeletonBow close-range movement admission across var2 bands. */
    private static int GetSkeletonRangedMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.SkeletonRangedMovementIdleTicks == -1
            ? longIdleChance
            : monster.SkeletonRangedMovementIdleTicks > 20
                ? longIdleChance
                : monster.SkeletonRangedMovementIdleTicks == 0
                    && monster.SkeletonRangedMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native SkeletonBow AI keeps a clear-lane monster ranged-only. */
    private bool TryAdvanceSkeletonRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var skeletonRangedTuning = GetSkeletonRangedTuning(monster);
        if (!skeletonRangedTuning.HasValue
            || !HasValidatedArrowMapping(monster)
            || GetEffectiveMonsterToHit(monster, special: false) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: false).Maximum <= 0)
            return false;

        if (IsPlayerProtectedByFlash(player) || IsPlayerEtherealized(player))
            return true;

        var nativeCatacombsRangedController = TryGetMonsterMissile(monster, out var nativeCatacombsMissile)
            && IsNativeCatacombsRangedController(monster, nativeCatacombsMissile);
        if (HasActiveMonsterArrow(monster) && !nativeCatacombsRangedController)
            return true;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var attackChance = Math.Min(
            100,
            skeletonRangedTuning.Value.AttackChanceBase
                + (skeletonRangedTuning.Value.AttackChanceIntelligenceScale
                    * monster.Intelligence));
        if (distance >= skeletonRangedTuning.Value.RetreatMinimumDistance
            && usesNativeAiStream)
            _ = random.RandomIntLessThan(100);
        var attackRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (!world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY))
            // Native SkeletonBowAi does not pursue through a blocked arrow
            // lane. Its close-range retreat branch has already run above;
            // preserve the remaining no-op instead of generic navigation.
            return true;
        if (attackRoll >= attackChance)
            return true;

        if (nativeCatacombsRangedController
            && TryStartNativeCatacombsRangedAction(monster, player, nativeCatacombsMissile, attackSeed))
            return true;

        return CreateNativeRangedAttackProjectile(
            monster,
            player,
            distance,
            attackSeed);
    }

    private (
        int RetreatMinimumDistance,
        int RetreatChanceBase,
        int RetreatChanceIntelligenceScale,
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int MovementFreshChanceBonus)? GetSkeletonRangedTuning(
            AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonRangedRetreatMinimumDistance > 0
                && definition.SkeletonRangedRetreatChanceBase > 0
                && definition.SkeletonRangedRetreatChanceIntelligenceScale > 0
                && definition.SkeletonRangedAttackChanceBase > 0
                && definition.SkeletonRangedAttackChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonRangedRetreatMinimumDistance,
                    definition.SkeletonRangedRetreatChanceBase,
                    definition.SkeletonRangedRetreatChanceIntelligenceScale,
                    definition.SkeletonRangedAttackChanceBase,
                    definition.SkeletonRangedAttackChanceIntelligenceScale,
                    definition.SkeletonRangedMovementFreshChanceBonus > 0
                        ? definition.SkeletonRangedMovementFreshChanceBonus
                        : 50);
            }
        }
        return UsesNativeAi(target, "SkeletonRanged")
            ? (4, 13, 2, 3, 2, 50)
            : null;
    }

    /** Resolves mapped missiles through explicit runtime identity before catalog fallback. */
    private bool TryGetMonsterMissile(
        AuthoritativeCombatTarget monster,
        out AuthoritativeMonsterMissileDefinition missile)
    {
        if (monsterMissiles is null) {
            missile = null!;
            return false;
        }
        // A unique encounter may retain a generic monster type while its
        // explicit runtime AI identity selects a different native missile.
        // Resolve that identity first; catalog metadata remains the fallback
        // for older targets that do not carry an explicit label.
        if (!string.IsNullOrWhiteSpace(monster.NativeAi)
            && monsterMissiles.TryGet(
                monster.NativeAi,
                monster.MonsterId,
                out missile))
            return true;
        if (monsterTypes?.TryGet(monster.MonsterId, out var definition) == true
            && monsterMissiles.TryGet(
                definition.NativeAi,
                monster.MonsterId,
                out missile))
            return true;
        // Native Zhar and Lazarus use CounselorAi's intelligence-selected
        // missile family; legacy aliases retain the validated Counselor row
        // when an older target has no alias-specific mapping.
        if (monster.NativeAi is "Zhar" or "Lazarus")
            return monsterMissiles.TryGet("Counselor", monster.MonsterId, out missile);
        missile = null!;
        return false;
    }

    /** Validated arrow mappings keep ranged-only monsters out of adjacent melee. */
    private bool HasValidatedArrowMapping(AuthoritativeCombatTarget monster)
    {
        return TryGetMonsterMissile(monster, out var missile)
            && missile.ProjectileBehavior is AuthoritativeProjectileBehavior.MonsterArrow
                or AuthoritativeProjectileBehavior.Targeted;
    }

    /** Native Rhino AI charge: clear long-range lanes end in a special melee hit. */
    private bool TryAdvanceRhinoCharge(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        int distance)
    {
        var rhinoTuning = GetRhinoTuning(monster);
        if (!rhinoTuning.HasValue)
            return false;

        if (monster.AdvanceRhinoDelay())
            return true;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (distance < 2) {
            monster.ClearRhinoMovement();
            var adjacentAttackChance = Math.Min(
                100,
                28 + (2 * monster.Intelligence));
            return actionRoll >= adjacentAttackChance;
        }

        var hasMoveGoal = monster.RhinoMovementTicks >= 0;
        var movementGoalTuning = GetRhinoMovementGoalTuning(monster);
        var startsMoveGoal = distance >= movementGoalTuning.MinimumDistance
            && (hasMoveGoal
                || (usesNativeAiStream
                    ? random.RandomIntLessThan(movementGoalTuning.ChanceModulo) != 0
                    : (attackSeed / 100U) % (uint)movementGoalTuning.ChanceModulo != 0U));
        if (hasMoveGoal || startsMoveGoal) {
            if (!hasMoveGoal)
                monster.StartRhinoMovement(
                    usesNativeAiStream
                        ? random.RandomIntLessThan(2)
                        : (int)((attackSeed / 100U) % 2U));
            if (monster.AdvanceRhinoMovement(
                    distance,
                    world.HasAiLineOfSight(
                        monster.LevelId,
                        monster.PositionX,
                        monster.PositionY,
                        player.PositionX,
                        player.PositionY))) {
                var roundWalk = usesNativeAiStream
                    ? FindNativeStatefulRoundWalkStepFromRandom(
                        monster,
                        player,
                        players,
                        monster.RhinoMovementTurn,
                        out var nextTurn)
                    : FindNativeStatefulRoundWalkStep(
                        monster,
                        player,
                        players,
                        attackSeed,
                        monster.RhinoMovementTurn,
                        out nextTurn);
                monster.UpdateRhinoMovementTurn(nextTurn);
                if (roundWalk.HasValue) {
                    monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                    monster.RecordRhinoMovement(
                        roundWalk.Value.X - monster.PositionX,
                        roundWalk.Value.Y - monster.PositionY);
                } else {
                    monster.StartRhinoDelay(
                        usesNativeAiStream
                            ? ResolveRhinoDelayFromRandom(monster)
                            : ResolveRhinoDelay(monster, attackSeed));
                }
                return true;
            }
        }

        if (distance < rhinoTuning.Value.MinimumDistance
            || GetEffectiveMonsterToHit(monster, special: true) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: true).Maximum <= 0) {
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);
        }

        var chargeChance = Math.Min(
            100,
            rhinoTuning.Value.ChanceBase
                + (rhinoTuning.Value.IntelligenceScale * monster.Intelligence));
        if (actionRoll >= chargeChance
            || !world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY))
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);

        var destination = FindRhinoChargeDestination(monster, player, players);
        if (!destination.HasValue)
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);

        monster.ApplyKnockback(destination.Value.X, destination.Value.Y);
        var damageQ6 = usesNativeAiStream
            ? RollEffectiveMonsterDamageQ6(monster, special: true)
            : SelectNativeSpecialDamageQ6(monster, attackSeed);
        damageQ6 = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHitQ6(
            damageQ6,
            DeterministicFixedPointQ6.Scale,
            player.Equipment);
        var usesLegacyWholeLife = player.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
        var appliedDamageQ6 = usesLegacyWholeLife
            ? DeterministicFixedPointQ6.ToWholeTruncated(damageQ6)
            : damageQ6;
        var damageResult = playerDamage.Apply(player, appliedDamageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            var eventDamageQ6 = usesLegacyWholeLife
                ? damageQ6
                : damageResult.IncomingDamageQ6;
            events.Add(new DiabloDirectedGameplayEvent(
                player,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(eventDamageQ6)) {
                    AmountQ6 = eventDamageQ6,
                }));
        }
        if (player.Life == 0)
            StartPlayerDeath(
                player,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (damageQ6 > 0)
            ApplyRhinoChargeKnockback(monster, player, players);
        return true;
    }

    /** Native Rhino movement after charge admission fails or its lane is blocked. */
    private bool TryAdvanceRhinoMovementFallback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        bool usesNativeAiStream = false)
    {
        if (usesNativeAiStream) {
            var nativeMovementTuning = GetRhinoMovementTuning(monster);
            var nativeMovementChance = monster.RhinoMovementMode is >= 1 and <= 3
                && monster.RhinoMovementIdleTicks == 0
                ? Math.Min(
                    100,
                    nativeMovementTuning.ChanceBase
                        + (nativeMovementTuning.IntelligenceScale * monster.Intelligence)
                        + nativeMovementTuning.FreshChanceBonus)
                : Math.Min(
                    100,
                    nativeMovementTuning.ChanceBase
                        + (nativeMovementTuning.IntelligenceScale * monster.Intelligence));
            var movementRoll = random.RandomIntLessThan(100);
            if (movementRoll < nativeMovementChance) {
                var randomWalk = FindNativeRandomWalkStepFromRandom(
                    monster,
                    player,
                    players);
                if (randomWalk.HasValue) {
                    monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
                    monster.RecordRhinoMovement(
                        randomWalk.Value.X - monster.PositionX,
                        randomWalk.Value.Y - monster.PositionY);
                } else {
                    monster.AdvanceRhinoMovementIdle();
                }
            } else {
                monster.AdvanceRhinoMovementIdle();
                monster.StartRhinoDelay(ResolveRhinoDelayFromRandom(monster));
            }
            return true;
        }

        var movementGoalTuning = GetRhinoMovementGoalTuning(monster);
        if (distance >= movementGoalTuning.MinimumDistance
            && (seed / 100U) % (uint)movementGoalTuning.ChanceModulo != 0U) {
            var roundWalk = FindNativeRoundWalkStep(
                monster,
                player,
                players,
                seed);
            if (roundWalk.HasValue) {
                monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                monster.RecordRhinoMovement(
                    roundWalk.Value.X - monster.PositionX,
                    roundWalk.Value.Y - monster.PositionY);
            }
            return true;
        }

        var movementTuning = GetRhinoMovementTuning(monster);
        var movementChance = GetRhinoMovementChance(
            monster,
            movementTuning.ChanceBase,
            movementTuning.IntelligenceScale,
            movementTuning.FreshChanceBonus);
        if ((seed / 100U) % 100U < (uint)movementChance) {
            var randomWalk = FindNativeRandomWalkStep(
                monster,
                player,
                players,
                seed);
            if (randomWalk.HasValue) {
                monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
                monster.RecordRhinoMovement(
                    randomWalk.Value.X - monster.PositionX,
                    randomWalk.Value.Y - monster.PositionY);
            } else {
                monster.AdvanceRhinoMovementIdle();
            }
        } else {
            monster.AdvanceRhinoMovementIdle();
            monster.StartRhinoDelay(ResolveRhinoDelay(monster, seed));
        }
        return true;
    }

    /** Native Rhino ordinary movement admission across var2 bands. */
    private static int GetRhinoMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.RhinoMovementIdleTicks == -1
            ? longIdleChance
            : monster.RhinoMovementIdleTicks > 20
                ? longIdleChance
                : monster.RhinoMovementIdleTicks == 0
                    && monster.RhinoMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    private (int ChanceBase, int IntelligenceScale, int FreshChanceBonus)
        GetRhinoMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoMovementChanceBase > 0
            && definition.RhinoMovementChanceIntelligenceScale > 0) {
            return (
                definition.RhinoMovementChanceBase,
                definition.RhinoMovementChanceIntelligenceScale,
                definition.RhinoMovementFreshChanceBonus > 0
                    ? definition.RhinoMovementFreshChanceBonus
                    : 50);
        }
        return (33, 2, 50);
    }

    private (int MinimumDistance, int ChanceModulo) GetRhinoMovementGoalTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoMovementGoalMinimumDistance > 0
            && definition.RhinoMovementGoalChanceModulo > 0) {
            return (
                definition.RhinoMovementGoalMinimumDistance,
                definition.RhinoMovementGoalChanceModulo);
        }
        return (5, 4);
    }

    private (int MinimumDistance, int ChanceBase, int IntelligenceScale)? GetRhinoTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.RhinoChargeMinimumDistance > 0
                && definition.RhinoChargeChanceBase > 0
                && definition.RhinoChargeChanceIntelligenceScale > 0) {
                return (
                    definition.RhinoChargeMinimumDistance,
                    definition.RhinoChargeChanceBase,
                    definition.RhinoChargeChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Rhino")
            ? (5, 43, 2)
            : null;
    }

    private int ResolveRhinoDelay(
        AuthoritativeCombatTarget target,
        uint seed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoDelayBase > 0
            && definition.RhinoDelayRandomRange > 0) {
            delayBase = definition.RhinoDelayBase;
            randomRange = definition.RhinoDelayRandomRange;
        }
        return checked(delayBase + (int)((seed / 200U) % (uint)randomRange));
    }

    /** Native Rhino recovery delay driven by the restored per-monster stream. */
    private int ResolveRhinoDelayFromRandom(AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoDelayBase > 0
            && definition.RhinoDelayRandomRange > 0) {
            delayBase = definition.RhinoDelayBase;
            randomRange = definition.RhinoDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private (int X, int Y)? FindRhinoChargeDestination(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var candidates = new List<(int X, int Y)>();
        for (var offsetY = -1; offsetY <= 1; offsetY++) {
            for (var offsetX = -1; offsetX <= 1; offsetX++) {
                if (offsetX == 0 && offsetY == 0)
                    continue;
                candidates.Add((player.PositionX + offsetX, player.PositionY + offsetY));
            }
        }
        return candidates
            .Where(candidate => world.IsWalkable(monster.LevelId, candidate.X, candidate.Y)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidate.X,
                    candidate.Y,
                    players,
                    monster.EntityId)
                && world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    candidate.X,
                    candidate.Y))
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.X,
                candidate.Y,
                monster.PositionX,
                monster.PositionY))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .Select(candidate => ((int X, int Y)?)candidate)
            .FirstOrDefault();
    }

    private void ApplyRhinoChargeKnockback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var destinationX = checked(player.PositionX + displacement.X);
        var destinationY = checked(player.PositionY + displacement.Y);
        if (!world.IsWalkable(monster.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                monster.LevelId,
                destinationX,
                destinationY,
                players,
                player.EntityId)) {
            return;
        }
        DiabloPlayerActionLifecycle.Cancel(player);
        player.PositionX = destinationX;
        player.PositionY = destinationY;
    }

    /** Validated Fat tuning selects its special melee band. */
    private bool UsesFatSpecialAttack(
        AuthoritativeCombatTarget target,
        int attackRoll,
        (
            int AttackChanceBase,
            int AttackChanceIntelligenceScale,
            int SpecialChanceBase,
            int SpecialChanceIntelligenceScale)? tuning)
    {
        return tuning.HasValue
            // Native Fat AI admits its Special band even when that graphic has
            // no damage/action frame. Preserve older authored/C1 admission.
            && (target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8,
                    NativeSpecialAttackFrameCount: > 0 }
                || GetEffectiveMonsterToHit(target, special: true) > 0
                    && GetEffectiveMonsterDamageRange(target, special: true).Maximum > 0)
            && attackRoll >= Math.Min(
                100,
                tuning.Value.SpecialChanceBase
                    + (tuning.Value.SpecialChanceIntelligenceScale
                        * target.Intelligence))
            && attackRoll < Math.Min(
                100,
                tuning.Value.AttackChanceBase
                    + (tuning.Value.AttackChanceIntelligenceScale
                        * target.Intelligence));
    }

    private static bool NativeFatAttackBandAllowsAttack(
        AuthoritativeCombatTarget target,
        int attackRoll,
        (
            int AttackChanceBase,
            int AttackChanceIntelligenceScale,
            int SpecialChanceBase,
            int SpecialChanceIntelligenceScale) tuning)
    {
        return attackRoll < Math.Min(
            100,
            tuning.AttackChanceBase
                + (tuning.AttackChanceIntelligenceScale * target.Intelligence));
    }

    /** Validated GoatMelee tuning selects a wounded special on its configured roll. */
    private bool UsesGoatSpecialAttack(
        AuthoritativeCombatTarget target,
        uint attackSeed,
        (
            int ChanceBase,
            int IntelligenceScale,
            int SpecialHealthThresholdPercent,
            int SpecialChanceGroupPeriod)? tuning)
    {
        return tuning.HasValue
            && (long)target.HitPointsQ6 * 100
                < (long)target.MaxHitPointsQ6 * tuning.Value.SpecialHealthThresholdPercent
            && GetEffectiveMonsterToHit(target, special: true) > 0
            && GetEffectiveMonsterDamageRange(target, special: true).Maximum > 0
            && (attackSeed / 100U) % (uint)tuning.Value.SpecialChanceGroupPeriod == 0U;
    }

    /** Native GoatMelee wounded-special admission driven by the restored stream. */
    private bool UsesGoatSpecialAttackFromRandom(
        AuthoritativeCombatTarget target,
        (
            int ChanceBase,
            int IntelligenceScale,
            int SpecialHealthThresholdPercent,
            int SpecialChanceGroupPeriod)? tuning)
    {
        if (!tuning.HasValue
            || (long)target.HitPointsQ6 * 100
                >= (long)target.MaxHitPointsQ6 * tuning.Value.SpecialHealthThresholdPercent
            || GetEffectiveMonsterToHit(target, special: true) <= 0
            || GetEffectiveMonsterDamageRange(target, special: true).Maximum <= 0)
            return false;

        var groupPeriod = tuning.Value.SpecialChanceGroupPeriod;
        return groupPeriod <= 1
            || random.RandomIntLessThan(groupPeriod) == 0;
    }

    /** Selects the native inclusive special damage range without adding a new RNG draw. */
    private int SelectNativeSpecialDamage(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        return SelectEffectiveMonsterDamage(target, special: true, seed: attackSeed);
    }

    /** Selects compatibility-stream Rhino damage in the native Q6 unit. */
    private int SelectNativeSpecialDamageQ6(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var range = GetEffectiveMonsterDamageRange(target, special: true);
        var minimumQ6 = DeterministicFixedPointQ6.FromWhole(range.Minimum);
        var maximumQ6 = DeterministicFixedPointQ6.FromWhole(range.Maximum);
        if (maximumQ6 < minimumQ6)
            return 0;
        var widthQ6 = checked(maximumQ6 - minimumQ6 + 1);
        return checked(minimumQ6 + (int)(attackSeed % (uint)widthQ6));
    }

    private static int SelectNativeDamage(int minimum, int maximum, uint attackSeed)
    {
        var range = checked(maximum - minimum + 1);
        return checked(minimum + (int)(attackSeed % (uint)range));
    }

    private void AdvancePlayerMinion(
        AuthoritativeCombatTarget minion,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player => player.EntityId == minion.OwnerPlayerId);
        // Preserve legacy faction-only minion markers used by non-Golem effects and
        // older snapshots which did not carry an owner identity.
        if (owner is null && minion.OwnerPlayerId == 0)
            return;
        if (owner is null
            || owner.Life <= 0
            || owner.CombatMode is DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit
            || owner.LevelId != minion.LevelId
            || (minion.HitPoints <= 0 && minion.DeathAnimationTicks == 0)) {
            world.CombatTargets.Remove(minion.EntityId);
            return;
        }
        if (minion.HitPoints <= 0) {
            if (minion.AdvanceDeathAnimation())
                world.CombatTargets.Remove(minion.EntityId);
            return;
        }
        if (minion.IsPetrified)
            return;

        var enemy = world.CombatTargets.Values
            .Where(candidate => candidate.EntityId != minion.EntityId
                && !candidate.IsPlayerMinion
                && candidate.HitPoints > 0
                && (candidate.LevelId == 0 || candidate.LevelId == minion.LevelId))
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.PositionX,
                candidate.PositionY,
                minion.PositionX,
                minion.PositionY))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (enemy is null) {
            TryBeginPlayerMinionIdleWalk(minion, owner, players);
            return;
        }

        // Native GolumAi refreshes its facing before either path planning or
        // the blocked-path RandomWalk fallback. Clients only receive the
        // resulting authoritative direction and movement.
        minion.RestoreDirection(GetDirection(
            minion.PositionX,
            minion.PositionY,
            enemy.PositionX,
            enemy.PositionY));
        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            enemy.PositionX,
            enemy.PositionY,
            minion.PositionX,
            minion.PositionY));
        if (distance <= 1) {
            var rolledDamage = random.RandomIntBetween(minion.MinDamage, minion.MaxDamage);
            var damage = rules.ResolveAttackDamage(
                rolledDamage,
                enemy,
                unchecked((uint)tick ^ minion.EntityId ^ enemy.EntityId));
            var minionEvents = new List<DiabloGameplayEvent>();
            ApplyResolvedDamageQ6(
                owner,
                enemy,
                DeterministicFixedPointQ6.FromWhole(damage),
                minionEvents);
            foreach (var minionEvent in minionEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, minionEvent));
            return;
        }

        var nextStep = FindPlayerMinionStep(minion, enemy, players);
        if (nextStep.HasValue)
            minion.BeginMovement(nextStep.Value.X, nextStep.Value.Y);
        else if (FindNativeRandomWalkStepFromRandom(
                     minion,
                     (int)owner.FacingDirection,
                     players) is { } randomWalk)
            minion.BeginMovement(randomWalk.X, randomWalk.Y);
        else {
            // GolumAi sweeps every direction clockwise from its current
            // facing after a failed RandomWalk, without invoking generic
            // navigation. Preserve that bounded native fallback order.
            foreach (var direction in DeterministicGridDirections.EnumerateClockwise(
                         minion.Direction)) {
                if (FindStepInDirection(minion, direction, players) is not { } step)
                    continue;
                minion.BeginMovement(step.X, step.Y);
                break;
            }
        }
    }

    private void TryBeginPlayerMinionIdleWalk(
        AuthoritativeCombatTarget minion,
        AuthoritativePlayerState owner,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var ownerDirection = (int)owner.FacingDirection;
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            ownerDirection,
            firstTurn,
            secondTurn);

        foreach (var direction in directions) {
            if (TryBeginPlayerMinionWalk(minion, direction, players))
                return;
        }

        var fallbackDirection = RotateDirection(ownerDirection, -1);
        for (var index = 0; index < DirectionDisplacements.Count; index++) {
            fallbackDirection = RotateDirection(fallbackDirection, 1);
            if (TryBeginPlayerMinionWalk(minion, fallbackDirection, players))
                return;
        }
    }

    private bool TryBeginPlayerMinionWalk(
        AuthoritativeCombatTarget minion,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(minion.PositionX + displacement.X);
        var candidateY = checked(minion.PositionY + displacement.Y);
        if (!world.IsWalkable(minion.LevelId, candidateX, candidateY)
            || world.IsOccupied(
                minion.LevelId,
                candidateX,
                candidateY,
                players,
                minion.EntityId)) {
            return false;
        }
        minion.BeginMovement(candidateX, candidateY);
        return true;
    }

    private (int X, int Y)? FindPlayerMinionStep(
        AuthoritativeCombatTarget minion,
        AuthoritativeCombatTarget enemy,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var start = (X: minion.PositionX, Y: minion.PositionY);
        var goals = new HashSet<(int X, int Y)>();
        for (var offsetY = -1; offsetY <= 1; offsetY++) {
            for (var offsetX = -1; offsetX <= 1; offsetX++) {
                if (offsetX == 0 && offsetY == 0)
                    continue;
                var goal = (X: enemy.PositionX + offsetX, Y: enemy.PositionY + offsetY);
                if (world.IsWalkable(minion.LevelId, goal.X, goal.Y)
                    && !world.IsOccupied(
                        minion.LevelId,
                        goal.X,
                        goal.Y,
                        players,
                        minion.EntityId)) {
                    goals.Add(goal);
                }
            }
        }
        if (goals.Count == 0)
            return null;

        if (goals.Contains(start))
            return start;
        return DeterministicGridNavigation.FindStepToGoal(
            start.X,
            start.Y,
            int.MaxValue,
            (x, y) => goals.Contains((x, y)),
            (x, y) => world.IsWalkable(minion.LevelId, x, y),
            (x, y) => world.IsOccupied(
                minion.LevelId,
                x,
                y,
                players,
                minion.EntityId));
    }
}
