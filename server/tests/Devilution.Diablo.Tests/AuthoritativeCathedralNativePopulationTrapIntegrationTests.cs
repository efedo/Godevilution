using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeWallTrapDisarmRetainsExactLinksAndColdRestoresConsumedState()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var generated = GenerateNativePopulationState(setup.Activation, setup.Fixture);
        var traps = world.CaptureNativeTraps()
            ?? throw new InvalidOperationException("The exact native activation did not configure wall traps.");
        Assert.NotEmpty(traps);
        Assert.Equal(traps.OrderBy(value => value.Facts.TrapObjectSlot), traps);

        var activeObjectOrder = generated.ActiveObjectIds.Take(generated.ActiveObjectCount)
            .Select((slot, order) => (slot, order))
            .ToDictionary(value => value.slot, value => value.order);
        foreach (var state in traps) {
            var facts = state.Facts;
            var rawTrap = generated.ObjectSlots[facts.TrapObjectSlot];
            var rawTrigger = generated.ObjectSlots[facts.TriggerObjectSlot];
            Assert.Equal(activeObjectOrder[facts.TrapObjectSlot], facts.ActiveObjectOrder);
            Assert.Equal((rawTrap.Type, rawTrap.Position.X, rawTrap.Position.Y, rawTrap.RndSeed,
                    rawTrap.Var1, rawTrap.Var2, rawTrap.Var3, rawTrap.Var4),
                (facts.TrapNativeTypeId, facts.SourceX, facts.SourceY, facts.RandomSeed,
                    facts.LinkX, facts.LinkY, facts.MissileTypeId, facts.InitialTrapVar4));
            Assert.Equal((rawTrigger.Slot, rawTrigger.Type, rawTrigger.Position.X, rawTrigger.Position.Y,
                    rawTrigger.TrapFlag),
                (facts.TriggerObjectSlot, facts.TriggerNativeTypeId, facts.TriggerX, facts.TriggerY,
                    facts.InitialTriggerTrapArmed));
            Assert.Equal(facts.TriggerObjectSlot + 1,
                Math.Abs(generated.ObjectRefs[facts.LinkY * AuthoritativeCathedralThemeRoomState.Width
                    + facts.LinkX]));
        }

        var selected = traps[0].Facts;
        var trigger = world.Objects[selected.TriggerEntityId];
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var start = FindAdjacentWalkableCell(world, trigger);
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            "native-cathedral-trap-disarm", rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: selected.LevelId, startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write("native-cathedral-trap-disarm", player => player.EntityId = 0x7E000201);
        runtime.Write("native-cathedral-trap-disarm", player => player.Attributes = player.Attributes with { Dexterity = new(25, 25) });
        var player = runtime.Read("native-cathedral-trap-disarm", value => value);
        Assert.True(player.HasAbility(DiabloPlayerAbilityKind.TrapDisarm));
        Assert.True(world.CanOperateNativeTrapTrigger(trigger));

        var codec = new DiabloCohortCodec();
        var beforeFailure = codec.Serialize(simulation.CaptureCohortState(runtime));
        var failedRandom = new DeterministicLegacyRandom(1);
        var expectedFailedRandom = new DeterministicLegacyRandom(1);
        Assert.True(expectedFailedRandom.GenerateRnd(100) > 2 * player.Attributes.Dexterity.Current - 5);
        Assert.Equal(DiabloNativeTrapDisarmResult.Failed,
            world.TryDisarmNativeTrapTrigger(player, trigger, failedRandom));
        Assert.Equal(expectedFailedRandom.State, failedRandom.State);
        Assert.True(trigger.TrapArmed);
        Assert.Equal(beforeFailure, codec.Serialize(simulation.CaptureCohortState(runtime)));

        runtime.Write("native-cathedral-trap-disarm", value => value.Attributes = value.Attributes with { Dexterity = new(53, 53) });
        player = runtime.Read("native-cathedral-trap-disarm", value => value);
        var successRandom = new DeterministicLegacyRandom(1);
        var expectedSuccessRandom = new DeterministicLegacyRandom(1);
        Assert.True(expectedSuccessRandom.GenerateRnd(100) <= 2 * player.Attributes.Dexterity.Current - 5);
        Assert.Equal(DiabloNativeTrapDisarmResult.Disarmed,
            world.TryDisarmNativeTrapTrigger(player, trigger, successRandom));
        Assert.Equal(expectedSuccessRandom.State, successRandom.State);
        Assert.False(trigger.TrapArmed);

        var linkedStates = world.CaptureNativeTraps()!
            .Where(value => value.Facts.TriggerObjectSlot == selected.TriggerObjectSlot)
            .ToArray();
        Assert.NotEmpty(linkedStates);
        Assert.All(linkedStates, value => {
            Assert.Equal(1, value.TrapVar4);
            Assert.False(value.TriggerTrapArmed);
            Assert.Equal(DiabloNativeTrapDisposition.Disarmed, value.Disposition);
        });

        var consumedBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var consumedState = codec.Deserialize(consumedBytes);
        Assert.Equal(DiabloCohortState.NativeTrapFormatVersion, consumedState.FormatVersion);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var cold = factory(consumedState);
        Assert.Equal(consumedBytes, codec.Serialize(cold.Simulation.CaptureCohortState(cold.CreateRuntime())));
    }

    [Fact(Explicit = true)]
    public void NativeWallTrapTriggerFiresOnceInObjectOrderAndPausesOffLevel()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var traps = world.CaptureNativeTraps()
            ?? throw new InvalidOperationException("The exact native activation did not configure wall traps.");
        Assert.NotEmpty(traps);
        var selected = traps.FirstOrDefault(value => value.Facts.TriggerNativeTypeId is 1 or 2)?.Facts
            ?? throw new InvalidOperationException("The pinned L1 capture has no supported wall-trapped door.");
        var trigger = world.Objects[selected.TriggerEntityId];
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var start = FindAdjacentWalkableCell(world, trigger);
        var session = "native-cathedral-trap-fire";
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: selected.LevelId, startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, player => player.EntityId = 0x7E000202);
        var player = runtime.Read(session, value => value);
        Assert.NotNull(trigger.CathedralDoor);
        if (trigger.CathedralDoor!.Status == AuthoritativeCathedralDoorStatus.Closed)
            Assert.True(world.TryOperateCathedralDoor(trigger.EntityId, [player]));
        Assert.NotEqual(AuthoritativeCathedralDoorStatus.Closed, trigger.CathedralDoor!.Status);

        var codec = new DiabloCohortCodec();
        var pendingBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var pending = codec.Deserialize(pendingBytes);
        Assert.Equal(DiabloCohortState.NativeTrapFormatVersion, pending.FormatVersion);
        var pendingStates = Assert.IsAssignableFrom<IReadOnlyList<DiabloNativeTrapState>>(pending.NativeTraps);
        var pendingLinks = pendingStates.Where(value => value.Facts.TriggerObjectSlot == selected.TriggerObjectSlot)
            .ToArray();
        Assert.NotEmpty(pendingLinks);
        Assert.All(pendingLinks, value => Assert.Equal(DiabloNativeTrapDisposition.Armed, value.Disposition));

        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var restored = factory(pending);
        var restoredRuntime = restored.CreateRuntime();
        var restoredPlayer = restoredRuntime.Read(session, value => value);
        Assert.Equal(pendingBytes,
            codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));

        var offLevelBefore = codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime));
        Assert.Empty(restored.Simulation.WorldState.ConsumeReadyNativeTraps([]));
        Assert.Equal(offLevelBefore,
            codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));

        var originalFiring = world.ConsumeReadyNativeTraps([player]);
        var restoredFiring = restored.Simulation.WorldState.ConsumeReadyNativeTraps([restoredPlayer]);
        Assert.NotEmpty(originalFiring);
        Assert.Equal(originalFiring, restoredFiring);
        Assert.Equal(originalFiring.OrderBy(value =>
                traps!.Single(state => state.Facts.TrapEntityId == value.TrapEntityId).Facts.ActiveObjectOrder),
            originalFiring);
        Assert.All(world.CaptureNativeTraps()!.Where(value =>
                value.Facts.TriggerObjectSlot == selected.TriggerObjectSlot), value => {
            Assert.Equal(1, value.TrapVar4);
            Assert.False(value.TriggerTrapArmed);
            Assert.Equal(DiabloNativeTrapDisposition.Fired, value.Disposition);
        });
        var firedBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.Equal(firedBytes,
            codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Empty(world.ConsumeReadyNativeTraps([player]));
        Assert.Equal(firedBytes, codec.Serialize(simulation.CaptureCohortState(runtime)));
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDisarmIntentOperatesDoorAndColdResumesPendingArrow(bool disarmSucceeds)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        const string session = "native-trap-disarm-command";
        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var traps = world.CaptureNativeTraps()
            ?? throw new InvalidOperationException("The exact native activation did not configure wall traps.");
        var offsets = new[] {
            (X: 0, Y: -1), (X: 1, Y: -1), (X: 1, Y: 0), (X: -1, Y: -1),
            (X: 1, Y: 1), (X: -1, Y: 0), (X: -1, Y: 1), (X: 0, Y: 1),
        };
        var candidates = traps
            .Where(value => value.Facts.TriggerNativeTypeId is 1 or 2
                && value.Disposition == DiabloNativeTrapDisposition.Armed)
            .GroupBy(value => value.Facts.TriggerObjectSlot)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single().Facts)
            .SelectMany(facts => {
                var trigger = world.Objects[facts.TriggerEntityId];
                if (trigger.CathedralDoor is not { Status: AuthoritativeCathedralDoorStatus.Closed }
                    || !world.CanOperateNativeTrapTrigger(trigger)
                    || world.WorldItems.Values.Any(item => item.LevelId == trigger.LevelId
                        && item.PositionX == trigger.PositionX && item.PositionY == trigger.PositionY)
                    || world.Corpses.Values.Any(corpse => corpse.LevelId == trigger.LevelId
                        && corpse.PositionX == trigger.PositionX && corpse.PositionY == trigger.PositionY)
                    || world.CombatTargets.Values.Any(target => target.HitPointsQ6 > 0
                        && (target.LevelId == 0 || target.LevelId == trigger.LevelId)
                        && ((target.PositionX, target.PositionY) == (trigger.PositionX, trigger.PositionY)
                            || target.IsMoving && (target.FuturePositionX, target.FuturePositionY)
                                == (trigger.PositionX, trigger.PositionY))))
                    return Enumerable.Empty<(AuthoritativeCathedralNativeTrapFacts Facts,
                        AuthoritativeWorldObject Trigger, int StartX, int StartY, int FirstX, int FirstY)>();
                return offsets.Select(offset => {
                    var startX = trigger.PositionX + offset.X;
                    var startY = trigger.PositionY + offset.Y;
                    if (!world.IsWalkable(trigger.LevelId, startX, startY)
                        || world.IsOccupied(trigger.LevelId, startX, startY, [])
                        || world.WorldItems.Values.Any(item => item.LevelId == trigger.LevelId
                            && item.PositionX == startX && item.PositionY == startY)
                        || world.Corpses.Values.Any(corpse => corpse.LevelId == trigger.LevelId
                            && corpse.PositionX == startX && corpse.PositionY == startY))
                        return (Facts: facts, Trigger: trigger, StartX: startX, StartY: startY,
                            FirstX: int.MinValue, FirstY: int.MinValue);
                    var velocity = DiabloCombatCommandProcessor.NativeTrapArrowVelocity(
                        facts.SourceX, facts.SourceY, startX, startY);
                    var first = DiabloCombatCommandProcessor.NativeTrapArrowTile(
                        facts.SourceX, facts.SourceY, velocity.X, velocity.Y);
                    return (Facts: facts, Trigger: trigger, StartX: startX, StartY: startY,
                        FirstX: first.X, FirstY: first.Y);
                });
            })
            .Where(value => value.FirstX != int.MinValue
                && Math.Abs(value.FirstX - value.Facts.SourceX)
                    + Math.Abs(value.FirstY - value.Facts.SourceY) == 1
                && (value.FirstX, value.FirstY) != (value.StartX, value.StartY)
                && !world.IsMissileBlocked(value.Facts.LevelId, value.FirstX, value.FirstY)
                && !world.CombatTargets.Values.Any(target => target.HitPointsQ6 > 0
                    && (target.LevelId == 0 || target.LevelId == value.Facts.LevelId)
                    && (DeterministicGridMetrics.ChebyshevDistance(
                            target.PositionX, target.PositionY, value.FirstX, value.FirstY) <= 1
                        || target.IsMoving && DeterministicGridMetrics.ChebyshevDistance(
                            target.FuturePositionX, target.FuturePositionY, value.FirstX, value.FirstY) <= 1)))
            .OrderByDescending(value => DeterministicGridMetrics.ChebyshevDistance(
                value.Facts.SourceX, value.Facts.SourceY, value.StartX, value.StartY))
            .FirstOrDefault();
        Assert.NotNull(candidates.Facts);
        var selected = candidates.Facts;
        var triggerObject = candidates.Trigger;
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: selected.LevelId, startingPositionX: candidates.StartX,
            startingPositionY: candidates.StartY));
        runtime.Write(session, player => player.EntityId = 0x7E000203);
        var dexterity = disarmSucceeds ? 53 : 1;
        runtime.Write(session, player => player.Attributes = player.Attributes with { Dexterity = new(dexterity, dexterity) });
        var player = runtime.Read(session, value => value);
        Assert.True(player.HasAbility(DiabloPlayerAbilityKind.TrapDisarm));
        Assert.True(triggerObject.TrapArmed);

        var codec = new DiabloCohortCodec();
        var expectedRandom = new DeterministicLegacyRandom(simulation.RandomState);
        expectedRandom.GenerateRnd(100);
        var result = simulation.Execute(player,
            new DiabloDisarmTrapIntent(triggerObject.EntityId), 1, [player]);
        Assert.True(result.Succeeded,
            $"Native disarm should still operate the linked door after a {(disarmSucceeds ? "successful" : "failed")} roll: {result.Failure}.");
        Assert.Equal(expectedRandom.State, simulation.RandomState);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, triggerObject.CathedralDoor!.Status);

        var linked = world.CaptureNativeTraps()!
            .Where(value => value.Facts.TriggerObjectSlot == selected.TriggerObjectSlot)
            .ToArray();
        Assert.Single(linked);
        Assert.Equal(disarmSucceeds ? DiabloNativeTrapDisposition.Disarmed : DiabloNativeTrapDisposition.Armed,
            linked[0].Disposition);
        Assert.Equal(!disarmSucceeds, triggerObject.TrapArmed);

        var openedBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var candidate = factory(codec.Deserialize(openedBytes));
        var resumedRuntime = candidate.CreateRuntime();
        var resumedSimulation = candidate.Simulation;
        var resumedPlayer = resumedRuntime.Read(session, value => value);
        Assert.Equal(openedBytes, codec.Serialize(resumedSimulation.CaptureCohortState(resumedRuntime)));

        runtime.AdvanceTo(1);
        resumedRuntime.AdvanceTo(1);
        var afterTick = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.Equal(afterTick, codec.Serialize(resumedSimulation.CaptureCohortState(resumedRuntime)));
        var afterTickTrap = Assert.Single(world.CaptureNativeTraps()!,
            value => value.Facts.TrapObjectSlot == selected.TrapObjectSlot);
        if (disarmSucceeds) {
            Assert.Equal(DiabloNativeTrapDisposition.Disarmed, afterTickTrap.Disposition);
            Assert.Empty(world.Projectiles.Values.Where(projectile => projectile.NativeTrapArrow is not null
                && projectile.NativeTrapArrow.NativeObjectSlot == selected.TrapObjectSlot));
        } else {
            Assert.Equal(DiabloNativeTrapDisposition.Fired, afterTickTrap.Disposition);
            var arrow = Assert.Single(world.Projectiles.Values.Where(projectile =>
                projectile.NativeTrapArrow?.NativeObjectSlot == selected.TrapObjectSlot));
            Assert.Equal(1U, arrow.NativeTrapArrow!.MissileDistance);
            var resumedArrow = Assert.Single(resumedSimulation.WorldState.Projectiles.Values.Where(projectile =>
                projectile.NativeTrapArrow?.NativeObjectSlot == selected.TrapObjectSlot));
            Assert.Equal(arrow.NativeTrapArrow, resumedArrow.NativeTrapArrow);
            Assert.Equal((arrow.PositionX, arrow.PositionY, arrow.RemainingTicks),
                (resumedArrow.PositionX, resumedArrow.PositionY, resumedArrow.RemainingTicks));
        }

        runtime.AdvanceTo(2);
        resumedRuntime.AdvanceTo(2);
        var afterSecondTick = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.Equal(afterSecondTick,
            codec.Serialize(resumedSimulation.CaptureCohortState(resumedRuntime)));
        Assert.Equal(disarmSucceeds ? DiabloNativeTrapDisposition.Disarmed : DiabloNativeTrapDisposition.Fired,
            Assert.Single(world.CaptureNativeTraps()!, value =>
                value.Facts.TrapObjectSlot == selected.TrapObjectSlot).Disposition);
        Assert.InRange(world.Projectiles.Values.Count(projectile =>
            projectile.NativeTrapArrow?.NativeObjectSlot == selected.TrapObjectSlot), 0, 1);
    }
}
