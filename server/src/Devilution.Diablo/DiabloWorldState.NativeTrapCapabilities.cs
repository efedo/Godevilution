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

    internal uint RequireNativeTrapMissileOwner(AuthoritativeProjectile projectile)
    {
        var missile = projectile.NativeTrapArrow
            ?? throw new InvalidDataException("An object missile is missing its retained state.");
        if (missile.IntrinsicChest) {
            if (!Objects.TryGetValue(projectile.SourceEntityId, out var chest)
                || !chest.Activated || chest.TrapArmed || chest.NativeObjectId is < 68 or > 70
                || projectile.TargetEntityId != chest.EntityId || projectile.LevelId != chest.LevelId
                || projectile.StartX != chest.PositionX || projectile.StartY != chest.PositionY
                || NativePopulationForLevel(chest.LevelId) is not { } owner
                || !owner.TryGetNativeChestFacts(chest, out var facts)
                || facts!.ObjectSlot != missile.NativeObjectSlot
                || missile.MissileTypeId != (facts.Var4 == 0 ? 0 : 27))
                throw new InvalidDataException("An intrinsic trap missile has no exact opened chest owner.");
            return chest.LevelId;
        }
        var wall = RequireNativeTrapArrowOwner(projectile.SourceEntityId, missile.NativeObjectSlot);
        if (projectile.LevelId != wall.LevelId || projectile.TargetEntityId != wall.TriggerEntityId
            || projectile.StartX != wall.SourceX || projectile.StartY != wall.SourceY
            || missile.MissileTypeId != wall.MissileTypeId)
            throw new InvalidDataException("A wall trap missile changed its generated missile or source.");
        return wall.LevelId;
    }

    internal (AuthoritativeCombatTarget? Monster, AuthoritativePlayerState? Player, short Hash)
        NativeTrapArrowOccupants(uint levelId, int x, int y,
            IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (cathedralItems is null || !OwnsNativePopulationLevel(levelId))
            throw new InvalidDataException("Native trap collision requires its exact item/population owner.");
        // Validates the owned floor's object and geometry projection before reading actors.
        _ = BuildCathedralItemOccupancy(cathedralItems.Profile, levelId, players);
        return NativeActorOccupants(levelId, x, y, players);
    }

    /** The native monster and player on a tile, with the signed dMonster ^ dPlayer collision hash. */
    internal (AuthoritativeCombatTarget? Monster, AuthoritativePlayerState? Player, short Hash)
        NativeActorOccupants(uint levelId, int x, int y,
            IReadOnlyList<AuthoritativePlayerState> players)
    {
        if ((uint)x >= 112 || (uint)y >= 112)
            return (null, null, 0);
        var (playerRefs, monsterRefs) = BuildNativeActorRefs(levelId, players);
        var index = y * 112 + x;
        var monsterRef = monsterRefs[index];
        var playerRef = playerRefs[index];
        var monster = monsterRef != 0 ? CombatTargets.Values.SingleOrDefault(value =>
            value.LevelId == levelId && value.NativeInitialization?.Binding.NativeSlot == Math.Abs(monsterRef) - 1
            && (monsterRef > 0 || value.IsPetrified)) : null;
        var player = playerRef > 0 ? players.SingleOrDefault(value => value.LevelId == levelId
            && value.Life > 0 && value.PositionX == x && value.PositionY == y) : null;
        return (monster, player, checked((short)(monsterRef ^ playerRef)));
    }
}
