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
    int MagicResistance)
{
    public int MonsterLevel { get; init; }

    public bool Moving { get; init; }

    public int FuturePositionX { get; init; } = PositionX;

    public int FuturePositionY { get; init; } = PositionY;

    public bool Petrified { get; init; }

    public bool StoneCurseImmune { get; init; }

    public bool IsUndead { get; init; }

    /** Native monster faction marker used by Apocalypse's minion exclusion. */
    public bool IsPlayerMinion { get; init; }

    /** Authoritative owner entity for a summoned player minion, when present. */
    public uint OwnerPlayerId { get; init; }

    public bool IsUnique { get; init; }

    public bool IsDiablo { get; init; }

    public bool BerserkImmune { get; init; }

    public int MinDamage { get; init; }

    public int MaxDamage { get; init; }

    public int MinDamageSpecial { get; init; }

    public int MaxDamageSpecial { get; init; }

    public bool IsBerserked { get; init; }

    /** Native death-animation duration imported from content. */
    public int DeathAnimationTicks { get; init; }

    /** Remaining terminal death-animation ticks, when the monster is dead. */
    public int DeathTicksRemaining { get; init; }

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
    public bool MissilePassable { get; init; }

    public bool Breakable { get; init; }

    public bool Destroyed { get; init; }

    public bool TrapArmed { get; init; }
}

