using Devilution.Protocol.V1;
using ProtocolObjectPresentationKind = Devilution.Protocol.V1.ObjectPresentationKind;

namespace Devilution.Client.Protocol;

/** Presentation family selected from server-owned world-object metadata. */
public enum ObjectPresentationKind
{
    Generic,
    Shrine,
    Quest,
    Breakable,
    Trap,
}

/** Native-compatible lifecycle frame selected for an authored object. */
public enum ObjectPresentationFrame
{
    Default,
    Activated,
}

/** Maps additive object presentation metadata with compatibility fallbacks. */
public static class ObjectPresentationStyle
{
    public static ObjectPresentationKind Resolve(ObjectSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PresentationKind switch {
            ProtocolObjectPresentationKind.Shrine => ObjectPresentationKind.Shrine,
            ProtocolObjectPresentationKind.Quest => ObjectPresentationKind.Quest,
            ProtocolObjectPresentationKind.Breakable => ObjectPresentationKind.Breakable,
            ProtocolObjectPresentationKind.Trap => ObjectPresentationKind.Trap,
            _ => ResolveLegacy(state),
        };
    }

    /**
     * Resolves the server-authored lifecycle frame without inferring state from
     * the object ID. Older snapshots retain the default frame behavior.
     */
    public static ObjectPresentationFrame ResolveFrame(ObjectSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Activated
            && state.KeepVisibleAfterActivation
            && state.ActivatedAnimationFrame > 0
            && state.AnimationFrame == state.ActivatedAnimationFrame
            ? ObjectPresentationFrame.Activated
            : ObjectPresentationFrame.Default;
    }

    private static ObjectPresentationKind ResolveLegacy(ObjectSnapshot state)
    {
        if (state.IsShrine)
            return ObjectPresentationKind.Shrine;
        if (state.TrapArmed)
            return ObjectPresentationKind.Trap;
        if (state.Breakable)
            return ObjectPresentationKind.Breakable;
        if (state.QuestId != 0)
            return ObjectPresentationKind.Quest;
        return ObjectPresentationKind.Generic;
    }
}
