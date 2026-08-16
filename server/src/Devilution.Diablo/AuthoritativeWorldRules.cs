using Devilution.Data;

namespace Devilution.Diablo;

/** External world identities shared by authoritative transition and service rules. */
public sealed record AuthoritativeWorldRules(
    uint TownLevelId,
    uint AdriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId,
    int TownPortalDestinationPositionX = AuthoritativePortalRules.TownPositionX,
    int TownPortalDestinationPositionY = AuthoritativePortalRules.TownPositionY,
    int TownPortalDestinationSlotCount = AuthoritativePortalRules.TownPortalSlotCount,
    int TownPortalDestinationSlotSpacing = AuthoritativePortalRules.TownPortalSlotSpacing)
{
    /** True when the active table explicitly owns Town Portal destination geometry. */
    public bool HasTownPortalDestinationGeometry { get; init; }

    /** Native-compatible fallback used by legacy callers without world rules. */
    public static AuthoritativeWorldRules NativeDefaults { get; } = new(
        AuthoritativePortalRules.TownLevelId,
        DiabloStoreCommandProcessor.AdriaStoreId);

    public static AuthoritativeWorldRules LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        var geometryColumns = new[] {
            "town_portal_destination_position_x",
            "town_portal_destination_position_y",
            "town_portal_destination_slot_count",
            "town_portal_destination_slot_spacing",
        };
        var hasGeometry = geometryColumns.Any(table.Columns.Contains);
        if (hasGeometry && geometryColumns.Any(column => !table.Columns.Contains(column))) {
            throw new InvalidDataException(
                "World rules Town Portal geometry must declare all four destination columns.");
        }
        return new AuthoritativeWorldRules(
            row.OptionalUInt32("town_level_id", AuthoritativePortalRules.TownLevelId),
            row.OptionalUInt32(
                "adria_store_id",
                DiabloStoreCommandProcessor.AdriaStoreId),
            row.OptionalInt32(
                "town_portal_destination_position_x",
                AuthoritativePortalRules.TownPositionX),
            row.OptionalInt32(
                "town_portal_destination_position_y",
                AuthoritativePortalRules.TownPositionY),
            row.OptionalInt32(
                "town_portal_destination_slot_count",
                AuthoritativePortalRules.TownPortalSlotCount),
            row.OptionalInt32(
                "town_portal_destination_slot_spacing",
                AuthoritativePortalRules.TownPortalSlotSpacing)) {
            HasTownPortalDestinationGeometry = hasGeometry,
        }.Validate();
    }

    /** Ensures the configured town identity exists in the activated geometry. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Validate();
        if (!world.ContainsLevel(TownLevelId)) {
            throw new InvalidDataException(
                $"World rules reference unknown town level {TownLevelId}.");
        }
    }

    /** Ensures active content cannot create invalid destination slot geometry. */
    public AuthoritativeWorldRules Validate()
    {
        if (TownPortalDestinationPositionX < 0
            || TownPortalDestinationPositionY < 0
            || TownPortalDestinationSlotCount <= 0
            || TownPortalDestinationSlotSpacing < 0) {
            throw new InvalidDataException("World rules Town Portal geometry is invalid.");
        }
        return this;
    }

    /** Ensures the service-only identity cannot collide with a stock vendor. */
    public void ValidateStores(StoreCatalog stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        if (AdriaStoreId == 0)
            throw new InvalidDataException("World rules require a non-zero Adria store identity.");
        if (stores.ContainsStore(AdriaStoreId)) {
            throw new InvalidDataException(
                $"World rules reserve store {AdriaStoreId} for Adria's service actions, "
                + "but the active store catalog contains stock for that identity.");
        }
    }
}
