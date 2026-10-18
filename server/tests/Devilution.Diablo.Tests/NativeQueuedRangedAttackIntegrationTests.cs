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
}
