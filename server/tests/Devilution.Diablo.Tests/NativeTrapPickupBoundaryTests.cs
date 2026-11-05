using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeTrapPickupBoundaryTests
{
    [Fact]
    public void WallArrowTemporarilyRefusesWalkingThenGoldCanBePickedUpDiagonally()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var content = Path.Combine(root!.FullName, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", File.ReadAllText(Path.Combine(content, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv("class_animations.tsv",
            File.ReadAllText(Path.Combine(content, "class_animations.tsv")), classes);
        var shield = new EquippedStoreItem(4, 1, AuthoritativeItemState.Empty with { StatFlag = true, ItemType = 5 });
        var player = new AuthoritativePlayerState(0, 0, 11776, 0, 0,
            new(new(20, 20), new(10, 10), new(100, 100), new(20, 20)), [shield], [], [],
            66, 23, 11776, 8, 5, 0) { EntityId = 1 };
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);
        var random = new DeterministicLegacyRandom(0);
        var combat = new DiabloCombatCommandProcessor(world, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100), random, classes, animations, useNativePlayerActionTiming: true);
        var missile = DiabloCombatCommandProcessor.AddObjectTrapMissile(100, 90, 91, 5, 60,
            65, 20, 66, 23, (int)DeterministicGridDirections.Toward(65, 20, 66, 23), 0, false, random, 5);
        world.Projectiles.Add(missile.EntityId, missile);
        ulong tick = 0;
        while (world.Projectiles.ContainsKey(missile.EntityId) && tick < 10) {
            combat.ProcessObjectTrapMissile(++tick, missile, 5, [player], []);
            if (player.CombatMode == DiabloPlayerCombatMode.Block) break;
        }
        Assert.Equal(DiabloPlayerCombatMode.Block, player.CombatMode);
        Assert.Equal(11776, player.Life);
        Assert.True(world.IsWalkable(5, 66, 24));
        Assert.False(world.IsOccupied(5, 66, 24, [player], player.EntityId));
        var processor = new DiabloWorldCommandProcessor(world, animations, useNativePlayerActionTiming: true);
        var move = processor.Execute(player, new DiabloMoveIntent(0, 1), tick, [player]);
        Assert.Equal(DiabloRuleFailure.NotAllowed, move.Failure);
        tick = player.CombatModeEndTick;
        DiabloCombatCommandProcessor.AdvanceCombatMode(player, tick);
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        world.WorldItems.Add(200, new AuthoritativeWorldItem(200, 5, 65, 24, 2, 48,
            AuthoritativeItemState.Empty with { ItemType = 11, ItemIndex = 0, Value = 48, IdentifiedValue = 48,
                StatFlag = true, InventoryWidth = 1, InventoryHeight = 1 }));
        var pickup = processor.Execute(player, new DiabloPickupWorldItemIntent(200), tick, [player]);
        Assert.True(pickup.Succeeded, pickup.Failure?.ToString());
        Assert.False(world.WorldItems.ContainsKey(200));
        Assert.Equal(48U, player.Gold);
        Assert.Equal((66, 23), (player.PositionX, player.PositionY));
        Assert.True(processor.Execute(player, new DiabloMoveIntent(0, 1), tick, [player]).Succeeded);
    }
}
