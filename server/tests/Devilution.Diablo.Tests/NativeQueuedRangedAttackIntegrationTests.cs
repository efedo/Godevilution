using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Theory(Explicit = true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NativeDepthQueuedRangedAttackFinishesCurrentStepAndUsesCompletionTiming(bool commandTick, bool quick)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, []));
        var player = activation.PlayerLoadouts.CreateTemplate("queued-native-ranged", rogue.ClassId,
            null, null, null, startingLevelId: 0,
            startingPositionX: start.X, startingPositionY: start.Y).CreatePlayer();
        player.EntityId = 0x70000101;
        if (quick) {
            // Controlled IAS fixture; native completion includes the new attack's first frame.
            var index = player.Equipment.FindIndex(item => item.Slot == 4);
            var bow = player.Equipment[index];
            player.Equipment[index] = bow with { State = bow.State with { Flags = bow.State.Flags | (1U << 17) } };
        }
        var participants = new[] { player };
        var goal = new[] { (X: 4, Y: 0), (X: 0, Y: 4), (X: -4, Y: 0), (X: 0, Y: -4) }
            .Select(offset => (X: start.X + offset.X, Y: start.Y + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, participants));
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(goal.X, goal.Y, FollowPath: true), 2, participants).Succeeded);
        Advance(2);
        var move = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(DiabloPlayerActionKind.Move, move.Kind);
        Assert.NotNull(player.NativeWalkPath);
        var destination = (X: player.FuturePositionX, Y: player.FuturePositionY);
        var originalPath = player.NativeWalkPath;
        var originalFacing = player.FacingDirection;
        Assert.False(simulation.Execute(player, new DiabloAttackIntent(uint.MaxValue), 3, participants).Succeeded);
        Assert.Same(originalPath, player.NativeWalkPath);
        Assert.False(simulation.Execute(player,
            new DiabloAttackAtIntent(int.MaxValue, int.MaxValue) { QueueDuringMove = true }, 3, participants).Succeeded);
        Assert.Same(originalPath, player.NativeWalkPath);
        Assert.Equal(originalFacing, player.FacingDirection);
        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(goal.X, goal.Y) { QueueDuringMove = true }, 3, participants).Succeeded);
        Assert.Same(move, player.CurrentAction);
        Assert.Equal(destination, (player.FuturePositionX, player.FuturePositionY));
        Assert.Equal(originalFacing, player.FacingDirection);
        Assert.Null(player.NativeWalkPath);
        Assert.Equal(new DiabloNativeQueuedRangedAttackState(0, 0, goal.X, goal.Y, true), player.NativeQueuedRangedAttack);
        for (ulong tick = 3; tick < move.EndTick; tick++) {
            Advance(tick);
            Assert.Same(move, player.CurrentAction);
            Assert.Equal(start, (player.PositionX, player.PositionY));
        }
        Advance(move.EndTick);
        Assert.Equal(destination, (player.PositionX, player.PositionY));
        Assert.Null(player.NativeQueuedRangedAttack);
        Assert.Null(player.NativeWalkPath);
        Assert.Equal(DiabloPlayerCombatMode.RangedAttack, player.CombatMode);
        var attack = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        var timing = activation.PlayerAnimations.GetRequired(rogue.ClassId).GetAttack(DiabloPlayerAttackAnimationProfile.Bow);
        var skipped = quick ? 1UL : 0UL;
        Assert.Equal((move.EndTick, move.EndTick + (ulong)timing.ActionFrame - 1 - skipped,
                move.EndTick + (ulong)timing.ActionFrame - skipped, move.EndTick + (ulong)timing.Frames - 1 - skipped),
            (attack.StartedAtTick, attack.EffectTick, attack.ReadyTick, attack.EndTick));
        Assert.True(attack.TargetCell);
        Assert.Equal((goal.X, goal.Y), (attack.TargetX, attack.TargetY));
        for (var tick = move.EndTick + 1; tick <= attack.EndTick + 1; tick++)
            Advance(tick);
        Assert.Null(player.CurrentAction);
        Assert.Equal(destination, (player.PositionX, player.PositionY));

        void Advance(ulong tick)
        {
            if (commandTick)
                simulation.AdvanceTickWithCommands(tick, participants, _ => []);
            else
                simulation.AdvanceTick(tick, participants);
        }
    }

    [Fact(Explicit = true)]
    public void NativeDepthQueuedRangedAttackColdContinuationAndWalkOverrideAreExact()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var entry = world.World!.WarpTargets.Single(warp => warp.SourceLevelId == 0 && warp.TargetLevelId == 1);
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: entry.TargetX + offset.X, Y: entry.TargetY + offset.Y))
            .First(cell => world.IsWalkable(1, cell.X, cell.Y) && !world.IsOccupied(1, cell.X, cell.Y, []));
        const string session = "native-queued-ranged-cold";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(session, rogue.ClassId,
            null, null, null, startingLevelId: 1,
            startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, value => value.EntityId = 0x70000102);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var goal = new[] { (X: 4, Y: 0), (X: 0, Y: 4), (X: -4, Y: 0), (X: 0, Y: -4) }
            .Select(offset => (X: start.X + offset.X, Y: start.Y + offset.Y))
            .First(cell => world.IsWalkable(1, cell.X, cell.Y) && !world.IsOccupied(1, cell.X, cell.Y, [player]));
        Assert.True(simulation.Execute(player, new DiabloMoveToIntent(goal.X, goal.Y, FollowPath: true), 2, [player]).Succeeded);
        runtime.AdvanceTo(2);
        var move = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(goal.X, goal.Y) { QueueDuringMove = true }, 3, [player]).Succeeded);
        runtime.AdvanceTo(3);
        var codec = new DiabloCohortCodec();
        var checkpoint = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.Contains("\"NativeQueuedRangedAttack\"", checkpoint);
        var restored = activation.CohortCandidateFactory!(codec.Deserialize(checkpoint));
        var restoredRuntime = restored.CreateRuntime();
        Assert.Equal(checkpoint, codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredPlayer = restoredRuntime.Read(session, value => value);
        var sawProjectile = false;
        for (var tick = 4UL; tick <= move.EndTick + 30; tick++) {
            runtime.AdvanceTo(tick);
            restoredRuntime.AdvanceTo(tick);
            Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
                codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));
            sawProjectile |= world.Projectiles.Values.Any(projectile => projectile.SourceEntityId == player.EntityId);
        }
        Assert.True(sawProjectile);
        Assert.Equal((player.PositionX, player.PositionY), (restoredPlayer.PositionX, restoredPlayer.PositionY));
        Assert.Null(player.NativeQueuedRangedAttack);

        // A later destination click replaces a queued shot without canceling the active step.
        var nextTick = runtime.LastAdvancedTick + 1;
        Assert.True(simulation.Execute(player, new DiabloMoveToIntent(goal.X, goal.Y, FollowPath: true), nextTick, [player]).Succeeded);
        runtime.AdvanceTo(nextTick);
        var replacementMove = player.CurrentAction;
        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(goal.X, goal.Y) { QueueDuringMove = true }, nextTick, [player]).Succeeded);
        Assert.NotNull(player.NativeQueuedRangedAttack);
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(player.FuturePositionX, player.FuturePositionY, FollowPath: true), nextTick, [player]).Succeeded);
        Assert.Null(player.NativeQueuedRangedAttack);
        Assert.Same(replacementMove, player.CurrentAction);
    }

    [Fact(Explicit = true)]
    public void NativeDepthQueuedRangedFollowupColdStartsAtReadyFrame()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var entry = world.World!.WarpTargets.Single(warp => warp.SourceLevelId == 0 && warp.TargetLevelId == 1);
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: entry.TargetX + offset.X, Y: entry.TargetY + offset.Y))
            .First(cell => world.IsWalkable(1, cell.X, cell.Y) && !world.IsOccupied(1, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-depth-ranged-followup-cold";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, null, null, null, startingLevelId: 1,
            startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, player => player.EntityId = 0x70000103);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var aimCells = new[] {
            (X: 4, Y: 0), (X: 0, Y: 4), (X: -4, Y: 0), (X: 0, Y: -4),
            (X: 3, Y: 2), (X: -3, Y: 2), (X: 3, Y: -2), (X: -3, Y: -2),
        }
            .Select(offset => (X: start.X + offset.X, Y: start.Y + offset.Y))
            .Where(cell => world.ContainsPosition(1, cell.X, cell.Y))
            .Distinct()
            .Take(3)
            .ToArray();
        Assert.Equal(3, aimCells.Length);

        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(aimCells[0].X, aimCells[0].Y), 2, [player]).Succeeded);
        var firstAction = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(DiabloPlayerCombatMode.RangedAttack, player.CombatMode);
        Assert.True(firstAction.ReadyTick > firstAction.StartedAtTick + 1);
        var queuedTick = firstAction.StartedAtTick + 1;
        Assert.False(simulation.Execute(player,
            new DiabloAttackAtIntent(aimCells[0].X, aimCells[0].Y),
            queuedTick,
            [player]).Succeeded);
        Assert.Same(firstAction, player.CurrentAction);
        Assert.Null(player.NativeQueuedRangedAttack);
        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(aimCells[1].X, aimCells[1].Y) { QueueDuringMove = true },
            queuedTick,
            [player]).Succeeded);
        Assert.Same(firstAction, player.CurrentAction);
        Assert.False(firstAction.EffectApplied);
        Assert.Equal(new DiabloNativeQueuedRangedAttackState(
            1, 0, aimCells[1].X, aimCells[1].Y, true), player.NativeQueuedRangedAttack);

        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(aimCells[2].X, aimCells[2].Y) { QueueDuringMove = true },
            queuedTick,
            [player]).Succeeded);
        var latestTarget = player.NativeQueuedRangedAttack;
        Assert.Equal(new DiabloNativeQueuedRangedAttackState(
            1, 0, aimCells[2].X, aimCells[2].Y, true), latestTarget);
        Assert.False(simulation.Execute(player,
            new DiabloAttackAtIntent(-1, -1) { QueueDuringMove = true },
            queuedTick,
            [player]).Succeeded);
        Assert.Equal(latestTarget, player.NativeQueuedRangedAttack);
        Assert.Same(firstAction, player.CurrentAction);

        runtime.AdvanceTo(queuedTick);
        Assert.Same(firstAction, player.CurrentAction);
        Assert.False(firstAction.EffectApplied);
        Assert.Equal(latestTarget, player.NativeQueuedRangedAttack);
        var codec = new DiabloCohortCodec();
        var checkpointBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(checkpointBytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(checkpointBytes,
            codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredPlayer = restoredRuntime.Read(session, value => value);

        for (var tick = queuedTick + 1; tick < firstAction.ReadyTick; tick++) {
            runtime.AdvanceTo(tick);
            restoredRuntime.AdvanceTo(tick);
            Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
                codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        }
        Assert.Same(firstAction, player.CurrentAction);
        Assert.True(firstAction.EffectApplied);
        Assert.Equal(latestTarget, player.NativeQueuedRangedAttack);
        runtime.AdvanceTo(firstAction.ReadyTick);
        restoredRuntime.AdvanceTo(firstAction.ReadyTick);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Null(player.NativeQueuedRangedAttack);
        Assert.Null(restoredPlayer.NativeQueuedRangedAttack);
        var followup = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.NotSame(firstAction, followup);
        Assert.Equal(firstAction.ReadyTick, followup.StartedAtTick);
        Assert.True(followup.TargetCell);
        Assert.Equal((aimCells[2].X, aimCells[2].Y), (followup.TargetX, followup.TargetY));
        var timing = activation.PlayerAnimations.GetRequired(rogue.ClassId)
            .GetAttack(DiabloPlayerAttackAnimationProfile.Bow);
        Assert.Equal((followup.StartedAtTick + (ulong)timing.ActionFrame - 1,
                followup.StartedAtTick + (ulong)timing.ActionFrame,
                followup.StartedAtTick + (ulong)timing.Frames - 1),
            (followup.EffectTick, followup.ReadyTick, followup.EndTick));
    }

    [Fact(Explicit = true)]
    public void NativeDepthQueuedRangedEntityFollowupUsesLiveAimAndDropsLostTarget()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x2468ACE0);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var entry = world.World!.WarpTargets.Single(warp => warp.SourceLevelId == 0 && warp.TargetLevelId == 1);
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: entry.TargetX + offset.X, Y: entry.TargetY + offset.Y))
            .First(cell => world.IsWalkable(1, cell.X, cell.Y) && !world.IsOccupied(1, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var player = activation.PlayerLoadouts.CreateTemplate(
            "native-depth-ranged-entity-followup", rogue.ClassId, null, null, null,
            startingLevelId: 1, startingPositionX: start.X, startingPositionY: start.Y).CreatePlayer();
        player.EntityId = 0x70000104;
        simulation.AdvanceTick(1, [player]);
        var target = world.CombatTargets.Values
            .Where(candidate => candidate.LevelId == 1 && candidate.HitPoints > 1)
            .OrderByDescending(candidate => candidate.HitPoints)
            .First();
        var firstAim = new[] { (X: 4, Y: 0), (X: 0, Y: 4), (X: -4, Y: 0), (X: 0, Y: -4) }
            .Select(offset => (X: start.X + offset.X, Y: start.Y + offset.Y))
            .Where(cell => world.ContainsPosition(1, cell.X, cell.Y))
            .OrderBy(cell => (cell.X - start.X) * (target.FuturePositionX - start.X)
                + (cell.Y - start.Y) * (target.FuturePositionY - start.Y))
            .First();

        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(firstAim.X, firstAim.Y), 2, [player]).Succeeded);
        var firstAction = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        simulation.AdvanceTick(2, [player]);
        var queuedTick = firstAction.StartedAtTick + 1;
        Assert.True(queuedTick < firstAction.ReadyTick);
        simulation.AdvanceTick(queuedTick, [player]);
        Assert.True(simulation.Execute(player,
            new DiabloAttackIntent(target.EntityId) { QueueDuringMove = true }, queuedTick, [player]).Succeeded);
        var queued = new DiabloNativeQueuedRangedAttackState(1, target.EntityId, 0, 0, false);
        Assert.Equal(queued, player.NativeQueuedRangedAttack);
        Assert.Same(firstAction, player.CurrentAction);

        var movedTargetPosition = Enumerable.Range(1, 8)
            .Select(code => AuthoritativeNativePlayerPathfinder.GetDisplacement(code))
            .Select(offset => (X: target.PositionX + offset.X, Y: target.PositionY + offset.Y))
            .First(cell => world.ContainsPosition(1, cell.X, cell.Y)
                && world.IsWalkable(1, cell.X, cell.Y)
                && !world.IsOccupied(1, cell.X, cell.Y, [player]));
        target.ApplyKnockback(movedTargetPosition.X, movedTargetPosition.Y);
        for (var tick = queuedTick + 1; tick < firstAction.ReadyTick; tick++)
            simulation.AdvanceTick(tick, [player]);
        var expectedLiveAim = (target.FuturePositionX, target.FuturePositionY);
        simulation.AdvanceTick(firstAction.ReadyTick, [player]);
        Assert.Null(player.NativeQueuedRangedAttack);
        var followup = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(target.EntityId, followup.TargetEntityId);
        Assert.False(followup.TargetCell);
        Assert.Equal(expectedLiveAim, (followup.TargetX, followup.TargetY));

        // A target removed after an accepted queue cancels only the follow-up;
        // the already-running native attack action remains intact.
        for (var tick = followup.StartedAtTick + 1; tick <= followup.EndTick; tick++)
            simulation.AdvanceTick(tick, [player]);
        Assert.Null(player.CurrentAction);
        var secondStartTick = followup.EndTick + 1;
        Assert.True(simulation.Execute(player,
            new DiabloAttackAtIntent(firstAim.X, firstAim.Y), secondStartTick, [player]).Succeeded);
        var secondAction = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        simulation.AdvanceTick(secondStartTick, [player]);
        var secondQueueTick = secondAction.StartedAtTick + 1;
        simulation.AdvanceTick(secondQueueTick, [player]);
        Assert.True(simulation.Execute(player,
            new DiabloAttackIntent(target.EntityId) { QueueDuringMove = true },
            secondQueueTick,
            [player]).Succeeded);
        target.HitPoints = 0;
        for (var tick = secondQueueTick + 1; tick <= secondAction.ReadyTick; tick++)
            simulation.AdvanceTick(tick, [player]);
        Assert.Null(player.NativeQueuedRangedAttack);
        Assert.Same(secondAction, player.CurrentAction);
    }
}
