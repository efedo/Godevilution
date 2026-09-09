using Devilution.Domain.Protocol;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Immutable protocol-neutral monster projection. */
public sealed record DiabloMonsterState(
    uint EntityId,
    uint MonsterId,
    uint LevelId,
    int PositionX,
    int PositionY,
    int HitPoints,
    int MaxHitPoints,
    int ArmorClass,
    int AttackDamage,
    int AggroRange,
    int FireResistance,
    int LightningResistance,
    int MagicResistance,
    int AcidResistance = 0)
{
    public int MonsterLevel { get; init; }

    public bool Moving { get; init; }

    public int FuturePositionX { get; init; } = PositionX;

    public int FuturePositionY { get; init; } = PositionY;

    /** Server tick at which the current one-tick walk was admitted. */
    public ulong MovementStartedTick { get; init; }

    /** First server tick at which the current walk is complete. */
    public ulong MovementEffectTick { get; init; }

    /** Native monster direction used by movement, attacks, and Hork Spawn. */
    public int Direction { get; init; }

    /** Native InitMonster seed used for monster-owned item drops. */
    public uint MonsterItemSeed { get; init; }

    /** Native InitMonster seed used for multiplayer AI synchronization. */
    public uint MonsterAiSeed { get; init; }

    /** Server-owned dialogue identity currently displayed by a unique encounter. */
    public int DialogueMessageId { get; init; }

    /** Server-owned authored dialogue state used by native compatibility presentation. */
    public AuthoritativeMonsterDialogueState DialogueState { get; init; }

    public bool Petrified { get; init; }

    public bool StoneCurseImmune { get; init; }

    public bool IsUndead { get; init; }

    /** Native monster class capability used by demon-specific equipment. */
    public bool IsDemon { get; init; }

    /** Native monster class capability used by sword/mace damage modifiers. */
    public bool IsAnimal { get; init; }

    /** Native monster ability that displaces a player after a successful hit. */
    public bool HasKnockback { get; init; }

    /** Native monster faction marker used by Apocalypse's minion exclusion. */
    public bool IsPlayerMinion { get; init; }

    /** Authoritative owner entity for a summoned player minion, when present. */
    public uint OwnerPlayerId { get; init; }

    public bool IsUnique { get; init; }

    /** Native unique-monster identity retained by the authoritative save boundary. */
    public string UniqueIdentity { get; init; } = string.Empty;

    /** Native leader identity retained for authoritative monster pack unity. */
    public uint LeaderEntityId { get; init; }

    /** Native leashed/separated relationship retained across save restore. */
    public AuthoritativeMonsterLeaderRelation LeaderRelation { get; init; }

    /** Native pack size retained on a monster leader. */
    public int PackSize { get; init; }

    /** Native activation countdown propagated through leashed pack members. */
    public int ActiveForTicks { get; init; }

    public bool IsDiablo { get; init; }

    public bool BerserkImmune { get; init; }

    public int MinDamage { get; init; }

    public int MaxDamage { get; init; }

    public int MinDamageSpecial { get; init; }

    public int MaxDamageSpecial { get; init; }

    public bool IsBerserked { get; init; }

    /** Native death-animation duration imported from content. */
    public int DeathAnimationTicks { get; init; }

    /** Native experience reward imported from content. */
    public uint ExperienceReward { get; init; }

    /** Native normal-attack chance to hit imported from content. */
    public int ToHit { get; init; }

    /** Native special-attack chance to hit imported from content. */
    public int ToHitSpecial { get; init; }

    /** Native MonsterAIID dispatch identity imported from content. */
    public string NativeAi { get; init; } = string.Empty;

    /** Native intelligence value imported from content. */
    public int Intelligence { get; init; }

    /** Native Gargoyle special lock held while it remains safely distant after healing. */
    public bool GargoyleSpecialActive { get; init; }

    /** Remaining terminal death-animation ticks, when the monster is dead. */
    public int DeathTicksRemaining { get; init; }

    /** Native Fallen fear countdown after a nearby Fallen dies. */
    public int FallenFearTicks { get; init; }

    /** Direction of the native Fallen fear retreat. */
    public int FallenFearDirection { get; init; }

    /** Current native Fallen standing-animation frame. */
    public int FallenStandAnimationFrame { get; init; }

    /** Current native Fallen standing-animation tick. */
    public int FallenStandAnimationTick { get; init; }

    /** Remaining native Fallen rally attack ticks. */
    public int FallenRallyTicks { get; init; }

    /** Native Sneak visibility state; hidden monsters are not targetable. */
    public bool SneakHidden { get; init; }

    /** Remaining native Sneak retreat ticks after a successful hit. */
    public int SneakRetreatTicks { get; init; }

    /** Remaining native Scavenger corpse-eating ticks. */
    public int ScavengerHealingTicks { get; init; }

    /** Selected corpse identity for the native Scavenger healing goal. */
    public uint ScavengerCorpseEntityId { get; init; }

    /** Remaining native Bat retreat steps after an adjacent attack. */
    public int BatRetreatTicks { get; init; }

    /** Direction toward the victim when the native Bat retreat started. */
    public int BatRetreatDirection { get; init; }

    /** Native Bat var2-compatible movement counter used by movement admissions. */
    public int BatMovementIdleTicks { get; init; } = -1;

    /** Native Bat var1-compatible movement value retained across movement. */
    public int BatMovementMode { get; init; }

    /** Native AiRangedAvoidance goalVar1; -1 means the Normal goal is active. */
    public int RangedAvoidanceMoveTicks { get; init; } = -1;

    /** Native AiRangedAvoidance goalVar2 turn state (0 or 1). */
    public int RangedAvoidanceTurn { get; init; }

    /** Native AiRangedAvoidance var2-compatible movement counter. */
    public int RangedAvoidanceMovementIdleTicks { get; init; } = -1;

    /** Native AiRangedAvoidance var1-compatible movement value. */
    public int RangedAvoidanceMovementMode { get; init; }

    /** Native Rhino goalVar1 movement counter; -1 means the Normal goal. */
    public int RhinoMovementTicks { get; init; } = -1;

    /** Native Rhino goalVar2 turn state (0 or 1). */
    public int RhinoMovementTurn { get; init; }

    /** Native AiAvoidance goalVar1 movement counter; -1 means Normal. */
    public int AvoidanceMovementTicks { get; init; } = -1;

    /** Native AiAvoidance goalVar2 turn state (0 or 1). */
    public int AvoidanceMovementTurn { get; init; }

    /** Native AiAvoidance var2-compatible movement counter used by admissions. */
    public int AvoidanceMovementIdleTicks { get; init; } = -1;

    /** Native AiAvoidance var1-compatible movement value retained across movement. */
    public int AvoidanceMovementMode { get; init; }

    /** Remaining native SkeletonAi delay ticks before the next decision. */
    public int SkeletonMeleeDelayTicks { get; init; }

    /** Native SkeletonBow var2-compatible movement counter. */
    public int SkeletonRangedMovementIdleTicks { get; init; } = -1;

    /** Native SkeletonBow var1-compatible movement value. */
    public int SkeletonRangedMovementMode { get; init; }

    /** Native OverlordAi var2-compatible movement counter. */
    public int FatMovementIdleTicks { get; init; } = -1;

    /** Native OverlordAi var1-compatible movement value. */
    public int FatMovementMode { get; init; }

    /** Native Rhino var2-compatible movement counter. */
    public int RhinoMovementIdleTicks { get; init; } = -1;

    /** Native Rhino var1-compatible movement value. */
    public int RhinoMovementMode { get; init; }

    /** Remaining native Rhino AiDelay ticks before the next decision. */
    public int RhinoDelayTicks { get; init; }

    /** Remaining native LeoricAi delay ticks before the next decision. */
    public int SkeletonKingDelayTicks { get; init; }

    /** Remaining native HorkDemonAi delay ticks before the next decision. */
    public int HorkDemonDelayTicks { get; init; }

    /** Remaining native AiRangedAvoidance delay ticks before the next decision. */
    public int RangedAvoidanceDelayTicks { get; init; }

    /** Remaining native SnakeAi delay ticks before the next decision. */
    public int SnakeDelayTicks { get; init; }

    /** Remaining native CounselorAi delay ticks before the next decision. */
    public int CounselorDelayTicks { get; init; }

    /** Remaining native AiAvoidance delay ticks before the next decision. */
    public int AvoidanceDelayTicks { get; init; }

    /** Remaining native AiRanged delay ticks after a ranged attack. */
    public int RangedDelayTicks { get; init; }

    /** Remaining native MegaAi post-action delay ticks. */
    public int MegaDelayTicks { get; init; }

    /** Remaining native Counselor retreat attempts after a wounded adjacent turn. */
    public int CounselorRetreatTicks { get; init; }

    public int HitPointsQ6 { get; init; } = DeterministicFixedPointQ6.FromWhole(HitPoints);

    public int MaxHitPointsQ6 { get; init; } = DeterministicFixedPointQ6.FromWhole(MaxHitPoints);
}

