using Devilution.Client.PresentationAssets;
using System.Text.Json.Nodes;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PresentationAssetPackTests
{
    [Fact]
    public void ParsesStableBindingsAndResolvesAtlasFrameRegions()
    {
        var json = """
        {
          "schemaVersion": 1,
          "packId": "sample-pack",
          "assets": [
            { "id": "player", "kind": "texture", "path": "sprites/player.png", "frames": {
              "frameCount": 4, "frameWidth": 16, "frameHeight": 16,
              "columns": 2, "rows": 2, "framesPerSecond": 20, "loop": true
            } },
            { "id": "feedback.damage", "kind": "localization", "text": "Damage" }
          ]
        }
        """;

        Assert.True(PresentationAssetCatalog.TryParse(json, out var catalog, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.NotNull(catalog);
        var resolver = new PresentationAssetResolver(
            catalog!,
            "res://packs/sample/presentation-pack.json");

        Assert.True(resolver.TryResolveFrame("player", 3, out var resolution, out var region));
        Assert.Equal("res://packs/sample/sprites/player.png", resolution.ResourcePath);
        Assert.Equal(new PresentationFrameRegion(16, 16, 16, 16), region);
        Assert.False(resolver.TryResolve("feedback.damage", out _));
        Assert.Equal("Damage", new PresentationLocalization(catalog!).Resolve("feedback.damage", "fallback"));
    }

    [Theory]
    [InlineData("../player.svg")]
    [InlineData("sprites/../../player.svg")]
    [InlineData("C:/player.svg")]
    [InlineData("res://player.svg")]
    [InlineData("user://player.svg")]
    [InlineData("/player.svg")]
    public void RejectsUnsafeRelativeResourcePaths(string path)
    {
        var json = $$"""
        {
          "schemaVersion": 1,
          "packId": "safe-pack",
          "assets": [{ "id": "player", "kind": "texture", "path": "{{path}}" }]
        }
        """;

        Assert.False(PresentationAssetCatalog.TryParse(json, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
        Assert.False(PresentationAssetPath.TryNormalizeRelativePath(path, out _, out _));
        // A res:// path is permitted for manifest selection, never for an asset's relative path.
        Assert.Equal(path == "res://player.svg", PresentationAssetPath.TryNormalizeResourcePath(path, out _, out _));
    }

    [Fact]
    public void RejectsDuplicateIdsAndDuplicateJsonPropertiesAtomically()
    {
        var duplicateIds = """
        {
          "schemaVersion": 1,
          "packId": "safe-pack",
          "assets": [
            { "id": "player", "kind": "texture", "path": "player.svg" },
            { "id": "player", "kind": "texture", "path": "other.svg" }
          ]
        }
        """;
        var duplicateProperties = """
        {
          "schemaVersion": 1,
          "packId": "safe-pack",
          "assets": [{ "id": "player", "kind": "texture", "path": "player.svg", "PATH": "other.svg" }]
        }
        """;

        Assert.False(PresentationAssetCatalog.TryParse(duplicateIds, out var idCatalog, out _));
        Assert.Null(idCatalog);
        Assert.False(PresentationAssetCatalog.TryParse(duplicateProperties, out var propertyCatalog, out _));
        Assert.Null(propertyCatalog);
    }

    [Fact]
    public void RejectsNumericKindsMissingFrameFieldsAndWrongLocalizationShape()
    {
        var json = """
        {
          "schemaVersion": 1,
          "packId": "safe-pack",
          "assets": [
            { "id": "numeric", "kind": 0, "path": "x.svg" },
            { "id": "atlas", "kind": "texture", "path": "x.png", "frames": {
              "frameCount": 2, "frameWidth": 16, "frameHeight": 16,
              "columns": 2, "rows": 1, "framesPerSecond": 20
            } },
            { "id": "label", "kind": "localization", "path": "label.txt", "text": "Label" }
          ]
        }
        """;

        Assert.False(PresentationAssetCatalog.TryParse(json, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("loop", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("kind", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("localization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectsOversizedManifestWithoutActivatingPartialMappings()
    {
        var oversized = "{" + new string('x', 1_000_001) + "}";

        Assert.False(PresentationAssetCatalog.TryParse(oversized, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("size", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(".. /player.svg")]
    [InlineData("sprites./player.svg")]
    [InlineData("player.svg ")]
    [InlineData("sprites\n/player.svg")]
    [InlineData("sprites\t/player.svg")]
    [InlineData("sprites\\player.svg")]
    [InlineData("sprites//player.svg")]
    public void RejectsWindowsAmbiguousOrControlCharacterPaths(string path)
    {
        Assert.False(PresentationAssetPath.TryNormalizeRelativePath(path, out _, out _));
        Assert.False(PresentationAssetPath.TryNormalizeResourcePath("res://packs/" + path, out _, out _));
        Assert.False(PresentationAssetPath.TryCombineResourcePath("res://packs/manifest.json", path, out _, out _));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"packId\":\"test\",\"assets\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"packId\":\"TEST\",\"assets\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"packId\":\"test\",\"assets\":null}")]
    [InlineData("{\"schemaVersion\":1,\"packId\":\"test\",\"assets\":[null]}")]
    [InlineData("{\"schemaVersion\":1,\"packId\":\"test\",\"assets\":[],\"extra\":true}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void RejectsInvalidDocumentShapesIndependently(string json)
    {
        Assert.False(PresentationAssetCatalog.TryParse(json, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
    }

    [Theory]
    [InlineData("{\"id\":\"player\",\"kind\":0,\"path\":\"player.svg\"}")]
    [InlineData("{\"id\":\"player\",\"kind\":\"unknown\",\"path\":\"player.svg\"}")]
    [InlineData("{\"id\":\"player\",\"kind\":\"texture\"}")]
    [InlineData("{\"id\":\"player\",\"kind\":\"texture\",\"path\":\"player.svg\",\"text\":\"Wrong\"}")]
    [InlineData("{\"id\":\"label\",\"kind\":\"localization\",\"path\":\"label.txt\",\"text\":\"Label\"}")]
    [InlineData("{\"id\":\"label\",\"kind\":\"localization\"}")]
    [InlineData("{\"id\":\"audio\",\"kind\":\"audio\",\"path\":\"audio.ogg\",\"frames\":{}}")]
    public void RejectsInvalidAssetShapesIndependently(string asset)
    {
        var json = "{\"schemaVersion\":1,\"packId\":\"test\",\"assets\":[" + asset + "]}";
        Assert.False(PresentationAssetCatalog.TryParse(json, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
    }

    [Theory]
    [InlineData("frameCount", "0")]
    [InlineData("frameCount", "257")]
    [InlineData("frameCount", "5")]
    [InlineData("frameWidth", "0")]
    [InlineData("frameHeight", "4097")]
    [InlineData("columns", "65")]
    [InlineData("rows", "0")]
    [InlineData("framesPerSecond", "0")]
    [InlineData("framesPerSecond", "241")]
    [InlineData("loop", "null")]
    public void RejectsEachInvalidFrameFieldAtomically(string field, string value)
    {
        var document = JsonNode.Parse("""
        { "schemaVersion": 1, "packId": "test", "assets": [
          { "id": "player", "kind": "texture", "path": "player.svg", "frames": {
            "frameCount": 4, "frameWidth": 16, "frameHeight": 16,
            "columns": 2, "rows": 2, "framesPerSecond": 8, "loop": true
          } }
        ] }
        """)!;
        document["assets"]![0]!["frames"]![field] = JsonNode.Parse(value);
        Assert.False(PresentationAssetCatalog.TryParse(document.ToJsonString(), out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void RejectsMissingLoopAndOversizedAssetArrayIndependently()
    {
        var missingLoop = """
        { "schemaVersion": 1, "packId": "test", "assets": [
          { "id": "player", "kind": "texture", "path": "player.svg", "frames": {
            "frameCount": 4, "frameWidth": 16, "frameHeight": 16,
            "columns": 2, "rows": 2, "framesPerSecond": 8
          } }
        ] }
        """;
        Assert.False(PresentationAssetCatalog.TryParse(missingLoop, out var missingCatalog, out _));
        Assert.Null(missingCatalog);
        var entries = string.Join(',', Enumerable.Range(0, 257)
            .Select(index => $"{{\"id\":\"asset{index}\",\"kind\":\"texture\",\"path\":\"player.svg\"}}"));
        Assert.False(PresentationAssetCatalog.TryParse(
            "{\"schemaVersion\":1,\"packId\":\"test\",\"assets\":[" + entries + "]}", out var oversizedCatalog, out _));
        Assert.Null(oversizedCatalog);
    }

    [Fact]
    public void ActivatedCatalogCannotBeMutatedThroughItsPublicDictionary()
    {
        Assert.True(PresentationAssetCatalog.TryParse(
            """{"schemaVersion":1,"packId":"test","assets":[{"id":"player","kind":"texture","path":"player.svg"}]}""",
            out var catalog, out _));
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, PresentationAssetDefinition>>(catalog!.Assets);
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        var original = catalog.Assets["player"];
        var changed = original with { Path = "other.svg" };
        Assert.NotEqual(original, changed);
        Assert.Equal("player.svg", catalog.Assets["player"].Path);
    }

    [Fact]
    public void UnknownBindingsUseFallbackAndLocalizationUsesCallerFallback()
    {
        var json = """
        {
          "schemaVersion": 1,
          "packId": "safe-pack",
          "assets": [{ "id": "player", "kind": "texture", "path": "player.svg" }]
        }
        """;
        Assert.True(PresentationAssetCatalog.TryParse(json, out var catalog, out _));
        var resolver = new PresentationAssetResolver(catalog!, "res://assets/presentation-pack.json");

        Assert.False(resolver.TryResolve("missing", out _));
        Assert.Equal("Fallback", new PresentationLocalization(catalog!).Resolve("missing", "Fallback"));
    }

    [Fact]
    public void FrameClockAdvancesDeterministicallyAndFreezesOnInvalidDelta()
    {
        var layout = new PresentationFrameLayout(4, 16, 16, 2, 2, 20, true);
        var clock = new PresentationFrameClock();

        Assert.Equal(0, clock.ResolveFrame(layout));
        clock.Advance(0.05);
        Assert.Equal(1, clock.ResolveFrame(layout));
        clock.Advance(double.NaN);
        clock.Advance(-1);
        Assert.Equal(1, clock.ResolveFrame(layout));
        clock.Advance(0.15);
        Assert.Equal(0, clock.ResolveFrame(layout));
        clock.Reset();
        Assert.Equal(0, clock.ResolveFrame(layout));
    }

    [Fact]
    public void NonLoopingFramesClampAtLastFrame()
    {
        var layout = new PresentationFrameLayout(3, 8, 8, 3, 1, 10, false);
        var clock = new PresentationFrameClock();

        clock.Advance(2);

        Assert.Equal(2, clock.ResolveFrame(layout));
    }

    [Fact]
    public void LoopingFramesContinueAfterTwentyFourHours()
    {
        var layout = new PresentationFrameLayout(4, 16, 16, 2, 2, 8, true);
        var clock = new PresentationFrameClock();
        clock.Advance(86400.125);
        Assert.Equal(1, clock.ResolveFrame(layout));
        clock.Advance(0.125);
        Assert.Equal(2, clock.ResolveFrame(layout));
    }

    [Fact]
    public void HugeFiniteDeltaCannotOverflowFrameSelection()
    {
        var layout = new PresentationFrameLayout(4, 16, 16, 2, 2, 240, true);
        var clock = new PresentationFrameClock();
        clock.Advance(double.MaxValue);
        clock.Advance(double.MaxValue);
        Assert.True(double.IsFinite(clock.ElapsedSeconds));
        Assert.InRange(clock.ResolveFrame(layout), 0, 3);
        Assert.Equal(3, clock.ResolveFrame(layout with { Loop = false }));
        Assert.Equal(0, clock.ResolveFrame(null));
        var before = clock.ElapsedSeconds;
        clock.Advance(double.PositiveInfinity);
        Assert.Equal(before, clock.ElapsedSeconds);
    }
}
