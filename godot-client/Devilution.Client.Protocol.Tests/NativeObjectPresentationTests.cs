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

    [Fact]
    public void DrawsCatacombsObjectsOnlyOnTheirBoundLevelsAndL2DoorsWithoutCathedralState()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(catacombs: true), out var catalog, out var errors),
            string.Join(" ", errors));
        var door = new ObjectSnapshot { EntityId = 4, LevelId = 6, NativeObjectId = 42, AnimationFrame = 3, MissilePassable = true };
        Assert.True(NativeObjectPresentation.TryResolve(catalog, door, ContentHash, 6, out var frame, out var diagnostic), diagnostic);
        Assert.Equal((3, 3U, 64), (frame!.AtlasId, frame.NativeFrame, frame.NativeWidth));

        Assert.False(NativeObjectPresentation.TryResolve(catalog, door, ContentHash, 1, out _, out var wrongLevel));
        Assert.Contains("not bound", wrongLevel, StringComparison.Ordinal);
        door.CathedralDoor = new CathedralDoorSnapshot { EntityId = 4, LevelId = 6, NativeObjectId = 42, AnimationFrame = 3 };
        Assert.False(NativeObjectPresentation.TryResolve(catalog, door, ContentHash, 6, out _, out var cathedralState));
        Assert.Contains("without Cathedral state", cathedralState, StringComparison.Ordinal);

        var sarcophagus = new ObjectSnapshot { EntityId = 5, LevelId = 5, NativeObjectId = 48, AnimationFrame = 1 };
        Assert.False(NativeObjectPresentation.TryResolve(catalog, sarcophagus, ContentHash, 5, out _, out var unsupported));
        Assert.Contains("supported catacombs art family", unsupported, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvesEveryShrineFrameAndOpenedTrappedChestOnAllNativeFloors(bool catacombs)
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(catacombs, interactions: true),
            out var catalog, out var errors), string.Join(" ", errors));
        foreach (var level in catacombs ? new uint[] { 5, 6, 7, 8 } : [1, 2, 3, 4]) {
            foreach (var id in new uint[] { 59, 60, 68, 69, 70 }) {
                foreach (var frame in Enumerable.Range(1, id is 59 or 60 ? 22 : 6)) {
                    var value = new ObjectSnapshot { EntityId = 151265329, LevelId = level, NativeObjectId = id,
                        PositionX = 81, PositionY = 65, AnimationFrame = (uint)frame, Activated = frame > 1 };
                    Assert.True(NativeObjectPresentation.TryResolve(catalog, value, ContentHash, level,
                        out var resolved, out var diagnostic), diagnostic);
                    Assert.Equal((uint)frame, resolved!.NativeFrame);
                    Assert.Equal(id is 59 or 60 ? 128 : 96, resolved.NativeWidth);
                }
            }
        }
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
