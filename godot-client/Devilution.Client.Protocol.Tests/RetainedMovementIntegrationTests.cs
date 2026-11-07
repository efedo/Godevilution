using Devilution.Client;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class RetainedMovementIntegrationTests
{
    [Fact]
    public void SpriteObservationCameraAndRetainedItemsShareEveryFractionalFrame()
    {
        var player = Player(50, 50);
        var movement = new AuthoritativeLocalPlayerPresentation();
        var position = movement.Advance(player, 10, new(51, 50), 0, 20);
        var reference = movement.NativeViewport(808, 520, 112, 112);
        var world = new WorldPresentation();
        world.BeginRetainedPlayerTest(player, position, reference);
        world.AddRetainedStaticTestActor(2, 51, 50);
        var terrain = CreateTerrainCell(reference);
        var originalRaster = Assert.Single(terrain.Cell.Content).Bounds;
        var itemReferenceAnchor = reference.CellBottomAnchor(51, 50);
        var invalidation = new NativeSceneRetentionState();
        var geometry = new object();
        invalidation.Update(geometry, 0, reference, 1, 10);

        for (var frame = 1; frame < 24; frame++) {
            position = movement.Advance(player, 10, new(51, 50), 1d / 60, 20);
            var view = movement.NativeViewport(808, 520, 112, 112);
            world.AdvanceRetainedTest(position, view);
            var expectedPlayerAnchor = view.CellBottomAnchor(position.X, position.Y);
            Assert.Equal(expectedPlayerAnchor, world.RetainedTestAnchor(1));
            Assert.NotEqual(view.CellBottomAnchor(51, 50), world.RetainedTestAnchor(1));
            var expectedItemAnchor = view.CellBottomAnchor(51, 50);
            Assert.Equal(expectedItemAnchor, world.RetainedTestAnchor(2));
            var dx = view.OriginX - reference.OriginX;
            var dy = view.OriginY - reference.OriginY;
            Assert.NotEqual(0, dx);
            Assert.Equal(expectedItemAnchor.X, itemReferenceAnchor.X + dx, 8);
            Assert.Equal(expectedItemAnchor.Y, itemReferenceAnchor.Y + dy, 8);
            Assert.Equal(NativeSceneRefresh.Viewport, invalidation.Update(geometry, 0, view, 1, 10));
            Assert.False(terrain.Cell.Update(terrain.Family, 0, null));
            Assert.Equal(originalRaster, Assert.Single(terrain.Cell.Content).Bounds);
            var actual = Assert.Single(terrain.Cell.Content).Bounds;
            var anchor = view.CellBottomAnchor(50, 50);
            Assert.Equal(anchor.X - 32, actual.X + dx, 8);
            Assert.Equal(anchor.Y - 31, actual.Y + dy, 8);
        }
    }

    [Fact]
    public void FractionalFeetStayBetweenTheCorrectWallsThroughoutEveryWalkingDirection()
    {
        var order = NativeSceneCatalog.ResolveIsometricCellOrder(8, 8);
        foreach (var start in new[] { (X: 0, Y: 0), (X: 3, Y: 3), (X: 7, Y: 7) })
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++) {
                    if (dx == 0 && dy == 0 || start.X + dx < 0 || start.X + dx >= 8 || start.Y + dy < 0 || start.Y + dy >= 8) continue;
                    var world = new WorldPresentation();
                    var player = Player(start.X, start.Y);
                    var origin = new ClientPresentationPosition(start.X, start.Y);
                    var initial = NativeSceneViewport.Create(200, 100, 8, 8, origin.X, origin.Y);
                    world.BeginRetainedPlayerTest(player, origin, initial);
                    for (var frame = 0; frame <= 24; frame++) {
                        var p = new ClientPresentationPosition(start.X + dx * frame / 24d, start.Y + dy * frame / 24d);
                        var view = NativeSceneViewport.Create(200, 100, 8, 8, p.X, p.Y);
                        world.AdvanceRetainedTest(p, view);
                        var foot = world.RetainedTestAnchor(1);
                        var expected = order.Last(cell => {
                            var wall = view.CellBottomAnchor(cell.X, cell.Y);
                            return wall.Y < foot.Y || wall.Y == foot.Y && wall.X <= foot.X;
                        });
                        Assert.Equal(expected, world.RetainedTestCell(1));
                        Assert.Equal(1, world.RetainedTestActorCount);
                    }
                }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RemoteActorsUseTheirActuallyRenderedSmoothedCenter(bool monster)
    {
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 50.25, 50.125);
        var world = new WorldPresentation();
        world.BeginRetainedPlayerTest(Player(50, 50), new(50.25, 50.125), view);
        var center = view.WorldToScreen(50.375, 50.25);
        var rendered = new Godot.Vector2((float)center.X, (float)center.Y);
        world.AddRetainedRemoteTestActor(2, monster, rendered);
        var anchor = world.RetainedTestAnchor(2);
        Assert.Equal(rendered.X, anchor.X, 4);
        Assert.Equal(rendered.Y + NativeSceneViewport.BottomInclusiveOffset, anchor.Y, 4);
    }

    [Fact]
    public void FractionalActorOrderChangesWithoutANewSnapshotOrDuplicatingMembers()
    {
        var world = new WorldPresentation();
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 50, 50);
        world.BeginRetainedPlayerTest(Player(50, 50), new(50, 50), view);
        Assert.True(world.AdvanceRetainedTest(new(50.25, 50.25), view));
        Assert.Equal((100, 0), world.RetainedTestCell(1));
        Assert.False(world.AdvanceRetainedTest(new(50.3, 50.3), view));
        Assert.True(world.AdvanceRetainedTest(new(50.5, 50.5), view));
        Assert.Equal((50, 51), world.RetainedTestCell(1));
        Assert.True(world.AdvanceRetainedTest(new(51, 51), view));
        Assert.Equal((51, 51), world.RetainedTestCell(1));
        for (var i = 0; i < 200; i++) {
            world.AdvanceRetainedTest(new(50 + i % 24 / 24d, 50), view);
            Assert.Equal(1, world.RetainedTestActorCount);
        }
    }

    private static PlayerSnapshot Player(int x, int y) => new() { EntityId = 1, PositionX = x, PositionY = y,
        Life = 64, LifeMaximum = 64, CombatMode = PlayerCombatMode.Stand };

    private static (NativeSceneRetainedCell Cell, NativeSceneFamily Family) CreateTerrainCell(NativeSceneViewport reference)
    {
        var raster = new NativeSceneRasterReference(0, 0, 0, 32, 32);
        var slot = new NativeSceneSlot(0, 0, 0, 0, 0, 0, NativeSceneTileType.Square, null,
            NativeSceneTileType.Square, false, false, NativeSceneSlotRole.Content, null, raster);
        var family = new NativeSceneFamily(NativeSceneLocation.Town, 16, 1, [], [], [], [], [], [slot], [], "after-actors");
        var cell = new NativeSceneRetainedCell(reference, 50, 50);
        Assert.True(cell.Update(family, 0, null));
        return (cell, family);
    }
}
