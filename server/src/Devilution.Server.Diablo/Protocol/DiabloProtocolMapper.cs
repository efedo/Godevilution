using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Server.Snapshots;
using AuthoritativeMonsterTypeCatalog = Devilution.Diablo.AuthoritativeMonsterTypeCatalog;
using AuthoritativeProjectileBehavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Protocol;

/** Maps transport-neutral Diablo state and events to the versioned wire schema. */
internal static class DiabloProtocolMapper
{
    private const int NativeMaximumPlayerCount = AuthoritativeProtocolLimits.NativeMaximumPlayerCount;
    private const int NativeMaximumInventoryCount = AuthoritativeProtocolLimits.NativeMaximumInventoryCount;
    private const int NativeMaximumEquipmentCount = AuthoritativeProtocolLimits.NativeMaximumEquipmentCount;
    private const int NativeMaximumBeltCount = AuthoritativeProtocolLimits.NativeMaximumBeltCount;
    private const int NativeMaximumInventoryGridCount = AuthoritativeProtocolLimits.NativeMaximumInventoryGridCount;
    private const int NativeInventoryWidth = AuthoritativeProtocolLimits.NativeInventoryWidth;
    private const int NativeInventoryHeight = AuthoritativeProtocolLimits.NativeInventoryHeight;
    private const int NativeMaximumWarpTriggerCount = AuthoritativeProtocolLimits.NativeMaximumWarpTriggerCount;
    private const int MaximumStatusEffectCount = AuthoritativeProtocolLimits.MaximumStatusEffectCount;
    private const int MaximumSnapshotQuestCount = AuthoritativeProtocolLimits.MaximumSnapshotQuestCount;
    private const int MaximumQuestTownerInteractionCount = AuthoritativeProtocolLimits.MaximumQuestTownerInteractionCount;
    private const int MaximumNativeMonsterDeathAnimationTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterDeathAnimationTicks;
    private const uint MaximumNativeMonsterExperienceReward = AuthoritativeProtocolLimits.MaximumNativeMonsterExperienceReward;
    private const int MaximumNativeMonsterDirection = AuthoritativeProtocolLimits.MaximumNativeMonsterDirection;
    private const int MaximumNativeMonsterPackSize = AuthoritativeProtocolLimits.MaximumNativeMonsterPackSize;
    private const int MaximumNativeMonsterActiveTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterActiveTicks;
    private const int MinimumNativeMonsterMovementMode = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementMode;
    private const int MaximumNativeMonsterMovementMode = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementMode;
    private const int MinimumNativeMonsterMovementTurn = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementTurn;
    private const int MaximumNativeMonsterMovementTurn = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTurn;
    private const int MaximumNativeMonsterAiTimerTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks;
    private const int MaximumNativeMonsterShortTimerTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterShortTimerTicks;
    private const int MaximumNativeMonsterRetreatTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterRetreatTicks;
    private const int MaximumNativeMonsterMovementTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks;
    private const int MaximumNativeMonsterFallenStandAnimationFrame = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationFrame;
    private const int MaximumNativeMonsterFallenStandAnimationTick = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationTick;
    private const int MaximumNativeMonsterFallenRallyTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenRallyTicks;
    private const int MinimumNativeMonsterResistance = AuthoritativeProtocolLimits.MinimumNativeMonsterResistance;
    private const int MaximumNativeMonsterResistance = AuthoritativeProtocolLimits.MaximumNativeMonsterResistance;
    private const int MaximumNativeQuestByte = AuthoritativeProtocolLimits.MaximumNativeQuestByte;
    private const int MinimumNativeOptionalQuestByte = AuthoritativeProtocolLimits.MinimumNativeOptionalQuestByte;
    private const int MinimumNativeRewardOffset = AuthoritativeProtocolLimits.MinimumNativeRewardOffset;
    private const int MaximumNativeRewardOffset = AuthoritativeProtocolLimits.MaximumNativeRewardOffset;
    private const int MinimumNativeObjectOffset = AuthoritativeProtocolLimits.MinimumNativeObjectOffset;
    private const int MaximumNativeObjectOffset = AuthoritativeProtocolLimits.MaximumNativeObjectOffset;
    private const int MinimumNativeDirection = AuthoritativeProtocolLimits.MinimumNativeDirection;
    private const int MaximumNativeDirection = AuthoritativeProtocolLimits.MaximumNativeDirection;
    private const uint MaximumNativeCharacterLevel = AuthoritativeProtocolLimits.MaximumNativeCharacterLevel;
    private const int MinimumNativeLightRadius = AuthoritativeProtocolLimits.MinimumNativeLightRadius;
    private const int MaximumNativeLightRadius = AuthoritativeProtocolLimits.MaximumNativeLightRadius;
    private static readonly (int TownerId, int X, int Y, int Direction)[] CanonicalNativeTownResidents = [
        (0, 62, 63, 1),
        (1, 55, 79, 7),
        (3, 55, 62, 1),
        (4, 62, 71, 0),
        (5, 71, 84, 0),
        (6, 80, 20, 0),
        (7, 43, 66, 0),
        (8, 11, 53, 0),
        (9, 58, 16, 1),
        (9, 56, 14, 3),
        (9, 59, 20, 4),
    ];
    private const int MaximumNativeVisitedLevelCount = AuthoritativeProtocolLimits.MaximumNativeVisitedLevelCount;

