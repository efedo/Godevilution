namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeBarrelMeleeBreakableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(
            Objects.Values
                .Where(CanBreakNativeBarrel)
                .Select(value => value.EntityId));
}
