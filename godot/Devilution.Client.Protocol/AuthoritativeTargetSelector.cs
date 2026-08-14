using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves a presentation tile to a server-owned combat entity identity. */
public static class AuthoritativeTargetSelector
{
    public static uint? SelectCombatTarget(
        Snapshot? snapshot,
        uint localPlayerEntityId,
        int targetX,
        int targetY)
    {
        if (snapshot is null)
            return null;

        var localPlayer = snapshot.Players.FirstOrDefault(
            player => player.EntityId == localPlayerEntityId)
            ?? snapshot.Players.FirstOrDefault();
        if (localPlayer is null)
            return null;

        var monster = snapshot.Monsters.FirstOrDefault(candidate =>
            candidate.Alive
            && !candidate.SneakHidden
            && IsVisible(candidate.LevelId, localPlayer.LevelId)
            && candidate.PositionX == targetX
            && candidate.PositionY == targetY);
        if (monster is not null)
            return monster.EntityId;

        var remotePlayer = snapshot.Players.FirstOrDefault(candidate =>
            candidate.EntityId != localPlayer.EntityId
            && candidate.Life > 0
            && IsVisible(candidate.LevelId, localPlayer.LevelId)
            && candidate.PositionX == targetX
            && candidate.PositionY == targetY);
        if (remotePlayer is not null)
            return remotePlayer.EntityId;
        return null;
    }

    private static bool IsVisible(uint levelId, uint localLevelId)
        => levelId == 0 || levelId == localLevelId;
}
