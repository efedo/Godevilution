using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    private void AdvanceNativeCatacombsArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!players.Any(player => player.LevelId == projectile.LevelId))
            return;

        if (world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var liveSource))
            world.RefreshNativeCatacombsArrowSource(liveSource);
        if (!world.TryGetNativeCatacombsArrow(projectile, out var state))
            throw new InvalidDataException("A native Catacombs Arrow lost its private flight owner.");

        if (projectile.RemainingTicks == 0)
            throw new InvalidDataException("A native Catacombs Arrow expired without being removed.");
        projectile.RemainingTicks--;
        var nextDistance = checked(state.MissileDistance + 1);
        var visitedTiles = MoveNativeProjectile(projectile);
        short finalCollisionHash = state.LastCollisionTargetHash;

        if (visitedTiles.Count == 0) {
            var occupants = world.NativeTrapArrowOccupants(projectile.LevelId,
                projectile.PositionX, projectile.PositionY, players);
            if (state.LastCollisionTargetHash != occupants.Hash
                && ResolveNativeCatacombsArrowTile(tick, projectile, state, nextDistance, players, events)) {
                world.RemoveNativeCatacombsArrow(projectile.EntityId);
                return;
            }
            finalCollisionHash = occupants.Hash;
        } else {
            foreach (var tile in visitedTiles) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                if (tile.X == projectile.StartX && tile.Y == projectile.StartY)
                    continue;
                if (ResolveNativeCatacombsArrowTile(tick, projectile, state, nextDistance, players, events)) {
                    world.RemoveNativeCatacombsArrow(projectile.EntityId);
                    return;
                }
            }
            finalCollisionHash = world.NativeTrapArrowOccupants(projectile.LevelId,
                projectile.PositionX, projectile.PositionY, players).Hash;
        }

        // Native PutMissile marks a projectile for deletion as soon as its
        // updated tile leaves the dungeon bounds.
        if (!world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            world.RemoveNativeCatacombsArrow(projectile.EntityId);
            return;
        }

        if (projectile.RemainingTicks == 0) {
            world.RemoveNativeCatacombsArrow(projectile.EntityId);
            return;
        }
        world.UpdateNativeCatacombsArrow(state.Advance(nextDistance, finalCollisionHash), projectile);
    }

    /** Native CheckMissileCol order: monster, player, then tile/object blocker. */
    private bool ResolveNativeCatacombsArrowTile(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeNativeCatacombsArrowState state,
        uint distance,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        // Native CheckMissileCol does no actor or blocker work outside the
        // dungeon; the caller removes the missile after PutMissile marks it.
        if (!world.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY))
            return false;
        var occupants = world.NativeTrapArrowOccupants(projectile.LevelId,
            projectile.PositionX, projectile.PositionY, players);
        var stopped = false;
        var observer = players.First(player => player.LevelId == projectile.LevelId);
        if (occupants.Monster is { } monster)
            stopped |= ResolveNativeCatacombsArrowMonsterHit(state, monster,
                checked((int)distance), observer, events);
        if (occupants.Player is { } player)
            stopped |= ResolveNativeCatacombsArrowPlayerHit(tick, state, player, players, events);
        if (world.IsMissileBlocked(projectile.LevelId, projectile.PositionX, projectile.PositionY)) {
            BreakMissileBlockingObject(projectile.LevelId, projectile.PositionX, projectile.PositionY);
            stopped = true;
        }
        return stopped;
    }

    private bool ResolveNativeCatacombsArrowMonsterHit(
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativeCombatTarget target,
        int distance,
        AuthoritativePlayerState observer,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.HitPointsQ6 < 64 || target.DeathTicksRemaining > 0
            || target.DialogueMessageId != 0 || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target)
            || target.NativeAi == "Rhino" && target.RhinoMovementTicks >= 0)
            return false;

        var source = world.CombatTargets.GetValueOrDefault(state.SourceEntityId);
        var sourceIsMinion = source?.IsPlayerMinion ?? state.SourceIsPlayerMinion;
        var sourceIsBerserked = source?.IsBerserked ?? state.SourceIsBerserked;
        if (sourceIsMinion == target.IsPlayerMinion && !sourceIsBerserked && !target.IsBerserked)
            return false;

        var hit = random.GenerateRnd(100);
        var hitChance = Math.Clamp(90 - GetEffectiveMonsterArmorClass(target) - distance, 5, 95);
        if (UsesNativeAi(target, "Gargoyle") && IsNativeGargoyleWakePending(target)) {
            _ = ConsumeNativeGargoyleWake(target);
            return true;
        }
        if (hit >= hitChance && !target.IsPetrified)
            return false;

        var damageRange = ResolveNativeCatacombsArrowDamageRange(state, source);
        var damageQ6 = checked(random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum)
            * DeterministicFixedPointQ6.Scale);
        var hardHit = DeterministicFixedPointQ6.ToWholeTruncated(damageQ6)
            >= GetEffectiveMonsterLevel(target) + 3;

        // The native monster hit path shares the trap death transaction but
        // attributes the damage event to the firing monster.
        var nativeEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(observer, target, damageQ6, nativeEvents, nativeTrap: true);
        // Monster::startHit resets the native scavenger goal before checking
        // petrification. A hard hit that survives must therefore cancel both
        // corpse selection and the remaining eat/heal clock.
        if (target.HitPointsQ6 > 0 && hardHit && GetScavengerTuning(target).HasValue)
            target.ClearScavengerHealing();
        if (target.HitPointsQ6 > 0 && target.NativeAi != "Golem")
            random.AdvanceRndSeed();
        foreach (var gameplayEvent in nativeEvents) {
            var forwarded = gameplayEvent.Kind == DiabloGameplayEventKind.Damage
                ? gameplayEvent with { SourceEntityId = state.SourceEntityId }
                : gameplayEvent;
            events.Add(new DiabloDirectedGameplayEvent(observer, forwarded));
        }
        return true;
    }

    private (int Minimum, int Maximum) ResolveNativeCatacombsArrowDamageRange(
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativeCombatTarget? source)
    {
        if (source is not null)
            return GetEffectiveMonsterDamageRange(source, special: false);
        return (
            rules.DifficultyRules.ApplyDamageScaling(state.SourceMinimumDamage, difficulty),
            rules.DifficultyRules.ApplyDamageScaling(state.SourceMaximumDamage, difficulty));
    }

    private bool ResolveNativeCatacombsArrowPlayerHit(
        ulong tick,
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.Life < 64 || IsPlayerProtectedByFlash(target) || IsPlayerEtherealized(target))
            return false;

        var source = world.CombatTargets.GetValueOrDefault(state.SourceEntityId);
        var sourceLevel = source is null
            ? checked(state.SourceMonsterLevel + GetDifficultyLevelOffset())
            : GetEffectiveMonsterLevel(source);
        var sourceToHit = source is null
            ? checked(state.SourceToHit + (state.SourceIsPlayerMinion ? 0 : GetDifficultyToHitBonus()))
            : GetEffectiveMonsterToHit(source, special: false);
        var targetArmor = target.ArmorClass;
        if (rules.GameVariant == DiabloGameVariant.Hellfire && source is not null) {
            targetArmor = AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                targetArmor, source.IsDemon, source.IsUndead, target.Equipment,
                rules.HellfireArmorBonusAgainstDemons, rules.HellfireArmorBonusAgainstUndead);
        }

        var distance = checked((int)state.MissileDistance + 1);
        var hitChance = checked(sourceToHit
            + (sourceLevel - checked((int)target.CharacterLevel)) * rules.MonsterToHitLevelScale
            + rules.MonsterToHitBaseBonus
            - distance * rules.MonsterRangedDistancePenalty
            - targetArmor);
        hitChance = Math.Max(hitChance, rules.MonsterRangedMinimumHitChance);
        var hitRoll = random.GenerateRnd(100);
        var targetClass = GetPlayerClass(target);
        var canBlock = target.CombatMode is DiabloPlayerCombatMode.Stand or DiabloPlayerCombatMode.Attack
            && CanBlock(target);
        var blockRoll = canBlock ? random.GenerateRnd(100) : 100;
        if (hitRoll >= hitChance)
            return false;

        var range = ResolveNativeCatacombsArrowDamageRange(state, source);
        var damageQ6 = checked(random.RandomIntBetween(
            checked(range.Minimum * DeterministicFixedPointQ6.Scale),
            checked(range.Maximum * DeterministicFixedPointQ6.Scale))
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        damageQ6 = Math.Max(damageQ6, DeterministicFixedPointQ6.Scale);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current + (targetClass?.BaseBlockChance ?? 0)
                - (sourceLevel - checked((int)target.CharacterLevel)) * 2,
            0,
            100);
        if (blockRoll < blockChance) {
            var sourcePositionX = source?.PositionX ?? state.SourcePositionX;
            var sourcePositionY = source?.PositionY ?? state.SourcePositionY;
            target.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
                target.PositionX, target.PositionY, sourcePositionX, sourcePositionY);
            StartBlock(target, targetClass, tick);
            return true;
        }

        var result = playerDamage.Apply(target, damageQ6);
        if (result.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(target,
                new DiabloGameplayEvent(DiabloGameplayEventKind.Damage,
                    state.SourceEntityId, target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) {
                    AmountQ6 = result.IncomingDamageQ6,
                }));
        }
        if (target.Life < 64) {
            target.Life = 0;
            StartPlayerDeath(target, state.SourceEntityId, tick, players,
                PlayerDeathReason.MonsterOrTrap);
        } else {
            // Native Say(ArghClang) chooses a voice variant from the shared
            // RNG before the hit-recovery threshold is tested.
            random.AdvanceRndSeed();
            if (ShouldStartHit(target, targetClass, damageQ6))
                StartHit(target, targetClass, tick);
        }
        return true;
    }
}
