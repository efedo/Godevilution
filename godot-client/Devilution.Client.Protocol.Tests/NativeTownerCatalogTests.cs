using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Client.PresentationAssets;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerCatalogTests
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TownFingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void PreservesTheTwoFieldNativePlayerProfileSetConstructorAndDeconstruction()
    {
        var hashes = new[] { ContentHash };
        var bindings = new[] { new NativePlayerArtLevelBinding(ContentHash, 0, "town") };
        var profileSet = new NativePlayerArtProfileSet(hashes, bindings);

        var (observedHashes, observedBindings) = profileSet;
        Assert.Same(hashes, observedHashes);
        Assert.Same(bindings, observedBindings);
        Assert.Empty(profileSet.TownCatalogFingerprints);
    }

    [Fact]
    public void ParsesCanonicalRosterAndFormatFiveTownBindingAndResolvesNativeArtFrames()
    {
        Assert.True(NativeTownerCatalog.TryParse(CreateCatalogJson(), out var catalog, out var errors), string.Join(" ", errors));
        Assert.NotNull(catalog);
        Assert.Equal(9, catalog.Atlases.Count);
        Assert.Equal(9, catalog.Sprites.Count);
        Assert.Equal(11, catalog.Residents.Count);
        Assert.Equal(11, catalog.SourceHashes.Count);
        Assert.True(catalog.SupportsBinding(ContentHash, TownFingerprint));
        Assert.False(catalog.SupportsBinding(ContentHash, new string('c', 64)));
        Assert.False(catalog.SupportsBinding(new string('d', 64), TownFingerprint));
        Assert.True(catalog.ResidentsMatches(CanonicalResidentTuples()));
        Assert.False(catalog.ResidentsMatches(CanonicalResidentTuples().Skip(1).ToArray()));
        Assert.True(catalog.TryGetSprite(8, out var cow));
        Assert.Equal(128, cow.AlignmentWidth);
        Assert.Equal(8, cow.DirectionGroupCount);
        Assert.True(catalog.TryGetAtlas(8, out var atlas));
        Assert.Equal(8, atlas.Id);
        Assert.Equal(new NativeTownerFrame(8, 0, 2, 1, 1, 0, 0), cow.ResolveFrame(0, 0));
        Assert.Equal(new NativeTownerFrame(8, 0, 11, 1, 1, 3, 0), cow.ResolveFrame(0, 3));
        Assert.Equal(new NativeTownerFrame(8, 4, 10, 2, 2, 3, 1), cow.ResolveFrame(3, 3));
        Assert.Equal(new NativeTownerFrame(0, 20, 0, 2, 3, 0, 5), catalog.Sprites[0].ResolveFrame(3, 6));
        Assert.Equal(new NativeTownerFrame(0, 0, 2, 1, 1, 0, 0), catalog.Sprites[0].ResolveFrame(double.NaN, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => cow.ResolveFrame(0, 8));
    }

    [Fact]
    public void RejectsEmptyOversizedMalformedUnknownAndDuplicateCatalogJson()
    {
        Assert.False(NativeTownerCatalog.TryParse(null, out _, out var emptyErrors));
        Assert.NotEmpty(emptyErrors);
        Assert.False(NativeTownerCatalog.TryParse(new string('x', NativeTownerCatalog.MaximumDocumentCharacters + 1), out _, out var sizeErrors));
        Assert.Contains(sizeErrors, error => error.Contains("exceeds", StringComparison.Ordinal));
        Assert.False(NativeTownerCatalog.TryParse("{", out _, out _));

        var unknown = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        unknown["unexpected"] = true;
        Assert.False(NativeTownerCatalog.TryParse(Repack(unknown), out _, out var unknownErrors));
        Assert.Contains(unknownErrors, error => error.Contains("unknown property", StringComparison.Ordinal));

        using var parsed = JsonDocument.Parse(CreateCatalogJson());
        var packId = parsed.RootElement.GetProperty("packId").GetString()!;
        var duplicate = CreateCatalogJson().Replace(
            "\"packId\":\"" + packId + "\"",
            "\"packId\":\"" + packId + "\",\"packId\":\"" + packId + "\"",
            StringComparison.Ordinal);
        Assert.False(NativeTownerCatalog.TryParse(duplicate, out _, out var duplicateErrors));
        Assert.Contains(duplicateErrors, error => error.Contains("repeats property", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInvalidFormatFiveBindingsAndNoncanonicalSourceMetadata()
    {
        var wrongLevel = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        wrongLevel["profileBindings"]![0]!["levelId"] = 1;
        Assert.False(NativeTownerCatalog.TryParse(Repack(wrongLevel), out _, out var levelErrors));
        Assert.Contains(levelErrors, error => error.Contains("Town level zero", StringComparison.Ordinal));

        var wrongHash = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        wrongHash["sourceHashes"]![0]!["sha256"] = new string('e', 64);
        Assert.False(NativeTownerCatalog.TryParse(Repack(wrongHash), out _, out var hashErrors));
        Assert.Contains(hashErrors, error => error.Contains("canonical towner animation metadata", StringComparison.Ordinal));

        var unsafePath = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        unsafePath["atlases"]![0]!["path"] = "sprites/../escape.png";
        Assert.False(NativeTownerCatalog.TryParse(Repack(unsafePath), out _, out var pathErrors));
        Assert.Contains(pathErrors, error => error.Contains("metadata is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnboundedAtlasFamiliesAndIncompleteFrameOrRosterTables()
    {
        var oneTooLarge = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        oneTooLarge["atlases"]![0]!["width"] = 8192;
        oneTooLarge["atlases"]![0]!["height"] = 8192;
        Assert.False(NativeTownerCatalog.TryParse(Repack(oneTooLarge), out _, out var atlasErrors));
        Assert.Contains(atlasErrors, error => error.Contains("pixel limit", StringComparison.Ordinal));

        var aggregateTooLarge = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        foreach (var atlas in aggregateTooLarge["atlases"]!.AsArray()) {
            var atlasObject = atlas!.AsObject();
            atlasObject["width"] = 8192;
            atlasObject["height"] = 512;
        }
        Assert.False(NativeTownerCatalog.TryParse(Repack(aggregateTooLarge), out _, out var aggregateErrors));
        Assert.Contains(aggregateErrors, error => error.Contains("aggregate size limit", StringComparison.Ordinal));

        var badOrder = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        badOrder["sprites"]![0]!["animationOrder"]![0] = 5;
        Assert.False(NativeTownerCatalog.TryParse(Repack(badOrder), out _, out var orderErrors));
        Assert.Contains(orderErrors, error => error.Contains("differs from the canonical", StringComparison.Ordinal));

        var missingFrame = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        missingFrame["sprites"]![0]!["frames"]!.AsArray().RemoveAt(0);
        Assert.False(NativeTownerCatalog.TryParse(Repack(missingFrame), out _, out var frameErrors));
        Assert.Contains(frameErrors, error => error.Contains("every source frame", StringComparison.Ordinal));

        var wrongResident = JsonNode.Parse(CreateCatalogJson())!.AsObject();
        wrongResident["residents"]![10]!["positionX"] = 60;
        Assert.False(NativeTownerCatalog.TryParse(Repack(wrongResident), out _, out var residentErrors));
        Assert.Contains(residentErrors, error => error.Contains("canonical base roster", StringComparison.Ordinal));
    }

    private static string CreateCatalogJson()
    {
        var textDataRoot = Path.Combine(FindRepositoryRoot(), "assets", "txtdata");
        var canonicalMetadata = NativeTownerArtCompiler.ReadBaseTownSpriteMetadata(textDataRoot);
        var sprites = Enumerable.Range(0, 9).Select(spriteId => {
            var groups = spriteId == 8 ? 8 : 1;
            var sourceFrames = canonicalMetadata[spriteId].AnimationFrames;
            var animationFrames = canonicalMetadata[spriteId].AnimationFrames;
            var ticks = canonicalMetadata[spriteId].TicksPerFrame;
            var frames = Enumerable.Range(0, groups).SelectMany(group => Enumerable.Range(0, sourceFrames).Select(frame => {
                var height = frame % 3 + 1;
                return new {
                    group,
                    frame,
                    atlasId = spriteId,
                    x = frame * 4,
                    y = group * 3 + 3 - height,
                    width = frame % 4 + 1,
                    height,
                };
            })).ToArray();
            var animationOrder = canonicalMetadata[spriteId].AnimationOrder.ToArray();
            return new {
                spriteId,
                alignmentWidth = spriteId == 8 ? 128 : 96,
                directionGroupCount = groups,
                sourceFramesPerDirection = sourceFrames,
                animationFrames,
                ticksPerFrame = ticks,
                animationOrder,
                frames,
            };
        }).ToArray();
        var sourcePaths = new[] {
            "assets/txtdata/towners/towners.tsv",
            "levels/towndata/town.pal",
            "towners/animals/cow.cel",
            "towners/drunk/twndrunk.cel",
            "towners/healer/healer.cel",
            "towners/smith/smithn.cel",
            "towners/townboy/pegkid1.cel",
            "towners/twnf/twnfn.cel",
            "towners/townwmn1/wmnn.cel",
            "towners/townwmn1/witch.cel",
            "towners/strytell/strytell.cel",
        }.Order(StringComparer.Ordinal).Select(path => new {
            path,
            sha256 = path == "assets/txtdata/towners/towners.tsv"
                ? "91546c020a1a24b3cae189bcc80ef7bf2c50675f5b92243b43e6317dea993b9e"
                : new string('c', 64),
        }).ToArray();
        var document = new {
            schemaVersion = 1,
            packId = string.Empty,
            profileBindings = new[] { new { contentManifestHash = ContentHash, townCatalogFingerprint = TownFingerprint, levelId = 0, location = "town" } },
            sourceHashes = sourcePaths,
            atlases = Enumerable.Range(0, 9).Select(id => {
                var frames = canonicalMetadata[id].AnimationFrames;
                var groups = id == 8 ? 8 : 1;
                return new { id, path = $"sprites/{id}.png", width = frames * 4, height = groups * 3, sha256 = new string('d', 64) };
            }).ToArray(),
            sprites,
            residents = CanonicalResidentTuples().Select((row, slot) => new {
                residentSlot = row.ResidentSlot,
                townerId = row.TownerId,
                positionX = row.PositionX,
                positionY = row.PositionY,
                nativeDirection = row.NativeDirection,
                spriteId = slot < 8 ? slot : 8,
            }).ToArray(),
        };
        return Repack(JsonNode.Parse(JsonSerializer.Serialize(document))!.AsObject());
    }

    private static string Repack(JsonObject root)
    {
        root["packId"] = string.Empty;
        root["packId"] = NativeTownerCatalog.CalculatePackId(root.ToJsonString());
        return root.ToJsonString();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "towners", "towners.tsv")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root for native Town catalog tests.");
    }

    private static (int ResidentSlot, int TownerId, int PositionX, int PositionY, int NativeDirection)[] CanonicalResidentTuples()
        => [
            (0, 0, 62, 63, 1),
            (1, 1, 55, 79, 7),
            (2, 3, 55, 62, 1),
            (3, 4, 62, 71, 0),
            (4, 5, 71, 84, 0),
            (5, 6, 80, 20, 0),
            (6, 7, 43, 66, 0),
            (7, 8, 11, 53, 0),
            (8, 9, 58, 16, 1),
            (9, 9, 56, 14, 3),
            (10, 9, 59, 20, 4),
        ];
}
