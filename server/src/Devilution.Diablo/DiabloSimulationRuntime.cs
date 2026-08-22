using Devilution.Engine;

namespace Devilution.Diablo;

/** Immutable starting-state template for one authoritative Diablo participant. */
public sealed class DiabloPlayerTemplate
{
    private const int MaximumInventoryCount = 40;
    private const int MaximumEquipmentCount = 7;
    private const int MaximumBeltCount = 8;
    private const int MaximumInventoryGridCells = 400;

    /** Creates the normalized first-party starting player used by server hosts. */
    public static DiabloPlayerTemplate CreateDefault(
        uint gold,
        int? life,
        int? mana,
        uint levelId,
        AuthoritativePlayerClassDefinition? playerClass = null)
    {
        var attributes = playerClass?.CreateStartingAttributes()
            ?? PlayerAttributesState.Zero;
        var resolvedLife = life
            ?? playerClass?.CalculateBaseLifeQ6(1, attributes.Vitality.Base)
            ?? 0;
        var resolvedMana = mana
            ?? playerClass?.CalculateBaseManaQ6(1, attributes.Magic.Base)
            ?? 0;
        return new DiabloPlayerTemplate(
            gold,
            0,
            resolvedLife,
            resolvedMana,
            resolvedMana,
            attributes,
            null,
            Enumerable.Repeat(-1, 40).ToArray(),
            null,
            0,
            0,
            resolvedLife,
            1,
            levelId,
            0,
            playerClass?.ClassId ?? 0,
            baseLifeMaximum: resolvedLife,
            baseManaMaximum: resolvedMana,
            playerClass: playerClass);
    }

