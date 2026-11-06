namespace Devilution.Diablo;

/** Detached native object and shared item-pool inputs at the operation boundary. */
public sealed record AuthoritativeNativeThemeItemLootRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    int NativeObjectId, int ObjectSlot, int PositionX, int PositionY,
    uint RandomSeed, int AnimationFrame,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

public sealed record AuthoritativeNativeThemeItemLootResult(
    AuthoritativeCathedralChestLootResult Loot, int AnimationFrame);
