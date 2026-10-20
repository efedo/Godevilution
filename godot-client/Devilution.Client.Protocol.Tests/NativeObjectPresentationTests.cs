using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectPresentationTests
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void UsesOnlyTheSnapshotCurrentFrameAndRetainsNativeWidthForAnchorPlacement()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(), out var catalog, out var errors), string.Join(" ", errors));
        var state = new ObjectSnapshot {
            EntityId = 7,
            LevelId = 1,
            NativeObjectId = 48,
            AnimationFrame = 3,
            ActivatedAnimationFrame = 1,
        };

        Assert.True(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out var frame, out var diagnostic), diagnostic);
        Assert.NotNull(frame);
        Assert.Equal((4, 3U, 128), (frame.AtlasId, frame.NativeFrame, frame.NativeWidth));
        Assert.Equal(3, frame.Height);
    }

    [Fact]
    public void RejectsUnknownIdsWrongLevelOrProfileAndUnavailableCurrentFrames()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(), out var catalog, out var errors), string.Join(" ", errors));
        var state = new ObjectSnapshot { EntityId = 1, LevelId = 1, NativeObjectId = 5, AnimationFrame = 2 };
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 0, out _, out var wrongLevel));
        Assert.Contains("not bound", wrongLevel, StringComparison.Ordinal);
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, new string('b', 64), 1, out _, out var wrongHash));
        Assert.Contains("not bound", wrongHash, StringComparison.Ordinal);
        state.NativeObjectId = 68;
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out _, out var unsupported));
        Assert.Contains("not in the supported", unsupported, StringComparison.Ordinal);
        state.NativeObjectId = 5;
        state.AnimationFrame = 0;
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out _, out var missingFrame));
        Assert.Contains("outside its source art", missingFrame, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresStrictConsistentTypedDoorSnapshotForDoorArt()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(), out var catalog, out var errors), string.Join(" ", errors));
        var state = CreateValidOpenDoor();
        Assert.True(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out var frame, out var diagnostic), diagnostic);
        Assert.Equal(3U, frame!.NativeFrame);

        var door = state.CathedralDoor!;
        door.PositionX++;
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out _, out var mismatch));
        Assert.Contains("consistent authoritative door", mismatch, StringComparison.Ordinal);
        door.PositionX = state.PositionX;
        state.AnimationFrame = 2;
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out _, out var frameMismatch));
        Assert.Contains("consistent authoritative door", frameMismatch, StringComparison.Ordinal);
        state.AnimationFrame = 3;
        door.SelectionRegion = 3;
        Assert.False(NativeObjectPresentation.TryResolve(catalog, state, ContentHash, 1, out _, out var invalidDoor));
        Assert.Contains("consistent authoritative door", invalidDoor, StringComparison.Ordinal);
    }

    private static ObjectSnapshot CreateValidOpenDoor()
        => new() {
            EntityId = 11,
            LevelId = 1,
            PositionX = 3,
            PositionY = 4,
            NativeObjectId = 1,
            KeepVisibleAfterActivation = true,
            AnimationFrame = 3,
            MissilePassable = true,
            CathedralDoor = new CathedralDoorSnapshot {
                EntityId = 11,
                LevelId = 1,
                PositionX = 3,
                PositionY = 4,
                NativeObjectId = 1,
                DoorState = 1,
                CachedPiece = 43,
                CachedNeighborPiece = 49,
                AnimationFrame = 3,
                SelectionRegion = 2,
                PreDraw = true,
                MissilePassable = true,
            },
        };
}
