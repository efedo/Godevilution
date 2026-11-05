using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    internal static (int X, int Y) NativeObjectMissileVelocity(int sx, int sy, int tx, int ty, int speed)
        => CalculateVelocity(sx, sy, tx, ty, speed);

    /** Native objects.cpp OperateChest/OperateTrap call AddMissile with TARGET_PLAYERS and source -1. */
    internal static AuthoritativeProjectile AddObjectTrapMissile(uint entityId, uint sourceEntityId,
        uint triggerEntityId, uint levelId, int objectSlot, int sx, int sy, int tx, int ty, int direction,
        int missileType, bool intrinsicChest, DeterministicLegacyRandom random, int depth = 0)
    {
        var (speed, damage) = missileType switch {
            0 => AddArrow(), 1 => AddFirebolt(depth, random),
            7 => AddLightningControl(random), 27 => AddElementalArrow(),
            _ => throw new InvalidDataException("An object trap has an unsupported native missile."),
        };
        if (missileType != 7 && sx == tx && sy == ty) {
            var offset = DeterministicGridDirections.GetDisplacement((DeterministicGridDirection)direction);
            tx += offset.X;
            ty += offset.Y;
        }
        var velocity = CalculateVelocity(sx, sy, tx, ty, speed);
        return new(entityId, sourceEntityId, triggerEntityId, 0, levelId, sx, sy, tx, ty,
            damage, missileType == 1 ? AuthoritativeDamageType.Fire
                : missileType == 7 ? AuthoritativeDamageType.Lightning : AuthoritativeDamageType.Physical,
            0, 256, allowZeroSpellId: true) {
            Behavior = missileType == 1 ? AuthoritativeProjectileBehavior.Firebolt
                : missileType == 7 ? AuthoritativeProjectileBehavior.Lightning : AuthoritativeProjectileBehavior.WeaponArrow,
            NativeMissileId = missileType switch { 0 => null, 1 => "Firebolt", 7 => "LightningControl", 27 => "FireArrow",
                _ => throw new InvalidDataException("An object trap has an unsupported native missile.") },
            NativeTrapArrow = new(objectSlot, 0, 0) { MissileTypeId = missileType, IntrinsicChest = intrinsicChest },
            VelocityX = velocity.X, VelocityY = velocity.Y, Speed = speed, OriginalDirection = direction,
        };
    }

    /** Native missiles.cpp AddArrow, TARGET_PLAYERS source-less branch. */
    private static (int Speed, int Damage) AddArrow() => (32, 0);

    /** Native missiles.cpp AddElementalArrow, TARGET_PLAYERS source-less branch. */
    private static (int Speed, int Damage) AddElementalArrow() => (32, 0);

    /** Native missiles.cpp AddFirebolt, TARGET_PLAYERS source-less branch. */
    private static (int Speed, int Damage) AddFirebolt(int depth, DeterministicLegacyRandom random)
    {
        if (depth is < 1 or > 8)
            throw new InvalidDataException("A trap Firebolt requires its owned native floor depth.");
        return (26, ProjectileTrapDamage(depth, random));
    }

    /** Native missiles.cpp ProjectileTrapDamage, reached by AddFirebolt. */
    private static int ProjectileTrapDamage(int depth, DeterministicLegacyRandom random)
        => depth + random.GenerateRnd(2 * depth);

    /** Native missiles.cpp AddLightningControl consumes its initial animation-frame roll. */
    private static (int Speed, int Damage) AddLightningControl(DeterministicLegacyRandom random)
    {
        _ = random.RandomIntBetween(1, 8);
        return (32, 0);
    }

    private void AdvanceNativeObjectMissile(ulong tick, AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var state = projectile.NativeTrapArrow!;
        _ = world.RequireNativeTrapMissileOwner(projectile);
        if (!players.Any(player => player.LevelId == projectile.LevelId))
            return;
        ProcessObjectTrapMissile(tick, projectile, world.NativePopulationDepth(projectile.LevelId), players, events);
    }

    internal void ProcessObjectTrapMissile(ulong tick, AuthoritativeProjectile projectile, int depth,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var state = projectile.NativeTrapArrow!;
        switch (state.Stage) {
        case NativeObjectMissileStage.ElementalImpact:
            ProcessElementalArrow(tick, projectile, depth, players, events);
            return;
        case NativeObjectMissileStage.Explosion:
            ProcessMissileExplosion(projectile);
            return;
        case NativeObjectMissileStage.LightningSegment:
            ProcessLightning(tick, projectile, players, events);
            return;
        }
        switch (state.MissileTypeId) {
        case 0:
            ProcessArrow(tick, projectile, depth, players, events);
            break;
        case 1:
            ProcessGenericProjectile(tick, projectile, players, events);
            break;
        case 7:
            ProcessLightningControl(projectile, depth);
            break;
        case 27:
            ProcessElementalArrow(tick, projectile, depth, players, events);
            break;
        }
    }

    /** Native missiles.cpp ProcessArrow: source-less damage is depth through twice depth. */
    private void ProcessArrow(ulong tick, AuthoritativeProjectile projectile, int depth,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = checked((int)projectile.RemainingTicks - 1);
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with {
            MissileDistance = projectile.NativeTrapArrow.MissileDistance + 1,
        };
        MoveMissileAndCheckMissileCol(tick, projectile, depth, 2 * depth, AuthoritativeDamageType.Physical,
            false, ref duration, players, events);
        if (duration == 0 || !world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY))
            world.Projectiles.Remove(projectile.EntityId);
        else
            projectile.RemainingTicks = (uint)duration;
    }

    /** Native missiles.cpp ProcessElementalArrow, source-less original-Diablo FireArrow branch. */
    internal void ProcessElementalArrow(ulong tick, AuthoritativeProjectile projectile, int depth,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = checked((int)projectile.RemainingTicks - 1);
        if (projectile.NativeTrapArrow!.Stage == NativeObjectMissileStage.ElementalImpact) {
            if (duration == 0)
                world.Projectiles.Remove(projectile.EntityId);
            else
                projectile.RemainingTicks = (uint)duration;
            return;
        }
        projectile.NativeTrapArrow = projectile.NativeTrapArrow with {
            MissileDistance = projectile.NativeTrapArrow.MissileDistance + 1,
        };
        var minimum = random.GenerateRnd(10) + 1 + depth;
        var maximum = random.GenerateRnd(10) + 1 + depth * 2;
        MoveMissileAndCheckMissileCol(tick, projectile, minimum, maximum, AuthoritativeDamageType.Physical,
            backOffHitTile: false, ref duration, players, events);
        if (!world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (duration != 0) {
            projectile.RemainingTicks = (uint)duration;
            return;
        }
        // FireArrow has four flight animation frames. Native sets duration before
        // switching to the ten-frame MagmaBallExplosion graphic, so impact lasts three ticks.
        projectile.RemainingTicks = 3;
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.VelocityX = projectile.VelocityY = 0;
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with { Stage = NativeObjectMissileStage.ElementalImpact };
        minimum = random.GenerateRnd(10) + 1 + depth;
        maximum = random.GenerateRnd(10) + 1 + depth * 2;
        _ = CheckMissileCol(tick, projectile, projectile.PositionX, projectile.PositionY,
            minimum, maximum, AuthoritativeDamageType.Fire, dontDeleteOnCollision: true,
            ref duration, players, events);
    }

    /** Native missiles.cpp ProcessGenericProjectile, Firebolt's Blockable expiry branch. */
    internal void ProcessGenericProjectile(ulong tick, AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = checked((int)projectile.RemainingTicks - 1);
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with {
            MissileDistance = projectile.NativeTrapArrow.MissileDistance + 1,
        };
        MoveMissileAndCheckMissileCol(tick, projectile, projectile.Damage, projectile.Damage, AuthoritativeDamageType.Fire,
            backOffHitTile: true, ref duration, players, events);
        if (!world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (duration != 0) {
            projectile.RemainingTicks = (uint)duration;
            return;
        }
        world.Projectiles.Remove(projectile.EntityId);
        AddMissileExplosion(projectile);
    }

    /** Native missiles.cpp AddMissileExplosion retains the parent's start, tile and traveled offset. */
    private void AddMissileExplosion(AuthoritativeProjectile projectile)
    {
        var explosion = new AuthoritativeProjectile(AllocateProjectileEntityId(), projectile.SourceEntityId,
            projectile.TargetEntityId, 0, projectile.LevelId, projectile.PositionX, projectile.PositionY,
            projectile.TargetX, projectile.TargetY, 0, AuthoritativeDamageType.Fire, 0, 10, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt, NativeMissileId = "MagmaBallExplosion",
            Phase = AuthoritativeProjectilePhase.Impact, StartX = projectile.StartX, StartY = projectile.StartY,
            TraveledX = projectile.TraveledX, TraveledY = projectile.TraveledY,
            OriginalDirection = projectile.OriginalDirection,
            NativeTrapArrow = projectile.NativeTrapArrow! with { Stage = NativeObjectMissileStage.Explosion },
        };
        AddNativeMissileFromMissile(explosion);
    }

    /** Native missiles.cpp ProcessMissileExplosion. */
    private void ProcessMissileExplosion(AuthoritativeProjectile projectile)
    {
        if (--projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native missiles.cpp MoveMissileAndCheckMissileCol; retains signed occupant collision suppression. */
    private void MoveMissileAndCheckMissileCol(ulong tick, AuthoritativeProjectile projectile, int minimum, int maximum,
        AuthoritativeDamageType damageType, bool backOffHitTile, ref int duration,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var visited = MoveNativeProjectile(projectile);
        foreach (var tile in visited) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                return;
            }
            if (tile.X == projectile.StartX && tile.Y == projectile.StartY)
                continue;
            var hit = CheckMissileCol(tick, projectile, tile.X, tile.Y, minimum, maximum,
                damageType, false, ref duration, players, events);
            if (duration != 0)
                continue;
            if (!hit && !world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (backOffHitTile) {
                // MoveMissile uses the full velocity for the final step and its per-tile
                // increment for skipped cells. These trap speeds visit at most one cell.
                projectile.TraveledX = unchecked(projectile.TraveledX - projectile.VelocityX);
                projectile.TraveledY = unchecked(projectile.TraveledY - projectile.VelocityY);
            }
            var position = CalculateMissileTile(projectile.StartX, projectile.StartY,
                projectile.TraveledX, projectile.TraveledY);
            projectile.PositionX = position.X;
            projectile.PositionY = position.Y;
            break;
        }
        var occupants = world.NativeActorOccupants(projectile.LevelId, projectile.PositionX, projectile.PositionY, players);
        if (visited.Count == 0 && projectile.NativeTrapArrow!.LastCollisionTargetHash != occupants.Hash)
            _ = CheckMissileCol(tick, projectile, projectile.PositionX, projectile.PositionY,
                minimum, maximum, damageType, false, ref duration, players, events);
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with { LastCollisionTargetHash = occupants.Hash };
    }

    /** Native missiles.cpp ProcessLightningControl, source-less trap branch. */
    internal void ProcessLightningControl(AuthoritativeProjectile projectile, int depth)
    {
        projectile.RemainingTicks--;
        var damage = random.GenerateRnd(depth) + 2 * depth;
        SpawnLightning(projectile, damage);
    }

    /** Native missiles.cpp SpawnLightning decrements duration again and appends a bolt on each new tile. */
    private void SpawnLightning(AuthoritativeProjectile projectile, int damage)
    {
        var duration = checked((int)projectile.RemainingTicks - 1);
        var oldX = projectile.PositionX;
        var oldY = projectile.PositionY;
        var visited = MoveNativeProjectile(projectile);
        foreach (var tile in visited) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)
                || (tile.X != projectile.StartX || tile.Y != projectile.StartY)
                    && world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                duration = 0;
                break;
            }
        }
        projectile.NativeTrapArrow = projectile.NativeTrapArrow! with {
            MissileDistance = projectile.NativeTrapArrow.MissileDistance + 1,
        };
        if (world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)
            && !world.IsMissileBlocked(projectile.LevelId, projectile.PositionX, projectile.PositionY)
            && (projectile.PositionX != oldX || projectile.PositionY != oldY)) {
            AddLightning(projectile, damage);
        }
        if (duration <= 0)
            world.Projectiles.Remove(projectile.EntityId);
        else
            projectile.RemainingTicks = (uint)duration;
    }

    /** Native missiles.cpp AddLightning: parent position, original source start, random frame and eight ticks. */
    private void AddLightning(AuthoritativeProjectile parent, int damage)
    {
        _ = random.RandomIntBetween(1, 8);
        var segment = new AuthoritativeProjectile(AllocateProjectileEntityId(), parent.SourceEntityId,
            parent.TargetEntityId, 0, parent.LevelId, parent.PositionX, parent.PositionY,
            parent.TargetX, parent.TargetY, damage, AuthoritativeDamageType.Lightning, 0, 8, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment, NativeMissileId = "Lightning",
            StartX = parent.StartX, StartY = parent.StartY, TraveledX = parent.TraveledX, TraveledY = parent.TraveledY,
            OriginalDirection = parent.OriginalDirection,
            NativeTrapArrow = parent.NativeTrapArrow! with { Stage = NativeObjectMissileStage.LightningSegment,
                MissileDistance = 0, LastCollisionTargetHash = 0 },
        };
        AddNativeMissileFromMissile(segment);
    }

    /** Native missiles.cpp ProcessLightning: CheckMissileCol cannot consume a successful hit's remaining duration. */
    internal void ProcessLightning(ulong tick, AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events)
    {
        var duration = checked((int)projectile.RemainingTicks - 1);
        var remaining = duration;
        if ((projectile.PositionX != projectile.StartX || projectile.PositionY != projectile.StartY)
            && CheckMissileCol(tick, projectile, projectile.PositionX, projectile.PositionY,
                projectile.Damage, projectile.Damage, AuthoritativeDamageType.Lightning, false,
                ref duration, players, events, isDamageShifted: true))
            duration = remaining;
        if (duration == 0)
            world.Projectiles.Remove(projectile.EntityId);
        else
            projectile.RemainingTicks = (uint)duration;
    }

    /** Native missiles.cpp CheckMissileCol: monster, player, then tile; source-less traps hit both actor factions. */
    private bool CheckMissileCol(ulong tick, AuthoritativeProjectile projectile, int x, int y,
        int minimum, int maximum, AuthoritativeDamageType damageType, bool dontDeleteOnCollision, ref int duration,
        IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloDirectedGameplayEvent> events,
        bool isDamageShifted = false)
    {
        if (!world.ContainsPosition(projectile.LevelId, x, y))
            return false;
        var occupants = world.NativeActorOccupants(projectile.LevelId, x, y, players);
        var observer = players.First(player => player.LevelId == projectile.LevelId);
        var localEvents = new List<DiabloGameplayEvent>();
        var distance = projectile.NativeTrapArrow!.MissileTypeId is 0 or 27
            ? checked((int)projectile.NativeTrapArrow.MissileDistance) : 0;
        var hit = false;
        if (occupants.Monster is { } monster)
            hit |= MonsterTrapHit(observer, monster, minimum, maximum, distance, damageType, localEvents, isDamageShifted);
        if (occupants.Player is { } player)
            hit |= PlayerMHit(projectile.SourceEntityId, player, minimum, maximum, distance,
                damageType, projectile.NativeTrapArrow.MissileTypeId is 0 or 27, tick, players, localEvents, isDamageShifted);
        if (hit && !dontDeleteOnCollision)
            duration = 0;
        foreach (var gameplayEvent in localEvents)
            events.Add(new(observer, gameplayEvent));
        if (world.IsMissileBlocked(projectile.LevelId, x, y)) {
            BreakMissileBlockingObject(projectile.LevelId, x, y);
            if (!dontDeleteOnCollision)
                duration = 0;
            hit = false;
        }
        return hit;
    }

    /** Native engine/random.hpp RandomIntBetween: independently rolled FireArrow bounds may be reversed. */
    private int RandomTrapDamageBetween(int minimum, int maximum)
        => checked(minimum + random.RandomIntLessThan(checked(maximum - minimum + 1)));

    /** Native missiles.cpp MonsterTrapHit: Lightning carries Q6; other object missiles carry whole HP. */
    private bool MonsterTrapHit(AuthoritativePlayerState observer, AuthoritativeCombatTarget target,
        int minimum, int maximum, int distance, AuthoritativeDamageType damageType, ICollection<DiabloGameplayEvent> events,
        bool isDamageShifted)
    {
        if (target.HitPointsQ6 < 64 || target.DeathTicksRemaining > 0
            || target.DialogueMessageId != 0 || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target))
            return false;
        var resistance = GetMonsterResistance(target, damageType);
        if (resistance >= 100)
            return false;
        var hit = random.GenerateRnd(100);
        var chance = Math.Clamp(90 - GetEffectiveMonsterArmorClass(target) - distance, 5, 95);
        if (UsesNativeAi(target, "Gargoyle") && IsNativeGargoyleWakePending(target)) {
            _ = ConsumeNativeGargoyleWake(target);
            return true;
        }
        if (hit >= chance && !target.IsPetrified)
            return false;
        var damage = RandomTrapDamageBetween(minimum, maximum);
        if (!isDamageShifted)
            damage = checked(damage * 64);
        if (resistance > 0)
            damage /= 4;
        ApplyResolvedDamageQ6(observer, target, damage, events, nativeTrap: true, nativeTrapResisted: resistance > 0);
        if (target.HitPointsQ6 >= 64 && (resistance > 0 || target.NativeInitialization?.Binding.NativeTypeId != 109))
            random.AdvanceRndSeed();
        return true;
    }

    /** Native missiles.cpp PlayerMHit(nullptr), Arrow/FireArrow/Firebolt whole HP or Lightning Q6; shifted hits still roll, then suppress blocking. */
    internal bool PlayerMHit(uint sourceEntityId, AuthoritativePlayerState target,
        int minimum, int maximum, int distance, AuthoritativeDamageType damageType, bool arrow,
        ulong tick, IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloGameplayEvent> events,
        bool isDamageShifted = false)
    {
        if (target.Life < 64 || IsPlayerProtectedByFlash(target) || arrow && IsPlayerEtherealized(target))
            return false;
        var hit = random.GenerateRnd(100);
        var chance = arrow ? Math.Max(100 - target.ArmorClass / 2 - distance * 2, 10) : 40;
        var canBlock = target.CombatMode is DiabloPlayerCombatMode.Stand or DiabloPlayerCombatMode.Attack && CanBlock(target);
        var block = canBlock ? random.GenerateRnd(100) : 100;
        if (isDamageShifted)
            block = 100;
        var playerClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(target.Attributes.Dexterity.Current + (playerClass?.BaseBlockChance ?? 0), 0, 100);
        var resistance = GetPlayerResistance(target, playerClass, damageType);
        if (hit >= chance)
            return false;
        var units = isDamageShifted ? 1 : 64;
        var damage = RandomTrapDamageBetween(checked(minimum * units), checked(maximum * units));
        if (HasUsableEquipmentFlag(target, AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag))
            damage /= 2;
        damage = Math.Max(64, checked(damage + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment) * units));
        if (resistance <= 0 && block < blockChance) {
            StartBlock(target, playerClass, tick);
            return true;
        }
        if (resistance > 0)
            damage -= damage * resistance / 100;
        var result = playerDamage.Apply(target, Math.Max(0, damage));
        if (result.IncomingDamageQ6 > 0)
            events.Add(new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, sourceEntityId, target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) { AmountQ6 = result.IncomingDamageQ6 });
        if (target.Life < 64) {
            target.Life = 0;
            StartPlayerDeath(target, sourceEntityId, tick, players, PlayerDeathReason.MonsterOrTrap);
        } else {
            random.AdvanceRndSeed();
            if (resistance <= 0 && ShouldStartHit(target, playerClass, result.IncomingDamageQ6))
                StartHit(target, playerClass, tick);
        }
        return true;
    }
}
