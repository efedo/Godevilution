namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal AuthoritativeCathedralItemBarrelMutationResult? TryBreakNativeCathedralBarrel(
        AuthoritativeWorldObject barrel,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        uint initialRandomState,
        Func<uint> allocateEntityId)
    {
        if (cathedralItems is null || !CanBreakNativeBarrel(barrel)
            || !TryGetNativeBarrelFacts(barrel, out var facts))
            return null;
        ValidateCathedralItemProjection();
        var localPlayers = participants.Contains(player) ? participants : participants.Append(player).ToArray();
        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, barrel.LevelId, localPlayers);
        var request = new AuthoritativeCathedralBarrelOperationRequest(
            cathedralItems.BuildWorldState(barrel.LevelId, occupancy),
            cathedralItems.Profile.ItemCatalog,
            cathedralItems.Profile.ItemPolicy with {
                Depth = 1, CurrentDungeonLevel = 1, Spawn = false, QuestEnabled = false, SetLevel = false,
            },
            facts, GetNativeBarrelSpawnAvailability(barrel, localPlayers), initialRandomState,
            new(true, false, player.BaseManaMaximum, player.BaseLifeMaximum));
        var transaction = cathedralItems.PrepareBarrelOperation(barrel.LevelId, request);
        var mutation = cathedralItems.CommitBarrelOperation(
            transaction, allocateEntityId, IsEntityIdOccupied,
            () => CanCommitNativeBarrelBreak(barrel, transaction.Operation, localPlayers),
            () => {
                if (!TryCommitNativeBarrelBreak(barrel, transaction.Operation, localPlayers))
                    throw new InvalidOperationException("A prevalidated native barrel lost its owner before commit.");
            });
        if (mutation is null)
            return null;
        foreach (var projection in mutation.Projections) {
            var item = ToWorldItem(barrel.LevelId, projection);
            WorldItems.Add(item.EntityId, item);
        }
        ValidateCathedralItemProjection();
        return mutation;
    }

    /** Current positive native tile occupants; moving reservations are deliberately excluded. */
    internal (AuthoritativeCombatTarget? Monster, AuthoritativePlayerState? Player) NativeBarrelBlastOccupants(
        uint levelId, int x, int y, IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (cathedralItems is null || !OwnsNativePopulationLevel(levelId))
            throw new InvalidOperationException("Native barrel damage requires its shared population/item owner.");
        if ((uint)x >= 112 || (uint)y >= 112)
            return (null, null);
        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, levelId, players);
        var index = y * 112 + x;
        var reference = occupancy.MonsterRefs[index];
        var monster = reference > 0 ? CombatTargets.Values.SingleOrDefault(value =>
            value.LevelId == levelId && value.NativeInitialization?.Binding.NativeSlot == reference - 1) : null;
        var player = occupancy.PlayerRefs[index] > 0
            ? players.SingleOrDefault(value => value.LevelId == levelId && value.Life > 0)
            : null;
        return (monster, player);
    }
}
