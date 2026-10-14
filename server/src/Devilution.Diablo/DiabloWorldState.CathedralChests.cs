namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal AuthoritativeCathedralItemChestMutationResult? TryOperateNativeCathedralChest(
        AuthoritativeWorldObject chest,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        Func<uint> allocateEntityId)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(allocateEntityId);
        if (cathedralItems is null || !CanOperateNativeChest(chest)
            || !TryGetNativeChestLootParameters(chest,
                out var nativeType, out var randomSeed, out var itemCount, out var itemBranch))
            return null;
        ValidateCathedralItemProjection();

        var levelProfile = cathedralItems.Profile.LevelProfiles.SingleOrDefault(
            value => value.LevelId == chest.LevelId && value.Depth == 1);
        var objectBinding = levelProfile?.ObjectBindings.SingleOrDefault(
            value => value.EntityId == chest.EntityId
                && value.NativeObjectSlot >= 0
                && value.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                && value.PositionX == chest.PositionX
                && value.PositionY == chest.PositionY);
        if (levelProfile is null || objectBinding is null)
            return null;

        var localParticipants = participants.Contains(player)
            ? participants
            : participants.Append(player).ToArray();
        var occupancy = BuildCathedralItemOccupancy(
            cathedralItems.Profile, chest.LevelId, localParticipants);
        var worldState = cathedralItems.BuildWorldState(chest.LevelId, occupancy);
        var policy = cathedralItems.Profile.ItemPolicy with {
            Depth = 1,
            CurrentDungeonLevel = 1,
            Spawn = false,
            QuestEnabled = false,
            SetLevel = false,
        };
        var request = new AuthoritativeCathedralChestLootRequest(
            worldState,
            cathedralItems.Profile.ItemCatalog,
            policy,
            (AuthoritativeCathedralOrdinaryChestType)nativeType,
            randomSeed,
            chest.PositionX,
            chest.PositionY,
            itemCount,
            itemBranch,
            TrapArmed: false,
            Player: new(true, false, player.BaseManaMaximum, player.BaseLifeMaximum)) {
                ChestObjectSlot = objectBinding.NativeObjectSlot,
            };
        var transaction = cathedralItems.PrepareChestLoot(chest.LevelId, request);
        var mutation = cathedralItems.CommitChestLoot(
            transaction,
            allocateEntityId,
            IsEntityIdOccupied,
            () => CanOperateNativeChest(chest),
            () => {
                if (!TryCommitNativeChestOpen(chest))
                    throw new InvalidOperationException("A prevalidated native chest could not commit its open transition.");
            });
        if (mutation is null)
            return null;

        foreach (var projection in mutation.Projections) {
            var item = ToWorldItem(chest.LevelId, projection);
            WorldItems.Add(item.EntityId, item);
        }
        ValidateCathedralItemProjection();
        return mutation;
    }
}
