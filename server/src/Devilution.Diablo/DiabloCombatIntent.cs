namespace Devilution.Diablo;

/** Base intent for authoritative combat actions. */
public abstract record DiabloCombatIntent;

public sealed record DiabloAttackIntent(uint TargetEntityId) : DiabloCombatIntent;

/** Authoritative origin of a requested spell cast. */
public enum DiabloCastSourceKind
{
    Unspecified,
    Ability,
    MemorizedSpell,
    ChargedItem,
    ConsumableItem,
}

public sealed record DiabloCastIntent(
    uint SpellId,
    uint TargetEntityId,
    int TargetX,
    int TargetY,
    DiabloCastSourceKind Source = DiabloCastSourceKind.Unspecified,
    DiabloPlayerItemReference? SourceItem = null) : DiabloCombatIntent;

/** Result and transient events from one module-owned combat transition. */
public readonly record struct DiabloCombatIntentResult(
    bool Succeeded,
    DiabloRuleFailure? Failure,
    IReadOnlyList<DiabloGameplayEvent> Events)
{
    public static DiabloCombatIntentResult Accepted { get; } = new(true, null, []);

    public static DiabloCombatIntentResult AcceptedWith(params DiabloGameplayEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return new(true, null, events);
    }

    public static DiabloCombatIntentResult Rejected(DiabloRuleFailure failure)
    {
        return new(false, failure, []);
    }
}
