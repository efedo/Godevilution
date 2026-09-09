using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldMutationTests
{
    [Fact]
    public void LoadsAndNormalizesCellOverlayDefinitions()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tblocked_cells_add\tblocked_cells_remove\tmissile_blocked_cells_add\tmissile_blocked_cells_remove\n"
                + "7\t1\t6, 6\t5\tnone\t5\n");

        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(7U, mutation.MutationId);
        Assert.Equal([6], mutation.BlockedCellsAdd);
        Assert.Equal([5], mutation.BlockedCellsRemove);
        Assert.Equal([], mutation.MissileBlockedCellsAdd);
        Assert.Equal([5], mutation.MissileBlockedCellsRemove);
    }

    [Fact]
    public void LoadsAndExpandsGeometryRectanglesDeterministically()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tgeometry_rect_add\tgeometry_rect_remove\n"
                + "7\t1\t1,1,2,2\t0,0,1,1;2,3,1,1\n");

        var mutation = Assert.Single(catalog.Definitions);
        var rectangle = Assert.Single(mutation.GeometryRectanglesAdd);
        Assert.Equal((1, 1, 2, 2), (rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
        Assert.Equal([5, 6, 9, 10], mutation.GetBlockedCells(4, 4));
        Assert.Equal([0, 14], mutation.GetBlockedCellsToRemove(4, 4));
        Assert.Equal(mutation.GetBlockedCells(4, 4), mutation.GetMissileBlockedCells(4, 4));
    }

    [Fact]
    public void AppliesGeometryRectanglesToWalkAndMissileProjection()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 2, 2)]),
        ]);
        var world = new DiabloWorldState(geometry, worldMutations: mutations);

        Assert.True(world.ApplyWorldMutation(7));
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 2));
        Assert.True(world.IsMissileGeometryBlocked(1, 1, 1));
        Assert.True(world.IsWalkable(1, 0, 0));
    }

    [Fact]
    public void RestoreWorldMutationsLeavesExistingStateWhenRequestedIdIsInvalid()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var world = new DiabloWorldState(
            geometry,
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    7,
                    1,
                    geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 1, 1)]),
            ]));
        Assert.True(world.ApplyWorldMutation(7));
        Assert.False(world.IsWalkable(1, 1, 1));

        Assert.Throws<InvalidDataException>(() => world.RestoreWorldMutations([99]));

        Assert.Equal([7U], world.ActiveWorldMutationIds);
        Assert.False(world.IsWalkable(1, 1, 1));
    }

    [Fact]
    public void RejectsOutOfBoundsOrOverlappingGeometryRectangles()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    7,
                    1,
                    geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(3, 3, 2, 2)]),
            ]).ValidateWorld(geometry));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    8,
                    1,
                    blockedCellsRemove: [5],
                    geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 2, 2)]),
            ]).ValidateWorld(geometry));
    }

    [Fact]
    public void LoadsAndProjectsTransparencyRectangles()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\ttransparency_rects\n"
                + "7\t1\t1,1,2,1,9\n");
        var mutation = Assert.Single(catalog.Definitions);
        var rectangle = Assert.Single(mutation.TransparencyRectangles);
        Assert.Equal((1, 1, 2, 1, (ushort)9), (
            rectangle.X,
            rectangle.Y,
            rectangle.Width,
            rectangle.Height,
            rectangle.Value));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            4,
            4,
            nativeTransparency: Enumerable.Repeat<ushort>(0, 16)));
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)9, level.NativeTransparency[5]);
        Assert.Equal((ushort)9, level.NativeTransparency[6]);
        Assert.Equal((ushort)0, level.NativeTransparency[4]);
    }

    [Fact]
    public void RejectsTransparencyWithoutLayerOrWithOverlappingAssignments()
    {
        var noLayer = new AuthoritativeWorld();
        noLayer.AddLevel(new AuthoritativeLevel(1, 2, 2));
        var mutation = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                transparencyRectangles: [
                    new AuthoritativeWorldMutationTransparencyRectangle(0, 0, 1, 1, 9),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => mutation.ValidateWorld(noLayer));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            2,
            2,
            nativeTransparency: Enumerable.Repeat<ushort>(0, 4)));
        var overlapping = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                8,
                1,
                transparencyRectangles: [
                    new AuthoritativeWorldMutationTransparencyRectangle(0, 0, 2, 1, 9),
                    new AuthoritativeWorldMutationTransparencyRectangle(1, 0, 1, 1, 8),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => overlapping.ValidateWorld(geometry));
    }

    [Fact]
    public void ExplicitTransparencyLayerCreationProjectsWithoutNativeLayer()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\ttransparency_rects\tcreate_transparency_layer\n"
                + "7\t1\t1,1,1,1,9\ttrue\n");
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 2, 2));
        catalog.ValidateWorld(geometry);
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal(new ushort[] { 0, 0, 0, 9 }, level.NativeTransparency);
    }

    [Fact]
    public void LoadsAndProjectsNativeObjectLayerRectangles()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tobject_layer_rects\n"
                + "7\t1\t1,1,2,1,14\n");
        var mutation = Assert.Single(catalog.Definitions);
        var rectangle = Assert.Single(mutation.ObjectLayerRectangles);
        Assert.Equal((1, 1, 2, 1, (ushort)14), (
            rectangle.X,
            rectangle.Y,
            rectangle.Width,
            rectangle.Height,
            rectangle.ObjectId));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            4,
            4,
            nativeObjectLayer: Enumerable.Repeat<ushort>(0, 16)));
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)14, level.NativeObjectLayer[5]);
        Assert.Equal((ushort)14, level.NativeObjectLayer[6]);
        Assert.Equal((ushort)0, level.NativeObjectLayer[4]);
    }

    [Fact]
    public void RejectsObjectLayerValuesOutsideRawDunCompatibilityRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tobject_layer_rects\n"
                    + "7\t1\t0,0,1,1,147\n"));
    }

    [Fact]
    public void RejectsPieceLayerValuesOutsideMaxTilesRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tpiece_layer_rects\n"
                    + "7\t1\t0,0,1,1,1379\n"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoritativeWorldMutationPieceRectangle(0, 0, 1, 1, 1379));
    }

    [Fact]
    public void PreservesSignedTransparencyWireBytesAndRejectsValuesOutsideNativeRanges()
    {
        var transparency = new AuthoritativeWorldMutationTransparencyRectangle(0, 0, 1, 1, 255);
        Assert.Equal((ushort)255, transparency.Value);
        var signedTransparency = new AuthoritativeWorldMutationTransparencyRectangle(0, 0, 1, 1, 128);
        Assert.Equal((ushort)128, signedTransparency.Value);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\ttransparency_rects\n"
                    + "7\t1\t0,0,1,1,256\n"));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tmega_tile_assignments\n"
                    + "7\t1\t0,0,256\n"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoritativeWorldMutationTransparencyRectangle(0, 0, 1, 1, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoritativeWorldMutationMegaTileAssignment(0, 0, 256));
    }

    [Fact]
    public void LoadsAndProjectsNativePieceLayerRectangles()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tpiece_layer_rects\n"
                + "7\t1\t1,1,2,1,299\n");
        var mutation = Assert.Single(catalog.Definitions);
        var rectangle = Assert.Single(mutation.PieceLayerRectangles);
        Assert.Equal((1, 1, 2, 1, (ushort)299), (
            rectangle.X,
            rectangle.Y,
            rectangle.Width,
            rectangle.Height,
            rectangle.PieceId));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)299, level.NativePieceLayer[5]);
        Assert.Equal((ushort)299, level.NativePieceLayer[6]);
        Assert.Equal((ushort)0, level.NativePieceLayer[4]);
    }

    [Fact]
    public void LoadsAndAppliesExternalPieceCollisionFlags()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tpiece_layer_rects\tpiece_property_overrides\n"
                + "7\t1\t1,1,1,1,299\t299:solid|block_missile\n");
        var mutation = Assert.Single(catalog.Definitions);
        var property = Assert.Single(mutation.PiecePropertyOverrides);
        Assert.Equal((ushort)299, property.PieceId);
        Assert.Equal(
            AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile,
            property.Flags);

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)299, level.NativePieceLayer[5]);
        Assert.Equal(property.Flags, level.NativePieceProperties[299]);
        Assert.Contains(5, level.NativePieceCollisionCells);
        Assert.Contains(5, level.NativePieceMissileBlockedCells);
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.True(world.IsMissileGeometryBlocked(1, 1, 1));
    }

    [Fact]
    public void RejectsInvalidOrDuplicateExternalPieceCollisionFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tpiece_property_overrides\n"
                    + "7\t1\t1379:none\n"));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tpiece_property_overrides\n"
                    + "7\t1\t299:unknown\n"));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tpiece_property_overrides\n"
                    + "7\t1\t299:none;299:solid\n"));
    }

    [Fact]
    public void LoadsAndProjectsAnchoredMegaTileAssignments()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_unique_identity\tanchor_mega_offset_x\tanchor_mega_offset_y\tmega_tile_assignments\n"
                + "7\t1\tSnotspill\t2\t2\t0,0,136;1,1,135\n");
        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal((2, 2), (mutation.AnchorMegaOffsetX, mutation.AnchorMegaOffsetY));
        Assert.Equal(
            [(2 * 4 + 2, (ushort)136), (3 * 4 + 3, (ushort)135)],
            mutation.GetMegaTileCells(8, 8, 2, 2));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            nativeMegaTileLayer: Enumerable.Repeat<ushort>(0, 16)));
        var target = new AuthoritativeCombatTarget(
            20,
            2,
            2,
            100,
            levelId: 1,
            unique: true,
            uniqueIdentity: "Snotspill");
        var world = new DiabloWorldState(
            geometry,
            combatTargets: [target],
            worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)136, level.NativeMegaTileLayer[2 * 4 + 2]);
        Assert.Equal((ushort)135, level.NativeMegaTileLayer[3 * 4 + 3]);
    }

    [Fact]
    public void SetLevelAnchoredMegaTileAssignmentsUseThePublishedOrigin()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_set_level_id\tanchor_mega_offset_x\tanchor_mega_offset_y\tmega_tile_assignments\n"
                + "7\t1\t6\t2\t2\t0,0,136\n");
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            nativeMegaTileLayer: Enumerable.Repeat<ushort>(0, 16)));
        var world = new DiabloWorldState(
            geometry,
            worldMutations: catalog);
        world.SetSetLevelOriginResolver(
            _ => new AuthoritativeSetLevelObjectOrigin(1, 2, 2));

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)136, level.NativeMegaTileLayer[2 * 4 + 2]);
    }

    [Fact]
    public void RejectsDuplicateOrUnalignedMegaTileAssignments()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            nativeMegaTileLayer: Enumerable.Repeat<ushort>(0, 16)));
        var duplicate = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                megaTileAssignments: [
                    new AuthoritativeWorldMutationMegaTileAssignment(0, 0, 136),
                    new AuthoritativeWorldMutationMegaTileAssignment(0, 0, 135),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => duplicate.ValidateWorld(geometry));

        var target = new AuthoritativeCombatTarget(
            20,
            2,
            2,
            100,
            levelId: 1,
            unique: true,
            uniqueIdentity: "Snotspill");
        var unaligned = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                8,
                1,
                anchorUniqueIdentity: "Snotspill",
                anchorMegaOffsetX: 1,
                megaTileAssignments: [
                    new AuthoritativeWorldMutationMegaTileAssignment(0, 0, 136),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => unaligned.ValidateWorld(geometry, combatTargets: [target]));

        var outOfBounds = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                9,
                1,
                megaTileAssignments: [
                    new AuthoritativeWorldMutationMegaTileAssignment(4, 0, 136),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => outOfBounds.ValidateWorld(geometry));
    }

    [Fact]
    public void CopiesNativeMegaTilesFromAnAuthoredSourceLevel()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tmega_tile_copy_source_level_id\tmega_tile_copy_rects\n"
                + "7\t1\t2\t1,0,1,2\n");
        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(2U, mutation.MegaTileCopySourceLevelId);
        var rectangle = Assert.Single(mutation.MegaTileCopyRectangles);
        Assert.Equal((1, 0, 1, 2), (rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            4,
            4,
            nativeMegaTileLayer: [0, 0, 0, 0],
            nativePieceLayer: new ushort[16]));
        geometry.AddLevel(new AuthoritativeLevel(
            2,
            4,
            4,
            nativeMegaTileLayer: [101, 102, 103, 104],
            nativePieceLayer: Enumerable.Range(1, 16).Select(value => (ushort)value),
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [3] = AuthoritativeDunTileFlags.Solid,
                [4] = AuthoritativeDunTileFlags.BlockMissile,
                [7] = AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile,
                [8] = AuthoritativeDunTileFlags.None,
                [11] = AuthoritativeDunTileFlags.Solid,
                [12] = AuthoritativeDunTileFlags.BlockMissile,
                [15] = AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile,
                [16] = AuthoritativeDunTileFlags.None,
            }));
        catalog.ValidateWorld(geometry);
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var target = Assert.Single(world.LevelGeometry, level => level.LevelId == 1);
        Assert.Equal([0, 102, 0, 104], target.NativeMegaTileLayer);
        Assert.Equal([0, 0, 3, 4, 0, 0, 7, 8, 0, 0, 11, 12, 0, 0, 15, 16], target.NativePieceLayer);
        Assert.Equal([2, 6, 10, 14], target.BlockedCells);
        Assert.Equal([3, 6, 11, 14], target.MissileBlockedCells);
    }

    [Fact]
    public void DefersSetLevelMutationValidationUntilOptionalMapBundlesLoad()
    {
        var targetLevelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            5,
            activeMap: true);
        var sourceLevelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            5,
            activeMap: false);
        var catalog = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                9302,
                targetLevelId,
                megaTileCopySourceLevelId: sourceLevelId,
                megaTileCopyRectangles: [new AuthoritativeWorldMutationRectangle(1, 1, 9, 10)]),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t40\t40\tnone\n");

        catalog.ValidateWorld(world);
    }

    [Fact]
    public void RecomputesNativeCollisionWhenAProjectedDPieceChanges()
    {
        var catalog = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                pieceLayerRectangles: [
                    new AuthoritativeWorldMutationPieceRectangle(0, 0, 1, 1, 13),
                    new AuthoritativeWorldMutationPieceRectangle(1, 0, 1, 1, 12),
                    new AuthoritativeWorldMutationPieceRectangle(0, 1, 1, 1, 13),
                ]),
        ]);
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            2,
            2,
            blockedCells: [0, 3],
            missileBlockedCells: [1, 2, 3],
            nativePieceLayer: [10, 11, 12, 13],
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [10] = AuthoritativeDunTileFlags.Solid,
                [11] = AuthoritativeDunTileFlags.BlockMissile,
                [12] = AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile,
                [13] = AuthoritativeDunTileFlags.None,
            },
            nativePieceCollisionCells: [0, 2],
            nativePieceMissileBlockedCells: [1, 2]));
        var world = new DiabloWorldState(geometry, worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal([1, 3], level.BlockedCells);
        Assert.Equal([1, 3], level.MissileBlockedCells);
        Assert.Equal([13, 12, 13, 13], level.NativePieceLayer);
    }

    [Fact]
    public void AnchoredObjectLayerMutationUsesRuntimeObjectPosition()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_entity_id\tobject_layer_rects\n"
                + "7\t1\t20\t-1,0,1,1,99;0,0,1,1,97\n");
        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(20U, mutation.AnchorEntityId);

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var anchor = new AuthoritativeWorldObject(20, 6003, 1, 2, 2);
        catalog.ValidateWorld(geometry, [anchor]);
        var world = new DiabloWorldState(
            geometry,
            objects: [anchor],
            worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        var level = Assert.Single(world.LevelGeometry);
        Assert.Equal(16, level.NativeObjectLayer.Count);
        Assert.Equal((ushort)99, level.NativeObjectLayer[2 * 4 + 1]);
        Assert.Equal((ushort)97, level.NativeObjectLayer[2 * 4 + 2]);
    }

    [Fact]
    public void AnchoredGeometryMutationUsesUniqueEncounterPosition()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_unique_identity\tgeometry_rect_add\n"
                + "7\t1\tSnotspill\t-1,0,1,1\n");
        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal("Snotspill", mutation.AnchorUniqueIdentity);

        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var target = new AuthoritativeCombatTarget(
            20,
            2,
            2,
            100,
            levelId: 1,
            unique: true,
            uniqueIdentity: "Snotspill");
        var world = new DiabloWorldState(
            geometry,
            combatTargets: [target],
            worldMutations: catalog);

        Assert.True(world.ApplyWorldMutation(7));
        Assert.False(world.IsWalkable(1, 1, 2));
        Assert.True(world.IsWalkable(1, 0, 0));
    }

    [Fact]
    public void SetLevelAnchoredMutationDefersUntilOriginIsPublished()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_set_level_id\tgeometry_rect_add\n"
                + "7\t1\t6\t0,0,1,1\n");
        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(6, mutation.AnchorSetLevelId);

        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var world = new DiabloWorldState(geometry, worldMutations: catalog);
        world.SetSetLevelOriginResolver(_ => null);
        world.RestoreWorldMutations([7]);
        Assert.True(world.IsWalkable(1, 0, 0));

        world.SetSetLevelOriginResolver(_ => new AuthoritativeSetLevelObjectOrigin(1, 2, 2));
        Assert.False(world.IsWalkable(1, 2, 2));
    }

    [Fact]
    public void SetLevelAnchorResolvesStableSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_ARENA_CHURCH", 6),
        ]);
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tanchor_set_level_symbol\tgeometry_rect_add\n"
                + "7\t1\tSL_ARENA_CHURCH\t0,0,1,1\n",
            stableIds);

        Assert.Equal(6, Assert.Single(catalog.Definitions).AnchorSetLevelId);
    }

    [Fact]
    public void RejectsObjectAndUniqueIdentityAnchorsTogether()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeWorldMutation(
            7,
            1,
            anchorEntityId: 20,
            anchorUniqueIdentity: "Snotspill"));
    }

    [Fact]
    public void RejectsMultipleWorldMutationAnchorKinds()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeWorldMutation(
            7,
            1,
            anchorEntityId: 20,
            anchorSetLevelId: 6));
    }

    [Fact]
    public void RejectsOverlappingObjectLayerAssignments()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            2,
            2,
            nativeObjectLayer: Enumerable.Repeat<ushort>(0, 4)));
        var overlapping = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                8,
                1,
                objectLayerRectangles: [
                    new AuthoritativeWorldMutationObjectRectangle(0, 0, 2, 1, 14),
                    new AuthoritativeWorldMutationObjectRectangle(1, 0, 1, 1, 15),
                ]),
        ]);
        Assert.Throws<InvalidDataException>(() => overlapping.ValidateWorld(geometry));
    }

    [Fact]
    public void ObjectLayerMutationRematerializesMappedObjects()
    {
        var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true);
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            levelId,
            4,
            4,
            nativeObjectLayer: Enumerable.Repeat<ushort>(0, 16)));
        var nativeObjects = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 14, 25, 0),
        ]);
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                levelId,
                objectLayerRectangles: [
                    new AuthoritativeWorldMutationObjectRectangle(1, 1, 1, 1, 14),
                ]),
        ]);
        var world = new DiabloWorldState(
            geometry,
            worldMutations: mutations,
            nativeDunObjects: nativeObjects);

        Assert.Empty(world.Objects);
        Assert.True(world.ApplyWorldMutation(7));
        var materialized = Assert.Single(world.Objects.Values);
        Assert.Equal(
            AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(2, true, 5),
            materialized.EntityId);
        Assert.Equal((1, 1), (materialized.PositionX, materialized.PositionY));
        Assert.Equal(25U, materialized.ObjectId);

        world.RestoreWorldMutations([]);
        Assert.Empty(world.Objects);
    }

    [Fact]
    public void GeometryMutationPreservesExistingMappedObjectState()
    {
        var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true);
        var entityId = AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(2, true, 5);
        var existing = new AuthoritativeWorldObject(entityId, 25, levelId, 1, 1, activated: true);
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            levelId,
            4,
            4,
            nativeObjectLayer: [0, 0, 0, 0, 0, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        var nativeObjects = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 14, 25, 0),
        ]);
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                levelId,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(0, 0, 1, 1)]),
        ]);
        var world = new DiabloWorldState(
            geometry,
            objects: [existing],
            worldMutations: mutations,
            nativeDunObjects: nativeObjects);

        Assert.Same(existing, world.Objects[entityId]);
        Assert.True(world.ApplyWorldMutation(7));
        Assert.Same(existing, world.Objects[entityId]);
        Assert.True(world.Objects[entityId].Activated);
    }

    [Fact]
    public void ResolvesSetLevelMutationTargetsToRuntimeLevels()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_BONECHAMB", 2),
        ]);
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tset_level_id\tset_level_symbol\tactive_map\tblocked_cells_add\n"
                + "7\t2\tSL_BONECHAMB\t1\t4\n",
            stableIds);

        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true),
            mutation.LevelId);
        Assert.Equal([4], mutation.BlockedCellsAdd);
    }

    [Fact]
    public void RejectsAmbiguousWorldMutationLevelTargets()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tset_level_id\tblocked_cells_add\n"
                    + "7\t1\t2\t4\n"));
    }

    [Fact]
    public void RejectsActiveMapOnOrdinaryWorldMutationTarget()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tactive_map\tblocked_cells_add\n"
                    + "7\t1\t1\t4\n"));
    }

    [Fact]
    public void AppliesMutationToWalkAndMissileGeometry()
    {
        var worldGeometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tmissile_blocked_cells\n"
                + "1\t4\t4\t5\t5\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(7, 1, [6], [5], [6], [5]),
        ]);
        var world = new DiabloWorldState(worldGeometry, worldMutations: mutations);

        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.True(world.IsWalkable(1, 2, 1));
        Assert.True(world.IsMissileGeometryBlocked(1, 1, 1));

        Assert.True(world.ApplyWorldMutation(7));
        Assert.True(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 1));
        Assert.True(world.IsMissileGeometryBlocked(1, 2, 1));
        Assert.Contains(7U, world.ActiveWorldMutationIds);

        world.RestoreWorldMutations([7]);
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void ObjectActivationAppliesValidatedMutation()
    {
        var worldGeometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\t5\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(7, 1, [6], [5]),
        ]);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7);
        var world = new DiabloWorldState(
            worldGeometry,
            objects: [@object],
            worldMutations: mutations);
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.True(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void ObjectRewardsAcceptTheNativeGatewayBookOffsetEnvelope()
    {
        Assert.True(AuthoritativeWorldObject.IsValidReward(
            AuthoritativeObjectRewardKind.NormalItem,
            2143,
            0,
            9,
            17));
        Assert.False(AuthoritativeWorldObject.IsValidReward(
            AuthoritativeObjectRewardKind.NormalItem,
            2143,
            0,
            33,
            0));
    }

    [Fact]
    public void ObjectTableLoadsQuestTransitionRequirements()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\tquest_transition_kind\tquest_transition_amount\tquest_transition_required_variable1\n"
                + "20\t4\t1\t1\t0\t30\tWorldMutation\t7\tActivateAndSetVariable1\t1\t0\n"));

        Assert.Equal(
            AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            @object.QuestTransitionKind);
        Assert.Equal(1, @object.QuestTransitionAmount);
        Assert.Equal(0, @object.QuestTransitionRequiredVariable1);
    }

    [Fact]
    public void ObjectTableLoadsValidatedRewardMetadata()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\treward_kind\treward_id\treward_item_level\treward_offset_x\treward_offset_y\n"
                + "20\t4\t1\t1\t0\tNormalItem\t100\t3\t0\t1\n"));

        Assert.Equal(AuthoritativeObjectRewardKind.NormalItem, @object.RewardKind);
        Assert.Equal(100U, @object.RewardId);
        Assert.Equal(3, @object.RewardItemLevel);
        Assert.Equal(1, @object.RewardOffsetY);
    }

    [Fact]
    public void ObjectTableLoadsSetLevelRewardOriginMetadata()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\treward_kind\treward_id\treward_origin\treward_set_level_id\n"
                + "20\t4\t1\t1\t0\tNormalItem\t100\tset_level_origin\t6\n"));

        Assert.Equal(AuthoritativeObjectRewardOriginKind.SetLevelOrigin, @object.RewardOriginKind);
        Assert.Equal(6, @object.RewardSetLevelId);
    }

    [Fact]
    public void ObjectTableLoadsSpellLevelGrant()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevel\t7\n"));

        Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
    }

    [Fact]
    public void ObjectTableLoadsSpellProjectileEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tSpawnSpellProjectile\t7\tphasing\t1\t-2\n",
            stableIds));

        Assert.Equal(AuthoritativeObjectEffectKind.SpawnSpellProjectile, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal((1, -2), (@object.ProjectileTargetOffsetX, @object.ProjectileTargetOffsetY));
    }

    [Fact]
    public void ObjectTableLoadsActivatingPlayerProjectileSource()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_source\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tSpawnSpellProjectile\t0\tphasing\tActivatingPlayer\t1\t-2\n",
            stableIds));

        Assert.Equal(
            AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
            @object.ProjectileSourceKind);
    }

    [Fact]
    public void ObjectTableLoadsCompositeSpellGrantAndProjectileEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "guardian", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevelAndSpawnProjectile\t0\tguardian\t1\t0\n",
            stableIds));

        Assert.Equal(
            AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
            @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal((1, 0), (@object.ProjectileTargetOffsetX, @object.ProjectileTargetOffsetY));
    }

    [Fact]
    public void ObjectTableLoadsQuestSequenceFollowUp()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_spell_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\n"
                + "20\t4\t1\t1\t0\t30\tQuestSequence\t1\tSpawnSpellProjectile\t0\tphasing\t1\t-2\n",
            stableIds));

        Assert.Equal(AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile, @object.QuestSequenceFollowUpKind);
        Assert.Equal(7U, @object.QuestSequenceFollowUpAmount);
        Assert.Equal((1, -2), (@object.QuestSequenceFollowUpOffsetX, @object.QuestSequenceFollowUpOffsetY));
    }

    [Fact]
    public void MonsterFollowUpValidationRejectsAnUnknownStableMonsterReference()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster,
            questSequenceFollowUpAmount: 99);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 7, "Native Seven A", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, false, false, false),
            new AuthoritativeMonsterTypeDefinition(
                7, 7, "Native Seven B", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, false, false, false),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateMonsterRequirements(
                [@object],
                monsterTypes));

        Assert.Contains("unknown monster 99", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MutationValidationRejectsAnUnknownSecondaryFollowUpMutation()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster,
            questSequenceFollowUpAmount: 137,
            questSequenceFollowUpWorldMutationId: 99);
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(137, 1),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateMutationRequirements([@object], mutations));

        Assert.Contains("unknown world mutation 99", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectTableLoadsWorldMutationProjectileFollowUp()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_spell_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\n"
                + "20\t4\t1\t1\t0\tWorldMutation\t7\tSpawnSpellProjectile\t0\tphasing\t1\t0\n",
            stableIds));

        Assert.Equal(AuthoritativeObjectEffectKind.WorldMutation, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal(
            AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            @object.QuestSequenceFollowUpKind);
        Assert.Equal(7U, @object.QuestSequenceFollowUpAmount);
        Assert.Equal((1, 0), (@object.QuestSequenceFollowUpOffsetX, @object.QuestSequenceFollowUpOffsetY));
    }

    [Fact]
    public void WorldMutationRejectsNonProjectileFollowUp()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            questSequenceFollowUpAmount: 8));
    }

    [Fact]
    public void ObjectTableResolvesStableSpellSymbolForSpellLevelGrant()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "guardian", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevel\t0\tguardian\n",
            stableIds));

        Assert.Equal(7, @object.EffectAmount);
    }

    [Fact]
    public void ObjectActivationGrantsOneCatalogSpellLevel()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevel,
            effectAmount: 7);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var learned = Assert.Single(player.MemorizedSpells);
        Assert.Equal(new MemorizedSpellState(7, 1), learned);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationSpawnsValidatedPhasingProjectile()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileTargetOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, projectile.Behavior);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal(2, projectile.PositionX);
        Assert.Equal(0, projectile.PositionY);
        Assert.Equal(2U, projectile.RemainingTicks);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationCanSpawnPhasingProjectileFromActivatingPlayer()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal((0, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((2, 0), (projectile.TargetX, projectile.TargetY));
    }

    [Fact]
    public void CompositeObjectEffectGrantsSpellAndSpawnsProjectileAtomically()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            41,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileTargetOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(new MemorizedSpellState(7, 1), Assert.Single(player.MemorizedSpells));
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, projectile.Behavior);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.True(@object.Activated);
    }

    [Fact]
    public void SetLevelQuestBookCompletesChamberQuestAndSpawnsGuardianFromPlayer()
    {
        var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            2,
            activeMap: true);
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            $"level_id\twidth\theight\tblocked_cells\n{levelId}\t8\t8\tnone\n");
        var quest = new AuthoritativeQuestState(
            3015,
            6,
            1,
            active: true);
        var @object = new AuthoritativeWorldObject(
            20,
            1,
            levelId,
            4,
            4,
            questId: 3015,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: -2,
            projectileTargetOffsetY: -4,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.CompleteQuest);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            quests: [quest]);
        var player = CreatePlayer();
        player.LevelId = levelId;
        player.PositionX = 3;
        player.PositionY = 4;
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3015,
                14,
                "The Chamber of Bone",
                6,
                6,
                "DTYPE_CATACOMBS",
                2,
                false),
        ]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 13, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 16,
                ProjectileBehavior = AuthoritativeProjectileBehavior.GuardianController,
                ProjectileLifetime = 256,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                SpellLevelDamage = 1,
                DamageBonus = 1,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 1,
                ManaAdjustment = 2,
                ManaAdjustmentDivisor = 1,
                MinimumManaCost = 30,
                ManaCostScale = 64,
                GuardianDurationTickScale = 16,
                GuardianMinimumDuration = 30,
                GuardianMaximumDuration = 30,
                GuardianCharacterLevelDurationDivisor = 2,
                GuardianSpawnSearchRadius = 5,
                GuardianTargetSearchRadius = 6,
                GuardianFireInterval = 16,
                GuardianProjectileSpeedLevelStep = 2,
                GuardianProjectileSpeedMaximumBonus = 47,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(new MemorizedSpellState(7, 1), Assert.Single(player.MemorizedSpells));
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.GuardianController, projectile.Behavior);
        Assert.Equal((3, 4), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((2, 0), (projectile.TargetX, projectile.TargetY));
    }

    [Fact]
    public void WorldMutationObjectAppliesMutationAndFollowUpProjectile()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            41,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 9,
            questSequenceFollowUpOffsetX: 1,
            questSequenceFollowUpOffsetY: 0);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6]),
            ]));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(9, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(9U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.True(@object.Activated);
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void WorldMutationFollowUpCanLaunchPhasingFromActivatingPlayer()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            41,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 9,
            questSequenceFollowUpOffsetX: 1,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6]),
            ]));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(9, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal((0, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((2, 0), (projectile.TargetX, projectile.TargetY));
    }

    [Fact]
    public void QuestSequenceAppliesFollowUpMutationOnlyOnExpectedStep()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\t5\n");
        var quest = new AuthoritativeQuestState(30, 1, 5, active: false);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            questSequenceFollowUpAmount: 7);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6], [5]),
            ]));
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, quest.Variable1);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.False(@object.Activated);

        quest.Variable1 = 1;
        world.RestoreWorldMutations([]);
        result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            2,
            [player]);
        Assert.True(result.Succeeded);
        Assert.Equal(0, quest.Variable1);
        Assert.Empty(world.ActiveWorldMutationIds);

        result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            3,
            [player]);
        Assert.True(result.Succeeded);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
    }

    [Fact]
    public void QuestSequenceSpawnsFollowUpProjectileAtAuthoredOffset()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var quest = new AuthoritativeQuestState(30, 1, 5, active: false);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 7,
            questSequenceFollowUpOffsetX: 1,
            questSequenceFollowUpOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object], quests: [quest]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.False(@object.Activated);
    }

    [Fact]
    public void ObjectActivationRejectsUnsupportedSpellProjectileBehavior()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7);
        var world = new DiabloWorldState(objects: [@object]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.False(result.Succeeded);
        Assert.False(@object.Activated);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void ObjectCatalogRejectsUnsupportedSpellProjectileBehavior()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateSpellRequirements([@object], spells));

        Assert.Contains("unsupported authored projectile behavior", exception.Message);
    }

    [Fact]
    public void ObjectCatalogAcceptsActivatingPlayerSourceForGuardian()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.GuardianController,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                SpellLevelDamage = 1,
                DamageBonus = 1,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 1,
                ManaAdjustment = 2,
                ManaAdjustmentDivisor = 1,
                MinimumManaCost = 30,
                ManaCostScale = 64,
                GuardianDurationTickScale = 16,
                GuardianMinimumDuration = 30,
                GuardianMaximumDuration = 30,
                GuardianCharacterLevelDurationDivisor = 2,
                GuardianSpawnSearchRadius = 5,
                GuardianTargetSearchRadius = 6,
                GuardianFireInterval = 16,
                GuardianProjectileSpeedLevelStep = 2,
                GuardianProjectileSpeedMaximumBonus = 47,
            },
        ]);

        AuthoritativeWorldObjectCatalog.ValidateSpellRequirements([@object], spells);
    }

    [Fact]
    public void ObjectCatalogRejectsProjectileTargetOutsideWorld()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            1,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t2\t2\tnone\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateWorld([@object], world));

        Assert.Contains("projectile target outside its level", exception.Message);
    }

    [Fact]
    public void ObjectActivationGeneratesDeterministicNormalItemReward()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 100,
            rewardItemLevel: 3,
            rewardOffsetY: 1);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(100, 3, 10, 10, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world, items: items).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(100U, reward.State.StableItemId);
        Assert.Equal(1, reward.PositionX);
        Assert.Equal(1, reward.PositionY);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationResolvesSetLevelRewardOrigin()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t6\t6\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 100,
            rewardOriginKind: AuthoritativeObjectRewardOriginKind.SetLevelOrigin,
            rewardSetLevelId: 6,
            rewardOffsetX: 1,
            rewardOffsetY: -1);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(100, 3, 10, 10, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var commands = new DiabloWorldCommandProcessor(world, items: items);
        commands.SetSetLevelOriginResolver(_ => new AuthoritativeSetLevelObjectOrigin(1, 3, 4));

        var result = commands.Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            [CreatePlayer()]);

        Assert.True(result.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal((1U, 4, 3), (reward.LevelId, reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void ObjectActivationAppliesQuestTransitionAndMutationAtomically()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\t5\n");
        var quest = new AuthoritativeQuestState(30, 1, 5, active: false);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            questTransitionAmount: 1,
            questTransitionRequiredVariable1: 0);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6], [5]),
            ]));

        var result = new DiabloWorldCommandProcessor(world).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(1, quest.Variable1);
        Assert.True(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void RejectsUniqueIdentityAnchorThatExceedsTheNativeUtf8ByteLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthoritativeWorldMutation(
            7,
            1,
            anchorUniqueIdentity: new string('é', 65)));
    }

    private static AuthoritativePlayerState CreatePlayer()
        => new(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            1,
            0);
}
