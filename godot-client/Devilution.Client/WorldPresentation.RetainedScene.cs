using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client;

public partial class WorldPresentation
{
    private const int FloorChunkCells = NativeSceneRetentionState.FloorChunkCells;
    private readonly NativeSceneRetentionState retainedInvalidation = new();
    private readonly Dictionary<(int X, int Y), RetainedCell> retainedCells = [];
    private readonly SortedList<int, RetainedFloorChunk> retainedFloors = [];
    private readonly SortedList<int, RetainedSceneCanvas> retainedContentOrder = [];
    private readonly Dictionary<(int X, int Y), NativeSceneSpecial> retainedSpecials = [];
    private readonly List<RetainedCell> retainedVisibleCells = [];
    private readonly HashSet<(int X, int Y)> retainedCurrentCells = [];
    private readonly List<RetainedCell> retainedOldVisible = [];
    private Dictionary<(int X, int Y), List<NativeSceneActorCommand>> retainedActors = [];
    private RetainedSceneCanvas? retainedRoot;
    private Node2D? retainedFloorLayer;
    private Node2D? retainedContentLayer;
    private RetainedSceneCanvas? retainedPreview;
    private CanvasItem? drawingCanvas;
    private NativeSceneViewport retainedReferenceView;
    private LevelLayout? retainedLevel;
    private NativeSceneFamily? retainedFamily;
    private int[] retainedRanks = [];
    private ulong retainedGeneration;
    private long terrainSubmissions;
    private long nativeDrawTicks;
    internal bool CaptureFrameCost { get; set; }
    internal long ConsumeTerrainSubmissions() { var value = terrainSubmissions; terrainSubmissions = 0; return value; }
    internal double ConsumeDrawMilliseconds() { var value = nativeDrawTicks; nativeDrawTicks = 0; return value * 1000d / System.Diagnostics.Stopwatch.Frequency; }

    private bool PrepareRetainedNativeScene()
    {
        if (nativeSceneFamily is not { } family || nativeSceneViewport is not { } view || snapshot is null) {
            if (retainedRoot is not null) retainedRoot.Visible = false;
            retainedInvalidation.Invalidate();
            return false;
        }
        if (retainedRoot is null || retainedFamily != family || retainedLevel?.Id != currentLevel.Id
            || retainedReferenceView.LevelWidth != currentLevel.Width || retainedReferenceView.LevelHeight != currentLevel.Height) {
            if (retainedRoot is not null) { retainedRoot.Visible = false; retainedRoot.QueueFree(); }
            retainedCells.Clear(); retainedFloors.Clear(); retainedContentOrder.Clear(); retainedVisibleCells.Clear(); retainedSpecials.Clear();
            retainedFamily = family; retainedLevel = null; retainedReferenceView = view;
            retainedRoot = NewRetainedCanvas(() => { });
            retainedRoot.Name = "NativeRetainedScene";
            AddChild(retainedRoot);
            retainedFloorLayer = new Node2D {UseParentMaterial = true}; retainedRoot.AddChild(retainedFloorLayer);
            retainedPreview = NewRetainedCanvas(() => DrawNativeTargetPreview(nativeSceneViewport!.Value)); retainedRoot.AddChild(retainedPreview);
            retainedContentLayer = new Node2D {UseParentMaterial = true}; retainedRoot.AddChild(retainedContentLayer);
            retainedInvalidation.Invalidate();
            QueueRedraw();
        }
        retainedRoot.Visible = true;
        var refresh = retainedInvalidation.Update(currentLevel, currentLevel.Id, view, lastAcceptedSnapshotRevision,
            snapshot.Players[0].LightRadius);
        if (snapshotDataChanged) refresh |= NativeSceneRefresh.Actors;
        var translation = new Vector2((float)(view.OriginX - retainedReferenceView.OriginX), (float)(view.OriginY - retainedReferenceView.OriginY));
        // Static commands keep their reference coordinates. Canvas transforms scroll them without _Draw.
        retainedRoot.Position = translation;
        retainedPreview!.Position = -translation;
        if (refresh.HasFlag(NativeSceneRefresh.Viewport)) { retainedPreview.QueueRedraw(); QueueRedraw(); }
        if (refresh.HasFlag(NativeSceneRefresh.Geometry)) {
            retainedLevel = currentLevel;
            var order = GetCachedNativeSceneCellOrder(currentLevel.Width, currentLevel.Height);
            retainedRanks = new int[checked(currentLevel.Width * currentLevel.Height)];
            for (var i = 0; i < order.Count; i++) retainedRanks[order[i].Y * currentLevel.Width + order[i].X] = i;
            retainedSpecials.Clear();
            if (currentLevel.NativeSpecialLayer.Count != 0)
                foreach (var cell in order)
                    if (NativeScenePlacement.TryResolveSpecialAtDrawCell(family, currentLevel.Width, currentLevel.Height,
                            currentLevel.NativeSpecialLayer, cell.X, cell.Y, out var special, out _, out _))
                        retainedSpecials[cell] = special;
        }
        if (refresh.HasFlag(NativeSceneRefresh.Actors)) {
            retainedActors = BuildNativeSceneActorCommands(view);
            BindRetainedActorPositions();
        }
        if (RefreshRetainedActorPositions(view)) refresh |= NativeSceneRefresh.Actors;
        if (refresh != NativeSceneRefresh.None) UpdateRetainedCells(family, view);
        foreach (var cell in retainedVisibleCells) {
            if (cell.ActorCanvas is null) continue;
            cell.ActorCanvas.Position = -translation;
            cell.ActorCanvas.Visible = retainedActors.TryGetValue((cell.X, cell.Y), out var actors) && actors.Any(ActorVisible);
            if (cell.ActorCanvas.Visible) cell.ActorCanvas.QueueRedraw();
        }
        doorDrawObservations.Clear(); nativeTownerDraws.Clear(); nativeObjectDraws.Clear();
        return true;
    }

