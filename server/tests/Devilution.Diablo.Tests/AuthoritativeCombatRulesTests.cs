using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCombatRulesTests
{
    [Fact]
    public void LoadsExternalCombatConstants()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thit_chance_percent\tcritical_chance_percent\tcritical_multiplier\tfriendly_fire\tgame_variant\trestart_level_id\trestart_position_x\trestart_position_y\trestart_life\trestart_life_scale\trestart_transition_ticks\tdeath_gold_divisor\tdeath_gold_maximum_stack\tdeath_gold_item_type\tdeath_gold_item_index\tdeath_drop_search_radius\n12\t2\t250\t80\t25\t3\t0\tHellfire\t4\t75\t68\t1\t64\t2\t3\t2500\t11\t0\t25\n");

        Assert.Equal(12, rules.BaseAttackDamage);
        Assert.Equal(2, rules.MinimumDamage);
        Assert.Equal(250U, rules.DefeatExperience);
        Assert.Equal(80, rules.HitChancePercent);
        Assert.Equal(25, rules.CriticalChancePercent);
        Assert.Equal(3, rules.CriticalMultiplier);
        Assert.False(rules.FriendlyFire);
        Assert.Equal(DiabloGameVariant.Hellfire, rules.GameVariant);
        Assert.Equal((4U, 75, 68), (
            rules.RestartLevelId,
            rules.RestartPositionX,
            rules.RestartPositionY));
        Assert.Equal((1, 64, 2U), (
            rules.RestartLife,
            rules.RestartLifeScale,
            rules.RestartTransitionTicks));
        Assert.Equal((3, 2500, 11, 0, 25), (
            rules.DeathGoldDivisor,
            rules.DeathGoldMaximumStack,
            rules.DeathGoldItemType,
            rules.DeathGoldItemIndex,
            rules.DeathDropSearchRadius));
        Assert.Equal(24U, rules.PvpEarItemId);
    }

    [Fact]
    public void DefaultsLegacyTablesToDiabloAndRejectsUnknownVariants()
    {
        var legacy = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\n10\t1\t100\n");
        var invalid =
            "base_attack_damage\tminimum_damage\tdefeat_experience\tgame_variant\n10\t1\t100\tExpansion\n";

        Assert.Equal(DiabloGameVariant.Diablo, legacy.GameVariant);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv("combat.tsv", invalid));
    }

    [Fact]
    public void RejectsInvalidDamageBounds()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(1, 2, 0));
    }

    [Fact]
    public void ValidatesRestartDestinationAgainstWorldGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(4, 80, 80, [68 * 80 + 75]));
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            restartLevelId: 4,
            restartPositionX: 75,
            restartPositionY: 68);

        Assert.Throws<InvalidDataException>(() => rules.ValidateWorld(world));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            restartLifeScale: 65));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            deathGoldDivisor: 1));
    }

    [Fact]
    public void ValidatesPvpEarNativeIdentityFromTheExternalItemCatalog()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var valid = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 23, miscId: 44),
        ]);
        var wrongItemIndex = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 24, miscId: 44),
        ]);
        var wrongMiscId = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 23, miscId: 43),
        ]);

        rules.ValidateItems(valid);
        Assert.Throws<InvalidDataException>(() => rules.ValidateItems(wrongItemIndex));
        Assert.Throws<InvalidDataException>(() => rules.ValidateItems(wrongMiscId));
    }

    [Fact]
    public void ResolvesArmorAndElementalResistanceDeterministically()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var target = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            armorClass: 3,
            fireResistance: 50,
            acidResistance: 75);

        Assert.Equal(7, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Physical, target));
        Assert.Equal(5, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Fire, target));
        Assert.Equal(2, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Acid, target));
    }

    [Fact]
    public void ResolvesDeterministicMissAndCriticalAttackOutcomes()
    {
        var target = new AuthoritativeCombatTarget(9, 0, 0, 100, armorClass: 0);
        var misses = new AuthoritativeCombatRules(10, 1, 100, hitChancePercent: 0);
        Assert.Equal(0, misses.ResolveAttackDamage(10, target, 1));

        var criticals = new AuthoritativeCombatRules(10, 1, 100, criticalChancePercent: 100, criticalMultiplier: 3);
        Assert.Equal(30, criticals.ResolveAttackDamage(10, target, 1));
    }

    [Fact]
    public void ResolvesAutonomousMonsterAttacksThroughTheSameRules()
    {
        var rules = new AuthoritativeCombatRules(3, 1, 100, hitChancePercent: 100, criticalChancePercent: 100, criticalMultiplier: 2);

        Assert.Equal(4, rules.ResolveAttackDamage(3, armorClass: 2, seed: 1));
    }

    [Fact]
    public void ResolvesNativeMonsterAccuracyWithoutSubtractingPlayerArmorFromDamage()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(
            10,
            rules.ResolveMonsterAttackDamage(
                baseDamage: 10,
                monsterToHit: 100,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 50,
                seed: 0));
        Assert.Equal(
            0,
            rules.ResolveMonsterAttackDamage(
                baseDamage: 10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 100,
                seed: 0));
    }

    [Fact]
    public void PhysicalHitChanceSubtractsArmorAndClampsToNativeBounds()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(5, rules.ResolvePhysicalHitChance(20, targetArmorClass: 30));
        Assert.Equal(65, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 5));
        Assert.Equal(95, rules.ResolvePhysicalHitChance(200, targetArmorClass: 0));
        Assert.Equal(57, rules.ResolvePhysicalHitChance(
            70,
            targetArmorClass: 16,
            armorPiercingBonus: 1,
            barbarianMelee: true));
    }

    [Fact]
    public void HellfirePhysicalHitChanceDoesNotApplyArmorPiercingBonus()
    {
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            gameVariant: DiabloGameVariant.Hellfire);

        Assert.Equal(62, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 1));
        Assert.Equal(70, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 50));
    }

    private static AuthoritativeItemDefinition CreateEarDefinition(
        int nativeItemIndex,
        int miscId)
    {
        return new AuthoritativeItemDefinition(
            24,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            false) {
                NativeItemIndex = nativeItemIndex,
                MiscId = miscId,
            };
    }
}
