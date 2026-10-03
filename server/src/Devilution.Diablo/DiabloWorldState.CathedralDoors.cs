namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    public const uint CathedralDoorEntityIdBase = 0x6A00_0000;
    private const int CathedralDoorLevelStride = 112 * 112;
    private readonly AuthoritativeCathedralDoorCatalog? cathedralDoorCatalog;
    private readonly DiabloWorldGeometryState? trustedCathedralDoorBackingGeometry;
    private readonly Dictionary<uint, (uint LevelId, AuthoritativeCathedralDoorState Door)> cathedralDoors = [];
    private readonly Dictionary<uint, AuthoritativeLevel> cathedralDoorLevels = [];

    public bool HasCathedralDoors => cathedralDoorCatalog is not null;

    public void RequireCathedralDoorProfile(DiabloCathedralDoorCheckpoint? checkpoint)
        => RequireCathedralDoorProfile(cathedralDoorCatalog, checkpoint);

    public static uint GetCathedralDoorEntityId(uint levelId, int width, int x, int y)
    {
        if (levelId is < 1 or > 4 || width is < 1 or > 112 || x < 0 || x >= width || y is < 0 or >= 112)
            throw new InvalidDataException("Cathedral door identity is outside the reserved level/cell range.");
        return checked(CathedralDoorEntityIdBase + levelId * CathedralDoorLevelStride
            + (uint)(y * width + x));
    }

    private void InitializeCathedralDoors()
    {
        if (cathedralDoorCatalog is null || World is null)
            throw new InvalidDataException("Cathedral doors require an explicit catalog and world geometry.");
        var prepared = new List<(uint LevelId, AuthoritativeCathedralDoorDiscovery Discovery)>();
        foreach (var definition in cathedralDoorCatalog.Definitions) {
            if (!projectedLevels.TryGetValue(definition.LevelId, out var level))
                throw new InvalidDataException("A mapped Cathedral door level has no geometry.");
            var discovery = AuthoritativeCathedralDoors.Discover(level,
                Objects.Values.Count(value => value.LevelId == level.LevelId));
            if (Objects.Values.Any(value => value.LevelId == level.LevelId
                && (discovery.Doors.Any(door => door.PositionX == value.PositionX && door.PositionY == value.PositionY)
                    || level.ContainsPosition(value.PositionX, value.PositionY)
                        && level.NativePieceLayer[value.PositionY * level.Width + value.PositionX] == 269)))
                throw new InvalidDataException("The door profile excludes overlapping authored scanner objects.");
            if (Objects.Values.Count(value => value.LevelId == level.LevelId) + discovery.Doors.Count
                    > AuthoritativeLevel.NativeMaximumObjectCount)
                throw new InvalidDataException("Generated Cathedral objects exceed the native per-level pool.");
            foreach (var door in discovery.Doors) {
                if (IsEntityIdOccupied(GetCathedralDoorEntityId(level.LevelId, level.Width,
                        door.PositionX, door.PositionY)))
                    throw new InvalidDataException("A generated Cathedral door collides with an existing entity identity.");
            }
            prepared.Add((definition.LevelId, discovery));
        }
        foreach (var (levelId, discovery) in prepared) {
            var definition = cathedralDoorCatalog.Definitions.Single(value => value.LevelId == levelId);
            var backing = World.Levels.Single(value => value.LevelId == levelId);
            World.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(backing,
                special: AuthoritativeCathedralDoors.Discover(backing).Level.NativeSpecialLayer));
            cathedralDoorLevels.Add(levelId, discovery.Level);
            projectedLevels[levelId] = discovery.Level;
            foreach (var door in discovery.Doors) {
                var entityId = GetCathedralDoorEntityId(levelId, discovery.Level.Width, door.PositionX, door.PositionY);
                cathedralDoors.Add(entityId, (levelId, door));
                var value = new AuthoritativeWorldObject(entityId,
                    door.Kind == AuthoritativeCathedralDoorKind.Left ? definition.LeftObjectId : definition.RightObjectId,
                    levelId, door.PositionX, door.PositionY,
                    nativeObjectId: (uint)door.Kind, hideWhenActivated: false,
                    animationFrame: door.AnimationFrame);
                ApplyDoorFields(value, door);
                Objects.Add(entityId, value);
            }
        }
    }

    public bool TryOperateCathedralDoor(uint entityId, IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!cathedralDoors.TryGetValue(entityId, out var entry))
            return false;
        var changed = AuthoritativeCathedralDoors.Operate(cathedralDoorLevels[entry.LevelId], entry.Door,
            IsCathedralDoorOccupied(entry.LevelId, entry.Door, players));
        cathedralDoors[entityId] = (entry.LevelId, changed.Door);
        cathedralDoorLevels[entry.LevelId] = changed.Level;
        projectedLevels[entry.LevelId] = changed.Level;
        ApplyDoorFields(Objects[entityId], changed.Door);
        return true;
    }

    public void UpdateCathedralDoorOccupancy(IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var (entityId, entry) in cathedralDoors.ToArray()) {
            var changed = AuthoritativeCathedralDoors.UpdateOccupancy(entry.Door,
                IsCathedralDoorOccupied(entry.LevelId, entry.Door, players));
            cathedralDoors[entityId] = (entry.LevelId, changed);
            if (Objects.TryGetValue(entityId, out var value))
                ApplyDoorFields(value, changed);
        }
    }

    private bool IsCathedralDoorOccupied(uint levelId, AuthoritativeCathedralDoorState door,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        bool At(int x, int y) => x == door.PositionX && y == door.PositionY;
        // Native tile occupancy includes both signs of actor markers, items and corpses,
        // not the door object itself. Moving reservations therefore remain occupied.
        return players.Any(value => value.EntityId != 0 && value.Life > 0 && value.LevelId == levelId
                && (At(value.PositionX, value.PositionY)
                    || value.IsMoving && At(value.FuturePositionX, value.FuturePositionY)))
            || CombatTargets.Values.Any(value => (value.HitPoints > 0
                    || value.DeathTicksRemaining > 0 || value.IsPetrified)
                && (value.LevelId == levelId || value.LevelId == 0)
                && (At(value.PositionX, value.PositionY)
                    || value.IsMoving && At(value.FuturePositionX, value.FuturePositionY)))
            || WorldItems.Values.Any(value => value.LevelId == levelId && At(value.PositionX, value.PositionY))
            || Corpses.Values.Any(value => value.LevelId == levelId && At(value.PositionX, value.PositionY));
    }

    private static void ApplyDoorFields(AuthoritativeWorldObject value, AuthoritativeCathedralDoorState door)
    {
        value.CathedralDoor = door;
        value.Activated = false; // Door state is not the legacy one-shot activation flag.
        value.AnimationFrame = door.AnimationFrame;
        value.MissilePassable = door.MissFlag;
        value.HideWhenActivated = false;
    }

    /** Refreshes unrelated projections without replaying or losing door operation history. */
    private void PreserveCathedralDoorProjection()
    {
        foreach (var (levelId, level) in cathedralDoorLevels)
            projectedLevels[levelId] = level;
        foreach (var (entityId, entry) in cathedralDoors) {
            if (Objects.TryGetValue(entityId, out var value))
                ApplyDoorFields(value, entry.Door);
        }
    }

    /** The current quest-disabled profile cannot accept uncharacterized map replacements. */
    private void ValidateCathedralDoorMutation(uint mutationId)
    {
        if (cathedralDoorCatalog is not null && WorldMutations?.TryGet(mutationId, out var mutation) == true
            && cathedralDoorCatalog.Definitions.Any(value => value.LevelId == mutation.LevelId))
            throw new InvalidDataException("Cathedral door map resynchronization requires native-qualified replacement support.");
    }

    public DiabloCathedralDoorCheckpoint? CaptureCathedralDoors()
    {
        RequireSpecialLayerPersistenceCoverage();
        if (cathedralDoorCatalog is null)
            return null;
        var checkpoint = new DiabloCathedralDoorCheckpoint(
            DiabloCathedralDoorCheckpoint.CurrentFormatVersion,
            AuthoritativeCathedralDoorCatalog.ProfileId, AuthoritativeCathedralDoorCatalog.Revision,
            cathedralDoorCatalog.Fingerprint, World!.CaptureGeometryState(),
            LevelGeometry.Select(CaptureCohortLevel).ToArray(),
            cathedralDoorCatalog.Definitions.Select(definition => new DiabloCathedralDoorLevelState(
                definition.LevelId,
                World.Levels.Single(value => value.LevelId == definition.LevelId).NativeSpecialLayer.ToArray(),
                cathedralDoorLevels[definition.LevelId].NativeSpecialLayer.ToArray(),
                cathedralDoors.Where(value => value.Value.LevelId == definition.LevelId)
                    .OrderBy(value => value.Value.Door.PositionY).ThenBy(value => value.Value.Door.PositionX)
                    .Select(value => CaptureDoor(value.Key, value.Value.LevelId, value.Value.Door)).ToArray()))
                .ToArray());
        checkpoint.ValidateShape();
        return checkpoint;
    }

    public IReadOnlyList<DiabloCathedralDoorObjectState> CaptureCathedralDoorObjects()
        => cathedralDoors.OrderBy(value => value.Value.LevelId)
            .ThenBy(value => value.Value.Door.PositionY).ThenBy(value => value.Value.Door.PositionX)
            .Select(value => CaptureDoor(value.Key, value.Value.LevelId, value.Value.Door)).ToArray();

    private void RequireSpecialLayerPersistenceCoverage()
    {
        if ((World?.Levels ?? []).Concat(projectedLevels.Values).Any(level => level.StaticObjectBlockedCells.Count != 0))
            throw new InvalidDataException("Live static-object collision must not be stored as canonical geometry.");
        if ((World?.Levels ?? []).Concat(projectedLevels.Values).Any(level =>
            level.NativeSpecialLayer.Count != 0
                && cathedralDoorCatalog?.Definitions.Any(value => value.LevelId == level.LevelId) != true))
            throw new InvalidDataException("Special-layer persistence requires a mapped versioned capability.");
    }

    public static void RequireCathedralDoorProfile(AuthoritativeCathedralDoorCatalog? catalog,
        DiabloCathedralDoorCheckpoint? checkpoint)
    {
        if (catalog is null && checkpoint is null)
            return;
        if (catalog is null || checkpoint is null)
            throw new InvalidDataException("Saved Cathedral door capability does not match the active profile.");
        checkpoint.ValidateShape();
        if (checkpoint.CatalogFingerprint != catalog.Fingerprint
            || !checkpoint.DoorLevels.Select(value => value.LevelId).SequenceEqual(
                catalog.Definitions.Select(value => value.LevelId)))
            throw new InvalidDataException("Saved Cathedral doors do not match the active content catalog.");
    }

    /** Validates detached geometry and object coverage before publishing exact saved state. */
    public void RestoreCathedralDoors(DiabloCathedralDoorCheckpoint? checkpoint,
        IReadOnlyList<DiabloObjectState> objects)
    {
        RequireCathedralDoorProfile(cathedralDoorCatalog, checkpoint);
        if (checkpoint is null)
            return;
        if (World is null)
            throw new InvalidDataException("A door checkpoint requires backing world geometry.");
        DiabloCathedralDoorCheckpoint.RequireSameGeometry(
            trustedCathedralDoorBackingGeometry, checkpoint.BackingGeometry);
        var backing = new AuthoritativeWorld();
        backing.RestoreGeometryState(checkpoint.BackingGeometry);
        var effective = ValidateCohortEffectiveLevels(checkpoint.EffectiveLevels).ToDictionary();
        var legitimatelyProjected = BuildProjectedGeometry();
        foreach (var (levelId, level) in effective) {
            if (!checkpoint.DoorLevels.Any(value => value.LevelId == levelId))
                DiabloCathedralDoorCheckpoint.RequireSameLevels(
                    [CaptureCohortLevel(legitimatelyProjected[levelId])], [CaptureCohortLevel(level)]);
        }
        var restored = new Dictionary<uint, (uint LevelId, AuthoritativeCathedralDoorState Door)>();
        foreach (var saved in checkpoint.DoorLevels) {
            var baseline = backing.Levels.SingleOrDefault(value => value.LevelId == saved.LevelId)
                ?? throw new InvalidDataException("Door checkpoint has no backing level.");
            if (saved.BackingSpecial.Count != checked(baseline.Width * baseline.Height)
                || saved.EffectiveSpecial.Count != checked(baseline.Width * baseline.Height)
                || !saved.BackingSpecial.SequenceEqual(
                    AuthoritativeCathedralDoors.Discover(baseline).Level.NativeSpecialLayer))
                throw new InvalidDataException("Door checkpoint special layers do not match complete native backing geometry.");
            baseline = AuthoritativeCathedralDoors.CopyLevel(baseline, special: saved.BackingSpecial);
            backing.ReplaceLevel(baseline);
            var projected = AuthoritativeCathedralDoors.CopyLevel(effective[saved.LevelId], special: saved.EffectiveSpecial);
            effective[saved.LevelId] = projected;
            var additionalObjects = objects.Count(value => value.LevelId == saved.LevelId) - saved.Doors.Count;
            if (additionalObjects < 0)
                throw new InvalidDataException("Door checkpoint is missing its generated object cohort.");
            var discovered = AuthoritativeCathedralDoors.Discover(baseline, additionalObjects);
            var expected = discovered.Doors;
            if (objects.Any(value => value.LevelId == saved.LevelId
                && !saved.Doors.Any(door => door.EntityId == value.EntityId)
                && (expected.Any(door => door.PositionX == value.PositionX && door.PositionY == value.PositionY)
                    || baseline.ContainsPosition(value.PositionX, value.PositionY)
                        && baseline.NativePieceLayer[value.PositionY * baseline.Width + value.PositionX] == 269)))
                throw new InvalidDataException("Door checkpoint contains overlapping scanner objects.");
            if (saved.Doors.Count != expected.Count)
                throw new InvalidDataException("Door checkpoint omits or adds a native-scanned door.");
            foreach (var (state, index) in saved.Doors.Select((state, index) => (state, index))) {
                var initial = expected[index];
                var door = new AuthoritativeCathedralDoorState((AuthoritativeCathedralDoorKind)state.NativeObjectId,
                    state.PositionX, state.PositionY, (AuthoritativeCathedralDoorStatus)state.DoorState,
                    state.CachedPiece, state.CachedNeighborPiece, state.AnimationFrame,
                    state.SelectionRegion, state.PreDraw, state.MissilePassable, state.Solid);
                AuthoritativeCathedralDoors.Validate(projected, door);
                var definition = cathedralDoorCatalog!.Definitions.Single(value => value.LevelId == saved.LevelId);
                var objectId = door.Kind == AuthoritativeCathedralDoorKind.Left ? definition.LeftObjectId : definition.RightObjectId;
                var matches = objects.Where(value => value.EntityId == state.EntityId).ToArray();
                var expectedObject = DiabloSimulation.CaptureObject(new AuthoritativeWorldObject(
                    state.EntityId, objectId, saved.LevelId, door.PositionX, door.PositionY,
                    missilePassable: door.MissFlag, nativeObjectId: (uint)door.Kind,
                    hideWhenActivated: false, animationFrame: door.AnimationFrame));
                if (door.Kind != initial.Kind || door.PositionX != initial.PositionX || door.PositionY != initial.PositionY
                    || door.Var1 != initial.Var1 || door.Var2 != initial.Var2
                    || state.EntityId != GetCathedralDoorEntityId(saved.LevelId, projected.Width, door.PositionX, door.PositionY)
                    || matches.Length != 1 || matches[0] != expectedObject
                    || matches[0].LevelId != saved.LevelId || matches[0].PositionX != door.PositionX || matches[0].PositionY != door.PositionY
                    || matches[0].NativeObjectId != (uint)door.Kind || matches[0].Activated || matches[0].Destroyed
                    || matches[0].EffectKind != AuthoritativeObjectEffectKind.None || matches[0].HideWhenActivated
                    || matches[0].AnimationFrame != door.AnimationFrame || matches[0].MissilePassable != door.MissFlag
                    || !restored.TryAdd(state.EntityId, (saved.LevelId, door)))
                    throw new InvalidDataException("Door checkpoint conflicts with its native scan or saved object projection.");
            }
            ValidateCathedralDoorEffectiveGeometry(discovered.Level, projected, saved.Doors);
        }
        // All fallible validation above is detached. Install exact geometry, never SyncDoor.
        World.RestoreGeometryState(backing.CaptureGeometryState());
        foreach (var saved in checkpoint.DoorLevels)
            World.ReplaceLevel(backing.Levels.Single(value => value.LevelId == saved.LevelId));
        projectedLevels.Clear();
        foreach (var (levelId, level) in effective)
            projectedLevels.Add(levelId, level);
        cathedralDoorLevels.Clear();
        foreach (var saved in checkpoint.DoorLevels)
            cathedralDoorLevels.Add(saved.LevelId, effective[saved.LevelId]);
        cathedralDoors.Clear();
        foreach (var (entityId, entry) in restored)
            cathedralDoors.Add(entityId, entry);
        PreserveCathedralDoorProjection();
    }

    /** Checks mutable cells against immutable SOL/layers, retaining shared-frame history. */
    private static void ValidateCathedralDoorEffectiveGeometry(AuthoritativeLevel baseline,
        AuthoritativeLevel effective, IReadOnlyList<DiabloCathedralDoorObjectState> doors)
    {
        var expected = CaptureCohortLevel(baseline);
        var actual = CaptureCohortLevel(effective);
        DiabloCathedralDoorCheckpoint.RequireSameLevels([expected], [actual with {
            NativePieceLayer = expected.NativePieceLayer,
            BlockedCells = expected.BlockedCells,
            MissileBlockedCells = expected.MissileBlockedCells,
            NativePieceCollisionCells = expected.NativePieceCollisionCells,
            NativePieceMissileBlockedCells = expected.NativePieceMissileBlockedCells,
            NativeVisionBlockedCells = expected.NativeVisionBlockedCells,
        }]);
        var mainCells = doors.ToDictionary(door => door.PositionY * baseline.Width + door.PositionX);
        var changedCells = mainCells.Keys.ToHashSet();
        var neighborCells = new HashSet<int>();
        foreach (var door in doors)
            neighborCells.Add(door.NativeObjectId == 1
                ? (door.PositionY - 1) * baseline.Width + door.PositionX
                : door.PositionY * baseline.Width + door.PositionX - 1);
        changedCells.UnionWith(neighborCells);
        var allowedPieces = changedCells.ToDictionary(cell => cell,
            cell => new HashSet<ushort> { baseline.NativePieceLayer[cell] });
        foreach (var door in doors) {
            var main = door.PositionY * baseline.Width + door.PositionX;
            var neighbor = door.NativeObjectId == 1 ? main - baseline.Width : main - 1;
            allowedPieces[main].Add(checked((ushort)(door.CachedPiece - 1)));
            allowedPieces[main].Add((ushort)(door.NativeObjectId == 1
                ? door.CachedPiece == 214 ? 407 : 392 : 394));
            allowedPieces[neighbor].Add(checked((ushort)(door.CachedNeighborPiece - 1)));
            if (door.CachedNeighborPiece == 50)
                allowedPieces[neighbor].Add((ushort)(door.NativeObjectId == 1 ? 411 : 410));
        }
        bool expanded;
        do {
            expanded = false;
            foreach (var door in doors) {
                var main = door.PositionY * baseline.Width + door.PositionX;
                var neighbor = door.NativeObjectId == 1 ? main - baseline.Width : main - 1;
                foreach (var piece in allowedPieces[neighbor].ToArray())
                    expanded |= allowedPieces[neighbor].Add(AuthoritativeCathedralDoors.OpenFrame(
                        piece, (AuthoritativeCathedralDoorKind)door.NativeObjectId));
            }
        } while (expanded);
        // A shared cell retains history, but its last native writer must agree
        // with that writer's current status. Do not replay doors on restore.
        var lastWrites = changedCells.ToDictionary(cell => cell, _ => new HashSet<ushort>());
        foreach (var door in doors) {
            var main = door.PositionY * baseline.Width + door.PositionX;
            var neighbor = door.NativeObjectId == 1 ? main - baseline.Width : main - 1;
            lastWrites[main].Add((ushort)(door.DoorState == 0 ? door.CachedPiece - 1
                : door.NativeObjectId == 1 ? door.CachedPiece == 214 ? 407 : 392 : 394));
            if (door.DoorState == 0) {
                lastWrites[neighbor].Add(checked((ushort)(door.CachedNeighborPiece - 1)));
                if (door.CachedNeighborPiece == 50)
                    lastWrites[neighbor].Add((ushort)(door.NativeObjectId == 1 ? 411 : 410));
            } else {
                foreach (var piece in allowedPieces[neighbor])
                    lastWrites[neighbor].Add(AuthoritativeCathedralDoors.OpenFrame(piece,
                        (AuthoritativeCathedralDoorKind)door.NativeObjectId));
            }
        }
        foreach (var cell in changedCells) {
            if (!lastWrites[cell].Contains(effective.NativePieceLayer[cell]))
                throw new InvalidDataException("Door checkpoint shared cell conflicts with its possible last native writer.");
        }
        foreach (var door in doors) {
            var main = door.PositionY * baseline.Width + door.PositionX;
            var neighbor = door.NativeObjectId == 1 ? main - baseline.Width : main - 1;
            if (mainCells.ContainsKey(neighbor) || doors.Count(other =>
                    other.NativeObjectId == 1
                        ? other.PositionY * baseline.Width + other.PositionX - baseline.Width == neighbor
                        : other.PositionY * baseline.Width + other.PositionX - 1 == neighbor) != 1)
                continue;
            var allowed = new HashSet<ushort>();
            var cached = checked((ushort)(door.CachedNeighborPiece - 1));
            if (door.DoorState == 0) {
                allowed.Add(cached);
                if (door.CachedNeighborPiece == 50)
                    allowed.Add((ushort)(door.NativeObjectId == 1 ? 411 : 410));
            } else {
                var current = AuthoritativeCathedralDoors.OpenFrame(cached, (AuthoritativeCathedralDoorKind)door.NativeObjectId);
                while (allowed.Add(current))
                    current = AuthoritativeCathedralDoors.OpenFrame(current, (AuthoritativeCathedralDoorKind)door.NativeObjectId);
            }
            if (!allowed.Contains(effective.NativePieceLayer[neighbor]))
                throw new InvalidDataException("Door checkpoint isolated frame conflicts with native status.");
        }
        var blocked = baseline.BlockedCells.ToHashSet();
        var missile = baseline.MissileBlockedCells.ToHashSet();
        var nativeBlocked = baseline.NativePieceCollisionCells.ToHashSet();
        var nativeMissile = baseline.NativePieceMissileBlockedCells.ToHashSet();
        var vision = baseline.NativeVisionBlockedCells.ToHashSet();
        for (var cell = 0; cell < baseline.NativePieceLayer.Count; cell++) {
            if (!changedCells.Contains(cell)
                && effective.NativePieceLayer[cell] != baseline.NativePieceLayer[cell])
                throw new InvalidDataException("Door checkpoint changes an unowned d-piece cell.");
            if (allowedPieces.TryGetValue(cell, out var allowed)
                && !allowed.Contains(effective.NativePieceLayer[cell]))
                throw new InvalidDataException("Door checkpoint has an unreachable owned d-piece value.");
            var special = mainCells.TryGetValue(cell, out var door)
                ? door.DoorState == 0 ? 0 : door.NativeObjectId == 1 ? 7 : 8
                : baseline.NativeSpecialLayer[cell];
            if (door is not null && !neighborCells.Contains(cell)) {
                var piece = door.DoorState == 0 ? door.CachedPiece - 1
                    : door.NativeObjectId == 1 ? door.CachedPiece == 214 ? 407 : 392 : 394;
                if (effective.NativePieceLayer[cell] != piece)
                    throw new InvalidDataException("Door checkpoint main d-piece conflicts with native status.");
            }
            if (effective.NativeSpecialLayer[cell] != special)
                throw new InvalidDataException("Door checkpoint changes unowned or inconsistent special bytes.");
            if (!changedCells.Contains(cell))
                continue;
            if (!baseline.NativePieceProperties.TryGetValue(effective.NativePieceLayer[cell], out var flags))
                throw new InvalidDataException("Door checkpoint uses a d-piece outside its trusted SOL catalog.");
            if (nativeBlocked.Remove(cell))
                blocked.Remove(cell);
            if (nativeMissile.Remove(cell))
                missile.Remove(cell);
            if ((baseline.NativePieceProperties[baseline.NativePieceLayer[cell]]
                    & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
                vision.Remove(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid)) {
                blocked.Add(cell);
                nativeBlocked.Add(cell);
            }
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile)) {
                missile.Add(cell);
                nativeMissile.Add(cell);
            }
            if ((flags & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
                vision.Add(cell);
        }
        if (!blocked.SetEquals(effective.BlockedCells)
            || !missile.SetEquals(effective.MissileBlockedCells)
            || !nativeBlocked.SetEquals(effective.NativePieceCollisionCells)
            || !nativeMissile.SetEquals(effective.NativePieceMissileBlockedCells)
            || !vision.SetEquals(effective.NativeVisionBlockedCells))
            throw new InvalidDataException("Door checkpoint blockers disagree with trusted SOL and owned cells.");
    }

    public bool IsCathedralDoorObject(uint entityId, uint levelId, int x, int y)
        => cathedralDoors.TryGetValue(entityId, out var entry) && entry.LevelId == levelId
            && entry.Door.PositionX == x && entry.Door.PositionY == y;

    /** Native death clears player occupancy even when its corpse remains in a door cell. */
    internal bool IsDeadPlayerOnCathedralDoor(int life, uint levelId, int x, int y)
        => life == 0 && cathedralDoors.Values.Any(entry => entry.LevelId == levelId
            && entry.Door.PositionX == x && entry.Door.PositionY == y);

    /** Native item overlap uses selectable doors, not movement occupancy. */
    public bool IsCathedralDoorItemBlocked(uint levelId, int x, int y)
    {
        bool InteractiveAt(int cellX, int cellY) => cathedralDoors.Values.Any(value =>
            value.LevelId == levelId && value.Door.PositionX == cellX
                && value.Door.PositionY == cellY && value.Door.SelectionRegion != 0);
        return IsCathedralLightObjectSolid(levelId, x, y) || InteractiveAt(x + 1, y + 1)
            || InteractiveAt(x + 1, y) && InteractiveAt(x, y + 1);
    }

    /** Reserved scanner lights are solid but non-selectable; animation/RNG is separate. */
    internal bool IsCathedralLightObjectSolid(uint levelId, int x, int y)
        => cathedralDoorLevels.TryGetValue(levelId, out var level)
            && level.ContainsPosition(x, y) && level.NativePieceLayer[y * level.Width + x] == 269;

    /** Fixed scanner lights cannot become a legitimate pending live move destination. */
    internal bool IsPendingPlayerMoveIntoCathedralLight(int life, uint levelId, int x, int y,
        AuthoritativePlayerActionState? action)
        => cathedralDoorLevels.ContainsKey(levelId) && life > 0
            && action is { Kind: DiabloPlayerActionKind.Move, EffectApplied: false }
            && IsCathedralLightObjectSolid(levelId, checked(x + action.DirectionX), checked(y + action.DirectionY));

    /** Live collision includes scanner light footprints without changing persisted SOL geometry. */
    public IReadOnlyList<AuthoritativeLevel> CapturePresentationLevelGeometry()
        => LevelGeometry.Select(level => {
            if (!cathedralDoorLevels.ContainsKey(level.LevelId))
                return level;
            var lights = level.NativePieceLayer.Select((piece, cell) => (piece, cell))
                .Where(value => value.piece == 269).Select(value => value.cell).ToArray();
            return lights.Length == 0 ? level : AuthoritativeCathedralDoors.CopyLevel(level,
                staticObjectBlocked: lights);
        }).ToArray();

    private static DiabloCathedralDoorObjectState CaptureDoor(uint entityId, uint levelId, AuthoritativeCathedralDoorState door)
        => new(entityId, levelId, door.PositionX, door.PositionY, (int)door.Kind, (int)door.Status,
            door.Var1, door.Var2, door.AnimationFrame, door.SelectionRegion, door.PreFlag, door.MissFlag, door.SolidFlag);
}
