using Devilution.Protocol.V1;
using Devilution.Server.Snapshots;

namespace Devilution.Server.Protocol;

/** Maps transport-neutral Diablo state and events to the versioned wire schema. */
internal static class DiabloProtocolMapper
{
    public static Snapshot ToProtocolSnapshot(
        DiabloSimulationSnapshot source,
        IReadOnlyList<StorePlayerSnapshot>? participants = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var visibleParticipants = participants?
            .Where(player => player.EntityId != 0)
            .ToArray();
        if (visibleParticipants is not null
            && visibleParticipants.Select(player => player.EntityId).Distinct().Count()
                != visibleParticipants.Length) {
            throw new InvalidDataException(
                "Authoritative participants must have unique entity IDs.");
        }
        var projectedParticipants = visibleParticipants is null
            ? [source.Player]
            : new[] { source.Player }
                .Concat(visibleParticipants
                    .Where(player => player.EntityId != source.Player.EntityId)
                    .OrderBy(player => player.EntityId))
                .ToArray();

        var snapshot = new Snapshot {
            Tick = source.Tick,
            RandomState = source.RandomState,
        };
        foreach (var participant in projectedParticipants)
            snapshot.Players.Add(ToProtocolPlayer(participant));

        foreach (var monster in source.Monsters) {
            snapshot.Monsters.Add(new MonsterSnapshot {
                EntityId = monster.EntityId,
                MonsterId = monster.MonsterId,
                LevelId = monster.LevelId,
                PositionX = monster.PositionX,
                PositionY = monster.PositionY,
                HitPoints = monster.HitPoints,
                MaxHitPoints = monster.MaxHitPoints,
                ArmorClass = monster.ArmorClass,
                Alive = monster.HitPoints > 0,
                AttackDamage = monster.AttackDamage,
                AggroRange = monster.AggroRange,
                FireResistance = monster.FireResistance,
                LightningResistance = monster.LightningResistance,
                MagicResistance = monster.MagicResistance,
                MonsterLevel = monster.MonsterLevel,
                HitPointsQ6 = monster.HitPointsQ6,
                MaxHitPointsQ6 = monster.MaxHitPointsQ6,
                Moving = monster.Moving,
                FuturePositionX = monster.FuturePositionX,
                FuturePositionY = monster.FuturePositionY,
                Petrified = monster.Petrified,
                StoneCurseImmune = monster.StoneCurseImmune,
                IsUndead = monster.IsUndead,
                IsPlayerMinion = monster.IsPlayerMinion,
                OwnerPlayerId = monster.OwnerPlayerId,
                IsUnique = monster.IsUnique,
                IsDiablo = monster.IsDiablo,
                BerserkImmune = monster.BerserkImmune,
                MinDamage = monster.MinDamage,
                MaxDamage = monster.MaxDamage,
                MinDamageSpecial = monster.MinDamageSpecial,
                MaxDamageSpecial = monster.MaxDamageSpecial,
                IsBerserked = monster.IsBerserked,
                DeathAnimationTicks = monster.DeathAnimationTicks,
                DeathTicksRemaining = monster.DeathTicksRemaining,
            });
        }
        foreach (var item in source.WorldItems) {
            snapshot.WorldItems.Add(new WorldItemSnapshot {
                EntityId = item.EntityId,
                LevelId = item.LevelId,
                PositionX = item.PositionX,
                PositionY = item.PositionY,
                ItemSeed = item.ItemSeed,
                Price = item.Price,
                State = ToProtocolItemState(item.State),
            });
        }
        foreach (var @object in source.Objects) {
            snapshot.Objects.Add(new ObjectSnapshot {
                EntityId = @object.EntityId,
                ObjectId = @object.ObjectId,
                LevelId = @object.LevelId,
                PositionX = @object.PositionX,
                PositionY = @object.PositionY,
                Activated = @object.Activated,
                QuestId = @object.QuestId,
                EffectKind = (int)@object.EffectKind,
                EffectAmount = @object.EffectAmount,
                MissilePassable = @object.MissilePassable,
                Breakable = @object.Breakable,
                Destroyed = @object.Destroyed,
                TrapArmed = @object.TrapArmed,
            });
        }
        foreach (var quest in source.Quests) {
            snapshot.Quests.Add(new QuestSnapshot {
                QuestId = quest.QuestId,
                LevelId = quest.LevelId,
                Progress = quest.Progress,
                RequiredProgress = quest.RequiredProgress,
                Completed = quest.Completed,
            });
        }
        foreach (var projectile in source.Projectiles) {
            snapshot.Projectiles.Add(new ProjectileSnapshot {
                EntityId = projectile.EntityId,
                SourceEntityId = projectile.SourceEntityId,
                TargetEntityId = projectile.TargetEntityId,
                SpellId = projectile.SpellId,
                LevelId = projectile.LevelId,
                PositionX = projectile.PositionX,
                PositionY = projectile.PositionY,
                TargetX = projectile.TargetX,
                TargetY = projectile.TargetY,
                Damage = projectile.Damage,
                DamageType = (int)projectile.DamageType,
                AreaRadius = projectile.AreaRadius,
                RemainingTicks = projectile.RemainingTicks,
                Behavior = (int)projectile.Behavior,
                Phase = (int)projectile.Phase,
                StartX = projectile.StartX,
                StartY = projectile.StartY,
                TraveledX = projectile.TraveledX,
                TraveledY = projectile.TraveledY,
                VelocityX = projectile.VelocityX,
                VelocityY = projectile.VelocityY,
                OriginalDirection = projectile.OriginalDirection,
                PathIndex = projectile.PathIndex,
                PathTicks = projectile.PathTicks,
                Speed = projectile.Speed,
                ImpactLifetime = projectile.ImpactLifetime,
                ResistantDamageDivisor = projectile.ResistantDamageDivisor,
                LastCollisionTargetEntityId = projectile.LastCollisionTargetEntityId,
                EffectiveSpellLevel = projectile.EffectiveSpellLevel,
                WallLeftX = projectile.WallLeftX,
                WallLeftY = projectile.WallLeftY,
                WallRightX = projectile.WallRightX,
                WallRightY = projectile.WallRightY,
                WallLeftDirection = projectile.WallLeftDirection,
                WallRightDirection = projectile.WallRightDirection,
                WallLeftStopped = projectile.WallLeftStopped,
                WallRightStopped = projectile.WallRightStopped,
                WallHitDirection = projectile.WallHitDirection,
                ApocalypseScanX = projectile.ApocalypseScanX,
                ApocalypseScanY = projectile.ApocalypseScanY,
                ApocalypseHit = projectile.ApocalypseHit,
                RingOfFireLimitReached = projectile.RingOfFireLimitReached,
                ElementalOriginX = projectile.ElementalOriginX,
                ElementalOriginY = projectile.ElementalOriginY,
                GuardianState = projectile.GuardianState,
            });
        }
        foreach (var portal in source.Portals.OrderBy(portal => portal.PortalId)) {
            snapshot.Portals.Add(new PortalSnapshot {
                PortalId = portal.PortalId,
                SourceLevelId = portal.SourceLevelId,
                SourcePositionX = portal.SourcePositionX,
                SourcePositionY = portal.SourcePositionY,
                DestinationLevelId = portal.DestinationLevelId,
                DestinationPositionX = portal.DestinationPositionX,
                DestinationPositionY = portal.DestinationPositionY,
                RemainingTicks = portal.RemainingTicks,
                OwnerEntityId = portal.OwnerEntityId,
                Bidirectional = portal.Bidirectional,
            });
        }
        if (source.ActiveStore is not null) {
            var store = new StoreSnapshot { StoreId = source.ActiveStore.StoreId };
            foreach (var item in source.ActiveStore.Items) {
                store.Items.Add(new StoreItemSnapshot {
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    State = ToProtocolItemState(item.State),
                });
            }
            snapshot.ActiveStore = store;
        }

        snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
        return snapshot;
    }

