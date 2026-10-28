using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Spell missile and controller creation (native missiles.cpp Add* functions). */
public sealed partial class DiabloCombatCommandProcessor
{
    private AuthoritativeProjectile CreateChargedBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var pathIndex = random.GenerateRnd(
            spell.ChargedBoltPathRandomRange > 0
                ? spell.ChargedBoltPathRandomRange
                : 15) + 1;
        var damage = random.GenerateRnd(
            player.Attributes.Magic.Current / spell.RandomMagicDamageDivisor) + 1;
        _ = random.RandomIntBetween(1, 8);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            PathIndex = pathIndex,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateFireboltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateHolyBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.HolyBolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateBloodStarProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = checked(
            player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            - player.Attributes.Magic.Current / spell.MagicDamageReductionDivisor);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            Math.Max(0, damage),
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.BloodStar,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateBoneSpiritProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.BoneSpirit,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateElementalProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            spell.ElementalCharacterLevelDamageMultiplier
                * (checked((int)player.CharacterLevel) + damage)
            + spell.DamageAmount);
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage /= spell.MagicDamageDivisor;

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Elemental,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
            ElementalOriginX = velocityTargetX,
            ElementalOriginY = velocityTargetY,
        };
    }

    private AuthoritativeProjectile CreateWallController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        (int X, int Y) spreadPosition,
        int wallDirection)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            spreadPosition.X,
            spreadPosition.Y,
            spreadPosition.X,
            spreadPosition.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.WallControlLifetime)) {
            Behavior = spell.ProjectileBehavior == AuthoritativeProjectileBehavior.LightningWallController
                ? AuthoritativeProjectileBehavior.LightningWallController
                : AuthoritativeProjectileBehavior.FireWallController,
            StartX = player.PositionX,
            StartY = player.PositionY,
            OriginalDirection = wallDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
            WallLeftX = spreadPosition.X,
            WallLeftY = spreadPosition.Y,
            WallRightX = spreadPosition.X,
            WallRightY = spreadPosition.Y,
            WallLeftDirection = RotateDirection(wallDirection, -2),
            WallRightDirection = RotateDirection(wallDirection, 2),
        };
    }

    private static AuthoritativeProjectile CreateRingOfFireController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.RingOfFireController,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateWallSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int wallDirection,
        int wallHitDirection)
    {
        var durationUnits = checked(
            spell.WallSegmentBaseDuration
            + controller.EffectiveSpellLevel * spell.WallSegmentSpellLevelDurationStep
            + checked((int)owner.CharacterLevel)
                * spell.WallSegmentCharacterLevelDurationStep);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + spell.DamageBonus
                + checked((int)owner.CharacterLevel)
                    * spell.WallCharacterLevelDamageMultiplier)
            * spell.DamageScale);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            damage,
            spell.DamageType,
            0,
            checked((uint)(durationUnits * spell.WallSegmentDurationScale))) {
            Behavior = AuthoritativeProjectileBehavior.FireWallSegment,
            OriginalDirection = wallDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
            WallHitDirection = wallHitDirection,
        };
    }

    private AuthoritativeProjectile CreateFireballProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + checked((int)player.CharacterLevel)
                    / spell.CharacterLevelDamageDivisor)
            * spell.DamageScale
            + spell.DamageBonus);
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    /** Resolves native Rune of Fire as an immediate BigExplosion-style splash. */
    private AuthoritativeProjectile CreateRuneFireExplosion(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + checked((int)player.CharacterLevel)
                    / spell.CharacterLevelDamageDivisor)
            * spell.DamageScale
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            targetX,
            targetY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            1) {
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            OriginalDirection = GetDirection(
                player.PositionX,
                player.PositionY,
                targetX,
                targetY),
            Speed = 1,
            ImpactLifetime = 1,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private static bool IsRuneFireSpell(AuthoritativeSpellDefinition spell)
    {
        return spell.ConsumableBehavior == AuthoritativeConsumableBehavior.RuneFire;
    }

    private AuthoritativeProjectile CreateLightningController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY,
        AuthoritativeProjectileBehavior behavior = AuthoritativeProjectileBehavior.Lightning)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        _ = random.RandomIntBetween(1, 8);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = behavior,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateChainLightningController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ChainLightningControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ChainLightningController,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateNovaController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            damage
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        damage /= spell.MagicDamageDivisor;
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage * spell.DamageScale);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.NovaControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.NovaController,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateNovaBall(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY);
        var (velocityX, velocityY) = CalculateVelocity(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        var ball = new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            controller.Damage,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.NovaBall,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
        _ = random.RandomIntBetween(1, 8);
        return ball;
    }

    private AuthoritativeProjectile CreateApocalypseController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < checked((int)player.CharacterLevel);
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(damage + checked((int)player.CharacterLevel));
        var minimumX = Math.Max(
            player.PositionX - spell.ApocalypseSearchRadius,
            1);
        var minimumY = Math.Max(
            player.PositionY - spell.ApocalypseSearchRadius,
            1);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.ApocalypseControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseController,
            StartX = player.PositionX,
            StartY = player.PositionY,
            EffectiveSpellLevel = effectiveSpellLevel,
            ApocalypseScanX = minimumX,
            ApocalypseScanY = minimumY,
        };
    }

    private static AuthoritativeProjectile CreateApocalypseBoom(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int positionX,
        int positionY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            controller.Damage,
            AuthoritativeDamageType.Physical,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateMonsterApocalypseBoom(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState target)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            target.EntityId,
            controller.SpellId,
            controller.LevelId,
            target.PositionX,
            target.PositionY,
            target.PositionX,
            target.PositionY,
            controller.Damage,
            AuthoritativeDamageType.Physical,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
        };
    }

    private static AuthoritativeProjectile CreateRageController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var lifetime = checked(
            spell.RageControllerLifetime
            + checked((int)player.CharacterLevel * spell.RageCharacterLevelLifetimeScale));
        var selfDamage = checked(
            (int)player.CharacterLevel * spell.RageSelfDamageCharacterLevel);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            selfDamage,
            spell.DamageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.RageController,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateGuardianBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int originX,
        int originY,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            originX,
            originY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == originX && velocityTargetY == originY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.GuardianProjectileSpeedLevelStep,
                spell.GuardianProjectileSpeedMaximumBonus));
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            + spell.DamageBonus);
        var (velocityX, velocityY) = CalculateVelocity(
            originX,
            originY,
            velocityTargetX,
            velocityTargetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            originX,
            originY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateGuardianController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        (int X, int Y) spawnPosition)
    {
        var durationUnits = Math.Min(
            spell.GuardianMaximumDuration,
            effectiveSpellLevel
                + checked((int)player.CharacterLevel / spell.GuardianCharacterLevelDurationDivisor));
        var duration = Math.Max(
            spell.GuardianMinimumDuration,
            checked(durationUnits * spell.GuardianDurationTickScale));
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            spawnPosition.X,
            spawnPosition.Y,
            spawnPosition.X,
            spawnPosition.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)duration)) {
            Behavior = AuthoritativeProjectileBehavior.GuardianController,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateFlameWaveController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.FlameWaveControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.FlameWaveController,
            OriginalDirection = (int)player.FacingDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateFlameWaveSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY)
    {
        var direction = controller.OriginalDirection;
        var displacement = DirectionDisplacements[direction];
        var targetX = checked(positionX + displacement.X);
        var targetY = checked(positionY + displacement.Y);
        var (velocityX, velocityY) = CalculateVelocity(
            positionX,
            positionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + checked((int)owner.CharacterLevel)
                * spell.FlameWaveCharacterLevelDamageBonus
            + spell.DamageBonus);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.SecondaryProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.FlameWaveSegment,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateInfernoController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(player.PositionX, player.PositionY, targetX, targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.InfernoController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateInfernoSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner)
    {
        var characterLevelDamage = random.GenerateRnd(Math.Max(1, (int)owner.CharacterLevel));
        var randomDamage = random.GenerateRnd(spell.RandomDamageRange);
        var damage = checked(
            (characterLevelDamage + randomDamage) * spell.DamageScale
            + spell.DamageBonus);
        var lifetime = checked(
            spell.SecondaryProjectileLifetime
            + controller.PathIndex * spell.InfernoSegmentLifetimeStep);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.PositionX,
            controller.PositionY,
            damage,
            controller.DamageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.InfernoSegment,
            ResistantDamageDivisor = controller.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateFlashLayer(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        AuthoritativeProjectileBehavior behavior)
    {
        var damage = GenerateFlashDamage(player, spell, effectiveSpellLevel);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = behavior,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    /** Native CounselorAi emits fixed-damage Flash layers from the monster. */
    private void CreateMonsterFlashLayers(AuthoritativeCombatTarget monster)
    {
        var flashSpellId = monsterFlashSpellId;
        var hasFlashSpell = spells.TryGet(flashSpellId, out var flashSpell);
        var damageType = hasFlashSpell
            ? flashSpell.DamageType
            : AuthoritativeDamageType.Magic;
        var areaRadius = hasFlashSpell ? flashSpell.AreaRadius : 1;
        var lifetime = hasFlashSpell && flashSpell.ProjectileLifetime > 0
            ? flashSpell.ProjectileLifetime
            : 19;
        var monsterDamageLevelScale = hasFlashSpell
            ? flashSpell.FlashMonsterDamageLevelScale
            : 2;
        var damage = checked(GetEffectiveMonsterLevel(monster) * monsterDamageLevelScale);

        foreach (var behavior in new[] {
                     AuthoritativeProjectileBehavior.FlashBottom,
                     AuthoritativeProjectileBehavior.FlashTop,
                 }) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                new AuthoritativeProjectile(
                    projectileEntityId,
                    monster.EntityId,
                    0,
                    flashSpellId,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    monster.PositionX,
                    monster.PositionY,
                    damage,
                    damageType,
                    areaRadius,
                    checked((uint)lifetime),
                    allowZeroSpellId: true) {
                    Behavior = behavior,
                    ResistantDamageDivisor = 1,
                });
        }
    }

    private int GenerateFlashDamage(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var characterLevel = checked((int)player.CharacterLevel);
        var iterationCount = checked(
            characterLevel / spell.RandomDamageIterationsCharacterLevelDivisor
            + spell.RandomDamageIterationsBonus);
        var damage = 0;
        for (var iteration = 0; iteration < iterationCount; iteration++)
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        damage = checked(
            damage
            + characterLevel / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        for (var spellLevel = 0; spellLevel < effectiveSpellLevel; spellLevel++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage + damage * spell.PostScaleDamagePercent / 100);
        return checked(damage * spell.DamageScale);
    }

    private bool IsMatchingChargedItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingChargedItem(state, spellId, items);
    }

    private bool IsMatchingConsumableItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingConsumableItem(state, spellId, spells, items);
    }

    private static void ConsumeCastSource(
        AuthoritativePlayerState player,
        ResolvedCastSource source,
        int manaCost)
    {
        switch (source.Kind) {
        case DiabloCastSourceKind.Ability:
            return;
        case DiabloCastSourceKind.MemorizedSpell:
            player.Mana -= manaCost;
            return;
        case DiabloCastSourceKind.ChargedItem:
            DiabloPlayerItemAccess.Replace(
                player,
                source.ItemReference!.Value,
                source.Item! with {
                    State = source.Item.State with { Charges = source.Item.State.Charges - 1 },
                });
            return;
        case DiabloCastSourceKind.ConsumableItem:
            DiabloPlayerItemAccess.Remove(player, source.ItemReference!.Value);
            return;
        default:
            throw new InvalidOperationException("A resolved cast source must be consumable.");
        }
    }

    private readonly record struct ResolvedCastSource(
        DiabloCastSourceKind Kind,
        DiabloPlayerItemReference? ItemReference,
        OwnedStoreItem? Item);

    private AuthoritativeProjectile CreateMovementController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        (int X, int Y) destination,
        AuthoritativeProjectileBehavior behavior)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            destination.X,
            destination.Y,
            destination.X,
            destination.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = behavior,
        };
    }

    private static AuthoritativeProjectile CreateResurrectionBeam(
        uint projectileEntityId,
        AuthoritativePlayerState caster,
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        int positionX,
        int positionY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            caster.EntityId == 0 ? 1U : caster.EntityId,
            target.EntityId,
            spell.SpellId,
            caster.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ResurrectionBeam,
        };
    }

    private static AuthoritativeProjectile CreateStoneCurseController(
        uint projectileEntityId,
        AuthoritativePlayerState caster,
        (AuthoritativeCombatTarget Target, int X, int Y) target,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var durationUnits = Math.Min(
            (long)effectiveSpellLevel + spell.StoneCurseBaseDuration,
            spell.StoneCurseMaximumDuration);
        var duration = checked((uint)(durationUnits * spell.StoneCurseDurationScale));
        return new AuthoritativeProjectile(
            projectileEntityId,
            caster.EntityId == 0 ? 1U : caster.EntityId,
            target.Target.EntityId,
            spell.SpellId,
            caster.LevelId,
            target.X,
            target.Y,
            target.X,
            target.Y,
            0,
            AuthoritativeDamageType.Magic,
            0,
            duration) {
            Behavior = AuthoritativeProjectileBehavior.StoneCurse,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private void ApplyResurrection(
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var destination = FindResurrectionPosition(target, spell, players);
        target.PositionX = destination.X;
        target.PositionY = destination.Y;
        target.Life = Math.Min(
            target.LifeMaximum,
            checked(spell.ResurrectionLife * spell.ResurrectionLifeScale));
        target.Mana = 0;
        target.ManaShieldActive = false;
        DiabloPlayerActionLifecycle.Cancel(target);
        target.CombatMode = DiabloPlayerCombatMode.Stand;
        target.CombatModeEndTick = 0;
    }

    private (int X, int Y) FindResurrectionPosition(
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var initialOffsets = spell.ResurrectionInitialOffsets.Count > 0
            ? spell.ResurrectionInitialOffsets
            : ResurrectionInitialOffsets;
        foreach (var offset in initialOffsets) {
            var candidateX = checked(target.PositionX + offset.X);
            var candidateY = checked(target.PositionY + offset.Y);
            if (IsResurrectionPositionAvailable(target, candidateX, candidateY, players))
                return (candidateX, candidateY);
        }

        return DeterministicGridCrawl.FindClosestValidPosition(
                target.PositionX,
                target.PositionY,
                1,
                spell.ResurrectionSearchRadius,
                (candidateX, candidateY) =>
                    IsResurrectionPositionAvailable(target, candidateX, candidateY, players)
                    && !IsPortalPosition(target.LevelId, candidateX, candidateY))
            ?? (0, 0);
    }

    private bool IsResurrectionPositionAvailable(
        AuthoritativePlayerState target,
        int positionX,
        int positionY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return world.IsWalkable(target.LevelId, positionX, positionY)
            && !world.IsOccupied(
                target.LevelId,
                positionX,
                positionY,
                players.Where(player => player.Life > 0),
                target.EntityId);
    }

    private bool IsPortalPosition(uint levelId, int positionX, int positionY)
    {
        return world.Portals.Values.Any(portal =>
            IsPortalEndpoint(
                portal.SourceLevelId,
                portal.SourcePositionX,
                portal.SourcePositionY,
                levelId,
                positionX,
                positionY)
            || IsPortalEndpoint(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY,
                levelId,
                positionX,
                positionY));
    }

    private static bool IsPortalEndpoint(
        uint endpointLevelId,
        int endpointX,
        int endpointY,
        uint levelId,
        int positionX,
        int positionY)
    {
        return endpointLevelId == levelId
            && ((endpointX == positionX && endpointY == positionY)
                || (endpointX + 1 == positionX && endpointY + 1 == positionY));
    }

    private (int X, int Y)? FindClosestTeleportDestination(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        int searchRadius)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                world.IsWalkable(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players.Where(candidate => candidate.Life > 0),
                    player.EntityId));
    }

    private (int X, int Y)? FindWallSpreadPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            5,
            (candidateX, candidateY) =>
                (candidateX != player.PositionX || candidateY != player.PositionY)
                && !world.IsMissileGeometryBlocked(
                    player.LevelId,
                    candidateX,
                    candidateY)
                && world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private (int X, int Y)? FindRuneFirePosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            9,
            (candidateX, candidateY) =>
                world.ContainsPosition(player.LevelId, candidateX, candidateY)
                && !world.IsMissileGeometryBlocked(
                    player.LevelId,
                    candidateX,
                    candidateY)
                && !world.TryGetMissileBlockingObject(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    out _)
                && !world.Projectiles.Values.Any(projectile =>
                    projectile.LevelId == player.LevelId
                    && projectile.PositionX == candidateX
                    && projectile.PositionY == candidateY));
    }

    private (int X, int Y)? FindGuardianSpawnPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            spell.GuardianSpawnSearchRadius,
            (candidateX, candidateY) =>
                world.ContainsPosition(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    player.EntityId)
                && !world.Projectiles.Values.Any(projectile =>
                    projectile.LevelId == player.LevelId
                    && projectile.PositionX == candidateX
                    && projectile.PositionY == candidateY)
                && !world.IsMissileBlocked(player.LevelId, candidateX, candidateY)
                && world.HasGuardianSpawnLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private (int X, int Y)? FindGolemSpawnPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            5,
            (candidateX, candidateY) =>
                world.IsWalkable(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    player.EntityId)
                && !world.IsMissileBlocked(player.LevelId, candidateX, candidateY)
                && world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private void SpawnGolem(
        AuthoritativePlayerState player,
        (int X, int Y) position,
        int effectiveSpellLevel,
        AuthoritativeSpellDefinition spell,
        int deathAnimationTicks)
    {
        foreach (var existing in world.CombatTargets.Values
                     .Where(target => target.IsPlayerMinion
                         && target.OwnerPlayerId == player.EntityId)
                     .ToArray()) {
            world.CombatTargets.Remove(existing.EntityId);
        }

        var entityId = AllocateProjectileEntityId();
        var hitPointsQ6 = checked(
            spell.GolemHitPointsLevelMultiplier * (spell.GolemHitPointsLevelBaseQ6 * effectiveSpellLevel
                + DeterministicFixedPointQ6.FromWhole(player.ManaMaximum)
                    / spell.GolemHitPointsManaDivisor));
        var hitPoints = DeterministicFixedPointQ6.ToWholeCeiling(hitPointsQ6);
        var minimumDamage = checked(
            spell.GolemDamageLevelMultiplier
                * (effectiveSpellLevel + spell.GolemMinimumDamageLevelOffset));
        var maximumDamage = checked(
            spell.GolemDamageLevelMultiplier
                * (effectiveSpellLevel + spell.GolemMaximumDamageLevelOffset));
        var golemType = spell.GolemMonsterId != 0
            ? monsterTypes?.TryGet(spell.GolemMonsterId, out var configuredGolemType) == true
                ? configuredGolemType
                : null
            : monsterTypes?.TryGetNativeAi("Golem", out var legacyGolemType) == true
                ? legacyGolemType
                : null;
        var spawnRandom = new DeterministicLegacyRandom(random.State);
        // InitializeSpawnedMonster seeds the native Golem after its standing
        // animation and native health draws, even though InitGolem replaces
        // the health and damage values with spell-owned formulas. Preserve
        // those draws so the server's next AI tick starts from the same stream.
        spawnRandom.GenerateRnd(Math.Max(
            0,
            (golemType?.StandAnimationTicksPerFrame ?? 1) - 1));
        spawnRandom.GenerateRnd(Math.Max(
            0,
            (golemType?.StandAnimationFrames ?? 1) - 1));
        spawnRandom.RandomIntBetween(
            golemType?.MinimumHitPoints ?? 1,
            golemType?.MaximumHitPoints ?? 1);
        var monsterItemSeed = unchecked((uint)spawnRandom.AdvanceRndSeed());
        var monsterAiSeed = unchecked((uint)spawnRandom.AdvanceRndSeed());
        random.Restore(spawnRandom.State);
        var golem = new AuthoritativeCombatTarget(
            entityId,
            position.X,
            position.Y,
            hitPoints,
            armorClass: spell.GolemArmorClass,
            maxHitPoints: hitPoints,
            levelId: player.LevelId,
            monsterId: golemType?.MonsterId ?? 0,
            attackDamage: maximumDamage,
            monsterLevel: checked((int)player.CharacterLevel),
            playerMinion: true,
            ownerPlayerId: player.EntityId,
            nativeAi: golemType?.NativeAi ?? "Golem",
            minDamage: minimumDamage,
            maxDamage: maximumDamage,
            minDamageSpecial: minimumDamage,
            maxDamageSpecial: maximumDamage,
            deathAnimationTicks: deathAnimationTicks,
            monsterItemSeed: monsterItemSeed,
            monsterAiSeed: monsterAiSeed);
        golem.RestoreHitPointsQ6(hitPointsQ6, hitPointsQ6);
        world.CombatTargets.Add(entityId, golem);
    }

    /** Source-compatible combat facade over engine-owned direction rotation. */
    private static int RotateDirection(int direction, int offset)
        => DeterministicGridDirections.Rotate(direction, offset);

    private (int X, int Y)? FindPhasingDestination(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var candidates = new List<(int X, int Y)>();
        for (var offsetY = -spell.PhasingMaximumAxisOffset;
             offsetY <= spell.PhasingMaximumAxisOffset;
             offsetY++) {
            for (var offsetX = -spell.PhasingMaximumAxisOffset;
                 offsetX <= spell.PhasingMaximumAxisOffset;
                 offsetX++) {
                if (Math.Abs(offsetX) < spell.PhasingMinimumAxisOffset
                    || Math.Abs(offsetY) < spell.PhasingMinimumAxisOffset) {
                    continue;
                }
                var candidateX = checked(player.PositionX + offsetX);
                var candidateY = checked(player.PositionY + offsetY);
                if (world.IsWalkable(player.LevelId, candidateX, candidateY)
                    && !world.IsOccupied(
                        player.LevelId,
                        candidateX,
                        candidateY,
                        players.Where(candidate => candidate.Life > 0),
                        player.EntityId)) {
                    candidates.Add((candidateX, candidateY));
                }
            }
        }
        return candidates.Count == 0
            ? null
            : candidates[random.RandomIntLessThan(candidates.Count)];
    }

    private (AuthoritativeCombatTarget Target, int X, int Y)? FindStoneCurseTarget(
        uint levelId,
        int targetX,
        int targetY,
        int searchRadius)
    {
        var position = DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                FindStoneCurseTargetAt(levelId, candidateX, candidateY) is not null);
        if (!position.HasValue)
            return null;
        var target = FindStoneCurseTargetAt(
            levelId,
            position.Value.X,
            position.Value.Y);
        return target is null
            ? null
            : (target, position.Value.X, position.Value.Y);
    }

    private AuthoritativeCombatTarget? FindBerserkTarget(
        uint levelId,
        uint targetEntityId,
        int targetX,
        int targetY,
        int searchRadius,
        int resistanceFrequency,
        bool respectMagicResistance)
    {
        if (targetEntityId != 0) {
            if (!world.CombatTargets.TryGetValue(targetEntityId, out var explicitTarget)
                || !IsBerserkTargetEligible(explicitTarget, levelId)
                || (respectMagicResistance
                    && !PassesBerserkResistance(explicitTarget, resistanceFrequency))) {
                return null;
            }
            return explicitTarget;
        }

        AuthoritativeCombatTarget? selectedTarget = null;
        var position = DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) => {
                selectedTarget = world.CombatTargets.Values
                .Where(candidate => candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY
                    && IsBerserkTargetEligible(candidate, levelId))
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate => !respectMagicResistance
                    || PassesBerserkResistance(candidate, resistanceFrequency));
                return selectedTarget is not null;
            });
        return position.HasValue ? selectedTarget : null;
    }

    private static bool IsBerserkTargetEligible(
        AuthoritativeCombatTarget target,
        uint levelId)
    {
        return target.HitPointsQ6 > 0
            && !target.SneakHidden
            && (target.LevelId == 0 || target.LevelId == levelId)
            && !target.IsPlayerMinion
            && !target.IsBerserked
            && !target.IsUnique
            && !target.IsDiablo
            && !target.BerserkImmune
            && !target.IsPetrified;
    }

    private bool PassesBerserkResistance(
        AuthoritativeCombatTarget target,
        int resistanceFrequency)
    {
        if (target.MagicResistance >= 100)
            return false;
        return target.MagicResistance <= 0
            || random.FlipCoin((uint)(resistanceFrequency > 0 ? resistanceFrequency : 2));
    }

    private void ApplyBerserk(
        AuthoritativeCombatTarget target,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        target.IsBerserked = true;
        target.MinDamage = ScaleBerserkDamage(
            target.MinDamage,
            spell,
            effectiveSpellLevel);
        target.MaxDamage = ScaleBerserkDamage(
            target.MaxDamage,
            spell,
            effectiveSpellLevel);
        target.MinDamageSpecial = ScaleBerserkDamage(
            target.MinDamageSpecial,
            spell,
            effectiveSpellLevel);
        target.MaxDamageSpecial = ScaleBerserkDamage(
            target.MaxDamageSpecial,
            spell,
            effectiveSpellLevel);
        target.AttackDamage = target.MaxDamage;
    }

    private int ScaleBerserkDamage(
        int damage,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var percent = checked(
            spell.BerserkDamagePercentBase
            + random.RandomIntLessThan(spell.BerserkDamagePercentRandomRange));
        return checked(
            percent * damage / 100
            + spell.BerserkSpellLevelBonus * effectiveSpellLevel);
    }

    private AuthoritativeCombatTarget? FindStoneCurseTargetAt(
        uint levelId,
        int positionX,
        int positionY)
    {
        return world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && !candidate.SneakHidden
                && !candidate.StoneCurseImmune
                && (candidate.LevelId == 0 || candidate.LevelId == levelId)
                && ((candidate.PositionX == positionX
                        && candidate.PositionY == positionY)
                    || (candidate.IsMoving
                        && candidate.FuturePositionX == positionX
                        && candidate.FuturePositionY == positionY)))
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }
}
