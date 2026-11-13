using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Pins the vanilla item rolls: the shipped 25/66/66 affix chances must use FlipCoin(4) and
 * !FlipCoin(3), and staff/ring/amulet must be always-magic, so a vanilla save seed regenerates
 * the same item. Seeds and names come from the vanilla saves in test/pack_test.cpp.
 */
public sealed class AuthoritativeVanillaItemRollTests
{
    private static readonly AuthoritativeCathedralThemeItemPolicy Policy = new(1);

    private static string Fixtures()
        => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(NativeSourceExportReferenceTests.TextDataRoot())!)!,
            "test", "fixtures", "native-open-metadata");

    private static AuthoritativeCathedralThemeItemMetadata Metadata()
        => AuthoritativeCathedralThemeItemMetadata.LoadTables(AuthoritativeCathedralThemeItemMetadata.TablePaths
            .Select(path => TsvTable.Parse(path, File.ReadAllText(Path.Combine(Fixtures(), path)))));

    [Theory]
    [InlineData(1286225254u, 13, 156, "Steel Ring of accuracy")]
    [InlineData(1140525626u, 1, 143, "Short Bow of atrophy")]
    [InlineData(1187758333u, 2, 118, "Brass Dagger of weakness")]
    [InlineData(1283803700u, 11, 143, "Clumsy Short Bow")]
    [InlineData(1317748726u, 5, 119, "Tin Short Sword of the fool")]
    [InlineData(1970135469u, 2, 48, "Cap of tears")]
    [InlineData(2008721689u, 2, 54, "Rusted Cape of dyslexia")]
    [InlineData(278391972u, 7, 124, "Clumsy Sabre of fragility")]
    [InlineData(1375639122u, 13, 151, "Dull Staff of Lightning")]
    public void VanillaSaveSeedRegeneratesTheVanillaItem(uint seed, int level, int nativeItemIndex, string name)
    {
        // RecreateItem for CF_UPER1 without CF_ONLYGOOD or CF_UNIQUE.
        var request = new AuthoritativeCathedralThemeItemRequest(
            seed, nativeItemIndex, level, 1, false, false, ForceNotUnique: true);

        var result = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(request, Metadata().Catalog, Policy);

        Assert.Equal(name, result.Item.IdentifiedName);
    }

    [Fact]
    public void ShippedRulesUseVanillaAffixChancesAndAlwaysMagicMisc()
    {
        var metadata = Metadata();

        Assert.Equal((25, 66, 66, 74), (metadata.Catalog.Rules.PrefixPercent, metadata.Catalog.Rules.SuffixPercent,
            metadata.Catalog.Rules.OnlyGoodChance, metadata.Catalog.Rules.GoldPercent));
        Assert.Equal([23, 25, 26], metadata.AlwaysMagicMiscIds.Order()); // STAFF, RING, AMULET
    }
}
