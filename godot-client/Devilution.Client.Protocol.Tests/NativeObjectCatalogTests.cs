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
    private static readonly string[] Paths = [
        "assets/txtdata/objects/objdat.tsv",
        "levels/l1data/l1_1.pal",
        "objects/l1doors.cel",
        "objects/chest1.cel",
        "objects/chest2.cel",
        "objects/chest3.cel",
        "objects/sarc.cel",
        "objects/traphole.cel",
        "objects/barrel.cel",
        "objects/barrelex.cel",
    ];
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

    public static string CreateJson()
    {
        var root = ParseJson();
        return Repack(root);
    }

    public static JsonObject ParseJson()
    {
        var root = new JsonObject {
            ["schemaVersion"] = 1,
            ["packId"] = string.Empty,
            ["profileBindings"] = new JsonArray(new JsonObject {
                ["contentManifestHash"] = ContentHash,
                ["levelId"] = 1,
                ["location"] = "cathedral",
            }),
            ["sourceHashes"] = new JsonArray(),
            ["atlases"] = new JsonArray(),
            ["sprites"] = new JsonArray(),
            ["objects"] = new JsonArray(),
        };
        foreach (var path in Paths.Order(StringComparer.Ordinal)) {
            root["sourceHashes"]!.AsArray().Add(new JsonObject {
                ["path"] = path,
                ["sha256"] = path == "assets/txtdata/objects/objdat.tsv"
                    ? "99ca3881bb95f5ee70a1bc811b00d11cf41e7f2a8e7defd77acc4af82e587dee"
                    : new string('b', 64),
            });
        }
        for (var id = 0; id < Stems.Length; id++) {
            var sourceFrames = MinimumFrameCounts[id] + (id == 0 ? 1 : 0);
            const int cellHeight = 4;
            root["atlases"]!.AsArray().Add(new JsonObject {
                ["id"] = id,
                ["path"] = $"sprites/{id}-{Stems[id]}.png",
                ["width"] = Widths[id] * sourceFrames,
                ["height"] = cellHeight,
                ["sha256"] = new string('c', 64),
            });
            var frames = new JsonArray();
            for (var frame = 0; frame < sourceFrames; frame++) {
                var height = frame == sourceFrames - 1 ? 4 : frame % 3 + 1;
                frames.Add(new JsonObject {
                    ["nativeFrame"] = frame + 1,
                    ["atlasId"] = id,
                    ["x"] = frame * Widths[id],
                    ["y"] = cellHeight - height,
                    ["width"] = Widths[id],
                    ["height"] = height,
                });
            }
            root["sprites"]!.AsArray().Add(new JsonObject {
                ["spriteId"] = id,
                ["sourcePath"] = $"objects/{Stems[id]}.cel",
                ["nativeWidth"] = Widths[id],
                ["sourceFrameCount"] = sourceFrames,
                ["frames"] = frames,
            });
        }
        foreach (var item in Objects) {
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
