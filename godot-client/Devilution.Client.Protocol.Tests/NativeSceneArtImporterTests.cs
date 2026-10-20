using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeSceneArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeSceneArtImporterTests
{
    [Fact]
    public void SelectsEachTownAssetWithNativeNLevelsFallbackPrecedence()
    {
        var available = new HashSet<string>(StringComparer.Ordinal) {
            "nlevels/towndata/town.min",
            "levels/towndata/town.min",
            "levels/towndata/town.cel",
        };

        Assert.Equal("nlevels/towndata/town.min",
            NativeSceneArtCompiler.SelectTownSourcePath("town.min", available.Contains));
        Assert.Equal("levels/towndata/town.cel",
            NativeSceneArtCompiler.SelectTownSourcePath("town.cel", available.Contains));
        Assert.Throws<InvalidDataException>(() => NativeSceneArtCompiler.SelectTownSourcePath("../town.min", available.Contains));
    }

    [Fact]
    public void DecodesMinSwizzleAllSixTileEncodingsAndFloorFoliageOpacity()
    {
        var input = BuildSyntheticFamily();
        var family = NativeSceneArtDecoder.DecodeFamily(
            "town", input.Cel, input.Min, input.Til, input.Sol, input.Palette);

        Assert.Equal((byte)16, family.MicroTileLength);
        var piece = Assert.Single(family.Pieces);
        Assert.Equal(16, piece.Slots.Count);
        Assert.True(NativeSceneArtDecoder.IsFloor(piece.Properties));
        Assert.Equal(6, piece.Slots.Where(slot => slot.SourceRawWord != 0).Select(slot => slot.SourceType).Distinct().Count());

        var leftFloor = piece.Slots[0];
        Assert.Equal(NativeSceneTileType.TransparentSquare, leftFloor.SourceType);
        Assert.Equal(NativeSceneTileType.LeftTriangle, leftFloor.DrawType);
        Assert.Equal((ushort)3, leftFloor.SourceFrameId);
        Assert.Equal((ushort)2, leftFloor.AdjustedFrameId);
        Assert.Equal((ushort)0x1003, leftFloor.SourceRawWord);
        Assert.Equal((ushort)0x1002, leftFloor.AdjustedRawWord);
        var floorRaster = Assert.IsType<NativeSceneRaster>(leftFloor.FloorRaster);
        var foliageRaster = Assert.IsType<NativeSceneRaster>(leftFloor.ContentRaster);
        Assert.Equal((byte)0, floorRaster.Indices[31 * 32 + 30]);
        Assert.Equal(byte.MaxValue, floorRaster.Opacity[31 * 32 + 30]);
        Assert.Equal((byte)0, foliageRaster.Indices[0]);
        Assert.Equal((byte)0, foliageRaster.Opacity[0]);
        Assert.Equal((byte)2, foliageRaster.Indices[1]);
        Assert.Equal(byte.MaxValue, foliageRaster.Opacity[1]);
        Assert.Equal((byte)0, foliageRaster.Indices[15]);
        Assert.Equal(byte.MaxValue, foliageRaster.Opacity[15]);
        Assert.Equal((byte)1, foliageRaster.Indices[17]);
        Assert.Equal((byte)15, foliageRaster.Indices[31]);

        var rightFloor = piece.Slots[1];
        Assert.Equal(NativeSceneTileType.RightTriangle, rightFloor.DrawType);
        Assert.Equal((ushort)4, rightFloor.SourceFrameId);
        Assert.Equal((ushort)3, rightFloor.AdjustedFrameId);
        Assert.IsType<NativeSceneRaster>(rightFloor.FloorRaster);
        Assert.IsType<NativeSceneRaster>(rightFloor.ContentRaster);

        var firstReferencedAfterGap = piece.Slots[6];
        Assert.Equal((ushort)9, firstReferencedAfterGap.SourceFrameId);
        Assert.Equal((ushort)8, firstReferencedAfterGap.AdjustedFrameId);
        Assert.Equal((ushort)9, firstReferencedAfterGap.SourceRawWord);
        Assert.Equal((ushort)8, firstReferencedAfterGap.AdjustedRawWord);

        var untransformedTransparentSquare = piece.Slots[7];
        Assert.Equal((ushort)1, untransformedTransparentSquare.SourceFrameId);
        Assert.Equal((ushort)1, untransformedTransparentSquare.AdjustedFrameId);
        Assert.Equal(NativeSceneTileType.TransparentSquare,
            family.Frames.Single(frame => frame.SourceFrameId == 1).StoredType);
        Assert.Null(untransformedTransparentSquare.FloorRaster);
        Assert.NotNull(untransformedTransparentSquare.ContentRaster);
    }

    [Fact]
    public void FoliageLiteralFlushAfterZeroDropsFirstColorAndKeepsOpaqueZero()
    {
        var input = BuildSyntheticFamily();
        var family = NativeSceneArtDecoder.DecodeFamily(
            "town", input.Cel, input.Min, input.Til, input.Sol, input.Palette);
        var foliage = Assert.IsType<NativeSceneRaster>(family.Pieces[0].Slots[0].ContentRaster);

        Assert.Equal((byte)2, foliage.Indices[1]);
        Assert.Equal(byte.MaxValue, foliage.Opacity[1]);
        Assert.Equal((byte)0, foliage.Indices[15]);
        Assert.Equal(byte.MaxValue, foliage.Opacity[15]);
        Assert.Equal((byte)1, foliage.Indices[17]);
        Assert.Equal((byte)15, foliage.Indices[31]);
    }

    [Fact]
    public void AcceptsNativeTransparentSquareAlignmentPaddingAndCanonicalRightTriangleTail()
    {
        var baselineInput = BuildSyntheticFamily();
        var baseline = NativeSceneArtDecoder.DecodeFamily(
            "town", baselineInput.Cel, baselineInput.Min, baselineInput.Til, baselineInput.Sol, baselineInput.Palette);

        for (var padding = 0; padding <= 3; padding++) {
            var source = BuildSyntheticFamily(transparentSquarePadding: padding, rightTrianglePadding: 2);
            var decoded = NativeSceneArtDecoder.DecodeFamily("town", source.Cel, source.Min, source.Til, source.Sol, source.Palette);
            var expectedFrame = baseline.Frames.Single(frame => frame.SourceFrameId == 6).StoredRaster;
            var actualFrame = decoded.Frames.Single(frame => frame.SourceFrameId == 6).StoredRaster;
            Assert.Equal(expectedFrame.Indices, actualFrame.Indices);
            Assert.Equal(expectedFrame.Opacity, actualFrame.Opacity);
        }

        var overPadded = BuildSyntheticFamily(transparentSquarePadding: 4);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", overPadded.Cel, overPadded.Min, overPadded.Til, overPadded.Sol, overPadded.Palette));
        var overlongTriangle = BuildSyntheticFamily(rightTrianglePadding: 4);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", overlongTriangle.Cel, overlongTriangle.Min, overlongTriangle.Til, overlongTriangle.Sol, overlongTriangle.Palette));
    }

    [Fact]
    public void DecodesSpecialCelRowsBottomUpAndKeepsTransparentPaletteZeroSeparate()
    {
        var frame = new byte[1 + 64 + 1];
        frame[0] = 64;
        for (var x = 0; x < 64; x++)
            frame[1 + x] = checked((byte)(x + 1));
        frame[^1] = 0xC0;

        var raster = Assert.Single(NativeSceneArtDecoder.DecodeSpecials(BuildCelFile([frame])));

        Assert.Equal(64, raster.Width);
        Assert.Equal(2, raster.Height);
        Assert.Equal((byte)0, raster.Indices[0]);
        Assert.Equal((byte)0, raster.Opacity[0]);
        Assert.Equal((byte)1, raster.Indices[64]);
        Assert.Equal(byte.MaxValue, raster.Opacity[64]);
        Assert.Equal((byte)64, raster.Indices[127]);
    }

    [Fact]
    public void RejectsInvalidMinTileTypesCelOffsetsSolLengthsAndSpecialRuns()
    {
        var valid = BuildSyntheticFamily();
        var invalidType = valid.Min.ToArray();
        var diskIndex = 16 - 2 + (2 & 1) - (2 & 0xE);
        BinaryPrimitives.WriteUInt16LittleEndian(invalidType.AsSpan(diskIndex * 2), (ushort)((6 << 12) | 5));
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", valid.Cel, invalidType, valid.Til, valid.Sol, valid.Palette));

        var invalidCel = valid.Cel.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidCel.AsSpan(sizeof(uint)), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", invalidCel, valid.Min, valid.Til, valid.Sol, valid.Palette));

        var invalidTil = valid.Til.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalidTil, 1);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", valid.Cel, valid.Min, invalidTil, valid.Sol, valid.Palette));

        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", valid.Cel, valid.Min, valid.Til, ReadOnlyMemory<byte>.Empty, valid.Palette));
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeSpecials(BuildCelFile([[0xBF]])));
    }

    [Fact]
    public void PreservesUnusedNativeSolBitsAndPadsShortSolTables()
    {
        var input = BuildSyntheticFamily();
        var sol = new byte[] { 0x40 };

        var family = NativeSceneArtDecoder.DecodeFamily(
            "town", input.Cel, input.Min, input.Til, sol, input.Palette);

        Assert.Equal((byte)0x40, family.Pieces[0].Properties);
        Assert.Single(family.Pieces);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeFamily(
            "town", input.Cel, input.Min, input.Til, ReadOnlyMemory<byte>.Empty, input.Palette));
    }

    [Fact]
    public void ImportsDeterministicallyBindsQualifiedProfileIdentityAndFailsAtomically()
    {
        using var fixture = new SceneImportFixture();
        var firstOutput = Path.Combine(fixture.Root, "published-one");
        var secondOutput = Path.Combine(fixture.Root, "published-two");
        var first = NativeSceneArtCompiler.Import(fixture.Options(firstOutput));
        var second = NativeSceneArtCompiler.Import(fixture.Options(secondOutput));

        Assert.Equal(first.PackId, second.PackId);
        AssertDirectoriesIdentical(firstOutput, secondOutput);
        using (var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(firstOutput, "native-scene-catalog.json")))) {
            var root = catalog.RootElement;
            Assert.Equal(first.PackId, root.GetProperty("packId").GetString());
            Assert.Equal(new[] { fixture.ManifestHash }, root.GetProperty("contentManifestHashes").EnumerateArray().Select(value => value.GetString()!).ToArray());
            var bindings = root.GetProperty("levelBindings").EnumerateArray().ToArray();
            Assert.Equal(5, bindings.Length);
            Assert.Equal("town", bindings[0].GetProperty("location").GetString());
            Assert.All(bindings.Skip(1), binding => Assert.Equal("cathedral", binding.GetProperty("location").GetString()));
            var townSources = root.GetProperty("families").EnumerateArray()
                .Single(family => family.GetProperty("location").GetString() == "town")
                .GetProperty("sourceHashes").EnumerateArray().Select(source => source.GetProperty("path").GetString()).ToArray();
            Assert.Contains("nlevels/towndata/town.min", townSources);
            Assert.Contains("nlevels/towndata/town.sol", townSources);
        }

        var existingOutput = Path.Combine(fixture.Root, "existing");
        Directory.CreateDirectory(existingOutput);
        var sentinelPath = Path.Combine(existingOutput, "keep.txt");
        File.WriteAllText(sentinelPath, "preserve", Encoding.UTF8);
        Assert.Throws<IOException>(() => NativeSceneArtCompiler.Import(fixture.Options(existingOutput)));
        Assert.Equal("preserve", File.ReadAllText(sentinelPath));

        var changedPalette = new byte[NativeSceneArtDecoder.PaletteByteCount];
        changedPalette[3] = 1;
        fixture.WriteSource("levels/towndata/town.pal", changedPalette);
        var changed = NativeSceneArtCompiler.Import(fixture.Options(Path.Combine(fixture.Root, "changed-identity")));
        Assert.NotEqual(first.PackId, changed.PackId);

        using var malformed = new SceneImportFixture();
        malformed.WriteSource("levels/l1data/l1.til", [1, 2, 3]);
        var failedOutput = Path.Combine(malformed.Root, "malformed-output");
        Assert.Throws<InvalidDataException>(() => NativeSceneArtCompiler.Import(malformed.Options(failedOutput)));
        Assert.False(Directory.Exists(failedOutput));
        Assert.Empty(Directory.EnumerateDirectories(malformed.Root, ".malformed-output.staging-*"));
    }

    [Fact]
    public void PublishesMixedHeightSpecialSpritesBottomAlignedInTheAtlas()
    {
        using var fixture = new SceneImportFixture();
        var output = Path.Combine(fixture.Root, "mixed-specials");
        NativeSceneArtCompiler.Import(fixture.Options(output));
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "native-scene-catalog.json")));

        foreach (var family in catalog.RootElement.GetProperty("families").EnumerateArray()) {
            var specials = family.GetProperty("specials").EnumerateArray().ToArray();
            Assert.Equal(new uint[] { 0, 1 }, specials.Select(item => item.GetProperty("specialId").GetUInt32()).ToArray());
            var shortReference = specials[0].GetProperty("raster");
            var tallReference = specials[1].GetProperty("raster");
            Assert.Equal(1, shortReference.GetProperty("height").GetInt32());
            Assert.Equal(2, tallReference.GetProperty("height").GetInt32());
            Assert.Equal(shortReference.GetProperty("atlasId").GetInt32(), tallReference.GetProperty("atlasId").GetInt32());
            Assert.Equal(0, shortReference.GetProperty("x").GetInt32());
            Assert.Equal(64, tallReference.GetProperty("x").GetInt32());
            Assert.Equal(1, shortReference.GetProperty("y").GetInt32());
            Assert.Equal(0, tallReference.GetProperty("y").GetInt32());

            var atlasId = shortReference.GetProperty("atlasId").GetInt32();
            var atlas = family.GetProperty("atlases").EnumerateArray()
                .Single(item => item.GetProperty("id").GetInt32() == atlasId);
            var png = DecodePng(File.ReadAllBytes(Path.Combine(output, atlas.GetProperty("path").GetString()!)));
            Assert.Equal((byte)0, png.Rgba[0 + 3]);
            var palette = BuildPalette();
            Assert.Equal(new byte[] { palette[129 * 3], palette[129 * 3 + 1], palette[129 * 3 + 2], 255 }, png.Rgba.AsSpan(64 * 4, 4).ToArray());
            Assert.Equal(new byte[] { palette[3], palette[4], palette[5], 255 }, png.Rgba.AsSpan((png.Width + 0) * 4, 4).ToArray());
            Assert.Equal(new byte[] { palette[65 * 3], palette[65 * 3 + 1], palette[65 * 3 + 2], 255 }, png.Rgba.AsSpan((png.Width + 64) * 4, 4).ToArray());
        }
    }

    [Fact]
    public void RejectsOversizedPaletteAndIncompleteMinBeforePublishing()
    {
        using (var paletteFixture = new SceneImportFixture()) {
            paletteFixture.WriteSource("levels/towndata/town.pal", new byte[NativeSceneArtDecoder.PaletteByteCount + 1]);
            var output = Path.Combine(paletteFixture.Root, "oversized-palette");
            Assert.Throws<InvalidDataException>(() => NativeSceneArtCompiler.Import(paletteFixture.Options(output)));
            Assert.False(Directory.Exists(output));
        }

        using (var minFixture = new SceneImportFixture()) {
            minFixture.WriteSource("nlevels/towndata/town.min", new byte[16 * sizeof(ushort) - 1]);
            var output = Path.Combine(minFixture.Root, "incomplete-min");
            Assert.Throws<InvalidDataException>(() => NativeSceneArtCompiler.Import(minFixture.Options(output)));
            Assert.False(Directory.Exists(output));
        }
    }

    [Fact(Explicit = true)]
    public void OptionallyMatchesEveryPublishedAtlasPixelAndPaddingToNativeReferences()
    {
        var referenceRoot = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(referenceRoot), "Set GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY to the private extracted source and native-reference fixture root.");
        var packRoot = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_PACK_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(packRoot), "Set GODEVILUTION_NATIVE_SCENE_PACK_DIRECTORY to the generated scene pack directory.");
        referenceRoot = Path.GetFullPath(referenceRoot!);
        packRoot = Path.GetFullPath(packRoot!);
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(packRoot, "native-scene-catalog.json")));
        var families = catalog.RootElement.GetProperty("families").EnumerateArray().ToArray();
        Assert.Equal(new[] { "cathedral", "town" }, families.Select(family => family.GetProperty("location").GetString()).OrderBy(location => location, StringComparer.Ordinal).ToArray());

        foreach (var familyElement in families)
            ComparePublishedFamily(referenceRoot, packRoot, familyElement);
    }

    [Fact]
    public void EnforcesGroupedSpecialFrameCountAndEveryGroupOffset()
    {
        var valid = BuildGroupedCel(groupCount: 2, framesPerGroup: 2);
        Assert.Equal(4, NativeSceneArtDecoder.DecodeSpecials(valid).Count);

        var invalidOffset = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidOffset.AsSpan(sizeof(uint)), 9);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeSpecials(invalidOffset));

        var excessiveFrameGroups = BuildGroupedCel(groupCount: 2, framesPerGroup: 2049);
        Assert.Throws<InvalidDataException>(() => NativeSceneArtDecoder.DecodeSpecials(excessiveFrameGroups));
    }

    [Fact]
    public void AlignsShortCelCellsToTheBottomOfMixedHeightAtlases()
    {
        var palette = new byte[NativeSceneArtDecoder.PaletteByteCount];
        SetColor(palette, 1, 10, 20, 30);
        SetColor(palette, 2, 40, 50, 60);
        var shortCell = new NativeSceneRaster(64, 1, Enumerable.Repeat((byte)1, 64).ToArray(), Enumerable.Repeat(byte.MaxValue, 64).ToArray());
        var tallCell = new NativeSceneRaster(64, 2, Enumerable.Repeat((byte)2, 128).ToArray(), Enumerable.Repeat(byte.MaxValue, 128).ToArray());

        var decoded = DecodePng(NativeSceneArtPng.EncodeAtlas([shortCell, tallCell], 2, 64, 2, 0, palette, bottomAlignCells: true));

        Assert.Equal((128, 2), (decoded.Width, decoded.Height));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, decoded.Rgba.AsSpan(0, 4).ToArray());
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, decoded.Rgba.AsSpan(128 * 4, 4).ToArray());
        Assert.Equal(new byte[] { 40, 50, 60, 255 }, decoded.Rgba.AsSpan(64 * 4, 4).ToArray());
    }

    [Fact]
    public void EmitsDeterministicPaletteBakedPngWithTransparentAndOpaqueZeroHandledSeparately()
    {
        var palette = new byte[NativeSceneArtDecoder.PaletteByteCount];
        SetColor(palette, 0, 10, 20, 30);
        SetColor(palette, 7, 40, 50, 60);
        var raster = new NativeSceneRaster(2, 1, [0, 7], [byte.MaxValue, 0]);

        var first = NativeSceneArtPng.EncodeAtlas([raster], 1, 2, 1, 0, palette);
        var second = NativeSceneArtPng.EncodeAtlas([raster], 1, 2, 1, 0, palette);
        var decoded = DecodePng(first);

        Assert.Equal(first, second);
        Assert.Equal((2, 1), (decoded.Width, decoded.Height));
        Assert.Equal(new byte[] { 10, 20, 30, 255, 0, 0, 0, 0 }, decoded.Rgba);
    }

    [Fact(Explicit = true)]
    public void OptionallyMatchesEveryNativeTownAndCathedralPieceAndSpecialReference()
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(root), "Set GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY to the private extracted source and native-reference fixture root.");
        root = Path.GetFullPath(root!);

        var allTypes = new HashSet<NativeSceneTileType>();
        CompareFamily(root, "town", "levels/towndata/town.cel", "levels/towndata/town.min",
            "levels/towndata/town.til", "levels/towndata/town.sol", "levels/towndata/town.pal", "levels/towndata/towns.cel",
            expectedMicroTileLength: 16, allTypes);
        CompareFamily(root, "cathedral", "levels/l1data/l1.cel", "levels/l1data/l1.min",
            "levels/l1data/l1.til", "levels/l1data/l1.sol", "levels/l1data/l1_1.pal", "levels/l1data/l1s.cel",
            expectedMicroTileLength: 10, allTypes);
        Assert.Equal(Enum.GetValues<NativeSceneTileType>().OrderBy(type => type), allTypes.OrderBy(type => type));
    }

    private static void CompareFamily(
        string root,
        string location,
        string celPath,
        string minPath,
        string tilPath,
        string solPath,
        string palettePath,
        string specialPath,
        byte expectedMicroTileLength,
        HashSet<NativeSceneTileType> allTypes)
    {
        var cel = ReadNativeSource(root, location, celPath, allowTownNLevels: true);
        var min = ReadNativeSource(root, location, minPath, allowTownNLevels: true);
        var til = ReadNativeSource(root, location, tilPath, allowTownNLevels: true);
        var sol = ReadNativeSource(root, location, solPath, allowTownNLevels: true);
        var palette = File.ReadAllBytes(Path.Combine(root, palettePath.Replace('/', Path.DirectorySeparatorChar)));
        var specialCel = File.ReadAllBytes(Path.Combine(root, specialPath.Replace('/', Path.DirectorySeparatorChar)));
        var family = NativeSceneArtDecoder.DecodeFamily(location, cel, min, til, sol, palette);
        var specials = NativeSceneArtDecoder.DecodeSpecials(specialCel);
        Assert.Equal(min.Length / (expectedMicroTileLength * sizeof(ushort)), family.Pieces.Count);
        var referenceDirectory = Path.Combine(root, location == "town" ? "Town" : "Cathedral");
        ComparePerSlotReference(family, File.ReadAllBytes(Path.Combine(referenceDirectory, "native-reference.bin")), expectedMicroTileLength);
        CompareComposedPieceReference(family, File.ReadAllBytes(Path.Combine(referenceDirectory, "native-pieces.bin")));
        CompareSpecialReference(specials, File.ReadAllBytes(Path.Combine(referenceDirectory, "native-specials.bin")));
        foreach (var slot in family.Pieces.SelectMany(piece => piece.Slots)) {
            if (slot.SourceRawWord != 0)
                allTypes.Add(slot.SourceType);
        }
    }

    private static byte[] ReadNativeSource(string root, string location, string fallbackPath, bool allowTownNLevels)
    {
        var relativePath = fallbackPath;
        if (location == "town" && allowTownNLevels) {
            var fileName = Path.GetFileName(fallbackPath);
            relativePath = NativeSceneArtCompiler.SelectTownSourcePath(fileName, path =>
                File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
        }
        return File.ReadAllBytes(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void ComparePublishedFamily(string referenceRoot, string packRoot, JsonElement familyElement)
    {
        var location = familyElement.GetProperty("location").GetString()!;
        var celPath = location == "town" ? "levels/towndata/town.cel" : "levels/l1data/l1.cel";
        var minPath = location == "town" ? "levels/towndata/town.min" : "levels/l1data/l1.min";
        var tilPath = location == "town" ? "levels/towndata/town.til" : "levels/l1data/l1.til";
        var solPath = location == "town" ? "levels/towndata/town.sol" : "levels/l1data/l1.sol";
        var palettePath = location == "town" ? "levels/towndata/town.pal" : "levels/l1data/l1_1.pal";
        var specialPath = location == "town" ? "levels/towndata/towns.cel" : "levels/l1data/l1s.cel";
        var cel = ReadNativeSource(referenceRoot, location, celPath, allowTownNLevels: true);
        var min = ReadNativeSource(referenceRoot, location, minPath, allowTownNLevels: true);
        var til = ReadNativeSource(referenceRoot, location, tilPath, allowTownNLevels: true);
        var sol = ReadNativeSource(referenceRoot, location, solPath, allowTownNLevels: true);
        var palette = File.ReadAllBytes(Path.Combine(referenceRoot, palettePath.Replace('/', Path.DirectorySeparatorChar)));
        var specialCel = File.ReadAllBytes(Path.Combine(referenceRoot, specialPath.Replace('/', Path.DirectorySeparatorChar)));
        var decoded = NativeSceneArtDecoder.DecodeFamily(location, cel, min, til, sol, palette);
        var specials = NativeSceneArtDecoder.DecodeSpecials(specialCel);
        AssertCatalogSelectorsAndProvenance(
            referenceRoot, familyElement, decoded, specials, location,
            celPath, minPath, tilPath, solPath, palettePath, specialPath);
        var placementsByAtlas = new Dictionary<int, List<PublishedRasterPlacement>>();

        foreach (var frameElement in familyElement.GetProperty("frames").EnumerateArray()) {
            var sourceFrameId = frameElement.GetProperty("sourceFrameId").GetUInt16();
            var frame = decoded.Frames.Single(value => value.SourceFrameId == sourceFrameId);
            AddPublishedRaster(placementsByAtlas, frameElement.GetProperty("storedRaster"), frame.StoredRaster,
                $"{location} source frame {sourceFrameId}");
            var foliageElement = frameElement.GetProperty("foliageRaster");
            if (foliageElement.ValueKind == JsonValueKind.Null) {
                Assert.Null(frame.FoliageRaster);
            } else {
                Assert.NotNull(frame.FoliageRaster);
                AddPublishedRaster(placementsByAtlas, foliageElement, frame.FoliageRaster!,
                    $"{location} source frame {sourceFrameId} foliage");
            }
        }

        foreach (var slotElement in familyElement.GetProperty("minSlots").EnumerateArray()) {
            var pieceId = slotElement.GetProperty("pieceId").GetUInt32();
            var slotIndex = slotElement.GetProperty("slot").GetByte();
            var slot = decoded.Pieces.Single(piece => piece.PieceId == pieceId).Slots[slotIndex];
            var floorElement = slotElement.GetProperty("floorRaster");
            if (floorElement.ValueKind == JsonValueKind.Null) {
                Assert.Null(slot.FloorRaster);
            } else {
                Assert.NotNull(slot.FloorRaster);
                AddPublishedRaster(placementsByAtlas, floorElement, slot.FloorRaster!,
                    $"{location} piece {pieceId} slot {slotIndex} floor");
            }
            var rasterElement = slotElement.GetProperty("raster");
            if (rasterElement.ValueKind == JsonValueKind.Null) {
                Assert.Null(slot.ContentRaster);
            } else {
                Assert.NotNull(slot.ContentRaster);
                AddPublishedRaster(placementsByAtlas, rasterElement, slot.ContentRaster!,
                    $"{location} piece {pieceId} slot {slotIndex} content");
            }
        }

        foreach (var specialElement in familyElement.GetProperty("specials").EnumerateArray()) {
            var specialId = specialElement.GetProperty("specialId").GetUInt32();
            Assert.True(specialId < checked((uint)specials.Count));
            AddPublishedRaster(placementsByAtlas, specialElement.GetProperty("raster"), specials[checked((int)specialId)],
                $"{location} special {specialId}");
        }

        var atlases = familyElement.GetProperty("atlases").EnumerateArray().ToArray();
        Assert.NotEmpty(atlases);
        foreach (var atlas in atlases) {
            var atlasId = atlas.GetProperty("id").GetInt32();
            var atlasWidth = atlas.GetProperty("width").GetInt32();
            var atlasHeight = atlas.GetProperty("height").GetInt32();
            var cellWidth = atlas.GetProperty("cellWidth").GetInt32();
            var cellHeight = atlas.GetProperty("cellHeight").GetInt32();
            var cellCount = atlas.GetProperty("cellCount").GetInt32();
            Assert.True(placementsByAtlas.TryGetValue(atlasId, out var placements), $"Atlas {location}/{atlasId} has no catalog raster references.");
            Assert.Equal(cellCount, placements!
                .Select(placement => (placement.X / cellWidth, placement.Y / cellHeight))
                .Distinct()
                .Count());
            var relativePath = atlas.GetProperty("path").GetString()!;
            var pngPath = Path.GetFullPath(Path.Combine(packRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var packPrefix = packRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(packPrefix, pngPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            var pngBytes = File.ReadAllBytes(pngPath);
            Assert.Equal(atlas.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant());
            var png = DecodePng(pngBytes);
            Assert.Equal((atlasWidth, atlasHeight), (png.Width, png.Height));
            var expectedRgba = new byte[png.Rgba.Length];
            foreach (var placement in placements!) {
                Assert.True(placement.X >= 0 && placement.Y >= 0
                    && placement.X + placement.Raster.Width <= atlasWidth
                    && placement.Y + placement.Raster.Height <= atlasHeight,
                    $"Raster {placement.Description} escapes atlas {location}/{atlasId}.");
                for (var y = 0; y < placement.Raster.Height; y++) {
                    for (var x = 0; x < placement.Raster.Width; x++) {
                        var sourcePixel = y * placement.Raster.Width + x;
                        if (placement.Raster.Opacity[sourcePixel] == 0)
                            continue;
                        var color = placement.Raster.Indices[sourcePixel] * 3;
                        var targetPixel = ((placement.Y + y) * atlasWidth + placement.X + x) * 4;
                        expectedRgba[targetPixel] = palette[color];
                        expectedRgba[targetPixel + 1] = palette[color + 1];
                        expectedRgba[targetPixel + 2] = palette[color + 2];
                        expectedRgba[targetPixel + 3] = byte.MaxValue;
                    }
                }
            }
            Assert.Equal(expectedRgba, png.Rgba);
        }
    }

    private static void AssertCatalogSelectorsAndProvenance(
        string referenceRoot,
        JsonElement familyElement,
        NativeSceneDecodedFamily decoded,
        IReadOnlyList<NativeSceneRaster> specials,
        string location,
        string celPath,
        string minPath,
        string tilPath,
        string solPath,
        string palettePath,
        string specialPath)
    {
        Assert.Equal(decoded.Location, familyElement.GetProperty("location").GetString());
        Assert.Equal(decoded.MicroTileLength, familyElement.GetProperty("microTileLength").GetByte());
        Assert.Equal(decoded.Pieces.Count, familyElement.GetProperty("pieceCount").GetInt32());

        var catalogFrames = familyElement.GetProperty("frames").EnumerateArray().ToArray();
        Assert.Equal(decoded.Frames.Count, catalogFrames.Length);
        Assert.Equal(decoded.Frames.Select(frame => frame.SourceFrameId).Order(),
            catalogFrames.Select(frame => frame.GetProperty("sourceFrameId").GetUInt16()).Order());
        var framesById = catalogFrames.ToDictionary(frame => frame.GetProperty("sourceFrameId").GetUInt16());
        foreach (var frame in decoded.Frames) {
            var catalogFrame = framesById[frame.SourceFrameId];
            Assert.Equal(frame.AdjustedFrameId, catalogFrame.GetProperty("adjustedFrameId").GetUInt16());
            Assert.Equal(frame.FirstUsePieceId, catalogFrame.GetProperty("firstUsePieceId").GetUInt32());
            Assert.Equal(frame.FirstUseSlotIndex, catalogFrame.GetProperty("firstUseSlotIndex").GetByte());
            Assert.Equal(TileTypeName(frame.SourceType), catalogFrame.GetProperty("sourceType").GetString());
            Assert.Equal(TileTypeName(frame.StoredType), catalogFrame.GetProperty("storedType").GetString());
            Assert.Equal(frame.IsFloorTransform, catalogFrame.GetProperty("isFloorTransform").GetBoolean());
        }

        var catalogPieces = familyElement.GetProperty("dPieces").EnumerateArray().ToArray();
        Assert.Equal(decoded.Pieces.Count, catalogPieces.Length);
        Assert.Equal(decoded.Pieces.Select(piece => piece.PieceId),
            catalogPieces.Select(piece => piece.GetProperty("pieceId").GetUInt32()));
        foreach (var piece in decoded.Pieces) {
            var catalogPiece = catalogPieces[checked((int)piece.PieceId)];
            Assert.Equal(piece.Properties, catalogPiece.GetProperty("solFlags").GetByte());
            Assert.Equal(piece.IsFloorPiece ? "floor" : "content", catalogPiece.GetProperty("floorClass").GetString());
        }

        var catalogMegaTiles = familyElement.GetProperty("megaTiles").EnumerateArray().ToArray();
        Assert.Equal(decoded.MegaTiles.Count, catalogMegaTiles.Length);
        foreach (var megaTile in decoded.MegaTiles) {
            var catalogMegaTile = catalogMegaTiles[checked((int)megaTile.MegaTileId)];
            Assert.Equal(megaTile.MegaTileId, catalogMegaTile.GetProperty("megaTileId").GetUInt32());
            Assert.Equal(megaTile.PieceIds, catalogMegaTile.GetProperty("pieceIds").EnumerateArray().Select(id => id.GetUInt16()));
        }

        var catalogSlots = familyElement.GetProperty("minSlots").EnumerateArray().ToArray();
        var decodedSlots = decoded.Pieces.SelectMany(piece => piece.Slots.Select(slot => (piece.PieceId, Slot: slot))).ToArray();
        Assert.Equal(decodedSlots.Length, catalogSlots.Length);
        var slotsByKey = catalogSlots.ToDictionary(slot => (slot.GetProperty("pieceId").GetUInt32(), slot.GetProperty("slot").GetByte()));
        foreach (var (pieceId, slot) in decodedSlots) {
            var catalogSlot = slotsByKey[(pieceId, slot.SlotIndex)];
            Assert.Equal(slot.SourceRawWord, catalogSlot.GetProperty("sourceWord").GetUInt16());
            Assert.Equal(slot.AdjustedRawWord, catalogSlot.GetProperty("remappedWord").GetUInt16());
            Assert.Equal(slot.SourceFrameId, catalogSlot.GetProperty("sourceFrameId").GetUInt16());
            Assert.Equal(slot.AdjustedFrameId, catalogSlot.GetProperty("adjustedFrameId").GetUInt16());
            Assert.Equal(TileTypeName(slot.SourceType), catalogSlot.GetProperty("sourceType").GetString());
            var firstUseType = slot.SourceFrameId == 0 ? "none" : TileTypeName(slot.FirstUseFrameType);
            Assert.Equal(firstUseType, catalogSlot.GetProperty("firstUseType").GetString());
            Assert.Equal(TileTypeName(slot.DrawType), catalogSlot.GetProperty("effectiveType").GetString());
            var expectedRole = slot.ContentRaster is null ? "none" : slot.DrawFoliage ? "foliage" : "content";
            Assert.Equal(expectedRole, catalogSlot.GetProperty("role").GetString());
        }

        var catalogSpecials = familyElement.GetProperty("specials").EnumerateArray().ToArray();
        Assert.Equal(specials.Count, catalogSpecials.Length);
        Assert.Equal(Enumerable.Range(0, specials.Count).Select(value => checked((uint)value)),
            catalogSpecials.Select(item => item.GetProperty("specialId").GetUInt32()));

        var expectedPaths = new[] {
            ResolveNativeSourcePath(referenceRoot, location, celPath),
            ResolveNativeSourcePath(referenceRoot, location, minPath),
            ResolveNativeSourcePath(referenceRoot, location, tilPath),
            ResolveNativeSourcePath(referenceRoot, location, solPath),
            palettePath,
            specialPath,
        }.ToHashSet(StringComparer.Ordinal);
        var sourceHashes = familyElement.GetProperty("sourceHashes").EnumerateArray().ToArray();
        var actualPaths = sourceHashes.Select(source => source.GetProperty("path").GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(expectedPaths.Count, sourceHashes.Length);
        Assert.True(expectedPaths.SetEquals(actualPaths), $"{location} source hashes do not identify exactly its six native inputs.");
        foreach (var source in sourceHashes) {
            var path = source.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(referenceRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), source.GetProperty("sha256").GetString());
        }
    }

    private static string ResolveNativeSourcePath(string root, string location, string fallbackPath)
    {
        if (location != "town")
            return fallbackPath;
        var fileName = Path.GetFileName(fallbackPath);
        return NativeSceneArtCompiler.SelectTownSourcePath(fileName, path =>
            File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static string TileTypeName(NativeSceneTileType type)
        => type switch {
            NativeSceneTileType.Square => "square",
            NativeSceneTileType.TransparentSquare => "transparent-square",
            NativeSceneTileType.LeftTriangle => "left-triangle",
            NativeSceneTileType.RightTriangle => "right-triangle",
            NativeSceneTileType.LeftTrapezoid => "left-trapezoid",
            NativeSceneTileType.RightTrapezoid => "right-trapezoid",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static void AddPublishedRaster(
        Dictionary<int, List<PublishedRasterPlacement>> placementsByAtlas,
        JsonElement reference,
        NativeSceneRaster expectedRaster,
        string description)
    {
        var atlasId = reference.GetProperty("atlasId").GetInt32();
        var x = reference.GetProperty("x").GetInt32();
        var y = reference.GetProperty("y").GetInt32();
        Assert.Equal(expectedRaster.Width, reference.GetProperty("width").GetInt32());
        Assert.Equal(expectedRaster.Height, reference.GetProperty("height").GetInt32());
        if (!placementsByAtlas.TryGetValue(atlasId, out var placements)) {
            placements = [];
            placementsByAtlas.Add(atlasId, placements);
        }
        placements.Add(new PublishedRasterPlacement(x, y, expectedRaster, description));
    }

    private static void ComparePerSlotReference(NativeSceneDecodedFamily family, byte[] data, byte expectedMicroTileLength)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream);
        Assert.Equal(1U, reader.ReadUInt32());
        var pieceCount = reader.ReadUInt32();
        var microTileLength = reader.ReadUInt32();
        Assert.Equal((uint)family.Pieces.Count, pieceCount);
        Assert.Equal((uint)expectedMicroTileLength, microTileLength);
        Assert.Equal((byte)expectedMicroTileLength, family.MicroTileLength);

        foreach (var piece in family.Pieces) {
            Assert.Equal(piece.PieceId, reader.ReadUInt32());
            Assert.Equal((uint)piece.Properties, reader.ReadUInt32());
            foreach (var slot in piece.Slots) {
                var sourceWord = reader.ReadUInt32();
                var remappedWord = reader.ReadUInt32();
                Assert.Equal((uint)slot.SourceRawWord, sourceWord);
                Assert.Equal((uint)slot.AdjustedRawWord, remappedWord);
                CompareFixedRaster(reader, slot.FloorRaster, 32, 32,
                    $"{family.Location} piece {piece.PieceId} slot {slot.SlotIndex} floor");
                CompareFixedRaster(reader, slot.ContentRaster, 32, 32,
                    $"{family.Location} piece {piece.PieceId} slot {slot.SlotIndex} content");
            }
        }
        Assert.Equal(stream.Length, stream.Position);
    }

    private static void CompareComposedPieceReference(NativeSceneDecodedFamily family, byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream);
        Assert.Equal(1U, reader.ReadUInt32());
        Assert.Equal((uint)family.Pieces.Count, reader.ReadUInt32());
        Assert.Equal((uint)family.MicroTileLength, reader.ReadUInt32());
        foreach (var piece in family.Pieces) {
            Assert.Equal(piece.PieceId, reader.ReadUInt32());
            var floor = NativeSceneRaster.Clear(64, 256);
            var content = NativeSceneRaster.Clear(64, 256);
            foreach (var slot in piece.Slots) {
                var x = slot.SlotIndex % 2 * 32;
                var y = 224 - slot.SlotIndex / 2 * 32;
                if (slot.FloorRaster is not null)
                    CopyRaster(slot.FloorRaster, floor, x, y);
                if (slot.ContentRaster is not null)
                    CopyRaster(slot.ContentRaster, content, x, y);
            }
            CompareFixedRaster(reader, floor, 64, 256, $"{family.Location} piece {piece.PieceId} floor composition");
            CompareFixedRaster(reader, content, 64, 256, $"{family.Location} piece {piece.PieceId} content composition");
        }
        Assert.Equal(stream.Length, stream.Position);
    }

    private static void CompareSpecialReference(IReadOnlyList<NativeSceneRaster> specials, byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream);
        Assert.Equal(1U, reader.ReadUInt32());
        Assert.Equal((uint)specials.Count, reader.ReadUInt32());
        for (var index = 0; index < specials.Count; index++) {
            var expected = specials[index];
            Assert.Equal((uint)expected.Width, reader.ReadUInt32());
            Assert.Equal((uint)expected.Height, reader.ReadUInt32());
            CompareFixedRaster(reader, expected, expected.Width, expected.Height, $"special {index}");
        }
        Assert.Equal(stream.Length, stream.Position);
    }

    private static void CompareFixedRaster(BinaryReader reader, NativeSceneRaster? actual, int width, int height, string description)
    {
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var expectedIndex = reader.ReadByte();
                var expectedOpacity = reader.ReadByte();
                var actualIndex = (byte)0;
                var actualOpacity = (byte)0;
                if (actual is not null && y < actual.Height && x < actual.Width) {
                    var pixel = y * actual.Width + x;
                    actualIndex = actual.Indices[pixel];
                    actualOpacity = actual.Opacity[pixel];
                }
                if (actualIndex != expectedIndex || actualOpacity != expectedOpacity)
                    Assert.Fail($"Native scene pixel mismatch at {description}, ({x},{y}): expected index/opacity {expectedIndex}/{expectedOpacity}, got {actualIndex}/{actualOpacity}.");
            }
        }
    }

    private static void CopyRaster(NativeSceneRaster source, NativeSceneRaster target, int x, int y)
    {
        for (var sourceY = 0; sourceY < source.Height; sourceY++) {
            for (var sourceX = 0; sourceX < source.Width; sourceX++) {
                var sourceIndex = sourceY * source.Width + sourceX;
                if (source.Opacity[sourceIndex] == 0)
                    continue;
                var targetIndex = (y + sourceY) * target.Width + x + sourceX;
                target.Indices[targetIndex] = source.Indices[sourceIndex];
                target.Opacity[targetIndex] = byte.MaxValue;
            }
        }
    }

    private static SyntheticFamily BuildSyntheticFamily(int transparentSquarePadding = 0, int rightTrianglePadding = 0)
    {
        if (transparentSquarePadding is < 0 or > 4 || rightTrianglePadding is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(transparentSquarePadding));
        var square = Enumerable.Range(0, 1024).Select(value => checked((byte)(value % 251))).ToArray();
        var floorLeftPixels = MakePixels();
        floorLeftPixels[31 * 32 + 30] = 0;
        var floorRightPixels = MakePixels(seed: 3);
        var frames = new[] {
            EncodeTransparentSquare(MakePixels(seed: 1)),
            square,
            EncodeTransparentSquare(floorLeftPixels),
            EncodeTransparentSquare(floorRightPixels),
            BuildLeftTriangle(),
            BuildRightTriangle(),
            BuildLeftTrapezoid(),
            BuildRightTrapezoid(),
            square,
        };
        if (transparentSquarePadding != 0) {
            foreach (var frameIndex in new[] { 0, 2, 3 })
                frames[frameIndex] = [.. frames[frameIndex], .. Enumerable.Repeat((byte)0xA5, transparentSquarePadding)];
        }
        if (rightTrianglePadding != 0)
            frames[5] = [.. frames[5], .. Enumerable.Repeat((byte)0x5A, rightTrianglePadding)];
        var min = new byte[16 * sizeof(ushort)];
        SetMinWord(min, 16, 0, PackWord(NativeSceneTileType.TransparentSquare, 3));
        SetMinWord(min, 16, 1, PackWord(NativeSceneTileType.TransparentSquare, 4));
        SetMinWord(min, 16, 2, PackWord(NativeSceneTileType.LeftTriangle, 5));
        SetMinWord(min, 16, 3, PackWord(NativeSceneTileType.RightTriangle, 6));
        SetMinWord(min, 16, 4, PackWord(NativeSceneTileType.LeftTrapezoid, 7));
        SetMinWord(min, 16, 5, PackWord(NativeSceneTileType.RightTrapezoid, 8));
        SetMinWord(min, 16, 6, PackWord(NativeSceneTileType.Square, 9));
        SetMinWord(min, 16, 7, PackWord(NativeSceneTileType.TransparentSquare, 1));
        var sol = new byte[1];
        var palette = Enumerable.Range(0, NativeSceneArtDecoder.PaletteByteCount).Select(value => (byte)value).ToArray();
        return new SyntheticFamily(BuildCelFile(frames), min, new byte[8], sol, palette);
    }

    private static byte[] BuildCelFile(IReadOnlyList<byte[]> frames)
    {
        var tableLength = checked((frames.Count + 2) * sizeof(uint));
        var totalLength = checked(tableLength + frames.Sum(frame => frame.Length));
        var data = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(data, checked((uint)frames.Count));
        var offset = tableLength;
        for (var index = 0; index < frames.Count; index++) {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sizeof(uint) * (index + 1)), checked((uint)offset));
            frames[index].CopyTo(data, offset);
            offset += frames[index].Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sizeof(uint) * (frames.Count + 1)), checked((uint)offset));
        return data;
    }

    private static byte[] BuildGroupedCel(int groupCount, int framesPerGroup)
    {
        var groups = new byte[groupCount][];
        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++) {
            var tableLength = checked((framesPerGroup + 2) * sizeof(uint));
            var group = new byte[checked(tableLength + framesPerGroup)];
            BinaryPrimitives.WriteUInt32LittleEndian(group, checked((uint)framesPerGroup));
            for (var frameIndex = 0; frameIndex < framesPerGroup; frameIndex++) {
                BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(sizeof(uint) * (frameIndex + 1)), checked((uint)(tableLength + frameIndex)));
                group[tableLength + frameIndex] = 0xC0;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(sizeof(uint) * (framesPerGroup + 1)), checked((uint)group.Length));
            groups[groupIndex] = group;
        }

        var headerLength = checked(groupCount * sizeof(uint));
        var result = new byte[checked(headerLength + groups.Sum(group => group.Length))];
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)headerLength));
        var cursor = headerLength;
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++) {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(sizeof(uint) * groupIndex), checked((uint)cursor));
            groups[groupIndex].CopyTo(result, cursor);
            cursor += groups[groupIndex].Length;
        }
        return result;
    }

    private static byte[] EncodeTransparentSquare(byte[] topDownPixels)
    {
        var output = new List<byte>(NativeSceneArtDecoder.FrameWidth * NativeSceneArtDecoder.FrameHeight * 2);
        for (var y = 31; y >= 0; y--) {
            output.Add(32);
            output.AddRange(topDownPixels.AsSpan(y * 32, 32).ToArray());
        }
        return [.. output];
    }

    private static byte[] BuildLeftTriangle()
    {
        var bytes = new List<byte>(544);
        var width = 0;
        for (var row = 0; row < 8; row++) {
            bytes.AddRange([0, 0]);
            width += 2;
            AddPixels(bytes, width, (byte)(row + 1));
            width += 2;
            AddPixels(bytes, width, (byte)(row + 11));
        }
        width = 32;
        for (var row = 0; row < 7; row++) {
            bytes.AddRange([0, 0]);
            width -= 2;
            AddPixels(bytes, width, (byte)(row + 21));
            width -= 2;
            AddPixels(bytes, width, (byte)(row + 31));
        }
        bytes.AddRange([0, 0]);
        width -= 2;
        AddPixels(bytes, width, 99);
        return [.. bytes];
    }

    private static byte[] BuildRightTriangle()
    {
        var bytes = new List<byte>(542);
        var width = 0;
        for (var row = 0; row < 8; row++) {
            width += 2;
            AddPixels(bytes, width, (byte)(row + 1));
            bytes.AddRange([0, 0]);
            width += 2;
            AddPixels(bytes, width, (byte)(row + 11));
        }
        width = 32;
        for (var row = 0; row < 7; row++) {
            width -= 2;
            AddPixels(bytes, width, (byte)(row + 21));
            bytes.AddRange([0, 0]);
            width -= 2;
            AddPixels(bytes, width, (byte)(row + 31));
        }
        width -= 2;
        AddPixels(bytes, width, 99);
        return [.. bytes];
    }

    private static byte[] BuildLeftTrapezoid()
    {
        var bytes = new List<byte>(800);
        var width = 0;
        for (var row = 0; row < 8; row++) {
            bytes.AddRange([0, 0]);
            width += 2;
            AddPixels(bytes, width, (byte)(row + 1));
            width += 2;
            AddPixels(bytes, width, (byte)(row + 11));
        }
        AddPixels(bytes, 512, 77);
        return [.. bytes];
    }

    private static byte[] BuildRightTrapezoid()
    {
        var bytes = new List<byte>(800);
        var width = 0;
        for (var row = 0; row < 8; row++) {
            width += 2;
            AddPixels(bytes, width, (byte)(row + 1));
            bytes.AddRange([0, 0]);
            width += 2;
            AddPixels(bytes, width, (byte)(row + 11));
        }
        AddPixels(bytes, 512, 77);
        return [.. bytes];
    }

    private static byte[] MakePixels(int seed = 0)
        => Enumerable.Range(0, 1024).Select(value => checked((byte)((value + seed) % 16))).ToArray();

    private static void AddPixels(List<byte> bytes, int length, byte value)
    {
        for (var index = 0; index < length; index++)
            bytes.Add(value);
    }

    private static ushort PackWord(NativeSceneTileType type, ushort frameId)
        => checked((ushort)(((ushort)type << 12) | frameId));

    private static void SetMinWord(byte[] min, int microTileLength, int slot, ushort word)
    {
        var diskIndex = microTileLength - 2 + (slot & 1) - (slot & 0xE);
        BinaryPrimitives.WriteUInt16LittleEndian(min.AsSpan(diskIndex * 2), word);
    }

    private static void SetColor(byte[] palette, int color, byte red, byte green, byte blue)
    {
        var offset = color * 3;
        palette[offset] = red;
        palette[offset + 1] = green;
        palette[offset + 2] = blue;
    }

    private static DecodedPng DecodePng(byte[] data)
    {
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, data.AsSpan(0, 8).ToArray());
        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4)));
        var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4)));
        using var compressed = new MemoryStream();
        var offset = 8;
        while (offset < data.Length) {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)));
            var type = data.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("IDAT"u8))
                compressed.Write(data, offset + 8, length);
            offset = checked(offset + length + 12);
            if (type.SequenceEqual("IEND"u8))
                break;
        }
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var rgba = new MemoryStream();
        zlib.CopyTo(rgba);
        var bytes = rgba.ToArray();
        var stride = checked(width * 4 + 1);
        Assert.Equal(checked(stride * height), bytes.Length);
        for (var y = 0; y < height; y++)
            Assert.Equal((byte)0, bytes[y * stride]);
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
            bytes.AsSpan(y * stride + 1, width * 4).CopyTo(pixels.AsSpan(y * width * 4));
        return new DecodedPng(width, height, pixels);
    }

    private static void AssertDirectoriesIdentical(string firstRoot, string secondRoot)
    {
        var firstFiles = Directory.EnumerateFiles(firstRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(firstRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var secondFiles = Directory.EnumerateFiles(secondRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(secondRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(firstFiles, secondFiles);
        foreach (var relativePath in firstFiles) {
            var firstPath = Path.Combine(firstRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var secondPath = Path.Combine(secondRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        }
    }

    private static byte[] BuildSpecialCel()
    {
        var shortFrame = new byte[65];
        shortFrame[0] = 64;
        for (var index = 0; index < 64; index++)
            shortFrame[index + 1] = checked((byte)(index + 1));

        var tallFrame = new byte[130];
        tallFrame[0] = 64;
        tallFrame[65] = 64;
        for (var index = 0; index < 64; index++) {
            tallFrame[index + 1] = checked((byte)(index + 65));
            tallFrame[index + 66] = checked((byte)(index + 129));
        }
        return BuildCelFile([shortFrame, tallFrame]);
    }

    private static byte[] BuildPalette()
    {
        var palette = new byte[NativeSceneArtDecoder.PaletteByteCount];
        for (var index = 0; index < 256; index++)
            SetColor(palette, index, checked((byte)index), checked((byte)(255 - index)), checked((byte)(index / 2)));
        return palette;
    }

    private sealed record PublishedRasterPlacement(int X, int Y, NativeSceneRaster Raster, string Description);

    private sealed record SyntheticFamily(byte[] Cel, byte[] Min, byte[] Til, byte[] Sol, byte[] Palette);
    private sealed record DecodedPng(int Width, int Height, byte[] Rgba);

    private sealed class SceneImportFixture : IDisposable
    {
        private const string ContentId = "godevilution.cathedral-doors-playtest";

        public SceneImportFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "godevilution-native-scene-test-" + Guid.NewGuid().ToString("N"));
            SourceRoot = Path.Combine(Root, "source");
            ProfileRoot = Path.Combine(Root, "profile");
            Directory.CreateDirectory(SourceRoot);
            Directory.CreateDirectory(ProfileRoot);
            WriteNativeSources();
            WriteProfile();
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string ProfileRoot { get; }
        public string ManifestHash { get; private set; } = string.Empty;

        public NativeSceneArtImportOptions Options(string outputRoot)
            => new(SourceRoot, outputRoot,
                "levels/towndata/town.pal", "levels/l1data/l1_1.pal", [ProfileRoot]);

        public void WriteSource(string relativePath, byte[] bytes)
        {
            var path = Path.Combine(SourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private void WriteNativeSources()
        {
            var town = BuildSyntheticFamily(rightTrianglePadding: 2);
            WriteSource("levels/towndata/town.cel", town.Cel);
            WriteSource("nlevels/towndata/town.min", town.Min);
            WriteSource("levels/towndata/town.min", new byte[16 * sizeof(ushort)]);
            WriteSource("levels/towndata/town.til", town.Til);
            WriteSource("nlevels/towndata/town.sol", town.Sol);
            WriteSource("levels/towndata/town.sol", [NativeSceneArtDecoder.SolidFlag]);
            WriteSource("levels/towndata/town.pal", BuildPalette());
            WriteSource("levels/towndata/towns.cel", BuildSpecialCel());

            WriteSource("levels/l1data/l1.cel", BuildCelFile([[0x80]]));
            WriteSource("levels/l1data/l1.min", new byte[10 * sizeof(ushort)]);
            WriteSource("levels/l1data/l1.til", new byte[8]);
            WriteSource("levels/l1data/l1.sol", [0]);
            WriteSource("levels/l1data/l1_1.pal", BuildPalette());
            WriteSource("levels/l1data/l1s.cel", BuildSpecialCel());
        }

        private void WriteProfile()
        {
            var contentDirectory = Path.Combine(ProfileRoot, "content");
            var populationDirectory = Path.Combine(ProfileRoot, "population");
            var runtimeDirectory = Path.Combine(ProfileRoot, "runtime");
            WritePack(contentDirectory, ContentId, "0.1.0",
                "level_id\tlevel_symbol\r\n0\ttown\r\n1\tcathedral\r\n2\tcathedral_depth_2\r\n3\tcathedral_depth_3\r\n4\tcathedral_depth_4\r\n",
                "levels.tsv");
            WritePack(populationDirectory, "godevilution.cathedral-native-population-metadata", "1.0.0",
                "key\tvalue\r\ntest\tpopulation\r\n");
            WritePack(runtimeDirectory, "godevilution.cathedral-native-runtime", "1.0.0",
                "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\r\n"
                + "0\twarrior\t4480\t640\t10\t10\r\n", "native_cathedral_runtime/native-character-start.tsv");

            var content = ContentPackLoader.LoadManifestDirectory(contentDirectory);
            var population = ContentPackLoader.LoadManifestDirectory(populationDirectory);
            var runtime = ContentPackLoader.LoadManifestDirectory(runtimeDirectory);
            ManifestHash = new ContentManifest(content.Id, content.Version, [content, population, runtime]).Sha256;
            var profile = new {
                formatVersion = 4,
                adapterId = NativePlayerArtProfiles.ClassProgressionAdapterId,
                contentDirectory = "content",
                contentId = content.Id,
                contentVersion = content.Version,
                contentManifestHash = ManifestHash,
                populationDirectory = "population",
                populationPackId = population.Id,
                populationPackVersion = population.Version,
                runtimeDirectory = "runtime",
                runtimePackId = runtime.Id,
                runtimePackVersion = runtime.Version,
                nativeDepth = 4,
                heroClassSymbol = "warrior",
            };
            File.WriteAllText(Path.Combine(ProfileRoot, "cathedral-native-class-progression-playtest.json"),
                JsonSerializer.Serialize(profile), new UTF8Encoding(false));
        }

        private static void WritePack(string directory, string id, string version, string table, string tablePath = "data.tsv")
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "content-pack.json"),
                JsonSerializer.Serialize(new { formatVersion = 1, id, version }), new UTF8Encoding(false));
            var tableFile = Path.Combine(directory, tablePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(tableFile)!);
            File.WriteAllText(tableFile, table, new UTF8Encoding(false));
        }
    }
}
