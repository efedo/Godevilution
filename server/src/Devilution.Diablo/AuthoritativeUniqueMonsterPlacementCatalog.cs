using Devilution.Data;

namespace Devilution.Diablo;

/** One content-owned placement for a generated unique encounter. */
public sealed record AuthoritativeUniqueMonsterPlacementDefinition(
    uint PlacementId,
    string UniqueIdentity,
    uint MonsterId,
    uint LevelId,
    uint EntityId,
    int PositionX,
    int PositionY,
    uint Seed,
    int Direction = 0)
{
    /** Allows a placement on a level outside the monster type's normal range. */
    public bool IgnoreDungeonLevelRange { get; init; }
}

/** Validated authored placement contract kept separate from unique identity selectors. */
public sealed class AuthoritativeUniqueMonsterPlacementCatalog
{
    private readonly IReadOnlyList<AuthoritativeUniqueMonsterPlacementDefinition> definitions;

    public AuthoritativeUniqueMonsterPlacementCatalog(
        IEnumerable<AuthoritativeUniqueMonsterPlacementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition => definition.PlacementId == 0
                || string.IsNullOrWhiteSpace(definition.UniqueIdentity)
                || definition.UniqueIdentity.Length > 128
                || definition.MonsterId == 0
                || definition.LevelId == 0
                || definition.LevelId > int.MaxValue
                || definition.EntityId == 0
                || definition.EntityId >= AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId
                || definition.Direction is < 0 or >= 8)
            || values.GroupBy(definition => definition.PlacementId)
                .Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.UniqueIdentity, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.EntityId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Unique monster placements contain invalid or duplicate identities.");
        }
        this.definitions = values
            .OrderBy(definition => definition.PlacementId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeUniqueMonsterPlacementDefinition> Definitions
        => definitions;

    /** Validates every placement against the active identity, type, and world catalogs. */
    public void Validate(
        AuthoritativeWorld world,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeUniqueMonsterCatalog uniqueMonsters,
        IEnumerable<AuthoritativeCombatTarget>? existingTargets = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        var occupied = (existingTargets ?? [])
            .Select(target => (target.LevelId, target.PositionX, target.PositionY))
            .ToHashSet();
        var entityIds = (existingTargets ?? [])
            .Select(target => target.EntityId)
            .ToHashSet();
        foreach (var placement in Definitions) {
            if (!entityIds.Add(placement.EntityId))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} reuses an existing monster entity ID.");
            if (!world.ContainsLevel(placement.LevelId))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} references unknown level {placement.LevelId}.");
            if (!world.ContainsPosition(
                    placement.LevelId,
                    placement.PositionX,
                    placement.PositionY)
                || !world.IsWalkable(
                    placement.LevelId,
                    placement.PositionX,
                    placement.PositionY)) {
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} contains a blocked or out-of-bounds cell.");
            }
            if (!monsterTypes.TryGet(placement.MonsterId, out var monsterType))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} references unknown monster {placement.MonsterId}.");
            var identity = uniqueMonsters.Definitions.SingleOrDefault(definition =>
                string.Equals(
                    definition.Identity,
                    placement.UniqueIdentity,
                    StringComparison.Ordinal));
            if (identity is null)
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} references unknown identity '{placement.UniqueIdentity}'.");
            if (identity.EntityId != 0)
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} must reference a generated unique identity.");
            if (identity.MonsterId != 0 && identity.MonsterId != placement.MonsterId)
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} does not match its unique monster selector.");
            if (identity.NativeAi.Length != 0
                && !string.Equals(
                    identity.NativeAi,
                    monsterType.NativeAi,
                    StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} does not match its native AI selector.");
            }
            if (!placement.IgnoreDungeonLevelRange
                && (monsterType.MinimumDungeonLevel > (int)placement.LevelId
                    || monsterType.MaximumDungeonLevel < (int)placement.LevelId)) {
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} is outside the monster's dungeon-level range.");
            }
            if (!occupied.Add((placement.LevelId, placement.PositionX, placement.PositionY)))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} overlaps another encounter cell.");
        }
    }

    /** Materializes validated placements through the same planner as ordinary summons. */
    public IReadOnlyList<AuthoritativeCombatTarget> Materialize(
        AuthoritativeMonsterSpawnPlanner planner,
        AuthoritativeUniqueMonsterCatalog uniqueMonsters,
        bool multiplayer)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        return Definitions.Select(placement => {
            var target = planner.CreateNativeMonsterSummon(
                placement.LevelId,
                placement.MonsterId,
                placement.Seed,
                placement.PositionX,
                placement.PositionY,
                placement.EntityId,
                multiplayer,
                placement.Direction,
                placement.IgnoreDungeonLevelRange);
            if (target is null)
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} could not materialize its monster target.");
            target.IsUnique = true;
            uniqueMonsters.ApplyToExplicit(target, placement.UniqueIdentity);
            return target;
        }).ToArray();
    }

    public static AuthoritativeUniqueMonsterPlacementCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeUniqueMonsterPlacementCatalog(table.Rows.Select(row =>
            new AuthoritativeUniqueMonsterPlacementDefinition(
                row.RequiredUInt32("placement_id"),
                row.Required("unique_identity"),
                AuthoritativeMonsterIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                row.RequiredUInt32("entity_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredUInt32("seed"),
                row.OptionalInt32("direction")) {
                IgnoreDungeonLevelRange = row.OptionalBoolean("ignore_dungeon_level_range"),
            }));
    }
}
