namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal AuthoritativeCathedralItemSarcophagusMutationResult? TryOperateNativeCathedralSarcophagus(
        AuthoritativeWorldObject sarcophagus,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        Func<uint> allocateEntityId)
    {
        ArgumentNullException.ThrowIfNull(sarcophagus);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(allocateEntityId);
        if (cathedralItems is null || !CanOperateNativeSarcophagus(sarcophagus)
            || !TryGetNativeSarcophagusOperationFacts(sarcophagus, out var facts))
            return null;
        ValidateCathedralItemProjection();

        var levelProfile = cathedralItems.Profile.LevelProfiles.SingleOrDefault(
            value => value.LevelId == sarcophagus.LevelId && value.Depth == 1);
        if (levelProfile is null || !levelProfile.ObjectBindings.Any(value =>
                value.EntityId == sarcophagus.EntityId
                && value.NativeObjectSlot == facts.ObjectSlot
                && value.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                && value.PositionX == sarcophagus.PositionX && value.PositionY == sarcophagus.PositionY))
            return null;

        var localParticipants = participants.Contains(player)
            ? participants
            : participants.Append(player).ToArray();
        var occupancy = BuildCathedralItemOccupancy(
            cathedralItems.Profile, sarcophagus.LevelId, localParticipants);
        var worldState = cathedralItems.BuildWorldState(sarcophagus.LevelId, occupancy);
        var policy = cathedralItems.Profile.ItemPolicy with {
            Depth = 1,
            CurrentDungeonLevel = 1,
            Spawn = false,
            QuestEnabled = false,
            SetLevel = false,
        };
        var request = new AuthoritativeCathedralSarcophagusOperationRequest(
            worldState, cathedralItems.Profile.ItemCatalog, policy, facts,
            GetNativeSarcophagusSpawnAvailability(sarcophagus, localParticipants),
            new(true, false, player.BaseManaMaximum, player.BaseLifeMaximum));
        var transaction = cathedralItems.PrepareSarcophagusOperation(sarcophagus.LevelId, request);
        var mutation = cathedralItems.CommitSarcophagusOperation(
            transaction, allocateEntityId, IsEntityIdOccupied,
            () => CanCommitNativeSarcophagusOperation(sarcophagus, transaction.Operation, localParticipants),
            () => {
                if (!TryCommitNativeSarcophagusOperation(sarcophagus, transaction.Operation, localParticipants))
                    throw new InvalidOperationException("A prevalidated native sarcophagus could not commit its operation.");
            });
        if (mutation is null)
            return null;

        foreach (var projection in mutation.Projections) {
            var item = ToWorldItem(sarcophagus.LevelId, projection);
            WorldItems.Add(item.EntityId, item);
        }
        ValidateCathedralItemProjection();
        return mutation;
    }
}
