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
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tavoidance_probability_percent\tspell_id\tspawn_monster_id\tprojectile_speed\tprojectile_lifetime\tprojectile_lifetime_base\tprojectile_lifetime_intelligence_scale\tpuddle_duration_random_range\tpuddle_duration_base\tpuddle_duration_intelligence_scale\tpuddle_damage_minimum_monster_level\tpuddle_damage_low\tpuddle_damage_high\n"
                + "Acid\tAcid\tAcid\tRangedAvoidance\t50\t1\t117\t8\t9\t20\t5\t15\t40\t40\t2\t1\t2\n");

        Assert.True(catalog.TryGet("Acid", out var acid));
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.RangedAvoidance, acid.AttackMode);
        Assert.Equal(50, acid.AvoidanceProbabilityPercent);
        Assert.Equal(1U, acid.SpellId);
        Assert.Equal(117U, acid.SpawnMonsterId);
        Assert.Equal(8, acid.ProjectileSpeed);
        Assert.Equal(9, acid.ProjectileLifetime);
        Assert.Equal(20, acid.ProjectileLifetimeBase);
        Assert.Equal(5, acid.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acid.PuddleDurationRandomRange);
        Assert.Equal(40, acid.PuddleDurationBase);
        Assert.Equal(40, acid.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acid.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acid.PuddleDamageLow);
        Assert.Equal(2, acid.PuddleDamageHigh);
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
        Assert.Equal(0, succubus.ProjectileSpeed);
        Assert.Equal(0, succubus.ProjectileLifetime);
        Assert.Equal(0, succubus.ProjectileLifetimeBase);
        Assert.Equal(0, succubus.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(0, succubus.PuddleDurationRandomRange);
        Assert.Equal(0, succubus.PuddleDurationBase);
        Assert.Equal(0, succubus.PuddleDurationIntelligenceScale);
        Assert.Equal(0, succubus.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(0, succubus.PuddleDamageLow);
        Assert.Equal(0, succubus.PuddleDamageHigh);
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