    private void UpdateRetainedCells(NativeSceneFamily family, NativeSceneViewport view)
    {
        retainedGeneration++;
        retainedOldVisible.Clear(); retainedOldVisible.AddRange(retainedVisibleCells);
        retainedVisibleCells.Clear(); retainedCurrentCells.Clear();
        foreach (var key in NativeSceneRetentionState.VisiblePieceCells(view, family.MicroTileLength)) retainedCurrentCells.Add(key);
        foreach (var (key, special) in retainedSpecials)
            if (NativeScenePlacement.ResolveSpecialBounds(view, key.X, key.Y, special).IntersectsViewport(view.ViewportWidth, view.ViewportHeight)) retainedCurrentCells.Add(key);
        foreach (var key in retainedActors.Keys) retainedCurrentCells.Add(key);
        foreach (var key in retainedCurrentCells) {
            var piece = currentLevel.NativePieceLayer[key.Y * currentLevel.Width + key.X];
            retainedSpecials.TryGetValue(key, out var special);
            if (!retainedCells.TryGetValue(key, out var cell)) {
                cell = new RetainedCell(key.X, key.Y, retainedRanks[key.Y * currentLevel.Width + key.X], retainedReferenceView);
                retainedCells.Add(key, cell);
            }
            cell.Used = retainedGeneration;
            var drawPiece = IntersectsViewport(ResolveNativePieceBounds(view, key.X, key.Y, family.MicroTileLength), new(view.ViewportWidth, view.ViewportHeight));
            var drawSpecial = special is not null && NativeScenePlacement.ResolveSpecialBounds(view, key.X, key.Y, special).IntersectsViewport(view.ViewportWidth, view.ViewportHeight);
            var changed = cell.Plan.Update(family, piece, special);
            if (changed) BindRetainedCell(family, cell);
            if (changed || cell.DrawPiece != drawPiece) {
                cell.DrawPiece = drawPiece;
                if (cell.Floors.Count != 0) GetRetainedFloor(cell).Canvas.QueueRedraw();
                cell.ContentCanvas?.QueueRedraw();
            }
            cell.DrawSpecial = drawSpecial;
            EnsureRetainedActors(cell);
            if (cell.ContentCanvas is { } content) content.Visible = drawPiece || drawSpecial || cell.ActorCanvas is not null;
            if (cell.SpecialCanvas is { } overlay) overlay.Visible = drawSpecial;
            retainedVisibleCells.Add(cell);
        }
        foreach (var old in retainedOldVisible) {
            if (retainedCurrentCells.Contains((old.X, old.Y))) continue;
            if (old.DrawPiece && old.Floors.Count != 0) GetRetainedFloor(old).Canvas.QueueRedraw();
            old.DrawPiece = false; old.DrawSpecial = false;
            if (old.ContentCanvas is { } node) node.Visible = false;
        }
        retainedVisibleCells.Sort(static (left, right) => left.Rank.CompareTo(right.Rank));
        // Bound retained canvas objects while crossing a floor; only off-screen entries are retired.
        if (retainedCells.Count > 2048)
            foreach (var old in retainedCells.Values.Where(value => value.Used != retainedGeneration).OrderBy(value => value.Used)
                         .Take(retainedCells.Count - 2048).ToArray()) {
                if (old.ContentCanvas is { } node) {
                    retainedContentLayer!.RemoveChild(node); node.QueueFree();
                    retainedContentOrder.Remove(old.Rank);
                }
                if (retainedFloors.TryGetValue(old.Rank / FloorChunkCells, out var chunk)) chunk.Cells.Remove(old);
                retainedCells.Remove((old.X, old.Y));
            }
    }

