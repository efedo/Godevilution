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

/** Exact native direction conversion and rotation used by authoritative player rules. */
internal static class DiabloPlayerDirections
{
    private static readonly (int X, int Y)[] Displacements = [
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
        (0, -1),
        (1, -1),
        (1, 0),
    ];

    public static DiabloPlayerDirection FromAdjacentStep(int directionX, int directionY)
    {
        for (var index = 0; index < Displacements.Length; index++) {
            if (Displacements[index] == (directionX, directionY))
                return (DiabloPlayerDirection)index;
        }
        throw new ArgumentOutOfRangeException(nameof(directionX));
    }

    public static DiabloPlayerDirection Toward(
        int startX,
        int startY,
        int destinationX,
        int destinationY)
    {
        var deltaX = destinationX - startX;
        var deltaY = destinationY - startY;
        int direction;
        if (deltaX >= 0) {
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return DiabloPlayerDirection.SouthWest;
                direction = (int)DiabloPlayerDirection.South;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return DiabloPlayerDirection.NorthEast;
                direction = (int)DiabloPlayerDirection.East;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = (int)DiabloPlayerDirection.SouthEast;
        } else {
            deltaX = -deltaX;
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return DiabloPlayerDirection.SouthWest;
                direction = (int)DiabloPlayerDirection.West;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return DiabloPlayerDirection.NorthEast;
                direction = (int)DiabloPlayerDirection.North;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = (int)DiabloPlayerDirection.NorthWest;
        }
        return (DiabloPlayerDirection)direction;
    }

    public static DiabloPlayerDirection RotateLeft(DiabloPlayerDirection direction, int steps = 1)
    {
        return (DiabloPlayerDirection)(((int)direction + 8 - (steps % 8)) % 8);
    }

    public static (int X, int Y) GetDisplacement(DiabloPlayerDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return Displacements[(int)direction];
    }
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

/** One learned spell and its native-compatible base level. */
public sealed record MemorizedSpellState(uint SpellId, uint Level);

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
    AuthoritativeItemCatalog? items = null)
{
    public uint Gold { get; set; } = gold;

    public uint Experience { get; set; } = experience;

    public int Life { get; set; } = Math.Clamp(life, 0, Math.Max(lifeMaximum, 0));

    public int BaseLifeMaximum { get; } = Math.Max(
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
    public uint ReflectCharges { get; set; } = reflectCharges <= ushort.MaxValue
        ? reflectCharges
        : throw new ArgumentOutOfRangeException(nameof(reflectCharges));

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

    public int Mana { get; set; } = mana;

    public int BaseManaMaximum { get; } = Math.Max(
        baseManaMaximum >= 0 ? baseManaMaximum : manaMaximum,
        0);

    public int ManaMaximum { get; private set; } = Math.Max(manaMaximum, 0);

    public PlayerAttributesState Attributes { get; set; } = attributes;

    private AuthoritativeEquipmentAttributeBonuses AppliedEquipmentAttributeBonuses { get; set; }

    public List<EquippedStoreItem> Equipment { get; } = equipment.ToList();

    public List<BeltStoreItem> Belt { get; } = belt.ToList();

    public List<int> InventoryGrid { get; } = inventoryGrid.ToList();

    public uint? ActiveStoreId { get; set; }

    public List<OwnedStoreItem> Inventory { get; } = inventory?.ToList() ?? [];

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

    /** Recomputes effective primary attributes without changing resources. */
    public void RecalculateEquipmentAttributes()
    {
        Attributes = AuthoritativeEquipmentAttributeRules.Apply(
            Attributes,
            Equipment,
            AppliedEquipmentAttributeBonuses);
        AppliedEquipmentAttributeBonuses = AuthoritativeEquipmentAttributeRules.ResolveBonuses(Equipment);
    }

    /** Aligns attribute bookkeeping with a serialized equipment/attribute pair. */
    internal void AdoptEquipmentAttributeSnapshot()
    {
        AppliedEquipmentAttributeBonuses = AuthoritativeEquipmentAttributeRules.ResolveBonuses(Equipment);
    }

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
