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
    int DestinationPositionY)
{
    public AuthoritativePortal ValidateDestinationIdentity()
    {
        if (DestinationSetLevelId is < -1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(DestinationSetLevelId));
        return this;
    }

    /** Optional native set-level identity represented by DestinationLevelId. */
    public int DestinationSetLevelId { get; init; } = -1;

    /** Selects the active or pre-map variant for a set-level destination. */
    public bool DestinationSetLevelActiveMap { get; init; } = true;

    /** Stable set-level symbol retained alongside the numeric destination. */
    public string DestinationSetLevelSymbol { get; init; } = string.Empty;

    /** Stable content symbol retained alongside the numeric portal identity. */
    public string StableSymbol { get; init; } = string.Empty;
}

public static class AuthoritativePortalRules
{
    /** Native town coordinates used by the four player portal slots. */
    public const uint TownLevelId = 0;

    public const int TownPositionX = 57;

    public const int TownPositionY = 40;

    public const int TownPortalSlotCount = 4;

    /** Native portal-array capacity (MAXPORTAL). */
    public const int NativeMaximumPortalCount = 4;

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
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "portal_id", "portal_symbol", "source_level_id", "source_level_symbol",
        "source_position_x", "source_position_y", "destination_level_id",
        "destination_level_symbol", "destination_position_x", "destination_position_y",
        "destination_set_level_id", "destination_set_level_symbol",
        "destination_set_level_active_map",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production portal table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("portal_id");
            _ = row.RequiredUInt32("source_level_id");
            _ = row.RequiredInt32("source_position_x");
            _ = row.RequiredInt32("source_position_y");
            _ = row.RequiredInt32("destination_position_x");
            _ = row.RequiredInt32("destination_position_y");
        }
    }

    /** Validates static portal levels and endpoint coordinates against world geometry. */
    public static void ValidateWorld(
        IEnumerable<AuthoritativePortal> portals,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(portals);
        ArgumentNullException.ThrowIfNull(world);
        var values = portals.ToArray();
        if (values.Length > AuthoritativePortalRules.NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"Portal catalog contains more than {AuthoritativePortalRules.NativeMaximumPortalCount} routes, exceeding the native portal capacity.");
        var portalIds = new HashSet<uint>();
        foreach (var portal in values) {
            portal.ValidateDestinationIdentity();
            if (portal.PortalId == 0 || !portalIds.Add(portal.PortalId))
                throw new InvalidDataException(
                    $"Portal catalog contains duplicate or zero ID {portal.PortalId}.");
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
            if (!world.IsWalkable(
                    portal.SourceLevelId,
                    portal.SourcePositionX,
                    portal.SourcePositionY)
                || !world.IsWalkable(
                    portal.DestinationLevelId,
                    portal.DestinationPositionX,
                    portal.DestinationPositionY)) {
                throw new InvalidDataException(
                    $"Portal {portal.PortalId} contains a blocked endpoint.");
            }
        }
    }

    public static IReadOnlyList<AuthoritativePortal> LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null,
        AuthoritativeSetLevelMapCatalog? setLevelMaps = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var portals = table.Rows.Select(row => {
            var hasDestinationSetLevel = row.TryGet(
                    "destination_set_level_id",
                    out var destinationSetLevelText)
                && !string.IsNullOrWhiteSpace(destinationSetLevelText)
                && !string.Equals(
                    destinationSetLevelText.Trim(),
                    "none",
                    StringComparison.OrdinalIgnoreCase)
                || row.TryGet(
                    "destination_set_level_symbol",
                    out var destinationSetLevelSymbolText)
                && !string.IsNullOrWhiteSpace(destinationSetLevelSymbolText);
            var destinationSetLevelId = hasDestinationSetLevel
                ? AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "destination_set_level_id",
                    "destination_set_level_symbol")
                : -1;
            var destinationSetLevelActiveMap = row.OptionalBoolean(
                "destination_set_level_active_map",
                defaultValue: true);
            var hasOrdinaryDestination = row.TryGet(
                    "destination_level_id",
                    out var destinationLevelText)
                && !string.IsNullOrWhiteSpace(destinationLevelText)
                || row.TryGet(
                    "destination_level_symbol",
                    out var destinationLevelSymbolText)
                && !string.IsNullOrWhiteSpace(destinationLevelSymbolText);
            if (hasDestinationSetLevel == hasOrdinaryDestination)
                throw new InvalidDataException(
                    $"Portal row {row.LineNumber} must target exactly one ordinary level or set-level variant.");
            if (hasDestinationSetLevel
                && (setLevelMaps is null
                    || !setLevelMaps.TryGet(destinationSetLevelId, out _))) {
                throw new InvalidDataException(
                    $"Portal row {row.LineNumber} references unavailable set level {destinationSetLevelId}.");
            }
            var destinationLevelId = hasDestinationSetLevel
                ? AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                    destinationSetLevelId,
                    destinationSetLevelActiveMap)
                : AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "destination_level_id",
                    "destination_level_symbol");
            return new AuthoritativePortal(
                AuthoritativePortalIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "source_level_id",
                    "source_level_symbol"),
                row.RequiredInt32("source_position_x"),
                row.RequiredInt32("source_position_y"),
                destinationLevelId,
                row.RequiredInt32("destination_position_x"),
                row.RequiredInt32("destination_position_y")) {
                StableSymbol = AuthoritativePortalIdentity.ReadSymbol(row),
                DestinationSetLevelId = destinationSetLevelId,
                DestinationSetLevelActiveMap = destinationSetLevelActiveMap,
                DestinationSetLevelSymbol = row.TryGet(
                        "destination_set_level_symbol",
                        out var symbol)
                    ? symbol.Trim()
                    : string.Empty,
            }.ValidateDestinationIdentity();
        }).ToArray();
        if (portals.Any(portal => portal.PortalId == 0)
            || portals.Length > AuthoritativePortalRules.NativeMaximumPortalCount
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
