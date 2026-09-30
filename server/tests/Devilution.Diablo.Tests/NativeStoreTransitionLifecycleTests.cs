using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeStoreTransitionLifecycleTests
{
    [Fact]
    public void AcceptedPortalClearsNativeStoreBeforeAStalePurchaseCanMutateState()
    {
        var catalog = CreateSmithCatalog();
        var random = new DiabloRandom(0x12345678);
        var store = CreateStoreProcessor(catalog, random);
        var portal = new AuthoritativePortal(5, 0, 10, 10, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var world = CreateWorldProcessor(
            CreateWorld(0, 1),
            store,
            [portal]);
        var player = CreatePlayer(10, 10);

        Assert.True(store.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var beforeGold = player.Gold;
        var beforeStock = catalog.GetItems(1).ToArray();
        var beforeRandom = random.State;

        var transition = world.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [player]);

        Assert.True(transition.Succeeded, transition.Failure?.ToString());
        Assert.Equal(1U, player.LevelId);
        Assert.Null(player.ActiveStoreId);

        var stalePurchase = store.Execute(player, new DiabloPurchaseIntent(1, 0), 2);

        Assert.False(stalePurchase.Succeeded);
        Assert.Equal(beforeGold, player.Gold);
        Assert.Equal(beforeStock, catalog.GetItems(1));
        Assert.Equal(beforeRandom, random.State);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void AcceptedExplicitWarpClearsNativeStoreSession()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var authoritativeWorld = CreateWorld(1, 2);
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            1001,
            1,
            4,
            4,
            2,
            2,
            2));
        var world = CreateWorldProcessor(authoritativeWorld, store);
        var player = CreatePlayer(4, 4, levelId: 1);
        player.ActiveStoreId = 1;

        var result = world.ExecuteWarp(player, 1, [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(2U, player.LevelId);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void AcceptedAutomaticLevelTriggerClearsNativeStoreSession()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var authoritativeWorld = CreateWorld(0, 1);
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            1001,
            0,
            10,
            10,
            1,
            2,
            2));
        var world = CreateWorldProcessor(authoritativeWorld, store);
        var player = CreatePlayer(10, 10);
        player.ActiveStoreId = 1;

        var result = world.ExecuteLevelTrigger(player, 1, [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(1U, player.LevelId);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void RejectedOrDryRunTransitionPreservesNativeStoreSession()
    {
        var catalog = CreateSmithCatalog();
        var random = new DiabloRandom(0x2468ACE0);
        var store = CreateStoreProcessor(catalog, random);
        var portal = new AuthoritativePortal(5, 0, 10, 10, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var authoritativeWorld = CreateWorld(0, 1, 2);
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            1001,
            1,
            4,
            4,
            2,
            2,
            2));
        var world = CreateWorldProcessor(authoritativeWorld, store, [portal]);

        var wrongEndpoint = CreatePlayer(9, 10);
        wrongEndpoint.ActiveStoreId = 1;
        var beforeGold = wrongEndpoint.Gold;
        var beforeStock = catalog.GetItems(1).ToArray();
        var beforeRandom = random.State;
        var rejected = world.Execute(
            wrongEndpoint,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [wrongEndpoint]);
        Assert.False(rejected.Succeeded);
        Assert.Equal(1U, wrongEndpoint.ActiveStoreId);
        Assert.Equal(beforeGold, wrongEndpoint.Gold);
        Assert.Equal(beforeStock, catalog.GetItems(1));
        Assert.Equal(beforeRandom, random.State);

        var blockedDestination = CreatePlayer(10, 10, entityId: 3);
        blockedDestination.ActiveStoreId = 1;
        var blocker = CreatePlayer(2, 2, levelId: 1, entityId: 4);
        var blocked = world.Execute(
            blockedDestination,
            new DiabloUsePortalIntent(portal.PortalId),
            2,
            [blockedDestination, blocker]);
        Assert.False(blocked.Succeeded);
        Assert.Equal(1U, blockedDestination.ActiveStoreId);
        Assert.Equal(beforeGold, blockedDestination.Gold);
        Assert.Equal(beforeStock, catalog.GetItems(1));
        Assert.Equal(beforeRandom, random.State);

        var dryRun = CreatePlayer(4, 4, levelId: 1);
        dryRun.ActiveStoreId = 1;
        var preview = world.ExecuteWarp(dryRun, 2, [dryRun], applyEffects: false);
        Assert.True(preview.Succeeded);
        Assert.Equal(1U, dryRun.LevelId);
        Assert.Equal(1U, dryRun.ActiveStoreId);
    }

    [Fact]
    public void IntoTownAndSameLevelMovementDoNotClearTheNativeStoreSession()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var townReturn = new AuthoritativePortal(5, 1, 2, 2, 0, 10, 10) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var authoritativeWorld = CreateWorld(0, 1);
        var world = CreateWorldProcessor(authoritativeWorld, store, [townReturn]);

        var returning = CreatePlayer(2, 2, levelId: 1);
        returning.ActiveStoreId = 1;
        var returnResult = world.Execute(
            returning,
            new DiabloUsePortalIntent(townReturn.PortalId),
            1,
            [returning]);
        Assert.True(returnResult.Succeeded, returnResult.Failure?.ToString());
        Assert.Equal(0U, returning.LevelId);
        Assert.Equal(1U, returning.ActiveStoreId);

        var sameLevel = CreatePlayer(10, 10);
        sameLevel.ActiveStoreId = 1;
        var moveResult = world.Execute(
            sameLevel,
            new DiabloMoveIntent(1, 0),
            2,
            [sameLevel]);
        Assert.True(moveResult.Succeeded, moveResult.Failure?.ToString());
        Assert.Equal(1U, sameLevel.ActiveStoreId);
    }

    [Fact]
    public void GenericAndMetadataFreeStoresRemainCompatibleAcrossTransitions()
    {
        var catalog = CreateSmithCatalog();
        catalog.AddStore(99, [], AuthoritativeStoreRole.Generic);
        var store = CreateStoreProcessor(catalog);
        var portal = new AuthoritativePortal(5, 0, 20, 20, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var world = CreateWorldProcessor(CreateWorld(0, 1), store, [portal]);

        var generic = CreatePlayer(20, 20);
        Assert.True(store.Execute(generic, new DiabloOpenStoreIntent(99), 0).Succeeded);
        Assert.True(world.Execute(
            generic,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [generic]).Succeeded);
        Assert.Equal(99U, generic.ActiveStoreId);

        var legacyStore = new DiabloStoreCommandProcessor(catalog);
        var legacyWorld = CreateWorldProcessor(
            CreateWorld(0, 1),
            legacyStore,
            [portal with { PortalId = 6 }]);
        var metadataFree = CreatePlayer(20, 20);
        Assert.True(legacyStore.Execute(
            metadataFree,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        Assert.True(legacyWorld.Execute(
            metadataFree,
            new DiabloUsePortalIntent(6),
            1,
            [metadataFree]).Succeeded);
        Assert.Equal(1U, metadataFree.ActiveStoreId);
    }

    [Fact]
    public void StashStateAndItemsAreNotTouchedByNativeStoreTransitionCleanup()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var portal = new AuthoritativePortal(5, 0, 43, 66, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var world = CreateWorldProcessor(CreateWorld(0, 1), store, [portal]);
        var player = CreatePlayer(43, 66, gold: 100);
        player.StashGold = 25;
        player.Stash.Add(new StashStoreItem(
            0,
            0,
            7,
            0,
            42,
            10,
            0,
            AuthoritativeItemState.Empty));
        Assert.True(store.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);
        var beforeStash = player.Stash.ToArray();

        var result = world.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(player.StashOpen);
        Assert.Equal(25U, player.StashGold);
        Assert.Equal(beforeStash, player.Stash);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void TransitionCleanupOnlyTouchesTheTransitioningPlayer()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var portal = new AuthoritativePortal(5, 0, 10, 10, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var world = CreateWorldProcessor(CreateWorld(0, 1), store, [portal]);
        var player = CreatePlayer(10, 10, entityId: 1);
        var other = CreatePlayer(8, 8, entityId: 2);
        player.ActiveStoreId = 1;
        other.ActiveStoreId = 1;

        var result = world.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [player, other]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Null(player.ActiveStoreId);
        Assert.Equal(1U, other.ActiveStoreId);
    }

    [Theory]
    [InlineData(73U, 20, 20)]
    [InlineData(74U, 30, 30)]
    public void ConfiguredReservedServiceIdsUseNativeRoleMapping(
        uint storeId,
        int positionX,
        int positionY)
    {
        var store = CreateStoreProcessor(
            new StoreCatalog(),
            adriaStoreId: 73,
            storytellerStoreId: 74,
            townLevelId: 7,
            greetingLevelId: 7);
        var player = CreatePlayer(positionX, positionY, levelId: 7);
        Assert.True(store.Execute(player, new DiabloOpenStoreIntent(storeId), 0).Succeeded);

        store.InvalidateNativeStoreAfterCrossLevelTransition(player, 7, 8);

        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void SameLevelWarpAndPortalPreserveNativeStoreSession()
    {
        var catalog = CreateSmithCatalog();
        var store = CreateStoreProcessor(catalog);
        var authoritativeWorld = CreateWorld(1);
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            1001,
            1,
            4,
            4,
            1,
            6,
            6));
        var portal = new AuthoritativePortal(5, 1, 8, 8, 1, 9, 9) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var world = CreateWorldProcessor(authoritativeWorld, store, [portal]);

        var warpPlayer = CreatePlayer(4, 4, levelId: 1);
        warpPlayer.ActiveStoreId = 1;
        var warp = world.ExecuteWarp(warpPlayer, 1, [warpPlayer]);
        Assert.True(warp.Succeeded, warp.Failure?.ToString());
        Assert.Equal(1U, warpPlayer.LevelId);
        Assert.Equal(1U, warpPlayer.ActiveStoreId);

        var portalPlayer = CreatePlayer(8, 8, levelId: 1);
        portalPlayer.ActiveStoreId = 1;
        var portalResult = world.Execute(
            portalPlayer,
            new DiabloUsePortalIntent(portal.PortalId),
            2,
            [portalPlayer]);
        Assert.True(portalResult.Succeeded, portalResult.Failure?.ToString());
        Assert.Equal(1U, portalPlayer.LevelId);
        Assert.Equal(1U, portalPlayer.ActiveStoreId);
    }

    [Fact]
    public void SimulationFactoryBindsTransitionCleanupToStoreCommands()
    {
        var catalog = CreateSmithCatalog();
        var world = CreateWorld(0, 1);
        var portal = new AuthoritativePortal(5, 0, 10, 10, 1, 2, 2) {
            OwnerEntityId = 1,
            Bidirectional = true,
            RemainingTicks = 1,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(catalog, DiabloModule.Instance.Rules) {
                World = world,
                Portals = [portal],
                TownerGreetings = CreateGreetings(),
            });
        var player = CreatePlayer(10, 10);

        Assert.True(simulation.Execute(
            player,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        Assert.True(simulation.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [player]).Succeeded);

        Assert.Null(player.ActiveStoreId);
    }

    private static DiabloWorldCommandProcessor CreateWorldProcessor(
        AuthoritativeWorld authoritativeWorld,
        DiabloStoreCommandProcessor store,
        IReadOnlyList<AuthoritativePortal>? portals = null)
    {
        var processor = new DiabloWorldCommandProcessor(
            new DiabloWorldState(authoritativeWorld, portals: portals));
        processor.SetNativeStoreTransitionInvalidator(
            store.InvalidateNativeStoreAfterCrossLevelTransition);
        return processor;
    }

    private static StoreCatalog CreateSmithCatalog()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(
            1,
            [new StoreItem(
                0,
                42,
                75,
                AuthoritativeItemState.Empty with {
                    InventoryWidth = 1,
                    InventoryHeight = 1,
                })],
            AuthoritativeStoreRole.Smith);
        return catalog;
    }

    private static DiabloStoreCommandProcessor CreateStoreProcessor(
        StoreCatalog catalog,
        DiabloRandom? random = null,
        uint adriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId,
        uint storytellerStoreId = DiabloStoreCommandProcessor.StorytellerStoreId,
        uint townLevelId = AuthoritativePortalRules.TownLevelId,
        uint greetingLevelId = AuthoritativePortalRules.TownLevelId)
        => new(
            catalog,
            random: random,
            adriaStoreId: adriaStoreId,
            storytellerStoreId: storytellerStoreId,
            townLevelId: townLevelId,
            townerGreetings: CreateGreetings(greetingLevelId));

    private static AuthoritativeWorld CreateWorld(params uint[] levelIds)
    {
        var world = new AuthoritativeWorld();
        foreach (var levelId in levelIds)
            world.AddLevel(new AuthoritativeLevel(levelId, 80, 80));
        return world;
    }

    private static AuthoritativeTownerGreetingCatalog CreateGreetings(uint levelId = 0)
        => new([
            new AuthoritativeTownerGreetingDefinition(0, levelId, 10, 10, 1),
            new AuthoritativeTownerGreetingDefinition(1, levelId, 40, 40, 2),
            new AuthoritativeTownerGreetingDefinition(4, levelId, 30, 30, 3),
            new AuthoritativeTownerGreetingDefinition(6, levelId, 20, 20, 4),
            new AuthoritativeTownerGreetingDefinition(7, levelId, 43, 66, 5),
            new AuthoritativeTownerGreetingDefinition(8, levelId, 50, 50, 6),
        ]);

    private static AuthoritativePlayerState CreatePlayer(
        int positionX,
        int positionY,
        uint levelId = 0,
        uint gold = 100,
        uint entityId = 1)
    {
        var player = new AuthoritativePlayerState(
            gold,
            0,
            640,
            0,
            640,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            positionX,
            positionY,
            640,
            1,
            levelId,
            0);
        player.EntityId = entityId;
        return player;
    }
}
