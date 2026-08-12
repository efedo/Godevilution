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
            player.CharacterLevel++;
        }
    }
}
