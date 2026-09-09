using System.Buffers.Binary;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunLevelCatalogTests
{
    [Fact]
    public void TsvLoaderParsesOrdinaryLevelAssetIdentity()
    {
        var catalog = AuthoritativeNativeDunLevelCatalog.LoadTsv(
            "native_dun_levels.tsv",
            "level_id\tlevel_symbol\tasset_path\n"
                + "2\tcatacombs\tlevels/l2data/catacombs.dun\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(2U, definition.LevelId);
        Assert.Equal("catacombs", definition.LevelSymbol);
        Assert.Equal("levels/l2data/catacombs.dun", definition.AssetPath);
    }

    [Fact]
    public void TsvLoaderParsesOptionalCollisionAssets()
    {
        var catalog = AuthoritativeNativeDunLevelCatalog.LoadTsv(
            "native_dun_levels.tsv",
            "level_id\tlevel_symbol\tasset_path\ttile_asset_path\tsol_asset_path\n"
                + "2\tcatacombs\tlevels/catacombs.dun\tlevels/l2data/l2.til\tlevels/l2data/l2.sol\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal("levels/l2data/l2.til", definition.TileAssetPath);
        Assert.Equal("levels/l2data/l2.sol", definition.SolAssetPath);
    }

    [Fact]
    public void AppliesNativeObjectLayerWithoutReplacingCollisionGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            2,
            2,
            2,
            blockedCells: [0],
            missileBlockedCells: [0, 1]) {
            StableSymbol = "catacombs",
        });
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/l2data/catacombs.dun", CreateDun([0, 17, 0, 19])),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "levels/l2data/catacombs.dun"),
        ]);

        catalog.Apply(content, world);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0], level.BlockedCells);
        Assert.Equal([0, 1], level.MissileBlockedCells);
        Assert.Equal([0, 17, 0, 19], level.NativeObjectLayer);
        Assert.Equal("catacombs", level.StableSymbol);
    }

    [Fact]
    public void AppliesOptionalCollisionAssetsAsEffectiveDPieceGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            2,
            2,
            2,
            blockedCells: [3],
            missileBlockedCells: [3]) {
            StableSymbol = "catacombs",
        });
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sol[1] = (byte)AuthoritativeDunTileFlags.Solid;
        sol[2] = (byte)AuthoritativeDunTileFlags.BlockMissile;
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level.dun", CreateDun([0, 0, 0, 0])),
                new ContentAsset("level.til", CreateTil([1, 2, 3, 4])),
                new ContentAsset("level.sol", sol),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "level.dun", "level.til", "level.sol"),
        ]);

        catalog.Apply(content, world);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0], level.BlockedCells);
        Assert.Equal([1], level.MissileBlockedCells);
        Assert.Equal([1, 2, 3, 4], level.NativePieceLayer);
        Assert.Equal([0], level.NativePieceCollisionCells);
        Assert.Equal([1], level.NativePieceMissileBlockedCells);
    }

    [Fact]
    public void AppliesExternalGeometryDefinitionsWhenBinaryInputsAreNotDeclared()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(2, 2, 2) {
            StableSymbol = "catacombs",
        });
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level.dun", CreateDun([0, 0, 0, 0])),
            ]),
        ]));
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            "native_dun_piece_definitions.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "0\t1\t2\t3\t4\n",
            "native_dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_light\tblock_missile\n"
                + "1\ttrue\tfalse\tfalse\n"
                + "2\tfalse\tfalse\ttrue\n"
                + "3\tfalse\tfalse\tfalse\n"
                + "4\tfalse\tfalse\tfalse\n");
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "level.dun"),
        ]);

        catalog.ValidateAssets(content, geometry);
        catalog.Apply(content, world, geometry);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0], level.BlockedCells);
        Assert.Equal([1], level.MissileBlockedCells);
        Assert.Equal([1, 2, 3, 4], level.NativePieceLayer);
        Assert.Equal([0], level.NativePieceCollisionCells);
        Assert.Equal([1], level.NativePieceMissileBlockedCells);
    }

    [Fact]
    public void ExternalGeometryCatalogRejectsDuplicateDefinitions()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunGeometryCatalog.LoadTsv(
                "native_dun_piece_definitions.tsv",
                "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                    + "0\t1\t2\t3\t4\n"
                    + "0\t1\t2\t3\t4\n",
                "native_dun_tile_properties.tsv",
                "piece_id\tsolid\tblock_light\tblock_missile\n"
                    + "1\ttrue\tfalse\tfalse\n"));

        Assert.Contains("duplicate IDs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalGeometryLoaderRequiresBothContentTables()
    {
        var pieceTable = TsvTable.Parse(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "0\t1\t1\t1\t1\n");
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [pieceTable]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunGeometryCatalog.LoadOptional(content));

        Assert.Contains("native_dun_tile_properties.tsv", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesAllReferencedAssetsBeforeActivation()
    {
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level.dun", CreateDun([0, 0, 0, 0])),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(
                2,
                "level.dun",
                "level.til",
                "level.sol"),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateAssets(content));

        Assert.Contains("level.til", exception.Message, StringComparison.Ordinal);
        Assert.Contains("level 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesCollisionBundleBeforeLevelActivation()
    {
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level.dun", CreateDun([0, 0, 0, 0], megaTileId: 43)),
                new ContentAsset("level.til", CreateTil([1, 2, 3, 4])),
                new ContentAsset(
                    "level.sol",
                    new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount]),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "level.dun", "level.til", "level.sol"),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateAssets(content));

        Assert.Contains("unknown mega-tile 43", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsIncompleteCollisionAssetPair()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunLevelCatalog([
                new AuthoritativeNativeDunLevelDefinition(2, "level.dun", "level.til"),
            ]));

        Assert.Contains(".til/.sol asset pair", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishesMultipleLevelReplacementsAtomically()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(2, 2, 2));
        world.AddLevel(new AuthoritativeLevel(3, 2, 2));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level2.dun", CreateDun([1, 0, 0, 0])),
                new ContentAsset("level3.dun", CreateDun(
                    [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "level2.dun"),
            new AuthoritativeNativeDunLevelDefinition(3, "level3.dun"),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.Apply(content, world));

        Assert.Empty(world.Levels.Single(level => level.LevelId == 2).NativeObjectLayer);
        Assert.Empty(world.Levels.Single(level => level.LevelId == 3).NativeObjectLayer);
    }

    [Fact]
    public void RejectsNativeObjectLayerDimensionsThatDoNotMatchLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(2, 2, 2));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("level.dun", CreateDun([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])),
            ]),
        ]));
        var catalog = new AuthoritativeNativeDunLevelCatalog([
            new AuthoritativeNativeDunLevelDefinition(2, "level.dun"),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(content, world));
        Assert.Contains("dimensions", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateDun(ushort[] objectLayer, ushort megaTileId = 0)
    {
        if (objectLayer.Length != 4 && objectLayer.Length != 16)
            throw new ArgumentException("Test DUN layers must be 2x2 or 4x4.", nameof(objectLayer));
        var megaWidth = objectLayer.Length == 4 ? 1 : 2;
        var megaHeight = megaWidth;
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + dPieceCount * 4];
        words[0] = (ushort)megaWidth;
        words[1] = (ushort)megaHeight;
        words[2] = megaTileId;
        Array.Copy(objectLayer, 0, words, 2 + megaCount + dPieceCount * 2, objectLayer.Length);
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++) {
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        }
        return bytes;
    }

    private static byte[] CreateTil(ushort[] pieces)
    {
        var bytes = new byte[pieces.Length * sizeof(ushort)];
        for (var index = 0; index < pieces.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                pieces[index]);
        return bytes;
    }
}
