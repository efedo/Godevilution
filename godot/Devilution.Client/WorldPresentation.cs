using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Asset-backed world view driven exclusively by authoritative snapshots. */
public partial class WorldPresentation : Node2D
{
    private const int TeleportProjectileBehavior = 9;
    private const int ResurrectionBeamProjectileBehavior = 11;
    private const int StoneCurseProjectileBehavior = 13;
    private const int PhasingProjectileBehavior = 15;
    private const int FireballProjectileBehavior = 16;
    private const int ProjectileImpactPhase = 1;
    private const float TileSize = 32;
    private const float OriginX = 96;
    private const float OriginY = 96;
    private readonly Dictionary<uint, Vector2> displayedPositions = [];
    private readonly AssetPalette assets = new();
    private readonly LevelLayoutCatalog levels = new();
    private Snapshot? snapshot;
    private ClientPosition? predictedPlayerPosition;
    private Vector2I? targetPreview;
    private LevelLayout currentLevel = LevelLayout.Empty;

    public void Apply(Snapshot? next, double delta, ClientPosition? predicted = null)
    {
        snapshot = next;
        predictedPlayerPosition = predicted;
        if (snapshot is null)
            return;

        var localPlayer = snapshot.Players.FirstOrDefault();
        currentLevel = levels.Resolve(localPlayer?.LevelId ?? 0);

        foreach (var player in snapshot.Players) {
            var projected = player.EntityId == localPlayer?.EntityId ? predicted : null;
            UpdatePosition(
                player.EntityId,
                ToScreen(projected?.X ?? player.PositionX, projected?.Y ?? player.PositionY),
                delta);
        }
        foreach (var monster in snapshot.Monsters)
            UpdatePosition(monster.EntityId, ToScreen(monster.PositionX, monster.PositionY), delta);
        foreach (var item in snapshot.WorldItems)
            UpdatePosition(item.EntityId, ToScreen(item.PositionX, item.PositionY), delta);
        foreach (var @object in snapshot.Objects)
            UpdatePosition(@object.EntityId, ToScreen(@object.PositionX, @object.PositionY), delta);
        foreach (var portal in snapshot.Portals) {
            if (localPlayer is not null && portal.SourceLevelId == localPlayer.LevelId)
                UpdatePosition(portal.PortalId, ToScreen(portal.SourcePositionX, portal.SourcePositionY), delta);
            else if (localPlayer is not null && portal.Bidirectional && portal.DestinationLevelId == localPlayer.LevelId)
                UpdatePosition(portal.PortalId, ToScreen(portal.DestinationPositionX, portal.DestinationPositionY), delta);
        }
        foreach (var projectile in snapshot.Projectiles.Where(
                     projectile => projectile.Behavior != TeleportProjectileBehavior
                         && projectile.Behavior != PhasingProjectileBehavior))
            UpdatePosition(projectile.EntityId, ToScreen(projectile.PositionX, projectile.PositionY), delta);
        QueueRedraw();
    }

    public Vector2I ScreenToTile(Vector2 screenPosition)
    {
        return new Vector2I(
            Mathf.FloorToInt((screenPosition.X - OriginX) / TileSize),
            Mathf.FloorToInt((screenPosition.Y - OriginY) / TileSize));
    }

    public void SetTargetPreview(Vector2I? target)
    {
        targetPreview = target;
        QueueRedraw();
    }

