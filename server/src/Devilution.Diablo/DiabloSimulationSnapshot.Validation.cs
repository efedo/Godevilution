using Devilution.Domain.Protocol;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Shared;
using System.Text.Json.Serialization;

namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /** Hydrates authored inventory footprints before persistence validation or use. */
    private StorePlayerSnapshot ResolveSnapshotItemFootprints(StorePlayerSnapshot source)
    {
        if (Items is null)
            return source;

        return source with {
            Inventory = source.Inventory is null
                ? null!
                : source.Inventory.Select(item => item is null || item.State is null
                    ? item!
                    : item with { State = ResolveItemState(item.State, item.ItemSeed) }).ToArray(),
            Equipment = source.Equipment is null
                ? null!
                : source.Equipment.Select(item => item is null || item.State is null
                    ? item!
                    : item with { State = ResolveItemState(item.State, item.ItemSeed) }).ToArray(),
            Belt = source.Belt is null
                ? null!
                : source.Belt.Select(item => item is null || item.State is null
                    ? item!
                    : item with { State = ResolveItemState(item.State, item.ItemSeed) }).ToArray(),
            Stash = source.Stash is null
                ? null!
                : source.Stash.Select(item => item is null || item.State is null
                    ? item!
                    : item with { State = ResolveItemState(item.State, item.ItemSeed) }).ToArray(),
            HeldItem = source.HeldItem is null || source.HeldItem.State is null
                ? source.HeldItem
                : source.HeldItem with {
                    State = ResolveItemState(source.HeldItem.State, source.HeldItem.ItemSeed),
                },
        };
    }

    private AuthoritativeItemState ResolveItemState(
        AuthoritativeItemState state,
        uint itemSeed)
    {
        return Items?.ResolveAuthoredMetadata(state, itemSeed) ?? state;
    }

    private AuthoritativeItemState ResolveWorldItemState(AuthoritativeWorldItem item)
    {
        return ResolveWorldItemState(item.State, item.Price, item.ItemSeed);
    }

    private DiabloWorldItemState ResolveWorldItemState(DiabloWorldItemState item)
    {
        if (item.State is null)
            return item;

        return item with {
            State = ResolveWorldItemState(item.State, item.Price, item.ItemSeed),
        };
    }

    private AuthoritativeItemState ResolveWorldItemState(
        AuthoritativeItemState source,
        uint price,
        uint itemSeed)
    {
        var state = ResolveItemState(source, itemSeed);
        if (price <= int.MaxValue
            && DiabloGoldInventory.HasGoldIdentity(state, CombatRules, Items)) {
            state = state with {
                Value = state.Value == 0 ? checked((int)price) : state.Value,
                IdentifiedValue = state.IdentifiedValue == 0
                    ? checked((int)price)
                    : state.IdentifiedValue,
            };
        }
        return state;
    }

    private DiabloStoreState? ResolveStoreState(DiabloStoreState? store)
    {
        if (Items is null || store is null)
            return store;

        return store with {
            Items = store.Items.Select(item => item is null || item.State is null
                ? item!
                : item with { State = ResolveItemState(item.State, item.ItemSeed) }).ToArray(),
        };
    }

    private bool IsMalformedGoldWorldItem(DiabloWorldItemState item)
    {
        var state = item.State;
        if (!DiabloGoldInventory.HasGoldIdentity(state, CombatRules, Items)) {
            return false;
        }
        return state.Value <= 0
            || state.Value > CombatRules.DeathGoldMaximumStack
            || state.IdentifiedValue != state.Value
            || item.Price != checked((uint)state.Value)
            || !state.StatFlag;
    }

    private IReadOnlyList<DiabloSetLevelObjectOriginState> ValidateSavedSetLevelObjectOrigins(
        IReadOnlyList<DiabloSetLevelObjectOriginState> origins)
    {
        if (origins.Count == 0)
            return [];
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Select(origin => origin.SetLevelId).Distinct().Count() != origins.Count
            || origins.Any(origin => origin.SetLevelId is < 0 or > 8)
            || WorldState.World is null
            || origins.Any(origin => !WorldState.World.ContainsPosition(
                origin.LevelId,
                origin.PositionX,
                origin.PositionY))) {
            throw new InvalidDataException(
                "The authoritative save contains invalid set-level object origins.");
        }
        return origins
            .OrderBy(origin => origin.SetLevelId)
            .ToArray();
    }

    private IReadOnlyList<DiabloProceduralQuestPieceOriginState>
        ValidateSavedProceduralQuestPieceOrigins(
            IReadOnlyList<DiabloProceduralQuestPieceOriginState> origins)
    {
        if (origins.Count == 0)
            return [];
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Any(origin => origin.OriginId == 0)
            || origins.Select(origin => origin.OriginId).Distinct().Count() != origins.Count
            || WorldState.World is null
            || origins.Any(origin => !WorldState.World.ContainsPosition(
                origin.LevelId,
                origin.PositionX,
                origin.PositionY))) {
            throw new InvalidDataException(
                "The authoritative save contains invalid procedural quest-piece origins.");
        }
        return origins
            .OrderBy(origin => origin.OriginId)
            .ToArray();
    }

    private IReadOnlySet<uint> ValidateSavedMaterializedProceduralLevelIds(
        IReadOnlyList<uint> levelIds)
    {
        if (levelIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || levelIds.Distinct().Count() != levelIds.Count
            || WorldState.World is not null
                && levelIds.Any(levelId => !WorldState.World.ContainsLevel(levelId))) {
            throw new InvalidDataException(
                "The authoritative save contains invalid materialized procedural levels.");
        }
        return levelIds.ToHashSet();
    }

    private DiabloStoreState? ValidateSavedStore(
        StorePlayerSnapshot player,
        DiabloStoreState? store)
    {
        if (player.ActiveStoreId is null) {
            if (store is not null) {
                throw new InvalidDataException(
                    "The authoritative save contains store state for an inactive store.");
            }
            return null;
        }
        if (store is null
            || store.StoreId != player.ActiveStoreId
            || !storeCatalog.ContainsStore(store.StoreId)
            || store.Items.Any(item => item is null
                || item.State is null
                || !IsNativeItemPrice(item.Price)
                || !HasKnownStableItemIdentity(item.State))
            || store.Items.Select(item => item.StoreSlot).Distinct().Count() != store.Items.Count) {
            throw new InvalidDataException(
                "The authoritative save contains invalid active-store state.");
        }
        return new DiabloStoreState(store.StoreId, store.Items.ToArray());
    }

    private void RestorePlayer(
        StorePlayerSnapshot source,
        AuthoritativePlayerState destination)
    {
        destination.ConfigureMaximumAttribute(CombatRules.MaximumAttribute);
        destination.Gold = source.Gold;
        destination.Experience = source.Experience;
        destination.Attributes = source.Attributes;
        destination.CharacterLevel = source.CharacterLevel;
        destination.CharacterClassId = source.CharacterClassId;
        if (PlayerClasses is not null)
            destination.PlayerClass = PlayerClasses.GetRequired(source.CharacterClassId);
        if (!string.IsNullOrEmpty(source.CharacterName))
            destination.CharacterName = source.CharacterName;
        destination.AbilitySpellId = source.AbilitySpellId;
        destination.AbilityKind = source.AbilityKind;
        destination.ManaShieldActive = source.ManaShieldActive;
        destination.ReflectCharges = source.ReflectCharges;
        destination.DiabloKillLevel = source.DiabloKillLevel;
        destination.StatPoints = source.StatPoints;
        destination.VisitedLevelIds.Clear();
        var restoredVisitedLevelIds = source.VisitedLevelIds
            .Where(AuthoritativeLevelIdentityRules.IsRegularLevel)
            .ToHashSet();
        if (restoredVisitedLevelIds.Count == 0
            && AuthoritativeLevelIdentityRules.IsRegularLevel(source.LevelId))
            restoredVisitedLevelIds.Add(source.LevelId);
        destination.VisitedLevelIds.UnionWith(restoredVisitedLevelIds);
        destination.FriendlyMode = source.FriendlyMode;
        destination.FacingDirection = source.FacingDirection;
        destination.CombatMode = source.CombatMode;
        destination.CombatModeEndTick = source.CombatModeEndTick;
        destination.CurrentAction = ClonePlayerAction(source.CurrentAction);
        destination.NativeWalkPath = CloneNativeWalkPath(source.NativeWalkPath);
        destination.NativeQueuedRangedAttack = CloneNativeQueuedRangedAttack(source.NativeQueuedRangedAttack);
        destination.MemorizedSpells.Clear();
        destination.MemorizedSpells.AddRange(source.MemorizedSpells);
        destination.PositionX = source.PositionX;
        destination.PositionY = source.PositionY;
        destination.LevelId = source.LevelId;
        destination.StatusEffects = source.StatusEffects.ToList();
        destination.ActiveStoreId = source.ActiveStoreId;
        destination.RestoreNativeResourceBases(null, null);
        destination.Inventory.Clear();
        destination.Inventory.AddRange(source.Inventory);
        destination.Equipment.Clear();
        destination.Equipment.AddRange(source.Equipment);
        if (source.BaseLifeMaximum > 0 || source.NativeLifeBase.HasValue)
            destination.BaseLifeMaximum = source.BaseLifeMaximum;
        if (source.BaseManaMaximum > 0 || source.NativeManaBase.HasValue)
            destination.BaseManaMaximum = source.BaseManaMaximum;
        destination.AdoptEquipmentAttributeSnapshot();
        destination.RestoreNativeResourceBases(source.NativeLifeBase, source.NativeManaBase);
        destination.RecalculateEquipmentResources();
        destination.Life = source.Life;
        destination.RestoreNativeMana(source.Mana, source.ManaMaximum);
        destination.RestoreNativeResourceBases(source.NativeLifeBase, source.NativeManaBase);
        destination.FullyExploredLevelIds.Clear();
        destination.FullyExploredLevelIds.UnionWith(source.FullyExploredLevelIds ?? []);
        destination.Belt.Clear();
        destination.Belt.AddRange(source.Belt);
        destination.InventoryGrid.Clear();
        destination.InventoryGrid.AddRange(source.InventoryGrid);
        destination.HeldItem = source.HeldItem;
        destination.Stash.Clear();
        destination.Stash.AddRange(source.Stash);
        destination.StashGold = source.StashGold;
        destination.StashOpen = source.StashOpen;
        destination.EntityId = source.EntityId;
    }

    private static bool IsInvalidStoneCurseProjectile(
        DiabloProjectileState projectile,
        IReadOnlyList<DiabloMonsterState> monsters)
    {
        if (projectile.Behavior != AuthoritativeProjectileBehavior.StoneCurse)
            return false;
        var target = monsters.SingleOrDefault(monster =>
            monster.EntityId == projectile.TargetEntityId);
        var occupiesControllerTile = target is not null
            && ((target.PositionX == projectile.PositionX
                    && target.PositionY == projectile.PositionY)
                || (target.Moving
                    && target.FuturePositionX == projectile.PositionX
                    && target.FuturePositionY == projectile.PositionY));
        return target is null
            || target.StoneCurseImmune
            || !target.Petrified
            || (target.LevelId != 0 && target.LevelId != projectile.LevelId)
            || (!occupiesControllerTile
                && projectile.Phase != AuthoritativeProjectilePhase.Impact)
            || projectile.TargetEntityId == 0
            || projectile.Damage != 0
            || projectile.DamageType != AuthoritativeDamageType.Magic
            || projectile.AreaRadius != 0
            || projectile.Speed != 0
            || projectile.PositionX != projectile.TargetX
            || projectile.PositionY != projectile.TargetY
            || projectile.StartX != projectile.PositionX
            || projectile.StartY != projectile.PositionY
            || projectile.TraveledX != 0
            || projectile.TraveledY != 0
            || projectile.VelocityX != 0
            || projectile.VelocityY != 0
            || projectile.OriginalDirection != 0
            || projectile.PathIndex != 0
            || projectile.PathTicks != 0
            || projectile.ImpactLifetime <= 0
            || projectile.ResistantDamageDivisor != 1
            || (projectile.Phase == AuthoritativeProjectilePhase.Travel
                && target.HitPointsQ6 == 0)
            || (projectile.Phase == AuthoritativeProjectilePhase.Impact
                && (target.HitPointsQ6 != 0
                    || projectile.RemainingTicks > projectile.ImpactLifetime));
    }

    private void RestoreMonsters(IReadOnlyList<DiabloMonsterState> monsters)
    {
        var savedIds = monsters.Select(monster => monster.EntityId).ToHashSet();
        foreach (var entityId in WorldState.CombatTargets.Keys
                     .Where(entityId => !savedIds.Contains(entityId))
                     .ToArray()) {
            WorldState.CombatTargets.Remove(entityId);
        }
        foreach (var monster in monsters) {
            if (WorldState.CombatTargets.TryGetValue(monster.EntityId, out var target)) {
                if (target.DeathAnimationTicks != monster.DeathAnimationTicks) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible monster death-animation data.");
                }
                if (target.ExperienceReward != monster.ExperienceReward) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible monster experience data.");
                }
                if (target.ToHit != monster.ToHit
                    || target.ToHitSpecial != monster.ToHitSpecial) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible monster accuracy data.");
                }
                if (target.NativeAi != monster.NativeAi) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible monster AI data.");
                }
                if (!string.IsNullOrEmpty(monster.UniqueIdentity)
                    && target.UniqueIdentity != monster.UniqueIdentity) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible unique monster identity data.");
                }
                if (target.Intelligence != monster.Intelligence) {
                    throw new InvalidDataException(
                        "The authoritative save contains incompatible monster intelligence data.");
                }
                target.PositionX = monster.PositionX;
                target.PositionY = monster.PositionY;
                target.RestoreDirection(monster.Direction);
                target.MonsterItemSeed = monster.MonsterItemSeed;
                target.MonsterAiSeed = monster.MonsterAiSeed;
                target.DialogueMessageId = monster.DialogueMessageId;
                target.RestoreDialogueState(monster.DialogueState);
                target.ArmorClass = monster.ArmorClass;
                target.RestoreHitPointsQ6(
                    monster.HitPointsQ6,
                    monster.MaxHitPointsQ6);
                target.LevelId = monster.LevelId;
                target.MonsterId = monster.MonsterId;
                target.AttackDamage = monster.AttackDamage;
                target.AggroRange = monster.AggroRange;
                target.FireResistance = monster.FireResistance;
                target.LightningResistance = monster.LightningResistance;
                target.MagicResistance = monster.MagicResistance;
                target.AcidResistance = monster.AcidResistance;
                target.MonsterLevel = monster.MonsterLevel;
                target.StoneCurseImmune = monster.StoneCurseImmune;
                target.IsUndead = monster.IsUndead;
                target.IsDemon = monster.IsDemon;
                target.IsAnimal = monster.IsAnimal;
                target.HasKnockback = monster.HasKnockback;
                target.IsPlayerMinion = monster.IsPlayerMinion;
                target.OwnerPlayerId = monster.OwnerPlayerId;
                target.IsUnique = monster.IsUnique;
                if (!string.IsNullOrEmpty(monster.UniqueIdentity))
                    target.UniqueIdentity = monster.UniqueIdentity;
                target.IsDiablo = monster.IsDiablo;
                target.BerserkImmune = monster.BerserkImmune;
                target.MinDamage = monster.MinDamage;
                target.MaxDamage = monster.MaxDamage;
                target.MinDamageSpecial = monster.MinDamageSpecial;
                target.MaxDamageSpecial = monster.MaxDamageSpecial;
                target.IsBerserked = monster.IsBerserked;
                target.GargoyleSpecialActive = monster.GargoyleSpecialActive;
                target.RestoreFallenFear(
                    monster.FallenFearTicks,
                    monster.FallenFearDirection);
                target.RestoreFallenAnimation(
                    monster.FallenStandAnimationFrame,
                    monster.FallenStandAnimationTick);
                target.RestoreFallenRally(monster.FallenRallyTicks);
                target.RestoreSneakState(
                    monster.SneakHidden,
                    monster.SneakRetreatTicks);
                target.RestoreScavengerHealing(
                    monster.ScavengerHealingTicks,
                    monster.ScavengerCorpseEntityId);
                target.RestoreGroupUnity(
                    monster.LeaderEntityId,
                    monster.LeaderRelation,
                    monster.PackSize);
                target.RestoreActiveForTicks(monster.ActiveForTicks);
                target.RestoreBatRetreat(
                    monster.BatRetreatTicks,
                    monster.BatRetreatDirection);
                target.RestoreBatMovementState(
                    monster.BatMovementIdleTicks,
                    monster.BatMovementMode);
                target.RestoreRangedAvoidanceMovement(
                    monster.RangedAvoidanceMoveTicks,
                    monster.RangedAvoidanceTurn);
                target.RestoreRangedAvoidanceMovementState(
                    monster.RangedAvoidanceMovementIdleTicks,
                    monster.RangedAvoidanceMovementMode);
                target.RestoreRhinoMovement(
                    monster.RhinoMovementTicks,
                    monster.RhinoMovementTurn);
                target.RestoreAvoidanceMovement(
                    monster.AvoidanceMovementTicks,
                    monster.AvoidanceMovementTurn);
                target.RestoreAvoidanceMovementState(
                    monster.AvoidanceMovementIdleTicks,
                    monster.AvoidanceMovementMode);
                target.RestoreSkeletonMeleeDelay(monster.SkeletonMeleeDelayTicks);
                target.RestoreSkeletonRangedMovementState(
                    monster.SkeletonRangedMovementIdleTicks,
                    monster.SkeletonRangedMovementMode);
                target.RestoreFatMovementState(
                    monster.FatMovementIdleTicks,
                    monster.FatMovementMode);
                target.RestoreRhinoMovementState(
                    monster.RhinoMovementIdleTicks,
                    monster.RhinoMovementMode);
                target.RestoreRhinoDelay(monster.RhinoDelayTicks);
                target.RestoreSkeletonKingDelay(monster.SkeletonKingDelayTicks);
                target.RestoreHorkDemonDelay(monster.HorkDemonDelayTicks);
                target.RestoreHorkDemonMovement(
                    monster.HorkDemonMoveTicks,
                    monster.HorkDemonMoveTurn);
                target.RestoreRangedAvoidanceDelay(monster.RangedAvoidanceDelayTicks);
                target.RestoreSnakeDelay(monster.SnakeDelayTicks);
                target.RestoreCounselorDelay(monster.CounselorDelayTicks);
                target.RestoreAvoidanceDelay(monster.AvoidanceDelayTicks);
                target.RestoreRangedDelay(monster.RangedDelayTicks);
                target.RestoreMegaDelay(monster.MegaDelayTicks);
                target.RestoreCounselorRetreat(monster.CounselorRetreatTicks);
                if (monster.HitPointsQ6 == 0 && monster.DeathAnimationTicks == 0
                    && monster.DeathTicksRemaining != 0) {
                    throw new InvalidDataException(
                        "The authoritative save contains an invalid monster death animation.");
                }
                target.RestoreDeathAnimation(monster.DeathTicksRemaining);
                target.RestoreOccupancy(
                    monster.Moving,
                    monster.FuturePositionX,
                    monster.FuturePositionY,
                    monster.Petrified);
                target.RestoreMovementTiming(
                    monster.MovementStartedTick,
                    monster.MovementEffectTick);
            } else {
                var restoredTarget = new AuthoritativeCombatTarget(
                    monster.EntityId,
                    monster.PositionX,
                    monster.PositionY,
                    monster.HitPoints,
                    monster.ArmorClass,
                    monster.MaxHitPoints,
                    monster.LevelId,
                    monster.MonsterId,
                    attackDamage: monster.AttackDamage,
                    aggroRange: monster.AggroRange,
                    fireResistance: monster.FireResistance,
                    lightningResistance: monster.LightningResistance,
                    magicResistance: monster.MagicResistance,
                    acidResistance: monster.AcidResistance,
                    monsterLevel: monster.MonsterLevel,
                    moving: monster.Moving,
                    futurePositionX: monster.FuturePositionX,
                    futurePositionY: monster.FuturePositionY,
                    direction: monster.Direction,
                    monsterItemSeed: monster.MonsterItemSeed,
                    monsterAiSeed: monster.MonsterAiSeed,
                    dialogueMessageId: monster.DialogueMessageId,
                    dialogueState: monster.DialogueState,
                    petrified: monster.Petrified,
                    stoneCurseImmune: monster.StoneCurseImmune,
                    undead: monster.IsUndead,
                    demon: monster.IsDemon,
                    animal: monster.IsAnimal,
                    knockback: monster.HasKnockback,
                    playerMinion: monster.IsPlayerMinion,
                    ownerPlayerId: monster.OwnerPlayerId,
                    minDamage: monster.MinDamage,
                    maxDamage: monster.MaxDamage,
                    minDamageSpecial: monster.MinDamageSpecial,
                    maxDamageSpecial: monster.MaxDamageSpecial,
                    unique: monster.IsUnique,
                    uniqueIdentity: monster.UniqueIdentity,
                    diablo: monster.IsDiablo,
                    berserkImmune: monster.BerserkImmune,
                    deathAnimationTicks: monster.DeathAnimationTicks,
                    experienceReward: monster.ExperienceReward,
                    toHit: monster.ToHit,
                    toHitSpecial: monster.ToHitSpecial,
                    nativeAi: monster.NativeAi,
                    intelligence: monster.Intelligence,
                    gargoyleSpecialActive: monster.GargoyleSpecialActive,
                    leaderEntityId: monster.LeaderEntityId,
                    leaderRelation: monster.LeaderRelation,
                    packSize: monster.PackSize,
                    activeForTicks: monster.ActiveForTicks,
                    initialStandAnimationFrame: monster.FallenStandAnimationFrame,
                    initialStandAnimationTick: monster.FallenStandAnimationTick,
                    fallenRallyTicks: monster.FallenRallyTicks,
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
                    avoidanceMovementMode: monster.AvoidanceMovementMode);
                restoredTarget.RestoreMovementTiming(
                    monster.MovementStartedTick,
                    monster.MovementEffectTick);
                restoredTarget.RestoreSkeletonMeleeDelay(monster.SkeletonMeleeDelayTicks);
                restoredTarget.RestoreSkeletonRangedMovementState(
                    monster.SkeletonRangedMovementIdleTicks,
                    monster.SkeletonRangedMovementMode);
                restoredTarget.RestoreFatMovementState(
                    monster.FatMovementIdleTicks,
                    monster.FatMovementMode);
                restoredTarget.RestoreRhinoMovementState(
                    monster.RhinoMovementIdleTicks,
                    monster.RhinoMovementMode);
                restoredTarget.RestoreRhinoDelay(monster.RhinoDelayTicks);
                restoredTarget.RestoreSkeletonKingDelay(monster.SkeletonKingDelayTicks);
                restoredTarget.RestoreHorkDemonDelay(monster.HorkDemonDelayTicks);
                restoredTarget.RestoreHorkDemonMovement(
                    monster.HorkDemonMoveTicks,
                    monster.HorkDemonMoveTurn);
                restoredTarget.RestoreRangedAvoidanceDelay(monster.RangedAvoidanceDelayTicks);
                restoredTarget.RestoreSnakeDelay(monster.SnakeDelayTicks);
                restoredTarget.RestoreCounselorDelay(monster.CounselorDelayTicks);
                restoredTarget.RestoreAvoidanceDelay(monster.AvoidanceDelayTicks);
                restoredTarget.RestoreRangedDelay(monster.RangedDelayTicks);
                restoredTarget.RestoreMegaDelay(monster.MegaDelayTicks);
                restoredTarget.RestoreFallenFear(
                    monster.FallenFearTicks,
                    monster.FallenFearDirection);
                restoredTarget.RestoreFallenAnimation(
                    monster.FallenStandAnimationFrame,
                    monster.FallenStandAnimationTick);
                restoredTarget.RestoreFallenRally(monster.FallenRallyTicks);
                restoredTarget.RestoreSneakState(
                    monster.SneakHidden,
                    monster.SneakRetreatTicks);
                restoredTarget.RestoreScavengerHealing(
                    monster.ScavengerHealingTicks,
                    monster.ScavengerCorpseEntityId);
                restoredTarget.RestoreBatRetreat(
                    monster.BatRetreatTicks,
                    monster.BatRetreatDirection);
                restoredTarget.RestoreBatMovementState(
                    monster.BatMovementIdleTicks,
                    monster.BatMovementMode);
                restoredTarget.RestoreRangedAvoidanceMovement(
                    monster.RangedAvoidanceMoveTicks,
                    monster.RangedAvoidanceTurn);
                restoredTarget.RestoreRangedAvoidanceMovementState(
                    monster.RangedAvoidanceMovementIdleTicks,
                    monster.RangedAvoidanceMovementMode);
                restoredTarget.RestoreRhinoMovement(
                    monster.RhinoMovementTicks,
                    monster.RhinoMovementTurn);
                restoredTarget.RestoreAvoidanceMovement(
                    monster.AvoidanceMovementTicks,
                    monster.AvoidanceMovementTurn);
                restoredTarget.RestoreAvoidanceMovementState(
                    monster.AvoidanceMovementIdleTicks,
                    monster.AvoidanceMovementMode);
                restoredTarget.RestoreSkeletonMeleeDelay(monster.SkeletonMeleeDelayTicks);
                restoredTarget.RestoreSkeletonRangedMovementState(
                    monster.SkeletonRangedMovementIdleTicks,
                    monster.SkeletonRangedMovementMode);
                restoredTarget.RestoreFatMovementState(
                    monster.FatMovementIdleTicks,
                    monster.FatMovementMode);
                restoredTarget.RestoreRhinoMovementState(
                    monster.RhinoMovementIdleTicks,
                    monster.RhinoMovementMode);
                restoredTarget.RestoreRhinoDelay(monster.RhinoDelayTicks);
                restoredTarget.RestoreSkeletonKingDelay(monster.SkeletonKingDelayTicks);
                restoredTarget.RestoreHorkDemonDelay(monster.HorkDemonDelayTicks);
                restoredTarget.RestoreHorkDemonMovement(
                    monster.HorkDemonMoveTicks,
                    monster.HorkDemonMoveTurn);
                restoredTarget.RestoreRangedAvoidanceDelay(monster.RangedAvoidanceDelayTicks);
                restoredTarget.RestoreSnakeDelay(monster.SnakeDelayTicks);
                restoredTarget.RestoreCounselorDelay(monster.CounselorDelayTicks);
                restoredTarget.RestoreAvoidanceDelay(monster.AvoidanceDelayTicks);
                restoredTarget.RestoreRangedDelay(monster.RangedDelayTicks);
                restoredTarget.RestoreMegaDelay(monster.MegaDelayTicks);
                restoredTarget.RestoreCounselorRetreat(monster.CounselorRetreatTicks);
                restoredTarget.RestoreHitPointsQ6(
                    monster.HitPointsQ6,
                    monster.MaxHitPointsQ6);
                restoredTarget.IsBerserked = monster.IsBerserked;
                restoredTarget.RestoreDeathAnimation(monster.DeathTicksRemaining);
                WorldState.CombatTargets.Add(monster.EntityId, restoredTarget);
            }
        }
    }

    private void RestoreObjects(IReadOnlyList<DiabloObjectState> objects)
    {
        var savedIds = objects.Select(@object => @object.EntityId).ToHashSet();
        foreach (var entityId in WorldState.Objects.Keys
                     .Where(entityId => !savedIds.Contains(entityId))
                     .ToArray()) {
            WorldState.Objects.Remove(entityId);
        }
        foreach (var @object in objects) {
            if (WorldState.Objects.TryGetValue(@object.EntityId, out var target)) {
                target.ObjectId = @object.ObjectId;
                target.LevelId = @object.LevelId;
                target.PositionX = @object.PositionX;
                target.PositionY = @object.PositionY;
                target.Activated = @object.Activated;
                target.QuestId = @object.QuestId;
                target.EffectKind = @object.EffectKind;
                target.EffectAmount = @object.EffectAmount;
                target.QuestProgressAmount = @object.QuestProgressAmount == 0
                    ? 1
                    : @object.QuestProgressAmount;
                target.MissilePassable = @object.MissilePassable;
                target.Breakable = @object.Breakable;
                target.Destroyed = @object.Destroyed;
                target.TrapArmed = @object.TrapArmed;
                target.IsShrine = @object.IsShrine;
                target.QuestTransitionKind = @object.QuestTransitionKind;
                target.QuestTransitionAmount = @object.QuestTransitionAmount;
                target.QuestTransitionRequiredVariable1 = @object.QuestTransitionRequiredVariable1;
                target.QuestTransitionRequiredVariable2 = @object.QuestTransitionRequiredVariable2;
                target.RewardKind = @object.RewardKind;
                target.RewardId = @object.RewardId;
                target.RewardItemLevel = @object.RewardItemLevel;
                target.RewardOffsetX = @object.RewardOffsetX;
                target.RewardOffsetY = @object.RewardOffsetY;
                target.RewardOriginKind = @object.RewardOriginKind;
                target.RewardSetLevelId = @object.RewardSetLevelId;
                target.ProjectileTargetOffsetX = @object.ProjectileTargetOffsetX;
                target.ProjectileTargetOffsetY = @object.ProjectileTargetOffsetY;
                target.QuestSequenceFollowUpKind = @object.QuestSequenceFollowUpKind;
                target.QuestSequenceFollowUpAmount = @object.QuestSequenceFollowUpAmount;
                target.QuestSequenceFollowUpOffsetX = @object.QuestSequenceFollowUpOffsetX;
                target.QuestSequenceFollowUpOffsetY = @object.QuestSequenceFollowUpOffsetY;
                target.ProjectileSourceKind = @object.ProjectileSourceKind;
                target.QuestSequenceFollowUpWorldMutationId = @object.QuestSequenceFollowUpWorldMutationId;
                target.ActivateOnOccupancy = @object.ActivateOnOccupancy;
                target.RequiredActivatedLinkedObjects = @object.RequiredActivatedLinkedObjects;
                target.NativeObjectId = @object.NativeObjectId;
                target.PresentationKind = @object.PresentationKind;
                target.HideWhenActivated = @object.HideWhenActivated;
                target.AnimationFrame = @object.AnimationFrame;
                target.ActivatedAnimationFrame = @object.ActivatedAnimationFrame;
                target.DialogueMessageId = @object.DialogueMessageId;
                target.NativeRandomSeed = @object.NativeRandomSeed;
            } else {
                WorldState.Objects.Add(@object.EntityId, new AuthoritativeWorldObject(
                    @object.EntityId,
                    @object.ObjectId,
                    @object.LevelId,
                    @object.PositionX,
                    @object.PositionY,
                    @object.Activated,
                    @object.QuestId,
                    @object.EffectKind,
                    @object.EffectAmount,
                    @object.QuestProgressAmount == 0 ? 1 : @object.QuestProgressAmount,
                    @object.MissilePassable,
                    @object.Breakable,
                    @object.Destroyed,
                    @object.TrapArmed,
                    @object.IsShrine,
                    @object.QuestTransitionKind,
                    @object.QuestTransitionAmount,
                    @object.QuestTransitionRequiredVariable1,
                    @object.QuestTransitionRequiredVariable2,
                    @object.RewardKind,
                    @object.RewardId,
                    @object.RewardItemLevel,
                    @object.RewardOffsetX,
                    @object.RewardOffsetY,
                    @object.ProjectileTargetOffsetX,
                    @object.ProjectileTargetOffsetY,
                    @object.QuestSequenceFollowUpKind,
                    @object.QuestSequenceFollowUpAmount,
                    @object.QuestSequenceFollowUpOffsetX,
                    @object.QuestSequenceFollowUpOffsetY,
                    @object.ProjectileSourceKind,
                    @object.QuestSequenceFollowUpWorldMutationId,
                    @object.ActivateOnOccupancy,
                    @object.RequiredActivatedLinkedObjects,
                    @object.NativeObjectId,
                    @object.HideWhenActivated,
                    @object.AnimationFrame,
                    @object.ActivatedAnimationFrame,
                    @object.RewardOriginKind,
                    @object.RewardSetLevelId,
                    @object.DialogueMessageId) {
                    PresentationKind = @object.PresentationKind,
                    NativeRandomSeed = @object.NativeRandomSeed,
                });
            }
        }
    }

    private void RestoreQuests(IReadOnlyList<DiabloQuestState> quests)
    {
        var savedIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var questId in WorldState.Quests.Keys
                     .Where(questId => !savedIds.Contains(questId))
                     .ToArray()) {
            WorldState.Quests.Remove(questId);
        }
        foreach (var quest in quests) {
            if (WorldState.Quests.TryGetValue(quest.QuestId, out var target)) {
                target.LevelId = quest.LevelId;
                target.Progress = quest.Progress;
                target.RequiredProgress = quest.RequiredProgress;
                target.Completed = quest.Completed;
                target.Unavailable = quest.Unavailable;
                target.Active = quest.Active && !quest.Completed && !quest.Unavailable;
                target.Logged = quest.Logged && !quest.Unavailable;
                target.Variable1 = quest.Variable1;
                target.Variable2 = quest.Variable2;
                target.MessageId = quest.MessageId;
                target.CompletionMonsterId = quest.CompletionMonsterId;
                if (!string.IsNullOrEmpty(quest.CompletionUniqueIdentity))
                    target.CompletionUniqueIdentity = quest.CompletionUniqueIdentity;
            } else {
                WorldState.Quests.Add(quest.QuestId, new AuthoritativeQuestState(
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
                    turnInTownerId: quest.TurnInTownerId,
                    turnInOnCompletion: quest.TurnInOnCompletion,
                    turnInCompletionVariable2: quest.TurnInCompletionVariable2,
                    turnInAlternateItemId: quest.TurnInAlternateItemId,
                    turnInAlternateMessageId: quest.TurnInAlternateMessageId,
                    turnInActivationOnly: quest.TurnInActivationOnly,
                    turnInMessageId: quest.TurnInMessageId,
                    turnInDialogueOnly: quest.TurnInDialogueOnly,
                    turnInCompletionRequiredVariable1: quest.TurnInCompletionRequiredVariable1,
                    turnInCompletionVariable1: quest.TurnInCompletionVariable1,
                    turnInRequiredVariable1: quest.TurnInRequiredVariable1,
                    turnInVariable1: quest.TurnInVariable1,
                    turnInProgressOnly: quest.TurnInProgressOnly,
                    turnInBlockedVisitedLevel: quest.TurnInBlockedVisitedLevel));
            }
        }
    }

    private void ValidateSavedPlayer(
        StorePlayerSnapshot source,
        AuthoritativePlayerState destination,
        ulong snapshotTick)
    {
        var baseLifeMaximum = source.BaseLifeMaximum == 0 && !source.NativeLifeBase.HasValue
            ? destination.BaseLifeMaximum
            : source.BaseLifeMaximum;
        var baseManaMaximum = source.BaseManaMaximum == 0 && !source.NativeManaBase.HasValue
            ? destination.BaseManaMaximum
            : source.BaseManaMaximum;
        AuthoritativePlayerClassDefinition? playerClass = null;
        var hasValidClass = PlayerClasses is null
            ? source.CharacterClassId == 0
            : PlayerClasses.TryGet(source.CharacterClassId, out playerClass);
        var expectedResources = baseLifeMaximum >= 0 && baseManaMaximum >= 0
            && source.Equipment is not null
            ? AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
                baseLifeMaximum,
                baseManaMaximum,
                source.Equipment,
                playerClass)
            : (LifeQ6: -1, ManaQ6: -1);
        if (source.Gold > int.MaxValue
            || source.StashGold > int.MaxValue
            || source.StatPoints > int.MaxValue
            || source.Life < 0
            || source.BaseLifeMaximum < 0
            || source.BaseManaMaximum < 0
            || source.NativeLifeBase.HasValue != source.NativeManaBase.HasValue
            || source.NativeLifeBase is < -(2000 << 6) || source.NativeLifeBase > source.BaseLifeMaximum
            || source.NativeManaBase is < -(2000 << 6) || source.NativeManaBase > source.BaseManaMaximum
            || source.Inventory is null
            || source.Equipment is null
            || source.Belt is null
            || source.InventoryGrid is null
            || source.Stash is null
            || source.MemorizedSpells is null
            || source.StatusEffects is null
            || source.VisitedLevelIds is null
            || !AuthoritativeLevelIdentityRules.IsSupported(source.LevelId)
            || source.VisitedLevelIds.Count > 256
            || source.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdentityRules.IsRegularLevel(levelId))
            || (AuthoritativeLevelIdentityRules.IsRegularLevel(source.LevelId)
                && source.VisitedLevelIds.Count > 0
                && !source.VisitedLevelIds.Contains(source.LevelId))
            || source.VisitedLevelIds.Distinct().Count() != source.VisitedLevelIds.Count
            || source.Life > expectedResources.LifeQ6
            || (source.Mana < 0 || source.ManaMaximum < 0)
                && (!source.NativeLifeBase.HasValue || !source.NativeManaBase.HasValue)
            || source.Mana < -(2000 << 6)
            || source.ManaMaximum < -(2000 << 6)
            || source.FullyExploredLevelIds is { } maps && (maps.Count > 256
                || maps.Distinct().Count() != maps.Count
                || maps.Any(levelId => levelId == 0 || !AuthoritativeLevelIdentityRules.IsRegularLevel(levelId)))
            || source.Mana > source.ManaMaximum
            || source.CharacterLevel is < 1 or > NativeCompatibilityLimits.MaximumNativeCharacterLevel
            || (!string.IsNullOrEmpty(source.CharacterName)
                && !DiabloPlayerName.IsValid(source.CharacterName))
            || !hasValidClass
            || !WorldState.IsWalkable(source.LevelId, source.PositionX, source.PositionY)
                && !WorldState.IsDeadPlayerOnCathedralDoor(source.Life, source.LevelId, source.PositionX, source.PositionY)
            || source.Inventory.Count > 40
            || source.Equipment.Count > 7
            || source.Belt.Count > 8
            || source.Stash.Count > DiabloStashLayout.MaximumItems
            || source.InventoryGrid.Count > 400
            || source.AbilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
            || !Enum.IsDefined(source.AbilityKind)
            || source.ReflectCharges > NativeCompatibilityLimits.MaximumNativeReflectCharges
            || source.DiabloKillLevel > NativeCompatibilityLimits.MaximumNativeDiabloKillLevel
            || source.MemorizedSpells.Count > AuthoritativePlayerLoadoutCatalog.MaximumMemorizedSpellCount
            || source.StatusEffects.Count > 128
            || !Enum.IsDefined(source.FacingDirection)
            || !Enum.IsDefined(source.CombatMode)
            || (source.CombatMode == DiabloPlayerCombatMode.Stand
                && source.CombatModeEndTick != 0)
            || ((source.CombatMode is DiabloPlayerCombatMode.Block
                    or DiabloPlayerCombatMode.GotHit)
                && source.CombatModeEndTick == 0)
            || !IsValidPlayerAction(source, snapshotTick)
            || source.LifeMaximum != (source.NativeLifeBase.HasValue ? Math.Clamp(expectedResources.LifeQ6, 64, 2000 << 6) : expectedResources.LifeQ6)
            || source.ManaMaximum >= 0 && source.ManaMaximum != (source.NativeManaBase.HasValue ? Math.Clamp(expectedResources.ManaQ6, 0, 2000 << 6) : expectedResources.ManaQ6)
            || (source.StashOpen && source.ActiveStoreId is not null)
            || !HasValidAttributeState(source.Attributes)) {
            throw new InvalidDataException(
                "The authoritative save contains out-of-range player state.");
        }

        if (source.MemorizedSpells.Any(spell =>
                spell is null
                || spell.SpellId is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                || spell.Level > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
            || source.MemorizedSpells.Select(spell => spell.SpellId).Distinct().Count()
                != source.MemorizedSpells.Count) {
            throw new InvalidDataException(
                "The authoritative save contains invalid memorized-spell state.");
        }

        if (source.StatusEffects.Any(effect =>
                effect is null
                || effect.EffectId == 0
                || effect.RemainingTicks == 0
                || !Enum.IsDefined(effect.Behavior)
                || ((effect.Behavior is AuthoritativeStatusEffectBehavior.Infravision
                        or AuthoritativeStatusEffectBehavior.Search
                        or AuthoritativeStatusEffectBehavior.Etherealize)
                    && effect.Magnitude != 0))
            || source.StatusEffects.Select(effect => effect.EffectId).Distinct().Count()
                != source.StatusEffects.Count) {
            throw new InvalidDataException(
                "The authoritative save contains invalid status-effect state.");
        }

        ValidateInventoryTopology(source);
    }

    private bool IsValidPlayerAction(StorePlayerSnapshot source, ulong snapshotTick)
    {
        var action = source.CurrentAction;
        if (action is null)
            return true;
        if (!Enum.IsDefined(action.Kind)
            || !AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(
                snapshotTick,
                action.StartedAtTick,
                action.EffectTick,
                action.ReadyTick,
                action.EndTick,
                action.EffectApplied)
            || action.EndTick != source.CombatModeEndTick) {
            return false;
        }

        return action.Kind switch {
            DiabloPlayerActionKind.Move =>
                source.CombatMode is DiabloPlayerCombatMode.WalkNorthwards
                    or DiabloPlayerCombatMode.WalkSouthwards
                    or DiabloPlayerCombatMode.WalkSideways
                && action.DirectionX is >= -1 and <= 1
                && action.DirectionY is >= -1 and <= 1
                && (action.DirectionX != 0 || action.DirectionY != 0)
                && action.TargetEntityId == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is null
                // Walkability and occupancy are effect-time decisions; only
                // the destination's bounds must survive save/restore.
                && WorldState.ContainsPosition(
                    source.LevelId,
                    checked(source.PositionX + action.DirectionX),
                    checked(source.PositionY + action.DirectionY)),
            DiabloPlayerActionKind.Attack =>
                source.CombatMode is DiabloPlayerCombatMode.Attack
                    or DiabloPlayerCombatMode.RangedAttack
                && action.DirectionX == 0
                && action.DirectionY == 0
                && (action.TargetCell
                    ? action.TargetEntityId == 0
                        && WorldState.ContainsPosition(
                            source.LevelId,
                            action.TargetX,
                            action.TargetY)
                    : action.TargetEntityId != 0
                        && (action.TargetX == 0 && action.TargetY == 0
                            || source.CombatMode == DiabloPlayerCombatMode.RangedAttack
                                && WorldState.ContainsPosition(
                                    source.LevelId,
                                    action.TargetX,
                                    action.TargetY)))
                && action.CastIntent is null,
            DiabloPlayerActionKind.Cast =>
                source.CombatMode == DiabloPlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is not null
                && action.CastIntent.SpellId != 0
                && action.TargetEntityId == action.CastIntent.TargetEntityId
                && IsValidCastTarget(source.LevelId, action.CastIntent)
                && IsValidCastSource(source, action.CastIntent, action.EffectApplied)
                && Enum.IsDefined(action.CastIntent.Source)
                && (!action.CastIntent.SourceItem.HasValue
                    || Enum.IsDefined(action.CastIntent.SourceItem.Value.Location)),
            DiabloPlayerActionKind.Death =>
                source.CombatMode == DiabloPlayerCombatMode.Death
                && source.Life == 0
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
                source.CombatMode == DiabloPlayerCombatMode.NewLevel
                && source.Life > 0
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
                source.CombatMode == DiabloPlayerCombatMode.NewLevel
                && source.Life > 0
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
    }

    private bool IsValidCastTarget(uint levelId, DiabloCastIntent cast)
    {
        if (cast.TargetX is < 0 or > byte.MaxValue
            || cast.TargetY is < 0 or > byte.MaxValue)
            return false;
        return cast.TargetEntityId != 0
            || WorldState.ContainsPosition(levelId, cast.TargetX, cast.TargetY);
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
        // Consumables are removed when their effect resolves, before the
        // animation tail completes. Retain only the native reference bounds
        // for this historical source; charged items still require ownership.
        if (effectApplied && cast.Source == DiabloCastSourceKind.ConsumableItem)
            return item.Location switch {
                DiabloPlayerItemLocation.Inventory =>
                    item.Slot < NativeCompatibilityLimits.MaximumInventoryCount,
                DiabloPlayerItemLocation.Belt =>
                    item.Slot < NativeCompatibilityLimits.MaximumBeltCount,
                DiabloPlayerItemLocation.Equipment =>
                    item.Slot < NativeCompatibilityLimits.MaximumEquipmentCount,
                DiabloPlayerItemLocation.Stash =>
                    item.Slot < NativeCompatibilityLimits.StashCellCount
                    && item.Page < NativeCompatibilityLimits.StashPageCount,
                _ => false,
            };
        return item.Location switch {
            DiabloPlayerItemLocation.Inventory =>
                item.Slot < player.Inventory.Count
                && MatchesStableItem(
                    player.Inventory[(int)item.Slot].State,
                    item.StableItemId),
            DiabloPlayerItemLocation.Belt =>
                item.Slot < NativeCompatibilityLimits.MaximumBeltCount
                && player.Belt.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Equipment =>
                item.Slot < NativeCompatibilityLimits.MaximumEquipmentCount
                && player.Equipment.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Stash =>
                player.StashOpen
                && item.Slot < NativeCompatibilityLimits.StashCellCount
                && item.Page < NativeCompatibilityLimits.StashPageCount
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

    private static AuthoritativePlayerActionState? ClonePlayerAction(
        AuthoritativePlayerActionState? source)
    {
        return source is null
            ? null
            : source with {
                CastIntent = source.CastIntent is null
                    ? null
                    : source.CastIntent with { },
            };
    }

    private void ValidateInventoryTopology(StorePlayerSnapshot source)
    {
        var itemIdentities = new HashSet<(uint Seed, uint CreateInfo, int ItemIndex)>();
        foreach (var item in source.Inventory) {
            if (item is null
                || item.State is null
                || !IsNativeItemPrice(item.Price)
                || !HasKnownStableItemIdentity(item.State)
                || !HasValidItemIdentity(item.ItemSeed, item.State)
                || !InventoryGridLayout.HasValidDimensions(
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid inventory item.");
            }
            if (!itemIdentities.Add((
                    item.ItemSeed,
                    item.State.CreateInfo,
                    item.State.ItemIndex))) {
                throw new InvalidDataException(
                    "The authoritative save contains a duplicate item seed.");
            }
        }
        foreach (var item in source.Equipment) {
            if (item is null
                || item.State is null
                || !HasKnownStableItemIdentity(item.State)
                || !HasValidItemIdentity(item.ItemSeed, item.State)
                || item.Slot >= 7
                || !InventoryGridLayout.HasValidDimensions(
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)
                || !itemIdentities.Add((
                    item.ItemSeed,
                    item.State.CreateInfo,
                    item.State.ItemIndex))) {
                throw new InvalidDataException(
                    "The authoritative save contains invalid or duplicate equipment.");
            }
        }
        if (source.Equipment.Select(item => item.Slot).Distinct().Count()
            != source.Equipment.Count) {
            throw new InvalidDataException(
                "The authoritative save contains duplicate equipment slots.");
        }
        foreach (var item in source.Belt) {
            if (item is null
                || item.State is null
                || !HasKnownStableItemIdentity(item.State)
                || !HasValidItemIdentity(item.ItemSeed, item.State)
                || item.Slot >= 8
                || !InventoryGridLayout.HasValidDimensions(
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)
                || !itemIdentities.Add((
                    item.ItemSeed,
                    item.State.CreateInfo,
                    item.State.ItemIndex))) {
                throw new InvalidDataException(
                    "The authoritative save contains invalid or duplicate belt items.");
            }
        }
        if (source.Belt.Select(item => item.Slot).Distinct().Count()
            != source.Belt.Count) {
            throw new InvalidDataException(
                "The authoritative save contains duplicate belt slots.");
        }
        var stashPages = new Dictionary<uint, int[]>();
        foreach (var item in source.Stash) {
            if (item is null
                || item.Page >= DiabloStashLayout.PageCount
                || item.Cell >= DiabloStashLayout.CellCount
                || item.State is null
                || !IsNativeItemPrice(item.Price)
                || !HasKnownStableItemIdentity(item.State)
                || !HasValidItemIdentity(item.ItemSeed, item.State)
                || !InventoryGridLayout.HasValidDimensions(
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)
                || !itemIdentities.Add((
                    item.ItemSeed,
                    item.State.CreateInfo,
                    item.State.ItemIndex))) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid or duplicate stash item.");
            }
            if (!stashPages.TryGetValue(item.Page, out var page)) {
                page = DiabloStashLayout.CreateEmptyPage();
                stashPages.Add(item.Page, page);
            }
            if (!DiabloStashLayout.CanPlace(
                    page,
                    item.Cell,
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)) {
                throw new InvalidDataException(
                    "The authoritative save contains overlapping stash items.");
            }
            DiabloStashLayout.Fill(
                page,
                item.Cell,
                item.State.InventoryWidth,
                item.State.InventoryHeight);
        }
        if (source.HeldItem is { } heldItem
            && (!IsNativeItemPrice(heldItem.Price)
                || heldItem.State is null
                || !HasKnownStableItemIdentity(heldItem.State)
                || !HasValidItemIdentity(heldItem.ItemSeed, heldItem.State)
                || !InventoryGridLayout.HasValidDimensions(
                    heldItem.State.InventoryWidth,
                    heldItem.State.InventoryHeight)
                || !itemIdentities.Add((
                    heldItem.ItemSeed,
                    heldItem.State.CreateInfo,
                    heldItem.State.ItemIndex))
                || (DiabloGoldInventory.HasGoldIdentity(heldItem.State, CombatRules, Items)
                    && !DiabloGoldInventory.IsValidStack(heldItem, CombatRules, Items)))) {
            throw new InvalidDataException(
                "The authoritative save contains an invalid or duplicate held item.");
        }
        if (source.InventoryGrid.Any(cell => cell < -1 || cell >= source.Inventory.Count)) {
            throw new InvalidDataException(
                "The authoritative save contains an invalid inventory-grid reference.");
        }

        for (var inventoryIndex = 0; inventoryIndex < source.Inventory.Count; inventoryIndex++) {
            var state = source.Inventory[inventoryIndex].State;
            var anchor = -1;
            for (var cell = 0; cell < source.InventoryGrid.Count; cell++) {
                if (source.InventoryGrid[cell] == inventoryIndex) {
                    anchor = cell;
                    break;
                }
            }
            if (anchor < 0)
                continue;
            if (!InventoryGridLayout.HasExactFootprint(
                    source.InventoryGrid,
                    anchor,
                    inventoryIndex,
                    state.InventoryWidth,
                    state.InventoryHeight)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid inventory footprint.");
            }
        }
    }

    private static bool HasValidItemIdentity(
        uint itemSeed,
        AuthoritativeItemState state)
    {
        var hasPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.NativeCursorGraphic.HasValue;
        if (!hasPresentation)
            return itemSeed != 0;
        var hasValidNames = !string.IsNullOrEmpty(state.BaseName)
            && !string.IsNullOrEmpty(state.IdentifiedName)
            && !state.BaseName.Contains('\0')
            && !state.IdentifiedName.Contains('\0')
            && System.Text.Encoding.UTF8.GetByteCount(state.BaseName) < 64
            && System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) < 64;
        return hasValidNames
            && (!state.NativeCursorGraphic.HasValue
                || state.NativeCursorGraphic is >= 0 and <= byte.MaxValue);
    }

    private static bool HasValidAttributeState(PlayerAttributesState? attributes)
    {
        return attributes is not null
            && attributes.Strength is { Base: >= 0, Current: >= 0 }
            && attributes.Magic is { Base: >= 0, Current: >= 0 }
            && attributes.Dexterity is { Base: >= 0, Current: >= 0 }
            && attributes.Vitality is { Base: >= 0, Current: >= 0 };
    }

    private static bool IsNativeItemPrice(uint price) => price <= int.MaxValue;

    private static int NativeTownDirectionIndex(string direction)
        => direction switch {
            "south" => 0,
            "south_west" => 1,
            "west" => 2,
            "north_west" => 3,
            "north" => 4,
            "north_east" => 5,
            "east" => 6,
            "south_east" => 7,
            _ => throw new InvalidDataException(
                $"Native Town roster has unsupported direction '{direction}'."),
        };

    private bool HasKnownStableItemIdentity(AuthoritativeItemState state)
    {
        return Items is null
            || state.StableItemId == 0
            || Items.TryGet(state.StableItemId, out _);
    }
}
