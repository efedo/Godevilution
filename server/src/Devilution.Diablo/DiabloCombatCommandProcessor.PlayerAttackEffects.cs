using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Player hit damage, elemental, knockback and durability effects (native player.cpp PlrHitMonst). */
public sealed partial class DiabloCombatCommandProcessor
{
    /** Native AddArrow velocity and the corresponding tile-travel lifetime. */
    private (int Speed, uint Lifetime) ResolveWeaponArrowTravelTiming(
        AuthoritativePlayerState player,
        long targetDistance)
    {
        var speed = 32;
        if (HasUsableEquipmentFlag(
                player,
                AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag)) {
            speed = random.RandomIntBetween(16, 47);
        }

        var playerClass = GetPlayerClass(player);
        var level = checked((int)player.CharacterLevel);
        if (playerClass?.RangedArrowSpeedLevelDivisor > 0)
            speed = checked(
                speed
                + Math.Max(0, level - 1) / playerClass.RangedArrowSpeedLevelDivisor);

        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            var flags = GetUsableEquipmentFlags(player);
            if ((flags & QuickAttackFlag) != 0)
                speed++;
            if ((flags & FastAttackFlag) != 0)
                speed = checked(speed + 2);
            if ((flags & FasterAttackFlag) != 0)
                speed = checked(speed + 4);
            if ((flags & FastestAttackFlag) != 0)
                speed = checked(speed + 8);
        }

        // Preserve the compact legacy representation when native AddArrow
        // resolves to its default 32-pixel cadence.
        if (speed == 32)
            return (0, (uint)Math.Min(
                (long)uint.MaxValue,
                Math.Max(1L, targetDistance)));

