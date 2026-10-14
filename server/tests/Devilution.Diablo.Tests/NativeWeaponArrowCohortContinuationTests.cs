using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeWeaponArrowImpactAndMissContinuationMatchAfterCohortRestore()
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimeTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
            || string.IsNullOrWhiteSpace(footprintTablePath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
            return;
        }

        using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
        var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var smith = Assert.Single(activation.TownerGreetings!.Definitions,
            definition => definition.TownerId == 0);
        var start = new[] {
            (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1),
        }.Select(offset => (X: smith.PositionX + offset.X, Y: smith.PositionY + offset.Y))
            .First(position => world.IsWalkable(smith.LevelId, position.X, position.Y)
                && !world.IsOccupied(smith.LevelId, position.X, position.Y,
                    Array.Empty<AuthoritativePlayerState>()));
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-arrow-cohort-continuation",
            rogue.ClassId,
            goldOverride: 100,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: smith.LevelId,
            startingPositionX: start.X,
            startingPositionY: start.Y);
        var runtime = simulation.CreateRuntime(template);
        const string sessionId = "native-arrow-cohort-continuation";
        runtime.Write(sessionId, player => player.EntityId = 0x70000071);
        var playerState = runtime.Read(sessionId, player => player);

        void UseHealingPotionIfNeeded()
        {
            var maximumLife = Math.Max(playerState.LifeMaximum, 0);
            if (playerState.Life <= 0 || maximumLife == 0
                || (long)playerState.Life * 3 > (long)maximumLife * 2)
                return;
            var potion = playerState.Belt
                .Where(item => item.State.StableItemId == 105)
                .OrderBy(item => item.Slot)
                .FirstOrDefault();
            if (potion is null)
                return;

            var useTick = checked(runtime.LastAdvancedTick + 1);
            var use = simulation.Execute(playerState,
                new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Belt, potion.Slot, potion.State.StableItemId)), useTick);
            Assert.True(use.Succeeded,
                $"The Rogue's ordinary healing potion was refused during arrow-route setup: {use.Failure}.");
            runtime.AdvanceTo(useTick);
            _ = runtime.DrainEvents(sessionId);
        }

        void AdvanceSourceTo(ulong tick)
        {
            while (runtime.LastAdvancedTick < tick) {
                runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
                _ = runtime.DrainEvents(sessionId);
                UseHealingPotionIfNeeded();
            }
        }

        var entrance = Assert.Single(world.World!.WarpTargets,
            target => target.SourceLevelId == smith.LevelId && target.TargetLevelId == 1);
        MoveToLevelTrigger(simulation, runtime, playerState, entrance, AdvanceSourceTo);
        Assert.Equal(1U, playerState.LevelId);
        Assert.True(playerState.Life > 0);

        var targets = world.CombatTargets.Values
            .Where(target => target.LevelId == 1 && target.HitPoints > 0
                && !target.IsPetrified && !target.IsPlayerMinion && !target.IsUnique
                && target.NativeInitialization is not null)
            .OrderBy(target => DeterministicGridMetrics.ChebyshevDistance(
                playerState.PositionX, playerState.PositionY, target.PositionX, target.PositionY))
            .ThenByDescending(target => target.HitPointsQ6)
            .ThenBy(target => target.EntityId)
            .ToArray();
        Assert.NotEmpty(targets);

        AuthoritativeCombatTarget? rangedTarget = null;
        foreach (var candidateTarget in targets) {
            var movementBudget = 128;
            if (MoveToRangedLineOfSight(simulation, runtime, world, playerState, candidateTarget,
                    ref movementBudget, AdvanceSourceTo, preferredMinimumFiringDistance: 4)) {
                rangedTarget = candidateTarget;
                break;
            }
            if (playerState.Life <= 0)
                break;
        }
        var targetState = Assert.IsType<AuthoritativeCombatTarget>(rangedTarget);
        Assert.True(playerState.Life > 0,
            "The Rogue did not survive ordinary positioning for the native arrow continuation check.");
        Assert.InRange(DeterministicGridMetrics.ChebyshevDistance(
            playerState.PositionX, playerState.PositionY,
            targetState.PositionX, targetState.PositionY), 1L, 4L);

        var attackTick = checked(runtime.LastAdvancedTick + 1);
        var attackResult = simulation.Execute(playerState,
            new DiabloAttackIntent(targetState.EntityId), attackTick, [playerState]);
        Assert.True(attackResult.Succeeded,
            $"The real Rogue bow attack was refused: {attackResult.Failure}.");
        var attack = Assert.IsType<AuthoritativePlayerActionState>(playerState.CurrentAction);
        Assert.Equal(DiabloPlayerCombatMode.RangedAttack, playerState.CombatMode);

        AuthoritativeProjectile? arrow = null;
        for (var step = 0; step < 64 && arrow is null; step++) {
            AdvanceSourceTo(checked(runtime.LastAdvancedTick + 1));
            arrow = world.Projectiles.Values
                .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    && projectile.SourceEntityId == playerState.EntityId
                    && projectile.TargetEntityId == targetState.EntityId
                    && projectile.NativeWeaponArrow is not null)
                .OrderBy(projectile => projectile.EntityId)
                .FirstOrDefault();
        }
        Assert.True(arrow is not null,
            "The actual native Rogue attack did not produce an in-flight native-owned arrow.");
        var arrowId = arrow!.EntityId;
        var initialNativeArrow = Assert.IsType<AuthoritativeNativeWeaponArrowState>(arrow.NativeWeaponArrow);
        Assert.InRange(arrow.RemainingTicks, 1U, 256U);
        Assert.True(arrow.VelocityX != 0 || arrow.VelocityY != 0);
        Assert.InRange(initialNativeArrow.MissileDistance, 0U, 255U);
        Assert.Equal(0, arrow.PathIndex);
        Assert.Equal(0, arrow.PathTicks);
        var initialArrowPositionX = arrow.PositionX;
        var initialArrowPositionY = arrow.PositionY;
        var initialArrowAimX = arrow.TargetX;
        var initialArrowAimY = arrow.TargetY;
        var initialArrowRemaining = arrow.RemainingTicks;
        _ = runtime.DrainEvents(sessionId);

        var codec = new DiabloCohortCodec();
        var encodedAtFlight = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            activation.CohortCandidateFactory);
        var candidate = candidateFactory(codec.Deserialize(encodedAtFlight));
        var restoredSimulation = candidate.Simulation;
        var restoredRuntime = candidate.CreateRuntime();
        var restoredPlayer = restoredRuntime.Read(sessionId, player => player);
        Assert.Equal(encodedAtFlight,
            codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));

        void AssertBranchesMatch(IReadOnlyList<DiabloGameplayEvent> sourceEvents,
            IReadOnlyList<DiabloGameplayEvent> restoredEvents)
        {
            Assert.Equal(sourceEvents, restoredEvents);
            // The full cohort bytes include the shared combat RNG and the in-flight projectile cursor.
            var sourceBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
            var restoredBytes = codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime));
            Assert.Equal(sourceBytes, restoredBytes);
        }

        void UseHealingPotionInBothBranchesIfNeeded()
        {
            var maximumLife = Math.Max(playerState.LifeMaximum, 0);
            if (playerState.Life <= 0 || maximumLife == 0
                || (long)playerState.Life * 3 > (long)maximumLife * 2)
                return;
            var sourcePotion = playerState.Belt
                .Where(item => item.State.StableItemId == 105)
                .OrderBy(item => item.Slot)
                .FirstOrDefault();
            if (sourcePotion is null)
                return;
            var restoredPotion = restoredPlayer.Belt.SingleOrDefault(item =>
                item.Slot == sourcePotion.Slot && item.State.StableItemId == sourcePotion.State.StableItemId);
            Assert.NotNull(restoredPotion);

            var intent = new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Belt, sourcePotion.Slot, sourcePotion.State.StableItemId));
            var useTick = checked(runtime.LastAdvancedTick + 1);
            var sourceUse = simulation.Execute(playerState, intent, useTick);
            var restoredUse = restoredSimulation.Execute(restoredPlayer, intent, useTick);
            Assert.Equal(sourceUse.Succeeded, restoredUse.Succeeded);
            Assert.True(sourceUse.Succeeded,
                $"The paired ordinary healing-potion use was refused: {sourceUse.Failure}.");
            runtime.AdvanceTo(useTick);
            var sourceEvents = runtime.DrainEvents(sessionId).ToArray();
            restoredRuntime.AdvanceTo(useTick);
            var restoredEvents = restoredRuntime.DrainEvents(sessionId).ToArray();
            AssertBranchesMatch(sourceEvents, restoredEvents);
        }

        IReadOnlyList<DiabloGameplayEvent> AdvanceBothOneTick()
        {
            var nextTick = checked(runtime.LastAdvancedTick + 1);
            runtime.AdvanceTo(nextTick);
            var sourceEvents = runtime.DrainEvents(sessionId).ToArray();
            restoredRuntime.AdvanceTo(nextTick);
            var restoredEvents = restoredRuntime.DrainEvents(sessionId).ToArray();
            AssertBranchesMatch(sourceEvents, restoredEvents);
            UseHealingPotionInBothBranchesIfNeeded();
            return sourceEvents;
        }

        bool sawTargetMissContinue = false;
        var resolved = false;
        var sawTargetDamage = false;
        var lastDistance = initialNativeArrow.MissileDistance;
        var initialTargetHitPoints = targetState.HitPointsQ6;
        for (var step = 0; step < 64; step++) {
            var events = AdvanceBothOneTick();
            var targetDamage = events.Any(gameplayEvent =>
                gameplayEvent.Kind == DiabloGameplayEventKind.Damage
                && gameplayEvent.SourceEntityId == playerState.EntityId
                && gameplayEvent.TargetEntityId == targetState.EntityId);
            sawTargetDamage |= targetDamage;
            if (!world.Projectiles.TryGetValue(arrowId, out var sourceArrow)) {
                Assert.False(restoredSimulation.WorldState.Projectiles.ContainsKey(arrowId));
                resolved = true;
                break;
            }

            Assert.True(restoredSimulation.WorldState.Projectiles.TryGetValue(arrowId,
                out var restoredArrow));
            Assert.NotNull(restoredArrow);
            Assert.Equal(sourceArrow.NativeWeaponArrow, restoredArrow!.NativeWeaponArrow);
            Assert.Equal((sourceArrow.PositionX, sourceArrow.PositionY,
                sourceArrow.TraveledX, sourceArrow.TraveledY, sourceArrow.RemainingTicks,
                sourceArrow.LastCollisionTargetEntityId),
                (restoredArrow.PositionX, restoredArrow.PositionY,
                    restoredArrow.TraveledX, restoredArrow.TraveledY, restoredArrow.RemainingTicks,
                    restoredArrow.LastCollisionTargetEntityId));
            var distance = Assert.IsType<AuthoritativeNativeWeaponArrowState>(
                sourceArrow.NativeWeaponArrow).MissileDistance;
            Assert.True(distance >= lastDistance,
                "The native missile-distance counter moved backward after cohort restoration.");
            lastDistance = distance;

            var passedAim = ((long)sourceArrow.PositionX - sourceArrow.TargetX) * sourceArrow.VelocityX
                + ((long)sourceArrow.PositionY - sourceArrow.TargetY) * sourceArrow.VelocityY > 0;
            if ((sourceArrow.LastCollisionTargetEntityId == targetState.EntityId || passedAim)
                && !targetDamage && targetState.HitPointsQ6 == initialTargetHitPoints) {
                sawTargetMissContinue = true;
                if (passedAim)
                    break;
                var distanceAtMiss = distance;
                _ = AdvanceBothOneTick();
                var sourceSurvivedContinuation = world.Projectiles.TryGetValue(arrowId, out sourceArrow);
                Assert.Equal(sourceSurvivedContinuation,
                    restoredSimulation.WorldState.Projectiles.ContainsKey(arrowId));
                if (sourceSurvivedContinuation) {
                    var continued = Assert.IsType<AuthoritativeNativeWeaponArrowState>(
                        sourceArrow!.NativeWeaponArrow);
                    Assert.True(continued.MissileDistance > distanceAtMiss,
                        "A missed native arrow did not keep advancing its native missile distance.");
                } else {
                    var nextX = initialArrowAimX + Math.Sign(initialArrowAimX - initialArrowPositionX);
                    var nextY = initialArrowAimY + Math.Sign(initialArrowAimY - initialArrowPositionY);
                    var blockedAfterAim = !world.ContainsPosition(1, nextX, nextY)
                        || world.IsMissileBlocked(1, nextX, nextY)
                        || !world.HasMissileLineOfSight(1,
                            initialArrowAimX, initialArrowAimY, nextX, nextY);
                    Assert.True(blockedAfterAim,
                        "A missed native arrow terminated at its aim despite a clear next tile.");
                }
                break;
            }
        }

        Assert.True(resolved || sawTargetMissContinue,
            "The in-flight native arrow neither resolved nor recorded a target collision; "
                + $"attack={attack.StartedAtTick}/{attack.EffectTick}/{attack.EndTick},"
                + $"target={targetState.EntityId}@({targetState.PositionX},{targetState.PositionY})"
                + $"/hpQ6={targetState.HitPointsQ6},arrow={arrowId}/at=({initialArrowPositionX},"
                + $"{initialArrowPositionY})/aim=({initialArrowAimX},{initialArrowAimY})"
                + $"/distance={lastDistance}/remaining={initialArrowRemaining}.");

        if (resolved && targetState.HitPointsQ6 < initialTargetHitPoints)
            Assert.True(sawTargetDamage,
                "The target lost hit points during the paired arrow run without a matching player damage event.");
    }

    [Fact(Explicit = true)]
    public void NativeBlockedBowFreezesAimAndLaunchesAfterTargetRemovalAcrossColdRestore()
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimeTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
            || string.IsNullOrWhiteSpace(footprintTablePath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
            return;
        }

        using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
        var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var smith = Assert.Single(activation.TownerGreetings!.Definitions,
            definition => definition.TownerId == 0);
        var start = new[] {
            (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1),
        }.Select(offset => (X: smith.PositionX + offset.X, Y: smith.PositionY + offset.Y))
            .First(position => world.IsWalkable(smith.LevelId, position.X, position.Y)
                && !world.IsOccupied(smith.LevelId, position.X, position.Y,
                    Array.Empty<AuthoritativePlayerState>()));
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-blocked-arrow-cohort-continuation",
            rogue.ClassId,
            goldOverride: 100,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: smith.LevelId,
            startingPositionX: start.X,
            startingPositionY: start.Y);
        var runtime = simulation.CreateRuntime(template);
        const string sessionId = "native-blocked-arrow-cohort-continuation";
        runtime.Write(sessionId, player => player.EntityId = 0x70000072);
        var player = runtime.Read(sessionId, current => current);

        void UseHealingPotionIfNeeded()
        {
            if (player.Life <= 0 || player.LifeMaximum <= 0
                || (long)player.Life * 3 > (long)player.LifeMaximum * 2)
                return;
            var potion = player.Belt
                .Where(item => item.State.StableItemId == 105)
                .OrderBy(item => item.Slot)
                .FirstOrDefault();
            if (potion is null)
                return;

            var useTick = checked(runtime.LastAdvancedTick + 1);
            var use = simulation.Execute(player,
                new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Belt, potion.Slot, potion.State.StableItemId)), useTick);
            Assert.True(use.Succeeded,
                $"The Rogue's ordinary healing potion was refused during blocked-arrow setup: {use.Failure}.");
            runtime.AdvanceTo(useTick);
            _ = runtime.DrainEvents(sessionId);
        }

        void AdvanceSourceTo(ulong tick) => AdvanceSourceWithHealingPolicy(tick, useHealingPotions: true);

        void AdvanceSourceWithHealingPolicy(ulong tick, bool useHealingPotions)
        {
            while (runtime.LastAdvancedTick < tick) {
                runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
                _ = runtime.DrainEvents(sessionId);
                if (useHealingPotions)
                    UseHealingPotionIfNeeded();
            }
        }

        var entrance = Assert.Single(world.World!.WarpTargets,
            target => target.SourceLevelId == smith.LevelId && target.TargetLevelId == 1);
        MoveToLevelTrigger(simulation, runtime, player, entrance, AdvanceSourceTo);
        Assert.Equal(1U, player.LevelId);
        Assert.True(player.Life > 0);

        // Distinct current/future aim capture is covered by the server activation fact; this case isolates
        // blocked-line admission and in-flight continuation even for a stationary eligible target.
        (AuthoritativeCombatTarget Target, int AimX, int AimY, int BlockedStep)? FindBlockedAimAt(
            int startX,
            int startY)
        {
            foreach (var target in world.CombatTargets.Values
                         .Where(value => value.LevelId == 1 && value.HitPointsQ6 > 0
                             && !value.SneakHidden && !value.IsPetrified && !value.IsPlayerMinion
                             && !value.IsUnique && value.NativeInitialization is not null)
                         .OrderBy(value => DeterministicGridMetrics.ChebyshevDistance(
                             startX, startY, value.PositionX, value.PositionY))
                         .ThenBy(value => value.EntityId)) {
                var currentDistance = DeterministicGridMetrics.ChebyshevDistance(
                    startX, startY, target.PositionX, target.PositionY);
                var aimX = target.FuturePositionX;
                var aimY = target.FuturePositionY;
                var aimDistance = DeterministicGridMetrics.ChebyshevDistance(
                    startX, startY, aimX, aimY);
                if (currentDistance < 1 || currentDistance > player.LightRadius
                    || aimDistance < 1 || aimDistance > player.LightRadius
                    || !world.ContainsPosition(1, aimX, aimY)
                    || world.HasMissileLineOfSight(1, startX, startY, target.PositionX, target.PositionY)
                    || world.HasMissileLineOfSight(1, startX, startY, aimX, aimY))
                    continue;

                var x = startX;
                var y = startY;
                for (var step = 1; step <= aimDistance; step++) {
                    var nextX = x + Math.Sign((long)aimX - x);
                    var nextY = y + Math.Sign((long)aimY - y);
                    if (!world.ContainsPosition(1, nextX, nextY)
                        || world.IsMissileGeometryBlocked(1, nextX, nextY)
                        || !world.HasMissileLineOfSight(1, x, y, nextX, nextY)) {
                        if (step < aimDistance && step >= 2)
                            return (target, aimX, aimY, step);
                        break;
                    }
                    if (world.CombatTargets.Values.Any(other => other.EntityId != target.EntityId
                            && other.HitPointsQ6 > 0 && other.LevelId == 1
                            && (other.PositionX == nextX && other.PositionY == nextY
                                || other.IsMoving && other.FuturePositionX == nextX
                                    && other.FuturePositionY == nextY)))
                        break;
                    x = nextX;
                    y = nextY;
                }
            }
            return null;
        }

        (AuthoritativeCombatTarget Target, int AimX, int AimY, int BlockedStep)? blockedAim = null;
        for (var movement = 0; movement < 128 && blockedAim is null; movement++) {
            blockedAim = FindBlockedAimAt(player.PositionX, player.PositionY);
            if (blockedAim is not null)
                break;
            var step = DeterministicGridNavigation.FindStepToGoal(
                player.PositionX,
                player.PositionY,
                maximumDepth: 256,
                (x, y) => FindBlockedAimAt(x, y) is not null,
                (x, y) => world.IsWalkable(player.LevelId, x, y),
                (x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
            if (step is not { } destination
                || !MoveOneCell(simulation, runtime, world, player, destination, AdvanceSourceTo))
                break;
            Assert.True(player.Life > 0,
                "The Rogue did not survive ordinary movement to a wall-blocked native bow target.");
        }

        Assert.True(blockedAim.HasValue,
            "The Rogue could not find a reachable target behind a static missile blocker.");
        var selectedAim = blockedAim.Value;
        Assert.True(selectedAim.BlockedStep < DeterministicGridMetrics.ChebyshevDistance(
            player.PositionX, player.PositionY, selectedAim.AimX, selectedAim.AimY));
        var randomBeforeAdmission = simulation.RandomState;
        var attackTick = checked(runtime.LastAdvancedTick + 1);
        var attackResult = simulation.Execute(player,
            new DiabloAttackIntent(selectedAim.Target.EntityId), attackTick, [player]);
        Assert.Equal(randomBeforeAdmission, simulation.RandomState);
        Assert.True(attackResult.Succeeded,
            $"The native-owned Rogue bow action was rejected through a blocked lane: {attackResult.Failure}.");
        var action = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(DiabloPlayerCombatMode.RangedAttack, player.CombatMode);
        Assert.False(action.TargetCell);
        Assert.Equal(selectedAim.Target.EntityId, action.TargetEntityId);
        var frozenAim = (X: selectedAim.AimX, Y: selectedAim.AimY);
        Assert.Equal(frozenAim, (action.TargetX, action.TargetY));
        Assert.True(action.EffectTick > attackTick,
            "The test needs a real pre-effect windup interval for cohort restoration.");

        var preEffectTick = action.EffectTick - 1;
        AdvanceSourceWithHealingPolicy(preEffectTick, useHealingPotions: false);
        Assert.Same(action, player.CurrentAction);
        Assert.False(action.EffectApplied);
        Assert.Equal(frozenAim, (action.TargetX, action.TargetY));
        var randomBeforeEffect = simulation.RandomState;
        var targetEntityId = selectedAim.Target.EntityId;
        var codec = new DiabloCohortCodec();
        var encodedWindup = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            activation.CohortCandidateFactory);
        var candidate = candidateFactory(codec.Deserialize(encodedWindup));
        var restoredSimulation = candidate.Simulation;
        var restoredRuntime = candidate.CreateRuntime();
        var restoredPlayer = restoredRuntime.Read(sessionId, current => current);
        var restoredAction = Assert.IsType<AuthoritativePlayerActionState>(restoredPlayer.CurrentAction);
        Assert.Equal((action.TargetEntityId, action.TargetCell, action.TargetX, action.TargetY,
            action.StartedAtTick, action.EffectTick, action.EndTick, action.EffectApplied),
            (restoredAction.TargetEntityId, restoredAction.TargetCell, restoredAction.TargetX,
                restoredAction.TargetY, restoredAction.StartedAtTick, restoredAction.EffectTick,
                restoredAction.EndTick, restoredAction.EffectApplied));
        Assert.Equal(encodedWindup,
            codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));

        var restoredWorld = restoredSimulation.WorldState;
        // The full native cohort was validated above; isolate this collision run
        // from unrelated monster AI and leave no target at the frozen aim tile.
        world.CombatTargets.Clear();
        restoredWorld.CombatTargets.Clear();
        world.Projectiles.Clear();
        restoredWorld.Projectiles.Clear();
        Assert.Equal(randomBeforeEffect, simulation.RandomState);
        Assert.Equal(randomBeforeEffect, restoredSimulation.RandomState);
        uint? randomAfterLaunch = null;

        AuthoritativeProjectile? FindArrow(DiabloWorldState currentWorld)
            => currentWorld.Projectiles.Values.SingleOrDefault(projectile =>
                projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                && projectile.SourceEntityId == player.EntityId
                && projectile.TargetEntityId == targetEntityId
                && projectile.NativeWeaponArrow is not null);

        var arrowId = 0U;
        var arrowWasInFlight = false;
        var arrowResolvedAtWall = false;
        AuthoritativeProjectile? lastSourceArrow = null;
        AuthoritativeProjectile? lastRestoredArrow = null;
        uint maximumMissileDistance = 0;
        (int X, int Y) lastArrowPosition = default;
        for (var step = 0; step < 64; step++) {
            var nextTick = checked(runtime.LastAdvancedTick + 1);
            runtime.AdvanceTo(nextTick);
            var sourceEvents = runtime.DrainEvents(sessionId).ToArray();
            restoredRuntime.AdvanceTo(nextTick);
            var restoredEvents = restoredRuntime.DrainEvents(sessionId).ToArray();
            Assert.Equal(sourceEvents, restoredEvents);
            Assert.DoesNotContain(sourceEvents, gameplayEvent =>
                gameplayEvent.Kind == DiabloGameplayEventKind.Damage);
            Assert.False(world.CombatTargets.ContainsKey(targetEntityId));
            Assert.False(restoredWorld.CombatTargets.ContainsKey(targetEntityId));

            var sourceArrow = FindArrow(world);
            var restoredArrow = FindArrow(restoredWorld);
            Assert.Equal(sourceArrow is not null, restoredArrow is not null);
            if (sourceArrow is not null && randomAfterLaunch is null)
                randomAfterLaunch = simulation.RandomState;
            var expectedRandomState = randomAfterLaunch ?? randomBeforeEffect;
            Assert.Equal(expectedRandomState, simulation.RandomState);
            Assert.Equal(simulation.RandomState, restoredSimulation.RandomState);
            if (sourceArrow is null) {
                if (arrowWasInFlight) {
                    Assert.NotNull(lastSourceArrow);
                    Assert.NotNull(lastRestoredArrow);
                    Assert.True(world.ContainsPosition(1, lastSourceArrow.PositionX, lastSourceArrow.PositionY));
                    Assert.True(world.IsMissileGeometryBlocked(
                        1, lastSourceArrow.PositionX, lastSourceArrow.PositionY));
                    Assert.True(restoredWorld.ContainsPosition(
                        1, lastRestoredArrow.PositionX, lastRestoredArrow.PositionY));
                    Assert.True(restoredWorld.IsMissileGeometryBlocked(
                        1, lastRestoredArrow.PositionX, lastRestoredArrow.PositionY));
                    Assert.True(lastSourceArrow.RemainingTicks > 0);
                    Assert.True(lastRestoredArrow.RemainingTicks > 0);
                    Assert.Equal((lastSourceArrow.PositionX, lastSourceArrow.PositionY,
                            lastSourceArrow.RemainingTicks),
                        (lastRestoredArrow.PositionX, lastRestoredArrow.PositionY,
                            lastRestoredArrow.RemainingTicks));
                    arrowResolvedAtWall = true;
                    break;
                }
                continue;
            }

            arrowWasInFlight = true;
            arrowId = sourceArrow.EntityId;
            lastSourceArrow = sourceArrow;
            lastRestoredArrow = restoredArrow;
            Assert.Equal(arrowId, restoredArrow!.EntityId);
            Assert.Equal(frozenAim, (sourceArrow.TargetX, sourceArrow.TargetY));
            Assert.Equal((sourceArrow.PositionX, sourceArrow.PositionY, sourceArrow.VelocityX,
                    sourceArrow.VelocityY, sourceArrow.RemainingTicks, sourceArrow.TraveledX,
                    sourceArrow.TraveledY, sourceArrow.AttackHit, sourceArrow.DamageQ6,
                    sourceArrow.NativeWeaponArrow),
                (restoredArrow.PositionX, restoredArrow.PositionY, restoredArrow.VelocityX,
                    restoredArrow.VelocityY, restoredArrow.RemainingTicks, restoredArrow.TraveledX,
                    restoredArrow.TraveledY, restoredArrow.AttackHit, restoredArrow.DamageQ6,
                    restoredArrow.NativeWeaponArrow));
            Assert.False(sourceArrow.AttackHit,
                "The targetless wall flight resolved a native hit before collision.");
            Assert.Equal(0, sourceArrow.DamageQ6);
            var nativeArrow = Assert.IsType<AuthoritativeNativeWeaponArrowState>(
                sourceArrow.NativeWeaponArrow);
            maximumMissileDistance = Math.Max(maximumMissileDistance, nativeArrow.MissileDistance);
            lastArrowPosition = (sourceArrow.PositionX, sourceArrow.PositionY);
        }

        Assert.NotEqual(0U, arrowId);
        Assert.True(arrowWasInFlight,
            "The accepted entity-target action did not launch an arrow after its target disappeared.");
        Assert.True(arrowResolvedAtWall,
            "The native arrow did not stop at the static missile blocker before its frozen aim tile.");
        Assert.True(maximumMissileDistance > 0,
            "The native missile-distance cursor did not advance before the wall collision.");
        Assert.NotEqual(frozenAim, lastArrowPosition);
    }
}
