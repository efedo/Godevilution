using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

public partial class WorldPresentation
{
    private readonly NativeTownerAssets nativeTownerAssets = new();
    private readonly NativeTownerPresentationClock nativeTownerClock = new();
    private readonly List<NativeTownerDrawObservation> nativeTownerDraws = [];
    private bool nativeTownerArtReady;
    private string? nativeTownerContentIdentity;
    private NativeTownerPackObservation? cachedTownerPack;

    internal NativeTownerPackObservation ObserveNativeTownerPack() => nativeTownerAssets.ObservePack();
    internal IReadOnlyList<NativeTownerDrawObservation> ObserveNativeTownerDrawing() => nativeTownerDraws.ToArray();

    private void BindNativeTownerIdentity(string? hash)
    {
        if (nativeTownerContentIdentity == hash)
            return;
        nativeTownerContentIdentity = hash;
        cachedTownerPack = null;
        nativeTownerClock.Reset();
        nativeTownerDraws.Clear();
        nativeTownerArtReady = false;
        nativeTownerAssets.Bind(hash, string.Empty, Array.Empty<NativeTownResidentSnapshot>());
    }

    private void AdvanceNativeTowners(double delta, uint tickRate, bool connected, bool reset)
    {
        var inTown = snapshot?.Players.FirstOrDefault()?.LevelId == 0 && snapshot.NativeTownResidents.Count == 11;
        nativeTownerClock.Advance(delta, tickRate, inTown, connected && !presentationEpochPending, reset);
        if (snapshot is not null && townerBindingChanges.ShouldUpdate(true,
                (nativeTownerContentIdentity, snapshot.NativeTownCatalogFingerprint, snapshot.NativeTownResidents))) {
            cachedTownerPack = null;
            nativeTownerArtReady = nativeTownerAssets.Bind(nativeTownerContentIdentity, snapshot.NativeTownCatalogFingerprint,
                snapshot.NativeTownResidents);
        }
    }

    private void AddNativeTownerCommands(Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        NativeSceneViewport sceneViewport)
    {
        if (snapshot?.Players.FirstOrDefault()?.LevelId != 0)
            return;
        foreach (var resident in snapshot.NativeTownResidents.Where(row => row.LevelId == 0)) {
            var anchor = sceneViewport.CellBottomAnchor(resident.PositionX, resident.PositionY);
            // A single actor at the positive cow anchor; the other collision cells are not sprites.
            AddNativeSceneActorCommand(commands, resident.PositionX, resident.PositionY, 1,
                checked((uint)resident.ResidentSlot), (float)anchor.X, (float)anchor.Y,
                () => DrawCurrentNativeTowner(resident), () => IsCurrentNativeTownerVisible(resident));
        }
    }

    private void DrawCurrentNativeTowner(NativeTownResidentSnapshot resident)
    {
        var anchor = nativeSceneViewport!.Value.CellBottomAnchor(resident.PositionX, resident.PositionY);
        DrawNativeTowner(resident, new((float)anchor.X, (float)anchor.Y), true);
    }

    private bool IsCurrentNativeTownerVisible(NativeTownResidentSnapshot resident)
    {
        if (nativeSceneViewport is not { } view) return false;
        var anchor = view.CellBottomAnchor(resident.PositionX, resident.PositionY);
        var bounds = new NativeScenePixelBounds(anchor.X - 18, anchor.Y - 32, 36, 40);
        if (nativeTownerArtReady && nativeTownerAssets.Catalog is { } catalog
            && catalog.TryGetSprite(catalog.Residents[resident.ResidentSlot].SpriteId, out var sprite)) {
            var frame = sprite.ResolveFrame(nativeTownerClock.Tick, resident.NativeDirection);
            bounds = NativeScenePlacement.ResolveResidentBounds(anchor.X, anchor.Y, sprite.AlignmentWidth, frame.Width, frame.Height);
        }
        return bounds.IntersectsViewport(view.ViewportWidth, view.ViewportHeight);
    }