    public static Snapshot ToProtocolSnapshot(
        DiabloSimulationSnapshot source,
        IReadOnlyList<StorePlayerSnapshot>? participants = null,
        bool revealTraps = true,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeItemCatalog? items = null,
        IReadOnlyList<AuthoritativeLevel>? levelGeometry = null,
        IReadOnlyList<AuthoritativeWarpTarget>? warpTargets = null,
        bool allowZeroPlayerEntityId = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateProjectedStoreState(source);
        var projectedLevels = levelGeometry?.ToDictionary(level => level.LevelId)
            ?? new Dictionary<uint, AuthoritativeLevel>();
        var projectedWarpTargets = (warpTargets ?? []).ToArray();
        ValidateProjectedWorldItemPositions(source.WorldItems, projectedLevels);
        ValidateProjectedObjectPositions(source.Objects, projectedLevels);
        ValidateProjectedQuestStates(source.Quests);
        ValidateProjectedObjectStates(source.Objects, source.Quests);
        ValidateProjectedCathedralDoorCheckpoint(
            source.CathedralDoors,
            source.CathedralDoorObjects);
        var cathedralDoors = ValidateProjectedCathedralDoorObjects(
            source.CathedralDoorObjects,
            source.Objects,
            projectedLevels);
        ValidateProjectedCathedralDoorSpecialLayers(
            cathedralDoors.Values,
            projectedLevels);
        ValidateProjectedProjectileStates(source.Projectiles, projectedLevels);
        ValidateProjectedPortalStates(source.Portals);
        ValidateProjectedWarpTargets(projectedWarpTargets, projectedLevels, source.Quests);
        ValidateProjectedQuestTownerInteractions(
            source.QuestTownerInteractions,
            source.Quests,
            projectedLevels);
        ValidateProjectedTownerGreetings(source.TownerGreetings, projectedLevels);
        ValidateProjectedNativeTownResidents(
            source.NativeTownCatalogFingerprint,
            source.NativeTownResidents,
            source.TownerGreetings,
            projectedLevels);
        ValidateProjectedWorldMutationIds(source.ActiveWorldMutationIds);
        ValidateProjectedMaterializedProceduralLevelIds(
            source.MaterializedProceduralLevelIds);
        ValidateProjectedSetLevelObjectOrigins(
            source.SetLevelObjectOrigins,
            levelGeometry is null ? null : projectedLevels);
        ValidateProjectedProceduralQuestPieceOrigins(
            source.ProceduralQuestPieceOrigins,
            levelGeometry is null ? null : projectedLevels);
        if (source.ActiveStore is not null) {
            var storeSlots = new HashSet<uint>();
            foreach (var item in source.ActiveStore.Items) {
                if (!storeSlots.Add(item.StoreSlot)) {
                    throw new InvalidDataException(
                        "Authoritative active-store stock contains duplicate slots.");
                }
            }
        }
        if (source.WorldItems.Any(item => item.Price > int.MaxValue)
            || source.ActiveStore?.Items.Any(item => item.Price > int.MaxValue) == true) {
            throw new InvalidDataException(
                "Authoritative item price exceeds the shared native protocol range.");
        }
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
        if (projectedParticipants.Length > NativeMaximumPlayerCount)
            throw new InvalidDataException(
                $"Authoritative player state contains more than {NativeMaximumPlayerCount} "
                + "players, exceeding the native player pool capacity.");
        ValidateProjectedPlayerMinionOwners(source.Monsters, projectedParticipants);
        ValidateProjectedPlayerPositions(projectedParticipants, projectedLevels);
        ValidateProjectedMonsterPositions(source.Monsters, projectedLevels);
        ValidateProjectedMonsterStates(source.Monsters);
        ValidateProjectedScavengerCorpseReferences(source.Monsters, source.Corpses);
        ValidateProjectedMonsterPoolCapacity(source.Monsters);
        ValidateProjectedCorpsePositions(source.Corpses, projectedLevels);
        ValidateProjectedPortalPositions(source.Portals, projectedLevels);

        var snapshot = new Snapshot {
            Tick = source.Tick,
            RandomState = source.RandomState,
            VictoryTriggered = source.VictoryTriggered,
            Difficulty = ToProtocolDifficulty(source.Difficulty),
            NativeTownCatalogFingerprint = source.NativeTownCatalogFingerprint,
        };
        snapshot.ActiveWorldMutationIds.Add(source.ActiveWorldMutationIds);
        snapshot.MaterializedProceduralLevelIds.Add(source.MaterializedProceduralLevelIds);
        foreach (var origin in source.SetLevelObjectOrigins.OrderBy(origin => origin.SetLevelId)) {
            snapshot.SetLevelObjectOrigins.Add(new SetLevelObjectOriginSnapshot {
                SetLevelId = origin.SetLevelId,
                LevelId = origin.LevelId,
                PositionX = origin.PositionX,
                PositionY = origin.PositionY,
            });
        }
        foreach (var origin in source.ProceduralQuestPieceOrigins.OrderBy(origin => origin.OriginId)) {
            snapshot.ProceduralQuestPieceOrigins.Add(new ProceduralQuestPieceOriginSnapshot {
                OriginId = origin.OriginId,
                LevelId = origin.LevelId,
                PositionX = origin.PositionX,
                PositionY = origin.PositionY,
            });
        }
        foreach (var participant in projectedParticipants)
            snapshot.Players.Add(
                ToProtocolPlayer(
                    participant,
                    source.Tick,
                    spells,
                    spellBooks,
                    items,
                    projectedLevels));

        foreach (var monster in source.Monsters) {
            var projectedMonster = new MonsterSnapshot {
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
                MovementStartedTick = monster.MovementStartedTick,
                MovementEffectTick = monster.MovementEffectTick,
                Direction = monster.Direction,
                DialogueMessageId = monster.DialogueMessageId,
                DialogueState = (MonsterDialogueState)monster.DialogueState,
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
                FallenStandAnimationFrame = monster.FallenStandAnimationFrame,
                FallenStandAnimationTick = monster.FallenStandAnimationTick,
                FallenRallyTicks = monster.FallenRallyTicks,
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
            };
            if (monster.MonsterItemSeed != 0)
                projectedMonster.MonsterItemSeed = monster.MonsterItemSeed;
            // Omit the additive Normal defaults to preserve legacy save bytes.
            if (monster.HorkDemonMoveTicks != -1 || monster.HorkDemonMoveTurn != 0) {
                projectedMonster.HorkDemonMoveTicks = monster.HorkDemonMoveTicks;
                projectedMonster.HorkDemonMoveTurn = monster.HorkDemonMoveTurn;
            }
            if (monster.MonsterAiSeed != 0)
                projectedMonster.MonsterAiSeed = monster.MonsterAiSeed;
            if (monster.NativeInitializationPresent)
                projectedMonster.NativeInitializationPresent = true;
            if (monster.LeaderEntityId != 0)
                projectedMonster.LeaderEntityId = monster.LeaderEntityId;
            if (monster.LeaderRelation != AuthoritativeMonsterLeaderRelation.None)
                projectedMonster.LeaderRelation = (int)monster.LeaderRelation;
            if (monster.PackSize != 0)
                projectedMonster.PackSize = monster.PackSize;
            if (monster.ActiveForTicks != 0)
                projectedMonster.ActiveForTicks = monster.ActiveForTicks;
            snapshot.Monsters.Add(projectedMonster);
        }
        foreach (var item in source.WorldItems) {
            snapshot.WorldItems.Add(new WorldItemSnapshot {
                EntityId = item.EntityId,
                LevelId = item.LevelId,
                PositionX = item.PositionX,
                PositionY = item.PositionY,
                ItemSeed = item.ItemSeed,
                Price = item.Price,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        foreach (var @object in source.Objects) {
            var projectedObject = new ObjectSnapshot {
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
                QuestTransitionKind = (int)@object.QuestTransitionKind,
                QuestTransitionAmount = @object.QuestTransitionAmount,
                QuestTransitionRequiredVariable1 = @object.QuestTransitionRequiredVariable1,
                QuestTransitionRequiredVariable2 = @object.QuestTransitionRequiredVariable2,
                RewardKind = (int)@object.RewardKind,
                RewardId = @object.RewardId,
                RewardItemLevel = @object.RewardItemLevel,
                RewardOffsetX = @object.RewardOffsetX,
                RewardOffsetY = @object.RewardOffsetY,
                RewardOriginKind = (int)@object.RewardOriginKind,
                RewardSetLevelId = @object.RewardSetLevelId < 0 ? 0 : @object.RewardSetLevelId,
                ProjectileTargetOffsetX = @object.ProjectileTargetOffsetX,
                ProjectileTargetOffsetY = @object.ProjectileTargetOffsetY,
                QuestSequenceFollowUpKind = (int)@object.QuestSequenceFollowUpKind,
                QuestSequenceFollowUpAmount = @object.QuestSequenceFollowUpAmount,
                QuestSequenceFollowUpOffsetX = @object.QuestSequenceFollowUpOffsetX,
                QuestSequenceFollowUpOffsetY = @object.QuestSequenceFollowUpOffsetY,
                ProjectileSourceKind = (int)@object.ProjectileSourceKind,
                NativeObjectId = @object.NativeObjectId,
                NativeChestInteractable = source.NativeChestInteractableEntityIds.Contains(@object.EntityId),
                NativeSarcophagusInteractable = source.NativeSarcophagusInteractableEntityIds.Contains(@object.EntityId),
                NativeBarrelMeleeBreakable = source.NativeBarrelMeleeBreakableEntityIds.Contains(@object.EntityId),
                NativeTrapInteractable = revealTraps
                    && source.NativeTrapInteractableEntityIds.Contains(@object.EntityId),
                KeepVisibleAfterActivation = !@object.HideWhenActivated,
                AnimationFrame = checked((uint)@object.AnimationFrame),
                ActivatedAnimationFrame = checked((uint)@object.ActivatedAnimationFrame),
                DialogueMessageId = @object.DialogueMessageId,
                ActivateOnOccupancy = @object.ActivateOnOccupancy,
                PresentationKind = ResolveObjectPresentationKind(@object, revealTraps),
            };
            if (cathedralDoors.TryGetValue(@object.EntityId, out var cathedralDoor))
                projectedObject.CathedralDoor = ToProtocolCathedralDoor(cathedralDoor);
            snapshot.Objects.Add(projectedObject);
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
            if (quest.TurnInMessageId != 0)
                projectedQuest.TurnInMessageId = quest.TurnInMessageId;
            projectedQuest.TurnInDialogueOnly = quest.TurnInDialogueOnly;
            if (quest.TurnInCompletionRequiredVariable1 >= 0)
                projectedQuest.TurnInCompletionRequiredVariable1 = quest.TurnInCompletionRequiredVariable1;
            if (quest.TurnInCompletionVariable1 >= 0)
                projectedQuest.TurnInCompletionVariable1 = quest.TurnInCompletionVariable1;
            if (quest.TurnInRequiredVariable1 >= 0)
                projectedQuest.TurnInRequiredVariable1 = quest.TurnInRequiredVariable1;
            if (quest.TurnInVariable1 >= 0)
                projectedQuest.TurnInVariable1 = quest.TurnInVariable1;
            projectedQuest.TurnInProgressOnly = quest.TurnInProgressOnly;
            if (quest.NativeQuestId.HasValue)
                projectedQuest.NativeQuestId = quest.NativeQuestId.Value;
            if (quest.TurnInBlockedVisitedLevel != 0)
                projectedQuest.TurnInBlockedVisitedLevel = quest.TurnInBlockedVisitedLevel;
            snapshot.Quests.Add(projectedQuest);
        }
        foreach (var interaction in source.QuestTownerInteractions) {
            var projectedInteraction = new QuestTownerInteractionSnapshot {
                InteractionId = interaction.InteractionId,
                QuestId = interaction.QuestId,
                TownerId = interaction.TownerId,
                LevelId = interaction.LevelId,
                PositionX = interaction.PositionX,
                PositionY = interaction.PositionY,
                ItemId = interaction.ItemId,
                Kind = (int)interaction.Kind,
                ConsumeItem = interaction.ConsumeItem,
                AllowCompleted = interaction.AllowCompleted,
                RequiredVisitedLevel1 = interaction.RequiredVisitedLevel1,
                RequiredVisitedLevel2 = interaction.RequiredVisitedLevel2,
                RequireUnlogged = interaction.RequireUnlogged,
                RequiredItemId = interaction.RequiredItemId,
                RequireItemPresent = interaction.RequireItemPresent,
                AllowInitial = interaction.AllowInitial,
                UseQuestTurnInReward = interaction.UseQuestTurnInReward,
                UseQuestCompletionReward = interaction.UseQuestCompletionReward,
            };
            if (interaction.RequiredVariable1 >= 0)
                projectedInteraction.RequiredVariable1 = interaction.RequiredVariable1;
            if (interaction.RequiredVariable2 >= 0)
                projectedInteraction.RequiredVariable2 = interaction.RequiredVariable2;
            if (interaction.Variable1 >= 0)
                projectedInteraction.Variable1 = interaction.Variable1;
            if (interaction.Variable2 >= 0)
                projectedInteraction.Variable2 = interaction.Variable2;
            if (interaction.MessageId != 0)
                projectedInteraction.MessageId = interaction.MessageId;
            snapshot.QuestTownerInteractions.Add(projectedInteraction);
        }
        foreach (var greeting in source.TownerGreetings.OrderBy(greeting => greeting.TownerId)) {
            snapshot.TownerGreetings.Add(new TownerGreetingSnapshot {
                TownerId = greeting.TownerId,
                LevelId = greeting.LevelId,
                PositionX = greeting.PositionX,
                PositionY = greeting.PositionY,
                MessageId = greeting.MessageId,
            });
        }
        foreach (var resident in source.NativeTownResidents) {
            snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot {
                ResidentSlot = resident.ResidentSlot,
                TownerId = resident.TownerId,
                LevelId = resident.LevelId,
                PositionX = resident.PositionX,
                PositionY = resident.PositionY,
                NativeDirection = resident.NativeDirection,
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
                DamageQ6 = projectile.DamageQ6,
                ElementalFireDamage = projectile.ElementalFireDamage,
                ElementalLightningDamage = projectile.ElementalLightningDamage,
                AttackHit = projectile.AttackHit,
                PerilSelfDamageQ6 = projectile.PerilSelfDamageQ6,
                DoppelgangerProc = projectile.DoppelgangerProc,
                ActivationDelayTicks = projectile.ActivationDelayTicks,
                DamageMinimum = projectile.DamageMinimum,
                DamageMaximum = projectile.DamageMaximum,
                NativeVisual = ResolveNativeProjectileVisual(
                    projectile.Behavior,
                    projectile.DamageType,
                    projectile.NativeMissileId),
                NativeMissileId = projectile.NativeMissileId ?? string.Empty,
                PresentationKind = ResolveProjectilePresentationKind(
                    projectile.Behavior,
                    projectile.Phase,
                    projectile.DamageType),
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
            geometry.StaticObjectBlockedCells.Add(level.StaticObjectBlockedCells);
            geometry.MissileBlockedCells.Add(level.MissileBlockedCells);
            geometry.NativeTransparency.Add(level.NativeTransparency.Select(value => (uint)value));
            geometry.NativeObjectLayer.Add(level.NativeObjectLayer.Select(value => (uint)value));
            geometry.NativeMegaTileLayer.Add(level.NativeMegaTileLayer.Select(value => (uint)value));
            geometry.NativePieceLayer.Add(level.NativePieceLayer.Select(value => (uint)value));
            geometry.NativeVisionBlockedCells.Add(level.NativeVisionBlockedCells);
            geometry.NativeMonsterLayer.Add(level.NativeMonsterLayer.Select(value => (uint)value));
            geometry.NativeSpecialLayer.Add(level.NativeSpecialLayer.Select(value => (uint)value));
            foreach (var warp in projectedWarpTargets
                         .Where(warp => warp.SourceLevelId == level.LevelId)
                         .OrderBy(warp => warp.WarpId)) {
                geometry.WarpTriggers.Add(new WarpTriggerSnapshot {
                    WarpId = warp.WarpId,
                    TriggerX = warp.TriggerX,
                    TriggerY = warp.TriggerY,
                    TargetLevelId = warp.TargetLevelId,
                    TargetX = warp.TargetX,
                    TargetY = warp.TargetY,
                    RequiredQuestId = warp.RequiredQuestId,
                    RequiredQuestCompleted = warp.RequiredQuestCompleted,
                    StableSymbol = warp.StableSymbol,
                    Available = IsWarpAvailable(warp, source.Quests),
                });
            }
            snapshot.LevelGeometry.Add(geometry);
        }
        if (source.ActiveStore is not null) {
            var store = new StoreSnapshot { StoreId = source.ActiveStore.StoreId };
            foreach (var item in source.ActiveStore.Items) {
                store.Items.Add(new StoreItemSnapshot {
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
                });
            }
            if (source.ActiveStore.Services is { } services) {
                store.Services = new StoreServicesSnapshot {
                    Repair = services.Repair,
                    Recharge = services.Recharge,
                    Identify = services.Identify,
                    RefillMana = services.RefillMana,
                };
            }
            if (source.ActiveStore.Quotes is { } quotes) {
                var projectedQuotes = new StoreServiceQuotesSnapshot {
                    RefillMana = quotes.RefillMana is null
                        ? null
                        : ToProtocolStoreServiceQuote(quotes.RefillMana),
                };
                foreach (var item in quotes.Items) {
                    projectedQuotes.Items.Add(new StoreItemServiceQuotes {
                        Item = ToProtocolItemReference(item.Item),
                        Repair = item.Repair is null
                            ? null
                            : ToProtocolStoreServiceQuote(item.Repair),
                        Recharge = item.Recharge is null
                            ? null
                            : ToProtocolStoreServiceQuote(item.Recharge),
                        Identify = item.Identify is null
                            ? null
                            : ToProtocolStoreServiceQuote(item.Identify),
                    });
                }
                store.Quotes = projectedQuotes;
            }
            snapshot.ActiveStore = store;
        }

        ValidateEntityIds(snapshot, allowZeroPlayerEntityId);
        snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
        return snapshot;
    }

    private static PlayerItemReference ToProtocolItemReference(
        DiabloPlayerItemReference reference)
    {
        var location = reference.Location switch {
            DiabloPlayerItemLocation.Inventory => PlayerItemLocation.Inventory,
            DiabloPlayerItemLocation.Belt => PlayerItemLocation.Belt,
            DiabloPlayerItemLocation.Equipment => PlayerItemLocation.Equipment,
            DiabloPlayerItemLocation.Stash => PlayerItemLocation.Stash,
            _ => throw new ArgumentOutOfRangeException(nameof(reference)),
        };
        return new PlayerItemReference {
            Location = location,
            Slot = reference.Slot,
            StableItemId = reference.StableItemId,
            Page = reference.Page,
        };
    }

    private static Devilution.Protocol.V1.StoreServicePriceQuote ToProtocolStoreServiceQuote(
        DiabloStoreServiceQuote quote)
    {
        var rejectReason = quote.RejectReason switch {
            null => CommandRejectReason.Unspecified,
            DiabloRuleFailure.InvalidTarget => CommandRejectReason.InvalidTarget,
            DiabloRuleFailure.NotAllowed => CommandRejectReason.NotAllowed,
            DiabloRuleFailure.InsufficientResources => CommandRejectReason.InsufficientResources,
            DiabloRuleFailure.Malformed => CommandRejectReason.Malformed,
            DiabloRuleFailure.TooLate => CommandRejectReason.TooLate,
            _ => throw new ArgumentOutOfRangeException(nameof(quote)),
        };
        return new Devilution.Protocol.V1.StoreServicePriceQuote {
            Price = quote.Price,
            RejectReason = rejectReason,
        };
    }

    private static void ValidateProjectedStoreState(DiabloSimulationSnapshot source)
    {
        var activeStoreId = source.Player.ActiveStoreId;
        if ((source.ActiveStore is null) != (activeStoreId is null)
            || source.ActiveStore is not null
                && (source.ActiveStore.StoreId == 0
                    || source.ActiveStore.StoreId != activeStoreId)) {
            throw new InvalidDataException(
                "Authoritative active-store state does not match the local player projection.");
        }
    }

    private static void ValidateProjectedWorldItemPositions(
        IReadOnlyList<DiabloWorldItemState> worldItems,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var group in worldItems.GroupBy(item => item.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumItemCount)
                throw new InvalidDataException(
                    $"Authoritative world-item state for level {group.Key} exceeds the native "
                    + $"{AuthoritativeLevel.NativeMaximumItemCount}-item capacity.");
        }
        foreach (var item in worldItems) {
            ValidateProjectedEntityPosition(
                item.LevelId,
                item.PositionX,
                item.PositionY,
                levels,
                "world-item");
        }
    }

    private static void ValidateProjectedObjectPositions(
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var group in objects.GroupBy(@object => @object.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"Authoritative object state for level {group.Key} exceeds the native "
                    + $"{AuthoritativeLevel.NativeMaximumObjectCount}-object capacity.");
        }
        foreach (var @object in objects) {
            ValidateProjectedEntityPosition(
                @object.LevelId,
                @object.PositionX,
                @object.PositionY,
                levels,
                "object");
        }
    }

    private static void ValidateProjectedObjectStates(
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyList<DiabloQuestState> quests)
    {
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var @object in objects) {
            var transitionKind = @object.QuestTransitionKind;
            var rewardKind = @object.RewardKind;
            var followUpKind = @object.QuestSequenceFollowUpKind;
            var effectKind = @object.EffectKind;
            var transitionSetsVariable = transitionKind is
                AuthoritativeObjectQuestTransitionKind.SetVariable1
                or AuthoritativeObjectQuestTransitionKind.SetVariable2
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable2
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2;
            var transitionValid = transitionKind == AuthoritativeObjectQuestTransitionKind.None
                || @object.QuestId != 0
                    && (transitionSetsVariable
                        ? @object.QuestTransitionAmount > 0
                        : @object.QuestTransitionAmount == 0);
            var rewardValid = rewardKind == AuthoritativeObjectRewardKind.None
                ? @object.RewardId == 0
                    && @object.RewardItemLevel == 0
                    && @object.RewardOffsetX == 0
                    && @object.RewardOffsetY == 0
                    && @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.Object
                    && @object.RewardSetLevelId == -1
                : @object.RewardId != 0;
            var producesProjectile = effectKind is
                    AuthoritativeObjectEffectKind.SpawnSpellProjectile
                    or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile;
            var followUpValid = followUpKind switch {
                AuthoritativeQuestSequenceFollowUpKind.None
                    => @object.QuestSequenceFollowUpAmount == 0
                        && @object.QuestSequenceFollowUpOffsetX == 0
                        && @object.QuestSequenceFollowUpOffsetY == 0,
                AuthoritativeQuestSequenceFollowUpKind.WorldMutation
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0
                        && @object.QuestSequenceFollowUpOffsetX == 0
                        && @object.QuestSequenceFollowUpOffsetY == 0,
                AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || effectKind == AuthoritativeObjectEffectKind.WorldMutation
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0,
                AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0,
                _ => false,
            };

            if (@object.ObjectId == 0
                || @object.EffectAmount < 0
                || effectKind is < AuthoritativeObjectEffectKind.None
                    or > AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || @object.QuestId != 0 && !questIds.Contains(@object.QuestId)
                || transitionKind is < AuthoritativeObjectQuestTransitionKind.None
                    or > AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2
                || @object.QuestTransitionAmount is < 0 or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !transitionValid
                || rewardKind is < AuthoritativeObjectRewardKind.None
                    or > AuthoritativeObjectRewardKind.UniqueItem
                || @object.RewardItemLevel is < 0 or > MaximumNativeQuestByte
                || @object.RewardOffsetX is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOffsetY is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOriginKind is < AuthoritativeObjectRewardOriginKind.Object
                    or > AuthoritativeObjectRewardOriginKind.SetLevelOrigin
                || @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.Object
                    && @object.RewardSetLevelId != -1
                || @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.SetLevelOrigin
                    && @object.RewardSetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId
                || !rewardValid
                || @object.ProjectileTargetOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.ProjectileTargetOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !producesProjectile
                    && (@object.ProjectileTargetOffsetX != 0
                        || @object.ProjectileTargetOffsetY != 0)
                || followUpKind is < AuthoritativeQuestSequenceFollowUpKind.None
                    or > AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                || @object.QuestSequenceFollowUpOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.QuestSequenceFollowUpOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !followUpValid
                || @object.ProjectileSourceKind is < AuthoritativeObjectProjectileSourceKind.Object
                    or > AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
                || !producesProjectile && @object.ProjectileSourceKind
                    != AuthoritativeObjectProjectileSourceKind.Object
                || @object.NativeObjectId > AuthoritativeLevel.NativeMaximumObjectId
                || !Enum.IsDefined(@object.PresentationKind)
                || @object.DialogueMessageId is < 0 or > short.MaxValue
                || !AuthoritativeWorldObject.IsValidPresentation(
                    @object.HideWhenActivated,
                    @object.AnimationFrame,
                    @object.ActivatedAnimationFrame)
                || @object.Breakable && @object.MissilePassable
                || @object.Destroyed && !@object.Breakable) {
                throw new InvalidDataException(
                    "Authoritative object state is outside the shared protocol range.");
            }
        }
    }

    /** Validates the optional typed Cathedral door projection without changing the legacy object shape. */
    private static Dictionary<uint, DiabloCathedralDoorObjectState> ValidateProjectedCathedralDoorObjects(
        IReadOnlyList<DiabloCathedralDoorObjectState> doors,
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(doors);
        var objectByEntity = new Dictionary<uint, DiabloObjectState>();
        foreach (var @object in objects) {
            if (!objectByEntity.TryAdd(@object.EntityId, @object)) {
                throw new InvalidDataException(
                    "Authoritative object projection contains duplicate entity IDs.");
            }
        }
        var byEntity = new Dictionary<uint, DiabloCathedralDoorObjectState>();
        foreach (var door in doors) {
            if (door is null || door.EntityId == 0 || !byEntity.TryAdd(door.EntityId, door)
                || !objectByEntity.TryGetValue(door.EntityId, out var @object)) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door projection contains an unknown or duplicate entity ID.");
            }
            ValidateCathedralDoorFields(door, levels);
            if (door.LevelId != @object.LevelId
                || door.PositionX != @object.PositionX
                || door.PositionY != @object.PositionY
                || door.NativeObjectId != @object.NativeObjectId
                || @object.Activated
                || @object.Destroyed
                || @object.EffectKind != AuthoritativeObjectEffectKind.None
                || @object.QuestId != 0
                || @object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
                || @object.RewardKind != AuthoritativeObjectRewardKind.None
                || @object.QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
                || @object.DialogueMessageId != 0
                || @object.Breakable
                || @object.IsShrine
                || @object.ActivateOnOccupancy
                || @object.HideWhenActivated
                || @object.AnimationFrame != door.AnimationFrame
                || @object.MissilePassable != door.MissilePassable) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door projection conflicts with its object metadata.");
            }
        }

