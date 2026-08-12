namespace Devilution.Diablo;

/** Module-owned authoritative world state and deterministic navigation rules. */
public sealed class DiabloWorldState
{
    private readonly int fallbackWidth;
    private readonly int fallbackHeight;
    private readonly HashSet<int> fallbackBlockedCells;
    private readonly HashSet<int> fallbackMissileBlockedCells;
    private ulong? lastPortalTick;

    public DiabloWorldState(
        AuthoritativeWorld? world = null,
        int fallbackWidth = 40,
        int fallbackHeight = 40,
        IEnumerable<int>? fallbackBlockedCells = null,
        IReadOnlyList<AuthoritativeCombatTarget>? combatTargets = null,
        IReadOnlyList<AuthoritativePortal>? portals = null,
        IReadOnlyList<AuthoritativeWorldItem>? worldItems = null,
        IReadOnlyList<AuthoritativeWorldObject>? objects = null,
        IReadOnlyList<AuthoritativeQuestState>? quests = null,
        IReadOnlyList<AuthoritativeProjectile>? projectiles = null,
        IEnumerable<int>? fallbackMissileBlockedCells = null)
    {
        World = world;
        this.fallbackWidth = fallbackWidth > 0
            ? fallbackWidth
            : throw new ArgumentOutOfRangeException(nameof(fallbackWidth));
        this.fallbackHeight = fallbackHeight > 0
            ? fallbackHeight
            : throw new ArgumentOutOfRangeException(nameof(fallbackHeight));
        this.fallbackBlockedCells = (fallbackBlockedCells ?? []).ToHashSet();
        if (this.fallbackBlockedCells.Any(
                cell => cell < 0 || cell >= checked(fallbackWidth * fallbackHeight))) {
            throw new InvalidDataException(
                "Starting blocked cells must be inside the configured world bounds.");
        }
        this.fallbackMissileBlockedCells = (fallbackMissileBlockedCells
            ?? this.fallbackBlockedCells).ToHashSet();
        if (this.fallbackMissileBlockedCells.Any(
                cell => cell < 0 || cell >= checked(fallbackWidth * fallbackHeight))) {
            throw new InvalidDataException(
                "Starting missile-blocked cells must be inside the configured world bounds.");
        }

        foreach (var target in combatTargets ?? [])
            CombatTargets.Add(target.EntityId, target);
        foreach (var portal in portals ?? [])
            Portals.Add(portal.PortalId, portal);
        foreach (var item in worldItems ?? [])
            WorldItems.Add(item.EntityId, item);
        foreach (var @object in objects ?? []) {
            if (!Objects.TryAdd(@object.EntityId, @object)) {
                throw new InvalidDataException(
                    $"Object entity {@object.EntityId} is registered more than once.");
            }
        }
        foreach (var quest in quests ?? []) {
            if (!Quests.TryAdd(quest.QuestId, quest))
                throw new InvalidDataException($"Quest {quest.QuestId} is registered more than once.");
        }
        foreach (var projectile in projectiles ?? [])
            Projectiles.Add(projectile.EntityId, projectile);
    }

    public AuthoritativeWorld? World { get; }

    public Dictionary<uint, AuthoritativeCombatTarget> CombatTargets { get; } = [];

    public Dictionary<uint, AuthoritativePortal> Portals { get; } = [];

    /** Expires dynamic portals using elapsed authoritative simulation ticks. */
    public void AdvancePortals(ulong tick)
    {
        if (lastPortalTick is not ulong previousTick) {
            lastPortalTick = tick;
            return;
        }
        if (tick <= previousTick)
            return;

        var elapsed = checked(tick - previousTick);
        foreach (var portal in Portals.Values.ToArray()) {
            if (portal.RemainingTicks == 0)
                continue;
            portal.RemainingTicks = elapsed >= portal.RemainingTicks
                ? 0
                : portal.RemainingTicks - (uint)elapsed;
            if (portal.RemainingTicks == 0)
                Portals.Remove(portal.PortalId);
        }
        lastPortalTick = tick;
    }

