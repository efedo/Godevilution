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
    public AuthoritativeNativeDunFragmentObjectPlacement Validate()
    {
        if (PlacementId == 0
            || FragmentId == 0
            || EntityId == 0
            || AnchorEntityId == 0
            || EntityId == AnchorEntityId
            || OffsetX is < -80 or > 80
            || OffsetY is < -80 or > 80) {
            throw new InvalidDataException(
                $"Native DUN fragment object placement {PlacementId} contains invalid identity or offset metadata.");
        }
        return this;
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

    /** Applies placements using the anchors' original positions atomically. */
    public void Apply(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeNativeDunFragmentCatalog fragments)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(fragments);
        var fragmentsById = fragments.Definitions.ToDictionary(definition => definition.FragmentId);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var anchorPositions = objectsById.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value.LevelId, pair.Value.PositionX, pair.Value.PositionY));
        var plannedPositions = new Dictionary<uint, (int X, int Y)>();
        foreach (var placement in placements) {
            if (!fragmentsById.TryGetValue(placement.FragmentId, out var fragment))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references unknown fragment {placement.FragmentId}.");
            if (!objectsById.TryGetValue(placement.EntityId, out var target))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references missing object {placement.EntityId}.");
            if (!anchorPositions.TryGetValue(placement.AnchorEntityId, out var anchor))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} references missing anchor {placement.AnchorEntityId}.");
            if (target.LevelId != anchor.LevelId)
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} targets level {target.LevelId} but its anchor is on level {anchor.LevelId}.");
            if (fragment.LevelId != target.LevelId
                || fragment.AnchorEntityId != placement.AnchorEntityId) {
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} does not match its fragment anchor or level.");
            }
            var positionX = checked(anchor.PositionX + placement.OffsetX);
            var positionY = checked(anchor.PositionY + placement.OffsetY);
            if (!world.ContainsPosition(target.LevelId, positionX, positionY))
                throw new InvalidDataException(
                    $"Native DUN fragment object placement {placement.PlacementId} exceeds level bounds.");
            plannedPositions.Add(placement.EntityId, (positionX, positionY));
        }

        foreach (var planned in plannedPositions) {
            objectsById[planned.Key].PositionX = planned.Value.X;
            objectsById[planned.Key].PositionY = planned.Value.Y;
        }
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
                row.RequiredInt32("offset_y"))));
    }
}
