using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native objects.cpp ProcessObjects/UpdateBurningCrossDamage: every object tick, only the tile directly north of the cross. */
    private void AdvanceNativeBurningCrosses(ulong tick, IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var cross in world.NativeBurningCrossesInActiveOrder()) {
            world.ValidateNativePopulationObject(cross);
            foreach (var player in players) {
                if (player.LevelId != cross.LevelId || player.CombatMode == DiabloPlayerCombatMode.Death
                    || player.PositionX != cross.PositionX || player.PositionY != cross.PositionY - 1)
                    continue;
                var damage = world.NativePopulationForLevel(cross.LevelId)!.Family == AuthoritativeNativePopulationFamily.Cathedral ? 6 : 8;
                var resistance = GetPlayerResistance(player, GetPlayerClass(player), AuthoritativeDamageType.Fire);
                if (resistance > 0) damage -= resistance * damage / 100;
                player.EnableNativeResourceBases();
                var result = playerDamage.Apply(player, damage);
                if (result.IncomingDamageQ6 > 0)
                    events.Add(new(player, new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, cross.EntityId,
                        player.EntityId, DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) {
                        AmountQ6 = result.IncomingDamageQ6,
                    }));
                // Native hasNoLife tests whole hit points, including a positive Q6 remainder.
                if (player.Life < 64) {
                    player.Life = 0;
                    StartPlayerDeath(player, cross.EntityId, tick, players, PlayerDeathReason.MonsterOrTrap);
                }
                // Native Say(Argh), unlike ArghClang, has no randomized sound and starts no hit recovery.
            }
        }
    }

    /** Native missiles.cpp ProcessManaShield: after all object/missile damage, discard shields below one whole mana. */
    private static void ProcessManaShield(IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var player in players) {
            if (player.ManaShieldActive && player.Mana < 64)
                player.ManaShieldActive = false;
        }
    }
}
