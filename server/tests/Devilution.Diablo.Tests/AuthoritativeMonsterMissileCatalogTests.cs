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
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.Ranged, succubus.AttackMode);
        Assert.Equal(100, succubus.AvoidanceProbabilityPercent);
    }

    [Fact]
    public void LoadsValidatedAvoidanceModeAndProbability()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tavoidance_probability_percent\tspell_id\tspawn_monster_id\n"
                + "Acid\tAcid\tAcid\tRangedAvoidance\t50\t1\t117\n");

        Assert.True(catalog.TryGet("Acid", out var acid));
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.RangedAvoidance, acid.AttackMode);
        Assert.Equal(50, acid.AvoidanceProbabilityPercent);
        Assert.Equal(1U, acid.SpellId);
        Assert.Equal(117U, acid.SpawnMonsterId);
    }

    [Fact]
    public void LegacyRowsDefaultToCompatibilitySpellIdentity()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n");

        Assert.True(catalog.TryGet("Succubus", out var succubus));
        Assert.Equal(0U, succubus.SpellId);
        Assert.Equal(0U, succubus.SpawnMonsterId);
    }

    [Fact]
    public void LoadsExplicitMonsterArrowBehavior()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "GoatRanged\tArrow\tMonsterArrow\n");

        Assert.True(catalog.TryGet("GoatRanged", out var goatRanged));
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, goatRanged.ProjectileBehavior);
    }

    [Fact]
    public void RejectsAnOutOfRangeAvoidanceProbability()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    0),
            ]));
    }

    [Fact]
    public void ResolvesMonsterSpecificRowsBeforeNativeAiWildcards()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\n"
                + "Bat\tArrow\tTargeted\t0\n"
                + "Bat\tRhino\tBatCharge\t42\n");

        Assert.True(catalog.TryGet("Bat", 42, out var gloom));
        Assert.Equal("Rhino", gloom.MissileId);
        Assert.Equal(42U, gloom.MonsterId);
        Assert.True(catalog.TryGet("Bat", out var wildcard));
        Assert.Equal("Arrow", wildcard.MissileId);
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
