namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal bool CanOperateNativeChest(AuthoritativeWorldObject value)
        => nativePopulationPolicy is not null
            && value.LevelId == nativePopulationPolicy.LevelId
            && Objects.TryGetValue(value.EntityId, out var current)
            && ReferenceEquals(current, value)
            && nativePopulationPolicy.CanOperateNativeChest(value);

    internal bool TryGetNativeChestLootParameters(
        AuthoritativeWorldObject value,
        out int nativeType,
        out uint randomSeed,
        out int itemCount,
        out int itemBranch)
    {
        nativeType = 0;
        randomSeed = 0;
        itemCount = 0;
        itemBranch = 0;
        return nativePopulationPolicy is not null
            && value.LevelId == nativePopulationPolicy.LevelId
            && Objects.TryGetValue(value.EntityId, out var current)
            && ReferenceEquals(current, value)
            && nativePopulationPolicy.TryGetNativeChestLootParameters(
                value, out nativeType, out randomSeed, out itemCount, out itemBranch);
    }

    internal bool TryCommitNativeChestOpen(AuthoritativeWorldObject value)
    {
        if (!CanOperateNativeChest(value))
            return false;
        return nativePopulationPolicy!.TryCommitNativeChestOpen(value);
    }
}
