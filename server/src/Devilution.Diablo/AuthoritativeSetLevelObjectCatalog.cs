using Devilution.Data;

namespace Devilution.Diablo;

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
                || placement.PositionX is < 0 or > 255
                || placement.PositionY is < 0 or > 255
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

    public IReadOnlyList<AuthoritativeSetLevelObjectPlacement> GetForSetLevel(int setLevelId)
        => definitions.Where(placement => placement.SetLevelId == setLevelId).ToArray();

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
        IEnumerable<AuthoritativeWorldObject>? templates = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var usedEntityIds = (existingObjects ?? [])
            .Select(@object => @object.EntityId)
            .ToHashSet();
        var templateByEntityId = (templates ?? [])
            .ToDictionary(@object => @object.EntityId);
        var objects = new List<AuthoritativeWorldObject>();
        foreach (var placement in definitions) {
            var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                placement.SetLevelId,
                activeMap: true);
            if (!world.ContainsLevel(levelId))
                continue;
            if (!world.ContainsPosition(levelId, placement.PositionX, placement.PositionY))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} is outside runtime level {levelId}.");

            var entityId = checked(RuntimeObjectEntityIdBase + placement.PlacementId);
            if (!usedEntityIds.Add(entityId))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} collides with entity {entityId}.");
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
                    placement.PositionX,
                    placement.PositionY));
            } else {
                objects.Add(new AuthoritativeWorldObject(
                    entityId,
                    placement.ObjectId,
                    levelId,
                    placement.PositionX,
                    placement.PositionY));
            }
        }
        return objects;
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
                TemplateEntityId = row.OptionalUInt32("template_entity_id"),
            }));
    }
}
