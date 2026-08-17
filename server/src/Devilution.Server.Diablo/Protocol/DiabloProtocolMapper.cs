using Devilution.Protocol.V1;
using Devilution.Server.Snapshots;

namespace Devilution.Server.Protocol;

/** Maps transport-neutral Diablo state and events to the versioned wire schema. */
internal static class DiabloProtocolMapper
{
    public static Snapshot ToProtocolSnapshot(
        DiabloSimulationSnapshot source,
        IReadOnlyList<StorePlayerSnapshot>? participants = null,
        bool revealTraps = true,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        IReadOnlyList<AuthoritativeLevel>? levelGeometry = null,
        bool allowZeroPlayerEntityId = false)
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
            VictoryTriggered = source.VictoryTriggered,
            Difficulty = ToProtocolDifficulty(source.Difficulty),
        };
        foreach (var participant in projectedParticipants)
            snapshot.Players.Add(ToProtocolPlayer(participant, spells, spellBooks));

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
                AcidResistance = monster.AcidResistance,
                MonsterLevel = monster.MonsterLevel,
                HitPointsQ6 = monster.HitPointsQ6,
                MaxHitPointsQ6 = monster.MaxHitPointsQ6,
                Moving = monster.Moving,
                FuturePositionX = monster.FuturePositionX,
                FuturePositionY = monster.FuturePositionY,
                Direction = monster.Direction,
                Petrified = monster.Petrified,
                StoneCurseImmune = monster.StoneCurseImmune,
                IsUndead = monster.IsUndead,
                IsDemon = monster.IsDemon,
                IsAnimal = monster.IsAnimal,
                HasKnockback = monster.HasKnockback,
                IsPlayerMinion = monster.IsPlayerMinion,
                OwnerPlayerId = monster.OwnerPlayerId,
                IsUnique = monster.IsUnique,
                UniqueIdentity = monster.UniqueIdentity,
                IsDiablo = monster.IsDiablo,
                BerserkImmune = monster.BerserkImmune,
                MinDamage = monster.MinDamage,
                MaxDamage = monster.MaxDamage,
                MinDamageSpecial = monster.MinDamageSpecial,
                MaxDamageSpecial = monster.MaxDamageSpecial,
                IsBerserked = monster.IsBerserked,
                DeathAnimationTicks = monster.DeathAnimationTicks,
                DeathTicksRemaining = monster.DeathTicksRemaining,
                ExperienceReward = monster.ExperienceReward,
                ToHit = monster.ToHit,
                ToHitSpecial = monster.ToHitSpecial,
                NativeAi = monster.NativeAi,
                Intelligence = monster.Intelligence,
                GargoyleSpecialActive = monster.GargoyleSpecialActive,
                FallenFearTicks = monster.FallenFearTicks,
                FallenFearDirection = monster.FallenFearDirection,
                SneakHidden = monster.SneakHidden,
                SneakRetreatTicks = monster.SneakRetreatTicks,
                ScavengerHealingTicks = monster.ScavengerHealingTicks,
                ScavengerCorpseEntityId = monster.ScavengerCorpseEntityId,
                BatRetreatTicks = monster.BatRetreatTicks,
                BatRetreatDirection = monster.BatRetreatDirection,
                BatMovementIdleTicks = monster.BatMovementIdleTicks,
                BatMovementMode = monster.BatMovementMode,
                RangedAvoidanceMoveTicks = monster.RangedAvoidanceMoveTicks,
                RangedAvoidanceTurn = monster.RangedAvoidanceTurn,
                RangedAvoidanceMovementIdleTicks = monster.RangedAvoidanceMovementIdleTicks,
                RangedAvoidanceMovementMode = monster.RangedAvoidanceMovementMode,
                RhinoMovementTicks = monster.RhinoMovementTicks,
                RhinoMovementTurn = monster.RhinoMovementTurn,
                AvoidanceMovementTicks = monster.AvoidanceMovementTicks,
                AvoidanceMovementTurn = monster.AvoidanceMovementTurn,
                AvoidanceMovementIdleTicks = monster.AvoidanceMovementIdleTicks,
                AvoidanceMovementMode = monster.AvoidanceMovementMode,
                SkeletonMeleeDelayTicks = monster.SkeletonMeleeDelayTicks,
                SkeletonRangedMovementIdleTicks = monster.SkeletonRangedMovementIdleTicks,
                SkeletonRangedMovementMode = monster.SkeletonRangedMovementMode,
                FatMovementIdleTicks = monster.FatMovementIdleTicks,
                FatMovementMode = monster.FatMovementMode,
                RhinoMovementIdleTicks = monster.RhinoMovementIdleTicks,
                RhinoMovementMode = monster.RhinoMovementMode,
                RhinoDelayTicks = monster.RhinoDelayTicks,
                SkeletonKingDelayTicks = monster.SkeletonKingDelayTicks,
                HorkDemonDelayTicks = monster.HorkDemonDelayTicks,
                RangedAvoidanceDelayTicks = monster.RangedAvoidanceDelayTicks,
                SnakeDelayTicks = monster.SnakeDelayTicks,
                CounselorDelayTicks = monster.CounselorDelayTicks,
                AvoidanceDelayTicks = monster.AvoidanceDelayTicks,
                RangedDelayTicks = monster.RangedDelayTicks,
                MegaDelayTicks = monster.MegaDelayTicks,
                CounselorRetreatTicks = monster.CounselorRetreatTicks,
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
                State = ToProtocolItemState(item.State, spells, spellBooks),
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
                QuestProgressAmount = @object.QuestProgressAmount == 0
                    ? 1
                    : @object.QuestProgressAmount,
                MissilePassable = @object.MissilePassable,
                Breakable = @object.Breakable,
                Destroyed = @object.Destroyed,
                TrapArmed = revealTraps && @object.TrapArmed,
                IsShrine = @object.IsShrine,
                PresentationKind = ResolveObjectPresentationKind(@object, revealTraps),
            });
        }
        foreach (var quest in source.Quests) {
            var projectedQuest = new QuestSnapshot {
                QuestId = quest.QuestId,
                LevelId = quest.LevelId,
                Progress = quest.Progress,
                RequiredProgress = quest.RequiredProgress,
                Completed = quest.Completed,
                Unavailable = quest.Unavailable,
                Initial = !quest.Active && !quest.Completed && !quest.Unavailable,
                Logged = quest.Logged,
                Variable1 = quest.Variable1,
                Variable2 = quest.Variable2,
                MessageId = quest.MessageId,
                CompletionMonsterId = quest.CompletionMonsterId,
                CompletionUniqueIdentity = quest.CompletionUniqueIdentity,
            };
            if (quest.TurnInTownerId >= 0)
                projectedQuest.TurnInTownerId = quest.TurnInTownerId;
            projectedQuest.TurnInOnCompletion = quest.TurnInOnCompletion;
            if (quest.TurnInCompletionVariable2 >= 0)
                projectedQuest.TurnInCompletionVariable2 = quest.TurnInCompletionVariable2;
            if (quest.TurnInAlternateItemId != 0)
                projectedQuest.TurnInAlternateItemId = quest.TurnInAlternateItemId;
            if (quest.TurnInAlternateMessageId != 0)
                projectedQuest.TurnInAlternateMessageId = quest.TurnInAlternateMessageId;
            projectedQuest.TurnInActivationOnly = quest.TurnInActivationOnly;
            snapshot.Quests.Add(projectedQuest);
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
                DamageQ6 = projectile.DamageQ6,
                ElementalFireDamage = projectile.ElementalFireDamage,
                ElementalLightningDamage = projectile.ElementalLightningDamage,
                AttackHit = projectile.AttackHit,
                PerilSelfDamageQ6 = projectile.PerilSelfDamageQ6,
                DoppelgangerProc = projectile.DoppelgangerProc,
                ActivationDelayTicks = projectile.ActivationDelayTicks,
                DamageMinimum = projectile.DamageMinimum,
                DamageMaximum = projectile.DamageMaximum,
                PresentationKind = ResolveProjectilePresentationKind(
                    projectile.Behavior,
                    projectile.Phase),
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
        foreach (var corpse in source.Corpses.OrderBy(corpse => corpse.EntityId)) {
            snapshot.Corpses.Add(new CorpseSnapshot {
                EntityId = corpse.EntityId,
                LevelId = corpse.LevelId,
                PositionX = corpse.PositionX,
                PositionY = corpse.PositionY,
            });
        }
        foreach (var level in levelGeometry ?? []) {
            var geometry = new LevelGeometrySnapshot {
                LevelId = level.LevelId,
                Width = level.Width,
                Height = level.Height,
            };
            geometry.BlockedCells.Add(level.BlockedCells);
            geometry.MissileBlockedCells.Add(level.MissileBlockedCells);
            snapshot.LevelGeometry.Add(geometry);
        }
        if (source.ActiveStore is not null) {
            var store = new StoreSnapshot { StoreId = source.ActiveStore.StoreId };
            foreach (var item in source.ActiveStore.Items) {
                store.Items.Add(new StoreItemSnapshot {
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    State = ToProtocolItemState(item.State, spells, spellBooks),
                });
            }
            snapshot.ActiveStore = store;
        }

        ValidateEntityIds(snapshot, allowZeroPlayerEntityId);
        snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
        return snapshot;
    }

    private static PlayerSnapshot ToProtocolPlayer(
        StorePlayerSnapshot state,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks)
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
            BaseManaMaximum = state.BaseManaMaximum,
            BaseLifeMaximum = state.BaseLifeMaximum,
            CharacterLevel = state.CharacterLevel,
            CharacterClassId = state.CharacterClassId,
            CharacterName = state.CharacterName,
            ManaShieldActive = state.ManaShieldActive,
            ReflectCharges = state.ReflectCharges,
            AbilitySpellId = state.AbilitySpellId,
            AbilityKind = (PlayerAbilityKind)state.AbilityKind,
            HostileMode = !state.FriendlyMode,
            FacingDirection = (PlayerDirection)state.FacingDirection,
            CombatMode = (PlayerCombatMode)state.CombatMode,
            CombatModeEndTick = state.CombatModeEndTick,
            LevelId = state.LevelId,
            Experience = state.Experience,
            DiabloKillLevel = state.DiabloKillLevel,
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
                DisplayName = ResolveSpellDisplayName(spell.SpellId, spells, spellBooks),
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
                State = ToProtocolItemState(item.State, spells, spellBooks),
            });
        }
        if (state.HeldItem is { } heldItem) {
            player.HeldItem = new ItemSnapshot {
                StoreId = heldItem.StoreId,
                StoreSlot = heldItem.StoreSlot,
                ItemSeed = heldItem.ItemSeed,
                Price = heldItem.Price,
                PurchasedAtTick = heldItem.PurchasedAtTick,
                State = ToProtocolItemState(heldItem.State, spells, spellBooks),
            };
        }
        foreach (var item in state.Equipment) {
            player.Equipment.Add(new EquippedItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.State, spells, spellBooks),
            });
        }
        foreach (var item in state.Belt) {
            player.Belt.Add(new BeltItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.State, spells, spellBooks),
            });
        }
        player.InventoryGrid.Add(state.InventoryGrid);
        return player;
    }

    public static DiabloSimulationSnapshot FromProtocolSnapshot(Snapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateEntityIds(source, allowZeroPlayerEntityId: true);
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
                monster.MagicResistance,
                monster.AcidResistance) {
                MonsterLevel = monster.MonsterLevel,
                Moving = monster.Moving,
                FuturePositionX = monster.Moving
                    ? monster.FuturePositionX
                    : monster.PositionX,
                FuturePositionY = monster.Moving
                    ? monster.FuturePositionY
                    : monster.PositionY,
                Direction = monster.Direction,
                Petrified = monster.Petrified,
                StoneCurseImmune = monster.StoneCurseImmune,
                IsUndead = monster.IsUndead,
                IsDemon = monster.IsDemon,
                IsAnimal = monster.IsAnimal,
                HasKnockback = monster.HasKnockback,
                IsPlayerMinion = monster.IsPlayerMinion,
                OwnerPlayerId = monster.OwnerPlayerId,
                IsUnique = monster.IsUnique,
                UniqueIdentity = monster.UniqueIdentity,
                IsDiablo = monster.IsDiablo,
                BerserkImmune = monster.BerserkImmune,
                MinDamage = monster.MinDamage,
                MaxDamage = monster.MaxDamage,
                MinDamageSpecial = monster.MinDamageSpecial,
                MaxDamageSpecial = monster.MaxDamageSpecial,
                IsBerserked = monster.IsBerserked,
                DeathAnimationTicks = monster.DeathAnimationTicks,
                DeathTicksRemaining = monster.DeathTicksRemaining,
                ExperienceReward = monster.ExperienceReward,
                ToHit = monster.ToHit,
                ToHitSpecial = monster.ToHitSpecial,
                NativeAi = monster.NativeAi,
                Intelligence = monster.Intelligence,
                GargoyleSpecialActive = monster.GargoyleSpecialActive,
                FallenFearTicks = monster.FallenFearTicks,
                FallenFearDirection = monster.FallenFearDirection,
                SneakHidden = monster.SneakHidden,
                SneakRetreatTicks = monster.SneakRetreatTicks,
                ScavengerHealingTicks = monster.ScavengerHealingTicks,
                ScavengerCorpseEntityId = monster.ScavengerCorpseEntityId,
                BatRetreatTicks = monster.BatRetreatTicks,
                BatRetreatDirection = monster.BatRetreatDirection,
                BatMovementIdleTicks = monster.BatMovementIdleTicks,
                BatMovementMode = monster.BatMovementMode,
                RangedAvoidanceMoveTicks = monster.HasRangedAvoidanceMoveTicks
                    ? monster.RangedAvoidanceMoveTicks
                    : -1,
                RangedAvoidanceTurn = monster.HasRangedAvoidanceTurn
                    ? monster.RangedAvoidanceTurn
                    : 0,
                RangedAvoidanceMovementIdleTicks = monster.HasRangedAvoidanceMovementIdleTicks
                    ? monster.RangedAvoidanceMovementIdleTicks
                    : -1,
                RangedAvoidanceMovementMode = monster.HasRangedAvoidanceMovementMode
                    ? monster.RangedAvoidanceMovementMode
                    : 0,
                RhinoMovementTicks = monster.HasRhinoMovementTicks
                    ? monster.RhinoMovementTicks
                    : -1,
                RhinoMovementTurn = monster.HasRhinoMovementTurn
                    ? monster.RhinoMovementTurn
                    : 0,
                AvoidanceMovementTicks = monster.HasAvoidanceMovementTicks
                    ? monster.AvoidanceMovementTicks
                    : -1,
                AvoidanceMovementTurn = monster.HasAvoidanceMovementTurn
                    ? monster.AvoidanceMovementTurn
                    : 0,
                AvoidanceMovementIdleTicks = monster.HasAvoidanceMovementIdleTicks
                    ? monster.AvoidanceMovementIdleTicks
                    : -1,
                AvoidanceMovementMode = monster.HasAvoidanceMovementMode
                    ? monster.AvoidanceMovementMode
                    : 0,
                SkeletonMeleeDelayTicks = monster.HasSkeletonMeleeDelayTicks
                    ? monster.SkeletonMeleeDelayTicks
                    : 0,
                SkeletonRangedMovementIdleTicks = monster.HasSkeletonRangedMovementIdleTicks
                    ? monster.SkeletonRangedMovementIdleTicks
                    : -1,
                SkeletonRangedMovementMode = monster.HasSkeletonRangedMovementMode
                    ? monster.SkeletonRangedMovementMode
                    : 0,
                FatMovementIdleTicks = monster.HasFatMovementIdleTicks
                    ? monster.FatMovementIdleTicks
                    : -1,
                FatMovementMode = monster.HasFatMovementMode
                    ? monster.FatMovementMode
                    : 0,
                RhinoMovementIdleTicks = monster.HasRhinoMovementIdleTicks
                    ? monster.RhinoMovementIdleTicks
                    : -1,
                RhinoMovementMode = monster.HasRhinoMovementMode
                    ? monster.RhinoMovementMode
                    : 0,
                RhinoDelayTicks = monster.HasRhinoDelayTicks
                    ? monster.RhinoDelayTicks
                    : 0,
                SkeletonKingDelayTicks = monster.HasSkeletonKingDelayTicks
                    ? monster.SkeletonKingDelayTicks
                    : 0,
                HorkDemonDelayTicks = monster.HasHorkDemonDelayTicks
                    ? monster.HorkDemonDelayTicks
                    : 0,
                RangedAvoidanceDelayTicks = monster.HasRangedAvoidanceDelayTicks
                    ? monster.RangedAvoidanceDelayTicks
                    : 0,
                SnakeDelayTicks = monster.HasSnakeDelayTicks
                    ? monster.SnakeDelayTicks
                    : 0,
                CounselorDelayTicks = monster.HasCounselorDelayTicks
                    ? monster.CounselorDelayTicks
                    : 0,
                AvoidanceDelayTicks = monster.HasAvoidanceDelayTicks
                    ? monster.AvoidanceDelayTicks
                    : 0,
                RangedDelayTicks = monster.HasRangedDelayTicks
                    ? monster.RangedDelayTicks
                    : 0,
                MegaDelayTicks = monster.HasMegaDelayTicks
                    ? monster.MegaDelayTicks
                    : 0,
                CounselorRetreatTicks = monster.CounselorRetreatTicks,
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
                QuestProgressAmount = @object.QuestProgressAmount == 0
                    ? 1
                    : @object.QuestProgressAmount,
                MissilePassable = @object.MissilePassable,
                Breakable = @object.Breakable,
                Destroyed = @object.Destroyed,
                TrapArmed = @object.TrapArmed,
                IsShrine = @object.IsShrine,
            }).ToArray(),
            source.Quests.Select(quest => new DiabloQuestState(
                quest.QuestId,
                quest.LevelId,
                quest.Progress,
                quest.RequiredProgress,
                quest.Completed,
                quest.Unavailable,
                !quest.Initial && !quest.Completed && !quest.Unavailable,
                quest.Logged,
                quest.Variable1,
                quest.Variable2,
                quest.MessageId,
                quest.CompletionMonsterId,
                quest.CompletionUniqueIdentity,
                quest.HasTurnInTownerId ? quest.TurnInTownerId : -1,
                quest.TurnInOnCompletion,
                quest.HasTurnInCompletionVariable2 ? quest.TurnInCompletionVariable2 : -1,
                quest.HasTurnInAlternateItemId ? quest.TurnInAlternateItemId : 0,
                quest.HasTurnInAlternateMessageId ? quest.TurnInAlternateMessageId : 0,
                quest.TurnInActivationOnly)).ToArray(),
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
                DamageQ6 = projectile.DamageQ6,
                ElementalFireDamage = projectile.ElementalFireDamage,
                ElementalLightningDamage = projectile.ElementalLightningDamage,
                AttackHit = projectile.AttackHit,
                PerilSelfDamageQ6 = projectile.PerilSelfDamageQ6,
                DoppelgangerProc = projectile.DoppelgangerProc,
                ActivationDelayTicks = projectile.ActivationDelayTicks,
                DamageMinimum = projectile.DamageMinimum,
                DamageMaximum = projectile.DamageMaximum,
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
            Corpses = source.Corpses.Select(corpse => new DiabloCorpseState(
                corpse.EntityId,
                corpse.LevelId,
                corpse.PositionX,
                corpse.PositionY)).ToArray(),
            VictoryTriggered = source.VictoryTriggered,
            Difficulty = FromProtocolDifficulty(source.Difficulty),
        };
    }

    private static void ValidateEntityIds(
        Snapshot snapshot,
        bool allowZeroPlayerEntityId = false)
    {
        var entityIds = new HashSet<uint>();
        foreach (var player in snapshot.Players)
            ValidateEntityId(entityIds, player.EntityId, "player", allowZeroPlayerEntityId);
        foreach (var monster in snapshot.Monsters)
            ValidateEntityId(entityIds, monster.EntityId, "monster");
        foreach (var item in snapshot.WorldItems)
            ValidateEntityId(entityIds, item.EntityId, "world item");
        foreach (var @object in snapshot.Objects)
            ValidateEntityId(entityIds, @object.EntityId, "object");
        foreach (var projectile in snapshot.Projectiles)
            ValidateEntityId(entityIds, projectile.EntityId, "projectile");
        foreach (var corpse in snapshot.Corpses)
            ValidateEntityId(entityIds, corpse.EntityId, "corpse");
    }

    private static void ValidateEntityId(
        HashSet<uint> entityIds,
        uint entityId,
        string entityKind,
        bool allowZero = false)
    {
        if (allowZero && entityKind == "player" && entityId == 0)
            return;
        if (entityId == 0 || !entityIds.Add(entityId))
            throw new InvalidDataException(
                $"The authoritative snapshot contains a duplicate or zero {entityKind} entity ID ({entityId}).");
    }

    private static GameDifficulty ToProtocolDifficulty(DiabloDifficulty difficulty)
    {
        return difficulty switch {
            DiabloDifficulty.Normal => GameDifficulty.Normal,
            DiabloDifficulty.Nightmare => GameDifficulty.Nightmare,
            DiabloDifficulty.Hell => GameDifficulty.Hell,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };
    }

    private static DiabloDifficulty FromProtocolDifficulty(GameDifficulty difficulty)
    {
        return difficulty switch {
            GameDifficulty.Unspecified or GameDifficulty.Normal => DiabloDifficulty.Normal,
            GameDifficulty.Nightmare => DiabloDifficulty.Nightmare,
            GameDifficulty.Hell => DiabloDifficulty.Hell,
            _ => throw new InvalidDataException("The authoritative snapshot difficulty is invalid."),
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
            case DiabloGameplayEventKind.Dialogue:
                batch.Events.Add(new GameEvent {
                    Dialogue = new DialogueEvent {
                        SourceEntityId = gameplayEvent.SourceEntityId,
                        TargetEntityId = gameplayEvent.TargetEntityId,
                        QuestId = gameplayEvent.QuestId,
                        MessageId = gameplayEvent.MessageId,
                    },
                });
                break;
            case DiabloGameplayEventKind.Victory:
                batch.Events.Add(new GameEvent {
                    Victory = new VictoryEvent {
                        PlayerEntityId = entityId,
                        BossEntityId = gameplayEvent.TargetEntityId,
                        DiabloKillLevel = (uint)Math.Max(gameplayEvent.Amount, 0),
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
            BaseLifeMaximum = source.BaseLifeMaximum == 0
                ? source.LifeMaximum
                : source.BaseLifeMaximum,
            BaseManaMaximum = source.BaseManaMaximum == 0
                ? source.ManaMaximum
                : source.BaseManaMaximum,
            CharacterLevel = source.CharacterLevel,
            CharacterClassId = source.CharacterClassId,
            CharacterName = source.CharacterName,
            ManaShieldActive = source.ManaShieldActive,
            ReflectCharges = source.ReflectCharges,
            DiabloKillLevel = source.DiabloKillLevel,
            AbilitySpellId = source.AbilitySpellId,
            AbilityKind = (DiabloPlayerAbilityKind)source.AbilityKind,
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
                    StableItemId = item.StableItemId,
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
                    source.Cast.SourceItem.Slot,
                    source.Cast.SourceItem.StableItemId);
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
            SpellSourceKind = source.SpellSourceKind == ItemSpellSourceKind.Unspecified
                ? null
                : (DiabloItemSpellSourceKind)source.SpellSourceKind,
            ItemActionKind = source.ItemActionKind == ItemActionKind.Unspecified
                ? DiabloItemActionKind.None
                : (DiabloItemActionKind)source.ItemActionKind,
            StableItemId = source.StableItemId,
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
            ManaToLifePercent = source.ManaToLifePercent,
            LifeToManaPercent = source.LifeToManaPercent,
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

    private static ItemStateSnapshot ToProtocolItemState(
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks)
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
            ManaToLifePercent = state.ManaToLifePercent,
            LifeToManaPercent = state.LifeToManaPercent,
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
            SpellSourceKind = (ItemSpellSourceKind)(state.SpellSourceKind
                ?? DiabloCastSourceRules.ClassifyItem(state, spells)),
            ItemActionKind = (ItemActionKind)state.ItemActionKind,
            SpellDisplayName = state.SpellId > 0
                ? ResolveSpellDisplayName(checked((uint)state.SpellId), spells, spellBooks)
                : string.Empty,
            PresentationKind = ResolveItemPresentationKind(state),
            StableItemId = state.StableItemId,
        };
        if (state.NativeCursorGraphic.HasValue)
            snapshot.NativeCursorGraphic = state.NativeCursorGraphic.Value;
        return snapshot;
    }

    private static ItemPresentationKind ResolveItemPresentationKind(
        AuthoritativeItemState state)
    {
        if (state.ItemType == 11 || state.ItemClass == 4)
            return ItemPresentationKind.Gold;
        if (state.MiscId == 44 && state.ItemClass == 3)
            return ItemPresentationKind.Ear;
        // Keep the additive field at its zero value for ordinary items so
        // legacy snapshots and clients remain byte-for-byte compatible.
        return ItemPresentationKind.Unspecified;
    }

    private static ObjectPresentationKind ResolveObjectPresentationKind(
        DiabloObjectState state,
        bool revealTraps)
    {
        if (state.IsShrine)
            return ObjectPresentationKind.Shrine;
        if (state.QuestId != 0
            || state.EffectKind is AuthoritativeObjectEffectKind.QuestSequence
                or AuthoritativeObjectEffectKind.QuestItemCompletion)
            return ObjectPresentationKind.Quest;
        if (state.Breakable)
            return ObjectPresentationKind.Breakable;
        if (revealTraps && state.TrapArmed)
            return ObjectPresentationKind.Trap;
        return ObjectPresentationKind.Unspecified;
    }

    private static string ResolveSpellDisplayName(
        uint spellId,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks)
    {
        if (spells is not null
            && spells.TryGet(spellId, out var spell)
            && !string.IsNullOrWhiteSpace(spell.DisplayName))
            return spell.DisplayName;
        if (spellBooks is not null
            && spellBooks.TryGet(spellId, out var book))
            return book.DisplayName;
        return string.Empty;
    }

    private static ProjectilePresentationKind ResolveProjectilePresentationKind(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeProjectilePhase phase)
    {
        if (behavior == AuthoritativeProjectileBehavior.ResurrectionBeam)
            return ProjectilePresentationKind.Beam;
        if (behavior == AuthoritativeProjectileBehavior.Teleport)
            return ProjectilePresentationKind.Teleport;
        if (behavior == AuthoritativeProjectileBehavior.Phasing)
            return ProjectilePresentationKind.Phasing;
        if (behavior == AuthoritativeProjectileBehavior.StoneCurse)
            return ProjectilePresentationKind.StoneCurse;
        if (behavior == AuthoritativeProjectileBehavior.RageController)
            return ProjectilePresentationKind.Hidden;
        if (behavior is AuthoritativeProjectileBehavior.Flash
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop)
            return ProjectilePresentationKind.Flash;
        if (behavior is AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball)
            return phase == AuthoritativeProjectilePhase.Impact
                ? ProjectilePresentationKind.FireImpact
                : ProjectilePresentationKind.Fire;
        if (behavior is AuthoritativeProjectileBehavior.FireWallController
                or AuthoritativeProjectileBehavior.FireWallSegment
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.RingOfFireController
                or AuthoritativeProjectileBehavior.ImmolationController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.Elemental)
            return ProjectilePresentationKind.Fire;
        if (behavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
            return ProjectilePresentationKind.FireImpact;
        if (behavior == AuthoritativeProjectileBehavior.GuardianController)
            return ProjectilePresentationKind.Guardian;
        if (behavior is AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.ChainLightningController
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaController
                or AuthoritativeProjectileBehavior.NovaBall
                or AuthoritativeProjectileBehavior.LightningWallController
                or AuthoritativeProjectileBehavior.MonsterChargedBolt)
            return ProjectilePresentationKind.Lightning;
        if (behavior == AuthoritativeProjectileBehavior.HolyBolt)
            return ProjectilePresentationKind.Holy;
        if (behavior == AuthoritativeProjectileBehavior.BloodStar)
            return ProjectilePresentationKind.BloodStar;
        if (behavior == AuthoritativeProjectileBehavior.MonsterMagic)
            return ProjectilePresentationKind.MonsterMagic;
        if (behavior == AuthoritativeProjectileBehavior.HorkSpawn)
            return ProjectilePresentationKind.HorkSpawn;
        if (behavior == AuthoritativeProjectileBehavior.BoneSpirit)
            return ProjectilePresentationKind.BoneSpirit;
        if (behavior is AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle)
            return ProjectilePresentationKind.Acid;
        if (behavior is AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow)
            return ProjectilePresentationKind.Arrow;
        return ProjectilePresentationKind.Generic;
    }
}
