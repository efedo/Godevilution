using Devilution.Data;

namespace Devilution.Diablo;

/** Selects how a set-level object's position is resolved. */
public enum AuthoritativeSetLevelObjectPlacementMode
{
    /** PositionX/PositionY are absolute cells on the materialized set-level map. */
    Absolute,

    /** PositionX/PositionY are signed offsets from a generated set-piece origin. */
    SetLevelOriginOffset,
}

/** Runtime origin supplied when a procedural set piece is generated. */
public sealed record AuthoritativeSetLevelObjectOrigin(
    uint LevelId,
    int PositionX,
    int PositionY);

/** One native fixed-object placement on a set-level map. */
public sealed record AuthoritativeSetLevelObjectPlacement(
    uint PlacementId,
    int SetLevelId,
    uint ObjectId,
    int PositionX,
    int PositionY,
    int TemplateX,
    int TemplateY,
    int TemplateWidth,
    int TemplateHeight)
{
    public string SetLevelSymbol { get; init; } = string.Empty;

    /** Resolves absolute coordinates or generated set-piece-relative offsets. */
    public AuthoritativeSetLevelObjectPlacementMode PlacementMode { get; init; }
        = AuthoritativeSetLevelObjectPlacementMode.Absolute;

    /** Optional objects.tsv entity whose authored behavior is copied to this placement. */
    public uint TemplateEntityId { get; init; }
}

/** Validated external placements for native set-level object footprints. */
public sealed class AuthoritativeSetLevelObjectCatalog
{
    /** Reserved entity-ID range for materialized native set-level objects. */
    public const uint RuntimeObjectEntityIdBase = 0x6000_0000;

    private readonly IReadOnlyList<AuthoritativeSetLevelObjectPlacement> definitions;