    public uint? WorldItemEntityIdAt(Vector2I target)
    {
        var currentLevelId = snapshot?.Players.FirstOrDefault()?.LevelId;
        return snapshot?.WorldItems.FirstOrDefault(item =>
            currentLevelId.HasValue
            && IsVisible(item.LevelId, currentLevelId.Value)
            && item.PositionX == target.X
            && item.PositionY == target.Y)?.EntityId;
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
        DrawGrid();
        if (snapshot is null || snapshot.Players.Count == 0)
            return;

        var localPlayer = snapshot.Players[0];
        var currentLevel = localPlayer.LevelId;
        var searchActive = localPlayer.StatusEffects.Any(effect =>
            effect.Behavior == StatusEffectBehavior.Search);
        DrawTargetPreview();
        foreach (var player in snapshot.Players.Where(player => IsVisible(player.LevelId, currentLevel))) {
            var isLocal = player.EntityId == localPlayer.EntityId;
            DrawPlayer(player, isLocal ? predictedPlayerPosition : null, isLocal);
        }
        foreach (var monster in snapshot.Monsters.Where(monster => IsVisible(monster.LevelId, currentLevel) && monster.Alive))
            DrawMonster(monster);
        foreach (var item in snapshot.WorldItems.Where(item => IsVisible(item.LevelId, currentLevel)))
            DrawItem(item, searchActive);
        foreach (var @object in snapshot.Objects.Where(@object => IsVisible(@object.LevelId, currentLevel)))
            DrawObject(@object);
        foreach (var portal in snapshot.Portals)
            DrawPortal(portal, currentLevel);
        foreach (var projectile in snapshot.Projectiles.Where(projectile =>
                     projectile.Behavior != TeleportProjectileBehavior
                     && projectile.Behavior != PhasingProjectileBehavior
                     && IsVisible(projectile.LevelId, currentLevel)))
            DrawProjectile(projectile);
    }

