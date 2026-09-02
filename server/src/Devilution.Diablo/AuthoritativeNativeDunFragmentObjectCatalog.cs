using Devilution.Data;
using Devilution.Domain.Protocol;

namespace Devilution.Diablo;

/** Places an existing authored object relative to a native DUN fragment anchor. */
public sealed record AuthoritativeNativeDunFragmentObjectPlacement(
    uint PlacementId,
    uint FragmentId,
    uint EntityId,
    uint AnchorEntityId,
    int OffsetX,
    int OffsetY)
{
    /** Optional generated unique encounter used as the placement anchor. */
    public string AnchorUniqueIdentity { get; init; } = string.Empty;

    /** Optional generated set-level origin used as the placement anchor. */
    public int? AnchorSetLevelId { get; init; }

    /** Stable set-level symbol retained for composed-pack diagnostics. */
    public string AnchorSetLevelSymbol { get; init; } = string.Empty;

    /** Optional behavior-only object entity cloned when the placement materializes. */
    public uint TemplateEntityId { get; init; }

    public AuthoritativeNativeDunFragmentObjectPlacement Validate()
    {
        var anchorUniqueIdentity = AnchorUniqueIdentity.Trim();
        var anchorSetLevelSymbol = AnchorSetLevelSymbol.Trim();
        if (PlacementId == 0
            || FragmentId == 0
            || EntityId == 0
            || AnchorEntityId != 0 && anchorUniqueIdentity.Length != 0
            || AnchorEntityId != 0 && AnchorSetLevelId is not null
            || anchorUniqueIdentity.Length != 0 && AnchorSetLevelId is not null
            || AnchorSetLevelId is < 0 or > 8
            || EntityId == AnchorEntityId
            || OffsetX is < -80 or > 80
            || OffsetY is < -80 or > 80
            || !NativeTextLimits.IsIdentityWithinLimit(anchorUniqueIdentity)) {
            throw new InvalidDataException(
                $"Native DUN fragment object placement {PlacementId} contains invalid identity or offset metadata.");
        }
        if (!NativeTextLimits.IsIdentityWithinLimit(anchorSetLevelSymbol))
            throw new InvalidDataException(
                $"Native DUN fragment object placement {PlacementId} contains an invalid set-level anchor symbol.");
        return anchorUniqueIdentity == AnchorUniqueIdentity
            && anchorSetLevelSymbol == AnchorSetLevelSymbol
            ? this
            : this with {
                AnchorUniqueIdentity = anchorUniqueIdentity,
                AnchorSetLevelSymbol = anchorSetLevelSymbol,
            };
    }
}

/** Validated anchor-relative placement for authored native set-piece objects. */
public sealed class AuthoritativeNativeDunFragmentObjectCatalog
{
    private readonly IReadOnlyList<AuthoritativeNativeDunFragmentObjectPlacement> placements;

    public AuthoritativeNativeDunFragmentObjectCatalog(
        IEnumerable<AuthoritativeNativeDunFragmentObjectPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        var values = placements
            .Select(placement => placement.Validate())
            .OrderBy(placement => placement.PlacementId)
            .ToArray();
        if (values.Select(placement => placement.PlacementId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "Native DUN fragment object placements must contain unique placement identities.");
        if (values.Select(placement => placement.EntityId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "Native DUN fragment object placements must target each object at most once.");
        this.placements = values;
    }

    public IReadOnlyList<AuthoritativeNativeDunFragmentObjectPlacement> Placements
        => placements;

    /** Validates behavior-template references before any encounter materializes. */
    public void ValidateTemplates(IEnumerable<AuthoritativeWorldObject> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        IReadOnlyDictionary<uint, AuthoritativeWorldObject> templatesById;
        try {
            templatesById = templates.ToDictionary(@object => @object.EntityId);
        } catch (ArgumentException exception) {
            throw new InvalidDataException(
                "Native DUN fragment object templates contain duplicate entity IDs.",
                exception);
        }
        foreach (var placement in placements.Where(placement => placement.TemplateEntityId != 0)) {
            if (!templatesById.ContainsKey(placement.TemplateEntityId))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references unknown template entity {placement.TemplateEntityId}.");
        }
    }

    /** Validates optional set-level-origin anchors against the active set-level catalog. */
    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var placement in placements.Where(placement => placement.AnchorSetLevelId is not null)) {
            if (!setLevels.TryGet(placement.AnchorSetLevelId!.Value, out _))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references unknown set level {placement.AnchorSetLevelId}.");
        }
    }

