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

    [Fact]
    public void RequiresTheBaseCompatibilityAliasNamespace()
    {
        var catalog = AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            "missile_id\nFireball\n",
            ["OrangeFlare", "YellowFlare", "BlueFlare", "RedFlare"]);

        var exception = Assert.Throws<InvalidDataException>(
            catalog.ValidateCompatibilityAliases);
        Assert.Contains("BlueFlare2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsUnknownGraphicReferences()
    {
        var missileCatalog = AuthoritativeNativeMissileCatalog.LoadTsv(
            "native_missiles.tsv",
            "missile_id\tgraphic\nFirebolt\tFireball\n");
        var spriteCatalog = AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            "native_missile_sprites.tsv",
            "missile_id\twidth\talternate_width\tname\tframe_count\tflags\tframe_delay\tframe_length\n"
                + "Lightning\t96\t16\tlghning\t1\t\t"
                + string.Join(',', Enumerable.Repeat("0", 16)) + "\t"
                + string.Join(',', Enumerable.Repeat("8", 16)) + "\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            missileCatalog.ValidateGraphicReferences(spriteCatalog));
        Assert.Contains("Firebolt", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Fireball", exception.Message, StringComparison.Ordinal);
    }
}
