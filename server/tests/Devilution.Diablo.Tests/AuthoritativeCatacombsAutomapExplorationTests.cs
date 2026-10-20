using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** L2 catalog projection follows the native SetAutomapView neighbor cases and atomic guards. */
public sealed class AuthoritativeCatacombsAutomapExplorationTests
{
    [Theory]
    [InlineData(AuthoritativeCathedralAutomapTileType.Vertical, false, "0,0;-1,0")]
    [InlineData(AuthoritativeCathedralAutomapTileType.Vertical, true, "0,0;0,1")]
    [InlineData(AuthoritativeCathedralAutomapTileType.Horizontal, false, "0,0;0,-1")]
    [InlineData(AuthoritativeCathedralAutomapTileType.Horizontal, true, "0,0;1,0")]
    [InlineData(AuthoritativeCathedralAutomapTileType.Cross, false, "0,0;-1,0;0,-1;-1,-1")]
    [InlineData(AuthoritativeCathedralAutomapTileType.Cross, true, "0,0;0,1;1,0")]
    [InlineData(AuthoritativeCathedralAutomapTileType.FenceVertical, false, "0,0;-1,0")]
    [InlineData(AuthoritativeCathedralAutomapTileType.FenceVertical, true, "0,0;0,-1;0,1")]
    [InlineData(AuthoritativeCathedralAutomapTileType.FenceHorizontal, false, "0,0;0,-1")]
    [InlineData(AuthoritativeCathedralAutomapTileType.FenceHorizontal, true, "0,0;-1,0;1,0")]
    [InlineData(AuthoritativeCathedralAutomapTileType.None, false, "0,0")]
    public void NativeNeighborBranchesRetainExplorationPriority(
        AuthoritativeCathedralAutomapTileType type, bool solid, string offsets)
    {
        var metadata = Load(type, solid);
        var tiles = Enumerable.Repeat((ushort)2, 1600).ToArray();
        tiles[10 + 10 * 40] = 1;
        var exploration = new byte[1600];
        metadata.ProjectExplorationAtMap(tiles, 10, 10, 4, exploration.AsSpan());
        metadata.ProjectExplorationAtMap(tiles, 10, 10, 2, exploration.AsSpan());

        var expected = new byte[1600];
        foreach (var pair in offsets.Split(';')) {
            var xy = pair.Split(',').Select(int.Parse).ToArray();
            expected[10 + xy[0] + (10 + xy[1]) * 40] = 4;
        }
        Assert.Equal(expected, exploration);
    }

    [Fact]
    public void NativeOriginRoundingAndMapEdgesRemainBounded()
    {
        var metadata = Load(AuthoritativeCathedralAutomapTileType.Cross, false);
        var tiles = Enumerable.Repeat((ushort)1, 1600).ToArray();
        var exploration = new byte[1600];
        metadata.ProjectExploration(tiles, 15, 15, 3, exploration);
        Assert.Equal(3, exploration[0]); // native signed division truncates -1 / 2 to zero
        Assert.Equal(1, exploration.Count(value => value != 0));
        metadata.ProjectExploration(tiles, 14, 14, 4, exploration);
        Assert.Equal(3, exploration[0]);
        var result = metadata.ProjectExploration(tiles, 94, 94, 2);
        Assert.Equal(2, result[1599]);
        Assert.Equal(0, result[0]);
    }

    [Fact]
    public void UnsupportedHotTailAndMalformedInputAreRejectedBeforeAnyWrite()
    {
        var metadata = Load(AuthoritativeCathedralAutomapTileType.None, false);
        var tiles = Enumerable.Repeat((ushort)160, 1600).ToArray();
        var exploration = Enumerable.Repeat((byte)1, 1600).ToArray();
        metadata.ProjectExploration(tiles, 36, 36, 4, exploration);
        var before = exploration.ToArray();
        tiles[1599] = 161;
        Assert.Throws<InvalidDataException>(() => metadata.ProjectExploration(tiles, 36, 36, 4, exploration));
        tiles[1599] = 160;
        Assert.Throws<InvalidDataException>(() => metadata.ProjectExploration(tiles, 36, 36, 5, exploration));
        Assert.Throws<InvalidDataException>(() => metadata.ProjectExploration(tiles[..^1], 36, 36, 4, exploration));
        Assert.Throws<ArgumentException>(() => metadata.ProjectExploration(tiles, 36, 36, 4, new byte[1599]));
        Assert.Equal(before, exploration);
    }

    [Fact]
    public void CornerArchCorrectionAndNativeL2OverrideUseResolvedCatalog()
    {
        var words = new ushort[160];
        words[0] = Pack(AuthoritativeCathedralAutomapTileType.Cross, AuthoritativeCathedralAutomapTileFlags.Dirt
            | AuthoritativeCathedralAutomapTileFlags.VerticalArch);
        words[1] = Pack(AuthoritativeCathedralAutomapTileType.Corner, AuthoritativeCathedralAutomapTileFlags.Dirt);
        words[2] = Pack(AuthoritativeCathedralAutomapTileType.None, AuthoritativeCathedralAutomapTileFlags.HorizontalArch);
        words[41] = Pack(AuthoritativeCathedralAutomapTileType.Cross, AuthoritativeCathedralAutomapTileFlags.Dirt);
        var metadata = Load(words);
        var tiles = new ushort[1600];
        tiles[10 + 10 * 40] = 1;
        tiles[10 + 11 * 40] = 2;
        tiles[9 + 11 * 40] = 3;
        var actual = metadata.ProjectExplorationAtMap(tiles, 10, 10, 4);
        Assert.Equal(4, actual[10 + 10 * 40]);
        Assert.Equal(0, actual[10 + 11 * 40]);
        tiles[10 + 10 * 40] = 42;
        tiles[10 + 9 * 40] = 2;
        actual = metadata.ProjectExplorationAtMap(tiles, 10, 10, 4);
        Assert.Equal(4, actual[10 + 9 * 40]); // source row41 is a non-solid horizontal fence
    }

    private static AuthoritativeCatacombsAutomapMetadata Load(
        AuthoritativeCathedralAutomapTileType type, bool solid)
    {
        var words = new ushort[160];
        words[0] = Pack(type, solid ? AuthoritativeCathedralAutomapTileFlags.Dirt : 0);
        words[1] = Pack(AuthoritativeCathedralAutomapTileType.Corner, AuthoritativeCathedralAutomapTileFlags.Dirt);
        return Load(words);
    }

    private static ushort Pack(AuthoritativeCathedralAutomapTileType type, AuthoritativeCathedralAutomapTileFlags flags)
        => (ushort)((byte)type | ((byte)flags << 8));

    private static AuthoritativeCatacombsAutomapMetadata Load(ushort[] words)
    {
        var bytes = new byte[words.Length * 2];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        var tables = AuthoritativeCatacombsAutomapMetadata.CreateTables(words,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
            AuthoritativeCatacombsAutomapMetadata.CatalogId,
            AuthoritativeCatacombsAutomapMetadata.SchemaVersion, tables,
            schema: new ContentSchemaIdentity(AuthoritativeCatacombsAutomapMetadata.SchemaId,
                AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));
    }
}
