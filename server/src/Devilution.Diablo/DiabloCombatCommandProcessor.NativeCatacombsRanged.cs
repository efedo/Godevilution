using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    private bool IsNativeCatacombsRangedController(
        AuthoritativeCombatTarget monster,
        AuthoritativeMonsterMissileDefinition missile)
        => monster.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
            && world.OwnsNativePopulationLevel(monster.LevelId)
            && (AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(monster, missile)
                || AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(monster, missile));

    private bool TryStartNativeCatacombsRangedAction(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        AuthoritativeMonsterMissileDefinition missile,
        uint attackSeed)
    {
        if (!IsNativeCatacombsRangedController(monster, missile))
            return false;

        world.StartNativeCatacombsRangedAction(monster, missile, player, attackSeed);
        _ = AdvanceNativeCatacombsRangedAction(monster, [player]);
        return true;
    }

    /** Advances only an already selected C2 mode; this never re-runs AI admission or its RNG. */
    private bool AdvanceNativeCatacombsRangedAction(
        AuthoritativeCombatTarget monster,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!world.TryGetNativeCatacombsRangedAction(monster, out var action))
            return false;
        if (monster.HitPointsQ6 <= 0 || monster.NativeMeleeCursor is { HitRecovery: true }) {
            world.CancelNativeCatacombsRangedAction(monster.EntityId);
            return false;
        }
        if (monster.IsPetrified)
            return true;
        if (!TryGetMonsterMissile(monster, out var missile))
            throw new InvalidDataException("A pending Catacombs ranged action lost its admitted missile mapping.");

        var target = players.FirstOrDefault(value => value.EntityId == action.TargetEntityId
            && value.LevelId == monster.LevelId
            && value.CombatMode is not (DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit));
        var targetX = target?.FuturePositionX;
        var targetY = target?.FuturePositionY;
        var step = world.AdvanceNativeCatacombsRangedAction(monster, missile, targetX, targetY);
        if (step.Release) {
            var released = step.Action;
            if (AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(monster, missile)) {
                CreateNativeCatacombsArrowProjectile(monster, target, released, missile);
            } else if (AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(monster, missile)) {
                CreateNativeCatacombsMappedRangedProjectile(monster, released, missile);
            } else {
                throw new InvalidDataException("A pending Catacombs ranged action no longer matches a supported controller.");
            }
        }

        if (!step.Completed)
            return true;

        ResetNativeCatacombsRangedMovementState(monster, step.Action);
        ApplyNativeCatacombsGroupUnityAfterModeCompletion(monster);
        if (monster.ActiveForTicks == 0)
            return true;
        // AiRanged sees the just-finished Goat normal attack as var1 and
        // consumes GenerateRnd(20) in the same ProcessMonsters update. A zero
        // result leaves the actor eligible to choose another mode immediately.
        // Native delays only at the full active window; intermediate timers
        // route through the random-walk branch instead.
        if (!step.Action.UsesSpecialAttack && step.Action.NativeAi == "GoatRanged"
            && monster.ActiveForTicks == byte.MaxValue)
            monster.StartRangedDelay(ResolveRangedDelayFromRandom(missile));
        // Native UpdateModeStance repeats the selected AI in the same update
        // after Stand is restored. The action has already been removed, so the
        // normal processor path can dispatch that AI exactly once.
        return false;
    }

    /** Native StartStand restores var1/var2 to the non-movement state after ranged completion. */
    private static void ResetNativeCatacombsRangedMovementState(
        AuthoritativeCombatTarget monster,
        AuthoritativeNativeCatacombsRangedAction action)
    {
        if (action.NativeAi == "SkeletonRanged") {
            monster.RestoreSkeletonRangedMovementState(0, 0);
        } else if (action.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance) {
            monster.RestoreRangedAvoidanceMovementState(0, 0);
        }
    }

    private void CreateNativeCatacombsArrowProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState? player,
        AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile)
    {
        if (missile.ProjectileBehavior != AuthoritativeProjectileBehavior.MonsterArrow
            || missile.AttackMode != AuthoritativeMonsterRangedAttackMode.Ranged
            || missile.SpellId != 0
            || missile.DamageType != AuthoritativeDamageType.Physical)
            throw new InvalidDataException("The native Catacombs arrow mapping is inconsistent.");
        var destination = AuthoritativeNativeCatacombsArrowState.ResolveEffectiveDestination(
            monster.PositionX, monster.PositionY, action.TargetX, action.TargetY, monster.Direction);
        var direction16 = AuthoritativeNativeCatacombsArrowState.ResolveDirection16(
            monster.PositionX, monster.PositionY, destination.X, destination.Y);
        var velocity = CalculateVelocity(monster.PositionX, monster.PositionY,
            destination.X, destination.Y, 32);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(), monster.EntityId, action.TargetEntityId, 0,
            monster.LevelId, monster.PositionX, monster.PositionY, destination.X, destination.Y,
            0, AuthoritativeDamageType.Physical, 0, 256, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            NativeMissileId = "Arrow",
            Speed = 32,
            ImpactLifetime = 0,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = direction16 / 2,
        };
        world.RegisterNativeCatacombsArrow(projectile, monster, action, missile, direction16);
    }

    private void CreateNativeCatacombsMappedRangedProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile)
    {
        if (missile.ProjectileBehavior is not (AuthoritativeProjectileBehavior.Acid
            or AuthoritativeProjectileBehavior.MonsterMagic)
            || missile.SpellId == 0)
            throw new InvalidDataException("The native Catacombs missile mapping has no exact stable spell identity.");

        var spell = spells.Definitions.FirstOrDefault(definition => definition.SpellId == missile.SpellId);
        var speed = missile.ProjectileSpeed > 0 ? missile.ProjectileSpeed : 16;
        var lifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : missile.ProjectileLifetimeBase > 0 || missile.ProjectileLifetimeIntelligenceScale > 0
                ? checked(missile.ProjectileLifetimeBase
                    + missile.ProjectileLifetimeIntelligenceScale * monster.Intelligence)
                : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                    ? checked(5 * (monster.Intelligence + 4))
                    : spell?.ProjectileLifetime > 0 ? spell.ProjectileLifetime : 256;
        var impactLifetime = ResolveMonsterProjectileImpactLifetime(missile);
        var damageType = missile.DamageType
            ?? spell?.DamageType
            ?? (missile.MissileId == "MagmaBall" ? AuthoritativeDamageType.Fire : AuthoritativeDamageType.Acid);
        var direction = GetDirection(monster.PositionX, monster.PositionY, action.TargetX, action.TargetY);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX, monster.PositionY, action.TargetX, action.TargetY, speed);
        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        // Native AddAcid/AddMagmaBall call ProjectileMonsterDamage while
        // constructing their missile, using the shared source RNG stream.
        // PlaySfxLoc(SpellAcid) consumes one shared RNG value first, including
        // the native headless audio stub, so Acid and AcidUnique keep that order.
        if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid)
            _ = random.AdvanceRndSeed();
        var damage = missile.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(reuseReleasedId: true),
            monster.EntityId,
            action.TargetEntityId,
            missile.SpellId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            action.TargetX,
            action.TargetY,
            damage,
            damageType,
            0,
            checked((uint)lifetime)) {
            Behavior = missile.ProjectileBehavior,
            NativeMissileId = missile.MissileId,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = impactLifetime,
            ResistantDamageDivisor = 1,
        };
        var initialTravelQuanta = missile.InitialTravelQuanta > 0
            ? missile.InitialTravelQuanta
            : missile.MissileId == "MagmaBall" ? 3 : 0;
        if (initialTravelQuanta > 0) {
            projectile.TraveledX = checked(projectile.VelocityX * initialTravelQuanta);
            projectile.TraveledY = checked(projectile.VelocityY * initialTravelQuanta);
            var initialTile = CalculateMissileTile(
                projectile.StartX, projectile.StartY, projectile.TraveledX, projectile.TraveledY);
            projectile.PositionX = initialTile.X;
            projectile.PositionY = initialTile.Y;
        }
        world.Projectiles.Add(projectile.EntityId, projectile);
    }
}
