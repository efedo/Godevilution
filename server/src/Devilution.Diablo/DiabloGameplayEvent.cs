using Devilution.Engine;

namespace Devilution.Diablo;

/** Presentation-independent gameplay event emitted by authoritative rules. */
public enum DiabloGameplayEventKind
{
    Damage,
    Experience,
    Healing,
    Dialogue,
    Victory,
    MapOpened,
}

public readonly record struct DiabloGameplayEvent(
    DiabloGameplayEventKind Kind,
    uint SourceEntityId,
    uint TargetEntityId,
    int Amount)
{
    /** Optional quest speech identity carried by Dialogue events. */
    public int MessageId { get; init; }

    /** Optional quest identity carried by Dialogue events. */
    public uint QuestId { get; init; }

    /** Exact Q6 amount; legacy healing events already express Amount in native resource units. */
    public int AmountQ6 { get; init; } = Kind == DiabloGameplayEventKind.Healing
        ? Amount
        : DeterministicFixedPointQ6.FromWhole(Amount);
}

/** Gameplay event paired with the module-owned player state that should receive it. */
public readonly record struct DiabloDirectedGameplayEvent(
    AuthoritativePlayerState Recipient,
    DiabloGameplayEvent Event);