    private void BindRetainedCell(NativeSceneFamily family, RetainedCell cell)
    {
        var hadFloors = cell.Floors.Count != 0;
        cell.Floors.Clear(); cell.Content.Clear(); cell.Specials.Clear();
        BindRetainedRasters(family, cell.Plan.Floors, cell.Floors, NativeSceneRasterLayer.Floor);
        BindRetainedRasters(family, cell.Plan.Content, cell.Content, NativeSceneRasterLayer.Content);
        BindRetainedRasters(family, cell.Plan.Specials, cell.Specials, NativeSceneRasterLayer.Special);
        if (cell.Floors.Count != 0 || hadFloors) {
            var chunk = GetRetainedFloor(cell);
            if (!chunk.Cells.Contains(cell)) { chunk.Cells.Add(cell); chunk.Cells.Sort(static (a, b) => a.Rank.CompareTo(b.Rank)); }
            chunk.Canvas.QueueRedraw();
        }
        if (cell.Content.Count != 0 || cell.Specials.Count != 0) EnsureRetainedContent(cell);
        cell.ContentCanvas?.QueueRedraw(); cell.SpecialCanvas?.QueueRedraw();
    }

    private void BindRetainedRasters(NativeSceneFamily family, IReadOnlyList<NativeSceneRasterDraw> plan, List<RetainedRaster> commands, NativeSceneRasterLayer layer)
    {
        foreach (var (raster, bounds) in plan) {
            if (nativeSceneAssets.TryGetTexture(family, raster, out var texture, out var path, out var error) && texture is not null)
                commands.Add(new(texture, new((float)bounds.X, (float)bounds.Y, bounds.Width, bounds.Height), new(raster.X, raster.Y, raster.Width, raster.Height), path, layer));
            else nativeSceneDiagnostic = error;
        }
    }

    private RetainedFloorChunk GetRetainedFloor(RetainedCell cell)
    {
        // Contiguous ranges of the global cell order preserve floor overdraw across chunk boundaries.
        var key = cell.Rank / FloorChunkCells;
        if (retainedFloors.TryGetValue(key, out var chunk)) return chunk;
        var cells = new List<RetainedCell>();
        var canvas = NewRetainedCanvas(() => { foreach (var value in cells) if (value.DrawPiece) DrawRetainedRasters(value.Floors); });
        retainedFloorLayer!.AddChild(canvas);
        retainedFloors.Add(key, chunk = new(canvas, cells));
        retainedFloorLayer.MoveChild(canvas, retainedFloors.IndexOfKey(key));
        return chunk;
    }

