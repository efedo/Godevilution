using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloStoreServiceCapabilitiesTests
{
    [Fact]
    public void AvailabilityUsesConfiguredRolesAndLocationWithoutCheckingAffordability()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        catalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        catalog.AddStore(3, [], AuthoritativeStoreRole.Wirt);
        catalog.AddStore(99, [], AuthoritativeStoreRole.Generic);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            adriaStoreId: 73,
            townLevelId: 7,
            storytellerStoreId: 74);
        var player = CreatePlayer(levelId: 7);

        player.ActiveStoreId = 99;
        Assert.Equal(
            new DiabloStoreServices(true, true, true, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = 1;
        player.LevelId = 8;
        Assert.Equal(
            new DiabloStoreServices(true, false, false, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = 2;
        Assert.Equal(
            new DiabloStoreServices(false, true, false, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = 3;
        player.LevelId = 7;
        Assert.Equal(
            new DiabloStoreServices(false, false, false, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = 74;
        Assert.Equal(
            new DiabloStoreServices(false, false, true, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = 73;
        Assert.Equal(
            new DiabloStoreServices(false, false, false, true),
            processor.CaptureStoreServices(player));

        player.LevelId = 8;
        Assert.Equal(
            new DiabloStoreServices(false, false, false, false),
            processor.CaptureStoreServices(player));

        player.ActiveStoreId = null;
        player.LevelId = 7;
        Assert.Equal(
            new DiabloStoreServices(false, false, false, false),
            processor.CaptureStoreServices(player));
    }

    [Fact]
    public void FullCaptureOmitsHintsAndRestoredLiveCaptureRecomputesThem()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(
            DiabloStoreCommandProcessor.StorytellerStoreId,
            [],
            AuthoritativeStoreRole.Wirt);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(catalog, DiabloModule.Instance.Rules));
        var player = CreatePlayer(levelId: AuthoritativePortalRules.TownLevelId);
        player.ActiveStoreId = DiabloStoreCommandProcessor.StorytellerStoreId;

        var saved = simulation.CaptureSnapshot(player, 0);
        Assert.Null(saved.ActiveStore!.Services);

        var restoredPlayer = CreatePlayer(levelId: saved.Player.LevelId);
        simulation.RestoreSnapshot(restoredPlayer, saved);
        var live = simulation.CapturePresentationSnapshot(restoredPlayer, saved.Tick + 1);
        Assert.Equal(
            new DiabloStoreServices(false, false, true, false),
            live.ActiveStore!.Services);

        restoredPlayer.LevelId = AuthoritativePortalRules.TownLevelId + 1;
        var outsideTown = simulation.CapturePresentationSnapshot(
            restoredPlayer,
            saved.Tick + 2);
        Assert.Equal(
            new DiabloStoreServices(false, false, false, false),
            outsideTown.ActiveStore!.Services);
    }

    private static AuthoritativePlayerState CreatePlayer(uint levelId)
    {
        var player = DiabloPlayerTemplate.CreateDefault(0, 64, 0, 0).CreatePlayer();
        player.LevelId = levelId;
        return player;
    }
}
