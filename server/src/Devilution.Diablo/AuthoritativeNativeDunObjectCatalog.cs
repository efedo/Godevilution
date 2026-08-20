using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit mapping from a native DUN object-layer value to an authored object. */
public sealed record AuthoritativeNativeDunObjectDefinition(
    int SetLevelId,
    bool ActiveMap,
    ushort NativeObjectId,
    uint ObjectId,
    uint TemplateEntityId)
{
    public string SetLevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunObjectDefinition Validate()
    {
        if (SetLevelId is < 0 or > 8
            || NativeObjectId == 0
            || ObjectId == 0) {
            throw new InvalidDataException(
                "Native DUN object definitions contain an invalid set-level or object identity.");
        }
        return this;
    }
}

/** Validated mappings that materialize native DUN object layers authoritatively. */
public sealed class AuthoritativeNativeDunObjectCatalog
{
    /** Reserved entity-ID range for objects discovered in native DUN layers. */
    public const uint RuntimeObjectEntityIdBase = 0x6800_0000;

    private const int RuntimeLevelCellStride = 80 * 80;

    private readonly IReadOnlyList<AuthoritativeNativeDunObjectDefinition> definitions;

    public AuthoritativeNativeDunObjectCatalog(
        IEnumerable<AuthoritativeNativeDunObjectDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .ToArray();
        if (values.GroupBy(definition => (
                definition.SetLevelId,
                definition.ActiveMap,
                definition.NativeObjectId))
            .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Native DUN object definitions contain duplicate layer mappings.");
        }
        this.definitions = values
            .OrderBy(definition => definition.SetLevelId)
            .ThenBy(definition => definition.ActiveMap)
            .ThenBy(definition => definition.NativeObjectId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeNativeDunObjectDefinition> Definitions
        => definitions;

    /**
     * Materializes every explicitly mapped nonzero native DUN object cell in a
     * loaded set-level variant. Unmapped native IDs remain inert and are not
     * guessed as gameplay objects.
     */
    public IReadOnlyList<AuthoritativeWorldObject> LoadAvailableObjects(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        IEnumerable<AuthoritativeWorldObject>? templates = null,
        bool preserveExisting = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        return LoadAvailableObjects(world.Levels, existingObjects, templates, preserveExisting);
    }

    /** Materializes mapped objects from an already projected level collection. */
    public IReadOnlyList<AuthoritativeWorldObject> LoadAvailableObjects(
        IEnumerable<AuthoritativeLevel> levels,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        IEnumerable<AuthoritativeWorldObject>? templates = null,
        bool preserveExisting = false)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var existingValues = (existingObjects ?? []).ToArray();
        var templateValues = (templates ?? []).ToArray();
        var usedEntityIds = existingValues
            .Select(@object => @object.EntityId)
            .ToHashSet();
        var existingByEntityId = existingValues
            .ToDictionary(@object => @object.EntityId);
        var templateByEntityId = templateValues
            .ToDictionary(@object => @object.EntityId);
        var levelsById = levels.ToDictionary(level => level.LevelId);
        var objects = new List<AuthoritativeWorldObject>();
        foreach (var definition in definitions) {
            var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                definition.SetLevelId,
                definition.ActiveMap);
            if (!levelsById.TryGetValue(levelId, out var level))
                continue;
            if (level.NativeObjectLayer.Count == 0)
                throw new InvalidDataException(
                    $"Native DUN object mapping for set level {definition.SetLevelId} requires a native object layer.");
            if (level.NativeObjectLayer.Count != level.Width * level.Height)
                throw new InvalidDataException(
                    $"Native DUN object layer for level {levelId} has an invalid length.");

            for (var cell = 0; cell < level.NativeObjectLayer.Count; cell++) {
                if (level.NativeObjectLayer[cell] != definition.NativeObjectId)
                    continue;
                var positionX = cell % level.Width;
                var positionY = cell / level.Width;
                var entityId = GetRuntimeEntityId(
                    definition.SetLevelId,
                    definition.ActiveMap,
                    cell);
                if (!usedEntityIds.Add(entityId)) {
                    if (preserveExisting
                        && existingByEntityId.TryGetValue(entityId, out var existing)
                        && existing.LevelId == levelId
                        && existing.PositionX == positionX
                        && existing.PositionY == positionY
                        && existing.ObjectId == definition.ObjectId) {
                        continue;
                    }
                    throw new InvalidDataException(
                        $"Native DUN object mapping collides with entity {entityId}.");
                }
                if (definition.TemplateEntityId != 0) {
                    if (!templateByEntityId.TryGetValue(
                            definition.TemplateEntityId,
                            out var template)) {
                        throw new InvalidDataException(
                            $"Native DUN object mapping for set level {definition.SetLevelId} references unknown template entity {definition.TemplateEntityId}.");
                    }
                    if (template.ObjectId != definition.ObjectId)
                        throw new InvalidDataException(
                            $"Native DUN object mapping for set level {definition.SetLevelId} template {definition.TemplateEntityId} uses object {template.ObjectId}, expected {definition.ObjectId}.");
                    objects.Add(template.WithPlacement(
                        entityId,
                        levelId,
                        positionX,
                        positionY));
                } else {
                    objects.Add(new AuthoritativeWorldObject(
                        entityId,
                        definition.ObjectId,
                        levelId,
                        positionX,
                        positionY));
                }
            }
        }
        return objects;
    }

    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var definition in definitions) {
            if (!setLevels.TryGet(definition.SetLevelId, out _))
                throw new InvalidDataException(
                    $"Native DUN object definition {definition.NativeObjectId} references an unknown set level.");
        }
    }

    public static uint GetRuntimeEntityId(
        int setLevelId,
        bool activeMap,
        int cell)
    {
        if (setLevelId is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(setLevelId));
        if (cell is < 0 or >= RuntimeLevelCellStride)
            throw new ArgumentOutOfRangeException(nameof(cell));
        return checked(RuntimeObjectEntityIdBase
            + ((uint)(setLevelId * 2) + (activeMap ? 1U : 0U)) * (uint)RuntimeLevelCellStride
            + (uint)cell);
    }

    public static AuthoritativeNativeDunObjectCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunObjectCatalog(table.Rows.Select(row =>
            CreateDefinition(row, stableIds)));
    }

    private static AuthoritativeNativeDunObjectDefinition CreateDefinition(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var nativeObjectId = row.RequiredUInt32("native_object_id");
        if (nativeObjectId is 0 or > ushort.MaxValue)
            throw new InvalidDataException(
                $"Native DUN object row {row.LineNumber} contains an invalid native object ID.");
        return new AuthoritativeNativeDunObjectDefinition(
            AuthoritativeSetLevelIdentity.ResolveOptional(
                row,
                stableIds,
                "set_level_id",
                "set_level_symbol"),
            row.RequiredBoolean("active_map"),
            (ushort)nativeObjectId,
            row.RequiredUInt32("object_id"),
            row.OptionalUInt32("template_entity_id")) {
            SetLevelSymbol = row.TryGet("set_level_symbol", out var symbol)
                ? symbol
                : string.Empty,
        };
    }
}
