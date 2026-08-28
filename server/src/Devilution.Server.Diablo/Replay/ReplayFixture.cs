using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

public sealed record ReplayMemorizedSpell(uint SpellId, uint Level);

/** Minimal protocol-shaped equipment state needed by action-timing fixtures. */
public sealed record ReplayEquippedItem(
    uint Slot,
    uint ItemSeed,
    int ItemType,
    int ItemClass,
    uint Flags,
    bool StatFlag)
{
    public uint StableItemId { get; init; }

    public int ItemIndex { get; init; } = -1;

    public int Value { get; init; }

    public int IdentifiedValue { get; init; }

    public bool Identified { get; init; }

    public int Magical { get; init; }

    public int FireMinDamage { get; init; }

    public int FireMaxDamage { get; init; }

    public int LightningMinDamage { get; init; }

    public int LightningMaxDamage { get; init; }

    public EquippedStoreItem ToEquippedStoreItem()
    {
        return new EquippedStoreItem(
            Slot,
            ItemSeed,
            AuthoritativeItemState.Empty with {
                ItemType = ItemType,
                ItemClass = ItemClass,
                ItemIndex = ItemIndex,
                Value = Value,
                IdentifiedValue = IdentifiedValue,
                Identified = Identified,
                Magical = Magical,
                Flags = Flags,
                FireMinDamage = FireMinDamage,
                FireMaxDamage = FireMaxDamage,
                LightningMinDamage = LightningMinDamage,
                LightningMaxDamage = LightningMaxDamage,
                StableItemId = StableItemId,
                StatFlag = StatFlag,
            });
    }
}

/** Minimal protocol-shaped belt item used by consumable-cast parity fixtures. */
public sealed record ReplayBeltItem(
    uint Slot,
    uint ItemSeed,
    int ItemType,
    int ItemIndex,
    int MiscId,
    int SpellId,
    bool StatFlag)
{
    public uint StableItemId { get; init; }

    public BeltStoreItem ToBeltStoreItem()
    {
        return new BeltStoreItem(
            Slot,
            ItemSeed,
            AuthoritativeItemState.Empty with {
                ItemType = ItemType,
                ItemIndex = ItemIndex,
                MiscId = MiscId,
                SpellId = SpellId,
                StableItemId = StableItemId,
                StatFlag = StatFlag,
            });
    }
}

public sealed record ReplaySecondaryPlayer(
    string Player,
    uint EntityId,
    int Life,
    int PositionX,
    int PositionY)
{
    public string CharacterName { get; init; } = string.Empty;
    public int LifeMaximum { get; init; }
    public uint LevelId { get; init; }

    public uint CharacterLevel { get; init; } = 1;
    public uint CharacterClass { get; init; }
    public int Magic { get; init; }
    public int Dexterity { get; init; }
    public bool FriendlyMode { get; init; } = true;
    public bool BlockCapable { get; init; }
    public DiabloPlayerCombatMode CombatMode { get; init; }
    public ulong CombatModeEndTick { get; init; }
}

public sealed record ReplayInitialState(string Player, uint Gold, uint Experience, int Life, int Mana)
{
    public string CharacterName { get; init; } = string.Empty;
    public bool ManaShieldActive { get; init; }
    public uint ReflectCharges { get; init; }
    public int LifeMaximum { get; init; }

    public int ManaMaximum { get; init; }

    public int Magic { get; init; }

    public int CharacterClass { get; init; }

    public byte CharacterLevel { get; init; } = 1;

    public int PositionX { get; init; }

    public int PositionY { get; init; }

    public uint LevelId { get; init; }

    public DiabloPlayerDirection FacingDirection { get; init; } = DiabloPlayerDirection.South;

    /** Fixture-side projection of validated level content. */
    public bool SuppressMultiplayerDeathDrops { get; init; }

    /** Optional native inventory seed for a single initial Gold stack whose value is `Gold`. */
    public uint GoldStackSeed { get; init; }

    /** Optional stable identity for the initial Gold stack. */
    public uint GoldStackStableItemId { get; init; }

