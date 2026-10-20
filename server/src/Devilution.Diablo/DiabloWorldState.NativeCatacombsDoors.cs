namespace Devilution.Diablo;

/** Immutable door-owned state captured independently from the legacy C1 checkpoint. */
internal sealed record AuthoritativeNativeCatacombsDoorFloorState(
    uint LevelId,
    int Depth,
    string OwnerFingerprint,
    string CatalogFingerprint,
    AuthoritativeLevel BackingGeometry,
    AuthoritativeLevel EffectiveGeometry,
    IReadOnlyList<AuthoritativeCatacombsDoorState> Doors);

/** Live C2 doors are owned by raw generated object slots, never by tile scanning. */
public sealed partial class DiabloWorldState
{
    private sealed record NativeCatacombsDoorEntry(
        uint LevelId,
        int Depth,
        AuthoritativeCatacombsDoorState State);

    private readonly Dictionary<uint, AuthoritativeNativePopulationBindings> nativeCatacombsDoorOwners = [];
    private readonly Dictionary<uint, NativeCatacombsDoorEntry> nativeCatacombsDoorsByEntityId = [];
    private readonly Dictionary<uint, AuthoritativeLevel> nativeCatacombsDoorLevels = [];
    private string? nativeCatacombsDoorCatalogFingerprint;

    internal bool HasNativeCatacombsDoors => nativeCatacombsDoorCatalogFingerprint is not null;