/** Immutable protocol-neutral quest projection. */
public sealed record DiabloQuestState(
    uint QuestId,
    uint LevelId,
    uint Progress,
    uint RequiredProgress,
    bool Completed);

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
            CharacterLevel = player.CharacterLevel,
            LevelId = player.LevelId,
            EntityId = player.EntityId,
            CharacterClassId = player.CharacterClassId,
            CharacterName = player.CharacterName,
            ManaShieldActive = player.ManaShieldActive,
            ReflectCharges = player.ReflectCharges,
            AbilitySpellId = player.AbilitySpellId,
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
                    target.MagicResistance) {
                    MonsterLevel = target.MonsterLevel,
                    Moving = target.IsMoving,
                    FuturePositionX = target.FuturePositionX,
                    FuturePositionY = target.FuturePositionY,
                    Petrified = target.IsPetrified,
                    StoneCurseImmune = target.StoneCurseImmune,
                    IsUndead = target.IsUndead,
                    IsPlayerMinion = target.IsPlayerMinion,
                    OwnerPlayerId = target.OwnerPlayerId,
                    IsUnique = target.IsUnique,
                    IsDiablo = target.IsDiablo,
                    BerserkImmune = target.BerserkImmune,
                    MinDamage = target.MinDamage,
                    MaxDamage = target.MaxDamage,
                    MinDamageSpecial = target.MinDamageSpecial,
                    MaxDamageSpecial = target.MaxDamageSpecial,
                    IsBerserked = target.IsBerserked,
                    DeathAnimationTicks = target.DeathAnimationTicks,
                    DeathTicksRemaining = target.DeathTicksRemaining,
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
                    MissilePassable = @object.MissilePassable,
                    Breakable = @object.Breakable,
                    Destroyed = @object.Destroyed,
                    TrapArmed = @object.TrapArmed,
                })
                .ToArray(),
            WorldState.Quests.Values
                .OrderBy(quest => quest.QuestId)
                .Select(quest => new DiabloQuestState(
                    quest.QuestId,
                    quest.LevelId,
                    quest.Progress,
                    quest.RequiredProgress,
                    quest.Completed))
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
        };
    }

    /** Restores validated module state without knowing its serialized representation. */
    public void RestoreSnapshot(
        AuthoritativePlayerState destination,
        DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(snapshot);
        var source = snapshot.Player
            ?? throw new InvalidDataException("The authoritative save does not contain player state.");
        if (snapshot.Monsters is null
            || snapshot.WorldItems is null
            || snapshot.Objects is null
            || snapshot.Quests is null
            || snapshot.Projectiles is null
            || snapshot.Portals is null) {
            throw new InvalidDataException(
                "The authoritative save contains incomplete world state.");
        }
        ValidateSavedPlayer(source, destination);

        var monsterEntityIds = snapshot.Monsters.Select(monster => monster.EntityId).ToArray();
        var worldItemEntityIds = snapshot.WorldItems.Select(item => item.EntityId).ToArray();
        var objectEntityIds = snapshot.Objects.Select(@object => @object.EntityId).ToArray();
        var projectileEntityIds = snapshot.Projectiles.Select(projectile => projectile.EntityId).ToArray();
        var allWorldEntityIds = monsterEntityIds
            .Concat(worldItemEntityIds)
            .Concat(objectEntityIds)
            .Concat(projectileEntityIds)
            .ToArray();
        if (monsterEntityIds.Any(entityId => entityId == 0)
            || worldItemEntityIds.Any(entityId => entityId == 0)
            || objectEntityIds.Any(entityId => entityId == 0)
            || projectileEntityIds.Any(entityId => entityId == 0)
            || monsterEntityIds.Distinct().Count() != monsterEntityIds.Length
            || worldItemEntityIds.Distinct().Count() != worldItemEntityIds.Length
            || objectEntityIds.Distinct().Count() != objectEntityIds.Length
            || projectileEntityIds.Distinct().Count() != projectileEntityIds.Length
            || allWorldEntityIds.Distinct().Count() != allWorldEntityIds.Length
            || allWorldEntityIds.Contains(source.EntityId)
            || snapshot.Monsters.Count > 4096
            || snapshot.WorldItems.Count > 4096
            || snapshot.Objects.Count > 4096
            || snapshot.Projectiles.Count > 4096) {
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
                || (!monster.IsPlayerMinion && monster.OwnerPlayerId != 0)) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid monster.");
            }
            return monster;
        }).ToArray();
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
            if (projectile.SourceEntityId == 0
                || projectile.SpellId == 0
                || projectile.RemainingTicks == 0
                || projectile.Damage < 0
                || projectile.AreaRadius < 0
                || projectile.EffectiveSpellLevel < 0
                || !Enum.IsDefined(projectile.DamageType)
                || !Enum.IsDefined(projectile.Behavior)
                || !Enum.IsDefined(projectile.Phase)
                || projectile.Behavior is AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Resurrection
                || IsInvalidStoneCurseProjectile(projectile, restoredMonsters)
                || !WorldState.ContainsPosition(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY)
                || !WorldState.ContainsPosition(
                    projectile.LevelId,
                    projectile.TargetX,
                    projectile.TargetY)
                || (projectile.Behavior == AuthoritativeProjectileBehavior.ChargedBolt
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
                projectile.RemainingTicks) {
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
                || quest.Progress > quest.RequiredProgress) {
                throw new InvalidDataException(
                    "The authoritative save contains an invalid quest state.");
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
        WorldState.WorldItems.Clear();
        foreach (var item in restoredWorldItems)
            WorldState.WorldItems.Add(item.EntityId, item);
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
        if (restoredStore is not null)
            storeCatalog.ReplaceStore(restoredStore.StoreId, restoredStore.Items);
    }

    private bool IsMalformedGoldWorldItem(DiabloWorldItemState item)
    {
        var state = item.State;
        if (state.ItemType != CombatRules.DeathGoldItemType
            || state.ItemIndex != CombatRules.DeathGoldItemIndex) {
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

    private static void RestorePlayer(
        StorePlayerSnapshot source,
        AuthoritativePlayerState destination)
    {
        destination.Gold = source.Gold;
        destination.Experience = source.Experience;
        destination.Life = source.Life;
        destination.Mana = source.Mana;
        destination.Attributes = source.Attributes;
        destination.CharacterLevel = source.CharacterLevel;
        destination.CharacterClassId = source.CharacterClassId;
        if (!string.IsNullOrEmpty(source.CharacterName))
            destination.CharacterName = source.CharacterName;
        destination.AbilitySpellId = source.AbilitySpellId;
        destination.ManaShieldActive = source.ManaShieldActive;
        destination.ReflectCharges = source.ReflectCharges;
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
                target.PositionX = monster.PositionX;
                target.PositionY = monster.PositionY;
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
                target.MonsterLevel = monster.MonsterLevel;
                target.StoneCurseImmune = monster.StoneCurseImmune;
                target.IsUndead = monster.IsUndead;
                target.IsPlayerMinion = monster.IsPlayerMinion;
                target.OwnerPlayerId = monster.OwnerPlayerId;
                target.IsUnique = monster.IsUnique;
                target.IsDiablo = monster.IsDiablo;
                target.BerserkImmune = monster.BerserkImmune;
                target.MinDamage = monster.MinDamage;
                target.MaxDamage = monster.MaxDamage;
                target.MinDamageSpecial = monster.MinDamageSpecial;
                target.MaxDamageSpecial = monster.MaxDamageSpecial;
                target.IsBerserked = monster.IsBerserked;
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
                    monsterLevel: monster.MonsterLevel,
                    moving: monster.Moving,
                    futurePositionX: monster.FuturePositionX,
                    futurePositionY: monster.FuturePositionY,
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
                    deathAnimationTicks: monster.DeathAnimationTicks);
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
                target.MissilePassable = @object.MissilePassable;
                target.Breakable = @object.Breakable;
                target.Destroyed = @object.Destroyed;
                target.TrapArmed = @object.TrapArmed;
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
                    @object.MissilePassable,
                    @object.Breakable,
                    @object.Destroyed,
                    @object.TrapArmed));
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
            } else {
                WorldState.Quests.Add(quest.QuestId, new AuthoritativeQuestState(
                    quest.QuestId,
                    quest.LevelId,
                    quest.RequiredProgress,
                    quest.Progress,
                    quest.Completed));
            }
        }
    }

    private void ValidateSavedPlayer(
        StorePlayerSnapshot source,
        AuthoritativePlayerState destination)
    {
        if (source.Life < 0
            || source.Inventory is null
            || source.Equipment is null
            || source.Belt is null
            || source.InventoryGrid is null
            || source.MemorizedSpells is null
            || source.StatusEffects is null
            || source.Life > destination.LifeMaximum
            || source.Mana < 0
            || source.Mana > destination.ManaMaximum
            || source.CharacterLevel is < 1 or > 50
            || (!string.IsNullOrEmpty(source.CharacterName)
                && !DiabloPlayerName.IsValid(source.CharacterName))
            || (PlayerClasses is null
                ? source.CharacterClassId != 0
                : !PlayerClasses.TryGet(source.CharacterClassId, out _))
            || !WorldState.IsWalkable(source.LevelId, source.PositionX, source.PositionY)
            || source.Inventory.Count > 40
            || source.Equipment.Count > 7
            || source.Belt.Count > 8
            || source.InventoryGrid.Count > 400
            || source.AbilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
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
            || source.LifeMaximum != destination.LifeMaximum
            || source.ManaMaximum != destination.ManaMaximum
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
                || (DiabloGoldInventory.HasGoldIdentity(heldItem.State, CombatRules)
                    && !DiabloGoldInventory.IsValidStack(heldItem, CombatRules)))) {
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
        var hasDynamicPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.NativeCursorGraphic.HasValue;
        if (!hasDynamicPresentation)
            return itemSeed != 0;
        return !string.IsNullOrEmpty(state.BaseName)
            && !string.IsNullOrEmpty(state.IdentifiedName)
            && state.NativeCursorGraphic is >= 0 and <= byte.MaxValue
            && !state.BaseName.Contains('\0')
            && !state.IdentifiedName.Contains('\0')
            && System.Text.Encoding.UTF8.GetByteCount(state.BaseName) < 64
            && System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) < 64;
    }
}