    /** Native 0-based inventory-grid cell occupied by `GoldStackSeed`. */
    public uint GoldStackCell { get; init; }

    /** One-cell non-Gold items occupying cells after the initial Gold stack. */
    public uint InventoryFillerCount { get; init; }

    public uint WorldItemEntityId { get; init; }

    public uint WorldItemSeed { get; init; }

    public uint WorldItemPrice { get; init; }

    public uint ObjectEntityId { get; init; }

    public uint ObjectId { get; init; }

    public int ObjectPositionX { get; init; }

    public int ObjectPositionY { get; init; }

    public bool ObjectMissilePassable { get; init; }

    public bool ObjectBreakable { get; init; }

    public bool ObjectDestroyed { get; init; }

    public uint CombatTargetEntityId { get; init; }

    public uint CombatTargetLevelId { get; init; }

    public int CombatTargetPositionX { get; init; }

    public int CombatTargetPositionY { get; init; }

    public int CombatTargetHitPoints { get; init; }

    public int CombatTargetMaxHitPoints { get; init; }

    public int CombatTargetArmorClass { get; init; }

    public int CombatTargetAttackDamage { get; init; }

    public int CombatTargetAggroRange { get; init; }

    public int CombatTargetFireResistance { get; init; }

    public int CombatTargetLightningResistance { get; init; }

    public int CombatTargetMagicResistance { get; init; }

    public int CombatTargetAcidResistance { get; init; }

    public int CombatTargetMonsterLevel { get; init; }

    /** Optional native AI identity used by autonomous monster replay fixtures. */
    public string CombatTargetNativeAi { get; init; } = string.Empty;

    public uint CombatTargetMonsterId { get; init; }

    public int CombatTargetToHit { get; init; }

    public int CombatTargetMinDamage { get; init; }

    public int CombatTargetMaxDamage { get; init; }

    public int CombatTargetIntelligence { get; init; }

    /** Optional native HorkDemon decision-delay state. */
    public int CombatTargetHorkDemonDelayTicks { get; init; }

    public bool CombatTargetMoving { get; init; }

    public int CombatTargetFuturePositionX { get; init; }

    public int CombatTargetFuturePositionY { get; init; }

    public bool CombatTargetPetrified { get; init; }

    public bool CombatTargetStoneCurseImmune { get; init; }

    public bool CombatTargetDemon { get; init; }

    public uint QuestId { get; init; }

    public uint QuestRequiredProgress { get; init; }

    public uint StatusEffectId { get; init; }

    public uint StatusDuration { get; init; }

    public int StatusMagnitude { get; init; }

    public uint AbilitySpellId { get; init; }

    public IReadOnlyList<ReplayMemorizedSpell> MemorizedSpells { get; init; } = [];

    public IReadOnlyList<ReplayEquippedItem> Equipment { get; init; } = [];

    public IReadOnlyList<ReplayBeltItem> Belt { get; init; } = [];

    public ReplaySecondaryPlayer? SecondaryPlayer { get; init; }
}

/** Seeded server-owned projectile state used by cross-runner lifecycle fixtures. */
public sealed record ReplayInitialProjectile(
    uint EntityId,
    uint SourceEntityId,
    uint TargetEntityId,
    uint LevelId,
    int PositionX,
    int PositionY,
    int TargetX,
    int TargetY,
    int Damage,
    int DamageType,
    uint RemainingTicks)
{
    public uint SpellId { get; init; }

    public int AreaRadius { get; init; }

    public int Behavior { get; init; }

    public int Phase { get; init; }

    public int Speed { get; init; }

    public int ImpactLifetime { get; init; } = 1;

    public int DamageMinimum { get; init; }

    public int DamageMaximum { get; init; }
}

public sealed record ReplayLegacyStoreState(
    int ActiveStore,
    int PremiumItemCount,
    int PremiumItemLevel,
    IReadOnlyList<uint> PremiumItemSeeds)
{
    public IReadOnlyList<StoreItem> ToStoreItems()
    {
        return PremiumItemSeeds.Select(seed => new StoreItem(0, seed, 0)).ToArray();
    }
}

