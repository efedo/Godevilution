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
    public void ComposesValidatedNativeCollisionWhenTileDefinitionsAreSupplied()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\ttile_asset_path\tsol_asset_path\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\tfragment.til\tfragment.sol\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8, blockedCells: [0]));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
                new ContentAsset("fragment.til", CreateTil()),
                new ContentAsset("fragment.sol", CreateSol()),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0, 18, 26], level.BlockedCells);
        Assert.Equal([0, 19, 26], level.MissileBlockedCells);
    }

    [Fact]
    public void AppliesAFragmentRelativeToAUniqueEncounter()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_unique_identity\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t\tSnotspill\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var target = new AuthoritativeCombatTarget(
            0x10000000,
            4,
            4,
            100,
            levelId: 24,
            unique: true,
            uniqueIdentity: "Snotspill");
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(content, world, [], [target]);

        var level = Assert.Single(world.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void RejectsAnIncompleteNativeCollisionAssetPair()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                9,
                -2,
                -2,
                "fragment.til",
                null).Validate());
        Assert.Contains(".til/.sol asset pair", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingOrAmbiguousFragmentAnchors()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            0,
            -2,
            -2).Validate());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            9,
            -2,
            -2,
            AnchorUniqueIdentity: "Snotspill").Validate());
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

    private static byte[] CreateTil()
    {
        var bytes = new byte[43 * sizeof(ushort) * 4];
        var offset = 42 * sizeof(ushort) * 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort), sizeof(ushort)),
            2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort) * 2, sizeof(ushort)),
            3);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort) * 3, sizeof(ushort)),
            4);
        return bytes;
    }

    private static byte[] CreateSol()
    {
        var bytes = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        bytes[1] = (byte)AuthoritativeDunTileFlags.Solid;
        bytes[2] = (byte)AuthoritativeDunTileFlags.BlockMissile;
        bytes[3] = (byte)(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile);
        return bytes;
    }
}
