using System.Collections.ObjectModel;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldTests
{
    [Fact]
    public void ResolvesStableQuestSymbolsForWarpAndObjectLinks()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("quest", "grave_matters", 3017),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\tnone\n2\t3\t3\tnone\n");
        var warp = Assert.Single(AuthoritativeWarpCatalog.LoadTsv(
            "warps.tsv",
            "warp_id\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\trequired_quest_id\trequired_quest_symbol\trequired_quest_completed\n"
                + "7\t1\t1\t1\t2\t2\t2\t3017\tgrave_matters\t1\n",
            world,
            stableIds));
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tactivated\tquest_id\tquest_symbol\teffect_kind\teffect_amount\tmissile_passable\tbreakable\tdestroyed\ttrap_armed\n"
                + "9\t1\t1\t1\t1\t0\t3017\tgrave_matters\tQuestSequence\t1\t0\t0\t0\t0\n",
            stableIds));

        Assert.Equal(3017U, warp.RequiredQuestId);
        Assert.Equal(3017U, @object.QuestId);
    }

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
        Assert.IsType<ReadOnlyCollection<int>>(world.Levels[0].BlockedCells);
        Assert.IsType<ReadOnlyCollection<int>>(world.Levels[0].MissileBlockedCells);
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
    public void LoadsNormalizedWarpTargetsAndRejectsUnknownLevels()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\tnone\n2\t3\t3\tnone\n");
        var targets = AuthoritativeWarpCatalog.LoadTsv(
            "warps.tsv",
            "warp_id\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\n"
                + "7\t1\t1\t1\t2\t2\t2\n",
            world);

        world.AddWarpTarget(Assert.Single(targets));

        Assert.Equal(7U, world.FindNearestWarpTarget(1, 0, 0)!.WarpId);
        Assert.True(world.IsWarpTrigger(1, 1, 1));
        Assert.Equal(0U, world.WarpTargets.Single().RequiredQuestId);
        var gatedTargets = AuthoritativeWarpCatalog.LoadTsv(
            "gated-warps.tsv",
            "warp_id\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\trequired_quest_id\trequired_quest_completed\n"
                + "8\t1\t1\t1\t2\t2\t2\t3017\t1\n",
            world);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWarpCatalog.ValidateQuestRequirements(
            gatedTargets,
            [new AuthoritativeQuestState(3018, 17, 1)]));
        Assert.Throws<InvalidDataException>(() => AuthoritativeWarpCatalog.LoadTsv(
            "invalid-warps.tsv",
            "warp_id\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\n"
                + "8\t1\t1\t1\t9\t0\t0\n",
            world));
        var blockedWorld = AuthoritativeWorld.LoadTsv(
            "blocked-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\t4\n2\t3\t3\tnone\n");
        var blockedTarget = Assert.Single(AuthoritativeWarpCatalog.LoadTsv(
            "blocked-warps.tsv",
            "warp_id\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\n"
                + "9\t1\t1\t1\t2\t2\t2\n",
            blockedWorld));
        Assert.Throws<InvalidDataException>(() => blockedWorld.AddWarpTarget(blockedTarget));
    }

    [Fact]
    public void ValidatesStaticPortalEndpointsAgainstWorldGeometry()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\tnone\n2\t3\t3\tnone\n");
        var portals = AuthoritativePortalCatalog.LoadTsv(
            "portals.tsv",
            "portal_id\tsource_level_id\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_position_x\tdestination_position_y\n"
                + "1\t1\t1\t1\t2\t2\t2\n");

        AuthoritativePortalCatalog.ValidateWorld(portals, world);
        Assert.Throws<InvalidDataException>(() => AuthoritativePortalCatalog.ValidateWorld(
            [new AuthoritativePortal(2, 1, 3, 0, 2, 0, 0)],
            world));
        Assert.Throws<InvalidDataException>(() => AuthoritativePortalCatalog.ValidateWorld(
            [new AuthoritativePortal(3, 1, 0, 0, 9, 0, 0)],
            world));
        var blockedWorld = AuthoritativeWorld.LoadTsv(
            "blocked-levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\t4\n2\t3\t3\tnone\n");
        Assert.Throws<InvalidDataException>(() => AuthoritativePortalCatalog.ValidateWorld(
            portals,
            blockedWorld));
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
    public void ValidatesObjectLevelsAndPositionsAgainstWorldBounds()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t3\t3\tnone\n");
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\n"
                + "20\t4\t1\t2\t2\n");

        AuthoritativeWorldObjectCatalog.ValidateWorld(objects, world);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldObjectCatalog.ValidateWorld(
            [new AuthoritativeWorldObject(21, 4, 9, 0, 0)],
            world));
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldObjectCatalog.ValidateWorld(
            [new AuthoritativeWorldObject(22, 4, 1, 3, 0)],
            world));
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
    public void LoadsExternalQuestProgressAmount()
    {
        const string header = "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\tquest_progress\n";
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            header + "20\t4\t1\t2\t3\t30\t3\n");

        Assert.Equal(3U, Assert.Single(objects).QuestProgressAmount);
    }

    [Fact]
    public void LoadsExternalQuestSequenceObjectSteps()
    {
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\n"
                + "2401\t6001\t24\t20\t20\t3022\tQuestSequence\t1\n");

        var @object = Assert.Single(objects);
        Assert.Equal(AuthoritativeObjectEffectKind.QuestSequence, @object.EffectKind);
        Assert.Equal(1, @object.EffectAmount);
        Assert.Equal(3022U, @object.QuestId);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldObjectCatalog.ValidateQuestRequirements(
            objects,
            [new AuthoritativeQuestState(3023, 24, 1)]));
    }

    [Fact]
    public void ResolvesStableItemSymbolForQuestItemCompletionObjects()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "cathedral_map", 43),
        ]);
        const string header = "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\teffect_item_symbol\n";
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            header + "2404\t6004\t0\t36\t22\t3017\tQuestItemCompletion\t\tcathedral_map\n",
            stableIds));

        Assert.Equal(43, @object.EffectAmount);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            header + "2404\t6004\t0\t36\t22\t3017\tQuestItemCompletion\t2\tcathedral_map\n",
            stableIds));
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
