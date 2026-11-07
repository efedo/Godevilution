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

    // Literal marker choices from native world_renderer.cpp DrawDungeon (899-926, 946-974),
    // WalkSettings (players.cpp 160-169), and GetMonsterMovementMode (monsters.cpp 3746-3759).
    public static IEnumerable<object[]> NativeDirections() => [
        new object[] { 1, 1, PlayerCombatMode.WalkSouthwards, 51, 51 },  // South: negative destination marker.
        new object[] { 0, 1, PlayerCombatMode.WalkSouthwards, 50, 51 },  // SouthWest: negative destination marker.
        new object[] { -1, 1, PlayerCombatMode.WalkSideways, 50, 50 },  // West: positive origin marker.
        new object[] { -1, 0, PlayerCombatMode.WalkNorthwards, 50, 50 }, // NorthWest: positive origin marker.
        new object[] { -1, -1, PlayerCombatMode.WalkNorthwards, 50, 50 },// North: positive origin marker.
        new object[] { 0, -1, PlayerCombatMode.WalkNorthwards, 50, 50 }, // NorthEast: positive origin marker.
        new object[] { 1, -1, PlayerCombatMode.WalkSideways, 51, 49 },  // East: negative destination marker.
        new object[] { 1, 0, PlayerCombatMode.WalkSouthwards, 51, 50 }, // SouthEast: negative destination marker.
    ];

    public static IEnumerable<object[]> NativeWalkFrames()
    {
        foreach (var direction in NativeDirections())
            foreach (var monster in new[] { false, true })
                foreach (var progress in new[] { 0d, 0.25, 0.5, 1d })
                    yield return [.. direction, monster, progress];
    }

    [Theory]
    [MemberData(nameof(NativeWalkFrames))]
    public void NativeMarkerCellStaysFixedAtTheStartQuarterMiddleAndEndOfEachWalk(
        int dx, int dy, PlayerCombatMode mode, int expectedX, int expectedY, bool monster, double progress)
    {
        var world = new WorldPresentation();
        var p = new ClientPresentationPosition(50 + dx * progress, 50 + dy * progress);
        var view = NativeSceneViewport.Create(808, 520, 112, 112, p.X, p.Y);
        uint id;
        if (monster) {
            var state = WalkingMonster(dx, dy);
            id = state.EntityId;
            world.BeginRetainedMonsterTest(state, p, view);
        } else {
            var state = WalkingPlayer(dx, dy, mode);
            id = state.EntityId;
            world.BeginRetainedPlayerTest(state, p, view);
        }
        world.AdvanceRetainedTest(p, view);

        Assert.Equal((expectedX, expectedY), world.RetainedTestCell(id));
        Assert.Equal(1, world.RetainedTestActorCount); // The other signed occupancy marker never draws a second actor.
        var actual = world.RetainedTestAnchor(id);
        var expected = view.CellBottomAnchor(p.X, p.Y);
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4); // The cell changes ordering, never the interpolated pixel position.
    }

    public static IEnumerable<object[]> PetrifiedDirections()
        => NativeDirections().Select(direction => new[] { direction[0], direction[1] });

    [Theory]
    [MemberData(nameof(PetrifiedDirections))]
    public void PetrifiedMonstersUseThePositiveOriginMarkerEvenWithAFrozenWalkOffset(int dx, int dy)
    {
        // DrawDungeon checks actual MonsterMode::Petrified, not getVisualMonsterMode's saved walk mode.
        var state = WalkingMonster(dx, dy);
        state.Petrified = true;
        var p = new ClientPresentationPosition(50 + dx * 0.25, 50 + dy * 0.25);
        var view = NativeSceneViewport.Create(808, 520, 112, 112, p.X, p.Y);
        var world = new WorldPresentation();
        world.BeginRetainedMonsterTest(state, p, view);

        Assert.Equal((50, 50), world.RetainedTestCell(state.EntityId));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompletedWalkUsesTheNewPositiveMarker(bool monster)
    {
        // Native DoWalk / MonsterWalk clear the old markers, move to the future tile, and stand.
        foreach (var data in NativeDirections()) {
            var dx = (int)data[0]; var dy = (int)data[1];
            var p = new ClientPresentationPosition(50 + dx, 50 + dy);
            var view = NativeSceneViewport.Create(808, 520, 112, 112, p.X, p.Y);
            var world = new WorldPresentation();
            if (monster) world.BeginRetainedMonsterTest(new() { EntityId = 2, PositionX = 50 + dx,
                PositionY = 50 + dy, FuturePositionX = 50 + dx, FuturePositionY = 50 + dy }, p, view);
            else world.BeginRetainedPlayerTest(Player(50 + dx, 50 + dy), p, view);
            Assert.Equal((50 + dx, 50 + dy), world.RetainedTestCell(monster ? 2U : 1U));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RemoteActorsRoundTripTheActualDisplayedBottomAnchorWithoutAnExtraFifteenPixels(bool monster)
    {
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 50.25, 50.125);
        var world = new WorldPresentation();
        world.BeginRetainedPlayerTest(Player(50, 50), new(50.25, 50.125), view);
        // WorldPresentation.ToScreen stores this anchored coordinate, not WorldToScreen's center.
        var foot = view.CellBottomAnchor(50.375, 50.25);
        var rendered = new Godot.Vector2((float)foot.X, (float)foot.Y);
        world.AddRetainedRemoteTestActor(2, monster, rendered);
        var anchor = world.RetainedTestAnchor(2);
        Assert.Equal(rendered.X, anchor.X, 4);
        Assert.Equal(rendered.Y, anchor.Y, 4);
        Assert.Equal((52, 50), world.RetainedTestCell(2)); // Snapshot occupancy, independent of rendering/smoothing offset.
    }

    [Fact]
    public void PixelPredictionDoesNotInventAnotherOccupancyMarker()
    {
        var world = new WorldPresentation();
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 50, 50);
        var player = Player(50, 50);
        world.BeginRetainedPlayerTest(player, new(50, 50), view);
        for (var i = 0; i < 200; i++) {
            Assert.False(world.AdvanceRetainedTest(new(50 + i % 24 / 24d, 50), view));
            Assert.Equal((50, 50), world.RetainedTestCell(1));
            Assert.Equal(1, world.RetainedTestActorCount);
        }
        player.PositionX = 51; // A newly admitted standing position moves the positive marker.
        Assert.True(world.AdvanceRetainedTest(new(51, 50), view));
        Assert.Equal((51, 50), world.RetainedTestCell(1));
    }

    private static PlayerSnapshot WalkingPlayer(int dx, int dy, PlayerCombatMode mode)
    {
        var player = Player(50, 50);
        player.CombatMode = mode; player.CombatModeEndTick = 18;
        player.CurrentAction = new() { Kind = PlayerActionKind.Move, StartedAtTick = 10,
            EffectTick = 18, ReadyTick = 18, EndTick = 18, DirectionX = dx, DirectionY = dy };
        return player;
    }

    private static MonsterSnapshot WalkingMonster(int dx, int dy) => new() { EntityId = 2, Alive = true, HitPoints = 1, MaxHitPoints = 1,
        PositionX = 50, PositionY = 50, FuturePositionX = 50 + dx, FuturePositionY = 50 + dy,
        Moving = true, MovementStartedTick = 10, MovementEffectTick = 18 };

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