    private void EnsureRetainedContent(RetainedCell cell)
    {
        if (cell.ContentCanvas is null) {
            cell.ContentCanvas = NewRetainedCanvas(() => { if (cell.DrawPiece) DrawRetainedRasters(cell.Content); });
            retainedContentOrder.Add(cell.Rank, cell.ContentCanvas);
            retainedContentLayer!.AddChild(cell.ContentCanvas);
            retainedContentLayer.MoveChild(cell.ContentCanvas, retainedContentOrder.IndexOfKey(cell.Rank));
        }
        if (cell.Specials.Count != 0 && cell.SpecialCanvas is null) {
            cell.SpecialCanvas = NewRetainedCanvas(() => DrawRetainedRasters(cell.Specials));
            cell.ContentCanvas.AddChild(cell.SpecialCanvas);
        }
    }

    private void EnsureRetainedActors(RetainedCell cell)
    {
        if (!retainedActors.ContainsKey((cell.X, cell.Y))) {
            if (cell.ActorCanvas is { } old) old.Visible = false;
            return;
        }
        EnsureRetainedContent(cell);
        if (cell.ActorCanvas is null) {
            cell.ActorCanvas = NewRetainedCanvas(() => {
                if (retainedActors.TryGetValue((cell.X, cell.Y), out var actors))
                    foreach (var actor in actors) if (ActorVisible(actor)) actor.Draw();
            });
            cell.ContentCanvas!.AddChild(cell.ActorCanvas);
            // Per cell: parent content, then actors, then specials; the next cell follows in tree order.
            cell.ContentCanvas.MoveChild(cell.ActorCanvas, 0);
        }
    }

    private static bool ActorVisible(NativeSceneActorCommand actor) => actor.Visible?.Invoke() ?? true;
    private RetainedSceneCanvas NewRetainedCanvas(Action action) => new() {OwnerView = this, DrawAction = action, UseParentMaterial = true};
    private void DrawRetainedRasters(List<RetainedRaster> rasters)
    {
        foreach (var raster in rasters) {
            drawingCanvas!.DrawTextureRectRegion(raster.Texture, raster.Destination, raster.Source, Colors.White);
            terrainSubmissions++;
        }
    }

    /** Counts the visible commands held by the canvas; submission counters separately measure actual _Draw calls. */
    private NativeSceneDrawingObservation? ObserveRetainedDrawing()
    {
        if (nativeSceneFamily is not { } family || nativeSceneViewport is not { } view || retainedRoot is null) return ObserveInactiveNativeSceneDrawing();
        var frame = new NativeSceneRenderFrame(nativeSceneAssets.ObservePack());
        var offset = retainedRoot.Position;
        var visibleCount = 0;
        foreach (var cell in retainedVisibleCells) {
            if (!cell.DrawPiece && !cell.DrawSpecial && (!retainedActors.TryGetValue((cell.X, cell.Y), out var atCell) || !atCell.Any(ActorVisible))) continue;
            visibleCount++;
            if (cell.DrawPiece) foreach (var raster in cell.Floors) frame.RecordRaster(raster.Layer, raster.Path, new(raster.Destination.Position + offset, raster.Destination.Size));
            if (cell.DrawPiece) foreach (var raster in cell.Content) frame.RecordRaster(raster.Layer, raster.Path, new(raster.Destination.Position + offset, raster.Destination.Size));
            if (retainedActors.TryGetValue((cell.X, cell.Y), out var actors)) foreach (var actor in actors) {
                if (!ActorVisible(actor)) continue;
                frame.ActorDrawCount++;
                if (frame.FirstActorAnchor is null) {
                    var anchor = RetainedActorAnchor(actor, view);
                    frame.FirstActorAnchor = new((float)anchor.X, (float)anchor.Y);
                }
            }
            if (cell.DrawSpecial) foreach (var raster in cell.Specials) frame.RecordRaster(raster.Layer, raster.Path, new(raster.Destination.Position + offset, raster.Destination.Size));
        }
        return frame.Complete(family, nativeSceneContentManifestHash, currentLevel.Id, visibleCount,
            currentLevel.Width * currentLevel.Height - visibleCount, nativeSceneDiagnostic);
    }

