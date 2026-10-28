using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Player actions and melee/bow attacks (native player.cpp StartAttack/DoAttack/DoRangeAttack). */
public sealed partial class DiabloCombatCommandProcessor
{
    /** Returns true only when a modeled primary action actually reaches completion. */
    private bool AdvancePlayerAction(
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var action = player.CurrentAction;
        if (action is null)
            return false;

        if (!action.EffectApplied && tick >= action.EffectTick) {
            switch (action.Kind) {
            case DiabloPlayerActionKind.Move:
                var destinationX = checked(player.PositionX + action.DirectionX);
                var destinationY = checked(player.PositionY + action.DirectionY);
                if (world.IsWalkable(player.LevelId, destinationX, destinationY)
                    && !world.IsOccupied(
                        player.LevelId,
                        destinationX,
                        destinationY,
                        players,
                        player.EntityId)) {
                    player.PositionX = destinationX;
                    player.PositionY = destinationY;
                    if (worldCommands is not null) {
                        AddDirectedEvents(
                            player,
                            worldCommands.ExecuteOccupancyTriggers(player, players),
                            events);
                        AddDirectedEvents(
                            player,
                            worldCommands.ExecuteLevelTrigger(player, tick, players),
                            events);
                    }
                }
                break;
            case DiabloPlayerActionKind.Attack:
                AddDirectedEvents(
                    player,
                    action.TargetCell
                        ? ResolveAttackAt(
                            player,
                            action.TargetX,
                            action.TargetY,
                            action.StartedAtTick,
                            player.CombatMode == DiabloPlayerCombatMode.RangedAttack,
                            players,
                            effectTick: tick)
                        : player.CombatMode == DiabloPlayerCombatMode.RangedAttack
                            && world.OwnsNativePopulationLevel(player.LevelId)
                            && (action.TargetX != 0 || action.TargetY != 0)
                            ? ResolveNativeWeaponArrowVolley(
                                player,
                                action.TargetEntityId,
                                action.TargetX,
                                action.TargetY,
                                tick)
                            : ResolveAttack(
                                player,
                                action.TargetEntityId,
                                action.StartedAtTick,
                                player.CombatMode == DiabloPlayerCombatMode.RangedAttack,
                                players,
                                effectTick: tick),
                    events);
                break;
            case DiabloPlayerActionKind.Cast:
                AddDirectedEvents(
                    player,
                    ResolveCast(
                        player,
                        action.CastIntent
                            ?? throw new InvalidOperationException(
                                "A timed cast action has no cast intent."),
                        appliedTick: tick,
                        players: players),
                    events);
                break;
            default:
                throw new InvalidOperationException("A timed player action has an invalid kind.");
            }
            action.EffectApplied = true;
        }

        if (ReferenceEquals(player.CurrentAction, action) && tick >= action.EndTick) {
            var completed = action.Kind is DiabloPlayerActionKind.Move
                or DiabloPlayerActionKind.Attack
                or DiabloPlayerActionKind.Cast;
            DiabloPlayerActionLifecycle.Cancel(player);
            if (player.CombatMode == DiabloPlayerCombatMode.Death) {
                player.CombatModeEndTick = 0;
            } else {
                player.CombatMode = DiabloPlayerCombatMode.Stand;
                player.CombatModeEndTick = 0;
            }
            return completed;
        }
        return false;
    }

