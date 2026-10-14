namespace Devilution.Diablo;

/** Shared deterministic character progression used by Diablo gameplay transitions. */
internal static class DiabloPlayerProgression
{
    /** Resource inputs seen by native loot after experience, without mutating a failed command. */
    internal static (int Life, int Mana) PreviewExperienceResources(
        AuthoritativePlayerState player, uint amount, AuthoritativeExperienceCatalog? experience)
    {
        var total = checked(player.Experience + amount);
        var level = player.CharacterLevel;
        var life = player.BaseLifeMaximum;
        var mana = player.BaseManaMaximum;
        while (level < (experience?.MaximumLevel ?? AuthoritativeExperienceCatalog.MaximumSupportedLevel)
            && total >= (experience?.GetRequiredExperience(level) ?? checked(level * 1000U))) {
            level = checked(level + 1);
            life = checked(life + (player.PlayerClass?.LifePerLevelQ6 ?? 0));
            mana = checked(mana + (player.PlayerClass?.ManaPerLevelQ6 ?? 0));
        }
        return (life, mana);
    }

    public static void GrantExperience(
        AuthoritativePlayerState player,
        uint amount,
        AuthoritativeExperienceCatalog? experience = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Experience = checked(player.Experience + amount);
        while (player.CharacterLevel < (experience?.MaximumLevel ?? AuthoritativeExperienceCatalog.MaximumSupportedLevel)
            && player.Experience >= (experience?.GetRequiredExperience(player.CharacterLevel)
                ?? checked(player.CharacterLevel * 1000U))) {
            player.AdvanceLevel();
            var available = CalculateAvailableAttributePoints(player);
            player.StatPoints = available < 5
                ? (uint)available
                : checked(player.StatPoints + 5U);
        }
    }

    private static int CalculateAvailableAttributePoints(AuthoritativePlayerState player)
    {
        var attributes = player.Attributes;
        var maximumStrength = player.PlayerClass?.MaximumStrength ?? player.MaximumAttribute;
        var maximumMagic = player.PlayerClass?.MaximumMagic ?? player.MaximumAttribute;
        var maximumDexterity = player.PlayerClass?.MaximumDexterity ?? player.MaximumAttribute;
        var maximumVitality = player.PlayerClass?.MaximumVitality ?? player.MaximumAttribute;
        return Math.Max(maximumStrength - attributes.Strength.Base, 0)
            + Math.Max(maximumMagic - attributes.Magic.Base, 0)
            + Math.Max(maximumDexterity - attributes.Dexterity.Base, 0)
            + Math.Max(maximumVitality - attributes.Vitality.Base, 0);
    }
}
