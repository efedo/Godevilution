using Devilution.Data;

namespace Devilution.Diablo;

/** External world identities shared by authoritative transition and service rules. */
public sealed record AuthoritativeWorldRules(
    uint TownLevelId,
    uint AdriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId)
{
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
        return new AuthoritativeWorldRules(
            row.OptionalUInt32("town_level_id", AuthoritativePortalRules.TownLevelId),
            row.OptionalUInt32(
                "adria_store_id",
                DiabloStoreCommandProcessor.AdriaStoreId));
    }

    /** Ensures the configured town identity exists in the activated geometry. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.ContainsLevel(TownLevelId)) {
            throw new InvalidDataException(
                $"World rules reference unknown town level {TownLevelId}.");
        }
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