    /** Restores the authoritative clock used for dynamic portal expiry. */
    public void RestorePortalClock(ulong tick)
    {
        lastPortalTick = tick;
    }

    public Dictionary<uint, AuthoritativeWorldItem> WorldItems { get; } = [];

    public Dictionary<uint, AuthoritativeWorldObject> Objects { get; } = [];

    public Dictionary<uint, AuthoritativeQuestState> Quests { get; } = [];

    public Dictionary<uint, AuthoritativeProjectile> Projectiles { get; } = [];

    public bool IsWalkable(uint levelId, int positionX, int positionY)
    {
        if (World is not null)
            return World.IsWalkable(levelId, positionX, positionY);
        return positionX >= 0 && positionX < fallbackWidth
            && positionY >= 0 && positionY < fallbackHeight
            && !fallbackBlockedCells.Contains(positionY * fallbackWidth + positionX);
    }

    public bool ContainsPosition(uint levelId, int positionX, int positionY)
    {
        if (World is not null)
            return World.ContainsPosition(levelId, positionX, positionY);
        return positionX >= 0 && positionX < fallbackWidth
            && positionY >= 0 && positionY < fallbackHeight;
    }

    public bool SuppressMultiplayerDeathDrops(uint levelId)
    {
        return World?.SuppressMultiplayerDeathDrops(levelId) ?? false;
    }

    public bool IsMissileBlocked(uint levelId, int positionX, int positionY)
    {
        if (IsMissileGeometryBlocked(levelId, positionX, positionY))
            return true;
        return TryGetMissileBlockingObject(
            levelId,
            positionX,
            positionY,
            out _);
    }

    public bool IsMissileGeometryBlocked(uint levelId, int positionX, int positionY)
    {
        if (!ContainsPosition(levelId, positionX, positionY))
            return true;
        if (World is not null)
            return World.IsMissileBlocked(levelId, positionX, positionY);
        return fallbackMissileBlockedCells.Contains(
            positionY * fallbackWidth + positionX);
    }

    public bool TryGetMissileBlockingObject(
        uint levelId,
        int positionX,
        int positionY,
        out AuthoritativeWorldObject? result)
    {
        result = Objects.Values
            .Where(@object => !@object.Activated
                && !@object.Destroyed
                && !@object.MissilePassable
                && @object.LevelId == levelId
                && @object.PositionX == positionX
                && @object.PositionY == positionY)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
        return result is not null;
    }

