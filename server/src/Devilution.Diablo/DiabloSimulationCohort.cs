using Devilution.Domain.Protocol;
using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    internal static DiabloCohortCandidate CreateCohortCandidate(
        DiabloSimulationOptions sourceOptions,
        DiabloCohortState state)
    {
        state.ValidateShape();
        ValidateCohortConfiguration(sourceOptions, state.Configuration);
        if (state.WorldInfrastructure.FallbackWidth != sourceOptions.FallbackWidth
            || state.WorldInfrastructure.FallbackHeight != sourceOptions.FallbackHeight)
            throw new InvalidDataException(
                "Cohort fallback dimensions do not match the active Diablo configuration.");
        if ((sourceOptions.World is null) != (state.Geometry is null))
            throw new InvalidDataException(
                "Cohort backing geometry does not match the active world configuration.");

        var world = state.Geometry is null
            ? null
            : RestoreGeometry(state.Geometry);
        var stores = CreateCohortStoreCatalog(sourceOptions.StoreCatalog, state.Stores);
        var participants = state.Participants
            .OrderBy(participant => participant.SessionId, StringComparer.Ordinal)
            .ToArray();
        var targets = state.CombatTargets
            .OrderBy(target => target.Monster.EntityId)
            .Select(target => CreateCombatTarget(target))
            .ToArray();
        var worldItems = state.WorldItems
            .OrderBy(item => item.EntityId)
            .Select(CreateWorldItem)
            .ToArray();
        var objects = state.Objects
            .OrderBy(@object => @object.EntityId)
            .Select(CreateObject)
            .ToArray();
        var quests = state.Quests
            .OrderBy(quest => quest.QuestId)
            .Select(quest => CreateQuest(sourceOptions, quest))
            .ToArray();
        var projectiles = state.Projectiles
            .OrderBy(projectile => projectile.EntityId)
            .Select(CreateProjectile)
            .ToArray();
        var portals = state.Portals
            .OrderBy(portal => portal.PortalId)
            .Select(CreatePortal)
            .ToArray();
        var corpses = state.Corpses
            .OrderBy(corpse => corpse.EntityId)
            .Select(CreateCorpse)
            .ToArray();
        var players = participants
            .Select(participant => CreatePlayer(
                sourceOptions,
                world,
                state.Tick,
                participant))
            .ToArray();
        var setLevelOrigins = state.SetLevelObjectOrigins
            .ToDictionary(
                origin => origin.SetLevelId,
                origin => new AuthoritativeSetLevelObjectOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        var questPieceOrigins = state.ProceduralQuestPieceOrigins
            .ToDictionary(
                origin => origin.OriginId,
                origin => new AuthoritativeProceduralQuestPieceOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        ValidateParticipantEntityOwnership(
            participants,
            targets,
            worldItems,
            objects,
            projectiles,
            corpses);

        var candidateOptions = CloneOptionsForCohort(
            sourceOptions,
            state,
            stores,
            world,
            targets,
            portals,
            worldItems,
            objects,
            quests,
            projectiles,
            corpses,
            setLevelOrigins,
            questPieceOrigins);
        var simulation = DiabloSimulationFactory.Instance.Create(candidateOptions);
        simulation.RestoreCohortDynamicState(state, players);
        simulation.ValidateCohortActorTopology(players);
        simulation.MarkCohortCandidate();

        return new DiabloCohortCandidate(
            simulation,
            participants.Select((participant, index) =>
                new SimulationSession<AuthoritativePlayerState>(
                    participant.SessionId,
                    players[index]))
                .ToArray(),
            state.Tick);
    }

    private static AuthoritativeWorld RestoreGeometry(
        DiabloWorldGeometryState state)
    {
        var world = new AuthoritativeWorld();
        world.RestoreGeometryState(state);
        return world;
    }

    private static void ValidateParticipantEntityOwnership(
        IReadOnlyList<DiabloCohortParticipantState> participants,
        IReadOnlyList<AuthoritativeCombatTarget> targets,
        IReadOnlyList<AuthoritativeWorldItem> worldItems,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        IReadOnlyList<AuthoritativeProjectile> projectiles,
        IReadOnlyList<AuthoritativeCorpse> corpses)
    {
        var liveIds = targets.Select(target => target.EntityId)
            .Concat(worldItems.Select(item => item.EntityId))
            .Concat(objects.Select(@object => @object.EntityId))
            .Concat(projectiles.Select(projectile => projectile.EntityId))
            .Concat(corpses.Select(corpse => corpse.EntityId))
            .ToArray();
        var occupied = new HashSet<uint>();
        foreach (var entityId in liveIds) {
            if (entityId == 0 || !occupied.Add(entityId))
                throw new InvalidDataException("A cohort contains conflicting live entity identities.");
        }
        foreach (var participant in participants) {
            if (participant.EntityId != 0 && occupied.Contains(participant.EntityId))
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' collides with a world entity.");
        }
    }

    private static StoreCatalog CreateCohortStoreCatalog(
        StoreCatalog source,
        IReadOnlyList<DiabloStoreCatalogState> states)
    {
        ArgumentNullException.ThrowIfNull(source);
        var catalog = new StoreCatalog();
        foreach (var definition in source.CaptureAll())
            catalog.AddStore(
                definition.StoreId,
                definition.Items?.Select(CloneStoreItem) ?? [],
                definition.Role);
        catalog.RestoreAll(states);
        return catalog;
    }

    private static StoreItem CloneStoreItem(StoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new StoreItem(
            item.StoreSlot,
            item.ItemSeed,
            item.Price,
            item.State with { });
    }

    private static AuthoritativeWorldItem CreateWorldItem(
        DiabloWorldItemState state)
    {
        var item = new AuthoritativeWorldItem(
            state.EntityId,
            state.LevelId,
            state.PositionX,
            state.PositionY,
            state.ItemSeed,
            state.Price,
            state.State);
        RequireExactActor(state, CaptureWorldItem(item), "world item");
        return item;
    }

    private static AuthoritativeCorpse CreateCorpse(DiabloCorpseState state)
    {
        var corpse = new AuthoritativeCorpse(state.EntityId, state.LevelId, state.PositionX, state.PositionY);
        RequireExactActor(state, new DiabloCorpseState(corpse.EntityId, corpse.LevelId,
            corpse.PositionX, corpse.PositionY), "corpse");
        return corpse;
    }

    private void ValidateCohortActorTopology(IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var target in WorldState.CombatTargets.Values) {
            RequirePosition(target.LevelId, target.PositionX, target.PositionY, "combat target");
            if (target.IsMoving)
                RequirePosition(target.LevelId, target.FuturePositionX, target.FuturePositionY, "moving target");
            if (target.Drop is { } drop)
                RequirePosition(drop.LevelId, drop.PositionX, drop.PositionY, "fixed monster drop");
        }
        foreach (var item in WorldState.WorldItems.Values)
            RequirePosition(item.LevelId, item.PositionX, item.PositionY, "world item");
        foreach (var item in WorldState.Objects.Values)
            RequirePosition(item.LevelId, item.PositionX, item.PositionY, "object");
        foreach (var corpse in WorldState.Corpses.Values)
            RequirePosition(corpse.LevelId, corpse.PositionX, corpse.PositionY, "corpse");
        foreach (var projectile in WorldState.Projectiles.Values) {
            if (!WorldState.ContainsProjectilePosition(projectile.Behavior, projectile.Phase,
                projectile.LevelId, projectile.PositionX, projectile.PositionY))
                throw new InvalidDataException("A cohort projectile lies outside its captured level.");
        }
        var owners = players.Select(player => player.EntityId).ToHashSet();
        if (WorldState.Portals.Count > AuthoritativePortalRules.NativeMaximumPortalCount)
            throw new InvalidDataException("A cohort exceeds the supported portal capacity.");
        foreach (var portal in WorldState.Portals.Values) {
            portal.ValidateDestinationIdentity();
            if (portal.PortalId == 0 || (portal.Bidirectional
                ? portal.OwnerEntityId == 0 || !owners.Contains(portal.OwnerEntityId) || portal.RemainingTicks == 0
                : portal.OwnerEntityId != 0 || portal.RemainingTicks != 0))
                throw new InvalidDataException("A cohort portal has inconsistent ownership or lifetime.");
            RequirePosition(portal.SourceLevelId, portal.SourcePositionX, portal.SourcePositionY, "portal source");
            RequirePosition(portal.DestinationLevelId, portal.DestinationPositionX,
                portal.DestinationPositionY, "portal destination");
        }

        void RequirePosition(uint level, int x, int y, string category)
        {
            if (!WorldState.ContainsPosition(level, x, y))
                throw new InvalidDataException($"A cohort {category} lies outside its captured level.");
        }
    }

    private static AuthoritativeWorldObject CreateObject(DiabloObjectState state)
    {
        var result = new AuthoritativeWorldObject(
            state.EntityId,
            state.ObjectId,
            state.LevelId,
            state.PositionX,
            state.PositionY,
            state.Activated,
            state.QuestId,
            state.EffectKind,
            state.EffectAmount,
            state.QuestProgressAmount,
            state.MissilePassable,
            state.Breakable,
            state.Destroyed,
            state.TrapArmed,
            state.IsShrine,
            state.QuestTransitionKind,
            state.QuestTransitionAmount,
            state.QuestTransitionRequiredVariable1,
            state.QuestTransitionRequiredVariable2,
            state.RewardKind,
            state.RewardId,
            state.RewardItemLevel,
            state.RewardOffsetX,
            state.RewardOffsetY,
            state.ProjectileTargetOffsetX,
            state.ProjectileTargetOffsetY,
            state.QuestSequenceFollowUpKind,
            state.QuestSequenceFollowUpAmount,
            state.QuestSequenceFollowUpOffsetX,
            state.QuestSequenceFollowUpOffsetY,
            state.ProjectileSourceKind,
            state.QuestSequenceFollowUpWorldMutationId,
            state.ActivateOnOccupancy,
            state.RequiredActivatedLinkedObjects,
            state.NativeObjectId,
            state.HideWhenActivated,
            state.AnimationFrame,
            state.ActivatedAnimationFrame,
            state.RewardOriginKind,
            state.RewardSetLevelId,
            state.DialogueMessageId) {
            PresentationKind = state.PresentationKind,
        };
        RequireExactActor(state, CaptureObject(result), "object");
        return result;
    }

    private static AuthoritativePortal CreatePortal(DiabloCohortPortalState state)
    {
        var result = new AuthoritativePortal(
            state.PortalId,
            state.SourceLevelId,
            state.SourcePositionX,
            state.SourcePositionY,
            state.DestinationLevelId,
            state.DestinationPositionX,
            state.DestinationPositionY) {
            DestinationSetLevelId = state.DestinationSetLevelId,
            DestinationSetLevelActiveMap = state.DestinationSetLevelActiveMap,
            DestinationSetLevelSymbol = state.DestinationSetLevelSymbol,
            StableSymbol = state.StableSymbol,
            RemainingTicks = state.RemainingTicks,
            OwnerEntityId = state.OwnerEntityId,
            Bidirectional = state.Bidirectional,
        };
        RequireExactActor(state, CapturePortal(result), "portal");
        return result;
    }

    private static AuthoritativeCombatTarget CreateCombatTarget(
        DiabloCohortCombatTargetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var monster = state.Monster
            ?? throw new InvalidDataException("A cohort combat target is missing its monster state.");
        var drop = state.Drop;
        var target = new AuthoritativeCombatTarget(
            monster.EntityId,
            monster.PositionX,
            monster.PositionY,
            monster.HitPoints,
            monster.ArmorClass,
            monster.MaxHitPoints,
            monster.LevelId,
            monster.MonsterId,
            drop?.EntityId ?? 0,
            drop?.ItemSeed ?? 0,
            drop?.Price ?? 0,
            drop?.State,
            monster.AttackDamage,
            monster.AggroRange,
            monster.FireResistance,
            monster.LightningResistance,
            monster.MagicResistance,
            monster.AcidResistance,
            monster.MonsterLevel,
            moving: false,
            futurePositionX: monster.PositionX,
            futurePositionY: monster.PositionY,
            petrified: monster.Petrified,
            stoneCurseImmune: monster.StoneCurseImmune,
            undead: monster.IsUndead,
            playerMinion: monster.IsPlayerMinion,
            ownerPlayerId: monster.OwnerPlayerId,
            minDamage: monster.MinDamage,
            maxDamage: monster.MaxDamage,
            minDamageSpecial: monster.MinDamageSpecial,
            maxDamageSpecial: monster.MaxDamageSpecial,
            unique: monster.IsUnique,
            diablo: monster.IsDiablo,
            berserkImmune: monster.BerserkImmune,
            deathAnimationTicks: monster.DeathAnimationTicks,
            demon: monster.IsDemon,
            animal: monster.IsAnimal,
            knockback: monster.HasKnockback,
            experienceReward: monster.ExperienceReward,
            toHit: monster.ToHit,
            toHitSpecial: monster.ToHitSpecial,
            nativeAi: monster.NativeAi,
            intelligence: monster.Intelligence,
            gargoyleSpecialActive: monster.GargoyleSpecialActive,
            fallenFearTicks: monster.FallenFearTicks,
            fallenFearDirection: monster.FallenFearDirection,
            sneakHidden: monster.SneakHidden,
            sneakRetreatTicks: monster.SneakRetreatTicks,
            scavengerHealingTicks: monster.ScavengerHealingTicks,
            scavengerCorpseEntityId: monster.ScavengerCorpseEntityId,
            batRetreatTicks: monster.BatRetreatTicks,
            batRetreatDirection: monster.BatRetreatDirection,
            uniqueIdentity: monster.UniqueIdentity,
            counselorRetreatTicks: monster.CounselorRetreatTicks,
            batMovementIdleTicks: monster.BatMovementIdleTicks,
            batMovementMode: monster.BatMovementMode,
            rangedAvoidanceMoveTicks: monster.RangedAvoidanceMoveTicks,
            rangedAvoidanceTurn: monster.RangedAvoidanceTurn,
            rangedAvoidanceMovementIdleTicks: monster.RangedAvoidanceMovementIdleTicks,
            rangedAvoidanceMovementMode: monster.RangedAvoidanceMovementMode,
            rhinoMovementTicks: monster.RhinoMovementTicks,
            rhinoMovementTurn: monster.RhinoMovementTurn,
            avoidanceMovementTicks: monster.AvoidanceMovementTicks,
            avoidanceMovementTurn: monster.AvoidanceMovementTurn,
            avoidanceMovementIdleTicks: monster.AvoidanceMovementIdleTicks,
            avoidanceMovementMode: monster.AvoidanceMovementMode,
            skeletonMeleeDelayTicks: monster.SkeletonMeleeDelayTicks,
            skeletonRangedMovementIdleTicks: monster.SkeletonRangedMovementIdleTicks,
            skeletonRangedMovementMode: monster.SkeletonRangedMovementMode,
            fatMovementIdleTicks: monster.FatMovementIdleTicks,
            fatMovementMode: monster.FatMovementMode,
            rhinoMovementIdleTicks: monster.RhinoMovementIdleTicks,
            rhinoMovementMode: monster.RhinoMovementMode,
            rhinoDelayTicks: monster.RhinoDelayTicks,
            skeletonKingDelayTicks: monster.SkeletonKingDelayTicks,
            horkDemonDelayTicks: monster.HorkDemonDelayTicks,
            rangedAvoidanceDelayTicks: monster.RangedAvoidanceDelayTicks,
            snakeDelayTicks: monster.SnakeDelayTicks,
            counselorDelayTicks: monster.CounselorDelayTicks,
            avoidanceDelayTicks: monster.AvoidanceDelayTicks,
            rangedDelayTicks: monster.RangedDelayTicks,
            megaDelayTicks: monster.MegaDelayTicks,
            direction: monster.Direction,
            dialogueMessageId: monster.DialogueMessageId,
            monsterItemSeed: monster.MonsterItemSeed,
            monsterAiSeed: monster.MonsterAiSeed,
            initialStandAnimationFrame: monster.FallenStandAnimationFrame,
            initialStandAnimationTick: monster.FallenStandAnimationTick,
             fallenRallyTicks: monster.FallenRallyTicks,
             leaderEntityId: monster.LeaderEntityId,
             leaderRelation: monster.LeaderRelation,
             packSize: monster.PackSize,
             activeForTicks: monster.ActiveForTicks,
             dialogueState: monster.DialogueState,
             dropLevelId: drop?.LevelId,
             dropPositionX: drop?.PositionX,
             dropPositionY: drop?.PositionY);

        target.RestoreSkeletonMeleeDelay(monster.SkeletonMeleeDelayTicks);
        target.RestoreSkeletonRangedMovementState(
            monster.SkeletonRangedMovementIdleTicks,
            monster.SkeletonRangedMovementMode);
        target.RestoreFatMovementState(monster.FatMovementIdleTicks, monster.FatMovementMode);
        target.RestoreRhinoMovementState(monster.RhinoMovementIdleTicks, monster.RhinoMovementMode);
        target.RestoreRhinoDelay(monster.RhinoDelayTicks);
        target.RestoreSkeletonKingDelay(monster.SkeletonKingDelayTicks);
        target.RestoreHorkDemonDelay(monster.HorkDemonDelayTicks);
        target.RestoreRangedAvoidanceDelay(monster.RangedAvoidanceDelayTicks);
        target.RestoreSnakeDelay(monster.SnakeDelayTicks);
        target.RestoreCounselorDelay(monster.CounselorDelayTicks);
        target.RestoreAvoidanceDelay(monster.AvoidanceDelayTicks);
        target.RestoreRangedDelay(monster.RangedDelayTicks);
        target.RestoreMegaDelay(monster.MegaDelayTicks);
        target.RestoreFallenFear(monster.FallenFearTicks, monster.FallenFearDirection);
        target.RestoreFallenAnimation(
            monster.FallenStandAnimationFrame,
            monster.FallenStandAnimationTick);
        target.RestoreFallenRally(monster.FallenRallyTicks);
        target.RestoreSneakState(monster.SneakHidden, monster.SneakRetreatTicks);
        target.RestoreScavengerHealing(
            monster.ScavengerHealingTicks,
            monster.ScavengerCorpseEntityId);
        target.RestoreBatRetreat(monster.BatRetreatTicks, monster.BatRetreatDirection);
        target.RestoreBatMovementState(monster.BatMovementIdleTicks, monster.BatMovementMode);
        target.RestoreRangedAvoidanceMovement(
            monster.RangedAvoidanceMoveTicks,
            monster.RangedAvoidanceTurn);
        target.RestoreRangedAvoidanceMovementState(
            monster.RangedAvoidanceMovementIdleTicks,
            monster.RangedAvoidanceMovementMode);
        target.RestoreRhinoMovement(monster.RhinoMovementTicks, monster.RhinoMovementTurn);
        target.RestoreAvoidanceMovement(
            monster.AvoidanceMovementTicks,
            monster.AvoidanceMovementTurn);
        target.RestoreAvoidanceMovementState(
            monster.AvoidanceMovementIdleTicks,
            monster.AvoidanceMovementMode);
        target.RestoreGroupUnity(
            monster.LeaderEntityId,
            monster.LeaderRelation,
            monster.PackSize);
        target.RestoreActiveForTicks(monster.ActiveForTicks);
        target.RestoreHitPointsQ6(monster.HitPointsQ6, monster.MaxHitPointsQ6);
        target.IsBerserked = monster.IsBerserked;
        target.RestoreDeathAnimation(monster.DeathTicksRemaining);
        target.RestoreOccupancy(
            monster.Moving,
            monster.FuturePositionX,
            monster.FuturePositionY,
            monster.Petrified);
        target.RestoreMovementTiming(monster.MovementStartedTick, monster.MovementEffectTick);
        target.RestoreCohortFallenRallyAttackPending(state.FallenRallyAttackPending);
        RequireExactActor(state,
            new DiabloCohortCombatTargetState(target.CaptureCohortState(),
                CaptureOptionalWorldItem(target.Drop), target.FallenRallyAttackPending),
            "combat target");
        return target;
    }

    private static AuthoritativeQuestState CreateQuest(
        DiabloSimulationOptions options,
        DiabloCohortQuestState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ValidateShape();
        uint? expectedNativeQuestId = options.QuestTypes is not null
            && options.QuestTypes.TryGet(state.QuestId, out var definition)
            ? definition.NativeQuestId
            : null;
        if (state.NativeQuestId != expectedNativeQuestId)
            throw new InvalidDataException(
                $"Cohort quest {state.QuestId} has an incompatible native quest identity.");
        var quest = new AuthoritativeQuestState(
            state.QuestId,
            state.LevelId,
            state.RequiredProgress,
            state.Progress,
            false,
            false,
            true,
            false,
            state.Variable1,
            state.Variable2,
            state.MessageId,
            state.CompletionMonsterId,
            state.CompletionUniqueIdentity,
            state.DialogueVariable1,
            state.DialogueVariable2,
            state.CompletionVariable1,
            state.CompletionVariable2,
            state.CompletionActivateQuestId,
            state.ActivateOnRoute,
            state.CompletionDiablo,
            state.CompletionRewardItemId,
            state.CompletionRewardItemLevel,
            turnInTownerId: state.TurnInTownerId,
            turnInLevelId: state.TurnInLevelId,
            turnInPositionX: state.TurnInPositionX,
            turnInPositionY: state.TurnInPositionY,
            turnInItemId: state.TurnInItemId,
            turnInRequiredVariable2: state.TurnInRequiredVariable2,
            turnInRewardUniqueId: state.TurnInRewardUniqueId,
            turnInRewardItemLevel: state.TurnInRewardItemLevel,
            turnInRewardPositionX: state.TurnInRewardPositionX,
            turnInRewardPositionY: state.TurnInRewardPositionY,
            turnInMessageId: state.TurnInMessageId,
            turnInRewardItemId: state.TurnInRewardItemId,
            dialogueItemId: state.DialogueItemId,
            dialogueRewardUniqueId: state.DialogueRewardUniqueId,
            dialogueRewardItemLevel: state.DialogueRewardItemLevel,
            dialogueRewardOffsetX: state.DialogueRewardOffsetX,
            dialogueRewardOffsetY: state.DialogueRewardOffsetY,
            dialogueCompletionMessageId: state.DialogueCompletionMessageId,
            dialogueRewardItemId: state.DialogueRewardItemId,
            dialogueRewardOnActivation: state.DialogueRewardOnActivation,
            turnInOnCompletion: state.TurnInOnCompletion,
            turnInCompletionVariable2: state.TurnInCompletionVariable2,
            turnInCompletionMessageId: state.TurnInCompletionMessageId,
            turnInCompletionRewardUniqueId: state.TurnInCompletionRewardUniqueId,
            turnInCompletionRewardItemLevel: state.TurnInCompletionRewardItemLevel,
            turnInCompletionRewardPositionX: state.TurnInCompletionRewardPositionX,
            turnInCompletionRewardPositionY: state.TurnInCompletionRewardPositionY,
            turnInCompletionRewardItemId: state.TurnInCompletionRewardItemId,
            turnInAlternateItemId: state.TurnInAlternateItemId,
            turnInAlternateMessageId: state.TurnInAlternateMessageId,
            turnInActivationVariable1: state.TurnInActivationVariable1,
            routeActivationVariable1: state.RouteActivationVariable1,
            turnInActivationOnly: state.TurnInActivationOnly,
            turnInCompletionRequiredVariable1: state.TurnInCompletionRequiredVariable1,
            turnInCompletionVariable1: state.TurnInCompletionVariable1,
            turnInDialogueOnly: state.TurnInDialogueOnly,
            turnInRequiredVariable1: state.TurnInRequiredVariable1,
            turnInVariable1: state.TurnInVariable1,
            turnInProgressOnly: state.TurnInProgressOnly,
            turnInBlockedVisitedLevel: state.TurnInBlockedVisitedLevel,
            dialogueCompletionVariable1: state.DialogueCompletionVariable1,
            dialogueCompletionDefeatsTarget: state.DialogueCompletionDefeatsTarget) {
            StableSymbol = state.StableSymbol,
        };
        quest.LevelId = state.LevelId;
        quest.Progress = state.Progress;
        quest.RequiredProgress = state.RequiredProgress;
        quest.Completed = state.Completed;
        quest.Unavailable = state.Unavailable;
        quest.Active = state.Active;
        quest.Logged = state.Logged;
        quest.Variable1 = state.Variable1;
        quest.Variable2 = state.Variable2;
        quest.MessageId = state.MessageId;
        quest.CompletionMonsterId = state.CompletionMonsterId;
        quest.CompletionUniqueIdentity = state.CompletionUniqueIdentity;
        var recaptured = CaptureCohortQuest(quest, options.QuestTypes);
        if (!Equals(state, recaptured))
            throw new InvalidDataException(
                $"Cohort quest {state.QuestId} was normalized during restore.");
        return quest;
    }

    private static AuthoritativeProjectile CreateProjectile(
        DiabloProjectileState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = new AuthoritativeProjectile(
            state.EntityId,
            state.SourceEntityId,
            state.TargetEntityId,
            state.SpellId,
            state.LevelId,
            state.PositionX,
            state.PositionY,
            state.TargetX,
            state.TargetY,
            state.Damage,
            state.DamageType,
            state.AreaRadius,
            state.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = state.Behavior,
            NativeMissileId = state.NativeMissileId,
            Phase = state.Phase,
            StartX = state.StartX,
            StartY = state.StartY,
            TraveledX = state.TraveledX,
            TraveledY = state.TraveledY,
            VelocityX = state.VelocityX,
            VelocityY = state.VelocityY,
            OriginalDirection = state.OriginalDirection,
            PathIndex = state.PathIndex,
            PathTicks = state.PathTicks,
            Speed = state.Speed,
            ImpactLifetime = state.ImpactLifetime,
            ResistantDamageDivisor = state.ResistantDamageDivisor,
            EffectiveSpellLevel = state.EffectiveSpellLevel,
            LastCollisionTargetEntityId = state.LastCollisionTargetEntityId,
            WallLeftX = state.WallLeftX,
            WallLeftY = state.WallLeftY,
            WallRightX = state.WallRightX,
            WallRightY = state.WallRightY,
            WallLeftDirection = state.WallLeftDirection,
            WallRightDirection = state.WallRightDirection,
            WallLeftStopped = state.WallLeftStopped,
            WallRightStopped = state.WallRightStopped,
            WallHitDirection = state.WallHitDirection,
            ApocalypseScanX = state.ApocalypseScanX,
            ApocalypseScanY = state.ApocalypseScanY,
            ApocalypseHit = state.ApocalypseHit,
            RingOfFireLimitReached = state.RingOfFireLimitReached,
            GuardianState = state.GuardianState,
            ElementalOriginX = state.ElementalOriginX,
            ElementalOriginY = state.ElementalOriginY,
            DamageQ6 = state.DamageQ6,
            ElementalFireDamage = state.ElementalFireDamage,
            ElementalLightningDamage = state.ElementalLightningDamage,
            AttackHit = state.AttackHit,
            PerilSelfDamageQ6 = state.PerilSelfDamageQ6,
            DoppelgangerProc = state.DoppelgangerProc,
            ActivationDelayTicks = state.ActivationDelayTicks,
            DamageMinimum = state.DamageMinimum,
            DamageMaximum = state.DamageMaximum,
        };
        RequireExactActor(state, CaptureProjectile(result), "projectile");
        return result;
    }

    private static void RequireExactActor<T>(T expected, T restored, string category)
    {
        if (!Equals(expected, restored))
            throw new InvalidDataException($"Cohort {category} state was normalized during restore.");
    }

    private static AuthoritativePlayerState CreatePlayer(
        DiabloSimulationOptions options,
        AuthoritativeWorld? world,
        ulong cohortTick,
        DiabloCohortParticipantState participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        var snapshot = participant.Player
            ?? throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has no player snapshot.");
        ValidatePlayerSnapshot(options, world, cohortTick, participant);
        AuthoritativePlayerClassDefinition? playerClass = null;
        if (participant.PlayerClassBound == true
            && options.PlayerClasses is not null
            && options.PlayerClasses.TryGet(snapshot.CharacterClassId, out var resolvedPlayerClass))
            playerClass = resolvedPlayerClass;
        var player = new AuthoritativePlayerState(
            snapshot.Gold,
            snapshot.Experience,
            snapshot.Life,
            snapshot.Mana,
            snapshot.ManaMaximum,
            snapshot.Attributes,
            snapshot.Equipment?.Select(CloneEquippedItem).ToArray() ?? [],
            snapshot.InventoryGrid?.ToArray() ?? [],
            snapshot.Belt?.Select(CloneBeltItem).ToArray() ?? [],
            snapshot.PositionX,
            snapshot.PositionY,
            snapshot.LifeMaximum,
            snapshot.CharacterLevel,
            snapshot.LevelId,
            participant.BaseArmorClass,
            snapshot.CharacterClassId,
            snapshot.Inventory?.Select(CloneOwnedItem).ToArray() ?? [],
            snapshot.AbilitySpellId,
            snapshot.MemorizedSpells?.ToArray() ?? [],
            snapshot.FriendlyMode,
            snapshot.CombatMode,
            snapshot.CombatModeEndTick,
            snapshot.HeldItem is null ? null : CloneOwnedItem(snapshot.HeldItem),
            snapshot.FacingDirection,
            snapshot.CharacterName,
            snapshot.ManaShieldActive,
            snapshot.ReflectCharges,
            snapshot.BaseLifeMaximum,
            snapshot.BaseManaMaximum,
            playerClass,
            snapshot.AbilityKind,
            participant.ItemCatalogBound == true ? options.Items : null,
            participant.MaximumAttribute,
            snapshot.DiabloKillLevel,
            snapshot.VisitedLevelIds?.ToArray() ?? [],
            snapshot.StatPoints,
            snapshot.Stash?.Select(CloneStashItem).ToArray() ?? [],
            snapshot.StashGold,
            participant.GoldTopologyEnabled);
        player.EntityId = participant.EntityId != 0
            ? participant.EntityId
            : snapshot.EntityId;
        player.ActiveStoreId = snapshot.ActiveStoreId;
        player.StatusEffects = snapshot.StatusEffects?.Select(effect => effect with { }).ToList() ?? [];
        player.StashOpen = snapshot.StashOpen;
        player.CurrentAction = snapshot.CurrentAction is null
            ? null
            : snapshot.CurrentAction with { };
        player.LastAppliedTick = participant.LastAppliedTick;
        player.AdoptEquipmentAttributeSnapshot();
        player.RestoreCohortDerivedState(
            participant.EffectiveArmorClass,
            participant.AppliedEquipmentAttributeBonuses);
        var recaptured = CaptureRawPlayerSnapshot(player);
        if (!PlayerSnapshotsEqual(snapshot, recaptured)
            || player.EntityId != participant.EntityId
            || player.MaximumAttribute != participant.MaximumAttribute
            || player.BaseArmorClass != participant.BaseArmorClass
            || player.GoldTopologyEnabled != participant.GoldTopologyEnabled
            || participant.PlayerClassBound is not null
                && participant.PlayerClassBound.Value != (player.PlayerClass is not null)
            || participant.ItemCatalogBound is not null
                && participant.ItemCatalogBound.Value != (player.Items is not null)
            || participant.PlayerClassDefinition is not null
                && !Equals(participant.PlayerClassDefinition, player.PlayerClass))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' player state was normalized during restore.");
        return player;
    }

    private static void ValidatePlayerSnapshot(
        DiabloSimulationOptions options,
        AuthoritativeWorld? world,
        ulong cohortTick,
        DiabloCohortParticipantState participant)
    {
        var snapshot = participant.Player
            ?? throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has no player snapshot.");
        if (participant.EntityId == 0
            || snapshot.EntityId == 0
            || participant.EntityId != snapshot.EntityId)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' must carry one non-zero entity identity.");
        if (participant.LastAppliedTick > cohortTick)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' is applied after the cohort tick.");
        if (snapshot.PositionX < 0 || snapshot.PositionY < 0)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has a negative position.");
        if (world is not null) {
            if (!world.ContainsPosition(snapshot.LevelId, snapshot.PositionX, snapshot.PositionY))
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' lies outside the captured level.");
        } else if (snapshot.PositionX >= options.FallbackWidth
            || snapshot.PositionY >= options.FallbackHeight)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' lies outside the fallback world.");
        if (snapshot.Life < 0 || snapshot.Life > snapshot.LifeMaximum
            || snapshot.LifeMaximum < 0
            || snapshot.Mana < 0 || snapshot.Mana > snapshot.ManaMaximum
            || snapshot.ManaMaximum < 0
            || snapshot.BaseLifeMaximum < 0
            || snapshot.BaseManaMaximum < 0)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has inconsistent resources.");
        if (snapshot.CharacterLevel == 0
            || snapshot.LightRadius != AuthoritativeEquipmentLightRules.CalculateLightRadius(
                snapshot.Equipment ?? []))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has inconsistent derived player state.");
        var inventory = snapshot.Inventory ?? [];
        var grid = snapshot.InventoryGrid ?? [];
        if (grid.Any(cell => cell < -1 || cell >= inventory.Count))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has an invalid inventory-grid reference.");
        for (var index = 0; index < inventory.Count; index++) {
            var anchor = -1;
            for (var cell = 0; cell < grid.Count; cell++) {
                if (grid[cell] == index) {
                    anchor = cell;
                    break;
                }
            }
            if (anchor >= 0 && !InventoryGridLayout.HasExactFootprint(
                grid, anchor, index, inventory[index].State.InventoryWidth,
                inventory[index].State.InventoryHeight))
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has an invalid inventory footprint.");
        }
        if (!Enum.IsDefined(snapshot.AbilityKind)
            || !Enum.IsDefined(snapshot.CombatMode)
            || !Enum.IsDefined(snapshot.FacingDirection))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has an invalid player mode.");
        if (snapshot.CurrentAction is not null
            && (!Enum.IsDefined(snapshot.CurrentAction.Kind)
                || !AuthoritativePlayerActionTimingRules.IsOrdered(
                    snapshot.CurrentAction.StartedAtTick,
                    snapshot.CurrentAction.EffectTick,
                    snapshot.CurrentAction.ReadyTick,
                    snapshot.CurrentAction.EndTick)))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has an invalid action lifecycle.");
        if (snapshot.CurrentAction is { Kind: DiabloPlayerActionKind.Cast, CastIntent: null }
            || snapshot.CurrentAction is {
                Kind: DiabloPlayerActionKind.Death or DiabloPlayerActionKind.RestartTown
                    or DiabloPlayerActionKind.LevelTransition,
                EffectApplied: false,
            })
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has an impossible action payload.");
        if (snapshot.CurrentAction?.Kind == DiabloPlayerActionKind.Move) {
            var action = snapshot.CurrentAction;
            if (action.DirectionX is < -1 or > 1
                || action.DirectionY is < -1 or > 1) {
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has an invalid move direction.");
            }
            int futureX;
            int futureY;
            try {
                futureX = checked(snapshot.PositionX + action.DirectionX);
                futureY = checked(snapshot.PositionY + action.DirectionY);
            } catch (OverflowException exception) {
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has an overflowing move destination.",
                    exception);
            }
            if (world is not null
                ? !world.ContainsPosition(snapshot.LevelId, futureX, futureY)
                : futureX < 0
                    || futureY < 0
                    || futureX >= options.FallbackWidth
                    || futureY >= options.FallbackHeight)
                throw new InvalidDataException(
                    $"Participant '{participant.SessionId}' has an out-of-bounds move destination.");
        }
        if (participant.BaseArmorClass < 0 || participant.EffectiveArmorClass < 0)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has invalid armor state.");
        if (participant.PlayerClassBound == true
            ? participant.PlayerClassDefinition is null
            : participant.PlayerClassDefinition is not null)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has incomplete class-policy binding.");
        if (participant.ItemCatalogBound == true && options.Items is null)
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' has an incompatible item-policy binding.");
        if (participant.PlayerClassBound == true
            && (options.PlayerClasses is null
                || !options.PlayerClasses.TryGet(snapshot.CharacterClassId, out _)))
            throw new InvalidDataException(
                $"Participant '{participant.SessionId}' references an unknown player class.");
    }

    private static bool PlayerSnapshotsEqual(
        StorePlayerSnapshot expected,
        StorePlayerSnapshot actual)
    {
        return expected.Gold == actual.Gold
            && expected.ActiveStoreId == actual.ActiveStoreId
            && expected.Experience == actual.Experience
            && expected.Life == actual.Life
            && expected.Mana == actual.Mana
            && expected.ManaMaximum == actual.ManaMaximum
            && Equals(expected.Attributes, actual.Attributes)
            && expected.PositionX == actual.PositionX
            && expected.PositionY == actual.PositionY
            && expected.LifeMaximum == actual.LifeMaximum
            && expected.BaseLifeMaximum == actual.BaseLifeMaximum
            && expected.BaseManaMaximum == actual.BaseManaMaximum
            && expected.CharacterLevel == actual.CharacterLevel
            && expected.LevelId == actual.LevelId
            && expected.EntityId == actual.EntityId
            && expected.CharacterClassId == actual.CharacterClassId
            && string.Equals(expected.CharacterName, actual.CharacterName, StringComparison.Ordinal)
            && expected.ManaShieldActive == actual.ManaShieldActive
            && expected.ReflectCharges == actual.ReflectCharges
            && expected.DiabloKillLevel == actual.DiabloKillLevel
            && expected.StatPoints == actual.StatPoints
            && expected.AbilitySpellId == actual.AbilitySpellId
            && expected.AbilityKind == actual.AbilityKind
            && expected.FriendlyMode == actual.FriendlyMode
            && expected.FacingDirection == actual.FacingDirection
            && expected.CombatMode == actual.CombatMode
            && expected.CombatModeEndTick == actual.CombatModeEndTick
            && expected.StashGold == actual.StashGold
            && expected.StashOpen == actual.StashOpen
            && expected.LightRadius == actual.LightRadius
            && SequenceEqual(expected.Equipment, actual.Equipment)
            && SequenceEqual(expected.InventoryGrid, actual.InventoryGrid)
            && SequenceEqual(expected.Belt, actual.Belt)
            && SequenceEqual(expected.Inventory, actual.Inventory)
            && SequenceEqual(expected.Stash, actual.Stash)
            && SequenceEqual(expected.MemorizedSpells, actual.MemorizedSpells)
            && SequenceEqual(expected.VisitedLevelIds, actual.VisitedLevelIds)
            && SequenceEqual(expected.StatusEffects, actual.StatusEffects)
            && Equals(expected.HeldItem, actual.HeldItem)
            && ActionEqual(expected.CurrentAction, actual.CurrentAction);

        static bool SequenceEqual<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
            => left is not null && right is not null && left.SequenceEqual(right);

        static bool ActionEqual(
            AuthoritativePlayerActionState? left,
            AuthoritativePlayerActionState? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            return left.Kind == right.Kind
                && left.StartedAtTick == right.StartedAtTick
                && left.EffectTick == right.EffectTick
                && left.ReadyTick == right.ReadyTick
                && left.EndTick == right.EndTick
                && left.EffectApplied == right.EffectApplied
                && left.DirectionX == right.DirectionX
                && left.DirectionY == right.DirectionY
                && left.TargetEntityId == right.TargetEntityId
                && left.TargetX == right.TargetX
                && left.TargetY == right.TargetY
                && left.TargetCell == right.TargetCell
                && Equals(left.CastIntent, right.CastIntent);
        }
    }

    private static OwnedStoreItem CloneOwnedItem(OwnedStoreItem item)
        => item with { State = item.State with { } };

    private static EquippedStoreItem CloneEquippedItem(EquippedStoreItem item)
        => item with { State = item.State with { } };

    private static BeltStoreItem CloneBeltItem(BeltStoreItem item)
        => item with { State = item.State with { } };

    private static StashStoreItem CloneStashItem(StashStoreItem item)
        => item with { State = item.State with { } };

    private static void ValidateCohortConfiguration(
        DiabloSimulationOptions options,
        DiabloCohortConfigurationState configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        var activeVariant = options.CombatRules?.GameVariant ?? DiabloGameVariant.Diablo;
        if (configuration.GameMode != options.GameMode
            || configuration.GameVariant != activeVariant
            || configuration.Difficulty != options.Difficulty
            || configuration.UseNativePlayerActionTiming != options.UseNativePlayerActionTiming
            || configuration.InitialRandomSeed != options.RandomSeed
            || configuration.ProceduralMonsterSeed != options.RandomSeed
            || configuration.ProceduralSetPieceOriginSeed != options.RandomSeed
            || configuration.ProceduralQuestPieceOriginSeed != options.RandomSeed
            || configuration.AllowMissingNativeDunLayers != (options.NativeDunFragments is not null)) {
            throw new InvalidDataException(
                "The Diablo cohort configuration does not match the active content configuration.");
        }
        DiabloNativeGeneratedWorldIdentity.RequireSame(
            options.NativeGeneratedWorldIdentity,
            configuration.NativeGeneratedWorldIdentity);
        DiabloCathedralDepthWorldIdentity.RequireSame(
            options.CathedralDepthWorldIdentity,
            configuration.CathedralDepthWorldIdentity);
        if (configuration.NativeGeneratedWorldIdentity is not null
            && configuration.CathedralDepthWorldIdentity is not null)
            throw new InvalidDataException(
                "A Diablo cohort cannot combine ordinary and Cathedral depth identities.");
    }

    private static DiabloSimulationOptions CloneOptionsForCohort(
        DiabloSimulationOptions source,
        DiabloCohortState state,
        StoreCatalog stores,
        AuthoritativeWorld? world,
        IReadOnlyList<AuthoritativeCombatTarget> targets,
        IReadOnlyList<AuthoritativePortal> portals,
        IReadOnlyList<AuthoritativeWorldItem> worldItems,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        IReadOnlyList<AuthoritativeQuestState> quests,
        IReadOnlyList<AuthoritativeProjectile> projectiles,
        IReadOnlyList<AuthoritativeCorpse> corpses,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin> setLevelOrigins,
        IReadOnlyDictionary<uint, AuthoritativeProceduralQuestPieceOrigin> questPieceOrigins)
    {
        var infrastructure = state.WorldInfrastructure;
        return new DiabloSimulationOptions(stores, source.StoreRules) {
            World = world,
            NativeGeneratedWorldIdentity = state.Configuration.NativeGeneratedWorldIdentity,
            CathedralDepthWorldIdentity = state.Configuration.CathedralDepthWorldIdentity,
            WorldMutations = source.WorldMutations,
            WorldRules = source.WorldRules,
            FallbackWidth = source.FallbackWidth,
            FallbackHeight = source.FallbackHeight,
            FallbackBlockedCells = infrastructure.BlockedCells.ToArray(),
            FallbackMissileBlockedCells = infrastructure.MissileBlockedCells.ToArray(),
            CombatTargets = targets,
            Portals = portals,
            WorldItems = worldItems,
            Objects = objects,
            Quests = quests,
            QuestTypes = source.QuestTypes,
            QuestTownerInteractions = source.QuestTownerInteractions,
            QuestDialogueStages = source.QuestDialogueStages,
            QuestRouteMessages = source.QuestRouteMessages,
            TownerGreetings = source.TownerGreetings,
            SetLevelObjects = source.SetLevelObjects,
            SetLevelObjectOrigins = setLevelOrigins,
            ProceduralSetPieceOrigins = source.ProceduralSetPieceOrigins,
            ProceduralQuestPieceOrigins = source.ProceduralQuestPieceOrigins,
            CohortProceduralQuestPieceOrigins = questPieceOrigins,
            NativeDunObjects = source.NativeDunObjects,
            NativeDunMonsters = source.NativeDunMonsters,
            NativeDunFragments = source.NativeDunFragments,
            NativeDunGeometry = source.NativeDunGeometry,
            NativeDunFragmentObjects = source.NativeDunFragmentObjects,
            Content = source.Content,
            RequireExternalContentCatalogs = source.RequireExternalContentCatalogs,
            NativeObjectTemplates = source.NativeObjectTemplates?
                .Select(template => CreateObject(CaptureObject(template)))
                .ToArray(),
            SetLevelMaps = source.SetLevelMaps,
            DialogueMessages = source.DialogueMessages,
            Projectiles = projectiles,
            Corpses = corpses,
            Spells = source.Spells,
            Experience = source.Experience,
            MonsterFlashSpellId = source.MonsterFlashSpellId,
            SpellBooks = source.SpellBooks,
            MonsterMissiles = source.MonsterMissiles,
            NativeMissiles = source.NativeMissiles,
            NativeMissileSprites = source.NativeMissileSprites,
            MonsterTypes = source.MonsterTypes,
            CombatRules = source.CombatRules,
            Items = source.Items,
            ObjectItemGates = source.ObjectItemGates,
            ItemCombinations = source.ItemCombinations,
            ItemQuestEffects = source.ItemQuestEffects,
            IdentifyAbilitySpellId = source.IdentifyAbilitySpellId,
            RepairAbilitySpellId = source.RepairAbilitySpellId,
            RechargeAbilitySpellId = source.RechargeAbilitySpellId,
            TrapDisarmAbilitySpellId = source.TrapDisarmAbilitySpellId,
            AdriaStoreId = source.AdriaStoreId,
            StorytellerStoreId = source.StorytellerStoreId,
            PlayerClasses = source.PlayerClasses,
            PlayerAnimations = source.PlayerAnimations,
            UseNativePlayerActionTiming = source.UseNativePlayerActionTiming,
            RandomSeed = source.RandomSeed,
            GameMode = source.GameMode,
            Difficulty = source.Difficulty,
            ProceduralMonsterSpawns = source.ProceduralMonsterSpawns,
            MonsterSpawnPlanner = source.MonsterSpawnPlanner,
            DeferCohortInitialization = true,
            SuppressInitialWorldScaling = true,
        };
    }

    private void RestoreCohortDynamicState(
        DiabloCohortState state,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (players.Count != state.Participants.Count)
            throw new InvalidDataException("The candidate participant roster is incomplete.");

        setLevelObjectOrigins.Clear();
        foreach (var origin in state.SetLevelObjectOrigins)
            setLevelObjectOrigins.Add(
                origin.SetLevelId,
                new AuthoritativeSetLevelObjectOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        proceduralQuestPieceOrigins.Clear();
        foreach (var origin in state.ProceduralQuestPieceOrigins)
            proceduralQuestPieceOrigins.Add(
                origin.OriginId,
                new AuthoritativeProceduralQuestPieceOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        materializedProceduralLevels.Clear();
        materializedProceduralLevels.UnionWith(state.MaterializedProceduralLevelIds);
        appliedNativeDunFragments.Clear();
        appliedNativeDunFragments.UnionWith(state.AppliedNativeDunFragmentIds);

        storeCatalog.RestoreAll(state.Stores);
        random.Restore(state.RandomState);
        worldCommands.RestoreCohortAllocators(state.Allocators.WorldCommand);
        combatCommands.RestoreCohortAllocators(state.Allocators.CombatCommand);
        nextGeneratedMonsterEntityId = state.Allocators.NextGeneratedMonsterEntityId;
        WorldState.VictoryTriggered = state.VictoryTriggered;
        WorldState.RestoreCohortInfrastructure(state.WorldInfrastructure);

        // The candidate was built with all dynamic dictionaries detached from
        // the source.  No lifecycle replay or level-entry materialization is
        // permitted after this point.
    }

    /**
     * Captures the complete module state at the runtime's exact barrier tick.
     * The runtime callback never advances participants or drains transient
     * events; all returned collections are detached from the live simulation.
     */
    public DiabloCohortState CaptureCohortState(
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        RequireOwnedRuntime(runtime);
        return runtime.CaptureCohort((tick, sessions) => {
            // Acquire this after the Engine barrier: nested Read/Write capture
            // must not invert the lock order against another capture.
            lock (cohortBindingGate)
                return CaptureDetachedCohortState(tick, sessions);
        });
    }

    private DiabloCohortState CaptureDetachedCohortState(
        ulong tick,
        IReadOnlyList<SimulationSession<AuthoritativePlayerState>> sessions)
    {
        RequireSingleCohortRuntimeOwner();
        worldCommands.AssertCohortCaptureBoundary();
        var participants = sessions
            .Select(session => CaptureCohortParticipant(session.Id, session.State))
            .ToArray();
        var infrastructure = WorldState.CaptureCohortInfrastructure();

        var state = new DiabloCohortState(
            DiabloCohortState.CurrentFormatVersion,
            tick,
            new DiabloCohortConfigurationState(
                gameMode,
                CombatRules.GameVariant,
                difficulty,
                useNativePlayerActionTiming,
                initialRandomSeed,
                proceduralMonsterSeed,
                proceduralSetPieceOriginSeed,
                proceduralQuestPieceOriginSeed,
                nativeDunFragments is not null,
                NativeGeneratedWorldIdentity,
                CathedralDepthWorldIdentity),
            participants,
            WorldState.CombatTargets.Values
                .OrderBy(target => target.EntityId)
                .Select(target => new DiabloCohortCombatTargetState(
                    target.CaptureCohortState(),
                    CaptureOptionalWorldItem(target.Drop),
                    target.FallenRallyAttackPending))
                .ToArray(),
            WorldState.WorldItems.Values
                .OrderBy(item => item.EntityId)
                .Select(CaptureWorldItem)
                .ToArray(),
            WorldState.Objects.Values
                .OrderBy(@object => @object.EntityId)
                .Select(CaptureObject)
                .ToArray(),
            WorldState.Quests.Values
                .OrderBy(quest => quest.QuestId)
                .Select(quest => CaptureCohortQuest(quest, QuestTypes))
                .ToArray(),
            WorldState.Projectiles.Values
                .OrderBy(projectile => projectile.EntityId)
                .Select(CaptureProjectile)
                .ToArray(),
            WorldState.Portals.Values
                .OrderBy(portal => portal.PortalId)
                .Select(CapturePortal)
                .ToArray(),
            WorldState.Corpses.Values
                .OrderBy(corpse => corpse.EntityId)
                .Select(corpse => new DiabloCorpseState(
                    corpse.EntityId,
                    corpse.LevelId,
                    corpse.PositionX,
                    corpse.PositionY))
                .ToArray(),
            SetLevelObjectOrigins: setLevelObjectOrigins
                .OrderBy(pair => pair.Key)
                .Select(pair => new DiabloSetLevelObjectOriginState(
                    pair.Key,
                    pair.Value.LevelId,
                    pair.Value.PositionX,
                    pair.Value.PositionY))
                .ToArray(),
            ProceduralQuestPieceOrigins: proceduralQuestPieceOrigins
                .OrderBy(pair => pair.Key)
                .Select(pair => new DiabloProceduralQuestPieceOriginState(
                    pair.Key,
                    pair.Value.LevelId,
                    pair.Value.PositionX,
                    pair.Value.PositionY))
                .ToArray(),
            MaterializedProceduralLevelIds: materializedProceduralLevels
                .OrderBy(levelId => levelId)
                .ToArray(),
            AppliedNativeDunFragmentIds: appliedNativeDunFragments
                .OrderBy(fragmentId => fragmentId)
                .ToArray(),
            Stores: storeCatalog.CaptureAll(),
            Allocators: new DiabloCohortAllocatorState(
                worldCommands.CaptureCohortAllocators(),
                combatCommands.CaptureCohortAllocators(),
                nextGeneratedMonsterEntityId),
            RandomState: random.State,
            VictoryTriggered: WorldState.VictoryTriggered,
            Geometry: WorldState.World?.CaptureGeometryState(),
            WorldInfrastructure: infrastructure);
        state.ValidateShape();
        return state;
    }

    private DiabloCohortParticipantState CaptureCohortParticipant(
        string sessionId,
        AuthoritativePlayerState player)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(player);
        if (player.Items is not null && !ReferenceEquals(player.Items, Items))
            throw new InvalidDataException(
                $"Participant '{sessionId}' uses an item catalog outside the module policy.");
        if (player.PlayerClass is not null
            && (PlayerClasses is null
                || !PlayerClasses.TryGet(player.CharacterClassId, out var playerClass)
                || !ReferenceEquals(player.PlayerClass, playerClass))) {
            throw new InvalidDataException(
                $"Participant '{sessionId}' uses a player-class definition outside the module catalog.");
        }
        return new DiabloCohortParticipantState(
            sessionId,
            player.EntityId,
            CaptureRawPlayerSnapshot(player),
            player.LastAppliedTick,
            player.BaseArmorClass,
            player.MaximumAttribute,
            player.GoldTopologyEnabled,
            player.ArmorClass,
            player.CaptureAppliedEquipmentAttributeBonuses(),
            player.PlayerClass is not null,
            player.Items is not null,
            player.PlayerClass);
    }

    private static StorePlayerSnapshot CaptureRawPlayerSnapshot(
        AuthoritativePlayerState player)
    {
        return new StorePlayerSnapshot(
            player.Gold,
            player.ActiveStoreId,
            player.Inventory.Select(CloneOwnedItem).ToArray(),
            player.Experience,
            player.Life,
            player.Mana,
            player.ManaMaximum,
            CloneAttributes(player.Attributes),
            player.Equipment.Select(CloneEquippedItem).ToArray(),
            player.InventoryGrid.ToArray(),
            player.Belt.Select(CloneBeltItem).ToArray()) {
            PositionX = player.PositionX,
            PositionY = player.PositionY,
            LifeMaximum = player.LifeMaximum,
            BaseLifeMaximum = player.BaseLifeMaximum,
            BaseManaMaximum = player.BaseManaMaximum,
            CharacterLevel = player.CharacterLevel,
            LevelId = player.LevelId,
            EntityId = player.EntityId,
            CharacterClassId = player.CharacterClassId,
            CharacterName = player.CharacterName,
            ManaShieldActive = player.ManaShieldActive,
            ReflectCharges = player.ReflectCharges,
            DiabloKillLevel = player.DiabloKillLevel,
            StatPoints = player.StatPoints,
            VisitedLevelIds = player.VisitedLevelIds.Order().ToArray(),
            AbilitySpellId = player.AbilitySpellId,
            AbilityKind = player.AbilityKind,
            MemorizedSpells = player.MemorizedSpells.ToArray(),
            StatusEffects = player.StatusEffects.ToArray(),
            FriendlyMode = player.FriendlyMode,
            FacingDirection = player.FacingDirection,
            CombatMode = player.CombatMode,
            CombatModeEndTick = player.CombatModeEndTick,
            CurrentAction = player.CurrentAction is null
                ? null
                : player.CurrentAction with { },
            HeldItem = player.HeldItem is null ? null : CloneOwnedItem(player.HeldItem),
            Stash = player.Stash.Select(CloneStashItem).ToArray(),
            StashGold = player.StashGold,
            StashOpen = player.StashOpen,
            LightRadius = player.LightRadius,
        };
    }

    private static PlayerAttributesState CloneAttributes(PlayerAttributesState attributes)
        => new(
            attributes.Strength with { },
            attributes.Magic with { },
            attributes.Dexterity with { },
            attributes.Vitality with { });

    private static DiabloWorldItemState? CaptureOptionalWorldItem(
        AuthoritativeWorldItem? item)
    {
        return item is null
            ? null
            : new DiabloWorldItemState(
                item.EntityId,
                item.LevelId,
                item.PositionX,
                item.PositionY,
                item.ItemSeed,
                item.Price,
                item.State);
    }

    private static DiabloWorldItemState CaptureWorldItem(
        AuthoritativeWorldItem item)
        => CaptureOptionalWorldItem(item)!;

    private static DiabloObjectState CaptureObject(AuthoritativeWorldObject @object)
    {
        return new DiabloObjectState(
            @object.EntityId,
            @object.ObjectId,
            @object.LevelId,
            @object.PositionX,
            @object.PositionY,
            @object.Activated,
            @object.QuestId,
            @object.EffectKind,
            @object.EffectAmount) {
            QuestProgressAmount = @object.QuestProgressAmount,
            MissilePassable = @object.MissilePassable,
            Breakable = @object.Breakable,
            Destroyed = @object.Destroyed,
            TrapArmed = @object.TrapArmed,
            IsShrine = @object.IsShrine,
            QuestTransitionKind = @object.QuestTransitionKind,
            QuestTransitionAmount = @object.QuestTransitionAmount,
            QuestTransitionRequiredVariable1 = @object.QuestTransitionRequiredVariable1,
            QuestTransitionRequiredVariable2 = @object.QuestTransitionRequiredVariable2,
            RewardKind = @object.RewardKind,
            RewardId = @object.RewardId,
            RewardItemLevel = @object.RewardItemLevel,
            RewardOffsetX = @object.RewardOffsetX,
            RewardOffsetY = @object.RewardOffsetY,
            RewardOriginKind = @object.RewardOriginKind,
            RewardSetLevelId = @object.RewardSetLevelId,
            ProjectileTargetOffsetX = @object.ProjectileTargetOffsetX,
            ProjectileTargetOffsetY = @object.ProjectileTargetOffsetY,
            QuestSequenceFollowUpKind = @object.QuestSequenceFollowUpKind,
            QuestSequenceFollowUpAmount = @object.QuestSequenceFollowUpAmount,
            QuestSequenceFollowUpOffsetX = @object.QuestSequenceFollowUpOffsetX,
            QuestSequenceFollowUpOffsetY = @object.QuestSequenceFollowUpOffsetY,
            ProjectileSourceKind = @object.ProjectileSourceKind,
            QuestSequenceFollowUpWorldMutationId = @object.QuestSequenceFollowUpWorldMutationId,
            ActivateOnOccupancy = @object.ActivateOnOccupancy,
            RequiredActivatedLinkedObjects = @object.RequiredActivatedLinkedObjects,
            NativeObjectId = @object.NativeObjectId,
            PresentationKind = @object.PresentationKind,
            HideWhenActivated = @object.HideWhenActivated,
            AnimationFrame = @object.AnimationFrame,
            ActivatedAnimationFrame = @object.ActivatedAnimationFrame,
            DialogueMessageId = @object.DialogueMessageId,
        };
    }

    private static DiabloCohortQuestState CaptureCohortQuest(
        AuthoritativeQuestState quest,
        AuthoritativeQuestTypeCatalog? questTypes)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return new DiabloCohortQuestState(
            quest.QuestId,
            quest.LevelId,
            quest.RequiredProgress,
            quest.Progress,
            quest.Completed,
            quest.Unavailable,
            quest.Active,
            quest.Logged,
            quest.Variable1,
            quest.Variable2,
            quest.MessageId,
            quest.CompletionMonsterId,
            quest.CompletionUniqueIdentity,
            quest.DialogueVariable1,
            quest.DialogueVariable2,
            quest.DialogueCompletionVariable1,
            quest.CompletionVariable1,
            quest.CompletionVariable2,
            quest.CompletionActivateQuestId,
            quest.ActivateOnRoute,
            quest.RouteActivationVariable1,
            quest.CompletionDiablo,
            quest.CompletionRewardItemId,
            quest.CompletionRewardItemLevel,
            quest.TurnInTownerId,
            quest.TurnInLevelId,
            quest.TurnInPositionX,
            quest.TurnInPositionY,
            quest.TurnInItemId,
            quest.TurnInRequiredVariable2,
            quest.TurnInRewardUniqueId,
            quest.TurnInRewardItemLevel,
            quest.TurnInRewardPositionX,
            quest.TurnInRewardPositionY,
            quest.TurnInRewardItemId,
            quest.TurnInMessageId,
            quest.TurnInActivationOnly,
            quest.TurnInOnCompletion,
            quest.TurnInCompletionVariable2,
            quest.TurnInCompletionMessageId,
            quest.TurnInCompletionRewardUniqueId,
            quest.TurnInCompletionRewardItemLevel,
            quest.TurnInCompletionRewardPositionX,
            quest.TurnInCompletionRewardPositionY,
            quest.TurnInCompletionRewardItemId,
            quest.TurnInAlternateItemId,
            quest.TurnInAlternateMessageId,
            quest.TurnInActivationVariable1,
            quest.TurnInCompletionRequiredVariable1,
            quest.TurnInCompletionVariable1,
            quest.TurnInDialogueOnly,
            quest.TurnInRequiredVariable1,
            quest.TurnInVariable1,
            quest.TurnInProgressOnly,
            quest.TurnInBlockedVisitedLevel,
            quest.DialogueItemId,
            quest.DialogueRewardUniqueId,
            quest.DialogueRewardItemLevel,
            quest.DialogueRewardOffsetX,
            quest.DialogueRewardOffsetY,
            quest.DialogueCompletionMessageId,
            quest.DialogueRewardItemId,
            quest.DialogueRewardOnActivation,
            quest.DialogueCompletionDefeatsTarget,
            quest.StableSymbol,
            questTypes is not null && questTypes.TryGet(quest.QuestId, out var definition)
                ? definition.NativeQuestId
                : null);
    }

    private static DiabloProjectileState CaptureProjectile(
        AuthoritativeProjectile projectile)
    {
        return new DiabloProjectileState(
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
            projectile.DamageType,
            projectile.AreaRadius,
            projectile.RemainingTicks) {
            Behavior = projectile.Behavior,
            NativeMissileId = projectile.NativeMissileId,
            Phase = projectile.Phase,
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
            EffectiveSpellLevel = projectile.EffectiveSpellLevel,
            LastCollisionTargetEntityId = projectile.LastCollisionTargetEntityId,
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
            GuardianState = projectile.GuardianState,
            ElementalOriginX = projectile.ElementalOriginX,
            ElementalOriginY = projectile.ElementalOriginY,
            DamageQ6 = projectile.DamageQ6,
            ElementalFireDamage = projectile.ElementalFireDamage,
            ElementalLightningDamage = projectile.ElementalLightningDamage,
            AttackHit = projectile.AttackHit,
            PerilSelfDamageQ6 = projectile.PerilSelfDamageQ6,
            DoppelgangerProc = projectile.DoppelgangerProc,
            ActivationDelayTicks = projectile.ActivationDelayTicks,
            DamageMinimum = projectile.DamageMinimum,
            DamageMaximum = projectile.DamageMaximum,
        };
    }

    private static DiabloCohortPortalState CapturePortal(AuthoritativePortal portal)
    {
        return new DiabloCohortPortalState(
            portal.PortalId,
            portal.SourceLevelId,
            portal.SourcePositionX,
            portal.SourcePositionY,
            portal.DestinationLevelId,
            portal.DestinationPositionX,
            portal.DestinationPositionY,
            portal.DestinationSetLevelId,
            portal.DestinationSetLevelActiveMap,
            portal.DestinationSetLevelSymbol,
            portal.StableSymbol,
            portal.RemainingTicks,
            portal.OwnerEntityId,
            portal.Bidirectional);
    }
}
