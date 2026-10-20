using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.PresentationAssets;

/** Resolves only the current server-authored L1 object frame; this type owns no animation clock. */
public static class NativeObjectPresentation
{
    public static bool TryResolve(
        NativeObjectCatalog? catalog,
        ObjectSnapshot? state,
        string? contentManifestHash,
        uint currentLevelId,
        out NativeObjectFrame? frame,
        out string diagnostic)
    {
        frame = null;
        if (catalog is null || state is null) {
            diagnostic = "Native object art is unavailable for this snapshot.";
            return false;
        }
        if (currentLevelId != 1 || state.LevelId != currentLevelId
            || !catalog.SupportsBinding(contentManifestHash, currentLevelId)) {
            diagnostic = "Native object art is not bound to this content profile and Cathedral level.";
            return false;
        }
        if (!catalog.TryGetObject(state.NativeObjectId, out _)) {
            diagnostic = $"Native object ID {state.NativeObjectId} is not in the supported L1 art family.";
            return false;
        }
        if (state.NativeObjectId is 1 or 2) {
            var door = state.CathedralDoor;
            if (door is null
                || door.EntityId != state.EntityId
                || door.LevelId != state.LevelId
                || door.PositionX != state.PositionX
                || door.PositionY != state.PositionY
                || door.NativeObjectId != state.NativeObjectId
                || door.AnimationFrame != state.AnimationFrame
                || !AuthoritativeObjectSelector.HasAuthoritativeInteraction(state)) {
                diagnostic = "Native Cathedral door art requires a consistent authoritative door snapshot.";
                return false;
            }
        } else if (state.CathedralDoor is not null) {
            diagnostic = "A non-door native object cannot carry Cathedral door state.";
            return false;
        }

        if (!catalog.TryResolveFrame(state.NativeObjectId, state.AnimationFrame, out var resolved)) {
            diagnostic = $"Native object {state.NativeObjectId} frame {state.AnimationFrame} is outside its source art.";
            return false;
        }
        frame = resolved;
        diagnostic = string.Empty;
        return true;
    }
}
