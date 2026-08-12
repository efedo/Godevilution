using Devilution.Engine;

namespace Devilution.Diablo;

/** Immutable starting-state template for one authoritative Diablo participant. */
public sealed class DiabloPlayerTemplate
{
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
            playerClass?.ClassId ?? 0);
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
        uint reflectCharges = 0)
    {
        Gold = gold;
        Experience = experience;
        Life = life;
        Mana = mana;
        ManaMaximum = Math.Max(mana, manaMaximum);
        Attributes = attributes ?? PlayerAttributesState.Zero;
        Equipment = equipment?.ToArray() ?? [];
        InventoryGrid = inventoryGrid?.ToArray() ?? [];
        Belt = belt?.ToArray() ?? [];
        PositionX = positionX;
        PositionY = positionY;
        LifeMaximum = Math.Max(life, lifeMaximum);
        CharacterLevel = Math.Clamp(characterLevel, 1U, 50U);
        LevelId = levelId;
        ArmorClass = Math.Max(0, armorClass);
        CharacterClassId = characterClassId;
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
    }

    public uint Gold { get; }
    public uint Experience { get; }
    public int Life { get; }
    public int Mana { get; }
    public int ManaMaximum { get; }
    public PlayerAttributesState Attributes { get; }
    public IReadOnlyList<EquippedStoreItem> Equipment { get; }
    public IReadOnlyList<int> InventoryGrid { get; }
    public IReadOnlyList<BeltStoreItem> Belt { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public int LifeMaximum { get; }
    public uint CharacterLevel { get; }
    public uint LevelId { get; }
    public int ArmorClass { get; }
    public uint CharacterClassId { get; }
    public IReadOnlyList<OwnedStoreItem> Inventory { get; }
    public uint AbilitySpellId { get; }
    public IReadOnlyList<MemorizedSpellState> MemorizedSpells { get; }
    public bool FriendlyMode { get; }
    public DiabloPlayerCombatMode CombatMode { get; }
    public ulong CombatModeEndTick { get; }
    public OwnedStoreItem? HeldItem { get; }
    public DiabloPlayerDirection FacingDirection { get; }
    public string CharacterName { get; }
    public bool ManaShieldActive { get; }
    public uint ReflectCharges { get; }

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
            ReflectCharges);
    }

    /** Creates fresh mutable module state from this immutable template. */
    public AuthoritativePlayerState CreatePlayer()
    {
        return new AuthoritativePlayerState(
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
            ReflectCharges);
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
            return playerTemplate.CreatePlayer();
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
