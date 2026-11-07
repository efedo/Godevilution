using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class HudPresentationChangeTests
{
    [Theory]
    [InlineData(0, 1)] [InlineData(1, 0)]
    public void RouteSelectionRefreshesInBothDirectionsWithoutANewSnapshot(int previous, int selected)
    {
        var hud = new HudPresentation {RouteSelection = previous};
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
        hud.RouteSelection = selected;
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, -1)]
    public void DestinationSelectionRefreshesWithoutANewSnapshot(int previous, int selected)
    {
        var hud = new HudPresentation {DestinationSelection = previous};
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
        hud.DestinationSelection = selected;
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
    }

    [Fact]
    public void FirstEmptyPlayerReplacesTheWaitingCaptionAndDisconnectRestoresIt()
    {
        var hud = new HudPresentation();
        hud.ApplyHeadlessCaption(null);
        Assert.Equal(1, hud.CaptionFormatCount);
        var player = new PlayerSnapshot();
        hud.ApplyHeadlessCaption(player);
        Assert.Equal(2, hud.CaptionFormatCount);
        Assert.Same(player, hud.CaptionPlayer);
        hud.ApplyHeadlessCaption(player.Clone());
        Assert.Equal(2, hud.CaptionFormatCount);
        hud.ApplyHeadlessCaption(null);
        Assert.Equal(3, hud.CaptionFormatCount);
        Assert.Null(hud.CaptionPlayer);
    }

    [Fact]
    public void HiddenCaptionDefersItsLatestPlayerUntilOpened()
    {
        var hud = new HudPresentation {CaptionVisible = false};
        hud.ApplyHeadlessCaption(null);
        var player = new PlayerSnapshot();
        hud.ApplyHeadlessCaption(player);
        Assert.Equal(0, hud.CaptionFormatCount);
        hud.CaptionVisible = true;
        hud.ApplyHeadlessCaption(player);
        Assert.Equal(1, hud.CaptionFormatCount);
        Assert.Same(player, hud.CaptionPlayer);
    }

    [Theory]
    [InlineData("tab")] [InlineData("diagnostics")] [InlineData("inventory")] [InlineData("belt")]
    [InlineData("equipment")] [InlineData("store")] [InlineData("stash")] [InlineData("stash-page")]
    [InlineData("oil-target")] [InlineData("spell")] [InlineData("session-transition")]
    public void OtherLocalInputsRefreshOnceWithoutANewSnapshot(string input)
    {
        var hud = new HudPresentation();
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
        hud.ChangeLocalInput(input);
        Assert.True(hud.NeedsHeadlessHudUpdate());
        Assert.False(hud.NeedsHeadlessHudUpdate());
    }
}
