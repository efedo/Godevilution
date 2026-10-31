using Devilution.Engine;

namespace Devilution.Diablo;

/**
 * Native missiles.cpp for L5-L8 monster missiles: AddAcid/AddMagmaBall with ProcessGenericProjectile,
 * ProcessAcidSplate, ProcessAcidPuddle, ProcessMissileExplosion and the Familiar's AddLightning/
 * ProcessLightning, with CheckMissileCol's MonsterTrapHit and PlayerMHit. Arrows share CheckMissileCol
 * through the Arrow port.
 */
public sealed partial class DiabloCombatCommandProcessor
{
    private const int NativeAcidSplatAnimationLength = 8;
    private const int NativeMagmaBallExplosionAnimationLength = 10;
    private const int NativeAcidPuddleEndAnimationLength = 4;
    private const int NativeFamiliarLightningDuration = 8;
    private const int NativeMissileMinimumHitChance = 10;

    private void AdvanceNativeMonsterMissile(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var state = projectile.NativeMonsterMissile!;
        if (state.Kind == AuthoritativeNativeMonsterMissileKind.Arrow) {
            AdvanceNativeMonsterArrow(tick, projectile, players, events);
            return;
        }
        if (!players.Any(player => player.LevelId == projectile.LevelId))
            return;
        if (world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var liveSource))
            state = state.Refresh(liveSource);
        state.ValidateShape(projectile);
        projectile.NativeMonsterMissile = state;
        switch (state.Kind) {
        case AuthoritativeNativeMonsterMissileKind.Acid:
        case AuthoritativeNativeMonsterMissileKind.MagmaBall:
            AdvanceNativeGenericProjectile(tick, projectile, players, events);
            break;
        case AuthoritativeNativeMonsterMissileKind.AcidSplat:
            AdvanceNativeAcidSplat(projectile);
            break;
        case AuthoritativeNativeMonsterMissileKind.AcidPuddle:
            AdvanceNativeAcidPuddle(tick, projectile, players, events);
            break;
        case AuthoritativeNativeMonsterMissileKind.MagmaBallExplosion:
            if (--projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            break;
        case AuthoritativeNativeMonsterMissileKind.Lightning:
            AdvanceNativeMonsterLightning(tick, projectile, players, events);
            break;
        default:
            throw new InvalidDataException("A native monster missile has no process function.");
        }
    }

