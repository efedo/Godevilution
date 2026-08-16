namespace Devilution.Diablo;

using Devilution.Data;

/** Deterministic level transition owned by the authoritative world. */
public sealed partial record AuthoritativePortal(
    uint PortalId,
    uint SourceLevelId,
    int SourcePositionX,
    int SourcePositionY,
    uint DestinationLevelId,
    int DestinationPositionX,
    int DestinationPositionY);

public static class AuthoritativePortalRules
{
    /** Native town coordinates used by the four player portal slots. */
    public const uint TownLevelId = 0;

    public const int TownPositionX = 57;

    public const int TownPositionY = 40;

    public const int TownPortalSlotCount = 4;

    public const int TownPortalSlotSpacing = 2;

    public static int GetTownPositionX(
        uint ownerEntityId,
        int positionX = TownPositionX,
        int slotCount = TownPortalSlotCount,
        int slotSpacing = TownPortalSlotSpacing)
    {
        if (ownerEntityId == 0)
            throw new ArgumentOutOfRangeException(nameof(ownerEntityId));
        if (slotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        if (slotSpacing < 0)
            throw new ArgumentOutOfRangeException(nameof(slotSpacing));
        return checked(positionX + (int)((ownerEntityId - 1) % (uint)slotCount) * slotSpacing);
    }
}

/** Mutable lifetime and ownership metadata for a dynamic native town portal. */
public sealed partial record AuthoritativePortal
{
    /** Zero denotes an external/static route that does not expire. */
    public uint RemainingTicks { get; set; }

    /** Player entity that owns a dynamic town portal; zero denotes static content. */
    public uint OwnerEntityId { get; init; }

    /** Dynamic town portals can be entered from either endpoint. */
    public bool Bidirectional { get; init; }
}

/** Immutable snapshot projection of an authoritative portal. */
public sealed record DiabloPortalState(
    uint PortalId,
    uint SourceLevelId,
    int SourcePositionX,
    int SourcePositionY,
    uint DestinationLevelId,
    int DestinationPositionX,
    int DestinationPositionY)
{
    public uint RemainingTicks { get; init; }

    public uint OwnerEntityId { get; init; }

    public bool Bidirectional { get; init; }
}

/** External portal definitions used to validate level transitions. */
public static class AuthoritativePortalCatalog
{
    /** Validates static portal levels and endpoint coordinates against world geometry. */
    public static void ValidateWorld(
        IEnumerable<AuthoritativePortal> portals,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(portals);
        ArgumentNullException.ThrowIfNull(world);
        foreach (var portal in portals) {
            if (!world.ContainsLevel(portal.SourceLevelId)
                || !world.ContainsLevel(portal.DestinationLevelId)) {
                throw new InvalidDataException(
                    $"Portal {portal.PortalId} references an unknown source or destination level.");
            }
            if (!world.ContainsPosition(
                    portal.SourceLevelId,
                    portal.SourcePositionX,
                    portal.SourcePositionY)
                || !world.ContainsPosition(
                    portal.DestinationLevelId,
                    portal.DestinationPositionX,
                    portal.DestinationPositionY)) {
                throw new InvalidDataException(
                    $"Portal {portal.PortalId} contains an endpoint outside its level.");
            }
        }
    }

    public static IReadOnlyList<AuthoritativePortal> LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var portals = table.Rows.Select(row => new AuthoritativePortal(
            row.RequiredUInt32("portal_id"),
            row.RequiredUInt32("source_level_id"),
            row.RequiredInt32("source_position_x"),
            row.RequiredInt32("source_position_y"),
            row.RequiredUInt32("destination_level_id"),
            row.RequiredInt32("destination_position_x"),
            row.RequiredInt32("destination_position_y"))).ToArray();
        if (portals.Any(portal => portal.PortalId == 0)
            || portals.Select(portal => portal.PortalId).Distinct().Count() != portals.Length)
            throw new InvalidDataException($"Portal table '{sourcePath}' contains duplicate or zero IDs.");
        return portals;
    }
}

/** Stable runtime meaning projected with a timed authoritative status. */
public enum AuthoritativeStatusEffectBehavior
{
    None,
    Infravision,
    Search,
    Etherealize,
}

public sealed record AuthoritativeStatusEffect(
    uint EffectId,
    uint RemainingTicks,
    int Magnitude)
{
    public AuthoritativeStatusEffectBehavior Behavior { get; init; }
}