    private static PlayerSnapshot ToProtocolPlayer(StorePlayerSnapshot state)
    {
        var player = new PlayerSnapshot {
            EntityId = state.EntityId,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            Gold = state.Gold,
            ActiveStoreId = state.ActiveStoreId ?? 0,
            Life = state.Life,
            Mana = state.Mana,
            ManaMaximum = state.ManaMaximum,
            LifeMaximum = state.LifeMaximum,
            CharacterLevel = state.CharacterLevel,
            CharacterClassId = state.CharacterClassId,
            CharacterName = state.CharacterName,
            ManaShieldActive = state.ManaShieldActive,
            ReflectCharges = state.ReflectCharges,
            AbilitySpellId = state.AbilitySpellId,
            HostileMode = !state.FriendlyMode,
            FacingDirection = (PlayerDirection)state.FacingDirection,
            CombatMode = (PlayerCombatMode)state.CombatMode,
            CombatModeEndTick = state.CombatModeEndTick,
            LevelId = state.LevelId,
            Experience = state.Experience,
            Attributes = new PlayerAttributesSnapshot {
                Strength = ToProtocolAttribute(state.Attributes.Strength),
                Magic = ToProtocolAttribute(state.Attributes.Magic),
                Dexterity = ToProtocolAttribute(state.Attributes.Dexterity),
                Vitality = ToProtocolAttribute(state.Attributes.Vitality),
            },
        };
        foreach (var spell in state.MemorizedSpells.OrderBy(spell => spell.SpellId)) {
            player.MemorizedSpells.Add(new PlayerSpellLevelSnapshot {
                SpellId = spell.SpellId,
                Level = spell.Level,
            });
        }
        if (state.CurrentAction is not null)
            player.CurrentAction = ToProtocolPlayerAction(state.CurrentAction);
        foreach (var effect in state.StatusEffects) {
            player.StatusEffects.Add(new StatusEffectSnapshot {
                EffectId = effect.EffectId,
                RemainingTicks = effect.RemainingTicks,
                Magnitude = effect.Magnitude,
                Behavior = (StatusEffectBehavior)effect.Behavior,
            });
        }
        foreach (var item in state.Inventory) {
            player.Inventory.Add(new ItemSnapshot {
                StoreId = item.StoreId,
                StoreSlot = item.StoreSlot,
                ItemSeed = item.ItemSeed,
                Price = item.Price,
                PurchasedAtTick = item.PurchasedAtTick,
                State = ToProtocolItemState(item.State),
            });
        }
        if (state.HeldItem is { } heldItem) {
            player.HeldItem = new ItemSnapshot {
                StoreId = heldItem.StoreId,
                StoreSlot = heldItem.StoreSlot,
                ItemSeed = heldItem.ItemSeed,
                Price = heldItem.Price,
                PurchasedAtTick = heldItem.PurchasedAtTick,
                State = ToProtocolItemState(heldItem.State),
            };
        }
        foreach (var item in state.Equipment) {
            player.Equipment.Add(new EquippedItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.State),
            });
        }
        foreach (var item in state.Belt) {
            player.Belt.Add(new BeltItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.State),
            });
        }
        player.InventoryGrid.Add(state.InventoryGrid);
        return player;
    }

    public static DiabloSimulationSnapshot FromProtocolSnapshot(Snapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var player = source.Players.Count == 1
            ? ToPlayerState(source.Players[0])
            : throw new InvalidDataException(
                "The authoritative save must contain exactly one player snapshot.");
        var activeStore = source.ActiveStore is null
            ? null
            : new DiabloStoreState(
                source.ActiveStore.StoreId,
                source.ActiveStore.Items.Select(item => new StoreItem(
                    item.StoreSlot,
                    item.ItemSeed,
                    item.Price,
                    FromProtocolItemState(item.State))).ToArray());
        return new DiabloSimulationSnapshot(
            source.Tick,
            player,
            source.Monsters.Select(monster => new DiabloMonsterState(
                monster.EntityId,
                monster.MonsterId,
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                monster.HitPoints,
                monster.MaxHitPoints,
                monster.ArmorClass,
                monster.AttackDamage,
                monster.AggroRange,
                monster.FireResistance,
                monster.LightningResistance,
                monster.MagicResistance) {
                MonsterLevel = monster.MonsterLevel,
                Moving = monster.Moving,
                FuturePositionX = monster.Moving
                    ? monster.FuturePositionX
                    : monster.PositionX,
                FuturePositionY = monster.Moving
                    ? monster.FuturePositionY
                    : monster.PositionY,
                Petrified = monster.Petrified,
                StoneCurseImmune = monster.StoneCurseImmune,
                IsUndead = monster.IsUndead,
                IsPlayerMinion = monster.IsPlayerMinion,
                OwnerPlayerId = monster.OwnerPlayerId,
                IsUnique = monster.IsUnique,
                IsDiablo = monster.IsDiablo,
                BerserkImmune = monster.BerserkImmune,
                MinDamage = monster.MinDamage,
                MaxDamage = monster.MaxDamage,
                MinDamageSpecial = monster.MinDamageSpecial,
                MaxDamageSpecial = monster.MaxDamageSpecial,
                IsBerserked = monster.IsBerserked,
                DeathAnimationTicks = monster.DeathAnimationTicks,
                DeathTicksRemaining = monster.DeathTicksRemaining,
                HitPointsQ6 = ResolveQ6(monster.HitPoints, monster.HitPointsQ6),
                MaxHitPointsQ6 = ResolveQ6(monster.MaxHitPoints, monster.MaxHitPointsQ6),
            }).ToArray(),
            source.WorldItems.Select(item => new DiabloWorldItemState(
                item.EntityId,
                item.LevelId,
                item.PositionX,
                item.PositionY,
                item.ItemSeed,
                item.Price,
                FromProtocolItemState(item.State))).ToArray(),
            source.Objects.Select(@object => new DiabloObjectState(
                @object.EntityId,
                @object.ObjectId,
                @object.LevelId,
                @object.PositionX,
                @object.PositionY,
                @object.Activated,
                @object.QuestId,
                (AuthoritativeObjectEffectKind)@object.EffectKind,
                @object.EffectAmount) {
                MissilePassable = @object.MissilePassable,
                Breakable = @object.Breakable,
                Destroyed = @object.Destroyed,
                TrapArmed = @object.TrapArmed,
            }).ToArray(),
            source.Quests.Select(quest => new DiabloQuestState(
                quest.QuestId,
                quest.LevelId,
                quest.Progress,
                quest.RequiredProgress,
                quest.Completed)).ToArray(),
            source.Projectiles.Select(projectile => new DiabloProjectileState(
                projectile.EntityId,
                projectile.SourceEntityId,
                projectile.TargetEntityId,
                projectile.SpellId,
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                projectile.TargetX,
                projectile.TargetY,
                projectile.Damage,
                (AuthoritativeDamageType)projectile.DamageType,
                projectile.AreaRadius,
                projectile.RemainingTicks) {
                Behavior = (AuthoritativeProjectileBehavior)projectile.Behavior,
                Phase = (AuthoritativeProjectilePhase)projectile.Phase,
                StartX = projectile.StartX,
                StartY = projectile.StartY,
                TraveledX = projectile.TraveledX,
                TraveledY = projectile.TraveledY,
                VelocityX = projectile.VelocityX,
                VelocityY = projectile.VelocityY,
                OriginalDirection = projectile.OriginalDirection,
                PathIndex = projectile.PathIndex,
                PathTicks = projectile.PathTicks,
                Speed = projectile.Speed,
                ImpactLifetime = projectile.ImpactLifetime == 0
                    ? 1
                    : projectile.ImpactLifetime,
                ResistantDamageDivisor = projectile.ResistantDamageDivisor == 0
                    ? 1
                    : projectile.ResistantDamageDivisor,
                LastCollisionTargetEntityId = projectile.LastCollisionTargetEntityId,
                EffectiveSpellLevel = projectile.EffectiveSpellLevel,
                WallLeftX = projectile.WallLeftX,
                WallLeftY = projectile.WallLeftY,
                WallRightX = projectile.WallRightX,
                WallRightY = projectile.WallRightY,
                WallLeftDirection = projectile.WallLeftDirection,
                WallRightDirection = projectile.WallRightDirection,
                WallLeftStopped = projectile.WallLeftStopped,
                WallRightStopped = projectile.WallRightStopped,
                WallHitDirection = projectile.WallHitDirection,
                ApocalypseScanX = projectile.ApocalypseScanX,
                ApocalypseScanY = projectile.ApocalypseScanY,
                ApocalypseHit = projectile.ApocalypseHit,
                RingOfFireLimitReached = projectile.RingOfFireLimitReached,
                ElementalOriginX = projectile.ElementalOriginX,
                ElementalOriginY = projectile.ElementalOriginY,
                GuardianState = projectile.GuardianState,
            }).ToArray(),
            activeStore) {
            RandomState = source.RandomState,
            Portals = source.Portals.Select(portal => new DiabloPortalState(
                portal.PortalId,
                portal.SourceLevelId,
                portal.SourcePositionX,
                portal.SourcePositionY,
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY) {
                RemainingTicks = portal.RemainingTicks,
                OwnerEntityId = portal.OwnerEntityId,
                Bidirectional = portal.Bidirectional,
            }).ToArray(),
        };
    }

    public static EventBatch? ToProtocolEventBatch(
        IReadOnlyList<DiabloGameplayEvent> events,
        uint entityId,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
            return null;

        var batch = new EventBatch { Tick = tick };
        foreach (var gameplayEvent in events) {
            switch (gameplayEvent.Kind) {
            case DiabloGameplayEventKind.Damage:
                batch.Events.Add(new GameEvent {
                    Damage = new DamageEvent {
                        SourceEntityId = gameplayEvent.SourceEntityId,
                        TargetEntityId = gameplayEvent.TargetEntityId,
                        Amount = gameplayEvent.Amount,
                        AmountQ6 = gameplayEvent.AmountQ6,
                    },
                });
                break;
            case DiabloGameplayEventKind.Experience:
                batch.Events.Add(new GameEvent {
                    Experience = new ExperienceEvent {
                        PlayerEntityId = entityId,
                        Amount = (uint)gameplayEvent.Amount,
                    },
                });
                break;
            case DiabloGameplayEventKind.Healing:
                batch.Events.Add(new GameEvent {
                    Healing = new HealingEvent {
                        TargetEntityId = gameplayEvent.TargetEntityId,
                        Amount = gameplayEvent.AmountQ6,
                        AmountQ6 = gameplayEvent.AmountQ6,
                    },
                });
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Diablo gameplay event kind '{gameplayEvent.Kind}'.");
            }
        }
        return batch;
    }

    private static StorePlayerSnapshot ToPlayerState(PlayerSnapshot source)
    {
        var attributes = source.Attributes ?? new PlayerAttributesSnapshot();
        return new StorePlayerSnapshot(
            source.Gold,
            source.ActiveStoreId == 0 ? null : source.ActiveStoreId,
            source.Inventory.Select(item => new OwnedStoreItem(
                item.StoreId,
                item.StoreSlot,
                item.ItemSeed,
                item.Price,
                item.PurchasedAtTick,
                FromProtocolItemState(item.State))).ToArray(),
            source.Experience,
            source.Life,
            source.Mana,
            source.ManaMaximum,
            new PlayerAttributesState(
                FromProtocolAttribute(attributes.Strength),
                FromProtocolAttribute(attributes.Magic),
                FromProtocolAttribute(attributes.Dexterity),
                FromProtocolAttribute(attributes.Vitality)),
            source.Equipment.Select(item => new EquippedStoreItem(
                item.Slot,
                item.ItemSeed,
                FromProtocolItemState(item.State))).ToArray(),
            source.InventoryGrid.ToArray(),
            source.Belt.Select(item => new BeltStoreItem(
                item.Slot,
                item.ItemSeed,
                FromProtocolItemState(item.State))).ToArray()) {
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            LifeMaximum = source.LifeMaximum,
            CharacterLevel = source.CharacterLevel,
            CharacterClassId = source.CharacterClassId,
            CharacterName = source.CharacterName,
            ManaShieldActive = source.ManaShieldActive,
            ReflectCharges = source.ReflectCharges,
            AbilitySpellId = source.AbilitySpellId,
            MemorizedSpells = source.MemorizedSpells.Select(spell =>
                new MemorizedSpellState(spell.SpellId, spell.Level)).ToArray(),
            LevelId = source.LevelId,
            EntityId = source.EntityId,
            StatusEffects = source.StatusEffects.Select(effect =>
                new AuthoritativeStatusEffect(
                    effect.EffectId,
                    effect.RemainingTicks,
                    effect.Magnitude) {
                    Behavior = (AuthoritativeStatusEffectBehavior)effect.Behavior,
                }).ToArray(),
            FriendlyMode = !source.HostileMode,
            FacingDirection = (DiabloPlayerDirection)source.FacingDirection,
            CombatMode = (DiabloPlayerCombatMode)source.CombatMode,
            CombatModeEndTick = source.CombatModeEndTick,
            CurrentAction = FromProtocolPlayerAction(source.CurrentAction),
            HeldItem = source.HeldItem is null
                ? null
                : new OwnedStoreItem(
                    source.HeldItem.StoreId,
                    source.HeldItem.StoreSlot,
                    source.HeldItem.ItemSeed,
                    source.HeldItem.Price,
                    source.HeldItem.PurchasedAtTick,
                    FromProtocolItemState(source.HeldItem.State)),
        };
    }

    private static PlayerActionSnapshot ToProtocolPlayerAction(
        AuthoritativePlayerActionState source)
    {
        var result = new PlayerActionSnapshot {
            Kind = (PlayerActionKind)source.Kind,
            StartedAtTick = source.StartedAtTick,
            EffectTick = source.EffectTick,
            ReadyTick = source.ReadyTick,
            EndTick = source.EndTick,
            EffectApplied = source.EffectApplied,
            DirectionX = source.DirectionX,
            DirectionY = source.DirectionY,
            TargetEntityId = source.TargetEntityId,
        };
        if (source.CastIntent is not null) {
            result.Cast = new CastRequested {
                SpellId = source.CastIntent.SpellId,
                TargetEntityId = source.CastIntent.TargetEntityId,
                TargetX = source.CastIntent.TargetX,
                TargetY = source.CastIntent.TargetY,
                Source = (CastSourceKind)source.CastIntent.Source,
            };
            if (source.CastIntent.SourceItem is DiabloPlayerItemReference item) {
                result.Cast.SourceItem = new PlayerItemReference {
                    Location = (PlayerItemLocation)item.Location,
                    Slot = item.Slot,
                };
            }
        }
        return result;
    }

    private static AuthoritativePlayerActionState? FromProtocolPlayerAction(
        PlayerActionSnapshot? source)
    {
        if (source is null)
            return null;
        DiabloCastIntent? castIntent = null;
        if (source.Cast is not null) {
            var sourceItem = source.Cast.SourceItem is null
                ? (DiabloPlayerItemReference?)null
                : new DiabloPlayerItemReference(
                    (DiabloPlayerItemLocation)source.Cast.SourceItem.Location,
                    source.Cast.SourceItem.Slot);
            castIntent = new DiabloCastIntent(
                source.Cast.SpellId,
                source.Cast.TargetEntityId,
                source.Cast.TargetX,
                source.Cast.TargetY,
                (DiabloCastSourceKind)source.Cast.Source,
                sourceItem);
        }
        return new AuthoritativePlayerActionState(
            (DiabloPlayerActionKind)source.Kind,
            source.StartedAtTick,
            source.EffectTick,
            source.ReadyTick,
            source.EndTick) {
            EffectApplied = source.EffectApplied,
            DirectionX = source.DirectionX,
            DirectionY = source.DirectionY,
            TargetEntityId = source.TargetEntityId,
            CastIntent = castIntent,
        };
    }

    private static int ResolveQ6(int whole, int raw)
    {
        return raw == 0 && whole != 0
            ? DiabloFixedPointQ6.FromWhole(whole)
            : raw;
    }

    private static AttributeSnapshot ToProtocolAttribute(PlayerAttributeState attribute)
    {
        return new AttributeSnapshot { Base = attribute.Base, Current = attribute.Current };
    }

    private static PlayerAttributeState FromProtocolAttribute(AttributeSnapshot? attribute)
    {
        return new PlayerAttributeState(attribute?.Base ?? 0, attribute?.Current ?? 0);
    }

    private static AuthoritativeItemState FromProtocolItemState(ItemStateSnapshot? source)
    {
        source ??= new ItemStateSnapshot { ItemType = -1, ItemIndex = -1 };
        return AuthoritativeItemState.Empty with {
            CreateInfo = source.CreateInfo,
            ItemType = source.ItemType,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            Deleted = source.Deleted,
            Identified = source.Identified,
            Magical = source.Magical,
            EquipLocation = source.EquipLocation,
            ItemClass = source.ItemClass,
            Value = source.Value,
            IdentifiedValue = source.IdentifiedValue,
            MinDamage = source.MinDamage,
            MaxDamage = source.MaxDamage,
            ArmorClass = source.ArmorClass,
            Flags = source.Flags,
            MiscId = source.MiscId,
            SpellId = source.SpellId,
            ItemIndex = source.ItemIndex,
            Charges = source.Charges,
            MaxCharges = source.MaxCharges,
            Durability = source.Durability,
            MaxDurability = source.MaxDurability,
            PlusDamage = source.PlusDamage,
            PlusToHit = source.PlusToHit,
            PlusArmorClass = source.PlusArmorClass,
            PlusStrength = source.PlusStrength,
            PlusMagic = source.PlusMagic,
            PlusDexterity = source.PlusDexterity,
            PlusVitality = source.PlusVitality,
            PlusFireResistance = source.PlusFireResistance,
            PlusLightningResistance = source.PlusLightningResistance,
            PlusMagicResistance = source.PlusMagicResistance,
            PlusMana = source.PlusMana,
            PlusHitPoints = source.PlusHitPoints,
            PlusDamageModifier = source.PlusDamageModifier,
            PlusGetHit = source.PlusGetHit,
            PlusLight = source.PlusLight,
            SpellLevelAdd = source.SpellLevelAdd,
            UniqueId = source.UniqueId,
            FireMinDamage = source.FireMinDamage,
            FireMaxDamage = source.FireMaxDamage,
            LightningMinDamage = source.LightningMinDamage,
            LightningMaxDamage = source.LightningMaxDamage,
            PlusEnemyArmorClass = source.PlusEnemyArmorClass,
            PrefixPower = source.PrefixPower,
            SuffixPower = source.SuffixPower,
            ValueAdd1 = source.ValueAdd1,
            ValueMultiply1 = source.ValueMultiply1,
            ValueAdd2 = source.ValueAdd2,
            ValueMultiply2 = source.ValueMultiply2,
            MinimumStrength = source.MinimumStrength,
            MinimumMagic = source.MinimumMagic,
            MinimumDexterity = source.MinimumDexterity,
            StatFlag = source.StatFlag,
            HellfireDamageArmorFlags = source.HellfireDamageArmorFlags,
            Buff = source.Buff,
            InventoryWidth = source.InventoryWidth == 0 ? 1 : (int)source.InventoryWidth,
            InventoryHeight = source.InventoryHeight == 0 ? 1 : (int)source.InventoryHeight,
            BaseName = source.BaseName,
            IdentifiedName = source.IdentifiedName,
            NativeCursorGraphic = source.HasNativeCursorGraphic
                ? source.NativeCursorGraphic
                : null,
        };
    }

    private static ItemStateSnapshot ToProtocolItemState(AuthoritativeItemState state)
    {
        var snapshot = new ItemStateSnapshot {
            CreateInfo = state.CreateInfo,
            ItemType = state.ItemType,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            Deleted = state.Deleted,
            Identified = state.Identified,
            Magical = state.Magical,
            EquipLocation = state.EquipLocation,
            ItemClass = state.ItemClass,
            Value = state.Value,
            IdentifiedValue = state.IdentifiedValue,
            MinDamage = state.MinDamage,
            MaxDamage = state.MaxDamage,
            ArmorClass = state.ArmorClass,
            Flags = state.Flags,
            MiscId = state.MiscId,
            SpellId = state.SpellId,
            ItemIndex = state.ItemIndex,
            Charges = state.Charges,
            MaxCharges = state.MaxCharges,
            Durability = state.Durability,
            MaxDurability = state.MaxDurability,
            PlusDamage = state.PlusDamage,
            PlusToHit = state.PlusToHit,
            PlusArmorClass = state.PlusArmorClass,
            PlusStrength = state.PlusStrength,
            PlusMagic = state.PlusMagic,
            PlusDexterity = state.PlusDexterity,
            PlusVitality = state.PlusVitality,
            PlusFireResistance = state.PlusFireResistance,
            PlusLightningResistance = state.PlusLightningResistance,
            PlusMagicResistance = state.PlusMagicResistance,
            PlusMana = state.PlusMana,
            PlusHitPoints = state.PlusHitPoints,
            PlusDamageModifier = state.PlusDamageModifier,
            PlusGetHit = state.PlusGetHit,
            PlusLight = state.PlusLight,
            SpellLevelAdd = state.SpellLevelAdd,
            UniqueId = state.UniqueId,
            FireMinDamage = state.FireMinDamage,
            FireMaxDamage = state.FireMaxDamage,
            LightningMinDamage = state.LightningMinDamage,
            LightningMaxDamage = state.LightningMaxDamage,
            PlusEnemyArmorClass = state.PlusEnemyArmorClass,
            PrefixPower = state.PrefixPower,
            SuffixPower = state.SuffixPower,
            ValueAdd1 = state.ValueAdd1,
            ValueMultiply1 = state.ValueMultiply1,
            ValueAdd2 = state.ValueAdd2,
            ValueMultiply2 = state.ValueMultiply2,
            MinimumStrength = state.MinimumStrength,
            MinimumMagic = state.MinimumMagic,
            MinimumDexterity = state.MinimumDexterity,
            StatFlag = state.StatFlag,
            HellfireDamageArmorFlags = state.HellfireDamageArmorFlags,
            Buff = state.Buff,
            InventoryWidth = state.InventoryWidth > 1 ? (uint)state.InventoryWidth : 0,
            InventoryHeight = state.InventoryHeight > 1 ? (uint)state.InventoryHeight : 0,
            BaseName = state.BaseName,
            IdentifiedName = state.IdentifiedName,
        };
        if (state.NativeCursorGraphic.HasValue)
            snapshot.NativeCursorGraphic = state.NativeCursorGraphic.Value;
        return snapshot;
    }
}
