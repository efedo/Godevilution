using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Monster turn loop and melee family AI (native monster.cpp ProcessMonsters and AI routines). */
public sealed partial class DiabloCombatCommandProcessor
{
    private void AdvanceMonsters(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var target in world.CombatTargets.Values
                     .OrderBy(target => target.EntityId)
                     .ToArray()) {
            // Native inactive levels do not process their monster or RNG state.
            if (OwnsNativeMonsterTiming(target)
                && !players.Any(player => player.LevelId == target.LevelId))
                continue;
            AuthoritativeMonsterGroupUnity.ApplyFollowTheLeader(
                target,
                world.CombatTargets);
            world.ApplyNativeCatacombsGargoyleFollowerWakeBeforeAi(target);
            target.ClearFallenRallyAttack();
            if (target.IsMoving && !target.IsPetrified && !OwnsNativeMonsterTiming(target))
                target.CompleteMovement();
            if (target.NativeInitialization is not { CatacombsDepth: >= 5 and <= 8 })
                AuthoritativeMonsterGroupUnity.Apply(
                    target,
                    world.CombatTargets,
                    world.HasAiLineOfSight);
            AdvanceNativeMonsterAiSeed(target, tick);
            if (target.IsPlayerMinion) {
                AdvancePlayerMinion(target, tick, players, events);
                continue;
            }
            if (target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }) {
                var emergence = world.AdvanceNativeCatacombsInitialSpecialStand(target);
                if (emergence.CompletedNaturally)
                    ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
                if (emergence.Active)
                    continue;
                var objectEmergence = world.AdvanceNativeCatacombsObjectEmergence(target);
                if (objectEmergence.CompletedNaturally)
                    ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
                if (objectEmergence.Active)
                    continue;
            } else if (world.OwnsNativePopulationLevel(target.LevelId)
                && (world.AdvanceNativeInitialSpecialStand(target)
                    || world.AdvanceNativeSarcophagusSpecialStand(target)
                    || world.AdvanceNativeBarrelSpecialStand(target)))
                continue;
            if (target.HitPoints <= 0) {
                world.CancelNativeCatacombsRangedAction(target.EntityId);
                // Native Stone Curse keeps a dead target in its petrified
                // shatter state instead of converting it into a corpse tile.
                if (target.IsPetrified)
                    continue;
                if (target.DeathAnimationTicks != 0
                    && target.AdvanceDeathAnimation()) {
                    if (target.IsPlayerMinion)
                        world.CombatTargets.Remove(target.EntityId);
                    else
                        ReplaceDeadMonsterWithCorpse(target);
                } else if (!target.IsPlayerMinion
                    && target.DeathAnimationTicks == 0) {
                    ReplaceDeadMonsterWithCorpse(target);
                }
                continue;
            }
            ApplyNativeMonsterRegeneration(target, rules.MonsterRegenerationLevelDivisor);
            if (target.IsPetrified)
                continue;
            if (world.HasNativeCatacombsRangedAction(target.EntityId)
                && target.NativeMeleeCursor is { HitRecovery: true })
                world.CancelNativeCatacombsRangedAction(target.EntityId);
            if (world.World is not null
                && players.Count > 0
                && !IsMonsterVisibleToAnyPlayer(target, players)) {
                if (target.ActiveForTicks > 0 && !target.IsDiablo)
                    target.RestoreActiveForTicks(target.ActiveForTicks - 1);
                if (target.ActiveForTicks == 0
                    && !world.HasNativeCatacombsMonsterPhase(target.EntityId)
                    && !world.HasNativeCatacombsRangedAction(target.EntityId)
                    && !(target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
                        && target.NativeMeleeCursor is not null))
                    continue;
            } else if (world.World is not null && players.Count > 0) {
                // Native ProcessMonsters wakes visible monsters for the full
                // active window before dispatching their AI.
                target.RestoreActiveForTicks(byte.MaxValue);
            }
            if (AdvanceNativeCatacombsGargoyle(target, players))
                continue;
            if (AdvanceNativeCatacombsRangedAction(target, players))
                continue;
            if (OwnsNativeMonsterTiming(target) && target.IsMoving) {
                if (!AdvanceNativeWalk(target, tick))
                    continue;
                ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
            }
            if (OwnsNativeMonsterTiming(target) && target.NativeMeleeCursor is not null) {
                var currentEnemy = players.Where(player => player.Life > 0 && player.LevelId == target.LevelId)
                    .OrderBy(player => player.EntityId).FirstOrDefault();
                var animation = AdvanceNativeMelee(target);
                if (animation.EffectFrame && currentEnemy is not null
                    && DeterministicGridMetrics.ChebyshevDistance(target.PositionX, target.PositionY,
                        currentEnemy.PositionX, currentEnemy.PositionY) <= 1
                    && !IsPlayerProtectedByFlash(currentEnemy) && !IsPlayerEtherealized(currentEnemy))
                    ResolveMonsterMeleeEffect(target, currentEnemy, players, tick, events,
                        currentEnemy.ArmorClass, unchecked((uint)tick ^ target.EntityId ^ currentEnemy.EntityId),
                        usesSpecialAttack: animation.UsesSpecialAttack);
                if (!animation.Completed || target.HitPointsQ6 <= 0)
                    continue;
                ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
                if (target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
                    && target.ActiveForTicks == 0)
                    continue;
                // Native UpdateModeStance repeats AI in the same update after entering Stand.
                target.RestoreFallenAnimation(0, 0);
            }
            if (IsQuestDialogueTarget(target))
                continue;
            if (IsLazarusMinionCombatSuppressed(target))
                continue;
            // Native Lachdanan only exposes quest dialogue, while FireMan has
            // no registered native AI; neither may fall through to generic
            // combat or movement when a player is in aggro range.
            if (UsesNativeAi(target, "Lachdanan")
                || UsesNativeAi(target, "FireMan"))
                continue;
            if (TryAdvanceScavengerAi(target, tick, players))
                continue;
            if (TryStartFallenRally(target, tick))
                continue;
            if (TryAdvanceFallenFear(target, players))
                continue;
            if (TryAdvanceBatRetreat(target, players, tick))
                continue;
            var fallenRallyActive = target.FallenRallyTicks > 0;
            // Native actors use the visibility/active window above, not a legacy aggro radius.
            var nativePopulationTarget = OwnsNativeMonsterTiming(target);
            if (!nativePopulationTarget && target.AggroRange <= 0 && !fallenRallyActive)
                continue;