        return byEntity;
    }

    private static void ValidateProjectedCathedralDoorCheckpoint(
        DiabloCathedralDoorCheckpoint? checkpoint,
        IReadOnlyList<DiabloCathedralDoorObjectState> doors)
    {
        if (checkpoint is null)
            return;

        checkpoint.ValidateShape();
        var checkpointDoors = checkpoint.DoorLevels
            .SelectMany(level => level.Doors)
            .ToArray();
        if (checkpointDoors.Length != doors.Count
            || checkpointDoors.Length != checkpointDoors.Distinct().Count()
            || doors.Count != doors.Distinct().Count()
            || !checkpointDoors.ToHashSet().SetEquals(doors)) {
            throw new InvalidDataException(
                "Authoritative Cathedral door checkpoint does not match its detached object projection.");
        }
    }

    private static void ValidateProjectedCathedralDoorSpecialLayers(
        IEnumerable<DiabloCathedralDoorObjectState> doors,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var door in doors) {
            if (!levels.TryGetValue(door.LevelId, out var level)
                || level.NativeSpecialLayer.Count == 0
                || door.PositionX < 0
                || door.PositionY < 0
                || door.PositionX >= level.Width
                || door.PositionY >= level.Height) {
                continue;
            }
            var cell = checked(door.PositionY * level.Width + door.PositionX);
            var expected = door.DoorState == 0
                ? 0
                : door.NativeObjectId == 1 ? 7 : 8;
            if (level.NativeSpecialLayer[cell] != expected) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door conflicts with its projected native special layer.");
            }
        }
    }

    private static void ValidateCathedralDoorFields(
        DiabloCathedralDoorObjectState door,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        ValidateProjectedEntityPosition(
            door.LevelId,
            door.PositionX,
            door.PositionY,
            levels,
            "Cathedral door");
        var closed = door.DoorState == 0;
        var baselineFrame = door.NativeObjectId == 1 ? 1 : 2;
        if (door.NativeObjectId is < 1 or > 2
            || door.DoorState is < 0 or > 2
            || door.CachedPiece is <= 0
            || door.CachedPiece > checked((int)AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1)
            || door.CachedNeighborPiece is <= 0
            || door.CachedNeighborPiece > checked((int)AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1)
            || door.AnimationFrame is < 0 or > byte.MaxValue
            || door.AnimationFrame < baselineFrame
            || (door.AnimationFrame - baselineFrame) % 2 != 0
            || closed && door.AnimationFrame > 253
            || !closed && door.AnimationFrame < baselineFrame + 2
            || door.SelectionRegion != (closed ? 3 : 2)
            || door.PreDraw != !closed
            || door.MissilePassable != !closed
            || door.Solid) {
            throw new InvalidDataException(
                "Authoritative Cathedral door projection contains invalid native fields.");
        }
    }

    private static CathedralDoorSnapshot ToProtocolCathedralDoor(
        DiabloCathedralDoorObjectState door)
        => new() {
            EntityId = door.EntityId,
            LevelId = door.LevelId,
            PositionX = door.PositionX,
            PositionY = door.PositionY,
            NativeObjectId = checked((uint)door.NativeObjectId),
            DoorState = door.DoorState,
            CachedPiece = checked((uint)door.CachedPiece),
            CachedNeighborPiece = checked((uint)door.CachedNeighborPiece),
            AnimationFrame = checked((uint)door.AnimationFrame),
            SelectionRegion = checked((uint)door.SelectionRegion),
            PreDraw = door.PreDraw,
            MissilePassable = door.MissilePassable,
            Solid = door.Solid,
        };

    private static void ValidateProjectedQuestStates(
        IReadOnlyList<DiabloQuestState> quests)
    {
        if (quests.Count > MaximumSnapshotQuestCount) {
            throw new InvalidDataException(
                "Authoritative quest state exceeds the shared protocol collection limit.");
        }

        var questIds = new HashSet<uint>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var quest in quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
                || quest.NativeQuestId.HasValue
                    && (quest.NativeQuestId.Value > AuthoritativeQuestTypeCatalog.MaximumNativeQuestId
                        || !nativeQuestIds.Add(quest.NativeQuestId.Value))
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || quest.Unavailable && (quest.Completed || quest.Progress != 0)
                || quest.Variable1 is < 0 or > MaximumNativeQuestByte
                || quest.Variable2 is < 0 or > MaximumNativeQuestByte
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !NativeTextLimits.IsIdentityWithinLimit(quest.CompletionUniqueIdentity)) {
                throw new InvalidDataException(
                    "Authoritative quest state is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedPortalStates(
        IReadOnlyList<DiabloPortalState> portals)
    {
        if (portals.Count > AuthoritativePortalRules.NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"Authoritative portal state exceeds the native {AuthoritativePortalRules.NativeMaximumPortalCount}-portal capacity.");
        var portalIds = new HashSet<uint>();
        foreach (var portal in portals) {
            if (portal.PortalId == 0
                || !portalIds.Add(portal.PortalId)
                || portal.Bidirectional
                    && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0)
                || !portal.Bidirectional
                    && (portal.OwnerEntityId != 0 || portal.RemainingTicks != 0)) {
                throw new InvalidDataException(
                    "Authoritative portal state is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedWarpTargets(
        IReadOnlyList<AuthoritativeWarpTarget> warpTargets,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        IReadOnlyList<DiabloQuestState> quests)
    {
        foreach (var group in warpTargets.GroupBy(warp => warp.SourceLevelId)) {
            if (group.Count() > NativeMaximumWarpTriggerCount)
                throw new InvalidDataException(
                    $"Authoritative warp targets contain more than {NativeMaximumWarpTriggerCount} "
                    + $"triggers on level {group.Key}, exceeding the native trigger capacity.");
        }
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        var warpIds = new HashSet<uint>();
        foreach (var warp in warpTargets) {
            var hasSourceLevel = levels.TryGetValue(warp.SourceLevelId, out var sourceLevel);
            if (warp.WarpId == 0
                || !warpIds.Add(warp.WarpId)
                || !hasSourceLevel
                || warp.TriggerX < 0
                || warp.TriggerY < 0
                || warp.TriggerX >= sourceLevel!.Width
                || warp.TriggerY >= sourceLevel.Height
                || !levels.TryGetValue(warp.TargetLevelId, out var targetLevel)
                || warp.TargetX < 0
                || warp.TargetY < 0
                || warp.TargetX >= targetLevel.Width
                || warp.TargetY >= targetLevel.Height
                || warp.RequiredQuestId != 0
                    && !questIds.Contains(warp.RequiredQuestId)
                || warp.RequiredQuestId == 0 && warp.RequiredQuestCompleted
                || warp.StableSymbol.Any(character => character is < (char)0x21 or > (char)0x7e)) {
                throw new InvalidDataException(
                    "Authoritative warp target is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedQuestTownerInteractions(
        IReadOnlyList<AuthoritativeQuestTownerInteractionDefinition> interactions,
        IReadOnlyList<DiabloQuestState> quests,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        if (interactions.Count > MaximumQuestTownerInteractionCount) {
            throw new InvalidDataException(
                "Authoritative quest/towner interactions exceed the shared protocol limit.");
        }

        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        var interactionIds = new HashSet<uint>();
        foreach (var interaction in interactions) {
            var hasLevel = levels.TryGetValue(interaction.LevelId, out var level);
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
                || !questIds.Contains(interaction.QuestId)
                || levels.Count > 0
                    && (!hasLevel
                        || interaction.PositionX >= level!.Width
                        || interaction.PositionY >= level.Height)
                || interaction.TownerId is < 0 or > MaximumNativeQuestByte
                || interaction.LevelId > ushort.MaxValue
                || interaction.RequiredVisitedLevel1 > ushort.MaxValue
                || interaction.RequiredVisitedLevel2 > ushort.MaxValue
                || interaction.RequiredItemId > ushort.MaxValue
                || interaction.PositionX < 0
                || interaction.PositionY < 0
                || interaction.Kind is < AuthoritativeQuestTownerInteractionKind.Activate
                    or > AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest
                || interaction.MessageId is <= 0 or > short.MaxValue
                || interaction.RequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.RequiredVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.Variable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.Variable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte) {
                throw new InvalidDataException(
                    "Authoritative quest/towner interaction is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedWorldMutationIds(
        IReadOnlyList<uint> mutationIds)
    {
        if (mutationIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || mutationIds.Any(mutationId => mutationId == 0)
            || mutationIds.Distinct().Count() != mutationIds.Count) {
            throw new InvalidDataException(
                "Authoritative active world mutations contain invalid or duplicate identities.");
        }
    }

    private static void ValidateProjectedMaterializedProceduralLevelIds(
        IReadOnlyList<uint> levelIds)
    {
        if (levelIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || levelIds.Distinct().Count() != levelIds.Count) {
            throw new InvalidDataException(
                "Authoritative materialized procedural levels contain duplicate identities.");
        }
    }

    private static void ValidateProjectedMaterializedProceduralLevelIds(
        Google.Protobuf.Collections.RepeatedField<uint> levelIds)
    {
        if (levelIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || levelIds.Distinct().Count() != levelIds.Count) {
            throw new InvalidDataException(
                "Authoritative materialized procedural levels contain duplicate identities.");
        }
    }

    private static void ValidateProjectedSetLevelObjectOrigins(
        IReadOnlyList<DiabloSetLevelObjectOriginState> origins,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels = null)
    {
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Any(origin => origin.SetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId)
            || origins.Select(origin => origin.SetLevelId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative set-level object origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative set-level object origin {origin.SetLevelId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedSetLevelObjectOrigins(
        Google.Protobuf.Collections.RepeatedField<SetLevelObjectOriginSnapshot> origins,
        IReadOnlyDictionary<uint, LevelGeometrySnapshot>? levels = null)
    {
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Any(origin => origin.SetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId)
            || origins.Select(origin => origin.SetLevelId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative set-level object origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || level.Width <= 0
                || level.Height <= 0
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative set-level object origin {origin.SetLevelId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedActiveStoreSlots(
        IEnumerable<uint> storeSlots)
    {
        var uniqueSlots = new HashSet<uint>();
        foreach (var storeSlot in storeSlots) {
            if (!uniqueSlots.Add(storeSlot)) {
                throw new InvalidDataException(
                    "Authoritative active-store stock contains duplicate slots.");
            }
        }
    }

    private static void ValidateProjectedEntityPosition(
        uint levelId,
        int positionX,
        int positionY,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        string entityKind)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(
                positionX,
                positionY)
            || levels.TryGetValue(levelId, out var level)
                && (positionX >= level.Width || positionY >= level.Height)) {
            throw new InvalidDataException(
                $"Authoritative {entityKind} position is outside the projected level geometry.");
        }
    }

    private static void ValidateProjectedProjectileStates(
        IReadOnlyList<DiabloProjectileState> projectiles,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var projectile in projectiles) {
            if (projectile.NativeMissileId is not null
                && (projectile.Behavior != AuthoritativeProjectileBehavior.MonsterMagic
                    || ResolveNativeProjectileVisual(
                        projectile.Behavior,
                        projectile.DamageType,
                        projectile.NativeMissileId) == NativeProjectileVisual.Unspecified)) {
                throw new InvalidDataException(
                    "Authoritative projectile contains an unknown or misplaced native missile identity.");
            }

            ValidateProjectedProjectilePosition(
                projectile,
                projectile.PositionX,
                projectile.PositionY,
                levels,
                "projectile");
            ValidateProjectedProjectilePosition(
                projectile,
                projectile.TargetX,
                projectile.TargetY,
                levels,
                "projectile target");
            if (projectile.SourceEntityId == 0
                || projectile.SpellId == 0
                    && projectile.Behavior is not (AuthoritativeProjectileBehavior.WeaponArrow
                        or AuthoritativeProjectileBehavior.MonsterArrow)
                || projectile.RemainingTicks == 0
                || projectile.Damage < 0
                || projectile.AreaRadius < 0
                || projectile.DamageMinimum < 0
                || projectile.DamageMaximum < projectile.DamageMinimum
                || projectile.DamageQ6 < 0
                || projectile.ElementalFireDamage < 0
                || projectile.ElementalLightningDamage < 0
                || projectile.PerilSelfDamageQ6 < 0
                || projectile.EffectiveSpellLevel < 0
                || projectile.DamageType is < AuthoritativeDamageType.Physical
                    or > AuthoritativeDamageType.Acid
                || projectile.Behavior is < AuthoritativeProjectileBehavior.Targeted
                    or > AuthoritativeProjectileBehavior.MonsterArrow
                || projectile.Phase is < AuthoritativeProjectilePhase.Travel
                    or > AuthoritativeProjectilePhase.Impact
                || UsesDirectionalPresentation(projectile)
                    && projectile.OriginalDirection is < 0 or > MaximumNativeMonsterDirection) {
                throw new InvalidDataException(
                    "Authoritative projectile state is outside the shared protocol range.");
            }
        }
    }

    private static bool UsesDirectionalPresentation(DiabloProjectileState projectile)
    {
        if (!string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;

        return projectile.Behavior is
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
                or AuthoritativeProjectileBehavior.ResurrectionBeam
                or AuthoritativeProjectileBehavior.Phasing
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.HolyBolt
                or AuthoritativeProjectileBehavior.BloodStar
                or AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.FireWallSegment
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaBall
                or AuthoritativeProjectileBehavior.ApocalypseBoom
                or AuthoritativeProjectileBehavior.Elemental
                or AuthoritativeProjectileBehavior.GuardianController
                or AuthoritativeProjectileBehavior.Etherealize
                or AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.HorkSpawn
                or AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;
    }

    private static void ValidateProjectedPlayerPositions(
        IEnumerable<StorePlayerSnapshot> players,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var player in players) {
            ValidateProjectedEntityPosition(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                levels,
                "player");
        }
    }

    private static void ValidateProjectedMonsterPositions(
        IReadOnlyList<DiabloMonsterState> monsters,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var monster in monsters) {
            ValidateProjectedEntityPosition(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                levels,
                "monster");
            if (monster.Moving) {
                ValidateProjectedEntityPosition(
                    monster.LevelId,
                    monster.FuturePositionX,
                    monster.FuturePositionY,
                    levels,
                    "monster future");
            }
        }
    }

    private static void ValidateProjectedMonsterStates(
        IReadOnlyList<DiabloMonsterState> monsters)
    {
        foreach (var monster in monsters) {
            if (monster.HitPoints < 0
                || monster.MaxHitPoints < 0
                || monster.MaxHitPoints > 0 && monster.HitPoints > monster.MaxHitPoints
                || monster.HitPointsQ6 < 0
                || monster.MaxHitPointsQ6 < 0
                || monster.MaxHitPointsQ6 > 0
                    && monster.HitPointsQ6 > monster.MaxHitPointsQ6
                || monster.DialogueMessageId < 0
                || monster.DialogueMessageId > short.MaxValue
                || monster.DialogueMessageId != 0 && !monster.IsUnique
                || !Enum.IsDefined(monster.DialogueState)
                || monster.DialogueState != AuthoritativeMonsterDialogueState.Unspecified
                    && !monster.IsUnique
                || monster.AttackDamage < 0
                || monster.MinDamage < 0
                || monster.MaxDamage < 0
                || monster.MaxDamage < monster.MinDamage
                || monster.MinDamageSpecial < 0
                || monster.MaxDamageSpecial < 0
                || monster.MaxDamageSpecial < monster.MinDamageSpecial
                || monster.AggroRange < 0
                || monster.MonsterLevel < 0
                || monster.DeathAnimationTicks is < 0 or > MaximumNativeMonsterDeathAnimationTicks
                || monster.DeathTicksRemaining < 0
                || monster.DeathTicksRemaining > monster.DeathAnimationTicks
                || monster.HitPointsQ6 > 0 && monster.DeathTicksRemaining != 0
                || monster.ExperienceReward > MaximumNativeMonsterExperienceReward
                || (!monster.NativeInitializationPresent
                    && (monster.MonsterItemSeed == 0) != (monster.MonsterAiSeed == 0))
                || monster.ToHit is < 0 or > byte.MaxValue
                || monster.ToHitSpecial is < 0 or > byte.MaxValue
                || monster.Intelligence is < 0 or > byte.MaxValue
                || !NativeTextLimits.IsIdentityWithinLimit(monster.NativeAi)
                || monster.NativeAi.Contains('\0')
                || !string.IsNullOrEmpty(monster.NativeAi)
                    && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(monster.NativeAi)
                || !NativeTextLimits.IsIdentityWithinLimit(monster.UniqueIdentity)
                || !string.IsNullOrEmpty(monster.UniqueIdentity) && !monster.IsUnique
                || monster.Direction is < 0 or > MaximumNativeMonsterDirection
                || monster.FallenFearDirection is < 0 or > MaximumNativeMonsterDirection
                || monster.BatRetreatDirection is < 0 or > MaximumNativeMonsterDirection
                || monster.FireResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.LightningResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.MagicResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.AcidResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || !Enum.IsDefined(monster.LeaderRelation)
                || (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.None
                    ? monster.LeaderEntityId != 0
                    : monster.LeaderEntityId == 0)
                || (monster.LeaderRelation != AuthoritativeMonsterLeaderRelation.None
                    && monster.IsUnique)
                || monster.PackSize is < 0 or > MaximumNativeMonsterPackSize
                || monster.ActiveForTicks is < 0 or > MaximumNativeMonsterActiveTicks
                || monster.BatMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RangedAvoidanceTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.RangedAvoidanceMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RhinoMovementTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.SkeletonRangedMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.FatMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RhinoMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.FallenFearTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SneakRetreatTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
                || monster.ScavengerHealingTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
                || monster.BatRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
                || monster.BatMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RangedAvoidanceMoveTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.RangedAvoidanceMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoMovementTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.AvoidanceMovementTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.AvoidanceMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonMeleeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonRangedMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.FatMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonKingDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.HorkDemonDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.HorkDemonMoveTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.HorkDemonMoveTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.RangedAvoidanceDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SnakeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.CounselorDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.AvoidanceDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.RangedDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.MegaDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.CounselorRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
                || monster.FallenStandAnimationFrame is < 0 or > MaximumNativeMonsterFallenStandAnimationFrame
                || monster.FallenStandAnimationTick is < 0 or > MaximumNativeMonsterFallenStandAnimationTick
                || monster.FallenRallyTicks is < 0 or > MaximumNativeMonsterFallenRallyTicks
                || !monster.IsPlayerMinion && monster.OwnerPlayerId != 0) {
                throw new InvalidDataException(
                    "Authoritative monster state is outside the shared protocol range.");
            }

            var futureDeltaX = Math.Abs((long)monster.FuturePositionX - monster.PositionX);
            var futureDeltaY = Math.Abs((long)monster.FuturePositionY - monster.PositionY);
            if (monster.Moving
                    && Math.Max(futureDeltaX, futureDeltaY) != 1
                || !monster.Moving
                    && (monster.FuturePositionX != monster.PositionX
                        || monster.FuturePositionY != monster.PositionY)) {
                throw new InvalidDataException(
                    "Authoritative monster movement state is outside the shared protocol range.");
            }
            if (!monster.Moving
                && (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0)
                || (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0)
                    && monster.MovementEffectTick <= monster.MovementStartedTick) {
                throw new InvalidDataException(
                    "Authoritative monster movement timing is outside the shared protocol range.");
            }
        }

        var monstersById = new Dictionary<uint, DiabloMonsterState>();
        foreach (var monster in monsters) {
            if (!monstersById.TryAdd(monster.EntityId, monster))
                throw new InvalidDataException(
                    "Authoritative monster state contains duplicate entity IDs.");
        }
        foreach (var monster in monsters.Where(monster =>
                     monster.LeaderRelation != AuthoritativeMonsterLeaderRelation.None)) {
            if (!monstersById.TryGetValue(monster.LeaderEntityId, out var leader)
                || leader.EntityId == monster.EntityId
                || leader.HitPoints <= 0
                || leader.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "Authoritative monster state contains an invalid pack leader.");
            }
        }
    }

    private static void ValidateProjectedScavengerCorpseReferences(
        IReadOnlyList<DiabloMonsterState> monsters,
        IReadOnlyList<DiabloCorpseState> corpses)
    {
        var corpsesById = new Dictionary<uint, DiabloCorpseState>();
        foreach (var corpse in corpses) {
            if (!corpsesById.TryAdd(corpse.EntityId, corpse))
                throw new InvalidDataException(
                    "Authoritative corpse state contains duplicate entity IDs.");
        }

        foreach (var monster in monsters) {
            if (monster.ScavengerCorpseEntityId == 0)
                continue;
            if (!corpsesById.TryGetValue(monster.ScavengerCorpseEntityId, out var corpse)
                || corpse.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "Authoritative monster state contains an invalid Scavenger corpse reference.");
            }
        }
    }

    private static void ValidateProjectedProjectilePosition(
        DiabloProjectileState projectile,
        int positionX,
        int positionY,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        string entityKind)
    {
        if (levels.TryGetValue(projectile.LevelId, out var level)
            && AuthoritativeProtocolLimits.IsElementalImpactPositionAtLevelEdge(
                (int)projectile.Behavior,
                (int)projectile.Phase,
                positionX,
                positionY,
                level.Width,
                level.Height)) {
            return;
        }
        ValidateProjectedEntityPosition(
            projectile.LevelId,
            positionX,
            positionY,
            levels,
            entityKind);
    }

    private static void ValidateProjectedProceduralQuestPieceOrigins(
        IReadOnlyList<DiabloProceduralQuestPieceOriginState> origins,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels = null)
    {
        if (origins.Count > AuthoritativeProtocolLimits.MaximumProceduralQuestPieceOriginCount
            || origins.Any(origin => origin.OriginId == 0)
            || origins.Select(origin => origin.OriginId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative procedural quest-piece origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative procedural quest-piece origin {origin.OriginId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedProceduralQuestPieceOrigins(
        Google.Protobuf.Collections.RepeatedField<ProceduralQuestPieceOriginSnapshot> origins,
        IReadOnlyDictionary<uint, LevelGeometrySnapshot>? levels = null)
    {
        if (origins.Count > AuthoritativeProtocolLimits.MaximumProceduralQuestPieceOriginCount
            || origins.Any(origin => origin.OriginId == 0)
            || origins.Select(origin => origin.OriginId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative procedural quest-piece origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || level.Width <= 0
                || level.Height <= 0
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative procedural quest-piece origin {origin.OriginId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedPlayerMinionOwners(
        IReadOnlyList<DiabloMonsterState> monsters,
        IReadOnlyList<StorePlayerSnapshot> players)
    {
        var playersById = new Dictionary<uint, StorePlayerSnapshot>();
        foreach (var player in players) {
            if (!playersById.TryAdd(player.EntityId, player))
                throw new InvalidDataException(
                    "Authoritative player state contains duplicate entity IDs.");
        }

        foreach (var monster in monsters) {
            if (!monster.IsPlayerMinion || monster.OwnerPlayerId == 0)
                continue;
            if (!playersById.TryGetValue(monster.OwnerPlayerId, out var owner)
                || owner.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "Authoritative monster state contains an invalid player-minion owner reference.");
            }
        }
    }

    private static void ValidateProjectedTownerGreetings(
        IReadOnlyList<AuthoritativeTownerGreetingDefinition> greetings,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        if (greetings.Count > AuthoritativeProtocolLimits.MaximumTownerGreetingCount) {
            throw new InvalidDataException(
                "Authoritative towner greetings exceed the shared protocol limit.");
        }

        var townerIds = new HashSet<int>();
        foreach (var greeting in greetings) {
            var hasLevel = levels.TryGetValue(greeting.LevelId, out var level);
            if (!townerIds.Add(greeting.TownerId)
                || greeting.TownerId is < 0 or > MaximumNativeQuestByte
                || greeting.LevelId > ushort.MaxValue
                || !AuthoritativeProtocolLimits.IsNativeTileCoordinate(
                    greeting.PositionX,
                    greeting.PositionY)
                || levels.Count > 0
                    && (!hasLevel
                        || greeting.PositionX >= level!.Width
                        || greeting.PositionY >= level.Height)
                || greeting.MessageId is <= 0 or > short.MaxValue) {
                throw new InvalidDataException(
                    "Authoritative towner greeting is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedNativeTownResidents(
        string? fingerprint,
        IReadOnlyList<DiabloNativeTownResidentState>? residents,
        IReadOnlyList<AuthoritativeTownerGreetingDefinition> greetings,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        if (fingerprint is null || residents is null)
            throw new InvalidDataException("Native Town resident projection is missing required data.");
        if (fingerprint.Length == 0 && residents.Count == 0)
            return;
        if (!IsLowerSha256Hex(fingerprint)
            || residents.Count != CanonicalNativeTownResidents.Length) {
            throw new InvalidDataException(
                "Native Town resident projection must include its catalog fingerprint and complete canonical roster.");
        }

        for (var slot = 0; slot < CanonicalNativeTownResidents.Length; slot++) {
            var expected = CanonicalNativeTownResidents[slot];
            var resident = residents[slot];
            if (resident.ResidentSlot != slot
                || resident.TownerId != expected.TownerId
                || resident.LevelId != 0
                || resident.PositionX != expected.X
                || resident.PositionY != expected.Y
                || resident.NativeDirection != expected.Direction) {
                throw new InvalidDataException(
                    $"Native Town resident slot {slot} does not match the canonical original Diablo roster.");
            }
        }

        if (greetings.Count != 8) {
            throw new InvalidDataException(
                "Native Town resident projection requires exactly eight matching human greetings.");
        }
        for (var slot = 0; slot < 8; slot++) {
            var expected = CanonicalNativeTownResidents[slot];
            if (!greetings.Any(greeting => greeting.TownerId == expected.TownerId
                    && greeting.LevelId == 0
                    && greeting.PositionX == expected.X
                    && greeting.PositionY == expected.Y)) {
                throw new InvalidDataException(
                    $"Native Town greeting for human resident slot {slot} does not match the canonical roster.");
            }
        }

        if (levels.Count == 0)
            return;
        if (!levels.TryGetValue(0, out var town)
            || town.Width != 112
            || town.Height != 112) {
            throw new InvalidDataException(
                "Native Town resident projection requires available 112 by 112 Town geometry.");
        }
        var expectedOccupancy = CanonicalNativeTownOccupancy();
        if (expectedOccupancy.Count != 20
            || expectedOccupancy.Any(cell => !town.StaticObjectBlockedCells.Contains(cell))) {
            throw new InvalidDataException(
                "Native Town presentation geometry is missing canonical resident occupancy cells.");
        }
    }

    private static HashSet<int> CanonicalNativeTownOccupancy()
    {
        var cells = new HashSet<int>();
        for (var slot = 0; slot < CanonicalNativeTownResidents.Length; slot++) {
            var resident = CanonicalNativeTownResidents[slot];
            Add(resident.X, resident.Y);
            if (resident.TownerId == 9) {
                Add(resident.X - 1, resident.Y);
                Add(resident.X, resident.Y - 1);
                Add(resident.X - 1, resident.Y - 1);
            }
        }
        return cells;

        void Add(int x, int y) => cells.Add(y * 112 + x);
    }

    private static bool IsLowerSha256Hex(string value)
    {
        if (value.Length != 64)
            return false;
        foreach (var character in value) {
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }

    private static void ValidateProjectedMonsterPoolCapacity(
        IReadOnlyList<DiabloMonsterState> monsters)
    {
        foreach (var group in monsters.GroupBy(monster => monster.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"Authoritative monster state contains more than {AuthoritativeLevel.NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }
    }

    private static void ValidateProjectedCorpsePositions(
        IReadOnlyList<DiabloCorpseState> corpses,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var corpse in corpses) {
            ValidateProjectedEntityPosition(
                corpse.LevelId,
                corpse.PositionX,
                corpse.PositionY,
                levels,
                "corpse");
        }
    }

    private static void ValidateProjectedPortalPositions(
        IReadOnlyList<DiabloPortalState> portals,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var portal in portals) {
            ValidateProjectedEntityPosition(
                portal.SourceLevelId,
                portal.SourcePositionX,
                portal.SourcePositionY,
                levels,
                "portal source");
            ValidateProjectedEntityPosition(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY,
                levels,
                "portal destination");
        }
    }

    private static bool IsWarpAvailable(
        AuthoritativeWarpTarget warp,
        IReadOnlyList<DiabloQuestState> quests)
    {
        if (warp.RequiredQuestId == 0)
            return true;
        var quest = quests.FirstOrDefault(quest => quest.QuestId == warp.RequiredQuestId);
        return quest is not null
            && !quest.Unavailable
            && (warp.RequiredQuestCompleted ? quest.Completed : quest.Active);
    }

    private static PlayerSnapshot ToProtocolPlayer(
        StorePlayerSnapshot state,
        ulong snapshotTick,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks,
        AuthoritativeItemCatalog? items,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels = null)
    {
        if (state.StashOpen && state.ActiveStoreId is not null) {
            throw new InvalidDataException(
                "Authoritative player projection opens a stash and store simultaneously.");
        }
        ValidateProjectedPlayerItemTopology(state);
        if (state.Inventory.Any(item => item.Price > int.MaxValue)
            || state.HeldItem is { Price: > int.MaxValue }
            || state.Stash.Any(item => item.Price > int.MaxValue)) {
            throw new InvalidDataException(
                "Authoritative item price exceeds the shared native protocol range.");
        }
        if (!string.IsNullOrEmpty(state.CharacterName)
            && !DiabloPlayerName.IsValid(state.CharacterName)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid character name.");
        }
        if (!Enum.IsDefined(state.AbilityKind)
            || !Enum.IsDefined(state.FacingDirection)
            || !Enum.IsDefined(state.CombatMode)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid enum value.");
        }
        if (state.LightRadius != 0
            && (state.LightRadius is < MinimumNativeLightRadius or > MaximumNativeLightRadius)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid light radius.");
        }
        if (state.CombatMode == DiabloPlayerCombatMode.Stand
                && state.CombatModeEndTick != 0
            || state.CombatMode is DiabloPlayerCombatMode.Block
                or DiabloPlayerCombatMode.GotHit
                && state.CombatModeEndTick == 0) {
            throw new InvalidDataException(
                "Authoritative player combat-mode timing is invalid.");
        }
        if (state.Attributes.Strength.Base < 0
            || state.Attributes.Strength.Current < 0
            || state.Attributes.Magic.Base < 0
            || state.Attributes.Magic.Current < 0
            || state.Attributes.Dexterity.Base < 0
            || state.Attributes.Dexterity.Current < 0
            || state.Attributes.Vitality.Base < 0
            || state.Attributes.Vitality.Current < 0) {
            throw new InvalidDataException(
                "Authoritative player attributes contain negative values.");
        }
        ValidateProjectedVisitedLevels(state);
        ValidateProjectedPlayerSpells(state);
        ValidateProjectedStatusEffects(state);
        ValidateProjectedPlayerAction(state, snapshotTick, levels);
        if (state.Life < 0
            || state.LifeMaximum < 0
            || state.LifeMaximum > 0 && state.Life > state.LifeMaximum
            || state.Mana < 0
            || state.ManaMaximum < 0
            || state.ManaMaximum > 0 && state.Mana > state.ManaMaximum
            || state.BaseLifeMaximum < 0
            || state.BaseManaMaximum < 0
            || state.Gold > int.MaxValue
            || state.StashGold > int.MaxValue
            || state.StatPoints > int.MaxValue
            || state.ReflectCharges > ushort.MaxValue
            || state.DiabloKillLevel > byte.MaxValue
            || state.CharacterLevel > MaximumNativeCharacterLevel) {
            throw new InvalidDataException(
                "Authoritative player resources exceed the shared protocol range.");
        }
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
            AbilityTargetingKind = ResolveSpellTargetingKind(state.AbilitySpellId, spells),
            HostileMode = !state.FriendlyMode,
            FacingDirection = (PlayerDirection)state.FacingDirection,
            CombatMode = (PlayerCombatMode)state.CombatMode,
            CombatModeEndTick = state.CombatModeEndTick,
            LevelId = state.LevelId,
            Experience = state.Experience,
            DiabloKillLevel = state.DiabloKillLevel,
            StatPoints = state.StatPoints,
            VisitedLevelIds = { state.VisitedLevelIds.OrderBy(levelId => levelId) },
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
                TargetingKind = ResolveSpellTargetingKind(spell.SpellId, spells),
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
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        if (state.HeldItem is { } heldItem) {
            player.HeldItem = new ItemSnapshot {
                StoreId = heldItem.StoreId,
                StoreSlot = heldItem.StoreSlot,
                ItemSeed = heldItem.ItemSeed,
                Price = heldItem.Price,
                PurchasedAtTick = heldItem.PurchasedAtTick,
                State = ToProtocolItemState(heldItem.ItemSeed, heldItem.State, spells, spellBooks, items),
            };
        }
        foreach (var item in state.Equipment) {
            player.Equipment.Add(new EquippedItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        foreach (var item in state.Belt) {
            player.Belt.Add(new BeltItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        player.InventoryGrid.Add(state.InventoryGrid);
        foreach (var item in state.Stash.OrderBy(item => item.Page).ThenBy(item => item.Cell)) {
            player.Stash.Add(new StashItemSnapshot {
                Page = item.Page,
                Cell = item.Cell,
                Item = new ItemSnapshot {
                    StoreId = item.StoreId,
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    PurchasedAtTick = item.PurchasedAtTick,
                    State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
                },
            });
        }
        player.StashGold = state.StashGold;
        player.StashOpen = state.StashOpen;
        player.LightRadius = state.LightRadius;
        player.NativeWalkPathPending = state.NativeWalkPath is not null
            && state.NativeWalkPath.Directions.Count > 0;
        return player;
    }

    private static void ValidateProjectedPlayerItemTopology(StorePlayerSnapshot state)
    {
        if (state.Inventory is null
            || state.Equipment is null
            || state.Belt is null
            || state.InventoryGrid is null
            || state.Stash is null
            || state.Inventory.Any(item => item is null)
            || state.Stash.Any(item => item is null)
            || state.Inventory.Count > NativeMaximumInventoryCount
            || state.Equipment.Count > NativeMaximumEquipmentCount
            || state.Belt.Count > NativeMaximumBeltCount
            || state.Stash.Count > DiabloStashLayout.MaximumItems
            || state.InventoryGrid.Count != 0 && state.InventoryGrid.Count != NativeMaximumInventoryGridCount
            || state.InventoryGrid.Any(cell => cell < -1 || cell >= state.Inventory.Count)) {
            throw new InvalidDataException(
                "Authoritative player item collections have invalid topology.");
        }

        if (state.InventoryGrid.Count > 0) {
            var expected = Enumerable.Repeat(-1, state.InventoryGrid.Count).ToArray();
            var grid = state.InventoryGrid.ToArray();
            for (var itemIndex = 0; itemIndex < state.Inventory.Count; itemIndex++) {
                var anchor = Array.IndexOf(grid, itemIndex);
                var itemState = state.Inventory[itemIndex].State;
                var width = Math.Clamp(itemState.InventoryWidth, 1, NativeInventoryWidth);
                var height = Math.Clamp(itemState.InventoryHeight, 1, NativeInventoryHeight);
                if (anchor < 0
                    || !HasExactProjectedInventoryFootprint(
                        expected,
                        anchor,
                        itemIndex,
                        width,
                        height)) {
                    throw new InvalidDataException(
                        "Authoritative player inventory has an invalid footprint topology.");
                }
            }
            if (!grid.SequenceEqual(expected)) {
                throw new InvalidDataException(
                    "Authoritative player inventory grid does not match item footprints.");
            }
        }

        var equipmentSlots = new HashSet<uint>();
        foreach (var item in state.Equipment) {
            if (item is null || item.State is null || item.Slot >= NativeMaximumEquipmentCount
                || !equipmentSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "Authoritative player equipment slots are invalid or duplicated.");
            }
        }

        var beltSlots = new HashSet<uint>();
        foreach (var item in state.Belt) {
            if (item is null || item.State is null || item.Slot >= NativeMaximumBeltCount
                || !beltSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "Authoritative player belt slots are invalid or duplicated.");
            }
        }

        var itemSeeds = new HashSet<uint>();
        bool AddItemSeed(uint itemSeed) => itemSeed == 0 || itemSeeds.Add(itemSeed);
        foreach (var item in state.Inventory) {
            if (item is null || item.State is null || !AddItemSeed(item.ItemSeed)) {
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
            }
        }
        foreach (var item in state.Equipment)
            if (!AddItemSeed(item.ItemSeed))
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
        foreach (var item in state.Belt)
            if (!AddItemSeed(item.ItemSeed))
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
        foreach (var item in state.Stash) {
            if (item is null || item.State is null || !AddItemSeed(item.ItemSeed)) {
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
            }
        }
        if (state.HeldItem is { } heldItem
            && (heldItem.State is null || !AddItemSeed(heldItem.ItemSeed))) {
            throw new InvalidDataException(
                "Authoritative held-item reference is invalid or duplicated.");
        }

        foreach (var pageGroup in state.Stash.GroupBy(item => item.Page)) {
            if (pageGroup.Key >= DiabloStashLayout.PageCount)
                throw new InvalidDataException("Authoritative stash page is outside the native range.");
            var cells = DiabloStashLayout.CreateEmptyPage();
            foreach (var item in pageGroup) {
                var width = Math.Max(1, item.State.InventoryWidth);
                var height = Math.Max(1, item.State.InventoryHeight);
                if (width > DiabloStashLayout.ColumnCount
                    || height > DiabloStashLayout.RowCount
                    || !DiabloStashLayout.CanPlace(cells, item.Cell, width, height)) {
                    throw new InvalidDataException(
                        "Authoritative stash contains an invalid or overlapping footprint.");
                }
                DiabloStashLayout.Fill(cells, item.Cell, width, height);
            }
        }
    }

    private static bool HasExactProjectedInventoryFootprint(
        int[] expected,
        int anchor,
        int itemIndex,
        int width,
        int height)
    {
        var column = anchor % NativeInventoryWidth;
        var row = anchor / NativeInventoryWidth;
        if (column + width > NativeInventoryWidth || row + height > NativeInventoryHeight)
            return false;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * NativeInventoryWidth + column + x;
                if (expected[cell] >= 0)
                    return false;
                expected[cell] = itemIndex;
            }
        }
        return true;
    }

    private static void ValidateProjectedVisitedLevels(StorePlayerSnapshot state)
    {
        if (state.VisitedLevelIds is null
            || !AuthoritativeLevelIdRules.IsSupported(state.LevelId)
            || state.VisitedLevelIds.Count > MaximumNativeVisitedLevelCount
            || state.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdRules.IsRegularLevel(levelId))
            || state.VisitedLevelIds.Count > 0
                && AuthoritativeLevelIdRules.IsRegularLevel(state.LevelId)
                && !state.VisitedLevelIds.Contains(state.LevelId)
            || state.VisitedLevelIds.Distinct().Count() != state.VisitedLevelIds.Count) {
            throw new InvalidDataException(
                "Authoritative player visited-level state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedPlayerSpells(StorePlayerSnapshot state)
    {
        if (state.MemorizedSpells is null
            || state.AbilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
            || state.MemorizedSpells.Count > AuthoritativePlayerLoadoutCatalog.MaximumMemorizedSpellCount
            || state.MemorizedSpells.Any(spell =>
                spell is null
                || spell.SpellId == 0
                || spell.SpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                || spell.Level == 0
                || spell.Level > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
            || state.MemorizedSpells.Select(spell => spell.SpellId).Distinct().Count()
                != state.MemorizedSpells.Count) {
            throw new InvalidDataException(
                "Authoritative player spell state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedStatusEffects(StorePlayerSnapshot state)
    {
        if (state.StatusEffects is null
            || state.StatusEffects.Count > MaximumStatusEffectCount
            || state.StatusEffects.Any(effect =>
                effect is null
                || effect.EffectId == 0
                || effect.RemainingTicks == 0
                || !Enum.IsDefined(effect.Behavior)
                || ((effect.Behavior is AuthoritativeStatusEffectBehavior.Infravision
                        or AuthoritativeStatusEffectBehavior.Search
                        or AuthoritativeStatusEffectBehavior.Etherealize)
                    && effect.Magnitude != 0))
            || state.StatusEffects.Select(effect => effect.EffectId).Distinct().Count()
                != state.StatusEffects.Count) {
            throw new InvalidDataException(
                "Authoritative player status-effect state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedPlayerAction(
        StorePlayerSnapshot state,
        ulong snapshotTick,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        var action = state.CurrentAction;
        if (action is null)
            return;
        if (!Enum.IsDefined(action.Kind)
            || !AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(
                snapshotTick,
                action.StartedAtTick,
                action.EffectTick,
                action.ReadyTick,
                action.EndTick,
                action.EffectApplied)
            || action.EndTick != state.CombatModeEndTick) {
            throw new InvalidDataException(
                $"Authoritative player action timing is invalid at snapshot tick {snapshotTick}.");
        }

        var valid = action.Kind switch {
            DiabloPlayerActionKind.Move =>
                state.CombatMode is DiabloPlayerCombatMode.WalkNorthwards
                    or DiabloPlayerCombatMode.WalkSouthwards
                    or DiabloPlayerCombatMode.WalkSideways
                && action.DirectionX is >= MinimumNativeDirection and <= MaximumNativeDirection
                && action.DirectionY is >= MinimumNativeDirection and <= MaximumNativeDirection
                && (action.DirectionX != 0 || action.DirectionY != 0)
                && action.TargetEntityId == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is null,
            DiabloPlayerActionKind.Attack =>
                state.CombatMode is DiabloPlayerCombatMode.Attack
                    or DiabloPlayerCombatMode.RangedAttack
                && action.DirectionX == 0
                && action.DirectionY == 0
                && (action.TargetCell
                    ? action.TargetEntityId == 0
                        && IsProjectedActionTarget(state.LevelId, action.TargetX, action.TargetY, levels)
                    : action.TargetEntityId != 0
                        && (action.TargetX == 0 && action.TargetY == 0
                            || state.CombatMode == DiabloPlayerCombatMode.RangedAttack
                                && IsProjectedActionTarget(
                                    state.LevelId,
                                    action.TargetX,
                                    action.TargetY,
                                    levels)))
                && action.CastIntent is null,
            DiabloPlayerActionKind.Cast =>
                state.CombatMode == DiabloPlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is not null
                && action.CastIntent.SpellId != 0
                && action.TargetEntityId == action.CastIntent.TargetEntityId
                && IsProjectedCastTarget(state.LevelId, action.CastIntent, levels)
                && IsValidCastSource(state, action.CastIntent, action.EffectApplied)
                && Enum.IsDefined(action.CastIntent.Source)
                && (!action.CastIntent.SourceItem.HasValue
                    || Enum.IsDefined(action.CastIntent.SourceItem.Value.Location)),
            DiabloPlayerActionKind.Death =>
                state.CombatMode == DiabloPlayerCombatMode.Death
                && state.Life == 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetEntityId != 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is null,
            DiabloPlayerActionKind.LevelTransition =>
                state.CombatMode == DiabloPlayerCombatMode.NewLevel
                && state.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.TargetEntityId != 0
                && action.CastIntent is null,
            DiabloPlayerActionKind.RestartTown =>
                state.CombatMode == DiabloPlayerCombatMode.NewLevel
                && state.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.TargetEntityId == 0
                && action.CastIntent is null,
            _ => false,
        };
        if (!valid)
            throw new InvalidDataException("Authoritative player action is invalid.");
    }

    private static bool IsProjectedActionTarget(
        uint levelId,
        int targetX,
        int targetY,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(targetX, targetY))
            return false;
        return levels is null
            || !levels.TryGetValue(levelId, out var level)
            || level.Width == 0
            || level.Height == 0
            || targetX < level.Width && targetY < level.Height;
    }

    private static bool IsProjectedCastTarget(
        uint levelId,
        DiabloCastIntent cast,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(
                cast.TargetX,
                cast.TargetY))
            return false;
        if (cast.TargetEntityId != 0)
            return true;
        return levels is null
            || !levels.TryGetValue(levelId, out var level)
            || level.Width == 0
            || level.Height == 0
            || cast.TargetX < level.Width && cast.TargetY < level.Height;
    }

    private static bool IsValidCastSource(
        StorePlayerSnapshot player,
        DiabloCastIntent cast,
        bool effectApplied)
    {
        var requiresItem = cast.Source is DiabloCastSourceKind.ChargedItem
            or DiabloCastSourceKind.ConsumableItem;
        if (requiresItem != cast.SourceItem.HasValue)
            return false;
        if (!cast.SourceItem.HasValue)
            return true;
        var item = cast.SourceItem.Value;
        // Consumption precedes the animation tail; its source is now history,
        // not an inventory index that must survive removal/reindexing.
        if (effectApplied && cast.Source == DiabloCastSourceKind.ConsumableItem)
            return item.Location switch {
                DiabloPlayerItemLocation.Inventory => item.Slot < NativeMaximumInventoryCount,
                DiabloPlayerItemLocation.Belt => item.Slot < NativeMaximumBeltCount,
                DiabloPlayerItemLocation.Equipment => item.Slot < NativeMaximumEquipmentCount,
                DiabloPlayerItemLocation.Stash =>
                    item.Slot < AuthoritativeProtocolLimits.NativeStashCellCount
                    && item.Page < AuthoritativeProtocolLimits.NativeStashPageCount,
                _ => false,
            };
        return item.Location switch {
            DiabloPlayerItemLocation.Inventory =>
                item.Slot < player.Inventory.Count
                && MatchesStableItem(
                    player.Inventory[(int)item.Slot].State,
                    item.StableItemId),
            DiabloPlayerItemLocation.Belt =>
                item.Slot < NativeMaximumBeltCount
                && player.Belt.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Equipment =>
                item.Slot < NativeMaximumEquipmentCount
                && player.Equipment.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Stash =>
                player.StashOpen
                && item.Slot < AuthoritativeProtocolLimits.NativeStashCellCount
                && item.Page < AuthoritativeProtocolLimits.NativeStashPageCount
                && player.Stash.Any(candidate =>
                    candidate.Page == item.Page
                    && candidate.Cell == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            _ => false,
        };
    }

    private static bool MatchesStableItem(
        AuthoritativeItemState state,
        uint stableItemId)
    {
        return stableItemId == 0 || state.StableItemId == stableItemId;
    }

    public static DiabloSimulationSnapshot FromProtocolSnapshot(Snapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateProtocolSnapshotHash(source);
        if (source.NativeTownCatalogFingerprint.Length != 0
            || source.NativeTownResidents.Count != 0)
            AuthoritativeSnapshotValidator.Validate(source);
        ValidateEntityIds(source, allowZeroPlayerEntityId: true);
        ValidateProjectedWorldMutationIds(source.ActiveWorldMutationIds);
        ValidateProjectedMaterializedProceduralLevelIds(
            source.MaterializedProceduralLevelIds);
        ValidateProjectedSetLevelObjectOrigins(
            source.SetLevelObjectOrigins,
            source.LevelGeometry.Count == 0
                ? null
                : source.LevelGeometry.ToDictionary(level => level.LevelId));
        ValidateProjectedProceduralQuestPieceOrigins(
            source.ProceduralQuestPieceOrigins,
            source.LevelGeometry.Count == 0
                ? null
                : source.LevelGeometry.ToDictionary(level => level.LevelId));
        if (source.ActiveStore is not null)
            ValidateProjectedActiveStoreSlots(source.ActiveStore.Items.Select(item => item.StoreSlot));
        if (source.Players.Count != 1) {
            throw new InvalidDataException(
                "The authoritative save must contain exactly one player snapshot.");
        }
        ValidateImportedStoreState(source);
        var player = ToPlayerState(source.Players[0]);
        ValidateImportedPlayerState(player, source.Tick);
        var activeStore = source.ActiveStore is null
            ? null
            : new DiabloStoreState(
                source.ActiveStore.StoreId,
                source.ActiveStore.Items.Select(item => new StoreItem(
                    item.StoreSlot,
                    item.ItemSeed,
                    item.Price,
                    FromProtocolItemState(item.State))).ToArray());
        var cathedralDoorObjects = source.Objects
            .Where(@object => @object.CathedralDoor is not null)
            .Select(FromProtocolCathedralDoor)
            .ToArray();
        var snapshot = new DiabloSimulationSnapshot(
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
                MovementStartedTick = monster.MovementStartedTick,
                MovementEffectTick = monster.MovementEffectTick,
                Direction = monster.Direction,
                MonsterItemSeed = monster.MonsterItemSeed,
                MonsterAiSeed = monster.MonsterAiSeed,
                NativeInitializationPresent = monster.NativeInitializationPresent,
                DialogueState = (AuthoritativeMonsterDialogueState)monster.DialogueState,
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
                LeaderEntityId = monster.LeaderEntityId,
                LeaderRelation = (AuthoritativeMonsterLeaderRelation)monster.LeaderRelation,
                PackSize = monster.PackSize,
                ActiveForTicks = monster.ActiveForTicks,
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
                FallenStandAnimationFrame = monster.FallenStandAnimationFrame,
                FallenStandAnimationTick = monster.FallenStandAnimationTick,
                FallenRallyTicks = monster.FallenRallyTicks,
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
                HorkDemonMoveTicks = monster.HasHorkDemonMoveTicks
                    ? monster.HorkDemonMoveTicks
                    : -1,
                HorkDemonMoveTurn = monster.HasHorkDemonMoveTurn
                    ? monster.HorkDemonMoveTurn
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
                QuestTransitionKind = (AuthoritativeObjectQuestTransitionKind)@object.QuestTransitionKind,
                QuestTransitionAmount = @object.QuestTransitionAmount,
                QuestTransitionRequiredVariable1 = @object.QuestTransitionKind == 0
                    && @object.QuestTransitionRequiredVariable1 == 0
                    ? -1
                    : @object.QuestTransitionRequiredVariable1,
                QuestTransitionRequiredVariable2 = @object.QuestTransitionKind == 0
                    && @object.QuestTransitionRequiredVariable2 == 0
                    ? -1
                    : @object.QuestTransitionRequiredVariable2,
                RewardKind = (AuthoritativeObjectRewardKind)@object.RewardKind,
                RewardId = @object.RewardId,
                RewardItemLevel = @object.RewardItemLevel,
                RewardOffsetX = @object.RewardOffsetX,
                RewardOffsetY = @object.RewardOffsetY,
                RewardOriginKind = (AuthoritativeObjectRewardOriginKind)@object.RewardOriginKind,
                RewardSetLevelId = @object.RewardOriginKind == 0 && @object.RewardSetLevelId == 0
                    ? -1
                    : @object.RewardSetLevelId,
                ProjectileTargetOffsetX = @object.ProjectileTargetOffsetX,
                ProjectileTargetOffsetY = @object.ProjectileTargetOffsetY,
                QuestSequenceFollowUpKind = (AuthoritativeQuestSequenceFollowUpKind)@object.QuestSequenceFollowUpKind,
                QuestSequenceFollowUpAmount = @object.QuestSequenceFollowUpAmount,
                QuestSequenceFollowUpOffsetX = @object.QuestSequenceFollowUpOffsetX,
                QuestSequenceFollowUpOffsetY = @object.QuestSequenceFollowUpOffsetY,
                ProjectileSourceKind = (AuthoritativeObjectProjectileSourceKind)@object.ProjectileSourceKind,
                NativeObjectId = @object.NativeObjectId,
                PresentationKind = FromProtocolObjectPresentationKind(@object.PresentationKind),
                HideWhenActivated = !@object.KeepVisibleAfterActivation,
                AnimationFrame = checked((int)@object.AnimationFrame),
                ActivatedAnimationFrame = checked((int)@object.ActivatedAnimationFrame),
                ActivateOnOccupancy = @object.ActivateOnOccupancy,
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
                quest.TurnInActivationOnly,
                quest.HasTurnInMessageId ? quest.TurnInMessageId : 0,
                quest.TurnInDialogueOnly,
                quest.HasTurnInCompletionRequiredVariable1 ? quest.TurnInCompletionRequiredVariable1 : -1,
                quest.HasTurnInCompletionVariable1 ? quest.TurnInCompletionVariable1 : -1,
                quest.HasTurnInRequiredVariable1 ? quest.TurnInRequiredVariable1 : -1,
                quest.HasTurnInVariable1 ? quest.TurnInVariable1 : -1,
                quest.TurnInProgressOnly,
                quest.TurnInBlockedVisitedLevel)).ToArray(),
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
                NativeMissileId = (AuthoritativeProjectileBehavior)projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic
                    ? string.IsNullOrEmpty(projectile.NativeMissileId)
                        ? ResolveNativeMonsterMissileId(projectile.NativeVisual)
                        : projectile.NativeMissileId
                    : null,
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
            ActiveWorldMutationIds = source.ActiveWorldMutationIds.ToArray(),
            MaterializedProceduralLevelIds = source.MaterializedProceduralLevelIds.ToArray(),
            SetLevelObjectOrigins = source.SetLevelObjectOrigins
                .OrderBy(origin => origin.SetLevelId)
                .Select(origin => new DiabloSetLevelObjectOriginState(
                    origin.SetLevelId,
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY))
                .ToArray(),
            ProceduralQuestPieceOrigins = source.ProceduralQuestPieceOrigins
                .OrderBy(origin => origin.OriginId)
                .Select(origin => new DiabloProceduralQuestPieceOriginState(
                    origin.OriginId,
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY))
                .ToArray(),
            CathedralDoorObjects = cathedralDoorObjects,
        };
        ValidateImportedSimulationState(snapshot);
        return snapshot;
    }

    private static void ValidateProtocolSnapshotHash(Snapshot source)
    {
        if (string.IsNullOrWhiteSpace(source.StateSha256))
            return;

        var expectedHash = SnapshotStateHasher.Compute(source);
        if (!string.Equals(source.StateSha256, expectedHash, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException(
                "Authoritative protocol snapshot state hash does not match its contents.");
        }
    }

    private static void ValidateImportedStoreState(Snapshot source)
    {
        var player = source.Players[0];
        var activeStoreId = player.ActiveStoreId;
        if ((source.ActiveStore is null) != (activeStoreId == 0)
            || source.ActiveStore is not null
                && (source.ActiveStore.StoreId == 0
                    || source.ActiveStore.StoreId != activeStoreId)) {
            throw new InvalidDataException(
                "Authoritative active-store state does not match the saved player projection.");
        }
        if (player.StashOpen && activeStoreId != 0) {
            throw new InvalidDataException(
                "Authoritative saved player opens a stash and store simultaneously.");
        }
    }

    private static void ValidateImportedPlayerState(
        StorePlayerSnapshot state,
        ulong snapshotTick)
    {
        // Keep save imports on the same fail-closed player boundary as live
        // projections without duplicating the topology and range rules.
        _ = ToProtocolPlayer(
            state,
            snapshotTick,
            spells: null,
            spellBooks: null,
            items: null);
    }

    private static void ValidateImportedSimulationState(
        DiabloSimulationSnapshot state)
    {
        // Reuse the aggregate projection boundary for world entities so save
        // imports cannot bypass checks that protect live authoritative output.
        _ = ToProtocolSnapshot(state, allowZeroPlayerEntityId: true);
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

    private static DiabloCathedralDoorObjectState FromProtocolCathedralDoor(
        ObjectSnapshot @object)
    {
        var door = @object.CathedralDoor
            ?? throw new InvalidDataException(
                "The authoritative object does not contain its typed Cathedral door payload.");
        if (door.EntityId == 0
            || door.NativeObjectId is < 1 or > 2
            || door.CachedPiece == 0
            || door.CachedNeighborPiece == 0
            || door.CachedPiece > AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1
            || door.CachedNeighborPiece > AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1
            || door.AnimationFrame > byte.MaxValue
            || door.SelectionRegion > int.MaxValue
            || door.DoorState is < 0 or > 2
            || door.EntityId != @object.EntityId
            || door.LevelId != @object.LevelId
            || door.PositionX != @object.PositionX
            || door.PositionY != @object.PositionY
            || door.NativeObjectId != @object.NativeObjectId
            || @object.Activated
            || @object.Destroyed
            || @object.EffectKind != (int)AuthoritativeObjectEffectKind.None
            || @object.QuestId != 0
            || @object.QuestTransitionKind != (int)AuthoritativeObjectQuestTransitionKind.None
            || @object.RewardKind != (int)AuthoritativeObjectRewardKind.None
            || @object.QuestSequenceFollowUpKind != (int)AuthoritativeQuestSequenceFollowUpKind.None
            || @object.DialogueMessageId != 0
            || @object.Breakable
            || @object.IsShrine
            || @object.ActivateOnOccupancy
            || !@object.KeepVisibleAfterActivation
            || @object.AnimationFrame != door.AnimationFrame
            || @object.MissilePassable != door.MissilePassable) {
            throw new InvalidDataException(
                "The authoritative Cathedral door payload conflicts with its object metadata.");
        }

        var state = new DiabloCathedralDoorObjectState(
            door.EntityId,
            door.LevelId,
            door.PositionX,
            door.PositionY,
            checked((int)door.NativeObjectId),
            door.DoorState,
            checked((int)door.CachedPiece),
            checked((int)door.CachedNeighborPiece),
            checked((int)door.AnimationFrame),
            checked((int)door.SelectionRegion),
            door.PreDraw,
            door.MissilePassable,
            door.Solid);
        ValidateCathedralDoorFields(state, new Dictionary<uint, AuthoritativeLevel>());
        return state;
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
        ulong tick,
        AuthoritativeDialogueMessageCatalog? dialogueMessages = null)
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
                var dialogueText = dialogueMessages is not null
                    && dialogueMessages.TryGet(gameplayEvent.MessageId, out var dialogueMessage)
                    ? dialogueMessage.Text
                    : string.Empty;
                batch.Events.Add(new GameEvent {
                    Dialogue = new DialogueEvent {
                        SourceEntityId = gameplayEvent.SourceEntityId,
                        TargetEntityId = gameplayEvent.TargetEntityId,
                        QuestId = gameplayEvent.QuestId,
                        MessageId = gameplayEvent.MessageId,
                        Text = dialogueText,
                    },
                });
                break;
            case DiabloGameplayEventKind.Victory:
                if (gameplayEvent.Amount < 0 || gameplayEvent.Amount > byte.MaxValue)
                    throw new InvalidDataException(
                        "Authoritative victory event rank exceeds the shared native range.");
                batch.Events.Add(new GameEvent {
                    Victory = new VictoryEvent {
                        PlayerEntityId = entityId,
                        BossEntityId = gameplayEvent.TargetEntityId,
                        DiabloKillLevel = (uint)gameplayEvent.Amount,
                    },
                });
                break;
            case DiabloGameplayEventKind.MapOpened:
                batch.Events.Add(new GameEvent {
                    MapOpened = new MapOpenedEvent {
                        PlayerEntityId = gameplayEvent.TargetEntityId == 0
                            ? entityId
                            : gameplayEvent.TargetEntityId,
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
            StatPoints = source.StatPoints,
            VisitedLevelIds = source.VisitedLevelIds.ToArray(),
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
            Stash = source.Stash.Select(item => new StashStoreItem(
                item.Page,
                item.Cell,
                item.Item?.StoreId ?? 0,
                item.Item?.StoreSlot ?? 0,
                item.Item?.ItemSeed ?? 0,
                item.Item?.Price ?? 0,
                item.Item?.PurchasedAtTick ?? 0,
                FromProtocolItemState(item.Item?.State))).ToArray(),
            StashGold = source.StashGold,
            StashOpen = source.StashOpen,
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
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            TargetCell = source.TargetCell,
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
                    Page = item.Page,
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
                    source.Cast.SourceItem.StableItemId,
                    source.Cast.SourceItem.Page);
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
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            TargetCell = source.TargetCell,
            CastIntent = castIntent,
        };
    }

    private static int ResolveQ6(int whole, int raw)
    {
        return raw == 0 && whole != 0
            ? DeterministicFixedPointQ6.FromWhole(whole)
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
        uint itemSeed,
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks,
        AuthoritativeItemCatalog? items)
    {
        ValidateProjectedItemState(itemSeed, state, spells);
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
            SpellTargetingKind = state.SpellId > 0
                ? ResolveSpellTargetingKind(checked((uint)state.SpellId), spells)
                : SpellTargetingKind.Unspecified,
            PresentationKind = ResolveItemPresentationKind(state, items),
            StableItemId = state.StableItemId,
        };
        if (state.NativeCursorGraphic.HasValue)
            snapshot.NativeCursorGraphic = state.NativeCursorGraphic.Value;
        return snapshot;
    }

    private static void ValidateProjectedItemState(
        uint itemSeed,
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells)
    {
        var spellSourceKind = state.SpellSourceKind
            ?? DiabloCastSourceRules.ClassifyItem(state, spells);
        var hasPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.NativeCursorGraphic.HasValue;
        if (!hasPresentation && itemSeed == 0
            || state.Value < 0
            || state.IdentifiedValue < 0
            || state.MinDamage < 0
            || state.MaxDamage < state.MinDamage
            || state.ArmorClass < 0
            || state.Charges < 0
            || state.MaxCharges < state.Charges
            || state.Durability < 0
            || state.MaxDurability < state.Durability
            || state.FireMinDamage < 0
            || state.FireMaxDamage < state.FireMinDamage
            || state.LightningMinDamage < 0
            || state.LightningMaxDamage < state.LightningMinDamage
            || !IsNativeItemStateRepresentable(state)
            || state.InventoryWidth < 0
            || state.InventoryWidth > NativeInventoryWidth
            || state.InventoryHeight < 0
            || state.InventoryHeight > NativeInventoryHeight
            || hasPresentation
                && (string.IsNullOrEmpty(state.BaseName)
                    || string.IsNullOrEmpty(state.IdentifiedName)
                    || !state.NativeCursorGraphic.HasValue
                    || state.BaseName.Contains('\0')
                    || state.IdentifiedName.Contains('\0')
                    || System.Text.Encoding.UTF8.GetByteCount(state.BaseName) >= 64
                    || System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) >= 64
                    || state.NativeCursorGraphic is < 0 or > byte.MaxValue)
            || !Enum.IsDefined(spellSourceKind)
            || !Enum.IsDefined(state.ItemActionKind)) {
            throw new InvalidDataException(
                "Authoritative item projection contains impossible state ranges.");
        }
    }

    private static bool IsNativeItemStateRepresentable(AuthoritativeItemState state)
    {
        return state.CreateInfo <= ushort.MaxValue
            && state.ItemType is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.Magical is >= byte.MinValue and <= byte.MaxValue
            && state.EquipLocation is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemClass is >= byte.MinValue and <= byte.MaxValue
            && state.MinDamage is >= byte.MinValue and <= byte.MaxValue
            && state.MaxDamage is >= byte.MinValue and <= byte.MaxValue
            && state.ArmorClass is >= short.MinValue and <= short.MaxValue
            && state.MiscId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SpellId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemIndex is >= short.MinValue and <= short.MaxValue
            && state.PlusDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusToHit is >= short.MinValue and <= short.MaxValue
            && state.PlusArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PlusStrength is >= short.MinValue and <= short.MaxValue
            && state.PlusMagic is >= short.MinValue and <= short.MaxValue
            && state.PlusDexterity is >= short.MinValue and <= short.MaxValue
            && state.PlusVitality is >= short.MinValue and <= short.MaxValue
            && state.PlusFireResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusLightningResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMagicResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMana is >= short.MinValue and <= short.MaxValue
            && state.PlusHitPoints is >= short.MinValue and <= short.MaxValue
            && state.PlusDamageModifier is >= short.MinValue and <= short.MaxValue
            && state.PlusGetHit is >= short.MinValue and <= short.MaxValue
            && state.PlusLight is >= short.MinValue and <= short.MaxValue
            && state.SpellLevelAdd is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.FireMinDamage is >= short.MinValue and <= short.MaxValue
            && state.FireMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMinDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusEnemyArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PrefixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SuffixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumStrength is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumMagic is >= byte.MinValue and <= byte.MaxValue
            && state.MinimumDexterity is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.HellfireDamageArmorFlags is >= byte.MinValue and <= byte.MaxValue;
    }

    private static ItemPresentationKind ResolveItemPresentationKind(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        if (items is not null && items.TryGetByState(state, out var definition)) {
            if (definition.GenerationTags.Contains("Gold", StringComparer.OrdinalIgnoreCase))
                return ItemPresentationKind.Gold;
            return definition.GenerationTags.Contains("Ear", StringComparer.OrdinalIgnoreCase)
                ? ItemPresentationKind.Ear
                : ItemPresentationKind.Unspecified;
        }
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
        // Presentation family is authored in object_presentations.tsv and carried through
        // authoritative state; native_object_id remains compatibility metadata.
        return state.PresentationKind switch {
            AuthoritativeObjectPresentationKind.Generic => ObjectPresentationKind.Generic,
            AuthoritativeObjectPresentationKind.NativeArmorStand
                => ObjectPresentationKind.NativeArmorStand,
            AuthoritativeObjectPresentationKind.NativeWeaponRack
                => ObjectPresentationKind.NativeWeaponRack,
            _ => ObjectPresentationKind.Unspecified,
        };
    }

    private static AuthoritativeObjectPresentationKind FromProtocolObjectPresentationKind(
        ObjectPresentationKind kind)
    {
        // Shrine, quest, breakable, and trap are derived from authoritative
        // behavior. Preserve only authored families that cannot be recovered
        // from the object state during a save round-trip.
        return kind switch {
            ObjectPresentationKind.Generic
                => AuthoritativeObjectPresentationKind.Generic,
            ObjectPresentationKind.NativeArmorStand
                => AuthoritativeObjectPresentationKind.NativeArmorStand,
            ObjectPresentationKind.NativeWeaponRack
                => AuthoritativeObjectPresentationKind.NativeWeaponRack,
            _ => AuthoritativeObjectPresentationKind.Unspecified,
        };
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

    private static SpellTargetingKind ResolveSpellTargetingKind(
        uint spellId,
        AuthoritativeSpellCatalog? spells)
    {
        if (spellId == 0
            || spells is null
            || !spells.TryGet(spellId, out var spell)) {
            return SpellTargetingKind.Unspecified;
        }

        return spell.ProjectileBehavior switch {
            AuthoritativeProjectileBehavior.Resurrection
                or AuthoritativeProjectileBehavior.HealOther
                => SpellTargetingKind.Player,
            AuthoritativeProjectileBehavior.Telekinesis
                => SpellTargetingKind.WorldEntity,
            AuthoritativeProjectileBehavior.Targeted
                or AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.HolyBolt
                or AuthoritativeProjectileBehavior.BloodStar
                or AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.Berserk
                => SpellTargetingKind.CombatEntity,
            _ => SpellTargetingKind.Tile,
        };
    }

    private static ProjectilePresentationKind ResolveProjectilePresentationKind(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeProjectilePhase phase,
        AuthoritativeDamageType damageType)
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
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.RingOfFireController
                or AuthoritativeProjectileBehavior.ImmolationController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.Elemental)
            return ProjectilePresentationKind.Fire;
        if (behavior == AuthoritativeProjectileBehavior.FireWallSegment)
            return damageType == AuthoritativeDamageType.Lightning
                ? ProjectilePresentationKind.Lightning
                : ProjectilePresentationKind.Fire;
        if (behavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
            return ProjectilePresentationKind.FireImpact;
        if (behavior == AuthoritativeProjectileBehavior.GuardianController)
            return ProjectilePresentationKind.Guardian;
        if (behavior == AuthoritativeProjectileBehavior.NovaBall)
            return ProjectilePresentationKind.NovaBall;
        if (behavior is AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.ChainLightningController
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaController
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

    /** Selects the native compatibility sprite without exposing gameplay IDs to the client. */
    private static NativeProjectileVisual ResolveNativeProjectileVisual(
        AuthoritativeProjectileBehavior behavior,
        AuthoritativeDamageType damageType,
        string? nativeMissileId)
    {
        if (behavior == AuthoritativeProjectileBehavior.MonsterMagic) {
            return nativeMissileId switch {
                "Lightning" => NativeProjectileVisual.MonsterLightning,
                "OrangeFlare" => NativeProjectileVisual.MonsterOrangeFlare,
                "BlueFlare" => NativeProjectileVisual.MonsterBlueFlare,
                "RedFlare" => NativeProjectileVisual.MonsterRedFlare,
                "YellowFlare" => NativeProjectileVisual.MonsterYellowFlare,
                "BlueFlare2" => NativeProjectileVisual.MonsterBlueFlare2,
                "MagmaBall" => NativeProjectileVisual.MonsterMagmaBall,
                _ => NativeProjectileVisual.Unspecified,
            };
        }

        return behavior switch {
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                => NativeProjectileVisual.ChargedBolt,
            AuthoritativeProjectileBehavior.Firebolt => NativeProjectileVisual.Firebolt,
            AuthoritativeProjectileBehavior.Lightning => NativeProjectileVisual.Lightning,
            AuthoritativeProjectileBehavior.LightningSegment => NativeProjectileVisual.ThinLightning,
            AuthoritativeProjectileBehavior.FlashBottom => NativeProjectileVisual.FlashBottom,
            AuthoritativeProjectileBehavior.FlashTop => NativeProjectileVisual.FlashTop,
            AuthoritativeProjectileBehavior.ResurrectionBeam => NativeProjectileVisual.ResurrectBeam,
            AuthoritativeProjectileBehavior.Phasing => NativeProjectileVisual.Phasing,
            AuthoritativeProjectileBehavior.Fireball => NativeProjectileVisual.Fireball,
            AuthoritativeProjectileBehavior.HolyBolt => NativeProjectileVisual.HolyBolt,
            AuthoritativeProjectileBehavior.BloodStar => NativeProjectileVisual.BloodStar,
            AuthoritativeProjectileBehavior.BoneSpirit => NativeProjectileVisual.BoneSpirit,
            AuthoritativeProjectileBehavior.FireWallSegment when damageType == AuthoritativeDamageType.Lightning => NativeProjectileVisual.LightningWall,
            AuthoritativeProjectileBehavior.FireWallSegment => NativeProjectileVisual.FireWall,
            AuthoritativeProjectileBehavior.FlameWaveController => NativeProjectileVisual.FlameWaveControl,
            AuthoritativeProjectileBehavior.FlameWaveSegment => NativeProjectileVisual.FlameWave,
            AuthoritativeProjectileBehavior.InfernoSegment => NativeProjectileVisual.Inferno,
            AuthoritativeProjectileBehavior.ChainLightningBolt => NativeProjectileVisual.ChainLightning,
            AuthoritativeProjectileBehavior.ApocalypseBoom => NativeProjectileVisual.ApocalypseBoom,
            AuthoritativeProjectileBehavior.Elemental => NativeProjectileVisual.Elemental,
            AuthoritativeProjectileBehavior.GuardianController => NativeProjectileVisual.Guardian,
            AuthoritativeProjectileBehavior.Etherealize => NativeProjectileVisual.Etherealize,
            AuthoritativeProjectileBehavior.Acid => NativeProjectileVisual.Acid,
            AuthoritativeProjectileBehavior.AcidSplat => NativeProjectileVisual.AcidSplat,
            AuthoritativeProjectileBehavior.AcidPuddle => NativeProjectileVisual.AcidPuddle,
            AuthoritativeProjectileBehavior.HorkSpawn => NativeProjectileVisual.HorkSpawn,
            AuthoritativeProjectileBehavior.NovaBall => NativeProjectileVisual.NovaBall,
            AuthoritativeProjectileBehavior.WeaponArrow or AuthoritativeProjectileBehavior.MonsterArrow => NativeProjectileVisual.Arrow,
            _ => NativeProjectileVisual.Unspecified,
        };
    }

    private static string? ResolveNativeMonsterMissileId(NativeProjectileVisual visual)
        => visual switch {
            NativeProjectileVisual.MonsterLightning => "Lightning",
            NativeProjectileVisual.MonsterOrangeFlare => "OrangeFlare",
            NativeProjectileVisual.MonsterBlueFlare => "BlueFlare",
            NativeProjectileVisual.MonsterRedFlare => "RedFlare",
            NativeProjectileVisual.MonsterYellowFlare => "YellowFlare",
            NativeProjectileVisual.MonsterBlueFlare2 => "BlueFlare2",
            NativeProjectileVisual.MonsterMagmaBall => "MagmaBall",
            _ => null,
        };
}
