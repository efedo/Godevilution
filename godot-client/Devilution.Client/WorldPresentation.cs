using Devilution.Client.Protocol;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Godot;
using ProjectilePresentationKind = Devilution.Client.Protocol.ProjectilePresentationKind;
using ItemPresentationKind = Devilution.Client.Protocol.ItemPresentationKind;
using ObjectPresentationKind = Devilution.Client.Protocol.ObjectPresentationKind;
using MonsterPresentationKind = Devilution.Client.Protocol.MonsterPresentationKind;

namespace Devilution.Client;

/** Asset-backed world view driven exclusively by authoritative snapshots. */
public partial class WorldPresentation : Node2D
{
    private const float TileSize = 32;
    private AuthoritativeWorldViewport viewport = AuthoritativeWorldViewport.Create(
        1280,
        720,
        LevelLayout.Empty.Width,
        LevelLayout.Empty.Height,
        0,
        0,
        TileSize);
    private readonly Dictionary<uint, Vector2> displayedPositions = [];
    private readonly Dictionary<uint, uint> displayedPositionLevels = [];
    private readonly Dictionary<uint, PlayerSnapshot> displayedPlayerSnapshots = [];
    private readonly Dictionary<uint, PresentationPlayerTextureBindingObservation> playerTextureBindings = [];
    private readonly Dictionary<uint, List<PresentationPlayerTextureBindingObservation>> playerTextureBindingHistory = [];
    private readonly Dictionary<uint, NativePlayerDrawObservation> nativePlayerDrawObservations = [];
    private readonly Dictionary<uint, List<NativePlayerDrawObservation>> nativePlayerDrawHistory = [];
    private readonly Dictionary<uint, MonsterSnapshot> displayedMonsterSnapshots = [];
    private readonly Dictionary<uint, ProjectileSnapshot> displayedProjectileSnapshots = [];
    private readonly Dictionary<uint, CathedralDoorDrawObservation> doorDrawObservations = [];
    private readonly AssetPalette assets = new();
    private readonly LevelLayoutCatalog levels = new();
    private readonly AuthoritativeGeometryInstallationGate geometryInstallation = new();
    private readonly AuthoritativePresentationClock presentationClock = new();
    private double? nativePlayerPresentationTick;
    private ulong lastAcceptedSnapshotRevision;
    private ulong pendingPresentationEpochRevision;
    private bool presentationEpochPending;
    internal ulong PresentationEpochResetCount { get; private set; }
    private Snapshot? snapshot;
    private uint? presentationLevelId;
    private ClientPosition? predictedPlayerPosition;
    private Vector2I? targetPreview;
    private LevelLayout currentLevel = LevelLayout.Empty;

    private float OriginX => (float)viewport.OriginX;

    private float OriginY => (float)viewport.OriginY;

    public void Apply(
        Snapshot? next,
        double delta,
        ClientPosition? predicted = null,
        uint serverTickRateHz = AuthoritativePresentationClock.DefaultTickRateHz)
        => ApplyCore(next, delta, predicted, serverTickRateHz, null, true);

    /** Applies an admitted model snapshot while reusing its installed geometry generation. */
    internal void ApplyAccepted(
        Snapshot? next,
        ulong acceptedSnapshotRevision,
        double delta,
        ClientPosition? predicted = null,
        uint serverTickRateHz = AuthoritativePresentationClock.DefaultTickRateHz,
        bool transportConnected = true)
        => ApplyCore(next, delta, predicted, serverTickRateHz, acceptedSnapshotRevision, transportConnected);

    /** Freezes presentation animation until a newer admitted model epoch arrives. */
    internal void MarkPresentationEpochPending()
    {
        pendingPresentationEpochRevision = lastAcceptedSnapshotRevision;
        presentationEpochPending = true;
    }

    internal PresentationPackObservation ObservePresentationPack()
        => assets.ObservePack();

    internal NativePlayerPackObservation ObserveNativePlayerPack()
        => assets.ObserveNativePlayerPack();

    /** Returns the latest native player draw result, including explicit atlas fallback state. */
    internal NativePlayerDrawObservation? ObserveNativePlayerDrawing(uint entityId)
        => nativePlayerDrawObservations.TryGetValue(entityId, out var observation) ? observation : null;

    /** Returns bounded distinct frame observations made by the actual renderer. */
    internal IReadOnlyList<NativePlayerDrawObservation> ObserveNativePlayerDrawingHistory(uint entityId)
        => nativePlayerDrawHistory.TryGetValue(entityId, out var history)
            ? history.ToArray()
            : Array.Empty<NativePlayerDrawObservation>();

    /** Binds class-specific art to the exact content identity admitted by the server. */
    internal void SetNativePlayerContentIdentity(string? contentManifestHash)
    {
        if (!assets.SetNativePlayerContentIdentity(contentManifestHash))
            return;
        nativePlayerPresentationTick = null;
        playerTextureBindings.Clear();
        playerTextureBindingHistory.Clear();
        nativePlayerDrawObservations.Clear();
        nativePlayerDrawHistory.Clear();
    }

    /** Records from actual drawing, not from a snapshot-derived observation getter. */
    internal CathedralDoorDrawObservation? ObserveCathedralDoorDrawing(uint entityId)
        => doorDrawObservations.TryGetValue(entityId, out var observation) ? observation : null;

    internal PresentationTextureBindingObservation? ObserveTextureBinding(string assetName)
        => assets.ObserveTexture(assetName);

    /** Returns the binding recorded by the actual per-entity world draw. */
    internal PresentationPlayerTextureBindingObservation? ObservePlayerTextureBinding(uint entityId)
        => playerTextureBindings.TryGetValue(entityId, out var observation) ? observation : null;

    /** Returns a bounded history of distinct actor selections observed during actual drawing. */
    internal IReadOnlyList<PresentationPlayerTextureBindingObservation> ObservePlayerTextureBindingHistory(uint entityId)
        => playerTextureBindingHistory.TryGetValue(entityId, out var history)
            ? history.ToArray()
            : Array.Empty<PresentationPlayerTextureBindingObservation>();