/** Immutable protocol-neutral world-item projection. */
public sealed record DiabloWorldItemState(
    uint EntityId,
    uint LevelId,
    int PositionX,
    int PositionY,
    uint ItemSeed,
    uint Price,
    AuthoritativeItemState State);

/** Immutable server-owned corpse projection. */
public sealed record DiabloCorpseState(
    uint EntityId,
    uint LevelId,
    int PositionX,
    int PositionY);

/** Immutable protocol-neutral world-object projection. */
public sealed record DiabloObjectState(
    uint EntityId,
    uint ObjectId,
    uint LevelId,
    int PositionX,
    int PositionY,
    bool Activated,
    uint QuestId,
    AuthoritativeObjectEffectKind EffectKind,
    int EffectAmount)
{
    public uint QuestProgressAmount { get; init; } = 1;

    public bool MissilePassable { get; init; }

    public bool Breakable { get; init; }

    public bool Destroyed { get; init; }

    public bool TrapArmed { get; init; }

    public bool IsShrine { get; init; }

    public AuthoritativeObjectQuestTransitionKind QuestTransitionKind { get; init; }

    public int QuestTransitionAmount { get; init; }

    public int QuestTransitionRequiredVariable1 { get; init; } = -1;

    public int QuestTransitionRequiredVariable2 { get; init; } = -1;

    public AuthoritativeObjectRewardKind RewardKind { get; init; }

    public uint RewardId { get; init; }

    public int RewardItemLevel { get; init; }

    public int RewardOffsetX { get; init; }

    public int RewardOffsetY { get; init; }

    public AuthoritativeObjectRewardOriginKind RewardOriginKind { get; init; }

    public int RewardSetLevelId { get; init; } = -1;

    public int ProjectileTargetOffsetX { get; init; }

    public int ProjectileTargetOffsetY { get; init; }

    public AuthoritativeQuestSequenceFollowUpKind QuestSequenceFollowUpKind { get; init; }

    public uint QuestSequenceFollowUpAmount { get; init; }

    public int QuestSequenceFollowUpOffsetX { get; init; }

    public int QuestSequenceFollowUpOffsetY { get; init; }

    public AuthoritativeObjectProjectileSourceKind ProjectileSourceKind { get; init; }

    public uint QuestSequenceFollowUpWorldMutationId { get; init; }

    public bool ActivateOnOccupancy { get; init; }

    public int RequiredActivatedLinkedObjects { get; init; }

    /** Optional native object type used only by compatibility presentations. */
    public uint NativeObjectId { get; init; }

    /** Compatibility presentation may retain an activated object on the map. */
    public bool HideWhenActivated { get; init; } = true;

    /** Native-compatible animation frame before activation. */
    public int AnimationFrame { get; init; }

    /** Native-compatible animation frame selected when the object activates. */
    public int ActivatedAnimationFrame { get; init; }

    /** Optional authored dialogue emitted when this object is activated. */
    public int DialogueMessageId { get; init; }
}

