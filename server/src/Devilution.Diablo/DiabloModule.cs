using Devilution.Engine;

namespace Devilution.Diablo;

/** First-party gameplay module and its engine-facing capability registration. */
public sealed class DiabloModule : IGameplayModule
{
    public static DiabloModule Instance { get; } = new();
    private readonly DiabloStoreRules rules;
    private readonly DiabloContentBootstrap contentBootstrap;

    public DiabloModule(StoreServicePricing? pricing = null)
        : this(AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1, pricing)
    {
    }

    public DiabloModule(
        AuthoritativeQuestPoolSelectionOrder questPoolSelectionOrder,
        StoreServicePricing? pricing)
    {
        if (!Enum.IsDefined(typeof(AuthoritativeQuestPoolSelectionOrder), questPoolSelectionOrder))
            throw new ArgumentOutOfRangeException(nameof(questPoolSelectionOrder));

        QuestPoolSelectionOrder = questPoolSelectionOrder;
        rules = new DiabloStoreRules(pricing);
        contentBootstrap = new DiabloContentBootstrap(
            DiabloSimulationFactory.Instance,
            questPoolSelectionOrder);
        var version = questPoolSelectionOrder == AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1
            ? "0.2.0"
            : "0.1.0";
        // Quest selection changes generated state, so the opt-in policy has a distinct identity.
        Identity = new GameplayModuleIdentity(
            "devilution.diablo",
            version,
            GameplayModuleIdentity.BuiltInAssemblyHash("devilution.diablo", version),
            [],
            new GameplayModuleCompatibility(
                GameplayEngineApiVersion.Current,
                GameplayEngineApiVersion.Current));
    }

    public AuthoritativeQuestPoolSelectionOrder QuestPoolSelectionOrder { get; }

    public StoreServicePricing Pricing => rules.Pricing;

    public IDiabloStoreRules Rules => rules;

    public IDiabloContentBootstrap ContentBootstrap => contentBootstrap;

    public IDiabloNativeGeneratedContentBootstrap NativeGeneratedContentBootstrap => contentBootstrap;

    public IDiabloCathedralDepthContentBootstrap CathedralDepthContentBootstrap => contentBootstrap;

    public IDiabloCathedralDoorContentBootstrap CathedralDoorContentBootstrap => contentBootstrap;

    public IDiabloCathedralNativePopulationBootstrap CathedralNativePopulationContentBootstrap => contentBootstrap;

    public IDiabloCathedralNativeProgressionBootstrap CathedralNativeProgressionContentBootstrap => contentBootstrap;

    public IDiabloCathedralNativeDepthProgressionBootstrap CathedralNativeDepthProgressionContentBootstrap => contentBootstrap;

    public GameplayModuleIdentity Identity { get; }

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        registry.RegisterCapability<IDiabloStoreRules>(rules);
        registry.RegisterCapability<IDiabloSimulationFactory>(DiabloSimulationFactory.Instance);
        registry.RegisterCapability<IDiabloContentBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloNativeGeneratedContentBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloCathedralDepthContentBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloCathedralDoorContentBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloCathedralNativePopulationBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloCathedralNativeProgressionBootstrap>(contentBootstrap);
        registry.RegisterCapability<IDiabloCathedralNativeDepthProgressionBootstrap>(contentBootstrap);
    }
}