    /** Returns placements admitted by the active anchor lifecycle. */
    public AuthoritativeNativeDunFragmentObjectCatalog Filter(
        Func<AuthoritativeNativeDunFragmentObjectPlacement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new AuthoritativeNativeDunFragmentObjectCatalog(placements.Where(predicate));
    }

    /** Applies placements using the anchors' original positions atomically. */
    public void Apply(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeNativeDunFragmentCatalog fragments,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins = null)
    {
        _ = Materialize(world, objects, [], fragments, combatTargets, setLevelOrigins);
    }

    /** Applies placements and returns any objects cloned from behavior templates. */
    public IReadOnlyList<AuthoritativeWorldObject> Materialize(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeWorldObject> templates,
        AuthoritativeNativeDunFragmentCatalog fragments,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(fragments);
        var fragmentsById = fragments.Definitions.ToDictionary(definition => definition.FragmentId);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var existingObjectCountsByLevel = objectsById.Values
            .GroupBy(@object => @object.LevelId)
            .ToDictionary(group => group.Key, group => group.Count());
        var generatedObjectCountsByLevel = new Dictionary<uint, int>();
        var templatesById = templates.ToDictionary(@object => @object.EntityId);
        var anchorPositions = objectsById.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value.LevelId, pair.Value.PositionX, pair.Value.PositionY));
        var targetsByUniqueIdentity = (combatTargets ?? [])
            .Where(target => target.IsUnique)
            .GroupBy(target => target.UniqueIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var plannedPositions = new Dictionary<uint, (int X, int Y)>();
        var generatedObjects = new List<AuthoritativeWorldObject>();
        foreach (var placement in placements) {
            if (!fragmentsById.TryGetValue(placement.FragmentId, out var fragment))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references unknown fragment {placement.FragmentId}.");
            var anchorSetLevelId = placement.AnchorSetLevelId ?? fragment.AnchorSetLevelId;
            objectsById.TryGetValue(placement.EntityId, out var target);
            if (target is null && placement.TemplateEntityId == 0)
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references missing object {placement.EntityId}.");
            (uint LevelId, int PositionX, int PositionY) anchor;
            if (placement.AnchorEntityId != 0) {
                if (!anchorPositions.TryGetValue(placement.AnchorEntityId, out anchor))
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} references missing anchor {placement.AnchorEntityId}.");
            } else if (!string.IsNullOrWhiteSpace(placement.AnchorUniqueIdentity)) {
                if (!targetsByUniqueIdentity.TryGetValue(
                        placement.AnchorUniqueIdentity,
                        out var matchingTargets)) {
                    if (string.Equals(
                            fragment.AnchorUniqueIdentity,
                            placement.AnchorUniqueIdentity,
                            StringComparison.Ordinal))
                        continue;
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} unique anchor "
                        + $"'{placement.AnchorUniqueIdentity}' does not match its fragment.");
                }
                if (matchingTargets.Length != 1) {
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} unique anchor "
                        + $"'{placement.AnchorUniqueIdentity}' must match exactly one encounter; found "
                        + $"{(matchingTargets?.Length ?? 0)}.");
                }
                var uniqueTarget = matchingTargets[0];
                anchor = (uniqueTarget.LevelId, uniqueTarget.PositionX, uniqueTarget.PositionY);
            } else if (anchorSetLevelId is { } placementSetLevelId) {
                if (setLevelOrigins is null
                    || !setLevelOrigins.TryGetValue(placementSetLevelId, out var origin)) {
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} references unpublished set-level origin {placementSetLevelId}.");
                }
                anchor = (origin.LevelId, origin.PositionX, origin.PositionY);
            } else if (fragment.AnchorPositionX is { } fragmentPositionX
                && fragment.AnchorPositionY is { } fragmentPositionY) {
                anchor = (fragment.LevelId, fragmentPositionX, fragmentPositionY);
            } else {
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} has no resolvable anchor.");
            }
            if (target is not null && target.LevelId != anchor.LevelId)
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} targets level {target.LevelId} but its anchor is on level {anchor.LevelId}.");
            var targetLevelId = target?.LevelId ?? anchor.LevelId;
            if (fragment.LevelId != targetLevelId
                || fragment.AnchorEntityId != placement.AnchorEntityId
                || !string.Equals(
                    fragment.AnchorUniqueIdentity,
                    placement.AnchorUniqueIdentity,
                    StringComparison.Ordinal)
                || fragment.AnchorSetLevelId != anchorSetLevelId) {
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} does not match its fragment anchor or level.");
            }
            var positionX = checked(anchor.PositionX + placement.OffsetX);
            var positionY = checked(anchor.PositionY + placement.OffsetY);
            if (!world.ContainsPosition(targetLevelId, positionX, positionY))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} exceeds level bounds.");
            if (target is not null) {
                plannedPositions.Add(placement.EntityId, (positionX, positionY));
            } else {
                if (!templatesById.TryGetValue(placement.TemplateEntityId, out var template))
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} references missing template {placement.TemplateEntityId}.");
                var existingLevelObjectCount = existingObjectCountsByLevel.GetValueOrDefault(targetLevelId);
                var generatedLevelObjectCount = generatedObjectCountsByLevel.GetValueOrDefault(targetLevelId);
                if (existingLevelObjectCount + generatedLevelObjectCount
                    >= AuthoritativeLevel.NativeMaximumObjectCount)
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} for level {targetLevelId} "
                        + $"exceeds the native {AuthoritativeLevel.NativeMaximumObjectCount}-object capacity.");
                generatedObjects.Add(template.WithPlacement(
                    placement.EntityId,
                    targetLevelId,
                    positionX,
                    positionY));
                generatedObjectCountsByLevel[targetLevelId] = generatedLevelObjectCount + 1;
            }
        }

        foreach (var planned in plannedPositions) {
            objectsById[planned.Key].PositionX = planned.Value.X;
            objectsById[planned.Key].PositionY = planned.Value.Y;
        }
        return generatedObjects;
    }

    public static AuthoritativeNativeDunFragmentObjectCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunFragmentObjectCatalog(table.Rows.Select(row =>
            new AuthoritativeNativeDunFragmentObjectPlacement(
                row.RequiredUInt32("placement_id"),
                row.RequiredUInt32("fragment_id"),
                row.RequiredUInt32("entity_id"),
                row.RequiredUInt32("anchor_entity_id"),
                row.RequiredInt32("offset_x"),
                row.RequiredInt32("offset_y")) {
                AnchorUniqueIdentity = row.TryGet("anchor_unique_identity", out var identity)
                    && !string.Equals(identity, "none", StringComparison.OrdinalIgnoreCase)
                    ? identity
                    : string.Empty,
                AnchorSetLevelId = OptionalSetLevelId(row, stableIds),
                AnchorSetLevelSymbol = row.TryGet("anchor_set_level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
                TemplateEntityId = row.OptionalUInt32("template_entity_id"),
            }));
    }

    private static int? OptionalSetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var numericPresent = row.TryGet("anchor_set_level_id", out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var symbolPresent = row.TryGet("anchor_set_level_symbol", out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            && !string.Equals(symbol, "none", StringComparison.OrdinalIgnoreCase);
        if (!numericPresent && !symbolPresent)
            return null;
        if (stableIds is null && !numericPresent)
            throw new InvalidDataException(
                $"Native DUN fragment object row {row.LineNumber} requires stable IDs to resolve set-level anchor '{symbol}'.");
        return AuthoritativeSetLevelIdentity.ResolveOptional(
            row,
            stableIds,
            "anchor_set_level_id",
            "anchor_set_level_symbol");
    }
}