    private void ApplyCore(
        Snapshot? next,
        double delta,
        ClientPosition? predicted,
        uint serverTickRateHz,
        ulong? acceptedSnapshotRevision,
        bool transportConnected)
    {
        if (acceptedSnapshotRevision is { } acceptedRevision)
            lastAcceptedSnapshotRevision = acceptedRevision;
        if (transportConnected
            && presentationEpochPending
            && acceptedSnapshotRevision is { } epochRevision
            && epochRevision != pendingPresentationEpochRevision) {
            presentationEpochPending = false;
            nativePlayerPresentationTick = null;
            assets.ResetAnimation();
            PresentationEpochResetCount++;
        }
        snapshot = next;
        predictedPlayerPosition = predicted;
        if (snapshot is null) {
            displayedPositions.Clear();
            displayedPositionLevels.Clear();
            displayedPlayerSnapshots.Clear();
            playerTextureBindings.Clear();
            playerTextureBindingHistory.Clear();
            nativePlayerDrawObservations.Clear();
            nativePlayerDrawHistory.Clear();
            displayedMonsterSnapshots.Clear();
            displayedProjectileSnapshots.Clear();
            presentationClock.Reset();
            nativePlayerPresentationTick = null;
            assets.ResetAnimation();
            presentationLevelId = null;
            geometryInstallation.Invalidate();
            levels.ApplyAuthoritative(Array.Empty<LevelGeometrySnapshot>());
            currentLevel = LevelLayout.Empty;
            viewport = CreateViewportTransform(currentLevel, null);
            QueueRedraw();
            return;
        }

        var localPlayer = snapshot.Players.FirstOrDefault();
        if (presentationLevelId != localPlayer?.LevelId) {
            presentationClock.Reset();
            nativePlayerPresentationTick = null;
            assets.ResetAnimation();
            presentationLevelId = localPlayer?.LevelId;
        }
        var presentationTick = presentationClock.Advance(
            snapshot.Tick,
            delta,
            serverTickRateHz);
        var snapshotRewound = presentationClock.LastAdvanceRewound;
        if (nativePlayerPresentationTick is null || snapshotRewound)
            nativePlayerPresentationTick = snapshot.Tick;
        else if (transportConnected && !presentationEpochPending)
            nativePlayerPresentationTick = presentationTick;
        if (snapshotRewound)
            assets.ResetAnimation();
        else if (transportConnected && !presentationEpochPending)
            assets.AdvanceAnimation(delta);

        var activeEntityIds = new HashSet<uint>();
        if (acceptedSnapshotRevision is { } geometryRevision) {
            if (geometryInstallation.NeedsInstallation(geometryRevision)) {
                levels.ApplyAuthoritative(snapshot.LevelGeometry);
                geometryInstallation.MarkInstalled(geometryRevision);
            }
        } else {
            geometryInstallation.Invalidate();
            levels.ApplyAuthoritative(snapshot.LevelGeometry);
        }
        currentLevel = levels.Resolve(localPlayer?.LevelId ?? 0);
        var nextViewport = CreateViewportTransform(currentLevel, localPlayer);
        if (nextViewport.ViewportWidth != viewport.ViewportWidth || nextViewport.ViewportHeight != viewport.ViewportHeight) {
            displayedPositions.Clear();
            displayedPositionLevels.Clear();
        }
        viewport = nextViewport;
        var localGeometry = localPlayer is null
            ? null
            : snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == localPlayer.LevelId);
        var localLightRadius = localPlayer?.LightRadius is >= 2 and <= 15
            ? localPlayer.LightRadius
            : 10;

