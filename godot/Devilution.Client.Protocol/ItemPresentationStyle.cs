using Devilution.Protocol.V1;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;

namespace Devilution.Client.Protocol;

/** Presentation family selected from server-owned item state metadata. */
public enum ItemPresentationKind
{
    Generic,
    Gold,
    Ear,
}

/** Maps additive item presentation metadata with a generic compatibility fallback. */
public static class ItemPresentationStyle
{
    public static ItemPresentationKind Resolve(ItemStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PresentationKind switch {
            ProtocolItemPresentationKind.Gold => ItemPresentationKind.Gold,
            ProtocolItemPresentationKind.Ear => ItemPresentationKind.Ear,
            _ => ItemPresentationKind.Generic,
        };
    }
}
