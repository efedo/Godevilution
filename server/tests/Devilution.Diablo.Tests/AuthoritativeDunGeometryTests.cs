using System.Buffers.Binary;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeDunGeometryTests
{
    [Fact]
    public void MaterializerExpandsMegaTilesUsingNativeCollisionFlags()
    {
        var asset = AuthoritativeDunMapAsset.Load("synthetic.dun", CreateDun(1, 1, [7]));
        var pieces = new AuthoritativeDunPieceCatalog([
            new(7, 10, 11, 12, 13),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.Solid),
            new(11, AuthoritativeDunTileFlags.BlockMissile),
            new(12, AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile),
            new(13, AuthoritativeDunTileFlags.None),
        ]);

        var level = AuthoritativeDunGeometryMaterializer.Materialize(
            101,
            asset,
            pieces,
            properties,
            "SL_SYNTHETIC");

        Assert.Equal(2, level.Width);
        Assert.Equal(2, level.Height);
        Assert.Equal([0, 2], level.BlockedCells);
        Assert.Equal([1, 2], level.MissileBlockedCells);
        Assert.Equal("SL_SYNTHETIC", level.StableSymbol);
    }

    [Fact]
    public void MaterializerRejectsMissingNativeCollisionInputs()
    {
        var asset = AuthoritativeDunMapAsset.Load("synthetic.dun", CreateDun(1, 1, [7]));
        var pieces = new AuthoritativeDunPieceCatalog([new(7, 10, 11, 12, 13)]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.None),
        ]);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunGeometryMaterializer.Materialize(101, asset, pieces, properties));
    }

    [Fact]
    public void CatalogLoadersUseStrictExternalBooleanAndPieceColumns()
    {
        var pieces = AuthoritativeDunPieceCatalog.LoadTsv(
            "dun_pieces.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "7\t10\t11\t12\t13\n");
        var properties = AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\n10\ttrue\tfalse\n");

        Assert.Equal(7U, Assert.Single(pieces.Definitions).MegaTileId);
        Assert.Equal(AuthoritativeDunTileFlags.Solid, Assert.Single(properties.Definitions).Flags);
        Assert.Throws<InvalidDataException>(() => AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\n10\tmaybe\tfalse\n"));
    }

    private static byte[] CreateDun(int megaWidth, int megaHeight, ushort[] tiles)
    {
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + dPieceCount * 4];
        words[0] = (ushort)megaWidth;
        words[1] = (ushort)megaHeight;
        Array.Copy(tiles, 0, words, 2, tiles.Length);
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        return bytes;
    }
}
