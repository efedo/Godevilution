using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native ProcessObjects follows monsters and precedes this tick's missile processing. */
    private void AdvanceNativeTraps(IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var firing in world.ConsumeReadyNativeTraps(players)) {
            var owner = world.RequireNativeTrapArrowOwner(firing.TrapEntityId, firing.TrapObjectSlot);
            var projectile = AddObjectTrapMissile(AllocateProjectileEntityId(), firing.TrapEntityId,
                owner.TriggerEntityId, firing.LevelId, firing.TrapObjectSlot,
                firing.SourceX, firing.SourceY, firing.TargetX, firing.TargetY, firing.Direction,
                firing.MissileTypeId, intrinsicChest: false, random, world.NativePopulationDepth(firing.LevelId));
            world.Projectiles.Add(projectile.EntityId, projectile);
        }
    }

    internal static (int X, int Y) NativeTrapArrowVelocity(int sourceX, int sourceY, int targetX, int targetY)
        => CalculateVelocity(sourceX, sourceY, targetX, targetY, 32);

    internal static (int X, int Y) NativeTrapArrowTile(int sourceX, int sourceY, int traveledX, int traveledY)
        => CalculateMissileTile(sourceX, sourceY, traveledX, traveledY);

    /** PlayerMHit(nullptr, Arrow, depth..2*depth, unshifted); no source-monster bonuses. */
    internal bool ResolveNativeTrapArrowPlayerHit(uint sourceEntityId, AuthoritativePlayerState target,
        int distance, ulong tick, IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        return PlayerMHit(sourceEntityId, target, world.NativePopulationDepth(target.LevelId),
            2 * world.NativePopulationDepth(target.LevelId), distance, AuthoritativeDamageType.Physical,
            arrow: true, tick, players, events);
    }
}
