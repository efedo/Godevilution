using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

internal sealed record NativeObjectDrawObservation(ulong SnapshotTick, uint EntityId, uint NativeObjectId,
    uint LevelId, int PositionX, int PositionY, uint NativeFrame, bool Activated, bool Destroyed, bool TrapArmed,
    bool NativeArt, bool GenericFallback, string? PackId, string? AtlasPath, int NativeWidth,
    float AnchorX, float AnchorY, float DrawX, float DrawY, int Width, int Height,
    bool AnchorInsideViewport, string? Diagnostic);

public partial class WorldPresentation
{
    private readonly NativeObjectAssets nativeObjectAssets = new();
    private readonly List<NativeObjectDrawObservation> nativeObjectDraws = [];
    private string? nativeObjectContentIdentity;
    private bool nativeObjectArtReady;

    internal NativeObjectPackObservation ObserveNativeObjectPack() => nativeObjectAssets.ObservePack();
    internal IReadOnlyList<NativeObjectDrawObservation> ObserveNativeObjectDrawing() => nativeObjectDraws.ToArray();

    private void BindNativeObjectIdentity(string? hash)
    {
        if (nativeObjectContentIdentity == hash)
            return;
        nativeObjectContentIdentity = hash;
        nativeObjectArtReady = false;
        nativeObjectDraws.Clear();
    }

    /** Only the admitted snapshot selects lifecycle frames. No local object clock is advanced. */
    private bool TryDrawNativeObject(ObjectSnapshot state)
    {
        var anchor = ToScreen(state.PositionX, state.PositionY);
        var size = nativeSceneViewport is { } viewport
            ? new Vector2(viewport.ViewportWidth, viewport.ViewportHeight) : GetViewportRect().Size;
        var pack = nativeObjectAssets.ObservePack();
        var diagnostic = pack.Diagnostic ?? "Native object art requires an active native Cathedral scene.";
        NativeObjectFrame? frame = null;
        if (nativeSceneViewport is not null && nativeObjectArtReady
            && NativeObjectPresentation.TryResolve(nativeObjectAssets.Catalog, state, nativeObjectContentIdentity,
                currentLevel.Id, out frame, out diagnostic)
            && nativeObjectAssets.TryGetTexture(frame!.AtlasId, out var texture)) {
            var bounds = NativeObjectPlacement.ResolveBounds(anchor.X, anchor.Y, frame);
            if (!bounds.IntersectsViewport((int)size.X, (int)size.Y))
                return true;
            var destination = new Rect2((float)bounds.X, (float)bounds.Y, bounds.Width, bounds.Height);
            DrawTextureRectRegion(texture, destination, new Rect2(frame.X, frame.Y, frame.Width, frame.Height));
            nativeObjectAssets.Catalog!.TryGetAtlas(frame.AtlasId, out var atlas);
            RecordNativeObjectDraw(state, anchor, destination, size, true, pack.PackId, atlas.Path, frame.NativeWidth, null);
            // Preserve existing semantic door observations when native art replaces the marker.
            if (state.CathedralDoor is { } door)
                doorDrawObservations[state.EntityId] = new CathedralDoorDrawObservation(state.EntityId,
                    door.NativeObjectId, door.DoorState, state.AnimationFrame, ObjectPresentationStyle.ResolveFrame(state));
            return true;
        }
        var fallback = new Rect2(anchor - new Vector2(11, 11), new Vector2(22, 22));
        if (IntersectsViewport(fallback, size))
            RecordNativeObjectDraw(state, anchor, fallback, size, false, pack.PackId, null, 0, diagnostic);
        return false;
    }

    private void RecordNativeObjectDraw(ObjectSnapshot state, Vector2 anchor, Rect2 bounds, Vector2 size,
        bool native, string? packId, string? atlasPath, int nativeWidth, string? diagnostic)
        => nativeObjectDraws.Add(new NativeObjectDrawObservation(snapshot!.Tick, state.EntityId, state.NativeObjectId,
            state.LevelId, state.PositionX, state.PositionY, state.AnimationFrame, state.Activated, state.Destroyed, state.TrapArmed,
            native, !native, packId, atlasPath, nativeWidth, anchor.X, anchor.Y, bounds.Position.X, bounds.Position.Y,
            (int)bounds.Size.X, (int)bounds.Size.Y, anchor.X >= 0 && anchor.Y >= 0 && anchor.X < size.X && anchor.Y < size.Y,
            diagnostic));
}