    public AuthoritativeSetLevelObjectCatalog(
        IEnumerable<AuthoritativeSetLevelObjectPlacement> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(placement =>
                placement.PlacementId == 0
                || placement.PlacementId > uint.MaxValue - RuntimeObjectEntityIdBase
                || placement.SetLevelId is < 0 or > 8
                || placement.ObjectId == 0
                || !Enum.IsDefined(placement.PlacementMode)
                || placement.PlacementMode == AuthoritativeSetLevelObjectPlacementMode.Absolute
                    && (placement.PositionX is < 0 or > 255 || placement.PositionY is < 0 or > 255)
                || placement.PlacementMode == AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset
                    && (placement.PositionX is < -255 or > 255 || placement.PositionY is < -255 or > 255)
                || placement.TemplateX is < 0 or > 255
                || placement.TemplateY is < 0 or > 255
                || placement.TemplateWidth is <= 0 or > 64
                || placement.TemplateHeight is <= 0 or > 64)
            || values.Select(placement => placement.PlacementId).Distinct().Count() != values.Length
            || values.GroupBy(placement => (
                    placement.SetLevelId,
                    placement.ObjectId,
                    placement.PositionX,
                    placement.PositionY))
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Set-level object placements contain invalid or duplicate values.");
        }
        this.definitions = values
            .OrderBy(placement => placement.SetLevelId)
            .ThenBy(placement => placement.PlacementId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeSetLevelObjectPlacement> Definitions
        => definitions;

    /** Validates behavior-template references before set-level geometry is loaded. */
    public void ValidateTemplates(IEnumerable<AuthoritativeWorldObject> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        IReadOnlyDictionary<uint, AuthoritativeWorldObject> templatesById;
        try {
            templatesById = templates.ToDictionary(@object => @object.EntityId);
        } catch (ArgumentException exception) {
            throw new InvalidDataException(
                "Set-level object templates contain duplicate entity IDs.",
                exception);
        }
        foreach (var placement in definitions.Where(placement => placement.TemplateEntityId != 0)) {
            if (!templatesById.TryGetValue(placement.TemplateEntityId, out var template))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} references unknown template entity {placement.TemplateEntityId}.");
            if (template.ObjectId != placement.ObjectId)
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} template {placement.TemplateEntityId} uses object {template.ObjectId}, expected {placement.ObjectId}.");
        }
    }

    public IReadOnlyList<AuthoritativeSetLevelObjectPlacement> GetForSetLevel(int setLevelId)
        => definitions.Where(placement => placement.SetLevelId == setLevelId).ToArray();

    /** Returns a validated view used when a runtime origin is published later. */
    public AuthoritativeSetLevelObjectCatalog Filter(
        Func<AuthoritativeSetLevelObjectPlacement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new AuthoritativeSetLevelObjectCatalog(definitions.Where(predicate));
    }

    /**
     * Materializes placements whose active native set-level geometry is loaded.
     * Legacy packs may carry the placement catalog before shipping map assets;
     * those rows remain metadata-only until the corresponding active level is
     * present in the authoritative world. A placement with a template entity
     * copies the authored behavior from the matching objects.tsv row.
     */
    public IReadOnlyList<AuthoritativeWorldObject> LoadAvailableObjects(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        IEnumerable<AuthoritativeWorldObject>? templates = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var existingValues = (existingObjects ?? []).ToArray();
        var usedEntityIds = existingValues
            .Select(@object => @object.EntityId)
            .ToHashSet();
        var existingObjectCountsByLevel = existingValues
            .GroupBy(@object => @object.LevelId)
            .ToDictionary(group => group.Key, group => group.Count());
        var materializedObjectCountsByLevel = new Dictionary<uint, int>();
        var templateByEntityId = (templates ?? [])
            .ToDictionary(@object => @object.EntityId);
        var objects = new List<AuthoritativeWorldObject>();
        foreach (var placement in definitions) {
            var resolved = ResolvePosition(world, placement, setLevelOrigins);
            if (resolved is null)
                continue;
            var (levelId, positionX, positionY) = resolved.Value;
            if (!world.ContainsPosition(levelId, positionX, positionY))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} is outside runtime level {levelId}.");

            var entityId = checked(RuntimeObjectEntityIdBase + placement.PlacementId);
            if (!usedEntityIds.Add(entityId))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} collides with entity {entityId}.");
            var existingLevelObjectCount = existingObjectCountsByLevel.GetValueOrDefault(levelId);
            var materializedLevelObjectCount = materializedObjectCountsByLevel.GetValueOrDefault(levelId);
            if (existingLevelObjectCount + materializedLevelObjectCount
                >= AuthoritativeLevel.NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} for level {levelId} exceeds the native "
                    + $"{AuthoritativeLevel.NativeMaximumObjectCount}-object capacity.");
            if (placement.TemplateEntityId != 0) {
                if (!templateByEntityId.TryGetValue(placement.TemplateEntityId, out var template))
                    throw new InvalidDataException(
                        $"Set-level object placement {placement.PlacementId} references unknown template entity {placement.TemplateEntityId}.");
                if (template.ObjectId != placement.ObjectId)
                    throw new InvalidDataException(
                        $"Set-level object placement {placement.PlacementId} template {placement.TemplateEntityId} uses object {template.ObjectId}, expected {placement.ObjectId}.");
                objects.Add(template.WithPlacement(
                    entityId,
                    levelId,
                    positionX,
                    positionY));
            } else {
                objects.Add(new AuthoritativeWorldObject(
                    entityId,
                    placement.ObjectId,
                    levelId,
                    positionX,
                    positionY));
            }
            materializedObjectCountsByLevel[levelId] = materializedLevelObjectCount + 1;
        }
        return objects;
    }

    private static (uint LevelId, int PositionX, int PositionY)? ResolvePosition(
        AuthoritativeWorld world,
        AuthoritativeSetLevelObjectPlacement placement,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins)
    {
        if (placement.PlacementMode == AuthoritativeSetLevelObjectPlacementMode.Absolute) {
            var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                placement.SetLevelId,
                activeMap: true);
            return world.ContainsLevel(levelId)
                ? (levelId, placement.PositionX, placement.PositionY)
                : null;
        }

        if (setLevelOrigins is null
            || !setLevelOrigins.TryGetValue(placement.SetLevelId, out var origin))
            return null;
        if (!world.ContainsLevel(origin.LevelId))
            return null;
        if (!world.ContainsPosition(origin.LevelId, origin.PositionX, origin.PositionY))
            throw new InvalidDataException(
                $"Set-level object placement {placement.PlacementId} has an origin outside runtime level {origin.LevelId}.");
        var positionX = checked(origin.PositionX + placement.PositionX);
        var positionY = checked(origin.PositionY + placement.PositionY);
        return (origin.LevelId, positionX, positionY);
    }

    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var placement in definitions) {
            if (!setLevels.TryGet(placement.SetLevelId, out _))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} references unknown set level {placement.SetLevelId}.");
        }
    }

    public static AuthoritativeSetLevelObjectCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSetLevelObjectCatalog(table.Rows.Select(row =>
            new AuthoritativeSetLevelObjectPlacement(
                row.RequiredUInt32("placement_id"),
                AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "set_level_id",
                    "set_level_symbol"),
                row.RequiredUInt32("object_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("template_x"),
                row.RequiredInt32("template_y"),
                row.RequiredInt32("template_width"),
                row.RequiredInt32("template_height")) {
                SetLevelSymbol = row.TryGet("set_level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
                PlacementMode = ParsePlacementMode(row),
                TemplateEntityId = row.OptionalUInt32("template_entity_id"),
            }));
    }

    private static AuthoritativeSetLevelObjectPlacementMode ParsePlacementMode(TsvRow row)
    {
        if (!row.TryGet("placement_mode", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeSetLevelObjectPlacementMode.Absolute;
        return value.Trim().ToLowerInvariant() switch {
            "absolute" => AuthoritativeSetLevelObjectPlacementMode.Absolute,
            "set_level_origin" or "set_level_origin_offset"
                => AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            _ => throw new InvalidDataException(
                $"Column 'placement_mode' on TSV row {row.LineNumber} has an unknown value '{value}'."),
        };
    }
}