    /** Native pixel selection followed by native neighbouring-tile fallback. */
    public Vector2I? NativeTownerTileAt(Vector2 screenPosition)
    {
        if (snapshot?.Players.FirstOrDefault()?.LevelId != 0 || nativeSceneViewport is not { } view
            || !nativeTownerArtReady || nativeTownerAssets.Catalog is not { } catalog)
            return null;
        var point = ToLocal(screenPosition);
        var resident = NativeTownerPicking.Select(view, snapshot.NativeTownResidents, catalog,
            nativeTownerAssets.PickingMasks, nativeTownerClock.Tick, point.X, point.Y);
        return resident is null ? null : new(resident.PositionX, resident.PositionY);
    }

    private void DrawGenericTowners()
    {
        if (snapshot?.Players.FirstOrDefault()?.LevelId != 0)
            return;
        foreach (var resident in snapshot.NativeTownResidents.Where(row => row.LevelId == 0))
            DrawNativeTowner(resident, ToScreen(resident.PositionX, resident.PositionY), false);
    }

    private void DrawNativeTowner(NativeTownResidentSnapshot resident, Vector2 anchor, bool nativeScene)
    {
        var pack = cachedTownerPack ??= nativeTownerAssets.ObservePack();
        var size = nativeScene && nativeSceneViewport is { } viewport
            ? new Vector2(viewport.ViewportWidth, viewport.ViewportHeight) : GetViewportRect().Size;
        var anchorInside = anchor.X >= 0 && anchor.Y >= 0 && anchor.X < size.X && anchor.Y < size.Y;
        if (nativeScene && nativeTownerArtReady && nativeTownerAssets.Catalog is { } catalog) {
            var metadata = catalog.Residents[resident.ResidentSlot];
            if (catalog.TryGetSprite(metadata.SpriteId, out var sprite)) {
                var frame = sprite.ResolveFrame(nativeTownerClock.Tick, resident.NativeDirection);
                if (nativeTownerAssets.TryGetTexture(frame.AtlasId, out var texture)) {
                    var bounds = NativeScenePlacement.ResolveResidentBounds(anchor.X, anchor.Y,
                        sprite.AlignmentWidth, frame.Width, frame.Height);
                    var position = new Vector2((float)bounds.X, (float)bounds.Y);
                    var rectangle = new Rect2(position, new Vector2(frame.Width, frame.Height));
                    if (!IntersectsViewport(rectangle, size))
                        return;
                    DrawTextureRectRegion(texture, rectangle, new Rect2(frame.X, frame.Y, frame.Width, frame.Height));
                    catalog.TryGetAtlas(frame.AtlasId, out var atlas);
                    if (recordDrawDiagnostics) nativeTownerDraws.Add(new NativeTownerDrawObservation(resident.ResidentSlot, resident.TownerId,
                        resident.PositionX, resident.PositionY, resident.NativeDirection, true, false, pack.PackId,
                        atlas.Path, frame.DirectionIndex, frame.FrameIndex, sprite.AlignmentWidth,
                        anchor.X, anchor.Y, position.X, position.Y, frame.Width, frame.Height, nativeTownerClock.Tick, null, anchorInside));
                    return;
                }
            }
        }
        var fallback = new Rect2(anchor - new Vector2(18, 32), new Vector2(36, 40));
        if (!IntersectsViewport(fallback, size))
            return;
        var cow = resident.TownerId == 9;
        DrawCircle(anchor - new Vector2(0, 10), cow ? 14 : 10, cow ? new Color("b8aea0") : new Color("b19a65"));
        if (recordDrawDiagnostics) nativeTownerDraws.Add(new NativeTownerDrawObservation(resident.ResidentSlot, resident.TownerId,
            resident.PositionX, resident.PositionY, resident.NativeDirection, false, true, pack.PackId, null,
            0, 0, cow ? 128 : 96, anchor.X, anchor.Y, fallback.Position.X, fallback.Position.Y,
            36, 40, nativeTownerClock.Tick, pack.Diagnostic ?? "Native resident art requires an active native Town scene.", anchorInside));
    }
}
