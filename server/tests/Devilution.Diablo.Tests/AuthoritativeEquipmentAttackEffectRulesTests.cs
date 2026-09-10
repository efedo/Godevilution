using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentAttackEffectRulesTests
{
    [Fact]
    public void AppliesNativeRandomAndHighestFixedLifeAndManaRates()
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6: 6400,
            usableEquipmentFlags:
                AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag
                | AuthoritativeEquipmentAttackEffectRules.StealLife3Flag
                | AuthoritativeEquipmentAttackEffectRules.StealLife5Flag
                | AuthoritativeEquipmentAttackEffectRules.StealMana3Flag
                | AuthoritativeEquipmentAttackEffectRules.StealMana5Flag,
            generateRnd: limit => {
                Assert.Equal(800, limit);
                return 17;
            });

        Assert.Equal(320, gain.LifeQ6);
        Assert.Equal(320, gain.ManaQ6);
    }

    [Fact]
    public void NoManaSuppressesManaOnHitButNotLifeOnHit()
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6: 6400,
            usableEquipmentFlags:
                AuthoritativeEquipmentAttackEffectRules.StealMana5Flag
                | AuthoritativeEquipmentAttackEffectRules.StealLife3Flag
                | AuthoritativeEquipmentAttackEffectRules.NoManaFlag,
            generateRnd: _ => throw new InvalidOperationException("Random life effect was not selected."));

        Assert.Equal(192, gain.LifeQ6);
        Assert.Equal(0, gain.ManaQ6);
    }

    [Fact]
    public void ZeroDamageProducesNoGainAndDoesNotRollRandom()
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6: 0,
            usableEquipmentFlags: AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag,
            generateRnd: _ => throw new InvalidOperationException("Zero damage must not roll random life gain."));

        Assert.Equal(new AuthoritativeEquipmentAttackEffectRules.ResourceGain(0, 0), gain);
    }

    [Fact]
    public void UsesConfiguredResourceRestorationTuning()
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6: 10000,
            usableEquipmentFlags:
                AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag
                | AuthoritativeEquipmentAttackEffectRules.StealLife3Flag
                | AuthoritativeEquipmentAttackEffectRules.StealMana5Flag,
            generateRnd: limit => {
                Assert.Equal(1000, limit);
                return 25;
            },
            randomStealLifeDivisor: 10,
            stealLife3Percent: 7,
            stealMana5Percent: 9);

        Assert.Equal(700, gain.LifeQ6);
        Assert.Equal(900, gain.ManaQ6);
    }

    [Fact]
    public void RejectsInvalidResourceRestorationTuning()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeEquipmentAttackEffectRules.Resolve(
                100,
                0,
                _ => 0,
                randomStealLifeDivisor: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeEquipmentAttackEffectRules.Resolve(
                100,
                0,
                _ => 0,
                stealMana5Percent: 101));
    }

    [Fact]
    public void PlayerTargetRestorationOnlyUsesRandomLifeSteal()
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.ResolvePlayerTarget(
            damageQ6: 6400,
            usableEquipmentFlags:
                AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag
                | AuthoritativeEquipmentAttackEffectRules.StealLife5Flag
                | AuthoritativeEquipmentAttackEffectRules.StealMana5Flag,
            generateRnd: limit => {
                Assert.Equal(800, limit);
                return 17;
            });

        Assert.Equal(new AuthoritativeEquipmentAttackEffectRules.ResourceGain(17, 0), gain);
    }

    [Fact]
    public void PlayerTargetRestorationRejectsInvalidRandomDivisor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeEquipmentAttackEffectRules.ResolvePlayerTarget(
                100,
                AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag,
                _ => 0,
                randomStealLifeDivisor: 0));
    }
}