    private void DrawGrid()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("090914"));
        if (assets.Get("floor") is { } floor) {
            for (var x = 0; x < currentLevel.Width; x++)
                for (var y = 0; y < currentLevel.Height; y++)
                    if (!currentLevel.IsBlocked(x, y))
                        DrawTextureRect(floor, new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), false);
        }
        for (var x = 0; x <= currentLevel.Width; x++)
            DrawLine(new Vector2(OriginX + x * TileSize, OriginY), new Vector2(OriginX + x * TileSize, OriginY + currentLevel.Height * TileSize), new Color("17172b"));
        for (var y = 0; y <= currentLevel.Height; y++)
            DrawLine(new Vector2(OriginX, OriginY + y * TileSize), new Vector2(OriginX + currentLevel.Width * TileSize, OriginY + y * TileSize), new Color("17172b"));
        foreach (var cell in currentLevel.Blocked) {
            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            DrawRect(new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), new Color("202033"));
        }
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

    private void DrawPlayer(PlayerSnapshot player, ClientPosition? predicted, bool isLocal)
    {
        var center = predicted is { } position
            ? ToScreen(position.X, position.Y)
            : PositionFor(player.EntityId, player.PositionX, player.PositionY);
        var color = new Color(isLocal ? "4da6ff" : "6dd47e");
        DrawTextureOrCircle("player", center, new Vector2(26, 26), color, 11);
        if (player.ManaShieldActive)
            DrawArc(center, 17, 0, Mathf.Tau, 32, new Color("6fffe9"), 3);
        if (player.ReflectCharges > 0)
            DrawArc(center, 20, 0, Mathf.Tau, 32, new Color("d4a5ff"), 2);
        if (isLocal && player.StatusEffects.Any(effect =>
                effect.Behavior == StatusEffectBehavior.Infravision)) {
            DrawArc(center, 21, 0, Mathf.Tau, 32, new Color("ef6f6c"), 2);
        }
        if (!isLocal)
            DrawArc(center, 14, 0, Mathf.Tau, 24, color, 2);
    }

    private void DrawMonster(MonsterSnapshot monster)
    {
        var center = PositionFor(monster.EntityId, monster.PositionX, monster.PositionY);
        var color = monster.Petrified ? new Color("aeb7c4") : new Color("d95151");
        DrawTextureOrRect("monster", center, new Vector2(24, 24), color);
        if (monster.Petrified)
            DrawArc(center, 15, 0, Mathf.Tau, 20, new Color("dce3eb"), 2);
    }

    private void DrawItem(WorldItemSnapshot item, bool searchActive)
    {
        var center = PositionFor(item.EntityId, item.PositionX, item.PositionY);
        DrawTextureOrRect("item", center, new Vector2(18, 18), new Color("e5bd52"));
        if (searchActive)
            DrawArc(center, 14, 0, Mathf.Tau, 24, new Color("fff0a8"), 3);
    }

    private void DrawObject(ObjectSnapshot @object)
    {
        var color = @object.Activated ? new Color("4b4b62") : new Color("a68bca");
        var center = PositionFor(@object.EntityId, @object.PositionX, @object.PositionY);
        DrawTextureOrRect("object", center, new Vector2(22, 22), color, true);
        if (@object.TrapArmed) {
            var trapColor = new Color("f05d5e");
            DrawArc(center, 15, 0, Mathf.Tau, 20, trapColor, 3);
            DrawLine(center + new Vector2(-6, -6), center + new Vector2(6, 6), trapColor, 2);
            DrawLine(center + new Vector2(6, -6), center + new Vector2(-6, 6), trapColor, 2);
        }
    }

    private void DrawPortal(PortalSnapshot portal, uint currentLevel)
    {
        var isSource = portal.SourceLevelId == currentLevel;
        var isDestination = portal.Bidirectional && portal.DestinationLevelId == currentLevel;
        if (!isSource && !isDestination)
            return;
        var x = isSource ? portal.SourcePositionX : portal.DestinationPositionX;
        var y = isSource ? portal.SourcePositionY : portal.DestinationPositionY;
        var center = PositionFor(portal.PortalId, x, y);
        DrawArc(center, 13, 0, Mathf.Tau, 24, new Color("bd7cff"), 3);
        DrawArc(center, 8, 0, Mathf.Tau, 20, new Color("f0c7ff"), 2);
    }

    private void DrawProjectile(ProjectileSnapshot projectile)
    {
        var resurrectionBeam = projectile.Behavior == ResurrectionBeamProjectileBehavior;
        var stoneCurse = projectile.Behavior == StoneCurseProjectileBehavior;
        var fireball = projectile.Behavior == FireballProjectileBehavior;
        var fireballImpact = fireball && projectile.Phase == ProjectileImpactPhase;
        DrawTextureOrCircle(
            resurrectionBeam ? "resurrection_beam" : "projectile",
            PositionFor(projectile.EntityId, projectile.PositionX, projectile.PositionY),
            resurrectionBeam
                ? new Vector2(20, 28)
                : stoneCurse
                    ? new Vector2(18, 18)
                    : fireballImpact ? new Vector2(30, 30) : new Vector2(12, 12),
            resurrectionBeam
                ? new Color("f8d66d")
                : stoneCurse
                    ? new Color("dce3eb")
                    : fireball ? new Color("ff7b32") : new Color("6fffe9"),
            resurrectionBeam ? 8 : stoneCurse ? 8 : fireballImpact ? 14 : fireball ? 7 : 5);
    }

    private void DrawTextureOrRect(string assetName, Vector2 center, Vector2 size, Color fallback, bool outline = false)
    {
        if (assets.Get(assetName) is { } texture) {
            DrawTextureRect(texture, new Rect2(center - size / 2, size), false, fallback);
            return;
        }

        DrawRect(new Rect2(center - size / 2, size), fallback, !outline, outline ? 2 : -1);
    }

    private void DrawTextureOrCircle(string assetName, Vector2 center, Vector2 size, Color fallback, float radius)
    {
        if (assets.Get(assetName) is { } texture) {
            DrawTextureRect(texture, new Rect2(center - size / 2, size), false, fallback);
            return;
        }

        DrawCircle(center, radius, fallback);
    }

    private Vector2 PositionFor(uint entityId, int x, int y)
    {
        return displayedPositions.TryGetValue(entityId, out var position) ? position : ToScreen(x, y);
    }

    private void UpdatePosition(uint entityId, Vector2 target, double delta)
    {
        if (!displayedPositions.TryGetValue(entityId, out var current))
            displayedPositions[entityId] = target;
        else
            displayedPositions[entityId] = current.Lerp(target, 1.0f - Mathf.Exp((float)(-delta * 16)));
    }

    private static Vector2 ToScreen(int x, int y) => new(OriginX + (x + 0.5f) * TileSize, OriginY + (y + 0.5f) * TileSize);

    private static bool IsVisible(uint levelId, uint currentLevel) => levelId == 0 || levelId == currentLevel;
}
