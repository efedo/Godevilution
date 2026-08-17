using Devilution.Domain.Inventory;

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

    /** Native monster direction used by movement, attacks, and Hork Spawn. */
    public int Direction { get; init; }

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

    public int HitPointsQ6 { get; init; } = DiabloFixedPointQ6.FromWhole(HitPoints);

    public int MaxHitPointsQ6 { get; init; } = DiabloFixedPointQ6.FromWhole(MaxHitPoints);
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
    string CompletionUniqueIdentity = "");

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
}

public sealed partial class DiabloSimulation
{
    public StorePlayerSnapshot CapturePlayerState(AuthoritativePlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new StorePlayerSnapshot(
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
        };
    }

    public DiabloSimulationSnapshot CaptureSnapshot(
        AuthoritativePlayerState player,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        var activeStore = player.ActiveStoreId is uint storeId
            ? new DiabloStoreState(storeId, storeCatalog.GetItems(storeId).ToArray())
            : null;
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
                    MonsterLevel = target.MonsterLevel,
                    Moving = target.IsMoving,
                    FuturePositionX = target.FuturePositionX,
                    FuturePositionY = target.FuturePositionY,
                    Direction = target.Direction,
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
                    item.State))
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
                    quest.CompletionUniqueIdentity))
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
        };
    }

    /** Restores validated module state without knowing its serialized representation. */
    public void RestoreSnapshot(
        AuthoritativePlayerState destination,
        DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(snapshot);
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
            || snapshot.Corpses is null) {
            throw new InvalidDataException(
                "The authoritative save contains incomplete world state.");
        }
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
        if (savedPortals.Length > 4096
            || savedPortals.Select(portal => portal.PortalId).Distinct().Count() != savedPortals.Length) {
            throw new InvalidDataException(
                "The authoritative save contains invalid portal identity.");
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
                || monster.HitPoints != DiabloFixedPointQ6.ToWholeCeiling(monster.HitPointsQ6)
                || monster.MaxHitPoints != DiabloFixedPointQ6.ToWholeCeiling(monster.MaxHitPointsQ6)
                || monster.AttackDamage < 0
                || monster.MinDamage < 0
                || monster.MaxDamage < 0
                || monster.MinDamageSpecial < 0
                || monster.MaxDamageSpecial < 0
                || monster.MaxDamage < monster.MinDamage
                || monster.MaxDamageSpecial < monster.MinDamageSpecial
                || monster.AggroRange < 0
                || monster.MonsterLevel < 0
                || monster.DeathAnimationTicks is < 0 or > 1024
                || monster.ExperienceReward > 1_000_000
                || monster.ToHit is < 0 or > 255
                || monster.ToHitSpecial is < 0 or > 255
                || monster.Intelligence is < 0 or > 255
                || monster.UniqueIdentity.Length > 128
                || (!string.IsNullOrWhiteSpace(monster.UniqueIdentity) && !monster.IsUnique)
                || (!string.IsNullOrEmpty(monster.NativeAi)
                    && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(monster.NativeAi))
                || monster.FallenFearTicks is < 0 or > 1024
                || monster.FallenFearDirection is < 0 or >= 8
                || monster.Direction is < 0 or >= 8
                || monster.SneakRetreatTicks is < 0 or > 64
                || monster.ScavengerHealingTicks is < 0 or > 64
                || monster.BatRetreatTicks is < 0 or > 64
                || monster.BatRetreatDirection is < 0 or >= 8
                || monster.BatMovementIdleTicks is < -1 or > 1024
                || monster.BatMovementMode is < -1 or > 3
                || monster.RangedAvoidanceMoveTicks is < -1 or > 255
                || monster.RangedAvoidanceTurn is < 0 or > 1
                || monster.RangedAvoidanceMovementIdleTicks is < -1 or > 1024
                || monster.RangedAvoidanceMovementMode is < -1 or > 3
                || monster.RhinoMovementTicks is < -1 or > 255
                || monster.RhinoMovementTurn is < 0 or > 1
                || monster.AvoidanceMovementTicks is < -1 or > 255
                || monster.AvoidanceMovementTurn is < 0 or > 1
                || monster.AvoidanceMovementIdleTicks is < -1 or > 1024
                || monster.AvoidanceMovementMode is < -1 or > 3
                || monster.SkeletonMeleeDelayTicks is < 0 or > 1024
                || monster.SkeletonRangedMovementIdleTicks is < -1 or > 1024
                || monster.SkeletonRangedMovementMode is < -1 or > 3
                || monster.FatMovementIdleTicks is < -1 or > 1024
                || monster.FatMovementMode is < -1 or > 3
                || monster.RhinoMovementIdleTicks is < -1 or > 1024
                || monster.RhinoMovementMode is < -1 or > 3
                || monster.RhinoDelayTicks is < 0 or > 1024
                || monster.SkeletonKingDelayTicks is < 0 or > 1024
                || monster.HorkDemonDelayTicks is < 0 or > 1024
                || monster.RangedAvoidanceDelayTicks is < 0 or > 1024
                || monster.SnakeDelayTicks is < 0 or > 1024
                || monster.CounselorDelayTicks is < 0 or > 1024
                || monster.AvoidanceDelayTicks is < 0 or > 1024
                || monster.RangedDelayTicks is < 0 or > 1024
                || monster.MegaDelayTicks is < 0 or > 1024
                || monster.CounselorRetreatTicks is < 0 or > 64
                || monster.DeathTicksRemaining is < 0
                || monster.DeathTicksRemaining > monster.DeathAnimationTicks
                || (monster.HitPointsQ6 > 0 && monster.DeathTicksRemaining != 0)
                || (monster.Moving
                    && (!WorldState.IsWalkable(
                            monster.LevelId,
                            monster.FuturePositionX,
                            monster.FuturePositionY)
                        || Math.Max(
                            Math.Abs(monster.FuturePositionX - monster.PositionX),
                            Math.Abs(monster.FuturePositionY - monster.PositionY)) != 1))
                || (!monster.Moving
                    && (monster.FuturePositionX != monster.PositionX
                        || monster.FuturePositionY != monster.PositionY))
                || monster.FireResistance is < -100 or > 100
                || monster.LightningResistance is < -100 or > 100
                || monster.MagicResistance is < -100 or > 100
                || monster.AcidResistance is < -100 or > 100
                || (!monster.IsPlayerMinion && monster.OwnerPlayerId != 0)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid monster.");
            }
            return monster;
        }).ToArray();
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
            if (projectile.SourceEntityId == 0
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
                        || projectile.Damage != DiabloFixedPointQ6.ToWholeTruncated(projectile.DamageQ6)
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
                || quest.MessageId is < 0 or > short.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid quest state.");
            }
            if (quest.CompletionUniqueIdentity.Length > 128) {
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
                .Where(@object => !@object.Activated && !@object.Destroyed)
                .Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));

        RestorePlayer(source, destination);
        RestoreMonsters(restoredMonsters);
        ReconcileProceduralSpawnState();
        WorldState.WorldItems.Clear();
        foreach (var item in restoredWorldItems)
            WorldState.WorldItems.Add(item.EntityId, item);
        WorldState.Corpses.Clear();
        foreach (var corpse in restoredCorpses)
            WorldState.Corpses.Add(corpse.EntityId, corpse);
        RestoreObjects(restoredObjects);
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
        WorldState.VictoryTriggered = snapshot.VictoryTriggered;
        if (restoredStore is not null)
            storeCatalog.ReplaceStore(restoredStore.StoreId, restoredStore.Items);
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
            || store.Items.Any(item => item is null || item.State is null)
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
        destination.AdoptEquipmentAttributeSnapshot();
        destination.RecalculateEquipmentResources();
        destination.Life = source.Life;
        destination.Mana = source.Mana;
        destination.Belt.Clear();
        destination.Belt.AddRange(source.Belt);
        destination.InventoryGrid.Clear();
        destination.InventoryGrid.AddRange(source.InventoryGrid);
        destination.HeldItem = source.HeldItem;
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
                target.RestoreSneakState(
                    monster.SneakHidden,
                    monster.SneakRetreatTicks);
                target.RestoreScavengerHealing(
                    monster.ScavengerHealingTicks,
                    monster.ScavengerCorpseEntityId);
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
                    @object.IsShrine));
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
                    quest.CompletionUniqueIdentity));
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
        if (source.Life < 0
            || source.BaseLifeMaximum < 0
            || source.BaseManaMaximum < 0
            || source.Inventory is null
            || source.Equipment is null
            || source.Belt is null
            || source.InventoryGrid is null
            || source.MemorizedSpells is null
            || source.StatusEffects is null
            || source.Life > expectedResources.LifeQ6
            || source.Mana < 0
            || source.Mana > expectedResources.ManaQ6
            || source.CharacterLevel is < 1 or > 50
            || (!string.IsNullOrEmpty(source.CharacterName)
                && !DiabloPlayerName.IsValid(source.CharacterName))
            || !hasValidClass
            || !WorldState.IsWalkable(source.LevelId, source.PositionX, source.PositionY)
            || source.Inventory.Count > 40
            || source.Equipment.Count > 7
            || source.Belt.Count > 8
            || source.InventoryGrid.Count > 400
            || source.AbilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
            || !Enum.IsDefined(source.AbilityKind)
            || source.ReflectCharges > ushort.MaxValue
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
            || baseLifeMaximum != destination.BaseLifeMaximum
            || baseManaMaximum != destination.BaseManaMaximum
            || source.LifeMaximum != expectedResources.LifeQ6
            || source.ManaMaximum != expectedResources.ManaQ6
            || source.Attributes is null
            || source.Attributes.Strength is null
            || source.Attributes.Magic is null
            || source.Attributes.Dexterity is null
            || source.Attributes.Vitality is null) {
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
                        or AuthoritativeStatusEffectBehavior.Search)
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
                && action.TargetEntityId != 0
                && action.CastIntent is null,
            DiabloPlayerActionKind.Cast =>
                source.CombatMode == DiabloPlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.CastIntent is not null
                && action.CastIntent.SpellId != 0
                && action.TargetEntityId == action.CastIntent.TargetEntityId
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
                && action.CastIntent is null,
            DiabloPlayerActionKind.LevelTransition =>
                source.CombatMode == DiabloPlayerCombatMode.NewLevel
                && source.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
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
                && action.TargetEntityId == 0
                && action.CastIntent is null,
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
        if (source.HeldItem is { } heldItem
            && (heldItem.State is null
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
}