    private static void AddDirectedEvents(
        AuthoritativePlayerState player,
        DiabloCombatIntentResult result,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var gameplayEvent in result.Events) {
            events.Add(new DiabloDirectedGameplayEvent(player, gameplayEvent));
        }
    }

    private static void AddDirectedEvents(
        AuthoritativePlayerState player,
        DiabloWorldIntentResult result,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var gameplayEvent in result.Events) {
            events.Add(new DiabloDirectedGameplayEvent(player, gameplayEvent));
        }
    }

    private DiabloCombatIntentResult Attack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        bool? includesFirstFrameOverride = null)
    {
        if (world.Objects.TryGetValue(targetEntityId, out var barrel)
            && barrel.LevelId == player.LevelId
            && world.CanBreakNativeBarrel(barrel)
            && Math.Max(Math.Abs(barrel.PositionX - player.PositionX),
                Math.Abs(barrel.PositionY - player.PositionY)) == 1
            && GetAttackAnimationProfile(player) != DiabloPlayerAttackAnimationProfile.Bow) {
            if (useNativePlayerActionTiming && !DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            return AttackAt(player, barrel.PositionX, barrel.PositionY, appliedTick, players);
        }
        if (!useNativePlayerActionTiming)
            return ResolveAttack(
                player,
                targetEntityId,
                appliedTick,
                ranged: GetAttackAnimationProfile(player)
                    == DiabloPlayerAttackAnimationProfile.Bow,
                players: players);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var profile = GetAttackAnimationProfile(player);
        var timing = animation.GetAttack(profile);
        var ranged = profile == DiabloPlayerAttackAnimationProfile.Bow;
        var nativePopulationBow = ranged
            && world.OwnsNativePopulationLevel(player.LevelId);
        AuthoritativeCombatTarget? target = null;
        AuthoritativePlayerState? playerTarget = null;
        if (!TryGetAttackTarget(
                player,
                targetEntityId,
                ranged,
                out target,
                requireMissileLineOfSight: !nativePopulationBow)
            && !TryGetPlayerAttackTarget(
                player,
                targetEntityId,
                ranged,
                players,
                out playerTarget))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var hasNativeAim = nativePopulationBow && target is not null;
        var targetX = hasNativeAim
            ? target!.FuturePositionX
            : target?.PositionX ?? playerTarget!.PositionX;
        var targetY = hasNativeAim
            ? target!.FuturePositionY
            : target?.PositionY ?? playerTarget!.PositionY;
        UpdateFacingToward(player, targetX, targetY);
        var includesFirstFrame = includesFirstFrameOverride ?? GetIncludesFirstFrame(player);
        var skippedFrames = GetAttackSkippedFrames(
            player,
            ranged,
            includesFirstFrame);
        var effectTick = checked(
            appliedTick + (ulong)(timing.ActionFrame - 1 - skippedFrames));
        var endTick = checked(appliedTick + (ulong)(timing.Frames - 1 - skippedFrames));
        var readyTick = Math.Min(
            endTick,
            checked(appliedTick + (ulong)(timing.ActionFrame - skippedFrames)));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Attack,
                appliedTick,
                effectTick,
                readyTick,
                endTick) {
                TargetEntityId = targetEntityId,
                TargetX = hasNativeAim ? targetX : 0,
                TargetY = hasNativeAim ? targetY : 0,
            },
            ranged
                ? DiabloPlayerCombatMode.RangedAttack
                : DiabloPlayerCombatMode.Attack);
        return DiabloCombatIntentResult.Accepted;
    }

    /** Admits a native CMD_*ATTACKXY-compatible destination-cell action. */
    private DiabloCombatIntentResult AttackAt(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        bool? includesFirstFrameOverride = null)
    {
        if (!world.ContainsPosition(player.LevelId, targetX, targetY))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        UpdateFacingToward(player, targetX, targetY);
        if (!useNativePlayerActionTiming)
            return ResolveAttackAt(
                player,
                targetX,
                targetY,
                appliedTick,
                GetAttackAnimationProfile(player) == DiabloPlayerAttackAnimationProfile.Bow,
                players);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var profile = GetAttackAnimationProfile(player);
        var timing = animation.GetAttack(profile);
        var ranged = profile == DiabloPlayerAttackAnimationProfile.Bow;
        var includesFirstFrame = includesFirstFrameOverride ?? GetIncludesFirstFrame(player);
        var skippedFrames = GetAttackSkippedFrames(player, ranged, includesFirstFrame);
        var effectTick = checked(
            appliedTick + (ulong)(timing.ActionFrame - 1 - skippedFrames));
        var endTick = checked(appliedTick + (ulong)(timing.Frames - 1 - skippedFrames));
        var readyTick = Math.Min(
            endTick,
            checked(appliedTick + (ulong)(timing.ActionFrame - skippedFrames)));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Attack,
                appliedTick,
                effectTick,
                readyTick,
                endTick) {
                TargetCell = true,
                TargetX = targetX,
                TargetY = targetY,
            },
            ranged
                ? DiabloPlayerCombatMode.RangedAttack
                : DiabloPlayerCombatMode.Attack);
        return DiabloCombatIntentResult.Accepted;
    }

    private DiabloCombatIntentResult ResolveAttack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick,
        bool ranged = false,
        IReadOnlyList<AuthoritativePlayerState>? players = null,
        ulong? effectTick = null)
    {
        players ??= [player];
        if (!TryGetAttackTarget(player, targetEntityId, ranged, out var target)) {
            if (TryGetPlayerAttackTarget(
                    player,
                    targetEntityId,
                    ranged,
                    players,
                    out var playerTarget)) {
                if (ranged)
                    return ResolvePlayerArrowVolley(
                        player,
                        playerTarget,
                        appliedTick);
                return ResolvePlayerAttack(
                    player,
                    playerTarget,
                    appliedTick,
                    players,
                    effectTick ?? appliedTick);
            }
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        UpdateFacingToward(player, target.PositionX, target.PositionY);
        if (ranged) {
            var arrowCount = HasUsableEquipmentFlag(
                player,
                AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag)
                ? rules.MultipleArrowsCount
                : 1;
            return ResolveWeaponArrowVolley(
                player,
                target,
                appliedTick,
                arrowCount);
        }
        var events = new List<DiabloGameplayEvent>();
        var attackHit = ResolveMonsterPlayerAttack(
            player,
            target,
            appliedTick,
            players,
            adjacentDamage: false,
            seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId),
            applyElemental: true,
            events);
        attackHit |= ResolveCleaveSideTargets(
            player,
            players,
            appliedTick,
            target.EntityId,
            events);
        if (ranged || attackHit) {
            var hellfireRemoved = ApplyHellfireWeaponDecay(player);
            if (!hellfireRemoved)
                ApplyNativeWeaponDurabilityLoss(player, ranged ? 40U : 30U);
        }
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /** Resolves one native monster-target physical hit, including shared effects. */
    private bool ResolveMonsterPlayerAttack(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        bool adjacentDamage,
        uint seed,
        bool applyElemental,
        ICollection<DiabloGameplayEvent> events)
    {
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY));
        var gargoyleWake = string.Equals(
                target.NativeAi,
                "Gargoyle",
                StringComparison.Ordinal)
            && IsNativeGargoyleWakePending(target);
        var damageQ6 = ResolvePlayerAttackDamageQ6(
            player,
            target,
            monsterTarget: true,
            ranged: false,
            deferGargoyleWake: false,
            targetDistance: targetDistance,
            adjacentDamage: adjacentDamage,
            seed: seed,
            out var perilSelfDamageQ6,
            out var perilActive,
            out var doppelgangerProc,
            out var attackHit);
        if (gargoyleWake)
            attackHit = true;
        if (doppelgangerProc)
            SpawnDoppelganger(target, players);
        if (perilActive) {
            var damageResult = playerDamage.Apply(player, perilSelfDamageQ6);
            if (damageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (player.Life == 0) {
                StartPlayerDeath(
                    player,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }
        ApplyResolvedDamageQ6(player, target, damageQ6, events);
        // Native DoAttack emits melee WeaponExplosion missiles before
        // PlrHitMonst resolves physical admission. FireDamage and
        // LightningDamage therefore still reach a live monster when the
        // physical roll misses; ranged arrow channels remain captured only
        // for admitted arrows at launch.
        if (applyElemental
            && !gargoyleWake
            && target.HitPointsQ6 > 0)
            ApplyEquipmentElementalDamage(player, target, events, ranged: false);
        ApplyEquipmentKnockback(player, target, damageQ6, players);
        if (damageQ6 > 0)
            ApplyEquipmentAttackEffects(player, damageQ6, events);
        return attackHit;
    }

    /** Returns the native side monsters affected by a class Cleave attack. */
    private IReadOnlyList<AuthoritativeCombatTarget> FindCleaveTargets(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint primaryTargetEntityId = 0)
    {
        var targets = new List<AuthoritativeCombatTarget>(2);
        var facing = (int)player.FacingDirection;
        foreach (var offset in new[] { 1, -1 }) {
            var direction = DeterministicGridDirections.Rotate(facing, offset);
            var displacement = DirectionDisplacements[direction];
            var positionX = checked(player.PositionX + displacement.X);
            var positionY = checked(player.PositionY + displacement.Y);
            var sideTarget = world.CombatTargets.Values
                .Where(candidate => candidate.EntityId != primaryTargetEntityId
                    && candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetAttackTarget(player, candidate.EntityId, ranged: false, out _));
            if (sideTarget is not null)
                targets.Add(sideTarget);
        }
        return targets;
    }

    /** Mirrors Player::CanCleave using validated class and equipment identities. */
    private bool CanPlayerCleave(AuthoritativePlayerState player)
    {
        var playerClass = GetPlayerClass(player);
        if (playerClass is null)
            return false;
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        var hasStaff = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsStaff(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsStaff(right.State, items);
        var hasShield = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsShield(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsShield(right.State, items);
        var hasSwordLeft = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsSword(left.State, items);
        var hasSwordRight = right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsSword(right.State, items);
        var hasAxe = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsAxe(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsAxe(right.State, items);
        var hasTwoHandedMaceOrSword = left is not null
            && IsUsable(left.State)
            && AuthoritativeItemClassification.IsTwoHanded(left.State, items)
            && (AuthoritativeItemClassification.IsMace(left.State, items)
                || AuthoritativeItemClassification.IsSword(left.State, items));
        return playerClass.PhysicalDamageFormula switch {
            AuthoritativePhysicalDamageFormula.Monk => hasStaff,
            AuthoritativePhysicalDamageFormula.Bard => hasSwordLeft && hasSwordRight,
            AuthoritativePhysicalDamageFormula.Barbarian => hasAxe
                || (!hasShield && hasTwoHandedMaceOrSword),
            _ => false,
        };
    }

    /** Resolves a cell action against the first authoritative occupant on its native attack path. */
    private DiabloCombatIntentResult ResolveAttackAt(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        ulong appliedTick,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong? effectTick = null)
    {
        if (ranged && world.SupportsNativeTargetCellPathing
            && world.OwnsNativePopulationLevel(player.LevelId))
            return ResolveNativeWeaponArrowVolley(player, 0, targetX, targetY, effectTick ?? appliedTick);
        var targetEntityId = FindCellAttackTarget(
            player,
            targetX,
            targetY,
            ranged,
            players);
        // Native cell attacks still perform their animation when the lane is
        // empty. A melee object hit and the two native Cleave lanes still
        // resolve at the same action boundary as a monster hit.
        if (targetEntityId == 0) {
            var events = new List<DiabloGameplayEvent>();
            var attackHit = false;
            if (!ranged
                && TryGetNativeMeleeBarrel(player, targetX, targetY, out var nativeBarrel)) {
                attackHit = TryBreakNativeMeleeBarrel(player, nativeBarrel!, appliedTick, players, events);
            } else if (!ranged
                && TryGetMeleeBreakableObject(
                    player,
                    targetX,
                    targetY,
                    out var breakableObject)
                && breakableObject is not null) {
                breakableObject.Destroyed = true;
                attackHit = true;
            }
            if (!ranged) {
                attackHit |= ResolveCleaveSideTargets(
                    player,
                    players,
                    appliedTick,
                    primaryTargetEntityId: 0,
                    events);
            }
            if (attackHit) {
                var hellfireRemoved = ApplyHellfireWeaponDecay(player);
                if (!hellfireRemoved)
                    ApplyNativeWeaponDurabilityLoss(player, 30);
            }
            return events.Count == 0
                ? DiabloCombatIntentResult.Accepted
                : new DiabloCombatIntentResult(true, null, events);
        }
        return ResolveAttack(
            player,
            targetEntityId,
            appliedTick,
            ranged,
            players,
            effectTick);
    }

    /** Resolves both native Cleave side lanes after any melee primary target. */
    private bool ResolveCleaveSideTargets(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong appliedTick,
        uint primaryTargetEntityId,
        ICollection<DiabloGameplayEvent> events)
    {
        if (!CanPlayerCleave(player))
            return false;

        var attackHit = false;
        var sideIndex = 0U;
        foreach (var sideTarget in FindCleaveTargets(
                     player,
                     players,
                     primaryTargetEntityId)) {
            attackHit |= ResolveMonsterPlayerAttack(
                player,
                sideTarget,
                appliedTick,
                players,
                adjacentDamage: true,
                seed: unchecked(
                    (uint)appliedTick
                    ^ sideTarget.EntityId
                    ^ player.EntityId
                    ^ ++sideIndex),
                applyElemental: true,
                events);
        }
        return attackHit;
    }

    private uint FindCellAttackTarget(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var steps = ranged
            ? DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                player.PositionX,
                player.PositionY)
            : 1;
        for (var step = 1; step <= steps; step++) {
            var positionX = checked(player.PositionX + displacement.X * step);
            var positionY = checked(player.PositionY + displacement.Y * step);
            var monster = world.CombatTargets.Values
                .Where(candidate =>
                    candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetAttackTarget(player, candidate.EntityId, ranged, out _));
            if (monster is not null)
                return monster.EntityId;

            var playerTarget = players
                .Where(candidate =>
                    candidate.EntityId != player.EntityId
                    && candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetPlayerAttackTarget(
                        player,
                        candidate.EntityId,
                        ranged,
                        players,
                        out _));
            if (playerTarget is not null)
                return playerTarget.EntityId;
        }
        return 0;
    }

    /** Returns the native adjacent breakable object selected by a melee cell attack. */
    private bool TryGetMeleeBreakableObject(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        out AuthoritativeWorldObject? breakableObject)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var positionX = checked(player.PositionX + displacement.X);
        var positionY = checked(player.PositionY + displacement.Y);
        breakableObject = world.Objects.Values
            .Where(@object => @object.LevelId == player.LevelId
                && @object.PositionX == positionX
                && @object.PositionY == positionY
                && world.CanBreakWorldObject(@object)
                && !@object.Activated
                && !@object.Destroyed)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
        return breakableObject is not null;
    }

    /** Emits server-owned weapon arrows targeting another player. */
    private DiabloCombatIntentResult ResolvePlayerArrowVolley(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        ulong appliedTick)
    {
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY));
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            target.PositionX,
            target.PositionY);
        var directionDisplacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var perpendicular = (-directionDisplacement.Y, directionDisplacement.X);
        var targetProxy = CreatePlayerCombatTargetProxy(target);
        var arrowCount = HasUsableEquipmentFlag(
            player,
            AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag)
            ? rules.MultipleArrowsCount
            : 1;
        for (var arrowIndex = 0; arrowIndex < arrowCount; arrowIndex++) {
            var damageQ6 = ResolvePlayerAttackDamageQ6(
                player,
                targetProxy,
                monsterTarget: false,
                ranged: true,
                deferGargoyleWake: false,
                targetDistance: targetDistance,
                adjacentDamage: false,
                seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId ^ (uint)arrowIndex),
                out var perilSelfDamageQ6,
                out var perilActive,
                out var doppelgangerProc,
                out var attackHit);
            var elemental = attackHit
                ? AuthoritativeEquipmentElementalDamageRules.Resolve(
                    player.Equipment,
                    random.GenerateRnd,
                    ranged: true)
                : default;
            var spread = arrowCount == 1 || arrowIndex == 0
                ? (X: 0, Y: 0)
                : (X: checked(
                    perpendicular.Item1
                    * (arrowIndex == 1 ? -1 : 1)
                    * rules.MultipleArrowsSpreadDistance),
                    Y: checked(
                        perpendicular.Item2
                        * (arrowIndex == 1 ? -1 : 1)
                        * rules.MultipleArrowsSpreadDistance));
            var arrowPositionX = checked(player.PositionX + spread.X);
            var arrowPositionY = checked(player.PositionY + spread.Y);
            if (!world.ContainsPosition(player.LevelId, arrowPositionX, arrowPositionY)) {
                arrowPositionX = player.PositionX;
                arrowPositionY = player.PositionY;
            }
            var (arrowSpeed, arrowLifetime) = ResolveWeaponArrowTravelTiming(
                player,
                targetDistance);
            var projectile = new AuthoritativeProjectile(
                AllocateProjectileEntityId(),
                player.EntityId == 0 ? 1U : player.EntityId,
                target.EntityId,
                0,
                player.LevelId,
                arrowPositionX,
                arrowPositionY,
                target.PositionX,
                target.PositionY,
                DeterministicFixedPointQ6.ToWholeTruncated(Math.Max(0, damageQ6)),
                AuthoritativeDamageType.Physical,
                0,
                arrowLifetime,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                DamageQ6 = Math.Max(0, damageQ6),
                ElementalFireDamage = elemental.Fire,
                ElementalLightningDamage = elemental.Lightning,
                AttackHit = attackHit,
                PerilSelfDamageQ6 = perilActive ? perilSelfDamageQ6 : 0,
                DoppelgangerProc = doppelgangerProc,
                ActivationDelayTicks = arrowIndex == 0
                    ? 0U
                    : checked((uint)rules.MultipleArrowsDelayTicks),
                Speed = arrowSpeed,
            };
            world.Projectiles.Add(projectile.EntityId, projectile);
            if (!ApplyHellfireWeaponDecay(player))
                ApplyNativeWeaponDurabilityLoss(player, 40);
        }
        return DiabloCombatIntentResult.Accepted;
    }

    private AuthoritativeCombatTarget CreatePlayerCombatTargetProxy(
        AuthoritativePlayerState target)
    {
        var targetClass = GetPlayerClass(target);
        return new AuthoritativeCombatTarget(
            target.EntityId,
            target.PositionX,
            target.PositionY,
            target.Life,
            armorClass: target.ArmorClass,
            maxHitPoints: target.LifeMaximum,
            levelId: target.LevelId,
            fireResistance: GetPlayerResistance(
                target,
                targetClass,
                AuthoritativeDamageType.Fire),
            lightningResistance: GetPlayerResistance(
                target,
                targetClass,
                AuthoritativeDamageType.Lightning),
            unique: true);
    }

    /** Resolves the initial authoritative melee path against another player. */
    private DiabloCombatIntentResult ResolvePlayerAttack(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong effectTick)
    {
        UpdateFacingToward(player, target.PositionX, target.PositionY);
        var targetProxy = CreatePlayerCombatTargetProxy(target);
        var damageQ6 = ResolvePlayerAttackDamageQ6(
            player,
            targetProxy,
            monsterTarget: false,
            ranged: false,
            deferGargoyleWake: false,
            targetDistance: 1,
            adjacentDamage: false,
            seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId),
            out var perilSelfDamageQ6,
            out var perilActive,
            out _,
            out var attackHit);
        var events = new List<DiabloGameplayEvent>();
        if (perilActive) {
            var selfDamageResult = playerDamage.Apply(player, perilSelfDamageQ6);
            if (selfDamageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        selfDamageResult.IncomingDamageQ6)) {
                    AmountQ6 = selfDamageResult.IncomingDamageQ6,
                });
            }
            if (player.Life == 0) {
                StartPlayerDeath(
                    player,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }

        var primaryBlocked = attackHit
            && TryResolvePlayerBlock(player, target, effectTick);
        if (!primaryBlocked) {
            var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
            if (damageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (target.Life == 0) {
                StartPlayerDeath(
                    target,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.Player);
            }
            if (attackHit && target.Life > 0)
                ApplyPlayerElementalDamage(
                    player,
                    target,
                    targetProxy,
                    appliedTick,
                    players,
                    events);
            ApplyPlayerKnockback(player, target, damageQ6, players);
            if (damageQ6 > 0)
                ApplyPlayerAttackEffects(player, damageQ6, events);
        }

        attackHit |= ResolveCleaveSideTargets(
            player,
            players,
            appliedTick,
            target.EntityId,
            events);
        if (attackHit) {
            var hellfireRemoved = ApplyHellfireWeaponDecay(player);
            if (!hellfireRemoved)
                ApplyNativeWeaponDurabilityLoss(player, 30);
        }
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /**
     * Emits server-owned bow missiles. Multiple Arrows uses the validated
     * content count; the first starts on the attack effect frame and subsequent
     * spread arrows begin after the configured delay and use the configured
     * perpendicular offset, matching DoRangeAttack's second firing window.
     */
    private DiabloCombatIntentResult ResolveWeaponArrowVolley(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ulong appliedTick,
        int arrowCount)
    {
        return ResolveWeaponArrowVolley(
            player,
            target,
            target.EntityId,
            target.PositionX,
            target.PositionY,
            appliedTick,
            arrowCount);
    }

    private DiabloCombatIntentResult ResolveNativeWeaponArrowVolley(
        AuthoritativePlayerState player,
        uint targetEntityId,
        int targetX,
        int targetY,
        ulong appliedTick)
    {
        if (!world.OwnsNativePopulationLevel(player.LevelId))
            throw new InvalidOperationException(
                "A frozen native weapon-aim attack requires a native-owned level.");
        var arrowCount = HasUsableEquipmentFlag(
            player,
            AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag)
            ? rules.MultipleArrowsCount
            : 1;
        return ResolveWeaponArrowVolley(
            player,
            null,
            targetEntityId,
            targetX,
            targetY,
            appliedTick,
            arrowCount);
    }

    private DiabloCombatIntentResult ResolveWeaponArrowVolley(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget? target,
        uint targetEntityId,
        int targetX,
        int targetY,
        ulong appliedTick,
        int arrowCount)
    {
        if (arrowCount is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(arrowCount));
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            targetX,
            targetY,
            player.PositionX,
            player.PositionY));
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var directionDisplacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var perpendicular = (-directionDisplacement.Y, directionDisplacement.X);
        var nativePopulationArrow = world.OwnsNativePopulationLevel(player.LevelId);
        for (var arrowIndex = 0; arrowIndex < arrowCount; arrowIndex++) {
            var damageQ6 = 0;
            var perilSelfDamageQ6 = 0;
            var perilActive = false;
            var doppelgangerProc = false;
            var attackHit = false;
            if (!nativePopulationArrow) {
                var damageTarget = target
                    ?? throw new InvalidOperationException(
                        "A non-native weapon arrow requires a live combat target.");
                damageQ6 = ResolvePlayerAttackDamageQ6(
                    player,
                    damageTarget,
                    monsterTarget: true,
                    ranged: true,
                    deferGargoyleWake: true,
                    targetDistance: targetDistance,
                    adjacentDamage: false,
                    seed: unchecked((uint)appliedTick ^ damageTarget.EntityId ^ player.EntityId ^ (uint)arrowIndex),
                    out perilSelfDamageQ6,
                    out perilActive,
                    out doppelgangerProc,
                    out attackHit);
            }
            var elemental = attackHit
                ? AuthoritativeEquipmentElementalDamageRules.Resolve(
                    player.Equipment,
                    random.GenerateRnd,
                    ranged: true)
                : default;
            var spread = arrowCount == 1 || arrowIndex == 0
                ? (X: 0, Y: 0)
                : (X: checked(
                    perpendicular.Item1
                    * (arrowIndex == 1 ? -1 : 1)
                    * rules.MultipleArrowsSpreadDistance),
                    Y: checked(
                        perpendicular.Item2
                        * (arrowIndex == 1 ? -1 : 1)
                        * rules.MultipleArrowsSpreadDistance));
            var arrowPositionX = checked(player.PositionX + spread.X);
            var arrowPositionY = checked(player.PositionY + spread.Y);
            if (!world.ContainsPosition(player.LevelId, arrowPositionX, arrowPositionY)) {
                arrowPositionX = player.PositionX;
                arrowPositionY = player.PositionY;
            }
            var (arrowSpeed, arrowLifetime) = ResolveWeaponArrowTravelTiming(
                player,
                targetDistance);
            var arrowRemainingTicks = arrowLifetime;
            var arrowVelocityX = 0;
            var arrowVelocityY = 0;
            var arrowDirection = 0;
            if (nativePopulationArrow) {
                var nativeSpeed = arrowSpeed == 0 ? 32 : arrowSpeed;
                var nativeTargetX = targetX;
                var nativeTargetY = targetY;
                if (nativeTargetX == arrowPositionX && nativeTargetY == arrowPositionY) {
                    nativeTargetX += directionDisplacement.X;
                    nativeTargetY += directionDisplacement.Y;
                }
                (arrowVelocityX, arrowVelocityY) = CalculateVelocity(
                    arrowPositionX,
                    arrowPositionY,
                    nativeTargetX,
                    nativeTargetY,
                    nativeSpeed);
                arrowRemainingTicks = 256;
                arrowSpeed = nativeSpeed;
                arrowDirection = (int)direction;
            }
            var projectile = new AuthoritativeProjectile(
                AllocateProjectileEntityId(),
                player.EntityId == 0 ? 1U : player.EntityId,
                targetEntityId,
                0,
                player.LevelId,
                arrowPositionX,
                arrowPositionY,
                targetX,
                targetY,
                DeterministicFixedPointQ6.ToWholeTruncated(Math.Max(0, damageQ6)),
                AuthoritativeDamageType.Physical,
                0,
                arrowRemainingTicks,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                DamageQ6 = Math.Max(0, damageQ6),
                ElementalFireDamage = elemental.Fire,
                ElementalLightningDamage = elemental.Lightning,
                AttackHit = attackHit,
                PerilSelfDamageQ6 = perilActive ? perilSelfDamageQ6 : 0,
                DoppelgangerProc = doppelgangerProc,
                NativeWeaponArrow = nativePopulationArrow
                    ? new AuthoritativeNativeWeaponArrowState(0)
                    : null,
                VelocityX = arrowVelocityX,
                VelocityY = arrowVelocityY,
                OriginalDirection = arrowDirection,
                ActivationDelayTicks = arrowIndex == 0
                    ? 0U
                    : checked((uint)rules.MultipleArrowsDelayTicks),
                Speed = arrowSpeed,
            };
            world.Projectiles.Add(projectile.EntityId, projectile);
            if (!ApplyHellfireWeaponDecay(player))
                ApplyNativeWeaponDurabilityLoss(player, 40);
        }
        return DiabloCombatIntentResult.Accepted;
    }
}
