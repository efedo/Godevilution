using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

internal sealed record NativeSceneDrawRectObservation(float X, float Y, float Width, float Height);

internal sealed record NativeSceneDrawPointObservation(float X, float Y);

internal sealed record NativeSceneDrawingObservation(
    bool NativeSceneActive,
    bool GenericFallback,
    string? PackId,
    bool IdentityMatched,
    string? ContentManifestHash,
    uint? LevelId,
    string? Location,
    byte? MicroTileLength,
    int FloorRasterDrawCount,
    int ContentRasterDrawCount,
    int ActorDrawCount,
    int SpecialRasterDrawCount,
    int TextureDrawCount,
    int CellsDrawn,
    int CellsCulled,
    IReadOnlyList<string> LoadedAtlasPaths,
    IReadOnlyList<string> MissingAtlasPaths,
    NativeSceneDrawRectObservation? FirstFloorRect,
    NativeSceneDrawRectObservation? FirstContentRect,
    NativeSceneDrawRectObservation? FirstSpecialRect,
    NativeSceneDrawPointObservation? FirstActorAnchor,
    string DrawOrder,
    string ScopeLimitations,
    string? Diagnostic);

public partial class WorldPresentation
{
    private const string NativeSceneDrawOrder = "floors-global;cells-by-(x+y,x,y):content,entities,specials";
    private const string NativeSceneScopeLimitations =
        "Actor and wall ordering is approximate; native pre/post object, item, and missile phases and wall lookahead are not reproduced. Native lighting, palette cycling, and Cathedral wall transparency blending are also not reproduced; scene parity remains open.";
    private const int NativeSceneCellOrderCacheCapacity = 16;
    private static readonly Dictionary<(int Width, int Height), IReadOnlyList<(int X, int Y)>> NativeSceneCellOrderCache = [];
    private static readonly Queue<(int Width, int Height)> NativeSceneCellOrderCacheOrder = [];

    private readonly NativeSceneAssets nativeSceneAssets = new();
    private string? nativeSceneContentManifestHash;
    private NativeSceneFamily? nativeSceneFamily;
    private NativeSceneViewport? nativeSceneViewport;
    private string? nativeSceneDiagnostic;
    private LevelLayout? validatedNativeSceneLayout;

    internal NativeScenePackObservation ObserveNativeScenePack()
        => nativeSceneAssets.ObservePack();

    internal string? ObserveNativeSceneDiagnostic()
        => nativeSceneDiagnostic;

    internal NativeSceneDrawingObservation? ObserveNativeSceneDrawing()
        => ObserveRetainedDrawing();

    private void BindNativeSceneIdentity(string? contentManifestHash)
    {
        if (!nativeSceneAssets.SetContentManifestHash(contentManifestHash))
            return;
        nativeSceneContentManifestHash = string.IsNullOrWhiteSpace(contentManifestHash)
            ? null
            : contentManifestHash;
        nativeSceneFamily = null;
        nativeSceneViewport = null;
        validatedNativeSceneLayout = null;
    }

    private void ResolveNativeScene(PlayerSnapshot? localPlayer)
    {
        nativeSceneViewport = null;
        if (localPlayer is null) {
            nativeSceneFamily = null;
            nativeSceneDiagnostic = "Native scene rendering is inactive because no local player is present.";
            return;
        }
        if (currentLevel.Id != localPlayer.LevelId) {
            nativeSceneFamily = null;
            nativeSceneDiagnostic = "Native scene rendering is inactive because the selected level does not match the local player.";
            return;
        }
        if (!ReferenceEquals(validatedNativeSceneLayout, currentLevel)) {
            validatedNativeSceneLayout = currentLevel;
            nativeSceneFamily = null;
            nativeSceneDiagnostic = null;
            if (nativeSceneAssets.TryResolveFamily(
                nativeSceneContentManifestHash,
                localPlayer.LevelId,
                currentLevel,
                out var family,
                out nativeSceneDiagnostic))
                nativeSceneFamily = family;
        }
        if (nativeSceneFamily is null) return;
        var viewportWidth = GetParent() is Control frame
            ? Math.Max(1, Mathf.RoundToInt(frame.Size.X))
            : Math.Max(1, Mathf.RoundToInt(GetViewportRect().Size.X));
        var viewportHeight = GetParent() is Control parentFrame
            ? Math.Max(1, Mathf.RoundToInt(parentFrame.Size.Y))
            : Math.Max(1, Mathf.RoundToInt(GetViewportRect().Size.Y));
        nativeSceneViewport = NativeSceneViewport.Create(
            viewportWidth,
            viewportHeight,
            currentLevel.Width,
            currentLevel.Height,
            localPlayer.PositionX,
            localPlayer.PositionY);
    }

