using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentAttackEffectRulesTests
{
    [Fact]
    public void AppliesRandomAndHighestFixedLifeAndManaRates()
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

        Assert.Equal(337, gain.LifeQ6);
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
}
