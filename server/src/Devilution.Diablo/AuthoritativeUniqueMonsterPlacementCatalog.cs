using Devilution.Data;
using Devilution.Domain.Protocol;
using Devilution.Engine;

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

    /** Optional quest whose availability gates this native placement. */
    public uint RequiredQuestId { get; init; }
}

/** Validated authored placement contract kept separate from unique identity selectors. */
public sealed class AuthoritativeUniqueMonsterPlacementCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "placement_id",
        "unique_identity",
        "monster_id",
        "monster_symbol",
        "level_id",
        "level_symbol",
        "entity_id",
        "position_x",
        "position_y",
        "seed",
        "direction",
        "ignore_dungeon_level_range",
        "required_quest_id",
        "required_quest_symbol",
    ];

    private readonly IReadOnlyList<AuthoritativeUniqueMonsterPlacementDefinition> definitions;

    public AuthoritativeUniqueMonsterPlacementCatalog(
        IEnumerable<AuthoritativeUniqueMonsterPlacementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition => definition.PlacementId == 0
                || string.IsNullOrWhiteSpace(definition.UniqueIdentity)
                || !NativeTextLimits.IsIdentityWithinLimit(definition.UniqueIdentity)
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

    /** Returns placements admitted by the active quest/content state. */
    public AuthoritativeUniqueMonsterPlacementCatalog Filter(
        Func<AuthoritativeUniqueMonsterPlacementDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new AuthoritativeUniqueMonsterPlacementCatalog(definitions.Where(predicate));
    }

    /** Validates every placement against the active identity, type, and world catalogs. */
    public void Validate(
        AuthoritativeWorld world,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeUniqueMonsterCatalog uniqueMonsters,
        IEnumerable<AuthoritativeCombatTarget>? existingTargets = null,
        IEnumerable<(uint LevelId, int X, int Y)>? reservedPositions = null,
        IEnumerable<uint>? reservedEntityIds = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        var targets = (existingTargets ?? []).ToArray();
        var occupied = (reservedPositions ?? [])
            .Select(position => (position.LevelId, position.X, position.Y))
            .ToHashSet();
        occupied.UnionWith(
            targets.Select(target => (target.LevelId, target.PositionX, target.PositionY)));
        var entityIds = (reservedEntityIds ?? [])
            .ToHashSet();
        entityIds.UnionWith(targets.Select(target => target.EntityId));
        foreach (var placement in Definitions) {
            if (!entityIds.Add(placement.EntityId))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} reuses an existing entity ID.");
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
        bool multiplayer,
        AuthoritativeUniqueMonsterPackCatalog? packs = null,
        IEnumerable<AuthoritativeCombatTarget>? existingTargets = null,
        IEnumerable<(uint LevelId, int X, int Y)>? reservedPositions = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(uniqueMonsters);
        var targets = (existingTargets ?? []).ToList();
        var occupied = (reservedPositions ?? [])
            .Select(position => (position.LevelId, position.X, position.Y))
            .ToHashSet();
        occupied.UnionWith(targets.Select(target => (
            target.LevelId,
            target.PositionX,
            target.PositionY)));
        var usedEntityIds = targets
            .Select(target => target.EntityId)
            .ToHashSet();
        foreach (var target in targets) {
            if (target.Drop is not null)
                usedEntityIds.Add(target.Drop.EntityId);
        }
        var generatedEntityId = AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId;
        var materialized = new List<AuthoritativeCombatTarget>();
        foreach (var placement in Definitions) {
            var target = planner.CreateStableMonsterSummon(
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
            if (!usedEntityIds.Add(target.EntityId))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} reuses an entity ID during materialization.");
            if (target.Drop is not null
                && !usedEntityIds.Add(target.Drop.EntityId))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} reuses drop entity {target.Drop.EntityId} during materialization.");
            if (!occupied.Add((target.LevelId, target.PositionX, target.PositionY)))
                throw new InvalidDataException(
                    $"Unique placement {placement.PlacementId} overlaps an occupied cell during materialization.");
            materialized.Add(target);

            var pack = packs?.FindForPlacement(placement.PlacementId);
            if (pack is null)
                continue;
            generatedEntityId = DeterministicEntityIdAllocator.FindFirstAvailable(
                generatedEntityId,
                candidate => usedEntityIds.Contains(candidate)
                    || usedEntityIds.Contains(GetGeneratedDropEntityId(candidate)));
            var members = planner.CreateNativeMonsterPack(
                target,
                pack.MinionMonsterId,
                pack.PackSize,
                placement.Seed,
                generatedEntityId,
                multiplayer,
                pack.Kind,
                occupied);
            foreach (var member in members) {
                if (!usedEntityIds.Add(member.EntityId))
                    throw new InvalidDataException(
                        $"Unique monster pack {pack.PackId} reuses member entity {member.EntityId}.");
                var memberDrop = member.Drop;
                if (memberDrop is not null
                    && !usedEntityIds.Add(memberDrop.EntityId))
                    throw new InvalidDataException(
                        $"Unique monster pack {pack.PackId} reuses drop entity {memberDrop.EntityId}.");
                occupied.Add((member.LevelId, member.PositionX, member.PositionY));
                materialized.Add(member);
                generatedEntityId = checked(member.EntityId + 1);
            }
        }
        return materialized;
    }

    private static uint GetGeneratedDropEntityId(uint monsterEntityId)
    {
        if (monsterEntityId < AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId)
            return AuthoritativeMonsterSpawnPlanner.FirstGeneratedDropEntityId;
        return checked(AuthoritativeMonsterSpawnPlanner.FirstGeneratedDropEntityId
            + (monsterEntityId - AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId));
    }

    /** Rejects partial generated-placement metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production unique-monster placement table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("placement_id");
            _ = row.Required("unique_identity");
            _ = row.Required("monster_id");
            _ = row.Required("level_id");
            _ = row.Required("entity_id");
            _ = row.Required("position_x");
            _ = row.Required("position_y");
            _ = row.Required("seed");
        }
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
                RequiredQuestId = AuthoritativeQuestIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "required_quest_id",
                    "required_quest_symbol"),
            }));
    }
}
