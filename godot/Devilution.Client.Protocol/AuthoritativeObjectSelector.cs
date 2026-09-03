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

        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player.LevelId))
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    /** Selects an active authored object at the local player's exact tile. */
    public static ObjectSnapshot? SelectAt(Snapshot? snapshot, int x, int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player.LevelId)
                && @object.PositionX == x
                && @object.PositionY == y)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    private static bool IsSelectable(ObjectSnapshot @object, uint currentLevelId)
        => @object.EntityId != 0
            && AuthoritativeLevelVisibility.IsVisible(@object.LevelId, currentLevelId)
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
