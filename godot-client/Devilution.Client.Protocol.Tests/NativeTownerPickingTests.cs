using System.Text.Json.Nodes;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class NativeTownerPickingTests
{
    private static readonly IReadOnlyDictionary<int, NativeTownerOpacityMask> NoMasks = new Dictionary<int, NativeTownerOpacityMask>();
    private static readonly Lazy<NativeTownerCatalog> Catalog = new(CreateCatalog);
    private static readonly NativeSceneViewport View = NativeSceneViewport.Create(800, 600, 112, 112, 50, 50);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TransparentPaddingFallsBackInNativeFlankCurrentSoutheastOrder(bool flip)
    {
        var center = Resident(0, 50, 50);
        var flank = Resident(1, flip ? 50 : 51, flip ? 51 : 50);
        var opposite = Resident(2, flip ? 51 : 50, flip ? 50 : 51);
        var southeast = Resident(3, 51, 51);
        var x = 400 + (flip ? -10 : 10);
        Assert.Equal((50, 50), NativeTownerPicking.ResolveTile(View, x, 300, out var actualFlip));
        Assert.Equal(flip, actualFlip);
        Assert.Null(Select([opposite], x, 300));
        Assert.Same(flank, Select([opposite, flank], x, 300));
        Assert.Same(center, Select([center, opposite, flank], x, 300));
        Assert.Same(southeast, Select([southeast, center, opposite, flank], x, 300));
        Assert.Same(southeast, Select([flank, opposite, center, southeast], x, 300));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CowNegativeFootprintCellsResolveTheirPositiveOwner(int cell)
    {
        var cow = Resident(8, 51, 51, 9);
        var x = 51 - cell % 2; var y = 51 - cell / 2;
        var screen = View.WorldToScreen(x, y);
        Assert.Same(cow, NativeTownerPicking.Select(View, new[] { cow }, null, NoMasks, 0, screen.X, screen.Y));
        cow.LevelId = 1;
        Assert.Null(NativeTownerPicking.Select(View, new[] { cow }, null, NoMasks, 0, screen.X, screen.Y));
    }

    [Fact]
    public void PixelSelectionPrecedesFallbackAndSearchesRenderedCellsBackToFront()
    {
        var center = Resident(0, 50, 50);
        var southeast = Resident(3, 51, 51);
        var flank = Resident(1, 51, 50);
        const double x = 400.25, y = 285.25;
        var masks = new Dictionary<int, NativeTownerOpacityMask> {
            [0] = MaskWithPixel(center, x, y),
        };
        Assert.Same(center, NativeTownerPicking.Select(View, new[] { center, southeast }, Catalog.Value, masks, 0, x, y));
        masks[1] = MaskWithPixel(flank, x, y);
        // cursor.cpp's reverse rectangle search encounters (51,50) before (50,50), unlike tile fallback.
        Assert.Same(flank, NativeTownerPicking.Select(View, new[] { center, flank }, Catalog.Value, masks, 0, x, y));
        Assert.Same(flank, NativeTownerPicking.Select(View, new[] { flank, center }, Catalog.Value, masks, 0, x, y));
    }

    [Fact]
    public void ShadowPixelsFallThroughToNativeOccupiedTileSelection()
    {
        var center = Resident(0, 50, 50);
        var southeast = Resident(3, 51, 51);
        var masks = new Dictionary<int, NativeTownerOpacityMask> { [0] = MaskWithPixel(center, 400.25, 285.25, selectable: false) };
        Assert.Same(southeast, NativeTownerPicking.Select(View, new[] { center, southeast }, Catalog.Value, masks, 0, 400.25, 285.25));
    }

    [Fact]
    public void NativeIntegerPixelDiamondsPreserveOddPixelTiesAndFractionalCameraMotion()
    {
        foreach (var camera in new[] { (50d, 50d), (50.125, 50.375) }) {
            var view = NativeSceneViewport.Create(800, 600, 112, 112, camera.Item1, camera.Item2);
            var screen = view.WorldToScreen(50, 50);
            for (var py = 0; py < 32; py++)
                for (var px = 0; px < 64; px++) {
                    // Literal ShiftToDiamondGridAlignment rules; integer division is significant at odd pixels.
                    var flipY = py < px / 2;
                    var flipX = py >= 32 - px / 2;
                    Assert.Equal((50 + (flipX ? 1 : 0), 50 - (flipY ? 1 : 0)),
                        NativeTownerPicking.ResolveTile(view, screen.X + px, screen.Y + py - 16, out var flip));
                    Assert.Equal(flipY && flipX || (flipY || flipX) && px < 32, flip);
                }
        }
    }

    [Fact]
    public void FallbackClampsAtMapEdgesAndIgnoresOffMapOccupancyAndOffscreenClicks()
    {
        var view = NativeSceneViewport.Create(800, 600, 112, 112, 0, 0);
        var resident = Resident(0, 0, 0);
        Assert.Equal((0, 0), NativeTownerPicking.ResolveTile(view, 400, 0, out _));
        Assert.Same(resident, NativeTownerPicking.Select(view, new[] { resident }, null, NoMasks, 0, 400, 0));
        Assert.Null(NativeTownerPicking.Select(view, new[] { resident }, null, NoMasks, 0, -1, 0));
        Assert.Null(NativeTownerPicking.Select(view, new[] { resident }, null, NoMasks, 0, double.NaN, 0));
        Assert.Null(NativeTownerPicking.Select(view, new[] { Resident(0, -1, 0) }, null, NoMasks, 0, 400, 0));
    }

    [Fact]
    public void PixelAndTileSelectionDoNotAllocatePerClick()
    {
        var residents = new[] { Resident(0, 50, 50) };
        var masks = new Dictionary<int, NativeTownerOpacityMask> { [0] = MaskWithPixel(residents[0], 400.25, 285.25) };
        var catalog = Catalog.Value;
        Action<int> click = i => NativeTownerPicking.Select(View, residents, catalog, masks, 0, i % 2 == 0 ? 400.25 : 410, i % 2 == 0 ? 285.25 : 300);
        HotPathAllocation.Warm(click); // Both pixel-hit and tile-fallback paths.
        var measured = HotPathAllocation.Measure(click, 1000);
        Assert.Equal(0, measured.Bytes);
    }

    private static NativeTownResidentSnapshot? Select(NativeTownResidentSnapshot[] residents, double x, double y)
        => NativeTownerPicking.Select(View, residents, Catalog.Value, NoMasks, 0, x, y);

    private static NativeTownResidentSnapshot Resident(int slot, int x, int y, int type = 0)
        => new() { ResidentSlot = slot, PositionX = x, PositionY = y, TownerId = type };

    private static NativeTownerOpacityMask MaskWithPixel(NativeTownResidentSnapshot resident, double x, double y, bool selectable = true)
    {
        var catalog = Catalog.Value;
        catalog.TryGetSprite(catalog.Residents[resident.ResidentSlot].SpriteId, out var sprite);
        var frame = sprite.ResolveFrame(0, 0);
        catalog.TryGetAtlas(frame.AtlasId, out var atlas);
        var anchor = View.CellBottomAnchor(resident.PositionX, resident.PositionY);
        var bounds = NativeScenePlacement.ResolveResidentBounds(anchor.X, anchor.Y, sprite.AlignmentWidth, frame.Width, frame.Height);
        var pixel = (frame.Y + (int)Math.Floor(y - bounds.Y)) * atlas.Width + frame.X + (int)Math.Floor(x - bounds.X);
        var rgba = new byte[atlas.Width * atlas.Height * 4];
        rgba[pixel * 4 + 3] = 255;
        var data = NativeTownerPickingData.Create(atlas.Width, atlas.Height);
        if (selectable) NativeTownerPickingData.SetSelectable(data, pixel);
        return new NativeTownerOpacityMask(atlas.Width, atlas.Height, rgba, data);
    }

    private static NativeTownerCatalog CreateCatalog()
    {
        var root = JsonNode.Parse(NativeTownerCatalogTests.CreateCatalogJson())!.AsObject();
        foreach (var sprite in root["sprites"]!.AsArray()) {
            var id = sprite!["spriteId"]!.GetValue<int>();
            var width = id == 8 ? 128 : 96;
            var groups = sprite["directionGroupCount"]!.GetValue<int>();
            root["atlases"]![id]!["width"] = width * sprite["sourceFramesPerDirection"]!.GetValue<int>();
            root["atlases"]![id]!["height"] = groups * width;
            foreach (var frame in sprite["frames"]!.AsArray()) {
                frame!["x"] = frame["frame"]!.GetValue<int>() * width;
                frame["y"] = frame["group"]!.GetValue<int>() * width;
                frame["width"] = width; frame["height"] = width;
            }
        }
        root["packId"] = string.Empty;
        root["packId"] = NativeTownerCatalog.CalculatePackId(root.ToJsonString());
        Assert.True(NativeTownerCatalog.TryParse(root.ToJsonString(), out var catalog, out var errors), string.Join("; ", errors));
        return catalog!;
    }
}
