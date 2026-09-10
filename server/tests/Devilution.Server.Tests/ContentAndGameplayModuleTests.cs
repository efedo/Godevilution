using Devilution.Protocol.V1;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ContentAndGameplayModuleTests
{
    [Fact]
    public void PortalCatalogLoadsStableDefinitionsAndRejectsDuplicateIds()
    {
        var portals = AuthoritativePortalCatalog.LoadTsv(
            "portals.tsv",
            "portal_id\tsource_level_id\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_position_x\tdestination_position_y\n"
                + "7\t1\t2\t3\t2\t4\t5\n");

        var portal = Assert.Single(portals);
        Assert.Equal(7U, portal.PortalId);
        Assert.Equal(2U, portal.DestinationLevelId);
        Assert.Throws<InvalidDataException>(() => AuthoritativePortalCatalog.LoadTsv(
            "portals.tsv",
            "portal_id\tsource_level_id\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_position_x\tdestination_position_y\n"
                + "7\t1\t2\t3\t2\t4\t5\n7\t1\t3\t3\t2\t4\t5\n"));
    }

    [Fact]
    public void WorldObjectAndQuestCatalogsLoadExternalState()
    {
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tactivated\n20\t4\t2\t3\t5\t0\n");
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tturn_in_towner_id\n30\t2\t3\t1\t0\t4\n");

        Assert.Equal(4U, Assert.Single(objects).ObjectId);
        Assert.Equal(1U, Assert.Single(quests).Progress);
        Assert.Equal(4, quests[0].TurnInTownerId);
    }

    [Fact]
    public void WorldObjectCatalogValidatesDeclarativeEffects()
    {
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n20\t4\t2\t3\t5\tHeal\t10\n");

        Assert.Equal(AuthoritativeObjectEffectKind.Heal, Assert.Single(objects).EffectKind);
        Assert.Equal(10, objects[0].EffectAmount);
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n20\t4\t2\t3\t5\tUnknown\t10\n"));
    }

    [Fact]
    public void ObjectPresentationCatalogCarriesAuthoredNativeFamilies()
    {
        var objects = AuthoritativeWorldObjectCatalog.LoadTemplates(
            "objects.tsv",
            "entity_id\tobject_id\ttemplate_only\n"
                + "2026\t89\t1\n"
                + "2027\t90\t1\n");
        var presentations = AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
            "object_presentations.tsv",
            "entity_id\thide_when_activated\tanimation_frame\tactivated_animation_frame\tpresentation_kind\n"
                + "2026\t1\t0\t0\tNativeArmorStand\n"
                + "2027\t1\t0\t0\tNativeWeaponRack\n");

        presentations.Apply(objects);

        Assert.Equal(
            AuthoritativeObjectPresentationKind.NativeArmorStand,
            objects[0].PresentationKind);
        Assert.Equal(
            AuthoritativeObjectPresentationKind.NativeWeaponRack,
            objects[1].PresentationKind);
    }

    [Fact]
    public void ProductionObjectPresentationSchemaRejectsIncompleteRows()
    {
        var table = TsvTable.Parse(
            "object_presentations.tsv",
            "entity_id\thide_when_activated\tanimation_frame\tactivated_animation_frame\tpresentation_kind\n"
                + "2026\t1\t0\t0\t\n");

        Assert.Throws<InvalidDataException>(
            () => AuthoritativeWorldObjectPresentationCatalog.ValidateExternalizedProduction(table));
    }

    [Fact]
    public void StoreServicePricingLoadsOverridesAndSpellStaffCosts()
    {
        var pricing = StoreServicePricing.LoadTsv("store_services.tsv", "key\tspell_id\tvalue\nsale_divisor\t\t5\nspell_staff_cost\t7\t12\n");

        Assert.Equal(5, pricing.SaleDivisor);
        Assert.Equal(12U, pricing.GetSpellStaffCost(7));
        Assert.Equal(0U, pricing.GetSpellStaffCost(8));
        Assert.Equal(StoreServicePricing.Default.ManaChunk, pricing.ManaChunk);
    }

    [Fact]
    public void RulesetIdentityIsUsedAsTheHandshakeContentIdentity()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("items.tsv", "id\n1\n")])]);
        var module = DiabloGameplayModule.Instance;
        var ruleset = new GameplayRulesetIdentity(content, [module.Identity]);

        var identity = ProtocolServerIdentity.FromRuleset("server", "0.1.0", 20, ruleset);

        Assert.Equal(ruleset.CombinedSha256, identity.ContentManifestHash);
    }

    [Fact]
    public void ProtocolV2PublishesIndependentContentAndRulesetIdentities()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("items.tsv", "id\n1\n")])]);
        var module = DiabloGameplayModule.Instance;
        var ruleset = new GameplayRulesetIdentity(content, [module.Identity]);

        var identity = ProtocolServerIdentity.FromRuleset(
            "server",
            ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            20,
            ruleset);

        Assert.Equal(content.Sha256, identity.ContentManifestHash);
        Assert.Equal(ruleset.CombinedSha256, identity.RulesetIdentityHash);
        Assert.NotEqual(identity.ContentManifestHash, identity.RulesetIdentityHash);
    }

    [Fact]
    public void GameplayModulesLoadDependenciesInDeclaredOrder()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        var clock = new Devilution.Engine.DelegateAuthoritativeClock(() => 0);
        var registry = new GameplayModuleRegistry();
        var dependency = new TestGameplayModule(new GameplayModuleIdentity("base.rules", "1", "hash-a"));
        var dependent = new TestGameplayModule(new GameplayModuleIdentity("diablo.rules", "1", "hash-b", ["base.rules"]));

        registry.Load([dependent, dependency], new GameplayModuleContext(content, clock));

        Assert.Equal(["base.rules", "diablo.rules"], registry.Modules.Select(module => module.Identity.Id));
        Assert.Equal(["base.rules", "diablo.rules"], dependent.RegistrationOrder);
    }

    [Fact]
    public void MissingGameplayModuleDependencyIsRejected()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        var registry = new GameplayModuleRegistry();
        var module = new TestGameplayModule(new GameplayModuleIdentity("diablo.rules", "1", "hash", ["missing"]));

        Assert.Throws<GameplayModuleCompatibilityException>(() => registry.Load(
            [module],
            new GameplayModuleContext(
                content,
                new Devilution.Engine.DelegateAuthoritativeClock(() => 0))));
    }

    [Fact]
    public void StoreCatalogLoadsExternalDefinitionsAndDiabloModuleOwnsPurchaseValidation()
    {
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_type\tidentified\n1\t0\t42\t75\t1\t1\n");
        var executor = new StoreSimulationExecutor(catalog, 100, gameplayRules: DiabloGameplayModule.Instance);
        var server = new Commands.AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player", new Command {
            ClientSequence = 1,
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, currentTick: 0).Status);
        var purchase = server.Process("player", new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
        }, currentTick: 1);

        Assert.Equal(CommandStatus.Accepted, purchase.Status);
        var item = Assert.Single(executor.GetPlayerState("player").Inventory);
        Assert.Equal(1, item.State.ItemType);
        Assert.True(item.State.Identified);
    }

    [Fact]
    public void DiabloStoreRulesMatchLegacySaleServiceAndRefillPricing()
    {
        var state = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Magical = 1,
            Identified = true,
            Value = 100,
            IdentifiedValue = 100,
            Charges = 0,
            MaxCharges = 1,
            Durability = 5,
            MaxDurability = 20,
        };
        var item = new OwnedStoreItem(1, 0, 42, 75, 0, state);
        var rules = DiabloGameplayModule.Instance;

        Assert.Equal(25U, rules.GetSalePrice(item));
        Assert.Equal(11U, rules.GetRepairPrice(item));
        Assert.Equal(50U, rules.GetRechargePrice(item));
        Assert.Equal(100U, rules.GetIdentificationPrice(item));
        Assert.Null(rules.ValidateManaRefill(32, 640, 10));
        Assert.Equal(10U, rules.GetManaRefillPrice(32, 640));
    }

    [Fact]
    public void CustomHostStoreRulesRemainSupportedByTheModuleBridge()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 100,
            gameplayRules: new RejectPurchaseRules());
        var server = new Commands.AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player", new Command {
            ClientSequence = 1,
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, 0).Status);
        var result = server.Process("player", new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
        }, 1);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.RejectReason);
        Assert.Single(catalog.GetItems(1));
    }

    private sealed class TestGameplayModule(GameplayModuleIdentity identity) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = identity;

        public List<string> RegistrationOrder { get; } = [];

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
            RegistrationOrder.AddRange(registry.Modules.Select(module => module.Identity.Id));
            RegistrationOrder.Add(Identity.Id);
        }
    }

    private sealed class RejectPurchaseRules : IStoreGameplayRules
    {
        public CommandRejectReason? ValidatePurchase(StoreItem item, uint playerGold)
            => CommandRejectReason.NotAllowed;

        public CommandRejectReason? ValidateSale(OwnedStoreItem item) => throw Unused();

        public CommandRejectReason? ValidateRepair(OwnedStoreItem item, uint playerGold) => throw Unused();

        public CommandRejectReason? ValidateRecharge(OwnedStoreItem item, uint playerGold) => throw Unused();

        public CommandRejectReason? ValidateIdentification(OwnedStoreItem item, uint playerGold) => throw Unused();

        public CommandRejectReason? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold)
            => throw Unused();

        public uint GetSalePrice(OwnedStoreItem item) => throw Unused();

        public uint GetRepairPrice(OwnedStoreItem item) => throw Unused();

        public uint GetRechargePrice(OwnedStoreItem item) => throw Unused();

        public uint GetIdentificationPrice(OwnedStoreItem item) => throw Unused();

        public uint GetManaRefillPrice(int currentMana, int maximumMana) => throw Unused();

        private static InvalidOperationException Unused()
            => new("The test bridge called an unrelated store rule.");
    }
}
