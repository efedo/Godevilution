using Devilution.Domain.Hashing;
using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Server.Snapshots;

/** Hashes the authoritative fields currently exposed by Snapshot. */
public static class SnapshotStateHasher
{
    public static string Compute(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasher = new CanonicalStateHasher();
        if (snapshot.VictoryTriggered) {
            hasher.AppendString("godevilution-victory-v1");
            hasher.AppendBool(true);
        }
        if (snapshot.Difficulty != GameDifficulty.Unspecified
            && snapshot.Difficulty != GameDifficulty.Normal) {
            hasher.AppendString("godevilution-game-difficulty-v1");
            hasher.AppendInt32((int)snapshot.Difficulty);
        }
        var activeWorldMutationIds = snapshot.ActiveWorldMutationIds
            .OrderBy(mutationId => mutationId)
            .ToArray();
        if (activeWorldMutationIds.Length > 0) {
            hasher.AppendString("godevilution-world-mutations-v1");
            hasher.AppendUInt64((ulong)activeWorldMutationIds.Length);
            foreach (var mutationId in activeWorldMutationIds)
                hasher.AppendUInt32(mutationId);
        }
        var materializedProceduralLevelIds = snapshot.MaterializedProceduralLevelIds
            .OrderBy(levelId => levelId)
            .ToArray();
        if (materializedProceduralLevelIds.Length > 0) {
            hasher.AppendString("godevilution-materialized-procedural-levels-v1");
            hasher.AppendUInt64((ulong)materializedProceduralLevelIds.Length);
            foreach (var levelId in materializedProceduralLevelIds)
                hasher.AppendUInt32(levelId);
        }
        var setLevelObjectOrigins = snapshot.SetLevelObjectOrigins
            .OrderBy(origin => origin.SetLevelId)
            .ToArray();
        if (setLevelObjectOrigins.Length > 0) {
            hasher.AppendString("godevilution-set-level-object-origins-v1");
            hasher.AppendUInt64((ulong)setLevelObjectOrigins.Length);
            foreach (var origin in setLevelObjectOrigins) {
                hasher.AppendInt32(origin.SetLevelId);
                hasher.AppendUInt32(origin.LevelId);
                hasher.AppendInt32(origin.PositionX);
                hasher.AppendInt32(origin.PositionY);
            }
        }
        var proceduralQuestPieceOrigins = snapshot.ProceduralQuestPieceOrigins
            .OrderBy(origin => origin.OriginId)
            .ToArray();
        if (proceduralQuestPieceOrigins.Length > 0) {
            hasher.AppendString("godevilution-procedural-quest-piece-origins-v1");
            hasher.AppendUInt64((ulong)proceduralQuestPieceOrigins.Length);
            foreach (var origin in proceduralQuestPieceOrigins) {
                hasher.AppendUInt32(origin.OriginId);
                hasher.AppendUInt32(origin.LevelId);
                hasher.AppendInt32(origin.PositionX);
                hasher.AppendInt32(origin.PositionY);
            }
        }
        var players = snapshot.Players.OrderBy(player => player.EntityId).ToArray();
        hasher.AppendUInt64((ulong)players.Length);
        foreach (var player in players) {
            hasher.AppendUInt32(player.EntityId);
            hasher.AppendInt32(player.PositionX);
            hasher.AppendInt32(player.PositionY);
            hasher.AppendInt32(player.Life);
            hasher.AppendInt32(player.Mana);
            hasher.AppendInt32(player.ManaMaximum);
            hasher.AppendInt32(player.LifeMaximum);
            if ((player.BaseManaMaximum != 0
                    && player.BaseManaMaximum != player.ManaMaximum)
                || (player.BaseLifeMaximum != 0
                    && player.BaseLifeMaximum != player.LifeMaximum)) {
                hasher.AppendString("godevilution-player-resource-base-v1");
                hasher.AppendInt32(player.BaseManaMaximum);
                hasher.AppendInt32(player.BaseLifeMaximum);
            }
            if (player.LightRadius != 0 && player.LightRadius != 10) {
                hasher.AppendString("godevilution-player-light-radius-v1");
                hasher.AppendInt32(player.LightRadius);
            }
            hasher.AppendUInt32(player.CharacterLevel);
            hasher.AppendUInt32(player.LevelId);
            var historicalVisitedLevelIds = player.VisitedLevelIds
                .Where(levelId => levelId != player.LevelId)
                .OrderBy(levelId => levelId)
                .ToArray();
            if (historicalVisitedLevelIds.Length > 0) {
                hasher.AppendString("godevilution-player-visited-levels-v1");
                hasher.AppendUInt64((ulong)historicalVisitedLevelIds.Length);
                foreach (var levelId in historicalVisitedLevelIds)
                    hasher.AppendUInt32(levelId);
            }
            if (player.CharacterClassId != 0) {
                hasher.AppendString("godevilution-player-class-v1");
                hasher.AppendUInt32(player.CharacterClassId);
            }
            if (!string.IsNullOrEmpty(player.CharacterName)) {
                hasher.AppendString("godevilution-player-name-v1");
                hasher.AppendString(player.CharacterName);
            }
            if (player.ManaShieldActive) {
                hasher.AppendString("godevilution-player-mana-shield-v1");
                hasher.AppendBool(true);
            }
            if (player.ReflectCharges != 0) {
                hasher.AppendString("godevilution-player-reflect-v1");
                hasher.AppendUInt32(player.ReflectCharges);
            }
            if (player.DiabloKillLevel != 0) {
                hasher.AppendString("godevilution-player-diablo-kill-level-v1");
                hasher.AppendUInt32(player.DiabloKillLevel);
            }
            var memorizedSpells = player.MemorizedSpells
                .OrderBy(spell => spell.SpellId)
                .ThenBy(spell => spell.Level)
                .ToArray();
            if (player.AbilitySpellId != 0 || memorizedSpells.Length != 0) {
                hasher.AppendString("godevilution-player-spells-v1");
                hasher.AppendUInt32(player.AbilitySpellId);
                if (player.AbilityKind != PlayerAbilityKind.Unspecified) {
                    hasher.AppendString("godevilution-player-ability-kind-v1");
                    hasher.AppendInt32((int)player.AbilityKind);
                }
                hasher.AppendUInt64((ulong)memorizedSpells.Length);
                foreach (var spell in memorizedSpells) {
                    hasher.AppendUInt32(spell.SpellId);
                    hasher.AppendUInt32(spell.Level);
                }
            }
            if (player.HostileMode) {
                hasher.AppendString("godevilution-player-hostile-mode-v1");
                hasher.AppendBool(true);
            }
            if (player.FacingDirection != PlayerDirection.South) {
                hasher.AppendString("godevilution-player-facing-v1");
                hasher.AppendInt32((int)player.FacingDirection);
            }
            if (player.CombatMode != PlayerCombatMode.Stand
                || player.CombatModeEndTick != 0) {
                hasher.AppendString("godevilution-player-combat-mode-v1");
                hasher.AppendInt32((int)player.CombatMode);
                hasher.AppendUInt64(player.CombatModeEndTick);
            }
            if (player.CurrentAction is not null) {
                var action = player.CurrentAction;
                hasher.AppendString("godevilution-player-action-v1");
                hasher.AppendInt32((int)action.Kind);
                hasher.AppendUInt64(action.StartedAtTick);
                hasher.AppendUInt64(action.EffectTick);
                hasher.AppendUInt64(action.ReadyTick);
                hasher.AppendUInt64(action.EndTick);
                hasher.AppendBool(action.EffectApplied);
                hasher.AppendInt32(action.DirectionX);
                hasher.AppendInt32(action.DirectionY);
                hasher.AppendUInt32(action.TargetEntityId);
                hasher.AppendBool(action.Cast is not null);
                if (action.Cast is not null) {
                    hasher.AppendUInt32(action.Cast.SpellId);
                    hasher.AppendUInt32(action.Cast.TargetEntityId);
                    hasher.AppendInt32(action.Cast.TargetX);
                    hasher.AppendInt32(action.Cast.TargetY);
                    hasher.AppendInt32((int)action.Cast.Source);
                    hasher.AppendBool(action.Cast.SourceItem is not null);
                    if (action.Cast.SourceItem is not null) {
                        hasher.AppendInt32((int)action.Cast.SourceItem.Location);
                        hasher.AppendUInt32(action.Cast.SourceItem.Slot);
                        if (action.Cast.SourceItem.Page != 0) {
                            hasher.AppendString("godevilution-player-item-reference-page-v1");
                            hasher.AppendUInt32(action.Cast.SourceItem.Page);
                        }
                    }
                }
            }
            var statusEffects = player.StatusEffects
                .OrderBy(effect => effect.EffectId)
                .ThenBy(effect => effect.RemainingTicks)
                .ThenBy(effect => effect.Magnitude)
                .ToArray();
            hasher.AppendUInt64((ulong)statusEffects.Length);
            foreach (var effect in statusEffects) {
                hasher.AppendUInt32(effect.EffectId);
                hasher.AppendUInt32(effect.RemainingTicks);
                hasher.AppendInt32(effect.Magnitude);
                if (effect.Behavior != StatusEffectBehavior.Unspecified) {
                    hasher.AppendString("godevilution-status-effect-behavior-v1");
                    hasher.AppendInt32((int)effect.Behavior);
                }
            }
            hasher.AppendUInt32(player.Gold);
            hasher.AppendUInt32(player.Experience);
            if (player.StatPoints != 0) {
                hasher.AppendString("godevilution-stat-points-v1");
                hasher.AppendUInt32(player.StatPoints);
            }
            hasher.AppendUInt32(player.ActiveStoreId);

            var attributes = player.Attributes ?? new PlayerAttributesSnapshot();
            AppendAttribute(hasher, attributes.Strength);
            AppendAttribute(hasher, attributes.Magic);
            AppendAttribute(hasher, attributes.Dexterity);
            AppendAttribute(hasher, attributes.Vitality);

            var equipment = player.Equipment
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.ItemSeed)
                .ToArray();
            hasher.AppendUInt64((ulong)equipment.Length);
            foreach (var item in equipment) {
                hasher.AppendUInt32(item.Slot);
                hasher.AppendUInt32(item.ItemSeed);
                AppendItemState(hasher, item.State);
            }

            var belt = player.Belt
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.ItemSeed)
                .ToArray();
            hasher.AppendUInt64((ulong)belt.Length);
            foreach (var item in belt) {
                hasher.AppendUInt32(item.Slot);
                hasher.AppendUInt32(item.ItemSeed);
                AppendItemState(hasher, item.State);
            }