    private NativeSceneDrawingObservation ObserveInactiveNativeSceneDrawing()
    {
        var pack = nativeSceneAssets.ObservePack();
        return new NativeSceneDrawingObservation(
                false,
                true,
                pack.PackId,
                pack.IdentityMatched,
                nativeSceneContentManifestHash,
                snapshot?.Players.FirstOrDefault()?.LevelId,
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                null,
                null,
                null,
                NativeSceneDrawOrder,
                NativeSceneScopeLimitations,
                nativeSceneDiagnostic ?? pack.Diagnostic);
    }

    private bool DrawNativeSceneWorld()
    {
        if (nativeSceneFamily is null || nativeSceneViewport is null) return false;
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color("090914"));
        return true;
    }

    private static IReadOnlyList<(int X, int Y)> GetCachedNativeSceneCellOrder(int width, int height)
    {
        var key = (width, height);
        if (NativeSceneCellOrderCache.TryGetValue(key, out var cached))
            return cached;
        var order = NativeSceneCatalog.ResolveIsometricCellOrder(width, height);
        if (NativeSceneCellOrderCache.Count >= NativeSceneCellOrderCacheCapacity)
            NativeSceneCellOrderCache.Remove(NativeSceneCellOrderCacheOrder.Dequeue());
        NativeSceneCellOrderCache.Add(key, order);
        NativeSceneCellOrderCacheOrder.Enqueue(key);
        return order;
    }

    private static Rect2 ResolveNativePieceBounds(
        NativeSceneViewport sceneViewport,
        int x,
        int y,
        byte microTileLength)
    {
        var anchor = sceneViewport.CellBottomAnchor(x, y);
        var rows = microTileLength / 2;
        var topY = anchor.Y - 31 - (rows - 1) * 32;
        return new Rect2(
            (float)(anchor.X - NativeSceneViewport.HalfTileWidth),
            (float)topY,
            64,
            (float)(anchor.Y - topY + 1));
    }

    private static bool IntersectsViewport(Rect2 rectangle, Vector2 viewportSize)
        => rectangle.Position.X < viewportSize.X
            && rectangle.End.X > 0
            && rectangle.Position.Y < viewportSize.Y
            && rectangle.End.Y > 0;

    private Dictionary<(int X, int Y), List<NativeSceneActorCommand>> BuildNativeSceneActorCommands(
        NativeSceneViewport sceneViewport)
    {
        var commands = new Dictionary<(int X, int Y), List<NativeSceneActorCommand>>();
        var localPlayer = snapshot!.Players[0];
        var levelId = localPlayer.LevelId;
        var viewerX = localPlayer.PositionX;
        var viewerY = localPlayer.PositionY;
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == levelId);
        var lightRadius = localPlayer.LightRadius is >= 2 and <= 15 ? localPlayer.LightRadius : 10;
        var searchActive = StatusPresentationStyle.ResolvePlayer(localPlayer)
            .Contains(StatusPresentationKind.Search);
        foreach (var player in visiblePlayers) {
            var isLocal = player.EntityId == localPlayer.EntityId;
            var projected = AuthoritativePlayerPositionProjection.ResolvePresentation(
                player,
                nativePlayerPresentationTick ?? snapshot.Tick,
                isLocal ? predictedPlayerPosition : null);
            var anchor = sceneViewport.CellBottomAnchor(projected.X, projected.Y);
            AddNativeSceneActorCommand(
                commands,
                player.PositionX,
                player.PositionY,
                0,
                player.EntityId,
                (float)anchor.X,
                (float)anchor.Y,
                () => DrawPlayer(
                    player,
                    isLocal ? predictedPlayerPosition : null,
                    isLocal,
                    levelId,
                    geometry,
                    viewerX,
                    viewerY,
                    lightRadius));
        }
        foreach (var monster in visibleMonsters) {
            var projected = AuthoritativeMonsterPositionProjection.ResolvePresentation(
                monster,
                nativePlayerPresentationTick ?? snapshot.Tick);
            var anchor = sceneViewport.CellBottomAnchor(projected.X, projected.Y);
            AddNativeSceneActorCommand(
                commands,
                monster.PositionX,
                monster.PositionY,
                1,
                monster.EntityId,
                (float)anchor.X,
                (float)anchor.Y,
                () => DrawMonster(monster));
        }
        foreach (var item in visibleItems) {
            AddNativeSceneActorCommand(
                commands,
                item.PositionX,
                item.PositionY,
                2,
                item.EntityId,
                sceneViewport,
                () => DrawItem(item, searchActive));
        }
        foreach (var @object in visibleObjects) {
            AddNativeSceneActorCommand(
                commands,
                @object.PositionX,
                @object.PositionY,
                3,
                @object.EntityId,
                sceneViewport,
                () => DrawObject(@object));
        }
        foreach (var corpse in visibleCorpses) {
            AddNativeSceneActorCommand(
                commands,
                corpse.PositionX,
                corpse.PositionY,
                4,
                corpse.EntityId,
                sceneViewport,
                () => DrawCorpse(corpse));
        }
        foreach (var portal in visiblePortals) {
            if (AuthoritativePortalPresentation.Resolve(
                    portal,
                    levelId,
                    geometry,
                    viewerX,
                    viewerY,
                    lightRadius) is not { } endpoint)
                continue;
            AddNativeSceneActorCommand(
                commands,
                endpoint.X,
                endpoint.Y,
                5,
                portal.PortalId,
                sceneViewport,
                () => DrawPortal(portal, levelId, geometry, viewerX, viewerY, lightRadius));
        }
        foreach (var projectile in visibleProjectiles) {
            AddNativeSceneActorCommand(
                commands,
                projectile.PositionX,
                projectile.PositionY,
                6,
                projectile.EntityId,
                sceneViewport,
                () => DrawProjectile(projectile));
        }
        AddNativeTownerCommands(commands, sceneViewport);
        foreach (var actors in commands.Values)
            actors.Sort(NativeSceneActorCommand.Compare);
        return commands;
    }

    private void AddNativeSceneActorCommand(
        Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        int x,
        int y,
        int priority,
        uint entityId,
        NativeSceneViewport sceneViewport,
        Action draw)
    {
        var anchor = sceneViewport.CellBottomAnchor(x, y);
        AddNativeSceneActorCommand(
            commands,
            x,
            y,
            priority,
            entityId,
            (float)anchor.X,
            (float)anchor.Y,
            draw);
    }

    private void AddNativeSceneActorCommand(
        Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        int x,
        int y,
        int priority,
        uint entityId,
        float anchorX,
        float anchorY,
        Action draw,
        Func<bool>? visible = null)
    {
        if (x < 0 || y < 0 || x >= currentLevel.Width || y >= currentLevel.Height)
            return;
        var key = (x, y);
        if (!commands.TryGetValue(key, out var actors)) {
            actors = [];
            commands.Add(key, actors);
        }
        actors.Add(new NativeSceneActorCommand(priority, entityId, anchorX, anchorY, draw, visible));
    }

    private void DrawNativeTargetPreview(NativeSceneViewport sceneViewport)
    {
        if (targetPreview is not { } target
            || target.X < 0 || target.Y < 0
            || target.X >= currentLevel.Width || target.Y >= currentLevel.Height)
            return;
        var center = sceneViewport.WorldToScreen(target.X, target.Y);
        var point = new Vector2((float)center.X, (float)center.Y);
        var top = point + new Vector2(0, -16);
        var right = point + new Vector2(32, 0);
        var bottom = point + new Vector2(0, 16);
        var left = point + new Vector2(-32, 0);
        var color = new Color("f4d35e");
        DrawColoredPolygon(new[] { top, right, bottom, left }, new Color(color, 0.16f));
        DrawLine(top, right, color, 2);
        DrawLine(right, bottom, color, 2);
        DrawLine(bottom, left, color, 2);
        DrawLine(left, top, color, 2);
    }

    private sealed record NativeSceneActorCommand(
        int Priority,
        uint EntityId,
        float AnchorX,
        float AnchorY,
        Action Draw,
        Func<bool>? Visible)
    {
        public static int Compare(NativeSceneActorCommand left, NativeSceneActorCommand right)
        {
            var comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
        }
    }

    private enum NativeSceneRasterLayer
    {
        Floor,
        Content,
        Special,
    }

    private sealed class NativeSceneRenderFrame
    {
        private readonly NativeScenePackObservation pack;
        private readonly HashSet<string> loadedAtlasPaths = new(StringComparer.Ordinal);
        private readonly HashSet<string> missingAtlasPaths = new(StringComparer.Ordinal);

        public NativeSceneRenderFrame(NativeScenePackObservation pack)
        {
            this.pack = pack;
        }

        public int FloorRasterDrawCount { get; private set; }
        public int ContentRasterDrawCount { get; private set; }
        public int ActorDrawCount { get; set; }
        public int SpecialRasterDrawCount { get; private set; }
        public int TextureDrawCount { get; private set; }
        public NativeSceneDrawRectObservation? FirstFloorRect { get; private set; }
        public NativeSceneDrawRectObservation? FirstContentRect { get; private set; }
        public NativeSceneDrawRectObservation? FirstSpecialRect { get; private set; }
        public NativeSceneDrawPointObservation? FirstActorAnchor { get; set; }

        public void RecordMissingAtlas(string? path)
        {
            if (path is not null)
                missingAtlasPaths.Add(path);
        }

        public void RecordRaster(NativeSceneRasterLayer layer, string? path, Rect2 destination)
        {
            TextureDrawCount++;
            if (path is not null)
                loadedAtlasPaths.Add(path);
            switch (layer) {
            case NativeSceneRasterLayer.Floor:
                FloorRasterDrawCount++;
                FirstFloorRect ??= new(destination.Position.X, destination.Position.Y, destination.Size.X, destination.Size.Y);
                break;
            case NativeSceneRasterLayer.Content:
                ContentRasterDrawCount++;
                FirstContentRect ??= new(destination.Position.X, destination.Position.Y, destination.Size.X, destination.Size.Y);
                break;
            case NativeSceneRasterLayer.Special:
                SpecialRasterDrawCount++;
                FirstSpecialRect ??= new(destination.Position.X, destination.Position.Y, destination.Size.X, destination.Size.Y);
                break;
            }
        }

        public NativeSceneDrawingObservation Complete(
            NativeSceneFamily family,
            string? contentManifestHash,
            uint levelId,
            int cellsDrawn,
            int cellsCulled,
            string? diagnostic)
            => new(
                true,
                false,
                pack.PackId,
                pack.IdentityMatched,
                contentManifestHash,
                levelId,
                NativeSceneCatalog.FormatLocation(family.Location),
                family.MicroTileLength,
                FloorRasterDrawCount,
                ContentRasterDrawCount,
                ActorDrawCount,
                SpecialRasterDrawCount,
                TextureDrawCount,
                cellsDrawn,
                cellsCulled,
                loadedAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                missingAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                FirstFloorRect,
                FirstContentRect,
                FirstSpecialRect,
                FirstActorAnchor,
                NativeSceneDrawOrder,
                NativeSceneScopeLimitations,
                diagnostic);
    }
}
