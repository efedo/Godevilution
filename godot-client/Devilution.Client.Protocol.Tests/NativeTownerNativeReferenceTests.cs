using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Opt-in pixel qualification against the original native Town CEL renderer and palette. */
public sealed class NativeTownerNativeReferenceTests
{
    private const string ReferenceDirectoryVariable = "GODEVILUTION_NATIVE_TOWNER_ART_REFERENCE_DIRECTORY";
    private const string PackDirectoryVariable = "GODEVILUTION_NATIVE_TOWNER_ART_PACK_DIRECTORY";
    private const int MaximumReferenceBytes = 256 * 1024 * 1024;
    private const int MaximumPngBytes = 128 * 1024 * 1024;

    private static readonly ExpectedSprite[] ExpectedSprites = [
        new(0, 96, 1, 16, 3, "towners/smith/smithn.cel"),
        new(1, 96, 1, 20, 6, "towners/healer/healer.cel"),
        new(2, 96, 1, 16, 3, "towners/twnf/twnfn.cel"),
        new(3, 96, 1, 25, 3, "towners/strytell/strytell.cel"),
        new(4, 96, 1, 18, 3, "towners/drunk/twndrunk.cel"),
        new(5, 96, 1, 19, 6, "towners/townwmn1/witch.cel"),
        new(6, 96, 1, 18, 6, "towners/townwmn1/wmnn.cel"),
        new(7, 96, 1, 20, 6, "towners/townboy/pegkid1.cel"),
        new(8, 128, 8, 12, 3, "towners/animals/cow.cel"),
    ];

    [Fact(Explicit = true)]
    public void DecodesEveryIndexedAndOpacityPixelAgainstNativeCelBlitterReference()
    {
        var referenceRoot = RequireDirectory(ReferenceDirectoryVariable);
        var sourceRoot = Path.Combine(referenceRoot, "source");
        var nativeSprites = ReadNativeReference(referenceRoot);
        Assert.Equal(9, nativeSprites.Count);
        Assert.Equal(248, ExpectedSprites.Sum(sprite => sprite.AnimationFrames * sprite.DirectionCount));

        var catalog = ReadCatalog(RequireDirectory(PackDirectoryVariable));
        var sprites = catalog.Sprites.ToDictionary(sprite => sprite.SpriteId);
        Assert.Equal(ExpectedSprites.Select(sprite => sprite.SpriteId).Order(), sprites.Keys.Order());

        var decodedFrames = 0;
        foreach (var expected in ExpectedSprites) {
            Assert.True(sprites.TryGetValue(expected.SpriteId, out var metadata), $"Missing catalog sprite {expected.SpriteId}.");
            Assert.True(nativeSprites.TryGetValue(expected.SpriteId, out var native), $"Missing native sprite {expected.SpriteId}.");
            Assert.Equal(expected.AlignmentWidth, metadata!.AlignmentWidth);
            Assert.Equal(expected.DirectionCount, metadata.DirectionGroupCount);
            Assert.Equal(expected.AnimationFrames, metadata.AnimationFrames);
            Assert.Equal(expected.TicksPerFrame, metadata.TicksPerFrame);
            Assert.Equal(expected.SourcePath, FindSourceHash(catalog, expected.SourcePath).Path);

            var cel = ReadSourceFile(sourceRoot, expected.SourcePath);
            Assert.Equal(FindSourceHash(catalog, expected.SourcePath).Sha256, Sha256Hex(cel));
            var decoded = NativeTownerCelDecoder.Decode(
                cel,
                expected.AlignmentWidth,
                expected.DirectionCount,
                metadata.SourceFramesPerDirection);
            Assert.Equal(expected.DirectionCount, decoded.Directions.Count);
            Assert.Equal(expected.DirectionCount, native!.Groups.Count);
            Assert.Equal(metadata.SourceFramesPerDirection, native.FrameCount);
            Assert.True(native.FrameCount >= expected.AnimationFrames);
            Assert.Equal(native.FrameCount * expected.DirectionCount, metadata.Frames.Count);

            for (var direction = 0; direction < expected.DirectionCount; direction++) {
                Assert.Equal(native.FrameCount, decoded.Directions[direction].Count);
                Assert.Equal(native.FrameCount, native.Groups[direction].Count);
                for (var frame = 0; frame < native.FrameCount; frame++) {
                    var managed = decoded.Directions[direction][frame];
                    var reference = native.Groups[direction][frame];
                    Assert.True(
                        (managed.Width, managed.Height) == (reference.Width, reference.Height),
                        $"Sprite {expected.SpriteId}, direction {direction}, frame {frame}: decoded bounds "
                        + $"{managed.Width}x{managed.Height} differ from native {reference.Width}x{reference.Height}.");
                    Assert.Equal(Interleave(managed.Indices, managed.Opacity), reference.IndexOpacity);
                    decodedFrames++;
                }
            }
        }

        Assert.Equal(nativeSprites.Values.Sum(sprite => sprite.FrameCount * sprite.DirectionCount), decodedFrames);
    }

