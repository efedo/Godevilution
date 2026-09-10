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
    /** Service-only store identity used by Cain's paid identification menu. */
    public uint StorytellerStoreId { get; init; } = DiabloStoreCommandProcessor.StorytellerStoreId;

    /** True when the active table explicitly owns Town Portal destination geometry. */
    public bool HasTownPortalDestinationGeometry { get; init; }

    /** Native-compatible fallback used by legacy callers without world rules. */
    public static AuthoritativeWorldRules NativeDefaults { get; } = new(
        AuthoritativePortalRules.TownLevelId,
        DiabloStoreCommandProcessor.AdriaStoreId);

    public static AuthoritativeWorldRules LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
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
            AuthoritativeLevelIdentity.ResolveOptional(
                row,
                stableIds,
                "town_level_id",
                "town_level_symbol"),
            AuthoritativeStoreIdentity.ResolveOptional(
                row,
                stableIds,
                "adria_store_id",
                "adria_store_symbol",
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
            StorytellerStoreId = AuthoritativeStoreIdentity.ResolveOptional(
                row,
                stableIds,
                "storyteller_store_id",
                "storyteller_store_symbol",
                DiabloStoreCommandProcessor.StorytellerStoreId),
            HasTownPortalDestinationGeometry = hasGeometry,
        }.Validate();
    }

    /**
     * Rejects compatibility fallbacks at the production content boundary.
     * Direct callers may continue to load older partial rows.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Rows.Count != 1)
            throw new InvalidDataException(
                $"Production world rules table '{table.SourcePath}' must contain exactly one row.");

        var requiredColumns = new[] {
            "town_level_id",
            "adria_store_id",
            "storyteller_store_id",
            "town_portal_destination_position_x",
            "town_portal_destination_position_y",
            "town_portal_destination_slot_count",
            "town_portal_destination_slot_spacing",
        };
        var missingColumns = requiredColumns
            .Where(column => !table.Columns.Contains(column, StringComparer.Ordinal))
            .ToArray();
        if (missingColumns.Length != 0)
            throw new InvalidDataException(
                $"Production world rules table '{table.SourcePath}' is missing required columns: "
                + string.Join(", ", missingColumns));

        var row = table.Rows[0];
        foreach (var column in requiredColumns)
            _ = row.Required(column);
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
        if (!HasTownPortalDestinationGeometry)
            return;

        for (var slot = 0; slot < TownPortalDestinationSlotCount; slot++) {
            var positionX = checked(
                (long)TownPortalDestinationPositionX
                + (long)slot * TownPortalDestinationSlotSpacing);
            if (positionX > int.MaxValue
                || !world.ContainsPosition(TownLevelId, (int)positionX, TownPortalDestinationPositionY)) {
                throw new InvalidDataException(
                    $"World rules Town Portal destination slot {slot} is outside town level "
                    + $"{TownLevelId}.");
            }
            if (!world.IsWalkable(TownLevelId, (int)positionX, TownPortalDestinationPositionY)) {
                throw new InvalidDataException(
                    $"World rules Town Portal destination slot {slot} is blocked in town level "
                    + $"{TownLevelId}.");
            }
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
        if (StorytellerStoreId == 0)
            throw new InvalidDataException("World rules require a non-zero Storyteller store identity.");
        if (StorytellerStoreId == AdriaStoreId)
            throw new InvalidDataException(
                "World rules require distinct Adria and Storyteller store identities.");
        if (stores.ContainsStore(AdriaStoreId)) {
            throw new InvalidDataException(
                $"World rules reserve store {AdriaStoreId} for Adria's service actions, "
                + "but the active store catalog contains stock for that identity.");
        }
        if (stores.ContainsStore(StorytellerStoreId)) {
            throw new InvalidDataException(
                $"World rules reserve store {StorytellerStoreId} for Storyteller's service actions, "
                + "but the active store catalog contains stock for that identity.");
        }
    }
}
