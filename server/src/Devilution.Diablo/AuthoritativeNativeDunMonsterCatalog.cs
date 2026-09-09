using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Mapping from a raw native DUN monster-layer value to a native monster identity. */
public sealed record AuthoritativeNativeDunMonsterDefinition(
    ushort NativeDunMonsterId,
    uint NativeMonsterId,
    uint MonsterId)
{
    /** Optional stable monster symbol retained for composed-pack diagnostics. */
    public string MonsterSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunMonsterDefinition Validate()
    {
        if (NativeDunMonsterId == 0
            || NativeDunMonsterId > AuthoritativeLevel.NativeMaximumDunMonsterLayerId
            || NativeMonsterId > AuthoritativeLevel.NativeMaximumMonsterTypeId
            || MonsterId == 0) {
            throw new InvalidDataException(
                "Native DUN monster mappings contain an invalid raw or native identity.");
        }
        return this;
    }
}

/** Validated external native DUN monster-layer mappings.
 *
 * The native layer stores an index into DevilutionX's MonstConvTbl rather than
 * an authoritative monster identity. This catalog externalizes that conversion
 * and provides deterministic authoritative placement when geometry is ready.
 */
public sealed class AuthoritativeNativeDunMonsterCatalog
{
    /** Reserved entity range for monsters discovered in native DUN layers. */
    public const uint RuntimeMonsterEntityIdBase = 0x5000_0000;

    private readonly IReadOnlyDictionary<ushort, AuthoritativeNativeDunMonsterDefinition>
        definitions;

    public AuthoritativeNativeDunMonsterCatalog(
        IEnumerable<AuthoritativeNativeDunMonsterDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .ToArray();
        if (values.GroupBy(definition => definition.NativeDunMonsterId)
            .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Native DUN monster mappings contain duplicate raw layer identities.");
        }
        this.definitions = values.ToDictionary(
            definition => definition.NativeDunMonsterId);
    }

    public IReadOnlyList<AuthoritativeNativeDunMonsterDefinition> Definitions
        => definitions.Values
            .OrderBy(definition => definition.NativeDunMonsterId)
            .ToArray();

    public bool TryGet(
        ushort nativeDunMonsterId,
        out AuthoritativeNativeDunMonsterDefinition definition)
        => definitions.TryGetValue(nativeDunMonsterId, out definition!);

    public bool TryGetNativeMonsterId(ushort nativeDunMonsterId, out uint nativeMonsterId)
    {
        if (!definitions.TryGetValue(nativeDunMonsterId, out var definition)) {
            nativeMonsterId = 0;
            return false;
        }
        nativeMonsterId = definition.NativeMonsterId;
        return true;
    }

    public bool TryGetMonsterId(ushort nativeDunMonsterId, out uint monsterId)
    {
        if (!definitions.TryGetValue(nativeDunMonsterId, out var definition)) {
            monsterId = 0;
            return false;
        }
        monsterId = definition.MonsterId;
        return true;
    }