    [Fact(Explicit = true)]
    public void MatchesEveryPublishedAtlasPixelPaddingHashAndCatalogRegionToNativeFrames()
    {
        var referenceRoot = RequireDirectory(ReferenceDirectoryVariable);
        var sourceRoot = Path.Combine(referenceRoot, "source");
        var packRoot = RequireDirectory(PackDirectoryVariable);
        var catalog = ReadCatalog(packRoot);
        var nativeSprites = ReadNativeReference(referenceRoot);
        var townPaletteBytes = ReadSourceFile(sourceRoot, NativeTownerArtCompiler.TownPalettePath);
        var paletteSource = FindSourceHash(catalog, NativeTownerArtCompiler.TownPalettePath);
        Assert.Equal(paletteSource.Sha256, Sha256Hex(townPaletteBytes));
        var palette = NativePlayerPalette.Parse(townPaletteBytes, "native Town reference palette");

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.StartsWith("native-towner-", catalog.PackId, StringComparison.Ordinal);
        Assert.NotEmpty(catalog.ProfileBindings);
        Assert.Equal(9, catalog.Sprites.Count);
        Assert.Equal(11, catalog.Residents.Count);
        var expectedResidents = NativeTownerArtCompiler.ReadBaseTownRoster(
            Path.Combine(FindRepositoryRoot(), "assets", "txtdata"));
        Assert.Equal(expectedResidents, catalog.Residents);
        Assert.Equal(ExpectedSprites.Select(sprite => sprite.SpriteId).Order(), catalog.Atlases.Select(atlas => atlas.Id).Order());
        Assert.Equal(ExpectedSprites.Select(sprite => sprite.SpriteId).Order(), catalog.Sprites.Select(sprite => sprite.SpriteId).Order());

        var expectedPngFiles = catalog.Atlases.Select(atlas => atlas.Path.Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
        var actualPngFiles = Directory.EnumerateFiles(packRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedPngFiles, actualPngFiles);

        foreach (var expectedSprite in ExpectedSprites) {
            var sprite = Assert.Single(catalog.Sprites, candidate => candidate.SpriteId == expectedSprite.SpriteId);
            var atlas = Assert.Single(catalog.Atlases, candidate => candidate.Id == expectedSprite.SpriteId);
            Assert.Equal(expectedSprite.AlignmentWidth, sprite.AlignmentWidth);
            Assert.Equal(expectedSprite.DirectionCount, sprite.DirectionGroupCount);
            Assert.Equal(expectedSprite.AnimationFrames, sprite.AnimationFrames);
            Assert.Equal(expectedSprite.TicksPerFrame, sprite.TicksPerFrame);
            Assert.True(nativeSprites.TryGetValue(sprite.SpriteId, out var native));
            Assert.Equal(native!.FrameCount, sprite.SourceFramesPerDirection);

            var expectedRgba = BuildNativeAtlasRgba(atlas, sprite, native, palette.Rgb);
            var atlasPath = ResolvePackFile(packRoot, atlas.Path);
            var pngBytes = ReadBoundedFile(atlasPath, MaximumPngBytes, $"Published atlas {atlas.Path}");
            Assert.Equal(atlas.Sha256, Sha256Hex(pngBytes));
            var published = ReadRgbaPng(pngBytes, atlas.Path);
            Assert.Equal((atlas.Width, atlas.Height), (published.Width, published.Height));
            Assert.Equal(expectedRgba, published.Rgba);

            var decoded = NativeTownerCelDecoder.Decode(
                ReadSourceFile(sourceRoot, expectedSprite.SourcePath),
                expectedSprite.AlignmentWidth,
                expectedSprite.DirectionCount,
                sprite.SourceFramesPerDirection);
            var reproduced = NativeTownerArtPng.EncodeAtlas(decoded, palette);
            Assert.Equal((atlas.Width, atlas.Height), (reproduced.Width, reproduced.Height));
            Assert.Equal(pngBytes, reproduced.Png);
            AssertSpriteRegions(sprite, atlas, native);
        }

        var expectedSources = ExpectedSprites.Select(sprite => sprite.SourcePath)
            .Append(NativeTownerArtCompiler.TownPalettePath)
            .Append($"assets/txtdata/{NativeTownerArtCompiler.TownerMetadataPath}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedSources, catalog.SourceHashes.Select(source => source.Path).Order(StringComparer.Ordinal));
        var metadataPath = Path.Combine(FindRepositoryRoot(), "assets", "txtdata", "towners", "towners.tsv");
        Assert.Equal(
            FindSourceHash(catalog, $"assets/txtdata/{NativeTownerArtCompiler.TownerMetadataPath}").Sha256,
            Sha256Hex(ReadBoundedFile(metadataPath, 1024 * 1024, "Town roster metadata")));
    }

    [Fact(Explicit = true)]
    public void ResolvesEveryResidentFrameAcrossTheNativeProcessTownersTimeline()
    {
        var referenceRoot = RequireDirectory(ReferenceDirectoryVariable);
        var packRoot = RequireDirectory(PackDirectoryVariable);
        var catalogPath = Path.Combine(packRoot, NativeTownerArtCompiler.CatalogFileName);
        var catalogBytes = ReadBoundedFile(catalogPath, NativeTownerArtCompiler.MaximumCatalogBytes, "Native towner catalog");
        var catalogJson = new System.Text.UTF8Encoding(false, true).GetString(catalogBytes);
        Assert.True(
            Devilution.Client.PresentationAssets.NativeTownerCatalog.TryParse(catalogJson, out var catalog, out var diagnostics),
            string.Join("; ", diagnostics));
        Assert.NotNull(catalog);
        Assert.Equal(11, catalog!.Residents.Count);

        var timeline = ReadNativeTimeline(Path.Combine(referenceRoot, "native-towner-timeline.tsv"));
        Assert.NotEmpty(timeline);
        Assert.Equal(0, timeline[0].Tick);
        Assert.Equal(0, timeline[0].ResidentSlot);
        Assert.Equal(0, timeline[0].Frame);
        Assert.Equal(0, timeline[0].Counter);
        Assert.Equal(0, timeline[0].OrderCursor);
        Assert.Equal(0, timeline[8].Frame);
        Assert.Equal(0, timeline[9].Frame);
        Assert.Equal(0, timeline[10].Frame);

        Assert.Equal(0, timeline.Count % catalog.Residents.Count);
        var tickCount = timeline.Count / catalog.Residents.Count;
        var requiredTickCount = 0;
        foreach (var sprite in catalog.Sprites) {
            var cycleFrames = sprite.AnimationOrder.Count > 0
                ? sprite.AnimationOrder.Count
                : sprite.AnimationFrames;
            requiredTickCount = Math.Max(
                requiredTickCount,
                checked((cycleFrames + 1) * sprite.TicksPerFrame));
        }
        Assert.True(tickCount >= requiredTickCount,
            $"The native timeline covers {tickCount} ticks; a full animation cycle plus one delay requires "
            + $"{requiredTickCount}.");

        var spritesById = catalog.Sprites.ToDictionary(sprite => sprite.Id);
        for (var tick = 0; tick < tickCount; tick++) {
            var batch = timeline.Skip(tick * catalog.Residents.Count).Take(catalog.Residents.Count).ToArray();
            Assert.Equal(Enumerable.Range(0, catalog.Residents.Count), batch.Select(row => row.ResidentSlot));
            foreach (var row in batch) {
                Assert.Equal(tick, row.Tick);
                var resident = catalog.Residents[row.ResidentSlot];
                Assert.Equal(resident.SpriteId, row.SpriteId);
                Assert.True(spritesById.TryGetValue(resident.SpriteId, out var sprite));
                Assert.Equal(tick % sprite!.TicksPerFrame, row.Counter);
                var expectedOrderCursor = sprite.AnimationOrder.Count == 0
                    ? 0
                    : checked((int)((tick / sprite.TicksPerFrame) % sprite.AnimationOrder.Count));
                Assert.Equal(expectedOrderCursor, row.OrderCursor);

                var selected = sprite.ResolveFrame(tick, resident.NativeDirection);
                Assert.Equal(selected.FrameIndex, row.Frame);
                Assert.Equal(resident.SpriteId == 8 ? resident.NativeDirection : 0, selected.DirectionIndex);
                Assert.InRange(row.Frame, 0, sprite.SourceFramesPerDirection - 1);
            }
        }
    }

    private static byte[] BuildNativeAtlasRgba(
        NativeTownerAtlas atlas,
        NativeTownerSpriteMetadata sprite,
        NativeSpriteReference native,
        byte[] palette)
    {
        Assert.InRange(atlas.Width, 1, NativeTownerArtPng.MaximumAtlasDimension);
        Assert.InRange(atlas.Height, 1, NativeTownerArtPng.MaximumAtlasDimension);
        Assert.InRange((long)atlas.Width * atlas.Height, 1, NativeTownerArtPng.MaximumAtlasPixels);
        Assert.Equal(sprite.DirectionGroupCount, native.DirectionCount);
        Assert.Equal(sprite.SourceFramesPerDirection, native.FrameCount);
        var cellWidth = atlas.Width / sprite.SourceFramesPerDirection;
        var cellHeight = atlas.Height / sprite.DirectionGroupCount;
        Assert.Equal(atlas.Width, cellWidth * sprite.SourceFramesPerDirection);
        Assert.Equal(atlas.Height, cellHeight * sprite.DirectionGroupCount);
        var rgba = new byte[checked(atlas.Width * atlas.Height * 4)];
        var seen = new HashSet<(int Group, int Frame)>();
        foreach (var region in sprite.Frames) {
            Assert.Equal(atlas.Id, region.AtlasId);
            Assert.InRange(region.Group, 0, sprite.DirectionGroupCount - 1);
            Assert.InRange(region.Frame, 0, sprite.SourceFramesPerDirection - 1);
            Assert.True(seen.Add((region.Group, region.Frame)), "Catalog contains a duplicate frame region.");
            var frame = native.Groups[region.Group][region.Frame];
            var expectedX = checked(region.Frame * cellWidth);
            var expectedY = checked(region.Group * cellHeight + cellHeight - frame.Height);
            Assert.Equal((expectedX, expectedY, frame.Width, frame.Height),
                (region.X, region.Y, region.Width, region.Height));
            Assert.InRange(region.X, 0, atlas.Width - region.Width);
            Assert.InRange(region.Y, 0, atlas.Height - region.Height);
            foreach (var other in sprite.Frames.Where(candidate =>
                         candidate.Group < region.Group
                         || candidate.Group == region.Group && candidate.Frame < region.Frame)) {
                Assert.False(Overlaps(region, other), "Catalog frame regions overlap.");
            }

            for (var y = 0; y < frame.Height; y++) {
                for (var x = 0; x < frame.Width; x++) {
                    var sourcePixel = checked(y * frame.Width + x);
                    if (frame.IndexOpacity[sourcePixel * 2 + 1] == 0)
                        continue;
                    var colorOffset = frame.IndexOpacity[sourcePixel * 2] * 3;
                    var target = checked(((region.Y + y) * atlas.Width + region.X + x) * 4);
                    rgba[target] = palette[colorOffset];
                    rgba[target + 1] = palette[colorOffset + 1];
                    rgba[target + 2] = palette[colorOffset + 2];
                    rgba[target + 3] = byte.MaxValue;
                }
            }
        }
        Assert.Equal(checked(sprite.DirectionGroupCount * sprite.SourceFramesPerDirection), seen.Count);
        return rgba;
    }

    private static void AssertSpriteRegions(
        NativeTownerSpriteMetadata sprite,
        NativeTownerAtlas atlas,
        NativeSpriteReference native)
    {
        var cellWidth = atlas.Width / sprite.SourceFramesPerDirection;
        var cellHeight = atlas.Height / sprite.DirectionGroupCount;
        var expected = new List<(int Group, int Frame, int AtlasId, int X, int Y, int Width, int Height)>();
        for (var group = 0; group < sprite.DirectionGroupCount; group++) {
            for (var frameIndex = 0; frameIndex < sprite.SourceFramesPerDirection; frameIndex++) {
                var frame = native.Groups[group][frameIndex];
                expected.Add((group, frameIndex, atlas.Id, frameIndex * cellWidth,
                    group * cellHeight + cellHeight - frame.Height, frame.Width, frame.Height));
            }
        }
        var actual = sprite.Frames.Select(frame =>
            (frame.Group, frame.Frame, frame.AtlasId, frame.X, frame.Y, frame.Width, frame.Height))
            .OrderBy(frame => frame.Group).ThenBy(frame => frame.Frame).ToArray();
        Assert.Equal(expected, actual);
    }

    private static bool Overlaps(NativeTownerFrameRegion left, NativeTownerFrameRegion right)
        => left.X < right.X + right.Width
            && right.X < left.X + left.Width
            && left.Y < right.Y + right.Height
            && right.Y < left.Y + left.Height;

    private static IReadOnlyDictionary<int, NativeSpriteReference> ReadNativeReference(string referenceRoot)
    {
        var path = Path.Combine(referenceRoot, "native-towner-reference.bin");
        var bytes = ReadBoundedFile(path, MaximumReferenceBytes, "Native towner reference");
        var offset = 0;
        Assert.Equal(1u, ReadUInt32(bytes, ref offset, "reference version"));
        var spriteCount = ReadUInt32(bytes, ref offset, "sprite count");
        Assert.Equal((uint)ExpectedSprites.Length, spriteCount);
        var sprites = new Dictionary<int, NativeSpriteReference>();
        long totalPixels = 0;
        foreach (var expected in ExpectedSprites) {
            var spriteId = checked((int)ReadUInt32(bytes, ref offset, "sprite ID"));
            var directionCount = checked((int)ReadUInt32(bytes, ref offset, "direction count"));
            var frameCount = checked((int)ReadUInt32(bytes, ref offset, "frame count"));
            Assert.Equal(expected.SpriteId, spriteId);
            Assert.Equal(expected.DirectionCount, directionCount);
            Assert.InRange(frameCount, expected.AnimationFrames, NativeTownerCelDecoder.MaximumFramesPerGroup);
            var groups = new List<IReadOnlyList<NativeFrameReference>>(directionCount);
            for (var group = 0; group < directionCount; group++) {
                Assert.Equal((uint)frameCount, ReadUInt32(bytes, ref offset, "group frame count"));
                var frames = new List<NativeFrameReference>(frameCount);
                for (var frame = 0; frame < frameCount; frame++) {
                    var width = checked((int)ReadUInt32(bytes, ref offset, "native frame width"));
                    var height = checked((int)ReadUInt32(bytes, ref offset, "native frame height"));
                    Assert.InRange(width, expected.AlignmentWidth, expected.AlignmentWidth + 64);
                    Assert.InRange(height, 1, NativeTownerCelDecoder.MaximumFrameHeight);
                    totalPixels = checked(totalPixels + (long)width * height);
                    Assert.InRange(totalPixels, 1, MaximumReferenceBytes / 2);
                    var payloadLength = checked(width * height * 2);
                    Assert.True(offset <= bytes.Length - payloadLength, "Native reference frame payload is truncated.");
                    var indexOpacity = bytes.AsSpan(offset, payloadLength).ToArray();
                    offset += payloadLength;
                    for (var pixel = 1; pixel < indexOpacity.Length; pixel += 2)
                        Assert.True(indexOpacity[pixel] is 0 or 255, "Native reference opacity must be zero or opaque.");
                    frames.Add(new NativeFrameReference(width, height, indexOpacity));
                }
                groups.Add(frames.AsReadOnly());
            }
            Assert.True(sprites.TryAdd(spriteId, new NativeSpriteReference(spriteId, directionCount, frameCount, groups.AsReadOnly())));
        }
        Assert.Equal(bytes.Length, offset);
        return sprites;
    }

    private static IReadOnlyList<TimelineRow> ReadNativeTimeline(string path)
    {
        var bytes = ReadBoundedFile(path, 64 * 1024 * 1024, "Native towner timeline");
        var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("tick\tresident_slot\tsprite_id\tframe\tcounter\torder_cursor", lines[0]);
        Assert.InRange(lines.Length, 2, 1_000_001);
        var rows = new List<TimelineRow>(lines.Length - 1);
        for (var index = 1; index < lines.Length; index++) {
            if (lines[index].Length == 0)
                continue;
            var fields = lines[index].Split('\t');
            Assert.Equal(6, fields.Length);
            rows.Add(new TimelineRow(
                ParseTimelineInt(fields[0], index, "tick"),
                ParseTimelineInt(fields[1], index, "resident_slot"),
                ParseTimelineInt(fields[2], index, "sprite_id"),
                ParseTimelineInt(fields[3], index, "frame"),
                ParseTimelineInt(fields[4], index, "counter"),
                ParseTimelineInt(fields[5], index, "order_cursor")));
        }
        Assert.NotEmpty(rows);
        return rows.AsReadOnly();
    }

    private static int ParseTimelineInt(string value, int line, string column)
    {
        Assert.True(int.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed),
            $"Native timeline line {line + 1} has an invalid {column}.");
        Assert.True(parsed >= 0, $"Native timeline line {line + 1} has a negative {column}.");
        return parsed;
    }

    private static NativeTownerArtCatalog ReadCatalog(string packRoot)
    {
        var path = Path.Combine(packRoot, NativeTownerArtCompiler.CatalogFileName);
        var bytes = ReadBoundedFile(path, NativeTownerArtCompiler.MaximumCatalogBytes, "Native towner catalog");
        var catalog = JsonSerializer.Deserialize<NativeTownerArtCatalog>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        Assert.NotNull(catalog);
        return catalog!;
    }

    private static NativeTownerSourceHash FindSourceHash(NativeTownerArtCatalog catalog, string path)
        => Assert.Single(catalog.SourceHashes, source => string.Equals(source.Path, path, StringComparison.Ordinal));

    private static byte[] ReadSourceFile(string sourceRoot, string relativePath)
        => ReadBoundedFile(ResolvePackFile(sourceRoot, relativePath), NativeTownerCelDecoder.MaximumInputBytes, $"Native source {relativePath}");

    private static string ResolvePackFile(string root, string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/');
        Assert.False(Path.IsPathRooted(relativePath));
        Assert.DoesNotContain(':', relativePath);
        Assert.DoesNotContain(segments, segment => segment.Length == 0 || segment is "." or "..");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(prefix, fullPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return fullPath;
    }

    private static byte[] ReadBoundedFile(string path, long maximumBytes, string description)
    {
        var info = new FileInfo(path);
        Assert.True(info.Exists, $"{description} is missing: {path}");
        Assert.InRange(info.Length, 1, maximumBytes);
        return File.ReadAllBytes(path);
    }

    private static (int Width, int Height, byte[] Rgba) ReadRgbaPng(byte[] png, string description)
        => NativeArtReferencePng.Read(png, NativeTownerArtPng.MaximumAtlasDimension,
            NativeTownerArtPng.MaximumAtlasPixels, description);

    private static uint ReadUInt32(byte[] bytes, ref int offset, string description)
    {
        Assert.True(offset >= 0 && offset <= bytes.Length - sizeof(uint), $"Native reference is truncated at {description}.");
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    private static byte[] Interleave(byte[] indices, byte[] opacity)
    {
        Assert.Equal(indices.Length, opacity.Length);
        var result = new byte[checked(indices.Length * 2)];
        for (var pixel = 0; pixel < indices.Length; pixel++) {
            result[pixel * 2] = indices[pixel];
            result[pixel * 2 + 1] = opacity[pixel];
        }
        return result;
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string RequireDirectory(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(path), $"Set {variable} to the explicit native fixture or pack directory.");
        Assert.True(Directory.Exists(path), $"Missing {variable} directory.");
        return Path.GetFullPath(path!);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "towners", "towners.tsv")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root for the native Town art test.");
    }

    private sealed record ExpectedSprite(
        int SpriteId,
        int AlignmentWidth,
        int DirectionCount,
        int AnimationFrames,
        int TicksPerFrame,
        string SourcePath);

    private sealed record NativeFrameReference(int Width, int Height, byte[] IndexOpacity);

    private sealed record NativeSpriteReference(
        int SpriteId,
        int DirectionCount,
        int FrameCount,
        IReadOnlyList<IReadOnlyList<NativeFrameReference>> Groups);

    private sealed record TimelineRow(
        int Tick,
        int ResidentSlot,
        int SpriteId,
        int Frame,
        int Counter,
        int OrderCursor);
}
