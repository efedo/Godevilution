using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativePlayerDamageResolverTests
{
    [Theory]
    [InlineData(1, 610)]
    [InlineData(7, 427)]
    [InlineData(15, 427)]
    public void LearnedSpellLevelUsesNativeReductionReciprocal(
        uint spellLevel,
        int expectedManaDamageQ6)
    {
        var resolver = CreateResolver();
        var player = CreatePlayer(
            life: 1280,
            mana: 1280,
            memorizedSpells: [new MemorizedSpellState(11, spellLevel)]);

        var result = resolver.Apply(player, 640);

        Assert.Equal(640, result.IncomingDamageQ6);
        Assert.Equal(expectedManaDamageQ6, result.ManaDamageQ6);
        Assert.Equal(0, result.LifeDamageQ6);
        Assert.Equal(1280 - expectedManaDamageQ6, player.Mana);
        Assert.Equal(1280, player.Life);
        Assert.True(player.ManaShieldActive);
    }

    [Fact]
    public void DepletionReversesReductionForUnabsorbedLifeDamage()
    {
        var resolver = CreateResolver();
        var player = CreatePlayer(
            life: 1280,
            mana: 100,
            memorizedSpells: [new MemorizedSpellState(11, 1)]);

        var result = resolver.Apply(player, 640);

        Assert.Equal(100, result.ManaDamageQ6);
        Assert.Equal(535, result.LifeDamageQ6);
        Assert.Equal(745, player.Life);
        Assert.Equal(0, player.Mana);
        Assert.True(result.ManaShieldDepleted);
        Assert.False(player.ManaShieldActive);
    }

    [Fact]
    public void LevelZeroItemCastConvertsDamageWithoutLearnedReduction()
    {
        var resolver = CreateResolver();
        var player = CreatePlayer(life: 1280, mana: 300);

        var result = resolver.Apply(player, 640);

        Assert.Equal(300, result.ManaDamageQ6);
        Assert.Equal(340, result.LifeDamageQ6);
        Assert.Equal(940, player.Life);
        Assert.False(player.ManaShieldActive);
    }

    [Fact]
    public void ExactManaAbsorptionRemovesDepletedShieldWithoutLifeDamage()
    {
        var resolver = CreateResolver();
        var player = CreatePlayer(
            life: 1280,
            mana: 610,
            memorizedSpells: [new MemorizedSpellState(11, 1)]);

        var result = resolver.Apply(player, 640);

        Assert.Equal(0, result.LifeDamageQ6);
        Assert.Equal(0, player.Mana);
        Assert.Equal(1280, player.Life);
        Assert.True(result.ManaShieldDepleted);
        Assert.False(player.ManaShieldActive);
    }

    [Fact]
    public void NoManaEquipmentBypassesShieldLikeNativeItemFlag()
    {
        var resolver = CreateResolver();
        var noManaItem = AuthoritativeItemState.Empty with {
            Flags = 0x08000000U,
            StatFlag = true,
        };
        var player = CreatePlayer(
            life: 1280,
            mana: 1280,
            equipment: [new EquippedStoreItem(0, 1, noManaItem)]);

        var result = resolver.Apply(player, 640);

        Assert.Equal(640, result.LifeDamageQ6);
        Assert.Equal(0, result.ManaDamageQ6);
        Assert.Equal(640, player.Life);
        Assert.Equal(1280, player.Mana);
        Assert.True(player.ManaShieldActive);
    }

    private static AuthoritativePlayerDamageResolver CreateResolver()
    {
        return new AuthoritativePlayerDamageResolver(new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(11, 33, 0, 0, 0, 0) {
                ProjectileBehavior = AuthoritativeProjectileBehavior.ManaShield,
                ManaShieldBaseDamageReductionDivisor = 24,
                ManaShieldDamageReductionLevelStep = 3,
                ManaShieldDamageReductionMaximumLevel = 7,
            },
        ]));
    }

    private static AuthoritativePlayerState CreatePlayer(
        int life,
        int mana,
        IReadOnlyList<MemorizedSpellState>? memorizedSpells = null,
        IReadOnlyList<EquippedStoreItem>? equipment = null)
    {
        return new AuthoritativePlayerState(
            0,
            0,
            life,
            mana,
            mana,
            PlayerAttributesState.Zero,
            equipment ?? [],
            [],
            [],
            0,
            0,
            life,
            1,
            0,
            0,
            memorizedSpells: memorizedSpells,
            manaShieldActive: true);
    }
}
