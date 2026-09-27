using Devilution.Engine;

namespace Devilution.Diablo;

/** Module-owned authoritative world state and deterministic navigation rules. */
public sealed class DiabloWorldState
{
    /** Per-category bound shared by live state and save restoration. */
    public const int MaximumWorldStateEntries = 4096;

    private readonly int fallbackWidth;
    private readonly int fallbackHeight;
    private readonly HashSet<int> fallbackBlockedCells;
    private readonly HashSet<int> fallbackMissileBlockedCells;
    private readonly AuthoritativeNativeDunObjectCatalog? nativeDunObjects;
    private readonly IReadOnlyList<AuthoritativeWorldObject> nativeObjectTemplates;
    private readonly bool allowMissingNativeDunLayers;
    private Func<int, AuthoritativeSetLevelObjectOrigin?>? setLevelOriginResolver;
    private Func<uint, AuthoritativeProceduralQuestPieceOrigin?>? proceduralQuestPieceOriginResolver;
    private readonly Dictionary<uint, AuthoritativeLevel> projectedLevels = [];
    private readonly HashSet<uint> activeWorldMutationIds = [];
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
        IEnumerable<int>? fallbackMissileBlockedCells = null,
        IReadOnlyList<AuthoritativeCorpse>? corpses = null,
        AuthoritativeWorldMutationCatalog? worldMutations = null,
        IEnumerable<uint>? activeWorldMutationIds = null,
        AuthoritativeNativeDunObjectCatalog? nativeDunObjects = null,
        IReadOnlyList<AuthoritativeWorldObject>? nativeObjectTemplates = null,
        bool allowMissingNativeDunLayers = false)
    {
        ValidateEntryCount(combatTargets, "combat target");
        ValidateEntryCount(
            portals,
            "portal",
            AuthoritativePortalRules.NativeMaximumPortalCount);
        ValidateEntryCount(worldItems, "world item");
        ValidateEntryCount(objects, "object");
        ValidateEntryCount(projectiles, "projectile");
        ValidateEntryCount(corpses, "corpse");
        World = world;
        WorldMutations = worldMutations;
        this.nativeDunObjects = nativeDunObjects;
        this.nativeObjectTemplates = nativeObjectTemplates ?? [];
        this.allowMissingNativeDunLayers = allowMissingNativeDunLayers;
        foreach (var mutationId in activeWorldMutationIds ?? []) {
            if (WorldMutations is null || !WorldMutations.TryGet(mutationId, out _))
                throw new InvalidDataException(
                    $"Active world mutation {mutationId} is not supplied by the content catalog.");
            this.activeWorldMutationIds.Add(mutationId);
        }
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

        var entityIds = new HashSet<uint>();
        void RegisterEntityId(uint entityId)
        {
            if (!entityIds.Add(entityId))
                throw new InvalidDataException(
                    $"Entity {entityId} is registered more than once across world-state categories.");
        }

        foreach (var target in combatTargets ?? []) {
            RegisterEntityId(target.EntityId);
            CombatTargets.Add(target.EntityId, target);
        }
        ValidateNativeMonsterPoolCapacity(CombatTargets.Values);
        foreach (var portal in portals ?? []) {
            ValidatePortalState(portal);
            if (!Portals.TryAdd(portal.PortalId, portal))
                throw new InvalidDataException(
                    $"Portal {portal.PortalId} is registered more than once.");
        }
        foreach (var item in worldItems ?? []) {
            RegisterEntityId(item.EntityId);
            WorldItems.Add(item.EntityId, item);
        }
        foreach (var @object in objects ?? []) {
            RegisterEntityId(@object.EntityId);
            Objects.Add(@object.EntityId, @object);
        }
        foreach (var quest in quests ?? []) {
            if (!Quests.TryAdd(quest.QuestId, quest))
                throw new InvalidDataException($"Quest {quest.QuestId} is registered more than once.");
        }
        foreach (var projectile in projectiles ?? []) {
            RegisterEntityId(projectile.EntityId);
            Projectiles.Add(projectile.EntityId, projectile);
        }
        foreach (var corpse in corpses ?? []) {
            RegisterEntityId(corpse.EntityId);
            Corpses.Add(corpse.EntityId, corpse);
        }
        if (World is not null)
            WorldMutations?.ValidateWorld(World, Objects.Values, CombatTargets.Values);
        RebuildProjectedGeometry();

        static void ValidateEntryCount<T>(
            IReadOnlyList<T>? entries,
            string category,
            int maximum = MaximumWorldStateEntries)
        {
            if (entries is not null && entries.Count > maximum)
                throw new InvalidDataException(
                    $"Initial world state contains too many {category} entries.");
        }
    }

    public AuthoritativeWorld? World { get; }

    /** Adds late-generated level geometry and refreshes the effective projection. */
    public void AddLevel(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (World is null)
            throw new InvalidDataException(
                "Late-generated levels require an authoritative world geometry catalog.");
        World.AddLevel(level);
        RebuildProjectedGeometry();
    }

    /** Replaces late-generated level geometry and refreshes the effective projection. */
    public void ReplaceLevel(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (World is null)
            throw new InvalidDataException(
                "Generated level replacement requires an authoritative world geometry catalog.");
        World.ReplaceLevel(level);
        RebuildProjectedGeometry();
    }

    /** Rebuilds effective geometry after a catalog mutates the backing world. */
    public void RefreshProjectedGeometry()
    {
        RebuildProjectedGeometry();
    }

    /** Validated content-backed level-cell overlays available to object effects. */
    public AuthoritativeWorldMutationCatalog? WorldMutations { get; }

    /** Active mutation identities persisted with the authoritative session. */
    public IReadOnlySet<uint> ActiveWorldMutationIds => activeWorldMutationIds;

    /** Effective level geometry after all active content mutations. */
    public IReadOnlyList<AuthoritativeLevel> LevelGeometry
        => World is null
            ? []
            : projectedLevels.Values.OrderBy(level => level.LevelId).ToArray();

    public bool ApplyWorldMutation(uint mutationId)
    {
        if (WorldMutations is null
            || !WorldMutations.TryGet(mutationId, out var mutation)
            || mutation.AnchorSetLevelId >= 0
                && (setLevelOriginResolver is null
                    || setLevelOriginResolver(mutation.AnchorSetLevelId) is null)
            || mutation.AnchorProceduralQuestPieceOriginId != 0
                && (proceduralQuestPieceOriginResolver is null
                    || proceduralQuestPieceOriginResolver(mutation.AnchorProceduralQuestPieceOriginId) is null)
            || !activeWorldMutationIds.Add(mutationId)) {
            return WorldMutations?.TryGet(mutationId, out _) == true
                && activeWorldMutationIds.Contains(mutationId);
        }
        RebuildProjectedGeometry();
        return true;
    }

    public void RestoreWorldMutations(IEnumerable<uint> mutationIds)
    {
        ArgumentNullException.ThrowIfNull(mutationIds);
        var restoredMutationIds = new HashSet<uint>();
        foreach (var mutationId in mutationIds) {
            if (WorldMutations is null || !WorldMutations.TryGet(mutationId, out _))
                throw new InvalidDataException(
                    $"Active world mutation {mutationId} is not supplied by the content catalog.");
            if (!restoredMutationIds.Add(mutationId))
                throw new InvalidDataException(
                    $"Active world mutation {mutationId} is listed more than once.");
        }
        activeWorldMutationIds.Clear();
        activeWorldMutationIds.UnionWith(restoredMutationIds);
        RebuildProjectedGeometry();
    }

    internal void SetSetLevelOriginResolver(
        Func<int, AuthoritativeSetLevelObjectOrigin?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        setLevelOriginResolver = resolver;
        RebuildProjectedGeometry();
    }

    internal void SetProceduralQuestPieceOriginResolver(
        Func<uint, AuthoritativeProceduralQuestPieceOrigin?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        proceduralQuestPieceOriginResolver = resolver;
        RebuildProjectedGeometry();
    }

    public Dictionary<uint, AuthoritativeCombatTarget> CombatTargets { get; } = [];

    /** Native-compatible terminal Diablo defeat state owned by the server. */
    public bool VictoryTriggered { get; internal set; }

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

    /** Defeated non-minion monster tiles used by corpse-consuming AI. */
    public Dictionary<uint, AuthoritativeCorpse> Corpses { get; } = [];

    /** Returns whether an ID is occupied by any authoritative world entity. */
    public bool IsEntityIdOccupied(uint entityId)
    {
        return CombatTargets.ContainsKey(entityId)
            || WorldItems.ContainsKey(entityId)
            || Objects.ContainsKey(entityId)
            || Projectiles.ContainsKey(entityId)
            || Portals.ContainsKey(entityId)
            || Corpses.ContainsKey(entityId);
    }

    public bool IsWalkable(uint levelId, int positionX, int positionY)
    {
        if (World is not null)
            return projectedLevels.TryGetValue(levelId, out var level)
                && level.IsWalkable(positionX, positionY);
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

    /** Allows the exact native positive-edge center retained by an Elemental impact. */
    public bool ContainsProjectilePosition(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeProjectilePhase phase,
        uint levelId,
        int positionX,
        int positionY)
    {
        if (ContainsPosition(levelId, positionX, positionY))
            return true;
        if (behavior != AuthoritativeProjectileBehavior.Elemental
            || phase != AuthoritativeProjectilePhase.Impact
            || positionX < 0
            || positionY < 0) {
            return false;
        }

        var width = fallbackWidth;
        var height = fallbackHeight;
        if (World is not null) {
            if (!projectedLevels.TryGetValue(levelId, out var level))
                return false;
            width = level.Width;
            height = level.Height;
        }
        return positionX <= width
            && positionY <= height
            && (positionX == width || positionY == height);
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
            return projectedLevels.TryGetValue(levelId, out var level)
                && level.IsMissileBlocked(positionX, positionY);
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
        if (!ContainsPosition(levelId, startX, startY)
            || !ContainsPosition(levelId, endX, endY)) {
            return false;
        }

        if (World is not null) {
            var level = projectedLevels.TryGetValue(levelId, out var projected)
                ? projected
                : null;
            return level?.HasLineOfSight(startX, startY, endX, endY) ?? false;
        }

        return DeterministicGridLineTraversal.HasLineOfSight(
            startX,
            startY,
            endX,
            endY,
            (x, y) => !IsWalkable(levelId, x, y));
    }

    /** Applies native vision to authoritative direct-target interactions when geometry is present. */
    public bool HasTargetLineOfSight(
        uint levelId,
        int startX,
        int startY,
        int endX,
        int endY,
        int viewerLightRadius = AuthoritativeEquipmentLightRules.NativeDefaultLightRadius)
    {
        if (!ContainsPosition(levelId, startX, startY)
            || !ContainsPosition(levelId, endX, endY)) {
            return false;
        }

        if (World is not null
            && projectedLevels.TryGetValue(levelId, out var level)
            && (level.NativeTransparency.Count != 0
                || level.NativeVisionBlockedCells.Count != 0)) {
            return level.HasNativeVisionLineOfSight(
                startX,
                startY,
                endX,
                endY,
                viewerLightRadius);
        }

        return HasLineOfSight(levelId, startX, startY, endX, endY);
    }

    /** Applies the native vision ray table to monster AI when projected geometry is present. */
    public bool HasAiLineOfSight(
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

        if (World is not null
            && projectedLevels.TryGetValue(levelId, out var level)
            && (level.NativeTransparency.Count != 0
                || level.NativeVisionBlockedCells.Count != 0)) {
            return level.HasNativeVisionLineOfSight(startX, startY, endX, endY);
        }

        return HasLineOfSight(levelId, startX, startY, endX, endY);
    }

    private void RebuildProjectedGeometry()
    {
        projectedLevels.Clear();
        if (World is null)
            return;

        foreach (var baseLevel in World.Levels) {
            var blocked = baseLevel.BlockedCells.ToHashSet();
            var missileBlocked = baseLevel.MissileBlockedCells.ToHashSet();
            var transparency = baseLevel.NativeTransparency.ToArray();
            var monsterLayer = baseLevel.NativeMonsterLayer.ToArray();
            var objectLayer = baseLevel.NativeObjectLayer.ToArray();
            var megaTileLayer = baseLevel.NativeMegaTileLayer.ToArray();
            var pieceLayer = baseLevel.NativePieceLayer.ToArray();
            var nativePieceProperties = baseLevel.NativePieceProperties.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
            var nativePieceCollisionCells = baseLevel.NativePieceCollisionCells.ToHashSet();
            var nativePieceMissileBlockedCells = baseLevel.NativePieceMissileBlockedCells.ToHashSet();
            foreach (var mutationId in activeWorldMutationIds.OrderBy(id => id)) {
                if (WorldMutations?.TryGet(mutationId, out var mutation) != true
                    || mutation.LevelId != baseLevel.LevelId) {
                    continue;
                }
                var mutationOrigin = ResolveMutationOrigin(mutation);
                if (mutationOrigin is null)
                    continue;
                var (originX, originY) = mutationOrigin.Value;
                foreach (var pieceProperty in mutation.PiecePropertyOverrides) {
                    if (nativePieceProperties.TryGetValue(
                            pieceProperty.PieceId,
                            out var existingFlags)
                        && existingFlags != pieceProperty.Flags) {
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} redefines d-piece "
                            + $"{pieceProperty.PieceId} with different collision flags.");
                    }
                    nativePieceProperties[pieceProperty.PieceId] = pieceProperty.Flags;
                }
                blocked.UnionWith(mutation.GetBlockedCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY));
                blocked.ExceptWith(mutation.GetBlockedCellsToRemove(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY));
                missileBlocked.UnionWith(mutation.GetMissileBlockedCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY));
                missileBlocked.ExceptWith(
                    mutation.GetMissileBlockedCellsToRemove(
                        baseLevel.Width,
                        baseLevel.Height,
                        originX,
                        originY));
                foreach (var (cell, value) in mutation.GetTransparencyCells(
                             baseLevel.Width,
                             baseLevel.Height,
                             originX,
                             originY)) {
                    if (transparency.Length == 0 && mutation.CreateTransparencyLayer)
                        transparency = new ushort[checked(baseLevel.Width * baseLevel.Height)];
                    if (transparency.Length == 0)
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires a native transparency layer.");
                    transparency[cell] = value;
                }
                var objectLayerCells = mutation.GetObjectLayerCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY);
                if (objectLayerCells.Count > 0 && objectLayer.Length == 0)
                    objectLayer = new ushort[checked(baseLevel.Width * baseLevel.Height)];
                foreach (var (cell, objectId) in objectLayerCells) {
                    if (objectLayer.Length == 0)
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires a native object layer.");
                    objectLayer[cell] = objectId;
                }
                var monsterLayerCells = mutation.GetMonsterLayerCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY);
                if (monsterLayerCells.Count > 0 && monsterLayer.Length == 0)
                    monsterLayer = new ushort[checked(baseLevel.Width * baseLevel.Height)];
                foreach (var (cell, monsterId) in monsterLayerCells) {
                    if (monsterLayer.Length == 0)
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires a native monster layer.");
                    monsterLayer[cell] = monsterId;
                }
                var pieceLayerCells = mutation.GetPieceLayerCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY);
                if (pieceLayerCells.Count > 0 && pieceLayer.Length == 0)
                    pieceLayer = new ushort[checked(baseLevel.Width * baseLevel.Height)];
                foreach (var (cell, pieceId) in pieceLayerCells) {
                    if (pieceLayer.Length == 0)
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires a native d-piece layer.");
                    if (nativePieceCollisionCells.Remove(cell))
                        blocked.Remove(cell);
                    if (nativePieceMissileBlockedCells.Remove(cell))
                        missileBlocked.Remove(cell);
                    pieceLayer[cell] = pieceId;
                    if (nativePieceProperties.TryGetValue(
                            pieceId,
                            out var pieceFlags)) {
                        if (pieceFlags.HasFlag(AuthoritativeDunTileFlags.Solid)) {
                            blocked.Add(cell);
                            nativePieceCollisionCells.Add(cell);
                        }
                        if (pieceFlags.HasFlag(AuthoritativeDunTileFlags.BlockMissile)) {
                            missileBlocked.Add(cell);
                            nativePieceMissileBlockedCells.Add(cell);
                        }
                    }
                }
                var megaTileCells = mutation.GetMegaTileCells(
                    baseLevel.Width,
                    baseLevel.Height,
                    originX,
                    originY);
                if (megaTileCells.Count > 0 && megaTileLayer.Length == 0)
                    megaTileLayer = new ushort[checked(((baseLevel.Width + 1) / 2)
                        * ((baseLevel.Height + 1) / 2))];
                foreach (var (cell, tileId) in megaTileCells)
                    megaTileLayer[cell] = tileId;
                if (mutation.MegaTileCopyRectangles.Count > 0) {
                    var sourceLevel = World.Levels.SingleOrDefault(candidate =>
                        candidate.LevelId == mutation.MegaTileCopySourceLevelId);
                    if (sourceLevel is null
                        || sourceLevel.NativeMegaTileLayer.Count == 0
                        || baseLevel.NativeMegaTileLayer.Count == 0) {
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires native mega-tile layers on both target and source levels.");
                    }
                    if (sourceLevel.NativePieceLayer.Count
                            != checked(sourceLevel.Width * sourceLevel.Height)
                        || baseLevel.NativePieceLayer.Count
                            != checked(baseLevel.Width * baseLevel.Height)) {
                        throw new InvalidDataException(
                            $"World mutation {mutation.MutationId} requires native d-piece layers on both target and source levels.");
                    }
                    var copyCells = mutation.GetMegaTileCopyCells(
                        baseLevel.Width,
                        baseLevel.Height,
                        sourceLevel.Width,
                        sourceLevel.Height);
                    var targetMegaWidth = checked((baseLevel.Width + 1) / 2);
                    var sourceMegaWidth = checked((sourceLevel.Width + 1) / 2);
                    foreach (var (targetCell, sourceCell) in copyCells) {
                        megaTileLayer[targetCell] = sourceLevel.NativeMegaTileLayer[sourceCell];
                        var megaX = targetCell % targetMegaWidth;
                        var megaY = targetCell / targetMegaWidth;
                        var sourceMegaX = sourceCell % sourceMegaWidth;
                        var sourceMegaY = sourceCell / sourceMegaWidth;
                        for (var offsetY = 0; offsetY < 2; offsetY++) {
                            for (var offsetX = 0; offsetX < 2; offsetX++) {
                                var targetX = megaX * 2 + offsetX;
                                var targetY = megaY * 2 + offsetY;
                                var sourceX = sourceMegaX * 2 + offsetX;
                                var sourceY = sourceMegaY * 2 + offsetY;
                                if (!baseLevel.ContainsPosition(targetX, targetY)
                                    || !sourceLevel.ContainsPosition(sourceX, sourceY))
                                    continue;
                                var targetPieceCell = targetY * baseLevel.Width + targetX;
                                var sourcePieceCell = sourceY * sourceLevel.Width + sourceX;
                                if (nativePieceCollisionCells.Remove(targetPieceCell))
                                    blocked.Remove(targetPieceCell);
                                if (nativePieceMissileBlockedCells.Remove(targetPieceCell))
                                    missileBlocked.Remove(targetPieceCell);
                                var pieceId = sourceLevel.NativePieceLayer[sourcePieceCell];
                                pieceLayer[targetPieceCell] = pieceId;
                                if (!sourceLevel.NativePieceProperties.TryGetValue(
                                        pieceId,
                                        out var pieceFlags)
                                    && !nativePieceProperties.TryGetValue(
                                        pieceId,
                                        out pieceFlags))
                                    continue;
                                if (nativePieceProperties.TryGetValue(
                                        pieceId,
                                        out var existingPieceFlags)
                                    && existingPieceFlags != pieceFlags) {
                                    throw new InvalidDataException(
                                        $"World mutation {mutation.MutationId} redefines d-piece "
                                        + $"{pieceId} with different collision flags.");
                                }
                                nativePieceProperties[pieceId] = pieceFlags;
                                if (pieceFlags.HasFlag(AuthoritativeDunTileFlags.Solid)) {
                                    blocked.Add(targetPieceCell);
                                    nativePieceCollisionCells.Add(targetPieceCell);
                                }
                                if (pieceFlags.HasFlag(AuthoritativeDunTileFlags.BlockMissile)) {
                                    missileBlocked.Add(targetPieceCell);
                                    nativePieceMissileBlockedCells.Add(targetPieceCell);
                                }
                            }
                        }
                    }
                }
            }
            projectedLevels.Add(
                baseLevel.LevelId,
                new AuthoritativeLevel(
                    baseLevel.LevelId,
                    baseLevel.Width,
                    baseLevel.Height,
                    blocked,
                    missileBlocked,
                    baseLevel.IsDefaultStart,
                    baseLevel.SuppressMultiplayerDeathDrops,
                    transparency,
                    objectLayer,
                    megaTileLayer,
                    pieceLayer,
                    nativePieceProperties,
                    nativePieceCollisionCells,
                    nativePieceMissileBlockedCells,
                    monsterLayer,
                    baseLevel.StartPositionX,
                    baseLevel.StartPositionY,
                    baseLevel.HasExplicitNativeVisionBlockedCells
                        ? baseLevel.NativeVisionBlockedCells
                        : null) {
                    StableSymbol = baseLevel.StableSymbol,
                });
        }

        ReconcileNativeDunObjects(allowMissingNativeDunLayers);
    }

    /** Resolves an authored mutation origin from a fixed object, encounter, or set-level origin. */
    private (int X, int Y)? ResolveMutationOrigin(AuthoritativeWorldMutation mutation)
    {
        if (mutation.AnchorEntityId != 0) {
            if (!Objects.TryGetValue(mutation.AnchorEntityId, out var anchoredObject))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references missing anchor entity {mutation.AnchorEntityId}.");
            return (anchoredObject.PositionX, anchoredObject.PositionY);
        }

        if (mutation.AnchorSetLevelId >= 0) {
            if (setLevelOriginResolver is null
                || setLevelOriginResolver(mutation.AnchorSetLevelId) is not { } origin)
                return null;
            if (origin.LevelId != mutation.LevelId)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} set-level anchor {mutation.AnchorSetLevelId} "
                    + $"is on level {origin.LevelId}, expected {mutation.LevelId}.");
            return (origin.PositionX, origin.PositionY);
        }

        if (mutation.AnchorProceduralQuestPieceOriginId != 0) {
            if (proceduralQuestPieceOriginResolver is null
                || proceduralQuestPieceOriginResolver(mutation.AnchorProceduralQuestPieceOriginId)
                    is not { } origin)
                return null;
            if (origin.LevelId != mutation.LevelId)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} procedural quest-piece origin "
                    + $"{mutation.AnchorProceduralQuestPieceOriginId} is on level {origin.LevelId}, "
                    + $"expected {mutation.LevelId}.");
            return (origin.PositionX, origin.PositionY);
        }

        if (mutation.AnchorUniqueIdentity.Length == 0)
            return (0, 0);

        var matches = CombatTargets.Values
            .Where(target => target.IsUnique
                && string.Equals(
                    target.UniqueIdentity,
                    mutation.AnchorUniqueIdentity,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException(
                $"World mutation {mutation.MutationId} unique anchor '{mutation.AnchorUniqueIdentity}' "
                + $"must match exactly one encounter; found {matches.Length}.");
        if (matches[0].LevelId != mutation.LevelId)
            throw new InvalidDataException(
                $"World mutation {mutation.MutationId} unique anchor '{mutation.AnchorUniqueIdentity}' "
                + $"is on level {matches[0].LevelId}, expected {mutation.LevelId}.");
        return (matches[0].PositionX, matches[0].PositionY);
    }

    /** Reconciles mapped native object entities with the currently projected object layer. */
    public void ReconcileNativeDunObjects(bool allowMissingLayers = false)
    {
        if (nativeDunObjects is null)
            return;

        foreach (var definition in nativeDunObjects.Definitions) {
            var levelId = definition.GetRuntimeLevelId();
            if (!projectedLevels.TryGetValue(levelId, out var level))
                continue;
            if (level.NativeObjectLayer.Count == 0 && allowMissingLayers)
                continue;
            for (var cell = 0; cell < level.NativeObjectLayer.Count; cell++) {
                if (level.NativeObjectLayer[cell] == definition.NativeObjectId)
                    continue;
                var entityId = definition.TargetKind
                    == AuthoritativeNativeDunObjectTargetKind.SetLevel
                    ? AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(
                        definition.SetLevelId,
                        definition.ActiveMap,
                        cell)
                    : AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(
                        definition.LevelId,
                        cell);
                Objects.Remove(entityId);
            }
        }

        var materialized = nativeDunObjects.LoadAvailableObjects(
            projectedLevels.Values,
            Objects.Values.ToArray(),
            nativeObjectTemplates,
            preserveExisting: true,
            allowMissingLayers: allowMissingLayers);
        foreach (var @object in materialized) {
            if (!Objects.TryAdd(@object.EntityId, @object))
                throw new InvalidDataException(
                    $"Native DUN object entity {@object.EntityId} is registered more than once.");
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

        return DeterministicGridLineTraversal.HasLineOfSight(
            startX,
            startY,
            endX,
            endY,
            (x, y) => IsMissileGeometryBlocked(levelId, x, y));
    }

    /** Native Elemental CheckBlock semantics: Solid geometry only, endpoint included. */
    public bool HasSolidLineOfSight(
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

        if (World is not null) {
            return projectedLevels.TryGetValue(levelId, out var level)
                && level.HasSolidLineOfSight(startX, startY, endX, endY);
        }

        var positionX = startX;
        var positionY = startY;
        for (var step = 0; step <= AuthoritativeLevel.NativeMaximumWidth
                + AuthoritativeLevel.NativeMaximumHeight; step++) {
            if (positionX == endX && positionY == endY)
                return true;

            var direction = DeterministicGridDirections.Toward(
                positionX,
                positionY,
                endX,
                endY);
            var displacement = DeterministicGridDirections.GetDisplacement(direction);
            positionX = checked(positionX + displacement.X);
            positionY = checked(positionY + displacement.Y);
            if (!IsWalkable(levelId, positionX, positionY))
                return false;
        }

        return false;
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
            || Objects.Values.Any(@object => !@object.ActivateOnOccupancy
                && !@object.Activated
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
        ValidateNativeMonsterPoolCapacity(CombatTargets.Values);
        foreach (var entry in CombatTargets) {
            ValidateEntityDictionaryKey(entry.Key, entry.Value.EntityId, "monster");
            var target = entry.Value;
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
        foreach (var entry in WorldItems) {
            ValidateEntityDictionaryKey(entry.Key, entry.Value.EntityId, "world item");
            var item = entry.Value;
            ValidatePosition(item.LevelId, item.PositionX, item.PositionY, "world item");
            if (!entityIds.Add(item.EntityId))
                throw new InvalidDataException($"Entity {item.EntityId} is registered more than once.");
        }
        foreach (var entry in Corpses) {
            ValidateEntityDictionaryKey(entry.Key, entry.Value.EntityId, "corpse");
            var corpse = entry.Value;
            ValidatePosition(corpse.LevelId, corpse.PositionX, corpse.PositionY, "corpse");
            if (!entityIds.Add(corpse.EntityId))
                throw new InvalidDataException($"Entity {corpse.EntityId} is registered more than once.");
        }
        foreach (var entry in Objects) {
            ValidateEntityDictionaryKey(entry.Key, entry.Value.EntityId, "object");
            var @object = entry.Value;
            ValidatePosition(@object.LevelId, @object.PositionX, @object.PositionY, "object");
            if (!entityIds.Add(@object.EntityId)) {
                throw new InvalidDataException(
                    $"Entity {@object.EntityId} is registered more than once.");
            }
        }
        foreach (var entry in Quests) {
            if (entry.Key != entry.Value.QuestId)
                throw new InvalidDataException(
                    $"Quest dictionary key {entry.Key} does not match quest identity {entry.Value.QuestId}.");
        }
        foreach (var entry in Projectiles) {
            ValidateEntityDictionaryKey(entry.Key, entry.Value.EntityId, "projectile");
            var projectile = entry.Value;
            if (projectile.SourceEntityId == 0
                || projectile.RemainingTicks == 0
                || !Enum.IsDefined(projectile.DamageType)
                || !Enum.IsDefined(projectile.Behavior)
                || !Enum.IsDefined(projectile.Phase)
                || !ContainsProjectilePosition(
                    projectile.Behavior,
                    projectile.Phase,
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY)
                || !ContainsProjectilePosition(
                    projectile.Behavior,
                    projectile.Phase,
                    projectile.LevelId,
                    projectile.TargetX,
                    projectile.TargetY)) {
                throw new InvalidDataException(
                    $"Projectile {projectile.EntityId} has invalid initial state.");
            }
            if (!entityIds.Add(entry.Value.EntityId))
                throw new InvalidDataException(
                    $"Entity {entry.Value.EntityId} is registered more than once.");
        }
        var portalIds = new HashSet<uint>();
        if (Portals.Count > AuthoritativePortalRules.NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"Initial world state contains more than {AuthoritativePortalRules.NativeMaximumPortalCount} portals.");
        foreach (var entry in Portals) {
            ValidatePortalState(entry.Value, entry.Key);
            if (!portalIds.Add(entry.Value.PortalId))
                throw new InvalidDataException(
                    $"Portal {entry.Value.PortalId} is registered more than once.");
            var portal = entry.Value;
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
                .Where(@object => !@object.Activated
                    && !@object.Destroyed
                    && !@object.ActivateOnOccupancy)
                .Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));
    }

    private static void ValidateNativeMonsterPoolCapacity(
        IEnumerable<AuthoritativeCombatTarget> targets)
    {
        foreach (var group in targets.GroupBy(target => target.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"Initial world state contains more than {AuthoritativeLevel.NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }
    }

    private static void ValidatePortalState(
        AuthoritativePortal portal,
        uint? expectedPortalId = null)
    {
        ArgumentNullException.ThrowIfNull(portal);
        portal.ValidateDestinationIdentity();
        if (portal.PortalId == 0)
            throw new InvalidDataException("Portal identity cannot be zero.");
        if (expectedPortalId is uint dictionaryKey && dictionaryKey != portal.PortalId)
            throw new InvalidDataException(
                $"Portal dictionary key {dictionaryKey} does not match portal identity {portal.PortalId}.");
        if (portal.Bidirectional
            ? portal.OwnerEntityId == 0 || portal.RemainingTicks == 0
            : portal.OwnerEntityId != 0 || portal.RemainingTicks != 0) {
            throw new InvalidDataException(
                $"Portal {portal.PortalId} has invalid ownership or lifetime metadata.");
        }
    }

    private static void ValidateEntityDictionaryKey(
        uint dictionaryKey,
        uint entityId,
        string entityKind)
    {
        if (dictionaryKey != entityId)
            throw new InvalidDataException(
                $"{entityKind} dictionary key {dictionaryKey} does not match entity identity {entityId}.");
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