    public bool HasLineOfSight(
        uint levelId,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        if (World is not null) {
            var level = World.Levels.SingleOrDefault(candidate => candidate.LevelId == levelId);
            return level?.HasLineOfSight(startX, startY, endX, endY) ?? false;
        }

        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;
        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY)
                && !IsWalkable(levelId, x, y))
                return false;
            if (x == endX && y == endY)
                return true;
            var doubledError = 2 * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }

    public bool HasMissileLineOfSight(
        uint levelId,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        if (!ContainsPosition(levelId, startX, startY)
            || !ContainsPosition(levelId, endX, endY)) {
            return false;
        }

        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;
        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY)
                && IsMissileGeometryBlocked(levelId, x, y)) {
                return false;
            }
            if (x == endX && y == endY)
                return true;
            var doubledError = 2 * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }

    public bool IsOccupied(
        uint levelId,
        int positionX,
        int positionY,
        IEnumerable<AuthoritativePlayerState> players,
        uint excludedEntityId = 0)
    {
        ArgumentNullException.ThrowIfNull(players);
        return CombatTargets.Values.Any(target => target.HitPoints > 0
            && target.EntityId != excludedEntityId
            && (target.LevelId == 0 || target.LevelId == levelId)
            && ((target.PositionX == positionX
                    && target.PositionY == positionY)
                || (target.IsMoving
                    && target.FuturePositionX == positionX
                    && target.FuturePositionY == positionY)))
            || players.Any(player => player.EntityId != 0
                && player.EntityId != excludedEntityId
                && player.LevelId == levelId
                && ((player.PositionX == positionX
                        && player.PositionY == positionY)
                    || (player.IsMoving
                        && player.FuturePositionX == positionX
                        && player.FuturePositionY == positionY)))
            || Objects.Values.Any(@object => !@object.Activated
                && !@object.Destroyed
                && @object.EntityId != excludedEntityId
                && @object.LevelId == levelId
                && @object.PositionX == positionX
                && @object.PositionY == positionY);
    }

    public void ValidateInitialPlayerPosition(uint levelId, int positionX, int positionY)
    {
        if (!IsWalkable(levelId, positionX, positionY)) {
            throw new InvalidDataException(
                "The starting player position is not walkable in the authoritative world.");
        }

        var entityIds = new HashSet<uint>();
        foreach (var target in CombatTargets.Values) {
            ValidatePosition(target.LevelId, target.PositionX, target.PositionY, "monster");
            if (target.IsMoving) {
                ValidatePosition(
                    target.LevelId,
                    target.FuturePositionX,
                    target.FuturePositionY,
                    "moving monster destination");
            }
            if (!entityIds.Add(target.EntityId))
                throw new InvalidDataException($"Entity {target.EntityId} is registered more than once.");
            if (target.Drop is not null && !entityIds.Add(target.Drop.EntityId)) {
                throw new InvalidDataException(
                    $"Entity {target.Drop.EntityId} is registered more than once.");
            }
        }
        foreach (var item in WorldItems.Values) {
            ValidatePosition(item.LevelId, item.PositionX, item.PositionY, "world item");
            if (!entityIds.Add(item.EntityId))
                throw new InvalidDataException($"Entity {item.EntityId} is registered more than once.");
        }
        foreach (var @object in Objects.Values) {
            ValidatePosition(@object.LevelId, @object.PositionX, @object.PositionY, "object");
            if (!entityIds.Add(@object.EntityId)) {
                throw new InvalidDataException(
                    $"Entity {@object.EntityId} is registered more than once.");
            }
        }
        foreach (var portal in Portals.Values) {
            ValidatePosition(
                portal.SourceLevelId,
                portal.SourcePositionX,
                portal.SourcePositionY,
                "portal source");
            ValidatePosition(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY,
                "portal destination");
        }

        ValidateOccupancy(
            levelId,
            positionX,
            positionY,
            CombatTargets.Values
                .Where(target => target.HitPoints > 0)
                .SelectMany(target => target.IsMoving
                    ? new[] {
                        (target.LevelId, target.PositionX, target.PositionY),
                        (target.LevelId, target.FuturePositionX, target.FuturePositionY),
                    }
                    : [(target.LevelId, target.PositionX, target.PositionY)]),
            WorldItems.Values.Select(item => (item.LevelId, item.PositionX, item.PositionY)),
            Objects.Values
                .Where(@object => !@object.Activated && !@object.Destroyed)
                .Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));
    }

    private void ValidatePosition(uint levelId, int positionX, int positionY, string entityKind)
    {
        if (!IsWalkable(levelId, positionX, positionY)) {
            throw new InvalidDataException(
                $"The starting {entityKind} position is not walkable in the authoritative world.");
        }
    }

    /** Validates that a serialized world does not contain overlapping live entities. */
    public static void ValidateOccupancy(
        uint sourceLevelId,
        int sourcePositionX,
        int sourcePositionY,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> monsters,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> items,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> objects)
    {
        var occupied = new HashSet<(uint LevelId, int PositionX, int PositionY)> {
            (sourceLevelId, sourcePositionX, sourcePositionY),
        };
        foreach (var monster in monsters) {
            if (!occupied.Add(monster))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
        foreach (var item in items) {
            if (!occupied.Add(item))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
        foreach (var @object in objects) {
            if (!occupied.Add(@object))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
    }
}