    /** AddAcid / AddMagmaBall from AddMissile(tile, enemyPosition, direction, missile, TARGET_PLAYERS, monster, 0, 0). */
    private void CreateNativeMonsterProjectile(
        AuthoritativeCombatTarget monster,
        uint targetEntityId,
        int targetX,
        int targetY,
        AuthoritativeNativeMonsterMissileKind kind,
        AuthoritativeMonsterMissileDefinition mapping)
    {
        var acid = kind == AuthoritativeNativeMonsterMissileKind.Acid;
        // AddMissile plays the Acid cast sound (SpellAcid picks a variant) before AddAcid.
        if (acid)
            random.AdvanceRndSeed();
        var velocity = CalculateVelocity(monster.PositionX, monster.PositionY, targetX, targetY, 16);
        var duration = acid ? checked(5 * (monster.Intelligence + 4)) : 256;
        // ProjectileMonsterDamage.
        var range = GetEffectiveMonsterDamageRange(monster, special: false);
        var damage = random.RandomIntBetween(range.Minimum, range.Maximum);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(), monster.EntityId, targetEntityId, mapping.SpellId,
            monster.LevelId, monster.PositionX, monster.PositionY, targetX, targetY,
            damage, acid ? AuthoritativeDamageType.Acid : AuthoritativeDamageType.Fire, 0,
            checked((uint)duration), allowZeroSpellId: true) {
            Behavior = acid ? AuthoritativeProjectileBehavior.Acid : AuthoritativeProjectileBehavior.MonsterMagic,
            NativeMissileId = kind.ToString(),
            Speed = 16,
            ImpactLifetime = 0,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = GetDirection(monster.PositionX, monster.PositionY, targetX, targetY),
            NativeMonsterMissile = AuthoritativeNativeMonsterMissileState.Create(kind, monster),
        };
        if (!acid) {
            // AddMagmaBall starts three steps along its flight.
            projectile.TraveledX = checked(3 * velocity.X);
            projectile.TraveledY = checked(3 * velocity.Y);
            var tile = CalculateMissileTile(projectile.StartX, projectile.StartY, projectile.TraveledX, projectile.TraveledY);
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
        }
        world.Projectiles.Add(projectile.EntityId, projectile);
    }

    /**
     * BatAi's Familiar branch: AddMissile(enemyPosition, enemyPosition + SouthEast, South, Lightning,
     * TARGET_PLAYERS, monster, GenerateRnd(10) + 1, 0). AddLightning moves start to dst and picks an
     * animation frame; the bolt strikes its tile, the enemy's, while that differs from start.
     */
    private void CreateNativeFamiliarLightning(AuthoritativeCombatTarget monster, AuthoritativePlayerState enemy)
    {
        var damage = random.GenerateRnd(10) + 1;
        _ = random.RandomIntBetween(1, 8);
        var tileX = enemy.FuturePositionX;
        var tileY = enemy.FuturePositionY;
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(), monster.EntityId, enemy.EntityId, 0,
            monster.LevelId, tileX, tileY, tileX + 1, tileY + 1,
            damage, AuthoritativeDamageType.Lightning, 0, NativeFamiliarLightningDuration, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterMagic,
            NativeMissileId = nameof(AuthoritativeNativeMonsterMissileKind.Lightning),
            StartX = tileX + 1,
            StartY = tileY + 1,
            ImpactLifetime = 0,
            NativeMonsterMissile = AuthoritativeNativeMonsterMissileState.Create(
                AuthoritativeNativeMonsterMissileKind.Lightning, monster),
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
    }

    /** ProcessGenericProjectile: MoveMissileAndCheckMissileCol(ignoreStart, ifCollidesDontMoveToHitTile), then expiry. */
    private void AdvanceNativeGenericProjectile(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = (int)projectile.RemainingTicks - 1;
        var damage = projectile.Damage;
        bool CheckTile(int x, int y)
        {
            if (x == projectile.StartX && y == projectile.StartY)
                return true;
            var hitFlag = CheckNativeMonsterMissileCollision(tick, projectile, x, y, damage, damage,
                shifted: false, dontDeleteOnCollision: false, ref duration, players, events);
            if (duration != 0)
                return true;
            // Acid and MagmaBall are Blockable: an actor hit stops before the hit tile.
            if (hitFlag)
                return false;
            return !world.IsMissileBlocked(projectile.LevelId, x, y);
        }

        var finalTraveledX = unchecked(projectile.TraveledX + projectile.VelocityX);
        var finalTraveledY = unchecked(projectile.TraveledY + projectile.VelocityY);
        var visited = MoveNativeProjectile(projectile);
        var tileChanged = visited.Count != 0;
        if (tileChanged) {
            var increment = NativeSkippedTileIncrement(projectile);
            for (var index = 0; index < visited.Count; index++) {
                var tile = visited[index];
                if (CheckTile(tile.X, tile.Y))
                    continue;
                // The missile stops one step back from the tile that stopped it.
                var final = index == visited.Count - 1 && tile.TraveledX == finalTraveledX && tile.TraveledY == finalTraveledY;
                projectile.TraveledX = final ? unchecked(finalTraveledX - projectile.VelocityX) : unchecked(tile.TraveledX - increment.X);
                projectile.TraveledY = final ? unchecked(finalTraveledY - projectile.VelocityY) : unchecked(tile.TraveledY - increment.Y);
                var stopped = CalculateMissileTile(projectile.StartX, projectile.StartY, projectile.TraveledX, projectile.TraveledY);
                projectile.PositionX = stopped.X;
                projectile.PositionY = stopped.Y;
                projectile.VelocityX = 0;
                projectile.VelocityY = 0;
                break;
            }
        }
        // ProcessMissiles deletes a missile that left the dungeon, without its expiry missile.
        if (!world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        var occupants = world.NativeActorOccupants(projectile.LevelId, projectile.PositionX, projectile.PositionY, players);
        var state = projectile.NativeMonsterMissile!;
        if (!tileChanged && state.LastCollisionTargetHash != occupants.Hash)
            CheckNativeMonsterMissileCollision(tick, projectile, projectile.PositionX, projectile.PositionY, damage, damage,
                shifted: false, dontDeleteOnCollision: false, ref duration, players, events);
        projectile.NativeMonsterMissile = state with { LastCollisionTargetHash = occupants.Hash };
        if (duration > 0) {
            projectile.RemainingTicks = (uint)duration;
            return;
        }
        world.Projectiles.Remove(projectile.EntityId);
        var acid = state.Kind == AuthoritativeNativeMonsterMissileKind.Acid;
        // The expiry missile's AddMissileExplosion copies the parent's tile, start and traveled.
        var expiry = new AuthoritativeProjectile(
            AllocateProjectileEntityId(), projectile.SourceEntityId, 0, projectile.SpellId, projectile.LevelId,
            projectile.StartX, projectile.StartY, projectile.PositionX, projectile.PositionY,
            0, projectile.DamageType, 0,
            (uint)(acid ? NativeAcidSplatAnimationLength : NativeMagmaBallExplosionAnimationLength), allowZeroSpellId: true) {
            Behavior = acid ? AuthoritativeProjectileBehavior.AcidSplat : AuthoritativeProjectileBehavior.MonsterMagic,
            Phase = acid ? AuthoritativeProjectilePhase.Travel : AuthoritativeProjectilePhase.Impact,
            NativeMissileId = (acid ? AuthoritativeNativeMonsterMissileKind.AcidSplat
                : AuthoritativeNativeMonsterMissileKind.MagmaBallExplosion).ToString(),
            ImpactLifetime = 0,
            TraveledX = projectile.TraveledX,
            TraveledY = projectile.TraveledY,
            OriginalDirection = projectile.OriginalDirection,
            NativeMonsterMissile = state with {
                Kind = acid ? AuthoritativeNativeMonsterMissileKind.AcidSplat
                    : AuthoritativeNativeMonsterMissileKind.MagmaBallExplosion,
                LastCollisionTargetHash = 0,
            },
        };
        expiry.PositionX = projectile.PositionX;
        expiry.PositionY = projectile.PositionY;
        AddNativeMissileFromMissile(expiry);
    }

    /** MoveMissile's per-tile increment when a fast missile visits skipped tiles. */
    private static (int X, int Y) NativeSkippedTileIncrement(AuthoritativeProjectile projectile)
    {
        var speedX = Math.Abs((long)projectile.VelocityX);
        var speedY = Math.Abs((long)projectile.VelocityY);
        var denominator = (float)(2 * speedY >= speedX ? 2 * speedY : speedX);
        if (denominator == 0)
            return (0, 0);
        var fraction = (32 << 16) / denominator;
        return (unchecked((int)(projectile.VelocityX * fraction)), unchecked((int)(projectile.VelocityY * fraction)));
    }

    /** ProcessAcidSplate: shift one tile on the first update, then leave a puddle. */
    private void AdvanceNativeAcidSplat(AuthoritativeProjectile projectile)
    {
        if (projectile.RemainingTicks == NativeAcidSplatAnimationLength) {
            projectile.PositionX++;
            projectile.PositionY++;
        }
        if (--projectile.RemainingTicks != 0)
            return;
        world.Projectiles.Remove(projectile.EntityId);
        var state = projectile.NativeMonsterMissile!;
        var damage = state.SourceMonsterLevel >= 2 ? 2 : 1;
        // AddAcidPuddle; the SpellPuddle cast sound has no variant.
        var duration = random.GenerateRnd(15) + 40 * (state.SourceIntelligence + 1);
        var puddle = new AuthoritativeProjectile(
            AllocateProjectileEntityId(), projectile.SourceEntityId, 0, projectile.SpellId, projectile.LevelId,
            projectile.PositionX, projectile.PositionY, projectile.PositionX, projectile.PositionY,
            damage, AuthoritativeDamageType.Acid, 0, checked((uint)duration), allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
            NativeMissileId = nameof(AuthoritativeNativeMonsterMissileKind.AcidPuddle),
            ImpactLifetime = 0,
            NativeMonsterMissile = state with { Kind = AuthoritativeNativeMonsterMissileKind.AcidPuddle },
        };
        AddNativeMissileFromMissile(puddle);
    }

    /** ProcessAcidPuddle: a shifted acid hit every update until the End animation finishes. */
    private void AdvanceNativeAcidPuddle(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var range = (int)projectile.RemainingTicks - 1;
        var duration = range;
        CheckNativeMonsterMissileCollision(tick, projectile, projectile.PositionX, projectile.PositionY,
            projectile.Damage, projectile.Damage, shifted: true, dontDeleteOnCollision: false, ref duration, players, events);
        if (range > 0) {
            projectile.RemainingTicks = (uint)range;
            return;
        }
        var state = projectile.NativeMonsterMissile!;
        if (state.Ending) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        projectile.NativeMonsterMissile = state with { Ending = true };
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = NativeAcidPuddleEndAnimationLength;
    }

    /** ProcessLightning: strike the bolt's tile; an actor hit restores the duration. */
    private void AdvanceNativeMonsterLightning(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = (int)projectile.RemainingTicks - 1;
        var j = duration;
        var hitFlag = false;
        if (projectile.PositionX != projectile.StartX || projectile.PositionY != projectile.StartY)
            hitFlag = CheckNativeMonsterMissileCollision(tick, projectile, projectile.PositionX, projectile.PositionY,
                projectile.Damage, projectile.Damage, shifted: true, dontDeleteOnCollision: false, ref duration, players, events);
        if (hitFlag)
            duration = j;
        if (duration == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        projectile.RemainingTicks = (uint)duration;
    }

    private void AddNativeMissileFromMissile(AuthoritativeProjectile projectile)
    {
        // ProcessMissiles reaches a missile appended to the list during the same pass.
        world.Projectiles.Add(projectile.EntityId, projectile);
    }

    /**
     * CheckMissileCol for a TARGET_PLAYERS monster missile: an opposing or berserk monster
     * (MonsterTrapHit), the player (PlayerMHit), then a missile-blocking tile. Returns _miHitFlag.
     */
    private bool CheckNativeMonsterMissileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        int x,
        int y,
        int minimumDamage,
        int maximumDamage,
        bool shifted,
        bool dontDeleteOnCollision,
        ref int duration,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.ContainsPosition(projectile.LevelId, x, y))
            return false;
        var state = projectile.NativeMonsterMissile!;
        var hitFlag = false;
        var occupants = world.NativeActorOccupants(projectile.LevelId, x, y, players);
        if (occupants.Monster is { } monster
            && (monster.IsPlayerMinion != state.SourceIsPlayerMinion || state.SourceIsBerserked || monster.IsBerserked)
            && players.FirstOrDefault(player => player.LevelId == projectile.LevelId) is { } observer
            && ResolveNativeMonsterMissileMonsterHit(projectile, monster, minimumDamage, maximumDamage, shifted, observer, events)) {
            if (!dontDeleteOnCollision)
                duration = 0;
            hitFlag = true;
        }
        if (occupants.Player is { } player
            && ResolveNativeMonsterMissilePlayerHit(tick, projectile, state, player, minimumDamage, maximumDamage,
                shifted, players, events)) {
            if (!dontDeleteOnCollision)
                duration = 0;
            hitFlag = true;
        }
        if (world.IsMissileBlocked(projectile.LevelId, x, y)) {
            BreakMissileBlockingObject(projectile.LevelId, x, y);
            if (!dontDeleteOnCollision)
                duration = 0;
            hitFlag = false;
        }
        return hitFlag;
    }

    /** MonsterTrapHit(dist 0): AC-based hit, immunity and quarter-damage resistance. */
    private bool ResolveNativeMonsterMissileMonsterHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        int minimumDamage,
        int maximumDamage,
        bool shifted,
        AuthoritativePlayerState observer,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.HitPointsQ6 < 64 || target.DeathTicksRemaining > 0
            || target.DialogueMessageId != 0 || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target))
            return false;
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var hit = random.GenerateRnd(100);
        var chance = Math.Clamp(90 - GetEffectiveMonsterArmorClass(target), 5, 95);
        if (UsesNativeAi(target, "Gargoyle") && IsNativeGargoyleWakePending(target)) {
            _ = ConsumeNativeGargoyleWake(target);
            return true;
        }
        if (hit >= chance && !target.IsPetrified)
            return false;
        var damage = random.RandomIntBetween(minimumDamage, maximumDamage);
        if (!shifted)
            damage = checked(damage * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damage /= 4;
        var nativeEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(observer, target, damage, nativeEvents, nativeTrap: true, nativeTrapResisted: resistance > 0);
        // playEffect(Hit) for a resisted hit, startHit otherwise; golems skip startHit.
        if (target.HitPointsQ6 >= 64
            && (resistance > 0 || target.NativeInitialization?.Binding.NativeTypeId != 109))
            random.AdvanceRndSeed();
        foreach (var gameplayEvent in nativeEvents) {
            var forwarded = gameplayEvent.Kind == DiabloGameplayEventKind.Damage
                ? gameplayEvent with { SourceEntityId = projectile.SourceEntityId }
                : gameplayEvent;
            events.Add(new DiabloDirectedGameplayEvent(observer, forwarded));
        }
        return true;
    }

    /** PlayerMHit for a monster's non-arrow missile at _midist 0. */
    private bool ResolveNativeMonsterMissilePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeNativeMonsterMissileState state,
        AuthoritativePlayerState target,
        int minimumDamage,
        int maximumDamage,
        bool shifted,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.Life < 64 || IsPlayerProtectedByFlash(target))
            return false;
        var source = world.CombatTargets.GetValueOrDefault(projectile.SourceEntityId);
        var sourceLevel = source is null
            ? checked(state.SourceMonsterLevel + GetDifficultyLevelOffset())
            : GetEffectiveMonsterLevel(source);
        var characterLevel = checked((int)target.CharacterLevel);
        var hit = random.GenerateRnd(100);
        var hitChance = Math.Max(40 + sourceLevel * 2 - characterLevel * 2, NativeMissileMinimumHitChance);
        var targetClass = GetPlayerClass(target);
        var canBlock = target.CombatMode is DiabloPlayerCombatMode.Stand or DiabloPlayerCombatMode.Attack
            && CanBlock(target);
        var block = canBlock ? random.GenerateRnd(100) : 100;
        if (shifted || state.Kind == AuthoritativeNativeMonsterMissileKind.AcidPuddle)
            block = 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current + (targetClass?.BaseBlockChance ?? 0)
                - (sourceLevel - characterLevel) * 2,
            0,
            100);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        if (hit >= hitChance)
            return false;
        var getHit = AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment);
        var damage = shifted
            ? checked(random.RandomIntBetween(minimumDamage, maximumDamage) + getHit)
            : checked(random.RandomIntBetween(minimumDamage * DeterministicFixedPointQ6.Scale,
                maximumDamage * DeterministicFixedPointQ6.Scale) + getHit * DeterministicFixedPointQ6.Scale);
        damage = Math.Max(damage, DeterministicFixedPointQ6.Scale);
        if ((resistance <= 0 || rules.GameVariant == DiabloGameVariant.Hellfire) && block < blockChance) {
            target.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
                target.PositionX, target.PositionY,
                source?.PositionX ?? state.SourcePositionX, source?.PositionY ?? state.SourcePositionY);
            StartBlock(target, targetClass, tick);
            return true;
        }
        if (resistance > 0)
            damage -= damage * resistance / 100;
        var result = playerDamage.Apply(target, Math.Max(0, damage));
        if (result.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(target,
                new DiabloGameplayEvent(DiabloGameplayEventKind.Damage,
                    projectile.SourceEntityId, target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) {
                    AmountQ6 = result.IncomingDamageQ6,
                }));
        }
        if (target.Life < 64) {
            target.Life = 0;
            StartPlayerDeath(target, projectile.SourceEntityId, tick, players, PlayerDeathReason.MonsterOrTrap);
            return true;
        }
        // Say(ArghClang) picks a voice variant; an unresisted hit's startHit then tests hit recovery.
        random.AdvanceRndSeed();
        if (resistance <= 0 && ShouldStartHit(target, targetClass, damage))
            StartHit(target, targetClass, tick);
        return true;
    }
}
