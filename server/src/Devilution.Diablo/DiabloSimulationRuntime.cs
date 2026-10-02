using Devilution.Engine;
using Devilution.Domain.Inventory;
using Devilution.Shared;
using System.Runtime.CompilerServices;

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
        uint stashGold = 0,
        bool goldTopologyEnabled = true)
    {
        ValidateCount(equipment, MaximumEquipmentCount, "equipment");
        ValidateCount(inventoryGrid, MaximumInventoryGridCells, "inventory-grid");
        ValidateCount(belt, MaximumBeltCount, "belt");
        ValidateCount(inventory, MaximumInventoryCount, "inventory");
        ValidateCount(memorizedSpells, AuthoritativePlayerLoadoutCatalog.MaximumMemorizedSpellCount, "memorized-spell");
        ValidateCount(stash, DiabloStashLayout.MaximumItems, "stash");
        ValidateScalarState(
            life,
            mana,
            manaMaximum,
            lifeMaximum,
            baseLifeMaximum,
            baseManaMaximum,
            abilitySpellId,
            reflectCharges,
            combatMode,
            combatModeEndTick);
        ValidateAttributes(attributes);
        ValidateItemStates(equipment, item => item.State, "equipment");
        ValidateItemStates(inventory, item => item.State, "inventory");
        ValidateItemStates(belt, item => item.State, "belt");
        ValidateHeldItem(heldItem);
        ValidateInventoryFootprints(inventoryGrid, inventory);
        ValidateSlotRange(equipment, item => item.Slot, 7, "equipment");
        ValidateSlotRange(belt, item => item.Slot, 8, "belt");
        ValidateUniqueSlots(equipment, item => item.Slot, "equipment");
        ValidateUniqueSlots(belt, item => item.Slot, "belt");
        ValidateInventoryGrid(inventoryGrid, inventory);
        ValidateMemorizedSpells(memorizedSpells);
        var itemIdentities = new HashSet<(uint Seed, uint CreateInfo, int ItemIndex)>();
        ValidateItemIdentities(inventory, item => item.ItemSeed, item => item.State, itemIdentities);
        ValidateItemIdentities(equipment, item => item.ItemSeed, item => item.State, itemIdentities);
        ValidateItemIdentities(belt, item => item.ItemSeed, item => item.State, itemIdentities);
        ValidateItemIdentities(stash, item => item.ItemSeed, item => item.State, itemIdentities);
        ValidateItemIdentity(heldItem, itemIdentities);
        ValidateCollectionItemMetadata(inventory, item => item.ItemSeed, item => item.State, items);
        ValidateCollectionItemMetadata(equipment, item => item.ItemSeed, item => item.State, items);
        ValidateCollectionItemMetadata(belt, item => item.ItemSeed, item => item.State, items);
        ValidateCollectionItemMetadata(stash, item => item.ItemSeed, item => item.State, items);
        ValidateOwnedItemMetadata(heldItem, item => item.ItemSeed, item => item.State, items);
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
        CharacterLevel = Math.Clamp(characterLevel, 1U, NativeCompatibilityLimits.MaximumNativeCharacterLevel);
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
        GoldTopologyEnabled = goldTopologyEnabled;

        static void ValidateCount<T>(
            IReadOnlyList<T>? values,
            int maximum,
            string category)
        {
            if (values is not null && values.Count > maximum)
                throw new InvalidDataException(
                    $"Player template contains too many {category} entries.");
        }

        static void ValidateScalarState(
            int life,
            int mana,
            int manaMaximum,
            int lifeMaximum,
            int baseLifeMaximum,
            int baseManaMaximum,
            uint abilitySpellId,
            uint reflectCharges,
            DiabloPlayerCombatMode combatMode,
            ulong combatModeEndTick)
        {
            if (life < 0
                || mana < 0
                || manaMaximum < 0
                || lifeMaximum < 0
                || baseLifeMaximum < -1
                || baseManaMaximum < -1) {
                throw new InvalidDataException(
                    "Player template contains out-of-range resource state.");
            }
            if (abilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId)
                throw new InvalidDataException(
                    "Player template contains an out-of-range ability spell.");
            if (reflectCharges > NativeCompatibilityLimits.MaximumNativeReflectCharges)
                throw new InvalidDataException(
                    "Player template contains out-of-range reflect charges.");
            if ((combatMode == DiabloPlayerCombatMode.Stand && combatModeEndTick != 0)
                || (combatMode is DiabloPlayerCombatMode.Block or DiabloPlayerCombatMode.GotHit
                    && combatModeEndTick == 0)) {
                throw new InvalidDataException(
                    "Player template contains invalid combat-mode timing.");
            }
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

        static void ValidateAttributes(PlayerAttributesState? attributes)
        {
            if (attributes is null)
                return;
            if (attributes.Strength is null
                || attributes.Magic is null
                || attributes.Dexterity is null
                || attributes.Vitality is null
                || attributes.Strength.Base < 0
                || attributes.Strength.Current < 0
                || attributes.Magic.Base < 0
                || attributes.Magic.Current < 0
                || attributes.Dexterity.Base < 0
                || attributes.Dexterity.Current < 0
                || attributes.Vitality.Base < 0
                || attributes.Vitality.Current < 0) {
                throw new InvalidDataException(
                    "Player template contains invalid attribute state.");
            }
        }

        static void ValidateItemStates<T>(
            IReadOnlyList<T>? values,
            Func<T, AuthoritativeItemState?> stateSelector,
            string category)
        {
            if (values is not null
                && values.Any(item => item is null
                    || !HasValidItemDimensions(stateSelector(item)))) {
                throw new InvalidDataException(
                    $"Player template contains an invalid {category} item state.");
            }
        }

        static void ValidateHeldItem(OwnedStoreItem? item)
        {
            if (item is not null && !HasValidItemDimensions(item.State)) {
                throw new InvalidDataException(
                    "Player template contains an invalid held-item state.");
            }
        }

        static void ValidateSlotRange<T>(
            IReadOnlyList<T>? values,
            Func<T, uint> slotSelector,
            uint slotCount,
            string category)
        {
            if (values is not null && values.Any(item => slotSelector(item) >= slotCount)) {
                throw new InvalidDataException(
                    $"Player template contains an invalid {category} slot.");
            }
        }

        static void ValidateInventoryFootprints(
            IReadOnlyList<int>? grid,
            IReadOnlyList<OwnedStoreItem>? inventory)
        {
            if (grid is null || inventory is null)
                return;
            for (var inventoryIndex = 0; inventoryIndex < inventory.Count; inventoryIndex++) {
                var anchor = -1;
                for (var cell = 0; cell < grid.Count; cell++) {
                    if (grid[cell] == inventoryIndex) {
                        anchor = cell;
                        break;
                    }
                }
                if (anchor >= 0
                    && !InventoryGridLayout.HasExactFootprint(
                        grid,
                        anchor,
                        inventoryIndex,
                        inventory[inventoryIndex].State.InventoryWidth,
                        inventory[inventoryIndex].State.InventoryHeight)) {
                    throw new InvalidDataException(
                        "Player template contains an invalid inventory footprint.");
                }
            }
        }

        static bool HasValidItemDimensions(AuthoritativeItemState? state)
        {
            return state is not null
                && InventoryGridLayout.HasValidDimensions(
                    state.InventoryWidth,
                    state.InventoryHeight);
        }

        static void ValidateInventoryGrid(
            IReadOnlyList<int>? grid,
            IReadOnlyList<OwnedStoreItem>? inventory)
        {
            if (grid is not null
                && inventory is not null
                && grid.Any(cell => cell < -1 || cell >= inventory.Count))
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

        static void ValidateItemIdentities<T>(
            IReadOnlyList<T>? values,
            Func<T, uint> seedSelector,
            Func<T, AuthoritativeItemState?> stateSelector,
            ISet<(uint Seed, uint CreateInfo, int ItemIndex)> identities)
        {
            if (values is null)
                return;
            foreach (var item in values) {
                if (item is null || !TryAddItemIdentity(item, seedSelector, stateSelector, identities))
                    throw new InvalidDataException(
                        "Player template contains a duplicate item identity.");
            }
        }

        static void ValidateItemIdentity(
            OwnedStoreItem? item,
            ISet<(uint Seed, uint CreateInfo, int ItemIndex)> identities)
        {
            if (item is not null
                && !TryAddItemIdentity(item, value => value.ItemSeed, value => value.State, identities)) {
                throw new InvalidDataException(
                    "Player template contains a duplicate item identity.");
            }
        }

        static bool TryAddItemIdentity<T>(
            T item,
            Func<T, uint> seedSelector,
            Func<T, AuthoritativeItemState?> stateSelector,
            ISet<(uint Seed, uint CreateInfo, int ItemIndex)> identities)
        {
            var state = stateSelector(item);
            return state is not null
                && identities.Add((seedSelector(item), state.CreateInfo, state.ItemIndex));
        }

        static void ValidateCollectionItemMetadata<T>(
            IReadOnlyList<T>? values,
            Func<T, uint> seedSelector,
            Func<T, AuthoritativeItemState?> stateSelector,
            AuthoritativeItemCatalog? items)
        {
            if (values is null)
                return;
            foreach (var item in values)
                ValidateItemIdentityMetadata(seedSelector(item), stateSelector(item), items);
        }

        static void ValidateOwnedItemMetadata(
            OwnedStoreItem? item,
            Func<OwnedStoreItem, uint> seedSelector,
            Func<OwnedStoreItem, AuthoritativeItemState?> stateSelector,
            AuthoritativeItemCatalog? items)
        {
            if (item is not null)
                ValidateItemIdentityMetadata(seedSelector(item), stateSelector(item), items);
        }

        static void ValidateItemIdentityMetadata(
            uint itemSeed,
            AuthoritativeItemState? state,
            AuthoritativeItemCatalog? items)
        {
            if (state is null
                || (state.StableItemId != 0
                    && items is not null
                    && !items.TryGet(state.StableItemId, out _))
                || !HasValidItemIdentity(itemSeed, state)) {
                throw new InvalidDataException(
                    "Player template contains invalid item identity metadata.");
            }
        }

        static bool HasValidItemIdentity(
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

    public uint Gold { get; }
    public bool GoldTopologyEnabled { get; }
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
            stashGold: StashGold,
            goldTopologyEnabled: GoldTopologyEnabled);
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
            stashGold: StashGold,
            goldTopologyEnabled: GoldTopologyEnabled);
        player.RecalculateEquipmentAttributes();
        player.RecalculateEquipmentArmor();
        return player;
    }
}

