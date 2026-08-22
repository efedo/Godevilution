using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunFragmentCatalogTests
{
    [Fact]
    public void LoadsAndAppliesAnAnchorRelativeMegaTileFragment()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8, blockedCells: [0]));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0], level.BlockedCells);
        Assert.Equal(16, level.NativeMegaTileLayer.Count);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void RejectsAnOddAnchorRelativeDunOrigin()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(1, 24, "fragment.dun", 9, -1, -2),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(
            content,
            world,
            [new AuthoritativeWorldObject(9, 6003, 24, 4, 4)]));
        Assert.Contains("even d-piece origin", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateDun(ushort tileId)
    {
        var words = new ushort[19];
        words[0] = 1;
        words[1] = 1;
        words[2] = tileId;
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++) {
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        }
        return bytes;
    }
}
