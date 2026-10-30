using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Client.PresentationAssets;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectCatalogTests
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ParsesCanonicalL1ObjectSourcesAndResolvesEverySourceFrame()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(), out var catalog, out var errors), string.Join(" ", errors));
        Assert.NotNull(catalog);
        Assert.Single(catalog.ProfileBindings);
        Assert.Equal((ContentHash, 1U, "cathedral"), (
            catalog.ProfileBindings[0].ContentManifestHash,
            catalog.ProfileBindings[0].LevelId,
            catalog.ProfileBindings[0].Location));
        Assert.Equal(10, catalog.SourceHashes.Count);
        Assert.Equal(8, catalog.Atlases.Count);
        Assert.Equal(8, catalog.Sprites.Count);
        Assert.Equal(new[] { 5, 6, 6, 6, 5, 2, 9, 10 }, catalog.Sprites.Select(sprite => sprite.SourceFrameCount));
        Assert.Equal(new uint[] { 1, 2, 5, 6, 7, 48, 53, 54, 57, 58 }, catalog.Objects.Select(item => item.NativeObjectId));
        Assert.True(catalog.SupportsBinding(ContentHash, 1));
        Assert.False(catalog.SupportsBinding(ContentHash, 0));
        Assert.False(catalog.SupportsBinding(new string('b', 64), 1));
        Assert.False(catalog.TryGetObject(68, out _));
        Assert.False(catalog.TryResolveFrame(68, 1, out _));

        foreach (var item in catalog.Objects) {
            Assert.True(catalog.TryResolveFrame(item.NativeObjectId, 2, out var frame));
            Assert.Equal((item.SpriteId, 2U), (frame.AtlasId, frame.NativeFrame));
            Assert.Equal(item.AnimWidth, frame.NativeWidth);
            Assert.True(frame.Width <= frame.NativeWidth);
            Assert.True(frame.Height > 0);
            Assert.False(catalog.TryResolveFrame(item.NativeObjectId, 0, out _));
            Assert.True(catalog.TryGetSprite(item.SpriteId, out var sprite));
            Assert.False(catalog.TryResolveFrame(item.NativeObjectId, checked((uint)sprite.SourceFrameCount + 1), out _));
        }
        Assert.True(catalog.TryGetAtlas(0, out var doorAtlas));
        Assert.Equal("sprites/0-l1doors.png", doorAtlas.Path);
        Assert.True(catalog.TryGetSprite(5, out var trapSprite));
        Assert.Equal("objects/traphole.cel", trapSprite.SourcePath);
    }

    [Fact]
    public void RejectsMalformedBindingsSourcesAtlasFramesObjectsAndPackIdentity()
    {
        Assert.False(NativeObjectCatalog.TryParse(null, out _, out var emptyErrors));
        Assert.NotEmpty(emptyErrors);
        Assert.False(NativeObjectCatalog.TryParse("{", out _, out _));
        Assert.False(NativeObjectCatalog.TryParse(new string('x', NativeObjectCatalog.MaximumDocumentCharacters + 1), out _, out var sizeErrors));
        Assert.Contains(sizeErrors, error => error.Contains("exceeds", StringComparison.Ordinal));

        var wrongLevel = NativeObjectCatalogTestData.ParseJson();
        wrongLevel["profileBindings"]![0]!["levelId"] = 0;
        AssertRejected(wrongLevel, "level 1");

        var noncanonicalMetadata = NativeObjectCatalogTestData.ParseJson();
        noncanonicalMetadata["sourceHashes"]![0]!["sha256"] = new string('c', 64);
        AssertRejected(noncanonicalMetadata, "canonical base-game objdat.tsv");

        var unsafeAtlas = NativeObjectCatalogTestData.ParseJson();
        unsafeAtlas["atlases"]![0]!["path"] = "sprites/../escape.png";
        AssertRejected(unsafeAtlas, "canonical order");

        var missingFrame = NativeObjectCatalogTestData.ParseJson();
        missingFrame["sprites"]![0]!["frames"]!.AsArray().RemoveAt(0);
        AssertRejected(missingFrame, "every source frame");

        var minimumFrames = new[] { 4, 6, 6, 6, 5, 2, 9, 10 };
        for (var spriteId = 0; spriteId < minimumFrames.Length; spriteId++) {
            var truncatedSprite = NativeObjectCatalogTestData.ParseJson();
            var truncatedCount = minimumFrames[spriteId] - 1;
            truncatedSprite["sprites"]![spriteId]!["sourceFrameCount"] = truncatedCount;
            var truncatedFrames = truncatedSprite["sprites"]![spriteId]!["frames"]!.AsArray();
            while (truncatedFrames.Count > truncatedCount)
                truncatedFrames.RemoveAt(truncatedFrames.Count - 1);
            var nativeWidth = truncatedSprite["sprites"]![spriteId]!["nativeWidth"]!.GetValue<int>();
            truncatedSprite["atlases"]![spriteId]!["width"] = nativeWidth * truncatedCount;
            AssertRejected(truncatedSprite, "canonical source");
        }

        var objectExpansion = NativeObjectCatalogTestData.ParseJson();
        objectExpansion["objects"]![0]!["nativeObjectId"] = 68;
        AssertRejected(objectExpansion, "canonical base-game object identity");

        var changedContent = NativeObjectCatalogTestData.ParseJson();
        NativeObjectCatalogTestData.Repack(changedContent);
        changedContent["atlases"]![0]!["sha256"] = new string('d', 64);
        var oldPackId = changedContent["packId"]!.GetValue<string>();
        Assert.NotEqual(oldPackId, NativeObjectCatalog.CalculatePackId(changedContent.ToJsonString()));
        Assert.False(NativeObjectCatalog.TryParse(changedContent.ToJsonString(), out _, out var identityErrors));
        Assert.Contains(identityErrors, error => error.Contains("packId does not match", StringComparison.Ordinal));

        var duplicateBinding = NativeObjectCatalogTestData.ParseJson();
        duplicateBinding["profileBindings"]!.AsArray().Add(duplicateBinding["profileBindings"]![0]!.DeepClone());
        AssertRejected(duplicateBinding, "unique and sorted");
    }

    [Fact]
    public void ParsesTheCatacombsFamilyForLevelsFiveToEightWithL2Doors()
    {
        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(catacombs: true), out var catalog, out var errors),
            string.Join(" ", errors));
        Assert.Equal(NativeObjectCatalog.CatacombsLocation, catalog!.Location);
        Assert.Equal(new uint[] { 5, 6, 7, 8 }, catalog.ProfileBindings.Select(binding => binding.LevelId));
        Assert.All(catalog.ProfileBindings, binding => Assert.Equal("catacombs", binding.Location));
        Assert.Equal(9, catalog.SourceHashes.Count);
        Assert.Contains(catalog.SourceHashes, source => source.Path == "levels/l2data/l2_1.pal");
        Assert.Equal(new uint[] { 5, 6, 7, 42, 43, 53, 54, 57, 58 }, catalog.Objects.Select(item => item.NativeObjectId));
        Assert.True(catalog.TryGetAtlas(3, out var doorAtlas));
        Assert.Equal("sprites/catacombs/3-l2doors.png", doorAtlas.Path);
        Assert.True(catalog.SupportsBinding(ContentHash, 5));
        Assert.True(catalog.SupportsBinding(ContentHash, 8));
        Assert.False(catalog.SupportsBinding(ContentHash, 1));
        Assert.False(catalog.TryGetObject(1, out _));
        Assert.False(catalog.TryGetObject(48, out _));
        Assert.True(catalog.TryResolveFrame(43, 3, out var openDoor));
        Assert.Equal((3, 64), (openDoor.AtlasId, openDoor.NativeWidth));

        Assert.True(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.CreateJson(), out var cathedral, out _));
        Assert.Equal(NativeObjectCatalog.CathedralLocation, cathedral!.Location);
    }

    [Fact]
    public void RejectsCatacombsCatalogsWithCathedralLevelsMixedLocationsOrL1Art()
    {
        var cathedralLevel = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        cathedralLevel["profileBindings"]![0]!["levelId"] = 1;
        AssertRejected(cathedralLevel, "catacombs level 5, 6, 7, 8");

        var mixedLocation = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        mixedLocation["profileBindings"]![1]!["location"] = "cathedral";
        AssertRejected(mixedLocation, "catacombs level");

        var unsorted = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        var bindings = unsorted["profileBindings"]!.AsArray();
        var first = bindings[0]!.DeepClone();
        bindings.RemoveAt(0);
        bindings.Add(first);
        AssertRejected(unsorted, "unique and sorted");

        var cathedralPalette = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        foreach (var source in cathedralPalette["sourceHashes"]!.AsArray())
            if (source!["path"]!.GetValue<string>() == "levels/l2data/l2_1.pal")
                source["path"] = "levels/l1data/l1_1.pal";
        AssertRejected(cathedralPalette, "source-hash set is not canonical");

        var cathedralAtlasDirectory = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        cathedralAtlasDirectory["atlases"]![3]!["path"] = "sprites/3-l2doors.png";
        AssertRejected(cathedralAtlasDirectory, "canonical order");

        var l1Door = NativeObjectCatalogTestData.ParseJson(catacombs: true);
        l1Door["objects"]![3]!["nativeObjectId"] = 1;
        AssertRejected(l1Door, "canonical base-game object identity");
    }

    [Fact]
    public void RejectsPerAtlasAndAggregateAtlasPixelOverflow()
    {
        var tooLargeAtlas = NativeObjectCatalogTestData.ParseJson();
        tooLargeAtlas["atlases"]![0]!["width"] = 8192;
        tooLargeAtlas["atlases"]![0]!["height"] = 8192;
        AssertRejected(tooLargeAtlas, "pixel limit");

        var tooLargeFamily = NativeObjectCatalogTestData.ParseJson();
        foreach (var atlas in tooLargeFamily["atlases"]!.AsArray()) {
            var atlasObject = atlas!.AsObject();
            atlasObject["width"] = 8192;
            atlasObject["height"] = 512;
        }
        AssertRejected(tooLargeFamily, "aggregate pixel limit");
    }

    private static void AssertRejected(JsonObject json, string expectedDiagnostic)
    {
        Assert.False(NativeObjectCatalog.TryParse(NativeObjectCatalogTestData.Repack(json), out _, out var errors));
        Assert.Contains(errors, error => error.Contains(expectedDiagnostic, StringComparison.OrdinalIgnoreCase));
    }
}

