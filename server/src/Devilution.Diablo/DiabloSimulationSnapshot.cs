using Devilution.Domain.Protocol;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Shared;
using System.Text.Json.Serialization;

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

    /** An explicit native RNG policy; zero seeds are valid when this is true. */
    public bool NativeInitializationPresent { get; init; }

    /** Native-owned normal melee animation; absent in legacy snapshots. */
    public AuthoritativeNativeMeleeCursor? NativeMeleeCursor { get; init; }
    public AuthoritativeNativeWalkCursor? NativeWalkCursor { get; init; }

    /** L5-L8 native SneakAi state; absent before Catacombs cohorts. */
    public AuthoritativeNativeSneakState? NativeSneak { get; init; }

    /** L5-L8 native ranged-AI var1/var2 and goal; absent before Catacombs ranged cohorts. */
    public AuthoritativeNativeAiVars? NativeAiVars { get; init; }

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

    /** Native HorkDemon goalVar1; -1 means the Normal goal is active. */
    public int HorkDemonMoveTicks { get; init; } = -1;

    /** Native HorkDemon goalVar2 turn state (0 or 1). */
    public int HorkDemonMoveTurn { get; init; }

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

    /** Data-authored presentation family; native_object_id remains compatibility metadata. */
    public AuthoritativeObjectPresentationKind PresentationKind { get; init; }

    /** Compatibility presentation may retain an activated object on the map. */
    public bool HideWhenActivated { get; init; } = true;

    /** Native-compatible animation frame before activation. */
    public int AnimationFrame { get; init; }

    /** Native-compatible animation frame selected when the object activates. */
    public int ActivatedAnimationFrame { get; init; }

    /** Mutable object RNG state, absent in pre-shrine saves. */
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? NativeRandomSeed { get; init; }

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

    /** Presence selects collision-time native bow resolution. */
    public AuthoritativeNativeWeaponArrowState? NativeWeaponArrow { get; init; }

    /** Present only for an exact owned L1 wall-trap Arrow. */
    public AuthoritativeNativeTrapArrowState? NativeTrapArrow { get; init; }

    /** Present only for a native L5-L8 monster Arrow. */
    public AuthoritativeNativeMonsterMissileState? NativeMonsterMissile { get; init; }

    public int PerilSelfDamageQ6 { get; init; }

    public bool DoppelgangerProc { get; init; }

    public uint ActivationDelayTicks { get; init; }

    /** Dynamic monster-arrow minimum damage rolled at impact; zero means fixed payload. */
    public int DamageMinimum { get; init; }

    /** Dynamic monster-arrow maximum damage rolled at impact; zero means fixed payload. */
    public int DamageMaximum { get; init; }
}

/** Informational availability of the native services for one active store. */
public sealed record DiabloStoreServices(
    bool Repair,
    bool Recharge,
    bool Identify,
    bool RefillMana);

/** One player-owned target's currently offered paid service quotes. */
public sealed record DiabloStoreItemServiceQuotes(
    DiabloPlayerItemReference Item,
    DiabloStoreServiceQuote? Repair,
    DiabloStoreServiceQuote? Recharge,
    DiabloStoreServiceQuote? Identify);

/** Live-only paid service quotes for one active store. */
public sealed record DiabloStoreQuotes(
    IReadOnlyList<DiabloStoreItemServiceQuotes> Items,
    DiabloStoreServiceQuote? RefillMana);

/** Remaining stock and live-only service hints for the store visible to the player. */
public sealed record DiabloStoreState(uint StoreId, IReadOnlyList<StoreItem> Items)
{
    /** Recomputed presentation hint; omitted from persistence snapshots. */
    public DiabloStoreServices? Services { get; init; }

    /** Recomputed presentation hint; omitted from persistence snapshots. */
    public DiabloStoreQuotes? Quotes { get; init; }
}

/** Persisted origin for one runtime-generated set-piece object group. */
public sealed record DiabloSetLevelObjectOriginState(
    int SetLevelId,
    uint LevelId,
    int PositionX,
    int PositionY);

/** Persisted origin for one runtime-generated ordinary-level quest piece. */
public sealed record DiabloProceduralQuestPieceOriginState(
    uint OriginId,
    uint LevelId,
    int PositionX,
    int PositionY);