            var inventory = player.Inventory
                .OrderBy(item => item.StoreId)
                .ThenBy(item => item.StoreSlot)
                .ThenBy(item => item.ItemSeed)
                .ThenBy(item => item.Price)
                .ThenBy(item => item.PurchasedAtTick)
                .ToArray();
            hasher.AppendUInt64((ulong)inventory.Length);
            foreach (var item in inventory) {
                hasher.AppendUInt32(item.StoreId);
                hasher.AppendUInt32(item.StoreSlot);
                hasher.AppendUInt32(item.ItemSeed);
                hasher.AppendUInt32(item.Price);
                hasher.AppendUInt64(item.PurchasedAtTick);
                AppendItemState(hasher, item.State);
            }

            var stash = player.Stash
                .OrderBy(item => item.Page)
                .ThenBy(item => item.Cell)
                .ThenBy(item => item.Item?.StoreId ?? 0)
                .ThenBy(item => item.Item?.StoreSlot ?? 0)
                .ThenBy(item => item.Item?.ItemSeed ?? 0)
                .ThenBy(item => item.Item?.Price ?? 0)
                .ThenBy(item => item.Item?.PurchasedAtTick ?? 0)
                .ToArray();
            if (stash.Length > 0 || player.StashGold != 0) {
                hasher.AppendString("godevilution-player-stash-v1");
                hasher.AppendUInt32(player.StashGold);
                hasher.AppendUInt64((ulong)stash.Length);
                foreach (var item in stash) {
                    hasher.AppendUInt32(item.Page);
                    hasher.AppendUInt32(item.Cell);
                    hasher.AppendUInt32(item.Item?.StoreId ?? 0);
                    hasher.AppendUInt32(item.Item?.StoreSlot ?? 0);
                    hasher.AppendUInt32(item.Item?.ItemSeed ?? 0);
                    hasher.AppendUInt32(item.Item?.Price ?? 0);
                    hasher.AppendUInt64(item.Item?.PurchasedAtTick ?? 0);
                    AppendItemState(hasher, item.Item?.State);
                }
            }
            if (player.StashOpen) {
                hasher.AppendString("godevilution-player-stash-session-v1");
                hasher.AppendBool(true);
            }

