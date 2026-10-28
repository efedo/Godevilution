using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Avoidance, mapped charge and ranged monster AI and their delays (native monster.cpp). */
public sealed partial class DiabloCombatCommandProcessor
{
    /** Native AiAvoidance far-distance Move goal shared by GoatMelee/Gargoyle. */
    private bool TryAdvanceAiAvoidanceMoveGoal(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance,
        uint seed)
    {
        var movementMinimumDistance = 4;
        var movementChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var avoidanceDefinition) == true
            && avoidanceDefinition.AiAvoidanceMovementMinimumDistance > 0
            && avoidanceDefinition.AiAvoidanceMovementChanceModulo > 0) {
            movementMinimumDistance = avoidanceDefinition.AiAvoidanceMovementMinimumDistance;
            movementChanceModulo = avoidanceDefinition.AiAvoidanceMovementChanceModulo;
        }
        if (distance < 2) {
            monster.ClearAvoidanceMovement();
            return false;
        }

        var hasMoveGoal = monster.AvoidanceMovementTicks >= 0;
        var startsMoveGoal = distance >= movementMinimumDistance
            && (seed / 100U) % (uint)movementChanceModulo == 0U;
        if (!hasMoveGoal && !startsMoveGoal)
            return false;

        if (!hasMoveGoal)
            monster.StartAvoidanceMovement((int)((seed / 100U) % 2U));
        if (!monster.AdvanceAvoidanceMovement(
                distance,
                world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY),
                IsNativeAvoidanceDirectionAvailable(
                    monster,
                    player,
                    players)))
            return false;

        var roundWalk = FindNativeStatefulRoundWalkStep(
            monster,
            player,
            players,
            seed,
            monster.AvoidanceMovementTurn,
            out var nextTurn);
        monster.UpdateAvoidanceMovementTurn(nextTurn);
        if (roundWalk.HasValue) {
            monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                roundWalk.Value.X - monster.PositionX,
                roundWalk.Value.Y - monster.PositionY);
        } else {
            monster.StartAvoidanceDelay(ResolveAvoidanceDelay(monster, seed));
        }
        return true;
    }

    /** Native AiAvoidance Move goal driven by the restored monster stream. */
    private bool TryAdvanceAiAvoidanceMoveGoalFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance,
        int actionRoll)
    {
        var movementMinimumDistance = 4;
        var movementChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var avoidanceDefinition) == true
            && avoidanceDefinition.AiAvoidanceMovementMinimumDistance > 0
            && avoidanceDefinition.AiAvoidanceMovementChanceModulo > 0) {
            movementMinimumDistance = avoidanceDefinition.AiAvoidanceMovementMinimumDistance;
            movementChanceModulo = avoidanceDefinition.AiAvoidanceMovementChanceModulo;
        }
        if (distance < 2) {
            monster.ClearAvoidanceMovement();
            return false;
        }

        var hasMoveGoal = monster.AvoidanceMovementTicks >= 0;
        var startsMoveGoal = distance >= movementMinimumDistance
            && random.FlipCoin((uint)movementChanceModulo);
        if (!hasMoveGoal && !startsMoveGoal)
            return false;

        if (!hasMoveGoal)
            monster.StartAvoidanceMovement(random.RandomIntLessThan(2));
        if (!monster.AdvanceAvoidanceMovement(
                distance,
                world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY),
                IsNativeAvoidanceDirectionAvailable(
                    monster,
                    player,
                    players)))
            return false;

        var roundWalkChanceBase = 16;
        var roundWalkChanceIntelligenceScale = 5;
        if (monsterTypes?.TryGet(monster.MonsterId, out var roundWalkDefinition) == true
            && roundWalkDefinition.AiAvoidanceRoundWalkChanceBase > 0
            && roundWalkDefinition.AiAvoidanceRoundWalkChanceIntelligenceScale > 0) {
            roundWalkChanceBase = roundWalkDefinition.AiAvoidanceRoundWalkChanceBase;
            roundWalkChanceIntelligenceScale = roundWalkDefinition.AiAvoidanceRoundWalkChanceIntelligenceScale;
        }
        var roundWalkChance = Math.Min(
            100,
            roundWalkChanceIntelligenceScale * (monster.Intelligence + roundWalkChanceBase));
        if (actionRoll >= roundWalkChance)
            return true;

        var roundWalk = FindNativeStatefulRoundWalkStepFromRandom(
            monster,
            player,
            players,
            monster.AvoidanceMovementTurn,
            out var nextTurn);
        monster.UpdateAvoidanceMovementTurn(nextTurn);
        if (roundWalk.HasValue) {
            monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                roundWalk.Value.X - monster.PositionX,
                roundWalk.Value.Y - monster.PositionY);
        } else {
            monster.StartAvoidanceDelay(ResolveAvoidanceDelayFromRandom(monster));
        }
        return true;
    }

    /** Native AiAvoidance ordinary movement admission across var2 bands. */
    private static int GetAiAvoidanceMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.AvoidanceMovementIdleTicks == -1
            ? longIdleChance
            : monster.AvoidanceMovementIdleTicks > 20
                ? longIdleChance
                : monster.AvoidanceMovementIdleTicks == 0
                    && monster.AvoidanceMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native AiAvoidance fresh-mode bonus with a compatibility fallback for older packs. */
    private int GetAiAvoidanceMovementFreshChanceBonus(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AiAvoidanceMovementFreshChanceBonus > 0)
            return definition.AiAvoidanceMovementFreshChanceBonus;
        return 50;
    }

    private bool IsNativeAvoidanceDirectionAvailable(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId);
    }

    /** Native SnakeAi's six-step lateral drift and three-candidate RandomWalk2 fallback. */
    private (int X, int Y)? FindSnakeDriftStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        IReadOnlyList<int>? configuredPattern)
    {
        var targetDirection = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var pattern = configuredPattern is { Count: > 0 }
            ? configuredPattern
            : SnakeDriftPattern;
        var patternIndex = (int)((seed / 100U) % (uint)pattern.Count);
        var direction = RotateDirection(
            targetDirection,
            pattern[patternIndex]);
        var driftDisplacement = DirectionDisplacements[direction];
        var driftX = checked(monster.PositionX + driftDisplacement.X);
        var driftY = checked(monster.PositionY + driftDisplacement.Y);
        if (world.IsWalkable(monster.LevelId, driftX, driftY)
            && !world.IsOccupied(
                monster.LevelId,
                driftX,
                driftY,
                players,
                monster.EntityId))
            return (driftX, driftY);

        // Native RandomWalk2 starts from monster.direction (the unmodified
        // target direction) and only considers straight, left, and right.
        var fallbackTurn = (seed / 600U) % 2U == 0 ? 1 : -1;
        var targetDirections = DeterministicGridDirections.EnumerateStraightAndLateral(
            targetDirection,
            fallbackTurn);
        foreach (var candidateDirection in targetDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native Snake RandomWalk2 fallback driven by the restored stream. */
    private (int X, int Y)? FindSnakeDriftStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        IReadOnlyList<int>? configuredPattern)
    {
        var targetDirection = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var pattern = configuredPattern is { Count: > 0 }
            ? configuredPattern
            : SnakeDriftPattern;
        var patternIndex = (int)((seed / 100U) % (uint)pattern.Count);
        var direction = RotateDirection(
            targetDirection,
            pattern[patternIndex]);
        var drift = FindStepInDirection(monster, direction, players);
        if (drift.HasValue)
            return drift;

        // Native RandomWalk2 consumes one coin to order the two lateral
        // candidates after its straight candidate is unavailable.
        var fallbackTurn = random.FlipCoin(2) ? 1 : -1;
        foreach (var candidateDirection in DeterministicGridDirections.EnumerateStraightAndLateral(
                     targetDirection,
                     fallbackTurn)) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native Familiar uses Bat movement at range and emits Lightning after melee. */
    private bool TryAdvanceFamiliarBatAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || !IsFamiliarLightningMissile(missile)
            || distance <= 1)
            return false;

        return TryAdvanceBatMovementFallback(
            monster,
            player,
            players,
            unchecked((uint)tick ^ monster.EntityId ^ player.EntityId),
            distance);
    }

    private static bool IsFamiliarLightningMissile(
        AuthoritativeMonsterMissileDefinition missile)
        => missile.IsPostMeleeMissile
            || missile.MonsterId != 0
            && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic
            && missile.MissileId == "Lightning";

    /** Captures Familiar's validated GenerateRnd damage bound before travel. */
    private void CreateFamiliarLightningProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        AuthoritativeMonsterMissileDefinition missile)
    {
        var speed = missile.ProjectileSpeed > 0 ? missile.ProjectileSpeed : 16;
        var lifetime = missile.ProjectileLifetime > 0 ? missile.ProjectileLifetime : 2;
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
        var damageRandomRange = missile.ProjectileDamageRandomRange > 0
            ? missile.ProjectileDamageRandomRange
            : 10;
        var damage = missile.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : random.RandomIntBetween(1, damageRandomRange);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            missile.SpellId != 0 ? missile.SpellId : 3,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            missile.DamageType ?? AuthoritativeDamageType.Lightning,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.MonsterMagic,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
    }

    /** Native Bat-family charge admitted by a monster-specific missile row. */
    private bool TryAdvanceMappedChargeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)) {
            if (distance < 2 || !UsesNativeAi(monster, "Bat"))
                return false;
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                unchecked((uint)tick ^ monster.EntityId ^ player.EntityId),
                distance);
        }
        if (missile.ProjectileBehavior != AuthoritativeProjectileBehavior.BatCharge)
            return false;

        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.BatCharge))
            return true;

        // Adjacent Bat attacks use the shared melee admission path below; the
        // mapped charge controller owns only ranged ticks.
        if (distance < 2)
            return false;

        var chargeTuning = missile.ChargeMinimumDistance > 0
            && missile.ChargeChanceBase > 0
            && missile.ChargeChanceIntelligenceScale > 0
            ? (
                MinimumDistance: missile.ChargeMinimumDistance,
                ChanceBase: missile.ChargeChanceBase,
                IntelligenceScale: missile.ChargeChanceIntelligenceScale)
            : (MinimumDistance: 5, ChanceBase: 33, IntelligenceScale: 4);
        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (distance < chargeTuning.MinimumDistance)
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                actionRoll);

        var chargeChance = Math.Min(
            100,
            chargeTuning.ChanceBase
                + (chargeTuning.IntelligenceScale * monster.Intelligence));
        if (actionRoll >= chargeChance
            || !world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                actionRoll);
        }

        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var chargeProjectileTuning = ResolveMappedChargeProjectileTuning(
            missile,
            monster.Intelligence);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            chargeProjectileTuning.Speed);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            missile.SpellId != 0 ? missile.SpellId : 1,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            0,
            missile.DamageType ?? AuthoritativeDamageType.Physical,
            0,
            checked((uint)chargeProjectileTuning.Lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.BatCharge,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = chargeProjectileTuning.Speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        return true;
    }

    /** Native BatAi movement after its Gloom charge branch does not fire. */
    private bool TryAdvanceBatMovementFallback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        int? actionRoll = null)
    {
        if (distance < 2)
            return false;

        // BatAi uses the native var1/var2 movement state: after a long idle
        // animation it admits intelligence + 13, while a fresh movement mode
        // with var2 == 0 admits intelligence + 63. Legacy hand-built targets
        // without that state retain the earlier standing-tick boundary once.
        var movementTuning = GetBatMovementTuning(monster);
        var movementChance = monster.BatMovementIdleTicks == -1
            ? movementTuning.ChanceBase
                + movementTuning.IntelligenceScale * monster.Intelligence
                + movementTuning.FreshChanceBonus
            : monster.BatMovementIdleTicks > 20
                ? movementTuning.ChanceBase
                    + movementTuning.IntelligenceScale * monster.Intelligence
                : monster.BatMovementIdleTicks == 0
                    && monster.BatMovementMode is >= 1 and <= 3
                    ? movementTuning.ChanceBase
                        + movementTuning.IntelligenceScale * monster.Intelligence
                        + movementTuning.FreshChanceBonus
                    : 0;
        movementChance = Math.Min(100, movementChance);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var movementRoll = usesNativeAiStream
            ? actionRoll ?? random.RandomIntLessThan(100)
            : (int)(seed % 100U);
        if (movementRoll >= movementChance) {
            monster.AdvanceBatMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, seed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordBatMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
        } else {
            monster.AdvanceBatMovementIdle();
        }
        return true;
    }

    private (int ChanceBase, int IntelligenceScale, int FreshChanceBonus)
        GetBatMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.BatMovementChanceBase > 0
            && definition.BatMovementChanceIntelligenceScale > 0
            && definition.BatMovementFreshChanceBonus > 0) {
            return (
                definition.BatMovementChanceBase,
                definition.BatMovementChanceIntelligenceScale,
                definition.BatMovementFreshChanceBonus);
        }
        return (13, 1, 50);
    }

    private (int X, int Y)? FindGoatRangedRetreatDestination(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int currentDistance)
    {
        var candidates = Enumerable.Range(-1, 3)
            .SelectMany(offsetY => Enumerable.Range(-1, 3)
                .Select(offsetX => (
                    X: monster.PositionX + offsetX,
                    Y: monster.PositionY + offsetY)))
            .Where(candidate => candidate.X != monster.PositionX
                || candidate.Y != monster.PositionY)
            .Where(candidate => world.IsWalkable(
                monster.LevelId,
                candidate.X,
                candidate.Y))
            .Where(candidate => !world.IsOccupied(
                monster.LevelId,
                candidate.X,
                candidate.Y,
                players,
                monster.EntityId))
            .Where(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y) >= currentDistance)
            .OrderByDescending(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[0];
    }

    /** Native AiRanged projectile dispatch for validated ranged missile mappings. */
    private bool TryAdvanceMappedRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.MonsterMagic
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.Acid))
            return false;
        if (IsFamiliarLightningMissile(missile))
            return false;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var usesNativeAiStream = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega")
            && monster.UsesNativeRandomSeeds;
        var usesNativeRangedAvoidanceAiStream = missile.AttackMode
                == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
            && monster.UsesNativeRandomSeeds;
        // Zhar and Lazarus use the native Counselor controller even though
        // their runtime identities are distinct aliases. Keep the mapped
        // projectile's delay/RNG branch aligned with that controller.
        var isCounselor = UsesNativeCounselorAi(monster);
        var usesNativeRangedAiStream = missile.AttackMode
                == AuthoritativeMonsterRangedAttackMode.Ranged
            && !isCounselor
            && monster.UsesNativeRandomSeeds;
        var usesNativeCounselorAiStream = isCounselor
            && monster.UsesNativeRandomSeeds;
        if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega")
            && monster.AdvanceMegaDelay())
            return true;
        if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController
            && !isCounselor
            && monster.AdvanceRangedDelay())
            return true;
        if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
            && monster.AdvanceRangedAvoidanceDelay())
            return true;
        var nativeCatacombsRangedController = IsNativeCatacombsRangedController(monster, missile);
        if (world.Projectiles.Values.Any(projectile =>
            {
                if (projectile.SourceEntityId != monster.EntityId)
                    return false;
                if (projectile.Behavior == missile.ProjectileBehavior)
                    return true;
                if (missile.ProjectileBehavior
                    == AuthoritativeProjectileBehavior.ApocalypseController
                    && projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
                    return true;
                return missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                    && (projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle);
            }) && !nativeCatacombsRangedController)
            return true;
        var rangedAvoidanceUsesRoundWalk = false;
        var rangedAvoidanceRoundWalkTurn = -1;
        if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController) {
            var infernoTuning = ResolveMonsterInfernoTuning(missile);
            if (distance >= infernoTuning.MaximumDistance)
                return false;
            var infernoActionRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)(attackSeed % 100U);
            if (distance <= 1) {
                var adjacentChance = checked(
                    infernoTuning.AdjacentChanceBase
                    + infernoTuning.AdjacentChanceIntelligenceScale
                        * monster.Intelligence);
                if (infernoActionRoll >= Math.Min(100, adjacentChance)
                    || (usesNativeAiStream
                        ? !random.FlipCoin((uint)infernoTuning.AdjacentChoiceModulo)
                        : (attackSeed / 100U)
                            % (uint)infernoTuning.AdjacentChoiceModulo != 0))
                    return false;
            } else {
                var attackBand = checked(
                    infernoTuning.RangedChanceBase
                    + infernoTuning.RangedChanceIntelligenceScale
                        * (monster.Intelligence
                            + (distance >= infernoTuning.RangedFarDistance
                                ? infernoTuning.RangedFarIntelligenceOffset
                                : 0)));
                if (infernoActionRoll >= Math.Min(100, attackBand)) {
                    // MegaAi's normal branch may RandomWalk after a missed
                    // Inferno roll. Consume that branch here so the target
                    // does not fall through to generic pursuit.
                    if (UsesNativeAi(monster, "Mega")) {
                        TryAdvanceMegaIntermediateMovement(
                            monster,
                            player,
                            players,
                            attackSeed,
                            distance,
                            usesNativeAiStream);
                        monster.StartMegaDelay(
                            usesNativeAiStream
                                ? ResolveMegaDelayFromRandom(missile)
                                : ResolveMegaDelay(missile, attackSeed));
                        return true;
                    }
                    return false;
                }
            }
        } else if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance) {
            // Legacy rows retain the native AiRangedAvoidance values while
            // validated content progressively owns this admission boundary.
            var minimumDistance = missile.RangedAvoidanceMinimumDistance == 0
                ? 2
                : missile.RangedAvoidanceMinimumDistance;
            var farDistance = missile.RangedAvoidanceFarDistance == 0
                ? 3
                : missile.RangedAvoidanceFarDistance;
            var chanceBase = missile.RangedAvoidanceChanceBase == 0
                ? 500
                : missile.RangedAvoidanceChanceBase;
            var chanceIntelligenceScale = missile.RangedAvoidanceChanceIntelligenceScale == 0
                ? 500
                : missile.RangedAvoidanceChanceIntelligenceScale;
            var farIntelligenceOffset = missile.RangedAvoidanceFarIntelligenceOffset;
            if (missile.RangedAvoidanceMinimumDistance == 0
                && missile.RangedAvoidanceFarDistance == 0
                && missile.RangedAvoidanceChanceBase == 0
                && missile.RangedAvoidanceChanceIntelligenceScale == 0
                && missile.RangedAvoidanceFarIntelligenceOffset == 0)
                farIntelligenceOffset = 1;
            var rangedAvoidanceActionRoll = usesNativeRangedAvoidanceAiStream
                ? random.RandomIntLessThan(10_000)
                : (int)(attackSeed % 10_000U);
            if (distance < minimumDistance) {
                monster.ClearRangedAvoidanceMovement();
                // Native AiRangedAvoidance falls back to a probabilistic
                // adjacent melee attack instead of admitting every shared
                // melee tick. Keep failed native rolls out of that fallback;
                // successful rolls continue into the authoritative melee
                // resolver below.
                if (distance <= 1) {
                    // Validated rows own the divisor; omitted rows retain the
                    // native Acid half-band compatibility behavior.
                    var adjacentAttackChanceDivisor = missile
                        .RangedAvoidanceAdjacentAttackChanceDivisor > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceDivisor
                        : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                            ? 2
                            : 1;
                    var adjacentAttackChanceBase = missile
                        .RangedAvoidanceAdjacentAttackChanceBase > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceBase
                        : 6_000;
                    var adjacentAttackChanceIntelligenceScale = missile
                        .RangedAvoidanceAdjacentAttackChanceIntelligenceScale > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceIntelligenceScale
                        : 1_000;
                    var adjacentAttackBand = checked(
                        adjacentAttackChanceBase
                        + adjacentAttackChanceIntelligenceScale * monster.Intelligence);
                    adjacentAttackBand /= adjacentAttackChanceDivisor;
                    if (rangedAvoidanceActionRoll
                        >= (uint)Math.Min(10_000, adjacentAttackBand))
                        return true;
                } else {
                    var moved = TryAdvanceRangedAvoidanceMovement(
                        monster,
                        player,
                        players,
                        attackSeed,
                        missile,
                        useRoundWalk: false,
                        usesNativeAiStream: usesNativeRangedAvoidanceAiStream);
                    if (!moved)
                        monster.StartRangedAvoidanceDelay(
                            usesNativeRangedAvoidanceAiStream
                                ? ResolveRangedAvoidanceDelayFromRandom(missile)
                                : ResolveRangedAvoidanceDelay(missile, attackSeed));
                    return true;
                }
                return false;
            }
            // Native AiRangedAvoidance enters its far-distance Move goal only
            // on the configured far boundary and a one-in-four (one-in-eight
            // Acid) admission. Once entered, goalVar1/goalVar2 persist across
            // ticks and saves until the native movement window completes.
            var lessMissiles = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.Acid ? 1 : 0;
            var movementChanceModulo = missile.RangedAvoidanceMovementChanceModulo == 0
                ? 4 << lessMissiles
                : missile.RangedAvoidanceMovementChanceModulo;
            var hasMoveGoal = monster.RangedAvoidanceMoveTicks >= 0;
            var startsMoveGoal = distance >= farDistance
                && (usesNativeRangedAvoidanceAiStream
                    ? random.FlipCoin((uint)movementChanceModulo)
                    : attackSeed % (uint)movementChanceModulo == 0);
            if (hasMoveGoal || startsMoveGoal) {
                if (!hasMoveGoal)
                    monster.StartRangedAvoidanceMovement(
                        usesNativeRangedAvoidanceAiStream
                            ? random.RandomIntLessThan(2)
                            : (int)((attackSeed / 100U) % 2U));
                if (monster.AdvanceRangedAvoidanceMovement(
                        distance,
                        IsNativeRangedAvoidanceDirectionAvailable(
                            monster,
                            player,
                            players))) {
                    rangedAvoidanceUsesRoundWalk = true;
                    rangedAvoidanceRoundWalkTurn = monster.RangedAvoidanceTurn;
                }
            }
            var attackBand = checked(
                chanceBase + chanceIntelligenceScale * (monster.Intelligence
                    + (rangedAvoidanceUsesRoundWalk
                        ? 0
                        : distance >= farDistance
                            ? farIntelligenceOffset
                            : 0)));
            attackBand = checked(
                attackBand * missile.AvoidanceProbabilityPercent / 100);
            if (rangedAvoidanceActionRoll
                >= (uint)Math.Min(10_000, attackBand)) {
                var moved = TryAdvanceRangedAvoidanceMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    missile,
                    rangedAvoidanceUsesRoundWalk,
                    rangedAvoidanceRoundWalkTurn,
                    usesNativeRangedAvoidanceAiStream);
                if (!moved)
                    monster.StartRangedAvoidanceDelay(
                        usesNativeRangedAvoidanceAiStream
                            ? ResolveRangedAvoidanceDelayFromRandom(missile)
                            : ResolveRangedAvoidanceDelay(missile, attackSeed));
                return true;
            }
        } else {
            var retreatTuning = ResolveRangedRetreatTuning(missile);
            var retreatChance = checked(
                retreatTuning.ChanceBase
                + retreatTuning.IntelligenceScale * monster.Intelligence);
            if (distance < retreatTuning.MinimumDistance) {
                var retreatRoll = usesNativeRangedAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (retreatRoll < Math.Min(100, retreatChance)) {
                    var retreat = usesNativeRangedAiStream
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
                        : FindGoatRangedRetreatDestination(
                            monster,
                            player,
                            players,
                            distance);
                    if (retreat.HasValue) {
                        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                        return true;
                    }
                }
            }
        }

        if (!world.HasMissileLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance) {
                var moved = TryAdvanceRangedAvoidanceMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    missile,
                    rangedAvoidanceUsesRoundWalk,
                    rangedAvoidanceRoundWalkTurn,
                    usesNativeRangedAvoidanceAiStream);
                if (!moved)
                    monster.StartRangedAvoidanceDelay(
                        usesNativeRangedAvoidanceAiStream
                            ? ResolveRangedAvoidanceDelayFromRandom(missile)
                            : ResolveRangedAvoidanceDelay(missile, attackSeed));
                return true;
            }
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
                && UsesNativeAi(monster, "Mega")) {
                TryAdvanceMegaIntermediateMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    distance,
                    usesNativeAiStream);
                monster.StartMegaDelay(
                    usesNativeAiStream
                        ? ResolveMegaDelayFromRandom(missile)
                        : ResolveMegaDelay(missile, attackSeed));
                return true;
            }
            return missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController;
        }

        if (TryStartNativeCatacombsRangedAction(monster, player, missile, attackSeed))
            return true;

        var spell = spells.Definitions.FirstOrDefault(definition =>
            definition.ProjectileBehavior == missile.ProjectileBehavior
                || missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterChargedBolt
                    && definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt);
        var spellId = missile.SpellId != 0
            ? missile.SpellId
            : spell?.SpellId
            ?? (missile.ProjectileBehavior switch {
                AuthoritativeProjectileBehavior.Fireball => 12U,
                AuthoritativeProjectileBehavior.Lightning => 3U,
                AuthoritativeProjectileBehavior.ApocalypseController => 25U,
                _ => 1U,
            });
        // Native monster Firebolt/Fireball use speed 16; Storm's
        // ThinLightningControl and Mega's InfernoControl use speed 32.
        // Player spell speed remains content-owned separately in spells.tsv.
        var speed = missile.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : missile.ProjectileBehavior switch {
                AuthoritativeProjectileBehavior.MonsterChargedBolt => 8,
                AuthoritativeProjectileBehavior.Lightning => 32,
                AuthoritativeProjectileBehavior.InfernoController => spell?.ProjectileSpeed > 0
                    ? spell.ProjectileSpeed
                    : 32,
                AuthoritativeProjectileBehavior.ApocalypseController => 0,
                _ => 16,
            };
        var lifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : missile.ProjectileLifetimeBase > 0
                || missile.ProjectileLifetimeIntelligenceScale > 0
                ? checked(
                    missile.ProjectileLifetimeBase
                    + missile.ProjectileLifetimeIntelligenceScale * monster.Intelligence)
                : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                    ? checked(5 * (monster.Intelligence + 4))
                    : spell?.ProjectileLifetime > 0 ? spell.ProjectileLifetime : 256;
        var impactLifetime = ResolveMonsterProjectileImpactLifetime(missile);
        var damageType = missile.DamageType
            ?? spell?.DamageType
            ?? (missile.MissileId == "MagmaBall"
                ? AuthoritativeDamageType.Fire
                : missile.MissileId == "Acid"
                    ? AuthoritativeDamageType.Acid
                    : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic
                        ? AuthoritativeDamageType.Magic
                        : AuthoritativeDamageType.Fire);
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
        var projectileCount = missile.ProjectileBehavior
            == AuthoritativeProjectileBehavior.MonsterChargedBolt
            ? missile.ProjectileCount > 0 ? missile.ProjectileCount : 3
            : 1;
        var projectileDamage = missile.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : missile.ProjectileBehavior switch {
                // Native AddChargedBolt assigns monster-fired bolts a fixed
                // 15-point payload; validated content may override it.
                AuthoritativeProjectileBehavior.MonsterChargedBolt => 15,
                AuthoritativeProjectileBehavior.Lightning => 0,
                // Native AddFirebolt and AddGenericMagicMissile roll the
                // monster range while constructing the missile, rather than
                // deriving the payload from the AI admission seed.
                AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.MonsterMagic
                    => random.RandomIntBetween(
                        GetEffectiveMonsterDamageRange(monster, special: false).Minimum,
                        GetEffectiveMonsterDamageRange(monster, special: false).Maximum),
                _ => SelectEffectiveMonsterDamage(
                    monster,
                    special: false,
                    seed: attackSeed),
            };
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++) {
            var projectile = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.InfernoController
                ? CreateMonsterInfernoController(
                    AllocateProjectileEntityId(),
                    monster,
                    player,
                    spellId,
                    speed,
                    lifetime,
                    impactLifetime,
                    damageType,
                    missile.ProjectileDamage)
                : new AuthoritativeProjectile(
                    AllocateProjectileEntityId(reuseReleasedId: true),
                    monster.EntityId,
                    player.EntityId,
                    spellId,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY,
                    projectileDamage,
                    damageType,
                    0,
                    checked((uint)lifetime)) {
                    Behavior = missile.ProjectileBehavior,
                    NativeMissileId = missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic ? missile.MissileId : null,
                    VelocityX = velocityX,
                    VelocityY = velocityY,
                    OriginalDirection = direction,
                    Speed = speed,
                    ImpactLifetime = impactLifetime,
                    ResistantDamageDivisor = 1,
                };
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterChargedBolt) {
                var pathRandomRange = spell?.ChargedBoltPathRandomRange > 0
                    ? spell.ChargedBoltPathRandomRange
                    : 15;
                projectile.PathIndex = random.GenerateRnd(pathRandomRange) + 1;
                // Native AddChargedBolt also rolls a presentation frame.
                _ = random.RandomIntBetween(1, 8);
            }
            var initialTravelQuanta = missile.InitialTravelQuanta > 0
                ? missile.InitialTravelQuanta
                : missile.MissileId == "MagmaBall" ? 3 : 0;
            if (initialTravelQuanta > 0) {
                // Older mappings retain the native MagmaBall compatibility fallback;
                // shipped rows declare the same advance through content.
                projectile.TraveledX = checked(projectile.VelocityX * initialTravelQuanta);
                projectile.TraveledY = checked(projectile.VelocityY * initialTravelQuanta);
                var initialTile = CalculateMissileTile(
                    projectile.StartX,
                    projectile.StartY,
                    projectile.TraveledX,
                    projectile.TraveledY);
                projectile.PositionX = initialTile.X;
                projectile.PositionY = initialTile.Y;
            }
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController) {
                projectile.VelocityX = velocityX;
                projectile.VelocityY = velocityY;
            }
            world.Projectiles.Add(projectile.EntityId, projectile);
        }
        if (isCounselor)
            monster.StartCounselorDelay(
                usesNativeCounselorAiStream
                    ? ResolveCounselorAttackDelayFromRandom(monster)
                    : ResolveCounselorAttackDelay(monster, attackSeed));
        else if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController)
            monster.StartRangedDelay(
                usesNativeRangedAiStream
                    ? ResolveRangedDelayFromRandom(missile)
                    : ResolveRangedDelay(missile, attackSeed));
        else if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega"))
            monster.StartMegaDelay(
                usesNativeAiStream
                    ? ResolveMegaDelayFromRandom(missile)
                    : ResolveMegaDelay(missile, attackSeed));
        return true;
    }

    private static (int MinimumDistance, int ChanceBase, int IntelligenceScale)
        ResolveRangedRetreatTuning(AuthoritativeMonsterMissileDefinition? missile)
    {
        // Legacy rows retain the native four-tile and 70 + 10 * intelligence
        // values while validated content progressively owns this boundary.
        return (
            missile?.RangedRetreatMinimumDistance > 0
                ? missile.RangedRetreatMinimumDistance
                : 4,
            missile?.RangedRetreatChanceBase > 0
                ? missile.RangedRetreatChanceBase
                : 70,
            missile?.RangedRetreatChanceIntelligenceScale > 0
                ? missile.RangedRetreatChanceIntelligenceScale
                : 10);
    }

    private static int ResolveRangedDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.RangedDelayRandomRange > 0
            ? missile.RangedDelayRandomRange
            : 20;
        var delayBase = missile?.RangedDelayBase ?? 0;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native AiRanged post-shot delay driven by the restored per-monster stream. */
    private int ResolveRangedDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.RangedDelayRandomRange > 0
            ? missile.RangedDelayRandomRange
            : 20;
        var delayBase = missile?.RangedDelayBase ?? 0;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveRangedAvoidanceDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.RangedAvoidanceDelayRandomRange > 0
            ? missile.RangedAvoidanceDelayRandomRange
            : 10;
        var delayBase = missile?.RangedAvoidanceDelayBase > 0
            ? missile.RangedAvoidanceDelayBase
            : 5;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native AiRangedAvoidance recovery delay driven by the restored stream. */
    private int ResolveRangedAvoidanceDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.RangedAvoidanceDelayRandomRange > 0
            ? missile.RangedAvoidanceDelayRandomRange
            : 10;
        var delayBase = missile?.RangedAvoidanceDelayBase > 0
            ? missile.RangedAvoidanceDelayBase
            : 5;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveHorkDemonDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint actionSeed)
    {
        var randomRange = missile?.HorkDemonDelayRandomRange > 0
            ? missile.HorkDemonDelayRandomRange
            : 10;
        var delayBase = missile?.HorkDemonDelayBase > 0
            ? missile.HorkDemonDelayBase
            : 10;
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    /** Native HorkDemon recovery delay driven by the restored per-monster stream. */
    private int ResolveHorkDemonDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.HorkDemonDelayRandomRange > 0
            ? missile.HorkDemonDelayRandomRange
            : 10;
        var delayBase = missile?.HorkDemonDelayBase > 0
            ? missile.HorkDemonDelayBase
            : 10;
        // GenerateRnd preserves native signed remainder semantics. In
        // particular, an INT_MIN stream value produces -8 for range 10;
        // clamping through RandomIntLessThan would silently change AiDelay.
        return checked(delayBase + random.GenerateRnd(randomRange));
    }

    private static int ResolveSnakeDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint actionSeed,
        int intelligence,
        bool adjacent)
    {
        var baseDelay = adjacent
            ? missile?.SnakeAdjacentDelayBase > 0
                ? missile.SnakeAdjacentDelayBase
                : 10
            : missile?.SnakeDistantDelayBase > 0
                ? missile.SnakeDistantDelayBase
                : 15;
        var intelligenceScale = adjacent
            ? missile?.SnakeAdjacentDelayIntelligenceScale > 0
                ? missile.SnakeAdjacentDelayIntelligenceScale
                : 1
            : missile?.SnakeDistantDelayIntelligenceScale > 0
                ? missile.SnakeDistantDelayIntelligenceScale
                : 1;
        var randomRange = adjacent
            ? missile?.SnakeAdjacentDelayRandomRange > 0
                ? missile.SnakeAdjacentDelayRandomRange
                : 10
            : missile?.SnakeDistantDelayRandomRange > 0
                ? missile.SnakeDistantDelayRandomRange
                : 10;
        return Math.Max(
            0,
            checked(baseDelay - intelligenceScale * intelligence
                + (int)((actionSeed / 100U) % (uint)randomRange)));
    }

    /** Native Snake delay roll driven by the restored per-monster stream. */
    private int ResolveSnakeDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile,
        int intelligence,
        bool adjacent)
    {
        var baseDelay = adjacent
            ? missile?.SnakeAdjacentDelayBase > 0
                ? missile.SnakeAdjacentDelayBase
                : 10
            : missile?.SnakeDistantDelayBase > 0
                ? missile.SnakeDistantDelayBase
                : 15;
        var intelligenceScale = adjacent
            ? missile?.SnakeAdjacentDelayIntelligenceScale > 0
                ? missile.SnakeAdjacentDelayIntelligenceScale
                : 1
            : missile?.SnakeDistantDelayIntelligenceScale > 0
                ? missile.SnakeDistantDelayIntelligenceScale
                : 1;
        var randomRange = adjacent
            ? missile?.SnakeAdjacentDelayRandomRange > 0
                ? missile.SnakeAdjacentDelayRandomRange
                : 10
            : missile?.SnakeDistantDelayRandomRange > 0
                ? missile.SnakeDistantDelayRandomRange
                : 10;
        return Math.Max(
            0,
            checked(baseDelay - intelligenceScale * intelligence
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveAvoidanceDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AvoidanceDelayRandomRange > 0) {
            delayBase = definition.AvoidanceDelayBase > 0
                ? definition.AvoidanceDelayBase
                : delayBase;
            randomRange = definition.AvoidanceDelayRandomRange;
        }
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    private int ResolveAvoidanceDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AvoidanceDelayRandomRange > 0) {
            delayBase = definition.AvoidanceDelayBase > 0
                ? definition.AvoidanceDelayBase
                : delayBase;
            randomRange = definition.AvoidanceDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private int ResolveCounselorDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorDelayRandomRange > 0) {
            delayBase = definition.CounselorDelayBase > 0
                ? definition.CounselorDelayBase
                : delayBase;
            intelligenceScale = definition.CounselorDelayIntelligenceScale > 0
                ? definition.CounselorDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.CounselorDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((actionSeed / 200U) % (uint)randomRange)));
    }

    /** Native Counselor no-action delay driven by the restored stream. */
    private int ResolveCounselorDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorDelayRandomRange > 0) {
            delayBase = definition.CounselorDelayBase > 0
                ? definition.CounselorDelayBase
                : delayBase;
            intelligenceScale = definition.CounselorDelayIntelligenceScale > 0
                ? definition.CounselorDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.CounselorDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveCounselorAttackDelay(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var delayBase = 5;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorAttackDelayRandomRange > 0) {
            delayBase = definition.CounselorAttackDelayBase > 0
                ? definition.CounselorAttackDelayBase
                : delayBase;
            randomRange = definition.CounselorAttackDelayRandomRange;
        }
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native Counselor attack delay driven by the restored stream. */
    private int ResolveCounselorAttackDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 5;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorAttackDelayRandomRange > 0) {
            delayBase = definition.CounselorAttackDelayBase > 0
                ? definition.CounselorAttackDelayBase
                : delayBase;
            randomRange = definition.CounselorAttackDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveMegaDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.MegaDelayRandomRange > 0
            ? missile.MegaDelayRandomRange
            : 10;
        var delayBase = missile?.MegaDelayBase > 0
            ? missile.MegaDelayBase
            : 5;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native Mega recovery delay driven by the restored per-monster stream. */
    private int ResolveMegaDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.MegaDelayRandomRange > 0
            ? missile.MegaDelayRandomRange
            : 10;
        var delayBase = missile?.MegaDelayBase > 0
            ? missile.MegaDelayBase
            : 5;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static (int Speed, int Lifetime) ResolveMappedChargeProjectileTuning(
        AuthoritativeMonsterMissileDefinition missile,
        int intelligence)
    {
        var speed = missile.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : 18;
        var lifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : missile.ProjectileLifetimeBase > 0
                || missile.ProjectileLifetimeIntelligenceScale > 0
                ? checked(
                    missile.ProjectileLifetimeBase
                    + missile.ProjectileLifetimeIntelligenceScale * intelligence)
                : 256;
        return (speed, Math.Max(1, lifetime));
    }

    private static int ResolveMonsterProjectileImpactLifetime(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        return missile?.ProjectileImpactLifetime > 0
            ? missile.ProjectileImpactLifetime
            : 1;
    }

    /** Native AiRangedAvoidance round-walk fallback for a failed ranged roll. */
    private bool TryAdvanceRangedAvoidanceMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        AuthoritativeMonsterMissileDefinition missile,
        bool useRoundWalk,
        int roundWalkTurn = -1,
        bool usesNativeAiStream = false)
    {
        if (!useRoundWalk) {
            var movementChanceBase = missile.RangedAvoidanceMovementChanceBase == 0
                ? 50
                : missile.RangedAvoidanceMovementChanceBase;
            var movementChanceIntelligenceScale =
                missile.RangedAvoidanceMovementChanceIntelligenceScale == 0
                    ? 10
                    : missile.RangedAvoidanceMovementChanceIntelligenceScale;
            var movementFreshChanceBonus = missile.RangedAvoidanceMovementFreshChanceBonus == 0
                ? 30
                : missile.RangedAvoidanceMovementFreshChanceBonus;
            var movementChance = GetRangedAvoidanceMovementChance(
                monster,
                movementChanceBase,
                movementChanceIntelligenceScale,
                movementFreshChanceBonus);
            var movementRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)((seed / 100U) % 100U);
            if (movementRoll >= (uint)movementChance) {
                monster.AdvanceRangedAvoidanceMovementIdle();
                return false;
            }
        }

        (int X, int Y)? movement;
        if (useRoundWalk && roundWalkTurn is 0 or 1) {
            movement = usesNativeAiStream
                ? FindNativeStatefulRoundWalkStepFromRandom(
                    monster,
                    player,
                    players,
                    roundWalkTurn,
                    out var nextTurn)
                : FindNativeStatefulRoundWalkStep(
                    monster,
                    player,
                    players,
                    seed,
                    roundWalkTurn,
                    out nextTurn);
            monster.UpdateRangedAvoidanceTurn(nextTurn);
        } else {
            movement = usesNativeAiStream
                ? FindNativeRandomWalkStepFromRandom(monster, player, players)
                : FindNativeRandomWalkStep(monster, player, players, seed);
        }
        if (!movement.HasValue) {
            if (!useRoundWalk)
                monster.AdvanceRangedAvoidanceMovementIdle();
            return false;
        }

        monster.BeginMovement(movement.Value.X, movement.Value.Y);
        monster.RecordRangedAvoidanceMovement(
            movement.Value.X - monster.PositionX,
            movement.Value.Y - monster.PositionY);
        return true;
    }

    /** Native OverlordAi movement admission across var2 bands. */
    private static int GetFatMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.FatMovementIdleTicks == -1
            ? longIdleChance
            : monster.FatMovementIdleTicks > 20
                ? longIdleChance
                : monster.FatMovementIdleTicks == 0
                    && monster.FatMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native Fat fresh-mode bonus with a compatibility fallback for older packs. */
    private int GetFatMovementFreshChanceBonus(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.FatMovementFreshChanceBonus > 0)
            return definition.FatMovementFreshChanceBonus;
        return 50;
    }

    /** Native AiRangedAvoidance ordinary movement admission across var2 bands. */
    private static int GetRangedAvoidanceMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.RangedAvoidanceMovementIdleTicks == -1
            ? longIdleChance
            : monster.RangedAvoidanceMovementIdleTicks > 20
                ? longIdleChance
                : monster.RangedAvoidanceMovementIdleTicks == 0
                    && monster.RangedAvoidanceMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native RoundWalk with persisted goalVar2 mutation. */
    private (int X, int Y)? FindNativeStatefulRoundWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int turnState,
        out int nextTurnState)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnState == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId)) {
                nextTurnState = turnState;
                return (candidateX, candidateY);
            }
        }

        // Native RoundWalk flips goalVar2 before calling RandomWalk in the
        // opposite direction. Keep the deterministic fallback order shared
        // with the existing RoundWalk approximation.
        nextTurnState = turnState == 0 ? 1 : 0;
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var fallbackTurn = (seed / 200U) % 2U == 0U ? 1 : -1;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            fallbackTurn,
            2);
        foreach (var candidateDirection in fallbackDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native RoundWalk fallback with restored RandomWalk coin ordering. */
    private (int X, int Y)? FindNativeStatefulRoundWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int turnState,
        out int nextTurnState)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnState == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId)) {
                nextTurnState = turnState;
                return (candidateX, candidateY);
            }
        }

        nextTurnState = turnState == 0 ? 1 : 0;
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in fallbackDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    private bool IsNativeRangedAvoidanceDirectionAvailable(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId);
    }

    private static (
        int MaximumDistance,
        int AdjacentChanceBase,
        int AdjacentChanceIntelligenceScale,
        int RangedFarDistance,
        int RangedChanceBase,
        int RangedChanceIntelligenceScale,
        int RangedFarIntelligenceOffset,
        int AdjacentChoiceModulo) ResolveMonsterInfernoTuning(
        AuthoritativeMonsterMissileDefinition missile)
    {
        var legacyDefaults = missile.InfernoMaximumDistance == 0
            && missile.InfernoAdjacentChanceBase == 0
            && missile.InfernoAdjacentChanceIntelligenceScale == 0
            && missile.InfernoRangedFarDistance == 0
            && missile.InfernoRangedChanceBase == 0
            && missile.InfernoRangedChanceIntelligenceScale == 0
            && missile.InfernoRangedFarIntelligenceOffset == 0
            && missile.InfernoAdjacentChoiceModulo == 0;
        return (
            missile.InfernoMaximumDistance > 0
                ? missile.InfernoMaximumDistance
                : 5,
            missile.InfernoAdjacentChanceBase > 0
                ? missile.InfernoAdjacentChanceBase
                : 40,
            missile.InfernoAdjacentChanceIntelligenceScale > 0
                ? missile.InfernoAdjacentChanceIntelligenceScale
                : 10,
            missile.InfernoRangedFarDistance > 0
                ? missile.InfernoRangedFarDistance
                : 3,
            missile.InfernoRangedChanceBase > 0
                ? missile.InfernoRangedChanceBase
                : 5,
            missile.InfernoRangedChanceIntelligenceScale > 0
                ? missile.InfernoRangedChanceIntelligenceScale
                : 5,
            legacyDefaults ? 1 : missile.InfernoRangedFarIntelligenceOffset,
            missile.InfernoAdjacentChoiceModulo > 0
                ? missile.InfernoAdjacentChoiceModulo
                : 2);
    }
}
