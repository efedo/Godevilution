namespace Devilution.Diablo;

/** Native-compatible game mode selected for one authoritative simulation. */
public enum DiabloGameMode
{
    SinglePlayer,
    Multiplayer,
}

/** Construction data for one protocol-independent Diablo simulation. */
public sealed class DiabloSimulationOptions
{
    public DiabloSimulationOptions(StoreCatalog storeCatalog, IDiabloStoreRules storeRules)
    {
        StoreCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        StoreRules = storeRules ?? throw new ArgumentNullException(nameof(storeRules));
    }

    public StoreCatalog StoreCatalog { get; }

    public IDiabloStoreRules StoreRules { get; }

    public AuthoritativeWorld? World { get; init; }

    public int FallbackWidth { get; init; } = 40;

    public int FallbackHeight { get; init; } = 40;

    public IEnumerable<int>? FallbackBlockedCells { get; init; }

    public IEnumerable<int>? FallbackMissileBlockedCells { get; init; }

    public IReadOnlyList<AuthoritativeCombatTarget>? CombatTargets { get; init; }

    public IReadOnlyList<AuthoritativePortal>? Portals { get; init; }

    public IReadOnlyList<AuthoritativeWorldItem>? WorldItems { get; init; }

    public IReadOnlyList<AuthoritativeWorldObject>? Objects { get; init; }

    public IReadOnlyList<AuthoritativeQuestState>? Quests { get; init; }

    public IReadOnlyList<AuthoritativeProjectile>? Projectiles { get; init; }

    public AuthoritativeSpellCatalog? Spells { get; init; }

    public AuthoritativeCombatRules? CombatRules { get; init; }

    public AuthoritativeItemCatalog? Items { get; init; }

    /** Stable spell identity authorizing the Bard's zero-cost Identify action. */
    public uint IdentifyAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Warrior's native Repair action. */
    public uint RepairAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Sorcerer's native Staff Recharge action. */
    public uint RechargeAbilitySpellId { get; init; }

    /** Stable spell identity authorizing the Rogue's native Trap Disarm action. */
    public uint TrapDisarmAbilitySpellId { get; init; }

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; init; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; init; }

    /** Enables deferred native animation-frame effects during the migration. */
    public bool UseNativePlayerActionTiming { get; init; }

    /** Server-owned initial state for the native-compatible gameplay RNG. */
    public uint RandomSeed { get; init; }

    /** Explicit session mode; it must not be inferred from connected participants. */
    public DiabloGameMode GameMode { get; init; } = DiabloGameMode.SinglePlayer;
}

/** Engine capability that constructs a configured first-party Diablo simulation. */
public interface IDiabloSimulationFactory
{
    DiabloSimulation Create(DiabloSimulationOptions options);
}

/** Default first-party simulation factory registered by the Diablo module. */
public sealed class DiabloSimulationFactory : IDiabloSimulationFactory
{
    public static DiabloSimulationFactory Instance { get; } = new();

    private DiabloSimulationFactory()
    {
    }

    public DiabloSimulation Create(DiabloSimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var worldState = new DiabloWorldState(
            options.World,
            options.FallbackWidth,
            options.FallbackHeight,
            options.FallbackBlockedCells,
            options.CombatTargets,
            options.Portals,
            options.WorldItems,
            options.Objects,
            options.Quests,
            options.Projectiles,
            options.FallbackMissileBlockedCells);
        var spells = options.Spells ?? new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0),
            new AuthoritativeSpellDefinition(2, 3, 0, 1, 10, 1),
        ]);
        var combatRules = options.CombatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        if (options.Items is not null)
            combatRules.ValidateItems(options.Items);
        var random = new DiabloRandom(options.RandomSeed);
        var playerDamage = new AuthoritativePlayerDamageResolver(spells);
        var worldCommands = new DiabloWorldCommandProcessor(
            worldState,
            options.PlayerAnimations,
            options.UseNativePlayerActionTiming,
            combatRules,
            random,
            playerDamage,
            options.TrapDisarmAbilitySpellId);
        return new DiabloSimulation(
            options.StoreCatalog,
            worldState,
            new DiabloStoreCommandProcessor(
                options.StoreCatalog,
                options.StoreRules,
                combatRules,
                random,
                options.IdentifyAbilitySpellId,
                options.RepairAbilitySpellId,
                options.RechargeAbilitySpellId,
                spells),
            worldCommands,
            new DiabloCombatCommandProcessor(
                worldState,
                spells,
                combatRules,
                random,
                options.PlayerClasses,
                options.PlayerAnimations,
                options.UseNativePlayerActionTiming,
                options.GameMode,
                options.Items,
                playerDamage,
                worldCommands),
            random,
            combatRules,
            options.PlayerClasses,
            options.PlayerAnimations);
    }
}

/**
 * Protocol-independent aggregate composing the current Diablo state and all
 * authoritative gameplay transition processors.
 */
public sealed partial class DiabloSimulation
{
    private readonly StoreCatalog storeCatalog;
    private readonly DiabloStoreCommandProcessor storeCommands;
    private readonly DiabloWorldCommandProcessor worldCommands;
    private readonly DiabloCombatCommandProcessor combatCommands;
    private readonly DiabloRandom random;

    internal DiabloSimulation(
        StoreCatalog storeCatalog,
        DiabloWorldState worldState,
        DiabloStoreCommandProcessor storeCommands,
        DiabloWorldCommandProcessor worldCommands,
        DiabloCombatCommandProcessor combatCommands,
        DiabloRandom random,
        AuthoritativeCombatRules combatRules,
        AuthoritativePlayerClassCatalog? playerClasses,
        AuthoritativePlayerAnimationCatalog? playerAnimations)
    {
        this.storeCatalog = storeCatalog ?? throw new ArgumentNullException(nameof(storeCatalog));
        WorldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
        this.storeCommands = storeCommands ?? throw new ArgumentNullException(nameof(storeCommands));
        this.worldCommands = worldCommands ?? throw new ArgumentNullException(nameof(worldCommands));
        this.combatCommands = combatCommands ?? throw new ArgumentNullException(nameof(combatCommands));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        CombatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
        PlayerClasses = playerClasses;
        PlayerAnimations = playerAnimations;
    }

    public DiabloWorldState WorldState { get; }

    public AuthoritativePlayerClassCatalog? PlayerClasses { get; }

    public AuthoritativePlayerAnimationCatalog? PlayerAnimations { get; }

    /** Validated content rules governing this Diablo simulation instance. */
    public AuthoritativeCombatRules CombatRules { get; }

    public uint RandomState => random.State;

    public DiabloIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloStoreIntent intent,
        ulong appliedTick)
    {
        return storeCommands.Execute(player, intent, appliedTick);
    }

    public DiabloWorldIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloWorldIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        return worldCommands.Execute(player, intent, appliedTick, players);
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick)
    {
        return combatCommands.Execute(player, intent, appliedTick, [player]);
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return combatCommands.Execute(player, intent, appliedTick, players);
    }

    public void AdvanceStatusEffects(AuthoritativePlayerState player, ulong tick)
    {
        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, tick);
    }

    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTick(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return combatCommands.AdvanceTick(tick, players);
    }
}
