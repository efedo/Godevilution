using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation family selected from a server-owned status-effect identity. */
public enum StatusPresentationKind
{
    None,
    Infravision,
    Search,
    Etherealize,
}

/** Maps authoritative status identities to client-only visual families. */
public static class StatusPresentationStyle
{
    public static StatusPresentationKind Resolve(StatusEffectBehavior behavior)
    {
        return behavior switch {
            StatusEffectBehavior.Infravision => StatusPresentationKind.Infravision,
            StatusEffectBehavior.Search => StatusPresentationKind.Search,
            StatusEffectBehavior.Etherealize => StatusPresentationKind.Etherealize,
            _ => StatusPresentationKind.None,
        };
    }
}
