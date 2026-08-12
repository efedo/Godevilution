using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldTests
{
    [Fact]
    public void LevelsValidateBoundsAndBlockedCellsIndependently()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 3, 3, [4]));
        world.AddLevel(new AuthoritativeLevel(2, 2, 2, [0]));

        Assert.True(world.IsWalkable(1, 0, 0));
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(2, 0, 0));
        Assert.False(world.IsWalkable(2, 2, 0));
    }

    [Fact]
    public void RejectsDuplicateLevelsAndInvalidBlockedCells()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 2, 2));
        Assert.Throws<InvalidDataException>(() => world.AddLevel(new AuthoritativeLevel(1, 2, 2)));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(2, 2, 2, [4]));
    }

    [Fact]
    public void MonsterMovementRequiresAnAdjacentFutureOccupancy()
    {
        Assert.Throws<ArgumentException>(() => new AuthoritativeCombatTarget(
            9,
            1,
            1,
            10,
            moving: true,
            futurePositionX: 3,
            futurePositionY: 1));
        Assert.Throws<ArgumentException>(() => new AuthoritativeCombatTarget(
            9,
            1,
            1,
            10,
            futurePositionX: 2,
            futurePositionY: 1));
    }

    [Fact]
    public void LoadsLevelGeometryFromExternalTsv()
    {
        var world = AuthoritativeWorld.LoadTsv("levels.tsv", "level_id\twidth\theight\tblocked_cells\n1\t3\t3\t4\n");

        Assert.True(world.ContainsLevel(1));
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.True(world.IsMissileBlocked(1, 1, 1));
    }

    [Fact]
    public void LoadsOneExplicitDefaultStartAndRejectsDuplicates()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tdefault_start\n0\t3\t3\tnone\t0\n1\t3\t3\tnone\t1\n");

        Assert.Equal(1U, world.DefaultStartingLevelId);
        Assert.True(world.Levels.Single(level => level.LevelId == 1).IsDefaultStart);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorld.LoadTsv(
            "duplicate-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tdefault_start\n0\t3\t3\tnone\t1\n1\t3\t3\tnone\t1\n"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorld.LoadTsv(
            "invalid-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tdefault_start\n0\t3\t3\tnone\t2\n"));
    }

    [Fact]
    public void LoadsAndValidatesMultiplayerDeathDropSuppression()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tsuppress_multiplayer_death_drops\n16\t3\t3\tnone\t1\n");

        Assert.True(world.SuppressMultiplayerDeathDrops(16));
        Assert.False(world.SuppressMultiplayerDeathDrops(1));
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorld.LoadTsv(
            "invalid-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tsuppress_multiplayer_death_drops\n16\t3\t3\tnone\t2\n"));
    }

    [Fact]
    public void MovementAndMissileBlockingGeometryAreIndependent()
    {
        var level = new AuthoritativeLevel(
            1,
            3,
            1,
            blockedCells: [0],
            missileBlockedCells: [1]);

        Assert.False(level.IsWalkable(0, 0));
        Assert.False(level.IsMissileBlocked(0, 0));
        Assert.True(level.IsWalkable(1, 0));
        Assert.True(level.IsMissileBlocked(1, 0));
        Assert.True(level.IsMissileBlocked(3, 0));
    }

    [Fact]
    public void LoadsExplicitMissileGeometryWhileLegacyTablesDefaultToMovementGeometry()
    {
        var explicitWorld = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tmissile_blocked_cells\n1\t3\t1\t0\t1\n");
        var legacyWorld = AuthoritativeWorld.LoadTsv(
            "legacy-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t1\t0\n");

        Assert.False(explicitWorld.IsMissileBlocked(1, 0, 0));
        Assert.True(explicitWorld.IsMissileBlocked(1, 1, 0));
        Assert.True(legacyWorld.IsMissileBlocked(1, 0, 0));
    }

    [Fact]
    public void NoneCellSentinelDeclaresEmptyMovementAndMissileGeometry()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tmissile_blocked_cells\n1\t3\t1\tnone\tNONE\n");

        Assert.True(world.IsWalkable(1, 0, 0));
        Assert.False(world.IsMissileBlocked(1, 0, 0));
    }

    [Fact]
    public void LoadsAndValidatesExternalObjectMissileCollisionFlags()
    {
        const string header = "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tmissile_passable\tbreakable\tdestroyed\n";
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            header + "20\t4\t1\t2\t3\t0\t1\t1\n");

        var @object = Assert.Single(objects);
        Assert.True(@object.Breakable);
        Assert.True(@object.Destroyed);
        Assert.False(@object.MissilePassable);
        Assert.Throws<ArgumentException>(() => AuthoritativeWorldObjectCatalog.LoadTsv(
            "invalid-objects.tsv",
            header + "20\t4\t1\t2\t3\t1\t1\t0\n"));
        Assert.Throws<ArgumentException>(() => new AuthoritativeWorldObject(
            20,
            4,
            1,
            2,
            3,
            destroyed: true));
    }

    [Fact]
    public void LoadsExternalArmedTrapState()
    {
        const string header = "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\ttrap_armed\n";
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            header + "20\t4\t1\t2\t3\t1\n");

        Assert.True(Assert.Single(objects).TrapArmed);
    }

    [Fact]
    public void MissileLineOfSightUsesGeometryWhileMovingCollisionIncludesObjects()
    {
        var @object = new AuthoritativeWorldObject(20, 4, 0, 1, 0);
        var world = new DiabloWorldState(
            fallbackWidth: 3,
            fallbackHeight: 1,
            objects: [@object]);

        Assert.True(world.HasMissileLineOfSight(0, 0, 0, 2, 0));
        Assert.False(world.IsMissileGeometryBlocked(0, 1, 0));
        Assert.True(world.IsMissileBlocked(0, 1, 0));
    }

    [Fact]
    public void LineOfSightRejectsBlockedIntermediateCells()
    {
        var level = new AuthoritativeLevel(1, 5, 3, [1 * 5 + 2]);

        Assert.False(level.HasLineOfSight(0, 1, 4, 1));
        Assert.True(level.HasLineOfSight(0, 0, 4, 0));
    }
}