/** Immutable protocol-neutral quest projection. */
public sealed record DiabloQuestState(
    uint QuestId,
    uint LevelId,
    uint Progress,
    uint RequiredProgress,
    bool Completed,
    bool Unavailable = false,
    bool Active = true,
    bool Logged = false,
    int Variable1 = 0,
    int Variable2 = 0,
    int MessageId = 0,
    uint CompletionMonsterId = 0,
    string CompletionUniqueIdentity = "",
    int TurnInTownerId = -1,
    bool TurnInOnCompletion = false,
    int TurnInCompletionVariable2 = -1,
    uint TurnInAlternateItemId = 0,
    int TurnInAlternateMessageId = 0,
    bool TurnInActivationOnly = false,
    int TurnInMessageId = 0,
    bool TurnInDialogueOnly = false,
    int TurnInCompletionRequiredVariable1 = -1,
    int TurnInCompletionVariable1 = -1,
    int TurnInRequiredVariable1 = -1,
    int TurnInVariable1 = -1,
    bool TurnInProgressOnly = false,
    uint TurnInBlockedVisitedLevel = 0)
{
    /** Native quest ordinal used by compatibility clients; zero is a valid ordinal. */
    public uint? NativeQuestId { get; init; }
}

/** Immutable protocol-neutral projectile projection. */
public sealed record DiabloProjectileState(
    uint EntityId,
    uint SourceEntityId,
    uint TargetEntityId,
    uint SpellId,
    uint LevelId,
    int PositionX,
    int PositionY,
    int TargetX,
    int TargetY,
    int Damage,
    AuthoritativeDamageType DamageType,
    int AreaRadius,
    uint RemainingTicks)
{
    public AuthoritativeProjectileBehavior Behavior { get; init; }

    public string? NativeMissileId { get; init; }

    public AuthoritativeProjectilePhase Phase { get; init; }

    public int StartX { get; init; }

    public int StartY { get; init; }

    public int TraveledX { get; init; }

    public int TraveledY { get; init; }

    public int VelocityX { get; init; }

    public int VelocityY { get; init; }

    public int OriginalDirection { get; init; }

    public int PathIndex { get; init; }

    public int PathTicks { get; init; }

    public int Speed { get; init; }

    public int ImpactLifetime { get; init; } = 1;

    public int ResistantDamageDivisor { get; init; } = 1;

    public int EffectiveSpellLevel { get; init; }

    public uint LastCollisionTargetEntityId { get; init; }

    public int WallLeftX { get; init; }

    public int WallLeftY { get; init; }

    public int WallRightX { get; init; }

    public int WallRightY { get; init; }

    public int WallLeftDirection { get; init; } = -1;

    public int WallRightDirection { get; init; } = -1;

    public bool WallLeftStopped { get; init; }

    public bool WallRightStopped { get; init; }

    public int WallHitDirection { get; init; } = -1;

    public int ApocalypseScanX { get; init; }

    public int ApocalypseScanY { get; init; }

    public bool ApocalypseHit { get; init; }

    public bool RingOfFireLimitReached { get; init; }

    public int GuardianState { get; init; }

    public int ElementalOriginX { get; init; }

    public int ElementalOriginY { get; init; }

    public int DamageQ6 { get; init; }

    public int ElementalFireDamage { get; init; }

    public int ElementalLightningDamage { get; init; }

    public bool AttackHit { get; init; }

    public int PerilSelfDamageQ6 { get; init; }

    public bool DoppelgangerProc { get; init; }

    public uint ActivationDelayTicks { get; init; }

    /** Dynamic monster-arrow minimum damage rolled at impact; zero means fixed payload. */
    public int DamageMinimum { get; init; }

    /** Dynamic monster-arrow maximum damage rolled at impact; zero means fixed payload. */
    public int DamageMaximum { get; init; }
}

