using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Server.Snapshots;
using AuthoritativeMonsterTypeCatalog = Devilution.Diablo.AuthoritativeMonsterTypeCatalog;
using AuthoritativeProjectileBehavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Protocol;

/** Maps transport-neutral Diablo state and events to the versioned wire schema. */
internal static partial class DiabloProtocolMapper
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
        bool allowZeroPlayerEntityId = false,
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null)
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
        ValidateProjectedProjectileStates(source.Projectiles, projectedLevels, monsterMissiles);
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
            var nativeShrine = source.NativeShrineTypes.SingleOrDefault(value => value.EntityId == @object.EntityId);
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
                // Generated native owners predate the authored-shrine flag; keep saved object identity unchanged.
                IsShrine = @object.IsShrine || nativeShrine.EntityId != 0,
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
                NativeShrineInteractable = source.NativeShrineInteractableEntityIds.Contains(@object.EntityId),
                NativeShrineType = (uint)nativeShrine.Type,
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
                SpellId = ProjectedProjectileSpellId(projectile, monsterMissiles),
                LevelId = projectile.LevelId,
                PositionX = projectile.PositionX,
                PositionY = projectile.PositionY,
                TargetX = ProjectedProjectileTarget(projectile).X,
                TargetY = ProjectedProjectileTarget(projectile).Y,
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
                    : NativeMissileIdentityOf((AuthoritativeProjectileBehavior)projectile.Behavior) is { } identity
                        && projectile.NativeMissileId == identity ? identity : null,
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
                var dialogueText = !string.IsNullOrEmpty(gameplayEvent.Text)
                    ? gameplayEvent.Text : dialogueMessages is not null
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

}
