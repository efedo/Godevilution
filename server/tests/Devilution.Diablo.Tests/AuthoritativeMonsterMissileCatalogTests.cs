using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterMissileCatalogTests
{
    [Fact]
    public void LoadsNativeMissileMappingsInStableIdentityOrder()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n"
                + "GoatRanged\tArrow\tTargeted\n");

        Assert.Equal(["GoatRanged", "Succubus"], catalog.Definitions.Select(definition => definition.NativeAi));
        Assert.True(catalog.TryGet("Succubus", out var succubus));
        Assert.Equal("BloodStar", succubus.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, succubus.ProjectileBehavior);
    }

    [Fact]
    public void RejectsUnknownNativeAiIdentity()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "NotARealAi\tArrow\tTargeted\n"));
    }

    [Fact]
    public void RejectsDuplicateNativeAiIdentity()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n"
                + "Succubus\tBloodStar\tBloodStar\n"));
    }
}
