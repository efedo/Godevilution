using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves visible authored objects without treating presentation-only objects as commands. */
public static class AuthoritativeObjectSelector
{
    /** Selects an active authored object at the local player's exact tile. */
    public static ObjectSnapshot? SelectAt(Snapshot? snapshot, int x, int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        return snapshot!.Objects
            .Where(@object => @object.EntityId != 0
                && @object.LevelId == player.LevelId
                && !@object.Activated
                && !@object.Destroyed
                && @object.PositionX == x
                && @object.PositionY == y
                && HasAuthoritativeInteraction(@object))
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    /** Returns whether the server declared behavior that can be submitted as an intent. */
    public static bool HasAuthoritativeInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.EffectKind != 0
            || @object.QuestId != 0
            || @object.QuestTransitionKind != 0
            || @object.RewardKind != 0
            || @object.QuestSequenceFollowUpKind != 0
            || @object.TrapArmed;
    }
}