    private sealed partial class RetainedSceneCanvas : Node2D
    {
        internal WorldPresentation OwnerView = null!;
        internal Action DrawAction = null!;
        public override void _Draw()
        {
            var started = OwnerView.CaptureFrameCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            OwnerView.drawingCanvas = this;
            try { DrawAction(); }
            finally {
                OwnerView.drawingCanvas = null;
                if (started != 0) OwnerView.nativeDrawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            }
        }
    }

    private sealed class RetainedCell(int x, int y, int rank, NativeSceneViewport referenceView)
    {
        internal readonly int X = x, Y = y, Rank = rank;
        internal readonly NativeSceneRetainedCell Plan = new(referenceView, x, y);
        internal bool DrawPiece, DrawSpecial;
        internal ulong Used;
        internal readonly List<RetainedRaster> Floors = [], Content = [], Specials = [];
        internal RetainedSceneCanvas? ContentCanvas, ActorCanvas, SpecialCanvas;
    }
    private sealed record RetainedFloorChunk(RetainedSceneCanvas Canvas, List<RetainedCell> Cells);
    private readonly record struct RetainedRaster(Texture2D Texture, Rect2 Destination, Rect2 Source, string? Path, NativeSceneRasterLayer Layer);

    // Existing actor drawing stays in one place; only its destination canvas changes.
    private new void DrawTextureRectRegion(Texture2D texture, Rect2 destination, Rect2 source, Color? color = null, bool transpose = false, bool clipUv = true)
    { if (drawingCanvas is { } canvas) canvas.DrawTextureRectRegion(texture, destination, source, color, transpose, clipUv); else base.DrawTextureRectRegion(texture, destination, source, color, transpose, clipUv); }
    private new void DrawTextureRect(Texture2D texture, Rect2 destination, bool tile, Color? color = null, bool transpose = false)
    { if (drawingCanvas is { } canvas) canvas.DrawTextureRect(texture, destination, tile, color, transpose); else base.DrawTextureRect(texture, destination, tile, color, transpose); }
    private new void DrawRect(Rect2 rectangle, Color color, bool filled = true, float width = -1, bool antialiased = false)
    { if (drawingCanvas is { } canvas) canvas.DrawRect(rectangle, color, filled, width, antialiased); else base.DrawRect(rectangle, color, filled, width, antialiased); }
    private new void DrawCircle(Vector2 position, float radius, Color color, bool filled = true, float width = -1, bool antialiased = false)
    { if (drawingCanvas is { } canvas) canvas.DrawCircle(position, radius, color, filled, width, antialiased); else base.DrawCircle(position, radius, color, filled, width, antialiased); }
    private new void DrawLine(Vector2 from, Vector2 to, Color color, float width = -1, bool antialiased = false)
    { if (drawingCanvas is { } canvas) canvas.DrawLine(from, to, color, width, antialiased); else base.DrawLine(from, to, color, width, antialiased); }
    private new void DrawArc(Vector2 center, float radius, float start, float end, int points, Color color, float width = -1, bool antialiased = false)
    { if (drawingCanvas is { } canvas) canvas.DrawArc(center, radius, start, end, points, color, width, antialiased); else base.DrawArc(center, radius, start, end, points, color, width, antialiased); }
    private new void DrawColoredPolygon(Vector2[] points, Color color, Vector2[]? uvs = null, Texture2D? texture = null)
    { if (drawingCanvas is { } canvas) canvas.DrawColoredPolygon(points, color, uvs, texture); else base.DrawColoredPolygon(points, color, uvs, texture); }
}
