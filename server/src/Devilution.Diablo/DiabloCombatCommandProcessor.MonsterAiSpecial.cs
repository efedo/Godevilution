using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Counselor, scavenger, sneak, skeleton, zombie, gargoyle and snake AI (native monster.cpp). */
public sealed partial class DiabloCombatCommandProcessor
{
    /** Native Counselor wounded-adjacent retreat persists its four RandomWalk attempts. */
    private bool TryAdvanceCounselorAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!UsesNativeCounselorAi(monster))
            return false;
        if (monster.AdvanceCounselorDelay())
            return true;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        // Native CounselorAi always consumes its initial GenerateRnd(100)
        // before evaluating the current goal. Seeded targets use the restored
        // stream; zero-seed targets retain the compatibility draw.
        var actionRoll = random.GenerateRnd(100);

        if (monster.CounselorRetreatTicks != 0) {
            var retreat = usesNativeAiStream
                ? FindNativeCounselorRetreatStepFromRandom(monster, victim, players)
                : FindNativeCounselorRetreatStep(
                    monster,
                    victim,
                    players,
                    unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId));
            if (retreat.HasValue)
                monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
            monster.AdvanceCounselorRetreat();
            return true;
        }

        if (distance >= 2
            && TryGetMonsterMissile(monster, out var missile)
            && missile.ProjectileBehavior is (
                AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.MonsterMagic
                or AuthoritativeProjectileBehavior.Lightning)) {
            var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
            var attackChanceBase = monsterTypes?.TryGet(
                    monster.MonsterId,
                    out var counselorAttackDefinition) == true
                && counselorAttackDefinition.CounselorAttackChanceBase > 0
                ? counselorAttackDefinition.CounselorAttackChanceBase
                : 50;
            var attackChanceIntelligenceScale = monsterTypes?.TryGet(
                    monster.MonsterId,
                    out counselorAttackDefinition) == true
                && counselorAttackDefinition.CounselorAttackChanceIntelligenceScale > 0
                ? counselorAttackDefinition.CounselorAttackChanceIntelligenceScale
                : 5;
            var attackChance = Math.Min(
                100,
                attackChanceBase
                    + (attackChanceIntelligenceScale * monster.Intelligence));
            if ((usesNativeAiStream
                    ? actionRoll
                    : (int)(attackSeed % 100U)) >= attackChance
                || !world.HasMissileLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    victim.PositionX,
                    victim.PositionY)) {
                // Native CounselorAi delays after a missed ranged admission,
                // then uses a 30-percent Move/ RoundWalk branch instead of
                // falling through to generic pursuit.
                var movementChance = monsterTypes?.TryGet(
                        monster.MonsterId,
                        out var counselorDefinition) == true
                    && counselorDefinition.CounselorMoveChancePercent > 0
                    ? counselorDefinition.CounselorMoveChancePercent
                    : 30;
                var movementRoll = usesNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)((attackSeed / 100U) % 100U);
                if (movementRoll < movementChance) {
                    var roundWalk = FindNativeRoundWalkStep(
                        monster,
                        victim,
                        players,
                        attackSeed);
                    if (roundWalk.HasValue)
                        monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                } else {
                    monster.StartCounselorDelay(
                        usesNativeAiStream
                            ? ResolveCounselorDelayFromRandom(monster)
                            : ResolveCounselorDelay(monster, attackSeed));
                }
                return true;
            }
        }

        if (distance > 1)
            return false;

        // CounselorAi compares whole native hit points against the integer
        // half-health threshold before entering MonsterGoal::Retreat.
        if (monster.HitPoints < monster.MaxHitPoints / 2) {
            // Native goalVar1 starts at zero and admits four RandomWalk calls
            // (0, 1, 2, and 3) on subsequent active AI ticks.
            monster.StartCounselorRetreat(4);
            return true;
        }

        // Native CounselorAi uses its adjacent ranged attack to emit the two
        // stationary Flash layers; it never falls through to generic melee.
        var adjacentAttackSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var adjacentAttackChanceBase = 20;
        var adjacentAttackChanceIntelligenceScale = 2;
        if (monsterTypes?.TryGet(monster.MonsterId, out var adjacentAttackDefinition) == true
            && adjacentAttackDefinition.CounselorAdjacentAttackChanceBase > 0
            && adjacentAttackDefinition.CounselorAdjacentAttackChanceIntelligenceScale > 0) {
            adjacentAttackChanceBase = adjacentAttackDefinition.CounselorAdjacentAttackChanceBase;
            adjacentAttackChanceIntelligenceScale = adjacentAttackDefinition.CounselorAdjacentAttackChanceIntelligenceScale;
        }
        var adjacentAttackChance = Math.Min(
            100,
            adjacentAttackChanceBase
                + (adjacentAttackChanceIntelligenceScale * monster.Intelligence));
        var adjacentAttackRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(adjacentAttackSeed % 100U);
        if (adjacentAttackRoll < adjacentAttackChance) {
            CreateMonsterFlashLayers(monster);
            monster.StartCounselorDelay(
                usesNativeAiStream
                    ? ResolveCounselorAttackDelayFromRandom(monster)
                    : ResolveCounselorAttackDelay(monster, adjacentAttackSeed));
        } else {
            monster.StartCounselorDelay(
                usesNativeAiStream
                    ? ResolveCounselorDelayFromRandom(monster)
                    : ResolveCounselorDelay(monster, adjacentAttackSeed));
        }
        return true;
    }

    /** Native Counselor RandomWalk begins in the direction opposite the victim. */
    private (int X, int Y)? FindNativeCounselorRetreatStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var towardPlayer = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var awayFromPlayer = RotateDirection(towardPlayer, 4);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            awayFromPlayer,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
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

    private (
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int SpawnMinimumDistance,
        int SpawnChanceBase,
        int SpawnChanceIntelligenceScale,
        int MovementChanceBase,
        int MovementChanceIntelligenceScale)? GetHorkDemonTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.HorkDemonAttackChanceBase > 0
                && definition.HorkDemonAttackChanceIntelligenceScale > 0
                && definition.HorkDemonSpawnMinimumDistance > 0
                && definition.HorkDemonSpawnChanceBase > 0
                && definition.HorkDemonSpawnChanceIntelligenceScale > 0
                && definition.HorkDemonMovementChanceBase > 0
                && definition.HorkDemonMovementChanceIntelligenceScale > 0) {
                return (
                    definition.HorkDemonAttackChanceBase,
                    definition.HorkDemonAttackChanceIntelligenceScale,
                    definition.HorkDemonSpawnMinimumDistance,
                    definition.HorkDemonSpawnChanceBase,
                    definition.HorkDemonSpawnChanceIntelligenceScale,
                    definition.HorkDemonMovementChanceBase,
                    definition.HorkDemonMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "HorkDemon")
            ? (28, 2, 3, 43, 2, 33, 2)
            : null;
    }

    private bool TryAdvanceScavengerAi(
        AuthoritativeCombatTarget target,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var scavengerTuning = GetScavengerTuning(target);
        if (!scavengerTuning.HasValue)
            return false;
        if (target.HitPointsQ6 >= target.MaxHitPointsQ6
                * scavengerTuning.Value.HealingHealthThresholdPercent
                / 100
            && target.ScavengerHealingTicks == 0) {
            return false;
        }
        if (target.ScavengerHealingTicks == 0)
            target.StartScavengerHealing(scavengerTuning.Value.HealingDurationTicks);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.UsesNativeRandomSeeds;

        AuthoritativeCorpse? corpse = null;
        if (target.ScavengerCorpseEntityId != 0)
            world.Corpses.TryGetValue(target.ScavengerCorpseEntityId, out corpse);
        if (corpse is null
            || (corpse.LevelId != 0 && corpse.LevelId != target.LevelId)
            || DeterministicGridMetrics.ChebyshevDistance(
                corpse.PositionX,
                corpse.PositionY,
                target.PositionX,
                target.PositionY)
                > scavengerTuning.Value.CorpseSearchRadius
            || !world.HasAiLineOfSight(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                corpse.PositionX,
                corpse.PositionY)) {
            corpse = usesNativeAiStream
                ? FindNativeScavengerCorpseFromRandom(
                    target,
                    scavengerTuning.Value.CorpseSearchRadius)
                : FindNativeScavengerCorpse(
                    target,
                    scavengerTuning.Value.CorpseSearchRadius);
            target.SelectScavengerCorpse(corpse?.EntityId ?? 0);
        }

        if (corpse is null) {
            target.AdvanceScavengerHealing();
            if (target.ScavengerHealingTicks == 0)
                target.ClearScavengerHealing();
            return false;
        }

        if (target.PositionX == corpse.PositionX
            && target.PositionY == corpse.PositionY) {
            var healingQ6 = rules.GameVariant == DiabloGameVariant.Hellfire
                ? target.MaxHitPointsQ6 / scavengerTuning.Value.HellfireHealingDivisor
                : DeterministicFixedPointQ6.FromWhole(scavengerTuning.Value.BaseHealingAmount);
            if (rules.GameVariant != DiabloGameVariant.Hellfire)
                healingQ6 = DeterministicFixedPointQ6.FromWhole(scavengerTuning.Value.BaseHealingAmount);
            target.ApplyHealingQ6(healingQ6);
            target.AdvanceScavengerHealing();
            var targetHealthQ6 = rules.GameVariant == DiabloGameVariant.Hellfire
                ? target.MaxHitPointsQ6
                : target.MaxHitPointsQ6
                    * scavengerTuning.Value.BaseHealingTargetPercent
                    / 100;
            if (rules.GameVariant == DiabloGameVariant.Hellfire
                && (target.HitPointsQ6 == target.MaxHitPointsQ6
                    || target.ScavengerHealingTicks == 0)) {
                world.Corpses.Remove(corpse.EntityId);
            }
            if (target.HitPointsQ6 >= targetHealthQ6
                || target.ScavengerHealingTicks == 0)
                target.ClearScavengerHealing();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepTowardFromRandom(
                target,
                corpse.PositionX,
                corpse.PositionY,
                players)
            : FindNativeRandomWalkStepToward(
                target,
                corpse.PositionX,
                corpse.PositionY,
                players,
                unchecked((uint)tick ^ target.EntityId ^ corpse.EntityId));
        if (randomWalk.HasValue)
            target.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
        target.AdvanceScavengerHealing();
        if (target.ScavengerHealingTicks == 0)
            target.ClearScavengerHealing();
        return true;
    }

    /** Native ScavengerFindCorpse's coin-selected square scan order. */
    private AuthoritativeCorpse? FindNativeScavengerCorpse(
        AuthoritativeCombatTarget scavenger,
        int searchRadius)
    {
        var reverseSearch = random.FlipCoin(2);
        var first = reverseSearch ? searchRadius : -searchRadius;
        var last = reverseSearch ? -searchRadius : searchRadius;
        var increment = reverseSearch ? -1 : 1;
        for (var y = first; reverseSearch ? y >= last : y <= last; y += increment) {
            for (var x = first; reverseSearch ? x >= last : x <= last; x += increment) {
                var candidateX = checked(scavenger.PositionX + x);
                var candidateY = checked(scavenger.PositionY + y);
                var corpse = world.Corpses.Values.FirstOrDefault(candidate =>
                    (candidate.LevelId == 0 || candidate.LevelId == scavenger.LevelId)
                    && candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY);
                if (corpse is not null
                    && world.HasAiLineOfSight(
                        scavenger.LevelId,
                        scavenger.PositionX,
                        scavenger.PositionY,
                        candidateX,
                        candidateY))
                    return corpse;
            }
        }
        return null;
    }

    private (
        int HealingDurationTicks,
        int CorpseSearchRadius,
        int HellfireHealingDivisor,
        int BaseHealingAmount,
        int BaseHealingTargetPercent,
        int HealingHealthThresholdPercent)? GetScavengerTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasHealingTuning = definition.ScavengerHealingDurationTicks > 0
                && definition.ScavengerCorpseSearchRadius > 0
                && definition.ScavengerHellfireHealingDivisor > 0
                && definition.ScavengerBaseHealingAmount > 0
                && definition.ScavengerBaseHealingTargetPercent > 0;
            var hasThresholdTuning = definition.ScavengerHealingHealthThresholdPercent > 0;
            if (hasHealingTuning || hasThresholdTuning) {
                return (
                    hasHealingTuning ? definition.ScavengerHealingDurationTicks : 10,
                    hasHealingTuning ? definition.ScavengerCorpseSearchRadius : 4,
                    hasHealingTuning ? definition.ScavengerHellfireHealingDivisor : 8,
                    hasHealingTuning ? definition.ScavengerBaseHealingAmount : 1,
                    hasHealingTuning ? definition.ScavengerBaseHealingTargetPercent : 75,
                    hasThresholdTuning ? definition.ScavengerHealingHealthThresholdPercent : 50);
            }
        }
        return UsesNativeAi(target, "Scavenger")
            ? (10, 4, 8, 1, 75, 50)
            : null;
    }

    private void ReplaceDeadMonsterWithCorpse(AuthoritativeCombatTarget target)
    {
        if (!world.CombatTargets.ContainsKey(target.EntityId))
            return;
        world.RefreshNativeCatacombsArrowSource(target);
        world.InterruptNativeInitialSpecialStand(target.EntityId);
        world.CompleteNativeSarcophagusActorDeath(target.EntityId);
        world.CompleteNativeBarrelActorDeath(target.EntityId);
        if (!world.CombatTargets.Remove(target.EntityId))
            throw new InvalidOperationException("A validated native corpse target disappeared before replacement.");
        world.Corpses[target.EntityId] = new AuthoritativeCorpse(
            target.EntityId,
            target.LevelId,
            target.PositionX,
            target.PositionY);
    }

    private bool TryAdvanceSneakAi(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var sneakTuning = GetSneakTuning(target);
        if (!sneakTuning.HasValue)
            return false;

        var preferredDistance = Math.Max(
            1,
            sneakTuning.Value.DistanceBase
                - (target.Intelligence * sneakTuning.Value.DistanceIntelligenceScale));
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)target.EntityId ^ victim.EntityId);
        // Native SneakAi consumes one GenerateRnd(100) before evaluating its
        // fade, retreat, and adjacent-attack branches. Zero-seed targets keep
        // the deterministic compatibility path and therefore do not advance
        // the shared stream here.
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (target.SneakRetreatTicks != 0) {
            if (distance >= preferredDistance + sneakTuning.Value.RetreatReleaseDistance) {
                target.StartSneakRetreat(0);
                return false;
            }

            var direction = GetDirection(
                victim.PositionX,
                victim.PositionY,
                target.PositionX,
                target.PositionY);
            (int X, int Y)? retreat;
            if (usesNativeAiStream) {
                retreat = FindNativeRandomWalkStepFromRandom(target, direction, players);
            } else {
                retreat = null;
                foreach (var candidateDirection in DeterministicGridDirections.EnumerateAlternating(
                             direction)) {
                    var displacement = DirectionDisplacements[candidateDirection];
                    var candidateX = checked(target.PositionX + displacement.X);
                    var candidateY = checked(target.PositionY + displacement.Y);
                    if (!world.IsWalkable(target.LevelId, candidateX, candidateY)
                        || world.IsOccupied(
                            target.LevelId,
                            candidateX,
                            candidateY,
                            players,
                            target.EntityId)) {
                        continue;
                    }
                    retreat = (candidateX, candidateY);
                    break;
                }
            }
            if (retreat.HasValue) {
                target.BeginMovement(retreat.Value.X, retreat.Value.Y);
            }
            target.AdvanceSneakRetreat();
            return true;
        }

        if (target.SneakHidden) {
            if (distance < preferredDistance)
                target.SetSneakHidden(false);
            return true;
        }

        if (distance >= preferredDistance + 1) {
            target.SetSneakHidden(true);
            return true;
        }

        // Native Sneak only starts an adjacent attack when its action roll is
        // below intelligence + 10 (scaled by four). Returning true suppresses
        // the generic melee fallback for the admitted no-attack tick.
        if (usesNativeAiStream
            && distance <= 1
            && actionRoll >= Math.Min(100, 4 * target.Intelligence + 10))
            return true;
        return false;
    }

    private (int DistanceBase, int DistanceIntelligenceScale, int RetreatReleaseDistance, int RetreatTicks)?
        GetSneakTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SneakDistanceBase > 0
                && definition.SneakDistanceIntelligenceScale > 0
                && definition.SneakRetreatReleaseDistance > 0
                && definition.SneakRetreatTicks > 0) {
                return (
                    definition.SneakDistanceBase,
                    definition.SneakDistanceIntelligenceScale,
                    definition.SneakRetreatReleaseDistance,
                    definition.SneakRetreatTicks);
            }
        }
        return UsesNativeAi(target, "Sneak")
            ? (5, 1, 3, 9)
            : null;
    }

    private (
        int ChanceBase,
        int IntelligenceScale,
        int RetreatDurationTicks)? GetBatAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasAttackTuning = definition.BatAttackChanceBase > 0
                && definition.BatAttackChanceIntelligenceScale > 0;
            var hasRetreatTuning = definition.BatRetreatDurationTicks > 0;
            if (hasAttackTuning || hasRetreatTuning) {
                return (
                    hasAttackTuning ? definition.BatAttackChanceBase : 8,
                    hasAttackTuning ? definition.BatAttackChanceIntelligenceScale : 4,
                    hasRetreatTuning ? definition.BatRetreatDurationTicks : 2);
            }
        }
        return UsesNativeAi(target, "Bat")
            ? (8, 4, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonMeleeDelayTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonMeleeDelayChanceBase > 0
                && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonMeleeDelayChanceBase,
                    definition.SkeletonMeleeDelayChanceIntelligenceScale);
            }
        }
        return UsesNativeSkeletonAi(target)
            ? (35, 4)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonMeleeAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonMeleeAttackChanceBase > 0
                && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonMeleeAttackChanceBase,
                    definition.SkeletonMeleeAttackChanceIntelligenceScale);
            }
        }
        return UsesNativeSkeletonAi(target)
            ? (20, 2)
            : null;
    }

    private int ResolveSkeletonMeleeMovementDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 15;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeMovementDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeMovementDelayBase > 0
                ? definition.SkeletonMeleeMovementDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeMovementDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeMovementDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeMovementDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((actionSeed / 100U) % (uint)randomRange)));
    }

    private int ResolveSkeletonMeleeMovementDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 15;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeMovementDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeMovementDelayBase > 0
                ? definition.SkeletonMeleeMovementDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeMovementDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeMovementDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeMovementDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveSkeletonMeleeAttackDelay(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeAttackDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeAttackDelayBase > 0
                ? definition.SkeletonMeleeAttackDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeAttackDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeAttackDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeAttackDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((attackSeed / 100U) % (uint)randomRange)));
    }

    private int ResolveSkeletonMeleeAttackDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeAttackDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeAttackDelayBase > 0
                ? definition.SkeletonMeleeAttackDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeAttackDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeAttackDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeAttackDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    /** FallenAi delegates its normal state to the native SkeletonAi controller. */
    private bool UsesNativeSkeletonAi(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            // Validated controller tuples take precedence over a legacy label
            // when a compatibility target carries both. Unique encounters
            // may still retain a generic catalog type while supplying an
            // explicit native AI identity, so the label fallback remains
            // below the complete-tuning checks.
            var hasSkeletonTuning = definition.SkeletonMeleeDelayChanceBase > 0
                && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0
                || definition.SkeletonMeleeAttackChanceBase > 0
                && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0;
            if (hasSkeletonTuning)
                return true;
            if (target.NativeAi is "SkeletonMelee" or "Snotspill" or "Warlord"
                && target.NativeAi != definition.NativeAi)
                return true;
            if (target.NativeAi == definition.NativeAi
                && (GetSneakTuning(target).HasValue
                    || GetBatAttackTuning(target).HasValue
                    || GetGoatMeleeMovementTuning(target).HasValue
                    || GetGoatMeleeAttackTuning(target).HasValue
                    || GetFatMovementTuning(target).HasValue
                    || GetFatAttackTuning(target).HasValue
                    || GetZombieActionTuning(target).HasValue
                    || GetZombieMovementTuning(target).HasValue
                    || GetZombieRandomDirectionTuning(target).HasValue
                    || GetSkeletonKingSummonTuning(target).HasValue
                    || GetHorkDemonTuning(target).HasValue
                    || GetScavengerTuning(target).HasValue
                    || GetGargoyleTuning(target).HasValue
                    || GetSkeletonRangedTuning(target).HasValue
                    || GetRhinoTuning(target).HasValue))
                return false;
            if (definition.HasCompleteAiTuning)
                return false;
            return definition.NativeAi is "SkeletonMelee" or "Fallen" or "Snotspill";
        }
        return target.NativeAi is "SkeletonMelee" or "Fallen" or "Snotspill" or "Warlord";
    }

    private (int ChanceBase, int IntelligenceScale)? GetGoatMeleeMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.GoatMeleeMovementChanceBase > 0
                && definition.GoatMeleeMovementChanceIntelligenceScale > 0) {
                return (
                    definition.GoatMeleeMovementChanceBase,
                    definition.GoatMeleeMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "GoatMelee")
            || UsesNativeAi(target, "Gharbad")
            ? (28, 2)
            : null;
    }

    private (
        int ChanceBase,
        int IntelligenceScale,
        int SpecialHealthThresholdPercent,
        int SpecialChanceGroupPeriod)? GetGoatMeleeAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasAttackTuning = definition.GoatMeleeAttackChanceBase > 0
                && definition.GoatMeleeAttackChanceIntelligenceScale > 0;
            var hasSpecialTuning = definition.GoatMeleeSpecialHealthThresholdPercent > 0
                && definition.GoatMeleeSpecialChanceGroupPeriod > 0;
            if (hasAttackTuning || hasSpecialTuning) {
                return (
                    hasAttackTuning
                        ? definition.GoatMeleeAttackChanceBase
                        : 23,
                    hasAttackTuning
                        ? definition.GoatMeleeAttackChanceIntelligenceScale
                        : 2,
                    hasSpecialTuning
                        ? definition.GoatMeleeSpecialHealthThresholdPercent
                        : 50,
                    hasSpecialTuning
                        ? definition.GoatMeleeSpecialChanceGroupPeriod
                        : 2);
            }
        }
        return UsesNativeAi(target, "GoatMelee")
            || UsesNativeAi(target, "Gharbad")
            ? (23, 2, 50, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetFatMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FatMovementChanceBase > 0
                && definition.FatMovementChanceIntelligenceScale > 0) {
                return (
                    definition.FatMovementChanceBase,
                    definition.FatMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Fat")
            ? (20, 4)
            : null;
    }

    private (
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int SpecialChanceBase,
        int SpecialChanceIntelligenceScale)? GetFatAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FatAttackChanceBase > 0
                && definition.FatAttackChanceIntelligenceScale > 0
                && definition.FatSpecialChanceBase > 0
                && definition.FatSpecialChanceIntelligenceScale > 0) {
                return (
                    definition.FatAttackChanceBase,
                    definition.FatAttackChanceIntelligenceScale,
                    definition.FatSpecialChanceBase,
                    definition.FatSpecialChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Fat")
            ? (20, 4, 15, 4)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetZombieActionTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieActionChanceBase > 0
                && definition.ZombieActionChanceIntelligenceScale > 0) {
                return (
                    definition.ZombieActionChanceBase,
                    definition.ZombieActionChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (10, 2)
            : null;
    }

    private (int MinimumDistance, int DistanceBase, int IntelligenceScale)?
        GetZombieMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieMovementDistanceMinimum > 0
                && definition.ZombieMovementDistanceBase > 0
                && definition.ZombieMovementDistanceIntelligenceScale > 0) {
                return (
                    definition.ZombieMovementDistanceMinimum,
                    definition.ZombieMovementDistanceBase,
                    definition.ZombieMovementDistanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (2, 4, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)?
        GetZombieRandomDirectionTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieRandomDirectionChanceBase > 0
                && definition.ZombieRandomDirectionChanceIntelligenceScale > 0) {
                return (
                    definition.ZombieRandomDirectionChanceBase,
                    definition.ZombieRandomDirectionChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (20, 2)
            : null;
    }

    private bool TryAdvanceSkeletonKingAi(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (target.AdvanceSkeletonKingDelay())
            return true;

        var summonTuning = GetSkeletonKingSummonTuning(target);
        if (!summonTuning.HasValue)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.UsesNativeRandomSeeds;
        var usesNativeActionRoll = gameMode == DiabloGameMode.SinglePlayer
            || usesNativeAiStream;
        var actionSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
        // Native LeoricAi consumes its first GenerateRnd(100) before the
        // summon/attack branches, even when multiplayer quest rules suppress
        // summoning. Single-player uses the process stream; seed-zero
        // multiplayer targets retain the compatibility path.
        var actionRoll = usesNativeActionRoll
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        var canAttemptSummon = summonTuning.HasValue
            && monsterSpawnPlanner is not null
            && gameMode != DiabloGameMode.Multiplayer
            && distance >= summonTuning.Value.MinimumDistance
            && actionRoll < Math.Min(
                    100,
                    summonTuning.Value.ChanceBase
                        + (summonTuning.Value.IntelligenceScale * target.Intelligence))
            && world.HasMissileLineOfSight(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                victim.PositionX,
                victim.PositionY)
            && world.CombatTargets.Count < 256;
        if (canAttemptSummon) {
            var direction = GetDirection(
                target.PositionX,
                target.PositionY,
                victim.PositionX,
                victim.PositionY);
            var displacement = DirectionDisplacements[direction];
            var positionX = checked(target.PositionX + displacement.X);
            var positionY = checked(target.PositionY + displacement.Y);
            if (world.IsWalkable(target.LevelId, positionX, positionY)
                && !world.IsOccupied(
                    target.LevelId,
                    positionX,
                    positionY,
                    players,
                    target.EntityId)) {
                var entityId = AllocateMonsterEntityId(players);
                var summon = monsterSpawnPlanner!.CreateSkeletonSummon(
                    target.LevelId,
                    random,
                    positionX,
                    positionY,
                    entityId,
                    gameMode == DiabloGameMode.Multiplayer,
                    direction);
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
                    world.CombatTargets.Add(entityId, summon);
                    return true;
                }
            }
        }

        if (distance <= 1) {
            // Native Leoric uses the first action roll for adjacent attack
            // admission once its summon branch is not taken.
            if (usesNativeActionRoll
                && actionRoll >= Math.Min(100, target.Intelligence + 20))
                return true;
            return false;
        }

        var movementTuning = GetSkeletonKingMovementTuning(target);
        var movementChance = Math.Min(
            100,
            (movementTuning?.ChanceBase ?? 25)
                + (movementTuning?.IntelligenceScale ?? 1) * target.Intelligence);
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= (uint)movementChance) {
            target.StartSkeletonKingDelay(
                usesNativeAiStream
                    ? ResolveSkeletonKingDelayFromRandom(target)
                    : ResolveSkeletonKingDelay(target, actionSeed));
            return true;
        }

        var roundWalkChanceModulo = 4;
        if (monsterTypes?.TryGet(target.MonsterId, out var skeletonKingDefinition) == true
            && skeletonKingDefinition.SkeletonKingRoundWalkChanceModulo > 0) {
            roundWalkChanceModulo = skeletonKingDefinition.SkeletonKingRoundWalkChanceModulo;
        }
        var admitsRoundWalk = distance >= 3
            && (usesNativeAiStream
                ? random.FlipCoin((uint)roundWalkChanceModulo)
                : (actionSeed / 100U) % (uint)roundWalkChanceModulo == 0U);
        if (admitsRoundWalk) {
            var roundWalk = usesNativeAiStream
                ? FindNativeRoundWalkStepFromRandom(target, victim, players)
                : FindNativeRoundWalkStep(target, victim, players, actionSeed);
            if (roundWalk.HasValue) {
                target.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                return true;
            }

            // Native LeoricAi enters AiDelay when its admitted RoundWalk
            // cannot find a candidate; do not replace that tick with a
            // different movement policy.
            target.StartSkeletonKingDelay(
                usesNativeAiStream
                    ? ResolveSkeletonKingDelayFromRandom(target)
                    : ResolveSkeletonKingDelay(target, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(target, victim, players)
            : FindNativeRandomWalkStep(target, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            target.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native LeoricAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    private (int MinimumDistance, int ChanceBase, int IntelligenceScale)?
        GetSkeletonKingSummonTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonKingSummonMinimumDistance > 0
                && definition.SkeletonKingSummonChanceBase > 0
                && definition.SkeletonKingSummonChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonKingSummonMinimumDistance,
                    definition.SkeletonKingSummonChanceBase,
                    definition.SkeletonKingSummonChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "SkeletonKing")
            ? (3, 35, 4)
            : null;
    }

    private int ResolveSkeletonKingDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingDelayBase > 0
            && definition.SkeletonKingDelayRandomRange > 0) {
            delayBase = definition.SkeletonKingDelayBase;
            randomRange = definition.SkeletonKingDelayRandomRange;
        }
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    /** Native Leoric delay roll driven by the restored per-monster stream. */
    private int ResolveSkeletonKingDelayFromRandom(AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingDelayBase > 0
            && definition.SkeletonKingDelayRandomRange > 0) {
            delayBase = definition.SkeletonKingDelayBase;
            randomRange = definition.SkeletonKingDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonKingMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingMovementChanceBase > 0
            && definition.SkeletonKingMovementChanceIntelligenceScale > 0) {
            return (
                definition.SkeletonKingMovementChanceBase,
                definition.SkeletonKingMovementChanceIntelligenceScale);
        }
        return UsesNativeAi(target, "SkeletonKing")
            ? (25, 1)
            : null;
    }

    /** Native ProcessMonsters health regeneration, applied before each AI tick. */
    private void ApplyNativeMonsterRegeneration(
        AuthoritativeCombatTarget monster,
        int levelDivisor)
    {
        var effectiveMonsterLevel = GetEffectiveMonsterLevel(monster);
        if (effectiveMonsterLevel <= 0 || monster.HitPointsQ6 < DeterministicFixedPointQ6.Scale
            || monster.HitPointsQ6 >= monster.MaxHitPointsQ6)
            return;

        var healing = effectiveMonsterLevel > 1
            ? effectiveMonsterLevel / levelDivisor
            : effectiveMonsterLevel;
        // ProcessMonsters adds this amount directly to native Q6 hit points.
        monster.ApplyHealingQ6(healing);
    }

    /** Native ZombieAi action band and long-range walk boundary. */
    private bool TryAdvanceZombieAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var zombieActionTuning = GetZombieActionTuning(monster);
        var zombieMovementTuning = GetZombieMovementTuning(monster);
        if (!zombieActionTuning.HasValue || !zombieMovementTuning.HasValue)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var actionChance = Math.Min(
            100,
            zombieActionTuning.Value.ChanceBase
                + (zombieActionTuning.Value.IntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll >= actionChance)
            return true;
        var longRangeMovementDistance = Math.Max(
            zombieMovementTuning.Value.MinimumDistance,
            zombieMovementTuning.Value.DistanceBase
                + (zombieMovementTuning.Value.IntelligenceScale * monster.Intelligence));
        if (distance < longRangeMovementDistance) {
            if (distance < 2)
                return false;
            var randomStep = usesNativeAiStream
                ? FindNativeRandomWalkStepFromRandom(monster, player, players)
                : FindNativeRandomWalkStep(monster, player, players, actionSeed);
            if (randomStep.HasValue)
                monster.BeginMovement(randomStep.Value.X, randomStep.Value.Y);
            return true;
        }

        if (usesNativeAiStream) {
            var direction = monster.Direction;
            var randomDirectionChance = Math.Min(
                100,
                zombieMovementTuning.Value.IntelligenceScale * monster.Intelligence + 20);
            if (random.RandomIntLessThan(100) < randomDirectionChance)
                direction = random.RandomIntLessThan(8);
            var nativeStep = FindStepInDirection(monster, direction, players);
            if (nativeStep.HasValue)
                monster.BeginMovement(nativeStep.Value.X, nativeStep.Value.Y);
            return true;
        }

        var step = FindZombieStep(monster, player, players, actionSeed);
        if (step.HasValue) {
            monster.BeginMovement(step.Value.X, step.Value.Y);
            return true;
        }
        // Native ZombieAi consumes the action even when its one-step walk is
        // blocked; do not replace the failed walk with generic navigation.
        return true;
    }

    private (int X, int Y)? FindZombieStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var deltaX = Math.Sign((long)player.PositionX - monster.PositionX);
        var deltaY = Math.Sign((long)player.PositionY - monster.PositionY);
        var directions = new[] {
            (X: deltaX, Y: deltaY),
            (X: deltaX, Y: 0),
            (X: 0, Y: deltaY),
        };
        var randomDirectionTuning = GetZombieRandomDirectionTuning(monster);
        if (randomDirectionTuning.HasValue
            && seed % 100U < (uint)Math.Min(
                100,
                randomDirectionTuning.Value.ChanceBase
                    + (randomDirectionTuning.Value.IntelligenceScale
                        * monster.Intelligence))) {
            var randomDirection = (int)((seed / 100U) % 8U);
            directions = new[] {
                (X: randomDirection switch { 0 or 1 or 7 => 1, 2 or 6 => 0, _ => -1 },
                    Y: randomDirection switch { 1 or 2 or 3 => 1, 5 or 6 or 7 => -1, _ => 0 }),
            }.Concat(directions).ToArray();
        }
        foreach (var direction in directions) {
            if (direction.X == 0 && direction.Y == 0)
                continue;
            var candidate = (X: monster.PositionX + direction.X, Y: monster.PositionY + direction.Y);
            if (world.IsWalkable(monster.LevelId, candidate.X, candidate.Y)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidate.X,
                    candidate.Y,
                    players,
                    monster.EntityId))
                return candidate;
        }
        return null;
    }

    /** Native Counselor retreat RandomWalk ordering driven by the restored stream. */
    private (int X, int Y)? FindNativeCounselorRetreatStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var towardPlayer = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var awayFromPlayer = RotateDirection(towardPlayer, 4);
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        foreach (var candidateDirection in new[] {
                     awayFromPlayer,
                     RotateDirection(awayFromPlayer, firstTurn),
                     RotateDirection(awayFromPlayer, -firstTurn),
                     RotateDirection(awayFromPlayer, secondTurn),
                     RotateDirection(awayFromPlayer, -secondTurn),
                 }) {
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

    /** Native ScavengerFindCorpse scan driven by the restored stream. */
    private AuthoritativeCorpse? FindNativeScavengerCorpseFromRandom(
        AuthoritativeCombatTarget scavenger,
        int searchRadius)
    {
        var reverseSearch = random.FlipCoin(2);
        var first = reverseSearch ? searchRadius : -searchRadius;
        var last = reverseSearch ? -searchRadius : searchRadius;
        var increment = reverseSearch ? -1 : 1;
        for (var y = first; reverseSearch ? y >= last : y <= last; y += increment) {
            for (var x = first; reverseSearch ? x >= last : x <= last; x += increment) {
                var candidateX = checked(scavenger.PositionX + x);
                var candidateY = checked(scavenger.PositionY + y);
                var corpse = world.Corpses.Values.FirstOrDefault(candidate =>
                    (candidate.LevelId == 0 || candidate.LevelId == scavenger.LevelId)
                    && candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY);
                if (corpse is not null
                    && world.HasAiLineOfSight(
                        scavenger.LevelId,
                        scavenger.PositionX,
                        scavenger.PositionY,
                        candidateX,
                        candidateY))
                    return corpse;
            }
        }
        return null;
    }

    /** Native Scavenger RandomWalk toward its selected corpse driven by the stream. */
    private (int X, int Y)? FindNativeRandomWalkStepTowardFromRandom(
        AuthoritativeCombatTarget monster,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY);
        return FindNativeRandomWalkStepFromRandom(monster, direction, players);
    }

    private (int X, int Y)? FindStepInDirection(
        AuthoritativeCombatTarget monster,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId)
            ? (candidateX, candidateY)
            : null;
    }

    /** Native RandomWalk candidate order shared by movement AI controllers. */
    private (int X, int Y)? FindNativeRandomWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
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

    /** Native RandomWalk ordering driven by the restored per-monster stream. */
    private (int X, int Y)? FindNativeRandomWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return FindNativeRandomWalkStepFromRandom(
            monster,
            GetDirection(
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY),
            players);
    }

    /** Native RandomWalk ordering from an explicitly selected direction. */
    private (int X, int Y)? FindNativeRandomWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native RandomWalk candidate order toward an arbitrary AI target tile. */
    private (int X, int Y)? FindNativeRandomWalkStepToward(
        AuthoritativeCombatTarget monster,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
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

    /** Native Gargoyle self-healing and its distance-based special lock. */
    private bool TryAdvanceGargoyleHealing(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        int distance)
    {
        var gargoyleTuning = GetGargoyleTuning(monster);
        if (!gargoyleTuning.HasValue)
            return false;
        if (monster.AdvanceAvoidanceDelay())
            return true;

        var safeDistance = gargoyleTuning.Value.DistanceBase
            + (gargoyleTuning.Value.IntelligenceScale * monster.Intelligence);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        if (monster.GargoyleSpecialActive) {
            if (distance < safeDistance)
                monster.GargoyleSpecialActive = false;
            return true;
        }

        if (monster.HitPointsQ6 >= monster.MaxHitPointsQ6
                * gargoyleTuning.Value.HealingHealthThresholdPercent
                / 100)
            return false;

        if (distance < safeDistance) {
            if (!usesNativeAiStream)
                return TryBeginGargoyleRetreat(monster, player, players, distance);

            var direction = GetDirection(
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY);
            var retreat = FindNativeRandomWalkStepFromRandom(
                monster,
                RotateDirection(direction, 4),
                players);
            if (retreat.HasValue) {
                monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                return true;
            }
            return false;
        }

        var healSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var healRoll = usesNativeAiStream
            ? random.RandomIntLessThan(gargoyleTuning.Value.HealDivisorRandomRange)
            : (int)(healSeed % (uint)gargoyleTuning.Value.HealDivisorRandomRange);
        var divisor = gargoyleTuning.Value.HealDivisorMultiplier
            * (healRoll
                + gargoyleTuning.Value.HealDivisorRandomOffset);
        var healingQ6 = DeterministicFixedPointQ6.FromWhole(
            monster.MaxHitPoints / divisor);
        var appliedHealingQ6 = monster.ApplyHealingQ6(healingQ6);
        if (appliedHealingQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                player,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Healing,
                    monster.EntityId,
                    monster.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
                    AmountQ6 = appliedHealingQ6,
                }));
        }

        // Native MFLAG_ALLOW_SPECIAL remains set until the player closes the
        // intelligence+2 boundary, unless this heal reached full life.
        monster.GargoyleSpecialActive = monster.HitPointsQ6 < monster.MaxHitPointsQ6;
        return true;
    }

    private (
        int DistanceBase,
        int IntelligenceScale,
        int HealingHealthThresholdPercent,
        int HealDivisorMultiplier,
        int HealDivisorRandomRange,
        int HealDivisorRandomOffset)? GetGargoyleTuning(
        AuthoritativeCombatTarget target)
    {
        if (HasDifferentCatacombsEffectiveAi(target, "Gargoyle"))
            return null;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasSafetyTuning = definition.GargoyleSafeDistanceBase > 0
                && definition.GargoyleSafeDistanceIntelligenceScale > 0;
            var hasHealingTuning = definition.GargoyleHealingHealthThresholdPercent > 0
                || definition.GargoyleHealDivisorMultiplier > 0
                || definition.GargoyleHealDivisorRandomRange > 0
                || definition.GargoyleHealDivisorRandomOffset > 0;
            if (hasSafetyTuning || hasHealingTuning) {
                return (
                    hasSafetyTuning ? definition.GargoyleSafeDistanceBase : 2,
                    hasSafetyTuning ? definition.GargoyleSafeDistanceIntelligenceScale : 1,
                    definition.GargoyleHealingHealthThresholdPercent > 0
                        ? definition.GargoyleHealingHealthThresholdPercent
                        : 50,
                    definition.GargoyleHealDivisorMultiplier > 0
                        ? definition.GargoyleHealDivisorMultiplier
                        : 16,
                    definition.GargoyleHealDivisorRandomRange > 0
                        ? definition.GargoyleHealDivisorRandomRange
                        : 5,
                    definition.GargoyleHealDivisorRandomOffset > 0
                        ? definition.GargoyleHealDivisorRandomOffset
                        : 4);
            }
        }
        return UsesNativeAi(target, "Gargoyle")
            ? (2, 1, 50, 16, 5, 4)
            : null;
    }

    /** Native Gargoyle AiAvoidance adjacent attack admission. */
    private (int ChanceBase, int IntelligenceScale)? GetGargoyleAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (HasDifferentCatacombsEffectiveAi(target, "Gargoyle"))
            return null;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.GargoyleAttackChanceBase > 0
            && definition.GargoyleAttackChanceIntelligenceScale > 0) {
            return (
                definition.GargoyleAttackChanceBase,
                definition.GargoyleAttackChanceIntelligenceScale);
        }
        return UsesNativeAi(target, "Gargoyle")
            ? (23, 2)
            : null;
    }

    /** Native GargoyleAi delegates healthy non-adjacent ticks to AiAvoidance. */
    private bool TryAdvanceGargoyleMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!HasGargoyleAi(monster) || distance <= 1)
            return false;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        var advancedMoveGoal = usesNativeAiStream
            ? TryAdvanceAiAvoidanceMoveGoalFromRandom(
                monster,
                player,
                players,
                distance,
                actionRoll)
            : TryAdvanceAiAvoidanceMoveGoal(
                monster,
                player,
                players,
                distance,
                actionSeed);
        if (advancedMoveGoal)
            return true;

        var movementTuning = GetGargoyleMovementTuning(monster);
        var movementChance = GetAiAvoidanceMovementChance(
            monster,
            movementTuning?.ChanceBase ?? 28,
            movementTuning?.IntelligenceScale ?? 2,
            GetAiAvoidanceMovementFreshChanceBonus(monster));
        if (actionRoll >= movementChance)
        {
            monster.AdvanceAvoidanceMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
        } else {
            monster.AdvanceAvoidanceMovementIdle();
        }
        return true;
    }

    private bool HasGargoyleAi(AuthoritativeCombatTarget target)
        => !HasDifferentCatacombsEffectiveAi(target, "Gargoyle")
            && (target.NativeAi == "Gargoyle"
            || monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.NativeAi == "Gargoyle");

    private (int ChanceBase, int IntelligenceScale)? GetGargoyleMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (HasDifferentCatacombsEffectiveAi(target, "Gargoyle"))
            return null;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.GargoyleMovementChanceBase > 0
            && definition.GargoyleMovementChanceIntelligenceScale > 0) {
            return (
                definition.GargoyleMovementChanceBase,
                definition.GargoyleMovementChanceIntelligenceScale);
        }
        return HasGargoyleAi(target)
            ? (28, 2)
            : null;
    }

    /** Native GargoyleAi retreats from a wounded player before healing. */
    private bool TryBeginGargoyleRetreat(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int currentDistance)
    {
        var retreat = Enumerable.Range(-1, 3)
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
            .Select(candidate => ((int X, int Y)?)candidate)
            .FirstOrDefault();

        if (!retreat.HasValue)
            return false;

        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
        return true;
    }

    /** Native Arrow behavior selected by a validated MonsterArrow mapping. */
    private bool TryAdvanceGoatRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.MonsterArrow
                or AuthoritativeProjectileBehavior.Targeted)
            || GetEffectiveMonsterToHit(monster, special: false) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: false).Maximum <= 0
            || IsPlayerProtectedByFlash(player)
            || IsPlayerEtherealized(player))
            return false;

        if (monster.AdvanceRangedDelay())
            return true;

        var nativeCatacombsRangedController = IsNativeCatacombsRangedController(monster, missile);
        if (HasActiveMonsterArrow(monster) && !nativeCatacombsRangedController)
            return true;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
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
                player.PositionY))
            // Native AiRanged does not pursue when its clear missile lane is
            // unavailable. Close-range avoidance has already run above;
            // preserve the remaining deferred tick instead of generic
            // navigation.
            return true;

        if (nativeCatacombsRangedController
            && TryStartNativeCatacombsRangedAction(monster, player, missile, attackSeed))
            return true;

        var created = CreateNativeRangedAttackProjectile(
            monster,
            player,
            distance,
            attackSeed,
            missile);
        if (created)
            monster.StartRangedDelay(
                usesNativeAiStream
                    ? ResolveRangedDelayFromRandom(missile)
                    : ResolveRangedDelay(missile, attackSeed));
        return created;
    }

    /** Native Snake charge behavior selected by a validated SnakeCharge mapping. */
    private bool TryAdvanceSnakeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (monsterMissiles is null
            || !TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.SnakeCharge)
            return false;
        if (monster.AdvanceSnakeDelay())
            return true;

        var chargeDistance = missile.ChargeMinimumDistance > 0
            ? missile.ChargeMinimumDistance
            : 2;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.SnakeCharge)) {
            // Native SnakeAi still consumes the adjacent attack-admission draw
            // while an in-flight charge owns the movement path.
            if (distance < 2)
                _ = random.RandomIntLessThan(100);
            return true;
        }
        if (distance == chargeDistance) {
            if (world.HasMissileLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY)) {
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
                    missile.ProjectileDamage,
                    missile.DamageType ?? AuthoritativeDamageType.Physical,
                    0,
                    checked((uint)chargeProjectileTuning.Lifetime)) {
                    Behavior = AuthoritativeProjectileBehavior.SnakeCharge,
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
        }

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (distance < 2) {
            var attackChanceBase = missile.SnakeAdjacentAttackChanceBase > 0
                ? missile.SnakeAdjacentAttackChanceBase
                : 20;
            var attackChanceIntelligenceScale = missile.SnakeAdjacentAttackChanceIntelligenceScale > 0
                ? missile.SnakeAdjacentAttackChanceIntelligenceScale
                : 1;
            var attackChance = Math.Min(
                100,
                attackChanceBase + (attackChanceIntelligenceScale * monster.Intelligence));
            if (actionRoll >= attackChance) {
                monster.StartSnakeDelay(
                    usesNativeAiStream
                        ? ResolveSnakeDelayFromRandom(
                            missile,
                            monster.Intelligence,
                            adjacent: true)
                        : ResolveSnakeDelay(missile, actionSeed, monster.Intelligence, adjacent: true));
                return true;
            }
            return false;
        }

        var delayChanceBase = missile.SnakeDistantDelayChanceBase > 0
            ? missile.SnakeDistantDelayChanceBase
            : 35;
        var delayChanceIntelligenceScale = missile.SnakeDistantDelayChanceIntelligenceScale > 0
            ? missile.SnakeDistantDelayChanceIntelligenceScale
            : 2;
        var delayBand = Math.Max(
            0,
            delayChanceBase - (delayChanceIntelligenceScale * monster.Intelligence));
        if (actionRoll < delayBand) {
            monster.StartSnakeDelay(
                usesNativeAiStream
                    ? ResolveSnakeDelayFromRandom(
                        missile,
                        monster.Intelligence,
                        adjacent: false)
                    : ResolveSnakeDelay(missile, actionSeed, monster.Intelligence, adjacent: false));
            return true;
        }

        var drift = usesNativeAiStream
            ? FindSnakeDriftStepFromRandom(
                monster,
                player,
                players,
                actionSeed,
                missile.SnakeDriftPattern)
            : FindSnakeDriftStep(
                monster,
                player,
                players,
                actionSeed,
                missile.SnakeDriftPattern);
        if (drift.HasValue)
            monster.BeginMovement(drift.Value.X, drift.Value.Y);
        return true;
    }
}