/** Remaining stock for the store visible to the projected player. */
public sealed record DiabloStoreState(uint StoreId, IReadOnlyList<StoreItem> Items);

/** Persisted origin for one runtime-generated set-piece object group. */
public sealed record DiabloSetLevelObjectOriginState(
    int SetLevelId,
    uint LevelId,
    int PositionX,
    int PositionY);

/**
 * Transport-independent snapshot used by protocol and persistence adapters.
 * Collections are captured in stable identity order.
 */
public sealed record DiabloSimulationSnapshot(
    ulong Tick,
    StorePlayerSnapshot Player,
    IReadOnlyList<DiabloMonsterState> Monsters,
    IReadOnlyList<DiabloWorldItemState> WorldItems,
    IReadOnlyList<DiabloObjectState> Objects,
    IReadOnlyList<DiabloQuestState> Quests,
    IReadOnlyList<DiabloProjectileState> Projectiles,
    DiabloStoreState? ActiveStore)
{
    public uint RandomState { get; init; }

    /** Dynamic and external portal routes owned by the authoritative world. */
    public IReadOnlyList<DiabloPortalState> Portals { get; init; } = [];

    /** Defeated non-minion monster tiles used by corpse-consuming AI. */
    public IReadOnlyList<DiabloCorpseState> Corpses { get; init; } = [];

    /** Native-compatible terminal Diablo defeat state. */
    public bool VictoryTriggered { get; init; }

    /** Native difficulty persisted with the authoritative session. */
    public DiabloDifficulty Difficulty { get; init; } = DiabloDifficulty.Normal;

    /** Content-owned repeatable quest/towner interaction definitions. */
    public IReadOnlyList<AuthoritativeQuestTownerInteractionDefinition> QuestTownerInteractions { get; init; } = [];

    /** Content-owned general towner greeting definitions. */
    public IReadOnlyList<AuthoritativeTownerGreetingDefinition> TownerGreetings { get; init; } = [];

    /** Active content world-cell mutations persisted with the session. */
    public IReadOnlyList<uint> ActiveWorldMutationIds { get; init; } = [];

    /** Runtime-generated set-piece origins persisted with the session. */
    public IReadOnlyList<DiabloSetLevelObjectOriginState> SetLevelObjectOrigins { get; init; } = [];

    /** Procedural encounter levels already materialized by the server. */
    public IReadOnlyList<uint> MaterializedProceduralLevelIds { get; init; } = [];
}

