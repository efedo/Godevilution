using Devilution.Diablo;
using Devilution.Domain.Inventory;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeBarrelEntityAttackRequiresAdjacentMeleeAndResumesItsTimedCellCallback()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;
        const string session = "native-barrel-entity-target";
        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var barrel = world.Objects.Values.First(value => value.NativeObjectId == 57
            && world.CanBreakNativeBarrel(value)
            && world.TryGetNativeBarrelFacts(value, out var facts) && facts.Var2 is >= 2 and < 8
            && !world.CombatTargets.Values.Any(actor => actor.LevelId == value.LevelId && actor.HitPointsQ6 > 0
                && Chebyshev(actor.PositionX, actor.PositionY, value.PositionX, value.PositionY) <= 1));
        var start = FindAdjacentWalkableCell(world, barrel);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, value => value.EntityId = 0x7E000401);
        var player = runtime.Read(session, value => value);
        var codec = new DiabloCohortCodec();
        var bow = Assert.Single(player.Equipment, value => value.Slot == 4);
        var beforeBow = codec.Serialize(simulation.CaptureCohortState(runtime));
        var ranged = simulation.Execute(player, new DiabloAttackIntent(barrel.EntityId), 1, [player]);
        Assert.False(ranged.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, ranged.Failure);
        Assert.Equal(beforeBow, codec.Serialize(simulation.CaptureCohortState(runtime)));

        var cell = InventoryGridLayout.FindNativePlacement(player.InventoryGrid,
            bow.State.InventoryWidth, bow.State.InventoryHeight);
        Assert.True(cell >= 0);
        var unequip = simulation.Execute(player, new DiabloMoveItemIntent(
            new(DiabloPlayerItemLocation.Equipment, 4, bow.State.StableItemId),
            new(DiabloPlayerItemLocation.Inventory, checked((uint)cell), bow.State.StableItemId)), 1);
        Assert.True(unequip.Succeeded);
        runtime.AdvanceTo(1);
        Assert.DoesNotContain(player.Equipment, value => value.Slot == 4);

        var far = world.Objects.Values.First(value => value.EntityId != barrel.EntityId
            && value.LevelId == player.LevelId && world.CanBreakNativeBarrel(value)
            && Chebyshev(value.PositionX, value.PositionY, player.PositionX, player.PositionY) > 1);
        var foreign = world.Objects.Values.First(value => value.LevelId == player.LevelId
            && !world.CanBreakNativeBarrel(value));
        foreach (var entityId in new[] { far.EntityId, foreign.EntityId }) {
            var before = codec.Serialize(simulation.CaptureCohortState(runtime));
            var refusal = simulation.Execute(player, new DiabloAttackIntent(entityId), 2, [player]);
            Assert.False(refusal.Succeeded);
            Assert.Equal(DiabloRuleFailure.InvalidTarget, refusal.Failure);
            Assert.Equal(before, codec.Serialize(simulation.CaptureCohortState(runtime)));
        }

        Assert.Contains(barrel.EntityId, simulation.CaptureSnapshot(player, runtime.LastAdvancedTick).NativeBarrelMeleeBreakableEntityIds);
        var accepted = simulation.Execute(player, new DiabloAttackIntent(barrel.EntityId), 2, [player]);
        Assert.True(accepted.Succeeded);
        var action = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.True(action.TargetCell);
        Assert.Equal(0U, action.TargetEntityId);
        Assert.Equal((barrel.PositionX, barrel.PositionY), (action.TargetX, action.TargetY));
        Assert.True(action.EffectTick > 2);
        Assert.False(barrel.Activated);
        runtime.AdvanceTo(2);
        var mid = codec.Serialize(simulation.CaptureCohortState(runtime));
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var fresh = factory(codec.Deserialize(mid));
        simulation = fresh.Simulation;
        world = simulation.WorldState;
        runtime = fresh.CreateRuntime();
        player = runtime.Read(session, value => value);
        barrel = world.Objects[barrel.EntityId];
        Assert.Equal(mid, codec.Serialize(simulation.CaptureCohortState(runtime)));
        runtime.AdvanceTo(action.EffectTick);
        Assert.True(barrel.Activated);
        Assert.DoesNotContain(barrel.EntityId, simulation.CaptureSnapshot(player, runtime.LastAdvancedTick).NativeBarrelMeleeBreakableEntityIds);
        runtime.AdvanceTo(checked(action.EndTick + 1));
        var broken = codec.Serialize(simulation.CaptureCohortState(runtime));
        var duplicate = simulation.Execute(player, new DiabloAttackIntent(barrel.EntityId),
            checked(runtime.LastAdvancedTick + 1), [player]);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, duplicate.Failure);
        Assert.Equal(broken, codec.Serialize(simulation.CaptureCohortState(runtime)));
    }
}