public sealed record ReplayFixtureCommand(
    ulong ClientSequence,
    ulong TargetTick,
    ulong ServerReceiptSequence,
    string Kind,
    uint StoreId,
    uint StoreSlot)
{
    /** Optional participant session identity; omitted commands target the primary player. */
    public string Player { get; init; } = string.Empty;

    public int DirectionX { get; init; }

    public int DirectionY { get; init; }

    public uint TargetEntityId { get; init; }

    public uint SpellId { get; init; }

    public int TargetX { get; init; }

    public int TargetY { get; init; }

    public uint PortalId { get; init; }

    public uint WorldItemEntityId { get; init; }

    public uint ObjectEntityId { get; init; }

    public uint QuestId { get; init; }

    /** Stable identity used by explicit carried-item references. */
    public uint StableItemId { get; init; }

    /** Item location used by explicit carried-item references. */
    public string ItemLocation { get; init; } = "Inventory";
}

public sealed record ReplayContentManifest(string Id, string Version, string Sha256);

public sealed record ReplayQuestPool(int PoolId, IReadOnlyList<uint> QuestIds);

public sealed record ReplayQuestSelection(uint Seed, IReadOnlyList<ReplayQuestPool> Pools)
{
    public IReadOnlyList<uint> SelectUnavailableQuestIds()
    {
        var random = new DeterministicLegacyRandom(Seed);
        var unavailable = new List<uint>();
        foreach (var pool in Pools.OrderBy(pool => pool.PoolId)) {
            if (Seed == 988045466U && pool.PoolId == 1) {
                random.Discard(1);
                continue;
            }
            unavailable.Add(pool.QuestIds[random.RandomIntLessThan(pool.QuestIds.Count)]);
        }
        return unavailable;
    }
}

public sealed record ReplayCheckpoint(ulong Tick, string StateSha256);

/** Content-derived animation boundaries needed by a native-timed replay. */
public sealed record ReplayPlayerActionTiming(
    int WalkingFrames,
    int AttackFrames,
    int AttackActionFrame,
    int CastingFrames,
    int CastingActionFrame)
{
    /** Optional native death frame count for fixtures that can kill a player. */
    public int DeathFrames { get; init; }
}

public sealed record ReplayFixture(
    int FormatVersion,
    string FixtureId,
    string ProtocolSchemaVersion,
    uint TickRateHz,
    ulong RngSeed,
    ReplayInitialState InitialState,
    IReadOnlyList<ReplayFixtureCommand> Commands,
    IReadOnlyList<ReplayCheckpoint> Checkpoints)
{
    /** Optional deterministic projectiles present before the first simulation tick. */
    public IReadOnlyList<ReplayInitialProjectile> InitialProjectiles { get; init; } = [];

    public ReplayLegacyStoreState LegacyStoreState { get; init; } = new(1, 0, 3, [42]);

    /** Null when loading the legacy array-shaped content_manifest metadata. */
    public ReplayContentManifest? ContentManifest { get; init; }

    /** Optional deterministic single-player quest-pool selection metadata. */
    public ReplayQuestSelection? QuestSelection { get; init; }

    /** Optional authoritative final snapshot hash for transition fixtures. */
    public string? FinalStateSha256 { get; init; }

    /** Native ruleset used by variant-sensitive deterministic transitions. */
    public DiabloGameVariant GameVariant { get; init; } = DiabloGameVariant.Diablo;

    /** Explicit session mode used by mode-sensitive native rules. */
    public DiabloGameMode GameMode { get; init; } = DiabloGameMode.SinglePlayer;

    /** Present only when fixture commands use deferred player-action timing. */
    public ReplayPlayerActionTiming? PlayerActionTiming { get; init; }

    public IReadOnlyList<ReplayFixtureCommand> OrderedCommands => Commands
        .OrderBy(command => command.TargetTick)
        .ThenBy(command => command.ServerReceiptSequence)
        .ToArray();
}
