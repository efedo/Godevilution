using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectPlacementTests
{
    [Fact]
    public void KeepsNativeCenterAndInclusiveBottomWhileCullingTheWholeSpillFootprint()
    {
        var frame = new NativeObjectFrame(0, 0, 0, 128, 80, 96, 1);
        Assert.Equal(new NativeScenePixelBounds(184, 21, 128, 80), NativeObjectPlacement.ResolveBounds(200, 100, frame));
        Assert.Equal(new NativeScenePixelBounds(152, 37, 128, 80), NativeObjectPlacement.ResolveBounds(200, 100, frame, 0, -1));
        var edge = NativeObjectPlacement.ResolveBounds(-100, 100, frame);
        Assert.True(edge.IntersectsViewport(1280, 720));
        Assert.False((edge with { Width = 96 }).IntersectsViewport(1280, 720));
        Assert.False((edge with { Y = 720 }).IntersectsViewport(1280, 720));
    }

    [Fact]
    public void RetainsProjectedBrokenBarrelResidueWithoutRelaxingGenericDestructionVisibility()
    {
        var state = new ObjectSnapshot { EntityId = 1, LevelId = 1, NativeObjectId = 57, AnimationFrame = 9,
            Activated = true, Destroyed = false, KeepVisibleAfterActivation = true, MissilePassable = true };
        Assert.True(AuthoritativeObjectPresentationVisibility.IsVisible(state, 1));
        Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(state));
        state.Destroyed = true;
        Assert.False(AuthoritativeObjectPresentationVisibility.IsVisible(state, 1));
        state.Destroyed = false; state.KeepVisibleAfterActivation = false;
        Assert.False(AuthoritativeObjectPresentationVisibility.IsVisible(state, 1));
    }
}
