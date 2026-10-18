using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Native-compatible authoritative player action modes. Numeric values mirror `PLR_MODE`. */
public enum DiabloPlayerCombatMode
{
    Stand = 0,
    WalkNorthwards = 1,
    WalkSouthwards = 2,
    WalkSideways = 3,
    Attack = 4,
    RangedAttack = 5,
    Block = 6,
    GotHit = 7,
    Death = 8,
    Spell = 9,
    NewLevel = 10,
    Quit = 11,
}

/** Native eight-way player facing encoded in the same order as `Direction`. */
public enum DiabloPlayerDirection
{
    South = 0,
    SouthWest = 1,
    West = 2,
    NorthWest = 3,
    North = 4,
    NorthEast = 5,
    East = 6,
    SouthEast = 7,
}

/** Protocol-independent authoritative inventory item owned by a player. */
public sealed record OwnedStoreItem(
    uint StoreId,
    uint StoreSlot,
    uint ItemSeed,
    uint Price,
    ulong PurchasedAtTick,
    AuthoritativeItemState State)
{
    public OwnedStoreItem(uint storeId, uint storeSlot, uint itemSeed, uint price, ulong purchasedAtTick)
        : this(storeId, storeSlot, itemSeed, price, purchasedAtTick, AuthoritativeItemState.Empty)
    {
    }
}

public sealed record PlayerAttributeState(int Base, int Current);

/** Native primary attributes that can receive server-owned level-up points. */
public enum DiabloPlayerAttribute
{
    Strength = 1,
    Magic = 2,
    Dexterity = 3,
    Vitality = 4,
}

public sealed record PlayerAttributesState(
    PlayerAttributeState Strength,
    PlayerAttributeState Magic,
    PlayerAttributeState Dexterity,
    PlayerAttributeState Vitality)
{
    public static PlayerAttributesState Zero { get; } = new(
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0));
}

public sealed record EquippedStoreItem(uint Slot, uint ItemSeed, AuthoritativeItemState State)
{
    public EquippedStoreItem(uint slot, uint itemSeed)
        : this(slot, itemSeed, AuthoritativeItemState.Empty)
    {
    }
}

public sealed record BeltStoreItem(uint Slot, uint ItemSeed, AuthoritativeItemState State)
{
    public BeltStoreItem(uint slot, uint itemSeed)
        : this(slot, itemSeed, AuthoritativeItemState.Empty)
    {
    }
}

/** One server-owned item placed in a native-compatible stash page grid. */
public sealed record StashStoreItem(
    uint Page,
    uint Cell,
    uint StoreId,
    uint StoreSlot,
    uint ItemSeed,
    uint Price,
    ulong PurchasedAtTick,
    AuthoritativeItemState State);

/** One learned spell and its native-compatible base level. */
public sealed record MemorizedSpellState(uint SpellId, uint Level);

/** Remaining native A* direction queue for an opted-in destination walk. */
public sealed record DiabloNativeWalkPathState(uint LevelId, IReadOnlyList<int> Directions);