/** Content-bound original Town resident projection for native presentation clients. */
public sealed record DiabloNativeTownResidentState(
    int ResidentSlot,
    int TownerId,
    uint LevelId,
    int PositionX,
    int PositionY,
    int NativeDirection);

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

    /** Cohort-only actor metadata identity, retained even with an empty roster. */
    public string? NativeMonsterMetadataFingerprint { get; init; }

    /** Game-owned generated geometry identity carried only by persistence adapters. */
    public DiabloNativeGeneratedWorldIdentity? NativeGeneratedWorldIdentity { get; init; }

    /** Explicit depth profile identity, separate from legacy L1 persistence metadata. */
    public DiabloCathedralDepthWorldIdentity? CathedralDepthWorldIdentity { get; init; }

    /** Exact mutable-door projection owned by explicit save version four. */
    public DiabloCathedralDoorCheckpoint? CathedralDoors { get; init; }

    /** Transient server-policy capability; it is re-derived and never persisted. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeChestInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient native sarcophagus capability, never imported as gameplay state. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeSarcophagusInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient native barrel melee capability, never imported as gameplay state. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeBarrelMeleeBreakableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Immutable native identity projected from the existing population owner. */
    [JsonIgnore]
    public IReadOnlyList<DiabloNativeShrineProjection> NativeShrineTypes { get; internal init; } = [];

    /** Transient native shrine capability, never imported as gameplay state. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeShrineInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient item-object capability from the shared native population owner. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeThemeObjectInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient fountain/goat-shrine/cauldron capability derived from the real population owner. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeThemeEffectInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient native trap capability, never imported as gameplay state. */
    [JsonIgnore]
    public IReadOnlySet<uint> NativeTrapInteractableEntityIds { get; internal init; } = DiabloNativeChestInteractableEntitySet.Empty;

    /** Transient view of retained Fired owners; their lifecycle already lives in shared native trap state. */
    [JsonIgnore]
    public IReadOnlyList<DiabloNativeTrapState> NativeFiredTraps { get; internal init; } = [];

    /** Bounded presentation fields; clients never replay native door operations. */
    public IReadOnlyList<DiabloCathedralDoorObjectState> CathedralDoorObjects { get; init; } = [];

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

    /** Fingerprint of the active semantic Town catalog; empty for legacy profiles. */
    public string NativeTownCatalogFingerprint { get; init; } = "";

    /** Stable original Town roster projection; empty unless the explicit Town owner is active. */
    public IReadOnlyList<DiabloNativeTownResidentState> NativeTownResidents { get; init; } = [];

    /** Active content world-cell mutations persisted with the session. */
    public IReadOnlyList<uint> ActiveWorldMutationIds { get; init; } = [];

    /** Runtime-generated set-piece origins persisted with the session. */
    public IReadOnlyList<DiabloSetLevelObjectOriginState> SetLevelObjectOrigins { get; init; } = [];

    /** Runtime-generated ordinary-level quest-piece origins persisted with the session. */
    public IReadOnlyList<DiabloProceduralQuestPieceOriginState> ProceduralQuestPieceOrigins { get; init; } = [];

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
            NativeLifeBase = player.NativeLifeBase, NativeManaBase = player.NativeManaBase,
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
            FullyExploredLevelIds = player.FullyExploredLevelIds.Count == 0 ? null : player.FullyExploredLevelIds.Order().ToArray(),
            StatusEffects = player.StatusEffects.ToArray(),
            FriendlyMode = player.FriendlyMode,
            FacingDirection = player.FacingDirection,
            CombatMode = player.CombatMode,
            CombatModeEndTick = player.CombatModeEndTick,
            CurrentAction = ClonePlayerAction(player.CurrentAction),
            NativeWalkPath = CloneNativeWalkPath(player.NativeWalkPath),
            NativeQueuedRangedAttack = CloneNativeQueuedRangedAttack(player.NativeQueuedRangedAttack),
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
        => CaptureSnapshotCore(player, tick, includeDoorPersistence: true);

    /** Live presentation omits full door geometry copies; exports use CaptureSnapshot. */
    public DiabloSimulationSnapshot CapturePresentationSnapshot(
        AuthoritativePlayerState player,
        ulong tick)
        => CaptureSnapshotCore(player, tick, includeDoorPersistence: false);

    private DiabloSimulationSnapshot CaptureSnapshotCore(
        AuthoritativePlayerState player,
        ulong tick,
        bool includeDoorPersistence)
    {
        ArgumentNullException.ThrowIfNull(player);
        var activeStore = player.ActiveStoreId is uint storeId
            ? new DiabloStoreState(
                storeId,
                storeCatalog.GetItems(storeId)
                    .Select(item => item with {
                        State = ResolveItemState(item.State, item.ItemSeed),
                    })
                    .ToArray()) {
                    Services = includeDoorPersistence
                        ? null
                        : storeCommands.CaptureStoreServices(player),
                    Quotes = includeDoorPersistence
                        ? null
                        : storeCommands.CaptureStoreQuotes(player),
                }
            : null;
        foreach (var target in WorldState.CombatTargets.Values)
            target.EnsureMovementTiming(tick);
        var nativeTownCatalog = WorldState.NativeTownCatalog;
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
                    NativeInitializationPresent = target.NativeInitializationPresent,
                    NativeMeleeCursor = target.NativeMeleeCursor,
                    NativeWalkCursor = target.NativeWalkCursor,
                    NativeSneak = target.NativeSneak,
                    NativeAiVars = target.NativeAiVars,
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
                    HorkDemonMoveTicks = target.HorkDemonMoveTicks,
                    HorkDemonMoveTurn = target.HorkDemonMoveTurn,
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
                    PresentationKind = @object.PresentationKind,
                    HideWhenActivated = @object.HideWhenActivated,
                    AnimationFrame = @object.AnimationFrame,
                    ActivatedAnimationFrame = @object.ActivatedAnimationFrame,
                    DialogueMessageId = @object.DialogueMessageId,
                    NativeRandomSeed = @object.NativeRandomSeed,
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
                    NativeWeaponArrow = projectile.NativeWeaponArrow,
                    NativeTrapArrow = projectile.NativeTrapArrow,
                    NativeMonsterMissile = projectile.NativeMonsterMissile,
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
            NativeMonsterMetadataFingerprint = this.NativeMonsterMetadataFingerprint,
            NativeGeneratedWorldIdentity = this.NativeGeneratedWorldIdentity,
            CathedralDepthWorldIdentity = this.CathedralDepthWorldIdentity,
            CathedralDoors = includeDoorPersistence ? WorldState.CaptureCathedralDoors() : null,
            CathedralDoorObjects = WorldState.CaptureCathedralDoorObjects(),
            NativeChestInteractableEntityIds = WorldState.CaptureNativeChestInteractableEntityIds(),
            NativeShrineInteractableEntityIds = WorldState.CaptureNativeShrineInteractableEntityIds(),
            NativeShrineTypes = WorldState.CaptureNativeShrineTypes(),
            NativeThemeObjectInteractableEntityIds = WorldState.CaptureNativeThemeObjectInteractableEntityIds(),
            NativeThemeEffectInteractableEntityIds = WorldState.CaptureNativeThemeEffectInteractableEntityIds(),
            NativeSarcophagusInteractableEntityIds = WorldState.CaptureNativeSarcophagusInteractableEntityIds(),
            NativeBarrelMeleeBreakableEntityIds = WorldState.CaptureNativeBarrelMeleeBreakableEntityIds(),
            NativeTrapInteractableEntityIds = WorldState.CaptureNativeTrapInteractableEntityIds(),
            NativeFiredTraps = WorldState.CaptureNativeFiredTraps(),
            QuestTownerInteractions = QuestTownerInteractions?.Definitions ?? [],
            TownerGreetings = TownerGreetings?.Definitions.ToArray() ?? [],
            NativeTownCatalogFingerprint = nativeTownCatalog?.Fingerprint ?? "",
            NativeTownResidents = nativeTownCatalog is null
                ? Array.Empty<DiabloNativeTownResidentState>()
                : Array.AsReadOnly(nativeTownCatalog.Roster.Residents
                    .Select((resident, slot) => new DiabloNativeTownResidentState(
                        slot,
                        resident.TownerId,
                        nativeTownCatalog.LevelId,
                        resident.PositionX,
                        resident.PositionY,
                        NativeTownDirectionIndex(resident.Direction)))
                    .ToArray()),
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
            ProceduralQuestPieceOrigins = proceduralQuestPieceOrigins
                .OrderBy(pair => pair.Key)
                .Select(pair => new DiabloProceduralQuestPieceOriginState(
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

}
