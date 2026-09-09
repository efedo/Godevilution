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
            && AuthoritativeNativeVisionVisibility.IsVisible(
                candidate.LevelId,
                candidate.PositionX,
                candidate.PositionY,
                localPlayer.LevelId,
                ResolveLocalGeometry(snapshot, localPlayer),
                localPlayer.PositionX,
                localPlayer.PositionY,
                ResolveLightRadius(localPlayer))
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

        var geometry = ResolveLocalGeometry(snapshot, localPlayer);
        var lightRadius = ResolveLightRadius(localPlayer);

        var worldObject = snapshot.Objects
            .Where(candidate => candidate.EntityId != 0
                && AuthoritativeObjectPresentationVisibility.IsVisible(
                    candidate,
                    localPlayer.LevelId,
                    geometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    lightRadius)
                && !candidate.Activated
                && !candidate.Destroyed
                && AuthoritativeObjectSelector.HasAuthoritativeInteraction(candidate)
                && candidate.PositionX == targetX
                && candidate.PositionY == targetY)
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (worldObject is not null)
            return worldObject.EntityId;

        var item = snapshot.WorldItems
            .Where(candidate => candidate.EntityId != 0
                && AuthoritativeWorldItemPresentationVisibility.IsVisible(
                    candidate,
                    localPlayer.LevelId,
                    geometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    lightRadius)
                && candidate.PositionX == targetX
                && candidate.PositionY == targetY)
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (item is not null)
            return item.EntityId;

        return snapshot.Monsters
            .Where(candidate => candidate.EntityId != 0
                && IsVisibleMonster(candidate, localPlayer, geometry, lightRadius)
                && candidate.DialogueMessageId == 0
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
                && AuthoritativeNativeVisionVisibility.IsVisible(
                    candidate.LevelId,
                    candidate.PositionX,
                    candidate.PositionY,
                    localPlayer.LevelId,
                    ResolveLocalGeometry(snapshot, localPlayer),
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    ResolveLightRadius(localPlayer))
                && IsAtPresentationPosition(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .Select(candidate => (uint?)candidate.EntityId)
            .FirstOrDefault();
    }

    private static uint? SelectMonsterTarget(
        Snapshot snapshot,
        PlayerSnapshot localPlayer,
        int targetX,
        int targetY)
    {
        var geometry = ResolveLocalGeometry(snapshot, localPlayer);
        var lightRadius = ResolveLightRadius(localPlayer);
        return snapshot.Monsters
            .Where(candidate => IsVisibleMonster(candidate, localPlayer, geometry, lightRadius)
                && AuthoritativeMonsterPositionProjection.IsAt(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .Select(candidate => (uint?)candidate.EntityId)
            .FirstOrDefault();
    }

    private static bool IsVisibleMonster(
        MonsterSnapshot monster,
        PlayerSnapshot localPlayer,
        LevelGeometrySnapshot? geometry,
        int lightRadius)
        => AuthoritativeMonsterPresentationVisibility.IsVisible(
            monster,
            localPlayer.LevelId,
            geometry,
            localPlayer.PositionX,
            localPlayer.PositionY,
            lightRadius);

    private static LevelGeometrySnapshot? ResolveLocalGeometry(
        Snapshot snapshot,
        PlayerSnapshot localPlayer)
        => snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == localPlayer.LevelId);

    private static int ResolveLightRadius(PlayerSnapshot localPlayer)
        => localPlayer.LightRadius is >= 2 and <= 15
            ? localPlayer.LightRadius
            : 10;

    private static bool IsAtPresentationPosition(PlayerSnapshot player, int targetX, int targetY)
    {
        var position = AuthoritativePlayerPositionProjection.Resolve(player);
        return position.X == targetX && position.Y == targetY;
    }
}