public sealed partial class DiabloSimulation
{
    public StorePlayerSnapshot CapturePlayerState(AuthoritativePlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var snapshot = new StorePlayerSnapshot(
            player.Gold,
            player.ActiveStoreId,
            player.Inventory.ToArray(),
            player.Experience,
            player.Life,
            player.Mana,
            player.ManaMaximum,
            player.Attributes,
            player.Equipment.ToArray(),
            player.InventoryGrid.ToArray(),
            player.Belt.ToArray()) {
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
            VisitedLevelIds = (AuthoritativeLevelIdentityRules.IsRegularLevel(player.LevelId)
                    ? player.VisitedLevelIds.Append(player.LevelId)
                    : player.VisitedLevelIds)
                .Distinct()
                .Where(AuthoritativeLevelIdentityRules.IsRegularLevel)
                .OrderBy(levelId => levelId)
                .ToArray(),
            AbilitySpellId = player.AbilitySpellId,
            AbilityKind = player.AbilityKind,
            MemorizedSpells = player.MemorizedSpells.ToArray(),
            StatusEffects = player.StatusEffects.ToArray(),
            FriendlyMode = player.FriendlyMode,
            FacingDirection = player.FacingDirection,
            CombatMode = player.CombatMode,
            CombatModeEndTick = player.CombatModeEndTick,
            CurrentAction = ClonePlayerAction(player.CurrentAction),
            HeldItem = player.HeldItem,
            Stash = player.Stash.ToArray(),
            StashGold = player.StashGold,
            StashOpen = player.StashOpen,
            LightRadius = player.LightRadius,
        };
        return ResolveSnapshotItemFootprints(snapshot);
    }

