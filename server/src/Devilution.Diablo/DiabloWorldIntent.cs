namespace Devilution.Diablo;

/** Base intent for non-combat authoritative world interactions. */
public abstract record DiabloWorldIntent;

public sealed record DiabloMoveIntent(int DirectionX, int DirectionY) : DiabloWorldIntent;

public sealed record DiabloUsePortalIntent(uint PortalId) : DiabloWorldIntent;

public sealed record DiabloPickupWorldItemIntent(uint ItemEntityId) : DiabloWorldIntent;

public sealed record DiabloDropHeldItemIntent(int TargetX, int TargetY) : DiabloWorldIntent;

public sealed record DiabloOperateObjectIntent(uint ObjectEntityId) : DiabloWorldIntent;

public sealed record DiabloAdvanceQuestIntent(uint QuestId) : DiabloWorldIntent;

public sealed record DiabloSetFriendlyModeIntent(bool FriendlyMode) : DiabloWorldIntent;

public sealed record DiabloRestartTownIntent : DiabloWorldIntent;

/** Result and transient events from one module-owned world transition. */
public readonly record struct DiabloWorldIntentResult(
    bool Succeeded,
    DiabloRuleFailure? Failure,
    IReadOnlyList<DiabloGameplayEvent> Events)
{
    public static DiabloWorldIntentResult Accepted { get; } = new(true, null, []);

    public static DiabloWorldIntentResult AcceptedWith(params DiabloGameplayEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return new(true, null, events);
    }

    public static DiabloWorldIntentResult Rejected(DiabloRuleFailure failure)
    {
        return new(false, failure, []);
    }
}