/** Protocol-independent read-only projection of authoritative player state. */
public sealed record StorePlayerSnapshot(
    uint Gold,
    uint? ActiveStoreId,
    IReadOnlyList<OwnedStoreItem> Inventory,
    uint Experience,
    int Life,
    int Mana,
    int ManaMaximum,
    PlayerAttributesState Attributes,
    IReadOnlyList<EquippedStoreItem> Equipment,
    IReadOnlyList<int> InventoryGrid,
    IReadOnlyList<BeltStoreItem> Belt)
{
    /** Item currently carried by the native cursor and excluded from inventory totals. */
    public OwnedStoreItem? HeldItem { get; init; }

    /** Server-owned stash items; page and cell identify the 10x10 placement. */
    public IReadOnlyList<StashStoreItem> Stash { get; init; } = [];

    /** Native stash gold, persisted separately from the carried wallet. */
    public uint StashGold { get; init; }

    /** Whether the player has an authoritative stash session open. */
    public bool StashOpen { get; init; }

    /** Native-clamped light radius derived from identified equipment. */
    public int LightRadius { get; init; }

    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int LifeMaximum { get; init; }
    /** Unmodified native life maximum used to derive equipment conversions. */
    public int BaseLifeMaximum { get; init; }
    /** Unmodified native mana maximum used to derive equipment conversions. */
    public int BaseManaMaximum { get; init; }
    public uint CharacterLevel { get; init; } = 1;
    public uint LevelId { get; init; }
    public uint EntityId { get; init; }
    public uint CharacterClassId { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public bool ManaShieldActive { get; init; }
    public uint ReflectCharges { get; init; }
    /** Native persistent hero rank set after a single-player Diablo defeat. */
    public uint DiabloKillLevel { get; init; }

    /** Server-owned level-up points available for primary attributes. */
    public uint StatPoints { get; init; }
    /** Native per-player dungeon visitation flags used by authored quest gates. */
    public IReadOnlyList<uint> VisitedLevelIds { get; init; } = [];
    public uint AbilitySpellId { get; init; }
    /** Content-owned semantic role for the active class skill. */
    public DiabloPlayerAbilityKind AbilityKind { get; init; }
    public IReadOnlyList<MemorizedSpellState> MemorizedSpells { get; init; } = [];
    public IReadOnlyList<AuthoritativeStatusEffect> StatusEffects { get; init; } = [];

    /** Native-compatible opt-out from non-PvP targeting. */
    public bool FriendlyMode { get; init; } = true;

    /** Server-owned native facing used by action projection and directional drops. */
    public DiabloPlayerDirection FacingDirection { get; init; } = DiabloPlayerDirection.South;

    /** Server-owned native action mode used by gameplay admission and projection. */
    public DiabloPlayerCombatMode CombatMode { get; init; }

    /** First authoritative tick on which a timed combat mode is no longer active. */
    public ulong CombatModeEndTick { get; init; }

    /** Complete in-progress authoritative action, when animation timing is enabled. */
    public AuthoritativePlayerActionState? CurrentAction { get; init; }

    /** Optional v10-only remainder of a native destination walk. */
    public DiabloNativeWalkPathState? NativeWalkPath { get; init; }

    public StorePlayerSnapshot(uint gold, uint? activeStoreId, IReadOnlyList<OwnedStoreItem> inventory)
        : this(gold, activeStoreId, inventory, 0, 0, 0, 0, PlayerAttributesState.Zero, [], [], [])
    {
    }

    public StorePlayerSnapshot(
        uint gold,
        uint? activeStoreId,
        IReadOnlyList<OwnedStoreItem> inventory,
        uint experience,
        int life,
        int mana)
        : this(gold, activeStoreId, inventory, experience, life, mana, mana, PlayerAttributesState.Zero, [], [], [])
    {
    }
}

/** Mutable authoritative state consumed by protocol-independent Diablo rules. */
public sealed class AuthoritativePlayerState(
    uint gold,
    uint experience,
    int life,
    int mana,
    int manaMaximum,
    PlayerAttributesState attributes,
    IReadOnlyList<EquippedStoreItem> equipment,
    IReadOnlyList<int> inventoryGrid,
    IReadOnlyList<BeltStoreItem> belt,
    int positionX,
    int positionY,
    int lifeMaximum,
    uint characterLevel,
    uint levelId,
    int armorClass,
    uint characterClassId = 0,
    IReadOnlyList<OwnedStoreItem>? inventory = null,
    uint abilitySpellId = 0,
    IReadOnlyList<MemorizedSpellState>? memorizedSpells = null,
    bool friendlyMode = true,
    DiabloPlayerCombatMode combatMode = DiabloPlayerCombatMode.Stand,
    ulong combatModeEndTick = 0,
    OwnedStoreItem? heldItem = null,
    DiabloPlayerDirection facingDirection = DiabloPlayerDirection.South,
    string characterName = "",
    bool manaShieldActive = false,
    uint reflectCharges = 0,
    int baseLifeMaximum = -1,
    int baseManaMaximum = -1,
    AuthoritativePlayerClassDefinition? playerClass = null,
    DiabloPlayerAbilityKind abilityKind = DiabloPlayerAbilityKind.None,
    AuthoritativeItemCatalog? items = null,
    int maximumAttribute = AuthoritativeEquipmentAttributeRules.NativeMaximumAttribute,
    uint diabloKillLevel = 0,
    IReadOnlyList<uint>? visitedLevelIds = null,
    uint statPoints = 0,
    IReadOnlyList<StashStoreItem>? stash = null,
    uint stashGold = 0,
    bool goldTopologyEnabled = true)
{
    public uint Gold { get; set; } = gold;

    /** Whether an empty native-sized inventory grid represents Gold topology. */
    public bool GoldTopologyEnabled { get; } = goldTopologyEnabled;

    public uint Experience { get; set; } = experience;

    public int Life { get; set; } = Math.Clamp(life, 0, Math.Max(lifeMaximum, 0));

    /** Native base life maximum, before equipment conversions. */
    public int BaseLifeMaximum { get; internal set; } = Math.Max(
        baseLifeMaximum >= 0 ? baseLifeMaximum : lifeMaximum,
        0);

    public int LifeMaximum { get; private set; } = Math.Max(lifeMaximum, 0);

    public uint CharacterLevel { get; set; } = characterLevel;

    public int PositionX { get; set; } = positionX;

    public int PositionY { get; set; } = positionY;

    public uint LevelId { get; set; } = levelId;

    /** Unmodified native player armor before equipment and dexterity. */
    public int BaseArmorClass { get; } = Math.Max(0, armorClass);

    /** Effective native player armor after equipment and dexterity. */
    public int ArmorClass { get; private set; } = Math.Max(0, armorClass);

    /** Native-clamped light radius derived from identified equipment. */
    public int LightRadius => AuthoritativeEquipmentLightRules.CalculateLightRadius(Equipment);

    public uint CharacterClassId { get; set; } = characterClassId;

    /** Content-owned native class metadata used for equipment resource scaling. */
    public AuthoritativePlayerClassDefinition? PlayerClass { get; internal set; } = playerClass;

    /** Validated item definitions used for semantic equipment classification. */
    public AuthoritativeItemCatalog? Items { get; } = items;

    /** Native-compatible character identity used for multiplayer presentation and ears. */
    public string CharacterName { get; set; } = string.IsNullOrEmpty(characterName)
        ? string.Empty
        : DiabloPlayerName.RequireValid(characterName, nameof(characterName));

    public uint AbilitySpellId { get; set; } = abilitySpellId;

    /** Content-owned semantic role for the active class skill. */
    public DiabloPlayerAbilityKind AbilityKind { get; internal set; } =
        Enum.IsDefined(abilityKind)
            ? abilityKind
            : throw new ArgumentOutOfRangeException(nameof(abilityKind));

    /**
     * Checks a class capability by its content-owned semantic role.
     *
     * Legacy snapshots and direct callers may only carry the historical
     * spell identity, so the optional fallback is considered only when no
     * semantic role was serialized. An explicit role always takes precedence.
     */
    public bool HasAbility(
        DiabloPlayerAbilityKind requiredKind,
        uint legacySpellId = 0)
    {
        if (!Enum.IsDefined(requiredKind)
            || requiredKind == DiabloPlayerAbilityKind.None
            || AbilitySpellId == 0) {
            return false;
        }
        if (AbilityKind != DiabloPlayerAbilityKind.None)
            return AbilityKind == requiredKind;
        return legacySpellId != 0 && AbilitySpellId == legacySpellId;
    }

    /** True while the server-owned native Mana Shield defense is active. */
    public bool ManaShieldActive { get; set; } = manaShieldActive;

    /** Native-compatible remaining Reflect charges, capped at uint16 like DevilutionX. */
    public uint ReflectCharges { get; set; } = reflectCharges <= NativeCompatibilityLimits.MaximumNativeReflectCharges
        ? reflectCharges
        : throw new ArgumentOutOfRangeException(nameof(reflectCharges));

    /** Native persistent hero rank set after a single-player Diablo defeat. */
    public uint DiabloKillLevel { get; set; } = diabloKillLevel;

    /** Server-owned native _pLvlVisited flags, keyed by authored level ID. */
    public HashSet<uint> VisitedLevelIds { get; } =
        (visitedLevelIds ?? [levelId])
            .Where(AuthoritativeLevelIdentityRules.IsRegularLevel)
            .ToHashSet();

    public bool HasVisitedLevel(uint levelId) => VisitedLevelIds.Contains(levelId);

    public void MarkLevelVisited(uint levelId)
    {
        if (AuthoritativeLevelIdentityRules.IsRegularLevel(levelId))
            VisitedLevelIds.Add(levelId);
    }

    /** True while the player has not opted into hostile player targeting. */
    public bool FriendlyMode { get; set; } = friendlyMode;

    /** Current server-owned native facing. */
    public DiabloPlayerDirection FacingDirection { get; set; } = Enum.IsDefined(facingDirection)
        ? facingDirection
        : throw new ArgumentOutOfRangeException(nameof(facingDirection));

    /** Current server-owned native action mode. */
    public DiabloPlayerCombatMode CombatMode { get; set; } = Enum.IsDefined(combatMode)
        ? combatMode
        : throw new ArgumentOutOfRangeException(nameof(combatMode));

    /** First authoritative tick on which the current timed mode has expired. */
    public ulong CombatModeEndTick { get; set; } = combatModeEndTick;

    /** Deferred gameplay action currently driven by the native animation timeline. */
    public AuthoritativePlayerActionState? CurrentAction { get; set; }

    /** Native destination walk directions, captured once and consumed in order. */
    public DiabloNativeWalkPathState? NativeWalkPath { get; set; }

    public bool IsMoving => CurrentAction?.Kind == DiabloPlayerActionKind.Move;

    public int FuturePositionX => IsMoving
        ? checked(PositionX + CurrentAction!.DirectionX)
        : PositionX;

    public int FuturePositionY => IsMoving
        ? checked(PositionY + CurrentAction!.DirectionY)
        : PositionY;

    public List<MemorizedSpellState> MemorizedSpells { get; }
        = memorizedSpells?.ToList() ?? [];

    public List<AuthoritativeStatusEffect> StatusEffects { get; set; } = [];

    public ulong? LastAppliedTick { get; set; }

    public uint EntityId { get; set; }

    /** Server-owned level-up points available for primary attributes. */
    public uint StatPoints { get; set; } = statPoints;

    public int Mana { get; set; } = mana;

    /** Native base mana maximum, before equipment conversions. */
    public int BaseManaMaximum { get; internal set; } = Math.Max(
        baseManaMaximum >= 0 ? baseManaMaximum : manaMaximum,
        0);

    public int ManaMaximum { get; private set; } = Math.Max(manaMaximum, 0);

    public PlayerAttributesState Attributes { get; set; } = attributes;

    /** Active content-owned ceiling for equipment-derived primary attributes. */
    public int MaximumAttribute { get; private set; } =
        AuthoritativeEquipmentAttributeRules.ValidateMaximumAttribute(maximumAttribute);

    private AuthoritativeEquipmentAttributeBonuses AppliedEquipmentAttributeBonuses { get; set; }

    public List<EquippedStoreItem> Equipment { get; } = equipment.ToList();

    public List<BeltStoreItem> Belt { get; } = belt.ToList();

    public List<int> InventoryGrid { get; } = inventoryGrid.ToList();

    public uint? ActiveStoreId { get; set; }

    public List<OwnedStoreItem> Inventory { get; } = inventory?.ToList() ?? [];

    /** Server-owned native-compatible stash pages. */
    public List<StashStoreItem> Stash { get; } = stash?.ToList() ?? [];

    /** Server-owned gold stored in the stash. */
    public uint StashGold { get; set; } = stashGold;

    /** Whether stash item and gold commands are currently admitted. */
    public bool StashOpen { get; set; }

    /** Server-owned item currently carried by the presentation cursor. */
    public OwnedStoreItem? HeldItem { get; set; } = heldItem;

    /** Recomputes native equipment attributes, resources, and defense. */
    public void RecalculateEquipmentResources()
    {
        RecalculateEquipmentAttributes();
        var lifeBase = checked(Life + BaseLifeMaximum - LifeMaximum);
        var manaBase = checked(Mana + BaseManaMaximum - ManaMaximum);
        var adjusted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            BaseLifeMaximum,
            BaseManaMaximum,
            Equipment,
            PlayerClass);
        LifeMaximum = adjusted.LifeQ6;
        ManaMaximum = adjusted.ManaQ6;
        Life = Math.Clamp(
            checked(adjusted.LifeQ6 - BaseLifeMaximum + lifeBase),
            0,
            LifeMaximum);
        Mana = Math.Clamp(
            checked(adjusted.ManaQ6 - BaseManaMaximum + manaBase),
            0,
            ManaMaximum);
        RecalculateEquipmentArmor();
    }

    /** Applies one native level-up resource increase and refill. */
    internal void AdvanceLevel()
    {
        CharacterLevel = checked(CharacterLevel + 1);

        var lifePerLevel = PlayerClass?.LifePerLevelQ6 ?? 0;
        BaseLifeMaximum = checked(BaseLifeMaximum + lifePerLevel);
        LifeMaximum = checked(LifeMaximum + lifePerLevel);
        Life = LifeMaximum;

        var manaPerLevel = PlayerClass?.ManaPerLevelQ6 ?? 0;
        BaseManaMaximum = checked(BaseManaMaximum + manaPerLevel);
        ManaMaximum = checked(ManaMaximum + manaPerLevel);
        if (!AuthoritativeEquipmentManaRules.HasNoManaItem(Equipment))
            Mana = ManaMaximum;

        RecalculateEquipmentResources();
    }

    /** Applies one native elixir stat point and its resource side effect. */
    public bool ApplyElixir(DiabloItemActionKind action, bool restoreHellfireResource)
    {
        if (action is not (DiabloItemActionKind.IncreaseStrength
            or DiabloItemActionKind.IncreaseMagic
            or DiabloItemActionKind.IncreaseDexterity
            or DiabloItemActionKind.IncreaseVitality)) {
            return false;
        }

        var maximum = action switch {
            DiabloItemActionKind.IncreaseStrength => PlayerClass?.MaximumStrength,
            DiabloItemActionKind.IncreaseMagic => PlayerClass?.MaximumMagic,
            DiabloItemActionKind.IncreaseDexterity => PlayerClass?.MaximumDexterity,
            DiabloItemActionKind.IncreaseVitality => PlayerClass?.MaximumVitality,
            _ => null,
        } ?? MaximumAttribute;

        var attributes = Attributes;
        var attribute = action switch {
            DiabloItemActionKind.IncreaseStrength => attributes.Strength,
            DiabloItemActionKind.IncreaseMagic => attributes.Magic,
            DiabloItemActionKind.IncreaseDexterity => attributes.Dexterity,
            DiabloItemActionKind.IncreaseVitality => attributes.Vitality,
            _ => throw new InvalidOperationException("Validated elixir action was not handled."),
        };
        var available = Math.Max(maximum - attribute.Base, 0);
        var delta = Math.Min(1, available);
        var updatedAttribute = attribute with {
            Base = checked(attribute.Base + delta),
            Current = Math.Clamp(checked(attribute.Current + delta), 0, MaximumAttribute),
        };
        Attributes = action switch {
            DiabloItemActionKind.IncreaseStrength => attributes with { Strength = updatedAttribute },
            DiabloItemActionKind.IncreaseMagic => attributes with { Magic = updatedAttribute },
            DiabloItemActionKind.IncreaseDexterity => attributes with { Dexterity = updatedAttribute },
            DiabloItemActionKind.IncreaseVitality => attributes with { Vitality = updatedAttribute },
            _ => attributes,
        };

        if (action == DiabloItemActionKind.IncreaseMagic) {
            var amount = checked(delta * (PlayerClass?.ManaPerMagicQ6 ?? 0));
            BaseManaMaximum = checked(BaseManaMaximum + amount);
            ManaMaximum = checked(ManaMaximum + amount);
            if (!AuthoritativeEquipmentManaRules.HasNoManaItem(Equipment))
                Mana = checked(Mana + amount);
        } else if (action == DiabloItemActionKind.IncreaseVitality) {
            var amount = checked(delta * (PlayerClass?.LifePerVitalityQ6 ?? 0));
            BaseLifeMaximum = checked(BaseLifeMaximum + amount);
            LifeMaximum = checked(LifeMaximum + amount);
            Life = checked(Life + amount);
        }

        RecalculateEquipmentResources();
        if (restoreHellfireResource
            && action == DiabloItemActionKind.IncreaseMagic
            && !AuthoritativeEquipmentManaRules.HasNoManaItem(Equipment))
            Mana = ManaMaximum;
        if (restoreHellfireResource && action == DiabloItemActionKind.IncreaseVitality)
            Life = LifeMaximum;
        return true;
    }

    /** Spends level-up points on one native primary attribute atomically. */
    public bool AllocateAttribute(DiabloPlayerAttribute attribute, uint amount)
    {
        if (!Enum.IsDefined(attribute) || amount == 0 || amount > StatPoints)
            return false;

        var attributes = Attributes;
        var current = attribute switch {
            DiabloPlayerAttribute.Strength => attributes.Strength,
            DiabloPlayerAttribute.Magic => attributes.Magic,
            DiabloPlayerAttribute.Dexterity => attributes.Dexterity,
            DiabloPlayerAttribute.Vitality => attributes.Vitality,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute)),
        };
        var maximum = attribute switch {
            DiabloPlayerAttribute.Strength => PlayerClass?.MaximumStrength,
            DiabloPlayerAttribute.Magic => PlayerClass?.MaximumMagic,
            DiabloPlayerAttribute.Dexterity => PlayerClass?.MaximumDexterity,
            DiabloPlayerAttribute.Vitality => PlayerClass?.MaximumVitality,
            _ => null,
        } ?? MaximumAttribute;
        var available = Math.Max(maximum - current.Base, 0);
        if (amount > (uint)available)
            return false;

        var delta = checked((int)amount);
        var updated = current with {
            Base = checked(current.Base + delta),
            Current = Math.Clamp(checked(current.Current + delta), 0, MaximumAttribute),
        };
        Attributes = attribute switch {
            DiabloPlayerAttribute.Strength => attributes with { Strength = updated },
            DiabloPlayerAttribute.Magic => attributes with { Magic = updated },
            DiabloPlayerAttribute.Dexterity => attributes with { Dexterity = updated },
            DiabloPlayerAttribute.Vitality => attributes with { Vitality = updated },
            _ => attributes,
        };
        StatPoints -= amount;

        if (attribute == DiabloPlayerAttribute.Magic) {
            var resource = checked(delta * (PlayerClass?.ManaPerMagicQ6 ?? 0));
            BaseManaMaximum = checked(BaseManaMaximum + resource);
            ManaMaximum = checked(ManaMaximum + resource);
            if (!AuthoritativeEquipmentManaRules.HasNoManaItem(Equipment))
                Mana = checked(Mana + resource);
        } else if (attribute == DiabloPlayerAttribute.Vitality) {
            var resource = checked(delta * (PlayerClass?.LifePerVitalityQ6 ?? 0));
            BaseLifeMaximum = checked(BaseLifeMaximum + resource);
            LifeMaximum = checked(LifeMaximum + resource);
            Life = checked(Life + resource);
        }

        RecalculateEquipmentResources();
        return true;
    }

    /** Recomputes effective primary attributes without changing resources. */
    public void RecalculateEquipmentAttributes()
    {
        Attributes = AuthoritativeEquipmentAttributeRules.Apply(
            Attributes,
            Equipment,
            AppliedEquipmentAttributeBonuses,
            MaximumAttribute);
        AppliedEquipmentAttributeBonuses = AuthoritativeEquipmentAttributeRules.ResolveBonuses(Equipment);
    }

    /** Applies the simulation's active attribute ceiling to this mutable state. */
    internal void ConfigureMaximumAttribute(int maximumAttribute)
    {
        maximumAttribute = AuthoritativeEquipmentAttributeRules.ValidateMaximumAttribute(maximumAttribute);
        if (MaximumAttribute == maximumAttribute)
            return;
        MaximumAttribute = maximumAttribute;
        RecalculateEquipmentAttributes();
    }

    /** Aligns attribute bookkeeping with a serialized equipment/attribute pair. */
    internal void AdoptEquipmentAttributeSnapshot()
    {
        AppliedEquipmentAttributeBonuses = AuthoritativeEquipmentAttributeRules.ResolveBonuses(Equipment);
    }

    /** Restores derived equipment bookkeeping without recalculating raw state. */
    internal void RestoreCohortDerivedState(
        int armorClass,
        AuthoritativeEquipmentAttributeBonuses appliedEquipmentAttributeBonuses)
    {
        if (armorClass < 0)
            throw new ArgumentOutOfRangeException(nameof(armorClass));
        ArmorClass = armorClass;
        AppliedEquipmentAttributeBonuses = appliedEquipmentAttributeBonuses;
    }

    internal AuthoritativeEquipmentAttributeBonuses
        CaptureAppliedEquipmentAttributeBonuses()
        => AppliedEquipmentAttributeBonuses;

    /** Recomputes effective armor without changing current life or mana. */
    public void RecalculateEquipmentArmor()
    {
        ArmorClass = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            BaseArmorClass,
            Attributes.Dexterity.Current,
            Equipment,
            PlayerClass,
            CharacterLevel,
            Items);
    }
}