    public DiabloSimulationSnapshot CaptureSnapshot(
        AuthoritativePlayerState player,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        var activeStore = player.ActiveStoreId is uint storeId
            ? new DiabloStoreState(
                storeId,
                storeCatalog.GetItems(storeId)
                    .Select(item => item with {
                        State = ResolveItemState(item.State, item.ItemSeed),
                    })
                    .ToArray())
            : null;
        foreach (var target in WorldState.CombatTargets.Values)
            target.EnsureMovementTiming(tick);
        return new DiabloSimulationSnapshot(
            tick,
            CapturePlayerState(player),
            WorldState.CombatTargets.Values
                .OrderBy(target => target.EntityId)
                .Select(target => new DiabloMonsterState(
                    target.EntityId,
                    target.MonsterId,
                    target.LevelId,
                    target.PositionX,
                    target.PositionY,
                    target.HitPoints,
                    target.MaxHitPoints,
                    target.ArmorClass,
                    target.AttackDamage,
                    target.AggroRange,
                    target.FireResistance,
                    target.LightningResistance,
                    target.MagicResistance,
                    target.AcidResistance) {
                    MovementStartedTick = target.IsMoving ? target.MovementStartedTick : 0,
                    MovementEffectTick = target.IsMoving ? target.MovementEffectTick : 0,
                    MonsterLevel = target.MonsterLevel,
                    Moving = target.IsMoving,
                    FuturePositionX = target.FuturePositionX,
                    FuturePositionY = target.FuturePositionY,
                    Direction = target.Direction,
                    MonsterItemSeed = target.MonsterItemSeed,
                    MonsterAiSeed = target.MonsterAiSeed,
                    DialogueMessageId = target.DialogueMessageId,
                    DialogueState = target.DialogueState,
                    Petrified = target.IsPetrified,
                    StoneCurseImmune = target.StoneCurseImmune,
                    IsUndead = target.IsUndead,
                    IsDemon = target.IsDemon,
                    IsAnimal = target.IsAnimal,
                    HasKnockback = target.HasKnockback,
                    IsPlayerMinion = target.IsPlayerMinion,
                    OwnerPlayerId = target.OwnerPlayerId,
                    IsUnique = target.IsUnique,
                    UniqueIdentity = target.UniqueIdentity,
                    LeaderEntityId = target.LeaderEntityId,
                    LeaderRelation = target.LeaderRelation,
                    PackSize = target.PackSize,
                    ActiveForTicks = target.ActiveForTicks,
                    IsDiablo = target.IsDiablo,
                    BerserkImmune = target.BerserkImmune,
                    MinDamage = target.MinDamage,
                    MaxDamage = target.MaxDamage,
                    MinDamageSpecial = target.MinDamageSpecial,
                    MaxDamageSpecial = target.MaxDamageSpecial,
                    IsBerserked = target.IsBerserked,
                    DeathAnimationTicks = target.DeathAnimationTicks,
                    ExperienceReward = target.ExperienceReward,
                    ToHit = target.ToHit,
                    ToHitSpecial = target.ToHitSpecial,
                    NativeAi = target.NativeAi,
                    Intelligence = target.Intelligence,
                    GargoyleSpecialActive = target.GargoyleSpecialActive,
                    DeathTicksRemaining = target.DeathTicksRemaining,
                    FallenFearTicks = target.FallenFearTicks,
                    FallenFearDirection = target.FallenFearDirection,
                    FallenStandAnimationFrame = target.FallenStandAnimationFrame,
                    FallenStandAnimationTick = target.FallenStandAnimationTick,
                    FallenRallyTicks = target.FallenRallyTicks,
                    SneakHidden = target.SneakHidden,
                    SneakRetreatTicks = target.SneakRetreatTicks,
                    ScavengerHealingTicks = target.ScavengerHealingTicks,
                    ScavengerCorpseEntityId = target.ScavengerCorpseEntityId,
                    BatRetreatTicks = target.BatRetreatTicks,
                    BatRetreatDirection = target.BatRetreatDirection,
                    BatMovementIdleTicks = target.BatMovementIdleTicks,
                    BatMovementMode = target.BatMovementMode,
                    RangedAvoidanceMoveTicks = target.RangedAvoidanceMoveTicks,
                    RangedAvoidanceTurn = target.RangedAvoidanceTurn,
                    RangedAvoidanceMovementIdleTicks = target.RangedAvoidanceMovementIdleTicks,
                    RangedAvoidanceMovementMode = target.RangedAvoidanceMovementMode,
                    RhinoMovementTicks = target.RhinoMovementTicks,
                    RhinoMovementTurn = target.RhinoMovementTurn,
                    AvoidanceMovementTicks = target.AvoidanceMovementTicks,
                    AvoidanceMovementTurn = target.AvoidanceMovementTurn,
                    AvoidanceMovementIdleTicks = target.AvoidanceMovementIdleTicks,
                    AvoidanceMovementMode = target.AvoidanceMovementMode,
                    SkeletonMeleeDelayTicks = target.SkeletonMeleeDelayTicks,
                    SkeletonRangedMovementIdleTicks = target.SkeletonRangedMovementIdleTicks,
                    SkeletonRangedMovementMode = target.SkeletonRangedMovementMode,
                    FatMovementIdleTicks = target.FatMovementIdleTicks,
                    FatMovementMode = target.FatMovementMode,
                    RhinoMovementIdleTicks = target.RhinoMovementIdleTicks,
                    RhinoMovementMode = target.RhinoMovementMode,
                    RhinoDelayTicks = target.RhinoDelayTicks,
                    SkeletonKingDelayTicks = target.SkeletonKingDelayTicks,
                    HorkDemonDelayTicks = target.HorkDemonDelayTicks,
                    RangedAvoidanceDelayTicks = target.RangedAvoidanceDelayTicks,
                    SnakeDelayTicks = target.SnakeDelayTicks,
                    CounselorDelayTicks = target.CounselorDelayTicks,
                    AvoidanceDelayTicks = target.AvoidanceDelayTicks,
                    RangedDelayTicks = target.RangedDelayTicks,
                    MegaDelayTicks = target.MegaDelayTicks,
                    CounselorRetreatTicks = target.CounselorRetreatTicks,
                    HitPointsQ6 = target.HitPointsQ6,
                    MaxHitPointsQ6 = target.MaxHitPointsQ6,
                })
                .ToArray(),
            WorldState.WorldItems.Values
                .OrderBy(item => item.EntityId)
                .Select(item => new DiabloWorldItemState(
                    item.EntityId,
                    item.LevelId,
                    item.PositionX,
                    item.PositionY,
                    item.ItemSeed,
                    item.Price,
                    ResolveWorldItemState(item)))
                .ToArray(),
            WorldState.Objects.Values
                .OrderBy(@object => @object.EntityId)
                .Select(@object => new DiabloObjectState(
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
                    HideWhenActivated = @object.HideWhenActivated,
                    AnimationFrame = @object.AnimationFrame,
                    ActivatedAnimationFrame = @object.ActivatedAnimationFrame,
                    DialogueMessageId = @object.DialogueMessageId,
                })
                .ToArray(),
            WorldState.Quests.Values
                .OrderBy(quest => quest.QuestId)
                .Select(quest => new DiabloQuestState(
                    quest.QuestId,
                    quest.LevelId,
                    quest.Progress,
                    quest.RequiredProgress,
                    quest.Completed,
                    quest.Unavailable,
                    quest.Active,
                    quest.Logged,
                    quest.Variable1,
                    quest.Variable2,
                    quest.MessageId,
                    quest.CompletionMonsterId,
                    quest.CompletionUniqueIdentity,
                    quest.TurnInTownerId,
                    quest.TurnInOnCompletion,
                    quest.TurnInCompletionVariable2,
                    quest.TurnInAlternateItemId,
                    quest.TurnInAlternateMessageId,
                    quest.TurnInActivationOnly,
                    quest.TurnInMessageId,
                    quest.TurnInDialogueOnly,
                    quest.TurnInCompletionRequiredVariable1,
                    quest.TurnInCompletionVariable1,
                    quest.TurnInRequiredVariable1,
                    quest.TurnInVariable1,
                    quest.TurnInProgressOnly,
                    quest.TurnInBlockedVisitedLevel) {
                    NativeQuestId = QuestTypes is not null
                        && QuestTypes.TryGet(quest.QuestId, out var definition)
                        ? definition.NativeQuestId
                        : null,
                })
                .ToArray(),
            WorldState.Projectiles.Values
                .OrderBy(projectile => projectile.EntityId)
                .Select(projectile => new DiabloProjectileState(
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
                })
                .ToArray(),
            activeStore) {
            RandomState = random.State,
            Portals = WorldState.Portals.Values
                .OrderBy(portal => portal.PortalId)
                .Select(portal => new DiabloPortalState(
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
                })
                .ToArray(),
            Corpses = WorldState.Corpses.Values
                .OrderBy(corpse => corpse.EntityId)
                .Select(corpse => new DiabloCorpseState(
                    corpse.EntityId,
                    corpse.LevelId,
                    corpse.PositionX,
                    corpse.PositionY))
                .ToArray(),
            VictoryTriggered = WorldState.VictoryTriggered,
            Difficulty = this.Difficulty,
            QuestTownerInteractions = QuestTownerInteractions?.Definitions ?? [],
            TownerGreetings = TownerGreetings?.Definitions.ToArray() ?? [],
            ActiveWorldMutationIds = WorldState.ActiveWorldMutationIds
                .OrderBy(mutationId => mutationId)
                .ToArray(),
            SetLevelObjectOrigins = setLevelObjectOrigins
                .OrderBy(pair => pair.Key)
                .Select(pair => new DiabloSetLevelObjectOriginState(
                    pair.Key,
                    pair.Value.LevelId,
                    pair.Value.PositionX,
                    pair.Value.PositionY))
                .ToArray(),
            MaterializedProceduralLevelIds = materializedProceduralLevels
                .OrderBy(levelId => levelId)
                .ToArray(),
        };
    }

