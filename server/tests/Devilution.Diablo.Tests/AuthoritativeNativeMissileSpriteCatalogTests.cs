using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeMissileSpriteCatalogTests
{
    [Fact]
    public void LoadsDirectionalSpriteTiming()
    {
        var values = string.Join(',', Enumerable.Repeat("2", 16));
        var catalog = AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            "native_missile_sprites.tsv",
            "missile_id\twidth\talternate_width\tname\tframe_count\tflags\tframe_delay\tframe_length\n"
                + $"Fireball\t96\t16\tfireba\t16\t\t{values}\t{values}\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal("Fireball", definition.MissileId);
        Assert.Equal((96, 16, 16), (
            definition.Width,
            definition.AlternateWidth,
            definition.FrameCount));
        Assert.Equal(16, definition.FrameDelay.Count);
        Assert.Equal(2, definition.FrameLength[15]);
    }

    [Fact]
    public void RejectsInvalidDirectionalTimingAndDuplicateIdentities()
    {
        var header = "missile_id\twidth\talternate_width\tname\tframe_count\tflags\tframe_delay\tframe_length\n";
        var values = string.Join(',', Enumerable.Repeat("0", 16));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            "native_missile_sprites.tsv",
            header + $"Fireball\t96\t16\t\t1\t\t0\t{values}\n"));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeMissileSpriteCatalog([
            new("Fireball", 96, 16, "fireba", 1, "", [.. Enumerable.Repeat(0, 16)], [.. Enumerable.Repeat(1, 16)]),
            new("Fireball", 96, 16, "fireba", 1, "", [.. Enumerable.Repeat(0, 16)], [.. Enumerable.Repeat(1, 16)]),
        ]));
    }

    [Fact]
    public void AllowsNativeNegativeAlternateWidthOffsets()
    {
        var values = Enumerable.Repeat(1, 16).ToArray();
        var catalog = new AuthoritativeNativeMissileSpriteCatalog([
            new("OrangeFlareExplosion", 96, -12, "ex_ora1", 1, "MonsterOwned", values, values),
        ]);

        Assert.Equal(-12, catalog.GetRequired("OrangeFlareExplosion").AlternateWidth);
    }
}
