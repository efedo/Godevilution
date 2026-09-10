using Devilution.Engine;

namespace Devilution.Diablo;

/** First-party gameplay module and its engine-facing capability registration. */
public sealed class DiabloModule : IGameplayModule
{
    public static DiabloModule Instance { get; } = new();
    private readonly DiabloStoreRules rules;
    private readonly IDiabloContentBootstrap contentBootstrap;

    public DiabloModule(StoreServicePricing? pricing = null)
    {
        rules = new DiabloStoreRules(pricing);
        contentBootstrap = new DiabloContentBootstrap(DiabloSimulationFactory.Instance);
    }

    public StoreServicePricing Pricing => rules.Pricing;

    public IDiabloStoreRules Rules => rules;

    public IDiabloContentBootstrap ContentBootstrap => contentBootstrap;

    public GameplayModuleIdentity Identity { get; } = new(
        "devilution.diablo",
        "0.1.0",
        GameplayModuleIdentity.BuiltInAssemblyHash("devilution.diablo", "0.1.0"),
        [],
        new GameplayModuleCompatibility(
            GameplayEngineApiVersion.Current,
            GameplayEngineApiVersion.Current));

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        registry.RegisterCapability<IDiabloStoreRules>(rules);
        registry.RegisterCapability<IDiabloSimulationFactory>(DiabloSimulationFactory.Instance);
        registry.RegisterCapability<IDiabloContentBootstrap>(contentBootstrap);
    }
}
