using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeDunMapAssetTests
{
    [Fact]
    public void ParserPreservesNativeLayersAndDoubledTileDimensions()
    {
        var asset = AuthoritativeDunMapAsset.Load(
            "levels/l2data/bonestr1.dun",
            File.ReadAllBytes(Path.Combine(
                FindRepositoryRoot(),
                "test",
                "fixtures",
                "levels",
                "l2data",
                "bonestr1.dun")));

        Assert.Equal(7, asset.MegaWidth);
        Assert.Equal(7, asset.MegaHeight);
        Assert.Equal(14, asset.TileWidth);
        Assert.Equal(14, asset.TileHeight);
        Assert.Equal(49, asset.TileLayer.Count);
        Assert.Equal(196, asset.ReservedLayer.Count);
        Assert.Equal(196, asset.MonsterLayer.Count);
        Assert.Equal(196, asset.ObjectLayer.Count);
        Assert.Equal(196, asset.TransparencyLayer.Count);
        Assert.Equal(0, asset.GetObjectId(0, 0));
        Assert.Equal(0, asset.GetMonsterId(13, 13));
    }

    [Fact]
    public void ParserRejectsTruncatedAndTrailingAssets()
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "test",
            "fixtures",
            "levels",
            "l1data",
            "banner1.dun"));

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunMapAsset.Load("banner1.dun", bytes[..^2]));
        var trailingBytes = new byte[bytes.Length + 2];
        Array.Copy(bytes, trailingBytes, bytes.Length);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunMapAsset.Load("banner1.dun", trailingBytes));
    }

    [Fact]
    public void LayerAccessRejectsCoordinatesOutsideNativeMap()
    {
        var asset = AuthoritativeDunMapAsset.Load(
            "levels/l1data/banner1.dun",
            File.ReadAllBytes(Path.Combine(
                FindRepositoryRoot(),
                "test",
                "fixtures",
                "levels",
                "l1data",
                "banner1.dun")));

        Assert.Throws<ArgumentOutOfRangeException>(() => asset.GetObjectId(16, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => asset.GetTransparencyValue(0, -1));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
