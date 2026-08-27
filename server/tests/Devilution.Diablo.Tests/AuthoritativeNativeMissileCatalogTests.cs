using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeMissileCatalogTests
{
    [Fact]
    public void LoadsNativeIdentityAndPresentationMetadata()
    {
        var catalog = AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            "missile_id\tadd_function\tprocess_function\tcast_sound\thit_sound\tgraphic\tflags\t"
                + "movement_distribution\ton_expiry_missile\n"
                + "Firebolt\tAddFirebolt\tProcessGenericProjectile\tSpellFirebolt\tSpellFireHit\t"
                + "Fireball\tFire\tBlockable\tMagmaBallExplosion\n"
                + "MagmaBallExplosion\tAddMissileExplosion\tProcessMissileExplosion\t\t\t"
                + "MagmaBallExplosion\tPhysical\t\t\n");

        Assert.Equal(["Firebolt", "MagmaBallExplosion"],
            catalog.Definitions.Select(definition => definition.MissileId));
        var firebolt = catalog.GetRequired("Firebolt");
        Assert.Equal("AddFirebolt", firebolt.AddFunction);
        Assert.Equal("SpellFirebolt", firebolt.CastSound);
        Assert.Equal("Fire", firebolt.Flags);
        Assert.Equal("Blockable", firebolt.MovementDistribution);
        Assert.Equal("MagmaBallExplosion", firebolt.OnExpiryMissile);
        Assert.True(catalog.TryGet("MagmaBallExplosion", out _));
    }

    [Fact]
    public void RejectsUnknownFlagsMovementAndExpiryReferences()
    {
        const string header = "missile_id\tflags\tmovement_distribution\ton_expiry_missile\n";
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            header + "Firebolt\tUnknown\tBlockable\t\n"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            header + "Firebolt\tFire\tInvalid\t\n"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            header + "Firebolt\tFire\tBlockable\tMissing\n"));
    }

    [Fact]
    public void RejectsDuplicateMissileIdentities()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeMissileCatalog([
            new("Firebolt", "", "", "", "", "", "", "", null),
            new("Firebolt", "", "", "", "", "", "", "", null),
        ]));
    }

    [Fact]
    public void AppendsValidatedCompatibilityAliasesWithoutSourceMetadata()
    {
        var catalog = AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            "missile_id\nFireball\n",
            ["OrangeFlare"]);

        var alias = Assert.Single(
            catalog.Definitions,
            definition => definition.MissileId == "OrangeFlare");
        Assert.True(alias.CompatibilityAlias);
        Assert.True(catalog.TryGet("OrangeFlare", out _));
    }
}
