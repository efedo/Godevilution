namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeSarcophagusInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(
            Objects.Values
                .Where(CanOperateNativeSarcophagus)
                .Select(value => value.EntityId));
}
