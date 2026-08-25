using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunMonsterCatalogTests
{
    [Fact]
    public void TsvLoaderMapsRawLayerIdentityToNativeMonsterIdentity()
    {
        var catalog = AuthoritativeNativeDunMonsterCatalog.LoadTsv(
            "native_dun_monsters.tsv",
            "native_dun_monster_id\tnative_monster_id\n"
                + "1\t0\n155\t51\n");

        Assert.Equal(2, catalog.Definitions.Count);
        Assert.True(catalog.TryGetNativeMonsterId(1, out var zombie));
        Assert.Equal(0U, zombie);
        Assert.True(catalog.TryGetNativeMonsterId(155, out var butcher));
        Assert.Equal(51U, butcher);
        Assert.False(catalog.TryGetNativeMonsterId(2, out _));
    }

    [Fact]
    public void RejectsRawLayerIdentityOutsideNativeConversionTable()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunMonsterCatalog.LoadTsv(
                "native_dun_monsters.tsv",
                "native_dun_monster_id\tnative_monster_id\n158\t0\n"));
    }

    [Fact]
    public void RejectsDuplicateRawLayerIdentity()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunMonsterCatalog([
                new AuthoritativeNativeDunMonsterDefinition(1, 0),
                new AuthoritativeNativeDunMonsterDefinition(1, 1),
            ]));
    }

    [Fact]
    public void ValidatesNativeMonsterReferencesAgainstActiveCatalog()
    {
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 0),
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                0,
                "Zombie",
                1,
                2,
                1,
                1,
                1,
                0,
                1,
                1,
                0,
                0,
                false,
                false,
                false),
        ]);

        catalog.ValidateMonsterTypes(monsterTypes);
    }

    [Fact]
    public void RejectsNativeMonsterReferencesAbsentFromActiveCatalog()
    {
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 51),
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                0,
                "Zombie",
                1,
                2,
                1,
                1,
                1,
                0,
                1,
                1,
                0,
                0,
                false,
                false,
                false),
        ]);

        Assert.Throws<InvalidDataException>(
            () => catalog.ValidateMonsterTypes(monsterTypes));
    }
}
