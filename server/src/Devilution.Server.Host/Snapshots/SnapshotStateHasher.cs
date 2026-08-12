using Devilution.Domain.Hashing;
using Devilution.Protocol.V1;

namespace Devilution.Server.Snapshots;

/** Hashes the authoritative fields currently exposed by Snapshot. */
public static class SnapshotStateHasher
{
    public static string Compute(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasher = new CanonicalStateHasher();
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
            hasher.AppendUInt32(player.CharacterLevel);
            hasher.AppendUInt32(player.LevelId);
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
            var memorizedSpells = player.MemorizedSpells
                .OrderBy(spell => spell.SpellId)
                .ThenBy(spell => spell.Level)
                .ToArray();
            if (player.AbilitySpellId != 0 || memorizedSpells.Length != 0) {
                hasher.AppendString("godevilution-player-spells-v1");
                hasher.AppendUInt32(player.AbilitySpellId);
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
                    || monster.LightningResistance != 0 || monster.MagicResistance != 0) {
                    hasher.AppendInt32(monster.AttackDamage);
                    hasher.AppendInt32(monster.AggroRange);
                    hasher.AppendInt32(monster.FireResistance);
                    hasher.AppendInt32(monster.LightningResistance);
                    hasher.AppendInt32(monster.MagicResistance);
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
                if (monster.StoneCurseImmune) {
                    hasher.AppendString("godevilution-monster-stone-curse-immunity-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsUndead) {
                    hasher.AppendString("godevilution-monster-undead-v1");
                    hasher.AppendBool(true);
                }
                if (monster.IsPlayerMinion) {
                    hasher.AppendString("godevilution-monster-player-minion-v1");
                    hasher.AppendBool(true);
                }
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
                if (projectile.Behavior != 0
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
                    || projectile.ApocalypseHit) {
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
    }
}