            hasher.AppendUInt64((ulong)player.InventoryGrid.Count);
            foreach (var cell in player.InventoryGrid)
                hasher.AppendInt32(cell);
            if (player.HeldItem is not null) {
                hasher.AppendString("godevilution-player-held-item-v1");
                hasher.AppendUInt32(player.HeldItem.StoreId);
                hasher.AppendUInt32(player.HeldItem.StoreSlot);
                hasher.AppendUInt32(player.HeldItem.ItemSeed);
                hasher.AppendUInt32(player.HeldItem.Price);
                hasher.AppendUInt64(player.HeldItem.PurchasedAtTick);
                AppendItemState(hasher, player.HeldItem.State);
            }
        }

        // Preserve hashes for legacy snapshots with no world entities while
        // making the first authoritative monster projection deterministic.
        var monsters = snapshot.Monsters.OrderBy(monster => monster.EntityId).ToArray();
        if (monsters.Length > 0) {
            hasher.AppendUInt64((ulong)monsters.Length);
            foreach (var monster in monsters) {
                hasher.AppendUInt32(monster.EntityId);
                hasher.AppendUInt32(monster.MonsterId);
                hasher.AppendUInt32(monster.LevelId);
                hasher.AppendInt32(monster.PositionX);
                hasher.AppendInt32(monster.PositionY);
                hasher.AppendInt32(monster.HitPoints);
                hasher.AppendInt32(monster.MaxHitPoints);
                hasher.AppendInt32(monster.ArmorClass);
                hasher.AppendBool(monster.Alive);
                if (monster.AttackDamage != 0 || monster.AggroRange != 0 || monster.FireResistance != 0
                    || monster.LightningResistance != 0 || monster.MagicResistance != 0
                    || monster.AcidResistance != 0) {
                    hasher.AppendInt32(monster.AttackDamage);
                    hasher.AppendInt32(monster.AggroRange);
                    hasher.AppendInt32(monster.FireResistance);
                    hasher.AppendInt32(monster.LightningResistance);
                    hasher.AppendInt32(monster.MagicResistance);
                    if (monster.AcidResistance != 0) {
                        hasher.AppendString("godevilution-monster-acid-resistance-v1");
                        hasher.AppendInt32(monster.AcidResistance);
                    }
                }
                if (monster.MonsterLevel != 0) {
                    hasher.AppendString("godevilution-monster-level-v1");
                    hasher.AppendInt32(monster.MonsterLevel);
                }
                var hitPointsQ6 = ResolveQ6(monster.HitPoints, monster.HitPointsQ6);
                var maxHitPointsQ6 = ResolveQ6(monster.MaxHitPoints, monster.MaxHitPointsQ6);
                if (hitPointsQ6 != (long)monster.HitPoints * DiabloFixedPointScale
                    || maxHitPointsQ6 != (long)monster.MaxHitPoints * DiabloFixedPointScale) {
                    hasher.AppendString("godevilution-monster-hit-points-q6-v1");
                    hasher.AppendInt32(hitPointsQ6);
                    hasher.AppendInt32(maxHitPointsQ6);
                }
                if (monster.Moving || monster.Petrified) {
                    hasher.AppendString("godevilution-monster-occupancy-v1");
                    hasher.AppendBool(monster.Moving);
                    hasher.AppendInt32(monster.FuturePositionX);
                    hasher.AppendInt32(monster.FuturePositionY);
                    hasher.AppendBool(monster.Petrified);
                }
                if (monster.Direction != 0) {
                    hasher.AppendString("godevilution-monster-direction-v1");
                    hasher.AppendInt32(monster.Direction);
                }
                if (monster.DialogueMessageId != 0) {
                    hasher.AppendString("godevilution-monster-dialogue-message-v1");
                    hasher.AppendInt32(monster.DialogueMessageId);
                }
                if (monster.DialogueState != MonsterDialogueState.Unspecified) {
                    hasher.AppendString("godevilution-monster-dialogue-state-v1");
                    hasher.AppendInt32((int)monster.DialogueState);
                }
                if (monster.MonsterItemSeed != 0 || monster.MonsterAiSeed != 0) {
                    hasher.AppendString("godevilution-monster-runtime-seeds-v1");
                    hasher.AppendUInt32(monster.MonsterItemSeed);
                    hasher.AppendUInt32(monster.MonsterAiSeed);
                }
                if (monster.LeaderEntityId != 0
                    || monster.LeaderRelation != 0
                    || monster.PackSize != 0
                    || monster.ActiveForTicks != 0) {
                    hasher.AppendString("godevilution-monster-group-unity-v1");
                    hasher.AppendUInt32(monster.LeaderEntityId);
                    hasher.AppendInt32(monster.LeaderRelation);
                    hasher.AppendInt32(monster.PackSize);
                    hasher.AppendInt32(monster.ActiveForTicks);
                }
                if (monster.StoneCurseImmune) {
                    hasher.AppendString("godevilution-monster-stone-curse-immunity-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsUndead) {
                    hasher.AppendString("godevilution-monster-undead-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsDemon) {
                    hasher.AppendString("godevilution-monster-demon-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsAnimal) {
                    hasher.AppendString("godevilution-monster-animal-v1");
                    hasher.AppendBool(true);
                }
                if (monster.HasKnockback) {
                    hasher.AppendString("godevilution-monster-knockback-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsPlayerMinion) {
                    hasher.AppendString("godevilution-monster-player-minion-v1");
                    hasher.AppendBool(true);
                    hasher.AppendString("godevilution-monster-owner-v1");
                    hasher.AppendUInt32(monster.OwnerPlayerId);
                }
                if (monster.IsUnique || monster.IsDiablo || monster.BerserkImmune || monster.IsBerserked
                    || monster.MinDamage != monster.AttackDamage
                    || monster.MaxDamage != monster.AttackDamage
                    || monster.MinDamageSpecial != monster.AttackDamage
                    || monster.MaxDamageSpecial != monster.AttackDamage) {
                    hasher.AppendString("godevilution-monster-berserk-v1");
                    hasher.AppendBool(monster.IsUnique);
                    if (!string.IsNullOrEmpty(monster.UniqueIdentity)) {
                        hasher.AppendString("godevilution-monster-unique-identity-v1");
                        hasher.AppendString(monster.UniqueIdentity);
                    }
                    hasher.AppendBool(monster.IsDiablo);
                    hasher.AppendBool(monster.BerserkImmune);
                    hasher.AppendInt32(monster.MinDamage);
                    hasher.AppendInt32(monster.MaxDamage);
                    hasher.AppendInt32(monster.MinDamageSpecial);
                    hasher.AppendInt32(monster.MaxDamageSpecial);
                    hasher.AppendBool(monster.IsBerserked);
                }
                if (monster.DeathAnimationTicks != 0 || monster.DeathTicksRemaining != 0) {
                    hasher.AppendString("godevilution-monster-death-animation-v1");
                    hasher.AppendInt32(monster.DeathAnimationTicks);
                    hasher.AppendInt32(monster.DeathTicksRemaining);
                }
                if (monster.ExperienceReward != 0) {
                    hasher.AppendString("godevilution-monster-experience-v1");
                    hasher.AppendUInt32(monster.ExperienceReward);
                }
                if (monster.ToHit != 0 || monster.ToHitSpecial != 0) {
                    hasher.AppendString("godevilution-monster-accuracy-v1");
                    hasher.AppendInt32(monster.ToHit);
                    hasher.AppendInt32(monster.ToHitSpecial);
                }
                if (!string.IsNullOrEmpty(monster.NativeAi)) {
                    hasher.AppendString("godevilution-monster-native-ai-v1");
                    hasher.AppendString(monster.NativeAi);
                }
                if (monster.Intelligence != 0) {
                    hasher.AppendString("godevilution-monster-intelligence-v1");
                    hasher.AppendInt32(monster.Intelligence);
                }
                if (monster.GargoyleSpecialActive) {
                    hasher.AppendString("godevilution-monster-gargoyle-special-v1");
                    hasher.AppendBool(true);
                }
                if (monster.FallenFearTicks != 0 || monster.FallenFearDirection != 0) {
                    hasher.AppendString("godevilution-monster-fallen-fear-v1");
                    hasher.AppendInt32(monster.FallenFearTicks);
                    hasher.AppendInt32(monster.FallenFearDirection);
                }
                if (monster.FallenStandAnimationFrame != 0
                    || monster.FallenStandAnimationTick != 0
                    || monster.FallenRallyTicks != 0) {
                    hasher.AppendString("godevilution-monster-fallen-rally-v1");
                    hasher.AppendInt32(monster.FallenStandAnimationFrame);
                    hasher.AppendInt32(monster.FallenStandAnimationTick);
                    hasher.AppendInt32(monster.FallenRallyTicks);
                }
                if (monster.SneakHidden || monster.SneakRetreatTicks != 0) {
                    hasher.AppendString("godevilution-monster-sneak-v1");
                    hasher.AppendBool(monster.SneakHidden);
                    hasher.AppendInt32(monster.SneakRetreatTicks);
                }
                if (monster.ScavengerHealingTicks != 0 || monster.ScavengerCorpseEntityId != 0) {
                    hasher.AppendString("godevilution-monster-scavenger-v1");
                    hasher.AppendInt32(monster.ScavengerHealingTicks);
                    hasher.AppendUInt32(monster.ScavengerCorpseEntityId);
                }
                if (monster.BatRetreatTicks != 0 || monster.BatRetreatDirection != 0) {
                    hasher.AppendString("godevilution-monster-bat-retreat-v1");
                    hasher.AppendInt32(monster.BatRetreatTicks);
                    hasher.AppendInt32(monster.BatRetreatDirection);
                }
                if (monster.BatMovementIdleTicks != -1 || monster.BatMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-bat-movement-v1");
                    hasher.AppendInt32(monster.BatMovementIdleTicks);
                    hasher.AppendInt32(monster.BatMovementMode);
                }
                if (monster.RangedAvoidanceMoveTicks != -1
                    || monster.RangedAvoidanceTurn != 0) {
                    hasher.AppendString("godevilution-monster-ranged-avoidance-v1");
                    hasher.AppendInt32(monster.RangedAvoidanceMoveTicks);
                    hasher.AppendInt32(monster.RangedAvoidanceTurn);
                }
                if (monster.RangedAvoidanceMovementIdleTicks != -1
                    || monster.RangedAvoidanceMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-ranged-avoidance-counter-v1");
                    hasher.AppendInt32(monster.RangedAvoidanceMovementIdleTicks);
                    hasher.AppendInt32(monster.RangedAvoidanceMovementMode);
                }
                if (monster.RhinoMovementTicks != -1
                    || monster.RhinoMovementTurn != 0) {
                    hasher.AppendString("godevilution-monster-rhino-movement-v1");
                    hasher.AppendInt32(monster.RhinoMovementTicks);
                    hasher.AppendInt32(monster.RhinoMovementTurn);
                }
                if (monster.AvoidanceMovementTicks != -1
                    || monster.AvoidanceMovementTurn != 0) {
                    hasher.AppendString("godevilution-monster-avoidance-movement-v1");
                    hasher.AppendInt32(monster.AvoidanceMovementTicks);
                    hasher.AppendInt32(monster.AvoidanceMovementTurn);
                }
                if (monster.AvoidanceMovementIdleTicks != -1
                    || monster.AvoidanceMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-avoidance-counter-v1");
                    hasher.AppendInt32(monster.AvoidanceMovementIdleTicks);
                    hasher.AppendInt32(monster.AvoidanceMovementMode);
                }
                if (monster.SkeletonMeleeDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-skeleton-delay-v1");
                    hasher.AppendInt32(monster.SkeletonMeleeDelayTicks);
                }
                if (monster.SkeletonRangedMovementIdleTicks != -1
                    || monster.SkeletonRangedMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-skeleton-ranged-counter-v1");
                    hasher.AppendInt32(monster.SkeletonRangedMovementIdleTicks);
                    hasher.AppendInt32(monster.SkeletonRangedMovementMode);
                }
                if (monster.FatMovementIdleTicks != -1
                    || monster.FatMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-fat-counter-v1");
                    hasher.AppendInt32(monster.FatMovementIdleTicks);
                    hasher.AppendInt32(monster.FatMovementMode);
                }
                if (monster.RhinoMovementIdleTicks != -1
                    || monster.RhinoMovementMode != 0) {
                    hasher.AppendString("godevilution-monster-rhino-counter-v1");
                    hasher.AppendInt32(monster.RhinoMovementIdleTicks);
                    hasher.AppendInt32(monster.RhinoMovementMode);
                }
                if (monster.RhinoDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-rhino-delay-v1");
                    hasher.AppendInt32(monster.RhinoDelayTicks);
                }
                if (monster.SkeletonKingDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-skeleton-king-delay-v1");
                    hasher.AppendInt32(monster.SkeletonKingDelayTicks);
                }
                if (monster.HorkDemonDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-hork-demon-delay-v1");
                    hasher.AppendInt32(monster.HorkDemonDelayTicks);
                }
                if (monster.RangedAvoidanceDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-ranged-avoidance-delay-v1");
                    hasher.AppendInt32(monster.RangedAvoidanceDelayTicks);
                }
                if (monster.SnakeDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-snake-delay-v1");
                    hasher.AppendInt32(monster.SnakeDelayTicks);
                }
                if (monster.CounselorDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-counselor-delay-v1");
                    hasher.AppendInt32(monster.CounselorDelayTicks);
                }
                if (monster.AvoidanceDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-avoidance-delay-v1");
                    hasher.AppendInt32(monster.AvoidanceDelayTicks);
                }
                if (monster.RangedDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-ranged-delay-v1");
                    hasher.AppendInt32(monster.RangedDelayTicks);
                }
                if (monster.MegaDelayTicks != 0) {
                    hasher.AppendString("godevilution-monster-mega-delay-v1");
                    hasher.AppendInt32(monster.MegaDelayTicks);
                }
                if (monster.CounselorRetreatTicks != 0) {
                    hasher.AppendString("godevilution-monster-counselor-retreat-v1");
                    hasher.AppendInt32(monster.CounselorRetreatTicks);
                }
            }
        }

        var corpses = snapshot.Corpses.OrderBy(corpse => corpse.EntityId).ToArray();
        if (corpses.Length > 0) {
            hasher.AppendString("godevilution-corpses-v1");
            hasher.AppendUInt64((ulong)corpses.Length);
            foreach (var corpse in corpses) {
                hasher.AppendUInt32(corpse.EntityId);
                hasher.AppendUInt32(corpse.LevelId);
                hasher.AppendInt32(corpse.PositionX);
                hasher.AppendInt32(corpse.PositionY);
            }
        }

        var worldItems = snapshot.WorldItems.OrderBy(item => item.EntityId).ToArray();
        if (worldItems.Length > 0) {
            hasher.AppendUInt64((ulong)worldItems.Length);
            foreach (var item in worldItems) {
                hasher.AppendUInt32(item.EntityId);
                hasher.AppendUInt32(item.LevelId);
                hasher.AppendInt32(item.PositionX);
                hasher.AppendInt32(item.PositionY);
                hasher.AppendUInt32(item.ItemSeed);
                hasher.AppendUInt32(item.Price);
                AppendItemState(hasher, item.State);
            }
        }

        var objects = snapshot.Objects.OrderBy(@object => @object.EntityId).ToArray();
        if (objects.Length > 0) {
            hasher.AppendUInt64((ulong)objects.Length);
            foreach (var @object in objects) {
                hasher.AppendUInt32(@object.EntityId);
                hasher.AppendUInt32(@object.ObjectId);
                hasher.AppendUInt32(@object.LevelId);
                hasher.AppendInt32(@object.PositionX);
                hasher.AppendInt32(@object.PositionY);
                hasher.AppendBool(@object.Activated);
                if (@object.EffectKind != 0 || @object.EffectAmount != 0) {
                    hasher.AppendInt32(@object.EffectKind);
                    hasher.AppendInt32(@object.EffectAmount);
                }
                if (@object.MissilePassable || @object.Breakable || @object.Destroyed) {
                    hasher.AppendString("godevilution-object-collision-v1");
                    hasher.AppendBool(@object.MissilePassable);
                    hasher.AppendBool(@object.Breakable);
                    hasher.AppendBool(@object.Destroyed);
                }
                if (@object.TrapArmed) {
                    hasher.AppendString("godevilution-object-trap-v1");
                    hasher.AppendBool(true);
                }
                if (@object.KeepVisibleAfterActivation
                    || @object.AnimationFrame != 0
                    || @object.ActivatedAnimationFrame != 0) {
                    hasher.AppendString("godevilution-object-presentation-v1");
                    hasher.AppendBool(!@object.KeepVisibleAfterActivation);
                    hasher.AppendInt32(checked((int)@object.AnimationFrame));
                    hasher.AppendInt32(checked((int)@object.ActivatedAnimationFrame));
                }
                if (@object.PresentationKind != ObjectPresentationKind.Unspecified) {
                    hasher.AppendString("godevilution-object-presentation-family-v1");
                    hasher.AppendInt32((int)@object.PresentationKind);
                }
                if (@object.DialogueMessageId != 0) {
                    hasher.AppendString("godevilution-object-dialogue-v1");
                    hasher.AppendInt32(@object.DialogueMessageId);
                }
                if (@object.QuestId != 0) {
                    hasher.AppendString("godevilution-object-quest-v1");
                    hasher.AppendUInt32(@object.QuestId);
                }
                if (@object.QuestProgressAmount != 1) {
                    hasher.AppendString("godevilution-object-quest-progress-v1");
                    hasher.AppendUInt32(@object.QuestProgressAmount);
                }
                if (@object.IsShrine) {
                    hasher.AppendString("godevilution-object-shrine-v1");
                    hasher.AppendBool(true);
                }
                if (@object.QuestTransitionKind != 0
                    || @object.QuestTransitionAmount != 0
                    || @object.QuestTransitionRequiredVariable1 >= 0
                    || @object.QuestTransitionRequiredVariable2 >= 0) {
                    hasher.AppendString("godevilution-object-quest-transition-v1");
                    hasher.AppendInt32((int)@object.QuestTransitionKind);
                    hasher.AppendInt32(@object.QuestTransitionAmount);
                    hasher.AppendInt32(@object.QuestTransitionRequiredVariable1);
                    hasher.AppendInt32(@object.QuestTransitionRequiredVariable2);
                }
                if (@object.RewardKind != 0
                    || @object.RewardId != 0
                    || @object.RewardItemLevel != 0
                    || @object.RewardOffsetX != 0
                    || @object.RewardOffsetY != 0
                    || @object.RewardOriginKind != 0
                    || @object.RewardSetLevelId != 0) {
                    hasher.AppendString("godevilution-object-reward-v1");
                    hasher.AppendInt32((int)@object.RewardKind);
                    hasher.AppendUInt32(@object.RewardId);
                    hasher.AppendInt32(@object.RewardItemLevel);
                    hasher.AppendInt32(@object.RewardOffsetX);
                    hasher.AppendInt32(@object.RewardOffsetY);
                    hasher.AppendInt32(@object.RewardOriginKind);
                    hasher.AppendInt32(@object.RewardSetLevelId);
                }
                if (@object.ProjectileTargetOffsetX != 0
                    || @object.ProjectileTargetOffsetY != 0) {
                    hasher.AppendString("godevilution-object-projectile-target-v1");
                    hasher.AppendInt32(@object.ProjectileTargetOffsetX);
                    hasher.AppendInt32(@object.ProjectileTargetOffsetY);
                }
                if (@object.ProjectileSourceKind != 0) {
                    hasher.AppendString("godevilution-object-projectile-source-v1");
                    hasher.AppendInt32((int)@object.ProjectileSourceKind);
                }
                if (@object.QuestSequenceFollowUpKind != 0
                    || @object.QuestSequenceFollowUpAmount != 0
                    || @object.QuestSequenceFollowUpOffsetX != 0
                    || @object.QuestSequenceFollowUpOffsetY != 0) {
                    hasher.AppendString("godevilution-object-quest-sequence-follow-up-v1");
                    hasher.AppendInt32(@object.QuestSequenceFollowUpKind);
                    hasher.AppendUInt32(@object.QuestSequenceFollowUpAmount);
                    hasher.AppendInt32(@object.QuestSequenceFollowUpOffsetX);
                    hasher.AppendInt32(@object.QuestSequenceFollowUpOffsetY);
                }
            }
        }

        var quests = snapshot.Quests.OrderBy(quest => quest.QuestId).ToArray();
        if (quests.Length > 0) {
            hasher.AppendUInt64((ulong)quests.Length);
            foreach (var quest in quests) {
                hasher.AppendUInt32(quest.QuestId);
                hasher.AppendUInt32(quest.LevelId);
                hasher.AppendUInt32(quest.Progress);
                hasher.AppendUInt32(quest.RequiredProgress);
                hasher.AppendBool(quest.Completed);
                if (quest.Unavailable) {
                    hasher.AppendString("godevilution-quest-unavailable-v1");
                    hasher.AppendBool(true);
                }
                if (quest.Initial && !quest.Completed && !quest.Unavailable) {
                    hasher.AppendString("godevilution-quest-initial-v1");
                    hasher.AppendBool(true);
                }
                if (quest.Logged) {
                    hasher.AppendString("godevilution-quest-logged-v1");
                    hasher.AppendBool(true);
                }
                if (quest.Variable1 != 0 || quest.Variable2 != 0 || quest.MessageId != 0
                    || quest.CompletionMonsterId != 0
                    || !string.IsNullOrEmpty(quest.CompletionUniqueIdentity)) {
                    hasher.AppendString("godevilution-quest-native-state-v1");
                    hasher.AppendInt32(quest.Variable1);
                    hasher.AppendInt32(quest.Variable2);
                    hasher.AppendInt32(quest.MessageId);
                    hasher.AppendUInt32(quest.CompletionMonsterId);
                    if (!string.IsNullOrEmpty(quest.CompletionUniqueIdentity)) {
                        hasher.AppendString("godevilution-quest-unique-identity-v1");
                        hasher.AppendString(quest.CompletionUniqueIdentity);
                    }
                }
                if (quest.HasTurnInTownerId) {
                    hasher.AppendString("godevilution-quest-turn-in-towner-v1");
                    hasher.AppendInt32(quest.TurnInTownerId);
                }
                if (quest.TurnInActivationOnly) {
                    hasher.AppendString("godevilution-quest-turn-in-activation-v1");
                }
                if (quest.TurnInAlternateItemId != 0 || quest.TurnInAlternateMessageId != 0) {
                    hasher.AppendString("godevilution-quest-turn-in-alternate-v1");
                    hasher.AppendUInt32(quest.TurnInAlternateItemId);
                    hasher.AppendInt32(quest.TurnInAlternateMessageId);
                }
                if (quest.TurnInOnCompletion) {
                    hasher.AppendString("godevilution-quest-turn-in-completion-v1");
                    hasher.AppendBool(true);
                    if (quest.HasTurnInCompletionVariable2)
                        hasher.AppendInt32(quest.TurnInCompletionVariable2);
                    if (quest.HasTurnInCompletionRequiredVariable1)
                        hasher.AppendInt32(quest.TurnInCompletionRequiredVariable1);
                    if (quest.HasTurnInCompletionVariable1)
                        hasher.AppendInt32(quest.TurnInCompletionVariable1);
                }
                if (quest.TurnInProgressOnly) {
                    hasher.AppendString("godevilution-quest-turn-in-progress-v1");
                    hasher.AppendBool(true);
                    if (quest.HasTurnInRequiredVariable1)
                        hasher.AppendInt32(quest.TurnInRequiredVariable1);
                    if (quest.HasTurnInVariable1)
                        hasher.AppendInt32(quest.TurnInVariable1);
                }
                if (quest.TurnInDialogueOnly) {
                    hasher.AppendString("godevilution-quest-turn-in-dialogue-v1");
                    hasher.AppendBool(true);
                    if (quest.TurnInMessageId != 0)
                        hasher.AppendInt32(quest.TurnInMessageId);
                }
            }
        }

        var projectiles = snapshot.Projectiles.OrderBy(projectile => projectile.EntityId).ToArray();
        if (projectiles.Length > 0) {
            hasher.AppendUInt64((ulong)projectiles.Length);
            foreach (var projectile in projectiles) {
                hasher.AppendUInt32(projectile.EntityId);
                hasher.AppendUInt32(projectile.SourceEntityId);
                hasher.AppendUInt32(projectile.TargetEntityId);
                hasher.AppendUInt32(projectile.SpellId);
                hasher.AppendUInt32(projectile.LevelId);
                hasher.AppendInt32(projectile.PositionX);
                hasher.AppendInt32(projectile.PositionY);
                hasher.AppendInt32(projectile.TargetX);
                hasher.AppendInt32(projectile.TargetY);
                hasher.AppendInt32(projectile.Damage);
                hasher.AppendInt32(projectile.DamageType);
                hasher.AppendInt32(projectile.AreaRadius);
                hasher.AppendUInt32(projectile.RemainingTicks);
                if ((AuthoritativeProjectileBehavior)projectile.Behavior
                    != AuthoritativeProjectileBehavior.Targeted
                    || projectile.Phase != 0
                    || projectile.StartX != 0
                    || projectile.StartY != 0
                    || projectile.TraveledX != 0
                    || projectile.TraveledY != 0
                    || projectile.VelocityX != 0
                    || projectile.VelocityY != 0
                    || projectile.OriginalDirection != 0
                    || projectile.PathIndex != 0
                    || projectile.PathTicks != 0
                    || projectile.Speed != 0
                    || projectile.ImpactLifetime > 1
                    || projectile.ResistantDamageDivisor > 1
                    || projectile.WallLeftX != 0
                    || projectile.WallLeftY != 0
                    || projectile.WallRightX != 0
                    || projectile.WallRightY != 0
                    || projectile.WallLeftDirection != -1
                    || projectile.WallRightDirection != -1
                    || projectile.WallLeftStopped
                    || projectile.WallRightStopped
                    || projectile.WallHitDirection != -1
                    || projectile.ApocalypseScanX != 0
                    || projectile.ApocalypseScanY != 0
                    || projectile.ApocalypseHit
                    || projectile.ElementalOriginX != 0
                    || projectile.ElementalOriginY != 0
                    || projectile.GuardianState != 0) {
                    hasher.AppendString("godevilution-projectile-runtime-v1");
                    hasher.AppendInt32(projectile.Behavior);
                    hasher.AppendInt32(projectile.Phase);
                    hasher.AppendInt32(projectile.StartX);
                    hasher.AppendInt32(projectile.StartY);
                    hasher.AppendInt32(projectile.TraveledX);
                    hasher.AppendInt32(projectile.TraveledY);
                    hasher.AppendInt32(projectile.VelocityX);
                    hasher.AppendInt32(projectile.VelocityY);
                    hasher.AppendInt32(projectile.OriginalDirection);
                    hasher.AppendInt32(projectile.PathIndex);
                    hasher.AppendInt32(projectile.PathTicks);
                    hasher.AppendInt32(projectile.Speed);
                    hasher.AppendInt32(projectile.ImpactLifetime);
                    hasher.AppendInt32(projectile.ResistantDamageDivisor);
                    hasher.AppendInt32(projectile.WallLeftX);
                    hasher.AppendInt32(projectile.WallLeftY);
                    hasher.AppendInt32(projectile.WallRightX);
                    hasher.AppendInt32(projectile.WallRightY);
                    hasher.AppendInt32(projectile.WallLeftDirection);
                    hasher.AppendInt32(projectile.WallRightDirection);
                    hasher.AppendBool(projectile.WallLeftStopped);
                    hasher.AppendBool(projectile.WallRightStopped);
                    hasher.AppendInt32(projectile.WallHitDirection);
                    hasher.AppendInt32(projectile.ApocalypseScanX);
                    hasher.AppendInt32(projectile.ApocalypseScanY);
                    hasher.AppendBool(projectile.ApocalypseHit);
                    hasher.AppendInt32(projectile.ElementalOriginX);
                    hasher.AppendInt32(projectile.ElementalOriginY);
                    hasher.AppendInt32(projectile.GuardianState);
                }
                if ((AuthoritativeProjectileBehavior)projectile.Behavior
                    == AuthoritativeProjectileBehavior.WeaponArrow
                    || projectile.DamageQ6 != 0
                    || projectile.ElementalFireDamage != 0
                    || projectile.ElementalLightningDamage != 0
                    || projectile.AttackHit
                    || projectile.PerilSelfDamageQ6 != 0
                    || projectile.DoppelgangerProc
                    || projectile.ActivationDelayTicks != 0) {
                    hasher.AppendString("godevilution-projectile-weapon-arrow-v1");
                    hasher.AppendInt32(projectile.DamageQ6);
                    hasher.AppendInt32(projectile.ElementalFireDamage);
                    hasher.AppendInt32(projectile.ElementalLightningDamage);
                    hasher.AppendBool(projectile.AttackHit);
                    hasher.AppendInt32(projectile.PerilSelfDamageQ6);
                    hasher.AppendBool(projectile.DoppelgangerProc);
                    hasher.AppendUInt32(projectile.ActivationDelayTicks);
                }
                if ((AuthoritativeProjectileBehavior)projectile.Behavior
                    == AuthoritativeProjectileBehavior.MonsterArrow) {
                    hasher.AppendString("godevilution-projectile-monster-arrow-v1");
                    hasher.AppendInt32(projectile.Damage);
                    hasher.AppendInt32(projectile.DamageMinimum);
                    hasher.AppendInt32(projectile.DamageMaximum);
                    hasher.AppendUInt32(projectile.SourceEntityId);
                    hasher.AppendUInt32(projectile.TargetEntityId);
                }
                if (projectile.LastCollisionTargetEntityId != 0) {
                    hasher.AppendString("godevilution-projectile-collision-v1");
                    hasher.AppendUInt32(projectile.LastCollisionTargetEntityId);
                }
                if (projectile.EffectiveSpellLevel != 0) {
                    hasher.AppendString("godevilution-projectile-spell-level-v1");
                    hasher.AppendInt32(projectile.EffectiveSpellLevel);
                }
            }
        }

        // Static catalog routes are content, not mutable simulation state. Keep
        // them in the protocol snapshot without changing the legacy replay
        // hash; dynamic portals carry ownership/lifetime metadata and must be
        // included so their authoritative state remains hash-visible.
        var portals = snapshot.Portals
            .Where(portal => portal.RemainingTicks != 0
                || portal.OwnerEntityId != 0
                || portal.Bidirectional)
            .OrderBy(portal => portal.PortalId)
            .ToArray();
        if (portals.Length > 0) {
            hasher.AppendString("godevilution-portals-v1");
            hasher.AppendUInt64((ulong)portals.Length);
            foreach (var portal in portals) {
                hasher.AppendUInt32(portal.PortalId);
                hasher.AppendUInt32(portal.SourceLevelId);
                hasher.AppendInt32(portal.SourcePositionX);
                hasher.AppendInt32(portal.SourcePositionY);
                hasher.AppendUInt32(portal.DestinationLevelId);
                hasher.AppendInt32(portal.DestinationPositionX);
                hasher.AppendInt32(portal.DestinationPositionY);
                hasher.AppendUInt32(portal.RemainingTicks);
                hasher.AppendUInt32(portal.OwnerEntityId);
                hasher.AppendBool(portal.Bidirectional);
            }
        }

        hasher.AppendBool(snapshot.ActiveStore is not null);
        if (snapshot.ActiveStore is not null) {
            hasher.AppendUInt32(snapshot.ActiveStore.StoreId);
            var items = snapshot.ActiveStore.Items
                .OrderBy(item => item.StoreSlot)
                .ThenBy(item => item.ItemSeed)
                .ThenBy(item => item.Price)
                .ToArray();
            hasher.AppendUInt64((ulong)items.Length);
            foreach (var item in items) {
                hasher.AppendUInt32(item.StoreSlot);
                hasher.AppendUInt32(item.ItemSeed);
                hasher.AppendUInt32(item.Price);
                AppendItemState(hasher, item.State);
            }
        }

        if (snapshot.RandomState != 0) {
            hasher.AppendString("godevilution-gameplay-rng-v1");
            hasher.AppendUInt32(snapshot.RandomState);
        }

        return hasher.HexDigest();
    }

    private const int DiabloFixedPointScale = 64;

    private static int ResolveQ6(int whole, int raw)
    {
        return raw == 0 && whole != 0
            ? checked(whole * DiabloFixedPointScale)
            : raw;
    }

    private static void AppendAttribute(CanonicalStateHasher hasher, AttributeSnapshot? attribute)
    {
        hasher.AppendInt32(attribute?.Base ?? 0);
        hasher.AppendInt32(attribute?.Current ?? 0);
    }

    private static void AppendItemState(CanonicalStateHasher hasher, ItemStateSnapshot? state)
    {
        state ??= new ItemStateSnapshot { ItemType = -1, ItemIndex = -1 };
        hasher.AppendUInt32(state.CreateInfo);
        hasher.AppendInt32(state.ItemType);
        hasher.AppendInt32(state.PositionX);
        hasher.AppendInt32(state.PositionY);
        hasher.AppendBool(state.Deleted);
        hasher.AppendBool(state.Identified);
        hasher.AppendInt32(state.Magical);
        hasher.AppendInt32(state.EquipLocation);
        hasher.AppendInt32(state.ItemClass);
        hasher.AppendInt32(state.Value);
        hasher.AppendInt32(state.IdentifiedValue);
        hasher.AppendInt32(state.MinDamage);
        hasher.AppendInt32(state.MaxDamage);
        hasher.AppendInt32(state.ArmorClass);
        hasher.AppendUInt32(state.Flags);
        hasher.AppendInt32(state.MiscId);
        hasher.AppendInt32(state.SpellId);
        if (state.SpellSourceKind != ItemSpellSourceKind.Unspecified) {
            hasher.AppendString("godevilution-item-spell-source-kind-v1");
            hasher.AppendInt32((int)state.SpellSourceKind);
        }
        if (state.ItemActionKind != ItemActionKind.Unspecified) {
            hasher.AppendString("godevilution-item-action-kind-v1");
            hasher.AppendInt32((int)state.ItemActionKind);
        }
        hasher.AppendInt32(state.ItemIndex);
        hasher.AppendInt32(state.Charges);
        hasher.AppendInt32(state.MaxCharges);
        hasher.AppendInt32(state.Durability);
        hasher.AppendInt32(state.MaxDurability);
        hasher.AppendInt32(state.PlusDamage);
        hasher.AppendInt32(state.PlusToHit);
        hasher.AppendInt32(state.PlusArmorClass);
        hasher.AppendInt32(state.PlusStrength);
        hasher.AppendInt32(state.PlusMagic);
        hasher.AppendInt32(state.PlusDexterity);
        hasher.AppendInt32(state.PlusVitality);
        hasher.AppendInt32(state.PlusFireResistance);
        hasher.AppendInt32(state.PlusLightningResistance);
        hasher.AppendInt32(state.PlusMagicResistance);
        hasher.AppendInt32(state.PlusMana);
        hasher.AppendInt32(state.PlusHitPoints);
        hasher.AppendInt32(state.PlusDamageModifier);
        hasher.AppendInt32(state.PlusGetHit);
        hasher.AppendInt32(state.PlusLight);
        hasher.AppendInt32(state.SpellLevelAdd);
        hasher.AppendInt32(state.UniqueId);
        hasher.AppendInt32(state.FireMinDamage);
        hasher.AppendInt32(state.FireMaxDamage);
        hasher.AppendInt32(state.LightningMinDamage);
        hasher.AppendInt32(state.LightningMaxDamage);
        hasher.AppendInt32(state.PlusEnemyArmorClass);
        hasher.AppendInt32(state.PrefixPower);
        hasher.AppendInt32(state.SuffixPower);
        hasher.AppendInt32(state.ValueAdd1);
        hasher.AppendInt32(state.ValueMultiply1);
        hasher.AppendInt32(state.ValueAdd2);
        hasher.AppendInt32(state.ValueMultiply2);
        hasher.AppendInt32(state.MinimumStrength);
        hasher.AppendInt32(state.MinimumMagic);
        hasher.AppendInt32(state.MinimumDexterity);
        hasher.AppendBool(state.StatFlag);
        hasher.AppendInt32(state.HellfireDamageArmorFlags);
        hasher.AppendUInt32(state.Buff);
        hasher.AppendUInt32(state.InventoryWidth == 0 ? 1U : state.InventoryWidth);
        hasher.AppendUInt32(state.InventoryHeight == 0 ? 1U : state.InventoryHeight);
        if (!string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.HasNativeCursorGraphic) {
            hasher.AppendString("godevilution-dynamic-item-presentation-v1");
            hasher.AppendString(state.BaseName);
            hasher.AppendString(state.IdentifiedName);
            hasher.AppendBool(state.HasNativeCursorGraphic);
            if (state.HasNativeCursorGraphic)
                hasher.AppendInt32(state.NativeCursorGraphic);
        }
        if (state.ManaToLifePercent != 0 || state.LifeToManaPercent != 0) {
            hasher.AppendString("godevilution-item-resource-conversion-v1");
            hasher.AppendInt32(state.ManaToLifePercent);
            hasher.AppendInt32(state.LifeToManaPercent);
        }
    }
}
