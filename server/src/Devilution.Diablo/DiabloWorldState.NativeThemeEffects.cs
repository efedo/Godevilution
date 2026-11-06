namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeThemeEffectInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(Objects.Values
            .Where(value => value.NativeObjectId is 66 or 76 or 79 or 80 or 81 or 82
                && NativePopulationForLevel(value.LevelId)?.CanOperate(value) == true)
            .Select(value => value.EntityId));

    internal IEnumerable<AuthoritativeWorldObject> NativeBurningCrossesInActiveOrder()
        => NativePopulationOwners.OrderBy(owner => owner.LevelId)
            .SelectMany(owner => owner.NativeBurningCrossEntitiesInActiveOrder())
            .Select(entityId => Objects[entityId]);
}
