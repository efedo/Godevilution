using System.Collections.ObjectModel;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldTests
{
    [Fact]
    public void LoadsContentDefinedStartPositionAndKeepsLegacyDefault()
    {
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\tstart_x\tstart_y\n"
                + "0\ttown\t4\t4\tnone\t0\t2\t3\n"
                + "1\tcathedral\t4\t4\tnone\t1\t0\t0\n");

        Assert.Equal((2, 3), world.GetStartPosition(0));
        Assert.Equal((0, 0), world.GetStartPosition(1));
        Assert.Equal((2, 3), (world.Levels[0].StartPositionX, world.Levels[0].StartPositionY));

        var legacyWorld = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\n"
                + "1\t4\t4\n");
        Assert.Equal((0, 0), legacyWorld.GetStartPosition(1));
    }

    [Fact]
    public void RejectsAContentDefinedStartPositionThatIsBlocked()
    {
        var exception = Assert.Throws<InvalidDataException>(() => AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tstart_x\tstart_y\n"
                + "1\t3\t3\t4\t1\t1\n"));

        Assert.Contains("invalid start position", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvesStableLevelSymbolsIncludingNativeTownZero()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
            new StableContentId("level", "cathedral", 1),
            new StableContentId("portal", "cathedral_entrance", 1),
            new StableContentId("warp", "grave_matters_dungeon", 7),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\n"
                + "0\ttown\t3\t3\tnone\t0\n"
                + "1\tcathedral\t3\t3\tnone\t1\n",
            stableIds);

        Assert.Equal("cathedral", Assert.Single(world.Levels, level => level.IsDefaultStart).StableSymbol);
        Assert.Equal(1U, world.DefaultStartingLevelId);
    }

    [Fact]
    public void WarpAndPortalLevelSymbolsResolveAgainstStableIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
            new StableContentId("level", "cathedral", 1),
            new StableContentId("warp", "grave_matters_dungeon", 7),
            new StableContentId("portal", "cathedral_entrance", 1),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\n"
                + "0\ttown\t3\t3\tnone\n"
                + "1\tcathedral\t3\t3\tnone\n",
            stableIds);
        var warp = Assert.Single(AuthoritativeWarpCatalog.LoadTsv(
            "warps.tsv",
            "warp_id\twarp_symbol\tsource_level_id\tsource_level_symbol\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_level_symbol\ttarget_x\ttarget_y\n"
                + "7\tgrave_matters_dungeon\t0\ttown\t1\t1\t0\ttown\t2\t2\n",
            world,
            stableIds));
        var portal = Assert.Single(AuthoritativePortalCatalog.LoadTsv(
            "portals.tsv",
            "portal_id\tportal_symbol\tsource_level_id\tsource_level_symbol\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_level_symbol\tdestination_position_x\tdestination_position_y\n"
                + "1\tcathedral_entrance\t0\ttown\t1\t1\t1\tcathedral\t2\t2\n",
            stableIds));

        Assert.Equal(0U, warp.SourceLevelId);
        Assert.Equal("grave_matters_dungeon", warp.StableSymbol);
        Assert.Equal(1U, portal.DestinationLevelId);
        Assert.Equal("cathedral_entrance", portal.StableSymbol);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativePortalCatalog.LoadTsv(
                "portals.tsv",
                "portal_id\tportal_symbol\tsource_level_id\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_position_x\tdestination_position_y\n"
                    + "2\tcathedral_entrance\t0\t1\t1\t1\t2\t2\n",
                stableIds));
        Assert.Contains("maps portal 2 to symbol 'cathedral_entrance'", exception.Message, StringComparison.Ordinal);

        var warpException = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWarpCatalog.LoadTsv(
                "warps.tsv",
                "warp_id\twarp_symbol\tsource_level_id\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_x\ttarget_y\n"
                    + "8\tgrave_matters_dungeon\t0\t1\t1\t0\t2\t2\n",
                world,
                stableIds));
        Assert.Contains("maps warp 8 to symbol 'grave_matters_dungeon'", warpException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoredMonsterObjectAndQuestLevelLinksResolveStableSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "cathedral", 1),
        ]);
        var monster = Assert.Single(AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tlevel_symbol\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t1\t1\tcathedral\t1\t1\t10\t10\t2\n",
            stableIds: stableIds).Targets);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tlevel_symbol\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                + "9\t1\t1\tcathedral\t1\t1\tNone\t0\n",
            stableIds));
        var quest = Assert.Single(AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\tlevel_symbol\trequired_progress\n"
                + "3001\t1\tcathedral\t1\n",
            stableIds: stableIds));

        Assert.Equal(1U, monster.LevelId);
        Assert.Equal(1U, @object.LevelId);
        Assert.Equal(1U, quest.LevelId);
    }

    [Fact]
    public void TemplateOnlyObjectRowsAreExcludedFromWorldAndAvailableForMappings()
    {
        const string objects =
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\ttemplate_only\n"
            + "20\t4\t1\t1\t1\tNone\t0\t0\n"
            + "21\t40\t\t\t\tGrantSpellLevel\t7\t1\n";

        var placed = AuthoritativeWorldObjectCatalog.LoadTsv("objects.tsv", objects);
        var templates = AuthoritativeWorldObjectCatalog.LoadTemplates("objects.tsv", objects);

        Assert.Equal(20U, Assert.Single(placed).EntityId);
        var template = Assert.Single(templates);
        Assert.Equal(21U, template.EntityId);
        Assert.Equal(40U, template.ObjectId);
        Assert.Equal(0U, template.LevelId);
        Assert.Equal((0, 0), (template.PositionX, template.PositionY));
        Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, template.EffectKind);
        Assert.Equal(7, template.EffectAmount);
    }

    [Fact]
    public void OrdinaryObjectRowsStillRequirePlacementCoordinates()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.LoadTsv(
                "objects.tsv",
                "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                    + "20\t4\t1\t\t1\tNone\t0\n"));
    }

    [Fact]
    public void ResolvesStableWorldObjectSymbolsAndRejectsMismatches()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("object", "l1_left_door", 1),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tobject_symbol\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                + "20\t1\tl1_left_door\t1\t2\t2\tNone\t0\n",
            stableIds));

        Assert.Equal(1U, @object.ObjectId);
        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.LoadTsv(
                "objects.tsv",
                "entity_id\tobject_id\tobject_symbol\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                    + "20\t2\tl1_left_door\t1\t2\t2\tNone\t0\n",
                stableIds));
        Assert.Contains("maps object 2 to symbol 'l1_left_door'", exception.Message, StringComparison.Ordinal);
    }

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
    public void ProjectedGeometryRetainsNativeTransparencyLayer()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            2,
            2,
            nativeTransparency: [1, 2, 3, 4]));

        var state = new DiabloWorldState(world);

        Assert.Equal([1, 2, 3, 4], state.LevelGeometry.Single().NativeTransparency);
    }

    [Fact]
    public void LateGeneratedLevelsUpdateTheAuthoritativeProjection()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 3, 3));
        var state = new DiabloWorldState(world);

        state.AddLevel(new AuthoritativeLevel(2, 4, 4));
        Assert.True(state.IsWalkable(2, 3, 3));
        Assert.Contains(state.LevelGeometry, level => level.LevelId == 2);

        state.ReplaceLevel(new AuthoritativeLevel(2, 4, 4, blockedCells: [5]));
        Assert.False(state.IsWalkable(2, 1, 1));
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
    public void RejectsNativePresentationValuesOutsideCompatibilityRanges()
    {
        var signedTransparency = new AuthoritativeLevel(
            1,
            1,
            1,
            nativeTransparency: [128]);
        Assert.Equal((ushort)128, signedTransparency.NativeTransparency[0]);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            1,
            1,
            nativeTransparency: [256]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            1,
            1,
            nativeMegaTileLayer: [256]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(1, 113, 1));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            82,
            82,
            nativeMegaTileLayer: Enumerable.Repeat<ushort>(0, 41 * 41)));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            1,
            1,
            nativeObjectLayer: [(ushort)(AuthoritativeLevel.NativeMaximumDunObjectLayerId + 1)]));
        var worldObject = new AuthoritativeWorldObject(
            1,
            1,
            1,
            0,
            0,
            nativeObjectId: (uint)AuthoritativeLevel.NativeMaximumObjectId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            worldObject.NativeObjectId = (uint)(AuthoritativeLevel.NativeMaximumObjectId + 1));
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
    public void RejectsDuplicateWarpSourceTriggers()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 8, 8));
        world.AddLevel(new AuthoritativeLevel(2, 8, 8));
        world.AddLevel(new AuthoritativeLevel(3, 8, 8));
        world.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 2, 2));

        var exception = Assert.Throws<InvalidDataException>(() => world.AddWarpTarget(
            new AuthoritativeWarpTarget(8, 1, 1, 1, 3, 3, 3)));

        Assert.Contains("duplicates an existing source trigger", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WarpDistanceDoesNotWrapForExtremeCallerCoordinates()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 8, 8));
        world.AddLevel(new AuthoritativeLevel(2, 8, 8));
        world.AddLevel(new AuthoritativeLevel(3, 8, 8));
        world.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 0, 0, 2, 1, 1));
        world.AddWarpTarget(new AuthoritativeWarpTarget(8, 1, 1, 0, 3, 1, 1));

        var nearest = world.FindNearestWarpTarget(1, int.MinValue, 0);

        Assert.Equal(7U, nearest!.WarpId);
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
            [new AuthoritativePortal(0, 1, 1, 1, 2, 2, 2)],
            world));
        Assert.Throws<InvalidDataException>(() => AuthoritativePortalCatalog.ValidateWorld(
            [
                new AuthoritativePortal(4, 1, 1, 1, 2, 2, 2),
                new AuthoritativePortal(4, 1, 0, 0, 2, 1, 1),
            ],
            world));
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
    public void RuntimeWorldStateRejectsInvalidPortalIdentities()
    {
        var zeroException = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            portals: [new AuthoritativePortal(0, 1, 1, 1, 2, 2, 2)]));
        Assert.Contains("Portal identity cannot be zero", zeroException.Message, StringComparison.Ordinal);

        var duplicateException = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            portals: [
                new AuthoritativePortal(4, 1, 1, 1, 2, 2, 2),
                new AuthoritativePortal(4, 1, 0, 0, 2, 1, 1),
            ]));
        Assert.Contains("Portal 4 is registered more than once", duplicateException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeWorldStateRejectsInvalidPortalOwnershipAndLifetime()
    {
        var missingOwnerException = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            portals: [new AuthoritativePortal(5, 1, 1, 1, 2, 2, 2) {
                Bidirectional = true,
                RemainingTicks = 10,
            }]));
        Assert.Contains("Portal 5 has invalid ownership or lifetime metadata", missingOwnerException.Message, StringComparison.Ordinal);

        var staticLifetimeException = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            portals: [new AuthoritativePortal(6, 1, 1, 1, 2, 2, 2) {
                OwnerEntityId = 9,
            }]));
        Assert.Contains("Portal 6 has invalid ownership or lifetime metadata", staticLifetimeException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeWorldStateRejectsOversizedInitialPortalCollection()
    {
        var portals = Enumerable.Range(1, AuthoritativePortalRules.NativeMaximumPortalCount + 1)
            .Select(id => new AuthoritativePortal((uint)id, 0, id, 0, 1, id, 1))
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            portals: portals));
        Assert.Contains("too many portal entries", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeWorldStateRejectsNativeMonsterPoolOverflowPerLevel()
    {
        var monsters = Enumerable.Range(1, AuthoritativeLevel.NativeMaximumMonsterCount + 1)
            .Select(entityId => new AuthoritativeCombatTarget(
                (uint)entityId,
                1,
                0,
                1) {
                LevelId = 1,
            })
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            combatTargets: monsters));
        Assert.Contains("native monster capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticPortalCatalogRejectsOversizedRouteSet()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var portals = Enumerable.Range(1, AuthoritativePortalRules.NativeMaximumPortalCount + 1)
            .Select(id => new AuthoritativePortal((uint)id, 0, id, 0, 1, id, 1))
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativePortalCatalog.ValidateWorld(portals, world));
        Assert.Contains("native portal capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeWorldStateRejectsOversizedInitialCollections()
    {
        var corpses = Enumerable.Range(1, DiabloWorldState.MaximumWorldStateEntries + 1)
            .Select(entityId => new AuthoritativeCorpse((uint)entityId, 0, 0, 0))
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            corpses: corpses));
        Assert.Contains("too many corpse entries", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialWorldValidationRejectsMutatedPortalDictionaryEntries()
    {
        var mismatchedKeyWorld = new DiabloWorldState();
        mismatchedKeyWorld.Portals.Add(
            7,
            new AuthoritativePortal(8, 0, 0, 0, 0, 1, 1));
        var keyException = Assert.Throws<InvalidDataException>(() =>
            mismatchedKeyWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("Portal dictionary key 7 does not match portal identity 8", keyException.Message, StringComparison.Ordinal);

        var invalidMetadataWorld = new DiabloWorldState();
        invalidMetadataWorld.Portals.Add(
            9,
            new AuthoritativePortal(9, 0, 0, 0, 0, 1, 1) {
                OwnerEntityId = 3,
            });
        var metadataException = Assert.Throws<InvalidDataException>(() =>
            invalidMetadataWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("Portal 9 has invalid ownership or lifetime metadata", metadataException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialWorldValidationRejectsMutatedEntityDictionaryKeys()
    {
        var target = new AuthoritativeCombatTarget(10, 0, 0, 5);
        var targetWorld = new DiabloWorldState(combatTargets: [target]);
        targetWorld.CombatTargets.Clear();
        targetWorld.CombatTargets.Add(11, target);
        var targetException = Assert.Throws<InvalidDataException>(() =>
            targetWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("monster dictionary key 11 does not match entity identity 10", targetException.Message, StringComparison.Ordinal);

        var projectile = new AuthoritativeProjectile(
            20,
            10,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            AuthoritativeDamageType.Magic,
            0,
            1);
        var projectileWorld = new DiabloWorldState(projectiles: [projectile]);
        projectileWorld.Projectiles.Clear();
        projectileWorld.Projectiles.Add(21, projectile);
        var projectileException = Assert.Throws<InvalidDataException>(() =>
            projectileWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("projectile dictionary key 21 does not match entity identity 20", projectileException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialWorldValidationRejectsInvalidProjectileState()
    {
        var outOfBoundsProjectile = new AuthoritativeProjectile(
            30,
            10,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            AuthoritativeDamageType.Magic,
            0,
            1) {
            PositionX = 40,
        };
        var outOfBoundsWorld = new DiabloWorldState(projectiles: [outOfBoundsProjectile]);
        var positionException = Assert.Throws<InvalidDataException>(() =>
            outOfBoundsWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("Projectile 30 has invalid initial state", positionException.Message, StringComparison.Ordinal);

        var expiredProjectile = new AuthoritativeProjectile(
            31,
            10,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            AuthoritativeDamageType.Magic,
            0,
            1) {
            RemainingTicks = 0,
        };
        var expiredWorld = new DiabloWorldState(projectiles: [expiredProjectile]);
        var lifetimeException = Assert.Throws<InvalidDataException>(() =>
            expiredWorld.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("Projectile 31 has invalid initial state", lifetimeException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialWorldValidationRejectsMutatedQuestDictionaryKeys()
    {
        var quest = new AuthoritativeQuestState(40, 0, 1);
        var world = new DiabloWorldState(quests: [quest]);
        world.Quests.Clear();
        world.Quests.Add(41, quest);

        var exception = Assert.Throws<InvalidDataException>(() =>
            world.ValidateInitialPlayerPosition(0, 0, 0));
        Assert.Contains("Quest dictionary key 41 does not match quest identity 40", exception.Message, StringComparison.Ordinal);
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
    public void LoadsAuthoredObjectDialogueMessageIdentity()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tdialogue_message_id\n"
                + "20\t4\t1\t2\t3\t255\n"));

        Assert.Equal(255, @object.DialogueMessageId);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthoritativeWorldObject(
            21,
            4,
            1,
            2,
            3,
            dialogueMessageId: short.MaxValue + 1));
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
    public void WorldStateRejectsCrossCategoryEntityIdCollisions()
    {
        var target = new AuthoritativeCombatTarget(10, 1, 0, 5);
        var worldItem = new AuthoritativeWorldItem(
            10,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty);
        var worldObject = new AuthoritativeWorldObject(10, 4, 0, 1, 0);
        var projectile = new AuthoritativeProjectile(
            10,
            11,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            AuthoritativeDamageType.Physical,
            0,
            1);
        var corpse = new AuthoritativeCorpse(10, 0, 1, 0);

        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            combatTargets: [target],
            worldItems: [worldItem]));
        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            combatTargets: [target],
            objects: [worldObject]));
        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]));
        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
            combatTargets: [target],
            corpses: [corpse]));
    }

    [Fact]
    public void EntityIdOccupancyIncludesAllWorldCategories()
    {
        var world = new DiabloWorldState(
            combatTargets: [new AuthoritativeCombatTarget(10, 1, 0, 5)],
            portals: [new AuthoritativePortal(11, 0, 0, 0, 0, 1, 1)],
            worldItems: [new AuthoritativeWorldItem(
                12,
                0,
                1,
                0,
                42,
                75,
                AuthoritativeItemState.Empty)],
            objects: [new AuthoritativeWorldObject(13, 4, 0, 1, 0)],
            projectiles: [new AuthoritativeProjectile(
                14,
                10,
                0,
                1,
                0,
                0,
                0,
                1,
                0,
                1,
                AuthoritativeDamageType.Physical,
                0,
                1)],
            corpses: [new AuthoritativeCorpse(15, 0, 1, 0)]);

        Assert.True(world.IsEntityIdOccupied(10));
        Assert.True(world.IsEntityIdOccupied(11));
        Assert.True(world.IsEntityIdOccupied(12));
        Assert.True(world.IsEntityIdOccupied(13));
        Assert.True(world.IsEntityIdOccupied(14));
        Assert.True(world.IsEntityIdOccupied(15));
        Assert.False(world.IsEntityIdOccupied(16));
    }

    [Fact]
    public void LineOfSightRejectsBlockedIntermediateCells()
    {
        var level = new AuthoritativeLevel(1, 5, 3, [1 * 5 + 2]);

        Assert.False(level.HasLineOfSight(0, 1, 4, 1));
        Assert.True(level.HasLineOfSight(0, 0, 4, 0));
    }

    [Fact]
    public void LineOfSightRejectsEndpointsOutsideTheLevelBeforeArithmetic()
    {
        var level = new AuthoritativeLevel(1, 8, 8);

        Assert.False(level.HasLineOfSight(int.MinValue, 0, 0, 0));
        Assert.False(level.HasLineOfSight(0, 0, int.MaxValue, 0));
    }

    [Fact]
    public void FallbackWorldLineOfSightRejectsEndpointsOutsideTheLevelBeforeArithmetic()
    {
        var world = new DiabloWorldState(fallbackWidth: 8, fallbackHeight: 8);

        Assert.False(world.HasLineOfSight(1, int.MinValue, 0, 0, 0));
        Assert.False(world.HasLineOfSight(1, 0, 0, int.MaxValue, 0));
    }

    [Fact]
    public void NativeTransparencyRegionsMatchNativePlacementSemantics()
    {
        var level = new AuthoritativeLevel(
            1,
            3,
            2,
            nativeTransparency: [4, 4, 7, 4, 7, 7]);

        Assert.True(level.IsSameTransparencyRegion(0, 0, 1, 0));
        Assert.False(level.IsSameTransparencyRegion(0, 0, 2, 0));
        Assert.True(new AuthoritativeLevel(2, 2, 2)
            .IsSameTransparencyRegion(0, 0, 1, 1));
    }

    [Fact]
    public void NativeVisionLineOfSightHonorsDunBlockLightPieces()
    {
        var pieces = new ushort[5];
        pieces[1] = 7;
        var level = new AuthoritativeLevel(
            1,
            5,
            1,
            nativeTransparency: [1, 1, 1, 1, 1],
            nativePieceLayer: pieces,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            });

        Assert.False(level.HasNativeVisionLineOfSight(0, 0, 4, 0));
        Assert.True(level.HasNativeVisionLineOfSight(0, 0, 1, 0));
    }

    [Fact]
    public void RejectsNativeDPieceLayersAndPropertiesOutsideMaxTilesRange()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            2,
            2,
            nativePieceLayer: [1379, 0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1,
            2,
            2,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [1379] = AuthoritativeDunTileFlags.None,
            }));
    }

    [Fact]
    public void NativeVisionLineOfSightUsesTheNativeRayTable()
    {
        var level = new AuthoritativeLevel(1, 16, 16, [1]);

        Assert.True(level.HasNativeVisionLineOfSight(0, 0, 8, 4));

        var cornerBlocked = new AuthoritativeLevel(1, 16, 16, [1, 16]);
        Assert.False(cornerBlocked.HasNativeVisionLineOfSight(0, 0, 8, 4));
    }
}
