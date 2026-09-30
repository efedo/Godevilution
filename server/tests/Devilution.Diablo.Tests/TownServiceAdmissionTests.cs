using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class TownServiceAdmissionTests
{
    [Fact]
    public void NativeVendorEntryUsesGreetingLevelChebyshevBoundaryAndHeldCursor()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var greetings = CreateGreetings();
        var processor = CreateProcessor(catalog, greetings);

        var diagonal = CreatePlayer(9, 9);
        Assert.True(processor.Execute(diagonal, new DiabloOpenStoreIntent(1), 0).Succeeded);

        var remote = CreatePlayer(8, 9);
        var remoteResult = processor.Execute(remote, new DiabloOpenStoreIntent(1), 0);
        Assert.False(remoteResult.Succeeded);
        Assert.Null(remote.ActiveStoreId);

        var wrongLevel = CreatePlayer(10, 10, levelId: 1);
        var wrongLevelResult = processor.Execute(wrongLevel, new DiabloOpenStoreIntent(1), 0);
        Assert.False(wrongLevelResult.Succeeded);
        Assert.Null(wrongLevel.ActiveStoreId);

        var held = CreatePlayer(
            10,
            10,
            heldItem: new OwnedStoreItem(0, 0, 1, 1, 0));
        var heldResult = processor.Execute(held, new DiabloOpenStoreIntent(1), 0);
        Assert.False(heldResult.Succeeded);
        Assert.Null(held.ActiveStoreId);
    }

    [Fact]
    public void NativeStoreEntryUsesRoleLocationAndPreservesGenericFallback()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        catalog.AddStore(99, [], AuthoritativeStoreRole.Generic);
        var processor = CreateProcessor(catalog, CreateGreetings());

        var wrongNpc = CreatePlayer(20, 20);
        Assert.False(processor.Execute(
            wrongNpc,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);

        var custom = CreatePlayer(-100, -100);
        Assert.True(processor.Execute(
            custom,
            new DiabloOpenStoreIntent(99),
            0).Succeeded);
    }

    [Fact]
    public void NativeStoreRolesUseTheirMappedTownerGreetingsAndMissingGreetingRejects()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        catalog.AddStore(2, [], AuthoritativeStoreRole.Healer);
        catalog.AddStore(3, [], AuthoritativeStoreRole.Witch);
        catalog.AddStore(4, [], AuthoritativeStoreRole.Wirt);
        catalog.AddStore(5, [], AuthoritativeStoreRole.Adria);
        var greetings = CreateGreetings();
        var processor = CreateProcessor(catalog, greetings);

        foreach (var (storeId, positionX, positionY) in new[] {
            (1U, 10, 10),
            (2U, 40, 40),
            (3U, 20, 20),
            (4U, 50, 50),
            (5U, 20, 20),
        }) {
            var player = CreatePlayer(positionX, positionY);
            Assert.True(processor.Execute(
                player,
                new DiabloOpenStoreIntent(storeId),
                0).Succeeded);
        }

        var withoutWirt = new AuthoritativeTownerGreetingCatalog(
            greetings.Definitions.Where(definition => definition.TownerId != 8));
        var missingGreetingProcessor = CreateProcessor(catalog, withoutWirt);
        var missingGreeting = CreatePlayer(50, 50);
        var rejected = missingGreetingProcessor.Execute(
            missingGreeting,
            new DiabloOpenStoreIntent(4),
            0);

        Assert.False(rejected.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, rejected.Failure);
        Assert.Null(missingGreeting.ActiveStoreId);
    }

    [Fact]
    public void ReservedServicesUseConfiguredNativeTowners()
    {
        var processor = CreateProcessor(
            new StoreCatalog(),
            CreateGreetings(),
            adriaStoreId: 73,
            storytellerStoreId: 74);

        var adria = CreatePlayer(20, 20);
        Assert.True(processor.Execute(
            adria,
            new DiabloOpenStoreIntent(73),
            0).Succeeded);

        var storyteller = CreatePlayer(30, 30);
        Assert.True(processor.Execute(
            storyteller,
            new DiabloOpenStoreIntent(74),
            0).Succeeded);

        var defaultAdria = CreatePlayer(20, 20);
        var defaultProcessor = CreateProcessor(new StoreCatalog(), CreateGreetings());
        Assert.False(defaultProcessor.Execute(
            defaultAdria,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.StorytellerStoreId),
            0).Succeeded);
    }

    [Fact]
    public void GillianStashEntryIsBoundedWithoutRevalidatingAnOpenSession()
    {
        var processor = CreateProcessor(new StoreCatalog(), CreateGreetings());
        var player = CreatePlayer(42, 65, gold: 100);

        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);
        player.LevelId = 1;
        var beforeGold = player.Gold;
        var beforeStashGold = player.StashGold;
        var deposit = processor.Execute(
            player,
            new DiabloDepositStashGoldIntent(10),
            1);

        Assert.True(deposit.Succeeded);
        Assert.Equal(beforeGold - 10, player.Gold);
        Assert.Equal(beforeStashGold + 10, player.StashGold);
        Assert.True(player.StashOpen);

        var remote = CreatePlayer(40, 65);
        Assert.False(processor.Execute(
            remote,
            new DiabloOpenStashIntent(),
            0).Succeeded);

        var held = CreatePlayer(
            42,
            66,
            heldItem: new OwnedStoreItem(0, 0, 2, 1, 0));
        Assert.False(processor.Execute(
            held,
            new DiabloOpenStashIntent(),
            0).Succeeded);
    }

    [Fact]
    public void MissingMetadataPreservesDirectStoreAndStashCompatibility()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var processor = new DiabloStoreCommandProcessor(catalog);
        var player = CreatePlayer(-100, -100, heldItem: new OwnedStoreItem(0, 0, 3, 1, 0));

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        Assert.True(processor.Execute(player, new DiabloCloseStoreIntent(), 1).Succeeded);
        player.HeldItem = null;
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 2).Succeeded);
    }

    [Fact]
    public void RejectedMetadataEntryDoesNotHydrateItemDimensions()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                1,
                1,
                1,
                1,
                0,
                0,
                0,
                0,
                0,
                2,
                1,
                true),
        ]);
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 1,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var held = new OwnedStoreItem(0, 0, 4, 1, 0, state);
        var player = CreatePlayer(8, 9, heldItem: held);
        var processor = CreateProcessor(
            catalog,
            CreateGreetings(),
            items: items);

        var rejected = processor.Execute(player, new DiabloOpenStoreIntent(1), 0);

        Assert.False(rejected.Succeeded);
        Assert.Null(player.ActiveStoreId);
        var rejectedHeld = player.HeldItem;
        Assert.NotNull(rejectedHeld);
        Assert.Equal(1, rejectedHeld!.State.InventoryWidth);
        Assert.Equal(1, rejectedHeld.State.InventoryHeight);

        var legacyPlayer = CreatePlayer(8, 9, heldItem: held);
        var legacyProcessor = CreateProcessor(catalog, null, items: items);
        Assert.True(legacyProcessor.Execute(
            legacyPlayer,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        var hydratedHeld = legacyPlayer.HeldItem;
        Assert.NotNull(hydratedHeld);
        Assert.Equal(2, hydratedHeld!.State.InventoryWidth);
    }

    [Fact]
    public void RejectedEntryDoesNotAdvanceRandomOrReplaceExistingSession()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(catalog, CreateGreetings(), random: random);
        var player = CreatePlayer(10, 10);
        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var beforeRandom = random.State;

        player.PositionX = 8;
        var rejected = processor.Execute(player, new DiabloOpenStoreIntent(1), 1);

        Assert.False(rejected.Succeeded);
        Assert.Equal(beforeRandom, random.State);
        Assert.Equal(1U, player.ActiveStoreId);
    }

    private static DiabloStoreCommandProcessor CreateProcessor(
        StoreCatalog catalog,
        AuthoritativeTownerGreetingCatalog? greetings,
        uint adriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId,
        uint storytellerStoreId = DiabloStoreCommandProcessor.StorytellerStoreId,
        DiabloRandom? random = null,
        AuthoritativeItemCatalog? items = null)
    {
        return new DiabloStoreCommandProcessor(
            catalog,
            adriaStoreId: adriaStoreId,
            storytellerStoreId: storytellerStoreId,
            random: random,
            items: items,
            townerGreetings: greetings);
    }

    private static AuthoritativeTownerGreetingCatalog CreateGreetings()
        => new([
            new AuthoritativeTownerGreetingDefinition(0, 0, 10, 10, 1),
            new AuthoritativeTownerGreetingDefinition(1, 0, 40, 40, 2),
            new AuthoritativeTownerGreetingDefinition(4, 0, 30, 30, 3),
            new AuthoritativeTownerGreetingDefinition(6, 0, 20, 20, 4),
            new AuthoritativeTownerGreetingDefinition(7, 0, 43, 66, 5),
            new AuthoritativeTownerGreetingDefinition(8, 0, 50, 50, 6),
        ]);

    private static AuthoritativePlayerState CreatePlayer(
        int positionX,
        int positionY,
        uint levelId = 0,
        uint gold = 100,
        OwnedStoreItem? heldItem = null)
        => new(
            gold,
            0,
            640,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            positionX,
            positionY,
            640,
            1,
            levelId,
            0,
            heldItem: heldItem);
}