    /**
     * Materializes mapped nonzero DUN cells in native row-major placement order.
     * Unmapped conversion entries remain inert; mapped cells fail closed when
     * their level, geometry, occupancy, or authoritative type is invalid.
     */
    public IReadOnlyList<AuthoritativeCombatTarget> Materialize(
        AuthoritativeWorld world,
        AuthoritativeMonsterSpawnPlanner planner,
        IEnumerable<AuthoritativeCombatTarget>? existingTargets = null,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        uint seed = 0,
        bool multiplayer = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planner);
        var targets = (existingTargets ?? []).ToArray();
        var objects = (existingObjects ?? []).ToArray();
        var existingByCell = targets
            .GroupBy(target => (target.LevelId, target.PositionX, target.PositionY))
            .ToDictionary(group => group.Key, group => {
                var values = group.ToArray();
                if (values.Length != 1)
                    throw new InvalidDataException(
                        $"Native DUN monster cell {group.Key} contains duplicate targets.");
                return values[0];
            });
        var occupiedCells = objects
            .Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY))
            .ToHashSet();
        var usedEntityIds = targets.Select(target => target.EntityId)
            .Concat(objects.Select(@object => @object.EntityId))
            .ToHashSet();
        var entityIds = new DeterministicEntityIdAllocator(RuntimeMonsterEntityIdBase);
        var materialized = new List<AuthoritativeCombatTarget>();
        foreach (var level in world.Levels.OrderBy(level => level.LevelId)) {
            if (level.NativeMonsterLayer.Count == 0)
                continue;
            if (level.NativeMonsterLayer.Count != level.Width * level.Height)
                throw new InvalidDataException(
                    $"Native DUN monster layer for level {level.LevelId} has an invalid length.");
            var existingLevelMonsterCount = targets.Count(target => target.LevelId == level.LevelId);
            var materializedLevelMonsterCount = 0;
            for (var cell = 0; cell < level.NativeMonsterLayer.Count; cell++) {
                var nativeDunMonsterId = level.NativeMonsterLayer[cell];
                if (nativeDunMonsterId == 0
                    || !definitions.TryGetValue(nativeDunMonsterId, out var mapping))
                    continue;
                var positionX = cell % level.Width;
                var positionY = cell / level.Width;
                var cellKey = (level.LevelId, positionX, positionY);
                if (existingByCell.TryGetValue(cellKey, out var existing)) {
                    if (existing.MonsterId != mapping.MonsterId)
                        throw new InvalidDataException(
                            $"Native DUN monster cell {cellKey} conflicts with monster {existing.MonsterId}.");
                    continue;
                }
                if (!level.IsWalkable(positionX, positionY)
                    || occupiedCells.Contains(cellKey))
                    throw new InvalidDataException(
                        $"Native DUN monster cell {cellKey} is not available for authoritative placement.");
                if (existingLevelMonsterCount + materializedLevelMonsterCount
                    >= AuthoritativeLevel.NativeMaximumMonsterCount) {
                    throw new InvalidDataException(
                        $"Native DUN monster materialization for level {level.LevelId} "
                        + $"exceeds the native {AuthoritativeLevel.NativeMaximumMonsterCount}-monster capacity.");
                }
                var entityId = entityIds.Allocate(usedEntityIds.Contains);
                var cellSeed = CreateCellSeed(seed, level.LevelId, cell, nativeDunMonsterId);
                var target = planner.CreateNativeMonsterSummon(
                    level.LevelId,
                    mapping.NativeMonsterId,
                    cellSeed,
                    positionX,
                    positionY,
                    entityId,
                    multiplayer,
                    // Native DUN layers already carry the authored encounter
                    // selection. Runtime level IDs are presentation/route
                    // identities and may not equal the native dungeon depth
                    // used by the monster range table (for example Hell's
                    // level-13 Warlord room runs on runtime level 4).
                    ignoreDungeonLevelRange: true,
                    stableMonsterId: mapping.MonsterId);
                if (target is null)
                    throw new InvalidDataException(
                        $"Native DUN monster mapping {nativeDunMonsterId} cannot materialize "
                        + $"monster {mapping.MonsterId} on level {level.LevelId}.");
                materialized.Add(target);
                usedEntityIds.Add(entityId);
                existingByCell.Add(cellKey, target);
                materializedLevelMonsterCount++;
            }
        }
        return materialized;
    }

    /** Fails activation if a mapping points at an absent or mismatched monster type. */
    public void ValidateMonsterTypes(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var definition in Definitions) {
            if (!monsterTypes.TryGet(definition.MonsterId, out var monsterType)
                || monsterType.NativeMonsterId != definition.NativeMonsterId
                || definition.MonsterSymbol.Length != 0
                    && !string.Equals(
                        definition.MonsterSymbol,
                        monsterType.StableSymbol,
                        StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Native DUN monster mapping {definition.NativeDunMonsterId} references "
                    + $"incompatible monster {definition.MonsterId} for native monster "
                    + $"{definition.NativeMonsterId} or stable symbol '{definition.MonsterSymbol}'.");
        }
    }

    public static AuthoritativeNativeDunMonsterCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunMonsterCatalog(table.Rows.Select(row => {
            var rawId = row.RequiredUInt32("native_dun_monster_id");
            if (rawId > ushort.MaxValue)
                throw new InvalidDataException(
                    "Native DUN monster mappings contain an invalid raw layer identity.");
            return new AuthoritativeNativeDunMonsterDefinition(
                (ushort)rawId,
                row.RequiredUInt32("native_monster_id"),
                AuthoritativeMonsterIdentity.Resolve(row, stableIds)) {
                MonsterSymbol = AuthoritativeMonsterIdentity.ReadSymbol(row),
            };
        }));
    }

    private static uint CreateCellSeed(
        uint seed,
        uint levelId,
        int cell,
        ushort nativeDunMonsterId)
    {
        var mixed = seed
            ^ unchecked(levelId * 0x9E3779B9U)
            ^ unchecked((uint)cell * 0x85EBCA6BU)
            ^ unchecked((uint)nativeDunMonsterId * 0xC2B2AE35U);
        return mixed == 0 ? 1U : mixed;
    }
}
