using Devilution.Domain.Protocol;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Shared;
using System.Text.Json.Serialization;

namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /** Restores validated module state without knowing its serialized representation. */
    public void RestoreSnapshot(
        AuthoritativePlayerState destination,
        DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (NativeMonsterMetadataFingerprint is not null || snapshot.NativeMonsterMetadataFingerprint is not null
            || snapshot.Monsters.Any(monster => monster.NativeInitializationPresent))
            throw new InvalidDataException("Native-initialized actors require complete cohort restore, not a legacy projection.");
        // Reject a different generation profile before rollback capture or any mutation.
        DiabloNativeGeneratedWorldIdentity.RequireSame(
            NativeGeneratedWorldIdentity, snapshot.NativeGeneratedWorldIdentity);
        DiabloCathedralDepthWorldIdentity.RequireSame(
            CathedralDepthWorldIdentity, snapshot.CathedralDepthWorldIdentity);
        WorldState.RequireCathedralDoorProfile(snapshot.CathedralDoors);
        DiabloCathedralDoorCheckpoint.RequireSnapshotCopies(snapshot);
        // The incoming snapshot may be older than the live destination. Capture
        // rollback state at a tick that can represent any action already in
        // progress, otherwise validation of the rollback snapshot can fail and
        // mask the original rejection with an AggregateException.
        var previous = CaptureSnapshot(
            destination,
            GetRollbackSnapshotTick(destination, snapshot.Tick));
        try {
            RestoreSnapshotCore(destination, snapshot);
        }
        catch (Exception exception) {
            try {
                RestoreSnapshotCore(destination, previous);
            } catch (Exception rollbackException) {
                throw new AggregateException(
                    "The authoritative snapshot restore failed and its rollback also failed.",
                    exception,
                    rollbackException);
            }
            throw;
        }
    }

    private static ulong GetRollbackSnapshotTick(
        AuthoritativePlayerState player,
        ulong requestedTick)
    {
        var rollbackTick = requestedTick;
        if (player.CurrentAction is not { } action)
            return rollbackTick;

        rollbackTick = Math.Max(rollbackTick, action.StartedAtTick);
        if (action.EffectApplied)
            rollbackTick = Math.Max(rollbackTick, action.EffectTick);
        return rollbackTick;
    }

    private void RestoreSnapshotCore(
        AuthoritativePlayerState destination,
        DiabloSimulationSnapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.Difficulty)
            || snapshot.Difficulty != Difficulty) {
            throw new InvalidDataException(
                "The authoritative save difficulty does not match the active session.");
        }
        var source = snapshot.Player
            ?? throw new InvalidDataException("The authoritative save does not contain player state.");
        if (snapshot.Monsters is null
            || snapshot.WorldItems is null
            || snapshot.Objects is null
            || snapshot.Quests is null
            || snapshot.Projectiles is null
            || snapshot.Portals is null
            || snapshot.Corpses is null
            || snapshot.ActiveWorldMutationIds is null
            || snapshot.SetLevelObjectOrigins is null
            || snapshot.ProceduralQuestPieceOrigins is null
            || snapshot.MaterializedProceduralLevelIds is null) {
            throw new InvalidDataException(
                "The authoritative save contains incomplete world state.");
        }
        var restoredSetLevelObjectOrigins = ValidateSavedSetLevelObjectOrigins(
            snapshot.SetLevelObjectOrigins);
        var restoredProceduralQuestPieceOrigins =
            ValidateSavedProceduralQuestPieceOrigins(snapshot.ProceduralQuestPieceOrigins);
        var restoredMaterializedProceduralLevelIds =
            ValidateSavedMaterializedProceduralLevelIds(
                snapshot.MaterializedProceduralLevelIds);
        setLevelObjectOrigins.Clear();
        foreach (var origin in restoredSetLevelObjectOrigins)
            setLevelObjectOrigins.Add(
                origin.SetLevelId,
                new AuthoritativeSetLevelObjectOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        proceduralQuestPieceOrigins.Clear();
        foreach (var origin in restoredProceduralQuestPieceOrigins)
            proceduralQuestPieceOrigins.Add(
                origin.OriginId,
                new AuthoritativeProceduralQuestPieceOrigin(
                    origin.LevelId,
                    origin.PositionX,
                    origin.PositionY));
        source = ResolveSnapshotItemFootprints(source);
        snapshot = snapshot with {
            Player = source,
            WorldItems = snapshot.WorldItems
                .Select(ResolveWorldItemState)
                .ToArray(),
            ActiveStore = ResolveStoreState(snapshot.ActiveStore),
        };
        WorldState.RestoreWorldMutations(snapshot.ActiveWorldMutationIds);
        WorldState.RestoreCathedralDoors(snapshot.CathedralDoors, snapshot.Objects);
        ValidateSavedPlayer(source, destination, snapshot.Tick);

        var monsterEntityIds = snapshot.Monsters.Select(monster => monster.EntityId).ToArray();
        var worldItemEntityIds = snapshot.WorldItems.Select(item => item.EntityId).ToArray();
        var objectEntityIds = snapshot.Objects.Select(@object => @object.EntityId).ToArray();
        var projectileEntityIds = snapshot.Projectiles.Select(projectile => projectile.EntityId).ToArray();
        var corpseEntityIds = snapshot.Corpses.Select(corpse => corpse.EntityId).ToArray();
        var allWorldEntityIds = monsterEntityIds
            .Concat(worldItemEntityIds)
            .Concat(objectEntityIds)
            .Concat(projectileEntityIds)
            .Concat(corpseEntityIds)
            .ToArray();
        if (monsterEntityIds.Any(entityId => entityId == 0)
            || worldItemEntityIds.Any(entityId => entityId == 0)
            || objectEntityIds.Any(entityId => entityId == 0)
            || projectileEntityIds.Any(entityId => entityId == 0)
            || corpseEntityIds.Any(entityId => entityId == 0)
            || monsterEntityIds.Distinct().Count() != monsterEntityIds.Length
            || worldItemEntityIds.Distinct().Count() != worldItemEntityIds.Length
            || objectEntityIds.Distinct().Count() != objectEntityIds.Length
            || projectileEntityIds.Distinct().Count() != projectileEntityIds.Length
            || corpseEntityIds.Distinct().Count() != corpseEntityIds.Length
            || allWorldEntityIds.Distinct().Count() != allWorldEntityIds.Length
            || allWorldEntityIds.Contains(source.EntityId)
            || snapshot.Monsters.Count > 4096
            || snapshot.WorldItems.Count > 4096
            || snapshot.Objects.Count > 4096
            || snapshot.Projectiles.Count > 4096
            || snapshot.Corpses.Count > 4096) {
            throw new InvalidDataException(
                "The authoritative save contains invalid world entity identity.");
        }
        foreach (var group in snapshot.Monsters.GroupBy(monster => monster.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"The authoritative save contains more than {AuthoritativeLevel.NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }

        var savedPortals = snapshot.Portals
            .Select(portal => {
                if (portal.PortalId == 0
                    || !WorldState.IsWalkable(
                        portal.SourceLevelId,
                        portal.SourcePositionX,
                        portal.SourcePositionY)
                    || !WorldState.IsWalkable(
                        portal.DestinationLevelId,
                        portal.DestinationPositionX,
                        portal.DestinationPositionY)
                    || (portal.Bidirectional
                        && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0))
                    || (!portal.Bidirectional && portal.OwnerEntityId != 0)
                    || (!portal.Bidirectional && portal.RemainingTicks != 0)) {
                    throw new InvalidDataException(
                        "The authoritative save contains an invalid portal.");
                }
                return portal;
            })
            .ToArray();
        if (savedPortals.Length > AuthoritativePortalRules.NativeMaximumPortalCount
            || savedPortals.Select(portal => portal.PortalId).Distinct().Count() != savedPortals.Length) {
            throw new InvalidDataException(
                "The authoritative save contains invalid or excessive portal state.");
        }

        var restoredCorpses = snapshot.Corpses.Select(corpse => {
            if (!WorldState.IsWalkable(corpse.LevelId, corpse.PositionX, corpse.PositionY))
                throw new InvalidDataException(
                    "The authoritative save contains an invalid corpse.");
            return new AuthoritativeCorpse(
                corpse.EntityId,
                corpse.LevelId,
                corpse.PositionX,
                corpse.PositionY);
        }).ToArray();

        var restoredWorldItems = snapshot.WorldItems.Select(item => {
            if (item.State is null
                || !IsNativeItemPrice(item.Price)
                || !HasKnownStableItemIdentity(item.State)
                || !HasValidItemIdentity(item.ItemSeed, item.State)
                || !WorldState.IsWalkable(item.LevelId, item.PositionX, item.PositionY)
                || IsMalformedGoldWorldItem(item)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid world item.");
            }
            return new AuthoritativeWorldItem(
                item.EntityId,
                item.LevelId,
                item.PositionX,
                item.PositionY,
                item.ItemSeed,
                item.Price,
                item.State);
        }).ToArray();
        var restoredMonsters = snapshot.Monsters.Select(monster => {
            if (!WorldState.IsWalkable(monster.LevelId, monster.PositionX, monster.PositionY)
                || monster.HitPoints < 0
                || monster.MaxHitPoints < monster.HitPoints
                || monster.MaxHitPoints < 0
                || monster.HitPointsQ6 < 0
                || monster.MaxHitPointsQ6 < monster.HitPointsQ6
                || monster.HitPoints != DeterministicFixedPointQ6.ToWholeCeiling(monster.HitPointsQ6)
                || monster.MaxHitPoints != DeterministicFixedPointQ6.ToWholeCeiling(monster.MaxHitPointsQ6)
                || monster.AttackDamage < 0
                || (!monster.NativeInitializationPresent
                    && (monster.MonsterItemSeed == 0) != (monster.MonsterAiSeed == 0))
                || monster.MinDamage < 0
                || monster.MaxDamage < 0
                || monster.MinDamageSpecial < 0
                || monster.MaxDamageSpecial < 0
                || monster.MaxDamage < monster.MinDamage
                || monster.MaxDamageSpecial < monster.MinDamageSpecial
                || monster.AggroRange < 0
                || monster.MonsterLevel < 0
                || monster.DeathAnimationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDeathAnimationTicks
                || monster.ExperienceReward > NativeCompatibilityLimits.MaximumNativeMonsterExperienceReward
                || monster.ToHit is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterToHit
                || monster.ToHitSpecial is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterToHit
                || monster.Intelligence is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterIntelligence
                || !NativeTextLimits.IsIdentityWithinLimit(monster.UniqueIdentity)
                || (!string.IsNullOrEmpty(monster.UniqueIdentity) && !monster.IsUnique)
                || (monster.DialogueMessageId != 0 && !monster.IsUnique)
                || !Enum.IsDefined(monster.DialogueState)
                || (monster.DialogueState != AuthoritativeMonsterDialogueState.Unspecified
                    && !monster.IsUnique)
                || !Enum.IsDefined(monster.LeaderRelation)
                || (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.None
                    ? monster.LeaderEntityId != 0
                    : monster.LeaderEntityId == 0)
                || (monster.LeaderRelation != AuthoritativeMonsterLeaderRelation.None
                    && monster.IsUnique)
                || monster.PackSize is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterPackSize
                || monster.ActiveForTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterActiveTicks
                || (!string.IsNullOrEmpty(monster.NativeAi)
                    && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(monster.NativeAi))
                || monster.FallenFearTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.FallenFearDirection is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection
                || monster.FallenStandAnimationFrame is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationFrame
                || monster.FallenStandAnimationTick is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationTick
                || monster.FallenRallyTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenRallyTicks
                || monster.Direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection
                || monster.DialogueMessageId is < 0 or > short.MaxValue
                || monster.SneakRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks
                || monster.ScavengerHealingTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks
                || monster.BatRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks
                || monster.BatRetreatDirection is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection
                || monster.BatMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.BatMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.RangedAvoidanceMoveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks
                || monster.RangedAvoidanceTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn
                || monster.RangedAvoidanceMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.RangedAvoidanceMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.RhinoMovementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks
                || monster.RhinoMovementTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks
                || monster.AvoidanceMovementTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.AvoidanceMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.SkeletonMeleeDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonRangedMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonRangedMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.FatMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.FatMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.RhinoMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.RhinoMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode
                || monster.RhinoDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonKingDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.HorkDemonDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.HorkDemonMoveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks
                || monster.HorkDemonMoveTurn is < 0 or > 1
                || monster.RangedAvoidanceDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.SnakeDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.CounselorDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.AvoidanceDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.RangedDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.MegaDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || monster.CounselorRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks
                || monster.DeathTicksRemaining is < 0
                || monster.DeathTicksRemaining > monster.DeathAnimationTicks
                || (monster.HitPointsQ6 > 0 && monster.DeathTicksRemaining != 0)
                || (monster.Moving
                    && (!WorldState.IsWalkable(
                            monster.LevelId,
                            monster.FuturePositionX,
                            monster.FuturePositionY)
                        || DeterministicGridMetrics.ChebyshevDistance(
                            monster.FuturePositionX,
                            monster.FuturePositionY,
                            monster.PositionX,
                            monster.PositionY) != 1))
                || (!monster.Moving
                    && (monster.FuturePositionX != monster.PositionX
                        || monster.FuturePositionY != monster.PositionY))
                || (!monster.Moving
                    && (monster.MovementStartedTick != 0
                        || monster.MovementEffectTick != 0))
                || (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0)
                    && monster.MovementEffectTick <= monster.MovementStartedTick
                || monster.FireResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
                || monster.LightningResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
                || monster.MagicResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
                || monster.AcidResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
                || (!monster.IsPlayerMinion && monster.OwnerPlayerId != 0)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid monster.");
            }
            return monster;
        }).ToArray();
        var restoredMonsterById = restoredMonsters.ToDictionary(monster => monster.EntityId);
        foreach (var monster in restoredMonsters.Where(monster =>
                     monster.LeaderRelation != AuthoritativeMonsterLeaderRelation.None)) {
            if (!restoredMonsterById.TryGetValue(monster.LeaderEntityId, out var leader)
                || leader.EntityId == monster.EntityId
                || leader.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid monster pack leader.");
            }
        }
        var restoredCorpseIds = restoredCorpses
            .Select(corpse => corpse.EntityId)
            .ToHashSet();
        if (restoredMonsters.Any(monster => monster.ScavengerCorpseEntityId != 0
                && !restoredCorpseIds.Contains(monster.ScavengerCorpseEntityId))) {
            throw new InvalidDataException(
                "The authoritative save contains an unknown Scavenger corpse.");
        }
        var restoredObjects = snapshot.Objects.Select(@object => {
            if ((!WorldState.IsWalkable(@object.LevelId, @object.PositionX, @object.PositionY)
                    && !WorldState.IsCathedralDoorObject(@object.EntityId, @object.LevelId,
                        @object.PositionX, @object.PositionY))
                || @object.ObjectId == 0
                || @object.EffectAmount < 0
                || !Enum.IsDefined(@object.EffectKind)
                || !Enum.IsDefined(@object.QuestTransitionKind)
                || @object.QuestTransitionAmount is < 0 or > byte.MaxValue
                || @object.QuestTransitionRequiredVariable1 is < -1 or > byte.MaxValue
                || @object.QuestTransitionRequiredVariable2 is < -1 or > byte.MaxValue
                || !AuthoritativeWorldObject.IsValidQuestTransition(
                    @object.QuestTransitionKind,
                    @object.QuestTransitionAmount,
                    @object.QuestTransitionRequiredVariable1,
                    @object.QuestTransitionRequiredVariable2,
                    @object.QuestId)
                || !AuthoritativeWorldObject.IsValidReward(
                    @object.RewardKind,
                    @object.RewardId,
                    @object.RewardItemLevel,
                    @object.RewardOffsetX,
                    @object.RewardOffsetY,
                    @object.RewardOriginKind,
                    @object.RewardSetLevelId)
                || !AuthoritativeWorldObject.IsValidProjectileTargetOffset(
                    @object.EffectKind,
                    @object.ProjectileTargetOffsetX,
                    @object.ProjectileTargetOffsetY)
                || !AuthoritativeWorldObject.IsValidQuestSequenceFollowUp(
                    @object.EffectKind,
                    @object.QuestSequenceFollowUpKind,
                    @object.QuestSequenceFollowUpAmount,
                    @object.QuestSequenceFollowUpOffsetX,
                    @object.QuestSequenceFollowUpOffsetY)
                || !AuthoritativeWorldObject.IsValidProjectileSource(
                    @object.EffectKind,
                    @object.QuestSequenceFollowUpKind,
                    @object.ProjectileSourceKind)
                || !AuthoritativeWorldObject.IsValidPresentation(
                    @object.HideWhenActivated,
                    @object.AnimationFrame,
                    @object.ActivatedAnimationFrame)
                || !Enum.IsDefined(@object.PresentationKind)
                || !AuthoritativeWorldObject.IsValidOccupancyTrigger(
                    @object.ActivateOnOccupancy,
                    @object.RequiredActivatedLinkedObjects,
                    @object.QuestId)
                || (@object.Breakable && @object.MissilePassable)
                || (@object.Destroyed && !@object.Breakable)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid object position.");
            }
            return @object;
        }).ToArray();
        var restoredProjectiles = snapshot.Projectiles.Select(projectile => {
            projectile.NativeTrapArrow?.ValidateShape(projectile);
            var familiarLightning = projectile.NativeMonsterMissile?.Kind == AuthoritativeNativeMonsterMissileKind.Lightning;
            projectile.NativeMonsterMissile?.ValidateShape(projectile);
            if (projectile.NativeTrapArrow is not null
                && (!WorldState.OwnsNativePopulationLevel(projectile.LevelId)
                    || !WorldState.ContainsPosition(projectile.LevelId, projectile.PositionX, projectile.PositionY)
                    || !WorldState.ContainsPosition(projectile.LevelId, projectile.StartX, projectile.StartY)
                    || !WorldState.ContainsPosition(projectile.LevelId, projectile.TargetX, projectile.TargetY)))
                throw new InvalidDataException("An object trap missile requires an owned native level and valid coordinates.");
            if (projectile.NativeWeaponArrow is { } nativeArrow
                && (!WorldState.OwnsNativePopulationLevel(projectile.LevelId)
                    || projectile.Behavior != AuthoritativeProjectileBehavior.WeaponArrow
                    || nativeArrow.MissileDistance > int.MaxValue
                    || projectile.AttackHit || projectile.Damage != 0 || projectile.DamageQ6 != 0
                    || projectile.PerilSelfDamageQ6 != 0 || projectile.DoppelgangerProc))
                throw new InvalidDataException("Native arrow state requires its owned level and unresolved payload.");
            var isMonsterArrow = projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow
                && restoredMonsters.Any(monster => monster.EntityId == projectile.SourceEntityId);
            var nativeMissileIdentityValid = projectile.NativeMissileId is null
                || projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic
                    && MonsterMissiles is not null
                    && MonsterMissiles.TryGetByMissileId(projectile.NativeMissileId, out _);
            if (projectile.NativeTrapArrow is null
                && (projectile.SourceEntityId == 0
                || !nativeMissileIdentityValid
                || (projectile.SpellId == 0
                    && projectile.Behavior != AuthoritativeProjectileBehavior.WeaponArrow
                    && !isMonsterArrow && !familiarLightning)
                || projectile.RemainingTicks == 0
                || projectile.Damage < 0
                || projectile.DamageMinimum < 0
                || projectile.DamageMaximum < 0
                || (projectile.DamageMinimum == 0
                    ? projectile.DamageMaximum != 0
                    : projectile.DamageMaximum < projectile.DamageMinimum)
                || (projectile.Behavior != AuthoritativeProjectileBehavior.MonsterArrow
                    && (projectile.DamageMinimum != 0
                        || projectile.DamageMaximum != 0))
                || projectile.DamageQ6 < 0
                || projectile.ElementalFireDamage < 0
                || projectile.ElementalLightningDamage < 0
                || projectile.PerilSelfDamageQ6 < 0
                || projectile.AreaRadius < 0
                || projectile.EffectiveSpellLevel < 0
                || !Enum.IsDefined(projectile.DamageType)
                || !Enum.IsDefined(projectile.Behavior)
                || !Enum.IsDefined(projectile.Phase)
                || projectile.Behavior is AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Resurrection
                || (projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    && (projectile.SpellId != 0
                        || projectile.TargetEntityId == 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed < 0
                        || projectile.Speed > 74
                        || projectile.Speed is > 0 and < 16
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.Damage != DeterministicFixedPointQ6.ToWholeTruncated(projectile.DamageQ6)
                        || projectile.ActivationDelayTicks > 2
                        || (projectile.NativeWeaponArrow is null && projectile.NativeTrapArrow is null
                            ? projectile.VelocityX != 0 || projectile.VelocityY != 0 || projectile.OriginalDirection != 0
                            : projectile.VelocityX == 0 && projectile.VelocityY == 0
                                || projectile.OriginalDirection is < 0 or > 7
                                || projectile.RemainingTicks > 256)
                        || projectile.PathIndex != 0
                        || projectile.PathTicks is < 0 or > 31
                        || projectile.Speed == 0 && projectile.PathTicks != 0
                        || projectile.ImpactLifetime != 1
                        || projectile.ResistantDamageDivisor != 1
                        || projectile.EffectiveSpellLevel != 0
                        || projectile.DamageMinimum != 0
                        || projectile.DamageMaximum != 0
                        || (projectile.NativeWeaponArrow is null && projectile.LastCollisionTargetEntityId != 0)
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
                        || projectile.RingOfFireLimitReached
                        || projectile.GuardianState != 0
                        || projectile.ElementalOriginX != 0
                        || projectile.ElementalOriginY != 0))
                || (isMonsterArrow
                    && (projectile.TargetEntityId == 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 1
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.ImpactLifetime != 1
                        || projectile.ResistantDamageDivisor != 1
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.OriginalDirection != 0
                        || projectile.PathIndex != 0
                        || projectile.PathTicks != 0
                        || projectile.EffectiveSpellLevel != 0))
                || IsInvalidStoneCurseProjectile(projectile, restoredMonsters)
                || !WorldState.ContainsProjectilePosition(
                    projectile.Behavior,
                    projectile.Phase,
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY)
                || !WorldState.ContainsProjectilePosition(
                    projectile.Behavior,
                    projectile.Phase,
                    projectile.LevelId,
                    projectile.TargetX,
                    projectile.TargetY)
                    && projectile.NativeMonsterMissile?.HasFamiliarLightningAim(projectile.PositionX,
                        projectile.PositionY, projectile.TargetX, projectile.TargetY) != true
                || ((projectile.Behavior is AuthoritativeProjectileBehavior.ChargedBolt
                        or AuthoritativeProjectileBehavior.MonsterChargedBolt)
                    && (projectile.OriginalDirection is < 0 or > 7
                        || projectile.PathIndex is < 0 or > 15
                        || projectile.PathTicks is < 0 or > 16
                        || projectile.Speed <= 0
                        || projectile.ImpactLifetime <= 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt
                    && (projectile.OriginalDirection is < 0 or > 7
                        || projectile.Speed <= 0
                        || projectile.ImpactLifetime <= 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.Acid
                    && (projectile.TargetEntityId == 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Acid
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 16
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.VelocityX == 0
                            && projectile.VelocityY == 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Acid
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.RemainingTicks != 1))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Acid
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase is not (
                            AuthoritativeProjectilePhase.Travel
                            or AuthoritativeProjectilePhase.Impact)
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY
                        || projectile.RemainingTicks == 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.SnakeCharge
                    && (projectile.TargetEntityId == 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 18
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.VelocityX == 0
                            && projectile.VelocityY == 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.BatCharge
                    && (projectile.TargetEntityId == 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 18
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.VelocityX == 0
                            && projectile.VelocityY == 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.HolyBolt
                    && (projectile.TargetEntityId != 0
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.Speed <= 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.ImpactLifetime <= 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.BoneSpirit
                    && (projectile.TargetEntityId != 0
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.PathIndex is < 0 or > 1
                        || projectile.Speed <= 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Magic
                        || projectile.AreaRadius != 0
                        || projectile.ImpactLifetime != 7
                        || projectile.ResistantDamageDivisor != 4
                        || (projectile.Phase == AuthoritativeProjectilePhase.Impact
                            && (projectile.VelocityX != 0
                                || projectile.VelocityY != 0
                                 || projectile.RemainingTicks > projectile.ImpactLifetime))))
                || ((projectile.Behavior is AuthoritativeProjectileBehavior.FireWallController
                        or AuthoritativeProjectileBehavior.LightningWallController)
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType is not (AuthoritativeDamageType.Fire or AuthoritativeDamageType.Lightning)
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.WallLeftDirection is < 0 or > 7
                        || projectile.WallRightDirection is < 0 or > 7
                        || projectile.WallHitDirection != -1
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.WallLeftX,
                            projectile.WallLeftY)
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.WallRightX,
                            projectile.WallRightY)))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType is not (AuthoritativeDamageType.Fire or AuthoritativeDamageType.Lightning)
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.WallLeftDirection != -1
                        || projectile.WallRightDirection != -1
                        || projectile.WallLeftStopped
                        || projectile.WallRightStopped
                        || projectile.WallLeftX != 0
                        || projectile.WallLeftY != 0
                        || projectile.WallRightX != 0
                        || projectile.WallRightY != 0
                        || projectile.WallHitDirection is < -1 or > 7))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.RingOfFireController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.RemainingTicks != 1
                        || projectile.PositionX != projectile.StartX
                        || projectile.PositionY != projectile.StartY
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.RemainingTicks != 1
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.PositionX,
                            projectile.PositionY)
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.TargetX,
                            projectile.TargetY)))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveSegment
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 16
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 32
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.PathIndex is < 0 or > 3))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Lightning
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.RemainingTicks != 1))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningBolt
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.DamageType != AuthoritativeDamageType.Lightning
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 32
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.ResistantDamageDivisor != 4))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.NovaController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Lightning
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.RemainingTicks != 1
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.PositionX,
                            projectile.PositionY)
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.TargetX,
                            projectile.TargetY)))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.NovaBall
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Lightning
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 16
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.VelocityX == 0
                            && projectile.VelocityY == 0
                        || projectile.ResistantDamageDivisor != 4))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Magic
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.ApocalypseScanX < 1
                        || projectile.ApocalypseScanY < 1
                        || projectile.ApocalypseHit))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.ApocalypseScanX != 0
                        || projectile.ApocalypseScanY != 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.RageController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Physical
                        || projectile.AreaRadius != 0
                        || projectile.Speed != 0
                        || projectile.Phase is not (
                            AuthoritativeProjectilePhase.Travel
                            or AuthoritativeProjectilePhase.Impact)
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY
                        || projectile.StartX != projectile.PositionX
                        || projectile.StartY != projectile.PositionY
                        || projectile.TraveledX != 0
                        || projectile.TraveledY != 0
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.ApocalypseScanX != 0
                        || projectile.ApocalypseScanY != 0
                        || projectile.ApocalypseHit))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.Elemental
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 1
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.Speed != 16
                        || projectile.ImpactLifetime != 14
                        || projectile.Phase is not (
                            AuthoritativeProjectilePhase.Travel
                            or AuthoritativeProjectilePhase.Impact)
                        || projectile.PathIndex is < 0 or > 1
                        || !WorldState.ContainsPosition(
                            projectile.LevelId,
                            projectile.ElementalOriginX,
                            projectile.ElementalOriginY)
                        || (projectile.Phase == AuthoritativeProjectilePhase.Impact
                            && (projectile.VelocityX != 0
                                || projectile.VelocityY != 0
                                || projectile.RemainingTicks > projectile.ImpactLifetime))))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.GuardianController
                    && (projectile.TargetEntityId != 0
                        || projectile.Damage != 0
                        || projectile.AreaRadius != 0
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.RemainingTicks == 0
                        || projectile.GuardianState is < 0 or > 3
                        || projectile.Speed != 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.Fireball
                    && (projectile.TargetEntityId != 0
                        || projectile.OriginalDirection is < 0 or > 7
                        || projectile.Speed <= 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Fire
                        || projectile.AreaRadius != 1
                        || projectile.ImpactLifetime <= 1
                        || projectile.ResistantDamageDivisor <= 1
                        || (projectile.Phase == AuthoritativeProjectilePhase.Travel
                            && projectile.VelocityX == 0
                            && projectile.VelocityY == 0)
                        || (projectile.Phase == AuthoritativeProjectilePhase.Impact
                            && (projectile.VelocityX != 0
                                || projectile.VelocityY != 0
                                || projectile.RemainingTicks
                                    > projectile.ImpactLifetime))))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.Lightning
                    && (projectile.OriginalDirection is < 0 or > 7
                        || projectile.Speed <= 0
                        || projectile.Damage != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.RemainingTicks % 2 != 0
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.LightningSegment
                    && (projectile.Speed != 0
                        || projectile.Damage <= 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior is AuthoritativeProjectileBehavior.FlashBottom
                        or AuthoritativeProjectileBehavior.FlashTop
                    && (projectile.TargetEntityId != 0
                        || projectile.Speed != 0
                        || projectile.Damage <= 0
                        || projectile.DamageType != AuthoritativeDamageType.Magic
                        || projectile.AreaRadius != 1
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY
                        || projectile.StartX != projectile.PositionX
                        || projectile.StartY != projectile.PositionY
                        || projectile.TraveledX != 0
                        || projectile.TraveledY != 0
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.ResistantDamageDivisor <= 0))
                || ((projectile.Behavior is AuthoritativeProjectileBehavior.Teleport
                        or AuthoritativeProjectileBehavior.Phasing)
                    && (projectile.TargetEntityId != 0
                        || projectile.Speed != 0
                        || projectile.Damage != 0
                        || projectile.AreaRadius != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY
                        || projectile.StartX != projectile.PositionX
                        || projectile.StartY != projectile.PositionY
                        || projectile.TraveledX != 0
                        || projectile.TraveledY != 0
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.RemainingTicks > 2
                        || projectile.ResistantDamageDivisor <= 0))
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ResurrectionBeam
                    && (projectile.TargetEntityId == 0
                        || projectile.Speed != 0
                        || projectile.Damage != 0
                        || projectile.AreaRadius != 0
                        || projectile.Phase != AuthoritativeProjectilePhase.Travel
                        || projectile.PositionX != projectile.TargetX
                        || projectile.PositionY != projectile.TargetY
                        || projectile.StartX != projectile.PositionX
                        || projectile.StartY != projectile.PositionY
                        || projectile.TraveledX != 0
                        || projectile.TraveledY != 0
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.ResistantDamageDivisor <= 0)))) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid projectile.");
            }
            return new AuthoritativeProjectile(
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
                projectile.RemainingTicks,
                allowZeroSpellId: familiarLightning || projectile.NativeTrapArrow is not null
                    || projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    || (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow
                        && restoredMonsters.Any(monster => monster.EntityId == projectile.SourceEntityId))) {
                Behavior = projectile.Behavior,
                NativeMissileId = projectile.NativeMissileId,
                Phase = projectile.Phase,
                StartX = projectile.Behavior == AuthoritativeProjectileBehavior.Targeted
                    && projectile.StartX == 0
                    && projectile.StartY == 0
                    ? projectile.PositionX
                    : projectile.StartX,
                StartY = projectile.Behavior == AuthoritativeProjectileBehavior.Targeted
                    && projectile.StartX == 0
                    && projectile.StartY == 0
                    ? projectile.PositionY
                    : projectile.StartY,
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
                NativeWeaponArrow = projectile.NativeWeaponArrow,
                NativeTrapArrow = projectile.NativeTrapArrow,
                NativeMonsterMissile = projectile.NativeMonsterMissile,
                PerilSelfDamageQ6 = projectile.PerilSelfDamageQ6,
                DoppelgangerProc = projectile.DoppelgangerProc,
                ActivationDelayTicks = projectile.ActivationDelayTicks,
                DamageMinimum = projectile.DamageMinimum,
                DamageMaximum = projectile.DamageMaximum,
            };
        }).ToArray();
        if (restoredProjectiles
            .Where(projectile =>
                projectile.Behavior == AuthoritativeProjectileBehavior.StoneCurse)
            .GroupBy(projectile => projectile.TargetEntityId)
            .Any(group => group.Skip(1).Any())) {
            throw new InvalidDataException(
                "The authoritative save contains duplicate Stone Curse controllers.");
        }
        var restoredQuests = snapshot.Quests.Select(quest => {
            if (quest.QuestId == 0
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || (quest.Unavailable && (quest.Completed || quest.Progress != 0))
                || quest.Variable1 is < 0 or > byte.MaxValue
                || quest.Variable2 is < 0 or > byte.MaxValue
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < -1 or > byte.MaxValue
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInVariable1 is < -1 or > byte.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid quest state.");
            }
            if (!NativeTextLimits.IsIdentityWithinLimit(quest.CompletionUniqueIdentity)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid quest unique-monster identity.");
            }
            return quest;
        }).ToArray();
        var restoredStore = ValidateSavedStore(source, snapshot.ActiveStore);

        // A timed move still occupies only its current cell. Its future cell
        // is revalidated at the effect tick and may be claimed by geometry or
        // another participant while the action is in flight.
        if (WorldState.IsCathedralLightObjectSolid(source.LevelId, source.PositionX, source.PositionY)
            || WorldState.IsPendingPlayerMoveIntoCathedralLight(source.Life, source.LevelId,
                source.PositionX, source.PositionY, source.CurrentAction))
            throw new InvalidDataException("The authoritative save places a player inside a Cathedral light.");
        if (restoredWorldItems.Any(item => WorldState.IsCathedralLightObjectSolid(item.LevelId, item.PositionX, item.PositionY))
            || restoredMonsters.Any(monster => WorldState.IsCathedralLightObjectSolid(monster.LevelId, monster.PositionX, monster.PositionY)
                || monster.Moving && WorldState.IsCathedralLightObjectSolid(monster.LevelId, monster.FuturePositionX, monster.FuturePositionY)))
            throw new InvalidDataException("The authoritative save places an actor inside a Cathedral light.");
        var deadDoorCell = WorldState.IsDeadPlayerOnCathedralDoor(source.Life, source.LevelId, source.PositionX, source.PositionY);
        bool IsAtDeadDoor((uint LevelId, int PositionX, int PositionY) position)
            => deadDoorCell && position == (source.LevelId, source.PositionX, source.PositionY);
        var liveMonsterPositions = restoredMonsters.Where(monster => monster.HitPoints > 0)
            .SelectMany(monster => monster.Moving
                ? new[] { (monster.LevelId, monster.PositionX, monster.PositionY),
                    (monster.LevelId, monster.FuturePositionX, monster.FuturePositionY) }
                : [(monster.LevelId, monster.PositionX, monster.PositionY)]).ToArray();
        var itemPositions = restoredWorldItems.Select(item => (item.LevelId, item.PositionX, item.PositionY)).ToArray();
        if (liveMonsterPositions.Count(IsAtDeadDoor) > 1 || itemPositions.Count(IsAtDeadDoor) > 1)
            throw new InvalidDataException("The authoritative save duplicates native occupancy in a dead-player door cell.");
        DiabloWorldState.ValidateOccupancy(
            source.LevelId,
            source.PositionX,
            source.PositionY,
            liveMonsterPositions.Where(position => !IsAtDeadDoor(position)),
            itemPositions.Where(position => !IsAtDeadDoor(position)),
            restoredObjects
                .Where(@object => !@object.Activated
                    && !WorldState.IsCathedralDoorObject(@object.EntityId, @object.LevelId,
                        @object.PositionX, @object.PositionY)
                    && !@object.Destroyed
                    && !@object.ActivateOnOccupancy)
                .Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));

        RestorePlayer(source, destination);
        RestoreMonsters(restoredMonsters);
        ResetProceduralSpawnState();
        materializedProceduralLevels.UnionWith(restoredMaterializedProceduralLevelIds);
        WorldState.WorldItems.Clear();
        foreach (var item in restoredWorldItems)
            WorldState.WorldItems.Add(item.EntityId, item);
        WorldState.Corpses.Clear();
        foreach (var corpse in restoredCorpses)
            WorldState.Corpses.Add(corpse.EntityId, corpse);
        RestoreObjects(restoredObjects);
        WorldState.RefreshProjectedGeometry();
        // Restored unique encounters can be the first available anchors for
        // deferred native-DUN fragments. Set-level origins are restored first
        // so those fragments rebuild the same geometry as the source.
        ApplyDeferredNativeDunFragments();
        WorldState.RefreshProjectedGeometry();
        WorldState.Portals.Clear();
        foreach (var portal in savedPortals) {
            WorldState.Portals.Add(
                portal.PortalId,
                new AuthoritativePortal(
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
                });
        }
        WorldState.RestorePortalClock(snapshot.Tick);
        WorldState.Projectiles.Clear();
        foreach (var projectile in restoredProjectiles)
            WorldState.Projectiles.Add(projectile.EntityId, projectile);
        random.Restore(snapshot.RandomState);
        RestoreQuests(restoredQuests);
        RefreshAuthoredDialogueTargets();
        worldCommands.EnsureLevelEntryState(destination.LevelId);
        WorldState.VictoryTriggered = snapshot.VictoryTriggered;
        if (restoredStore is not null)
            storeCatalog.ReplaceStore(restoredStore.StoreId, restoredStore.Items);
    }

}
