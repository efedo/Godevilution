namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal IReadOnlySet<uint> CaptureNativeTrapInteractableEntityIds()
        => DiabloNativeChestInteractableEntitySet.Create(Objects.Values
            .Where(value => value.TrapArmed && IsNativeTrapTrigger(value)
                && CanOperateNativeTrapTrigger(value) && !value.Activated && !value.Destroyed)
            .Select(value => value.EntityId));

    internal AuthoritativeCathedralNativeTrapFacts RequireNativeTrapArrowOwner(
        uint sourceEntityId, int objectSlot)
    {
        var state = CaptureNativeTraps()?.SingleOrDefault(value =>
            value.Facts.TrapEntityId == sourceEntityId && value.Facts.TrapObjectSlot == objectSlot);
        if (state is null || state.Disposition != DiabloNativeTrapDisposition.Fired
            || state.TrapVar4 != 1 || state.TriggerTrapArmed
            || !OwnsNativePopulationLevel(state.Facts.LevelId))
            throw new InvalidDataException("A native trap arrow has no exact fired object owner.");
        return state.Facts;
    }

    internal (AuthoritativeCombatTarget? Monster, AuthoritativePlayerState? Player, short Hash)
        NativeTrapArrowOccupants(uint levelId, int x, int y,
            IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (cathedralItems is null || !OwnsNativePopulationLevel(levelId))
            throw new InvalidDataException("Native trap collision requires its exact item/population owner.");
        if ((uint)x >= 112 || (uint)y >= 112)
            return (null, null, 0);
        var occupancy = BuildCathedralItemOccupancy(cathedralItems.Profile, levelId, players);
        var index = y * 112 + x;
        var monsterRef = occupancy.MonsterRefs[index];
        var playerRef = occupancy.PlayerRefs[index];
        var monster = monsterRef != 0 ? CombatTargets.Values.SingleOrDefault(value =>
            value.LevelId == levelId && value.NativeInitialization?.Binding.NativeSlot == Math.Abs(monsterRef) - 1
            && (monsterRef > 0 || value.IsPetrified)) : null;
        var player = playerRef > 0 ? players.SingleOrDefault(value => value.LevelId == levelId
            && value.Life > 0 && value.PositionX == x && value.PositionY == y) : null;
        return (monster, player, checked((short)(monsterRef ^ playerRef)));
    }
}
