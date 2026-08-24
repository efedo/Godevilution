using Devilution.Data;

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

    /** Optional behavior-only object entity cloned when the placement materializes. */
    public uint TemplateEntityId { get; init; }

    public AuthoritativeNativeDunFragmentObjectPlacement Validate()
    {
        var anchorUniqueIdentity = AnchorUniqueIdentity.Trim();
        if (PlacementId == 0
            || FragmentId == 0
            || EntityId == 0
            || AnchorEntityId != 0 && anchorUniqueIdentity.Length != 0
            || AnchorEntityId == 0 && anchorUniqueIdentity.Length == 0
            || EntityId == AnchorEntityId
            || OffsetX is < -80 or > 80
            || OffsetY is < -80 or > 80
            || anchorUniqueIdentity.Length > 128) {
            throw new InvalidDataException(
                $"Native DUN fragment object placement {PlacementId} contains invalid identity or offset metadata.");
        }
        return anchorUniqueIdentity == AnchorUniqueIdentity
            ? this
            : this with { AnchorUniqueIdentity = anchorUniqueIdentity };
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
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null)
    {
        _ = Materialize(world, objects, [], fragments, combatTargets);
    }

    /** Applies placements and returns any objects cloned from behavior templates. */
    public IReadOnlyList<AuthoritativeWorldObject> Materialize(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeWorldObject> templates,
        AuthoritativeNativeDunFragmentCatalog fragments,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(fragments);
        var fragmentsById = fragments.Definitions.ToDictionary(definition => definition.FragmentId);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
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
            objectsById.TryGetValue(placement.EntityId, out var target);
            if (target is null && placement.TemplateEntityId == 0)
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references missing object {placement.EntityId}.");
            (uint LevelId, int PositionX, int PositionY) anchor;
            if (placement.AnchorEntityId != 0) {
                if (!anchorPositions.TryGetValue(placement.AnchorEntityId, out anchor))
                    throw new InvalidDataException(
                        $"Native DUN fragment object placement {placement.PlacementId} references missing anchor {placement.AnchorEntityId}.");
            } else {
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
                    StringComparison.Ordinal)) {
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
                generatedObjects.Add(template.WithPlacement(
                    placement.EntityId,
                    targetLevelId,
                    positionX,
                    positionY));
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
        string contents)
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
                TemplateEntityId = row.OptionalUInt32("template_entity_id"),
            }));
    }
}