    public DiabloPlayerTemplate(
        uint gold,
        uint experience,
        int life,
        int mana,
        int manaMaximum,
        PlayerAttributesState? attributes,
        IReadOnlyList<EquippedStoreItem>? equipment,
        IReadOnlyList<int>? inventoryGrid,
        IReadOnlyList<BeltStoreItem>? belt,
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
        uint statPoints = 0,
        IReadOnlyList<StashStoreItem>? stash = null,
        uint stashGold = 0)
    {
        ValidateCount(equipment, MaximumEquipmentCount, "equipment");
        ValidateCount(inventoryGrid, MaximumInventoryGridCells, "inventory-grid");
        ValidateCount(belt, MaximumBeltCount, "belt");
        ValidateCount(inventory, MaximumInventoryCount, "inventory");
        ValidateCount(memorizedSpells, AuthoritativePlayerLoadoutCatalog.MaximumMemorizedSpellCount, "memorized-spell");
        ValidateCount(stash, DiabloStashLayout.MaximumItems, "stash");
        ValidateUniqueSlots(equipment, item => item.Slot, "equipment");
        ValidateUniqueSlots(belt, item => item.Slot, "belt");
        ValidateInventoryGrid(inventoryGrid, inventory?.Count ?? 0);
        ValidateMemorizedSpells(memorizedSpells);
        Gold = gold;
        Experience = experience;
        Life = life;
        Mana = mana;
        ManaMaximum = Math.Max(mana, manaMaximum);
        BaseManaMaximum = Math.Max(
            baseManaMaximum >= 0 ? baseManaMaximum : manaMaximum,
            0);
        Attributes = attributes ?? PlayerAttributesState.Zero;
        Equipment = equipment?.ToArray() ?? [];
        InventoryGrid = inventoryGrid?.ToArray() ?? [];
        Belt = belt?.ToArray() ?? [];
        PositionX = positionX;
        PositionY = positionY;
        LifeMaximum = Math.Max(life, lifeMaximum);
        BaseLifeMaximum = Math.Max(
            baseLifeMaximum >= 0 ? baseLifeMaximum : lifeMaximum,
            0);
        CharacterLevel = Math.Clamp(characterLevel, 1U, 50U);
        LevelId = levelId;
        ArmorClass = Math.Max(0, armorClass);
        CharacterClassId = characterClassId;
        PlayerClass = playerClass;
        Items = items;
        AbilityKind = Enum.IsDefined(abilityKind)
            ? abilityKind
            : throw new ArgumentOutOfRangeException(nameof(abilityKind));
        Inventory = inventory?.ToArray() ?? [];
        AbilitySpellId = abilitySpellId;
        MemorizedSpells = memorizedSpells?.ToArray() ?? [];
        FriendlyMode = friendlyMode;
        CombatMode = Enum.IsDefined(combatMode)
            ? combatMode
            : throw new ArgumentOutOfRangeException(nameof(combatMode));
        CombatModeEndTick = combatModeEndTick;
        HeldItem = heldItem;
        FacingDirection = Enum.IsDefined(facingDirection)
            ? facingDirection
            : throw new ArgumentOutOfRangeException(nameof(facingDirection));
        CharacterName = string.IsNullOrEmpty(characterName)
            ? string.Empty
            : DiabloPlayerName.RequireValid(characterName, nameof(characterName));
        ManaShieldActive = manaShieldActive;
        ReflectCharges = reflectCharges;
        StatPoints = statPoints;
        Stash = stash?.ToArray() ?? [];
        StashGold = stashGold;

        static void ValidateCount<T>(
            IReadOnlyList<T>? values,
            int maximum,
            string category)
        {
            if (values is not null && values.Count > maximum)
                throw new InvalidDataException(
                    $"Player template contains too many {category} entries.");
        }

        static void ValidateUniqueSlots<T>(
            IReadOnlyList<T>? values,
            Func<T, uint> slotSelector,
            string category)
        {
            if (values is not null
                && values.Select(slotSelector).Distinct().Count() != values.Count) {
                throw new InvalidDataException(
                    $"Player template contains duplicate {category} slots.");
            }
        }

        static void ValidateInventoryGrid(
            IReadOnlyList<int>? grid,
            int inventoryCount)
        {
            if (grid is not null && grid.Any(cell => cell < -1 || cell >= inventoryCount))
                throw new InvalidDataException(
                    "Player template contains an invalid inventory-grid reference.");
        }

        static void ValidateMemorizedSpells(IReadOnlyList<MemorizedSpellState>? spells)
        {
            if (spells is null)
                return;
            if (spells.Any(spell =>
                    spell is null
                    || spell.SpellId is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                    || spell.Level is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
                || spells.Where(spell => spell is not null)
                    .Select(spell => spell.SpellId)
                    .Distinct()
                    .Count() != spells.Count) {
                throw new InvalidDataException(
                    "Player template contains invalid memorized-spell state.");
            }
        }
    }

    public uint Gold { get; }
    public uint Experience { get; }
    public int Life { get; }
    public int Mana { get; }
    public int ManaMaximum { get; }
    public int BaseManaMaximum { get; }
    public PlayerAttributesState Attributes { get; }
    public IReadOnlyList<EquippedStoreItem> Equipment { get; }
    public IReadOnlyList<int> InventoryGrid { get; }
    public IReadOnlyList<BeltStoreItem> Belt { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public int LifeMaximum { get; }
    public int BaseLifeMaximum { get; }
    public uint CharacterLevel { get; }
    public uint LevelId { get; }
    public int ArmorClass { get; }
    public uint CharacterClassId { get; }
    public AuthoritativePlayerClassDefinition? PlayerClass { get; }
    public AuthoritativeItemCatalog? Items { get; }
    public IReadOnlyList<OwnedStoreItem> Inventory { get; }
    public uint AbilitySpellId { get; }
    public DiabloPlayerAbilityKind AbilityKind { get; }
    public IReadOnlyList<MemorizedSpellState> MemorizedSpells { get; }
    public bool FriendlyMode { get; }
    public DiabloPlayerCombatMode CombatMode { get; }
    public ulong CombatModeEndTick { get; }
    public OwnedStoreItem? HeldItem { get; }
    public DiabloPlayerDirection FacingDirection { get; }
    public string CharacterName { get; }
    public bool ManaShieldActive { get; }
    public uint ReflectCharges { get; }
    public uint StatPoints { get; }
    public IReadOnlyList<StashStoreItem> Stash { get; }
    public uint StashGold { get; }

    /** Returns a fresh-template identity without mutating the content-owned loadout. */
    public DiabloPlayerTemplate WithCharacterName(string characterName)
    {
        return new DiabloPlayerTemplate(
            Gold,
            Experience,
            Life,
            Mana,
            ManaMaximum,
            Attributes,
            Equipment,
            InventoryGrid,
            Belt,
            PositionX,
            PositionY,
            LifeMaximum,
            CharacterLevel,
            LevelId,
            ArmorClass,
            CharacterClassId,
            Inventory,
            AbilitySpellId,
            MemorizedSpells,
            FriendlyMode,
            CombatMode,
            CombatModeEndTick,
            HeldItem,
            FacingDirection,
            characterName,
            ManaShieldActive,
            ReflectCharges,
            BaseLifeMaximum,
            BaseManaMaximum,
            PlayerClass,
            AbilityKind,
            Items,
            statPoints: StatPoints,
            stash: Stash,
            stashGold: StashGold);
    }

    /** Creates fresh mutable module state from this immutable template. */
    public AuthoritativePlayerState CreatePlayer()
    {
        var player = new AuthoritativePlayerState(
            Gold,
            Experience,
            Life,
            Mana,
            ManaMaximum,
            Attributes,
            Equipment,
            InventoryGrid,
            Belt,
            PositionX,
            PositionY,
            LifeMaximum,
            CharacterLevel,
            LevelId,
            ArmorClass,
            CharacterClassId,
            Inventory,
            AbilitySpellId,
            MemorizedSpells,
            FriendlyMode,
            CombatMode,
            CombatModeEndTick,
            HeldItem,
            FacingDirection,
            CharacterName,
            ManaShieldActive,
            ReflectCharges,
            BaseLifeMaximum,
            BaseManaMaximum,
            PlayerClass,
            AbilityKind,
            Items,
            statPoints: StatPoints,
            stash: Stash,
            stashGold: StashGold);
        player.RecalculateEquipmentAttributes();
        player.RecalculateEquipmentArmor();
        return player;
    }
}

public sealed partial class DiabloSimulation
{
    /** Creates an Engine-owned runtime over this module-owned simulation. */
    public AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>
        CreateRuntime(DiabloPlayerTemplate playerTemplate)
    {
        ArgumentNullException.ThrowIfNull(playerTemplate);
        return new AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>(
            new Lifecycle(this, playerTemplate));
    }

    private sealed class Lifecycle
        : IAuthoritativeSimulationLifecycle<AuthoritativePlayerState, DiabloGameplayEvent>
    {
        private readonly DiabloSimulation simulation;
        private readonly DiabloPlayerTemplate playerTemplate;

        public Lifecycle(DiabloSimulation simulation, DiabloPlayerTemplate playerTemplate)
        {
            this.simulation = simulation;
            this.playerTemplate = playerTemplate;
            simulation.WorldState.ValidateInitialPlayerPosition(
                playerTemplate.LevelId,
                playerTemplate.PositionX,
                playerTemplate.PositionY);
        }

        public AuthoritativePlayerState CreateParticipant(string sessionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            var player = playerTemplate.CreatePlayer();
            player.ConfigureMaximumAttribute(simulation.CombatRules.MaximumAttribute);
            return player;
        }

        public void AdvanceParticipant(AuthoritativePlayerState state, ulong tick)
        {
            simulation.AdvanceStatusEffects(state, tick);
        }

        public IReadOnlyList<DirectedSimulationEvent<AuthoritativePlayerState, DiabloGameplayEvent>>
            AdvanceTick(ulong tick, IReadOnlyList<AuthoritativePlayerState> participants)
        {
            return simulation.AdvanceTick(tick, participants)
                .Select(delivery =>
                    new DirectedSimulationEvent<AuthoritativePlayerState, DiabloGameplayEvent>(
                        delivery.Recipient,
                        delivery.Event))
                .ToArray();
        }
    }
}