            var victim = players
                .Where(player => player.Life > 0
                    && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                        or DiabloPlayerCombatMode.Quit)
                    && player.LevelId == target.LevelId
                    && (nativePopulationTarget || fallenRallyActive
                        || DeterministicGridMetrics.ChebyshevDistance(
                            player.PositionX,
                            player.PositionY,
                            target.PositionX,
                            target.PositionY) <= target.AggroRange))
                .OrderBy(player => DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX,
                    player.PositionY,
                    target.PositionX,
                    target.PositionY))
                .ThenBy(player => player.EntityId)
                .FirstOrDefault();
            if (victim is null) {
                if (fallenRallyActive)
                    target.AdvanceFallenRally(forceAttack: false);
                continue;
            }

            var victimDistance = DeterministicGridMetrics.ChebyshevDistance(
                victim.PositionX,
                victim.PositionY,
                target.PositionX,
                target.PositionY);
            var distance = ClampNativeDistance(victimDistance);
            // Acid owns one continuous controller from the travelling missile
            // through its splat and persistent puddle. Never admit a second
            // Acid controller while that chain is still live.
            var nativeCatacombsRangedController = TryGetMonsterMissile(target, out var nativeCatacombsMissile)
                && IsNativeCatacombsRangedController(target, nativeCatacombsMissile);
            if (!nativeCatacombsRangedController
                && target.NativeAi is ("Acid" or "AcidUnique")
                && world.Projectiles.Values.Any(projectile =>
                    projectile.SourceEntityId == target.EntityId
                    && (projectile.Behavior == AuthoritativeProjectileBehavior.Acid
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle)))
                continue;
            if (TryAdvanceFallenRally(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSneakAi(target, victim, players, distance))
                continue;
            if (TryAdvanceButcherAi(target, victim, players, distance))
                continue;
            if (TryAdvanceSkeletonAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceGoatMeleeAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceFatAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceHorkDemonAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSkeletonKingAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceZombieAi(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceGargoyleHealing(target, victim, tick, players, events, distance))
                continue;
            if (TryAdvanceGargoyleMovement(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceRhinoCharge(target, victim, tick, players, events, distance))
                continue;
            if (TryAdvanceSnakeAi(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceFamiliarBatAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceMappedChargeAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceMegaAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceCounselorAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSuccubusRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceMappedRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceSkeletonRangedRetreat(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSkeletonRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceGoatRangedAttack(target, victim, tick, players, distance))
                continue;
            if (distance <= 1
                && (GetSkeletonRangedTuning(target).HasValue
                    || HasValidatedArrowMapping(target)))
                continue;
            if (distance <= 1) {
                if (!world.OwnsNativePopulationLevel(target.LevelId)
                    && (IsPlayerProtectedByFlash(victim) || IsPlayerEtherealized(victim)))
                    continue;
                var armorClass = victim.ArmorClass;
                if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                    armorClass = AuthoritativeEquipmentIncomingDamageRules
                        .ApplyHellfireMonsterClassArmor(
                            armorClass,
                            target.IsDemon,
                            target.IsUndead,
                            victim.Equipment,
                            rules.HellfireArmorBonusAgainstDemons,
                            rules.HellfireArmorBonusAgainstUndead);
                }
                var attackSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
                var skeletonMeleeAttackTuning = target.FallenRallyAttackPending
                    ? null
                    : GetSkeletonMeleeAttackTuning(target);
                // Initialized single-player targets retain the native
                // InitMonster AI seed as a marker that their global process
                // stream is authoritative. Zero-seed fixtures and legacy
                // multiplayer snapshots keep the compatibility controller.
                var usesNativeAiStream = target.UsesNativeRandomSeeds;
                var attackRoll = skeletonMeleeAttackTuning.HasValue && usesNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (skeletonMeleeAttackTuning.HasValue
                    && attackRoll >= Math.Min(
                        100,
                        skeletonMeleeAttackTuning.Value.ChanceBase
                            + (skeletonMeleeAttackTuning.Value.IntelligenceScale
                                * target.Intelligence))) {
                    target.StartSkeletonMeleeDelay(
                        usesNativeAiStream
                            ? ResolveSkeletonMeleeAttackDelayFromRandom(target)
                            : ResolveSkeletonMeleeAttackDelay(target, attackSeed));
                    continue;
                }
                var goatMeleeAttackTuning = GetGoatMeleeAttackTuning(target);
                var usesGoatNativeAiStream = goatMeleeAttackTuning.HasValue
                    && target.UsesNativeRandomSeeds;
                var goatAttackRoll = usesGoatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (goatMeleeAttackTuning.HasValue
                    && goatAttackRoll >= Math.Min(
                        100,
                        goatMeleeAttackTuning.Value.ChanceBase
                            + (goatMeleeAttackTuning.Value.IntelligenceScale
                                * target.Intelligence)))
                    continue;
                var gargoyleAttackTuning = GetGargoyleAttackTuning(target);
                var usesGargoyleNativeAiStream = gargoyleAttackTuning.HasValue
                    && target.UsesNativeRandomSeeds;
                var gargoyleAttackRoll = usesGargoyleNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (gargoyleAttackTuning.HasValue
                    && gargoyleAttackRoll >= Math.Min(
                        100,
                        gargoyleAttackTuning.Value.ChanceBase
                            + (gargoyleAttackTuning.Value.IntelligenceScale
                                * target.Intelligence)))
                    continue;
                var fatAttackTuning = GetFatAttackTuning(target);
                var usesFatNativeAiStream = fatAttackTuning.HasValue
                    && target.UsesNativeRandomSeeds;
                var fatAttackRoll = usesFatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (fatAttackTuning.HasValue
                    && !NativeFatAttackBandAllowsAttack(
                        target,
                        fatAttackRoll,
                        fatAttackTuning.Value))
                    continue;
                var batAttackTuning = GetBatAttackTuning(target);
                var usesBatNativeAiStream = batAttackTuning.HasValue
                    && target.UsesNativeRandomSeeds;
                var batAttackRoll = usesBatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (batAttackTuning.HasValue
                    && batAttackRoll >= Math.Min(
                            100,
                            batAttackTuning.Value.ChanceBase
                                + (batAttackTuning.Value.IntelligenceScale
                                    * target.Intelligence)))
                    continue;
                var usesSpecialAttack = UsesFatSpecialAttack(
                    target,
                    fatAttackRoll,
                    fatAttackTuning)
                    || (usesGoatNativeAiStream
                        ? UsesGoatSpecialAttackFromRandom(
                            target,
                            goatMeleeAttackTuning)
                        : UsesGoatSpecialAttack(
                            target,
                            attackSeed,
                            goatMeleeAttackTuning));
                if (OwnsNativeMonsterTiming(target)) {
                    // Legacy C1 initialization has no Special graphic metadata. New
                    // family admission binds it explicitly before enabling that cursor.
                    var nativeSpecial = usesSpecialAttack
                        && target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 };
                    BeginNativeMelee(target, nativeSpecial);
                    var animation = AdvanceNativeMelee(target);
                    if (animation.EffectFrame && !IsPlayerProtectedByFlash(victim) && !IsPlayerEtherealized(victim))
                        ResolveMonsterMeleeEffect(target, victim, players, tick, events,
                            armorClass, attackSeed, usesSpecialAttack: animation.UsesSpecialAttack);
                    continue;
                }
                ResolveMonsterMeleeEffect(target, victim, players, tick, events,
                    armorClass, attackSeed, usesSpecialAttack);
                continue;
            }

            var deltaX = Math.Sign((long)victim.PositionX - target.PositionX);
            var deltaY = Math.Sign((long)victim.PositionY - target.PositionY);
            var candidates = new[] {
                (X: target.PositionX + deltaX, Y: target.PositionY + deltaY),
                (X: target.PositionX + deltaX, Y: target.PositionY),
                (X: target.PositionX, Y: target.PositionY + deltaY),
            };
            var moved = false;
            foreach (var candidate in candidates.Where(
                         candidate => candidate.X != target.PositionX
                             || candidate.Y != target.PositionY)) {
                if (!world.IsWalkable(target.LevelId, candidate.X, candidate.Y)
                    || world.IsOccupied(
                        target.LevelId,
                        candidate.X,
                        candidate.Y,
                        players,
                        target.EntityId)
                    || DeterministicGridMetrics.ChebyshevDistance(
                        victim.PositionX,
                        victim.PositionY,
                        candidate.X,
                        candidate.Y) >= distance) {
                    continue;
                }
                target.BeginMovement(candidate.X, candidate.Y);
                moved = true;
                break;
            }
            if (!moved) {
                var maximumDepth = Math.Clamp(
                    checked(target.AggroRange * 2 + 2),
                    8,
                    256);
                var fallback = DeterministicGridNavigation.FindStep(
                    target.PositionX,
                    target.PositionY,
                    victim.PositionX,
                    victim.PositionY,
                    maximumDepth,
                    (x, y) => world.IsWalkable(target.LevelId, x, y),
                    (x, y) => world.IsOccupied(
                        target.LevelId,
                        x,
                        y,
                        players,
                        target.EntityId));
                if (fallback.HasValue)
                    target.BeginMovement(fallback.Value.X, fallback.Value.Y);
            }
        }
        // Newly admitted walks receive this update's native processAnimation step.
        foreach (var walker in world.CombatTargets.Values.Where(target => target.IsMoving
            && target.HitPointsQ6 > 0 && target.NativeWalkCursor is null
            && OwnsNativeMonsterTiming(target)
            && players.Any(player => player.LevelId == target.LevelId)))
            BeginNativeWalk(walker, tick);
        foreach (var leader in world.CombatTargets.Values
                     .Where(target => target.IsMoving && target.PackSize > 0)
                     .OrderBy(target => target.EntityId)) {
            if (!AuthoritativeMonsterGroupUnity.CanLeaderMove(
                    leader,
                    world.CombatTargets))
                leader.CancelMovement();
        }
        foreach (var member in world.CombatTargets.Values
                     .Where(target => target.IsMoving
                         && target.LeaderRelation
                             == AuthoritativeMonsterLeaderRelation.Leashed)
                     .OrderBy(target => target.EntityId)) {
            if (!AuthoritativeMonsterGroupUnity.CanLeashedMemberMove(
                    member,
                    world.CombatTargets))
                member.CancelMovement();
        }
    }

    /** Returns whether a live player can see the monster in authoritative geometry. */
    private bool IsMonsterVisibleToAnyPlayer(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var level = world.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == target.LevelId);
        return players.Any(player => player.Life > 0
            && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                or DiabloPlayerCombatMode.Quit)
            && player.LevelId == target.LevelId
            && (level is null
                || (level.NativeTransparency.Count == 0
                    && level.NativeVisionBlockedCells.Count == 0)
                || IsWithinNativeVision(level, target, player)));
    }

    /** Native ProcessMonsters normally receives a visibility flag from the
     * player's equipment-derived light-radius vision pass. */
    private static bool IsWithinNativeVision(
        AuthoritativeLevel level,
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState player)
    {
        var deltaX = (long)target.PositionX - player.PositionX;
        var deltaY = (long)target.PositionY - player.PositionY;
        var nativeVisionRadius = player.LightRadius;
        if (deltaX * deltaX + deltaY * deltaY
            > (long)nativeVisionRadius * nativeVisionRadius)
            return false;
        return level.HasNativeVisionLineOfSight(
            player.PositionX,
            player.PositionY,
            target.PositionX,
            target.PositionY);
    }

    /** Keeps legacy int distance consumers fail-closed for malformed coordinates. */
    private static int ClampNativeDistance(long distance)
        => distance >= int.MaxValue ? int.MaxValue : (int)distance;

    /**
     * Native ProcessMonsters advances each monster's multiplayer AI seed once per tick.
     * Native-populated monsters first take multi.cpp MonsterSeeds: every game loop
     * reseeds slot i with ((loops >> 8) | (loops << 24)) + i. The server tick is the loop count.
     */
    private void AdvanceNativeMonsterAiSeed(AuthoritativeCombatTarget target, ulong tick)
    {
        if (gameMode != DiabloGameMode.Multiplayer || !target.UsesNativeRandomSeeds)
            return;

        if (target.NativeInitialization is { } native) {
            var loops = unchecked((uint)tick);
            target.MonsterAiSeed = unchecked(((loops >> 8) | (loops << 24)) + (uint)native.InitialState.Slot);
        }
        random.Restore(target.MonsterAiSeed);
        target.MonsterAiSeed = unchecked((uint)random.AdvanceRndSeed());
    }

    private bool TryAdvanceFallenFear(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (target.FallenFearTicks == 0
            || !GetFallenFearTuning(target).HasValue)
            return false;

        var usesNativeRandomWalk = gameMode == DiabloGameMode.SinglePlayer
            || target.UsesNativeRandomSeeds;
        (int X, int Y)? retreat;
        if (usesNativeRandomWalk) {
            // Native FallenAi uses RandomWalk while fear is active. The
            // process stream orders the two side-pair coin draws even when
            // the straight candidate is available; multiplayer restores the
            // per-monster stream before reaching this branch.
            retreat = FindNativeRandomWalkStepFromRandom(
                target,
                target.FallenFearDirection,
                players);
        } else {
            // Preserve the deterministic compatibility path for targets
            // without a native multiplayer stream.
            retreat = null;
            foreach (var candidateDirection in DeterministicGridDirections.EnumerateAlternating(
                         target.FallenFearDirection)) {
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
        if (retreat.HasValue)
            target.BeginMovement(retreat.Value.X, retreat.Value.Y);
        target.AdvanceFallenFear();
        return true;
    }

    /** Advances the native Fallen stand animation and admits its rally special. */
    private bool TryStartFallenRally(
        AuthoritativeCombatTarget target,
        ulong tick)
    {
        if (!UsesNativeAi(target, "Fallen")
            || target.HitPointsQ6 == 0
            || GetFallenStandAnimationTuning(target) is not { } tuning
            || !target.AdvanceFallenStandAnimation(
                tuning.Frames,
                tuning.TicksPerFrame))
            return false;

        var usesNativeRandom = gameMode == DiabloGameMode.SinglePlayer
            || target.UsesNativeRandomSeeds;
        var rallies = usesNativeRandom
            ? random.FlipCoin(4)
            : (unchecked((uint)tick) ^ target.EntityId) % 4U == 0U;
        target.ResetFallenStandAnimation();
        if (!rallies)
            return true;

        target.ApplyHealingQ6(
            DeterministicFixedPointQ6.FromWhole(
                checked((2 * target.Intelligence) + 2)));
        TriggerFallenRally(target);
        return true;
    }

    /** Native Fallen attack-goal pursuit after a successful rally special. */
    private bool TryAdvanceFallenRally(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (target.FallenRallyTicks == 0)
            return false;
        if (distance <= 1) {
            target.AdvanceFallenRally(forceAttack: true);
            return false;
        }

        var usesNativeRandom = gameMode == DiabloGameMode.SinglePlayer
            || target.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
        var pursuit = usesNativeRandom
            ? FindNativeRandomWalkStepFromRandom(target, victim, players)
            : FindNativeRandomWalkStep(target, victim, players, actionSeed);
        if (pursuit.HasValue)
            target.BeginMovement(pursuit.Value.X, pursuit.Value.Y);
        target.AdvanceFallenRally(forceAttack: false);
        return true;
    }

    private (int Frames, int TicksPerFrame)? GetFallenStandAnimationTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.StandAnimationFrames > 0
            && definition.StandAnimationTicksPerFrame > 0) {
            return (
                definition.StandAnimationFrames,
                definition.StandAnimationTicksPerFrame);
        }
        return UsesNativeAi(target, "Fallen")
            ? (11, 3)
            : null;
    }

    /** Native Bat retreat: move away once, then take one deterministic side step. */
    private bool TryAdvanceBatRetreat(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick)
    {
        var batTuning = GetBatAttackTuning(target);
        if (!batTuning.HasValue || target.BatRetreatTicks == 0)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.UsesNativeRandomSeeds;
        if (usesNativeAiStream) {
            // Native BatAi consumes its action roll before either retreat
            // branch. RandomWalk then consumes two side-order coin draws.
            _ = random.RandomIntLessThan(100);
            var nativeDirection = target.BatRetreatTicks == batTuning.Value.RetreatDurationTicks
                ? RotateDirection(target.BatRetreatDirection, 4)
                : RotateDirection(
                    target.BatRetreatDirection,
                    random.FlipCoin(2) ? 1 : -1);
            var retreat = FindNativeRandomWalkStepFromRandom(
                target,
                nativeDirection,
                players);
            if (retreat.HasValue)
                target.BeginMovement(retreat.Value.X, retreat.Value.Y);
            target.AdvanceBatRetreat();
            return true;
        }

        var direction = target.BatRetreatTicks == batTuning.Value.RetreatDurationTicks
            ? RotateDirection(target.BatRetreatDirection, 4)
            : RotateDirection(
                target.BatRetreatDirection,
                ((unchecked((uint)tick) ^ target.EntityId) & 1U) == 0 ? 1 : -1);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(target.PositionX + displacement.X);
        var candidateY = checked(target.PositionY + displacement.Y);
        if (world.IsWalkable(target.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                target.LevelId,
                candidateX,
                candidateY,
                players,
                target.EntityId)) {
            target.BeginMovement(candidateX, candidateY);
        }
        target.AdvanceBatRetreat();
        return true;
    }

    /** Native ButcherAi pursues through the server navigation boundary until adjacent. */
    private bool TryAdvanceButcherAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!UsesButcherPursuit(monster) || distance <= 1)
            return false;

        var actionSeed = unchecked((uint)monster.EntityId ^ victim.EntityId);
        var randomWalk = monster.UsesNativeRandomSeeds
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native ButcherAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Active monster content owns Butcher controller assignment; direct legacy targets keep the label fallback. */
    private bool UsesButcherPursuit(AuthoritativeCombatTarget monster)
        => UsesNativeAi(monster, "Butcher");

    /** Zhar and Lazarus retain the native CounselorAi controller after quest dialogue. */
    private bool UsesNativeCounselorAi(AuthoritativeCombatTarget monster)
        => UsesNativeAi(monster, "Counselor")
            || UsesNativeAi(monster, "Zhar")
            || UsesNativeAi(monster, "Lazarus");

    /** Active monster content owns fallback native-AI identity; direct targets retain the label path. */
    private bool UsesNativeAi(AuthoritativeCombatTarget monster, string nativeAi)
    {
        // Unique encounters carry an explicit runtime identity that must not
        // be replaced by an incomplete generic base-type catalog row.
        if (monster.IsUnique && !string.IsNullOrWhiteSpace(monster.NativeAi))
            return monster.NativeAi == nativeAi;
        if (monsterTypes?.TryGet(monster.MonsterId, out var definition) == true)
            return monster.NativeAi == nativeAi
                || !definition.HasCompleteAiTuning
                    && definition.NativeAi == nativeAi;
        return monster.NativeAi == nativeAi;
    }

    /** Native SkeletonAi admits movement through its 35 - 4 * intelligence band. */
    private bool TryAdvanceSkeletonAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (monster.FallenRallyAttackPending)
            return false;
        if (monster.AdvanceSkeletonMeleeDelay())
            return true;

        var skeletonTuning = GetSkeletonMeleeDelayTuning(monster);
        if (!skeletonTuning.HasValue || distance <= 1)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var delayBand = Math.Max(
            0,
            skeletonTuning.Value.ChanceBase
                - (skeletonTuning.Value.IntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll < delayBand) {
            monster.StartSkeletonMeleeDelay(
                usesNativeAiStream
                    ? ResolveSkeletonMeleeMovementDelayFromRandom(monster)
                    : ResolveSkeletonMeleeMovementDelay(monster, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native SkeletonAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Native AiAvoidance admits GoatMelee movement through its intelligence band. */
    private bool TryAdvanceGoatMeleeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var goatTuning = GetGoatMeleeMovementTuning(monster);
        if (!goatTuning.HasValue || distance <= 1)
            return false;
        if (monster.AdvanceAvoidanceDelay())
            return true;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
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
                victim,
                players,
                distance,
                actionRoll)
            : TryAdvanceAiAvoidanceMoveGoal(
                monster,
                victim,
                players,
                distance,
                actionSeed);
        if (advancedMoveGoal)
            return true;

        var movementChance = GetAiAvoidanceMovementChance(
            monster,
            goatTuning.Value.ChanceBase,
            goatTuning.Value.IntelligenceScale,
            GetAiAvoidanceMovementFreshChanceBonus(monster));
        if (actionRoll >= movementChance)
        {
            monster.AdvanceAvoidanceMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
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

    /** Native RoundWalk candidate order with deterministic turn state. */
    private (int X, int Y)? FindNativeRoundWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int? turnOverride = null)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnOverride.HasValue
            ? turnOverride.Value == 0 ? 1 : -1
            : (seed / 100U) % 2U == 0U ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        var turn90 = primaryDirections[0];
        var fallbackDirection = RotateDirection(turn90, 4);
        var fallbackTurn = (seed / 200U) % 2U == 0U ? 1 : -1;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            fallbackTurn,
            2);
        foreach (var candidateDirection in primaryDirections.Concat(fallbackDirections)) {
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

    /** Native RoundWalk candidate order driven by the restored monster stream. */
    private (int X, int Y)? FindNativeRoundWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        // Leoric initializes goalVar2 with GenerateRnd(2) when entering its
        // Move goal; the value selects the clockwise/counter-clockwise turn.
        var turn = random.RandomIntLessThan(2) == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }

        // RoundWalk falls back to RandomWalk in the opposite direction. Keep
        // its two native coin draws even when the fallback has no open tile.
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var fallbackTurn = random.FlipCoin(2) ? 1 : -1;
        foreach (var candidateDirection in DeterministicGridDirections.EnumerateTurnCandidates(
                     fallbackDirection,
                     fallbackTurn,
                     2)) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native HorkDemon RoundWalk using its persisted goalVar2 turn state. */
    private (int X, int Y)? FindNativeHorkRoundWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int goalTurn)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = goalTurn == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }

        // RoundWalk calls RandomWalk(Opposite(turn90)); RandomWalk consumes
        // both native coin draws before testing its five candidates.
        monster.UpdateHorkDemonMovementTurn(goalTurn == 0 ? 1 : 0);
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        return FindNativeRandomWalkStepFromRandom(monster, fallbackDirection, players);
    }

    /** Native OverlordAi admits Fat movement through its 20 + 4 * intelligence band. */
    private bool TryAdvanceFatAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var fatTuning = GetFatMovementTuning(monster);
        if (!fatTuning.HasValue || distance <= 1)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var movementChance = GetFatMovementChance(
            monster,
            fatTuning.Value.ChanceBase,
            fatTuning.Value.IntelligenceScale,
            GetFatMovementFreshChanceBonus(monster));
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= movementChance) {
            monster.AdvanceFatMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordFatMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
            return true;
        }

        monster.AdvanceFatMovementIdle();

        // Native OverlordAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Attempts the native Hork Spawn cell admission without consuming RNG. */
    private bool TryStartHorkDemonSpawn(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeMonsterMissileDefinition missile)
    {
        if (monsterSpawnPlanner is null || world.CombatTargets.Count >= 256)
            return false;

        // Native HorkDemonAi checks the monster's current direction for the
        // adjacent admission cell; it does not turn toward the victim before
        // starting Hork Spawn.
        var direction = monster.Direction;
        var displacement = DirectionDisplacements[direction];
        var positionX = checked(monster.PositionX + displacement.X);
        var positionY = checked(monster.PositionY + displacement.Y);
        if (!world.IsWalkable(monster.LevelId, positionX, positionY)
            || world.IsOccupied(
                monster.LevelId,
                positionX,
                positionY,
                players,
                monster.EntityId))
            return false;

        var projectileSpeed = missile.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : 8;
        var projectileLifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : 9;
        var velocity = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            victim.PositionX,
            victim.PositionY,
            projectileSpeed);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            victim.EntityId,
            missile.SpawnMonsterId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            victim.PositionX,
            victim.PositionY,
            0,
            AuthoritativeDamageType.Physical,
            0,
            checked((uint)projectileLifetime)) {
            Behavior = missile.ProjectileBehavior,
            NativeMissileId = missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic ? missile.MissileId : null,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = direction,
            Speed = projectileSpeed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        return true;
    }

    /** Native HorkDemonAi admission with server-owned Hork Spawn trajectory. */
    private bool TryAdvanceHorkDemonAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var horkDemonTuning = GetHorkDemonTuning(monster);
        if (!horkDemonTuning.HasValue)
            return false;
        if (monster.AdvanceHorkDemonDelay())
            return true;
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.HorkSpawn)
            return true;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == missile.ProjectileBehavior))
            return true;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        // HorkDemonAi always consumes GenerateRnd(100) before examining its
        // goal. Initialized targets use the native per-monster stream; the
        // zero-seed compatibility path remains deterministic and side-effect
        // free.
        var actionRoll = usesNativeAiStream
            ? random.GenerateRnd(100)
            : (int)(actionSeed % 100U);
        if (distance <= 1) {
            monster.ClearHorkDemonMovement();
            var attackChance = Math.Min(
                100,
                horkDemonTuning.Value.AttackChanceBase
                    + (horkDemonTuning.Value.AttackChanceIntelligenceScale
                        * monster.Intelligence));
            return actionRoll >= attackChance;
        }

        var roundWalkChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var horkDefinition) == true
            && horkDefinition.HorkDemonRoundWalkChanceModulo > 0) {
            roundWalkChanceModulo = horkDefinition.HorkDemonRoundWalkChanceModulo;
        }
        var spawnAdmitted = distance >= horkDemonTuning.Value.SpawnMinimumDistance
            && actionRoll < Math.Min(
                100,
                horkDemonTuning.Value.SpawnChanceBase
                    + (horkDemonTuning.Value.SpawnChanceIntelligenceScale
                        * monster.Intelligence));
        if (!usesNativeAiStream) {
            // Preserve the legacy zero-seed controller exactly: movement
            // admission precedes its far-distance RoundWalk choice, and a
            // failed spawn-cell probe falls through to that controller.
            if (spawnAdmitted && TryStartHorkDemonSpawn(monster, victim, players, missile))
                return true;

            var legacyMovementChance = Math.Min(
                100,
                horkDemonTuning.Value.MovementChanceBase
                    + (horkDemonTuning.Value.MovementChanceIntelligenceScale
                        * monster.Intelligence));
            if (actionRoll >= legacyMovementChance) {
                monster.StartHorkDemonDelay(ResolveHorkDemonDelay(missile, actionSeed));
                return true;
            }

            if (distance >= 5
                && (actionSeed / 100U) % (uint)roundWalkChanceModulo != 0U) {
                var roundWalk = FindNativeRoundWalkStep(
                    monster,
                    victim,
                    players,
                    actionSeed);
                if (roundWalk.HasValue) {
                    monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                    return true;
                }
            }

            var legacyRandomWalk = FindNativeRandomWalkStep(
                monster,
                victim,
                players,
                actionSeed);
            if (legacyRandomWalk.HasValue)
                monster.BeginMovement(legacyRandomWalk.Value.X, legacyRandomWalk.Value.Y);
            return true;
        }

        var hasMoveGoal = monster.HorkDemonMoveTicks >= 0;
        if (hasMoveGoal) {
            if (TryAdvanceHorkDemonRoundWalk(
                monster,
                victim,
                players,
                distance,
                usesNativeAiStream,
                actionSeed,
                missile))
                return true;
            // Native goalVar1 reached its limit and changed the goal back to
            // Normal. The already-consumed action roll is reused below.
        } else if (distance >= 5
            && (usesNativeAiStream
                ? !random.FlipCoin((uint)roundWalkChanceModulo)
                : (actionSeed / 100U) % (uint)roundWalkChanceModulo != 0U)) {
            monster.StartHorkDemonMovement(
                usesNativeAiStream
                    ? random.GenerateRnd(2)
                    : (int)((actionSeed / 100U) % 2U));
            if (TryAdvanceHorkDemonRoundWalk(
                monster,
                victim,
                players,
                distance,
                usesNativeAiStream,
                actionSeed,
                missile))
                return true;
        }

        if (spawnAdmitted) {
            if (TryStartHorkDemonSpawn(monster, victim, players, missile))
                return true;
            // Native's admitted ranged branch does not fall through to
            // movement when the spawn cell/capacity is unavailable.
            return true;
        }

        var movementChance = Math.Min(
            100,
            horkDemonTuning.Value.MovementChanceBase
                + (horkDemonTuning.Value.MovementChanceIntelligenceScale
                    * monster.Intelligence));
        var movementRoll = usesNativeAiStream
            ? random.GenerateRnd(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= movementChance) {
            var delay = usesNativeAiStream
                ? ResolveHorkDemonDelayFromRandom(missile)
                : ResolveHorkDemonDelay(missile, actionSeed);
            if (delay > 0)
                monster.StartHorkDemonDelay(delay);
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native HorkDemonAi consumes an admitted movement branch even when
        // every normal RandomWalk candidate is blocked.
        return true;
    }

    /** Executes one native HorkDemon Move-goal RoundWalk, including fallback delay. */
    private bool TryAdvanceHorkDemonRoundWalk(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance,
        bool usesNativeAiStream,
        uint actionSeed,
        AuthoritativeMonsterMissileDefinition missile)
    {
        var sameTransparencyRegion = world.World?.Levels
            .FirstOrDefault(level => level.LevelId == monster.LevelId)
            ?.IsSameTransparencyRegion(
                monster.PositionX,
                monster.PositionY,
                victim.PositionX,
                victim.PositionY)
            ?? true;
        if (!monster.AdvanceHorkDemonMovement(distance, sameTransparencyRegion))
            return false;

        var roundWalk = usesNativeAiStream
            ? FindNativeHorkRoundWalkStepFromRandom(
                monster,
                victim,
                players,
                monster.HorkDemonMoveTurn)
            : FindNativeRoundWalkStep(
                monster,
                victim,
                players,
                actionSeed,
                monster.HorkDemonMoveTurn);
        if (roundWalk.HasValue) {
            monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
            return true;
        }

        var delay = usesNativeAiStream
            ? ResolveHorkDemonDelayFromRandom(missile)
            : ResolveHorkDemonDelay(missile, actionSeed);
        if (delay > 0)
            monster.StartHorkDemonDelay(delay);
        return true;
    }

    /** Native MegaAi delegates to SkeletonAi beyond its Inferno range. */
    private bool TryAdvanceMegaAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!UsesNativeAi(monster, "Mega"))
            return false;
        if (monsterMissiles is not null) {
            if (!TryGetMonsterMissile(monster, out var missile)
                || missile.ProjectileBehavior
                    != AuthoritativeProjectileBehavior.InfernoController)
                return false;
            if (distance < ResolveMonsterInfernoTuning(missile).MaximumDistance)
                return false;
        } else if (distance < 5) {
            return false;
        }

        if (distance <= 1)
            return false;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        if (monster.AdvanceSkeletonMeleeDelay())
            return true;
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.UsesNativeRandomSeeds;
        var delayChanceBase = 35;
        var delayChanceIntelligenceScale = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var megaDefinition) == true
            && megaDefinition.MegaDelayChanceBase > 0
            && megaDefinition.MegaDelayChanceIntelligenceScale > 0) {
            delayChanceBase = megaDefinition.MegaDelayChanceBase;
            delayChanceIntelligenceScale = megaDefinition.MegaDelayChanceIntelligenceScale;
        }
        var delayBand = Math.Max(
            0,
            delayChanceBase - (delayChanceIntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll < delayBand) {
            monster.StartSkeletonMeleeDelay(
                usesNativeAiStream
                    ? ResolveSkeletonMeleeMovementDelayFromRandom(monster)
                    : ResolveSkeletonMeleeMovementDelay(monster, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native MegaAi delegates to SkeletonAi here; a blocked RandomWalk
        // consumes the tick rather than invoking bounded generic navigation.
        return true;
    }

    /** Native Mega normal-state fallback after an Inferno admission miss. */
    private bool TryAdvanceMegaIntermediateMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        bool usesNativeAiStream = false)
    {
        if (!UsesNativeAi(monster, "Mega") || distance < 2)
            return false;

        var movementChanceBase = 50;
        var movementChanceIntelligenceScale = 10;
        if (monsterTypes?.TryGet(monster.MonsterId, out var megaDefinition) == true
            && megaDefinition.MegaIntermediateMovementChanceBase > 0
            && megaDefinition.MegaIntermediateMovementChanceIntelligenceScale > 0) {
            movementChanceBase = megaDefinition.MegaIntermediateMovementChanceBase;
            movementChanceIntelligenceScale = megaDefinition.MegaIntermediateMovementChanceIntelligenceScale;
        }
        var movementChance = Math.Min(
            100,
            movementChanceBase
                + (movementChanceIntelligenceScale * monster.Intelligence));
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)((seed / 100U) % 100U);
        if (movementRoll >= movementChance)
            return false;

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, seed);
        if (!randomWalk.HasValue)
            return false;
        monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
        return true;
    }
}
