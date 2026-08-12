namespace Devilution.Diablo;

using Devilution.Data;

/** Deterministic level transition owned by the authoritative world. */
public sealed record AuthoritativePortal(
    uint PortalId,
    uint SourceLevelId,
    int SourcePositionX,
    int SourcePositionY,
    uint DestinationLevelId,
    int DestinationPositionX,
    int DestinationPositionY);

/** External portal definitions used to validate level transitions. */
public static class AuthoritativePortalCatalog
{
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
}

public sealed record AuthoritativeStatusEffect(
    uint EffectId,
    uint RemainingTicks,
    int Magnitude)
{
    public AuthoritativeStatusEffectBehavior Behavior { get; init; }
}
