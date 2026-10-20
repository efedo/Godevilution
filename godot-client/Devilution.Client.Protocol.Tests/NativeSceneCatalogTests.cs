using System.Text.Json;
using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeSceneCatalogTests
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ParsesProfileBoundFamiliesAndResolvesOnlyQualifiedLevels()
    {
        Assert.True(NativeSceneCatalog.TryParse(CreateCatalogJson(), out var catalog, out var diagnostics),
            string.Join(" ", diagnostics));
        Assert.NotNull(catalog);
        Assert.StartsWith("native-scene-", catalog.PackId, StringComparison.Ordinal);
        Assert.True(catalog.SupportsContentManifestHash(ContentHash));
        Assert.True(catalog.TryResolveFamily(ContentHash, 7, out var town));
        Assert.Equal(NativeSceneLocation.Town, town.Location);
        Assert.True(catalog.TryResolveFamily(ContentHash, 18, out var cathedral));
        Assert.Equal(NativeSceneLocation.Cathedral, cathedral.Location);
        Assert.False(catalog.TryResolveFamily(ContentHash, 19, out _));
        Assert.False(catalog.TryResolveFamily(new string('b', 64), 7, out _));
        Assert.Equal(16, town.MicroTileLength);
        Assert.Equal(10, cathedral.MicroTileLength);
        Assert.Equal(16, town.GetSlots(0).Count);
        Assert.True(town.HasValidFinePieceLayer(1, 1, [0]));
        Assert.False(town.HasValidFinePieceLayer(1, 1, []));
        Assert.False(town.HasValidFinePieceLayer(1, 1, [1]));
        Assert.True(town.HasValidSpecialLayer(1, 1, [1]));
        Assert.False(town.HasValidSpecialLayer(1, 1, [2]));
        Assert.True(town.TryGetSpecial(1, out var special));
        Assert.Equal(-1, special.SourceTileOffsetX);
        Assert.Equal(-32, special.AnchorY);
        Assert.False(town.TryGetSpecial(0, out _));
    }

    [Fact]
    public void RejectsUnknownDuplicateAndUnsafeCatalogProperties()
    {
        var json = CreateCatalogJson();
        using var parsed = JsonDocument.Parse(json);
        var packId = parsed.RootElement.GetProperty("packId").GetString()!;
        Assert.False(NativeSceneCatalog.TryParse(
            json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"extra\":true", StringComparison.Ordinal),
            out _, out var unknownDiagnostics));
        Assert.Contains(unknownDiagnostics, diagnostic => diagnostic.Contains("unknown property", StringComparison.Ordinal));

        Assert.False(NativeSceneCatalog.TryParse(
            json.Replace("\"packId\":\"" + packId + "\"", "\"packId\":\"" + packId + "\",\"packId\":\"" + packId + "\"", StringComparison.Ordinal),
            out _, out var duplicateDiagnostics));
        Assert.Contains(duplicateDiagnostics, diagnostic => diagnostic.Contains("repeats property", StringComparison.Ordinal));

        Assert.False(NativeSceneCatalog.TryParse(
            json.Replace("sprites/town/specials.png", "sprites/../specials.png", StringComparison.Ordinal),
            out _, out var unsafeDiagnostics));
        Assert.Contains(unsafeDiagnostics, diagnostic => diagnostic.Contains("invalid resource path", StringComparison.Ordinal));

        Assert.False(NativeSceneCatalog.TryParse(
            json.Replace(packId, "native-scene-000000000000000000000000", StringComparison.Ordinal),
            out _, out var packIdDiagnostics));
        Assert.Contains(packIdDiagnostics, diagnostic => diagnostic.Contains("packId does not match", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateUnlistedOrMalformedContentBindings()
    {
        var duplicate = CreateCatalogJson().Replace(
            "{\"contentManifestHash\":\"" + ContentHash + "\",\"levelId\":7,\"location\":\"town\"},",
            "{\"contentManifestHash\":\"" + ContentHash + "\",\"levelId\":7,\"location\":\"town\"},{\"contentManifestHash\":\"" + ContentHash + "\",\"levelId\":7,\"location\":\"town\"},",
            StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(duplicate, out _, out var duplicateDiagnostics));
        Assert.Contains(duplicateDiagnostics, diagnostic => diagnostic.Contains("is repeated", StringComparison.Ordinal));

        var unlisted = CreateCatalogJson().Replace(
            "\"contentManifestHash\":\"" + ContentHash + "\",\"levelId\":7",
            "\"contentManifestHash\":\"" + new string('b', 64) + "\",\"levelId\":7",
            StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(unlisted, out _, out var unlistedDiagnostics));
        Assert.Contains(unlistedDiagnostics, diagnostic => diagnostic.Contains("unlisted content hash", StringComparison.Ordinal));

        var invalidLocation = CreateCatalogJson().Replace("\"location\":\"town\"", "\"location\":\"catacombs\"", StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(invalidLocation, out _, out var locationDiagnostics));
        Assert.Contains(locationDiagnostics, diagnostic => diagnostic.Contains("location must be town or cathedral", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInconsistentAtlasRegionsSpecialAnchorsAndSlotCoverage()
    {
        var outOfRange = CreateCatalogJson().Replace(
            "\"x\":0,\"y\":0,\"width\":64,\"height\":32",
            "\"x\":1,\"y\":0,\"width\":64,\"height\":32",
            StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(outOfRange, out _, out var atlasDiagnostics));
        Assert.Contains(atlasDiagnostics, diagnostic => diagnostic.Contains("special raster or native offset is invalid", StringComparison.Ordinal));

        var wrongAnchor = CreateCatalogJson().Replace("\"anchorY\":-32", "\"anchorY\":0", StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(wrongAnchor, out _, out var anchorDiagnostics));
        Assert.Contains(anchorDiagnostics, diagnostic => diagnostic.Contains("native offset is invalid", StringComparison.Ordinal));

        var duplicateSlot = CreateCatalogJson().Replace(
            "\"pieceId\":0,\"slot\":1,",
            "\"pieceId\":0,\"slot\":0,",
            StringComparison.Ordinal);
        Assert.False(NativeSceneCatalog.TryParse(duplicateSlot, out _, out var slotDiagnostics));
        Assert.Contains(slotDiagnostics, diagnostic => diagnostic.Contains("repeats piece/slot", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesPainterOrderByDiagonalThenRowAndColumn()
    {
        Assert.Equal(
            new[] { (0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1) },
            NativeSceneCatalog.ResolveIsometricCellOrder(3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSceneCatalog.ResolveIsometricCellOrder(0, 2));
    }

    [Fact]
    public void ResolvesTownSpecialOnFollowingDrawCellAndCullsByInclusiveRasterBounds()
    {
        Assert.True(NativeSceneCatalog.TryParse(CreateCatalogJson(), out var catalog, out var diagnostics),
            string.Join(" ", diagnostics));
        Assert.NotNull(catalog);
        var town = catalog.Families[NativeSceneLocation.Town];
        var specialLayer = new uint[3 * 3];
        specialLayer[1 * 3 + 1] = 1;

        Assert.True(NativeScenePlacement.TryResolveSpecialAtDrawCell(
            town, 3, 3, specialLayer, 2, 2, out var special, out var ownerX, out var ownerY));
        Assert.Equal((1, 1), (ownerX, ownerY));
        Assert.False(NativeScenePlacement.TryResolveSpecialAtDrawCell(
            town, 3, 3, specialLayer, 1, 1, out _, out _, out _));

        var viewport = NativeSceneViewport.Create(800, 480, 3, 3, 1, 1);
        var bounds = NativeScenePlacement.ResolveSpecialBounds(viewport, 2, 2, special);
        var drawCellBottom = viewport.CellBottomAnchor(2, 2);
        var ownerBottom = viewport.CellBottomAnchor(ownerX, ownerY);
        Assert.Equal(drawCellBottom.X - 32, bounds.X);
        Assert.Equal(ownerBottom.Y - special.Raster.Height + 1, bounds.Y);
        Assert.Equal(64, bounds.Width);
        Assert.Equal(32, bounds.Height);
        Assert.Equal((bounds.X, bounds.Y), (drawCellBottom.X - 32, drawCellBottom.Y - 32 - 32 + 1));

        Assert.True(new NativeScenePixelBounds(0, 0, bounds.Width, bounds.Height).IntersectsViewport(1, 1));
        Assert.True(new NativeScenePixelBounds(-bounds.Width + 1, 0, bounds.Width, bounds.Height)
            .IntersectsViewport(1, 1));
        Assert.True(new NativeScenePixelBounds(0, -bounds.Height + 1, bounds.Width, bounds.Height)
            .IntersectsViewport(1, 1));
        Assert.True(new NativeScenePixelBounds(-bounds.Width + 1, -bounds.Height + 1, bounds.Width, bounds.Height)
            .IntersectsViewport(1, 1));
        Assert.False(new NativeScenePixelBounds(-bounds.Width, -bounds.Height + 1, bounds.Width, bounds.Height)
            .IntersectsViewport(1, 1));
        Assert.False(new NativeScenePixelBounds(-bounds.Width + 1, -bounds.Height, bounds.Width, bounds.Height)
            .IntersectsViewport(1, 1));
        Assert.Equal(bounds.X + bounds.Width - 1, drawCellBottom.X + 31);
        Assert.Equal(bounds.Y + bounds.Height - 1, ownerBottom.Y);
        Assert.False(new NativeScenePixelBounds(1, 1, bounds.Width, bounds.Height).IntersectsViewport(1, 1));
    }

    [Fact]
    public void ResolvesCathedralSpecialOnItsCellWithNativeLeftAndInclusiveBottomBounds()
    {
        Assert.True(NativeSceneCatalog.TryParse(CreateCatalogJson(), out var catalog, out var diagnostics),
            string.Join(" ", diagnostics));
        Assert.NotNull(catalog);
        var cathedral = catalog.Families[NativeSceneLocation.Cathedral];
        var specialLayer = new uint[3 * 3];
        specialLayer[1 * 3 + 1] = 1;

        Assert.True(NativeScenePlacement.TryResolveSpecialAtDrawCell(
            cathedral, 3, 3, specialLayer, 1, 1, out var special, out var ownerX, out var ownerY));
        Assert.Equal((1, 1), (ownerX, ownerY));

        var viewport = NativeSceneViewport.Create(640, 480, 3, 3, 1, 1);
        var bounds = NativeScenePlacement.ResolveSpecialBounds(viewport, 1, 1, special);
        var bottomAnchor = viewport.CellBottomAnchor(1, 1);
        Assert.Equal(bottomAnchor.X - 32, bounds.X);
        Assert.Equal(bottomAnchor.Y - special.Raster.Height + 1, bounds.Y);
        Assert.Equal(bottomAnchor.Y, bounds.Y + bounds.Height - 1);
        Assert.True(bounds.IntersectsViewport(640, 480));
    }

    [Fact]
    public void PreservesReservedSolBitsWithoutChangingFloorClassification()
    {
        Assert.True(NativeSceneCatalog.TryParse(CreateCatalogJson(0x41), out var catalog, out var diagnostics),
            string.Join(" ", diagnostics));
        Assert.NotNull(catalog);
        Assert.Equal((byte)0x41, catalog.Families[NativeSceneLocation.Cathedral].DPieces[0].SolFlags);
        Assert.Equal("content", catalog.Families[NativeSceneLocation.Cathedral].DPieces[0].FloorClass);
    }

    private static string CreateCatalogJson(byte solFlags = 1)
    {
        var document = new {
            schemaVersion = 1,
            packId = string.Empty,
            contentManifestHashes = new[] { ContentHash },
            levelBindings = new[] {
                new { contentManifestHash = ContentHash, levelId = 7, location = "town" },
                new { contentManifestHash = ContentHash, levelId = 18, location = "cathedral" },
            },
            families = new[] {
                CreateFamily("town", 16, "-32", "-1", solFlags),
                CreateFamily("cathedral", 10, "0", "0", solFlags),
            },
        };
        var json = JsonSerializer.Serialize(document);
        var packId = NativeSceneCatalog.CalculatePackId(json);
        return json.Replace("\"packId\":\"\"", "\"packId\":\"" + packId + "\"", StringComparison.Ordinal);
    }

    private static object CreateFamily(
        string location,
        int microTileLength,
        string anchorY,
        string sourceOffset,
        byte solFlags)
    {
        var slots = Enumerable.Range(0, microTileLength).Select(slot => new {
            pieceId = 0,
            slot,
            sourceWord = 0,
            remappedWord = 0,
            sourceFrameId = 0,
            adjustedFrameId = 0,
            sourceType = "square",
            firstUseType = "none",
            effectiveType = "square",
            role = "none",
            floorRaster = (object?)null,
            raster = (object?)null,
        }).ToArray();
        return new {
            location,
            microTileLength,
            pieceCount = 1,
            sourceHashes = new[] { new { path = $"levels/{location}/source.cel", sha256 = ContentHash } },
            atlases = new object[] {
                new {
                    id = 0,
                    kind = "frames",
                    path = $"sprites/{location}/frames.png",
                    width = 32,
                    height = 32,
                    cellWidth = 32,
                    cellHeight = 32,
                    cellCount = 1,
                    sha256 = ContentHash,
                },
                new {
                    id = 1,
                    kind = "specials",
                    path = $"sprites/{location}/specials.png",
                    width = 64,
                    height = 32,
                    cellWidth = 64,
                    cellHeight = 32,
                    cellCount = 1,
                    sha256 = ContentHash,
                },
            },
            megaTiles = new[] { new { megaTileId = 0, pieceIds = new ushort[] { 0, 0, 0, 0 } } },
            frames = Array.Empty<object>(),
            dPieces = new[] { new { pieceId = 0, solFlags, floorClass = "content" } },
            minSlots = slots,
            specials = new[] {
                new {
                    specialId = 0,
                    raster = new { atlasId = 1, x = 0, y = 0, width = 64, height = 32 },
                    anchor = "bottom-left",
                    anchorX = 0,
                    anchorY = int.Parse(anchorY, System.Globalization.CultureInfo.InvariantCulture),
                    drawPhase = "after-actors",
                    sourceTileOffsetX = int.Parse(sourceOffset, System.Globalization.CultureInfo.InvariantCulture),
                    sourceTileOffsetY = int.Parse(sourceOffset, System.Globalization.CultureInfo.InvariantCulture),
                },
            },
            drawPhase = "after-actors",
        };
    }
}
