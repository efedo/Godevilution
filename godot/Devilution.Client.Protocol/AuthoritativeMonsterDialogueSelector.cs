using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves a visible server-authored unique-monster dialogue target. */
public readonly record struct AuthoritativeMonsterDialogueTarget(
    uint QuestId,
    uint MonsterEntityId);

public static class AuthoritativeMonsterDialogueSelector
{
    public static AuthoritativeMonsterDialogueTarget? Select(
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

        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == localPlayer.LevelId);
        var lightRadius = localPlayer.LightRadius is >= 2 and <= 15
            ? localPlayer.LightRadius
            : 10;
        var monster = snapshot.Monsters
            .Where(candidate => candidate.EntityId != 0
                && candidate.IsUnique
                && candidate.DialogueMessageId != 0
                && AuthoritativeMonsterPresentationVisibility.IsVisible(
                    candidate,
                    localPlayer.LevelId,
                    geometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    lightRadius)
                && AuthoritativeMonsterPositionProjection.IsAt(candidate, targetX, targetY))
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (monster is null || string.IsNullOrEmpty(monster.UniqueIdentity))
            return null;

        var quest = snapshot.Quests
            .Where(candidate => !candidate.Completed
                && !candidate.Unavailable
                && AuthoritativeLevelVisibility.IsVisible(candidate.LevelId, localPlayer.LevelId)
                && string.Equals(
                    candidate.CompletionUniqueIdentity,
                    monster.UniqueIdentity,
                    StringComparison.Ordinal))
            .OrderBy(candidate => candidate.QuestId)
            .FirstOrDefault();
        return quest is null
            ? null
            : new AuthoritativeMonsterDialogueTarget(quest.QuestId, monster.EntityId);
    }

}
