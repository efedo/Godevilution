using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    private bool TryGetNativeMeleeBarrel(AuthoritativePlayerState player, int targetX, int targetY,
        out AuthoritativeWorldObject? barrel)
    {
        var direction = DeterministicGridDirections.Toward(player.PositionX, player.PositionY, targetX, targetY);
        var offset = DeterministicGridDirections.GetDisplacement(direction);
        var x = checked(player.PositionX + offset.X);
        var y = checked(player.PositionY + offset.Y);
        barrel = world.Objects.Values.SingleOrDefault(value => value.LevelId == player.LevelId
            && value.PositionX == x && value.PositionY == y && world.CanBreakNativeBarrel(value));
        return barrel is not null;
    }

    /** One native melee callback, including immediate depth-first explosion consequences. */
    internal bool TryBreakNativeMeleeBarrel(AuthoritativePlayerState breaker, AuthoritativeWorldObject barrel,
        ulong tick, IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloGameplayEvent> events,
        bool chainValidated = false)
    {
        var participants = players.Contains(breaker) ? players : players.Append(breaker).ToArray();
        if (!chainValidated && !CanCommitNativeBarrelChain(barrel, participants))
            return false;
        var mutation = world.TryBreakNativeCathedralBarrel(barrel, breaker, participants,
            random.State, () => AllocateWorldItemEntityId(participants));
        if (mutation is null)
            return false;
        // Publish the ordinary seed reset or unchanged explosive stream before
        // any hit/death/loot/recursive break can consume the live RNG.
        random.Restore(mutation.FinalWorldRandomState);
        if (!mutation.Operation.IsExplosive)
            return true;
        for (var y = barrel.PositionY - 1; y <= barrel.PositionY + 1; y++) {
            for (var x = barrel.PositionX - 1; x <= barrel.PositionX + 1; x++) {
                var occupants = world.NativeBarrelBlastOccupants(barrel.LevelId, x, y, participants);
                if (occupants.Monster is { } monster)
                    ResolveNativeBarrelMonsterHit(breaker, monster, events);
                // Monster death may publish loot and change the shared RNG;
                // the player hit must observe those effects before recursion.
                if (occupants.Player is { } player)
                    ResolveNativeBarrelPlayerHit(barrel.EntityId, player, tick, participants, events);
                var adjacent = world.Objects.Values.SingleOrDefault(value => value.LevelId == barrel.LevelId
                    && value.PositionX == x && value.PositionY == y && value.NativeObjectId == 58
                    && !value.Activated && !value.Destroyed);
                if (adjacent is not null
                    && !TryBreakNativeMeleeBarrel(breaker, adjacent, tick, participants, events, chainValidated: true))
                    throw new InvalidOperationException("An eligible explosive chain member could not commit.");
            }
        }
        return true;
    }

    private bool CanCommitNativeBarrelChain(AuthoritativeWorldObject initial,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        world.ValidateNativePopulationState(players);
        var pending = new Stack<AuthoritativeWorldObject>();
        var visited = new HashSet<uint>();
        pending.Push(initial);
        while (pending.TryPop(out var barrel)) {
            if (!visited.Add(barrel.EntityId))
                continue;
            if (!world.CanBreakNativeBarrel(barrel))
                return false;
            if (barrel.NativeObjectId != 58)
                continue;
            foreach (var neighbor in world.Objects.Values.Where(value => value.LevelId == barrel.LevelId
                && value.NativeObjectId == 58 && !value.Activated && !value.Destroyed
                && Math.Max(Math.Abs(value.PositionX - barrel.PositionX),
                    Math.Abs(value.PositionY - barrel.PositionY)) <= 1))
                pending.Push(neighbor);
        }
        return true;
    }

    /** MonsterTrapHit(Firebolt, 1..4, distance 0, unshifted). */
    private void ResolveNativeBarrelMonsterHit(AuthoritativePlayerState breaker,
        AuthoritativeCombatTarget target, ICollection<DiabloGameplayEvent> events)
    {
        if (target.HitPointsQ6 < 64 || target.DeathTicksRemaining > 0
            || target.DialogueMessageId != 0 || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target) || IsNativeRetreatTarget(target))
            return;
        var resistance = GetMonsterResistance(target, AuthoritativeDamageType.Fire);
        if (resistance >= 100)
            return;
        var hit = random.GenerateRnd(100);
        var chance = Math.Clamp(90 - GetEffectiveMonsterArmorClass(target), 5, 95);
        if (UsesNativeAi(target, "Gargoyle") && IsNativeGargoyleWakePending(target)) {
            _ = ConsumeNativeGargoyleWake(target);
            return;
        }
        if (hit >= chance && !target.IsPetrified)
            return;
        var damage = checked(random.RandomIntBetween(1, 4) * 64);
        if (resistance > 0)
            damage /= 4;
        ApplyResolvedDamageQ6(breaker, target, damage, events,
            nativeTrap: true, nativeTrapResisted: resistance > 0);
        // Native playEffect(Hit) selects a variant even with audio disabled.
        // Resistant hits always play it; unresisted golems omit startHit.
        if (target.HitPointsQ6 >= 64
            && (resistance > 0 || target.NativeInitialization?.Binding.NativeTypeId != 109))
            random.AdvanceRndSeed();
    }

    /** Native single-player _addExperience: level scaling, max-level refusal and cumulative cap. */
    private uint GetNativeTrapExperienceReward(AuthoritativePlayerState player, AuthoritativeCombatTarget target)
    {
        var maximumLevel = experience?.MaximumLevel ?? AuthoritativeExperienceCatalog.MaximumSupportedLevel;
        if (player.Life < 64 || player.CharacterLevel >= maximumLevel)
            return 0;
        var reward = GetEffectiveMonsterExperienceReward(target);
        var delta = GetEffectiveMonsterLevel(target) - (int)player.CharacterLevel;
        var scaled = (uint)Math.Clamp((long)(reward * (1 + delta / 10.0)), 0L, uint.MaxValue);
        var maximumExperience = experience?.GetRequiredExperience(maximumLevel) ?? checked(maximumLevel * 1000U);
        var remaining = maximumExperience - Math.Min(maximumExperience, player.Experience);
        return Math.Min(scaled, remaining);
    }

    /** PlayerMHit(Firebolt, no monster source, 8..16, distance 0, unshifted). */
    private void ResolveNativeBarrelPlayerHit(uint sourceEntityId, AuthoritativePlayerState target,
        ulong tick, IReadOnlyList<AuthoritativePlayerState> players, ICollection<DiabloGameplayEvent> events)
    {
        if (target.Life < 64 || IsPlayerProtectedByFlash(target))
            return;
        var hit = random.GenerateRnd(100);
        var canBlock = target.CombatMode is DiabloPlayerCombatMode.Stand or DiabloPlayerCombatMode.Attack
            && CanBlock(target);
        var block = canBlock ? random.GenerateRnd(100) : 100;
        // Native rolls block before rejecting a miss, and rolls Q6 damage even
        // for a subsequently blocked hit.
        if (hit >= 40)
            return;
        var damage = random.RandomIntBetween(8 * 64, 16 * 64);
        if (HasUsableEquipmentFlag(target, AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag))
            damage /= 2;
        damage = Math.Max(64, checked(damage
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment) * 64));
        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(target, targetClass, AuthoritativeDamageType.Fire);
        var blockChance = Math.Clamp(target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0), 0, 100);
        if (resistance <= 0 && block < blockChance) {
            StartBlock(target, targetClass, tick);
            return;
        }
        if (resistance > 0)
            damage -= damage * resistance / 100;
        var result = playerDamage.Apply(target, Math.Max(0, damage));
        if (result.IncomingDamageQ6 > 0)
            events.Add(new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, sourceEntityId,
                target.EntityId, DeterministicFixedPointQ6.ToWholeTruncated(result.IncomingDamageQ6)) {
                AmountQ6 = result.IncomingDamageQ6,
            });
        if (target.Life < 64) {
            target.Life = 0;
            StartPlayerDeath(target, sourceEntityId, tick, players, PlayerDeathReason.MonsterOrTrap);
        } else {
            // Native Say(ArghClang) selects one of two voice variants before
            // testing hard-hit recovery, including resisted hits and NOSOUND.
            // This consumes the gameplay stream even when no audio is played.
            random.AdvanceRndSeed();
            if (resistance <= 0 && ShouldStartHit(target, targetClass, result.IncomingDamageQ6))
                StartHit(target, targetClass, tick);
        }
    }
}
