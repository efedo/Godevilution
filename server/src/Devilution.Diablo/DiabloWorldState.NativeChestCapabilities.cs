namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeChestInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(
            Objects.Values
                .Where(value => !value.TrapArmed && CanOperateNativeChest(value))
                .Select(value => value.EntityId));
}
