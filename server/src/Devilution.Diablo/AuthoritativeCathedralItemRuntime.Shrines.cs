using Devilution.Engine;

namespace Devilution.Diablo;

public partial class AuthoritativeCathedralItemRuntime
{
    /** Native objects.cpp OperateShrineDivine delegates item allocation to items.cpp CreateTypeItem. */
    internal AuthoritativeCathedralNativeItemProjection? CreateShrineItem(uint levelId, int x, int y,
        int miscId, AuthoritativeCathedralItemOccupancyContext occupancy, Func<uint> allocateEntityId,
        DeterministicLegacyRandom random)
    {
        var level = GetLevel(levelId);
        var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(new(
            BuildWorldState(levelId, occupancy), profile.ItemCatalog,
            profile.ItemPolicy with { Depth = level.Depth, CurrentDungeonLevel = level.Depth },
            random.State, x, y, false, RequestedItemType: 0, RequestedMiscId: miscId), NativeItemFamily(levelId));
        random.Restore(result.FinalRandomState);
        if (result.Item is null || !result.PlacementSucceeded) return null;
        var item = result.Item;
        var projection = ProjectNativeItem(result.SelectedSlot, allocateEntityId(), item.Seed,
            PriceFor(item), item, GetBinding(item.ItemIndex), profile.ContentInventoryDimensions);
        if (!TryAddWorldItem(levelId, projection, out var slot) || slot != result.SelectedSlot)
            throw new InvalidOperationException("Native shrine item allocation diverged from its prepared pool.");
        uniqueItemFlags.UnionWith(result.UniqueItemFlags);
        return projection;
    }
}
