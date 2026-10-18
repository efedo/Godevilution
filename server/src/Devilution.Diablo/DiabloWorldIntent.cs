namespace Devilution.Diablo;

/** Base intent for non-combat authoritative world interactions. */
public abstract record DiabloWorldIntent;

public sealed record DiabloMoveIntent(int DirectionX, int DirectionY) : DiabloWorldIntent;

/** Finds and applies one deterministic step, or captures an opt-in native path queue. */
public sealed record DiabloMoveToIntent(int TargetX, int TargetY, bool FollowPath = false) : DiabloWorldIntent;

public sealed record DiabloUsePortalIntent(uint PortalId) : DiabloWorldIntent;

public sealed record DiabloPickupWorldItemIntent(uint ItemEntityId) : DiabloWorldIntent;

public sealed record DiabloDropHeldItemIntent(int TargetX, int TargetY) : DiabloWorldIntent;

/** Drops one stable-referenced carried inventory item into an adjacent world cell. */
public sealed record DiabloDropItemIntent(
    DiabloPlayerItemReference Item,
    int TargetX,
    int TargetY) : DiabloWorldIntent;

public sealed record DiabloOperateObjectIntent(uint ObjectEntityId) : DiabloWorldIntent;

public sealed record DiabloDisarmTrapIntent(uint ObjectEntityId) : DiabloWorldIntent;

public sealed record DiabloAdvanceQuestIntent(uint QuestId) : DiabloWorldIntent;

/** Starts a validated quest conversation with a server-owned unique encounter. */
public sealed record DiabloTalkToQuestIntent(uint QuestId, uint TargetEntityId) : DiabloWorldIntent;

/** Completes a content-defined quest turn-in at an adjacent native towner. */
public sealed record DiabloTurnInQuestIntent(uint QuestId, uint TownerId) : DiabloWorldIntent;

/** Requests the content-owned greeting for a general native townsperson. */
public sealed record DiabloTalkToTownerIntent(int TownerId) : DiabloWorldIntent;

public sealed record DiabloSetFriendlyModeIntent(bool FriendlyMode) : DiabloWorldIntent;

/** Spends server-owned level-up points on one native primary attribute. */
public sealed record DiabloAllocateAttributeIntent(
    DiabloPlayerAttribute Attribute,
    uint Amount) : DiabloWorldIntent;

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