        var travelTiles = Math.Max(0L, targetDistance - 1);
        var travelTicks = (travelTiles * 32 + speed - 1) / speed;
        return (speed, (uint)Math.Min(
            (long)uint.MaxValue,
            travelTicks + 1));
    }

    /** Applies native fire/lightning weapon channels after a physical hit. */
    private void ApplyEquipmentElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ICollection<DiabloGameplayEvent> events,
        bool ranged)
    {
        var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
            player.Equipment,
            random.GenerateRnd,
            ranged);
        if (elemental.Fire > 0) {
            var fireDamage = rules.ResolveDamage(
                elemental.Fire,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Fire,
                target);
            ApplyResolvedDamage(player, target, fireDamage, events);
        }
        if (elemental.Lightning > 0 && target.HitPointsQ6 > 0) {
            var lightningDamage = rules.ResolveDamage(
                elemental.Lightning,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Lightning,
                target);
            ApplyResolvedDamage(player, target, lightningDamage, events);
        }
    }

    /** Applies native fire/lightning weapon channels to a live player target. */
    private void ApplyPlayerElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        AuthoritativeCombatTarget targetProxy,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
            player.Equipment,
            random.GenerateRnd,
            ranged: false);
        ApplyPlayerElementalChannels(
            player,
            target,
            targetProxy,
            elemental,
            appliedTick,
            players,
            events);
    }

    private void ApplyPlayerElementalChannels(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        AuthoritativeCombatTarget targetProxy,
        AuthoritativeEquipmentElementalDamageRules.ElementalDamage elemental,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (elemental.Fire > 0) {
            var fireDamage = rules.ResolveDamage(
                elemental.Fire,
                targetProxy.ArmorClass,
                AuthoritativeDamageType.Fire,
                targetProxy);
            ApplyPlayerElementalDamageQ6(
                player,
                target,
                DeterministicFixedPointQ6.FromWhole(fireDamage),
                appliedTick,
                players,
                events);
        }
        if (elemental.Lightning > 0 && target.Life > 0) {
            var lightningDamage = rules.ResolveDamage(
                elemental.Lightning,
                targetProxy.ArmorClass,
                AuthoritativeDamageType.Lightning,
                targetProxy);
            ApplyPlayerElementalDamageQ6(
                player,
                target,
                DeterministicFixedPointQ6.FromWhole(lightningDamage),
                appliedTick,
                players,
                events);
        }
    }

    private void ApplyPlayerElementalDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        int damageQ6,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (damageQ6 <= 0 || target.Life <= 0)
            return;
        var damageResult = playerDamage.Apply(target, damageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(
                    damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                player.EntityId,
                appliedTick,
                players,
                PlayerDeathReason.Player);
    }

    /** Applies native knockback after a successful non-lethal physical hit. */
    private void ApplyEquipmentKnockback(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damageQ6,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (damageQ6 <= 0
            || target.HitPoints <= 0
            || target.IsPetrified
            || (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.KnockbackFlag) == 0) {
            return;
        }

        var directionX = Math.Sign((long)target.PositionX - player.PositionX);
        var directionY = Math.Sign((long)target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return;
        var destinationX = target.PositionX + directionX;
        var destinationY = target.PositionY + directionY;
        if (world.IsWalkable(target.LevelId, destinationX, destinationY)
            && !world.IsOccupied(
                target.LevelId,
                destinationX,
                destinationY,
                players,
                target.EntityId)) {
            target.ApplyKnockback(destinationX, destinationY);
        }
    }

    /** Applies native knockback equipment to a live adjacent player target. */
    private void ApplyPlayerKnockback(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        int damageQ6,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (damageQ6 <= 0
            || target.Life <= 0
            || (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.KnockbackFlag) == 0) {
            return;
        }

        var directionX = Math.Sign((long)target.PositionX - player.PositionX);
        var directionY = Math.Sign((long)target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return;
        var destinationX = checked(target.PositionX + directionX);
        var destinationY = checked(target.PositionY + directionY);
        if (!world.IsWalkable(target.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                target.LevelId,
                destinationX,
                destinationY,
                players,
                target.EntityId)) {
            return;
        }

        DiabloPlayerActionLifecycle.Cancel(target);
        target.PositionX = destinationX;
        target.PositionY = destinationY;
    }

    /** Applies native monster MFLAG_KNOCKBACK after a successful player hit. */
    private void ApplyMonsterKnockback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!monster.HasKnockback || player.Life <= 0)
            return;

        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var destinationX = checked(player.PositionX + displacement.X);
        var destinationY = checked(player.PositionY + displacement.Y);
        if (!world.IsWalkable(monster.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                monster.LevelId,
                destinationX,
                destinationY,
                players,
                player.EntityId)) {
            return;
        }

        DiabloPlayerActionLifecycle.Cancel(player);
        player.PositionX = destinationX;
        player.PositionY = destinationY;
    }

    /** Applies native usable equipment resource gains after a successful hit. */
    private void ApplyEquipmentAttackEffects(
        AuthoritativePlayerState player,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events)
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6,
            GetUsableEquipmentFlags(player),
            random.GenerateRnd,
            rules.RandomStealLifeDivisor,
            rules.StealLife3Percent,
            rules.StealLife5Percent,
            rules.StealMana3Percent,
            rules.StealMana5Percent);
        var lifeGain = Math.Min(
            Math.Max(0, player.LifeMaximum - player.Life),
            gain.LifeQ6);
        player.Life = checked(player.Life + lifeGain);
        var manaGain = Math.Min(
            Math.Max(0, player.ManaMaximum - player.Mana),
            gain.ManaQ6);
        player.Mana = checked(player.Mana + manaGain);
        if (lifeGain > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Healing,
                player.EntityId,
                player.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(lifeGain)) {
                AmountQ6 = lifeGain,
            });
        }
    }

    /** Applies native Hellfire Decay to eligible hand weapons in native order. */
    private bool ApplyHellfireWeaponDecay(AuthoritativePlayerState player)
    {
        if (rules.GameVariant != DiabloGameVariant.Hellfire)
            return false;

        foreach (var slot in new uint[] { 4, 5 }) {
            var equipmentIndex = player.Equipment.FindIndex(item => item.Slot == slot);
            if (equipmentIndex < 0)
                continue;
            var equippedItem = player.Equipment[equipmentIndex];
            var state = equippedItem.State;
            if (!IsUsable(state)
                || (state.Magical != 0 && !state.Identified)
                || !AuthoritativeItemClassification.IsWeapon(state, items)
                || (state.HellfireDamageArmorFlags
                    & AuthoritativeEquipmentIncomingDamageRules.DecayFlag) == 0) {
                continue;
            }

            var decayedState = state with {
                PlusDamage = checked(state.PlusDamage - rules.HellfireDecayDamageReduction),
            };
            if (decayedState.PlusDamage <= -rules.HellfireDecayRemovalThreshold)
                player.Equipment.RemoveAt(equipmentIndex);
            else
                player.Equipment[equipmentIndex] = equippedItem with {
                    State = decayedState,
                };
            player.RecalculateEquipmentResources();
            if (decayedState.PlusDamage <= -rules.HellfireDecayRemovalThreshold)
                return true;
        }
        return false;
    }

    /** Mirrors native DamageWeapon's slot ordering and break semantics. */
    private bool ApplyNativeWeaponDurabilityLoss(
        AuthoritativePlayerState player,
        uint damageFrequency)
    {
        // Compact replay fixtures may carry compatibility weapons without
        // durability metadata. Native server-backed clients do not mutate
        // those placeholder items, so they must not consume a durability roll.
        if (!HasNativeWeaponDurabilityTarget(player))
            return false;
        if (!random.FlipCoin(damageFrequency))
            return false;

        var leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        if (leftIndex >= 0
            && AuthoritativeItemClassification.IsWeapon(
                player.Equipment[leftIndex].State,
                items)) {
            if (player.Equipment[leftIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[leftIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        var rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex >= 0
            && AuthoritativeItemClassification.IsWeapon(
                player.Equipment[rightIndex].State,
                items)) {
            if (player.Equipment[rightIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[rightIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(rightIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (leftIndex < 0
            && rightIndex >= 0
            && AuthoritativeItemClassification.IsShield(
                player.Equipment[rightIndex].State,
                items)) {
            if (player.Equipment[rightIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[rightIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(rightIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex < 0
            && leftIndex >= 0
            && AuthoritativeItemClassification.IsShield(
                player.Equipment[leftIndex].State,
                items)) {
            if (player.Equipment[leftIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[leftIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        return false;
    }

    private bool HasNativeWeaponDurabilityTarget(AuthoritativePlayerState player)
    {
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        if (left is not null
            && AuthoritativeItemClassification.IsWeapon(left.State, items)
            && left.State.MaxDurability > 0)
            return true;
        if (right is not null
            && AuthoritativeItemClassification.IsWeapon(right.State, items)
            && right.State.MaxDurability > 0)
            return true;
        if (left is null
            && right is not null
            && AuthoritativeItemClassification.IsShield(right.State, items)
            && right.State.MaxDurability > 0)
            return true;
        return right is null
            && left is not null
            && AuthoritativeItemClassification.IsShield(left.State, items)
            && left.State.MaxDurability > 0;
    }

    /** Mirrors native DamageParryItem's left-hand-first block damage. */
    private void ApplyNativeParryDurabilityLoss(AuthoritativePlayerState player)
    {
        var leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        if (leftIndex >= 0
            && (AuthoritativeItemClassification.IsShield(
                    player.Equipment[leftIndex].State,
                    items)
                || AuthoritativeItemClassification.IsStaff(
                    player.Equipment[leftIndex].State,
                    items))) {
            var state = player.Equipment[leftIndex].State;
            if (state.Durability == 255)
                return;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
            } else {
                player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                    State = state with { Durability = checked(state.Durability - 1) },
                };
            }
        }

        var rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex < 0
            || !AuthoritativeItemClassification.IsShield(
                player.Equipment[rightIndex].State,
                items)) {
            return;
        }

        var rightState = player.Equipment[rightIndex].State;
        if (rightState.Durability == 255)
            return;
        if (rightState.Durability <= 1) {
            player.Equipment.RemoveAt(rightIndex);
            player.RecalculateEquipmentResources();
        } else {
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = rightState with { Durability = checked(rightState.Durability - 1) },
            };
        }
    }

    /** Applies native player-versus-player resource restoration after a hit. */
    private void ApplyPlayerAttackEffects(
        AuthoritativePlayerState player,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events)
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.ResolvePlayerTarget(
            damageQ6,
            GetUsableEquipmentFlags(player),
            random.GenerateRnd,
            rules.RandomStealLifeDivisor);
        var lifeGain = Math.Min(
            Math.Max(0, player.LifeMaximum - player.Life),
            gain.LifeQ6);
        player.Life = checked(player.Life + lifeGain);
        if (lifeGain > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Healing,
                player.EntityId,
                player.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(lifeGain)) {
                AmountQ6 = lifeGain,
            });
        }
    }

    /** Mirrors native DamageArmor's helmet/chest selection and break path. */
    private void ApplyNativeArmorDurabilityLoss(AuthoritativePlayerState player)
    {
        var headIndex = player.Equipment.FindIndex(item => item.Slot == 0);
        var chestIndex = player.Equipment.FindIndex(item => item.Slot == 6);
        if (headIndex < 0 && chestIndex < 0)
            return;

        var targetHead = random.FlipCoin(3);
        if (chestIndex < 0)
            targetHead = true;
        else if (headIndex < 0)
            targetHead = false;

        var equipmentIndex = targetHead ? headIndex : chestIndex;
        if (equipmentIndex < 0)
            return;
        var state = player.Equipment[equipmentIndex].State;
        if (state.Durability == 255)
            return;
        if (state.Durability <= 1) {
            player.Equipment.RemoveAt(equipmentIndex);
            player.RecalculateEquipmentResources();
        } else {
            player.Equipment[equipmentIndex] = player.Equipment[equipmentIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }
    }

    private bool HasNativeArmorDurabilityTarget(AuthoritativePlayerState player)
    {
        return player.Equipment.Any(item =>
            (item.Slot is 0 or 6)
            && item.State.MaxDurability > 0);
    }

    private bool HasNativeParryDurabilityTarget(AuthoritativePlayerState player)
    {
        return player.Equipment.Any(item =>
            (item.Slot is 4 or 5)
            && item.State.MaxDurability > 0
            && (AuthoritativeItemClassification.IsShield(item.State, items)
                || AuthoritativeItemClassification.IsStaff(item.State, items)));
    }

    /**
     * Applies native usable equipment damage and hit bonuses, returning Q6
     * damage. PvP deliberately keeps its native armor boundary separate from
     * monster combat: armor piercing only affects monster admission, player
     * armor only affects PvP admission, and neither reduces player damage.
     */
    private int ResolvePlayerAttackDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        bool monsterTarget,
        bool ranged,
        bool deferGargoyleWake,
        int targetDistance,
        bool adjacentDamage,
        uint seed,
        out int perilSelfDamageQ6,
        out bool perilActive,
        out bool doppelgangerProc,
        out bool attackHit,
        bool nativeImpactRoll = false)
    {
        perilSelfDamageQ6 = 0;
        perilActive = false;
        doppelgangerProc = false;
        attackHit = false;
        if (monsterTarget
            && !deferGargoyleWake
            && !nativeImpactRoll
            && string.Equals(target.NativeAi, "Gargoyle", StringComparison.Ordinal)
            && IsNativeGargoyleWakePending(target)) {
            // Native PlrHitMonst/MonsterMHit calls tryLiftGargoyle before
            // applying damage. The waking hit is admitted but deals neither
            // physical nor equipment elemental damage.
            _ = ConsumeNativeGargoyleWake(target);
            return 0;
        }
        var damagePercent = 0;
        var damageFlat = 0;
        var hitBonus = 0;
        var armorPiercingBonus = 0;
        foreach (var equippedItem in player.Equipment) {
            var state = equippedItem.State;
            if (!IsUsable(state) || (state.Magical != 0 && !state.Identified))
                continue;
            damagePercent = checked(damagePercent + state.PlusDamage);
            damageFlat = checked(damageFlat + state.PlusDamageModifier);
            hitBonus = checked(hitBonus + state.PlusToHit);
            armorPiercingBonus = checked(armorPiercingBonus + state.PlusEnemyArmorClass);
        }

        var playerClass = GetPlayerClass(player);
        // Legacy hand-built fixtures may omit class-derived attributes; retain
        // the global damage fallback for those compatibility templates.
        var usesNativeClassProfile = playerClass is not null
            && player.Attributes.Strength.Current > 0
            && player.Attributes.Dexterity.Current > 0;
        if (nativeImpactRoll && (!ranged || !usesNativeClassProfile))
            throw new InvalidDataException(
                "Native population arrows require a ranged attack with complete class attributes.");
        var damageProfile = usesNativeClassProfile
            ? AuthoritativeEquipmentDamageRules.Calculate(
                player.CharacterLevel,
                player.Attributes,
                player.Equipment,
                playerClass!,
                items,
                rules.BarbarianWeaponStrengthDamageDivisor,
                rules.RogueStrengthDexterityDamageDivisor,
                rules.MonkStaffUnarmedDamageDivisor,
                rules.MonkWeaponDamageDivisor,
                rules.BardSwordStrengthDexterityDamageDivisor,
                rules.BardBowStrengthDexterityDamageDivisor,
                rules.BardDefaultStrengthDamageDivisor,
                rules.StandardStrengthDamageDivisor,
                rules.BarbarianBowStrengthDamageDivisor,
                rules.BarbarianDefaultStrengthDamageDivisor,
                rules.BarbarianVitalityDamageDivisor)
            : new AuthoritativePhysicalDamageProfile(
                rules.BaseAttackDamage,
                rules.BaseAttackDamage,
                0);
        var rolledBaseDamage = usesNativeClassProfile
            ? nativeImpactRoll
                ? 0
                : random.RandomIntBetween(
                    damageProfile.MinimumDamage,
                    damageProfile.MaximumDamage)
            : rules.BaseAttackDamage;
        var damageBeforeFlat = checked(
            rolledBaseDamage
            + rolledBaseDamage * damagePercent / 100);
        var classDamageModifier = damageProfile.DamageModifier;
        if (!monsterTarget
            && ranged
            && usesNativeClassProfile
            && playerClass!.PhysicalDamageFormula
                != AuthoritativePhysicalDamageFormula.Rogue) {
            // Native Plr2PlrMHit halves _pDamageMod for non-Rogue physical
            // arrows; melee PvP and all monster attacks retain the full
            // class-derived modifier.
            classDamageModifier /= 2;
        }
        var damage = checked(damageBeforeFlat + damageFlat + classDamageModifier);
        perilActive = rules.GameVariant == DiabloGameVariant.Hellfire
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.PerilFlag);
        perilSelfDamageQ6 = 0;
        if (perilActive) {
            perilSelfDamageQ6 = checked(
                DeterministicFixedPointQ6.FromWhole(
                    checked(damageBeforeFlat + damageFlat))
                + DeterministicFixedPointQ6.FromWhole(
                    AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(
                        player.Equipment)));
            perilSelfDamageQ6 = Math.Max(0, perilSelfDamageQ6);
        }
        // Legacy hand-built fixtures may omit class-derived attributes; retain
        // the historical global hit fallback for those compatibility templates.
        var usesNativeClassToHit = playerClass is not null
            && player.Attributes.Strength.Current > 0
            && player.Attributes.Dexterity.Current > 0;
        var classHitChance = !usesNativeClassToHit
            ? rules.HitChancePercent
            : playerClass!.CalculatePhysicalToHit(
                player.CharacterLevel,
                player.Attributes.Dexterity.Current,
                ranged);
        var targetArmorClass = monsterTarget
            ? GetEffectiveMonsterArmorClass(target)
            : target.ArmorClass;
        var rangedDistancePenalty = nativeImpactRoll
            ? GetNativeArrowDistancePenalty(targetDistance)
            : targetDistance * targetDistance / 2;
        var effectiveHitChance = usesNativeClassToHit
            ? monsterTarget
                ? rules.ResolvePhysicalHitChance(
                    checked(
                        classHitChance
                        + hitBonus
                        - (adjacentDamage
                            ? player.CharacterLevel > 20
                                ? 30
                                : checked((35 - (int)player.CharacterLevel) * 2)
                            : 0)
                        - (ranged
                            ? rangedDistancePenalty
                            : 0)),
                    targetArmorClass,
                    armorPiercingBonus,
                    meleeArmorReductionDivisor: !ranged
                        ? playerClass!.MeleeArmorReductionDivisor
                        : 0)
                : Math.Clamp(
                    checked(
                        classHitChance
                        + hitBonus
                        - (adjacentDamage
                            ? player.CharacterLevel > 20
                                ? 30
                                : checked((35 - (int)player.CharacterLevel) * 2)
                            : 0)
                        - (ranged
                            ? rangedDistancePenalty
                            : 0)
                        - targetArmorClass),
                    5,
                    95)
            : Math.Clamp(
                checked(
                    classHitChance
                    + hitBonus
                    + (monsterTarget ? armorPiercingBonus : 0)
                    - (!monsterTarget && ranged
                        ? targetDistance * targetDistance / 2
                        : 0)
                    - (!monsterTarget ? targetArmorClass : 0)),
                0,
                100);
        // Native PlrHitMonst/MonsterMHit set the hit roll to zero for a
        // petrified monster, making Stone Curse a guaranteed-hit boundary.
        if (monsterTarget && target.IsPetrified)
            effectiveHitChance = 100;
        var nativeHitRoll = nativeImpactRoll
            ? random.RandomIntLessThan(100)
            : 0;
        if (nativeImpactRoll
            && monsterTarget
            && string.Equals(target.NativeAi, "Gargoyle", StringComparison.Ordinal)
            && IsNativeGargoyleWakePending(target)) {
            // Native MonsterMHit consumes the collision roll, then tries the
            // Gargoyle lift before comparing that roll with hit chance.
            _ = ConsumeNativeGargoyleWake(target);
            attackHit = true;
            return 0;
        }
        attackHit = nativeImpactRoll
            ? nativeHitRoll < effectiveHitChance
            : rules.ResolveAttackHit(effectiveHitChance, seed);
        if (!attackHit) {
            perilActive = false;
            perilSelfDamageQ6 = 0;
            return 0;
        }
        if (nativeImpactRoll) {
            // MonsterMHit draws weapon damage only after the hit check passes.
            rolledBaseDamage = random.RandomIntBetween(
                damageProfile.MinimumDamage,
                damageProfile.MaximumDamage);
            damageBeforeFlat = checked(
                rolledBaseDamage
                + rolledBaseDamage * damagePercent / 100);
            damage = checked(damageBeforeFlat + damageFlat + classDamageModifier);
            if (perilActive) {
                perilSelfDamageQ6 = checked(
                    DeterministicFixedPointQ6.FromWhole(
                        checked(damageBeforeFlat + damageFlat))
                    + DeterministicFixedPointQ6.FromWhole(
                        AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(
                            player.Equipment)));
                perilSelfDamageQ6 = Math.Max(0, perilSelfDamageQ6);
            }
        }
        var criticalDamage = damage;
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.CriticalStrike) == true
            && random.RandomIntLessThan(100) < Math.Min(
                100,
                checked(
                    (int)player.CharacterLevel
                    * playerClass.CriticalStrikeChancePerLevelPercent))) {
            criticalDamage = checked(
                criticalDamage * playerClass.CriticalDamageMultiplier);
        }
        criticalDamage = AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
            criticalDamage,
            target.IsUndead,
            player.Equipment,
            target.IsAnimal,
            items);
        var resolvedDamage = nativeImpactRoll
            ? criticalDamage <= 0
                ? 0
                : Math.Max(rules.MinimumDamage, criticalDamage)
            : rules.ResolveAttackDamage(
                criticalDamage,
                // Fully initialized class profiles use native admission-only
                // monster armor. Legacy compatibility templates have no class
                // damage profile and retain the historical armor reduction.
                !usesNativeClassProfile && monsterTarget ? targetArmorClass : 0,
                effectiveHitChance,
                seed);
        if (target.IsDemon
            && (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag) != 0) {
            resolvedDamage = checked(resolvedDamage * rules.TripleDemonDamageMultiplier);
        }
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamage > 0
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.DevastationFlag)
            && random.RandomIntLessThan(100) < rules.HellfireDevastationChancePercent) {
            resolvedDamage = checked(
                resolvedDamage * rules.HellfireDevastationDamageMultiplier);
        }
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamage > 0
            && !target.IsDiablo
            && !target.IsUnique
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag)
            && random.RandomIntLessThan(100) < rules.HellfireDoppelgangerChancePercent) {
            doppelgangerProc = true;
        }
        var resolvedDamageQ6 = DeterministicFixedPointQ6.FromWhole(resolvedDamage);
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamageQ6 > 0
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.JestersFlag)) {
            var roll = random.RandomIntBetween(0, rules.HellfireJestersRollMaximum);
            if (roll >= rules.HellfireJestersExpansionThreshold) {
                roll = checked(
                    rules.HellfireJestersExpansionThreshold
                    + (roll - rules.HellfireJestersExpansionThreshold)
                        * rules.HellfireJestersExpansionMultiplier);
            }
            resolvedDamageQ6 = checked(resolvedDamageQ6 * roll / 100);
        }
        if (adjacentDamage)
            resolvedDamageQ6 >>= 2;
        if (perilActive)
            resolvedDamageQ6 = checked(resolvedDamageQ6 * rules.HellfirePerilDamageMultiplier);
        return resolvedDamageQ6;
    }

    private static int GetNativeArrowDistancePenalty(int missileDistance)
    {
        if (missileDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(missileDistance));
        return checked((int)Math.Min(
            int.MaxValue,
            (long)missileDistance * missileDistance / 2));
    }

    /** Spawns the native full-health, non-unique Doppelganger copy. */
    private void SpawnDoppelganger(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        (int X, int Y)? position = null;
        foreach (var displacement in DirectionDisplacements) {
            var candidateX = checked(target.PositionX + displacement.X);
            var candidateY = checked(target.PositionY + displacement.Y);
            if (world.IsWalkable(target.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    target.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    target.EntityId)) {
                position = (candidateX, candidateY);
            }
        }
        if (!position.HasValue)
            return;

        var entityId = AllocateMonsterEntityId(players);
        var copy = new AuthoritativeCombatTarget(
            entityId,
            position.Value.X,
            position.Value.Y,
            DeterministicFixedPointQ6.ToWholeCeiling(target.MaxHitPointsQ6),
            armorClass: target.ArmorClass,
            maxHitPoints: DeterministicFixedPointQ6.ToWholeCeiling(target.MaxHitPointsQ6),
            levelId: target.LevelId,
            monsterId: target.MonsterId,
            attackDamage: target.AttackDamage,
            aggroRange: target.AggroRange,
            fireResistance: target.FireResistance,
            lightningResistance: target.LightningResistance,
            magicResistance: target.MagicResistance,
            acidResistance: target.AcidResistance,
            monsterLevel: target.MonsterLevel,
            stoneCurseImmune: target.StoneCurseImmune,
            undead: target.IsUndead,
            minDamage: target.MinDamage,
            maxDamage: target.MaxDamage,
            minDamageSpecial: target.MinDamageSpecial,
            maxDamageSpecial: target.MaxDamageSpecial,
            berserkImmune: target.BerserkImmune,
            deathAnimationTicks: target.DeathAnimationTicks,
            experienceReward: target.ExperienceReward,
            toHit: target.ToHit,
            toHitSpecial: target.ToHitSpecial,
            nativeAi: target.NativeAi,
            intelligence: target.Intelligence,
            gargoyleSpecialActive: target.GargoyleSpecialActive,
            demon: target.IsDemon,
            animal: target.IsAnimal,
            knockback: target.HasKnockback);
        copy.RestoreHitPointsQ6(target.MaxHitPointsQ6, target.MaxHitPointsQ6);
        world.CombatTargets.Add(entityId, copy);
    }

    private uint AllocateMonsterEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return monsterEntityIds.Allocate(entityId =>
            world.CombatTargets.ContainsKey(entityId)
            || world.WorldItems.ContainsKey(entityId)
            || world.Objects.ContainsKey(entityId)
            || world.Projectiles.ContainsKey(entityId)
            || world.Portals.ContainsKey(entityId)
            || world.Corpses.ContainsKey(entityId)
            || players.Any(player => player.EntityId == entityId));
    }

    private bool TryGetAttackTarget(
        AuthoritativePlayerState player,
        uint targetEntityId,
        bool ranged,
        out AuthoritativeCombatTarget target,
        bool requireMissileLineOfSight = true)
    {
        if (!world.CombatTargets.TryGetValue(targetEntityId, out target!)
            || target.HitPoints <= 0
            || target.SneakHidden
            || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target)
            || IsNativeRetreatTarget(target)
            || (target.LevelId != 0 && target.LevelId != player.LevelId)) {
            return false;
        }
        var distance = DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY);
        if (!ranged)
            return distance <= 1;
        return distance > 0
            && (!requireMissileLineOfSight || world.HasMissileLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                target.PositionX,
                target.PositionY));
    }

    /**
     * Mirrors the native Monster::isPossibleToHit goal gates that are
     * represented by authoritative compatibility timers.  The timers are
     * server-owned state, so this admission applies equally to immediate,
     * delayed, and effect-time revalidated player attacks.
     */
    private bool IsNativeRetreatTarget(AuthoritativeCombatTarget target)
    {
        if (UsesNativeCounselorAi(target) && target.CounselorRetreatTicks != 0)
            return true;

        // Illusion Weaver uses the native Sneak controller; its retreat
        // countdown is the authoritative representation of MonsterGoal::Retreat.
        return UsesNativeAi(target, "Sneak") && target.SneakRetreatTicks != 0;
    }

    /**
     * Keeps content-owned unique monsters non-combatant until their authored
     * staged dialogue reaches its terminal native variable marker.
     */
    private bool IsQuestDialogueTarget(AuthoritativeCombatTarget target)
    {
        if (!target.IsUnique || target.UniqueIdentity.Length == 0)
            return false;

        foreach (var quest in world.Quests.Values) {
            if (!quest.Active
                || quest.Unavailable
                || quest.Completed
                || (quest.LevelId != 0 && quest.LevelId != target.LevelId)
                || !string.Equals(
                    quest.CompletionUniqueIdentity,
                    target.UniqueIdentity,
                    StringComparison.Ordinal)) {
                continue;
            }

            if (questDialogueStages?.HasPendingStage(
                    quest.QuestId,
                    quest.Variable1,
                    quest.Variable2) == true) {
                return true;
            }

            // Older packs retain the native terminal marker until their staged
            // dialogue route is migrated.
            if (quest.DialogueCompletionVariable1 >= 0
                && quest.Variable1 < quest.DialogueCompletionVariable1) {
                return true;
            }
        }

        return false;
    }

    /** Native LazarusMinionAi keeps summoned succubi non-combatant until the single-player quest gate opens. */
    private bool IsLazarusMinionCombatSuppressed(AuthoritativeCombatTarget target)
    {
        if (gameMode == DiabloGameMode.Multiplayer
            || !UsesNativeAi(target, "LazarusSuccubus"))
            return false;

        return world.Quests.Values.Any(quest =>
            quest.Active
            && !quest.Unavailable
            && !quest.Completed
            && quest.Variable1 <= 5
            && string.Equals(
                quest.CompletionUniqueIdentity,
                "Lazarus",
                StringComparison.Ordinal));
    }

    private bool TryGetPlayerAttackTarget(
        AuthoritativePlayerState player,
        uint targetEntityId,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players,
        out AuthoritativePlayerState target)
    {
        target = players.FirstOrDefault(candidate =>
            candidate.EntityId == targetEntityId
            && candidate.EntityId != player.EntityId)!;
        if (target is null
            || target.Life <= 0
            || (target.LevelId != 0 && target.LevelId != player.LevelId)
            || (!rules.FriendlyFire && player.FriendlyMode)
            || IsPlayerProtectedByFlash(target)
            || IsPlayerEtherealized(target)) {
            return false;
        }

        var distance = DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY);
        return ranged
            ? distance > 0 && world.HasMissileLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                target.PositionX,
                target.PositionY)
            : distance <= 1;
    }
}
