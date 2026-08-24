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

        var monster = SelectMonsterTarget(snapshot, localPlayer, targetX, targetY);
        if (monster is not null)
            return monster;

        var remotePlayer = snapshot.Players.FirstOrDefault(candidate =>
            candidate.EntityId != localPlayer.EntityId
            && candidate.Life > 0
            && IsVisible(candidate.LevelId, localPlayer.LevelId)
            && IsAtPresentationPosition(candidate, targetX, targetY));
        if (remotePlayer is not null)
            return remotePlayer.EntityId;
        return null;
    }

    /** Resolves a visible living monster to its authoritative entity identity. */
    public static uint? SelectMonsterTarget(
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
        return localPlayer is null
            ? null
            : SelectMonsterTarget(snapshot, localPlayer, targetX, targetY);
    }

    /** Resolves a visible item, object, or living monster for Telekinesis. */
    public static uint? SelectWorldEntityTarget(
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

        var item = snapshot.WorldItems.FirstOrDefault(candidate =>
            candidate.EntityId != 0
            && IsVisible(candidate.LevelId, localPlayer.LevelId)
            && candidate.PositionX == targetX
            && candidate.PositionY == targetY);
        if (item is not null)
            return item.EntityId;

        var worldObject = snapshot.Objects.FirstOrDefault(candidate =>
            candidate.EntityId != 0
            && IsVisible(candidate.LevelId, localPlayer.LevelId)
            && !candidate.Activated
            && !candidate.Destroyed
            && AuthoritativeObjectSelector.HasAuthoritativeInteraction(candidate)
            && candidate.PositionX == targetX
            && candidate.PositionY == targetY);
        if (worldObject is not null)
            return worldObject.EntityId;

        return snapshot.Monsters
            .Where(candidate => candidate.EntityId != 0
                && candidate.Alive
                && !candidate.SneakHidden
                && IsVisible(candidate.LevelId, localPlayer.LevelId)
                && AuthoritativeMonsterPositionProjection.IsAt(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .Select(candidate => (uint?)candidate.EntityId)
            .FirstOrDefault();
    }

    /** Resolves a visible remote player for a player-targeted cast request. */
    public static uint? SelectRemotePlayerTarget(
        Snapshot? snapshot,
        uint localPlayerEntityId,
        int targetX,
        int targetY,
        bool includeDead)
    {
        if (snapshot is null)
            return null;

        var localPlayer = snapshot.Players.FirstOrDefault(
            player => player.EntityId == localPlayerEntityId)
            ?? snapshot.Players.FirstOrDefault();
        if (localPlayer is null)
            return null;

        return snapshot.Players
            .Where(candidate => candidate.EntityId != localPlayer.EntityId
                && (includeDead || candidate.Life > 0)
                && IsVisible(candidate.LevelId, localPlayer.LevelId)
                && IsAtPresentationPosition(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .Select(candidate => (uint?)candidate.EntityId)
            .FirstOrDefault();
    }

    private static bool IsVisible(uint levelId, uint localLevelId)
        => levelId == 0 || levelId == localLevelId;

    private static uint? SelectMonsterTarget(
        Snapshot snapshot,
        PlayerSnapshot localPlayer,
        int targetX,
        int targetY)
    {
        return snapshot.Monsters
            .Where(candidate => candidate.Alive
                && !candidate.SneakHidden
                && IsVisible(candidate.LevelId, localPlayer.LevelId)
                && AuthoritativeMonsterPositionProjection.IsAt(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .Select(candidate => (uint?)candidate.EntityId)
            .FirstOrDefault();
    }

    private static bool IsAtPresentationPosition(PlayerSnapshot player, int targetX, int targetY)
    {
        var position = AuthoritativePlayerPositionProjection.Resolve(player);
        return position.X == targetX && position.Y == targetY;
    }
}