public sealed partial class DiabloSimulation
{
    private int cohortCandidateOnly;
    private int ordinaryRuntimeCount;
    private int cohortRuntimeBound;
    private readonly object cohortBindingGate = new();

    private static readonly ConditionalWeakTable<
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>,
        DiabloSimulation> RuntimeOwners = new();

    /** Creates an Engine-owned runtime over this module-owned simulation. */
    public AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>
        CreateRuntime(DiabloPlayerTemplate playerTemplate)
    {
        ArgumentNullException.ThrowIfNull(playerTemplate);
        if (System.Threading.Volatile.Read(ref cohortCandidateOnly) != 0)
            throw new InvalidOperationException(
                "A restored Diablo cohort must bind through its candidate runtime exactly once.");
        lock (cohortBindingGate) {
            System.Threading.Interlocked.Increment(ref ordinaryRuntimeCount);
            try {
                var runtime = new AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>(
                    new Lifecycle(this, playerTemplate));
                RuntimeOwners.Add(runtime, this);
                return runtime;
            } catch {
                System.Threading.Interlocked.Decrement(ref ordinaryRuntimeCount);
                throw;
            }
        }
    }

    /**
     * Creates the runtime for an already restored cohort.  Unlike ordinary
     * startup, this path does not validate or materialize a player template;
     * the candidate roster and exact tick are installed in one engine barrier.
     */
    internal AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>
        CreateCohortRuntime(
            ulong tick,
        IReadOnlyList<SimulationSession<AuthoritativePlayerState>> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Any(participant => participant is null || participant.State is null))
            throw new InvalidDataException("A cohort runtime contains a null participant.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in participants) {
            if (string.IsNullOrWhiteSpace(participant.Id) || !ids.Add(participant.Id))
                throw new InvalidDataException("A cohort runtime contains duplicate participant IDs.");
        }
        lock (cohortBindingGate) {
            if (System.Threading.Volatile.Read(ref cohortCandidateOnly) == 0
                || System.Threading.Interlocked.CompareExchange(ref cohortRuntimeBound, 1, 0) != 0)
                throw new InvalidOperationException(
                    "A Diablo cohort candidate can only bind one restored runtime.");
            try {
                var runtime = new AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent>(
                    new CohortLifecycle(this));
                runtime.InitializeCohort(tick, participants);
                RuntimeOwners.Add(runtime, this);
                return runtime;
            } catch {
                System.Threading.Interlocked.Exchange(ref cohortRuntimeBound, 0);
                throw;
            }
        }
    }

    internal void MarkCohortCandidate()
        => System.Threading.Interlocked.Exchange(ref cohortCandidateOnly, 1);

    internal void RequireSingleCohortRuntimeOwner()
    {
        var count = System.Threading.Volatile.Read(ref cohortCandidateOnly) != 0
            ? System.Threading.Volatile.Read(ref cohortRuntimeBound)
            : System.Threading.Volatile.Read(ref ordinaryRuntimeCount);
        if (count != 1)
            throw new InvalidOperationException(
                "A Diablo cohort capture requires one synchronized runtime owner.");
    }

    internal void RequireOwnedRuntime(
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!RuntimeOwners.TryGetValue(runtime, out var owner)
            || !ReferenceEquals(owner, this))
            throw new InvalidOperationException(
                "The supplied runtime is not owned by this Diablo simulation.");
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

    private sealed class CohortLifecycle
        : IAuthoritativeSimulationLifecycle<AuthoritativePlayerState, DiabloGameplayEvent>
    {
        private readonly DiabloSimulation simulation;

        public CohortLifecycle(DiabloSimulation simulation)
        {
            this.simulation = simulation;
        }

        public AuthoritativePlayerState CreateParticipant(string sessionId)
            => throw new InvalidOperationException(
                "A restored cohort cannot create an implicit participant.");

        public void AdvanceParticipant(AuthoritativePlayerState state, ulong tick)
            => simulation.AdvanceStatusEffects(state, tick);

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
