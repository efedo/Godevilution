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

    private static byte[] CreateDun(ushort[] objectLayer)
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
        Array.Copy(objectLayer, 0, words, 2 + megaCount + dPieceCount * 2, objectLayer.Length);
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++) {
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        }
        return bytes;
    }
}
