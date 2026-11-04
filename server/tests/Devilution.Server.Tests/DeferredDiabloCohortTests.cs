using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Mutating live module owners cannot change a graph handed to the background encoder. */
public sealed class DeferredDiabloCohortTests
{
    [Fact]
    public async Task DeferredGraphKeepsExactBytesAfterMutatingLivePlayersWorldAndStores()
    {
        var stores = new StoreCatalog();
        stores.AddStore(1, [new StoreItem(0, 12, 10)]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(stores, DiabloModule.Instance.Rules) {
            World = world, FallbackWidth = 8, FallbackHeight = 8,
            CombatTargets = [new AuthoritativeCombatTarget(40, 3, 3, 10, levelId: 0)],
            Objects = [new AuthoritativeWorldObject(50, 1, 0, 2, 2)],
            WorldItems = [new AuthoritativeWorldItem(60, 0, 4, 4, 70, 1, AuthoritativeItemState.Empty)],
            Quests = [new AuthoritativeQuestState(1, 0, 10)],
            Portals = [new AuthoritativePortal(1, 0, 1, 1, 0, 6, 6)],
            Projectiles = [new AuthoritativeProjectile(80, 7, 40, 1, 0, 1, 1, 3, 3, 1, AuthoritativeDamageType.Fire, 0, 3)],
        });
        var template = DiabloPlayerTemplate.CreateDefault(100, 0, 0, 0);
        var runtime = simulation.CreateRuntime(template);
        var player = template.CreatePlayer();
        player.EntityId = 7;
        runtime.AddParticipant("hero", player);
        var codec = new DiabloCohortCodec();
        var expected = codec.Serialize(simulation.CaptureCohortState(runtime));
        var captured = simulation.CaptureCohortForDeferredEncoding(runtime, out var validate);
        var capturedPlayer = Assert.Single(captured.Participants).Player;
        Assert.NotSame(player.Inventory, capturedPlayer.Inventory);
        Assert.NotSame(player.InventoryGrid, capturedPlayer.InventoryGrid);
        Assert.NotSame(player.Equipment, capturedPlayer.Equipment);
        Assert.NotSame(player.Belt, capturedPlayer.Belt);
        Assert.NotSame(player.Stash, capturedPlayer.Stash);
        Assert.NotSame(player.MemorizedSpells, capturedPlayer.MemorizedSpells);
        Assert.NotSame(player.StatusEffects, capturedPlayer.StatusEffects);
        var geometry = Assert.Single(captured.Geometry!.Levels!);
        Assert.Same(geometry, Assert.Single(world.CaptureGeometryState().Levels!));
        Assert.Throws<NotSupportedException>(() => ((IList<int>)geometry.BlockedCells!)[0] = 1);

        player.Gold++;
        player.CharacterName = "changed";
        player.MemorizedSpells.Add(new MemorizedSpellState(1, 1));
        player.InventoryGrid[0] = 1;
        simulation.WorldState.CombatTargets[40].HitPoints--;
        simulation.WorldState.Objects[50].Activated = true;
        simulation.WorldState.Quests[1].Progress++;
        simulation.WorldState.Projectiles[80].PositionX++;
        simulation.WorldState.Projectiles[80].RemainingTicks--;
        simulation.WorldState.WorldItems.Clear();
        simulation.WorldState.Portals.Clear();
        simulation.WorldState.Corpses[90] = new AuthoritativeCorpse(90, 0, 4, 4);
        stores.RemoveItem(1, 0);
        world.ReplaceLevel(new AuthoritativeLevel(0, 8, 8, blockedCells: [63], isDefaultStart: true));
        runtime.AddParticipant("other", template.CreatePlayer());

        var actual = await Task.Run(() => { validate(); return codec.Serialize(captured); }, TestContext.Current.CancellationToken);
        Assert.Equal(expected, actual);
        Assert.Equal(100U, capturedPlayer.Gold);
        Assert.Single(captured.Participants);
        Assert.Single(captured.Stores[0].Items);
        Assert.Empty(geometry.BlockedCells!);
    }

    [Fact]
    public void DeferredShapeValidationStillRejectsAnInvalidGraphAtEncoding()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0));
        runtime.Write("unbound", _ => { });
        Assert.Throws<InvalidDataException>(() => simulation.CaptureCohortState(runtime));
        var captured = simulation.CaptureCohortForDeferredEncoding(runtime, out var validate);
        Assert.Throws<InvalidDataException>(() => new DiabloCohortCodec().Serialize(captured));
    }
}
