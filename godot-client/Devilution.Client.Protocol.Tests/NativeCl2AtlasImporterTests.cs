using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Tools.NativePlayerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeCl2AtlasImporterTests
{
    [Fact]
    public void DecodesDirectionOrderAndKeepsOpaquePaletteZeroSeparateFromTransparentRuns()
    {
        var sheet = NativeCl2SpriteSheetDecoder.Decode(BuildSheet(), frameWidth: 3, expectedFramesPerDirection: 1);

        Assert.Equal(8, sheet.Directions.Count);
        Assert.Single(sheet.Directions[0]);
        var south = sheet.Directions[0][0];
        Assert.Equal(3, south.Width);
        Assert.Equal(2, south.Height);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 7, 0 }, south.Indices);
        Assert.Equal(new byte[] { 255, 255, 255, 0, 255, 0 }, south.Opacity);
        Assert.Equal(2, sheet.Directions[1][0].Indices[0]);
        Assert.Equal(8, sheet.Directions[7][0].Indices[0]);
    }

    [Fact]
    public void ReencodesAdjacentOpaqueRunsAcrossRowsAndRetainsNativeRightEdgeSpill()
    {
        var stream = new byte[] { 0x02, 0xFF, 0x11, 0xFF, 0x22, 0x02 };
        var frame = NativeCl2SpriteSheetDecoder.Decode(BuildSheet(stream), frameWidth: 3, expectedFramesPerDirection: 1).Directions[0][0];

        Assert.Equal(4, frame.Width);
        Assert.Equal(3, frame.AnchorWidth);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new byte[] { 0, 0, 0x11, 0x22, 0, 0, 0, 0 }, frame.Indices);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0, 0, 0 }, frame.Opacity);
    }

    [Fact]
    public void ZeroWidthTransparentControlFlushesOpaqueRunBeforeTheNextSourceRow()
    {
        var stream = new byte[] { 0x02, 0xFF, 0x11, 0x00, 0xFF, 0x22, 0x02 };
        var frame = NativeCl2SpriteSheetDecoder.Decode(BuildSheet(stream), frameWidth: 3, expectedFramesPerDirection: 1).Directions[0][0];

        Assert.Equal(3, frame.Width);
        Assert.Equal(3, frame.AnchorWidth);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new byte[] { 0, 0, 0x11, 0x22, 0, 0 }, frame.Indices);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0 }, frame.Opacity);
    }

    [Fact]
    public void ReencodesSixtyThreePixelFillAndBoundsTheNativeSpillToThePaddedPitch()
    {
        var frame = NativeCl2SpriteSheetDecoder.Decode(BuildSheet([0x80, 0x2A]), frameWidth: 3, expectedFramesPerDirection: 1).Directions[0][0];

        Assert.Equal(63, frame.Width);
        Assert.Equal(3, frame.AnchorWidth);
        Assert.Equal(21, frame.Height);
        Assert.Equal(Enumerable.Repeat((byte)0x2A, 63), frame.Indices.Take(63));
        Assert.Equal(Enumerable.Repeat(byte.MaxValue, 63), frame.Opacity.Take(63));
        Assert.All(frame.Opacity.Skip(63), opacity => Assert.Equal((byte)0, opacity));
    }

    [Fact]
    public void RejectsMalformedOffsetsRunsCountsAndOversizedFrames()
    {
        var badTopOffset = BuildSheet();
        BinaryPrimitives.WriteUInt32LittleEndian(badTopOffset.AsSpan(sizeof(uint)), 32);
        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(badTopOffset, 3, 1));

        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(BuildSheet(firstStream: [0x80]), 3, 1));
        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(BuildSheet(firstStream: [0xFE, 9]), 3, 1));
        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(BuildSheet(), 3, 2));

        var oversizedRun = Enumerable.Repeat((byte)0x7F, 8261).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(BuildSheet(firstStream: oversizedRun), 3, 1));
    }

    [Fact]
    public void DecodesFrameOffsetsAfterAllEightDirectionHeaders()
    {
        var sheet = NativeCl2SpriteSheetDecoder.Decode(BuildHeadersFirstSheet(), frameWidth: 3, expectedFramesPerDirection: 1);

        Assert.Equal(8, sheet.Directions.Count);
        for (var direction = 0; direction < NativeCl2SpriteSheetDecoder.DirectionCount; direction++) {
            var frame = sheet.Directions[direction][0];
            Assert.Equal(1, frame.Height);
            Assert.Equal(new byte[] { (byte)direction, (byte)direction, (byte)direction }, frame.Indices);
            Assert.Equal(new byte[] { 255, 255, 255 }, frame.Opacity);
        }
    }

    [Fact]
    public void AcceptsAdditionalSourceFramesOnlyWhenExplicitlyRequested()
    {
        var data = BuildNativeWidthSheet(frameWidth: 3, frameCount: 2);

        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(data, 3, 1));
        var sheet = NativeCl2SpriteSheetDecoder.Decode(data, 3, 1, allowAdditionalFrames: true);
        Assert.All(sheet.Directions, direction => Assert.Equal(2, direction.Count));
        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(data, 3, 3, allowAdditionalFrames: true));
    }

    [Fact]
    public void RejectsFrameRangesThatOverlapLaterDirectionHeaders()
    {
        var data = BuildHeadersFirstSheet();
        var firstGroup = NativeCl2SpriteSheetDecoder.DirectionCount * sizeof(uint);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(firstGroup + sizeof(uint)), 3 * sizeof(uint));

        Assert.Throws<InvalidDataException>(() => NativeCl2SpriteSheetDecoder.Decode(data, frameWidth: 3, expectedFramesPerDirection: 1));
    }

    [Fact]
    public void RejectsAtlasAboveRuntimePixelBoundBeforePngAllocation()
    {
        const int width = 512;
        const int height = 513;
        var frameBytes = width * height;
        var directions = Enumerable.Range(0, NativeCl2SpriteSheetDecoder.DirectionCount)
            .Select(_ => (IReadOnlyList<NativeCl2Frame>)[
                new NativeCl2Frame(width, height, new byte[frameBytes], new byte[frameBytes]),
                new NativeCl2Frame(width, height, new byte[frameBytes], new byte[frameBytes]),
            ])
            .ToArray();

        Assert.Throws<InvalidDataException>(() => NativePlayerArtPng.EncodeAtlas(
            new NativeCl2Sheet(directions),
            NativePlayerPalette.Parse(new byte[256 * 3], "test palette")));
    }

    [Fact]
    public void EmitsDeterministicBottomAlignedPngWithOrderedTranslationsAndValidChunkCrcs()
    {
        var sheet = NativeCl2SpriteSheetDecoder.Decode(BuildSheet(), frameWidth: 3, expectedFramesPerDirection: 1);
        var paletteBytes = new byte[256 * 3];
        SetColor(paletteBytes, 0, 10, 20, 30);
        SetColor(paletteBytes, 1, 40, 50, 60);
        SetColor(paletteBytes, 7, 70, 80, 90);
        SetColor(paletteBytes, 8, 100, 110, 120);
        var graphicTranslation = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var classTranslation = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        graphicTranslation[0] = 7;
        classTranslation[7] = 1;

        var first = NativePlayerArtPng.EncodeAtlas(sheet, NativePlayerPalette.Parse(paletteBytes, "test palette"), graphicTranslation, classTranslation);
        var second = NativePlayerArtPng.EncodeAtlas(sheet, NativePlayerPalette.Parse(paletteBytes, "test palette"), graphicTranslation, classTranslation);
        Assert.Equal(first, second);

        var decoded = DecodePng(first);
        Assert.Equal((3, 16), (decoded.Width, decoded.Height));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 40, 50, 60, 255, 0, 0, 0, 0 }, decoded.Pixels.AsSpan(0, 12).ToArray());
        Assert.Equal(new byte[] { 40, 50, 60, 255, 40, 50, 60, 255, 40, 50, 60, 255 }, decoded.Pixels.AsSpan(12, 12).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, decoded.Pixels.AsSpan(24, 12).ToArray());
        Assert.All(decoded.Chunks, chunk => Assert.True(chunk.CrcValid, $"Invalid CRC in {chunk.Type} chunk."));
    }

    [Fact]
    public void ParsesNativeSpellGraphicsFromJoinedStableIdsAndMagicTypeFlags()
    {
        using var fixture = new ImportFixture();
        var metadata = NativePlayerArtMetadata.Load(fixture.TextDataRoot, fixture.GameContentRoot);

        Assert.Collection(metadata.Spells.OrderBy(spell => spell.SpellId),
            spell => { Assert.Equal(1u, spell.SpellId); Assert.Equal("fire", spell.Graphic); },
            spell => { Assert.Equal(5u, spell.SpellId); Assert.Equal("magic", spell.Graphic); },
            spell => { Assert.Equal(30u, spell.SpellId); Assert.Equal("lightning", spell.Graphic); });
        Assert.Contains(metadata.Diagnostics, diagnostic => diagnostic.Contains("nativeSpellId=4", StringComparison.Ordinal));

        fixture.WriteRuntimeSpell(99, 222);
        Assert.Throws<InvalidDataException>(() => NativePlayerArtMetadata.Load(fixture.TextDataRoot, fixture.GameContentRoot));
    }

    [Fact]
    public void PublishesPartialAvailableCatalogAndFailsCorruptOrEmptyImportsAtomically()
    {
        using var fixture = new ImportFixture();
        fixture.WriteSource("plrgfx/warrior/wln/wlnas.cl2", BuildNativeWidthSheet(frameWidth: 3, frameCount: 2));
        fixture.WritePalettes();
        var output = Path.Combine(fixture.Root, "published-native-players");
        var result = NativePlayerArtCompiler.Import(fixture.Options(output));

        Assert.Equal(1, result.ClipCount);
        Assert.NotEmpty(result.MissingAssets);
        Assert.True(File.Exists(Path.Combine(output, "native-player-catalog.json")));
        Assert.True(File.Exists(Path.Combine(output, "missing-assets.txt")));
        using (var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "native-player-catalog.json")))) {
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(new string('a', 64), root.GetProperty("contentManifestHashes")[0].GetString());
            var clip = Assert.Single(root.GetProperty("clips").EnumerateArray());
            Assert.Equal(3, clip.GetProperty("frameWidth").GetInt32());
            Assert.Equal(3, clip.GetProperty("anchorWidth").GetInt32());
            Assert.Equal(2, clip.GetProperty("framesPerDirection").GetInt32());
            Assert.Equal(1, clip.GetProperty("animationFrames").GetInt32());
            var levelBindings = root.GetProperty("levelBindings").EnumerateArray().ToArray();
            Assert.Equal(5, levelBindings.Length);
            Assert.Equal("town", levelBindings[0].GetProperty("location").GetString());
            var catalogText = File.ReadAllText(Path.Combine(output, "native-player-catalog.json"));
            Assert.Contains("\r\n", catalogText, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", catalogText.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Equal(0u, clip.GetProperty("classId").GetUInt32());
            Assert.Equal("cathedral", clip.GetProperty("location").GetString());
            Assert.Equal("stand", clip.GetProperty("graphic").GetString());
            Assert.Equal("sprites/0/cathedral/light/unarmed/stand.png", clip.GetProperty("path").GetString());
            Assert.Equal(3, root.GetProperty("spells").GetArrayLength());
        }

        using var corruptFixture = new ImportFixture();
        corruptFixture.WriteSource("plrgfx/warrior/wln/wlnas.cl2", [0x01, 0x02, 0x03]);
        corruptFixture.WritePalettes();
        var corruptOutput = Path.Combine(corruptFixture.Root, "must-not-publish");
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.Import(corruptFixture.Options(corruptOutput)));
        Assert.False(Directory.Exists(corruptOutput));
        Assert.Empty(Directory.EnumerateDirectories(corruptFixture.Root, ".must-not-publish.staging-*"));

        using var emptyFixture = new ImportFixture();
        emptyFixture.WritePalettes();
        var emptyOutput = Path.Combine(emptyFixture.Root, "must-not-publish-empty");
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.Import(emptyFixture.Options(emptyOutput)));
        Assert.False(Directory.Exists(emptyOutput));
    }

    [Fact]
    public void ImportsAClipWhenOptionalGraphicAndClassTranslationsAreAbsent()
    {
        using var fixture = new ImportFixture();
        fixture.WriteSource("plrgfx/warrior/wln/wlnas.cl2", BuildNativeWidthSheet(frameWidth: 3, frameCount: 2));
        fixture.WritePalettes();
        var output = Path.Combine(fixture.Root, "without-optional-translations");

        var result = NativePlayerArtCompiler.Import(fixture.Options(output));

        Assert.Equal(1, result.ClipCount);
        Assert.True(File.Exists(Path.Combine(output, "sprites/0/cathedral/light/unarmed/stand.png")));
    }

    [Fact]
    public void RequiresUniqueLowercaseContentManifestHashes()
    {
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateContentManifestHashes([]));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateContentManifestHashes([new string('A', 64)]));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateContentManifestHashes([new string('a', 64), new string('a', 64)]));
        Assert.Equal(new[] { new string('a', 64), new string('b', 64) }, NativePlayerArtCompiler.ValidateContentManifestHashes([new string('b', 64), new string('a', 64)]));
    }

    [Fact]
    public void QualifiedProfilesDeriveAndValidateExactContentLevelBindings()
    {
        using var fixture = new ProfileFixture();
        fixture.WriteProfile(levelTwoSymbol: "cathedral_depth_2");

        var profiles = NativePlayerArtProfiles.LoadDirectories([fixture.Root]);

        Assert.Equal(new[] { fixture.ManifestHash }, profiles.ContentManifestHashes);
        Assert.Equal(5, profiles.LevelBindings.Count);
        Assert.Equal(new NativePlayerArtLevelBinding(fixture.ManifestHash, 0, "town"), profiles.LevelBindings[0]);
        Assert.All(profiles.LevelBindings.Skip(1), binding => Assert.Equal("cathedral", binding.Location));

        fixture.WriteProfile(levelTwoSymbol: "catacombs");
        var exception = Assert.Throws<InvalidDataException>(() => NativePlayerArtProfiles.LoadDirectories([fixture.Root]));
        Assert.Contains("expected 'cathedral_depth_2'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyRogueDepthProfileRemainsAdmissibleWithoutClassBoundRuntimeTable()
    {
        using var fixture = new ProfileFixture();
        fixture.WriteProfile(levelTwoSymbol: "cathedral_depth_2", classBound: false);

        var profiles = NativePlayerArtProfiles.LoadDirectories([fixture.Root]);

        Assert.Single(profiles.ContentManifestHashes);
        Assert.Equal(5, profiles.LevelBindings.Count);
        Assert.Equal("town", profiles.LevelBindings[0].Location);
        Assert.All(profiles.LevelBindings.Skip(1), binding => Assert.Equal("cathedral", binding.Location));
    }

    [Fact]
    public void RejectsContentLevelBindingsWithoutAdmittedHashOrUniqueIdentity()
    {
        var admitted = new string('a', 64);
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateLevelBindings(
            [admitted],
            [new NativePlayerArtLevelBinding(new string('b', 64), 0, "town")]));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateLevelBindings(
            [admitted],
            [new NativePlayerArtLevelBinding(admitted, 0, "town"), new NativePlayerArtLevelBinding(admitted, 0, "cathedral")]));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtCompiler.ValidateLevelBindings(
            [admitted],
            [new NativePlayerArtLevelBinding(admitted, 0, "catacombs")]));
    }

    [Fact(Explicit = true)]
    public void OptionallyMatchesEveryExportedNativePlayerFrameReference()
    {
        var referenceRoot = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_PLAYER_ART_REFERENCE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(referenceRoot), "Set GODEVILUTION_NATIVE_PLAYER_ART_REFERENCE_DIRECTORY to the verified native export directory.");
        referenceRoot = Path.GetFullPath(referenceRoot!);
        Assert.True(Directory.Exists(referenceRoot), $"Native player reference directory is missing: {referenceRoot}");

        var textDataRoot = FindRepositoryPath("assets", "txtdata");
        var gameContentRoot = FindRepositoryPath("server", "content", "base");
        var metadata = NativePlayerArtMetadata.Load(textDataRoot, gameContentRoot);
        var inventory = Path.Combine(referenceRoot, "native-reference.tsv");
        Assert.True(File.Exists(inventory), $"Native player reference inventory is missing: {inventory}");
        var lines = File.ReadAllLines(inventory);
        Assert.Equal("class_id\tpath\twidth\tdirections\tframes", lines[0]);
        Assert.Equal(765, lines.Length - 1);
        var expectedAnimationFrameDifferences = new Dictionary<string, (int SourceFrameCount, int AnimationFrameCount)>(StringComparer.Ordinal) {
            ["plrgfx/warrior/wln/wlnlm.cl2"] = (21, 20),
            ["plrgfx/warrior/wmd/wmdlm.cl2"] = (21, 20),
            ["plrgfx/warrior/whm/whmat.cl2"] = (17, 16),
            ["plrgfx/rogue/rmt/rmtat.cl2"] = (18, 16),
            ["plrgfx/rogue/rhu/rhuht.cl2"] = (8, 7),
            ["plrgfx/rogue/rhu/rhuqm.cl2"] = (17, 16),
            ["plrgfx/rogue/rht/rhtat.cl2"] = (18, 16),
        };
        var perClass = new Dictionary<uint, int>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var comparedFrames = 0;
        foreach (var line in lines.Skip(1).Where(line => line.Length != 0)) {
            var fields = line.Split('\t');
            Assert.Equal(5, fields.Length);
            var classId = uint.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture);
            var relativePath = NormalizeSafeRelativePath(fields[1]);
            Assert.True(paths.Add(relativePath), $"Native reference inventory duplicates {relativePath}.");
            var width = int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture);
            var directionCount = int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture);
            var framesPerDirection = int.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(NativeCl2SpriteSheetDecoder.DirectionCount, directionCount);
            var sourcePath = Path.GetFullPath(Path.Combine(referenceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.StartsWith(Path.GetFullPath(referenceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, sourcePath, StringComparison.OrdinalIgnoreCase);
            var referencePath = sourcePath + ".native.bin";
            Assert.True(File.Exists(sourcePath), $"Reference source CL2 is missing: {sourcePath}");
            Assert.True(File.Exists(referencePath), $"Reference pixel capture is missing: {referencePath}");

            var matchingClasses = metadata.Classes.Where(entry => entry.ClassId == classId).ToArray();
            Assert.Single(matchingClasses);
            var playerClass = matchingClasses[0];
            var inventoryMetadata = ResolveInventoryMetadata(relativePath, playerClass);
            Assert.Equal(width, inventoryMetadata.FrameWidth);
            Assert.True(inventoryMetadata.FramesPerDirection <= framesPerDirection,
                $"Animation cycle has {inventoryMetadata.FramesPerDirection} frames but {relativePath} stores only {framesPerDirection} source frames.");
            if (expectedAnimationFrameDifferences.Remove(relativePath, out var knownDifference)) {
                Assert.Equal(knownDifference.SourceFrameCount, framesPerDirection);
                Assert.Equal(knownDifference.AnimationFrameCount, inventoryMetadata.FramesPerDirection);
            } else {
                Assert.Equal(inventoryMetadata.FramesPerDirection, framesPerDirection);
            }
            var graphicTranslation = ReadOptionalTranslation(Path.ChangeExtension(sourcePath, ".trn"));
            var classTranslation = ReadOptionalTranslation(Path.Combine(referenceRoot, "plrgfx", $"{playerClass.ClassTranslation}.trn"));
            var decoded = NativePlayerArtCompiler.DecodeAndApplyTranslations(
                File.ReadAllBytes(sourcePath),
                width,
                framesPerDirection,
                graphicTranslation,
                classTranslation);
            CompareReference(decoded, File.ReadAllBytes(referencePath), sourcePath);
            perClass[classId] = perClass.GetValueOrDefault(classId) + 1;
            comparedFrames = checked(comparedFrames + framesPerDirection * directionCount);
        }

        Assert.True(comparedFrames > 0, "No native player frames were compared.");
        Assert.Empty(expectedAnimationFrameDifferences);
        foreach (var playerClass in metadata.Classes)
            Assert.Equal(255, perClass.GetValueOrDefault(playerClass.ClassId));
    }

    private static NativePlayerClipMetadata ResolveInventoryMetadata(string relativePath, NativePlayerClassMetadata playerClass)
    {
        var segments = relativePath.Split('/');
        Assert.Equal(4, segments.Length);
        Assert.Equal("plrgfx", segments[0]);
        Assert.Equal(playerClass.SpritePath, segments[1]);

        var prefix = segments[2];
        Assert.Equal(3, prefix.Length);
        Assert.Equal(playerClass.ClassCharacter, prefix[0]);
        var armor = prefix[1] switch {
            'l' => "light",
            'm' => "medium",
            'h' => "heavy",
            _ => throw new InvalidDataException($"Unknown native armor code in {relativePath}."),
        };
        var weapon = prefix[2] switch {
            'n' => "unarmed",
            'u' => "unarmed-shield",
            's' => "sword",
            'd' => "sword-shield",
            'b' => "bow",
            'a' => "axe",
            'm' => "mace",
            'h' => "mace-shield",
            't' => "staff",
            _ => throw new InvalidDataException($"Unknown native weapon code in {relativePath}."),
        };

        var stem = Path.GetFileNameWithoutExtension(segments[3]);
        Assert.StartsWith(prefix, stem, StringComparison.Ordinal);
        var suffix = stem[prefix.Length..];
        var (location, graphic) = suffix switch {
            "st" => (NativePlayerArtNames.TownLocation, "town-stand"),
            "wl" => (NativePlayerArtNames.TownLocation, "town-walk"),
            "as" => (NativePlayerArtNames.CathedralLocation, "stand"),
            "aw" => (NativePlayerArtNames.CathedralLocation, "walk"),
            "at" => (NativePlayerArtNames.CathedralLocation, "attack"),
            "ht" => (NativePlayerArtNames.CathedralLocation, "hit"),
            "bl" => (NativePlayerArtNames.CathedralLocation, "block"),
            "lm" => (NativePlayerArtNames.CathedralLocation, "lightning"),
            "fm" => (NativePlayerArtNames.CathedralLocation, "fire"),
            "qm" => (NativePlayerArtNames.CathedralLocation, "magic"),
            "dt" => (NativePlayerArtNames.CathedralLocation, "death"),
            _ => throw new InvalidDataException($"Unknown native player animation suffix in {relativePath}."),
        };
        var clip = Assert.Single(NativePlayerClassMetadata.CreateClipMetadata(playerClass, location, graphic, suffix, weapon, armor));
        return clip;
    }

    private static byte[] BuildNativeWidthSheet(int frameWidth, int frameCount, byte color = 0x2A)
    {
        if (frameWidth is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameWidth)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (frameCount is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameCountPerDirection)
            throw new ArgumentOutOfRangeException(nameof(frameCount));

        var stream = new List<byte>();
        var remainingWidth = frameWidth;
        while (remainingWidth > 0) {
            var runLength = Math.Min(0x3F, remainingWidth);
            stream.Add(checked((byte)(0xBF - runLength)));
            stream.Add(color);
            remainingWidth -= runLength;
        }

        const int directoryBytes = NativeCl2SpriteSheetDecoder.DirectionCount * sizeof(uint);
        var frameBytes = checked(sizeof(ushort) + stream.Count);
        var groupHeaderBytes = checked((frameCount + 2) * sizeof(uint));
        var groupBytes = checked(groupHeaderBytes + frameCount * frameBytes);
        var result = new byte[checked(directoryBytes + NativeCl2SpriteSheetDecoder.DirectionCount * groupBytes)];
        for (var direction = 0; direction < NativeCl2SpriteSheetDecoder.DirectionCount; direction++) {
            var groupStart = checked(directoryBytes + direction * groupBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(direction * sizeof(uint)), checked((uint)groupStart));
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(groupStart), checked((uint)frameCount));
            for (var frameIndex = 0; frameIndex <= frameCount; frameIndex++) {
                var frameOffset = checked(groupHeaderBytes + frameIndex * frameBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    result.AsSpan(groupStart + sizeof(uint) * (frameIndex + 1)),
                    checked((uint)frameOffset));
            }

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++) {
                var frameStart = checked(groupStart + groupHeaderBytes + frameIndex * frameBytes);
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(frameStart), sizeof(ushort));
                for (var streamIndex = 0; streamIndex < stream.Count; streamIndex++)
                    result[frameStart + sizeof(ushort) + streamIndex] = stream[streamIndex];
            }
        }
        return result;
    }

    private static byte[] BuildSheet(byte[]? firstStream = null)
    {
        firstStream ??= [0xBC, 0x00, 0x01, 0xFF, 0x07, 0x01];
        var groups = new List<byte[]>(NativeCl2SpriteSheetDecoder.DirectionCount);
        for (var direction = 0; direction < NativeCl2SpriteSheetDecoder.DirectionCount; direction++) {
            byte[] stream = direction == 0 ? firstStream : new byte[] { 0xBC, checked((byte)(direction + 1)) };
            var frame = new byte[sizeof(ushort) + stream.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(frame, sizeof(ushort));
            stream.CopyTo(frame.AsSpan(sizeof(ushort)));
            var group = new byte[3 * sizeof(uint) + frame.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(group, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(sizeof(uint)), 3 * sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(2 * sizeof(uint)), checked((uint)group.Length));
            frame.CopyTo(group.AsSpan(3 * sizeof(uint)));
            groups.Add(group);
        }

        var directoryBytes = NativeCl2SpriteSheetDecoder.DirectionCount * sizeof(uint);
        var totalLength = directoryBytes + groups.Sum(group => group.Length);
        var result = new byte[totalLength];
        var nextOffset = directoryBytes;
        for (var direction = 0; direction < groups.Count; direction++) {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(direction * sizeof(uint)), checked((uint)nextOffset));
            groups[direction].CopyTo(result.AsSpan(nextOffset));
            nextOffset += groups[direction].Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)directoryBytes));
        return result;
    }

    private static byte[] BuildHeadersFirstSheet()
    {
        const int directoryBytes = NativeCl2SpriteSheetDecoder.DirectionCount * sizeof(uint);
        const int directionHeaderBytes = 3 * sizeof(uint);
        const int headersEnd = directoryBytes + NativeCl2SpriteSheetDecoder.DirectionCount * directionHeaderBytes;
        const int frameBytes = sizeof(ushort) + 2;
        var result = new byte[headersEnd + NativeCl2SpriteSheetDecoder.DirectionCount * frameBytes];
        for (var direction = 0; direction < NativeCl2SpriteSheetDecoder.DirectionCount; direction++) {
            var groupStart = directoryBytes + direction * directionHeaderBytes;
            var frameStart = headersEnd + direction * frameBytes;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(direction * sizeof(uint)), checked((uint)groupStart));
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(groupStart), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(groupStart + sizeof(uint)), checked((uint)(frameStart - groupStart)));
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(groupStart + 2 * sizeof(uint)), checked((uint)(frameStart + frameBytes - groupStart)));
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(frameStart), sizeof(ushort));
            result[frameStart + sizeof(ushort)] = 0xBC;
            result[frameStart + sizeof(ushort) + 1] = checked((byte)direction);
        }
        return result;
    }

    private static void SetColor(byte[] palette, int index, byte red, byte green, byte blue)
    {
        var offset = index * 3;
        palette[offset] = red;
        palette[offset + 1] = green;
        palette[offset + 2] = blue;
    }

    private static DecodedPng DecodePng(byte[] png)
    {
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.AsSpan(0, 8).ToArray());
        var offset = 8;
        var chunks = new List<DecodedChunk>();
        using var compressed = new MemoryStream();
        var width = 0;
        var height = 0;
        while (offset < png.Length) {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4)));
            offset += 4;
            var type = Encoding.ASCII.GetString(png, offset, 4);
            var typeBytes = png.AsSpan(offset, 4).ToArray();
            offset += 4;
            var data = png.AsSpan(offset, length).ToArray();
            offset += length;
            var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            offset += 4;
            var crcInput = typeBytes.Concat(data).ToArray();
            var crcValid = ComputeCrc32(crcInput) == actualCrc;
            chunks.Add(new DecodedChunk(type, crcValid));
            if (type == "IHDR") {
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
                Assert.Equal((byte)6, data[9]);
                Assert.Equal((byte)8, data[8]);
            } else if (type == "IDAT") {
                compressed.Write(data);
            }
        }

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var pixels = new MemoryStream();
        zlib.CopyTo(pixels);
        var filtered = pixels.ToArray();
        var scanlineBytes = checked(width * 4 + 1);
        Assert.Equal(checked(scanlineBytes * height), filtered.Length);
        var rgba = new byte[checked(width * height * 4)];
        for (var row = 0; row < height; row++) {
            var sourceRow = row * scanlineBytes;
            Assert.Equal((byte)0, filtered[sourceRow]);
            filtered.AsSpan(sourceRow + 1, width * 4).CopyTo(rgba.AsSpan(row * width * 4, width * 4));
        }
        return new DecodedPng(width, height, rgba, chunks);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var value = uint.MaxValue;
        foreach (var current in bytes) {
            value ^= current;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320U : value >> 1;
        }
        return ~value;
    }

    private static string FindRepositoryPath(params string[] segments)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }) {
            var directory = new DirectoryInfo(start);
            while (directory is not null) {
                var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException($"Cannot find repository path {Path.Combine(segments)}.");
    }

    private static string NormalizeSafeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        Assert.False(Path.IsPathRooted(normalized));
        Assert.DoesNotContain("..", normalized.Split('/'));
        Assert.DoesNotContain(".", normalized.Split('/'));
        Assert.DoesNotContain("", normalized.Split('/'));
        return normalized;
    }

    private static byte[]? ReadOptionalTranslation(string path)
    {
        if (!File.Exists(path))
            return null;
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(256, bytes.Length);
        return bytes;
    }

    private static void CompareReference(NativeCl2Sheet actual, byte[] reference, string sourcePath)
    {
        var offset = 0;
        var directions = ReadUInt32(reference, ref offset, sourcePath);
        Assert.Equal((uint)NativeCl2SpriteSheetDecoder.DirectionCount, directions);
        Assert.Equal(NativeCl2SpriteSheetDecoder.DirectionCount, actual.Directions.Count);
        for (var direction = 0; direction < checked((int)directions); direction++) {
            var frameCount = ReadUInt32(reference, ref offset, sourcePath);
            Assert.Equal((uint)actual.Directions[(int)direction].Count, frameCount);
            for (var frameIndex = 0; frameIndex < checked((int)frameCount); frameIndex++) {
                var width = checked((int)ReadUInt32(reference, ref offset, sourcePath));
                var height = checked((int)ReadUInt32(reference, ref offset, sourcePath));
                var frame = actual.Directions[(int)direction][(int)frameIndex];
                Assert.Equal((width, height), (frame.Width, frame.Height));
                var pixelCount = checked(width * height);
                Assert.InRange(pixelCount, 1, (int)NativeCl2SpriteSheetDecoder.MaximumDecodedPixelsPerSheet);
                Assert.True(offset <= reference.Length - pixelCount * 2, $"Reference frame pixels are truncated in {sourcePath}.");
                for (var topY = 0; topY < height; topY++) {
                    var sourceRow = (height - 1 - topY) * width;
                    for (var x = 0; x < width; x++) {
                        var pixel = sourceRow + x;
                        var index = reference[offset++];
                        var opacity = reference[offset++];
                        if (opacity is not 0 and not 255
                            || frame.Indices[pixel] != index
                            || frame.Opacity[pixel] != opacity) {
                            Assert.Fail($"Native frame mismatch in {sourcePath}: direction={direction}, frame={frameIndex}, x={x}, topY={topY}, expectedIndex={index}, actualIndex={frame.Indices[pixel]}, expectedOpacity={opacity}, actualOpacity={frame.Opacity[pixel]}.");
                        }
                    }
                }
            }
        }
        Assert.Equal(reference.Length, offset);
    }

    private static uint ReadUInt32(byte[] data, ref int offset, string description)
    {
        Assert.True(offset <= data.Length - sizeof(uint), $"Reference header is truncated in {description}.");
        var result = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return result;
    }

    private sealed record DecodedChunk(string Type, bool CrcValid);
    private sealed record DecodedPng(int Width, int Height, byte[] Pixels, IReadOnlyList<DecodedChunk> Chunks);

    private sealed class ImportFixture : IDisposable
    {
        private static readonly string[] AnimationKeys = [
            "unarmedFrames", "unarmedActionFrame", "unarmedShieldFrames", "unarmedShieldActionFrame",
            "swordFrames", "swordActionFrame", "swordShieldFrames", "swordShieldActionFrame",
            "bowFrames", "bowActionFrame", "axeFrames", "axeActionFrame", "maceFrames", "maceActionFrame",
            "maceShieldFrames", "maceShieldActionFrame", "staffFrames", "staffActionFrame", "idleFrames",
            "walkingFrames", "blockingFrames", "deathFrames", "castingFrames", "recoveryFrames",
            "townIdleFrames", "townWalkingFrames", "castingActionFrame",
        ];

        public ImportFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "godevilution-player-art-test-" + Guid.NewGuid().ToString("N"));
            SourceRoot = Path.Combine(Root, "source");
            TextDataRoot = Path.Combine(Root, "textdata");
            GameContentRoot = Path.Combine(Root, "game-content");
            Directory.CreateDirectory(SourceRoot);
            Directory.CreateDirectory(Path.Combine(TextDataRoot, "classes"));
            Directory.CreateDirectory(Path.Combine(TextDataRoot, "spells"));
            Directory.CreateDirectory(GameContentRoot);
            WriteClassMetadata();
            WriteGameMetadata();
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string TextDataRoot { get; }
        public string GameContentRoot { get; }

        public NativePlayerArtImportOptions Options(string outputRoot)
            => new(
                SourceRoot,
                TextDataRoot,
                GameContentRoot,
                outputRoot,
                "levels/towndata/town.pal",
                "levels/l1data/l1_1.pal",
                [new string('a', 64)],
                Enumerable.Range(0, 5)
                    .Select(levelId => new NativePlayerArtLevelBinding(new string('a', 64), checked((uint)levelId), levelId == 0 ? "town" : "cathedral"))
                    .ToArray());

        public void WriteSource(string relativePath, byte[] data)
        {
            var path = Path.Combine(SourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        }

        public void WritePalettes()
        {
            WriteSource("levels/towndata/town.pal", new byte[256 * 3]);
            WriteSource("levels/l1data/l1_1.pal", new byte[256 * 3]);
        }

        public void WriteRuntimeSpell(uint spellId, uint nativeSpellId)
        {
            var path = Path.Combine(GameContentRoot, "spells.tsv");
            var lines = File.ReadAllLines(path).ToList();
            lines.Add($"{spellId}\ttest_spell\tFire\t{nativeSpellId}");
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private void WriteClassMetadata()
        {
            File.WriteAllText(Path.Combine(TextDataRoot, "classes", "classdat.tsv"),
                "className\tfolderName\tportrait\tinv\r\nWarrior\twarrior\t0\tinv\r\nRogue\trogue\t1\tinv_rog\r\nSorcerer\tsorcerer\t2\tinv_sor\r\n",
                new UTF8Encoding(false));
            foreach (var playerClass in new[] { ("warrior", "warrior", 'w'), ("rogue", "rogue", 'r'), ("sorcerer", "sorceror", 's') }) {
                var classDirectory = Path.Combine(TextDataRoot, "classes", playerClass.Item1);
                Directory.CreateDirectory(classDirectory);
                File.WriteAllText(Path.Combine(classDirectory, "sprites.tsv"),
                    "Variable\tValue\r\n"
                    + $"classPath\t{playerClass.Item2}\r\nclassChar\t{playerClass.Item3}\r\ntrn\t{playerClass.Item1}\r\n"
                    + "stand\t3\r\nwalk\t3\r\nattack\t3\r\nbow\t3\r\nswHit\t3\r\nblock\t3\r\nlightning\t3\r\nfire\t3\r\nmagic\t3\r\ndeath\t3\r\n",
                    new UTF8Encoding(false));
                var animationText = "Variable\tValue\r\n" + string.Join("\r\n", AnimationKeys.Select(key => $"{key}\t{(key.EndsWith("Frames", StringComparison.Ordinal) ? 1 : 0)}")) + "\r\n";
                File.WriteAllText(Path.Combine(classDirectory, "animations.tsv"), animationText, new UTF8Encoding(false));
            }
        }

        private void WriteGameMetadata()
        {
            File.WriteAllText(Path.Combine(GameContentRoot, "native_spells.tsv"),
                "native_spell_id\tnative_spell_symbol\r\n0\tNull\r\n1\tFirebolt\r\n2\tHealing\r\n3\tLightning\r\n4\tFuture\r\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(TextDataRoot, "spells", "spelldat.tsv"),
                "id\tname\tsoundId\tbookCost10\tstaffCost10\tmanaCost\tflags\tbookLevel\tstaffLevel\tminIntelligence\tmissiles\tmanaMultiplier\tminMana\tstaffMin\tstaffMax\r\n"
                + "Firebolt\tFirebolt\tCastFire\t0\t0\t0\tFire,Targeted\t1\t1\t0\tFirebolt\t1\t0\t0\t0\r\n"
                + "Healing\tHealing\tCastHealing\t0\t0\t0\tMagic\t1\t1\t0\tHealing\t1\t0\t0\t0\r\n"
                + "Lightning\tLightning\tCastLightning\t0\t0\t0\tLightning\t1\t1\t0\tLightning\t1\t0\t0\t0\r\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(GameContentRoot, "spells.tsv"),
                "spell_id\tspell_symbol\tdamage_type\tnative_spell_id\r\n"
                + "1\tfirebolt\tLightning\t1\r\n"
                + "5\theal_spell\tFire\t2\r\n"
                + "30\tlightning_spell\tFire\t3\r\n"
                + "99\tfuture_spell\tMagic\t4\r\n",
                new UTF8Encoding(false));
        }
    }

    private sealed class ProfileFixture : IDisposable
    {
        public ProfileFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "godevilution-native-profile-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string ManifestHash { get; private set; } = string.Empty;

        public void WriteProfile(string levelTwoSymbol, bool classBound = true)
        {
            WritePack("content", "godevilution.cathedral-doors-playtest", "0.1.0",
                "level_id\tlevel_symbol\r\n0\ttown\r\n1\tcathedral\r\n2\t" + levelTwoSymbol
                + "\r\n3\tcathedral_depth_3\r\n4\tcathedral_depth_4\r\n", "levels.tsv");
            WritePack("native_cathedral_population", "godevilution.cathedral-native-population-metadata", "1.0.0", "key\tvalue\r\ntest\tpopulation\r\n");
            if (classBound) {
                WritePack("native_cathedral_runtime", "godevilution.cathedral-native-runtime", "1.0.0",
                    "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\r\n0\twarrior\t4480\t640\t10\t10\r\n",
                    "native_cathedral_runtime/native-character-start.tsv");
            } else {
                WritePack("native_cathedral_runtime", "godevilution.cathedral-native-runtime", "1.0.0", "key\tvalue\r\ntest\truntime\r\n");
            }
            var content = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "content"));
            var population = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "native_cathedral_population"));
            var runtime = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "native_cathedral_runtime"));
            ManifestHash = new ContentManifest(content.Id, content.Version, [content, population, runtime]).Sha256;
            var profile = new {
                formatVersion = classBound ? 4 : 3,
                adapterId = classBound ? NativePlayerArtProfiles.ClassProgressionAdapterId : NativePlayerArtProfiles.DepthProgressionAdapterId,
                contentDirectory = "content",
                contentId = content.Id,
                contentVersion = content.Version,
                contentManifestHash = ManifestHash,
                populationDirectory = "native_cathedral_population",
                populationPackId = population.Id,
                populationPackVersion = population.Version,
                runtimeDirectory = "native_cathedral_runtime",
                runtimePackId = runtime.Id,
                runtimePackVersion = runtime.Version,
                nativeDepth = 4,
                heroClassSymbol = classBound ? "warrior" : "rogue",
            };
            var profileFileName = classBound
                ? "cathedral-native-class-progression-playtest.json"
                : "cathedral-native-depth-progression-playtest.json";
            File.WriteAllText(Path.Combine(Root, profileFileName),
                JsonSerializer.Serialize(profile), new UTF8Encoding(false));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private void WritePack(string directoryName, string id, string version, string table, string tableFileName = "data.tsv")
        {
            var root = Path.Combine(Root, directoryName);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "content-pack.json"),
                JsonSerializer.Serialize(new { formatVersion = 1, id, version }), new UTF8Encoding(false));
            var tablePath = Path.Combine(root, tableFileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(tablePath)!);
            File.WriteAllText(tablePath, table, new UTF8Encoding(false));
        }
    }
}