internal static class NativeObjectCatalogTestData
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly (uint Id, int SpriteId, int Delay, int Length, int Width)[] Objects = [
        (1, 0, 1, 0, 64),
        (2, 0, 2, 0, 64),
        (5, 1, 1, 0, 96),
        (6, 2, 1, 0, 96),
        (7, 3, 1, 0, 96),
        (48, 4, 1, 5, 128),
        (53, 5, 1, 0, 64),
        (54, 5, 2, 0, 64),
        (57, 6, 1, 9, 96),
        (58, 7, 1, 10, 96),
    ];
    private static readonly string[] Stems = ["l1doors", "chest1", "chest2", "chest3", "sarc", "traphole", "barrel", "barrelex"];
    private static readonly int[] Widths = [64, 96, 96, 96, 128, 64, 96, 96];
    private static readonly int[] MinimumFrameCounts = [4, 6, 6, 6, 5, 2, 9, 10];
    private static readonly (uint Id, int SpriteId, int Delay, int Length, int Width)[] CatacombsObjects = [
        (5, 0, 1, 0, 96),
        (6, 1, 1, 0, 96),
        (7, 2, 1, 0, 96),
        (42, 3, 1, 0, 64),
        (43, 3, 2, 0, 64),
        (53, 4, 1, 0, 64),
        (54, 4, 2, 0, 64),
        (57, 5, 1, 9, 96),
        (58, 6, 1, 10, 96),
    ];
    private static readonly string[] CatacombsStems = ["chest1", "chest2", "chest3", "l2doors", "traphole", "barrel", "barrelex"];
    private static readonly int[] CatacombsWidths = [96, 96, 96, 64, 64, 96, 96];
    private static readonly int[] CatacombsMinimumFrameCounts = [6, 6, 6, 4, 2, 9, 10];

    public static string CreateJson(bool catacombs = false)
    {
        var root = ParseJson(catacombs);
        return Repack(root);
    }

    /** Builds a synthetic Cathedral catalog, or the batch 2.3 Catacombs catalog bound to levels 5-8. */
    public static JsonObject ParseJson(bool catacombs = false)
    {
        var objects = catacombs ? CatacombsObjects : Objects;
        var stems = catacombs ? CatacombsStems : Stems;
        var widths = catacombs ? CatacombsWidths : Widths;
        var minimumFrameCounts = catacombs ? CatacombsMinimumFrameCounts : MinimumFrameCounts;
        var atlasDirectory = catacombs ? "sprites/catacombs" : "sprites";
        string[] paths = [
            "assets/txtdata/objects/objdat.tsv",
            catacombs ? "levels/l2data/l2_1.pal" : "levels/l1data/l1_1.pal",
            .. stems.Select(stem => $"objects/{stem}.cel"),
        ];
        var bindings = new JsonArray();
        foreach (var level in catacombs ? new[] { 5, 6, 7, 8 } : [1]) {
            bindings.Add(new JsonObject {
                ["contentManifestHash"] = ContentHash,
                ["levelId"] = level,
                ["location"] = catacombs ? "catacombs" : "cathedral",
            });
        }
        var root = new JsonObject {
            ["schemaVersion"] = 1,
            ["packId"] = string.Empty,
            ["profileBindings"] = bindings,
            ["sourceHashes"] = new JsonArray(),
            ["atlases"] = new JsonArray(),
            ["sprites"] = new JsonArray(),
            ["objects"] = new JsonArray(),
        };
        foreach (var path in paths.Order(StringComparer.Ordinal)) {
            root["sourceHashes"]!.AsArray().Add(new JsonObject {
                ["path"] = path,
                ["sha256"] = path == "assets/txtdata/objects/objdat.tsv"
                    ? "99ca3881bb95f5ee70a1bc811b00d11cf41e7f2a8e7defd77acc4af82e587dee"
                    : new string('b', 64),
            });
        }
        for (var id = 0; id < stems.Length; id++) {
            var sourceFrames = minimumFrameCounts[id] + (id == 0 ? 1 : 0);
            const int cellHeight = 4;
            root["atlases"]!.AsArray().Add(new JsonObject {
                ["id"] = id,
                ["path"] = $"{atlasDirectory}/{id}-{stems[id]}.png",
                ["width"] = widths[id] * sourceFrames,
                ["height"] = cellHeight,
                ["sha256"] = new string('c', 64),
            });
            var frames = new JsonArray();
            for (var frame = 0; frame < sourceFrames; frame++) {
                var height = frame == sourceFrames - 1 ? 4 : frame % 3 + 1;
                frames.Add(new JsonObject {
                    ["nativeFrame"] = frame + 1,
                    ["atlasId"] = id,
                    ["x"] = frame * widths[id],
                    ["y"] = cellHeight - height,
                    ["width"] = widths[id],
                    ["height"] = height,
                });
            }
            root["sprites"]!.AsArray().Add(new JsonObject {
                ["spriteId"] = id,
                ["sourcePath"] = $"objects/{stems[id]}.cel",
                ["nativeWidth"] = widths[id],
                ["sourceFrameCount"] = sourceFrames,
                ["frames"] = frames,
            });
        }
        foreach (var item in objects) {
            root["objects"]!.AsArray().Add(new JsonObject {
                ["nativeObjectId"] = item.Id,
                ["spriteId"] = item.SpriteId,
                ["animDelay"] = item.Delay,
                ["animLen"] = item.Length,
                ["animWidth"] = item.Width,
                ["animated"] = false,
            });
        }
        return root;
    }

    public static string Repack(JsonObject root)
    {
        root["packId"] = string.Empty;
        var blank = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        root["packId"] = NativeObjectCatalog.CalculatePackId(blank);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
