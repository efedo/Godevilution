using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloPlayerProgressionTests
{
    [Fact]
    public void LevelUpGrowsAndRefillsNativeResources()
    {
        var player = CreatePlayer(life: 300, mana: 100);

        DiabloPlayerProgression.GrantExperience(player, 1);

        Assert.Equal(2U, player.CharacterLevel);
        Assert.Equal(5U, player.StatPoints);
        Assert.Equal(704, player.BaseLifeMaximum);
        Assert.Equal(704, player.LifeMaximum);
        Assert.Equal(704, player.Life);
        Assert.Equal(352, player.BaseManaMaximum);
        Assert.Equal(352, player.ManaMaximum);
        Assert.Equal(352, player.Mana);
    }

    [Fact]
    public void LevelUpPreservesManaWhenNoManaEquipmentIsActive()
    {
        var noManaItem = new EquippedStoreItem(
            0,
            1,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Identified = true,
                Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
            });
        var player = CreatePlayer(life: 640, mana: 100, equipment: [noManaItem]);

        DiabloPlayerProgression.GrantExperience(player, 1);

        Assert.Equal(352, player.ManaMaximum);
        Assert.Equal(100, player.Mana);
    }

    private static AuthoritativePlayerState CreatePlayer(
        int life,
        int mana,
        IReadOnlyList<EquippedStoreItem>? equipment = null)
    {
        var playerClass = new AuthoritativePlayerClassDefinition(
            ClassId: 0,
            Symbol: "test",
            DisplayName: "Test",
            Availability: DiabloPlayerClassAvailability.BaseGame,
            IsDefault: true,
            Flags: DiabloPlayerClassFlags.None,
            BaseStrength: 30,
            BaseMagic: 10,
            BaseDexterity: 20,
            BaseVitality: 25,
            MaximumStrength: 250,
            MaximumMagic: 250,
            MaximumDexterity: 250,
            MaximumVitality: 250,
            BaseBlockChance: 0,
            BaseMagicToHit: 0,
            BaseMeleeToHit: 0,
            BaseRangedToHit: 0,
            LifeAdjustmentQ6: 0,
            ManaAdjustmentQ6: 0,
            LifePerLevelQ6: 64,
            ManaPerLevelQ6: 32,
            LifePerVitalityQ6: 64,
            ManaPerMagicQ6: 32,
            ItemLifePerVitalityQ6: 0,
            ItemManaPerMagicQ6: 0);
        return new AuthoritativePlayerState(
            gold: 0,
            experience: 999,
            life: life,
            mana: mana,
            manaMaximum: 320,
            attributes: playerClass.CreateStartingAttributes(),
            equipment: equipment ?? [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 640,
            characterLevel: 1,
            levelId: 1,
            armorClass: 0,
            playerClass: playerClass);
    }
}
