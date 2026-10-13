using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Google.Protobuf;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreServiceCapabilitiesProjectionTests
{
    [Fact]
    public void OptionalHintsPreserveLegacyStoreWireAndStateHashAndAreNotHydrated()
    {
        var legacy = DiabloProtocolMapper.ToProtocolSnapshot(CreateSource(null));
        var allFalse = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateSource(new DiabloStoreServices(false, false, false, false)));
        var quote = new DiabloStoreServiceQuote(123, DiabloRuleFailure.InsufficientResources);
        var sourceQuotes = new DiabloStoreQuotes(
            [new DiabloStoreItemServiceQuotes(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Equipment,
                    2,
                    StableItemId: 44),
                quote,
                null,
                new DiabloStoreServiceQuote(0, null))],
            new DiabloStoreServiceQuote(10, null));
        var available = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateSource(new DiabloStoreServices(true, false, true, false), sourceQuotes));

        Assert.Null(legacy.ActiveStore.Services);
        Assert.Null(legacy.ActiveStore.Quotes);
        Assert.Equal(new byte[] { 0x08, 0x0C }, legacy.ActiveStore.ToByteArray());
        Assert.NotNull(allFalse.ActiveStore.Services);
        Assert.Equal(
            new byte[] { 0x08, 0x0C, 0x1A, 0x00 },
            allFalse.ActiveStore.ToByteArray());

        var roundTrippedStore = StoreSnapshot.Parser.ParseFrom(available.ActiveStore.ToByteArray());
        Assert.NotNull(roundTrippedStore.Services);
        Assert.True(roundTrippedStore.Services.Repair);
        Assert.False(roundTrippedStore.Services.Recharge);
        Assert.True(roundTrippedStore.Services.Identify);
        Assert.False(roundTrippedStore.Services.RefillMana);
        Assert.NotNull(roundTrippedStore.Quotes);
        var projectedItemQuotes = Assert.Single(roundTrippedStore.Quotes.Items);
        Assert.Equal(PlayerItemLocation.Equipment, projectedItemQuotes.Item.Location);
        Assert.Equal(2U, projectedItemQuotes.Item.Slot);
        Assert.Equal(44U, projectedItemQuotes.Item.StableItemId);
        Assert.Equal(123U, projectedItemQuotes.Repair.Price);
        Assert.Equal(
            CommandRejectReason.InsufficientResources,
            projectedItemQuotes.Repair.RejectReason);
        Assert.Null(projectedItemQuotes.Recharge);
        Assert.Equal(0U, projectedItemQuotes.Identify.Price);
        Assert.Equal(
            CommandRejectReason.Unspecified,
            projectedItemQuotes.Identify.RejectReason);
        Assert.Equal(10U, roundTrippedStore.Quotes.RefillMana.Price);

        Assert.Equal(legacy.StateSha256, allFalse.StateSha256);
        Assert.Equal(legacy.StateSha256, available.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(legacy), legacy.StateSha256);

        var imported = DiabloProtocolMapper.FromProtocolSnapshot(available);
        Assert.Null(imported.ActiveStore!.Services);
        Assert.Null(imported.ActiveStore.Quotes);
    }

    [Fact]
    public void DefaultStoreExecutorPublishesQuotesFromItsConcreteRules()
    {
        const uint storeId = 99;
        var catalog = new StoreCatalog();
        catalog.AddStore(storeId, [], AuthoritativeStoreRole.Generic);
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 7,
                Value = 100,
                Durability = 5,
                MaxDurability = 20,
            });
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 1000,
            startingInventory: [item]);
        var server = new AuthoritativeCommandServer(executor);
        var result = server.Process("quote-player", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            OpenStoreRequested = new OpenStoreRequested { StoreId = storeId },
        }, currentTick: 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("quote-player", entityId: 1, tick: 0);
        var store = snapshot.ActiveStore;
        Assert.NotNull(store);
        var itemQuotes = Assert.Single(Assert.IsType<StoreServiceQuotesSnapshot>(store.Quotes).Items);
        Assert.Equal(PlayerItemLocation.Inventory, itemQuotes.Item.Location);
        Assert.Equal(0U, itemQuotes.Item.Slot);
        Assert.Equal(7U, itemQuotes.Item.StableItemId);
        Assert.NotNull(itemQuotes.Repair);
        Assert.True(itemQuotes.Repair.Price > 0);
    }

    [Fact]
    public void ShippedContentHostPublishesLiveQuotesForTheConfiguredSmith()
    {
        const string sessionId = "shipped-smith-quotes";
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content");
        var basePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "base"));
        var adjacentSmithStart = new ContentPack(
            "test.smith-adjacent-start",
            "1.0.0",
            [TsvTable.Parse(
                "level_start_positions.tsv",
                "level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t61\t63\n")],
            dependencies: [basePack.Id]);
        var packs = new List<ContentPack> { basePack, adjacentSmithStart };
        var manifest = new ContentManifest("devilution.base", "0.1.0", packs);
        var registry = new GameplayModuleRegistry();
        var factory = new DiabloAuthoritativeGameHostAdapterFactory();
        registry.Load(factory.CreateBuiltInModules(), new GameplayModuleContext(
            manifest,
            new DelegateAuthoritativeClock(() => 0)));
        var adapter = factory.Activate(
            registry,
            ComposedContent.Compose(manifest),
            new AuthoritativePlayerStartConfiguration(1000, null, null) {
                RandomSeed = 0,
                StartingLevelId = 0,
            });
        adapter.InitializeParticipant(sessionId, new AuthoritativeParticipantSelection(0) {
            CharacterName = sessionId,
        });
        var commands = new AuthoritativeCommandServer(adapter);
        var start = adapter.CreateSnapshot(sessionId, entityId: 1, tick: 0);
        Assert.Equal(0U, start.Players.Single().LevelId);
        Assert.Equal(61, start.Players.Single().PositionX);
        Assert.Equal(63, start.Players.Single().PositionY);

        var opened = commands.Process(sessionId, new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, currentTick: 0);

        Assert.Equal(CommandStatus.Accepted, opened.Status);
        var snapshot = adapter.CreateSnapshot(sessionId, entityId: 1, tick: 0);
        var store = Assert.IsType<StoreSnapshot>(snapshot.ActiveStore);
        Assert.Equal(1U, store.StoreId);
        Assert.True(store.Services.Repair);
        Assert.NotNull(store.Quotes);
        Assert.NotEmpty(store.Quotes.Items);
        Assert.All(store.Quotes.Items, item => {
            Assert.NotNull(item.Repair);
            Assert.Null(item.Recharge);
            Assert.Null(item.Identify);
        });
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static DiabloSimulationSnapshot CreateSource(
        DiabloStoreServices? services,
        DiabloStoreQuotes? quotes = null)
    {
        var player = new StorePlayerSnapshot(
            0,
            12,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        return new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            new DiabloStoreState(12, []) { Services = services, Quotes = quotes });
    }
}
