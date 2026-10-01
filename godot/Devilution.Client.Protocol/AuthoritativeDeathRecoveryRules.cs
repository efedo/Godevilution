using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation eligibility only; the server owns death and town recovery. */
public static class AuthoritativeDeathRecoveryRules
{
    /** A restart intent is available only after the projected death action settles. */
    public static bool CanRequestTownRestart(PlayerSnapshot? player)
        => player is { Life: 0, CombatMode: PlayerCombatMode.Death, CurrentAction: null };
}