    /** Restores validated module state without knowing its serialized representation. */
    public void RestoreSnapshot(
        AuthoritativePlayerState destination,
        DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(snapshot);
        var previous = CaptureSnapshot(destination, snapshot.Tick);
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
            || snapshot.MaterializedProceduralLevelIds is null) {
            throw new InvalidDataException(
                "The authoritative save contains incomplete world state.");
        }
        var restoredSetLevelObjectOrigins = ValidateSavedSetLevelObjectOrigins(
            snapshot.SetLevelObjectOrigins);
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
        source = ResolveSnapshotItemFootprints(source);
        snapshot = snapshot with {
            Player = source,
            WorldItems = snapshot.WorldItems
                .Select(ResolveWorldItemState)
                .ToArray(),
            ActiveStore = ResolveStoreState(snapshot.ActiveStore),
        };
        WorldState.RestoreWorldMutations(snapshot.ActiveWorldMutationIds);
        ValidateSavedPlayer(source, destination);

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
                || (monster.MonsterItemSeed == 0) != (monster.MonsterAiSeed == 0)
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
            if (!WorldState.IsWalkable(@object.LevelId, @object.PositionX, @object.PositionY)
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
            var isMonsterArrow = projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow
                && restoredMonsters.Any(monster => monster.EntityId == projectile.SourceEntityId);
            var nativeMissileIdentityValid = projectile.NativeMissileId is null
                || projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic
                    && MonsterMissiles is not null
                    && MonsterMissiles.TryGetByMissileId(projectile.NativeMissileId, out _);
            if (projectile.SourceEntityId == 0
                || !nativeMissileIdentityValid
                || (projectile.SpellId == 0
                    && projectile.Behavior != AuthoritativeProjectileBehavior.WeaponArrow
                    && !isMonsterArrow)
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
                        || projectile.VelocityX != 0
                        || projectile.VelocityY != 0
                        || projectile.OriginalDirection != 0
                        || projectile.PathIndex != 0
                        || projectile.PathTicks is < 0 or > 31
                        || projectile.Speed == 0 && projectile.PathTicks != 0
                        || projectile.ImpactLifetime != 1
                        || projectile.ResistantDamageDivisor != 1
                        || projectile.EffectiveSpellLevel != 0
                        || projectile.DamageMinimum != 0
                        || projectile.DamageMaximum != 0
                        || projectile.LastCollisionTargetEntityId != 0
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
                || !WorldState.ContainsPosition(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY)
                || !WorldState.ContainsPosition(
                    projectile.LevelId,
                    projectile.TargetX,
                    projectile.TargetY)
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
                        || projectile.ResistantDamageDivisor <= 0))) {
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
                allowZeroSpellId: projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
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

        DiabloWorldState.ValidateOccupancy(
            source.LevelId,
            source.PositionX,
            source.PositionY,
            (source.CurrentAction?.Kind == DiabloPlayerActionKind.Move
                ? new[] {
                    (
                        source.LevelId,
                        checked(source.PositionX + source.CurrentAction.DirectionX),
                        checked(source.PositionY + source.CurrentAction.DirectionY)),
                }
                : [])
                .Concat(restoredMonsters
                .Where(monster => monster.HitPoints > 0)
                .SelectMany(monster => monster.Moving
                    ? new[] {
                        (monster.LevelId, monster.PositionX, monster.PositionY),
                        (monster.LevelId, monster.FuturePositionX, monster.FuturePositionY),
                    }
                    : [(monster.LevelId, monster.PositionX, monster.PositionY)])),
            restoredWorldItems.Select(item => (item.LevelId, item.PositionX, item.PositionY)),
            restoredObjects
                .Where(@object => !@object.Activated
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
        destination.MemorizedSpells.Clear();
        destination.MemorizedSpells.AddRange(source.MemorizedSpells);
        destination.PositionX = source.PositionX;
        destination.PositionY = source.PositionY;
        destination.LevelId = source.LevelId;
        destination.StatusEffects = source.StatusEffects.ToList();
        destination.ActiveStoreId = source.ActiveStoreId;
        destination.Inventory.Clear();
        destination.Inventory.AddRange(source.Inventory);
        destination.Equipment.Clear();
        destination.Equipment.AddRange(source.Equipment);
        if (source.BaseLifeMaximum > 0)
            destination.BaseLifeMaximum = source.BaseLifeMaximum;
        if (source.BaseManaMaximum > 0)
            destination.BaseManaMaximum = source.BaseManaMaximum;
        destination.AdoptEquipmentAttributeSnapshot();
        destination.RecalculateEquipmentResources();
        destination.Life = source.Life;
        destination.Mana = source.Mana;
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
                target.HideWhenActivated = @object.HideWhenActivated;
                target.AnimationFrame = @object.AnimationFrame;
                target.ActivatedAnimationFrame = @object.ActivatedAnimationFrame;
                target.DialogueMessageId = @object.DialogueMessageId;
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
                    @object.DialogueMessageId));
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
        AuthoritativePlayerState destination)
    {
        var baseLifeMaximum = source.BaseLifeMaximum == 0
            ? destination.BaseLifeMaximum
            : source.BaseLifeMaximum;
        var baseManaMaximum = source.BaseManaMaximum == 0
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
            || source.Mana < 0
            || source.Mana > expectedResources.ManaQ6
            || source.CharacterLevel is < 1 or > NativeCompatibilityLimits.MaximumNativeCharacterLevel
            || (!string.IsNullOrEmpty(source.CharacterName)
                && !DiabloPlayerName.IsValid(source.CharacterName))
            || !hasValidClass
            || !WorldState.IsWalkable(source.LevelId, source.PositionX, source.PositionY)
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
            || !IsValidPlayerAction(source)
            || source.LifeMaximum != expectedResources.LifeQ6
            || source.ManaMaximum != expectedResources.ManaQ6
            || (source.StashOpen && source.ActiveStoreId is not null)
            || !HasValidAttributeState(source.Attributes)) {
            throw new InvalidDataException(
                "The authoritative save contains out-of-range player state.");
        }

        if (source.MemorizedSpells.Any(spell =>
                spell is null
                || spell.SpellId is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                || spell.Level is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
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

    private bool IsValidPlayerAction(StorePlayerSnapshot source)
    {
        var action = source.CurrentAction;
        if (action is null)
            return true;
        if (!Enum.IsDefined(action.Kind)
            || action.EffectTick < action.StartedAtTick
            || action.ReadyTick < action.EffectTick
            || action.ReadyTick > action.EndTick
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
                && !action.TargetCell
                && action.CastIntent is null
                && WorldState.IsWalkable(
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
                    : action.TargetEntityId != 0)
                && action.CastIntent is null,
            DiabloPlayerActionKind.Cast =>
                source.CombatMode == DiabloPlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && !action.TargetCell
                && action.CastIntent is not null
                && action.CastIntent.SpellId != 0
                && action.TargetEntityId == action.CastIntent.TargetEntityId
                && IsValidCastTarget(source.LevelId, action.CastIntent)
                && IsValidCastSource(action.CastIntent)
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

    private static bool IsValidCastSource(DiabloCastIntent cast)
    {
        var requiresItem = cast.Source is DiabloCastSourceKind.ChargedItem
            or DiabloCastSourceKind.ConsumableItem;
        if (requiresItem != cast.SourceItem.HasValue)
            return false;
        if (!cast.SourceItem.HasValue)
            return true;
        var item = cast.SourceItem.Value;
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

    private bool HasKnownStableItemIdentity(AuthoritativeItemState state)
    {
        return Items is null
            || state.StableItemId == 0
            || Items.TryGet(state.StableItemId, out _);
    }
}