    /** Binds all L2 doors from the owner's ordered raw object pool before publishing any state. */
    internal void ConfigureNativeCatacombsDoors(
        AuthoritativeNativePopulationBindings owner,
        AuthoritativeCatacombsDoorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(catalog);
        if (owner.Family != AuthoritativeNativePopulationFamily.Catacombs
            || owner.Depth is < 5 or > 8
            || owner.LevelId == 0
            || owner.CatacombsDoorCatalogFingerprint != catalog.Fingerprint
            || World is null
            || !ReferenceEquals(NativePopulationForLevel(owner.LevelId), owner)
            || nativeCatacombsDoorOwners.ContainsKey(owner.LevelId)
            || nativeCatacombsDoorCatalogFingerprint is not null
                && !string.Equals(nativeCatacombsDoorCatalogFingerprint, catalog.Fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Native Catacombs doors require one explicit owner and stable catalog per level.");

        _ = catalog.ForDepth(checked((uint)owner.Depth));
        if (!World.ContainsLevel(owner.LevelId)
            || !projectedLevels.TryGetValue(owner.LevelId, out var currentLevel))
            throw new InvalidDataException("Native Catacombs doors require their generated backing and projected level.");

        var backingLevel = World.Levels.Single(level => level.LevelId == owner.LevelId);
        owner.ValidateBackingLevel(backingLevel);
        owner.ValidateBackingLevel(currentLevel);

        var activeRows = owner.InitialState.ActiveObjectIds
            .Take(owner.InitialState.ActiveObjectCount)
            .Select(slot => owner.InitialState.ObjectSlots[slot])
            .ToArray();
        var prepared = new Dictionary<uint, AuthoritativeCatacombsDoorState>();
        var positions = new HashSet<(int X, int Y)>();
        var seenDoorRows = 0;
        foreach (var raw in activeRows) {
            var isDoorType = raw.Type == (int)AuthoritativeCatacombsDoorCatalog.LeftNativeObjectId
                || raw.Type == (int)AuthoritativeCatacombsDoorCatalog.RightNativeObjectId;
            if (!isDoorType && !raw.DoorFlag)
                continue;

            if (!raw.DoorFlag || !isDoorType
                || !positions.Add((raw.Position.X, raw.Position.Y)))
                throw new InvalidDataException("A generated Catacombs door row is not a unique raw type 42/43 object.");

            var entityId = owner.ObjectEntityAt(raw.Position.X, raw.Position.Y)
                ?? throw new InvalidDataException("A generated Catacombs door has no retained object entity at its raw position.");
            if (!Objects.TryGetValue(entityId, out var value))
                throw new InvalidDataException("A generated Catacombs door entity is missing from the world object registry.");

            var state = AuthoritativeCatacombsDoors.BindRetained(
                currentLevel, checked((uint)owner.Depth), catalog, owner.InitialState, raw, value);
            if (state.LevelId != owner.LevelId
                || value.LevelId != owner.LevelId
                || value.EntityId != entityId
                || !prepared.TryAdd(entityId, state))
                throw new InvalidDataException("A generated Catacombs door is duplicated or bound to another level.");
            seenDoorRows++;
        }

        var projectedDoors = Objects.Values.Count(value => value.LevelId == owner.LevelId
            && (value.NativeObjectId == AuthoritativeCatacombsDoorCatalog.LeftNativeObjectId
                || value.NativeObjectId == AuthoritativeCatacombsDoorCatalog.RightNativeObjectId));
        if (seenDoorRows != owner.InitialDoorCount
            || projectedDoors != prepared.Count
            || prepared.Keys.Any(nativeCatacombsDoorsByEntityId.ContainsKey))
            throw new InvalidDataException("Catacombs door rows and projected entities do not have exact coverage.");

        // Publish only after every raw row, entity and geometry binding has passed.
        nativeCatacombsDoorCatalogFingerprint ??= catalog.Fingerprint;
        nativeCatacombsDoorOwners.Add(owner.LevelId, owner);
        nativeCatacombsDoorLevels.Add(owner.LevelId, currentLevel);
        foreach (var (entityId, state) in prepared)
            nativeCatacombsDoorsByEntityId.Add(entityId,
                new(owner.LevelId, owner.Depth, state));
        nativeCatacombsDoorCatalog ??= catalog;
    }

    /** Applies one native OperateL2LDoor/OperateL2RDoor transition to its bound entity. */
    internal bool TryOperateNativeCatacombsDoor(
        uint entityId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (!nativeCatacombsDoorsByEntityId.TryGetValue(entityId, out var entry))
            return false;
        var catalog = RequireNativeCatacombsDoorCatalog();
        ValidateNativeCatacombsDoor(entry);
        if (Objects[entityId].TrapArmed)
            return false;
        var transition = AuthoritativeCatacombsDoors.Operate(
            nativeCatacombsDoorLevels[entry.LevelId], checked((uint)entry.Depth), catalog,
            entry.State, IsNativeCatacombsDoorOccupied(entry.LevelId, entry.State, participants));
        nativeCatacombsDoorsByEntityId[entityId] = entry with { State = transition.Door };
        nativeCatacombsDoorLevels[entry.LevelId] = transition.Level;
        projectedLevels[entry.LevelId] = transition.Level;
        ApplyNativeCatacombsDoorFields(Objects[entityId], transition.Door);
        return true;
    }

    /** Refreshes open/blocked occupancy without changing animation frames or geometry. */
    internal void UpdateNativeCatacombsDoorOccupancy(IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var activeLevels = participants.Where(player => player.EntityId != 0)
            .Select(player => player.LevelId).ToHashSet();
        foreach (var (entityId, entry) in nativeCatacombsDoorsByEntityId.ToArray()) {
            if (!activeLevels.Contains(entry.LevelId))
                continue;
            ValidateNativeCatacombsDoor(entry);
            var changed = AuthoritativeCatacombsDoors.UpdateOccupancy(entry.State,
                IsNativeCatacombsDoorOccupied(entry.LevelId, entry.State, participants));
            nativeCatacombsDoorsByEntityId[entityId] = entry with { State = changed };
            ApplyNativeCatacombsDoorFields(Objects[entityId], changed);
        }
    }

    /** Reinstates door-owned piece/special geometry after a generic geometry projection rebuild. */
    internal void PreserveNativeCatacombsDoorProjection()
    {
        foreach (var (levelId, retained) in nativeCatacombsDoorLevels) {
            if (World is null || !World.ContainsLevel(levelId))
                throw new InvalidDataException("A retained Catacombs door lost its world level.");
            projectedLevels[levelId] = retained;
        }
        foreach (var (entityId, entry) in nativeCatacombsDoorsByEntityId) {
            if (!Objects.TryGetValue(entityId, out var value))
                throw new InvalidDataException("A retained Catacombs door lost its object entity.");
            ApplyNativeCatacombsDoorFields(value, entry.State);
        }
        foreach (var owner in nativeCatacombsDoorOwners.Values)
            ValidateNativeCatacombsDoors(owner);
    }

    internal bool TryGetNativeCatacombsDoorState(uint entityId, out AuthoritativeCatacombsDoorState door)
    {
        if (!nativeCatacombsDoorsByEntityId.TryGetValue(entityId, out var entry)) {
            door = null!;
            return false;
        }
        ValidateNativeCatacombsDoor(entry);
        door = entry.State;
        return true;
    }

    internal bool IsNativeCatacombsDoor(uint entityId, uint levelId, int x, int y)
        => nativeCatacombsDoorsByEntityId.TryGetValue(entityId, out var entry)
            && entry.LevelId == levelId && entry.State.PositionX == x && entry.State.PositionY == y;

    /** Native item placement treats open doors as selectable; closed/blocked pieces own geometry. */
    internal bool IsNativeCatacombsDoorItemBlocked(uint levelId, int x, int y)
    {
        bool SelectableAt(int cellX, int cellY) => nativeCatacombsDoorsByEntityId.Values.Any(entry =>
            entry.LevelId == levelId && entry.State.PositionX == cellX && entry.State.PositionY == cellY
                && entry.State.SelectionRegion != 0);
        return SelectableAt(x + 1, y + 1)
            || SelectableAt(x + 1, y) && SelectableAt(x, y + 1);
    }

    /** Snapshots C2 state in deterministic floor/entity order for the format-12 owner. */
    internal IReadOnlyList<AuthoritativeNativeCatacombsDoorFloorState> CaptureNativeCatacombsDoorStates()
    {
        if (!HasNativeCatacombsDoors)
            return Array.Empty<AuthoritativeNativeCatacombsDoorFloorState>();
        var catalogFingerprint = RequireNativeCatacombsDoorCatalog().Fingerprint;
        var result = nativeCatacombsDoorOwners.OrderBy(pair => pair.Key).Select(pair => {
            var (levelId, owner) = (pair.Key, pair.Value);
            ValidateNativeCatacombsDoors(owner);
            var states = nativeCatacombsDoorsByEntityId
                .Where(entry => entry.Value.LevelId == levelId)
                .Select(entry => entry.Value.State)
                .OrderBy(state => state.PositionY).ThenBy(state => state.PositionX).ToArray();
            return new AuthoritativeNativeCatacombsDoorFloorState(
                levelId, owner.Depth, owner.Fingerprint, catalogFingerprint,
                owner.CreateBackingLevel(), nativeCatacombsDoorLevels[levelId], Array.AsReadOnly(states));
        }).ToArray();
        return Array.AsReadOnly(result);
    }

    internal void ValidateNativeCatacombsDoors(AuthoritativeNativePopulationBindings owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var levelId = owner.LevelId;
        if (!nativeCatacombsDoorOwners.TryGetValue(levelId, out var configuredOwner)
            || !ReferenceEquals(configuredOwner, owner)
            || !nativeCatacombsDoorLevels.TryGetValue(levelId, out var level))
            throw new InvalidDataException("A Catacombs door level has no exact retained owner.");
        var catalog = RequireNativeCatacombsDoorCatalog();
        if (World is null || !World.ContainsLevel(levelId))
            throw new InvalidDataException("A Catacombs door owner lost its backing world level.");
        owner.ValidateBackingLevel(World.Levels.Single(level => level.LevelId == levelId));
        var entries = nativeCatacombsDoorsByEntityId.Where(pair => pair.Value.LevelId == levelId).ToArray();
        var rawDoors = owner.InitialState.ActiveObjectIds
            .Take(owner.InitialState.ActiveObjectCount)
            .Select(slot => owner.InitialState.ObjectSlots[slot])
            .Where(raw => raw.DoorFlag)
            .ToArray();
        var backing = owner.CreateBackingLevel();
        foreach (var raw in rawDoors) {
            var entityId = owner.ObjectEntityAt(raw.Position.X, raw.Position.Y)
                ?? throw new InvalidDataException("A Catacombs raw door lost its object entity binding.");
            var current = Objects.GetValueOrDefault(entityId)
                ?? throw new InvalidDataException("A Catacombs raw door lost its projected object.");
            var rawMatches = entries.Where(pair => pair.Key == entityId
                && pair.Value.State.NativeObjectId == (uint)raw.Type
                && pair.Value.State.PositionX == raw.Position.X
                && pair.Value.State.PositionY == raw.Position.Y).ToArray();
            if (rawMatches.Length != 1)
                throw new InvalidDataException("Catacombs door state no longer matches the exact raw object row.");
            var initialProjection = new AuthoritativeWorldObject(entityId, current.ObjectId, levelId,
                raw.Position.X, raw.Position.Y, missilePassable: raw.MissilePassable,
                nativeObjectId: (uint)raw.Type, hideWhenActivated: false,
                animationFrame: checked((int)raw.AnimFrame), trapArmed: raw.TrapFlag);
            _ = AuthoritativeCatacombsDoors.BindRetained(backing, checked((uint)owner.Depth), catalog,
                owner.InitialState, raw, initialProjection);
        }
        foreach (var pair in entries)
            ValidateNativeCatacombsDoor(pair.Value);
        if (owner.Family != AuthoritativeNativePopulationFamily.Catacombs
            || nativeCatacombsDoorCatalogFingerprint != catalog.Fingerprint
            || owner.CatacombsDoorCatalogFingerprint != catalog.Fingerprint
            || entries.Length != owner.InitialDoorCount
            || rawDoors.Length != entries.Length
            || Objects.Values.Count(value => value.LevelId == levelId
                && (value.NativeObjectId == AuthoritativeCatacombsDoorCatalog.LeftNativeObjectId
                    || value.NativeObjectId == AuthoritativeCatacombsDoorCatalog.RightNativeObjectId)) != entries.Length
            || catalog.ForDepth(checked((uint)owner.Depth)).Depth != owner.Depth
            || !ReferenceEquals(projectedLevels.GetValueOrDefault(levelId), level)
            || World is null || !World.ContainsLevel(levelId))
            throw new InvalidDataException("Retained Catacombs door coverage or level projection changed.");
    }

    private AuthoritativeCatacombsDoorCatalog RequireNativeCatacombsDoorCatalog()
    {
        if (nativeCatacombsDoorCatalogFingerprint is null)
            throw new InvalidOperationException("The world has no configured native Catacombs door catalog.");
        return nativeCatacombsDoorCatalog
            ?? throw new InvalidOperationException("The native Catacombs door catalog instance is unavailable.");
    }

    private AuthoritativeCatacombsDoorCatalog? nativeCatacombsDoorCatalog;

    private void ValidateNativeCatacombsDoor(NativeCatacombsDoorEntry entry)
    {
        if (!nativeCatacombsDoorOwners.TryGetValue(entry.LevelId, out var owner)
            || owner.Depth != entry.Depth
            || !nativeCatacombsDoorLevels.TryGetValue(entry.LevelId, out var level)
            || !Objects.TryGetValue(entry.State.EntityId, out var value))
            throw new InvalidDataException("A Catacombs door state lost its owner, level or object.");
        var catalog = RequireNativeCatacombsDoorCatalog();
        AuthoritativeCatacombsDoors.Validate(level, checked((uint)entry.Depth), catalog, entry.State);
        if (value.EntityId != entry.State.EntityId || value.LevelId != entry.LevelId
            || value.NativeObjectId != entry.State.NativeObjectId
            || value.PositionX != entry.State.PositionX || value.PositionY != entry.State.PositionY
            || value.Activated || value.Breakable || value.Destroyed
            || value.CathedralDoor is not null
            || value.AnimationFrame != entry.State.AnimationFrame
            || value.MissilePassable != entry.State.MissFlag)
            throw new InvalidDataException("A Catacombs door object projection changed its retained raw identity or scalar state.");
        owner.ValidateObject(value);
    }

    private bool IsNativeCatacombsDoorOccupied(
        uint levelId,
        AuthoritativeCatacombsDoorState door,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        bool At(int x, int y) => x == door.PositionX && y == door.PositionY;
        return participants.Any(player => player.EntityId != 0 && player.Life > 0 && player.LevelId == levelId
                && (At(player.PositionX, player.PositionY)
                    || player.IsMoving && At(player.FuturePositionX, player.FuturePositionY)))
            || CombatTargets.Values.Any(target => target.LevelId == levelId
                && (target.HitPoints > 0 || target.DeathTicksRemaining > 0 || target.IsPetrified)
                && (At(target.PositionX, target.PositionY)
                    || target.IsMoving && At(target.FuturePositionX, target.FuturePositionY)))
            || WorldItems.Values.Any(item => item.LevelId == levelId && At(item.PositionX, item.PositionY))
            || Corpses.Values.Any(corpse => corpse.LevelId == levelId && At(corpse.PositionX, corpse.PositionY));
    }

    private static void ApplyNativeCatacombsDoorFields(
        AuthoritativeWorldObject value,
        AuthoritativeCatacombsDoorState door)
    {
        value.Activated = false;
        value.AnimationFrame = door.AnimationFrame;
        value.MissilePassable = door.MissFlag;
    }
}
