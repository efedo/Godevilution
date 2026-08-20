namespace Devilution.Diablo;

/** Shared deterministic character progression used by Diablo gameplay transitions. */
internal static class DiabloPlayerProgression
{
    public static void GrantExperience(AuthoritativePlayerState player, uint amount)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Experience = checked(player.Experience + amount);
        while (player.CharacterLevel < 50
            && player.Experience >= player.CharacterLevel * 1000U) {
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
