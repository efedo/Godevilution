namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal bool CanOperateNativeChest(AuthoritativeWorldObject value)
        => NativePopulationForLevel(value.LevelId) is { Family: AuthoritativeNativePopulationFamily.Cathedral } policy
            && Objects.TryGetValue(value.EntityId, out var current)
            && ReferenceEquals(current, value)
            && policy.CanOperateNativeChest(value);

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
        return NativePopulationForLevel(value.LevelId) is { } policy
            && Objects.TryGetValue(value.EntityId, out var current)
            && ReferenceEquals(current, value)
            && policy.TryGetNativeChestLootParameters(
                value, out nativeType, out randomSeed, out itemCount, out itemBranch);
    }

    internal bool TryCommitNativeChestOpen(AuthoritativeWorldObject value)
    {
        if (!CanOperateNativeChest(value))
            return false;
        return NativePopulationForLevel(value.LevelId)!.TryCommitNativeChestOpen(value);
    }
}
