namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeChestInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(
            Objects.Values
                .Where(CanOperateNativeChest)
                .Select(value => value.EntityId));
}
