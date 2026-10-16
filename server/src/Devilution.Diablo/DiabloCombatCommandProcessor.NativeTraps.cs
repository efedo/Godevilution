using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native ProcessObjects follows monsters and precedes this tick's missile processing. */
    private void AdvanceNativeTraps(IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var firing in world.ConsumeReadyNativeTraps(players)) {
            var owner = world.RequireNativeTrapArrowOwner(firing.TrapEntityId, firing.TrapObjectSlot);
            var velocity = CalculateVelocity(firing.SourceX, firing.SourceY, firing.TargetX, firing.TargetY, 32);
            var projectile = new AuthoritativeProjectile(AllocateProjectileEntityId(), firing.TrapEntityId,
                owner.TriggerEntityId, 0, firing.LevelId, firing.SourceX, firing.SourceY,
                firing.TargetX, firing.TargetY, 0, AuthoritativeDamageType.Physical, 0, 256,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                NativeTrapArrow = new(firing.TrapObjectSlot, 0, 0),
                VelocityX = velocity.X,
                VelocityY = velocity.Y,
                OriginalDirection = firing.Direction,
                Speed = 32,
            };
            world.Projectiles.Add(projectile.EntityId, projectile);
        }
    }

    internal static (int X, int Y) NativeTrapArrowVelocity(int sourceX, int sourceY, int targetX, int targetY)
        => CalculateVelocity(sourceX, sourceY, targetX, targetY, 32);

    internal static (int X, int Y) NativeTrapArrowTile(int sourceX, int sourceY, int traveledX, int traveledY)
        => CalculateMissileTile(sourceX, sourceY, traveledX, traveledY);

    private void AdvanceNativeTrapArrow(ulong tick, AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var state = projectile.NativeTrapArrow
            ?? throw new InvalidDataException("Native trap flight is missing its source clock.");
        var facts = world.RequireNativeTrapArrowOwner(projectile.SourceEntityId, state.NativeObjectSlot);
        if (projectile.LevelId != facts.LevelId || projectile.RemainingTicks == 0)
            throw new InvalidDataException("Native trap flight left its owned live level.");
        if (!players.Any(value => value.LevelId == projectile.LevelId))
            return;
        projectile.RemainingTicks--;
        projectile.NativeTrapArrow = state with { MissileDistance = checked(state.MissileDistance + 1) };
        var tiles = MoveNativeProjectile(projectile);
        if (tiles.Count == 0) {
            var occupants = world.NativeTrapArrowOccupants(projectile.LevelId,
                projectile.PositionX, projectile.PositionY, players);
            if (state.LastCollisionTargetHash != occupants.Hash
                && ResolveNativeTrapArrowTile(tick, projectile, players, events)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
        } else {
            foreach (var tile in tiles) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)) {
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                if (tile.X == projectile.StartX && tile.Y == projectile.StartY)
                    continue;
                if (ResolveNativeTrapArrowTile(tick, projectile, players, events)) {
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
            }
        }
        var finalOccupants = world.NativeTrapArrowOccupants(projectile.LevelId,
            projectile.PositionX, projectile.PositionY, players);
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with {
            LastCollisionTargetHash = finalOccupants.Hash,
        };
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Monster, then positive player occupancy, then missile blocking on each native tile. */
    private bool ResolveNativeTrapArrowTile(ulong tick, AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var occupants = world.NativeTrapArrowOccupants(projectile.LevelId,
            projectile.PositionX, projectile.PositionY, players);
        var stopped = false;
        var localEvents = new List<DiabloGameplayEvent>();
        var observer = players.First(value => value.LevelId == projectile.LevelId);
        var distance = checked((int)projectile.NativeTrapArrow!.MissileDistance);
        if (occupants.Monster is { } monster)
            stopped |= ResolveNativeTrapArrowMonsterHit(observer, monster, distance, localEvents);
        if (occupants.Player is { } player)
            stopped |= ResolveNativeTrapArrowPlayerHit(projectile.SourceEntityId, player,
                distance, tick, players, localEvents);
        foreach (var gameplayEvent in localEvents)
            events.Add(new(observer, gameplayEvent));
        if (world.IsMissileBlocked(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            // Native-owned barrels remain excluded from generic projectile breaking.
            BreakMissileBlockingObject(projectile.LevelId, projectile.PositionX, projectile.PositionY);
            stopped = true;
        }
        return stopped;
    }

    private bool ResolveNativeTrapArrowMonsterHit(AuthoritativePlayerState observer,
        AuthoritativeCombatTarget target, int distance, ICollection<DiabloGameplayEvent> events)
    {
        if (target.HitPointsQ6 < 64 || target.DeathTicksRemaining > 0
            || target.DialogueMessageId != 0 || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target))
            return false;
        var resistance = GetMonsterResistance(target, AuthoritativeDamageType.Physical);
        if (resistance >= 100)
            return false;
        var hit = random.GenerateRnd(100);
        var chance = Math.Clamp(90 - GetEffectiveMonsterArmorClass(target) - distance, 5, 95);
        if (UsesNativeAi(target, "Gargoyle") && target.GargoyleSpecialActive) {
            target.GargoyleSpecialActive = false;
            return true;
        }
        if (hit >= chance && !target.IsPetrified)
            return false;
        var damage = checked(random.RandomIntBetween(world.NativePopulationDepth(target.LevelId),
            2 * world.NativePopulationDepth(target.LevelId)) * 64);
        if (resistance > 0)
            damage /= 4;
        ApplyResolvedDamageQ6(observer, target, damage, events,
            nativeTrap: true, nativeTrapResisted: resistance > 0);
        if (target.HitPointsQ6 >= 64
            && (resistance > 0 || target.NativeInitialization?.Binding.NativeTypeId != 109))
            random.AdvanceRndSeed();
        return true;
    }

    /** PlayerMHit(nullptr, Arrow, depth..2*depth, unshifted); no source-monster bonuses. */
    internal bool ResolveNativeTrapArrowPlayerHit(uint sourceEntityId, AuthoritativePlayerState target,
        int distance, ulong tick, IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (target.Life < 64 || IsPlayerProtectedByFlash(target) || IsPlayerEtherealized(target))
            return false;
        var hit = random.GenerateRnd(100);
        var canBlock = target.CombatMode is DiabloPlayerCombatMode.Stand or DiabloPlayerCombatMode.Attack
            && CanBlock(target);
        var block = canBlock ? random.GenerateRnd(100) : 100;
        if (hit >= Math.Max(100 - target.ArmorClass / 2 - distance * 2, 10))
            return false;
        var damage = random.RandomIntBetween(64 * world.NativePopulationDepth(target.LevelId),
            128 * world.NativePopulationDepth(target.LevelId));
        if (HasUsableEquipmentFlag(target, AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag))
            damage /= 2;
        damage = Math.Max(64, checked(damage
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment) * 64));
        var targetClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0), 0, 100);
        if (block < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }
        var result = playerDamage.Apply(target, damage);
        if (result.IncomingDamageQ6 > 0)
            events.Add(new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, sourceEntityId,
                target.EntityId, DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) {
                AmountQ6 = result.IncomingDamageQ6,
            });
        if (target.Life < 64) {
            target.Life = 0;
            StartPlayerDeath(target, sourceEntityId, tick, players, PlayerDeathReason.MonsterOrTrap);
        } else {
            random.AdvanceRndSeed();
            if (ShouldStartHit(target, targetClass, result.IncomingDamageQ6))
                StartHit(target, targetClass, tick);
        }
        return true;
    }
}
