using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Shared authoritative boundary for actions and queued native destination walks. */
public static class AuthoritativePlayerSettlement
{
    /**
     * A completed animation does not imply that a native destination walk is
     * finished; the server may still own queued directions between actions.
     * Older snapshots omit the additive flag and retain its default false value.
     */
    public static bool IsSettled(PlayerSnapshot player, int pendingCommandCount)
    {
        ArgumentNullException.ThrowIfNull(player);
        return pendingCommandCount == 0
            && player.CurrentAction is null
            && player.CombatMode == PlayerCombatMode.Stand
            && !player.NativeWalkPathPending;
    }
}