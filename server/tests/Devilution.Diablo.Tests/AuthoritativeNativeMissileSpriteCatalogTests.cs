using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeMissileSpriteCatalogTests
{
    [Fact]
    public void AcceptsCompleteProductionSchema()
    {
        var values = string.Join(',', Enumerable.Repeat("2", 16));
        var table = TsvTable.Parse(
            "native_missile_sprites.tsv",
            "missile_id\twidth\talternate_width\tname\tframe_count\tflags\tframe_delay\tframe_length\n"
                + $"Fireball\t96\t16\tfireba\t16\t\t{values}\t{values}\n");

        AuthoritativeNativeMissileSpriteCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void RejectsPartialProductionSchema()
    {
        var table = TsvTable.Parse(
            "native_missile_sprites.tsv",
            "missile_id\twidth\tname\tframe_count\tframe_delay\tframe_length\n"
                + "Fireball\t96\tfireba\t16\t0\t0\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeMissileSpriteCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

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

    [Fact]
    public void RejectsUnknownSpriteFlags()
    {
        var values = string.Join(',', Enumerable.Repeat("0", 16));
        var header = "missile_id\twidth\talternate_width\tname\tframe_count\tflags\tframe_delay\tframe_length\n";

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
                "native_missile_sprites.tsv",
                header + $"Fireball\t96\t16\tfireba\t1\tUnknown\t{values}\t{values}\n"));
        Assert.Contains("unknown flag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCompatibilityCatalogsMayRemainPartialUntilExplicitValidation()
    {
        var values = Enumerable.Repeat(1, 16).ToArray();
        var catalog = new AuthoritativeNativeMissileSpriteCatalog([
            new("Fireball", 96, 16, "fireba", 16, "", values, values),
        ]);

        Assert.Single(catalog.Definitions);
        var exception = Assert.Throws<InvalidDataException>(
            catalog.ValidateRequiredNativeNamespace);

        Assert.Contains("Acid", exception.Message, StringComparison.Ordinal);
    }
}
