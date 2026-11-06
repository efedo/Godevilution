namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal bool CanOperateNativeThemeItem(AuthoritativeWorldObject value)
        => cathedralItems is not null && Quests.Count == 0
            && NativePopulationForLevel(value.LevelId)?.CanOperateNativeThemeItem(value) == true;

    internal IReadOnlySet<uint> CaptureNativeThemeObjectInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(Objects.Values
            .Where(value => AuthoritativeNativeThemeItemLootGenerator.Supports((int)value.NativeObjectId)
                && CanOperateNativeThemeItem(value)).Select(value => value.EntityId));

    internal AuthoritativeCathedralItemChestMutationResult? TryOperateNativeThemeItem(AuthoritativeWorldObject value,
        AuthoritativePlayerState player, IReadOnlyList<AuthoritativePlayerState> participants, Func<uint> allocateEntityId)
    {
        if (!CanOperateNativeThemeItem(value)) return null;
        var owner = NativePopulationForLevel(value.LevelId)!;
        if (!owner.TryGetNativeThemeItemFacts(value, out var raw)) return null;
        ValidateCathedralItemProjection();
        var level = cathedralItems!.Profile.LevelProfiles.Single(row => row.LevelId == value.LevelId);
        var binding = level.ObjectBindings.Single(row => row.EntityId == value.EntityId);
        var local = participants.Contains(player) ? participants : participants.Append(player).ToArray();
        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, value.LevelId, local);
        var request = new AuthoritativeNativeThemeItemLootRequest(
            cathedralItems.BuildWorldState(value.LevelId, occupancy), cathedralItems.Profile.ItemCatalog,
            cathedralItems.Profile.ItemPolicy with { Depth = owner.Depth, CurrentDungeonLevel = owner.Depth },
            raw!.Type, binding.NativeObjectSlot, value.PositionX, value.PositionY, raw.RndSeed, value.AnimationFrame,
            new(true, false, player.BaseManaMaximum, player.BaseLifeMaximum));
        var prepared = cathedralItems.PrepareThemeItemLoot(value.LevelId, request);
        var mutation = cathedralItems.CommitChestLoot(prepared, allocateEntityId, IsEntityIdOccupied,
            () => CanOperateNativeThemeItem(value), () => owner.CommitNativeThemeItem(value));
        if (mutation is null) return null;
        foreach (var projection in mutation.Projections)
            WorldItems.Add(projection.EntityId, ToWorldItem(value.LevelId, projection));
        ValidateCathedralItemProjection();
        return mutation;
    }
}
