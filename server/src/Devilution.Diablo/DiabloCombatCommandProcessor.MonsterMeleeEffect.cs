using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Shared damage/event path, called only after AI or native animation admits its effect. */
    private void ResolveMonsterMeleeEffect(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        ICollection<DiabloDirectedGameplayEvent> events,
        int armorClass,
        uint attackSeed,
        bool usesSpecialAttack)
    {
        var usesNativeCombatStream = gameMode == DiabloGameMode.SinglePlayer
            || gameMode == DiabloGameMode.Multiplayer
                && target.UsesNativeRandomSeeds;
        var attackToHit = GetEffectiveMonsterToHit(target, usesSpecialAttack);
        if (usesNativeCombatStream && attackToHit > 0) {
            var hitChance = rules.ResolveMonsterAttackHitChance(
                attackToHit,
                GetEffectiveMonsterLevel(target),
                victim.CharacterLevel,
                target.LevelId,
                armorClass);
            if (random.RandomIntLessThan(100) >= hitChance)
                return;

            var victimClass = GetPlayerClass(victim);
            var canBlock = (victim.CombatMode is DiabloPlayerCombatMode.Stand
                    or DiabloPlayerCombatMode.Attack)
                && CanBlock(victim);
            var blockRoll = canBlock
                ? random.RandomIntLessThan(100)
                : 100;
            var blockChance = Math.Clamp(
                victim.Attributes.Dexterity.Current
                    + (victimClass?.BaseBlockChance ?? 0)
                    + checked((int)victim.CharacterLevel
                        * rules.MonsterPlayerBlockLevelScale)
                    - (rules.MonsterPlayerBlockLevelScale
                        * GetEffectiveMonsterLevel(target)),
                0,
                100);
            if (blockRoll < blockChance) {
                StartBlock(victim, victimClass, tick);
                // Native MonsterAttackPlayer still resolves Reflect on
                // a blocked hit. The blocked player takes no damage,
                // but the monster receives a percentage of the rolled
                // Q6 damage and one charge is consumed.
                if (victim.ReflectCharges > 0) {
                    victim.ReflectCharges--;
                    var reflectedBaseQ6 = AuthoritativeEquipmentIncomingDamageRules
                        .ApplyGetHitQ6(
                            RollEffectiveMonsterDamageQ6(target, usesSpecialAttack),
                            DeterministicFixedPointQ6.Scale,
                            victim.Equipment);
                    var reflectMinimum = reflectSpell?.ReflectDamagePercentMinimum > 0
                        ? reflectSpell.ReflectDamagePercentMinimum
                        : NativeReflectDamagePercentMinimum;
                    var reflectMaximum = reflectSpell?.ReflectDamagePercentMaximum > 0
                        ? reflectSpell.ReflectDamagePercentMaximum
                        : NativeReflectDamagePercentMaximum;
                    var reflectedDamageQ6 = checked(
                        reflectedBaseQ6
                        * random.RandomIntBetween(reflectMinimum, reflectMaximum)
                        / 100);
                    if (reflectedDamageQ6 > 0) {
                        var reflectedEvents = new List<DiabloGameplayEvent>();
                        ApplyResolvedDamageQ6(
                            victim,
                            target,
                            reflectedDamageQ6,
                            reflectedEvents);
                        foreach (var reflectedEvent in reflectedEvents)
                            events.Add(new DiabloDirectedGameplayEvent(
                                victim,
                                reflectedEvent));
                    }
                }
                return;
            }
        }

        var usesNativeFixedPointDamage = usesNativeCombatStream
            && attackToHit > 0;
        int damageQ6;
        if (usesNativeFixedPointDamage) {
            damageQ6 = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHitQ6(
                RollEffectiveMonsterDamageQ6(target, usesSpecialAttack),
                DeterministicFixedPointQ6.Scale,
                victim.Equipment);
        } else {
            var attackDamage = usesSpecialAttack
                ? usesNativeCombatStream
                    ? RollEffectiveMonsterDamage(target, special: true)
                    : SelectNativeSpecialDamage(target, attackSeed)
                : usesNativeCombatStream
                    ? RollEffectiveMonsterDamage(target, special: false)
                    : difficulty == DiabloDifficulty.Normal
                        ? target.AttackDamage
                        : SelectEffectiveMonsterDamage(
                            target,
                            special: false,
                            seed: attackSeed);
            var resolvedDamage = attackToHit == 0
                ? rules.ResolveAttackDamage(
                    attackDamage,
                    armorClass,
                    attackSeed)
                : rules.ResolveMonsterAttackDamage(
                    attackDamage,
                    attackToHit,
                    GetEffectiveMonsterLevel(target),
                    victim.CharacterLevel,
                    target.LevelId,
                    armorClass,
                    attackSeed);
            damageQ6 = DeterministicFixedPointQ6.FromWhole(
                AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                    resolvedDamage,
                    rules.MinimumDamage,
                    victim.Equipment));
        }
        var damage = DeterministicFixedPointQ6.ToWholeTruncated(damageQ6);
        var reflectedDamage = 0;
        if (damage > 0 && victim.ReflectCharges > 0) {
            victim.ReflectCharges--;
            var reflectMinimum = reflectSpell?.ReflectDamagePercentMinimum > 0
                ? reflectSpell.ReflectDamagePercentMinimum
                : NativeReflectDamagePercentMinimum;
            var reflectMaximum = reflectSpell?.ReflectDamagePercentMaximum > 0
                ? reflectSpell.ReflectDamagePercentMaximum
                : NativeReflectDamagePercentMaximum;
            reflectedDamage = checked(
                damage * random.RandomIntBetween(reflectMinimum, reflectMaximum) / 100);
            if (reflectedDamage > 0) {
                var reflectedEvents = new List<DiabloGameplayEvent>();
                ApplyResolvedDamageQ6(
                    victim,
                    target,
                    DeterministicFixedPointQ6.FromWhole(reflectedDamage),
                    reflectedEvents);
                foreach (var reflectedEvent in reflectedEvents)
                    events.Add(new DiabloDirectedGameplayEvent(victim, reflectedEvent));
            }
        }
        var incomingDamageQ6 = Math.Max(
            0,
            damageQ6 - DeterministicFixedPointQ6.FromWhole(reflectedDamage));
        // Direct compatibility fixtures historically store whole life
        // points, while authoritative class-backed players store Q6.
        var usesLegacyWholeLife = victim.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
        var appliedDamageQ6 = usesLegacyWholeLife
            ? DeterministicFixedPointQ6.ToWholeTruncated(incomingDamageQ6)
            : incomingDamageQ6;
        var damageResult = playerDamage.Apply(victim, appliedDamageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            var eventDamageQ6 = usesLegacyWholeLife
                ? incomingDamageQ6
                : damageResult.IncomingDamageQ6;
            events.Add(new DiabloDirectedGameplayEvent(
                victim,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    target.EntityId,
                    victim.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        eventDamageQ6)) {
                    AmountQ6 = eventDamageQ6,
                }));
        }
        // Native thorns retaliates after a successful monster hit,
        // before the player death check, using one content-owned Q6 roll.
        if (damage > 0
            && target.HitPointsQ6 > 0
            && HasUsableEquipmentFlag(
                victim,
                AuthoritativeEquipmentIncomingDamageRules.ThornsFlag)) {
            var thornsEvents = new List<DiabloGameplayEvent>();
            ApplyResolvedDamageQ6(
                victim,
                target,
                DeterministicFixedPointQ6.FromWhole(random.RandomIntBetween(
                    rules.ThornsDamageMinimum,
                    rules.ThornsDamageMaximum)),
                thornsEvents);
            foreach (var thornsEvent in thornsEvents)
                events.Add(new DiabloDirectedGameplayEvent(victim, thornsEvent));
        }
        if (victim.Life == 0)
            StartPlayerDeath(
                victim,
                target.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (damage > 0 && target.HitPointsQ6 > 0)
            ApplyMonsterKnockback(target, victim, players);
        var batTuning = GetBatAttackTuning(target);
        if (target.HitPointsQ6 > 0 && batTuning.HasValue)
            target.StartBatRetreat(
                GetDirection(
                    target.PositionX,
                    target.PositionY,
                    victim.PositionX,
                    victim.PositionY),
                batTuning.Value.RetreatDurationTicks);
        if (target.HitPointsQ6 > 0
            && TryGetMonsterMissile(target, out var postMeleeMissile)
            && IsFamiliarLightningMissile(postMeleeMissile))
            CreateFamiliarLightningProjectile(target, victim, postMeleeMissile);
    }
}