        foreach (var player in snapshot.Players) {
            if (player.EntityId != localPlayer?.EntityId
                && (localPlayer is null
                    || !AuthoritativeNativeVisionVisibility.IsVisible(
                        player.LevelId,
                        player.PositionX,
                        player.PositionY,
                        localPlayer.LevelId,
                        localGeometry,
                        localPlayer.PositionX,
                        localPlayer.PositionY,
                        localLightRadius)))
                continue;
            activeEntityIds.Add(player.EntityId);
            var projected = AuthoritativePlayerPositionProjection.ResolvePresentation(
                player,
                presentationTick,
                player.EntityId == localPlayer?.EntityId ? predicted : null);
            var reset = displayedPlayerSnapshots.TryGetValue(player.EntityId, out var previous)
                && AuthoritativePlayerPositionProjection.RequiresPresentationReset(previous, player)
                || snapshotRewound;
            UpdatePosition(
                player.EntityId,
                ToScreen(
                    projected.X,
                    projected.Y),
                delta,
                player.LevelId,
                reset);
            displayedPlayerSnapshots[player.EntityId] = player.Clone();
        }
        foreach (var monster in snapshot.Monsters.Where(monster =>
                     localPlayer is not null
                     && AuthoritativeMonsterPresentationVisibility.IsVisible(
                         monster,
                         localPlayer.LevelId,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius))) {
            activeEntityIds.Add(monster.EntityId);
            var reset = displayedMonsterSnapshots.TryGetValue(monster.EntityId, out var previous)
                && AuthoritativeMonsterPositionProjection.RequiresPresentationReset(previous, monster)
                || snapshotRewound;
            var projected = AuthoritativeMonsterPositionProjection.ResolvePresentation(
                monster,
                presentationTick);
            UpdatePosition(
                monster.EntityId,
                ToScreen(projected.X, projected.Y),
                delta,
                monster.LevelId,
                reset);
            displayedMonsterSnapshots[monster.EntityId] = monster.Clone();
        }
        foreach (var item in snapshot.WorldItems.Where(item =>
                     localPlayer is not null
                     && AuthoritativeWorldItemPresentationVisibility.IsVisible(
                         item,
                         localPlayer.LevelId,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius))) {
            activeEntityIds.Add(item.EntityId);
            UpdatePosition(item.EntityId, ToScreen(item.PositionX, item.PositionY), delta, item.LevelId);
        }
        foreach (var @object in snapshot.Objects.Where(@object =>
                     localPlayer is not null
                     && AuthoritativeObjectPresentationVisibility.IsVisible(
                         @object,
                         localPlayer.LevelId,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius))) {
            activeEntityIds.Add(@object.EntityId);
            UpdatePosition(@object.EntityId, ToScreen(@object.PositionX, @object.PositionY), delta, @object.LevelId);
        }
        foreach (var corpse in snapshot.Corpses.Where(corpse =>
                     localPlayer is not null
                     && AuthoritativeCorpsePresentationVisibility.IsVisible(
                         corpse,
                         localPlayer.LevelId,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius))) {
            activeEntityIds.Add(corpse.EntityId);
            UpdatePosition(corpse.EntityId, ToScreen(corpse.PositionX, corpse.PositionY), delta, corpse.LevelId);
        }
        foreach (var portal in snapshot.Portals) {
            if (localPlayer is not null
                && AuthoritativePortalPresentation.Resolve(
                    portal,
                    localPlayer.LevelId,
                    localGeometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    localLightRadius) is { } endpoint) {
                activeEntityIds.Add(portal.PortalId);
                UpdatePosition(portal.PortalId, ToScreen(endpoint.X, endpoint.Y), delta, localPlayer.LevelId);
            }
        }
        foreach (var projectile in snapshot.Projectiles.Where(projectile =>
                     AuthoritativeEntityPositionProjection.IsVisibleOnActiveLevel(
                         projectile.LevelId,
                         localPlayer?.LevelId ?? 0))) {
            activeEntityIds.Add(projectile.EntityId);
            var reset = displayedProjectileSnapshots.TryGetValue(projectile.EntityId, out var previous)
                && AuthoritativeProjectilePositionProjection.RequiresPresentationReset(previous, projectile)
                || snapshotRewound;
            UpdatePosition(
                projectile.EntityId,
                ToScreen(projectile.PositionX, projectile.PositionY),
                delta,
                projectile.LevelId,
                reset);
            displayedProjectileSnapshots[projectile.EntityId] = projectile.Clone();
        }
        foreach (var entityId in displayedPositions.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray()) {
            displayedPositions.Remove(entityId);
            displayedPositionLevels.Remove(entityId);
        }
        foreach (var entityId in displayedPlayerSnapshots.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            displayedPlayerSnapshots.Remove(entityId);
        foreach (var entityId in playerTextureBindings.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            playerTextureBindings.Remove(entityId);
        foreach (var entityId in playerTextureBindingHistory.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            playerTextureBindingHistory.Remove(entityId);
        foreach (var entityId in nativePlayerDrawObservations.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            nativePlayerDrawObservations.Remove(entityId);
        foreach (var entityId in nativePlayerDrawHistory.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            nativePlayerDrawHistory.Remove(entityId);
        foreach (var entityId in displayedMonsterSnapshots.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            displayedMonsterSnapshots.Remove(entityId);
        foreach (var entityId in displayedProjectileSnapshots.Keys.Where(id => !activeEntityIds.Contains(id)).ToArray())
            displayedProjectileSnapshots.Remove(entityId);
        QueueRedraw();
    }

    private AuthoritativeWorldViewport CreateViewportTransform(
        LevelLayout level,
        PlayerSnapshot? localPlayer)
    {
        var size = GetParent() is Control frame ? frame.Size : GetViewportRect().Size;
        return AuthoritativeWorldViewport.Create(
            Math.Max(1, Mathf.RoundToInt(size.X)),
            Math.Max(1, Mathf.RoundToInt(size.Y)),
            level.Width,
            level.Height,
            localPlayer?.PositionX ?? level.Width / 2d,
            localPlayer?.PositionY ?? level.Height / 2d,
            TileSize);
    }

    public Vector2I ScreenToTile(Vector2 screenPosition)
    {
        var local = ToLocal(screenPosition);
        var tile = viewport.ScreenToTile(local.X, local.Y);
        return new Vector2I(
            tile.X,
            tile.Y);
    }

    /** Read-only tile projection shared with scene acceptance. */
    internal Vector2 ScreenPositionForTile(int x, int y) => ToGlobal(ToScreen(x, y));

    public Vector2? ScreenPositionForEntity(uint entityId)
    {
        if (!displayedPositions.TryGetValue(entityId, out var position)
            || !displayedPositionLevels.TryGetValue(entityId, out var entityLevelId)
            || snapshot?.Players.FirstOrDefault() is not { } localPlayer
            || !AuthoritativeEntityPositionProjection.IsVisibleOnActiveLevel(
                entityLevelId,
                localPlayer.LevelId))
            return null;
        return ToGlobal(position);
    }

    public void SetTargetPreview(Vector2I? target)
    {
        targetPreview = target;
        QueueRedraw();
    }

    public uint? WorldItemEntityIdAt(Vector2I target)
    {
        var localPlayer = snapshot?.Players.FirstOrDefault();
        if (snapshot is null || localPlayer is null)
            return null;
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == localPlayer.LevelId);
        var lightRadius = localPlayer.LightRadius is >= 2 and <= 15 ? localPlayer.LightRadius : 10;
        return snapshot.WorldItems
            .Where(item => AuthoritativeWorldItemPresentationVisibility.IsVisible(
                    item,
                    localPlayer.LevelId,
                    geometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    lightRadius)
                && item.PositionX == target.X
                && item.PositionY == target.Y)
            .OrderBy(item => item.EntityId)
            .FirstOrDefault()?.EntityId;
    }

    /** Returns an authoritative portal occupying a visible clicked tile. */
    public uint? PortalEntityIdAt(Vector2I target)
    {
        var localPlayer = snapshot?.Players.FirstOrDefault();
        if (snapshot is null || localPlayer is null)
            return null;
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == localPlayer.LevelId);
        var lightRadius = localPlayer.LightRadius is >= 2 and <= 15 ? localPlayer.LightRadius : 10;
        foreach (var portal in snapshot.Portals.OrderBy(portal => portal.PortalId)) {
            if (AuthoritativePortalPresentation.Resolve(
                    portal,
                    localPlayer.LevelId,
                    geometry,
                    localPlayer.PositionX,
                    localPlayer.PositionY,
                    lightRadius) is { } endpoint
                && endpoint.X == target.X
                && endpoint.Y == target.Y)
                return portal.PortalId;
        }
        return null;
    }

    /** Returns an authoritative world object occupying a visible clicked tile. */
    public uint? ObjectEntityIdAt(Vector2I target)
    {
        return AuthoritativeObjectSelector.SelectAt(snapshot, target.X, target.Y)?.EntityId;
    }

    /** Returns the authoritative combat entity occupying a visible target tile. */
    public uint? CombatTargetEntityIdAt(Vector2I target)
    {
        var currentSnapshot = snapshot;
        var localPlayerEntityId = currentSnapshot?.Players.FirstOrDefault()?.EntityId ?? 0;
        return AuthoritativeTargetSelector.SelectCombatTarget(
            currentSnapshot,
            localPlayerEntityId,
            target.X,
            target.Y);
    }

    public override void _Draw()
    {
        doorDrawObservations.Clear();
        DrawGrid();
        if (snapshot is null || snapshot.Players.Count == 0)
            return;

        var localPlayer = snapshot.Players[0];
        var currentLevel = localPlayer.LevelId;
        var localGeometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == currentLevel);
        var localLightRadius = localPlayer.LightRadius is >= 2 and <= 15
            ? localPlayer.LightRadius
            : 10;
        var localStatusPresentation = StatusPresentationStyle.ResolvePlayer(localPlayer);
        var searchActive = localStatusPresentation.Contains(StatusPresentationKind.Search);
        DrawTargetPreview();
        foreach (var player in snapshot.Players.Where(player =>
                     player.EntityId == localPlayer.EntityId
                     || AuthoritativeNativeVisionVisibility.IsVisible(
                         player.LevelId,
                         player.PositionX,
                         player.PositionY,
                         currentLevel,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius))) {
            var isLocal = player.EntityId == localPlayer.EntityId;
            DrawPlayer(
                player,
                isLocal ? predictedPlayerPosition : null,
                isLocal,
                currentLevel,
                localGeometry,
                localPlayer.PositionX,
                localPlayer.PositionY,
                localLightRadius);
        }
        foreach (var monster in snapshot.Monsters.Where(monster =>
                     AuthoritativeMonsterPresentationVisibility.IsVisible(
                         monster,
                         currentLevel,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius)))
            DrawMonster(monster);
        foreach (var item in snapshot.WorldItems.Where(item =>
                     AuthoritativeWorldItemPresentationVisibility.IsVisible(
                         item,
                         currentLevel,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius)))
            DrawItem(item, searchActive);
        foreach (var @object in snapshot.Objects.Where(@object =>
                     AuthoritativeObjectPresentationVisibility.IsVisible(
                         @object,
                         currentLevel,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius)))
            DrawObject(@object);
        foreach (var corpse in snapshot.Corpses.Where(corpse =>
                     AuthoritativeCorpsePresentationVisibility.IsVisible(
                         corpse,
                         currentLevel,
                         localGeometry,
                         localPlayer.PositionX,
                         localPlayer.PositionY,
                         localLightRadius)))
            DrawCorpse(corpse);
        foreach (var portal in snapshot.Portals)
            DrawPortal(
                portal,
                currentLevel,
                localGeometry,
                localPlayer.PositionX,
                localPlayer.PositionY,
                localLightRadius);
        foreach (var projectile in snapshot.Projectiles.Where(projectile =>
                     AuthoritativeLevelVisibility.IsVisible(projectile.LevelId, currentLevel)))
            DrawProjectile(projectile);
    }

    private void DrawGrid()
    {
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color("090914"));
        if (assets.GetTextureFrame("floor") is { } floor) {
            for (var x = 0; x < currentLevel.Width; x++)
                for (var y = 0; y < currentLevel.Height; y++)
                    if (!currentLevel.IsBlocked(x, y))
                        DrawTextureFrame(floor, new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), Colors.White);
        }
        DrawNativeGeometryOverlay();
        DrawNativeObjectLayerOverlay();
        for (var x = 0; x <= currentLevel.Width; x++)
            DrawLine(new Vector2(OriginX + x * TileSize, OriginY), new Vector2(OriginX + x * TileSize, OriginY + currentLevel.Height * TileSize), new Color("17172b"));
        for (var y = 0; y <= currentLevel.Height; y++)
            DrawLine(new Vector2(OriginX, OriginY + y * TileSize), new Vector2(OriginX + currentLevel.Width * TileSize, OriginY + y * TileSize), new Color("17172b"));
        foreach (var cell in currentLevel.Blocked) {
            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            DrawRect(new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), new Color("202033"));
        }
        foreach (var cell in currentLevel.MissileBlocked) {
            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            var rectangle = new Rect2(
                OriginX + x * TileSize,
                OriginY + y * TileSize,
                TileSize,
                TileSize);
            var color = new Color("6a3d58", 0.45f);
            DrawRect(rectangle, color, true);
            DrawLine(rectangle.Position + new Vector2(6, 6), rectangle.End - new Vector2(6, 6), color, 2);
            DrawLine(
                new Vector2(rectangle.End.X - 6, rectangle.Position.Y + 6),
                new Vector2(rectangle.Position.X + 6, rectangle.End.Y - 6),
                color,
                2);
        }
        foreach (var trigger in levels.GetWarpTriggers(currentLevel.Id))
            DrawWarpTrigger(trigger);
    }

    /**
     * Shows authoritative native d-piece provenance until native tile art is
     * available to the Godot asset pipeline. Gameplay never depends on this
     * overlay; blocked and missile-blocked cells are already projected as
     * authoritative collision sets below.
     */
    private void DrawNativeGeometryOverlay()
    {
        var cellCount = checked(currentLevel.Width * currentLevel.Height);
        if (currentLevel.NativePieceLayer.Count != cellCount)
            return;

        for (var cell = 0; cell < cellCount; cell++) {
            var pieceId = currentLevel.NativePieceLayer[cell];
            var transparency = currentLevel.NativeTransparency.Count == cellCount
                ? currentLevel.NativeTransparency[cell]
                : 0u;
            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            var style = AuthoritativeNativeGeometryStyle.Resolve(
                pieceId,
                transparency,
                currentLevel.Blocked.Contains(cell),
                currentLevel.MissileBlocked.Contains(cell));
            if (!style.IsVisible)
                continue;

            DrawRect(
                new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize),
                new Color(
                    style.Red / 255f,
                    style.Green / 255f,
                    style.Blue / 255f,
                    style.Alpha / 255f),
                true);
        }
    }

    /** Shows native object-layer provenance without making IDs interactive. */
    private void DrawNativeObjectLayerOverlay()
    {
        var cellCount = checked(currentLevel.Width * currentLevel.Height);
        if (currentLevel.NativeObjectLayer.Count != cellCount)
            return;

        for (var cell = 0; cell < cellCount; cell++) {
            var style = AuthoritativeNativeObjectLayerStyle.Resolve(
                currentLevel.NativeObjectLayer[cell]);
            if (!style.IsVisible)
                continue;

            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            var rectangle = new Rect2(
                OriginX + x * TileSize + 4,
                OriginY + y * TileSize + 4,
                TileSize - 8,
                TileSize - 8);
            var color = new Color(
                style.Red / 255f,
                style.Green / 255f,
                style.Blue / 255f,
                style.Alpha / 255f);
            DrawRect(rectangle, color, false, 2);
        }
    }

    private void DrawWarpTrigger(WarpTriggerSnapshot trigger)
    {
        var center = ToScreen(trigger.TriggerX, trigger.TriggerY);
        var color = trigger.Available ? new Color("f3c969") : new Color("6f6680");
        DrawCircle(center, 8, new Color(color, 0.28f));
        DrawArc(center, 10, 0, Mathf.Tau, 4, color, 2);
        DrawLine(center + new Vector2(-4, 0), center + new Vector2(4, 0), color, 2);
        DrawLine(center + new Vector2(0, -4), center + new Vector2(0, 4), color, 2);
    }

    private void DrawTargetPreview()
    {
        if (targetPreview is not { } target
            || target.X < 0 || target.Y < 0
            || target.X >= currentLevel.Width || target.Y >= currentLevel.Height)
            return;

        var rectangle = new Rect2(OriginX + target.X * TileSize, OriginY + target.Y * TileSize, TileSize, TileSize);
        var color = new Color("f4d35e");
        DrawRect(rectangle, new Color(color, 0.16f), true);
        DrawRect(rectangle, color, false, 2);
        DrawLine(rectangle.Position + new Vector2(6, TileSize / 2), rectangle.Position + new Vector2(TileSize - 6, TileSize / 2), color, 2);
        DrawLine(rectangle.Position + new Vector2(TileSize / 2, 6), rectangle.Position + new Vector2(TileSize / 2, TileSize - 6), color, 2);
    }

    private void DrawPlayer(
        PlayerSnapshot player,
        ClientPosition? predicted,
        bool isLocal,
        uint currentLevel,
        LevelGeometrySnapshot? localGeometry,
        int localViewerX,
        int localViewerY,
        int localLightRadius)
    {
        var center = predicted is { } position
            ? ToScreen(position.X, position.Y)
            : PositionFor(player.EntityId, player.PositionX, player.PositionY);
        var statusPresentation = StatusPresentationStyle.ResolvePlayer(player);
        var statusKinds = statusPresentation.Badges
            .Select(badge => badge.Kind)
            .ToHashSet();
        var etherealized = statusKinds.Contains(StatusPresentationKind.Etherealize);
        var color = new Color(
            etherealized
                ? "dce3eb"
                : isLocal ? "4da6ff" : "6dd47e");
        var nativePackConfigured = assets.TryGetNativePlayerTextureFrame(
            player,
            nativePlayerPresentationTick ?? snapshot?.Tick ?? 0,
            out var nativeBinding);
        if (nativePackConfigured && nativeBinding.IdentityMatched && nativeBinding.Selection is not null) {
            DrawNativePlayer(player, center, color, nativeBinding);
        } else {
            var actorFrame = assets.GetPlayerTextureFrame(player, out var actorSelection, out var actorAssetId);
            var actorObservation = new PresentationPlayerTextureBindingObservation(
                player.EntityId,
                actorSelection is not null,
                actorSelection?.RuleId,
                actorAssetId,
                assets.ObserveTexture(actorAssetId));
            playerTextureBindings[player.EntityId] = actorObservation;
            if (!playerTextureBindingHistory.TryGetValue(player.EntityId, out var history))
                playerTextureBindingHistory[player.EntityId] = history = [];
            var last = history.LastOrDefault();
            if (last is null
                || last.RuleId != actorObservation.RuleId
                || last.AssetId != actorObservation.AssetId
                || last.Binding?.IsLoaded != actorObservation.Binding?.IsLoaded
                || last.Binding?.UsedFallback != actorObservation.Binding?.UsedFallback) {
                history.Add(actorObservation);
                if (history.Count > 32)
                    history.RemoveAt(0);
            }
            if (actorFrame is { } semanticFrame)
                DrawTextureFrame(semanticFrame, new Rect2(center - new Vector2(13, 13), new Vector2(26, 26)), color);
            else if (actorSelection is not null)
                // A declared semantic resource must remain a procedural fallback;
                // do not silently substitute the legacy/default player texture.
                DrawCircle(center, 11, color);
            else
                DrawTextureOrCircle("player", center, new Vector2(26, 26), color, 11);
            if (nativePackConfigured)
                RecordNativePlayerFallback(player, center, nativeBinding, actorObservation);
        }
        var resources = PlayerResourceBarProjection.Resolve(player);
        if (resources.LifeVisible || resources.ManaVisible) {
            var bar = new Rect2(center.X - 13, center.Y - 21, 26, 3);
            DrawRect(bar, new Color("24151b"), true);
            if (resources.LifeVisible) {
                DrawRect(
                    new Rect2(bar.Position + new Vector2(1, 1), new Vector2(24 * resources.LifeRatio, 1)),
                    new Color("e85d75"),
                    true);
            }
            if (resources.ManaVisible) {
                var manaBar = new Rect2(center.X - 13, center.Y - 17, 26, 3);
                DrawRect(manaBar, new Color("24151b"), true);
                DrawRect(
                    new Rect2(manaBar.Position + new Vector2(1, 1), new Vector2(24 * resources.ManaRatio, 1)),
                    new Color("5aa9e6"),
                    true);
            }
        }
        if (player.ManaShieldActive)
            DrawArc(center, 17, 0, Mathf.Tau, 32, new Color("6fffe9"), 3);
        if (player.ReflectCharges > 0)
            DrawArc(center, 20, 0, Mathf.Tau, 32, new Color("d4a5ff"), 2);
        if (statusKinds.Contains(StatusPresentationKind.Infravision)) {
            DrawArc(center, 21, 0, Mathf.Tau, 32, new Color("ef6f6c"), 2);
        }
        if (statusKinds.Contains(StatusPresentationKind.Search))
            DrawArc(center, 23, 0, Mathf.Tau, 32, new Color("fff0a8"), 2);
        if (etherealized)
            DrawArc(center, 25, 0, Mathf.Tau, 32, new Color("dce3eb", 0.8f), 2);
        if (!isLocal)
            DrawArc(center, 14, 0, Mathf.Tau, 24, color, 2);
        DrawActionIndicator(
            player,
            center,
            currentLevel,
            localGeometry,
            localViewerX,
            localViewerY,
            localLightRadius);
    }

    private void DrawNativePlayer(
        PlayerSnapshot player,
        Vector2 anchor,
        Color fallbackColor,
        NativePlayerTextureBinding binding)
    {
        playerTextureBindings.Remove(player.EntityId);
        playerTextureBindingHistory.Remove(player.EntityId);
        var selection = binding.Selection;
        var fallbackDestination = new NativePlayerDestinationRect(
            anchor.X - 11,
            anchor.Y - 11,
            22,
            22);
        NativePlayerDestinationRect destination = fallbackDestination;
        NativePlayerClip? clip = null;
        if (selection?.ClipKey is { } clipKey)
            binding.Catalog.TryGetClip(clipKey, out clip!);
        if (clip is not null) {
            destination = NativePlayerPresentation.CalculateDestinationRect(
                anchor.X,
                anchor.Y,
                clip.FrameWidth,
                clip.FrameHeight,
                anchorWidth: clip.EffectiveAnchorWidth);
        }

        if (binding.Frame is { } nativeFrame) {
            var rect = new Rect2(
                (float)destination.X,
                (float)destination.Y,
                (float)destination.Width,
                (float)destination.Height);
            DrawTextureFrame(nativeFrame, rect, Colors.White);
        } else {
            DrawCircle(anchor, 11, fallbackColor);
            destination = fallbackDestination;
        }

        var clipText = selection?.ClipKey is { } selectedKey
            ? NativePlayerCatalog.FormatKey(selectedKey)
            : null;
        RecordNativePlayerDrawing(new NativePlayerDrawObservation(
            player.EntityId,
            binding.Catalog.PackId,
            binding.ContentManifestHash ?? string.Empty,
            clipText,
            binding.ResourcePath,
            binding.IsLoaded,
            !binding.IsLoaded,
            selection?.DirectionIndex ?? -1,
            selection?.FrameIndex,
            selection?.ActionFrame,
            selection?.TimingSource ?? NativePlayerPresentationTimingSource.Unresolved,
            binding.Region?.X,
            binding.Region?.Y,
            binding.Region?.Width,
            binding.Region?.Height,
            destination.X,
            destination.Y,
            destination.Width,
            destination.Height,
            anchor.X,
            anchor.Y,
            binding.Diagnostic ?? selection?.Diagnostic,
            clip?.EffectiveAnchorWidth));
    }

    private void RecordNativePlayerFallback(
        PlayerSnapshot player,
        Vector2 anchor,
        NativePlayerTextureBinding binding,
        PresentationPlayerTextureBindingObservation actorObservation)
    {
        var frameRegion = actorObservation.Binding?.FrameRegion;
        var destinationSize = actorObservation.Binding?.IsLoaded == true ? 26 : 22;
        var destination = new NativePlayerDestinationRect(
            anchor.X - destinationSize / 2d,
            anchor.Y - destinationSize / 2d,
            destinationSize,
            destinationSize);
        var direction = Enum.IsDefined(player.FacingDirection)
            ? (int)player.FacingDirection
            : -1;
        RecordNativePlayerDrawing(new NativePlayerDrawObservation(
            player.EntityId,
            binding.Catalog.PackId,
            binding.ContentManifestHash ?? string.Empty,
            null,
            actorObservation.Binding?.ResourcePath,
            actorObservation.Binding?.IsLoaded == true,
            true,
            direction,
            null,
            null,
            NativePlayerPresentationTimingSource.Unresolved,
            frameRegion?.X,
            frameRegion?.Y,
            frameRegion?.Width,
            frameRegion?.Height,
            destination.X,
            destination.Y,
            destination.Width,
            destination.Height,
            anchor.X,
            anchor.Y,
            binding.Diagnostic ?? actorObservation.Binding?.Diagnostic));
    }

    private void RecordNativePlayerDrawing(NativePlayerDrawObservation observation)
    {
        nativePlayerDrawObservations[observation.EntityId] = observation;
        if (!nativePlayerDrawHistory.TryGetValue(observation.EntityId, out var history))
            nativePlayerDrawHistory[observation.EntityId] = history = [];
        if (history.LastOrDefault() == observation)
            return;
        history.Add(observation);
        if (history.Count > 128)
            history.RemoveAt(0);
    }

    /** Renders server-owned action timing without advancing or predicting it. */
    private void DrawActionIndicator(
        PlayerSnapshot player,
        Vector2 center,
        uint currentLevel,
        LevelGeometrySnapshot? localGeometry,
        int localViewerX,
        int localViewerY,
        int localLightRadius)
    {
        var action = player.CurrentAction;
        if (action is null || action.Kind == PlayerActionKind.Unspecified)
            return;

        var color = action.Kind switch {
            PlayerActionKind.Attack => new Color("ff9f43"),
            PlayerActionKind.Cast => new Color("b388ff"),
            PlayerActionKind.Move => new Color("6dd47e"),
            PlayerActionKind.Death => new Color("d95151"),
            _ => new Color("f4d35e"),
        };
        DrawArc(center, 24, 0, Mathf.Tau, 24, color, 2);

        if (action.TargetEntityId == 0 || snapshot is null)
            return;
        var targetMonster = snapshot.Monsters.FirstOrDefault(monster =>
            monster.EntityId == action.TargetEntityId
            && AuthoritativeMonsterPresentationVisibility.IsVisible(
                monster,
                currentLevel,
                localGeometry,
                localViewerX,
                localViewerY,
                localLightRadius));
        var targetPlayer = targetMonster is null
            ? snapshot.Players.FirstOrDefault(candidate =>
                candidate.EntityId == action.TargetEntityId
                && candidate.Life > 0
                && (candidate.EntityId == snapshot.Players[0].EntityId
                    || AuthoritativeNativeVisionVisibility.IsVisible(
                        candidate.LevelId,
                        candidate.PositionX,
                        candidate.PositionY,
                        currentLevel,
                        localGeometry,
                        localViewerX,
                        localViewerY,
                        localLightRadius)))
            : null;
        var targetLevel = targetMonster?.LevelId ?? targetPlayer?.LevelId;
        var targetX = targetMonster?.PositionX ?? targetPlayer?.PositionX;
        var targetY = targetMonster?.PositionY ?? targetPlayer?.PositionY;
        if (targetLevel is not { } levelId
            || targetX is not { } positionX
            || targetY is not { } positionY
            || !AuthoritativeLevelVisibility.IsVisible(levelId, currentLevel))
            return;
        DrawLine(
            center,
            PositionFor(action.TargetEntityId, positionX, positionY),
            new Color(color, 0.55f),
            2);
    }

    private void DrawMonster(MonsterSnapshot monster)
    {
        var center = PositionFor(monster.EntityId, monster.PositionX, monster.PositionY);
        var presentation = MonsterPresentationStyle.Resolve(monster);
        var color = monster.Petrified
            ? new Color("aeb7c4")
            : presentation == MonsterPresentationKind.Dialogue
                ? new Color("e5bd52")
                : presentation == MonsterPresentationKind.PlayerMinion
                    ? new Color("63d6a0")
                : new Color("d95151");
        DrawTextureOrRect("monster", center, new Vector2(24, 24), color);
        var health = MonsterHealthBarProjection.Resolve(monster);
        if (health.Visible) {
            var bar = new Rect2(center.X - 13, center.Y - 19, 26, 4);
            DrawRect(bar, new Color("24151b"), true);
            DrawRect(
                new Rect2(bar.Position + new Vector2(1, 1), new Vector2(24 * health.Ratio, 2)),
                new Color("e85d75"),
                true);
        }
        if (monster.Petrified)
            DrawArc(center, 15, 0, Mathf.Tau, 20, new Color("dce3eb"), 2);
        else if (presentation == MonsterPresentationKind.Dialogue)
            DrawArc(center, 16, 0, Mathf.Tau, 24, new Color("fff0a8"), 3);
        else if (presentation == MonsterPresentationKind.PlayerMinion)
            DrawArc(center, 16, 0, Mathf.Tau, 24, new Color("8ff0be"), 3);
    }

    private void DrawItem(WorldItemSnapshot item, bool searchActive)
    {
        var kind = ItemPresentationStyle.Resolve(item.State);
        var center = PositionFor(item.EntityId, item.PositionX, item.PositionY);
        var color = kind switch {
            ItemPresentationKind.Gold => new Color("e5bd52"),
            ItemPresentationKind.Ear => new Color("c48cff"),
            _ => new Color("8fb8d8"),
        };
        var size = kind == ItemPresentationKind.Ear
            ? new Vector2(20, 20)
            : new Vector2(18, 18);
        DrawTextureOrRect("item", center, size, color);
        if (searchActive)
            DrawArc(center, 14, 0, Mathf.Tau, 24, new Color("fff0a8"), 3);
    }

    private void DrawObject(ObjectSnapshot @object)
    {
        var kind = ObjectPresentationStyle.Resolve(@object);
        var frame = ObjectPresentationStyle.ResolveFrame(@object);
        if (kind == ObjectPresentationKind.CathedralDoor) {
            DrawCathedralDoor(@object, frame);
            return;
        }
        var color = kind switch {
            ObjectPresentationKind.Shrine => new Color("7d8cff"),
            ObjectPresentationKind.Quest => new Color("e5bd52"),
            ObjectPresentationKind.Breakable => new Color("c69a6b"),
            ObjectPresentationKind.Trap => new Color("f05d5e"),
            ObjectPresentationKind.NativeArmorStand => new Color("c9a77d"),
            ObjectPresentationKind.NativeWeaponRack => new Color("9d6b4f"),
            _ => @object.Activated ? new Color("4b4b62") : new Color("a68bca"),
        };
        var center = PositionFor(@object.EntityId, @object.PositionX, @object.PositionY);
        var size = kind switch {
            ObjectPresentationKind.NativeArmorStand => new Vector2(18, 24),
            ObjectPresentationKind.NativeWeaponRack => new Vector2(24, 18),
            _ => new Vector2(22, 22),
        };
        DrawTextureOrRect("object", center, size, color, true);
        if (kind == ObjectPresentationKind.NativeArmorStand)
            DrawArmorStandSilhouette(center, color);
        else if (kind == ObjectPresentationKind.NativeWeaponRack)
            DrawWeaponRackSilhouette(center, color);
        if (frame == ObjectPresentationFrame.Activated)
            DrawActivatedObjectFrame(center, color);
        if (@object.TrapArmed) {
            var trapColor = new Color("f05d5e");
            DrawArc(center, 15, 0, Mathf.Tau, 20, trapColor, 3);
            DrawLine(center + new Vector2(-6, -6), center + new Vector2(6, 6), trapColor, 2);
            DrawLine(center + new Vector2(6, -6), center + new Vector2(-6, 6), trapColor, 2);
        }
    }

    /** Snapshot-owned state; simple door cues until complete production art is available. */
    private void DrawCathedralDoor(ObjectSnapshot state, ObjectPresentationFrame frame)
    {
        var center = PositionFor(state.EntityId, state.PositionX, state.PositionY);
        var color = frame switch {
            ObjectPresentationFrame.DoorClosed => new Color("bf9156"),
            ObjectPresentationFrame.DoorBlocked => new Color("e4ad45"),
            _ => new Color("78cba2"),
        };
        DrawLine(center + new Vector2(-12, -10), center + new Vector2(-12, 10), color, 3);
        DrawLine(center + new Vector2(12, -10), center + new Vector2(12, 10), color, 3);
        var hinge = state.CathedralDoor.NativeObjectId == 1 ? -12 : 12;
        var endpoint = frame == ObjectPresentationFrame.DoorClosed
            ? new Vector2(-hinge, 0) : new Vector2(hinge / 2f, -12);
        DrawLine(center + new Vector2(hinge, 0), center + endpoint, color, 4);
        if (frame == ObjectPresentationFrame.DoorBlocked)
            DrawCircle(center + new Vector2(0, 10), 3, color);
        doorDrawObservations[state.EntityId] = new CathedralDoorDrawObservation(
            state.EntityId, state.CathedralDoor.NativeObjectId, state.CathedralDoor.DoorState,
            state.AnimationFrame, frame);
    }

    /** Adds a compact stand cue until production sprite assets are available. */
    private void DrawArmorStandSilhouette(Vector2 center, Color color)
    {
        DrawCircle(center + new Vector2(0, -7), 3, new Color("f4dfb4"));
        DrawLine(center + new Vector2(0, -4), center + new Vector2(0, 6), color, 2);
        DrawLine(center + new Vector2(-6, 9), center + new Vector2(6, 9), color, 2);
    }

    /** Adds a compact rack cue until production sprite assets are available. */
    private void DrawWeaponRackSilhouette(Vector2 center, Color color)
    {
        DrawLine(center + new Vector2(-8, -6), center + new Vector2(-8, 7), color, 2);
        DrawLine(center + new Vector2(8, -6), center + new Vector2(8, 7), color, 2);
        DrawLine(center + new Vector2(-8, 7), center + new Vector2(8, 7), color, 2);
        DrawLine(center + new Vector2(-5, -8), center + new Vector2(5, -8), new Color("e7c27d"), 2);
    }

    /** Draws a presentation-only open-book cue for an authored activated frame. */
    private void DrawActivatedObjectFrame(Vector2 center, Color color)
    {
        var pageColor = new Color("fff0a8");
        DrawLine(center + new Vector2(-8, -5), center + new Vector2(-1, -2), pageColor, 2);
        DrawLine(center + new Vector2(-8, 2), center + new Vector2(-1, 5), pageColor, 2);
        DrawLine(center + new Vector2(8, -5), center + new Vector2(1, -2), pageColor, 2);
        DrawLine(center + new Vector2(8, 2), center + new Vector2(1, 5), pageColor, 2);
        DrawLine(center + new Vector2(0, -5), center + new Vector2(0, 6), color, 2);
    }

    private void DrawCorpse(CorpseSnapshot corpse)
    {
        var center = PositionFor(corpse.EntityId, corpse.PositionX, corpse.PositionY);
        var color = new Color("8f7f72");
        DrawCircle(center, 9, new Color(color, 0.72f));
        DrawArc(center, 11, 0, Mathf.Tau, 20, color, 2);
        DrawLine(center + new Vector2(-6, -4), center + new Vector2(6, 4), color, 2);
        DrawLine(center + new Vector2(6, -4), center + new Vector2(-6, 4), color, 2);
    }

    private void DrawPortal(
        PortalSnapshot portal,
        uint currentLevel,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
    {
        if (AuthoritativePortalPresentation.Resolve(
                portal,
                currentLevel,
                geometry,
                viewerX,
                viewerY,
                viewerLightRadius) is not { } endpoint)
            return;
        var center = PositionFor(portal.PortalId, endpoint.X, endpoint.Y);
        DrawArc(center, 13, 0, Mathf.Tau, 24, new Color("bd7cff"), 3);
        DrawArc(center, 8, 0, Mathf.Tau, 20, new Color("f0c7ff"), 2);
    }

    private void DrawProjectile(ProjectileSnapshot projectile)
    {
        var kind = ProjectilePresentationStyle.Resolve(projectile);
        if (kind == ProjectilePresentationKind.Hidden)
            return;
        var beam = kind == ProjectilePresentationKind.Beam;
        var stoneCurse = kind == ProjectilePresentationKind.StoneCurse;
        var guardian = kind == ProjectilePresentationKind.Guardian;
        var flash = kind == ProjectilePresentationKind.Flash;
        var fireImpact = kind == ProjectilePresentationKind.FireImpact;
        var novaBall = kind == ProjectilePresentationKind.NovaBall;
        var movement = kind is ProjectilePresentationKind.Teleport
            or ProjectilePresentationKind.Phasing;
        var size = beam
            ? new Vector2(20, 28)
            : stoneCurse
                ? new Vector2(18, 18)
                : guardian
                    ? new Vector2(20, 20)
                : flash
                    ? new Vector2(24, 24)
                : fireImpact
                    ? new Vector2(30, 30)
                : novaBall
                    ? new Vector2(16, 16)
                    : movement ? new Vector2(20, 20) : new Vector2(12, 12);
        var color = kind switch {
            ProjectilePresentationKind.Fire
                or ProjectilePresentationKind.FireImpact => new Color("ff7b32"),
            ProjectilePresentationKind.Lightning => new Color("f4d35e"),
            ProjectilePresentationKind.NovaBall => new Color("b8f2ff"),
            ProjectilePresentationKind.Holy => new Color("f8d66d"),
            ProjectilePresentationKind.BloodStar => new Color("d86bff"),
            ProjectilePresentationKind.MonsterMagic => new Color("b78cff"),
            ProjectilePresentationKind.MonsterLightning => new Color("f4d35e"),
            ProjectilePresentationKind.MonsterOrangeFlare => new Color("ff8a3d"),
            ProjectilePresentationKind.MonsterBlueFlare => new Color("65b7ff"),
            ProjectilePresentationKind.MonsterRedFlare => new Color("ff5c6c"),
            ProjectilePresentationKind.MonsterYellowFlare => new Color("ffe066"),
            ProjectilePresentationKind.MonsterBlueFlare2 => new Color("4f7cff"),
            ProjectilePresentationKind.MonsterMagmaBall => new Color("d14b32"),
            ProjectilePresentationKind.HorkSpawn => new Color("ff9f5c"),
            ProjectilePresentationKind.BoneSpirit => new Color("dce3eb"),
            ProjectilePresentationKind.Acid => new Color("8be28b"),
            ProjectilePresentationKind.Arrow => new Color("d29b5f"),
            ProjectilePresentationKind.StoneCurse => new Color("dce3eb"),
            ProjectilePresentationKind.Teleport => new Color("bd7cff"),
            ProjectilePresentationKind.Phasing => new Color("6fffe9"),
            ProjectilePresentationKind.Guardian => new Color("ffb347"),
            ProjectilePresentationKind.Flash => new Color("8fd3ff"),
            _ => new Color("6fffe9"),
        };
        var radius = beam
            ? 8
            : stoneCurse
                ? 8
                : guardian
                    ? 9
                : flash
                    ? 11
                : fireImpact
                    ? 14
                : novaBall
                    ? 8
                    : movement
                        ? 9
                        : kind == ProjectilePresentationKind.Lightning ? 6 : 5;
        DrawTextureOrCircle(
            beam ? "resurrection_beam" : "projectile",
            PositionFor(projectile.EntityId, projectile.PositionX, projectile.PositionY),
            size,
            color,
            radius);
    }

    private void DrawTextureOrRect(string assetName, Vector2 center, Vector2 size, Color fallback, bool outline = false)
    {
        if (assets.GetTextureFrame(assetName) is { } frame) {
            DrawTextureFrame(frame, new Rect2(center - size / 2, size), fallback);
            return;
        }

        DrawRect(new Rect2(center - size / 2, size), fallback, !outline, outline ? 2 : -1);
    }

    private void DrawTextureOrCircle(string assetName, Vector2 center, Vector2 size, Color fallback, float radius)
    {
        if (assets.GetTextureFrame(assetName) is { } frame) {
            DrawTextureFrame(frame, new Rect2(center - size / 2, size), fallback);
            return;
        }

        DrawCircle(center, radius, fallback);
    }

    private void DrawTextureFrame(
        AssetPalette.TextureFrame frame,
        Rect2 destination,
        Color modulate)
    {
        if (frame.Region is { } region) {
            DrawTextureRectRegion(
                frame.Texture,
                destination,
                new Rect2(region.X, region.Y, region.Width, region.Height),
                modulate);
            return;
        }
        DrawTextureRect(frame.Texture, destination, false, modulate);
    }

    private Vector2 PositionFor(uint entityId, int x, int y)
    {
        return displayedPositions.TryGetValue(entityId, out var position) ? position : ToScreen(x, y);
    }

    private void UpdatePosition(
        uint entityId,
        Vector2 target,
        double delta,
        uint levelId,
        bool forceReset = false)
    {
        var levelChanged = displayedPositionLevels.TryGetValue(entityId, out var previousLevelId)
            && AuthoritativeEntityPositionProjection.RequiresLevelReset(previousLevelId, levelId);
        if (forceReset || levelChanged || !displayedPositions.TryGetValue(entityId, out var current))
            displayedPositions[entityId] = target;
        else
            displayedPositions[entityId] = current.Lerp(target, 1.0f - Mathf.Exp((float)(-delta * 16)));
        displayedPositionLevels[entityId] = levelId;
    }

    private Vector2 ToScreen(int x, int y)
    {
        var screen = viewport.WorldToScreen(x, y);
        return new Vector2((float)screen.X, (float)screen.Y);
    }

    private Vector2 ToScreen(double x, double y)
    {
        var screen = viewport.WorldToScreen(x, y);
        return new Vector2((float)screen.X, (float)screen.Y);
    }

}
