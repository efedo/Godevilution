using Devilution.Data;

namespace Devilution.Diablo;

/** Runtime level target kind for one native DUN object mapping. */
public enum AuthoritativeNativeDunObjectTargetKind
{
    SetLevel,
    Level,
}

/** Explicit mapping from a native DUN object-layer value to an authored object. */
public sealed record AuthoritativeNativeDunObjectDefinition(
    int SetLevelId,
    bool ActiveMap,
    ushort NativeObjectId,
    uint ObjectId,
    uint TemplateEntityId)
{
    private const uint OrdinaryEntityIdBase = 0x6900_0000;

    public string SetLevelSymbol { get; init; } = string.Empty;

    /** Stable authored-object symbol retained for composed-pack diagnostics. */
    public string ObjectSymbol { get; init; } = string.Empty;

    /** Selects the legacy set-level target or an ordinary runtime level target. */
    public AuthoritativeNativeDunObjectTargetKind TargetKind { get; init; }
        = AuthoritativeNativeDunObjectTargetKind.SetLevel;

    /** Ordinary runtime level identity when TargetKind is Level. */
    public uint LevelId { get; init; }

    /** Stable ordinary-level symbol retained for diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunObjectDefinition Validate()
    {
        if (!Enum.IsDefined(TargetKind)
            || NativeObjectId == 0
            || NativeObjectId > AuthoritativeLevel.NativeMaximumDunObjectLayerId
            || ObjectId == 0
            || (TargetKind == AuthoritativeNativeDunObjectTargetKind.SetLevel
                && SetLevelId is < 0 or > 8)
            || (TargetKind == AuthoritativeNativeDunObjectTargetKind.Level
                && LevelId > (uint.MaxValue - OrdinaryEntityIdBase)
                    / (80U * 80U))) {
            throw new InvalidDataException(
                "Native DUN object definitions contain an invalid level or object identity.");
        }
        return this;
    }

    public uint GetRuntimeLevelId()
    {
        return TargetKind == AuthoritativeNativeDunObjectTargetKind.SetLevel
            ? AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(SetLevelId, ActiveMap)
            : LevelId;
    }
}

/** Validated mappings that materialize native DUN object layers authoritatively. */
public sealed class AuthoritativeNativeDunObjectCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "set_level_id",
        "set_level_symbol",
        "level_id",
        "level_symbol",
        "active_map",
        "native_object_id",
        "object_id",
        "object_symbol",
        "template_entity_id",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-DUN object table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("native_object_id");
            _ = row.RequiredUInt32("object_id");
        }
    }

    /** Reserved entity-ID range for objects discovered in native DUN layers. */
    public const uint RuntimeObjectEntityIdBase = 0x6800_0000;

    /** Reserved entity-ID range for objects discovered in ordinary level DUN layers. */
    public const uint RuntimeLevelObjectEntityIdBase = 0x6900_0000;

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
                definition.TargetKind,
                definition.SetLevelId,
                definition.ActiveMap,
                definition.LevelId,
                definition.NativeObjectId))
            .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Native DUN object definitions contain duplicate layer mappings.");
        }
        this.definitions = values
            .OrderBy(definition => definition.TargetKind)
            .ThenBy(definition => definition.SetLevelId)
            .ThenBy(definition => definition.ActiveMap)
            .ThenBy(definition => definition.LevelId)
            .ThenBy(definition => definition.NativeObjectId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeNativeDunObjectDefinition> Definitions
        => definitions;

    /** Fails activation when an ordinary-level mapping targets an unknown level. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var definition in definitions.Where(definition =>
                     definition.TargetKind == AuthoritativeNativeDunObjectTargetKind.Level)) {
            if (!world.ContainsLevel(definition.LevelId))
                throw new InvalidDataException(
                    $"Native DUN object definition {definition.NativeObjectId} references an unknown level {definition.LevelId}.");
        }
    }

    /** Validates behavior-template references before native geometry is loaded. */
    public void ValidateTemplates(IEnumerable<AuthoritativeWorldObject> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        IReadOnlyDictionary<uint, AuthoritativeWorldObject> templatesById;
        try {
            templatesById = templates.ToDictionary(@object => @object.EntityId);
        } catch (ArgumentException exception) {
            throw new InvalidDataException(
                "Native DUN object templates contain duplicate entity IDs.",
                exception);
        }
        foreach (var definition in definitions.Where(definition => definition.TemplateEntityId != 0)) {
            if (!templatesById.TryGetValue(definition.TemplateEntityId, out var template))
                throw new InvalidDataException(
                    $"Native DUN object mapping for level {definition.GetRuntimeLevelId()} references unknown template entity {definition.TemplateEntityId}.");
            if (template.ObjectId != definition.ObjectId)
                throw new InvalidDataException(
                    $"Native DUN object mapping for level {definition.GetRuntimeLevelId()} template {definition.TemplateEntityId} uses object {template.ObjectId}, expected {definition.ObjectId}.");
            ValidateTemplateNativeObjectId(definition, template);
        }
    }

    /**
     * Materializes every explicitly mapped nonzero native DUN object cell in a
     * loaded runtime level. Unmapped native IDs remain inert and are not
     * guessed as gameplay objects.
     */
    public IReadOnlyList<AuthoritativeWorldObject> LoadAvailableObjects(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        IEnumerable<AuthoritativeWorldObject>? templates = null,
        bool preserveExisting = false,
        bool allowMissingLayers = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        return LoadAvailableObjects(
            world.Levels,
            existingObjects,
            templates,
            preserveExisting,
            allowMissingLayers);
    }

    /** Materializes mapped objects from an already projected level collection. */
    public IReadOnlyList<AuthoritativeWorldObject> LoadAvailableObjects(
        IEnumerable<AuthoritativeLevel> levels,
        IEnumerable<AuthoritativeWorldObject>? existingObjects = null,
        IEnumerable<AuthoritativeWorldObject>? templates = null,
        bool preserveExisting = false,
        bool allowMissingLayers = false)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var existingValues = (existingObjects ?? []).ToArray();
        var templateValues = (templates ?? []).ToArray();
        var usedEntityIds = existingValues
            .Select(@object => @object.EntityId)
            .ToHashSet();
        var existingByEntityId = existingValues
            .ToDictionary(@object => @object.EntityId);
        var existingObjectCountsByLevel = existingValues
            .GroupBy(@object => @object.LevelId)
            .ToDictionary(group => group.Key, group => group.Count());
        var materializedObjectCountsByLevel = new Dictionary<uint, int>();
        var templateByEntityId = templateValues
            .ToDictionary(@object => @object.EntityId);
        var levelsById = levels.ToDictionary(level => level.LevelId);
        var objects = new List<AuthoritativeWorldObject>();
        foreach (var definition in definitions) {
            var levelId = definition.GetRuntimeLevelId();
            if (!levelsById.TryGetValue(levelId, out var level))
                continue;
            if (level.NativeObjectLayer.Count == 0 && allowMissingLayers)
                continue;
            if (level.NativeObjectLayer.Count == 0)
                throw new InvalidDataException(
                    $"Native DUN object mapping for level {levelId} requires a native object layer.");
            if (level.NativeObjectLayer.Count != level.Width * level.Height)
                throw new InvalidDataException(
                    $"Native DUN object layer for level {levelId} has an invalid length.");

            for (var cell = 0; cell < level.NativeObjectLayer.Count; cell++) {
                if (level.NativeObjectLayer[cell] != definition.NativeObjectId)
                    continue;
                var positionX = cell % level.Width;
                var positionY = cell / level.Width;
                var entityId = GetRuntimeEntityId(definition, cell);
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
                var existingLevelObjectCount = existingObjectCountsByLevel.GetValueOrDefault(levelId);
                var materializedLevelObjectCount = materializedObjectCountsByLevel
                    .GetValueOrDefault(levelId);
                if (existingLevelObjectCount + materializedLevelObjectCount
                    >= AuthoritativeLevel.NativeMaximumObjectCount) {
                    throw new InvalidDataException(
                        $"Native DUN object mapping for level {levelId} exceeds the native "
                        + $"{AuthoritativeLevel.NativeMaximumObjectCount}-object capacity.");
                }
                if (definition.TemplateEntityId != 0) {
                    if (!templateByEntityId.TryGetValue(
                            definition.TemplateEntityId,
                            out var template)) {
                        throw new InvalidDataException(
                            $"Native DUN object mapping for level {levelId} references unknown template entity {definition.TemplateEntityId}.");
                    }
                    if (template.ObjectId != definition.ObjectId)
                        throw new InvalidDataException(
                            $"Native DUN object mapping for level {levelId} template {definition.TemplateEntityId} uses object {template.ObjectId}, expected {definition.ObjectId}.");
                    ValidateTemplateNativeObjectId(definition, template);
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
                materializedObjectCountsByLevel[levelId] = materializedLevelObjectCount + 1;
            }
        }
        return objects;
    }

    /**
     * A template may omit its native presentation identity for custom objects,
     * but an explicit identity must agree with the mapped authored object
     * identity. Raw DUN layer values are conversion-table indices and are not
     * the same namespace as the native presentation enum.
     */
    private static void ValidateTemplateNativeObjectId(
        AuthoritativeNativeDunObjectDefinition definition,
        AuthoritativeWorldObject template)
    {
        if (template.NativeObjectId != 0
            && template.NativeObjectId != definition.ObjectId) {
            throw new InvalidDataException(
                $"Native DUN object mapping for level {definition.GetRuntimeLevelId()} "
                + $"template {definition.TemplateEntityId} uses native object "
                + $"{template.NativeObjectId}, expected {definition.ObjectId}.");
        }
    }

    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var definition in definitions) {
            if (definition.TargetKind != AuthoritativeNativeDunObjectTargetKind.SetLevel)
                continue;
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

    public static uint GetRuntimeEntityId(uint levelId, int cell)
    {
        if (cell is < 0 or >= RuntimeLevelCellStride)
            throw new ArgumentOutOfRangeException(nameof(cell));
        return checked(RuntimeLevelObjectEntityIdBase
            + (levelId * (uint)RuntimeLevelCellStride)
            + (uint)cell);
    }

    private static uint GetRuntimeEntityId(
        AuthoritativeNativeDunObjectDefinition definition,
        int cell)
    {
        return definition.TargetKind == AuthoritativeNativeDunObjectTargetKind.SetLevel
            ? GetRuntimeEntityId(definition.SetLevelId, definition.ActiveMap, cell)
            : GetRuntimeEntityId(definition.LevelId, cell);
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
        if (nativeObjectId is 0 or > AuthoritativeLevel.NativeMaximumDunObjectLayerId)
            throw new InvalidDataException(
                $"Native DUN object row {row.LineNumber} contains an invalid raw object-layer ID.");
        var hasSetLevel = (row.TryGet("set_level_id", out var setLevelId)
                && !string.IsNullOrWhiteSpace(setLevelId))
            || (row.TryGet("set_level_symbol", out var setLevelSymbol)
                && !string.IsNullOrWhiteSpace(setLevelSymbol));
        var hasLevel = (row.TryGet("level_id", out var levelId)
                && !string.IsNullOrWhiteSpace(levelId))
            || (row.TryGet("level_symbol", out var levelSymbol)
                && !string.IsNullOrWhiteSpace(levelSymbol));
        if (hasSetLevel == hasLevel)
            throw new InvalidDataException(
                $"Native DUN object row {row.LineNumber} must target exactly one set-level or ordinary level.");

        if (hasLevel) {
            if (row.TryGet("active_map", out var activeMap)
                && !string.IsNullOrWhiteSpace(activeMap))
                throw new InvalidDataException(
                    $"Native DUN object row {row.LineNumber} cannot use active_map with an ordinary level target.");
            return new AuthoritativeNativeDunObjectDefinition(
                0,
                row.OptionalBoolean("active_map"),
                (ushort)nativeObjectId,
                AuthoritativeWorldObjectIdentity.Resolve(row, stableIds),
                row.OptionalUInt32("template_entity_id")) {
                TargetKind = AuthoritativeNativeDunObjectTargetKind.Level,
                LevelId = AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
                ObjectSymbol = AuthoritativeWorldObjectIdentity.ReadSymbol(row),
            };
        }

        return new AuthoritativeNativeDunObjectDefinition(
            AuthoritativeSetLevelIdentity.ResolveOptional(
                row,
                stableIds,
                "set_level_id",
                "set_level_symbol"),
            row.RequiredBoolean("active_map"),
            (ushort)nativeObjectId,
            AuthoritativeWorldObjectIdentity.Resolve(row, stableIds),
            row.OptionalUInt32("template_entity_id")) {
            SetLevelSymbol = row.TryGet("set_level_symbol", out var symbol)
                ? symbol
                : string.Empty,
            ObjectSymbol = AuthoritativeWorldObjectIdentity.ReadSymbol(row),
        };
    }
}
