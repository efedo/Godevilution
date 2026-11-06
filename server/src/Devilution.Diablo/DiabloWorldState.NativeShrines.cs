using Devilution.Engine;

namespace Devilution.Diablo;

public readonly record struct DiabloNativeShrineProjection(uint EntityId, int Type);

public sealed partial class DiabloWorldState
{
    internal IReadOnlyList<DiabloNativeShrineProjection> CaptureNativeShrineTypes()
        => Objects.Values.Where(value => value.NativeObjectId is 59 or 60)
            .Where(value => NativePopulationForLevel(value.LevelId)?.NativeShrineType(value) is not null)
            .Select(value => new DiabloNativeShrineProjection(value.EntityId,
                NativePopulationForLevel(value.LevelId)!.NativeShrineType(value)!.Value)).ToArray();

    internal IReadOnlySet<uint> CaptureNativeShrineInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(Objects.Values.Where(value => value.NativeObjectId is 59 or 60
            && NativePopulationForLevel(value.LevelId)?.CanOperate(value) == true).Select(value => value.EntityId));

    internal void CreateNativeShrineItem(AuthoritativeWorldObject shrine, int miscId,
        IReadOnlyList<AuthoritativePlayerState> participants, Func<uint> allocateEntityId,
        DeterministicLegacyRandom random)
    {
        if (cathedralItems is null)
            throw new InvalidOperationException("A native shrine requires its shared item pool.");
        ValidateCathedralItemProjection();
        var projection = cathedralItems.CreateShrineItem(shrine.LevelId, shrine.PositionX, shrine.PositionY,
            miscId, BuildCathedralItemOccupancy(cathedralItems.Profile, shrine.LevelId, participants),
            allocateEntityId, random);
        if (projection is not null)
            WorldItems.Add(projection.EntityId, ToWorldItem(shrine.LevelId, projection));
        ValidateCathedralItemProjection();
    }

    /** Native objects.cpp OperateShrineThaumaturgic. Trap flags are intentionally not rearmed. */
    internal void ResetNativeChests(uint levelId, DeterministicLegacyRandom rng)
    {
        var owner = NativePopulationForLevel(levelId)!;
        foreach (var entityId in owner.NativeChestEntitiesInActiveOrder()) {
            var chest = Objects[entityId];
            if (!chest.Activated) continue;
            chest.NativeRandomSeed = rng.AdvanceSeed();
            chest.Activated = false;
            chest.AnimationFrame -= 2;
            owner.ValidateObject(chest);
        }
    }
}
