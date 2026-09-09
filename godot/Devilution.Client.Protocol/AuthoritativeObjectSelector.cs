using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves visible authored objects without treating presentation-only objects as commands. */
public static class AuthoritativeObjectSelector
{
    /** Selects the first visible active authored object for generic UI actions. */
    public static ObjectSnapshot? SelectFirst(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player, geometry, lightRadius))
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    /** Selects an active authored object at the local player's exact tile. */
    public static ObjectSnapshot? SelectAt(Snapshot? snapshot, int x, int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player, geometry, lightRadius)
                && @object.PositionX == x
                && @object.PositionY == y)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    private static bool IsSelectable(
        ObjectSnapshot @object,
        PlayerSnapshot player,
        LevelGeometrySnapshot? geometry,
        int lightRadius)
        => @object.EntityId != 0
            && AuthoritativeObjectPresentationVisibility.IsVisible(
                @object,
                player.LevelId,
                geometry,
                player.PositionX,
                player.PositionY,
                lightRadius)
            && !@object.Activated
            && !@object.Destroyed
            && HasAuthoritativeInteraction(@object);

    /** Returns whether the server declared behavior that can be submitted as an intent. */
    public static bool HasAuthoritativeInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.EffectKind != 0
            || @object.QuestId != 0
            || @object.QuestTransitionKind != 0
            || @object.RewardKind != 0
            || @object.QuestSequenceFollowUpKind != 0
            || @object.DialogueMessageId != 0
            || @object.TrapArmed;
    }
}
